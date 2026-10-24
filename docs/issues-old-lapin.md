# Old Lapin's fixes and features, checked against lapin-pro (24.10.2026)

The source is `from old Lapin issues solved.md`, the old app's 23.10.2026 update.

Status: ✅ done, 🟡 partial, ⬜ to do, ➖ not relevant (cannot happen in lapin-pro).

## Views and graphics
- ✅ **Visibility/Graphics sets:** named sets in the project; a view picks one, keeps its own settings, or saves them as a new set. The VV dialog, also for 3D.
- ✅ **Linework:**
  - commands LW, LWTRIM, LWEXTEND, LWJOIN, LWRESET;
  - styles: invisible, show, thin, medium, wide, hidden, overhead, beyond, center, dotted;
  - a warning when an edited element has moved.
- 🟡 **Pen table 1–16 and object styles:** the engine has them; there is no UI and no pen table stored per project.
- ✅ **Hidden lines per view, with defaults by view type.**
- ✅ **Elements of the same material meet without a line;** the Joints option per material.
- 🟡 **Grid axes:** bubbles per axis are done; the ends are set per axis type, not per end of each axis; ends do not line up with each other.
- ⬜ **Base point and survey point in plans.**
- ✅ **3D hiding by category and IFC class;** VV/VG for the view in front. (IFC layers wait for layers on elements.)
- 🟡 **Full view range:** done in the engine; the UI needs a View Range dialog and must show its problems.

## Dimensions
- ✅ **Associative dimensions:** they follow moves and thicker walls, but a rotated element orphans them, and they hold only on cut edges.
- ✅ **Dimension ends on grid axes and wall centre lines.**
- ✅ **Driving dimensions:** typing a value moves the element.
- ✅ **Dimension strings:** editing one dimension pushes the elements after it.
- ✅ **Orphan dimensions:** drawn red; one-click freeing and a list are missing.

## Elements and types
- ✅ **Grids:**
  - Selection should be per axis, and Delete should remove that axis.
  - Grids should be pickable in drawing views and shown in 3D.
- ⬜ **Columns on grid intersections:** intersection snaps.
- ✅ **Deleting a type:** refuse while it is in use, or reassign its elements; clear the tool's type.
- ➖ **Grey layer colours after saving a type:** colours come from the materials.
- ✅ **Material names suggested in the type editors.**
- ✅ **Tab moves to the next field.**
- ✅ **Beam and column types show their section in the list.** (I/H profiles come with the steel profiles.)
- ✅ **Multi-selection edits:** one command with `ids`.
- ✅ **Rotate, move, copy, mirror elements;** copies do not share geometry (➖ in lapin-pro).
- ✅ **Create Similar, Match Type.**

## Schedules and lintels
- 🟡 **Schedule card drawings:** interior and exterior exist; plan and section, and the picker, are missing.
- 🟡 **Rows:** the order and the marking of overridden cells are missing from the UI.
- 🟡 **Sub-schedules and continuation sheets:** done in the engine; no UI.
- 🟡 **Duplicate checks:** done; the review list is missing.
- 🟡 **Lintel in the configurator:** the editor covers only the mode, bearing and height.
- ⬜ **Precast lintel products from a CSV.**
- ✅ **The lintel schedule.**

## Sheets
- 🟡 **Schedules wrap at the frame;** side handles to set the width are missing.
- ⬜ **Warnings past the frame or over the title block.**
- ⬜ **Snap guides while dragging,** arrow nudges of 1 and 10 mm, and double-click to open.

## Work
- 🟡 **Revit shortcuts:** TR, CS, MA, LW and VV come with their tools.
- 🟡 **Every tool a JSON command:** done; parameter schemas and an MCP bridge are missing.
- ⬜ **Romanian interface and a language choice.**
- ✅ **Reliable saves, crash recovery, a warning before leaving, undoable deletes.**
- ✅ **Two View buttons without actions:** Visibility and Visual style.
