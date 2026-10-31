using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// CAD linework made civil elements: a line or polyline drafted in a plan or
/// brought in by a DXF (with the heights the file gave it: a surveyed axis,
/// a contour, a kerb line) becomes a track, a road, a path or a breakline of
/// the terrain (or what a module adds: <see cref="Makers"/>, a railway's track). The line picked is followed through every line and open
/// polyline on its view and layer that meets it end to end, so a DXF's
/// hundreds of short segments read as one alignment; its heights, when it
/// has them, give the red line (a track's or a road's profile) or the
/// breakline's levels.
/// </summary>
public static class LineworkElements
{
    public static void Register(IModuleRegistry r) => r.Command("fromLinework", "From linework", From);

    /// <summary>What the linework may become here; a module adds its own (<see cref="Makers"/>).</summary>
    public static readonly string[] Kinds = ["road", "path", "breakline"];

    /// <summary>What modules make of linework, by kind (assigned whole, never mutated: another session may be reading it).</summary>
    public static IReadOnlyDictionary<string, LineworkMaker> Makers { get; set; } = new Dictionary<string, LineworkMaker>();

    static JsonNode? From(ICommandContext ctx, JsonObject a)
    {
        var e = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which line?");
        if (Points(e) == null) throw new CommandException("Pick a line or an open polyline: one drafted, or one a DXF brought in.");
        var kind = (string?)a["as"] ?? (Makers.ContainsKey("track") ? "track" : "road");
        if (!Kinds.Contains(kind) && !Makers.ContainsKey(kind)) {
            var all = Makers.Keys.Concat(Kinds).Select(k => $"a {k}").ToList();
            throw new CommandException($"Linework becomes {string.Join(", ", all.SkipLast(1))} or {all[^1]}, not „{kind}”.");
        }
        var (viewId, layer) = (e.Str("viewId"), e.Str("layer"));
        // The chain the line belongs to: every line and open polyline on its view and layer, joined end to end.
        var pieces = ctx.Model.OfClass(IfcClass.Annotation).Where(x => x.Str("viewId") == viewId && x.Str("layer") == layer)
            .Select(x => (E: x, Pts: Points(x))).Where(x => x.Pts != null).Select(x => (x.E, Pts: x.Pts!)).ToList();
        var at = pieces.FindIndex(p => p.E.Id == e.Id);
        var chain = Chains(pieces.Select(p => p.Pts).ToList(), Json.Number(a["tolerance"]) ?? 1e-3).First(c => c.Members.Contains(at));
        var pts = chain.Pts;
        if (pts.Count < 2) throw new CommandException("The line has one point.");
        var plan = pts.Select(p => new Vec2(p.X, p.Y)).ToList();
        var heights = pts.Any(p => !double.IsNaN(p.Z)) ? pts.Select(p => p.Z).ToList() : null;
        var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : null;
        var result = new JsonObject { ["kind"] = kind, ["points"] = pts.Count, ["pieces"] = chain.Members.Count, ["heights"] = heights != null, ["layer"] = layer };
        if (Makers.TryGetValue(kind, out var maker))
        {
            foreach (var (k, v) in maker(ctx, a, storeyId, name, layer, plan, heights)) result[k] = v?.DeepClone();
            return result;
        }
        switch (kind)
        {
            case "road":
            case "path":
            {
                var args = new JsonObject { ["storeyId"] = storeyId, ["points"] = new JsonArray(plan.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray()) };
                if (name != null) args["name"] = name;
                if (kind == "road")
                {
                    // As the Road tool draws one: two 3 m lanes on a 2,5 % crown, kerbs and 1,5 m sidewalks.
                    JsonObject Side() => new() { ["kerb"] = true, ["sidewalk"] = true, ["kerbWidth"] = 0.15, ["kerbHeight"] = 0.12, ["sidewalkWidth"] = 1.5 };
                    args["name"] ??= "Road"; args["template"] = "road"; args["laneWidth"] = 3; args["crossfall"] = 2.5; args["fall"] = "crown";
                    args["surfaceThickness"] = 0.08; args["baseThickness"] = 0.3; args["left"] = Side(); args["right"] = Side();
                }
                var made = SiteCommands.AddPath(ctx, args) as JsonObject;
                var id = (string)made!["id"]!;
                // Its profile from the heights: the red line designed through them.
                if (heights != null && ctx.Model.Get(id) is { } path)
                {
                    var s = new double[plan.Count];
                    for (var i = 1; i < s.Length; i++) s[i] = s[i - 1] + (plan[i] - plan[i - 1]).Length;
                    var spec = CorridorSpec.Of(path);
                    if (PvisFrom(s, heights, spec.StationStart) is { Count: >= 2 } pvis)
                    {
                        var attrs = path.CloneAttrs();
                        attrs["corridor"] = (spec with { Mode = "design", Pvis = pvis }).ToJson();
                        ctx.Replace(path.With(attrs: attrs));
                    }
                }
                result["id"] = id;
                return result;
            }
            default:
            {
                // A breakline of the terrain under the line's start, at the line's heights (the ground's where it has none).
                var terrain = ctx.Model.OfClass(IfcClass.GeographicElement).FirstOrDefault(t => t.PredefinedType == Terrains.Predefined && Terrains.Of(t).ElevationAt(plan[0].X, plan[0].Y) != null)
                    ?? throw new CommandException("No terrain under the line's start: make the terrain first, then its breaklines.");
                var ground = Terrains.Of(terrain);
                var line = pts.Select((p, i) => new Vec3(p.X, p.Y, heights != null && !double.IsNaN(heights[i]) ? heights[i] : ground.ElevationAt(p.X, p.Y) ?? 0)).ToList();
                var lines = (terrain.Attrs["breaklines"] as JsonArray)?.Select(x => x!.DeepClone()).ToList() ?? [];
                lines.Add(Terrains.PointsJson(line));
                var attrs = terrain.CloneAttrs();
                attrs["breaklines"] = new JsonArray([.. lines]);
                ctx.Replace(terrain.With(attrs: attrs));
                result["id"] = terrain.Id; result["breaklines"] = lines.Count;
                return result;
            }
        }
    }

