using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>Where a dimension's end holds on: an element, the point's offset from the element's middle on the drawing, the direction of the edge it lies on.</summary>
public sealed record DimEndRef(string Element, Vec2 Offset, Vec2 Dir)
{
    public JsonObject ToJson() => new() { ["element"] = Element, ["offset"] = new JsonArray(Offset.X, Offset.Y), ["dir"] = new JsonArray(Dir.X, Dir.Y) };
    public static DimEndRef? From(JsonNode? n) => n is JsonObject o && (string?)o["element"] is { Length: > 0 } e && o["offset"] is JsonArray off && o["dir"] is JsonArray d
        ? new DimEndRef(e, new Vec2(Json.Number(off[0]) ?? 0, Json.Number(off[1]) ?? 0), new Vec2(Json.Number(d[0]) ?? 0, Json.Number(d[1]) ?? 0))
        : null;
}

/// <summary>
/// Aligned dimensions on a view (the old Lapin's associative dimensions): an
/// end placed on an edge the view cuts keeps that element, the edge's
/// direction and where the point sits from the element's middle. Each time the
/// view is drawn the end is found anew: the element's middle now plus that
/// offset, laid on the element's nearest edge of the same direction (so a wall
/// moved, or made thicker, takes the end along). Nothing is written back. An
/// end whose element is gone, or has no edge that way any more, is an orphan:
/// drawn where it was.
/// </summary>
public static class Dimensions
{
    /// <summary>How far (m) an end may be from an edge of its direction and still lie on it.</summary>
    const double Reach = 0.6;
    /// <summary>Directions this close (sine of the angle) are the same.</summary>
    const double Parallel = 0.01;

    static IEnumerable<(Vec2 A, Vec2 B)> Edges(ViewDrawing d, string element) =>
        d.Cuts.Where(p => p.Element == element).SelectMany(p => p.Loops.SelectMany(l => l.Select((q, i) => (q, l[(i + 1) % l.Length]))));

    /// <summary>The middle of what an element draws: its cut areas' box centre.</summary>
    static Vec2? Middle(ViewDrawing d, string element)
    {
        var pts = d.Cuts.Where(p => p.Element == element).SelectMany(p => p.Loops.SelectMany(l => l)).ToList();
        return pts.Count == 0 ? null : new Vec2((pts.Min(p => p.X) + pts.Max(p => p.X)) / 2, (pts.Min(p => p.Y) + pts.Max(p => p.Y)) / 2);
    }

    static (double Dist, Vec2 Foot) Foot(Vec2 p, Vec2 a, Vec2 b)
    {
        var d = b - a;
        var t = Math.Clamp(Vec2.Dot(p - a, d) / Math.Max(Vec2.Dot(d, d), 1e-18), 0, 1);
        var f = a + d * t;
        return ((p - f).Length, f);
    }

    /// <summary>The element whose cut edge lies within <paramref name="tolerance"/> of the point, and the reference that keeps it; null: a free end.</summary>
    public static DimEndRef? Associate(ViewDrawing d, Vec2 p, double tolerance)
    {
        (DPolygon Poly, Vec2 A, Vec2 B, double Dist)? best = null;
        foreach (var poly in d.Cuts)
            foreach (var l in poly.Loops)
                for (var i = 0; i < l.Length; i++)
                {
                    var (a, b) = (l[i], l[(i + 1) % l.Length]);
                    if ((b - a).Length < 1e-6) continue;
                    var (dist, _) = Foot(p, a, b);
                    if (dist <= tolerance && (best == null || dist < best.Value.Dist)) best = (poly, a, b, dist);
                }
        if (best is not { } hit || Middle(d, hit.Poly.Element) is not { } mid) return null;
        return new DimEndRef(hit.Poly.Element, p - mid, (hit.B - hit.A).Normalized());
    }

    /// <summary>The end now, or null when its element is gone or has no edge its way any more.</summary>
    public static Vec2? Resolve(ViewDrawing d, DimEndRef r)
    {
        if (Middle(d, r.Element) is not { } mid) return null;
        var q = mid + r.Offset;
        (double Dist, Vec2 P)? best = null;
        foreach (var (a, b) in Edges(d, r.Element))
        {
            var e = b - a;
            if (e.Length < 1e-9 || Math.Abs(Vec2.Cross(e.Normalized(), r.Dir)) > Parallel) continue;
            // On the edge's line: the end slides along it, so a wall made longer keeps its end where it measured.
            var u = e.Normalized();
            var onLine = a + u * Vec2.Dot(q - a, u);
            var dist = (q - onLine).Length;
            if (dist <= Reach && (best == null || dist < best.Value.Dist)) best = (dist, onLine);
        }
        return best?.P;
    }

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;

