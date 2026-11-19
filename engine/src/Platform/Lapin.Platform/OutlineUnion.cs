using Lapin.Geometry;

namespace Lapin.Platform;

/// <summary>
/// The union of closed outlines with arcs (<see cref="ArcOutline"/>: corners and each edge's rise), as a merge of terrains
/// needs it: pieces that touch along a cut come back as the outline they were cut from, pieces that overlap as the outline
/// round both, pieces apart as several outlines. Each outline is taken counter-clockwise; every straight edge is broken where
/// another outline's edge crosses or touches it; an edge is kept when it lies outside every other outline, an edge two of them
/// share running opposite ways (the cut between two pieces) goes, one running the same way is kept once. What is kept is
/// walked into loops, and corners left in the middle of a straight run (where the cut met the outline) are dropped. An arc
/// that no other outline touches stays an arc; one that is touched is taken as its chords (the one rule, <see cref="Chords"/>).
/// </summary>
public static class OutlineUnion
{
    const double Eps = 1e-6;

    public sealed record Loop(List<Vec2> Corners, double[]? Rises);

    /// <summary>The loops round what the outlines cover, counter-clockwise (<see cref="Outers"/>), and those round gaps they enclose between them, clockwise (<see cref="Holes"/>).</summary>
    public sealed record Result(List<Loop> Outers, List<Loop> Holes)
    {
        /// <summary>The one outline, when the union is one piece with no gap; else null.</summary>
        public Loop? Single => Outers.Count == 1 && Holes.Count == 0 ? Outers[0] : null;
    }

    sealed record Edge(int From, int To, double Rise, int Owner);

