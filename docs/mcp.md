# Lapin and AI agents: the MCP bridge

Every Lapin tool is an engine command with JSON parameters. `scripts/lapin-mcp.mjs` lets an AI agent drive the Lapin that is open, through the Model Context Protocol, on stdio.

The agent's edits go through the same engine as the user's, so:
- they show at once in the window;
- each one is one undo step.

## Setting it up

```sh
claude mcp add lapin -- node /path/to/lapin-pro/scripts/lapin-mcp.mjs
```

Any MCP client that runs a stdio server works the same way. The bridge needs Node 22 and nothing else.

## Finding the engine

While it runs, the engine writes its port to `<user-dir>/host.json`. The user folder is:
- `~/Library/Application Support/ro.ciuntucbimstudio.lapin` for the desktop app on macOS;
- `~/.local/share/Lapin` under `pnpm dev`.

`LAPIN_PORT` overrides this (`pnpm dev` uses 47810).

The engine listens only on 127.0.0.1.

## Tools

| Tool | What it does |
|---|---|
| `lapin_info` | The product, its modules, every command with the arguments it reads, the queries, and the classes with their fields. Call it first. The arguments come from the engine source beside the script (each handler's `a["key"]`), so they never drift; keys read inside helpers are not listed, and the command's error names them. |
| `lapin_model` | The entities (id, class, name, storey, attributes), optionally only some classes. |
| `lapin_execute` | One command, for example `architecture.addWall {x1, y1, x2, y2}`. Lengths are in metres. |
| `lapin_query` | A question that changes nothing, for example `view.drawing`, `schedule.csv`, `civil.elevationAt`, or `core.dependents {id}`: what is built from an element and what it is built from (docs/dependencies.md), to know what an edit changes before making it. |
| `lapin_quantities` | An element's quantity sets. |
| `lapin_documents` | The project's written documents and the templates a new one starts from, in one call. |
| `lapin_document_export` | A document written as DOCX, ODT or PDF to an absolute path on this computer (see Documents). |
| `lapin_undo`, `lapin_redo` | One step back or forward. |
| `lapin_save` | Save the project. |

## Documents

The project's written documents (docs/plan-documents.md) are engine commands and queries like the rest. An assistant makes one this way:

1. `lapin_documents`: the templates (`memoriu-arhitectura`, `caiet-sarcini-finisaje`, `borderou`, and the company's) and the documents there are.
2. `lapin_execute` `document.create {template, name?}`: a document from a template; it returns its `id`.
3. `lapin_query` `document.fields {id}`: what the document can read (every field with its value now, the collections, the tables); `document.preview {id}`: the document filled, with `missing`, the fields the project does not fill yet, and `inputs`, the texts the template asks for.
4. What is missing is written where it lives: the project's data with `lapin_execute` `project.setInfo {client, address, number, phase, designer, …}`; the document's own texts with `document.update {id, inputs: {key: text}}` (a null removes one). `document.update {id, blocks}` replaces the blocks; an image block shows a view (`{kind: "image", view, scale?, width?, caption?}`), cropped to one of its named view boundaries with `boundary` (`LapinViewBoundary`, `view.addBoundary`), as a sheet's viewport bound to it shows it.
5. `lapin_document_export {id, format, path}`: the file written to `path` (absolute; its folders made; a relative path is refused). It returns `{path, bytes, name, missing}`.

A DOCX or ODT exported this way carries its pictures as SVG only: the bridge draws them from `document.images` as the interface does, but has no rasteriser, so the engine puts a blank PNG beside each (Word 2016 and later and LibreOffice show the SVG). A PDF is drawn by the engine whole.

```text
lapin_documents                                         → {templates: [{id: "memoriu-arhitectura", …}], documents: []}
lapin_execute document.create {template: "memoriu-arhitectura", name: "Memoriu"}   → {id: "e41"}
lapin_query   document.preview {id: "e41"}              → {…, missing: ["project.client", …]}
lapin_execute project.setInfo {client: "Ionescu Maria", address: "Str. Florilor 12, Huși"}
lapin_execute document.update {id: "e41", inputs: {situatiaExistenta: "Teren liber, fără construcții."}}
lapin_document_export {id: "e41", format: "docx", path: "/Users/me/Desktop/Memoriu.docx"}
                                                        → {path: "/Users/me/Desktop/Memoriu.docx", bytes: 9461, missing: [...]}
```

`node scripts/lapin-mcp-check.mjs [out-dir]` checks the bridge against an engine of its own on a free port (never the one of `pnpm dev`): it drives the bridge over stdio through this flow, with a picture cropped to a boundary, and checks the PDF, DOCX and ODT it writes.
