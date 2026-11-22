using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// An element pinned where it is (decision 449), as Revit pins and as a sheet's view is pinned: any element, a reference, a
/// drafted shape. It is not moved, turned, mirrored, raised, sent to another storey nor deleted until it is unpinned; its
/// ends and outline are not dragged, and a wall joined to one that moves keeps its own ends. Its parameters still change
/// (a type, a height, a material), and it is copied freely: a copy is not pinned.
/// </summary>
public static class Pins
{
    public const string Key = "pinned";
    public const string LockedToKey = "lockedTo";

    /// <summary>The plan attributes that say where an element stands, which a pinned one keeps (<see cref="Placement.Carry"/>'s).</summary>
    public static readonly string[] PlaceKeys = ["start", "end", "at", "a", "b", "rotation", "contour", "points", "islands", "shape", "xf"];

    public static bool Is(Entity? e) => e?.Attrs[Key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public static string Message(Entity e) => $"„{e.Name}” is pinned: unpin it first (Modify → Unpin, or untick Pinned in its properties).";

    /// <summary>Refused when one of these is pinned.</summary>
    public static void Refuse(IEnumerable<Entity> entities)
    {
        if (entities.FirstOrDefault(Is) is { } pinned) throw new CommandException(Message(pinned));
    }

    /// <summary>Whether an element is a reference from a file: its body stored as the file gave it, or marked a reference.</summary>
    public static bool IsReference(Entity e) => StoredBodies.Has(e) || e.Attrs["reference"] != null;

    /// <summary>
    /// What an edit of an element's own outline or axis asks first (a split, a trim, an offset): refused when it is a reference
    /// from a file (<see cref="IsReference"/>, or <paramref name="reference"/>: the caller's own reason to treat it as one), or pinned.
    /// </summary>
    public static void RefuseEdit(Entity e, bool reference = false)
    {
        if (reference || IsReference(e)) throw new CommandException($"„{e.Name}” is a reference from a file: convert it to native first.");
        Refuse([e]);
    }

    /// <summary>Refused when the element is pinned and the change moves it (one of <see cref="PlaceKeys"/>).</summary>
    public static void RefuseMove(Entity e, IEnumerable<string> keys)
    {
        if (Is(e) && keys.Any(k => PlaceKeys.Contains(k))) throw new CommandException(Message(e));
    }

    /// <summary>A copy's attributes: not pinned, whatever its original is.</summary>
    public static void Unpinned(ICommandContext ctx, IEnumerable<string> ids)
    {
        foreach (var id in ids)
            if (ctx.Model.Get(id) is { } e && Is(e))
            {
                var attrs = e.CloneAttrs();
                attrs.Remove(Key);
                ctx.Replace(e.With(attrs: attrs));
            }
    }

    /// <summary>The elements (a group's members with the group) pinned or unpinned: how many changed.</summary>
    public static int Set(ICommandContext ctx, IReadOnlyList<string> given, bool pinned)
    {
        var (ids, groups) = Groups.Expand(ctx.Model, given);
        var changed = 0;
        foreach (var id in ids.Concat(groups).Distinct())
        {
            if (ctx.Model.Get(id) is not { } e || e.Class is IfcClass.Project or IfcClass.Site or IfcClass.Building or IfcClass.BuildingStorey) continue;
            if (Is(e) == pinned) continue;
            var attrs = e.CloneAttrs();
            if (pinned) attrs[Key] = true; else { attrs.Remove(Key); attrs.Remove(LockedToKey); }
            ctx.Replace(e.With(attrs: attrs));
            changed++;
        }
        return changed;
    }

    /// <summary>Link or unlink elements to a grid axis or reference plane (issue #37); linking also pins the element.</summary>
    public static int LockTo(ICommandContext ctx, IReadOnlyList<string> given, JsonObject? target)
    {
        var (ids, groups) = Groups.Expand(ctx.Model, given);
        var changed = 0;
        foreach (var id in ids.Concat(groups).Distinct())
        {
            if (ctx.Model.Get(id) is not { } e || e.Class is IfcClass.Project or IfcClass.Site or IfcClass.Building or IfcClass.BuildingStorey) continue;
            var attrs = e.CloneAttrs();
            if (target == null) attrs.Remove(LockedToKey);
            else { attrs[LockedToKey] = target.DeepClone(); attrs[Key] = true; }
            ctx.Replace(e.With(attrs: attrs));
            changed++;
        }
        return changed;
    }

    static bool IsIdentity(Placement2D t) =>
        t.Point(new Vec2(0, 0)).Length < 1e-9 && (t.Dir(new Vec2(1, 0)) - new Vec2(1, 0)).Length < 1e-9 && !t.Mirrored;

    /// <summary>Move/stretch elements locked to a grid axis when that axis moves.</summary>
    public static void FollowAxis(ICommandContext ctx, string gridId, string tag, Placement2D t)
    {
        if (IsIdentity(t)) return;
        var followers = ctx.Model.Entities.Where(e => e.Attrs[LockedToKey] is JsonObject o
            && (string?)o["kind"] == "axis" && (string?)o["gridId"] == gridId && (string?)o["tag"] == tag).ToList();
        if (followers.Count == 0) return;
        var moving = followers.Select(e => e.Id).ToHashSet();
        foreach (var e in followers)
            if (ctx.Model.Get(e.Id) is { } now) Placement.Transform(ctx, now, t, moving);
    }

    /// <summary>Move/stretch elements locked to a reference plane when that plane moves.</summary>
    public static void FollowPlane(ICommandContext ctx, string planeId, Placement2D t, IReadOnlySet<string> alreadyMoving)
    {
        if (IsIdentity(t)) return;
        var followers = ctx.Model.Entities.Where(e => !alreadyMoving.Contains(e.Id) && e.Attrs[LockedToKey] is JsonObject o
            && (string?)o["kind"] == "plane" && (string?)o["id"] == planeId).ToList();
        if (followers.Count == 0) return;
        var moving = alreadyMoving.Concat(followers.Select(e => e.Id)).ToHashSet();
        foreach (var e in followers)
            if (ctx.Model.Get(e.Id) is { } now) Placement.Transform(ctx, now, t, moving);
    }
}
