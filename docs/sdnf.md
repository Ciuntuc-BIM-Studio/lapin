# SDNF (`.sdnf`): the Steel Detailing Neutral File

Decision 474, a tester's report #141: *"SDNF is about the only format that allows round-tripping through supported applications."* Lapin writes SDNF 3.0 and reads 3.0 and 2.0, in the steel module (a product without it offers neither). Tekla Structures, Advance Steel, SDS2, ProStructures, AVEVA E3D, Smart 3D/FrameWorks, RISA, SCIA and Dlubal read and write it.

## The specification

Intergraph defined SDNF for exchanging steel structures between analysis and design programs and steel detailing programs. Its public descriptions, which Lapin follows:

- **Version 3.0, every record** — AVEVA E3D Structural Design User Guide, *SDNF – Structural Steel Detailing Neutral File Format* (https://help.aveva.com/AVEVA_Everything3D/3.1/SDUVPDMS/OSUG3.11.05.html): the packets' record tables (00, 10, 20, 22, 30, 40, 50, 60) and their interpretation: cardinal point line, cutbacks, eccentricities, orientation vector and rotation, plate loops.
- **Version 2.0** — CATIA V5 Structure Design, *Exporting SDNF* (http://catiadoc.free.fr/online/sr1ug_C2/sr1ugat0801.htm): the general rules (ASCII, 132 characters, blank-delimited fields, strings in double quotes, `#` comments, fixed packet headers; field formats FP, I, Cnn) and version 2's packets 00, 10 and 20.
- **The packets as programs use them** — Tekla's *SDNF* page (https://support.tekla.com/article/sdnf), and SDS2's *Export Model – SDNF* (version 3.0 adds packet 22 and the version line).

**The file.** Packets, each a fixed header record (`Packet 10`; `Packet10` is read too) and free-format records:

| Packet | What | Lapin |
|---|---|---|
| 00 | Title: version (3.0 only), engineering firm, client, structure, project, date and time, revision and issue code (the program that wrote it), design code, the load records | written and read |
| 10 | Linear members: the units and their count, then ten records each (six in 2.0) | written and read |
| 20 | Plates: linear and thickness units and their count, then a plate's records | written and read |
| 22 | Holes | read past, counted in a note |
| 30, 40 | Member loads; connection details (text) | read past, noted |
| 50 | Grids: sets of lines (label, start, end) | written and read |
| 60 | Curved members | read past, noted |

**A linear member (packet 10).**
1. Member number, cardinal point, status, class, type, piece mark, revision (`4I, 2C24, I`).
2. Section, grade, rotation, mirror X, mirror Y (`2C24, FP, 2I`).
3. Orientation vector; start; end; start and end cutbacks (`3FP, 3FP, 3FP, 2FP`).
4. Cross-section X and Y offsets (`2FP`).
5. Start and end X, Y, Z offsets (`6FP`): from the member's end to its node.
6. Releases: Tx Ty Tz Rx Ry Rz at each end (`12I`, 1 released).
7. Fabricator, prebuy mark, subtype, created and modified dates and times, updated, approval status.
8. The connections at each end, configuration, assemblies (`12I`).
9. Material, coatings, fireproofing (`2I, FP, 2I, 2FP`).
10. Fabricator's note, five user attributes (`6I`).

A file that writes more records per member (FrameWorks writes eleven) is read by the count: the packet's records divided by its members.

**A plate (packet 20, 3.0).** Number, connect point (0 centred, 1 the positive face, −1 the negative), status, class, type, revision; piece mark, grade, thickness, vertices per face, offset, periphery (0 straight edges, 1 straight and curved, 2 a circle); the top face's loop then the bottom's, each vertex `x y z flag` (1 a straight segment follows, 2 a curved one, 0 on the closing vertex, the first again); a circle is its centre on each face, the flag its radius; then four records like a member's 7–10. Version 2.0: piece mark, grade, thickness, vertex count, and one loop of `x y z`.

**Meaning.**
- **Cardinal point**: where the member's line runs through its section's box, 1–9 in rows from the bottom, left to right (5 the middle; 2 bottom centre, 8 top centre); 10 the centroid.
- **Orientation vector**: the section's up (its y, a beam's web) before the **rotation**, a right-handed turn about the member's axis looking from start to end.
- **Mirror**: Lapin reads *mirror Y-axis* as the section mirrored about its Y axis (x → −x, an angle or channel facing the other way) and *mirror X-axis* as mirrored about its X axis (the same turned half round). The descriptions do not say more.
- **Coordinates**: the start and end of the cardinal point's line, the real steel (AVEVA's reading); the cutbacks shorten it; the 3D offsets go from each end to its node.

## Export (File → Export IFC, Fragments or BOS… → **SDNF**; `steel.sdnfExport`)

`engine/src/Modules/Steel/Lapin.Steel/Sdnf/`: `SdnfFile.cs` (the records, the writer and the reader), `SdnfProfiles.cs`, `SdnfExport.cs`, `SdnfImport.cs`. The query takes the dialog's storeys, kinds and what the 3D view hides, and returns `{name, data, report}`.

