using Lapin.Geometry;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>A command as the catalog keeps it: who brought it, and its handler.</summary>
public sealed record CommandEntry(string Name, string Label, string Module, CommandHandler Handler);

/// <summary>
/// Everything the product's modules registered: their classes, commands,
/// queries, geometry, catalogues and services. Built once, at start. The
/// platform's own module, <see cref="CoreModule"/>, is always first.
/// </summary>
public sealed class ModuleCatalog : IServices, ICutters, IPathElements, IGeometryProducers
{
    readonly Dictionary<string, PathFrame> _paths = [];
    readonly Dictionary<string, (int Priority, EnvelopeProducer? Envelope)> _cutters = [];
    readonly Dictionary<string, (ClassDescriptor Descriptor, string Module)> _classes = [];
    readonly Dictionary<string, CommandEntry> _commands = [];
    readonly Dictionary<string, QueryHandler> _queries = [];
    readonly Dictionary<string, GeometryProducer> _geometry = [];
    readonly Dictionary<string, QuantityProducer> _quantities = [];
    readonly Dictionary<string, JsonNode> _catalogs = [];
    readonly List<(Type Contract, object Implementation)> _services = [];
    readonly List<ILapinModule> _modules = [];
    readonly List<(OverrideInfo Info, Delegate Handler)> _pending = [];
    readonly List<OverrideInfo> _overrides = [];

    public IReadOnlyList<ILapinModule> Modules => _modules;
    /// <summary>What the modules override of each other's (<see cref="IModuleRegistry.Override"/>), in load order: none in the editions of this repository.</summary>
    public IReadOnlyList<OverrideInfo> Overrides => _overrides;
    public IEnumerable<(ClassDescriptor Descriptor, string Module)> Classes => _classes.Values;
    public IEnumerable<CommandEntry> Commands => _commands.Values;
    /// <summary>Every query's full name ("schedule.csv").</summary>
    public IEnumerable<string> QueryNames => _queries.Keys;
    public IReadOnlyDictionary<string, JsonNode> Catalogs => _catalogs;

    public static ModuleCatalog Create(IEnumerable<ILapinModule> modules)
    {
        var c = new ModuleCatalog();
        foreach (var m in modules.Prepend(new CoreModule()))
        {
            if (c._modules.Any(x => x.Id == m.Id)) throw new InvalidOperationException($"Module „{m.Id}” is loaded twice.");
            c._modules.Add(m);
            m.Register(new Registry(c, m.Id));
        }
        // Overrides wrap what every module registered, whichever loaded first: each one round the one before it.
        foreach (var (info, handler) in c._pending) c.Wrap(info, handler);
        c._pending.Clear();
        // What modules this edition lacks own in a project is kept as it is (ModuleOwned), asked after every module.
        var absent = new AbsentModules(c._modules.Select(x => x.Id).ToHashSet());
        c._services.Add((typeof(IDrivenElements), absent));
        c._services.Add((typeof(IIfcExportFilter), absent));
        return c;
    }

    void Wrap(OverrideInfo o, Delegate handler)
    {
        if (o.Kind == "command")
        {
            if (!_commands.TryGetValue(o.Name, out var entry))
                throw new InvalidOperationException($"„{o.Module}” overrides the command {o.Name}, which no module of this product registers.");
            var inner = entry.Handler;
            var wrap = (CommandOverride)handler;
            _commands[o.Name] = entry with { Handler = (ctx, args) => wrap(ctx, args, a => inner(ctx, a)) };
        }
        else
        {
            if (!_queries.TryGetValue(o.Name, out var inner))
                throw new InvalidOperationException($"„{o.Module}” overrides the query {o.Name}, which no module of this product registers.");
            var wrap = (QueryOverride)handler;
            _queries[o.Name] = (ctx, args) => wrap(ctx, args, a => inner(ctx, a));
        }
        _overrides.Add(o);
    }

    /// <summary>The module that authors <paramref name="ifcClass"/>, or null: a class no module of this product knows (kept, shown, never edited).</summary>
    public string? OwnerOf(string ifcClass) =>
        _classes.TryGetValue(ifcClass, out var c) ? c.Module : CoreModule.Owns(ifcClass) ? CoreModule.ModuleId : null;

