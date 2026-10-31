using Lapin.Geometry;

namespace Lapin.Alignments;

/// <summary>A PI (point of intersection of two tangents): an inner one rounds with an arc of radius R, entered and left by clothoids.</summary>
public sealed record AlignmentPi(double X, double Y, double Radius = 0, double SpiralIn = 0, double SpiralOut = 0)
{
    public Vec2 P => new(X, Y);
    /// <summary>The curve at a PI as stored beside the path: radius, entry and exit spiral lengths.</summary>
    public PiCurve Curve => new(Radius, SpiralIn, SpiralOut);
}

/// <summary>What rounds a path vertex: a radius (0: a corner) and the clothoids either side.</summary>
public sealed record PiCurve(double Radius = 0, double SpiralIn = 0, double SpiralOut = 0)
{
    public static readonly PiCurve None = new();
    public bool IsCorner => !(Radius > 0);
}

public enum PiSegmentKind { Line, Arc, SpiralIn, SpiralOut }

/// <summary>Why a PI chain cannot be built: too few points, two that coincide, a turn without a radius, clothoids that turn more than the angle, a curve that overlaps the one before or runs past the next PI.</summary>
public enum PiProblem { TooFewPoints, Coincident, NoRadius, SpiralsTooLong, OverlapsPrevious, RunsPastNext }

/// <summary>
/// A PI chain that cannot be built, said in English (<see cref="Exception.Message"/>, PIs
/// counted from 1) and as data, for a program that says it in its own language:
/// the problem, the PI it is at (counted from 0) and, for a turn, its angle in degrees.
/// </summary>
public sealed class PiAlignmentException(PiProblem problem, int pi, double degrees, string message) : ArgumentException(message)
{
    public PiProblem Problem { get; } = problem;
    public int Pi { get; } = pi;
    public double Degrees { get; } = degrees;
}

/// <summary>One tangent-continuous piece of a PI chain: where it starts (distance along, point, direction CCW from +X) and how it turns (+1 left, −1 right).</summary>
public sealed record PiSegment(PiSegmentKind Kind, double S, double Length, Vec2 Start, double Direction, double Radius, int Turn, int Pi);

/// <summary>
/// The PI-based editing model of a horizontal alignment — a chain of PIs,
/// each inner one rounded by an arc of radius R between an entry and an exit
/// clothoid — resolved to line, spiral, arc, spiral, line…, and from there to
/// IFC 4.3 segments (<see cref="ToSegments"/>), which are what gets evaluated,
/// exported and diagnosed. Ported from BubbleBIM Standalone
/// (lib/civil/alignment.ts, align/fromPi.ts) by way of BubbleBIM Pro: at a PI
/// the arc centre lies R + p off each tangent (p the spiral's shift), so
/// unequal spirals need no special case. Distances along are metres from the
/// first PI.
/// </summary>
public sealed class PiAlignment
{
    public IReadOnlyList<PiSegment> Segments { get; }
    public double Length { get; }

    PiAlignment(List<PiSegment> segments, double length) { Segments = segments; Length = length; }

    static double Wrap(double a) => ((a + Math.PI) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI) - Math.PI;
    static Vec2 Rot(Vec2 p, double a) => new(p.X * Math.Cos(a) - p.Y * Math.Sin(a), p.X * Math.Sin(a) + p.Y * Math.Cos(a));

    /// <summary>A clothoid of length L reaching radius R: local coordinates l along it (x forward, y toward the turn).</summary>
    public static Vec2 ClothoidPoint(double l, double r, double len)
    {
        if (double.IsInfinity(r) || len <= 0) return new(l, 0);
        var a = 2 * r * len;
        double x = 0, y = 0, fact = 1;
        for (var k = 0; k < 7; k++)
        {
            var sign = k % 2 == 0 ? 1 : -1;
            var f2k = fact;
            x += sign * Math.Pow(l, 4 * k + 1) / ((4 * k + 1) * f2k * Math.Pow(a, 2 * k));
            var f2k1 = f2k * (2 * k + 1);
            y += sign * Math.Pow(l, 4 * k + 3) / ((4 * k + 3) * f2k1 * Math.Pow(a, 2 * k + 1));
            fact = f2k1 * (2 * k + 2);
        }
        return new(x, y);
    }

    /// <summary>A spiral's shift p and tangent offset k against the arc it leads into, and the angle it turns.</summary>
    static (double P, double K, double Theta) SpiralShift(double r, double len)
    {
        if (len <= 0) return (0, 0, 0);
        var theta = len / (2 * r);
        var s = ClothoidPoint(len, r, len);
        return (s.Y - r * (1 - Math.Cos(theta)), s.X - r * Math.Sin(theta), theta);
    }

