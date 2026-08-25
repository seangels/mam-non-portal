[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$BackupFile,
    [string]$ContainerName = "api_postgres_1",
    [string]$DatabaseName,
    [switch]$RecreateDatabase,
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

if (-not $Force) {
    throw "Restore is destructive. Re-run with -Force after confirming the target container and backup file."
}

$backupPath = Resolve-Path -LiteralPath $BackupFile
$backupItem = Get-Item -LiteralPath $backupPath

$state = & podman inspect $ContainerName --format "{{.State.Status}}"
if ($LASTEXITCODE -ne 0 -or $state -ne "running") {
    throw "Container $ContainerName is not running. Start it before restoring."
}

$postgresUser = Get-ContainerEnvValue "POSTGRES_USER"
if ([string]::IsNullOrWhiteSpace($DatabaseName)) {
    $DatabaseName = Get-ContainerEnvValue "POSTGRES_DB"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$containerPath = "/tmp/restore-$timestamp$($backupItem.Extension)"

try {
    Invoke-Native podman cp $backupItem.FullName "${ContainerName}:$containerPath"

    if ($backupItem.Extension -eq ".dump") {
        if ($RecreateDatabase) {
            Invoke-Native podman exec $ContainerName dropdb -U $postgresUser --maintenance-db=postgres --if-exists $DatabaseName
            Invoke-Native podman exec $ContainerName createdb -U $postgresUser $DatabaseName
        }

        Invoke-Native podman exec $ContainerName pg_restore -U $postgresUser -d $DatabaseName --clean --if-exists --no-owner --no-acl $containerPath
    }
    elseif ($backupItem.Extension -eq ".sql") {
        Invoke-Native podman exec $ContainerName psql -v ON_ERROR_STOP=1 -U $postgresUser -d postgres -f $containerPath
    }
    else {
        throw "Unsupported backup extension '$($backupItem.Extension)'. Use .dump from backup-postgres.ps1 or a .sql dump."
    }
}
finally {
    & podman exec $ContainerName rm -f $containerPath | Out-Null
}

Write-Output "Restore completed for $($backupItem.FullName) into $ContainerName/$DatabaseName."