#!/usr/bin/env bash
# A Windows build tested in the Lapin VM (scripts/windows-vm/README.md): the VM started if it is not, the installer
# copied in and installed silently (NSIS /S, for the current user), the installed engine started and driven from
# the Mac through an SSH tunnel (engine-smoke.mjs), then the app opened in the user's session and its window
# captured to build/windows-vm-screenshot.png.
#   scripts/windows-vm/test-build.sh [build/lapin/bundle/nsis/Lapin_<version>_x64-setup.exe]
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
NAME="${LAPIN_VM_NAME:-Lapin Windows}"
SETUP="${1:-$(ls -t "$ROOT"/build/lapin/bundle/nsis/*_x64-setup.exe 2>/dev/null | head -1)}"
[ -f "$SETUP" ] || { echo "No installer: build one (pnpm build:product lapin --target windows) or name it." >&2; exit 1; }
utmctl status "$NAME" 2>/dev/null | grep -q started || { echo "→ starting ${NAME}"; utmctl start "$NAME"; }
"$HERE/wait.sh" >/dev/null
IP="$("$HERE/vm-ip.sh")"
SSH=(ssh -i ~/.ssh/lapin_vm -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o BatchMode=yes -o LogLevel=ERROR "lapin@$IP")
echo "→ $(basename "$SETUP") to the VM ($IP)"
scp -q -i ~/.ssh/lapin_vm -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR "$SETUP" "lapin@$IP:C:/Users/lapin/lapin-setup.exe"
"${SSH[@]}" "Get-Process Lapin,lapin-host -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Process C:\\Users\\lapin\\lapin-setup.exe -ArgumentList '/S' -Wait; Get-Item \$env:LOCALAPPDATA\\Lapin\\Lapin.exe | ForEach-Object { 'installed ' + \$_.VersionInfo.ProductVersion }"

# The engine as the app starts it, on a port of its own, reached through a tunnel.
PORT=47899
"${SSH[@]}" "\$d = \"\$env:LOCALAPPDATA\\Lapin\"; Start-Process \"\$d\\lapin-host.exe\" -ArgumentList '--product',\"\$d\\resources\\product.json\",'--modules-dir',\"\$d\\resources\\modules\",'--user-dir',\"\$env:TEMP\\lapin-smoke\",'--port','$PORT' -WindowStyle Hidden"
ssh -i ~/.ssh/lapin_vm -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o BatchMode=yes -o LogLevel=ERROR -N -L "$PORT:127.0.0.1:$PORT" "lapin@$IP" &
TUNNEL=$!
trap 'kill $TUNNEL 2>/dev/null; "${SSH[@]}" "Get-Process lapin-host -ErrorAction SilentlyContinue | Stop-Process -Force" >/dev/null 2>&1 || true' EXIT
for i in $(seq 1 60); do nc -z 127.0.0.1 $PORT 2>/dev/null && break; sleep 1; done
node "$HERE/engine-smoke.mjs" "$PORT"

# The app itself, in the user's session (a scheduled task: what SSH starts has no desktop), and its window captured.
"${SSH[@]}" "schtasks /create /tn LapinApp /tr \"\$env:LOCALAPPDATA\\Lapin\\Lapin.exe\" /sc once /st 00:00 /it /rl highest /f | Out-Null; schtasks /run /tn LapinApp | Out-Null"
sleep 15
"${SSH[@]}" "schtasks /create /tn LapinShot /tr \"powershell -NoProfile -WindowStyle Hidden -Command Add-Type -AssemblyName System.Windows.Forms,System.Drawing; \`\$b=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds; \`\$i=New-Object System.Drawing.Bitmap \`\$b.Width,\`\$b.Height; [System.Drawing.Graphics]::FromImage(\`\$i).CopyFromScreen(\`\$b.Location,[System.Drawing.Point]::Empty,\`\$b.Size); \`\$i.Save('C:\\Users\\lapin\\lapin-shot.png')\" /sc once /st 00:00 /it /f | Out-Null; schtasks /run /tn LapinShot | Out-Null; Start-Sleep 5; (Get-Process Lapin -ErrorAction SilentlyContinue | Measure-Object).Count"
scp -q -i ~/.ssh/lapin_vm -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR "lapin@$IP:C:/Users/lapin/lapin-shot.png" "$ROOT/build/windows-vm-screenshot.png" && echo "✓ the app's window: build/windows-vm-screenshot.png"
