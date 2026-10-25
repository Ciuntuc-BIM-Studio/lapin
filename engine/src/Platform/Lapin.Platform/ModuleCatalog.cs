using BubbleBim.Geometry;
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
    readonly Dictionary<string, (int Priority, EnvelopeProducer Envelope)> _cutters = [];
    readonly Dictionary<string, (ClassDescriptor Descriptor, string Module)> _classes = [];
    readonly Dictionary<string, CommandEntry> _commands = [];
    readonly Dictionary<string, QueryHandler> _queries = [];
    readonly Dictionary<string, GeometryProducer> _geometry = [];
    readonly Dictionary<string, QuantityProducer> _quantities = [];
    readonly Dictionary<string, JsonNode> _catalogs = [];
    readonly List<(Type Contract, object Implementation)> _services = [];
    readonly List<ILapinModule> _modules = [];

    public IReadOnlyList<ILapinModule> Modules => _modules;
    public IEnumerable<(ClassDescriptor Descriptor, string Module)> Classes => _classes.Values;
    public IEnumerable<CommandEntry> Commands => _commands.Values;
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
        return c;
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
        if (PriorityOf(target, model) is not { } mine) return [];
        var o = new List<Solid>();
        foreach (var (cls, (_, envelope)) in _cutters)
        {
            if (ClassPriority(cls, model) is not { } classPriority) continue;
            // A class ranked under the target's: only its elements raised over the target by their own priority
            // (read under a narrow key, so a wall does not rebuild on every edit of every other wall).
            // Priorities by material: any element may rank over its class, read whole.
            IEnumerable<Entity> candidates = classPriority > mine || CutOrder.MaterialsOf(model) != null ? model.OfClass(cls)
                : model is TrackingView tv ? tv.OfClassUnder(cls, TrackingView.CutKey(cls)).Where(e => Raised(e, model, mine)).Select(e => model.Get(e.Id)!)
                : model.OfClass(cls).Where(e => Raised(e, model, mine));
            foreach (var e in candidates)
            {
                if (e.Id == target.Id || PriorityOf(e, model) is not { } theirs || theirs <= mine) continue;
                if (envelope(e, model) is not { } solid || Measure.Bounds(solid) is not { } b) continue;
                const double eps = 1e-6;
                if (b.Min.X >= bounds.Max.X - eps || b.Max.X <= bounds.Min.X + eps || b.Min.Y >= bounds.Max.Y - eps || b.Max.Y <= bounds.Min.Y + eps || b.Min.Z >= bounds.Max.Z - eps || b.Max.Z <= bounds.Min.Z + eps) continue;
                o.Add(solid);
            }
        }
        return o;
    }
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

        public void Cutter(string ifcClass, int priority, EnvelopeProducer envelope)
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
    }
}