- **Members**: the structure's columns and beams, of any section and material (SDNF allows any), and the steel module's members. Each line is the axis Lapin builds the body on (`MemberAxes`): a column from its foot up its height, centred (cardinal point 5); a beam along its clear span between the columns' faces, at its top (8), its own ends — the columns' centres — as the offsets to its nodes; a member through its insertion point (centre 5, top 8, bottom 2). The section's up is the orientation vector, the rotation 0; a flipped beam or member is mirrored about its Y axis. A steel end's extension, cut, mitre or fit is the cutback (the body's stations off the axis's ends). The type: `Column`, `Beam`, or the member's predefined type (`Brace`, `Rafter`, `Purlin`…). The grade: the strength class the material's name says (S355), else the material's name. Curved beams are left out (listed in the report).
- **Sections**: a library profile by its name (IPE 300, HEA 240, L 100x100x10, a project's AISC or own profile); a section with no name by its kind and sizes in millimetres: `I300x150x7.1x10.7`, `RHS200x100x8`, `CHS114.3x5`, `C200x65x2x20`, `300x400` (a rectangle, width × depth), `D300` (a circle). `×` and `Σ` are written as `x` and `SIGMA` (the file is ASCII).
- **Plates**: the plates of the steel connections (end, base, anchor plates) of the members written: the largest face and the face through the thickness, as two loops; grade from the part (S235).
- **Grids**: each IfcGrid a set, its axes at ±0.00.
- **Units**: millimetres, to a thousandth (fewer decimals only when a record would pass 132 characters); the orientation vector to nine places.
- **Ids**: a member's number is the one it came with from an SDNF file (`sdnf.id`), else one from its GlobalId (FNV-1a, at most eight digits, the next free one on a clash). A comment after the title maps each number to its element: `# Lapin member 37239609 00tMh1vYTFOfUQA5$zsorE`, `# Lapin plate n node:<GlobalId>:<part>`, `# Lapin grid n <GlobalId>`. Comments are allowed anywhere; other programs skip them.
- **Releases**: none (Lapin has no end releases yet): every end is written fixed. Status, class, revision and the records 7–10 are written as zeros (the dates are the export's).

## Import (File → Import SDNF…; `steel.importSdnf {name, data | text | path}`)

One undo step; the report counts what was made, updated and made again, the plates and grids, the profiles no library has, and notes.

- **Class**: a member called a column that stands upright (and is not a mirrored handed section) becomes a column; one called a beam (girder, joist) that lies level with its section upright becomes a beam; anything else a steel member, its predefined type from the file's type (BRACE, PURLIN, RAFTER, POST, STRUT…). The section's box centre is found from the cardinal point, the frame from the orientation vector, rotation and mirrors; a column's foot, rotation and own height, a beam's ends, top and top offset, a member's ends, rotation and insertion follow. Storeys: a column's and a member's the highest whose level is at or below its foot, a beam's the one under its top; an element brought in before stays on its own.
- **Sections** (`SdnfProfiles.Resolve`): the project's copies of library profiles by name, then Lapin's catalogue and built-in EN library (spaces, `x` and `×` alike), then the generic names above. Any other name (an AISC `W12X50` when the project has no AISC table) is kept as the type's name on a 100 mm square placeholder, listed in the report, until its type is given a profile.
- **Types and materials**: the project's type of that class with that profile and material, else a new one; the grade a material of that name, or one whose strength class is that grade.
- **Ids**: Lapin's comments give each element's GlobalId; else a member numbered as one the project holds — brought in before from the same source (`sdnf: {id, source}`, the source the title's firm, client, structure and project), or exported by Lapin (its number from its GlobalId) — is that element; else a GlobalId derived from the source and number. Importing a file again therefore updates what it brought: positions, sections, storeys; an element keeps its other attributes (its steel ends). One whose class no longer fits (a column now sloping) is made again under its id as what it now is.
- **Beams from Lapin**: their nodes' offsets put their ends back on the columns' centres; Lapin stops them at the faces again.
- **Cutbacks**: on a new steel element, its ends' extensions (negative).
- **Plates**: plates with their bodies (`IfcPlate`), a prism between the two loops (a version 2 plate from its loop, its thickness and connect point; a circle by the chord rule; curved segments straight between their vertices). The plates of a Lapin connection still in the project are left to the connection, which builds them.
- **Grids**: an IfcGrid per set, updated by its id.
- **Left out**, in notes: holes, loads, connection details, curved members, end releases.

## Tests

`SdnfTests`: the file's packet layout (the title's ten records, ten records a member of 7, 5, 11, 2, 6, 12, 9, 12, 7 and 6 fields, the plates' and grids' headers, 132 characters, ASCII); a frame exported and imported into an empty project (classes, profiles, ends, frames, insertion and bodies to 0.1 mm, the beam back on its nodes, the grid, one undo step); a re-import into the same project updating every element and nothing added, a sloping column made again as a member; another program's 3.0 file in feet and inches (a W12X50 kept as a placeholder, cardinal point 1, a plate) imported twice, and a 2.0 file; a hall's plates out and back.

## Not done

- End releases (Lapin has none), holes in plates, curved members (packet 60), loads.
- Profiles named by another program's table (W12X50) resolve only through the project's own profiles: a mapping table of names is for later.
