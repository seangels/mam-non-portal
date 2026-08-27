# ============================================================
# UTF-8 CONSOLE
# ============================================================
# Hỗ trợ hiển thị tiếng Việt tốt hơn trên Windows PowerShell 5.1
chcp 65001 > $null

$Utf8 = New-Object System.Text.UTF8Encoding($false)

[Console]::InputEncoding  = $Utf8
[Console]::OutputEncoding = $Utf8
$OutputEncoding           = $Utf8

$ErrorActionPreference = "Stop"

# ============================================================
# CONFIG
# ============================================================

$ConfigFile = Join-Path $PSScriptRoot "copyConfig.json"

if (-not (Test-Path -LiteralPath $ConfigFile -PathType Leaf)) {
    throw "Không tìm thấy file config: $ConfigFile"
}

try {
    $Config = Get-Content `
        -LiteralPath $ConfigFile `
        -Raw `
        -Encoding UTF8 |
        ConvertFrom-Json
}
catch {
    throw "Không đọc được copyConfig.json. JSON không hợp lệ.`n$($_.Exception.Message)"
}

# ============================================================
# FUNCTIONS
# ============================================================

function Resolve-ConfigPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $Path = $Path.Trim()

    # Absolute path
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    # Relative path luôn tính từ thư mục chứa script
    return [System.IO.Path]::GetFullPath(
        (Join-Path $PSScriptRoot $Path)
    )
}


function Normalize-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return $Path.Replace('\', '/').TrimStart('/')
}


function Test-Excluded {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath,

        [array]$ExcludePatterns
    )

    if ($null -eq $ExcludePatterns -or $ExcludePatterns.Count -eq 0) {
        return $false
    }

    $RelativePath = Normalize-RelativePath $RelativePath

    foreach ($PatternValue in $ExcludePatterns) {

        if ($null -eq $PatternValue) {
            continue
        }

        $Pattern = $PatternValue.ToString().Trim()

        if ([string]::IsNullOrWhiteSpace($Pattern)) {
            continue
        }

        $Pattern = $Pattern.Replace('\', '/')
        $Pattern = $Pattern.TrimStart('/').TrimEnd('/')

        # ====================================================
        # 1. Exact path
        #
        # Ví dụ:
        # src/config.json
        # ====================================================
        if ($RelativePath -ieq $Pattern) {
            return $true
        }

        # ====================================================
        # 2. Directory/path + tất cả file bên trong
        #
        # Ví dụ:
        # src/temp
        #
        # Match:
        # src/temp/a.txt
        # src/temp/cache/b.txt
        # ====================================================
        if ($RelativePath.StartsWith(
            "$Pattern/",
            [System.StringComparison]::OrdinalIgnoreCase
        )) {
            return $true
        }

        # ====================================================
        # 3. Wildcard
        #
        # Ví dụ:
        # *.log
        # *.tmp
        # src/*.json
        # ====================================================
        if ($RelativePath -like $Pattern) {
            return $true
        }

        # ====================================================
        # 4. Folder/file name ở bất kỳ cấp nào
        #
        # Ví dụ:
        # bin
        #
        # Match:
        # bin/test.dll
        # src/bin/test.dll
        # apps/api/bin/test.dll
        #
        # node_modules
        # obj
        # .git
        # ====================================================
        if (-not $Pattern.Contains('/')) {

            $Segments = $RelativePath -split '/'

            foreach ($Segment in $Segments) {

                if ($Segment -ieq $Pattern) {
                    return $true
                }
            }
        }
    }

    return $false
}


function Get-BooleanSetting {
    param(
        $Value,
        [bool]$DefaultValue
    )

    if ($null -eq $Value) {
        return $DefaultValue
    }

    return [bool]$Value
}


function Test-PathInside {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Parent,

        [Parameter(Mandatory = $true)]
        [string]$Child
    )

    $ParentFull = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\', '/')
    $ChildFull  = [System.IO.Path]::GetFullPath($Child).TrimEnd('\', '/')

    if ($ParentFull -ieq $ChildFull) {
        return $true
    }

    $ParentWithSlash = $ParentFull + [System.IO.Path]::DirectorySeparatorChar

    return $ChildFull.StartsWith(
        $ParentWithSlash,
        [System.StringComparison]::OrdinalIgnoreCase
    )
}


