using Lapin.Geometry;

namespace Lapin.Alignments;

/// <summary>
/// The geometric representation of an alignment's layers in IFC 4.3: every
/// semantic segment mapped to its IfcCurveSegment(s) — the parent curve, the
/// Placement, SegmentStart and SegmentLength — as plain data for the export,
/// and an evaluator of those definitions on their own (it never looks at the
/// segment model), so the written curves can be checked against the
/// alignment evaluator.
///
/// The encoding is IFC 4.3 ADD2's, the same as ifcopenshell 0.8.5's alignment
/// API (the library the buildingSMART validation service runs on). A curve
/// segment cuts its parent curve from SegmentStart over SegmentLength
/// (negative: against the parent's sense) and is moved so that the parent's
/// point at SegmentStart, travelled in that sense, lands on Placement with its
/// tangent along Placement.RefDirection. Parent curves sit at their own origin.
///
///   horizontal (IfcCompositeCurve)
///     LINE          IfcLine
///     CIRCULARARC   IfcCircle |R|, SegmentLength = L · sign R
///     CLOTHOID      IfcClothoid A = L / √|f| · sign f, f = L (κ₂ − κ₁); SegmentStart = κ₁ A|A|
///     CUBIC         IfcPolynomialCurve x = u, y = A₃u³, A₃ = (κ₂ − κ₁) / 6L; SegmentStart = κ₁L / (κ₂ − κ₁)
///     HELMERTCURVE  two IfcSecondOrderPolynomialSpiral halves
///     BLOSSCURVE    IfcThirdOrderPolynomialSpiral
///     COSINECURVE   IfcCosineSpiral
///     SINECURVE     IfcSineSpiral
///     VIENNESEBEND  IfcSeventhOrderPolynomialSpiral (with the cant term, Δψ from the cant layer)
///   The spirals' curvature is κ(t) = Σ sign(Aᵢ) tⁱ / |Aᵢ|ⁱ⁺¹; the cosine and sine terms run over the
///   segment's length: κ = 1/A₀ + cos(πt/L)/A₁, κ = 1/A₀ + t/(A₁|A₁|) + sin(2πt/L)/A₂.
///
///   vertical (IfcGradientCurve over the composite curve, in the (distance along, height) plane)
///     CONSTANTGRADIENT  IfcLine, SegmentLength along the slope
///     PARABOLICARC      IfcPolynomialCurve x = u, y = z₀ + g₁u + (g₂ − g₁)u² / 2L, SegmentLength its arc length
///     CIRCULARARC       IfcCircle, R = L / |sin θ₂ − sin θ₁|
///     CLOTHOID          IfcClothoid in that plane (curvature linear in its arc length)
///
///   cant (IfcSegmentedReferenceCurve over the gradient curve): a function of the distance along,
///   y(u) = Placement.y + L² (κ(t) − κ(SegmentStart)) — the mean of the two rails' cants — with the
///   same parent curves as the horizontal transitions (LINEARTRANSITION: IfcClothoid; CONSTANTCANT:
///   IfcLine); Placement is 3D, its Axis tilted by the superelevation over the rail head distance.
///
/// Every layer ends with a zero-length segment (concept template "Alignment
/// Geometry": Transition DISCONTINUOUS); the other transition codes say how
/// each segment meets the next, from the alignment evaluator. Metres, radians.
/// </summary>
public static class IfcCurves
{
    const string Horizontal = "horizontal", Vertical = "vertical", CantLayerName = "cant";
    const string Continuous = "CONTINUOUS", SameGradient = "CONTSAMEGRADIENT", SameCurvature = "CONTSAMEGRADIENTSAMECURVATURE", Discontinuous = "DISCONTINUOUS";

    static double Sign(double v) => v < 0 ? -1 : 1;

    /// <summary>Aᵢ for L κ = Σ aᵢ ξⁱ: Aᵢ = L |aᵢ|^(−1/(i+1)) sign aᵢ; null when aᵢ = 0.</summary>
    static double? TermH(double a, double L, int i) => a == 0 ? null : L * Math.Pow(Math.Abs(a), -1.0 / (i + 1)) * Sign(a);
    /// <summary>Cant: Aᵢ = L^((i+2)/(i+1)) |aᵢ|^(−1/(i+1)) sign aᵢ.</summary>
    static double? TermC(double a, double L, int i) => a == 0 ? null : Math.Pow(L, (i + 2.0) / (i + 1)) * Math.Pow(Math.Abs(a), -1.0 / (i + 1)) * Sign(a);
    /// <summary>An exponent-1 term (the cosine and sine terms): A = scale / a.</summary>
    static double? TermOne(double a, double scale) => a == 0 ? null : scale / a;