    public ClassDescriptor? Class(string ifcClass) => _classes.TryGetValue(ifcClass, out var c) ? c.Descriptor : null;
    public CommandEntry? Command(string name) => _commands.GetValueOrDefault(name);
    public QueryHandler? Query(string name) => _queries.GetValueOrDefault(name);
    public GeometryProducer? Geometry(string ifcClass) => _geometry.GetValueOrDefault(ifcClass);
    public QuantityProducer? QuantitiesOf(string ifcClass) => _quantities.GetValueOrDefault(ifcClass);

    public T? Get<T>() where T : class =>
        typeof(T) == typeof(ICutters) || typeof(T) == typeof(IPathElements) || typeof(T) == typeof(IGeometryProducers) ? (T)(object)this : _services.Where(s => s.Contract == typeof(T)).Select(s => (T)s.Implementation).FirstOrDefault();

    public bool IsPath(string ifcClass) => _paths.ContainsKey(ifcClass);
    public JoinWall? FrameOf(Entity entity, IModelView model) => _paths.TryGetValue(entity.Class, out var f) ? f(entity, model) : null;

    /// <summary>The classes that take part in cutting, with the priority each module gives them.</summary>
    public IReadOnlyDictionary<string, int> CutDefaults => _cutters.ToDictionary(x => x.Key, x => x.Value.Priority);

    /// <summary>A class's cut priority in this project: the project's cut order (<see cref="CutOrder"/>), else its module's; null when it takes no part.</summary>
    public int? ClassPriority(string ifcClass, IModelView model)
    {
        if (!_cutters.TryGetValue(ifcClass, out var c)) return null;
        return CutOrder.Of(model) is { } table && Json.Number(table[ifcClass]) is { } own ? (int)own : c.Priority;
    }

    /// <summary>
    /// An entity's cut priority: its own <c>cutPriority</c>, else the class it
    /// is written as (a Studio solid exported as a slab ranks as slabs do), else
    /// its class's, in this project; null when its class takes no part.
    /// </summary>
    public int? PriorityOf(Entity e, IModelView model)
    {
        if (ClassPriority(e.Class, model) is not { } own) return null;
        var asClass = e.Str("exportAs") is { } x ? ClassPriority(x, model) : null;
        return (int)(e.NumOrNull("cutPriority") ?? CutOrder.ByMaterial(e, model) ?? asClass ?? own);
    }

    /// <summary>An element ranked over a priority by its own priority, its material or the class it is written as.</summary>
    bool Raised(Entity e, IModelView model, int mine) =>
        e.NumOrNull("cutPriority") > mine || (e.Str("exportAs") is { } x && ClassPriority(x, model) > mine) || CutOrder.ByMaterial(e, model) > mine;

