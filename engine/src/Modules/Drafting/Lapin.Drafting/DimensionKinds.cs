using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drafting.Cad;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// The dimensions beside the aligned and linear ones (a dimension's
/// <c>dimKind</c>), each drawn in the view's dimension style:
/// <list type="bullet">
/// <item><c>angular</c>: the angle at <c>c</c> between the rays to <c>a</c> and <c>b</c>, its arc through <c>at</c>; held on two straight path
/// elements (<c>holds</c>: each its element and <c>way</c> along its axis from where they meet; decision 738, an angular temporary
/// dimension's padlock), its vertex where their axes cross and its rays along them, so it follows them, and its value typed turns
/// the second about the vertex;</item>
/// <item><c>radial</c> and <c>diameter</c>: a circle's or arc's radius (R) or diameter (Ø), its centre <c>c</c> and a point <c>a</c> on it, the text at <c>at</c>; held on a drafted circle or arc (<c>shape</c>), it follows the shape and drives its radius;</item>
/// <item><c>arc</c>: an arc's length (⌒), held on a drafted arc (<c>shape</c>) or its centre, radius and angles;</item>
/// <item><c>slope</c>: the slope of the line from <c>a</c> to <c>b</c>, in % or degrees (<c>format</c>: auto, %, deg, ratio; auto: % under 15 %, degrees above), its arrow pointing down the slope;</item>
/// <item><c>level</c>: the level at <c>a</c> (in a section or elevation its height; in a plan its storey's, or <c>z</c> when given), with the level mark;</item>
/// <item><c>ordinate</c>: how far <c>a</c> lies from the origin <c>o</c> along x or y (<c>axis</c>), its leader to <c>at</c>;</item>
/// <item><c>coordinate</c> (a spot coordinate, decision 447): where <c>a</c> lies on a plan, its leader to <c>at</c> and two lines of text, in the
/// system it keeps (<c>system</c>): the map's eastings and northings (through the survey point, turned to true north), the project base
/// point's X and Y, or the plan's own; worked out on every drawing, so moving the survey point or the base point rewrites them.</item>
/// </list>
/// </summary>
public static class DimensionKinds
{
    public static readonly string[] Kinds = ["angular", "radial", "diameter", "arc", "slope", "level", "ordinate", "coordinate"];
    public static readonly string[] CoordinateSystems = ["map", "project", "internal"];

    /// <summary>
    /// A spot coordinate's two lines: E and N on the map, X and Y from the base point or the plan's origin. In the style's
    /// <paramref name="unit"/> when it is imperial (feet and inches, or inches, to 1/16"); else in metres with three decimals,
    /// as a survey writes coordinates whatever unit the lengths are dimensioned in.
    /// </summary>
    public static (string First, string Second) CoordinateText(IModelView m, Vec2 p, string? system, string unit = "m")
    {
        string F(double v) => Standards.IsImperial(unit) ? Standards.FormatImperial(v, unit == "ft-in", 2) : v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        switch (system)
        {
            case "map" when Location.Survey(m) != null:
            {
                var (e, n) = GeoRef.ToMap(m, p);
                return ($"E {F(e)}", $"N {F(n)}");
            }
            case "internal": return ($"X {F(p.X)}", $"Y {F(p.Y)}");
            default:
            {
                var d = p - Location.Base(m).At;
                return ($"X {F(d.X)}", $"Y {F(d.Y)}");
            }
        }
    }
    public static readonly string[] SlopeFormats = ["auto", "%", "deg", "ratio"];

    static Vec2 P(JsonNode? n) => Args.PointOf(n);
    static JsonArray J(Vec2 p) => new(p.X, p.Y);

    public static bool IsKind(Entity dim) => dim.Str("dimKind") is { } k && Kinds.Contains(k);

