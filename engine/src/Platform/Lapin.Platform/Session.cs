using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// One open project in the engine: the model, its history, and the geometry
/// derived from it. Every change comes in as a command and goes out as a
/// <see cref="Diff"/>: the entities and relationships it changed and the
/// elements whose geometry it rebuilt. Single-threaded by design.
/// </summary>
public sealed class Session
{
    readonly ModuleCatalog _catalog;
    readonly ProductManifest _product;
    readonly ModelStore _store = new();
    readonly History _history;
    readonly Dictionary<string, Built> _built = [];
    readonly Dictionary<string, string> _foreignClasses = [];
    long _version;

    sealed record Built(ElementGeometry? Geometry, HashSet<string> Reads, RenderItem? Item);

    public Session(ModuleCatalog catalog, ProductManifest product)
    {
        _catalog = catalog;
        _product = product;
        _history = new History(_store);
        StartEmpty();
    }

    public IModelView Model => _store;
    public ModuleCatalog Catalog => _catalog;
    public ProductManifest Product => _product;
    public History History => _history;
    public string? Path { get; private set; }

    public SessionInfo Info() => new(
        _product,
        _catalog.Modules.Select(m => new ModuleInfo(m.Id, m.Name, m.SchemaVersion)).ToList(),
        [],
        _catalog.Classes.Select(c => new NodeTypeInfo(c.Descriptor.Class, c.Descriptor.Label, c.Module, c.Descriptor.Color, c.Descriptor.Color, c.Descriptor.Kind, "", c.Descriptor.Kind == "element", c.Descriptor.Fields, [], false)).ToList(),
        [],
        _catalog.Commands.Select(c => new CommandInfo(c.Name, c.Label)).ToList(),
        "m",
        typeof(Session).Assembly.GetName().Version?.ToString() ?? "0",
        _catalog.Catalogs);

    // ------------------------------------------------------------ project

    /// <summary>A new project: the project, site and building, and a ground floor. Nothing to undo, nothing to save.</summary>
    public Diff New()
    {
        StartEmpty();
        Path = null;
        return Snapshot();
    }

    void StartEmpty()
    {
        _foreignClasses.Clear();
        _history.Load([], []);
        _history.Execute("New project", edit =>
        {
            var building = Spatial.EnsureBuilding(edit);
            var storey = new Entity(edit.NewId(), IfcClass.BuildingStorey, "Ground floor", new JsonObject { ["elevation"] = 0.0, ["height"] = 3.0 });
            edit.Add(storey);
            edit.Relate(IfcClass.RelAggregates, building.Id, storey.Id);
        });
        _history.Reset();
        _built.Clear();
    }

    /// <summary>
    /// The module that wrote a class: its owner in this product, else the
    /// module the file says wrote it (a module this product does not have:
    /// its entities are kept and written back as they came).
    /// </summary>
    public string ModuleOf(string ifcClass) => _catalog.OwnerOf(ifcClass) ?? _foreignClasses.GetValueOrDefault(ifcClass, "unknown");

    /// <summary>Replaces the model with one read from a file (no undo step). <paramref name="classModules"/>: which module wrote each class.</summary>
    public Diff Load(IEnumerable<Entity> entities, IEnumerable<Relation> relations, string? path, IReadOnlyDictionary<string, string>? classModules = null)
    {
        _foreignClasses.Clear();
        foreach (var (cls, module) in classModules ?? new Dictionary<string, string>())
            if (_catalog.OwnerOf(cls) == null) _foreignClasses[cls] = module;
        _history.Load(entities, relations);
        _built.Clear();
        Path = path;
        return Snapshot();
    }

    /// <summary>The project was written to <paramref name="path"/>: nothing left to save.</summary>
    public Diff Saved(string path)
    {
        Path = path;
        _history.MarkSaved();
        return Empty();
    }

    // ------------------------------------------------------------ edits

    public Diff Execute(string command, JsonObject args, out JsonNode? result)
    {
        var entry = _catalog.Command(command) ?? throw new CommandException($"Unknown command „{command}”.");
        JsonNode? r = null;
        var t = _history.Execute(entry.Label, edit => r = entry.Handler(new CommandContext(edit, _catalog), args));
        result = r;
        return t == null ? Empty() : DiffOf(t);
    }

    public Diff Undo() => _history.Undo() is { } t ? DiffOf(t) : Empty();
    public Diff Redo() => _history.Redo() is { } t ? DiffOf(t) : Empty();

    public JsonNode? Query(string name, JsonObject args)
    {
        var q = _catalog.Query(name) ?? throw new CommandException($"Unknown query „{name}”.");
        return q(new QueryContext(this), args);
    }

    /// <summary>An element's geometry as it is now (built when first asked for).</summary>
    public ElementGeometry? Geometry(string id)
    {
        if (_built.TryGetValue(id, out var b)) return b.Geometry;
        return _store.Get(id) is { } e ? Build(e)?.Geometry : null;
    }

    // ------------------------------------------------------------ diffs

    public Diff Snapshot()
    {
        _built.Clear();
        var render = new List<RenderItem>();
        foreach (var e in _store.Entities)
            if (_catalog.Geometry(e.Class) != null && Build(e) is { Item: { } item }) render.Add(item);
        return new Diff(++_version, true,
            _store.Entities.Select(Dto).ToList(), [], [], [],
            render, [], State(),
            _store.Relations.Select(Dto).ToList(), []);
    }

    Diff Empty() => new(++_version, false, [], [], [], [], [], [], State(), [], []);

