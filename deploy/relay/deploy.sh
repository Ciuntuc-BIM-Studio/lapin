#!/usr/bin/env bash
set -euo pipefail

# The relay of shared sessions (docs/plan-multiplayer.md) to the Hetzner server,
# beside lapin-web: engine/src/Relay/Lapin.Relay published self-contained for
# Alpine (linux-musl-x64, one file) into <DEPLOY_DIR>/relay, run by the
# lapin-relay container (README.md), which nginx serves at
# wss://lapin.ciuntucbimstudio.ro/relay/. The relay pairs hosts and guests and
# moves their frames, encrypted end to end; it keeps nothing.
#
#   deploy/relay/deploy.sh
#
# The server is named in deploy/web/.env (DEPLOY_SERVER=root@…), git-ignored.

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
ENV="$ROOT/deploy/web/.env"
[[ -f "$ENV" ]] || { echo "✗ No $ENV (DEPLOY_SERVER=root@…)."; exit 1; }
# shellcheck disable=SC1090
source "$ENV"
: "${DEPLOY_SERVER:?DEPLOY_SERVER is not set in $ENV}"
DEPLOY_DIR="${DEPLOY_DIR:-/opt/lapin}"
OUT="$ROOT/build/relay"

echo "▸ publishing the relay (linux-musl-x64, self-contained)"
rm -rf "$OUT"
dotnet publish "$ROOT/engine/src/Relay/Lapin.Relay/Lapin.Relay.csproj" -c Release -r linux-musl-x64 --self-contained \
  -p:PublishSingleFile=true -p:DebugType=none -o "$OUT" -v q -nologo
echo "→ $(du -h "$OUT/Lapin.Relay" | cut -f1) to ${DEPLOY_DIR}/relay"
ssh "$DEPLOY_SERVER" "mkdir -p ${DEPLOY_DIR}/relay"
rsync -az "$OUT/Lapin.Relay" "${DEPLOY_SERVER}:${DEPLOY_DIR}/relay/Lapin.Relay.new"
ssh "$DEPLOY_SERVER" "cd ${DEPLOY_DIR} && mv relay/Lapin.Relay.new relay/Lapin.Relay && chmod +x relay/Lapin.Relay && docker compose up -d relay >/dev/null && docker restart lapin-relay >/dev/null"
for i in $(seq 1 20); do
  if curl -fsS https://lapin.ciuntucbimstudio.ro/relay/health >/dev/null 2>&1; then echo "✓ https://lapin.ciuntucbimstudio.ro/relay/health"; exit 0; fi
  sleep 1
done
echo "✗ The relay does not answer at /relay/health (README.md: the container and nginx's location)."; exit 1
