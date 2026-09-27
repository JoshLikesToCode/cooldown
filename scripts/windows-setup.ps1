# One-time setup for the Windows test PC. Run in an admin PowerShell:
#   powershell -ExecutionPolicy Bypass -File windows-setup.ps1
param([string]$InstallDir = "C:\Tools\Cooldown")
$ErrorActionPreference = "Stop"

# 1. SSH server, so deploy.sh can reach this PC.
$ssh = Get-WindowsCapability -Online -Name "OpenSSH.Server*"
if ($ssh.State -ne "Installed") {
    Add-WindowsCapability -Online -Name $ssh.Name | Out-Null
}
Set-Service sshd -StartupType Automatic
Start-Service sshd

# 2. Install folder.
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

# 3. A task that starts Cooldown in your desktop session when deploy.sh asks it to.
# Resolved as a SID, not "domain\username": Register-ScheduledTask fails for Entra ID / Azure AD
# accounts with "No mapping between account names and security IDs was done" otherwise.
$sid = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value
$action = New-ScheduledTaskAction -Execute (Join-Path $InstallDir "Cooldown.exe")
$principal = New-ScheduledTaskPrincipal -UserId $sid -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName "CooldownDev" -Action $action -Principal $principal -Force | Out-Null

Write-Host "Ready. SSH is running and the CooldownDev task points at $InstallDir\Cooldown.exe."
Write-Host "If your account is an admin, put your public key in C:\ProgramData\ssh\administrators_authorized_keys."
