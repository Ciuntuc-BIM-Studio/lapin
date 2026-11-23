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
| The HTTP remote: discovery, password sign-in, refresh on 401, refs with ETag, `refs/update`, `objects/missing`, PUT, resumable uploads, packs, the commits walk, viewer records | `Remote/HttpRemote.cs` |
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

- The browser's engine: the HTTP remote is synchronous (`HttpClient.Send`), which the WebAssembly engine cannot do; remote calls work on the desktop. The browser needs an async path in the worker.
- Versions as `refs/tags/v/<n>` tags and issues as `refs/tags/issue/*` (the client pushes and fetches tags, but Save version and Issue do not make them yet).
- The status bar's `↑2 ↓1`, the History tab in Manage → Versions, fetch on open and every 5 minutes, Clean up (folder GC).
- An `atrium:` author id: Foundation's `current-user` gives the e-mail address as the id, which never goes into a commit, so commits on a server are signed `local:<installation>` with the account's name until the server tells the public id (the server's ref log names the account by its `atrium:` id all the same).
