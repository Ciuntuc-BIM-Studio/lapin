using System.Runtime.CompilerServices;
using Lapin.Geometry;

namespace Lapin.Alignments;

/// <summary>The station of a plan point: the foot of the perpendicular to the horizontal layer, and the signed offset (left positive).</summary>
public readonly record struct StationResult(double S, double Station, double Offset, double Distance, Vec2 Foot, int Segment);

/// <summary>Adaptive sampling options: largest chord deviation (m), largest and smallest step (m), and a sub-range of distance along.</summary>
public sealed record SampleOptions(double Chord = 0.005, double MaxStep = 20, double MinStep = 0.01, double? From = null, double? To = null);

/// <summary>
/// The whole alignment at a distance along: plan position, direction and
/// curvature, plus height / gradient where the vertical layer covers it and
/// rail cants where the cant layer does. Preparing caches the transition
/// integrals; <see cref="Of"/> keeps one prepared copy per alignment object,
/// so treat alignments as immutable (a changed one is a new record).
/// </summary>
public sealed class PreparedAlignment
{
    public Alignment Alignment { get; }
    public PreparedHorizontal Horizontal { get; }
    public PreparedVertical? Vertical { get; }
    public double StartStation => Alignment.StartStation;
    /// <summary>Horizontal length (the distance-along range is [0, Length]).</summary>
    public double Length => Horizontal.Length;

    static readonly ConditionalWeakTable<Alignment, PreparedAlignment> Cache = [];

    /// <summary>The prepared form of an alignment, cached per object.</summary>
    public static PreparedAlignment Of(Alignment a) => Cache.GetValue(a, x => new PreparedAlignment(x));

    public PreparedAlignment(Alignment alignment)
    {
        Alignment = alignment;
        Horizontal = new PreparedHorizontal(alignment.Horizontal, i =>
        {
            // A VIENNESEBEND's curvature depends on the cant over the same stretch.
            double start = 0;
            for (var k = 0; k < i; k++) start += alignment.Horizontal[k].Length;
            return Cant.AngleChangeAt(alignment.Cant, start);
        });
        Vertical = alignment.Vertical is { Count: > 0 } v ? new PreparedVertical(v) : null;
    }

    public CantPoint? CantAt(double s) => Alignment.Cant == null ? null : Cant.Evaluate(Alignment.Cant, s);

    /// <summary>The alignment at distance along <paramref name="s"/> (metres from the horizontal start; clamped).</summary>
    public AlignmentPoint At(double s) => new(s, StartStation + s, Horizontal.At(s), Vertical?.At(s), CantAt(s));

    /// <summary>The alignment at a station (StartStation + distance along).</summary>
    public AlignmentPoint AtStation(double station) => At(station - StartStation);

    // ─── The inverse: the station of a plan point ────────────────────────────

    const double Phi = 0.6180339887498949;

    static PreparedHorizontalSegment.Scan ScanOf(PreparedHorizontalSegment seg)
    {
        if (seg.ScanCache is { } sc) return sc;
        var L = seg.Length;
        var n = Math.Max(8, (int)Math.Ceiling(L / 2));
        var xy = new double[(n + 1) * 2];
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        for (var j = 0; j <= n; j++)
        {
            var q = seg.At(L * j / n);
            xy[j * 2] = q.X; xy[j * 2 + 1] = q.Y;
            minX = Math.Min(minX, q.X); minY = Math.Min(minY, q.Y); maxX = Math.Max(maxX, q.X); maxY = Math.Max(maxY, q.Y);
        }
        var pad = L / n + 1e-6;
        return seg.ScanCache = new(n, xy, minX - pad, minY - pad, maxX + pad, maxY + pad);
    }

