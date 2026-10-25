using Lapin.Geometry;

namespace Lapin.Drafting.Cad;

/// <summary>The drafting edits (ported from the old Lapin's offset, trim, extend and fillet, same behaviour).</summary>
public static class Ops
{
    // ------------------------------------------------------------ offset

    /// <summary>A shape offset by <paramref name="d"/> toward <paramref name="side"/>; null when it would collapse.</summary>
    public static Shape? Offset(Shape s, double d, Vec2 side)
    {
        switch (s)
        {
            case Line l:
            {
                var n = Geo.Norm(l.B - l.A).Perp;
                var sign = Vec2.Cross(l.B - l.A, side - l.A) >= 0 ? 1 : -1;
                return new Line(l.A + n * (d * sign), l.B + n * (d * sign));
            }
            case Circle c:
            {
                var r = (side - c.C).Length > c.R ? c.R + d : c.R - d;
                return r > 1e-9 ? new Circle(c.C, r) : null;
            }
            case Arc a:
            {
                var r = (side - a.C).Length > a.R ? a.R + d : a.R - d;
                return r > 1e-9 ? a with { R = r } : null;
            }
            case Polyline p:
            {
                var pts = Clean(p.Pts, p.Closed);
                if (pts.Count < 2) return null;
                double sign;
                if (p.Closed)
                {
                    var inside = Polygon2.Contains(pts, side);
                    var ccw = Polygon2.SignedArea(pts) > 0;
                    // The interior lies left of a counter-clockwise loop.
                    sign = inside == ccw ? 1 : -1;
                }
                else
                {
                    var segs = Enumerable.Range(0, pts.Count - 1).Select(i => (pts[i], pts[i + 1])).ToList();
                    var (a, b) = segs.MinBy(x => Geo.NearestOnSegment(side, x.Item1, x.Item2).Dist);
                    sign = Vec2.Cross(b - a, side - a) >= 0 ? 1 : -1;
                }
                var off = OffsetPath(pts, p.Closed, d * sign);
                return off == null ? null : new Polyline(off, p.Closed);
            }
        }
        return null;
    }

    /// <summary>Consecutive duplicate points dropped (and the closing one of a loop).</summary>
    static List<Vec2> Clean(IReadOnlyList<Vec2> pts, bool closed)
    {
        var o = new List<Vec2>();
        foreach (var q in pts) if (o.Count == 0 || (q - o[^1]).Length > 1e-9) o.Add(q);
        if (closed && o.Count > 1 && (o[0] - o[^1]).Length <= 1e-9) o.RemoveAt(o.Count - 1);
        return o;
    }

    /// <summary>A path offset to its left by <paramref name="d"/> (right when negative), its corners mitred.</summary>
    static List<Vec2>? OffsetPath(List<Vec2> pts, bool closed, double d)
    {
        var n = pts.Count;
        var segCount = closed ? n : n - 1;
        var lines = new List<(Vec2 A, Vec2 B)>();
        for (var i = 0; i < segCount; i++)
        {
            var (a, b) = (pts[i], pts[(i + 1) % n]);
            var nrm = Geo.Norm(b - a).Perp * d;
            lines.Add((a + nrm, b + nrm));
        }
        Vec2 Join((Vec2 A, Vec2 B) l1, (Vec2 A, Vec2 B) l2)
        {
            var (d1, d2) = (l1.B - l1.A, l2.B - l2.A);
            var den = Vec2.Cross(d1, d2);
            if (Math.Abs(den) < 1e-12 * Math.Max(d1.Length * d2.Length, 1e-18)) return l1.B; // collinear: no corner
            var t = Vec2.Cross(l2.A - l1.A, d2) / den;
            return l1.A + d1 * t;
        }
        var o = new List<Vec2>();
        if (closed)
            for (var i = 0; i < n; i++) o.Add(Join(lines[(i - 1 + n) % n], lines[i]));
        else
        {
            o.Add(lines[0].A);
            for (var i = 1; i < n - 1; i++) o.Add(Join(lines[i - 1], lines[i]));
            o.Add(lines[^1].B);
        }
        return o.All(q => double.IsFinite(q.X) && double.IsFinite(q.Y)) ? o : null;
    }

    // ------------------------------------------------------------ trim

