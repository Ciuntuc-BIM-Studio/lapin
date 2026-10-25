using Lapin.Geometry;

namespace Lapin.Civil;

/// <summary>What the site's elements build with: solids lofted between two sections, oriented prisms, a densified path, a seeded random.</summary>
public static class SiteGeometry
{
    /// <summary>A solid between two rings of the same count (two sections of a path): the rings as caps, a quad between each pair of edges; faces turned so the volume is positive.</summary>
    public static Solid? Loft(IReadOnlyList<Vec3> a, IReadOnlyList<Vec3> b, string? tag = null)
    {
        if (a.Count < 3 || a.Count != b.Count) return null;
        var n = a.Count;
        var verts = new List<Vec3>(a);
        verts.AddRange(b);
        var faces = new List<Face>
        {
            Face([.. Enumerable.Range(0, n).Reverse()], verts),
            Face([.. Enumerable.Range(n, n)], verts),
        };
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            faces.Add(Face([i, j, n + j, n + i], verts));
        }
        var s = new Solid([.. verts], [.. faces], tag);
        if (Measure.Volume(s) < 1e-12) return null;
        return Measure.SignedVolume(s) < 0 ? Flip(s) : s;
    }

    static Face Face(int[] ring, List<Vec3> verts)
    {
        var normal = VecMath.NewellNormal(ring.Select(i => verts[i]).ToList());
        return new Face(ring, [], normal.Length < 1e-15 ? Vec3.UnitZ : normal.Normalized());
    }

    static Solid Flip(Solid s) => new(s.Vertices, s.Faces.Select(f => f with { Outer = [.. f.Outer.Reverse()], Holes = f.Holes.Select(h => h.Reverse().ToArray()).ToArray(), Normal = -f.Normal }).ToArray(), s.Tag);

    /// <summary>A profile extruded along a vector, its volume positive whichever way the ring was given.</summary>
    public static Solid? Oriented(List<Vec3> ring, Vec3 along, string? tag = null)
    {
        var s = Prism.Extrude(ring, null, along, tag);
        if (s == null || Measure.SignedVolume(s) >= 0) return s;
        var reversed = new List<Vec3>(ring);
        reversed.Reverse();
        return Prism.Extrude(reversed, null, along, tag);
    }

    /// <summary>A polyline with points added so no segment is longer than <paramref name="step"/>; repeated points dropped.</summary>
    public static List<Vec2> Densify(IReadOnlyList<Vec2> path, double step)
    {
        var o = new List<Vec2>();
        for (var i = 0; i < path.Count; i++)
        {
            if (o.Count > 0 && (path[i] - o[^1]).Length < 1e-6) continue;
            if (o.Count > 0 && step > 0)
            {
                var a = o[^1];
                var len = (path[i] - a).Length;
                var n = (int)Math.Ceiling(len / step);
                for (var k = 1; k < n; k++) o.Add(a + (path[i] - a) * ((double)k / n));
            }
            o.Add(path[i]);
        }
        return o;
    }

    /// <summary>The direction of a path at each of its points: a segment's, or the mean of the two that meet there.</summary>
    public static List<Vec2> Directions(IReadOnlyList<Vec2> path)
    {
        var o = new List<Vec2>();
        for (var i = 0; i < path.Count; i++)
        {
            var before = i > 0 ? (path[i] - path[i - 1]).Normalized() : default;
            var after = i + 1 < path.Count ? (path[i + 1] - path[i]).Normalized() : default;
            var d = i == 0 ? after : i + 1 == path.Count ? before : (before + after);
            if (d.Length < 1e-9) d = i == 0 ? after : before;
            o.Add(d.Length < 1e-9 ? new Vec2(1, 0) : d.Normalized());
        }
        return o;
    }

    public static double Length(IReadOnlyList<Vec2> path)
    {
        var l = 0.0;
        for (var i = 0; i + 1 < path.Count; i++) l += (path[i + 1] - path[i]).Length;
        return l;
    }

    /// <summary>A small, seeded random (xorshift), the same sequence for the same seed on every machine.</summary>
    public sealed class Random(int seed)
    {
        uint _s = seed == 0 ? 0x9e3779b9u : (uint)seed;
        public double Next()
        {
            _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
            return _s / 4294967296.0;
        }
    }
}