function Invoke-CopyJob {
    param(
        [Parameter(Mandatory = $true)]
        $Job,

        [Parameter(Mandatory = $true)]
        [int]$JobIndex,

        [bool]$ShowSkippedFiles,

        [bool]$ShowCopiedFiles
    )

    # ========================================================
    # JOB NAME
    # ========================================================

    $JobName = $Job.name

    if ([string]::IsNullOrWhiteSpace($JobName)) {
        $JobName = "Job #$JobIndex"
    }

    # ========================================================
    # ENABLED
    # ========================================================

    $Enabled = Get-BooleanSetting `
        -Value $Job.enabled `
        -DefaultValue $true

    if (-not $Enabled) {

        Write-Host ""
        Write-Host "BỎ QUA JOB: $JobName" -ForegroundColor DarkGray

        return @{
            Enabled  = $false
            Success  = $true
            JobName  = $JobName
        }
    }

    # ========================================================
    # VALIDATE
    # ========================================================

    if ([string]::IsNullOrWhiteSpace($Job.source)) {
        throw "Job '$JobName' thiếu thuộc tính 'source'."
    }

    if ([string]::IsNullOrWhiteSpace($Job.destination)) {
        throw "Job '$JobName' thiếu thuộc tính 'destination'."
    }

    # ========================================================
    # RESOLVE PATH
    # ========================================================

    $Source = Resolve-ConfigPath $Job.source
    $Destination = Resolve-ConfigPath $Job.destination

    if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
        throw "Source không tồn tại: $Source"
    }

    $Source = (Resolve-Path -LiteralPath $Source).Path.TrimEnd('\', '/')

    $Destination = [System.IO.Path]::GetFullPath(
        $Destination
    ).TrimEnd('\', '/')

    # ========================================================
    # SAFETY CHECK
    # ========================================================

    if ($Source -ieq $Destination) {
        throw @"
Source và Destination không được giống nhau.

Source      : $Source
Destination : $Destination
"@
    }

    # Không cho destination nằm trong source
    #
    # Ví dụ không hợp lệ:
    # source      = C:\project
    # destination = C:\project\backup
    #
    # Nếu không kiểm tra có thể sinh:
    # backup\backup\backup...
    if (Test-PathInside -Parent $Source -Child $Destination) {

        throw @"
Destination không được nằm bên trong Source.

Source      : $Source
Destination : $Destination
"@
    }

    # ========================================================
    # JOB OPTIONS
    # ========================================================

    $CleanDestination = Get-BooleanSetting `
        -Value $Job.clean_destination `
        -DefaultValue $false

    $Overwrite = Get-BooleanSetting `
        -Value $Job.overwrite `
        -DefaultValue $true

    # ========================================================
    # EXCLUDE PATHS
    # ========================================================

    $ExcludePatterns = @()

    if ($null -ne $Job.exclude_paths) {

        $ExcludePatterns = @(
            $Job.exclude_paths |
                Where-Object { $null -ne $_ } |
                ForEach-Object {
                    $_.ToString().Trim()
                } |
                Where-Object {
                    -not [string]::IsNullOrWhiteSpace($_)
                }
        )
    }

    # ========================================================
    # JOB INFO
    # ========================================================

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Yellow
    Write-Host "JOB         : $JobName" -ForegroundColor Yellow
    Write-Host "Source      : $Source" -ForegroundColor Cyan
    Write-Host "Destination : $Destination" -ForegroundColor Cyan
    Write-Host "Clean       : $CleanDestination" -ForegroundColor Cyan
    Write-Host "Overwrite   : $Overwrite" -ForegroundColor Cyan
    Write-Host "Exclude     : $($ExcludePatterns.Count) pattern(s)" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Yellow

    # ========================================================
    # CLEAN DESTINATION
    # ========================================================

    if (
        $CleanDestination -and
        (Test-Path -LiteralPath $Destination -PathType Container)
    ) {

        # Extra safety:
        # Nếu source nằm bên trong destination,
        # clean destination có thể xóa source.
        if (Test-PathInside -Parent $Destination -Child $Source) {

            throw @"
Không thể xóa Destination vì Source đang nằm bên trong Destination.

Source      : $Source
Destination : $Destination
"@
        }

        Write-Host ""
        Write-Host "XÓA NỘI DUNG DESTINATION: $Destination" -ForegroundColor Yellow

        Get-ChildItem `
            -LiteralPath $Destination `
            -Force `
            -ErrorAction Stop |
            Remove-Item `
                -Recurse `
                -Force `
                -ErrorAction Stop
    }

    # ========================================================
    # CREATE DESTINATION
    # ========================================================

    if (-not (Test-Path -LiteralPath $Destination -PathType Container)) {

        Write-Host ""
        Write-Host "TẠO DESTINATION: $Destination" -ForegroundColor Cyan

        New-Item `
            -ItemType Directory `
            -Path $Destination `
            -Force `
            -ErrorAction Stop |
            Out-Null
    }

    # ========================================================
    # COUNTERS
    # ========================================================

    $CopiedCount   = 0
    $ExcludedCount = 0
    $ExistingCount = 0
    $ErrorCount    = 0

    # ========================================================
    # READ SOURCE FILES
    # ========================================================

    $Files = Get-ChildItem `
        -LiteralPath $Source `
        -Recurse `
        -File `
        -Force `
        -ErrorAction Stop

    # ========================================================
    # COPY FILES
    # ========================================================

    foreach ($File in $Files) {

        try {

            $RelativePath = $File.FullName.Substring($Source.Length).TrimStart('\', '/')

            # =================================================
            # EXCLUDE
            # =================================================

            if (
                Test-Excluded `
                    -RelativePath $RelativePath `
                    -ExcludePatterns $ExcludePatterns
            ) {

                $ExcludedCount++

                if ($ShowSkippedFiles) {
                    Write-Host "BỎ QUA : $RelativePath" -ForegroundColor DarkGray
                }

                continue
            }

            # =================================================
            # TARGET PATH
            # =================================================

            $TargetFile = Join-Path $Destination $RelativePath
            $TargetDir  = Split-Path $TargetFile -Parent

            # =================================================
            # CREATE TARGET DIRECTORY
            # =================================================

            if (-not (Test-Path -LiteralPath $TargetDir -PathType Container)) {

                New-Item `
                    -ItemType Directory `
                    -Path $TargetDir `
                    -Force `
                    -ErrorAction Stop |
                    Out-Null
            }

            # =================================================
            # FILE EXISTS + OVERWRITE FALSE
            # =================================================

            if (
                (Test-Path -LiteralPath $TargetFile -PathType Leaf) -and
                -not $Overwrite
            ) {

                $ExistingCount++

                if ($ShowSkippedFiles) {
                    Write-Host "ĐÃ CÓ   : $RelativePath" -ForegroundColor DarkYellow
                }

                continue
            }

            # =================================================
            # COPY FILE
            # =================================================

            if ($Overwrite) {

                Copy-Item `
                    -LiteralPath $File.FullName `
                    -Destination $TargetFile `
                    -Force `
                    -ErrorAction Stop
            }
            else {

                Copy-Item `
                    -LiteralPath $File.FullName `
                    -Destination $TargetFile `
                    -ErrorAction Stop
            }

            $CopiedCount++

            if ($ShowCopiedFiles) {
                Write-Host "COPY    : $RelativePath" -ForegroundColor Green
            }
        }
        catch {

            $ErrorCount++

            Write-Host ""
            Write-Host "LỖI FILE: $($File.FullName)" -ForegroundColor Red
            Write-Host "         $($_.Exception.Message)" -ForegroundColor Red

            throw
        }
    }

    # ========================================================
    # JOB RESULT
    # ========================================================

    Write-Host ""
    Write-Host "------------------------------------------------------------" -ForegroundColor DarkGray
    Write-Host "HOÀN TẤT : $JobName" -ForegroundColor Green
    Write-Host "Đã copy  : $CopiedCount" -ForegroundColor Green
    Write-Host "Đã loại  : $ExcludedCount" -ForegroundColor DarkGray
    Write-Host "Đã tồn tại: $ExistingCount" -ForegroundColor DarkYellow

    if ($ErrorCount -eq 0) {
        Write-Host "Lỗi       : $ErrorCount" -ForegroundColor Green
    }
    else {
        Write-Host "Lỗi       : $ErrorCount" -ForegroundColor Red
    }

    Write-Host "------------------------------------------------------------" -ForegroundColor DarkGray

    return @{
        Enabled       = $true
        Success       = $true
        JobName       = $JobName
        CopiedCount   = $CopiedCount
        ExcludedCount = $ExcludedCount
        ExistingCount = $ExistingCount
        ErrorCount    = $ErrorCount
    }
}

