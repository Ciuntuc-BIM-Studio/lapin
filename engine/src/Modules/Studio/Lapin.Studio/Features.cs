using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// What a Studio solid does after its operation (docs/plan-studio-editor.md):
/// a list of steps in its <c>features</c> attribute, done in order on the body
/// the operation made, each kept with what it needs to find its face or edge
/// again (a point on it and its normal, an edge's two ends), so every step is
/// redone when the sketch under it changes. A step may be switched off
/// (<c>off</c>), moved up or down the list, changed or taken out.
/// <list type="bullet">
/// <item><c>pushPull</c> {point, normal, distance}: the face pushed out (added) or in (taken away);</item>
/// <item><c>chamfer</c> {a, b, distance} and <c>fillet</c> {a, b, radius}: an edge bevelled or rounded (a concave one filled);</item>
/// <item><c>shell</c> {thickness, open}: hollowed, the walls that thick, open at the ends named;</item>
/// <item><c>slice</c> {point, normal, keep}: cut by a plane, the side behind it kept (<c>back</c>) or in front (<c>front</c>);</item>
/// <item><c>array</c> {kind: linear, count, step} or {kind: polar, count, angle, centre, axis}: copies in a row or round an axis;</item>
/// <item><c>move</c> {by}, <c>rotate</c> {centre, axis, angle}, <c>mirror</c> {point, normal}: the body placed in 3D.</item>
/// </list>
/// </summary>
public static class Features
{
    public static readonly string[] Ops = ["pushPull", "chamfer", "fillet", "shell", "slice", "array", "move", "rotate", "mirror"];

    /// <summary>What the steps may need: the booleans, and the inner body of a shell (the operation again, its profile inset).</summary>
    public sealed record Context(ISolidBooleans? Booleans, Func<JsonObject, (Solid? Inner, string? Problem)>? ShellInner = null);

