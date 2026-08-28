# ============================================================
# upload-release-to-drive.ps1
# ============================================================
# Tải package IIS mới nhất trong thư mục release/ lên một thư mục
# Google Drive (link hoặc id ghi trong file config JSON).
#
#   [thu-muc-script]\
#       upload-release-to-drive.ps1     <-- script nay
#       upload-release-to-drive.json    <-- file config
#       client_secret.json              <-- OAuth client (Desktop app)
#
# Viec script thuc hien:
#   1. Doc upload-release-to-drive.json de biet:
#        - driveFolder            : link/id thu muc Drive dich (bat buoc)
#        - releaseDirectory       : thu muc chua package (mac dinh ..\..\release)
#        - packagePattern         : mau ten package (mac dinh gv-portal-iis-*.zip)
#        - uploadChecksumSidecar  : co upload kem file .sha256 khong (mac dinh true)
#        - clientSecretFile       : ten file OAuth client (mac dinh client_secret.json)
#        - tokenFile              : noi luu refresh token (mac dinh upload-release-to-drive.token.json)
#        - scope                  : OAuth scope (mac dinh .../auth/drive)
#   2. Quet releaseDirectory, chon package co timestamp trong TEN
#      FILE moi nhat (YYYYMMDD-HHMMSS).
#   3. Xac thuc Google bang client_secret.json cung cap:
#        - Lan dau: mo trinh duyet de cap quyen (loopback OAuth),
#          luu refresh token vao tokenFile.
#        - Lan sau: dung refresh token, khong can thao tac.
#   4. Kiem tra tren thu muc Drive da co file cung TEN chua.
#      Chua co -> upload (resumable). Da co -> bo qua, in link.
#
# Cach chay:
#   Set-ExecutionPolicy -Scope Process Bypass
#   .\upload-release-to-drive.ps1
#
# Tham so tuy chon:
#   -ConfigFile <path>   File config khac (mac dinh upload-release-to-drive.json canh script).
#   -Force               Upload lai ngay ca khi Drive da co file cung ten.
#   -ReAuth              Bo qua token da luu, chay lai luong cap quyen.
# ============================================================

[CmdletBinding()]
param(
    [string]$ConfigFile,
    [switch]$Force,
    [switch]$ReAuth
)

# ============================================================
# UTF-8 CONSOLE (ho tro tieng Viet tren Windows PowerShell 5.1)
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
$ProgressPreference     = "SilentlyContinue"
Set-StrictMode -Version 3.0
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

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

function Resolve-DriveFolderId {
    param([string]$Value)

    $v = ([string]$Value).Trim()
    if ($v -match '/folders/([A-Za-z0-9_-]+)') { return $Matches[1] }
    if ($v -match '[?&]id=([A-Za-z0-9_-]+)')   { return $Matches[1] }
    if ($v -match '^[A-Za-z0-9_-]{10,}$')      { return $v }
    throw "Khong nhan dien duoc Drive folder id/link tu 'driveFolder': $Value"
}

function Get-ContentTypeFor {
    param([string]$Path)

    switch -Wildcard ($Path.ToLowerInvariant()) {
        "*.zip"    { return "application/zip" }
        "*.sha256" { return "text/plain" }
        default    { return "application/octet-stream" }
    }
}

function Read-WebExceptionBody {
    param($Exception)

    try {
        $resp = $Exception.Response
        if ($null -ne $resp) {
            $stream = $resp.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            return $reader.ReadToEnd()
        }
    }
    catch { }
    return $null
}

# ============================================================
# 1. DOC CONFIG
# ============================================================
$ScriptDir = [System.IO.Path]::GetFullPath($PSScriptRoot)

if ([string]::IsNullOrWhiteSpace($ConfigFile)) {
    $ConfigFile = Join-Path $ScriptDir "upload-release-to-drive.json"
}
elseif (-not [System.IO.Path]::IsPathRooted($ConfigFile)) {
    $ConfigFile = Join-Path $ScriptDir $ConfigFile
}
$ConfigFile = [System.IO.Path]::GetFullPath($ConfigFile)

if (-not (Test-Path -LiteralPath $ConfigFile -PathType Leaf)) {
    throw "Khong tim thay file config: $ConfigFile"
}

try {
    $Config = Get-Content -LiteralPath $ConfigFile -Raw -Encoding UTF8 | ConvertFrom-Json
}
catch {
    throw "Khong doc duoc $ConfigFile. JSON khong hop le.`n$($_.Exception.Message)"
}

