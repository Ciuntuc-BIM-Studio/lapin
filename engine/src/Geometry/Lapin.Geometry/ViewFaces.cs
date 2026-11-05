using System.Runtime.CompilerServices;

namespace Lapin.Geometry;

/// <summary>
/// The faces of solids a drawing sees, and what of each is not hidden in it by a nearer face: what the vector sun
/// shadows (<see cref="SunShadows"/>) and the surface hatches of elevations and sections both stand on.
/// <para>
/// A face is seen when it looks towards the viewer; in a view that cuts (a section) only its part beyond the cut plane
/// counts, within the view's depth. What of a seen face is visible is worked out without a 2D boolean library: the face
/// and the nearer faces over it are cut into trapezoids by a sweep (every edge's ends and crossings make the slabs; within
/// a slab the edges are ordered), and each trapezoid is tested at its middle: inside the face (even-odd), behind no
/// nearer face. Trapezoids that go on with the same two edges from slab to slab are joined, so a rectangle comes out one.
/// </para>
/// <para>
/// A section's cut areas (its caps: the cut solid's section at the plane, which no face of the solid holds) hide what lies
/// behind them like any face, when they are given.
/// </para>
/// </summary>
public sealed class ViewFaces
{
    /// <summary>A face of a solid (world): its loops (outer first, then holes), its normal, the owner its solid was given, the solid and the face's index in it.</summary>
    public sealed record Face(List<Vec3[]> Loops, Vec3 Normal, int Owner, Solid? Solid, int Index);

    /// <summary>A face the view sees: its loops kept beyond the cut (world), the same in the view, their box, nearest and farthest depth, a point on its plane.</summary>
    public sealed record Seen(Face Face, List<Vec3[]> Loops, List<Vec2[]> View, (Vec2 Min, Vec2 Max) Box, double MinDepth, double MaxDepth, Vec3 P0);

    public ViewFrame Frame { get; }
    public bool Cut { get; }
    public double Depth { get; }
    /// <summary>Every face of the solids (with a normal and three corners at least).</summary>
    public IReadOnlyList<Face> Faces { get; }
    /// <summary>The faces the view sees, in the solids' order, then the caps (owner −1, seen but never a solid's).</summary>
    public IReadOnlyList<Seen> SeenFaces { get; }

    /// <param name="solids">The solids and their owners.</param>
    /// <param name="cut">Whether the view cuts: only what lies beyond its plane is seen and hides, within <paramref name="depth"/>.</param>
    /// <param name="caps">The view's cut areas, each its loops in the view's coordinates on the cut plane: they hide what is behind them.</param>
    public ViewFaces(IReadOnlyList<(Solid Solid, int Owner)> solids, ViewFrame view, bool cut = false, double depth = double.PositiveInfinity, IEnumerable<IReadOnlyList<Vec2[]>>? caps = null)
        : this(FacesOf(solids), view, cut, depth, caps) { }

    /// <summary>
    /// As the other, from faces already gathered (<see cref="FacesOf"/>: the pieces of a developed section share them);
    /// <paramref name="span"/>: the view sees only between these x (a piece's ends), a face wholly outside them is not seen
    /// (it still casts a shadow).
    /// </summary>
    public ViewFaces(IReadOnlyList<Face> faces, ViewFrame view, bool cut = false, double depth = double.PositiveInfinity, IEnumerable<IReadOnlyList<Vec2[]>>? caps = null, (double Min, double Max)? span = null)
    {
        Frame = view; Cut = cut; Depth = depth;
        var toEye = -view.Dir;
        Faces = faces;
        var seen = new List<Seen>();
        foreach (var f in faces)
        {
            if (Vec3.Dot(f.Normal, toEye) < 0.02) continue;
            if (span is { } sp && Outside(f.Loops, view, sp)) continue;
            var loops = f.Loops.Select(Beyond).Where(l => l.Length >= 3).ToList();
            if (loops.Count == 0 || loops.All(l => l.Min(view.Depth) > depth)) continue;
            seen.Add(Make(f, loops));
        }
        if (caps != null)
            foreach (var cap in caps)
            {
                var loops = cap.Where(l => l.Length >= 3).Select(l => l.Select(p => view.Origin + view.Right * p.X + view.Up * p.Y).ToArray()).ToList();
                // A cap is no solid's: it is told apart by what it holds (for the cache), rounded to a tenth of a millimetre.
                var hash = new HashCode();
                foreach (var l in cap) foreach (var p in l) { hash.Add(Math.Round(p.X, 4)); hash.Add(Math.Round(p.Y, 4)); }
                if (loops.Count > 0) seen.Add(Make(new Face(loops, toEye, -1, null, hash.ToHashCode()), loops));
            }
        SeenFaces = seen;
    }

