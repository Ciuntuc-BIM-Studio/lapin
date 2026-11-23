# Local Speckle server (development only)

A Speckle server (2.31.14) on this machine for developing Lapin's Speckle connector
(`docs/plan-speckle.md`, decision 800). **Dev only, never production, never exposed**:
everything binds to 127.0.0.1, and Lapin does not host a Speckle for users.

`docker-compose.yml` is adapted from Speckle's official compose files at tag 2.31.14
(source URLs in its header): published images pinned to that version, no preview,
webhook, IFC-import or multi-region services. The images are amd64 and run under
emulation on Apple silicon (OrbStack); about 450 MB of RAM and 1.7 GB of images.

| What | Address |
|---|---|
| Web UI and GraphQL | http://127.0.0.1:8090 (GraphQL at `/graphql`) |
| Object storage (MinIO, used by file uploads) | http://127.0.0.1:9100 |

## Start, stop, reset

```sh
cd deploy/speckle-dev
cp .env.example .env     # first time only; fill the empty values with: openssl rand -hex 16
docker compose up -d     # first start pulls the images and takes a minute or two
docker compose down      # stop, data kept
docker compose down -v   # reset: deletes the database, the account and the tokens
```

`.env` is git-ignored. After a reset, register the account again (below).

## Account and token

Register the first user in the web UI (it becomes the server admin), then Profile ->
Developer settings -> Access tokens -> New token, with the scopes `streams:read`,
`streams:write`, `profile:read`, `profile:email` and `users:read`.

On the author's Mac the dev account is `dev@localhost.dev`, with its password and
token in the keychain, never in the repo:

```sh
security find-generic-password -s lapin-speckle-dev -a dev@localhost.dev -w          # password
security find-generic-password -s lapin-speckle-dev-token -a dev@localhost.dev -w    # token
```

Without the UI: `POST /auth/local/register?challenge=<c>` and `/auth/local/login`
give an `access_code`; `POST /auth/token` with the default app (`spklwebapp` /
`spklwebapp`) trades it for a token; the GraphQL mutation `apiTokenCreate` makes a
personal access token.

## Try it

```sh
curl -s http://127.0.0.1:8090/graphql -H "authorization: Bearer $TOKEN" \
  -H 'content-type: application/json' -d '{"query":"{activeUser{name email}}"}'
```

The SDK is `Speckle.Sdk` and `Speckle.Objects` 2026.9.0 (`IClientFactory.Create(account)`,
`IOperations.Send2` / `Receive2`, `client.Version.Create`).
