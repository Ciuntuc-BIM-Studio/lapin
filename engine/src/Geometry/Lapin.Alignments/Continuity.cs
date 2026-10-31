namespace Lapin.Alignments;

public enum ContinuityKind { G0, G1, G2, Gap, Overlap, Coverage, Inconsistent }
public enum AlignmentLayer { Horizontal, Vertical, Cant }

/// <summary>
/// One continuity finding: the layer, the segment it is about (for joints: the later one),
/// its kind and severity, its size — metres (G0, gap, overlap, coverage), radians (plan G1),
/// gradient ratio (profile G1), 1/m (G2) — and the message.
/// </summary>
public sealed record ContinuityDiagnostic(AlignmentLayer Layer, int Index, ContinuityKind Kind, string Severity, double Size, string Message);

/// <summary>Tolerances: metres (default 1e-4: 0.1 mm), radians for plan direction and ratio for gradient (1e-6), 1/m (1e-7).</summary>
public sealed record ContinuityTolerances(double Position = 1e-4, double Tangent = 1e-6, double Curvature = 1e-7);

/// <summary>
/// Continuity between consecutive segments of each layer, as structured diagnostics:
///
///   G0  position — the previous segment's computed end vs this one's start — error
///   G1  tangent  — direction (plan) or gradient (profile) — warning: a kink
///   G2  curvature — info: a curvature jump (line → arc without a transition) is legal,
///       but worth seeing where a transition was meant
///   gap / overlap — profile and cant segments must tile the distance along — error
///   coverage — the profile / cant layer does not span the horizontal — warning
///   inconsistent — a segment contradicting itself (CONSTANTGRADIENT with EndGradient ≠
///       StartGradient, CONSTANTCANT with different end cants, CIRCULARARC with different
///       start and end radii, LINE with a radius) — warning
/// </summary>
public static class Continuity
{
    static double Wrap(double a) { var t = (a + Math.PI) % (2 * Math.PI); return (t < 0 ? t + 2 * Math.PI : t) - Math.PI; }
    static string Mm(double m) => FormattableString.Invariant($"{m * 1000:0.0##} mm");
    static string Severity(ContinuityKind k) => k == ContinuityKind.G2 ? "info" : k is ContinuityKind.G1 or ContinuityKind.Coverage or ContinuityKind.Inconsistent ? "warning" : "error";

