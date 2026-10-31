using Lapin.Geometry;

namespace Lapin.Alignments;

/// <summary>A stretch of the measured line (distance along it, m from its first point) where the shift is held between two values (m).</summary>
public sealed record ShiftBand(double From, double To, double Min, double Max);

/// <summary>
/// What a realignment may do: the shift allowed either way across (slew) and up or down (lift), stretches
/// held tighter or looser (a bridge, a platform edge, a fixed point: a band of a few millimetres), the
/// smallest radius, whether the clothoids may change, the PIs left as they are, the smallest radius of a
/// vertical curve and the closest two PVIs may come.
/// </summary>
public sealed record RealignOptions
{
    public double Slew { get; init; } = 0.05;
    public double Lift { get; init; } = 0.05;
    public IReadOnlyList<ShiftBand> SlewBands { get; init; } = [];
    public IReadOnlyList<ShiftBand> LiftBands { get; init; } = [];
    public double MinRadius { get; init; } = 150;
    public bool Spirals { get; init; } = true;
    public IReadOnlyCollection<int> Locked { get; init; } = [];
    public double MinVerticalRadius { get; init; } = 2000;
    public double PviSpacing { get; init; } = 40;
    /// <summary>How many PIs (large-radius curves on a straight) the alignment may gain where the limits are not met.</summary>
    public int MaxAddedPis { get; init; } = 6;
    /// <summary>The radius an added curve starts from.</summary>
    public double AddedRadius { get; init; } = 20000;
    /// <summary>How many PVIs the profile may gain where the limits are not met.</summary>
    public int MaxAddedPvis { get; init; } = 40;
}

/// <summary>
/// A design proposed from the existing track: the alignment and the red line moved within the shifts a
/// tamping machine may make (a few centimetres either way, across and up or down), so the measured line,
/// slewed and lifted, becomes the design. The shift at a measured point is what moves it onto the design:
/// slew across, + to the left of the way the line runs; lift up, + up. Both are found by least squares
/// (Levenberg–Marquardt) over the PIs (position, radius, clothoids) and then the PVIs (station, height),
/// small shifts preferred and the limits held by a penalty that grows steeply past them; a PI's change
/// moves only the points between its neighbours' curves, so each derivative is taken there alone. The
/// profile gains a PVI where the limits are still not met. What cannot be met is reported, for the user to
/// settle (a band of their own, a PI moved or added by hand, a PI locked).
/// </summary>
public static class Realignment
{
    /// <summary>The shifts at the measured points: the station on the design (m from its start), the slew and the lift (NaN without a height or a profile).</summary>
    public sealed record Shifts(double[] Along, double[] Station, double[] Slew, double[] Lift);

    /// <summary>The PIs (with those added), each point's station and slew, how far past its band the worst one is, the PIs locked (counted in the new list) and how many were added.</summary>
    public sealed record HorizontalResult(List<AlignmentPi> Pis, double[] Station, double[] Slew, double MaxExcess, int Iterations, IReadOnlyList<int> Locked, int Added);
    public sealed record VerticalResult(List<Pvi> Pvis, double[] Lift, double MaxExcess, int Iterations, int Added);

    /// <summary>Distance along a measured line at each point, from the first.</summary>
    public static double[] Along(IReadOnlyList<Vec2> pts)
    {
        var s = new double[pts.Count];
        for (var i = 1; i < s.Length; i++) s[i] = s[i - 1] + (pts[i] - pts[i - 1]).Length;
        return s;
    }

    /// <summary>The band in force at a distance along: the last band given that holds it, else ± the default.</summary>
    public static (double Min, double Max) BandAt(IReadOnlyList<ShiftBand> bands, double along, double limit)
    {
        for (var i = bands.Count - 1; i >= 0; i--) if (along >= bands[i].From - 1e-9 && along <= bands[i].To + 1e-9) return (Math.Min(bands[i].Min, bands[i].Max), Math.Max(bands[i].Min, bands[i].Max));
        return (-limit, limit);
    }

    /// <summary>How far past its band a shift is (m; 0 inside).</summary>
    public static double Excess(double shift, (double Min, double Max) band) => Math.Max(0, Math.Max(band.Min - shift, shift - band.Max));

