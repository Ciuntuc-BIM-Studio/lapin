using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Graphs;

/// <summary>
/// One node of a graph as saved: its id, its kind (a node of the module's own, command:…, query:… or <c>group</c>), where it
/// stands, its constants, its own name, and how it shows in the graph's palette (<see cref="Palette"/>, format 2). A group
/// (format 2, a selection of nodes collapsed into one) holds its nodes as a graph of their own (<see cref="Inner"/>) and its
/// open ports: <see cref="Ins"/>, the inner inputs fed from outside, <see cref="Outs"/>, the inner outputs read outside.
/// </summary>
public sealed record GraphNode(string Id, string Kind, double X, double Y, JsonObject Values, IReadOnlySet<string> Whole, string? Label)
{
    /// <summary>The node in the graph's palette: label, hide, order, min, max, step (an input node's field), show (an output's value).</summary>
    public JsonObject? Palette { get; init; }
    public GraphDoc? Inner { get; init; }
    public IReadOnlyList<GroupPort> Ins { get; init; } = [];
    public IReadOnlyList<GroupPort> Outs { get; init; } = [];
    public bool IsGroup => Kind == GraphDoc.GroupKind;
}

/// <summary>An open port of a group: a port of one of its nodes, named <c>node.port</c> on the group.</summary>
public sealed record GroupPort(string Node, string Port, string? Label)
{
    public string Name => $"{Node}.{Port}";
}

/// <summary>One wire: an output of a node to an input of another.</summary>
public sealed record GraphWire(string From, string Out, string To, string In);

/// <summary>
/// A graph as JSON (docs/plan-visual-graphs.md §2): <c>{format, nodes: [{id, kind, x, y, values?, whole?, label?, palette?,
/// graph?, inputs?, outputs?}], wires: [{from, out, to, in}], tool?}</c>, read and checked: ids unique, wires between nodes
/// it has, one wire per input, no cycle, within <see cref="GraphLimits"/>. Format 2 (stage 2, decision 536) adds the
/// palette's settings (a node's <c>palette</c>, the graph's <c>tool</c>) and groups; format 1 is read by
/// <see cref="FromFormat1"/>, and a graph is always written as format 2.
/// </summary>
public sealed class GraphDoc
{
    public const int Format = 2;
    public const string GroupKind = "group";
    /// <summary>How deep groups go inside groups.</summary>
    public const int MaxDepth = 8;
    public IReadOnlyList<GraphNode> Nodes { get; }
    public IReadOnlyList<GraphWire> Wires { get; }
    /// <summary>The graph as a tool of My tools: show, name, icon, tooltip, group (format 2); null when it is none.</summary>
    public JsonObject? Tool { get; init; }
    readonly Dictionary<string, GraphNode> _byId;

    GraphDoc(List<GraphNode> nodes, List<GraphWire> wires)
    {
        Nodes = nodes;
        Wires = wires;
        _byId = nodes.ToDictionary(n => n.Id);
    }

    public GraphNode? Node(string id) => _byId.GetValueOrDefault(id);

    /// <summary>A node by its path: its id, or a group's id, a slash and the path inside it (<c>g1/n2</c>).</summary>
    public GraphNode? Find(string path)
    {
        var slash = path.IndexOf('/');
        if (slash < 0) return Node(path);
        return Node(path[..slash]) is { Inner: { } inner } ? inner.Find(path[(slash + 1)..]) : null;
    }

    /// <summary>Every node with its path, groups' nodes after their group.</summary>
    public IEnumerable<(string Path, GraphNode Node)> All(string prefix = "")
    {
        foreach (var n in Nodes)
        {
            yield return (prefix + n.Id, n);
            if (n.Inner != null) foreach (var x in n.Inner.All($"{prefix}{n.Id}/")) yield return x;
        }
    }

    static readonly Regex IdPattern = new(@"^[A-Za-z0-9_\-:.]{1,64}$", RegexOptions.CultureInvariant);
    static readonly Regex PortPattern = new(@"^[A-Za-z0-9_\-:.$]{1,130}$", RegexOptions.CultureInvariant);

    /// <summary>The graph in <paramref name="json"/>, checked; a <see cref="CommandException"/> says what is wrong.</summary>
    public static GraphDoc Read(JsonNode? json)
    {
        if (json is not JsonObject o) throw new CommandException("A graph is a JSON object with its nodes and wires.");
        if (o.ToJsonString().Length > GraphLimits.MaxJson) throw new CommandException($"A graph is at most {GraphLimits.MaxJson / 1024} KB.");
        var format = Json.Number(o["format"]) ?? 1;
        if (format > Format) throw new CommandException("The graph was made by a newer Lapin: update Lapin to open it.");
        if (format < 2) o = FromFormat1(o);
        var count = 0;
        return Read(o, 0, ref count);
    }

