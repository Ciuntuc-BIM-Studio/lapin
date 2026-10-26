using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
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

    /// <summary>An element as built: its geometry, what it read, its render item as the tessellator made it (its edges before the seams go), and its box.</summary>
    sealed record Built(ElementGeometry? Geometry, HashSet<string> Reads, RenderItem? Item, (Vec3 Min, Vec3 Max)? Bounds);

    /// <summary>Each solid's faces ready for the seams, once per solid (solids are immutable; an element rebuilt brings new ones).</summary>
    readonly Dictionary<Solid, List<Seams.Plane>> _planes = new(ReferenceEqualityComparer.Instance);

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

    /// <summary>
    /// The storeys a new project starts with (name, base level, height): the
    /// foundations below ±0.00, the ground floor and the first floor.
    /// </summary>
    public static readonly IReadOnlyList<(string Name, double Elevation, double Height)> NewProjectStoreys =
    [
        ("Foundations", -1.5, 1.5),
        ("Ground floor", 0.0, 3.0),
        ("First floor", 3.0, 3.0),
    ];

    /// <summary>
    /// A new project: the project, site and building, and its storeys
    /// (<see cref="NewProjectStoreys"/>); <paramref name="plain"/>: one ground
    /// floor only, for a project another file fills (an old project, an IFC
    /// opened as a project). Nothing to undo, nothing to save.
    /// </summary>
    public Diff New(bool plain = false)
    {
        if (plain) StartEmpty(); else StartEmpty(NewProjectStoreys);
        Path = null;
        Emit("start");
        return Snapshot();
    }

    /// <summary>The session as it starts, before a project is made or opened: one ground floor.</summary>
    void StartEmpty() => StartEmpty([("Ground floor", 0.0, 3.0)]);

    void StartEmpty(IReadOnlyList<(string Name, double Elevation, double Height)> storeys)
    {
        _foreignClasses.Clear();
        _history.Load([], []);
        _history.Execute("New project", edit =>
        {
            var building = Spatial.EnsureBuilding(edit);
            foreach (var (name, elevation, height) in storeys)
            {
                var storey = new Entity(edit.NewId(), IfcClass.BuildingStorey, name, new JsonObject { ["elevation"] = elevation, ["height"] = height });
                edit.Add(storey);
                edit.Relate(IfcClass.RelAggregates, building.Id, storey.Id);
            }
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

    /// <summary>Classes a file's content brought in through an edit (a version restored): which module wrote each, for the next save.</summary>
    public void AdoptClasses(IReadOnlyDictionary<string, string> classModules)
    {
        foreach (var (cls, module) in classModules)
            if (_catalog.OwnerOf(cls) == null) _foreignClasses[cls] = module;
    }

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
        // What a link brought in is read-only, except to the link commands and to the imports that make links.
        var allowLinked = command.StartsWith("link.", StringComparison.Ordinal) || command.EndsWith(".importDxf", StringComparison.Ordinal);
        var t = _history.Execute(entry.Label, edit => r = entry.Handler(new CommandContext(edit, _catalog, new QueryContext(this), allowLinked), args));
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
        var ids = new List<string>();
        foreach (var e in _store.Entities)
            if (ProducerOf(e) != null && Build(e) is { Item: not null }) ids.Add(e.Id);
        // Every element built first: each one's edges are trimmed against its neighbours.
        var keys = new Dictionary<string, string?>();
        var render = ids.Select(id => Emitted(id, keys)).OfType<RenderItem>().ToList();
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

        // Rebuild what read anything the edit touched, and the elements the edit left in the model (new ones; an undone deletion brings one back).
        var rebuild = touched.SelectMany(id => _readers.TryGetValue(id, out var r) ? r : (IEnumerable<string>)[])
            .Concat(t.Entities.Where(c => _store.Get(c.Id) is { } now && ProducerOf(now) != null).Select(c => c.Id))
            .Distinct().ToList();
        var renderRemoved = new List<string>();
        var emit = new List<string>();
        // Where the rebuilt elements were and are: the neighbours there get their seams again, or lose them.
        var boxes = new List<(Vec3 Min, Vec3 Max)>();
        foreach (var id in rebuild)
        {
            var hadItem = _built.TryGetValue(id, out var old) && old.Item != null;
            if (old?.Bounds is { } was) boxes.Add(was);
            Forget(id);
            if (_store.Get(id) is { } e && Build(e) is { Item: not null } built)
            {
                emit.Add(id);
                if (built.Bounds is { } now) boxes.Add(now);
            }
            else if (hadItem) renderRemoved.Add(id);
        }
        emit.AddRange(Neighbours(boxes, rebuild.ToHashSet()));
        var keys = new Dictionary<string, string?>();
        var render = emit.Select(id => Emitted(id, keys)).OfType<RenderItem>().ToList();
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
        var built = new Built(g, view.Read, item, BoundsOf(g));
        Forget(e.Id);
        _built[e.Id] = built;
        foreach (var id in built.Reads) (_readers.TryGetValue(id, out var set) ? set : _readers[id] = []).Add(e.Id);
        return built;
    }

    static (Vec3 Min, Vec3 Max)? BoundsOf(ElementGeometry? g)
    {
        (Vec3 Min, Vec3 Max)? box = null;
        foreach (var p in g?.Parts ?? [])
        {
            if (Measure.Bounds(p.Solid) is not { } b) continue;
            box = box is { } o
                ? (new Vec3(Math.Min(o.Min.X, b.Min.X), Math.Min(o.Min.Y, b.Min.Y), Math.Min(o.Min.Z, b.Min.Z)), new Vec3(Math.Max(o.Max.X, b.Max.X), Math.Max(o.Max.Y, b.Max.Y), Math.Max(o.Max.Z, b.Max.Z)))
                : b;
        }
        return box;
    }

    static bool Overlaps((Vec3 Min, Vec3 Max) a, (Vec3 Min, Vec3 Max) b, double tol = 1e-3) =>
        a.Min.X <= b.Max.X + tol && b.Min.X <= a.Max.X + tol && a.Min.Y <= b.Max.Y + tol && b.Min.Y <= a.Max.Y + tol && a.Min.Z <= b.Max.Z + tol && b.Min.Z <= a.Max.Z + tol;

    /// <summary>The built elements whose box touches one of <paramref name="boxes"/>, other than <paramref name="except"/>.</summary>
    IEnumerable<string> Neighbours(IReadOnlyList<(Vec3 Min, Vec3 Max)> boxes, ICollection<string> except)
    {
        if (boxes.Count == 0) yield break;
        foreach (var (id, b) in _built)
            if (b.Bounds is { } box && b.Item != null && !except.Contains(id) && boxes.Any(x => Overlaps(x, box))) yield return id;
    }

    /// <summary>
    /// An element's render item as the interface gets it: its parts' edges less
    /// the seams, where a surface goes on, in its own plane, into a neighbour's
    /// part of the same material (a wall's render over the storey above; see
    /// <see cref="Seams"/>). The same rule as the elevations' and the plan's:
    /// the elements stay apart, the material reads as one.
    /// </summary>
    RenderItem? Emitted(string id, Dictionary<string, string?> keys)
    {
        if (!_built.TryGetValue(id, out var b) || b.Item is not { } item) return null;
        if (b.Geometry is not { } g || b.Bounds is not { } box || item.Parts is not { Count: > 0 } parts || parts.Count != g.Parts.Count) return item;
        var cls = _store.Get(id)?.Class ?? item.Type;
        var near = new List<(Built Built, string Class)>();
        foreach (var (nid, nb) in _built)
            if (nid != id && nb.Geometry != null && nb.Bounds is { } nbox && Overlaps(box, nbox)) near.Add((nb, _store.Get(nid)?.Class ?? nb.Item?.Type ?? ""));
        if (near.Count == 0) return item;
        List<Seams.Plane> PlanesOf(Solid s) => _planes.TryGetValue(s, out var p) ? p : _planes[s] = Seams.Planes(s);
        List<RenderPart>? trimmed = null;
        for (var i = 0; i < parts.Count; i++)
        {
            var part = g.Parts[i];
            var key = Views.SeamKeyOf(_store, part.Material, cls, keys);
            if (key == null || Measure.Bounds(part.Solid) is not { } pbox) continue;
            var onward = new List<Seams.Plane>();
            foreach (var (nb, ncls) in near)
                foreach (var q in nb.Geometry!.Parts)
                    if (Views.SeamKeyOf(_store, q.Material, ncls, keys) == key && Measure.Bounds(q.Solid) is { } qbox && Overlaps(pbox, qbox)) onward.AddRange(PlanesOf(q.Solid));
            if (onward.Count == 0) continue;
            trimmed ??= [.. parts];
            trimmed[i] = parts[i] with { Edges = Seams.Flat(Seams.Edges(part.Solid, onward)) };
        }
        return trimmed == null ? item : item with { Parts = trimmed };
    }

    void Forget(string id)
    {
        if (!_built.Remove(id, out var old)) return;
        foreach (var p in old.Geometry?.Parts ?? []) _planes.Remove(p.Solid);
        foreach (var r in old.Reads)
            if (_readers.TryGetValue(r, out var set)) { set.Remove(id); if (set.Count == 0) _readers.Remove(r); }
    }

    void ForgetAll()
    {
        _built.Clear();
        _readers.Clear();
        _planes.Clear();
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
sealed class CommandContext(IEdit edit, ModuleCatalog catalog, IQueryContext query, bool allowLinked = false) : ICommandContext
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
        if (!allowLinked && e.Str(Links.Key) is { } linkId)
        {
            var link = edit.Model.Get(linkId);
            throw new CommandException($"„{e.Name}” belongs to the link „{link?.Name ?? "?"}”: it can be seen, not edited. Manage → Links: unlink it, convert it to native, or open it on its own.");
        }
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
