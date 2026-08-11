[CmdletBinding()]
param(
    [switch]$UseExistingArtifacts,
    [switch]$SkipNpmInstall,
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 3.0

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "release"
}
elseif (-not [IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot $OutputDirectory
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

$deployScript = Join-Path $PSScriptRoot "deploy-iis.ps1"
$artifactRoot = Join-Path $repoRoot "artifacts\iis"
$apiArtifactPath = Join-Path $artifactRoot "api"
$uiArtifactPath = Join-Path $artifactRoot "ui"

function Invoke-RobocopyCopy {
    param(
        [string]$Source,
        [string]$Destination,
        [string[]]$ExcludedFiles = @()
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    $arguments = @($Source, $Destination, "/E", "/R:2", "/W:2", "/COPY:DAT", "/DCOPY:DAT", "/NP", "/NFL", "/NDL", "/NJH", "/NJS")
    if ($ExcludedFiles.Count -gt 0) {
        $arguments += "/XF"
        $arguments += $ExcludedFiles
    }
    & robocopy.exe @arguments
    if ($LASTEXITCODE -ge 8) {
        throw "Robocopy failed with exit code $LASTEXITCODE."
    }
}

if ($UseExistingArtifacts) {
    & $deployScript -PrepareOnly
}
else {
    $deployArguments = @{
        Build = $true
        PrepareOnly = $true
    }
    if ($SkipNpmInstall) {
        $deployArguments["SkipNpmInstall"] = $true
    }
    & $deployScript @deployArguments
}
if (-not $?) {
    throw "Artifact build or validation failed."
}

$version = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
$packageName = "gv-portal-iis-" + $version
$temporaryBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd("\")
$temporaryRoot = Join-Path $temporaryBase ("gv-portal-package-" + [Guid]::NewGuid().ToString("N"))
$bundleRoot = Join-Path $temporaryRoot $packageName
$bundleDeployPath = Join-Path $bundleRoot "deploy\iis"
$bundleArtifactPath = Join-Path $bundleRoot "artifacts\iis"

New-Item -ItemType Directory -Path $bundleDeployPath -Force | Out-Null
New-Item -ItemType Directory -Path $bundleArtifactPath -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

try {
    Copy-Item -LiteralPath $deployScript -Destination $bundleDeployPath -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "ui.web.config") -Destination $bundleDeployPath -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "HUONG-DAN-DEPLOY-IIS.md") -Destination $bundleDeployPath -Force

    Invoke-RobocopyCopy -Source $apiArtifactPath -Destination (Join-Path $bundleArtifactPath "api") -ExcludedFiles @("*.pdb")
    Invoke-RobocopyCopy -Source $uiArtifactPath -Destination (Join-Path $bundleArtifactPath "ui")

    $apiDll = Join-Path $bundleArtifactPath "api\AdminPortal.Api.dll"
    $mainBundle = Get-ChildItem -LiteralPath (Join-Path $bundleArtifactPath "ui") -Filter "main*.js" -File | Select-Object -First 1
    if (-not (Test-Path -LiteralPath $apiDll) -or $null -eq $mainBundle) {
        throw "The deployment package is missing API or UI output."
    }

    $buildInfo = @(
        "Package=$packageName",
        "BuiltAtUtc=$((Get-Date).ToUniversalTime().ToString('O'))",
        "ApiHost=https://api-gv-portal.local",
        "UiHost=https://gv-portal.local",
        "ApiDllSha256=$((Get-FileHash -LiteralPath $apiDll -Algorithm SHA256).Hash)",
        "UiMainSha256=$((Get-FileHash -LiteralPath $mainBundle.FullName -Algorithm SHA256).Hash)",
        "ContainsSecrets=false"
    )
    Set-Content -LiteralPath (Join-Path $bundleRoot "BUILD-INFO.txt") -Value $buildInfo -Encoding ASCII

    $zipPath = Join-Path $OutputDirectory ($packageName + ".zip")
    Compress-Archive -Path $bundleRoot -DestinationPath $zipPath -CompressionLevel Optimal -Force

    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    $checksumPath = $zipPath + ".sha256"
    Set-Content -LiteralPath $checksumPath -Value ($zipHash + "  " + [IO.Path]::GetFileName($zipPath)) -Encoding ASCII

    Write-Host ""
    Write-Host "IIS deployment package created." -ForegroundColor Green
    Write-Host ("ZIP    : " + $zipPath)
    Write-Host ("SHA256 : " + $zipHash)
    Write-Host ("Checksum: " + $checksumPath)
}
finally {
    $temporaryFullPath = [IO.Path]::GetFullPath($temporaryRoot)
    $expectedPrefix = $temporaryBase + [IO.Path]::DirectorySeparatorChar + "gv-portal-package-"
    if ($temporaryFullPath.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $temporaryFullPath)) {
        Remove-Item -LiteralPath $temporaryFullPath -Recurse -Force
    }
}
