# BIM Open Schema (`.bos`)

[BIM Open Schema](https://github.com/ara3d/bim-open-schema) (Ara 3D, MIT, schema version 0.3) is an open format for BIM data: a zip of Apache Parquet files, one per table. It is easy to query with data tools (DuckDB, pandas, Power BI) and is read by Ara 3D's web viewers. Lapin imports and exports it (decision in `docs/decisions-pending.md`), in the same Exchange section as IFC and Fragments.

## The format, as Lapin reads and writes it

Checked against the reference code (`ara3d/bim-open-data`, `Ara3D.BimOpenSchema.IO/ParquetUtils.cs`: Parquet.Net, Brotli pages, entries stored in the zip) and against its open samples (`duplex.bos`, `schependomlaan.bos`, read with pyarrow).

**Entries.** Each table is an entry `<Table>.parquet`:
- data tables: `Diagnostics`, `Points`, `Strings`, `Descriptors`, `Documents`, `Entities`, `Relations`, `Parameters`, `Numbers`;
- geometry tables: `Materials`, `VertexBuffer`, `IndexBuffer`, `Instances`, `Meshes`, `Transforms`;
- an optional JSON manifest (`BimOpenSchemaVersion`, `GeneratorApplication`, `GeneratorVersion`).

The reference reader takes columns by position; Lapin takes them by name, and by position when a name is missing. Older files keep their parameters in a table per kind (`IntegerParameters`, `StringParameters`, `EntityParameters`, `PointParameters`, `SingleParameters`, the last with inline floats); Lapin reads these too.

**Columns.**
- Every index is an INT32 into the table it names, −1 when absent. `Entities.LocalId` is INT64.
- `Strings` is an optional UTF-8 BYTE_ARRAY column. `Numbers`, `Points` and `Transforms` are FLOAT.
- The material channels and `InstanceFlags` are INT32 tagged as an unsigned 8-bit integer. Flag 1 means hidden; the IFC converter hides spaces.

**Meaning.**
- An entity's category is another entity, named after the IFC class (`IFCWALLSTANDARDCASE`) or, in a Revit export, after the Revit category (`Walls`).
- IFC attributes are parameters named `Ifc:<Attribute>`, grouped by the class. Property and quantity sets are groups, named after the set.
- Revit parameters are named `Rvt:<Scope>:<Name>`.
- Relations go from A to B: an element `ContainedIn` its storey, an opening `Voids` its wall, a window `Fills` its opening, an element `HasMaterial` a material or `HasLayer` a layer (and the layer `HasMaterial`), a part `MemberOf` its whole.
- In a Revit export the level is a parameter: `Rvt:Element:Level`, which points at the level entity.

**Geometry.**
- A mesh is a range of the shared vertex buffer plus local indices; its ranges run from its offsets to the next mesh's.
- Vertices are integers in tenths of a millimetre (divide by 10,000 for metres), Z up, right-handed.
- An instance places a mesh in a material for an entity. Its transform is translation, quaternion and scale: p′ = R(q)(S·p) + T. When the product of the scales is negative the instance mirrors, and its triangles are turned.

## Parquet in Lapin

`engine/src/Platform/Lapin.Mesh/Bos/` has its own Parquet reader and writer, with no dependency:
- `Thrift.cs`: the compact protocol, for the metadata.
- `Parquet.cs`: the reader and the writer, plus Snappy decompression.

**Reading.**
- Flat schemas; BOOLEAN, INT32, INT64, FLOAT, DOUBLE and UTF-8 BYTE_ARRAY columns, required or optional.
- Data pages v1 and v2, and dictionary pages.
- Encodings PLAIN, PLAIN_DICTIONARY, RLE_DICTIONARY, DELTA_BINARY_PACKED, and RLE for booleans.
- Uncompressed, Snappy, GZIP and Brotli pages. Brotli comes from .NET; in a browser build without it, such a file is refused with a message.
- ZSTD, LZ4 and nested columns are refused with a message.

**Writing.**
- One row group, one PLAIN page per column, Brotli (stored uncompressed where Brotli is missing).
- The same logical types as Parquet.Net: INT32 and INT64 signed, UINT_8 for bytes, STRING for text.

**Tests.** `BosTests`:
- pyarrow fixtures: Snappy with dictionaries and pages v1; GZIP with DELTA_BINARY_PACKED and pages v2;
- Lapin's writer read back;
- a Snappy block made by hand;
- a project exported and imported back to a tenth of a millimetre;
- `LAPIN_BOS_SAMPLE=<file.bos>` (with `LAPIN_BOS_REPORT=<out.json>`: the import's report, plus the model again as GLB and BOS) for a real file.

## Import (`project.bosSurvey`, `project.importBos`; File → Import BIM Open Schema…)

`BosView` reads what the file means:
- **class:** taken from the category. Spatial names are mapped directly; a Revit category goes through a table (Walls → IfcWall, Floors → IfcSlab …); anything else becomes a proxy.
- **storey:** the storey it is `ContainedIn`; failing that, the storey reached through what holds it (a space, an assembly, a host); failing that, the Revit level.
- **visible instances:** the hidden ones are left out.
- **storey elevation:** `Ifc:Elevation` or `Rvt:Level:Elevation`. The value is scaled by whatever best fits the lowest point of the storey's elements (metres, millimetres, feet).

`BosImport` brings the file in as `FragImport` brings in a Fragments file, and shares its options and its result:
- **References.** Each element with a body becomes a reference.
- **Shared bodies.** A mesh in one material, used by more than one element, becomes a shared body with instances.
- **Glass.** It keeps a transparent material.
- **Properties.** Parameters become property sets: the `Ifc:` attributes under *Attributes*, together with the type and the materials. `Rvt:` parameters keep their last name, in their group.
- **Storeys.** Matched by name; made when missing.
- **Grouping.** Everything comes in as one group.
- **Recognition and editable.** Recognised into native elements, or made editable, as asked.
- **Ids.** The file's GlobalIds; for a Revit UniqueId, an id derived from it. Importing again therefore updates what came in before.

## Export (File → Export IFC, Fragments or BOS…, `project.exportMesh {format: "bos"}`)

`BosWriter` writes the mesh scene the way Ara 3D's IFC converter writes a file:
- **Entities.** The project, site, building and storeys (`PartOf` relations). Each element under its class's category entity, with its GlobalId, `ContainedIn` its storey; an assembly's parts are `MemberOf` it. Types and materials are entities too (`Entity.Type`, `HasMaterial`).
- **Parameters.** The IFC attributes, the property and quantity sets (quantities with their units) and the user's parameters, as the Fragments export collects them (`FragContext`). An imported reference's *Attributes* go back as `Ifc:` parameters.
- **Geometry.** Each part is welded at a tenth of a millimetre around its middle, which becomes the instance's translation. Identical meshes are stored once: a column type or a window appears once with many instances.
- **Openings.** Every opening (`OpeningVoids`: a wall's bare openings, doors, windows, floor and roof openings) becomes an IFCOPENINGELEMENT entity, as the converter writes them. It `Voids` its host, is `Fills`-ed by its door or window, and is `ContainedIn` the host's storey. Its kind, shape and sizes are parameters (`Ifc:ObjectType`, `Pset_ProvisionForVoid.Shape`, `Qto_OpeningElementBaseQuantities` Width/Height/Depth). Its body is an instance flagged hidden: a viewer leaves it out, a clash tool finds it. The import skips hidden instances, so openings never come in as elements.
- **Left out.** Point clouds are not part of the format.

## Not done

- The `Diagnostics` and `Points` tables are written empty.
- Ara 3D's own viewer has not yet been checked against Lapin's file. pyarrow reads it with the same schema as the reference files.
