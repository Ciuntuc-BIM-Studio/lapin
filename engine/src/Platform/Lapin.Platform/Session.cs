using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
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
    readonly System.Collections.Concurrent.ConcurrentDictionary<Solid, List<Seams.Plane>> _planes = new(ReferenceEqualityComparer.Instance);

    /// <summary>The shared bodies' meshes (docs/plan-instancing.md), each with the body it was tessellated from; and those the interface has.</summary>
    readonly Dictionary<string, (JsonObject Body, SharedMesh Mesh)> _meshes = [];
    readonly HashSet<string> _meshesSent = [];

    /// <summary>
    /// Everything that changes the project's history, as it happens: what the
    /// recovery journal writes. "start": a new or opened project (what came
    /// before is gone from the session); "saved": the project written to its
    /// file (what came before is safe); "tx", "undo", "redo": an edit.
    /// </summary>
    public event Action<HistoryEvent>? HistoryChanged;
    bool _quiet;

    void Emit(string kind, Transaction? t = null, string? author = null)
    {
        if (!_quiet) HistoryChanged?.Invoke(new HistoryEvent(kind, t, author == History.Me ? null : author));
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

    /// <summary>The app's version: the build's (0.1.&lt;commit count&gt;, given as InformationalVersion when the desktop app is built), without the commit hash.</summary>
    public static string AppVersion { get; } = (typeof(Session).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).FirstOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute)?.InformationalVersion.Split('+')[0] ?? "0";

    public SessionInfo Info() => new(
        _product,
        _catalog.Modules.Select(m => new ModuleInfo(m.Id, m.Name, m.SchemaVersion)).ToList(),
        [],
        _catalog.Classes.Select(c => new NodeTypeInfo(c.Descriptor.Class, c.Descriptor.Label, c.Module, c.Descriptor.Color, c.Descriptor.Color, c.Descriptor.Kind, "", c.Descriptor.Kind == "element", c.Descriptor.Fields, [], false)).ToList(),
        [],
        _catalog.Commands.Select(c => new CommandInfo(c.Name, c.Label)).ToList(),
        "m",
        AppVersion,
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
            // A new project's Site: its site plan and its location plan, on the ground floor.
            if (storeys.Count > 1 && SitePlans.Host(edit.Model) is { } host)
                foreach (var purpose in SitePlans.Purposes)
                {
                    var (attrs, name) = SitePlans.New(purpose, host.Id, null, null);
                    edit.Add(new Entity(edit.NewId(), Views.Class, name, attrs));
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
        var (es, rs) = Upgraded(entities, relations);
        _history.Load(es, rs);
        ForgetAll();
        Path = path;
        Emit("start");
        return Snapshot();
    }

    /// <summary>What an older Lapin wrote, as this one keeps it: each module's <see cref="IProjectUpgrade"/> in turn.</summary>
    public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgraded(IEnumerable<Entity> entities, IEnumerable<Relation> relations)
    {
        IReadOnlyList<Entity> es = entities as IReadOnlyList<Entity> ?? entities.ToList();
        IReadOnlyList<Relation> rs = relations as IReadOnlyList<Relation> ?? relations.ToList();
        foreach (var upgrade in _catalog.All<IProjectUpgrade>()) (es, rs) = upgrade.Upgrade(es, rs);
        return (es, rs);
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
                    case "replay": (e.Replay ?? throw new InvalidDataException("A journal edit without a way to do it again."))(this); break;
                    case "undo": _history.Undo(e.Author ?? History.Me); break;
                    case "redo": _history.Redo(e.Author ?? History.Me); break;
                }
        }
        finally { _quiet = false; }
        ForgetAll();
        return Snapshot();
    }

    /// <summary>
    /// What the edit in progress can be done again from (an import: its file, hash, mode and seed), for the recovery
    /// journal to record instead of the edit's every change; null for an ordinary edit. Set by the host round the edit.
    /// </summary>
    public JsonObject? EditSource { get; set; }

    /// <summary>Told how a long edit goes (an import: its stage, how much of it is done): the host shows it. May be called from any thread.</summary>
    public Action<string, int, int>? Progress { get; set; }

    /// <summary>
    /// New ids from <paramref name="seed"/> until disposed, one after the other, so an edit done again from its source
    /// (a recovered import) makes the same ids as the first time, and the journal's later edits find them.
    /// </summary>
    public IDisposable SeededIds(long seed)
    {
        var was = _history.NewId;
        var n = 0L;
        _history.NewId = () =>
        {
            Span<byte> input = stackalloc byte[16];
            BitConverter.TryWriteBytes(input, seed);
            BitConverter.TryWriteBytes(input[8..], n++);
            Span<byte> hash = stackalloc byte[32];
            System.Security.Cryptography.SHA256.HashData(input, hash);
            return GlobalId.FromGuid(new Guid(hash[..16]));
        };
        return new Restore(() => _history.NewId = was);
    }

    sealed class Restore(Action back) : IDisposable { public void Dispose() => back(); }

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

    public Diff Execute(string command, JsonObject args, out JsonNode? result) => Execute(command, args, Author, out result);

    /// <summary>Who the edits being made now are by: the computer's own user, or the shared session's guest whose call this is (the host sets it per call).</summary>
    public string Author { get; set; } = History.Me;

    /// <summary>A command made by <paramref name="author"/> (a guest of a shared session: their participant id).</summary>
    public Diff Execute(string command, JsonObject args, string author, out JsonNode? result)
    {
        var entry = _catalog.Command(command) ?? throw new CommandException($"Unknown command „{command}”.");
        JsonNode? r = null;
        // What a link brought in is read-only, except to the link commands and to the imports that make links.
        var allowLinked = command.StartsWith("link.", StringComparison.Ordinal) || command.EndsWith(".importDxf", StringComparison.Ordinal);
        var t = _history.Execute(entry.Label, edit => r = entry.Handler(new CommandContext(edit, _catalog, new QueryContext(this), allowLinked), args), author);
        result = r;
        if (t == null) return Empty();
        Emit("tx", t);
        return DiffAfter(t);
    }

    /// <summary>One edit made outside a module's commands (an IFC import): one undo step, journalled and diffed like a command.</summary>
    public Diff Edit(string label, Action<IEdit> edit)
    {
        var t = _history.Execute(label, edit, Author);
        if (t == null) return Empty();
        Emit("tx", t);
        // Being recovered (an import done again): the snapshot at the end draws everything once.
        return _quiet ? Empty() : DiffAfter(t);
    }

    /// <summary>
    /// <paramref name="author"/>'s last edit undone (docs/plan-multiplayer.md:
    /// in a shared history only their own, as a new transaction, which the
    /// journal keeps as one).
    /// </summary>
    public Diff Undo(string? author = null)
    {
        author ??= Author;
        if (_history.Undo(author) is not { } t) return Empty();
        if (t.Reverts != null) Emit("tx", t); else Emit("undo", null, author);
        return DiffAfter(t);
    }

    public Diff Redo(string? author = null)
    {
        author ??= Author;
        if (_history.Redo(author) is not { } t) return Empty();
        if (t.Redoes != null) Emit("tx", t); else Emit("redo", null, author);
        return DiffAfter(t);
    }

    /// <summary>The document's state as <paramref name="author"/> sees it: their own undo and redo.</summary>
    public DocState StateFor(string author) => State() with
    {
        CanUndo = _history.CanUndoOf(author), CanRedo = _history.CanRedoOf(author),
        UndoLabel = _history.UndoLabelOf(author), RedoLabel = _history.RedoLabelOf(author),
    };

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

    /// <summary>
    /// The caps a section seen in 3D closes the cut elements with
    /// (<see cref="SectionCaps"/>): for every built part the planes cut, the
    /// faces on the planes inside what they all keep, in the part's material,
    /// their outlines less the seams where one material goes on from one
    /// element into another (the 2D sections' rule). <paramref name="lift"/>
    /// moves them into the kept side, so the renderer's own clipping keeps them.
    /// <paramref name="drawing"/>: as a sheet draws the cut, each cap with its material's section
    /// fill and hatch, the hatch at 1:<paramref name="scale"/>.
    /// </summary>
    public List<CapItem> Caps(IReadOnlyList<ClipPlane> planes, double lift = 0.0005, bool drawing = false, double scale = 50, string layers = "all")
    {
        var result = new List<CapItem>();
        if (planes.Count == 0) return result;
        var solids = new List<Solid>();
        var owners = new List<(string Id, int Part, GeometryPart Geometry)>();
        var seamKeys = new List<string?>();
        var keys = new Dictionary<string, string?>();
        var bands = new Dictionary<string, IReadOnlyList<LayerBand>?>();
        foreach (var (id, b) in _built)
        {
            if (b.Geometry is not { } g || b.Bounds is not { } box || b.Item == null) continue;
            // Only what a plane crosses and none cuts wholly away.
            var crossed = false;
            var gone = false;
            foreach (var p in planes)
            {
                double lo = double.MaxValue, hi = double.MinValue;
                for (var c = 0; c < 8; c++)
                {
                    var d = p.Distance(new Vec3((c & 1) == 0 ? box.Min.X : box.Max.X, (c & 2) == 0 ? box.Min.Y : box.Max.Y, (c & 4) == 0 ? box.Min.Z : box.Max.Z));
                    lo = Math.Min(lo, d); hi = Math.Max(hi, d);
                }
                if (hi < 0) { gone = true; break; }
                if (lo < 0) crossed = true;
            }
            if (gone || !crossed) continue;
            var cls = _store.Get(id)?.Class ?? b.Item.Type;
            for (var i = 0; i < g.Parts.Count; i++)
            {
                // The layers the 3D view leaves out (its layers mode) have no caps: what is kept closes alone.
                if (!ViewStyles.Shows3d(_store, layers, id, g.Parts[i], bands)) continue;
                solids.Add(g.Parts[i].Solid);
                owners.Add((id, i, g.Parts[i]));
                seamKeys.Add(Views.SeamKeyOf(_store, g.Parts[i].Material, cls, keys));
            }
        }
        var (caps, faces) = SectionCaps.MakeWithFaces(solids, planes, seamKeys, lift);
        var byNode = new Dictionary<string, List<CapPart>>();
        var patterns = drawing ? DraftingResources.Patterns(_store) : null;
        for (var k = 0; k < caps.Length; k++)
        {
            var m = caps[k];
            if (m.Indices.Length == 0) continue;
            var (id, part, gp) = owners[k];
            if (!byNode.TryGetValue(id, out var list)) { byNode[id] = list = []; result.Add(new CapItem(id, list)); }
            if (!drawing) { list.Add(new CapPart(part, gp.Color, gp.Material, m.Positions, m.Normals, m.Indices, m.Edges)); continue; }
            var look = MaterialGraphics.Resolve(gp.Material == null ? null : _store.Get(gp.Material), "section");
            var (fill, hatch) = CapHatch(faces[k], look, patterns!, scale);
            list.Add(new CapPart(part, gp.Color, gp.Material, m.Positions, m.Normals, m.Indices, m.Edges, fill, hatch));
        }
        return result;
    }

    /// <summary>
    /// A cap drawn as a sheet draws the cut: its fill (white without one; a solid pattern fills it) and its
    /// hatch's segments on its faces, lifted a little off them towards the viewer. The hatch lies in axes of the
    /// face's plane through the world's origin, so neighbouring caps on one plane hatch as one; on an upright
    /// plane its X runs level, its Y up, as the section's drawing has them.
    /// </summary>
    static (string Fill, float[] Hatch) CapHatch(List<CapFace> faces, Lapin.Drawing.ResolvedMaterial look, IReadOnlyList<Lapin.Drawing.Hatch.HatchPattern> patterns, double scale)
    {
        var fill = look.Fill ?? "#ffffff";
        var pattern = look.Hatch is { } h ? Lapin.Drawing.Hatch.HatchLibrary.Find(h, patterns) : null;
        var hatch = new List<float>();
        if (pattern == null) return (fill, []);
        if (pattern.Solid) return ("#2b2b2b", []);
        foreach (var f in faces)
        {
            var n = f.Normal;
            var u = Math.Abs(n.Z) < 0.9 ? Vec3.Cross(n, Vec3.UnitZ).Normalized() : new Vec3(1, 0, 0);
            var v = Math.Abs(n.Z) < 0.9 ? Vec3.Cross(u, n).Normalized() : Vec3.Cross(n, u).Normalized();
            var d = Vec3.Dot(f.Origin, n) + 0.0002;
            var loops = f.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(p => { var w = f.At(p); return new Vec2(Vec3.Dot(w, u), Vec3.Dot(w, v)); }).ToList()).ToList();
            // Along the layer on a level cut (a wall's courses follow the wall, however it is turned), and wherever the
            // material asks for it; else level, as the section's drawing has it.
            var (angle, origin) = Math.Abs(n.Z) >= 0.9 || look.HatchAlign == Lapin.Drawing.HatchAlign.Layer ? AlongLayer(loops) : (0.0, (Vec2?)null);
            var segs = Lapin.Drawing.Hatch.BattHatch.Is(pattern)
                ? Lapin.Drawing.Hatch.BattHatch.Segments(loops, 1.2 * scale / 1000, 20_000)
                : Lapin.Drawing.Hatch.HatchFill.Segments(loops, pattern, new Lapin.Drawing.Hatch.HatchOptions(look.HatchScale * scale / 1000, angle + look.HatchAngle, origin, 20_000)).Segments;
            foreach (var s in segs)
                foreach (var p in new[] { s.A, s.B })
                {
                    var w = u * p.X + v * p.Y + n * d;
                    hatch.Add((float)w.X); hatch.Add((float)w.Y); hatch.Add((float)w.Z);
                }
        }
        return (fill, [.. hatch]);
    }

    /// <summary>
    /// A cap's layer direction, as the 2D drawings' "layer" alignment reads a band (<c>MaterialHatch.Aligned</c>):
    /// the longest edge of its largest loop, by direction not sense (degrees), the hatch starting at that edge's start.
    /// </summary>
    static (double Angle, Vec2? Origin) AlongLayer(List<IReadOnlyList<Vec2>> loops)
    {
        IReadOnlyList<Vec2>? outer = null;
        var area = -1.0;
        foreach (var l in loops) { var a = Math.Abs(Polygon2.SignedArea(l)); if (a > area) { area = a; outer = l; } }
        if (outer is not { Count: >= 2 }) return (0, null);
        var best = 0; var bestLen = -1.0;
        for (var i = 0; i < outer.Count; i++)
        {
            var len = (outer[(i + 1) % outer.Count] - outer[i]).Length;
            if (len > bestLen) { bestLen = len; best = i; }
        }
        var d = outer[(best + 1) % outer.Count] - outer[best];
        var along = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
        if (along > 90 + 1e-9 || along <= -90 + 1e-9) along += along > 0 ? -180 : 180;
        return (along, outer[best]);
    }

    // ------------------------------------------------------------ diffs

    public Diff Snapshot()
    {
        ForgetAll();
        var todo = _store.Entities.Where(e => ProducerOf(e) != null).ToList();
        var built = BuildMany(todo);
        var ids = new List<string>();
        for (var i = 0; i < todo.Count; i++) if (built[i] is { Item: not null }) ids.Add(todo[i].Id);
        // Every element built first: each one's edges are trimmed against its neighbours.
        var grid = new BoxGrid(_built);
        var render = EmitMany(ids, grid);
        _meshesSent.Clear();
        return new Diff(++_version, true,
            _store.Entities.Select(Dto).ToList(), [], [], [],
            render, [], State(),
            _store.Relations.Select(Dto).ToList(), [], MeshesFor(render), []);
    }

    /// <summary>
    /// A diff in parts of at most <paramref name="max"/> nodes, render items and relationships each, so
    /// no frame outgrows what one message carries (a million references imported at once). The first part
    /// keeps the reset, the removals and the meshes; each part after it has a version of its own, in order,
    /// so the interface applies them one after the other as ordinary diffs.
    /// </summary>
    public IReadOnlyList<Diff> Parts(Diff d, int max)
    {
        var n = Math.Max(d.Nodes.Count, Math.Max(d.Render.Count, d.Relations.Count));
        if (max <= 0 || n <= max) return [d];
        var o = new List<Diff>();
        for (var k = 0; k * max < n; k++)
        {
            var first = k == 0;
            List<T> Slice<T>(IReadOnlyList<T> l) => [.. l.Skip(k * max).Take(max)];
            o.Add(new Diff(first ? d.Version : ++_version, first && d.Reset, Slice(d.Nodes), first ? d.RemovedNodes : [], first ? d.Edges : [], first ? d.RemovedEdges : [],
                Slice(d.Render), first ? d.RenderRemoved : [], d.State, Slice(d.Relations), first ? d.RemovedRelations : [], first ? d.Meshes : [], first ? d.MeshesRemoved : []));
        }
        return o;
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
        var hadItems = new HashSet<string>();
        foreach (var id in rebuild)
        {
            if (_built.TryGetValue(id, out var old) && old.Item != null) hadItems.Add(id);
            if (old?.Bounds is { } was) boxes.Add(was);
            Forget(id);
        }
        // Built on every core, then taken in the order they came.
        var live = rebuild.Select(_store.Get).OfType<Entity>().ToList();
        var made = BuildMany(live);
        var byId = new Dictionary<string, Built?>();
        for (var i = 0; i < live.Count; i++) byId[live[i].Id] = made[i];
        foreach (var id in rebuild)
        {
            if (byId.GetValueOrDefault(id) is { Item: not null } built)
            {
                emit.Add(id);
                if (built.Bounds is { } now) boxes.Add(now);
            }
            else if (hadItems.Contains(id)) renderRemoved.Add(id);
        }
        var grid = new BoxGrid(_built);
        emit.AddRange(Neighbours(grid, boxes, rebuild.ToHashSet()));
        var render = EmitMany(emit, grid);
        // A shared body gone: its mesh too (its instances went with it, or were rebuilt without it).
        var meshesRemoved = new List<string>();
        foreach (var c in t.Entities)
            if (c.Before?.Class == StoredBodies.SharedClass && _store.Get(c.Id) == null && _meshes.Remove(c.Id)) { _meshesSent.Remove(c.Id); meshesRemoved.Add(c.Id); }
        return new Diff(++_version, false, nodes, removedNodes, [], [], render, renderRemoved, State(), relations, removedRelations, MeshesFor(render), meshesRemoved);
    }

    DocState State() => new(_history.CanUndo, _history.CanRedo, _history.UndoLabel, _history.RedoLabel, _history.Dirty, Path, _store.EntityCount, _store.RelationCount);

    NodeDto Dto(Entity e)
    {
        var props = e.CloneAttrs();
        // A stored body's triangles travel as its render; as props they were the biggest part of an imported model's diff.
        props.Remove(StoredBodies.Key);
        props.Remove(StoredBodies.SolidBodyKey);
        // An instance's mesh and placement travel as its render's (RenderItem.Instance).
        props.Remove(StoredBodies.BodyOf);
        props.Remove(StoredBodies.XfKey);
        // What the file said of a foreign element, kept for the export only.
        props.Remove(IfcSource.Key);
        // A remark's snapshots (a PNG each) are asked for when shown.
        Cde.Remarks.Remarks.Strip(e, props);
        if (e.PredefinedType != null) props["predefinedType"] = e.PredefinedType;
        var owner = _catalog.OwnerOf(e.Class);
        var storey = e.Class == IfcClass.BuildingStorey ? null : Spatial.StoreyOf(_store, e.Id)?.Id;
        return new NodeDto(e.Id, e.Class, e.Name, Spatial.ParentOf(_store, e.Id)?.Id, 0, 0, props, owner != null, owner, storey);
    }

    static RelationDto Dto(Relation r) => new(r.Id, r.Class, r.Relating, r.Related);

    // ------------------------------------------------------------ geometry

    /// <summary>What builds an entity's geometry: a body that came with it (a reference from IFC), else its class's producer.</summary>
    GeometryProducer? ProducerOf(Entity e) => StoredBodies.Has(e) ? StoredBodies.Geometry : _catalog.Geometry(e.Class);

    Built? Build(Entity e) => Compute(e) is { } b ? Register(e, b) : null;

    /// <summary>Below this many, elements are built one after the other: a thread's start costs more than a small edit's builds.</summary>
    const int ParallelFrom = 16;

    /// <summary>Every element built on one core (for tests, and to rule the cores out when something looks wrong).</summary>
    public bool OneCore { get; set; }

    /// <summary>
    /// Elements built on every core (<see cref="Compute"/>: each reads the model through a view of its own,
    /// and nothing edits it meanwhile), then registered in their order. Null where an element has no producer.
    /// </summary>
    Built?[] BuildMany(IReadOnlyList<Entity> es)
    {
        var o = new Built?[es.Count];
        var done = 0;
        var step = Math.Max(1, es.Count / 100);
        void Did() { var n = Interlocked.Increment(ref done); if (Progress != null && es.Count >= ParallelFrom && (n == es.Count || n % step == 0)) Progress("Building the model", n, es.Count); }
        if (OneCore || es.Count < ParallelFrom) for (var i = 0; i < es.Count; i++) { o[i] = Compute(es[i]); Did(); }
        else Parallel.For(0, es.Count, i => { o[i] = Compute(es[i]); Did(); });
        for (var i = 0; i < es.Count; i++) if (o[i] is { } b) o[i] = Register(es[i], b);
        return o;
    }

    /// <summary>The render items of <paramref name="ids"/> with their seams, on every core (each element's against its neighbours, read only), in order.</summary>
    List<RenderItem> EmitMany(IReadOnlyList<string> ids, BoxGrid grid)
    {
        var o = new RenderItem?[ids.Count];
        if (OneCore || ids.Count < ParallelFrom) { var keys = new Dictionary<string, string?>(); for (var i = 0; i < ids.Count; i++) o[i] = Emitted(ids[i], keys, grid); }
        else Parallel.For(0, ids.Count, () => new Dictionary<string, string?>(), (i, _, keys) => { o[i] = Emitted(ids[i], keys, grid); return keys; }, _ => { });
        return [.. o.OfType<RenderItem>()];
    }

    /// <summary>The session's caches take a built element: what it read, so an edit there builds it again.</summary>
    Built Register(Entity e, Built built)
    {
        Forget(e.Id);
        _built[e.Id] = built;
        foreach (var id in built.Reads) (_readers.TryGetValue(id, out var set) ? set : _readers[id] = []).Add(e.Id);
        return built;
    }

    /// <summary>An element's geometry, filters and render, without touching the session's caches (the shared meshes' aside, under their lock): safe on any thread while nothing edits the model.</summary>
    Built? Compute(Entity e)
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
            if (g != null) item = Render(e, g, problems, view);
        }
        catch (Exception ex)
        {
            // A refusal the module meant, or a bug in it (or a solid the tessellator cannot
            // read): the element is shown with its problem, and never takes the diff down with it.
            g = null;
            problems.Add(ex is ModelException or InvalidOperationException or ArgumentException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}");
            item = Render(e, null, problems, view);
        }
        return new Built(g, view.Read, item, BoundsOf(g));
    }

    static (Vec3 Min, Vec3 Max)? BoundsOf(ElementGeometry? g)
    {
        (Vec3 Min, Vec3 Max)? box = null;
        foreach (var p in g?.Parts ?? [])
        {
            if (p.Bounds() is not { } b) continue;
            box = box is { } o
                ? (new Vec3(Math.Min(o.Min.X, b.Min.X), Math.Min(o.Min.Y, b.Min.Y), Math.Min(o.Min.Z, b.Min.Z)), new Vec3(Math.Max(o.Max.X, b.Max.X), Math.Max(o.Max.Y, b.Max.Y), Math.Max(o.Max.Z, b.Max.Z)))
                : b;
        }
        return box;
    }

    static bool Overlaps((Vec3 Min, Vec3 Max) a, (Vec3 Min, Vec3 Max) b, double tol = 1e-3) =>
        a.Min.X <= b.Max.X + tol && b.Min.X <= a.Max.X + tol && a.Min.Y <= b.Max.Y + tol && b.Min.Y <= a.Max.Y + tol && a.Min.Z <= b.Max.Z + tol && b.Min.Z <= a.Max.Z + tol;

    /// <summary>The built elements whose box touches one of <paramref name="boxes"/>, other than <paramref name="except"/>.</summary>
    static IEnumerable<string> Neighbours(BoxGrid grid, IReadOnlyList<(Vec3 Min, Vec3 Max)> boxes, ICollection<string> except) =>
        grid.Near(boxes).Where(n => n.Built.Item != null && !except.Contains(n.Id)).Select(n => n.Id);

    /// <summary>
    /// The built elements' boxes on a grid in plan, so the elements near a box are found
    /// without going through every element (each one against all was quadratic: a hundred
    /// thousand references imported at once took minutes). What it finds comes in the
    /// order of <see cref="_built"/>, as the plain search gave it.
    /// </summary>
    sealed class BoxGrid
    {
        const int MaxCells = 256;   // a box over more cells (a terrain, a site) is kept apart and always tried
        readonly List<(string Id, Built Built, (Vec3 Min, Vec3 Max) Box)> _items = [];
        readonly Dictionary<(int X, int Y), List<int>> _cells = [];
        readonly List<int> _large = [];
        readonly double _cell;

        public BoxGrid(IEnumerable<KeyValuePair<string, Built>> built)
        {
            foreach (var (id, b) in built) if (b.Bounds is { } box) _items.Add((id, b, box));
            // Twice the median element's larger side in plan: most fall in one to four cells.
            var sides = _items.Select(i => Math.Max(i.Box.Max.X - i.Box.Min.X, i.Box.Max.Y - i.Box.Min.Y)).Where(double.IsFinite).Order().ToList();
            _cell = sides.Count == 0 ? 1 : Math.Max(2 * sides[sides.Count / 2], 0.05);
            for (var k = 0; k < _items.Count; k++)
            {
                if (Range(_items[k].Box) is not { } r) { _large.Add(k); continue; }
                var (x0, y0, x1, y1) = r;
                for (var x = x0; x <= x1; x++)
                    for (var y = y0; y <= y1; y++)
                        (_cells.TryGetValue((x, y), out var l) ? l : _cells[(x, y)] = []).Add(k);
            }
        }

        /// <summary>The cells a box (with the overlap's tolerance) falls in; null when too many, or not finite.</summary>
        (int, int, int, int)? Range((Vec3 Min, Vec3 Max) b)
        {
            const double tol = 1e-3;
            double x0 = Math.Floor((b.Min.X - tol) / _cell), y0 = Math.Floor((b.Min.Y - tol) / _cell);
            double x1 = Math.Floor((b.Max.X + tol) / _cell), y1 = Math.Floor((b.Max.Y + tol) / _cell);
            if (!double.IsFinite(x0 + y0 + x1 + y1) || Math.Abs(x0) > 1e9 || Math.Abs(y0) > 1e9 || Math.Abs(x1) > 1e9 || Math.Abs(y1) > 1e9) return null;
            if ((x1 - x0 + 1) * (y1 - y0 + 1) > MaxCells) return null;
            return ((int)x0, (int)y0, (int)x1, (int)y1);
        }

        /// <summary>The elements whose box touches one of <paramref name="boxes"/>.</summary>
        public IEnumerable<(string Id, Built Built)> Near(IReadOnlyList<(Vec3 Min, Vec3 Max)> boxes)
        {
            if (boxes.Count == 0) return [];
            var found = new HashSet<int>();
            void Try(int k, (Vec3 Min, Vec3 Max) box) { if (!found.Contains(k) && Overlaps(box, _items[k].Box)) found.Add(k); }
            foreach (var box in boxes)
            {
                // A box over many cells is rare: tried against everything, as before.
                if (Range(box) is not { } r) { for (var k = 0; k < _items.Count; k++) Try(k, box); continue; }
                var (x0, y0, x1, y1) = r;
                foreach (var k in _large) Try(k, box);
                for (var x = x0; x <= x1; x++)
                    for (var y = y0; y <= y1; y++)
                        if (_cells.TryGetValue((x, y), out var l))
                            foreach (var k in l) Try(k, box);
            }
            return found.Order().Select(k => (_items[k].Id, _items[k].Built));
        }
    }

    /// <summary>
    /// An element's render item as the interface gets it: its parts' edges less
    /// the seams, where a surface goes on, in its own plane, into a neighbour's
    /// part of the same material (a wall's render over the storey above; see
    /// <see cref="Seams"/>). The same rule as the elevations' and the plan's:
    /// the elements stay apart, the material reads as one.
    /// </summary>
    /// <summary>
    /// The seams join Lapin's own elements (a render going on from one storey's wall into the next):
    /// not a reference's body, read from a file as triangles (a Revit body of millions tried every
    /// neighbour's edges against its planes: minutes), nor a part of more faces than this.
    /// </summary>
    const int SeamedUpTo = 20_000;

    static bool Seamed(Entity? e) => e != null && !StoredBodies.Has(e);

    RenderItem? Emitted(string id, Dictionary<string, string?> keys, BoxGrid grid)
    {
        if (!_built.TryGetValue(id, out var b) || b.Item is not { } item) return null;
        if (b.Geometry is not { } g || b.Bounds is not { } box || item.Parts is not { Count: > 0 } parts || parts.Count != g.Parts.Count) return item;
        if (!Seamed(_store.Get(id))) return item;
        var cls = _store.Get(id)?.Class ?? item.Type;
        var near = new List<(Built Built, string Class)>();
        foreach (var (nid, nb) in grid.Near([box]))
            if (nid != id && nb.Geometry != null && Seamed(_store.Get(nid))) near.Add((nb, _store.Get(nid)?.Class ?? nb.Item?.Type ?? ""));
        if (near.Count == 0) return item;
        List<Seams.Plane> PlanesOf(Solid s) => _planes.GetOrAdd(s, Seams.Planes);
        List<RenderPart>? trimmed = null;
        for (var i = 0; i < parts.Count; i++)
        {
            var part = g.Parts[i];
            if (part.Mesh != null || part.FaceCount > SeamedUpTo) continue;
            var key = Views.SeamKeyOf(_store, part.Material, cls, keys);
            if (key == null || part.Bounds() is not { } pbox) continue;
            var onward = new List<Seams.Plane>();
            foreach (var (nb, ncls) in near)
                foreach (var q in nb.Geometry!.Parts)
                    if (q.Mesh == null && q.FaceCount <= SeamedUpTo && Views.SeamKeyOf(_store, q.Material, ncls, keys) == key && q.Bounds() is { } qbox && Overlaps(pbox, qbox)) onward.AddRange(PlanesOf(q.Solid));
            if (onward.Count == 0) continue;
            trimmed ??= [.. parts];
            trimmed[i] = parts[i] with { Edges = Seams.Flat(Seams.Edges(part.Solid, onward)) };
        }
        return trimmed == null ? item : item with { Parts = trimmed };
    }

    void Forget(string id)
    {
        if (!_built.Remove(id, out var old)) return;
        foreach (var p in old.Geometry?.Parts ?? []) _planes.TryRemove(p.Solid, out _);
        foreach (var r in old.Reads)
            if (_readers.TryGetValue(r, out var set)) { set.Remove(id); if (set.Count == 0) _readers.Remove(r); }
    }

    /// <summary>A shared body's mesh: tessellated once for as long as the body is the same.</summary>
    SharedMesh? MeshOf(string bodyId, Entity e, IModelView read, Dictionary<string, IReadOnlyList<LayerBand>?> bands)
    {
        if (read.Get(bodyId) is not { Class: StoredBodies.SharedClass } shared || shared.Attrs[StoredBodies.Key] is not JsonObject body) return null;
        lock (_meshes) return MeshOfLocked(bodyId, shared, body, e, read, bands);
    }

    SharedMesh MeshOfLocked(string bodyId, Entity shared, JsonObject body, Entity e, IModelView read, Dictionary<string, IReadOnlyList<LayerBand>?> bands)
    {
        if (_meshes.TryGetValue(bodyId, out var known) && ReferenceEquals(known.Body, body)) return known.Mesh;
        var parts = new List<RenderPart>();
        (Vec3 Min, Vec3 Max)? box = null;
        foreach (var p in StoredBodies.SharedParts(shared))
        {
            var m = p.Tessellate();
            parts.Add(new RenderPart(p.Name, p.Color, m.Positions, m.Normals, m.Indices, m.Edges, p.Material, ViewStyles.HiddenBy(read, e.Id, p, bands)));
            if (p.Bounds() is { } b) box = box is { } o ? (new Vec3(Math.Min(o.Min.X, b.Min.X), Math.Min(o.Min.Y, b.Min.Y), Math.Min(o.Min.Z, b.Min.Z)), new Vec3(Math.Max(o.Max.X, b.Max.X), Math.Max(o.Max.Y, b.Max.Y), Math.Max(o.Max.Z, b.Max.Z))) : b;
        }
        var (lo, hi) = box ?? (Vec3.Zero, Vec3.Zero);
        var mesh = new SharedMesh(bodyId, parts, [(float)lo.X, (float)lo.Y, (float)lo.Z, (float)hi.X, (float)hi.Y, (float)hi.Z]);
        _meshes[bodyId] = (body, mesh);
        _meshesSent.Remove(bodyId);
        return mesh;
    }

    /// <summary>The meshes <paramref name="render"/>'s instances use that the interface does not have yet; marked as sent.</summary>
    List<SharedMesh> MeshesFor(IEnumerable<RenderItem> render)
    {
        var o = new List<SharedMesh>();
        foreach (var r in render)
            if (r.Instance is { } i && !_meshesSent.Contains(i.Mesh) && _meshes.TryGetValue(i.Mesh, out var m)) { o.Add(m.Mesh); _meshesSent.Add(i.Mesh); }
        return o;
    }

    void ForgetAll()
    {
        _built.Clear();
        _readers.Clear();
        _planes.Clear();
    }

    /// <summary>
    /// An element's render item: each part tessellated, with the layers modes a 3D view
    /// leaves it out by (<see cref="ViewStyles.HiddenBy"/>), read through the build's
    /// view so a material's category changed builds the element again.
    /// </summary>
    RenderItem Render(Entity e, ElementGeometry? g, List<string> problems, IModelView read)
    {
        var parts = new List<RenderPart>();
        var bands = new Dictionary<string, IReadOnlyList<LayerBand>?>();
        if (g?.Instance is { } inst && MeshOf(inst.Body, e, read, bands) is { } mesh)
        {
            var plan2d = (g.Plan ?? []).Select(i => new PlanItemDto(i.Kind, i.Closed, i.Points.SelectMany(v => new[] { v.X, v.Y }).ToArray())).ToList();
            return new RenderItem(e.Id, e.Class, Spatial.StoreyOf(_store, e.Id)?.Id, _catalog.OwnerOf(e.Class) != null,
                mesh.Parts.FirstOrDefault()?.Color ?? "#c9ced6", [], [], [], [], plan2d, [],
                [.. problems, .. g.Diagnostics ?? []], [], new RenderInstance(inst.Body, [.. inst.Xf.Select(v => (float)v)]));
        }
        foreach (var p in g?.Parts ?? [])
        {
            var m = p.Tessellate();
            parts.Add(new RenderPart(p.Name, p.Color, m.Positions, m.Normals, m.Indices, m.Edges, p.Material, ViewStyles.HiddenBy(read, e.Id, p, bands)));
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
        public string? Title => s.Path is { Length: > 0 } p ? System.IO.Path.GetFileNameWithoutExtension(p.Replace('\\', '/').Split('/')[^1]) : null;
    }
}

/// <summary>A change of the project's history (<see cref="Session.HistoryChanged"/>).</summary>
/// <summary>
/// A change of the project's history. <see cref="Replay"/>: an edit the journal recorded by its source rather than its changes (an
/// import from a file), done again from it on recovery.
/// </summary>
public sealed record HistoryEvent(string Kind, Transaction? Transaction = null, string? Author = null, Action<Session>? Replay = null);

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