    /// <summary>A circle or arc the dimension holds on, as drawn now: its centre and radius (and an arc's angles); null when it holds on none or it is gone.</summary>
    static (Vec2 C, double R, Arc? Arc)? Round(IModelView m, Entity dim)
    {
        if (dim.Str("shape") is not { } id) return null;
        return Drafts.ShapeOf(m.Get(id) ?? new Entity("", "", "", [])) switch
        {
            Circle c => (c.C, c.R, null),
            Arc a => (a.C, a.R, a),
            _ => null,
        };
    }

    /// <summary>The value a dimension of these kinds shows, in its own unit (m, degrees or the slope's number), and whether it lost what it held on.</summary>
    public static (double Value, bool Orphan) Measure(ViewContext? c, IModelView m, Entity dim)
    {
        var kind = dim.Str("dimKind");
        var round = Round(m, dim);
        var orphan = dim.Str("shape") != null && round == null;
        var (cc, a, b) = (P(dim.Attrs["c"]), P(dim.Attrs["a"]), P(dim.Attrs["b"]));
        var atStored = P(dim.Attrs["at"]);
        if (kind == "angular" && dim.Attrs["holds"] != null)
        {
            if (Held(m, dim) is { } h) (cc, a, b, atStored) = h;
            else orphan = true;
        }
        switch (kind)
        {
            case "angular":
            {
                var (u, v) = (a - cc, b - cc);
                if (u.Length < 1e-9 || v.Length < 1e-9) return (0, orphan);
                var ang = Math.Abs(Math.Atan2(Vec2.Cross(u, v), Vec2.Dot(u, v))) * 180 / Math.PI;
                // The sector the arc's point lies in: the smaller angle, or the reflex one.
                var at = atStored - cc;
                if (at.Length > 1e-9 && !Between(u, v, at)) ang = 360 - ang;
                return (ang, orphan);
            }
            case "radial": return (round?.R ?? (a - cc).Length, orphan);
            case "diameter": return (2 * (round?.R ?? (a - cc).Length), orphan);
            case "arc":
            {
                if (round is { Arc: { } arc }) return (arc.R * arc.Sweep, orphan);
                return (dim.Num("r") * Math.Abs(dim.Num("sweep")), orphan);
            }
            case "slope":
            {
                var d = b - a;
                return (Math.Abs(d.X) < 1e-12 ? double.PositiveInfinity : Math.Abs(d.Y / d.X), orphan);
            }
            case "level":
            {
                if (dim.NumOrNull("z") is { } z) return (z, orphan);
                if (c != null && c.Kind == "plan") return (m.Get(c.View.Str("storeyId") ?? "")?.Num("elevation") ?? 0, orphan);
                return (a.Y, orphan);
            }
            case "ordinate":
            {
                var o = P(dim.Attrs["o"]);
                return (dim.Str("axis") == "y" ? a.Y - o.Y : a.X - o.X, orphan);
            }
        }
        return (0, orphan);
    }

    /// <summary>
    /// An angle held on two straight path elements (decision 738): its vertex where their axes (start → end) cross, a point on
    /// each ray (the element's axis that <c>way</c> from the vertex) and the arc's point on the bisector, <c>r</c> from the vertex;
    /// null when either element is gone, has no straight axis, or they have turned parallel.
    /// </summary>
    public static (Vec2 C, Vec2 A, Vec2 B, Vec2 At)? Held(IModelView m, Entity dim)
    {
        if (dim.Attrs["holds"] is not JsonArray { Count: 2 } holds) return null;
        var rays = new List<(Vec2 O, Vec2 U)>(2);
        foreach (var n in holds)
        {
            if (n is not JsonObject o || (string?)o["element"] is not { } id || m.Get(id) is not { } e || e.Attrs["start"] is not JsonArray || e.Attrs["end"] is not JsonArray) return null;
            if (PathCurve.Of(e).IsArc) return null;
            var (s0, s1) = (P(e.Attrs["start"]), P(e.Attrs["end"]));
            if ((s1 - s0).Length < 1e-9) return null;
            rays.Add((s0, (s1 - s0).Normalized() * ((Json.Number(o["way"]) ?? 1) < 0 ? -1 : 1)));
        }
        var ((o1, u1), (o2, u2)) = (rays[0], rays[1]);
        var cr = Vec2.Cross(u1, u2);
        if (Math.Abs(cr) < 1e-6) return null;
        var c = o1 + u1 * (Vec2.Cross(o2 - o1, u2) / cr);
        var r = dim.NumOrNull("r") is > 1e-6 and var rr ? rr : Math.Max((P(dim.Attrs["at"]) - P(dim.Attrs["c"])).Length, 0.5);
        var bis = (u1 + u2).Length > 1e-9 ? (u1 + u2).Normalized() : u1.Perp;
        return (c, c + u1 * r, c + u2 * r, c + bis * r);
    }