    static double[] Dir2(double a) => [Math.Cos(a), Math.Sin(a)];

    /// <summary>A piece before its layer, source and transition are known.</summary>
    sealed record Piece(int Index, double[] Location, double[] RefDirection, double[]? Axis, double Start, double Length, IfcParentCurve Parent);

    static IfcParentCurve Circle(double radius, Vec2 centre) => new("circle", Radius: radius, Centre: centre);
    static IfcParentCurve Clothoid(double a) => new("clothoid", A: a);
    static IfcParentCurve Spiral(params double?[] terms) => new("spiral", Terms: terms);

    // ─── Horizontal ─────────────────────────────────────────────────────────

    static List<Piece> HorizontalPieces(HorizontalSegment seg, double dPsi)
    {
        var L = seg.Length;
        var k1 = Alignment.CurvatureOf(seg.StartRadius); var k2 = Alignment.CurvatureOf(seg.EndRadiusOf);
        var f = L * (k2 - k1);
        double[] loc = [seg.Start.X, seg.Start.Y]; var dir = Dir2(seg.StartDirection);
        List<Piece> Line() => [new(0, loc, dir, null, 0, L, IfcParentCurve.Line)];
        List<Piece> Arc(double k) => k == 0 ? Line() : [new(0, loc, dir, null, 0, L * Sign(k), Circle(Math.Abs(1 / k), default))];
        List<Piece> One(IfcParentCurve parent, double start = 0) => [new(0, loc, dir, null, start, L, parent)];
        var a0 = L * k1;
        switch (seg.Type)
        {
            case HorizontalType.Line: return Line();
            case HorizontalType.CircularArc: return Arc(k1 != 0 ? k1 : k2);
            case HorizontalType.Clothoid:
            {
                if (f == 0) return Arc(k1);
                var A = L / Math.Sqrt(Math.Abs(f)) * Sign(f);
                return One(Clothoid(A), k1 * A * Math.Abs(A));
            }
            case HorizontalType.Cubic:
            {
                if (k1 == k2) return Arc(k1);
                var a3 = (k2 - k1) / (6 * L);
                return One(new("polynomial", X: [0, 1], Y: [0, 0, 0, a3]), k1 * L / (k2 - k1));
            }
            case HorizontalType.HelmertCurve:
            {
                if (f == 0) return Arc(k1);
                // κ L = a₀ + 2f ξ² on the first half, (a₀ − f) + 4f ξ − 2f ξ² on the second; the second starts where the first ends.
                var mid = PreparedHorizontal.Prepare(seg, dPsi).At(L / 2);
                return
                [
                    new(0, loc, dir, null, 0, L / 2, Spiral(TermH(a0, L, 0), null, TermH(2 * f, L, 2))),
                    new(1, [mid.X, mid.Y], Dir2(mid.Direction), null, L / 2, L / 2, Spiral(TermH(a0 - f, L, 0), TermH(4 * f, L, 1), TermH(-2 * f, L, 2))),
                ];
            }
            case HorizontalType.BlossCurve:
                return f == 0 ? Arc(k1) : One(Spiral(TermH(a0, L, 0), null, TermH(3 * f, L, 2), TermH(-2 * f, L, 3)));
            case HorizontalType.CosineCurve:
                return f == 0 ? Arc(k1) : One(new("cosine", Constant: TermOne(0.5 * f + a0, L), Trig: TermOne(-0.5 * f, L)!.Value));
            case HorizontalType.SineCurve:
                return f == 0 ? Arc(k1) : One(new("sine", Constant: TermOne(a0, L), Linear: TermH(f, L, 1), Trig: TermOne(-f / (2 * Math.PI), L)!.Value));
            case HorizontalType.VienneseBend:
            {
                // −420 h Δψ / L on the gravity-centre term, ψ in the small-angle form D / b.
                var c = -420 * ((seg.GravityCenterLineHeight ?? 0) / L) * dPsi;
                if (f == 0 && c == 0) return Arc(k1);
                double[] a = [a0, 0, c, -4 * c, 5 * c + 35 * f, -2 * c - 84 * f, 70 * f, -20 * f];
                return One(Spiral(a.Select((v, i) => TermH(v, L, i)).ToArray()));
            }
            default: return Line();
        }
    }

