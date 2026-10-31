# Studio: thick solids and work planes

Decision 186 (`docs/decisions-pending.md`). Asked for and approved by the user on 31.10.2026.

## Goal

Three Studio tools that give a drafted shape a thickness in its plane and a height off it, the way a modeller works with walls drawn anywhere: round a circle, round a rectangle, along a line. They work on every plane Lapin draws on: a plan, a section (a developed one too), an elevation, and a new **reference plane** placed anywhere in the model.

## The user's decisions

1. **On plans**, the tools make **native Lapin walls** (IfcWall of a wall type, with its layers, joins and openings) by default, of the current wall type, or a Studio solid when asked. **On sections, elevations and reference planes** they make **Studio solids classed IfcWall**; the user gives them a type afterwards (the Studio solid's **Type** in the properties panel, a `LapinSolidType` written as IfcWallType).
2. **Box**: by default only the walls round the outline. A **bottom** and a **top** are optional, each switched on separately with its own thickness, each a separate element (IfcSlab, a Studio solid). With walls and slabs the three are grouped in an **IfcElementAssembly**.
3. **Work planes**: a new **reference plane** object (through three points, on a face picked in 3D, or a view's plane moved by an offset), with a view of its own to draw on with Draft. The planes of sections and elevations can be drawn on directly: an elevation gets a plane (at the front of the model, its depth editable), a section has its cut, a developed section's sketch lies on the piece it is drawn on. Solids stay tied to their plane: moving or turning the plane (or the section line) moves them.

## Architecture

### Planes (platform)

- `WorkPlane` (`Lapin.Platform/RefPlanes.cs`): origin, Right, Up, Normal (`Right × Up`, towards the viewer), `World(Vec2)`.
- `Views.PlaneOf(model, services, view, atX?)` gives the plane of every kind of view:
  - **plan**: its storey's level, facing up (x, y the model's);
  - **section**: its line's first piece, standing, facing the viewer (`ViewSpec.Section`'s frame); **developed** (jogged, curved, along an element): the piece at station `atX`, its x running on from the station, so a sketch drawn on the second piece lands on it;
  - **elevation**: once it has a plane (`originX`, `planeDepth`): standing across its look, `planeDepth` along it from the model's origin, its x measured from `originX`. Before that an elevation's drawing moves with the model's bounds (its x origin is the bounds' middle), so it has no fixed plane. `Views.FixElevationPlane` (command `view.fixPlane`) fixes it where it is drawn now (nothing drawn on it moves), its depth at the front of the model; Studio does it the first time a solid is sketched on the elevation. `ViewSpec.Elevation(…, originX)` then measures x from there.
  - **reference plane** (`kind: "plane"`): `RefPlanes.Of`.
- **Reference plane** = a `LapinView` of kind `plane`, not exported to IFC, listed in the browser under **Reference planes**. Its place is six fields, editable in the properties panel: `x`, `y`, `z` (origin), `bearing` (the plan direction its normal faces), `tilt` (how far the normal rises: 0 standing, 90 lying face up), `spin` (its x turned about the normal from level). The level x direction is `(−sin b, cos b, 0)`, defined for every tilt (no singular case). Its drawing is a section on the plane (a hair towards the viewer, so faces lying on it are seen), looking against the normal, `depth` deep.
  - `view.create {kind: "plane", points: [[x,y,z]×3]}`: through three points (the first the origin, x towards the second);
  - `{point, normal, elementId?}`: on a face (the element's named face's normal when `core.faceAt` finds one);
  - `{fromView, offset}`: a view's plane moved along its normal.
  - `view.movePlane {id, by?, along?, turn?, about?}` moves it and turns it about the vertical.

### Thick solids (Studio)

- New Studio operations `ring`, `box`, `wall` (`studio.ring`, `studio.box`, `studio.wall`): `{profileId, thickness, height, justify, class?, offset?}`; a box also `bottom`, `top`, `bottomThickness`, `topThickness`. The source shape is the solid's `profile` (as every Studio solid), so grips, Edit sketch, copies and moves work as before. Default class IfcWall (a proxy that exports as IfcWall, as Studio writes classes it does not build).
- `Thick.Build` (`Lapin.Studio/Thick.cs`) makes the band in the sketch's plane:
  - circle: two circles (64 sides) of radius r ± t;
  - arc (counter-clockwise, its left towards the centre): the band between two arcs;
  - closed polyline: made counter-clockwise, offset outward and inward with mitred corners (`Offsets.Loop`); refused when the inner side folds;
  - line or open polyline: offset left and right with mitred corners, square ends (`Offsets.Open`); refused when a side is too short for its mitres.
  - justification: ring and box `inside` (default), `outside`, `centre`; wall strip `left`, `right`, `centre` (default), seen along the line as drawn (a closed polyline drawn clockwise has its left outside).