    /// <summary>The shifts of the measured points (plan, with heights; a NaN height has no lift) onto a design: PIs, and PVIs stationed from the design's start (null: no lift).</summary>
    public static Shifts Of(IReadOnlyList<AlignmentPi> pis, IReadOnlyList<Pvi>? pvis, IReadOnlyList<Vec3> measured)
    {
        var plan = measured.Select(p => new Vec2(p.X, p.Y)).ToList();
        var a = PiAlignment.Build(pis);
        var (st, off) = (new double[plan.Count], new double[plan.Count]);
        Seed(a, plan, st);
        Project(a, plan, st, off, 0, plan.Count);
        var slew = off.Select(o => -o).ToArray();
        var profile = pvis != null && pvis.Count >= 2 && VerticalProfile.Problem(pvis) == null ? VerticalProfile.Build(pvis) : null;
        var lift = measured.Select((p, i) => profile == null || double.IsNaN(p.Z) ? double.NaN : profile.ElevationAt(st[i]) - p.Z).ToArray();
        return new(Along(plan), st, slew, lift);
    }

    // ─── Projection ──────────────────────────────────────────────────────────

    /// <summary>A first station for each point: the nearest of samples a metre apart, the search moving along with the points.</summary>
    static void Seed(PiAlignment a, IReadOnlyList<Vec2> pts, double[] st)
    {
        var n = Math.Max(2, (int)Math.Ceiling(a.Length) + 1);
        var line = new Vec2[n];
        for (var i = 0; i < n; i++) { var q = a.At(Math.Min(a.Length, i * a.Length / (n - 1))); line[i] = new(q.X, q.Y); }
        var at = 0;
        for (var j = 0; j < pts.Count; j++)
        {
            var best = double.MaxValue; var bi = at;
            var (lo, hi) = (Math.Max(0, at - 30), Math.Min(n, at + 30));
            for (var i = lo; i < hi; i++) { var d = (line[i] - pts[j]).Length; if (d < best) { best = d; bi = i; } }
            if (bi == lo && lo > 0 || bi == hi - 1 && hi < n)
                for (var i = 0; i < n; i++) { var d = (line[i] - pts[j]).Length; if (d < best) { best = d; bi = i; } }
            at = bi;
            st[j] = bi * a.Length / (n - 1);
        }
    }

    /// <summary>The foot of each point from <paramref name="from"/> to <paramref name="to"/> (Newton from the station it has), its station and signed offset (+ left).</summary>
    static void Project(PiAlignment a, IReadOnlyList<Vec2> pts, double[] st, double[] off, int from, int to)
    {
        for (var j = from; j < to; j++)
        {
            var s = Math.Clamp(st[j], 0, a.Length);
            PlanPoint q = a.At(s);
            for (var it = 0; it < 8; it++)
            {
                var t = new Vec2(Math.Cos(q.Direction), Math.Sin(q.Direction));
                var ds = Vec2.Dot(pts[j] - q.P, t);
                var next = Math.Clamp(s + ds, 0, a.Length);
                if (Math.Abs(next - s) < 1e-7) break;
                s = next;
                q = a.At(s);
            }
            var tt = new Vec2(Math.Cos(q.Direction), Math.Sin(q.Direction));
            var d = pts[j] - q.P;
            st[j] = s;
            off[j] = tt.X * d.Y - tt.Y * d.X;
        }
    }

    // ─── The alignment ───────────────────────────────────────────────────────

    enum H { X, Y, LnR, SpiralIn, SpiralOut, Normal }

