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

    /// <summary>Where a sweep's profile sits on its path, looking along the path.</summary>
    public static readonly string[] Places = ["centre", "right", "left", "outside", "inside"];

    /// <summary>A sweep that could not be built, and why (a side too short for its mitres…).</summary>
    public sealed record SweepResult(Solid? Solid, string? Problem);

    /// <summary>
    /// The profile (2D: its outline and holes) standing on a path in a plane
    /// (normal <paramref name="up"/>): its y along the normal, from the
    /// bottom of its box; its x across the path, placed by <paramref name="place"/>:
    /// <list type="bullet">
    /// <item><c>centre</c>: the middle of its box on the path (shifted right by the offset);</item>
    /// <item><c>right</c> or <c>left</c> of the path: its left edge (smallest x) on the path, the profile on that side, mirrored for the left so its left edge always meets the path;</item>
    /// <item><c>outside</c> or <c>inside</c> a closed path: right or left as the path turns, whichever way it was drawn.</item>
    /// </list>
    /// The offset is the gap between the path and the profile's near edge.
    /// Corners are mitred; a closed path (its last point on its first) gives
    /// one ring, every corner mitred, without end faces.
    /// </summary>
    public static SweepResult SweepPlaced(IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> holes, IReadOnlyList<Vec3> path, Vec3 up, string place = "centre", double offset = 0)
    {
        if (outer.Count < 3) return new(null, "The profile needs three points at least.");
        var pts = new List<Vec3>();
        foreach (var p in path) if (pts.Count == 0 || (p - pts[^1]).Length > 1e-9) pts.Add(p);
        var closed = pts.Count >= 4 && (pts[^1] - pts[0]).Length < 1e-6;
        if (closed) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < 2 || (closed && pts.Count < 3)) return new(null, "The path needs two points at least.");
        var n = pts.Count;
        var segs = closed ? n : n - 1;
        var tangents = Enumerable.Range(0, segs).Select(i => (pts[(i + 1) % n] - pts[i]).Normalized()).ToList();
        Vec3 Right(Vec3 t) => Vec3.Cross(t, up).Normalized();

        // Which side: outside and inside follow the way a closed path turns (seen from above its plane).
        var u = up.Normalized();
        var turn = 0.0;
        for (var i = 0; i < n; i++) turn += Vec3.Dot(Vec3.Cross(pts[i], pts[(i + 1) % n]), u);
        var ccw = turn > 0;
        var side = place switch
        {
            "right" => 1, "left" => -1,
            "outside" => ccw || !closed ? 1 : -1,
            "inside" => ccw || !closed ? -1 : 1,
            _ => 0,
        };
        var (minX, maxX, minY) = (outer.Min(p => p.X), outer.Max(p => p.X), outer.Min(p => p.Y));
        var cx = (minX + maxX) / 2;
        Vec2 L(Vec2 p) => side == 0
            ? new(p.X - cx + offset, p.Y - minY)
            : new(side * (p.X - minX + offset), p.Y - minY);
        var reach = outer.Concat(holes.SelectMany(h => h)).Max(p => Math.Abs(L(p).X));

        // A corner: its mitre (the two sides' bisector, stretched) and how far along each side it reaches.
        var mitres = new Vec3[n];
        var takes = new double[n];
        for (var i = 0; i < n; i++)
        {
            var hasPrev = closed || i > 0;
            var hasNext = closed || i < n - 1;
            if (!hasPrev) { mitres[i] = Right(tangents[0]); continue; }
            if (!hasNext) { mitres[i] = Right(tangents[^1]); continue; }
            var (s1, s2) = (Right(tangents[(i - 1 + segs) % segs]), Right(tangents[i % segs]));
            var bis = (s1 + s2).Normalized();
            var cos = Vec3.Dot(bis, s1);
            mitres[i] = bis * (1 / Math.Max(cos, 0.2));
            // Half the turn: the section's far edge runs past the corner by reach · tan(half).
            takes[i] = reach * Math.Sqrt(Math.Max(0, 1 / (Math.Max(cos, 0.2) * Math.Max(cos, 0.2)) - 1));
        }
        for (var i = 0; i < segs; i++)
        {
            var len = (pts[(i + 1) % n] - pts[i]).Length;
            if (takes[i] + takes[(i + 1) % n] > len + 1e-9)
                return new(null, $"Side {i + 1} of the path ({len:0.###} m) is too short for its mitred corners: the section would fold. Lengthen it or narrow the profile.");
        }

        var stations = new List<List<IReadOnlyList<Vec3>>>();
        for (var i = 0; i < n; i++)
        {
            var at = pts[i];
            var dir = closed ? (tangents[(i - 1 + segs) % segs] + tangents[i % segs]).Normalized() : i < n - 1 ? tangents[i] : tangents[^1];
            if (dir.Length < 1e-9) dir = tangents[i % segs];
            Vec3 W(Vec2 p) { var q = L(p); return at + mitres[i] * q.X + u * q.Y; }
            var rings = new List<IReadOnlyList<Vec3>> { Orient(outer.Select(W).ToList(), dir, false) };
            rings.AddRange(holes.Select(h => Orient(h.Select(W).ToList(), dir, true)));
            stations.Add(rings);
        }
        var solid = Loft(stations, closed, tangents[0], tangents[^1]);
        return new(solid, solid == null ? "The swept solid could not be built." : null);
    }

    /// <summary>The profile on the path, its middle on it, mitred at corners (closed paths make a ring).</summary>
    public static Solid? Sweep(IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> holes, IReadOnlyList<Vec3> path, Vec3 up) =>
        SweepPlaced(outer, holes, path, up).Solid;

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