    // ─── Vertical ───────────────────────────────────────────────────────────

    /// <summary>∫₀ᴸ √(1 + (B + 2Cu)²) du — a parabola's arc length.</summary>
    static double ParabolaLength(double B, double C, double L)
    {
        if (C == 0) return L * Math.Sqrt(1 + B * B);
        double F(double u) { var t = B + 2 * C * u; var r = Math.Sqrt(1 + t * t); return (t * r + Math.Asinh(t)) / (4 * C); }
        return F(L) - F(0);
    }

    static List<Piece> VerticalPieces(VerticalSegment seg)
    {
        double L = seg.HorizontalLength, d0 = seg.StartDistAlong, z0 = seg.StartHeight, g1 = seg.StartGradient, g2 = seg.EndGradient;
        double t1 = Math.Atan(g1), t2 = Math.Atan(g2);
        double[] loc = [d0, z0]; double[] dir = [Math.Cos(t1), Math.Sin(t1)];
        List<Piece> Line() => [new(0, loc, dir, null, 0, L / Math.Cos(t1), IfcParentCurve.Line)];
        switch (seg.Type)
        {
            case VerticalType.ConstantGradient: return Line();
            case VerticalType.ParabolicArc:
            {
                var C = (g2 - g1) / (2 * L);
                return [new(0, loc, dir, null, 0, ParabolaLength(g1, C, L), new("polynomial", X: [0, 1], Y: [z0, g1, C]))];
            }
            case VerticalType.CircularArc:
            {
                if (t1 == t2) return Line();
                if (t1 < t2)
                {
                    var R = L / (Math.Sin(t2) - Math.Sin(t1));
                    return [new(0, loc, dir, null, R * (t1 + 1.5 * Math.PI), R * (t2 - t1), Circle(R, new(-R * Math.Sin(t1), R * Math.Cos(t1))))];
                }
                var Rc = L / (Math.Sin(t1) - Math.Sin(t2));
                return [new(0, loc, dir, null, Rc * (t1 + 0.5 * Math.PI), Rc * (t2 - t1), Circle(Rc, new(Rc * Math.Sin(t1), -Rc * Math.Cos(t1))))];
            }
            case VerticalType.Clothoid:
            {
                if (PreparedVertical.ClothoidArc(seg) is not { } arc) return Line();
                // Constant curvature: a circle in that plane (the parent at its origin, run clockwise for a crest).
                if (arc.K1 == arc.K2) return [new(0, loc, dir, null, 0, arc.Length * Sign(arc.K1), Circle(Math.Abs(1 / arc.K1), default))];
                var A = Math.Sqrt(arc.Length / Math.Abs(arc.K2 - arc.K1)) * Sign(arc.K2 - arc.K1);
                return [new(0, loc, dir, null, arc.K1 * A * Math.Abs(A), arc.Length, Clothoid(A))];
            }
            default: return Line();
        }
    }

    // ─── Cant ───────────────────────────────────────────────────────────────

    /// <summary>The placement's Axis for a superelevation D over b: (0, D, b) normalised.</summary>
    static double[] CantAxis(double superelevation, double b)
    {
        var d = Math.Sqrt(superelevation * superelevation + b * b); if (d == 0) d = 1;
        return [0, superelevation / d, b / d];
    }

