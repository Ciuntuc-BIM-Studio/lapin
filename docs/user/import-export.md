---
title: Import and export
order: 110
summary: Exchange the model as IFC, bring in DXF drawings, link files, and export 3D models for rendering and AR.
---

Everything that goes in or out is in the **File** menu.

![The File menu](shots/file-menu.webp)

## IFC and Fragments

**Export IFC, Fragments or BOS…** writes the model as IFC. Every wall, floor, door and roof is a real IFC element with its layers, its type and its properties, so the file opens as a model in other BIM tools. Doors and windows say how they open: the hand of each leaf and sash, which way it swings, and the panels, the way IFC defines them, so other programs draw the same swings. Each door and window also carries its material per part (Frame, Sash, Glazing, Panel…), and every material its rendering style under its own name (see [Materials](materials.md#materials-for-rendering)); glass in an imported IFC stays see-through.

The export asks for the schema (**IFC4**, read everywhere, or **IFC4X3**), the detail (parts as elements of their own, or one body per element), the phase, and how the bodies are written. **Geometry** is a **faceted B-rep** (exact, what other programs expect most often) or **face sets**: the same faces in a much smaller file. **Simplify** writes fewer triangles where a body is a fine mesh, such as the ducts and pipes of another program's file, never further from it than the **tolerance** you type (2 mm to start with). Bodies Lapin builds itself (walls, floors, doors) keep their exact faces, and simplifying changes only the shape of a body, never its properties.

![Export IFC](shots/export-ifc.webp)

**Exact surfaces.** Tick **Exact surfaces: Studio solids as advanced B-rep on their exact surfaces** and the Studio solids Lapin knows exactly are written as *IfcAdvancedBrep* on their true surfaces instead of facets: a cylinder on a cylindrical surface, a sphere on a spherical one, a revolution on planes, cylinders, cones, spheres and tori, a smooth loft on the B-spline surface it is made of, a closed SubD on bicubic B-spline patches (its faces round the star points as its facets). Primitives, extrusions of lines and arcs (not tapered, not up to a face), revolutions, push/pull prisms, smooth lofts of three profiles or more and closed SubDs are exact; a solid with steps (fillets, shells, push/pull on it), one cut by another element, a sweep, a Follow Me or a body made from a mesh stays as its facets. Lapin checks each exact body against the one it draws, so nothing is written that the model is not. Unticked (the default), every body is written as before; IFC4 and IFC4X3 both have advanced B-reps.

**STEP** in the same dialog writes the bodies as solids for CAD programs (FreeCAD, Inventor, SolidWorks, Fusion): an AP214 file in millimetres, each element a part of its own named after it, in its colour, on its exact surfaces where Lapin knows them (as above) and on the planes of its facets otherwise. The kinds, storeys and what the 3D view hides are chosen as for IFC.

Beams, columns and steel members are written by their parametric profile, named, with its mechanical values ([Profile libraries](profiles.md)).

**Import IFC…** reads another program's IFC. Before anything comes in, Lapin reads the file and shows what it holds: its elements by discipline (architecture, structure, MEP, furniture and equipment, site), each discipline by kind with how many there are, and its storeys with how many elements stand on each. Untick what you do not need, for example MEP and furniture when you work on the structure, or keep only the storeys you work on. **How** brings the elements recognised as Lapin elements, all of them as references, or linked. While the file is read and imported, Lapin shows what it is doing and how far along it is; a large file takes a while.

![Import IFC](shots/import-ifc.webp)

Some files carry 2D besides their model: plan symbols, footprints, hatches and texts drawn for an element, or elements drawn only in 2D (lines, texts, symbols with no body). When a file has them, the dialog counts them under **Drawn in 2D**. Untick **the elements' own 2D** and a plan draws those elements by their cut, as it does Lapin's; untick **elements drawn only in 2D** and they stay out. Both come in unless you untick them.

![Drawn in 2D](shots/import-ifc-2d.webp)

**Clean-up and optimisation** holds the import's rules, the same for any file, kept as your own for the next import. **Keep a repeated body once** (on unless you untick it) keeps a body the file repeats under other names (a bolt, a flange, a support) once, its elements sharing it, nothing lost. The others leave out, when ticked, what another program brought along: **flat bodies** (texts, lines and hatches turned into triangles), **elements further from the rest** than the distance you type (a drawing's texts, a title block), **duplicates** in the same place, and **ports and openings written as objects**. **Simplify meshes** writes fewer triangles within the tolerance you type; **read numbers written as text** turns `DoubleLength:0,01` into 0.01 m and `1.15395 kg` into a mass (a whole number alone, a code, stays text). Under **What to bring**, **Property sets** lists the file's sets with how many elements carry each; the **technical** ones (Navisworks' transform, internal ids, geometry statistics) are left out unless you tick them. The import's message says what each rule did.

![Clean-up and optimisation](shots/import-ifc-rules.webp)

What the file places comes in as one group named after it, which you can open and clear up, or bring in loose by unticking **As one group** (see [Groups and clearing up](groups.md)).

**Storeys.** An import's storeys are the file's: they replace your project's. Each storey of the file comes in with its name, its GlobalId and its level as the file gives it, read in the file's unit (millimetres or metres). A storey of your project at the level of one of the file's (within 5 cm) is merged into it: its elements stay where they are, and its views, its plan and what is drafted on it go to the file's storey, which keeps the file's name. A storey of yours at none of the file's levels is removed when it holds nothing (no element, nothing drafted on its plan), its views going to the file's nearest storey; one that holds something stays, and the import's message says which. A new project's Foundations, Ground floor and First floor thus give way to the file's own. All of it is one undo step with the import. A link keeps its storeys to itself and changes none of yours. When the import is done, the 3D view frames the model, unless you moved the camera while it came in.

