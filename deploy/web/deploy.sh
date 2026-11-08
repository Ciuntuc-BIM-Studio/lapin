#!/usr/bin/env bash
set -euo pipefail

# The web build (the engine in the browser, docs/plan-browser-engine.md) to the
# Hetzner server, beside the old Lapin: build/<id>/web goes to
# <DEPLOY_DIR>/www/web, which the nginx there already serves (static files;
# application/wasm is in its types), at https://lapin.ciuntucbimstudio.ro/web/.
# Nothing on the server changes but that folder. While the old Lapin is under
# maintenance, the server's nginx/default.conf opens /web/ and /api/feedback only.
#
#   deploy/web/deploy.sh [id] [--app-only | --docs-only] [--full] [--jobs N] [--dry-run]
#
#   id            the edition (default: lapin); the app is built unless SKIP_BUILD=1
#   (nothing)     the app first, live in minutes, then the user guide
#   --app-only    the app alone (SKIP_DOCS=1 says the same)
#   --docs-only   the guide alone, the app on the server left as it is
#   --full        the guide's pictures all taken again (else only the scenes whose inputs changed,
#                 and a full run anyway once a week: scripts/user-docs/run.mjs)
#   --jobs N      how many scenes at once (default: run.mjs's, by cores and memory)
#   --dry-run     everything but the server: the uploads printed, not made
#
# The user guide goes to <DEPLOY_DIR>/www/docs (https://lapin.ciuntucbimstudio.ro/docs/):
# its pictures retaken from this commit by scripts/user-docs (pnpm docs:user), so
# the guide shows the app being published. The app is live before the guide's
# pictures start; a guide that fails leaves the app as published and the old guide
# on the server, and the script exits non-zero.
#
# Careful: the old Lapin's deploy (ifc-lite/deploy/lapin/deploy.sh) syncs www/
# with --delete and would remove www/web; give it --exclude web before using it again.

ID="lapin"; APP=1; DOCS=1; DRY=0; DOC_ARGS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --app-only) DOCS=0 ;;
    --docs-only) APP=0 ;;
    --full) DOC_ARGS+=(--full) ;;
    --jobs) DOC_ARGS+=(--jobs "$2"); shift ;;
    --dry-run) DRY=1 ;;
    -*) echo "✗ Unknown option $1"; exit 1 ;;
    *) ID="$1" ;;
  esac
  shift
done
[[ "${SKIP_DOCS:-0}" == "1" ]] && DOCS=0
(( APP || DOCS )) || { echo "✗ --app-only and --docs-only together leave nothing to do."; exit 1; }

[[ -f "$(dirname "$0")/.env" ]] && source "$(dirname "$0")/.env"
if (( DRY )); then SERVER="${DEPLOY_SERVER:-user@server}"; else SERVER="${DEPLOY_SERVER:?Set DEPLOY_SERVER (user@host) in deploy/web/.env, see .env.example}"; fi
REMOTE_DIR="${DEPLOY_DIR:-/opt/lapin}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
WEB="${ROOT}/build/${ID}/web"
cd "${ROOT}"

# What reaches the server, or (--dry-run) what would.
remote() { if (( DRY )); then echo "  (dry run) $*"; else "$@"; fi; }

# What goes live is a commit that is on GitHub (ALLOW_DIRTY=1 for an emergency; a dry run checks nothing).
if [[ "${ALLOW_DIRTY:-0}" != "1" ]] && (( ! DRY )); then
  if [[ -n "$(git status --porcelain --untracked-files=no)" ]]; then echo "✗ Uncommitted changes: commit and push first (or ALLOW_DIRTY=1)."; exit 1; fi
  git fetch -q origin
  if [[ -n "$(git log --oneline '@{u}..HEAD' 2>/dev/null)" ]] || ! git rev-parse -q --verify '@{u}' >/dev/null; then echo "✗ HEAD is not pushed: push first (or ALLOW_DIRTY=1)."; exit 1; fi
fi
SHA="$(git rev-parse --short=12 HEAD)"

if (( APP )); then
  T0=$SECONDS
  if [[ "${SKIP_BUILD:-0}" != "1" ]]; then node scripts/build-web.mjs "${ID}"; fi
  [[ -f "${WEB}/index.html" ]] || { echo "No build in ${WEB}"; exit 1; }
  node scripts/check-web.mjs "${ID}"
  printf '{"commit":"%s","builtAt":"%s"}\n' "${SHA}" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "${WEB}/version.json"

  echo "→ ${SHA} to ${SERVER}:${REMOTE_DIR}/www/web"
  remote ssh "${SERVER}" "mkdir -p ${REMOTE_DIR}/www/web"
  # The hashed files first, the page last: an open tab never meets a page whose files are not there yet.
  remote rsync -az --exclude index.html "${WEB}/" "${SERVER}:${REMOTE_DIR}/www/web/"
  remote rsync -az "${WEB}/index.html" "${SERVER}:${REMOTE_DIR}/www/web/index.html"
  echo "✓ https://lapin.ciuntucbimstudio.ro/web/ ($(( SECONDS - T0 )) s)"
fi

if (( DOCS )); then
  T0=$SECONDS
  DOCS_DIR="${ROOT}/build/user-docs"
  # The guide on its own: whatever fails here, the app above stays published.
  if ! node scripts/user-docs/run.mjs ${DOC_ARGS[@]+"${DOC_ARGS[@]}"}; then
    echo "✗ The user guide failed (above): the server keeps the guide it had$( (( APP )) && echo ', the app is published' )."
    exit 1
  fi
  echo "→ the user guide to ${SERVER}:${REMOTE_DIR}/www/docs"
  remote ssh "${SERVER}" "mkdir -p ${REMOTE_DIR}/www/docs"
  # The pictures first, the pages last; pictures no page shows any more go.
  remote rsync -az --delete --exclude '*.html' "${DOCS_DIR}/" "${SERVER}:${REMOTE_DIR}/www/docs/"
  remote rsync -az --include '*/' --include '*.html' --exclude '*' "${DOCS_DIR}/" "${SERVER}:${REMOTE_DIR}/www/docs/"
  echo "✓ https://lapin.ciuntucbimstudio.ro/docs/ ($(( SECONDS - T0 )) s)"
fi
