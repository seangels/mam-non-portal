# ============================================================
# deploy-iis.change.ps1
# ============================================================
# Đặt script này (kèm file config deploy-iis.change.json) trong
# MỘT THƯ MỤC BẤT KỲ — gọi là [folder-update]:
#
#   [folder-update]\
#       deploy-iis.change.ps1      <-- script này
#       deploy-iis.change.json     <-- file config
#
# Việc script thực hiện:
#   1. Tự set execution policy cho tiến trình hiện tại (Bypass).
#   2. Đọc deploy-iis.change.json để biết THƯ MỤC CHỨA PACKAGE
#      (packageDirectory) và mẫu tên (packagePattern).
#   3. Quét thư mục đó, lấy package có timestamp trong TÊN FILE
#      mới nhất (YYYYMMDD-HHMMSS), kiểm tra .sha256 nếu có.
#   4. Giải nén NỘI DUNG package vào [folder-update] (bỏ lớp thư
#      mục gốc bọc bên ngoài nếu có).
#   5. Gọi copy-files.ps1 vừa giải nén ra
#      ([folder-update]\deploy\iis\copy-files.ps1) và trả về đúng
#      mã thoát của nó.
#
# Cách chạy (Run as administrator):
#
#   Set-ExecutionPolicy -Scope Process Bypass
#   .\deploy-iis.change.ps1
# ============================================================

[CmdletBinding()]
param(
    # File config JSON. Mặc định deploy-iis.change.json cạnh script.
    [string]$ConfigFile
)

# ============================================================
# UTF-8 CONSOLE (hỗ trợ tiếng Việt trên Windows PowerShell 5.1)
# ============================================================
try { chcp 65001 > $null } catch { }

$Utf8 = New-Object System.Text.UTF8Encoding($false)
try {
    [Console]::InputEncoding  = $Utf8
    [Console]::OutputEncoding = $Utf8
    $OutputEncoding           = $Utf8
}
catch { }

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 3.0

# ============================================================
# HELPERS
# ============================================================
function Get-ConfigString {
    param($Object, [string]$Name, [string]$Default = $null)

    if ($null -ne $Object -and
        ($Object.PSObject.Properties.Name -contains $Name) -and
        $null -ne $Object.$Name -and
        -not [string]::IsNullOrWhiteSpace([string]$Object.$Name)) {

        return ([string]$Object.$Name).Trim()
    }

    return $Default
}

function Get-ConfigBool {
    param($Object, [string]$Name, [bool]$Default)

    if ($null -ne $Object -and
        ($Object.PSObject.Properties.Name -contains $Name) -and
        $null -ne $Object.$Name) {

        return [bool]$Object.$Name
    }

    return $Default
}

# ============================================================
# 1. SET EXECUTION POLICY CHO TIẾN TRÌNH HIỆN TẠI
# ============================================================
try {
    Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
    Write-Host "Execution policy (Process) = Bypass" -ForegroundColor DarkGray
}
catch {
    Write-Host "Không set được execution policy: $($_.Exception.Message)" -ForegroundColor Yellow
}

# ============================================================
# 2. ĐỌC CONFIG
# ============================================================
$UpdateFolder = [System.IO.Path]::GetFullPath($PSScriptRoot)

if ([string]::IsNullOrWhiteSpace($ConfigFile)) {
    $ConfigFile = Join-Path $UpdateFolder "deploy-iis.change.json"
}
elseif (-not [System.IO.Path]::IsPathRooted($ConfigFile)) {
    $ConfigFile = Join-Path $UpdateFolder $ConfigFile
}
$ConfigFile = [System.IO.Path]::GetFullPath($ConfigFile)

if (-not (Test-Path -LiteralPath $ConfigFile -PathType Leaf)) {
    throw "Không tìm thấy file config: $ConfigFile"
}

try {
    $Config = Get-Content -LiteralPath $ConfigFile -Raw -Encoding UTF8 | ConvertFrom-Json
}
catch {
    throw "Không đọc được $ConfigFile. JSON không hợp lệ.`n$($_.Exception.Message)"
}

$ConfigDir      = [System.IO.Path]::GetDirectoryName($ConfigFile)
$PackagePattern = Get-ConfigString $Config "packagePattern" "gv-portal-iis-*.zip"
$VerifyChecksum = Get-ConfigBool   $Config "verifyChecksum" $true
$CopyFilesRel   = Get-ConfigString $Config "copyFilesScript" "deploy\iis\copy-files.ps1"

$PackageDirectory = Get-ConfigString $Config "packageDirectory"
if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
    throw "Thiếu 'packageDirectory' trong $ConfigFile."
}
if (-not [System.IO.Path]::IsPathRooted($PackageDirectory)) {
    # Đường dẫn tương đối tính từ thư mục chứa file config.
    $PackageDirectory = Join-Path $ConfigDir $PackageDirectory
}
$PackageDirectory = [System.IO.Path]::GetFullPath($PackageDirectory)

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "DEPLOY IIS - CHANGE" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Folder update   : $UpdateFolder" -ForegroundColor DarkGray
Write-Host "Config          : $ConfigFile" -ForegroundColor DarkGray
Write-Host "Package dir     : $PackageDirectory" -ForegroundColor DarkGray
Write-Host "Package pattern : $PackagePattern" -ForegroundColor DarkGray

if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) {
    throw "Không tìm thấy thư mục package: $PackageDirectory"
}

