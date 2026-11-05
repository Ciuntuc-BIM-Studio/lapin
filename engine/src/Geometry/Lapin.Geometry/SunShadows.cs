namespace Lapin.Geometry;

/// <summary>
/// Vector sun shadows in a drawing (docs/plan-visualisation.md, vector shadows; as Archicad draws them in elevations and
/// sections): the sun's rays are parallel, so the shadow a face casts on another's plane is its projection along them.
/// For each face the drawing sees and the sun lights, the lit faces between it and the sun are projected onto its plane;
/// what of that lies on the face, is not hidden in the view by a nearer face, is shadow. A face the sun does not reach
/// (turned away from it) is in shade whole. The result is filled areas in the view's coordinates, each owned by the
/// element whose face it lies on.
/// <para>
/// No 2D boolean library: the face, the shadows and the faces in front are cut into trapezoids by a sweep (every edge's
/// ends and crossings make the slabs; within a slab the edges are ordered), and each trapezoid is tested at its middle:
/// inside the face (even-odd), inside any shadow, behind no nearer face. Trapezoids that go on with the same two edges
/// from slab to slab are joined, so a rectangle's shadow comes out one.
/// </para>
/// </summary>
public static class SunShadows
{
    /// <summary>A shadow area in the view: its outline (view metres) and the element it lies on.</summary>
    public sealed record Area(Vec2[] Outline, int Owner);

