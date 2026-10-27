#!/usr/bin/env bash
set -euo pipefail

# The web build (the engine in the browser, docs/plan-browser-engine.md) to the
# Hetzner server, beside the old Lapin: build/<id>/web goes to
# <DEPLOY_DIR>/www/web, which the nginx there already serves (static files;
# application/wasm is in its types), at https://lapin.ciuntucbimstudio.ro/web/.
# Nothing on the server changes but that folder. While the old Lapin is under
# maintenance, the server's nginx/default.conf opens /web/ and /api/feedback only.
#
#   deploy/web/deploy.sh [id]          (default: lapin; builds it unless SKIP_BUILD=1)
#
# The user guide goes with it, to <DEPLOY_DIR>/www/docs (https://lapin.ciuntucbimstudio.ro/docs/):
# its pictures retaken from this commit by scripts/user-docs (pnpm docs:user), so
# the guide shows the app being published. SKIP_DOCS=1 leaves the guide as it is.
#
# Careful: the old Lapin's deploy (ifc-lite/deploy/lapin/deploy.sh) syncs www/
# with --delete and would remove www/web; give it --exclude web before using it again.

[[ -f "$(dirname "$0")/.env" ]] && source "$(dirname "$0")/.env"
SERVER="${DEPLOY_SERVER:?Set DEPLOY_SERVER (user@host) in deploy/web/.env, see .env.example}"
REMOTE_DIR="${DEPLOY_DIR:-/opt/lapin}"
ID="${1:-lapin}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
WEB="${ROOT}/build/${ID}/web"
cd "${ROOT}"

# What goes live is a commit that is on GitHub (ALLOW_DIRTY=1 for an emergency).
if [[ "${ALLOW_DIRTY:-0}" != "1" ]]; then
  if [[ -n "$(git status --porcelain --untracked-files=no)" ]]; then echo "✗ Uncommitted changes: commit and push first (or ALLOW_DIRTY=1)."; exit 1; fi
  git fetch -q origin
  if [[ -n "$(git log --oneline '@{u}..HEAD' 2>/dev/null)" ]] || ! git rev-parse -q --verify '@{u}' >/dev/null; then echo "✗ HEAD is not pushed: push first (or ALLOW_DIRTY=1)."; exit 1; fi
fi
SHA="$(git rev-parse --short=12 HEAD)"
if [[ "${SKIP_BUILD:-0}" != "1" ]]; then node scripts/build-web.mjs "${ID}"; fi
[[ -f "${WEB}/index.html" ]] || { echo "No build in ${WEB}"; exit 1; }
node scripts/check-web.mjs "${ID}"
printf '{"commit":"%s","builtAt":"%s"}\n' "${SHA}" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "${WEB}/version.json"

echo "→ ${SHA} to ${SERVER}:${REMOTE_DIR}/www/web"
ssh "${SERVER}" "mkdir -p ${REMOTE_DIR}/www/web"
# The hashed files first, the page last: an open tab never meets a page whose files are not there yet.
rsync -az --exclude index.html "${WEB}/" "${SERVER}:${REMOTE_DIR}/www/web/"
rsync -az "${WEB}/index.html" "${SERVER}:${REMOTE_DIR}/www/web/index.html"
echo "✓ https://lapin.ciuntucbimstudio.ro/web/"

if [[ "${SKIP_DOCS:-0}" != "1" ]]; then
  node scripts/user-docs/run.mjs
  DOCS="${ROOT}/build/user-docs"
  echo "→ the user guide to ${SERVER}:${REMOTE_DIR}/www/docs"
  ssh "${SERVER}" "mkdir -p ${REMOTE_DIR}/www/docs"
  # The pictures first, the pages last; pictures no page shows any more go.
  rsync -az --delete --exclude '*.html' "${DOCS}/" "${SERVER}:${REMOTE_DIR}/www/docs/"
  rsync -az --include '*/' --include '*.html' --exclude '*' "${DOCS}/" "${SERVER}:${REMOTE_DIR}/www/docs/"
  echo "✓ https://lapin.ciuntucbimstudio.ro/docs/"
fi
