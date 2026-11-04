using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform.Coordination;

/// <summary>
/// The openings' voids on plans, sections and elevations (decision 290), as a
/// coordination drawing shows a provision for void: the void's outline where
/// the view cuts it (a wall's opening through the wall's thickness in a plan,
/// through its height in a section; a floor's opening through the slab), or
/// as seen (a floor opening below the cut, a wall's opening on the face an
/// elevation looks at), dashed when it lies above a plan's cut or inside a
/// wall under it (a sleeve below the cut plane); crossed
/// corner to corner, with its mark (V1, V2…, as <see cref="OpeningVoids.Of"/>
/// numbers them) and on request its size and a wall opening's bottom level
/// ("300×200 +2.45", "Ø200"). Drawn by the engine with the view's other
/// annotations, so sheets, the PDF and DXF carry them; only when the view's
/// style says (<see cref="ViewStyles.Style.Voids"/>, off by default, on in
/// the Coordination style). Their lines are the category <see cref="Category"/>
/// of the object styles (cut, projection, hidden, overhead) and of a view's graphics
/// (shown, colour, weight), on the layer <see cref="Layer"/>.
/// </summary>
public static class VoidDrawings
{
    /// <summary>The voids' category in object styles and in a view's graphics.</summary>
    public const string Category = "voids";
    /// <summary>The class the line styles read the category by (no element of the model has it).</summary>
    public const string Class = "LapinOpeningVoid";
    /// <summary>The layer their lines are on (DXF).</summary>
    public const string Layer = "VOIDS";
    /// <summary>Their pens (paper mm) unless styled: cut, seen, overhead.</summary>
    public static readonly (double Cut, double Seen, double Overhead) Widths = (0.35, 0.25, 0.18);

