# The store, store.lapinbim.com

Lapin Extensions' stage 1 (docs/plan-extensions.md, decision 504): the catalogue's web pages, sign-in with a forum.lapinbim.com account, the author dashboard (submit an extension or a new version, follow its review, see its downloads), download counts, and the hosting of the catalogue itself (lapinbim.com/extensions/). It is a small Node 22 service with **no dependencies**, run on the Hetzner server as the `lapin-store` container, like `lapin-feedback`.

| File | What it is |
|---|---|
| `server/server.mjs` | the HTTP service (`node server.mjs`): pages, catalogue, packages, sign-in, API, downloads |
| `server/discourse.mjs` | DiscourseConnect: the request to the forum and its answer checked |
| `server/github.mjs` | the catalogue's repository: a submission's branch and pull request, its review state |
| `server/data.mjs` | the files under `/data`, the lock and atomic writes, the download counter |
| `server/catalogue-lib.mjs` | **a copy** of `lapin-extensions/tools/lib.mjs` (the manifest and package rules); the test fails when the two differ: `cp ../lapin-extensions/tools/lib.mjs deploy/store/server/catalogue-lib.mjs` |
| `server/cli.mjs` | the maintainers' commands (`list`, `stats`, `owner`, `forget`) |
| `server/public/` | the dashboard's page and script |
| `server/server.test.mjs` | the tests: `pnpm test:store` (the forum and GitHub faked) |
| `deploy.sh` | the service's files to the server, the container restarted |

The store's pages themselves are **not** here: they are generated from the catalogue by `lapin-extensions/site/build.mjs` and published, with the signed index and the package copies, by `lapin-extensions/tools/publish.mjs --upload` (signing happens on the maintainer's Mac; the key never goes to the server).

## What it serves

| Address | What |
|---|---|
| `store.lapinbim.com/` | the store's pages (`/data/public/site`): the list, `/extension/<id>/`, `/authors/…`, `/publish/`, `/legal/…` |
| `store.lapinbim.com/dashboard/` | the author dashboard |
| `lapinbim.com/extensions/index.json`, `index.json.sig`, `files/<id>/<version>/<file>` | the catalogue (`/data/public/extensions`), for Lapin; `Access-Control-Allow-Origin: *`; the index cached 5 minutes, the files for a year (a new version is a new path) |
| `store.lapinbim.com/packages/<id>/<version>/<file>` | the files authors uploaded (`/data/packages`): a store submission's `package.url`, always as a download, sandboxed |
| `/auth/login`, `/auth/callback`, `/auth/logout` | sign-in through the forum |
| `/api/me`, `/api/me/submissions`, `/api/submissions…` | the dashboard's API (a session cookie; changes only from the store's own pages) |
| `/api/download/<id>/<version>` | counts a download, then redirects to the copy (`package.download` in the index: Lapin tries it first) |
| `/api/stats/<id>` | `{ total, versions }` |
| `/api/health` | `{ ok: true }` |

## How a submission goes

1. The author signs in (DiscourseConnect: the forum is the identity provider; the store keeps the forum id, user name and display name, never the e-mail) and fills in the form: the manifest's fields, the file, up to six screenshots, the author terms accepted.
2. The store checks the fields with the catalogue's rules, then the file as its kind's (`checkPackage`: the same check as the catalogue's CI and Lapin's installer), and keeps it at `/data/packages/<id>/<version>/<file>`.
3. **Send** opens a pull request in `Ciuntuc-BIM-Studio/lapin-extensions` (a branch `store/<id>-<version>-…` with `extensions/<id>/manifest.json` and the screenshots). The manifest's `package.url` is the store's own address of the file, `author.forum` the author's forum name. The catalogue's CI checks it like any other pull request (it downloads the file from the store).
4. The dashboard shows the pull request's state and checks (read from GitHub at most once a minute). A maintainer merges it (a script after its code is read).
5. The maintainer publishes (`node tools/publish.mjs --upload` in lapin-extensions): the index is rebuilt and signed, the file copied to `lapinbim.com/extensions/files/…` after its SHA-256 is checked, the pages regenerated.

**Who may publish an id.** The first author whose submission of an id is merged owns it here (`owners.json`); only they (or a maintainer: a forum admin, or a name in `STORE_MAINTAINERS`) submit its next versions. An id listed through GitHub belongs to the forum name in its `author.forum`, else to nobody here (`cli.mjs owner` gives it to its author once they ask). While a submission of an id is in review, nobody else can submit that id. The pull requests are opened by the store's token, so the catalogue's CI sees a maintainer; the store's ownership rule is what holds.