    /// <summary>Every face of the solids with three corners or more, its loops in world space.</summary>
    public static List<Face> FacesOf(IReadOnlyList<(Solid Solid, int Owner)> solids)
    {
        var faces = new List<Face>();
        foreach (var (s, owner) in solids)
            for (var fi = 0; fi < s.Faces.Length; fi++)
            {
                var f = s.Faces[fi];
                if (f.Outer.Length < 3 || f.Normal == Vec3.Zero) continue;
                var loops = new List<Vec3[]> { f.Outer.Select(i => s.Vertices[i]).ToArray() };
                foreach (var h in f.Holes) if (h.Length >= 3) loops.Add(h.Select(i => s.Vertices[i]).ToArray());
                faces.Add(new Face(loops, f.Normal.Normalized(), owner, s, fi));
            }
        return faces;
    }

    /// <summary>Whether a face lies wholly to one side of the view's x span.</summary>
    static bool Outside(List<Vec3[]> loops, ViewFrame view, (double Min, double Max) span)
    {
        bool left = true, right = true;
        foreach (var p in loops[0])
        {
            var x = Vec3.Dot(p - view.Origin, view.Right);
            left &= x <= span.Min; right &= x >= span.Max;
            if (!left && !right) return false;
        }
        return true;
    }

    Seen Make(Face f, List<Vec3[]> loops)
    {
        var all = loops.SelectMany(l => l);
        return new Seen(f, loops, loops.Select(l => l.Select(Frame.Map).ToArray()).ToList(), Box2(all.Select(Frame.Map)), all.Min(Frame.Depth), all.Max(Frame.Depth), loops[0][0]);
    }

    /// <summary>The part of a loop beyond the view's plane, when it cuts; the loop itself otherwise.</summary>
    public Vec3[] Beyond(Vec3[] loop) => Cut ? ClipPlane(loop, Frame.Origin, Frame.Dir) : loop;

    /// <summary>The depth of the point of a plane seen at (x, y) in the view.</summary>
    public double DepthOn(Vec3 normal, Vec3 point, Vec2 at)
    {
        var q = Frame.Origin + Frame.Right * at.X + Frame.Up * at.Y;
        var den = Vec3.Dot(Frame.Dir, normal);
        return Math.Abs(den) < 1e-9 ? double.PositiveInfinity : Vec3.Dot(point - q, normal) / den;
    }

    /// <summary>The seen faces that may stand in front of <paramref name="r"/> within a zone of the view.</summary>
    public List<Seen> Over(Seen r, (Vec2 Min, Vec2 Max) zone) =>
        SeenFaces.Where(h => !ReferenceEquals(h, r) && h.MinDepth < r.MaxDepth - 1e-6
            && !(h.Box.Max.X <= zone.Min.X || h.Box.Min.X >= zone.Max.X || h.Box.Max.Y <= zone.Min.Y || h.Box.Min.Y >= zone.Max.Y)).ToList();

    /// <summary>
    /// What of a seen face no nearer face hides, as trapezoids in the view: within <paramref name="zone"/> (its box when
    /// null) and inside any of <paramref name="within"/> (each a set of loops; null: everywhere on the face).
    /// </summary>
    public List<Vec2[]> Visible(Seen r, List<List<Vec2[]>>? within = null, (Vec2 Min, Vec2 Max)? zone = null) => Visible(r, Over(r, zone ?? r.Box), within, zone ?? r.Box);

    List<Vec2[]> Visible(Seen r, List<Seen> over, List<List<Vec2[]>>? within, (Vec2 Min, Vec2 Max) zone)
    {
        var n = r.Face.Normal;
        var p0 = r.P0;
        return Cells(r.View, within, over.Select(h => h.View).ToList(),
            (k, at) => DepthOn(over[k].Face.Normal, over[k].P0, at) < DepthOn(n, p0, at) - 1e-4, zone).ToList();
    }

    /// <summary>
    /// As <see cref="Visible(Seen, List{List{Vec2[]}}?, ValueTuple{Vec2, Vec2}?)"/> for the whole face, kept in a cache between
    /// drawings: a face whose solid and whose faces in front are the same ones as last time (the same solids, as a model
    /// keeps an unchanged element's) gives the same trapezoids without the sweep.
    /// </summary>
    public List<Vec2[]> Visible(Seen r, VisibilityCache cache)
    {
        var over = Over(r, r.Box);
        if (r.Face.Solid is not { } solid) return Visible(r, over, null, r.Box);
        // What the answer depends on: the faces in front, by their solids' identities and their indices.
        var sig = 17L;
        foreach (var h in over)
            sig += Mix(h.Face.Solid is { } hs ? RuntimeHelpers.GetHashCode(hs) : 0, h.Face.Index);
        var faces = cache.FacesOf(solid);
        lock (faces)
            if (faces.TryGetValue(r.Face.Index, out var hit) && hit.Sig == sig && hit.Over == over.Count) return hit.Cells;
        var cells = Visible(r, over, null, r.Box);
        lock (faces) faces[r.Face.Index] = (sig, over.Count, cells);
        return cells;
    }

