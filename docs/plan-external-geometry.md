# External geometry and live scripts: the contract

**Decided 13.11.2026** (the user's approval): an external program that generates geometry (the first is Lite-STEP, a declarative Python DSL by Ginnung Technologies) works with Lapin **through Lapin's open Python API** (`lapinbim`, docs/python.md), never embedded in it. Lapin adds four general steps that any such program uses; nothing in them names or knows Lite-STEP.

**Lite-STEP's licence** (docs/plan-python-scripting.md §10): unknown when this was planned, it is **MIT** (13.11.2026: the public repository `ginnung-tech/lite-step`, Copyright (c) 2026 David Arbirk). Its runtime needs ifcopenshell (LGPL-3.0), manifold3d, numpy, shapely, pydantic and libcst, so it **stays an external process**: Lapin depends neither on it nor on ifcopenshell, and `lapinbim` never imports it. Its code was read to shape the API so its side is easy to write (below); nothing of it is copied, and the example of step 4 is Lapin's own, written from scratch, MIT.

**What Lite-STEP's side looks like, and what that asked of the API** (read 13.11.2026): a model is Python objects (`Project`, `Storey`, `Wall`, `Column`, … holding `Box`, `Extrude`, `Sweep`, `Mesh` solids) in **integer millimetres, Z up**; the compiler places them, carves the booleans and writes IFC4X3 with ifcopenshell; curved solids are meshed as `(verts N×3 float, faces M×3 int)` arrays and booleans run in manifold3d; every element has a **canonical name** derived from its place in the tree (`window:left:wall:north`), while its IFC GlobalIds are random on each compile; a `Product` is a definition placed many times. So `element.createBody` takes `units: "mm"`, a mesh as rows of three (`vertices` and `faces`/`triangles`, as numpy's arrays come out), a body set on many elements is one shared body (with a type for the `Product`), and `lapinbim.global_id(name, namespace)` makes a stable GlobalId from a canonical name (a UUID 5, compressed as IFC writes it), so an element compiled again keeps its id in Lapin. `python/examples/litestep_bridge.py` (step 4) sketches the bridge.

## 1. What an external program may do

An external program is any process on this computer that connects to the open Lapin over the protocol (127.0.0.1 only, docs/mcp.md): a script in a terminal or VS Code, the Script panel's Python, another program's own runtime. Through `lapinbim` it may:

- **make elements whose body it computes**: `element.createBody` with an IFC class, a storey and a body (a triangle mesh, or planar faces with holes), optionally a pinned GlobalId, an owner, a type, a material, a placement and attributes of its own; `element.setBody` replaces the body later, keeping the element and its GlobalId (step 1);
- **know what the user selected and measure the model**: a `selection` event and named measures with a `measure` event when one changes (step 2);
- **be told when what it depends on changes**: `script.watch` (step 3); a project's own script is re-run by Lapin, an external program gets a `watch` event and updates its bodies itself;
- everything `lapinbim` already does: commands, queries, batches and steps (one undo step each), the model's entities, quantities, events.