    /// <summary>
    /// The shape cut where the others cross it, without the piece the pick lies
    /// on: the pieces left (a loop needs two cuts), or null when nothing crosses.
    /// </summary>
    public static List<Shape>? Trim(Shape target, IEnumerable<Shape> cutters, Vec2 pick)
    {
        var others = cutters.Where(c => !ReferenceEquals(c, target) && !c.Equals(target)).ToList();
        switch (target)
        {
            case Line l:
            {
                var ts = Params(others, target, p => T(l.A, l.B, p)).Where(t => t > 1e-9 && t < 1 - 1e-9).ToList();
                if (ts.Count == 0) return null;
                var pieces = Pieces(ts, 0, 1, T(l.A, l.B, Geo.NearestOnSegment(pick, l.A, l.B).Point));
                return pieces.Select(x => (Shape)new Line(l.A + (l.B - l.A) * x.From, l.A + (l.B - l.A) * x.To)).ToList();
            }
            case Polyline p when !p.Closed:
            {
                var pts = p.Pts;
                var ts = Params(others, target, q => PathParam(pts, false, q)).Where(t => t > 1e-9 && t < pts.Count - 1 - 1e-9).ToList();
                if (ts.Count == 0) return null;
                var pieces = Pieces(ts, 0, pts.Count - 1, PathParam(pts, false, pick));
                return pieces.Select(x => (Shape)new Polyline(SubPath(pts, false, x.From, x.To), false)).ToList();
            }
            case Polyline p:
            {
                var pts = p.Pts;
                var n = pts.Count;
                var ts = Params(others, target, q => PathParam(pts, true, q)).Select(t => t >= n - 1e-9 ? 0 : t).Distinct(new Near()).OrderBy(t => t).ToList();
                if (ts.Count < 2) return null;
                return CyclicPieces(ts, n, PathParam(pts, true, pick)).Select(x => (Shape)new Polyline(SubPath(pts, true, x.From, x.To), false)).ToList();
            }
            case Circle c:
            {
                var ts = Params(others, target, q => Geo.NormAngle(Math.Atan2(q.Y - c.C.Y, q.X - c.C.X))).Distinct(new Near()).OrderBy(t => t).ToList();
                if (ts.Count < 2) return null;
                var at = Geo.NormAngle(Math.Atan2(pick.Y - c.C.Y, pick.X - c.C.X));
                return CyclicPieces(ts, 2 * Math.PI, at).Select(x => (Shape)new Arc(c.C, c.R, Geo.NormAngle(x.From), Geo.NormAngle(x.To))).ToList();
            }
            case Arc a:
            {
                double Rel(Vec2 q) => Geo.NormAngle(Math.Atan2(q.Y - a.C.Y, q.X - a.C.X) - a.Start);
                var sweep = a.Sweep;
                var ts = Params(others, target, Rel).Where(t => t > 1e-9 && t < sweep - 1e-9).ToList();
                if (ts.Count == 0) return null;
                var pt = Rel(pick);
                if (pt > sweep) pt = pt - sweep < 2 * Math.PI - pt ? sweep : 0;
                return Pieces(ts, 0, sweep, pt).Select(x => (Shape)new Arc(a.C, a.R, Geo.NormAngle(a.Start + x.From), Geo.NormAngle(a.Start + x.To))).ToList();
            }
        }
        return null;
    }

    sealed class Near : IEqualityComparer<double>
    {
        public bool Equals(double a, double b) => Math.Abs(a - b) < 1e-9;
        public int GetHashCode(double a) => 0;
    }

    static double T(Vec2 a, Vec2 b, Vec2 p) { var d = b - a; return Vec2.Dot(p - a, d) / Math.Max(Vec2.Dot(d, d), 1e-18); }

    static List<double> Params(List<Shape> others, Shape target, Func<Vec2, double> param) =>
        others.SelectMany(o => Geo.Intersect(target, o)).Select(param).Distinct(new Near()).OrderBy(t => t).ToList();

    /// <summary>Where a point lies along a path: the segment's index plus how far along it.</summary>
    static double PathParam(IReadOnlyList<Vec2> pts, bool closed, Vec2 p)
    {
        var n = closed ? pts.Count : pts.Count - 1;
        var best = (Dist: double.PositiveInfinity, S: 0.0);
        for (var i = 0; i < n; i++)
        {
            var (a, b) = (pts[i], pts[(i + 1) % pts.Count]);
            var (q, dist) = Geo.NearestOnSegment(p, a, b);
            if (dist < best.Dist - 1e-12) best = (dist, i + T(a, b, q));
        }
        return best.S;
    }

