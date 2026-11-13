using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Graphs;

/// <summary>What one node gave: its outputs by port, or why it failed; how many items it ran and how many failed.</summary>
public sealed class NodeResult
{
    public Dictionary<string, object?> Outputs { get; } = new(StringComparer.Ordinal);
    public string? Error { get; set; }
    public string? Warning { get; set; }
    public int Items { get; set; } = 1;
    public int Failed { get; set; }
    public bool IsCommand { get; init; }

    public JsonObject ToJson()
    {
        var o = new JsonObject();
        var outs = new JsonObject();
        foreach (var (k, v) in Outputs) outs[k] = Values.ToDisplay(v);
        o["outputs"] = outs;
        if (Error != null) o["error"] = Error;
        if (Warning != null) o["warning"] = Warning;
        if (Items != 1) o["items"] = Items;
        // How many values each output holds, so the interface draws a list's wire thicker (§3).
        var counts = new JsonObject();
        foreach (var (k, v) in Outputs) if (v is List<object?> l) counts[k] = l.Count;
        if (counts.Count > 0) o["counts"] = counts;
        return o;
    }
}

/// <summary>A graph stopped as a whole (its time or its calls ran out): not one item's failure, so never caught per item.</summary>
public sealed class GraphStopped(string message) : ModelException(message);

/// <summary>
/// A graph evaluated (docs/plan-visual-graphs.md §3): its nodes in the order their wires give, each fed by its wires, else
/// its constants, else its ports' defaults. A list on an input that takes one value maps the node over it, in parallel
/// with the other inputs' lists by the longest (a shorter list repeats its last item), one level at a time; a node that
/// fails says why and the nodes after it say which input has nothing, while the rest goes on. Command nodes run through
/// <paramref name="commands"/> (a copy of the model for a preview, the command's own context for Bake); without one they
/// are not run.
/// </summary>
public sealed class Evaluator(GraphDoc doc, IModelView model, ModuleCatalog catalog, TrackingContext? commands, IQueryContext query, TimeSpan budget)
{
    /// <summary>
    /// The storey a command node without one of its own works on (the view's, as a tool draws on the storey shown): set as its
    /// <c>storeyId</c> when its inputs have none. Null: each command's own default (the first storey).
    /// </summary>
    public string? Storey { get; init; }

    public Dictionary<string, NodeResult> Results { get; } = new(StringComparer.Ordinal);
    /// <summary>What the commands made, in order (elements first: what has a geometry).</summary>
    public List<string> Made { get; } = [];
    public int Calls { get; private set; }
    readonly Stopwatch _clock = Stopwatch.StartNew();

    public const string CommandPrefix = "command:";
    public const string QueryPrefix = "query:";

    NodeResult RunNode(GraphNode node)
    {
        var isCommand = node.Kind.StartsWith(CommandPrefix, StringComparison.Ordinal);
        var result = new NodeResult { IsCommand = isCommand };
        try
        {
            var (inputs, whole, outputs, run) = Prepare(node);
            var (values, items, failed, first) = Map(run, inputs, whole, outputs.Length);
            for (var i = 0; i < outputs.Length; i++) result.Outputs[outputs[i]] = values[i];
            result.Items = items;
            result.Failed = failed;
            if (failed > 0 && failed == items) throw new CommandException(first!);
            if (failed > 0) result.Warning = $"{failed} of {items} failed: {first}";
            foreach (var (port, v) in result.Outputs)
                if (Values.Size(v) > GraphLimits.MaxItems * 2) throw new CommandException($"„{port}” holds more than {GraphLimits.MaxItems} items.");
        }
        catch (GraphStopped e) { result.Outputs.Clear(); result.Error = e.Message; throw new Stop(result); }
        catch (Exception e) when (e is not Stop)
        {
            result.Outputs.Clear();
            result.Error = Message(e);
        }
        return result;
    }

