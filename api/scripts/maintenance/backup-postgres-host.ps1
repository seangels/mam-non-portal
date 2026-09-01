<#
.SYNOPSIS
    Backup PostgreSQL cài trực tiếp trên máy (không dùng container).

.DESCRIPTION
    Gọi `pg_dump` trên host để dump database chính ra thư mục `api/backups`
    (mặc định). `-Format custom` (mặc định) tạo file `.dump` (`pg_dump -Fc`);
    `-Format plain` tạo file `.sql` text — phù hợp để restore lên Supabase / host
    khác bằng `psql`. `-Schema`, `-NoOwner`, `-NoPrivileges` truyền thẳng cho
    pg_dump (Supabase nên dùng `-NoOwner -NoPrivileges -Schema public`).
    Tuỳ chọn kèm globals (roles/tablespaces) và cluster dump qua `pg_dumpall`.
    Tự tìm pg_dump/pg_dumpall trong PATH hoặc `C:\Program Files\PostgreSQL\*\bin`;
    có thể chỉ định bằng -PgBinDir.

    Thiếu -PgHost / -Port / -Username / -Database / -Password thì script tự đọc từ
    file `.env` (mặc định: `.env` CÙNG THƯ MỤC script; đổi bằng -EnvFile) — các key
    POSTGRES_HOST/PORT/USER/DB/PASSWORD (hoặc PGHOST/PGPORT/PGUSER/PGDATABASE/
    PGPASSWORD). Vẫn thiếu thì HỎI trực tiếp (trừ khi -NoPrompt). Thứ tự ưu tiên
    mật khẩu: -Password > .env > $env:PGPASSWORD > hỏi ẩn > pgpass.conf.

.EXAMPLE
    # Chạy không tham số -> script hỏi host/port/database/username/password
    ./backup-postgres-host.ps1

.EXAMPLE
    ./backup-postgres-host.ps1 -Username admin_portal -Database admin_portal_dev

