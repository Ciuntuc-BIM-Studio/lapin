using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform.Analysis;

/// <summary>A node of the analytical model: metres, its storey (by level), its restraints (null: none known) and where they were read.</summary>
public sealed class ANode(int index, Vec3 at)
{
    public int Index { get; } = index;
    public Vec3 At { get; set; } = at;
    public string Id { get; set; } = "";
    public string? StoreyId { get; set; }
    public bool[]? Restraints { get; set; }
    public string? RestraintSource { get; set; }
    public string? RestraintKind { get; set; }
}

/// <summary>One bar of the analytical model: a piece of its element's axis between two nodes, its local axes' rotation.</summary>
public sealed record AFrame(string Id, AnalyticalFrame Source, int Piece, int Pieces, ANode I, ANode J, double RotationDeg, double OffsetZ);

/// <summary>One shell of the analytical model: a piece of its element's surface, its nodes in order round it.</summary>
public sealed record AShell(string Id, AnalyticalShell Source, int Piece, int Pieces, IReadOnlyList<ANode> Nodes);

/// <summary>
/// The analytical model derived from the physical one (docs/jg-structures.md, The analytical model): the modules' members
/// (<see cref="IAnalyticalElements"/>) laid on shared nodes. Horizontal bars move into the mid-plane of the slab they carry,
/// column and wall ends to the beam or slab they meet, beam ends onto column axes, wall ends onto the walls they meet in a T,
/// slab corners onto the nodes of their supports; points closer than <see cref="Tolerance"/> are one node; a convex slab is
/// split along the beams and wall tops that cross it, a wall at the walls and columns that meet it; every node on a shell's
/// edge or a bar's span becomes one of its corners or splits it. Supports hold the lowest column and wall ends on a footing,
/// or a steel column base as its connection says. Ids come from the elements' GlobalIds (a piece's with #n).
/// </summary>
public sealed class AnalyticalModel
{
    /// <summary>Points closer than this (metres) are one node.</summary>
    public const double Tolerance = 0.01;
    /// <summary>How far below a column's or a wall's end (metres) the beam or slab it meets may lie.</summary>
    public const double EndReach = 1.0;
    /// <summary>How far a slab's corner (metres) moves onto a support's node.</summary>
    public const double CornerReach = 0.5;

    public List<ANode> Nodes { get; } = [];
    public List<AFrame> Frames { get; } = [];
    public List<AShell> Shells { get; } = [];
    public List<LeftOut> LeftOut { get; } = [];
    public List<string> Notes { get; } = [];
    public List<Entity> Storeys { get; } = [];
    /// <summary>The nodes at a column's or wall's foot that nothing holds and nothing else meets: they need a support.</summary>
    public List<ANode> FreeBases { get; } = [];

    sealed class Bar(AnalyticalFrame src)
    {
        public AnalyticalFrame Src = src;
        public List<Vec3> Pts = [.. src.Points];
        public double OffsetZ;
        public bool Vertical => Pts.Count == 2 && (Pts[1].Xy - Pts[0].Xy).Length < 1e-6 && Math.Abs(Pts[1].Z - Pts[0].Z) > 1e-6;
        public bool Horizontal => Pts.All(p => Math.Abs(p.Z - Pts[0].Z) < 1e-6);
        public double Z => Pts[0].Z;
    }

    sealed class Plate(AnalyticalShell src)
    {
        public AnalyticalShell Src = src;
        public bool IsWall => Src.Line != null;
        public List<Vec2> Outline = src.Outline?.ToList() ?? [];
        public double Z = src.Z;
        public List<Vec2> Line = src.Line?.ToList() ?? [];
        public double Z0 = src.Z0, Z1 = src.Z1;
    }