    /// <summary>Thrown to end the run at a node whose time or calls ran out: that node keeps its error, the ones after it are skipped.</summary>
    sealed class Stop(NodeResult at) : Exception { public NodeResult At => at; }

    /// <summary>The run, all nodes; a run stopped part way marks what was not reached.</summary>
    public Evaluator RunAll()
    {
        var order = doc.Order();
        for (var i = 0; i < order.Count; i++)
        {
            try { Results[order[i].Id] = RunNode(order[i]); }
            catch (Stop s)
            {
                Results[order[i].Id] = s.At;
                foreach (var rest in order.Skip(i + 1)) Results[rest.Id] = new NodeResult { Error = "Not reached: the graph was stopped before it.", IsCommand = rest.Kind.StartsWith(CommandPrefix, StringComparison.Ordinal) };
                break;
            }
        }
        return this;
    }

    static string Message(Exception e) => e switch
    {
        CommandException or ModelException or ExprException => e.Message,
        _ => $"{e.GetType().Name}: {e.Message}",
    };

    (Dictionary<string, object?> Inputs, HashSet<string> Whole, string[] Outputs, Func<Dictionary<string, object?>, object?[]> Run) Prepare(GraphNode node)
    {
        var inputs = new Dictionary<string, object?>(StringComparer.Ordinal);
        var whole = new HashSet<string>(node.Whole, StringComparer.Ordinal);
        // What its wires bring: an upstream node that failed leaves this one with nothing to work on.
        foreach (var w in doc.Wires.Where(w => w.To == node.Id))
        {
            var from = Results.GetValueOrDefault(w.From);
            if (from == null || from.Error != null)
                throw new CommandException($"The input „{w.In}” has nothing: „{Name(doc.Node(w.From)!)}” failed.");
            inputs[w.In] = from.Outputs.GetValueOrDefault(w.Out);
        }
        if (Builtins.Of(node.Kind) is { } def)
        {
            foreach (var p in def.Inputs)
            {
                if (p.Whole || p.Setting) whole.Add(p.Name);
                if (inputs.ContainsKey(p.Name)) continue;
                inputs[p.Name] = node.Values[p.Name] is { } v ? Values.FromConstant(v) : p.Default is { } d ? Values.FromConstant(d) : null;
            }
            var call = def.Run;
            return (inputs, whole, def.Outputs.Select(o => o.Name).ToArray(), i => Timed(() => call(new NodeCall(node, i, model, Storey, query))));
        }
        foreach (var (k, v) in node.Values) if (!inputs.ContainsKey(k)) inputs[k] = Values.FromConstant(v);
        if (node.Kind.StartsWith(CommandPrefix, StringComparison.Ordinal))
        {
            var name = node.Kind[CommandPrefix.Length..];
            if (GraphLimits.Refused(name) is { } refused) throw new CommandException(refused);
            var entry = catalog.Command(name) ?? throw new CommandException($"This edition has no command „{name}”.");
            if (commands == null) throw new CommandException("Commands run in a preview or when the graph is baked.");
            return (inputs, whole, ["ids", "result"], i => Timed(() => RunCommand(entry, i)));
        }
        if (node.Kind.StartsWith(QueryPrefix, StringComparison.Ordinal))
        {
            var name = node.Kind[QueryPrefix.Length..];
            var handler = catalog.Query(name) ?? throw new CommandException($"This edition has no query „{name}”.");
            return (inputs, whole, ["result"], i => Timed(() => [Values.FromJson(handler(query, Args(i)))]));
        }
        throw new CommandException($"„{node.Kind}” is not a node this edition knows.");
    }

    static string Name(GraphNode n) => n.Label ?? (Builtins.Of(n.Kind)?.Label ?? n.Kind.Split(':')[^1]);

    object?[] Timed(Func<object?[]> f)
    {
        if (_clock.Elapsed > budget) throw new GraphStopped($"The graph took longer than {budget.TotalSeconds:0} s: stopped here.");
        return f();
    }

