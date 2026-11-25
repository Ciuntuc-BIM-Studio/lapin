using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture.Stairs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Accessibility in a rule pack (docs/plan-national-packs.md, stage N3): the least clear width of a door (inside its frame, from
/// the joinery's data: <see cref="Joinery.ClearOpening"/>), the least width of a corridor (a room of a corridor's function, at its
/// narrowest, to its finishes), the turning circle a room of the functions named must hold, and whether ramps are checked against
/// the stairs' set <c>ramp</c> (its steepest slope and least width: the limit is not written twice). The check lists what falls
/// short; nothing is refused. Lapin's own pack checks none of it (every limit empty), so a project's report stays what it was
/// until a pack's values say otherwise. The values are the pack's writer's, indicative; no norm's text is shipped.
/// </summary>
public sealed class AccessibilityRuleSection : IRuleSection, IRuleCheck
{
    public const string SectionKey = "accessibility";
    public string Key => SectionKey;
    public string Label => "Accessibility";

    public static readonly AccessibilityRuleSection Instance = new();

    /// <summary>The functions whose rooms are corridors, and those that hold a turning circle, by default.</summary>
    public static readonly string[] DefaultCorridorFunctions = ["Circulation"];
    public static readonly string[] DefaultTurningFunctions = ["Living", "Bedroom", "Kitchen", "Bath", "WC", "Circulation"];

    public JsonObject Default => new()
    {
        ["doorClearWidth"] = null, ["corridorWidth"] = null, ["turningDiameter"] = null, ["checkRamps"] = false,
        ["corridorFunctions"] = Strings(DefaultCorridorFunctions), ["turningFunctions"] = Strings(DefaultTurningFunctions),
    };

    static JsonArray Strings(IEnumerable<string> s) => new(s.Select(x => (JsonNode)x).ToArray());

    /// <summary>A limit: absent or null, not checked; else a length within [min, max] metres.</summary>
    static JsonNode? Limit(JsonObject o, string key, double min, double max, string what) =>
        o[key] is null ? null : RulePacks.Num(o, new JsonObject(), key, min, max, what);

    static JsonArray Functions(JsonObject o, string key, string[] fallback, string what)
    {
        if (o[key] is null) return Strings(fallback);
        if (o[key] is not JsonArray a || a.Count > 30) throw new CommandException($"{what} are a list of at most 30 room functions.");
        var list = new List<string>();
        foreach (var x in a)
        {
            var t = x is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : throw new CommandException($"{what} are names of room functions.");
            if (t.Length is 0 or > 60) throw new CommandException($"{what}: a function's name is 1 to 60 characters.");
            if (!list.Contains(t, StringComparer.OrdinalIgnoreCase)) list.Add(t);
        }
        return Strings(list);
    }

    public JsonObject Check(JsonNode? section)
    {
        if (section is null) return Default;
        if (section is not JsonObject o) throw new CommandException("Accessibility is an object: {doorClearWidth, corridorWidth, turningDiameter, checkRamps}.");
        var ramps = o["checkRamps"] is null ? false
            : o["checkRamps"] is JsonValue b && b.TryGetValue<bool>(out var bv) ? bv : throw new CommandException("Whether ramps are checked is true or false.");
        return new JsonObject
        {
            ["doorClearWidth"] = Limit(o, "doorClearWidth", 0.5, 2.5, "A door's least clear width (m)"),
            ["corridorWidth"] = Limit(o, "corridorWidth", 0.6, 5, "A corridor's least width (m)"),
            ["turningDiameter"] = Limit(o, "turningDiameter", 1, 3, "The turning circle's diameter (m)"),
            ["checkRamps"] = ramps,
            ["corridorFunctions"] = Functions(o, "corridorFunctions", DefaultCorridorFunctions, "The corridors' functions"),
            ["turningFunctions"] = Functions(o, "turningFunctions", DefaultTurningFunctions, "The turning circle's functions"),
        };
    }

    static string Mm(double m) => Math.Round(m * 1000).ToString(CultureInfo.InvariantCulture) + " mm";
    static string Pct(double s) => (s * 100).ToString("0.#", CultureInfo.InvariantCulture) + " %";

    public IEnumerable<RuleFinding> Check(IQueryContext q)
    {
        if (RulePacks.TrySection(q.Model, q.Services, SectionKey) is not { } rules) yield break;
        var m = q.Model;
        if (Json.Number(rules["doorClearWidth"]) is { } door)
            foreach (var d in m.OfClass(IfcClass.Door))
            {
                if (Openings.SpecOf(m, d.Id) is not { } spec) continue;
                var (w, _) = Joinery.ClearOpening(spec);
                if (w < door - 1e-6)
                    yield return new RuleFinding("architecture", d.Id, "warning", $"„{d.Name}”: its clear width {Mm(w)} is under the least {Mm(door)} (accessibility).");
            }

        if (rules["checkRamps"] is JsonValue cr && cr.TryGetValue<bool>(out var checkRamps) && checkRamps)
        {
            var set = StairRuleSection.For(m, q.Services, "ramp");
            foreach (var ramp in m.OfClass(IfcClass.Ramp))
            {
                if (RampBuilder.Resolve(m, ramp) is not { } r) continue;
                if (r.Resolved.Slope > set.MaxSlope + 1e-9)
                    yield return new RuleFinding("architecture", ramp.Id, "warning", $"„{ramp.Name}”: its slope {Pct(r.Resolved.Slope)} is steeper than the {Pct(set.MaxSlope)} of the rules' ramp set (accessibility).");
                if (r.Resolved.Width < set.MinWidth - 1e-6)
                    yield return new RuleFinding("architecture", ramp.Id, "warning", $"„{ramp.Name}”: its width {Mm(r.Resolved.Width)} is under the {Mm(set.MinWidth)} of the rules' ramp set (accessibility).");
            }
        }

        var corridor = Json.Number(rules["corridorWidth"]);
        var turning = Json.Number(rules["turningDiameter"]);
        if (corridor == null && turning == null) yield break;
        var corridorFns = Names(rules["corridorFunctions"]);
        var turningFns = Names(rules["turningFunctions"]);
        foreach (var room in m.OfClass(Rooms.Class))
        {
            var fn = room.Str("function")?.Trim();
            if (string.IsNullOrEmpty(fn)) continue;
            var isCorridor = corridor != null && corridorFns.Contains(fn);
            var turns = turning != null && turningFns.Contains(fn);
            if (!isCorridor && !turns) continue;
            if (Rooms.Of(room, m, q.Geometry) is not { } o) continue;
            var net = Rooms.MeasuresOf(room, o, m).Net;
            if (isCorridor && RoomWidths.Least(net.Outer, net.Islands) is { } least && least < corridor!.Value - 1e-3)
                yield return new RuleFinding("architecture", room.Id, "warning", $"„{room.Name}”: {Mm(least)} wide at its narrowest, under a corridor's least {Mm(corridor.Value)} (accessibility).");
            if (turns && RoomWidths.LargestCircle(net.Outer, net.Islands) is var dia && dia < turning!.Value - 1e-3)
                yield return new RuleFinding("architecture", room.Id, "warning", $"„{room.Name}”: holds a circle of {Mm(dia)} at most, under the turning circle of {Mm(turning.Value)} (accessibility).");
        }
    }

    static HashSet<string> Names(JsonNode? n) =>
        n is JsonArray a ? a.Select(x => (string?)x).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase) : [];

    public static void Register(IModuleRegistry r)
    {
        r.Service<IRuleSection>(Instance);
        r.Service<IRuleCheck>(Instance);
    }
}

