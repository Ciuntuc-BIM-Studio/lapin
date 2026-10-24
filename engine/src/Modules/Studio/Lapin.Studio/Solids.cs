using BubbleBim.Geometry;

namespace Lapin.Studio;

/// <summary>Studio's solids from a profile (its outline and holes, in 3D): an extrusion, a sweep along a path, a revolution about an axis.</summary>
public static class Solids
{
    public static Solid? Extrude(IReadOnlyList<Vec3> outer, IReadOnlyList<IReadOnlyList<Vec3>> holes, Vec3 d) =>
        Prism.Extrude(outer, holes, d, "studio");

    /// <summary>A closed triangle mesh, its triangles turned outward when they came out inside out.</summary>
    sealed class Mesh
    {
        public readonly List<Vec3> V = [];
        public readonly List<(int A, int B, int C)> T = [];
        public int[] Add(IEnumerable<Vec3> pts) { var o = new List<int>(); foreach (var p in pts) { o.Add(V.Count); V.Add(p); } return o.ToArray(); }
        public void Quad(int a, int b, int c, int d) { T.Add((a, b, c)); T.Add((a, c, d)); }

        public void Cap(int[] outer, int[][] holes, Vec3 normal, bool flip)
        {
            var all = outer.Concat(holes.SelectMany(h => h)).ToList();
            var pts = all.Select(i => V[i]).ToList();
            var local = new Dictionary<int, int>();
            for (var k = 0; k < all.Count; k++) local[k] = all[k];
            var o = Enumerable.Range(0, outer.Length).ToArray();
            var off = outer.Length;
            var hs = holes.Select(h => { var r = Enumerable.Range(off, h.Length).ToArray(); off += h.Length; return r; }).ToArray();
            foreach (var (a, b, c) in Triangulator.Face(pts, o, hs, normal))
                T.Add(flip ? (local[a], local[c], local[b]) : (local[a], local[b], local[c]));
        }

        public Solid? Build()
        {
            double vol = 0;
            foreach (var (a, b, c) in T) vol += Vec3.Dot(V[a], Vec3.Cross(V[b], V[c])) / 6;
            var tris = vol < 0 ? T.Select(t => (t.A, t.C, t.B)).ToList() : T;
            return SolidFromMesh.Build(V, tris, "studio");
        }
    }

    /// <summary>A ring oriented so its normal runs with <paramref name="dir"/> (holes against it).</summary>
    static IReadOnlyList<Vec3> Orient(IReadOnlyList<Vec3> ring, Vec3 dir, bool hole)
    {
        var along = Vec3.Dot(VecMath.NewellNormal(ring), dir) >= 0;
        return along != hole ? ring : ring.Reverse().ToList();
    }

    /// <summary>
    /// Rings of the same profile one after another (a sweep's stations, a
    /// revolution's steps), joined side by side; capped at both ends unless
    /// the last ring closes back on the first.
    /// </summary>
    static Solid? Loft(List<List<IReadOnlyList<Vec3>>> stations, bool closed, Vec3 startDir, Vec3 endDir)
    {
        var mesh = new Mesh();
        var ids = stations.Select(st => st.Select(mesh.Add).ToList()).ToList();
        var last = closed ? ids.Count : ids.Count - 1;
        for (var i = 0; i < last; i++)
        {
            var (a, b) = (ids[i], ids[(i + 1) % ids.Count]);
            for (var r = 0; r < a.Count; r++)
                for (var k = 0; k < a[r].Length; k++)
                {
                    var k2 = (k + 1) % a[r].Length;
                    mesh.Quad(a[r][k], a[r][k2], b[r][k2], b[r][k]);
                }
        }
        if (!closed)
        {
            mesh.Cap(ids[0][0], ids[0].Skip(1).ToArray(), -startDir, false);
            mesh.Cap(ids[^1][0], ids[^1].Skip(1).ToArray(), endDir, false);
        }
        return mesh.Build();
    }

