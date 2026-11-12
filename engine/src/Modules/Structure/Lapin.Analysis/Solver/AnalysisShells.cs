using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Analysis.Solver;

/// <summary>
/// The shells' results (docs/plan-fem.md §10.6, §10.10 step 5): each surface's values at its nodes, smoothed by the patch
/// recovery (<see cref="ShellSmoothing.Recovered"/>, linear in the results, so a combination's are its cases' by their
/// factors), in mechanics' sign (a moment positive when the face the surface's normal points to is in tension: a slab's top,
/// a wall's right as its line runs; the interface turns a slab's over, §10.12). w is the displacement along the normal.
/// </summary>
public sealed partial class AnalysisRun
{
    public static readonly string[] ShellQuantities = ["w", "mx", "my", "mxy", "nx", "ny", "nxy", "vx", "vy"];

    static int ForceIndex(string q) => q switch { "nx" => 0, "ny" => 1, "nxy" => 2, "mx" => 3, "my" => 4, "mxy" => 5, "vx" => 6, "vy" => 7, _ => -1 };
    public static string ShellUnit(string q) => q == "w" ? "m" : q[0] == 'm' ? "kN·m/m" : "kN/m";

    (string Group, int Node)[]? _keys;
    Dictionary<string, List<int>>? _bySurface;
    ShellForces[]?[]? _perCase;
    readonly object _gate = new();

