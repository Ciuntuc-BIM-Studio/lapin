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
    /// <summary>What each read id is read by: the elements to rebuild when it changes.</summary>
    readonly Dictionary<string, HashSet<string>> _readers = [];
    readonly Dictionary<string, string> _foreignClasses = [];
    long _version;

    sealed record Built(ElementGeometry? Geometry, HashSet<string> Reads, RenderItem? Item);

    /// <summary>
    /// Everything that changes the project's history, as it happens: what the
    /// recovery journal writes. "start": a new or opened project (what came
    /// before is gone from the session); "saved": the project written to its
    /// file (what came before is safe); "tx", "undo", "redo": an edit.
    /// </summary>
    public event Action<HistoryEvent>? HistoryChanged;
    bool _quiet;

    void Emit(string kind, Transaction? t = null)
    {
        if (!_quiet) HistoryChanged?.Invoke(new HistoryEvent(kind, t));
    }

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
        Emit("start");
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
        ForgetAll();
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
        ForgetAll();
        Path = path;
        Emit("start");
        return Snapshot();
    }

    /// <summary>
    /// Unsaved work brought back: the project as it started (a file's content,
    /// or a new project's first state), then the recorded edits, undos and
    /// redos done again. The undo history comes back with them; the project
    /// is unsaved.
    /// </summary>
    public Diff Recover(IEnumerable<Entity> entities, IEnumerable<Relation> relations, string? path, IReadOnlyDictionary<string, string>? classModules, IEnumerable<HistoryEvent> edits)
    {
        _quiet = true;
        try
        {
            Load(entities, relations, path, classModules);
            foreach (var e in edits)
                switch (e.Kind)
                {
                    case "tx": _history.Replay(e.Transaction ?? throw new InvalidDataException("A journal edit without its changes.")); break;
                    case "undo": _history.Undo(); break;
                    case "redo": _history.Redo(); break;
                }
        }
        finally { _quiet = false; }
        ForgetAll();
        return Snapshot();
    }

    /// <summary>The project was written to <paramref name="path"/>: nothing left to save.</summary>
    public Diff Saved(string path)
    {
        Path = path;
        _history.MarkSaved();
        Emit("saved");
        return Empty();
    }

    // ------------------------------------------------------------ edits

    /// <summary>The model and its geometry as queries see them.</summary>
    public IQueryContext Reader => new QueryContext(this);

    public Diff Execute(string command, JsonObject args, out JsonNode? result)
    {
        var entry = _catalog.Command(command) ?? throw new CommandException($"Unknown command „{command}”.");
        JsonNode? r = null;
        var t = _history.Execute(entry.Label, edit => r = entry.Handler(new CommandContext(edit, _catalog, new QueryContext(this)), args));
        result = r;
        if (t == null) return Empty();
        Emit("tx", t);
        return DiffAfter(t);
    }

    /// <summary>One edit made outside a module's commands (an IFC import): one undo step, journalled and diffed like a command.</summary>
    public Diff Edit(string label, Action<IEdit> edit)
    {
        var t = _history.Execute(label, edit);
        if (t == null) return Empty();
        Emit("tx", t);
        return DiffAfter(t);
    }

    public Diff Undo()
    {
        if (_history.Undo() is not { } t) return Empty();
        Emit("undo");
        return DiffAfter(t);
    }

    public Diff Redo()
    {
        if (_history.Redo() is not { } t) return Empty();
        Emit("redo");
        return DiffAfter(t);
    }

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

    /// <summary>An element's geometry built afresh with <paramref name="options"/>, outside the cache the views use (an export).</summary>
    public ElementGeometry? Geometry(string id, GeometryOptions options)
    {
        if (_store.Get(id) is not { } e || ProducerOf(e) is not { } producer) return null;
        var services = new ServicesWith(_catalog, options);
        var g = producer(e, _store, services);
        if (g != null) foreach (var f in _catalog.All<IGeometryFilter>()) g = f.Apply(e, g, _store, services) ?? g;
        return g;
    }

    /// <summary>What an entity measures: its quantity sets and its parts' (none when its class has no measures).</summary>
    public IReadOnlyList<QuantitySet> Quantities(string id)
    {
        if (_store.Get(id) is not { } e || _catalog.QuantitiesOf(e.Class) is not { } producer) return [];
        return producer(e, Geometry(id), _store).ToList();
    }

    // ------------------------------------------------------------ diffs

    public Diff Snapshot()
    {
        ForgetAll();
        var render = new List<RenderItem>();
        foreach (var e in _store.Entities)
            if (ProducerOf(e) != null && Build(e) is { Item: { } item }) render.Add(item);
        return new Diff(++_version, true,
            _store.Entities.Select(Dto).ToList(), [], [], [],
            render, [], State(),
            _store.Relations.Select(Dto).ToList(), []);
    }

    Diff Empty() => new(++_version, false, [], [], [], [], [], [], State(), [], []);

    /// <summary>
    /// The diff of an edit that is in the history and the journal already. Should
    /// it fail (an engine bug past what <see cref="Build"/> contains), the whole
    /// project goes instead: the interface is never left behind the model.
    /// </summary>
    Diff DiffAfter(Transaction t)
    {
        try { return DiffOf(t); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[engine] The diff of „{t.Label}” failed; the whole project is sent instead: {ex}");
            return Snapshot();
        }
    }

    Diff DiffOf(Transaction t)
    {
        // An entity's attributes, its relationships (whom a relationship change concerns), its class.
        var touched = t.Entities.Select(c => c.Id)
            .Concat(t.Relations.Select(c => c.Id))
            .Concat(t.Relations.SelectMany(Transaction.Concerned).Select(TrackingView.RelationsKey))
            .ToHashSet();
        foreach (var c in t.Entities)
        {
            if (c.Before != null) touched.Add(TrackingView.ClassKey(c.Before.Class));
            if (c.After != null) touched.Add(TrackingView.ClassKey(c.After.Class));
            // An element's own cut priority: what lower classes it may now cut (or no longer cuts).
            var (was, now) = (c.Before?.NumOrNull("cutPriority"), c.After?.NumOrNull("cutPriority"));
            if (was != now || c.Before?.Str("exportAs") != c.After?.Str("exportAs"))
            {
                if (c.Before != null) touched.Add(TrackingView.CutKey(c.Before.Class));
                if (c.After != null) touched.Add(TrackingView.CutKey(c.After.Class));
            }
        }
        touched.Add("class:*");

        // An entity's node changes with it, and with the relationships that place it (its parent, its storey).
        var nodeIds = t.Entities.Select(c => c.Id)
            .Concat(t.Relations.SelectMany(Transaction.Concerned).Where(id => _store.GetRelation(id) == null))
            .Distinct().ToList();
        var nodes = nodeIds.Select(_store.Get).OfType<Entity>().Select(Dto).ToList();
        var removedNodes = t.Entities.Where(c => _store.Get(c.Id) == null).Select(c => c.Id).ToList();
        var relations = t.Relations.Select(c => _store.GetRelation(c.Id)).OfType<Relation>().Select(Dto).ToList();
        var removedRelations = t.Relations.Where(c => _store.GetRelation(c.Id) == null).Select(c => c.Id).ToList();

        // Rebuild what read anything the edit touched, and the new elements.
        var rebuild = touched.SelectMany(id => _readers.TryGetValue(id, out var r) ? r : (IEnumerable<string>)[])
            .Concat(t.Entities.Where(c => c.After != null && ProducerOf(c.After) != null).Select(c => c.Id))
            .Distinct().ToList();
        var render = new List<RenderItem>();
        var renderRemoved = new List<string>();
        foreach (var id in rebuild)
        {
            var hadItem = _built.TryGetValue(id, out var old) && old.Item != null;
            Forget(id);
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

    /// <summary>What builds an entity's geometry: a body that came with it (a reference from IFC), else its class's producer.</summary>
    GeometryProducer? ProducerOf(Entity e) => StoredBodies.Has(e) ? StoredBodies.Geometry : _catalog.Geometry(e.Class);

    Built? Build(Entity e)
    {
        var producer = ProducerOf(e);
        if (producer == null) return null;
        var view = new TrackingView(_store);
        view.Read.Add(e.Id);
        ElementGeometry? g;
        RenderItem? item = null;
        List<string> problems = [];
        try
        {
            g = producer(e, view, _catalog);
            if (g != null) foreach (var f in _catalog.All<IGeometryFilter>()) g = f.Apply(e, g, view, _catalog) ?? g;
            if (g != null) item = Render(e, g, problems);
        }
        catch (Exception ex)
        {
            // A refusal the module meant, or a bug in it (or a solid the tessellator cannot
            // read): the element is shown with its problem, and never takes the diff down with it.
            g = null;
            problems.Add(ex is ModelException or InvalidOperationException or ArgumentException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}");
            item = Render(e, null, problems);
        }
        var built = new Built(g, view.Read, item);
        Forget(e.Id);
        _built[e.Id] = built;
        foreach (var id in built.Reads) (_readers.TryGetValue(id, out var set) ? set : _readers[id] = []).Add(e.Id);
        return built;
    }

    void Forget(string id)
    {
        if (!_built.Remove(id, out var old)) return;
        foreach (var r in old.Reads)
            if (_readers.TryGetValue(r, out var set)) { set.Remove(id); if (set.Count == 0) _readers.Remove(r); }
    }

    void ForgetAll()
    {
        _built.Clear();
        _readers.Clear();
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

    /// <summary>The catalog's services and one more, for a single build.</summary>
    sealed class ServicesWith(IServices inner, object extra) : IServices
    {
        public T? Get<T>() where T : class => extra as T ?? inner.Get<T>();
        public IReadOnlyList<T> All<T>() where T : class => extra is T x ? [x, .. inner.All<T>()] : inner.All<T>();
    }

    sealed class QueryContext(Session s) : IQueryContext
    {
        public IModelView Model => s._store;
        public IServices Services => s._catalog;
        public ElementGeometry? Geometry(string entityId) => s.Geometry(entityId);
    }
}

/// <summary>A change of the project's history (<see cref="Session.HistoryChanged"/>).</summary>
public sealed record HistoryEvent(string Kind, Transaction? Transaction = null);

/// <summary>A command's view of the edit: the model, the services, and a refusal for what this product cannot change.</summary>
sealed class CommandContext(IEdit edit, ModuleCatalog catalog, IQueryContext query) : ICommandContext
{
    public IQueryContext Query => query;
    public IModelView Model => edit.Model;
    public IServices Services => catalog;
    public string NewId() => edit.NewId();
    public ClassDescriptor? Describe(string ifcClass) => catalog.Class(ifcClass);

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
    public void AddRelation(Relation relation) => edit.AddRelation(relation);
    public void ReplaceRelation(Relation relation) => edit.ReplaceRelation(relation);
    public void RemoveRelation(string id) => edit.RemoveRelation(id);
}
