using Lapin.Geometry;

namespace Lapin.Drawing.Hatch;

/// <summary>A region of free space: its outline and the islands inside it (a column standing in a room), each a closed loop.</summary>
public sealed record FreeRegion(IReadOnlyList<Vec2> Outer, IReadOnlyList<IReadOnlyList<Vec2>> Islands)
{
    /// <summary>The area inside the outline, less the islands'.</summary>
    public double Area => Math.Abs(Polygon2.SignedArea(Outer)) - Islands.Sum(i => Math.Abs(Polygon2.SignedArea(i)));
    public double GrossArea => Math.Abs(Polygon2.SignedArea(Outer));
}

/// <summary>
/// The free space between solid areas in plan (a storey's walls and columns
/// cut at a height), and the region of it around a point: what a room placed
/// by a click fills. The areas' outlines are split where they cross or touch,
/// the pieces with material on exactly one side kept, each turned so the free
/// space lies on its left, and chained into loops: counter-clockwise ones
/// enclose free space, clockwise ones go round material. The region around a
/// point is the smallest counter-clockwise loop holding it, with the
/// clockwise loops whose free side belongs to that loop as its islands.
/// Built once for a set of areas; each point is then answered from the loops.
/// </summary>
public sealed class FreeSpace
{
    /// <summary>Points closer than this are one (metres).</summary>
    const double Snap = 1e-6;
    /// <summary>How far to either side of a piece its sides are sampled (metres): far below any wall's thickness.</summary>
    const double Side = 2e-5;

    sealed record Area(Vec2[][] Rings, double MinX, double MinY, double MaxX, double MaxY);
    sealed record Loop(Vec2[] Pts, double Signed, double MinX, double MinY, double MaxX, double MaxY);

    readonly List<Area> _areas = [];
    readonly List<Loop> _loops = [];

    FreeSpace() { }

    /// <summary>The loops found: counter-clockwise round free space, clockwise round material.</summary>
    public IEnumerable<IReadOnlyList<Vec2>> Loops => _loops.Select(l => (IReadOnlyList<Vec2>)l.Pts);

    /// <summary>The free space around the given areas, each a list of rings filled even-odd (an area's outline and its holes).</summary>
    public static FreeSpace Of(IEnumerable<IReadOnlyList<IReadOnlyList<Vec2>>> areas)
    {
        var fs = new FreeSpace();
        foreach (var a in areas)
        {
            var rings = a.Where(r => r.Count >= 3).Select(r => r.ToArray()).ToArray();
            if (rings.Length == 0) continue;
            var all = rings.SelectMany(r => r).ToList();
            fs._areas.Add(new Area(rings, all.Min(p => p.X), all.Min(p => p.Y), all.Max(p => p.X), all.Max(p => p.Y)));
        }
        fs.Build();
        return fs;
    }

    /// <summary>Whether a point lies in one of the areas.</summary>
    public bool InMaterial(Vec2 p)
    {
        foreach (var a in _areas)
            if (p.X >= a.MinX && p.X <= a.MaxX && p.Y >= a.MinY && p.Y <= a.MaxY && Polygon2.Inside(a.Rings, p)) return true;
        return false;
    }

    /// <summary>The free region around <paramref name="p"/>; null when it lies in an area or nothing closes round it.</summary>
    public FreeRegion? Around(Vec2 p)
    {
        if (InMaterial(p)) return null;
        var outer = Smallest(p);
        if (outer == null) return null;
        var islands = new List<IReadOnlyList<Vec2>>();
        foreach (var l in _loops)
        {
            if (l.Signed >= 0 || l.MinX < outer.MinX - Snap || l.MaxX > outer.MaxX + Snap || l.MinY < outer.MinY - Snap || l.MaxY > outer.MaxY + Snap) continue;
            // Its free side: just left of its first piece. It is one of this region's islands when that point is in the region.
            var (a, b) = (l.Pts[0], l.Pts[1]);
            var d = (b - a).Normalized();
            var probe = (a + b) * 0.5 + d.Perp * (Side * 4);
            if (ReferenceEquals(Smallest(probe), outer)) islands.Add(l.Pts);
        }
        return new FreeRegion(outer.Pts, islands);
    }

