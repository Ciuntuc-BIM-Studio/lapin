using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Groups (docs/groups.md): elements kept together, IFC's own way, an <c>IfcGroup</c> assigned its members by
/// <c>IfcRelAssignsToGroup</c>. An element is in one group at most and groups do not nest. An imported file
/// comes in as one (named after the file). Selected, a group is its members: deleted, moved, copied
/// (the copies make a group of their own) or rotated as one; opened (edit mode in the interface), its members
/// are picked one by one. A group without members is not kept.
/// </summary>
public static class Groups
{
    public const string Class = "IfcGroup";
    public const string Rel = "IfcRelAssignsToGroup";

    public static IEnumerable<Entity> All(IModelView m) => m.OfClass(Class);

    /// <summary>The group an element is in, if any.</summary>
    public static Entity? Of(IModelView m, string id) => m.RelatingOf(id, Rel) is { } g && m.Get(g.Id) is { Class: Class } x ? x : null;

    public static IEnumerable<string> MembersOf(IModelView m, string groupId) => m.RelatedOf(groupId, Rel).Select(e => e.Id);

    /// <summary>The ids given with each group among them as its members (the groups apart).</summary>
    public static (List<string> Elements, List<string> Groups) Expand(IModelView m, IEnumerable<string> ids)
    {
        var elements = new List<string>();
        var groups = new List<string>();
        var seen = new HashSet<string>();
        foreach (var id in ids)
        {
            if (m.Get(id) is { Class: Class })
            {
                if (!groups.Contains(id)) groups.Add(id);
                foreach (var x in MembersOf(m, id)) if (seen.Add(x)) elements.Add(x);
            }
            else if (seen.Add(id)) elements.Add(id);
        }
        return (elements, groups);
    }

    /// <summary>Whether an entity may be in a group: something placed (not the spatial structure, a type, a resource, a view or a link's).</summary>
    public static bool Groupable(IModelView m, Entity e) =>
        e.Class is not (Class or IfcClass.Project or IfcClass.Site or IfcClass.Building or IfcClass.BuildingStorey or IfcClass.OpeningElement)
        && Spatial.StoreyOf(m, e.Id) != null && e.Str(Links.Key) == null;

    /// <summary>A new group of <paramref name="ids"/> (those that may be in one; each leaves the group it was in, which goes when emptied). Null when none may.</summary>
    public static Entity? Make(IEdit edit, string name, IEnumerable<string> ids)
    {
        var members = ids.Distinct().Select(edit.Model.Get).OfType<Entity>().Where(e => Groupable(edit.Model, e)).ToList();
        if (members.Count == 0) return null;
        var left = members.Select(e => Of(edit.Model, e.Id)?.Id).OfType<string>().Distinct().ToList();
        var group = new Entity(edit.NewId(), Class, name, []);
        edit.Add(group);
        edit.RelateAll(Rel, group.Id, members.Select(e => e.Id));
        DropEmpty(edit, left);
        return group;
    }

    /// <summary>
    /// <paramref name="apply"/> done, and what it placed anew made one group named <paramref name="name"/> (an imported
    /// file's elements: docs/groups.md). What it only updated keeps the group it had.
    /// </summary>
    public static Entity? Around(IEdit edit, string name, Action<IEdit> apply)
    {
        var before = edit.Model.Entities.Select(e => e.Id).ToHashSet();
        apply(edit);
        return Make(edit, name, edit.Model.Entities.Where(e => !before.Contains(e.Id)).Select(e => e.Id).ToList());
    }

    /// <summary>The groups given that have no members left, taken out.</summary>
    public static void DropEmpty(IEdit edit, IEnumerable<string> groupIds)
    {
        foreach (var g in groupIds.Distinct())
            if (edit.Model.Get(g) is { Class: Class } && !MembersOf(edit.Model, g).Any()) edit.Remove(g);
    }

    /// <summary>A name not yet taken: "Group 1", "Group 2"…</summary>
    public static string NextName(IModelView m)
    {
        var taken = All(m).Select(g => g.Name).ToHashSet();
        for (var i = 1; ; i++) if (!taken.Contains($"Group {i}")) return $"Group {i}";
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Group", [], "#888888", Kind: "group"));

        // A group of the elements given (groups among them are dissolved into it).
        r.Command("group.create", "Group", (ctx, a) =>
        {
            var (elements, groups) = Expand(ctx.Model, Args.Ids(a));
            if (elements.Select(ctx.Model.Get).OfType<Entity>().FirstOrDefault(e => e.Str(Links.Key) != null) is { } linked)
                throw new CommandException($"„{linked.Name}” belongs to a link: convert the link to native to group its elements.");
            var name = (string?)a["name"] is { Length: > 0 } n ? n : NextName(ctx.Model);
            var group = Make(ctx, name, elements) ?? throw new CommandException("Select elements placed on a storey to group them.");
            DropEmpty(ctx, groups);
            return new JsonObject { ["id"] = group.Id, ["members"] = MembersOf(ctx.Model, group.Id).Count() };
        });

        // The groups given let go: their members stay, on their own.
        r.Command("group.ungroup", "Ungroup", (ctx, a) =>
        {
            var ids = Args.Ids(a).Where(id => ctx.Model.Get(id) is { Class: Class }).ToList();
            if (ids.Count == 0) throw new CommandException("Select a group to ungroup it.");
            var members = ids.SelectMany(id => MembersOf(ctx.Model, id)).ToList();
            foreach (var id in ids) ctx.Remove(id);
            return new JsonObject { ["ids"] = new JsonArray([.. members.Select(x => (JsonNode)x)]) };
        });

        // Elements taken out of their group (a group left empty goes).
        r.Command("group.remove", "Remove from group", (ctx, a) =>
        {
            var left = new List<string>();
            foreach (var id in Args.Ids(a))
                if (Of(ctx.Model, id) is { } g) { left.Add(g.Id); ctx.Unrelate(Rel, id); }
            if (left.Count == 0) throw new CommandException("None of these is in a group.");
            DropEmpty(ctx, left);
            return null;
        });

        // Elements put into a group (out of any other).
        r.Command("group.add", "Add to group", (ctx, a) =>
        {
            var group = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } g ? g : throw new CommandException("There is no such group.");
            var left = new List<string>();
            var adding = new List<string>();
            foreach (var id in Args.Ids(a))
            {
                if (ctx.Model.Get(id) is not { } e || !Groupable(ctx.Model, e)) continue;
                if (Of(ctx.Model, id) is { } was && was.Id != group.Id) left.Add(was.Id);
                adding.Add(id);
            }
            if (adding.Count == 0) throw new CommandException("Select elements placed on a storey to add them.");
            ctx.RelateAll(Rel, group.Id, adding);
            DropEmpty(ctx, left);
            return null;
        });
    }
}
