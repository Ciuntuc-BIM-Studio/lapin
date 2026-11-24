using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Align (forum topics 109, 170, 171; Revit's Align): a reference line picked on anything (a wall's face or axis, a grid axis,
/// a reference plane, a floor's edge, a drafted line, a column's face…), then a line on the element to move, parallel to it.
/// The element moves square to the reference until its line lies on it, by <see cref="ElementTransform"/>'s move, so what is
/// carried with it (its openings, its tags, its joins) goes as a move takes it; one undo step. The lines are the view's: a
/// plan's, or a section's or an elevation's with its <c>frame</c> (the move then goes up or down in the model). With
/// <c>lock</c> and a reference that is a grid axis or a reference plane, the element is linked to it (<see cref="Pins.LockTo"/>)
/// and follows it from then on, as Revit's padlock keeps an alignment. With <c>rotate</c> (forum topic 234), a line not parallel
/// to the reference is turned onto its direction first, about the point picked on it, in the same undo step.
/// </summary>
public static class ElementAlign
{
    /// <summary>How far from parallel two lines may be (radians, about 0.1°) and still align.</summary>
    public const double AngleTolerance = 0.1 * Math.PI / 180;

    /// <summary>The move that brings <paramref name="line"/> onto the reference line through <paramref name="ra"/> and <paramref name="rb"/>; null when they are not parallel.</summary>
    public static Vec2? Offset(Vec2 ra, Vec2 rb, Vec2 la, Vec2 lb)
    {
        var (r, l) = ((rb - ra).Normalized(), (lb - la).Normalized());
        if (Math.Abs(Vec2.Cross(r, l)) > Math.Sin(AngleTolerance)) return null;
        var n = r.Perp;
        return n * Vec2.Dot(ra - la, n);
    }

    /// <summary>
    /// {ids, reference: [[x, y], [x, y]], line: [[x, y], [x, y]], referenceId?, lock?, frame?, rotate?, pivot?: [x, y]}: the
    /// elements moved so the line lies on the reference; with <c>rotate</c>, a line not parallel to it is turned first (in a plan),
    /// by the smaller angle about <c>pivot</c>'s foot on it (else its middle). Returns {ids, by: [dx, dy], turned? (degrees), locked?}.
    /// </summary>
    public static JsonObject Run(ICommandContext ctx, JsonObject a)
    {
        var ids = Args.Ids(a).ToList();
        if (ids.Count == 0) throw new CommandException("Pick the element to align.");
        var (ra, rb) = Line(a["reference"], "reference");
        var (la, lb) = Line(a["line"], "line to align");
        var referenceId = (string?)a["referenceId"];
        if (referenceId != null && ids.Contains(referenceId)) throw new CommandException("Pick a line on another element: the reference stays where it is.");
        var lockTo = a["lock"] is JsonValue lv && lv.TryGetValue<bool>(out var lk) && lk;
        var target = lockTo ? LockTarget(ctx.Model, referenceId)
            ?? throw new CommandException("Only a grid axis or a reference plane keeps an alignment locked: align without the lock.") : null;
        var o = new JsonObject { ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]) };
        ElementTransform.Result? spun = null;
        // Not parallel, with rotate (forum topic 234, Revit's Align on lines): turned first by the smaller angle about the point
        // picked on its line (its foot on the line; the line's middle when none is given), in the same step.
        if (Offset(ra, rb, la, lb) == null && a["rotate"] is JsonValue rv && rv.TryGetValue<bool>(out var rot) && rot)
        {
            if (a["frame"] != null) throw new CommandException("Elements turn in a plan: in a section or an elevation, pick a line parallel to the reference.");
            foreach (var id in ids)
                if (ctx.Model.RelatingOf(id, IfcClass.RelFillsElement) != null)
                    throw new CommandException("A door or a window turns with its wall: pick a line on it parallel to the reference.");
            var (r, l) = ((rb - ra).Normalized(), (lb - la).Normalized());
            var turn = Math.Atan2(Vec2.Cross(l, r), Vec2.Dot(l, r));
            if (turn > Math.PI / 2) turn -= Math.PI;
            else if (turn <= -Math.PI / 2) turn += Math.PI;
            var pivot = Args.TryPoint(a["pivot"]) is { } pv ? la + l * Vec2.Dot(pv - la, l) : (la + lb) * 0.5;
            var degrees = turn * 180 / Math.PI;
            var spin = new JsonObject { ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["op"] = "rotate", ["center"] = new JsonArray(pivot.X, pivot.Y), ["angle"] = degrees };
            // Joined again once, after the move: not where it stands between the two.
            spun = ElementTransform.Run(ctx, spin);
            Vec2 Turn(Vec2 p) { var d = p - pivot; var (c, sn) = (Math.Cos(turn), Math.Sin(turn)); return pivot + new Vec2(d.X * c - d.Y * sn, d.X * sn + d.Y * c); }
            (la, lb) = (Turn(la), Turn(lb));
            o["turned"] = Math.Round(degrees, 9);
        }
        var by = Offset(ra, rb, la, lb)
            ?? throw new CommandException("The lines are not parallel: Align moves an element square to the reference, so pick a line parallel to it (Rotate it first).");
        o["by"] = new JsonArray(by.X, by.Y);
        if (by.Length > 1e-9)
        {
            var move = new JsonObject { ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["op"] = "move", ["by"] = new JsonArray(by.X, by.Y) };
            if (a["frame"] is JsonObject f) move["frame"] = f.DeepClone();
            var t = ElementTransform.Run(ctx, move);
            ElementTransform.JoinWhatMeets(ctx, t);
        }
        else if (spun != null) ElementTransform.JoinWhatMeets(ctx, spun);
        if (target != null)
        {
            Pins.LockTo(ctx, ids, target);
            o["locked"] = target.DeepClone();
        }
        return o;
    }

    /// <summary>What an element aligned to this reference locks to: a grid's axis (grid#tag) or a reference plane; none for anything else.</summary>
    public static JsonObject? LockTarget(IModelView m, string? referenceId)
    {
        if (string.IsNullOrEmpty(referenceId)) return null;
        var hash = referenceId.IndexOf('#');
        if (hash > 0 && m.Get(referenceId[..hash]) is { Class: IfcClass.Grid } grid)
            return new JsonObject { ["kind"] = "axis", ["gridId"] = grid.Id, ["tag"] = referenceId[(hash + 1)..] };
        if (m.Get(referenceId) is { Class: Views.Class } v && v.Str("kind") == RefPlanes.Kind)
            return new JsonObject { ["kind"] = "plane", ["id"] = v.Id };
        return null;
    }

    static (Vec2, Vec2) Line(JsonNode? n, string what)
    {
        if (n is not JsonArray { Count: 2 } l || Args.TryPoint(l[0]) is not { } p || Args.TryPoint(l[1]) is not { } q)
            throw new CommandException($"Give the {what} as two points.");
        if ((q - p).Length < 1e-9) throw new CommandException($"The {what} needs two different points.");
        return (p, q);
    }
}
