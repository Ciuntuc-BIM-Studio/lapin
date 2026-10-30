---
title: Import and export
order: 110
summary: Exchange the model as IFC, bring in DXF drawings, link files, and export 3D models for rendering and AR.
---

Everything that goes in or out is in the **File** menu.

![The File menu](shots/file-menu.webp)

## IFC

**Export IFC…** writes the model as IFC. Every wall, floor, door and roof is a real IFC element with its layers, its type and its properties, so the file opens as a model in other BIM tools. Doors and windows say how they open: the hand of each leaf and sash, which way it swings, and the panels, the way IFC defines them, so other programs draw the same swings.

**Import IFC…** reads another program's IFC. Before anything comes in, Lapin reads the file and shows what it holds: its elements by discipline (architecture, structure, MEP, furniture and equipment, site), each discipline by kind with how many there are, and its storeys with how many elements stand on each. Untick what you do not need, for example MEP and furniture when you work on the structure, or keep only the storeys you work on. **How** brings the elements recognised as Lapin elements, all of them as references, or linked. While the file is read and imported, Lapin shows what it is doing and how far along it is; a large file takes a while.

![Import IFC](shots/import-ifc.webp)

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

![Export 3D](shots/export-3d.webp)

You choose what to export, which phase, and the origin. **What** is **everything**, or the model **as the view shows it**: the 3D view in front, without its hidden elements and categories and the storeys it does not show, with the layers it shows (without finishes, or the cores only). When that view is cut (the cut above the storeys shown, a section shown in 3D, the section box), **Cut** cuts the export there too and closes each cut in its materials, so the file looks like the screen. A 3D view that leaves anything out is exported as it shows it unless you choose otherwise. Every element keeps its IFC class, type and GlobalId. To exchange the model itself rather than its surfaces, use **Export IFC**.
