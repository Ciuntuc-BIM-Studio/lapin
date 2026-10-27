#!/usr/bin/env bash
# The Lapin test VM's media (scripts/windows-vm/README.md), in ~/VMs/lapin-windows:
#   1. windows-noprompt.iso: the Windows 11 ARM ISO given, booting without "Press any key to boot from CD";
#   2. lapin-setup.iso: UTM's guest tools (drivers, SPICE) with our autounattend.xml, lapin-setup.ps1 and the
#      Mac's SSH key; Windows Setup reads the answer file from it.
# The account's password is made once and kept in the keychain (service lapin-windows-vm); it is never printed.
#   scripts/windows-vm/prepare.sh ~/Downloads/Win11_*_English_Arm64.iso
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
VM="${LAPIN_VM_DIR:-$HOME/VMs/lapin-windows}"
ISO="${1:?usage: prepare.sh <Windows 11 ARM64 ISO>}"
mkdir -p "$VM"
command -v xorriso >/dev/null || { echo "brew install xorriso" >&2; exit 1; }
[ -f ~/.ssh/lapin_vm.pub ] || ssh-keygen -q -t ed25519 -N "" -C lapin-windows-vm -f ~/.ssh/lapin_vm
[ -f "$VM/utm-guest-tools.iso" ] || curl -sfL -o "$VM/utm-guest-tools.iso" https://getutm.app/downloads/utm-guest-tools-latest.iso

# The password: the keychain's, else a new random one put there.
if ! PW="$(security find-generic-password -s lapin-windows-vm -a lapin -w 2>/dev/null)"; then
  PW="$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 20)Aa1"
  security add-generic-password -s lapin-windows-vm -a lapin -w "$PW"
fi

# 1. Windows without the boot prompt: the same image, its UEFI boot image swapped for efisys_noprompt.bin.
echo "→ windows-noprompt.iso"
rm -rf "$VM/win" && mkdir -p "$VM/win"
xorriso -osirrox on -indev "$ISO" -extract / "$VM/win" >/dev/null 2>&1
chmod -R u+w "$VM/win"
EFI="efi/microsoft/boot/efisys_noprompt.bin"
[ -f "$VM/win/$EFI" ] || EFI="efi/microsoft/boot/efisys.bin"
xorriso -as mkisofs -iso-level 3 -udf -J -joliet-long -V WIN11_ARM64 \
  -e "$EFI" -no-emul-boot -o "$VM/windows-noprompt.iso" "$VM/win" >/dev/null 2>&1
rm -rf "$VM/win"

# 2. The setup media: the guest tools, our answer file (with the password) and script, the key.
echo "→ lapin-setup.iso"
rm -rf "$VM/setup" && mkdir -p "$VM/setup"
xorriso -osirrox on -indev "$VM/utm-guest-tools.iso" -extract / "$VM/setup" >/dev/null 2>&1
chmod -R u+w "$VM/setup"
rm -f "$VM/setup/Autounattend.xml"
PW="$PW" perl -pe 's/LAPIN_PASSWORD/$ENV{PW}/g' "$HERE/autounattend.xml" > "$VM/setup/autounattend.xml"
cp "$HERE/lapin-setup.ps1" "$VM/setup/"
cp ~/.ssh/lapin_vm.pub "$VM/setup/lapin_vm.pub"
xorriso -as mkisofs -iso-level 3 -J -joliet-long -V LAPIN_SETUP -o "$VM/lapin-setup.iso" "$VM/setup" >/dev/null 2>&1
rm -rf "$VM/setup"
ls -la "$VM"/*.iso
echo "✓ media ready: scripts/windows-vm/create.sh makes the VM"
