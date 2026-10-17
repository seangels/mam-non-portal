#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $PSScriptRoot '../SpaPackage.ps1')
. (Join-Path $PSScriptRoot '../AppLauncher.ps1')
$fixtureRoot = Get-SpaSafePath $repoRoot ('artifacts/spa-tests/local with spaces-' + [Guid]::NewGuid().ToString('N'))
$global:SpaLocalTests = 0
function Assert-Local {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "ASSERT FAILED: $Message" }
    $global:SpaLocalTests++
    Write-Host "PASS: $Message"
}
function Expect-LocalError {
    param([scriptblock]$Action, [string]$Pattern, [string]$Message)
    $failure = $null
    try { & $Action | Out-Null } catch { $failure = $_.Exception.Message }
    Assert-Local ($null -ne $failure -and $failure -match $Pattern) "$Message [$failure]"
}
function Invoke-LocalUpdate {
    & (Join-Path $PSScriptRoot '../update-ui-from-folder.ps1') -SourceRoot $fixtureRoot -ConfigFile $configPath -IfNewer -LiveUpdate -PassThru
}
# Any accidental HTTP/OAuth path fails closed.
function Invoke-WebRequest { throw 'Local package update must not use HTTP' }
function Invoke-RestMethod { throw 'Local package update must not use HTTP' }
function Get-SpaDriveHeaders { throw 'Local package update must not use Google OAuth' }
try {
    $defaults = Get-SpaAppConfig
    Assert-Local ($defaults.packagesDirectory -ceq 'G:\My Drive\scripts\QLMamNonPackages\packages') 'Default start-app.config.json points to the requested synced folder'
    $api = Join-Path $fixtureRoot 'api/src/AdminPortal.Api'
    $compiled = Join-Path $fixtureRoot 'compiled'
    $packages = Join-Path $fixtureRoot 'synced drive/packages'
    New-Item -ItemType Directory -Path $api, $compiled, $packages -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $api 'AdminPortal.Api.csproj') -Value '<Project />'
    Set-Content -LiteralPath (Join-Path $compiled 'index.html') -Value '<script src="main.local.js"></script>'
    Set-Content -LiteralPath (Join-Path $compiled 'main.local.js') -Value 'local fixture'
    $old = New-SpaUiPackage $fixtureRoot $compiled (Join-Path $packages 'gv-portal-ui-20261016T000000001Z.zip')
    $latest = New-SpaUiPackage $fixtureRoot $compiled (Join-Path $packages 'gv-portal-ui-20261017T000000001Z.zip')
    (Get-Item -LiteralPath $old).LastWriteTimeUtc = [datetime]'2099-01-01'
    Set-Content -LiteralPath (Join-Path $packages 'gv-portal-ui-20991317T000000001Z.zip') -Value 'invalid date'
    Set-Content -LiteralPath (Join-Path $packages 'gv-portal-ui-20991017T000000001Z.zip.partial') -Value 'still syncing'
    $configPath = Join-Path $fixtureRoot 'start-app.config.json'
    @{ packagesDirectory = 'synced drive/packages' } | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
    Assert-Local ((Get-SpaAppConfig $configPath).packagesDirectory -eq $packages) 'Resolve relative folder paths against the config location'
    $packageHash = (Get-FileHash -LiteralPath $latest).Hash
    $updated = Update-SpaAppUi -SourceRoot $fixtureRoot -ConfigFile $configPath
    Assert-Local ($updated.Status -eq 'Updated' -and $updated.PackageName -eq [IO.Path]::GetFileName($latest)) 'Launcher update helper forwards custom config and installs latest without HTTP/OAuth'
    $statePath = Join-Path $fixtureRoot 'artifacts/spa/installed-ui.json'
    $stateHash = (Get-FileHash -LiteralPath $statePath).Hash
    $current = Invoke-LocalUpdate
    Assert-Local ($current.Status -eq 'Current' -and (Get-FileHash -LiteralPath $statePath).Hash -eq $stateHash) 'Skip the already installed package and leave state untouched'
    Assert-Local ((Get-FileHash -LiteralPath $latest).Hash -eq $packageHash) 'Never modify the synced source ZIP'
    $index = Join-Path $api 'ClientApp/build/index.html'
    $indexHash = (Get-FileHash -LiteralPath $index).Hash
    $bad = Join-Path $packages 'gv-portal-ui-20261018T000000001Z.zip'
    Set-Content -LiteralPath $bad -Value 'incomplete ZIP fixture'
    Expect-LocalError { Invoke-LocalUpdate } 'archive|directory|ZIP|central|End' 'A partially synced or corrupt ZIP cannot be installed'
    Assert-Local ((Get-FileHash -LiteralPath $index).Hash -eq $indexHash -and (Get-FileHash -LiteralPath $statePath).Hash -eq $stateHash) 'Bad ZIP preserves current UI and installed marker'
    Remove-Item -LiteralPath $bad
    Remove-Item -LiteralPath $latest
    $current = Invoke-LocalUpdate
    Assert-Local ($current.Status -eq 'Current') 'Do not downgrade when only an older package is locally available'
    Remove-Item -LiteralPath $old
    $none = Invoke-LocalUpdate
    Assert-Local ($none.Status -eq 'NoPackage') 'Empty matching folder returns NoPackage'
    @{ packagesDirectory = 'folder-not-mounted' } | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
    Expect-LocalError { Invoke-LocalUpdate } 'folder|thu muc|ton tai' 'Report unavailable synced folder for launcher fallback'
    Set-Content -LiteralPath $configPath -Value '{invalid-json'
    Expect-LocalError { Get-SpaAppConfig $configPath } 'config|JSON' 'Report invalid configuration clearly'
    Set-Content -LiteralPath $configPath -Value '{"packagesDirectory":""}'
    Expect-LocalError { Get-SpaAppConfig $configPath } 'packagesDirectory' 'Require a nonempty packagesDirectory'
    Write-Host "SUCCESS: $global:SpaLocalTests local package assertions passed."
}
finally {
    Remove-SpaWorkDirectory $repoRoot $fixtureRoot
    Remove-Variable -Name SpaLocalTests -Scope Global
}
