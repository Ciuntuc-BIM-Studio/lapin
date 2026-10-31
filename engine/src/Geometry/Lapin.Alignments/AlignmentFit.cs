using Lapin.Geometry;

namespace Lapin.Alignments;

/// <summary>
/// A design alignment read back from a measured centre line (a track survey:
/// a point every half metre along the rails, as a GEDO trolley or a total
/// station gives them). The curvature along the line is estimated by a circle
/// fitted through the points of a window; where it stays under 1/<c>straightRadius</c>
/// the line is a straight, elsewhere a curve. Each straight is fitted as a
/// line, each curve becomes a PI where the tangents either side meet, with
/// the radius of the curve's plateau and clothoids as long as the curvature
/// takes to ramp up and down. A curve at either end takes the line's own
/// tangent there. The result builds as a <see cref="PiAlignment"/>; how far
/// the measured points lie from it says how well the design fits.
/// </summary>
public static class AlignmentFit
{
    /// <summary>The PIs found, how many curves among them, and the largest distance of a measured point from the alignment they build (m).</summary>
    public sealed record Result(IReadOnlyList<AlignmentPi> Pis, int Curves, double MaxDeviation);

    /// <summary>
    /// The PIs of a measured centre line. <paramref name="straightRadius"/>: a curvature flatter than this is a
    /// straight; <paramref name="window"/>: the half-length (m) of the stretch each curvature estimate is fitted over.
    /// </summary>
    public static Result FromMeasured(IReadOnlyList<Vec2> measured, double straightRadius = 5000, double window = 15)
    {
        var pts = Clean(measured);
        if (pts.Count < 2) throw new ArgumentException("A centre line needs two measured points at least.");
        var s = new double[pts.Count];
        for (var i = 1; i < pts.Count; i++) s[i] = s[i - 1] + (pts[i] - pts[i - 1]).Length;
        var total = s[^1];
        if (pts.Count < 8 || total < 2 * window)
        {
            var two = new List<AlignmentPi> { new(pts[0].X, pts[0].Y), new(pts[^1].X, pts[^1].Y) };
            return new(two, 0, Deviation(two, pts));
        }

        // The curvature at each point: the circle through the points within the window either side.
        var k = new double[pts.Count];
        var lo = 0; var hi = 0;
        for (var i = 0; i < pts.Count; i++)
        {
            while (s[i] - s[lo] > window) lo++;
            while (hi < pts.Count - 1 && s[hi + 1] - s[i] <= window) hi++;
            k[i] = hi - lo + 1 >= 8 ? Curvature(pts, lo, hi) : 0;
        }
        // Straight or curved, in runs; a run shorter than the window is noise and joins its neighbours.
        var curved = k.Select(x => Math.Abs(x) >= 1 / straightRadius).ToArray();
        var runs = Runs(curved);
        for (var pass = 0; pass < 4; pass++)
        {
            var changed = false;
            for (var r = 0; r < runs.Count; r++)
            {
                var (from, to, _) = runs[r];
                if (s[to] - s[from] >= window || runs.Count == 1) continue;
                // An end run shorter than the window joins the one beside it; an inner one, both (they are of one kind).
                for (var i = from; i <= to; i++) curved[i] = !curved[i];
                changed = true;
            }
            if (!changed) break;
            runs = Runs(curved);
        }

        var pis = new List<AlignmentPi>();
        var curves = 0;
        // The line each straight run fits, by index of the run.
        var lines = new Dictionary<int, (Vec2 P, Vec2 Dir)>();
        for (var r = 0; r < runs.Count; r++) if (!runs[r].Curved) lines[r] = LineOf(pts, runs[r].From, runs[r].To);

        // The start: on the first straight's line, or the first point of a curve at the start.
        if (!runs[0].Curved) { var p = Project(lines[0], pts[0]); pis.Add(new(p.X, p.Y)); }
        else pis.Add(new(pts[0].X, pts[0].Y));

        for (var r = 0; r < runs.Count; r++)
        {
            if (!runs[r].Curved) continue;
            var (from, to, _) = runs[r];
            // The tangents either side: the neighbouring straights, or the curve's own at the end of the line.
            var before = r > 0 ? lines[r - 1] : TangentAt(pts, k, from, window, s, true);
            var after = r < runs.Count - 1 ? lines[r + 1] : TangentAt(pts, k, to, window, s, false);
            var cross = before.Dir.X * after.Dir.Y - before.Dir.Y * after.Dir.X;
            if (Math.Abs(cross) < 1e-4) continue; // no turn worth a PI: the straights carry on
            var pi = Intersect(before, after);
            // The plateau: the sharpest the curve gets, read as a median over its middle (the clothoids ramp either side).
            var abs = Enumerable.Range(from, to - from + 1).Select(i => Math.Abs(k[i])).ToList();
            var mid = abs.Skip(abs.Count / 4).Take(Math.Max(1, abs.Count / 2)).Order().ToList();
            var plateau = mid[mid.Count / 2];
            if (plateau < 1 / straightRadius) continue;
            var radius = Math.Round(1 / plateau);
            var first = from; while (first < to && Math.Abs(k[first]) < 0.85 * plateau) first++;
            var last = to; while (last > from && Math.Abs(k[last]) < 0.85 * plateau) last--;
            var spiralIn = r > 0 ? Math.Round(s[first] - s[from]) : 0;
            var spiralOut = r < runs.Count - 1 ? Math.Round(s[to] - s[last]) : 0;
            if (spiralIn < 5) spiralIn = 0;
            if (spiralOut < 5) spiralOut = 0;
            pis.Add(new(pi.X, pi.Y, radius, spiralIn, spiralOut));
            curves++;
        }

        if (!runs[^1].Curved) { var p = Project(lines[runs.Count - 1], pts[^1]); pis.Add(new(p.X, p.Y)); }
        else pis.Add(new(pts[^1].X, pts[^1].Y));

        // What the chain cannot hold is eased: the clothoids first, then the radius down to what fits.
        if (PiAlignment.Problem(pis) != null) pis = pis.Select(p => p with { SpiralIn = 0, SpiralOut = 0 }).ToList();
        for (var i = 1; i < pis.Count - 1 && PiAlignment.Problem(pis) != null; i++)
            pis[i] = pis[i] with { Radius = Math.Round(PiAlignment.FitRadius(pis[i - 1].P, pis[i].P, pis[i + 1].P, pis[i].Radius > 0 ? pis[i].Radius : straightRadius)) };
        if (PiAlignment.Problem(pis) is { } why) throw new ArgumentException($"The measured line does not read as a design alignment: {why}");
        return new(pis, curves, Deviation(pis, pts));
    }

