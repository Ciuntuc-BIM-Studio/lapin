using System.Text.Json.Nodes;
using Lapin.Geometry;

namespace Lapin.Studio;

/// <summary>A planar face of a body as it is built now: its outline and holes (model coordinates), its outward normal.</summary>
public sealed record BrepFace(IReadOnlyList<Vec3> Outer, IReadOnlyList<IReadOnlyList<Vec3>> Holes, Vec3 Normal)
{
    public Vec3 Centre => new(Outer.Average(p => p.X), Outer.Average(p => p.Y), Outer.Average(p => p.Z));
    public double Area => Math.Abs(Vec3.Dot(VecMath.NewellNormal(Outer), Normal)) - Holes.Sum(h => Math.Abs(Vec3.Dot(VecMath.NewellNormal(h), Normal)));

    /// <summary>Its boundary's segments: the outline's, then each hole's.</summary>
    public IEnumerable<(Vec3 A, Vec3 B)> Segments()
    {
        foreach (var ring in Holes.Prepend(Outer))
            for (var i = 0; i < ring.Count; i++) yield return (ring[i], ring[(i + 1) % ring.Count]);
    }

    public JsonObject ToJson() => new()
    {
        ["normal"] = new JsonArray(Normal.X, Normal.Y, Normal.Z),
        ["outline"] = new JsonArray(Outer.Select(p => (JsonNode)new JsonArray(p.X, p.Y, p.Z)).ToArray()),
        ["holes"] = new JsonArray(Holes.Select(h => (JsonNode)new JsonArray(h.Select(p => (JsonNode)new JsonArray(p.X, p.Y, p.Z)).ToArray())).ToArray()),
        ["area"] = Math.Round(Area, 6),
    };
}

/// <summary>An edge of a body: its two ends and the faces on either side (the second missing on an open body).</summary>
public sealed record BrepEdge(Vec3 A, Vec3 B, BrepFace First, BrepFace? Second)
{
    public Vec3 Dir => (B - A).Normalized();
    public double Length => (B - A).Length;

    public JsonObject ToJson() => new()
    {
        ["a"] = new JsonArray(A.X, A.Y, A.Z), ["b"] = new JsonArray(B.X, B.Y, B.Z), ["length"] = Math.Round(Length, 6),
        ["normal"] = new JsonArray(First.Normal.X, First.Normal.Y, First.Normal.Z),
    };
}

/// <summary>
/// The faces and edges of a body found again by where they are: a pick in 3D
/// (a point on the face and the normal of the triangle hit), or what a Studio
/// operation keeps of the face or edge it works on (a point and a normal, two
/// ends), so a push/pull or a fillet finds its face again after the sketch
/// under it changed.
/// </summary>
public static class BrepPick
{
    public static IEnumerable<BrepFace> Faces(Solid s) => s.Faces.Select(f => new BrepFace(
        f.Outer.Select(i => s.Vertices[i]).ToList(),
        f.Holes.Select(h => (IReadOnlyList<Vec3>)h.Select(i => s.Vertices[i]).ToList()).ToList(),
        f.Normal.Normalized()));

    /// <summary>
    /// The face a point lies on facing the way given (within about 5°): of those, the one whose plane is nearest the
    /// point and whose outline holds it, else the nearest; null when no face faces that way within <paramref name="reach"/> of its plane.
    /// </summary>
    public static BrepFace? Face(IEnumerable<Solid> solids, Vec3 point, Vec3 normal, double reach = 0.05)
    {
        var n = normal.Normalized();
        BrepFace? best = null;
        var score = double.MaxValue;
        foreach (var f in solids.SelectMany(Faces))
        {
            if (Vec3.Dot(f.Normal, n) < 0.996) continue;
            var plane = Math.Abs(Vec3.Dot(point - f.Outer[0], f.Normal));
            if (plane > reach) continue;
            var s = plane + (Inside(f, point) ? 0 : 1 + OutlineDistance(f, point));
            if (s < score) { score = s; best = f; }
        }
        return best;
    }

