using Lapin.Geometry;

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

    /// <summary>Rings of corresponding points turned all together, so their normals run with <paramref name="dir"/> (the first one decides).</summary>
    static List<List<IReadOnlyList<Vec3>>> OrientAll(List<List<Vec3>> rings, Vec3 dir)
    {
        var flip = Vec3.Dot(VecMath.NewellNormal(rings[0]), dir) < 0;
        return rings.Select(r => new List<IReadOnlyList<Vec3>> { flip ? r.AsEnumerable().Reverse().ToList() : r }).ToList();
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

    // ------------------------------------------------------------ profiles that change: morphs, rails, lofts

    /// <summary>
    /// Closed rings as corresponding points: each counter-clockwise, from where
    /// the ray east of its middle meets it, sampled at the same fractions of
    /// its length (every ring's corners, and an even fill), so a square meets a
    /// circle corner to corner without losing its corners.
    /// </summary>
    public static List<List<Vec2>> Matched(IReadOnlyList<IReadOnlyList<Vec2>> rings, int fill = 48)
    {
        var norm = rings.Select(r => Polygon2.SignedArea(r) < 0 ? r.Reverse().ToList() : r.ToList()).ToList();
        var starts = new List<double>();
        var lengths = new List<double[]>();
        var fractions = new SortedSet<double>(Enumerable.Range(0, fill).Select(i => (double)i / fill));
        foreach (var r in norm)
        {
            var cum = new double[r.Count + 1];
            for (var i = 0; i < r.Count; i++) cum[i + 1] = cum[i] + (r[(i + 1) % r.Count] - r[i]).Length;
            var total = cum[^1];
            var mid = new Vec2((r.Min(p => p.X) + r.Max(p => p.X)) / 2, (r.Min(p => p.Y) + r.Max(p => p.Y)) / 2);
            // Where the ray east of the middle crosses the ring (the farthest crossing, for odd shapes).
            var s0 = 0.0; var best = double.NegativeInfinity;
            for (var i = 0; i < r.Count; i++)
            {
                var (a, b) = (r[i], r[(i + 1) % r.Count]);
                if ((a.Y - mid.Y) * (b.Y - mid.Y) > 0 || Math.Abs(b.Y - a.Y) < 1e-12) continue;
                var t = (mid.Y - a.Y) / (b.Y - a.Y);
                var x = a.X + (b.X - a.X) * t;
                if (x > mid.X && x > best) { best = x; s0 = cum[i] + (b - a).Length * t; }
            }
            starts.Add(s0);
            lengths.Add(cum);
            for (var i = 0; i < r.Count; i++) fractions.Add((((cum[i] - s0) % total) + total) % total / total);
        }
        var fs = new List<double>();
        foreach (var f in fractions) if (fs.Count == 0 || f - fs[^1] > 1e-6) fs.Add(f);
        if (fs.Count > 1 && 1 - fs[^1] < 1e-6) fs.RemoveAt(fs.Count - 1);
        var o = new List<List<Vec2>>();
        for (var k = 0; k < norm.Count; k++)
        {
            var (r, cum, s0) = (norm[k], lengths[k], starts[k]);
            var total = cum[^1];
            o.Add(fs.Select(f =>
            {
                var at = (s0 + f * total) % total;
                var i = Array.FindLastIndex(cum, c => c <= at + 1e-12);
                i = Math.Clamp(i, 0, r.Count - 1);
                var seg = cum[i + 1] - cum[i];
                var t = seg < 1e-12 ? 0 : (at - cum[i]) / seg;
                var (a, b) = (r[i], r[(i + 1) % r.Count]);
                return a + (b - a) * t;
            }).ToList());
        }
        return o;
    }

    /// <summary>Points at fractions of a polyline's length (its own vertices and the other's, so both rails keep their corners).</summary>
    static List<double> RailFractions(params IReadOnlyList<Vec3>[] rails)
    {
        var fs = new SortedSet<double> { 0, 1 };
        foreach (var r in rails)
        {
            var total = 0.0; var cum = new List<double> { 0 };
            for (var i = 0; i + 1 < r.Count; i++) { total += (r[i + 1] - r[i]).Length; cum.Add(total); }
            if (total > 0) foreach (var c in cum) fs.Add(c / total);
        }
        var o = new List<double>();
        foreach (var f in fs) if (o.Count == 0 || f - o[^1] > 1e-6) o.Add(f);
        return o;
    }

    static Vec3 At(IReadOnlyList<Vec3> r, double f)
    {
        var total = 0.0;
        for (var i = 0; i + 1 < r.Count; i++) total += (r[i + 1] - r[i]).Length;
        var at = f * total;
        for (var i = 0; i + 1 < r.Count; i++)
        {
            var seg = (r[i + 1] - r[i]).Length;
            if (at <= seg + 1e-12) return r[i] + (r[i + 1] - r[i]) * (seg < 1e-12 ? 0 : Math.Min(1, at / seg));
            at -= seg;
        }
        return r[^1];
    }

    /// <summary>
    /// A sweep whose profile becomes another on the way: the start profile's
    /// box bottom-middle on the path at its start, the end profile's at its
    /// end, the points matched and blended by the distance along the path;
    /// mitred at corners. Holes are not carried (their counts rarely agree).
    /// </summary>
    public static SweepResult SweepMorph(IReadOnlyList<Vec2> start, IReadOnlyList<Vec2> end, IReadOnlyList<Vec3> path, Vec3 up)
    {
        if (start.Count < 3 || end.Count < 3) return new(null, "Both profiles need three points at least.");
        var pts = new List<Vec3>();
        foreach (var p in path) if (pts.Count == 0 || (p - pts[^1]).Length > 1e-9) pts.Add(p);
        if (pts.Count < 2) return new(null, "The path needs two points at least.");
        static List<Vec2> Local(IReadOnlyList<Vec2> r) { var (cx, minY) = ((r.Min(p => p.X) + r.Max(p => p.X)) / 2, r.Min(p => p.Y)); return r.Select(p => new Vec2(p.X - cx, p.Y - minY)).ToList(); }
        var m = Matched([Local(start), Local(end)]);
        var u = up.Normalized();
        var n = pts.Count;
        var tangents = Enumerable.Range(0, n - 1).Select(i => (pts[i + 1] - pts[i]).Normalized()).ToList();
        Vec3 Right(Vec3 t) => Vec3.Cross(t, up).Normalized();
        var cum = new double[n];
        for (var i = 1; i < n; i++) cum[i] = cum[i - 1] + (pts[i] - pts[i - 1]).Length;
        var stations = new List<List<IReadOnlyList<Vec3>>>();
        for (var i = 0; i < n; i++)
        {
            Vec3 side;
            if (i == 0) side = Right(tangents[0]);
            else if (i == n - 1) side = Right(tangents[^1]);
            else { var (s1, s2) = (Right(tangents[i - 1]), Right(tangents[i])); var bis = (s1 + s2).Normalized(); side = bis * (1 / Math.Max(Vec3.Dot(bis, s1), 0.2)); }
            var t = cum[^1] < 1e-12 ? 0 : cum[i] / cum[^1];
            var dir = i < n - 1 ? tangents[i] : tangents[^1];
            stations.Add([m[0].Zip(m[1], (a, b) => a + (b - a) * t).Select(q => pts[i] + side * q.X + u * q.Y).ToList()]);
        }
        var solid = Loft(OrientAll(stations.Select(st => st[0].ToList()).ToList(), tangents[0]), false, tangents[0], tangents[^1]);
        return new(solid, solid == null ? "The swept solid could not be built." : null);
    }

    /// <summary>
    /// A sweep between two rails: the profile's left edge on the first rail,
    /// its right edge on the second, stretched across to meet both (its height
    /// kept), the rails taken at the same fractions of their lengths.
    /// </summary>
    public static SweepResult SweepRails(IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> holes, IReadOnlyList<Vec3> rail1, IReadOnlyList<Vec3> rail2, Vec3 up)
    {
        if (outer.Count < 3) return new(null, "The profile needs three points at least.");
        if (rail1.Count < 2 || rail2.Count < 2) return new(null, "Each rail needs two points at least.");
        var u = up.Normalized();
        var (minX, maxX, minY) = (outer.Min(p => p.X), outer.Max(p => p.X), outer.Min(p => p.Y));
        var pw = maxX - minX;
        if (pw < 1e-9) return new(null, "The profile has no width to stretch.");
        var fs = RailFractions(rail1, rail2);
        var stations = new List<List<IReadOnlyList<Vec3>>>();
        Vec3 first = default, last = default;
        for (var k = 0; k < fs.Count; k++)
        {
            var (p, q) = (At(rail1, fs[k]), At(rail2, fs[k]));
            var across = q - p;
            across -= u * Vec3.Dot(across, u);
            var width = across.Length;
            if (width < 1e-6) return new(null, "The rails meet: the profile would have no width there.");
            var ex = across * (1 / width);
            var dir = Vec3.Cross(u, ex).Normalized();
            if (k == 0) first = dir;
            last = dir;
            Vec3 W(Vec2 v) => p + ex * ((v.X - minX) / pw * width) + u * (v.Y - minY);
            var rings = new List<IReadOnlyList<Vec3>> { Orient(outer.Select(W).ToList(), dir, false) };
            rings.AddRange(holes.Select(h => Orient(h.Select(W).ToList(), dir, true)));
            stations.Add(rings);
        }
        var solid = Loft(stations, false, first, last);
        return new(solid, solid == null ? "The solid between the rails could not be built." : null);
    }

    /// <summary>A loft through profiles in space (each on its own plane), their points matched; capped at both ends.</summary>
    public static SweepResult LoftThrough(IReadOnlyList<IReadOnlyList<Vec2>> profiles2D, IReadOnlyList<Func<Vec2, Vec3>> world)
    {
        if (profiles2D.Count < 2) return new(null, "A loft needs two profiles at least.");
        var m = Matched(profiles2D);
        var rings = m.Select((r, i) => r.Select(world[i]).ToList()).ToList();
        Vec3 Mid(List<Vec3> r) => new(r.Average(p => p.X), r.Average(p => p.Y), r.Average(p => p.Z));
        var mids = rings.Select(Mid).ToList();
        for (var i = 0; i + 1 < mids.Count; i++)
            if ((mids[i + 1] - mids[i]).Length < 1e-6) return new(null, "Two profiles of the loft lie in the same place: draw them on different views or levels.");
        var solid = Loft(OrientAll(rings, (mids[1] - mids[0]).Normalized()), false, (mids[1] - mids[0]).Normalized(), (mids[^1] - mids[^2]).Normalized());
        return new(solid, solid == null ? "The loft could not be built." : null);
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
