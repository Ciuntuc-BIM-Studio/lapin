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
    /// <remarks>
    /// A label stands by default where nothing else is drawn (decision 292): beside the void across its wall, else moved
    /// along the wall, else further out, clear of the door's swing, the door's marker and the labels before it
    /// (<paramref name="others"/>: the view's annotations so far). Moved or turned by its grips, it keeps its offset from
    /// the void's middle along and across the wall in the view's <c>voidLabels</c> (by the void's key), so it follows the
    /// opening.
    /// </remarks>
    public static List<ViewMark> Marks(IQueryContext q, ViewContext c, ViewSpec spec, IReadOnlyList<(ViewSpec Spec, double Station)> pieces, bool sizes,
        LineStyles.Resolver? styles, CategoryGraphics? graphics, IReadOnlyList<ViewMark>? others = null)
    {
        var m = q.Model;
        var view = c.View;
        var o = new List<ViewMark>();
        if (graphics?.Hidden == true || spec.Kind == "plan" && SitePlans.Of(view) != null) return o;
        var voids = OpeningVoids.Of(q, VoidOptions.From(m, null));
        if (voids.Count == 0) return o;
        var hidden = (view.Attrs["hiddenElements"] as System.Text.Json.Nodes.JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        var viewGraphics = VisibilityGraphics.Of(m, view);
        var display = Views.StoreyDisplay.Of(view);
        var storey = view.Str("storeyId");
        var list = pieces.Count > 1 ? pieces : [(spec, 0.0)];
        var pens = new Pens(styles, graphics);
        var obstacles = new Obstacles(c, others);
        foreach (var v in voids)
        {
            if (m.Get(v.HostId) is not { } host || hidden.Contains(host.Id) || viewGraphics.Hides(host)) continue;
            if (v.OpeningId != null && hidden.Contains(v.OpeningId)) continue;
            if (spec.Kind == "plan" && display.Shows(storey, Spatial.StoreyOf(m, host.Id)?.Id) == null) continue;
            foreach (var (s, station) in list)
                if (Mark(c, v, s, station, pieces.Count <= 1, sizes, pens, obstacles) is { } mk) o.Add(mk);
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

    /// <summary>What a label should not stand on: the drawing's lines and cut outlines, the view's annotations, the labels placed before.</summary>
    sealed class Obstacles
    {
        readonly List<(Vec2 A, Vec2 B, double X0, double Y0, double X1, double Y1)> segments = [];

        public Obstacles(ViewContext c, IReadOnlyList<ViewMark>? others)
        {
            foreach (var l in c.Drawing.Lines) Add(l.A, l.B);
            foreach (var p in c.Drawing.Cuts)
                foreach (var loop in p.Loops)
                    for (var i = 0; i < loop.Length; i++) Add(loop[i], loop[(i + 1) % loop.Length]);
            foreach (var t in c.Drawing.Texts ?? []) Box(LabelParts.BoxOf(new PenText(t.At, t.Text, t.Height ?? 2.5, t.Angle, "middle", "middle"), c.Mm));
            foreach (var mk in others ?? [])
            {
                if (mk.Pick) continue;
                foreach (var l in mk.Lines) Add(l.A, l.B);
                foreach (var (t, _) in mk.Texts) Box(LabelParts.BoxOf(t, c.Mm));
            }
        }

        void Add(Vec2 a, Vec2 b) => segments.Add((a, b, Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));

        /// <summary>A text's box as four sides (and its middle crossed, so a box inside another counts).</summary>
        public void Box((Vec2 C, double Hw, double Hh, double Deg) box)
        {
            foreach (var (a, b) in LabelParts.Sides(box)) Add(a, b);
            var u = LabelParts.Dir(box.Deg) * box.Hw;
            Add(box.C - u, box.C + u);
        }

        /// <summary>How many of them cross a box standing upright at <paramref name="c"/>.</summary>
        public int Hits(Vec2 c, double hw, double hh)
        {
            var n = 0;
            foreach (var s in segments)
            {
                if (s.X1 < c.X - hw || s.X0 > c.X + hw || s.Y1 < c.Y - hh || s.Y0 > c.Y + hh) continue;
                if (LabelParts.Crosses((c, hw, hh, 0), s.A, s.B)) n++;
            }
            return n;
        }
    }

    static ViewMark? Mark(ViewContext c, OpeningVoid v, ViewSpec s, double station, bool plain, bool sizes, Pens pens, Obstacles obstacles)
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

        // The mark and its size beside the void: across its wall, else along it or further out, where nothing else is drawn.
        var style = Standards.TextOf(c.Book, null);
        var mm = c.Mm;
        var h = style.Height * mm;
        var (lo, hi) = (new Vec2(hull.Min(p => p.X), hull.Min(p => p.Y)) + shift, new Vec2(hull.Max(p => p.X), hull.Max(p => p.Y)) + shift);
        var text = new List<string> { v.Mark };
        if (sizes) text.Add(Size(v, c.Book.Units));
        var (bw, bh) = (text.Max(t => LabelParts.Width(t, h)), h * (1 + 1.4 * (text.Count - 1)));
        // The label's own directions: along the wall (read left to right) and across it; the view's x and y for any other void.
        var u = new Vec2(1, 0);
        if (s.Kind == "plan" && inWall && v.Along is { } dir)
        {
            var d = new Vec2(Vec3.Dot(dir.At(0), f.Right), Vec3.Dot(dir.At(0), f.Up));
            if (d.Length > 1e-9) u = d.Normalized();
        }
        if (u.X < -1e-9 || Math.Abs(u.X) <= 1e-9 && u.Y < 0) u = u * -1;
        var across = new Vec2(-u.Y, u.X);
        var middle = (lo + hi) * 0.5;
        var labels = c.View.Attrs["voidLabels"] as System.Text.Json.Nodes.JsonObject;
        var part = LabelParts.Of(labels, v.Key);
        var pos = part.D is { } off ? middle + (u * off.X + across * off.Y) * mm : Place(v, s, inWall, hull, shift, middle, u, across, bw, bh, h, obstacles);
        var angle = part.Rot is { } r ? LabelParts.Upright(LabelParts.AngleOf(u) + r) : 0;
        var tdir = LabelParts.Dir(angle);
        var up = new Vec2(-tdir.Y, tdir.X);
        for (var i = 0; i < text.Count; i++)
            mark.Text(new PenText(pos + up * (bh / 2 - h / 2 - 1.4 * h * i), text[i], style.Height, angle, "middle", "middle", i == 0 || style.Bold, style.Font, style.Italic), color);
        obstacles.Box((pos, bw / 2, bh / 2, angle));
        mark.Data!["label"] = LabelParts.TextData("label", pos, middle, u, across, mm, angle, LabelParts.AngleOf(u), pos + tdir * (bw / 2 + h), part, (pos, bw / 2, bh / 2, angle));
        mark.Data!["view"] = c.View.Id;
        return mark;
    }

    /// <summary>
    /// Where a void's label stands by default: beside it across its wall (over a wide void, right of a tall one, as
    /// before), else on the other side, else beside it and moved along the wall past its end, else further out, else
    /// along the wall; the first place nothing else crosses, else the one fewest things cross.
    /// </summary>
    static Vec2 Place(OpeningVoid v, ViewSpec s, bool inWall, Vec2[] hull, Vec2 shift, Vec2 middle, Vec2 u, Vec2 across, double bw, double bh, double h, Obstacles obstacles)
    {
        var gap = 0.6 * h;
        // The void's and the label's half sizes along a direction.
        double Of(Vec2 n) => hull.Max(p => Vec2.Dot(p + shift - middle, n));
        double Box(Vec2 n) => Math.Abs(n.X) * bw / 2 + Math.Abs(n.Y) * bh / 2;
        Vec2 sideways, along;
        if (s.Kind == "plan" && inWall && v.Along != null) (sideways, along) = (across, u);
        else
        {
            var wide = hull.Max(p => p.X) - hull.Min(p => p.X) >= hull.Max(p => p.Y) - hull.Min(p => p.Y);
            (sideways, along) = wide ? (new Vec2(0, 1), new Vec2(1, 0)) : (new Vec2(1, 0), new Vec2(0, 1));
        }
        // Over (or right of) the void first, as a label was put before it could move.
        if (Vec2.Dot(sideways, new Vec2(1, 1)) < 0) sideways = sideways * -1;
        Vec2 Out(Vec2 n, double extra = 0) => n * (Of(n) + gap + Box(n) + extra);
        var candidates = new List<Vec2>();
        foreach (var side in new[] { sideways, sideways * -1 }) candidates.Add(middle + Out(side));
        foreach (var side in new[] { sideways, sideways * -1 })
            foreach (var end in new[] { along, along * -1 })
                candidates.Add(middle + Out(side) + Out(end));
        foreach (var side in new[] { sideways, sideways * -1 }) candidates.Add(middle + Out(side, bh + gap));
        foreach (var end in new[] { along, along * -1 }) candidates.Add(middle + Out(end));
        var (best, fewest) = (candidates[0], int.MaxValue);
        foreach (var at in candidates)
        {
            var n = obstacles.Hits(at, bw / 2 + gap / 2, bh / 2 + gap / 2);
            if (n == 0) return at;
            if (n < fewest) (best, fewest) = (at, n);
        }
        return best;
    }

    /// <summary>
    /// A void's label moved (offset: [along, across] paper mm from the void's middle; null: back where it stands by
    /// default), turned (rot: degrees from the wall; null: level again) or reset (reset: true; with no key, every label
    /// of the view): kept on the view by the void's key (decision 292).
    /// </summary>
    public static void Register(IModuleRegistry r)
    {
        r.Command("view.voidLabel", "Move void label", (ctx, a) =>
        {
            var (view, write) = GraphicsSets.Target(ctx, (string?)a["viewId"] ?? throw new CommandException("A void's label is on a view."));
            var key = ((string?)a["key"])?.Trim();
            var args = a.DeepClone().AsObject();
            args["part"] = key;
            var attrs = view.CloneAttrs();
            if (LabelParts.Apply(attrs["voidLabels"] as System.Text.Json.Nodes.JsonObject, args) is { } labels) attrs["voidLabels"] = labels; else attrs.Remove("voidLabels");
            write(attrs);
            return null;
        });
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