    Diff DiffOf(Transaction t)
    {
        var touched = t.TouchedIds().ToHashSet();
        foreach (var c in t.Entities)
        {
            if (c.Before != null) touched.Add(TrackingView.ClassKey(c.Before.Class));
            if (c.After != null) touched.Add(TrackingView.ClassKey(c.After.Class));
        }
        touched.Add("class:*");

        // An entity's node changes with it, and with the relationships that place it (its parent, its storey).
        var nodeIds = t.Entities.Select(c => c.Id)
            .Concat(t.Relations.SelectMany(c => (c.Before?.Related ?? []).Concat(c.After?.Related ?? [])))
            .Distinct().ToList();
        var nodes = nodeIds.Select(_store.Get).OfType<Entity>().Select(Dto).ToList();
        var removedNodes = t.Entities.Where(c => _store.Get(c.Id) == null).Select(c => c.Id).ToList();
        var relations = t.Relations.Select(c => _store.GetRelation(c.Id)).OfType<Relation>().Select(Dto).ToList();
        var removedRelations = t.Relations.Where(c => _store.GetRelation(c.Id) == null).Select(c => c.Id).ToList();

        // Rebuild what read anything the edit touched, and the new elements.
        var rebuild = _built.Where(kv => kv.Value.Reads.Overlaps(touched)).Select(kv => kv.Key)
            .Concat(t.Entities.Where(c => c.After != null && _catalog.Geometry(c.After.Class) != null).Select(c => c.Id))
            .Distinct().ToList();
        var render = new List<RenderItem>();
        var renderRemoved = new List<string>();
        foreach (var id in rebuild)
        {
            var hadItem = _built.TryGetValue(id, out var old) && old.Item != null;
            _built.Remove(id);
            if (_store.Get(id) is { } e && Build(e) is { Item: { } item }) render.Add(item);
            else if (hadItem) renderRemoved.Add(id);
        }
        return new Diff(++_version, false, nodes, removedNodes, [], [], render, renderRemoved, State(), relations, removedRelations);
    }

    DocState State() => new(_history.CanUndo, _history.CanRedo, _history.UndoLabel, _history.RedoLabel, _history.Dirty, Path, _store.EntityCount, _store.RelationCount);

    NodeDto Dto(Entity e)
    {
        var props = e.CloneAttrs();
        if (e.PredefinedType != null) props["predefinedType"] = e.PredefinedType;
        var owner = _catalog.OwnerOf(e.Class);
        var storey = e.Class == IfcClass.BuildingStorey ? null : Spatial.StoreyOf(_store, e.Id)?.Id;
        return new NodeDto(e.Id, e.Class, e.Name, Spatial.ParentOf(_store, e.Id)?.Id, 0, 0, props, owner != null, owner, storey);
    }

    static RelationDto Dto(Relation r) => new(r.Id, r.Class, r.Relating, r.Related);

    // ------------------------------------------------------------ geometry

    Built? Build(Entity e)
    {
        var producer = _catalog.Geometry(e.Class);
        if (producer == null) return null;
        var view = new TrackingView(_store);
        view.Read.Add(e.Id);
        ElementGeometry? g;
        List<string> problems = [];
        try { g = producer(e, view, _catalog); }
        catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException)
        {
            g = null;
            problems.Add(ex.Message);
        }
        var built = new Built(g, view.Read, g == null && problems.Count == 0 ? null : Render(e, g, problems));
        _built[e.Id] = built;
        return built;
    }

    RenderItem Render(Entity e, ElementGeometry? g, List<string> problems)
    {
        var parts = new List<RenderPart>();
        foreach (var p in g?.Parts ?? [])
        {
            var m = Tessellator.Tessellate(p.Solid);
            parts.Add(new RenderPart(p.Name, p.Color, m.Positions, m.Normals, m.Indices, m.Edges, p.Material));
        }
        var plan = (g?.Plan ?? []).Select(i => new PlanItemDto(i.Kind, i.Closed, i.Points.SelectMany(v => new[] { v.X, v.Y }).ToArray())).ToList();
        var snaps = (g?.Snaps ?? []).SelectMany(v => new[] { v.X, v.Y }).ToArray();
        return new RenderItem(e.Id, e.Class, Spatial.StoreyOf(_store, e.Id)?.Id, _catalog.OwnerOf(e.Class) != null,
            parts.FirstOrDefault()?.Color ?? "#c9ced6", [], [], [], [], plan, snaps,
            [.. problems, .. g?.Diagnostics ?? []], parts);
    }

    sealed class QueryContext(Session s) : IQueryContext
    {
        public IModelView Model => s._store;
        public IServices Services => s._catalog;
        public ElementGeometry? Geometry(string entityId) => s.Geometry(entityId);
    }
}

/// <summary>A command's view of the edit: the model, the services, and a refusal for what this product cannot change.</summary>
sealed class CommandContext(IEdit edit, ModuleCatalog catalog) : ICommandContext
{
    public IModelView Model => edit.Model;
    public IServices Services => catalog;
    public string NewId() => edit.NewId();

    void EnsureEditable(Entity e)
    {
        if (catalog.OwnerOf(e.Class) == null)
            throw new CommandException($"„{e.Name}” ({e.Class}) belongs to a module this product does not have: here it can only be seen.");
    }

    public void Add(Entity entity) { EnsureEditable(entity); edit.Add(entity); }

    public void Replace(Entity entity)
    {
        if (edit.Model.Get(entity.Id) is { } old) EnsureEditable(old);
        edit.Replace(entity);
    }

    public void Remove(string id)
    {
        if (edit.Model.Get(id) is { } old) EnsureEditable(old);
        edit.Remove(id);
    }

    public Relation Relate(string relationClass, string relating, string related) => edit.Relate(relationClass, relating, related);
    public void Unrelate(string relationClass, string related) => edit.Unrelate(relationClass, related);
}