    static List<Piece> CantPieces(CantSegment seg, double b)
    {
        double L = seg.HorizontalLength, d0 = seg.StartDistAlong;
        double sl = seg.StartCantLeft, sr = seg.StartCantRight, el = seg.EndLeft, er = seg.EndRight;
        double Ds = (sl + sr) / 2, De = (el + er) / 2, f = De - Ds;
        Piece At(double y, double slope, double superelevation, double start, double length, IfcParentCurve parent, int index = 0, double? x = null)
        {
            var t = Math.Atan(slope);
            return new(index, [x ?? d0, y, 0], [Math.Cos(t), Math.Sin(t), 0], CantAxis(superelevation, b), start, length, parent);
        }
        List<Piece> Flat() => [At(Ds, 0, sr - sl, 0, L, IfcParentCurve.Line)];
        if (seg.Type == CantType.ConstantCant || (f == 0 && el - sl == 0 && er - sr == 0)) return Flat();
        List<Piece> One(IfcParentCurve parent, double slope = 0) => [At(Ds, slope, sr - sl, 0, L, parent)];
        switch (seg.Type)
        {
            case CantType.LinearTransition:
            {
                var A = TermC(f, L, 1);
                return A is null ? Flat() : One(Clothoid(A.Value), f / L);
            }
            case CantType.HelmertCurve:
            {
                // The sums of the two rails (Σ = 2 · mean) over the full length, each half scaled by its own (L/2)².
                double S = 2 * Ds, F = 2 * f;
                var mid = Cant.EvaluateSegment(seg, L / 2, b);
                return
                [
                    At(Ds, 0, sr - sl, 0, L / 2, Spiral(TermC(2 * S, L, 0), null, TermC(4 * F, L, 2))),
                    At(Ds + f / 2, 2 * f / L, mid.Superelevation, L / 2, L / 2, Spiral(TermC(-2 * F + 2 * S, L, 0), TermC(8 * F, L, 1), TermC(-4 * F, L, 2)), 1, d0 + L / 2),
                ];
            }
            case CantType.BlossCurve: return One(Spiral(TermC(Ds, L, 0), null, TermC(3 * f, L, 2), TermC(-2 * f, L, 3)));
            case CantType.CosineCurve: return One(new("cosine", Constant: TermOne(Ds + 0.5 * f, L * L), Trig: TermOne(-0.5 * f, L * L)!.Value));
            case CantType.SineCurve: return One(new("sine", Constant: TermOne(Ds, L * L), Linear: TermC(f, L, 1), Trig: TermOne(-f / (2 * Math.PI), L * L)!.Value));
            case CantType.VienneseBend:
            {
                double[] a = [Ds, 0, 0, 0, 35 * f, -84 * f, 70 * f, -20 * f];
                return One(Spiral(a.Select((v, i) => TermC(v, L, i)).ToArray()));
            }
            default: return Flat();
        }
    }

    // ─── Transition codes ───────────────────────────────────────────────────

    readonly record struct EndState(double[] P, double Dir, double K);

    static string Code(EndState a, EndState b)
    {
        var dist = Math.Sqrt(a.P.Select((v, i) => (v - b.P[i]) * (v - b.P[i])).Sum());
        if (dist > 1e-5) return Discontinuous;
        var dd = Math.Atan2(Math.Sin(a.Dir - b.Dir), Math.Cos(a.Dir - b.Dir));
        if (Math.Abs(dd) > 1e-7) return Continuous;
        return Math.Abs(a.K - b.K) > 1e-7 * Math.Max(1, Math.Abs(a.K)) ? SameGradient : SameCurvature;
    }

    static (EndState Start, EndState End) HorizontalEnds(HorizontalSegment seg, double dPsi)
    {
        var p = PreparedHorizontal.Prepare(seg, dPsi);
        var s = p.At(0); var e = p.At(seg.Length);
        return (new([s.X, s.Y], s.Direction, s.Curvature), new([e.X, e.Y], e.Direction, e.Curvature));
    }

    static (EndState Start, EndState End) VerticalEnds(VerticalSegment seg)
    {
        var p = PreparedVertical.Prepare(seg);
        var s = p.At(0); var e = p.At(seg.HorizontalLength);
        return (new([seg.StartDistAlong, s.Z], Math.Atan(s.Gradient), s.Curvature), new([seg.EndDistAlong, e.Z], Math.Atan(e.Gradient), e.Curvature));
    }

    static (EndState Start, EndState End) CantEnds(CantSegment seg, double b)
    {
        var L = seg.HorizontalLength; var h = Math.Min(1e-3, L / 10); if (h == 0) h = 1e-3;
        double Mean(double d) { var c = Cant.EvaluateSegment(seg, Math.Clamp(d, 0, L), b); return (c.CantLeft + c.CantRight) / 2; }
        double Slope(double d) => (Mean(d + h / 2) - Mean(d - h / 2)) / h;
        return (new([seg.StartDistAlong, Mean(0)], Math.Atan(Slope(h / 2)), 0), new([seg.EndDistAlong, Mean(L)], Math.Atan(Slope(L - h / 2)), 0));
    }

