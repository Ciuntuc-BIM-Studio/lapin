# Plan: the Converge IFC connector

Lapin sends the open project as IFC to a **Converge IFC server** (ifc-hub), an ISO 19650 common data environment for IFC files, as a new file or a new version of one, and fetches versions of the files there into the IFC import. The module is open (MIT) and brings no dependency.

## Why

- A firm whose deliverables live in a CDE can issue from Lapin and follow the other disciplines' models without a trip through the file system.
- It does not overlap with Atrium (`docs/plan-atrium-cde.md`). Atrium keeps the `.lapin` project itself, with its history; this connector exchanges **IFC deliverables** with a server that manages files, revisions, review states and BCF issues. Speckle (`docs/plan-speckle.md`) exchanges converted objects; this exchanges the IFC Lapin's own export writes.

## The module

- **Engine**, `engine/src/Modules/Exchange/Lapin.ConvergeIfc` (`convergeifc`, desktop only, plain `HttpClient`). It only carries files: the IFC is made by `project.exportIfc` and read by the IFC import. All its calls are queries (nothing of the project changes, so there is no undo step), and all are the host's alone (`EngineDispatcher.HostOnlyQueries`): the account is the host's, and the calls read and write the host's files.

| Query | Does |
|---|---|
| `convergeifc.accounts`, `.setAccount`, `.forgetAccount` | The account (server and token), checked against the server, held in memory only. |
| `convergeifc.projects`, `.files`, `.versions` | The account's projects (with whether it may upload), a project's IFC files, a file's versions. |
| `convergeifc.tempPath` | A path in a folder of the connector's own, to export to before a send. |
| `convergeifc.send` | A file sent as a new file or a new version; `discard` removes it after, if the connector made it. |
| `convergeifc.fetch` | A version downloaded, its SHA-256 checked, an `.ifczip` unpacked; to `path` when given, replacing it only once the download is whole. |
| `convergeifc.discard` | Removes a file the connector made; any other path is left alone. |

- **Interface**, `ui/src/modules/convergeifc`: File → Collaborate → **Converge IFC: Send…** and **Get…**. The token is kept in the system's secure store under `convergeifc:<server>` (the shell's `secret_*`). The server's address, the last project and file, and where each file was last saved are settings.

## Security

- `https` only (`http` for a server on this computer), so a token does not cross a network in the clear. No default server.
- The token is never written into the project, a setting, a report or a log, and never told back by a query.
- A fetched file is kept only when its checksum matches the server's. An `.ifczip` entry's name is never used as a path, and what it unpacks to is limited.

## The server's API

| Call | Used for |
|---|---|
| `GET /api/v1/me` | who the token is, the projects and the roles in them |
| `GET`, `POST /api/v1/projects/{id}/containers` | a project's files; a new file (multipart field `file`) |
| `GET /api/v1/containers/{id}` | one file as it stands |
| `GET`, `POST /api/v1/containers/{id}/versions` | the versions; a new version |
| `GET /api/v1/containers/{id}/versions/{n}/file` | a version's bytes |

Authentication is `Authorization: Bearer <personal API token>`. Refusals carry a `message`, shown to the user.

## Tests

- `ConvergeIfcTests` (xUnit) against an in-memory server that answers as ifc-hub does. One test runs against a real server, only when `LAPIN_CONVERGE_TEST_URL`, `LAPIN_CONVERGE_TEST_TOKEN` and `LAPIN_CONVERGE_TEST_PROJECT` are set.
- `ui/src/modules/convergeifc/convergeifc.test.ts` (vitest) for the interface's helpers.

## Known limits

- An IFC export carries its time stamp, so a second send of an unchanged project is a new version. "Nothing sent" applies only when the very same bytes are already current.
- The whole project is sent; there is no sending of a view or a selection.
- `.ifcx` files of a server are not offered: Lapin does not read them.
- Issues (BCF) are not exchanged by this module.
- The user guide has no picture of the dialogs yet (`scripts/user-docs/scenes.mjs`).

## Possible later

- BCF issues through the server's BCF API.
- Sending what a view shows, or a selection.
- Remembering, per project, which server file it is sent to.
