# Components

Asked for by a tester (09.11.2026, "work efficiently with instances … how SketchUp deals with components"); the user limited the first version to what stands on its own (decision in `docs/decisions-pending.md`): Studio solids, floors and rooms. Not native walls: their joins, hosted doors and lintels belong to their neighbours, which would make the problem much harder. A linked `.lapin` file (hotlink) is the way to repeat whole buildings or apartments with walls.

## The model

- `LapinComponent` (`Lapin.Platform/Components.cs`): the component, a name. It goes when its last instance goes.
- An **instance** is a group (`docs/groups.md`) with `component` and `componentXf` (`[x, y, z, degrees, mirrored]`: mirrored first, about its own x axis, then turned; the older four numbers read as not mirrored). It is therefore picked, moved, copied, turned and opened (double-click) as a group is.
- Its **members** stay real elements, each with `componentKey`, the same in every instance. Plans, sections, quantities, schedules and every export read them like any other element, and each keeps its own GlobalId.
- Only `Components.Takes` may be a member: Studio solids (`operation`), `IfcSlab`, `IfcSpace`. References from files and native walls are refused, with a message.

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

## Commands

`component.make {ids, name?}`, `component.makeUnique {id}`, `component.rename {id, name}`, the query `component.list`. Interface: Modify → Make component (CK), the group's section in Properties (Select instances, Make unique), Manage → Components.

## Not done

- Nested components, on purpose: an element belongs to one group, and selection and the edit scope rest on that (`docs/groups.md`). Letting groups nest changes both everywhere, for every group, not only for components.
- Scaled instances, on purpose: `Placement2D` is rigid, and only drafting knows a scale. A scaled floor or solid would have to say what becomes of its thickness, its height and its sketch's numbers.
- Sharing one body between instances on the screen or in IFC. Measured (`Many_instances_follow_an_edit_in_reasonable_time`, 09.11.2026): an edit carried to 200 instances of two members takes about 0.5 s in a Debug build, so it is not needed yet. If big repeated components prove slow, the shared-body mechanism of `docs/plan-instancing.md` is the next step.
- A solid sketched on a section, an elevation or a reference plane keeps sharing that sketch between its copies: it follows when the solid changes, while a move of the sketch itself moves them all.
