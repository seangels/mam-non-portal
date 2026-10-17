#Requires -Version 5.1
# Launcher helpers; importing this file starts no process and performs no network IO.

function Test-SpaApiProcess {
    param([string]$SourceRoot, $Process)
    if (-not $Process -or -not $Process.ExecutablePath) { return $false }
    $candidate = [string]$Process.ExecutablePath
    if ([IO.Path]::GetFileName($candidate) -ieq 'dotnet.exe') {
        $match = [regex]::Match([string]$Process.CommandLine,
            '^\s*(?:"[^"]*"|\S+)\s+(?:exec\s+)?(?:"(?<dll>[^"]+)"|(?<dll>\S+))')
        if (-not $match.Success) { return $false }
        $candidate = $match.Groups['dll'].Value
        if ([IO.Path]::GetFileName($candidate) -ine 'AdminPortal.Api.dll') { return $false }
    }
    elseif ([IO.Path]::GetFileName($candidate) -ine 'AdminPortal.Api.exe') { return $false }
    if (-not [IO.Path]::IsPathRooted($candidate)) { return $false }
    try {
        $candidate = Get-SpaSafePath $SourceRoot $candidate
        $bin = Get-SpaSafePath $SourceRoot 'api/src/AdminPortal.Api/bin'
        return $candidate.StartsWith($bin + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
    }
    catch { return $false }
}

function Get-SpaApiListeners {
    param([string]$SourceRoot)
    $listeners = @(Get-NetTCPConnection -State Listen -ErrorAction Stop | Where-Object { $_.LocalPort -in @(7158, 5158) })
    foreach ($listener in $listeners) {
        $ownerId = [int]$listener.OwningProcess
        $owner = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId=$ownerId" -ErrorAction Stop
        if (-not (Test-SpaApiProcess $SourceRoot $owner)) {
            throw "Port $($listener.LocalPort) belongs to another application/source (PID $ownerId). Khong dung tien trinh nay."
        }
        [pscustomobject]@{ LocalPort = [int]$listener.LocalPort; ProcessId = $ownerId }
    }
}

function Get-SpaLaunchLock {
    param([string]$SourceRoot)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($SourceRoot).TrimEnd('\', '/').ToLowerInvariant())) }
    finally { $sha.Dispose() }
    $name = 'Local\GVPortal-' + [BitConverter]::ToString($hash).Replace('-', '')
    $mutex = [Threading.Mutex]::new($false, $name)
    try {
        try { $acquired = $mutex.WaitOne(0) }
        catch [Threading.AbandonedMutexException] { $acquired = $true }
        return [pscustomobject]@{ Acquired = $acquired; Mutex = $mutex }
    }
    catch { $mutex.Dispose(); throw }
}

function New-SpaDesktopShortcut {
    param([string]$SourceRoot)
    $entry = Get-SpaSafePath $SourceRoot 'deploy/spa/start-app.ps1'
    if (-not (Test-Path -LiteralPath $entry -PathType Leaf)) { throw "Thieu launcher: $entry" }
    $desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
    if (-not $desktop) { throw 'Khong xac dinh duoc Desktop cua user hien tai.' }
    $shell = New-Object -ComObject WScript.Shell
    $shortcutPath = Join-Path $desktop 'GV Portal.lnk'
    $link = $shell.CreateShortcut($shortcutPath)
    $link.TargetPath = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
    $link.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$entry`""
    $link.WorkingDirectory = $SourceRoot
    $link.Description = 'Cap nhat UI, giu/chay API nen va mo GV Portal'
    $link.IconLocation = (Join-Path $env:SystemRoot 'System32/shell32.dll') + ',13'
    $link.WindowStyle = 7
    $link.Save()
    return $shortcutPath
}

function Start-SpaApiBackground {
    param([string]$SourceRoot, [string]$RuntimePath)
    $RuntimePath = Get-SpaSafePath $SourceRoot $RuntimePath
    New-Item -ItemType Directory -Path $RuntimePath -Force | Out-Null
    $project = Get-SpaSafePath $SourceRoot 'api/src/AdminPortal.Api/AdminPortal.Api.csproj'
    $stamp = [datetime]::UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'")
    $stdout = Get-SpaSafePath $SourceRoot (Join-Path $RuntimePath "api-$stamp.stdout.log")
    $stderr = Get-SpaSafePath $SourceRoot (Join-Path $RuntimePath "api-$stamp.stderr.log")
    $settings = @{
        ASPNETCORE_ENVIRONMENT = 'Development'
        DOTNET_ENVIRONMENT = 'Development'
        Spa__ServeFromClientAppBuild = 'true'
        Spa__BuildPath = 'ClientApp/build'
    }
    $previous = @{}
    foreach ($key in $settings.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
    }
    try {
        return Start-Process -FilePath 'dotnet.exe' -ArgumentList @(
            'run', '--project', "`"$project`"", '--no-launch-profile', '--',
            '--urls', '"https://localhost:7158;http://localhost:5158"'
        ) -WorkingDirectory (Split-Path -Parent $project) -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    }
    finally {
        foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
    }
}