    /// <summary>
    /// The PIs moved so the measured centre line, slewed, lies on them: inner PIs by their position, radius and
    /// clothoids, the end PIs across their tangents; locked PIs stay. The measured line in plan.
    /// </summary>
    public static HorizontalResult Horizontal(IReadOnlyList<AlignmentPi> start, IReadOnlyList<Vec2> measured, RealignOptions o)
    {
        var along = Along(measured);
        var band = along.Select(s => BandAt(o.SlewBands, s, o.Slew)).ToArray();
        // A PI on a straight (no radius, no turn: one the user put there to bend the line) gets a large radius to bend with.
        var pis = start.Select((p, i) => i > 0 && i < start.Count - 1 && !(p.Radius > 0) ? p with { Radius = o.AddedRadius } : p).ToList();
        var locked = pis.Select((_, i) => o.Locked.Contains(i)).ToList();
        var (current, st, off, iterations) = FitPlan(pis, locked, measured, band, o);
        double Worst(double[] offs, out int at)
        {
            at = -1; var w = 0.0;
            for (var j = 0; j < offs.Length; j++) { var e = Excess(-offs[j], band[j]); if (e > w) { w = e; at = j; } }
            return w;
        }
        var excess = Worst(off, out var worst);
        var added = 0;
        var basis = pis;
        // Where the limits are still missed on a straight, curves of large radius at the crests of the wave there (the
        // measured offsets from the straight simplified to the limit): the line bends with the long wave.
        while (excess > 1e-4 && added < o.MaxAddedPis)
        {
            var a = PiAlignment.Build(current);
            // The straight that misses its limits worst (a curve that misses them was already fitted as well as it goes).
            var offLimits = off;
            var candidates = a.Segments.Where(x => x.Kind == PiSegmentKind.Line && x.Length >= 80)
                .Select(x => (Seg: x, Worst: Enumerable.Range(0, st.Length).Where(j => st[j] > x.S + 30 && st[j] < x.S + x.Length - 30).Select(j => (J: j, E: Excess(-offLimits[j], band[j]))).DefaultIfEmpty((J: -1, E: 0)).MaxBy(y => y.E)))
                .Where(x => x.Worst.E > 1e-4).ToList();
            if (candidates.Count == 0) break;
            var seg = candidates.MaxBy(x => x.Worst.E);
            worst = seg.Worst.J;
            var s = st[worst];
            var (lo, hi) = (seg.Seg.S + 30, seg.Seg.S + seg.Seg.Length - 30);
            var on = Enumerable.Range(0, st.Length).Where(j => st[j] > lo && st[j] < hi).OrderBy(j => st[j]).ToList();
            if (on.Count < 3) break;
            // The crests to the tightest band on the straight: a bridge held to a centimetre asks the line to follow its wave that closely.
            var crests = Crests(on, st, off, 0.8 * on.Min(j => (band[j].Max - band[j].Min) / 2));
            var kept = new List<int>();
            foreach (var j in crests) if (kept.Count == 0 || st[j] - st[kept[^1]] >= 60) kept.Add(j);
            if (kept.Count == 0) kept.Add(on.MinBy(j => Math.Abs(st[j] - s)));
            kept = kept.Take(o.MaxAddedPis - added).ToList();
            var k = Math.Clamp(seg.Seg.Pi, 1, current.Count - 1);
            // Added to the PIs as they came, and to those the fit left: a fit that compromised around a stretch it could not hold is
            // often no start for one that can, though where the first no longer makes a chain with the curves added, the second may.
            (List<AlignmentPi> Pis, List<bool> Locked, List<AlignmentPi> Basis, double[] St, double[] Off, double E, int W)? best = null;
            foreach (var from in new[] { basis, current })
            {
                var next = from.ToList();
                var nextLocked = locked.ToList();
                next.InsertRange(k, kept.Select(j => new AlignmentPi(measured[j].X, measured[j].Y, o.AddedRadius)));
                nextLocked.InsertRange(k, kept.Select(_ => false));
                if (PiAlignment.Problem(next) != null) continue;
                var (c2, st2, off2, it2) = FitPlan(next, nextLocked, measured, band, o);
                iterations += it2;
                var e2 = Worst(off2, out var w2);
                if (best == null || e2 < best.Value.E) best = (c2, nextLocked, next, st2, off2, e2, w2);
            }
            if (best is not { } got || got.E >= excess - 1e-4) break;
            (current, locked, basis, st, off, excess, worst) = (got.Pis, got.Locked, got.Basis, got.St, got.Off, got.E, got.W);
            added += kept.Count;
        }
        var slew = off.Select(x => -x).ToArray();
        return new(current, st, slew, excess, iterations, locked.Select((l, i) => (l, i)).Where(x => x.l).Select(x => x.i).ToList(), added);
    }

