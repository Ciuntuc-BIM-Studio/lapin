using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A wall's top (the forum's topic #45): where it goes up to. <c>top</c> is <c>"above"</c> (the storey above its own),
/// a storey's id, or missing (unconnected: the wall's own <c>height</c>); <c>topOffset</c> is added to the level it
/// goes to. The height is worked out on every ask, never stored for a wall with a top: the level of the storey it goes
/// to plus the offset, less the wall's base (its storey's level plus its base offset). The storey above is the first
/// whose level is higher than the wall's own storey's (as a stair's, <see cref="Stairs.StairBuilder.TopStorey"/>); on
/// the topmost storey it is that storey's own top (its level and height). A top that is gone (its storey deleted by a
/// path that did not disconnect the wall) or at or below the base leaves the wall its own height, and the wall says so.
/// The stored <c>height</c> is kept as what an unconnected wall stands to: set to the worked-out height whenever the
/// top is set, so a wall disconnected keeps the height it had.
/// </summary>
public static class WallTop
{
    public const string Key = "top";
    public const string OffsetKey = "topOffset";
    public const string Above = "above";
    /// <summary>A wall type's top for new walls (<see cref="Above"/> or <see cref="Unconnected"/>), its offset and its height for unconnected walls.</summary>
    public const string TypeTopKey = "defaultTop", TypeOffsetKey = "defaultTopOffset", TypeHeightKey = "defaultHeight";
    public const string Unconnected = "unconnected";
    /// <summary>The least a wall's top may stand over its base (m).</summary>
    public const double MinHeight = 0.01;

    /// <summary>The wall's top as it holds it: <see cref="Above"/>, a storey's id, or null (unconnected).</summary>
    public static string? Of(Entity wall) => wall.Str(Key) is { Length: > 0 } t ? t : null;

