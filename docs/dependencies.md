# What depends on what

Lapin's engine already knows which elements are built from which. This page says how, and how it is shown (decision 284).

## How the engine knows

Each element's geometry is built through a view of the model that records what it reads (`TrackingView`): the entities it gets, the relationships it follows, the classes it lists. The session keeps the reverse of that record, `_readers` in `Session.cs`: for every entity, the elements that read it.

After an edit, the session rebuilds the elements that read anything the edit touched (`Session.DiffOf`), and nothing else. Moving a wall rebuilds:
- the walls joined to it;
- its doors and windows;
- a wall attached to the roof over it.

The record is made again each time an element is built, and when a project opens. Nothing is declared by hand or saved apart from the model, so it cannot fall behind the code that builds the elements.

## The query

`core.dependents {id | ids}` answers:
- `dependents`: the elements built from the given ones (from the entity or its relationships), the given ones left out. They change when these change, and are rebuilt when these are deleted.
- `dependencies`, for one id: the entities it was built from, itself left out. That includes its type, its storey, its host, the walls it joins and its materials.

Reads of a whole class are left out of `dependents`. For example, the joins' search lists every wall, which says nothing about one wall in particular. The session builds whatever it has not built yet before it answers.

Modules reach the same through `IQueryContext.DependentsOf` and `DependenciesOf`, and an AI agent through the MCP bridge (`lapin_query`, `core.dependents`).

## In the interface

- **Properties → Dependencies**:
  - **Used by**: the elements built from the selected one, each picked by a click.
  - **Built from**: the elements it depends on, then its type and materials.
  
  The section lives in `ui/src/shell/DependenciesSection.tsx`.
- **Deleting**: Delete, ERASE and the panel's Delete go through `deleteElements` (`ui/src/engine/dependents.ts`). It asks for the dependents first, deletes, then names those still in the model, in a message.

Tests: `DependencyTests` (engine), `dependents.test.ts` (UI).

## Later

- Elements that take long to build could be rebuilt in the background, keeping their old body or their box until they are done. A module would need a way to send a result later.
- Bodies could be cached on disk under a hash of what they read, so a large project opens faster. To be measured on a large model first.
