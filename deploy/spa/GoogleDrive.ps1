#Requires -Version 5.1
# Shared by the two SPA entry scripts. Does not authenticate when dot-sourced.

function Resolve-SpaCredentialFile {
    param([string]$SourceRoot, [string]$ClientSecretFile)

    if ($ClientSecretFile) {
        if (-not [IO.Path]::IsPathRooted($ClientSecretFile)) {
            $ClientSecretFile = Join-Path $SourceRoot $ClientSecretFile
        }
        $candidates = @($ClientSecretFile)
    }
    else {
        $candidates = @(
            (Join-Path $SourceRoot 'api/src/AdminPortal.Api/client_secret.json'),
            (Join-Path $SourceRoot 'deploy/iis/client_secret.json')
        )
    }
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return [IO.Path]::GetFullPath($candidate)
        }
    }
    throw 'Khong tim thay client_secret.json. Truyen -ClientSecretFile <OAuth Desktop client JSON>.'
}

function Get-SpaDriveHeaders {
    param(
        [string]$SourceRoot,
        [string]$ClientSecretFile,
        [string]$TokenFile,
        [switch]$ReadOnly,
        [switch]$ReAuth
    )

    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $credentialPath = Resolve-SpaCredentialFile $SourceRoot $ClientSecretFile
    $secret = Get-Content -LiteralPath $credentialPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $secret.PSObject.Properties['installed']) {
        throw 'client_secret.json phai la OAuth client loai Desktop app (khoa installed).'
    }
    $clientId = [string]$secret.installed.client_id
    $clientSecret = [string]$secret.installed.client_secret
    if (-not $clientId -or -not $clientSecret) { throw 'OAuth Desktop client JSON thieu client_id/client_secret.' }
    $scope = 'https://www.googleapis.com/auth/drive'
    if ($ReadOnly) { $scope += '.readonly' }
    if (-not $TokenFile) {
        $tokenName = 'upload.token.json'
        if ($ReadOnly) { $tokenName = 'download.token.json' }
        $TokenFile = Join-Path $SourceRoot "deploy/spa/$tokenName"
    }
    if (-not [IO.Path]::IsPathRooted($TokenFile)) { $TokenFile = Join-Path $SourceRoot $TokenFile }
    $TokenFile = [IO.Path]::GetFullPath($TokenFile)

    $refreshToken = $null
    $token = $null
    if (-not $ReAuth -and (Test-Path -LiteralPath $TokenFile -PathType Leaf)) {
        $stored = Get-Content -LiteralPath $TokenFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($stored.PSObject.Properties['client_id'] -and $stored.client_id -eq $clientId -and
            $stored.PSObject.Properties['scope'] -and $stored.scope -eq $scope -and
            $stored.PSObject.Properties['refresh_token'] -and $stored.refresh_token) {
            $refreshToken = [string]$stored.refresh_token
            try {
                $token = Invoke-RestMethod -Method Post -Uri 'https://oauth2.googleapis.com/token' `
                    -ContentType 'application/x-www-form-urlencoded' -UseBasicParsing -TimeoutSec 60 -Body @{
                        client_id = $clientId; client_secret = $clientSecret
                        refresh_token = $refreshToken; grant_type = 'refresh_token'
                    }
            }
            catch { throw 'Khong refresh duoc Google token. Kiem tra mang hoac chay lai voi -ReAuth.' }
        }
    }

    if (-not $token) {
        # PKCE + state; only Google's fixed endpoints receive OAuth credentials.
        $random = [Security.Cryptography.RandomNumberGenerator]::Create()
        $bytes = New-Object byte[] 32
        try { $random.GetBytes($bytes) } finally { $random.Dispose() }
        $verifier = [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = $sha.ComputeHash([Text.Encoding]::ASCII.GetBytes($verifier)) } finally { $sha.Dispose() }
        $challenge = [Convert]::ToBase64String($hash).TrimEnd('=').Replace('+', '-').Replace('/', '_')
        $state = [Guid]::NewGuid().ToString('N')
        $probe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        $probe.Start()
        $port = $probe.LocalEndpoint.Port
        $probe.Stop()
        $redirectUri = "http://127.0.0.1:$port/"
        $listener = [Net.HttpListener]::new()
        $listener.Prefixes.Add($redirectUri)
        try {
            $listener.Start()
            $authUri = 'https://accounts.google.com/o/oauth2/v2/auth' +
                "?client_id=$([uri]::EscapeDataString($clientId))" +
                "&redirect_uri=$([uri]::EscapeDataString($redirectUri))" +
                "&scope=$([uri]::EscapeDataString($scope))" +
                "&response_type=code&access_type=offline&prompt=consent&state=$state" +
                "&code_challenge=$challenge&code_challenge_method=S256"
            Write-Host "Dang nhap Google tren may nay (toi da 5 phut):`n$authUri"
            try { Start-Process $authUri | Out-Null } catch { Write-Host 'Mo link tren bang trinh duyet cua may nay.' }
            $pending = $listener.GetContextAsync()
            if (-not $pending.Wait(300000)) { throw 'Het 5 phut cho Google OAuth. Chay lai script de dang nhap.' }
            $context = $pending.GetAwaiter().GetResult()
            $code = $context.Request.QueryString['code']
            $validState = $context.Request.QueryString['state'] -ceq $state
            $reply = [Text.Encoding]::UTF8.GetBytes('Quay lai PowerShell de xem ket qua dang nhap. Co the dong tab nay.')
            $context.Response.ContentType = 'text/plain; charset=utf-8'
            $context.Response.OutputStream.Write($reply, 0, $reply.Length)
            $context.Response.Close()
            if (-not $validState -or -not $code) { throw 'Google OAuth bi tu choi hoac state khong khop.' }
            try {
                $token = Invoke-RestMethod -Method Post -Uri 'https://oauth2.googleapis.com/token' `
                    -ContentType 'application/x-www-form-urlencoded' -UseBasicParsing -TimeoutSec 60 -Body @{
                        code = $code; client_id = $clientId; client_secret = $clientSecret
                        redirect_uri = $redirectUri; grant_type = 'authorization_code'; code_verifier = $verifier
                    }
            }
            catch { throw 'Doi Google authorization code that bai. Kiem tra OAuth Desktop client va thu lai.' }
            if (-not $token.PSObject.Properties['refresh_token'] -or -not $token.refresh_token) {
                throw 'Google khong tra refresh_token. Chay lai voi -ReAuth va chap nhan quyen Drive.'
            }
            $refreshToken = [string]$token.refresh_token
        }
        finally { $listener.Close() }
    }
    if (-not $token.PSObject.Properties['access_token'] -or -not $token.access_token) {
        throw 'Google khong tra access_token.'
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $TokenFile) -Force | Out-Null
    @{ client_id = $clientId; scope = $scope; refresh_token = $refreshToken } |
        ConvertTo-Json | Set-Content -LiteralPath $TokenFile -Encoding UTF8
    return @{ Authorization = "Bearer $($token.access_token)" }
}

function Resolve-SpaDriveFolderId {
    param([string]$Value)
    if ($Value -match '\A[A-Za-z0-9_-]{10,}\z') { return $Value }
    $uri = $null
    if ([uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri) -and $uri.Host -eq 'drive.google.com' -and
        $uri.AbsolutePath -match '/folders/([A-Za-z0-9_-]+)') { return $Matches[1] }
    throw 'DriveFolder phai la folder ID hoac URL https://drive.google.com/drive/folders/<id>.'
}

function Get-SpaDriveFiles {
    param([string]$FolderId, [hashtable]$Headers)
    # Do not sort by Drive modifiedTime: the caller parses the timestamp in the name.
    $query = "'$FolderId' in parents and trashed = false and mimeType != 'application/vnd.google-apps.folder'"
    $baseUri = 'https://www.googleapis.com/drive/v3/files' +
        "?q=$([uri]::EscapeDataString($query))&pageSize=1000&supportsAllDrives=true&includeItemsFromAllDrives=true" +
        '&fields=nextPageToken,incompleteSearch,files(id,name,size,md5Checksum,webViewLink)'
    $nextPage = $null
    do {
        $uri = $baseUri
        if ($nextPage) { $uri += "&pageToken=$([uri]::EscapeDataString($nextPage))" }
        $response = Invoke-RestMethod -Method Get -Uri $uri -Headers $Headers -UseBasicParsing -TimeoutSec 120
        if ($response.PSObject.Properties['incompleteSearch'] -and $response.incompleteSearch) {
            throw 'Drive tra ket qua tim kiem khong day du; huy de tranh chon nham goi.'
        }
        foreach ($file in $response.files) { $file }
        $nextPage = $null
        if ($response.PSObject.Properties['nextPageToken']) { $nextPage = $response.nextPageToken }
    } while ($nextPage)
}

function Assert-SpaDriveDownload {
    param([string]$Path, $Metadata)
    if (-not $Metadata.PSObject.Properties['size'] -or [long]$Metadata.size -le 0 -or
        -not $Metadata.PSObject.Properties['md5Checksum'] -or $Metadata.md5Checksum -notmatch '\A[0-9a-fA-F]{32}\z') {
        throw 'Drive metadata thieu size/md5Checksum hop le; khong cai goi.'
    }
    if ((Get-Item -LiteralPath $Path).Length -ne [long]$Metadata.size -or
        (Get-FileHash -LiteralPath $Path -Algorithm MD5).Hash -ine $Metadata.md5Checksum) {
        throw 'Goi tai ve sai kich thuoc hoac checksum MD5; giu nguyen build hien tai.'
    }
}

function Send-SpaDrivePackage {
    param([string]$Path, [string]$FolderId, [hashtable]$Headers)
    $name = [IO.Path]::GetFileName($Path)
    $initUri = 'https://www.googleapis.com/upload/drive/v3/files' +
        '?uploadType=resumable&supportsAllDrives=true&fields=id,name,size,md5Checksum,webViewLink'
    $metadata = @{ name = $name; parents = @($FolderId) } | ConvertTo-Json -Compress
    $init = Invoke-WebRequest -Method Post -Uri $initUri -Headers ($Headers + @{
        'X-Upload-Content-Type' = 'application/zip'
        'X-Upload-Content-Length' = [string](Get-Item -LiteralPath $Path).Length
    }) -Body $metadata -ContentType 'application/json; charset=utf-8' -UseBasicParsing -TimeoutSec 120
    $session = [string](@($init.Headers['Location'])[0])
    $sessionUri = $null
    if (-not [uri]::TryCreate($session, [UriKind]::Absolute, [ref]$sessionUri) -or
        $sessionUri.Scheme -ne 'https' -or $sessionUri.Host -ne 'www.googleapis.com') {
        throw 'Drive khong tra upload session URL hop le.'
    }
    $result = Invoke-WebRequest -Method Put -Uri $session -Headers $Headers -InFile $Path `
        -ContentType 'application/zip' -UseBasicParsing -TimeoutSec 1800
    $created = $result.Content | ConvertFrom-Json
    # Read back metadata; do not report success from the PUT alone.
    $verified = Invoke-RestMethod -Method Get -Headers $Headers -UseBasicParsing -TimeoutSec 120 -Uri (
        "https://www.googleapis.com/drive/v3/files/$([uri]::EscapeDataString($created.id))" +
        '?supportsAllDrives=true&fields=id,name,size,md5Checksum,webViewLink')
    if ($verified.name -cne $name) { throw 'Drive readback khac ten goi da upload.' }
    Assert-SpaDriveDownload -Path $Path -Metadata $verified
    return $verified
}