function Wait-SpaApiReady {
    param([string]$SourceRoot, $StartedProcess, [ValidateRange(1, 600)][int]$TimeoutSeconds = 120)
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if ($null -ne $StartedProcess) {
            $StartedProcess.Refresh()
            if ($StartedProcess.HasExited) { throw "dotnet run da thoat (exit $($StartedProcess.ExitCode)). Xem api-*.stderr.log/stdout.log." }
        }
        # Validate process ownership before every readiness request, including startup races.
        $listeners = @(Get-SpaApiListeners $SourceRoot)
        if (@($listeners | Where-Object { $_.LocalPort -eq 7158 }).Count -gt 0) {
            try {
                $remaining = [Math]::Max(1, [Math]::Min(3, [Math]::Ceiling($TimeoutSeconds - $timer.Elapsed.TotalSeconds)))
                $health = Invoke-WebRequest -Uri 'https://localhost:7158/health/live' -UseBasicParsing -TimeoutSec $remaining
                if ($health.StatusCode -eq 200) { return }
            }
            catch { } # Transient startup or TLS error; do not open the browser early.
        }
        Start-Sleep -Milliseconds 300
    }
    throw "Het $TimeoutSeconds giay cho API health (timeout). Kiem tra database, HTTPS dev certificate va api-*.log."
}

function Update-SpaAppUi {
    param([string]$SourceRoot)
    & (Join-Path $PSScriptRoot 'update-ui-from-drive.ps1') -SourceRoot $SourceRoot -IfNewer -LiveUpdate -PassThru
}

function Open-SpaAppBrowser {
    Start-Process -FilePath 'https://localhost:7158/api/v1/google-sheets/credential-smoke?return=%2F' | Out-Null
}

function Write-SpaRuntimeLog {
    param([string]$LogPath, [string]$Message)
    $line = [datetime]::UtcNow.ToString('o') + ' ' + $Message
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
    Write-Host $line
}

function Invoke-SpaAppLauncher {
    param([string]$SourceRoot, [ValidateRange(1, 600)][int]$TimeoutSeconds = 120)
    $SourceRoot = Get-SpaSourceRoot $SourceRoot
    $lock = Get-SpaLaunchLock $SourceRoot
    $logPath = $null
    $transcribing = $false
    try {
        if (-not $lock.Acquired) { return [pscustomobject]@{ Status = 'AlreadyLaunching' } }
        $runtime = Get-SpaSafePath $SourceRoot 'artifacts/spa/runtime'
        New-Item -ItemType Directory -Path $runtime -Force | Out-Null
        $logPath = Get-SpaSafePath $SourceRoot (Join-Path $runtime 'launcher.log')
        $transcript = Get-SpaSafePath $SourceRoot (Join-Path $runtime 'update.log')
        Start-Transcript -LiteralPath $transcript -Append | Out-Null
        $transcribing = $true
        Write-SpaRuntimeLog $logPath 'Launcher start; API dang chay se duoc giu nguyen.'
        Get-SpaApiListeners $SourceRoot | Out-Null
        try {
            $result = Update-SpaAppUi $SourceRoot
            Write-SpaRuntimeLog $logPath "UI status: $($result.Status)"
        }
        catch { Write-SpaRuntimeLog $logPath "UI update warning; thu dung UI hien co: $($_.Exception.Message)" }
        if (-not (Test-SpaCurrentBuild $SourceRoot)) { throw 'Khong co UI hop le de chay app. Kiem tra Drive/update.log.' }
        $apiProcess = $null
        $listeners = @(Get-SpaApiListeners $SourceRoot)
        if ($listeners.Count -eq 0) {
            $apiProcess = Start-SpaApiBackground $SourceRoot $runtime
            Write-SpaRuntimeLog $logPath "Da start dotnet run nen (PID $($apiProcess.Id))."
        }
        else { Write-SpaRuntimeLog $logPath 'Giu nguyen API da chay; khong restart.' }
        Wait-SpaApiReady -SourceRoot $SourceRoot -StartedProcess $apiProcess -TimeoutSeconds $TimeoutSeconds
        Open-SpaAppBrowser
        Write-SpaRuntimeLog $logPath 'API health OK; da mo credential-smoke tren trinh duyet.'
        return [pscustomobject]@{ Status = 'Ready' }
    }
    catch {
        if ($logPath) { Write-SpaRuntimeLog $logPath "Launcher failed: $($_.Exception.Message)" }
        throw
    }
    finally {
        if ($transcribing) { Stop-Transcript | Out-Null }
        if ($lock.Acquired) { $lock.Mutex.ReleaseMutex() }
        if ($lock.Mutex) { $lock.Mutex.Dispose() }
    }
}