    /// <summary>Each surface's nodes, in the order their values are kept.</summary>
    (string Group, int Node)[] Keys()
    {
        if (_keys != null) return _keys;
        var keys = new List<(string, int)>();
        var seen = new HashSet<(string, int)>();
        foreach (var e in Problem.Problem.Shells)
            foreach (var n in e.Nodes)
                if (seen.Add((e.Group ?? "", n))) keys.Add((e.Group ?? "", n));
        _bySurface = keys.Select((k, i) => (k.Item1, i)).GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.Select(x => x.i).ToList());
        return _keys = [.. keys];
    }

    /// <summary>A case's resultants at the surfaces' nodes, recovered once.</summary>
    ShellForces[] CaseNodal(int ci)
    {
        lock (_gate)
        {
            var keys = Keys();
            _perCase ??= new ShellForces[]?[Solution!.Cases.Count];
            if (_perCase[ci] is { } had) return had;
            var rec = ShellSmoothing.Recovered(Problem.Problem, Solution!.Cases[ci]);
            var o = new ShellForces[keys.Length];
            for (var i = 0; i < keys.Length; i++) o[i] = rec.TryGetValue(keys[i], out var f) ? f : default;
            return _perCase[ci] = o;
        }
    }

    /// <summary>A set of cases by their factors: the resultants at the nodes, and w (the displacement along each surface's normal).</summary>
    (ShellForces[] F, double[] W) SetNodal(List<(int Case, double Factor)> terms)
    {
        var keys = Keys();
        var f = new ShellForces[keys.Length];
        var w = new double[keys.Length];
        foreach (var (ci, k) in terms)
        {
            var c = CaseNodal(ci);
            var u = Solution!.Cases[ci].U;
            for (var i = 0; i < keys.Length; i++)
            {
                f[i] += c[i] * k;
                var n = Problem.Surfaces.TryGetValue(keys[i].Group, out var s) ? s.Normal : Vec3.UnitZ;
                var x = keys[i].Node * 6;
                w[i] += (u[x] * n.X + u[x + 1] * n.Y + u[x + 2] * n.Z) * k;
            }
        }
        return (f, w);
    }

    static double Value(string q, ShellForces f, double w) => q == "w" ? w : f[ForceIndex(q)];

    IEnumerable<string> SurfacesFor(string? id)
    {
        var all = Problem.Surfaces.Keys.OrderBy(k => Problem.Surfaces[k].Name, StringComparer.Ordinal).ThenBy(k => k, StringComparer.Ordinal).ToList();
        if (id == null) return all;
        var list = all.Where(k => k == id || Problem.Surfaces[k].SourceId == id).ToList();
        return list.Count > 0 ? list : throw new CommandException("That element is not a shell of the analysis.");
    }

    /// <summary>
    /// The shells in analysis.results: each surface's extremes of every quantity (at its nodes, smoothed; over an envelope's
    /// combinations), where they are; with one shell asked for, its nodes' values and its elements' at their centres.
    /// </summary>
    (JsonArray Shells, List<(string Key, JsonNode Value)> Maxima) ShellResults(Selection sel, string? shellId)
    {
        var p = Problem.Problem;
        var keys = Keys();
        var sets = sel.Sets.Select(s => SetNodal(s.Terms)).ToList();
        var envelope = sel.Kind == "envelope";
        var o = new JsonArray();
        var best = new Dictionary<string, ((string Surface, double Value, int Node) Max, (string Surface, double Value, int Node) Min)>();
        foreach (var g in SurfacesFor(shellId))
        {
            var info = Problem.Surfaces[g];
            var idx = _bySurface![g];
            var ext = new JsonObject();
            foreach (var q in ShellQuantities)
            {
                var (hi, hiAt, lo, loAt) = (double.NegativeInfinity, -1, double.PositiveInfinity, -1);
                foreach (var (f, w) in sets)
                    foreach (var i in idx)
                    {
                        var v = Value(q, f[i], w[i]);
                        if (v > hi) (hi, hiAt) = (v, keys[i].Node);
                        if (v < lo) (lo, loAt) = (v, keys[i].Node);
                    }
                ext[q] = new JsonObject { ["max"] = R(hi, q == "w" ? 9 : 6), ["atMax"] = V3(p.Nodes[hiAt]), ["min"] = R(lo, q == "w" ? 9 : 6), ["atMin"] = V3(p.Nodes[loAt]) };
                var had = best.TryGetValue(q, out var b) ? b : (Max: (Surface: g, Value: double.NegativeInfinity, Node: -1), Min: (Surface: g, Value: double.PositiveInfinity, Node: -1));
                if (hi > had.Max.Value) had.Max = (g, hi, hiAt);
                if (lo < had.Min.Value) had.Min = (g, lo, loAt);
                best[q] = had;
            }
            var so = new JsonObject
            {
                ["id"] = g, ["sourceId"] = info.SourceId, ["name"] = info.Name, ["kind"] = info.Kind, ["normal"] = V3(info.Normal),
                ["thickness"] = p.Shells.FirstOrDefault(s => s.Group == g)?.Thickness, ["extremes"] = ext,
            };
            if (shellId != null)
            {
                so["nodes"] = new JsonArray(idx.Select(i => (JsonNode)new JsonObject
                {
                    ["node"] = p.NodeIds[keys[i].Node], ["at"] = V3(p.Nodes[keys[i].Node]),
                    ["values"] = new JsonObject(ShellQuantities.Select(q => KeyValuePair.Create(q, (JsonNode?)(envelope
                        ? new JsonArray(R(sets.Min(s => Value(q, s.F[i], s.W[i])), 9), R(sets.Max(s => Value(q, s.F[i], s.W[i])), 9))
                        : JsonValue.Create(R(Value(q, sets[0].F[i], sets[0].W[i]), 9)))))),
                }).ToArray());
                var shellStates = sel.Sets.Select(s => Enumerable.Range(0, p.Shells.Count).Where(k => p.Shells[k].Group == g)
                    .ToDictionary(k => k, k => ShellResult.Combine(s.Terms.Select(t => (Solution!.Cases[t.Case].Shells[k], t.Factor))))).ToList();
                so["elements"] = new JsonArray(shellStates[0].Keys.Select(k => (JsonNode)new JsonObject
                {
                    ["id"] = p.Shells[k].Id, ["centre"] = V3(p.Shells[k].Nodes.Aggregate(Vec3.Zero, (a, n) => a + p.Nodes[n]) * (1.0 / p.Shells[k].Count)),
                    ["values"] = new JsonObject(ShellQuantities.Where(q => q != "w").Select(q => KeyValuePair.Create(q, (JsonNode?)(envelope
                        ? new JsonArray(R(shellStates.Min(s => s[k].Centre[ForceIndex(q)])), R(shellStates.Max(s => s[k].Centre[ForceIndex(q)])))
                        : JsonValue.Create(R(shellStates[0][k].Centre[ForceIndex(q)])))))),
                }).ToArray());
            }
            o.Add((JsonNode)so);
        }
        var maxima = new JsonObject();
        foreach (var (q, (hi, lo)) in best)
        {
            if (hi.Node < 0) continue;
            maxima[q] = new JsonObject
            {
                ["max"] = new JsonObject { ["surface"] = hi.Surface, ["sourceId"] = Problem.Surfaces[hi.Surface].SourceId, ["value"] = R(hi.Value, 9), ["at"] = V3(p.Nodes[hi.Node]) },
                ["min"] = new JsonObject { ["surface"] = lo.Surface, ["sourceId"] = Problem.Surfaces[lo.Surface].SourceId, ["value"] = R(lo.Value, 9), ["at"] = V3(p.Nodes[lo.Node]) },
            };
        }
        return (o, [("shells", maxima)]);
    }

    // ------------------------------------------------------------ analysis.contours

    /// <summary>{case | combination | envelope, quantity, shellId?}: the analysis at the model's revision, its shells' contours.</summary>
    public static JsonObject Contours(IQueryContext q, JsonObject a) =>
        Of(q).Contours(new Ask((string?)a["case"], (string?)a["combination"], (string?)a["envelope"], null, (string?)a["shellId"]), (string?)a["quantity"]);

    /// <summary>
    /// Each surface's mesh (its nodes in world coordinates, flat [x, y, z, …]; its faces four node indices each, a triangle's
    /// last repeated) and the quantity at its nodes (an envelope's least and largest), its unit, the extremes over all; in
    /// mechanics' sign, with each surface's kind and normal so a view can turn a slab's moments over (§10.12).
    /// </summary>
    public JsonObject Contours(Ask ask, string? quantity)
    {
        var sel = Select(ask);
        if (quantity == null || !ShellQuantities.Contains(quantity)) throw new CommandException($"Say the quantity: one of {string.Join(", ", ShellQuantities)}.");
        var p = Problem.Problem;
        var o = new JsonObject { ["kind"] = sel.Kind, ["key"] = sel.Key, ["quantity"] = quantity, ["unit"] = ShellUnit(quantity) };
        if (p.Shells.Count == 0) { o["surfaces"] = new JsonArray(); o["min"] = 0; o["max"] = 0; return o; }
        var keys = Keys();
        var at = new Dictionary<(string, int), int>();
        for (var i = 0; i < keys.Length; i++) at[keys[i]] = i;
        var sets = sel.Sets.Select(s => SetNodal(s.Terms)).ToList();
        var envelope = sel.Kind == "envelope";
        var digits = quantity == "w" ? 9 : 4;
        var (gMin, gMax) = (double.PositiveInfinity, double.NegativeInfinity);
        var surfaces = new JsonArray();
        var bySurface = p.Shells.Select((s, k) => (s, k)).GroupBy(x => x.s.Group ?? "").ToDictionary(g => g.Key, g => g.Select(x => x.s).ToList());
        foreach (var g in SurfacesFor(ask.ShellId))
        {
            var info = Problem.Surfaces[g];
            if (!bySurface.TryGetValue(g, out var shells)) continue;
            var local = new Dictionary<int, int>();
            var positions = new JsonArray();
            var faces = new JsonArray();
            var nodes = new List<int>();
            foreach (var e in shells)
            {
                var ix = e.Nodes.Select(n =>
                {
                    if (local.TryGetValue(n, out var li)) return li;
                    local[n] = nodes.Count;
                    nodes.Add(n);
                    var q = p.Nodes[n];
                    positions.Add((JsonNode)R(q.X, 4)); positions.Add((JsonNode)R(q.Y, 4)); positions.Add((JsonNode)R(q.Z, 4));
                    return nodes.Count - 1;
                }).ToArray();
                foreach (var x in ix) faces.Add((JsonNode)x);
                if (ix.Length == 3) faces.Add((JsonNode)ix[2]);
            }
            var lo = new double[nodes.Count];
            var hi = new double[nodes.Count];
            for (var j = 0; j < nodes.Count; j++)
            {
                var i = at[(g, nodes[j])];
                var vs = sets.Select(s => Value(quantity, s.F[i], s.W[i])).ToList();
                (lo[j], hi[j]) = (vs.Min(), vs.Max());
                gMin = Math.Min(gMin, lo[j]);
                gMax = Math.Max(gMax, hi[j]);
            }
            var so = new JsonObject
            {
                ["id"] = g, ["sourceId"] = info.SourceId, ["name"] = info.Name, ["kind"] = info.Kind, ["normal"] = V3(info.Normal),
                ["thickness"] = shells[0].Thickness, ["positions"] = positions, ["faces"] = faces,
            };
            if (envelope) { so["min"] = Arr(lo, digits); so["max"] = Arr(hi, digits); }
            else so["values"] = Arr(hi, digits);
            surfaces.Add((JsonNode)so);
        }
        o["min"] = double.IsFinite(gMin) ? R(gMin, digits) : 0;
        o["max"] = double.IsFinite(gMax) ? R(gMax, digits) : 0;
        o["surfaces"] = surfaces;
        return o;
    }
}
