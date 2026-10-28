---
title: Import and export
order: 110
summary: Exchange the model as IFC, bring in DXF drawings, link files, and export 3D models for rendering and AR.
---

Everything that goes in or out is in the **File** menu.

![The File menu](shots/file-menu.webp)

## IFC

**Export IFC…** writes the model as IFC. Every wall, floor, door and roof is a real IFC element with its layers, its type and its properties, so the file opens as a model in other BIM tools. Doors and windows say how they open: the hand of each leaf and sash, which way it swings, and the panels, the way IFC defines them, so other programs draw the same swings.

**Import IFC…** reads another program's IFC. Lapin recognises the walls, floors, columns, beams, footings, doors and windows and turns them into Lapin elements you can edit. Whatever it cannot recognise comes in as a reference, drawn but not editable. Doors and windows come in opening the way the file says: their hand, the side they open to and, for windows, how each panel opens.

The file's spaces (from Revit, ArchiCAD and others) come in as Lapin rooms, each on its own level, with their outline, number, name, height, function, department and floor finish: see [Rooms](rooms.md#rooms-from-an-ifc-file). Everything an imported element had in the file (its property sets and quantities) is kept and shown under **IFC properties** in its properties.

## DXF and DWG

**Import DXF…** brings a CAD drawing into the view in front, with its layers, hatches, texts and dimensions. It comes in as drafting you can snap to and edit. To use a DWG, first save it as DXF from your CAD program (AutoCAD, BricsCAD or DraftSight: **Save As → DXF**). PDF import is not available yet.

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

![Export 3D](shots/export-3d.webp)

You choose what to export (the whole model or what is selected), which phase, and the origin. Every element keeps its IFC class, type and GlobalId. To exchange the model itself rather than its surfaces, use **Export IFC**.
