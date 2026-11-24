# Components

Asked for by a tester (09.11.2026, "work efficiently with instances … how SketchUp deals with components"); the user limited the first version to what stands on its own (decision in `docs/decisions-pending.md`): Studio solids, floors and rooms. Not native walls at first: their joins, hosted doors and lintels belong to their neighbours. Apartment types (`docs/plan-units.md`) solved that (joins inside carried, a stop at the limit that is the instance's own), and since forum topic #224 (24.11.2026, "work like Revit's model groups, walls too, for facade types and other repeated parts") every component takes walls the same way: the apartment type is now only a component named and listed as a flat. A linked `.lapin` file (hotlink) is still the way to repeat whole buildings read-only.

## The model

- `LapinComponent` (`Lapin.Platform/Components.cs`): the component, a name. It goes when its last instance goes.
- An **instance** is a group (`docs/groups.md`) with `component` and `componentXf` (`[x, y, z, degrees, mirrored]`: mirrored first, about its own x axis, then turned; the older four numbers read as not mirrored). It is therefore picked, moved, copied, turned and opened (double-click) as a group is.
- Its **members** stay real elements, each with `componentKey`, the same in every instance. Plans, sections, quantities, schedules and every export read them like any other element, and each keeps its own GlobalId.
- Only `Components.Takes` may be a member: anything of the project's own that stands on a storey (`Groups.Groupable`), walls included; what a wall hosts (its openings, doors, windows) comes with its wall and keeps its ids, found by `componentKey`. A door selected without its wall is refused, and so are references from files (`reference`, stored bodies), which stay where the file put them.
- Walls inside an instance join among themselves as the component says. Where one meets a wall outside every instance, the end stops in it (`Components.StopInnerWalls`, `Joins.Stop`): the outer wall is left as it is, the stop is the instance's own and never carried (`FollowsLimit`), and an instance about to move lets go of what stays (`Leaving`). Moved or copied members are not joined by `Joins.Auto` (`InstanceMemberIds`). The details are the apartment type's, below.

## How instances stay the same

`Session.Execute` calls `Components.Settle` after each command, inside the command's undo step:

1. It looks at what the command changed (`IEdit.EntityChanges`, `RelationChanges`): a member edited, a member joining or leaving (the group's relationship), or a member's sketch redrawn (`IElementPlacement.Dependents`).
2. An instance the command moved, turned, copied or just made is not an edit of its component, whatever happened to its members: its `componentXf` changed, or it is new.
3. For each instance edited from within, each other instance is made again: `ElementTransform.Run` copies the members by the rigid placement between the two (`Between`), `StoreyMove.Move` puts them on the instance's own storey, `ElementRise.Lift` makes up the difference in height, the old members and their sketches go, and each copy takes the id of the member it replaces (`Rename`, relations and attributes included), so GlobalIds stay stable.
4. A component left without instances goes.

Undo takes the whole step back. Only the commands go through `Settle`; imports and undo itself do not need it.

## What an instance can do

- Move, turn and copy on its own, also to other storeys (Copy to storeys).
- Mirror: it stays an instance, of the other hand. An edit in either reaches the other mirrored (`Between` gives a reflection when one of the two is mirrored and the other not, a turn otherwise), with a test on an asymmetric outline.
- `component.makeUnique`: a component of its own, with this one instance.
- Ungroup: its members become plain elements, the others stay instances.

## Apartment types

A component of `kind: "unit"` (`docs/plan-units.md`, `component.make {kind: "unit"}`, Modify → Make apartment type, CU): the same as any component since #224, named "Apartment type N", listed apart and written as `Lapin_Unit` in IFC (another component as `Lapin_Component`; both come back instances, without "As one group"). What a wall hosts comes with its wall and keeps its ids, found by `componentKey` as the members are. An edit carries only the members that changed; a join between one of them and a member that stayed is made again on the copy. Measured (`Many_flats_follow_an_edit_in_reasonable_time`, 09.11.2026): one wall edited, carried to 100 flats, about 0.5 s in a Debug build on a loaded machine.

What meets the building belongs to the instance (`docs/plan-units.md`, Limita). An inner wall's free end stops in the nearest building wall on its storey, cutting only itself (`Components.StopInnerWalls`, `Joins.Stop`, `stopsOn` on the join). This is looked for after each placing and when a building wall changes near the instance, and it is never carried: a join leaving the instance and an end following the building's wall are not edits of the type (`FollowsLimit`). A module keeps its own things right through `IInstanceMembers`: a room's doors and windows stand in the wall on their side wherever the instance stands (`RoomOpenings.Settle`), and what is done to them by hand is written on the room's list (`RoomOpenings.Edited`), so it is carried like any edit.

In IFC an instance is an `IfcGroup` with `Lapin_Unit` (its type's name and id, where it stands). It comes back an instance when the file is imported without "As one group" (`IfcImport`, `UnitInstances`).

## Detail sets (forum #224)

Revit's detail groups attached to a model group: named sets of 2D drafting that travel with each instance and that a view shows or hides by set.

- A **detail** is a member of an instance like any other: an `IfcAnnotation` (a drafted shape or hatch, a text, a symbol, a tag, a dimension) with `detailSet` (the set's name) and `viewId` set to `Components.DetailView` (`component-detail`), a view that is none of the project's, so deleting, duplicating or saving a view never touches it, and the 3D view leaves it out. `detailFrom` keeps the view it was drawn on.
- **Carried as members are** (`Settle`, `Carry`): each instance has its own copy, placed by `Between` (turned, mirrored), under its own ids kept through edits (`componentKey`); a tag's element and a dimension's points named after members are reworded to the instance's own. Copy to storeys copies an instance's details into its twin (`Components.CopyDetails`).
- **Drawn** by `Components.DetailsIn`, which `Views.Render` asks before the annotations: in a plan, the plan details of each instance standing on its storey whose set the view's graphics do not hide are put in an overlay model (`OverlayModel`) as if drawn on that view, so every module draws them with its own code (lines, hatches, texts, symbols, tags, dimensions), on screen, on sheets and in exports. A section or an elevation draws the side details (below) of each instance it sees on the side it looks at; a 3D view draws none.
- **Sides** (sections and elevations): a detail drawn in a section or an elevation is kept on a side of the component, `detailSide` (`front`, `back`, `left`, `right`: the plan direction the view looked along in the component's own frame, before the instance is turned or mirrored; the front is looked at northwards, `Components.Sides`), with its points as drawn and `detailFrame` [x0, y0, s], how the view it was drawn on mapped the side's own points (x along the side from the instance's point, y up from its level: its storey's plus its own height) to its drawing, x = x0 + s·x′, y = y0 + y′. Every instance's copy is the same, so bringing instances up to each other, and moving, turning or mirroring one as a whole, leave side details as they are (`Components.SideCarry`: no plan placement reaches them). A section or an elevation shows them on an instance when its look, taken into the instance's frame, is the side's within 0.08° (`SideMap`, giving the map to its drawing, s = −1 for a mirrored instance) and some of the instance lies within its depth and its ends (`Sees`); the points are carried there by the drafting module's own placement without an edit (`IElementPlacement.Carried`: texts stay readable, a mirrored one aligned the other way). Moved, turned or mirrored in such a view (`element.transform` with the view's `frame`), the view's placement is taken back to the kept points, so every instance follows.
- **Per view** (the one resolver): `view.graphics {id, detailSet, value: {hidden: true} | null}` writes `detailSets` in the view's graphics (or its graphics set, so a template rules all its views); `VisibilityGraphics.HidesDetailSet`. Shown by default. A detail never counts as another view's annotation (`ownAnnotationsOnly`).
- **Made**: `component.addDetail {id: instance, ids, set}` (Modify → Add to detail set, DS; the instance's properties), only 2D drafting drawn on a plan, a section or an elevation (one that does not look square at a side of the instance is refused); `component.make` puts drafting selected with the elements in the set `Details` (or `detailSet`). One undo step each. `component.list` and the Visibility / Graphics query list the sets.
- **Leaving**: a detail no longer in an instance (Remove from group, Ungroup) is plain drafting again, on its storey's floor plan (`Unkeep`); a side detail on the section or elevation it was drawn on, else the first that looks at that side of the instance it left, where that view drew it (`Unside`).
- Tests: `ComponentDetailTests`.

## Commands

`component.make {ids, name?}`, `component.makeUnique {id}`, `component.rename {id, name}`, the query `component.list`. Interface: Modify → Make component (CK), the group's section in Properties (Select instances, Make unique), Manage → Components.

## Not done

- A tag or a dimension in a detail set hung on an element outside the instance (a wall of the building) is not hung again on what stands by each instance: its copies point at the same element. Only what it names among the members is reworded to each instance's own.
- A side detail is edited in place by the Modify tools (move, rotate, mirror: `element.transform` with the view's frame) and by what does not place it (its text, layer, pen). A grip or a command that writes its points afresh (`drafting.updateDraft`) writes them as the view it is used in draws them, which is right only on the view and instance it was drawn on while that instance has not moved (its `detailFrame`); elsewhere the detail lands off. A developed section maps by its first piece; a section across an element, as any section.

- Nested components, on purpose: an element belongs to one group, and selection and the edit scope rest on that (`docs/groups.md`). Letting groups nest changes both everywhere, for every group, not only for components.
- Scaled instances, on purpose: `Placement2D` is rigid, and only drafting knows a scale. A scaled floor or solid would have to say what becomes of its thickness, its height and its sketch's numbers.
- Sharing one body between instances on the screen or in IFC. Measured (`Many_instances_follow_an_edit_in_reasonable_time`, 09.11.2026): an edit carried to 200 instances of two members takes about 0.5 s in a Debug build, so it is not needed yet. If big repeated components prove slow, the shared-body mechanism of `docs/plan-instancing.md` is the next step.
- A solid sketched on a section, an elevation or a reference plane keeps sharing that sketch between its copies: it follows when the solid changes, while a move of the sketch itself moves them all.
