# The first logon of the Lapin test VM (scripts/windows-vm/README.md): UTM's guest tools (network, display,
# clipboard), OpenSSH Server with the Mac's key, PowerShell as its shell, no sleep and no lock. Logged to C:\lapin-setup.log.
param([string]$Media = 'E:')
Start-Transcript -Path C:\lapin-setup.log -Append
$ErrorActionPreference = 'Continue'

# 1. UTM's guest tools, silently: the virtio network driver comes with them.
$tools = Get-ChildItem "$Media\" -Filter 'utm-guest-tools-*.exe' | Select-Object -First 1
if ($tools) { Start-Process $tools.FullName -ArgumentList '/S' -Wait }

# 2. Never sleep, never lock: the VM is driven from the Mac.
powercfg /change standby-timeout-ac 0
powercfg /change monitor-timeout-ac 0
powercfg /hibernate off
reg add 'HKLM\SOFTWARE\Policies\Microsoft\Windows\Personalization' /v NoLockScreen /t REG_DWORD /d 1 /f

# 3. The network, once the driver is in.
for ($i = 0; $i -lt 60 -and -not (Test-NetConnection -ComputerName 1.1.1.1 -Port 443 -InformationLevel Quiet -WarningAction SilentlyContinue); $i++) { Start-Sleep 5 }

# 4. OpenSSH Server: Windows' own (from Windows Update), else Microsoft's release from GitHub.
$cap = Get-WindowsCapability -Online -Name 'OpenSSH.Server*' | Select-Object -First 1
if ($cap -and $cap.State -ne 'Installed') { Add-WindowsCapability -Online -Name $cap.Name | Out-Null }
if (-not (Get-Service sshd -ErrorAction SilentlyContinue)) {
  $rel = Invoke-RestMethod 'https://api.github.com/repos/PowerShell/Win32-OpenSSH/releases/latest'
  $msi = $rel.assets | Where-Object { $_.name -like 'OpenSSH-ARM64-*.msi' } | Select-Object -First 1
  if ($msi) { Invoke-WebRequest $msi.browser_download_url -OutFile "$env:TEMP\openssh.msi"; Start-Process msiexec -ArgumentList "/i `"$env:TEMP\openssh.msi`" /qn" -Wait }
}
Set-Service sshd -StartupType Automatic
Start-Service sshd
New-ItemProperty -Path 'HKLM:\SOFTWARE\OpenSSH' -Name DefaultShell -Value 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -PropertyType String -Force | Out-Null
if (-not (Get-NetFirewallRule -Name 'lapin-sshd' -ErrorAction SilentlyContinue)) { New-NetFirewallRule -Name 'lapin-sshd' -DisplayName 'OpenSSH (Lapin test VM)' -Direction Inbound -Protocol TCP -LocalPort 22 -Action Allow | Out-Null }

# 5. The Mac's key, for the administrators (the account is one).
$keys = 'C:\ProgramData\ssh\administrators_authorized_keys'
Copy-Item "$Media\lapin_vm.pub" $keys -Force
icacls $keys /inheritance:r /grant 'Administrators:F' /grant 'SYSTEM:F' | Out-Null
Restart-Service sshd

New-Item -ItemType File -Path C:\lapin-setup.done -Force | Out-Null
Stop-Transcript
