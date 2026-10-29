# Large models: shared bodies and instancing (decision 166)

Branch `perf/instancing`. Merged into `main` when every stage below passes its tests.

**Status (29.10.2026): stages 1–4 done, and stage 5 for IFC.** Results are under "Measured after", and what is left under "Still open".

## Why

`cubes_1m_fixed.ifc` (251 MB): a million `IfcBuildingElementProxy` sharing one
`IfcProductDefinitionShape` (a 1 m cube), each with its own `IfcLocalPlacement`.

Measured on an M1 Max, Release, after the quadratic steps were fixed (commit 9951674):

| | 10k | 100k | 1M |
|---|---|---|---|
| import | 1.6 s | 8.9 s | 113 s |
| diff frame to the UI | 22 MB | 218 MB | **fails: over 2 GB** |
| snapshot | 0.75 s | 3.5 s | 49 s |
| plan drawing | 0.65 s | 2.9 s | 70 s |
| save / open `.lapin` | 0.27 / 0.9 s | 1.4 / 6.4 s | 18 / 88 s |
| managed memory after import | 0.4 GB | 2.3 GB | ~21 GB |

### Where the cost is, per element
- **The body, in world coordinates, as JSON on the entity** (`StoredBodies`, `attrs.body`): about 60 `JsonNode`s, 3–4 KB.
  - Copied again into every `NodeDto.Props` of a diff or snapshot, so it travels to the UI twice: as JSON in the frame's header and as the render's buffers.
  - Copied again into the recovery journal.
  - Written as a JSON string per entity in `.lapin`.
- **`Built`**: its own `Solid` (`SolidFromMesh` run again on every build), a tessellation with vertices repeated per face, edges, the `Reads` set, and seam planes. About 3–4 KB.
- **In the UI**: one `THREE.Group` per element, with a `Mesh`, a new `MeshStandardMaterial` and a `LineSegments` per part. That is about 2 draw calls per element: 2M for 1M cubes, before shadows.

## What BubbleBIM-Standalone taught

These are BubbleBIM's own techniques, rewritten here (MIT). No MPL code is used: not web-ifc, not ifc-lite.

- **Dedup by representation id** (`ifcTilePipeline.worker.ts`): a geometry used two or more times becomes local geometry, plus `{elementId, matrix, colour}` per instance. One element may own several sub-meshes; highlight, hide and delete act on all of them.
- **Dedup by hash** of the centred float32 mesh, with a double-precision centre per element (`frag_writer.py`), for files written in world coordinates. "Or a million identical cubes become a million meshes."
- **`instanceId → elementId` and `elementId → refs[]`**, driving `setColorAt` (highlight), visibility and `setMatrixAt`. Opaque and transparent instances are kept in separate batches.
- **A shared buffer is copied before it is transferred**, since it can be transferred only once. Data from a memory pool is copied before it is transformed (the Fragments pitfall).
- **Containment relationships in batches of 500** in the written IFC. The importer's quadratic copy is fixed in 9951674.
- **Kept for later, not in this plan:**
  - tiles streamed by frustum and distance under an LRU byte budget, with in-flight cancellation;
  - meshlet culling;
  - a persistent cache in IndexedDB (IFC hash → gzip tiles);
  - batches merged by (material, type, storey) for native elements, with triangle ranges for picking (2 800 → 174 draw calls for a five-storey block).

## The design

### Engine model: a shared body and its instances
- **`LapinSharedBody`**: a new entity of the platform (`core`, not an element, on no storey) that holds `body` once, in local coordinates.
- **A reference that uses it** holds two attributes:
  - `bodyOf`: the shared body's id;
  - `xf`: 12 numbers, the affine 3×4 matrix, rows `X Y Z O`, as `Xf` in Lapin.Ifc.
  
  It no longer holds its own `body`. `StoredBodies.Has` is true for both forms.
- **When the import shares a body:**
  1. **A type's map**: every body item of the product is an `IfcMappedItem` of the same `IfcRepresentationMap`.
     - Key: the map's id.
     - Instance: `Placement(product) · Operator(MappingTarget) · Axis2(MappingOrigin)`.
     - Local body: the map's representation at identity.
  2. **A shape shared by two or more products** (`IfcProductDefinitionShape`).
     - Key: the shape's id.
     - Instance: `Placement(product)`.
     - Local body: `Body` at identity.
  3. **A product with openings** cut by booleans: never shared. It is baked in world coordinates, as today.
  4. **A shape used once**: baked, as today. A shared body pays off only from two uses.
  
  A second import of the same file finds its shared bodies by a GlobalId derived from the file and the key (`Step.Guid`), the way elements are found today.
- **Geometry** (`StoredBodies.Geometry`):
  - The shared body's parts are built once per shared body and revision (`SolidFromMesh` once), kept by the session.
  - An instance's `Solid` is the shared one transformed. Under a translation it keeps the shared `Face[]`, so only the vertices are new. Under a rotation, the normals are new too. A mirror flips (`SolidBuilder.Transform`).
  - Every consumer that wants world solids keeps working unchanged: plans and sections (HLR across elements), caps, Export 3D, the IFC export, WallTops.
