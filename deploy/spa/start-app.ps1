#Requires -Version 5.1
<#
.SYNOPSIS
Update UI if newer, keep/start the API hidden, then open the Google credential URL.
.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File .\deploy\spa\start-app.ps1
.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\spa\start-app.ps1 -CreateDesktopShortcut
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SourceRoot,
    [string]$ConfigFile,
    [switch]$CreateDesktopShortcut,
    [ValidateRange(1, 600)][int]$TimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Set-StrictMode -Version 3.0
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $PSScriptRoot '../..' }
. (Join-Path $PSScriptRoot 'SpaPackage.ps1')
. (Join-Path $PSScriptRoot 'AppLauncher.ps1')
$SourceRoot = Get-SpaSourceRoot $SourceRoot
if ($CreateDesktopShortcut) {
    if ($PSCmdlet.ShouldProcess('Desktop/GV Portal.lnk', 'Create hidden PowerShell shortcut')) {
        New-SpaDesktopShortcut -SourceRoot $SourceRoot -ConfigFile $ConfigFile | Write-Output
    }
    return
}
if (-not $PSCmdlet.ShouldProcess($SourceRoot, 'Update UI live, keep/start API hidden and open browser after health')) { return }
Invoke-SpaAppLauncher -SourceRoot $SourceRoot -TimeoutSeconds $TimeoutSeconds -ConfigFile $ConfigFile | Out-Null
