[CmdletBinding()]
param(
    [switch]$Build,
    [switch]$PrepareOnly,
    [switch]$SkipNpmInstall,
    [switch]$SkipHostsFile,
    [switch]$SkipHealthCheck,
    [switch]$DoNotTrustSelfSignedCertificate,
    [string]$ArtifactsPath,
    [string]$PostgresHost = "localhost",
    [ValidateRange(1, 65535)]
    [int]$PostgresPort = 5432,
    [string]$PostgresDatabase = "gv_portal",
    [string]$PostgresUsername = "gv_portal_app",
    [Security.SecureString]$PostgresPassword,
    [Security.SecureString]$JwtSigningKey,
    [string]$CertificateThumbprint
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 3.0

$apiHostName = "api-gv-portal.local"
$uiHostName = "gv-portal.local"
$apiSiteName = "api-gv-portal.local"
$uiSiteName = "gv-portal.local"
$apiPoolName = "api-gv-portal.local"
$uiPoolName = "gv-portal.local"
$apiTargetPath = "C:\inetpub\api-gv-portal.local"
$uiTargetPath = "C:\inetpub\gv-portal.local"
$certificateFriendlyName = "GV Portal local HTTPS"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
if ([string]::IsNullOrWhiteSpace($ArtifactsPath)) {
    $ArtifactsPath = Join-Path $repoRoot "artifacts\iis"
}
elseif (-not [IO.Path]::IsPathRooted($ArtifactsPath)) {
    $ArtifactsPath = Join-Path $repoRoot $ArtifactsPath
}
$ArtifactsPath = [IO.Path]::GetFullPath($ArtifactsPath)
$apiArtifactPath = Join-Path $ArtifactsPath "api"
$uiArtifactPath = Join-Path $ArtifactsPath "ui"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host ("==> " + $Message) -ForegroundColor Cyan
}

function Assert-ChildPath {
    param(
        [string]$Path,
        [string]$AllowedRoot,
        [string]$Description
    )

    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd("\")
    $fullRoot = [IO.Path]::GetFullPath($AllowedRoot).TrimEnd("\")
    $prefix = $fullRoot + [IO.Path]::DirectorySeparatorChar
    if ($fullPath -eq $fullRoot -or -not $fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw ("{0} is not safely contained in {1}: {2}" -f $Description, $fullRoot, $fullPath)
    }
}

function Reset-ArtifactDirectory {
    param([string]$Path)

    Assert-ChildPath -Path $Path -AllowedRoot $ArtifactsPath -Description "Artifact path"
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Invoke-NativeCommand {
    param(
        [string]$FilePath,
        [string[]]$CommandArguments,
        [string]$WorkingDirectory
    )

    Push-Location $WorkingDirectory
    try {
        & $FilePath @CommandArguments
        if ($LASTEXITCODE -ne 0) {
            throw "$FilePath failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Build-Artifacts {
    Write-Step "Build .NET 10 API and Angular UI for IIS"
    New-Item -ItemType Directory -Path $ArtifactsPath -Force | Out-Null
    Reset-ArtifactDirectory -Path $apiArtifactPath
    Reset-ArtifactDirectory -Path $uiArtifactPath

    $apiProject = Join-Path $repoRoot "api\src\AdminPortal.Api\AdminPortal.Api.csproj"
    Invoke-NativeCommand -FilePath "dotnet" -CommandArguments @(
        "publish",
        $apiProject,
        "-c",
        "Release",
        "-o",
        $apiArtifactPath,
        "--nologo"
    ) -WorkingDirectory (Join-Path $repoRoot "api")

    $uiSourcePath = Join-Path $repoRoot "ui"
    if (-not $SkipNpmInstall) {
        Invoke-NativeCommand -FilePath "npm.cmd" -CommandArguments @("ci") -WorkingDirectory $uiSourcePath
    }
    elseif (-not (Test-Path -LiteralPath (Join-Path $uiSourcePath "node_modules"))) {
        throw "ui\node_modules was not found. Remove -SkipNpmInstall or run npm ci first."
    }

    Invoke-NativeCommand -FilePath "npm.cmd" -CommandArguments @(
        "run",
        "build",
        "--",
        "--configuration",
        "iis",
        "--output-path",
        $uiArtifactPath
    ) -WorkingDirectory $uiSourcePath

    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "ui.web.config") -Destination (Join-Path $uiArtifactPath "web.config") -Force
}

function Assert-ArtifactLayout {
    $requiredFiles = @(
        (Join-Path $apiArtifactPath "AdminPortal.Api.dll"),
        (Join-Path $apiArtifactPath "web.config"),
        (Join-Path $uiArtifactPath "index.html"),
        (Join-Path $uiArtifactPath "web.config")
    )
    foreach ($file in $requiredFiles) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            throw "Missing artifact: $file. Run this script with -Build first."
        }
    }

    $mainBundle = Get-ChildItem -LiteralPath $uiArtifactPath -Filter "main*.js" -File | Select-Object -First 1
    if ($null -eq $mainBundle) {
        throw "Angular main bundle was not found in $uiArtifactPath."
    }
    if (-not (Select-String -LiteralPath $mainBundle.FullName -SimpleMatch "https://api-gv-portal.local/api/v1" -Quiet)) {
        throw "Angular artifact does not target https://api-gv-portal.local/api/v1."
    }
}

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal -ArgumentList $identity
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Open Windows PowerShell with Run as administrator and try again."
    }
}

