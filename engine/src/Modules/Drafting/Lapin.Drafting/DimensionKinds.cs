using System.Text.Json.Nodes;
using BubbleBim.Geometry;
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
/// <item><c>angular</c>: the angle at <c>c</c> between the rays to <c>a</c> and <c>b</c>, its arc through <c>at</c>;</item>
/// <item><c>radial</c> and <c>diameter</c>: a circle's or arc's radius (R) or diameter (Ø), its centre <c>c</c> and a point <c>a</c> on it, the text at <c>at</c>; held on a drafted circle or arc (<c>shape</c>), it follows the shape and drives its radius;</item>
/// <item><c>arc</c>: an arc's length (⌒), held on a drafted arc (<c>shape</c>) or its centre, radius and angles;</item>
/// <item><c>slope</c>: the slope of the line from <c>a</c> to <c>b</c>, in % or degrees (<c>format</c>: auto, %, deg, ratio; auto: % under 15 %, degrees above), its arrow pointing down the slope;</item>
/// <item><c>level</c>: the level at <c>a</c> (in a section or elevation its height; in a plan its storey's, or <c>z</c> when given), with the level mark;</item>
/// <item><c>ordinate</c>: how far <c>a</c> lies from the origin <c>o</c> along x or y (<c>axis</c>), its leader to <c>at</c>.</item>
/// </list>
/// </summary>
public static class DimensionKinds
{
    public static readonly string[] Kinds = ["angular", "radial", "diameter", "arc", "slope", "level", "ordinate"];
    public static readonly string[] SlopeFormats = ["auto", "%", "deg", "ratio"];

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
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
        switch (kind)
        {
            case "angular":
            {
                var (u, v) = (a - cc, b - cc);
                if (u.Length < 1e-9 || v.Length < 1e-9) return (0, orphan);
                var ang = Math.Abs(Math.Atan2(Vec2.Cross(u, v), Vec2.Dot(u, v))) * 180 / Math.PI;
                // The sector the arc's point lies in: the smaller angle, or the reflex one.
                var at = P(dim.Attrs["at"]) - cc;
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

    /// <summary>Whether w lies in the smaller sector from u to v.</summary>
    static bool Between(Vec2 u, Vec2 v, Vec2 w)
    {
        var cuv = Vec2.Cross(u, v);
        return cuv >= 0 ? Vec2.Cross(u, w) >= 0 && Vec2.Cross(w, v) >= 0 : Vec2.Cross(u, w) <= 0 && Vec2.Cross(w, v) <= 0;
    }

    /// <summary>A slope as written: 12.5 %, 30.0°, 1:8.</summary>
    public static string FormatSlope(double ratio, string format)
    {
        if (double.IsInfinity(ratio)) return "∞";
        var f = format == "auto" ? (ratio < 0.15 ? "%" : "deg") : format;
        return f switch
        {
            "deg" => $"{Math.Atan(ratio) * 180 / Math.PI:0.0}°",
            "ratio" => ratio < 1e-9 ? "0" : $"1:{1 / ratio:0.#}",
            _ => $"{ratio * 100:0.0} %",
        };
    }

    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        foreach (var dim in c.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "DIMENSION" && x.Str("viewId") == c.View.Id && IsKind(x)))
        {
            var style = Standards.ResolveDim(c.Book, Dimensions.ParamsOf(dim));
            var text = Standards.ResolveText(c.Book, Dimensions.ParamsOf(dim));
            var (value, orphan) = Measure(c, c.Model, dim);
            var color = orphan ? "#c0392b" : style.Color ?? "#000000";
            var mm = c.Mm;
            var w = Pens.Default.Hatch;
            var mk = new ViewMark(dim.Id);
            void Label(Vec2 at, string s, double angleDeg = 0, string anchor = "middle", string baseline = "bottom") =>
                mk.Text(new PenText(at, dim.Str("text") is { Length: > 0 } over ? over : s, text.Height, -Standards.ReadableDeg(-angleDeg), anchor, baseline, text.Bold, text.Font), color);
            void Arrow(Vec2 tip, Vec2 dir)
            {
                var s = style.ArrowSize * mm;
                var n = dir.Perp;
                switch (style.Arrow)
                {
                    case "tick": { var t = (dir + n).Normalized() * (s / 2); mk.Line(tip - t, tip + t, "DIMENSION", Pens.Default.Seen, color); break; }
                    case "dot": mk.Polyline(ViewMark.Circle(tip, s / 4, 12), true, "DIMENSION", Pens.Default.Seen, color); break;
                    default: mk.Line(tip, tip - dir * s + n * (s * 0.3), "DIMENSION", w, color).Line(tip, tip - dir * s - n * (s * 0.3), "DIMENSION", w, color); break;
                }
            }
            var (cc, a, b, at) = (P(dim.Attrs["c"]), P(dim.Attrs["a"]), P(dim.Attrs["b"]), P(dim.Attrs["at"]));
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
                    var n = Math.Max(8, (int)Math.Ceiling(sweep / (Math.PI / 36)));
                    var pts = Enumerable.Range(0, n + 1).Select(i => cc + new Vec2(Math.Cos(start + sweep * i / n), Math.Sin(start + sweep * i / n)) * r).ToList();
                    mk.Polyline(pts, false, "DIMENSION", w, color);
                    // Extension lines along the rays out to the arc.
                    foreach (var (ray, p) in new[] { (u, a), (v, b) }) if ((p - cc).Length < r) mk.Line(p + ray * (style.ExtGap * mm), cc + ray * (r + style.ExtOver * mm), "DIMENSION", w, color);
                    Arrow(pts[0], (pts[0] - pts[1]).Normalized());
                    Arrow(pts[^1], (pts[^1] - pts[^2]).Normalized());
                    var mid = start + sweep / 2;
                    var tat = cc + new Vec2(Math.Cos(mid), Math.Sin(mid)) * (r + style.TextGap * mm);
                    Label(tat, $"{value:0.#}°", mid * 180 / Math.PI - 90);
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
                    mk.Line(from, to, "DIMENSION", w, color);
                    Arrow(onCircle, dir);
                    if (diameter) Arrow(centre - dir * r, -dir);
                    var s = (diameter ? "Ø" : "R") + Standards.FormatDimension(value, style.Unit, style.Precision);
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
                    var n = Math.Max(8, (int)Math.Ceiling(Math.Abs(sw) / (Math.PI / 36)));
                    var pts = Enumerable.Range(0, n + 1).Select(i => centre + new Vec2(Math.Cos(s0 + sw * i / n), Math.Sin(s0 + sw * i / n)) * off).ToList();
                    mk.Polyline(pts, false, "DIMENSION", w, color);
                    foreach (var t in new[] { s0, s0 + sw })
                    {
                        var ray = new Vec2(Math.Cos(t), Math.Sin(t));
                        mk.Line(centre + ray * (r + style.ExtGap * mm), centre + ray * (off + style.ExtOver * mm), "DIMENSION", w, color);
                    }
                    Arrow(pts[0], (pts[0] - pts[1]).Normalized());
                    Arrow(pts[^1], (pts[^1] - pts[^2]).Normalized());
                    var mid = s0 + sw / 2;
                    Label(centre + new Vec2(Math.Cos(mid), Math.Sin(mid)) * (off + style.TextGap * mm), "⌒" + Standards.FormatDimension(value, style.Unit, style.Precision), mid * 180 / Math.PI - 90);
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
                    mk.Line(p, q, "DIMENSION", w, color);
                    Arrow(q, down);
                    Label(mid + lift + n * (style.TextGap * mm), FormatSlope(value, dim.Str("format") ?? "auto"), Math.Atan2(u.Y, u.X) * 180 / Math.PI);
                    mk.Snaps.AddRange([a, b]);
                    break;
                }
                case "level":
                {
                    // The level mark: a triangle on the point, a line out to the text.
                    var s = 2.5 * mm;
                    var side = at.X >= a.X ? 1 : -1;
                    mk.Polyline([a, a + new Vec2(-s * 0.6, s), a + new Vec2(s * 0.6, s)], true, "DIMENSION", w, color);
                    var end = a + new Vec2(side * Math.Max(Math.Abs(at.X - a.X), 12 * mm), s);
                    mk.Line(a + new Vec2(side * s * 0.6, s), end, "DIMENSION", w, color);
                    Label(a + new Vec2(side * s, s + style.TextGap * mm), Standards.FormatLevel(value), 0, side > 0 ? "start" : "end");
                    mk.Snaps.Add(a);
                    break;
                }
                case "ordinate":
                {
                    mk.Line(a, at, "DIMENSION", w, color);
                    var dir = (at - a).Length > 1e-9 ? (at - a).Normalized() : new Vec2(0, 1);
                    Label(at + dir * (style.TextGap * mm), Standards.FormatDimension(value, style.Unit, style.Precision), Math.Atan2(dir.Y, dir.X) * 180 / Math.PI, dir.X >= 0 ? "start" : "end", "middle");
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
                break;
            case "ordinate":
                if (a["a"] is null) throw new CommandException("An ordinate needs its point.");
                if (a["o"] is null) attrs["o"] = J(new Vec2(0, 0));
                attrs["axis"] = (string?)a["axis"] == "y" ? "y" : "x";
                break;
        }
        return attrs;
    }

    /// <summary>A radius or diameter typed: the drafted circle or arc it holds on takes it.</summary>
    public static void Drive(ICommandContext ctx, Entity dim, double value)
    {
        var kind = dim.Str("dimKind");
        if (kind is not ("radial" or "diameter")) throw new CommandException("Only a radius or a diameter held on a drafted circle drives it.");
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
}
