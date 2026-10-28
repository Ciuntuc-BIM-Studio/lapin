using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// Where a dimension's end holds on:
/// <list type="bullet">
/// <item>"edge": an edge an element's cut draws: the point and the edge's
/// direction kept in the element's own frame (its axis, or its placement and
/// rotation), so the end follows a move, a turn, a thicker wall; elements
/// without a frame keep the point from their middle (the old way);</item>
/// <item>"grid": a grid axis (by its tag), how far along it;</item>
/// <item>"axis": a wall's (or beam's) centre line, how far along it.</item>
/// </list>
/// </summary>
public sealed record DimEndRef(string Kind, string Element, Vec2 Offset, Vec2 Dir, Vec2? Local = null, Vec2? LocalDir = null, string? Tag = null, double S = 0)
{
    static JsonArray J(Vec2 p) => new(p.X, p.Y);
    static Vec2? V(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : null;

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["kind"] = Kind };
        switch (Kind)
        {
            case "grid": o["grid"] = Element; o["tag"] = Tag; o["s"] = S; break;
            case "axis": o["element"] = Element; o["s"] = S; break;
            default:
                o["element"] = Element; o["offset"] = J(Offset); o["dir"] = J(Dir);
                if (Local is { } l && LocalDir is { } ld) { o["local"] = J(l); o["localDir"] = J(ld); }
                break;
        }
        return o;
    }

    public static DimEndRef? From(JsonNode? n)
    {
        if (n is not JsonObject o) return null;
        switch ((string?)o["kind"] ?? "edge")
        {
            case "grid" when (string?)o["grid"] is { } g && (string?)o["tag"] is { } t: return new("grid", g, default, default, Tag: t, S: Json.Number(o["s"]) ?? 0);
            case "axis" when (string?)o["element"] is { } e: return new("axis", e, default, default, S: Json.Number(o["s"]) ?? 0);
            case "edge" when (string?)o["element"] is { Length: > 0 } e && V(o["offset"]) is { } off && V(o["dir"]) is { } d:
                return new("edge", e, off, d, V(o["local"]), V(o["localDir"]));
        }
        return null;
    }
}