    /// <summary>
    /// The station of a plan point — the foot of the perpendicular from (x, y)
    /// to the horizontal layer — and its signed offset (left positive). Each
    /// segment is scanned coarsely, the best bracket narrowed by golden section
    /// on the squared distance and finished with Newton on (P(s) − Q) · T(s) = 0,
    /// so the result is exact to ~1e-9 m for any segment type (CUBIC included,
    /// whose parameter is not arc length).
    /// </summary>
    public StationResult StationOf(double x, double y)
    {
        var h = Horizontal;
        if (h.Segments.Count == 0) throw new InvalidOperationException("The horizontal layer has no segments.");
        (double S, double D2, int Seg)? best = null;
        for (var i = 0; i < h.Segments.Count; i++)
        {
            var seg = h.Segments[i];
            var L = seg.Length;
            double D2(double s) { var q = seg.At(s); return (q.X - x) * (q.X - x) + (q.Y - y) * (q.Y - y); }
            var sc = ScanOf(seg);
            // A segment whose padded box is farther than the best foot so far cannot hold a nearer one.
            if (best is { } b0)
            {
                double dx = Math.Max(Math.Max(sc.MinX - x, 0), x - sc.MaxX), dy = Math.Max(Math.Max(sc.MinY - y, 0), y - sc.MaxY);
                if (dx * dx + dy * dy > b0.D2) continue;
            }
            var n = sc.N;
            var k = 0; var kd = double.PositiveInfinity;
            for (var j = 0; j <= n; j++) { var v = (sc.Xy[j * 2] - x) * (sc.Xy[j * 2] - x) + (sc.Xy[j * 2 + 1] - y) * (sc.Xy[j * 2 + 1] - y); if (v < kd) { kd = v; k = j; } }
            double a = L * Math.Max(0, k - 1) / n, b = L * Math.Min(n, k + 1) / n;
            double c = b - Phi * (b - a), d = a + Phi * (b - a);
            double fc = D2(c), fd = D2(d);
            for (var it = 0; it < 60 && b - a > 1e-9; it++)
            {
                if (fc < fd) { b = d; d = c; fd = fc; c = b - Phi * (b - a); fc = D2(c); }
                else { a = c; c = d; fc = fd; d = a + Phi * (b - a); fd = D2(d); }
            }
            var s = (a + b) / 2;
            // Newton on f(s) = (P − Q)·T with f′ = |dP/ds|² + κ(P − Q)·N ≈ 1 + κ(P − Q)·N.
            for (var it = 0; it < 8; it++)
            {
                var q = seg.At(s);
                double tx = Math.Cos(q.Direction), ty = Math.Sin(q.Direction);
                double rx = q.X - x, ry = q.Y - y;
                var f = rx * tx + ry * ty;
                var df = 1 + q.Curvature * (-rx * ty + ry * tx);
                if (Math.Abs(df) < 1e-9) break;
                var next = Math.Clamp(s - f / df, 0, L);
                if (Math.Abs(next - s) < 1e-12) { s = next; break; }
                s = next;
            }
            var v2 = D2(s);
            if (best == null || v2 < best.Value.D2 - 1e-18) best = (h.Starts[i] + s, v2, i);
        }
        var bb = best!.Value;
        var local = bb.S - h.Starts[bb.Seg];
        var p = h.Segments[bb.Seg].At(local);
        var offset = Math.Cos(p.Direction) * (y - p.Y) - Math.Sin(p.Direction) * (x - p.X);
        return new(bb.S, StartStation + bb.S, offset, Math.Sqrt(bb.D2), p.P, bb.Seg);
    }

    public StationResult StationOf(Vec2 p) => StationOf(p.X, p.Y);

    // ─── Adaptive sampling ───────────────────────────────────────────────────

    /// <summary>Segment boundaries of all three layers inside [from, to], sorted, with both ends.</summary>
    public List<double> Breakpoints(double from = 0, double to = double.PositiveInfinity)
    {
        var end = Math.Min(to, Length);
        var set = new SortedSet<double> { from, end };
        foreach (var s in Horizontal.Starts) set.Add(s);
        foreach (var v in Alignment.Vertical ?? []) { set.Add(v.StartDistAlong); set.Add(v.EndDistAlong); }
        foreach (var c in Alignment.Cant?.Segments ?? []) { set.Add(c.StartDistAlong); set.Add(c.EndDistAlong); }
        return set.Where(s => s >= from - 1e-9 && s <= end + 1e-9).ToList();
    }

    /// <summary>
    /// Distances along spaced so the chord between neighbours strays at most
    /// <c>Chord</c> metres from the curve — the sagitta of a step Δs on curvature κ
    /// is κΔs²/8, so Δs = √(8e/|κ|) — taking the larger of plan and profile
    /// curvature, capped by <c>MaxStep</c>. Every segment boundary of every layer
    /// is a sample, so corners of the curvature diagram are never cut.
    /// </summary>
    public List<double> SampleStations(SampleOptions? opts = null)
    {
        opts ??= new();
        double from = Math.Max(0, opts.From ?? 0), to = Math.Min(Length, opts.To ?? Length);
        double Kappa(double s) { var q = At(s); return Math.Max(Math.Abs(q.Curvature), Math.Abs(q.Profile?.Curvature ?? 0)); }
        double StepAt(double k) => k > 0 ? Math.Min(opts.MaxStep, Math.Max(opts.MinStep, Math.Sqrt(8 * opts.Chord / k))) : opts.MaxStep;
        var marks = Breakpoints(from, to);
        var o = new List<double> { marks[0] };
        for (var i = 1; i < marks.Count; i++)
        {
            double a = marks[i - 1], b = marks[i];
            if (b - a < 1e-9) continue;
            var s = a;
            while (s < b)
            {
                // Step from the curvature here and where the step lands, the larger of the two.
                var step = StepAt(Kappa(s));
                step = Math.Min(step, StepAt(Kappa(Math.Min(b, s + step))));
                // Spread the remainder evenly instead of leaving a sliver at the end.
                var left = b - s;
                var n = (int)Math.Ceiling(left / step - 1e-9);
                s = n <= 1 ? b : s + left / n;
                o.Add(s);
            }
        }
        return o;
    }

    /// <summary>The alignment sampled adaptively: points with plan, profile and cant values.</summary>
    public List<AlignmentPoint> Sample(SampleOptions? opts = null) => SampleStations(opts).Select(At).ToList();
}

public static class AlignmentExtensions
{
    public static PreparedAlignment Prepared(this Alignment a) => PreparedAlignment.Of(a);
    public static AlignmentPoint Evaluate(this Alignment a, double s) => PreparedAlignment.Of(a).At(s);
    public static AlignmentPoint EvaluateAtStation(this Alignment a, double station) => PreparedAlignment.Of(a).AtStation(station);
    public static StationResult StationOf(this Alignment a, double x, double y) => PreparedAlignment.Of(a).StationOf(x, y);
}
