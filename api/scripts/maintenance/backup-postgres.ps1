[CmdletBinding()]
param(
    [string]$ContainerName = "api_postgres_1",
    [string]$DatabaseName,
    [string]$OutputDirectory,
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
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "backups"
}

$state = & podman inspect $ContainerName --format "{{.State.Status}}"
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

$dbContainerPath = "/tmp/postgres-db-$timestamp.dump"
$dbBackupPath = Join-Path $OutputDirectory "postgres-db-$DatabaseName-$timestamp.dump"

try {
    Invoke-Native podman exec $ContainerName pg_dump -U $postgresUser -d $DatabaseName -Fc -f $dbContainerPath
    Invoke-Native podman cp "${ContainerName}:$dbContainerPath" $dbBackupPath
    $createdFiles.Add($dbBackupPath)
}
finally {
    & podman exec $ContainerName rm -f $dbContainerPath | Out-Null
}

if ($IncludeGlobals) {
    $globalsContainerPath = "/tmp/postgres-globals-$timestamp.sql"
    $globalsBackupPath = Join-Path $OutputDirectory "postgres-globals-$timestamp.sql"

    try {
        Invoke-Native podman exec $ContainerName pg_dumpall -U $postgresUser --globals-only -f $globalsContainerPath
        Invoke-Native podman cp "${ContainerName}:$globalsContainerPath" $globalsBackupPath
        $createdFiles.Add($globalsBackupPath)
    }
    finally {
        & podman exec $ContainerName rm -f $globalsContainerPath | Out-Null
    }
}

if ($ClusterDump) {
    $clusterContainerPath = "/tmp/postgres-all-$timestamp.sql"
    $clusterBackupPath = Join-Path $OutputDirectory "postgres-all-$timestamp.sql"

    try {
        Invoke-Native podman exec $ContainerName pg_dumpall -U $postgresUser --clean --if-exists -f $clusterContainerPath
        Invoke-Native podman cp "${ContainerName}:$clusterContainerPath" $clusterBackupPath
        $createdFiles.Add($clusterBackupPath)
    }
    finally {
        & podman exec $ContainerName rm -f $clusterContainerPath | Out-Null
    }
}

if ($KeepDays -gt 0) {
    $cutoff = (Get-Date).AddDays(-$KeepDays)
    Get-ChildItem -LiteralPath $OutputDirectory -File -Include "postgres-db-*.dump", "postgres-globals-*.sql", "postgres-all-*.sql" |
        Where-Object { $_.LastWriteTime -lt $cutoff } |
        Remove-Item -Force
}

$createdFiles | ForEach-Object { Get-Item -LiteralPath $_ }