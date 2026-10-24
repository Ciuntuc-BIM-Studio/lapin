# Direct editing, live input, CAD roles, cut order, Studio classes, the properties panel: plan

Asked on 25.10.2026. This plan starts from what the code does today; the open questions at the end need answers before the items they mark.

## What exists today

- **Grips.** A grip is `{key, at, move}` (`ui/src/platform/module.ts`), drawn and dragged only in the plan (`PlanView.tsx`), one at a time. Walls, beams and strip footings have their two ends, columns their point, drafted shapes their ends and vertices. Slabs, roofs, openings, platforms and terrain have none. No axis line is drawn on selection.
- **Joins.** Joined walls, beams and footings are linked by `IfcRelConnectsPathElements` (ATSTART, ATEND, ATPATH). `Joins.MoveEnds` moves the other element's joined end with the dragged one by default (`moveJoinedEnds`); T-joins do not follow.
- **Outlines.** Slabs, slab openings, roofs and platforms store a `contour`. Their commands replace the whole contour; no command moves, inserts or deletes one vertex. A roof also keeps `edges[]` by edge index (eave or gable, pitch, overhang). Terrain is the exception: `setTerrainPoint` edits one point.
- **Studio.** A solid keeps its `profile` and `guide` draft ids and is rebuilt from them on every change: the link is already live. One profile per solid; sweep takes one profile and one path; there is no loft between two profiles and no two-rail sweep. `studio.setClass` changes the IFC class of a solid.
- **Drafting.** A shape is an `IfcAnnotation` DRAFT with `shape`, `layer`, `color`, `hatch`. The name is automatic ("Line"). There is no role. The property section shows the shape in CAD terms (ends, length, angle, radius, area).
- **Live input.** While drawing, the plan shows the length of a two-point gesture and W × H of a rectangle; no angle, nothing editable. The command line takes `@dx,dy`, `@d<a`, a bare distance; BIM tools take no typed values.
- **Dimensions.** Aligned and linear, chained, associative to edges, grid lines and wall or beam axes, driving values on double-click. No angular, radial, diameter, arc length, slope or spot elevation; the ribbon's "Level" does nothing.
- **Cut order.** The engine has it: a class priority set in code (wall 50, slab 60, beam 80, column 90, footing 95) and an element's own `cutPriority`; higher cuts lower. There is no project table and no place to change it; roofs and Studio solids take no part.
- **Properties panel.** Module sections, then the class's declared fields; `element.update` refuses any undeclared key. No user parameters, no custom property sets.

## The work, in order

Each step ends with tests ported or written first, the checks green, and its row in `docs/decisions-pending.md`.

### E1. Grips as a shared mechanism

- A grip gets a kind (`end`, `vertex`, `edgeMid`, `centre`, `radius`, `move`) and an owner (element and index), so the view can merge, filter and draw them differently.
- **Selection rule.**
  - One element selected: dragging its end moves only that end. The joined element stays where it is.
  - Several selected: grips at the same point (within a tolerance) become one grip. Dragging it moves every selected element's end there in one engine command, so one undo step.
  - Today the engine's default moves joined ends too. The UI will pass `moveJoinedEnds: false` and a new `element.moveGrips` command will take the list. What happens to the join is question Q1.
- **Edge grips.** The middle of an edge moves the edge parallel to itself, keeping its neighbours' directions. Ctrl or Alt on an edge middle inserts a vertex. Ctrl on a vertex deletes it, if the shape keeps enough vertices.
- **Feedback.** While dragging: the rubber band, the length and angle of the moved segments, snaps, tracking, typed values (E5).
- **Views.** Plans first. Sections and elevations next, for heights and drafted shapes. 3D last, with grips on the active storey's plane (Q4).

### E2. Linear elements: the axis on selection

- Selecting a wall, beam, strip footing or grid line draws its reference line: the location line the element is placed by (core centre, left or right face). The reference line is dash-dot in the accent colour, with grips at its ends and a move grip in the middle.
- A wall's grips sit on its reference line, not on its faces, so a drag keeps the side the wall is drawn from, as ArchiCAD does.
- Columns get a centre grip and a rotation grip.
- Curved walls and beams are question Q3.

### E3. Surface elements: the outline on selection

