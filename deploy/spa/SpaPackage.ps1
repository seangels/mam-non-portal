#Requires -Version 5.1
# Filesystem/package helpers only; no build, network or installation on import.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$script:SpaPackageRegex = '\Agv-portal-ui-(?<timestamp>\d{8}T\d{9}Z)\.zip\z'
$script:SpaTimestampFormat = "yyyyMMdd'T'HHmmssfff'Z'"

function Get-SpaSafePath {
    param([string]$Root, [string]$Path)
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    if (-not [IO.Path]::IsPathRooted($Path)) { $Path = Join-Path $rootPath $Path }
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Duong dan phai nam trong source root: $fullPath"
    }
    # Check existing ancestors before any recursive delete/move/copy.
    $current = $fullPath
    while ($current.Length -ge $rootPath.Length) {
        if (Test-Path -LiteralPath $current) {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Khong thao tac qua junction/symlink: $current"
            }
        }
        if ($current -eq $rootPath) { break }
        $current = Split-Path -Parent $current
    }
    return $fullPath
}

function Get-SpaSourceRoot {
    param([string]$SourceRoot)
    $root = [IO.Path]::GetFullPath($SourceRoot)
    $project = Get-SpaSafePath $root 'api/src/AdminPortal.Api/AdminPortal.Api.csproj'
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { throw "Khong dung source root: $root" }
    return $root
}

function Remove-SpaWorkDirectory {
    param([string]$SourceRoot, [string]$Path)
    $safePath = Get-SpaSafePath $SourceRoot $Path
    if (Test-Path -LiteralPath $safePath) { Remove-Item -LiteralPath $safePath -Recurse -Force }
}

function Get-SpaBuildFiles {
    param([string]$BuildDirectory)
    if (-not (Test-Path -LiteralPath $BuildDirectory -PathType Container)) { throw "Thieu build: $BuildDirectory" }
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($BuildDirectory)
    while ($pending.Count -gt 0) {
        foreach ($item in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Build chua symlink/junction: $($item.FullName)" }
            if ($item.PSIsContainer) { $pending.Push($item.FullName) }
            elseif ($item.Name -ne '.gitkeep') {
                if ($item.Name -match '(?i)\A(appsettings.*\.json|client_secret.*\.json|.*\.token\.json|\.env.*)\z') {
                    throw "Build chua file cau hinh/credential khong duoc dong goi: $($item.Name)"
                }
                $item
            }
        }
    }
}

function Assert-SpaBuild {
    param([string]$BuildDirectory)
    $files = @(Get-SpaBuildFiles $BuildDirectory)
    $index = Join-Path $BuildDirectory 'index.html'
    if (-not (Test-Path -LiteralPath $index -PathType Leaf) -or (Get-Item -LiteralPath $index).Length -eq 0) {
        throw "Build thieu index.html hop le: $BuildDirectory"
    }
    if (@($files | Where-Object { $_.Name -like 'main*.js' -and $_.Length -gt 0 }).Count -eq 0) {
        throw "Build thieu main*.js: $BuildDirectory"
    }
    # Live updates retain old bundles; validate the assets used by the current shell.
    $html = Get-Content -LiteralPath $index -Raw -Encoding UTF8
    $references = [regex]::Matches($html, '(?i)(?:src|href)\s*=\s*["''](?<asset>[^"'']+\.(?:js|css)(?:[?#][^"'']*)?)["'']')
    foreach ($reference in $references) {
        $asset = [Net.WebUtility]::HtmlDecode($reference.Groups['asset'].Value)
        if ($asset -match '\A(?:[a-z][a-z0-9+.-]*:|//)') { continue }
        $relative = [uri]::UnescapeDataString(($asset -replace '[?#].*\z', '')).TrimStart('/')
        $assetPath = Get-SpaSafePath $BuildDirectory $relative
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf) -or (Get-Item -LiteralPath $assetPath).Length -eq 0) {
            throw "Build thieu asset duoc index.html tham chieu: $relative"
        }
    }
}