    /// <summary>Whether w lies in the smaller sector from u to v.</summary>
    static bool Between(Vec2 u, Vec2 v, Vec2 w)
    {
        var cuv = Vec2.Cross(u, v);
        return cuv >= 0 ? Vec2.Cross(u, w) >= 0 && Vec2.Cross(w, v) >= 0 : Vec2.Cross(u, w) <= 0 && Vec2.Cross(w, v) <= 0;
    }

    /// <summary>
    /// A level dimension's value (follow-up to forum #81): as the project's levels are written (their decimals and trailing
    /// zeros), unless the dimension says its own (levelDecimals, levelZeros); in feet and inches when its style's unit is.
    /// </summary>
    public static string LevelText(StyleBook book, Entity dim, double value, string unit)
    {
        if (Standards.IsImperial(unit)) return Standards.FormatLevel(value, unit);
        var decimals = dim.NumOrNull("levelDecimals") is { } d ? (int)Math.Round(d) : book.LevelDecimals;
        var zeros = dim.Str("levelZeros") switch { "keep" => false, "suppress" => true, _ => Standards.SuppressesLevelZeros(book) };
        return Standards.FormatLevel(value, decimals, zeros);
    }

    /// <summary>
    /// A slope as written: 12.5 %, 30.0°, 1:8; with the style's <paramref name="precision"/> (0–6 decimals) when it gives one,
    /// else (−1) one decimal for a percentage and an angle, up to one for a ratio, as before.
    /// </summary>
    public static string FormatSlope(double ratio, string format, int precision = -1)
    {
        if (double.IsInfinity(ratio)) return "∞";
        var f = format == "auto" ? (ratio < 0.15 ? "%" : "deg") : format;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string N(double v, string usual) => v.ToString(precision >= 0 ? "F" + Math.Min(precision, 6) : usual, inv);
        return f switch
        {
            "deg" => $"{N(Math.Atan(ratio) * 180 / Math.PI, "0.0")}°",
            "ratio" => ratio < 1e-9 ? "0" : $"1:{N(1 / ratio, "0.#")}",
            _ => $"{N(ratio * 100, "0.0")} %",
        };
    }

