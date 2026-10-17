#Requires -Version 5.1
# Offline regression checks. No production build or real HTTP requests.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $PSScriptRoot '../SpaPackage.ps1')
. (Join-Path $PSScriptRoot '../GoogleDrive.ps1')
$fixtureRoot = Get-SpaSafePath $repoRoot ('artifacts/spa-tests/run-' + [Guid]::NewGuid().ToString('N'))
$global:SpaOfflineTestState = @{ Passed = 0; PageCalls = 0; ReadbackCalls = 0; DownloadCalls = 0; Metadata = $null; EmptyDrive = $false }

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "ASSERT FAILED: $Message" }
    $global:SpaOfflineTestState.Passed++
    Write-Host "PASS: $Message"
}
function Assert-Throws {
    param([scriptblock]$Action, [string]$Pattern, [string]$Message)
    $caught = $null
    try { & $Action | Out-Null } catch { $caught = $_.Exception.Message }
    Assert-True ($null -ne $caught -and $caught -match $Pattern) "$Message [$caught]"
}
function Write-FixtureFile {
    param([string]$RelativePath, [string]$Content)
    $path = Get-SpaSafePath $fixtureRoot $RelativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
    Set-Content -LiteralPath $path -Value $Content -Encoding UTF8
    return $path
}
function New-FixtureZip {
    param([string]$Name, [string[]]$Entries)
    $path = Get-SpaSafePath $fixtureRoot $Name
    $zip = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in $Entries) {
            $entry = $zip.CreateEntry($name)
            $writer = [IO.StreamWriter]::new($entry.Open())
            try { $writer.Write('fixture') } finally { $writer.Dispose() }
        }
    }
    finally { $zip.Dispose() }
    return $path
}

