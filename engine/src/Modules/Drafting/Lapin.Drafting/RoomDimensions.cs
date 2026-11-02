using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// A room's interior dimensions on a plan, made at once (a tester's request, decision 249): one aligned dimension string
/// along each side of the room's outline, inside it, its points at the side's corners and wherever the plan's cut meets
/// that side (a wall's end or corner, a door's or window's jambs), each held on the edge it lies on, so the strings follow
/// the walls as any dimension does. Sides in line one after another are one side; sides shorter than
/// <see cref="MinSide"/> (a curved wall's chords, a pier's return) are left out. A room dimensioned again on the same
/// plan has its strings made anew (new openings counted), the old ones taken away.
/// </summary>
public static class RoomDimensions
{
    /// <summary>The shortest side dimensioned (m).</summary>
    public const double MinSide = 0.3;
    /// <summary>How far in from its side a string lies, on the paper (m): 5 mm, 0.5 m at 1:100.</summary>
    const double PaperOffset = 0.005;
    /// <summary>How close (m) a cut's corner must lie to a side to be a point of its string.</summary>
    const double OnSide = 0.002;

    /// <summary>The attribute that says which room a string dimensions.</summary>
    public const string RoomKey = "room";

    /// <summary>The rooms' strings on the plan (<paramref name="ids"/>: those rooms; null: every room of its storey); how many rooms and strings.</summary>
    public static (int Rooms, int Strings) Add(ICommandContext ctx, Entity view, IReadOnlyCollection<string>? ids, double? offset, Func<Vec2, Vec2, DimEndRef?> hold, ViewDrawing? drawing)
    {
        var m = ctx.Model;
        var storey = view.Str("storeyId");
        var rooms = ids != null
            ? ids.Select(id => m.Get(id) is { Class: IfcClass.Space } r ? r : throw new CommandException("Only rooms are dimensioned this way: select rooms, or none for every room of the plan.")).ToList()
            : m.OfClass(IfcClass.Space).Where(r => Spatial.StoreyOf(m, r.Id)?.Id == storey).ToList();
        var corners = Corners(drawing, rooms.Select(r => r.Id).ToHashSet());
        var scale = view.NumOrNull("scale") ?? 100;
        var (roomCount, strings) = (0, 0);
        foreach (var room in rooms)
        {
            if (ctx.Query.Geometry(room.Id)?.Plan?.FirstOrDefault(p => p.Kind == "room") is not { } outline || outline.Points.Count < 3) continue;
            // Dimensioned again: the old strings go.
            foreach (var old in m.OfClass(IfcClass.Annotation).Where(a => a.PredefinedType == "DIMENSION" && a.Str("viewId") == view.Id && a.Str(RoomKey) == room.Id).ToList())
                ctx.Remove(old.Id);
            var made = Strings(outline.Points, corners, offset ?? PaperOffset * scale);
            foreach (var (pts, at) in made)
            {
                var attrs = new JsonObject { ["viewId"] = view.Id, ["at"] = new JsonArray(at.X, at.Y), [RoomKey] = room.Id };
                var u = (pts[^1] - pts[0]).Normalized();
                Dimensions.Write(attrs, pts.Select(p => (p, hold(p, u))).ToList());
                ctx.Add(new Entity(ctx.NewId(), IfcClass.Annotation, $"Dimension {room.Name}".Trim(), attrs, "DIMENSION"));
            }
            if (made.Count > 0) roomCount++;
            strings += made.Count;
        }
        return (roomCount, strings);
    }

    /// <summary>The corners of what the plan cuts (walls, columns, doors and windows' openings), the rooms themselves left out.</summary>
    static List<Vec2> Corners(ViewDrawing? d, HashSet<string> rooms) =>
        d == null ? [] : d.Cuts.Where(p => !rooms.Contains(p.Element)).SelectMany(p => p.Loops.SelectMany(l => l)).ToList();

    /// <summary>
    /// The strings for one outline: for each side (sides in line merged, short ones left out), its points from end to end
    /// with the cut's corners on it between, and a point of the dimension line, <paramref name="offset"/> in from the side
    /// (at most a quarter of the room's narrower extent, so two opposite strings do not cross).
    /// </summary>
    public static List<(List<Vec2> Pts, Vec2 At)> Strings(IReadOnlyList<Vec2> outline, IReadOnlyList<Vec2> corners, double offset)
    {
        var poly = Sides(outline);
        var o = new List<(List<Vec2>, Vec2)>();
        if (poly.Count < 3) return o;
        var area = Polygon2.SignedArea(poly);
        if (Math.Abs(area) < 1e-9) return o;
        var (minX, maxX, minY, maxY) = (poly.Min(p => p.X), poly.Max(p => p.X), poly.Min(p => p.Y), poly.Max(p => p.Y));
        offset = Math.Min(offset, Math.Min(maxX - minX, maxY - minY) / 4);
        for (var i = 0; i < poly.Count; i++)
        {
            var (a, b) = (poly[i], poly[(i + 1) % poly.Count]);
            var side = b - a;
            var len = side.Length;
            if (len < MinSide) continue;
            var u = side * (1 / len);
            // Inside: the left of an anticlockwise outline, the right of a clockwise one.
            var inward = area > 0 ? new Vec2(-u.Y, u.X) : new Vec2(u.Y, -u.X);
            var along = new List<double> { 0, len };
            foreach (var c in corners)
            {
                var t = Vec2.Dot(c - a, u);
                if (t <= OnSide || t >= len - OnSide) continue;
                if (Math.Abs(Vec2.Cross(u, c - a)) > OnSide) continue;
                along.Add(t);
            }
            along.Sort();
            var pts = new List<Vec2>();
            foreach (var t in along)
                if (pts.Count == 0 || Vec2.Dot(a + u * t - pts[^1], u) > 0.005) pts.Add(a + u * t);
            // The far end exact, not a point 5 mm short of it.
            pts[^1] = b;
            o.Add((pts, a + u * (len / 2) + inward * offset));
        }
        return o;
    }

    /// <summary>The outline's sides: repeated points dropped and corners in line (within 0.5°) taken out.</summary>
    static List<Vec2> Sides(IReadOnlyList<Vec2> outline)
    {
        var pts = new List<Vec2>();
        foreach (var p in outline)
            if (pts.Count == 0 || (p - pts[^1]).Length > 1e-6) pts.Add(p);
        if (pts.Count > 1 && (pts[0] - pts[^1]).Length <= 1e-6) pts.RemoveAt(pts.Count - 1);
        var changed = true;
        while (changed && pts.Count >= 3)
        {
            changed = false;
            for (var i = 0; i < pts.Count; i++)
            {
                var (p, q, r) = (pts[(i + pts.Count - 1) % pts.Count], pts[i], pts[(i + 1) % pts.Count]);
                var (d1, d2) = (q - p, r - q);
                if (d1.Length < 1e-9 || d2.Length < 1e-9 || (Math.Abs(Vec2.Cross(d1.Normalized(), d2.Normalized())) < Math.Sin(Math.PI / 360) && Vec2.Dot(d1, d2) > 0))
                {
                    pts.RemoveAt(i);
                    changed = true;
                    break;
                }
            }
        }
        return pts;
    }
}