- **Rendering**:
  - **Shared meshes**: `RenderItem` gains `Instance: { Mesh, Matrix }`, where `Mesh` is the shared body's id and `Matrix` is 12 floats. Its `Parts` are empty.
  - **The diff** gains `Meshes`: the shared bodies' tessellated parts, sent when they are new or changed, and all of them on a reset. It also gains `MeshesRemoved`.
  - **Bounds** come from the shared body's box, its 8 corners transformed.
  - **Seams**: an instance's edges are the shared mesh's. No seams are trimmed between instances (a reference's seams are a nicety, not a drawing).
- **The wire**:
  - `NodeDto.Props` no longer carries `body` (nor a shared body's). The UI never reads it (checked), and it is the biggest part of a reference's props. This helps every reference, shared or not.
  - **A diff over a frame's limit goes out in parts.** The first part keeps `Reset`; the next ones are ordinary diffs (`Reset: false`) with increasing versions, which the UI already applies in order.
    - A snapshot's reply carries the first part, and the rest follow as diff events before the reply's promise resolves on the client. This is checked by a test with a small limit.
  - **The recovery journal** writes an import's entities as they are. The body is no longer repeated per instance, so the journal shrinks with the model.
- **`.lapin`**: nothing new. The shared body is an entity (its `body` a JSON column), and `xf` is a flat number array (`Kind.Numbers`). Saving drops a shared body that no instance uses any more. Deleting an element never removes a body another one uses.

### UI: InstancedMesh
- **Instancing** (`viewport/instancing.ts`, new):
  - For each shared mesh and part, an `InstancedMesh` of its local geometry, with one shared material per part's colour.
  - The edges are drawn by an instanced line material: the segments' local positions, plus the instance matrix as an instanced attribute, with clipping planes and fog.
  - It keeps the maps `nodeId → (batch, index)` and `(batch, instanceId) → nodeId`.
- **Per instance**:
  - colour and highlight through `setColorAt` (selection's emissive look becomes a lighter colour);
  - hidden (looks, storeys, links) as a zero-scale matrix;
  - faded (opacity < 1: scope, halftone links, phases) moves the instance to the part's second, transparent `InstancedMesh`, which casts no shadow.
- **Picking**: `instanceId` is mapped to its `nodeId`. `faceHit` turns the normal by the instance's matrix too.
- **Bounds**: fit, section box and sun use the instances' boxes (the shared box × matrix), leaving out hidden instances.
- **The walker**: instances are expanded into the triangle world within a radius of the walker (default 60 m, rebuilt as it moves on), or entirely below 200 000 triangles.
- **The store**: `render` keeps instance items keyed by `nodeId`, as today. `meshes` is a new map of shared meshes, keyed by id.

### Exports (later stage, same branch if time allows)
- IFC: a shared body is written as an `IfcRepresentationMap`, and each instance as an `IfcMappedItem` (written today as one `IfcFacetedBrep` per element).
- glTF/GLB: each instance as a node on one mesh. The export as the view shows it, cut by planes, bakes the instances it cuts.

## Stages and their tests

1. **The engine's shared bodies.**
   - The importer's two keys (type map, shared shape), `LapinSharedBody`, and `StoredBodies` for instances.
   - Geometry and bounds; render items with `Instance` and the diff's `Meshes`; props without `body`.
   - **Tests:**
     - 8 cubes sharing a shape give 1 shared body and 8 instances, each in its own place (bounds), and 1 mesh in the diff.
     - The plan draws 8 squares.
     - A rotated placement and a mirrored map are handled.
     - A product with an opening is baked.
     - A product whose items are a type's map shares with its siblings.
     - A second import does not double anything.
     - Undo removes the shared body.
     - Saving and opening keep the sharing.
     - Deleting one instance keeps the body.
     - The IFC export and a re-import give the same count of elements.
2. **The wire in parts.**
   - A diff split by size: a snapshot over the limit arrives whole.
   - **Tests:** a small limit (an internal setting), the parts' versions in order, the client's applyDiff over parts (vitest), and the frame's decoder.
3. **The UI's instancing.**
   - **Tests** (vitest): the batches from items and meshes; `instanceId ↔ nodeId`; looks to colour, hidden and faded; bounds from instances; the walker's radius.
   - **In the app:** the 100k file opened in dev (screenshot), with pick, select, hide and fade.
4. **The benchmark again**, at 10k, 100k and 1M, in this file, plus the browser's frame size. The benchmark test stays, optional (`LAPIN_IFC_BENCH`).
5. **Exports** (IFC map and glTF nodes), if stages 1–4 are in.

Merged into `main` when the engine's tests (xUnit), the UI's tests (vitest) and typecheck pass, and the 100k file works in the app.

## Measured after (29.10.2026, M1 Max, Release; `LAPIN_IFC_BENCH`)

| | 10k | 100k | 1M (before) |
|---|---|---|---|
| import | 0.76 s | 4.8 s | **61 s** (113 s) |
| managed memory after import | 0.2 GB | 1.1 GB | **9.7 GB** (21 GB) |
| the import's diff | 9 MB | 91 MB | 913 MB, sent in parts of 20,000 (failed: over 2 GB) |
| snapshot | 0.2 s | 1.0 s | **12 s** (49 s) |
| plan drawing | 0.7 s | 2.8 s | 62 s (70 s) |
| save / open `.lapin` | 0.17 / 0.34 s | 1.2 / 2.0 s | **9 / 23 s** (18 / 88 s) |

In the app (dev, engine in Debug, Chrome; `pnpm dev`, a Playwright script):

| | 10k | 100k | 1M |
|---|---|---|---|
| import, until every element is in the page | 1 s | 7.6 s | 130 s |
| the 3D view shown and fitted | – | 3.5 s | 7.8 s |
| a frame while turning, median / p95 | 17 / 17 ms | 17 / 17 ms | 17 / 150 ms |
| a pick | 0.8 s | 0.9 s | 1.8 s |
| the page's memory: JS heap after GC / renderer RSS | – | 123 MB / 476 MB | 1.07 GB / 1.64 GB |

- **Small arrays copied out of frames** (an instance's matrix; one shared object for every empty list). A typed array view kept its whole 20 MB frame alive. At 100k this took the renderer from 566 to 476 MB, and at 1M the JS heap from 4.2 GB to 1.07 GB.

## Still open
- **glTF/GLB, USD and COLLADA with real instancing** (one mesh, a node per instance; `EXT_mesh_gpu_instancing`). Today the export bakes every instance; the scene model (`MeshScene`) and its three writers need a shared mesh.
- **The plan at 1M** (62 s): every instance's solid goes through the hidden-line drawing. The fix is to draw a shared body's plan once and place it.
- **Picking at 1M** (1.8 s): the InstancedMesh raycast tries every instance. The fix is a BVH over the instances' boxes.
- **Hitches while turning at 1M** (p95 150 ms), likely the garbage collector. Restyling only what changed is in; checking the rest is open.
- **The walker** takes instances within 60 m of where the walk starts, not of where it is now.
- **One opacity per batch** for the faded instances: the first faded instance's. Mixed fades in one mesh (a phase's and a link's halftone) show the same.
- **Files written in world coordinates** (no shared representation) are not shared yet. BubbleBIM's hash of the centred mesh would catch translated copies.
- Tiles, streaming by distance, an LRU budget and a persistent cache (BubbleBIM's `TileStreamingManager`) are for files beyond a few million elements.

## A real model: the 1.2 GB Revit export (29.10.2026)

`02767_0_IDP_EJE_ARQ+EST+MEP.ifc`, 20.9 million entities (5.3 million faces), 2,857 products; one of them, a dryer
exported as a mesh, 6.8 million triangles (78 % of the model's geometry). At first the file could not be read at all:
a .NET string holds at most a gigabyte. What each step gave (M1 Max, Release, server GC; the same 549 elements
recognised and 2,255 references throughout):

| step | import | after import | open `.lapin` |
|---|---|---|---|
| the parser on bytes, in parallel; elements built on every core | 233 s | 21 GB | 140 s |
| the edges' hash (a long's own hash is a ^ b), holes to their innermost outer, triangle faces straight, a stored body's parts cached, no coplanar merge above 100,000 triangles | 251 s | 21 GB | 140 s |
| references prepared on every core; no seams for a reference's body nor a part over 20,000 faces | 111 s | 18.5 GB | 6.8 s |
| what the recognisers get prepared on every core; a B-rep over 4,000 faces is no prism | 72 s | 16 GB | 2.9 s |
| a product's shape evaluated once (bounds, extents, footprint and body asked it four times) | 45 s | 15.7 GB | 3.1 s |
| the file kept as bytes with an index, entities parsed when asked for; a shell over 20,000 faces built in parts on every core | **30 s** | **7.2 GB** | 3.1 s |

### Still open for such a file
- **The giant mesh as a mesh, not a solid of faces.** The dryer becomes 6.8 million `Face` objects (~0.7 GB), tessellated with its
  vertices repeated per face (~580 MB of positions and normals) and every triangle edge (~240 MB): the 856 MB diff frame is the
  dryer. Above a threshold, draw from the stored arrays with shared vertices and averaged normals, only the crease edges
  (`TessellateMany`'s `creaseDeg`), and build the `Solid` only for a consumer that asks (plans, sections, exports). Expected: the
  dryer ~1.5 GB → ~200 MB in the engine, the frame and the page alike.
- **The recovery journal** copies an import's every entity (the dryer's body, 230 MB of base64, among them); a large import should
  be recorded by its source file and options, and made again on recovery.
- **Decimation for display** (a 10 % mesh for the 3D view, the full one kept), and tiles streamed by distance, for bigger files still.

