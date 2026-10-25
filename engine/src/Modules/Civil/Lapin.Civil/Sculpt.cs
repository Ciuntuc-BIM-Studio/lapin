using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;

namespace Lapin.Civil;

/// <summary>One stroke of the terrain brush: where, how wide, and what it does (raise or lower by an amount, flatten to a level, smooth with a strength).</summary>
public sealed record SculptDab(string Kind, Vec2 At, double Radius, double Amount, double Level, double Strength)
{
    public static readonly string[] Kinds = ["raise", "lower", "flatten", "smooth"];

    public JsonObject ToJson() => new() { ["kind"] = Kind, ["x"] = At.X, ["y"] = At.Y, ["radius"] = Radius, ["amount"] = Amount, ["level"] = Level, ["strength"] = Strength };

    public static SculptDab? From(JsonNode? n) =>
        n is JsonObject o && Kinds.Contains((string?)o["kind"]) && Json.Number(o["radius"]) is { } r && r > 0.05
            ? new((string)o["kind"]!, new Vec2(Json.Number(o["x"]) ?? 0, Json.Number(o["y"]) ?? 0), r, Json.Number(o["amount"]) ?? 0, Json.Number(o["level"]) ?? 0, Math.Clamp(Json.Number(o["strength"]) ?? 0.5, 0, 1))
            : null;
}

/// <summary>
/// The terrain brush on a TIN. A stroke is kept as data on the terrain and
/// applied again whenever the terrain is built, in order, over the survey:
/// it can be undone and cleared. Each stroke makes the TIN dense inside its
/// circle (a grid at a quarter of the radius) and moves the heights there by
/// a weight that fades smoothly to nothing at the rim, so the ground stays
/// continuous around it.
/// </summary>
public static class Sculpt
{
    public static List<SculptDab> Of(Entity terrain) =>
        (terrain.Attrs["sculpts"] as JsonArray)?.Select(SculptDab.From).OfType<SculptDab>().ToList() ?? [];

    /// <summary>The points (model heights) after the strokes; <paramref name="segments"/> index the breaklines, which keep their indices (points are only appended or moved).</summary>
    public static List<Vec3> Apply(List<Vec3> points, IReadOnlyList<(int A, int B)> segments, IReadOnlyList<SculptDab> dabs)
    {
        foreach (var dab in dabs) points = One(points, segments, dab);
        return points;
    }

    static double Weight(SculptDab d, Vec2 p)
    {
        var t = 1 - (p - d.At).Length / d.Radius;
        return t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);
    }

    static List<Vec3> One(List<Vec3> points, IReadOnlyList<(int A, int B)> segments, SculptDab d)
    {
        var before = new Terrain(Tin.Delaunay(points, segments));
        if (before.Tin.Triangles.Count == 0) return points;
        var step = Math.Max(d.Radius / 4, 0.25);
        double Target(Vec2 p, double z0, double w) => d.Kind switch
        {
            "raise" => z0 + d.Amount * w,
            "lower" => z0 - d.Amount * w,
            "flatten" => z0 + (d.Level - z0) * w,
            _ => z0 + (Around(before, p, step) - z0) * w * d.Strength,
        };
        var o = new List<Vec3>(points.Count);
        var taken = new HashSet<(long, long)>();
        foreach (var p in points)
        {
            var w = Weight(d, new Vec2(p.X, p.Y));
            o.Add(w > 0 ? p with { Z = Target(new Vec2(p.X, p.Y), p.Z, w) } : p);
            taken.Add(Key(p.X, p.Y, step));
        }
        for (var x = d.At.X - d.Radius; x <= d.At.X + d.Radius + 1e-9; x += step)
            for (var y = d.At.Y - d.Radius; y <= d.At.Y + d.Radius + 1e-9; y += step)
            {
                var p = new Vec2(x, y);
                var w = Weight(d, p);
                if (w <= 1e-6 || !taken.Add(Key(x, y, step)) || before.ElevationAt(x, y) is not { } z0) continue;
                o.Add(new Vec3(x, y, Target(p, z0, w)));
            }
        return o;
    }

    /// <summary>The mean height of the ground a step away in four directions (what smoothing pulls towards).</summary>
    static double Around(Terrain t, Vec2 p, double step)
    {
        var sum = 0.0; var n = 0;
        foreach (var (dx, dy) in new[] { (step, 0.0), (-step, 0.0), (0.0, step), (0.0, -step) })
            if (t.ElevationAt(p.X + dx, p.Y + dy) is { } z) { sum += z; n++; }
        return n == 0 ? t.ElevationAt(p.X, p.Y) ?? 0 : sum / n;
    }

    static (long, long) Key(double x, double y, double step) => ((long)Math.Round(x / (step * 0.45)), (long)Math.Round(y / (step * 0.45)));
}