$ConfigDir       = [System.IO.Path]::GetDirectoryName($ConfigFile)
$PackagePattern  = Get-ConfigString $Config "packagePattern" "gv-portal-iis-*.zip"
$UploadChecksum  = Get-ConfigBool   $Config "uploadChecksumSidecar" $true
$ClientSecretRel = Get-ConfigString $Config "clientSecretFile" "client_secret.json"
$TokenFileRel    = Get-ConfigString $Config "tokenFile" "upload-release-to-drive.token.json"
$Scope           = Get-ConfigString $Config "scope" "https://www.googleapis.com/auth/drive"

$DriveFolderRaw = Get-ConfigString $Config "driveFolder"
if ([string]::IsNullOrWhiteSpace($DriveFolderRaw)) {
    throw "Thieu 'driveFolder' (link hoac id thu muc Drive) trong $ConfigFile."
}
$FolderId = Resolve-DriveFolderId $DriveFolderRaw

$ReleaseDirRel = Get-ConfigString $Config "releaseDirectory" "..\..\release"
if ([System.IO.Path]::IsPathRooted($ReleaseDirRel)) {
    $ReleaseDir = $ReleaseDirRel
}
else {
    $ReleaseDir = Join-Path $ConfigDir $ReleaseDirRel
}
$ReleaseDir = [System.IO.Path]::GetFullPath($ReleaseDir)

if ([System.IO.Path]::IsPathRooted($ClientSecretRel)) { $ClientSecretPath = $ClientSecretRel }
else { $ClientSecretPath = Join-Path $ConfigDir $ClientSecretRel }
$ClientSecretPath = [System.IO.Path]::GetFullPath($ClientSecretPath)

if ([System.IO.Path]::IsPathRooted($TokenFileRel)) { $TokenPath = $TokenFileRel }
else { $TokenPath = Join-Path $ConfigDir $TokenFileRel }
$TokenPath = [System.IO.Path]::GetFullPath($TokenPath)

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "UPLOAD RELEASE -> GOOGLE DRIVE" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Config          : $ConfigFile" -ForegroundColor DarkGray
Write-Host "Release dir     : $ReleaseDir" -ForegroundColor DarkGray
Write-Host "Package pattern : $PackagePattern" -ForegroundColor DarkGray
Write-Host "Drive folder id : $FolderId" -ForegroundColor DarkGray
Write-Host "Client secret   : $ClientSecretPath" -ForegroundColor DarkGray

if (-not (Test-Path -LiteralPath $ReleaseDir -PathType Container)) {
    throw "Khong tim thay thu muc release: $ReleaseDir"
}
if (-not (Test-Path -LiteralPath $ClientSecretPath -PathType Leaf)) {
    throw "Khong tim thay file OAuth client: $ClientSecretPath"
}

# ============================================================
# 2. TIM PACKAGE MOI NHAT THEO TIMESTAMP TRONG TEN
# ============================================================
$TimestampRegex = '(?<ts>\d{8}-\d{6})'

$Package = Get-ChildItem -LiteralPath $ReleaseDir -Filter $PackagePattern -File |
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
    throw "Khong tim thay package nao khop '$PackagePattern' co timestamp hop le trong '$ReleaseDir'."
}

$ZipPath = $Package.File.FullName

Write-Host ""
Write-Host "Package moi nhat : $($Package.File.Name)" -ForegroundColor Green
Write-Host "Timestamp       : $($Package.Timestamp.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Green

# Danh sach file se upload: package + (tuy chon) file .sha256 di kem.
$UploadTargets = @($ZipPath)
if ($UploadChecksum) {
    $ChecksumPath = $ZipPath + ".sha256"
    if (Test-Path -LiteralPath $ChecksumPath -PathType Leaf) {
        $UploadTargets += $ChecksumPath
    }
    else {
        Write-Host "Khong co file $([System.IO.Path]::GetFileName($ChecksumPath)), bo qua checksum sidecar." -ForegroundColor Yellow
    }
}

# ============================================================
# 3. XAC THUC GOOGLE (OAuth loopback, luu refresh token)
# ============================================================
$secretJson = Get-Content -LiteralPath $ClientSecretPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($secretJson.PSObject.Properties.Name -contains "installed") { $clientConf = $secretJson.installed }
elseif ($secretJson.PSObject.Properties.Name -contains "web")   { $clientConf = $secretJson.web }
else { throw "client_secret.json khong hop le: thieu khoa 'installed' hoac 'web'." }

$ClientId     = [string]$clientConf.client_id
$ClientSecret = [string]$clientConf.client_secret
$TokenUri     = Get-ConfigString $clientConf "token_uri" "https://oauth2.googleapis.com/token"
$AuthUri      = Get-ConfigString $clientConf "auth_uri"  "https://accounts.google.com/o/oauth2/auth"

