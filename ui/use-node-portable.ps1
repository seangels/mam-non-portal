#Requires -Version 5.1
<#
.SYNOPSIS
  Kich hoat Node portable pinned cho ui/ trong session hien tai (khong can admin).

.DESCRIPTION
  Prepend ui/.tools/node-v14.21.3-win-x64 vao $env:PATH cua session, roi verify
  Node v14.21.3 + npm 8.19.4 (khop ui/package.json engines va preinstall guard).

  Cach dung (tu workspace root):
    . .\ui\use-node-portable.ps1
    node --version  # v14.21.3
    npm --version   # 8.19.4

  Cach dung (tu thu muc ui/):
    . .\use-node-portable.ps1
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ExpectedNode = '14.21.3'
$ExpectedNpm = '8.19.4'
$PortableDirName = 'node-v14.21.3-win-x64'

$UiDir = Split-Path -Parent $PSCommandPath
$PortableDir = Join-Path $UiDir (Join-Path '.tools' $PortableDirName)
$NodeExe = Join-Path $PortableDir 'node.exe'
$NpmCmd = Join-Path $PortableDir 'npm.cmd'

if (-not (Test-Path -LiteralPath $NodeExe)) {
  throw "Khong thay $NodeExe. Giai nen https://nodejs.org/dist/v14.21.3/node-v14.21.3-win-x64.zip (verify SHA256) vao $PortableDir truoc."
}
if (-not (Test-Path -LiteralPath $NpmCmd)) {
  throw "Khong thay $NpmCmd. Giai nen chua day du; xoa $PortableDir va giai nen lai."
}

$PathParts = $env:PATH -split ';'
if ($PathParts[0] -ne $PortableDir) {
  $env:PATH = "$PortableDir;$env:PATH"
}

$ActualNode = (& $NodeExe --version).Trim()
$ActualNpm = (& $NpmCmd --version).Trim()

if ($ActualNode -ne "v$ExpectedNode") {
  throw "Node portable sai phien ban: $ActualNode (can v$ExpectedNode) tai $NodeExe."
}
if ($ActualNpm -ne $ExpectedNpm) {
  throw "npm portable sai phien ban: $ActualNpm (can $ExpectedNpm). Xem ui/README.md muc Node portable de cai lai."
}

Write-Output "Node portable OK: $ActualNode + npm $ActualNpm ($PortableDir, session PATH)."