try {
    foreach ($scriptFile in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '..') -Filter '*.ps1' -Recurse -File) {
        $tokens = $null
        $errors = $null
        [Management.Automation.Language.Parser]::ParseFile($scriptFile.FullName, [ref]$tokens, [ref]$errors) | Out-Null
        Assert-True ($errors.Count -eq 0) "PowerShell syntax: $($scriptFile.Name)"
    }
    Write-FixtureFile 'api/src/AdminPortal.Api/AdminPortal.Api.csproj' '<Project />' | Out-Null
    Assert-True ((Get-SpaSourceRoot $fixtureRoot) -eq $fixtureRoot) 'Validate a source clone'
    Assert-Throws { Get-SpaSafePath $fixtureRoot '../outside' } 'phai nam trong' 'Block path escape'
    Assert-Throws { Get-SpaSafePath $fixtureRoot $fixtureRoot } 'phai nam trong' 'Block deleting/moving the source root itself'

    $old = [pscustomobject]@{ id = 'old'; name = 'gv-portal-ui-20261016T235959999Z.zip'; modifiedTime = '2099-01-01' }
    $new = [pscustomobject]@{ id = 'new'; name = 'gv-portal-ui-20261017T000000001Z.zip'; modifiedTime = '2000-01-01' }
    $unrelated = @(
        [pscustomobject]@{ name = 'gv-portal-iis-20990101-000000.zip' },
        [pscustomobject]@{ name = 'gv-portal-ui-20261317T000000001Z.zip' },
        [pscustomobject]@{ name = 'gv-portal-ui-20260230T000000001Z.zip' },
        [pscustomobject]@{ name = 'gv-portal-ui-20991017T000000001Z.zip.sha256' },
        [pscustomobject]@{ name = 'prefix-gv-portal-ui-20991017T000000001Z.zip' }
    )
    $latest = Get-SpaLatestPackage -Files (@($old, $new) + $unrelated)
    Assert-True ($latest.File.id -eq 'new' -and $latest.Timestamp.Kind -eq [DateTimeKind]::Utc) 'Select filename timestamp in UTC, ignoring Drive modifiedTime and invalid names/dates'
    Assert-Throws { Get-SpaLatestPackage -Files $unrelated } 'Khong co goi' 'Fail clearly with no matching package'
    Assert-Throws { Get-SpaLatestPackage -Files @($new, $new) } 'nhieu goi' 'Reject ambiguous newest timestamp'
    Assert-Throws { Get-SpaLatestPackage -Files @($new) -PackageRegex '.*' } 'named group' 'Require named timestamp group'
    $custom = Get-SpaLatestPackage -Files @([pscustomobject]@{ name = 'custom-20261017-103000.zip' }) `
        -PackageRegex '\Acustom-(?<timestamp>\d{8}-\d{6})\.zip\z' -TimestampFormat 'yyyyMMdd-HHmmss'
    Assert-True ($custom.Timestamp.Hour -eq 10) 'Support a custom filename regex and timestamp format'

    Write-FixtureFile 'compiled/index.html' '<script src="main.123.js"></script>' | Out-Null
    Write-FixtureFile 'compiled/main.123.js' 'console.log("fixture");' | Out-Null
    Write-FixtureFile 'compiled/assets/nested/data.json' '{"fixture":true}' | Out-Null
    Write-FixtureFile 'compiled/.gitkeep' '' | Out-Null
    $build = Join-Path $fixtureRoot 'compiled'
    $package = New-SpaUiPackage $fixtureRoot $build (Join-Path $fixtureRoot $new.name)
    $zip = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $names = @($zip.Entries | ForEach-Object { $_.FullName })
        Assert-True ($names.Count -eq 3 -and @($names | Where-Object { $_ -notlike 'ClientApp/build/*' }).Count -eq 0) 'ZIP contains only ClientApp/build files; excludes .gitkeep'
    }
    finally { $zip.Dispose() }
    $sha = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    Assert-True ((Get-Content -LiteralPath ($package + '.sha256') -Raw).StartsWith($sha, [StringComparison]::OrdinalIgnoreCase)) 'Local SHA-256 sidecar matches package'
    $expanded = Expand-SpaUiPackage $fixtureRoot $package (Join-Path $fixtureRoot 'expanded')
    Assert-True (Test-Path -LiteralPath (Join-Path $expanded 'assets/nested/data.json')) 'Extract nested SPA assets'

    $targetRelative = 'api/src/AdminPortal.Api/ClientApp/build'
    Write-FixtureFile "$targetRelative/index.html" 'old-index' | Out-Null
    Write-FixtureFile "$targetRelative/main.old.js" 'old-main' | Out-Null
    Write-FixtureFile "$targetRelative/stale.js" 'old-stale' | Out-Null
    Write-FixtureFile "$targetRelative/.gitkeep" '' | Out-Null
    $config = Write-FixtureFile 'api/src/AdminPortal.Api/appsettings.Development.json' '{"fixture":"keep"}'
    $configHash = (Get-FileHash -LiteralPath $config).Hash
    $installed = Install-SpaClientBuild $fixtureRoot $expanded
    Assert-True ((Test-Path -LiteralPath (Join-Path $installed 'main.123.js')) -and -not (Test-Path -LiteralPath (Join-Path $installed 'stale.js'))) 'Replace build and remove stale assets'
    Assert-True ((Test-Path -LiteralPath (Join-Path $installed '.gitkeep')) -and (Get-FileHash -LiteralPath $config).Hash -eq $configHash) 'Preserve .gitkeep and API configuration'
    $backups = @(Get-ChildItem -LiteralPath (Join-Path $fixtureRoot 'artifacts/spa/backups') -Directory)
    Assert-True ($backups.Count -eq 1 -and (Test-Path -LiteralPath (Join-Path $backups[0].FullName 'main.old.js'))) 'Keep previous UI build for recovery'

    Write-FixtureFile 'broken/index.html' '<script src="main.missing.js"></script>' | Out-Null
    Write-FixtureFile 'broken/main.stale.js' 'old retained bundle' | Out-Null
    Assert-Throws { Assert-SpaBuild (Join-Path $fixtureRoot 'broken') } 'asset|tham chieu' 'A stale retained main bundle cannot hide a missing bundle referenced by index'

    Write-FixtureFile 'live-next/index.html' '<script src="main.next.js"></script>' | Out-Null
    Write-FixtureFile 'live-next/main.next.js' 'new bundle' | Out-Null
    Write-FixtureFile 'live-next/assets/nested/data.json' '{"fixture":"changed"}' | Out-Null
    $global:SpaOfflineTestState.LiveImplementation = ${function:Set-SpaLiveFile}
    $global:SpaOfflineTestState.LiveWrites = [Collections.Generic.List[string]]::new()
    $global:SpaOfflineTestState.FailIndexOnce = $true
    function Set-SpaLiveFile {
        param([string]$SourceRoot, [string]$Source, [string]$Target)
        $global:SpaOfflineTestState.LiveWrites.Add([IO.Path]::GetFileName($Target))
        if ([IO.Path]::GetFileName($Target) -eq 'index.html' -and $global:SpaOfflineTestState.FailIndexOnce) {
            $global:SpaOfflineTestState.FailIndexOnce = $false
            throw 'Fixture index write failure'
        }
        & $global:SpaOfflineTestState.LiveImplementation $SourceRoot $Source $Target
    }
    $indexHash = (Get-FileHash -LiteralPath (Join-Path $installed 'index.html')).Hash
    $assetHash = (Get-FileHash -LiteralPath (Join-Path $installed 'assets/nested/data.json')).Hash
    try { Assert-Throws { Install-SpaClientBuild $fixtureRoot (Join-Path $fixtureRoot 'live-next') -LiveUpdate } 'Fixture index write failure' 'Rollback when the final index publication fails' }
    finally { Set-Item -LiteralPath Function:\Set-SpaLiveFile -Value $global:SpaOfflineTestState.LiveImplementation }
    Assert-True ($global:SpaOfflineTestState.LiveWrites.IndexOf('index.html') -eq 2) 'Publish all new assets before index.html'
    Assert-True ((Get-FileHash -LiteralPath (Join-Path $installed 'index.html')).Hash -eq $indexHash -and
        (Get-FileHash -LiteralPath (Join-Path $installed 'assets/nested/data.json')).Hash -eq $assetHash -and
        -not (Test-Path -LiteralPath (Join-Path $installed 'main.next.js'))) 'Failed live update restores overwritten assets/index and removes newly added bundles'

    # Simulate failure specifically when moving the staged build to the API target.
    function Move-Item {
        [CmdletBinding()]
        param([string]$LiteralPath, [string]$Destination)
        if ((Split-Path -Leaf $LiteralPath) -like 'stage-*') { throw 'Simulated swap failure' }
        Microsoft.PowerShell.Management\Move-Item -LiteralPath $LiteralPath -Destination $Destination
    }
    try { Assert-Throws { Install-SpaClientBuild $fixtureRoot $expanded } 'Simulated swap failure' 'Report a failed UI swap' }
    finally { Remove-Item -LiteralPath 'Function:\Move-Item' }
    Assert-True (Test-Path -LiteralPath (Join-Path $installed 'main.123.js')) 'Restore previous build after a failed swap'

    $badCases = @(
        @{ Name = 'traversal'; Entries = @('ClientApp/build/../../outside.txt'); Error = 'duong dan' },
        @{ Name = 'outside'; Entries = @('appsettings.Development.json'); Error = 'ngoai' },
        @{ Name = 'absolute'; Entries = @('ClientApp/build/C:/outside.txt'); Error = 'duong dan' },
        @{ Name = 'backslash'; Entries = @('ClientApp/build/..\outside.txt'); Error = 'ngoai' },
        @{ Name = 'duplicate'; Entries = @('ClientApp/build/index.html', 'ClientApp/build/INDEX.html'); Error = 'trung ten' },
        @{ Name = 'missing-main'; Entries = @('ClientApp/build/index.html'); Error = 'main' },
        @{ Name = 'secret'; Entries = @('ClientApp/build/index.html', 'ClientApp/build/main.js', 'ClientApp/build/client_secret.json'); Error = 'credential' }
    )
    foreach ($case in $badCases) {
        $badZip = New-FixtureZip ($case.Name + '.zip') $case.Entries
        $badOutput = Join-Path $fixtureRoot ('bad-' + $case.Name)
        Assert-Throws { Expand-SpaUiPackage $fixtureRoot $badZip $badOutput } $case.Error "Reject ZIP: $($case.Name)"
        Assert-True (-not (Test-Path -LiteralPath $badOutput)) "Clean failed extraction: $($case.Name)"
    }
    Assert-True (Test-Path -LiteralPath (Join-Path $installed 'main.123.js')) 'Rejected ZIPs leave the installed UI intact'
    $metadata = [pscustomobject]@{ id = 'new'; name = $new.name; size = (Get-Item -LiteralPath $package).Length; md5Checksum = (Get-FileHash -LiteralPath $package -Algorithm MD5).Hash }
    Assert-SpaDriveDownload $package $metadata
    Assert-True $true 'Validate Drive size and checksum'
    $wrongChecksum = [pscustomobject]@{ size = $metadata.size; md5Checksum = '00000000000000000000000000000000' }
    Assert-Throws { Assert-SpaDriveDownload $package $wrongChecksum } 'checksum' 'Reject checksum mismatch'
    Assert-Throws { Assert-SpaDriveDownload $package ([pscustomobject]@{ size = $metadata.size }) } 'metadata' 'Reject missing checksum metadata'

    # Every HTTP command is mocked from this point; unexpected requests fail closed.
    $global:SpaOfflineTestState.Metadata = $metadata
    function Invoke-RestMethod {
        [CmdletBinding()]
        param([string]$Method, [string]$Uri, [hashtable]$Headers, $Body, [string]$ContentType, [switch]$UseBasicParsing, [int]$TimeoutSec)
        if ($Uri -eq 'https://oauth2.googleapis.com/token') {
            if ($Body.grant_type -ne 'refresh_token') { throw 'Unexpected OAuth consent in offline test' }
            return [pscustomobject]@{ access_token = 'offline-fixture-token' }
        }
        if ($Uri -match '/drive/v3/files\?') {
            $global:SpaOfflineTestState.PageCalls++
            if ($global:SpaOfflineTestState.EmptyDrive) { return [pscustomobject]@{ files = @(); incompleteSearch = $false } }
            if ($Uri -notmatch 'pageToken=') {
                return [pscustomobject]@{ files = @($old) + $unrelated; nextPageToken = 'page-2'; incompleteSearch = $false }
            }
            return [pscustomobject]@{ files = @($global:SpaOfflineTestState.Metadata); incompleteSearch = $false }
        }
        if ($Uri -match '/drive/v3/files/uploaded-fixture\?') {
            $global:SpaOfflineTestState.ReadbackCalls++
            return $global:SpaOfflineTestState.Metadata
        }
        throw "Unexpected REST request in offline test: $Method $Uri"
    }
    function Invoke-WebRequest {
        [CmdletBinding()]
        param([string]$Method, [string]$Uri, [hashtable]$Headers, $Body, [string]$ContentType,
            [string]$OutFile, [string]$InFile, [switch]$UseBasicParsing, [int]$TimeoutSec)
        if ($Method -eq 'Post' -and $Uri -match '/upload/drive/v3/files\?') {
            $uploadMetadata = $Body | ConvertFrom-Json
            Assert-True ($uploadMetadata.name -eq $global:SpaOfflineTestState.Metadata.name -and $uploadMetadata.parents[0] -eq 'fixture-folder-id') 'Upload exact package to configured folder'
            return [pscustomobject]@{ Headers = @{ Location = 'https://www.googleapis.com/upload-fixture' } }
        }
        if ($Method -eq 'Put' -and $Uri -eq 'https://www.googleapis.com/upload-fixture') {
            Assert-True ($InFile -eq $package) 'Upload file bytes from selected package'
            return [pscustomobject]@{ Content = '{"id":"uploaded-fixture"}' }
        }
        if ($Method -eq 'Get' -and $Uri -match '/drive/v3/files/new\?alt=media') {
            $global:SpaOfflineTestState.DownloadCalls++
            if ($global:SpaOfflineTestState.ContainsKey('CorruptDownload') -and $global:SpaOfflineTestState.CorruptDownload) {
                Set-Content -LiteralPath $OutFile -Value 'corrupt download fixture'
            }
            else { Copy-Item -LiteralPath $package -Destination $OutFile }
            return
        }
        throw "Unexpected web request in offline test: $Method $Uri"
    }
    $headers = @{ Authorization = 'Bearer offline-fixture-token' }
    $listed = @(Get-SpaDriveFiles 'fixture-folder-id' $headers)
    Assert-True ($global:SpaOfflineTestState.PageCalls -eq 2 -and (Get-SpaLatestPackage $listed).File.id -eq 'new') 'Find newest package on second Drive page'
    Send-SpaDrivePackage $package 'fixture-folder-id' $headers | Out-Null
    Assert-True ($global:SpaOfflineTestState.ReadbackCalls -eq 1) 'Read back uploaded package metadata and checksum'

    # The builder runs only this fake npm.cmd in a fake clone; no Angular build.
    Write-FixtureFile 'ui/use-node-portable.ps1' 'Write-Output "Offline fixture toolchain"' | Out-Null
    Write-FixtureFile 'ui/.tools/node-v14.21.3-win-x64/npm.cmd' @'
@echo off
if "%3"=="ci" exit /b 0
if not "%3"=="run" exit /b 91
if not "%4"=="build" exit /b 92
if not "%6"=="--configuration" exit /b 93
if not "%7"=="production" exit /b 94
if not "%8"=="--output-path" exit /b 95
if defined SPA_OFFLINE_BUILD_FAIL exit /b 7
mkdir "%~9"
echo ^<script src="main.fixture.js"^>^</script^> > "%~9\index.html"
echo console.log('fixture'); > "%~9\main.fixture.js"
exit /b 0
'@ | Out-Null
    $originalPath = $env:PATH
    & (Join-Path $PSScriptRoot '../build-ui-package.ps1') -SourceRoot $fixtureRoot -SkipUpload
    $builtPackages = @(Get-ChildItem -LiteralPath (Join-Path $fixtureRoot 'release/spa') -Filter '*.zip' -File)
    Assert-True ($builtPackages.Count -eq 1 -and (Test-Path -LiteralPath (Join-Path $installed 'main.fixture.js'))) 'Builder end-to-end with fake npm: ci, explicit production config, fresh output, ClientApp copy and ZIP'
    Assert-True ($env:PATH -eq $originalPath) 'Builder restores session PATH'
    $buildHash = (Get-FileHash -LiteralPath (Join-Path $installed 'main.fixture.js')).Hash
    $env:SPA_OFFLINE_BUILD_FAIL = '1'
    try {
        Assert-Throws { & (Join-Path $PSScriptRoot '../build-ui-package.ps1') -SourceRoot $fixtureRoot -SkipUpload -SkipNpmInstall } 'build that bai.*7' 'Stop packaging when npm build fails'
    }
    finally { Remove-Item -LiteralPath Env:\SPA_OFFLINE_BUILD_FAIL }
    Assert-True ((Get-FileHash -LiteralPath (Join-Path $installed 'main.fixture.js')).Hash -eq $buildHash -and
        @(Get-ChildItem -LiteralPath (Join-Path $fixtureRoot 'release/spa') -Filter '*.zip' -File).Count -eq 1) 'Failed build preserves current ClientApp and emits no stale package'
    Assert-True (@(Get-ChildItem -LiteralPath (Join-Path $fixtureRoot 'artifacts/spa') -Filter 'build-*' -Directory).Count -eq 0) 'Builder cleans temporary output after success and failure'

    $credential = Write-FixtureFile 'api/src/AdminPortal.Api/client_secret.json' '{"installed":{"client_id":"offline-fixture-client","client_secret":"offline-fixture-secret"}}'
    $credentialHash = (Get-FileHash -LiteralPath $credential).Hash
    $downloadToken = Write-FixtureFile 'deploy/spa/download.token.json' '{"client_id":"offline-fixture-client","scope":"https://www.googleapis.com/auth/drive.readonly","refresh_token":"offline-fixture-refresh"}'
    # Run the real updater against the fixture clone, using only mocked Google HTTP.
    & (Join-Path $PSScriptRoot '../update-ui-from-drive.ps1') -SourceRoot $fixtureRoot -DriveFolder 'fixture-folder-id'
    Assert-True ($global:SpaOfflineTestState.DownloadCalls -eq 1 -and (Test-Path -LiteralPath (Join-Path $installed 'main.123.js'))) 'Updater end-to-end: refresh, paginate, select, download, verify, extract and install'
    Assert-True ((Get-FileHash -LiteralPath $credential).Hash -eq $credentialHash -and (Get-FileHash -LiteralPath $config).Hash -eq $configHash) 'Updater preserves copied credential and Development config'

    $statePath = Join-Path $fixtureRoot 'artifacts/spa/installed-ui.json'
    Assert-True (Test-Path -LiteralPath $statePath) 'Persist installed version after successful install'
    $current = & (Join-Path $PSScriptRoot '../update-ui-from-drive.ps1') -SourceRoot $fixtureRoot -DriveFolder 'fixture-folder-id' -IfNewer -LiveUpdate -PassThru
    Assert-True ($current.Status -eq 'Current' -and $global:SpaOfflineTestState.DownloadCalls -eq 1) 'Same package skips download and copy'
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $state.timestampUtc = '2099-01-01T00:00:00.0000000Z'
    $state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8
    $current = & (Join-Path $PSScriptRoot '../update-ui-from-drive.ps1') -SourceRoot $fixtureRoot -DriveFolder 'fixture-folder-id' -IfNewer -LiveUpdate -PassThru
    Assert-True ($current.Status -eq 'Current' -and $global:SpaOfflineTestState.DownloadCalls -eq 1) 'Never downgrade a valid installed UI'
    Remove-Item -LiteralPath $statePath
    Write-FixtureFile "$targetRelative/old-page-bundle.js" 'keep for existing browser tabs' | Out-Null
    $updated = & (Join-Path $PSScriptRoot '../update-ui-from-drive.ps1') -SourceRoot $fixtureRoot -DriveFolder 'fixture-folder-id' -IfNewer -LiveUpdate -PassThru
    Assert-True ($updated.Status -eq 'Updated' -and $global:SpaOfflineTestState.DownloadCalls -eq 2) 'Missing version state installs latest once'
    Assert-True (Test-Path -LiteralPath (Join-Path $installed 'old-page-bundle.js')) 'Live update retains assets for existing browser tabs'
    Remove-Item -LiteralPath (Join-Path $installed 'main.123.js')
    $updated = & (Join-Path $PSScriptRoot '../update-ui-from-drive.ps1') -SourceRoot $fixtureRoot -DriveFolder 'fixture-folder-id' -IfNewer -LiveUpdate -PassThru
    Assert-True ($updated.Status -eq 'Updated' -and $global:SpaOfflineTestState.DownloadCalls -eq 3) 'Reinstall when UI is invalid despite matching version state'
    $global:SpaOfflineTestState.EmptyDrive = $true
    $noPackage = & (Join-Path $PSScriptRoot '../update-ui-from-drive.ps1') -SourceRoot $fixtureRoot -DriveFolder 'fixture-folder-id' -IfNewer -LiveUpdate -PassThru
    Assert-True ($noPackage.Status -eq 'NoPackage' -and $global:SpaOfflineTestState.DownloadCalls -eq 3) 'Empty Drive returns NoPackage without download'
    $global:SpaOfflineTestState.EmptyDrive = $false

    Set-Content -LiteralPath $statePath -Value '{invalid-json' -Encoding UTF8
    $updated = & (Join-Path $PSScriptRoot '../update-ui-from-drive.ps1') -SourceRoot $fixtureRoot -DriveFolder 'fixture-folder-id' -IfNewer -LiveUpdate -PassThru
    Assert-True ($updated.Status -eq 'Updated' -and $global:SpaOfflineTestState.DownloadCalls -eq 4) 'Corrupt local state triggers reinstall and writes valid state'
    $global:SpaOfflineTestState.Metadata.name = 'gv-portal-ui-20261018T000000001Z.zip'
    $updated = & (Join-Path $PSScriptRoot '../update-ui-from-drive.ps1') -SourceRoot $fixtureRoot -DriveFolder 'fixture-folder-id' -IfNewer -LiveUpdate -PassThru
    Assert-True ($updated.Status -eq 'Updated' -and $global:SpaOfflineTestState.DownloadCalls -eq 5) 'A newer timestamp installs and advances installed version'
    $stateHash = (Get-FileHash -LiteralPath $statePath).Hash
    $indexHash = (Get-FileHash -LiteralPath (Join-Path $installed 'index.html')).Hash
    $global:SpaOfflineTestState.Metadata.name = 'gv-portal-ui-20261019T000000001Z.zip'
    $global:SpaOfflineTestState.CorruptDownload = $true
    Assert-Throws { & (Join-Path $PSScriptRoot '../update-ui-from-drive.ps1') -SourceRoot $fixtureRoot -DriveFolder 'fixture-folder-id' -IfNewer -LiveUpdate -PassThru } 'checksum' 'Reject a corrupt new download before installing or recording its version'
    Assert-True ((Get-FileHash -LiteralPath $statePath).Hash -eq $stateHash -and
        (Get-FileHash -LiteralPath (Join-Path $installed 'index.html')).Hash -eq $indexHash) 'Failed download keeps installed version marker and current shell intact'
    $global:SpaOfflineTestState.CorruptDownload = $false

    $before = @(Get-ChildItem -LiteralPath $fixtureRoot -Recurse -File).Count
    & (Join-Path $PSScriptRoot '../build-ui-package.ps1') -SourceRoot $fixtureRoot -WhatIf
    & (Join-Path $PSScriptRoot '../update-ui-from-drive.ps1') -SourceRoot $fixtureRoot -WhatIf
    Assert-True (@(Get-ChildItem -LiteralPath $fixtureRoot -Recurse -File).Count -eq $before -and $global:SpaOfflineTestState.DownloadCalls -eq 6) 'Both WhatIf entry points perform no write, build or HTTP request'

    Write-Host "SUCCESS: $($global:SpaOfflineTestState.Passed) offline assertions passed."
}
finally {
    Remove-SpaWorkDirectory $repoRoot $fixtureRoot
    Remove-Variable -Name SpaOfflineTestState -Scope Global
}