    /// <summary>A dimension's ends as drawn now, and whether any end lost its element.</summary>
    public static (Vec2 A, Vec2 B, bool Orphan) Ends(Entity dim, ViewDrawing d)
    {
        var (a, b) = (P(dim.Attrs["a"]), P(dim.Attrs["b"]));
        var orphan = false;
        if (DimEndRef.From(dim.Attrs["refA"]) is { } ra) { if (Resolve(d, ra) is { } p) a = p; else orphan = true; }
        if (DimEndRef.From(dim.Attrs["refB"]) is { } rb) { if (Resolve(d, rb) is { } p) b = p; else orphan = true; }
        return (a, b, orphan);
    }

    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        foreach (var dim in c.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "DIMENSION" && x.Str("viewId") == c.View.Id))
        {
            var (a, b, orphan) = Ends(dim, c.Drawing);
            var len = (b - a).Length;
            if (len < 1e-6) continue;
            var style = Standards.ResolveDim(c.Book, ParamsOf(dim));
            var text = Standards.ResolveText(c.Book, ParamsOf(dim));
            var color = orphan ? "#c0392b" : style.Color ?? "#000000";
            var u = (b - a) * (1 / len);
            var n = u.Perp;
            var off = Vec2.Dot(P(dim.Attrs["at"]) - a, n);
            var side = off >= 0 ? 1 : -1;
            var (da, db) = (a + n * off, b + n * off);
            var mm = c.Mm;
            var mk = new ViewMark(dim.Id);
            mk.Line(da, db, "DIMENSION", Pens.Default.Hatch, color);
            foreach (var (p, q) in new[] { (a, da), (b, db) })
            {
                // Extension lines: a gap from the measured point, past the dimension line by the overshoot.
                if (Math.Abs(off) > style.ExtGap * mm) mk.Line(p + n * (side * style.ExtGap * mm), q + n * (side * style.ExtOver * mm), "DIMENSION", Pens.Default.Hatch, color);
                var s = style.ArrowSize * mm;
                switch (style.Arrow)
                {
                    case "tick": { var t = (u + n).Normalized() * (s / 2); mk.Line(q - t, q + t, "DIMENSION", Pens.Default.Seen, color); break; }
                    case "arrow":
                    {
                        var inward = q == da ? u : -u;
                        mk.Line(q, q + inward * s + n * (s * 0.3), "DIMENSION", Pens.Default.Hatch, color).Line(q, q + inward * s - n * (s * 0.3), "DIMENSION", Pens.Default.Hatch, color);
                        break;
                    }
                    case "dot": mk.Polyline(ViewMark.Circle(q, s / 4, 12), true, "DIMENSION", Pens.Default.Seen, color); break;
                }
            }
            // The value: in the style's unit, reading left to right (bottom to top), above, on or below the line.
            var value = Standards.FormatDimension(len, style.Unit, style.Precision);
            var angle = Math.Atan2(u.Y, u.X) * 180 / Math.PI;
            var readable = -Standards.ReadableDeg(-angle);
            var up = new Vec2(-Math.Sin(readable * Math.PI / 180), Math.Cos(readable * Math.PI / 180));
            var mid = (da + db) * 0.5;
            var (at, baseline) = style.Placement switch
            {
                "centered" => (mid, "middle"),
                "below" => (mid - up * (style.TextGap * mm + text.Height * mm), "bottom"),
                _ => (mid + up * (style.TextGap * mm), "bottom"),
            };
            mk.Text(new PenText(at, value, text.Height, readable, "middle", baseline, text.Bold, text.Font), color);
            mk.Snaps.AddRange([da, db]);
            yield return mk;
        }
    }

    /// <summary>The style params an annotation names (its styles and o.* overrides).</summary>
    public static JsonObject ParamsOf(Entity e)
    {
        var p = new JsonObject();
        foreach (var k in new[] { "textStyle", "dimStyle" }) if (e.Str(k) is { } v) p[k] = v;
        if (e.Attrs["overrides"] is JsonObject o) foreach (var (k, v) in o) p["o." + k] = v?.DeepClone();
        return p;
    }
}