    /// <summary>Inputs as a command's arguments: a point on pt:N gives xN and yN (a wall's ends), a path never goes.</summary>
    static JsonObject Args(Dictionary<string, object?> inputs)
    {
        var a = new JsonObject();
        foreach (var (k, v) in inputs)
        {
            if (v == null) continue;
            if (GraphLimits.PathKeys.Contains(k)) throw new CommandException($"A graph does not pass a file's path („{k}”).");
            if (k.StartsWith("pt:", StringComparison.Ordinal))
            {
                var p = Values.Point(v, k);
                var n = k[3..];
                a[$"x{n}"] = p.X;
                a[$"y{n}"] = p.Y;
                continue;
            }
            a[k] = Values.ToArg(v);
        }
        return a;
    }

    object?[] RunCommand(CommandEntry entry, Dictionary<string, object?> inputs)
    {
        if (++Calls > GraphLimits.MaxCalls) throw new GraphStopped($"A graph runs at most {GraphLimits.MaxCalls} commands at once: stopped here.");
        var ctx = commands!;
        var args = Args(inputs);
        if (Storey != null && !args.ContainsKey("storeyId") && ctx.Model.Get(Storey) is { Class: IfcClass.BuildingStorey }) args["storeyId"] = Storey;
        ctx.Begin();
        var r = entry.Handler(ctx, args);
        Driven.After(ctx, ctx, entry.Name);
        var added = ctx.Take().Where(id => ctx.Model.Get(id) != null).Distinct().ToList();
        var producers = catalog.Get<IGeometryProducers>();
        var elements = added.Where(id => producers?.Geometry(ctx.Model.Get(id)!.Class) != null).ToList();
        var ids = elements.Count > 0 ? elements : added;
        Made.AddRange(ids);
        return [ids.Cast<object?>().ToList(), Values.FromJson(r)];
    }

    /// <summary>
    /// <paramref name="run"/> over its inputs: once, or mapped over the lists given to inputs that take one value (§3). The
    /// values by output (lists when mapped), how many items ran, how many failed and the first failure's message.
    /// </summary>
    static (object?[] Values, int Items, int Failed, string? First) Map(Func<Dictionary<string, object?>, object?[]> run, Dictionary<string, object?> inputs, HashSet<string> whole, int outputs)
    {
        var listed = inputs.Where(kv => !whole.Contains(kv.Key) && kv.Value is List<object?>).Select(kv => kv.Key).ToList();
        if (listed.Count == 0) return (Fit(run(inputs), outputs), 1, 0, null);
        var n = listed.Max(k => ((List<object?>)inputs[k]!).Count);
        if (listed.Any(k => ((List<object?>)inputs[k]!).Count == 0)) n = 0;
        var columns = Enumerable.Range(0, outputs).Select(_ => new List<object?>(n)).ToArray();
        int items = 0, failed = 0;
        string? first = null;
        for (var i = 0; i < n; i++)
        {
            var one = new Dictionary<string, object?>(inputs, StringComparer.Ordinal);
            foreach (var k in listed) { var l = (List<object?>)inputs[k]!; one[k] = l[Math.Min(i, l.Count - 1)]; }
            try
            {
                var (v, it, f, msg) = Map(run, one, whole, outputs);
                for (var o = 0; o < outputs; o++) columns[o].Add(v[o]);
                items += it;
                failed += f;
                first ??= msg;
            }
            catch (GraphStopped) { throw; }
            catch (Exception e)
            {
                for (var o = 0; o < outputs; o++) columns[o].Add(null);
                items++;
                failed++;
                first ??= Message(e);
            }
        }
        return (columns.Cast<object?>().ToArray(), items, failed, first);
    }

    static object?[] Fit(object?[] v, int n) => v.Length == n ? v : [.. v.Concat(Enumerable.Repeat<object?>(null, Math.Max(0, n - v.Length))).Take(n)];

