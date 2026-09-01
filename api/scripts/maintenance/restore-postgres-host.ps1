<#
.SYNOPSIS
    Restore file backup vào một PostgreSQL host đang online (kể cả Supabase).

.DESCRIPTION
    Thao tác PHÁ HUỶ dữ liệu: nạp file `.dump` (custom format, qua `pg_restore
    --no-owner --no-acl`) hoặc `.sql` (plain, qua `psql`) vào một PostgreSQL đang
    chạy — cục bộ, server online, hoặc Supabase.

    Client tool (`pg_restore` / `psql` / `createdb` / `dropdb`) lấy từ:
      * Mặc định: cài trực tiếp trên máy (PATH, -PgBinDir, hoặc
        `C:\Program Files\PostgreSQL\*\bin`).
      * `-ToolContainer <tên>`: chạy tool BÊN TRONG container Docker/Podman rồi
        kết nối ra đích online (hữu ích khi máy không cài client, hoặc Supabase
        direct connection chỉ có IPv6 còn container có IPv4).

    Chọn đích theo 1 trong 2 cách:
      * Rời rạc: -PgHost / -Port / -Username / -Database (hỏi trực tiếp nếu thiếu).
      * -ConnectionString "postgresql://user:pass@host:port/db" — dùng nguyên chuỗi
        (tiện cho chuỗi copy từ dashboard Supabase). Mật khẩu có thể nằm trong chuỗi.

    -Supabase: bật preset cho Supabase — `sslmode=require`, không `--clean` (nạp vào
    schema rỗng), cấm tạo/drop database, và in ghi chú kết nối. Vẫn cần -ConnectionString
    hoặc -PgHost trỏ tới project.

    Thiếu thông tin kết nối / -Password thì script tự đọc từ file `.env` (mặc định:
    `.env` CÙNG THƯ MỤC script; đổi bằng -EnvFile) — key POSTGRES_HOST/PORT/USER/DB/
    PASSWORD (hoặc PG*). Vẫn thiếu thì HỎI trực tiếp (trừ khi -NoPrompt). Bắt buộc `-Force`.
    Thứ tự ưu tiên mật khẩu: -Password > chuỗi trong -ConnectionString > .env >
    $env:PGPASSWORD > hỏi ẩn.

    Database đích và user có thể KHÁC lúc backup:
      * `.dump` custom format không gắn cứng tên database — nạp vào DB nào tuỳ đích.
      * `--no-owner --no-acl` bỏ qua owner/grant gốc; object thuộc về user đang kết nối.
      * `-CreateDatabase` / `-RecreateDatabase` (chỉ `.dump`, KHÔNG dùng với -Supabase
        hay -ConnectionString).

    Mật khẩu: -Password, chuỗi trong -ConnectionString, $env:PGPASSWORD, hoặc
    %APPDATA%\postgresql\pgpass.conf (pgpass chỉ dùng ở chế độ tool cài trực tiếp).

.EXAMPLE
    # Tool cài trực tiếp, hỏi host/port/username/database/password
    ./restore-postgres-host.ps1 -BackupFile ./backups/postgres-db-admin_portal-20260901-120000.dump -Force

.EXAMPLE
    # Restore .sql lên Supabase bằng connection string của Session pooler
    ./restore-postgres-host.ps1 -Supabase -Force `
      -BackupFile .\backups\postgres-db-gv_portal-20260901-120000.sql `
      -ConnectionString "postgresql://postgres.abcdxyz:PWD@aws-0-ap-southeast-1.pooler.supabase.com:5432/postgres"