/// <summary>
/// A room's widths in plan, for the accessibility check: its least width (from every point along each wall of some length,
/// straight across to the wall facing it) and the largest circle it holds (the pole of inaccessibility, found by refining
/// square cells, as Mapbox's polylabel does: a cell's best possible distance bounds what it can hold).
/// </summary>
public static class RoomWidths
{
    /// <summary>The walls shorter than this send no measure (a door's reveal, a jog): the wall across from them measures them.</summary>
    const double ShortEdge = 0.3;
    const double Step = 0.05;

    static IEnumerable<(Vec2 A, Vec2 B)> Edges(IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> islands)
    {
        foreach (var loop in islands.Prepend(outer))
            for (var i = 0; i < loop.Count; i++)
            {
                var (a, b) = (loop[i], loop[(i + 1) % loop.Count]);
                if ((b - a).Length > 1e-9) yield return (a, b);
            }
    }

    static bool Inside(IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> islands, Vec2 p) =>
        Polygon2.Contains(outer, p) && !islands.Any(i => Polygon2.Contains(i, p));

    /// <summary>The least width across the room (metres); null for a room with no wall long enough to measure from.</summary>
    public static double? Least(IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> islands)
    {
        if (outer.Count < 3) return null;
        var edges = Edges(outer, islands).ToList();
        double? best = null;
        for (var k = 0; k < edges.Count; k++)
        {
            var (a, b) = edges[k];
            var along = b - a;
            var len = along.Length;
            if (len < ShortEdge) continue;
            var dir = along * (1 / len);
            var normal = dir.Perp;
            var mid = a + along * 0.5;
            if (!Inside(outer, islands, mid + normal * 1e-4)) normal = -normal;
            if (!Inside(outer, islands, mid + normal * 1e-4)) continue;
            var n = Math.Min(400, Math.Max(1, (int)Math.Floor(len / Step)));
            for (var i = 0; i <= n; i++)
            {
                // From just inside each end, along the wall.
                var t = 0.01 + (len - 0.02) * i / n;
                var p = a + dir * t;
                if (Ray(edges, k, p, normal) is { } d && (best == null || d < best)) best = d;
            }
        }
        return best;
    }