    /// <summary>The union of <paramref name="loops"/>; null when the edges do not walk into closed loops (outlines that cross themselves).</summary>
    public static Result? Of(IReadOnlyList<Loop> loops)
    {
        // Counter-clockwise, and each one's built outline (its arcs in chords) to tell inside from outside.
        var ccw = new List<Loop>();
        foreach (var l in loops)
        {
            if (l.Corners.Count < 3) continue;
            var area = ArcOutline.SignedArea(l.Corners, l.Rises);
            if (Math.Abs(area) < 1e-9) continue;
            if (area > 0) { ccw.Add(l); continue; }
            var c = l.Corners.ToList();
            c.Reverse();
            ccw.Add(new Loop(c, ArcOutline.Reversed(l.Rises)));
        }
        if (ccw.Count == 0) return new Result([], []);
        var built = ccw.Select(l => ArcOutline.Points(l.Corners, l.Rises)).ToList();

        // Each loop's edges: an arc no other outline comes near stays whole; any other arc goes in its chords.
        var segs = new List<List<(Vec2 A, Vec2 B, double Rise)>>();
        for (var i = 0; i < ccw.Count; i++)
        {
            var (c, r) = (ccw[i].Corners, ccw[i].Rises);
            var list = new List<(Vec2, Vec2, double)>();
            for (var k = 0; k < c.Count; k++)
            {
                var e = ArcOutline.Edge(c, r, k);
                if (!e.IsArc) { list.Add((e.Start, e.End, 0)); continue; }
                var chords = e.Stations(0, e.Length).Select(x => e.PointAt(x)).ToList();
                chords[0] = e.Start; chords[^1] = e.End;
                // Touching another outline only at its ends, it stays an arc (the other's edge is broken there).
                var touched = Enumerable.Range(0, ccw.Count).Where(j => j != i).Any(j => Near(chords, built[j], e.Start, e.End));
                if (!touched) { list.Add((e.Start, e.End, e.Rise)); continue; }
                for (var s = 0; s + 1 < chords.Count; s++) list.Add((chords[s], chords[s + 1], 0));
            }
            segs.Add(list);
        }

        // Every straight edge broken where another loop's straight edge crosses it or ends on it.
        var points = new List<Vec2>();
        int Id(Vec2 p)
        {
            for (var i = 0; i < points.Count; i++) if ((points[i] - p).Length <= Eps) return i;
            points.Add(p);
            return points.Count - 1;
        }
        var edges = new List<Edge>();
        for (var i = 0; i < segs.Count; i++)
            foreach (var (a, b, rise) in segs[i])
            {
                if (rise != 0) { var (fa, fb) = (Id(a), Id(b)); if (fa != fb) edges.Add(new Edge(fa, fb, rise, i)); continue; }
                var len = (b - a).Length;
                if (len <= Eps) continue;
                var dir = (b - a) * (1 / len);
                var at = new List<double> { 0, len };
                for (var j = 0; j < segs.Count; j++)
                {
                    if (j == i) continue;
                    foreach (var (p, q, r) in segs[j])
                    {
                        foreach (var x in new[] { p, q })
                            if (Polyline2.SegmentDistance(x, a, b) <= Eps) at.Add(Vec2.Dot(x - a, dir));
                        if (r == 0 && Crossing(a, b, p, q) is { } y) at.Add(Vec2.Dot(y - a, dir));
                    }
                }
                var stations = at.Select(s => Math.Clamp(s, 0, len)).OrderBy(s => s).ToList();
                var prev = Id(a);
                for (var k = 1; k < stations.Count; k++)
                {
                    var next = k == stations.Count - 1 ? Id(b) : Id(a + dir * stations[k]);
                    if (next != prev) edges.Add(new Edge(prev, next, 0, i));
                    prev = next;
                }
            }

        // Kept: outside every other loop; a shared edge running the other way goes, one running the same way stays once.
        bool SameRise(double x, double y) => Math.Abs(x - y) <= Eps;
        var kept = new List<Edge>();
        foreach (var e in edges)
        {
            var others = edges.Where(o => o.Owner != e.Owner).ToList();
            if (others.Any(o => o.From == e.To && o.To == e.From && SameRise(o.Rise, -e.Rise))) continue;
            var same = others.Where(o => o.From == e.From && o.To == e.To && SameRise(o.Rise, e.Rise)).ToList();
            if (same.Count > 0) { if (same.All(o => o.Owner > e.Owner)) kept.Add(e); continue; }
            var curve = new PathCurve(points[e.From], points[e.To], e.Rise);
            var mid = curve.PointAt(curve.Length / 2);
            if (Enumerable.Range(0, ccw.Count).Any(j => j != e.Owner && Polygon2.Contains(built[j], mid))) continue;
            kept.Add(e);
        }

        // Corners that may go where they end up in a straight run: where an edge was dropped (a cut's ends), or none of the
        // outlines had a corner (where another's edge broke an edge). A corner of an outline itself stays, even in line.
        var corners = new HashSet<int>(ccw.SelectMany(l => l.Corners).Select(Id));
        var keptSet = new HashSet<Edge>(kept, ReferenceEqualityComparer.Instance);
        var loose = new HashSet<int>(Enumerable.Range(0, points.Count).Where(i => !corners.Contains(i)));
        foreach (var e in edges.Where(e => !keptSet.Contains(e))) { loose.Add(e.From); loose.Add(e.To); }

        // Walked into loops; where two leave the same corner, the one turning right the most (the outline stays outside).
        var outgoing = kept.GroupBy(e => e.From).ToDictionary(g => g.Key, g => g.ToList());
        var used = new HashSet<Edge>(ReferenceEqualityComparer.Instance);
        var outers = new List<Loop>();
        var holes = new List<Loop>();
        foreach (var start in kept)
        {
            if (used.Contains(start)) continue;
            var walk = new List<Edge>();
            var e = start;
            while (true)
            {
                used.Add(e);
                walk.Add(e);
                if (e.To == start.From) break;
                var dirIn = Tangent(e, points, atEnd: true);
                var next = outgoing.GetValueOrDefault(e.To)?.Where(o => !used.Contains(o))
                    .OrderBy(o => { var d = Tangent(o, points, atEnd: false); return Math.Atan2(Vec2.Cross(dirIn, d), Vec2.Dot(dirIn, d)); })
                    .FirstOrDefault();
                if (next == null) return null;
                e = next;
            }
            var loop = Clean(walk, points, loose);
            if (loop.Corners.Count < 3 && (loop.Rises == null || loop.Corners.Count < 2)) continue;
            var area = ArcOutline.SignedArea(loop.Corners, loop.Rises);
            if (Math.Abs(area) < 1e-9) continue;
            (area > 0 ? outers : holes).Add(loop);
        }
        return new Result(outers, holes);
    }

