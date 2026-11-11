using Lapin.Geometry;

namespace Lapin.Architecture;

/*
 * Doors and windows that are not rectangles (decision 188): an arched head (round, segmental,
 * pointed, a gable), or a round or oval window. A headed opening keeps its rectangle up to the
 * spring line, where the configurator's grid of panels stops under a transom, and above it a
 * fixed fanlight under the head, glazed and, when asked, divided by bars (a sunburst's spokes,
 * or the mullions carried up). A round window is one fixed light in a ring frame, with a cross
 * or spokes. The wall's opening, the frame, the glass, the surround and the drawings all
 * follow the outline. Glazing bars (muntins) divide the rectangular panes: a colonial grid,
 * prairie margins, a diamond lattice.
 */

/// <summary>
/// The outline of a door or window: <see cref="Kind"/> "rect", "round" (a semicircular head),
/// "segmental" (an arc of <see cref="Rise"/>), "elliptical" (a half ellipse across the width, <see cref="Rise"/> high),
/// "radiused" (a flat head with its top corners rounded, <see cref="Rise"/> their radius), "pointed" (two arcs meeting at the apex, gothic;
/// equilateral unless <see cref="Rise"/> says), "gable" (a triangular head), "circle" (a round or
/// oval window filling the width and height). <see cref="Rise"/>: the head's height over the
/// spring line (null: the kind's own). <see cref="Bars"/>: the fanlight's or round window's
/// bars, "none", "sunburst" (spokes from the spring line's middle, or the centre), "mullions"
/// (the grid's mullions carried up), "cross"; <see cref="Spokes"/>: how many sunburst spokes.
/// <see cref="Transom"/> (a tester's report #112): false, no transom at the spring line: the grid's top row reaches into the
/// head, its panes (a casement, a fixed light, a door leaf) following the arch all round (an arched casement opening
/// side-hung, no bar under the arch); null or true, the transom and a fixed fanlight over it, as before.
/// </summary>
public sealed record JoineryShape(string Kind = "rect", double? Rise = null, string Bars = "none", int Spokes = 5, bool? Transom = null);

/// <summary>
/// Glazing bars dividing every rectangular pane: <see cref="Pattern"/> "none", "grid" (colonial:
/// <see cref="Across"/> × <see cref="Up"/> lites a pane), "prairie" (bars <see cref="Margin"/> in
/// from each edge), "diamond" (a lattice, <see cref="Across"/> diamonds across); <see cref="Width"/>: the bar's face.
/// </summary>
public sealed record JoineryMuntins(string Pattern = "none", int Across = 2, int Up = 3, double Width = 0.022, double Margin = 0.1);

public static class JoineryShapes
{
    public static readonly string[] Kinds = ["rect", "round", "segmental", "elliptical", "radiused", "pointed", "gable", "circle"];
    public static readonly string[] HeadBars = ["none", "sunburst", "mullions", "cross"];
    public static readonly string[] MuntinPatterns = ["none", "grid", "prairie", "diamond"];

    public static string KindOf(JoinerySpec s) => s.Shape?.Kind is { } k && Kinds.Contains(k) ? k : "rect";
    public static bool Headed(JoinerySpec s) => KindOf(s) is "round" or "segmental" or "elliptical" or "radiused" or "pointed" or "gable";
    public static bool Round(JoinerySpec s) => KindOf(s) == "circle";
    public static bool Shaped(JoinerySpec s) => KindOf(s) != "rect";

    /// <summary>A headed type without the transom at the spring line (report #112): its top row of panes follows the head.</summary>
    public static bool ArchedPanes(JoinerySpec s) => Headed(s) && s.Shape?.Transom == false;

    /// <summary>
    /// A pane's outline when it reaches into the head (<see cref="ArchedPanes"/>, a panel of the grid's top row), taken
    /// <paramref name="inset"/> in from the panel's rectangle <paramref name="r"/> (negative: out, a sash's lip): its sides
    /// and bottom the rectangle's, its top the head's inner curve as far in, counter-clockwise from the bottom right; null
    /// for a pane that stays a rectangle.
    /// </summary>
    public static List<Vec2>? PaneOutline(JoinerySpec s, CellRect r, double inset)
    {
        if (!ArchedPanes(s) || r.Z1 < Joinery.InnerRect(s).Z1 - 1e-6) return null;
        var (x0, x1, z0, d) = (r.X0 + inset, r.X1 - inset, r.Z0 + inset, s.Frame.Width + inset);
        var head = Head(s, d).Where(p => p.X < x1 - 1e-9 && p.X > x0 + 1e-9);
        return Clean([new(x1, z0), new(x1, Top(s, x1, d)), .. head, new(x0, Top(s, x0, d)), new(x0, z0)]);
    }