function Assert-IisTarget {
    param(
        [string]$Path,
        [string]$ExpectedLeaf
    )

    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd("\")
    $inetpub = [IO.Path]::GetFullPath("C:\inetpub").TrimEnd("\")
    Assert-ChildPath -Path $fullPath -AllowedRoot $inetpub -Description "IIS target"
    if ([IO.Path]::GetFileName($fullPath) -ne $ExpectedLeaf) {
        throw "IIS target must end with '$ExpectedLeaf': $fullPath"
    }
}

function ConvertFrom-SecureValue {
    param([Security.SecureString]$Value)

    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function Invoke-RobocopyMirror {
    param(
        [string]$Source,
        [string]$Destination,
        [string[]]$ExcludedDirectories = @()
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    $arguments = @(
        $Source,
        $Destination,
        "/MIR",
        "/R:2",
        "/W:2",
        "/COPY:DAT",
        "/DCOPY:DAT",
        "/NP",
        "/NFL",
        "/NDL",
        "/NJH",
        "/NJS"
    )
    if ($ExcludedDirectories.Count -gt 0) {
        $arguments += "/XD"
        $arguments += $ExcludedDirectories
    }

    & robocopy.exe @arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ge 8) {
        throw "Robocopy from '$Source' to '$Destination' failed with exit code $exitCode."
    }
}

function Set-ApiWebConfig {
    param(
        [string]$Path,
        [string]$ConnectionString,
        [string]$SigningKey
    )

    [xml]$document = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $aspNetCore = $document.SelectSingleNode("/configuration/location/system.webServer/aspNetCore")
    if ($null -eq $aspNetCore) {
        throw "Published web.config has no aspNetCore node: $Path"
    }

    $aspNetCore.SetAttribute("stdoutLogEnabled", "false")
    $aspNetCore.SetAttribute("hostingModel", "inprocess")

    $oldEnvironmentVariables = $aspNetCore.SelectSingleNode("environmentVariables")
    if ($null -ne $oldEnvironmentVariables) {
        [void]$aspNetCore.RemoveChild($oldEnvironmentVariables)
    }

    $environmentVariables = $document.CreateElement("environmentVariables")
    $values = [ordered]@{
        "ASPNETCORE_ENVIRONMENT" = "Production"
        "ASPNETCORE_DETAILEDERRORS" = "false"
        "AllowedHosts" = $apiHostName
        "ConnectionStrings__DefaultConnection" = $ConnectionString
        "Database__MigrateOnStartup" = "true"
        "Jwt__SigningKey" = $SigningKey
        "Security__AllowedOrigins__0" = ("https://" + $uiHostName)
    }
    foreach ($entry in $values.GetEnumerator()) {
        $node = $document.CreateElement("environmentVariable")
        $node.SetAttribute("name", [string]$entry.Key)
        $node.SetAttribute("value", [string]$entry.Value)
        [void]$environmentVariables.AppendChild($node)
    }
    [void]$aspNetCore.AppendChild($environmentVariables)
    $document.Save($Path)
}

function Ensure-WebAppPool {
    param([string]$Name)

    $poolPath = "IIS:\AppPools\$Name"
    if (-not (Test-Path $poolPath)) {
        New-WebAppPool -Name $Name | Out-Null
    }
    Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value ""
    Set-ItemProperty $poolPath -Name managedPipelineMode -Value "Integrated"
    Set-ItemProperty $poolPath -Name enable32BitAppOnWin64 -Value $false
    Set-ItemProperty $poolPath -Name processModel.identityType -Value 4
}

function Stop-ManagedSite {
    param(
        [string]$SiteName,
        [string]$PoolName
    )

    if (Test-Path "IIS:\Sites\$SiteName") {
        Stop-Website -Name $SiteName -ErrorAction SilentlyContinue
    }
    if (Test-Path "IIS:\AppPools\$PoolName") {
        Stop-WebAppPool -Name $PoolName -ErrorAction SilentlyContinue
    }
}

function Start-ManagedSite {
    param(
        [string]$SiteName,
        [string]$PoolName
    )

    if ((Get-WebAppPoolState -Name $PoolName).Value -ne "Started") {
        Start-WebAppPool -Name $PoolName
    }
    if ((Get-WebsiteState -Name $SiteName).Value -ne "Started") {
        Start-Website -Name $SiteName
    }
}

function Grant-SitePermissions {
    param(
        [string]$Path,
        [string]$PoolName
    )

    $identity = "IIS AppPool\$PoolName"
    & icacls.exe $Path `
        /inheritance:r `
        /grant:r `
        "*S-1-5-18:(OI)(CI)(F)" `
        "*S-1-5-32-544:(OI)(CI)(F)" `
        ("{0}:(OI)(CI)(RX)" -f $identity) `
        /T /C /Q | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot grant read access to $identity on $Path."
    }
}

function Ensure-LocalCertificateTrust {
    param([Security.Cryptography.X509Certificates.X509Certificate2]$Certificate)

    if ($DoNotTrustSelfSignedCertificate) {
        return
    }

    $rootCertificate = Get-Item "Cert:\LocalMachine\Root\$($Certificate.Thumbprint)" -ErrorAction SilentlyContinue
    if ($null -ne $rootCertificate) {
        return
    }

    $temporaryCertificate = Join-Path ([IO.Path]::GetTempPath()) ("gv-portal-" + [Guid]::NewGuid().ToString("N") + ".cer")
    try {
        Export-Certificate -Cert $Certificate -FilePath $temporaryCertificate | Out-Null
        Import-Certificate -FilePath $temporaryCertificate -CertStoreLocation "Cert:\LocalMachine\Root" | Out-Null
    }
    finally {
        if (Test-Path -LiteralPath $temporaryCertificate) {
            Remove-Item -LiteralPath $temporaryCertificate -Force
        }
    }
}

function Get-OrCreateCertificate {
    if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
        $normalizedThumbprint = $CertificateThumbprint.Replace(" ", "")
        $provided = Get-Item "Cert:\LocalMachine\My\$normalizedThumbprint" -ErrorAction Stop
        if (-not $provided.HasPrivateKey) {
            throw "Certificate $normalizedThumbprint has no private key."
        }
        if ($provided.NotAfter -le (Get-Date)) {
            throw "Certificate $normalizedThumbprint has expired."
        }
        $providedDnsNames = @($provided.DnsNameList | ForEach-Object { $_.Unicode })
        if (($providedDnsNames -notcontains $apiHostName) -or ($providedDnsNames -notcontains $uiHostName)) {
            throw "Certificate $normalizedThumbprint must contain both IIS host names in its SAN."
        }
        return $provided
    }

    $minimumExpiry = (Get-Date).AddDays(30)
    $existing = Get-ChildItem "Cert:\LocalMachine\My" | Where-Object {
        if ($_.FriendlyName -ne $certificateFriendlyName -or $_.NotAfter -le $minimumExpiry -or -not $_.HasPrivateKey) {
            return $false
        }
        $dnsNames = @($_.DnsNameList | ForEach-Object { $_.Unicode })
        return ($dnsNames -contains $apiHostName) -and ($dnsNames -contains $uiHostName)
    } | Sort-Object NotAfter -Descending | Select-Object -First 1

    if ($null -ne $existing) {
        Ensure-LocalCertificateTrust -Certificate $existing
        return $existing
    }

    Write-Step "Create the local HTTPS certificate for both host names"
    $certificateParameters = @{
        DnsName = @($uiHostName, $apiHostName)
        CertStoreLocation = "Cert:\LocalMachine\My"
        FriendlyName = $certificateFriendlyName
        NotAfter = (Get-Date).AddYears(5)
        KeyAlgorithm = "RSA"
        KeyLength = 2048
        HashAlgorithm = "SHA256"
    }
    $certificate = New-SelfSignedCertificate @certificateParameters

    Ensure-LocalCertificateTrust -Certificate $certificate
    return $certificate
}

function Ensure-HostsEntries {
    $hostsPath = Join-Path $env:SystemRoot "System32\drivers\etc\hosts"
    $lines = @(Get-Content -LiteralPath $hostsPath -ErrorAction Stop)

    foreach ($hostName in @($apiHostName, $uiHostName)) {
        $foundLocal = $false
        foreach ($line in $lines) {
            $withoutComment = ($line -split "#", 2)[0].Trim()
            if ([string]::IsNullOrWhiteSpace($withoutComment)) {
                continue
            }
            $tokens = @($withoutComment -split "\s+")
            if ($tokens.Count -lt 2 -or -not ($tokens[1..($tokens.Count - 1)] -contains $hostName)) {
                continue
            }
            if ($tokens[0] -notin @("127.0.0.1", "::1")) {
                throw "$hostName is already mapped to $($tokens[0]) in the hosts file."
            }
            $foundLocal = $true
        }
        if (-not $foundLocal) {
            $entry = "127.0.0.1 " + $hostName
            Add-Content -LiteralPath $hostsPath -Value $entry -Encoding ASCII
            $lines += $entry
        }
    }
}

function Ensure-HttpsSite {
    param(
        [string]$Name,
        [string]$PhysicalPath,
        [string]$PoolName,
        [string]$HostName,
        [string]$Thumbprint
    )

    $sitePath = "IIS:\Sites\$Name"
    if (Test-Path $sitePath) {
        $existingPath = [IO.Path]::GetFullPath((Get-Website -Name $Name).PhysicalPath).TrimEnd("\")
        if ($existingPath -ne [IO.Path]::GetFullPath($PhysicalPath).TrimEnd("\")) {
            throw "IIS site '$Name' points to '$existingPath'; it will not be overwritten."
        }
    }
    else {
        New-Website -Name $Name -PhysicalPath $PhysicalPath -ApplicationPool $PoolName -Port 80 -HostHeader ("bootstrap-" + $HostName + ".invalid") | Out-Null
        Stop-Website -Name $Name -ErrorAction SilentlyContinue
    }

    Set-ItemProperty $sitePath -Name applicationPool -Value $PoolName
    Get-WebBinding -Name $Name | Remove-WebBinding
    New-WebBinding -Name $Name -IPAddress "*" -Port 443 -HostHeader $HostName -Protocol "https" -SslFlags 1

    $bindingInformation = "*:443:" + $HostName
    $binding = Get-WebBinding -Name $Name -Protocol "https" | Where-Object {
        $_.BindingInformation -eq $bindingInformation
    } | Select-Object -First 1
    if ($null -eq $binding) {
        throw "The newly created HTTPS binding for $HostName was not found."
    }
    $binding.AddSslCertificate($Thumbprint, "My")
}

function Assert-NoConflictingBindings {
    param(
        [string]$HostName,
        [string]$ExpectedSiteName
    )

    $suffix = ":443:" + $HostName
    $conflicts = @()
    foreach ($site in Get-Website) {
        if ($site.Name -eq $ExpectedSiteName) {
            continue
        }
        $conflicts += @(Get-WebBinding -Name $site.Name | Where-Object {
            $_.Protocol -eq "https" -and
            $_.BindingInformation.EndsWith($suffix, [StringComparison]::OrdinalIgnoreCase)
        })
    }
    if ($conflicts.Count -gt 0) {
        throw ("Host name {0}:443 is already used by another IIS site." -f $HostName)
    }
}

function Test-Deployment {
    $apiUrl = "https://$apiHostName"
    $uiUrl = "https://$uiHostName"
    $lastError = $null
    for ($attempt = 1; $attempt -le 20; $attempt++) {
        try {
            $ready = Invoke-WebRequest -Uri ($apiUrl + "/health/ready") -UseBasicParsing -TimeoutSec 10
            $setupStatus = Invoke-RestMethod -Uri ($apiUrl + "/api/v1/setup/status") -Method Get -TimeoutSec 10
            $ui = Invoke-WebRequest -Uri $uiUrl -UseBasicParsing -TimeoutSec 10

            if ($ready.StatusCode -eq 200 -and $ui.StatusCode -eq 200) {
                Write-Host ("API readiness: {0}; UI: {1}; requiresInitialization: {2}" -f $ready.StatusCode, $ui.StatusCode, $setupStatus.requiresInitialization) -ForegroundColor Green
                return
            }
        }
        catch {
            $lastError = $_
        }
        Start-Sleep -Seconds 2
    }

    throw "Health check did not succeed within 40 seconds. Last error: $($lastError.Exception.Message)"
}

if ($Build) {
    Build-Artifacts
}

Assert-ArtifactLayout
Write-Host ("Artifacts are ready at: " + $ArtifactsPath) -ForegroundColor Green

if ($PrepareOnly) {
    Write-Host "PrepareOnly completed; IIS was not changed." -ForegroundColor Yellow
    return
}

Assert-Administrator
Assert-IisTarget -Path $apiTargetPath -ExpectedLeaf $apiHostName
Assert-IisTarget -Path $uiTargetPath -ExpectedLeaf $uiHostName

Import-Module WebAdministration -ErrorAction Stop
$aspNetCoreModule = Get-WebGlobalModule | Where-Object { $_.Name -eq "AspNetCoreModuleV2" } | Select-Object -First 1
if ($null -eq $aspNetCoreModule) {
    throw "AspNetCoreModuleV2 is missing. Install the .NET 10 Hosting Bundle and try again."
}

if ($null -eq $PostgresPassword) {
    $PostgresPassword = Read-Host "PostgreSQL password cho role $PostgresUsername" -AsSecureString
}
if ($null -eq $JwtSigningKey) {
    $JwtSigningKey = Read-Host "Stable JWT signing key (at least 32 characters)" -AsSecureString
}

$databasePasswordPlainText = ConvertFrom-SecureValue -Value $PostgresPassword
$jwtSigningKeyPlainText = ConvertFrom-SecureValue -Value $JwtSigningKey
try {
    if ([string]::IsNullOrWhiteSpace($databasePasswordPlainText)) {
        throw "PostgreSQL password cannot be empty."
    }
    if ($jwtSigningKeyPlainText.Length -lt 32) {
        throw "JWT signing key must contain at least 32 characters."
    }

    $connectionStringBuilder = New-Object System.Data.Common.DbConnectionStringBuilder
    $connectionStringBuilder["Host"] = $PostgresHost
    $connectionStringBuilder["Port"] = $PostgresPort
    $connectionStringBuilder["Database"] = $PostgresDatabase
    $connectionStringBuilder["Username"] = $PostgresUsername
    $connectionStringBuilder["Password"] = $databasePasswordPlainText
    $connectionStringBuilder["Include Error Detail"] = $false
    $connectionString = $connectionStringBuilder.ConnectionString

    Write-Step "Stop both IIS sites before updating files"
    Stop-ManagedSite -SiteName $apiSiteName -PoolName $apiPoolName
    Stop-ManagedSite -SiteName $uiSiteName -PoolName $uiPoolName

    Write-Step "Deploy artifacts to C:\inetpub"
    Invoke-RobocopyMirror -Source $apiArtifactPath -Destination $apiTargetPath -ExcludedDirectories @("logs")
    Invoke-RobocopyMirror -Source $uiArtifactPath -Destination $uiTargetPath

    New-Item -ItemType Directory -Path (Join-Path $apiTargetPath "logs") -Force | Out-Null
    Write-Step "Configure IIS app pools and directory permissions"
    Ensure-WebAppPool -Name $apiPoolName
    Ensure-WebAppPool -Name $uiPoolName
    Grant-SitePermissions -Path $apiTargetPath -PoolName $apiPoolName
    Grant-SitePermissions -Path $uiTargetPath -PoolName $uiPoolName
    Set-ApiWebConfig -Path (Join-Path $apiTargetPath "web.config") -ConnectionString $connectionString -SigningKey $jwtSigningKeyPlainText
    & icacls.exe (Join-Path $apiTargetPath "logs") /grant:r ("{0}:(OI)(CI)(M)" -f ("IIS AppPool\" + $apiPoolName)) /T /C /Q | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot grant API app pool write access to the logs directory."
    }

    $certificate = Get-OrCreateCertificate
    Write-Host ("Certificate thumbprint: " + $certificate.Thumbprint) -ForegroundColor Green

    Write-Step "Create both HTTPS IIS sites with SNI"
    Assert-NoConflictingBindings -HostName $apiHostName -ExpectedSiteName $apiSiteName
    Assert-NoConflictingBindings -HostName $uiHostName -ExpectedSiteName $uiSiteName
    Ensure-HttpsSite -Name $apiSiteName -PhysicalPath $apiTargetPath -PoolName $apiPoolName -HostName $apiHostName -Thumbprint $certificate.Thumbprint
    Ensure-HttpsSite -Name $uiSiteName -PhysicalPath $uiTargetPath -PoolName $uiPoolName -HostName $uiHostName -Thumbprint $certificate.Thumbprint

    if (-not $SkipHostsFile) {
        Write-Step "Update the Windows hosts file"
        Ensure-HostsEntries
    }

    Start-ManagedSite -SiteName $apiSiteName -PoolName $apiPoolName
    Start-ManagedSite -SiteName $uiSiteName -PoolName $uiPoolName

    if (-not $SkipHealthCheck) {
        Write-Step "Verify HTTPS, PostgreSQL, and the UI"
        Test-Deployment
    }

    Write-Host ""
    Write-Host "IIS deployment completed." -ForegroundColor Green
    Write-Host ("UI : https://" + $uiHostName)
    Write-Host ("API: https://" + $apiHostName)
}
finally {
    $databasePasswordPlainText = $null
    $jwtSigningKeyPlainText = $null
    $connectionString = $null
}