    public IReadOnlyList<Solid> For(Entity target, IModelView model, (Vec3 Min, Vec3 Max) bounds)
    {
        // An element of a material that stays out of joins (forum topic 59) is cut by nothing, and cuts nothing (below).
        if (PriorityOf(target, model) is not { } mine || !Materials.Joins(model, target)) return [];
        // Elements joined to the target (two walls at a corner, a T or a crossing) settle their meeting layer by layer
        // in their join (testers' report #81): the higher one's envelope, square at its axis' end, would cut into the other's core.
        var joined = IsPath(target.Class) ? Joins.Of(model, target.Id).Select(r => Joins.Other(r, target.Id)).ToHashSet() : [];
        var o = new List<Solid>();
        foreach (var (cls, (_, envelope)) in _cutters)
        {
            // A class that ranks without an envelope (MEP runs) cuts by its own means: not read here.
            if (envelope == null || ClassPriority(cls, model) is not { } classPriority) continue;
            // Who may cut is worked out once for all the walls built at one revision (see CandidatesOf), not once for each of them.
            if (CacheCutters && model is TrackingView { Inner: ModelStore store } view)
            {
                var whole = classPriority > mine || CutOrder.MaterialsOf(model) != null;
                var list = CandidatesOf(store, cls, envelope, whole);
                // What the search reads, as the plain loop below notes it: the class's listing, and what working out its cutters read.
                // Past NearReads.WholeUpTo of them, only the cutters round the target (report #148: every beam read every column, a
                // frame of thousands built in a time growing with its square); one elsewhere that comes near, or leaves, is built
                // again where it was and is, which touches the cells read here.
                view.Read.Add(whole ? TrackingView.ClassKey(cls) : TrackingView.CutKey(cls));
                var near = list.Count > NearReads.WholeUpTo;
                if (near)
                {
                    view.Read.Add(NearReads.Any(cls));
                    foreach (var k in NearReads.Cells(cls, bounds)) view.Read.Add(k);
                }
                else view.Read.UnionWith(list.Reads);
                foreach (var c in near ? list.Near(bounds) : list.All)
                {
                    const double tolerance = 1e-6;
                    if (c.Bounds is not { } cb || cb.Min.X >= bounds.Max.X - tolerance || cb.Max.X <= bounds.Min.X + tolerance || cb.Min.Y >= bounds.Max.Y - tolerance || cb.Max.Y <= bounds.Min.Y + tolerance || cb.Min.Z >= bounds.Max.Z - tolerance || cb.Max.Z <= bounds.Min.Z + tolerance) continue;
                    if (near) view.Read.UnionWith(c.Reads);
                    if (c.Entity.Id == target.Id || joined.Contains(c.Entity.Id) || c.Priority is not { } theirs || theirs <= mine || !c.Joins) continue;
                    if (!whole && !Raised(c.Entity, model, mine)) continue;
                    if (c.Solid is null) continue;
                    o.Add(c.Solid);
                }
                continue;
            }
            // A class ranked under the target's: only its elements raised over the target by their own priority
            // (read under a narrow key, so a wall does not rebuild on every edit of every other wall).
            // Priorities by material: any element may rank over its class, read whole.
            IEnumerable<Entity> candidates = classPriority > mine || CutOrder.MaterialsOf(model) != null ? model.OfClass(cls)
                : model is TrackingView tv ? tv.OfClassUnder(cls, TrackingView.CutKey(cls)).Where(e => Raised(e, model, mine)).Select(e => model.Get(e.Id)!)
                : model.OfClass(cls).Where(e => Raised(e, model, mine));
            foreach (var e in candidates)
            {
                if (e.Id == target.Id || joined.Contains(e.Id) || PriorityOf(e, model) is not { } theirs || theirs <= mine || !Materials.Joins(model, e)) continue;
                if (envelope(e, model) is not { } solid || Measure.Bounds(solid) is not { } b) continue;
                const double eps = 1e-6;
                if (b.Min.X >= bounds.Max.X - eps || b.Max.X <= bounds.Min.X + eps || b.Min.Y >= bounds.Max.Y - eps || b.Max.Y <= bounds.Min.Y + eps || b.Min.Z >= bounds.Max.Z - eps || b.Max.Z <= bounds.Min.Z + eps) continue;
                o.Add(solid);
            }
        }
        return o;
    }
    /// <summary>
    /// Whether the cutters of a class are worked out once per model revision (decision 423). Off only to build as it was, for the
    /// tests that compare the two (<c>SessionTuning.Reference</c>).
    /// </summary>
    public bool CacheCutters { get; set; } = true;

    sealed record Candidate(Entity Entity, int? Priority, Solid? Solid, (Vec3 Min, Vec3 Max)? Bounds, HashSet<string> Reads, bool Joins = true);

    /// <summary>A class's possible cutters at a revision, found by the cells in plan their boxes fall in (<see cref="NearReads.Size"/>).</summary>
    sealed class Candidates
    {
        readonly Dictionary<(long X, long Y), List<Candidate>> _cells;
        readonly List<Candidate> _wide;

        public Candidates(List<Candidate> all)
        {
            All = all;
            _cells = Index(all, out _wide);
            // A few: every reader notes what all of them read (NearReads.WholeUpTo).
            if (all.Count <= NearReads.WholeUpTo) foreach (var c in all) Reads.UnionWith(c.Reads);
        }

