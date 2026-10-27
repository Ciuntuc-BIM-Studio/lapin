#!/usr/bin/env bash
# The Lapin test VM in UTM (scripts/windows-vm/README.md): Windows 11 ARM, 8 GB, 4 cores, a 64 GB NVMe disk,
# the two media of prepare.sh on USB (Windows first, the setup media second, so it is E: for the drivers), a
# virtio network on UTM's shared network (the Mac reaches the guest at its own address: vm-ip.sh). Started at once: Windows installs by itself
# (20–30 minutes), then lapin-setup.ps1 installs the guest tools and OpenSSH; wait.sh waits for it.
set -euo pipefail
VM="${LAPIN_VM_DIR:-$HOME/VMs/lapin-windows}"
NAME="${LAPIN_VM_NAME:-Lapin Windows}"
for f in windows-noprompt.iso lapin-setup.iso; do [ -f "$VM/$f" ] || { echo "No $VM/$f: run prepare.sh first." >&2; exit 1; }; done
if utmctl list | grep -q "  ${NAME}\$"; then echo "UTM already has a VM named ${NAME}." >&2; exit 1; fi
mac="$(osascript <<OSA
tell application "UTM"
  set winIso to POSIX file "$VM/windows-noprompt.iso"
  set setupIso to POSIX file "$VM/lapin-setup.iso"
  set vm to make new virtual machine with properties {backend:qemu, configuration:{name:"$NAME", notes:"Lapin's Windows test machine (lapin-pro/scripts/windows-vm).", architecture:"aarch64", memory:8192, cpu cores:4, hypervisor:true, uefi:true, drives:{{removable:true, interface:USB, source:winIso}, {removable:true, interface:USB, source:setupIso}, {interface:NVMe, guest size:65536}}, network interfaces:{{mode:shared, hardware:"virtio-net-pci"}}}}
  set c to configuration of vm
  -- A screen: a VM made by script has none (virtio-ramfb needs no driver in Windows).
  set displays of c to {{hardware:"virtio-ramfb"}}
  update configuration of vm with c
  set c to configuration of vm
  set nets to network interfaces of c
  set mac to address of item 1 of nets
  start vm
  return mac
end tell
OSA
)"
echo "$mac" > "$VM/mac.txt"
echo "✓ ${NAME} started: Windows installs by itself; scripts/windows-vm/wait.sh waits for SSH (MAC ${mac})"
