using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Content a module owns in a project, and what an edition without that module does with it (docs/overrides.md, Content
/// of an absent module). A class of the module's own is known by the package (it was written under its module, and is
/// kept and written back as it came); an entity of a platform class the module owns (a group it drives) carries the
/// module's id under <see cref="Key"/>. In an edition without that module such a group is <b>kept as it is</b>: its
/// members stay ordinary elements, moved, edited, deleted and exported as any, but the group itself is not renamed,
/// changed, ungrouped or emptied away, its members are not taken out of it, nor others put in, and an IFC export leaves
/// it out (only its members are written). The module, when the project opens with it again, finds what was done to its
/// members meanwhile.
/// </summary>
public static class ModuleOwned
{
    /// <summary>On an entity of a platform class: the id of the module that owns it.</summary>
    public const string Key = "ownerModule";

    /// <summary>The module an entity says owns it, if any.</summary>
    public static string? Of(Entity e) => e.Str(Key);
}

/// <summary>
/// The platform's guard for what modules this edition lacks own (<see cref="ModuleOwned"/>): registered after every
/// module, it drives each entity owned by an absent module and every member of such a group.
/// </summary>
public sealed class AbsentModules(IReadOnlySet<string> loaded) : IDrivenElements, IIfcExportFilter
{
    /// <summary>Whether this entity is owned by a module the edition does not have.</summary>
    public bool Kept(Entity e) => ModuleOwned.Of(e) is { } m && !loaded.Contains(m);

    /// <summary>The group an element is in when that group is kept for an absent module.</summary>
    Entity? KeptGroup(IModelView m, Entity e) => Groups.Of(m, e.Id) is { } g && Kept(g) ? g : null;

    public bool Drives(IModelView m, Entity e) => Kept(e) || KeptGroup(m, e) != null;

    public bool SkipsGroup(IModelView model, Entity group) => Kept(group);

    static string Absent(Entity owned) =>
        $"„{owned.Name}” belongs to the module „{ModuleOwned.Of(owned)}”, which this edition does not have: it is kept as it is";

    public void Before(ICommandContext ctx, DrivenEdit edit, DrivenOutcome outcome)
    {
        var m = ctx.Model;
        var entities = edit.Ids.Select(m.Get).OfType<Entity>().ToList();
        var owned = entities.Where(Kept).ToList();
        var members = entities.Where(e => !Kept(e)).ToList();
        switch (edit.Command)
        {
            // Its members are ordinary elements: deleted, moved, copied (a copy is plain), changed, sent to another storey as
            // any. The group stays (element.delete and Groups.DropEmpty keep it, even emptied).
            case "element.delete":
            case "element.transform":
            case "element.copyToStoreys":
            case "element.moveToStorey":
            case "storey.duplicate":
                return;
            case "element.update":
            case "element.rename":
            case "element.setParam":
            case "element.removeParam":
                if (owned.Count > 0) throw new CommandException($"{Absent(owned[0])}.");
                return;
            // Nothing goes into it or out of it, and it is not let go.
            case "group.create":
            case "group.add":
            case "group.remove":
            case "group.ungroup":
            case "component.make":
            case "component.makeUnique":
            {
                var g = owned.FirstOrDefault() ?? KeptGroup(m, members[0])!;
                throw new CommandException($"{Absent(g)}, its elements staying in it. Open it to edit them one by one.");
            }
            default:
                return;
        }
    }
}