    /// <summary>The nearest wall straight ahead of <paramref name="p"/> (not the one it lies on).</summary>
    static double? Ray(List<(Vec2 A, Vec2 B)> edges, int self, Vec2 p, Vec2 d)
    {
        double? best = null;
        for (var j = 0; j < edges.Count; j++)
        {
            if (j == self) continue;
            var (a, b) = edges[j];
            var e = b - a;
            var den = Vec2.Cross(d, e);
            if (Math.Abs(den) < 1e-12) continue;
            var w = a - p;
            var t = Vec2.Cross(w, e) / den;
            var u = Vec2.Cross(w, d) / den;
            if (t > 1e-6 && u >= -1e-9 && u <= 1 + 1e-9 && (best == null || t < best)) best = t;
        }
        return best;
    }

    static double SegDist(Vec2 p, Vec2 a, Vec2 b)
    {
        var e = b - a;
        var l2 = Vec2.Dot(e, e);
        var t = l2 > 0 ? Math.Clamp(Vec2.Dot(p - a, e) / l2, 0, 1) : 0;
        return (p - (a + e * t)).Length;
    }

    /// <summary>Signed distance to the room's boundary: positive inside, negative outside.</summary>
    static double Distance(List<(Vec2 A, Vec2 B)> edges, IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> islands, Vec2 p)
    {
        var d = double.PositiveInfinity;
        foreach (var (a, b) in edges) d = Math.Min(d, SegDist(p, a, b));
        return Inside(outer, islands, p) ? d : -d;
    }

    /// <summary>The diameter of the largest circle the room holds (metres), to within a centimetre.</summary>
    public static double LargestCircle(IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> islands, double precision = 0.005)
    {
        if (outer.Count < 3) return 0;
        var edges = Edges(outer, islands).ToList();
        var (x0, y0, x1, y1) = (outer.Min(p => p.X), outer.Min(p => p.Y), outer.Max(p => p.X), outer.Max(p => p.Y));
        var size = Math.Min(x1 - x0, y1 - y0);
        if (size <= 0) return 0;
        var h = size / 2;
        var queue = new PriorityQueue<(Vec2 C, double H, double D), double>();
        void Push(Vec2 c, double half)
        {
            var d = Distance(edges, outer, islands, c);
            queue.Enqueue((c, half, d), -(d + half * Math.Sqrt(2)));
        }
        for (var x = x0; x < x1; x += size)
            for (var y = y0; y < y1; y += size)
                Push(new Vec2(x + h, y + h), h);
        var best = Distance(edges, outer, islands, Polygon2.Centroid(outer));
        var guard = 0;
        while (queue.Count > 0 && guard++ < 200_000)
        {
            var (c, half, d) = queue.Dequeue();
            if (d > best) best = d;
            if (d + half * Math.Sqrt(2) - best <= precision) continue;
            var q = half / 2;
            Push(c + new Vec2(-q, -q), q);
            Push(c + new Vec2(q, -q), q);
            Push(c + new Vec2(-q, q), q);
            Push(c + new Vec2(q, q), q);
        }
        return Math.Max(0, best) * 2;
    }
}
