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
5. **Attributes are JSON triples** `["Name","Wall 1","IFCLABEL"]` (Name; ObjectType, the type's name; Tag, PredefinedType: decision 323); the GlobalId goes to `guids`/`guids_items`; the type, property and quantity sets and materials are items of their own (decision 323). **Relations are JSON arrays**: `ContainedInStructure` on elements, `ContainsElements` on storeys, `IsDecomposedBy`/`Decomposes` down the project, site, building, storeys.
6. **The spatial tree alternates** a category node (`IFCPROJECT`, `IFCSITE`, …, each element class) and entity nodes (their local ids), as the reference files have it and the library's tree reader expects.
7. **Y up** (three.js): the model's `(x, y, z)` is written `(x, z, −y)`, a rotation, so the winding holds. `coordinates` is the identity: the geometry is in the project's coordinates already; with the survey origin, the real coordinates go in the metadata's `crs.mapConversion`, never in the float32 vertices.
8. **Faces**: the triangles in one plane share a face id, what a pick highlights whole, and are written as one profile per region with its holes (decision 323; a region that is not clean keeps its triangles).
9. **No file identifier**; zlib unless raw is asked for (`core.load(bytes, {raw})` expects either).

## The import

Every item with geometry becomes a reference (`StoredBodies`): its class from the category (`IFCWALL` → `IfcWall`, the proxy when unknown), its name from `Name`, its predefined type, the file's attributes under `properties.Fragments` (shown under IFC properties), `fragment` = the local id, the category, the model's guid. Its id is the file's GlobalId (else one derived from the model's guid and the local id), so importing the file again updates what it brought.

Items whose pieces are the same (the same shells, materials and local placements) share one `LapinSharedBody`, in the file's own frame turned Z up, and stand as instances of it (`bodyOf`, `xf`: the file's global transform taken through the frame change); an item alone gets its body in world coordinates. Shell profiles are polygons, triangulated in their plane with their holes (decision 323; before, as fans, the holes filled). Circle extrusions (pipes, bars) are left out (noted). The elements go to the storey the file contains them in, matched by name to the project's storeys, made when missing at the file's `Elevation` (else the lowest element). Nothing is recognised into native elements: a `.frag` holds triangles, not axes and layers.

## How it is checked

- `FragmentsTests`: the conventions above on a small house, the zlib form, the export through `MeshExport`, the import (bodies, places, storeys, sharing, re-import, undo), a storey made, a refusal.
- **The Python reader as an oracle** (optional): `LAPIN_FRAG_PYTHON=<python with flatbuffers and numpy> LAPIN_FRAG_PACKAGE=<BubbleBIM-Standalone/backend> pnpm test:engine` runs `ifc_convert.frag_reader` on Lapin's file and compares the counts.
- **The official library** (decisive): `node scripts/fragments-check.mjs <file.frag> [<node_modules with @thatopen/fragments>]` loads the file with `SingleThreadedFragmentsModel` and prints the categories, the items with geometry, their guids, the metadata, the spatial tree, the attributes and the geometry it hands a renderer. The library is not a dependency of Lapin (three.js, web-ifc); it is taken from BubbleBIM Standalone's `node_modules`. `LAPIN_FRAG_SAMPLE_OUT=<path> dotnet test --filter FragmentsSampleWriter` writes a sample to feed it.

## Decided by default (to confirm)

1. ~~Only Name and ObjectType go out as attributes~~: since decision 323 the type, quantities, the user's parameters, an imported element's sets and the materials go out too, as That Open's importer writes them.
2. The import keeps everything as references; recognising walls or floors from a `.frag` is not planned (no axes, no layers).
3. Textures do not go out (Fragments carries colours); the material's colour and opacity do.

## Exchange: IFC and Fragments alike (decision 205, 31.10.2026)

Asked for on 31.10.2026: the Fragments import did not work, not even for what Lapin exported; IFC and Fragments to be the main exchange, in one place, with the same panels and filters on import and on export.

What was wrong, measured on two files the user exported (OneDrive, 31.10.2026), of 7 MB (20,650 items) and 81 MB (17,628 items):

