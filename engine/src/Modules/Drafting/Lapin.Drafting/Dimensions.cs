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
/// <item>"edge": an edge an element's cut draws, or (an element the plan does not cut: a stair, a beam overhead, a floor,
/// a piece of furniture, a railing, a Studio solid seen below; forum 102) a line it draws or its outline: the point and the
/// edge's direction kept in the element's own frame (its axis, its placement and rotation, its outline's first side, its
/// path's first piece), so the end follows a move, a turn, a thicker wall, a wider stair; elements without a frame keep
/// the point from their middle (the old way). <see cref="FrameRev"/> 2: the rotation read in degrees, as every element
/// keeps it (older ends read it in radians, brought up to date as a project opens: <see cref="DimensionFrameUpgrade"/>);</item>
/// <item>"grid": a grid axis (by its tag), how far along it;</item>
/// <item>"axis": a wall's (or beam's) centre line, how far along it;</item>
/// <item>"opening": a door's, window's or bare opening's place in its wall (forum topic 78): <c>local</c> from its
/// centre in the wall's frame (along, across), so the point goes where the opening is slid; with <c>localDir</c> a jamb,
/// taken onto the wall's jamb face as the plan cuts it (an opening made wider moves it).</item>
/// </list>
/// </summary>
public sealed record DimEndRef(string Kind, string Element, Vec2 Offset, Vec2 Dir, Vec2? Local = null, Vec2? LocalDir = null, string? Tag = null, double S = 0, double? S2 = null, double? Len = null, int FrameRev = 0)
{
    /// <summary>The frame an edge's local point is kept in now (<see cref="Dimensions.Frame(Entity?)"/>): the rotation in degrees, outlines and paths too.</summary>
    public const int CurrentFrame = 2;

