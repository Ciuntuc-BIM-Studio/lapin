# Overrides and replacements

For edition authors. An edition kept in a sibling repository (`products/<id>.json` with `"external"`, as `lapin-urban` or `lapin-mep`) may add modules, and it may also **replace** what Lapin's modules do: a command, a query, a tool, a dialog, a view, a File action. The hooks below are generic; no module of this repository uses them, and tests fail if one of the `lapin` edition's does (`OverrideTests.The_lapin_edition_overrides_nothing`, `ui/src/platform/editionReplaces.test.ts`). Users never see them except in Help → About, which lists what the edition's modules replace when anything is.

## Engine: command and query overrides

In `Register`:

```csharp
r.Override("architecture.addWall", (ctx, args, next) =>
{
    var mine = (JsonObject)args.DeepClone();
    mine["height"] ??= 2.8;              // change the args
    var result = next(mine);             // the command as it was (or the override before this one)
    result!["by"] = "litestep";          // change the result
    return result;
});

r.OverrideQuery("core.faceAt", (ctx, args, next) => next(args));
```

- By the full id (`module.name`). An id no module of the product registers fails when the product loads (`ModuleCatalog.Create`), naming the overriding module.
- Order: overrides wrap in load order, whichever module loaded first; the last loaded is the outermost. `next` calls the one before it, the first's `next` the original.
- An override may skip `next` (replace the command), call it more than once, or throw a `CommandException` (a refusal the user reads).
- Everything happens inside the command's own transaction: one undo step, under the original's label; a throw rolls all of it back.
- The command keeps its id, label and owner (`CommandEntry.Module`), so what a guest of a shared session may call (`EngineDispatcher.AllowedRemote`, `SiteCommands`), what may touch linked elements and what the interface lists stay as they were. Validation is the original's: it validates whatever args `next` gives it.
- `edit.batch` runs the overridden command too (it reads the catalog).
- No override: the catalog holds the handlers as registered; the command path is unchanged.
- `ModuleCatalog.Overrides` lists them; `session.info` sends them as `overrides` only when there are any.

## Engine: driven elements

For modules that drive elements from a specification of their own (a generator, a premium configurator whose output is rebuilt from its inputs): their elements are ordinary native elements, so the generic commands would act on them as on any other and the next rebuild would undo the edit. Two services keep them right; neither is used by a module of this repository.

```csharp
public void Register(IModuleRegistry r)
{
    var mine = new MyDriven();
    r.Service<IDrivenElements>(mine);
    r.Service<IIfcExportFilter>(mine);
}

sealed class MyDriven : IDrivenElements, IIfcExportFilter
{
    public bool Drives(IModelView m, Entity e) => /* one of mine: a member, or a group I own */;

    public void Before(ICommandContext ctx, DrivenEdit edit, DrivenOutcome outcome)
    {
        // edit.Command, edit.Ids (only mine), edit.Args; element.transform also gives Transform, Up, Copy; Copy to storeys,
        // Move to storey and Duplicate storey give Storeys.
        if (edit.Command == "group.create") throw new CommandException("… is its generator's.");   // refused
        foreach (var id in edit.Ids) { /* the edit done in my terms */ outcome.Handled.Add(id); }    // done here
        // outcome.Made: what I made in their stead (a copy), returned with the command's ids
        // nothing done: the command goes on as it is
    }

    public void After(ICommandContext ctx, IEdit edit, string command) { /* edit.EntityChanges written back into my spec */ }

    public bool SkipsGroup(IModelView m, Entity group) => /* my own group: not written to IFC */;
    public IEnumerable<string> PrivateKeys => ["myKey"];   // never written in Lapin_Intent
}
```