        public List<Candidate> All { get; }
        public int Count => All.Count;
        /// <summary>What working them all out read, for a few of them; empty past <see cref="NearReads.WholeUpTo"/>.</summary>
        public HashSet<string> Reads { get; } = [];

        static (long, long, long, long)? Span((Vec3 Min, Vec3 Max) b)
        {
            double x0 = Math.Floor(b.Min.X / NearReads.Size), y0 = Math.Floor(b.Min.Y / NearReads.Size), x1 = Math.Floor(b.Max.X / NearReads.Size), y1 = Math.Floor(b.Max.Y / NearReads.Size);
            if (!double.IsFinite(x0 + y0 + x1 + y1) || (x1 - x0 + 1) * (y1 - y0 + 1) > 4096) return null;
            return ((long)x0, (long)y0, (long)x1, (long)y1);
        }

        static Dictionary<(long, long), List<Candidate>> Index(List<Candidate> all, out List<Candidate> wide)
        {
            var o = new Dictionary<(long, long), List<Candidate>>();
            wide = [];
            foreach (var c in all)
            {
                if (c.Bounds is not { } b) continue;
                if (Span(b) is not var (x0, y0, x1, y1)) { wide.Add(c); continue; }
                for (var x = x0; x <= x1; x++)
                    for (var y = y0; y <= y1; y++)
                        (o.TryGetValue((x, y), out var l) ? l : o[(x, y)] = []).Add(c);
            }
            return o;
        }

        /// <summary>The cutters whose boxes may meet <paramref name="b"/>, each once; those with no box are none of them.</summary>
        public IEnumerable<Candidate> Near((Vec3 Min, Vec3 Max) b)
        {
            if (Span(b) is not var (x0, y0, x1, y1)) { foreach (var c in _cells.Values.SelectMany(l => l).Concat(_wide).Distinct()) yield return c; yield break; }
            var seen = new HashSet<Candidate>(ReferenceEqualityComparer.Instance);
            for (var x = x0; x <= x1; x++)
                for (var y = y0; y <= y1; y++)
                    if (_cells.TryGetValue((x, y), out var l))
                        foreach (var c in l) if (seen.Add(c)) yield return c;
            foreach (var c in _wide) if (seen.Add(c)) yield return c;
        }
    }
    sealed class CandidateCache(ModelStore store, long revision)
    {
        public readonly ModelStore Store = store;
        public readonly long Revision = revision;
        public readonly System.Collections.Concurrent.ConcurrentDictionary<(string, bool), Lazy<Candidates>> Lists = new();
    }
    CandidateCache? _candidates;

    /// <summary>
    /// A class's possible cutters at this revision, each with its priority, envelope and box, and what working them out read: all
    /// of the class, or (not <paramref name="whole"/>) only those with a priority or a class of their own, which are the only ones
    /// <see cref="Raised"/> can pass when the project ranks no material. Every wall built at the revision shares one list; without it each
    /// wall sorted the slabs again and rebuilt every envelope (28% of a type change on 4,440 walls, with 3,240 following).
    /// </summary>
    Candidates CandidatesOf(ModelStore store, string cls, EnvelopeProducer envelope, bool whole)
    {
        var cache = Volatile.Read(ref _candidates);
        if (cache == null || !ReferenceEquals(cache.Store, store) || cache.Revision != store.Revision)
        {
            cache = new CandidateCache(store, store.Revision);
            Volatile.Write(ref _candidates, cache);
        }
        return cache.Lists.GetOrAdd((cls, whole), _ => new Lazy<Candidates>(() =>
        {
            var all = new List<Candidate>();
            foreach (var e in store.OfClass(cls))
            {
                if (!whole && !e.Attrs.ContainsKey("cutPriority") && !e.Attrs.ContainsKey("exportAs")) continue;
                // What each one read, for the targets it may cut to note (only those it is near).
                var rec = new TrackingView(store);
                rec.Get(e.Id);
                var priority = PriorityOf(e, rec);
                var joins = priority == null || Materials.Joins(rec, e);
                var solid = priority != null && joins ? envelope(e, rec) : null;
                all.Add(new Candidate(e, priority, solid, solid != null ? Measure.Bounds(solid) : null, rec.Read, joins));
            }
            return new Candidates(all);
        })).Value;
    }

