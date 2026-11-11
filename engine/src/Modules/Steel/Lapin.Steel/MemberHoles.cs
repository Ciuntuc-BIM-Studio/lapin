using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// A hole through a steel member (decision 480, from an SDNF file's packet 22): round (<see cref="Radius"/> &gt; 0, its
/// centre on each face, <see cref="Top"/> and <see cref="Bottom"/> one point each) or a loop of straight edges (the top
/// face's and the bottom's, vertex for vertex). Points are in the member's frame — the section's x and y and the station
/// along the axis from the member's origin (<see cref="MemberHoles.Origin"/>) — so a hole goes with its member when it moves.
/// <see cref="Number"/> is the SDNF number it came with, so a file read again finds it.
/// </summary>
public sealed record MemberHole(long? Number, double Radius, IReadOnlyList<Vec3> Top, IReadOnlyList<Vec3> Bottom)
{
    public bool Round => Radius > 0;
}

/// <summary>The holes a steel member, or a steel beam or column, is cut by (kept under <see cref="Key"/>), taken off its body and written to IFC as openings.</summary>
public static class MemberHoles
{
    public const string Key = "holes";

    /// <summary>How far a hole's cutter goes past the faces it is given between, so it leaves no skin.</summary>
    const double Past = 0.001;

    public static bool Has(Entity e) => e.Attrs[Key] is JsonArray { Count: > 0 };

    public static IReadOnlyList<MemberHole> Of(Entity e)
    {
        if (e.Attrs[Key] is not JsonArray a) return [];
        var o = new List<MemberHole>();
        foreach (var h in a.OfType<JsonObject>())
        {
            static List<Vec3> Points(JsonNode? n) => n is JsonArray l ? [.. l.Select(x => Args.TryPoint3(x)).OfType<Vec3>()] : [];
            var (top, bottom) = (Points(h["top"]), Points(h["bottom"]));
            var r = Json.Number(h["r"]) ?? 0;
            if (top.Count == 0 || top.Count != bottom.Count || (r <= 0 && top.Count < 3)) continue;
            o.Add(new MemberHole(Json.Number(h["sdnf"]) is { } n ? (long)n : null, r, top, bottom));
        }
        return o;
    }

    static double R(double v) => Math.Round(v, 9) is var x && x == 0 ? 0 : x;

    public static JsonArray Write(IEnumerable<MemberHole> holes) => new([.. holes.Select(h =>
    {
        var o = new JsonObject();
        if (h.Number is { } n) o["sdnf"] = n;
        if (h.Round) o["r"] = R(h.Radius);
        o["top"] = new JsonArray([.. h.Top.Select(p => (JsonNode)new JsonArray(R(p.X), R(p.Y), R(p.Z)))]);
        o["bottom"] = new JsonArray([.. h.Bottom.Select(p => (JsonNode)new JsonArray(R(p.X), R(p.Y), R(p.Z)))]);
        return (JsonNode)o;
    })]);

    /// <summary>
    /// Where a member's holes are measured from: the section's centre at the start of the member's own axis — a beam's own
    /// start at its top (not its clear span's, which moves with the columns), a column's foot, a member's start.
    /// </summary>
    public static Vec3 Origin(IModelView m, Entity e, MemberAxis ax)
    {
        var start = e.Class == IfcClass.Beam ? PathCurve.Of(e).Start.At(Beams.Top(m, e)) : ax.Start;
        return start + ax.Offset;
    }

    /// <summary>A point of the member's frame (x, y across, z along) in the world.</summary>
    public static Vec3 World(MemberAxis ax, Vec3 origin, Vec3 local)
    {
        var (v, w) = ax.Frame;
        return origin + v * local.X + w * local.Y + ax.U * local.Z;
    }

    /// <summary>A world point in the member's frame.</summary>
    public static Vec3 Local(MemberAxis ax, Vec3 origin, Vec3 p)
    {
        var (v, w) = ax.Frame;
        var d = p - origin;
        return new Vec3(Vec3.Dot(d, v), Vec3.Dot(d, w), Vec3.Dot(d, ax.U));
    }

    /// <summary>The holes in the world: each its top loop (a round one's by the chord rule) and the way through it to the bottom.</summary>
    public static IEnumerable<(MemberHole Hole, IReadOnlyList<Vec3> Top, Vec3 Through)> InWorld(IModelView m, Entity e, MemberAxis ax)
    {
        var origin = Origin(m, e, ax);
        foreach (var h in Of(e))
        {
            var top = h.Top.Select(p => World(ax, origin, p)).ToList();
            var bottom = h.Bottom.Select(p => World(ax, origin, p)).ToList();
            var through = bottom[0] - top[0];
            if (through.Length < 1e-6) continue;
            if (h.Round) top = Ring(top[0], through, h.Radius);
            yield return (h, top, through);
        }
    }

    /// <summary>A circle about <paramref name="c"/> across <paramref name="axis"/>, by the chord rule.</summary>
    public static List<Vec3> Ring(Vec3 c, Vec3 axis, double r)
    {
        var (e1, e2) = VecMath.PlaneBasis(axis.Normalized());
        var n = Math.Max(8, Chords.Circle(r));
        return [.. Enumerable.Range(0, n).Select(i => c + e1 * (r * Math.Cos(2 * Math.PI * i / n)) + e2 * (r * Math.Sin(2 * Math.PI * i / n)))];
    }

    /// <summary>The solids the holes take off the body: each its loop swept through, a millimetre past each face.</summary>
    public static List<Solid> Cutters(IModelView m, Entity e, MemberAxis ax)
    {
        var o = new List<Solid>();
        foreach (var (_, top, through) in InWorld(m, e, ax))
        {
            var d = through.Normalized();
            if (Prism.Extrude([.. top.Select(p => p - d * Past)], null, through + d * (2 * Past), "hole") is { } s) o.Add(s);
        }
        return o;
    }

    /// <summary>The holes as IFC's openings: each its loop laid across the way through, swept along it from before its first face to past its last.</summary>
    public static List<IfcSweep> Sweeps(IModelView m, Entity e, MemberAxis ax)
    {
        var o = new List<IfcSweep>();
        foreach (var (_, top, through) in InWorld(m, e, ax))
        {
            var d = through.Normalized();
            var (x, y) = VecMath.PlaneBasis(d);
            var along = top.Select(p => Vec3.Dot(p, d)).ToList();
            var start = along.Min() - Past;
            var depth = along.Max() + through.Length - start + Past;
            var origin = top[0] + d * (start - along[0]);
            var profile = top.Select(p => new Vec2(Vec3.Dot(p - origin, x), Vec3.Dot(p - origin, Vec3.Cross(d, x)))).ToList();
            o.Add(new IfcSweep(Polygon2.EnsureCcw(profile), [], origin, x, d, depth));
        }
        return o;
    }
}
