# The feedback service

The server side of Help → Report an issue and Help → Messages (decision 177 in `docs/decisions-pending.md`). It is a small Node 22 service with no dependencies. It runs on the Hetzner server as the `lapin-feedback` container, behind the `lapin-web` nginx and the Cloudflare tunnel. It was written for Lapin in the old fork (`deploy/lapin/feedback/`, listed in `lapin-own-files.txt`) and rewritten here under MIT. From now on it is maintained here; the old repository's copy is no longer used.

| File | What it is |
|---|---|
| `server.mjs` | the HTTP service (`node server.mjs`) |
| `cli.mjs` | the team's commands, run in the container by `../reports.sh` |
| `store.mjs` | the report files, the lock and atomic writes, shared by both |
| `server.test.mjs` | the tests: `node --test deploy/feedback/server/server.test.mjs` |

## The API

All bodies are JSON. Secrets travel only in POST bodies, never in a URL, so they never reach a log.

| Endpoint | Body | Answer |
|---|---|---|
| `POST /api/feedback` | a report, as before; optional `contact: { nickname?, keyId }` | `201 { number }` |
| `POST /api/feedback/inbox` | `{ secret }` | `{ reports: [{ number, title, kind, receivedAt, status, description, messages, seen, unread }], unread, nickname }` |
| `POST /api/feedback/seen` | `{ secret, report, upTo }` | `{ ok, seen }`: the first `upTo` messages have been read |
| `POST /api/feedback/message` | `{ secret, report, text }` (at most 4000 characters) | `{ ok, status }`: a closed report is reopened |
| `POST /api/feedback/nickname` | `{ secret, nickname }` (at most 40 characters) | `{ ok, updated }` |
| `POST /api/feedback/forget` | `{ secret }` | `{ ok, forgotten }` |
| `GET /api/feedback/health` | | `{ ok }` |

- **The key.** `keyId` is the SHA-256, in hex, of the secret's text: 32 random bytes in base64url, 43 characters, made by the app. The server compares `sha256(secret)` with the `keyId` it stored and never keeps the secret itself.
- **A wrong secret** gets an empty inbox. For `seen` and `message` it gets a 404, the same as for a report that does not exist.
- **Old clients** keep working: a report without `contact` is stored exactly as before.
- **Limits.**
  - Reports: 4 MB per body, 6 per visitor per hour.
  - The other endpoints: 16 KB per body. Messages and nickname changes: 30 per key per hour (`MESSAGE_LIMIT`). A thread holds at most 200 messages.
  - nginx limits come before all of these (see below).

## Storage

The files are the same as before: `/data/open/<n>.json` and `/data/closed/<n>.json`, with the screenshot beside each. The changes:

- **The e-mail** of a new report is stored as `email`. Older files keep it as `contact` (a string), and the CLI reads both.
- **A report that asked for a reply** has a `thread`:
  ```json
  "thread": { "nickname": "Ana", "keyId": "<64 hex>", "messages": [{ "from": "lapin", "text": "…", "at": "…" }],
              "userSeen": 1, "lapinSeen": 2, "lastActivity": "…" }
  ```
  `userSeen` and `lapinSeen` count the messages each side has read.
- **A forgotten thread** becomes `{ "forgotten": "<date>", "messages": [...] }`. The report and the text of its messages stay. Nothing ties them to a person any more, and they can no longer be answered. "Forget me" does this to every report of the key. So does `prune`, and the server's daily housekeeping, for threads idle for more than 12 months (`REPLY_MONTHS`).
- **Writes.** Every write is atomic (a temporary file renamed over the old one). Every change happens under a lock (the directory `/data/.lock`), so the server and `docker exec … cli.mjs` never overwrite each other.
- **Closed reports** are still deleted after `RETENTION_DAYS` (365).

## The CLI (`../reports.sh`)

```sh
./reports.sh                    # the open ones: ✉ <nickname> when the report accepts replies, ● when the sender wrote and it is unread
./reports.sh list all
./reports.sh show 18            # the report and its thread (the sender's messages are then marked read)
./reports.sh reply 18 "Fixed in 0.1.950: update from Help → Check for updates."
./reports.sh close 18           # or reopen 18
./reports.sh prune              # anonymise the threads idle for more than 12 months (prune 6: six months)
```

`reply` refuses a report that did not ask for a reply. If the report has an e-mail, `reply` prints it instead.

## Deploying (the server's `/opt/lapin`)

The compose file and the nginx configuration on the server stay where they are. Only the service's files and the nginx locations change.

