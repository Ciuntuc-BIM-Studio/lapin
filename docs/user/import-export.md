---
title: Import and export
order: 110
summary: Exchange the model as IFC, bring in DXF drawings, link files, and export 3D models for rendering and AR.
---

Everything that goes in or out is in the **File** menu.

![The File menu](shots/file-menu.webp)

## IFC

**Export IFC…** writes the model as IFC. Every wall, floor, door and roof is a real IFC element with its layers, its type and its properties, so the file opens as a model in other BIM tools. Doors and windows say how they open: the hand of each leaf and sash, which way it swings, and the panels, the way IFC defines them, so other programs draw the same swings.

The export asks for the schema (**IFC4**, read everywhere, or **IFC4X3**), the detail (parts as elements of their own, or one body per element), the phase, and how the bodies are written. **Geometry** is a **faceted B-rep** (exact, what other programs expect most often) or **face sets**: the same faces in a much smaller file. **Simplify** writes fewer triangles where a body is a fine mesh, such as the ducts and pipes of another program's file, never further from it than the **tolerance** you type (2 mm to start with). Bodies Lapin builds itself (walls, floors, doors) keep their exact faces, and simplifying changes only the shape of a body, never its properties.

![Export IFC](shots/export-ifc.webp)

**Import IFC…** reads another program's IFC. Before anything comes in, Lapin reads the file and shows what it holds: its elements by discipline (architecture, structure, MEP, furniture and equipment, site), each discipline by kind with how many there are, and its storeys with how many elements stand on each. Untick what you do not need, for example MEP and furniture when you work on the structure, or keep only the storeys you work on. **How** brings the elements recognised as Lapin elements, all of them as references, or linked. While the file is read and imported, Lapin shows what it is doing and how far along it is; a large file takes a while.

![Import IFC](shots/import-ifc.webp)

Some files carry 2D besides their model: plan symbols, footprints, hatches and texts drawn for an element, or elements drawn only in 2D (lines, texts, symbols with no body). When a file has them, the dialog counts them under **Drawn in 2D**. Untick **the elements' own 2D** and a plan draws those elements by their cut, as it does Lapin's; untick **elements drawn only in 2D** and they stay out. Both come in unless you untick them.

![Drawn in 2D](shots/import-ifc-2d.webp)

**Clean-up and optimisation** holds the import's rules, the same for any file, kept as your own for the next import. **Keep a repeated body once** (on unless you untick it) keeps a body the file repeats under other names (a bolt, a flange, a support) once, its elements sharing it, nothing lost. The others leave out, when ticked, what another program brought along: **flat bodies** (texts, lines and hatches turned into triangles), **elements further from the rest** than the distance you type (a drawing's texts, a title block), **duplicates** in the same place, and **ports and openings written as objects**. **Simplify meshes** writes fewer triangles within the tolerance you type; **read numbers written as text** turns `DoubleLength:0,01` into 0.01 m and `1.15395 kg` into a mass (a whole number alone, a code, stays text). Under **What to bring**, **Property sets** lists the file's sets with how many elements carry each; the **technical** ones (Navisworks' transform, internal ids, geometry statistics) are left out unless you tick them. The import's message says what each rule did.

![Clean-up and optimisation](shots/import-ifc-rules.webp)

Lapin recognises the walls, floors, columns, beams, footings, doors and windows and turns them into Lapin elements you can edit. Whatever it cannot recognise comes in as a reference, drawn but not editable. Doors and windows come in opening the way the file says: their hand, the side they open to and, for windows, how each panel opens.

The file's spaces (from Revit, ArchiCAD and others) come in as Lapin rooms, each on its own level, with their outline, number, name, height, function, department and floor finish: see [Rooms](rooms.md#rooms-from-an-ifc-file). Everything an imported element had in the file (its property sets and quantities) is kept and shown under **IFC properties** in its properties. A reference leaves **Export IFC…** as it came in: with the same GlobalId, class and attributes (description, tag, predefined type), its property sets and quantities with every value typed as the file had it, and its type, material, ports, connections and system, each with its GlobalId. Bring only the MEP of a file and export it, and the new file carries it whole.

## DXF and DWG

**Import DXF…** brings a CAD drawing into the view in front, with its layers, hatches, texts and dimensions. It comes in as drafting you can snap to and edit. To use a DWG, first save it as DXF from your CAD program (AutoCAD, BricsCAD or DraftSight: **Save As → DXF**). PDF import is not available yet. Into a plan, **Placed: by the map's coordinates** lays a drawing in real coordinates (a cadastral plan in Stereo 70) where the map puts it (see [Site](site.md)).

## Links

**Link IFC…** keeps another file linked instead of importing it. It is read-only and reloaded when it changes. This works for a DXF too. **Manage → Links** shows, reloads, converts or removes the links.

## 3D models

**Export 3D…** writes the model as surfaces for rendering, visualisation and augmented reality (AR):

| Format | For |
|---|---|
| GLB, glTF | Blender, three.js, web viewers. Your materials' own images are included. |
| USDZ | Apple's AR Quick Look, Reality Composer, Omniverse. Images included. |
| USDA | OpenUSD as text, one file. |
| DAE | COLLADA, for SketchUp and older tools. |
| Fragments | That Open Engine's `.frag`, for BIM web viewers built on it (That Open Components, Bubbles). One file; identical bodies stored once. |

**Import Fragments…** brings a `.frag` file in: every item with geometry becomes a reference drawn from its body, in the class the file names, on the storey the file puts it (matched by name to yours, made when missing), with the file's attributes under **IFC properties**. Items with the same body (the same shape, placed in many places) share it. Importing the same file again updates what it brought before. Nothing is recognised into walls or floors: a `.frag` holds triangles.

**Import mesh (OBJ, STL, glTF)…** brings in a mesh from another 3D program (OBJ, STL, glTF or GLB) as references on the active level. glTF says its unit and which way is up; for OBJ and STL Lapin asks. Studio's **Make Studio solid** makes a reference editable (see [Studio](studio.md)).

![Export 3D](shots/export-3d.webp)

You choose what to export, which phase, and the origin. **What** is **everything**, or the model **as the view shows it**: the 3D view in front, without its hidden elements and categories and the storeys it does not show, with the layers it shows (without finishes, or the cores only). When that view is cut (the cut above the storeys shown, a section shown in 3D, the section box), **Cut** cuts the export there too and closes each cut in its materials, so the file looks like the screen. A 3D view that leaves anything out is exported as it shows it unless you choose otherwise. Every element keeps its IFC class, type and GlobalId. To exchange the model itself rather than its surfaces, use **Export IFC**.
