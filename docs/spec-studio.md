# Studio: specification from the old Lapin

Read from the old app (`~/Documents/GitHub/Lapin/ifc-lite`, branch `lapin`) on 24.10.2026. The old user doc is `~/Documents/GitHub/Lapin/docs/studio.md`, but it is out of date: it calls union and intersect "definitive", while the code keeps them live.

Studio is a ribbon tab for free-form solids:
- it turns 2D contours drawn on a view into IFC solids (Face, Extrude, Sweep, Revolve);
- each solid stays tied to its sketch, so editing the sketch rebuilds the solid with the same GlobalId;
- live booleans work between elements (Subtract, Union, Intersect, Show operands, Remove operation).

## Solids

- **FACE** (FACE, REGION, REG): a click inside closed contours makes a face from the smallest outline around it.
  - Its first-level inner contours become holes.
  - Islands inside holes become faces of their own.
- **EXTRUDE** (EXT): a closed polyline, circle (64 segments), face or region.
  - Holes are the first-level closed shapes inside it.
  - A typed number sets the depth (m); a negative depth goes away from the viewer.
  - A typed word sets the class:
    - WALL, SLAB/FLOOR, COLUMN, BEAM, MEMBER, PLATE, COVERING, ROOF, FOOTING;
    - PROXY → IfcBuildingElementProxy, FURNITURE → IfcFurnishingElement;
    - STAIR, RAILING, RAMP, CHIMNEY;
    - or any `IfcXxx`.
  - The default class is IfcBuildingElementProxy.
  - A contour that is already linked is refused.
- **SWEEP** (SW): pick a path (line, polyline, arc or circle), then a closed profile.
  - The profile stands upright on the path, with its bounding box's bottom centre on the path.
- **REVOLVE** (REV): pick a closed profile, then an axis line; the angle defaults to 360°.
- **History:** every solid records its history (`Pset_LapinSolid.Spec`):
  - `{operation:'extrude', outer, holes, depth, location, axis, refDirection}`;
  - `{operation:'sweep', profile, path}`;
  - `{operation:'revolve', profile, origin, direction, angle}`.
- **Properties card:**
  - shows the sketch (its view, outline and number of holes), the net face area, and the path or axis (marked "missing" in red when gone);
  - edits the depth, the angle (|a| ≤ 360) and the class;
  - has an Edit sketch button.
- **Opening the sketch:** a double-click on the solid, or Edit sketch.
  - It opens the view the sketch lies on.
  - When there is none, it creates a section "Sketch {name}" on the sketch's plane and redraws the sketch there from the history.
  - Walls, slabs, beams, columns, doors and windows are refused: "edited with its own tool".

## Booleans

- **Selection:** the first selected element is the target; the others are the tools, added in order with Shift-click.
  - All must be in one model.
  - Each operation is one undo step.
- **Subtract:** writes `IfcRelInterferesElements` (target, tool, 'Subtract', ImpliedOrder true).
  - Both elements stay and stay editable; the cut follows them.
- **Union / Intersect:** live.
  - The target's body is the result; the tools are hidden and still editable through Show operands.
  - A change to either side rebuilds the result. Rebuilding twice is not cumulative.
  - On export the tools are left out, so the union is written once.
  - A tool that already takes part in a combination is refused, and so is a target that is another's tool.
- **Remove operation:** separates the elements and shows the freed tools again.

## Tests (to port)

- `studio-feature.test.ts`:
  - Fixture: a 100×100 slab (depth 0.2) with a 40×40 hole at (10,10), which holds a 10×10 island.
  - Holes are first level only: `['hole']`.
  - The extrusion reads back as extrude, 0.2, IfcSlab, with sketchIds `[plate, hole]`.
  - Revolve: angle 90; a missing axis gives the guide null.
  - A hip roof is not a Studio feature.
- `element-boolean.test.ts`, `element-boolean.e2e.test.ts`:
  - Walls: A (0,0)→(4,0), thickness 0.2, height 3; B (2,-1)→(2,1), thickness 0.4, height 5.
  - Subtract: cuts at x = 1.8 and 2.2, and removing it makes A whole again.
  - Union: top 5, x from 0 to 4. Raising B to 6 gives top 6, and separating gives 3 again.
  - Intersect: x from 1.8 to 2.2, top 3; with B 1 m thick, x goes from 1.5 to 2.5.
  - Undo brings back one combination, then none.
- `solid.e2e.test.ts`: a block with NetVolume 0.875 = (2−0.25)×0.5 in `Qto_BuildingElementProxyQuantities`.