1. **Copy the service's files** to the folder the container mounts (`./feedback:/srv:ro`):
   ```sh
   scp deploy/feedback/server/{server,cli,store}.mjs "$DEPLOY_SERVER:/opt/lapin/feedback/"
   ```
   The compose service itself does not change:
   ```yaml
   feedback:
     image: node:22-alpine
     container_name: lapin-feedback
     restart: unless-stopped
     working_dir: /srv
     command: ["node", "server.mjs"]
     environment:
       - FEEDBACK_SALT=${FEEDBACK_SALT:-}   # optional: keeps one visitor's hash stable across restarts
     volumes:
       - ./feedback:/srv:ro
       - ./feedback-data:/data
   ```
   (The `environment` lines are optional. Without them the service behaves as today.)
2. **Restart the service:**
   ```sh
   ssh "$DEPLOY_SERVER" 'cd /opt/lapin && docker compose restart feedback && sleep 1 && docker logs --tail 3 lapin-feedback'
   ```
3. **Update nginx.** Add the zone next to the existing `limit_req_zone` lines in `/opt/lapin/nginx/default.conf`, and the location next to `location /api/feedback`:
   ```nginx
   # Replies (Help → Messages): the app polls the inbox every 15 minutes, a person reads and answers.
   limit_req_zone $http_cf_connecting_ip zone=lapin_feedback_inbox:1m rate=30r/m;
   ```
   ```nginx
   # The replies' endpoints: small bodies, their own limit (a regex location wins over the /api/feedback prefix).
   location ~ ^/api/feedback/(inbox|seen|message|nickname|forget|health)$ {
       include /etc/nginx/conf.d/headers.inc;
       limit_req zone=lapin_feedback_inbox burst=10 nodelay;
       limit_except GET POST { deny all; }
       client_max_body_size 16k;
       access_log off;   # nothing secret is in the URL, but the polling need not fill the log
       resolver 127.0.0.11 valid=30s ipv6=off;
       set $feedback http://lapin-feedback:8080;
       proxy_pass $feedback;
       proxy_set_header Host $host;
       proxy_set_header X-Real-IP $remote_addr;
       proxy_set_header CF-Connecting-IP $http_cf_connecting_ip;
   }
   ```
   The existing submit location stays as it is (`zone=lapin_feedback`, 10r/m burst 5, 5m bodies). It can be made exact with `location = /api/feedback`, but it does not need to be.
4. **Check and reload nginx:**
   ```sh
   ssh "$DEPLOY_SERVER" 'docker exec lapin-web nginx -t && docker exec lapin-web nginx -s reload'
   ```
5. **Check it works:**
   ```sh
   curl -s https://lapin.ciuntucbimstudio.ro/api/feedback/health                                   # {"ok":true}
   curl -s -X POST -H 'content-type: application/json' -d '{"secret":"'"$(openssl rand -base64 32 | tr '+/' '-_' | tr -d '=')"'"}' \
        https://lapin.ciuntucbimstudio.ro/api/feedback/inbox                                         # {"reports":[],"unread":0,"nickname":""}
   ./deploy/feedback/reports.sh list
   ```

The full section, as it would read in `default.conf`:

```nginx
limit_req_zone $http_cf_connecting_ip zone=lapin_feedback:1m rate=10r/m;
limit_req_zone $http_cf_connecting_ip zone=lapin_feedback_inbox:1m rate=30r/m;
limit_req_status 429;

server {
    # …

    # The in-app issue form: a report, with its screenshot.
    location = /api/feedback {
        include /etc/nginx/conf.d/headers.inc;
        limit_req zone=lapin_feedback burst=5 nodelay;
        limit_except POST { deny all; }
        client_max_body_size 5m;
        resolver 127.0.0.11 valid=30s ipv6=off;
        set $feedback http://lapin-feedback:8080;
        proxy_pass $feedback;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header CF-Connecting-IP $http_cf_connecting_ip;
    }

    # Replies: the inbox polled, read markers, answers, nickname, forget me; the health check.
    location ~ ^/api/feedback/(inbox|seen|message|nickname|forget|health)$ {
        include /etc/nginx/conf.d/headers.inc;
        limit_req zone=lapin_feedback_inbox burst=10 nodelay;
        limit_except GET POST { deny all; }
        client_max_body_size 16k;
        access_log off;
        resolver 127.0.0.11 valid=30s ipv6=off;
        set $feedback http://lapin-feedback:8080;
        proxy_pass $feedback;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header CF-Connecting-IP $http_cf_connecting_ip;
    }

    # …
}
```

**Rolling back:** copy the old fork's `server.mjs` and `cli.mjs` back and restart. The reports written in the meantime stay readable by them; they ignore `thread` and `email`.