    /// <summary>The smallest counter-clockwise loop holding the point.</summary>
    Loop? Smallest(Vec2 p)
    {
        Loop? best = null;
        foreach (var l in _loops)
        {
            if (l.Signed <= 0 || p.X < l.MinX || p.X > l.MaxX || p.Y < l.MinY || p.Y > l.MaxY) continue;
            if (!Polygon2.Contains(l.Pts, p)) continue;
            if (best == null || l.Signed < best.Signed) best = l;
        }
        return best;
    }

    // ─── Building ────────────────────────────────────────────────────────

    readonly Dictionary<(long, long), int> _index = [];
    readonly List<Vec2> _vertices = [];

    int Vertex(Vec2 p)
    {
        var key = ((long)Math.Round(p.X / Snap), (long)Math.Round(p.Y / Snap));
        if (_index.TryGetValue(key, out var i)) return i;
        _vertices.Add(p);
        return _index[key] = _vertices.Count - 1;
    }

    void Build()
    {
        // Every ring's sides, as segments with the parameters where they are split.
        var segs = new List<(Vec2 A, Vec2 B, List<double> T)>();
        foreach (var a in _areas)
            foreach (var r in a.Rings)
                for (var i = 0; i < r.Length; i++)
                {
                    var (p, q) = (r[i], r[(i + 1) % r.Length]);
                    if ((q - p).Length > Snap) segs.Add((p, q, [0, 1]));
                }
        // Split where two sides cross, where an end of one lies on the other, and where they overlap.
        var order = Enumerable.Range(0, segs.Count).OrderBy(i => Math.Min(segs[i].A.X, segs[i].B.X)).ToArray();
        for (var oi = 0; oi < order.Length; oi++)
        {
            var i = order[oi];
            var (a, b, ti) = segs[i];
            var maxX = Math.Max(a.X, b.X) + Snap;
            var (minY, maxY) = (Math.Min(a.Y, b.Y) - Snap, Math.Max(a.Y, b.Y) + Snap);
            for (var oj = oi + 1; oj < order.Length; oj++)
            {
                var j = order[oj];
                var (c, d, tj) = segs[j];
                if (Math.Min(c.X, d.X) > maxX) break;
                if (Math.Max(c.Y, d.Y) < minY || Math.Min(c.Y, d.Y) > maxY) continue;
                Split(a, b, ti, c, d, tj);
            }
        }
        // The pieces, once each.
        var edges = new HashSet<(int, int)>();
        foreach (var (a, b, ts) in segs)
        {
            ts.Sort();
            var prev = Vertex(a);
            foreach (var t in ts.Skip(1))
            {
                var v = Vertex(a + (b - a) * t);
                if (v != prev) edges.Add(prev < v ? (prev, v) : (v, prev));
                prev = v;
            }
        }
        // The pieces between free space and material, turned to have the free space on their left.
        var outgoing = new Dictionary<int, List<int>>();
        foreach (var (u, v) in edges)
        {
            var (pu, pv) = (_vertices[u], _vertices[v]);
            var dir = (pv - pu).Normalized();
            var mid = (pu + pv) * 0.5;
            var left = InMaterial(mid + dir.Perp * Side);
            var right = InMaterial(mid - dir.Perp * Side);
            if (left == right) continue;
            var (from, to) = left ? (v, u) : (u, v);
            (outgoing.TryGetValue(from, out var l) ? l : outgoing[from] = []).Add(to);
        }
        // Chained: at each vertex the piece that turns most tightly to the left of the way back, so the free space stays on the left.
        var used = new HashSet<(int, int)>();
        foreach (var (start, tos) in outgoing)
            foreach (var first in tos)
            {
                if (used.Contains((start, first))) continue;
                var pts = new List<Vec2> { _vertices[start] };
                var (from, at) = (start, first);
                used.Add((from, at));
                var closed = false;
                for (var guard = 0; guard < 1_000_000; guard++)
                {
                    if (at == start) { closed = true; break; }
                    pts.Add(_vertices[at]);
                    if (!outgoing.TryGetValue(at, out var next)) break;
                    var back = _vertices[from] - _vertices[at];
                    var thetaBack = Math.Atan2(back.Y, back.X);
                    var best = -1; var bestTurn = double.MaxValue;
                    foreach (var n in next)
                    {
                        if (used.Contains((at, n))) continue;
                        var w = _vertices[n] - _vertices[at];
                        var turn = thetaBack - Math.Atan2(w.Y, w.X);
                        while (turn <= 1e-12) turn += 2 * Math.PI;
                        while (turn > 2 * Math.PI + 1e-12) turn -= 2 * Math.PI;
                        if (turn < bestTurn) { bestTurn = turn; best = n; }
                    }
                    if (best < 0) break;
                    used.Add((at, best));
                    (from, at) = (at, best);
                }
                if (!closed || pts.Count < 3) continue;
                var arr = Clean(pts);
                if (arr.Length < 3) continue;
                var signed = Polygon2.SignedArea(arr);
                if (Math.Abs(signed) < 1e-10) continue;
                _loops.Add(new Loop(arr, signed, arr.Min(p => p.X), arr.Min(p => p.Y), arr.Max(p => p.X), arr.Max(p => p.Y)));
            }
    }

