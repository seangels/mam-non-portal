#Requires -Version 5.1
<#
.SYNOPSIS
Download the newest UI package by filename timestamp and install into source ClientApp/build.
.EXAMPLE
.\deploy\spa\update-ui-from-drive.ps1
.EXAMPLE
.\deploy\spa\update-ui-from-drive.ps1 -SourceRoot D:\apps\api-portal
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SourceRoot,
    [string]$DriveFolder = '1EJK12xqBWBlsVchCeXUlwYOpouzZ_D_B',
    [string]$PackageRegex = '\Agv-portal-ui-(?<timestamp>\d{8}T\d{9}Z)\.zip\z',
    [string]$TimestampFormat = "yyyyMMdd'T'HHmmssfff'Z'",
    [string]$ClientSecretFile,
    [string]$TokenFile,
    [switch]$IfNewer,
    [switch]$LiveUpdate,
    [switch]$PassThru,
    [switch]$ReAuth
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Set-StrictMode -Version 3.0
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $PSScriptRoot '../..' }
. (Join-Path $PSScriptRoot 'SpaPackage.ps1')
. (Join-Path $PSScriptRoot 'GoogleDrive.ps1')

$SourceRoot = Get-SpaSourceRoot $SourceRoot
$folderId = Resolve-SpaDriveFolderId $DriveFolder
$target = Get-SpaSafePath $SourceRoot 'api/src/AdminPortal.Api/ClientApp/build'
# WhatIf performs no OAuth, download, filesystem write or app update.
if (-not $PSCmdlet.ShouldProcess($target, 'Download latest matching UI ZIP from Drive and replace ClientApp/build (keep previous build)')) { return }
$headers = Get-SpaDriveHeaders -SourceRoot $SourceRoot -ClientSecretFile $ClientSecretFile `
    -TokenFile $TokenFile -ReadOnly -ReAuth:$ReAuth
$files = @(Get-SpaDriveFiles -FolderId $folderId -Headers $headers)
$latest = Get-SpaLatestPackage -Files $files -PackageRegex $PackageRegex -TimestampFormat $TimestampFormat -AllowNone
if ($null -eq $latest) {
    Write-Host 'Khong co package UI hop le tren Drive; giu UI hien co.'
    if ($PassThru) { [pscustomobject]@{ Status = 'NoPackage'; PackageName = $null; InstalledTimestampUtc = $null } }
    return
}
Write-Host "Goi moi nhat: $($latest.File.name) (UTC $($latest.Timestamp.ToString('yyyy-MM-dd HH:mm:ss.fff')))"
$installedState = Get-SpaInstalledPackage $SourceRoot
if ($IfNewer -and $null -ne $installedState -and (Test-SpaCurrentBuild $SourceRoot) -and
    $latest.Timestamp -le $installedState.Timestamp) {
    Write-Host 'UI hien tai da bang hoac moi hon package tren Drive; bo qua download/copy.'
    if ($PassThru) {
        [pscustomobject]@{ Status = 'Current'; PackageName = $installedState.PackageName; InstalledTimestampUtc = $installedState.Timestamp.ToString('o') }
    }
    return
}

$work = Get-SpaSafePath $SourceRoot ('artifacts/spa/download-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    $zipPath = Get-SpaSafePath $SourceRoot (Join-Path $work $latest.File.name)
    $uri = "https://www.googleapis.com/drive/v3/files/$([uri]::EscapeDataString($latest.File.id))?alt=media&supportsAllDrives=true"
    Invoke-WebRequest -Method Get -Uri $uri -Headers $headers -OutFile $zipPath -UseBasicParsing -TimeoutSec 1800 | Out-Null
    Assert-SpaDriveDownload -Path $zipPath -Metadata $latest.File
    $expanded = Expand-SpaUiPackage -SourceRoot $SourceRoot -PackagePath $zipPath -Destination (Join-Path $work 'build')
    $installed = Install-SpaClientBuild -SourceRoot $SourceRoot -BuildDirectory $expanded -LiveUpdate:$LiveUpdate
    Set-SpaInstalledPackage -SourceRoot $SourceRoot -Package $latest
    Write-Host "Cap nhat UI thanh cong: $installed"
    Write-Host 'API can bat Spa__ServeFromClientAppBuild=true va Spa__BuildPath=ClientApp/build; live update khong restart API.'
    if ($PassThru) {
        [pscustomobject]@{ Status = 'Updated'; PackageName = $latest.File.name; InstalledTimestampUtc = $latest.Timestamp.ToString('o') }
    }
}
finally { Remove-SpaWorkDirectory $SourceRoot $work }
