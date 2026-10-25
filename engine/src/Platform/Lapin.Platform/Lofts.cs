using BubbleBim.Geometry;

namespace Lapin.Platform;

/// <summary>A closed solid through rings of corresponding points (a profile's stations along a curved path), capped at both ends.</summary>
public static class Lofts
{
    public static Solid? Rings(IReadOnlyList<IReadOnlyList<Vec3>> rings, string tag)
    {
        if (rings.Count < 2 || rings[0].Count < 3 || rings.Any(r => r.Count != rings[0].Count)) return null;
        // Every ring counter-clockwise looking back along the path, so the sides face out as the caps do.
        if (Vec3.Dot(VecMath.NewellNormal(rings[0]), Centroid(rings[1]) - Centroid(rings[0])) < 0)
            rings = rings.Select(r => (IReadOnlyList<Vec3>)r.Reverse().ToList()).ToList();
        var v = new List<Vec3>();
        var t = new List<(int A, int B, int C)>();
        var ids = rings.Select(r => { var o = new int[r.Count]; for (var k = 0; k < r.Count; k++) { o[k] = v.Count; v.Add(r[k]); } return o; }).ToList();
        for (var i = 0; i + 1 < ids.Count; i++)
            for (var k = 0; k < ids[i].Length; k++)
            {
                var k2 = (k + 1) % ids[i].Length;
                var (a, b, c, d) = (ids[i][k], ids[i][k2], ids[i + 1][k2], ids[i + 1][k]);
                t.Add((a, b, c));
                t.Add((a, c, d));
            }
        void Cap(int[] ring, Vec3 normal)
        {
            var pts = ring.Select(i => v[i]).ToList();
            foreach (var (a, b, c) in Triangulator.Face(pts, Enumerable.Range(0, ring.Length).ToArray(), null, normal)) t.Add((ring[a], ring[b], ring[c]));
        }
        var dir0 = Centroid(rings[1]) - Centroid(rings[0]);
        var dir1 = Centroid(rings[^1]) - Centroid(rings[^2]);
        Cap(ids[0], -NormalOf(rings[0], dir0));
        Cap(ids[^1], NormalOf(rings[^1], dir1));
        // Triangles turned outward when the rings ran the other way round.
        double vol = 0;
        foreach (var (a, b, c) in t) vol += Vec3.Dot(v[a], Vec3.Cross(v[b], v[c])) / 6;
        var tris = vol < 0 ? t.Select(x => (x.A, x.C, x.B)).ToList() : t;
        return SolidFromMesh.Build(v, tris, tag);
    }

    static Vec3 Centroid(IReadOnlyList<Vec3> r) => new(r.Average(p => p.X), r.Average(p => p.Y), r.Average(p => p.Z));

    /// <summary>The ring's own normal, turned to run with <paramref name="dir"/>.</summary>
    static Vec3 NormalOf(IReadOnlyList<Vec3> ring, Vec3 dir)
    {
        var n = VecMath.NewellNormal(ring);
        return Vec3.Dot(n, dir) < 0 ? -n : n;
    }
}