function Get-SpaLatestPackage {
    param(
        [object[]]$Files,
        [string]$PackageRegex = $script:SpaPackageRegex,
        [string]$TimestampFormat = $script:SpaTimestampFormat,
        [switch]$AllowNone
    )
    $pattern = [regex]::new($PackageRegex, [Text.RegularExpressions.RegexOptions]::CultureInvariant, [TimeSpan]::FromSeconds(2))
    if ($pattern.GetGroupNames() -notcontains 'timestamp') { throw 'PackageRegex phai co named group (?<timestamp>...).' }
    $candidates = @(
        foreach ($file in $Files) {
            if (-not $file.PSObject.Properties['name']) { continue }
            $name = [string]$file.name
            if ($name -match '[/\\]' -or [IO.Path]::GetFileName($name) -cne $name) { continue }
            $match = $pattern.Match($name)
            if (-not $match.Success -or $match.Length -ne $name.Length) { continue }
            $stamp = [datetime]::MinValue
            $styles = [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal
            if ([datetime]::TryParseExact($match.Groups['timestamp'].Value, $TimestampFormat,
                    [Globalization.CultureInfo]::InvariantCulture, $styles, [ref]$stamp)) {
                [pscustomobject]@{ File = $file; Timestamp = $stamp }
            }
        }
    )
    $ordered = @($candidates | Sort-Object Timestamp -Descending)
    if ($ordered.Count -eq 0) {
        if ($AllowNone) { return $null }
        throw 'Khong co goi UI khop regex va timestamp hop le trong folder Drive.'
    }
    if ($ordered.Count -gt 1 -and $ordered[0].Timestamp -eq $ordered[1].Timestamp) {
        throw 'Co nhieu goi cung timestamp moi nhat; can giu lai dung mot goi tren Drive.'
    }
    return $ordered[0]
}

function New-SpaUiPackage {
    param([string]$SourceRoot, [string]$BuildDirectory, [string]$PackagePath)
    $BuildDirectory = Get-SpaSafePath $SourceRoot $BuildDirectory
    $PackagePath = Get-SpaSafePath $SourceRoot $PackagePath
    Assert-SpaBuild $BuildDirectory
    if (Test-Path -LiteralPath $PackagePath) { throw "Goi da ton tai: $PackagePath" }
    New-Item -ItemType Directory -Path (Split-Path -Parent $PackagePath) -Force | Out-Null
    $partialPath = Get-SpaSafePath $SourceRoot ($PackagePath + '.partial')
    $archive = $null
    try {
        $archive = [IO.Compression.ZipFile]::Open($partialPath, [IO.Compression.ZipArchiveMode]::Create)
        foreach ($file in Get-SpaBuildFiles $BuildDirectory) {
            $relative = $file.FullName.Substring($BuildDirectory.TrimEnd('\', '/').Length + 1).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName,
                "ClientApp/build/$relative", [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
        $archive.Dispose()
        $archive = $null
        Move-Item -LiteralPath $partialPath -Destination $PackagePath
        $checksum = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $checksumPath = Get-SpaSafePath $SourceRoot ($PackagePath + '.sha256')
        "$checksum  $([IO.Path]::GetFileName($PackagePath))" | Set-Content -LiteralPath $checksumPath -Encoding ASCII
        return $PackagePath
    }
    finally {
        if ($archive) { $archive.Dispose() }
        if (Test-Path -LiteralPath $partialPath) { Remove-Item -LiteralPath $partialPath -Force }
    }
}

function Expand-SpaUiPackage {
    param([string]$SourceRoot, [string]$PackagePath, [string]$Destination)
    $Destination = Get-SpaSafePath $SourceRoot $Destination
    if (Test-Path -LiteralPath $Destination) { throw "Thu muc giai nen da ton tai: $Destination" }
    $archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $totalBytes = 0L
        if ($archive.Entries.Count -gt 50000) { throw 'ZIP co qua nhieu entry.' }
        # Validate the whole archive before writing any extracted files.
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName
            if ($name -ceq 'ClientApp/' -or $name -ceq 'ClientApp/build/') { continue }
            if (-not $name.StartsWith('ClientApp/build/', [StringComparison]::Ordinal) -or $name.Contains('\')) {
                throw "ZIP co entry ngoai ClientApp/build: $name"
            }
            $relative = $name.Substring('ClientApp/build/'.Length).TrimEnd('/')
            foreach ($segment in $relative.Split('/')) {
                if (-not $segment -or $segment -eq '.' -or $segment -eq '..' -or
                    $segment -match '[<>:"|?*\x00-\x1f]' -or $segment -match '[. ]\z') {
                    throw "ZIP co duong dan khong hop le: $name"
                }
            }
            if (-not $seen.Add($relative)) { throw "ZIP co entry trung ten: $name" }
            if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw "ZIP co symlink: $name" }
            $totalBytes += $entry.Length
            if ($totalBytes -gt 1GB) { throw 'ZIP vuot gioi han 1 GB sau giai nen.' }
            Get-SpaSafePath $Destination (Join-Path $Destination $relative) | Out-Null
        }
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            $relative = $entry.FullName.Substring('ClientApp/build/'.Length)
            $target = Get-SpaSafePath $Destination (Join-Path $Destination $relative)
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $false)
        }
        Assert-SpaBuild $Destination
        return $Destination
    }
    catch {
        Remove-SpaWorkDirectory $SourceRoot $Destination
        throw
    }
    finally { $archive.Dispose() }
}

function Install-SpaClientBuild {
    param([string]$SourceRoot, [string]$BuildDirectory, [switch]$LiveUpdate)
    $BuildDirectory = Get-SpaSafePath $SourceRoot $BuildDirectory
    Assert-SpaBuild $BuildDirectory
    if ($LiveUpdate) { return Install-SpaLiveBuild $SourceRoot $BuildDirectory }
    $target = Get-SpaSafePath $SourceRoot 'api/src/AdminPortal.Api/ClientApp/build'
    $id = [datetime]::UtcNow.ToString($script:SpaTimestampFormat) + '-' + [Guid]::NewGuid().ToString('N')
    $stage = Get-SpaSafePath $SourceRoot "artifacts/spa/stage-$id"
    $backup = Get-SpaSafePath $SourceRoot "artifacts/spa/backups/build-$id"
    $movedOldBuild = $false
    try {
        New-Item -ItemType Directory -Path $stage -Force | Out-Null
        foreach ($file in Get-SpaBuildFiles $BuildDirectory) {
            $relative = $file.FullName.Substring($BuildDirectory.TrimEnd('\', '/').Length + 1)
            $copyTarget = Get-SpaSafePath $SourceRoot (Join-Path $stage $relative)
            New-Item -ItemType Directory -Path (Split-Path -Parent $copyTarget) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $copyTarget
        }
        Assert-SpaBuild $stage
        $gitkeep = Get-SpaSafePath $SourceRoot (Join-Path $target '.gitkeep')
        if (Test-Path -LiteralPath $gitkeep -PathType Leaf) {
            Copy-Item -LiteralPath $gitkeep -Destination (Join-Path $stage '.gitkeep')
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
        if (Test-Path -LiteralPath $target) {
            Get-SpaSafePath $SourceRoot $target | Out-Null
            Get-SpaSafePath $SourceRoot $backup | Out-Null
            Move-Item -LiteralPath $target -Destination $backup
            $movedOldBuild = $true
        }
        try {
            Get-SpaSafePath $SourceRoot $stage | Out-Null
            Get-SpaSafePath $SourceRoot $target | Out-Null
            Move-Item -LiteralPath $stage -Destination $target
        }
        catch {
            if ($movedOldBuild) {
                Get-SpaSafePath $SourceRoot $backup | Out-Null
                Get-SpaSafePath $SourceRoot $target | Out-Null
                Move-Item -LiteralPath $backup -Destination $target
            }
            throw
        }
        if ($movedOldBuild) { Write-Host "Build truoc duoc giu tai: $backup" }
        return $target
    }
    finally { Remove-SpaWorkDirectory $SourceRoot $stage }
}

function Test-SpaCurrentBuild {
    param([string]$SourceRoot)
    try {
        Assert-SpaBuild (Get-SpaSafePath $SourceRoot 'api/src/AdminPortal.Api/ClientApp/build')
        return $true
    }
    catch { return $false }
}

function Get-SpaInstalledPackage {
    param([string]$SourceRoot)
    $path = Get-SpaSafePath $SourceRoot 'artifacts/spa/installed-ui.json'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    try {
        $state = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
        $stamp = [datetime]::MinValue
        if (-not $state.PSObject.Properties['timestampUtc'] -or -not $state.PSObject.Properties['packageName'] -or
            -not [datetime]::TryParseExact([string]$state.timestampUtc, 'o', [Globalization.CultureInfo]::InvariantCulture,
                [Globalization.DateTimeStyles]::RoundtripKind, [ref]$stamp) -or $stamp.Kind -ne [DateTimeKind]::Utc) {
            return $null
        }
        return [pscustomobject]@{ Timestamp = $stamp; PackageName = [string]$state.packageName }
    }
    catch { return $null }
}

function Set-SpaInstalledPackage {
    param([string]$SourceRoot, $Package)
    $path = Get-SpaSafePath $SourceRoot 'artifacts/spa/installed-ui.json'
    $temp = Get-SpaSafePath $SourceRoot ($path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
    try {
        @{
            packageName = $Package.File.name
            timestampUtc = $Package.Timestamp.ToUniversalTime().ToString('o')
            sourceId = $Package.File.id
            md5Checksum = $Package.File.md5Checksum
        } | ConvertTo-Json | Set-Content -LiteralPath $temp -Encoding UTF8
        if (Test-Path -LiteralPath $path) { [IO.File]::Replace($temp, $path, [System.Management.Automation.Language.NullString]::Value) }
        else { Move-Item -LiteralPath $temp -Destination $path }
    }
    finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force } }
}

function Set-SpaLiveFile {
    param([string]$SourceRoot, [string]$Source, [string]$Target)
    $Target = Get-SpaSafePath $SourceRoot $Target
    $temp = Get-SpaSafePath $SourceRoot ($Target + '.spa-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    New-Item -ItemType Directory -Path (Split-Path -Parent $Target) -Force | Out-Null
    try {
        Copy-Item -LiteralPath $Source -Destination $temp
        if (Test-Path -LiteralPath $Target -PathType Leaf) { [IO.File]::Replace($temp, $Target, [System.Management.Automation.Language.NullString]::Value) }
        else { Move-Item -LiteralPath $temp -Destination $Target }
    }
    finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force } }
}

function Install-SpaLiveBuild {
    param([string]$SourceRoot, [string]$BuildDirectory)
    $target = Get-SpaSafePath $SourceRoot 'api/src/AdminPortal.Api/ClientApp/build'
    $backup = Get-SpaSafePath $SourceRoot ('artifacts/spa/backups/live-' + [datetime]::UtcNow.ToString($script:SpaTimestampFormat) + '-' + [Guid]::NewGuid().ToString('N'))
    $indexPath = Join-Path $BuildDirectory 'index.html'
    $files = @(Get-SpaBuildFiles $BuildDirectory | Sort-Object { if ($_.FullName -ieq $indexPath) { 1 } else { 0 } })
    $changes = @(
        foreach ($file in $files) {
            $relative = $file.FullName.Substring($BuildDirectory.TrimEnd('\', '/').Length + 1)
            $destination = Get-SpaSafePath $SourceRoot (Join-Path $target $relative)
            $saved = Get-SpaSafePath $SourceRoot (Join-Path $backup $relative)
            $existed = Test-Path -LiteralPath $destination -PathType Leaf
            if ($existed) {
                New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force | Out-Null
                Copy-Item -LiteralPath $destination -Destination $saved
            }
            [pscustomobject]@{ Source = $file.FullName; Target = $destination; Backup = $saved; Existed = $existed }
        }
    )
    $attempted = [Collections.Generic.List[object]]::new()
    try {
        foreach ($change in $changes) {
            $attempted.Add($change)
            Set-SpaLiveFile $SourceRoot $change.Source $change.Target
        }
    }
    catch {
        $installError = $_
        try {
            for ($i = $attempted.Count - 1; $i -ge 0; $i--) {
                $change = $attempted[$i]
                if ($change.Existed) { Set-SpaLiveFile $SourceRoot $change.Backup $change.Target }
                elseif (Test-Path -LiteralPath $change.Target -PathType Leaf) {
                    Get-SpaSafePath $SourceRoot $change.Target | Out-Null
                    Remove-Item -LiteralPath $change.Target -Force
                }
            }
        }
        catch { throw "Live update va rollback deu loi. Backup: $backup. Install: $($installError.Exception.Message). Rollback: $($_.Exception.Message)" }
        throw $installError
    }
    Write-Host "Live update OK; giu asset cu va backup tai: $backup"
    return $target
}
