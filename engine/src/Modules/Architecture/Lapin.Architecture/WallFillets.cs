using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Two walls' corner rounded or cut, as CAD's FILLET and CHAMFER do to two lines (a user's request): the two walls meet, or
/// would meet carried on along their axes (extended or trimmed, like CAD's fillet); each is trimmed back (or carried on) to
/// where the corner's piece meets it, and that piece is a wall of its own added between, joined to both: a curved wall for a
/// fillet, tangent to both (an in-line join at each end), a straight one for a chamfer (a corner at each end). No radius (or
/// distance) makes the corner sharp: both run to their axes' crossing, joined there. One undo step.
/// <para>
/// The radius is the axis's (the wall's core's middle, Lapin's layer rule), or the inside or outside face's of the bend
/// (<c>line</c>); a chamfer's distance is measured along each axis from the crossing. The piece added is the first wall again:
/// its type, height, base, top (its constraint and roof attachment), layers and their side, its layer extents at the bottom
/// and top; the two walls keep their GlobalIds, their ends moved only. Walls of different types, heights or bases are refused
/// unless the first one's is taken (<c>matchFirst</c>). Only straight walls are filleted: a curved one's tangent changes along
/// it, and a wall with its own top profile (along its length) would lose it; both are refused. An opening in a trimmed wall
/// stays where it is in plan; one that would fall into the part cut away refuses the fillet.
/// </para>
/// </summary>
public static class WallFillets
{
    /// <summary>The shortest a trimmed wall may be left.</summary>
    public const double MinLength = 0.05;

    public static readonly string[] Lines = ["axis", "inside", "outside"];

    /// <summary>The attributes a wall keeps of its own that the piece made from it does not take: where it stands, its ends' own cuts, what the file it came from said.</summary>
    static readonly string[] OwnKeys = ["start", "end", PathCurve.Key, "imported", Walls.LayerEndsKey, Joins.StartAngle, Joins.EndAngle, Joins.AutoKey, WallTops.TopProfileKey, Pins.Key];

    /// <summary>
    /// The fillet (or chamfer) of walls <c>a</c> and <c>b</c>: {a, b, atA?, atB? (where each was picked: the part kept, as CAD
    /// keeps it), radius | distance, line? (axis, inside, outside), matchFirst?}. The piece added's id (none for a sharp corner).
    /// </summary>
    public static JsonObject Apply(ICommandContext ctx, JsonObject a, IPathElements paths, bool chamfer)
    {
        var m = ctx.Model;
        var wa = Spatial.Require(m, (string?)a["a"], IfcClass.Wall);
        var wb = Spatial.Require(m, (string?)a["b"], IfcClass.Wall);
        if (wa.Id == wb.Id) throw new CommandException("Pick two walls: a corner is between one wall and another.");
        var size = chamfer ? Json.Number(a["distance"]) ?? Json.Number(a["d1"]) ?? 0 : Json.Number(a["radius"]) ?? 0;
        if (!double.IsFinite(size) || size < 0) throw new CommandException(chamfer ? "A chamfer's distance is a length, 0 or more." : "A fillet's radius is a length, 0 or more.");
        var line = (string?)a["line"] ?? "axis";
        if (!Lines.Contains(line)) throw new CommandException("The radius is the axis's, or the inside or the outside face's.");
        var matchFirst = a["matchFirst"] is JsonValue mv && mv.TryGetValue<bool>(out var mf) && mf;
        Pins.Refuse([wa, wb]);
        foreach (var w in new[] { wa, wb })
        {
            if (w.Attrs["start"] is not JsonArray || w.Attrs["end"] is not JsonArray) throw new CommandException($"„{w.Name}” has no axis to fillet: it is a reference from a file.");
            if (PathCurve.Of(w).IsArc) throw new CommandException($"„{w.Name}” is curved: fillet and chamfer take two straight walls.");
            if (w.Attrs[WallTops.TopProfileKey] != null) throw new CommandException($"„{w.Name}” has a profile of its own along it: make its top flat first.");
        }
        if (Spatial.StoreyOf(m, wa.Id)?.Id != Spatial.StoreyOf(m, wb.Id)?.Id) throw new CommandException("The walls stand on different storeys.");
        if (!matchFirst && Differences(m, wa, wb) is { } diff)
            throw new CommandException($"The walls differ ({diff}): fillet them with the first wall's, or make them alike first.");

        var (sa, ea, sb, eb) = (Walls.Start(wa), Walls.End(wa), Walls.Start(wb), Walls.End(wb));
        var (ua, ub) = ((ea - sa).Normalized(), (eb - sb).Normalized());
        var den = Vec2.Cross(ua, ub);
        if (Math.Abs(den) < 1e-6) throw new CommandException("The walls run parallel: they never meet in a corner.");
        var x = sa + ua * (Vec2.Cross(sb - sa, ub) / den);
        var (da, movedA, keptA) = Side(sa, ea, ua, x, Args.TryPoint(a["atA"]));
        var (db, movedB, keptB) = Side(sb, eb, ub, x, Args.TryPoint(a["atB"]));
        if (keptA < MinLength || keptB < MinLength) throw new CommandException("Pick each wall on the part to keep: that one lies on the other side of the corner.");
        var theta = Math.Acos(Math.Clamp(Vec2.Dot(da, db), -1, 1));
        if (theta < 1e-3) throw new CommandException("The walls run on along each other: there is no corner between them.");

        // The piece runs on from the first wall the way it runs (its layers on the same side), so from the first wall's
        // moved end to the second's when that is its end, else the other way.
        var forward = movedA == 1;
        // How the axis turns going along the piece: left (+1) or right.
        var turn = Math.Sign(forward ? Vec2.Cross(-da, db) : Vec2.Cross(-db, da));
        var frame = paths.FrameOf(wa, m) ?? throw new CommandException($"„{wa.Name}” has no body to fillet.");
        var axisRadius = size;
        if (!chamfer && size > 0 && line != "axis")
        {
            // The face nearer the bend's centre: left of the way the piece runs when it turns left.
            var y = (line == "inside") == (turn > 0) ? frame.YMax : frame.YMin;
            axisRadius = size + turn * y;
        }
        if (!chamfer && size > 0)
        {
            var inner = axisRadius - turn * (turn > 0 ? frame.YMax : frame.YMin);
            if (inner < 1e-3) throw new CommandException($"The radius is too small for the wall's thickness: its inside face would turn round nothing (the axis needs more than {turn * (turn > 0 ? frame.YMax : frame.YMin):0.###} m).");
        }
        var back = size <= 1e-9 ? 0 : chamfer ? size : axisRadius / Math.Tan(theta / 2);
        if (keptA - back < MinLength || keptB - back < MinLength)
            throw new CommandException($"Too large for these walls: it needs {back:0.###} m along each from the corner, and they run {keptA:0.###} m and {keptB:0.###} m from it.");
        var (ta, tb) = (x + da * back, x + db * back);

        // The trimmed walls, and their openings where they stand in plan; refused when one would not fit any more.
        var nextA = Trimmed(wa, movedA, ta);
        var nextB = Trimmed(wb, movedB, tb);
        var places = new List<(Entity Opening, double Along)>();
        foreach (var (before, after) in new[] { (wa, nextA), (wb, nextB) })
        {
            var (s0, u0) = (Walls.Start(before), (Walls.End(before) - Walls.Start(before)).Normalized());
            var s1 = Walls.Start(after);
            foreach (var o in Openings.PlacedOn(m, before.Id))
            {
                var along = Vec2.Dot(s0 + u0 * o.Along - s1, u0);
                try { Openings.CheckFits(m, after, along, o.Sill, o.Width, o.Height, o.Opening.Id); }
                catch (CommandException) { throw new CommandException($"„{o.Fill?.Name ?? o.Opening.Name}” in „{before.Name}” would fall where the wall is cut away: a smaller {(chamfer ? "distance" : "radius")}, or move it first."); }
                if (Math.Abs(along - o.Along) > 1e-9) places.Add((o.Opening, along));
            }
        }

        // The joins at the moved ends go (the corner's, and any other there); the rest are worked out again on the walls as they are now.
        var styleWas = Joins.Between(m, wa.Id, wb.Id) is { } was ? Joins.StyleOf(was) : JoinStyle.Mitre;
        foreach (var (w, end) in new[] { (wa, movedA), (wb, movedB) })
            foreach (var r in Joins.Of(m, w.Id).ToList())
            {
                var mine = (string?)r.Attrs?[r.Relating == w.Id ? "relatingConnection" : "relatedConnection"];
                if (r.Touches(wa.Id) && r.Touches(wb.Id) || mine == (end == 0 ? "ATSTART" : "ATEND")) ctx.RemoveRelation(r.Id);
            }
        ctx.Replace(nextA);
        ctx.Replace(nextB);
        foreach (var (o, along) in places)
        {
            var now = ctx.Model.Get(o.Id)!;
            var oa = now.CloneAttrs();
            oa["along"] = along;
            ctx.Replace(now.With(attrs: oa));
        }
        Joins.Refresh(ctx, paths, [wa.Id, wb.Id]);

        if (back <= 1e-9)
        {
            Joins.Join(ctx, paths, ctx.Model.Get(wa.Id)!, ctx.Model.Get(wb.Id)!, styleWas);
            return new JsonObject { ["a"] = wa.Id, ["b"] = wb.Id };
        }
        var (p, q) = forward ? (ta, tb) : (tb, ta);
        var rise = chamfer ? 0 : -turn * (q - p).Length / 2 * Math.Tan((Math.PI - theta) / 4);
        var piece = Twin(ctx, wa, p, q, rise);
        // Tangent at both ends, an arc meets each wall in line; a chamfer meets each at a corner, mitred.
        Joins.Join(ctx, paths, ctx.Model.Get(wa.Id)!, ctx.Model.Get(piece.Id)!, chamfer ? JoinStyle.Mitre : null);
        Joins.Join(ctx, paths, ctx.Model.Get(piece.Id)!, ctx.Model.Get(wb.Id)!, chamfer ? JoinStyle.Mitre : null);
        var o2 = new JsonObject { ["id"] = piece.Id, ["a"] = wa.Id, ["b"] = wb.Id };
        if (!chamfer) o2["radius"] = Math.Round(axisRadius, 9);
        return o2;
    }

    /// <summary>
    /// Which way from the corner a wall is kept (where it was picked; without a pick, the way its far end lies), which of its
    /// ends moves to the corner's piece (0 start, 1 end), and how far its kept end lies from the corner.
    /// </summary>
    static (Vec2 Dir, int Moved, double Kept) Side(Vec2 s, Vec2 e, Vec2 u, Vec2 x, Vec2? pick)
    {
        var (t0, t1) = (Vec2.Dot(s - x, u), Vec2.Dot(e - x, u));
        var sign = pick is { } p && Math.Abs(Vec2.Dot(p - x, u)) > 1e-6 ? Math.Sign(Vec2.Dot(p - x, u)) : Math.Abs(t1) >= Math.Abs(t0) ? Math.Sign(t1) : Math.Sign(t0);
        if (sign == 0) sign = 1;
        var (k0, k1) = (t0 * sign, t1 * sign);
        return (u * sign, k1 >= k0 ? 0 : 1, Math.Max(k0, k1));
    }

    static Entity Trimmed(Entity w, int moved, Vec2 to)
    {
        var attrs = w.CloneAttrs();
        attrs[moved == 0 ? "start" : "end"] = new JsonArray(to.X, to.Y);
        // Its own end cut there (an angle) goes: the corner's piece joins it.
        attrs.Remove(moved == 0 ? Joins.StartAngle : Joins.EndAngle);
        return w.With(attrs: attrs);
    }

    /// <summary>What tells the two walls apart: their types, heights or bases; null when they are alike.</summary>
    static string? Differences(IModelView m, Entity a, Entity b)
    {
        var o = new List<string>();
        if (Walls.TypeOf(m, a.Id)?.Id != Walls.TypeOf(m, b.Id)?.Id) o.Add("type");
        if (Math.Abs(Walls.HeightOf(m, a) - Walls.HeightOf(m, b)) > 1e-6) o.Add("height");
        if (Math.Abs(a.Num("baseOffset") - b.Num("baseOffset")) > 1e-6) o.Add("base");
        if (Math.Abs((a.NumOrNull("thickness") ?? 0) - (b.NumOrNull("thickness") ?? 0)) > 1e-6) o.Add("thickness");
        return o.Count == 0 ? null : string.Join(", ", o);
    }

    /// <summary>
    /// A wall like <paramref name="like"/> from <paramref name="p"/> to <paramref name="q"/> (rising <paramref name="rise"/>): its
    /// fields (height, base, top, roof attachment, thickness, phase and what other modules wrote on it), its type, storey and
    /// material (its own usage, the same side), its type's sweeps, its own layer extents at the bottom and the top.
    /// </summary>
    static Entity Twin(ICommandContext ctx, Entity like, Vec2 p, Vec2 q, double rise)
    {
        var m = ctx.Model;
        var attrs = like.CloneAttrs();
        foreach (var k in OwnKeys) attrs.Remove(k);
        attrs["start"] = new JsonArray(p.X, p.Y);
        attrs["end"] = new JsonArray(q.X, q.Y);
        if (Math.Abs(rise) > 1e-9) attrs[PathCurve.Key] = Math.Round(rise, 9);
        var extents = LayerExtents.Of(like).Where(x => x.Edge is LayerExtents.Bottom or LayerExtents.Top).ToList();
        if (extents.Count > 0) attrs[LayerExtents.Key] = new JsonArray([.. extents.Select(x => (JsonNode)x.ToJson())]);
        else attrs.Remove(LayerExtents.Key);
        var wall = new Entity(ctx.NewId(), IfcClass.Wall, like.Name, attrs, like.PredefinedType);
        ctx.Add(wall);
        foreach (var r in m.RelationsOf(like.Id).Where(r => r.Related.Contains(like.Id)).ToList())
            switch (r.Class)
            {
                case IfcClass.RelContainedInSpatialStructure or IfcClass.RelDefinesByType:
                    ctx.Relate(r.Class, r.Relating, wall.Id);
                    break;
                case IfcClass.RelAssociatesMaterial when m.Get(r.Relating) is { } relating:
                    if (relating.Class == IfcClass.MaterialLayerSetUsage)
                    {
                        // A usage belongs to one wall: the piece gets its own, the same layers on the same side.
                        var usage = new Entity(ctx.NewId(), relating.Class, relating.Name, relating.CloneAttrs(), relating.PredefinedType);
                        ctx.Add(usage);
                        foreach (var ur in m.RelationsOf(relating.Id).Where(x => x.Related.Contains(relating.Id)).ToList()) ctx.Relate(ur.Class, ur.Relating, usage.Id);
                        ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelAssociatesMaterial, usage.Id, [wall.Id]));
                    }
                    else ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelAssociatesMaterial, relating.Id, [wall.Id]));
                    break;
            }
        WallSweepTypes.Rebuild(ctx, wall);
        return ctx.Model.Get(wall.Id)!;
    }
}
