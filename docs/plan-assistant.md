# The model assistant (decision 171)

A local-LLM assistant in the desktop app that reads the open project, answers questions about it, makes quick reports, and enriches it with property values the user approves. It never designs, never touches geometry, and the model never writes code: it acts only through a small set of tools Lapin gives it.

Asked for and approved by the user on 29.10.2026.

## Principles

- **Local only.** The model runs in [Ollama](https://ollama.com) on this computer (`http://127.0.0.1:11434`). Nothing about the project leaves the machine. The address setting accepts only loopback (`127.0.0.1`, `localhost`, `::1`, always written as `127.0.0.1`, which the desktop's content policy allows).
- **Desktop only.** The web build (the engine in a Web Worker, `VITE_ENGINE=worker`) has no assistant at all: no panel, no settings, no menu entry, no command. The gate is a build-time constant there, so the bundler drops the code (checked: the web bundle has no `/api/chat`, no `propose_properties`, no assistant chunk; only the i18n tables carry its phrases). A guest in a shared session has none either.
- **Read, analyse, report, enrich.** Never design, never geometry, never delete, never another command.
- **Tools, not code.** The model sees eight tools, not the generic `lapin_execute` of the MCP bridge (`docs/mcp.md`).
- **The engine or deterministic code computes.** Every count, sum, average, minimum and maximum comes from the `aggregate` tool (TypeScript over the session and the engine's quantities). The system prompt forbids the model's own arithmetic.
- **Proposals, not edits.** The one tool that writes, `propose_properties`, only fills a changeset. The user ticks and applies it; Apply is one engine edit, one undo step.
- **Small context.** Each result is capped at 6,000 characters of JSON, with paging (`offset`, `limit` ≤ 50, `next_offset`, `truncated`); the whole list goes to the chat's table for the user, not to the model.

## Architecture

```
AssistantPanel (React, right side tab)            ui/src/assistant/AssistantPanel.tsx
  └ store.ts — the conversation, the changeset, send/stop/apply
      └ loop.ts — one turn: /api/chat (stream, tools) ⇄ tool calls, ≤ 8 rounds
          ├ ollama.ts — address, models, status, the transport (fetch | shell)
          └ tools.ts — the tools over the session (nodes, relations) and the engine
                ├ quantities.many   (new protocol method, read-only)
                ├ doc.query params.standardSets (new query: the standard Pset table)
                └ doc.query schedule.csv
  Apply → execute('edit.batch', [element.setParam {ids, pset, name, value}…])  — one undo step
```

**Where the loop runs.** In the interface, which already holds the model (the session's nodes and relations) and talks to the engine; Ollama only sees the conversation and the tools' results.

**CORS (checked against Ollama 0.33.3 on 29.10.2026).** Ollama's default `OLLAMA_ORIGINS` accepts `tauri://localhost` (the webview on macOS and Linux) and `http://localhost:*` (`pnpm dev`, `pnpm dev:desktop`): `fetch` from the webview works there, and the desktop CSP already allows `http://127.0.0.1:*`. Windows' webview is `http://tauri.localhost`, which Ollama answers with 403. There the HTTP goes through the desktop shell: the Rust command `ollama_request` (plain HTTP/1.1 to 127.0.0.1 only, `/api/` paths only, chunked bodies read line by line and sent through a Tauri channel; `ollama_cancel` stops it). `transportFor(origin, desktop)` picks one. No browser path, no engine path.

**The gate.** `ui/src/assistant/gate.ts`: `ASSISTANT_BUILD = import.meta.env.VITE_ENGINE !== 'worker'` (build time) and `gateOpen({ build, desktop, guest, devOverride })`; `available.ts` feeds it `isDesktop()` (files.ts, the app's Tauri check) and `isGuest()`. Every way in also tests `import.meta.env.VITE_ENGINE !== 'worker'` inline, so the web build drops it. For development, a dev server (never a build) opens the gate with `localStorage['lapin.assistant.dev'] = '1'`: that is how the guide's scene and the real-model trials run in a browser.

**The right panel.** On the desktop the properties side gets two tabs, *Properties* and *Assistant* (`usePanels.propsTab`, `showPropsTab`); folding it folds both, and its rail has an Assistant icon that unfolds it on the assistant. Entries: View → Interface → Assistant, the command `app.assistant` (palette, assignable shortcut). The conversation lives in a store, so switching tabs keeps it.

## Tools

All take and return JSON; lengths in metres, areas m², volumes m³. A failure is returned to the model as `{error}`, never thrown.

| Tool | Arguments | Returns |
|---|---|---|
| `model_overview` | — | project, storeys (id, name, elevation, element count), elements by class, types with their use count (≤ 60), materials (≤ 40), schedules, the selection's size |
| `list_elements` | filter¹, `fields[]` (≤ 8), `sort_by`, `descending`, `offset`, `limit` (≤ 50) | `{total, offset, rows[{id, class, name, storey, type, …fields or the class's base quantities}], next_offset, truncated?, note?}` |
| `get_element` | `id` | attributes (scalars), its own and its type's parameters (with their set), the IFC file's property sets (capped), quantity sets |
| `aggregate` | filter¹, `group_by[]` (≤ 3 fields), `metrics[{op: count·sum·avg·min·max, field}]` | `{elements, groups[…], total, elements_without_the_field?, note?}`; the chat gets the table with a Total row |
| `property_names` | `class` | the standard sets for that class with their property names and kinds, the parameters already used on it, the rule |
| `list_schedules` | — | the schedules (id, name, kind) |
| `read_schedule` | `id`, `offset`, `limit` | the schedule's columns and a page of rows (from `schedule.csv`) |
| `propose_properties` | `changes[{id, pset, name, value}]` (≤ 500) | `{proposed, refused[{id, name, why}], note}`; the accepted ones go to the changeset |

¹ The filter: `class` (IfcDoor, "doors", several comma-separated), `storey` (name or id), `type` (part of the type's name), `name_contains`, `selected_only`, `where[{field, op, value}]` with `op` one of `= != > >= < <= contains exists missing` (also `gt`, `eq`, `not_equals`… as models write them). A field is a quantity (`Width`, `Length`, `NetSideArea`… the base set's first), an attribute, a parameter, `Set.Name` (`Pset_DoorCommon.FireRating`, `Qto_WallBaseQuantities.Length`) or one of `id class name storey type`. A value may carry a unit (`"1 m"`, `"900 mm"`, `"1,2"`). A field no candidate has is named back with the fields they do have (`note`), so the model can ask again.

## Property sets: the rule

Held in the engine (`StandardPsets`, `engine/src/Platform/Lapin.Platform/StandardPsets.cs`), sent to the interface by `params.standardSets`, checked by both:

- A set starting with `Pset_` is IFC's own: only one Lapin knows (the IFC 4 `Pset_…Common` of walls, curtain walls, slabs, roofs, columns, beams, members, plates, footings, doors, windows, coverings, railings, stairs, ramps, chimneys, proxies and spaces), on its classes (and their types), with one of its property names; the value is held as the property's kind (FireRating text, IsExternal yes/no, ThermalTransmittance number, Span length…).
- Any other set is the user's own and must not start with `Pset_` or `Qto_`, nor be `Lapin_Intent`, `Lapin_Phase` or `Pset_Lapin…`. No set: `Lapin_Parameters`, as before.

The values are the elements' user parameters (`params`), now with an optional `pset`: `element.setParam {ids, pset?, name, value, kind?}` and `element.removeParam {ids, pset?, name}`; a parameter is its set and its name. The properties panel shows the set before the name. The IFC export writes a standard set's values into the common set Lapin already writes (`Pset_DoorCommon`: the user's value over Lapin's of the same name), and each set of the user's as its own `IfcPropertySet`; text in a standard set is an `IfcLabel`.

## The approval flow

1. The model calls `propose_properties`. Each value is checked (`changeset.ts`: the element exists, the set and name are allowed, the value fits the kind) and becomes a row: element, set, name, its value now (its parameter, else what its IFC file said), the new value, ticked. A later value for the same element, set and name replaces the earlier one; a value equal to the current one is left out. Refused ones go back to the model with why.
2. The panel shows **Proposed changes**: a tick box per row and one for all, the element's name (a click selects it), `Set · Name: now → new`.
3. **Apply** sends the ticked rows as one `edit.batch` of `element.setParam` steps: one transaction, one undo step. **Reject** empties the changeset. A new chat keeps the changeset until it is applied or rejected.

## Models

The default is the first installed of `qwen3-coder:30b`, then `gemma4:12b`, else the first model installed (embedding models are left out of the list). The status line checks `/api/tags` and, for the chosen model, `/api/show`'s `capabilities` (a model without `tools` is said). The user's measurements: qwen3-coder:30b 66 tok/s with correct tool use, gemma4:12b 26 tok/s correct, qwen3.8:27b failed the two-step tool test.

Requests: `/api/chat` with `tools`, `stream: true`, `think: false`, `temperature 0.1`, `num_ctx 16384`. The system prompt: only the tools, never invented data, numbers from `aggregate`, no geometry or deleting, proposals only, the Pset rule, metres, page with offset, answer briefly in the user's language (the interface's language as the fallback). A reasoning channel some models leak with thinking off (gemma4's `thought … <channel|>`) is cut from the answer.

## Limits

- At most 8 rounds of tool calls per question, then the model is asked to answer with what it has.
- Tool results ≤ 6,000 characters; lists ≤ 50 rows a page; tables in the chat ≤ 5,000 rows; ≤ 500 proposals per call; `quantities.many` ≤ 5,000 ids per call (the tools ask by 2,000).
- A local model can misread a question (gemma4:12b once filtered doors by height for "wider"; see the trial below): the user reads the answer and the changeset before applying.

## Stages

1. **Done (29.10.2026):** the tools, the loop, the panel, the changeset and Apply, the Pset rule in the engine and the export, the shell transport for Windows, tests (vitest: tools, aggregation, truncation, the Pset rule, the changeset, the loop against a fake Ollama, the gate; xUnit: the rule, the export, one undo step; Rust: the line reader), the guide's page and its scene (a stand-in for Ollama, under the development switch).
2. Next, if wanted: proposals for types (a type's parameter, which its elements inherit), a selection tool ("select the doors it found"), exporting a report as XLSX through the engine, more standard sets (MEP), and a model-quality check in the settings (the two-step tool test, run on demand).

## The first trial with real models (29.10.2026)

The test model `testLapin.lapin` (the old Lapin's `modele/`; 2 storeys, 3 walls, a slab, a column) with two 900 mm doors, one 1200 mm door and two more walls on Level 1, driven through the real interface (Chrome, `pnpm dev`-style server, the real engine and Ollama):

- **qwen3-coder:30b** (≈ 53 tok/s here): *walls per storey and total length* → `model_overview`, `aggregate` → Ground Floor 3 walls 5.915 m, Level 1 2 walls 9 m, total 14.915 m, right (16 s with the model loading, then a few seconds). *Doors wider than 1 m: FireRating EI30* → `list_elements`, `property_names`, `propose_properties` → D3 only, `Pset_DoorCommon.FireRating` — → EI30; Apply wrote it as one step ("Edit"), one undo took it back (5 s). The same in Romanian answered in Romanian, same result. *Delete all the walls on Level 1* → refused, no tool called.
- **gemma4:12b** (≈ 25 tok/s): the same report, right; the English EI30 request right. In Romanian it filtered by height instead of width and proposed all three doors — which is what the approval step is for. It first answered an English question in French until the prompt named the interface's language as the fallback, and leaked its reasoning channel, now cut.