    /// <summary>The inner vertices of the points' offsets along a straight simplified to <paramref name="tol"/> (Douglas–Peucker): where a long wave crests.</summary>
    static List<int> Crests(List<int> idx, double[] st, double[] off, double tol)
    {
        var o = new List<int>();
        void Walk(int a, int b)
        {
            if (b - a < 2) return;
            var (sa, sb) = (st[idx[a]], st[idx[b]]);
            double worst = 0; var at = -1;
            for (var i = a + 1; i < b; i++)
            {
                var t = (st[idx[i]] - sa) / Math.Max(1e-9, sb - sa);
                var d = Math.Abs(off[idx[i]] - (off[idx[a]] + (off[idx[b]] - off[idx[a]]) * t));
                if (d > worst) { worst = d; at = i; }
            }
            if (worst <= tol || at < 0) return;
            Walk(a, at); o.Add(idx[at]); Walk(at, b);
        }
        Walk(0, idx.Count - 1);
        return o;
    }

    static (List<AlignmentPi> Pis, double[] Station, double[] Offset, int Iterations) FitPlan(List<AlignmentPi> pis, List<bool> locked, IReadOnlyList<Vec2> measured, (double Min, double Max)[] band, RealignOptions o)
    {
        var a0 = PiAlignment.Build(pis);

        // The unknowns: what may move at each PI.
        var map = new List<(int Pi, H Kind)>();
        var normals = new Vec2[pis.Count];
        for (var i = 0; i < pis.Count; i++)
        {
            if (locked[i]) continue;
            if (i == 0 || i == pis.Count - 1)
            {
                var leg = i == 0 ? pis[1].P - pis[0].P : pis[^1].P - pis[^2].P;
                var u = leg.Normalized();
                normals[i] = new Vec2(-u.Y, u.X);
                map.Add((i, H.Normal));
                continue;
            }
            map.Add((i, H.X)); map.Add((i, H.Y));
            if (pis[i].Radius > 0)
            {
                map.Add((i, H.LnR));
                if (o.Spirals) { map.Add((i, H.SpiralIn)); map.Add((i, H.SpiralOut)); }
            }
        }
        var p0 = map.Select(m => m.Kind switch
        {
            H.X => pis[m.Pi].X, H.Y => pis[m.Pi].Y, H.LnR => Math.Log(pis[m.Pi].Radius),
            H.SpiralIn => pis[m.Pi].SpiralIn, H.SpiralOut => pis[m.Pi].SpiralOut, _ => 0,
        }).ToArray();
        var minLnR = map.Select(m => m.Kind == H.LnR ? Math.Log(Math.Min(o.MinRadius, pis[m.Pi].Radius)) : 0).ToArray();
        double[] Clamp(double[] p)
        {
            var c = (double[])p.Clone();
            for (var k = 0; k < c.Length; k++)
            {
                if (map[k].Kind == H.LnR) c[k] = Math.Clamp(c[k], minLnR[k], Math.Log(100000));
                if (map[k].Kind is H.SpiralIn or H.SpiralOut) c[k] = Math.Clamp(c[k], 0, 400);
                if (map[k].Kind == H.Normal) c[k] = Math.Clamp(c[k], -1, 1);
            }
            return c;
        }
        List<AlignmentPi> Apply(double[] p)
        {
            var o2 = pis.ToList();
            for (var k = 0; k < p.Length; k++)
            {
                var (i, kind) = map[k];
                var q = o2[i];
                o2[i] = kind switch
                {
                    H.X => q with { X = p[k] }, H.Y => q with { Y = p[k] }, H.LnR => q with { Radius = Math.Exp(p[k]) },
                    H.SpiralIn => q with { SpiralIn = p[k] }, H.SpiralOut => q with { SpiralOut = p[k] },
                    _ => q with { X = pis[i].X + normals[i].X * p[k], Y = pis[i].Y + normals[i].Y * p[k] },
                };
            }
            return o2;
        }
        static PiAlignment? TryBuild(List<AlignmentPi> pis) { try { return PiAlignment.Build(pis); } catch (ArgumentException) { return null; } }

        var st = new double[measured.Count];
        var off = new double[measured.Count];
        Seed(a0, measured, st);
        Project(a0, measured, st, off, 0, measured.Count);

        // Two residuals a point: the shift, lightly (small shifts preferred), and its fourth power past 90 % of the band (the limit).
        void Residuals(double[] offs, int from, int to, double[] into, int at)
        {
            for (var j = from; j < to; j++)
            {
                var (lo, hi) = band[j];
                var h = Math.Max(5e-4, (hi - lo) / 2);
                var d = (-offs[j] - (lo + hi) / 2) / h;
                into[at + 2 * (j - from)] = 0.3 * d;
                var e = d / 0.9;
                into[at + 2 * (j - from) + 1] = e * e * e * e;
            }
        }
        // A radius stays near where it started unless the points ask otherwise: a curve that turns little (one added on a straight) says little of its radius.
        const double prior = 0.3;
        double Prior(double[] q) { double c = 0; for (var k = 0; k < q.Length; k++) if (map[k].Kind == H.LnR) { var d = q[k] - p0[k]; c += prior * prior * d * d; } return c; }
        double Cost(double[] r, double[] q) => r.Sum(x => x * x) + Prior(q);

        var p = Clamp(p0);
        var current = Apply(p);
        var a = TryBuild(current) ?? a0;
        var r = new double[2 * measured.Count];
        Residuals(off, 0, measured.Count, r, 0);
        var cost = Cost(r, p);
        var lambda = 1e-3;
        var iterations = 0;
        var n = p.Length;
        for (; iterations < 80 && n > 0; iterations++)
        {
            // Where each unknown reaches: between the curves of the PI's neighbours (and a margin).
            var ranges = new (int From, int To)[n];
            var spans = Spans(a, pis.Count);
            for (var k = 0; k < n; k++)
            {
                var i = map[k].Pi;
                var lo = i == 0 ? 0 : spans[i - 1].Start;
                var hi = i == pis.Count - 1 ? a.Length : spans[i + 1].End;
                if (i == 0) hi = spans[Math.Min(pis.Count - 1, 1)].End;
                if (i == pis.Count - 1) lo = spans[Math.Max(0, pis.Count - 2)].Start;
                (lo, hi) = (lo - 15, hi + 15);
                var f = 0; while (f < st.Length && st[f] < lo) f++;
                var t = st.Length; while (t > f && st[t - 1] > hi) t--;
                ranges[k] = (f, t);
            }
            // The derivatives, each over its reach.
            var cols = new double[n][];
            for (var k = 0; k < n; k++)
            {
                var (f, t) = ranges[k];
                cols[k] = new double[2 * (t - f)];
                if (t <= f) continue;
                var h = map[k].Kind switch { H.X or H.Y => 1e-3, H.LnR => 1e-4, H.Normal => 1e-3, _ => 1e-2 };
                var dir = 1.0;
                var q = (double[])p.Clone(); q[k] += h;
                var b = TryBuild(Apply(q));
                if (b == null) { dir = -1; q[k] = p[k] - h; b = TryBuild(Apply(q)); }
                if (b == null) continue;
                var (st2, off2) = ((double[])st.Clone(), (double[])off.Clone());
                Project(b, measured, st2, off2, f, t);
                var r2 = new double[2 * (t - f)];
                Residuals(off2, f, t, r2, 0);
                for (var x = 0; x < r2.Length; x++) cols[k][x] = (r2[x] - r[2 * f + x]) / (h * dir);
            }
            var (A, g) = Normal(cols, ranges, r, n);
            for (var k = 0; k < n; k++) if (map[k].Kind == H.LnR) { A[k, k] += prior * prior; g[k] += prior * prior * (p[k] - p0[k]); }

            var accepted = false;
            while (lambda < 1e10)
            {
                var delta = Solve(A, g, lambda);
                if (delta == null) { lambda *= 10; continue; }
                var trial = Clamp(p.Zip(delta, (x, d) => x + d).ToArray());
                var pisT = Apply(trial);
                var bT = TryBuild(pisT);
                if (bT == null) { lambda *= 4; continue; }
                var (stT, offT) = ((double[])st.Clone(), (double[])off.Clone());
                Project(bT, measured, stT, offT, 0, measured.Count);
                var rT = new double[r.Length];
                Residuals(offT, 0, measured.Count, rT, 0);
                var cT = Cost(rT, trial);
                if (cT < cost)
                {
                    var gain = (cost - cT) / Math.Max(1e-12, cost);
                    (p, a, st, off, r, cost, current) = (trial, bT, stT, offT, rT, cT, pisT);
                    lambda = Math.Max(1e-9, lambda / 3);
                    accepted = gain > 1e-7;
                    break;
                }
                lambda *= 4;
            }
            if (!accepted) break;
        }
        // Rounded as a drawing keeps them (0,1 mm, 0,1 m of radius, 1 cm of clothoid), when the chain still builds so.
        var rounded = current.Select(q => q with { X = Math.Round(q.X, 4), Y = Math.Round(q.Y, 4), Radius = q.Radius > 0 ? Math.Round(q.Radius, 1) : 0, SpiralIn = Math.Round(q.SpiralIn, 2), SpiralOut = Math.Round(q.SpiralOut, 2) }).ToList();
        if (TryBuild(rounded) is { } fin)
        {
            current = rounded;
            Project(fin, measured, st, off, 0, measured.Count);
        }
        return (current, st, off, iterations);
    }

