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
  PW="$(python3 -c 'import secrets, string; print("".join(secrets.choice(string.ascii_letters + string.digits) for _ in range(20)) + "Aa1")')"
  security add-generic-password -s lapin-windows-vm -a lapin -w "$PW"
fi

# 1. Windows without the boot prompt: a copy of the ISO (an APFS clone: instant, sharing its blocks) whose UEFI
#    boot image, efisys.bin at the El Torito image's place, is overwritten by efisys_noprompt.bin (the same size,
#    the same image without "Press any key"). The rest of the disc is untouched: Microsoft's ISOs are UDF, and an
#    install.wim over 4 GB could not be written back anyway.
echo "→ windows-noprompt.iso"
MNT="$(hdiutil attach -readonly -nobrowse -noverify "$ISO" | awk -F'\t' '/\/Volumes\//{print $NF}' | tail -1)"
trap 'hdiutil detach -quiet "$MNT" 2>/dev/null || true' EXIT
python3 - "$ISO" "$MNT" "$VM/windows-noprompt.iso" <<'PY'
import os, re, shutil, subprocess, sys
iso, mnt, out = sys.argv[1:4]
# The El Torito UEFI image: where it is and how long, as xorriso reports it.
rep = subprocess.run(['xorriso', '-indev', iso, '-report_el_torito', 'plain'], capture_output=True, text=True).stdout
row = re.search(r'El Torito boot img\s*:\s*\d+\s+UEFI\s+\S+\s+\S+\s+\S+\s+\S+\s+(\d+)\s+(\d+)', rep)
if not row: sys.exit('No UEFI boot image in the ISO.')
blocks, lba = int(row.group(1)), int(row.group(2))
with open(iso, 'rb') as f: f.seek(lba * 2048); boot = f.read(blocks * 512)
prompt = open(os.path.join(mnt, 'efi/microsoft/boot/efisys.bin'), 'rb').read()
quiet = open(os.path.join(mnt, 'efi/microsoft/boot/efisys_noprompt.bin'), 'rb').read()
if boot != prompt[:len(boot)] or len(quiet) != len(prompt): sys.exit('The ISO boots from another image than efisys.bin, or efisys_noprompt.bin differs in size: not patched.')
if os.path.exists(out): os.remove(out)
if subprocess.run(['cp', '-c', iso, out]).returncode != 0: shutil.copyfile(iso, out)
with open(out, 'r+b') as f: f.seek(lba * 2048); f.write(quiet[:len(boot)])
print(f'  boot image at block {lba} ({len(boot)} bytes) replaced by efisys_noprompt.bin')
PY
hdiutil detach -quiet "$MNT"; trap - EXIT

# 2. The setup media: the guest tools, our answer file (with the password) and script, the key.
echo "→ lapin-setup.iso"
chmod -R u+w "$VM/setup" 2>/dev/null || true; rm -rf "$VM/setup" && mkdir -p "$VM/setup"
xorriso -osirrox on -indev "$VM/utm-guest-tools.iso" -extract / "$VM/setup" >/dev/null 2>&1
chmod -R u+w "$VM/setup"
rm -f "$VM/setup/Autounattend.xml"
PW="$PW" perl -pe 's/LAPIN_PASSWORD/$ENV{PW}/g' "$HERE/autounattend.xml" > "$VM/setup/autounattend.xml"
cp "$HERE/lapin-setup.ps1" "$VM/setup/"
cp ~/.ssh/lapin_vm.pub "$VM/setup/lapin_vm.pub"
xorriso -as mkisofs -iso-level 3 -J -joliet-long -V LAPIN_SETUP -o "$VM/lapin-setup.iso" "$VM/setup" >/dev/null 2>&1
chmod -R u+w "$VM/setup"; rm -rf "$VM/setup"
ls -la "$VM"/*.iso
echo "✓ media ready: scripts/windows-vm/create.sh makes the VM"
