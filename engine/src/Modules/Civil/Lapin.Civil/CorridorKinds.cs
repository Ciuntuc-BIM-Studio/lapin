using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A kind of corridor a module adds (a railway track): an element on an axis
/// (its chain: a polyline with curves, or IFC 4.3 segments) and a red line,
/// whose cross-section the module gives at each station. The corridor
/// commands (the axis, the profile, the sections) take it as they take a
/// road, a ditch or a retaining wall, and tell the module when it changed.
/// </summary>
public interface ICorridorKind
{
    /// <summary>The kind's name, as the corridor queries say it ("track").</summary>
    string Kind { get; }

    bool Is(Entity e);

    /// <summary>The cross-section at a station: its parts and how far it reaches left (+) and right (−).</summary>
    (IReadOnlyList<SectionPart> Parts, double Left, double Right) Section(IModelView m, Entity e, Chain c, double station);

    /// <summary>The material a part of the section is made of (an id the project keeps, or a name), for the drawn section.</summary>
    string? MaterialOf(Entity e, string key) => null;

    /// <summary>After a corridor command changed the element (its axis, its profile).</summary>
    void AfterChange(ICommandContext ctx, string id) { }
}

/// <summary>The corridor kinds the modules add (assigned whole, never mutated: another session may be reading them).</summary>
public static class CorridorKinds
{
    public static IReadOnlyList<ICorridorKind> All { get; set; } = [];

    public static ICorridorKind? Of(Entity e)
    {
        foreach (var k in All) if (k.Is(e)) return k;
        return null;
    }
}

/// <summary>
/// What CAD linework with heights becomes, beyond a road and a path: a kind a
/// module adds (a track from a surveyed axis). The maker takes the line's
/// points in plan, their heights (NaN: none) and the command's arguments, and
/// returns what it made (its id and what it says of it).
/// </summary>
public delegate JsonObject LineworkMaker(ICommandContext ctx, JsonObject args, string storeyId, string? name, string? layer, IReadOnlyList<Vec2> plan, IReadOnlyList<double>? heights);
