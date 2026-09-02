<#
.SYNOPSIS
    Tao / xoa Windows Scheduled Task chay backup Supabase dinh ky (Windows 10/11).

.DESCRIPTION
    Task goi run-backup-cycle.ps1 moi -IntervalMinutes phut (mac dinh doc tu
    backup-config.json, thuong la 15).

    Cau hinh task:
      * MultipleInstances = IgnoreNew   -> lan chay moi bi bo qua neu lan truoc chua xong.
      * StartWhenAvailable = true       -> chay bu neu den gio ma may dang tat/ngu.
      * WakeToRun = false               -> khong tu danh thuc may.
      * DisallowStartIfOnBatteries = false / StopIfGoingOnBatteries = false -> chay ca khi dung pin.
      * ExecutionTimeLimit = 1 gio.
      * LogonType S4U (mac dinh)        -> chay ca khi chua dang nhap, KHONG can luu mat khau.
        Can quyen Administrator de tao. Dung -Interactive neu chi muon chay khi da dang nhap.

.EXAMPLE
    # Xem se tao gi (khong tao that)
    ./register-backup-task.ps1 -WhatIf

.EXAMPLE
    # Tao task (chay PowerShell as Administrator)
    ./register-backup-task.ps1

.EXAMPLE
    ./register-backup-task.ps1 -TaskName "GvPortal Supabase Backup" -IntervalMinutes 15

.EXAMPLE
    # Go task
    ./register-backup-task.ps1 -Remove
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$TaskName = "GvPortal-Supabase-Backup",
    [int]$IntervalMinutes,
    [string]$ConfigFile,
    [switch]$Interactive,
    [switch]$Remove
)

$ErrorActionPreference = "Stop"

function Get-ConfigValue {
    param([hashtable]$Config, [string]$Name, $Default)
    if ($Config.ContainsKey($Name) -and $null -ne $Config[$Name] -and "$($Config[$Name])".Trim().Length -gt 0) {
        return $Config[$Name]
    }
    return $Default
}

if (-not (Get-Command Register-ScheduledTask -ErrorAction SilentlyContinue)) {
    throw "Khong tim thay module ScheduledTasks. Can Windows 8/Server 2012 tro len."
}

# --- Go task ---
if ($Remove) {
    $existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if (-not $existing) {
        Write-Host "Khong co task '$TaskName'."
        return
    }
    if ($PSCmdlet.ShouldProcess($TaskName, "Unregister scheduled task")) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
        Write-Host "Da go task '$TaskName'."
    }
    return
}

# --- Config ---
$config = @{}
$configPath = if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) { $ConfigFile } else { Join-Path $PSScriptRoot "backup-config.json" }
if (Test-Path -LiteralPath $configPath) {
    $raw = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    foreach ($prop in $raw.PSObject.Properties) {
        if (-not $prop.Name.StartsWith("//")) { $config[$prop.Name] = $prop.Value }
    }
}
if (-not $PSBoundParameters.ContainsKey("IntervalMinutes")) {
    $IntervalMinutes = [int](Get-ConfigValue $config "intervalMinutes" 15)
}
if ($IntervalMinutes -lt 1 -or $IntervalMinutes -gt 1440) {
    throw "-IntervalMinutes phai trong khoang 1..1440."
}

$runner = Join-Path $PSScriptRoot "run-backup-cycle.ps1"
if (-not (Test-Path -LiteralPath $runner)) { throw "Khong tim thay $runner" }

$psExe = Join-Path $env:SystemRoot "System32\WindowsPowerShell\v1.0\powershell.exe"
$arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "{0}"' -f $runner

$action = New-ScheduledTaskAction -Execute $psExe -Argument $arguments -WorkingDirectory $PSScriptRoot

# Trigger: bat dau tu phut :00 gan nhat, lap lai vo han
$startAt = (Get-Date).Date.AddHours((Get-Date).Hour)
if ($startAt -lt (Get-Date).AddMinutes(-1)) { $startAt = $startAt.AddHours(1) }
$trigger = New-ScheduledTaskTrigger -Once -At $startAt `
    -RepetitionInterval (New-TimeSpan -Minutes $IntervalMinutes) `
    -RepetitionDuration ([TimeSpan]::FromDays(3650))

$settings = New-ScheduledTaskSettingsSet `
    -MultipleInstances IgnoreNew `
    -StartWhenAvailable `
    -DontStopIfGoingOnBatteries `
    -AllowStartIfOnBatteries `
    -DontStopOnIdleEnd `
    -ExecutionTimeLimit (New-TimeSpan -Hours 1) `
    -RestartCount 2 -RestartInterval (New-TimeSpan -Minutes 5)
$settings.WakeToRun = $false

$user = "$env:USERDOMAIN\$env:USERNAME"
$principal = if ($Interactive) {
    New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
}
else {
    New-ScheduledTaskPrincipal -UserId $user -LogonType S4U -RunLevel Limited
}

$description = "Backup database Supabase moi $IntervalMinutes phut + don dep theo retention. Script: $runner"

Write-Host "Task      : $TaskName"
Write-Host "Chay      : $psExe $arguments"
Write-Host "Chu ky    : moi $IntervalMinutes phut, bat dau $($startAt.ToString('yyyy-MM-dd HH:mm'))"
Write-Host "Tai khoan : $user  (LogonType: $(if ($Interactive) { 'Interactive' } else { 'S4U' }))"

if (-not $PSCmdlet.ShouldProcess($TaskName, "Register scheduled task")) { return }

if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Write-Host "Task da ton tai -> ghi de."
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
}

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
    -Settings $settings -Principal $principal -Description $description | Out-Null

Write-Host "Da tao task '$TaskName'."
Write-Host ""
Write-Host "Kiem tra:"
Write-Host "  Get-ScheduledTask -TaskName '$TaskName' | Get-ScheduledTaskInfo"
Write-Host "  Start-ScheduledTask -TaskName '$TaskName'        # chay thu ngay"
Write-Host "  ./register-backup-task.ps1 -Remove               # go task"
