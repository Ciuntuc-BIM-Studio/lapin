using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Graphs;

/// <summary>One node of a graph as saved: its id, its kind (a node of the module's own, command:… or query:…), where it stands, its constants.</summary>
public sealed record GraphNode(string Id, string Kind, double X, double Y, JsonObject Values, IReadOnlySet<string> Whole, string? Label);

/// <summary>One wire: an output of a node to an input of another.</summary>
public sealed record GraphWire(string From, string Out, string To, string In);

/// <summary>
/// A graph as JSON (docs/plan-visual-graphs.md §2, format 1): <c>{format, nodes: [{id, kind, x, y, values?, whole?, label?}],
/// wires: [{from, out, to, in}]}</c>, read and checked: ids unique, wires between nodes it has, one wire per input, no
/// cycle, within <see cref="GraphLimits"/>.
/// </summary>
public sealed class GraphDoc
{
    public const int Format = 1;
    public IReadOnlyList<GraphNode> Nodes { get; }
    public IReadOnlyList<GraphWire> Wires { get; }
    readonly Dictionary<string, GraphNode> _byId;

    GraphDoc(List<GraphNode> nodes, List<GraphWire> wires)
    {
        Nodes = nodes;
        Wires = wires;
        _byId = nodes.ToDictionary(n => n.Id);
    }

    public GraphNode? Node(string id) => _byId.GetValueOrDefault(id);

    static readonly Regex IdPattern = new(@"^[A-Za-z0-9_\-:.]{1,64}$", RegexOptions.CultureInvariant);
    static readonly Regex PortPattern = new(@"^[A-Za-z0-9_\-:.$]{1,64}$", RegexOptions.CultureInvariant);

    /// <summary>The graph in <paramref name="json"/>, checked; a <see cref="CommandException"/> says what is wrong.</summary>
    public static GraphDoc Read(JsonNode? json)
    {
        if (json is not JsonObject o) throw new CommandException("A graph is a JSON object with its nodes and wires.");
        if (o.ToJsonString().Length > GraphLimits.MaxJson) throw new CommandException($"A graph is at most {GraphLimits.MaxJson / 1024} KB.");
        if (Json.Number(o["format"]) is { } f && f > Format) throw new CommandException("The graph was made by a newer Lapin: update Lapin to open it.");
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
            nodes.Add(new GraphNode(id, kind, Json.Number(no["x"]) ?? 0, Json.Number(no["y"]) ?? 0, (JsonObject)values.DeepClone(), whole,
                ((string?)no["label"])?.Trim() is { Length: > 0 } l ? (l.Length > 80 ? l[..80] : l) : null));
        }
        if (nodes.Count > GraphLimits.MaxNodes) throw new CommandException($"A graph has at most {GraphLimits.MaxNodes} nodes.");
        if (nodes.Select(n => n.Id).Distinct().Count() != nodes.Count) throw new CommandException("Two nodes of the graph have the same id.");
        var ids = nodes.Select(n => n.Id).ToHashSet();
        var wires = new List<GraphWire>();
        foreach (var w in o["wires"] as JsonArray ?? [])
        {
            if (w is not JsonObject wo) throw new CommandException("A wire of the graph is not an object.");
            var wire = new GraphWire((string?)wo["from"] ?? "", (string?)wo["out"] ?? "", (string?)wo["to"] ?? "", (string?)wo["in"] ?? "");
            if (!ids.Contains(wire.From) || !ids.Contains(wire.To)) throw new CommandException("A wire of the graph joins a node it does not have.");
            if (!PortPattern.IsMatch(wire.Out) || !PortPattern.IsMatch(wire.In)) throw new CommandException("A wire of the graph names no port.");
            wires.Add(wire);
        }
        if (wires.Count > GraphLimits.MaxWires) throw new CommandException($"A graph has at most {GraphLimits.MaxWires} wires.");
        if (wires.GroupBy(w => (w.To, w.In)).FirstOrDefault(g => g.Count() > 1) is { } twice)
            throw new CommandException($"The input „{twice.Key.In}” takes one wire.");
        var doc = new GraphDoc(nodes, wires);
        doc.Order();
        return doc;
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

    /// <summary>The graph as JSON again (what is saved: only what <see cref="Read"/> kept).</summary>
    public JsonObject ToJson() => new()
    {
        ["format"] = Format,
        ["nodes"] = new JsonArray(Nodes.Select(n =>
        {
            var o = new JsonObject { ["id"] = n.Id, ["kind"] = n.Kind, ["x"] = Math.Round(n.X, 1), ["y"] = Math.Round(n.Y, 1) };
            if (n.Values.Count > 0) o["values"] = n.Values.DeepClone();
            if (n.Whole.Count > 0) o["whole"] = new JsonArray(n.Whole.Order(StringComparer.Ordinal).Select(x => (JsonNode)x).ToArray());
            if (n.Label != null) o["label"] = n.Label;
            return (JsonNode)o;
        }).ToArray()),
        ["wires"] = new JsonArray(Wires.Select(w => (JsonNode)new JsonObject { ["from"] = w.From, ["out"] = w.Out, ["to"] = w.To, ["in"] = w.In }).ToArray()),
    };
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
