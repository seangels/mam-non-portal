# ============================================================
# deploy-iis.change.ps1
# ============================================================
# Đặt script này trong THƯ MỤC CHỨA NHIỀU PACKAGE, ví dụ:
#
#   C:\deploy\
#       gv-portal-iis-20260101-090000.zip
#       gv-portal-iis-20260101-090000.zip.sha256
#       gv-portal-iis-20260215-143000.zip
#       gv-portal-iis-20260215-143000.zip.sha256
#       deploy-iis.change.ps1   <-- script này
#
# Việc script thực hiện:
#   1. Tự set execution policy cho tiến trình hiện tại (Bypass).
#   2. Tìm package .zip có thời gian MỚI NHẤT dựa theo tên file
#      (gv-portal-iis-YYYYMMDD-HHMMSS.zip).
#   3. Kiểm tra checksum .sha256 nếu có, rồi giải nén package đó.
#   4. Gọi copy-files.ps1 nằm trong thư mục vừa giải nén ra
#      (…\<package>\deploy\iis\copy-files.ps1) để copy artifact
#      sang C:\inetpub theo copyConfig.json.
#
# Cách chạy (Run as administrator):
#
#   Set-ExecutionPolicy -Scope Process Bypass
#   .\deploy-iis.change.ps1
# ============================================================

[CmdletBinding()]
param(
    # Thư mục chứa các package. Mặc định là thư mục chứa script này.
    [string]$PackageDirectory = $PSScriptRoot,

    # Mẫu tên package cần tìm.
    [string]$PackagePattern = "gv-portal-iis-*.zip",

    # Bỏ qua bước kiểm tra checksum .sha256.
    [switch]$SkipChecksum,

    # Giữ lại thư mục đã giải nén trước đó (mặc định xoá và giải nén lại).
    [switch]$KeepExistingExtract
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
# 2. TÌM PACKAGE MỚI NHẤT THEO TÊN
# ============================================================
$PackageDirectory = [System.IO.Path]::GetFullPath($PackageDirectory)

if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) {
    throw "Không tìm thấy thư mục package: $PackageDirectory"
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "DEPLOY IIS - CHANGE" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Thư mục package : $PackageDirectory" -ForegroundColor DarkGray
Write-Host "Mẫu tên        : $PackagePattern" -ForegroundColor DarkGray

# Chỉ nhận file có timestamp YYYYMMDD-HHMMSS trong tên; sắp theo timestamp giảm dần.
$TimestampRegex = 'gv-portal-iis-(?<ts>\d{8}-\d{6})'

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
                [pscustomobject]@{
                    File      = $_
                    Timestamp = $parsed
                    Stamp     = $match.Groups['ts'].Value
                }
            }
        }
    } |
    Sort-Object Timestamp -Descending |
    Select-Object -First 1

if ($null -eq $Package) {
    throw "Không tìm thấy package nào khớp '$PackagePattern' có timestamp hợp lệ trong '$PackageDirectory'."
}

$ZipPath = $Package.File.FullName
$PackageName = [System.IO.Path]::GetFileNameWithoutExtension($ZipPath)

Write-Host ""
Write-Host "Package mới nhất : $($Package.File.Name)" -ForegroundColor Green
Write-Host "Timestamp       : $($Package.Timestamp.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Green

# ============================================================
# 3. KIỂM TRA CHECKSUM (nếu có file .sha256)
# ============================================================
$ChecksumPath = $ZipPath + ".sha256"

if ($SkipChecksum) {
    Write-Host "Bỏ qua kiểm tra checksum (-SkipChecksum)." -ForegroundColor Yellow
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
# 4. GIẢI NÉN PACKAGE
# ============================================================
# Zip chứa sẵn thư mục gốc <PackageName>\… nên giải nén thẳng vào PackageDirectory.
$ExtractRoot = Join-Path $PackageDirectory $PackageName

if (Test-Path -LiteralPath $ExtractRoot) {
    if ($KeepExistingExtract) {
        Write-Host ""
        Write-Host "Dùng lại thư mục đã giải nén: $ExtractRoot" -ForegroundColor Yellow
    }
    else {
        Write-Host ""
        Write-Host "Xoá thư mục giải nén cũ: $ExtractRoot" -ForegroundColor Yellow
        Remove-Item -LiteralPath $ExtractRoot -Recurse -Force
    }
}

if (-not (Test-Path -LiteralPath $ExtractRoot)) {
    Write-Host ""
    Write-Host "Giải nén: $($Package.File.Name)" -ForegroundColor Cyan
    Write-Host "     ra : $PackageDirectory" -ForegroundColor Cyan
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $PackageDirectory -Force
}

if (-not (Test-Path -LiteralPath $ExtractRoot -PathType Container)) {
    throw "Giải nén xong nhưng không thấy thư mục: $ExtractRoot"
}

# Gỡ chặn (Zone.Identifier) toàn bộ file vừa giải nén.
Get-ChildItem -LiteralPath $ExtractRoot -Recurse -File -Force |
    Unblock-File -ErrorAction SilentlyContinue

# ============================================================
# 5. GỌI copy-files.ps1 TRONG THƯ MỤC VỪA GIẢI NÉN
# ============================================================
$CopyScript = Join-Path $ExtractRoot "deploy\iis\copy-files.ps1"

if (-not (Test-Path -LiteralPath $CopyScript -PathType Leaf)) {
    # Dự phòng: tìm ở bất kỳ cấp nào trong thư mục giải nén.
    $CopyScript = Get-ChildItem -LiteralPath $ExtractRoot -Recurse -File -Filter "copy-files.ps1" |
        Select-Object -First 1 -ExpandProperty FullName
}

if (-not $CopyScript -or -not (Test-Path -LiteralPath $CopyScript -PathType Leaf)) {
    throw "Không tìm thấy copy-files.ps1 trong package đã giải nén: $ExtractRoot"
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
    Write-Host "HOÀN TẤT: đã deploy package $PackageName" -ForegroundColor Green
}
else {
    Write-Host "copy-files.ps1 kết thúc với mã lỗi $copyExitCode" -ForegroundColor Red
}

exit $copyExitCode
