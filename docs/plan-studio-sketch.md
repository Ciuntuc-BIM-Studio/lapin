# Studio: sketching on faces in 3D, SketchUp's way and CAD's

Decision 374 (`docs/decisions-pending.md`), asked for by the user on 07.11.2026. It follows decision 272 (Rectangle, Circle and Polygon on face) and decision 312 (the Draft tab's commands in the 3D view).

## Goal

Drawing on an existing face, then pushing or pulling what was drawn, must feel natural to someone coming from SketchUp, and the CAD user must be able to use Lapin's own Draft commands (LINE, PLINE, RECTANGLE, CIRCLE, ARC, OFFSET, TRIM…) in 3D on the same faces. Both ways are possible and share one implementation underneath.

Before this, drawing for an extrusion from a face meant: Studio → Plane on face → a 2D view opens → the Draft tab → draw → back to Studio → Extrude → back to 3D. In 3D there were only Rectangle, Circle and Polygon on face (snapped to the face's corners and middles, no lines, no inference) and the Draft commands on a work plane (decision 312), without the face lit, without a way to end the sketch in a push/pull.

## What stays

- **Storage.** What is drawn on a face is a sketch (a drafted shape, `IfcAnnotation`) on the reference plane lying on that face: `studio.onFace` makes it, or finds the one made before for that face (`onFace` marks it with the element, named "<element>, <side> face"; the 3D view does not draw the plane, the face shows where it is). The solid made from it stays parametric: its profile is the sketch. In empty space the plane is the active storey's level, and the sketch is one of its plan's drafts (`plan:<storey>` or the storey's plan view), as decision 312 already stores what the command line draws there.
- **Push/pull.** A closed shape ends in the push/pull arrow (`beginAxisDrag`, decision 269): pulled out, a solid of its own on the face in the face's material; pushed in, a cut into the face's element; Esc or 0 keeps the sketch.
- **Sketches seen in 3D.** `Drafts.Geometry3d` (decision 312) already draws every drafted shape on its view's plane in 3D, so lines drawn on a face lie on it in the 3D view (View → Drafts in 3D).

## Two ways over one core

### The shared core (`ui/src/viewport/sketch/`)

- **The plane frame**: `WorkFrame` (`workPlane.ts`: origin, right, up, normal) for both ways. The direct way takes it from the face under the cursor (`frameOf(normal, outline)`, x along the face's longest edge), the CAD way from the reference plane (`frameOfView`); the engine converts world points through the plane's own frame, so both land on the same sketch.
- **Inference** (`inference.ts`), in the plane's (x, y): Endpoint, Midpoint, Intersection, On edge, the model's axes that lie in the plane from the last point (red x, green y, blue z; `axesInPlane`), Parallel and Perpendicular to an edge (magenta), else On face (On level in empty space). Locks: → red, ← green, ↑ blue (each only when it lies in the plane), ↓ parallel to the edge last inferred, the same key lets go; Shift held keeps the direction inferred now (`arrowLock`, `holdLock`). In the CAD way the command line's own aim (object snaps, ortho, polar, tracking) comes first, and the inference fills in where it finds nothing; the face's edges are given to both as snap lines.
- **The typed value** (`vcb.ts`), SketchUp's Measurements box: typed at any time without clicking a box, Enter applies it. `1200` a length along the way the cursor points (a circle's radius), `1200,600` a rectangle (also `1200x600`, `1200;600`), `r300` a radius, `<30` and `1000<30` an angle from the plane's x. Lengths in the project's unit unless they carry their own.
- **The follow-up** (`sketch3d.ts`): `pushPullShape` (a closed shape's push/pull arrow, on a face through `studio.onFace`, elsewhere `studio.extrude` of the sketch), `finishSketch`, `poseFacing`/`lookAtPlane`.
- **Engine** (`StudioModule.OnFace.cs`): `studio.onFace` with no shape returns only the face's plane (made or found: the CAD way's work plane is the very plane the direct tools store on); `open: true` keeps an open line on the face as a sketch (never pushed); `sketchId` pushes or pulls a closed shape already drafted on the plane (what the command line drew).

### 1. The SketchUp way (direct)

Studio → **Sketch** → Line, Rectangle, Circle while no work plane is active (`Sketch3D.tsx`, `direct.ts`):

- The plane is the face under the cursor, lit as the cursor passes (its outline asked once of `studio.faceAt`); in empty space the active storey's level. The first click locks it.
- Line: point after point; back on the first point closes the loop; Enter or Esc leaves it open (a sketch on the face); Backspace takes the last point back. Rectangle: two corners or `w,h`. Circle: centre, then the radius clicked or typed.
- A closed loop gets the push/pull arrow at once; Esc keeps it as a sketch. The tool stays on for the next shape (SketchUp's), Esc with nothing drawn or × ends it.
- Shortcuts: two letters as every Lapin tool (KL, KR, KC, KA; K for sketch: none of them was taken). SketchUp's one-letter set (L, R, C, A, P, F, T, M, E) clashes with Lapin's two-letter shortcuts and the command line, so it is a setting (stage C).

### 2. The work-plane way (CAD)

- **Sketch on face** (KF): a click on a face makes its plane the **active work plane** (`useWorkPlane`), lit, with a chip *Sketching on <face>* (Finish sketch, Look at plane, ×). **Sketch on plane**: the selected reference plane becomes the work plane. A work plane active, Studio's Line, Rectangle, Circle and Arc start LINE, RECTANGLE, CIRCLE and ARC on it, and every command typed in the command line runs there (`workPlaneView` puts the sketch's plane before the selection).
- The 3D view is the command line's host (`Draft3D.tsx`, decision 312): the cursor's ray meets the plane and the point is the plane's (x, y), aimed as on a plan; the commands are the same functions as in 2D, not rewritten.
- **Finish sketch** (KE, or Enter with no command running): the command is ended, lines drawn are welded (JOIN) and the newest closed shape drawn on the plane since the sketch began gets the push/pull arrow; nothing closed, the lines stay a sketch. Esc with no command running leaves the sketch (what is drawn stays).
- **Look at plane** (KP): the camera square to the plane, its face in the middle.

## Stages

### A: both ways for the basic shapes (done, 07.11.2026)

- The direct way: Line, Rectangle, Circle on the face under the cursor or on the level; the inference basics (endpoint, midpoint, intersection, on edge, the axes in the plane, parallel and perpendicular in magenta, arrow and Shift locks); the typed value; a closed loop → push/pull, an open line → a sketch on the face.
- The CAD way: Sketch on face / on plane, the face lit and snapped to, LINE, PLINE, RECTANGLE, CIRCLE, ARC on it (and every other command already working in 3D), the inference where the CAD aim finds nothing, Finish sketch → push/pull, Look at plane.
- The **Sketch** group in Studio's ribbon: Line, Rectangle, Circle, Arc; Sketch on face, Sketch on plane, Finish sketch, Look at plane.
- Engine: `studio.onFace` plane only, `open`, `sketchId` (`StudioOnFaceTests`). UI tests: `inference.test.ts`, `vcb.test.ts`, `direct.test.ts`, `sketch3d.test.ts`.

### B: faces split by lines, arcs and offsets, CAD edits in 3D (done, 07.11.2026, decision 375)

- A line drawn across a face (from edge to edge, or a closed loop of lines inside it) **splits it into regions**: Push/Pull on a region of a face with sketch lines takes the region round the point clicked (the smallest loop made by the face's outline and the sketch lines, the engine's `HatchRegion.At` over the plane's loops), pushed or pulled on its own as an `onFace` shape.
- **Arc** (two-point arc with its bulge, SketchUp's) and **Offset** (of a face's outline or a closed sketch, by a distance typed or dragged) in the direct way.
- The CAD edits on the work plane in 3D: OFFSET, TRIM, EXTEND, FILLET, MOVE, COPY, picked on the plane (the 3D host's pick of drafts by the plane's x, y).
- **Grips on sketch lines in 3D**: a sketch on the active work plane shows its points, dragged on the plane.
- **Snapping to other elements' edges**: the edges of the element under the cursor that lie in the plane (and the points of edges that meet it) as inference lines.

As built: `SketchRegions` (Studio) makes the planar graph of the face's outline and the plane's drafted shapes (split where they cross or touch, dangling ends pruned, faces traced by the sharpest left turn) and takes the smallest region holding the pick; `studio.faceAt` returns that region (so both ways light it), `studio.pushPull` pushes or pulls it through `studio.onFace` (`region: true` in the result). `studio.onFace {arc: {start, through, end}}` stores a three-point arc. The direct Arc and Offset (`direct.ts`: `bulgeOf`, `arcMiddle`, `offsetLoop`, `insideDistance`); TRIM, EXTEND, FILLET (and OFFSET with a sketch open) in the Sketch group run the command line's commands on the work plane; `SketchGrips3D.tsx` and `sketch/grips.ts` drag a drafted shape's points on its plane; `sketch/edges.ts` gives the bodies' sharp edges (20°) lying in the plane, and the points where edges pass through it, to both ways. Islands (a loop inside a region touching nothing) are regions of their own, not left out of the region round them.

### C: tape measure, Follow Me, the shortcut set

- **Tape measure** (T in SketchUp's set): a distance measured between two inferred points, and **guides**: a guide line parallel to an edge at the distance typed, a guide point; drawn dashed, snapped to, kept in the project (`LapinGuide`), never exported or printed; deleted together (Erase guides).
- **Follow Me**: a face picked, then an edge (or a chain of edges) of the model or a sketch: onto the existing Sweep (`studio.sweep`), the face's outline the profile (made a sketch on the face's plane), the edges the path.
- **The full shortcut set**: View → Interface… → *SketchUp shortcuts* (off by default): one letter in the 3D view with nothing typed — L line, R rectangle, C circle, A arc, P push/pull, F offset, T tape, M move, E erase, Space select; the two-letter shortcuts and the command line stay as they are (one letter then waits a moment for a second, as with commands sharing their letters).
