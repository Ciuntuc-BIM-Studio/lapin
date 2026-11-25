# Reference documents, folders, the generator and MCP in the CDE: plan

Asked by the user on 25.11.2026 (in Romanian): "can we import IFC files, do we have an ISO folder structure for projects, can we include documents there, for example urbanism certificates, permits and so on; and we would need a documentation generator starting from some data, using MCP servers too so AI models can connect to the application", then "I mean the Atrium CDE". Answered the same day: **metadata and folders, both places (the `.lapin` and the server), the open part.** Decision 874.

Everything here is open: MIT in lapin-pro, MIT-0 in Atrium. Premium may later add per-folder permissions, company folder templates on a server and SharePoint (`docs/plan-cde.md` stage 4), through hooks, never in these repos.

## 1. What exists, and what is missing

| Asked | Today | Missing |
|---|---|---|
| IFC into the CDE | Lapin (desktop and web) imports an IFC and saves it to the server as a native project. Every commit has a viewer IFC. The server runs IDS over the `.ifc` files of a submission (W6b). | Putting another consultant's IFC (structure, MEP) in the CDE as a reference model. The API takes any file in a submission, but the interface sends only what Manage → Issue… makes. |
| ISO 19650 folders | The containers WIP / Shared / Published / Archived: folders on disk (`plan-cde.md`), the Containers tab on the server (W4). ISO 19650-2 naming, a naming regex per project, S/A/B/CR status codes, P/C revisions, the document register (XLSX/CSV), the queue, the gate, the V1–V5 checks (W5). | A folder tree inside the project. Everything is organised by issue and sheet today. |
| Incoming documents (urbanism certificate, permits, land-book extract, studies) | Nothing. | A register of the documents the project **receives**, with their metadata, validity and folder; the certificate's list of required permits. |
| A document generator | `LapinDocument` (`docs/plan-documents.md`): templates (architecture report, specification, list of deliverables), filled from the project, edited visually, exported as DOCX / ODT / PDF, issued like the sheets, on desktop and web. | Fields from the CDE's data (the certificate's number and date, the permits); templates for a permit application (an index of the application package, a cover page, a list of permits); issuing a package that includes received documents. |
| MCP | `scripts/lapin-mcp.mjs` drives the open Lapin (`docs/mcp.md`), documents included. | An MCP on Atrium Server: projects, containers, the register, submissions, issues, reference documents, from an AI client, with the user's rights. |

## 2. Decisions

- **Reference documents are a register beside the issues, not a fifth container.** ISO 19650 calls what a team receives *reference information*; it is not produced through WIP → Shared → Published by this team, so it does not move between containers. It has its own state (below). An IFC received from a consultant is a reference document of kind *model*.
- **Metadata and folders together.** Each document has its metadata (the truth: type, issuer, number, dates, phase, discipline, status), and one **folder**, a path in the project's folder tree. Lists filter by metadata and browse by folder. A folder is a metadata field. Moving a document changes that field and nothing else: the file and its id stay.
- **The folder tree is a template.** The built-in default is in Romanian and in English. A company's template comes from this computer's library, like document templates (`plan-documents.md` stage 4). A project can then change its own copy. Each document type has a default folder (an urbanism certificate goes into *01 Input data / Urbanism certificate*), and the user can override it.
- **Issued deliverables are shown in folders too, derived, not stored.** Their names carry ISO 19650's type and role (`DR`, `M3`, `SP`; `A`, `S`, `E`), and the Containers tab can group by them through a mapping in the template (role A → *Architecture*). Tags stay as they are: immutable, with no new member.
- **Both places, as remarks do.** A reference document is a `LapinReference` entity in the project (its metadata) with its file in the package, outside the project state (`references/blobs/<sha256>`, like `issues/blobs/`). With a server remote, the server is the truth. `remote.syncReferences` merges metadata by GlobalId, field by field against the base last seen (last writer wins per field), exactly as `IssueSync` does for BCF topics. Files go through lapin-remote's object store (content-addressed, so a file sent twice is stored once). A browser user can add a document on the server without opening the project. A desktop user without a server keeps everything in the `.lapin`, and in the project folder when one is set.
- **Nothing is deleted outright.** A document is *withdrawn* (with a reason) or *superseded* (by its newer one, linked). Hard deletion is kept for owners, for a file uploaded by mistake, and is written in the journal. This is the audit trail ISO 19650 and a permit's history need.
- **Norms are the user's.** The document types, the certificate's checklist of permits and the default validity periods ship as **indicative** defaults that the user edits, per project and in the company's library. Lapin never claims a legal period. The dates written on the document are what count.
- **Text is read, never guessed.** The server already reads a PDF's text (`Atrium.Checks`). Suggestions for the number and date come from that text, by pattern, and the user confirms them. Scans have no text layer and get no suggestion (there is no OCR, as in W5). An AI client may read the text through MCP and propose metadata. What it writes goes through the same API, with the same rights, journaled as "via MCP".
- **Personal data.** Land-book extracts and certificates name people. Documents are visible to the project's members only (as everything on the server is today). The MCP setup page says that whatever an AI client reads is sent to that client's provider.