**Why the files are hosted here**, not in GitHub releases: the author needs no GitHub account, the file is the one the store checked (its SHA-256 goes into the manifest at once), and it is already beside the copies the browser edition reads. A file under review is served only as a download (`application/octet-stream`, `Content-Disposition: attachment`, a sandboxing CSP), never as a page.

**Downloads** are counted per extension, version and month, nothing about who; a visitor (a salted hash of Cloudflare's address, in memory only) counts at most `COUNTED_PER_HOUR` times per extension per hour, and is still served past that. Counts are written to `stats.json` every minute.

**Limits** (per visitor or per author, in memory): any API call 240 a minute, 30 sign-ins an hour, 10 submissions a day, 40 uploads an hour; a package ≤ 64 MB, a screenshot ≤ 2 MB (PNG, WebP or JPEG, checked by their bytes), a JSON body ≤ 64 KB.

**Housekeeping** (daily): declined or withdrawn submissions, and drafts never sent, are deleted with their files after 90 days (`KEEP_DECLINED_DAYS`); open ones have their state read.

## The settings

From the environment (the compose file and `store.env` on the server):

| Variable | Default | |
|---|---|---|
| `STORE_SSO_SECRET` | none: sign-in is off | DiscourseConnect's shared secret (the forum has the same) — **secret** |
| `STORE_GITHUB_TOKEN` | none: submissions cannot be sent | a fine-grained token on `lapin-extensions` — **secret** |
| `STORE_SALT` | random per start | keeps a visitor's rate-limit hash stable across restarts — **secret** |
| `STORE_MAINTAINERS` | none | forum user names that act as maintainers (forum admins always do) |
| `STORE_URL` | `https://store.lapinbim.com` | |
| `FORUM_URL` | `https://forum.lapinbim.com` | |
| `MIRROR_URL` | `https://lapinbim.com/extensions/files/` | where downloads redirect |
| `STORE_GITHUB_REPO` | `Ciuntuc-BIM-Studio/lapin-extensions` | |
| `DATA_DIR`, `PUBLIC_DIR` | `/data`, `/data/public` | |
| `PORT` | `8080` | |

## Deploying (first time)

**Done on 12.11.2026.** What differs from the steps below: no Cloudflare path route — the tunnel sends `store.lapinbim.com` and `lapinbim.com` to lapin-web (127.0.0.1:3461), whose nginx proxies `store.lapinbim.com` (its own `server` block) and `location /extensions/` to `lapin-store:8080` on the compose network; `store.env` was written on the server with the SSO secret and the salt made there (`openssl rand`) and the GitHub token piped from the Mac's keychain item `lapin-store-github`; the forum's two DiscourseConnect settings were set through the admin API with the secret read from `store.env`, never shown; the store key is `~/.lapin/store-key.pem` (id `2daeb4d5dc6eae92`), listed in `STORE_KEYS`, so **every publication is signed** (`node tools/publish.mjs --upload`, never `--unsigned` again). The steps stay as the record of a first install (and for a self-hosted copy). The forum, Cloudflare and GitHub steps are the owner's; secrets never go into a chat, a commit or a log.

### 1. GitHub: the store's token

GitHub → Settings → Developer settings → Fine-grained personal access tokens → Generate new token:

- **Resource owner**: Ciuntuc-BIM-Studio (if the organisation does not allow fine-grained tokens yet: organisation Settings → Personal access tokens → allow, with approval).
- **Repository access**: only `Ciuntuc-BIM-Studio/lapin-extensions`.
- **Permissions**: Contents *read and write*, Pull requests *read and write*, Checks *read* (Metadata *read* comes with them).
- **Expiration**: one year; note the date: the dashboard's Send fails with "could not be opened" when it has expired.

Copy it straight into `store.env` on the server (step 3).

### 2. The forum: DiscourseConnect provider

Admin → Settings, search "discourse connect provider":

- `enable discourse connect provider`: **on**;
- `discourse connect provider secrets`: one line, `store.lapinbim.com|<secret>`, where `<secret>` is a long random text (on the server: `openssl rand -hex 32`, typed into `store.env` too; the same text in both places).

Nothing else on the forum changes; its own sign-in stays as it is. (The admin API user `claude` could set these, but they are the owner's to set.)

### 3. The server (`/opt/lapin`)

```sh
ss -ltnp | grep -q ':3465 ' && echo "3465 is taken: choose another port here and in the tunnel"
mkdir -p /opt/lapin/store /opt/lapin/store-data/public && chown -R 1000:1000 /opt/lapin/store-data
install -m 600 /dev/null /opt/lapin/store.env && nano /opt/lapin/store.env
```

`store.env` (mode 600, never copied off the server):

```sh
STORE_SSO_SECRET=<the forum's secret>
STORE_GITHUB_TOKEN=<the token>
STORE_SALT=<openssl rand -hex 16>
STORE_MAINTAINERS=<the owner's forum user name>
```

The compose service, beside `feedback` in `/opt/lapin/docker-compose.yml`:

```yaml
  store:
    image: node:22-alpine
    container_name: lapin-store
    restart: unless-stopped
    user: "1000:1000"
    working_dir: /srv
    command: ["node", "server.mjs"]
    env_file: ./store.env
    environment:
      - DATA_DIR=/data
    ports:
      - "127.0.0.1:3465:8080"
    volumes:
      - ./store:/srv:ro
      - ./store-data:/data
    read_only: true
    mem_limit: 384m
```

Then, from the Mac: `deploy/store/deploy.sh` (it copies the service's files, starts the container and checks `/api/health`).

### 4. Cloudflare: the tunnel's routes

Zero Trust → Networks → Tunnels → the server's tunnel → Public Hostname → Add a public hostname (as for the forum, deploy/forum/README.md):

1. Subdomain `store`, domain `lapinbim.com`, service **HTTP** `127.0.0.1:3465`.
2. Subdomain empty, domain `lapinbim.com`, path `^/extensions/`, service **HTTP** `127.0.0.1:3465`. Check DNS → Records first: if `lapinbim.com` itself already points somewhere (a site), keep that record and add only this path route to the tunnel when the apex is served by the tunnel too; otherwise Cloudflare adds the record itself.

Then for `store.lapinbim.com`: Scrape Shield → Email Address Obfuscation **off** (it injects a script the store's Content-Security-Policy blocks), Rocket Loader off. Caching: the service sends its own `Cache-Control`; nothing to set.

### 5. Publish the catalogue and the pages (from lapin-extensions)

```sh
cd ../lapin-extensions
cp .env.example .env    # DEPLOY_SERVER=…
node tools/publish.mjs --unsigned --upload    # before the store key exists
```

### 6. Check

```sh
curl -s https://store.lapinbim.com/api/health                          # {"ok":true}
curl -sI https://lapinbim.com/extensions/index.json | grep -i access-control   # access-control-allow-origin: *
curl -sI https://store.lapinbim.com/api/download/ro.ciuntucbimstudio.mobilier-living/1.0.0 | grep -i location
```

Then sign in at `https://store.lapinbim.com/dashboard/`, and in Lapin: File → Extensions… reads the catalogue from lapinbim.com.

## Signing goes live (when the owner decides)

1. On the Mac: `node scripts/store-key.mjs` — makes `~/.lapin/store-key.pem` (encrypted; its password the keychain item `lapin-store-key`) and prints a line. Back the file up with the updater key.
2. `node tools/publish.mjs --upload` in lapin-extensions: the index and its packages are now signed (`index.json.sig`, `package.signature`). Builds without a key read it as before.
3. Add the printed line to `STORE_KEYS` in `ui/src/platform/extensions/keys.ts`, commit, release. From that build on, Lapin refuses Lapin's own catalogue unsigned or wrongly signed, and each of its packages unsigned; a catalogue of one's own may stay unsigned (the dialog warns).

The order matters: publish signed first, ship the key second. Every later publication is signed (`publish.mjs` without `--unsigned`).

## Every day

```sh
ssh "$DEPLOY_SERVER" docker exec lapin-store node cli.mjs list          # the submissions
ssh "$DEPLOY_SERVER" docker exec lapin-store node cli.mjs stats         # downloads
ssh "$DEPLOY_SERVER" docker exec lapin-store node cli.mjs forget <name> # a person's data erased on request (GDPR)
```

After merging pull requests: `node tools/publish.mjs --upload` in lapin-extensions.

**Backups**: `/opt/lapin/store-data` (submissions, packages, owners, stats) with the server's other backups. **Rolling back**: stop the container (`docker compose stop store`); the catalogue files stay on disk and Lapin keeps its installed extensions.