    static Vec2 PointAt(IReadOnlyList<Vec2> pts, bool closed, double s)
    {
        var n = pts.Count;
        var i = Math.Min((int)Math.Floor(s), closed ? n - 1 : n - 2);
        var t = s - i;
        var (a, b) = (pts[i % n], pts[(i + 1) % n]);
        return a + (b - a) * t;
    }

    /// <summary>The path from parameter <paramref name="from"/> to <paramref name="to"/> (a loop's parameters may wrap past its count).</summary>
    static List<Vec2> SubPath(IReadOnlyList<Vec2> pts, bool closed, double from, double to)
    {
        var n = pts.Count;
        var o = new List<Vec2> { PointAt(pts, closed, closed ? from % n : from) };
        for (var k = (int)Math.Floor(from) + 1; k < to - 1e-9; k++) o.Add(pts[k % n]);
        var end = PointAt(pts, closed, closed ? (to >= n ? to - n : to) : to);
        if (closed && Math.Abs(to - n) < 1e-9) end = pts[0];
        if ((end - o[^1]).Length > 1e-12) o.Add(end);
        return o;
    }

    /// <summary>The intervals between cuts on [lo, hi], without the one holding <paramref name="pick"/>.</summary>
    static List<(double From, double To)> Pieces(List<double> cuts, double lo, double hi, double pick)
    {
        var bounds = new List<double> { lo };
        bounds.AddRange(cuts);
        bounds.Add(hi);
        var o = new List<(double, double)>();
        var removed = false;
        for (var i = 0; i + 1 < bounds.Count; i++)
        {
            var (a, b) = (bounds[i], bounds[i + 1]);
            if (!removed && pick >= a - 1e-12 && pick <= b + 1e-12) { removed = true; continue; }
            o.Add((a, b));
        }
        return o;
    }

    /// <summary>On a loop of length <paramref name="period"/>: the arcs between cuts, without the one holding the pick; each runs from a cut to the next (its end may pass the period).</summary>
    static List<(double From, double To)> CyclicPieces(List<double> cuts, double period, double pick)
    {
        var o = new List<(double, double)>();
        for (var i = 0; i < cuts.Count; i++)
        {
            var a = cuts[i];
            var b = i + 1 < cuts.Count ? cuts[i + 1] : cuts[0] + period;
            var p = pick < a ? pick + period : pick;
            if (p >= a - 1e-12 && p <= b + 1e-12) continue;
            o.Add((a, b));
        }
        // Start with the piece after the removed one, so a loop cut twice reads as one path.
        return o;
    }

    // ------------------------------------------------------------ extend

    /// <summary>The end nearer the pick carried on to the first boundary it meets; null when it meets none.</summary>
    public static Shape? Extend(Shape target, IEnumerable<Shape> boundaries, Vec2 pick)
    {
        var others = boundaries.Where(c => !ReferenceEquals(c, target) && !c.Equals(target)).ToList();
        Vec2? Hit(Vec2 from, Vec2 dir) => others.SelectMany(o => Geo.Ray(from, dir, o)).OrderBy(h => h.T).Select(h => (Vec2?)h.Point).FirstOrDefault();
        switch (target)
        {
            case Line l:
            {
                var atB = (pick - l.B).Length <= (pick - l.A).Length;
                if (atB) return Hit(l.B, l.B - l.A) is { } p ? new Line(l.A, p) : null;
                return Hit(l.A, l.A - l.B) is { } q ? new Line(q, l.B) : null;
            }
            case Polyline p when !p.Closed && p.Pts.Count >= 2:
            {
                var pts = p.Pts.ToList();
                if ((pick - pts[^1]).Length <= (pick - pts[0]).Length)
                {
                    if (Hit(pts[^1], pts[^1] - pts[^2]) is not { } h) return null;
                    pts[^1] = h;
                }
                else
                {
                    if (Hit(pts[0], pts[0] - pts[1]) is not { } h) return null;
                    pts[0] = h;
                }
                return new Polyline(pts, false);
            }
            case Arc a:
            {
                var circle = new ArcP(a.C, a.R, 0, 0, true);
                var angles = others.SelectMany(o => Geo.Prims(o)).SelectMany(pr => Geo.Intersect(circle, pr)).Select(q => Math.Atan2(q.Y - a.C.Y, q.X - a.C.X)).ToList();
                var room = 2 * Math.PI - a.Sweep;
                if ((pick - a.At(a.End)).Length <= (pick - a.At(a.Start)).Length)
                {
                    var deltas = angles.Select(t => Geo.NormAngle(t - a.End)).Where(x => x > 1e-9 && x < room - 1e-9).ToList();
                    return deltas.Count == 0 ? null : a with { End = Geo.NormAngle(a.End + deltas.Min()) };
                }
                var back = angles.Select(t => Geo.NormAngle(a.Start - t)).Where(x => x > 1e-9 && x < room - 1e-9).ToList();
                return back.Count == 0 ? null : a with { Start = Geo.NormAngle(a.Start - back.Min()) };
            }
        }
        return null;
    }

