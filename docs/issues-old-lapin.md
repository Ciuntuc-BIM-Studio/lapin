# Old Lapin's fixes and features, checked against lapin-pro (24.10.2026)

The source is `from old Lapin issues solved.md`, the old app's 23.10.2026 update.

Status: ✅ done, 🟡 partial, ⬜ to do, ➖ not relevant (cannot happen in lapin-pro).

## Views and graphics
- ✅ **Visibility/Graphics sets:** named sets in the project; a view picks one, keeps its own settings, or saves them as a new set. The VV dialog, also for 3D.
- ✅ **Linework:**
  - commands LW, LWTRIM, LWEXTEND, LWJOIN, LWRESET;
  - styles: invisible, show, thin, medium, wide, hidden, overhead, beyond, center, dotted;
  - a warning when an edited element has moved.
- ✅ **Pen table 1–16 and object styles:** the Object Styles dialog; the pen table is the project's.
- ✅ **Hidden lines per view, with defaults by view type.**
- ✅ **Elements of the same material meet without a line;** the Joints option per material.
- 🟡 **Grid axes:** bubbles per axis are done; the ends are set per axis type, not per end of each axis; ends do not line up with each other.
- ✅ **Base point and survey point in plans.**
- ✅ **3D hiding by category and IFC class;** VV/VG for the view in front. (IFC layers wait for layers on elements.)
- ✅ **Full view range:** the View Range dialog; its problems show on the plan.

## Dimensions
- ✅ **Associative dimensions:** they follow moves, turns and thicker walls.
- ✅ **Dimension ends on grid axes and wall centre lines.**
- ✅ **Driving dimensions:** typing a value moves the element.
- ✅ **Dimension strings:** editing one dimension pushes the elements after it.
- ✅ **Orphan dimensions:** drawn red, listed, freed in one click.

## Elements and types
- ✅ **Grids:** selected and deleted per axis, in plans and drawing views. (Axes in 3D: later.)
- ✅ **Columns on grid intersections:** the Columns at grid tool, and intersection snaps.
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
- ✅ **Rows:** chosen and ordered in the schedule's settings; overridden cells marked and reset.
- ✅ **Sub-schedules and continuation sheets:** tables in the settings; the next page placed on a new sheet from the sheet's warning.
- ✅ **Duplicate checks:** the list of elements to check, marked reviewed.
- ✅ **Lintel in the configurator:** the whole rule.
- ✅ **Precast lintel products from a CSV.**
- ✅ **The lintel schedule.**

## Sheets
- ✅ **Schedules wrap at the frame;** side handles set the width.
- ✅ **Warnings past the frame or over the title block.**
- ✅ **Snap guides while dragging,** arrow nudges of 1 and 10 mm, and double-click to open.

## Work
- ✅ **Revit shortcuts:** TR, CS, MA, LW, VV and the CAD aliases.
- 🟡 **Every tool a JSON command:** done, with the MCP bridge; parameter schemas are missing.
- ✅ **Romanian interface and a language choice.** (The CAD command line stays English.)
- ✅ **Reliable saves, crash recovery, a warning before leaving, undoable deletes.**
- ✅ **Two View buttons without actions:** Visibility and Visual style.