    // ─── The layers ─────────────────────────────────────────────────────────

    static List<IfcCurveSegmentSpec> Finish(string layer, List<List<Piece>> pieces, List<(EndState Start, EndState End)> ends, Piece zero)
    {
        var o = new List<IfcCurveSegmentSpec>();
        for (var i = 0; i < pieces.Count; i++)
        {
            var ps = pieces[i];
            for (var k = 0; k < ps.Count; k++)
            {
                var p = ps[k];
                // Inside a Helmert pair the halves meet with the same gradient and curvature.
                EndState? next = k + 1 < ps.Count ? null : i + 1 < ends.Count ? ends[i + 1].Start : new EndState(zero.Location.Take(ends[i].End.P.Length).ToArray(), ends[i].End.Dir, 0);
                var transition = next is { } n ? Code(ends[i].End, n) : SameCurvature;
                o.Add(new(layer, i, p.Index, transition, p.Location, p.RefDirection, p.Axis, p.Start, p.Length, p.Parent));
            }
        }
        o.Add(new(layer, -1, 0, Discontinuous, zero.Location, zero.RefDirection, zero.Axis, 0, 0, IfcParentCurve.Line));
        return o;
    }

    /// <summary>An alignment's layers as IfcCurveSegments (each layer closed by its zero-length segment).</summary>
    public static IfcAlignmentCurves Of(Alignment a)
    {
        var hPieces = new List<List<Piece>>(); var hEnds = new List<(EndState, EndState)>();
        double s = 0;
        foreach (var seg in a.Horizontal)
        {
            var dPsi = seg.Type == HorizontalType.VienneseBend ? Cant.AngleChangeAt(a.Cant, s) : 0;
            hPieces.Add(HorizontalPieces(seg, dPsi));
            hEnds.Add(HorizontalEnds(seg, dPsi));
            s += seg.Length;
        }
        var hLast = hEnds.Count > 0 ? hEnds[^1].Item2 : new EndState([0, 0], 0, 0);
        var horizontal = Finish(Horizontal, hPieces, hEnds, new(0, hLast.P, Dir2(hLast.Dir), null, 0, 0, IfcParentCurve.Line));

        List<IfcCurveSegmentSpec>? vertical = null;
        if (a.Vertical is { Count: > 0 } vs)
        {
            var vEnds = vs.Select(VerticalEnds).ToList();
            var last = vEnds[^1].End;
            vertical = Finish(Vertical, vs.Select(VerticalPieces).ToList(), vEnds, new(0, last.P, Dir2(last.Dir), null, 0, 0, IfcParentCurve.Line));
        }

        List<IfcCurveSegmentSpec>? cant = null;
        if (a.Cant is { Segments.Count: > 0 } layer && vertical != null)
        {
            var b = layer.RailHeadDistance;
            var cEnds = layer.Segments.Select(c => CantEnds(c, b)).ToList();
            var lastSeg = layer.Segments[^1];
            var last = cEnds[^1].End;
            cant = Finish(CantLayerName, layer.Segments.Select(c => CantPieces(c, b)).ToList(), cEnds,
                new(0, [last.P[0], last.P[1], 0], [1, 0, 0], CantAxis(lastSeg.EndRight - lastSeg.EndLeft, b), 0, 0, IfcParentCurve.Line));
        }
        return new(horizontal, vertical, cant);
    }

    // ─── Evaluating the definitions ─────────────────────────────────────────

    /// <summary>κ(t) of a parent curve (the cosine and sine terms over <paramref name="period"/>, the segment's length).</summary>
    public static double ParentCurvature(IfcParentCurve c, double t, double period) => c.Kind switch
    {
        "circle" => 1 / c.Radius,
        "clothoid" => t / (c.A * Math.Abs(c.A)),
        "spiral" => c.Terms!.Select((A, i) => A is { } a ? Sign(a) * Math.Pow(t, i) / Math.Pow(Math.Abs(a), i + 1) : 0).Sum(),
        "cosine" => (c.Constant is { } k0 ? 1 / k0 : 0) + Math.Cos(Math.PI * t / period) / c.Trig,
        "sine" => (c.Constant is { } k1 ? 1 / k1 : 0) + (c.Linear is { } l ? t / (l * Math.Abs(l)) : 0) + Math.Sin(2 * Math.PI * t / period) / c.Trig,
        _ => 0,
    };

