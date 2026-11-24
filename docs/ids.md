# IDS 1.0 checks (CDE web W6)

Atrium's `docs/plan-cde-web.md`, §5 (the IDS row) and §6 W6: an IDS 1.0 reader and checker in Lapin's engine, run from Manage → IDS… and before a submission to a CDE server. Decision 829.

**Written from the public specification only**: buildingSMART's `ids.xsd` 1.0.0, the IDS user manual (facets, restrictions, cardinality, units, metadata) and the implementers' pages on data types and tolerance. No code or file from other implementations (IfcOpenShell's ifctester, ifc-lite, bimvoice-cde) was read or used. The test suite's *cases* (their names state the behaviour) are written anew as small IFC and IDS texts in `IdsTests.cs`; no test-suite file is copied. No dependency: the XML is read with .NET's `System.Xml` (DTDs prohibited, no resolver), the patterns with .NET's regex after a translation.

## What checks what

| What | Where |
|---|---|
| The model of an IDS: info block, specifications (name, IFC versions, identifier, description, instructions, `minOccurs`/`maxOccurs` as required, optional or prohibited), the six facets, values (a simple value or an `xs:restriction`), cardinalities | `engine/src/Platform/Lapin.Ids/IdsModel.cs` |
| The reader: ids.xsd's elements, their order and attributes; the manual's allowed facet configurations (a property's value needs its dataType; an optional facet needs something to check; a prohibited property names only its set and name; a prohibited specification has no requirements; partOf takes required or prohibited only); values in their base type's lexical form (booleans `true`/`false`/`1`/`0`, numbers with a dot, integers whole, dates); restriction facets per base type (lengths on strings, bounds on ordered types); every problem with its line (`IdsFormatException`) | `IdsReader.cs`, `IdsTypes.cs` |
| XSD patterns as .NET regexes: whole value, `^`/`$` plain characters, `.` not a line end, XSD's `\w` (no underscore), `\s`, `\i`, `\c`; .NET-only constructs refused; a 2 s match limit | `XsdPattern.cs` |
| The IFC side: one pass over `StepFile.Rooted` for the relationships (types, property definitions, classifications, materials, aggregation, nesting, containment, groups, voids and fills); attributes by name (`IfcAttributes`: the IFC4/4.3 explicit attributes by family, IFC2X3 where it differs; inverse and derived ones are not there, so never met); properties and quantities, the occurrence's over its type's per set and name; single, enumerated, list, bounded and table values, complex and reference ones unsupported; measures converted to SI from the project's units or the property's own; the predefined type through the type object (USERDEFINED read from ElementType/ObjectType); classifications with the codes above them; materials by name and category through sets, layers, profiles, constituents and lists, the type's when the occurrence has none | `IfcFacts.cs`, `IfcAttributes.cs` |
| Matching: type casting (a text equals as written, booleans, whole numbers whole, other numbers within 1e-6 relative + 1e-6 absolute); restrictions: enumeration any, patterns any (texts only), bounds exact, lengths in characters | `IdsMatch.cs` |
| The checker: candidates from the file's index when the entity facet names classes, else every object; applicability = every facet holds; each requirement by its cardinality; required specifications need one applicable entity, prohibited ones none; specifications for another IFC version are `skipped`. Each failure: GlobalId, class, name, the model element (its own GlobalId, else the whole it is part of), the requirement, actual, expected, instructions; the first 2000 told in full, every failing GlobalId kept. Deterministic (the file's order), cancellable, with progress | `IdsChecker.cs` |
| The report: XLSX (Report, Specifications, Failures) through `XlsxWriter` | `IdsReport.cs` |

## In the project

- **Storage**: each IDS is an entity `LapinIdsFile` (resource) holding the file's text as it came, its name, SHA-256, size, title, version and specification count. Its id is a GlobalId made when first added; a newer copy of the same file name keeps it. `ids.store` (one undo step, "Add IDS file"), `ids.remove`, `ids.setRules` (`cde.ids.blocks`, default on: a failing specification stops a submission; `cde.ids.onSubmit`, default on: Issue… runs them), `ids.files`. Kept in the `.lapin` with the model, so it travels in commits to a CDE server. No `IProjectUpgrade`: older projects simply have none. (`Lapin.Platform/Cde/IdsFiles.cs`)
- **The calls** (`EngineDispatcher.Ids.cs`, `IdsCalls.cs`): `ids.add` (text, base64 `data`, or `path`; refused with the reader's problems), `ids.read`, `ids.run` (the model written by `IfcExport` in memory, IFC4, or IFC 4.3 when every specification asks only for it, bodies as face sets; or an IFC file given), `ids.results` (the last run, or a specification's failures a page at a time; `stale` once the model changed), `ids.makeRemarks`, `ids.report` (XLSX). The run is undo-free and kept in the engine, not in the model.
- **Remarks**: `ids.makeRemarks` writes one remark per failing specification through `ids.writeRemarks`, one undo step. Its GUID is derived from the IDS file's id and the specification's identifier (else its name), so a run again updates the same remark (its description, its first viewpoint's selection of the failing elements, reopened if it was closed) rather than adding one. No label (a CDE server takes only its project's labels; the title starts with "IDS:"); priority High for required specifications. W3's `remote.syncIssues` sends them to the server like any remark.
- **Submission**: `remote.submitIssue` and `remote.issueTag` take `idsOverride` (the user's note) and `idsFailing`; the submission's and the tag's note gets "Submitted despite n failing IDS specifications: …" after the issue's own note.
- **The browser**: `Lapin.Ids` is a lazy part (`scripts/build-web.mjs`); the dispatcher reaches it only through `IdsCall` (never inlined) and `IdsCalls` (a class of its own, so no shared closure class mentions its types). The interface's IDS panel and the submission's IDS block are lazy chunks.

## The interface

- Manage → **IDS…** (`ui/src/modules/core/ids/IdsPanel.tsx`): add (several files at once), remove, the two settings, Run the check, each specification with Pass / Fail / Skipped and its line, opened for its description, instructions, requirements and failures; a failure's click selects the element and aims the 3D view at it (`showElements`); Select the failing elements; More; Make remarks (then Sync issues when the project's remote is a signed-in server); Report (XLSX). An HTML report was written and dropped to keep the browser build within its budget (below).
- Manage → Issue… → **On the CDE server** (`IdsOnSubmit.tsx` in `IssueOnServer`): the run before Submit for approval; when it fails and the project blocks, Submit and Make the issue tag now wait for "Submit despite the IDS failures" and a note (`submitGate` in `ids.ts`).
- File → Collaborate… → Publishing: an **IDS** tab (the same panel) when a project is open (`PublishingPanel withModel`).

## Tests

`engine/tests/Lapin.Engine.Tests/IdsTests.cs` (the facets, restrictions, cardinalities, units, tolerance, specification usage and versions, XSD patterns, the reader's refusals, determinism, cancellation), `IdsCommandTests.cs` (storage and undo, the run over a modelled project with `Lapin_Intent` and the user's own set, results pages, the rule, remarks in one step and updated, the report, save and reopen, Bloc-P-4), `ui/src/modules/core/ids/ids.test.ts`.

**Bloc-P-4** (`modele/Bloc-P-4.ifc`, 2.9 MB, 411 walls; Debug on the M-series Mac): the IFC file itself checked in about 0.1–0.2 s for five specifications (one over every object); imported (2323 entities), the model written as IFC in 5–6 s and checked in 0.2 s more. The time is the export's, which IDS needs since it checks what the IFC says.

## The browser budget

The CDE web build (`node scripts/build-web.mjs lapin --cde`) stood at its limits before W6 (20 MB first load, 23 MB with every lazy part). Measured on 24.11.2026 with W6 (brotli, as build-web.mjs counts): **first load 20,960,604 bytes** (10,916 under the 20 MB budget), **everything 24,113,839 bytes** (3,409 under 23 MB). What it took: `Lapin.Ids` a lazy part (50 KB) holding the IDS calls too (the dispatcher's own copy cost 18 KB of compiled code in the first load); its types plain classes instead of records (records' generated members cost 5 KB); the HTML report dropped (XLSX stays). The interface's IDS chunks are 7.5 KB; the strings in the largest language file about 3 KB. The next feature in this build needs room made first: the native code alone varies by a few KB from build to build.

## Limits

- IDS 1.0's own: no geometry, no calculations, no clashes.
- IFC2X3's occurrence-to-type mapping table (IfcFlowTerminal with IfcAirTerminalType read as IfcAirTerminal) is not applied: Lapin writes IFC4; a foreign IFC2X3 file is checked by its own classes.
- Attribute names cover IfcRoot's objects, types, spatial elements, processes, groups and the material resources by family and an explicit table; an unusual class's attributes past its PredefinedType are not addressable.
- `maxOccurs` other than 0 reads as unbounded (IDS 1.0 uses only required, optional and prohibited).
- The failures' texts (actual, expected) are the engine's English; the interface around them is translated.
- A specification that passes again does not close its remark: the remark stays for the team to resolve.
- The run's time on a large model is the IFC export's; the results are not drawn as colours in the 3D view (failing elements are selected instead).
