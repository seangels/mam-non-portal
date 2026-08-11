[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK is required to create the localhost HTTPS development certificate."
}

$uiRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$certificateDirectory = Join-Path $uiRoot ".certs"
$certificatePath = Join-Path $certificateDirectory "localhost.pem"
$privateKeyPath = Join-Path $certificateDirectory "localhost.key"

New-Item -ItemType Directory -Path $certificateDirectory -Force | Out-Null

& dotnet dev-certs https --check --trust | Out-Host
if ($LASTEXITCODE -ne 0) {
    Write-Host "Creating and trusting the localhost HTTPS development certificate..."
    & dotnet dev-certs https --trust
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to create and trust the localhost HTTPS development certificate."
    }
}

# Always re-export so the PEM files match the certificate currently trusted by
# the user running the UI. This also handles certificate renewal transparently.
Remove-Item -LiteralPath $certificatePath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $privateKeyPath -Force -ErrorAction SilentlyContinue

& dotnet dev-certs https `
    --export-path $certificatePath `
    --format Pem `
    --no-password

if ($LASTEXITCODE -ne 0 -or
    -not (Test-Path -LiteralPath $certificatePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $privateKeyPath -PathType Leaf)) {
    throw "Unable to export the localhost HTTPS certificate and private key."
}

Write-Host "HTTPS certificate ready for https://localhost:4200"