- The import took 128 s and 122 s with no progress shown: it looked stuck. Two quadratic steps: each element was contained in its storey one at a time (`IEdit.Relate` copied the storey's growing list and indexed it again: 88 s), and the import's group (decision 189) did the same (47 s). `EditRelations.RelateAll` writes a relationship once for many; `RelatedSets` answers "is this id in that relationship" from a set kept per (immutable) relationship instead of walking a storey of 20,000 for each element, which also speeds up every "which storey is it on" on large models. Now 5.4 s and about 25 s (of which some 8 s are the screen's coarser copies, decision 188; the simplifier's stray measure was made cheaper on the way).
- A file exported from the open project and imported back into it brings nothing: its elements are the project's (same GlobalIds) and stay Lapin elements. The result now counts them apart (`native`) and the interface says so plainly instead of "0 elements".
- The interface showed no progress for a Fragments import; it now uses the IFC import's overlay.
- `Building.frag` (That Open's sample) holds one item with geometry: read correctly.

The exchange, in the interface:

- File menu: **IFC and Fragments** (Import IFC…, Import Fragments…, Link IFC…, Export IFC or Fragments…) apart from **Other formats** (DXF, meshes, Export 3D for glTF, USD and DAE: surfaces for rendering, no longer Fragments).
- **Import Fragments…** surveys the file first (`project.fragSurvey`, `FragSurvey`: the IFC survey's shape, kinds by discipline by `IfcSurvey.KindOf`/`DisciplineOf`, storeys by the items' `ContainedInStructure`) and opens the IFC import's panel (`ImportIfcDialog` with `SurveyChoice`); `FragImportOptions` (kinds, storeys, group) choose what comes in, the file read once and kept between the survey and the import.
- **Export IFC or Fragments…** (`ExchangeExportDialog`): the project surveyed the same way (`project.exportSurvey`, `ModelSurvey`), the same choice of kinds and storeys, the phase and only what the 3D view shows; IFC's schema and parts, Fragments' origin. The engine turns the choice into the elements left out (`ModelSurvey.Excluded`): `IfcExportOptions.Exclude`, and the mesh scene's hidden set for Fragments.

Tests: `FragmentsTests` (a file surveyed and imported by kinds and storeys; one exported from the project counted as native), `ExchangeChoiceTests` (the export survey, an IFC export of one kind), the guide's `import-frag` scene and the `export` scene's exchange dialog.

## The cross-check with That Open's viewer (decision 323, 05.11.2026)

Asked on 05.11.2026: "the .frag export may not work correctly; investigate it, possibly against the That Open Components viewer in BubbleBIM Standalone". Checked against the library BubbleBIM Standalone's viewer uses, `@thatopen/fragments` **3.4.5** (with `@thatopen/components` 3.4.6, three 0.182, web-ifc 0.0.77): the schema Lapin writes (`FragSchema`) is that version's, field for field; nothing needed a newer library. The reference was the same project's IFC (Lapin's own IFC export) turned into `.frag` by That Open's `IfcImporter`, and the user's own export (`model1 frag.frag`, 31,861 items).

What was found:

- **The geometry was right.** In the official viewer Lapin's files draw as Lapin's 3D does (the same boxes, triangles, colours, glass transparent; Y up; the double-precision placement per element), the user's model included.
- **The viewer's properties panel showed only Name and ObjectType.** That Open's file of the same project carries, per element, `Tag`, `PredefinedType`, the type (`IsDefinedBy` → `IFC…TYPE`, `ObjectTypeOf` back), the property and quantity sets (`IsDefinedBy` → `IFCPROPERTYSET` / `IFCELEMENTQUANTITY`, their values by `HasProperties` / `Quantities`, `DefinesOccurrence` back) and the materials (`HasAssociations` → `IFCMATERIAL`, `AssociatedTo` back). Lapin's now does the same (`FragContext`; a value the same as one written is that one; a set stays each element's own, since a set shared by many leads the library's `getItemsData`, as the panel asks it, from element to element through `DefinesOccurrence` until its stack overflows, measured on the user's model): the quantities the modules measure (the IFC export's), the user's parameters (named sets, the others as `Lapin_Parameters`), an imported element's own sets, the materials of its parts, its type, one item per type and per material. Not written: the computed `Pset_…Common` and `Lapin_Intent` the IFC export adds (a viewer reads them from the IFC).
- **The project, site and building had no GlobalId** in the file; they have the model's now. `uniqueAttributes` and `relationNames` (what the library's `getAttributeNames`, `getRelationNames` read) were empty; they are filled as the library's own writer fills them.
- **The map conversion** had only eastings and northings, without the true north and without the system: it is now the IFC export's `IfcMapConversion` (the base point's angle, the system's name, datum, projection and unit), moved to the export's origin, written whenever the project has a survey point. Its height is the base point's elevation plus the origin's, as the IFC's (`orthogonalHeight`).
- **Every triangle was a profile of its own.** The official importer writes a planar face as one polygon and a hole (a wall's side around its window) as a hole of it; Lapin wrote twice the profiles, each a FlatBuffer table. `FragFaces` now merges each face's triangles (one face id: one plane) into its boundary loops: loops turning with the normal are outer profiles, the others holes of the outer that holds them; a region that is not clean (an edge used twice the same way, a vertex the boundary passes twice, an area not the triangles') keeps its triangles. Strings that repeat (categories, attributes, relations) are written once (`CreateSharedString`, as the official builders). The model of the cross-check: 14,311 profiles → 8,986 (That Open's: 7,273), the file 186 KB → 154 KB with its properties added; Bloc-P-4 imported from IFC, 198 KB → 291 KB with its properties (That Open's of the same IFC: 731 KB). The user's model (31,866 elements, 4.65 million triangles, imported back from its IFC): written in 16 s, 67 MB with 2.4 million property values (159,000 distinct) where the export without properties was 54 MB and That Open's of the same IFC is 39 MB; the rest of the difference is bodies turned in space, which Lapin writes once per position (That Open shares them through a local transform): not done.

The reverse, Lapin's import of a `.frag`:

- **Holes were filled and concave faces fanned wrong**: a profile is now triangulated in its plane with its holes (`Triangulator.Face`, Lapin's own ear clipping), so That Open's walls show their windows again.
- **That Open's file landed off by its `coordinates`**: the importer stores the model moved near the origin and keeps the move (stored = coordinates · project); the import takes it back, so the file's elements stand where the IFC's do, where Lapin's own import of that IFC puts them (Bloc-P-4: the same box to the millimetre).
- **Glass came in opaque**: a part whose colour lets light through takes a material of that colour and transparency (one per colour, `Fragments 9CC8E8 40%`), named on the stored body's part (`material`, read by `StoredBodies`).
- **The property sets were left behind** (the items without geometry "read for their names only"): an element's sets come in under its properties by name (a part's set with its description), its type's and materials' names with its attributes (`Type`, `Material`), shown under **IFC properties** as an IFC reference's.

### How to check it again

1. The files: `LAPIN_FRAG_CHECK_OUT=<folder> dotnet test engine/tests/Lapin.Engine.Tests --filter FragmentsCrossCheckWriter` writes, for a two-storey house with everything (layered walls with a door and windows, a curved wall, a floor, a roof, columns, a beam, Studio solids, a steel member, a heating run), the same house georeferenced, and IFC cubes sharing bodies, each as `.frag`, `.glb` (Lapin's geometry) and `.ifc`; `LAPIN_FRAG_CHECK_FILES="a.lapin;b.ifc"` adds projects or IFC files, `LAPIN_FRAG_CHECK_IMPORT="x.frag;…"` imports `.frag` files into an empty project and writes them back as GLB, `LAPIN_FRAG_CHECK_FRAG_ONLY=1` writes only the `.frag` (a big model).
2. The reference: That Open's `IfcImporter` (`new IfcImporter()`, `wasm = {path: <web-ifc>, absolute: true}`, `process({bytes, raw: false})`) on the `.ifc`, from BubbleBIM Standalone's `node_modules`.
3. What the library reads: `node scripts/fragments-check.mjs <file.frag> [node_modules]` (the items, tree, metadata and CRS, and one element's properties as BubbleBIM Standalone's panel asks for them).
4. What the viewer draws: `node scripts/fragments-render.mjs <out folder> <a.frag|a.glb>… [--modules <node_modules>] [--views iso,top,front,back]` loads each `.frag` as BubbleBIM Standalone's viewer does (`FragmentsModels` with its worker, `load(bytes, {modelId, camera, raw})`, `update(true)` until the tiles are in) and each `.glb` with three's loader, in headless Chrome (playwright-core), and writes one picture per view with the files side by side, and `report.json` (items with geometry, categories, triangles, materials, boxes, `coordinates`). Each file is shown in its own frame (`autoCoordinate` off): the viewer itself moves a second model by the difference of the two files' `coordinates`, which puts That Open's file of a project on Lapin's.

Tests: `FragmentsExchangeTests` (the type, sets and materials related as the official importer relates them; the GlobalIds of the spatial structure, the unique attributes and relation names; a wall with a window written as faces with holes and imported back with its area; the polygon merge and its fallback; a file placed by `coordinates`; glass and the sets on import; the map conversion), `FragmentsTests` (a box as six faces of four corners; a concave profile and a profile with a hole imported whole and open).
