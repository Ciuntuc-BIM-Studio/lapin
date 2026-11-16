# Outlines edited as sketches (forum topic 97)

Forum topic 97 (benlungu) asked to edit a slab's contour freely after it was made: add points, arcs, fillets, a round slab. The
user approved three stages: stage 1, corners made discoverable (decision 588); stage 2, curved edges, fillets and chamfers, the
outline tools' shapes (decision 589); stage 3, **Edit sketch** (decision 590). This page says what stage 3 is and what is left.

## Stage 3 as built

- `studio.editOutline {id}` (a floor, a floor opening, a ceiling) makes a plan view of the element's storey of its own
  (`outlineSketchOf`, the element hidden in it) and draws its outline there, one line or arc per edge, its corners held by
  coincident constraints; or, when the element's outline is still the one its sketch made, that sketch: its shapes and its
  constraints and dimensions (`outlineSketch` on the element, design intent: it goes to IFC in Lapin_Intent).
- The sketch is edited with what exists: the Draft tab's commands and the sketch constraints of Studio (the plan's
  `ConstraintOverlay`, the constraint tools, the solver in Lapin.Geometry/Constraints), each its own undo step.
- `studio.finishOutline {viewId}` chains the shapes into one closed outline (lines, arcs, polylines, or a circle alone; construction
  lines left out; straight pieces in line merged), writes it through the element's own update command (one undo step, the element's
  id and GlobalId kept, a floor's layer extents following its edges), keeps the sketch on the element and removes the view.
  `studio.cancelOutline` drops it. A reference kept from a file is refused; a recognised IFC element gets a sketch only when edited.
- UI: right-click → **Edit sketch** (and Studio → Edit sketch), a bar over the plan with **Finish sketch** and **Cancel**.

## Left

1. **Fillet and chamfer in a constrained sketch.** Draft → Fillet replaces the corner's two lines and adds an arc; the coincident
   that held the corner is dropped ("Constraint dropped: the point it held is gone") and the new arc is not tied to the lines. The
   outline stays closed as drawn, but a later dimension can pull the arc away from a line (Finish then says the sketch is open).
   Wanted: the drafting fillet in a constrained sketch adds coincident and tangent constraints at the arc's ends (as FreeCAD does).
2. **Edit sketch in 3D**, in place over the element (as `PartSketchEdit` does for a part's sketch), not on a plan of its own.
3. **Parameters.** A dimension of the outline sketch driven by a formula of the element's own parameters (as a part's are).
4. **Other outlines.** Done in audit S4 (decisions 623–627): drawn rooms, balconies, platforms and parcels take arcs; drawn rooms
   and platforms are edited as sketches; a sketch of several closed loops gives an outline and its holes; elliptical edges.
   Roofs keep straight edges by design (their edge rules are per straight edge); balconies have no Edit sketch (their first edge's
   order matters).
5. **Grip edits after a sketch.** An outline moved by its grips since its sketch was finished opens again from the outline, without
   its constraints; the sketch could instead be re-solved with the moved corners fixed.
