using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A core command about to act on elements a module drives (docs/overrides.md, Driven elements): its id, the ids it was
/// given that the module drives, and its arguments as given. <see cref="Transform"/>, <see cref="Up"/> and
/// <see cref="Copy"/> say what an <c>element.transform</c> does (its arrays and copies too); <see cref="Storeys"/> the
/// storeys an element is copied or moved to (<c>element.copyToStoreys</c>, <c>element.moveToStorey</c>,
/// <c>storey.duplicate</c>).
/// </summary>
public sealed record DrivenEdit(string Command, IReadOnlyList<string> Ids, JsonObject Args)
{
    public Placement2D? Transform { get; init; }
    public double Up { get; init; }
    public bool Copy { get; init; }
    public IReadOnlyList<string> Storeys { get; init; } = [];
}

/// <summary>What the modules did with a <see cref="DrivenEdit"/>.</summary>
public sealed class DrivenOutcome
{
    /// <summary>The ids a module did the edit for in its own terms: the command leaves them alone.</summary>
    public HashSet<string> Handled { get; } = [];
    /// <summary>What a module made in their stead (an element.transform's copies): returned with the command's own.</summary>
    public List<string> Made { get; } = [];
}

/// <summary>
/// Elements a module drives from a specification of its own (a generator's output: rebuilt from its spec, its groups the
/// module's), kept away from the generic commands: before core deletes, moves, copies, changes, renames, groups, makes a
/// component of, or sends to another storey any of them, the module is asked; it lets the command go on, refuses it with a
/// message (a <see cref="CommandException"/>, as a pinned element is refused), or does the edit itself, in the same undo
/// step, and names what it took care of. After every command it is told what changed, so an edit made by any other
/// command (a door's type, its place along its wall) is written back into its spec before the next rebuild could undo it.
/// </summary>
public interface IDrivenElements
{
    /// <summary>Whether this element (or group) is one the module drives.</summary>
    bool Drives(IModelView m, Entity e);

    /// <summary>Before a core command acts: <see cref="DrivenEdit.Ids"/> are only the module's. Nothing done: allowed.</summary>
    void Before(ICommandContext ctx, DrivenEdit edit, DrivenOutcome outcome);

    /// <summary>After every command, in its undo step (before components settle): what it changed, written back where the module keeps it.</summary>
    void After(ICommandContext ctx, IEdit edit, string command) { }
}

/// <summary>Core's side of <see cref="IDrivenElements"/>: asking the modules, and leaving out what they took care of.</summary>
public static class Driven
{
    static readonly DrivenOutcome Nothing = new();

    /// <summary>Each module asked about the ids it drives, in load order; a refusal throws, and the whole command with it.</summary>
    public static DrivenOutcome Before(ICommandContext ctx, DrivenEdit edit)
    {
        var services = ctx.Services.All<IDrivenElements>();
        if (services.Count == 0 || edit.Ids.Count == 0) return Nothing;
        var outcome = new DrivenOutcome();
        foreach (var s in services)
        {
            var mine = edit.Ids.Distinct().Where(id => !outcome.Handled.Contains(id) && ctx.Model.Get(id) is { } e && s.Drives(ctx.Model, e)).ToList();
            if (mine.Count > 0) s.Before(ctx, edit with { Ids = mine }, outcome);
        }
        return outcome;
    }

    /// <summary>The ids given that no module took care of, in order.</summary>
    public static List<string> Rest(IEnumerable<string> ids, DrivenOutcome outcome) =>
        outcome.Handled.Count == 0 ? ids.ToList() : ids.Where(id => !outcome.Handled.Contains(id)).ToList();

    public static void After(ICommandContext ctx, IEdit edit, string command)
    {
        foreach (var s in ctx.Services.All<IDrivenElements>()) s.After(ctx, edit, command);
    }
}