    /// <summary>A segment evaluated with the PI model's own formulas (the IFC evaluator is the reference; this is for the round-trip proof).</summary>
    public static PlanPoint Eval(PiSegment seg, double l)
    {
        var (start, d, r, turn, len) = (seg.Start, seg.Direction, seg.Radius, seg.Turn, seg.Length);
        switch (seg.Kind)
        {
            case PiSegmentKind.Line:
                return new(start.X + Math.Cos(d) * l, start.Y + Math.Sin(d) * l, d, 0);
            case PiSegmentKind.Arc:
            {
                var c = turn / r;
                var a = d + c * l;
                return new(start.X + (Math.Sin(a) - Math.Sin(d)) / c, start.Y - (Math.Cos(a) - Math.Cos(d)) / c, a, c);
            }
            case PiSegmentKind.SpiralIn:
            {
                var q = ClothoidPoint(l, r, len);
                var p = start + Rot(new Vec2(q.X, q.Y * turn), d);
                return new(p.X, p.Y, d + turn * l * l / (2 * r * len), turn * l / (r * len));
            }
            default:
            {
                // Spiral out: the entry spiral run backwards from its far end (the curvature's sign flips on reversal).
                var endDir = d + turn * len / (2 * r);
                var q = ClothoidPoint(len, r, len);
                var disp = Rot(new Vec2(q.X, -q.Y * turn), endDir + Math.PI);
                var back = Eval(seg with { Kind = PiSegmentKind.SpiralIn, Start = start - disp, Direction = endDir + Math.PI, Turn = -turn }, len - l);
                return new(back.X, back.Y, Wrap(back.Direction + Math.PI), -back.Curvature);
            }
        }
    }

    /// <summary>The chain at a distance along (clamped), with the PI model's own formulas.</summary>
    public PlanPoint At(double s)
    {
        var c = Math.Clamp(s, 0, Length);
        var seg = Segments[^1];
        foreach (var x in Segments) if (c <= x.S + x.Length + 1e-9) { seg = x; break; }
        return Eval(seg, Math.Clamp(c - seg.S, 0, seg.Length));
    }

    /// <summary>Segment limits (TS, SC, CS, ST…), start and end included.</summary>
    public IEnumerable<double> CriticalStations() => Segments.Select(s => s.S).Append(0).Append(Length).Distinct().Order();

    /// <summary>
    /// The IFC 4.3 segments of the chain: line → LINE, arc → CIRCULARARC with the radius
    /// signed by the turn (left +), spiralIn → CLOTHOID ∞ → ±R, spiralOut → CLOTHOID ±R → ∞.
    /// </summary>
    public List<HorizontalSegment> ToSegments() => Segments.Select(g =>
    {
        var signed = g.Turn * g.Radius;
        return g.Kind switch
        {
            PiSegmentKind.Line => new HorizontalSegment(HorizontalType.Line, g.Start, g.Direction, 0, 0, g.Length),
            PiSegmentKind.Arc => new HorizontalSegment(HorizontalType.CircularArc, g.Start, g.Direction, signed, signed, g.Length),
            PiSegmentKind.SpiralIn => new HorizontalSegment(HorizontalType.Clothoid, g.Start, g.Direction, 0, signed, g.Length),
            _ => new HorizontalSegment(HorizontalType.Clothoid, g.Start, g.Direction, signed, 0, g.Length),
        };
    }).ToList();

    /// <summary>Why the PIs make no chain, or null.</summary>
    public static string? Problem(IReadOnlyList<AlignmentPi> pis)
    {
        try { Build(pis); return null; }
        catch (ArgumentException e) { return e.Message; }
    }

