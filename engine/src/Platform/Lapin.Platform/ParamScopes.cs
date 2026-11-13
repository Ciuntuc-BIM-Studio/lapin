using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform.Documents;

namespace Lapin.Platform;

/// <summary>
/// Where a field or a formula may read beyond its element (decision 550, forum topics #36–#38): a name with a scope before a dot
/// reads that scope's entity, as the element itself would be read — <c>storey.code</c> the storey's parameter „code”,
/// <c>type.FireRating</c> its type's, <c>building.name</c>, <c>site.…</c>, <c>project.number</c> (the project's information and its
/// parameters, Manage → Project information), <c>group.…</c> (the group it is in), <c>assembly.…</c> (the whole it is a part of),
/// <c>host.…</c> (a door's or window's wall, an opening's), and the scopes modules add: <c>room</c> (the room an element stands in; a
/// door's or window's on its interior side), <c>fromRoom</c>, <c>toRoom</c> (a door's or window's rooms, exterior and interior side).
/// Scopes chain (<c>host.storey.code</c>). A parameter of the project's (its information's own parameters) is read by its key
/// alone too, from any element, when the element has nothing of that name: the project's parameters are global.
/// Nothing is stored or cached: every ask reads the model as it is, so a storey's parameter changed is what every tag, formula
/// and schedule reading it shows at once.
/// </summary>
public static class ParamScopes
{
    /// <summary>The scopes every model has, in the order the interface lists them.</summary>
    public static readonly string[] Builtin = ["type", "storey", "building", "site", "project", "group", "assembly", "host"];

    static readonly ConcurrentDictionary<string, Func<IModelView, Entity, Entity?>> Added = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A scope of a module's (rooms: <c>room</c>, <c>fromRoom</c>, <c>toRoom</c>); the same name again replaces it.</summary>
    public static void Add(string scope, Func<IModelView, Entity, Entity?> of) => Added[scope] = of;

    /// <summary>Every scope known now: the built-in ones, then the modules'.</summary>
    public static IEnumerable<string> Names => Builtin.Concat(Added.Keys.Order(StringComparer.OrdinalIgnoreCase));

    public static bool IsScope(string word) => Builtin.Contains(word, StringComparer.OrdinalIgnoreCase) || Added.ContainsKey(word);

    /// <summary>The entity a scope names for <paramref name="e"/>; null when it has none (a wall in no group, a type's storey).</summary>
    public static Entity? Of(IModelView m, Entity e, string scope)
    {
        switch (scope.ToLowerInvariant())
        {
            case "type": return m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
            case "storey" or "level": return Up(m, e, IfcClass.BuildingStorey);
            case "building": return Up(m, e, IfcClass.Building);
            case "site": return Up(m, e, IfcClass.Site);
            case "project": return Spatial.Project(m);
            case "group": return Groups.Of(m, e.Id);
            case "assembly": return Assemblies.WholeOf(m, e.Id);
            case "host": return HostOf(m, e);
        }
        return Added.TryGetValue(scope, out var of) ? of(m, e) : null;
    }

    /// <summary>A door's or window's wall (through its opening), an opening's; null for anything else.</summary>
    public static Entity? HostOf(IModelView m, Entity e) =>
        m.RelatingOf(e.Id, IfcClass.RelFillsElement) is { } opening ? m.RelatingOf(opening.Id, IfcClass.RelVoidsElement) ?? opening
        : m.RelatingOf(e.Id, IfcClass.RelVoidsElement);

    /// <summary>
    /// The spatial entity of a class above <paramref name="e"/>: itself when it is one, else up its spatial parents (an element's
    /// storey, a storey's building, a building's site); what is hosted (a door) or a part (of an assembly) by its host or whole.
    /// </summary>
    static Entity? Up(IModelView m, Entity e, string cls)
    {
        var seen = new HashSet<string>();
        for (Entity? x = e; x != null && seen.Add(x.Id);)
        {
            if (x.Class == cls) return x;
            x = Spatial.ParentOf(m, x.Id) ?? HostOf(m, x) ?? m.RelatingOf(x.Id, IfcClass.RelAggregates);
        }
        return null;
    }

    /// <summary>
    /// A field or name split at its scope: <c>storey.code</c> → the storey and „code”. Null when it has no scope before its first
    /// dot, or when the element keeps parameters in a set of that name (a set of the user's called „Site” is still read as one).
    /// <c>Owner</c> is null when the scope is one but the element has none of it (the field is then empty).
    /// </summary>
    public static (Entity? Owner, string Field)? Split(IModelView m, Entity e, string key)
    {
        var dot = key.IndexOf('.');
        if (dot <= 0 || dot == key.Length - 1) return null;
        var scope = key[..dot].Trim();
        if (!IsScope(scope) || HasSet(e, scope)) return null;
        return (Of(m, e, scope), key[(dot + 1)..].Trim());
    }

    static bool HasSet(Entity e, string set) =>
        e.Attrs["params"] is JsonArray list && list.OfType<JsonObject>().Any(p => string.Equals(Params.SetOf(p), set, StringComparison.OrdinalIgnoreCase))
        || e.Attrs["properties"] is JsonObject sets && sets.Any(kv => string.Equals(kv.Key, set, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// What the project's information says for a key: its name, a field (number, client, phase…) or one of its own parameters
    /// (as printed: a date dd.MM.yyyy); null when it has none of that key.
    /// </summary>
    public static string? ProjectValue(IModelView m, string key)
    {
        var k = key.Trim();
        if (k.Equals("name", StringComparison.OrdinalIgnoreCase)) return Spatial.Project(m)?.Name;
        if (ProjectInfo.Fields.FirstOrDefault(f => f.Key.Equals(k, StringComparison.OrdinalIgnoreCase)) is { Key: not null } field) return ProjectInfo.Get(m, field.Key) ?? "";
        var squashed = TagText.Squash(k);
        return ProjectInfo.Customs(m).FirstOrDefault(c => c.Key.Equals(k, StringComparison.OrdinalIgnoreCase) || TagText.Squash(c.Label) == squashed || TagText.Squash(c.Key) == squashed) is { } c ? c.Printed ?? "" : null;
    }

    /// <summary>A project parameter of that key as a formula's value: a number one as its number, any other as its text.</summary>
    public static XValue? ProjectFormulaValue(IModelView m, string key)
    {
        var squashed = TagText.Squash(key);
        if (ProjectInfo.Customs(m).FirstOrDefault(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase) || TagText.Squash(c.Key) == squashed) is { } c)
            return c.Type == "number" && c.Value is { } v && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n)
                ? new XValue(n, c.Printed) : XValue.Read(c.Printed ?? "");
        return ProjectValue(m, key) is { } s ? XValue.Read(s) : null;
    }

    /// <summary>The project's own parameters (Manage → Project information), read by their key alone from any element.</summary>
    public static IEnumerable<ProjectInfo.Custom> Globals(IModelView m) => ProjectInfo.Customs(m);
}