    /// <summary>The largest distance (m) of the measured points from the alignment the PIs build, sampled every half metre.</summary>
    public static double Deviation(IReadOnlyList<AlignmentPi> pis, IReadOnlyList<Vec2> measured)
    {
        var a = PiAlignment.Build(pis);
        var n = Math.Max(2, (int)Math.Ceiling(a.Length / 0.5) + 1);
        var line = new Vec2[n];
        for (var i = 0; i < n; i++) { var q = a.At(Math.Min(a.Length, i * 0.5)); line[i] = new(q.X, q.Y); }
        double worst = 0;
        var at = 0;
        foreach (var p in measured)
        {
            // The nearest sample moves along with the measured points: a look around the last one, then the segments beside it.
            var best = double.MaxValue; var bi = at;
            for (var i = Math.Max(0, at - 40); i < Math.Min(n, at + 40); i++) { var d = (line[i] - p).Length; if (d < best) { best = d; bi = i; } }
            if (bi == Math.Max(0, at - 40) || bi == Math.Min(n, at + 40) - 1)
                for (var i = 0; i < n; i++) { var d = (line[i] - p).Length; if (d < best) { best = d; bi = i; } }
            at = bi;
            for (var i = Math.Max(0, bi - 1); i < Math.Min(n - 1, bi + 1); i++) best = Math.Min(best, ToSegment(p, line[i], line[i + 1]));
            worst = Math.Max(worst, best);
        }
        return worst;
    }

    static List<Vec2> Clean(IReadOnlyList<Vec2> pts)
    {
        var o = new List<Vec2>(pts.Count);
        foreach (var p in pts) if (o.Count == 0 || (o[^1] - p).Length > 1e-3) o.Add(p);
        return o;
    }

    static List<(int From, int To, bool Curved)> Runs(bool[] curved)
    {
        var o = new List<(int, int, bool)>();
        var from = 0;
        for (var i = 1; i <= curved.Length; i++)
            if (i == curved.Length || curved[i] != curved[from]) { o.Add((from, i - 1, curved[from])); from = i; }
        return o;
    }