    /// <exception cref="PiAlignmentException">The chain cannot be built (the message says why, for the user; the exception, as data).</exception>
    public static PiAlignment Build(IReadOnlyList<AlignmentPi> pis)
    {
        if (pis.Count < 2) throw new PiAlignmentException(PiProblem.TooFewPoints, 0, 0, "An alignment needs two points (PIs) at least.");
        for (var i = 1; i < pis.Count; i++)
            if (Math.Abs(pis[i].X - pis[i - 1].X) + Math.Abs(pis[i].Y - pis[i - 1].Y) < 1e-6) throw new PiAlignmentException(PiProblem.Coincident, i, 0, $"PI {i + 1} coincides with PI {i}.");
        var segments = new List<PiSegment>();
        var cursor = pis[0].P;
        double s = 0;
        void Push(PiSegmentKind kind, Vec2 start, double dir, double length, double radius, int turn, int pi)
        {
            var seg = new PiSegment(kind, s, length, start, dir, radius, turn, pi);
            segments.Add(seg);
            s += length;
            cursor = Eval(seg, length).P;
        }
        for (var i = 1; i < pis.Count - 1; i++)
        {
            Vec2 prev = pis[i - 1].P, at = pis[i].P, next = pis[i + 1].P;
            var dIn = Math.Atan2(at.Y - prev.Y, at.X - prev.X);
            var dOut = Math.Atan2(next.Y - at.Y, next.X - at.X);
            var delta = Wrap(dOut - dIn);
            var r = pis[i].Radius;
            if (Math.Abs(delta) < 1e-9) continue;
            var deg = Math.Abs(delta) * 180 / Math.PI;
            if (!(r > 0)) throw new PiAlignmentException(PiProblem.NoRadius, i, deg, FormattableString.Invariant($"PI {i + 1} turns by {deg:0.0}° but has no radius."));
            var turn = delta > 0 ? 1 : -1;
            double l1 = Math.Max(0, pis[i].SpiralIn), l2 = Math.Max(0, pis[i].SpiralOut);
            var (s1, s2) = (SpiralShift(r, l1), SpiralShift(r, l2));
            var arcAngle = Math.Abs(delta) - s1.Theta - s2.Theta;
            if (arcAngle < -1e-9) throw new PiAlignmentException(PiProblem.SpiralsTooLong, i, deg, FormattableString.Invariant($"PI {i + 1}: the clothoids turn more than the {deg:0.0}° angle."));
            // The arc centre lies R + p off each tangent, on the inside of the turn.
            Vec2 u1 = new(Math.Cos(dIn), Math.Sin(dIn)), u2 = new(Math.Cos(dOut), Math.Sin(dOut));
            Vec2 n1 = new(-u1.Y * turn, u1.X * turn), n2 = new(-u2.Y * turn, u2.X * turn);
            var det = n1.X * n2.Y - n1.Y * n2.X;
            double c1 = r + s1.P, c2 = r + s2.P;
            var centre = new Vec2(at.X + (c1 * n2.Y - c2 * n1.Y) / det, at.Y + (c2 * n1.X - c1 * n2.X) / det);
            var ts = centre - u1 * s1.K - n1 * c1;
            var st = centre + u2 * s2.K - n2 * c2;
            var run = Vec2.Dot(ts - cursor, u1);
            if (run < -1e-6) throw new PiAlignmentException(PiProblem.OverlapsPrevious, i, deg, $"PI {i + 1}: its curve overlaps the one before; shorten the radius or the clothoids.");
            if (run > 1e-9) Push(PiSegmentKind.Line, cursor, dIn, run, double.PositiveInfinity, 1, i);
            if (l1 > 0) Push(PiSegmentKind.SpiralIn, ts, dIn, l1, r, turn, i);
            var arcDir = dIn + turn * s1.Theta;
            if (arcAngle > 1e-9) Push(PiSegmentKind.Arc, cursor, arcDir, r * arcAngle, r, turn, i);
            if (l2 > 0) Push(PiSegmentKind.SpiralOut, cursor, arcDir + turn * arcAngle, l2, r, turn, i);
            // Snap the numerical end onto the exact ST, so the next tangent starts clean.
            cursor = st;
            if (Vec2.Dot(next - st, u2) < -1e-6) throw new PiAlignmentException(PiProblem.RunsPastNext, i, deg, $"PI {i + 1}: its curve runs past PI {i + 2}; shorten the radius or the clothoids.");
        }
        var last = pis[^1].P;
        var tail = (last - cursor).Length;
        if (tail > 1e-9) Push(PiSegmentKind.Line, cursor, Math.Atan2(last.Y - cursor.Y, last.X - cursor.X), tail, double.PositiveInfinity, 1, pis.Count - 1);
        return new PiAlignment(segments, s);
    }

    /// <summary>The largest radius (no spirals) whose tangents fit between a PI's neighbours, with a little room to spare.</summary>
    public static double FitRadius(Vec2 prev, Vec2 at, Vec2 next, double wanted)
    {
        var delta = Math.Abs(Wrap(Math.Atan2(next.Y - at.Y, next.X - at.X) - Math.Atan2(at.Y - prev.Y, at.X - prev.X)));
        if (delta < 1e-9) return 0;
        var room = Math.Min((at - prev).Length, (next - at).Length) / 2 * 0.95;
        return Math.Min(wanted, room / Math.Tan(delta / 2));
    }

    /// <summary>PIs from a drawn polyline, every inner vertex rounded with <paramref name="radius"/> where it fits (smaller where not).</summary>
    public static List<AlignmentPi> FromPolyline(IReadOnlyList<Vec2> points, double radius) =>
        points.Select((p, i) => new AlignmentPi(p.X, p.Y,
            i > 0 && i < points.Count - 1 ? Math.Round(FitRadius(points[i - 1], p, points[i + 1], radius) * 100) / 100 : 0)).ToList();

