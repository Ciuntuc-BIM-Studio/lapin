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

/// <summary>
/// One bar of the analytical model: a piece of its element's axis between two nodes, its local axes' rotation. An eccentric
/// end (<see cref="OffI"/>, <see cref="OffJ"/>, metres, global) is where the member's own end is from its node: the bar runs
/// between its node's point plus the offset at each end, joined to the node rigidly.
/// </summary>
public sealed record AFrame(string Id, AnalyticalFrame Source, int Piece, int Pieces, ANode I, ANode J, double RotationDeg, double OffsetZ)
{
    public Vec3 OffI { get; init; }
    public Vec3 OffJ { get; init; }
    public bool Eccentric => OffI.Length > 1e-9 || OffJ.Length > 1e-9;
    /// <summary>What the eccentric ends connect to (a connection's name, or the member the end bears on).</summary>
    public string? Connection { get; init; }
}

/// <summary>One shell of the analytical model: a piece of its element's surface, its nodes in order round it.</summary>
public sealed record AShell(string Id, AnalyticalShell Source, int Piece, int Pieces, IReadOnlyList<ANode> Nodes);

/// <summary>A bar end only one bar uses and nothing holds: a cantilever's tip, or (<see cref="Cantilever"/> false) an end next to a member it could not be joined to.</summary>
public sealed record FreeEnd(AFrame Frame, ANode Node, bool Cantilever, string? Near);

/// <summary>
/// The analytical model derived from the physical one (docs/jg-structures.md, The analytical model): the modules' members
/// (<see cref="IAnalyticalElements"/>) laid on shared nodes. Horizontal bars move into the mid-plane of the slab they carry,
/// column and wall ends to the beam or slab they meet, beam ends onto column axes, wall ends onto the walls they meet in a T,
/// slab corners onto the nodes of their supports; points closer than <see cref="Tolerance"/> are one node. Eccentric
/// connections (a purlin on a rafter, a rail on a column, any bar end or crossing near another member's axis or a shell)
/// put the node on the supporting member and keep the eccentricity as the bar's end offset. Shells are cut into convex
/// pieces without holes (<see cref="ShellPieces"/>): at re-entrant corners, along the supports crossing them, round their
/// openings; a wall's top follows the roof or slab over it. Supports hold the lowest column and wall ends on a footing, or a
/// steel column base as its connection says. Ids come from the elements' GlobalIds (a piece's with #n).
/// </summary>
public sealed class AnalyticalModel
{
    /// <summary>Points closer than this (metres) are one node.</summary>
    public const double Tolerance = 0.01;
    /// <summary>How far below a column's or a wall's end (metres) the beam or slab it meets may lie.</summary>
    public const double EndReach = 1.0;
    /// <summary>How far a slab's corner (metres) moves onto a support's node.</summary>
    public const double CornerReach = 0.5;
    /// <summary>How far past the two members' half sizes (metres) an eccentric end or crossing still bears on a member; a known connection's reach.</summary>
    public const double EccentricMargin = 0.05, LinkMargin = 0.15;
    /// <summary>A shell's piece narrower than this (metres) goes with the opening beside it.</summary>
    public const double MinPieceWidth = 0.03;

    public List<ANode> Nodes { get; } = [];
    public List<AFrame> Frames { get; } = [];
    public List<AShell> Shells { get; } = [];
    public List<LeftOut> LeftOut { get; } = [];
    public List<string> Notes { get; } = [];
    public List<Entity> Storeys { get; } = [];
    /// <summary>The nodes at a column's or wall's foot that nothing holds and nothing else meets: they need a support.</summary>
    public List<ANode> FreeBases { get; } = [];
    /// <summary>Bar ends that meet nothing else.</summary>
    public List<FreeEnd> FreeEnds { get; } = [];
    /// <summary>The members in parts of the model that no support holds (each part's elements' names).</summary>
    public List<List<string>> Floating { get; } = [];
    /// <summary>How many ends and crossings were joined eccentrically (their offsets kept).</summary>
    public int EccentricJoins { get; private set; }
    /// <summary>The connections the physical model knows (cleats, gussets, fly braces): an analysis pins the members they join (<see cref="FrameReleases"/>).</summary>
    public List<AnalyticalLink> Links { get; } = [];
    /// <summary>The roofs framed in bars, with their planes (an analysis lays their area loads on their members plane by plane).</summary>
    public List<AnalyticalRoof> Roofs { get; } = [];

    sealed class Bar(AnalyticalFrame src)
    {
        public AnalyticalFrame Src = src;
        public List<Vec3> Pts = [.. src.Points];
        public double OffsetZ;
        /// <summary>The bar moved onto the wall it lies on: its own axis is <see cref="Pts"/> + Shift.</summary>
        public Vec3 Shift;
        /// <summary>A node an end goes to instead of its own point (an eccentric end), with what it bears on.</summary>
        public readonly Vec3?[] EndNode = new Vec3?[2];
        public readonly string?[] EndOn = new string?[2];
        /// <summary>Points of the bar's own axis joined to a node off it (an eccentric crossing).</summary>
        public readonly List<(Vec3 At, Vec3 Node, string On)> Attach = [];
        /// <summary>A known connection fixes it: its joins come from the connections only.</summary>
        public bool Linked;
        public bool Vertical => Pts.Count == 2 && (Pts[1].Xy - Pts[0].Xy).Length < 1e-6 && Math.Abs(Pts[1].Z - Pts[0].Z) > 1e-6;
        public bool Horizontal => Pts.All(p => Math.Abs(p.Z - Pts[0].Z) < 1e-6);
        public double Z => Pts[0].Z;
        public double Half => Math.Max(Src.Depth, Src.Width) / 2;
        public List<Vec3> Physical => Pts.Select(p => p + Shift).ToList();
        public double Length { get { var l = 0.0; for (var i = 1; i < Pts.Count; i++) l += (Pts[i] - Pts[i - 1]).Length; return l; } }
    }