- Selecting a slab, slab opening, roof, roof opening, platform or terrain outline shows the extrusion outline, with vertex and edge grips (E1).
- **One engine helper, `Contours.Edit`.** It does move vertex, insert vertex, delete vertex and offset edge on a contour, and keeps per-edge data in step: a roof's `edges[]` gets a copied entry on insert and loses one on delete, so the slopes stay with their edges.
- Each module gets one command, for example `architecture.editContour {id, op, index, at}`. Each command is one undo step and is tested against the existing contour tests.
- **Roofs.** The edge grip also shows the edge's slope arrow. A click on the arrow switches eave and gable. Typing gives the pitch.
- **Platforms and terrain.** The same helper works on the platform contour and the terrain's boundary and breaklines.

### E4. Draft and Studio: the source geometry drives the body

- Every drafted shape gets full grips:
  - arcs: ends, middle, radius;
  - polylines: edge middles and vertex insertion and deletion;
  - circles: four quadrant grips for the radius.
- **Selecting a Studio solid** shows the profile, holes, path or axis it is made from, with their grips. A drag edits the drafted shape, and the solid rebuilds (the live link exists). Plan sketches show in the plan. Section sketches show in their section, and in the plan as the cut line.
- The same rules as E1: dragging a shared vertex of two selected shapes moves both.

### E5. Live input while drawing and dragging

- **Dynamic input at the cursor.**
  - It shows two fields next to the rubber band: length and angle, the angle measured from +X counter-clockwise, as in AutoCAD.
  - Typing goes into the focused field, Tab switches fields, Enter takes the values, and an angle typed locks the direction.
  - A small arc shows the angle.
  - It works for walls, beams, footings, grid lines, sections, slab and roof outlines, and every CAD command.
- **An input bar above the view.**
  - It shows the same values in a form: length, angle, Δx, Δy, and x and y absolute or relative to the last point.
  - For walls it adds thickness and height; for an arc, radius and sweep.
  - It is the place to type with a keyboard when the cursor is elsewhere.
- **One parser for everything.** BIM tools and grip drags feed their points through the command line's parser, which already reads `@d<a`. Distances take units (`2.4`, `240cm`, `2400mm`) and simple sums (`3+0.25`).
- **One preview.** The plan preview of polylines and polygons shows each segment's length too, not only two-point gestures.

### E6. Names and roles for drafted shapes, and the sweeps they make

- **Names.** Every shape gets a unique name by default: kind plus a number per view (Line 12, Profile 3). The name can be edited in the properties header, and a duplicate is refused.
- **Roles.** A new `role` field, chosen in the properties panel:

  | Role | Use |
  |---|---|
  | `draft` | Construction lines, not printed |
  | `profile` | A closed shape to extrude, sweep or revolve |
  | `hole` | A closed shape cut out of a profile |
  | `path` | What a sweep follows |
  | `rail` | A second path |
  | `axis` | What a revolve turns about |
  | `endProfile` | The profile a sweep ends with |

  Drawing with a role on (a role chooser on the command line) tags the new shapes. The Studio commands then offer only shapes of the right role, and a sketch can list its parts.
- **New Studio operations.** Their order is question Q5.
  - Sweep with a start and an end profile: a loft along a path. Both profiles are resampled to the same number of points, and corners are matched by arc length.
  - Sweep along two rails: the profile is scaled and turned between the rails.
  - Loft through several profiles without a path.

  All stay live like today's solids and are tested on volumes and end faces.
- **Parameters on 2D and 3D geometry.** These are the user parameters of E10, shown on drafted shapes and Studio solids too.

### E7. More kinds of dimension

- Each kind is an engine annotation with its own drawing, associative where it can be, and printed in PDF and DXF:
  - angular, between two lines or three points;
  - radial and diameter, on circles, arcs and round columns;
  - arc length;
  - slope, as %, ° or 1:n (Q8), on a roof edge, a ramp or a drafted line;
  - spot elevation, the level mark on plans, sections and elevations, through the existing `Standards.FormatLevel`, which gives the ribbon's "Level" its job;
  - ordinate, from a chosen origin.
- Each kind is a command-line command too: DIMANGULAR, DIMRADIUS, DIMDIAMETER, DIMARC, DIMSLOPE, DIMLEVEL.

### E8. Cut order in Manage

