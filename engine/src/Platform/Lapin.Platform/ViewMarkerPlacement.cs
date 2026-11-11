using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A section's heads moved off its line's ends (decision 460): each end's arrow and name stand at <c>headA</c> or
/// <c>headB</c> (an offset in plan, m) from it, a fine leader back to the end; what the section cuts stays.
/// </summary>
public static class SectionHeads
{
    public static string Key(string end) => end == "a" ? "headA" : "headB";

    /// <summary>How far the head at an end (a: the line's start, b: its end) stands from it.</summary>
    public static Vec2 Offset(Entity section, string end) =>
        section.Attrs[Key(end)] is JsonArray { Count: >= 2 } o ? new Vec2(Json.Number(o[0]) ?? 0, Json.Number(o[1]) ?? 0) : default;

    public static void Register(IModuleRegistry r)
    {
        // A section's head moved: {id, end: "a" | "b", offset: [dx, dy] | null} (null: back on its end). One undo step; pinned, refused.
        r.Command("view.moveHead", "Move section head", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Views.Class } x && x.Str("kind") == "section" ? x : throw new CommandException("Only a section has heads to move.");
            var end = (string?)a["end"] is "a" or "b" ? (string)a["end"]! : throw new CommandException("A section's head is at its start (a) or its end (b).");
            Pins.Refuse([v]);
            var attrs = v.CloneAttrs();
            if (a["offset"] is JsonArray { Count: >= 2 } o && Json.Number(o[0]) is { } dx && Json.Number(o[1]) is { } dy && (Math.Abs(dx) > 1e-9 || Math.Abs(dy) > 1e-9))
                attrs[Key(end)] = new JsonArray(Math.Round(dx, 6), Math.Round(dy, 6));
            else if (a["offset"] == null || a["offset"] is JsonArray) attrs.Remove(Key(end));
            else throw new CommandException("A head's offset is [dx, dy].");
            ctx.Replace(v.With(attrs: attrs));
            return null;
        });
    }
}

/// <summary>
/// Sections and elevations carried by element.transform in plan (decision 460): a section as Revit moves one, its line
/// and so its cut with it (turned and mirrored too, its heads' offsets turned with it, a mirrored one looking the mirrored
/// way); a section across or along an element follows the element and is not moved on its own. An elevation's marker is
/// moved, along its line and its look (<see cref="ElevationMarkers.Moved"/>; into the building, the elevation starts seeing at its line, decision 460);
/// it looks north, south, east or west, so it is not turned or mirrored.
/// </summary>
public sealed class ViewMarkerPlacement : IElementPlacement
{
    public bool Handles(Entity e) => e.Class == Views.Class && e.Str("kind") is "section" or "elevation";

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving) => ctx.Replace(Carried(ctx, e, t));

    public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        var moved = Carried(ctx, e, t);
        var copy = new Entity(ctx.NewId(), e.Class, Views.CopyName(ctx.Model, e.Name, e.Class), moved.CloneAttrs(), e.PredefinedType);
        ctx.Add(copy);
        return copy.Id;
    }

    static Entity Carried(ICommandContext ctx, Entity e, Placement2D t)
    {
        if (e.Str("kind") == "elevation")
        {
            if (Math.Abs(t.Turn) > 1e-9 || t.Mirrored) throw new CommandException("An elevation looks north, south, east or west: its marker is moved, not turned or mirrored.");
            return ElevationMarkers.Moved(ctx.Query, e, t.Point(default));
        }
        if (AcrossSections.Is(e) || e.Str("along") is { Length: > 0 })
            throw new CommandException($"„{e.Name}” follows its element: move the element, or stop following it first.");
        static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));
        var attrs = e.CloneAttrs();
        foreach (var k in new[] { "start", "end" })
            if (attrs[k] is JsonArray { Count: >= 2 } p) attrs[k] = J(t.Point(Args.PointOf(p)));
        if (attrs["pts"] is JsonArray pts)
        {
            var q = pts.OfType<JsonArray>().Select(p => t.Point(Args.PointOf(p))).ToList();
            // Mirrored, it looks the mirrored way: its line runs the other way (an arc's ends swap, its middle stays).
            if (t.Mirrored) { if (e.Str("curve") == "arc" && q.Count == 3) (q[0], q[1]) = (q[1], q[0]); else q.Reverse(); }
            attrs["pts"] = new JsonArray(q.Select(p => (JsonNode)J(p)).ToArray());
        }
        if (t.Mirrored && attrs["start"] is JsonArray s0 && attrs["end"] is JsonArray e0) (attrs["start"], attrs["end"]) = (e0.DeepClone(), s0.DeepClone());
        var (ha, hb) = (SectionHeads.Offset(e, "a"), SectionHeads.Offset(e, "b"));
        if (t.Mirrored) (ha, hb) = (hb, ha);
        foreach (var (k, off) in new[] { ("headA", ha), ("headB", hb) })
            if (off.Length > 1e-9) attrs[k] = J(t.Dir(off)); else attrs.Remove(k);
        return e.With(attrs: attrs);
    }
}
