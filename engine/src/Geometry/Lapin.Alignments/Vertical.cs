namespace Lapin.Alignments;

/// <summary>A vertical segment ready to evaluate: <see cref="At"/> gives the point <c>d</c> metres (along the horizontal) from its start.</summary>
public sealed class PreparedVerticalSegment
{
    public VerticalSegment Segment { get; }
    readonly Func<double, ProfilePoint> _at;
    internal PreparedVerticalSegment(VerticalSegment segment, Func<double, ProfilePoint> at) { Segment = segment; _at = at; }
    public ProfilePoint At(double d) => _at(d);
}

/// <summary>
/// Evaluating the vertical layer: height, gradient and curvature at a
/// distance along the horizontal. The x axis of every vertical segment is
/// the distance along (HorizontalLength is measured on it), so:
///
///   CONSTANTGRADIENT  z = z₀ + g₁d
///   PARABOLICARC      z = z₀ + g₁d + (g₂ − g₁) d² / 2L — the gradient changes
///                     linearly with d (EN 13803's vertical parabola)
///   CIRCULARARC       a true circle in the (d, z) plane from angle atan g₁ to
///                     atan g₂, so R = L / (sin θ₂ − sin θ₁) — derived, not read
///                     from RadiusOfCurvature (the reference files leave it $)
///   CLOTHOID          curvature linear in the 3D arc length σ,
///                     κ(σ) = κ₁ + (κ₂ − κ₁) σ / Lσ. IFC gives only g₁, g₂ and L:
///                     κ₁ is RadiusOfCurvature's (absent: 0, a clothoid leaving
///                     a constant grade), κ₂ and Lσ follow from reaching θ₂
///                     after exactly L of horizontal run.
///
/// Exact (no small-angle approximation): the reference set uses gradients up to 1:1.
/// </summary>
public sealed class PreparedVertical
{
    public IReadOnlyList<PreparedVerticalSegment> Segments { get; }
    public double Start { get; }
    public double End { get; }
    const double Eps = 1e-9;

    public PreparedVertical(IReadOnlyList<VerticalSegment> segments)
    {
        Segments = segments.Select(Prepare).ToList();
        Start = segments.Count > 0 ? segments[0].StartDistAlong : 0;
        End = segments.Count > 0 ? segments[^1].EndDistAlong : 0;
    }

    /// <summary>The layer at distance along <paramref name="d"/>, or null outside its extent.</summary>
    public ProfilePoint? At(double d)
    {
        if (Segments.Count == 0 || d < Start - Eps || d > End + Eps) return null;
        var i = 0;
        while (i + 1 < Segments.Count && Segments[i + 1].Segment.StartDistAlong <= d) i++;
        var seg = Segments[i].Segment;
        return Segments[i].At(Math.Clamp(d - seg.StartDistAlong, 0, seg.HorizontalLength));
    }

    public static PreparedVerticalSegment Prepare(VerticalSegment seg) => new(seg, seg.Type switch
    {
        VerticalType.ConstantGradient => Constant(seg),
        VerticalType.ParabolicArc => Parabola(seg),
        VerticalType.CircularArc => Circle(seg),
        _ => Clothoid(seg),
    });

    /// <summary>The layer at distance along <paramref name="d"/>, or null outside it.</summary>
    public static ProfilePoint? Evaluate(IReadOnlyList<VerticalSegment> segments, double d) => new PreparedVertical(segments).At(d);

    static Func<double, ProfilePoint> Constant(VerticalSegment seg)
    {
        var g = seg.StartGradient;
        return d => new(seg.StartHeight + g * d, g, 0);
    }

    static Func<double, ProfilePoint> Parabola(VerticalSegment seg)
    {
        double g1 = seg.StartGradient, rate = (seg.EndGradient - g1) / seg.HorizontalLength;
        return d =>
        {
            var g = g1 + rate * d;
            return new(seg.StartHeight + g1 * d + rate * d * d / 2, g, rate / Math.Pow(1 + g * g, 1.5));
        };
    }

    static Func<double, ProfilePoint> Circle(VerticalSegment seg)
    {
        double t1 = Math.Atan(seg.StartGradient), t2 = Math.Atan(seg.EndGradient);
        double s1 = Math.Sin(t1), c1 = Math.Cos(t1);
        if (t1 == t2) return Constant(seg);
        var r = seg.HorizontalLength / (Math.Sin(t2) - s1);
        return d =>
        {
            var s = Math.Clamp(s1 + d / r, -1, 1);
            var c = Math.Sqrt(1 - s * s);
            // R (cos θ₁ − cos θ), written without the cancellation.
            var dz = r * (s * s - s1 * s1) / (c1 + c);
            return new(seg.StartHeight + dz, s / c, 1 / r);
        };
    }

    /// <summary>
    /// A vertical CLOTHOID's own numbers: its 3D arc length Lσ and its start and
    /// end curvatures (1/m, in the (d, z) plane) — what reaching θ₂ after exactly
    /// L of horizontal run fixes. Null when it degenerates to a constant grade.
    /// </summary>
    public static (double Length, double K1, double K2)? ClothoidArc(VerticalSegment seg)
    {
        var L = seg.HorizontalLength;
        double t1 = Math.Atan(seg.StartGradient), t2 = Math.Atan(seg.EndGradient);
        var k1 = Alignment.CurvatureOf(seg.RadiusOfCurvature ?? 0);
        if (t1 == t2 && k1 == 0) return null;
        Func<double, double> ThetaFor(double ls)
        {
            var k2 = 2 * (t2 - t1) / ls - k1;
            return sg => t1 + k1 * sg + (k2 - k1) * sg * sg / (2 * ls);
        }
        double Run(double ls) { var th = ThetaFor(ls); return Quadrature.Integrate(sg => Math.Cos(th(sg)), 0, ls) - L; }
        // Secant on the 3D length, from the chord through the mean angle.
        double a = L / Math.Cos((t1 + t2) / 2), fa = Run(a);
        double b = a * 1.001, fb = Run(b);
        for (var i = 0; i < 60 && Math.Abs(fb) > 1e-13; i++)
        {
            var next = b - fb * (b - a) / (fb - fa);
            a = b; fa = fb; b = next; fb = Run(b);
        }
        return (b, k1, 2 * (t2 - t1) / b - k1);
    }

    static Func<double, ProfilePoint> Clothoid(VerticalSegment seg)
    {
        var L = seg.HorizontalLength;
        double t1 = Math.Atan(seg.StartGradient), t2 = Math.Atan(seg.EndGradient);
        if (ClothoidArc(seg) is not var (ls, k1, k2)) return Constant(seg);
        double Theta(double sg) => t1 + k1 * sg + (k2 - k1) * sg * sg / (2 * ls);
        return d =>
        {
            // σ with horizontal run d: Newton on x(σ) = d, dx/dσ = cos θ.
            var sg = d / Math.Cos(t1 + (t2 - t1) * d / L);
            for (var i = 0; i < 30; i++)
            {
                var xz = Quadrature.IntegrateHeading(Theta, 0, sg, 1e-13);
                var step = (xz.X - d) / Math.Cos(Theta(sg));
                sg -= step;
                if (Math.Abs(step) < 1e-13) break;
            }
            var at = Quadrature.IntegrateHeading(Theta, 0, sg, 1e-13);
            return new(seg.StartHeight + at.Y, Math.Tan(Theta(sg)), k1 + (k2 - k1) * sg / ls);
        };
    }
}
