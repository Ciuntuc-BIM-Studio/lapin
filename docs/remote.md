# The project's history and its remote (lapin-remote 1.0 client, S1)

For developers. The protocol is `docs/spec-lapin-remote.md` (normative) with its vectors in `docs/spec-lapin-remote/vectors/`; the plan and its decisions are `docs/plan-atrium-cde.md` (decision 793). This page says where the client is, what it does in S1, and how to try it against a folder and against Atrium Server on this computer.

## Where the code is

| What | Where |
|---|---|
| RFC 8785 canonical JSON, written by hand (no reflection: the browser's engine is trimmed) | `engine/src/Platform/Lapin.Package/Remote/Jcs.cs` |
| Ids, trees (the manifest without `foreign`), commits, tags, ref names, wip branch names | `Remote/RemoteObjects.cs` |
| The history inside the `.lapin` (`history/HEAD`, `refs.json`, `index.json`, `commits/`, `objects/`, `remotes.json`), the once rule, fsck | `Remote/PackageHistory.cs` |
| One interface for both remotes | `Remote/IRemote.cs` |
| The folder remote: layout, gzip objects, append-only ref logs, fork resolution, the lock, compare-and-swap | `Remote/FolderRemote.cs` |
| The HTTP remote: discovery, password sign-in, refresh on 401, refs with ETag, `refs/update`, `objects/missing`, PUT, resumable uploads, packs, the commits walk, viewer records; asynchronous (`SendAsync`), the same code on the desktop and in the browser's engine | `Remote/HttpRemote.cs` |
| Push, fetch, clone, where a pull stands (§8) | `Remote/RemoteOps.cs` |
| The engine's calls `history.*` and `remote.*`, the viewer IFC, pull as one undo step | `engine/src/Platform/Lapin.Protocol/EngineDispatcher.Remote.cs`, `ProjectFiles.History.cs` |
| File → Server and collaboration: Collaborate…, Save to server…, Get latest from server, Open from server… (also on the start screen) | `ui/src/modules/core/RemoteDialog.tsx` (`RemoteDialog`, `SaveToServerDialog`, `OpenFromServerDialog`), `ui/src/engine/remote.ts` (`getLatest`), `ui/src/shell/fileActions.ts` |
| The desktop's store of sign-ins (`remote_secret_get` / `remote_secret_set`) | `desktop/src-tauri/src/lib.rs` |
| Tests: every vector, and the round trips | `engine/tests/Lapin.Engine.Tests/RemoteVectorTests.cs`, `RemoteRoundTripTests.cs` |

The client lives in `Lapin.Package` (as the spec names it), not in a project of its own: the history is part of the package (it is written by `LapinPackage.Write` with the other entries, read with `PackageForeign`), and the package is what every host already references. It needs nothing beyond the BCL.

The history travels with the other entries the file holds beside the model (`PackageForeign.History`): read with the file, written with every save, so Save As, recovery and versions carry it without new plumbing. `history` is now one of the names Lapin owns. A project without a history writes no `history/` and opens as before; no `IProjectUpgrade` is needed.

## The calls

All take `author` (`{id, name}`: `local:<installation id>`, or `atrium:<public id>`), and for a server `token` and `refreshToken`; a result carries `tokens` when a refresh replaced them (the refresh token is single-use, the interface stores the new one at once). None is let through to a guest of a shared session.

| Call | Does |
|---|---|
| `history.status` | branch, head commit, uncommitted (the live parts against the head's tree), the remote with ahead/behind as last seen, fsck problems |
| `history.log` `{limit}` | the branch's commits, newest first, each marked `pushed` when the remote has it |
| `history.commit` `{message}` | the project as it is committed and the file written (refused when nothing changed); `bytes: true` in the browser |
| `remote.add` `{kind, url, project? , create?, projectName?}` | a folder (a project made in it when none is given) or a server project; one remote per project in S1 |
| `remote.remove`, `remote.projects` `{kind, url}`, `remote.signIn` `{url, username, password}` | |
| `remote.changePassword` `{url, currentPassword, newPassword}` | Atrium Server's own `POST /atrium/1.0/account/password` (not lapin-remote; Atrium's `docs/atrium-server.md`): the server ends every sign-in of the account and hands back a fresh pair (`tokens`, kept as a refresh's are). A refusal is an answer, not an error (`changed: false`, `code`: `wrong_password`, `weak_password`, `too_many_attempts`, `not_supported` on a server without it, `unauthorized`; `message`), so that tokens a refresh replaced on the way still reach the interface. In the interface: File → Collaborate…, signed in, **Change password…** |
| `remote.fetch` | refs (with the ETag), commits walked, objects lacked; never changes the model; objects enter the file at the next save |
| `remote.pull` `{resolve?: "branch" \| "discard"}` | fast-forward only; refused with uncommitted changes; histories that diverged are reported (`kind: "diverged"`) until `resolve` says what to do |
| `remote.push` `{noViewer?}` | the current branch; a 409 or a remote that moved says so (`pushed: false`, `message`); attaches the IFC4 viewer copy of the head when the project is that commit |
| `remote.clone` `{kind, url, project, path}` | the remote's project into a new file (opened as any package is, then given the history) |

## Testing locally

### Against a folder

1. `pnpm dev` (or `pnpm dev:desktop`), open or make a project, save it.
2. File → Collaborate…: kind **Folder**, choose an empty folder (for instance `~/LapinRemoteTest`), give your name, **Make a new project there**. The folder gets `lapin-remote.json` and `<project id>/project.json`.
3. Type a message, **Commit**, then **Push**. The folder now holds `objects/`, `commits/`, `refs/heads/main/00000001-….ref` and `viewers/<commit>.json`.
4. Open a second Lapin window (or `pnpm dev` on another port) with another name; File → **Open from server…**: kind Folder, the same folder, the project id (shown in the first window), a new file. Both projects are the same.
5. Edit and push from one, **Pull** in the other: the change arrives in one undo step. Commit in both and push from both: the second is refused ("pull first"); **Keep my work as a branch** pushes it as `wip/<you>/<date>` and brings the project to the remote's.

From the command line, the same round trip runs in `dotnet test engine/tests/Lapin.Engine.Tests --filter RemoteRoundTripTests`.

### Against Atrium Server on 127.0.0.1

The server is in the Atrium repository (branch `feat/lapin-remote-server`); its `docs/atrium-server.md` has the details. In short:

```sh
cd ~/Documents/GitHub/Atrium            # the server's branch checked out
dotnet build src/Atrium.slnx
S=src/Atrium.Server/bin/Debug/net10.0/Atrium.Server
D=~/AtriumServerTest; mkdir -p "$D"
ATRIUM_PASSWORD='choose-a-password' $S user add mihai@example.com --name "Mihai Ionescu" --admin --data "$D"
ATRIUM_PASSWORD='choose-a-password' $S user add ana@example.com --name "Ana Popescu" --data "$D"
$S project add "Bloc P+4" --owner mihai@example.com --data "$D"       # prints the project id
$S project member <id> ana@example.com editor --data "$D"
$S --data "$D" --urls http://127.0.0.1:5280
```

Then in Lapin: File → **Collaborate…**, kind **Server**, address `http://127.0.0.1:5280` (plain http is accepted on this computer only), sign in as `mihai@example.com`, **Projects on the remote**, pick Bloc P+4, **Use this project**; commit and push. In a second window sign in as `ana@example.com` and clone the project. Push from both to see the 409, then keep the work as a branch. Atrium (the viewer) opens the pushed commit through its viewer IFC: File → Open from server.

The same round trip without the interface (push, clone, a second user's push, the 409, keep as a branch) runs against the server when its variables are set; without them the test passes at once:

```sh
LAPIN_REMOTE_SERVER=http://127.0.0.1:5280 LAPIN_REMOTE_PROJECT=<id of a project with no commit yet> \
LAPIN_REMOTE_MIHAI=mihai@example.com LAPIN_REMOTE_ANA=ana@example.com LAPIN_REMOTE_PASSWORD='choose-a-password' \
dotnet test engine/tests/Lapin.Engine.Tests --filter RemoteServerTests
```

With `LAPIN_REMOTE_PASSWORD_ACCOUNT=<a throwaway account of the test server>` too, the account's password is changed through the engine and changed back (never name a real account).

`curl -s http://127.0.0.1:5280/lapin-remote/1.0/capabilities` checks the server answers; `GET /lapin-remote/1.0/projects/<id>/refs/log` (with a bearer token) shows who moved what.

## Where the tokens are

Never in a project, a commit, a ref entry or a folder remote. The engine gets them with each call and forgets them. The interface keeps the access token in memory; on the desktop the refresh token goes to the shell's store of sign-ins, `remote-secrets.json` in the app's data folder, readable by its owner only (0600). That file is a stopgap of S1: the plan's place is the system's keychain through the `keyring` crate (S2). In the browser the tokens live in the tab's memory only.

## What S1 does not do yet

- The browser's engine reaches only the CDE server it is served by (W2, below): the page's CSP allows no other origin, so a stock web build without a CDE server still hides Collaborate, Save to server and Get latest.
- Versions as `refs/tags/v/<n>` tags and issues as `refs/tags/issue/*` (the client pushes and fetches tags, but Save version and Issue do not make them yet).
- The status bar's `↑2 ↓1`, the History tab in Manage → Versions, fetch on open and every 5 minutes, Clean up (folder GC).
- An `atrium:` author id: Foundation's `current-user` gives the e-mail address as the id, which never goes into a commit, so commits on a server are signed `local:<installation>` with the account's name until the server tells the public id (the server's ref log names the account by its `atrium:` id all the same).

## Lapin on the web in front of a CDE server (W1)

Atrium's `docs/plan-cde-web.md`, stage W1: Lapin's browser build served at `https://cde.lapinbim.com/` with Atrium Server's APIs on the same origin. Read-only, and without the engine's remote (which the browser cannot run yet):

| What | Where |
|---|---|
| The server's client in the interface: password sign-in (client `lapin-web`), refresh on 401 one at a time, projects, refs, the commits walk, tag objects, the ref log, members, change password, the package and the viewer IFC | `ui/src/engine/cdeServer.ts` (tests: `cdeServer.test.ts`) |
| The start screen in front of the server: sign in, projects, a project's history of main (author, date, message, version and issue chips), branches in progress, versions and issues, activity, members (owners change roles, remove, add by e-mail), change password, Work offline | `ui/src/shell/CdeStart.tsx`, chosen by `StartScreen.tsx`; File → **Server projects…** comes back |
| Open: the commit's package (`…/commits/{c}/package?viewer=false`) into the browser's engine with no file of its own, so Save keeps a copy and nothing goes back; downloads of the `.lapin` and of the commit's IFC copy | `openProjectBytes` in `ui/src/engine/files.ts` |
| The build | `node scripts/build-web.mjs lapin --cde` → `build/lapin/web-cde` (`VITE_CDE_SERVER=same-origin`); a stock web build also finds a server when its origin answers `/lapin-remote/1.0/capabilities` |
| The deploy | `deploy/web/deploy.sh --cde` → `/opt/lapin/www/cde`; the nginx block is Atrium's `deploy/cde/nginx-cde.conf` (API prefixes to `lapin-cde:8080`, `/api/feedback` to the feedback container, CSP) |

Tokens: the access token in the tab's memory, the refresh token (single use, rotated) in `sessionStorage` (a reload keeps the sign-in, closing the tab ends it); never `localStorage`, no cookie.

To try it locally: run Atrium Server on `127.0.0.1:5280` (above), `node scripts/build-web.mjs lapin --cde`, and serve `build/lapin/web-cde` with the four API prefixes proxied to the server (any static server with a proxy; `/lapin-remote/1.0/capabilities` must answer JSON on the page's origin).

## Working from the browser (W2)

Atrium's `docs/plan-cde-web.md`, stage W2 (§2.1, option A): the engine's remote is asynchronous, so the browser's engine (WebAssembly in a Web Worker, where `HttpClient` is the page's `fetch` and nothing may block) runs the same client as the desktop's. Hashing, the closure, compare-and-swap and the conflict handling are written once and tested by the vectors.

| What | Where |
|---|---|
| `IRemote`, `HttpRemote`, `RemoteOps` asynchronous (`PushAsync`, `FetchAsync`, `CloneAsync`; `ReadyAsync` reads a server's capabilities first). The folder remote stays synchronous underneath (a disk) and answers the same calls at once; the desktop's only | `engine/src/Platform/Lapin.Package/Remote/` |
| The dispatcher's async lane: `HandleAsync` awaits the `history.*` and `remote.*` calls and ends at once for every other; `Handle` waits for it (tests, scripts). A server out of reach or a timeout is said, not an engine error | `EngineDispatcher.cs`, `EngineDispatcher.Remote.cs` |
| The desktop host awaits `HandleAsync` inside its gate: a push or a pull holds the queue of calls, so the session never changes under it | `engine/src/Host/Lapin.Host/EngineHub.cs` |
| The browser host's `CallAsync` (`[JSExport]`, a `Task`): its diff and reply go to the page through `frame` when it ends. The worker answers calls one at a time; a remote call is awaited and the calls that come meanwhile wait in its queue | `engine/src/Host/Lapin.Browser/Program.cs`, `ui/src/engine/worker/engine.worker.ts` |
| One sign-in: in front of a CDE server the engine's remote calls carry the start screen's tokens (an access token got first after a reload) and the pair a refresh in the engine replaced goes back to `sessionStorage` at once (the refresh token is single use). Commits are signed with the account's name and this installation's `local:` id | `ui/src/engine/cdeServer.ts` (`cdeTokens`, `keepCdeTokens`, `isCdeServer`), `ui/src/engine/remote.ts` |
| **Clone to work on** on a project's page: `remote.clone` against the page's server, the project opened from the server's latest with its history; File → Collaborate…, **Save to server…** and **Get latest from server** are in the browser's File menu in front of a CDE server | `ui/src/shell/CdeStart.tsx`, `ui/src/shell/fileActions.ts` |
| The copy lives in the browser: the `.lapin` with its history is held by the engine (a commit writes it in memory, no download each time; File → Save still keeps a copy on this computer). For any project whose remote is the CDE server (cloned, or connected in Collaborate) the status bar says **Not saved to the server** while the browser holds changes or commits the server has not got (a click opens Save to server), else **Saved to the server**; the tab asks before closing while there is something not saved there | `CdeServerStatus` in `CdeStart.tsx`, `noteStatus` in `remote.ts`, `guardWindowClose` in `files.ts` |
| A Save to server the server refused (someone saved first) opens Collaborate on the choices: **Keep my work as a branch** or Discard my commits | `SaveToServerDialog`, `RemoteDialog` in `RemoteDialog.tsx` |

What the user does at `https://cde.lapinbim.com/`: sign in → the project → **Clone to work on** → edit → File → **Save to server…** (a note, then Save: commit and push in one step). Someone else's newer work: File → **Get latest from server**. When both saved, the second is told the server moved; File → Collaborate… → **Keep my work as a branch** pushes it as `wip/<name>/<date>` and brings the project to the server's.

`System.Net.Http` is one of the browser engine's lazy parts: fetched the first time a remote call runs. Same origin, so the CSP's `connect-src 'self'` covers every request and no CORS is involved.

To try it locally: Atrium Server on a free loopback port, `node scripts/build-web.mjs lapin --cde`, and the build served on another loopback port with the four API prefixes proxied to the server (as for W1). The proxy must pass the page's `Host` header on, as nginx does (`proxy_set_header Host $host`): the server names its API bases (`/foundation/versions`) after the host it was asked by, and the engine follows them, so a proxy that rewrites the host sends the engine to another origin, which the CSP refuses. Clone as one account, edit, Save to server; clone or Get latest as the other (a second browser profile, or a private window: the sign-in is per tab); save from both to see the conflict and keep the work as a branch. A new project goes onto an empty server project from File → Collaborate… (the server is filled in) → Projects on the remote → Use this project, then Save to server.

## Issues, activity and administration (W3)

Atrium's `docs/plan-cde-web.md`, stage W3, with W2's administration screens. The answers of §7 hold: a project's BCF topics live both in the `.lapin` (its remarks, `LapinRemark`, by GUID) and on the server; for a shared project the server is the truth; last writer wins per field; the server's BCF event journal says who changed what.

| What | Where |
|---|---|
| The sync: account, role, rules, members and BCF extensions read; what changed on the server since the last seq (`GET /atrium/1.0/projects/{p}/events?after=`, with every topic's ETag from `…/topics` as a second check) read whole (topic, comments, viewpoints with their selection, visibility and snapshot); each topic merged field by field against the server values last seen; new topics sent with the remark's GUID, then their viewpoints and comments; comments before a status (so "Resolved" finds its comment); edits as JSON merge patches of the changed fields with `If-Match`, a 412 read again and sent again (3 times); deletions both ways, a refused one bringing the topic or comment back | `engine/src/Platform/Lapin.Protocol/IssueSync.cs` |
| Requests to Atrium's own API and BCF on the remote's sign-in (refresh on 401 as every call): `HttpRemote.ApiAsync`, answering status, JSON and ETag whatever the status | `Lapin.Package/Remote/HttpRemote.cs` |
| `remote.syncIssues` (one undo step, "Sync issues"; the state kept with the history; in the browser the file is written in memory with it, on the desktop the next save writes it), `remote.issueInfo` (from the last sync, without the server: role, rules, people, choices, topics known, remarks waiting), `remote.atrium` (one request to `atrium/1.0/…`, `bcf/3.0/…` or `POST lapin-remote/1.0/projects`, for the desktop's interface, which cannot reach a server itself) | `EngineDispatcher.Remote.cs` |
| The sync's state: per remote, in `history/remotes.json` (member `issues`: server, project, account, the journal's last seq, each topic's ETag, its synced fields as last seen, the comment and viewpoint GUIDs the server holds, plus the role, rules, members' names and BCF choices for the interface). Never in a commit; an older Lapin keeps the member as it is (§6.1), so no `IProjectUpgrade` | `PackageHistory.Issues` |
| With a server remote the remarks' parts are left out of "uncommitted" and of a commit's "nothing changed", and a pull keeps the remarks here (a commit's copy would undo the sync); a folder remote has no issues of its own, so remarks travel with its commits as before | `IssuesApart` in `EngineDispatcher.Remote.cs`, `PackageHistory.SameParts` |
| The remarks dialog in front of a server: Sync issues, last synced and how many wait, the server's types, statuses, priorities and labels, Assigned to from the members, authors by name; what the role may not do greyed out (viewers make no issue and change only the status of one assigned to them; editors change what they made or are assigned to; Closed for the author or owners unless the rules say otherwise; only owners delete) | `ui/src/modules/core/remarks/RemarksDialog.tsx`, `issues.ts` |
| Sync along Save to server and Get latest; the server's refusals said in the reader's language (`status_only`, `close_forbidden`, `comment_required`, `delete_forbidden`, `exists`, `self`…) | `ui/src/engine/remote.ts` (`syncIssues`), `refusals.ts` |
| Activity (`GET …/activity`: saves, versions, issue sheets and issue changes, newest first, More), Rules (owners change them), Administration (accounts: create with the server's password shown once, disable and enable, reset a password, make administrator; new project) | `ui/src/modules/core/CdeAdmin.tsx`, `ui/src/engine/atrium.ts` (the page's fetch in the browser, `remote.atrium` on the desktop); on the CDE start screen and in File → Collaborate… (Administration… for administrators) |
| One refresh at a time: the start screen's refreshes and the engine's calls (which refresh by themselves) take turns (`withTokens`), and a refresh first looks whether another one already replaced the token, so a single-use refresh token is never spent twice; the desktop's calls on one server go one after another | `ui/src/engine/cdeServer.ts`, `remote.ts` |
| Tests: a fake Atrium Server (BCF, journal, ETags, merge patch, the issue rules) for the round trip, both users' fields kept, the last writer of one field, the 412, status_only, comment_required, close_forbidden, deletions both ways, one undo step, the state through save and reopen, remarks out of "uncommitted"; the token race, the rights and the Atrium client in the interface | `engine/tests/Lapin.Engine.Tests/IssueSyncTests.cs`; `ui/src/engine/cdeServer.test.ts`, `atrium.test.ts`, `ui/src/modules/core/remarks/issues.test.ts` |

Limits: comments are not edited from Lapin (only made and deleted); the BCF fields Lapin does not hold (reference links, index, BIM snippet) stay the server's; the merge of one field is "last to sync wins" (the server's PATCH semantics), not by clock. A user's guide page: `docs/user/cde-server.md` (no scene: it needs a live server).
