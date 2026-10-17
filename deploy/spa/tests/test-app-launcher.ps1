#Requires -Version 5.1
# Offline launcher checks: never start the actual API, call Google or open a browser.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $PSScriptRoot '../SpaPackage.ps1')
$global:SpaAppTests = @{ Passed = 0; Events = [Collections.Generic.List[string]]::new(); Listeners = @(); UpdateFails = $false; HealthFails = $false; StartFails = $false }
function Assert-App {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "ASSERT FAILED: $Message" }
    $global:SpaAppTests.Passed++
    Write-Host "PASS: $Message"
}
function Expect-AppError {
    param([scriptblock]$Action, [string]$Pattern, [string]$Message)
    $errorMessage = $null
    try { & $Action | Out-Null } catch { $errorMessage = $_.Exception.Message }
    Assert-App ($null -ne $errorMessage -and $errorMessage -match $Pattern) "$Message [$errorMessage]"
}
$fixtureRoot = Get-SpaSafePath $repoRoot ('artifacts/spa-tests/app with spaces-' + [Guid]::NewGuid().ToString('N'))
try {
    $npmScripts = (Get-Content -LiteralPath (Join-Path $repoRoot 'package.json') -Raw | ConvertFrom-Json).scripts
    Assert-App ($npmScripts.'build:spa' -match '-SkipNpmInstall' -and $npmScripts.'build:spa' -notmatch '-SkipUpload') 'Root build:spa skips npm ci and keeps upload enabled'
    Assert-App ($npmScripts.'ui:update' -match 'update-ui-from-folder.ps1' -and $npmScripts.'ui:update' -match '-IfNewer -LiveUpdate') 'Root ui:update uses the configured local folder and incremental live update'
    Assert-App ($npmScripts.'app:start' -match '-WindowStyle Hidden' -and $npmScripts.'app:start' -match 'start-app.ps1') 'Root app:start invokes the hidden launcher'
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    foreach ($entry in @('build-ui-package.ps1', 'update-ui-from-drive.ps1', 'update-ui-from-folder.ps1', 'start-app.ps1')) {
        $probeOutput = Join-Path $fixtureRoot ($entry + '.stdout.log')
        $probeError = Join-Path $fixtureRoot ($entry + '.stderr.log')
        $entryProbe = Start-Process -FilePath 'powershell.exe' -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru `
            -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "./deploy/spa/$entry", '-WhatIf') `
            -RedirectStandardOutput $probeOutput -RedirectStandardError $probeError
        $null = $entryProbe.Handle
        try {
            if (-not $entryProbe.WaitForExit(10000)) { throw "Entry WhatIf timeout: $entry" }
            $entryProbe.WaitForExit()
            $entryProbe.Refresh()
            Assert-App ($entryProbe.ExitCode -eq 0) "Native PowerShell -File resolves default source root: $entry ($(Get-Content -LiteralPath $probeError -Raw))"
        }
        finally {
            if (-not $entryProbe.HasExited) { $entryProbe.Kill(); $entryProbe.WaitForExit() }
            $entryProbe.Dispose()
        }
    }
    . (Join-Path $PSScriptRoot '../AppLauncher.ps1')

    $apiPath = Join-Path $fixtureRoot 'api/src/AdminPortal.Api'
    New-Item -ItemType Directory -Path (Join-Path $apiPath 'ClientApp/build') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $apiPath 'AdminPortal.Api.csproj') -Value '<Project />'
    Set-Content -LiteralPath (Join-Path $apiPath 'ClientApp/build/index.html') -Value '<script src="main.js"></script>'
    Set-Content -LiteralPath (Join-Path $apiPath 'ClientApp/build/main.js') -Value 'fixture'
    New-Item -ItemType Directory -Path (Join-Path $fixtureRoot 'deploy/spa') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'deploy/spa/start-app.ps1') -Value '# fixture entry'

    # Test ownership using process metadata, never kill or inspect a real process.
    $sourceExe = Join-Path $apiPath 'bin/Debug/net10.0/AdminPortal.Api.exe'
    Assert-App (Test-SpaApiProcess $fixtureRoot ([pscustomobject]@{ ExecutablePath = $sourceExe; CommandLine = '' })) 'Recognize source API apphost'
    Assert-App (-not (Test-SpaApiProcess $fixtureRoot ([pscustomobject]@{ ExecutablePath = 'C:\other\AdminPortal.Api.exe'; CommandLine = '' }))) 'Reject another clone apphost'
    $sourceDll = Join-Path $apiPath 'bin/Debug/net10.0/AdminPortal.Api.dll'
    Assert-App (Test-SpaApiProcess $fixtureRoot ([pscustomobject]@{ ExecutablePath = 'C:\Program Files\dotnet\dotnet.exe'; CommandLine = "`"C:\Program Files\dotnet\dotnet.exe`" `"$sourceDll`"" })) 'Recognize dotnet DLL host with spaces'
    Assert-App (-not (Test-SpaApiProcess $fixtureRoot ([pscustomobject]@{ ExecutablePath = 'C:\Program Files\dotnet\dotnet.exe'; CommandLine = "dotnet C:\other\app.dll --note `"$sourceDll`"" }))) 'Do not infer ownership from an unrelated command-line argument'

    # Test actual background command construction, mocking only process creation.
    function Start-Process {
        [CmdletBinding()]
        param([string]$FilePath, [string[]]$ArgumentList, [string]$WorkingDirectory, [string]$WindowStyle,
            [string]$RedirectStandardOutput, [string]$RedirectStandardError, [switch]$PassThru)
        $global:SpaAppTests.StartArguments = $ArgumentList -join ' '
        Assert-App ($WindowStyle -eq 'Hidden' -and $PassThru) 'API process starts hidden and returns a handle'
        Assert-App ($env:Spa__ServeFromClientAppBuild -eq 'true' -and $env:Spa__BuildPath -eq 'ClientApp/build' -and $env:DOTNET_ENVIRONMENT -eq 'Development') 'Background API inherits SPA and Development settings'
        Assert-App ($WorkingDirectory -eq $apiPath -and $RedirectStandardOutput -like '*artifacts*runtime*' -and $RedirectStandardError -like '*artifacts*runtime*') 'API uses source working directory and separate log files'
        return [pscustomobject]@{ Id = 4242 }
    }
    $oldEnvironment = $env:Spa__ServeFromClientAppBuild
    Start-SpaApiBackground $fixtureRoot (Get-SpaSafePath $fixtureRoot 'artifacts/spa/runtime') | Out-Null
    Assert-App ($global:SpaAppTests.StartArguments.Contains('"' + (Join-Path $apiPath 'AdminPortal.Api.csproj') + '"') -and
        $global:SpaAppTests.StartArguments -match '--no-launch-profile -- --urls "https://localhost:7158;http://localhost:5158"') 'Quote project paths and bind fixed HTTPS/HTTP URLs'
    Assert-App ($env:Spa__ServeFromClientAppBuild -eq $oldEnvironment) 'Restore caller environment after spawning API'
    Remove-Item -LiteralPath 'Function:\Start-Process'

    # Shortcut creation is verified via fake COM; no real Desktop write.
    function New-Object {
        [CmdletBinding()]
        param([string]$ComObject)
        if ($ComObject -ne 'WScript.Shell') { throw 'Unexpected COM request' }
        $shell = [pscustomobject]@{}
        $shell | Add-Member -MemberType ScriptMethod -Name CreateShortcut -Value {
            param($path)
            $global:SpaAppTests.ShortcutPath = $path
            $link = [pscustomobject]@{ TargetPath = ''; Arguments = ''; WorkingDirectory = ''; Description = ''; WindowStyle = 0; IconLocation = '' }
            $link | Add-Member -MemberType ScriptMethod -Name Save -Value { $global:SpaAppTests.Shortcut = $this }
            return $link
        }
        return $shell
    }
    New-SpaDesktopShortcut $fixtureRoot | Out-Null
    Assert-App ($global:SpaAppTests.ShortcutPath -like '*GV Portal.lnk' -and $global:SpaAppTests.Shortcut.Arguments.Contains('-WindowStyle Hidden -File "' + (Join-Path $fixtureRoot 'deploy/spa/start-app.ps1') + '"')) 'Desktop shortcut uses hidden PowerShell and a quoted absolute entry path'
    Assert-App ($global:SpaAppTests.Shortcut.WorkingDirectory -eq $fixtureRoot) 'Desktop shortcut works independently of Desktop working directory'
    $customConfig = Join-Path $fixtureRoot 'custom settings.json'
    New-SpaDesktopShortcut -SourceRoot $fixtureRoot -ConfigFile $customConfig | Out-Null
    Assert-App ($global:SpaAppTests.Shortcut.Arguments.Contains('-ConfigFile "' + $customConfig + '"')) 'Shortcut preserves an explicitly selected config path with spaces'
    Remove-Item -LiteralPath 'Function:\New-Object'

    # A real second PowerShell process must be excluded by the source-root mutex.
    $probePath = Join-Path $fixtureRoot 'mutex-probe.ps1'
    $probeResult = Join-Path $fixtureRoot 'mutex-result.txt'
    @'
param($Helpers, $Root, $Result)
$ErrorActionPreference = 'Stop'
. (Join-Path $Helpers 'SpaPackage.ps1')
. (Join-Path $Helpers 'AppLauncher.ps1')
$probeLock = Get-SpaLaunchLock $Root
try { [IO.File]::WriteAllText($Result, [string]$probeLock.Acquired) }
finally {
    if ($probeLock.Acquired) { $probeLock.Mutex.ReleaseMutex() }
    $probeLock.Mutex.Dispose()
}
'@ | Set-Content -LiteralPath $probePath -Encoding UTF8
    $heldLock = Get-SpaLaunchLock $fixtureRoot
    $probeProcess = $null
    try {
        $probeProcess = Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -PassThru -ArgumentList @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$probePath`"",
            '-Helpers', "`"$([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')))`"",
            '-Root', "`"$fixtureRoot`"", '-Result', "`"$probeResult`""
        )
        if (-not $probeProcess.WaitForExit(10000)) { throw 'Fixture mutex probe did not exit' }
        Assert-App ($probeProcess.ExitCode -eq 0 -and (Get-Content -LiteralPath $probeResult -Raw) -eq 'False') 'Real cross-process mutex prevents simultaneous launch for the same source'
    }
    finally {
        if ($probeProcess -and -not $probeProcess.HasExited) { $probeProcess.Kill(); $probeProcess.WaitForExit() }
        if ($probeProcess) { $probeProcess.Dispose() }
        $heldLock.Mutex.ReleaseMutex()
        $heldLock.Mutex.Dispose()
    }

    # Keep the real readiness implementation for failure/timeout checks later.
    $readinessImplementation = ${function:Wait-SpaApiReady}
    function Get-SpaApiListeners {
        param([string]$SourceRoot)
        $global:SpaAppTests.Events.Add('ports')
        if ($global:SpaAppTests.ContainsKey('PortError') -and $global:SpaAppTests.PortError) { throw 'Port belongs to another application' }
        return $global:SpaAppTests.Listeners
    }
    function Update-SpaAppUi {
        param([string]$SourceRoot, [string]$ConfigFile)
        $global:SpaAppTests.Events.Add('update')
        if ($global:SpaAppTests.UpdateFails) { throw 'Unavailable package folder fixture' }
        return [pscustomobject]@{ Status = 'Updated' }
    }
    function Start-SpaApiBackground {
        param([string]$SourceRoot, [string]$RuntimePath)
        $global:SpaAppTests.Events.Add('start')
        if ($global:SpaAppTests.StartFails) { throw 'Simulated dotnet failure' }
        $global:SpaAppTests.Listeners = @([pscustomobject]@{ LocalPort = 7158; ProcessId = 4242 })
        return [pscustomobject]@{ Id = 4242 }
    }
    function Wait-SpaApiReady {
        param([string]$SourceRoot, $StartedProcess, [int]$TimeoutSeconds)
        $global:SpaAppTests.Events.Add('health')
        if ($global:SpaAppTests.HealthFails) { throw 'Health timeout fixture' }
    }
    function Open-SpaAppBrowser {
        $global:SpaAppTests.Events.Add('browser')
    }

    Invoke-SpaAppLauncher $fixtureRoot | Out-Null
    Assert-App (($global:SpaAppTests.Events -join ',') -match 'update,ports,start,health,browser') 'New instance: update UI, start API, wait for health, then open browser'
    $global:SpaAppTests.Events.Clear()
    Invoke-SpaAppLauncher $fixtureRoot | Out-Null
    Assert-App ($global:SpaAppTests.Events -contains 'update' -and $global:SpaAppTests.Events -notcontains 'start' -and $global:SpaAppTests.Events -contains 'browser') 'Existing API stays running even when UI update reports Updated'
    $global:SpaAppTests.Events.Clear()
    $global:SpaAppTests.UpdateFails = $true
    Invoke-SpaAppLauncher $fixtureRoot | Out-Null
    Assert-App ($global:SpaAppTests.Events -contains 'browser' -and $global:SpaAppTests.Events -notcontains 'start') 'Unavailable package folder falls back to valid current UI and keeps API'
    $global:SpaAppTests.Listeners = @()
    $global:SpaAppTests.Events.Clear()
    Invoke-SpaAppLauncher $fixtureRoot | Out-Null
    Assert-App ($global:SpaAppTests.Events -contains 'start' -and $global:SpaAppTests.Events -contains 'browser') 'Unavailable folder still starts API when current UI is valid and API is not running'
    Remove-Item -LiteralPath (Join-Path $apiPath 'ClientApp/build/main.js')
    $global:SpaAppTests.Events.Clear()
    Expect-AppError { Invoke-SpaAppLauncher $fixtureRoot } 'UI' 'Unavailable folder with no valid UI stops launcher'
    Assert-App ($global:SpaAppTests.Events -notcontains 'browser' -and $global:SpaAppTests.Events -notcontains 'start') 'No valid UI never starts API or opens browser'
    Set-Content -LiteralPath (Join-Path $apiPath 'ClientApp/build/main.js') -Value 'fixture'
    $global:SpaAppTests.UpdateFails = $false
    $global:SpaAppTests.PortError = $true
    $global:SpaAppTests.Events.Clear()
    Expect-AppError { Invoke-SpaAppLauncher $fixtureRoot } 'another application' 'Occupied foreign port stops launcher'
    Assert-App ($global:SpaAppTests.Events -notcontains 'update' -and $global:SpaAppTests.Events -notcontains 'start') 'Foreign port is detected before updating UI or starting processes'
    $global:SpaAppTests.PortError = $false
    $global:SpaAppTests.HealthFails = $true
    $global:SpaAppTests.Events.Clear()
    Expect-AppError { Invoke-SpaAppLauncher $fixtureRoot } 'timeout' 'Readiness failure is reported'
    Assert-App ($global:SpaAppTests.Events -notcontains 'browser') 'Do not open browser after health failure'
    $global:SpaAppTests.HealthFails = $false
    $global:SpaAppTests.Listeners = @()
    $global:SpaAppTests.StartFails = $true
    Expect-AppError { Invoke-SpaAppLauncher $fixtureRoot } 'dotnet failure' 'API startup failure is reported'
    $global:SpaAppTests.StartFails = $false
    Assert-App ((Get-Content -LiteralPath (Join-Path $fixtureRoot 'artifacts/spa/runtime/launcher.log') -Raw) -match 'Unavailable package folder fixture') 'Persist update errors and launcher failures in runtime logs'

    function Get-SpaLaunchLock { param([string]$SourceRoot) return [pscustomobject]@{ Acquired = $false; Mutex = $null } }
    $global:SpaAppTests.Events.Clear()
    $busy = Invoke-SpaAppLauncher $fixtureRoot
    Assert-App ($busy.Status -eq 'AlreadyLaunching' -and $global:SpaAppTests.Events.Count -eq 0) 'Second click during launch does no work and opens no duplicate browser'
    Remove-Item -LiteralPath 'Function:\Get-SpaLaunchLock'

    # Real readiness loop with mocked HTTP/listeners, including actual one-second timeout.
    function Invoke-WebRequest {
        [CmdletBinding()]
        param([string]$Uri, [switch]$UseBasicParsing, [int]$TimeoutSec)
        Assert-App ($Uri -eq 'https://localhost:7158/health/live') 'Readiness uses HTTPS health, not credential-smoke'
        return [pscustomobject]@{ StatusCode = 200 }
    }
    $global:SpaAppTests.Listeners = @([pscustomobject]@{ LocalPort = 7158; ProcessId = 4242 })
    & $readinessImplementation -SourceRoot $fixtureRoot -TimeoutSeconds 1
    function Invoke-WebRequest {
        [CmdletBinding()]
        param([string]$Uri, [switch]$UseBasicParsing, [int]$TimeoutSec)
        throw [Net.WebException]::new('fixture TLS certificate rejected')
    }
    Expect-AppError { & $readinessImplementation -SourceRoot $fixtureRoot -TimeoutSeconds 1 } 'fixture TLS certificate rejected' 'Timeout reports the actual HTTPS failure instead of hiding certificate errors'
    $global:SpaAppTests.Listeners = @()
    Expect-AppError { & $readinessImplementation -SourceRoot $fixtureRoot -TimeoutSeconds 1 } 'Chua.*7158' 'Timeout distinguishes an API that has not opened the HTTPS port'
    $exitedProcess = [pscustomobject]@{ HasExited = $true; ExitCode = 7 }
    $exitedProcess | Add-Member -MemberType ScriptMethod -Name Refresh -Value { }
    Expect-AppError { & $readinessImplementation -SourceRoot $fixtureRoot -StartedProcess $exitedProcess -TimeoutSeconds 1 } 'thoat.*7' 'Detect dotnet run exiting before readiness instead of waiting or opening browser'

    # Verify the actual browser URL via a Start-Process mock.
    . (Join-Path $PSScriptRoot '../AppLauncher.ps1')
    function Start-Process { param([string]$FilePath) $global:SpaAppTests.BrowserUrl = $FilePath }
    Open-SpaAppBrowser
    Assert-App ($global:SpaAppTests.BrowserUrl -ceq 'https://localhost:7158/api/v1/google-sheets/credential-smoke?return=%2F') 'Open the exact requested Google credential URL'
    Remove-Item -LiteralPath 'Function:\Start-Process'
    Write-Host "SUCCESS: $($global:SpaAppTests.Passed) launcher assertions passed."
}
finally {
    Remove-SpaWorkDirectory $repoRoot $fixtureRoot
    Remove-Variable -Name SpaAppTests -Scope Global
}
