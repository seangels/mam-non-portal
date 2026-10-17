#Requires -Version 5.1
<#
.SYNOPSIS
Copy the latest UI ZIP from the local synced folder configured in start-app.config.json.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SourceRoot,
    [string]$ConfigFile,
    [switch]$IfNewer,
    [switch]$LiveUpdate,
    [switch]$PassThru
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Set-StrictMode -Version 3.0
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $PSScriptRoot '../..' }
. (Join-Path $PSScriptRoot 'SpaPackage.ps1')
. (Join-Path $PSScriptRoot 'AppLauncher.ps1')
$SourceRoot = Get-SpaSourceRoot $SourceRoot
$target = Get-SpaSafePath $SourceRoot 'api/src/AdminPortal.Api/ClientApp/build'
if (-not $PSCmdlet.ShouldProcess($target, 'Copy newest UI ZIP from the configured local folder and install')) { return }
$config = Get-SpaAppConfig $ConfigFile
$directory = $config.packagesDirectory
Write-Host "Package folder: $directory"
if (-not (Test-Path -LiteralPath $directory -PathType Container)) { throw "Package folder khong ton tai/khong truy cap duoc: $directory" }
$files = @(Get-ChildItem -LiteralPath $directory -Filter '*.zip' -File)
$latest = Get-SpaLatestPackage -Files $files -AllowNone
if ($null -eq $latest) {
    Write-Host 'Khong co package UI hop le trong folder; giu UI hien co.'
    if ($PassThru) { [pscustomobject]@{ Status = 'NoPackage'; PackageName = $null; InstalledTimestampUtc = $null } }
    return
}
$installedState = Get-SpaInstalledPackage $SourceRoot
if ($IfNewer -and $null -ne $installedState -and (Test-SpaCurrentBuild $SourceRoot) -and
    $latest.Timestamp -le $installedState.Timestamp) {
    Write-Host 'UI hien tai bang/moi hon package local; bo qua copy.'
    if ($PassThru) { [pscustomobject]@{ Status = 'Current'; PackageName = $installedState.PackageName; InstalledTimestampUtc = $installedState.Timestamp.ToString('o') } }
    return
}
$source = $latest.File.FullName
# Hash before copying: a ZIP still changing during Drive sync must not be installed.
$length = (Get-Item -LiteralPath $source).Length
$hash = (Get-FileHash -LiteralPath $source -Algorithm MD5).Hash
$package = [pscustomobject]@{
    Timestamp = $latest.Timestamp
    File = [pscustomobject]@{ name = $latest.File.Name; id = $source; md5Checksum = $hash }
}
$work = Get-SpaSafePath $SourceRoot ('artifacts/spa/local-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    $zip = Get-SpaSafePath $SourceRoot (Join-Path $work $latest.File.Name)
    Copy-Item -LiteralPath $source -Destination $zip
    if ((Get-Item -LiteralPath $zip).Length -ne $length -or (Get-FileHash -LiteralPath $zip -Algorithm MD5).Hash -ine $hash) {
        throw 'Package local thay doi khi copy hoac checksum khong khop. Cho Drive dong bo xong roi thu lai.'
    }
    $expanded = Expand-SpaUiPackage -SourceRoot $SourceRoot -PackagePath $zip -Destination (Join-Path $work 'build')
    $installed = Install-SpaClientBuild -SourceRoot $SourceRoot -BuildDirectory $expanded -LiveUpdate:$LiveUpdate
    Set-SpaInstalledPackage -SourceRoot $SourceRoot -Package $package
    Write-Host "UI da cap nhat tu $source vao $installed"
    if ($PassThru) { [pscustomobject]@{ Status = 'Updated'; PackageName = $package.File.name; InstalledTimestampUtc = $package.Timestamp.ToString('o') } }
}
finally { Remove-SpaWorkDirectory $SourceRoot $work }
