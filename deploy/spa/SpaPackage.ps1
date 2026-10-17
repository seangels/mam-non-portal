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
}

function Get-SpaLatestPackage {
    param(
        [object[]]$Files,
        [string]$PackageRegex = $script:SpaPackageRegex,
        [string]$TimestampFormat = $script:SpaTimestampFormat
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
    if ($ordered.Count -eq 0) { throw 'Khong co goi UI khop regex va timestamp hop le trong folder Drive.' }
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
    param([string]$SourceRoot, [string]$BuildDirectory)
    $BuildDirectory = Get-SpaSafePath $SourceRoot $BuildDirectory
    Assert-SpaBuild $BuildDirectory
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