    sealed class Plate(AnalyticalShell src)
    {
        public AnalyticalShell Src = src;
        public bool IsWall => Src.Line != null;
        public List<Vec2> Outline = src.Outline?.ToList() ?? [];
        public double Z = src.Z;
        public Vec2 Gradient = src.Gradient;
        public List<Vec2> Line = src.Line?.ToList() ?? [];
        public double Z0 = src.Z0, Z1 = src.Z1;
        public List<Vec3>? Top = src.Top?.ToList();
        /// <summary>Points that must be nodes of the shell (a bar's end on it).</summary>
        public readonly List<Vec3> Interior = [];
        public double ZAt(Vec2 p) => Z + Vec2.Dot(Gradient, p);
        public bool Level => Gradient.Length < 1e-9;
        /// <summary>The vertical half thickness of a sloping slab (its half thickness over the slope's cosine).</summary>
        public double HalfV => Src.Thickness / 2 * Math.Sqrt(1 + Vec2.Dot(Gradient, Gradient));
    }

    /// <summary>A planar stretch of a wall: its chord from A along Dir, and its outline in (along, up): the foot's two corners, then the top from the end back to the start.</summary>
    sealed record Face(Plate Wall, Vec2 A, Vec2 Dir, double Len, List<Vec2> Poly)
    {
        public Vec3 At(Vec2 sz) => (A + Dir * sz.X).At(sz.Y);
        public Vec2 Local(Vec3 p) => new(Vec2.Dot(p.Xy - A, Dir), p.Z);
        public double Off(Vec3 p) => Math.Abs(Vec2.Cross(Dir, p.Xy - A));
    }

    public static AnalyticalModel Build(IModelView m, IServices services, AnalyticalOptions options)
    {
        var all = new AnalyticalContribution { Services = services };
        foreach (var source in services.All<IAnalyticalElements>()) source.Contribute(m, all);
        var model = new AnalyticalModel();
        model.Storeys.AddRange(Spatial.Storeys(m));
        model.LeftOut.AddRange(all.LeftOut);
        model.Links.AddRange(all.Links);
        model.Roofs.AddRange(all.Roofs);

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
            var kind = s.Kind == "wall" ? AnalyticalKinds.Walls : s.IfcClass == "IfcRoof" ? AnalyticalKinds.Roofs : AnalyticalKinds.Slabs;
            if (!options.Takes(kind)) { model.LeftOut.Add(new(s.SourceId, s.Name, s.IfcClass, $"{kind} not chosen")); continue; }
            if (s.Kind == "wall" && !s.LoadBearing && !options.AllWalls) { model.LeftOut.Add(new(s.SourceId, s.Name, s.IfcClass, "not load-bearing (Pset_WallCommon.LoadBearing)")); continue; }
            plates.Add(new Plate(s));
        }
        var supports = options.Takes(AnalyticalKinds.Supports) ? all.Supports : [];