    /// <summary>Where each PI's curve starts and ends along the chain (a PI without a curve: where the tangents meet).</summary>
    static (double Start, double End)[] Spans(PiAlignment a, int count)
    {
        var o = new (double Start, double End)[count];
        for (var i = 0; i < count; i++)
        {
            var segs = a.Segments.Where(s => s.Pi == i).ToList();
            var curve = segs.Where(s => s.Kind != PiSegmentKind.Line).ToList();
            if (curve.Count > 0) o[i] = (curve[0].S, curve[^1].S + curve[^1].Length);
            else if (segs.Count > 0) { var e = segs[^1].S + segs[^1].Length; o[i] = (e, e); }
            else o[i] = i == 0 ? (0, 0) : (o[i - 1].End, o[i - 1].End);
        }
        if (count > 0) { o[0] = (0, 0); o[^1] = (a.Length, a.Length); }
        return o;
    }

    // ─── The red line ────────────────────────────────────────────────────────

    /// <summary>
    /// The PVIs moved so the measured heights, lifted, lie on the red line: each PVI's height, the inner ones'
    /// stations too; the vertical curves as long as the smallest radius asks (where they have room). Stations
    /// from the design's start; <paramref name="station"/> is each point's on the design, <paramref name="z"/>
    /// its height (NaN: none). Starts from <paramref name="start"/>, or from the heights simplified to the limit.
    /// </summary>
    public static VerticalResult Vertical(IReadOnlyList<Pvi>? start, double length, IReadOnlyList<double> along, IReadOnlyList<double> station, IReadOnlyList<double> z, RealignOptions o)
    {
        var idx = Enumerable.Range(0, z.Count).Where(i => !double.IsNaN(z[i])).OrderBy(i => station[i]).ToArray();
        if (idx.Length < 2) throw new ArgumentException("The measured points have no heights: the red line cannot be fitted.");
        var s = idx.Select(i => station[i]).ToArray();
        var h = idx.Select(i => z[i]).ToArray();
        var band = idx.Select(i => BandAt(o.LiftBands, along[i], o.Lift)).ToArray();
        double ZAt(double x)
        {
            var k = Array.BinarySearch(s, x);
            if (k >= 0) return h[k];
            k = ~k;
            if (k == 0) return h[0];
            if (k >= s.Length) return h[^1];
            var t = (x - s[k - 1]) / Math.Max(1e-9, s[k] - s[k - 1]);
            return h[k - 1] + (h[k] - h[k - 1]) * t;
        }
        // A start: the PVIs given (when they make a profile over the design), else the heights to 60 % of the limit; each point's band centred on.
        double Mid(double x) { var j = Math.Clamp(Array.BinarySearch(s, x) is var b && b < 0 ? ~b : b, 0, s.Length - 1); return (band[j].Min + band[j].Max) / 2; }
        List<(double S, double Z)> pv;
        if (start is { Count: >= 2 } && VerticalProfile.Problem(start) == null)
            pv = start.OrderBy(p => p.Station).Select(p => (p.Station, p.Elevation)).ToList();
        else
            pv = VerticalProfile.FromGround(0, length, x => ZAt(x) + Mid(x), h[0], 10, Math.Max(0.005, 0.6 * o.Lift)).Select(p => (p.Station, p.Elevation)).ToList();
        pv[0] = (0, pv[0].Z);
        pv[^1] = (length, pv[^1].Z);

        List<Pvi> Build(IReadOnlyList<(double S, double Z)> q)
        {
            var o2 = q.Select(x => new Pvi(x.S, x.Z)).ToList();
            for (var i = 1; i < o2.Count - 1; i++)
            {
                var g0 = (o2[i].Elevation - o2[i - 1].Elevation) / Math.Max(1e-6, o2[i].Station - o2[i - 1].Station);
                var g1 = (o2[i + 1].Elevation - o2[i].Elevation) / Math.Max(1e-6, o2[i + 1].Station - o2[i].Station);
                var room = 0.9 * Math.Min(o2[i].Station - o2[i - 1].Station, o2[i + 1].Station - o2[i].Station);
                o2[i] = o2[i] with { Length = Math.Max(0, Math.Min(room, o.MinVerticalRadius * Math.Abs(g1 - g0))) };
            }
            return o2;
        }
        double[] Lifts(List<Pvi> pvis, int from, int to)
        {
            var prof = VerticalProfile.Build(pvis);
            var o2 = new double[to - from];
            for (var j = from; j < to; j++) o2[j - from] = prof.ElevationAt(s[j]) - h[j];
            return o2;
        }
        void Residuals(double[] lifts, int from, double[] into, int at)
        {
            for (var x = 0; x < lifts.Length; x++)
            {
                var (lo, hi) = band[from + x];
                var hw = Math.Max(5e-4, (hi - lo) / 2);
                var d = (lifts[x] - (lo + hi) / 2) / hw;
                into[at + 2 * x] = 0.3 * d;
                var e = d / 0.9;
                into[at + 2 * x + 1] = e * e * e * e;
            }
        }

        var iterations = 0;
        var added = 0;
        double[] lift;
        while (true)
        {
            iterations += Fit(pv);
            lift = Lifts(Build(pv), 0, s.Length);
            var worst = -1; var we = 0.0;
            for (var j = 0; j < s.Length; j++) { var e = Excess(lift[j], band[j]); if (e > we) { we = e; worst = j; } }
            if (worst < 0 || added >= o.MaxAddedPvis) break;
            // A PVI where the limit is worst missed, unless one is that close already.
            var at = s[worst];
            if (pv.Any(x => Math.Abs(x.S - at) < o.PviSpacing / 2)) break;
            var k = pv.FindIndex(x => x.S > at);
            if (k <= 0) break;
            pv.Insert(k, (at, h[worst] + (band[worst].Min + band[worst].Max) / 2));
            added++;
        }

        int Fit(List<(double S, double Z)> q)
        {
            // Unknowns: every height, the inner stations.
            var map = new List<(int Pvi, bool Station)>();
            for (var i = 0; i < q.Count; i++) { map.Add((i, false)); if (i > 0 && i < q.Count - 1) map.Add((i, true)); }
            var n = map.Count;
            double[] Pack() => map.Select(m => m.Station ? q[m.Pvi].S : q[m.Pvi].Z).ToArray();
            List<(double S, double Z)> Unpack(double[] p)
            {
                var o2 = q.ToList();
                for (var k = 0; k < n; k++) { var (i, isS) = map[k]; o2[i] = isS ? (p[k], o2[i].Z) : (o2[i].S, p[k]); }
                // Stations kept in order, a few metres apart.
                for (var i = 1; i < o2.Count - 1; i++) o2[i] = (Math.Clamp(o2[i].S, o2[i - 1].S + 5, Math.Max(o2[i - 1].S + 5, o2[^1].S - 5 * (o2.Count - 1 - i))), o2[i].Z);
                return o2;
            }
            int From(double x) { var b = Array.BinarySearch(s, x); return b < 0 ? ~b : b; }
            var p = Pack();
            var r = new double[2 * s.Length];
            Residuals(Lifts(Build(q), 0, s.Length), 0, r, 0);
            var cost = r.Sum(x => x * x);
            var lambda = 1e-3;
            var it = 0;
            for (; it < 60; it++)
            {
                var cur = Unpack(p);
                var ranges = map.Select(m => (From(cur[Math.Max(0, m.Pvi - 1)].S - 1), From(cur[Math.Min(cur.Count - 1, m.Pvi + 1)].S + 1))).ToArray();
                var cols = new double[n][];
                for (var k = 0; k < n; k++)
                {
                    var (f, t) = ranges[k];
                    cols[k] = new double[2 * (t - f)];
                    if (t <= f) continue;
                    var step = map[k].Station ? 1e-2 : 1e-4;
                    var q2 = (double[])p.Clone(); q2[k] += step;
                    var r2 = new double[2 * (t - f)];
                    Residuals(Lifts(Build(Unpack(q2)), f, t), f, r2, 0);
                    for (var x = 0; x < r2.Length; x++) cols[k][x] = (r2[x] - r[2 * f + x]) / step;
                }
                var (A, g) = Normal(cols, ranges, r, n);
                var accepted = false;
                while (lambda < 1e10)
                {
                    var delta = Solve(A, g, lambda);
                    if (delta == null) { lambda *= 10; continue; }
                    var trial = p.Zip(delta, (x, d) => x + d).ToArray();
                    var pvT = Build(Unpack(trial));
                    if (VerticalProfile.Problem(pvT) != null) { lambda *= 4; continue; }
                    var rT = new double[r.Length];
                    Residuals(Lifts(pvT, 0, s.Length), 0, rT, 0);
                    var cT = rT.Sum(x => x * x);
                    if (cT < cost)
                    {
                        var gain = (cost - cT) / Math.Max(1e-12, cost);
                        (p, r, cost) = (trial, rT, cT);
                        lambda = Math.Max(1e-9, lambda / 3);
                        accepted = gain > 1e-7;
                        break;
                    }
                    lambda *= 4;
                }
                if (!accepted) break;
            }
            var done = Unpack(p);
            q.Clear(); q.AddRange(done);
            return it;
        }

        var pvis = Build(pv).Select(x => x with { Station = Math.Round(x.Station, 3), Elevation = Math.Round(x.Elevation, 4), Length = Math.Round(x.Length, 2) }).ToList();
        // Rounding may shave the room a curve had: the lengths again on the rounded PVIs.
        if (VerticalProfile.Problem(pvis) != null) pvis = Build(pvis.Select(x => (x.Station, x.Elevation)).ToList());
        var final = Lifts(pvis, 0, s.Length);
        var all = new double[z.Count];
        Array.Fill(all, double.NaN);
        for (var j = 0; j < idx.Length; j++) all[idx[j]] = final[j];
        var excess = final.Select((x, j) => Excess(x, band[j])).DefaultIfEmpty(0).Max();
        return new(pvis, all, excess, iterations, added);
    }

