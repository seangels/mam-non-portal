[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SourcePath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 3.0

$uiRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repoRoot = [IO.Path]::GetFullPath((Join-Path $uiRoot ".."))

if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    $SourcePath = Join-Path $uiRoot "dist\DevExtreme-app"
}
elseif (-not [IO.Path]::IsPathRooted($SourcePath)) {
    $SourcePath = Join-Path $uiRoot $SourcePath
}

$sourceFullPath = [IO.Path]::GetFullPath($SourcePath).TrimEnd("\")
$destinationFullPath = [IO.Path]::GetFullPath(
    (Join-Path $repoRoot "api\src\AdminPortal.Api\ClientApp\build")
).TrimEnd("\")
$expectedDestination = [IO.Path]::GetFullPath(
    (Join-Path $repoRoot "api\src\AdminPortal.Api\ClientApp\build")
).TrimEnd("\")

if ($destinationFullPath -ne $expectedDestination) {
    throw "Destination path is not the expected API ClientApp build folder: $destinationFullPath"
}

if (-not (Test-Path -LiteralPath $sourceFullPath -PathType Container)) {
    throw "Build output was not found: $sourceFullPath. Run Angular build first."
}

if (-not (Test-Path -LiteralPath (Join-Path $sourceFullPath "index.html") -PathType Leaf)) {
    throw "Build output is missing index.html: $sourceFullPath"
}

if (-not (Test-Path -LiteralPath $destinationFullPath -PathType Container)) {
    if ($PSCmdlet.ShouldProcess($destinationFullPath, "Create API ClientApp build folder")) {
        New-Item -ItemType Directory -Path $destinationFullPath -Force | Out-Null
    }
}

$itemsToRemove = @(
    Get-ChildItem -LiteralPath $destinationFullPath -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne ".gitkeep" }
)
foreach ($item in $itemsToRemove) {
    if ($PSCmdlet.ShouldProcess($item.FullName, "Remove old API ClientApp build item")) {
        Remove-Item -LiteralPath $item.FullName -Recurse -Force
    }
}

$itemsToCopy = @(Get-ChildItem -LiteralPath $sourceFullPath -Force)
foreach ($item in $itemsToCopy) {
    if ($PSCmdlet.ShouldProcess($item.FullName, "Copy Angular build output item")) {
        Copy-Item -LiteralPath $item.FullName -Destination $destinationFullPath -Recurse -Force
    }
}

if ($WhatIfPreference) {
    Write-Host ("Validated copy from '{0}' to '{1}' with -WhatIf; no files were changed." -f $sourceFullPath, $destinationFullPath)
}
else {
    Write-Host ("Copied Angular build output from '{0}' to '{1}'." -f $sourceFullPath, $destinationFullPath)
}