    static long Mix(int a, int b)
    {
        // A face's identity spread over 64 bits (splitmix), so a sum of them tells sets apart.
        var x = (ulong)(uint)a << 32 | (uint)b;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return (long)(x ^ (x >> 31));
    }

    /// <summary>
    /// The visible trapezoids of the seen faces <paramref name="which"/> picks, worked out in parallel, in the faces' order.
    /// </summary>
    public List<(Seen Face, List<Vec2[]> Cells)> VisibleAll(Func<Seen, bool> which, VisibilityCache? cache = null)
    {
        var picked = SeenFaces.Where(s => s.Face.Owner >= 0 && which(s)).ToList();
        var results = new List<Vec2[]>[picked.Count];
        Parallel.For(0, picked.Count, k => results[k] = cache != null ? Visible(picked[k], cache) : Visible(picked[k]));
        return picked.Select((s, k) => (s, results[k])).ToList();
    }

    /// <summary>
    /// Visible trapezoids kept between drawings of one view, while it looks the same way from the same place: per solid
    /// (held weakly, so a solid the model dropped takes its entries with it) and face, with what was in front of it.
    /// </summary>
    public sealed class VisibilityCache
    {
        readonly ConditionalWeakTable<Solid, Dictionary<int, (long Sig, int Over, List<Vec2[]> Cells)>> _faces = new();
        (ViewFrame Frame, bool Cut, double Depth)? _key;

        /// <summary>The cache for a view's frame, cut and depth: emptied when they are not the ones it was kept for.</summary>
        public VisibilityCache For(ViewFaces faces)
        {
            lock (this)
            {
                var key = (faces.Frame, faces.Cut, faces.Depth);
                if (_key != key) { _faces.Clear(); _key = key; }
            }
            return this;
        }

        internal Dictionary<int, (long Sig, int Over, List<Vec2[]> Cells)> FacesOf(Solid s) => _faces.GetValue(s, _ => []);
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

    public static (Vec2 Min, Vec2 Max) Box2(IEnumerable<Vec2> pts)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var p in pts) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
        return (new Vec2(x0, y0), new Vec2(x1, y1));
    }

    readonly record struct Seg(Vec2 A, Vec2 B, int Set, int Index, int Id);

    /// <summary>
    /// The trapezoids inside the face (even-odd over its loops), inside a region of <paramref name="within"/> (any; null:
    /// everywhere) and not behind a face in front (any whose outline holds the point and is nearer there).
    /// </summary>
    internal static IEnumerable<Vec2[]> Cells(List<Vec2[]> face, List<List<Vec2[]>>? within, List<List<Vec2[]>> over, Func<int, Vec2, bool> nearer, (Vec2 Min, Vec2 Max) zone)
    {
        // Set 0 the face; 1 the regions it is kept within; 2 the faces in front.
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
        if (within != null) for (var k = 0; k < within.Count; k++) Add(within[k], 1, k);
        for (var k = 0; k < over.Count; k++) Add(over[k], 2, k);
        // Slabs: every end, and every crossing, within the zone's height.
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
        var withinOn = new bool[within?.Count ?? 0];
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
            // Edges at one x (a shared edge, a region's side on the face's) kept in one order from slab to slab, so trapezoids join.
            if (sorter.Length < n) sorter = new (double X, int Id)[Math.Max(n, sorter.Length * 2)];
            for (var i = 0; i < n; i++) sorter[i] = (XAt(segs[active[i]], ym), active[i]);
            Array.Sort(sorter, 0, n, ByX);
            for (var i = 0; i < n; i++) { order[i] = sorter[i].Id; keys[i] = sorter[i].X; }
            var inFace = false;
            var withinCount = 0;
            still.Clear();
            for (var i = 0; i < n; i++)
            {
                var s = segs[order[i]];
                switch (s.Set)
                {
                    case 0: inFace = !inFace; break;
                    case 1: withinOn[s.Index] = !withinOn[s.Index]; withinCount += withinOn[s.Index] ? 1 : -1; break;
                    default:
                        overOn[s.Index] = !overOn[s.Index];
                        if (overOn[s.Index]) overList.Add(s.Index); else overList.Remove(s.Index);
                        break;
                }
                if (i + 1 >= n || keys[i + 1] - keys[i] < 1e-9 || !inFace || within != null && withinCount == 0) continue;
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