    /// <summary>A drafted line's or open polyline's points, with the heights it carries (NaN where it has none); null for anything else.</summary>
    public static List<Vec3>? Points(Entity e)
    {
        if (e.Class != IfcClass.Annotation || e.Attrs["shape"] is not JsonObject shape) return null;
        static Vec2? P(JsonNode? x) => x is JsonArray a && a.Count >= 2 && Json.Number(a[0]) is { } px && Json.Number(a[1]) is { } py ? new Vec2(px, py) : null;
        List<Vec2>? pts = (string?)shape["type"] switch
        {
            "line" when P(shape["a"]) is { } a && P(shape["b"]) is { } b => [a, b],
            "polyline" when shape["pts"] is JsonArray arr && !(shape["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var closed) && closed) => arr.Select(P).OfType<Vec2>().ToList(),
            _ => null,
        };
        if (pts == null || pts.Count < 2) return null;
        var zs = e.Attrs["heights"] is JsonArray za && za.Count == pts.Count ? za.Select(z => Json.Number(z) ?? double.NaN).ToList() : null;
        return pts.Select((p, i) => new Vec3(p.X, p.Y, zs?[i] ?? double.NaN)).ToList();
    }

    public sealed record Chain(List<Vec3> Pts, bool Closed, List<int> Members);

    /// <summary>
    /// Pieces joined end to end (within <paramref name="tolerance"/>, in plan) into chains: from every end and
    /// junction along the pieces, through each node where exactly two meet; what is left are rings. The heights
    /// ride along with the points.
    /// </summary>
    public static List<Chain> Chains(IReadOnlyList<IReadOnlyList<Vec3>> pieces, double tolerance)
    {
        var tol = Math.Max(tolerance, 1e-12);
        var cells = new Dictionary<(long, long), List<int>>();
        var nodes = new List<Vec2>();
        int Node(Vec3 q)
        {
            var p = new Vec2(q.X, q.Y);
            var (cx, cy) = ((long)Math.Floor(p.X / tol), (long)Math.Floor(p.Y / tol));
            for (var dx = -1L; dx <= 1; dx++)
                for (var dy = -1L; dy <= 1; dy++)
                    if (cells.TryGetValue((cx + dx, cy + dy), out var list))
                        foreach (var n in list) if ((nodes[n] - p).Length <= tol) return n;
            nodes.Add(p);
            var id = nodes.Count - 1;
            if (!cells.TryGetValue((cx, cy), out var mine)) cells[(cx, cy)] = mine = [];
            mine.Add(id);
            return id;
        }
        var ends = new (int A, int B)[pieces.Count];
        var incident = new Dictionary<int, List<int>>();
        for (var i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p.Count < 2) { ends[i] = (-1, -1); continue; }
            ends[i] = (Node(p[0]), Node(p[^1]));
            foreach (var n in new[] { ends[i].A, ends[i].B }.Distinct()) (incident.TryGetValue(n, out var l) ? l : incident[n] = []).Add(i);
        }
        var used = new bool[pieces.Count];
        var chains = new List<Chain>();
        Chain Follow(int piece, int from)
        {
            var pts = new List<Vec3>();
            var members = new List<int>();
            var node = from;
            var cur = piece;
            while (true)
            {
                used[cur] = true;
                members.Add(cur);
                var p = pieces[cur];
                var forward = ends[cur].A == node;
                var seq = forward ? p : p.Reverse().ToList();
                foreach (var q in seq) if (pts.Count == 0 || new Vec2(pts[^1].X - q.X, pts[^1].Y - q.Y).Length > tol) pts.Add(q);
                node = forward ? ends[cur].B : ends[cur].A;
                if (ends[cur].A == ends[cur].B) break;
                var next = incident[node];
                if (next.Count != 2) break;
                var other = next[0] == cur ? next[1] : next[0];
                if (used[other]) break;
                cur = other;
            }
            var closed = pts.Count >= 4 && new Vec2(pts[0].X - pts[^1].X, pts[0].Y - pts[^1].Y).Length <= tol;
            if (closed) pts.RemoveAt(pts.Count - 1);
            return new Chain(pts, closed, members);
        }
        foreach (var (node, list) in incident.Where(kv => kv.Value.Count != 2).ToList())
            foreach (var piece in list) if (!used[piece]) chains.Add(Follow(piece, node));
        for (var i = 0; i < pieces.Count; i++)
            if (!used[i] && ends[i].A >= 0) chains.Add(Follow(i, ends[i].A));
        return chains;
    }

    /// <summary>The PVIs of a red line through heights measured along a line (distance along <paramref name="s"/>, one height a point; a NaN is a point without one), stationed from <paramref name="start"/>.</summary>
    public static List<Pvi>? PvisFrom(IReadOnlyList<double> s, IReadOnlyList<double> heights, double start)
    {
        var known = s.Select((d, i) => (S: d, Z: i < heights.Count ? heights[i] : double.NaN)).Where(x => !double.IsNaN(x.Z)).ToList();
        if (known.Count < 2) return null;
        double? ZAt(double d)
        {
            var i = known.FindIndex(x => x.S >= d);
            if (i < 0) return known[^1].Z;
            if (i == 0) return known[0].Z;
            var (a, b) = (known[i - 1], known[i]);
            var t = (d - a.S) / Math.Max(1e-9, b.S - a.S);
            return a.Z + (b.Z - a.Z) * t;
        }
        return VerticalProfile.FromGround(0, s[^1], ZAt, known[0].Z, 5, 0.02).Select(p => p with { Station = Math.Round(start + p.Station, 3) }).ToList();
    }
}