    public static List<ContinuityDiagnostic> Check(Alignment alignment, ContinuityTolerances? tol = null)
    {
        var t = tol ?? new();
        var o = new List<ContinuityDiagnostic>();
        void Push(AlignmentLayer layer, int index, ContinuityKind kind, double size, string message) => o.Add(new(layer, index, kind, Severity(kind), size, message));
        var prep = new PreparedAlignment(alignment);

        // Horizontal.
        var hs = prep.Horizontal.Segments;
        for (var i = 0; i < hs.Count; i++)
        {
            var seg = hs[i].Segment;
            if (seg.Type == HorizontalType.Line && (seg.StartRadius != 0 || seg.EndRadius != 0)) Push(AlignmentLayer.Horizontal, i, ContinuityKind.Inconsistent, 0, $"Horizontal segment {i} is a LINE but has a radius");
            if (seg.Type == HorizontalType.CircularArc && seg.StartRadius != seg.EndRadius) Push(AlignmentLayer.Horizontal, i, ContinuityKind.Inconsistent, Math.Abs(seg.StartRadius - seg.EndRadius), FormattableString.Invariant($"Horizontal segment {i} is a CIRCULARARC with start radius {seg.StartRadius} m ≠ end radius {seg.EndRadius} m (the start radius is used)"));
            if (i == 0) continue;
            var end = hs[i - 1].At(hs[i - 1].Length); var start = hs[i].At(0);
            var g0 = Math.Sqrt((start.X - end.X) * (start.X - end.X) + (start.Y - end.Y) * (start.Y - end.Y));
            if (g0 > t.Position) Push(AlignmentLayer.Horizontal, i, ContinuityKind.G0, g0, $"Horizontal segment {i} ({SegmentTypes.Ifc(seg.Type)}) starts {Mm(g0)} away from where segment {i - 1} ({SegmentTypes.Ifc(hs[i - 1].Segment.Type)}) ends");
            var g1 = Math.Abs(Wrap(start.Direction - end.Direction));
            if (g1 > t.Tangent) Push(AlignmentLayer.Horizontal, i, ContinuityKind.G1, g1, FormattableString.Invariant($"Kink of {g1 * 180 / Math.PI:0.0000}° between horizontal segments {i - 1} and {i}"));
            var g2 = Math.Abs(start.Curvature - end.Curvature);
            if (g2 > t.Curvature) Push(AlignmentLayer.Horizontal, i, ContinuityKind.G2, g2, FormattableString.Invariant($"Curvature jumps by {g2:0.000e+00} 1/m between horizontal segments {i - 1} and {i}"));
        }

        // Vertical.
        var vs = alignment.Vertical ?? [];
        var vp = prep.Vertical;
        for (var i = 0; i < vs.Count; i++)
        {
            var seg = vs[i];
            if (seg.Type == VerticalType.ConstantGradient && Math.Abs(seg.EndGradient - seg.StartGradient) > t.Tangent)
                Push(AlignmentLayer.Vertical, i, ContinuityKind.Inconsistent, Math.Abs(seg.EndGradient - seg.StartGradient), FormattableString.Invariant($"Vertical segment {i} is a CONSTANTGRADIENT with end gradient {seg.EndGradient} ≠ start gradient {seg.StartGradient} (the start gradient is used)"));
            if (i == 0 || vp == null) continue;
            var prev = vs[i - 1];
            var jump = seg.StartDistAlong - prev.EndDistAlong;
            if (jump > t.Position) Push(AlignmentLayer.Vertical, i, ContinuityKind.Gap, jump, $"Gap of {Mm(jump)} before vertical segment {i}");
            if (jump < -t.Position) Push(AlignmentLayer.Vertical, i, ContinuityKind.Overlap, -jump, $"Vertical segment {i} overlaps the previous one by {Mm(-jump)}");
            var end = vp.Segments[i - 1].At(prev.HorizontalLength); var start = vp.Segments[i].At(0);
            var g0 = Math.Abs(start.Z - end.Z);
            if (g0 > t.Position) Push(AlignmentLayer.Vertical, i, ContinuityKind.G0, g0, $"Vertical segment {i} starts {Mm(g0)} {(start.Z > end.Z ? "above" : "below")} the end of segment {i - 1}");
            var g1 = Math.Abs(start.Gradient - end.Gradient);
            if (g1 > t.Tangent) Push(AlignmentLayer.Vertical, i, ContinuityKind.G1, g1, FormattableString.Invariant($"Grade break of {g1 * 100:0.0000} % between vertical segments {i - 1} and {i}"));
            var g2 = Math.Abs(start.Curvature - end.Curvature);
            if (g2 > t.Curvature) Push(AlignmentLayer.Vertical, i, ContinuityKind.G2, g2, FormattableString.Invariant($"Vertical curvature jumps by {g2:0.000e+00} 1/m between vertical segments {i - 1} and {i}"));
        }
        if (vp != null) Coverage(AlignmentLayer.Vertical, vp.Start, vp.End, vs.Count);

        // Cant.
        var cl = alignment.Cant;
        var cs = cl?.Segments ?? [];
        for (var i = 0; i < cs.Count; i++)
        {
            var seg = cs[i];
            if (seg.Type == CantType.ConstantCant && (seg.EndLeft != seg.StartCantLeft || seg.EndRight != seg.StartCantRight))
                Push(AlignmentLayer.Cant, i, ContinuityKind.Inconsistent, Math.Max(Math.Abs(seg.EndLeft - seg.StartCantLeft), Math.Abs(seg.EndRight - seg.StartCantRight)), $"Cant segment {i} is CONSTANTCANT but its end cants differ from the start (the start values are used)");
            if (i == 0 || cl == null) continue;
            var prev = cs[i - 1];
            var jump = seg.StartDistAlong - prev.EndDistAlong;
            if (jump > t.Position) Push(AlignmentLayer.Cant, i, ContinuityKind.Gap, jump, $"Gap of {Mm(jump)} before cant segment {i}");
            if (jump < -t.Position) Push(AlignmentLayer.Cant, i, ContinuityKind.Overlap, -jump, $"Cant segment {i} overlaps the previous one by {Mm(-jump)}");
            var end = Cant.EvaluateSegment(prev, prev.HorizontalLength, cl.RailHeadDistance); var start = Cant.EvaluateSegment(seg, 0, cl.RailHeadDistance);
            var g0 = Math.Max(Math.Abs(start.CantLeft - end.CantLeft), Math.Abs(start.CantRight - end.CantRight));
            if (g0 > t.Position) Push(AlignmentLayer.Cant, i, ContinuityKind.G0, g0, $"Cant jumps by {Mm(g0)} between cant segments {i - 1} and {i}");
        }
        if (cs.Count > 0) Coverage(AlignmentLayer.Cant, cs[0].StartDistAlong, cs[^1].EndDistAlong, cs.Count);

        void Coverage(AlignmentLayer layer, double from, double to, int count)
        {
            double before = from, after = prep.Length - to;
            var name = layer.ToString().ToLowerInvariant();
            if (before > t.Position) Push(layer, 0, ContinuityKind.Coverage, before, FormattableString.Invariant($"The {name} layer starts {before:0.000} m after the horizontal"));
            if (after > t.Position) Push(layer, count - 1, ContinuityKind.Coverage, after, FormattableString.Invariant($"The {name} layer ends {after:0.000} m before the horizontal"));
        }

        return o;
    }
}
