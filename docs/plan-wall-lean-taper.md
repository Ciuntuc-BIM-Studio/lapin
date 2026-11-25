# Leaning and tapered native walls: plan

Asked on 25.11.2026 in in-app report #191 (how to make a wall of variable thickness at top and/or bottom or along its length, and a wall that is slanted, not 90° vertical).

**Order: 4 of 6** (angle lock, structural usage, level datums, leaning walls, national packs, 3D mouse).
**Status: L1 and L1b done (branch `feat/wall-lean`, 25.11.2026); L2 done (branch `feat/wall-taper`, 25.11.2026); L3–L4 not started.**

## What exists today

- `Lapin.Architecture/Walls.cs` (`Geometry`, `LayoutOf`, `Section`/`Band`, `Frame` → `JoinWall`) builds every layer as `Prism.ExtrudeFootprint(footprint, z, h)`: strictly vertical. Band edges `From`/`To` are constant offsets from the axis. Joins (`Joins.EndsOf`, `LayerCorners`, `LayerCrossings`, `NodeFill`), extents (`LayerExtents`), end wraps (`WallEndWraps`, decision 862), zones, sweeps, roof/attach tops (`WallTops.Profile`, `topProfile`, decision 372) and openings (`Openings.Cutters` on a `WallFrame`) all assume that 2D footprint and vertical extrusion.
- Workarounds today: Studio solids (`WallsFromSolids` cannot turn them back into walls: it fits a plan rectangle); Civil's `BatteredWalls.cs` / `LapinRetainingWall` (plan-wing-walls S1–S4): front and back batters n:1, crown and base thickness, warped faces, exact IFC; unlayered, not a host for doors and windows. Wall by profile shapes only the top edge.
- IFC export writes IfcWall with a Brep per layer, `IfcMaterialLayerSetUsage` (AXIS2) and the attributes carried in `Lapin_Intent` (`IntentProperties` walks `e.Attrs`). The import does not read oblique extrusions, so such walls stay references (fidelity gate).

## Design

One idea serves all stages: a band edge becomes a function of height, `y(z) = y0·s + z·k`.

