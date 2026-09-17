#Requires -Version 5.1
<#
.SYNOPSIS
  Proxy moi lenh npm cua ui/ tu workspace root, tu kich hoat Node portable.

.DESCRIPTION
  Resolve ui/.tools/node-v14.21.3-win-x64, prepend vao PATH cua process hien
  tai (de ca npm lan cac tien trinh con nhu ng deu dung dung ban), roi forward
  toan bo args sang `npm --prefix ui`.

  Cach dung (tu workspace root, co the bo chu `run`):
    npm run ui -- start
    npm run ui -- test:ci
    npm run ui -- build -- --configuration development
    npm run ui -- ci
#>
# Co y KHONG dung [CmdletBinding()]/param: PowerShell 5.1 bind sai bare-word
# dung truoc `--` (vd `exec -- node --version`) khi script co
# ValueFromRemainingArguments. $args giu nguyen verbatim, ke ca `--`.
$NpmArgs = @($args)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$portableDir = Join-Path $repoRoot "ui\.tools\node-v14.21.3-win-x64"
$portableNpm = Join-Path $portableDir "npm.cmd"
$portableNode = Join-Path $portableDir "node.exe"

if ($NpmArgs.Count -eq 0) {
    Write-Host "Dung: npm run ui -- <lenh npm cua ui/>. Vi du:" -ForegroundColor Yellow
    Write-Host "  npm run ui -- start"
    Write-Host "  npm run ui -- run test:ci"
    Write-Host "  npm run ui -- run build -- --configuration development"
    exit 1
}

if (Test-Path -LiteralPath $portableNpm -PathType Leaf) {
    $nodeVersion = (& $portableNode --version).Trim()
    $npmVersion = (& $portableNpm --version).Trim()
    if ($nodeVersion -ne "v14.21.3" -or $npmVersion -ne "8.19.4") {
        throw "Node portable sai phien ban: $nodeVersion + npm $npmVersion (can v14.21.3 + 8.19.4). Cai lai theo ui/README.md muc Node portable."
    }
    $pathParts = $env:PATH -split ';'
    if ($pathParts[0] -ne $portableDir) {
        $env:PATH = "$portableDir;$env:PATH"
    }
}
else {
    Write-Host "Khong thay Node portable ($portableNpm); dung npm tren PATH. Cai portable theo ui/README.md muc Node portable." -ForegroundColor Yellow
}

$uiDir = Join-Path $repoRoot "ui"

# Cho phep bo chu `run`: `npm run ui -- build` == `npm run ui -- run build`.
# Cac lenh builtin/flag cua npm giu nguyen; con lai coi nhu ten script.
$bareCommands = @('install', 'ci', 'test', 'start', 'stop', 'restart', 'version', 'publish', 'pack', 'exec', 'ls', 'run', 'help')
if ($NpmArgs[0] -notmatch '^-' -and $bareCommands -notcontains $NpmArgs[0]) {
    $NpmArgs = @('run') + $NpmArgs
}

& npm.cmd --prefix $uiDir @NpmArgs
exit $LASTEXITCODE