    /// <summary>
    /// The signed curvature over the points from <paramref name="lo"/> to <paramref name="hi"/>: a parabola fitted
    /// in the frame of their chord (a circle fitted to a near-straight run is ill-conditioned; the parabola is not,
    /// and is the circle to the fourth order where the window is short against the radius). Positive turning left.
    /// </summary>
    static double Curvature(IReadOnlyList<Vec2> pts, int lo, int hi)
    {
        var chord = pts[hi] - pts[lo];
        if (chord.Length < 1e-9) return 0;
        var u = chord.Normalized();
        var v = new Vec2(-u.Y, u.X);
        // y = a x² + b x + c by least squares: the normal equations.
        double s0 = 0, s1 = 0, s2 = 0, s3 = 0, s4 = 0, t0 = 0, t1 = 0, t2 = 0;
        for (var i = lo; i <= hi; i++)
        {
            var d = pts[i] - pts[lo];
            var x = Vec2.Dot(d, u); var y = Vec2.Dot(d, v);
            var x2 = x * x;
            s0 += 1; s1 += x; s2 += x2; s3 += x2 * x; s4 += x2 * x2;
            t0 += y; t1 += x * y; t2 += x2 * y;
        }
        // [s4 s3 s2; s3 s2 s1; s2 s1 s0] · [a b c] = [t2 t1 t0], by Cramer.
        var det = s4 * (s2 * s0 - s1 * s1) - s3 * (s3 * s0 - s1 * s2) + s2 * (s3 * s1 - s2 * s2);
        if (Math.Abs(det) < 1e-18) return 0;
        var a = (t2 * (s2 * s0 - s1 * s1) - s3 * (t1 * s0 - s1 * t0) + s2 * (t1 * s1 - s2 * t0)) / det;
        var b = (s4 * (t1 * s0 - s1 * t0) - t2 * (s3 * s0 - s1 * s2) + s2 * (s3 * t0 - t1 * s2)) / det;
        return 2 * a / Math.Pow(1 + b * b, 1.5);
    }

    /// <summary>The line through a straight run, by its principal axis, heading the way the points run.</summary>
    static (Vec2 P, Vec2 Dir) LineOf(IReadOnlyList<Vec2> pts, int from, int to)
    {
        var n = to - from + 1;
        double cx = 0, cy = 0;
        for (var i = from; i <= to; i++) { cx += pts[i].X; cy += pts[i].Y; }
        cx /= n; cy /= n;
        double sxx = 0, syy = 0, sxy = 0;
        for (var i = from; i <= to; i++) { var (u, v) = (pts[i].X - cx, pts[i].Y - cy); sxx += u * u; syy += v * v; sxy += u * v; }
        var angle = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        var dir = new Vec2(Math.Cos(angle), Math.Sin(angle));
        if (Vec2.Dot(dir, pts[to] - pts[from]) < 0) dir = new(-dir.X, -dir.Y);
        return (new Vec2(cx, cy), dir);
    }

    /// <summary>The tangent of the line at a point in a curve, from the circle fitted over the window beside it (at the start, after it; at the end, before it).</summary>
    static (Vec2 P, Vec2 Dir) TangentAt(IReadOnlyList<Vec2> pts, double[] k, int at, double window, double[] s, bool start)
    {
        int lo = at, hi = at;
        if (start) { while (hi < pts.Count - 1 && s[hi + 1] - s[at] <= window) hi++; }
        else { while (lo > 0 && s[at] - s[lo - 1] <= window) lo--; }
        var chord = pts[hi] - pts[lo];
        var dir = chord.Length > 1e-9 ? chord.Normalized() : new Vec2(1, 0);
        // A chord of an arc turns half the arc's angle short of the tangent at its end: the chord turned back by it.
        var kk = Curvature(pts, lo, hi);
        var half = kk * (s[hi] - s[lo]) / 2;
        var turn = start ? -half : half;
        var (c, sn) = (Math.Cos(turn), Math.Sin(turn));
        return (pts[at], new Vec2(dir.X * c - dir.Y * sn, dir.X * sn + dir.Y * c));
    }

    static Vec2 Project((Vec2 P, Vec2 Dir) line, Vec2 p) => line.P + line.Dir * Vec2.Dot(p - line.P, line.Dir);

    static Vec2 Intersect((Vec2 P, Vec2 Dir) a, (Vec2 P, Vec2 Dir) b)
    {
        var cross = a.Dir.X * b.Dir.Y - a.Dir.Y * b.Dir.X;
        var d = b.P - a.P;
        var t = (d.X * b.Dir.Y - d.Y * b.Dir.X) / cross;
        return a.P + a.Dir * t;
    }

    static double ToSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        var len2 = ab.X * ab.X + ab.Y * ab.Y;
        var t = len2 > 0 ? Math.Clamp(Vec2.Dot(p - a, ab) / len2, 0, 1) : 0;
        return (a + ab * t - p).Length;
    }
}
