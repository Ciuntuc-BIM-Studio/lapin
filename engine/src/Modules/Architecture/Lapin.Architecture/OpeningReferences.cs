using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// What a door's, a window's or a bare opening's temporary dimensions measure to (a tester's report, decision 511): the
/// places along its host wall's axis, metres from the wall's start, where the plan can hold a dimension's other end. On each
/// of the host's faces (its left one, YMax, and its right one, YMin) as the face's line meets them:
/// <list type="bullet">
/// <item>every straight wall of the storey that meets the host (a corner, a T, a crossing): its two faces, its core's two
/// faces (when the core is not the whole wall) and its axis;</item>
/// <item>the host's own ends, as its joins leave them on that face;</item>
/// <item>the host's other openings, by their middle and width (the same on both faces: an opening is cut square).</item>
/// </list>
/// On a curved host (an arc) the places are arc lengths along its axis, and <c>scale</c> says how long a metre of axis is
/// on each face (the face's radius over the axis's), so a dimension reads the length along the face it is drawn on; its
/// centre and radius come with them. For a section or an elevation, the opening's bottom and top and the storey's level
/// (absolute heights) and the sill <c>architecture.moveOpening</c> takes. Asked for an opening (<c>id</c>: the opening or the door or
/// window filling it) or for a wall alone (<c>wallId</c>: a door or window being placed). Nothing changes in the model.
/// </summary>
public static class OpeningReferences
{
    /// <summary>How near (m) a wall's footprint comes to a face of the host to meet it.</summary>
    const double Touch = 0.01;
    /// <summary>Walls this near parallel to the host (the sine of the angle) do not cross its face: an in-line wall is the host's end.</summary>
    const double MinSine = 0.02;

    static JsonArray J(Vec2 p) => new(p.X, p.Y);