    /// <summary>The loop without the points that lie on a straight run between their neighbours.</summary>
    static Vec2[] Clean(List<Vec2> pts)
    {
        var o = new List<Vec2>(pts);
        for (var changed = true; changed && o.Count > 3;)
        {
            changed = false;
            for (var i = 0; i < o.Count && o.Count > 3; i++)
            {
                var (a, b, c) = (o[(i + o.Count - 1) % o.Count], o[i], o[(i + 1) % o.Count]);
                var ac = c - a;
                if (ac.Length > 1e-12 && Math.Abs(Vec2.Cross(ac, b - a)) / ac.Length < Snap && Vec2.Dot(b - a, c - b) > 0) { o.RemoveAt(i); i--; changed = true; }
            }
        }
        return [.. o];
    }

    /// <summary>Notes on both sides where they cross, touch or overlap.</summary>
    static void Split(Vec2 a, Vec2 b, List<double> ti, Vec2 c, Vec2 d, List<double> tj)
    {
        var (ab, cd) = (b - a, d - c);
        double lab = ab.Length, lcd = cd.Length;
        // An end of one lying on the other (a T, or the ends of an overlap).
        void OnSide(Vec2 p, Vec2 s, Vec2 dir, double len, List<double> ts)
        {
            var t = Vec2.Dot(p - s, dir) / (len * len);
            if (t <= Snap / len || t >= 1 - Snap / len) return;
            if (Math.Abs(Vec2.Cross(dir, p - s)) / len < Snap) ts.Add(t);
        }
        OnSide(c, a, ab, lab, ti); OnSide(d, a, ab, lab, ti);
        OnSide(a, c, cd, lcd, tj); OnSide(b, c, cd, lcd, tj);
        // A crossing of the two.
        var den = Vec2.Cross(ab, cd);
        if (Math.Abs(den) < 1e-15 * lab * lcd) return;
        var w = c - a;
        var t1 = Vec2.Cross(w, cd) / den;
        var t2 = Vec2.Cross(w, ab) / den;
        if (t1 > Snap / lab && t1 < 1 - Snap / lab && t2 > Snap / lcd && t2 < 1 - Snap / lcd) { ti.Add(t1); tj.Add(t2); }
    }
}
