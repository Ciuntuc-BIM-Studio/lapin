using System.Text.Json.Nodes;
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

    /// <summary>The plan attributes that say where an element stands, which a pinned one keeps (<see cref="Placement.Carry"/>'s).</summary>
    public static readonly string[] PlaceKeys = ["start", "end", "at", "a", "b", "rotation", "contour", "points", "islands", "shape", "xf"];

    public static bool Is(Entity? e) => e?.Attrs[Key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public static string Message(Entity e) => $"„{e.Name}” is pinned: unpin it first (Modify → Unpin, or untick Pinned in its properties).";

    /// <summary>Refused when one of these is pinned.</summary>
    public static void Refuse(IEnumerable<Entity> entities)
    {
        if (entities.FirstOrDefault(Is) is { } pinned) throw new CommandException(Message(pinned));
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
            if (pinned) attrs[Key] = true; else attrs.Remove(Key);
            ctx.Replace(e.With(attrs: attrs));
            changed++;
        }
        return changed;
    }
}
