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
- File actions by id (`ui/src/shell/fileActions.ts`): `file.new`, `file.open`, `file.save`, `file.saveAs`, `file.saveVersion`, `file.importIfc`, `file.importFragments`, `file.linkIfc`, `file.exportExchange`, `file.optimizeIfc`, `file.importDxf`, `file.importMesh`, `file.exportMesh`, `file.share`, `file.join`, `file.startScreen`, `file.leave`. The keyboard's New, Open, Save and Save as run the same actions. An unknown id throws.
- Several modules replacing one id chain in load order, each given the one before it.
- Resolved in `ui/src/platform/modules.ts` (pure helpers in `ui/src/platform/replaces.ts`). Without replacements the module list is the one loaded, unchanged.
- Code that imports another module's tool directly, rather than reading it from the modules, sees the original.