    /// <summary>The cutters' list is let go once the builds that used it are done: it holds a solid for each possible cutter.</summary>
    public void ForgetCutters() => Volatile.Write(ref _candidates, null);

    /// <summary>Whether a class ranks in the cut order but cuts by provision openings (MEP runs, decision 293): its edits build again the hosts near it (decision 296).</summary>
    public bool CutsByProvisions(string ifcClass) => _cutters.TryGetValue(ifcClass, out var c) && c.Envelope == null;
    public Solid? EnvelopeOf(Entity element, IModelView model) => _cutters.TryGetValue(element.Class, out var c) ? c.Envelope?.Invoke(element, model) : null;

    public IReadOnlyList<T> All<T>() where T : class => _services.Where(s => s.Contract == typeof(T)).Select(s => (T)s.Implementation).ToList();

    sealed class Registry(ModuleCatalog c, string module) : IModuleRegistry
    {
        string Qualified(string name) => name.Contains('.') ? name : $"{module}.{name}";

        public void Class(ClassDescriptor d)
        {
            if (c._classes.TryGetValue(d.Class, out var owner)) throw new InvalidOperationException($"{d.Class} is owned by „{owner.Module}”; „{module}” cannot own it too.");
            if (module != CoreModule.ModuleId && CoreModule.Owns(d.Class)) throw new InvalidOperationException($"{d.Class} belongs to the platform.");
            c._classes[d.Class] = (d, module);
        }

        public void Command(string name, string label, CommandHandler handler)
        {
            var q = Qualified(name);
            if (!c._commands.TryAdd(q, new CommandEntry(q, label, module, handler))) throw new InvalidOperationException($"Command {q} is registered twice.");
        }

        public void Query(string name, QueryHandler handler)
        {
            var q = Qualified(name);
            if (!c._queries.TryAdd(q, handler)) throw new InvalidOperationException($"Query {q} is registered twice.");
        }

        public void Geometry(string ifcClass, GeometryProducer producer)
        {
            if (c.OwnerOf(ifcClass) != module) throw new InvalidOperationException($"„{module}” builds the geometry of {ifcClass}, which it does not own.");
            c._geometry[ifcClass] = producer;
        }

        public void Quantities(string ifcClass, QuantityProducer producer)
        {
            if (c.OwnerOf(ifcClass) != module) throw new InvalidOperationException($"„{module}” measures {ifcClass}, which it does not own.");
            c._quantities[ifcClass] = producer;
        }

        public void Cutter(string ifcClass, int priority, EnvelopeProducer? envelope)
        {
            if (c.OwnerOf(ifcClass) != module) throw new InvalidOperationException($"„{module}” gives cut priorities to {ifcClass}, which it does not own.");
            c._cutters[ifcClass] = (priority, envelope);
        }

        public void PathElement(string ifcClass, PathFrame frame)
        {
            if (c.OwnerOf(ifcClass) != module) throw new InvalidOperationException($"„{module}” makes {ifcClass} a path element, which it does not own.");
            c._paths[ifcClass] = frame;
        }

        public void Catalog(string name, JsonNode data) => c._catalogs[Qualified(name)] = data;

        public void Service<T>(T implementation) where T : class => c._services.Add((typeof(T), implementation));

        public void Override(string command, CommandOverride handler) => c._pending.Add((new OverrideInfo("command", Full(command, "command"), module), handler));

        public void OverrideQuery(string query, QueryOverride handler) => c._pending.Add((new OverrideInfo("query", Full(query, "query"), module), handler));

        string Full(string name, string kind) => name.Contains('.') ? name
            : throw new InvalidOperationException($"„{module}” overrides the {kind} „{name}”: name it in full, with its module („architecture.addWall”).");
    }
}