# ============================================================
# 3. TÌM PACKAGE MỚI NHẤT THEO TIMESTAMP TRONG TÊN
# ============================================================
$TimestampRegex = '(?<ts>\d{8}-\d{6})'

$Package = Get-ChildItem -LiteralPath $PackageDirectory -Filter $PackagePattern -File |
    ForEach-Object {
        $match = [regex]::Match($_.Name, $TimestampRegex)
        if ($match.Success) {
            $parsed = [datetime]::MinValue
            $ok = [datetime]::TryParseExact(
                $match.Groups['ts'].Value,
                'yyyyMMdd-HHmmss',
                [System.Globalization.CultureInfo]::InvariantCulture,
                [System.Globalization.DateTimeStyles]::None,
                [ref]$parsed
            )
            if ($ok) {
                [pscustomobject]@{ File = $_; Timestamp = $parsed }
            }
        }
    } |
    Sort-Object Timestamp -Descending |
    Select-Object -First 1

if ($null -eq $Package) {
    throw "Không tìm thấy package nào khớp '$PackagePattern' có timestamp hợp lệ trong '$PackageDirectory'."
}

$ZipPath     = $Package.File.FullName
$PackageName = [System.IO.Path]::GetFileNameWithoutExtension($ZipPath)

Write-Host ""
Write-Host "Package mới nhất : $($Package.File.Name)" -ForegroundColor Green
Write-Host "Timestamp       : $($Package.Timestamp.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Green

# ============================================================
# 4. KIỂM TRA CHECKSUM (nếu bật và có file .sha256)
# ============================================================
$ChecksumPath = $ZipPath + ".sha256"

if (-not $VerifyChecksum) {
    Write-Host "Bỏ qua kiểm tra checksum (verifyChecksum = false)." -ForegroundColor Yellow
}
elseif (Test-Path -LiteralPath $ChecksumPath -PathType Leaf) {

    $expected = ((Get-Content -LiteralPath $ChecksumPath -Raw) -split '\s+' |
        Where-Object { $_ })[0]
    $actual = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash

    if ($expected -and ($actual -ieq $expected)) {
        Write-Host "Checksum SHA256 : OK" -ForegroundColor Green
    }
    else {
        throw @"
Checksum không khớp cho package: $ZipPath
Mong đợi : $expected
Thực tế  : $actual
"@
    }
}
else {
    Write-Host "Không có file .sha256, bỏ qua kiểm tra checksum." -ForegroundColor Yellow
}

# ============================================================
# 5. GIẢI NÉN NỘI DUNG PACKAGE VÀO [folder-update]
# ============================================================
$Staging = Join-Path $UpdateFolder (".extract-" + [Guid]::NewGuid().ToString("N"))

try {
    New-Item -ItemType Directory -Path $Staging -Force | Out-Null

    Write-Host ""
    Write-Host "Giải nén: $($Package.File.Name)" -ForegroundColor Cyan
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $Staging -Force

    # Nếu zip chỉ có đúng một thư mục gốc bọc ngoài -> lấy nội dung bên trong nó.
    $topLevel = @(Get-ChildItem -LiteralPath $Staging -Force)
    if ($topLevel.Count -eq 1 -and $topLevel[0].PSIsContainer) {
        $ContentRoot = $topLevel[0].FullName
    }
    else {
        $ContentRoot = $Staging
    }

    # Gỡ chặn (Zone.Identifier) trước khi copy.
    Get-ChildItem -LiteralPath $ContentRoot -Recurse -File -Force |
        Unblock-File -ErrorAction SilentlyContinue

    Write-Host "Chép nội dung vào: $UpdateFolder" -ForegroundColor Cyan
    Get-ChildItem -LiteralPath $ContentRoot -Force |
        Copy-Item -Destination $UpdateFolder -Recurse -Force
}
finally {
    if (Test-Path -LiteralPath $Staging) {
        Remove-Item -LiteralPath $Staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ============================================================
# 6. GỌI copy-files.ps1 VỪA GIẢI NÉN
# ============================================================
$CopyScript = Join-Path $UpdateFolder $CopyFilesRel

if (-not (Test-Path -LiteralPath $CopyScript -PathType Leaf)) {
    # Dự phòng: tìm ở bất kỳ cấp nào trong [folder-update].
    $CopyScript = Get-ChildItem -LiteralPath $UpdateFolder -Recurse -File -Filter "copy-files.ps1" |
        Select-Object -First 1 -ExpandProperty FullName
}

if (-not $CopyScript -or -not (Test-Path -LiteralPath $CopyScript -PathType Leaf)) {
    throw "Không tìm thấy copy-files.ps1 sau khi giải nén vào: $UpdateFolder"
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Gọi copy-files.ps1" -ForegroundColor Cyan
Write-Host $CopyScript -ForegroundColor DarkGray
Write-Host "============================================================" -ForegroundColor Cyan

& $CopyScript
$copyExitCode = $LASTEXITCODE

if ($null -eq $copyExitCode) {
    $copyExitCode = 0
}

Write-Host ""
if ($copyExitCode -eq 0) {
    Write-Host "HOÀN TẤT: đã cập nhật từ package $PackageName" -ForegroundColor Green
}
else {
    Write-Host "copy-files.ps1 kết thúc với mã lỗi $copyExitCode" -ForegroundColor Red
}

exit $copyExitCode
