#!/usr/bin/env bash
set -euo pipefail

# The store service (deploy/store/server) to the server: its files copied to <DEPLOY_DIR>/store, which the lapin-store
# container mounts read-only, then the container restarted and its health checked. The catalogue and the store's pages
# are published separately, from lapin-extensions (node tools/publish.mjs --upload), because they are signed on this
# computer. The first install (the compose service, the secrets, the tunnel's routes) is in README.md.
#
#   deploy/store/deploy.sh [--dry-run]

DRY=0
[[ "${1:-}" == "--dry-run" ]] && DRY=1
HERE="$(cd "$(dirname "$0")" && pwd)"
[[ -f "$HERE/.env" ]] && source "$HERE/.env"
SERVER="${DEPLOY_SERVER:?Set DEPLOY_SERVER (user@host) in deploy/store/.env, see .env.example}"
REMOTE_DIR="${DEPLOY_DIR:-/opt/lapin}"

node --test "$HERE/server/server.test.mjs" >/dev/null || { echo "✗ The store's tests fail: nothing deployed."; exit 1; }

FILES=(server.mjs cli.mjs data.mjs discourse.mjs github.mjs catalogue-lib.mjs public/dashboard.html public/dashboard.js)
if [[ $DRY == 1 ]]; then
  printf 'would copy %s to %s:%s/store/\n' "${FILES[*]}" "$SERVER" "$REMOTE_DIR"
  exit 0
fi
(cd "$HERE/server" && tar -cf - "${FILES[@]}") | ssh "$SERVER" "mkdir -p '$REMOTE_DIR/store' && tar -xf - -C '$REMOTE_DIR/store'"
ssh "$SERVER" "cd '$REMOTE_DIR' && docker compose up -d store && sleep 2 && docker logs --tail 5 lapin-store && curl -fsS http://127.0.0.1:3465/api/health"
echo
echo "✓ The store service is deployed."
