#Requires -Version 5.1
<#
.SYNOPSIS
Build same-host UI, copy to source ClientApp/build, zip and upload to Drive.
.EXAMPLE
.\deploy\spa\build-ui-package.ps1
.EXAMPLE
.\deploy\spa\build-ui-package.ps1 -SkipNpmInstall -SkipUpload
.NOTES
Production execution by an agent remains subject to the AGENTS.md production gate.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SourceRoot = (Join-Path $PSScriptRoot '../..'),
    [string]$DriveFolder = '1EJK12xqBWBlsVchCeXUlwYOpouzZ_D_B',
    [string]$OutputDirectory = 'release/spa',
    [string]$ClientSecretFile,
    [string]$TokenFile,
    [switch]$SkipNpmInstall,
    [switch]$SkipUpload,
    [switch]$ReAuth
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Set-StrictMode -Version 3.0
. (Join-Path $PSScriptRoot 'SpaPackage.ps1')
. (Join-Path $PSScriptRoot 'GoogleDrive.ps1')

$SourceRoot = Get-SpaSourceRoot $SourceRoot
$outputPath = Get-SpaSafePath $SourceRoot $OutputDirectory
$uiPath = Get-SpaSafePath $SourceRoot 'ui'
$folderId = Resolve-SpaDriveFolderId $DriveFolder
if (-not $PSCmdlet.ShouldProcess($SourceRoot, 'Build UI production, update ClientApp/build, create ZIP and upload (unless -SkipUpload)')) { return }

# Authenticate before building so missing credentials do not waste a production build.
$headers = $null
if (-not $SkipUpload) {
    $headers = Get-SpaDriveHeaders -SourceRoot $SourceRoot -ClientSecretFile $ClientSecretFile `
        -TokenFile $TokenFile -ReAuth:$ReAuth
}

$previousPath = $env:PATH
$work = Get-SpaSafePath $SourceRoot ('artifacts/spa/build-' + [Guid]::NewGuid().ToString('N'))
try {
    & (Join-Path $uiPath 'use-node-portable.ps1') | Out-Host
    $npm = Join-Path $uiPath '.tools/node-v14.21.3-win-x64/npm.cmd'
    if (-not $SkipNpmInstall) {
        & $npm --prefix $uiPath ci
        if ($LASTEXITCODE -ne 0) { throw "npm ci that bai (exit $LASTEXITCODE)." }
    }
    # A unique output path prevents an old dist/ build from being packaged after a failed build.
    $buildPath = Get-SpaSafePath $SourceRoot (Join-Path $work 'ui')
    & $npm --prefix $uiPath run build -- --configuration production --output-path $buildPath
    if ($LASTEXITCODE -ne 0) { throw "UI production build that bai (exit $LASTEXITCODE)." }
    Assert-SpaBuild $buildPath
    $installed = Install-SpaClientBuild -SourceRoot $SourceRoot -BuildDirectory $buildPath
    $name = 'gv-portal-ui-' + [datetime]::UtcNow.ToString($script:SpaTimestampFormat) + '.zip'
    $package = New-SpaUiPackage -SourceRoot $SourceRoot -BuildDirectory $installed -PackagePath (Join-Path $outputPath $name)
    Write-Host "UI da copy vao: $installed"
    Write-Host "Package: $package"
    Write-Host "SHA-256: $package.sha256"
    if (-not $SkipUpload) {
        $uploaded = Send-SpaDrivePackage -Path $package -FolderId $folderId -Headers $headers
        Write-Host "Upload va checksum readback OK: $($uploaded.name)"
        if ($uploaded.PSObject.Properties['webViewLink']) { Write-Host $uploaded.webViewLink }
    }
}
finally {
    $env:PATH = $previousPath
    Remove-SpaWorkDirectory $SourceRoot $work
}
