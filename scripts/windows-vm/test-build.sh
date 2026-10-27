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
"${SSH[@]}" "Get-Process Lapin,lapin-host -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Process C:\\Users\\lapin\\lapin-setup.exe -ArgumentList '/S' -Wait; (Get-ItemProperty HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\* | Where-Object DisplayName -eq 'Lapin' | ForEach-Object { 'installed Lapin ' + \$_.DisplayVersion + ' in ' + \$_.InstallLocation })"

# The engine as the app starts it, on a port of its own, inside an SSH session held open with a tunnel to it
# (Windows' OpenSSH ends what a session started when the session ends, so the engine lives as long as this one).
PORT=47899
LOG="$(mktemp)"
ssh -i ~/.ssh/lapin_vm -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o BatchMode=yes -o LogLevel=ERROR -L "$PORT:127.0.0.1:$PORT" "lapin@$IP" \
  "\$d = \"\$env:LOCALAPPDATA\\Lapin\"; & \"\$d\\lapin-host.exe\" --product \"\$d\\resources\\product.json\" --modules-dir \"\$d\\resources\\modules\" --user-dir \"\$env:TEMP\\lapin-smoke\" --port $PORT" > "$LOG" 2>&1 &
ENGINE=$!
trap 'kill $ENGINE 2>/dev/null; rm -f "$LOG"' EXIT
for i in $(seq 1 90); do grep -q LAPIN_HOST_READY "$LOG" && break; kill -0 $ENGINE 2>/dev/null || break; sleep 1; done
grep -q LAPIN_HOST_READY "$LOG" || { echo "The Windows engine did not start:" >&2; cat "$LOG" >&2; exit 1; }
node "$HERE/engine-smoke.mjs" "$PORT"

# The app itself, in the user's session (a scheduled task: what SSH starts has no desktop), and the screen captured.
scp -q -i ~/.ssh/lapin_vm -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR "$HERE/lapin-shot.ps1" "lapin@$IP:C:/Users/lapin/lapin-shot.ps1"
"${SSH[@]}" "Get-Process lapin-desktop,Lapin -ErrorAction SilentlyContinue | Stop-Process -Force; \$app = Get-ChildItem \$env:LOCALAPPDATA\\Lapin -Filter *.exe | Where-Object { \$_.Name -notin 'lapin-host.exe','uninstall.exe' } | Select-Object -First 1; schtasks /create /tn LapinApp /tr \"\$(\$app.FullName)\" /sc once /st 23:59 /it /f | Out-Null; schtasks /run /tn LapinApp | Out-Null; \$app.BaseName"
sleep 20
"${SSH[@]}" "schtasks /create /tn LapinShot /tr 'powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File C:\\Users\\lapin\\lapin-shot.ps1' /sc once /st 23:59 /it /f | Out-Null; schtasks /run /tn LapinShot | Out-Null; Start-Sleep 6; \$p = Get-Process lapin-desktop,Lapin -ErrorAction SilentlyContinue; 'app processes: ' + (\$p | Measure-Object).Count + ', engine processes: ' + (Get-Process lapin-host -ErrorAction SilentlyContinue | Measure-Object).Count"
scp -q -i ~/.ssh/lapin_vm -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR "lapin@$IP:C:/Users/lapin/lapin-shot.png" "$ROOT/build/windows-vm-screenshot.png" && echo "✓ the VM's screen: build/windows-vm-screenshot.png"