    /// <summary>The top of a pane that reaches into the head, at x (the head's inner curve), for its opening symbol; null for a rectangle's.</summary>
    public static Func<double, double>? PaneTop(JoinerySpec s, CellRect r) =>
        PaneOutline(s, r, 0) is null ? null : x => Math.Min(r.Z1, Top(s, x, s.Frame.Width));

    /// <summary>The part of the segment a..b inside a convex loop running counter-clockwise (Cyrus–Beck); null when it misses.</summary>
    public static (Vec2 A, Vec2 B)? ClipConvex(Vec2 a, Vec2 b, IReadOnlyList<Vec2> loop)
    {
        double t0 = 0, t1 = 1;
        var d = b - a;
        for (var i = 0; i < loop.Count; i++)
        {
            var (p, q) = (loop[i], loop[(i + 1) % loop.Count]);
            var e = q - p;
            // Inside is to the left of each edge: cross(e, x − p) ≥ 0.
            var num = Vec2.Cross(e, a - p);
            var den = Vec2.Cross(e, d);
            if (Math.Abs(den) < 1e-12) { if (num < 0) return null; continue; }
            var t = -num / den;
            if (den > 0) t0 = Math.Max(t0, t); else t1 = Math.Min(t1, t);
            if (t1 - t0 < 1e-9) return null;
        }
        return (a + d * t0, a + d * t1);
    }

    /// <summary>The head's height over the spring line: a semicircle's half width, a segment's rise (a sixth of the width unless asked), a half ellipse's (a quarter of the width unless asked), a radiused head's corner radius (150 mm unless asked, never more than half the width), an equilateral pointed arch's, a gable's quarter width; never more than leaves 150 mm of rectangle under it.</summary>
    public static double Rise(JoinerySpec s)
    {
        var w = s.Width;
        var r = KindOf(s) switch
        {
            "round" => w / 2,
            "segmental" => Math.Clamp(s.Shape?.Rise ?? w / 6, 0.02, w / 2),
            "elliptical" => Math.Clamp(s.Shape?.Rise ?? w / 4, 0.02, w),
            "radiused" => Math.Clamp(s.Shape?.Rise ?? DefaultCornerRadius, 0.01, w / 2),
            "pointed" => Math.Max(0.05, s.Shape?.Rise ?? w * Math.Sqrt(3) / 2),
            "gable" => Math.Max(0.02, s.Shape?.Rise ?? w / 4),
            _ => 0,
        };
        return Math.Min(r, Math.Max(0, s.Height - 0.15));
    }

    /// <summary>A radiused head's corner radius when none is asked.</summary>
    public const double DefaultCornerRadius = 0.15;

    /// <summary>Where the head springs from the jambs, over the bottom.</summary>
    public static double Spring(JoinerySpec s) => s.Height - Rise(s);

    /// <summary>
    /// The head's top at <paramref name="x"/> (along, from the axis) for the outline taken
    /// <paramref name="inset"/> inwards (negative: outwards, for a surround): each arc about its
    /// own centre with its radius less the inset, a gable's sides moved in square to themselves.
    /// </summary>
    public static double Top(JoinerySpec s, double x, double inset)
    {
        var (w, f, zs) = (s.Width, Rise(s), Spring(s));
        var ax = Math.Abs(x);
        static double Root(double v) => Math.Sqrt(Math.Max(0, v));
        switch (KindOf(s))
        {
            case "round":
                return zs + Root(Math.Pow(w / 2 - inset, 2) - x * x);
            case "segmental":
            {
                var r = (w * w / 4 + f * f) / (2 * f);
                return zs + f - r + Root(Math.Pow(r - inset, 2) - x * x);
            }
            case "elliptical":
            {
                // The half ellipse with both semi-axes less the inset (the frame's band nearly even all round).
                var (a, b) = (w / 2 - inset, f - inset);
                return a <= 0 || b <= 0 ? zs : zs + b * Root(1 - x * x / (a * a));
            }
            case "radiused":
            {
                // Each corner an arc about its own centre, the radius less the inset; square once the inset passes it.
                var (cx, rc) = (w / 2 - f, f - inset);
                return ax <= cx || rc <= 0 ? zs + f - inset : zs + Root(rc * rc - Math.Pow(ax - cx, 2));
            }
            case "pointed":
            {
                var r = (f * f + w * w / 4) / w;
                return zs + Root(Math.Pow(r - inset, 2) - Math.Pow(ax - (w / 2 - r), 2));
            }
            case "gable":
                return zs + f - f * ax / (w / 2) - inset * Math.Sqrt(1 + Math.Pow(f / (w / 2), 2));
            default:
                return s.Height - inset;
        }
    }

