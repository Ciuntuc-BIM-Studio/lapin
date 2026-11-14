using Lapin.Drawing;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// An opening's height under a dimension line (report #92, decision 338), as Romanian and European drawings write it:
/// a dimension's segment that spans a door, a window or a bare wall opening (its two points on the opening's jambs,
/// along its width, both within the wall's thickness in a plan, within its height in an elevation or a section) has
/// the width over the line and the height under it, in the dimension style's unit and decimals, in the form the style
/// says (<see cref="DimStyle.OpeningHeight"/>). Read from the model each time the view is drawn, so it follows the
/// opening's size and its type; a segment with a text of its own writes no height.
/// </summary>
public static class OpeningHeights
{
    /// <summary>How far (m) a segment's end may be from an opening's jamb, along the dimension, and still span it (a reveal's plaster, a frame's rebate).</summary>
    public const double Reach = 0.03;
    /// <summary>How far (m) a measured point may lie outside the opening's box across the dimension (beside the wall's face in a plan).</summary>
    public const double Across = 0.05;
    /// <summary>The rows under the line apart, in text heights.</summary>
    public const double Pitch = 1.4;
    /// <summary>A sill this low (m) is none: a door's.</summary>
    const double NoSill = 0.001;

    /// <summary>The openings of every module's walls.</summary>
    public static List<WallOpening> Of(ViewContext c) => c.Services?.All<IWallOpenings>().SelectMany(s => s.Openings(c.Model)).ToList() ?? [];

    /// <summary>
    /// The opening a segment from <paramref name="a"/> to <paramref name="b"/> (the points it measures, along <paramref name="u"/>)
    /// spans, the nearest fit; null when none. A plan takes its own storey's openings (an opening above or below at the same place is another floor's).
    /// </summary>
    public static WallOpening? Spanned(ViewContext c, IReadOnlyList<WallOpening> openings, Vec2 a, Vec2 b, Vec2 u)
    {
        var n = u.Perp;
        var (ta, tb) = (Vec2.Dot(a, u), Vec2.Dot(b, u));
        var (lo, hi) = (Math.Min(ta, tb), Math.Max(ta, tb));
        if (hi - lo < 1e-3) return null;
        var (na, nb) = (Vec2.Dot(a, n), Vec2.Dot(b, n));
        var storey = c.Kind == "plan" ? c.View.Str("storeyId") : null;
        (WallOpening O, double Off)? best = null;
        foreach (var o in openings)
        {
            if (storey != null && o.StoreyId != null && o.StoreyId != storey) continue;
            // Its width must run along the dimension in this view (an opening seen edge on, or measured up its height, is not spanned).
            var w = c.Map(o.Right) - c.Map(o.Left);
            if (w.Length < 1e-3 || Math.Abs(Vec2.Cross(w.Normalized(), u)) > 0.1) continue;
            double t0 = double.MaxValue, t1 = double.MinValue, n0 = double.MaxValue, n1 = double.MinValue;
            foreach (var corner in o.Corners)
            {
                var q = c.Map(corner);
                var (t, s) = (Vec2.Dot(q, u), Vec2.Dot(q, n));
                (t0, t1, n0, n1) = (Math.Min(t0, t), Math.Max(t1, t), Math.Min(n0, s), Math.Max(n1, s));
            }
            var off = Math.Abs(lo - t0) + Math.Abs(hi - t1);
            if (Math.Abs(lo - t0) > Reach || Math.Abs(hi - t1) > Reach) continue;
            if (na < n0 - Across || na > n1 + Across || nb < n0 - Across || nb > n1 + Across) continue;
            if (best == null || off < best.Value.Off) best = (o, off);
        }
        return best?.O;
    }

    /// <summary>The rows written under the line, top down: the height alone (150), h=150, h=150 and p=90 (no p for a door), 150/90.</summary>
    public static List<string> Rows(string format, WallOpening o, string unit, int precision, bool suppressZeros = false)
    {
        var h = Standards.FormatDimension(o.Height, unit, precision, suppressZeros);
        var sill = o.Sill > NoSill ? Standards.FormatDimension(o.Sill, unit, precision, suppressZeros) : null;
        return format switch
        {
            "number" => [h],
            "h" => ["h=" + h],
            "hp" => sill != null ? ["h=" + h, "p=" + sill] : ["h=" + h],
            "slash" => [sill != null ? $"{h}/{sill}" : h],
            _ => [],
        };
    }
}
