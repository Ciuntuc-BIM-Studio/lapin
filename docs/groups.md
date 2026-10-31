# Groups, edit mode and clearing up in 3D (decision 204)

Asked for on 31.10.2026: an imported IFC file should stay a group, editable in an edit mode; selection tools in 3D (region, lasso) for clearing up, deleting and grouping; the screen's coarser copies (decision 188) for imports and links as well.

## Groups

- IFC's own: an `IfcGroup` entity (class registered by the core, kind `group`, no geometry) and `IfcRelAssignsToGroup` from it to its members (`Lapin.Platform/Groups.cs`). An element is in one group at most (`Relate` replaces), groups do not nest, only elements placed on a storey and not a link's may be members. A group with no members is taken out (`DropEmpty`).
- Commands: `group.create {ids, name?}` (groups among the ids are dissolved into the new one; "Group N" by default), `group.ungroup {ids}`, `group.remove {ids}`, `group.add {id, ids}`.
- A group id given to `element.delete` deletes it with its members; to `element.transform` it is its members (`Groups.Expand`), and a copy makes a group of the copies with the same name. A member deleted on its own leaves its group.
- IFC export writes `IFCGROUP` and `IFCRELASSIGNSTOGROUP` for every group with members written. IFC import does not read a file's groups back into Lapin groups (the file comes in as one group; its own IfcGroups stay carried data on its references, `IfcCarry`).

## Imports

`project.importIfc` takes `group` (true unless said otherwise; never for a link): what the file places anew comes in as one group named after the file (`Groups.Around`, inside the import's one undo step). The journal records `group` with the import, and a journal line written before groups (without it) is done again without one, so recovery rebuilds what was there. Fragments (`FragImport`) and mesh files (`MeshImport`) are grouped the same way. The IFC import dialog has **As one group** (on by default; hidden for a link). References read from IFC stay where the file put them (`element.transform` refuses them, as before): an imported group is cleared up and deleted, not moved.

## In the interface

- `ui/src/platform/groups.ts`: a pick of a member selects its group (`pickTarget`, plans and 3D, clicks and regions), the selection is drawn with the group's members lit (`shownSelection`); a double-click on a member opens the group (`openGroup`: the platform's edit scope, `scope.ts`), where members are picked one by one and the rest is faded and not pickable; Done or Esc closes it.
- Modify: **Create group** (GP), **Ungroup** (UG), **Edit group** (EG), **Remove from group** (`modules/core/groupTools.ts`). A group's properties: its element count, Edit group, Ungroup, Select members (`GroupSection`).
- View → **Select region** (SR, `viewport/select3d.ts`): a left drag in 3D draws a box, Space makes it a lasso, as plans do (`selectBox.ts`, `selectDrag.tsx`): to the right a window, to the left crossing. Elements are tested by their edges projected on the screen (their box when they have none), instances by their mesh box placed; an element partly behind the eye is never wholly inside a window. While it is on, the right button turns the view and the middle moves it.
- View → **Hide** (HH), **Isolate** (HI), **Show all** (HR) (`viewport/temporary.ts`): looks of their own source, the 3D view only, not saved, not undone.
- Fixed on the way: a look that made an element see-through (an edit scope, a faded phase) did nothing to an element already drawn, three.js not being told its material changed (`material.needsUpdate`).

## The screen's copies for imports and links

They already had them: references, shared bodies and linked elements are drawn through the same `Session.Render` and `MeshOfLocked` as Lapin's own, so every part of 256 triangles or more is sent with its coarser copies (`DisplayPrecisionTests.An_imported_mesh_gets_its_copies_too`). A model of many small parts (an ArchiCAD export) gains little from them (docs/plan-display-precision.md, Measured).

## Not done

- Nested groups; reading a foreign file's IfcGroups as Lapin groups.
- Moving an imported group (its references stay where the file put them).
- 2D: hiding for now and the region in drawing views other than plans.