    /// <summary>How each void is drawn in a view (pieces: a developed section's, each with its station; one for any other view).</summary>
    public static List<ViewMark> Marks(IQueryContext q, ViewContext c, ViewSpec spec, IReadOnlyList<(ViewSpec Spec, double Station)> pieces, bool sizes,
        LineStyles.Resolver? styles, CategoryGraphics? graphics)
    {
        var m = q.Model;
        var view = c.View;
        var o = new List<ViewMark>();
        if (graphics?.Hidden == true || spec.Kind == "plan" && SitePlans.Of(view) != null) return o;
        var voids = OpeningVoids.Of(q, VoidOptions.From(m, null));
        if (voids.Count == 0) return o;
        var hidden = (view.Attrs["hiddenElements"] as System.Text.Json.Nodes.JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        var viewGraphics = GraphicsSets.Of(m, view);
        var display = Views.StoreyDisplay.Of(view);
        var storey = view.Str("storeyId");
        var list = pieces.Count > 1 ? pieces : [(spec, 0.0)];
        var pens = new Pens(styles, graphics);
        foreach (var v in voids)
        {
            if (m.Get(v.HostId) is not { } host || hidden.Contains(host.Id) || GraphicsSets.Hides(viewGraphics, host)) continue;
            if (v.OpeningId != null && hidden.Contains(v.OpeningId)) continue;
            if (spec.Kind == "plan" && display.Shows(storey, Spatial.StoreyOf(m, host.Id)?.Id) == null) continue;
            foreach (var (s, station) in list)
                if (Mark(c, v, s, station, pieces.Count <= 1, sizes, pens) is { } mk) o.Add(mk);
        }
        return o;
    }

    /// <summary>The pens of a view's voids: its graphics' colour and weight, else the object styles', else violet.</summary>
    sealed class Pens(LineStyles.Resolver? styles, CategoryGraphics? graphics)
    {
        public (double Width, string Color, double[]? Dash) Of(string role, double width, double[]? dash)
        {
            var r = styles?.Invoke(role, Class, null);
            var w = Lapin.Drawing.Views.Pens.WeightMm(graphics?.LineWeight) ?? r?.WidthMm ?? width;
            var color = graphics?.LineColor is { Length: > 0 } gc ? gc : r?.Color is { Length: > 0 } rc ? rc : OpeningVoids.Color;
            return (w, color, r?.Dash ?? dash);
        }
    }

    enum Seen { Cut, Seen, Hidden, Overhead }

    static ViewMark? Mark(ViewContext c, OpeningVoid v, ViewSpec s, double station, bool plain, bool sizes, Pens pens)
    {
        var solid = v.Solid;
        if (solid.Vertices.Length == 0) return null;
        var f = s.Frame;
        double d0 = double.PositiveInfinity, d1 = double.NegativeInfinity, x0 = double.PositiveInfinity, x1 = double.NegativeInfinity;
        foreach (var p in solid.Vertices)
        {
            var d = f.Depth(p);
            var x = f.Map(p).X;
            d0 = Math.Min(d0, d); d1 = Math.Max(d1, d);
            x0 = Math.Min(x0, x); x1 = Math.Max(x1, x);
        }
        if (s.Span is { } sp && (x1 < sp.Min + 1e-9 || x0 > sp.Max - 1e-9)) return null;
        const double eps = 1e-6;
        var inWall = v.Kind is OpeningVoids.Wall or OpeningVoids.Door or OpeningVoids.Window;
        Seen how;
        if (d0 < -eps && d1 > eps) how = Seen.Cut;
        else if (s.Kind == "plan")
        {
            // Looking down: what is below the cut down to the range's bottom is seen, what is above it up to the top overhead.
            // A ceiling plan (looking up): what is above the cut, up to the top, is seen.
            var reach = s.Above > 0 ? s.Above : ViewRanges.Unlimited;
            if (s.Up) { if (d1 <= eps && -d1 < reach) how = Seen.Seen; else return null; }
            // A wall's opening wholly under the cut (a pipe's sleeve low in the wall) is inside the wall seen from above: hidden, dashed.
            else if (d0 >= -eps && d0 < s.Below + eps) how = inWall ? Seen.Hidden : Seen.Seen;
            else if (d1 <= eps && s.Above > 0 && -d1 < s.Above) how = Seen.Overhead;
            else return null;
        }
        else
        {
            // A section or an elevation sees a wall's opening on the face turned to it; a floor's or roof's lies inside its host.
            if (!inWall || d0 < -eps || d0 > s.Below) return null;
            if (x1 - x0 < d1 - d0) return null;
            how = Seen.Seen;
        }

        // The outline: the cut's loops, or the solid's edges as seen (an overhead one seen from above everything).
        var loops = new List<Vec2[]>();
        var lines = new List<(Vec2 A, Vec2 B)>();
        if (how == Seen.Cut)
        {
            var (_, areas, _) = HiddenLines.Draw([(solid, 0)], f, cut: true, maxDepth: 1e-3);
            foreach (var a in areas) loops.AddRange(a.Loops.Where(l => l.Length >= 3));
            foreach (var l in loops) for (var i = 0; i < l.Length; i++) lines.Add((l[i], l[(i + 1) % l.Length]));
        }
        else
        {
            // Overhead: seen from above everything. A ceiling plan sees the underside: from below, laid the way the plan lies (the same x and y).
            var eye = s.Up ? new ViewFrame(f.Origin + f.Dir * 1000, f.Right, f.Up, f.Dir * -1)
                : how == Seen.Overhead ? f with { Origin = f.Origin - f.Dir * 1000 } : f;
            var (seen, _, _) = HiddenLines.Draw([(solid, 0)], eye);
            foreach (var g in seen) lines.Add((g.A, g.B));
        }
        if (lines.Count == 0) return null;
        var pts = lines.SelectMany(l => new[] { l.A, l.B }).ToList();
        var hull = Polygon2.ConvexHull(pts);
        if (hull.Length < 3 || Math.Abs(Polygon2.SignedArea(hull)) < 1e-8) return null;

        // Something in front of a wall's opening in a section or an elevation (another wall, a roof) hides it.
        if (how == Seen.Seen && s.Kind != "plan" && plain)
        {
            var mid = new Vec2((hull.Min(p => p.X) + hull.Max(p => p.X)) / 2, (hull.Min(p => p.Y) + hull.Max(p => p.Y)) / 2);
            var at = f.Origin + f.Right * mid.X + f.Up * mid.Y + f.Dir * (d0 + 1e-3);
            var except = new HashSet<string> { v.HostId };
            if (v.OpeningId != null) except.Add(v.OpeningId);
            if (v.FillId != null) except.Add(v.FillId);
            if (c.Occluded(at, except)) return null;
        }

        var shift = new Vec2(station, 0);
        var (width, color, dash) = how switch
        {
            Seen.Cut => pens.Of(LineRole.Cut, Widths.Cut, null),
            Seen.Overhead => pens.Of(LineRole.Overhead, Widths.Overhead, Dashes.Of("dashed")),
            Seen.Hidden => pens.Of(LineRole.Hidden, Widths.Overhead, Dashes.Of("hidden")),
            _ => pens.Of(LineRole.Projection, Widths.Seen, null),
        };
        var mark = new ViewMark(v.SelectId) { Data = new System.Text.Json.Nodes.JsonObject { ["void"] = v.Mark, ["key"] = v.Key } };
        foreach (var (a, b) in lines) mark.Line(a + shift, b + shift, Layer, width, color, dash);
        // The cross, corner to corner (a round void's two diameters at 45°), thinner.
        foreach (var (a, b) in Cross(hull)) mark.Line(a + shift, b + shift, Layer, Math.Min(width, Widths.Overhead), color, dash);
        foreach (var p in hull) mark.Snaps.Add(p + shift);

        // The mark and its size beside the void: over a wide one, right of a tall one.
        var style = Standards.TextOf(c.Book, null);
        var h = style.Height * c.Mm;
        var (lo, hi) = (new Vec2(hull.Min(p => p.X), hull.Min(p => p.Y)), new Vec2(hull.Max(p => p.X), hull.Max(p => p.Y)));
        var text = new List<string> { v.Mark };
        if (sizes) text.Add(Size(v, c.Book.Units));
        var wide = hi.X - lo.X >= hi.Y - lo.Y;
        // A wall's opening in a plan: beside the wall, whichever way it runs (its thickness across, the void's depth).
        if (s.Kind == "plan" && inWall) wide = Math.Abs(hi.X - lo.X - v.Depth) > Math.Abs(hi.Y - lo.Y - v.Depth);
        for (var i = 0; i < text.Count; i++)
        {
            var (at, anchor) = wide
                ? (new Vec2((lo.X + hi.X) / 2, hi.Y + h * (0.6 + 1.4 * (text.Count - 1 - i))), "middle")
                : (new Vec2(hi.X + h * 0.6, (lo.Y + hi.Y) / 2 + h * 1.4 * ((text.Count - 1) / 2.0 - i)), "start");
            mark.Text(new PenText(at + shift, text[i], style.Height, 0, anchor, wide ? "bottom" : "middle", i == 0 || style.Bold, style.Font, style.Italic), color);
        }
        return mark;
    }

    /// <summary>A void's size as a coordination drawing writes it: width × height (a round one's diameter), and a wall opening's bottom level.</summary>
    public static string Size(OpeningVoid v, string units)
    {
        string L(double metres) => TagText.Length(metres, "project", units);
        var size = v.Diameter is { } d ? "Ø" + L(d) : $"{L(v.Width)}×{L(v.Height)}";
        if (v.Kind is OpeningVoids.Wall or OpeningVoids.Door or OpeningVoids.Window && Measure.Bounds(v.Solid) is { } b)
            size += " " + Standards.FormatLevel(b.Min.Z, units);
        return size;
    }

    /// <summary>Two lines across an outline: a quadrilateral's diagonals, else its extremes along the two 45° directions.</summary>
    static IEnumerable<(Vec2, Vec2)> Cross(Vec2[] hull)
    {
        if (hull.Length == 4)
        {
            yield return (hull[0], hull[2]);
            yield return (hull[1], hull[3]);
            yield break;
        }
        foreach (var u in new[] { new Vec2(1, 1), new Vec2(1, -1) })
            yield return (hull.MinBy(p => Vec2.Dot(p, u)), hull.MaxBy(p => Vec2.Dot(p, u)));
    }
}