    /// <summary>PIs from a path and the curve at each of its vertices (a missing or corner curve leaves the vertex sharp).</summary>
    public static List<AlignmentPi> Of(IReadOnlyList<Vec2> path, IReadOnlyList<PiCurve>? curves) =>
        path.Select((p, i) => { var c = curves != null && i < curves.Count ? curves[i] : PiCurve.None; return new AlignmentPi(p.X, p.Y, c.Radius, c.SpiralIn, c.SpiralOut); }).ToList();

    // ─── The profile: PVIs ↔ vertical segments ───────────────────────────────

    /// <summary>
    /// The vertical segments of a PVI profile (a PVI tangent → CONSTANTGRADIENT, a PVI's
    /// parabolic curve → PARABOLICARC, symmetric BVC … EVC). Profile stations are absolute;
    /// StartDistAlong is measured from the horizontal start, so pass the alignment's start station.
    /// </summary>
    public static List<VerticalSegment> VerticalFromPvis(IReadOnlyList<Pvi> pvis, double startStation = 0)
    {
        var p = pvis.OrderBy(v => v.Station).ToList();
        if (p.Count < 2) return [];
        var g = p.Skip(1).Select((v, i) => (v.Elevation - p[i].Elevation) / (v.Station - p[i].Station)).ToList();
        double Half(int i) => i > 0 && i < p.Count - 1 ? p[i].Length / 2 : 0;
        var o = new List<VerticalSegment>();
        for (var i = 0; i < p.Count - 1; i++)
        {
            // The tangent from the end of PVI i's curve to the start of PVI i+1's.
            double from = p[i].Station + Half(i), to = p[i + 1].Station - Half(i + 1);
            if (to - from > 1e-9) o.Add(new(VerticalType.ConstantGradient, from - startStation, to - from, p[i].Elevation + g[i] * Half(i), g[i], g[i]));
            var L = 2 * Half(i + 1);
            if (L > 0)
            {
                var pvi = p[i + 1];
                o.Add(new(VerticalType.ParabolicArc, pvi.Station - L / 2 - startStation, L, pvi.Elevation - g[i] * L / 2, g[i], g[i + 1]));
            }
        }
        return o;
    }

    /// <summary>
    /// The PVI profile of a vertical layer, when it is one: CONSTANTGRADIENT and
    /// PARABOLICARC segments tiling the distance along, each parabola between two
    /// tangents (or touching parabolas), height-continuous. A PVI sits at every grade
    /// break and at the middle of every parabola (where its tangents meet), with the
    /// parabola's length. Null when the layer is not that — a circular or clothoid
    /// vertical curve, a parabola at an end, a gap — checked by evaluating the result
    /// against the segments (&lt; 0.1 mm).
    /// </summary>
    public static List<Pvi>? PvisFromVertical(IReadOnlyList<VerticalSegment> segments, double startStation = 0)
    {
        if (segments.Count == 0) return null;
        if (segments.Any(v => v.Type is not (VerticalType.ConstantGradient or VerticalType.ParabolicArc))) return null;
        var first = segments[0]; var last = segments[^1];
        if (first.Type != VerticalType.ConstantGradient || last.Type != VerticalType.ConstantGradient) return null;
        var pvis = new List<Pvi> { new(startStation + first.StartDistAlong, first.StartHeight) };
        for (var i = 0; i < segments.Count; i++)
        {
            var v = segments[i];
            if (v.Type == VerticalType.ParabolicArc)
            {
                var L = v.HorizontalLength;
                pvis.Add(new(startStation + v.StartDistAlong + L / 2, v.StartHeight + v.StartGradient * L / 2, L));
            }
            else if (i > 0 && segments[i - 1].Type == VerticalType.ConstantGradient)
            {
                // Two tangents meeting: a grade break with no curve.
                pvis.Add(new(startStation + v.StartDistAlong, v.StartHeight));
            }
        }
        pvis.Add(new(startStation + last.EndDistAlong, last.StartHeight + last.StartGradient * last.HorizontalLength));
        if (VerticalProfile.Problem(pvis) != null) return null;
        // The proof: the PVI chain gives the segments' heights at every end and middle.
        var built = VerticalProfile.Build(pvis);
        var layer = new PreparedVertical(segments);
        foreach (var v in segments)
            foreach (var f in new[] { 0.0, 0.5, 1.0 })
            {
                var d = v.StartDistAlong + f * v.HorizontalLength;
                if (layer.At(d) is not { } want || Math.Abs(built.ElevationAt(startStation + d) - want.Z) > 1e-4) return null;
            }
        return pvis;
    }
}