- A **Cut order** dialog in Manage shows the table of classes with their priority and a short note on what cuts what. The project stores the table, and new projects start from these defaults:

  | Priority | Class |
  |---|---|
  | 95 | Footings |
  | 90 | Columns |
  | 80 | Beams and ring beams |
  | 70 | Lintels |
  | 60 | Slabs |
  | 55 | Roofs |
  | 50 | Walls |
  | 40 | Studio solids and proxies |
  | 10 | Furnishing |

- Roofs and Studio solids take part. A Studio solid takes its class's priority (E9).
- An element can still set its own priority in the properties panel, shown as "from the project" or "own".
- Whether priority can also follow materials, the way ArchiCAD's building materials do, is question Q6.

### E9. Studio solids: IFC class and type from the properties panel

- **The panel shows the class.** It lists the building-element classes Lapin knows (wall, slab, beam, column, footing, roof, stair, railing, covering, plate, member, furnishing, proxy…), plus a predefined type and, when there is one, a type object (`IfcBeamType`…). The panel writes through `studio.setClass` and a new `studio.setType`.
- **The class decides the rest:**
  - the quantities: generic solid quantities always (`NetVolume`, `GrossSurfaceArea`), plus each class's own `Qto_*` from the shape, such as length along the path for beams and members, top area for slabs and coverings, side area for walls;
  - the property sets: `Pset_*Common`;
  - the cut priority (E8);
  - the IFC export.
- Changing the class recomputes all four at once.

### E10. The properties panel, rebuilt

- **A grid with a spreadsheet's feel, in Lapin's style.**
  - Each row is a label cell and a value cell, separated by hairline grid lines.
  - Numbers are right-aligned in tabular figures, with the unit as a muted suffix.
  - Computed values are read-only and greyed. A value that differs from the type's shows a small mark.
- **Editing like a spreadsheet.**
  - A click selects a cell. Typing or Enter starts editing.
  - Enter commits and moves down; Tab and Shift+Tab move across; arrow keys move when not editing.
  - Escape reverts. Validation shows inline.
  - Values take units and sums (E5); copy and paste work between cells and elements.
- **Several elements selected:** a value they share is shown, otherwise "varies"; typing sets it on all of them, as one undo step.
- **Groups:** Identity, Type, Geometry, Position, Phases, Quantities, Property sets, Parameters. Groups collapse and remember it, and a filter field searches the rows.
- **User parameters:** "Add parameter" on any element, drafted shape or Studio solid, with a name, a kind (text, number, length, area, angle, yes/no, choice) and a value. They are stored in the element's `params` and exported as an IFC property set (Q7). They can also be defined on a type and inherited.
- **The panel's width** can be dragged, and is remembered.

## Order and size

| Step | Depends on | Size |
|---|---|---|
| E10 panel grid (without parameters) | — | medium |
| E1 grips mechanism | — | medium |
| E2 axes on selection | E1 | small |
| E3 outlines | E1 | medium (engine + UI) |
| E5 live input | E1 (drag feedback) | medium |
| E4 Draft/Studio grips | E1, E3 | small |
| E8 cut order | — | small |
| E9 Studio class and type | E8, E10 | medium |
| E6 names, roles, new sweeps | E4, E10 | large (geometry) |
| E7 dimensions | E5 (input) | medium |
| E10 user parameters | E10 grid | small |

I would start with E10 (the panel grid) and E1 together, because everything after them shows up in one or the other.

## Open questions

- **Q1.** One wall selected, its joined end dragged away: is the join dropped, or does the other wall keep its end and the join stay as long as they still touch? Proposed: keep it while they touch (today's `Refresh` does that); a corner pulled apart loses its join.
- **Q2.** Two walls selected but only one end dragged that is not shared: only that wall changes. Correct?
- **Q3.** Are curved walls and curved beams (arcs) part of this work, or later?
- **Q4.** Grips in sections and 3D: plans first, sections second, 3D later. Agreed?
- **Q5.** Which Studio operation first: sweep with two end profiles, sweep along two rails, or loft through several profiles?
- **Q6.** Cut order by class only (with per-element exceptions), or also by material and per type, as ArchiCAD does with building materials?
- **Q7.** User parameters on export: one property set `Pset_Lapin_Parameters`, or named groups of your own (each group its own set)?
- **Q8.** Slope dimensions by default in %, °, or 1:n? A different default for roofs and for ramps or terraces?
- **Q9.** Dynamic input: the angle from +X counter-clockwise (AutoCAD), or relative to the previous segment as an option too?
