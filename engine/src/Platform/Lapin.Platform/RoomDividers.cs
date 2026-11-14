using System.Text.Json.Nodes;
using Lapin.Drawing.Hatch;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Room separation lines (forum topic 80): a line, a polyline, a rectangle, an arc or a circle drawn in plan on a storey that
/// bounds rooms as a wall would, with no body — an open-plan kitchen and living room, two zones of a hall, each a room of its
/// own with its own area. IFC's class for a boundary that is not built is <c>IfcVirtualElement</c> (IFC 4.3: PredefinedType
/// BOUNDARY), which Lapin's divider is. Its shape is kept as drafting keeps one (<c>{type: line, a, b}</c>, <c>{type: polyline,
/// pts, closed}</c>, <c>{type: circle, c, r}</c>, <c>{type: arc, c, r, start, end}</c>, counter-clockwise, radians), in the
/// model's plan metres. <see cref="RoomBounds"/> reads it as a strip <see cref="HalfWidth"/> to either side of the line, a
/// little longer than the line at its ends so one drawn to a wall's face closes on it: the rooms on either side are found
/// apart, then made exact (<see cref="Exact"/>): their corners on the strip put back on the line, so their areas add up to the
/// whole. Nothing is stored on the rooms: they find their outlines again as the dividers are drawn, moved or deleted.
/// </summary>
public static class RoomDividers
{
    public const string Class = "IfcVirtualElement";
    public const string Predefined = "BOUNDARY";
    /// <summary>How far to either side of its line a divider bounds rooms (m): far above the free space's sampling, far below any measure.</summary>
    public const double HalfWidth = 1e-4;
    /// <summary>How far past its ends a divider's line reaches (m), so one drawn to a wall's face (or a hair short of it) closes on it.</summary>
    public const double Reach = 0.005;

    public static bool Is(Entity e) => e.Class == Class && e.PredefinedType == Predefined && e.Attrs["shape"] is JsonObject;