What it makes is an **ordinary element** for the rest of Lapin: drawn in plans and sections (cut by the view's plane like any body), in 3D, picked, scheduled and measured (its volume and area), in phases, in Visibility/Graphics by its IFC class, moved, turned, copied, mirrored, hidden and deleted like any element, and exported to IFC with its class and GlobalId. Only its **shape** is the program's: an edit that would reshape it (making it a Studio solid, changing its body attributes) is refused in plain words, *This shape is made by a script; change it in the script, or move, rotate or cut it here.*

## 2. The four steps

| Step | What it adds | Where |
|---|---|---|
| **L1** externally generated geometry | `element.createBody`, `element.setBody`; bodies stored as shared bodies by content (one copy for many elements); validation (closed or not reported, metres, limits); IFC export with the class (else `IfcBuildingElementProxy`), the pinned GlobalId and the owner in `Lapin_Intent`, read back as the same elements; *script-owned* elements shown with a hint in Properties | engine (core), IFC, UI |
| **L2** selection and measures | the `selection` event (GlobalId, class, assembly path, bounding box); `measure.define`, `measure.get`, `measure.remove`, `measure.list`, the `measure` event, built-in measures (`storey.<name>.elevation`, `element.<id>.bbox`, `wall.<id>.top`) | host, `lapinbim` |
| **L3** regeneration on change | `script.watch` / `script.unwatch`: a connection's watch (the `watch` event) or a project script's (re-run as its own undo step, *“<script> regenerated”*, trusted only) | host, `scripting` module, `lapinbim` |
| **L4** an example and the developer page | `python/examples/truss_over_room.py`, docs/python.md *External geometry and live scripts*, the user guide's note | `python/`, docs |

### 2.1 L1: bodies

```text
element.createBody {ifcClass, storey, body, name?, type?, material?, globalId?, owner?, placement?, attrs?} → {id, shared, closed, triangles, notes}
element.setBody    {id, body, placement?}                                                                 → {id, shared, closed, triangles, notes}
```

- **`body`**: `{positions: [x, y, z, …], indices: [a, b, c, …]}` (a triangle mesh; each list flat or rows of three, `vertices` and `triangles` or `faces` as other names for them), or `{faces: [{outer: [[x, y, z], …], holes?: [[[x, y, z], …], …]}, …]}` (planar polygons, holes allowed: a B-rep of planar faces), or `{parts: [{name?, color?, …mesh or faces}, …]}` for several coloured parts. Metres (or `units: "mm"` / `"cm"`, the placement's move too), the project's coordinates (or the placement's).
- **`placement`**: a 4×4 matrix (16 numbers, row major, or 4 rows of 4), or `{at: [x, y, z], angle}` (degrees round Z). The body is stored in its own coordinates and placed by it, so the same body set on many elements is stored once.
- **Checks**: finite numbers, indices in range, at most 2 000 000 triangles and 10 km from the origin; a face that is not planar within 1 mm is refused. Whether the body is **closed** (every edge shared by exactly two triangles, consistently) is reported, never required: an open body is drawn and exported, its volume is left out of its quantities.
- **Stored** as Lapin already stores bodies that came with an element (`StoredBodies`, docs/plan-instancing.md): a shared body (`LapinSharedBody`) keyed by the SHA-256 of its content, the element an instance of it with its 12 placement numbers. `setBody` with the body another element has reuses that shared body; a shared body no element uses any more goes in the same step. Moving, copying and turning (decision 448's `ReferencePlacement`) work unchanged; a copy shares the body and gets a GlobalId of its own.
- **GlobalId**: a 22-character IFC GlobalId given is validated (alphabet and first character) and refused if any entity of the model has it; none given, a new one.
- **Owner**: `"script:<id>"` or free text, kept as the element's `owner` attribute; the program's own attributes as `externalAttrs` (an object). Both go into `Lapin_Intent` on export and come back on import.
- **Class**: any IfcElement subtype of IFC4 or IFC 4.3 (the schema's list, `IfcElementClasses`); another name becomes `IfcBuildingElementProxy` and the result says so.
- **Quantities**: the generic set `BaseQuantities` with `NetVolume` (closed bodies), `NetSurfaceArea`, `Length`, `Width`, `Height` (the bounding box), so schedules and `quantity("NetVolume")` read them.
- **IFC**: written as a body Lapin stores is written (a representation map per shared body, faceted B-rep, or `IfcTriangulatedFaceSet`/`IfcPolygonalFaceSet` with the export's *faceset* geometry, IFC4 and IFC4X3); read back by Lapin as the same element (its intent says `reference`), with `owner` and `externalAttrs` restored.

### 2.2 L2: selection and measures

- **The selection**: the window already tells the engine's host what it selects (`script.selected {ids}`, stage 3 of the Python plan). From now on it does so always on the desktop (not only with Developer extras), and the host sends every local connection a `selection` event: `{ids, items: [{id, globalId, ifcClass, name, storey, path, bbox}]}` (the path: the wholes it is a part of, outermost first; the box `[[xmin, ymin, zmin], [xmax, ymax, zmax]]`). `lapin.selection()` reads it as before; `lapin.on("selection", f)` receives it.
- **Measures**: named values a connection asks the host to keep:
  - `measure.define {name, query?, args?, expr?, id?}`: a catalogue query by name with its arguments (its JSON result is the value), or a formula in Lapin's `Expr` over one element (`id`): `attr.<field>`, `qty.<QuantityName>`, `bbox.xmin` … `bbox.zmax`; or no definition: a built-in name;
  - built-ins: `storey.<name>.elevation`, `storey.<name>.height`, `element.<id>.bbox`, `wall.<id>.top` (the wall's highest point);
  - `measure.get {name}` → `{name, value, error?}`, `measure.remove {name}`, `measure.list`;
  - after every change the host recomputes only the measures whose dependencies it touched (the elements they read: a diff's changed entities and the bodies it rebuilt, which already follow `TrackingView`'s record of what each body is built from) and sends the `measure` event `{name, value, was}` to the connection that defined it when the value changed. A query measure whose arguments name no element is recomputed after every change.
- Measures belong to the connection (gone when it closes); they are not in the model and not in undo.

### 2.3 L3: regeneration

- `script.watch {ids?, measures?}` from any connection: the connection's watch; after a change touching one of the ids (or what is built from them) or changing one of its measures, it gets the event `watch {watch, ids, measures}` and does its own update (`element.setBody`). A connection's own edits never fire its own watches.
- `script.watch {script, ids?, measures?}`: a project script's watch, saved in the project (`scripting.watch {id, ids, measures}`, one undo step, on the `LapinScript`). When what it watches changes, the host re-runs the script in the Script panel's Python, **only if it is trusted on this computer** (decision 489 §7: never an untrusted project's code); else the Script panel says why it was not re-run. The re-run is **its own undo step**, labelled *“<script> regenerated”*, right after the step that triggered it (not merged into the user's step: the user can undo the regeneration alone, and an edit is never held up by a script).
- **No loops**: the edits of any script run (the Script panel's Python) never trigger project-script watches; a regeneration waits 300 ms after the last change (debounce); a run has 60 s, then it is stopped and rolled back; a run going on (the user's) postpones it; errors go to the Script panel's output and the Problems list as any run's.
- `script.unwatch {watch}` / `{script}`.

### 2.4 L4

`python/examples/truss_over_room.py` connects, takes the selected room (or the first), builds a planar Warren truss over its short side at its ceiling as one body with owner `script:truss_over_room`, watches the room, and on a change rebuilds the body with `element.setBody`, keeping the GlobalId. It runs from VS Code or a terminal against `pnpm dev`.

## 3. What we deliberately do not do

- **Route IFC through an external engine.** Lapin's IFC import and export stay Lapin's own (decision 129, docs/spec-ifc.md; exchange formats after their public specifications, no dependency). An external program gives Lapin bodies; Lapin writes the IFC. A second path would split fidelity rules (`RecognitionFidelity`) and GlobalId stability across two codebases.
- **Shared-memory Arrow IPC or "ghost" frames.** The protocol stays JSON-RPC over the loopback WebSocket with LPF1 binary frames. Shared memory would be platform-specific, unavailable to the browser engine and to the MCP bridge, and the measured cost of a body over the socket (a few ms for 100 000 triangles) does not need it. Previews while dragging are the engine's (decision: a configurator's preview is the engine's); an external program updates real elements, each update an undo step.
- **AST patching of the user's script.** Lapin never rewrites a program's source to bind parameters or inject calls. A program states what it depends on (`script.watch`, measures) and Lapin tells it; the program stays the user's text.
- **Embedding a foreign runtime or DSL.** No external package ships with Lapin; the integration is the open protocol. An edition may later add a declarative layer over `lapinbim` (docs/plan-python-scripting.md §10; the licence, MIT, would allow it): a decision still to take.

## 4. Where it is

- Engine: `engine/src/Platform/Lapin.Platform/ExternalBodies.cs` (commands, checks, quantities, driven-element refusal), `IfcElementClasses.cs`; the IFC import's `Referenced` restores `owner`/`externalAttrs`.
- Host: `engine/src/Host/Lapin.Host/LiveLinks.cs` (selection event, measures, watches, regeneration).
- Python: `python/lapinbim` (`create_body`, `set_body`, `measure`, `watch`), `python/examples/truss_over_room.py`.
- UI: the Properties hint (`ui/src/shell`), the selection always told (`ui/src/modules/scripting`).
