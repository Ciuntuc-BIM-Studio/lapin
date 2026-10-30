#!/usr/bin/env bash
set -euo pipefail

# The reports sent from Lapin's "Report an issue" form, kept on the server
# (/opt/lapin/feedback-data), read from here over SSH:
#   ./reports.sh                      the open ones (✉ <nickname>: replies accepted;
#                                     ●: the sender wrote and it is unread)
#   ./reports.sh list closed|all
#   ./reports.sh show <n>             the whole report and its thread; its screenshot
#                                     is copied to ./feedback-screenshots/
#   ./reports.sh reply <n> "<text>"   a reply, shown in the sender's Help → Messages
#   ./reports.sh close <n> | reopen <n>
#   ./reports.sh prune [months]       anonymise threads idle that long (default 12)
# The service itself: server/ (server/README.md).

# The server comes from deploy/feedback/.env (not committed; see .env.example) or the environment.
[[ -f "$(dirname "$0")/.env" ]] && source "$(dirname "$0")/.env"
SERVER="${DEPLOY_SERVER:?Set DEPLOY_SERVER (user@host) in deploy/feedback/.env, see .env.example}"
REMOTE_DIR="${DEPLOY_DIR:-/opt/lapin}"
[[ $# -eq 0 ]] && set -- list
cmd="$1"
out="$(ssh "${SERVER}" "docker exec lapin-feedback node cli.mjs $(printf '%q ' "$@")")"
echo "${out}"
if [[ "${cmd}" == "show" ]]; then
  shot="$(sed -n 's#^screenshot: /data/##p' <<<"${out}")"
  if [[ -n "${shot}" ]]; then
    mkdir -p feedback-screenshots
    scp -q "${SERVER}:${REMOTE_DIR}/feedback-data/${shot}" feedback-screenshots/
    echo "→ feedback-screenshots/$(basename "${shot}")"
  fi
fi
