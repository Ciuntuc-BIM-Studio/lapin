# Large models: shared bodies and instancing (decision 164)

Branch `perf/instancing`. Merged into `main` when every stage below passes its tests.

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
