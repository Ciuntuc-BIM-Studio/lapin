using Lapin.Geometry;

namespace Lapin.Alignments;

/// <summary>A horizontal segment ready to evaluate: <see cref="At"/> gives the point <c>s</c> metres from its start.</summary>
public sealed class PreparedHorizontalSegment
{
    public HorizontalSegment Segment { get; }
    public double Length => Segment.Length;
    readonly Func<double, PlanPoint> _at;
    internal Scan? ScanCache;

    internal PreparedHorizontalSegment(HorizontalSegment segment, Func<double, PlanPoint> at) { Segment = segment; _at = at; }

    public PlanPoint At(double s) => _at(s);

    /// <summary>A segment's coarse scan, kept for the inverse: its samples every L / n, and their box padded by the step (the curve between two samples stays within it).</summary>
    internal sealed record Scan(int N, double[] Xy, double MinX, double MinY, double MaxX, double MaxY);
}

/// <summary>
/// Evaluating the horizontal layer: position, direction and curvature at a
/// distance along. Closed forms for LINE, CIRCULARARC, CLOTHOID (Fresnel
/// series, quadrature past its range) and CUBIC (an explicit polynomial);
/// the other transitions have θ(s) = ∫κ in closed form (<see cref="Transitions"/>)
/// and x, y by adaptive Gauss–Legendre, cached at panel knots so a point costs
/// one short integral.
///
/// Every segment is placed by its own StartPoint / StartDirection, as IFC
/// says; whether the previous one actually ends there is <see cref="Continuity"/>'s
/// business.
/// </summary>
public sealed class PreparedHorizontal
{
    public IReadOnlyList<PreparedHorizontalSegment> Segments { get; }
    /// <summary>Distance along at each segment's start.</summary>
    public IReadOnlyList<double> Starts { get; }
    public double Length { get; }

    /// <summary>Knot spacing of the cached transition integral, metres.</summary>
    const double Knot = 10;

    /// <summary>
    /// A horizontal layer ready to evaluate. <paramref name="cantAngleChange"/> gives Δψ for a
    /// VIENNESEBEND segment i (its curvature depends on the cant over the same stretch).
    /// </summary>
    public PreparedHorizontal(IReadOnlyList<HorizontalSegment> segments, Func<int, double>? cantAngleChange = null)
    {
        var prepared = new List<PreparedHorizontalSegment>(segments.Count);
        var starts = new List<double>(segments.Count);
        double acc = 0;
        for (var i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            prepared.Add(Prepare(seg, seg.Type == HorizontalType.VienneseBend ? cantAngleChange?.Invoke(i) ?? 0 : 0));
            starts.Add(acc);
            acc += seg.Length;
        }
        Segments = prepared; Starts = starts; Length = acc;
    }

    /// <summary>Index of the segment holding s (the later one at a shared boundary).</summary>
    public int IndexAt(double s)
    {
        int lo = 0, hi = Segments.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) >> 1;
            if (Starts[mid] <= s) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>The layer at distance along <paramref name="s"/> (clamped to its length).</summary>
    public PlanPoint At(double s)
    {
        if (Segments.Count == 0) throw new InvalidOperationException("The horizontal layer has no segments.");
        var c = Math.Clamp(s, 0, Length);
        var i = IndexAt(c);
        return Segments[i].At(Math.Min(c - Starts[i], Segments[i].Length));
    }

    /// <summary>One segment prepared on its own. <paramref name="cantAngleChange"/>: VIENNESEBEND only, Δψ over the segment (radians, positive when the right rail rises relative to the left); 0 leaves the gravity-centre term out.</summary>
    public static PreparedHorizontalSegment Prepare(HorizontalSegment seg, double cantAngleChange = 0)
    {
        double k1 = Alignment.CurvatureOf(seg.StartRadius), k2 = Alignment.CurvatureOf(seg.EndRadius);
        Func<double, PlanPoint> at = seg.Type switch
        {
            HorizontalType.Line => Line(seg),
            HorizontalType.CircularArc => Arc(seg, k1 != 0 ? k1 : k2),
            HorizontalType.Cubic => Cubic(seg, k1, k2),
            HorizontalType.Clothoid => Clothoid(seg, k1, k2) ?? (k1 == k2 ? Arc(seg, k1) : Transition(seg, TransitionShape.Linear, k1, k2, 0)),
            _ => k1 == k2 && (seg.Type != HorizontalType.VienneseBend || cantAngleChange == 0)
                ? Arc(seg, k1)
                : Transition(seg, Transitions.Of(seg.Type)!.Value, k1, k2, seg.Type == HorizontalType.VienneseBend ? cantAngleChange : 0),
        };
        return new PreparedHorizontalSegment(seg, at);
    }

    /// <summary>One segment, <paramref name="s"/> metres from its start.</summary>
    public static PlanPoint Evaluate(HorizontalSegment seg, double s, double cantAngleChange = 0) => Prepare(seg, cantAngleChange).At(s);

    static Vec2 Rotate(double x, double y, double a) => new(x * Math.Cos(a) - y * Math.Sin(a), x * Math.Sin(a) + y * Math.Cos(a));

    static Func<double, PlanPoint> Line(HorizontalSegment seg)
    {
        var (x0, y0, d) = (seg.Start.X, seg.Start.Y, seg.StartDirection);
        double c = Math.Cos(d), sn = Math.Sin(d);
        return s => new(x0 + c * s, y0 + sn * s, d, 0);
    }

