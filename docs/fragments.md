# Fragments (That Open Engine): in and out of Lapin

Asked on 30.10.2026 (decision 179): "I have in BubbleBIM Standalone a Python script that converts IFC files to fragments; could we use it to adapt a Fragments importer and exporter in Lapin?"

## Where it comes from

The script (`BubbleBIM-Standalone/backend/ifc_convert`) has two parts: the geometry from ifcopenshell (LGPL, Python: cannot ship in Lapin), and a writer and reader of the `.frag` file done by hand over the FlatBuffers wire format, with the conventions of the format read off real files. Lapin has its own geometry (every element tessellated in parts by material, the same `MeshScene` the glTF, USD and COLLADA exports take), so the second part was ported to C#, with no dependency:

- `engine/src/Platform/Lapin.Mesh/Fragments/FlatBuffers.cs`: `FlatBuilder` (tables, vtables, vectors, structs, strings, offsets, written back to front with FlatBuffers' alignment) and `FlatTable` (a reader by slot).
- `FragSchema.cs`: the vtable slot numbers of every table, from the generated code in `@thatopen/fragments` **3.4.5**. The order of the fields is the contract, not their names; after an upgrade of the package the file is derived again (the `grep` is in its header).
- `FragWriter.cs`: `MeshScene` → `.frag` (zlib, or raw).
- `FragReader.cs`: `.frag` → `FragModel`, plain data.
- `FragImport.cs`: `FragModel` → references in the open project.

The format is MIT (ThatOpen/engine_fragment).

## The conventions kept (not in the schema)

1. **Items with geometry come first**, so `meshes_items` is the identity array.
2. **Geometry per element, near the origin**: one global transform holds the element's centre in double precision; its shells hold points relative to it in float32 (at a site coordinate of 400,000 m a float32 steps by ~30 mm). Points are quantised to 0.01 mm, so two copies of one body hash to one shell.
3. **One sample per material** of an element; identical shells (points, triangles, face ids) are stored once: a thousand identical columns are one shell and a thousand samples.
4. **Synthetic ids** for transforms, representations, materials and samples, above every item's id; `max_local_id` past them.
5. **Attributes are JSON triples** `["Name","Wall 1","IFCLABEL"]` (Name; ObjectType, the type's name); the GlobalId goes to `guids`/`guids_items`. **Relations are JSON arrays**: `ContainedInStructure` on elements, `ContainsElements` on storeys, `IsDecomposedBy`/`Decomposes` down the project, site, building, storeys.
6. **The spatial tree alternates** a category node (`IFCPROJECT`, `IFCSITE`, …, each element class) and entity nodes (their local ids), as the reference files have it and the library's tree reader expects.
7. **Y up** (three.js): the model's `(x, y, z)` is written `(x, z, −y)`, a rotation, so the winding holds. `coordinates` is the identity: the geometry is in the project's coordinates already; with the survey origin, the real coordinates go in the metadata's `crs.mapConversion`, never in the float32 vertices.
8. **Face ids**: one per triangle, shared by the triangles in one plane, what a pick highlights whole. Holes stay empty.
9. **No file identifier**; zlib unless raw is asked for (`core.load(bytes, {raw})` expects either).

## The import

Every item with geometry becomes a reference (`StoredBodies`): its class from the category (`IFCWALL` → `IfcWall`, the proxy when unknown), its name from `Name`, its predefined type, the file's attributes under `properties.Fragments` (shown under IFC properties), `fragment` = the local id, the category, the model's guid. Its id is the file's GlobalId (else one derived from the model's guid and the local id), so importing the file again updates what it brought.

Items whose pieces are the same (the same shells, materials and local placements) share one `LapinSharedBody`, in the file's own frame turned Z up, and stand as instances of it (`bodyOf`, `xf`: the file's global transform taken through the frame change); an item alone gets its body in world coordinates. Shell profiles are polygons, triangulated as fans; a shell's holes are read by its outer profiles (noted). Circle extrusions (pipes, bars) are left out (noted). The elements go to the storey the file contains them in, matched by name to the project's storeys, made when missing at the file's `Elevation` (else the lowest element). Nothing is recognised into native elements: a `.frag` holds triangles, not axes and layers.

## How it is checked

- `FragmentsTests`: the conventions above on a small house, the zlib form, the export through `MeshExport`, the import (bodies, places, storeys, sharing, re-import, undo), a storey made, a refusal.
- **The Python reader as an oracle** (optional): `LAPIN_FRAG_PYTHON=<python with flatbuffers and numpy> LAPIN_FRAG_PACKAGE=<BubbleBIM-Standalone/backend> pnpm test:engine` runs `ifc_convert.frag_reader` on Lapin's file and compares the counts.
- **The official library** (decisive): `node scripts/fragments-check.mjs <file.frag> [<node_modules with @thatopen/fragments>]` loads the file with `SingleThreadedFragmentsModel` and prints the categories, the items with geometry, their guids, the metadata, the spatial tree, the attributes and the geometry it hands a renderer. The library is not a dependency of Lapin (three.js, web-ifc); it is taken from BubbleBIM Standalone's `node_modules`. `LAPIN_FRAG_SAMPLE_OUT=<path> dotnet test --filter FragmentsSampleWriter` writes a sample to feed it.

## Decided by default (to confirm)

1. Only Name and ObjectType go out as attributes; Lapin's property sets and quantities could follow (as the IFC export writes them) if a viewer needs them.
2. The import keeps everything as references; recognising walls or floors from a `.frag` is not planned (no axes, no layers).
3. Textures do not go out (Fragments carries colours); the material's colour and opacity do.
