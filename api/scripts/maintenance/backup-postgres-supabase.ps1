<#
.SYNOPSIS
    Backup database tu Supabase ra file .sql (hoac .dump).

.DESCRIPTION
    Goi pg_dump toi mot project Supabase va luu ket qua vao thu muc api/backups.
    Mac dinh -Format plain => file .sql, restore lai duoc bang restore-postgres-host.ps1.

    Thong tin ket noi lay theo thu tu:
      1. -ConnectionString truyen vao.
      2. SUPABASE_CONNECTION_URL trong file .env (mac dinh: .env CUNG THU MUC script;
         doi bang -EnvFile).
      3. Khoa roi SUPABASE_HOST / SUPABASE_DB_USER / SUPABASE_DB_PASSWORD / SUPABASE_DB
         trong .env (dung khi password chua ky tu reserved lam hong phan giai URI).

    pg_dump lay tu:
      * Mac dinh: cai truc tiep tren may (PATH, -PgBinDir, C:\Program Files\PostgreSQL\*\bin).
      * -ToolContainer <ten>: chay pg_dump BEN TRONG container Docker/Podman roi copy
        file ra host. Dung khi may khong cai PostgreSQL client.

    Mac dinh phu hop Supabase: --schema=public, --no-owner, --no-privileges,
    sslmode=require. Khong ho tro pg_dumpall/globals vi Supabase khong cap superuser.

    Voi -Format plain, script sua nhe file .sql sau khi dump de restore duoc vao mot
    database da co san schema public: doi "CREATE SCHEMA public;" thanh
    "CREATE SCHEMA IF NOT EXISTS public;" va bo dong "COMMENT ON SCHEMA public".
    Tat bang -NoSqlFixups.

.EXAMPLE
    # Doc .env, dump schema public ra .sql, chay pg_dump trong container
    ./backup-postgres-supabase.ps1 -ToolContainer api_postgres_1

.EXAMPLE
    # Toan bo schema, custom format
    ./backup-postgres-supabase.ps1 -ToolContainer api_postgres_1 -Format custom -Schema @()

.EXAMPLE
    # Chi cau truc, khong data
    ./backup-postgres-supabase.ps1 -ToolContainer api_postgres_1 -SchemaOnly
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', 'Password',
    Justification = 'libpq consumes the password as plaintext via PGPASSWORD.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '',
    Justification = 'The just-entered SecureString is converted back to plaintext only to populate PGPASSWORD.')]
[CmdletBinding()]
param(
    [string]$ConnectionString,
    [string]$EnvFile,
    [string]$ConfigFile,
    [string]$Password,
    [string]$OutputDirectory,
    [ValidateSet("plain", "custom")][string]$Format = "plain",
    [string[]]$Schema = @("public"),
    [string[]]$ExcludeSchema,
    [switch]$SchemaOnly,
    [switch]$DataOnly,
    [switch]$IncludeOwner,
    [switch]$Clean,
    [switch]$Compress,
    [string]$Sslmode = "require",
    [string]$PgBinDir,
    [string]$ToolContainer,
    [ValidateSet("auto", "podman", "docker")][string]$Engine = "auto",
    [int]$KeepDays = 7,
    [switch]$NoSqlFixups,
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

function Get-ConfigValue {
    param([hashtable]$Config, [string]$Name, $Default)
    if ($Config.ContainsKey($Name) -and $null -ne $Config[$Name] -and "$($Config[$Name])".Trim().Length -gt 0) {
        return $Config[$Name]
    }
    return $Default
}

function Resolve-ConfiguredPath {
    param([string]$Path, [string]$BaseDir)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    $expanded = [Environment]::ExpandEnvironmentVariables($Path)
    if ([System.IO.Path]::IsPathRooted($expanded)) { return [System.IO.Path]::GetFullPath($expanded) }
    return [System.IO.Path]::GetFullPath((Join-Path $BaseDir $expanded))
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

function Resolve-PgTool {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$BinDir
    )

    # 1. -PgBinDir chi dinh ro rang
    if (-not [string]::IsNullOrWhiteSpace($BinDir)) {
        $explicit = Join-Path $BinDir "$Name.exe"
        if (Test-Path -LiteralPath $explicit) { return $explicit }
        throw "$Name.exe not found in -PgBinDir '$BinDir'."
    }

    # 2. Cung thu muc voi script (pg_dump.exe / pg_restore.exe / psql.exe + DLL di kem)
    $local = Join-Path $PSScriptRoot "$Name.exe"
    if (Test-Path -LiteralPath $local) { return $local }

    # 3. PATH
    $onPath = Get-Command $Name -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    # 4. Ban PostgreSQL cai tren may
    $guess = Get-ChildItem "C:\Program Files\PostgreSQL\*\bin\$Name.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($guess) { return $guess.FullName }

    # 5. Bo client di kem DBeaver
    $dbeaver = Get-ChildItem (Join-Path $env:APPDATA "DBeaverData\drivers\clients\postgresql\win\*\$Name.exe") -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($dbeaver) { return $dbeaver.FullName }

    throw "$Name.exe not found. Dat $Name.exe (kem DLL libpq/libssl/libcrypto/...) canh script, them vao PATH, hoac dung -PgBinDir / -ToolContainer."
}

function Resolve-ContainerEngine {
    param([Parameter(Mandatory = $true)][string]$Preference)

    $candidates = switch ($Preference) {
        "podman" { @("podman") }
        "docker" { @("docker") }
        default  { @("podman", "docker") }
    }

    foreach ($candidate in $candidates) {
        if (Get-Command $candidate -ErrorAction SilentlyContinue) { return $candidate }
    }

    throw "No container engine found for preference '$Preference'. Install Podman or Docker, or pass -Engine explicitly."
}

function Hide-Secret {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Text }
    return ($Text -replace '(://[^:@/]+:)[^@/]+@', '${1}****@')
}