    /// <summary>
    /// The head's curve from the right jamb to the left, taken <paramref name="inset"/> in (its ends on the jamb lines ±(w/2 − inset)).
    /// Each arc in even steps of its own angle, as many as <see cref="Chords"/> asks of its radius (at least
    /// <see cref="MinPieces"/> to a head), so a wide arch strays from its circle no more than a curved wall does.
    /// </summary>
    public static List<Vec2> Head(JoinerySpec s, double inset)
    {
        var half = s.Width / 2 - inset;
        var (w, f) = (s.Width, Rise(s));
        // The x of n + 1 points in even steps of angle from a0 to a1 on a circle of radius rho about cx.
        static IEnumerable<double> Arc(double cx, double rho, double a0, double a1, int min) =>
            Enumerable.Range(0, Pieces(rho, a1 - a0, min) + 1).Select(i => cx + rho * Math.Cos(a0 + (a1 - a0) * i / Pieces(rho, a1 - a0, min)));
        static double Acos(double v) => Math.Acos(Math.Clamp(v, -1, 1));
        List<double> xs;
        switch (KindOf(s))
        {
            case "gable":
                xs = [half, 0, -half];
                break;
            case "segmental":
            {
                // The arc of its circle between the jamb lines.
                var rho = (w * w / 4 + f * f) / (2 * f) - inset;
                if (rho <= half + 1e-12) goto default;
                var a0 = Acos(half / rho);
                xs = [half, .. Arc(0, rho, a0, Math.PI - a0, MinPieces).Skip(1).SkipLast(1), -half];
                break;
            }
            case "elliptical":
            {
                // Even steps of the ellipse's own angle, as many as its flattest curve (the top's radius a²/b) asks.
                var (a, b) = (half, f - inset);
                var rho = a > 0 && b > 0 ? Math.Max(a * a / b, b * b / a) : half;
                var n = Pieces(rho, Math.PI, MinPieces);
                xs = [.. Enumerable.Range(0, n + 1).Select(i => half * Math.Cos(Math.PI * i / n))];
                break;
            }
            case "radiused":
            {
                // A quarter circle in each corner, the flat between them; the jamb's top square when the inset passes the radius.
                var (cx, rc) = (w / 2 - f, f - inset);
                if (rc <= 1e-9) return [new(half, Top(s, half, inset)), new(-half, Top(s, -half, inset))];
                var right = Arc(cx, rc, 0, Math.PI / 2, MinPieces / 2).ToList();
                xs = [.. right, .. Enumerable.Reverse(right).SkipWhile(x => x < 1e-9).Select(x => -x)];
                break;
            }
            case "pointed":
            {
                // Each half its own arc about a centre on the far side of the axis, the apex exactly at the middle.
                var r = (f * f + w * w / 4) / w;
                var (cx, rho) = (w / 2 - r, r - inset);
                var right = (rho > 0 ? Arc(cx, rho, Acos((half - cx) / rho), Acos(-cx / rho), MinPieces / 2) : [half, 0]).ToList();
                right[0] = half; right[^1] = 0;
                xs = [.. right, .. Enumerable.Reverse(right).Skip(1).Select(x => -x)];
                break;
            }
            default:
                // A semicircle (or a segment flattened to one): even steps of its angle.
                xs = [.. Arc(0, Math.Max(half, 1e-9), 0, Math.PI, MinPieces)];
                xs[0] = half; xs[^1] = -half;
                break;
        }
        return xs.Select(x => new Vec2(x, Top(s, x, inset))).ToList();
    }

    /// <summary>The fewest pieces a head's curve is made of (a small arch stays as round as before the chord rule).</summary>
    public const int MinPieces = 32;

    /// <summary>The pieces an arc of <paramref name="radius"/> turning <paramref name="sweep"/> is made of: the chord rule's, at least <paramref name="min"/>.</summary>
    static int Pieces(double radius, double sweep, int min) => Chords.Count(radius, sweep, min);