        JoinEnds(bars);
        SnapLevels(bars, plates);
        SnapPlan(bars, plates);
        OntoWalls(bars, plates);
        model.EccentricJoins = Connect(bars, plates, all.Links);
        model.Assemble(bars, plates, supports, m, options);
        return model;
    }

    // ------------------------------------------------------------ snapping

    static double DistToPolyline(Vec2 p, IReadOnlyList<Vec2> pts)
    {
        var best = double.PositiveInfinity;
        for (var i = 0; i + 1 < pts.Count; i++) best = Math.Min(best, DistToSegment(p, pts[i], pts[i + 1]));
        return pts.Count == 1 ? (p - pts[0]).Length : best;
    }

    static double DistToSegment(Vec2 p, Vec2 a, Vec2 b) => ShellPieces.DistToSegment(p, a, b);

    static double DistToPolygon(Vec2 p, IReadOnlyList<Vec2> poly) =>
        poly.Count >= 3 && Polygon2.Contains(poly, p) ? 0 : DistToPolyline(p, [.. poly, poly[0]]);

    /// <summary>The point of a 3D polyline closest to p, how far, and how far along the polyline it is.</summary>
    static (Vec3 C, double D, double Along) Closest(Vec3 p, IReadOnlyList<Vec3> pts)
    {
        (Vec3, double, double) best = (pts[0], (pts[0] - p).Length, 0);
        var run = 0.0;
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            var (a, b) = (pts[i], pts[i + 1]);
            var ab = b - a;
            var l2 = Vec3.Dot(ab, ab);
            var t = l2 > 0 ? Math.Clamp(Vec3.Dot(p - a, ab) / l2, 0, 1) : 0;
            var c = a + ab * t;
            var d = (c - p).Length;
            if (d < best.Item2) best = (c, d, run + Math.Sqrt(l2) * t);
            run += Math.Sqrt(l2);
        }
        return best;
    }

    /// <summary>The closest points of two polylines (each with how far along it lies), their distance, and the two segments' directions there.</summary>
    static (Vec3 C1, double Along1, Vec3 C2, double Along2, double D, Vec3 Dir1, Vec3 Dir2) ClosestPair(IReadOnlyList<Vec3> p1, IReadOnlyList<Vec3> p2)
    {
        (Vec3, double, Vec3, double, double, Vec3, Vec3) best = (p1[0], 0, p2[0], 0, double.PositiveInfinity, default, default);
        var run1 = 0.0;
        for (var i = 0; i + 1 < p1.Count; i++)
        {
            var run2 = 0.0;
            var (a1, b1) = (p1[i], p1[i + 1]);
            var l1 = (b1 - a1).Length;
            for (var j = 0; j + 1 < p2.Count; j++)
            {
                var (a2, b2) = (p2[j], p2[j + 1]);
                var l2 = (b2 - a2).Length;
                var (s, t) = SegSeg(a1, b1, a2, b2);
                var (c1, c2) = (a1 + (b1 - a1) * s, a2 + (b2 - a2) * t);
                var d = (c1 - c2).Length;
                if (d < best.Item5) best = (c1, run1 + l1 * s, c2, run2 + l2 * t, d, (b1 - a1).Normalized(), (b2 - a2).Normalized());
                run2 += l2;
            }
            run1 += l1;
        }
        return best;
    }

    /// <summary>The parameters (0–1 on each) of the closest points of two segments.</summary>
    static (double S, double T) SegSeg(Vec3 p1, Vec3 q1, Vec3 p2, Vec3 q2)
    {
        var (d1, d2, r) = (q1 - p1, q2 - p2, p1 - p2);
        var (a, e, f) = (Vec3.Dot(d1, d1), Vec3.Dot(d2, d2), Vec3.Dot(d2, r));
        if (a < 1e-12 && e < 1e-12) return (0, 0);
        if (a < 1e-12) return (0, Math.Clamp(f / e, 0, 1));
        var c = Vec3.Dot(d1, r);
        if (e < 1e-12) return (Math.Clamp(-c / a, 0, 1), 0);
        var b = Vec3.Dot(d1, d2);
        var den = a * e - b * b;
        var s = den > 1e-14 ? Math.Clamp((b * f - c * e) / den, 0, 1) : 0;
        var t = (b * s + f) / e;
        if (t < 0) { t = 0; s = Math.Clamp(-c / a, 0, 1); }
        else if (t > 1) { t = 1; s = Math.Clamp((b - c) / a, 0, 1); }
        return (s, t);
    }

    /// <summary>
    /// Horizontal bars into the mid-plane of the level slab they carry (one within their depth, over them); column and wall
    /// ends to the highest beam or slab at their foot or head within <see cref="EndReach"/> below it, or a slab whose
    /// thickness they reach into above; a wall's top that follows a roof or slab, point by point to the nearest slab over it.
    /// </summary>
    static void SnapLevels(List<Bar> bars, List<Plate> plates)
    {
        var slabs = plates.Where(p => !p.IsWall && p.Outline.Count >= 3).ToList();
        foreach (var b in bars.Where(b => b.Horizontal && b.Src.Depth > 0))
        {
            var top = b.Z + b.Src.Depth / 2;
            var mid = Mid(b.Pts);
            var reach = b.Src.Width / 2 + 0.3;
            var slab = slabs.Where(s => s.Level && s.Z <= top + 0.01 && s.Z >= top - b.Src.Depth - 0.01 && DistToPolygon(mid, s.Outline) <= reach)
                .OrderByDescending(s => s.Z).FirstOrDefault();
            if (slab == null) continue;
            b.OffsetZ = b.Z - slab.Z;
            b.Pts = b.Pts.Select(p => p with { Z = slab.Z }).ToList();
        }
        // What a vertical member's end may land on at a plan point: the beams over it and the slabs round it (a slab also from
        // above, as far as its half thickness: a wall under a roof's deck). A sloping bar (a rafter) gives its height where it
        // passes over the probe; each level with how far off its axis the probe is.
        IEnumerable<(double Z, double Off, double Up)> LevelsAt(IReadOnlyList<Vec2> probes, double reach) =>
            bars.Where(b => !b.Vertical).SelectMany(b => probes.SelectMany(q => HeightsOver(b, q, reach + b.Src.Width / 2)).Select(h => (h.Z, h.Off, 0.0)))
                .Concat(slabs.SelectMany(s => probes.Select(q => (Q: q, D: DistToPolygon(q, s.Outline))).Where(x => x.D <= reach).Select(x => (s.ZAt(x.Q), x.D, s.HalfV + 0.04))));
        double Snap(double z, IReadOnlyList<Vec2> probes, double reach)
        {
            var near = LevelsAt(probes, reach).Where(l => l.Z <= z + 0.01 + l.Up && l.Z >= z - EndReach).ToList();
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
            var z0 = Snap(w.Z0, probes, reach);
            if (w.Top is { Count: >= 2 } top)
            {
                // Each point of the top to the nearest slab or deck over it (within its thickness above, EndReach below).
                for (var i = 0; i < top.Count; i++)
                {
                    var (q, zt) = (top[i].Xy, top[i].Z);
                    var levels = slabs.Where(s => DistToPolygon(q, s.Outline) <= reach).Select(s => s.ZAt(q) is var lz && lz <= zt + s.HalfV + 0.05 && lz >= zt - EndReach ? lz : double.NaN)
                        .Where(double.IsFinite).ToList();
                    if (levels.Count > 0) top[i] = top[i] with { Z = levels.MinBy(l => Math.Abs(l - zt)) };
                }
                if (top.Min(t => t.Z) - z0 > Tolerance) { w.Z0 = z0; w.Z1 = top.Max(t => t.Z); }
                continue;
            }
            var z1 = Snap(w.Z1, probes, reach);
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
        // The anchors a slab's corner may move onto: column ends, wall corners and beam ends on its plane.
        foreach (var s in plates.Where(p => !p.IsWall && p.Outline.Count >= 3))
        {
            var anchors = new List<Vec2>();
            bool OnIt(Vec3 p) => Math.Abs(p.Z - s.ZAt(p.Xy)) <= 0.02;
            foreach (var c in columns) if (c.Pts.Any(OnIt)) anchors.Add(c.Pts[0].Xy);
            foreach (var w in walls) if (w.Line.Any(v => OnIt(v.At(w.Z1)) || OnIt(v.At(w.Z0)))) anchors.AddRange(w.Line);
            foreach (var b in bars.Where(b => !b.Vertical)) foreach (var e in new[] { b.Pts[0], b.Pts[^1] }) if (OnIt(e)) anchors.Add(e.Xy);
            if (anchors.Count == 0) continue;
            var moved = s.Outline.Select(v => anchors.MinBy(a => (a - v).Length) is var a && (a - v).Length <= CornerReach ? a : v).ToList();
            var clean = new List<Vec2>();
            foreach (var v in moved) if (clean.Count == 0 || (clean[^1] - v).Length > Tolerance) clean.Add(v);
            while (clean.Count > 1 && (clean[0] - clean[^1]).Length <= Tolerance) clean.RemoveAt(clean.Count - 1);
            if (clean.Count >= 3 && Math.Abs(Polygon2.SignedArea(clean)) > 1e-4) s.Outline = clean;
        }
    }

    // ------------------------------------------------------------ walls' faces

    /// <summary>A wall's planar stretches: each chord of its centre line, with its outline from the base up to its top (the roof's or slab's over it, or its height).</summary>
    static List<Face> Faces(Plate w)
    {
        var o = new List<Face>();
        for (var i = 0; i + 1 < w.Line.Count; i++)
        {
            var (a, b) = (w.Line[i], w.Line[i + 1]);
            var len = (b - a).Length;
            if (len <= Tolerance) continue;
            var dir = (b - a) * (1 / len);
            var poly = new List<Vec2> { new(0, w.Z0), new(len, w.Z0) };
            if (w.Top is { Count: >= 2 } top && w.Line.Count == 2)
            {
                var pts = top.Select(t => new Vec2(Vec2.Dot(t.Xy - a, dir), t.Z)).ToList();
                double At(double s)
                {
                    if (s <= pts[0].X) return pts[0].Y;
                    for (var k = 0; k + 1 < pts.Count; k++)
                        if (s <= pts[k + 1].X + 1e-12) return pts[k + 1].X - pts[k].X < 1e-12 ? pts[k + 1].Y : pts[k].Y + (pts[k + 1].Y - pts[k].Y) * (s - pts[k].X) / (pts[k + 1].X - pts[k].X);
                    return pts[^1].Y;
                }
                var run = new List<Vec2> { new(0, At(0)) };
                run.AddRange(pts.Where(p => p.X > Tolerance && p.X < len - Tolerance));
                run.Add(new Vec2(len, At(len)));
                run.Reverse();
                poly.AddRange(run);
            }
            else poly.AddRange([new(len, w.Z1), new(0, w.Z1)]);
            var clean = ShellPieces.Clean(poly);
            if (clean.Count >= 3 && Polygon2.SignedArea(clean) > 1e-4 && (clean[0] - poly[0]).Length < 1e-6 && (clean[1] - poly[1]).Length < 1e-6) o.Add(new Face(w, a, dir, len, clean));
        }
        return o;
    }

    /// <summary>The top of a wall's face as a polyline in 3D, from its start to its end.</summary>
    static List<Vec3> TopLine(Face f) => f.Poly.Skip(2).Reverse().Select(f.At).ToList();

    /// <summary>The height of a wall face's top at a station along it.</summary>
    static double TopAt(Face f, double s)
    {
        var top = f.Poly.Skip(2).Reverse().ToList();
        if (top.Count == 0) return f.Wall.Z1;
        if (s <= top[0].X) return top[0].Y;
        for (var k = 0; k + 1 < top.Count; k++)
            if (s <= top[k + 1].X + 1e-12) return top[k + 1].X - top[k].X < 1e-12 ? Math.Max(top[k].Y, top[k + 1].Y) : top[k].Y + (top[k + 1].Y - top[k].Y) * (s - top[k].X) / (top[k + 1].X - top[k].X);
        return top[^1].Y;
    }

    /// <summary>
    /// A bar lying along a wall's top (a roof's wall plate, a ring beam over a wall): moved down onto the top, its own axis kept
    /// as its offset, so the members crossing it bear on the wall.
    /// </summary>
    static void OntoWalls(List<Bar> bars, List<Plate> plates)
    {
        var faces = plates.Where(p => p.IsWall).SelectMany(Faces).ToList();
        foreach (var b in bars.Where(b => !b.Vertical && b.Pts.Count == 2))
        {
            var (p0, p1) = (b.Pts[0], b.Pts[1]);
            var u = (p1 - p0).Normalized();
            foreach (var f in faces)
            {
                var top = TopLine(f);
                if (top.Count < 2) continue;
                var (c0, d0, _) = Closest(p0, top);
                var (c1, d1, _) = Closest(p1, top);
                var (o0, o1) = (p0 - c0, p1 - c1);
                if (d0 <= Tolerance && d1 <= Tolerance) break;
                if ((o0 - o1).Length > 0.02 || (c1 - c0).Length < 0.1 || o0.Z < -0.02 || o0.Z > b.Src.Depth / 2 + 0.1) continue;
                if (Math.Sqrt(o0.X * o0.X + o0.Y * o0.Y) > f.Wall.Src.Thickness / 2 + 0.02) continue;
                if (Vec3.Cross(u, (c1 - c0).Normalized()).Length > 0.02) continue;
                b.Shift = (o0 + o1) * 0.5;
                b.Pts = b.Pts.Select(p => p - b.Shift).ToList();
                break;
            }
        }
    }

    // ------------------------------------------------------------ eccentric connections

    /// <summary>
    /// The joins the axes do not make by themselves: each known connection (a cleat, gusset or fly brace) puts its member's
    /// end or crossing on the host where their axes come closest; any other bar's end that meets nothing goes to the member or
    /// shell it bears on (within the two half sizes and <see cref="EccentricMargin"/>), and bars crossing with an eccentricity
    /// that small are joined at the crossing. The node is on the supporting member's axis (or the shell's plane); the bar keeps
    /// its own end as an offset. Ends on a shell's inside become nodes of it.
    /// </summary>
    static int Connect(List<Bar> bars, List<Plate> plates, IReadOnlyList<AnalyticalLink> links)
    {
        var joins = 0;
        var bySource = bars.GroupBy(b => b.Src.SourceId).ToDictionary(g => g.Key, g => g.ToList());
        var faces = plates.Where(p => p.IsWall).SelectMany(Faces).ToList();
        var slabs = plates.Where(p => !p.IsWall && p.Outline.Count >= 3).ToList();
        void SetEnd(Bar x, int k, Vec3 node, string on)
        {
            var end = x.Pts[k == 0 ? 0 : x.Pts.Count - 1] + x.Shift;
            if (x.EndNode[k] is { } had && (had - end).Length <= (node - end).Length) return;
            if (x.EndNode[k] == null) joins++;
            x.EndNode[k] = node;
            x.EndOn[k] = on;
        }

        // Known connections first.
        foreach (var link in links)
        {
            if (!bySource.TryGetValue(link.HostId, out var hosts) || !bySource.TryGetValue(link.MemberId, out var members)) continue;
            foreach (var x in members)
                foreach (var h in hosts)
                {
                    if (x == h) continue;
                    var (cx, ax, ch, _, d, _, _) = ClosestPair(x.Physical, h.Physical);
                    if (d > x.Half + h.Half + LinkMargin) continue;
                    x.Linked = true;
                    var node = ch - h.Shift;
                    var endReach = Math.Max(h.Half, 0.05) + 0.01;
                    if (ax <= endReach) SetEnd(x, 0, node, link.Source);
                    else if (x.Length - ax <= endReach) SetEnd(x, 1, node, link.Source);
                    else if (!x.Attach.Any(a => (a.At - cx).Length <= Tolerance)) { x.Attach.Add((cx, node, link.Source)); joins++; }
                }
        }

        // Ends on the inside of a shell become its nodes; ends meeting nothing bear on what is next to them.
        foreach (var b in bars)
            for (var k = 0; k < 2; k++)
            {
                if (b.EndNode[k] is { } en) { OnShell(en, faces, slabs); continue; }
                var p = b.Pts[k == 0 ? 0 : b.Pts.Count - 1];
                var lowerFoot = b.Vertical && p.Z <= Math.Min(b.Pts[0].Z, b.Pts[^1].Z) + 1e-9;
                var meets = bars.Any(o => o != b && Closest(p, o.Pts).D <= Tolerance);
                var onShell = OnShell(p, faces, slabs);
                if (meets || onShell || b.Linked || lowerFoot) continue;
                var phys = p + b.Shift;
                (Vec3 Node, double D, string On, bool Shell)? best = null;
                foreach (var o in bars)
                {
                    if (o == b) continue;
                    var (c, d, _) = Closest(phys, o.Physical);
                    if (d <= b.Half + o.Half + EccentricMargin && (best == null || d < best.Value.D)) best = (c - o.Shift, d, o.Src.Name, false);
                }
                if (ShellTarget(phys, b.Half, faces, slabs) is { } st && (best == null || st.D < best.Value.D)) best = (st.Node, st.D, st.On, true);
                if (best is not { } t) continue;
                SetEnd(b, k, t.Node, t.On);
                if (t.Shell) OnShell(t.Node, faces, slabs);
            }

        // Crossings: bars passing each other (or through each other) with no join, both away from their ends.
        var free = bars.Where(b => !b.Linked).ToList();
        var boxes = free.Select(b => { var p = b.Physical; return (Min: new Vec3(p.Min(q => q.X), p.Min(q => q.Y), p.Min(q => q.Z)), Max: new Vec3(p.Max(q => q.X), p.Max(q => q.Y), p.Max(q => q.Z))); }).ToList();
        for (var i = 0; i < free.Count; i++)
            for (var j = i + 1; j < free.Count; j++)
            {
                var (b1, b2) = (free[i], free[j]);
                var reach = b1.Half + b2.Half + EccentricMargin;
                var (m1, m2) = (boxes[i], boxes[j]);
                if (m1.Min.X > m2.Max.X + reach || m2.Min.X > m1.Max.X + reach || m1.Min.Y > m2.Max.Y + reach || m2.Min.Y > m1.Max.Y + reach || m1.Min.Z > m2.Max.Z + reach || m2.Min.Z > m1.Max.Z + reach) continue;
                var (c1, a1, c2, a2, d, u1, u2) = ClosestPair(b1.Physical, b2.Physical);
                if (d > reach || Vec3.Cross(u1, u2).Length < 0.2) continue;
                if (a1 <= b2.Half + 0.02 || b1.Length - a1 <= b2.Half + 0.02 || a2 <= b1.Half + 0.02 || b2.Length - a2 <= b1.Half + 0.02) continue;
                // The bigger member (a column before a beam) carries the other.
                var (s1, s2) = (b1.Src.Depth * b1.Src.Width, b2.Src.Depth * b2.Src.Width);
                var firstHosts = s1 > s2 + 1e-12 || (Math.Abs(s1 - s2) <= 1e-12 && (b1.Vertical || !b2.Vertical));
                var (h, x, ch, cx) = firstHosts ? (b1, b2, c1, c2) : (b2, b1, c2, c1);
                var node = ch - h.Shift;
                x.Attach.Add((d <= Tolerance ? node + x.Shift : cx, node, h.Src.Name));
                joins++;
            }
        // Bars passing over or through a wall's top (a ridge beam over a gable's peak, a purlin through a gable wall): joined to the top there.
        foreach (var b in free)
            foreach (var f in faces)
            {
                var top = TopLine(f);
                if (top.Count < 2) continue;
                var (cx, ax, ct, _, d, ub, ut) = ClosestPair(b.Physical, top);
                var half = f.Wall.Src.Thickness / 2;
                if (d > b.Half + half + EccentricMargin || Vec3.Cross(ub, ut).Length < 0.2) continue;
                if (ax <= half + 0.02 || b.Length - ax <= half + 0.02) continue;
                if (b.Attach.Any(a => (a.At - cx).Length <= Math.Max(b.Half, Tolerance))) continue;
                b.Attach.Add((cx, ct, f.Wall.Src.Name));
                joins++;
            }
        return joins;
    }

    /// <summary>Whether a point lies on a shell (in a wall's plane inside its outline, or in a slab's plane over its outline, within the tolerance); if so it becomes one of the shell's nodes.</summary>
    static bool OnShell(Vec3 p, List<Face> faces, List<Plate> slabs)
    {
        var found = false;
        foreach (var f in faces)
        {
            var s = Vec2.Dot(p.Xy - f.A, f.Dir);
            if (s < -Tolerance || s > f.Len + Tolerance || f.Off(p) > Tolerance) continue;
            if (p.Z < f.Wall.Z0 - Tolerance || p.Z > TopAt(f, Math.Clamp(s, 0, f.Len)) + Tolerance) continue;
            f.Wall.Interior.Add(p);
            found = true;
        }
        foreach (var sl in slabs)
        {
            if (Math.Abs(p.Z - sl.ZAt(p.Xy)) > Tolerance || DistToPolygon(p.Xy, sl.Outline) > Tolerance) continue;
            sl.Interior.Add(p);
            found = true;
        }
        return found;
    }

    /// <summary>The shell an end bears on (its point in the shell's plane), within the shell's half thickness, the bar's and the margin.</summary>
    static (Vec3 Node, double D, string On)? ShellTarget(Vec3 p, double half, List<Face> faces, List<Plate> slabs)
    {
        (Vec3, double, string)? best = null;
        foreach (var f in faces)
        {
            var s = Vec2.Dot(p.Xy - f.A, f.Dir);
            if (s < -0.02 || s > f.Len + 0.02 || f.Off(p) > f.Wall.Src.Thickness / 2 + half + EccentricMargin) continue;
            var sc = Math.Clamp(s, 0, f.Len);
            var zc = Math.Clamp(p.Z, f.Wall.Z0, TopAt(f, sc));
            if (Math.Abs(p.Z - zc) > half + EccentricMargin) continue;
            var target = (f.A + f.Dir * sc).At(zc);
            var d = (p - target).Length;
            if (best == null || d < best.Value.Item2) best = (target, d, f.Wall.Src.Name);
        }
        foreach (var sl in slabs)
        {
            if (DistToPolygon(p.Xy, sl.Outline) > 0.02) continue;
            var z = sl.ZAt(p.Xy);
            var d = Math.Abs(p.Z - z);
            if (d > sl.HalfV + half + EccentricMargin) continue;
            if (best == null || d < best.Value.Item2) best = (p.Xy.At(z), d, sl.Src.Name);
        }
        return best;
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

    void Assemble(List<Bar> bars, List<Plate> plates, IReadOnlyList<AnalyticalSupport> supports, IModelView m, AnalyticalOptions options)
    {
        // Every bar's points (an eccentric end's node instead of its own), every eccentric crossing's node.
        foreach (var b in bars)
        {
            for (var i = 0; i < b.Pts.Count; i++)
                Node(i == 0 && b.EndNode[0] is { } e0 ? e0 : i == b.Pts.Count - 1 && b.EndNode[1] is { } e1 ? e1 : b.Pts[i]);
            foreach (var at in b.Attach) Node(at.Node);
        }

        // The lines shells are split along: bars' axes, walls' feet and tops, slabs' edges.
        var segments = new List<(Vec3 A, Vec3 B)>();
        foreach (var b in bars.Where(b => !b.Vertical)) for (var i = 0; i + 1 < b.Pts.Count; i++) segments.Add((b.Pts[i], b.Pts[i + 1]));
        var faces = plates.Where(p => p.IsWall).ToDictionary(p => p, Faces);
        foreach (var f in faces.Values.SelectMany(x => x))
        {
            segments.Add((f.At(f.Poly[0]), f.At(f.Poly[1])));
            var top = TopLine(f);
            for (var i = 0; i + 1 < top.Count; i++) segments.Add((top[i], top[i + 1]));
        }
        foreach (var s in plates.Where(p => !p.IsWall && p.Outline.Count >= 3))
            for (var i = 0; i < s.Outline.Count; i++) { var (a, c) = (s.Outline[i], s.Outline[(i + 1) % s.Outline.Count]); segments.Add((a.At(s.ZAt(a)), c.At(s.ZAt(c)))); }

        var pieces3 = new List<(AnalyticalShell Src, List<Vec3> Corners)>();
        foreach (var s in plates.Where(p => !p.IsWall))
        {
            if (s.Outline.Count < 3) { LeftOut.Add(new(s.Src.SourceId, s.Src.Name, s.Src.IfcClass, "no outline")); continue; }
            var lines = segments.Where(l => Math.Abs(l.A.Z - s.ZAt(l.A.Xy)) <= 0.02 && Math.Abs(l.B.Z - s.ZAt(l.B.Xy)) <= 0.02 && (l.B.Xy - l.A.Xy).Length > Tolerance)
                .Select(l => (l.A.Xy, l.B.Xy)).ToList();
            var outline = Polygon2.EnsureCcw(s.Outline).ToList();
            var longest = Enumerable.Range(0, outline.Count).MaxBy(i => (outline[(i + 1) % outline.Count] - outline[i]).Length);
            var along = (outline[(longest + 1) % outline.Count] - outline[longest]).Normalized();
            var pieces = ShellPieces.Pieces(outline, lines, s.Interior.Select(p => p.Xy).ToList(), along, s.Src.Holes, Tolerance, MinPieceWidth);
            foreach (var piece in pieces) pieces3.Add((s.Src, piece.Select(v => v.At(s.ZAt(v))).ToList()));
        }

        // Walls: each face split where another wall ends on it (a T), a column stands in it, a slab's edge or a beam runs in
        // its plane, at the nodes bars put on it, and round its openings.
        var columns = bars.Where(b => b.Vertical).ToList();
        foreach (var (w, wf) in faces)
        {
            if (wf.Count == 0) { LeftOut.Add(new(w.Src.SourceId, w.Src.Name, w.Src.IfcClass, "no length or height")); continue; }
            foreach (var f in wf)
            {
                var (zLo, zHi) = (f.Poly.Min(p => p.Y) - 1, f.Poly.Max(p => p.Y) + 1);
                var lines = new List<(Vec2, Vec2)>();
                void Station(Vec2 p, double z0, double z1)
                {
                    if (z1 < w.Z0 + Tolerance || z0 > w.Z1 - Tolerance || f.Off(p.At(0)) > Tolerance) return;
                    var t = Vec2.Dot(p - f.A, f.Dir);
                    if (t > Tolerance && t < f.Len - Tolerance) lines.Add((new Vec2(t, zLo), new Vec2(t, zHi)));
                }
                foreach (var o in plates.Where(o => o != w && o.IsWall && o.Line.Count >= 2)) { Station(o.Line[0], o.Z0, o.Z1); Station(o.Line[^1], o.Z0, o.Z1); }
                foreach (var c in columns) Station(c.Pts[0].Xy, Math.Min(c.Pts[0].Z, c.Pts[1].Z), Math.Max(c.Pts[0].Z, c.Pts[1].Z));
                var band = w.Src.Thickness / 2 + 0.02;
                foreach (var (a, b) in segments)
                    if (f.Off(a) <= band && f.Off(b) <= band && (b.Xy - a.Xy).Length > Tolerance)
                    {
                        var (la, lb) = (f.Local(a), f.Local(b));
                        if ((lb - la).Length > Tolerance) lines.Add((la, lb));
                    }
                var points = w.Interior.Where(p => f.Off(p) <= band && Vec2.Dot(p.Xy - f.A, f.Dir) is var s && s > -Tolerance && s < f.Len + Tolerance).Select(f.Local).ToList();
                var holes = new List<IReadOnlyList<Vec2>>();
                foreach (var op in w.Src.Openings)
                {
                    if (Math.Abs(Vec2.Cross(op.Dir, f.Dir)) > 0.05) continue;
                    var sc = Vec2.Dot(op.Centre - f.A, f.Dir);
                    if (sc + op.HalfWidth < Tolerance || sc - op.HalfWidth > f.Len - Tolerance || Math.Abs(Vec2.Cross(f.Dir, op.Centre - f.A)) > w.Src.Thickness + 0.5) continue;
                    var (s0, s1) = (sc - op.HalfWidth, sc + op.HalfWidth);
                    var (z0, z1) = (op.Bottom, op.Top);
                    // An opening within a few centimetres of the wall's foot or ends opens it there (no sliver of wall under a door).
                    if (z0 - w.Z0 < 0.05) z0 = w.Z0 - 1;
                    if (s0 < 0.05) s0 = -1;
                    if (s1 > f.Len - 0.05) s1 = f.Len + 1;
                    holes.Add([new(s0, z0), new(s1, z0), new(s1, z1), new(s0, z1)]);
                }
                var pieces = ShellPieces.Pieces(f.Poly, lines, points, new Vec2(0, 1), holes, Tolerance, MinPieceWidth);
                foreach (var piece in pieces) pieces3.Add((w.Src, piece.Select(f.At).ToList()));
            }
        }
        foreach (var (_, corners) in pieces3) foreach (var c in corners) Node(c);

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
        var shells = pieces3.Select(x => (x.Src, Ring: Ring(x.Corners))).ToList();
        foreach (var g in shells.GroupBy(x => x.Src.SourceId))
        {
            var list = g.Where(x => x.Ring.Count >= 3).ToList();
            for (var k = 0; k < list.Count; k++)
                Shells.Add(new AShell(list.Count == 1 ? g.Key : $"{g.Key}#{k + 1}", list[k].Src, k + 1, list.Count, list[k].Ring));
        }

        // The bars, split at every node on their span and at their eccentric crossings.
        var made = new List<(Bar Bar, ANode I, ANode J, Vec3 OffI, Vec3 OffJ, string? On)>();
        foreach (var b in bars)
        {
            var chain = new List<(ANode N, Vec3 Off, string? On)>();
            void Add(ANode n, Vec3 off, string? on)
            {
                if (off.Length < 1e-6) off = default;
                if (chain.Count > 0 && chain[^1].N == n) return;
                chain.Add((n, off, on));
            }
            for (var i = 0; i + 1 < b.Pts.Count; i++)
            {
                var (p, q) = (b.Pts[i], b.Pts[i + 1]);
                if (i == 0)
                {
                    var n0 = b.EndNode[0] is { } e0 ? Node(e0) : Node(p);
                    Add(n0, p + b.Shift - n0.At, b.EndOn[0]);
                }
                var ab = q - p;
                var l2 = Vec3.Dot(ab, ab);
                var stops = On(p, q).Select(n => (T: Vec3.Dot(n.At - p, ab), N: n, Off: b.Shift, On: (string?)null)).ToList();
                foreach (var (at, node, on) in b.Attach)
                {
                    var a = at - b.Shift;
                    var t = l2 > 0 ? Vec3.Dot(a - p, ab) / l2 : 0;
                    if (t <= 1e-9 || t >= 1 - 1e-9 || (p + ab * t - a).Length > Tolerance) continue;
                    var n = Node(node);
                    stops.Add((t * l2, n, at - n.At, on));
                }
                foreach (var s in stops.OrderBy(s => s.T)) Add(s.N, s.Off, s.On);
                if (i + 1 == b.Pts.Count - 1)
                {
                    var n1 = b.EndNode[1] is { } e1 ? Node(e1) : Node(q);
                    Add(n1, q + b.Shift - n1.At, b.EndOn[1]);
                }
                else Add(Node(q), b.Shift, null);
            }
            var before = made.Count;
            for (var i = 0; i + 1 < chain.Count; i++)
                made.Add((b, chain[i].N, chain[i + 1].N, chain[i].Off, chain[i + 1].Off, chain[i].On ?? chain[i + 1].On));
            if (made.Count == before) LeftOut.Add(new(b.Src.SourceId, b.Src.Name, b.Src.IfcClass, "shorter than the node tolerance"));
        }
        foreach (var g in made.GroupBy(x => x.Bar.Src.SourceId))
        {
            var list = g.ToList();
            for (var k = 0; k < list.Count; k++)
            {
                var (b, i, j, oi, oj, on) = list[k];
                var frame = new AFrame(list.Count == 1 ? g.Key : $"{g.Key}#{k + 1}", b.Src, k + 1, list.Count, i, j, Rotation(i.At + oi, j.At + oj, b.Src.DepthDir), b.OffsetZ)
                { OffI = oi, OffJ = oj, Connection = oi.Length > 0 || oj.Length > 0 ? on : null };
                Frames.Add(frame);
                _barOf[frame] = b;
            }
        }

        ApplySupports(supports, options);
        AssignStoreys(m);
        NameNodes();
        FindFree();
    }

    readonly Dictionary<AFrame, Bar> _barOf = [];

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
        return Normalize(angle);
    }

    /// <summary>
    /// Where a bar's centroidal axis is from its line (metres, global): an unsymmetric section's (a T, a channel, an angle)
    /// centroid off its box centre, through which the analytical line runs. In the section's drawing x lies along −local 3
    /// (+local 3 for a mirrored member) and y along local 2, of the bar's geometric axes (docs/jg-structures.md); zero for a
    /// section symmetric about both axes or without properties.
    /// </summary>
    public static Vec3 CentroidOffset(AFrame f) => CentroidOffset(f, SectionProperties.Of(f.Source.Section.Shape));

    public static Vec3 CentroidOffset(AFrame f, SectionProps? props)
    {
        if (props is not { } p || p.Centroid.Length <= 1e-9) return Vec3.Zero;
        var (pi, pj) = (f.I.At + f.OffI, f.J.At + f.OffJ);
        var u = (pj - pi).Normalized();
        var v0 = Reference(u);
        var t = f.RotationDeg * Math.PI / 180;
        var l2 = (v0 * Math.Cos(t) + Vec3.Cross(u, v0) * Math.Sin(t)).Normalized();
        var l3 = Vec3.Cross(u, l2);
        return l2 * p.Centroid.Y + l3 * (f.Source.Mirrored ? p.Centroid.X : -p.Centroid.X);
    }

    /// <summary>An angle in degrees in (−180, 180], rounded to a millionth.</summary>
    public static double Normalize(double angle)
    {
        angle = Math.Round(angle, 6) % 360;
        if (angle <= -180) angle += 360;
        if (angle > 180) angle -= 360;
        return angle == -0.0 ? 0 : angle;
    }

    /// <summary>Local 2 before turning: +X for a vertical bar (within 0.001 rad), else the upward perpendicular to local 1 in its vertical plane.</summary>
    public static Vec3 Reference(Vec3 u)
    {
        if (Math.Sqrt(u.X * u.X + u.Y * u.Y) < 1e-3) return new Vec3(1, 0, 0);
        return (Vec3.UnitZ - u * u.Z).Normalized();
    }

    void ApplySupports(IReadOnlyList<AnalyticalSupport> supports, AnalyticalOptions options)
    {
        // The feet: a column's lower end, a wall's lowest nodes, each with the element it is the foot of.
        var owners = new Dictionary<ANode, HashSet<string>>();
        void Foot(ANode n, string id) { if (!owners.TryGetValue(n, out var set)) owners[n] = set = []; set.Add(id); }
        foreach (var f in Frames.Where(f => f.Source.Kind == "column" && f.Piece == 1)) Foot(f.I.At.Z <= f.J.At.Z ? f.I : f.J, f.Source.SourceId);
        foreach (var g in Shells.Where(s => s.Source.Kind == "wall").GroupBy(s => s.Source.SourceId))
        {
            var z0 = g.Min(s => s.Nodes.Min(n => n.At.Z));
            foreach (var s in g) foreach (var n in s.Nodes.Where(n => Math.Abs(n.At.Z - z0) <= Tolerance)) Foot(n, s.Source.SourceId);
        }
        var feet = owners.Keys.ToHashSet();
        var pinned = options.Footings == "pinned";
        foreach (var sup in supports.Where(s => s.ForFrame == null))
        {
            bool Over(Vec2 p) => sup.Region is { Count: >= 3 } region ? DistToPolygon(p, region) <= 0.05
                : sup.LineA is { } a && sup.LineB is { } b && DistToSegment(p, a, b) <= sup.Width / 2 + 0.05;
            var hits = feet.Where(n => Over(n.At.Xy) && n.At.Z >= sup.Bottom - 0.05 && n.At.Z <= sup.Top + 2.0).ToList();
            if (hits.Count == 0) continue;
            // As the export asks: fixed (cast with the footing, the default) or pinned.
            var spec = pinned && sup.Spec.Kind == "fixed" ? SupportSpec.Pinned(sup.Spec.Source.Replace("fixed, cast with what stands on it", "pinned, as the export asks")) : sup.Spec;
            foreach (var g in hits.GroupBy(n => (Math.Round(n.At.X / 0.05), Math.Round(n.At.Y / 0.05))))
            {
                var low = g.Min(n => n.At.Z);
                foreach (var n in g.Where(n => n.At.Z <= low + Tolerance && n.Restraints == null)) Hold(n, spec);
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
    /// The bar ends nothing else meets, each a cantilever's tip unless a member it is not joined to passes within reach (then
    /// an end Lapin could not connect); and the parts of the model no support holds.
    /// </summary>
    void FindFree()
    {
        var uses = new Dictionary<ANode, int>();
        foreach (var f in Frames) { uses[f.I] = uses.GetValueOrDefault(f.I) + 1; uses[f.J] = uses.GetValueOrDefault(f.J) + 1; }
        foreach (var s in Shells) foreach (var n in s.Nodes) uses[n] = uses.GetValueOrDefault(n) + 1;
        var piecesOf = _barOf.GroupBy(x => x.Value).ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList());
        var bars = piecesOf.Keys.ToList();
        foreach (var f in Frames)
            foreach (var (n, off) in new[] { (f.I, f.OffI), (f.J, f.OffJ) })
            {
                if (uses[n] != 1 || n.Restraints != null || FreeBases.Contains(n)) continue;
                var bar = _barOf[f];
                var mine = piecesOf[bar].SelectMany(x => new[] { x.I, x.J }).ToHashSet();
                var p = n.At + off;
                var near = bars.Where(o => o != bar && piecesOf[o].All(x => !mine.Contains(x.I) && !mine.Contains(x.J)))
                    .Where(o => Closest(p, o.Physical).D <= bar.Half + o.Half + EccentricMargin).Select(o => o.Src.Name).FirstOrDefault();
                FreeEnds.Add(new FreeEnd(f, n, near == null, near));
            }
        // Parts no support holds: the members and shells joined by their nodes.
        if (!Nodes.Any(n => n.Restraints != null)) return;
        var parent = Nodes.ToDictionary(n => n, n => n);
        ANode Find(ANode x)
        {
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }
        void Union(ANode a, ANode b) { var (ra, rb) = (Find(a), Find(b)); if (ra != rb) parent[ra] = rb; }
        foreach (var f in Frames) Union(f.I, f.J);
        foreach (var s in Shells) for (var i = 1; i < s.Nodes.Count; i++) Union(s.Nodes[0], s.Nodes[i]);
        var held = Nodes.Where(n => n.Restraints != null).Select(Find).ToHashSet();
        var parts = Frames.Select(f => (Root: Find(f.I), Name: f.Source.Name)).Concat(Shells.Select(s => (Root: Find(s.Nodes[0]), Name: s.Source.Name)))
            .Where(x => !held.Contains(x.Root)).GroupBy(x => x.Root);
        foreach (var g in parts) Floating.Add(g.Select(x => x.Name).Distinct().ToList());
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
