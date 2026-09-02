<#
.SYNOPSIS
    Mot chu ky backup: dump Supabase -> kiem tra file -> don dep retention -> ghi log.
    Day la script ma Windows Task Scheduler goi moi 15 phut.

.DESCRIPTION
    Doc cau hinh tu backup-config.json (cung thu muc). Luon chay retention ke ca khi
    backup that bai, de viec don dep khong bi ket lai khi mat mang.

    Ghi log ra <outputDirectory>\backup.log (xoay vong khi qua -MaxLogBytes).

.EXAMPLE
    ./run-backup-cycle.ps1

.EXAMPLE
    ./run-backup-cycle.ps1 -SkipRetention -Verbose
#>
[CmdletBinding()]
param(
    [string]$ConfigFile,
    [switch]$SkipBackup,
    [switch]$SkipRetention,
    [long]$MaxLogBytes = 5MB
)

$ErrorActionPreference = "Stop"

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

# --- Config ---
$config = @{}
$configPath = if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) { $ConfigFile } else { Join-Path $PSScriptRoot "backup-config.json" }
if (Test-Path -LiteralPath $configPath) {
    $raw = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    foreach ($prop in $raw.PSObject.Properties) {
        if (-not $prop.Name.StartsWith("//")) { $config[$prop.Name] = $prop.Value }
    }
}

$outDirRaw = Get-ConfigValue $config "outputDirectory" $null
$outputDirectory = if ($outDirRaw) {
    Resolve-ConfiguredPath -Path $outDirRaw -BaseDir $PSScriptRoot
}
else {
    Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) "backups"
}
$minValidBytes = [long](Get-ConfigValue $config "minValidBytes" 20480)

New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$logPath = Join-Path $outputDirectory "backup.log"

function Write-Log {
    param([string]$Level, [string]$Message)
    $line = "{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}" -f (Get-Date), $Level, $Message
    Write-Host $line
    try {
        if ((Test-Path -LiteralPath $logPath) -and ((Get-Item -LiteralPath $logPath).Length -gt $MaxLogBytes)) {
            Move-Item -LiteralPath $logPath -Destination "$logPath.1" -Force
        }
        Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
    }
    catch {
        Write-Host "  (khong ghi duoc log: $($_.Exception.Message))"
    }
}

$exitCode = 0

# --- 1. Backup ---
if (-not $SkipBackup) {
    try {
        $before = @(Get-ChildItem -LiteralPath $outputDirectory -File -Filter "supabase-db-*" -ErrorAction SilentlyContinue |
                Select-Object -ExpandProperty FullName)
        $result = & (Join-Path $PSScriptRoot "backup-postgres-supabase.ps1") -ConfigFile $configPath -NoPrompt
        $created = $result | Where-Object { $_ -is [System.IO.FileInfo] } | Select-Object -Last 1
        if (-not $created) {
            $created = Get-ChildItem -LiteralPath $outputDirectory -File -Filter "supabase-db-*" |
                Where-Object { $_.FullName -notin $before } |
                Sort-Object LastWriteTime -Descending | Select-Object -First 1
        }

        if (-not $created) {
            Write-Log "ERROR" "Backup chay xong nhung khong tim thay file moi."
            $exitCode = 1
        }
        elseif ($created.Length -lt $minValidBytes) {
            Write-Log "ERROR" ("File backup qua nho ({0:N0} < {1:N0} bytes) -> xoa: {2}" -f $created.Length, $minValidBytes, $created.Name)
            Remove-Item -LiteralPath $created.FullName -Force
            $exitCode = 1
        }
        else {
            Write-Log "OK" ("Backup {0} ({1:N0} bytes)" -f $created.Name, $created.Length)
        }
    }
    catch {
        Write-Log "ERROR" "Backup that bai: $($_.Exception.Message)"
        $exitCode = 1
    }
}

# --- 2. Retention (chay ca khi backup loi) ---
if (-not $SkipRetention) {
    try {
        $summary = & (Join-Path $PSScriptRoot "retention-backups.ps1") -ConfigFile $configPath 6>$null |
            Where-Object { $_ -is [pscustomobject] } | Select-Object -Last 1
        if ($summary) {
            Write-Log "OK" ("Retention: {0} file -> giu {1}, xoa {2} ({3:N1} MB)" -f
                $summary.Total, $summary.Kept, $summary.Deleted, ($summary.FreedBytes / 1MB))
        }
    }
    catch {
        Write-Log "ERROR" "Retention that bai: $($_.Exception.Message)"
        $exitCode = 1
    }
}

exit $exitCode