function Save-Token {
    param([string]$RefreshToken, [string]$AccessToken, [datetime]$ExpiresAtUtc)

    $payload = [pscustomobject]@{
        refresh_token = $RefreshToken
        access_token  = $AccessToken
        expires_at    = $ExpiresAtUtc.ToString("o")
        scope         = $Scope
        client_id     = $ClientId
        saved_at      = (Get-Date).ToUniversalTime().ToString("o")
    }
    $payload | ConvertTo-Json | Set-Content -LiteralPath $TokenPath -Encoding UTF8
}

function Invoke-TokenRequest {
    param([hashtable]$Body)

    try {
        return Invoke-RestMethod -Method Post -Uri $TokenUri -Body $Body `
            -ContentType "application/x-www-form-urlencoded" -UseBasicParsing
    }
    catch {
        $detail = Read-WebExceptionBody $_.Exception
        if ($detail) { throw "Yeu cau token that bai: $detail" }
        throw
    }
}

function Invoke-ConsentFlow {
    Write-Host ""
    Write-Host "Chua co refresh token hop le -> mo trinh duyet de cap quyen Google Drive." -ForegroundColor Yellow

    # Chon cong loopback trong.
    $probe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $probe.Start()
    $port = ([System.Net.IPEndPoint]$probe.LocalEndpoint).Port
    $probe.Stop()

    $redirectUri = "http://localhost:$port/"
    $http = [System.Net.HttpListener]::new()
    $http.Prefixes.Add($redirectUri)
    $http.Start()

    try {
        $state = [Guid]::NewGuid().ToString("N")
        $authUrl = "$AuthUri" +
            "?client_id=$([uri]::EscapeDataString($ClientId))" +
            "&redirect_uri=$([uri]::EscapeDataString($redirectUri))" +
            "&response_type=code" +
            "&scope=$([uri]::EscapeDataString($Scope))" +
            "&access_type=offline&prompt=consent" +
            "&state=$state"

        Write-Host "Neu trinh duyet khong tu mo, dan link sau vao trinh duyet:" -ForegroundColor DarkGray
        Write-Host $authUrl -ForegroundColor DarkGray
        try { Start-Process $authUrl | Out-Null } catch { }

        $context = $http.GetContext()
        $req = $context.Request
        $code          = $req.QueryString["code"]
        $err           = $req.QueryString["error"]
        $returnedState = $req.QueryString["state"]

        $html = "<html><head><meta charset='utf-8'></head><body style='font-family:sans-serif'>" +
                "Da nhan phan hoi tu Google. Ban co the dong tab nay va quay lai cua so PowerShell." +
                "</body></html>"
        $buffer = [System.Text.Encoding]::UTF8.GetBytes($html)
        $context.Response.ContentType = "text/html; charset=utf-8"
        $context.Response.StatusCode = 200
        $context.Response.OutputStream.Write($buffer, 0, $buffer.Length)
        $context.Response.OutputStream.Close()

        if ($err)   { throw "Google tra ve loi uy quyen: $err" }
        if (-not $code) { throw "Khong nhan duoc authorization code tu Google." }
        if ($returnedState -ne $state) { throw "State khong khop - co the bi can thiep, huy." }

        $token = Invoke-TokenRequest -Body @{
            code          = $code
            client_id     = $ClientId
            client_secret = $ClientSecret
            redirect_uri  = $redirectUri
            grant_type    = "authorization_code"
        }

        if (-not $token.refresh_token) {
            throw "Google khong tra ve refresh_token. Thu go quyen ung dung tai https://myaccount.google.com/permissions roi chay lai."
        }

        $expiresAt = (Get-Date).ToUniversalTime().AddSeconds([double]$token.expires_in - 60)
        Save-Token -RefreshToken $token.refresh_token -AccessToken $token.access_token -ExpiresAtUtc $expiresAt
        Write-Host "Da luu refresh token: $TokenPath" -ForegroundColor Green
        return [string]$token.access_token
    }
    finally {
        if ($http.IsListening) { $http.Stop() }
        $http.Close()
    }
}

$AccessToken = $null

if (-not $ReAuth -and (Test-Path -LiteralPath $TokenPath -PathType Leaf)) {
    try {
        $stored = Get-Content -LiteralPath $TokenPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        $stored = $null
        Write-Host "Token file hong, se cap quyen lai." -ForegroundColor Yellow
    }

    if ($stored -and $stored.refresh_token) {
        Write-Host ""
        Write-Host "Dung refresh token da luu de lay access token moi..." -ForegroundColor DarkGray
        try {
            $token = Invoke-TokenRequest -Body @{
                client_id     = $ClientId
                client_secret = $ClientSecret
                refresh_token = [string]$stored.refresh_token
                grant_type    = "refresh_token"
            }
            $AccessToken = [string]$token.access_token
            $expiresAt = (Get-Date).ToUniversalTime().AddSeconds([double]$token.expires_in - 60)
            Save-Token -RefreshToken ([string]$stored.refresh_token) -AccessToken $AccessToken -ExpiresAtUtc $expiresAt
        }
        catch {
            Write-Host "Refresh token khong dung nua ($($_.Exception.Message)). Se cap quyen lai." -ForegroundColor Yellow
            $AccessToken = $null
        }
    }
}

if (-not $AccessToken) {
    $AccessToken = Invoke-ConsentFlow
}

$AuthHeader = @{ Authorization = "Bearer $AccessToken" }

# ============================================================
# 4. KIEM TRA + UPLOAD TUNG FILE
# ============================================================
function Find-DriveFile {
    param([string]$Name)

    $escaped = $Name.Replace("\", "\\").Replace("'", "\'")
    $q = "name = '$escaped' and '$FolderId' in parents and trashed = false"
    $uri = "https://www.googleapis.com/drive/v3/files" +
        "?q=$([uri]::EscapeDataString($q))" +
        "&fields=files(id,name,size,md5Checksum,webViewLink)" +
        "&pageSize=20&supportsAllDrives=true&includeItemsFromAllDrives=true"

    try {
        $res = Invoke-RestMethod -Method Get -Uri $uri -Headers $AuthHeader -UseBasicParsing
    }
    catch {
        $detail = Read-WebExceptionBody $_.Exception
        if ($detail) { throw "Tim file tren Drive that bai: $detail" }
        throw
    }

    if ($res.files -and $res.files.Count -gt 0) { return $res.files[0] }
    return $null
}

function Upload-DriveFile {
    param([string]$Path)

    $name = [System.IO.Path]::GetFileName($Path)
    $contentType = Get-ContentTypeFor $Path
    $length = (Get-Item -LiteralPath $Path).Length
    $metadata = @{ name = $name; parents = @($FolderId) } | ConvertTo-Json -Compress

    $initUri = "https://www.googleapis.com/upload/drive/v3/files" +
        "?uploadType=resumable&supportsAllDrives=true" +
        "&fields=id,name,size,webViewLink"

    try {
        $init = Invoke-WebRequest -Method Post -Uri $initUri -Headers ($AuthHeader + @{
                "X-Upload-Content-Type"   = $contentType
                "X-Upload-Content-Length" = [string]$length
            }) -ContentType "application/json; charset=UTF-8" -Body $metadata -UseBasicParsing
    }
    catch {
        $detail = Read-WebExceptionBody $_.Exception
        if ($detail) { throw "Khoi tao upload that bai: $detail" }
        throw
    }

    $session = $null
    foreach ($key in $init.Headers.Keys) {
        if ($key -ieq "Location") {
            $val = $init.Headers[$key]
            if ($val -is [array]) { $session = [string]$val[0] } else { $session = [string]$val }
            break
        }
    }
    if ([string]::IsNullOrWhiteSpace($session)) {
        throw "Khong nhan duoc upload session URL tu Google."
    }

    try {
        $put = Invoke-WebRequest -Method Put -Uri $session -Headers $AuthHeader `
            -InFile $Path -ContentType $contentType -UseBasicParsing
    }
    catch {
        $detail = Read-WebExceptionBody $_.Exception
        if ($detail) { throw "Upload noi dung that bai: $detail" }
        throw
    }

    return ($put.Content | ConvertFrom-Json)
}

$uploaded = 0
$skipped  = 0

foreach ($target in $UploadTargets) {
    $name = [System.IO.Path]::GetFileName($target)
    Write-Host ""
    Write-Host "-> $name" -ForegroundColor Cyan

    $existing = $null
    if (-not $Force) {
        $existing = Find-DriveFile -Name $name
    }

    if ($existing) {
        $skipped++
        Write-Host "   Da co tren Drive, bo qua." -ForegroundColor Yellow
        Write-Host "   id   : $($existing.id)" -ForegroundColor DarkGray
        if ($existing.webViewLink) { Write-Host "   link : $($existing.webViewLink)" -ForegroundColor DarkGray }
        continue
    }

    $created = Upload-DriveFile -Path $target
    $uploaded++
    Write-Host "   Upload xong." -ForegroundColor Green
    Write-Host "   id   : $($created.id)" -ForegroundColor DarkGray
    if ($created.webViewLink) { Write-Host "   link : $($created.webViewLink)" -ForegroundColor DarkGray }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "HOAN TAT: upload $uploaded file, bo qua $skipped file (da ton tai)." -ForegroundColor Green
Write-Host "Thu muc Drive: https://drive.google.com/drive/folders/$FolderId" -ForegroundColor Green

exit 0
