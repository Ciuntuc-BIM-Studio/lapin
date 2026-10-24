# CAD drafting: specification from the old Lapin

Read from the old app (`apps/viewer/src/drafting/`, `components/viewer/drafting/`) on 24.10.2026. Lapin-pro ports the behaviour, not the code.

The old app did not have:
- revision clouds, splines, ellipses, SCALE, STRETCH or CHAMFER;
- polar tracking (coded but not wired);
- general alignment guides (it had them only between grid axes).

Ortho and guide lines from points are new in lapin-pro.

## Entities

- A draft entity: `{id, viewId, layerId, shape, params}`, in the view's coordinates (m). Angles are in radians, counter-clockwise.
- **Shapes:**
  - `line {a,b}`, `polyline {pts, closed}` (a rectangle is a closed polyline), `circle {c,r}`, `arc {c,r,start,end}` (counter-clockwise);
  - `text {p,text,height,rotation}`, `leader {pts,text,height}`;
  - `dimension {aligned|linear, a,b,at,dir?,text?}`, `radial {diameter}`, `angular {c,a,b,at}`;
  - `level {p,value,ref?}`;
  - `hatch {loops, pattern, scale, angle, color}`.
- **Layer:** `{name, color, visible, locked, lineWeight (0.25), lineType: continuous|dashed[3,1.5]|dotted[0.3,1]|dashdot[4,1,0.3,1], group}`. The default layer is "0", colour #18181b.
  - Line type and line weight come from the layer.
- **Drawing order:** hatches, then geometry, then annotations.
- **Undo:** every edit is one undo step.

## Commands

| Group | Commands |
|---|---|
| Draw | `LINE L`, `PLINE PL`, `RECTANGLE REC`, `CIRCLE C`, `ARC A` |
| Modify (on a selection) | `ERASE E`, `MOVE M`, `COPY CO`, `ROTATE RO`, `MIRROR MI` |
| Edit | `OFFSET O`, `TRIM TR`, `EXTEND EX`, `FILLET F` |
| Annotate | `TEXT T`, `LEADER LE`, `DIMALIGNED DAL`, `DIMLINEAR DLI`, `DIMRADIUS DRA`, `DIMDIAMETER DDI`, `DIMANGULAR DAN`, `LEVEL LV`, `HATCH H` |

- **Draw:**
  - LINE and PLINE: `C` closes the shape, `U` undoes the last vertex.
  - CIRCLE: a typed number is the radius.
  - ARC: through three points.
- **Modify:**
  - COPY repeats.
  - ROTATE takes a typed angle in degrees.
  - MIRROR: `E` erases the source.
- **Edit:**
  - OFFSET: a distance, the pick, then the side.
  - TRIM and EXTEND cut or extend to the drafted shapes and to the model's lines.
  - FILLET takes a radius; 0 makes a sharp corner.
- **Annotate:**
  - Dimensions continue in a chain by default; `S` makes a single one.
  - A dimension's ends attach to model edges.

## Command line

- **Idle:**
  - A typed name starts a command.
  - `name.sub {json}` runs a JSON command.
  - Enter or Space repeats the last command.
- **Inputs while a command runs:**
  - free text;
  - a keyword (`^[A-Z][A-Z0-9_.-]*$`);
  - `x,y`, `@dx,dy`, `@d<deg`, `d<deg`, or `@` (the last point);
  - a bare number: a value, or a distance from the base point toward the cursor (with ortho).
- **Keys:**
  - F3 turns snaps on and off. F8 turns ortho on and off; holding Ctrl gives ortho while held.
  - Esc cancels; Delete erases.
  - Cmd+Z undoes; Shift+Cmd+Z redoes.

## Snaps and ortho

- **Order:** endpoint > intersection > midpoint > center > quadrant > perpendicular (from the base point) > nearest.
- **Tolerances:** snap 10 px, pick 6 px, grip 7 px.
- **Sources:** drafting, dimension lines and axes, and the lines of the generated drawing.
- **Ortho:** from the base point, on the dominant axis. A point that snapped is not moved by ortho.

## Grips and selection

- **Grips:**
  - line: its ends;
  - polyline: its vertices, plus a grip in the middle of each side that pushes the side square to itself;
  - circle and arc: the centre;
  - text: its point;
  - dimension: a, b, the line, and the text.
- **Selection:**
  - A click picks; Shift adds or toggles.
  - A drag to the right is a window (fully inside); to the left, crossing (touching).

## Tests (to port)

- **`snaps-input`:** lines (0,0)-(10,0) and (5,-5)-(5,5), a circle (20,0) r3:
  - (9.8,0.1) → endpoint (10,0); nearest only → (9.8,0);
  - (5.1,0.1) → intersection (5,0); (2.6,0.1) → nearest;
  - (23.1,0), tolerance 0.2 → center (20,0);
  - (20.05,3.05) → center wins; quadrant only → (20,3);
  - perpendicular from (3,7) at (3.1,0.05) → (3,0);
  - (50,50) → null.
- **The parser:**
  - `3,4`; `" -1.5 , 2. "`; `@2,-3` from (1,1) → (3,-2); `@` → (1,1);
  - `@2<90` → (1,3); `2 < 180` → (-2,0);
  - `2.5` and `-.5` are distances;
  - refused: `"" abc 1,2,3 1;2 "1," <5 1<2<3 @5 "1 2" 1e 0x10`, and relative input with no last point.
- **Ortho:** (5,1) → (5,0); from (1,1), (0,-4) → (1,-4).
- **`edit-ops`:**
  - **Offset:** a line ±1; a circle r2 by 0.5 → 2.5; an L shape mitred to (0,1),(3,1),(3,4).
  - **Trim:** (0,0)-(10,0) with cutters at x = 2 and 6, picked at 4 → two pieces; a circle → an arc from π/2 to 3π/2.
  - **Extend:** to x = 10.
  - **Fillet:** r1 at the corner (5,0) → arc centre (4,1); r0 makes a sharp corner; parallel lines or r50 → null.
- **`session`:**
  - LINE + `c` makes 3 lines.
  - PL `1,2`, `@3,0`, `@2<90` on a plan → (1,-2),(4,-2),(4,-4) (on a plan, user y = −drawing y).
  - Ortho + `5` toward (10,1) → (5,0).
  - ROTATE 90 → b = (0,-2).
- **`annotation`:**
  - aligned (0,0)-(3,4) at (-4,3) → 5.00, with its line from (-4,3) to (-1,7);
  - a linear chain: 3.00 / 4.00 / 5.00;
  - `R 1.50`, `Ø 3.00`, `90.0°`;
  - formatLevel: `±0.00`, `+3.06`, `−3.98`.
- **`styles`:** readableDeg: 0→0, 180→0, 90→-90, -90→-90, 135→-45, -135→45. formatDimension of 3.2456: `3.25` m, `3246` mm, `324.6` cm.
- **`measure`:** a line's length 5; a rectangle's length 10 and area 6; a circle's area π; an arc's length π and angle 90.
- **DXF:** ACI #000 → 7, rgb(224,80,80) → 1. Line weights: 0.25 → 25, 0.7 → 70, 0.33 → 35.