    /// <summary>
    /// An ellipse filling the width and height, taken <paramref name="inset"/> in, counter-clockwise from the right: as many
    /// points as the chord rule asks of its flattest curve (at least 48), the same for every inset so a ring's two sides pair.
    /// </summary>
    public static List<Vec2> Ellipse(JoinerySpec s, double inset)
    {
        var (a0, b0) = (Math.Max(s.Width / 2, 1e-6), Math.Max(s.Height / 2, 1e-6));
        var n = Pieces(Math.Max(a0 * a0 / b0, b0 * b0 / a0), 2 * Math.PI, 48);
        var (a, b) = (s.Width / 2 - inset, s.Height / 2 - inset);
        return Enumerable.Range(0, n).Select(i => new Vec2(a * Math.Cos(2 * Math.PI * i / n), s.Height / 2 + b * Math.Sin(2 * Math.PI * i / n))).ToList();
    }

    /// <summary>The whole outline taken <paramref name="inset"/> in (negative: out), counter-clockwise from the bottom right, its bottom at <paramref name="bottom"/>.</summary>
    public static List<Vec2> Outline(JoinerySpec s, double inset = 0, double? bottom = null)
    {
        if (Round(s)) return Ellipse(s, inset);
        var half = s.Width / 2 - inset;
        var z0 = bottom ?? inset;
        if (!Headed(s)) return [new(half, z0), new(half, s.Height - inset), new(-half, s.Height - inset), new(-half, z0)];
        return [new(half, z0), .. Head(s, inset), new(-half, z0)];
    }

    /// <summary>A prism of the body: <paramref name="pts"/> (X along, Z up) through Y0..Y1, its box its bounds.</summary>
    public static JoineryBox Prism(string role, IReadOnlyList<Vec2> raw, double y0, double y1)
    {
        var pts = Clean(raw);
        return new(role, new Vec3(pts.Min(p => p.X), Math.Min(y0, y1), pts.Min(p => p.Y)), new Vec3(pts.Max(p => p.X), Math.Max(y0, y1), pts.Max(p => p.Y)), pts);
    }

    /// <summary>A loop without repeated points (where an arch springs exactly from its jamb).</summary>
    public static List<Vec2> Clean(IReadOnlyList<Vec2> raw)
    {
        var o = new List<Vec2>();
        foreach (var p in raw) if (o.Count == 0 || (p - o[^1]).Length > 1e-7) o.Add(p);
        while (o.Count > 1 && (o[0] - o[^1]).Length <= 1e-7) o.RemoveAt(o.Count - 1);
        return o;
    }

    /// <summary>A straight bar from a to b (X, Z), <paramref name="width"/> wide, through Y0..Y1.</summary>
    public static JoineryBox? Bar(string role, Vec2 a, Vec2 b, double width, double y0, double y1)
    {
        var d = b - a;
        if (d.Length < 1e-6) return null;
        var n = new Vec2(-d.Y, d.X).Normalized() * (width / 2);
        return Prism(role, [a + n, b + n, b - n, a - n], y0, y1);
    }

    /// <summary>A box's extent across a line: where a horizontal line at <paramref name="z"/> (or a vertical one at x, <paramref name="vertical"/>) crosses it, as intervals along the other axis.</summary>
    public static List<(double A, double B)> Spans(JoineryBox b, double at, bool vertical = false)
    {
        if (b.Profile is not { } outline)
        {
            var (lo, hi, a0, a1) = vertical ? (b.Min.X, b.Max.X, b.Min.Z, b.Max.Z) : (b.Min.Z, b.Max.Z, b.Min.X, b.Max.X);
            return at >= lo && at <= hi ? [(a0, a1)] : [];
        }
        var cuts = new List<double>();
        foreach (var pts in (b.Holes ?? []).Prepend(outline))
        for (var i = 0; i < pts.Count; i++)
        {
            var (p, q) = (pts[i], pts[(i + 1) % pts.Count]);
            var (u0, u1, v0, v1) = vertical ? (p.X, q.X, p.Y, q.Y) : (p.Y, q.Y, p.X, q.X);
            if ((u0 <= at && u1 > at) || (u1 <= at && u0 > at)) cuts.Add(v0 + (v1 - v0) * (at - u0) / (u1 - u0));
        }
        cuts.Sort();
        var o = new List<(double, double)>();
        for (var i = 0; i + 1 < cuts.Count; i += 2) o.Add((cuts[i], cuts[i + 1]));
        return o;
    }