    /// <summary>The direction a kept edge leaves its start (or arrives at its end) along.</summary>
    static Vec2 Tangent(Edge e, List<Vec2> points, bool atEnd)
    {
        var c = new PathCurve(points[e.From], points[e.To], e.Rise);
        var l = c.Length;
        var (s0, s1) = atEnd ? (Math.Max(0, l - Math.Min(1e-3, l / 4)), l) : (0, Math.Min(1e-3, l / 4));
        return (c.PointAt(s1) - c.PointAt(s0)).Normalized();
    }

    /// <summary>A walked loop as corners and rises, the <paramref name="loose"/> corners in the middle of a straight run dropped.</summary>
    static Loop Clean(List<Edge> walk, List<Vec2> points, HashSet<int> loose)
    {
        var ids = walk.Select(e => e.From).ToList();
        var c = ids.Select(i => points[i]).ToList();
        var r = walk.Select(e => e.Rise).ToList();
        for (var changed = true; changed && c.Count > 3;)
        {
            changed = false;
            for (var i = 0; i < c.Count && c.Count > 3; i++)
            {
                var prev = (i + c.Count - 1) % c.Count;
                if (r[prev] != 0 || r[i] != 0 || !loose.Contains(ids[i])) continue;
                var (a, b, d) = (c[prev], c[i], c[(i + 1) % c.Count]);
                if (Polyline2.SegmentDistance(b, a, d) > Eps) continue;
                c.RemoveAt(i); r.RemoveAt(i); ids.RemoveAt(i);
                changed = true;
                break;
            }
        }
        return new Loop(c, r.Any(x => x != 0) ? [.. r] : null);
    }

    /// <summary>Whether a run of chords comes within <see cref="Eps"/> of a closed outline anywhere but at its two ends.</summary>
    static bool Near(List<Vec2> run, IReadOnlyList<Vec2> ring, Vec2 start, Vec2 end)
    {
        bool AtEnd(Vec2 x) => (x - start).Length <= Eps || (x - end).Length <= Eps;
        for (var s = 0; s + 1 < run.Count; s++)
            for (var k = 0; k < ring.Count; k++)
            {
                var (p, q) = (ring[k], ring[(k + 1) % ring.Count]);
                if (Crossing(run[s], run[s + 1], p, q) != null) return true;
                if (!AtEnd(p) && Polyline2.SegmentDistance(p, run[s], run[s + 1]) <= Eps) return true;
                if (s > 0 && Polyline2.SegmentDistance(run[s], p, q) <= Eps) return true;
            }
        return false;
    }

    /// <summary>Where segment a–b crosses segment p–q, both inside; null when they are parallel or miss.</summary>
    static Vec2? Crossing(Vec2 a, Vec2 b, Vec2 p, Vec2 q)
    {
        var (d, e) = (b - a, q - p);
        var den = Vec2.Cross(d, e);
        if (Math.Abs(den) < 1e-12 * Math.Max(1, d.Length * e.Length)) return null;
        var t = Vec2.Cross(p - a, e) / den;
        var u = Vec2.Cross(p - a, d) / den;
        return t > 0 && t < 1 && u > 0 && u < 1 ? a + d * t : null;
    }
}
