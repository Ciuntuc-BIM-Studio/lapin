using Lapin.Geometry;

namespace Lapin.Alignments;

/// <summary>
/// Chaining: each segment of a layer starts where the one before it ends —
/// the editing aid that keeps an alignment G0 / G1 continuous while its
/// segments' lengths and radii change.
///
///   horizontal  StartPoint, StartDirection ← the previous segment's evaluated end;
///               a transition's StartRadius ← the previous end radius
///               (a transition exists to carry the curvature on)
///   vertical    StartDistAlong, StartHeight, StartGradient ← the previous end
///               (a CONSTANTGRADIENT's EndGradient follows its start)
///   cant        StartDistAlong, StartCantLeft / Right ← the previous end
///               (a CONSTANTCANT keeps no end values of its own)
///
/// A line's or an arc's radius is not chained: it is the designer's choice,
/// and a jump in it (line → arc) is legal — <see cref="Continuity"/> reports it as G2 info.
/// Pure: new lists, the segments before <c>from</c> untouched.
/// </summary>
public static class Chaining
{
    static double Wrap(double a) { var t = (a + Math.PI) % (2 * Math.PI); return (t < 0 ? t + 2 * Math.PI : t) - Math.PI; }
    /// <summary>−0 → 0, so a chained value reads (and serialises) the way it was typed.</summary>
    static double Z(double v) => v == 0 ? 0 : v;

    /// <summary>Horizontal segments <paramref name="from"/> … end re-started at the previous segment's end (a VIENNESEBEND sees <paramref name="cant"/>).</summary>
    public static List<HorizontalSegment> Horizontal(IReadOnlyList<HorizontalSegment> segs, int from = 1, CantLayer? cant = null)
    {
        var o = segs.ToList();
        double start = 0;
        for (var i = 0; i < o.Count; i++)
        {
            if (i >= Math.Max(1, from))
            {
                var prev = o[i - 1];
                var p = PreparedHorizontal.Prepare(prev, Alignments.Cant.AngleChangeAt(cant, start - prev.Length));
                var e = p.At(p.Length);
                o[i] = o[i] with { Start = new Vec2(Z(e.X), Z(e.Y)), StartDirection = Z(Wrap(e.Direction)) };
                if (SegmentTypes.IsTransition(o[i].Type)) o[i] = o[i] with { StartRadius = prev.EndRadiusOf };
            }
            start += o[i].Length;
        }
        return o;
    }

    /// <summary>Vertical segments <paramref name="from"/> … end tiled after the previous one, at its end height and gradient.</summary>
    public static List<VerticalSegment> Vertical(IReadOnlyList<VerticalSegment> segs, int from = 1)
    {
        var o = segs.ToList();
        for (var i = Math.Max(1, from); i < o.Count; i++)
        {
            var prev = o[i - 1];
            var e = PreparedVertical.Prepare(prev).At(prev.HorizontalLength);
            var seg = o[i] with { StartDistAlong = Z(prev.EndDistAlong), StartHeight = Z(e.Z), StartGradient = Z(e.Gradient) };
            if (seg.Type == VerticalType.ConstantGradient) seg = seg with { EndGradient = seg.StartGradient };
            o[i] = seg;
        }
        return o;
    }

    /// <summary>Cant segments <paramref name="from"/> … end tiled after the previous one, at its end cants.</summary>
    public static CantLayer Cant(CantLayer layer, int from = 1)
    {
        var o = layer.Segments.ToList();
        for (var i = Math.Max(1, from); i < o.Count; i++)
        {
            var prev = o[i - 1];
            var seg = o[i] with { StartDistAlong = Z(prev.EndDistAlong), StartCantLeft = prev.EndLeft, StartCantRight = prev.EndRight };
            if (seg.Type == CantType.ConstantCant) seg = seg with { EndCantLeft = null, EndCantRight = null };
            o[i] = seg;
        }
        return layer with { Segments = o };
    }
}