    /// <summary>The segment a..b clipped to a rectangle (Liang–Barsky); null when it misses.</summary>
    public static (Vec2 A, Vec2 B)? Clip(Vec2 a, Vec2 b, CellRect r)
    {
        double t0 = 0, t1 = 1;
        var d = b - a;
        foreach (var (p, q) in new[] { (-d.X, a.X - r.X0), (d.X, r.X1 - a.X), (-d.Y, a.Y - r.Z0), (d.Y, r.Z1 - a.Y) })
        {
            if (Math.Abs(p) < 1e-12) { if (q < 0) return null; continue; }
            var t = q / p;
            if (p < 0) { if (t > t1) return null; t0 = Math.Max(t0, t); }
            else { if (t < t0) return null; t1 = Math.Min(t1, t); }
        }
        return t1 - t0 < 1e-9 ? null : (a + d * t0, a + d * t1);
    }

    /// <summary>The glazing bars of one pane <paramref name="r"/> (the glass's rectangle), as segments.</summary>
    public static List<(Vec2 A, Vec2 B)> MuntinLines(JoineryMuntins m, CellRect r)
    {
        var o = new List<(Vec2, Vec2)>();
        var (w, h) = (r.X1 - r.X0, r.Z1 - r.Z0);
        if (w < 0.05 || h < 0.05) return o;
        switch (m.Pattern)
        {
            case "grid":
                for (var i = 1; i < Math.Clamp(m.Across, 1, 12); i++) { var x = r.X0 + w * i / Math.Clamp(m.Across, 1, 12); o.Add((new(x, r.Z0), new(x, r.Z1))); }
                for (var j = 1; j < Math.Clamp(m.Up, 1, 12); j++) { var z = r.Z0 + h * j / Math.Clamp(m.Up, 1, 12); o.Add((new(r.X0, z), new(r.X1, z))); }
                break;
            case "prairie":
            {
                var g = Math.Clamp(m.Margin, 0.03, Math.Min(w, h) / 3);
                o.Add((new(r.X0 + g, r.Z0), new(r.X0 + g, r.Z1)));
                o.Add((new(r.X1 - g, r.Z0), new(r.X1 - g, r.Z1)));
                o.Add((new(r.X0, r.Z0 + g), new(r.X1, r.Z0 + g)));
                o.Add((new(r.X0, r.Z1 - g), new(r.X1, r.Z1 - g)));
                break;
            }
            case "diamond":
            {
                // Diamonds twice as tall as wide, Across of them over the width; each line clipped to the pane.
                var dx = w / Math.Clamp(m.Across, 1, 12);
                var slope = 2.0;
                var reach = h / slope + w;
                for (var x = r.X0 - reach; x <= r.X1 + reach + 1e-9; x += dx)
                {
                    if (Clip(new(x, r.Z0), new(x + h / slope, r.Z1), r) is { } up) o.Add(up);
                    if (Clip(new(x, r.Z0), new(x - h / slope, r.Z1), r) is { } down) o.Add(down);
                }
                break;
            }
        }
        return o;
    }

    /// <summary>
    /// The head over the spring line of a headed type, in the type's frame: the frame's arch (on the
    /// jambs), the transom at the spring line, the fanlight's glass and its bars; the arch alone when the panes follow it.
    /// </summary>
    public static IEnumerable<JoineryBox> HeadBoxes(JoinerySpec s, double y0, double y1)
    {
        var f = s.Frame;
        var (half, zs, fw) = (s.Width / 2, Spring(s), f.Width);
        var outer = Head(s, 0);
        var inner = Head(s, fw);
        // The arch: from the outer curve down the jambs' insides and back along the inner curve.
        yield return Prism("frame", [new(half, zs), .. outer, new(-half, zs), new(-half + fw, zs), new(-half + fw, inner[^1].Y), .. Enumerable.Reverse<Vec2>(inner), new(half - fw, inner[0].Y), new(half - fw, zs)], y0, y1);
        // Without its transom the panes below follow the arch (report #112): no fanlight of its own.
        if (ArchedPanes(s)) yield break;
        // The transom on the spring line, under the fanlight.
        var t = Math.Max(f.Transom, 0.02);
        yield return new JoineryBox("frame", new Vec3(-half + fw, y0, zs - t), new Vec3(half - fw, y1, zs));
        // The fanlight: the glass under the inner curve, down to the spring line.
        List<Vec2> glass = [new(half - fw, zs), .. inner.Where(p => p.Y > zs + 1e-6), new(-half + fw, zs)];
        if (glass.Count >= 3) yield return Prism("glass", glass, f.Offset - s.Sash.GlassThickness / 2, f.Offset + s.Sash.GlassThickness / 2);
        foreach (var b in FanBars(s, inner, y0, y1)) yield return b;
    }