    /// <summary>
    /// The profile (2D: its outline and holes) standing on a path in a plane
    /// (normal <paramref name="up"/>): the bottom middle of its box on the
    /// path, its x across the path, its y along the normal; mitred at corners.
    /// </summary>
    public static Solid? Sweep(IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> holes, IReadOnlyList<Vec3> path, Vec3 up)
    {
        if (path.Count < 2 || outer.Count < 3) return null;
        var (minX, maxX, minY) = (outer.Min(p => p.X), outer.Max(p => p.X), outer.Min(p => p.Y));
        var cx = (minX + maxX) / 2;
        Vec2 L(Vec2 p) => new(p.X - cx, p.Y - minY);
        var n = path.Count;
        var tangents = Enumerable.Range(0, n - 1).Select(i => (path[i + 1] - path[i]).Normalized()).ToList();
        var stations = new List<List<IReadOnlyList<Vec3>>>();
        for (var i = 0; i < n; i++)
        {
            Vec3 side;
            if (i == 0) side = Vec3.Cross(tangents[0], up).Normalized();
            else if (i == n - 1) side = Vec3.Cross(tangents[^1], up).Normalized();
            else
            {
                var (s1, s2) = (Vec3.Cross(tangents[i - 1], up).Normalized(), Vec3.Cross(tangents[i], up).Normalized());
                var bis = (s1 + s2).Normalized();
                side = bis * (1 / Math.Max(Vec3.Dot(bis, s1), 0.2));
            }
            var at = path[i];
            var dir = i < n - 1 ? tangents[i] : tangents[^1];
            Vec3 W(Vec2 p) { var q = L(p); return at + side * q.X + up * q.Y; }
            var rings = new List<IReadOnlyList<Vec3>> { Orient(outer.Select(W).ToList(), dir, false) };
            rings.AddRange(holes.Select(h => Orient(h.Select(W).ToList(), dir, true)));
            stations.Add(rings);
        }
        // Every station maps the same 2D profile on its own (side, up): they all come out the same way round.
        return Loft(stations, false, tangents[0], tangents[^1]);
    }

    /// <summary>The profile (3D: its outline and holes) turned about an axis by an angle (degrees; 360 closes it).</summary>
    public static Solid? Revolve(IReadOnlyList<Vec3> outer, IReadOnlyList<IReadOnlyList<Vec3>> holes, Vec3 axisAt, Vec3 axisDir, double angleDeg)
    {
        var d = axisDir.Normalized();
        var full = Math.Abs(Math.Abs(angleDeg) - 360) < 1e-6;
        var angle = Math.Clamp(angleDeg, -360, 360) * Math.PI / 180;
        if (Math.Abs(angle) < 1e-6 || outer.Count < 3) return null;
        var steps = Math.Max(3, (int)Math.Ceiling(Math.Abs(angle) / (Math.PI / 18)));
        Vec3 Rot(Vec3 p, double t)
        {
            var v = p - axisAt;
            var (c, s) = (Math.Cos(t), Math.Sin(t));
            return axisAt + v * c + Vec3.Cross(d, v) * s + d * (Vec3.Dot(d, v) * (1 - c));
        }
        // The way the profile turns at its middle: its rings face that way.
        var mid = new Vec3(outer.Average(p => p.X), outer.Average(p => p.Y), outer.Average(p => p.Z));
        var turn = Vec3.Cross(d, mid - axisAt) * Math.Sign(angle);
        if (turn.Length < 1e-12) return null;
        var o = Orient(outer, turn, false);
        var hs = holes.Select(h => Orient(h, turn, true)).ToList();
        var stations = new List<List<IReadOnlyList<Vec3>>>();
        for (var j = 0; j < (full ? steps : steps + 1); j++)
        {
            var t = angle * j / steps;
            var rings = new List<IReadOnlyList<Vec3>> { o.Select(p => Rot(p, t)).ToList() };
            rings.AddRange(hs.Select(h => (IReadOnlyList<Vec3>)h.Select(p => Rot(p, t)).ToList()));
            stations.Add(rings);
        }
        var endTurn = Vec3.Cross(d, Rot(mid, angle) - axisAt) * Math.Sign(angle);
        return Loft(stations, full, turn.Normalized(), endTurn.Normalized());
    }
}