    /// <summary>θ(t) = ∫₀ᵗ κ, closed form.</summary>
    static double ParentAngle(IfcParentCurve c, double t, double period)
    {
        switch (c.Kind)
        {
            case "line": return 0;
            case "circle": return t / c.Radius + Math.PI / 2;
            case "polynomial":
            {
                var dx = c.X!.Select((v, i) => i == 0 ? 0 : i * v * Math.Pow(t, i - 1)).Sum();
                var dy = c.Y!.Select((v, i) => i == 0 ? 0 : i * v * Math.Pow(t, i - 1)).Sum();
                return Math.Atan2(dy, dx);
            }
            case "clothoid": return t * t / (2 * c.A * Math.Abs(c.A));
            case "spiral": return c.Terms!.Select((A, i) => A is { } a ? Sign(a) * Math.Pow(t, i + 1) / ((i + 1) * Math.Pow(Math.Abs(a), i + 1)) : 0).Sum();
            case "cosine": return (c.Constant is { } k0 ? t / k0 : 0) + period / (Math.PI * c.Trig) * Math.Sin(Math.PI * t / period);
            case "sine": return (c.Constant is { } k1 ? t / k1 : 0) + (c.Linear is { } l ? t * t / (2 * l * Math.Abs(l)) : 0) + period / (2 * Math.PI * c.Trig) * (1 - Math.Cos(2 * Math.PI * t / period));
            default: return 0;
        }
    }

    /// <summary>A parent curve's point at parameter t, in its own coordinates.</summary>
    static Vec2 ParentPoint(IfcParentCurve c, double t, double period)
    {
        switch (c.Kind)
        {
            case "line": return new(t, 0);
            case "circle": return new(c.Centre.X + c.Radius * Math.Cos(t / c.Radius), c.Centre.Y + c.Radius * Math.Sin(t / c.Radius));
            case "polynomial": return new(c.X!.Select((v, i) => v * Math.Pow(t, i)).Sum(), c.Y!.Select((v, i) => v * Math.Pow(t, i)).Sum());
            default:
            {
                double Theta(double u) => ParentAngle(c, u, period);
                return t >= 0 ? Quadrature.IntegrateHeading(Theta, 0, t, 1e-12, 20) : -Quadrature.IntegrateHeading(Theta, t, 0, 1e-12, 20);
            }
        }
    }

    public readonly record struct CurvePoint(double X, double Y, double Direction);

    /// <summary>
    /// A curve segment <paramref name="u"/> from its start (0 ≤ u ≤ |SegmentLength|,
    /// in the parent's parameter): the parent's point moved by the segment's
    /// placement. Plane curves only (horizontal and vertical); the cant is
    /// <see cref="EvaluateCantFunction"/>.
    /// </summary>
    public static CurvePoint EvaluateCurveSegment(IfcCurveSegmentSpec spec, double u)
    {
        var period = Math.Abs(spec.Length);
        var sense = spec.Length < 0 ? -1 : 1;
        var t0 = spec.Start; var t = t0 + sense * u;
        var p0 = ParentPoint(spec.Parent, t0, period); var p = ParentPoint(spec.Parent, t, period);
        var travel0 = ParentAngle(spec.Parent, t0, period) + (sense < 0 ? Math.PI : 0);
        var rot = Math.Atan2(spec.RefDirection[1], spec.RefDirection[0]) - travel0;
        double dx = p.X - p0.X, dy = p.Y - p0.Y;
        double cs = Math.Cos(rot), sn = Math.Sin(rot);
        return new(spec.Location[0] + dx * cs - dy * sn, spec.Location[1] + dx * sn + dy * cs, ParentAngle(spec.Parent, t, period) + (sense < 0 ? Math.PI : 0) + rot);
    }

    /// <summary>A cant curve segment's value (the mean of the two rails' cants) <paramref name="u"/> metres from its start.</summary>
    public static double EvaluateCantFunction(IfcCurveSegmentSpec spec, double u)
    {
        var L = Math.Abs(spec.Length);
        if (spec.Parent.Kind == "line") return spec.Location[1] + u * Math.Tan(Math.Atan2(spec.RefDirection[1], spec.RefDirection[0]));
        return spec.Location[1] + L * L * (ParentCurvature(spec.Parent, spec.Start + u, L) - ParentCurvature(spec.Parent, spec.Start, L));
    }