- **L1, lean.** An affine map `(x,y,z) → (x, y/cosθ + z·tanθ, z)` in the wall frame. Faces stay planes, layers keep their normal thickness, every horizontal section is a translated copy of the base footprint, the vertical height H stays the storey's.
  - New attributes on the wall (default on the type): `lean` (degrees from vertical, + towards the left face) and `leanFrom` (`base` | `top` | `axis`; default `base`: the wall stays on its footing or slab edge).
  - Build the layout at z0 and at z1 (joins computed on both levels, the other wall's frame also moved); each band piece is a loft between two footprints with the same vertices (`Lofts`/`SolidBuilder`), not `ExtrudeFootprint`. A vertical wall (lean 0) keeps the prism path and gives byte-identical output (a gate test).
  - Corner joins between two leaning walls: each corner trace is the line where two face planes meet, linear in z, so the loft is exact.
  - Openings: `openingCut` = `wall` (default: the cutter box goes through the same affine map, jambs lean in the wall's plane, sill and head stay level) or `vertical` (a plumb box). Joinery 3D placement follows the same matrix; a lintel or sill follows its opening.
  - Stage 1 refuses with a `Problem`, not silently: curved walls, `topProfile`, attach-to-roof or slab, layer extents that extend up or down, end wraps. Each comes back in L4.
- **L2, taper bottom to top.** `thicknessTop`, one face plumb (`plumbFace`, the user's answer of 25.11.2026 below; no per-face angles). Finish and other non-core layers keep their thickness and follow their face; the core (or a chosen `taperLayer`, any layer selectable) takes the difference. Zones taper with the core; the end faces of mitred joins become ruled (triangulated, allowed by the kernel).
- **L3, taper along the length.** `thicknessEnd`. Face lines are no longer parallel to the axis, so `JoinWall` must carry a face line per end and the join solver must use general lines: this touches every join test.
- **L4.** Lift the L1 refusals: roof/attach tops, `topProfile`, extents, wraps, sweeps. A curved leaning wall (a conical surface) later, with the exact tessellation from `Chords`.
- Retaining walls: Civil's `LapinRetainingWall` stays for footings, keys and warps; the native leaning wall serves buildings (and gets doors and windows). Both.
- GlobalIds unchanged (only attributes are added); missing means 0, so no `IProjectUpgrade`; each command is one undo step. Rooms: the boundary is taken at the floor.

## IFC

- A leaning wall (lean only) exports as IfcWall, PredefinedType from its type, Axis kept; the body is a SweptSolid `IfcExtrudedAreaSolid` with an oblique `ExtrudedDirection` (exact, best read by Revit and ArchiCAD); a Brep stays the fallback when joins make the ends irregular. `IfcMaterialLayerSetUsage` stays, thicknesses measured normal to the faces.
- Taper: a Brep, the `IfcMaterialLayerSetUsage` with the nominal (base) layers, the attributes in `Lapin_Intent`. **Not `IfcMaterialLayerWithOffsets`** (checked against the specification, 25.11.2026): its OffsetDirection (AXIS3 or AXIS1, never the layer set's direction) and OffsetValues[1..2] move a layer's *edges* at the start and end of its extrusion (the spec's example: an insulation layer running 100 mm past the top of the wall, OffsetValues = (0, 100)), with ReferenceExtent required on the usage. It says where a layer starts and stops, not that its thickness changes, so a reader would take a tapered core for a core stopping short of the top. It fits Lapin's layer extents (`LayerExtents`, a layer short of or past the wall's top or base) instead, a separate piece of work. Same conclusion as plan-floor-slopes for sculpted floor layers.
- `lean`, `thicknessTop`, `thicknessEnd` travel in `Lapin_Intent`.
- Import (L1b): read an IfcWall SweptSolid with an oblique direction and a layer set as a native leaning wall; it must pass `RecognitionFidelity` (1 mm / 0.5 %), otherwise it stays a reference.

## UI and drawings

- Wall properties: a "Lean" angle field with a ratio display n:1 for civil users; L2 adds "Thickness at top" and "Layer that tapers". Multi-select through `element.update {ids}`; the same defaults on the type in `WallTypesDialog`; the wall tool's option bar gets "Lean". A section-view grip to drag the lean comes later.
- One `VisibilityGraphics` resolver: plan cut = the footprint at cut height, translated by `cut·tanθ`; the base line drawn below and the top edge overhead when outside the cut; openings' plan gaps at cut height. Sections and 3D cut the real solids; elevations show the true sloping faces.
- Quantities (`Walls.Quantities`): Width is the nominal normal thickness, Height is vertical; GrossSideArea and NetSideArea per face (L·H/cosθ); Volume from the solids; finishes' and paint areas per real face; formwork (Construction) the leaning faces with their true areas.

## Tests first (new specification; docs/transfer.md has none for this)

`WallLeanTests`: a 5 m × 3 m wall at 10° with 0.3 m (volume, face area, top edge offset 3·tan10°); vertical identical to today vertex for vertex; an L corner of two leaning walls and one leaning plus one vertical (traces on the plane intersections, no gap or overlap: manifold union volume); a layered type keeps each layer's normal thickness; a door with `wall` and with `vertical` cut (volume removed, sill level); plan cut translated; IFC round trip (an oblique extrusion read back native, same GlobalIds; a gate fail means a reference); each refused combination names its problem; undo is one step. L2: `WallTaperTests` (core absorbs, finishes constant, one face plumb, another layer tapering, zones, corners with a plumb, a leaning and a tapered wall, door, plan, type and own values, refusals, one undo step, IFC round trip with the nominal layers and no `IfcMaterialLayerWithOffsets`, see IFC).

## Risks

The dense join code computed at two levels can give different piece topologies (loft only when vertex counts match, otherwise split by height bands); roof and attach cuts after the map; loft performance on big models (keep the prism path at lean 0); hosted families (sweeps, skirtings, curtain walls) on a leaning host; snapping and temporary dimensions measure at the base.

## Stages

| Stage | Content | Effort | Status |
|---|---|---|---|
| L1 | Lean, straight walls: layers, joins, openings, plan, quantities, IFC export; guide (walls.md EN and RO), scene `wall-lean`, strings, catalogue | L | Done |
| L1b | IFC import of oblique walls | M | Done |
| L2 | Bottom/top taper, one face plumb, the core absorbs | M–L | Done |
| L3 | Taper along the length (join solver with general face lines) | L | Not started |
| L4 | Lift the L1 refusals | M | Not started |

### What L1 and L1b are (25.11.2026)

- `WallLean.cs`: `lean`, `leanFrom` (`base`, `top`, `axis` = mid-height), `openingCut` (`wall`, `vertical`) on a wall and on its type (the wall's wins); `Refusal` names what stage 1 does not lean (curved, `topProfile`, a roof top or attach, layer extents or layers going on into the floors, wrapped ends), which then stands plumb with that problem. `WallLean.At(z)` puts a level in force: every leaning wall's `Walls.Section` is then its bands as they are at that height (each edge `y/cosθ + (z − pivot)·tanθ`), so `LayoutOf` and the joins give the layout there.
- `WallsLean.cs` (`Walls.LeanGeometry`): the layout at the base and at the top, each band's pieces matched vertex for vertex (the top's turned to the base's start) and lofted (`WallLean.Loft`: planar sides where the corners are on a plane, else two triangles); zones and height pieces cut from the same lofts (the vertices move linearly up). A plumb wall joined to a leaning one is lofted too (its end follows the other's face); a wall touching none is built as before (byte-identical, a gate test). Openings, lintels and bare openings' linings: the plumb wall's leaned by the wall's map (the hole is the same whichever way the door stands); doors and windows lean with the wall or stand plumb (`WallLean.Follow`). Plan: the cut at the plan's height, the base line seen below and the top edge overhead on the sides outside the cut. Quantities: side areas / cosθ, Width the normal thickness, Height vertical. `Envelope` leaned.
- IFC: `IfcSweep.Direction` (oblique `ExtrudedDirection`) and `LeaningWallBodies` write a one-layer leaning wall as an oblique `IfcExtrudedAreaSolid` where it is the body (else the B-rep); `lean` in Lapin_Intent. L1b: `ForeignElements.Wall` reads a slanted prism swept straight across its footprint as a leaning wall (normal thickness = horizontal · cosθ), through the fidelity gate; a prism swept along the wall stays a reference.
- Not yet: room boundaries use the wall's base footprint at its normal thickness (a few mm off the floor's true one at a steep lean); hosted sweeps, skirtings and curtain walls on a leaning host; snapping and temporary dimensions measure at the base; the wall tool's option bar has no Lean yet (`architecture.addWall` takes `lean`).

### What L2 is (25.11.2026)

- `WallTaper.cs`: `thicknessTop` (metres, 0.01–10), `plumbFace` (`first`: the first layer's face, the wall's right as drawn and usually its exterior, the default; `last`), `taperLayer` (a layer's index from the first, 0; empty: the core) on a wall and on its type (the wall's wins). The base keeps the layers' thickness; the leaning face moves by (top − base)·h/H; the layers between the plumb face and the tapering one stay plumb, those beyond lean with the other face, each its normal thickness (horizontally t/cosφ); the tapering layer takes what is left, refused (a problem, the wall keeping its thickness) when that is under 10 mm at its base or top, or when the layer asked for does not exist (the core then tapers). Same refusals as L1 (`WallLean.Refusal`, worded for a taper); a tapered wall ignores its lean, with a problem. `WallLean.Sectioned` gives the tapered bands at the level in force, so `WallsLean.LeanGeometry` lofts it as it does a leaning wall, zones included; `WallLean.Loft` splits a twisted side on the diagonal through its lowest corner, so two walls sharing a mitre split it alike.
- Openings: the frame is the wall's widest section (`Openings.FrameOf`), so every hole goes right through; doors and windows on the wall's middle at their mid-height (`WallTaper.Follow`); lintels intersected with the tapering core. Plan: the cut at the plan's height, the leaning face's base line below (thinning) or its top edge overhead (thickening). Quantities: Width the mean thickness, each layer its normal thickness, the tapering one its mean; GrossSideArea the elevation's. `Envelope` lofted.
- UI: Thickness at top, Face that stays plumb, Layer that tapers in a wall's properties (several walls, `element.update`) and in Wall types (`WallTaperFields.tsx`); guide (Walls → A tapered wall), scene `wall-taper`. Tests: `WallTaperTests` (13).
- Not yet: reveals and bay-window dressings of a tapered wall's openings are set against its widest section; room boundaries use the base; the wall tool's option bar has no Thickness at top (`architecture.addWall` takes `thicknessTop`); a tapered wall from another program's IFC stays a reference (only Lapin's own come back native, through Lapin_Intent).

## The user's answers (25.11.2026)

The user accepted every recommendation:

1. A leaning wall keeps its normal thickness (not the horizontal one). **Yes.**
2. The base stays put when the lean changes. **Yes.**
3. Openings follow the wall by default, vertical as an option; room boundaries at the floor. **Yes.**
4. On a taper the core changes by default, any layer selectable; finishes stay constant. **Yes.**
5. Retaining walls: both the Civil `LapinRetainingWall` and the native leaning wall. **Yes.**
6. L2, which face moves when `thicknessTop` is set: **one face stays plumb** and the other leans (retaining and foundation walls); a parameter picks which face stays plumb (`plumbFace`, default the first layer's face, the wall's right as drawn: its exterior); the core (or a chosen `taperLayer`) absorbs the change, the other layers keep their thickness and follow their face; zones taper with the core. Asked again: `IfcMaterialLayerWithOffsets` checked against the IFC4 specification before writing it (not used, see IFC above).