    /// <summary>The points and polylines the nodes gave (the ghost draws them), as many as <paramref name="most"/> coordinates allow.</summary>
    public (List<Vec3> Points, List<Polyline> Lines) Shapes(int most)
    {
        var points = new List<Vec3>();
        var lines = new List<Polyline>();
        var used = 0;
        void Walk(object? v)
        {
            if (used > most) return;
            switch (v)
            {
                case Vec3 p: points.Add(p); used += 3; break;
                case Polyline l: lines.Add(l); used += l.Points.Count * 3; break;
                case List<object?> list: foreach (var x in list) Walk(x); break;
            }
        }
        foreach (var node in doc.Nodes)
            if (Builtins.Of(node.Kind) is { Group: "Geometry" } && Results.GetValueOrDefault(node.Id) is { Error: null } r)
                foreach (var v in r.Outputs.Values) Walk(v);
        return (points, lines);
    }
}

/// <summary>
/// A command's context for the commands a graph runs: everything goes to <paramref name="edit"/> (a copy of the model's
/// transaction, or the bake command's own context), and what each command adds is noted (its <c>ids</c> output).
/// </summary>
public sealed class TrackingContext(IEdit edit, ModuleCatalog catalog, IQueryContext query) : ICommandContext
{
    List<string> _added = [];

    public void Begin() => _added = [];
    public List<string> Take() { var t = _added; _added = []; return t; }

    public IModelView Model => edit.Model;
    public IReadOnlyList<Change<Entity>> EntityChanges => edit.EntityChanges;
    public IReadOnlyList<Change<Relation>> RelationChanges => edit.RelationChanges;
    public string NewId() => edit.NewId();
    public void Add(Entity entity) { edit.Add(entity); _added.Add(entity.Id); }
    public void Replace(Entity entity) => edit.Replace(entity);
    public void Remove(string id) => edit.Remove(id);
    public Relation Relate(string relationClass, string relating, string related) => edit.Relate(relationClass, relating, related);
    public void Unrelate(string relationClass, string related) => edit.Unrelate(relationClass, related);
    public void AddRelation(Relation relation) => edit.AddRelation(relation);
    public void ReplaceRelation(Relation relation) => edit.ReplaceRelation(relation);
    public void RemoveRelation(string id) => edit.RemoveRelation(id);
    public IServices Services => catalog;
    public ClassDescriptor? Describe(string ifcClass) => catalog.Class(ifcClass);
    public IQueryContext Query => query;
}

/// <summary>A query's context over a model that is not the session's (the preview's copy, or the bake's edit): geometry built as asked, by the modules' producers.</summary>
public sealed class ModelQuery(IModelView model, ModuleCatalog catalog) : IQueryContext
{
    readonly Dictionary<string, (long Version, ElementGeometry? Geometry)> _built = [];

    public IModelView Model => model;
    public IServices Services => catalog;
    public ClassDescriptor? Describe(string ifcClass) => catalog.Class(ifcClass);

    public ElementGeometry? Geometry(string entityId)
    {
        if (model.Get(entityId) is not { } e) return null;
        if (_built.TryGetValue(entityId, out var b) && b.Version == e.Version) return b.Geometry;
        var g = Build(e, model, catalog);
        _built[entityId] = (e.Version, g);
        return g;
    }

    /// <summary>An entity's geometry, by its class's producer and the geometry filters; null when its class builds none or the build fails.</summary>
    public static ElementGeometry? Build(Entity e, IModelView model, ModuleCatalog catalog)
    {
        if (catalog.Get<IGeometryProducers>()?.Geometry(e.Class) is not { } producer) return null;
        try
        {
            var g = producer(e, model, catalog);
            if (g != null) foreach (var f in catalog.All<IGeometryFilter>()) g = f.Apply(e, g, model, catalog) ?? g;
            return g;
        }
        catch (Exception) { return null; }
    }
}
