<#
.SYNOPSIS
    Don dep file backup theo 3 tang luat.

.DESCRIPTION
    Xet tung file, moc thoi gian LAY TU TEN FILE (supabase-db-<db>-yyyyMMdd-HHmmss.sql[.gz]).
    Ap dung theo thu tu:

      Tang 1 - Qua han:  file cu hon -KeepDays ngay lich  -> XOA (thang moi luat khac).
                         Giu -KeepDays ngay gan nhat tinh ca hom nay.
      Tang 2 - Cua so:   file trong -KeepHours gio gan nhat -> GIU het.
      Tang 3 - Moi ngay: nhung file con lai gom theo ngay lich, moi ngay GIU 1 ban moi nhat.

    Luu y he qua: neu ban moi nhat cua mot ngay da nam trong cua so -KeepHours thi ngay do
    khong giu them ban dai dien nao nua -> cac file cu hon trong CHINH NGAY HOM NAY cung bi don.

    An toan:
      * Chi dung file khop dung pattern; file khac (vi du RECOVERY-*.sql) khong bi dung toi.
      * Khong de quy vao thu muc con (backups/bad/ an toan).
      * Khong bao gio xoa file moi nhat tuyet doi.
      * File nho hon -MinValidBytes coi la hong: khong duoc chon lam dai dien cua ngay.

.EXAMPLE
    ./retention-backups.ps1 -DryRun     # xem se xoa gi, khong xoa that

.EXAMPLE
    ./retention-backups.ps1
#>
[CmdletBinding()]
param(
    [string]$Directory,
    [string]$ConfigFile,
    [int]$KeepHours,
    [int]$KeepDays,
    [long]$MinValidBytes,
    [datetime]$Now,
    [switch]$DryRun
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
    if ([System.IO.Path]::IsPathRooted($expanded)) {
        return [System.IO.Path]::GetFullPath($expanded)
    }
    return [System.IO.Path]::GetFullPath((Join-Path $BaseDir $expanded))
}

# --- Doc config ---
$config = @{}
$configPath = if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) { $ConfigFile } else { Join-Path $PSScriptRoot "backup-config.json" }
if (Test-Path -LiteralPath $configPath) {
    $raw = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    foreach ($prop in $raw.PSObject.Properties) {
        if (-not $prop.Name.StartsWith("//")) { $config[$prop.Name] = $prop.Value }
    }
    Write-Verbose "Loaded config: $configPath"
}
elseif (-not [string]::IsNullOrWhiteSpace($ConfigFile)) {
    throw "ConfigFile not found: $ConfigFile"
}

if (-not $PSBoundParameters.ContainsKey("KeepHours")) { $KeepHours = [int](Get-ConfigValue $config "keepHours" 6) }
if (-not $PSBoundParameters.ContainsKey("KeepDays")) { $KeepDays = [int](Get-ConfigValue $config "keepDays" 45) }
if (-not $PSBoundParameters.ContainsKey("MinValidBytes")) { $MinValidBytes = [long](Get-ConfigValue $config "minValidBytes" 20480) }

if ([string]::IsNullOrWhiteSpace($Directory)) {
    $fromConfig = Get-ConfigValue $config "outputDirectory" $null
    if ($fromConfig) { $Directory = Resolve-ConfiguredPath -Path $fromConfig -BaseDir $PSScriptRoot }
    else { $Directory = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) "backups" }
}
else {
    $Directory = Resolve-ConfiguredPath -Path $Directory -BaseDir $PSScriptRoot
}

if ($KeepDays -lt 1) { throw "-KeepDays phai >= 1." }
if ($KeepHours -lt 0) { throw "-KeepHours phai >= 0." }
if (-not (Test-Path -LiteralPath $Directory)) {
    Write-Verbose "Thu muc backup chua ton tai: $Directory"
    return
}

