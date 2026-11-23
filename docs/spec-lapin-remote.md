# lapin-remote 1.0: the wire specification

**Status:** draft for implementation, 23.11.2026. Part of decision 793 (`docs/plan-atrium-cde.md`, which this document makes exact). Licence: MIT, like the rest of lapin-pro.

**Implemented by:** Lapin's client (C#, `engine/src/Platform/Lapin.Package/Remote/`, the folder remote included) and Atrium Server (C#, the Atrium repository, `src/Atrium.Server/Remote/`). Anyone may write a third client or server from this document alone.

**Conformance vectors:** `docs/spec-lapin-remote/vectors/` (its README says how to run them). Where this text and a vector disagree, it is a bug in one of them: report it, never "fix" one side alone.

The key words MUST, MUST NOT, REQUIRED, SHOULD, SHOULD NOT, MAY and OPTIONAL are to be read as in RFC 2119 and RFC 8174 when, and only when, they appear in capitals.

---

## Contents

1. Overview
2. Object ids
3. Canonical JSON
4. Objects: blob, tree, commit, tag
5. Refs
6. The local history inside a `.lapin`
7. The folder remote
8. Operations: push, fetch, clone, pull
9. HTTP: common rules
10. HTTP: the endpoints
11. What a server verifies
12. The assembled package and the viewer copy
13. Garbage collection
14. Versions and capabilities
15. Security
16. Conformance
- Appendix A: error codes
- Appendix B: details this specification decided beyond the plan

---

## 1. Overview

A Lapin project's history is a graph of **objects** addressed by the SHA-256 of their bytes, as in git:

- a **blob** is one part of the `.lapin` package (an Arrow table, `project/document.json`, a script) or one file an issue made (a PDF, a CSV);
- a **tree** is a project state: the package's manifest, listing every part by path, hash and size;
- a **commit** names a tree, its parents, an author, a moment and a message;
- a **tag** names a commit as an ISO 19650 issue, or as a numbered version of the project.

**Refs** are the only mutable state: names (`refs/heads/main`, `refs/tags/issue/7-P02`) that each hold one object id, moved by **compare-and-swap**.

A **remote** holds the objects and refs of projects. It is either a **folder** (§7: a local disk, a NAS, a synced folder) or a **server** speaking the HTTP API of §9–§10 under `/lapin-remote/1.0/`, beside OpenCDE Foundation 1.1 and BCF 3.0 on the same host, with the same accounts, tokens and projects.

Transfers move only missing objects: a push asks the remote which hashes it lacks (`objects/missing`), uploads only those, then moves refs; a fetch walks commits back to what it knows and downloads only the objects it lacks. Every receiver rehashes every object it is given.

---

## 2. Object ids

- An object's **id** is the SHA-256 (FIPS 180-4) of the object's bytes, **uncompressed**, written as **64 lowercase hexadecimal characters** (`[0-9a-f]{64}`).
- For a blob the bytes are the part's bytes exactly as they are stored in the package (the bytes the package's manifest hashes: `manifest.json`'s `parts[].sha256` today). For a tree, a commit and a tag the bytes are the object's canonical JSON (§3), UTF-8, with no byte-order mark and no trailing newline.
- Compression in transport (`Content-Encoding`) or storage (gzip on a folder, deflate in a zip) never changes an id.
- An id in a URL, a JSON body or a file name MUST be lowercase. A receiver MUST refuse an id with uppercase letters or of another length (HTTP: 400 `bad_id`), never fold its case.
- The empty blob is a valid object: `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.
- Ids are compared as strings, ordinally.

---

## 3. Canonical JSON

Trees, commits, tags and the folder remote's ref entries are JSON in the **JSON Canonicalization Scheme, RFC 8785 (JCS)**. In short, and normatively where RFC 8785 is not more precise:

1. No whitespace outside strings.
2. Object members sorted by their names as **arrays of UTF-16 code units**, compared unsigned, lexicographically (so `"😀"` (D83D DE00) sorts before `"דּ"` (FB33), unlike code-point order). The sort is recursive; array order is kept as given.
3. Strings: UTF-8 output; only `"` and `\` and the controls U+0000–U+001F are escaped; the controls as `\b \t \n \f \r` where those exist, else `\u00XX` with **lowercase** hex. Everything else is written raw: `/`, U+2028, U+2029, every non-ASCII character. No Unicode normalisation (`é` and `e`+U+0301 stay different). A lone surrogate is invalid.
4. Numbers: the ECMAScript `Number.prototype.toString` form of the IEEE-754 double (`1e+21`, `1e-7`, `-0` as `0`, `1.0` as `1`). NaN and infinities are invalid.
5. `true`, `false`, `null` as such.

Further rules for lapin-remote objects:

- A **duplicate member name** in any object makes the JSON invalid.
- Every number **defined by this specification** (sizes, counts, versions) MUST be an integer in [0, 2^53 − 1]. Unknown members (below) may hold any JSON, so implementations MUST implement RFC 8785 fully, numbers included.
- **The stored bytes are the object.** An object's bytes MUST equal the JCS serialisation of their own parse. A receiver checks it (parse, re-canonicalise, compare bytes) and refuses an object that is not canonical (HTTP: 422 `invalid_object`). A reader MUST NOT re-serialise an object it did not create: it keeps and forwards the bytes it received.
- **Unknown members are kept.** A reader MUST accept members it does not know in a tree, commit or tag of a version it knows (§14), MUST keep them (by keeping the bytes), and they are part of the hash. This is how a 1.x writer adds fields without breaking history. The vector `objects/commit-future.json` has two.
- A warning to C# implementers: `System.Text.Json` with default options escapes non-ASCII and `+`, `<`, `>`, `&`, `'`, and writes numbers in .NET's own form. A JCS writer must serialise strings and numbers itself (or use `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` for strings, which still escapes some characters and is therefore not enough); the vectors under `canonical/` catch the differences.

---

## 4. Objects

There are four kinds. A blob has no header: its kind is known from what names it (a tree's part, a tag's file). A tree, commit or tag is recognised by its `format` member.

| Kind | `format` | Named by | Bytes |
|---|---|---|---|
| blob | — | a tree's `parts[].sha256`, a tag's `files[].sha256`, a viewer record's `ifc` | any bytes |
| tree | `"lapin-project"` | a commit's `tree` | canonical JSON (§4.2) |
| commit | `"lapin-commit"` | a ref under `refs/heads/`, a commit's `parents`, a tag's `commit` | canonical JSON (§4.3) |
| tag | `"lapin-tag"` | a ref under `refs/tags/` | canonical JSON (§4.4) |

Every tree, commit and tag MUST be at most **16 MiB** (16 777 216 bytes). A blob MUST be at most the server's `maxObject` (§14; default 2 GiB).

### 4.1 Blob

The bytes of a part or of an issued file, exactly as the package holds them. A receiver never parses a blob. (Parts are Arrow IPC tables, JSON, UTF-8 text; the protocol does not care.)

### 4.2 Tree

