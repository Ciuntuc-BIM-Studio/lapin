# Web apps on other sites connecting to the desktop engine

A web app served from a public https origin (the first one: `https://ginnung.tech`; nothing third-party is hard-coded) may connect to the desktop engine's WebSocket on this computer, `ws://127.0.0.1:<port>/ws`, once the user lets it in. Decision 611 in `docs/decisions-pending.md`. Code: `engine/src/Host/Lapin.Host/WebConnect.cs` (origins, the list of sites, pairing), `EngineHub.cs` (the gate, message sizes), `Program.cs` (the Host and Origin checks); tests: `engine/tests/Lapin.Engine.Tests/WebConnectTests.cs`.

## What is done (stage 1, the engine)

### Who may open a connection

- **Host header.** `/ws` and `/health` answer only when `Host` is a loopback name (`127.0.0.1`, `localhost`, `[::1]`) with the port the engine listens on. A page whose own name was pointed at 127.0.0.1 (DNS rebinding) sends its own name and gets 403.
- **Origin, compared exactly.** An origin is parsed and must be a serialized origin exactly as a browser writes it (`scheme://host[:port]`, lower case, no default port spelled out, no path, user or trailing slash); scheme, host and port are compared for equality, never by prefix, so `http://localhost.evil.com`, `http://127.0.0.1.evil.com` or `http://localhost:5180@evil.com` are not the app's own.
  - **The app's own** (as before, full access): no `Origin` (a program: Python, the MCP bridge), `http://localhost:<port>`, `http://127.0.0.1:<port>`, `http://[::1]:<port>`, `tauri://localhost`, `http://tauri.localhost`, `https://tauri.localhost`.
  - **A site**: any other `https://` origin. Its connection is accepted, **paired with nothing**.
  - **Refused (403)**: anything else, `http://` sites included (their page could be changed on the way), and `null`.
- `/health` answers only the app's own (no `Origin`, or the app's own), and never carries CORS headers, so a site's page cannot read it. A web app finds the engine by trying a few ports with the WebSocket itself (14912, 14913, …, 47810 for `pnpm dev`) and `engine.hello`.

### Message sizes

Every incoming WebSocket message is bounded (`EngineHub.MaxMessage`): the app's own windows 512 MB (`MaxOwnMessage`), a site let in 100 MB (`MaxSiteMessage`), a site not let in yet 64 KB (`MaxUnpairedMessage`: it may only say hello and ask). A message over the limit closes the connection with **1009** (message too big), logged; the engine goes on. Note that a file sent inline as base64 grows by a third: an IFC of 75 MB is about the largest a site can send in one `project.importIfc`.

### The protocol for a site (the contract)

Calls are the engine's ordinary JSON-RPC text messages `{"id", "method", "params"}`; replies come as binary frames (`BinaryFrame`: a JSON header `{"id", "result"}` or `{"id", "error"}`, meshes as raw arrays), events as text `{"event", "data"}` (diffs as binary frames `{"event":"diff","data":…}`).

**Before it is let in** a site may call only:

| Call | Answer |
|---|---|
| `engine.hello` | `{"engine":"lapin","ok":true,"product":"lapin","version":"0.1.…","paired":false,"edit":false}`: no project path, pid, port or module list. |
| `pair.request {name?, edit?}` | `name`: what the site calls itself (≤ 60 characters, shown beside its origin, never alone); `edit`: whether it would like to edit (a wish shown to the user, not a grant). The reply comes when the user answers: `{"granted":true,"edit":bool,"remembered":bool}` or `{"granted":false,"reason":"denied"|"timeout"}`. A site on the user's list is let in at once (`remembered: true`). A site refused (or revoked) is answered `denied` without asking again for 10 minutes (`WebConnect.DenyCooldown`). No answer in 2 minutes (`WebConnect.AskTimeout`): `timeout`. Several tabs of one site asking together wait on the same question. |

Every other call is answered `{"error":"This site is not connected to Lapin: ask the user with pair.request first."}`, and the connection gets no diffs, no presence and no events.

**Once let in** the site is a guest of the engine, like a shared session's guest (`EngineDispatcher.AllowedRemote`, `docs/plan-multiplayer.md`), with a participant of its own (`w1`, `w2`, …, named "‹name› (‹host›)") that shows among the participants, its own undo and redo, and the diffs of every change:

