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

Limits: comments are not edited from Lapin (only made and deleted); the BCF fields Lapin does not hold (index, BIM snippet) stay the server's (reference links are synced since decision 824); the merge of one field is "last to sync wins" (the server's PATCH semantics), not by clock. A user's guide page: `docs/user/cde-server.md` (no scene: it needs a live server).

## Publishing (W4)

Atrium's `docs/plan-cde-web.md`, stage W4, against Atrium Server's publishing API (its `docs/atrium-server.md` § Publishing: submissions, the publish gate, containers, the handover diff, the e-mail outbox). The interface shows W4's screens only when the server announces `submissions` in `GET /atrium/1.0/` (no sign-in; an older server answers 404 there).

| What | Where |
|---|---|
| `remote.submitIssue`: a `LapinIssue` (the last one, or `issue`) submitted for approval: the head commit, which must be on the server (else "save to the server first"); the record's number (`next: true` lets the server number it), revision, Shared or Published from its container (`purpose`) with its status as the suitability when it fits (else S2 / A1); the documents (each issued file's name without its `_<status>_<revision>` suffix and extension); the files the project's file keeps, sent first as objects (only those the server lacks). A refusal is an answer (`ok: false`, the server's code and members: `tag_exists`, `already_submitted`, `forbidden`…) | `EngineDispatcher.Publishing.cs` |
| `remote.issueTag`: the issue tag made here (lapin-remote §4.4: a canonical `lapin-tag` of kind `issue` with the record's GlobalId, revision, status, container, files and note) and pushed as `refs/tags/issue/<n>-<rev>`, the files uploaded first; for a folder remote, or a server whose `issueTags` rule lets the account. Kept in the project's history only when the remote took it | same |
| `remote.handoverReport`: the handover report as an XLSX (`CdeHandoverReport`, through Lapin's own OOXML writer `Lapin.Drawing/Export/XlsxWriter`): sheets Report, Issues, Comments, Journal (the server's `…/events`, paged, every row with who, when, old and new value) and, with `from` and `to` (two issue tags), Handover diff (`…/handover-diff`, or the caller's `diff`); people by their names from the last issue sync. Without a server, or another server project open (`serverProject`), it has what the project holds and says what it lacks (`notes`). To `path`, or its bytes | `EngineDispatcher.Publishing.cs`, `Lapin.Platform/Cde/CdeHandoverReport.cs` |
| `remote.atrium` also reads, with `bytes: true`, a commit's package, its viewer record and an object of a project (`lapin-remote/1.0/projects/{p}/commits/{c}/package\|viewer`, `…/objects/{id}`), as base64 or to `saveTo`: the desktop's containers view opens and downloads revisions and issued files through it | `EngineDispatcher.Remote.cs` |
| The screens: Publishing (containers WIP / Shared / Published / Archived with revision, status, date, who issued it, Open read-only, .lapin, IFC, the issued files; the queue with its filter, a submission's gate with each open issue opening its remark, Approve / owner's override with a note / Reject with a reason / Withdraw, greyed by role and rule; the handover diff between two issued revisions with its export) on the CDE project page and in File → Collaborate…; the outbox in Administration; the `approve` rule in Rules; **On the CDE server** at the end of Manage → Issue… (Submit for approval; Make the issue tag now) | `ui/src/modules/core/CdePublish.tsx`, `CdeAdmin.tsx`, `cde/IssueDialog.tsx`, `ui/src/engine/atrium.ts`, `publishing.ts` |
| Tests | `engine/tests/Lapin.Engine.Tests/HandoverReportTests.cs` (the workbook opened as a zip: parts, content types, sheets, cell values, against the fake Atrium Server of W3 plus the handover diff), `CdePublishTests.cs` (submission body, files sent first, refusals as answers, WIP and suitability, the direct tag on a server and a folder, the desktop's downloads); `ui/src/engine/publishing.test.ts` (rights, containers, the proposed pair) |

Limits:

- The gate links an open issue to a document by a BCF reference link `lapin-document:<document>` (labels must be in the project's list, which no API edits). Lapin sets it itself since decision 824 (below).
- The handover diff is the server's: the two tags' issued files by name (status and revision stripped) and SHA-256, and the model's parts by path (whole Arrow tables, not per sheet). A per-sheet diff of the model, made by the engine opening both commits, is not done.
- Archived, on the server, is "the same issue number published again at a later revision": Lapin reuses the number for a re-issue since decision 824 (below).

## Remarks tied to documents, re-issues as revisions (decision 824)

The user's two decisions after W4 (23.11.2026), so that the publish gate and Archived work with what Lapin makes.

| What | Where |
|---|---|
| A remark's BCF reference links: `BcfTopic.ReferenceLinks` (kept as `referenceLinks` on the `LapinRemark`, only when there are some, so a project saved before needs no `IProjectUpgrade`); read and written in BCF 2.1 (`<ReferenceLink>` each, first in the topic) and 3.0 (`<ReferenceLinks><ReferenceLink/>…`); `lapin-document:<document>` ties the remark to a document (`Bcf.DocumentLink`, `Documents`, `WithDocuments`) | `Lapin.Platform/Cde/Remarks/BcfModel.cs`, `BcfReader.cs`, `BcfWriter.cs` |
| `remarks.create` with `sheetId` (or `viewId`: the sheets the view is placed on) ties the new remark to those documents; `remarks.create`/`remarks.update` take `documents` (the `lapin-document:` links replaced, the others kept) or `referenceLinks` (all); `remarks.documents` lists what a remark can be tied to (each sheet, written document and the model, by the name an issue gives it, through the edition's naming hook) and what a sheet or view in front gives (`at`); `remarks.list` gives each remark's `referenceLinks` and `documents` | `Remarks.cs` |
| The document's name is exactly what `remote.submitIssue` sends in `documents`: a sheet's `Cde.Naming(m).For(m, sheet)` (its issued file's name without `_<status>_<revision>` and extension), a written document's `Cde.DocumentName`, the model's `ForModel()` | `Remarks.DocumentChoices`, `EngineDispatcher.Publishing.cs` (`IssueFor`) |
| `remote.syncIssues` syncs `reference_links` as one of the topic's fields: sent with a new topic, a merge patch only when the links changed, the server's (another tool's URL) brought here | `IssueSync.cs` |
| The Remarks dialog: **Documents** on a new remark (preset from the sheet or view in front) and on a remark (× takes one away, **Tie to a document…** adds one); other links shown read-only | `ui/src/modules/core/remarks/RemarksDialog.tsx`, `remarks.ts` (`placeInFront`, `remarkDocuments`, `withDocument`) |
| A re-issue: a `LapinIssue` made with `of` (an earlier issue's id or GlobalId) keeps that issue's number and holds `issueId` (its first record's id, the issue's GlobalId; `CdeIssues.Issue.IssueId`); its revision must come after the issue's last (`CdeIssues.CheckRevision`, before any file is made); its name is the issue's by default. `CdeIssues.AllSeries` groups the records by number; `CdeIssues.Matching` finds the issue whose last revision sent exactly the sheets, written documents and model asked; `cde.naming` answers `series` and `match`, and with `of` the number, transmittal and next revision of that issue. The register is in order of making (`at`), so a sheet's last issue is its latest revision | `Lapin.Platform/Cde/CdeIssues.cs`, `Cde.cs`, `Lapin.Protocol/CdeIssue.cs` |
| `remote.submitIssue` sends the issue's number and `issueId` (the series' GlobalId; `next` does not renumber a revision); `remote.issueTag` writes the same `issue` in the tag, on a server and on a folder remote; the server then archives the earlier Published revision | `EngineDispatcher.Publishing.cs` |
| Manage → Issue…: **Issue as** — *New revision of issue #n* by default when what is ticked matches an issue (its name, the revision after its last; P → C gives C01), *New issue*, or any earlier issue; the revision is refused (red) when it does not come after the issue's last | `ui/src/modules/core/cde/IssueDialog.tsx`, `cde.ts` (`revisionFor`, `revisionFits`, `compareRevisions`) |
| Tests | `engine/tests/Lapin.Engine.Tests/CdeRevisionTests.cs` (a remark on a sheet or a view on it tied to the submitted document name; links in BCF 2.1/3.0; re-issue in Lapin's register with its checks and order; the submission and the tag with the number and GlobalId; a remark on a sheet synced stops the submission at the fake server's gate), `IssueSyncTests.Reference_links_…`; `ui/src/modules/core/cde/cde.test.ts`, `remarks/remarks.test.ts` |

Limits: a document's name is kept in the link as it was when tied; a sheet renumbered later (another number, level or role) is another document, and the remark keeps the old name until its documents are changed. Remarks read back from PDFs (`LapinSheetRemark`) are not BCF topics and do not reach the gate. The handover diff is still the server's.

## Checks on foreign documents (W5)

Atrium's `docs/plan-cde-web.md`, stage W5, against Atrium Server's checks (its `docs/atrium-server.md` § Checks on foreign documents, schema 5): the server checks every PDF and DXF a submission issues (V1–V5: name, revision and register; the title block; empty or shifted sheets; placeholders; another project's sheets) in the background, and errors fail the publish gate. The interface shows W5's screens only when `GET /atrium/1.0/` lists `checks` (the reports, the gate's checks, Run checks again, the check settings) and `register` (the Register tab).

| What | Where |
|---|---|
| The report's types; `pollChecks` (reads `GET …/submissions/{sid}` until `checks.status` is not `pending`: waits of 1, 1.5, 2, 3, 5, 8, then 10 s, a few failed reads ridden over, stopped by an `AbortSignal` when the screen closes, 10 minutes at most); `bboxToPixels` (mm from the page's top-left × 72/25.4 × scale, pdf.js's viewport) and `bboxToFractions`; `describeFinding` (each code R-01–03, V1-01–10, V2-01–08, V3-01/02, V4-01, V5-01–04 said from its code and severity, its field and details as facts; the server's English only for a code it does not know); `badMember` (which setting a 400 of `PUT rules` names); `tryNaming` (the naming rule tried on a code, anchored as the server does, in the browser's regex) | `ui/src/engine/checks.ts` (tests: `checks.test.ts`) |
| The calls: `rerunChecks` (`POST …/checks`, 202), the register (`getRegister`, null for 404 `no_register`; `putRegister`, the file itself as the body, `text/csv` or the XLSX type; `deleteRegister`), `Rules.naming` and `Rules.checks`, `AtriumError.serverMessage` and `.body` (a 422's `topics` and `checks`, a 400's words) | `ui/src/engine/atrium.ts` (tests: `atrium.test.ts`) |
| `remote.atrium` takes `data` (base64) and `contentType` for `PUT atrium/1.0/projects/{p}/register[?name=]` only: the desktop sends the register through the engine as the file it is; any other request with bytes is refused | `EngineDispatcher.Remote.cs`, `HttpRemote.ApiAsync(raw, contentType)`; test `CdeTests.The_desktop_sends_a_document_register_as_its_file_and_no_other_request_as_bytes` |
| The screens: the checks report (its line: checking, could not run, or the counts; each file with the findings on the whole file and its findings by severity; **page n** shows the page with the findings drawn over it as boxes tagged with their codes, the finding's sentence on hover); the gate's checks beside its topics; **Run checks again** (the author or an approver, greyed once decided); the Register tab under Publishing (import CSV/XLSX, entries with a filter, columns, import warnings, delete; owners change it, members read it); **Checks on documents** in Rules (naming rule with a live test, project code, number and name, another project's keywords, expected values, future-date days, the checks run, the title block: found by labels or described with a region and fields); `checks_done` in Activity with its counts | `ui/src/modules/core/CdeChecks.tsx`, `CdePublish.tsx`, `CdeAdmin.tsx` |
| After **Submit for approval** in Manage → Issue…, the dialog polls the submission and shows the report when the checks end (a notice says whether they found errors) | `SubmittedChecks` in `CdePublish.tsx` |
| The page is drawn by pdf.js 4.10.38 (Apache-2.0), imported only when a page is first shown (`ui/src/modules/core/pdfPage.ts`): its own worker, built by Vite from this origin (`worker-src 'self'`, already in the CSP), `isEvalSupported: false`, no font or CMap fetched. The file's bytes come from `…/objects/{sha256}` once per session | `pdfPage.ts` |

Limits:

- A **DXF** is not drawn: its page shows the paper's outline, the title block and fields as the server read them (their values in their boxes), and the findings' boxes over them. Lapin's DXF reader makes model elements (an import), which would be no cheap preview; the outline is also the fallback when pdf.js cannot draw a PDF (an old web view without `Promise.withResolvers`).
- A finding's sentence is translated; its values (codes, expected texts, keywords) are shown as the server gives them. A project's own title-block fields keep their names.
- The naming rule's live test uses the browser's regex, which agrees with .NET's for the usual syntax; the server is the judge (a rule that does not compile there comes back as a 400 next to the field).
- Pending submissions are not re-checked when the register or the settings change: **Run checks again** does it, as the server's contract says.

## IDS checks (W6)

Atrium's `docs/plan-cde-web.md`, stage W6: an IDS 1.0 reader and checker in Lapin's engine (`Lapin.Ids`, written from buildingSMART's specification, no dependency), the project's `.ids` files kept in the project, run from Manage → IDS… and before Submit for approval in Manage → Issue… → On the CDE server. A failing specification stops the submission unless the user ticks "Submit despite the IDS failures" and writes why (`idsOverride`, carried in the submission's and the tag's note); the project's setting `cde.ids.blocks` lets failures through. Failing specifications become remarks (one undo step) that `remote.syncIssues` sends. Everything is in [`docs/ids.md`](ids.md); the user's page is `docs/user/ids.md`.

## IDS on the server (W6c)

Atrium's `docs/plan-cde-web.md`, stage W6c: the Lapin side of Atrium Server's W6b (its `docs/atrium-server.md` § IDS on the server, schema 6), where the server checks every submission's model (the commit's viewer IFC and the submission's `.ifc`/`.ifczip` files) against the project's IDS files and counts the failures in the publish gate. The interface shows it only when `GET /atrium/1.0/` lists `ids`. Decision 831.

| What | Where |
|---|---|
| The calls: `listIds` (`GET …/ids`), `putIds` (`PUT …/ids/{name}`, the file itself as `application/xml`), `deleteIds`, `viewIds` (`?view=json`), `dryRunIds` (`POST …/ids/run?commit=`); `problemLines` (a 400 `bad_ids`'s `problems`, each with its line); `describeIds` (IDS-02–06 said from their codes and details, the server's English only for a code it does not know); `idsGroups` (the report by IDS file and model, each failing specification with its IDS-01 details) | `ui/src/engine/serverIds.ts` (tests: `serverIds.test.ts`) |
| `putFile` (a file as its own bytes: the page's fetch in the browser, `remote.atrium` with base64 on the desktop; `putRegister` is one), `need` exported, `Rules.ids` and `idsRule` (the whole `{enabled, blocking}` object for `PUT rules`, the other member kept), the refusals `no_ids`, `bad_ids`, `too_many` | `ui/src/engine/atrium.ts`, `refusals.ts` |
| `remote.atrium` sends bytes to `PUT atrium/1.0/projects/{p}/ids/{name}` too (an encoded name ending in `.ids`, no query) and reads the same path back as bytes for a download; any other request with bytes is still refused (the dry run, the list, `?view=json`, another method or name) | `EngineDispatcher.Remote.cs` (`IdsPath`); test `CdeTests.The_desktop_sends_an_IDS_file_as_its_file_reads_it_back_and_sends_no_other_IDS_request_as_bytes` |
| The screens: Publishing → **IDS on the server** (the files with title, version, IFC versions, specification count, who and when; owners Add an IDS file… and delete; everyone downloads and opens the specifications; a refused file's problems with their lines); the two switches in Rules (`ids.enabled`, `ids.blocking`); the `ids` group after the documents in a submission's checks (counts; IDS-02–06 as notices; each IDS file over each model with Pass / Fail / Skipped and the specification's line; a failing one with its requirements, failures (element, requirement, actual, expected), the failing GlobalIds with `globalIdsTotal`, a copy button, and `more`); **Check on the server** in Manage → Issue… → On the CDE server (editors and owners: saved to the server first, then the dry run on the head commit, the same report; the project's own IDS run of W6 stays as it was) | `ui/src/modules/core/CdeIds.tsx` (one lazy chunk), `CdePublish.tsx`, `CdeChecks.tsx`, `CdeAdmin.tsx` |

**The browser budget** (brotli, as `scripts/build-web.mjs` counts; measured on 24.11.2026): origin/main before W6c, first load 20,934,683 bytes and everything 24,109,442; with W6c, first load **20,933,266** (38,254 under the 20 MB budget; nothing new loads at start) and everything **24,112,587** (4,661 under 23 MB). `CdeIds` is one lazy chunk of 3,584 bytes; the largest language file (Lao) grew by 385. No budget was raised.

End to end (headless Chrome against a local Atrium Server with W6b on a copy of the test data): an owner uploads a bad IDS and sees the 400's problems with their lines, uploads a good one, opens its specifications, downloads it, turns blocking on in the Rules; an editor saves a model with two walls, runs Check on the server in Manage → Issue… (IDS-01, its failures and GlobalIds; nothing submitted), submits; the submission's checks show IDS-01 with the server's GlobalIds, the gate fails and Approve waits; the owner approves despite the errors with a note; the report and the tab in Romanian; an editor reads the files but may not upload, delete or change the switches; the owner deletes the file.

Limits:

- The failures' words (requirement, actual, expected, the facets' texts in the specifications view) are the server's English, shown as they come; the interface around them is translated.
- A failing GlobalId is not selected in the model from the server's report (the dry run's commit is the open project, but its elements' GlobalIds are not mapped back here); Manage → IDS… does that for the project's own run.
- The IDS files on the server and those kept in the project (W6) are separate; nothing copies one to the other.
- Pending submissions are not re-checked when the IDS files or the switches change: **Run checks again** does it, as the server's contract says.
