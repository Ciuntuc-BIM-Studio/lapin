---
title: Import and export
order: 110
summary: Exchange the model as IFC, bring in DXF drawings, link files, and export 3D models for rendering and AR.
---

Everything that goes in or out is in the **File** menu.

![The File menu](shots/file-menu.webp)

## IFC and Fragments

**Export IFC or Fragments…** writes the model as IFC. Every wall, floor, door and roof is a real IFC element with its layers, its type and its properties, so the file opens as a model in other BIM tools. Doors and windows say how they open: the hand of each leaf and sash, which way it swings, and the panels, the way IFC defines them, so other programs draw the same swings.

The export asks for the schema (**IFC4**, read everywhere, or **IFC4X3**), the detail (parts as elements of their own, or one body per element), the phase, and how the bodies are written. **Geometry** is a **faceted B-rep** (exact, what other programs expect most often) or **face sets**: the same faces in a much smaller file. **Simplify** writes fewer triangles where a body is a fine mesh, such as the ducts and pipes of another program's file, never further from it than the **tolerance** you type (2 mm to start with). Bodies Lapin builds itself (walls, floors, doors) keep their exact faces, and simplifying changes only the shape of a body, never its properties.

![Export IFC](shots/export-ifc.webp)

**Import IFC…** reads another program's IFC. Before anything comes in, Lapin reads the file and shows what it holds: its elements by discipline (architecture, structure, MEP, furniture and equipment, site), each discipline by kind with how many there are, and its storeys with how many elements stand on each. Untick what you do not need, for example MEP and furniture when you work on the structure, or keep only the storeys you work on. **How** brings the elements recognised as Lapin elements, all of them as references, or linked. While the file is read and imported, Lapin shows what it is doing and how far along it is; a large file takes a while.

![Import IFC](shots/import-ifc.webp)

Some files carry 2D besides their model: plan symbols, footprints, hatches and texts drawn for an element, or elements drawn only in 2D (lines, texts, symbols with no body). When a file has them, the dialog counts them under **Drawn in 2D**. Untick **the elements' own 2D** and a plan draws those elements by their cut, as it does Lapin's; untick **elements drawn only in 2D** and they stay out. Both come in unless you untick them.

![Drawn in 2D](shots/import-ifc-2d.webp)

**Clean-up and optimisation** holds the import's rules, the same for any file, kept as your own for the next import. **Keep a repeated body once** (on unless you untick it) keeps a body the file repeats under other names (a bolt, a flange, a support) once, its elements sharing it, nothing lost. The others leave out, when ticked, what another program brought along: **flat bodies** (texts, lines and hatches turned into triangles), **elements further from the rest** than the distance you type (a drawing's texts, a title block), **duplicates** in the same place, and **ports and openings written as objects**. **Simplify meshes** writes fewer triangles within the tolerance you type; **read numbers written as text** turns `DoubleLength:0,01` into 0.01 m and `1.15395 kg` into a mass (a whole number alone, a code, stays text). Under **What to bring**, **Property sets** lists the file's sets with how many elements carry each; the **technical** ones (Navisworks' transform, internal ids, geometry statistics) are left out unless you tick them. The import's message says what each rule did.

![Clean-up and optimisation](shots/import-ifc-rules.webp)

What the file places comes in as one group named after it, which you can open and clear up, or bring in loose by unticking **As one group** (see [Groups and clearing up](groups.md)).

**Everything editable**, a choice of **How**, goes further than recognition: whatever stays a reference becomes a Studio solid of its own triangles, the same element with its class, properties, storey and group, which takes push/pull, fillets and booleans at once (see [Studio](studio.md)). A body of more than 5,000 triangles, such as a finely meshed pump or fitting, stays a reference, and the import's message says how many and why. **Import Fragments…** has the same **How**: a Fragments file holds only triangles, yet its walls (with their doors and windows), slabs and columns are recognised from them as Lapin elements, the rest kept as references or, with **Everything editable**, made Studio solids. An IFC file's elements that recognition could not read from the file (a wall with its openings cut into its body) are tried from their triangles too. **Import mesh** asks **As: editable**. References brought in before are converted with Modify → **Convert to native** (NV): recognised where they can be, the rest made Studio solids, a selected group being its members. A reference brought in before is made editable one at a time with Studio → **Make Studio solid**, which keeps it in its group and on its storey.

Lapin recognises the walls, floors, columns, beams, footings, doors and windows and turns them into Lapin elements you can edit. Whatever it cannot recognise comes in as a reference, drawn but not editable. Doors and windows come in opening the way the file says: their hand, the side they open to and, for windows, how each panel opens.

Finishes another program exported as walls of their own (a 2.5 cm render on a facade, a plaster inside) become a layer of the wall they cover, when they lie against its face, as high as it and along it: the wall gets a type with that layer (*Masonry 350 + Lime render*), its own layers where they were, and the import's message says how many. A finish that stands off its wall, or covers only part of it, stays a thin wall. A column the file gives as a body cut at its top by a slope (under a sloping beam or roof) comes in as a column of its section, up to its highest point.

**Map imported types** (Manage) lists the types an import made (wall, floor, door, window, column, beam and footing types), each with its file and how many elements it has. Choose one of the project's own types of the same kind under **Becomes** and press **Map**: its elements take the project's type, a wall keeping its core where it was, and the imported type goes. With **Remember** ticked, a later import of a type of that kind and name takes the project's at once, and its message says so. The remembered choices are listed in the dialog, each with **Forget**.

![Map imported types](shots/map-types.webp)