## 3. The model

### 3.1 The reference document (`LapinReference`)

An entity of the project, in `Lapin.Platform/Cde/References/`, like `LapinRemark`, with a GlobalId as its id:

| Field | What |
|---|---|
| `title` | Typed, or the file's name without its extension. |
| `type` | Id from the project's type list: `cu`, `aviz`, `acord`, `autorizatie`, `cf` (land-book extract), `cadastru`, `studiu-geo`, `topo`, `expertiza`, `tema` (brief), `contract`, `corespondenta`, `model` (IFC / other models), `altul` (other). |
| `issuer` | The authority or company (a list per project, remembered: ISU, DSP, APM, the network operators, the town hall…). |
| `number`, `date` | As written on the document. |
| `validFrom`, `validUntil` | Optional; `validUntil` drives the expiry warnings. |
| `phase` | One or more of the project's phases (SF, DTAC, PT, DE…). |
| `discipline` | ISO 19650 role (A, S, E, M, P…), optional. |
| `status` | `requested` (asked for, no file yet; `requestedAt`, `requestedBy`), `received`, `expired` (derived from `validUntil`, never stored), `superseded` (`supersededBy`), `withdrawn` (`reason`). |
| `folder` | The path in the folder tree. |
| `files` | `[{name, sha256, size, mime}]`: usually one file, sometimes several (the permit and its annexes). |
| `requires` | For a certificate: the checklist of required permits, each `{key, label, document?}`. `document` is the reference document that answers it (§3.3). |
| `relatesTo` | Optional links: other documents, the project's elements (a permit about a connection, by GlobalId), a remark. |
| `fields` | The project's own extra fields (`{key: value}`), defined in the type list. A company adds what it tracks, under the user-extensibility rule. |
| `addedAt`, `addedBy`, `modifiedAt`, `modifiedBy` | Who and when (the remote author id when synced). |

### 3.2 Folders

`references.folders` on the IfcProject: an ordered tree `[{id, name: {ro, en}, children}]`, plus `typeFolders: {type: folderId}` and `roleFolders: {role: folderId}` for the issued deliverables. A folder's id stays when it is renamed. A document points at the id; the path is for display and for the disk.

The built-in default (Romanian names shown, English beside them in the template):

```text
00 Administrativ            (contracts, correspondence, the brief)
01 Date de intrare
   Certificat de urbanism
   Extras CF și cadastru
   Ridicare topografică
   Studiu geotehnic
   Expertize și audituri
02 Avize și acorduri
   Utilități                (water, sewerage, power, gas, telecom)
   Securitate la incendiu
   Sănătate publică
   Mediu
   Altele
03 Autorizații
04 Modele de referință      (consultants' IFC)
05 Piese scrise emise       (derived: documents issued by Lapin)
06 Piese desenate emise     (derived: sheets issued by Lapin)
```

Folders 05–06 hold no reference documents. They are the derived view of the issue tags.

### 3.3 The urbanism certificate's checklist

A certificate lists the permits it requires. A reference document of type `cu` has `requires`. The default list is indicative and editable:

- utilities: water supply, sewerage, electricity, gas, telecommunications, waste, urban transport;
- fire safety, civil protection, public health;
- the environmental agency's decision;
- the studies it asks for (geotechnical, topographic, others).

The user ticks the permits the certificate asks for and adds the others. Each item is then *missing*, *requested* (a `requested` document stands for it), or *received*, and the document's link sets this. The list is what the generator's table and the CDE's "what is still missing" read.

### 3.4 Commands and queries (engine)

Registered by the core module, each one an undo step, listed in the command catalogue (`node scripts/command-catalogue.mjs --check`):