    /// <summary>An angle as written: the style's <paramref name="precision"/> (0–6 decimals) when it gives one, else (−1) up to one decimal, as before.</summary>
    public static string FormatAngle(double degrees, int precision = -1) =>
        degrees.ToString(precision >= 0 ? "F" + Math.Min(precision, 6) : "0.#", System.Globalization.CultureInfo.InvariantCulture) + "°";

    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        foreach (var dim in c.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "DIMENSION" && x.Str("viewId") == c.View.Id && IsKind(x)))
        {
            var style = Standards.ResolveDim(c.Book, Dimensions.ParamsOf(dim));
            var text = Standards.ResolveText(c.Book, Dimensions.ParamsOf(dim));
            var (value, orphan) = Measure(c, c.Model, dim);
            var color = orphan ? "#c0392b" : style.Color ?? "#000000";
            var mm = c.Mm;
            var w = style.DimLineWeightOr(Pens.Default.Hatch);
            // The extension lines' and the dimension line's own pens (issue #66); unset: as before.
            var extColor = orphan ? color : style.ExtColor ?? color;
            var extW = style.ExtWeightOr(Pens.Default.Hatch);
            var (lineDash, extDash) = (Standards.DashOf(c.Book, style.DimLineType), Standards.DashOf(c.Book, style.ExtLineType));
            var lineColor = orphan ? color : style.DimLineColor ?? color;
            // The value's colour (forum topic 173): the dimension's, else its style's text colour, else its text style's, else the style's.
            var textColor = orphan ? color : Standards.DimTextColor(Dimensions.ParamsOf(dim), style, text) ?? color;
            var mk = new ViewMark(dim.Id);
            void Label(Vec2 at, string s, double angleDeg = 0, string anchor = "middle", string baseline = "bottom") =>
                mk.Text(new PenText(at, dim.Str("text") is { Length: > 0 } over ? over : s, text.Height, -Standards.ReadableDeg(-angleDeg), anchor, baseline, text.Bold, text.Font, text.Italic), textColor);
            // The terminator pointing along dir at its tip (a string's own, DimArrow): its barbs back along it, its tick leaning 45°; "none" draws none.
            void Arrow(Vec2 tip, Vec2 dir) => DimArrow.Draw(mk, style, mm, tip, -dir, -dir.Perp, w, lineColor);
            // Where a radial extension line starts: from the gap (<paramref name="gapStart"/>), or a fixed length back from the
            // dimension line at <paramref name="reach"/> along the ray, never nearer the centre than the gap (issue #66).
            Vec2 Fixed(Vec2 centre, Vec2 ray, double reach, Vec2 gapStart, double from) =>
                style.ExtFixed is > 0 and var f && reach - f * mm > from + style.ExtGap * mm ? centre + ray * (reach - f * mm) : gapStart;
            var (cc, a, b, at) = (P(dim.Attrs["c"]), P(dim.Attrs["a"]), P(dim.Attrs["b"]), P(dim.Attrs["at"]));
            if (dim.Str("dimKind") == "angular" && Held(c.Model, dim) is { } held) (cc, a, b, at) = held;
            switch (dim.Str("dimKind"))
            {
                case "angular":
                {
                    var r = Math.Max((at - cc).Length, 4 * mm);
                    var (u, v) = ((a - cc).Normalized(), (b - cc).Normalized());
                    var (a0, a1) = (Math.Atan2(u.Y, u.X), Math.Atan2(v.Y, v.X));
                    var sweep = value * Math.PI / 180;
                    // Run from the ray that makes the measured sector counter-clockwise.
                    var ccwFromU = (((a1 - a0) % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
                    var start = Math.Abs(ccwFromU - sweep) < 1e-6 ? a0 : a1;
                    // By the one chord rule (Lapin.Geometry/Chords), at least 8 pieces.
                    var n = Chords.Count(r, sweep, 8);
                    var pts = Enumerable.Range(0, n + 1).Select(i => cc + new Vec2(Math.Cos(start + sweep * i / n), Math.Sin(start + sweep * i / n)) * r).ToList();
                    mk.Polyline(pts, false, "DIMENSION", w, lineColor, lineDash);
                    // Extension lines along the rays out to the arc (from the gap, or a fixed length back from the arc), unless left out.
                    foreach (var (k, ray, p) in new[] { (0, u, a), (1, v, b) })
                        if ((p - cc).Length < r && !style.SuppressesExtension(k, 2))
                            mk.Line(Fixed(cc, ray, r, p + ray * (style.ExtGap * mm), (p - cc).Length), cc + ray * (r + style.ExtOver * mm), "DIMENSION", extW, extColor, extDash);
                    Arrow(pts[0], (pts[0] - pts[1]).Normalized());
                    Arrow(pts[^1], (pts[^1] - pts[^2]).Normalized());
                    var mid = start + sweep / 2;
                    var tat = cc + new Vec2(Math.Cos(mid), Math.Sin(mid)) * (r + style.TextGap * mm);
                    Label(tat, FormatAngle(value, style.Precision), mid * 180 / Math.PI - 90);
                    mk.Snaps.AddRange([cc, pts[0], pts[^1]]);
                    break;
                }
                case "radial" or "diameter":
                {
                    var round = Round(c.Model, dim);
                    var centre = round?.C ?? cc;
                    var r = round?.R ?? (a - cc).Length;
                    var dir = ((at - centre).Length > 1e-9 ? at - centre : a - centre).Normalized();
                    if (dir.Length < 1e-9) dir = new Vec2(1, 0);
                    var onCircle = centre + dir * r;
                    var diameter = dim.Str("dimKind") == "diameter";
                    var from = diameter ? centre - dir * r : centre;
                    var to = (at - centre).Length > r ? at : onCircle;
                    mk.Line(from, to, "DIMENSION", w, lineColor, lineDash);
                    Arrow(onCircle, dir);
                    if (diameter) Arrow(centre - dir * r, -dir);
                    var s = (diameter ? "Ø" : "R") + Standards.FormatDimension(value, style.Unit, style.Precision, style.SuppressesZeros);
                    var ang = Math.Atan2(dir.Y, dir.X) * 180 / Math.PI;
                    Label(to + dir * (style.TextGap * mm), s, ang, dir.X >= 0 ? "start" : "end", "middle");
                    mk.Snaps.AddRange([centre, onCircle]);
                    break;
                }
                case "arc":
                {
                    var round = Round(c.Model, dim);
                    var (centre, r, s0, sw) = round is { Arc: { } arc } ? (arc.C, arc.R, arc.Start, arc.Sweep) : (cc, dim.Num("r"), dim.Num("start"), dim.Num("sweep"));
                    if (r < 1e-9 || Math.Abs(sw) < 1e-9) break;
                    var off = Math.Max((at - centre).Length, r + 4 * mm);
                    var n = Chords.Count(off, sw, 8);
                    var pts = Enumerable.Range(0, n + 1).Select(i => centre + new Vec2(Math.Cos(s0 + sw * i / n), Math.Sin(s0 + sw * i / n)) * off).ToList();
                    mk.Polyline(pts, false, "DIMENSION", w, lineColor, lineDash);
                    foreach (var (k, t) in new[] { (0, s0), (1, s0 + sw) })
                    {
                        if (style.SuppressesExtension(k, 2)) continue;
                        var ray = new Vec2(Math.Cos(t), Math.Sin(t));
                        mk.Line(Fixed(centre, ray, off, centre + ray * (r + style.ExtGap * mm), r), centre + ray * (off + style.ExtOver * mm), "DIMENSION", extW, extColor, extDash);
                    }
                    Arrow(pts[0], (pts[0] - pts[1]).Normalized());
                    Arrow(pts[^1], (pts[^1] - pts[^2]).Normalized());
                    var mid = s0 + sw / 2;
                    Label(centre + new Vec2(Math.Cos(mid), Math.Sin(mid)) * (off + style.TextGap * mm), "⌒" + Standards.FormatDimension(value, style.Unit, style.Precision, style.SuppressesZeros), mid * 180 / Math.PI - 90);
                    break;
                }
                case "slope":
                {
                    var d = b - a;
                    if (d.Length < 1e-9) break;
                    var u = d.Normalized();
                    var down = u.Y <= 0 ? u : -u;
                    var mid = (a + b) * 0.5;
                    var n = u.Perp.Y >= 0 ? u.Perp : -u.Perp;
                    var lift = n * (style.TextGap * mm * 2);
                    var len = Math.Min(d.Length * 0.5, 18 * mm);
                    var (p, q) = (mid - down * (len / 2) + lift, mid + down * (len / 2) + lift);
                    mk.Line(p, q, "DIMENSION", w, lineColor);
                    Arrow(q, down);
                    Label(mid + lift + n * (style.TextGap * mm), FormatSlope(value, dim.Str("format") ?? "auto", style.Precision), Math.Atan2(u.Y, u.X) * 180 / Math.PI);
                    mk.Snaps.AddRange([a, b]);
                    break;
                }
                case "level":
                {
                    // The level mark: a triangle on the point, a line out to the text.
                    var s = 2.5 * mm;
                    var side = at.X >= a.X ? 1 : -1;
                    mk.Polyline([a, a + new Vec2(-s * 0.6, s), a + new Vec2(s * 0.6, s)], true, "DIMENSION", w, lineColor);
                    var end = a + new Vec2(side * Math.Max(Math.Abs(at.X - a.X), 12 * mm), s);
                    mk.Line(a + new Vec2(side * s * 0.6, s), end, "DIMENSION", w, lineColor);
                    Label(a + new Vec2(side * s, s + style.TextGap * mm), LevelText(c.Book, dim, value, style.Unit), 0, side > 0 ? "start" : "end");
                    mk.Snaps.Add(a);
                    break;
                }
                case "coordinate":
                {
                    // The point marked with a small circle and cross, a leader to a shoulder, the two lines above and below it.
                    var r0 = 1.2 * mm;
                    mk.Polyline(ViewMark.Circle(a, r0, 16), true, "DIMENSION", w, lineColor);
                    mk.Line(a - new Vec2(r0 * 1.6, 0), a + new Vec2(r0 * 1.6, 0), "DIMENSION", w, lineColor).Line(a - new Vec2(0, r0 * 1.6), a + new Vec2(0, r0 * 1.6), "DIMENSION", w, lineColor);
                    var (l1, l2) = CoordinateText(c.Model, a, dim.Str("system"), style.Unit);
                    var side = at.X >= a.X ? 1 : -1;
                    var shoulder = Math.Max(TextMeasure.Width(l1, text), TextMeasure.Width(l2, text)) * mm;
                    var dir = at - a;
                    if (dir.Length > r0) mk.Line(a + dir.Normalized() * r0, at, "DIMENSION", w, lineColor);
                    var end = at + new Vec2(side * shoulder, 0);
                    mk.Line(at, end, "DIMENSION", w, lineColor);
                    var anchor = side > 0 ? "start" : "end";
                    var from = at + new Vec2(side * style.TextGap * mm, 0);
                    mk.Text(new PenText(from + new Vec2(0, style.TextGap * mm), l1, text.Height, 0, anchor, "bottom", text.Bold, text.Font, text.Italic), color);
                    // Both on their baselines (every reader of a drawing honours that): the second a text's height and a gap under the shoulder.
                    mk.Text(new PenText(from - new Vec2(0, style.TextGap * mm + text.Height * mm), l2, text.Height, 0, anchor, "bottom", text.Bold, text.Font, text.Italic), color);
                    mk.Snaps.Add(a);
                    break;
                }
                case "ordinate":
                {
                    mk.Line(a, at, "DIMENSION", w, lineColor);
                    var dir = (at - a).Length > 1e-9 ? (at - a).Normalized() : new Vec2(0, 1);
                    Label(at + dir * (style.TextGap * mm), Standards.FormatDimension(value, style.Unit, style.Precision, style.SuppressesZeros), Math.Atan2(dir.Y, dir.X) * 180 / Math.PI, dir.X >= 0 ? "start" : "end", "middle");
                    mk.Snaps.Add(a);
                    break;
                }
            }
            if (mk.Lines.Count > 0) yield return mk;
        }
    }

    /// <summary>A new dimension of these kinds from the command's arguments (null when the kind is not one of these).</summary>
    public static JsonObject? Attrs(ICommandContext ctx, string viewId, JsonObject a)
    {
        var kind = (string?)a["dimKind"];
        if (kind == null || kind == "linear" || kind == "aligned") return null;
        if (!Kinds.Contains(kind)) throw new CommandException($"„{kind}” is not a kind of dimension (aligned, linear, {string.Join(", ", Kinds)}).");
        var attrs = new JsonObject { ["viewId"] = viewId, ["dimKind"] = kind, ["at"] = J(P(a["at"])) };
        foreach (var k in new[] { "c", "a", "b", "o" }) if (a[k] is JsonArray) attrs[k] = J(P(a[k]));
        if ((string?)a["dimStyle"] is { } ds) attrs["dimStyle"] = ds;
        switch (kind)
        {
            case "angular":
                if (a["c"] is null || a["a"] is null || a["b"] is null) throw new CommandException("An angle needs its vertex and a point on each side.");
                // Held on two straight path elements (decision 738): each its element and the way along its axis from the vertex.
                if (a["holds"] is JsonArray held)
                {
                    if (held.Count != 2) throw new CommandException("An angle holds on two elements.");
                    var keep = new JsonArray();
                    foreach (var n in held)
                    {
                        var id = (string?)n?["element"] ?? throw new CommandException("Which element does the angle hold on?");
                        if (ctx.Model.Get(id) is not { } e || e.Attrs["start"] is not JsonArray || e.Attrs["end"] is not JsonArray)
                            throw new CommandException("An angle holds on straight elements with an axis (walls, beams).");
                        keep.Add((JsonNode)new JsonObject { ["element"] = id, ["way"] = (Json.Number(n!["way"]) ?? 1) < 0 ? -1 : 1 });
                    }
                    attrs["holds"] = keep;
                    // Its padlock closed (decision 739): the angle kept from now on, at what it reads.
                    if (a[AngleLocks.Key] is JsonValue lv && lv.TryGetValue<bool>(out var locked) && locked) attrs[AngleLocks.Key] = true;
                    attrs["r"] = Math.Max((P(a["at"]) - P(a["c"])).Length, 0.05);
                    var made = new Entity("", IfcClass.Annotation, "", attrs, "DIMENSION");
                    if (Held(ctx.Model, made) is null) throw new CommandException("The two elements are parallel: there is no angle between them.");
                }
                break;
            case "radial" or "diameter" or "arc":
                if ((string?)a["shape"] is { } sid)
                {
                    var d = Drafts.Require(ctx.Model, sid, editable: false);
                    var shape = Drafts.ShapeOf(d);
                    if (kind == "arc" ? shape is not Arc : shape is not (Circle or Arc)) throw new CommandException(kind == "arc" ? "An arc length is measured on an arc." : "A radius is measured on a circle or an arc.");
                    attrs["shape"] = d.Id;
                }
                else if (kind == "arc")
                {
                    if (Json.Number(a["r"]) is not { } r || Json.Number(a["sweep"]) is not { } sw) throw new CommandException("An arc length needs an arc.");
                    (attrs["r"], attrs["start"], attrs["sweep"]) = (r, Json.Number(a["start"]) ?? 0, sw);
                }
                else if (a["c"] is null || a["a"] is null) throw new CommandException("A radius needs a centre and a point on the circle, or the circle.");
                break;
            case "slope":
                if (a["a"] is null || a["b"] is null) throw new CommandException("A slope needs two points on its line.");
                if ((string?)a["format"] is { } f) attrs["format"] = SlopeFormats.Contains(f) ? f : throw new CommandException($"A slope is written as {string.Join(", ", SlopeFormats)}.");
                break;
            case "level":
                if (a["a"] is null) throw new CommandException("A level needs its point.");
                if (Json.Number(a["z"]) is { } z) attrs["z"] = z;
                // Its own decimals and trailing zeros over the project's levels' (follow-up to forum #81).
                if (a["decimals"] is { } dn) attrs["levelDecimals"] = Standards.LevelDecimalsOf(dn) ?? throw new CommandException($"A level is written with 0 to {Standards.MaxLevelDecimals} decimals.");
                if ((string?)a["zeros"] is { } zs && zs != "project") attrs["levelZeros"] = zs is "keep" or "suppress" ? zs : throw new CommandException("A level's trailing zeros are kept, suppressed or as the project's.");
                break;
            case "ordinate":
                if (a["a"] is null) throw new CommandException("An ordinate needs its point.");
                if (a["o"] is null) attrs["o"] = J(new Vec2(0, 0));
                attrs["axis"] = (string?)a["axis"] == "y" ? "y" : "x";
                break;
            case "coordinate":
            {
                if (a["a"] is null) throw new CommandException("A spot coordinate needs its point.");
                var view = Views.Find(ctx.Model, viewId);
                if (view == null || view.Str("kind") is not ("plan" or null)) throw new CommandException("A spot coordinate is written on a plan: a section or elevation has no place on the map.");
                var system = (string?)a["system"] ?? (Location.Survey(ctx.Model) != null ? "map" : "project");
                if (!CoordinateSystems.Contains(system)) throw new CommandException("A spot coordinate reads the map (map), the project base point (project) or the plan's origin (internal).");
                if (system == "map" && Location.Survey(ctx.Model) == null) throw new CommandException("The project has no survey point: Manage → Location ties it to the map first.");
                attrs["system"] = system;
                break;
            }
        }
        return attrs;
    }

    /// <summary>A radius or diameter typed: the drafted circle or arc it holds on takes it.</summary>
    public static void Drive(ICommandContext ctx, Entity dim, double value)
    {
        var kind = dim.Str("dimKind");
        if (kind == "angular" && dim.Attrs["holds"] != null) { DriveAngle(ctx, dim, value); return; }
        if (kind is not ("radial" or "diameter")) throw new CommandException("Only a radius or a diameter held on a drafted circle, or an angle held on two elements, drives it.");
        if (dim.Str("shape") is not { } id || ctx.Model.Get(id) is not { } d) throw new CommandException("The dimension holds on no drafted circle: there is nothing to change.");
        var r = kind == "diameter" ? value / 2 : value;
        if (r <= 0) throw new CommandException("A radius is above zero.");
        Shape next = Drafts.ShapeOf(d) switch
        {
            Circle c => c with { R = r },
            Arc a => a with { R = r },
            _ => throw new CommandException("The dimension holds on no drafted circle."),
        };
        Drafts.Set(ctx, Drafts.Require(ctx.Model, id), next);
    }

    /// <summary>
    /// An angle held on two elements typed (decision 738): the second element turned about the vertex until the angle reads
    /// <paramref name="value"/> degrees, as element.transform turns it (its joins worked out again); one undo step.
    /// </summary>
    static void DriveAngle(ICommandContext ctx, Entity dim, double value)
    {
        if (value <= 0 || value >= 180) throw new CommandException("An angle between two elements is above 0° and under 180°.");
        if (Held(ctx.Model, dim) is not { } held) throw new CommandException("The angle lost one of its elements: there is nothing to turn.");
        var (c, a, b, _) = held;
        var (u, v) = ((a - c).Normalized(), (b - c).Normalized());
        var now = Math.Abs(Math.Atan2(Vec2.Cross(u, v), Vec2.Dot(u, v))) * 180 / Math.PI;
        var turn = (Vec2.Cross(u, v) >= 0 ? 1 : -1) * (value - now);
        if (Math.Abs(turn) < 1e-9) return;
        var id = (string)dim.Attrs["holds"]![1]!["element"]!;
        // Locked (decision 739): kept at the new value from now on, which the lock's follow-up then holds to.
        if (AngleLocks.IsLocked(dim)) AngleLocks.Record(ctx, ctx.Model.Get(dim.Id)!, value);
        var t = ElementTransform.Run(ctx, new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "rotate", ["center"] = J(c), ["angle"] = turn });
        ElementTransform.JoinWhatMeets(ctx, t);
    }
}
