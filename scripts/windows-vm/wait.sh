#!/usr/bin/env bash
# Waits for the Lapin test VM's SSH (its setup done), up to an hour; then prints what Windows it is.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
for i in $(seq 1 360); do
  ip="$("$HERE/vm-ip.sh")"
  [ -n "$ip" ] || { sleep 10; continue; }
  SSH=(ssh -i ~/.ssh/lapin_vm -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o ConnectTimeout=5 -o BatchMode=yes -o LogLevel=ERROR "lapin@$ip")
  if "${SSH[@]}" "Test-Path C:\\lapin-setup.done" 2>/dev/null | grep -q True; then
    "${SSH[@]}" "(Get-CimInstance Win32_OperatingSystem).Caption + ' ' + (Get-CimInstance Win32_OperatingSystem).Version + ', ' + \$env:PROCESSOR_ARCHITECTURE"
    echo "$ip" > "${LAPIN_VM_DIR:-$HOME/VMs/lapin-windows}/ip.txt"
    echo "✓ the VM answers on SSH at $ip"; exit 0
  fi
  sleep 10
done
echo "The VM did not answer in an hour: look at its window in UTM, and C:\\lapin-setup.log inside." >&2; exit 1