.EXAMPLE
    # Máy chỉ có Docker: client tool trong container -> Supabase
    ./restore-postgres-host.ps1 -Supabase -Force -ToolContainer api_postgres_1 `
      -BackupFile .\backups\postgres-db-gv_portal-20260901-120000.dump `
      -PgHost aws-0-ap-southeast-1.pooler.supabase.com -Port 5432 `
      -Username postgres.abcdxyz -Database postgres
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', 'Password',
    Justification = 'libpq consumes the password as plaintext via PGPASSWORD; the interactive path uses -AsSecureString.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '',
    Justification = 'The just-entered SecureString is converted back to plaintext only to populate PGPASSWORD / docker exec -e.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', 'Database',
    Justification = 'Used to build the connection args when -ConnectionString is not supplied.')]
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$BackupFile,
    [string]$Username,
    [string]$Database,
    [string]$PgHost,
    [int]$Port,
    [string]$Password,
    [string]$EnvFile,
    [string]$ConnectionString,
    [string]$Sslmode,
    [switch]$Supabase,
    [string]$PgBinDir,
    [string]$ToolContainer,
    [ValidateSet("auto", "podman", "docker")][string]$Engine = "auto",
    [string]$MaintenanceDatabase = "postgres",
    [switch]$CreateDatabase,
    [switch]$RecreateDatabase,
    [switch]$NoClean,
    [switch]$SingleTransaction,
    [switch]$NoPrompt,
    [switch]$Force
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

function Invoke-NativeCapture {
    if ($args.Count -lt 1) {
        throw "Invoke-NativeCapture requires a command."
    }

    $filePath = [string]$args[0]
    $nativeArgs = @()
    if ($args.Count -gt 1) {
        $nativeArgs = @($args[1..($args.Count - 1)])
    }

    $output = & $filePath @nativeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "$filePath failed with exit code $LASTEXITCODE"
    }
    return $output
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

function Resolve-ContainerEngine {
    param([Parameter(Mandatory = $true)][string]$Preference)

    $candidates = switch ($Preference) {
        "podman" { @("podman") }
        "docker" { @("docker") }
        default  { @("podman", "docker") }
    }

    foreach ($candidate in $candidates) {
        if (Get-Command $candidate -ErrorAction SilentlyContinue) {
            return $candidate
        }
    }

    throw "No container engine found for preference '$Preference'. Install Podman or Docker, or pass -Engine explicitly."
}

function Hide-Secret {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Text }
    return ($Text -replace '(://[^:@/]+:)[^@/]+@', '${1}****@')
}

if (-not $Force) {
    throw "Restore is destructive. Re-run with -Force after confirming the target and backup file."
}

$backupPath = Resolve-Path -LiteralPath $BackupFile
$backupItem = Get-Item -LiteralPath $backupPath
$extension = $backupItem.Extension.ToLowerInvariant()

if ($extension -ne ".dump" -and $extension -ne ".sql") {
    throw "Unsupported backup extension '$($backupItem.Extension)'. Use a .dump (pg_dump -Fc) or a .sql dump."
}

# --- Nạp .env (mặc định: file '.env' cùng thư mục script) ---
$envMap = @{}
$envIsExplicit = -not [string]::IsNullOrWhiteSpace($EnvFile)
$envPath = if ($envIsExplicit) { $EnvFile } else { Join-Path $PSScriptRoot ".env" }
if (Test-Path -LiteralPath $envPath) {
    $envMap = Import-DotEnv -Path (Resolve-Path -LiteralPath $envPath)
    Write-Verbose "Đã nạp env file: $envPath"
}
elseif ($envIsExplicit) {
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

# -Supabase + khoá SUPABASE_* rời trong .env: dùng thẳng host/user/password rời
# (bỏ qua SUPABASE_CONNECTION_URL vì password Supabase hay chứa ký tự reserved
#  như [ ] làm hỏng phân giải URI).
$sbHost = Get-DotEnvValue @("SUPABASE_HOST", "SUPABASE_DB_HOST")
$sbUser = Get-DotEnvValue @("SUPABASE_DB_USER", "SUPABASE_USER")
$sbPass = Get-DotEnvValue @("SUPABASE_DB_PASSWORD", "SUPABASE_PASSWORD")
$sbDb = Get-DotEnvValue @("SUPABASE_DB", "SUPABASE_DATABASE")
$sbDiscrete = [bool]($Supabase -and $sbHost -and $sbUser -and $sbPass)

if ([string]::IsNullOrWhiteSpace($ConnectionString) -and -not $sbDiscrete) {
    $v = Get-DotEnvValue @("SUPABASE_CONNECTION_URL", "SUPABASE_DB_URL", "DATABASE_URL", "CONNECTION_STRING", "PG_CONNECTION_STRING")
    if ($v) { $ConnectionString = $v }
}

$useContainer = -not [string]::IsNullOrWhiteSpace($ToolContainer)
$useConnString = -not [string]::IsNullOrWhiteSpace($ConnectionString)

if ($Supabase -and -not [string]::IsNullOrWhiteSpace($Sslmode) -and $Sslmode -ne "require" -and $Sslmode -ne "verify-full" -and $Sslmode -ne "verify-ca") {
    Write-Warning "Supabase yêu cầu SSL; -Sslmode '$Sslmode' có thể bị từ chối."
}
if ($Supabase -and [string]::IsNullOrWhiteSpace($Sslmode)) {
    $Sslmode = "require"
}
if ($Supabase) {
    $NoClean = $true
}

if (($CreateDatabase -or $RecreateDatabase) -and ($Supabase -or $useConnString)) {
    throw "-CreateDatabase / -RecreateDatabase không dùng được với -Supabase hoặc -ConnectionString (không tạo/drop được database quản trị từ xa)."
}
if ($extension -eq ".sql" -and ($CreateDatabase -or $RecreateDatabase)) {
    throw "-CreateDatabase / -RecreateDatabase chỉ dùng với file .dump."
}

# --- Thu thập thông tin kết nối ---
$connEmbedsPassword = $false
if ($useConnString) {
    if ($PSBoundParameters.ContainsKey("PgHost") -or $PSBoundParameters.ContainsKey("Port") -or
        $PSBoundParameters.ContainsKey("Username") -or $PSBoundParameters.ContainsKey("Database")) {
        Write-Warning "Đã có -ConnectionString nên bỏ qua -PgHost/-Port/-Username/-Database."
    }
    $connEmbedsPassword = $ConnectionString -match '^[a-zA-Z][a-zA-Z0-9+.\-]*://[^/@]+:[^/@]+@'
}
else {
    if ($sbDiscrete) {
        if ([string]::IsNullOrWhiteSpace($PgHost)) { $PgHost = $sbHost }
        if ([string]::IsNullOrWhiteSpace($Username)) { $Username = $sbUser }
        if ([string]::IsNullOrWhiteSpace($Database)) { $Database = if ($sbDb) { $sbDb } else { "postgres" } }
    }
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

    $defaultHost = if ($useContainer) { "host.docker.internal" } else { "localhost" }
    $PgHost = Read-Value -Prompt "Host PostgreSQL online" -Current $PgHost -Default $defaultHost
    if (-not $PSBoundParameters.ContainsKey("Port") -and $Port -le 0) {
        $Port = [int](Read-Value -Prompt "Cổng" -Default "5432")
    }
    elseif ($Port -le 0) {
        $Port = 5432
    }
    $Username = Read-Value -Prompt "Username" -Current $Username
    $dbDefault = if ($Supabase) { "postgres" } else { "" }
    $Database = Read-Value -Prompt "Database đích" -Current $Database -Default $dbDefault

    if ($useContainer -and $PgHost -in @("localhost", "127.0.0.1", "::1")) {
        Write-Warning "PgHost='$PgHost' là loopback CỦA CONTAINER, không phải máy host. Dùng host.docker.internal hoặc hostname/IP thật."
    }
}

$passwordProvided = $PSBoundParameters.ContainsKey("Password")
if (-not $passwordProvided -and -not $connEmbedsPassword -and $sbDiscrete) {
    $Password = $sbPass
    $passwordProvided = $true
}
if (-not $passwordProvided -and -not $connEmbedsPassword) {
    $v = Get-DotEnvValue @("POSTGRES_PASSWORD", "PGPASSWORD")
    if ($v) {
        $Password = $v
        $passwordProvided = $true
        if ($useConnString -and -not $envIsExplicit) {
            Write-Warning "Đang dùng mật khẩu từ .env cạnh script cho -ConnectionString — kiểm tra lại nếu đích là Supabase/host khác."
        }
    }
}
if (-not $passwordProvided -and -not $connEmbedsPassword -and
    [string]::IsNullOrWhiteSpace($env:PGPASSWORD) -and -not $NoPrompt) {
    $who = if ($useConnString) { Hide-Secret $ConnectionString } else { "${Username}@${PgHost}:$Port" }
    $secure = Read-Host "Mật khẩu cho $who" -AsSecureString
    if ($secure.Length -gt 0) {
        $Password = [System.Net.NetworkCredential]::new('', $secure).Password
        $passwordProvided = $true
    }
}
$effectivePassword = if ($passwordProvided) { $Password } else { $env:PGPASSWORD }

if ($Supabase) {
    Write-Host "Supabase preset: sslmode=$Sslmode, no --clean." -ForegroundColor Cyan
    Write-Host "  - Use the Session pooler (port 5432, user 'postgres.<ref>') or a direct connection." -ForegroundColor Cyan
    Write-Host "  - Make the dump with -Format plain -NoOwner -NoPrivileges -Schema public." -ForegroundColor Cyan
    Write-Host "  - Do not restore globals/roles (pg_dumpall); Supabase manages them." -ForegroundColor Cyan
}

# --- Chuẩn bị cách gọi client tool ---
$engineCmd = $null
if ($useContainer) {
    $engineCmd = Resolve-ContainerEngine -Preference $Engine
    $state = & $engineCmd inspect $ToolContainer --format "{{.State.Status}}"
    if ($LASTEXITCODE -ne 0 -or $state -ne "running") {
        throw "Container $ToolContainer is not running. Start it before restoring."
    }
}

function Get-PgToolCommand {
    param([Parameter(Mandatory = $true)][string]$Tool)

    if ($useContainer) {
        $prefix = @($engineCmd, "exec")
        if (-not [string]::IsNullOrWhiteSpace($effectivePassword)) {
            $prefix += @("-e", "PGPASSWORD=$effectivePassword")
        }
        if (-not [string]::IsNullOrWhiteSpace($Sslmode)) {
            $prefix += @("-e", "PGSSLMODE=$Sslmode")
        }
        $prefix += @($ToolContainer, $Tool)
        return $prefix
    }

    return @((Resolve-PgTool -Name $Tool -BinDir $PgBinDir))
}

# Cụm tham số kết nối cho tool
if ($useConnString) {
    $connArgs = @("-d", $ConnectionString)
    $psqlConnArgs = @("-d", $ConnectionString)
}
else {
    $connArgs = @("-h", $PgHost, "-p", $Port, "-U", $Username, "-d", $Database)
    $psqlConnArgs = @("-h", $PgHost, "-p", $Port, "-U", $Username, "-d", $MaintenanceDatabase)
}

# Resolve/verify tools sớm để fail nhanh trước ShouldProcess
$psqlCmd = Get-PgToolCommand -Tool "psql"
$pgRestoreCmd = $null
$createdbCmd = $null
$dropdbCmd = $null
if ($extension -eq ".dump") {
    $pgRestoreCmd = Get-PgToolCommand -Tool "pg_restore"
    if ($CreateDatabase -or $RecreateDatabase) {
        $createdbCmd = Get-PgToolCommand -Tool "createdb"
    }
    if ($RecreateDatabase) {
        $dropdbCmd = Get-PgToolCommand -Tool "dropdb"
    }
}

$displayTarget = if ($useConnString) { Hide-Secret $ConnectionString }
elseif ($extension -eq ".dump") { "$PgHost`:$Port/$Database" }
else { "$PgHost`:$Port/$MaintenanceDatabase" }