    public static JsonObject Of(IQueryContext q, JsonObject a)
    {
        var (m, paths) = (q.Model, q.Services.Get<IPathElements>()!);
        Entity wall;
        Entity? opening = null;
        if ((string?)a["id"] is { } id)
        {
            var e = m.Get(id) ?? throw new CommandException("There is no such opening.");
            opening = e.Class == IfcClass.OpeningElement ? e : Openings.OpeningOf(m, e.Id) ?? throw new CommandException("It fills no opening.");
            wall = Openings.WallOf(m, opening.Id) is { Class: IfcClass.Wall } w ? w : throw new CommandException("The opening is in no wall.");
        }
        else wall = Spatial.Require(m, (string?)a["wallId"], IfcClass.Wall);

        var o = new JsonObject { ["wallId"] = wall.Id };
        // Up the wall, for a section or an elevation: the storey's level and the wall's base (absolute heights).
        var storeyZ = Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0;
        var baseZ = storeyZ + wall.Num("baseOffset");
        o["levels"] = new JsonObject { ["storey"] = storeyZ, ["base"] = baseZ };
        if (opening != null)
        {
            var p = Openings.Place(m, opening);
            var op = new JsonObject
            {
                ["id"] = opening.Id, ["along"] = p.Along, ["width"] = p.Width,
                // What architecture.moveOpening's sill is for the element selected: a door's or window's own, a bare opening's.
                ["sill"] = p.Fill != null ? p.FillSill : p.Sill,
                ["bottom"] = baseZ + p.Sill, ["top"] = baseZ + p.Sill + p.Height,
            };
            if (p.Fill != null) op["fill"] = p.Fill.Id;
            o["opening"] = op;
        }
        if (Walls.Frame(wall, m) is not { } f) { o["curved"] = true; return o; }
        var c = PathCurve.Of(wall);

        // The host's faces: each one's line (a polyline along an arc), where along the axis its body ends on it, and how
        // long a metre of axis is on it (1 on a straight wall; the face's radius over the axis's on an arc).
        Func<double, double> from, to;
        if (c.IsArc)
        {
            var ends = Joins.WithOwnEnds(Joins.EndsOf(m, wall, paths, []), wall, f);
            (from, to) = c.Ends(ends[0], ends[1], f.YMin, f.YMax);
            o["curved"] = true;
            o["centre"] = J(c.Centre);
            o["radius"] = c.Radius;
        }
        else
        {
            var dir0 = (f.End - f.Start).Normalized();
            var foot = RoomBounds.JoinedFootprint(wall, m, paths) ?? Rect(f);
            // JoinedFootprint: [right face start, right face end, left face end, left face start].
            double S(Vec2 p) => Vec2.Dot(p - f.Start, dir0);
            var (r0, r1, l0, l1) = (S(foot[0]), S(foot[1]), S(foot[3]), S(foot[2]));
            from = y => y > f.YMin + 1e-9 ? l0 : r0;
            to = y => y > f.YMin + 1e-9 ? l1 : r1;
        }
        var faceY = new Dictionary<string, double> { ["left"] = f.YMax, ["right"] = f.YMin };
        var faceLine = faceY.ToDictionary(k => k.Key, k => c.Line(from(k.Value), to(k.Value), k.Value));
        double Scale(double y) => c.IsArc ? (c.At(c.Length / 2, y) - c.Centre).Length / c.Radius : 1;

        var dir = (f.End - f.Start).Normalized();
        o["length"] = c.Length;
        o["start"] = J(f.Start);
        o["end"] = J(f.End);
        o["rise"] = c.IsArc ? c.Rise : 0;
        o["dir"] = J(dir);
        o["faces"] = new JsonObject { ["left"] = f.YMax, ["right"] = f.YMin };
        o["scale"] = new JsonObject { ["left"] = Scale(f.YMax), ["right"] = Scale(f.YMin) };
        o["ends"] = new JsonObject { ["left"] = new JsonArray(from(f.YMax), to(f.YMax)), ["right"] = new JsonArray(from(f.YMin), to(f.YMin)) };

        // Where the line through p0 along d crosses the host's face y, metres along the axis; the crossing nearest t (along
        // the line) on an arc, where a line crosses its circle twice. Null where it does not cross.
        double? Cross(double y, Vec2 p0, Vec2 d, double tNear)
        {
            if (!c.IsArc)
            {
                var cr = Vec2.Cross(dir, d);
                var on0 = f.Start + dir.Perp * y;
                return Math.Abs(cr) < 1e-12 ? null : Vec2.Cross(p0 - on0, d) / cr;
            }
            var r = (c.At(c.Length / 2, y) - c.Centre).Length;
            var w = p0 - c.Centre;
            var (b, cc) = (Vec2.Dot(w, d), Vec2.Dot(w, w) - r * r);
            var disc = b * b - cc;
            if (disc < 0) return null;
            var root = Math.Sqrt(disc);
            var (t1, t2) = (-b - root, -b + root);
            var t = Math.Abs(t1 - tNear) <= Math.Abs(t2 - tNear) ? t1 : t2;
            return c.Project(p0 + d * t);
        }

        var storey = Spatial.StoreyOf(m, wall.Id)?.Id;
        var walls = new JsonArray();
        foreach (var w in m.OfClass(IfcClass.Wall))
        {
            if (w.Id == wall.Id || Spatial.StoreyOf(m, w.Id)?.Id != storey) continue;
            if (PathCurve.Of(w).IsArc || Walls.Frame(w, m) is not { } g) continue;
            var wd = (g.End - g.Start).Normalized();
            var glen = (g.End - g.Start).Length;
            var foot = RoomBounds.JoinedFootprint(w, m, paths) ?? Rect(g);
            var sides = new JsonArray();
            foreach (var (side, line) in faceLine)
                if (Enumerable.Range(0, line.Count - 1).Any(i => Distance(foot, line[i], line[i + 1]) < Touch)) sides.Add((JsonNode)side);
            if (sides.Count == 0) continue;
            // Nearly along the host where it crosses its axis: an in-line wall is the host's end, not a face across it.
            var tMid = glen / 2;
            if (Cross(0, g.Start, wd, tMid) is not { } sx || Math.Abs(Vec2.Cross(c.TangentAt(sx), wd)) < MinSine) continue;
            var wl = wd.Perp;
            var core = Walls.Section(m, w).Where(b => b.Core).ToList();
            var (c0, c1) = core.Count > 0 ? (core.Min(b => b.From), core.Max(b => b.To)) : (g.YMin, g.YMax);
            var hasCore = core.Count > 0 && (c0 > g.YMin + 1e-6 || c1 < g.YMax - 1e-6);
            // On an arc, the crossing nearest where the wall meets the host's body.
            var tNear = Vec2.Dot(c.PointAt(Math.Clamp(sx, 0, c.Length)) - g.Start, wd);
            var entry = new JsonObject { ["id"] = w.Id, ["name"] = w.Name, ["sides"] = sides };
            var ok = true;
            foreach (var (side, y) in faceY)
            {
                double? At(double yw) => Cross(y, g.Start + wl * yw, wd, tNear);
                var (fa, fb, ax) = (At(g.YMin), At(g.YMax), At(0));
                if (fa is null || fb is null || ax is null) { ok = false; break; }
                var on = new JsonObject { ["faces"] = new JsonArray(fa.Value, fb.Value), ["axis"] = ax.Value };
                if (hasCore && At(c0) is { } ca && At(c1) is { } cb) on["core"] = new JsonArray(ca, cb);
                entry[side] = on;
            }
            if (ok) walls.Add((JsonNode)entry);
        }
        o["walls"] = walls;

        var others = new JsonArray();
        foreach (var p in Openings.PlacedOn(m, wall.Id))
        {
            if (p.Opening.Id == opening?.Id) continue;
            others.Add((JsonNode)new JsonObject { ["id"] = p.Fill?.Id ?? p.Opening.Id, ["name"] = p.Fill?.Name ?? p.Opening.Name, ["along"] = p.Along, ["width"] = p.Width });
        }
        o["openings"] = others;
        return o;
    }

