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
/// "segmental" (an arc of <see cref="Rise"/>), "pointed" (two arcs meeting at the apex, gothic;
/// equilateral unless <see cref="Rise"/> says), "gable" (a triangular head), "circle" (a round or
/// oval window filling the width and height). <see cref="Rise"/>: the head's height over the
/// spring line (null: the kind's own). <see cref="Bars"/>: the fanlight's or round window's
/// bars, "none", "sunburst" (spokes from the spring line's middle, or the centre), "mullions"
/// (the grid's mullions carried up), "cross"; <see cref="Spokes"/>: how many sunburst spokes.
/// </summary>
public sealed record JoineryShape(string Kind = "rect", double? Rise = null, string Bars = "none", int Spokes = 5);

/// <summary>
/// Glazing bars dividing every rectangular pane: <see cref="Pattern"/> "none", "grid" (colonial:
/// <see cref="Across"/> × <see cref="Up"/> lites a pane), "prairie" (bars <see cref="Margin"/> in
/// from each edge), "diamond" (a lattice, <see cref="Across"/> diamonds across); <see cref="Width"/>: the bar's face.
/// </summary>
public sealed record JoineryMuntins(string Pattern = "none", int Across = 2, int Up = 3, double Width = 0.022, double Margin = 0.1);

public static class JoineryShapes
{
    public static readonly string[] Kinds = ["rect", "round", "segmental", "pointed", "gable", "circle"];
    public static readonly string[] HeadBars = ["none", "sunburst", "mullions", "cross"];
    public static readonly string[] MuntinPatterns = ["none", "grid", "prairie", "diamond"];

    public static string KindOf(JoinerySpec s) => s.Shape?.Kind is { } k && Kinds.Contains(k) ? k : "rect";
    public static bool Headed(JoinerySpec s) => KindOf(s) is "round" or "segmental" or "pointed" or "gable";
    public static bool Round(JoinerySpec s) => KindOf(s) == "circle";
    public static bool Shaped(JoinerySpec s) => KindOf(s) != "rect";

    /// <summary>The head's height over the spring line: a semicircle's half width, a segment's rise (a sixth of the width unless asked), an equilateral pointed arch's, a gable's quarter width; never more than leaves 150 mm of rectangle under it.</summary>
    public static double Rise(JoinerySpec s)
    {
        var w = s.Width;
        var r = KindOf(s) switch
        {
            "round" => w / 2,
            "segmental" => Math.Clamp(s.Shape?.Rise ?? w / 6, 0.02, w / 2),
            "pointed" => Math.Max(0.05, s.Shape?.Rise ?? w * Math.Sqrt(3) / 2),
            "gable" => Math.Max(0.02, s.Shape?.Rise ?? w / 4),
            _ => 0,
        };
        return Math.Min(r, Math.Max(0, s.Height - 0.15));
    }

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

    /// <summary>The head's curve from the right jamb to the left, taken <paramref name="inset"/> in (its ends on the jamb lines ±(w/2 − inset)).</summary>
    public static List<Vec2> Head(JoinerySpec s, double inset)
    {
        var half = s.Width / 2 - inset;
        var kind = KindOf(s);
        var xs = kind == "gable" ? [half, 0, -half]
            : Enumerable.Range(0, 33).Select(i => half * Math.Cos(Math.PI * i / 32)).ToList();
        if (kind == "pointed")
        {
            // Each half its own arc, the apex exactly at the middle.
            var right = Enumerable.Range(0, 17).Select(i => half * (1 - i / 16.0));
            xs = [.. right, .. right.Reverse().Skip(1).Select(x => -x)];
        }
        return xs.Select(x => new Vec2(x, Top(s, x, inset))).ToList();
    }

    /// <summary>An ellipse filling the width and height, taken <paramref name="inset"/> in, counter-clockwise from the right.</summary>
    public static List<Vec2> Ellipse(JoinerySpec s, double inset, int n = 48)
    {
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
    /// jambs), the transom at the spring line, the fanlight's glass and its bars.
    /// </summary>
    public static IEnumerable<JoineryBox> HeadBoxes(JoinerySpec s, double y0, double y1)
    {
        var f = s.Frame;
        var (half, zs, fw) = (s.Width / 2, Spring(s), f.Width);
        var outer = Head(s, 0);
        var inner = Head(s, fw);
        // The arch: from the outer curve down the jambs' insides and back along the inner curve.
        yield return Prism("frame", [new(half, zs), .. outer, new(-half, zs), new(-half + fw, zs), new(-half + fw, inner[^1].Y), .. Enumerable.Reverse<Vec2>(inner), new(half - fw, inner[0].Y), new(half - fw, zs)], y0, y1);
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
                foreach (var c in cols.Take(cols.Count - 1)) yield return new Vec2((c.B + s.Frame.Mullion / 2 + 0), zs);
            }
            else if (bars == "cross") yield return new Vec2(0, zs);
        }
        foreach (var end in Ends())
        {
            if (bars == "mullions" || bars == "cross")
            {
                var top = Hit(inner, new Vec2(end.X, zs), new Vec2(0, 1));
                if (Bar("frame", end, top, bars == "mullions" ? s.Frame.Mullion : w, by0, by1) is { } m) yield return m;
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