    public static AnalyticalModel Build(IModelView m, IServices services, AnalyticalOptions options)
    {
        var all = new AnalyticalContribution();
        foreach (var source in services.All<IAnalyticalElements>()) source.Contribute(m, all);
        var model = new AnalyticalModel();
        model.Storeys.AddRange(Spatial.Storeys(m));
        model.LeftOut.AddRange(all.LeftOut);

        var bars = new List<Bar>();
        foreach (var f in all.Frames)
        {
            if (!options.On(f.StoreyId)) continue;
            if (!options.Takes(f.Category)) { model.LeftOut.Add(new(f.SourceId, f.Name, f.IfcClass, $"{f.Category} not chosen")); continue; }
            if (f.Points.Count < 2) { model.LeftOut.Add(new(f.SourceId, f.Name, f.IfcClass, "no axis")); continue; }
            bars.Add(new Bar(f));
        }
        var plates = new List<Plate>();
        foreach (var s in all.Shells)
        {
            if (!options.On(s.StoreyId)) continue;
            var kind = s.Kind == "wall" ? AnalyticalKinds.Walls : AnalyticalKinds.Slabs;
            if (!options.Takes(kind)) { model.LeftOut.Add(new(s.SourceId, s.Name, s.IfcClass, $"{kind} not chosen")); continue; }
            if (s.Kind == "wall" && !s.LoadBearing && !options.AllWalls) { model.LeftOut.Add(new(s.SourceId, s.Name, s.IfcClass, "not load-bearing (Pset_WallCommon.LoadBearing)")); continue; }
            plates.Add(new Plate(s));
        }
        var supports = options.Takes(AnalyticalKinds.Supports) ? all.Supports : [];

        JoinEnds(bars);
        SnapLevels(bars, plates);
        SnapPlan(bars, plates);
        model.Assemble(bars, plates, supports, m);
        return model;
    }

    // ------------------------------------------------------------ snapping

    static double DistToPolyline(Vec2 p, IReadOnlyList<Vec2> pts)
    {
        var best = double.PositiveInfinity;
        for (var i = 0; i + 1 < pts.Count; i++) best = Math.Min(best, DistToSegment(p, pts[i], pts[i + 1]));
        return pts.Count == 1 ? (p - pts[0]).Length : best;
    }