- `references.add {files: [{name, bytes(base64) | path}], title?, type?, issuer?, number?, date?, validUntil?, phase?, folder?, …}` → `{id}`. A file already held (same SHA-256) is stored once.
- `references.update {id, …fields}`; `references.supersede {id, by}`; `references.withdraw {id, reason}`; `references.delete {id}` (owners on a server; locally, the project's author).
- `references.setFolders {folders, typeFolders?, roleFolders?}`; `references.setTypes {types}` (the type list with its extra fields and default validity).
- `references.list {folder?, type?, phase?, status?, expiringWithin?}`, `references.get {id}`, `references.file {id, name}` → bytes, `references.text {id, name, pages?}` → the PDF's text (the reader `PdfDocument` already in `Cde/PdfRemarks`), `references.suggest {id}` → `{number?, date?, issuer?, validUntil?}` from the text, by pattern.
- `references.checklist {id}` → the certificate's items with their state.
- `references.folders` → the tree with each folder's count, the derived deliverables included.

Guests of a shared session (`EngineDispatcher.AllowedRemote`) may read (`list`, `get`, `file`, `text`, `checklist`, `folders`) and nothing else.

### 3.5 The package

- `LapinReference` entities are model entities: they travel in the project state and in commits like every other entity. With a server remote, they are kept apart on pull, as remarks are (`IssuesApart`): the server's register is their truth.
- The files are in `references/blobs/<sha256>`, listed in `references/index.json` (the records repeated for whoever opens the zip without Lapin), outside the project state. The lapin-remote spec gains `references` among the first segments that are never parts (§4 of `spec-lapin-remote.md`), with a vector. `LapinPackage.Write` keeps the blobs the entities name and drops the others, as it does for issues.
- No `IProjectUpgrade` is needed: a project without references has none, and an older Lapin skips the folder as it skips `issues/`.
- **Size.** A geotechnical study can be 50–200 MB. A file above `references.keepLimit` (default 50 MB, per project) is kept on the server and in the project folder only: the entity keeps its hash and size, and the package says it is *not held here*. This is question R-Q1.

### 3.6 The project folder on disk

When the project has a folder (`cde.setFolder`), adding a document also writes its file to `<folder>/<references root>/<folder path>/<name>`. The root is `Reference` by default (question R-Q2). Moving a document moves its copy. **Scan folder** (Manage → Reference documents…) lists the files found there that the register does not hold and offers to add them, with metadata suggested from the path (the folder) and the text. The register is the truth: a file deleted on disk is a warning, never a deletion.

## 4. Atrium Server

Under `/atrium/1.0/projects/{id}` (`{p}`), with Atrium's errors and its role rules. Schema 7. The contract goes into `docs/atrium-server.md` when it is built.

| Endpoint | Who | What |
|---|---|---|
| `GET {p}/references?folder=&type=&phase=&status=&expiringWithin=&q=` | members | The list, with each document's ETag. `q` searches the title, the number, the issuer and the extracted text. |
| `POST {p}/references` | editors, owners | Metadata plus the ids of files already uploaded (`PUT …/objects/{id}` or `…/uploads`, as for a push) → 201. |
| `GET`/`PATCH {p}/references/{gid}` | members read; editors change what they added, owners anything | A JSON merge patch with `If-Match` (as topics). |
| `POST {p}/references/{gid}/supersede`, `…/withdraw`, `DELETE …` | as above; `DELETE` owners | |
| `GET {p}/references/{gid}/files/{name}`, `…/text?pages=` | members | The file; its text. |
| `GET`/`PUT {p}/references/folders`, `…/types` | members read, owners change | The folder tree and the type list (§3.2, §3.4). |

- **Storage.** Metadata in SQLite (`references`, `reference_files`, `reference_text`). Files in the object store. `Gc.cs` counts every file a reference names as a root, withdrawn and superseded ones included.
- **On upload**, in the background as W5's checks: a PDF's text is extracted and indexed (the `Atrium.Checks` reader). Encrypted PDFs and scans get a warning: *no text, no suggestions, no search*. An IFC gets its header read (schema, application, project name), IDS runs on it when the project's rules ask, and it can be opened in the viewer.
- **The journal.** `reference_added`, `reference_changed`, `reference_superseded`, `reference_withdrawn`, `reference_deleted`, `reference_expiring` (each document once, `references.warnDays` before `validUntil`, default 30), in `…/events` and `…/activity`. The e-mail outbox (off by default) sends the expiring warning to the owners.
- **Features.** `GET /atrium/1.0/` lists `references` (and `mcp`, §6). An older server answers 404, and Lapin shows "not offered by this server".
- **The gate.** A rule in the project's rules, off by default: a submission whose purpose is `published` needs a received, unexpired certificate, and every checklist item received. An owner overrides it with a note, as with the other gates.

## 5. Lapin's interface

- **Manage → Reference documents…** on desktop and web (for a project with or without a server):
  - the folder tree on the left, with its counts, the derived deliverables included;
  - the list in the middle (type, title, issuer, number, date, valid until, status), filtered by metadata;
  - the document on the right: its metadata form, its files, a preview (PDF through pdf.js, already lazy in the web build; images; IFC through **Open as link**, docs/links.md), and its links.
  - Files can be dropped anywhere in the dialog. **Suggest** fills the number and date from the text, for the user to confirm.
  - The certificate's checklist is a tab of the certificate.
- **The project's page in the CDE web** (`cde.lapinbim.com`) gets a **Documents** tab with the same list and form, working on the server directly, without opening the project. Uploading there needs no Lapin project open. The Containers tab gains **group by folder**.
- **Expiry.** The status bar and the project's page show a counter (*2 permits expire within 30 days*). A click opens the list filtered.
- **Sync.** Save to server and Get latest run `remote.syncReferences` with the issues. **Sync issues** becomes **Sync issues and documents**.
- **Strings** go into every language in `ui/src/i18n`.
- **The web budget.** The new interface is a lazy chunk, loaded when the dialog or tab opens. The first load must not grow: the CDE web build has almost no room left (24 MB whole-build budget since 24.11.2026).

## 6. The generator

- **Fields** (`IDocumentFields` of the core module):
  - `ref.<type>.number`, `.date`, `.issuer`, `.validUntil`, `.title`: the latest received, unexpired document of that type (`ref.cu.number`);
  - `ref.<type>.count`;
  - the collection `references` (item fields `type`, `title`, `issuer`, `number`, `date`, `validUntil`, `status`, `folder`, and the extra fields), filtered with `where` as other collections are;
  - the collection `checklist` (the certificate's items: `label`, `state`, `number`, `date`, `issuer`).
- **Tables** (`IDocumentTables`): *Permits and agreements* (the checklist with the documents that answer it) and *Reference documents* (any filter).
- **Starter templates** in Romanian, then English, marked indicative, under the same rule as plan-documents' templates (Lapin does not ship normative texts it cannot check):
  - *Borderou de avize și acorduri* (the list of permits and agreements);
  - *Opis dosar de autorizare* (the index of the application package: written parts, sheets, the reference documents included, with page counts);
  - *Foaie de capăt* (the cover page).
  - The architecture report's *General data* section gains the certificate's number, date and issuer.
  - The official application form for a building permit is **not** shipped. It is a form in an annex of the regulations, and its current wording is the user's to bring. The user can make it a project template with the editor, and the guide shows how.
- **Issuing a package.** Manage → Issue… gains **Include reference documents** (a choice of documents or folders). Their files go with the issue as copies, under their own names, listed in the transmittal and the manifest with their SHA-256, so a permit application leaves as one set. On a server, the tag's `files` name them. This needs no new tag member: they are files.

## 7. MCP on Atrium Server

- **Transport.** MCP's Streamable HTTP at `/mcp` on the server (`https://cde.lapinbim.com/mcp`). It is written after the public specification, like the other protocols (JSON-RPC 2.0: `initialize`, `tools/list`, `tools/call`, with no resources or prompts at first), with no dependency. The official C# SDK (MIT) is the alternative if the specification's churn makes our own costly (question R-Q5).
- **Sign-in, step 1: personal access tokens.** Made in the account's page (*Access tokens*: a name, a scope `read` or `read+write`, an expiry), shown once, stored as a hash like the session tokens, revocable. Clients send them as `Authorization: Bearer` (`claude mcp add --transport http atrium https://cde.lapinbim.com/mcp --header "Authorization: Bearer <token>"`). The token never goes into a project or a chat: the page says to paste it into the client's configuration.
- **Sign-in, step 2 (later): OAuth 2.1** (authorization code with PKCE, dynamic client registration), which MCP's authorization specification asks for and which hosted clients such as claude.ai's connectors need. lapin-remote 1.0 lists the browser sign-in as missing, so this is the same piece of work for both.
- **Tools.** Each tool calls the same service code as the REST API, with the token's account and role:

| Tool | Scope | What |
|---|---|---|
| `cde_projects` | read | The account's projects, with their role. |
| `cde_project` | read | One project's summary: the containers' counts, the queue, open issues, the certificate's checklist, documents expiring soon. |
| `cde_folders` | read | The folder tree with counts. |
| `cde_references` | read | The list, with filters and `q`. |
| `cde_reference` | read | One document's metadata and its files. |
| `cde_reference_text` | read | A file's text, by pages, marked as the document's content, never as instructions. |
| `cde_reference_add` | write | A file (base64, up to 16 MiB; larger through an upload URL the tool returns) with metadata. |
| `cde_reference_update` | write | Metadata, the folder, the checklist links. |
| `cde_register`, `cde_containers`, `cde_submissions`, `cde_submission` | read | The register, the containers, the queue, one submission with its checks and gate. |
| `cde_topics`, `cde_topic` | read | Issues (BCF) and their comments. |
| `cde_topic_comment` | write | A comment on an issue. |

  Approving, rejecting, deleting and changing rules or members are **not** tools: people do those in the interface.
- **The journal** marks each write made through MCP (`via: "mcp"`, with the token's name), and the activity shows it.
- **Lapin's own bridge** (`scripts/lapin-mcp.mjs`) gets nothing new: the engine's `references.*` commands reach it through `lapin_query` and `lapin_execute`, by the catalogue. `docs/mcp.md` gains a section on the two bridges (the open project, the CDE) and a worked example: an agent reads a certificate's text, fills its metadata and checklist, then makes the list of permits with `document.create` and `lapin_document_export`. `scripts/lapin-mcp-check.mjs` gets a twin for the server (`atrium-mcp-check`).
- **Limits.** 60 calls a minute per token, and text pages capped per call, so one agent cannot exhaust a 384 MB container. The size and time limits are those of IDS's runs.

## 8. Foreign files in the CDE web

- **Reference models.** An IFC added as a reference document (type `model`) opens in the CDE web's viewer read-only, and in Lapin through **Open as link** (federated: docs/links.md). Its IDS report shows beside it.
- **Submitting other tools' deliverables.** The Publishing tab gets **Submit files…**: PDFs, DXFs and IFCs from disk, with revision, status and container, into the queue (`POST {p}/submissions` with `files`, which the API already takes). They then go through the W5 checks and the gate. Today only Lapin's issues fill the queue. This lets a coordinator publish a consultant's sheets through the same queue.

## 9. Stages

Each stage leaves main working and has its tests, its guide page in both languages and its scene. Code in `engine/` and in Atrium Server is done on Opus, the interface, the guide and translations on Sonnet.

| Stage | What | Where | Days |
|---|---|---|---|
| **R1** | `LapinReference`, folders, types, the checklist, commands and queries, `references/blobs/` in the package, the spec's reserved segment and its vector, the project-folder copy and Scan folder, text and suggestions. xUnit: add / move / supersede / withdraw, dedup, package round trip, an older package, the folder copy, suggestions on sample texts. | lapin-pro engine | 3–4 |
| **R2** | Manage → Reference documents… (desktop and web), the checklist tab, the expiry counter, the strings in every language; the guide page *Reference documents* (+ ro) and its scene. | lapin-pro UI | 3 |
| **R3** | The server's references API, folders and types, the GC roots, text extraction and the IFC header on upload, the journal events, the expiring warnings and e-mails, the optional gate rule; schema 7, tests as W4–W6. | Atrium Server | 3–4 |
| **R4** | `remote.syncReferences` (by GlobalId and field, files through the object store, `IssuesApart`-like on pull); the CDE web's Documents tab; group by folder in Containers. | lapin-pro engine + UI | 2–3 |
| **R5** | The generator's fields, collections and tables; the three templates (ro, then en); the architecture report's fields; Issue… with reference documents. | lapin-pro engine + UI | 2 |
| **R6** | MCP at `/mcp` with personal access tokens, the tools, the limits, the journal's `via`; `docs/mcp.md`; the server check script. | Atrium Server + docs | 3 |
| **R7** | Reference models in the viewer and as links; Submit files… in Publishing. | lapin-pro UI (+ server, small) | 2–3 |
| **R6b** (later) | OAuth 2.1 with PKCE for MCP and the browser sign-in. | Atrium Server | 3 |

Order: R1 → R2 (usable on its own, without a server) → R3 → R4 → R5 → R6 → R7. R5 can start after R1. R6 needs R3.

## 10. Open questions (with what is done meanwhile)

- **R-Q1** Large files: keep up to 50 MB per file in the `.lapin`, larger only on the server and in the folder? *Meanwhile: 50 MB, per project.*
- **R-Q2** The folder on disk: `Reference/` beside WIP / Shared / Published / Archived, or the tree's folders straight under the project folder? *Meanwhile: `Reference/`, so the ISO containers stay alone at the top.*
- **R-Q3** Who adds documents: editors and owners, viewers only read? *Meanwhile: yes.*
- **R-Q4** The default folder tree and type list (§3.2, §3.3): the user's office practice may differ. *Meanwhile: as above, editable.*
- **R-Q5** MCP written by us (as `lapin-mcp.mjs` is) or the official C# SDK? *Meanwhile: ours, a small JSON-RPC subset.*
- **R-Q6** Should the gate rule (§4) be on by default for new projects? *Meanwhile: off.*
