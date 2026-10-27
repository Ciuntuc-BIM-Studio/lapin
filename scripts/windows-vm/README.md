# The Windows test machine (UTM on the Mac)

A Windows 11 ARM virtual machine in UTM, installed with no question asked, that tests a Windows build of Lapin made on the Mac (`pnpm build:product lapin --target windows`, docs/release-windows.md). Windows on ARM runs the x64 build by its own emulation.

## Once

1. Tools: `brew install --cask utm` and `brew install xorriso`. The first time a script drives UTM, macOS asks to let the terminal (or VS Code) control UTM: allow it.
2. The Windows 11 ARM64 ISO, by hand, from microsoft.com/software-download/windows11arm64 (multi-edition, English (United States)).
3. The media, then the machine:
   ```sh
   scripts/windows-vm/prepare.sh ~/Downloads/Win11_*_English_Arm64*.iso
   scripts/windows-vm/create.sh
   scripts/windows-vm/wait.sh          # 20–40 minutes: Windows installs and sets itself up
   ```

What happens: `prepare.sh` makes `windows-noprompt.iso` (an APFS clone of the ISO whose UEFI boot image, `efisys.bin`, is overwritten in place by `efisys_noprompt.bin`, the same size: Microsoft's ISOs are UDF with an `install.wim` over 4 GB, so they are patched, not rebuilt) and `lapin-setup.iso` (UTM's guest tools with `autounattend.xml`, `lapin-setup.ps1` and the key `~/.ssh/lapin_vm.pub`) in `~/VMs/lapin-windows`. `create.sh` makes the VM in UTM (8 GB, 4 cores, 64 GB NVMe disk, the two media on USB, UTM's shared network) and starts it. Windows Setup reads `autounattend.xml`: it skips the TPM, Secure Boot and RAM checks, wipes and partitions the disk, installs Windows 11 Pro with Microsoft's published generic key (installed, not activated), makes the local account `lapin` (an administrator, logged on by itself; its password is made once and kept in the keychain, service `lapin-windows-vm`) and asks for no Microsoft account. At the first logon `lapin-setup.ps1` installs UTM's guest tools (the network driver among them), OpenSSH Server with the Mac's key and PowerShell as its shell, and turns off sleep and the lock screen; its log is `C:\lapin-setup.log`. `wait.sh` waits for SSH; `vm-ip.sh` finds the VM's address by its MAC.

Windows unactivated works, with a watermark; activating it is the owner's call.

## Each build

```sh
pnpm build:product lapin --target windows
scripts/windows-vm/test-build.sh
```

It starts the VM if needed, installs the newest `build/lapin/bundle/nsis/*_x64-setup.exe` silently, starts the installed engine and drives it from the Mac through an SSH tunnel (`engine-smoke.mjs`: a wall, a window in it, a slab, the plan, save and open, IFC and GLB exports), then opens the app in the user's session and saves a screenshot to `build/windows-vm-screenshot.png`. Two things Windows does its own way: OpenSSH ends whatever a session started when the session ends (so the engine runs inside an SSH session held open, with the tunnel), and what SSH starts has no desktop (so the app and the screenshot, `lapin-shot.ps1`, run as scheduled tasks in the logged-on session).

First run, 27.10.2026: Windows 11 Pro ARM64 26300; the build 0.1.228 made on the Mac installed, its engine passed `engine-smoke.mjs` with the same results as on the Mac (the wall with its window 4 parts and 180 triangles, the IFC 54,437 bytes), and the app opened on its start screen with its engine connected.

## By hand

```sh
ssh -i ~/.ssh/lapin_vm lapin@$(scripts/windows-vm/vm-ip.sh)      # PowerShell in the VM
utmctl stop "Lapin Windows"
```