if ($SchemaOnly -and $DataOnly) {
    throw "-SchemaOnly and -DataOnly are mutually exclusive."
}

# --- Nap .env ---
$envMap = @{}
$envIsExplicit = -not [string]::IsNullOrWhiteSpace($EnvFile)
$envPath = if ($envIsExplicit) { $EnvFile } else { Join-Path $PSScriptRoot ".env" }
if (Test-Path -LiteralPath $envPath) {
    $envMap = Import-DotEnv -Path (Resolve-Path -LiteralPath $envPath)
    Write-Verbose "Loaded env file: $envPath"
}
elseif ($envIsExplicit) {
    throw "EnvFile not found: $EnvFile"
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

# --- Tham so ket noi cho pg_dump ---
$sbHost = Get-DotEnvValue @("SUPABASE_HOST", "SUPABASE_DB_HOST")
$sbUser = Get-DotEnvValue @("SUPABASE_DB_USER", "SUPABASE_USER")
$sbPass = Get-DotEnvValue @("SUPABASE_DB_PASSWORD", "SUPABASE_PASSWORD")
$sbDb = Get-DotEnvValue @("SUPABASE_DB", "SUPABASE_DATABASE")
$sbPort = Get-DotEnvValue @("SUPABASE_PORT", "SUPABASE_DB_PORT")

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = Get-DotEnvValue @("SUPABASE_CONNECTION_URL", "SUPABASE_DB_URL", "DATABASE_URL")
}

$effectivePassword = if ($PSBoundParameters.ContainsKey("Password")) { $Password } else { $sbPass }
$databaseLabel = "supabase"

if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
    $connArgs = @("-d", $ConnectionString)
    $displayTarget = Hide-Secret $ConnectionString
    if ($ConnectionString -match '/([^/?]+)(\?|$)') { $databaseLabel = $matches[1] }
    if ($ConnectionString -match '^[a-zA-Z][a-zA-Z0-9+.\-]*://[^/@]+:[^/@]+@') {
        # Password nam trong URI roi, khong can PGPASSWORD
        if (-not $PSBoundParameters.ContainsKey("Password")) { $effectivePassword = $null }
    }
}
elseif ($sbHost -and $sbUser) {
    $port = if ($sbPort) { [int]$sbPort } else { 5432 }
    $db = if ($sbDb) { $sbDb } else { "postgres" }
    $connArgs = @("-h", $sbHost, "-p", $port, "-U", $sbUser, "-d", $db)
    $displayTarget = "${sbHost}:$port/$db"
    $databaseLabel = $db
}
else {
    throw "Khong tim thay thong tin ket noi Supabase. Dat SUPABASE_CONNECTION_URL (hoac SUPABASE_HOST/SUPABASE_DB_USER/SUPABASE_DB_PASSWORD) trong '$envPath', hoac truyen -ConnectionString."
}