# ============================================================
# GLOBAL SETTINGS
# ============================================================

$ShowSkippedFiles = $false
$ShowCopiedFiles  = $true
$StopOnError      = $true

if ($null -ne $Config.settings) {

    $ShowSkippedFiles = Get-BooleanSetting `
        -Value $Config.settings.show_skipped_files `
        -DefaultValue $false

    $ShowCopiedFiles = Get-BooleanSetting `
        -Value $Config.settings.show_copied_files `
        -DefaultValue $true

    $StopOnError = Get-BooleanSetting `
        -Value $Config.settings.stop_on_error `
        -DefaultValue $true
}

# ============================================================
# VALIDATE JOBS
# ============================================================

if ($null -eq $Config.jobs) {
    throw "Thiếu thuộc tính 'jobs' trong copyConfig.json."
}

$Jobs = @($Config.jobs)

if ($Jobs.Count -eq 0) {
    throw "Không có job nào trong copyConfig.json."
}

# ============================================================
# START
# ============================================================

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "COPY FILE TOOL" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Script Root : $PSScriptRoot" -ForegroundColor DarkGray
Write-Host "Config      : $ConfigFile" -ForegroundColor DarkGray
Write-Host "Số Jobs     : $($Jobs.Count)" -ForegroundColor DarkGray
Write-Host "Stop On Error: $StopOnError" -ForegroundColor DarkGray