    static JsonArray J(Vec2 p) => new(p.X, p.Y);
    static Vec2? V(JsonNode? n) => Args.TryPoint(n);

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["kind"] = Kind };
        switch (Kind)
        {
            case "grid": o["grid"] = Element; o["tag"] = Tag; o["s"] = S; if (S2 is { } g2) o["s2"] = g2; break;
            case "axis": o["element"] = Element; o["s"] = S; if (S2 is { } a2) o["s2"] = a2; break;
            case "opening":
                o["element"] = Element; o["local"] = J(Local ?? default);
                if (LocalDir is { } jd) o["localDir"] = J(jd);
                break;
            default:
                o["element"] = Element; o["offset"] = J(Offset); o["dir"] = J(Dir);
                if (Local is { } l && LocalDir is { } ld)
                {
                    o["local"] = J(l); o["localDir"] = J(ld);
                    if (FrameRev > 0) o["frame"] = FrameRev;
                }
                if (Len is { } len) o["len"] = len;
                break;
        }
        return o;
    }

    public static DimEndRef? From(JsonNode? n)
    {
        if (n is not JsonObject o) return null;
        switch ((string?)o["kind"] ?? "edge")
        {
            case "grid" when (string?)o["grid"] is { } g && (string?)o["tag"] is { } t: return new("grid", g, default, default, Tag: t, S: Json.Number(o["s"]) ?? 0, S2: Json.Number(o["s2"]));
            case "axis" when (string?)o["element"] is { } e: return new("axis", e, default, default, S: Json.Number(o["s"]) ?? 0, S2: Json.Number(o["s2"]));
            case "opening" when (string?)o["element"] is { Length: > 0 } e && V(o["local"]) is { } l: return new("opening", e, default, default, l, V(o["localDir"]));
            case "edge" when (string?)o["element"] is { Length: > 0 } e && V(o["offset"]) is { } off && V(o["dir"]) is { } d:
                return new("edge", e, off, d, V(o["local"]), V(o["localDir"]), Len: Json.Number(o["len"]), FrameRev: (int)(Json.Number(o["frame"]) ?? 0));
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

    /// <summary>
    /// What a drawing gives each element to hold a dimension's point on: the edges of its cut areas, or, when the view does
    /// not cut it, the lines it draws and its outline (a stair's, drawn by its marks; forum 102). Worked out once a drawing.
    /// </summary>
    sealed class Held
    {
        public readonly Dictionary<string, List<(Vec2 A, Vec2 B)>> Edges = [];
        public readonly Dictionary<string, (Vec2 Min, Vec2 Max)> Boxes = [];
        /// <summary>The elements held by their lines (none cut), each line once.</summary>
        public readonly List<(string Element, Vec2 A, Vec2 B)> Lines = [];

        public Held(ViewDrawing d)
        {
            foreach (var p in d.Cuts)
                foreach (var l in p.Loops)
                    for (var i = 0; i < l.Length; i++) Add(p.Element, l[i], l[(i + 1) % l.Length]);
            var cut = Edges.Keys.ToHashSet();
            foreach (var l in d.Lines.Concat(d.Outlines ?? []))
            {
                if (l.Element is not { Length: > 0 } el || cut.Contains(el) || (l.B - l.A).Length < 1e-6) continue;
                Add(el, l.A, l.B);
                Lines.Add((el, l.A, l.B));
            }
        }

        void Add(string element, Vec2 a, Vec2 b)
        {
            if (!Edges.TryGetValue(element, out var list)) Edges[element] = list = [];
            list.Add((a, b));
            var box = Boxes.TryGetValue(element, out var bx) ? bx : (a, a);
            Boxes[element] = (new Vec2(Math.Min(box.Item1.X, Math.Min(a.X, b.X)), Math.Min(box.Item1.Y, Math.Min(a.Y, b.Y))),
                new Vec2(Math.Max(box.Item2.X, Math.Max(a.X, b.X)), Math.Max(box.Item2.Y, Math.Max(a.Y, b.Y))));
        }
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ViewDrawing, Held> HeldBy = new();

    static Held HeldOf(ViewDrawing d) => HeldBy.GetValue(d, x => new Held(x));

    static IEnumerable<(Vec2 A, Vec2 B)> Edges(ViewDrawing d, string element) =>
        HeldOf(d).Edges.TryGetValue(element, out var list) ? list : [];

    /// <summary>The middle of what an element draws: its cut areas' box centre (not cut: its lines' and outline's).</summary>
    static Vec2? Middle(ViewDrawing d, string element) =>
        HeldOf(d).Boxes.TryGetValue(element, out var b) ? (b.Min + b.Max) * 0.5 : null;

    static (double Dist, Vec2 Foot) Foot(Vec2 p, Vec2 a, Vec2 b)
    {
        var d = b - a;
        var t = Math.Clamp(Vec2.Dot(p - a, d) / Math.Max(Vec2.Dot(d, d), 1e-18), 0, 1);
        var f = a + d * t;
        return ((p - f).Length, f);
    }

    static Vec2 P(JsonNode? n) => Args.PointOf(n);

    /// <summary>
    /// An element's frame in plan: its axis (start → end), its placement and rotation (degrees, as every element keeps it), its
    /// outline's first side (a floor, a ceiling) or its path's first piece (a railing); null when it has none. Each goes with
    /// the element when it is moved or turned (<see cref="Placement.Carry"/>).
    /// </summary>
    public static (Vec2 O, Vec2 U)? Frame(Entity? e) => Frame(e, DimEndRef.CurrentFrame);

    /// <summary>The frame as an end of <paramref name="rev"/> keeps its point in: before 2, the rotation read in radians and no outlines nor paths.</summary>
    public static (Vec2 O, Vec2 U)? Frame(Entity? e, int rev)
    {
        if (e == null) return null;
        if (e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray)
        {
            var (a, b) = (P(e.Attrs["start"]), P(e.Attrs["end"]));
            return (b - a).Length < 1e-9 ? null : (a, (b - a).Normalized());
        }
        if (e.Attrs["at"] is JsonArray)
        {
            var r = rev >= DimEndRef.CurrentFrame ? e.Num("rotation") * Math.PI / 180 : e.Num("rotation");
            return (P(e.Attrs["at"]), new Vec2(Math.Cos(r), Math.Sin(r)));
        }
        if (rev < DimEndRef.CurrentFrame) return null;
        foreach (var key in new[] { "contour", "points" })
            if (e.Attrs[key] is JsonArray pts && pts.OfType<JsonArray>().Select(P).ToList() is { Count: >= 2 } list)
                for (var i = 1; i < list.Count; i++)
                    if ((list[i] - list[0]).Length > 1e-6) return (list[0], (list[i] - list[0]).Normalized());
        return null;
    }

    /// <summary>An element's axis length (m); null when it has no axis.</summary>
    static double? LengthOf(Entity? e) => e?.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray ? (P(e.Attrs["end"]) - P(e.Attrs["start"])).Length : null;

    static Vec2 ToLocal((Vec2 O, Vec2 U) f, Vec2 p) { var v = p - f.O; return new Vec2(Vec2.Dot(v, f.U), Vec2.Dot(v, f.U.Perp)); }
    static Vec2 FromLocal((Vec2 O, Vec2 U) f, Vec2 l) => f.O + f.U * l.X + f.U.Perp * l.Y;
    static Vec2 DirToLocal((Vec2 O, Vec2 U) f, Vec2 d) => new(Vec2.Dot(d, f.U), Vec2.Dot(d, f.U.Perp));
    static Vec2 DirFromLocal((Vec2 O, Vec2 U) f, Vec2 l) => f.U * l.X + f.U.Perp * l.Y;

    /// <summary>
    /// An edge's end kept in an older frame (the rotation read in radians) kept in today's instead, the point and direction
    /// where they are now: the same end, so it turns with its element from now on (forum 102). Any other end as it was.
    /// </summary>
    public static DimEndRef InCurrentFrame(Entity? element, DimEndRef r)
    {
        if (r.Kind != "edge" || r.FrameRev >= DimEndRef.CurrentFrame || r.Local is not { } l || r.LocalDir is not { } ld) return r;
        if (Frame(element, r.FrameRev) is not { } was || Frame(element) is not { } now) return r with { FrameRev = DimEndRef.CurrentFrame };
        return r with { Local = ToLocal(now, FromLocal(was, l)), LocalDir = DirToLocal(now, DirFromLocal(was, ld)), FrameRev = DimEndRef.CurrentFrame };
    }

    /// <summary>
    /// What an end placed at <paramref name="p"/> holds on: a cut edge within
    /// <paramref name="tolerance"/>, else (in a plan) a grid axis, else a wall's
    /// or beam's centre line; null: a free end. With <paramref name="along"/>
    /// (the direction the string measures), of the edges as near, the one most
    /// across it: at a corner, the face of the wall that closes the side, which
    /// moves the point when that wall moves, not the end of the wall measured.
    /// </summary>
    public static DimEndRef? Associate(IModelView m, string viewKind, ViewDrawing d, Vec2 p, double tolerance, Vec2? along = null)
    {
        (DPolygon Poly, Vec2 A, Vec2 B, double Dist, double Across)? best = null;
        foreach (var poly in d.Cuts)
            foreach (var l in poly.Loops)
                for (var i = 0; i < l.Length; i++)
                {
                    var (a, b) = (l[i], l[(i + 1) % l.Length]);
                    if ((b - a).Length < 1e-6) continue;
                    var (dist, _) = Foot(p, a, b);
                    if (dist > tolerance) continue;
                    var across = along is { } u ? Math.Abs(Vec2.Cross((b - a).Normalized(), u)) : 0;
                    if (best == null || dist < best.Value.Dist - 1e-6 || (dist <= best.Value.Dist + 1e-6 && across > best.Value.Across + 1e-9)) best = (poly, a, b, dist, across);
                }
        if (best is { } hit && Middle(d, hit.Poly.Element) is { })
            return HoldOn(m, viewKind, d, hit.Poly.Element, p, hit.B - hit.A);
        if (viewKind != "plan") return null;
        foreach (var grid in m.OfClass(IfcClass.Grid))
            foreach (var ax in Grids.Axes(grid))
                if (Foot(p, ax.A, ax.B).Dist <= tolerance)
                    return new DimEndRef("grid", grid.Id, default, default, Tag: ax.Tag, S: Vec2.Dot(p - ax.A, ax.Dir), S2: Vec2.Dot(ax.B - p, ax.Dir));
        // An element the plan does not cut (a stair, a beam overhead, a floor, furniture, a railing, a solid seen below; forum 102):
        // a line it draws, or its outline, held as a cut edge is.
        (string Element, Vec2 A, Vec2 B, double Dist, double Across)? line = null;
        foreach (var (el, a, b) in HeldOf(d).Lines)
        {
            var (dist, _) = Foot(p, a, b);
            if (dist > tolerance || m.Get(el) is not { } owner || owner.Class is IfcClass.Annotation or IfcClass.Grid) continue;
            var across = along is { } u ? Math.Abs(Vec2.Cross((b - a).Normalized(), u)) : 0;
            if (line == null || dist < line.Value.Dist - 1e-6 || (dist <= line.Value.Dist + 1e-6 && across > line.Value.Across + 1e-9)) line = (el, a, b, dist, across);
        }
        if (line is { } seen) return HoldOn(m, viewKind, d, seen.Element, p, seen.B - seen.A);
        foreach (var e in m.Entities.Where(e => e.Class is IfcClass.Wall or IfcClass.Beam && e.Attrs["start"] is JsonArray))
            if (Frame(e) is { } f && Foot(p, P(e.Attrs["start"]), P(e.Attrs["end"])).Dist <= tolerance)
                return new DimEndRef("axis", e.Id, default, default, S: Vec2.Dot(p - f.O, f.U), S2: Vec2.Dot(P(e.Attrs["end"]) - p, f.U));
        return null;
    }

    /// <summary>The end now, or null when what it holds on is gone.</summary>
    public static Vec2? Resolve(IModelView m, string viewKind, ViewDrawing d, DimEndRef r, Vec2? hint = null)
    {
        switch (r.Kind)
        {
            case "grid":
            {
                if (m.Get(r.Element) is not { Class: IfcClass.Grid } grid || Grids.Axes(grid).FirstOrDefault(a => a.Tag == r.Tag) is not { } ax) return null;
                return OnLine(ax.A, ax.B, r, hint);
            }
            case "axis":
                return m.Get(r.Element) is { } w && Frame(w) is { } f ? OnLine(f.O, P(w.Attrs["end"]), r, hint) : null;
            case "opening":
                return OnOpening(m, d, r);
        }
        Vec2 q, dir;
        if (viewKind == "plan" && r.Local is { } local && r.LocalDir is { } ld && Frame(m.Get(r.Element), r.FrameRev) is { } frame && Middle(d, r.Element) != null)
        {
            (q, dir) = (FromLocal(frame, local), DirFromLocal(frame, ld).Normalized());
            // A face along an element made longer or shorter (an end dragged along it): the point stays where it was measured (report #126).
            if (Math.Abs(ld.Normalized().Y) < Parallel && r.Len is { } len0 && hint is { } h && LengthOf(m.Get(r.Element)) is { } len1 && Math.Abs(len1 - len0) > 1e-6) q = h;
        }
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

    /// <summary>The wall an opening is in (a door or window: the wall of the opening it fills).</summary>
    public static Entity? HostOf(IModelView m, string id) =>
        m.RelatingOf(id, IfcClass.RelFillsElement) is { } opening ? m.RelatingOf(opening.Id, IfcClass.RelVoidsElement) : m.RelatingOf(id, IfcClass.RelVoidsElement);

    /// <summary>The opening that holds a place in its wall (<c>along</c>): the opening itself, or the one a door or window fills.</summary>
    static Entity? OpeningOf(IModelView m, string id) =>
        m.Get(id) is not { } e ? null : e.NumOrNull("along") != null ? e : m.RelatingOf(id, IfcClass.RelFillsElement);

    /// <summary>
    /// A point held on an opening (<paramref name="p"/> on the plan; <paramref name="jamb"/>: the jamb's face direction, null: its centre):
    /// where it lies from the opening's centre in its wall's frame; null when the opening has no wall with an axis.
    /// </summary>
    public static DimEndRef? HoldOnOpening(IModelView m, string openingId, Vec2 p, Vec2? jamb = null)
    {
        if (OpeningOf(m, openingId) is not { } o || HostOf(m, o.Id) is not { } wall || Frame(wall) is not { } f) return null;
        var centre = f.O + f.U * o.Num("along");
        var l = ToLocal((centre, f.U), p);
        return new DimEndRef("opening", o.Id, default, default, l, jamb is { } j ? DirToLocal(f, j.Normalized()) : null);
    }

    static Vec2? OnOpening(IModelView m, ViewDrawing d, DimEndRef r)
    {
        if (OpeningOf(m, r.Element) is not { } o || HostOf(m, o.Id) is not { } wall || Frame(wall) is not { } f || r.Local is not { } l) return null;
        var q = FromLocal((f.O + f.U * o.Num("along"), f.U), l);
        if (r.LocalDir is not { } ld) return q;
        // A jamb: onto the wall's jamb face nearest, as the plan cuts it (an opening made wider or narrower).
        var dir = DirFromLocal(f, ld).Normalized();
        (double Dist, Vec2 P)? best = null;
        foreach (var (a, b) in Edges(d, wall.Id))
        {
            var e = b - a;
            if (e.Length < 1e-9 || Math.Abs(Vec2.Cross(e.Normalized(), dir)) > Parallel) continue;
            var u = e.Normalized();
            var on = a + u * Vec2.Dot(q - a, u);
            var dist = (q - on).Length;
            if (dist <= OpeningHeights.Reach * 5 && (best == null || dist < best.Value.Dist)) best = (dist, on);
        }
        return best?.P ?? q;
    }

    /// <summary>
    /// A point held on a face of <paramref name="element"/> that runs along <paramref name="dir"/> (an edge of its cut in that
    /// direction), the point itself anywhere on that face's line: in a plan, kept in the element's frame, so it follows it.
    /// </summary>
    public static DimEndRef? HoldOn(IModelView m, string viewKind, ViewDrawing d, string element, Vec2 p, Vec2 dir)
    {
        if (Middle(d, element) is not { } mid) return null;
        dir = dir.Normalized();
        if (viewKind == "plan" && Frame(m.Get(element)) is { } f)
            return new DimEndRef("edge", element, p - mid, dir, ToLocal(f, p), DirToLocal(f, dir), Len: LengthOf(m.Get(element)), FrameRev: DimEndRef.CurrentFrame);
        return new DimEndRef("edge", element, p - mid, dir);
    }

    /// <summary>A point held on a wall's (or beam's) centre line: how far along it it lies.</summary>
    public static DimEndRef? HoldOnAxis(IModelView m, string element, Vec2 p) =>
        m.Get(element) is { } e && Frame(e) is { } f ? new DimEndRef("axis", e.Id, default, default, S: Vec2.Dot(p - f.O, f.U), S2: Vec2.Dot(P(e.Attrs["end"]) - p, f.U)) : null;

    /// <summary>A point held on a grid axis: how far along it it lies.</summary>
    public static DimEndRef HoldOnGrid(string gridId, Axis ax, Vec2 p) =>
        new("grid", gridId, default, default, Tag: ax.Tag, S: Vec2.Dot(p - ax.A, ax.Dir), S2: Vec2.Dot(ax.B - p, ax.Dir));

    /// <summary>
    /// A point held on a line (a grid axis, a wall's centre line), found by its distance from the start while the line keeps
    /// its length (moved, turned or mirrored as a whole, it takes the point with it), and by the foot of the point as it was
    /// drawn when the line is made longer or shorter: an end dragged along the line leaves the dimension where it was measured,
    /// not turned about the other point (report #126). Without the length (an older dimension) a grid axis keeps the foot.
    /// </summary>
    static Vec2 OnLine(Vec2 a, Vec2 b, DimEndRef r, Vec2? hint)
    {
        var dir = (b - a).Length < 1e-9 ? new Vec2(1, 0) : (b - a).Normalized();
        var viaStart = a + dir * r.S;
        var foot = hint is { } h ? a + dir * Vec2.Dot(h - a, dir) : viaStart;
        // A line the length it was moved or turned as a whole: the point goes with it. Made longer or shorter: it stays where it was.
        if (r.S2 is { } s2) return Math.Abs((b - a).Length - (r.S + s2)) < 1e-6 ? viaStart : foot;
        return r.Kind == "grid" ? foot : viaStart;
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
            if (Resolve(m, viewKind, d, r, p) is { } q) o.Add(q);
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
        // The walls' openings, read once a drawing when a dimension asks for their heights.
        List<WallOpening>? openings = null;
        // Every string as drawn now; what stays where its style puts it goes into the view's room first, so the values
        // that must move keep clear of every dimension of the view, those drawn after them too (forum topic 78).
        var strings = new List<(Entity Dim, List<Vec2> Pts, bool Orphan, Layout Lay)>();
        foreach (var dim in c.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "DIMENSION" && x.Str("viewId") == c.View.Id && !DimensionKinds.IsKind(x)))
        {
            var (pts, orphan) = Points(c.Model, c.Kind, dim, c.Drawing);
            if (pts.Count < 2 || Lay(dim, pts) is not { } lay) continue;
            strings.Add((dim, pts, orphan, lay));
        }
        var room = new TextRoom();
        foreach (var s in strings)
            Obstacles(c, s.Dim.Id, s.Pts, s.Lay, ParamsOf(s.Dim), TextsOf(s.Dim.Attrs, s.Pts.Count - 1), OffsetsOf(s.Dim.Attrs, s.Pts.Count - 1), room, ref openings);
        foreach (var s in strings)
            if (Draw(c, s.Dim.Id, s.Pts, s.Lay, ParamsOf(s.Dim), TextsOf(s.Dim.Attrs, s.Pts.Count - 1), OffsetsOf(s.Dim.Attrs, s.Pts.Count - 1), s.Orphan, ref openings, room) is { } mk) yield return mk;
    }

    /// <summary>One segment's value as its style places it: the text, its angle, its place and baseline, its opening's height rows.</summary>
    sealed record SegValue(string Text, double Readable, Vec2 Up, Vec2 Su, Vec2 Mid, Vec2 At, string Baseline, List<string>? Rows, double RowsTop);

    /// <summary>Each segment's value (null: a segment of no length) as the style puts it, before anything is moved.</summary>
    static List<SegValue?> SegValues(ViewContext c, IReadOnlyList<Vec2> pts, Layout lay, DimStyle style, TextStyle text, IReadOnlyList<string?> texts, ref List<WallOpening>? openings)
    {
        var (u, line) = (lay.U, lay.Line);
        var mm = c.Mm;
        var o = new List<SegValue?>();
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            if (Math.Abs(Vec2.Dot(line[i + 1] - line[i], u)) < 1e-6) { o.Add(null); continue; }
            var (qa, qb) = (line[i], line[i + 1]);
            var su = Vec2.Dot(qb - qa, u) >= 0 ? u : -u;
            var value = texts[i] ?? Standards.FormatDimension(Math.Abs(Vec2.Dot(qb - qa, u)), style.Unit, style.Precision, style.SuppressesZeros);
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
            List<string>? rows = null;
            double top = 0;
            // A segment across a door, a window or a bare opening: its height under the line (decision 338).
            if (style.OpeningHeight != "off" && texts[i] == null && OpeningHeights.Spanned(c, openings ??= OpeningHeights.Of(c), pts[i], pts[i + 1], u) is { } opening)
            {
                var h = text.Height * mm;
                top = style.Placement switch { "centered" => h * 0.5 + style.TextGap * mm, "below" => style.TextGap * mm + h * OpeningHeights.Pitch, _ => style.TextGap * mm };
                rows = OpeningHeights.Rows(style.OpeningHeight, opening, style.Unit, style.Precision, style.SuppressesZeros, style.OpeningText);
            }
            o.Add(new SegValue(value, readable, up, su, mid, tat, baseline, rows, top));
        }
        return o;
    }

    /// <summary>The segments' values as the placement reads them: where each stands, how wide, whether it fits, whether the user moved it.</summary>
    static List<DimensionTextPlacement.Seg> Segs(IReadOnlyList<SegValue?> values, Layout lay, TextStyle text, IReadOnlyList<Vec2?> moved, double mm)
    {
        var h = text.Height * mm;
        var o = new List<DimensionTextPlacement.Seg>();
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } v) continue;
            var r = new Vec2(v.Up.Y, -v.Up.X);
            var w = BuildupText.Measure(v.Text, text.Height, text.Bold) * mm;
            var length = (lay.Line[i + 1] - lay.Line[i]).Length;
            var centre = v.Baseline == "middle" ? v.At : v.At + v.Up * (h / 2);
            o.Add(new(i, v.Mid, length, r, v.Up, centre, w, h, DimensionTextPlacement.Fits(w, length, mm), moved[i] != null));
        }
        return o;
    }

    /// <summary>A string's opening-height rows' rectangles (each row as wide as its text), under the value moved by <paramref name="by"/>.</summary>
    static IEnumerable<TextRoom.Box> RowBoxes(string owner, SegValue v, TextStyle text, double mm, Vec2 by)
    {
        if (v.Rows == null) yield break;
        var h = text.Height * mm;
        var r = new Vec2(v.Up.Y, -v.Up.X);
        for (var k = 0; k < v.Rows.Count; k++)
        {
            var at = v.Mid - v.Up * (v.RowsTop + h + k * h * OpeningHeights.Pitch) + by;
            yield return new TextRoom.Box(owner, TextRoom.Kind.Text, at + v.Up * (h / 2), r, v.Up, BuildupText.Measure(v.Rows[k], text.Height, text.Bold) * mm / 2, h / 2);
        }
    }

    /// <summary>
    /// Where the leader of a value moved by hand by <paramref name="by"/> (standing at <paramref name="at"/>) ends: its box and
    /// its opening-height rows, a gap short of them (forum topic 227); null when its segment's middle lies within them.
    /// </summary>
    static Vec2? LeaderEnd(string owner, SegValue v, string value, Vec2 at, TextStyle text, double mm, Vec2 by)
    {
        var h = text.Height * mm;
        var r = new Vec2(v.Up.Y, -v.Up.X);
        var centre = v.Baseline == "middle" ? at : at + v.Up * (h / 2);
        var boxes = new List<TextRoom.Box> { new(owner, TextRoom.Kind.Text, centre, r, v.Up, BuildupText.Measure(value, text.Height, text.Bold) * mm / 2, h / 2) };
        boxes.AddRange(RowBoxes(owner, v, text, mm, by));
        return DimensionTextPlacement.LeaderEnd(boxes, v.Mid, DimensionTextPlacement.LeaderGap * mm);
    }

    /// <summary>
    /// What a string puts in the view's <paramref name="room"/> before any value moves: its dimension line, its extension
    /// lines, the values that stay where they are (they fit, or were moved by hand) and its opening heights.
    /// </summary>
    public static void Obstacles(ViewContext c, string owner, IReadOnlyList<Vec2> pts, Layout lay, JsonObject styleParams, IReadOnlyList<string?>? texts, IReadOnlyList<Vec2?>? moved, TextRoom room, ref List<WallOpening>? openings)
    {
        var (u, n, line, offs) = (lay.U, lay.N, lay.Line, lay.Off);
        texts ??= Enumerable.Repeat<string?>(null, pts.Count - 1).ToList();
        moved ??= Enumerable.Repeat<Vec2?>(null, pts.Count - 1).ToList();
        var style = Standards.ResolveDim(c.Book, styleParams);
        var text = Standards.ResolveText(c.Book, styleParams);
        var mm = c.Mm;
        var ts = line.Select(q => Vec2.Dot(q - line[0], u)).ToList();
        room.Boxes.Add(TextRoom.Box.Segment(owner, TextRoom.Kind.Line, line[0] + u * ts.Min(), line[0] + u * ts.Max()));
        for (var i = 0; i < pts.Count; i++)
        {
            if (style.SuppressesExtension(i, pts.Count)) continue;
            var side = offs[i] >= 0 ? 1 : -1;
            if (style.Extension(pts[i], line[i], n * side, Math.Abs(offs[i]), mm) is { } ext)
                room.Boxes.Add(TextRoom.Box.Segment(owner, TextRoom.Kind.Extension, ext.A, ext.B));
        }
        var values = SegValues(c, pts, lay, style, text, texts, ref openings);
        foreach (var s in Segs(values, lay, text, moved, mm))
        {
            if (s.Fits || s.Moved || !style.PlacesTexts) room.Boxes.Add(DimensionTextPlacement.Box(owner, s, moved[s.Index] ?? default));
            room.Boxes.AddRange(RowBoxes(owner, values[s.Index]!, text, mm, moved[s.Index] ?? default));
        }
    }

    /// <summary>
    /// A string of dimensions drawn: its line through <paramref name="lay"/>'s points, the extension lines, ticks (or dots, arrows)
    /// and values in the style <paramref name="styleParams"/> names (the drafting standards' otherwise), each value the user's
    /// text when given and moved by its offset; null when every segment has no length. What a dimension of the user's draws, and
    /// what a part's derived dimensions draw (decision 581), so both look alike. Values that do not fit move clear of
    /// <paramref name="room"/> (the view's, filled by <see cref="Obstacles"/> for every string first), else of the string's own.
    /// </summary>
    public static ViewMark? Draw(ViewContext c, string owner, IReadOnlyList<Vec2> pts, Layout lay, JsonObject styleParams, IReadOnlyList<string?>? texts, IReadOnlyList<Vec2?>? moved, bool orphan, ref List<WallOpening>? openings, TextRoom? room = null)
    {
        var (u, n, line, offs) = (lay.U, lay.N, lay.Line, lay.Off);
        var values = Enumerable.Range(0, pts.Count - 1).Select(i => Math.Abs(Vec2.Dot(line[i + 1] - line[i], u))).ToList();
        if (values.All(v => v < 1e-6)) return null;
        texts ??= Enumerable.Repeat<string?>(null, pts.Count - 1).ToList();
        moved ??= Enumerable.Repeat<Vec2?>(null, pts.Count - 1).ToList();
        var style = Standards.ResolveDim(c.Book, styleParams);
        var text = Standards.ResolveText(c.Book, styleParams);
        var textAt = new JsonArray();
        var color = orphan ? "#c0392b" : style.Color ?? "#000000";
        // The dimension line's and the extension lines' own pens, colours and line types (issue #66); unset: as the style's colour, 0.13 mm, continuous.
        var lineColor = orphan ? color : style.DimLineColor ?? color;
        var extColor = orphan ? color : style.ExtColor ?? color;
        // The values' own colour (forum topic 173): the dimension's, else its style's text colour, else its text style's, else the style's.
        var textColor = orphan ? color : Standards.DimTextColor(styleParams, style, text) ?? color;
        var (lineW, extW) = (style.DimLineWeightOr(Pens.Default.Hatch), style.ExtWeightOr(Pens.Default.Hatch));
        var (lineDash, extDash) = (Standards.DashOf(c.Book, style.DimLineType), Standards.DashOf(c.Book, style.ExtLineType));
        var mm = c.Mm;
        var mk = new ViewMark(owner);
        // One dimension line, from the first point to the last as they lie along it.
        var ts = line.Select(q => Vec2.Dot(q - line[0], u)).ToList();
        mk.Line(line[0] + u * ts.Min(), line[0] + u * ts.Max(), "DIMENSION", lineW, lineColor, lineDash);
        // Each point once: its extension line (a gap from the measured point, or a fixed length back from the dimension line;
        // past it by the overshoot; none where the style or the dimension leaves it out), its tick or dot.
        for (var i = 0; i < pts.Count; i++)
        {
            var (p, q, off) = (pts[i], line[i], offs[i]);
            var side = off >= 0 ? 1 : -1;
            if (!style.SuppressesExtension(i, pts.Count) && style.Extension(p, q, n * side, Math.Abs(off), mm) is { } ext) mk.Line(ext.A, ext.B, "DIMENSION", extW, extColor, extDash);
            if (style.Arrow != "arrow") DimArrow.Draw(mk, style, mm, q, u, n, lineW, lineColor);
        }
        // The values as the style places them (in the style's unit, or the text written over it, reading left to right or
        // bottom to top, above, on or below the line), then those that do not fit moved clear (forum topic 78): of the
        // view's other dimensions when the view's room is given, else of this string's own.
        var vals = SegValues(c, pts, lay, style, text, texts, ref openings);
        var auto = new Dictionary<int, Vec2>();
        var segs = style.PlacesTexts ? Segs(vals, lay, text, moved, mm) : [];
        if (segs.Any(x => !x.Fits && !x.Moved))
        {
            if (room == null) { room = new TextRoom(); Obstacles(c, owner, pts, lay, styleParams, texts, moved, room, ref openings); }
            auto = DimensionTextPlacement.Place(owner, segs, room, style.Placement, style.TextGap * mm, mm);
        }
        var autoAt = new JsonArray();
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            if (values[i] < 1e-6 || vals[i] is not { } v) { textAt.Add((JsonNode?)null); autoAt.Add((JsonNode?)null); continue; }
            var (qa, qb) = (line[i], line[i + 1]);
            var su = v.Su;
            // Arrows: each segment's, at both its ends, pointing in.
            if (style.Arrow == "arrow")
                foreach (var (q, inward) in new[] { (qa, su), (qb, -su) })
                    DimArrow.Draw(mk, style, mm, q, inward, n, lineW, lineColor);
            var (value, readable, up, mid, tat, baseline) = (v.Text, v.Readable, v.Up, v.Mid, v.At, v.Baseline);
            // Moved by hand (close values kept clear of one another): a leader back to its segment once it stands off it.
            if (moved[i] is { } by)
            {
                tat += by;
                var h = text.Height * mm;
                var across = Vec2.Dot(by, up);
                var along = Math.Abs(Vec2.Dot(by, su)) - (qb - qa).Length / 2;
                // It ends at the value's box (with its opening-height rows, which move with it) a small gap short of it,
                // never across the digits (forum topic 227).
                if ((across < -h * 0.5 || across > h * 1.5 || along > 0) && LeaderEnd(owner, v, value, tat, text, mm, by) is { } foot)
                    mk.Line(mid, foot, "DIMENSION", Pens.Default.Hatch, color);
            }
            // Moved by the placement: a leader from its segment's middle to the value's nearest point once it stands further than the style's distance.
            else if (auto.TryGetValue(i, out var placed))
            {
                tat += placed;
                if (placed.Length > (style.TextLeader ?? Standards.DefaultTextLeader) * mm + 1e-9)
                {
                    var seg = segs.First(x => x.Index == i);
                    if (DimensionTextPlacement.LeaderEnd([DimensionTextPlacement.Box(owner, seg, placed)], mid, DimensionTextPlacement.LeaderGap * mm) is { } end)
                        mk.Line(mid, end, "DIMENSION", Pens.Default.Hatch, color);
                }
            }
            mk.Text(new PenText(tat, value, text.Height, readable, "middle", baseline, text.Bold, text.Font, text.Italic), textColor);
            textAt.Add((JsonNode)J(tat));
            autoAt.Add(auto.TryGetValue(i, out var a) ? (JsonNode)J(a) : null);
            // A segment across a door, a window or a bare opening: its height under the line (decision 338), moved with the value
            // by hand; a value moved by the placement leaves it where it is.
            if (v.Rows is { } rows)
            {
                var h = text.Height * mm;
                for (var k = 0; k < rows.Count; k++)
                    mk.Text(new PenText(mid - up * (v.RowsTop + h + k * h * OpeningHeights.Pitch) + (moved[i] ?? default), rows[k], text.Height, readable, "middle", "bottom", text.Bold, text.Font, text.Italic), textColor);
            }
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
        // How far the placement moved each value (null: not moved), so a value's grip dragged moves it from where it is shown.
        if (auto.Count > 0) mk.Data["textAuto"] = autoAt;
        return mk;
    }

    /// <summary>The style params an annotation names (its styles and o.* overrides).</summary>
    public static JsonObject ParamsOf(Entity e)
    {
        var p = new JsonObject();
        foreach (var k in new[] { "textStyle", "dimStyle" }) if (e.Str(k) is { } v) p[k] = v;
        if (e.Attrs["overrides"] is JsonObject o) foreach (var (k, v) in o) p["o." + k] = v?.DeepClone();
        // Its own text height (paper mm, decision 271) over the style's.
        if (e.NumOrNull("textHeight") is { } h) p["o.height"] = h;
        // Its own extension lines left out (issue #66: first, second, both, or none of them whatever its style says) over the style's.
        if (e.Str("extSuppress") is { } sup && Standards.ExtSuppressChoices.Contains(sup)) p["o.extSuppress"] = sup;
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
        if (r.Kind == "opening")
        {
            // An opening slides along its wall: what the string drives moves its place.
            var o = OpeningOf(ctx.Model, r.Element) ?? throw new CommandException("The dimension's opening is gone.");
            var f = HostOf(ctx.Model, o.Id) is { } host ? Frame(host) : null;
            if (f == null) throw new CommandException("The dimension's opening has no wall to slide along.");
            var oa = o.CloneAttrs();
            oa["along"] = o.Num("along") + Vec2.Dot(by, f.Value.U);
            ctx.Replace(o.With(attrs: oa));
            return;
        }
        var e = ctx.Model.Get(r.Element) ?? throw new CommandException("The dimension's element is gone.");
        Placement.Transform(ctx, e, Placement2D.Move(by), new HashSet<string> { e.Id });
    }
}