if ([string]::IsNullOrWhiteSpace($effectivePassword) -and
    [string]::IsNullOrWhiteSpace($env:PGPASSWORD) -and
    $connArgs[0] -ne "-d" -and -not $NoPrompt) {
    $secure = Read-Host "Supabase password for $displayTarget" -AsSecureString
    if ($secure.Length -gt 0) {
        $effectivePassword = [System.Net.NetworkCredential]::new('', $secure).Password
    }
}

# --- pg_dump chay o dau ---
$useContainer = -not [string]::IsNullOrWhiteSpace($ToolContainer)
$engineCmd = $null
if ($useContainer) {
    $engineCmd = Resolve-ContainerEngine -Preference $Engine
    $state = & $engineCmd inspect $ToolContainer --format "{{.State.Status}}"
    if ($LASTEXITCODE -ne 0 -or $state -ne "running") {
        throw "Container $ToolContainer is not running. Start it before backing up."
    }

    $pgDumpCmd = @($engineCmd, "exec")
    if (-not [string]::IsNullOrWhiteSpace($effectivePassword)) {
        $pgDumpCmd += @("-e", "PGPASSWORD=$effectivePassword")
    }
    if (-not [string]::IsNullOrWhiteSpace($Sslmode)) {
        $pgDumpCmd += @("-e", "PGSSLMODE=$Sslmode")
    }
    $pgDumpCmd += @($ToolContainer, "pg_dump")
}
else {
    $pgDumpCmd = @((Resolve-PgTool -Name "pg_dump" -BinDir $PgBinDir))
}

# --- File ket qua ---
# --- Doc backup-config.json ---
$appConfig = @{}
$appConfigPath = if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) { $ConfigFile } else { Join-Path $PSScriptRoot "backup-config.json" }
if (Test-Path -LiteralPath $appConfigPath) {
    $rawCfg = Get-Content -LiteralPath $appConfigPath -Raw | ConvertFrom-Json
    foreach ($prop in $rawCfg.PSObject.Properties) {
        if (-not $prop.Name.StartsWith("//")) { $appConfig[$prop.Name] = $prop.Value }
    }
    Write-Verbose "Loaded config: $appConfigPath"
}
elseif (-not [string]::IsNullOrWhiteSpace($ConfigFile)) {
    throw "ConfigFile not found: $ConfigFile"
}