    /// <summary>Whether a point near a face's plane lies within its outline and outside its holes (in the face's own axes).</summary>
    public static bool Inside(BrepFace f, Vec3 p)
    {
        var (u, v) = VecMath.PlaneBasis(f.Normal);
        var at = VecMath.Project(p, u, v);
        var outer = f.Outer.Select(q => VecMath.Project(q, u, v)).ToList();
        if (!Polygon2.Contains(outer, at) && SegmentsDistance(outer, at) > 1e-4) return false;
        return !f.Holes.Any(h => { var ring = h.Select(q => VecMath.Project(q, u, v)).ToList(); return Polygon2.Contains(ring, at) && SegmentsDistance(ring, at) > 1e-4; });
    }

    static double SegmentsDistance(IReadOnlyList<Vec2> ring, Vec2 p)
    {
        var best = double.MaxValue;
        for (var i = 0; i < ring.Count; i++) best = Math.Min(best, SegDistance(ring[i], ring[(i + 1) % ring.Count], p));
        return best;
    }

    static double SegDistance(Vec2 a, Vec2 b, Vec2 p)
    {
        var d = b - a;
        var t = Math.Clamp(Vec2.Dot(p - a, d) / Math.Max(Vec2.Dot(d, d), 1e-18), 0, 1);
        return (a + d * t - p).Length;
    }

    public static double SegDistance(Vec3 a, Vec3 b, Vec3 p)
    {
        var d = b - a;
        var t = Math.Clamp(Vec3.Dot(p - a, d) / Math.Max(Vec3.Dot(d, d), 1e-18), 0, 1);
        return (a + d * t - p).Length;
    }

    static double OutlineDistance(BrepFace f, Vec3 p) => f.Segments().Min(s => SegDistance(s.A, s.B, p));

    /// <summary>
    /// The edge nearest a point picked on a face (the face it lies on, facing the way given), with the face on its
    /// other side; null when the point is on no face.
    /// </summary>
    public static BrepEdge? EdgeAt(IReadOnlyList<Solid> solids, Vec3 point, Vec3 normal)
    {
        if (Face(solids, point, normal) is not { } f) return null;
        var (a, b) = f.Segments().OrderBy(s => SegDistance(s.A, s.B, point)).First();
        return Edge(solids, a, b, f);
    }

    /// <summary>
    /// The edge lying along a–b now: a face's boundary segment along the same line overlapping it (the nearest), and
    /// the other face along that line; null when the body has no edge there any more.
    /// </summary>
    public static BrepEdge? Find(IReadOnlyList<Solid> solids, Vec3 a, Vec3 b, double reach = 0.02)
    {
        var d = (b - a).Normalized();
        var mid = (a + b) * 0.5;
        (Vec3 A, Vec3 B, BrepFace F)? best = null;
        var score = double.MaxValue;
        foreach (var f in solids.SelectMany(Faces))
            foreach (var (p, q) in f.Segments())
            {
                var e = q - p;
                if (e.Length < 1e-9 || Math.Abs(Vec3.Dot(e.Normalized(), d)) < 0.999) continue;
                var off = LineDistance(p, e.Normalized(), mid);
                if (off > reach) continue;
                var s = off + SegDistance(p, q, mid);
                if (s < score) { score = s; best = (p, q, f); }
            }
        return best is { } x ? Edge(solids, x.A, x.B, x.F) : null;
    }

    static double LineDistance(Vec3 p, Vec3 dir, Vec3 at) { var v = at - p; return (v - dir * Vec3.Dot(v, dir)).Length; }

    /// <summary>An edge a–b of face <paramref name="first"/>, the other face along it found by a boundary segment on the same line that overlaps it.</summary>
    static BrepEdge Edge(IReadOnlyList<Solid> solids, Vec3 a, Vec3 b, BrepFace first)
    {
        var d = (b - a).Normalized();
        var len = (b - a).Length;
        BrepFace? second = null;
        foreach (var f in solids.SelectMany(Faces))
        {
            if (Vec3.Dot(f.Normal, first.Normal) > 0.9999) continue;
            foreach (var (p, q) in f.Segments())
            {
                var e = q - p;
                if (e.Length < 1e-9 || Math.Abs(Vec3.Dot(e.Normalized(), d)) < 0.9999) continue;
                if (LineDistance(a, d, p) > 1e-5) continue;
                var (t0, t1) = (Vec3.Dot(p - a, d), Vec3.Dot(q - a, d));
                if (Math.Max(t0, t1) < 1e-6 || Math.Min(t0, t1) > len - 1e-6) continue;
                second = f;
                break;
            }
            if (second != null) break;
        }
        return new BrepEdge(a, b, first, second);
    }
}