    /// <summary>A vertical curve segment's height at a distance along (bisection on its x, which runs with the distance).</summary>
    public static (double Z, double Gradient) EvaluateGradientAt(IfcCurveSegmentSpec spec, double distanceAlong)
    {
        double lo = 0, hi = Math.Abs(spec.Length);
        for (var i = 0; i < 200; i++)
        {
            var mid = (lo + hi) / 2;
            if (EvaluateCurveSegment(spec, mid).X < distanceAlong) lo = mid; else hi = mid;
            if (hi - lo < 1e-13) break;
        }
        var q = EvaluateCurveSegment(spec, (lo + hi) / 2);
        return (q.Y, Math.Tan(q.Direction));
    }

    // ─── Checking the definitions against the alignment ─────────────────────

    /// <summary>The largest deviation of a layer's written curves from the alignment evaluator (metres: plan distance, height, mean cant), and where.</summary>
    public sealed record CurveCheck(string Layer, int Points, double Max, int Segment, double DistanceAlong);

    /// <summary>Every curve segment evaluated on its own at <paramref name="perSegment"/> points against the alignment; the acceptance is under 0.1 mm.</summary>
    public static List<CurveCheck> Check(Alignment a, IfcAlignmentCurves curves, int perSegment = 100)
    {
        var p = a.Prepared();
        var o = new List<CurveCheck>();
        Action<double, int, double> Track(string layer)
        {
            int n = 0, seg = -1; double max = 0, at = 0;
            o.Add(new(layer, 0, 0, -1, 0));
            var index = o.Count - 1;
            return (dev, segment, d) =>
            {
                n++;
                if (dev > max || !double.IsFinite(dev)) { max = double.IsFinite(dev) ? dev : double.PositiveInfinity; seg = segment; at = d; }
                o[index] = new(layer, n, max, seg, at);
            };
        }

        var h = Track(Horizontal);
        var offsets = new Dictionary<int, double>();
        foreach (var spec in curves.Horizontal)
        {
            if (spec.Source < 0)
            {
                var end = p.Horizontal.At(p.Length);
                h(Math.Sqrt(Math.Pow(spec.Location[0] - end.X, 2) + Math.Pow(spec.Location[1] - end.Y, 2)), -1, p.Length);
                continue;
            }
            var off = offsets.GetValueOrDefault(spec.Source);
            offsets[spec.Source] = off + Math.Abs(spec.Length);
            var n = Math.Abs(spec.Length) > 0 ? perSegment : 1;
            for (var i = 0; i <= n; i++)
            {
                var u = Math.Abs(spec.Length) * i / n;
                var q = EvaluateCurveSegment(spec, u);
                var e = p.Horizontal.Segments[spec.Source].At(off + u);
                h(Math.Sqrt(Math.Pow(q.X - e.X, 2) + Math.Pow(q.Y - e.Y, 2)), spec.Source, p.Horizontal.Starts[spec.Source] + off + u);
            }
        }

        if (curves.Vertical != null && a.Vertical is { Count: > 0 } vs && p.Vertical != null)
        {
            var v = Track(Vertical);
            foreach (var spec in curves.Vertical)
            {
                if (spec.Source < 0) continue;
                var seg = vs[spec.Source];
                for (var i = 0; i <= perSegment; i++)
                {
                    var d = seg.StartDistAlong + seg.HorizontalLength * i / perSegment;
                    var e = p.Vertical.Segments[spec.Source].At(d - seg.StartDistAlong);
                    v(Math.Abs(EvaluateGradientAt(spec, d).Z - e.Z), spec.Source, d);
                }
            }
        }

        if (curves.Cant != null && a.Cant is { Segments.Count: > 0 } layer)
        {
            var c = Track(CantLayerName);
            foreach (var spec in curves.Cant)
            {
                if (spec.Source < 0) continue;
                var len = Math.Abs(spec.Length);
                for (var i = 0; i <= perSegment; i++)
                {
                    var u = len * i / perSegment;
                    var d = spec.Location[0] + u;
                    if (Cant.Evaluate(layer, d) is not { } e) continue;
                    c(Math.Abs(EvaluateCantFunction(spec, u) - (e.CantLeft + e.CantRight) / 2), spec.Source, d);
                }
            }
        }
        return o;
    }
}