    /// <summary>The wall's base: its storey's level plus its base offset.</summary>
    public static double Base(IModelView m, Entity wall) => (Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");

    /// <summary>The storey above <paramref name="storey"/>: the first whose level is higher; null on the topmost.</summary>
    public static Entity? StoreyAbove(IModelView m, Entity storey) =>
        Spatial.Storeys(m).FirstOrDefault(s => s.Num("elevation") > storey.Num("elevation") + 1e-6);

    /// <summary>The storey the wall's top goes to: the one named, else the one above its own; null when unconnected, the named one is gone, or on the topmost storey.</summary>
    public static Entity? TargetStorey(IModelView m, Entity wall)
    {
        var top = Of(wall);
        if (top == null) return null;
        if (top != Above) return m.Get(top) is { Class: IfcClass.BuildingStorey } named ? named : null;
        return Spatial.StoreyOf(m, wall.Id) is { } own ? StoreyAbove(m, own) : null;
    }

    /// <summary>The level (absolute) the wall's top is asked at, the offset in; null when unconnected or when there is nothing to go to.</summary>
    public static double? Level(IModelView m, Entity wall)
    {
        var top = Of(wall);
        if (top == null) return null;
        double? level = TargetStorey(m, wall)?.Num("elevation");
        // The topmost storey's own top: its level and height.
        if (level == null && top == Above && Spatial.StoreyOf(m, wall.Id) is { } own && own.NumOrNull("height") is { } h && h > 0) level = own.Num("elevation") + h;
        return level + wall.Num(OffsetKey);
    }

    /// <summary>The wall's own height, what it stands to unconnected.</summary>
    public static double Stored(Entity wall) => wall.Num("height", Walls.DefaultHeight);

    /// <summary>The wall's height: from its base to its top's level; its own height when unconnected or when its top cannot be had.</summary>
    public static double Height(IModelView m, Entity wall) =>
        Level(m, wall) is { } level && level - Base(m, wall) is var h && h >= MinHeight ? h : Stored(wall);

    /// <summary>What is wrong with the wall's top (for the Problems panel); null when nothing is.</summary>
    public static string? Problem(IModelView m, Entity wall)
    {
        var top = Of(wall);
        if (top == null) return null;
        if (top != Above && TargetStorey(m, wall) == null) return $"The storey its top went to is gone: it stands {Fmt(Stored(wall))} m high, its own height.";
        if (Level(m, wall) is { } level && level - Base(m, wall) < MinHeight)
            return $"Its top ({(top == Above ? "the storey above" : TargetStorey(m, wall)?.Name)}{Offset(wall)}) is at or below its base: it stands {Fmt(Stored(wall))} m high, its own height.";
        return null;
    }

    static string Fmt(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    static string Offset(Entity wall) => wall.Num(OffsetKey) is var o && Math.Abs(o) > 1e-9 ? $" {(o > 0 ? "+" : "−")} {Fmt(Math.Abs(o))} m" : "";

    /// <summary>
    /// Sets a wall's top in <paramref name="attrs"/> (its attributes, changed in place): <paramref name="top"/> as
    /// <see cref="Of"/> holds it (null: unconnected), the offset, and the stored height the wall stands to when unconnected
    /// (its height worked out, when it has a top). Refused when the top would be at or below the base.
    /// </summary>
    public static void Set(IModelView m, Entity wall, JsonObject attrs, string? top, double offset, double? height)
    {
        if (top != null && top != Above && m.Get(top) is not { Class: IfcClass.BuildingStorey }) throw new CommandException("A wall's top goes to a storey: the storey above, or one of the project's.");
        if (height is { } given && !(given > 0)) throw new CommandException("A wall's height must be positive.");
        // Disconnected without a height of its own: it keeps the height it has now.
        if (top == null) { if (height == null && Of(wall) != null) attrs["height"] = Math.Round(Height(m, wall), 6); attrs.Remove(Key); attrs.Remove(OffsetKey); }
        else
        {
            attrs[Key] = top;
            if (Math.Abs(offset) > 1e-12) attrs[OffsetKey] = offset; else attrs.Remove(OffsetKey);
        }
        if (height != null) attrs["height"] = height;
        var after = wall.With(attrs: attrs);
        if (top != null)
        {
            if (Level(m, after) is not { } level) { if (height == null) attrs["height"] = Height(m, wall); return; }
            var h = level - Base(m, after);
            if (h < MinHeight)
                throw new CommandException($"„{wall.Name}”: its top would be at or below its base ({Fmt(level)} m against {Fmt(Base(m, after))} m). Choose a storey higher up, or a smaller offset.");
            attrs["height"] = Math.Round(h, 6);
        }
    }

    /// <summary>
    /// A new wall's top (addWall): the one asked (<c>top</c>, <c>topOffset</c>), else unconnected when a height is given,
    /// else its type's (<see cref="TypeTopKey"/>), else the storey above.
    /// </summary>
    public static (string? Top, double Offset, double Height, bool Asked) ForNew(JsonObject a, Entity? type)
    {
        var height = Json.Number(a["height"]) ?? type?.NumOrNull(TypeHeightKey) ?? Walls.DefaultHeight;
        var offset = Json.Number(a["topOffset"]) ?? type?.NumOrNull(TypeOffsetKey) ?? 0;
        if (a.ContainsKey("top")) return ((string?)a["top"] is { Length: > 0 } t && t != Unconnected ? t : null, offset, height, true);
        if (Json.Number(a["height"]) != null) return (null, 0, height, false);
        return (type?.Str(TypeTopKey) == Unconnected ? null : Above, offset, height, false);
    }

    /// <summary>A wall type's top for new walls: <see cref="Above"/> or <see cref="Unconnected"/>, its offset, its height when unconnected.</summary>
    public static void SetTypeDefaults(IEdit edit, JsonObject a, Entity type)
    {
        var attrs = type.CloneAttrs();
        if (a.ContainsKey("top"))
        {
            var top = (string?)a["top"] is { Length: > 0 } t && t != Above ? t : Above;
            if (top != Above && top != Unconnected) throw new CommandException("A wall type's top is the storey above, or unconnected (its own height).");
            if (top == Above) attrs.Remove(TypeTopKey); else attrs[TypeTopKey] = Unconnected;
        }
        if (a.ContainsKey("topOffset"))
        {
            if (Json.Number(a["topOffset"]) is { } o && Math.Abs(o) > 1e-12) attrs[TypeOffsetKey] = o; else attrs.Remove(TypeOffsetKey);
        }
        if (a.ContainsKey("height"))
        {
            if (Json.Number(a["height"]) is { } h) attrs[TypeHeightKey] = h > 0 ? h : throw new CommandException("A wall's height must be positive.");
            else attrs.Remove(TypeHeightKey);
        }
        edit.Replace(type.With(attrs: attrs));
    }

    /// <summary>
    /// Before a storey goes: each wall whose top goes to it (named, or as the storey above) is disconnected, keeping the
    /// height it has now. Returns how many.
    /// </summary>
    public static int Release(IEdit edit, Entity storey)
    {
        var n = 0;
        foreach (var wall in edit.Model.OfClass(IfcClass.Wall).Where(w => Of(w) != null).ToList())
        {
            if (TargetStorey(edit.Model, wall)?.Id != storey.Id) continue;
            var attrs = wall.CloneAttrs();
            attrs["height"] = Math.Round(Height(edit.Model, wall), 6);
            attrs.Remove(Key);
            attrs.Remove(OffsetKey);
            edit.Replace(wall.With(attrs: attrs));
            n++;
        }
        return n;
    }
}