The file's spaces (from Revit, ArchiCAD and others) come in as Lapin rooms, each on its own level, with their outline, number, name, height, function, department and floor finish: see [Rooms](rooms.md#rooms-from-an-ifc-file). Everything an imported element had in the file (its property sets and quantities) is kept and shown under **IFC properties** in its properties. A reference leaves **Export IFC…** as it came in: with the same GlobalId, class and attributes (description, tag, predefined type), its property sets and quantities with every value typed as the file had it, and its type, material, ports, connections and system, each with its GlobalId. Bring only the MEP of a file and export it, and the new file carries it whole.

**Import Fragments…** opens the same panel for a That Open `.frag` file: its elements by discipline and kind, its storeys, and **As one group**. A file exported from this very project brings nothing new when imported back into it: its elements are already here.

![Import Fragments](shots/import-frag.webp)

**Import Fragments…** brings a `.frag` file in: every item with geometry becomes a reference drawn from its body, in the class the file names, on the storey the file puts it (matched by name to yours, made when missing), with the file's attributes under **IFC properties**. Items with the same body (the same shape, placed in many places) share it. Importing the same file again updates what it brought before. What is recognised from its triangles is up to **How**, as above. Its property and quantity sets, its type and its materials come in with the attributes; glass stays see-through; a file made by That Open's own converter lands where its IFC would, and its faces with holes (a wall around its window) keep them.

**Export IFC or Fragments…** writes the model as IFC or as Fragments, chosen as an import is: the elements by discipline and kind, the storeys, the phase, and only what the 3D view shows. IFC goes out in IFC4 or IFC4X3, a layered element's parts under it or one body each; Fragments from the project base point, the internal origin or the survey point. A Fragments file carries what an IFC viewer shows: each element's type, its quantities, your parameters, its materials and the map coordinates, as That Open's converter writes them.

![Export IFC or Fragments](shots/export-exchange.webp)

## Optimize an IFC file

**Optimize IFC…** (in File, or on the start screen: no project needs to be open) makes a lighter copy of one or more IFC files without changing what they say. The copy keeps every GlobalId, name, property set, quantity, type, material and relationship exactly as the file wrote them, and its header too; nothing of Lapin's is added. Only the geometry changes: meshes (face sets, faceted B-reps of plain faces) are cleaned (duplicate vertices welded, degenerate and repeated triangles and unused coordinates dropped) and simplified, never further from the original than the **Precision** (Fast 5 mm, Balanced 1 mm, Precise 0.1 mm, or your own), measured both ways; identical bodies are written once and shared by their products; and what nothing references (unused points, styles, empty representations) goes. Extrusions, booleans and other exact bodies stay as they are. Rounding the coordinates, B-reps written as face sets (IFC4 and IFC4X3 only) and a zipped `.ifczip` are there when you want them.

![Optimize IFC](shots/optimize-ifc.webp)

The copy is written next to the original, or in a folder you choose, under the original's name with an **Ending** (`.optimized` to start with): `house.ifc` gives `house.optimized.ifc`. The original is never written, and when the copy is already there Lapin asks before replacing it. For each file the report gives the size, entities and triangles before and after, the bodies shared, the orphans removed, the largest deviation measured and the time taken; **Open folder** shows the copy and **Open in Lapin** opens it in another window. In the browser the copy is downloaded. A file already in the project keeps its way back: **Optimize the source file…** on an imported IFC's group (in its properties) or on a link (Manage → Links) does the same to the file it came from, while that file is still on disk.

![The report](shots/optimize-ifc-report.webp)

## DXF and DWG

**Import DXF…** brings a CAD drawing into the view in front, with its layers, hatches, texts and dimensions. It comes in as drafting you can snap to and edit. To use a DWG, first save it as DXF from your CAD program (AutoCAD, BricsCAD or DraftSight: **Save As → DXF**). PDF import is not available yet. Into a plan, **Placed: by the map's coordinates** lays a drawing in real coordinates (a cadastral plan in Stereo 70) where the map puts it (see [Site](site.md)).

## Links

**Link IFC…** keeps another file linked instead of importing it. It is read-only and reloaded when it changes. This works for a DXF too. **Manage → Links** shows, reloads, converts or removes the links.

## 3D models

**Export 3D (glTF, USD, DAE)…** writes the model as surfaces for rendering, visualisation and augmented reality (AR):

| Format | For |
|---|---|
| GLB, glTF | Blender, three.js, web viewers. Your materials' own images are included. |
| USDZ | Apple's AR Quick Look, Reality Composer, Omniverse. Images included. |
| USDA | OpenUSD as text, one file. |
| DAE | COLLADA, for SketchUp and older tools. |
| OBJ + MTL | A .zip for Twinmotion, Lumion, 3ds Max and any renderer. Images included. |

**Import mesh (OBJ, STL, glTF)…** brings in a mesh from another 3D program (OBJ, STL, glTF or GLB) as references on the active level. glTF says its unit and which way is up; for OBJ and STL Lapin asks. Studio's **Make Studio solid** makes a reference editable (see [Studio](studio.md)).

![Export 3D](shots/export-3d.webp)

You choose what to export, which phase, and the origin. **What** is **everything**, or the model **as the view shows it**: the 3D view in front, without its hidden elements and categories and the storeys it does not show, with the layers it shows (without finishes, or the cores only). When that view is cut (the cut above the storeys shown, a section shown in 3D, the section box), **Cut** cuts the export there too and closes each cut in its materials, so the file looks like the screen. A 3D view that leaves anything out is exported as it shows it unless you choose otherwise. Every element keeps its IFC class, type and GlobalId. To exchange the model itself rather than its surfaces, use **Export IFC**.