    // ─── Least squares ───────────────────────────────────────────────────────

    /// <summary>JᵀJ and Jᵀr from columns that are each nonzero over their own range of points (two residuals a point).</summary>
    static (double[,] A, double[] G) Normal(double[][] cols, (int From, int To)[] ranges, double[] r, int n)
    {
        var A = new double[n, n];
        var g = new double[n];
        for (var k = 0; k < n; k++)
        {
            var (fk, _) = ranges[k];
            for (var x = 0; x < cols[k].Length; x++) g[k] += cols[k][x] * r[2 * fk + x];
            for (var l = k; l < n; l++)
            {
                var (fl, _) = ranges[l];
                var from = Math.Max(fk, fl) * 2;
                var to = Math.Min(fk * 2 + cols[k].Length, fl * 2 + cols[l].Length);
                double sum = 0;
                for (var x = from; x < to; x++) sum += cols[k][x - 2 * fk] * cols[l][x - 2 * fl];
                A[k, l] = A[l, k] = sum;
            }
        }
        return (A, g);
    }

    /// <summary>(A + λ·diag A) δ = −g by Cholesky; null when it is not positive definite.</summary>
    static double[]? Solve(double[,] A, double[] g, double lambda)
    {
        var n = g.Length;
        var L = new double[n, n];
        for (var i = 0; i < n; i++)
            for (var j = 0; j <= i; j++)
            {
                var sum = A[i, j] + (i == j ? lambda * A[i, i] + 1e-12 : 0);
                for (var k = 0; k < j; k++) sum -= L[i, k] * L[j, k];
                if (i == j) { if (sum <= 0) return null; L[i, i] = Math.Sqrt(sum); }
                else L[i, j] = sum / L[j, j];
            }
        var y = new double[n];
        for (var i = 0; i < n; i++) { var sum = -g[i]; for (var k = 0; k < i; k++) sum -= L[i, k] * y[k]; y[i] = sum / L[i, i]; }
        var x = new double[n];
        for (var i = n - 1; i >= 0; i--) { var sum = y[i]; for (var k = i + 1; k < n; k++) sum -= L[k, i] * x[k]; x[i] = sum / L[i, i]; }
        return x;
    }
}