.EXAMPLE
    # Dump SQL text sẵn sàng restore lên Supabase
    ./backup-postgres-host.ps1 -Username gv_portal_app -Database gv_portal `
      -Format plain -Schema public -NoOwner -NoPrivileges

.EXAMPLE
    ./backup-postgres-host.ps1 -PgHost localhost -Port 5432 -Username gv_portal_app -Database gv_portal -IncludeGlobals -NoPrompt
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', 'Password',
    Justification = 'libpq consumes the password as plaintext via PGPASSWORD; the interactive path uses -AsSecureString.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '',
    Justification = 'The just-entered SecureString is converted back to plaintext only to populate PGPASSWORD.')]
[CmdletBinding()]
param(
    [string]$PgHost,
    [int]$Port,
    [string]$Username,
    [string]$Database,
    [string]$Password,
    [string]$EnvFile,
    [string]$OutputDirectory,
    [string]$PgBinDir,
    [ValidateSet("custom", "plain")][string]$Format = "custom",
    [string[]]$Schema,
    [switch]$NoOwner,
    [switch]$NoPrivileges,
    [int]$KeepDays = 7,
    [switch]$IncludeGlobals,
    [switch]$ClusterDump,
    [switch]$NoPrompt
)

$ErrorActionPreference = "Stop"

function Invoke-Native {
    if ($args.Count -lt 1) {
        throw "Invoke-Native requires a command."
    }

    $filePath = [string]$args[0]
    $nativeArgs = @()
    if ($args.Count -gt 1) {
        $nativeArgs = @($args[1..($args.Count - 1)])
    }

    & $filePath @nativeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "$filePath failed with exit code $LASTEXITCODE"
    }
}

function Import-DotEnv {
    param([Parameter(Mandatory = $true)][string]$Path)

    $map = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith("#")) { continue }
        if ($trimmed -like "export *") { $trimmed = $trimmed.Substring(7).Trim() }
        $eq = $trimmed.IndexOf("=")
        if ($eq -lt 1) { continue }
        $key = $trimmed.Substring(0, $eq).Trim()
        $val = $trimmed.Substring($eq + 1).Trim()
        if ($val.Length -ge 2 -and
            (($val.StartsWith('"') -and $val.EndsWith('"')) -or ($val.StartsWith("'") -and $val.EndsWith("'")))) {
            $val = $val.Substring(1, $val.Length - 2)
        }
        $map[$key] = $val
    }
    return $map
}

function Read-Value {
    param(
        [Parameter(Mandatory = $true)][string]$Prompt,
        [string]$Current,
        [string]$Default
    )

    if (-not [string]::IsNullOrWhiteSpace($Current)) {
        return $Current
    }
    if ($NoPrompt) {
        if (-not [string]::IsNullOrWhiteSpace($Default)) {
            return $Default
        }
        throw "Thiếu giá trị '$Prompt' và -NoPrompt đang bật."
    }

    $label = if ([string]::IsNullOrWhiteSpace($Default)) { $Prompt } else { "$Prompt [$Default]" }
    $entered = Read-Host $label
    if ([string]::IsNullOrWhiteSpace($entered)) {
        if (-not [string]::IsNullOrWhiteSpace($Default)) {
            return $Default
        }
        throw "'$Prompt' là bắt buộc."
    }
    return $entered
}

function Resolve-PgTool {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$BinDir
    )

    if (-not [string]::IsNullOrWhiteSpace($BinDir)) {
        $explicit = Join-Path $BinDir "$Name.exe"
        if (Test-Path -LiteralPath $explicit) {
            return $explicit
        }
        throw "$Name not found in -PgBinDir '$BinDir'."
    }

    $onPath = Get-Command $Name -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }

    $guess = Get-ChildItem "C:\Program Files\PostgreSQL\*\bin\$Name.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($guess) {
        return $guess.FullName
    }

    throw "$Name not found. Add the PostgreSQL bin folder to PATH or pass -PgBinDir."
}

# --- Nạp .env (mặc định: file '.env' cùng thư mục script) ---
$envMap = @{}
$envPath = if (-not [string]::IsNullOrWhiteSpace($EnvFile)) { $EnvFile } else { Join-Path $PSScriptRoot ".env" }
if (Test-Path -LiteralPath $envPath) {
    $envMap = Import-DotEnv -Path (Resolve-Path -LiteralPath $envPath)
    Write-Verbose "Đã nạp env file: $envPath"
}
elseif (-not [string]::IsNullOrWhiteSpace($EnvFile)) {
    throw "EnvFile không tồn tại: $EnvFile"
}

function Get-DotEnvValue {
    param([string[]]$Names)
    foreach ($n in $Names) {
        if ($envMap.ContainsKey($n) -and -not [string]::IsNullOrWhiteSpace($envMap[$n])) {
            return $envMap[$n]
        }
    }
    return $null
}

# --- Thu thập thông tin kết nối (tham số > .env > hỏi trực tiếp) ---
if ([string]::IsNullOrWhiteSpace($PgHost)) {
    $v = Get-DotEnvValue @("POSTGRES_HOST", "PGHOST"); if ($v) { $PgHost = $v }
}
if (-not $PSBoundParameters.ContainsKey("Port") -and $Port -le 0) {
    $v = Get-DotEnvValue @("POSTGRES_PORT", "PGPORT"); if ($v) { $Port = [int]$v }
}
if ([string]::IsNullOrWhiteSpace($Username)) {
    $v = Get-DotEnvValue @("POSTGRES_USER", "PGUSER"); if ($v) { $Username = $v }
}
if ([string]::IsNullOrWhiteSpace($Database)) {
    $v = Get-DotEnvValue @("POSTGRES_DB", "PGDATABASE"); if ($v) { $Database = $v }
}

$PgHost = Read-Value -Prompt "Host PostgreSQL" -Current $PgHost -Default "localhost"
if (-not $PSBoundParameters.ContainsKey("Port") -and $Port -le 0) {
    $Port = [int](Read-Value -Prompt "Cổng" -Default "5432")
}
elseif ($Port -le 0) {
    $Port = 5432
}
$Username = Read-Value -Prompt "Username" -Current $Username
$Database = Read-Value -Prompt "Database cần backup" -Current $Database

$passwordProvided = $PSBoundParameters.ContainsKey("Password")
if (-not $passwordProvided) {
    $v = Get-DotEnvValue @("POSTGRES_PASSWORD", "PGPASSWORD")
    if ($v) { $Password = $v; $passwordProvided = $true }
}
if (-not $passwordProvided -and [string]::IsNullOrWhiteSpace($env:PGPASSWORD) -and -not $NoPrompt) {
    $secure = Read-Host "Mật khẩu cho ${Username}@${PgHost}:$Port" -AsSecureString
    if ($secure.Length -gt 0) {
        $Password = [System.Net.NetworkCredential]::new('', $secure).Password
        $passwordProvided = $true
    }
}

$pgDump = Resolve-PgTool -Name "pg_dump" -BinDir $PgBinDir
$pgDumpAll = $null
if ($IncludeGlobals -or $ClusterDump) {
    $pgDumpAll = Resolve-PgTool -Name "pg_dumpall" -BinDir $PgBinDir
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "backups"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$createdFiles = New-Object System.Collections.Generic.List[string]

$passwordWasSet = $false
$previousPassword = $env:PGPASSWORD
if ($passwordProvided) {
    $env:PGPASSWORD = $Password
    $passwordWasSet = $true
}

$dumpFormatArg = if ($Format -eq "plain") { "-Fp" } else { "-Fc" }
$dumpExtension = if ($Format -eq "plain") { "sql" } else { "dump" }
$dumpExtraArgs = @()
foreach ($s in $Schema) { $dumpExtraArgs += @("--schema", $s) }
if ($NoOwner) { $dumpExtraArgs += "--no-owner" }
if ($NoPrivileges) { $dumpExtraArgs += "--no-privileges" }

try {
    $dbBackupPath = Join-Path $OutputDirectory "postgres-db-$Database-$timestamp.$dumpExtension"
    Invoke-Native $pgDump -h $PgHost -p $Port -U $Username -d $Database $dumpFormatArg @dumpExtraArgs -f $dbBackupPath
    $createdFiles.Add($dbBackupPath)

    if ($IncludeGlobals) {
        $globalsBackupPath = Join-Path $OutputDirectory "postgres-globals-$timestamp.sql"
        Invoke-Native $pgDumpAll -h $PgHost -p $Port -U $Username --globals-only -f $globalsBackupPath
        $createdFiles.Add($globalsBackupPath)
    }

    if ($ClusterDump) {
        $clusterBackupPath = Join-Path $OutputDirectory "postgres-all-$timestamp.sql"
        Invoke-Native $pgDumpAll -h $PgHost -p $Port -U $Username --clean --if-exists -f $clusterBackupPath
        $createdFiles.Add($clusterBackupPath)
    }
}
finally {
    if ($passwordWasSet) {
        if ($null -eq $previousPassword) {
            Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
        }
        else {
            $env:PGPASSWORD = $previousPassword
        }
    }
}

if ($KeepDays -gt 0) {
    $cutoff = (Get-Date).AddDays(-$KeepDays)
    Get-ChildItem -LiteralPath $OutputDirectory -File -Include "postgres-db-*.dump", "postgres-db-*.sql", "postgres-globals-*.sql", "postgres-all-*.sql" |
        Where-Object { $_.LastWriteTime -lt $cutoff } |
        Remove-Item -Force
}

$createdFiles | ForEach-Object { Get-Item -LiteralPath $_ }