    /// <summary>
    /// What to cast and receive: the solids with their owners; the view; the direction towards the sun (unit, world); whether
    /// the view cuts (only what lies beyond its plane receives and hides, within <paramref name="depth"/>); whether the casters
    /// are cut too (a section: what the cut took away casts nothing). Stops at <paramref name="maxAreas"/>.
    /// </summary>
    public static List<Area> Draw(IReadOnlyList<(Solid Solid, int Owner)> solids, ViewFrame view, Vec3 sun, bool cut = false, double depth = double.PositiveInfinity, bool cutCasters = true, int maxAreas = 200_000)
    {
        sun = sun.Normalized();
        var toEye = -view.Dir;
        var faces = new List<F>();
        foreach (var (s, owner) in solids)
            foreach (var f in s.Faces)
            {
                if (f.Outer.Length < 3 || f.Normal == Vec3.Zero) continue;
                var loops = new List<Vec3[]> { f.Outer.Select(i => s.Vertices[i]).ToArray() };
                foreach (var h in f.Holes) if (h.Length >= 3) loops.Add(h.Select(i => s.Vertices[i]).ToArray());
                faces.Add(new F(loops, f.Normal.Normalized(), owner));
            }
        // The part beyond the view's plane (a section's, a plan's): what is drawn, and what hides.
        Vec3[] Beyond(Vec3[] loop) => cut ? ClipPlane(loop, view.Origin, view.Dir) : loop;
        var seen = new List<(F Face, List<Vec3[]> Loops)>();
        foreach (var f in faces)
        {
            if (Vec3.Dot(f.Normal, toEye) < 0.02) continue;
            var loops = f.Loops.Select(Beyond).Where(l => l.Length >= 3).ToList();
            if (loops.Count == 0 || loops.All(l => l.Min(view.Depth) > depth)) continue;
            seen.Add((f, loops));
        }
        var casters = new List<(F Face, List<Vec3[]> Loops, Vec3 Min, Vec3 Max)>();
        foreach (var f in faces)
        {
            // A face nearly along the rays casts a sliver its neighbours' shadows cover anyway.
            if (Vec3.Dot(f.Normal, sun) <= 0.02) continue;
            var loops = cutCasters ? f.Loops.Select(Beyond).Where(l => l.Length >= 3).ToList() : f.Loops;
            if (loops.Count == 0) continue;
            var all = loops.SelectMany(l => l).ToList();
            casters.Add((f, loops, new Vec3(all.Min(p => p.X), all.Min(p => p.Y), all.Min(p => p.Z)), new Vec3(all.Max(p => p.X), all.Max(p => p.Y), all.Max(p => p.Z))));
        }
        // The faces that may hide another, in the view: their outline and the plane their depth is read from.
        var hiders = seen.Select(x => (x.Face, View: x.Loops.Select(l => l.Select(view.Map).ToArray()).ToList(), Box: Box2(x.Loops.SelectMany(l => l).Select(view.Map)),
            MinDepth: x.Loops.SelectMany(l => l).Min(view.Depth), P0: x.Loops[0][0])).ToList();
        // Each face seen is worked out on its own, in parallel; the areas kept in the faces' order.
        var results = new List<Vec2[]>[seen.Count];
        Parallel.For(0, seen.Count, k =>
        {
            var (r, rLoops) = seen[k];
            results[k] = Receive(r, rLoops);
        });
        var o = new List<Area>();
        for (var k = 0; k < seen.Count && o.Count < maxAreas; k++)
            foreach (var t in results[k]) { o.Add(new Area(t, seen[k].Face.Owner)); if (o.Count >= maxAreas) break; }
        return o;

        List<Vec2[]> Receive(F r, List<Vec3[]> rLoops)
        {
            var p0 = rLoops[0][0];
            var n = r.Normal;
            var rView = rLoops.Select(l => l.Select(view.Map).ToArray()).ToList();
            var rBox = Box2(rView.SelectMany(l => l));
            var rMaxDepth = rLoops.SelectMany(l => l).Max(view.Depth);
            List<List<Vec2[]>> shadows = [];
            var shade = Vec3.Dot(n, sun) <= 1e-6;
            if (!shade)
            {
                var dn = Vec3.Dot(sun, n);
                foreach (var (c, cLoops, cMin, cMax) in casters)
                {
                    if (ReferenceEquals(c, r)) continue;
                    // Only what stands in front of the face's plane, on the sun's side, casts on it.
                    if (MaxSigned(cMin, cMax, p0, n) <= 1e-6) continue;
                    // Its box's corners carried onto the plane first: far from the face, nothing more to do.
                    if (!Near(cMin, cMax, p0, n, dn, rBox)) continue;
                    var projected = new List<Vec2[]>();
                    foreach (var loop in cLoops)
                    {
                        var front = ClipPlane(loop, p0, n);
                        if (front.Length < 3) continue;
                        projected.Add(front.Select(v => view.Map(v - sun * (Vec3.Dot(v - p0, n) / dn))).ToArray());
                    }
                    if (projected.Count == 0) continue;
                    var b = Box2(projected.SelectMany(l => l));
                    if (b.Max.X <= rBox.Min.X || b.Min.X >= rBox.Max.X || b.Max.Y <= rBox.Min.Y || b.Min.Y >= rBox.Max.Y) continue;
                    if (Math.Abs(Area2(projected[0])) < 1e-10) continue;
                    shadows.Add(projected);
                }
                if (shadows.Count == 0) return [];
            }
            // The nearer faces over this one in the view, where it may be in shadow.
            var zone = rBox;
            if (!shade)
            {
                var sb = Box2(shadows.SelectMany(x => x).SelectMany(l => l));
                zone = (new Vec2(Math.Max(rBox.Min.X, sb.Min.X), Math.Max(rBox.Min.Y, sb.Min.Y)), new Vec2(Math.Min(rBox.Max.X, sb.Max.X), Math.Min(rBox.Max.Y, sb.Max.Y)));
                if (zone.Min.X >= zone.Max.X || zone.Min.Y >= zone.Max.Y) return [];
            }
            var over = hiders.Where(h => !ReferenceEquals(h.Face, r) && h.MinDepth < rMaxDepth - 1e-6
                && !(h.Box.Max.X <= zone.Min.X || h.Box.Min.X >= zone.Max.X || h.Box.Max.Y <= zone.Min.Y || h.Box.Min.Y >= zone.Max.Y)).ToList();
            double DepthOn(Vec3 normal, Vec3 point, Vec2 at)
            {
                // The depth of the point of a plane seen at (x, y) in the view.
                var q = view.Origin + view.Right * at.X + view.Up * at.Y;
                var den = Vec3.Dot(view.Dir, normal);
                return Math.Abs(den) < 1e-9 ? double.PositiveInfinity : Vec3.Dot(point - q, normal) / den;
            }
            return Cells(rView, shade ? null : shadows, over.Select(h => h.View).ToList(),
                (k, at) => DepthOn(over[k].Face.Normal, over[k].P0, at) < DepthOn(n, p0, at) - 1e-4, zone).ToList();
        }

        // Whether a caster's box, carried along the rays onto a plane, may reach a face's box in the view.
        bool Near(Vec3 min, Vec3 max, Vec3 p0, Vec3 n, double dn, (Vec2 Min, Vec2 Max) box)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            for (var i = 0; i < 8; i++)
            {
                var c = new Vec3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
                var d = Vec3.Dot(c - p0, n);
                // A corner behind the plane stands where it is (its part is clipped away); one in front, carried onto it.
                var q = view.Map(d > 0 ? c - sun * (d / dn) : c);
                x0 = Math.Min(x0, q.X); y0 = Math.Min(y0, q.Y); x1 = Math.Max(x1, q.X); y1 = Math.Max(y1, q.Y);
            }
            return !(x1 <= box.Min.X || x0 >= box.Max.X || y1 <= box.Min.Y || y0 >= box.Max.Y);
        }
    }

    sealed record F(List<Vec3[]> Loops, Vec3 Normal, int Owner);

    static double MaxSigned(Vec3 min, Vec3 max, Vec3 p0, Vec3 n)
    {
        var best = double.NegativeInfinity;
        for (var i = 0; i < 8; i++)
        {
            var c = new Vec3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
            best = Math.Max(best, Vec3.Dot(c - p0, n));
        }
        return best;
    }

    /// <summary>A loop kept where it lies on the positive side of a plane (Sutherland–Hodgman).</summary>
    public static Vec3[] ClipPlane(Vec3[] loop, Vec3 p0, Vec3 n)
    {
        var o = new List<Vec3>(loop.Length + 2);
        for (var i = 0; i < loop.Length; i++)
        {
            Vec3 a = loop[i], b = loop[(i + 1) % loop.Length];
            double da = Vec3.Dot(a - p0, n), db = Vec3.Dot(b - p0, n);
            if (da >= 0) o.Add(a);
            if (da >= 0 != db >= 0) o.Add(a + (b - a) * (da / (da - db)));
        }
        return [.. o];
    }

    static (Vec2 Min, Vec2 Max) Box2(IEnumerable<Vec2> pts)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var p in pts) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
        return (new Vec2(x0, y0), new Vec2(x1, y1));
    }

    static double Area2(Vec2[] l)
    {
        var a = 0.0;
        for (var i = 0; i < l.Length; i++) { var p = l[i]; var q = l[(i + 1) % l.Length]; a += p.X * q.Y - q.X * p.Y; }
        return a / 2;
    }

    readonly record struct Seg(Vec2 A, Vec2 B, int Set, int Index, int Id);

    /// <summary>
    /// The trapezoids inside the face (even-odd over its loops), inside a shadow (any; null: everywhere) and not behind a
    /// face in front (any whose outline holds the point and is nearer there).
    /// </summary>
    static IEnumerable<Vec2[]> Cells(List<Vec2[]> face, List<List<Vec2[]>>? shadows, List<List<Vec2[]>> over, Func<int, Vec2, bool> nearer, (Vec2 Min, Vec2 Max) zone)
    {
        // Set 0 the face; 1.. the shadows; then the faces in front.
        var segs = new List<Seg>();
        void Add(List<Vec2[]> loops, int set, int index)
        {
            foreach (var l in loops)
                for (var i = 0; i < l.Length; i++)
                {
                    Vec2 a = l[i], b = l[(i + 1) % l.Length];
                    if (Math.Abs(a.Y - b.Y) < 1e-12) continue; // level edges change nothing across a slab
                    segs.Add(new Seg(a, b, set, index, segs.Count));
                }
        }
        Add(face, 0, 0);
        if (shadows != null) for (var k = 0; k < shadows.Count; k++) Add(shadows[k], 1, k);
        for (var k = 0; k < over.Count; k++) Add(over[k], 2, k);
        // Slabs: every end, and every crossing, within the zone's height (the face where it may be in shadow).
        double lo = zone.Min.Y - 1e-9, hi = zone.Max.Y + 1e-9;
        var ys = new List<double> { zone.Min.Y, zone.Max.Y };
        foreach (var s in segs) { ys.Add(s.A.Y); ys.Add(s.B.Y); }
        // Crossings: pairs whose boxes meet, found by a sweep along y.
        var byY = segs.OrderBy(s => Math.Min(s.A.Y, s.B.Y)).ToList();
        var live = new List<Seg>();
        foreach (var s in byY)
        {
            var sMin = Math.Min(s.A.Y, s.B.Y);
            live.RemoveAll(t => Math.Max(t.A.Y, t.B.Y) < sMin);
            double sx0 = Math.Min(s.A.X, s.B.X), sx1 = Math.Max(s.A.X, s.B.X);
            foreach (var t in live)
                if (Math.Max(t.A.X, t.B.X) >= sx0 && Math.Min(t.A.X, t.B.X) <= sx1 && Cross(s, t) is { } y) ys.Add(y);
            live.Add(s);
        }
        ys = ys.Where(y => y >= lo && y <= hi).Order().ToList();
        // The slabs from the bottom up, the edges across each kept as the sweep goes (added at their bottom, taken out past their top).
        var open = new Dictionary<(int, int), (Vec2 BL, Vec2 BR, Vec2 TL, Vec2 TR)>();
        var still = new Dictionary<(int, int), (Vec2 BL, Vec2 BR, Vec2 TL, Vec2 TR)>();
        var done = new List<Vec2[]>();
        var shadowOn = new bool[shadows?.Count ?? 0];
        var overOn = new bool[over.Count];
        var overList = new List<int>();
        var active = new List<int>();
        var next = 0;
        var keys = new double[segs.Count];
        var order = new int[segs.Count];
        var sorter = new (double X, int Id)[Math.Max(16, segs.Count)];
        for (var k = 0; k + 1 < ys.Count; k++)
        {
            double y0 = ys[k], y1 = ys[k + 1];
            if (y1 - y0 < 1e-9) continue;
            while (next < byY.Count && Math.Min(byY[next].A.Y, byY[next].B.Y) <= y0 + 1e-12) active.Add(byY[next++].Id);
            active.RemoveAll(id => Math.Max(segs[id].A.Y, segs[id].B.Y) < y1 - 1e-12);
            var ym = (y0 + y1) / 2;
            var n = active.Count;
            // Edges at one x (a shared edge, a shadow's side on the face's) kept in one order from slab to slab, so trapezoids join.
            if (sorter.Length < n) sorter = new (double X, int Id)[Math.Max(n, sorter.Length * 2)];
            for (var i = 0; i < n; i++) sorter[i] = (XAt(segs[active[i]], ym), active[i]);
            Array.Sort(sorter, 0, n, ByX);
            for (var i = 0; i < n; i++) { order[i] = sorter[i].Id; keys[i] = sorter[i].X; }
            var inFace = false;
            var shadowCount = 0;
            still.Clear();
            for (var i = 0; i < n; i++)
            {
                var s = segs[order[i]];
                switch (s.Set)
                {
                    case 0: inFace = !inFace; break;
                    case 1: shadowOn[s.Index] = !shadowOn[s.Index]; shadowCount += shadowOn[s.Index] ? 1 : -1; break;
                    default:
                        overOn[s.Index] = !overOn[s.Index];
                        if (overOn[s.Index]) overList.Add(s.Index); else overList.Remove(s.Index);
                        break;
                }
                if (i + 1 >= n || keys[i + 1] - keys[i] < 1e-9 || !inFace || shadows != null && shadowCount == 0) continue;
                var mid = new Vec2((keys[i] + keys[i + 1]) / 2, ym);
                var hidden = false;
                foreach (var h in overList) if (nearer(h, mid)) { hidden = true; break; }
                if (hidden) continue;
                var l = s; var r = segs[order[i + 1]];
                var key = (l.Id, r.Id);
                // The same two edges as the slab below: the trapezoid goes on up.
                still[key] = open.TryGetValue(key, out var was)
                    ? (was.BL, was.BR, new Vec2(XAt(l, y1), y1), new Vec2(XAt(r, y1), y1))
                    : (new Vec2(XAt(l, y0), y0), new Vec2(XAt(r, y0), y0), new Vec2(XAt(l, y1), y1), new Vec2(XAt(r, y1), y1));
            }
            foreach (var (key, t) in open) if (!still.ContainsKey(key)) done.Add([t.BL, t.BR, t.TR, t.TL]);
            (open, still) = (still, open);
        }
        foreach (var t in open.Values) done.Add([t.BL, t.BR, t.TR, t.TL]);
        return done;
    }

    static readonly Comparer<(double X, int Id)> ByX = Comparer<(double X, int Id)>.Create((a, b) => Math.Abs(a.X - b.X) < 1e-9 ? a.Id.CompareTo(b.Id) : a.X.CompareTo(b.X));

    static double XAt(Seg s, double y) => Math.Abs(s.B.Y - s.A.Y) < 1e-15 ? s.A.X : s.A.X + (s.B.X - s.A.X) * (y - s.A.Y) / (s.B.Y - s.A.Y);

    /// <summary>The height where two segments cross (strictly inside both); null when they do not.</summary>
    static double? Cross(Seg p, Seg q)
    {
        var r = p.B - p.A; var s = q.B - q.A;
        var den = r.X * s.Y - r.Y * s.X;
        if (Math.Abs(den) < 1e-15) return null;
        var d = q.A - p.A;
        var t = (d.X * s.Y - d.Y * s.X) / den;
        var u = (d.X * r.Y - d.Y * r.X) / den;
        return t > 1e-9 && t < 1 - 1e-9 && u > 1e-9 && u < 1 - 1e-9 ? p.A.Y + r.Y * t : null;
    }
}