/// <summary>
/// Dimensions on a view (the old Lapin's associative dimensions): aligned, or
/// linear along a direction (<c>dir</c>). A dimension is a string of points
/// (<c>pts</c>, two or more) on one dimension line through <c>at</c>: one
/// object however many segments it has, like a polyline. Each point on an
/// element, a grid axis or a wall's centre line (<c>refs</c>) is found anew
/// each time the view is drawn; a point whose element is gone is an orphan,
/// drawn red where it was, until freed. Typing a segment's value moves what
/// lies after it in the string.
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

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;

    /// <summary>An element's frame in plan: its axis (start → end), or its placement and rotation; null when it has none.</summary>
    public static (Vec2 O, Vec2 U)? Frame(Entity? e)
    {
        if (e == null) return null;
        if (e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray)
        {
            var (a, b) = (P(e.Attrs["start"]), P(e.Attrs["end"]));
            return (b - a).Length < 1e-9 ? null : (a, (b - a).Normalized());
        }
        if (e.Attrs["at"] is JsonArray)
        {
            var r = e.Num("rotation");
            return (P(e.Attrs["at"]), new Vec2(Math.Cos(r), Math.Sin(r)));
        }
        return null;
    }

    static Vec2 ToLocal((Vec2 O, Vec2 U) f, Vec2 p) { var v = p - f.O; return new Vec2(Vec2.Dot(v, f.U), Vec2.Dot(v, f.U.Perp)); }
    static Vec2 FromLocal((Vec2 O, Vec2 U) f, Vec2 l) => f.O + f.U * l.X + f.U.Perp * l.Y;
    static Vec2 DirToLocal((Vec2 O, Vec2 U) f, Vec2 d) => new(Vec2.Dot(d, f.U), Vec2.Dot(d, f.U.Perp));
    static Vec2 DirFromLocal((Vec2 O, Vec2 U) f, Vec2 l) => f.U * l.X + f.U.Perp * l.Y;

    /// <summary>
    /// What an end placed at <paramref name="p"/> holds on: a cut edge within
    /// <paramref name="tolerance"/>, else (in a plan) a grid axis, else a wall's
    /// or beam's centre line; null: a free end.
    /// </summary>
    public static DimEndRef? Associate(IModelView m, string viewKind, ViewDrawing d, Vec2 p, double tolerance)
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
        if (best is { } hit && Middle(d, hit.Poly.Element) is { } mid)
        {
            var dir = (hit.B - hit.A).Normalized();
            if (viewKind == "plan" && Frame(m.Get(hit.Poly.Element)) is { } f)
                return new DimEndRef("edge", hit.Poly.Element, p - mid, dir, ToLocal(f, p), DirToLocal(f, dir));
            return new DimEndRef("edge", hit.Poly.Element, p - mid, dir);
        }
        if (viewKind != "plan") return null;
        foreach (var grid in m.OfClass(IfcClass.Grid))
            foreach (var ax in Grids.Axes(grid))
                if (Foot(p, ax.A, ax.B).Dist <= tolerance)
                    return new DimEndRef("grid", grid.Id, default, default, Tag: ax.Tag, S: Vec2.Dot(p - ax.A, ax.Dir));
        foreach (var e in m.Entities.Where(e => e.Class is IfcClass.Wall or IfcClass.Beam && e.Attrs["start"] is JsonArray))
            if (Frame(e) is { } f && Foot(p, P(e.Attrs["start"]), P(e.Attrs["end"])).Dist <= tolerance)
                return new DimEndRef("axis", e.Id, default, default, S: Vec2.Dot(p - f.O, f.U));
        return null;
    }

    /// <summary>The end now, or null when what it holds on is gone.</summary>
    public static Vec2? Resolve(IModelView m, string viewKind, ViewDrawing d, DimEndRef r)
    {
        switch (r.Kind)
        {
            case "grid":
            {
                if (m.Get(r.Element) is not { Class: IfcClass.Grid } grid || Grids.Axes(grid).FirstOrDefault(a => a.Tag == r.Tag) is not { } ax) return null;
                return ax.A + ax.Dir * r.S;
            }
            case "axis":
                return Frame(m.Get(r.Element)) is { } f ? f.O + f.U * r.S : null;
        }
        Vec2 q, dir;
        if (viewKind == "plan" && r.Local is { } local && r.LocalDir is { } ld && Frame(m.Get(r.Element)) is { } frame && Middle(d, r.Element) != null)
            (q, dir) = (FromLocal(frame, local), DirFromLocal(frame, ld).Normalized());
        else if (Middle(d, r.Element) is { } mid)
            (q, dir) = (mid + r.Offset, r.Dir);
        else return null;
        (double Dist, Vec2 P)? best = null;
        foreach (var (a, b) in Edges(d, r.Element))
        {
            var e = b - a;
            if (e.Length < 1e-9 || Math.Abs(Vec2.Cross(e.Normalized(), dir)) > Parallel) continue;
            // On the edge's line: the end slides along it, so a wall made longer keeps its end where it measured.
            var u = e.Normalized();
            var onLine = a + u * Vec2.Dot(q - a, u);
            var dist = (q - onLine).Length;
            if (dist <= Reach && (best == null || dist < best.Value.Dist)) best = (dist, onLine);
        }
        return best?.P;
    }

    // ------------------------------------------------------------ the string: its points, what each holds on, its texts

    /// <summary>A dimension's stored points, each with what it holds on (null: a free point), from <c>pts</c>/<c>refs</c> (or an older dimension's <c>a</c>, <c>b</c>, <c>refA</c>, <c>refB</c>).</summary>
    public static List<(Vec2 P, DimEndRef? Ref)> Stations(Entity dim) => Stations(dim.Attrs);

    public static List<(Vec2 P, DimEndRef? Ref)> Stations(JsonObject attrs)
    {
        var o = new List<(Vec2, DimEndRef?)>();
        if (attrs["pts"] is JsonArray pts)
        {
            var refs = attrs["refs"] as JsonArray;
            for (var i = 0; i < pts.Count; i++) o.Add((P(pts[i]), refs != null && i < refs.Count ? DimEndRef.From(refs[i]) : null));
            return o;
        }
        o.Add((P(attrs["a"]), DimEndRef.From(attrs["refA"])));
        o.Add((P(attrs["b"]), DimEndRef.From(attrs["refB"])));
        return o;
    }

    /// <summary>The value written over each segment instead of its measure (null: the measure): <c>texts</c>, or a one-segment dimension's <c>text</c>.</summary>
    public static List<string?> TextsOf(JsonObject attrs, int segments)
    {
        var o = new List<string?>();
        var texts = attrs["texts"] as JsonArray;
        for (var i = 0; i < segments; i++)
            o.Add(texts != null && i < texts.Count && texts[i] is JsonValue v && v.TryGetValue<string>(out var t) && t.Length > 0 ? t : null);
        if (texts == null && segments == 1 && (string?)attrs["text"] is { Length: > 0 } one) o[0] = one;
        return o;
    }

    /// <summary>
    /// A string's points and texts written into <paramref name="attrs"/> (<c>pts</c>,
    /// <c>refs</c> when any point holds on something, <c>texts</c> when any segment
    /// has its own); what older dimensions kept (<c>a</c>, <c>b</c>, <c>refA</c>,
    /// <c>refB</c>, <c>chain</c>, <c>chainIx</c>) goes. Texts not given stay as they are.
    /// </summary>
    public static void Write(JsonObject attrs, IReadOnlyList<(Vec2 P, DimEndRef? Ref)> stations, IReadOnlyList<string?>? texts = null)
    {
        foreach (var k in new[] { "a", "b", "refA", "refB", "chain", "chainIx" }) attrs.Remove(k);
        attrs["pts"] = new JsonArray(stations.Select(x => (JsonNode)J(x.P)).ToArray());
        if (stations.Any(x => x.Ref != null)) attrs["refs"] = new JsonArray(stations.Select(x => (JsonNode?)x.Ref?.ToJson()).ToArray());
        else attrs.Remove("refs");
        if (texts == null) return;
        if (texts.Count == 1) { attrs.Remove("texts"); if (texts[0] is { } t0) attrs["text"] = t0; else attrs.Remove("text"); return; }
        attrs.Remove("text");
        if (texts.Any(t => t != null)) attrs["texts"] = new JsonArray(texts.Select(t => t == null ? null : (JsonNode)JsonValue.Create(t)).ToArray());
        else attrs.Remove("texts");
    }

    /// <summary>
    /// How far each segment's value is moved from where the style puts it
    /// (<c>textAt</c>, plan metres, null: not moved), so close values can stand
    /// clear of one another; it moves with its segment.
    /// </summary>
    public static List<Vec2?> OffsetsOf(JsonObject attrs, int segments)
    {
        var a = attrs["textAt"] as JsonArray;
        return Enumerable.Range(0, segments).Select(i => a != null && i < a.Count && a[i] is JsonArray v && v.Count >= 2 ? P(v) : (Vec2?)null).ToList();
    }

    /// <summary>The segments' text offsets written into <paramref name="attrs"/> (none moved: the attribute goes).</summary>
    public static void WriteOffsets(JsonObject attrs, IReadOnlyList<Vec2?> offsets)
    {
        if (offsets.Any(o => o is { } v && v.Length > 1e-9)) attrs["textAt"] = new JsonArray(offsets.Select(o => o is { } v && v.Length > 1e-9 ? (JsonNode)new JsonArray(Math.Round(v.X, 6), Math.Round(v.Y, 6)) : null).ToArray());
        else attrs.Remove("textAt");
    }

    static JsonArray J(Vec2 p) => new(p.X, p.Y);

    /// <summary>A linear or aligned dimension (not one of <see cref="DimensionKinds"/>).</summary>
    public static bool IsLinear(Entity? e) => e is { Class: IfcClass.Annotation, PredefinedType: "DIMENSION" } && !DimensionKinds.IsKind(e);

    /// <summary>The string's points as drawn now (each on what it holds on), and whether any lost it.</summary>
    public static (List<Vec2> Pts, bool Orphan) Points(IModelView m, string viewKind, Entity dim, ViewDrawing d)
    {
        var orphan = false;
        var o = new List<Vec2>();
        foreach (var (p, r) in Stations(dim))
        {
            if (r == null) { o.Add(p); continue; }
            if (Resolve(m, viewKind, d, r) is { } q) o.Add(q);
            else { o.Add(p); orphan = true; }
        }
        return (o, orphan);
    }

    /// <summary>A dimension's first and last points as drawn now, and whether any point lost what it held on.</summary>
    public static (Vec2 A, Vec2 B, bool Orphan) Ends(IModelView m, string viewKind, Entity dim, ViewDrawing d)
    {
        var (pts, orphan) = Points(m, viewKind, dim, d);
        return (pts[0], pts[^1], orphan);
    }

    /// <summary>The direction a dimension measures along: its own <c>dir</c> (linear), else from end to end (aligned).</summary>
    public static Vec2? Along(Entity dim, Vec2 a, Vec2 b) => Along(dim.Attrs, a, b);

    public static Vec2? Along(JsonObject attrs, Vec2 a, Vec2 b)
    {
        if (attrs["dir"] is JsonArray && P(attrs["dir"]) is var d && d.Length > 1e-9) return d.Normalized();
        return (b - a).Length < 1e-9 ? null : (b - a).Normalized();
    }

    /// <summary>A string's direction: its <c>dir</c>, else from its first point to its last.</summary>
    public static Vec2? Along(Entity dim, IReadOnlyList<Vec2> pts) => Along(dim.Attrs, pts[0], pts[^1]);

    /// <summary>What a dimension measures: the distance along its direction.</summary>
    public static double Value(Entity dim, Vec2 a, Vec2 b) => Along(dim, a, b) is { } u ? Math.Abs(Vec2.Dot(b - a, u)) : 0;

    /// <summary>What each segment of a string measures, along the string's direction.</summary>
    public static List<double> Values(Entity dim, IReadOnlyList<Vec2> pts) =>
        Along(dim, pts) is { } u ? Enumerable.Range(0, pts.Count - 1).Select(i => Math.Abs(Vec2.Dot(pts[i + 1] - pts[i], u))).ToList() : Enumerable.Repeat(0.0, pts.Count - 1).ToList();

    /// <summary>How a string lies: its direction, the square to it, and each point carried square onto the dimension line through <c>at</c> (with how far it went).</summary>
    public sealed record Layout(Vec2 U, Vec2 N, List<Vec2> Line, List<double> Off);

    public static Layout? Lay(Entity dim, IReadOnlyList<Vec2> pts)
    {
        if (Along(dim, pts) is not { } u) return null;
        var n = u.Perp;
        var at = P(dim.Attrs["at"]);
        var off = pts.Select(p => Vec2.Dot(at - p, n)).ToList();
        return new Layout(u, n, pts.Select((p, i) => p + n * off[i]).ToList(), off);
    }

    /// <summary>The segment of a string nearest <paramref name="at"/> (on its dimension line).</summary>
    public static int SegmentAt(Layout lay, Vec2 at)
    {
        var (best, bd) = (0, double.PositiveInfinity);
        for (var i = 0; i + 1 < lay.Line.Count; i++)
        {
            var dist = Foot(at, lay.Line[i], lay.Line[i + 1]).Dist;
            if (dist < bd) (best, bd) = (i, dist);
        }
        return best;
    }

    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        foreach (var dim in c.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "DIMENSION" && x.Str("viewId") == c.View.Id && !DimensionKinds.IsKind(x)))
        {
            var (pts, orphan) = Points(c.Model, c.Kind, dim, c.Drawing);
            if (pts.Count < 2 || Lay(dim, pts) is not { } lay) continue;
            var (u, n, line, offs) = (lay.U, lay.N, lay.Line, lay.Off);
            var values = Enumerable.Range(0, pts.Count - 1).Select(i => Math.Abs(Vec2.Dot(line[i + 1] - line[i], u))).ToList();
            if (values.All(v => v < 1e-6)) continue;
            var style = Standards.ResolveDim(c.Book, ParamsOf(dim));
            var text = Standards.ResolveText(c.Book, ParamsOf(dim));
            var texts = TextsOf(dim.Attrs, pts.Count - 1);
            var moved = OffsetsOf(dim.Attrs, pts.Count - 1);
            var textAt = new JsonArray();
            var color = orphan ? "#c0392b" : style.Color ?? "#000000";
            var mm = c.Mm;
            var mk = new ViewMark(dim.Id);
            // One dimension line, from the first point to the last as they lie along it.
            var ts = line.Select(q => Vec2.Dot(q - line[0], u)).ToList();
            mk.Line(line[0] + u * ts.Min(), line[0] + u * ts.Max(), "DIMENSION", Pens.Default.Hatch, color);
            var s = style.ArrowSize * mm;
            // Each point once: its extension line (a gap from the measured point, past the dimension line by the overshoot), its tick or dot.
            for (var i = 0; i < pts.Count; i++)
            {
                var (p, q, off) = (pts[i], line[i], offs[i]);
                var side = off >= 0 ? 1 : -1;
                if (Math.Abs(off) > style.ExtGap * mm) mk.Line(p + n * (side * style.ExtGap * mm), q + n * (side * style.ExtOver * mm), "DIMENSION", Pens.Default.Hatch, color);
                switch (style.Arrow)
                {
                    case "tick": { var t = (u + n).Normalized() * (s / 2); mk.Line(q - t, q + t, "DIMENSION", Pens.Default.Seen, color); break; }
                    case "dot": mk.Polyline(ViewMark.Circle(q, s / 4, 12), true, "DIMENSION", Pens.Default.Seen, color); break;
                }
            }
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                if (values[i] < 1e-6) { textAt.Add((JsonNode?)null); continue; }
                var (qa, qb) = (line[i], line[i + 1]);
                var su = Vec2.Dot(qb - qa, u) >= 0 ? u : -u;
                // Arrows: each segment's, at both its ends, pointing in.
                if (style.Arrow == "arrow")
                    foreach (var (q, inward) in new[] { (qa, su), (qb, -su) })
                        mk.Line(q, q + inward * s + n * (s * 0.3), "DIMENSION", Pens.Default.Hatch, color).Line(q, q + inward * s - n * (s * 0.3), "DIMENSION", Pens.Default.Hatch, color);
                // The value: in the style's unit (or the text written over it), reading left to right (bottom to top), above, on or below the line.
                var value = texts[i] ?? Standards.FormatDimension(values[i], style.Unit, style.Precision);
                var angle = Math.Atan2(su.Y, su.X) * 180 / Math.PI;
                var readable = -Standards.ReadableDeg(-angle);
                var up = new Vec2(-Math.Sin(readable * Math.PI / 180), Math.Cos(readable * Math.PI / 180));
                var mid = (qa + qb) * 0.5;
                var (tat, baseline) = style.Placement switch
                {
                    "centered" => (mid, "middle"),
                    "below" => (mid - up * (style.TextGap * mm + text.Height * mm), "bottom"),
                    _ => (mid + up * (style.TextGap * mm), "bottom"),
                };
                // Moved by hand (close values kept clear of one another): a leader back to its segment once it stands off it.
                if (moved[i] is { } by)
                {
                    tat += by;
                    var h = text.Height * mm;
                    var across = Vec2.Dot(by, up);
                    var along = Math.Abs(Vec2.Dot(by, su)) - (qb - qa).Length / 2;
                    if (across < -h * 0.5 || across > h * 1.5 || along > 0)
                    {
                        var foot = baseline == "middle" ? tat - up * (h * 0.6) : tat - up * (h * 0.15);
                        mk.Line(mid, foot, "DIMENSION", Pens.Default.Hatch, color);
                    }
                }
                mk.Text(new PenText(tat, value, text.Height, readable, "middle", baseline, text.Bold, text.Font), color);
                textAt.Add((JsonNode)J(tat));
            }
            mk.Snaps.AddRange(line);
            // For the grips: the points as drawn now, and where each lands on the dimension line.
            mk.Data = new JsonObject
            {
                ["points"] = new JsonArray(pts.Select(p => (JsonNode)J(p)).ToArray()),
                ["line"] = new JsonArray(line.Select(p => (JsonNode)J(p)).ToArray()),
                // Where each segment's value stands (null: a segment of no length draws none), for its grip.
                ["texts"] = textAt,
            };
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

    static string Key(DimEndRef r) => r.Kind == "grid" ? $"{r.Element}:{r.Tag}" : r.Element;

    /// <summary>
    /// A string's segment driven to <paramref name="value"/>: what its second
    /// point holds on moves along the string, and so does what every later
    /// point holds on, so the other segments keep theirs (when the second point
    /// is free, what the first holds on moves instead, alone). The stored
    /// points follow: those that moved are kept where they went.
    /// </summary>
    public static void Drive(ICommandContext ctx, Entity dim, ViewDrawing d, string viewKind, double value, int segment = 0)
    {
        var m = ctx.Model;
        var stations = Stations(dim);
        if (segment < 0 || segment + 1 >= stations.Count) throw new CommandException("The dimension has no such segment.");
        var (pts, orphan) = Points(m, viewKind, dim, d);
        if (orphan) throw new CommandException("The dimension lost its element: free it first.");
        if (Along(dim, pts) is not { } u) throw new CommandException("The dimension measures nothing.");
        var signed = Vec2.Dot(pts[segment + 1] - pts[segment], u);
        var delta = value - Math.Abs(signed);
        if (Math.Abs(delta) < 1e-9) return;
        var dir = u * Math.Sign(signed == 0 ? 1 : signed);
        var refA = stations[segment].Ref;
        var refB = stations[segment + 1].Ref;
        // The points that go: the second and every later one that holds on something; else the first alone.
        var movers = new List<DimEndRef>();
        var push = dir * delta;
        if (refB != null) { for (var i = segment + 1; i < stations.Count; i++) if (stations[i].Ref is { } r) movers.Add(r); }
        else if (refA != null) { movers.Add(refA); push = dir * -delta; }
        else throw new CommandException("Neither end holds on an element: there is nothing to move.");
        var anchor = refB != null ? refA : null;
        var moved = new HashSet<string>();
        foreach (var r in movers)
        {
            if (anchor != null && Key(anchor) == Key(r)) throw new CommandException("Both ends hold on the same element: it cannot move against itself.");
            if (!moved.Add(Key(r))) continue;
            Move(ctx, r, push);
        }
        // The stored points as drawn now, those whose element moved where they went: they stay true for the grips.
        var next = stations.Select((s, i) => (s.Ref is { } r && moved.Contains(Key(r)) ? pts[i] + push : pts[i], s.Ref)).ToList();
        if (ctx.Model.Get(dim.Id) is { } now)
        {
            var attrs = now.CloneAttrs();
            Write(attrs, next);
            ctx.Replace(now.With(attrs: attrs));
        }
    }

    static void Move(ICommandContext ctx, DimEndRef r, Vec2 by)
    {
        if (r.Kind == "grid")
        {
            var grid = ctx.Model.Get(r.Element)!;
            var attrs = grid.CloneAttrs();
            foreach (var ax in (attrs["axes"] as JsonArray)?.OfType<JsonObject>() ?? [])
                if ((string?)ax["tag"] == r.Tag) { ax["a"] = new JsonArray(P(ax["a"]).X + by.X, P(ax["a"]).Y + by.Y); ax["b"] = new JsonArray(P(ax["b"]).X + by.X, P(ax["b"]).Y + by.Y); }
            ctx.Replace(grid.With(attrs: attrs));
            return;
        }
        var e = ctx.Model.Get(r.Element) ?? throw new CommandException("The dimension's element is gone.");
        Placement.Transform(ctx, e, Placement2D.Move(by), new HashSet<string> { e.Id });
    }
}