    static double DistToSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        var l2 = Vec2.Dot(ab, ab);
        var t = l2 > 0 ? Math.Clamp(Vec2.Dot(p - a, ab) / l2, 0, 1) : 0;
        return (a + ab * t - p).Length;
    }

    static double DistToPolygon(Vec2 p, IReadOnlyList<Vec2> poly) =>
        poly.Count >= 3 && Polygon2.Contains(poly, p) ? 0 : DistToPolyline(p, [.. poly, poly[0]]);

    /// <summary>
    /// Horizontal bars into the mid-plane of the slab they carry (one within their depth, over them); column and wall ends to
    /// the highest beam or slab at their foot or head within <see cref="EndReach"/> below it.
    /// </summary>
    static void SnapLevels(List<Bar> bars, List<Plate> plates)
    {
        var slabs = plates.Where(p => !p.IsWall && p.Outline.Count >= 3).ToList();
        foreach (var b in bars.Where(b => b.Horizontal && b.Src.Depth > 0))
        {
            var top = b.Z + b.Src.Depth / 2;
            var mid = Mid(b.Pts);
            var reach = b.Src.Width / 2 + 0.3;
            var slab = slabs.Where(s => s.Z <= top + 0.01 && s.Z >= top - b.Src.Depth - 0.01 && DistToPolygon(mid, s.Outline) <= reach)
                .OrderByDescending(s => s.Z).FirstOrDefault();
            if (slab == null) continue;
            b.OffsetZ = b.Z - slab.Z;
            b.Pts = b.Pts.Select(p => p with { Z = slab.Z }).ToList();
        }
        // What a vertical member's end may land on at a plan point: the beams over it and the slabs round it.
        // A sloping bar (a rafter) gives its height where it passes over the probe; each level with how far off its axis the probe is.
        IEnumerable<(double Z, double Off)> LevelsAt(IReadOnlyList<Vec2> probes, double reach) =>
            bars.Where(b => !b.Vertical).SelectMany(b => probes.SelectMany(q => HeightsOver(b, q, reach + b.Src.Width / 2)))
                .Concat(slabs.SelectMany(s => probes.Select(q => DistToPolygon(q, s.Outline)).Where(d => d <= reach).Select(d => (s.Z, d))));
        // The highest level below the end (within EndReach) among those whose axis passes through it (5 cm), else among all within reach.
        double Snap(double z, IReadOnlyList<Vec2> probes, double reach)
        {
            var near = LevelsAt(probes, reach).Where(l => l.Z <= z + 0.01 && l.Z >= z - EndReach).ToList();
            var through = near.Where(l => l.Off <= 0.05).ToList();
            var pick = through.Count > 0 ? through : near;
            return pick.Count > 0 ? pick.Max(l => l.Z) : z;
        }
        foreach (var c in bars.Where(b => b.Vertical))
        {
            var at = c.Pts[0].Xy;
            var reach = Math.Max(Math.Sqrt(c.Src.Width * c.Src.Width + c.Src.Depth * c.Src.Depth) / 2, 0.05) + 0.05;
            var (lo, hi) = c.Pts[0].Z <= c.Pts[1].Z ? (0, 1) : (1, 0);
            var zTop = Snap(c.Pts[hi].Z, [at], reach);
            var zBottom = Snap(c.Pts[lo].Z, [at], reach);
            if (zTop - zBottom > Tolerance) { c.Pts[hi] = c.Pts[hi] with { Z = zTop }; c.Pts[lo] = c.Pts[lo] with { Z = zBottom }; }
        }
        foreach (var w in plates.Where(p => p.IsWall && p.Line.Count >= 2))
        {
            var probes = new List<Vec2>();
            for (var i = 0; i + 1 < w.Line.Count; i++) probes.Add((w.Line[i] + w.Line[i + 1]) * 0.5);
            var reach = w.Src.Thickness / 2 + 0.05;
            var z1 = Snap(w.Z1, probes, reach);
            var z0 = Snap(w.Z0, probes, reach);
            if (z1 - z0 > Tolerance) { w.Z0 = z0; w.Z1 = z1; }
        }
    }

    /// <summary>The bar's height over a plan point within <paramref name="reach"/> of its axis in plan, segment by segment.</summary>
    static IEnumerable<(double Z, double Off)> HeightsOver(Bar b, Vec2 q, double reach)
    {
        for (var i = 0; i + 1 < b.Pts.Count; i++)
        {
            var (a, c) = (b.Pts[i], b.Pts[i + 1]);
            var ab = c.Xy - a.Xy;
            var l2 = Vec2.Dot(ab, ab);
            if (l2 < 1e-12) continue;
            var t = Math.Clamp(Vec2.Dot(q - a.Xy, ab) / l2, 0, 1);
            var off = (a.Xy + ab * t - q).Length;
            if (off <= reach) yield return (a.Z + (c.Z - a.Z) * t, off);
        }
    }

    /// <summary>
    /// Two bars whose ends nearly meet (a portal's rafters at the apex, set back by their connection) meet: both ends move to
    /// where their axes cross (the middle of the gap when they run in line), within half the deeper section and 5 cm.
    /// </summary>
    static void JoinEnds(List<Bar> bars)
    {
        var ends = bars.Where(b => !b.Vertical).SelectMany(b => new[] { (Bar: b, K: 0), (Bar: b, K: b.Pts.Count - 1) }).ToList();
        for (var x = 0; x < ends.Count; x++)
            for (var y = x + 1; y < ends.Count; y++)
            {
                var (a, b) = (ends[x], ends[y]);
                if (a.Bar == b.Bar) continue;
                var (pa, pb) = (a.Bar.Pts[a.K], b.Bar.Pts[b.K]);
                var gap = (pa - pb).Length;
                if (gap <= Tolerance || gap > Math.Max(a.Bar.Src.Depth, b.Bar.Src.Depth) / 2 + 0.05) continue;
                // Each end's direction along its bar, outwards.
                var da = (pa - a.Bar.Pts[a.K == 0 ? 1 : a.K - 1]).Normalized();
                var db = (pb - b.Bar.Pts[b.K == 0 ? 1 : b.K - 1]).Normalized();
                var meet = (pa + pb) * 0.5;
                var w0 = pa - pb;
                var (aa, bb, cc, dd, ee) = (Vec3.Dot(da, da), Vec3.Dot(da, db), Vec3.Dot(db, db), Vec3.Dot(da, w0), Vec3.Dot(db, w0));
                var den = aa * cc - bb * bb;
                if (den > 1e-9)
                {
                    var (s, t) = ((bb * ee - cc * dd) / den, (aa * ee - bb * dd) / den);
                    var (qa, qb) = (pa + da * s, pb + db * t);
                    if ((qa - qb).Length <= 5 * Tolerance && Math.Abs(s) <= gap * 4 + 0.05 && Math.Abs(t) <= gap * 4 + 0.05) meet = (qa + qb) * 0.5;
                }
                a.Bar.Pts[a.K] = meet;
                b.Bar.Pts[b.K] = meet;
            }
    }

    static Vec2 Mid(IReadOnlyList<Vec3> pts)
    {
        if (pts.Count == 2) return (pts[0].Xy + pts[1].Xy) * 0.5;
        var i = pts.Count / 2;
        return pts.Count % 2 == 1 ? pts[i].Xy : (pts[i - 1].Xy + pts[i].Xy) * 0.5;
    }

    /// <summary>Beam ends onto column axes, wall ends onto columns and the walls they meet in a T, slab corners onto their supports' nodes.</summary>
    static void SnapPlan(List<Bar> bars, List<Plate> plates)
    {
        var columns = bars.Where(b => b.Vertical).ToList();
        double Reach(Bar c) => Math.Max(Math.Sqrt(c.Src.Width * c.Src.Width + c.Src.Depth * c.Src.Depth) / 2, 0.05) + 0.02;
        bool Spans(Bar c, double z) => z >= Math.Min(c.Pts[0].Z, c.Pts[1].Z) - 0.01 && z <= Math.Max(c.Pts[0].Z, c.Pts[1].Z) + 0.01;
        Bar? ColumnAt(Vec2 p, double z) => columns.Where(c => (c.Pts[0].Xy - p).Length <= Reach(c) && Spans(c, z)).MinBy(c => (c.Pts[0].Xy - p).Length);
        foreach (var b in bars.Where(b => !b.Vertical))
            foreach (var k in new[] { 0, b.Pts.Count - 1 })
                if (ColumnAt(b.Pts[k].Xy, b.Pts[k].Z) is { } c) b.Pts[k] = c.Pts[0].Xy.At(b.Pts[k].Z);
        var walls = plates.Where(p => p.IsWall && p.Line.Count >= 2).ToList();
        foreach (var w in walls)
            foreach (var k in new[] { 0, w.Line.Count - 1 })
            {
                var p = w.Line[k];
                if (columns.Where(c => (c.Pts[0].Xy - p).Length <= Reach(c) && Spans(c, (w.Z0 + w.Z1) / 2)).MinBy(c => (c.Pts[0].Xy - p).Length) is { } c) { w.Line[k] = c.Pts[0].Xy; continue; }
                // A T: the end on another wall's centreline within that wall's half thickness, moved onto it.
                foreach (var o in walls)
                {
                    if (o == w || o.Z1 < w.Z0 + 0.01 || o.Z0 > w.Z1 - 0.01) continue;
                    for (var i = 0; i + 1 < o.Line.Count; i++)
                    {
                        var (a, b2) = (o.Line[i], o.Line[i + 1]);
                        var d = DistToSegment(p, a, b2);
                        if (d < 1e-9 || d > o.Src.Thickness / 2 + 0.02) continue;
                        var ab = b2 - a;
                        var t = Math.Clamp(Vec2.Dot(p - a, ab) / Vec2.Dot(ab, ab), 0, 1);
                        w.Line[k] = a + ab * t;
                        goto next;
                    }
                }
                next:;
            }
        // The anchors a slab's corner may move onto: column ends, wall corners and beam ends at its level.
        foreach (var s in plates.Where(p => !p.IsWall && p.Outline.Count >= 3))
        {
            var anchors = new List<Vec2>();
            foreach (var c in columns) if (c.Pts.Any(p => Math.Abs(p.Z - s.Z) <= 0.02)) anchors.Add(c.Pts[0].Xy);
            foreach (var w in walls) if (Math.Abs(w.Z1 - s.Z) <= 0.02 || Math.Abs(w.Z0 - s.Z) <= 0.02) anchors.AddRange(w.Line);
            foreach (var b in bars.Where(b => b.Horizontal && Math.Abs(b.Z - s.Z) <= 0.02)) { anchors.Add(b.Pts[0].Xy); anchors.Add(b.Pts[^1].Xy); }
            if (anchors.Count == 0) continue;
            var moved = s.Outline.Select(v => anchors.MinBy(a => (a - v).Length) is var a && (a - v).Length <= CornerReach ? a : v).ToList();
            var clean = new List<Vec2>();
            foreach (var v in moved) if (clean.Count == 0 || (clean[^1] - v).Length > Tolerance) clean.Add(v);
            while (clean.Count > 1 && (clean[0] - clean[^1]).Length <= Tolerance) clean.RemoveAt(clean.Count - 1);
            if (clean.Count >= 3 && Math.Abs(Polygon2.SignedArea(clean)) > 1e-4) s.Outline = clean;
        }
    }

    // ------------------------------------------------------------ nodes

    readonly Dictionary<(long, long, long), List<ANode>> _grid = [];

    ANode Node(Vec3 p)
    {
        var key = ((long)Math.Floor(p.X / Tolerance), (long)Math.Floor(p.Y / Tolerance), (long)Math.Floor(p.Z / Tolerance));
        for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
                for (var dz = -1; dz <= 1; dz++)
                    if (_grid.TryGetValue((key.Item1 + dx, key.Item2 + dy, key.Item3 + dz), out var cell))
                        foreach (var n in cell)
                            if (Vec3.DistanceSquared(n.At, p) <= Tolerance * Tolerance) return n;
        var node = new ANode(Nodes.Count, p);
        Nodes.Add(node);
        if (!_grid.TryGetValue(key, out var list)) _grid[key] = list = [];
        list.Add(node);
        return node;
    }

    /// <summary>The nodes strictly inside the segment a–b (within the tolerance of it), in order from a.</summary>
    List<ANode> On(Vec3 a, Vec3 b, ANode? skipA = null, ANode? skipB = null)
    {
        var ab = b - a;
        var l2 = Vec3.Dot(ab, ab);
        var o = new List<(double T, ANode N)>();
        if (l2 < Tolerance * Tolerance) return [];
        var (minX, maxX) = (Math.Min(a.X, b.X) - Tolerance, Math.Max(a.X, b.X) + Tolerance);
        var (minY, maxY) = (Math.Min(a.Y, b.Y) - Tolerance, Math.Max(a.Y, b.Y) + Tolerance);
        var (minZ, maxZ) = (Math.Min(a.Z, b.Z) - Tolerance, Math.Max(a.Z, b.Z) + Tolerance);
        var length = Math.Sqrt(l2);
        foreach (var n in Nodes)
        {
            var p = n.At;
            if (p.X < minX || p.X > maxX || p.Y < minY || p.Y > maxY || p.Z < minZ || p.Z > maxZ || n == skipA || n == skipB) continue;
            var t = Vec3.Dot(p - a, ab) / l2;
            if (t * length <= Tolerance || (1 - t) * length <= Tolerance) continue;
            if (Vec3.DistanceSquared(a + ab * t, p) <= Tolerance * Tolerance) o.Add((t, n));
        }
        return o.OrderBy(x => x.T).Select(x => x.N).ToList();
    }

    // ------------------------------------------------------------ assembly

    void Assemble(List<Bar> bars, List<Plate> plates, IReadOnlyList<AnalyticalSupport> supports, IModelView m)
    {
        // Every bar's points, every shell's corners.
        foreach (var b in bars) foreach (var p in b.Pts) Node(p);
        var slabPieces = new List<(Plate Plate, List<Vec2> Outline)>();
        foreach (var s in plates.Where(p => !p.IsWall))
        {
            if (s.Outline.Count < 3) { LeftOut.Add(new(s.Src.SourceId, s.Src.Name, s.Src.IfcClass, "no outline")); continue; }
            foreach (var v in s.Outline) Node(v.At(s.Z));
        }
        foreach (var w in plates.Where(p => p.IsWall))
        {
            if (w.Line.Count < 2 || !(w.Z1 - w.Z0 > Tolerance)) { LeftOut.Add(new(w.Src.SourceId, w.Src.Name, w.Src.IfcClass, "no length or height")); continue; }
            foreach (var v in w.Line) { Node(v.At(w.Z0)); Node(v.At(w.Z1)); }
        }

        // Slabs split along the beams and wall heads crossing them (convex ones; others share nodes on their edges only).
        var lines = new List<(Vec2 A, Vec2 B, double Z)>();
        foreach (var b in bars.Where(b => b.Horizontal)) for (var i = 0; i + 1 < b.Pts.Count; i++) lines.Add((b.Pts[i].Xy, b.Pts[i + 1].Xy, b.Z));
        foreach (var w in plates.Where(p => p.IsWall)) for (var i = 0; i + 1 < w.Line.Count; i++) { lines.Add((w.Line[i], w.Line[i + 1], w.Z1)); lines.Add((w.Line[i], w.Line[i + 1], w.Z0)); }
        foreach (var s in plates.Where(p => !p.IsWall && p.Outline.Count >= 3))
        {
            var pieces = new List<List<Vec2>> { Polygon2.EnsureCcw(s.Outline).ToList() };
            if (!Convex(pieces[0]))
                Notes.Add($"Slab „{s.Src.Name}” is not convex: it is one shell, its edges sharing the nodes of the members on them, not split along the supports inside it.");
            else
                foreach (var (a, b, z) in lines.Where(l => Math.Abs(l.Z - s.Z) <= 0.02))
                    pieces = pieces.SelectMany(piece => Split(piece, a, b)).ToList();
            foreach (var piece in pieces) { foreach (var v in piece) Node(v.At(s.Z)); slabPieces.Add((s, piece)); }
        }

        // Walls split where another wall ends on them (a T) and where a column stands in them.
        var wallPieces = new List<(Plate Plate, Vec2 A, Vec2 B)>();
        var columns = bars.Where(b => b.Vertical).ToList();
        foreach (var w in plates.Where(p => p.IsWall && p.Line.Count >= 2 && p.Z1 - p.Z0 > Tolerance))
            for (var i = 0; i + 1 < w.Line.Count; i++)
            {
                var (a, b) = (w.Line[i], w.Line[i + 1]);
                var ab = b - a;
                var len = ab.Length;
                if (len <= Tolerance) continue;
                var stations = new List<double> { 0, len };
                void Station(Vec2 p, double z0, double z1)
                {
                    if (z1 < w.Z0 + Tolerance || z0 > w.Z1 - Tolerance || DistToSegment(p, a, b) > Tolerance) return;
                    var t = Vec2.Dot(p - a, ab) / len;
                    if (t > Tolerance && t < len - Tolerance) stations.Add(t);
                }
                foreach (var o in plates.Where(o => o != w && o.IsWall && o.Line.Count >= 2)) { Station(o.Line[0], o.Z0, o.Z1); Station(o.Line[^1], o.Z0, o.Z1); }
                foreach (var c in columns) Station(c.Pts[0].Xy, Math.Min(c.Pts[0].Z, c.Pts[1].Z), Math.Max(c.Pts[0].Z, c.Pts[1].Z));
                stations = stations.OrderBy(x => x).Aggregate(new List<double>(), (acc, x) => { if (acc.Count == 0 || x - acc[^1] > Tolerance) acc.Add(x); return acc; });
                var dir = ab * (1 / len);
                for (var k = 0; k + 1 < stations.Count; k++)
                {
                    var (p, q) = (a + dir * stations[k], a + dir * stations[k + 1]);
                    foreach (var v in new[] { p, q }) { Node(v.At(w.Z0)); Node(v.At(w.Z1)); }
                    wallPieces.Add((w, p, q));
                }
            }

        // The shells, with every node on their edges.
        List<ANode> Ring(IReadOnlyList<Vec3> corners)
        {
            var ring = new List<ANode>();
            for (var i = 0; i < corners.Count; i++)
            {
                var (p, q) = (corners[i], corners[(i + 1) % corners.Count]);
                var np = Node(p);
                if (ring.Count == 0 || ring[^1] != np) ring.Add(np);
                ring.AddRange(On(p, q, np, Node(q)));
            }
            while (ring.Count > 1 && ring[0] == ring[^1]) ring.RemoveAt(ring.Count - 1);
            return ring.Distinct().ToList();
        }
        var shells = new List<(AnalyticalShell Src, List<ANode> Ring)>();
        foreach (var (s, piece) in slabPieces) shells.Add((s.Src, Ring(piece.Select(v => v.At(s.Z)).ToList())));
        foreach (var (w, a, b) in wallPieces) shells.Add((w.Src, Ring([a.At(w.Z0), b.At(w.Z0), b.At(w.Z1), a.At(w.Z1)])));
        foreach (var g in shells.GroupBy(x => x.Src.SourceId))
        {
            var list = g.Where(x => x.Ring.Count >= 3).ToList();
            for (var k = 0; k < list.Count; k++)
                Shells.Add(new AShell(list.Count == 1 ? g.Key : $"{g.Key}#{k + 1}", list[k].Src, k + 1, list.Count, list[k].Ring));
        }

        // The bars, split at every node on their span.
        foreach (var b in bars)
        {
            var nodes = new List<ANode>();
            for (var i = 0; i + 1 < b.Pts.Count; i++)
            {
                var (p, q) = (Node(b.Pts[i]), Node(b.Pts[i + 1]));
                if (nodes.Count == 0) nodes.Add(p);
                nodes.AddRange(On(p.At, q.At, p, q));
                if (nodes[^1] != q) nodes.Add(q);
            }
            var segs = new List<(ANode I, ANode J)>();
            for (var i = 0; i + 1 < nodes.Count; i++) if (nodes[i] != nodes[i + 1]) segs.Add((nodes[i], nodes[i + 1]));
            if (segs.Count == 0) { LeftOut.Add(new(b.Src.SourceId, b.Src.Name, b.Src.IfcClass, "shorter than the node tolerance")); continue; }
            for (var k = 0; k < segs.Count; k++)
                Frames.Add(new AFrame(segs.Count == 1 ? b.Src.SourceId : $"{b.Src.SourceId}#{k + 1}", b.Src, k + 1, segs.Count, segs[k].I, segs[k].J,
                    Rotation(segs[k].I.At, segs[k].J.At, b.Src.DepthDir), b.OffsetZ));
        }

        ApplySupports(bars, plates, supports);
        AssignStoreys(m);
        NameNodes();
    }

    static bool Convex(IReadOnlyList<Vec2> ccw)
    {
        for (var i = 0; i < ccw.Count; i++)
        {
            var (a, b, c) = (ccw[i], ccw[(i + 1) % ccw.Count], ccw[(i + 2) % ccw.Count]);
            if (Vec2.Cross(b - a, c - b) < -1e-9) return false;
        }
        return true;
    }

    /// <summary>A convex piece cut in two by the line a–b when the segment spans the whole chord across it; else the piece as it is.</summary>
    static IEnumerable<List<Vec2>> Split(List<Vec2> piece, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        var len = ab.Length;
        if (len <= Tolerance) return [piece];
        var dir = ab * (1 / len);
        var n = dir.Perp;
        double Side(Vec2 p) => Vec2.Dot(p - a, n);
        if (piece.All(p => Side(p) >= -Tolerance) || piece.All(p => Side(p) <= Tolerance)) return [piece];
        // The chord: where the line crosses the piece, along it.
        var ts = new List<double>();
        for (var i = 0; i < piece.Count; i++)
        {
            var (p, q) = (piece[i], piece[(i + 1) % piece.Count]);
            var (sp, sq) = (Side(p), Side(q));
            if ((sp < 0) != (sq < 0) && Math.Abs(sp - sq) > 1e-12) ts.Add(Vec2.Dot(p + (q - p) * (sp / (sp - sq)) - a, dir));
        }
        if (ts.Count < 2 || ts.Min() < -Tolerance * 2 || ts.Max() > len + Tolerance * 2) return [piece];
        List<Vec2> Keep(int sign)
        {
            var o = new List<Vec2>();
            for (var i = 0; i < piece.Count; i++)
            {
                var (p, q) = (piece[i], piece[(i + 1) % piece.Count]);
                var (sp, sq) = (Side(p) * sign, Side(q) * sign);
                if (sp >= 0) o.Add(p);
                if ((sp >= 0) != (sq >= 0) && Math.Abs(sp - sq) > 1e-12) o.Add(p + (q - p) * (sp / (sp - sq)));
            }
            var clean = new List<Vec2>();
            foreach (var v in o) if (clean.Count == 0 || (clean[^1] - v).Length > 1e-9) clean.Add(v);
            if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 1e-9) clean.RemoveAt(clean.Count - 1);
            return clean;
        }
        var (left, right) = (Keep(1), Keep(-1));
        return left.Count >= 3 && right.Count >= 3 && Math.Abs(Polygon2.SignedArea(left)) > 1e-4 && Math.Abs(Polygon2.SignedArea(right)) > 1e-4 ? [left, right] : [piece];
    }

    /// <summary>
    /// The local axes' rotation (degrees): local 1 from i to j; local 2 before turning is +X for a vertical bar, else the
    /// upward perpendicular to local 1 in its vertical plane; the angle turns it about local 1 (right hand) onto the
    /// section's depth.
    /// </summary>
    public static double Rotation(Vec3 i, Vec3 j, Vec3 depth)
    {
        var u = (j - i).Normalized();
        var reference = Reference(u);
        var d = (depth - u * Vec3.Dot(depth, u)).Normalized();
        if (d.Length < 0.5) return 0;
        var angle = Math.Atan2(Vec3.Dot(Vec3.Cross(reference, d), u), Vec3.Dot(reference, d)) * 180 / Math.PI;
        angle = Math.Round(angle, 6);
        if (angle <= -180) angle += 360;
        return angle == -0.0 ? 0 : angle;
    }

    /// <summary>Local 2 before turning: +X for a vertical bar (within 0.001 rad), else the upward perpendicular to local 1 in its vertical plane.</summary>
    public static Vec3 Reference(Vec3 u)
    {
        if (Math.Sqrt(u.X * u.X + u.Y * u.Y) < 1e-3) return new Vec3(1, 0, 0);
        return (Vec3.UnitZ - u * u.Z).Normalized();
    }

    void ApplySupports(List<Bar> bars, List<Plate> plates, IReadOnlyList<AnalyticalSupport> supports)
    {
        // The feet: a column's lower end, a wall piece's lower corners and the nodes on its foot, each with the element it is the foot of.
        var owners = new Dictionary<ANode, HashSet<string>>();
        void Foot(ANode n, string id) { if (!owners.TryGetValue(n, out var set)) owners[n] = set = []; set.Add(id); }
        foreach (var f in Frames.Where(f => f.Source.Kind == "column" && f.Piece == 1)) Foot(f.I.At.Z <= f.J.At.Z ? f.I : f.J, f.Source.SourceId);
        foreach (var s in Shells.Where(s => s.Source.Kind == "wall"))
        {
            var z0 = s.Nodes.Min(n => n.At.Z);
            foreach (var n in s.Nodes.Where(n => Math.Abs(n.At.Z - z0) <= Tolerance)) Foot(n, s.Source.SourceId);
        }
        var feet = owners.Keys.ToHashSet();
        foreach (var sup in supports.Where(s => s.ForFrame == null))
        {
            bool Over(Vec2 p) => sup.Region is { Count: >= 3 } region ? DistToPolygon(p, region) <= 0.05
                : sup.LineA is { } a && sup.LineB is { } b && DistToSegment(p, a, b) <= sup.Width / 2 + 0.05;
            var hits = feet.Where(n => Over(n.At.Xy) && n.At.Z >= sup.Bottom - 0.05 && n.At.Z <= sup.Top + 2.0).ToList();
            if (hits.Count == 0) continue;
            // Only the lowest feet on it (a column on a footing, not one standing on a slab over it).
            foreach (var g in hits.GroupBy(n => (Math.Round(n.At.X / 0.05), Math.Round(n.At.Y / 0.05))))
            {
                var low = g.Min(n => n.At.Z);
                foreach (var n in g.Where(n => n.At.Z <= low + Tolerance && n.Restraints == null)) Hold(n, sup.Spec);
            }
        }
        // A connection's support on its member's end overrides the footing's.
        foreach (var sup in supports.Where(s => s.ForFrame != null))
        {
            var pieces = Frames.Where(f => f.Source.SourceId == sup.ForFrame).OrderBy(f => f.Piece).ToList();
            if (pieces.Count == 0) continue;
            var node = sup.AtEnd ? pieces[^1].J : pieces[0].I;
            Hold(node, sup.Spec);
        }
        // Feet nothing holds and nothing else meets.
        foreach (var n in feet.Where(n => n.Restraints == null).OrderBy(n => n.Index))
        {
            var touching = Frames.Where(f => f.I == n || f.J == n).Select(f => f.Source.SourceId).Concat(Shells.Where(s => s.Nodes.Contains(n)).Select(s => s.Source.SourceId));
            if (touching.All(owners[n].Contains)) FreeBases.Add(n);
        }
    }

    static void Hold(ANode n, SupportSpec spec)
    {
        n.Restraints = [.. spec.Dofs];
        n.RestraintKind = spec.Kind;
        n.RestraintSource = spec.Source;
    }

    /// <summary>
    /// A node's storey: the storey whose level it lies on (within half a metre, the nearest: a slab's mid-plane and the beams in
    /// it belong to the level they carry), else the storey it stands in; below every storey, none.
    /// </summary>
    void AssignStoreys(IModelView m)
    {
        foreach (var n in Nodes)
        {
            var z = n.At.Z;
            var near = Storeys.Select(s => (S: s, D: Math.Abs(s.Num("elevation") - z))).Where(x => x.D <= 0.5).OrderBy(x => x.D).FirstOrDefault().S;
            n.StoreyId = (near ?? Storeys.LastOrDefault(s => s.Num("elevation") <= z + Tolerance))?.Id;
        }
    }

    /// <summary>Node ids N1… in order of height, then y, then x (a model exported twice names its nodes alike).</summary>
    void NameNodes()
    {
        var used = Nodes.Where(n => Frames.Any(f => f.I == n || f.J == n) || Shells.Any(s => s.Nodes.Contains(n))).ToHashSet();
        var k = 0;
        foreach (var n in Nodes.Where(used.Contains).OrderBy(n => Math.Round(n.At.Z, 4)).ThenBy(n => Math.Round(n.At.Y, 4)).ThenBy(n => Math.Round(n.At.X, 4)))
            n.Id = $"N{++k}";
        Nodes.RemoveAll(n => !used.Contains(n));
        Nodes.Sort((a, b) => int.Parse(a.Id[1..]).CompareTo(int.Parse(b.Id[1..])));
    }
}