A tree is the package's manifest (`PackageManifest` in `Lapin.Package/LapinPackage.cs`, as `manifest.json` holds it, camelCase member names) **without `foreign`**, as canonical JSON. `manifest.json` in a `.lapin` is indented and may carry `foreign`; the tree is not that file's bytes, it is its canonical form without `foreign` (the same rule as a version's manifest, `docs/versions.md`).

| Member | Type | Req. | Meaning |
|---|---|---|---|
| `format` | string | yes | `"lapin-project"` |
| `version` | integer | yes | the package format: `3` |
| `app` | string | yes | the Lapin build that wrote it (`"0.1.1612"`) |
| `product` | string | yes | the edition (`"lapin"`) |
| `modules` | array of `{id: string, schemaVersion: integer}` | yes | the modules the writer had loaded |
| `parts` | array of part | yes | every part of the project state, in the writer's order (the order is part of the hash; Lapin writes `project/document.json`, then the model chunks by module, class, storey, then `model/relations.arrow`, then the text parts by path) |
| `classes` | object, string → string | yes | which module wrote each class |
| `foreign` | — | **forbidden** | other tools' entries are never part of a project state (decision of `docs/format-lapin.md`) |
| anything else | any | no | kept (§3) |

A **part** is `{path: string, sha256: id, size: integer, module?: string}`; `module` is absent (never `null`) when the part has no module (Lapin's `WhenWritingNull`).

Path rules (the rules of `PackageForeign.Keeps`, plus Lapin's own names):

- relative, `/`-separated, 1 to 1024 bytes of UTF-8; no `\`, no NUL; no empty, `.` or `..` segment; no leading or trailing `/`;
- the first segment, compared without case, MUST NOT be one of `manifest.json`, `versions`, `issues`, `history`, `derived`, `journal` (they are never parts of a project state);
- no two parts with the same path (compared ordinally).

Example: `vectors/objects/T2.json` (707 bytes, id `941f7c834ec70c7171289afba2fc9d5042cab97306759b2c9979f34be04a7c26`):

```json
{"app":"0.1.1612","classes":{"IfcWall":"core"},"format":"lapin-project","modules":[{"id":"core","schemaVersion":1},{"id":"scripting","schemaVersion":1}],"parts":[{"path":"project/document.json","sha256":"5e0b1db2…","size":40},{"module":"core","path":"model/core/IfcWall/_.arrow","sha256":"40aff2e9…","size":256},{"path":"model/relations.arrow","sha256":"06102ac0…","size":64},{"module":"scripting","path":"scripts/hello.py","sha256":"0322c09d…","size":23}],"product":"lapin","version":3}
```

(Hashes shortened here; the file has them whole.)

### 4.3 Commit

| Member | Type | Req. | Meaning |
|---|---|---|---|
| `format` | string | yes | `"lapin-commit"` |
| `v` | integer | yes | `1` |
| `tree` | id | yes | the tree |
| `parents` | array of id | yes | `[]` for a first commit, one usually, two for a merge (from S4); distinct; at most 16 |
| `author` | `{id: string, name: string}` | yes | `id`: `<scheme>:<value>`, scheme `[a-z][a-z0-9]{0,15}`, value `[A-Za-z0-9._-]{1,128}`; 1.0 defines `atrium:<user id on the server>` and `local:<random id of the installation>`. `name`: 1–256 characters. **No e-mail address**, in this or any other member |
| `at` | string | yes | when the commit was made, UTC, RFC 3339 as `YYYY-MM-DDTHH:MM:SSZ` or `YYYY-MM-DDTHH:MM:SS.sssZ` |
| `message` | string | yes | may be empty; at most 65 536 bytes of UTF-8 |
| `app` | string | no (Lapin writes it) | the Lapin build |
| `product` | string | no (Lapin writes it) | the edition |
| `entities` | integer | no (Lapin writes it) | the number of entities of the tree |
| anything else | any | no | kept (§3) |

**The commit's id** is the SHA-256 of its canonical JSON bytes (§2), unknown members included.

A commit carries **no viewer copy**: the plan's `viewer` member is not part of the commit (Appendix B, B1). A viewer copy is a separate, replaceable record (§12.2).

Example, `vectors/objects/c1.json` (305 bytes, id `fedb729f0255c515f1716fa24c7a47eeeeb4df20007dd8bc967056da0c35aeb1`):

```json
{"app":"0.1.1612","at":"2026-11-23T10:00:00Z","author":{"id":"atrium:5d2c8a4e-1f3b-4c6d-9e7a-0b1c2d3e4f50","name":"Mihai Ionescu"},"entities":0,"format":"lapin-commit","message":"Initial model","parents":[],"product":"lapin","tree":"7e4c2e2a044109912f4dd8ad2e65e9c4e6946d015a4c38419ef7b39ac32ef0df","v":1}
```

The server does not check that `author` is the account that pushes the commit: a writer may push a colleague's commits (taken from a folder, for instance). Who moved a ref is recorded by the server in the ref log (§10.5), which is the audit trail.

### 4.4 Tag

Common members:

| Member | Type | Req. | Meaning |
|---|---|---|---|
| `format` | string | yes | `"lapin-tag"` |
| `v` | integer | yes | `1` |
| `commit` | id | yes | the commit it names |
| `kind` | string | yes | `"issue"` or `"version"` in 1.0 |
| `number` | integer ≥ 1 | yes | the issue's number, or the version's number (the `<n>` of its ref) |
| `author` | as in a commit | yes | |
| `at` | as in a commit | yes | |
| `note` | string | no | at most 65 536 bytes |
| anything else | any | no | kept |

`kind: "issue"` (an ISO 19650 issue; the `LapinIssue` record of `Cde/CdeIssues.cs`) adds, all required:

| Member | Type | Meaning |
|---|---|---|
| `issue` | string | the `LapinIssue` entity's GlobalId (22 characters) |
| `revision` | string | `^[PC][0-9]{2,3}$` (`CdeRevisions`) |
| `status` | string | `^(S[0-7]\|A[1-9]\|B[1-9]\|CR)$` |
| `container` | string | `"WIP"`, `"Shared"`, `"Published"` or `"Archived"` |
| `files` | array of `{name: string, sha256: id, size: integer}` | the issued files, blobs that travel with the tag; `name` 1–255 characters, no `/`, `\` or NUL; may be empty |

`kind: "version"` (a numbered version of the project, `docs/versions.md`) adds `name` (string, required, 1–256 characters).

Example, `vectors/objects/tag-issue.json` (496 bytes, id `336f44b81f7218e8d60240bb8beea12a4c37612e49c892131fddf1cc193fac67`):

```json
{"at":"2026-11-23T10:50:00Z","author":{"id":"atrium:5d2c8a4e-1f3b-4c6d-9e7a-0b1c2d3e4f50","name":"Mihai Ionescu"},"commit":"ce2c4e189b12a9e5bf3498d026dd8a8c465b546bf2ed29ffe644f6c5813a3c94","container":"Shared","files":[{"name":"PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf","sha256":"73fa0eeab0127616e19bacf37ac6cf241f8156c89aa6f851ad030b306c05728c","size":42}],"format":"lapin-tag","issue":"3vB2YO$MX4xv5uCqZZG05x","kind":"issue","note":"For coordination","number":1,"revision":"P01","status":"S2","v":1}
```

A tag object is immutable like every object. What can change is the ref that names it, under the rules of §5.3: an issue tag's ref never moves; a version's ref moves when the version is updated or renamed (a new tag object each time).

### 4.5 The closure of an object

The **closure** of a commit is the commit, its tree, every part the tree names, and the closure of every parent. The closure of a tag is the tag, every file it names, and the closure of its commit. A remote is **complete** when, for every ref, the closure of its value is held. A server keeps itself complete (§11); a local history may hold incomplete commits (§6.4).

---

## 5. Refs

### 5.1 Names

A ref name is ASCII, at most 255 bytes, and matches exactly one of:

| Ref | Grammar | Points to |
|---|---|---|
| the main branch | `refs/heads/main` | a commit |
| a personal branch | `refs/heads/wip/<seg>/<seg>` | a commit |
| a version | `refs/tags/v/<n>` | a tag of kind `version`, `number` = n |
| an issue | `refs/tags/issue/<n>-<rev>` | a tag of kind `issue`, `number` = n, `revision` = rev |
| reserved (S3) | `refs/proposals/<…>` | not in 1.0 |

where `<seg>` is `[a-z0-9][a-z0-9._-]{0,63}` without `..`, `<n>` is `[1-9][0-9]{0,8}` and `<rev>` is `[PC][0-9]{2,3}`.

- The first `<seg>` of a wip branch is the author's slug (Lapin: the account's display name, lowercased, non-alphanumerics to `-`: `ana-popescu`); the second is the branch's name (Lapin: `YYYY-MM-DD-HHMM`, a `-2`, `-3` suffix when taken).
- Any other name is refused (HTTP: 400 `bad_ref_name`). Free branch names and `refs/proposals/` come with later minor versions, announced in the capabilities (§14).
- Names are compared ordinally and are case-sensitive; the grammar allows only lowercase where case could collide on a file system.

### 5.2 Values

A ref's value is an id, or absent (the ref does not exist). In JSON an absent value is `null`. A ref under `refs/heads/` MUST point to a commit, one under `refs/tags/` to a tag whose `kind`, `number` and `revision` agree with the name (else 422 `wrong_kind` or `tag_mismatch`).

### 5.3 What may happen to each ref

| Ref | Create (`old: null`) | Move | Delete (`new: null`) |
|---|---|---|---|
| `refs/heads/main` | the project's `rules.main` role or above | fast-forward only (`new` descends from `old`), same role; a non-fast-forward move only by an owner with `force: true` | never (422 `protected_ref`) |
| `refs/heads/wip/…` | editor or above | its creator (the `by` of the ref's first log entry) or an owner; any move, no fast-forward rule | its creator or an owner |
| `refs/tags/v/<n>` | editor or above | editor or above, to any tag of the same `number` | editor or above |
| `refs/tags/issue/<n>-<rev>` | the project's `rules.issueTags` role or above (default owner) | **never**, not even by an owner (422 `tag_immutable`) | owner only; the deletion is logged like every update |

Roles are Atrium Server's per-project roles, which are this specification's rights: **viewer** = reader, **editor** = writer, **owner** = admin (`Role` in `Atrium.Server/Data/Projects.cs`). A server administrator is an owner of every project. `rules.main` is `"editor"` (default) or `"owner"`; `rules.issueTags` is `"owner"` (default) or `"editor"`; both are project settings read from `GET /projects/{id}` (§10.2).

### 5.4 Local refs

Inside a `.lapin` a project also keeps `HEAD` (the branch it is on) and, per remote, the refs as last seen (`remotes.<name>.refs` in `history/refs.json`, §6.2): enough to show "2 ahead, 1 behind" offline. These are never sent.

---

## 6. The local history inside a `.lapin`

### 6.1 Entries

The history lives in the project's file, in zip entries under `history/` (exact paths, case-sensitive):

| Entry | Bytes |
|---|---|
| `history/HEAD` | the current branch's ref name, ASCII, no newline: `refs/heads/main`. Readers trim surrounding whitespace |
| `history/refs.json` | the local refs, the remotes' last-seen refs and the local ref log (§6.2), JSON |
| `history/index.json` | the index of the objects the history names (§6.3), JSON |
| `history/commits/<id>` | a commit or a tag, its canonical bytes |
| `history/objects/<id>` | a tree (always), or a blob no other place in the file holds (§6.4) |
| `history/remotes.json` | the remotes (§6.5), JSON, **never a token** |

`refs.json`, `index.json` and `remotes.json` are not objects: any valid JSON (UTF-8, no BOM) with the members below; Lapin writes them indented. Readers ignore members they do not know; writers keep unknown top-level members they read.

The entries are a Lapin name: a Lapin that knows this specification adds `history` to the names it owns (`PackageForeign.Owned`). An older Lapin keeps the folder byte for byte as another tool's entries (`docs/format-lapin.md`, Părți străine). The package format stays 3.

### 6.2 `history/refs.json`

```json
{
  "format": "lapin-history-refs",
  "v": 1,
  "refs": {
    "refs/heads/main": "ce2c4e18…",
    "refs/tags/issue/1-P01": "336f44b8…",
    "refs/tags/v/3": "a228d75c…"
  },
  "remotes": {
    "origin": {
      "refs": { "refs/heads/main": "ce2c4e18…", "refs/tags/issue/1-P01": "336f44b8…" },
      "etag": "\"r-8\"",
      "fetchedAt": "2026-11-23T10:52:00Z"
    }
  },
  "log": [
    { "name": "refs/heads/main", "old": "fedb729f…", "new": "ce2c4e18…", "at": "2026-11-23T10:42:00Z", "by": { "id": "atrium:5d2c…", "name": "Mihai Ionescu" }, "why": "commit" }
  ]
}
```

- `refs`: every local ref, name → id. A missing name is an absent ref.
- `remotes.<name>`: per remote of `remotes.json`, the refs as the last successful fetch or push left them, the refs ETag (§10.3) and when; on a folder remote also `seenDetached`, the detached ref entries (§7.3) this client has handled.
- `log`: every local ref move, oldest first, `why` one of `commit`, `pull`, `push`, `restore`, `version`, `issue`, `branch`, `discard`, `clone`. Entries older than 30 days MAY be dropped when they are not the last entry of their ref; what they alone kept reachable may then be dropped too (§13).

### 6.3 `history/index.json`

```json
{
  "format": "lapin-history-index",
  "v": 1,
  "objects": [
    { "id": "0322c09d…", "kind": "blob", "size": 23 },
    { "id": "336f44b8…", "kind": "tag", "size": 496 },
    { "id": "941f7c83…", "kind": "tree", "size": 707 }
  ],
  "absent": [
    { "id": "535ec7ad…", "kind": "blob", "size": 24 }
  ]
}
```

- `objects`: every object reachable from the local refs (and from the log entries kept) that the file holds, wherever it holds it, sorted by id. `kind` is `blob`, `tree`, `commit` or `tag`.
- `absent`: objects reachable but not held (an older Lapin dropped them, or a later "shortened history" option left them on the server), sorted by id; `size` when known. A commit whose closure has an absent object is **incomplete**: it shows in the history but cannot be restored until a fetch brings the object.
- The index is a convenience: the zip's own directory and the location rule (§6.4) are the truth. On open, Lapin checks the index against them (*fsck*); a mismatch is repaired at the next save and never stops the project from opening.

### 6.4 Where each object is (the once rule)

An object is held **once** in the file, in the first of these places that applies:

1. **a commit or a tag**: `history/commits/<id>`;
2. **a tree**: `history/objects/<id>` (trees are small, and `manifest.json` is not a tree's bytes);
3. **a blob**, the first of:
   1. a live part: an entry whose path and hash the live `manifest.json` lists in `parts`;
   2. `versions/blobs/<id>`;
   3. `issues/blobs/<id>`;
   4. `history/objects/<id>`.

A reader looks a blob up in that order, and MUST verify its hash before using it. A writer writes a blob to `history/objects/` only when none of the first three holds it, and MUST NOT write it twice. `versions/index.json` goes on being written as today, derived from the `refs/tags/v/*` tags, for older Lapins.

### 6.5 `history/remotes.json`

```json
{
  "format": "lapin-history-remotes",
  "v": 1,
  "remotes": [
    { "name": "origin", "kind": "server", "url": "https://cde.example.com", "project": "0b6f1c2e-5a7d-4c1e-9f3a-2d8e6b4a1c90" }
  ]
}
```

- `name`: `[a-z0-9][a-z0-9._-]{0,31}`; Lapin names the first one `origin`. 1.0 clients use at most one remote (plan §6).
- `kind`: `"server"` or `"folder"`.
- `url`: for a server, the server's root (`https://host[:port][/path]`, where `/foundation/versions` is found); for a folder, a `file:` URI of the folder holding `lapin-remote.json` (`file:///Volumes/NAS/Lapin`, `file:///C:/Projects/Remote`).
- `project`: the project's id on that remote.
- The URL MUST NOT carry user information, a query or a fragment. **No token, password, cookie or e-mail address is ever written here or anywhere in the file** (§15).

---

## 7. The folder remote

### 7.1 Layout

```
<folder>/lapin-remote.json                            {"format":"lapin-remote","v":1}
<folder>/<project>/project.json                       the project (below)
<folder>/<project>/objects/<ab>/<id>.gz               a blob or a tree, gzip (RFC 1952) of its bytes; <ab> = the id's first two characters
<folder>/<project>/commits/<id>.json                  a commit or a tag, its canonical bytes (not compressed)
<folder>/<project>/refs/<ref name>/<entry>.ref        the append-only log of one ref (§7.3)
<folder>/<project>/viewers/<commit id>.json           a viewer record (§12.2), replaceable
<folder>/<project>/lock                               the lock, while a client updates refs or collects garbage
```

- `<project>` is the project's id: a UUID, lowercase, as a server would give it, so a project can move between a folder and a server with the same ids (a server SHOULD offer to import a folder project; the objects and the ref chains copy as they are).
- `lapin-remote.json` and `project.json` are canonical JSON. `project.json` is `{"format":"lapin-remote-project","v":1,"id":<project>,"name":string,"defaultRef":"refs/heads/main","createdAt":time}`, plus `rules` as a server's (§10.2), which a folder cannot enforce: anyone who can write the folder is an owner.
- A client MUST refuse a folder whose `lapin-remote.json` has a `v` it does not know.
- Files are written to a temporary name in the same directory (`.<name>.<random>.tmp`), flushed, then renamed. An object or a commit whose final name exists is not written again (the temporary file is deleted). Readers ignore names starting with `.`.
- A reader gunzips an object and verifies its hash and, for JSON objects, its canonical form; an object that fails is treated as absent and reported, never used.

### 7.2 The lock

- `<project>/lock` is created **exclusively** (`FileMode.CreateNew`, `O_CREAT|O_EXCL`) with `{"by": <author id>, "name": <display name>, "host": <computer name>, "at": <time>}`.
- It is held only for the time of one ref update (§7.4) or one garbage collection (§13), and deleted after it, also on failure.
- A lock older than **10 minutes** (by its `at`, or the file's modification time when unreadable) is stale: a client MAY delete it and take a new one, and MUST tell its user ("the remote's lock from <name> on <host> was older than 10 minutes and was broken").
- On a local disk, a NAS or a share (SMB, NFS) exclusive creation is atomic, so the lock makes compare-and-swap exact. On a **synced folder** (OneDrive, SharePoint sync, Dropbox, iCloud) it is not atomic between computers: there the lock only narrows the window, and the append-only log (§7.3) detects after the fact what the lock did not prevent. Nothing is lost either way.

### 7.3 Refs as append-only logs

A ref is not a file that is rewritten: it is a directory, `refs/<ref name>/` (so `refs/heads/main/`, `refs/tags/issue/1-P01/`), holding one file per update, never changed or deleted.

- **File name**: `<seq>-<new>-<author key>.ref`, where `<seq>` is the entry's sequence number, 8 decimal digits zero-padded (`00000001` … `99999999`); `<new>` is the new value's id, or 64 zeros for a deletion; `<author key>` is the author's id with `:` replaced by `-` (`local-9f2b6c1d0e8a4b7c`). A file name is parsed by position: 8 digits, `-`, 64 characters, `-`, the rest up to `.ref`.
- **Content**: canonical JSON `{"format":"lapin-ref","v":1,"name":<ref name>,"seq":integer,"old":id|null,"new":id|null,"author":{"id","name"},"at":time}`, plus `"force":true` for an owner's non-fast-forward move of main. An entry whose content does not parse, or whose `name` or `seq` disagree with its path, is ignored and reported.
- On a folder the author's id is the installation's `local:<id>` (a random 16 hex digits Lapin makes once per installation and keeps in its settings, not in projects), or the `atrium:` id when the user is signed in to a server for the same project.

**Resolving a ref** (every reader does the same; the vector is `folder/fork-detection.json`):

1. Read every valid entry of the directory.
2. Start with `value = null`, `seq = 0`, an empty chain.
3. The candidates are the entries with `seq = seq + 1` and `old = value`. If there are none, stop.
4. If there is one, it is the next link. If there are several (a **fork**), the next link is the one with the earliest `at` (compared as instants), and among equal `at` the smallest file name (ordinal). Append it; `value = its new`; `seq = seq + 1`; go to 3.
5. The ref's value is `value` (`null`: the ref does not exist, or was deleted). Every entry not on the chain is **detached**.

**A detached entry loses nothing.** Its `new` (and `old`) stay reachable for garbage collection for as long as the entry exists, which is forever. The client whose author id equals a detached entry's `author.id`, when it first sees it, MUST:

- for an entry under `refs/heads/`: create `refs/heads/wip/<its slug>/<name>` with `old: null`, `new:` the detached entry's `new`, and tell its user that the remote had moved ("Ana pushed at 10:51; your work is kept as branch wip/…; pull");
- for an entry under `refs/tags/`: tell its user that the tag was not taken (the winner's stands);

and then remember (locally, in `history/refs.json`'s `remotes.<name>`, member `seenDetached`, a list of file names) that it has done so. Other clients only show the fork.

### 7.4 Compare-and-swap on a folder

To apply `{name, old, new}` (several updates: all under one lock, each as below; a folder cannot make them atomic, so a client applies them in the order given and reports the ones that failed):

1. take the lock (§7.2);
2. resolve the ref (§7.3); if its value is not `old`, release the lock and fail as a server's 409 would (`ref_moved`, with the current value);
3. check the rules of §5.3 a folder can check (fast-forward on main, an issue tag never moves);
4. write the entry with `seq` = the chain's length + 1 (temporary name, then rename);
5. resolve again; if the new entry is not on the chain (a synced copy of another computer's entry arrived meanwhile and won), report `ref_moved` as in 2, the entry staying detached;
6. release the lock.

The objects are written before the refs, as on a server: all of the update's closure that the folder lacks (§8.1), then the ref entries.

---

## 8. Operations

These are the client's algorithms. A server implements the endpoints of §10; these sections say in which order a client calls them. "Hold" means "has in the local history, hash verified".

### 8.1 Push

1. **Fetch the refs**: `GET …/refs` (with `If-None-Match` from the last fetch). Update `remotes.<name>.refs`. For every ref the push will update, the remote's value is the update's `old`. If the remote's value of a branch is a commit the client does not hold, or one that is not an ancestor of the local value (the remote moved), stop: the user pulls first (§8.4); do not upload.
2. **Compute the closure to send**: the commits reachable from the values pushed (for a tag, its commit) that are not reachable from any value in `remotes.<name>.refs`, each with its tree and the tree's parts, plus every tag pushed with its files. Remove the parts named by the trees of the boundary commits (the remote's commits that are parents of commits sent): the remote holds them. The result is a list of ids.
3. **Ask what is missing**: `POST …/objects/missing` with the list, in batches of at most `maxBatch` (10 000). The answer is the subset the server does not hold.
4. **Upload** each missing object: up to `chunk` bytes (16 MiB) in one `PUT …/objects/{id}`; above it through `…/uploads` (§10.9), resumable. Uploads MAY run in parallel (Lapin: 4 at a time) and in any order; a client SHOULD send blobs, then trees, then tags and commits.
5. **Move the refs**: one `POST …/refs/update` with every update, `{name, old, new}`, all or none. On 200, set `remotes.<name>.refs` from the response. On **409** `ref_moved`, nothing moved: fetch (§8.2) and handle the divergence (plan §4.1: pull, or keep the work as `refs/heads/wip/<me>/<name>`). On **422** `object_missing`, upload what the body lists and repeat step 5 once.

After a successful push, a client MAY attach viewer copies (§12.2) to the new heads and tags.

### 8.2 Fetch

1. `GET …/refs` with `If-None-Match`; on 304 the fetch is over.
2. For every ref value the client does not hold: a tag ref's value is downloaded first (it is small), its hash verified, and its `commit` added to the wanted commits; a branch's value is a wanted commit.
3. **Walk the commits**: for each wanted commit not held, `GET …/commits?from=<id>&stop=<ids>` where `stop` lists the heads the client holds (its local branch values and last-seen remote values, at most 100), following `cursor` while `more` is true. For each returned `{id, commit}`, verify `sha256(JCS(commit)) == id`. The walk ends at commits the client holds.
4. **Download the objects**: the trees of the new commits that are not held, then the parts those trees name and the files of new tags that are not held anywhere in the file (§6.4): `POST …/objects/pack` in batches of at most `maxBatch` ids and `maxPack` bytes (the sizes are in the trees), or `GET …/objects/{id}` one at a time. Verify every hash before storing.
5. Store the objects (they enter the file at its next save) and set `remotes.<name>.refs`, `etag`, `fetchedAt`. A fetch never changes the model or a local ref. A fetch interrupted halfway keeps what it verified and does not update the last-seen refs of the values it did not finish.

### 8.3 Clone

1. `GET …/projects/{id}`: the name, the default ref, the role.
2. Fetch (§8.2) the default ref and every tag. Other branches' values are recorded in `remotes.<name>.refs`; their objects MAY be fetched later.
3. Set the local `refs/heads/main` (the default ref) to the remote's value, `HEAD` to it, `remotes.json` to the remote, and open the tree as the project (the same reading as a package: §12.1 is a package of exactly these parts).
4. The project has no file until its first save; then the history goes into it.

### 8.4 Pull (fast-forward only, 1.0)

1. If the project has uncommitted changes (the live state's tree id differs from the `HEAD` commit's `tree`), refuse: "commit first".
2. Fetch (§8.2).
3. Let `L` be the local value of `HEAD`'s branch and `R` the remote's value of the same name.
   - `L == R`: nothing to do.
   - `L` is an ancestor of `R`: **fast-forward**. Bring the model to `R`'s tree in one undo step (`ModelCompare.Apply`, as Restore does), set the local ref to `R` (log `why: "pull"`), save.
   - `R` is an ancestor of `L`: the project is ahead; nothing to pull (push).
   - Otherwise the histories diverged: refuse, and offer (plan §4.1) **Keep my work as a branch** (push `L` to `refs/heads/wip/<me>/<name>` with `old: null`, then fast-forward the local branch to `R`) or **Discard my commits** (they stay in the file under a local `refs/heads/wip/<me>/<name>` that is not pushed; the branch is set to `R`).

Issuing (a new `refs/tags/issue/*`) is refused while the remote's main has commits the project lacks: an issued revision is on the shared history (plan §2.3).

---

## 9. HTTP: common rules

### 9.1 Discovery

The server lists the protocol in OpenCDE Foundation's `GET /foundation/versions` (anonymous), beside `foundation` and `bcf`:

```json
{ "api_id": "lapin-remote", "version_id": "1.0",
  "detailed_version": "https://github.com/Ciuntuc-BIM-Studio/lapin-pro/blob/main/docs/spec-lapin-remote.md",
  "api_base_url": "https://cde.example.com/lapin-remote/1.0" }
```

OpenCDE clients ignore entries they do not know. A lapin-remote client takes `api_base_url` (below: **`{base}`**) from this entry, never builds it, and then reads `GET {base}/capabilities` (§10.1). Every 1.x server keeps `{base}` at `…/lapin-remote/1.0` (§14).

### 9.2 Authentication

Exactly Atrium Server's OpenCDE Foundation 1.1 sign-in (`Atrium.Server/Foundation/FoundationEndpoints.cs`, `Auth/Tokens.cs`):

1. `GET /foundation/1.1/auth` gives `oauth2_token_url` and `supported_oauth2_flows`. A client uses only a flow listed there: in 1.0 `resource_owner_password_credentials_grant`; later `authorization_code_grant` with PKCE (S2; RFC 7636, the redirect to a loopback address, RFC 8252).
2. `POST <oauth2_token_url>`, `Content-Type: application/x-www-form-urlencoded`, `grant_type=password&username=<e-mail>&password=<password>&client_id=<id>` (`lapin-desktop`, `lapin-web` or `atrium`). The answer:
   ```json
   { "access_token": "…", "token_type": "bearer", "expires_in": 3600, "refresh_token": "…" }
   ```
   An error is a 400 with the OAuth2 body `{"error": "invalid_grant", "error_description": "…", "message": "…"}`.
3. Every lapin-remote request but `GET {base}/capabilities` carries `Authorization: Bearer <access_token>`.
4. A token is opaque. An access token lasts one hour; a refresh token 30 days and is **single-use**: `grant_type=refresh_token&refresh_token=<token>` returns a new pair, and the client MUST store the new refresh token at once (the old one no longer works).
5. On a 401 (with `WWW-Authenticate: Bearer`), a client refreshes once and repeats the request once; if the refresh fails (400 `invalid_grant`), the user signs in again. A client never repeats a non-idempotent request (`refs/update`) blindly after a network failure: it fetches the refs first and sees whether the update happened.

Where tokens are kept is §15.

### 9.3 Projects and access

`{projectId}` is the server's project id, **the same project as BCF's** (`/bcf/3.0/projects/{projectId}`): one id for the history and for the review topics.

- A request on a project the caller is not a member of, or that does not exist, is a **404** `not_found`, never a 403: the answer does not tell whether it exists.
- A member whose role does not allow the request gets a **403** `forbidden`.
- Rights by endpoint (viewer = reader, editor = writer, owner = admin; §5.3 for refs):

| Reader (viewer) | Writer (editor) | Admin (owner) |
|---|---|---|
| every `GET` and `HEAD`; `POST objects/missing`; `POST objects/pack` | `PUT objects`; `uploads`; `refs/update` under §5.3; `PUT commits/{id}/viewer` | everything; `force`; issue tags by default; deleting issue tags |

### 9.4 Requests and responses

- JSON bodies are UTF-8, `Content-Type: application/json; charset=utf-8` (a server accepts `application/json` without the parameter), at most 16 MiB. Member names are **camelCase**, as in Lapin's package (unlike BCF 3.0's snake_case). Times are RFC 3339 UTC with `Z`.
- Unknown members in a request body are ignored by a server; unknown members in a response are ignored by a client (§14).
- Object bytes travel as `application/octet-stream`; chunks of an upload as `application/offset+octet-stream` (tus); a pack and a package as `application/zip`.
- **Compression** is of the transport: a server that lists `"gzip"` in `encodings` accepts `Content-Encoding: gzip` on `PUT …/objects/{id}` and on JSON bodies, and honours `Accept-Encoding: gzip` on responses (`"zstd"` likewise when both sides list it). Hashes and sizes are always of the decoded bytes. `PATCH …/uploads/{id}` bodies and range responses are never content-encoded.
- Every authenticated response carries `Cache-Control: private, no-store`. Objects are immutable but private.
- **CORS** (the browser edition): a server SHOULD allow the methods used here; allow the request headers `Authorization`, `Content-Type`, `Content-Encoding`, `If-None-Match`, `Upload-Offset`, `Range`; and expose `ETag`, `Location`, `Upload-Offset`, `Upload-Length`, `Retry-After`, `WWW-Authenticate`, `Content-Range`.

### 9.5 Status codes

| Code | Meaning in this protocol |
|---|---|
| 200 | OK; for `PUT …/objects/{id}`: the object existed already |
| 201 | Created: a new object, a new upload session, the last chunk of an upload (the object now exists), a new project |
| 204 | OK, no body (a chunk that was not the last; a cancelled upload) |
| 206 | A range of an object (`Range`) |
| 304 | `If-None-Match` matched (refs, an object) |
| 400 | The request is malformed: bad JSON, a bad id or ref name, too many hashes, **a hash that does not match** (`hash_mismatch`) |
| 401 | No token, or an invalid or expired one; with `WWW-Authenticate: Bearer` |
| 403 | A member whose role does not allow it |
| 404 | No such project (or not the caller's), object, commit, upload session or viewer record |
| 409 | **A compare-and-swap failed** (`ref_moved`, with the current values); an upload chunk at the wrong offset (`offset_mismatch`) |
| 413 | Too large: an object above `maxObject`, a `PUT` above `chunk`, a pack above `maxPack`, a push above `maxPush` |
| 415 | A body of the wrong `Content-Type` |
| 422 | A well-formed request the history cannot take: **the closure is incomplete** (`object_missing`), an invalid or non-canonical object (`invalid_object`), a rule of §5.3 (`not_fast_forward`, `tag_immutable`, `protected_ref`, `wrong_kind`, `tag_mismatch`), an object version the server does not know (`unsupported_version`) |
| 429 | Rate limited; with `Retry-After` (seconds) |
| 507 | The project's quota is full (`quota`; premium hosting) |
| 500, 503 | The server's failure; 503 with `Retry-After` while it starts or collects garbage |

When several apply, a server answers the first of: 401, 404 (the project), 400, 415, 403, 413, 422, 409 (a 404 for an object, commit or upload comes where the endpoint looks it up).

### 9.6 Errors

Every error with a body has the Foundation error body, `message`, plus a stable `code` (Appendix A) and the details the code defines:

```json
{ "code": "hash_mismatch", "message": "The bytes do not hash to the id.", "expected": "0322c09d…", "actual": "fffac16a…" }
```

`message` is for people (English; it may change); clients act on `code`. Lists in error bodies (`missing`, `invalid`, `refs`) hold at most 1000 entries, with `"truncated": true` when there were more.

### 9.7 Pagination

A listing takes `limit` (default 100, at most 1000) and `cursor` (opaque, from the previous page) and answers `more` (boolean) and, when `more` is true, `cursor`. A cursor is valid for at least 10 minutes. `GET …/refs` is not paginated (a project has hundreds of refs, not millions).

### 9.8 Limits

The defaults, which a server MAY change and MUST announce (`limits` in §10.1 and §10.2):

| Limit | Default | Where |
|---|---|---|
| `maxObject` | 2 147 483 648 (2 GiB) | any object; `POST …/uploads` |
| `chunk` | 16 777 216 (16 MiB) | a single `PUT …/objects/{id}`; one `PATCH` of an upload; a tree, commit or tag |
| `maxBatch` | 10 000 | ids in `objects/missing` and `objects/pack` |
| `maxPush` | 4 294 967 296 (4 GiB) | the bytes of the objects a `refs/update` makes reachable for the first time |
| `maxPack` | 4 294 967 296 (4 GiB) | the bytes of one pack |
| `maxCommitsPage` | 1000 | `limit` of `GET …/commits` |

Also: at most 100 updates in one `refs/update`; at most 100 ids in `stop`. The chunk of 16 MiB stays under Cloudflare's 100 MB per request.

---

## 10. HTTP: the endpoints

`{base}` is `api_base_url` (§9.1), `{p}` is `{base}/projects/{projectId}`. Ids in paths are lowercase hex (§2).

| # | Method and path | Who | What |
|---|---|---|---|
| 10.1 | `GET {base}/capabilities` | anyone | what the server supports |
| 10.2 | `GET {base}/projects`, `POST {base}/projects`, `GET {p}` | member / see text | projects, a project's role, rules and limits |
| 10.3 | `GET {p}/refs` | reader | every ref, with an ETag |
| 10.4 | `POST {p}/refs/update` | writer (§5.3) | compare-and-swap, all or none |
| 10.5 | `GET {p}/refs/log` | reader | who moved what, when |
| 10.6 | `POST {p}/objects/missing` | reader | which of these ids the server lacks |
| 10.7 | `PUT {p}/objects/{id}`, `HEAD {p}/objects/{id}` | writer / reader | upload one object up to `chunk`; does it exist |
| 10.8 | `GET {p}/objects/{id}`, `POST {p}/objects/pack` | reader | download one object; many as a zip |
| 10.9 | `POST {p}/uploads`, `HEAD`/`PATCH`/`DELETE {p}/uploads/{uploadId}` | writer | resumable upload |
| 10.10 | `GET {p}/commits`, `GET {p}/commits/{id}` | reader | walk the commits; one commit |
| 10.11 | `GET {p}/commits/{id}/package` | reader | the commit as a `.lapin` |
| 10.12 | `GET`/`PUT {p}/commits/{id}/viewer` | reader / writer | the viewer copy |

Reserved for later minor versions, announced by `features` (§14): `{p}/proposals…` (S3, plan §5.4), `{p}/reservations…` (S4, plan §4.3). A 1.0 server answers 404 to them.

### 10.1 `GET {base}/capabilities`

Anonymous. `200`:

```json
{
  "protocol": "lapin-remote",
  "version": "1.0",
  "majors": [1],
  "objects": { "commit": [1], "tag": [1], "tree": [3] },
  "limits": { "maxObject": 2147483648, "chunk": 16777216, "maxBatch": 10000, "maxPush": 4294967296, "maxPack": 4294967296, "maxCommitsPage": 1000 },
  "encodings": ["gzip"],
  "features": ["uploads", "pack", "package", "viewer", "range", "projects.create"],
  "auth": { "foundation": "/foundation/1.1" }
}
```

- `version`: the exact version the server implements, `1.<minor>`.
- `majors`: the major versions this server serves (each at its own `…/lapin-remote/<major>.0`).
- `objects`: the `v` values of commits and tags, and the `version` values of trees, the server accepts (§14).
- `features`: what is optional. 1.0 defines `uploads` (§10.9), `pack` (§10.8), `package` (§10.11), `viewer` (§10.12), `range` (`Range` on `GET …/objects/{id}`), `projects.create` (`POST {base}/projects`). A 1.0 server MUST list `uploads` and `pack`; a client MUST NOT use a feature that is not listed.
- `auth.foundation`: the Foundation base, relative to the server's root.

### 10.2 Projects

**`GET {base}/projects`**: the projects the caller is a member of (every project for a server administrator), by name. `200`: `{"projects": [{"id": "…", "name": "Bloc P+4", "role": "owner"}]}`. Paginated (§9.7).

**`POST {base}/projects`** (feature `projects.create`; by default server administrators only, else 403): `{"name": "Bloc P+4"}` → `201` with the project as below; the caller is its owner. It is also a BCF project.

**`GET {p}`**: `200`:

```json
{ "id": "0b6f1c2e-5a7d-4c1e-9f3a-2d8e6b4a1c90", "name": "Bloc P+4", "defaultRef": "refs/heads/main",
  "role": "owner", "rules": { "main": "editor", "issueTags": "owner" },
  "limits": { "maxObject": 2147483648, "chunk": 16777216, "maxBatch": 10000, "maxPush": 4294967296, "maxPack": 4294967296, "maxCommitsPage": 1000 } }
```

`role` is the caller's: `viewer`, `editor` or `owner`. `limits` are this project's (premium hosting may set them per project). Codes: 200, 401, 404. Changing the name, the members and the rules is done through Atrium's own administration (BCF `PUT /projects/{id}`, the command line), not this protocol.

### 10.3 `GET {p}/refs`

Optional query: `prefix` (`refs/heads/`, `refs/tags/issue/`). `200`, with a strong `ETag` that changes whenever any ref of the project changes:

```json
{ "refs": [
  { "name": "refs/heads/main", "target": "ce2c4e18…", "updatedAt": "2026-11-23T10:51:10Z", "updatedBy": { "id": "atrium:5d2c…", "name": "Mihai Ionescu" } },
  { "name": "refs/tags/issue/1-P01", "target": "336f44b8…", "updatedAt": "2026-11-23T10:51:10Z", "updatedBy": { "id": "atrium:5d2c…", "name": "Mihai Ionescu" } }
] }
```

Sorted by `name` (ordinal); a deleted ref is not listed. `target` is the id the ref holds (a commit or a tag: the plan's `commit` member, renamed because a tag ref does not hold a commit). With `If-None-Match` equal to the current ETag: `304`, no body. Codes: 200, 304, 401, 404.

### 10.4 `POST {p}/refs/update`

```json
{ "updates": [
  { "name": "refs/heads/main", "old": "fedb729f…", "new": "ce2c4e18…" },
  { "name": "refs/tags/issue/1-P01", "old": null, "new": "336f44b8…" }
] }
```

- `old`: the value the client expects (`null`: the ref must not exist). `new`: the value to set (`null`: delete the ref). `force` (boolean, optional, owner only): allows a non-fast-forward move of `refs/heads/main`.
- At most 100 updates, each name once (else 400). **All or none**: either every update is applied in one transaction, with its ref log entries, or none is.
- The server verifies, in this order (§11): the names and ids (400); the caller's rights for each update (403); each `new` exists, has the right kind, is valid and canonical, and its closure is complete (422); the rules of §5.3 (422); and last, atomically with the write, that every ref's current value is `old` (409).
- `200`: the refs after the update, as `GET {p}/refs` returns them, with the new `ETag`.
- `409` `ref_moved`: `{"code": "ref_moved", "message": "…", "refs": [{"name", "expected", "current", "updatedAt", "updatedBy"}]}`, one entry for every update whose `old` did not match (`current: null` when the ref does not exist). Nothing changed.
- `422` `object_missing`: `{"code": "object_missing", "message": "…", "missing": [{"id", "kind", "from", "path"?, "size"?}]}`: each object that is missing, its expected kind, the object that names it (`from`), and for a part its `path` and declared `size`. `422` `invalid_object`: `{"invalid": [{"id", "kind", "reason"}]}`. Both lists may appear in one body; `code` is the first that applies (`invalid_object` before `object_missing`). The other 422 codes carry `name` (the ref).
- Codes: 200, 400, 401, 403, 404, 413 (`maxPush`), 422, 409, 507.

In Atrium Server the compare-and-swap is `UPDATE refs SET target = $new … WHERE project = $p AND name = $n AND target IS $old` (an `INSERT` for a create, guarded by the primary key), all in one SQLite transaction with the log, any row count other than 1 rolling everything back with a 409 (plan §12).

### 10.5 `GET {p}/refs/log`

Query: `name` (optional: one ref; else all), `limit`, `cursor`. `200`, newest first:

```json
{ "entries": [
  { "name": "refs/heads/main", "old": "fedb729f…", "new": "ce2c4e18…", "by": { "id": "atrium:5d2c…", "name": "Mihai Ionescu" }, "at": "2026-11-23T10:51:10Z", "force": false }
], "more": false }
```

`by` is the account that made the request (not the commits' authors). The log is never deleted (plan §7: the ISO 19650 audit trail), even after a ref is deleted or its objects are collected.

### 10.6 `POST {p}/objects/missing`

`{"hashes": ["…", "…"]}`, at most `maxBatch` ids (else 400 `batch_too_large`), duplicates allowed. `200`: `{"missing": [...]}`, the ids the project does not hold, in the order asked, each once. An object counts as held once it is fully written and verified (an upload session in progress does not count). Objects are per project: an object held by another project of the same server is missing here (plan §14, answer 7). Codes: 200, 400, 401, 404.

### 10.7 `PUT {p}/objects/{id}` and `HEAD {p}/objects/{id}`

**`PUT`**: the body is the object's bytes (`application/octet-stream`; `Content-Encoding: gzip` allowed when announced), at most `chunk` decoded bytes.

- The server writes the bytes to a temporary file while hashing them (never the whole body in memory), compares the hash with `{id}`, and only then renames the file into place.
- `201`: stored. `200`: the project held it already (the server MAY answer before reading the body). `400` `hash_mismatch` `{expected, actual}`: nothing is stored. `413` `too_large` `{limit}`: above `chunk` (use §10.9) or `maxObject`. `507` `quota`. Both 200 and 201 carry `ETag: "<id>"`.
- The server does not parse the object here: a tree, commit or tag is validated when a ref update first makes it reachable (§11).

**`HEAD`**: `200` with `Content-Length` (the size) when held, `404` when not.

### 10.8 `GET {p}/objects/{id}` and `POST {p}/objects/pack`

**`GET {p}/objects/{id}`**: `200`, the bytes, `application/octet-stream`, `Content-Length`, `ETag: "<id>"`. `If-None-Match: "<id>"` → `304`. With feature `range`, `Range: bytes=…` → `206` with `Content-Range` (a large download resumes). `404` `object_missing`.

**`POST {p}/objects/pack`**: `{"hashes": [...]}`, at most `maxBatch` ids, each object counted once. The server checks first that it holds every one (else `404` `object_missing` with `missing`) and that their total size is at most `maxPack` (else `413` `too_large`), then streams `200`, `application/zip`:

- one entry per distinct id, named by the id (no folder), in any order;
- each entry stored or deflated; Zip64 when the pack needs it;
- nothing else in the zip.

The client verifies every entry's hash; a mismatch (or a broken stream) is retried with `GET …/objects/{id}` for the entries it lacks.

### 10.9 Resumable uploads

For an object above `chunk`, and for any object a client chooses to send this way (after tus 1.0, simplified: the `Upload-Offset` header and the `application/offset+octet-stream` body, without tus's other headers):

**`POST {p}/uploads`** `{"sha256": "<id>", "size": <bytes>}`:

- `201`, `Location: {p}/uploads/{uploadId}`: `{"id": "<uploadId>", "offset": 0, "chunk": 16777216, "expiresAt": "…"}`.
- `200` `{"exists": true, "sha256", "size"}`: the project holds the object; nothing to send.
- `200` `{"id", "offset", "chunk", "expiresAt", "resumed": true}`: the same caller has an unexpired session for the same id; it resumes.
- `413` `too_large` (above `maxObject`), `507` `quota`.

**`HEAD {p}/uploads/{uploadId}`**: `200` with `Upload-Offset` (the bytes the server holds) and `Upload-Length` (the size); `404` `upload_missing` (unknown, expired, another user's, or finished).

**`PATCH {p}/uploads/{uploadId}`**, `Content-Type: application/offset+octet-stream`, `Upload-Offset: <offset>`, a body of 1 to `chunk` bytes, not content-encoded:

- the offset MUST equal the server's (else `409` `offset_mismatch` `{offset}`, with the server's `Upload-Offset`; nothing appended);
- `offset + body length` MUST NOT exceed `size` (else 400);
- a chunk that does not complete the object: `204` with the new `Upload-Offset`;
- the chunk that completes it: the server verifies the hash of all `size` bytes; `201` `{"sha256", "size"}` and the object exists; or `400` `hash_mismatch`, and the session is discarded.
- If a `PATCH` breaks off, the server MAY keep any prefix of its bytes it received; `HEAD` tells where to resume.

**`DELETE {p}/uploads/{uploadId}`**: `204`; the session and its bytes are discarded.

A session expires 24 hours after its last `PATCH` (`expiresAt`); an expired session's bytes are deleted.

### 10.10 Commits

**`GET {p}/commits?from=<id>&stop=<id>,<id>&limit=<n>&cursor=<c>`**: the commits reachable from `from` (included) and not reachable from any `stop`, **each before all of its parents**; beyond that the order is the server's. Ids in `stop` the server does not know are ignored. `200`:

```json
{ "commits": [ { "id": "ce2c4e18…", "commit": { "app": "0.1.1612", "at": "2026-11-23T10:42:00Z", "…": "…" } } ], "more": false }
```

`commit` is the commit object as JSON; because objects are canonical, `sha256(JCS(commit)) == id`, which the client MUST check. `404` when `from` is not a commit of the project; `400` for more than 100 stops or a `limit` above `maxCommitsPage`.

**`GET {p}/commits/{id}`**: `200`, the commit's canonical bytes, `application/json`, `ETag: "<id>"`; `404` if the project has no such commit. A server knows an object is a commit once a ref update has validated it (§11); an uploaded object not yet reachable from a ref MAY answer 404 here (it is still served by `GET …/objects/{id}`).

### 10.11 `GET {p}/commits/{id}/package`

Feature `package`. The commit as a `.lapin` package (§12.1). Query `viewer=false` leaves the viewer copy out. `200`, `application/zip`, `Content-Disposition: attachment; filename="<project name> <first 8 of id>.lapin"`, `ETag: "<id>"` (`"<id>-v"` with the viewer copy). `404` for an unknown commit. Streamed; Zip64 when needed.

### 10.12 The viewer copy

Feature `viewer`.

**`PUT {p}/commits/{id}/viewer`** (writer), `{"ifc": "<id of the IFC object>", "schema": "IFC4"}`: attaches a viewer copy to a commit, replacing any earlier one. The IFC object is uploaded first like any object. `201` (new) or `200` (replaced), with the record; `404` for an unknown commit; `422` `object_missing` if the IFC object is not held; `schema` is `IFC2X3`, `IFC4`, `IFC4X3` or `IFC4X3_ADD2`.

**`GET {p}/commits/{id}/viewer`**: `200` `{"commit", "ifc", "schema", "size", "createdAt", "createdBy": {"id", "name"}}`; `404` `viewer_missing`. The IFC itself is `GET {p}/objects/<ifc>`.

---

## 11. What a server verifies

A server never trusts what a client says about an object. It MUST:

1. **Rehash every object it receives**, while it streams it to disk (`PUT`, the last chunk of an upload), and refuse (400 `hash_mismatch`) and discard an object whose bytes do not hash to its id. It never stores an object under an id it did not compute.
2. **Validate on reachability.** When a `refs/update` makes an object reachable from a ref for the first time, the server parses it as the kind its referrer expects (a ref under `refs/heads/` or a commit's `parents`: a commit; a commit's `tree`: a tree; a ref under `refs/tags/`: a tag) and checks: valid JSON, no duplicate member, **canonical** (re-canonicalised bytes equal), the `format` of its kind, a `v` (or, for a tree, a `version`) the server accepts (else 422 `unsupported_version`), the required members with their types and grammars (§4), the path rules of a tree (§4.2), and no `foreign` in a tree. A failure is 422 `invalid_object`.
3. **Check the closure is complete**: for every commit newly reachable, its tree and every parent exist; for every tree, every part exists **with its declared size**; for every tag, its commit and every file exist with their sizes. Else 422 `object_missing` with the list. (A server MAY remember which commits it has verified complete; a commit reachable from a ref is complete, and a walk stops there.)
4. **Check the rules of §5.3**: the kind and number behind each ref name, fast-forward on main (walking `new`'s ancestors until `old`, bounded by the commits the project holds; a server SHOULD keep each commit's generation number to bound it), issue tags never moving, `protected_ref`, roles, `rules`.
5. **Compare and swap**: every ref's current value is the update's `old`, checked and written atomically with the ref log; else 409 `ref_moved` with the current values.
6. **Check rights per project** on every request (§9.3), and answer 404 to non-members.

The server never executes anything it stores (scripts are text), never opens an IFC in 1.0, and never returns one project's object from another project's path.

---

## 12. The assembled package and the viewer copy

### 12.1 The package of a commit

`GET {p}/commits/{id}/package` returns a zip that any Lapin reading format 3 opens as a project:

| Entry | Bytes |
|---|---|
| `manifest.json` | the commit's **tree object, byte for byte** (canonical JSON is valid JSON; Lapin's reader does not need it indented) |
| every part of the tree, at its `path` | the blob's bytes |
| `derived/ifc/<commit id>.ifc` | the viewer copy's IFC, when the commit has one and `viewer` is not `false` |

Nothing else: no `versions/`, `issues/`, `history/` or other tools' entries (the history comes with a clone, §8.3). Entries are stored or deflated, in the tree's order, the IFC last. `derived/` is Lapin's cache folder: Lapin reads no model from it and drops it at the next save, while Atrium opens the IFC it finds there (the reader takes the single entry under `derived/ifc/`). A client verifies every part against the tree as it does for any package.

### 12.2 The viewer copy

A viewer copy is an IFC export of a commit (plan §5.1: the same GlobalIds, the same `VisibilityGraphics` resolution as Lapin's export), for Atrium, which has no Lapin engine.

- It is **not part of the commit or the tree**: an export is not byte-for-byte reproducible (it carries a time), and the copy is regenerated after garbage collection, so it cannot enter any hash. It is a **viewer record** beside the commit: `{commit, ifc, schema, size, createdAt, createdBy}`; on a folder, `viewers/<commit id>.json` (rewritten, by temporary file and rename).
- Lapin attaches one at push to the new head of every branch it pushes and to every tag it pushes (plan §14, answer 5). It is optional everywhere: its absence breaks nothing.
- A local `.lapin` does not keep viewer copies.

---

## 13. Garbage collection

An object is **live** if it is in the closure (§4.5) of any of:

- a ref's current value (every ref: heads, tags, and later proposals);
- the `old` or `new` of a ref log entry newer than **30 days**;
- on a folder, any detached ref entry (§7.3), whatever its age;
- a kept viewer record (below): its IFC object.

Also never deleted: an object written less than **24 hours** ago (a push in progress uploads before it moves refs), and the bytes of an upload session that has not expired.

**Issue tags are never collected.** An issue tag's ref cannot move (§5.3), so its closure stays live as long as the ref exists; the ref is deleted only by an owner, explicitly, and that deletion is logged forever. Only after such a deletion, and 30 days, may the tag's objects go.

**Viewer records** are regenerable. A collector keeps those of: every branch head, every commit a tag names, and the last 10 commits of `refs/heads/main` along first parents. Others MAY be deleted, and with them their IFC objects when nothing else keeps those live.

A collector deletes only objects that are not live. It never deletes a ref, a ref log entry, or a ref entry file of a folder.

- **On a server**: daily, while serving; deleting an object and a concurrent upload of the same id MUST NOT lose the upload (the 24-hour rule covers objects; a server re-checks before deleting).
- **On a folder**: only on request (Lapin: Manage → Remote → Clean up), under the lock (§7.2), by an owner of the folder (anyone who can write it).
- **Locally** (the `.lapin`): at save, objects no longer reachable from a local ref or a kept local log entry are not written again; nothing reachable is ever dropped.

---

## 14. Versions and capabilities

- The protocol is **lapin-remote 1.x**. A server says exactly which minor it implements in `GET {base}/capabilities` (`version`), and lists the `features` it has.
- **A 1.x minor version only adds**: optional members in responses and request bodies, endpoints and query parameters behind a new `feature`, error codes, ref namespaces (announced), object members (kept by every reader, §3). Every 1.x server stays at `{base}` = `…/lapin-remote/1.0`, and the Foundation entry's `version_id` stays `"1.0"`.
- Clients MUST ignore unknown response members and MUST NOT use a feature, an object version or a ref namespace the server does not announce. Servers MUST ignore unknown request members.
- **Object versions**: a commit's and a tag's `v` stays `1` for every additive change (new members are kept and hashed). A `v` of 2 means a reader that knows only 1 cannot interpret the object: a server stores and validates only the versions it lists in `objects` (else 422 `unsupported_version`); a client that meets a `v` it does not know keeps the object, shows the commit, and refuses to check it out ("made by a newer Lapin"). A tree's `version` is the package format (`3`), under the same rule.
- A change that would break a 1.x client is **lapin-remote 2.0**, served at `…/lapin-remote/2.0` beside 1.0 for as long as clients need it (`majors`).
- The folder remote carries its version in `lapin-remote.json` (`v: 1`) and `project.json`; the local history in each of its JSON files (`v: 1`). A reader refuses a `v` it does not know rather than guessing.

---

## 15. Security

- **Tokens never enter the project.** `history/remotes.json` holds a remote's URL and project id, nothing else (§6.5). No token, password, cookie or e-mail address is written into the `.lapin`, a commit, a tag, a ref entry, a viewer record or a folder remote. The refresh token is kept in the system's keychain (Keychain, Credential Manager, Secret Service, through the Tauri shell); in the browser, only in the tab's memory. The access token is held in memory only.
- Commits and tags carry an author's id and name, never an e-mail address: the file travels.
- Clients MUST NOT log `Authorization` headers or token responses; servers store only the SHA-256 of tokens (as `Tokens.cs` does).
- Every object is rehashed by whoever receives it, server and client (§11; §8.2). A client verifies a downloaded package against its manifest as it verifies any package.
- Paths in trees pass the rules of §4.2 before any writer turns them into file names; a folder remote's file names are built from ids and validated ref names only.
- A non-member gets 404, not 403 (§9.3). Objects are per project: `objects/missing` never tells whether another project holds a hash.
- A server rate-limits per user and per address (429 with `Retry-After`) and limits sizes (§9.8).
- TLS: every server URL other than a loopback address MUST be `https`. (On the test host, Cloudflare's tunnel ends TLS at Cloudflare's edge, plan §7; a self-hosted server ends it itself.)
- Nothing a remote stores is executed by the remote (scripts are text; IFC is not opened by the server in 1.0).

---

## 16. Conformance

An implementation conforms to lapin-remote 1.0 when it passes the vectors of `docs/spec-lapin-remote/vectors/`:

- **every implementation**: the ids of `blobs/` and `objects/` (`ids.json`); the JCS cases of `canonical/`; parsing every object of `objects/` and re-canonicalising it to the same bytes, `commit-future.json` included (its unknown members kept);
- **a server**: replaying `session/push`, `session/fetch`, `session/push-conflict` and `session/errors`, in that order, against a project seeded as the vectors' README says, with the same status codes, the same `code`s and the same non-opaque values;
- **a client**: sending the requests of the sessions to a fake server that gives their responses, and taking the same decisions (upload only what `missing` lists; stop at 409; resume an upload from `HEAD`'s offset);
- **a folder remote**: resolving `folder/fork-detection.json` to its expected chain, head and detached entries, and laying a project out as `folder/layout.json` lists.

Both repositories run the vectors in their tests: Lapin's in `Lapin.Engine.Tests`, Atrium Server's with `WebApplicationFactory`. The vectors are made by `make-vectors.mjs` beside them; a change to the protocol changes the script, the vectors and this text in one commit.

### Vector objects

| Name | Kind | Id | Size |
|---|---|---|---|
| `empty.bin` | blob | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | 0 |
| `hello.txt` | blob | `fffac16a720cca4dfaf893c3d25fbf5f1a8e07e5aa9c0ec3d4ec2da2a8bb7658` | 14 |
| `unicode.txt` | blob | `a2d740571f4fa8c34d22142f544115b12ee79470a805be2a2e58cedc1f1e72f1` | 39 |
| `document.json` | blob | `5e0b1db26ac04e4ff0dd62a50b538a74d1df1a11fc84e714dcc52d9d7c62ad66` | 40 |
| `walls.arrow` | blob | `40aff2e9d2d8922e47afd4648e6967497158785fbd1da870e7110266bf944880` | 256 |
| `relations.arrow` | blob | `06102ac035947e401232f8dec2ec7f18b3a14bf1add20ae3e247947f81d74d41` | 64 |
| `hello.py` | blob | `0322c09de894ffaf5e59b5b819ec263948006bc37999e4b3defb7d7cd1ab11d3` | 23 |
| `issue.pdf` | blob | `73fa0eeab0127616e19bacf37ac6cf241f8156c89aa6f851ad030b306c05728c` | 42 |
| `missing.py` | blob | `535ec7ad1a1a2734381137db05491f341c5431d1ebb600b11b2ab19c24c7bb98` | 24 |
| `T1` | tree | `7e4c2e2a044109912f4dd8ad2e65e9c4e6946d015a4c38419ef7b39ac32ef0df` | 378 |
| `T2` | tree | `941f7c834ec70c7171289afba2fc9d5042cab97306759b2c9979f34be04a7c26` | 707 |
| `T3` | tree | `840b22f3b53397e114f3dfc3d11817158ab758eebb378e141f01e3ca3235288f` | 550 |
| `T4` | tree | `a3a1b99c43064c9aef682e0a1fc3ee704a23191aba7f3b4819165afe8011bd6d` | 844 |
| `c1` | commit | `fedb729f0255c515f1716fa24c7a47eeeeb4df20007dd8bc967056da0c35aeb1` | 305 |
| `c2` | commit | `ce2c4e189b12a9e5bf3498d026dd8a8c465b546bf2ed29ffe644f6c5813a3c94` | 389 |
| `c3` | commit | `adc421509ab97b9cb16f960a1119756f71af1fae30b1f7d58ca8754df2955cc3` | 371 |
| `c4` | commit | `526d5f566c1249a0ca287dee0d830734e8b2fd1e19ade5fc5e932700b35ea697` | 371 |
| `commit-future` | commit | `4db4ba1dee86d896ee4956a7958127b3f3d2129876c8138d58ef887eb33db7c7` | 442 |
| `tag-issue` | tag | `336f44b81f7218e8d60240bb8beea12a4c37612e49c892131fddf1cc193fac67` | 496 |
| `tag-version` | tag | `a228d75c7f0b06705ebe34d91be043766ccd7d86fa9d9c2a2173ebd6a187e38d` | 291 |

---

## Appendix A: error codes

| `code` | Status | Details | When |
|---|---|---|---|
| `unauthorized` | 401 | | no token, or an invalid or expired one |
| `not_found` | 404 | | no such project, or not a member |
| `object_missing` | 404, 422 | `missing` | an object asked for is not held (404); the closure of an update or a viewer record is incomplete (422) |
| `upload_missing` | 404 | | unknown, expired, finished or another user's upload |
| `viewer_missing` | 404 | | the commit has no viewer copy |
| `bad_request` | 400 | | malformed JSON, a missing member, a bad query |
| `bad_id` | 400 | | an id not 64 lowercase hex characters |
| `bad_ref_name` | 400 | `name` | a ref name outside §5.1 |
| `batch_too_large` | 400 | `limit` | more than `maxBatch` ids, 100 updates or 100 stops |
| `hash_mismatch` | 400 | `expected`, `actual` | the bytes do not hash to the id |
| `forbidden` | 403 | | the caller's role does not allow it |
| `offset_mismatch` | 409 | `offset` | an upload chunk at another offset than the server's |
| `ref_moved` | 409 | `refs` | a compare-and-swap failed |
| `too_large` | 413 | `limit` | above `chunk`, `maxObject`, `maxPack` or `maxPush` |
| `unsupported_media_type` | 415 | | a body of the wrong type |
| `invalid_object` | 422 | `invalid` | not valid, not canonical, wrong `format`, a bad path, `foreign` in a tree |
| `unsupported_version` | 422 | `id`, `v` | an object version the server does not accept |
| `wrong_kind` | 422 | `name` | a ref pointing to the wrong kind of object |
| `tag_mismatch` | 422 | `name` | a tag's `number` or `revision` disagree with its ref name |
| `not_fast_forward` | 422 | `name` | main moved to a commit that does not descend from `old`, without an owner's `force` |
| `tag_immutable` | 422 | `name` | an issue tag moved |
| `protected_ref` | 422 | `name` | main deleted |
| `rate_limited` | 429 | | with `Retry-After` |
| `quota` | 507 | `limit`, `used` | the project's quota |

## Appendix B: details this specification decided beyond the plan

The plan (`docs/plan-atrium-cde.md`, decision 793) left these open or implicit; each is decided here, with its reason.

- **B1. The viewer copy is not a commit member.** The plan's `viewer` member would put a non-reproducible export into the commit's hash: attaching it at push, or regenerating it after GC, would change the commit's id. It is a replaceable record beside the commit (§12.2, §10.12).
- **B2. Trees are always stored in `history/objects/`,** because `manifest.json` is indented and may carry `foreign`, so it is never a tree's bytes; trees are small.
- **B3. `history/index.json`** lists the objects held and the absent ones, as a check, not the truth (§6.3); the location rule adds `issues/blobs/` to the plan's three places (an issued file is already there).
- **B4. Ref values are `target`, not `commit`,** in `GET …/refs`: a tag ref holds a tag. `refs/tags/*` hold tag objects (not commits), so a version's name and note travel with it.
- **B5. Tags carry `number`** (the issue's or the version's), so the ref name can be checked against the tag; version tags carry `name`.
- **B6. Rights per ref** (§5.3): main fast-forward only on the server (no client can reset main backwards by mistake), with an owner's `force`; issue tags never move and are created by owners by default (plan §7), with `rules.issueTags` to let editors issue; wip branches belong to their creator.
- **B7. The order of checks** (401, 404, 400, 415, 403, 413, 422, 409): validation before compare-and-swap, so a 409 always means "the ref moved" and a retry after pull can succeed.
- **B8. Resumable uploads use tus's `Upload-Offset`,** not `Content-Range` (plan §3.3 and §12); a `PATCH` may send any size up to `chunk`; `POST …/uploads` resumes a caller's open session; `DELETE` cancels.
- **B9. Commits in `GET …/commits` are JSON values,** verified by re-canonicalising, which works because every object is canonical; a server refuses non-canonical objects.
- **B10. A folder ref entry's name** is fixed-width (`<8-digit seq>-<64 chars>-<author key>.ref`), deletion as 64 zeros; forks are resolved by earliest `at`, then file name; the detached entry's own author makes the wip branch, so it is made once.
- **B11. `GET {base}/capabilities`** (anonymous) and `GET`/`POST {base}/projects` are added: the client must know the limits before signing in, and must list (and, where allowed, create) projects (plan §6, "alegerea sau crearea proiectului").
- **B12. JSON on the wire is camelCase,** like Lapin's package and the plan's examples, not BCF's snake_case; errors keep Foundation's `message` and add `code`.
- **B13. 1.x stays at `/lapin-remote/1.0/`;** the exact minor is in the capabilities.
- **B14. Tree paths**: the server checks the generic rules and refuses the names that are never project parts (`manifest.json`, `versions`, `issues`, `history`, `derived`, `journal`), instead of a whitelist, so a later Lapin can add a top-level folder without a server update.
- **B15. Clients name themselves** `lapin-desktop`, `lapin-web` and `atrium` as OAuth2 `client_id`.