    // ------------------------------------------------------------ fillet and chamfer

    /// <summary>The corner of two lines: where they meet, and each one's direction from there toward its pick.</summary>
    static (Vec2 X, Vec2 U1, Vec2 U2)? Corner(Line l1, Vec2 p1, Line l2, Vec2 p2)
    {
        var (d1, d2) = (l1.B - l1.A, l2.B - l2.A);
        var den = Vec2.Cross(d1, d2);
        if (Math.Abs(den) < 1e-12 * Math.Max(d1.Length * d2.Length, 1e-18)) return null;
        var x = l1.A + d1 * (Vec2.Cross(l2.A - l1.A, d2) / den);
        var u1 = Geo.Norm(d1) * (Vec2.Dot(p1 - x, d1) >= 0 ? 1 : -1);
        var u2 = Geo.Norm(d2) * (Vec2.Dot(p2 - x, d2) >= 0 ? 1 : -1);
        return (x, u1, u2);
    }

    /// <summary>A line with the end nearer the corner (along <paramref name="u"/>) moved to <paramref name="to"/>.</summary>
    static Line EndAt(Line l, Vec2 x, Vec2 u, Vec2 to) =>
        Vec2.Dot(l.A - x, u) <= Vec2.Dot(l.B - x, u) ? new Line(to, l.B) : new Line(l.A, to);

    static double Reach(Line l, Vec2 x, Vec2 u) => Math.Max(Vec2.Dot(l.A - x, u), Vec2.Dot(l.B - x, u));

    /// <summary>Two lines filleted with radius r (0 makes a sharp corner): the lines trimmed to the arc and the arc; null when parallel or the radius does not fit.</summary>
    public static (Line First, Line Second, Arc? Arc)? Fillet(Line l1, Vec2 p1, Line l2, Vec2 p2, double r)
    {
        if (Corner(l1, p1, l2, p2) is not { } k) return null;
        var (x, u1, u2) = k;
        if (r <= 1e-12) return (EndAt(l1, x, u1, x), EndAt(l2, x, u2, x), null);
        var theta = Math.Acos(Math.Clamp(Vec2.Dot(u1, u2), -1, 1));
        if (theta < 1e-9 || Math.PI - theta < 1e-9) return null;
        var t = r / Math.Tan(theta / 2);
        if (t > Reach(l1, x, u1) + 1e-9 || t > Reach(l2, x, u2) + 1e-9) return null;
        var (t1, t2) = (x + u1 * t, x + u2 * t);
        var c = x + Geo.Norm(u1 + u2) * (r / Math.Sin(theta / 2));
        var (a1, a2) = (Math.Atan2(t1.Y - c.Y, t1.X - c.X), Math.Atan2(t2.Y - c.Y, t2.X - c.X));
        var arc = Geo.NormAngle(a2 - a1) <= Math.PI ? new Arc(c, r, Geo.NormAngle(a1), Geo.NormAngle(a2)) : new Arc(c, r, Geo.NormAngle(a2), Geo.NormAngle(a1));
        return (EndAt(l1, x, u1, t1), EndAt(l2, x, u2, t2), arc);
    }

    /// <summary>Two lines chamfered: <paramref name="d1"/> back along the first, <paramref name="d2"/> along the second, joined by a line.</summary>
    public static (Line First, Line Second, Line Line)? Chamfer(Line l1, Vec2 p1, Line l2, Vec2 p2, double d1, double d2)
    {
        if (Corner(l1, p1, l2, p2) is not { } k) return null;
        var (x, u1, u2) = k;
        if (d1 > Reach(l1, x, u1) + 1e-9 || d2 > Reach(l2, x, u2) + 1e-9) return null;
        var (t1, t2) = (x + u1 * d1, x + u2 * d2);
        return (EndAt(l1, x, u1, t1), EndAt(l2, x, u2, t2), new Line(t1, t2));
    }
}