    static Func<double, PlanPoint> Arc(HorizontalSegment seg, double k)
    {
        var (x0, y0, d) = (seg.Start.X, seg.Start.Y, seg.StartDirection);
        if (k == 0) return Line(seg);
        // Chord 2 sin(ks/2)/k along the mid direction: no cancellation for small ks.
        return s =>
        {
            var half = k * s / 2;
            var chord = Math.Abs(half) < 1e-8 ? s * (1 - half * half / 6) : 2 * Math.Sin(half) / k;
            return new(x0 + chord * Math.Cos(d + half), y0 + chord * Math.Sin(d + half), d + k * s, k);
        };
    }

    /// <summary>
    /// Clothoid θ(u) = θ₀ + κ₁u + c u²/2, c = Δκ / L. Completing the square,
    /// u′ = u + κ₁ / c, the displacement is e^{iφ} √(2/|c|) [E(v₁) − E(v₀)] with
    /// v = u′ √(|c|/2) and E the Fresnel-type series (conjugated for c &lt; 0).
    /// Used while v² stays in the series' exact range, else null (quadrature).
    /// </summary>
    static Func<double, PlanPoint>? Clothoid(HorizontalSegment seg, double k1, double k2)
    {
        var L = seg.Length;
        var c = (k2 - k1) / L;
        if (c == 0) return null;
        var shift = k1 / c;
        var scale = Math.Sqrt(Math.Abs(c) / 2);
        double v0 = shift * scale, v1 = (shift + L) * scale;
        if (Math.Max(v0 * v0, v1 * v1) > Transitions.FresnelMaxX2) return null;
        var sign = c > 0 ? 1 : -1;
        var phi = seg.StartDirection - k1 * k1 / (2 * c);
        var (e0r, e0i) = Transitions.FresnelE(v0);
        var f = Math.Sqrt(2 / Math.Abs(c));
        var (x0, y0) = (seg.Start.X, seg.Start.Y);
        return s =>
        {
            var (er, ei) = Transitions.FresnelE((shift + s) * scale);
            double dr = (er - e0r) * f, di = sign * (ei - e0i) * f;
            var d = Rotate(dr, di, phi);
            return new(x0 + d.X, y0 + d.Y, seg.StartDirection + k1 * s + c * s * s / 2, k1 + c * s);
        };
    }

    /// <summary>Any transition: θ(s) closed form, x, y integrated, cached at knots.</summary>
    static Func<double, PlanPoint> Transition(HorizontalSegment seg, TransitionShape kind, double k1, double k2, double dPsi)
    {
        double L = seg.Length, dk = k2 - k1, t0 = seg.StartDirection;
        var h = kind == TransitionShape.Viennese ? seg.GravityCenterLineHeight ?? 0 : 0;
        var bank = h * dPsi * 420; // the Viennese Bend's gravity-centre term, ×1/L² on κ
        double Theta(double s)
        {
            var xi = s / L;
            return t0 + k1 * s + dk * L * Transitions.Integral(kind, xi) - (bank != 0 ? bank / L * Transitions.ViennesePsiTermIntegral(xi) : 0);
        }
        double Curvature(double s)
        {
            var xi = s / L;
            return k1 + dk * Transitions.Shape(kind, xi) - (bank != 0 ? bank / (L * L) * Transitions.ViennesePsiTerm(xi) : 0);
        }
        var turnRate = Math.Max(Math.Abs(k1), Math.Abs(k2)) + Math.Abs(bank) / (L * L);
        var n = Math.Max(1, (int)Math.Ceiling(L / Knot));
        var step = L / n;
        var knots = new Vec2[n + 1];
        knots[0] = seg.Start;
        for (var i = 0; i < n; i++) knots[i + 1] = knots[i] + Quadrature.IntegrateHeading(Theta, i * step, (i + 1) * step, 1e-13, turnRate: turnRate);
        return s =>
        {
            var i = Math.Min(n - 1, Math.Max(0, (int)Math.Floor(s / step)));
            var from = i * step;
            // Integrate from the nearer knot (backwards when closer to the next one).
            var k = s - from > step / 2 ? i + 1 : i;
            var d = Quadrature.IntegrateHeading(Theta, k * step, s, 1e-13, turnRate: turnRate);
            var b = knots[k];
            return new(b.X + d.X, b.Y + d.Y, Theta(s), Curvature(s));
        };
    }

    /// <summary>
    /// CUBIC: the parent curve y = A₃u³ (IfcPolynomialCurve) cut where its
    /// second derivative 6A₃u equals the start and end curvatures —
    /// A₃ = (κ₂ − κ₁) / 6L, the cut starting at u₀ = κ₁ / 6A₃ — then moved so
    /// its start sits on StartPoint with its tangent along StartDirection.
    /// Distance along is the parameter u − u₀ (the parent's x), which is how
    /// IFC parameterises the polynomial and how the reference lists are drawn.
    /// With κ₁ = κ₂ there is no cubic: it degenerates to the arc / line.
    /// </summary>
    static Func<double, PlanPoint> Cubic(HorizontalSegment seg, double k1, double k2)
    {
        var L = seg.Length;
        if (k1 == k2) return Arc(seg, k1);
        var a3 = (k2 - k1) / (6 * L);
        var u0 = k1 / (6 * a3);
        var y0 = a3 * u0 * u0 * u0;
        var alpha = Math.Atan(3 * a3 * u0 * u0);
        var rot = seg.StartDirection - alpha;
        return s =>
        {
            var u = u0 + s;
            var d = Rotate(s, a3 * u * u * u - y0, rot);
            var slope = 3 * a3 * u * u;
            return new(seg.Start.X + d.X, seg.Start.Y + d.Y, rot + Math.Atan(slope), 6 * a3 * u / Math.Pow(1 + slope * slope, 1.5));
        };
    }
}
