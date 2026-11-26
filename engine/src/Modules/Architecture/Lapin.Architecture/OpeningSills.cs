using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// What a door's, window's or opening's sill is measured from (forum topic 192): the wall's base, the structural level
/// (<see cref="Structural"/>, as every opening was before and every one an IFC file brings), or the finished floor
/// (<see cref="Finished"/>), as architects give it. The reference is the opening's own (<see cref="Key"/>, absent: structural);
/// the project's <see cref="ProjectKey"/> is the one new openings drawn in Lapin take. The finished floor's height over the
/// wall's base is never stored on the opening: it is worked out on every ask (<see cref="FinishedFloor"/>), so a wall or a
/// wall type given another one carries the openings measured from it. <see cref="Openings.Place"/> turns the stored sill into
/// the height over the wall's base, the one every consumer reads (the hole, the joinery, reveals, finishes, drawings,
/// quantities, IFC, which keeps the structural height and carries the reference in Lapin_Intent with the opening's attributes).
/// </summary>
public static class OpeningSills
{
    /// <summary>On the opening: "finished" (its sill over the finished floor), absent (over the wall's base).</summary>
    public const string Key = "sillFrom";
    /// <summary>On the project: the reference new doors, windows and openings take (absent: structural).</summary>
    public const string ProjectKey = "sillFrom";
    /// <summary>On a wall (its own) and a wall type (its walls' default): the finished floor's height over the wall's base, metres.</summary>
    public const string FinishedFloorKey = "finishedFloor";

    public const string Structural = "structural", Finished = "finished";
    public static readonly string[] References = [Structural, Finished];

    /// <summary>
    /// On a door's or window's opening (GitHub #149): true takes its hole down to the structure, the wall's base, by the finished
    /// floor's height, while the door or window stays on the finished floor — the screed and the flooring run under the door
    /// instead of a strip of wall. Only an opening measured from the finished floor has that floor under it; absent (false): the
    /// hole starts where the door does, as decision 749 has it.
    /// </summary>
    public const string ReachKey = "reachStructure";

    /// <summary>Whether the opening's hole goes down to the structure (<see cref="ReachKey"/>), for one measured from the finished floor.</summary>
    public static bool ReachesStructure(Entity opening) => FromFinished(opening) && opening.Bool(ReachKey);

    /// <summary>Whether the opening's sill is over the finished floor.</summary>
    public static bool FromFinished(Entity opening) => opening.Str(Key) == Finished;

    /// <summary>A reference given in a command, checked (null: none given).</summary>
    public static string? Parse(string? s)
    {
        if (s is null) return null;
        return s is Structural or Finished ? s : throw new CommandException("A sill is measured from the structural level (structural) or the finished floor (finished).");
    }

    /// <summary>The project's reference for new openings.</summary>
    public static string ProjectDefault(IModelView m) => Spatial.Project(m)?.Str(ProjectKey) == Finished ? Finished : Structural;

    /// <summary>
    /// The finished floor's height over the wall's base: the wall's own <see cref="FinishedFloorKey"/>, else its type's, else 0.
    /// A room's floor finish is not taken (the user's decision, 20.11.2026): its layers go down from the room's level.
    /// </summary>
    public static double FinishedFloor(IModelView m, Entity wall)
    {
        if (wall.NumOrNull(FinishedFloorKey) is { } own) return own;
        if (m.RelatingOf(wall.Id, IfcClass.RelDefinesByType) is { Class: IfcClass.WallType } type && type.NumOrNull(FinishedFloorKey) is { } typed) return typed;
        return 0;
    }

    /// <summary>How far over the wall's base the opening's stored sill is measured from: 0 for the structural level, else the finished floor.</summary>
    public static double BaseOf(IModelView m, Entity opening, Entity? wall = null)
    {
        if (!FromFinished(opening)) return 0;
        wall ??= Openings.WallOf(m, opening.Id);
        return wall is { Class: IfcClass.Wall } ? FinishedFloor(m, wall) : 0;
    }

    /// <summary>The opening's sill over the wall's base (the structural height), whichever it is measured from.</summary>
    public static double StructuralSill(IModelView m, Entity opening, Entity? wall = null) =>
        FromFinished(opening) ? opening.Num("sill") + BaseOf(m, opening, wall) : opening.Num("sill");

    /// <summary>The reference written on an opening's attributes: kept only for the finished floor, so a structural one stays as it was.</summary>
    public static void Write(JsonObject attrs, string? reference)
    {
        if (reference == Finished) attrs[Key] = Finished; else attrs.Remove(Key);
    }

    /// <summary>
    /// Refused when an opening of a wall measured from the finished floor no longer fits it as it stands now (the wall's or its
    /// type's finished floor changed, the wall given another type): the openings over the wall's base did not move.
    /// </summary>
    public static void CheckWall(IModelView m, Entity wall)
    {
        foreach (var p in Openings.PlacedOn(m, wall.Id))
            if (FromFinished(p.Opening)) Openings.CheckFits(m, wall, p.Along, p.Sill, p.Width, p.Height, p.Opening.Id);
    }

    /// <summary>The same for every wall of a wall type.</summary>
    public static void CheckType(IModelView m, Entity type)
    {
        foreach (var wall in m.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(w => w.Class == IfcClass.Wall).ToList()) CheckWall(m, wall);
    }

    /// <summary>A finished floor given in a command on a wall's or a type's attributes: a length (0 to 1 m), or null to clear it.</summary>
    public static void SetFinishedFloor(JsonObject attrs, JsonNode? value)
    {
        if (value is null) { attrs.Remove(FinishedFloorKey); return; }
        var v = Json.Number(value) ?? throw new CommandException("The finished floor is a height over the wall's base, in metres.");
        if (!(v >= 0 && v <= 1)) throw new CommandException("The finished floor lies 0 to 1000 mm over the wall's base.");
        attrs[FinishedFloorKey] = v;
    }
}
