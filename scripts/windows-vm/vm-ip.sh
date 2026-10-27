#!/usr/bin/env bash
# The Lapin test VM's address on UTM's shared network: its MAC (kept by create.sh) found in macOS' DHCP leases,
# else in the ARP table. Prints the IP, or nothing.
VM="${LAPIN_VM_DIR:-$HOME/VMs/lapin-windows}"
[ -f "$VM/mac.txt" ] || exit 0
# The leases write each byte without its leading zero (4e:a:6e:…).
mac="$(tr 'A-F' 'a-f' < "$VM/mac.txt" | sed -E 's/(^|:)0([0-9a-f])/\1\2/g')"
if [ -r /var/db/dhcpd_leases ]; then
  ip="$(awk -v m="1,$mac" '/^{/{ip="";hw=""} /ip_address=/{split($0,a,"=");ip=a[2]} /hw_address=/{split($0,a,"=");hw=a[2]} /^}/{if(hw==m)print ip}' /var/db/dhcpd_leases | tail -1)"
  [ -n "$ip" ] && { echo "$ip"; exit 0; }
fi
arp -an | awk -v m="$mac" '$4==m{gsub(/[()]/,"",$2);print $2}' | head -1
