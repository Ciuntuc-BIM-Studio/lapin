using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// Where a dimension is drawn (§4.2): an optional <c>place</c> the user gave it by dragging, else the interface places it.
/// One scheme for every kind, a distance from what it measures, in metres:
/// <list type="bullet">
/// <item><c>distance</c>, <c>distanceX</c>, <c>distanceY</c>: <c>{offset}</c>, signed, the dimension line's distance from its first
/// end, across its direction, positive to the left (<see cref="LinearFrame"/>: a distance's direction is from its first end to its
/// second, a horizontal one's +x, a vertical one's +y; so a horizontal distance's line stands <c>offset</c> above its first end, a
/// vertical one's <c>offset</c> left of it).</item>
/// <item><c>angle</c>: <c>{offset}</c>, the radius of its arc about the lines' crossing (one line: about its start), more than
/// nothing.</item>
/// <item><c>radius</c>, <c>diameter</c>: <c>{offset, angle}</c>, the text <c>offset</c> beyond the rim (negative: inside), on the
/// ray from the centre at <c>angle</c> degrees, counter-clockwise from the view's x axis.</item>
/// </list>
/// A move, turn, mirror or scale of all its shapes carries the place as it carries the shapes (<see cref="CarryPlace"/>); any other
/// change keeps the numbers, so the dimension stays where it was relative to what it measures.
/// </summary>
public static partial class SketchConstraints
{
    /// <summary>A place checked and written the one way, or removed; refused when it does not fit the kind.</summary>
    static void CheckPlace(JsonObject c, string kind)
    {
        if (c["place"] is null) { c.Remove("place"); return; }
        if (c["place"] is not JsonObject p) throw new CommandException("A dimension's place is {offset} ({offset, angle} for a radius or a diameter).");
        var offset = Json.Number(p["offset"]) is { } o && double.IsFinite(o) ? o : throw new CommandException("A dimension's place needs its offset, a length.");
        var o2 = new JsonObject { ["offset"] = Math.Round(offset, 9) };
        if (kind == "angle" && offset <= 0) throw new CommandException("An angle's arc has a radius more than nothing.");
        if (kind is "radius" or "diameter")
        {
            var angle = p["angle"] is null ? 45 : Json.Number(p["angle"]) is { } x && double.IsFinite(x) ? x : throw new CommandException("A radius' place has its angle in degrees.");
            o2["angle"] = Angle(angle);
        }
        c["place"] = o2;
    }

    /// <summary>
    /// The frame a linear dimension's offset is measured in: its first end and the unit vector to the left of its direction. The
    /// ends are the interface's (constraints.ts, dimensionDraw): a line's two ends, two points, a point and its foot on a line, a
    /// lone point measured from the sketch's origin.
    /// </summary>
    internal static (Vec2 A, Vec2 N)? LinearFrame(JsonObject c, Func<string, Shape?> shapeOf)
    {
        var refs = Refs(c).ToList();
        Vec2? Pt(int i) => i < refs.Count && shapeOf(refs[i].Shape) is { } s ? PointOf(s, refs[i].Part) : null;
        (Vec2 A, Vec2 B)? Seg(int i) => i < refs.Count && shapeOf(refs[i].Shape) is { } s ? SegmentOf(s, refs[i].Part) : null;
        static Vec2 Foot(Vec2 p, Vec2 a, Vec2 b)
        {
            var d = b - a;
            var l2 = Vec2.Dot(d, d);
            return l2 < 1e-24 ? a : a + d * (Vec2.Dot(p - a, d) / l2);
        }
        (Vec2 A, Vec2 B)? ends = refs.Count switch
        {
            1 => Seg(0) ?? (Pt(0) is { } p ? (new Vec2(0, 0), p) : null),
            2 when Pt(0) is { } p0 && Pt(1) is { } p1 => (p0, p1),
            2 when Pt(0) is { } q && Seg(1) is (var a, var b) => (q, Foot(q, a, b)),
            2 when Seg(0) is (var a0, var b0) && Pt(1) is { } q1 => (q1, Foot(q1, a0, b0)),
            _ => null,
        };
        if (ends is not (var from, var to)) return null;
        switch ((string?)c["kind"])
        {
            case "distanceX": return (from, new Vec2(0, 1));
            case "distanceY": return (from, new Vec2(-1, 0));
            case "distance":
            {
                var d = to - from;
                var l = d.Length;
                return (from, l < 1e-12 ? new Vec2(0, 1) : new Vec2(-d.Y / l, d.X / l));
            }
        }
        return null;
    }

    /// <summary>
    /// A dimension's place carried by a transform of all its shapes: a linear one's line as a point carried and measured again in
    /// the frame the carried constraint has on the carried shapes (a mirror puts it on the other side, a quarter turn of a
    /// horizontal distance makes it a vertical one's); an angle's arc and a radius' text scale; a radius' ray turns.
    /// </summary>
    static void CarryPlace(JsonObject before, JsonObject after, Func<string, Shape?> was, Func<string, Shape?> now, Placement2D t, double scale)
    {
        if (before["place"] is not JsonObject p || Json.Number(p["offset"]) is not { } offset) return;
        var o = (JsonObject)p.DeepClone();
        switch ((string?)after["kind"])
        {
            case "distance" or "distanceX" or "distanceY":
                if (LinearFrame(before, was) is not (var a, var n) || LinearFrame(after, now) is not (var a2, var n2)) return;
                o["offset"] = Math.Round(Vec2.Dot(t.Point(a + n * offset) - a2, n2), 9);
                break;
            case "angle":
                o["offset"] = Math.Round(offset * scale, 9);
                break;
            case "radius" or "diameter":
                o["offset"] = Math.Round(offset * scale, 9);
                if (Json.Number(p["angle"]) is { } deg) o["angle"] = Angle(t.Angle(deg * Math.PI / 180) * 180 / Math.PI);
                break;
            default: return;
        }
        after["place"] = o;
    }
}