    /// <summary>
    /// A graph of format 1 (stage 1: nodes and wires, a node's label) as format 2: the same nodes and wires, no palette
    /// settings (every input node in the palette, in the order of the canvas), no tool, no group (format 1 had none: a
    /// node of the kind <c>group</c> there is refused as a kind it did not know).
    /// </summary>
    public static JsonObject FromFormat1(JsonObject o)
    {
        var g = new JsonObject { ["format"] = Format, ["nodes"] = o["nodes"]?.DeepClone(), ["wires"] = o["wires"]?.DeepClone() };
        foreach (var n in g["nodes"] as JsonArray ?? [])
        {
            if (n is not JsonObject no) continue;
            if ((string?)no["kind"] == GroupKind) throw new CommandException("A graph of format 1 has no groups.");
            no.Remove("palette");
            no.Remove("graph");
            no.Remove("inputs");
            no.Remove("outputs");
        }
        return g;
    }

    static GraphDoc Read(JsonObject o, int depth, ref int count)
    {
        if (depth > MaxDepth) throw new CommandException($"Groups go at most {MaxDepth} deep.");
        var nodes = new List<GraphNode>();
        foreach (var n in o["nodes"] as JsonArray ?? [])
        {
            if (n is not JsonObject no) throw new CommandException("A node of the graph is not an object.");
            var id = (string?)no["id"] ?? "";
            if (!IdPattern.IsMatch(id)) throw new CommandException($"„{id}” is not a node's id.");
            var kind = (string?)no["kind"] ?? throw new CommandException($"The node {id} has no kind.");
            var whole = (no["whole"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>().ToHashSet(StringComparer.Ordinal);
            var values = no["values"] as JsonObject ?? [];
            foreach (var (k, _) in values) if (!PortPattern.IsMatch(k)) throw new CommandException($"„{k}” is not an input's name.");
            var node = new GraphNode(id, kind, Json.Number(no["x"]) ?? 0, Json.Number(no["y"]) ?? 0, (JsonObject)values.DeepClone(), whole,
                Text(no["label"], 80)) { Palette = PaletteOf(no["palette"]) };
            if (kind == GroupKind)
            {
                if (no["graph"] is not JsonObject inner) throw new CommandException($"The group {id} holds no graph.");
                var sub = Read(inner, depth + 1, ref count);
                node = node with { Inner = sub, Ins = Ports(no["inputs"], sub, id, "in"), Outs = Ports(no["outputs"], sub, id, "out") };
            }
            nodes.Add(node);
        }
        count += nodes.Count;
        if (count > GraphLimits.MaxNodes) throw new CommandException($"A graph has at most {GraphLimits.MaxNodes} nodes.");
        if (nodes.Select(n => n.Id).Distinct().Count() != nodes.Count) throw new CommandException("Two nodes of the graph have the same id.");
        var byId = nodes.ToDictionary(n => n.Id);
        var wires = new List<GraphWire>();
        foreach (var w in o["wires"] as JsonArray ?? [])
        {
            if (w is not JsonObject wo) throw new CommandException("A wire of the graph is not an object.");
            var wire = new GraphWire((string?)wo["from"] ?? "", (string?)wo["out"] ?? "", (string?)wo["to"] ?? "", (string?)wo["in"] ?? "");
            if (!byId.TryGetValue(wire.From, out var from) || !byId.TryGetValue(wire.To, out var to)) throw new CommandException("A wire of the graph joins a node it does not have.");
            if (!PortPattern.IsMatch(wire.Out) || !PortPattern.IsMatch(wire.In)) throw new CommandException("A wire of the graph names no port.");
            if (to.IsGroup && to.Ins.All(p => p.Name != wire.In)) throw new CommandException($"The group {to.Id} has no input „{wire.In}”.");
            if (from.IsGroup && from.Outs.All(p => p.Name != wire.Out)) throw new CommandException($"The group {from.Id} has no output „{wire.Out}”.");
            wires.Add(wire);
        }
        if (wires.Count > GraphLimits.MaxWires) throw new CommandException($"A graph has at most {GraphLimits.MaxWires} wires.");
        if (wires.GroupBy(w => (w.To, w.In)).FirstOrDefault(g => g.Count() > 1) is { } twice)
            throw new CommandException($"The input „{twice.Key.In}” takes one wire.");
        var doc = new GraphDoc(nodes, wires) { Tool = depth == 0 ? ToolOf(o["tool"]) : null };
        doc.Order();
        return doc;
    }

    static string? Text(JsonNode? n, int most) =>
        n is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim() is { Length: > 0 } t ? (t.Length > most ? t[..most] : t) : null;

    static double? Finite(JsonNode? n) => Json.Number(n) is { } d && double.IsFinite(d) ? d : null;

    static bool? Flag(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    /// <summary>A node's palette settings, only those known: label, hide, order, min, max, step, show.</summary>
    static JsonObject? PaletteOf(JsonNode? n)
    {
        if (n is not JsonObject p) return null;
        var o = new JsonObject();
        if (Text(p["label"], 80) is { } label) o["label"] = label;
        foreach (var k in (string[])["hide", "show"]) if (Flag(p[k]) is { } b) o[k] = b;
        foreach (var k in (string[])["order", "min", "max", "step"]) if (Finite(p[k]) is { } d) o[k] = d;
        return o.Count > 0 ? o : null;
    }

    /// <summary>The graph's tool settings, only those known: show, name, icon, tooltip, group.</summary>
    static JsonObject? ToolOf(JsonNode? n)
    {
        if (n is not JsonObject t) return null;
        var o = new JsonObject();
        if (Flag(t["show"]) is { } show) o["show"] = show;
        foreach (var (k, most) in new[] { ("name", 80), ("icon", 40), ("tooltip", 300), ("group", 60) }) if (Text(t[k], most) is { } s) o[k] = s;
        return o.Count > 0 ? o : null;
    }

    static List<GroupPort> Ports(JsonNode? n, GraphDoc inner, string group, string side)
    {
        var ports = new List<GroupPort>();
        foreach (var p in n as JsonArray ?? [])
        {
            if (p is not JsonObject po) continue;
            var node = (string?)po["node"] ?? "";
            var port = (string?)po["port"] ?? "";
            if (inner.Node(node) == null || !PortPattern.IsMatch(port)) throw new CommandException($"The group {group} names a port of a node it does not hold.");
            var gp = new GroupPort(node, port, Text(po["label"], 80));
            if (ports.Any(x => x.Name == gp.Name)) continue;
            if (side == "in" && inner.Wires.Any(w => w.To == node && w.In == port)) throw new CommandException($"The group {group}'s input „{gp.Name}” is wired inside it.");
            ports.Add(gp);
        }
        return ports;
    }

    /// <summary>The nodes in an order that has every node after those wired into it (Kahn's, ties in the graph's order); a cycle refused.</summary>
    public IReadOnlyList<GraphNode> Order()
    {
        var indegree = Nodes.ToDictionary(n => n.Id, _ => 0);
        foreach (var w in Wires) indegree[w.To]++;
        var outs = Wires.GroupBy(w => w.From).ToDictionary(g => g.Key, g => g.Select(w => w.To).ToList());
        var ready = new Queue<GraphNode>(Nodes.Where(n => indegree[n.Id] == 0));
        var order = new List<GraphNode>();
        while (ready.TryDequeue(out var n))
        {
            order.Add(n);
            foreach (var to in outs.GetValueOrDefault(n.Id) ?? [])
                if (--indegree[to] == 0) ready.Enqueue(_byId[to]);
        }
        if (order.Count != Nodes.Count) throw new CommandException("The graph goes round in a circle: a node cannot feed itself, even through others.");
        return order;
    }

    /// <summary>The graph as JSON again (what is saved: only what <see cref="Read"/> kept), always as format 2.</summary>
    public JsonObject ToJson()
    {
        var o = Body();
        o.Insert(0, "format", Format);
        if (Tool != null) o["tool"] = Tool.DeepClone();
        return o;
    }

    JsonObject Body() => new()
    {
        ["nodes"] = new JsonArray(Nodes.Select(n =>
        {
            var o = new JsonObject { ["id"] = n.Id, ["kind"] = n.Kind, ["x"] = Math.Round(n.X, 1), ["y"] = Math.Round(n.Y, 1) };
            if (n.Values.Count > 0) o["values"] = n.Values.DeepClone();
            if (n.Whole.Count > 0) o["whole"] = new JsonArray(n.Whole.Order(StringComparer.Ordinal).Select(x => (JsonNode)x).ToArray());
            if (n.Label != null) o["label"] = n.Label;
            if (n.Palette != null) o["palette"] = n.Palette.DeepClone();
            if (n.Inner != null)
            {
                o["graph"] = n.Inner.Body();
                o["inputs"] = PortsJson(n.Ins);
                o["outputs"] = PortsJson(n.Outs);
            }
            return (JsonNode)o;
        }).ToArray()),
        ["wires"] = new JsonArray(Wires.Select(w => (JsonNode)new JsonObject { ["from"] = w.From, ["out"] = w.Out, ["to"] = w.To, ["in"] = w.In }).ToArray()),
    };

    static JsonArray PortsJson(IReadOnlyList<GroupPort> ports) => new(ports.Select(p =>
    {
        var o = new JsonObject { ["node"] = p.Node, ["port"] = p.Port };
        if (p.Label != null) o["label"] = p.Label;
        return (JsonNode)o;
    }).ToArray());

    /// <summary>
    /// The graph with its input nodes' values replaced (a palette's fields, decision 536): <paramref name="values"/> by a
    /// node's path (<see cref="Find"/>), each the value of that input node (a number, a text, yes or no, a storey's or a
    /// type's id, the ids of elements picked). A path that is not an input node, or a value not of its kind, is refused.
    /// </summary>
    public GraphDoc WithValues(JsonObject? values)
    {
        if (values == null || values.Count == 0) return this;
        var json = ToJson();
        foreach (var (path, value) in values)
        {
            var node = Find(path) ?? throw new CommandException($"The graph has no node {path}.");
            var port = Builtins.InputPort(node.Kind) ?? throw new CommandException($"„{Name(node)}” is not an input of the graph.");
            Builtins.CheckInput(node.Kind, value, Name(node));
            var at = NodeJson(json, path)!;
            var vals = at["values"] as JsonObject ?? (JsonObject)(at["values"] = new JsonObject());
            vals[port] = value?.DeepClone();
        }
        return Read(json);
    }

    static string Name(GraphNode n) => n.Label ?? Builtins.Of(n.Kind)?.Label ?? n.Kind;

    static JsonObject? NodeJson(JsonObject graph, string path)
    {
        var parts = path.Split('/');
        JsonObject? at = graph;
        JsonObject? node = null;
        foreach (var id in parts)
        {
            node = (at?["nodes"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(n => (string?)n["id"] == id);
            at = node?["graph"] as JsonObject;
        }
        return node;
    }
}

/// <summary>How far a graph goes (docs/plan-visual-graphs.md §2): beyond these it is refused or stopped with a message.</summary>
public static class GraphLimits
{
    public const int MaxNodes = 500;
    public const int MaxWires = 2000;
    public const int MaxJson = 256 * 1024;
    /// <summary>The values a node's output may hold, its lists' items counted.</summary>
    public const int MaxItems = 10_000;
    /// <summary>The calls of commands one evaluation makes (each item of a mapped node is one).</summary>
    public const int MaxCalls = 2000;
    public static readonly TimeSpan PreviewTime = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan BakeTime = TimeSpan.FromSeconds(60);
    /// <summary>The coordinates the ghost carries (its meshes' and lines'); past them it is cut, with a warning.</summary>
    public const int MaxGhostFloats = 2_000_000;

    /// <summary>
    /// Commands a graph does not run (§3): those reading or writing files or projects, running other graphs or scripts, a
    /// batch. Matched on the whole name (a namespace ends with a dot) or on a part of it.
    /// </summary>
    static readonly string[] RefusedPrefixes = ["graphs.", "scripting.", "link.", "project.", "version.", "share.", "edit.batch"];
    static readonly string[] RefusedParts = ["import", "export", "Import", "Export"];

    /// <summary>Why a command cannot be a node; null when it can.</summary>
    public static string? Refused(string command) =>
        RefusedPrefixes.Any(p => command.StartsWith(p, StringComparison.Ordinal)) || RefusedParts.Any(p => command.Contains(p, StringComparison.Ordinal))
            ? $"„{command}” cannot run in a graph (it reads or writes files, or runs other graphs or scripts)."
            : null;

    /// <summary>The argument names that name a file or a folder (as EngineDispatcher's): a graph never passes one.</summary>
    public static readonly HashSet<string> PathKeys = new(StringComparer.OrdinalIgnoreCase) { "path", "paths", "folder", "directory", "dir" };
}
