<#
.SYNOPSIS
    Backup PostgreSQL chạy trong container (Podman hoặc Docker).

.DESCRIPTION
    Dump database chính bằng `pg_dump` bên trong container rồi copy ra thư mục
    `api/backups` (mặc định). `-Format custom` (mặc định) -> `.dump`; `-Format plain`
    -> `.sql` text (dùng cho Supabase/host khác). `-Schema`/`-NoOwner`/`-NoPrivileges`
    truyền thẳng cho pg_dump. Tuỳ chọn kèm globals (roles/tablespaces) và cluster dump.
    User/DB được đọc từ biến môi trường POSTGRES_USER / POSTGRES_DB của container.

.EXAMPLE
    ./backup-postgres-container.ps1
    ./backup-postgres-container.ps1 -Engine docker -ContainerName gv_postgres -IncludeGlobals
#>
[CmdletBinding()]
param(
    [ValidateSet("auto", "podman", "docker")]
    [string]$Engine = "auto",
    [string]$ContainerName = "api_postgres_1",
    [string]$DatabaseName,
    [string]$OutputDirectory,
    [ValidateSet("custom", "plain")][string]$Format = "custom",
    [string[]]$Schema,
    [switch]$NoOwner,
    [switch]$NoPrivileges,
    [int]$KeepDays = 7,
    [switch]$IncludeGlobals,
    [switch]$ClusterDump
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

function Get-ContainerEnvValue {
    param([Parameter(Mandatory = $true)][string]$Name)

    $value = & $engineCmd exec $ContainerName printenv $Name
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($value)) {
        throw "Unable to read $Name from container $ContainerName."
    }

    return $value.Trim()
}

$engineCmd = Resolve-ContainerEngine -Preference $Engine
Write-Verbose "Using container engine: $engineCmd"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "backups"
}

$state = & $engineCmd inspect $ContainerName --format "{{.State.Status}}"
if ($LASTEXITCODE -ne 0 -or $state -ne "running") {
    throw "Container $ContainerName is not running. Start it before backing up."
}

$postgresUser = Get-ContainerEnvValue "POSTGRES_USER"
if ([string]::IsNullOrWhiteSpace($DatabaseName)) {
    $DatabaseName = Get-ContainerEnvValue "POSTGRES_DB"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$createdFiles = New-Object System.Collections.Generic.List[string]

$dumpFormatArg = if ($Format -eq "plain") { "-Fp" } else { "-Fc" }
$dumpExtension = if ($Format -eq "plain") { "sql" } else { "dump" }
$dumpExtraArgs = @()
foreach ($s in $Schema) { $dumpExtraArgs += @("--schema", $s) }
if ($NoOwner) { $dumpExtraArgs += "--no-owner" }
if ($NoPrivileges) { $dumpExtraArgs += "--no-privileges" }

$dbContainerPath = "/tmp/postgres-db-$timestamp.$dumpExtension"
$dbBackupPath = Join-Path $OutputDirectory "postgres-db-$DatabaseName-$timestamp.$dumpExtension"

try {
    Invoke-Native $engineCmd exec $ContainerName pg_dump -U $postgresUser -d $DatabaseName $dumpFormatArg @dumpExtraArgs -f $dbContainerPath
    Invoke-Native $engineCmd cp "${ContainerName}:$dbContainerPath" $dbBackupPath
    $createdFiles.Add($dbBackupPath)
}
finally {
    & $engineCmd exec $ContainerName rm -f $dbContainerPath | Out-Null
}

if ($IncludeGlobals) {
    $globalsContainerPath = "/tmp/postgres-globals-$timestamp.sql"
    $globalsBackupPath = Join-Path $OutputDirectory "postgres-globals-$timestamp.sql"

    try {
        Invoke-Native $engineCmd exec $ContainerName pg_dumpall -U $postgresUser --globals-only -f $globalsContainerPath
        Invoke-Native $engineCmd cp "${ContainerName}:$globalsContainerPath" $globalsBackupPath
        $createdFiles.Add($globalsBackupPath)
    }
    finally {
        & $engineCmd exec $ContainerName rm -f $globalsContainerPath | Out-Null
    }
}

if ($ClusterDump) {
    $clusterContainerPath = "/tmp/postgres-all-$timestamp.sql"
    $clusterBackupPath = Join-Path $OutputDirectory "postgres-all-$timestamp.sql"

    try {
        Invoke-Native $engineCmd exec $ContainerName pg_dumpall -U $postgresUser --clean --if-exists -f $clusterContainerPath
        Invoke-Native $engineCmd cp "${ContainerName}:$clusterContainerPath" $clusterBackupPath
        $createdFiles.Add($clusterBackupPath)
    }
    finally {
        & $engineCmd exec $ContainerName rm -f $clusterContainerPath | Out-Null
    }
}

if ($KeepDays -gt 0) {
    $cutoff = (Get-Date).AddDays(-$KeepDays)
    Get-ChildItem -LiteralPath $OutputDirectory -File -Include "postgres-db-*.dump", "postgres-db-*.sql", "postgres-globals-*.sql", "postgres-all-*.sql" |
        Where-Object { $_.LastWriteTime -lt $cutoff } |
        Remove-Item -Force
}

$createdFiles | ForEach-Object { Get-Item -LiteralPath $_ }
