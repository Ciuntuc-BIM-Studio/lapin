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

## Commands

`component.make {ids, name?}`, `component.makeUnique {id}`, `component.rename {id, name}`, the query `component.list`. Interface: Modify → Make component (CK), the group's section in Properties (Select instances, Make unique), Manage → Components.

## Not done

- Detail groups attached to a component (forum #224, Revit's term): sets of 2D drafting that travel with each instance and that a view shows or hides by set ("technical", "sales"). Today a plan view's drafting may be a member of a group, but a component's instances live on several storeys and views, so it needs its own design: drafting owned by the component and drawn per view at each instance, with a per-view choice of sets. A tag already follows the element it tags, in every instance, since members keep their ids, but it is placed per instance.

- Nested components, on purpose: an element belongs to one group, and selection and the edit scope rest on that (`docs/groups.md`). Letting groups nest changes both everywhere, for every group, not only for components.
- Scaled instances, on purpose: `Placement2D` is rigid, and only drafting knows a scale. A scaled floor or solid would have to say what becomes of its thickness, its height and its sketch's numbers.
- Sharing one body between instances on the screen or in IFC. Measured (`Many_instances_follow_an_edit_in_reasonable_time`, 09.11.2026): an edit carried to 200 instances of two members takes about 0.5 s in a Debug build, so it is not needed yet. If big repeated components prove slow, the shared-body mechanism of `docs/plan-instancing.md` is the next step.
- A solid sketched on a section, an elevation or a reference plane keeps sharing that sketch between its copies: it follows when the solid changes, while a move of the sketch itself moves them all.
