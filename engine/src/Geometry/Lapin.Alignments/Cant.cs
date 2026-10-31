namespace Lapin.Alignments;

/// <summary>
/// Evaluating the cant layer: the left and right rail heights over the
/// profile at a distance along. Each rail follows the segment's base formula
/// on its own, D(s) = D₁ + f(ξ) ΔD with f from <see cref="Transitions"/>
/// (CONSTANTCANT: D₁ throughout). The bank angle is ψ = asin(D / b), D = right
/// − left and b the rail head distance.
/// </summary>
public static class Cant
{
    const double Eps = 1e-9;

    /// <summary>Bank angle of a superelevation D over rail head distance b.</summary>
    public static double BankAngle(double superelevation, double b) => b > 0 ? Math.Asin(Math.Clamp(superelevation / b, -1, 1)) : 0;

    /// <summary>One segment, <paramref name="d"/> metres from its start.</summary>
    public static CantPoint EvaluateSegment(CantSegment seg, double d, double railHeadDistance)
    {
        var f = Transitions.Of(seg.Type) is { } shape ? Transitions.Shape(shape, d / seg.HorizontalLength) : 0;
        var left = seg.StartCantLeft + f * (seg.EndLeft - seg.StartCantLeft);
        var right = seg.StartCantRight + f * (seg.EndRight - seg.StartCantRight);
        var superelevation = right - left;
        return new(left, right, superelevation, BankAngle(superelevation, railHeadDistance));
    }

    /// <summary>The layer at distance along <paramref name="d"/>, or null outside it.</summary>
    public static CantPoint? Evaluate(CantLayer layer, double d)
    {
        var segs = layer.Segments;
        if (segs.Count == 0) return null;
        if (d < segs[0].StartDistAlong - Eps || d > segs[^1].EndDistAlong + Eps) return null;
        var i = 0;
        while (i + 1 < segs.Count && segs[i + 1].StartDistAlong <= d) i++;
        var seg = segs[i];
        return EvaluateSegment(seg, Math.Clamp(d - seg.StartDistAlong, 0, seg.HorizontalLength), layer.RailHeadDistance);
    }

    /// <summary>
    /// Δψ over the cant segment that starts where a horizontal segment starts
    /// (the Viennese Bend's horizontal curvature depends on it), 0 when there is
    /// none. ψ follows the small-angle form the Viennese Bend formula is written
    /// in, ψ = D / b.
    /// </summary>
    public static double AngleChangeAt(CantLayer? layer, double startDistAlong)
    {
        if (layer == null || !(layer.RailHeadDistance > 0)) return 0;
        var seg = layer.Segments.FirstOrDefault(c => Math.Abs(c.StartDistAlong - startDistAlong) < 1e-6);
        if (seg == null) return 0;
        var b = layer.RailHeadDistance;
        var psi1 = (seg.StartCantRight - seg.StartCantLeft) / b;
        var psi2 = (seg.EndRight - seg.EndLeft) / b;
        return psi2 - psi1;
    }
}