- **`IDrivenElements.Before`** is asked by `element.delete`, `element.transform` (and so `element.array` and every copy made through it), `element.update`, `element.rename`, `element.setParam`, `element.removeParam`, `group.create`, `group.add`, `group.remove`, `group.ungroup`, `component.make`, `component.makeUnique`, `element.moveToStorey`, `element.copyToStoreys` and `storey.duplicate` (with contents), before they act, with the ids given that the module drives (`Driven.Before`). A group the module owns is given whole: moved, copied or ungrouped, it is the module's to say what that means (a placement changed, a new instance of the same spec, the link dropped).
- Three answers, as pinning (decision 449) and `r.Override` give them: let it go (do nothing), refuse (a `CommandException`, which rolls the whole command back), or do it in its own terms and add the ids to `outcome.Handled`, which the command then leaves out. Ids it does not drive go on as before, in the same undo step.
- **`IDrivenElements.After`** is called after every command (`Session.Execute`, before components settle), inside its undo step, with the command's changes: what any command did to a driven element (another module's command too: a door's type, its place along its wall) is written back into the module's spec there, so building it again keeps it.
- **`IIfcExportFilter`**: the export leaves out the groups `SkipsGroup` names (no `IFCGROUP`, no `IFCRELASSIGNSTOGROUP`; the members are written as any element) and never writes the `PrivateKeys` attributes in an element's `Lapin_Intent` set, so a link that lives in `.lapin` stays there.
- A grid a module drives (a generator's own IfcGrid) never takes the axes drawn with *Grid axis* or *Grid…*: `Grids.Ensure` skips it, so they go to the project's own grid (made when there is none). Its axes moved with the axis tools reach the module through `After`.
- Without a module that registers them, every command runs as it did. Tests: `DrivenElementsTests` (a test generator).

### Content of an absent module (decision 484)

A project may hold what a module this edition lacks wrote: a class of its own (kept by the package under its module and written back as it came, as before) and an entity of a platform class it owns, such as a group it drives. Such an entity says so with `ownerModule` (`ModuleOwned.Key`) set to the module's id:

```csharp
attrs[ModuleOwned.Key] = "mymodule";   // on the group (or other entity) the module owns
```

In an edition without that module, the platform's own `AbsentModules` service (registered after every module, an `IDrivenElements` and an `IIfcExportFilter`) keeps it **as it is**:

- its members are ordinary elements: moved, changed, renamed, deleted, copied (a copy is a plain group of plain copies), sent to another storey, given doors and windows;
- the group itself is not renamed, changed, ungrouped, filled or emptied by hand (`group.*`, `component.make*`, `element.rename/update/setParam` on it are refused, saying which module it belongs to); it is never dropped as empty (`Groups.DropEmpty`, `element.delete`), so the module finds it again;
- an IFC export leaves it out (its members are written as any element);
- `Session.AbsentModules()` names the modules the project needs and this edition lacks; `project.open` and `project.openBytes` return them as `absent`, and the interface says so once.

Opened again with the module, the module compares what it drives with what was done meanwhile (its own rule). Tests: `AbsentModuleTests` (a test module that owns a group and a class of its own).

## Engine: results delivered later

A module that works something out in the background (an analysis, a call to a model, a long solve) takes a stamp of the model in its command or query, starts the work and returns at once:

```csharp
r.Command("analyse", "Analyse", (ctx, args) =>
{
    var deliveries = ctx.Deliveries!;
    var stamp = deliveries.Stamp();
    var ids = ctx.Model.OfClass("IfcWall").Select(w => w.Id).ToList();
    _ = Task.Run(() =>
    {
        var result = Work(ids);                                   // off the session's thread: read nothing live here
        deliveries.Deliver("Analysis", stamp, edit => Write(edit, result), reads: ids);
    });
    return null;
});
```

- `Deliver` may be called from any thread. The session queues it and raises `DeliveryWaiting`; the host applies it between calls (desktop: `EngineHub.Deliveries` under the calls' gate; browser: after the call running, or at once between calls).
- Applied as **an undo step of its own**, named by its label, by the author whose call took the stamp (their own undo in a shared session), journalled like any edit and sent to every interface as an ordinary diff. It is never merged into the command that started it: that step may have been undone or followed by others meanwhile.
- **Stale** deliveries are dropped, not merged: when a project was opened or made since the stamp, or when the model changed since it. With `reads`, only a change to one of those entity or relationship ids counts. A stamp taken inside a command stands after that command's own edit.
- A delivery that throws is rolled back; one that changes nothing makes no step. The desktop host tells its own interfaces what became of each (`{"event":"delivery","data":{"label","status","error"}}`, status `applied`, `empty`, `stale` or `failed`).
- The stamp is taken on the session's thread; the work must not read the live model from its own thread (copy what it needs first).

## Interface: `replaces`

On a `LapinModule` (`ui/src/platform/module.ts`):

```ts
replaces: {
  tools: { 'architecture.wall': (orig) => ({ ...orig, label: 'Wall (AI)', complete: async (input, ctx) => { /* … */ await orig.complete?.(input, ctx); } }) },
  dialogs: { wallTypes: (Orig) => MyWallTypes },          // Orig: the dialog it replaces, or null
  views: { gantt: (orig) => ({ label: 'Schedule', component: MyGantt }) },
  fileActions: { 'file.save': (orig) => ({ ...orig, run: async () => { await check(); return orig.run(); } }) },
}
```

- Tools by `ToolDef` id: replaced wherever they are (tabs, shared groups, quick tools), one object per id, and the id is kept (keys, quick slots and workspaces name tools by it). An unknown tool id throws at load.
- Dialogs by the store's `dialog` key, a module's or the shell's (`share`, `join`, `interface`, `quickTools`, `shortcuts`, `palette`, `optimizeIfc`); views by a pane's module view kind. Both may stand where nothing was (their original is `null`), and are made once.
- File actions by id (`ui/src/shell/fileActions.ts`): `file.new`, `file.newFromTemplate`, `file.open`, `file.save`, `file.saveAs`, `file.saveVersion`, `file.saveTemplate`, `file.importIfc`, `file.importFragments`, `file.linkIfc`, `file.exportExchange`, `file.optimizeIfc`, `file.importDxf`, `file.importMesh`, `file.exportMesh`, `file.share`, `file.join`, `file.startScreen`, `file.leave`. The keyboard's New, Open, Save and Save as run the same actions. An unknown id throws.
- Several modules replacing one id chain in load order, each given the one before it.
- Resolved in `ui/src/platform/modules.ts` (pure helpers in `ui/src/platform/replaces.ts`). Without replacements the module list is the one loaded, unchanged.
- Code that imports another module's tool directly, rather than reading it from the modules, sees the original.

## Interface: plan overlays and grip snap

Two more points on a `LapinModule` (`ui/src/platform/module.ts`), for modules that drive elements from a specification of their own:

```ts
planOverlays: [MyGhost],                                  // ComponentType<PlanOverlayProps>[]
gripSnap: (element, grip, to, free) => Vec2 | undefined,  // undefined: leave the point
```

- `planOverlays` are drawn inside every plan view's SVG, over the elements and grips, with the plan's own transform: `toScreen(p)`, `toWorld(x, y)`, `scale` (px per metre), `rotation` (true north up) and `view` (`{ id, storeyId }`). The layer takes no pointer: an element that should be clicked carries `data-overlay-hit` and `pointer-events: all`, and a press on it starts no selection, grip or tool in the plan. Resolved by `planOverlays()` in `ui/src/platform/modules.ts`, drawn by `ui/src/viewport/PlanOverlays.tsx`.
- `gripSnap` is asked while a grip is dragged in a 2D view, after the owner's own `Grip.constrain`, for the elements the module does not own (the owner's `grips` are unchanged); the first module to answer with a point wins. It is not asked while Shift is held, nor when several grips are dragged together. Pure helper `snapGrip` in `ui/src/viewport/grips.ts`.

## Interface: placement checks

One more point on a `LapinModule`, for modules that drive walls (doors and windows put in them must fit):

```ts
placementCheck: (req) => ({ ok, message?, snapAlong? }) | null   // may be async; null leaves the host to the next module
```

- Asked, throttled (about 8 a second, only the latest question answered), while the Door or Window tool is on and the cursor is over a wall; `req` is `{ kind: 'door' | 'window', host, along (m from the start of the host's axis), width, typeId }`. The first module to answer wins.
- `ok: false` draws the ghost as an error with `message` beside the cursor; `snapAlong` (m along the axis) is where ghost and click go when the cursor is within a metre of it, on the cursor's side of the wall. The engine is still asked at the click and its sentence error shows as usual.
- A tool opts in with `gesture.placing(entity, at)` on its `pick` gesture (Door and Window do). Resolved by `askPlacement` in `ui/src/platform/modules.ts`; the store and pure helpers are in `ui/src/platform/placement.ts`.