**Everything editable**, a choice of **How**, goes further than recognition: whatever stays a reference becomes a Studio solid of its own triangles, the same element with its class, properties, storey and group, which takes push/pull, fillets and booleans at once (see [Studio](studio.md)). A body of more than 5,000 triangles, such as a finely meshed pump or fitting, stays a reference, and the import's message says how many and why. **Import Fragments…** has the same **How**: a Fragments file holds only triangles, yet its walls (with their doors and windows), slabs and columns are recognised from them as Lapin elements, the rest kept as references or, with **Everything editable**, made Studio solids. An IFC file's elements that recognition could not read from the file (a wall with its openings cut into its body) are tried from their triangles too. **Import mesh** asks **As: editable**. References brought in before are converted with Modify → **Convert to native** (NV): recognised where they can be, the rest made Studio solids, a selected group being its members. A reference brought in before is made editable one at a time with Studio → **Make Studio solid**, which keeps it in its group and on its storey.

Lapin recognises the walls, floors, columns, beams, footings, doors and windows and turns them into Lapin elements you can edit. Whatever it cannot recognise comes in as a reference, drawn as the file gives it: its shape cannot be edited, but it moves, turns, mirrors, is copied and arrayed like any element. An element becomes a Lapin element only when it is the file's own: Lapin measures each one it recognises against the body the file gives it, and one that differs by more than 1 mm or 0.5 % of its volume (a column whose top the file slopes, a wall end the file notches, which a Lapin element would square) comes in as the reference it was; the import's message says how many, and why. Doors and windows come in opening the way the file says: their hand, the side they open to and, for windows, how each panel opens. A flat roof from another program comes in as a Lapin roof when its IfcRoof is made of two or more slabs stacked one on the other, or of one body with a layer set: the top slab (or layer) is its covering, with its material and thickness, the ones under it its deck, of the thickest one's material; the outline is the deck's, with no overhang, at the deck's underside. A sloped roof, or a roof of one slab without layers, stays as it came. Recognised elements get only what the file has: no lintels, window boards, thresholds or reveals of Lapin's own, and each opening the size the file gives it; switch them on in the element's types if you want them.

