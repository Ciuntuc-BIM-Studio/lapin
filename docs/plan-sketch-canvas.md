# One sketch canvas for every 2D board (audit S11)

The audit (`docs/audit-2026-11-15.md`, §6 S11) counted at least eight 2D drawing boards, each with its own tools, snaps, keys and
undo, and recommended aligning them: one shared board, or "edit in a view". The user approved it in stages: **stage 1** puts Draft,
the Studio sketch and the part sketch on one canvas; **stage 2** brings the symbol, title block, profile, wall profile and joinery
editors onto it, one at a time. This page is the design and what stage 1 built (decision 643).

## 1. The eight boards before

| Board | Files | Frame | Tools | Snaps | Constraints, dimensions | Undo | Enter / Esc | Stored as |
|---|---|---|---|---|---|---|---|---|
| **Draft** on plans, sections, elevations, reference planes, sheets | `viewport/cad/*` (session, aim, snaps, tracking, recorded, constraints), `PlanView`, `DrawingView`, `SheetView`, `modules/drafting` | the view's: a plan in the UCS (metres), a section or a plane in its own x, y, a sheet in paper mm | every CAD command (LINE … TEXT, HATCH, OFFSET, TRIM, FILLET…), the ribbon's Draft tab and the command line | object snaps, ortho, polar, tracking, the view's model lines | yes, on any view (`studio.addConstraint`, `ConstraintOverlay`, `SketchChip`), recorded while drawing in a sketch | one step per command | the CAD rule (`cad/session.ts`, `gestureKeys.ts`): Enter ends the step or repeats the command, Esc cancels | drafted shapes (`IfcAnnotation` DRAFT, `viewId`) |
| **Edit sketch on outlines** (floors, openings, ceilings, rooms, platforms) | `studio/outlineSketch.tsx`, `OutlineSketches.cs` | a plan view of its own (`outlineSketchOf`) | Draft's | Draft's | Draft's (corners held by coincidents) | one per command, Finish one | Draft's; a bar with Finish sketch / Cancel | `outlineSketch` on the element, its outline through the element's update command |
| **Studio sketch** on a face or a plane, two ways (decision 374) | `Sketch3D.tsx` + `sketch/direct.ts` (SketchUp's), `Draft3D.tsx` (the CAD commands on the work plane), `SketchGrips3D.tsx`, `sketch/inference.ts`, `sketch3d.ts` | a reference plane (`onFace`) or the storey's level, in 3D | the direct Line, Rectangle, Circle, Arc, Offset, Tape; the CAD commands on the work plane | the CAD aim plus SketchUp's inference (axes, parallel, perpendicular) | none in 3D | one per shape | Enter: Finish (push/pull); Esc leaves, lines stay; a chip "Sketching on" with Finish, Look at plane, × | drafts on the plane |
| **Part sketch** (decision 520) | `studio/PartSketchEdit.tsx`, `partSketchModel.ts`, `PartSketches*.cs` | the part's plane (xy, xz, yz + offset) through the placed part, in 3D | its own Line, Rectangle, Circle, 3-point Arc, Delete | its own: the sketch's points only | its own overlay (`LocalSource`), the constraint commands through a custom CAD host | one step per stroke (`partSketchSetShapes`) | Esc puts the tool down, then leaves | the part's definition: `sketches[].shapes/constraints` |
| **Symbols** | `core/symbols/SymbolEditor.tsx` (on the title block designer's canvas) | paper or model mm round the insertion point | line, rectangle, polyline, circle, arc, text, live text | its own (ends, corners, middles, grid) | none | its own stack, Save one step | its own | the symbol definition |
| **Title blocks** | `core/titleBlocks/TitleBlockDesigner.tsx` | paper mm | lines, rectangles, polylines, fills, hatches, texts, image, revision table | its own | none | its own stack (Ctrl+Z/Y), Save one step | its own | the title block template |
| **Profiles** (#128) | `core/profileLibraries/ProfileEditor.tsx`, `sectionEditor.ts` | section mm, centred by the engine | rings of points: drag, click to add, right-click to remove, starters | 1 mm / 5 mm steps | none | none until Make | none | a profile in the libraries |
| **Wall profile** (#100) | `architecture/WallProfileDialog.tsx` | the wall's elevation, start to end, mm | the top's points: drag, add, remove, starters | 10 mm steps | none | none until Apply | none | the wall's `profile` |
| **Joinery** | `architecture/JoineryDialog.tsx`, `JoineryPreview3D.tsx` | the type's elevation, mm | the grid of columns and rows, each panel's operation (a form, not drawing) | — | — | Save one step | — | the door or window type |

## 2. The shared canvas

**The canvas is a view drawn on with the CAD session** (`viewport/cad/*`): the Draft commands, the object snaps and tracking, the
grips, the constraint and dimension tools and their overlay, the constraints recorded while drawing, the Enter/Esc rule. It is
shown **flat** (`PlanView`, `DrawingView`, `SheetView` and their `CadOverlay`) or **over the 3D view on its plane** (`Draft3D.tsx`,
the cursor's ray met with the plane, the same commands). A board is not a canvas of its own: it is a view the canvas draws on and
a few words about it. That is "edit in a view", as Edit sketch on outlines already did, and what Draft has always been.

### 2.1 The contract (`ui/src/viewport/sketch/board.ts`)

A module registers a resolver (`registerBoard`): given a view, the `Board` it is, or null (a view no resolver claims is plain Draft:
the same canvas, no bar). A board says:

- **its frame**: `viewId`, the view's own (a plan in the UCS, a reference plane's x and y, a sheet's paper). A board whose payload
  has a frame of its own gets a view on that frame, so its coordinates are copied, never transformed: a part's sketch (u, v) are
  its reference plane's (x, y) for the placed part it was opened from.
- **its tools**: the Draft commands on its bar (`tools`; `SKETCH_TOOLS` = Line, Polyline, Rectangle, Circle, Arc, Ellipse, Offset,
  Trim, Extend, Fillet, Chamfer, Erase; a part's without Ellipse). The ribbon's Draft tab and the command line stay whole.
- **constraints on or off** (`constraints`): the constraint and dimension tools on its bar, the constraints recorded while drawing
  even before it has any (`recorded.ts sketchViews`), solved by `Lapin.Geometry/Constraints` through `SketchConstraints`; formulas
  name what the view's parameters are (`SketchConstraints.ParamsOf`: a part's board, its parameters with their defaults).
- **what Finish writes back** (`finish`) and **what Cancel drops** (`cancel`); `leave` for Esc (below).
- **how the result is stored**: the board's own payload, written by its own engine command at Finish. The drafts are the board's
  working copy only.

The bar over the view (`studio/SketchBar.tsx`) is the same for every board: whose sketch it is, the constraint chip, the tools, the
constraint tools, Finish sketch, Cancel; over 3D also Look at plane and Open in 2D, flat Back to 3D (a board on a plane can be
edited either way: the same view).

### 2.2 Undo: one step per command while editing, one at Finish

Each command on the board is its own undo step (a line, a trim, a dimension), as Draft's always were, and **Finish is one more step
that writes the payload** (and removes the board's view). Why not one step for the whole edit: Ctrl+Z while drawing must take back
the last line, as everywhere else in Lapin, and the CAD commands are engine commands each. Why not write the payload at every
command (as the part sketch did): the payload is checked as a whole (a part's definition is solved with its defaults, every placed
part rebuilt and refused when one does not build; an outline must be one closed loop), and a sketch is often not valid halfway
(an open outline, a dimension not yet given); placed parts would also rebuild at every stroke. Undo past Finish brings the board
back with its edits; Cancel drops them in one step.

### 2.3 Enter and Esc

A running command follows the CAD rule (`gestureKeys.ts`, `cad/session.ts`): Enter finishes its step, Esc cancels it. With no
command (`boardKey`): Esc lets the selection go first; then **over the 3D view** it leaves the board keeping its work (Studio's
sketch: the lines stay, as before; a part's: Finish), **flat** it does nothing (the plan is the board's until Finish or Cancel: Esc
is pressed to clear selections in plans all the time). Enter over the 3D view is Finish; flat, Enter keeps repeating the last
command (the CAD rule). Cancel is a button only: no key discards work.

## 3. Stage 1, as built

- **Draft** is unchanged: plans, sections, sheets are the canvas with no board.
- **Edit sketch on outlines**: its board (`studio/boards.ts outlineBoard`); its bar is the shared one (the same test ids:
  `outline-sketch-bar`, `finish-outline`, `cancel-outline`), with the tools on it. The outline's view records constraints while
  drawing from the start.
- **The part sketch** moved onto the canvas. `studio.editPartSketch {partId, sketchId}` (engine, `PartSketches.Board.cs`) makes a
  reference plane where the placed part puts the sketch (`partSketchOf`; the placed part hidden in its 2D drawing), drafts each
  shape (`partShape`: its id) and its constraints with their formulas and places; the UI makes it the 3D work plane and turns the
  camera to it (as before). Every Draft command, snap, grip, constraint and dimension works there; the constraint overlay is drawn
  on the plane in 3D (`studio/Board3D.tsx`); the instance's own sketch is drawn faintly behind when its values differ from the
  defaults (`PartSketch3D`). `studio.finishPartSketch` writes the shapes and constraints back (a shape keeps its id, so the bodies'
  outlines and holes still name it; a constraint brought from the part keeps its id; new ones take the next `pN`, `cN`), solved with
  the defaults, through `Commit` (refused, nothing changed and the board left open, when it does not solve, a body loses its
  outline, or a placed part's values would not build); an ellipse, a hatch, a text or a construction line is refused, naming it.
  `studio.cancelPartSketch` drops it. Esc, Tab, or leaving the part's edit mode finishes it. The old per-stroke commands
  (`partSketchSetShapes`, `partSketchDrag`, `partSketchAddConstraint`…) stay for scripts and the MCP bridge.
- **The Studio sketch** (on a face or a plane): its board (`faceBoard`) gives it the shared bar in 3D (Draft tools, Finish sketch
  with its push/pull, **Cancel**: what was drawn since the sketch began goes, one step; Look at plane; Open in 2D). The SketchUp-way
  direct tools (`Sketch3D.tsx`) and their inference stay as they are, beside it.
- **Storage**: nothing stored changes. A part's sketch is stored as before (the same ids; a sketch opened and finished unchanged is
  the same JSON), an outline's as before, the Studio sketch's drafts as before; no `IProjectUpgrade` is needed. GlobalIds: the
  part, its placed parts and the outlines' elements keep theirs (Finish updates them in place); the boards' views and drafts are
  working copies, removed at Finish. A project saved with a board open opens with it open (Edit sketch again resumes it). Curves:
  the drafts are the shapes themselves (arcs, circles), laid by `Chords` as every draft is.

Tests: `PartSketchBoardTests` (engine: opened where the placed part puts it, constraints and formulas kept, finished unchanged =
the same JSON; edits reaching the part in one step with ids kept, undo bringing the board back; Cancel; refusals), `board.test.ts`
(the key rule), the guide's `studio-part` scene (the sketch drawn with LINE on the canvas, Esc finishing).

## 4. Stage 2: the other boards, in order

Each becomes a board by the same contract: a view of its own on its frame (made by its module's `edit…` command), its payload
copied in as drafts, its own Finish command writing the payload back in one step, Cancel dropping the view. The editors' dialogs
keep their non-drawing fields (names, materials, attributes) beside the board.

1. **Wall profile** (`WallProfileDialog`): a section view along the wall, start to end (a drawing view of its own, `wallProfileOf`);
   the top drawn as one polyline (and arcs, which the dialog cannot do today); Finish → `architecture.updateWall {profile}`. The
   smallest step and the clearest gain (Draft's snaps to levels and to the other walls in the section).
2. **Profiles** (`ProfileEditor`): a drafting view in section mm (`profileOf`), the outside and each chamber a closed shape, the
   starters as drafted shapes; constraints on (a profile's sizes as dimensions, later as parameters); Finish → the engine's profile
   maker (refusing crossing rings, centring), then the libraries as today.
3. **Symbols** (`SymbolEditor`): a drafting view in the symbol's mm round its insertion point (`symbolOf`; paper or model scale), the
   texts as Draft's texts with their live tokens; Finish → the symbol definition (`SymbolInstances` save, one step). Its preview
   (turned, flipped) stays the engine's beside it. The title block designer's canvas then has one user less.
4. **Title blocks** (`TitleBlockDesigner`): a sheet-like paper view of the block's box (`titleBlockOf`), fills, hatches, texts, the
   image and the revision table as drafts and annotations a sheet already has; Finish → the template. The fields panel stays beside.
5. **Joinery** (`JoineryDialog`): its grid is a form, not drawing; what joins the canvas is a **custom frame or panel outline**
   (an elevation view of the type, `joineryOf`) for shapes the grid cannot make (arched heads, odd panes); Finish → the type's
   outline. The grid stays the default way (extend, never break).

Last, the board's undo as one group in the history (the edit's steps folded into one "Sketch" entry at Finish) could be offered;
today each step stays, as Draft's do.

## 5. Left after stage 1

- Grips of a constrained draft in 3D (`SketchGrips3D`) are solved by the engine on release (`drafting.updateDraft`), without the
  live solver preview the flat grips have (`studio.dragPreview`).
- A part's sketch edited on its board does not rebuild the placed parts until Finish (the faint instance sketch shows the values).
- Two boards on one part's sketch at once are one (opened again, the same view is answered); a part edited by other commands while
  its board is open is overwritten by Finish with the board's sketch.
- Fillet and chamfer in a constrained sketch still drop the corner's coincident (plan-outline-sketch.md, Left 1).