    public static string Label(JsonObject f)
    {
        static string N(double? v) => (v ?? 0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        return (string?)f["op"] switch
        {
            "pushPull" => $"Push/pull {N(Json.Number(f["distance"]))} m",
            "chamfer" => $"Chamfer {N(Json.Number(f["distance"]))} m",
            "fillet" => $"Fillet r {N(Json.Number(f["radius"]))} m",
            "shell" => $"Shell {N(Json.Number(f["thickness"]))} m",
            "slice" => "Slice",
            "array" => (string?)f["kind"] == "polar" ? $"Polar array × {N(Json.Number(f["count"]))}" : $"Array × {N(Json.Number(f["count"]))}",
            "move" => "Move",
            "rotate" => $"Rotate {N(Json.Number(f["angle"]))}°",
            "mirror" => "Mirror",
            var x => x ?? "Step",
        };
    }

    public static bool Off(JsonObject f) => f["off"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>The steps done in order; each that fails says why and is passed over.</summary>
    public static (Solid? Solid, List<string> Problems) Apply(Solid start, JsonArray? features, Context c)
    {
        var problems = new List<string>();
        var cur = start;
        if (features == null) return (cur, problems);
        var k = 0;
        foreach (var f in features.OfType<JsonObject>())
        {
            k++;
            if (Off(f)) continue;
            try
            {
                var (next, why) = One(cur, f, c);
                if (why != null) problems.Add($"Step {k} ({Label(f)}): {why}");
                if (next != null) cur = next;
            }
            catch (InvalidOperationException ex) { problems.Add($"Step {k} ({Label(f)}): {ex.Message}"); }
        }
        return (cur, problems);
    }

    static (Solid? Solid, string? Problem) One(Solid cur, JsonObject f, Context c)
    {
        string? Need() => c.Booleans == null ? "it needs the boolean library (manifold), missing here." : null;
        switch ((string?)f["op"])
        {
            case "pushPull":
            {
                if (Need() is { } why) return (null, why);
                if (P(f["point"]) is not { } at || P(f["normal"]) is not { } n0) return (null, "its face is not given.");
                var d = Json.Number(f["distance"]) ?? 0;
                if (Math.Abs(d) < 1e-9) return (null, null);
                if (BrepPick.Face([cur], at, n0, double.PositiveInfinity) is not { } face) return (null, "its face is gone.");
                return PushPull(cur, face, d, c.Booleans!);
            }
            case "chamfer" or "fillet":
            {
                if (Need() is { } why) return (null, why);
                var size = Json.Number(f[(string?)f["op"] == "chamfer" ? "distance" : "radius"]) ?? 0;
                if (size <= 1e-6) return (null, "it needs a size.");
                // One edge (a, b), or several (every edge round a face): each done on the body the one before left.
                var edges = new List<(Vec3 A, Vec3 B)>();
                if (P(f["a"]) is { } a && P(f["b"]) is { } b) edges.Add((a, b));
                foreach (var pair in (f["edges"] as JsonArray)?.OfType<JsonArray>() ?? [])
                    if (pair.Count >= 2 && P(pair[0]) is { } pa && P(pair[1]) is { } pb) edges.Add((pa, pb));
                if (edges.Count == 0) return (null, "its edge is not given.");
                var body = cur;
                var missed = 0;
                string? last = null;
                foreach (var (ea, eb) in edges)
                {
                    if (BrepPick.Find([body], ea, eb, Math.Max(0.05, size)) is not { } edge) { missed++; continue; }
                    var (next, problem) = Round(body, edge, size, (string?)f["op"] == "fillet", c.Booleans!);
                    if (next != null) body = next; else { missed++; last = problem; }
                }
                if (missed == edges.Count) return (null, last ?? "its edge is gone.");
                return (body, missed > 0 ? $"{missed} of its {edges.Count} edges could not be done{(last != null ? ": " + last : ".")}" : null);
            }
            case "shell":
            {
                if (Need() is { } why) return (null, why);
                if (c.ShellInner == null) return (null, "this operation cannot be hollowed.");
                var (inner, problem) = c.ShellInner(f);
                if (inner == null) return (null, problem ?? "the inner body could not be made.");
                var o = c.Booleans!.Difference(cur, [inner], cur.Tag);
                return o == null ? (null, "nothing would be left.") : (o, null);
            }
            case "slice":
            {
                if (Need() is { } why) return (null, why);
                if (P(f["point"]) is not { } at || P(f["normal"]) is not { } n0 || n0.Length < 1e-9) return (null, "its plane is not given.");
                var half = HalfSpace(cur, at, n0.Normalized(), (string?)f["keep"] != "front");
                if (half == null) return (null, "the plane could not be made.");
                var o = c.Booleans!.Intersection(cur, half, cur.Tag);
                return o == null ? (null, "nothing is left on the side kept.") : (o, null);
            }
            case "array":
            {
                var count = (int)Math.Round(Json.Number(f["count"]) ?? 1);
                if (count < 2) return (null, null);
                if (count > 500) return (null, "an array keeps to 500 copies.");
                var copies = new List<Solid> { cur };
                for (var i = 1; i < count; i++)
                {
                    Func<Vec3, Vec3> map;
                    if ((string?)f["kind"] == "polar")
                    {
                        var centre = P(f["centre"]) ?? Vec3.Zero;
                        var axis = P(f["axis"]) is { } ax && ax.Length > 1e-9 ? ax.Normalized() : Vec3.UnitZ;
                        var total = Json.Number(f["angle"]) ?? 360;
                        // All the way round: count copies share the circle; less: the first and the last at the ends.
                        var step = Math.Abs(Math.Abs(total) - 360) < 1e-9 ? total / count : total / (count - 1);
                        var t = step * i * Math.PI / 180;
                        map = p => Turn(p, centre, axis, t);
                    }
                    else
                    {
                        var by = (P(f["step"]) ?? new Vec3(1, 0, 0)) * i;
                        map = p => p + by;
                    }
                    copies.Add(SolidBuilder.Transform(cur, map));
                }
                if (c.Booleans == null) return (null, "it needs the boolean library (manifold), missing here.");
                return (c.Booleans.Union(copies, cur.Tag), null);
            }
            case "move":
            {
                var by = P(f["by"]) ?? Vec3.Zero;
                return (SolidBuilder.Translate(cur, by), null);
            }
            case "rotate":
            {
                var centre = P(f["centre"]) ?? Vec3.Zero;
                var axis = P(f["axis"]) is { } ax && ax.Length > 1e-9 ? ax.Normalized() : Vec3.UnitZ;
                var t = (Json.Number(f["angle"]) ?? 0) * Math.PI / 180;
                return (SolidBuilder.Transform(cur, p => Turn(p, centre, axis, t)), null);
            }
            case "mirror":
            {
                if (P(f["point"]) is not { } at || P(f["normal"]) is not { } n0 || n0.Length < 1e-9) return (null, "its plane is not given.");
                var n = n0.Normalized();
                return (SolidBuilder.Transform(cur, p => p - n * (2 * Vec3.Dot(p - at, n))), null);
            }
        }
        return (null, $"„{(string?)f["op"]}” is not a step Studio knows.");
    }

    public static Vec3 Turn(Vec3 p, Vec3 c, Vec3 d, double t)
    {
        var v = p - c;
        var (co, s) = (Math.Cos(t), Math.Sin(t));
        return c + v * co + Vec3.Cross(d, v) * s + d * (Vec3.Dot(d, v) * (1 - co));
    }

    /// <summary>A face pushed out by d (added: a prism on it, united) or in (a prism into the body, taken away).</summary>
    public static (Solid? Solid, string? Problem) PushPull(Solid cur, BrepFace face, double d, ISolidBooleans booleans)
    {
        const double eps = 1e-4;
        var n = face.Normal;
        if (d > 0)
        {
            // Started a hair inside the body, so the two share volume, not only a face.
            var prism = Prism.Extrude(Shift(face.Outer, -n * eps), face.Holes.Select(h => (IReadOnlyList<Vec3>)Shift(h, -n * eps)).ToList(), n * (d + eps), cur.Tag);
            if (prism == null) return (null, "the face could not be pulled.");
            return (booleans.Union([cur, prism], cur.Tag), null);
        }
        var cut = Prism.Extrude(Shift(face.Outer, n * eps), face.Holes.Select(h => (IReadOnlyList<Vec3>)Shift(h, n * eps)).ToList(), n * (d - eps), cur.Tag);
        if (cut == null) return (null, "the face could not be pushed.");
        var o = booleans.Difference(cur, [cut], cur.Tag);
        return o == null ? (null, "nothing would be left.") : (o, null);
    }

    static List<Vec3> Shift(IReadOnlyList<Vec3> ring, Vec3 by) => ring.Select(p => p + by).ToList();

    /// <summary>
    /// An edge bevelled (a plane across it, <paramref name="size"/> back along each face) or rounded (a quarter round
    /// of radius <paramref name="size"/> tangent to both faces): a convex edge loses the corner, a concave one is filled.
    /// The piece runs a little past the edge's ends, so it goes through whatever the edge ends against.
    /// </summary>
    public static (Solid? Solid, string? Problem) Round(Solid cur, BrepEdge edge, double size, bool round, ISolidBooleans booleans)
    {
        if (edge.Second is not { } f2) return (null, "the edge has a face on one side only.");
        var e = edge.Dir;
        var (n1, n2) = (edge.First.Normal, f2.Normal);
        var mid = (edge.A + edge.B) * 0.5;
        Vec3 Into(BrepFace f, Vec3 n)
        {
            var w = Vec3.Cross(n, e).Normalized();
            return BrepPick.Inside(f, mid + w * 1e-3) ? w : -w;
        }
        var (w1, w2) = (Into(edge.First, n1), Into(f2, n2));
        var cos = Math.Clamp(Vec3.Dot(w1, w2), -1, 1);
        var theta = Math.Acos(cos);
        if (theta < 1e-3 || Math.PI - theta < 1e-3) return (null, "the faces on either side lie in one plane: there is no corner to round.");
        var convex = Vec3.Dot(w1, n2) < 0;
        var t = round ? size / Math.Tan(theta / 2) : size;
        if (t > Math.Min(Reach(edge.First, edge, w1), Reach(f2, edge, w2)) + 1e-9) return (null, $"{(round ? "the radius" : "the distance")} is larger than the faces beside the edge.");
        var m = Math.Max(size, 0.01) * 0.5;
        var (p1, p2) = (w1 * t, w2 * t);
        var ring = new List<Vec3>();
        // The corner's piece in the plane across the edge (relative to the edge): the faces' strips to the tangent points, the round or the bevel between.
        var curve = new List<Vec3> { p1 };
        if (round)
        {
            var bis = (w1 + w2).Normalized();
            var c = bis * (size / Math.Sin(theta / 2));
            var (r1, r2) = (p1 - c, p2 - c);
            const int steps = 12;
            var phi = Math.Acos(Math.Clamp(Vec3.Dot(r1.Normalized(), r2.Normalized()), -1, 1));
            var axis = Vec3.Cross(r1, r2).Normalized();
            for (var i = 1; i < steps; i++) curve.Add(c + Turn(r1, Vec3.Zero, axis, phi * i / steps));
        }
        curve.Add(p2);
        if (convex)
        {
            // Out past both faces, so nothing of the corner is left on them.
            ring.AddRange(curve);
            ring.AddRange([p2 + n2 * m, (n1 + n2) * m, p1 + n1 * m]);
        }
        else
        {
            // Sunk into the body behind both faces, so the fill meets it in volume.
            ring.AddRange(curve);
            ring.AddRange([p2 - n2 * m, -(n1 + n2) * m, p1 - n1 * m]);
        }
        var ext = 2 * t + m;
        var start = edge.A - e * ext;
        var piece = Prism.Extrude(ring.Select(q => q + start).ToList(), null, e * (edge.Length + 2 * ext), cur.Tag);
        if (piece == null) return (null, "the edge's piece could not be made.");
        if (convex)
        {
            var o = booleans.Difference(cur, [piece], cur.Tag);
            return o == null ? (null, "nothing would be left.") : (o, null);
        }
        return (booleans.Union([cur, piece], cur.Tag), null);
    }

    /// <summary>How far a face runs back from an edge, measured across it (the room a bevel or round has).</summary>
    static double Reach(BrepFace f, BrepEdge edge, Vec3 w) => f.Outer.Concat(f.Holes.SelectMany(h => h)).Max(p => Vec3.Dot(p - edge.A, w));

    /// <summary>Everything behind a plane (against its normal), or in front of it, as far as the body reaches: a box that big.</summary>
    static Solid? HalfSpace(Solid body, Vec3 at, Vec3 n, bool behind)
    {
        if (Measure.Bounds(body) is not { } b) return null;
        var r = (b.Max - b.Min).Length + (at - (b.Min + b.Max) * 0.5).Length + 1;
        var (u, v) = VecMath.PlaneBasis(n);
        var ring = new List<Vec3> { at - u * r - v * r, at + u * r - v * r, at + u * r + v * r, at - u * r + v * r };
        return Prism.Extrude(ring, null, n * (behind ? -r : r), body.Tag);
    }

    public static Vec3? P(JsonNode? n) =>
        n is JsonArray { Count: >= 3 } v && Json.Number(v[0]) is { } x && Json.Number(v[1]) is { } y && Json.Number(v[2]) is { } z ? new Vec3(x, y, z) : null;
    public static JsonArray J(Vec3 v) => new(Math.Round(v.X, 9), Math.Round(v.Y, 9), Math.Round(v.Z, 9));

    /// <summary>The steps carried by a move, turn or mirror in plan, with the sketch: their points and directions the same way.</summary>
    public static JsonArray? Map(JsonArray? features, Placement2D t)
    {
        if (features == null) return null;
        var o = new JsonArray();
        foreach (var node in features)
        {
            if (node is not JsonObject f) continue;
            var g = (JsonObject)f.DeepClone();
            foreach (var key in new[] { "point", "a", "b", "centre" })
                if (P(g[key]) is { } p) { var q = t.Point(p.Xy); g[key] = J(new Vec3(q.X, q.Y, p.Z)); }
            if (g["edges"] is JsonArray edges)
                foreach (var pair in edges.OfType<JsonArray>())
                    for (var j = 0; j < pair.Count; j++)
                        if (P(pair[j]) is { } p) { var q = t.Point(p.Xy); pair[j] = J(new Vec3(q.X, q.Y, p.Z)); }
            foreach (var key in new[] { "normal", "step", "axis", "by" })
                if (P(g[key]) is { } d) { var q = t.Dir(d.Xy); g[key] = J(new Vec3(q.X, q.Y, d.Z)); }
            // A mirror in plan turns the other way round an upright axis.
            if (t.Mirrored && Json.Number(g["angle"]) is { } ang && (string?)g["op"] is "rotate" or "array") g["angle"] = -ang;
            o.Add((JsonNode)g);
        }
        return o;
    }
}