$TotalSuccess  = 0
$TotalFailed   = 0
$TotalDisabled = 0

# ============================================================
# RUN JOBS
# ============================================================

for ($i = 0; $i -lt $Jobs.Count; $i++) {

    $Job = $Jobs[$i]
    $JobIndex = $i + 1

    try {

        $Result = Invoke-CopyJob `
            -Job $Job `
            -JobIndex $JobIndex `
            -ShowSkippedFiles $ShowSkippedFiles `
            -ShowCopiedFiles $ShowCopiedFiles

        if ($Result.Enabled -eq $false) {
            $TotalDisabled++
        }
        else {
            $TotalSuccess++
        }
    }
    catch {

        $TotalFailed++

        $Name = $Job.name

        if ([string]::IsNullOrWhiteSpace($Name)) {
            $Name = "Job #$JobIndex"
        }

        Write-Host ""
        Write-Host "============================================================" -ForegroundColor Red
        Write-Host "JOB THẤT BẠI: $Name" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        Write-Host "============================================================" -ForegroundColor Red

        if ($StopOnError) {

            Write-Host ""
            Write-Host "Dừng chương trình vì stop_on_error = true." -ForegroundColor Red

            exit 1
        }
    }
}

# ============================================================
# FINISH
# ============================================================

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "HOÀN TẤT TẤT CẢ JOB" -ForegroundColor Green
Write-Host "Thành công : $TotalSuccess" -ForegroundColor Green
Write-Host "Tắt        : $TotalDisabled" -ForegroundColor DarkGray

if ($TotalFailed -eq 0) {
    Write-Host "Thất bại   : $TotalFailed" -ForegroundColor Green
}
else {
    Write-Host "Thất bại   : $TotalFailed" -ForegroundColor Red
}

Write-Host "============================================================" -ForegroundColor Cyan

if ($TotalFailed -gt 0) {
    exit 1
}

exit 0