if (-not $PSCmdlet.ShouldProcess($displayTarget, "Restore $($backupItem.Name)")) {
    return
}

# Đường dẫn file mà tool sẽ đọc: trong container thì copy vào /tmp trước
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$toolFilePath = $backupItem.FullName
$containerFilePath = $null
if ($useContainer) {
    $containerFilePath = "/tmp/restore-$timestamp$extension"
    Invoke-Native $engineCmd cp $backupItem.FullName "${ToolContainer}:$containerFilePath"
    $toolFilePath = $containerFilePath
}

$envWasSet = @{}
$envPrevious = @{ PGPASSWORD = $env:PGPASSWORD; PGSSLMODE = $env:PGSSLMODE }
if (-not $useContainer) {
    if ($passwordProvided) {
        $env:PGPASSWORD = $Password
        $envWasSet.PGPASSWORD = $true
    }
    if (-not [string]::IsNullOrWhiteSpace($Sslmode)) {
        $env:PGSSLMODE = $Sslmode
        $envWasSet.PGSSLMODE = $true
    }
}

try {
    if ($extension -eq ".dump") {
        if ($RecreateDatabase) {
            Invoke-Native @dropdbCmd -h $PgHost -p $Port -U $Username --maintenance-db=$MaintenanceDatabase --if-exists $Database
        }

        if ($CreateDatabase -or $RecreateDatabase) {
            $exists = Invoke-NativeCapture @psqlCmd -h $PgHost -p $Port -U $Username -d $MaintenanceDatabase -tAc "SELECT 1 FROM pg_database WHERE datname = '$Database'"
            if (("$exists").Trim() -ne "1") {
                Invoke-Native @createdbCmd -h $PgHost -p $Port -U $Username --maintenance-db=$MaintenanceDatabase -O $Username $Database
            }
        }

        $restoreArgs = @()
        if (-not $NoClean) { $restoreArgs += @("--clean", "--if-exists") }
        if ($SingleTransaction) { $restoreArgs += "--single-transaction" }
        Invoke-Native @pgRestoreCmd @connArgs @restoreArgs --no-owner --no-acl $toolFilePath
    }
    else {
        $psqlArgs = @("-v", "ON_ERROR_STOP=1")
        if ($SingleTransaction) { $psqlArgs += "--single-transaction" }
        Invoke-Native @psqlCmd @psqlConnArgs @psqlArgs -f $toolFilePath
    }
}
finally {
    if ($useContainer -and $containerFilePath) {
        & $engineCmd exec $ToolContainer rm -f $containerFilePath | Out-Null
    }
    foreach ($name in @("PGPASSWORD", "PGSSLMODE")) {
        if ($envWasSet[$name]) {
            if ($null -eq $envPrevious[$name]) {
                Remove-Item "Env:\$name" -ErrorAction SilentlyContinue
            }
            else {
                Set-Item "Env:\$name" -Value $envPrevious[$name]
            }
        }
    }
}

Write-Output "Restore completed for $($backupItem.FullName) into $displayTarget."
