# The finish painter (forum topic 83)

A tester (Rychlas) asked for a **Finish Editor**: beside the room finishes laid on whole rooms (decision 265), an edit mode
where walls, floors and ceilings are divided into sections and painted with ready-made finishes by a brush — and, above
all, the **outside** of the building, which room finishes cannot reach.

## The model

- **A finish zone** (`LapinFinishZone`, exported as `IfcCovering`) belongs to one **named face** of a host element, the
  faces the platform already names for formwork (`IElementFaces`): a wall's `Face.Left` / `Face.Right` (left and right of its
  axis, so a flipped wall keeps its paint on the same side of the building), a floor's `Top` and `Soffit` (the ceiling under
  it) and its edges, a column's sides. The face key is semantic, never a triangle: the face is rebuilt from the host's
  attributes on every ask, so moving, lengthening or re-typing the host carries the zones with it.
- **The face's own frame**: origin at the face outline's first corner, `u` to the right seen from outside (horizontal on an
  upright face, plan x on a level one), `v` up the face (plan y on a top), `w` out of it. Everything stored on a face is in
  `(u, v)`, so it travels with the host.
- **Splits** (`LapinFinishSplit`): lines, polylines and closed shapes drawn on a face, in its frame. The face outline and
  every split make a planar graph; its bounded faces are the **regions** (`SketchRegions`, Studio's own face regions, moved
  into the geometry kernel so both use one rule). A shape floating inside a region is a region of its own, taken out of the
  one round it.
- **What a zone paints**: a zone with a **seed** point paints the region holding it (the smallest); a zone with none paints
  the **rest of the face**, every region no seeded zone holds. So "paint the face" and "paint this part" mix freely, and a
  split added later divides what was painted. A painted region painted again replaces its zone; **clear** removes it (a
  region of the rest gets a *bare* zone: nothing built there).
- **The finish**: a room finish type's part (its walls', floor's or ceiling's layers, read on every ask, so changing the
  type repaints), a **surface finish type** (`LapinSurfaceFinishType`: a name, layers from the face outward, a use: walls,
  floors, ceilings or any — ETICS, lime plaster, wall tiles, floor tiles, stone cladding, paint, from a library), or layers
  of its own (one material and a thickness, from the palette's materials). Layers always run **from the face outward** (a
  floor's are turned over, since a room finish lists them from the top down). A room finish's pieces up the height (tiles
  to 2.10 m) are not used on a zone: the painter divides the face instead.

## What it builds (derived on every ask)

- Each layer of each owned region: the region (less the host face's openings and the regions inside it) swept out of the
  face by the layer's depth and thickness, as a body of its own material. Real geometry, so **plans, sections and elevations**
  cut and hatch it by material, the **3D** shows it, **IFC** writes it.
- At a wall's **outer corner** (its face reaching past the axis end, where another wall joins), the layers are mitred at
  45°, so two painted facades close the corner (right-angled corners exactly).
- **Quantities**: per layer and material its net area (openings out), its gross (regions only) and volume
  (`Lapin_FinishLayerQuantities`), and the covering's `Qto_CoveringBaseQuantities`; the **Surface finishes** schedule lists
  them by host, face and material, with totals. Costing takes them as finishes.
- **IFC**: an `IfcCovering` per zone (CLADDING on an upright face, FLOORING on a top, CEILING on a soffit), its layers as
  its body's parts, and `IfcRelCoversBldgElements` from the host to its coverings.
- A host deleted takes its zones and splits with it (in the same undo step).
- **Rooms** keep measuring to the room finish (decision 554): a zone painted inside a room does not change the room's area.

## The painter (the interface)

An edit mode (the platform's edit scope, as MEP's system editing and Studio's part editing), **Finish painter…**, from the
Finishes tab and the Room finishes dialog: walls, floors, columns and the zones stay, everything else is faded. A panel
holds the **palette** (the project's room finishes split into their walls, floor and ceiling; the surface finishes; the
library's not yet brought in; a material with a thickness), and the tools:

- **Brush**: a click on a face paints the region under it (Shift: the whole face); in 3D the region lights up under the
  cursor; in a plan, a wall's side or a floor's top.
- **Split by rectangle**, **Split by line** (a polyline): drawn right on the face in 3D (the direct sketch of Studio, with
  its inferences and typed values), stored as a split.
- **Eyedropper**: a click takes the finish of the region under it into the palette.
- **Clear**: a click clears the region under it (Shift: the whole face, its splits kept).

Every stroke is one command, one undo step.

## Stages

1. **This stage**: the model and commands (`architecture.paintFinish`, `splitFinishFace`, `clearFinish`, the surface
   finish types and library, the queries `finishFaceAt` and `finishPalette`); walls, floors (tops, soffits, edges) and any
   element whose module names its faces (columns, beams); geometry, mitred outer corners, quantities, schedule, IFC; the
   painter in 3D and the brush in plans.
2. **Next**: painting in sections and elevations; splits drawn in plans (a line across a wall); the reveals of doors and
   windows lined by an exterior finish; corners other than right-angled mitred exactly, and a lone corner wrapped when the
   other face is not painted; sloped wall tops (gables) clipping the zones; curved walls; zones copied with their host
   (copy, storey copies, groups); IFC import of Lapin's own coverings back into zones; build-up tags of zones; the
   planning generator's exterior finishes phase; textures that follow the zones' frames.