    /// <summary>A straight wall's body square at its axis ends (no joins known).</summary>
    static IReadOnlyList<Vec2> Rect(JoinWall f)
    {
        var dir = (f.End - f.Start).Normalized();
        var l = dir.Perp;
        return [f.Start + l * f.YMin, f.End + l * f.YMin, f.End + l * f.YMax, f.Start + l * f.YMax];
    }

    /// <summary>How far the segment a–b lies from the polygon's outline (0 where it crosses it or lies inside it).</summary>
    internal static double Distance(IReadOnlyList<Vec2> poly, Vec2 a, Vec2 b)
    {
        if (Inside(poly, a) || Inside(poly, b)) return 0;
        var best = double.PositiveInfinity;
        for (var i = 0; i < poly.Count; i++) best = Math.Min(best, SegSeg(poly[i], poly[(i + 1) % poly.Count], a, b));
        return best;
    }

    static bool Inside(IReadOnlyList<Vec2> poly, Vec2 p)
    {
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var (pi, pj) = (poly[i], poly[j]);
            if ((pi.Y > p.Y) != (pj.Y > p.Y) && p.X < (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y) + pi.X) inside = !inside;
        }
        return inside;
    }

    static double SegSeg(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        double O(Vec2 p, Vec2 q, Vec2 r) => Vec2.Cross(q - p, r - p);
        var (o1, o2, o3, o4) = (O(a, b, c), O(a, b, d), O(c, d, a), O(c, d, b));
        if ((o1 > 0) != (o2 > 0) && (o3 > 0) != (o4 > 0) && o1 != 0 && o2 != 0 && o3 != 0 && o4 != 0) return 0;
        return Math.Min(Math.Min(PointSeg(c, a, b), PointSeg(d, a, b)), Math.Min(PointSeg(a, c, d), PointSeg(b, c, d)));
    }

    static double PointSeg(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        var l2 = Vec2.Dot(ab, ab);
        var t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p - a, ab) / l2, 0, 1);
        return (p - (a + ab * t)).Length;
    }
}