if (-not $PSBoundParameters.ContainsKey("Compress")) {
    if ([bool](Get-ConfigValue $appConfig "compress" $false)) { $Compress = [switch]$true }
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $fromCfg = Get-ConfigValue $appConfig "outputDirectory" $null
    if ($fromCfg) { $OutputDirectory = Resolve-ConfiguredPath -Path $fromCfg -BaseDir $PSScriptRoot }
    else { $OutputDirectory = Join-Path $repoRoot "backups" }
}
else {
    $OutputDirectory = Resolve-ConfiguredPath -Path $OutputDirectory -BaseDir $PSScriptRoot
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$extension = if ($Format -eq "plain") { "sql" } else { "dump" }
$outFile = Join-Path $OutputDirectory "supabase-db-$databaseLabel-$timestamp.$extension"

# --- Options cho pg_dump ---
$dumpArgs = @()
$dumpArgs += if ($Format -eq "plain") { "-Fp" } else { "-Fc" }
foreach ($s in $Schema) { if ($s) { $dumpArgs += @("--schema", $s) } }
foreach ($s in $ExcludeSchema) { if ($s) { $dumpArgs += @("--exclude-schema", $s) } }
if (-not $IncludeOwner) { $dumpArgs += @("--no-owner", "--no-privileges") }
if ($SchemaOnly) { $dumpArgs += "--schema-only" }
if ($DataOnly) { $dumpArgs += "--data-only" }
if ($Clean) { $dumpArgs += @("--clean", "--if-exists") }

$toolOutPath = $outFile
$containerOutPath = $null
if ($useContainer) {
    $containerOutPath = "/tmp/supabase-dump-$timestamp.$extension"
    $toolOutPath = $containerOutPath
}

Write-Host "Dumping $displayTarget -> $outFile" -ForegroundColor Cyan

$envWasSet = @{}
$envPrevious = @{ PGPASSWORD = $env:PGPASSWORD; PGSSLMODE = $env:PGSSLMODE }
if (-not $useContainer) {
    if (-not [string]::IsNullOrWhiteSpace($effectivePassword)) {
        $env:PGPASSWORD = $effectivePassword
        $envWasSet.PGPASSWORD = $true
    }
    if (-not [string]::IsNullOrWhiteSpace($Sslmode)) {
        $env:PGSSLMODE = $Sslmode
        $envWasSet.PGSSLMODE = $true
    }
}

try {
    Invoke-Native @pgDumpCmd @connArgs @dumpArgs -f $toolOutPath
    if ($useContainer) {
        Invoke-Native $engineCmd cp "${ToolContainer}:$containerOutPath" $outFile
    }
}
finally {
    if ($useContainer -and $containerOutPath) {
        & $engineCmd exec $ToolContainer rm -f $containerOutPath | Out-Null
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

# --- Chinh sua nho de file .sql restore duoc vao DB da co san schema public ---
if ($Format -eq "plain" -and -not $NoSqlFixups) {
    $fixed = 0
    $lines = [System.IO.File]::ReadAllLines($outFile)
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -eq "CREATE SCHEMA public;") {
            $lines[$i] = "CREATE SCHEMA IF NOT EXISTS public;"
            $fixed++
        }
        elseif ($lines[$i] -match "^\\(un)?restrict ") {
            # Meta-command cua psql >= 17.6; psql cu hon bao "invalid command \restrict".
            $lines[$i] = "-- (skipped by backup-postgres-supabase.ps1) " + $lines[$i]
            $fixed++
        }
        elseif ($lines[$i] -match "^DROP SCHEMA (IF EXISTS )?public;") {
            # -Clean sinh dong nay; xoa ca schema public la qua tay va tren
            # managed DB (Supabase) thuong khong du quyen.
            $lines[$i] = "-- (skipped by backup-postgres-supabase.ps1) " + $lines[$i]
            $fixed++
        }
        elseif ($lines[$i] -match "^CREATE FUNCTION ") {
            # OR REPLACE de nap de len function da ton tai ma khong phai DROP
            # (DROP se vuong event trigger / object khac phu thuoc vao no).
            $lines[$i] = $lines[$i] -replace "^CREATE FUNCTION ", "CREATE OR REPLACE FUNCTION "
            $fixed++
        }
        elseif ($lines[$i] -match "^DROP FUNCTION IF EXISTS ") {
            # Da dung CREATE OR REPLACE nen khong can DROP; giu DROP se loi
            # "cannot drop ... because other objects depend on it".
            $lines[$i] = "-- (skipped by backup-postgres-supabase.ps1) " + $lines[$i]
            $fixed++
        }
        elseif ($lines[$i] -match "^COMMENT ON SCHEMA public IS ") {
            # Doi chu so huu schema public -> hay loi tren managed DB; bo qua.
            $lines[$i] = "-- (skipped by backup-postgres-supabase.ps1) " + $lines[$i]
            $fixed++
        }
    }
    if ($fixed -gt 0) {
        [System.IO.File]::WriteAllLines($outFile, $lines, (New-Object System.Text.UTF8Encoding($false)))
        Write-Verbose "Applied $fixed SQL fixup(s) for restore compatibility."
    }
}

# --- Nen gzip (neu -Compress) ---
if ($Compress) {
    $gzPath = "$outFile.gz"
    $inStream = [System.IO.File]::OpenRead($outFile)
    try {
        $outStream = [System.IO.File]::Create($gzPath)
        try {
            $gzStream = New-Object System.IO.Compression.GZipStream($outStream, [System.IO.Compression.CompressionLevel]::Optimal)
            try { $inStream.CopyTo($gzStream) } finally { $gzStream.Dispose() }
        }
        finally { $outStream.Dispose() }
    }
    finally { $inStream.Dispose() }

    $before = (Get-Item -LiteralPath $outFile).Length
    $after = (Get-Item -LiteralPath $gzPath).Length
    Remove-Item -LiteralPath $outFile -Force
    $outFile = $gzPath
    Write-Verbose ("Compressed: {0:N0} -> {1:N0} bytes ({2:P0})" -f $before, $after, ($after / $before))
}

if ($KeepDays -gt 0) {
    $cutoff = (Get-Date).AddDays(-$KeepDays)
    Get-ChildItem -LiteralPath $OutputDirectory -File -Include "supabase-db-*.sql", "supabase-db-*.sql.gz", "supabase-db-*.dump" |
        Where-Object { $_.LastWriteTime -lt $cutoff } |
        Remove-Item -Force
}

Get-Item -LiteralPath $outFile
