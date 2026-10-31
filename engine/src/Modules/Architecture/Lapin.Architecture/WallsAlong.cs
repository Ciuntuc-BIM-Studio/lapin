using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Native walls along a drafted shape (docs/plan-studio-thick.md: Studio's Ring, Box and Wall on a plan): a line or an open
/// polyline makes a chain of walls, a closed polyline a closed loop, an arc curved walls, a circle four quarter walls, each
/// of the wall type given (the project's default without), joined at their corners as one drawn by hand would be. Where
/// the wall lies on the line is its justification: <c>left</c>, <c>right</c> or <c>centre</c> of it as drawn, or
/// <c>inside</c> and <c>outside</c> a closed shape; the type's whole thickness is laid off the line, whatever its core.
/// A closed shape may also take a floor at its bottom and one at its top (the type's default floor, its outline the shape).
/// </summary>
public static class WallsAlong
{
    readonly record struct Seg(Vec2 A, Vec2 B, double Rise);

    static Vec2 P(JsonNode? n) => n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : throw new CommandException("A shape's point is [x, y].");

    /// <summary>The shape's line as straight or curved pieces, counter-clockwise when closed, moved <paramref name="left"/> to its left; its outline when closed.</summary>
    static (List<Seg> Segs, List<Vec2>? Outline) Path(JsonObject shape, Func<bool, bool, double> leftOf)
    {
        switch ((string?)shape["type"])
        {
            case "line":
            {
                var pts = Offsets.Open([P(shape["a"]), P(shape["b"])], leftOf(false, false));
                return ([new Seg(pts[0], pts[1], 0)], null);
            }
            case "polyline":
            {
                var closed = shape["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c;
                var raw = Offsets.Clean((shape["pts"] as JsonArray ?? []).Select(P).ToList(), closed);
                if (raw.Count < 2 || (closed && raw.Count < 3)) throw new CommandException("The polyline needs more points.");
                if (!closed)
                {
                    var o = Offsets.Open(raw, leftOf(false, false));
                    return (o.Zip(o.Skip(1), (a, b) => new Seg(a, b, 0)).ToList(), null);
                }
                var drawnCw = Polygon2.SignedArea(raw) < 0;
                var ccw = drawnCw ? raw.AsEnumerable().Reverse().ToList() : raw;
                // Left of a counter-clockwise outline is inside it: moved left, the outline shrinks.
                var loop = Offsets.Loop(ccw, -leftOf(true, drawnCw));
                return (loop.Select((p, i) => new Seg(p, loop[(i + 1) % loop.Count], 0)).ToList(), ccw);
            }
            case "circle":
            {
                var (cc, r) = (P(shape["c"]), Json.Number(shape["r"]) ?? 0);
                var rr = r - leftOf(true, false);
                if (!(rr > 1e-3)) throw new CommandException("The walls would meet at the circle's centre: the circle is too small for them.");
                var outline = Enumerable.Range(0, 64).Select(i => cc + new Vec2(Math.Cos(Math.PI * i / 32), Math.Sin(Math.PI * i / 32)) * r).ToList();
                return (Arcs(cc, rr, 0, 2 * Math.PI), outline);
            }
            case "arc":
            {
                var (cc, r) = (P(shape["c"]), Json.Number(shape["r"]) ?? 0);
                var (s0, s1) = (Json.Number(shape["start"]) ?? 0, Json.Number(shape["end"]) ?? 0);
                var sweep = ((s1 - s0) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
                if (sweep < 1e-9) sweep = 2 * Math.PI;
                var rr = r - leftOf(false, false);
                if (!(rr > 1e-3)) throw new CommandException("The wall would reach past the arc's centre.");
                return (Arcs(cc, rr, s0, sweep), null);
            }
        }
        throw new CommandException("Walls are laid along a line, a polyline, an arc or a circle.");
    }

    /// <summary>A counter-clockwise arc as curved walls of a quarter turn at most (their rise to the right of each chord: negative).</summary>
    static List<Seg> Arcs(Vec2 c, double r, double start, double sweep)
    {
        var n = Math.Max(1, (int)Math.Ceiling(sweep / (Math.PI / 2) - 1e-9));
        var step = sweep / n;
        var rise = -r * (1 - Math.Cos(step / 2));
        Vec2 At(double a) => c + new Vec2(Math.Cos(a), Math.Sin(a)) * r;
        return Enumerable.Range(0, n).Select(i => new Seg(At(start + step * i), At(start + step * (i + 1)), rise)).ToList();
    }

    public static JsonObject Add(ICommandContext ctx, JsonObject a, IPathElements paths)
    {
        var shape = a["shape"] as JsonObject ?? throw new CommandException("Which shape do the walls follow?");
        var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
        var type = (string?)a["typeId"] is { } typeId ? Spatial.Require(ctx.Model, typeId, IfcClass.WallType) : WallTypes.EnsureDefault(ctx);
        var height = Json.Number(a["height"]) ?? Walls.DefaultHeight;
        var baseOffset = Json.Number(a["baseOffset"]) ?? 0;
        var justify = (string?)a["justify"] ?? "centre";
        if (justify is not ("left" or "right" or "centre" or "inside" or "outside")) throw new CommandException("Walls lie left, right or centred on the line, or inside or outside a closed shape.");
        var style = Args.JoinStyle(a["joinStyle"]) ?? JoinStyle.Mitre;

        // The type's section across its axis, read off a wall of it (its layers lie as the type lays them on a wall).
        var probe = Walls.AddUnjoined(ctx, storeyId, new Vec2(0, 0), new Vec2(1, 0), height, baseOffset, type);
        var bands = Walls.Section(ctx.Model, ctx.Model.Get(probe.Id)!);
        var (lo, hi) = (bands.Min(b => b.From), bands.Max(b => b.To));
        ctx.Remove(probe.Id);
        // How far the axis moves to the left of the line: the wall's right face on it (left), its left face (right), its middle.
        double LeftOf(bool closed, bool drawnCw)
        {
            var j = justify switch
            {
                "inside" => "left",
                "outside" => "right",
                "left" when closed && drawnCw => "right",
                "right" when closed && drawnCw => "left",
                var x => x,
            };
            return j switch { "left" => -lo, "right" => -hi, _ => -(lo + hi) / 2 };
        }
        var (segs, outline) = Path(shape, LeftOf);
        var ids = new JsonArray();
        foreach (var s in segs)
        {
            if ((s.B - s.A).Length < 1e-3) continue;
            var wall = Walls.AddUnjoined(ctx, storeyId, s.A, s.B, height, baseOffset, type);
            if (s.Rise != 0) { var attrs = wall.CloneAttrs(); attrs[PathCurve.Key] = PathCurve.CheckRise(s.Rise, (s.B - s.A).Length); ctx.Replace(wall.With(attrs: attrs)); }
            Joins.Auto(ctx, paths, wall.Id, style);
            ids.Add((JsonNode)wall.Id);
        }
        if (ids.Count == 0) throw new CommandException("The shape is too short for a wall.");
        var slabs = new JsonArray();
        static bool B(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        if ((B(a["bottom"]) || B(a["top"])) && outline == null) throw new CommandException("A bottom or a top needs a closed shape.");
        if (B(a["bottom"])) slabs.Add((JsonNode)Slabs.Add(ctx, storeyId, outline!, baseOffset, null).Id);
        if (B(a["top"])) slabs.Add((JsonNode)Slabs.Add(ctx, storeyId, outline!, baseOffset + height, null).Id);
        return new JsonObject { ["ids"] = ids, ["slabs"] = slabs };
    }
}
