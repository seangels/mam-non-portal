[CmdletBinding()]
param(
    [string]$ContainerName = "api_postgres_1",
    [string]$DatabaseName,
    [string]$SqlFile,
    [string]$BackupDirectory,
    [int]$BackupKeepDays = 7,
    [switch]$SkipDatabaseCleanup,
    [switch]$SkipBackupCleanup
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

function Get-ContainerEnvValue {
    param(
        [Parameter(Mandatory = $true)][string]$Name
    )

    $value = & podman exec $ContainerName printenv $Name
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($value)) {
        throw "Unable to read $Name from container $ContainerName."
    }

    return $value.Trim()
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($SqlFile)) {
    $SqlFile = Join-Path $PSScriptRoot "cleanup-retention.sql"
}
if ([string]::IsNullOrWhiteSpace($BackupDirectory)) {
    $BackupDirectory = Join-Path $repoRoot "backups"
}

if (-not $SkipDatabaseCleanup) {
    $state = & podman inspect $ContainerName --format "{{.State.Status}}"
    if ($LASTEXITCODE -ne 0 -or $state -ne "running") {
        throw "Container $ContainerName is not running. Start it before cleanup."
    }

    $postgresUser = Get-ContainerEnvValue "POSTGRES_USER"
    if ([string]::IsNullOrWhiteSpace($DatabaseName)) {
        $DatabaseName = Get-ContainerEnvValue "POSTGRES_DB"
    }

    $sqlPath = Resolve-Path -LiteralPath $SqlFile
    $containerSqlPath = "/tmp/cleanup-retention.sql"
    try {
        Invoke-Native podman cp $sqlPath "${ContainerName}:$containerSqlPath"
        Invoke-Native podman exec $ContainerName psql -v ON_ERROR_STOP=1 -U $postgresUser -d $DatabaseName -f $containerSqlPath
    }
    finally {
        & podman exec $ContainerName rm -f $containerSqlPath | Out-Null
    }
}

if (-not $SkipBackupCleanup -and $BackupKeepDays -gt 0 -and (Test-Path -LiteralPath $BackupDirectory)) {
    $cutoff = (Get-Date).AddDays(-$BackupKeepDays)
    Get-ChildItem -LiteralPath $BackupDirectory -File -Include "postgres-db-*.dump", "postgres-globals-*.sql", "postgres-all-*.sql" |
        Where-Object { $_.LastWriteTime -lt $cutoff } |
        Remove-Item -Force
}

Write-Output "Cleanup completed."