- **Read** (the grant every site starts with; the role `reader`): `session.info`, `session.catalogue`, `doc.snapshot`, `doc.query`, `quantities.of`, `quantities.many`, `view.caps`, `view.fine`, `pointcloud.tiles`, `project.versions`, `project.compareVersions`, and the exports as bytes (`export.pdf`, `export.dxf`, `project.exportIfc`, `project.exportMesh`, `project.exportSurvey`, `project.exportHtml`, `project.htmlPlan`, `project.exportBcf`). A reader changes nothing, not even the site progress a shared session's viewer may mark.
- **Edit** (a separate grant; the role `editor`): also `doc.execute` (one command, one undo step; several commands as one undo step through `edit.batch {steps:[{command,args}…], label?}`), `doc.undo`, `doc.redo`, `project.convertToNative`, and the imports with their bytes inline (`project.importIfc {data}`, …).
- `presence.update` and `engine.hello` (still redacted, with `paired: true` and the grant).
- **Never**, with either grant: `script.*`, `extensions.*`, `share.*`, `measure.*`, `cancel.*`, `presence.hello`, `pair.answer`, `sites.*`, `doc.begin`/`doc.commit`/`doc.rollback`, a project's scripts and graphs (`scripting.*`, `graphs.*` commands and queries, alone or in a batch), anything that opens, saves or changes the project (`project.open`, `project.save`, `project.new`, …), `recovery.*`, `support.*`, and any call that names a file or folder on this computer (a `path`, `paths`, `folder`, `directory` or `dir` anywhere in its params); `HostFiles` refuses a guest's file access a second time below the protocol.

### The user's side (host-only calls, for the window and Preferences)

| Call or event | Shape |
|---|---|
| event `pair.ask` | `{"requestId","origin","name","wantsEdit"}`, to every window of this computer. |
| `pair.answer {requestId, decision, edit}` | `decision`: `"once"` (this connection only), `"always"` (written to the list), `"deny"`; `edit`: grant editing too. Answer `{"origin","decision","edit","connections"}`. |
| event `pair.done` | `{"requestId","origin","decision","edit"?}` after an answer, or with `decision` `"withdrawn"` (the site went away), `"timeout"`, `"deny"` (revoked while asking): the other windows close their dialog. |
| `sites.list` | `{"sites":[{"origin","edit","addedAt","connected"}],"connections":[{"origin","name","edit","remembered","participant"}]}`: the list, and the sites connected now (those let in once among them). |
| `sites.set {origin, edit}` | Puts a site on the list or changes its edit grant (its date kept); its live connections take the new grant at once. The origin may be typed loosely (`https://Ginnung.tech/`); only an https site that is not the app's own. Answers as `sites.list`. |
| `sites.revoke {origin}` | Takes the site off the list and drops every connection of that origin at once (closed with 1008, "The user revoked this site's access to Lapin."); asked again, it is refused for 10 minutes unless the user puts it back with `sites.set`. Answers as `sites.list` with `revoked` (whether it was on the list) and `dropped` (connections closed). |
| event `sites` | As `sites.list`, after any change (a site let in, a connection gone, a set, a revoke). |

The list is `connected-sites.json` in the user's folder (`--user-dir`), empty by default: `{"version":1,"sites":[{"origin":"https://ginnung.tech","edit":false,"addedAt":"2026-11-16T…Z"}]}`. An entry that is not a site's origin as a browser sends it is ignored; an unreadable file lets no site in. These calls are the host's own (past the queue of calls, like `share.*`), not commands, so they are not in the command catalogue.

## Next stages

1. **The interface**: the question as a dialog in the window (`pair.ask` → Allow once / Always allow / Deny, with "Let it edit" off by default and the origin shown large), and Preferences → Connected sites built on `sites.list`, `sites.set`, `sites.revoke` and the `sites` event; the participants' bar showing sites as such. New strings in every language of `ui/src/i18n`, the user guide's page and scene.
2. **Links from inline data**: a link (`docs/links.md`) reloaded from IFC bytes a site sends, not from a path on this computer, so a web app can keep a linked model up to date.
3. Later, if asked: a grant narrower than read (one view, no exports), and an expiry on "Always allow".