    static IEnumerable<JoineryBox> FanBars(JoinerySpec s, List<Vec2> inner, double y0, double y1)
    {
        var bars = s.Shape?.Bars ?? "none";
        var zs = Spring(s);
        var w = Math.Max(0.015, s.Frame.Mullion * 0.4);
        var (by0, by1) = (s.Frame.Offset - (y1 - y0) * 0.3, s.Frame.Offset + (y1 - y0) * 0.3);
        // The grid's mullions carried up, each its own width (decision 466): none where the panels below meet.
        var mullions = Joinery.MullionWidths(s);
        var carried = new List<double>();
        IEnumerable<Vec2> Ends()
        {
            if (bars == "sunburst")
            {
                var n = Math.Clamp(s.Shape?.Spokes ?? 5, 1, 24);
                for (var i = 1; i <= n; i++)
                {
                    var a = Math.PI * i / (n + 1);
                    yield return Hit(inner, new Vec2(0, zs), new Vec2(Math.Cos(a), Math.Sin(a)));
                }
            }
            else if (bars == "mullions")
            {
                var (cols, _) = Joinery.Grid(s);
                for (var i = 0; i + 1 < cols.Count; i++)
                    if (mullions[i] > 1e-9) { carried.Add(mullions[i]); yield return new Vec2(cols[i].B + mullions[i] / 2, zs); }
            }
            else if (bars == "cross") yield return new Vec2(0, zs);
        }
        var k = 0;
        foreach (var end in Ends())
        {
            if (bars == "mullions" || bars == "cross")
            {
                var top = Hit(inner, new Vec2(end.X, zs), new Vec2(0, 1));
                if (Bar("frame", end, top, bars == "mullions" ? carried[k++] : w, by0, by1) is { } m) yield return m;
            }
            else if (Bar("frame", new Vec2(0, zs), end, w, by0, by1) is { } b) yield return b;
        }
    }

    /// <summary>Where a ray from <paramref name="from"/> along <paramref name="dir"/> first leaves through the polyline (the far point when it misses).</summary>
    public static Vec2 Hit(IReadOnlyList<Vec2> line, Vec2 from, Vec2 dir)
    {
        var best = double.MaxValue;
        for (var i = 0; i + 1 < line.Count; i++)
        {
            var (a, b) = (line[i], line[i + 1]);
            var e = b - a;
            var den = Vec2.Cross(dir, e);
            if (Math.Abs(den) < 1e-12) continue;
            var t = Vec2.Cross(a - from, e) / den;
            var u = Vec2.Cross(a - from, dir) / den;
            if (t > 1e-9 && u >= -1e-9 && u <= 1 + 1e-9) best = Math.Min(best, t);
        }
        return best < double.MaxValue ? from + dir * best : from + dir * 1;
    }

    /// <summary>A round window's body: the ring frame, the glass, its bars.</summary>
    public static List<JoineryBox> RoundBoxes(JoinerySpec s, double y0, double y1)
    {
        var f = s.Frame;
        var o = new List<JoineryBox>();
        var outer = Ellipse(s, 0);
        var inner = Ellipse(s, f.Width);
        o.Add(Prism("frame", outer, y0, y1) with { Holes = [inner] });
        o.Add(Prism("glass", inner, f.Offset - s.Sash.GlassThickness / 2, f.Offset + s.Sash.GlassThickness / 2));
        var c = new Vec2(0, s.Height / 2);
        var w = Math.Max(0.015, f.Mullion * 0.5);
        var (by0, by1) = (f.Offset - (y1 - y0) * 0.3, f.Offset + (y1 - y0) * 0.3);
        var closed = inner.Append(inner[0]).ToList();
        IEnumerable<double> Angles() => (s.Shape?.Bars ?? "none") switch
        {
            "cross" => [0, Math.PI / 2, Math.PI, 3 * Math.PI / 2],
            "sunburst" => Enumerable.Range(0, Math.Clamp(s.Shape?.Spokes ?? 8, 3, 24)).Select(i => 2 * Math.PI * i / Math.Clamp(s.Shape?.Spokes ?? 8, 3, 24)),
            "mullions" => [Math.PI / 2, 3 * Math.PI / 2],
            _ => [],
        };
        foreach (var a in Angles())
            if (Bar("frame", c, Hit(closed, c, new Vec2(Math.Cos(a), Math.Sin(a))), w, by0, by1) is { } b) o.Add(b);
        return o;
    }
}