- The band is extruded along the plane's normal by the height (negative: away from the viewer, as Extrude). Geometry is read from the sketch, the view and the plane every time, so editing the shape, the section line, the elevation's depth or the reference plane rebuilds the solid with the same GlobalId (the session tracks what a build read).
- **Box bottom and top** (`Boxes.cs`): separate Studio solids `{operation: "boxSlab", box, side}` written as IfcSlab, filling the walls' inner outline, flush with the walls' bottom and top (the box's outside size is the outline × the height); their thickness is the box's `bottomThickness`/`topThickness`. `Boxes.Reconcile` makes or removes them as the box's `bottom`/`top` say and keeps an `IfcElementAssembly` (`objectType: Box`, IfcRelAggregates to the walls and slabs) while any slab exists. It runs on creation, on `element.update` (an `IElementUpdate`), on copy; deleting a box takes its slabs and assembly (`IDeletion`), deleting a slab switches it off on its box.
- Quantities: Length (the middle line), Height, Width for walls; Width and NetArea for the slabs.
- History: Studio solids keep their recipe as their own attributes, written to IFC in Lapin's own set `Lapin_Intent` (no `Pset_LapinSolid` in code; the name appears only in `docs/spec-studio.md`, describing the old Lapin).

### Native walls on plans (Architecture)

`architecture.wallsAlong {shape, storeyId, typeId?, height?, baseOffset?, justify, joinStyle?, bottom?, top?}` lays walls of the type along the drafted shape's JSON: a line or an open polyline a chain, a closed polyline a closed loop (counter-clockwise), an arc curved walls of at most a quarter turn each (`arc` rise), a circle four quarter walls. Justification moves the axis so the type's whole thickness lies on the chosen side (read from the type's bands on a probe wall), `inside`/`outside` meaning left/right of the counter-clockwise loop. Each wall is joined as it is laid (`Joins.Auto`), so corners mitre as hand-drawn walls do. A closed shape's bottom and top become native floors (`Slabs.Add`) at the wall base and top.

### IFC

- A ring, a box's walls and a wall strip: IfcWall; a box's bottom and top: IfcSlab; types: the Studio type as IfcWallType or IfcSlabType.
- `IfcExport.Assemblies`: an `IfcElementAssembly` entity of the model without a body is written as IFCELEMENTASSEMBLY (USERDEFINED, ObjectType "Box") in the storey, with IFCRELAGGREGATES to its parts; the parts are then not contained in the storey themselves.

### Interface

- Studio tab: group **Thick** (Ring, Box, Wall strip), group **Work planes** (Plane from view, Plane on face, Plane by 3 points).
- Command line: `RING`/`RNG`, `BOX`/`BOXWALLS`, `TWALL`/`TW`, `REFPLANE`/`RPLANE`. Keywords: I/O/C or L/R/C, B and T (box), N and S (native or solid on a plan). Native: the height; solid: the thickness, then the height (`ui/src/modules/studio/thick.ts` holds the decisions, tested).
- Face gestures accept `types: ['*']` (any element).
- Properties: the Studio section shows a thick solid's fields (thickness, height, justification, bottom and top); a reference plane's x, y, z, faces, tilt, spin and buttons to open, move and turn it; an elevation its sketch plane depth (`ui/src/modules/core/planeFields.ts`).
- Browser: **Reference planes** under each set.

## Stages

1. Planes: `WorkPlane`, `Views.PlaneOf`, elevation planes, reference planes. **Done.**
2. Ring, Box, Wall strip as Studio solids on every plane; box slabs and assembly. **Done.**
3. Native walls along a shape on plans. **Done.**
4. IFC assembly export. **Done.**
5. Interface, guide (`docs/user/studio.md` in six languages, scene `studio`). **Done.**

## What is done (31.10.2026)

All five stages. Tests: `StudioThickTests` (10: ring from a circle and an arc, three justifications; wall strip mitred, left/right/centre, quantities, rebuilt with its GlobalId, negative height; box walls inside/outside, bottom and top on and off, assembly, undo, delete; box on a section following its line; a developed section's piece; an elevation's plane and its depth; a tilted reference plane moved, raised and turned; planes on a face and off a view; native walls along a closed polyline (inside, outside, clockwise), an arc and a circle; IFC export with the assembly), `thick.test.ts` (7).

## Left for later

- Reference planes are not drawn in 3D or in plans (as a line or a rectangle); they are picked from the browser.
- Native walls are not linked to the shape they were laid along (they are walls of their own, edited as walls); Studio solids are.
- A box's bottom and top copied alone are not copied (they come with their box); re-importing Lapin's own IFC has not been checked for the thick solids, and does not bring the assembly back.
- An IfcWallType with layers cannot be given to a Studio wall (its type is a `LapinSolidType`, exported as IfcWallType).
