# Floor slopes: falls, drains, split parts, a sculpted layer

Proposed on 19.11.2026 (decision 724, **proposed, not built**). Source: GitHub issue #96, from forum topic 157 (Rychlas), postponed on 18.11.2026 "to be planned later, starting with a spec (the model change is the hard part)". This is that spec. Nothing here is built yet; the questions for the user are in §11.

## 1. The request

From the forum (Rychlas, "a very important tool to have"):

- cut out or split parts of a floor and give each its own relative offset or level; set points, lines or shapes with a relative offset or a level;
- choose how the slope is made: by **bending the floor's core plane** (as walls follow a slanted roof), or by **varying the thickness of one layer** (a screed to falls on a flat structural slab), and choose which layer is sculpted;
- give each edge of the floor, or each split line, its own slope and direction (as roofs have per-edge pitches);
- say the level or offset at which a slope ends, and the distance it runs from its line, measured along the slope or in plan;
- slopes for a room: the tool takes the room's outline and starts from it (a bathroom falling to a drain);
- drainage points; "the more, the better".

The issue adds what the spec must settle: how a sloped floor is stored (still an IfcSlab with an IfcMaterialLayerSetUsage, the slope kept as the user's definition and the shape derived on every ask), the two ways of making it, how layers, quantities, drawings and IFC follow, and one shared idea of a slope with roofs (forum 160) and the terrain's slope tags (#82).

## 2. What others do

**Revit (Shape Editing on floors and roofs).** *Modify Sub Elements* drags or types the vertical offset of the slab's corners and edges; *Add Point* and *Add Split Line* add vertices and creases on the top; *Pick Supports* turns beams into split lines; *Reset Shape* goes back to flat. A layer of the type may be ticked **Variable**: then only that layer changes thickness and the bottom stays flat; with no variable layer the whole slab bends at a constant thickness. Revit triangulates the points and lines into its top surface. A floor sketch can also carry a **slope arrow** (tail and head heights, or a slope) for a single plane. What users find clunky (and what Rychlas means): offsets are typed point by point, so a 1.5 % fall is computed by hand; a split line must be drawn edge to edge; a drain with falls is four split lines to the corners and one point; nothing knows a room; the shape is lost when the outline is edited.

**ArchiCAD.** The Slab tool makes flat slabs; sloped single-plane floors are usually made with the Roof tool, falls and dished surfaces with the Mesh tool (points with heights and ridges) or Shell, or by subtracting a falls body with Solid Element Operations. I could not verify in this session whether a recent version added a slab with a custom top surface; that line of the comparison is to be checked before stage S2.

**What Lapin has today.**

| Area | What exists | Where |
|---|---|---|
| Floors | An outline with arcs, ellipses and its own holes; `offset` from the storey; layers from the top down from the type (`IfcMaterialLayerSetUsage`, AXIS3); per-edge **layer extents** (short of or past the outline); Edit sketch, offset outline, split and trim; beam-and-block floors. **Flat everywhere**: each layer is `Prism.ExtrudeFootprint` between `top + from` and `top + to`. | `Lapin.Architecture/Slabs.cs` (`Geometry`, `Envelope`, `Quantities`, `Top`), `LayerExtents.cs`, `BeamBlock.cs`, `docs/user/floors-roofs.md`, `docs/plan-direct-editing.md`, `docs/plan-layer-extents.md` |
| Roofs | Pitch per edge (eave or gable), overhangs, slope markers in plan (`slope` grips, a click toggles), a solver of planes, gutters with a fall to outlets (`Drainage.FallOf`, mm/m), walls attached under the slopes. | `Roofs/RoofSolver.cs`, `RoofEdges.cs`, `Drainage.cs`, `ui/src/modules/architecture/roofEdges.ts` |
| Ramps | A flight with a level at each end, its own class (IfcRamp). | `Stairs/Ramps.cs` |
| Terrain | Points, constrained Delaunay (`Tin.Delaunay` with breaklines), `ElevationAt`, platforms with cut/fill, spot levels and **slope tags** (% and 1:N, `DrawSlopeArrow`), 3D point grips (`pointGrips.ts`), subregions. | `Lapin.Civil/Tin.cs`, `Terrains.cs`, `CivilModule.Marks`, `SiteCommands.cs` (`addSlope`), `docs/spec-terrain.md`, decision 670 |
| Drafting | Dimension kinds `slope` and `level`. | `Lapin.Drafting/DimensionKinds.cs` |
| Room finishes | A room's floor finish (`LapinRoomFinish`, part `floor`, layers from the room's level down, IfcCovering FLOORING), and an optional structural slab under it. | `RoomFinishes.cs` |
| MEP (open) | Drainage roles (stack, cleanout, sanitary waste ports); no floor drain yet. | `Lapin.Mep/Roles.cs` |
| Attach top/bottom | Walls, columns, stairs and Studio solids attach to bodies, read as triangles (`BodySampler`). Floors are "top: no" there: a floor's top is its own model, which is what this plan adds. | `docs/plan-attach-to.md` |

Lapin's advantage to use: slopes are already typed as values in three places (roof pitch in °, gutter fall in mm/m, terrain tags in % and 1:N), the terrain already triangulates points with breaklines, and every drawing, quantity and export reads the floor's **solids**, not its parameters. If the floor's solids follow the slope, sections, the 3D view, clash, the cut order, mesh exports and the IFC body follow with no work of their own.

## 3. Decisions proposed

1. **The floor stays one IfcSlab with its type's layer set.** The slope is a definition on the floor (`surface`, §4), never baked geometry. A floor without `surface` is flat and built exactly as today (same faces, same volumes, same GlobalIds), so no `IProjectUpgrade` is needed.
2. **One height field.** Everything the user sets (a plane, points, lines, drains, edge slopes, split regions) compiles into one function `h(x, y)`: the height of the shaped surface above the floor's reference top (`Slabs.Top`, i.e. storey + `offset`). Tools are only ways of writing control data; the geometry has one builder.
3. **Two ways, chosen per floor (`how`):**
   - `bend` (Revit's constant-thickness shape edit, Rychlas's "core plane"): every layer moves by `h`; the bottom follows the top.
   - `layer` (Revit's *Variable*, "screed to falls"): one layer, chosen by the user, takes up `h` in its thickness; the layers above it ride on it at their own thickness; the layers below stay flat. The default sculpted layer is the first non-core layer above the core whose name or material says screed, else the layer just above the core; a type with no layer above the core falls back to `bend` with a Problem.
4. **Thickness measured vertically** in both ways (§11, Q1). At floor falls (0.5–3 %) the difference from the normal thickness is below 0.05 %.
5. **The reference stays the floor's `offset`.** `h = 0` is the floor's top as today; falls are negative (down to a drain), rises positive. The nominal thickness of a sculpted layer is its thickness where `h = 0`. This keeps doors, skirtings, walls and rooms that read the floor's level unchanged at the floor's held edges.
6. **A minimum thickness for the sculpted layer** (`minThickness`, default from §11 Q3). Where `t0 + h` would go below it, the layer stays at the minimum and its top (and everything above) stops following there; the floor reports a Problem with the area affected (*"Screed: held at its 30 mm minimum over 8.0 m²"*). Never refused silently, never negative.
7. **One idea of a slope**, shared with roofs and terrain: a `Slope` value type in `Lapin.Platform` (parse and format %, °, 1:N, mm/m; stored as a rise per unit run), and one slope arrow drawn by `Lapin.Drawing` (today `CivilModule.DrawSlopeArrow`, moved). The roof's slope markers, the terrain's tags and the floor's arrows then read and print slopes alike.
8. **Derived, never stored:** the triangulation, the layer bodies, spot levels at corners, the arrows' values, the quantities. Stored: only the user's control data.
9. **Extend, never break** (docs/plan-user-extensibility.md): the coded tools (slope by edge, drain with falls, room slope) are the default; the same control data can be drawn (a drafted polyline taken as a split line or a breakline) and typed with the project's expressions where a length is typed.

## 4. Data model

New attribute on the floor (`IfcSlab`), JSON, written to IFC in `Lapin_Intent` like every attribute (the intent set already writes attributes as JSON text, so it round-trips with no export code):

```jsonc
"surface": {
  "how": "layer",              // "bend" | "layer"
  "layer": 1,                   // the sculpted layer's index in the type's set (how = layer)
  "minThickness": 0.03,         // the sculpted layer's minimum (m)
  // S1: one plane, by an edge or by levels
  "plane": { "edge": 0, "slope": 0.015, "toward": "in" },          // falls 1.5 % from edge 0, inward
  // or "plane": { "points": [[0,0,0], [4,0,-0.04], [0,5,-0.02]] },  // 2 or 3 points [x, y, offset]
  // S2: control net (instead of plane)
  "points": [[x, y, h], …],                  // h relative; or {"x":…,"y":…,"level": 12.35} absolute
  "lines":  [[x1, y1, h1, x2, y2, h2], …],   // breaklines, linear between their ends
  "drains": [{ "x":…, "y":…, "depth": 0.02, "flat": 0.15, "id": "<mep element>"? }],
  "edges":  [{ "edge": 2, "h": 0 }],          // an outline edge held at h (default: all held at 0 when there are drains)
  // S3: regions and slopes from lines
  "regions": [{ "outline": [[…]], "roomId"?: "…", "offset": -0.02, "step": true }],
  "slopes":  [{ "from": {"edge": 0} | {"line": 3}, "slope": 0.02, "toward": "in", "until": {"offset": -0.03} | {"distance": 1.5, "along": "plan" | "slope"} }]
}
```

- **Points and lines in plan coordinates**, like the outline. An edge is referred to by its index, remapped when corners are added or removed exactly as layer extents are (`LayerExtents.RemapEdges`), so Edit sketch and corner grips keep the slope.
- **Absolute levels** (`level`) are stored as the user typed them and turned into `h` on every ask (`level − Slabs.Top`), so moving the storey or the floor's offset keeps an absolute point where it is, and a relative one with the floor.
- **A region linked to a room** (`roomId`) takes the room's outline on every ask, so it follows the walls as the room does.
- **A drain linked to an MEP element** (`id`, S4) takes that element's plan position on every ask.
- **Validation in the commands** (one undo step each): points outside the outline, a sculpted layer that is the type's only layer, a plane through three collinear points, `slope` ≤ 0 or above 100 % are refused with the reason. A point left outside by a later outline edit is kept and ignored, with a Problem (as layer extents do).
- **Storey copies, Copy, mirror, rotate** transform the plan coordinates with the outline (the existing transform of `Slabs` attributes, extended to `surface`).
- **Type changes**: `layer` is an index; when the new type has fewer layers or the index is the core, the floor falls back to `bend` with a Problem, and the user picks again.

## 5. Geometry

### 5.1 The height field

- **S1, a plane**: `h(x, y) = a·x + b·y + c`, from an edge and a slope (`c` so the edge is at 0, the gradient perpendicular to the edge, inward or outward), from two points (the slope along the line between them, level across it) or three.
- **S2, a TIN**: the outline's corners (at `h` of their edge, default 0), the points, the lines' ends, the drains (and a small square of `flat` size round each at its depth, a grate), triangulated by constrained Delaunay with the outline's edges, the lines and the drains' squares as constraints; `h` is linear on each triangle. The constrained Delaunay is today `Lapin.Civil/Tin.cs`; the architecture module cannot depend on civil, so S0 moves `Tin` (its triangulation and `ElevationAt`) to `Lapin.Geometry` and civil uses it from there, its tests unchanged (`spec-terrain.md` cases: 32 triangles on the 5×5 grid, `elevationAt(17,23) = 102.16`).
- **Arcs** in the outline are taken as their chords by `Lapin.Geometry/Chords` (one rule for every curve), so the TIN's boundary is the same polygon the body uses.
- **S3, regions**: each region is triangulated on its own; where two regions meet at different heights the surface steps (a vertical face), as a sunk shower tray; a region with `step: false` shares its boundary heights with its neighbour (a fold, no step).
- **Slopes from a line with a limit** (S3): compiled into control data before triangulating: the line itself at its `h`, and a parallel breakline at the distance where the slope reaches `until.offset` (or at `until.distance`, a distance along the slope converted to plan by `d·cos(atan s)`), beyond which the surface is level.

### 5.2 Building the layers

A new builder in `Lapin.Geometry`, `Prism.Between(footprint, holes, bottom, top)`: a closed solid over a polygon with holes whose bottom and top are height fields given as triangulations over the same plan mesh (top triangles, bottom triangles, vertical side faces along the outline and holes). For a plane both are one planar face each; for a TIN the faces are the TIN's triangles (coplanar neighbours merged, so a plane slope written through the TIN path gives the same faces as the plane path).

- `bend`: layer `i` between `top + from_i + h` and `top + to_i + h`.
- `layer` (sculpted index `k`): layers above `k` between `top + from_i + h'` and `top + to_i + h'`; layer `k` between `top + from_k` (flat) and `top + to_k + h'`; layers below flat, as today. `h' = max(h, minThickness − t_k)` (decision 6).
- A layer of thickness 0 follows the surface it lies on (decision 291 keeps it a line in sections).
- **Holes and openings**: the floor's own holes are holes of the footprint (no boolean, as today). Openings and provisions still cut a prism through the whole build-up; its z range is taken from the envelope's bounds, not from `top + section`.
- **Layer extents past the outline** follow the same `h`, extrapolated: a plane is a plane everywhere; a TIN is extended by holding the nearest edge's height (a Problem names the layer if that leaves a step).
- `Slabs.Envelope` (what the floor cuts lower priorities with) uses the same builder: the floor's whole build-up, top and bottom following.
- **Cost**: a flat floor goes the old path untouched; a plane floor is as cheap as a flat one (one face more per side, no boolean); a TIN floor of a bathroom is a few dozen triangles per layer. No booleans are added unless the floor already needed them.

### 5.3 What reads `Slabs.Top` today

Thirteen places outside `Slabs.cs` read the floor's top as one number. Each gets `Slabs.TopAt(m, slab, x, y)` (the shaped top at a point) or the envelope, decided one by one in S0:

| Caller | Reads | With a slope |
|---|---|---|
| `Walls.Extensions` (wall layers into floor layers) | the floor's top and bottom | from the layer bodies under the wall's footprint (lowest/highest), S1; exact cut by boolean when the wall ranks under the floor, S3 |
| `Faces.cs` (the finish painter's faces) | the top face | the shaped top's triangles |
| `RoomFinishes` (finds the slab under a room by its top within 0.6 m; the ceiling under the soffit) | top, soffit | the top at the room's centroid; the lowest soffit under the room |
| `Openings.cs`, `ArchitectureVoids.cs` (an opening's level in a floor) | top | `TopAt` the opening's centre |
| `LayerExtents.cs` | top | the builder above |
| `Analytical.cs` (structural surface at mid-thickness) | z | plane slope: an inclined analytical surface; TIN: refused for analysis with a Problem (S4 may mesh it) |
| `BeamBlock.cs`, `Lapin.Wood/Joists.cs` | top | a beam-and-block or joisted floor refuses `bend` (S1); `layer` on its topping/screed is allowed |
| `Lapin.Transport` (lift pit, floor at the landing) | top | `TopAt` the landing |

## 6. Drawings

- **Plan** (the floor lies below the cut, its outline drawn as today), drawn by the architecture module's plan marks (`ArchitectureModule.Marks`, beside `Drainage.Marks`), only for a floor with `surface`:
  - **slope arrows**: one per plane (S1) or per TIN face group of one gradient (S2: per facet of a drain's envelope), pointing down the fall, labelled in the project's slope unit (`1.5 %`), with the shared arrow (decision 7);
  - **spot levels** at the high and low points, the held edges' ends, every point, line end and drain, in the project's level format (`Standards.FormatLevel`), relative (`−20`) or absolute (`+3.18`) by a view option;
  - **split lines and breaklines** (thin, the "LEVELS" layer, as the terrain's), **region outlines** (S3);
  - **drains**: a small square (the `flat` grate) with a cross;
  - optionally (S4) **fall contours** every 5 mm, from the TIN (the terrain's contour code, shared);
  - all of it in Visibility/Graphics as one category, *Floor slopes*, on by default in architectural plans at 1:50 and larger, off at 1:100 and smaller (§11 Q8).
- **Sections and elevations**: nothing to do; they cut the layer solids (`SectionCaps`), so a tapered screed is hatched as a wedge and a bent slab as a sloped band. A test checks it (S1).
- **3D**: the bodies; while a floor is selected the control points show as grips (S2).
- **Schedules**: the floor schedule gains *Slope* (S1: the plane's slope; TIN: "varies") and, per layer, *Average thickness*, *Min* and *Max*.

## 7. Quantities

- **Per layer** (`Lapin_SlabLayerQuantities`): `NetVolume` from the solid (as today); `Width` stays the nominal thickness; new `AverageThickness` (volume / plan area of the layer), `MinThickness`, `MaxThickness`. A tapered screed's volume is therefore exact, and the cost module (which reads layer volumes) is right with no change.
- **Whole floor** (`Qto_SlabBaseQuantities`): `GrossVolume` becomes the envelope's volume (today area × width, the same for a flat floor); `GrossArea` stays the plan area, as IFC defines it; new `Lapin_SlabSlopeQuantities`: `SurfaceArea` (the shaped top's true area, for finishes and waterproofing measured on the slope), `Fall` (highest minus lowest `h`), `Slope` (plane only).
- A zero-thickness layer (a membrane) on a shaped surface measures the shaped area (it follows the surface; decision 291's rule keeps its area from the layer under it).

## 8. IFC

- **Body**: the faceted B-rep or face set the export already writes for every body (`IfcExport.Brep`); a plane-sloped floor is 6 faces per layer like a flat one. Considered and not chosen: an `IfcExtrudedAreaSolid` with an inclined direction (only a sheared prism, its edges not vertical, and only one plane), an `IfcBooleanClippingResult` of a flat extrusion by `IfcHalfSpaceSolid`s (fine for one plane, not for falls to a drain), `IfcAdvancedBrep` (no curved surfaces here). Q6 asks whether S4 should also write the clipped extrusion for single-plane floors, for readers that prefer parametric bodies.
- **Material**: the `IfcMaterialLayerSetUsage` stays (AXIS3, nominal thicknesses), which is what BubbleBIM and most readers read as the build-up. A floor with a sculpted layer is also written with its **layers as parts** (`IfcBuildingElementPart` per layer, each its body and material, aggregated in the IfcSlab, GlobalIds derived from the floor's and the layer key), so a reader that measures parts gets the screed's real volume. IFC4's `IfcMaterialLayerWithOffsets` (a layer tapered along one axis) fits only a single-direction taper and few readers use it: proposed for S4 only if the user wants it (Q6).
- **Properties**: `Pset_SlabCommon.PitchAngle` for a single-plane floor (the standard property for a sloped slab); the quantities of §7; `Lapin_Intent` carries `surface` as JSON, so re-importing a Lapin file rebuilds the definition, not a stored body.
- **Import**: a Lapin file's floor comes back from its intent. A foreign IfcSlab whose body is a planar sloped prism within the fidelity gate (1 mm or 0.5 %, `RecognitionFidelity`) may be recognised as a floor with a plane slope (S4); anything else stays a reference. An existing IFC file is never written to.
- **Other exports** (Fragments, glTF, USD, DXF of views) read the bodies or the drawings and need nothing.

## 9. Interactions and tools

**Walls attached to floors.** A wall stands at its storey level plus its base offset; a wall on a sculpted screed is unaffected (the screed is above its base or runs to it). Walls whose layers go down into the floor (`ExtendDown`) read the shaped layers (§5.3). A wall standing on a *bent* floor does not follow it by itself: that is the wall-bottom attach of `docs/plan-attach-to.md` (A8), which reads any body; this plan does not add a second mechanism (Q10).

**Rooms.** A room's volume and height stay the space's (storey + base offset); a sloped floor does not change them unless the user asks (Q11). The **room slope tool** (S3) picks a room and adds a region linked to it (`roomId`) on the floor under it, with a drain at the room's centre (or a clicked point) and falls from the room's outline held at 0; the region follows the room as it follows the walls.

**Finishes.** A room finish's floor layers (`LapinRoomFinish`, part floor) are laid from the room's level down; when they lie on a shaped floor they take its shape (`bend`, the same `h`), and a finish may itself carry `surface` with `how: layer` on its own screed (Q5: on the floor or on the finish). Skirtings stand on the floor's top at the walls; with the outline held at 0 (the default for drains) they are unchanged; where an edge is not held they follow `TopAt` along the wall.

**Openings in floors.** Shafts and stairwells cut through the whole shaped build-up (§5.2). A drain may optionally cut a small opening through the sculpted layer and the core (S4, with the MEP drain). Doors at a floor's edge keep their threshold at the storey level; a door on an edge that is not held gets a Problem on the floor (*"edge 3 is 12 mm below the door D04's sill"*).

**Split, merge, trim.** S1: a plane floor split by **Modify → Split element** gives both parts the same plane (the plane is global, so the parts stay continuous). S2/S3: points, lines, drains and regions go to the part they lie in, a line crossing the split is cut at it with its height there, as the terrain's split shares its spot levels (decision 670); merge gathers them.

**Ramps and roofs.** A ramp stays an IfcRamp; a floor's slope is for falls, not for circulation. A flat roof's tapered insulation is the same need (a sculpted layer to outlets): the builder and control data are shared later by the roof's flat shape (Q12), not in this plan's stages.

**Editing tools** (Architecture → Floor, a **Slope** group; and the floor's properties):

| Tool | Gesture | Stage |
|---|---|---|
| Properties → **Slope**: None / Plane / Shape; How: *Bend the floor* / *Sculpt a layer* (a list of the type's layers); Minimum thickness | fields | S1 |
| **Slope from edge** | select a floor, its edges show markers like the roof's slope markers (`slope` grips); click one, type the slope (`1.5%`, `2°`, `1:50`, `15 mm/m`) and whether it falls inward or outward | S1 |
| **Slope by levels** | click two or three points on the floor, type each one's offset or level | S1 |
| **Point** | click on the floor, type its offset (`-20`) or level (`=+3.18`); a grip shows the value; drag it, or double-click to type | S2 |
| **Line** (breakline, a ridge or a valley) | two clicks, an offset at each end | S2 |
| **Drain** | click; depth, grate size; the outline is held at 0, so the falls run from every edge to the drain (the four-facet "envelope" fall) | S2 |
| **Linear drain** (channel) | two clicks; depth; falls from the two sides | S2 |
| **Split line / Region** | draw a polyline across the floor (or pick a drafted one); each part gets an offset or a step | S3 |
| **Slope from line** with a limit | pick an edge or a split line; slope, direction, until an offset or a distance (in plan or along the slope) | S3 |
| **Room slope** | click a room; drain at its centre or a clicked point | S3 |
| **3D grips** on the control points (as the terrain's, capped near the view's centre) | drag vertically, type | S2 |
| **Reset shape** | right-click a floor | S1 |

Every command is one undo step, listed in the command catalogue (`node scripts/command-catalogue.mjs --check`), so Python, visual graphs and the MCP bridge get them; the floor slope commands are allowed to guests of a shared session (`EngineDispatcher.AllowedRemote`) like the other floor edits. Commands: `architecture.setFloorSlope {id, how?, layer?, minThickness?, plane?}` and `architecture.resetFloorSlope {id}` (S1); `architecture.floorPoint {id, add|move|remove…}`, `floorLine`, `floorDrain` (S2); `floorRegion`, `floorLineSlope`, `slopeRoom {roomId, drain?}` (S3). New strings in every language of `ui/src/i18n`; the guide's Floors page (`docs/user/floors-roofs.md` and `docs/user/ro/`) gains a *Slopes and falls* section with its scene in `scripts/user-docs/scenes.mjs`.

## 10. Stages and tests

Every stage ships on its own and leaves flat floors byte-for-byte as they were (a test exports a flat project before and after and compares bodies, quantities and GlobalIds).

**S0. Ground work (no visible change).**
- `Tin` moved from `Lapin.Civil` to `Lapin.Geometry`; the terrain's tests pass unchanged.
- `Slope` value type in `Lapin.Platform` (parse/format %, °, 1:N, mm/m), used by the terrain's tags and the roof markers' labels; the slope arrow moved to `Lapin.Drawing`.
- `Prism.Between` builder.
- `Slabs.TopAt`, and the thirteen callers of §5.3 routed through it (still flat).
- Tests: `Prism.Between` with flat fields equals `ExtrudeFootprint` (volume and face count) for a rectangle, an L with a hole and an outline with an arc; a plane field over 4 × 5 m from 0.20 to 0.30 thick has volume 5.0 m³; `Slope.Parse("1.5%") = 0.015`, `"1:50" = 0.02`, `"15 mm/m" = 0.015`, `"2°" = tan 2° = 0.034921`; formatting back to each unit.

**S1. One plane: by an edge or by levels, bent or a sculpted layer (the small, shippable stage).**
- `surface` with `how`, `layer`, `minThickness`, `plane`; `setFloorSlope`, `resetFloorSlope`; properties, edge markers, *Slope by levels*; arrow and spot levels in plan; quantities of §7; IFC of §8 (B-rep, parts for a sculpted layer, `PitchAngle`, intent); split carries the plane; storey copies, mirror.
- Tests (`FloorSlopeTests`), a 4 × 5 m floor (edge 0 from (0,0) to (4,0)), layers tiles 10, screed 60, concrete 200 (core):
  - *bend*, 1 % from edge 0 inward: top at (2, 5) = top − 0.050; every layer's volume as flat (screed 1.200 m³, concrete 4.000 m³, tiles 0.200 m³); `GrossVolume` 5.400 m³; `SurfaceArea` = 20 · √(1 + 0.01²) = 20.0010 m²; plan shows one arrow labelled `1.0 %` and spot levels ±0 / −50.
  - *layer* (screed), 0.5 % inward, min 30 mm: screed 60 → 35 mm, average 47.5 mm, volume 0.950 m³; concrete 4.000 m³ flat; tiles 0.200 m³ on the slope.
  - *layer*, 1 % inward, min 30 mm: the screed reaches 30 mm at y = 3 m; volume 4 · 3 · 0.045 + 4 · 2 · 0.030 = 0.780 m³; Problem *held at its 30 mm minimum over 8.0 m²*; the top at (2, 5) = top − 0.030.
  - by levels (0, 0) at 0 and (4, 0) at −0.04: falls 1 % along x, level along y; three points (0,0,0), (4,0,−0.04), (0,5,−0.02) give the plane through them.
  - refused: three collinear points; the core as the sculpted layer; a slope ≤ 0.
  - a section across y cuts the screed as a wedge 60 → 35 mm (the section's cut polygon areas).
  - IFC: the body's volume per part equals the solids' within 1e-6; `PitchAngle` = 0.5730° for 1 %; the re-imported floor has the same `surface` and volumes; a flat floor's export is unchanged.
  - split along x = 2: both parts keep the plane, the top continuous across the cut.
  - undo of each command restores the floor exactly; the edge index follows a corner added before it.
- Guide: *Slopes and falls* (EN, RO), scene `floor-slope`.

**S2. Points, lines and drains (a TIN).**
- Control net, drains with grates, linear drains, plan and 3D grips, typed offsets and levels, absolute levels kept through storey moves; plan arrows per facet; schedules' min/max/average.
- Tests, a 2 × 2 m floor, screed 50 mm sculpted, outline held at 0:
  - drain at (1, 1), depth 20 mm, no grate: 4 triangles; the drop volume is a pyramid 4 · 0.02 / 3 = 0.026667 m³, screed volume 0.2 − 0.026667 = 0.173333 m³, average thickness 43.33 mm; `TopAt(1, 1)` = −0.020, `TopAt(0.5, 1)` = −0.010.
  - linear drain from (1, 0) to (1, 2), depth 20 mm: two planes, drop volume 4 · 0.02 / 2 = 0.040 m³.
  - a grate 0.15 × 0.15 at the drain's depth: the drop volume grows by the grate's prism and the 4 frustum sides (computed in the test from the TIN).
  - a point with an absolute level stays at that level when the floor's offset changes by +0.1; a relative one moves.
  - a TIN that is in fact a plane gives the same faces and volume as S1's plane.
  - splitting the floor sends each point and drain to its part, a breakline crossing the cut is cut there with its interpolated height.

**S3. Regions, slopes from lines with limits, the room tool.**
- Split lines and regions (steps or folds), `slopes` with `until`, the room slope tool (`roomId` regions), merge, Problems for doors and skirtings on unheld edges.
- Tests: a 6 × 4 m floor split at x = 4, the part x > 4 at −0.02 with a step: a vertical face 4 m × 20 mm in the section, layer volumes reduced by 2 · 4 · 0.02 = 0.16 m³ on the sculpted layer; 2 % from edge 0 until −0.03: the plateau begins at 1.5 m in plan; `until.distance` 1.5 m along the slope gives 1.5 · cos(atan 0.02) = 1.49970 m in plan; a room moved by its walls moves its region and the drain stays at its centre; merging two sloped floors keeps both definitions.

**S4. Further.**
- MEP floor drain in the open `mep` module (IfcWasteTerminal, FLOORTRAP), linked by `drains[].id`, cutting its opening through the build-up.
- Fall contours in plan; slopes in visual graphs nodes and the `lapinbim` Python package (they come from the catalogue).
- IFC options of Q6; recognition of foreign planar sloped slabs within the fidelity gate.
- Wall bottoms on bent floors through `docs/plan-attach-to.md` A8; analytical surfaces for TIN floors.
- The roof's flat shape reusing the sculpted layer for tapered insulation (Q12).

## 11. Questions for the user

> **Q1. Thickness in a bent floor: vertical or normal to the slope?** Proposed: vertical (simple, exact faces, < 0.05 % off at floor falls). Normal matters only above ~10 %, where a ramp is the right element.
>
> **Q2. The reference and the nominal thickness.** Proposed: the floor's offset is its top where `h = 0` (the held edges), falls go down, and a sculpted layer's type thickness is its thickness there (so the screed is thickest at the walls and thinnest at the drain). The alternative is to give the nominal thickness at the lowest point and let the screed grow upward (the walls' finished level then rises above the offset).
>
> **Q3. Minimum thickness: default value and behaviour.** Proposed: 30 mm for a layer named or made of screed, else 10 mm; held at the minimum with a Problem (not refused). Should it be refused instead?
>
> **Q4. The default slope unit.** Proposed: % for floors (mm/m accepted when typed), ° for roofs, % and 1:N for the terrain, each a project setting, all through the one `Slope` type.
>
> **Q5. The room slope: on the floor or on the room finish?** When a room has a finish with its own screed (`LapinRoomFinish`), sculpt the finish's screed, or always the floor under the room? Proposed: the floor, unless the finish holds the screed, then the finish.
>
> **Q6. IFC for sculpted layers.** Proposed: B-rep body, the layer set usage, and the layers as IfcBuildingElementParts for every floor with a sculpted layer. Do you also want `IfcMaterialLayerWithOffsets` for single-direction tapers, and a clipped extrusion body for single-plane floors (S4)? Can you check what BubbleBIM prefers to read?
>
> **Q7. Steps between regions in a bent floor:** does the whole slab step (bottom too), or only the top (the core thickens)? Proposed: the whole slab steps in `bend`, only the sculpted layer in `layer`.
>
> **Q8. Plan display:** slope arrows and spot levels on by default in plans at 1:50 and larger, off at 1:100 and smaller? Relative or absolute levels by default?
>
> **Q9. Is S1 the right first release** (one plane, by edge or by levels, bent or sculpted), with drains in S2? Rychlas's bathroom case needs S2.
>
> **Q10. Walls on bent floors** follow only through the wall-bottom attach of `plan-attach-to.md` (A8), not automatically?
>
> **Q11. Rooms:** should a room's net height or volume follow a sloped floor under it, or stay the space's (storey + base offset)?
>
> **Q12. Flat roofs with tapered insulation** to outlets: a later stage of this plan, or of the roof plan?
>
> **Q13. ArchiCAD comparison:** I could not verify whether a recent ArchiCAD has a slab with a custom top surface; worth checking before S2's interface is fixed.

## 12. Risks

- **Thirteen readers of a flat top** (§5.3): each is small, but a missed one draws or cuts at the wrong level. S0 routes them all before any slope exists, and S1's tests cover walls into floors, openings and room finishes on a sloped floor.
- **Edge indices** under outline edits: reuse the layer extents' remap, with the same tests.
- **TIN quality** on outlines with arcs and holes: constrained Delaunay on chords; a sliver triangle near an arc is harmless for the body, but the plan arrows group facets by gradient, which must tolerate slivers (group within 0.01 %).
- **Cut order and booleans**: a shaped envelope cutting walls and beams goes the existing boolean path; a TIN envelope has more faces, measured on Bloc-P-4 with ten shaped floors per storey before S2 ships.