# --- Liet ke file hop le ---
$pattern = '^supabase-db-.+-(\d{8})-(\d{6})\.(sql\.gz|sql|dump)$'
$items = New-Object System.Collections.Generic.List[object]
foreach ($f in Get-ChildItem -LiteralPath $Directory -File) {
    $m = [regex]::Match($f.Name, $pattern)
    if (-not $m.Success) { continue }
    $stamp = "$($m.Groups[1].Value)$($m.Groups[2].Value)"
    try {
        $ts = [datetime]::ParseExact($stamp, "yyyyMMddHHmmss", [Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        Write-Verbose "Bo qua (timestamp khong hop le): $($f.Name)"
        continue
    }
    $items.Add([pscustomobject]@{
            File    = $f
            Stamp   = $ts
            Day     = $ts.Date
            IsValid = ($f.Length -ge $MinValidBytes)
        })
}

if ($items.Count -eq 0) {
    Write-Verbose "Khong co file backup nao khop pattern trong $Directory"
    return
}

$now = if ($PSBoundParameters.ContainsKey('Now')) { $Now } else { Get-Date }
$windowStart = $now.AddHours(-$KeepHours)
$oldestKeptDay = $now.Date.AddDays(-($KeepDays - 1))
$newestOverall = ($items | Sort-Object Stamp -Descending | Select-Object -First 1).File.FullName

$keep = New-Object 'System.Collections.Generic.HashSet[string]'
$expired = New-Object 'System.Collections.Generic.HashSet[string]'

# Tang 1: qua han theo ngay lich
$alive = New-Object System.Collections.Generic.List[object]
foreach ($it in $items) {
    if ($it.Day -lt $oldestKeptDay) { [void]$expired.Add($it.File.FullName) }
    else { $alive.Add($it) }
}

# Tang 2: cua so gio gan nhat
foreach ($it in $alive) {
    if ($it.Stamp -ge $windowStart) { [void]$keep.Add($it.File.FullName) }
}

# Tang 3: moi ngay giu 1 ban moi nhat (uu tien ban hop le)
foreach ($g in ($alive | Group-Object Day)) {
    $rep = $g.Group | Where-Object { $_.IsValid } | Sort-Object Stamp -Descending | Select-Object -First 1
    if (-not $rep) { $rep = $g.Group | Sort-Object Stamp -Descending | Select-Object -First 1 }
    [void]$keep.Add($rep.File.FullName)
}

# An toan: khong bao gio xoa ban moi nhat tuyet doi
[void]$keep.Add($newestOverall)
[void]$expired.Remove($newestOverall)

$toDelete = @()
foreach ($it in $items) {
    $p = $it.File.FullName
    if ($expired.Contains($p)) { $toDelete += [pscustomobject]@{ Item = $it; Reason = "qua $KeepDays ngay" }; continue }
    if (-not $keep.Contains($p)) {
        $reason = if (-not $it.IsValid) { "file hong (<$MinValidBytes bytes)" } else { "khong phai ban moi nhat cua ngay" }
        $toDelete += [pscustomobject]@{ Item = $it; Reason = $reason }
    }
}

$keptCount = $items.Count - $toDelete.Count
$freed = 0L
foreach ($d in $toDelete) { $freed += $d.Item.File.Length }

Write-Host ("Retention @ {0:yyyy-MM-dd HH:mm:ss} | thu muc: {1}" -f $now, $Directory)
Write-Host ("  cua so giu het : >= {0:yyyy-MM-dd HH:mm:ss}  ({1}h)" -f $windowStart, $KeepHours)
Write-Host ("  ngay cu nhat   : {0:yyyy-MM-dd}  ({1} ngay)" -f $oldestKeptDay, $KeepDays)
Write-Host ("  tong {0} file -> giu {1}, xoa {2} ({3:N1} MB)" -f $items.Count, $keptCount, $toDelete.Count, ($freed / 1MB))

foreach ($d in ($toDelete | Sort-Object { $_.Item.Stamp })) {
    $verb = if ($DryRun) { "[DryRun] se xoa" } else { "xoa" }
    Write-Host ("  {0}: {1}  ({2})" -f $verb, $d.Item.File.Name, $d.Reason)
    if (-not $DryRun) { Remove-Item -LiteralPath $d.Item.File.FullName -Force }
}

[pscustomobject]@{
    Directory   = $Directory
    Total       = $items.Count
    Kept        = $keptCount
    Deleted     = $toDelete.Count
    FreedBytes  = $freed
    DryRun      = [bool]$DryRun
    WindowStart = $windowStart
    OldestDay   = $oldestKeptDay
}