    static Vec2 P(JsonNode? n) => n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y
        ? new Vec2(x, y) : throw new CommandException("A point is [x, y].");

    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));

    /// <summary>
    /// The shape a command gives, checked and in drafting's form: <c>shape</c> as drafting keeps one, or <c>kind</c> with its
    /// <c>points</c> ([[x, y]…]): <c>line</c> two points, <c>polyline</c> two or more (<c>closed</c> to close it), <c>rectangle</c>
    /// two opposite corners, <c>arc</c> its start, its end and a point it passes through, <c>circle</c> its centre and a point on it.
    /// </summary>
    public static JsonObject ShapeOf(JsonObject a)
    {
        JsonObject shape;
        if (a["shape"] is JsonObject given) shape = (JsonObject)given.DeepClone();
        else
        {
            var pts = (a["points"] as JsonArray ?? throw new CommandException("Draw the divider: its kind and its points.")).Select(P).ToList();
            var kind = (string?)a["kind"] ?? (pts.Count == 2 ? "line" : "polyline");
            shape = kind switch
            {
                "line" when pts.Count == 2 => new JsonObject { ["type"] = "line", ["a"] = J(pts[0]), ["b"] = J(pts[1]) },
                "polyline" when pts.Count >= 2 => new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray([.. pts.Select(p => (JsonNode)J(p))]), ["closed"] = a["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c },
                "rectangle" when pts.Count == 2 => Rectangle(pts[0], pts[1]),
                "arc" when pts.Count == 3 => Arc(pts[0], pts[1], pts[2]),
                "circle" when pts.Count == 2 => new JsonObject { ["type"] = "circle", ["c"] = J(pts[0]), ["r"] = (pts[1] - pts[0]).Length },
                "line" or "polyline" or "rectangle" or "arc" or "circle" => throw new CommandException($"A {kind} takes {kind switch { "polyline" => "two points or more", "arc" => "three points: its start, its end and one it passes through", _ => "two points" }}."),
                _ => throw new CommandException($"„{kind}” is not a divider's kind (line, polyline, rectangle, arc, circle)."),
            };
        }
        // Checked: what it draws is long enough to divide.
        var paths = Paths(shape);
        if (paths.Count == 0 || paths.All(p => Length(p.Points, p.Closed) < 0.01)) throw new CommandException("The divider is too short: draw it across the room.");
        return shape;
    }

    static JsonObject Rectangle(Vec2 a, Vec2 b)
    {
        if (Math.Abs(a.X - b.X) < 1e-6 || Math.Abs(a.Y - b.Y) < 1e-6) throw new CommandException("A rectangle needs two opposite corners.");
        Vec2[] c = [a, new(b.X, a.Y), b, new(a.X, b.Y)];
        return new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray([.. c.Select(p => (JsonNode)J(p))]), ["closed"] = true };
    }

    /// <summary>The arc from <paramref name="s"/> to <paramref name="e"/> through <paramref name="through"/>, counter-clockwise as drafting keeps it.</summary>
    static JsonObject Arc(Vec2 s, Vec2 e, Vec2 through)
    {
        var d = 2 * (s.X * (e.Y - through.Y) + e.X * (through.Y - s.Y) + through.X * (s.Y - e.Y));
        if (Math.Abs(d) < 1e-12) throw new CommandException("Three points on a line make no arc: draw a line instead.");
        double Sq(Vec2 p) => p.X * p.X + p.Y * p.Y;
        var c = new Vec2((Sq(s) * (e.Y - through.Y) + Sq(e) * (through.Y - s.Y) + Sq(through) * (s.Y - e.Y)) / d,
            (Sq(s) * (through.X - e.X) + Sq(e) * (s.X - through.X) + Sq(through) * (e.X - s.X)) / d);
        double At(Vec2 p) => Math.Atan2(p.Y - c.Y, p.X - c.X);
        var (a0, a1, am) = (At(s), At(e), At(through));
        // Counter-clockwise from the start: the through point must lie on the sweep, else the arc runs the other way round.
        static double Norm(double x) { x %= 2 * Math.PI; return x < 0 ? x + 2 * Math.PI : x; }
        var ccw = Norm(am - a0) <= Norm(a1 - a0);
        var (start, end) = ccw ? (a0, a1) : (a1, a0);
        return new JsonObject { ["type"] = "arc", ["c"] = J(c), ["r"] = (s - c).Length, ["start"] = start, ["end"] = end };
    }

    /// <summary>What a shape draws: its runs of points (a polyline as it is, a circle and an arc in chords, <see cref="Chords"/>), each closed or not.</summary>
    public static List<(IReadOnlyList<Vec2> Points, bool Closed)> Paths(JsonNode? node)
    {
        if (node is not JsonObject o) return [];
        try
        {
            switch ((string?)o["type"])
            {
                case "line": return [([P(o["a"]), P(o["b"])], false)];
                case "polyline":
                {
                    var pts = (o["pts"] as JsonArray ?? []).Select(P).ToList();
                    var closed = o["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c && pts.Count >= 3;
                    return pts.Count >= 2 ? [(pts, closed)] : [];
                }
                case "circle":
                {
                    var (c, r) = (P(o["c"]), Json.Number(o["r"]) ?? 0);
                    if (r <= 1e-6) return [];
                    var n = Chords.Circle(r);
                    return [([.. Enumerable.Range(0, n).Select(i => c + new Vec2(Math.Cos(2 * Math.PI * i / n), Math.Sin(2 * Math.PI * i / n)) * r)], true)];
                }
                case "arc":
                {
                    var (c, r) = (P(o["c"]), Json.Number(o["r"]) ?? 0);
                    if (r <= 1e-6) return [];
                    var start = Json.Number(o["start"]) ?? 0;
                    var sweep = ((Json.Number(o["end"]) ?? 0) - start) % (2 * Math.PI);
                    if (sweep <= 1e-12) sweep += 2 * Math.PI;
                    var n = Chords.Count(r, sweep);
                    return [([.. Enumerable.Range(0, n + 1).Select(i => c + new Vec2(Math.Cos(start + sweep * i / n), Math.Sin(start + sweep * i / n)) * r)], false)];
                }
            }
        }
        catch (CommandException) { }
        return [];
    }

    static double Length(IReadOnlyList<Vec2> pts, bool closed)
    {
        var l = 0.0;
        for (var i = 0; i + 1 < pts.Count; i++) l += (pts[i + 1] - pts[i]).Length;
        return closed && pts.Count > 2 ? l + (pts[0] - pts[^1]).Length : l;
    }

    /// <summary>A divider's length along its line (its quantity).</summary>
    public static double LengthOf(Entity e) => Paths(e.Attrs["shape"]).Sum(p => Length(p.Points, p.Closed));

    /// <summary>The storey's dividers as straight pieces (arcs and circles in chords), each with whether its ends are an open line's.</summary>
    public static List<(Vec2 A, Vec2 B)> Pieces(IModelView m, string storeyId)
    {
        var o = new List<(Vec2, Vec2)>();
        foreach (var e in m.OfClass(Class))
        {
            if (!Is(e) || Spatial.StoreyOf(m, e.Id)?.Id != storeyId) continue;
            foreach (var (pts, closed) in Paths(e.Attrs["shape"]))
                for (var i = 0; i < (closed ? pts.Count : pts.Count - 1); i++)
                    if ((pts[(i + 1) % pts.Count] - pts[i]).Length >= 1e-6) o.Add((pts[i], pts[(i + 1) % pts.Count]));
        }
        return o;
    }

    /// <summary>
    /// A region found with the storey's dividers made exact: its corners on a divider's strip put back on the divider's line
    /// (where two pieces meet, on their meeting point; where the strip meets a wall, along the wall's face), so the rooms on
    /// either side meet on the line and their areas, perimeters and volumes add up to the room undivided; what the strip of a
    /// line ending in the room cut into the outline, there and back, taken out, and an island left with no area dropped.
    /// </summary>
    public static FreeRegion Exact(IModelView m, string storeyId, FreeRegion r)
    {
        var pieces = Pieces(m, storeyId);
        if (pieces.Count == 0) return r;
        var outer = Clean(Snap(r.Outer, pieces));
        if (outer.Count < 3) return r;
        var islands = r.Islands.Select(i => (IReadOnlyList<Vec2>)Clean(Snap(i, pieces))).Where(i => i.Count >= 3 && Math.Abs(Polygon2.SignedArea(i)) > 1e-12).ToList();
        return new FreeRegion(outer, islands);
    }

    /// <summary>Each corner on a piece's strip (within its half width and a little) moved onto the piece's line.</summary>
    static List<Vec2> Snap(IReadOnlyList<Vec2> loop, List<(Vec2 A, Vec2 B)> pieces)
    {
        const double Near = HalfWidth * 1.5;
        var o = new List<Vec2>(loop.Count);
        for (var i = 0; i < loop.Count; i++)
        {
            var v = loop[i];
            var near = new List<(Vec2 A, Vec2 D, double Off)>();
            foreach (var (a, b) in pieces)
            {
                var len = (b - a).Length;
                var d = (b - a) * (1 / len);
                var t = Vec2.Dot(v - a, d);
                var off = Math.Abs(Vec2.Cross(d, v - a));
                if (off <= Near && t >= -(Reach + 2 * HalfWidth) && t <= len + Reach + 2 * HalfWidth) near.Add((a, d, off));
            }
            if (near.Count == 0) { o.Add(v); continue; }
            // Where two pieces meet (a corner, a chord of an arc, a crossing): on their meeting point.
            Vec2? meet = null;
            for (var x = 0; x < near.Count && meet == null; x++)
                for (var y = x + 1; y < near.Count && meet == null; y++)
                    if (Meet(near[x].A, near[x].D, near[y].A, near[y].D) is { } q && (q - v).Length <= 3 * HalfWidth) meet = q;
            if (meet is { } mq) { o.Add(mq); continue; }
            var (pa, pd, _) = near.OrderBy(n => n.Off).First();
            // Else along the edge that crosses the piece (a wall's face, the strip's end) onto its line; else straight across.
            var (prev, next) = (loop[(i - 1 + loop.Count) % loop.Count], loop[(i + 1) % loop.Count]);
            var edge = new[] { (v - prev).Normalized(), (next - v).Normalized() }.OrderByDescending(e => Math.Abs(Vec2.Cross(e, pd))).First();
            o.Add(Math.Abs(Vec2.Cross(edge, pd)) > 1e-6 && Meet(v, edge, pa, pd) is { } on ? on : pa + pd * Vec2.Dot(v - pa, pd));
        }
        return o;
    }

    /// <summary>Where the line through <paramref name="a"/> along <paramref name="da"/> meets the one through <paramref name="b"/> along <paramref name="db"/>; null when they run side by side.</summary>
    static Vec2? Meet(Vec2 a, Vec2 da, Vec2 b, Vec2 db)
    {
        var c = Vec2.Cross(da, db);
        if (Math.Abs(c) < 1e-9) return null;
        return a + da * (Vec2.Cross(b - a, db) / c);
    }

    /// <summary>A loop without corners repeated, nor the spikes going out and back to the same point.</summary>
    static List<Vec2> Clean(List<Vec2> pts)
    {
        const double Same = 1e-9;
        var changed = true;
        while (changed && pts.Count >= 3)
        {
            changed = false;
            for (var i = 0; i < pts.Count && pts.Count >= 3; i++)
            {
                var n = pts.Count;
                if ((pts[i] - pts[(i + 1) % n]).Length < Same) { pts.RemoveAt((i + 1) % n); changed = true; break; }
                if ((pts[(i - 1 + n) % n] - pts[(i + 1) % n]).Length < Same) { pts.RemoveAt(i); changed = true; break; }
            }
        }
        return pts;
    }

    /// <summary>
    /// What the storey's dividers bound rooms with, as areas for <see cref="Lapin.Drawing.Hatch.FreeSpace"/>: a thin strip along
    /// each straight piece, reaching a little past an open line's ends; the pieces overlap at its corners.
    /// </summary>
    public static IEnumerable<IReadOnlyList<IReadOnlyList<Vec2>>> Areas(IModelView m, string storeyId)
    {
        foreach (var e in m.OfClass(Class))
        {
            if (!Is(e) || Spatial.StoreyOf(m, e.Id)?.Id != storeyId) continue;
            foreach (var (pts, closed) in Paths(e.Attrs["shape"]))
            {
                var n = closed ? pts.Count : pts.Count - 1;
                for (var i = 0; i < n; i++)
                {
                    var (a, b) = (pts[i], pts[(i + 1) % pts.Count]);
                    var along = b - a;
                    if (along.Length < 1e-6) continue;
                    var d = along.Normalized();
                    // An open line's ends reach a little past it, to the wall's face; at a corner the pieces overlap by their width.
                    var (from, to) = (a - d * (!closed && i == 0 ? Reach : HalfWidth), b + d * (!closed && i == n - 1 ? Reach : HalfWidth));
                    var left = d.Perp * HalfWidth;
                    yield return [new[] { from - left, to - left, to + left, from + left }];
                }
            }
        }
    }
}