Finishes another program exported as walls of their own (a 2.5 cm render on a facade, a plaster inside) become a layer of the wall they cover, when they lie against its face, as high as it and along it: the wall gets a type with that layer (*Masonry 350 + Lime render*), its own layers where they were, and the import's message says how many. A finish that stands off its wall, or covers only part of it, stays a thin wall. A column the file gives as a body cut at its top by a slope (under a sloping beam or roof) comes in as a column of its section, up to its highest point. A wall another program modelled as separate solids, one per layer (SketchUp's usual way), stays as those solids, references or Studio solids; select them and choose **Architecture → Wall from solids** (<kbd>WFS</kbd>) to make one layered wall of them, its openings and doors and windows with it (see [A layered wall from solids](walls.md#a-layered-wall-from-solids)). The import never does it by itself.

**Map imported types** (Manage) lists the types an import made (wall, floor, door, window, column, beam and footing types), each with its file and how many elements it has. Choose one of the project's own types of the same kind under **Becomes** and press **Map**: its elements take the project's type, a wall keeping its core where it was, and the imported type goes. With **Remember** ticked, a later import of a type of that kind and name takes the project's at once, and its message says so. The remembered choices are listed in the dialog, each with **Forget**.

![Map imported types](shots/map-types.webp)

The file's spaces (from Revit, ArchiCAD and others) come in as Lapin rooms, each on its own level, with their outline, number, name, height, function, department and floor finish: see [Rooms](rooms.md#rooms-from-an-ifc-file). Everything an imported element had in the file (its property sets and quantities) is kept and shown under **IFC properties** in its properties. A reference leaves **Export IFC…** as it came in: with the same GlobalId, class and attributes (description, tag, predefined type), its property sets and quantities with every value typed as the file had it, and its type, material, ports, connections and system, each with its GlobalId. Bring only the MEP of a file and export it, and the new file carries it whole.

**Import Fragments…** opens the same panel for a That Open `.frag` file: its elements by discipline and kind, its storeys, and **As one group**. A file exported from this very project brings nothing new when imported back into it: its elements are already here.

![Import Fragments](shots/import-frag.webp)

**Import Fragments…** brings a `.frag` file in: every item with geometry becomes a reference drawn from its body, in the class the file names, on the storey the file puts it (the file's storeys replace yours: see **Storeys** above), with the file's attributes under **IFC properties**. Items with the same body (the same shape, placed in many places) share it. Importing the same file again updates what it brought before. What is recognised from its triangles is up to **How**, as above. Its property and quantity sets, its type and its materials come in with the attributes; glass stays see-through; a file made by That Open's own converter lands where its IFC would, and its faces with holes (a wall around its window) keep them.

**Export IFC, Fragments or BOS…** writes the model as IFC, as Fragments or as BIM Open Schema, chosen as an import is: the elements by discipline and kind, the storeys, the phase, and only what the 3D view shows. IFC goes out in IFC4 or IFC4X3, a layered element's parts under it or one body each; Fragments from the project base point, the internal origin or the survey point. A Fragments file carries what an IFC viewer shows: each element's type, its quantities, your parameters, its materials and the map coordinates, as That Open's converter writes them. A project tied to the map (a survey point, or the base point's latitude and longitude in **Manage → Location**) goes out georeferenced: IFC writes IfcMapConversion and IfcProjectedCRS (EPSG:3844…) and the site's latitude and longitude, so a GIS program places the model where it is (see [Real coordinates](site.md#real-coordinates)).

Your parameters keep their IFC types in any property set, a standard one or your own: yes or no as *IfcBoolean*, a length as *IfcLengthMeasure* in metres, an area as *IfcAreaMeasure*, an angle as *IfcPlaneAngleMeasure* in radians (the file's angle unit; Lapin shows degrees), a number as *IfcReal*, a text as a label or text. A type's parameters go on the type, which IFC readers pass on to its elements; a type's instance defaults go on each element instead (see [Walls](walls.md)). An element with phases gets its *Status* in its common set (*Pset_WallCommon* and the others): NEW, EXISTING (created in an existing phase, or before the phase exported), DEMOLISH or TEMPORARY (built and struck in one phase); a *Status* you set yourself wins.

Some programs read only the element and not its type (CYPECAD MEP takes a wall without *IsExternal* on the wall itself for part of the outer envelope). For them, tick **Type properties: Also write type properties on each occurrence** in **Export IFC, Fragments or BOS…**: every property of a type (*Pset_WallCommon.IsExternal*, your own sets, *Lapin_Parameters*) is then written on each of its elements too, in the set of the same name, and a value the element has of its own wins over its type's. The type keeps its sets as before. Unticked (the default), the file is as it always was.

**What an unticked yes or no writes.** A yes-or-no parameter added in the properties or in a type starts unticked, and unticked is a value: the export writes it as *IfcBoolean* `.F.` (false), ticked as `.T.` (true). It is the same for a type and for an element: a type's parameter is written `.F.` on the type (and on its elements too when **Also write type properties on each occurrence** is ticked), an element's on the element, and a type's instance default on each element. Only a parameter with no value at all writes nothing, such as one a script set without a value or a formula that gives none. To leave *IsExternal* unsaid, remove the parameter (its ×) rather than untick it.

With **Parts** set to **grouped under their element**, a wall whose layers are cut by a lintel, a zone or a ring beam goes out as a wall over its parts (*IfcBuildingElementPart*, *IfcBeam* LINTEL), and the wall keeps a body of its own as well, made of its parts' faces, so programs that colour or measure the wall itself find it whole. Its quantities are written once, and Lapin reads such a file back without anything twice: its walls are rebuilt, or, as references, taken from their parts.

A reference keeps the openings its file cut into it, with the doors and windows filling them: **Export IFC, Fragments or BOS…** writes them back with the file's GlobalIds, and a reference moved, turned, mirrored or copied takes its doors and windows with it (a copy gets openings of its own, filled by copies of them). Deleting a reference deletes the doors and windows in it, as with a Lapin wall; a door deleted on its own, or moved away, leaves the opening empty.

**Spaces.** Some programs fail on the extruded profile an IfcSpace is written with (CYPECAD MEP is one). Tick **Spaces: export as faceted B-rep (instead of SweptSolid)** in **Export IFC, Fragments or BOS…** and each space is written as a faceted B-rep of its own geometry instead. Unticked (the default), the file is as it always was.

**Spaces.** Some programs fail on the extruded profile an IfcSpace is written with (CYPECAD MEP is one). Tick **Spaces: export as faceted B-rep (instead of SweptSolid)** in **Export IFC, Fragments or BOS…** and each space is written as a faceted B-rep of its own geometry instead. Unticked (the default), the file is as it always was.

On the desktop, **Bonsai drawing styles** (in **Export IFC, Fragments or BOS…**) writes beside the IFC a stylesheet (`name.css`) and its hatches (`name-patterns.svg`) for Bonsai (BlenderBIM), whose drawings are styled by CSS: each material's cut hatch, fill and elevation hatch, the cut and seen lines of your object styles and the text of your drafting standards. The IFC names both files, so a drawing you create in Bonsai from it takes them.

![Export IFC, Fragments or BOS](shots/export-exchange.webp)

### BIM Open Schema

[BIM Open Schema](https://github.com/ara3d/bim-open-schema) (`.bos`) is an open format by Ara 3D: a zip of Apache Parquet tables (entities, their parameters and relations, and the triangles placed by instances), easy to query with data tools such as DuckDB, pandas or Power BI, and read by Ara 3D's web viewers.

**Import BIM Open Schema…** opens the same panel as an IFC or Fragments import: the elements by discipline and kind, the storeys, **How** and **As one group**. Every element with visible geometry becomes a reference drawn from its body, in the IFC class its category names (a Revit category such as *Walls* or *Doors* is read as its IFC class), on its storey (the one it is contained in, or for a file from Revit its level), the file's storeys replacing yours (see **Storeys** above). Its parameters come in as property sets (its IFC attributes under **Attributes**, its type and materials with them); elements that share a shape share one body; glass stays see-through. Spaces and whatever else the file marks hidden are left out. Importing the same file again updates what it brought before.

![Import BIM Open Schema](shots/import-bos.webp)

![The imported house in 3D](shots/import-bos-3d.webp)

**Export IFC, Fragments or BOS…** with **BIM Open Schema** ticked writes the model as Ara 3D's IFC converter writes a file: the project, site, building and storeys, each element under its IFC class with its GlobalId, type, materials, IFC attributes, property and quantity sets and your parameters, contained in its storey; its triangles in tenths of a millimetre, a shape used many times (a column type, a window) stored once. Openings go out as the converter writes them: an opening element for each door, window and bare opening, cutting its wall and filled by its door or window, with its sizes. Its body is hidden in viewers but kept for clash tools. Point clouds are not part of the format and stay out.

### SDNF (steel detailing)

SDNF, the Steel Detailing Neutral File, is the text format steel detailing programs exchange a structure in (Tekla Structures, Advance Steel, SDS2, ProStructures, E3D): each member a line with its profile, its grade, the point of its section the line runs through and its turn; plates; grids. Lapin writes version 3.0 and reads 3.0 and 2.0, in editions with the steel module.

**Export IFC, Fragments or BOS…** with **SDNF** ticked writes the columns and beams (of any material) and the steel members of the storeys and kinds chosen, the plates of their steel connections and the grids, in millimetres. Each member keeps its profile's name (IPE 300, HEA 240, a profile of your own library), its grade and a number tied to its id, so the file can come back. Curved beams go as curved members, and the holes of plates and members as holes, the connections' bolt and anchor holes among them.

![Export SDNF](shots/export-sdnf.webp)

**Import SDNF…** brings a file in as Lapin's own elements, in one step you can undo: an upright column as a column, a level beam as a beam, anything else (a brace, a rafter, a sloping member) as a steel member, each on its storey, its profile taken from your project's profiles and Lapin's library; its plates and grids come in too. A profile no library has (W12X50, when you have no AISC table) keeps its name on a placeholder section and is listed, until you give its type a profile. Importing a file Lapin wrote, or the same file again after the detailer changed it, updates the elements it came from instead of adding new ones. A level curved member comes in as a curved beam (one arching in another plane is left out and said); holes are cut in the plates and members they go through, and replaced, not doubled, when the file comes again. SDNF carries no bolts, so only their holes come; end releases and loads are not brought in.

![The hall read back from SDNF](shots/import-sdnf-3d.webp)

While a file comes in, a window shows what Lapin is doing (reading the file, bringing the members in, the plates, the holes, building the model), how far it is and the time so far; **Cancel** stops it while it reads the file and makes the elements, and nothing of the file is kept. A frame of ten thousand members takes well under a minute. When it ends, the **SDNF import** window is its protocol: what came in, each packet of the file with how many items it holds and how many were imported, what was left out and why (a member shorter than a millimetre, one in the file twice, a curved member Lapin cannot make, a recess, a hole naming no plate), each with its number and its line in the file, what was changed, the profiles no library has, the notes and the time. ![The SDNF import's protocol](shots/import-sdnf-report.webp)

**Copy** puts the protocol on the clipboard as text and **Save…** writes it to a file, to send to whoever made the SDNF. A file Lapin cannot read is not imported at all: the window says why and where, for example *Line 1234, packet 10, member 57 of 3000: its start's x is „12,5”, not a number*.

## JG Structures (structural analysis)

**Export JG Structures (JSON)…** writes the project's analytical model in the exchange file of JG Structures, a structural analysis program: a JSON file after its contract (version 1.0.0). Lapin derives the model from what you drew. Columns, beams and steel members become frames on their axes; load-bearing walls (Pset_WallCommon.LoadBearing on the wall or its type; a wall that says nothing when it is of masonry or concrete with a core of 10 cm or more) and floors become shells on the middle of their core layer, finishes left out. They share their nodes: a beam that carries a floor lies in the floor's mid-plane, column and wall ends meet the beam or floor at their head and foot, a floor's corners move onto the columns and walls under them, and a floor is split along the beams that cross it. The feet of columns and walls on a footing are fixed; a steel column base is pinned or fixed as its connection says.

Choose the storeys, the kinds of members and the units: the contract's centimetres and kilogram-force, or SI (metres and kilonewtons). Materials take their values from their data (Manage → Materials, Pset_MaterialMechanical in pascals) or from the strength class in their name, C25/30 or S355, by the Eurocodes; a section's area, inertias, moduli and torsion constant are worked out from its geometry. What Lapin does not know stays empty (null) in the file, never zero. Before you export, the report counts what goes in, checks the file against the contract's rules and lists what an analysis still needs: a material without a class, a column that stands on nothing.

Where members do not meet on their axes (a purlin on a rafter, a rail on a column, a brace on its gusset plate, rafters on a wall plate), Lapin puts the node on the supporting member and keeps the distance as the member's end offset, so the secondary steel of a hall is connected too. Walls keep their doors, windows and openings, and floors their openings: since the contract's shells have no holes, they are cut into pieces round them, and an L-shaped floor into convex pieces along the beams under it. A wall under a roof rises to it, and the roof is exported too: its deck as shells, or the rafters, purlins and wall plates of a timber roof as frames of its timber class (C24 by EN 338). Angles and other unsymmetric sections are given about their principal axes. **Feet on footings** sets how the feet of columns and walls on a footing are held: fixed (the default) or pinned. A steel column base keeps its own connection.

![Export JG Structures](shots/export-jg.webp)

## Optimize an IFC file

**Optimize IFC…** (in File, or on the start screen: no project needs to be open) makes a lighter copy of one or more IFC files without changing what they say. The copy keeps every GlobalId, name, property set, quantity, type, material and relationship exactly as the file wrote them, and its header too; nothing of Lapin's is added. Only the geometry changes: meshes (face sets, faceted B-reps of plain faces) are cleaned (duplicate vertices welded, degenerate and repeated triangles and unused coordinates dropped) and simplified, never further from the original than the **Precision** (Fast 5 mm, Balanced 1 mm, Precise 0.1 mm, or your own), measured both ways; identical bodies are written once and shared by their products; and what nothing references (unused points, styles, empty representations) goes. Extrusions, booleans and other exact bodies stay as they are. Rounding the coordinates, B-reps written as face sets (IFC4 and IFC4X3 only) and a zipped `.ifczip` are there when you want them.

![Optimize IFC](shots/optimize-ifc.webp)

The copy is written next to the original, or in a folder you choose, under the original's name with an **Ending** (`.optimized` to start with): `house.ifc` gives `house.optimized.ifc`. The original is never written, and when the copy is already there Lapin asks before replacing it. For each file the report gives the size, entities and triangles before and after, the bodies shared, the orphans removed, the largest deviation measured and the time taken; **Open folder** shows the copy and **Open in Lapin** opens it in another window. In the browser the copy is downloaded. A file already in the project keeps its way back: **Optimize the source file…** on an imported IFC's group (in its properties) or on a link (Manage → Links) does the same to the file it came from, while that file is still on disk.

![The report](shots/optimize-ifc-report.webp)

## DXF and DWG

**Import DXF…** brings a CAD drawing into the view in front, with its layers, hatches, texts and dimensions. It comes in as drafting you can snap to and edit. To use a DWG, first save it as DXF from your CAD program (AutoCAD, BricsCAD or DraftSight: **Save As → DXF**), as text rather than binary. While the dialog reads a big file it counts the seconds; a file it cannot read (a binary DXF, a DWG renamed .dxf, a drawing whose blocks unfold into more than 2,000,000 entities) is named in the dialog with the reason. A PDF or an image comes in to trace over with **File → Import PDF or image…** or **Draft → PDF or image**: a vector PDF's lines as drafting at the scale it was drawn to, its pictures and any PNG or JPEG as see-through images (see [Drafting](drafting.md)). Into a plan, **Placed: by the map's coordinates** lays a drawing in real coordinates (a cadastral plan in Stereo 70) where the map puts it (see [Site](site.md)).

A DXF's blocks can come in as symbols (**Blocks → keep as symbols**) instead of exploded lines, and **convert its layers to elements…** turns its layers into walls, columns or bars straight after the import: see [Drafting](drafting.md#dxf-blocks-as-symbols) and [Converting drafting to elements](drafting.md#converting-drafting-to-elements).

The drawing also lies in the **3D view**, on the plane of the view it came into: on a plan at its storey's level, on a section or an elevation on that view's plane, in its layers' colours, its hatches faint, as thin lines that never hide the model. A layer turned off in **Draft → Layers** stays off in 3D. **View → DXF in 3D** (also in **View → View settings** of a 3D view) shows or hides every import; one import is switched by **In 3D** in the properties of any of its shapes or of its blocks kept as symbols, a linked DXF by **3D** in **Manage → Links**. Drawing in 3D on that level, with Draft's tools or Studio's Sketch, snaps to its lines, so you can trace over it in 3D as on the plan. Lines the file gave heights stand in space at those heights, as before.

![A site DXF in the 3D view, on the ground floor's level](shots/dxf-3d.webp)

An imported DXF is part of the project: **File → Save** keeps it in the `.lapin` file with its layers, hatches, texts and any group you made of it, and it is there when the project opens again, on the level's plan. The plan view it came into is the level's plan from then on, the one the project browser lists, and what was drawn on the level before goes onto it. Lapin asks to save before it closes on unsaved changes. To keep the drawing as a file of its own instead, tick **As a link** in the import dialog: it stays linked, read-only, and **Manage → Links** reloads it when the file changes.

## Links

**Link IFC…** keeps another file linked instead of importing it (a scan: **Link point cloud…**, below). It is read-only and reloaded when it changes. This works for a DXF too. **Manage → Links** shows, reloads, converts or removes the links. A linked DXF lies in 3D as an imported one does; its **3D** box in **Manage → Links** turns that off. **Visibility / Graphics** (<kbd>VV</kbd>) hides a link, halftones it or gives it its own categories in one view alone (*Sections, elevations and 3D*, Visibility / Graphics).

A linked IFC keeps what its file says of its elements' materials. Walls, floors and roofs with layer sets are drawn in their layers even as references, so an engineer hides the architectural link's finishes (**Walls ▸ Finishes**, **Slabs ▸ Finishes**) and a structural view style draws only their cores; a linked structural model's bars are in the **Rebar** category, which every 3D view leaves out until it is shown. A link made with an older version gets its walls' layers when it is reloaded (its floors' at once).

### Linking another Lapin project

**File → Link Lapin project…** places another `.lapin` file in this project as a read-only module, as a hotlink does in ArchiCAD. Say where it goes (**Placed at x**, **Placed at y**, **Turned**) and whether it is **Mirrored**, left and right swapped, as for the apartment on the other side of a staircase. Its walls, doors and floors come in as Lapin elements, with their plan symbols, and you can link the same project as many times as you want, each placement apart: a typical apartment or a repeated block, drawn in one file.

To change it, edit its own file: **Open separately** in **Manage → Links** opens it in another window. When that file changes, Lapin tells you as you come back, and **Manage → Links** says **The source changed: read it again**; a click brings in the new version, in the same places. The move button there places a link somewhere else or mirrors it, with no need to link it again. The link's other tools work as for any link: show, halftone, convert to native, unlink. In the browser the page has no path to the file, so reading it again or placing it elsewhere asks you for the file. What the source links itself comes along and moves with it, unless it is hidden there, and so do the references it brought from other files.

![The same project linked twice, the second turned](shots/hotlink-plan.webp)

![Manage → Links with both placements](shots/hotlink-links.webp)

## Point clouds

**File → Link point cloud…** links a laser scan or a photogrammetry cloud, in the open formats only: **LAS** (1.0–1.4, every point format), **LAZ** (compressed LAS), **E57**, **PLY** (ASCII and binary) and points as text, **XYZ**, **PTS** and **CSV**. Proprietary formats (Autodesk ReCap's RCP and RCS, FARO's FLS, Leica's LGS) are not read: export the scan from its program as E57 or LAS first.

Lapin asks where the cloud stands and in what unit:

- **by the project's map coordinates**: the file's x and y are eastings and northings, placed through the survey point and the angle to north of **Manage → Location**, its heights above the survey point's elevation. Lapin suggests it when the file names a coordinate system or its coordinates are far from zero and the project has a location;
- **origin to origin**: the file's origin on the project's;
- **centred on the project's origin**: its middle on the origin, its lowest point at ±0.00;
- **its unit**: as the file says (LAS and E57 say it), else metres; centimetres, millimetres, feet, US survey feet and inches are offered. When the cloud comes out unlikely for a building or a site (under 20 cm across or over 50 km), or the file says another unit, Lapin warns.

The cloud stays in its file. It is not copied into the project: the project keeps only the link (the file's path, where the cloud stands, how it is drawn). Lapin indexes it once, in the background, into an octree of tiles in its own folder on this computer (not beside the project), and **Manage → Links** shows how far it is. Then the 3D view streams the tiles the camera needs, nearest first and finer as you come closer, within a budget of points set by **View → Display quality** (1.5, 3 or 6 million points drawn at once), so a scan of a hundred million points turns as smoothly as a small one.

![A scan of the house linked as a point cloud, in the 3D view](shots/point-cloud-3d.webp)

In **Manage → Links** a cloud has its own row: its points, how far its index is, **Shown**, **Halftone**, **3D**, what colours its points (**RGB**, **Intensity**, **Elevation** or **Classification**, by the ASPRS classes: ground, vegetation, building, water…) and the **Size** of a point in pixels. The pin button places it again (by the map, origin to origin or centred, its unit, moved along x, y and z, turned); the reload button reads its file again and indexes it again only if it changed. **Unlink** removes the link; the index stays in Lapin's folder and serves the cloud again if it is linked again.

![Manage → Links with a point cloud](shots/point-cloud-links.webp)

A cloud is never exported with the model: IFC and Fragments carry the model, not the scan, and **Export 3D…** takes a sample of it as points in GLB and glTF only when asked. Plans, sections and elevations draw it too (*Sections, elevations and 3D*, Point clouds in views). In the browser a cloud of up to 160 MB can be linked; it is indexed in the page and has to be linked again after the page is reloaded. Larger clouds need the desktop app.

### Gaussian splats

A Gaussian splat capture (a scene trained from photos or a video by Postshot, Polycam, Luma, Lichtfeld Studio, Brush or the original 3D Gaussian splatting code) is linked the same way: **File → Link point cloud…** takes a **3DGS PLY** (the layout the trainers write: each splat's centre, colour, opacity, size and rotation), SuperSplat's **compressed PLY** (`.compressed.ply`), Niantic's **SPZ** (versions 1–4) and PlayCanvas's **SOG** (a `.sog` file, as SuperSplat and SplatTransform write it; on the desktop also a SOG folder, by its `meta.json`). Manage → Links names each file's format under its kind (**SOG v3 · SH 3**: the format, then the degree of its colour by direction). A splat file is a point cloud with more to it, and Manage → Links names its kind **Gaussian splats**:

- its splats' centres are indexed as the cloud's points, coloured by their base colour, the nearly transparent ones left out. So everything a point cloud does works on it: plans and sections draw its slice as dots, the snaps in 2D and 3D find its points, and **Visibility / Graphics** shows, hides and halftones it;
- a 3D view draws its splats: each one an ellipse that faces the camera, blended back to front, as the capture looks in its own viewer. Within the budget of **View → Display quality** (1, 2 or 4 million splats at once, half in a browser), the largest and most opaque splats are drawn first;
- splats come from reconstructions, whose axes are not a building's: an SPZ has y up, a 3DGS PLY, a compressed PLY and a SOG y down. Lapin stands the capture up by that when it is linked. If a file was written otherwise, the pin button in Manage → Links places it again and asks for its **Up axis** (z, y or −y), beside its unit, its move and its turn.

![A Gaussian splat capture of a garden pavilion in the 3D view](shots/gaussian-splat-3d.webp)

In Manage → Links a splat cloud's row has a **Display** list: **Splats** (its Gaussians) or **Points** (their centres, as any cloud, coloured and sized the same way). **Visibility / Graphics → Point clouds** has a **Drawn as** column that sets it for one view (**as its link**, **Splats** or **Points**). A point view is lighter and reads the shape better when tracing over it; a splat view shows the place as it was photographed.

![Manage → Links with a Gaussian splat capture](shots/gaussian-splat-links.webp)

![The same capture drawn as points](shots/gaussian-splat-points.webp)

Splats are not exported with the model (nor printed on sheets yet: a sheet draws a capture's slice as dots).

#### Full colour and base colour

A trained capture's colour changes with the side it is seen from: a shine on a car, a window that reflects the sky from one side and shows the room from the other. The file keeps that change as each splat's *spherical harmonics* (degrees 1 to 3, over its base colour). A 3D view draws it: in **Full colour** a splat's colour is worked out for the direction you look at it from, as the capture's own viewer does, whatever the link's turn and up axis. **Base colour** draws each splat in its base colour alone, the same from every side, with less memory and a quicker load. The list beside **Display** on the cloud's row in Manage → Links sets it for the link; **Visibility / Graphics → Point clouds** has a **Splat colour** column that sets it for one view (**as its link**, **Full colour** or **Base colour**).

Full colour follows **View → Display quality**: **Fast** draws the first degree (the broad change), **Balanced** and **Precise** all three. When the splats drawn are many, Lapin lowers the degree until their colours fit in memory (96 MB on the desktop, 48 MB in a browser): at Balanced's 2 million splats every degree fits; at Precise's 4 million, two.

![The pavilion as a SOG: its back wall warm seen from the garden, in full colour](shots/gaussian-splat-sh-front.webp)

![The same wall from behind: cool](shots/gaussian-splat-sh-back.webp)

![Manage → Links: the capture's format, SOG v3 with its first degree, and its colour set to Base colour](shots/gaussian-splat-sh-links.webp)

![The wall from the garden in its base colour](shots/gaussian-splat-sh-base.webp)

### Scale, crop and measure a capture

A capture trained from photos has no true size: its unit is whatever the reconstruction chose. **Calibrate scale** makes it true, for any point cloud, splats or not. Start it from the ruler button on the cloud's row in **Manage → Links**, or right-click near the cloud in the 3D view and choose **Calibrate scale…**. Click two points on the cloud a known distance apart (they snap to its points, a splat's centre) and type their true distance at the cursor, or click the second point and Lapin asks for it. The cloud grows or shrinks about the first point; its points, splats, slices, snaps and exports all follow, and one undo takes it back. The factor is shown as **Scale factor** in the place dialog (the pin button), beside the unit, where it can also be typed; the size check counts it.

![Calibrate scale: two splats' centres picked in 3D, their distance shown before the true one is typed](shots/gaussian-splat-calibrate.webp)

![The place dialog with the capture's scale factor](shots/gaussian-splat-place.webp)

A capture carries floaters round its subject and the neighbours' gardens; a scan, the street. The crop button on the cloud's row (or **Crop point cloud…** on the 3D view's right-click near it) opens its **crop box**: **Fit to cloud** makes a box round the whole cloud, along the link's own turn; then its middle, its sizes and its turn shrink it to what you keep, each change one undo step, and **Reset** takes it off. What is outside is hidden everywhere: in 3D (points and splats), in plans and sections, from the snaps and from the exports. The cloud's file and its index stay as they are.

![The crop box on a capture's row in Manage → Links](shots/gaussian-splat-crop-links.webp)

![The capture cropped: the tree at its edge cut away](shots/gaussian-splat-crop.webp)

**Measure** (<kbd>ME</kbd>) works on a capture in the 3D view as on the model: its points snap to the cloud's points and the splats' centres, and stay at the height they were picked. Its bar has two modes: **Distance**, a chain of legs with its running total shown in the tool's chip, and **Area**, a closed outline of snapped points whose area and perimeter, in the plane that best fits them, are shown as you pick them (a facade's patch, a roof's slope) and said again at the end, with the plane's tilt.

![Measure, Area: the capture's back wall measured on its splats](shots/gaussian-splat-area.webp)

A PLY that holds faces (a mesh, such as MeshSplatting's output) is not a point cloud: **Link point cloud…** says so and offers to import it as a 3D mesh instead, or to link its vertices as points.

## 3D models

**Export 3D (glTF, USD, DAE)…** writes the model as surfaces for rendering, visualisation and augmented reality (AR):

| Format | For |
|---|---|
| GLB, glTF | Blender, three.js, web viewers. Your materials' own images are included. |
| USDZ | Apple's AR Quick Look, Reality Composer, Omniverse. Images included. |
| USDA | OpenUSD as text, one file. |
| DAE | COLLADA, for SketchUp and older tools. |
| OBJ + MTL | A .zip for Twinmotion, Lumion, 3ds Max and any renderer. Images included. |

**Import mesh (OBJ, STL, glTF, PLY)…** brings in a mesh from another 3D program (OBJ, STL, glTF, GLB or PLY) as references on the active level. glTF says its unit and which way is up; for OBJ, STL and PLY Lapin asks. A PLY mesh coloured by vertex (as MeshSplatting and photogrammetry programs write it) comes in as one element whose colours are gathered into a few hundred parts; for a capture trained with 3D Gaussian splatting choose **−y** as its up. A piece of furniture or decoration brought this way is placed like any element: **Move**, **Copy**, **Rotate**, **Mirror** and **Array** in Modify, **Move 3D** in Studio for its height. Its copies share its triangles, so a row of chairs weighs what one does. Studio's **Make Studio solid** makes a reference's shape editable (see [Studio](studio.md)).

![Six chairs from an OBJ file round a table, made with Modify → Array](shots/reference-array.webp)

![Export 3D](shots/export-3d.webp)

You choose what to export, which phase, and the origin. **What** is **everything**, or the model **as the view shows it**: the 3D view in front, without its hidden elements and categories and the storeys it does not show, with the layers it shows (without finishes, or the cores only). When that view is cut (the cut above the storeys shown, a section shown in 3D, the section box), **Cut** cuts the export there too and closes each cut in its materials, so the file looks like the screen. A 3D view that leaves anything out is exported as it shows it unless you choose otherwise. Every element keeps its IFC class, type and GlobalId. To exchange the model itself rather than its surfaces, use **Export IFC**.

## Speckle

**File → Speckle → Send…** sends the model to a [Speckle](https://speckle.systems) server, from where Revit, Rhino and Grasshopper, Power BI, Blender and Speckle's web viewer take it, with no IFC between them. It is in the desktop app, not in Lapin for the browser.

![Send to Speckle: the account](shots/speckle-sign-in.webp)

The first time, Lapin asks for your account:

- **Server**: Speckle's own, `https://app.speckle.systems`, or the one your firm hosts.
- **Token**: a personal access token you make in Speckle, under your profile: **Developer settings → Access tokens → New token**, with the scopes `streams:read`, `streams:write` and `profile:read`.
- **Remember**: the token is kept in your computer's secure store (Keychain on macOS, Credential Manager on Windows, the Secret Service, GNOME Keyring or KWallet, on Linux), so Lapin signs you in by itself next time. It is never written into the project, Lapin's settings or a report. **Sign out** forgets it.

Signed in, you choose the **Project** (one of yours on that server), the **Model** to send to, or **a new model…** with its name, a **Message** for the version, and **What** goes:

- **the whole model**;
- **as the 3D view shows it**: the 3D view in front, without its hidden elements, categories and storeys, cut as it is cut, as **Export 3D** takes it;
- **the selection**.

**Send** shows how far it is (converting the model, uploading, making the version); **Cancel** stops it. When it is done, **Open the version in Speckle** opens it in your browser.

What arrives is a tree Project → Building → Storey → IFC class, with the site's elements beside the building. Each element is a Speckle object with:

- its **GlobalId** as its id in Speckle, so the next send makes a new version of the *same* objects (Speckle shows what changed between them);
- its surfaces, the same as **Export 3D** writes, in metres, in its materials' colours and transparency;
- its properties: IFC class, name, type, storey, phase, its materials and layers (each with its thickness), its property sets and its quantities, each with its unit.

The storeys go as Speckle levels, so Revit receives the elements on their levels. The materials' images (textures) and the elements' edges are not sent; a copy (a row of the same chairs) goes with its own surfaces.

### Receiving from Speckle

**File → Speckle → Receive…** brings a version of a Speckle model into the open project, whichever program sent it (Revit, Rhino, Archicad, another Lapin). It signs in with the same account as **Send…**. You choose the **Project**, the **Model** and the **Version** (**the latest** by default, or one from its list, by its message, date and the program that sent it), then:

- **Storeys**: **by the version's levels**, each object on the storey at its level (within 5 cm), else on the storey it stands on, else on the storey you are on; or **all on the current storey**.
- **Origin**: **the project base point** (Send writes the model from there, so a model sent from Lapin comes back where it was) or **the internal origin**.

**Receive** downloads the version (**Cancel** stops it) and brings it in as one step: one **Undo** takes it all out. The objects come in as **references**, as an imported mesh or IFC file does: shown, measured and snapped to, not edited. Each keeps:

- its surfaces in metres, whatever unit the version was in, in the colours of its render materials (a see-through one as a material of its own);
- its IFC class when the version names one (Revit's categories: walls, floors, roofs, doors, windows, columns, beams… as their IFC classes), else a building element proxy;
- its properties under **IFC properties**: the set **Speckle** (its applicationId, its Speckle id and type, the collections it stands in) and one set per group of its properties (**Speckle · Parameters**…);
- where it came from: the server, project, model and version.

The objects of one model come in as one group named after it. Copies of a block or family (Speckle's instances) share one body, as an imported Fragments file's do. Lines, polylines, arcs and circles that are an object by themselves are drawn in plan; points, texts and objects with no geometry are left out, and the dialog says how many of each.

**Receiving a newer version** of the same model updates what came before in place: each object is found again by its applicationId (else by its Speckle id) and keeps its GlobalId, its group and anything that names it; an object that did not change is left as it is; new ones are added to the model's group; those the version no longer has are removed, as a link's reload removes what its file no longer holds. The dialog shows how many were added, updated, unchanged and removed.

**A version sent from this project** (its objects are this project's own elements, by their GlobalIds) is not received over them: Lapin says so and offers **Receive as a copy**, which brings its objects in as references with ids of their own, beside the elements, which stay as they are. Received in another project, a model sent from Lapin keeps its elements' GlobalIds.

Not received yet: textures, the objects' edges, and recognising references as native walls and slabs (as an IFC import can).

## Lapin for the browser

**File → Lapin for the browser (.html)…** writes the project as one web page, a single .html file you can send to anyone. It opens with a double-click in any browser (Chrome, Edge, Safari, Firefox), on any computer, with no internet and nothing to install:

- **3D**: the model as the 3D view shows it. Drag to turn it, right-drag to pan, the wheel to zoom; **Fit** shows it all. Pick a storey alone or with those below it; click an element to select it (Shift adds to the selection), then **Isolate** it or **Hide** it; **Show all** brings everything back.
- **Drawings**: the plans, sections, elevations and sheets you ticked, drawn as Lapin prints them, as sharp at any zoom. Drag to pan, the wheel to zoom, **Print** to print the one shown. Click an element on a drawing to select it: it is the same element in 3D.
- **Estimate**, when the project has one: its chapters and lines, the totals, marked *indicative* when it uses Lapin's own rough values. Click a column's heading to sort, type in **Filter** to find a line, click a line to select its elements.
- **Properties**: whatever is selected, with its class, type, storey, material, its properties and quantities.

![File → Lapin for the browser](shots/html-export-dialog.webp)

In the dialog you choose which drawings go in (by default every one that shows an element of the model; a plan with only grids or marks is listed unticked, marked *(no element)*, and goes in if you tick it), the 3D model, the estimate, and **the project itself**. The page shows its size before it is written. The page speaks the language Lapin speaks when you export it.

![The page in a browser: the 3D model](shots/html-export-3d.webp)

![A plan in the page, a wall clicked showing its properties](shots/html-export-drawing.webp)

![The estimate in the page](shots/html-export-costs.webp)

With the project inside, the page is also the project: **File → Open** in Lapin takes the .html and opens the project it holds, unsaved; **Save** asks where to keep it as a .lapin, and never writes over the page. Without the project the page only shows the model, and Lapin does not open it. A web page that Lapin did not write is refused.

The page is a snapshot: what it shows is the project when it was exported, and nothing a reader does in it changes the project. It is as large as its parts: the project, the 3D model (about the size of a GLB export) and the drawings, with about 0.6 MB for the viewer itself.
