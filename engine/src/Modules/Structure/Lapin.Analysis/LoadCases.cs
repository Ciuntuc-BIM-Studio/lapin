using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis;

/// <summary>A load case as read from its entity (docs/plan-fem.md §8.2).</summary>
public sealed record LoadCase(string Id, string Code, string Name, string ActionType, string ActionSource, bool SelfWeight, IReadOnlyList<string> Derives, string? Category, Psi? OwnPsi)
{
    public bool Permanent => ActionType == LoadCases.Permanent;
    public bool Variable => ActionType == LoadCases.Variable;
    public bool Wind => ActionSource == "WIND_W" || Category == Categories.Wind;
    public bool Takes(string derived) => Derives.Contains(derived);

    /// <summary>The ψ factors the combinations use: the case's own, else the table's by its category, else A's (the report says so).</summary>
    public Psi Psi => OwnPsi ?? Categories.TableOf(Category) ?? Categories.Fallback;
}

/// <summary>
/// The project's load cases (<c>IfcStructuralLoadCase</c>, Kind resource): a code unique in the project, the action's type and
/// source as IFC names them, whether it carries the self weight (one case at most), the derived loads it takes (finishes and
/// partitions in one case each, the rooms' imposed loads in one case per category), a variable case's category and its own ψ.
/// The defaults (G1, G2, Q-A, S, W) are made when a project has none, by <c>analysis.defaultCases</c> or by any command that
/// needs a case, in the same undo step.
/// </summary>
public static class LoadCases
{
    public const string Class = "IfcStructuralLoadCase";
    public const string Permanent = "PERMANENT_G", Variable = "VARIABLE_Q", Extraordinary = "EXTRAORDINARY_A";
    public static readonly string[] ActionTypes = [Permanent, Variable, Extraordinary];
    public static readonly string[] ActionSources = ["DEAD_LOAD_G", "COMPLETION_G1", "LIVE_LOAD_Q", "SNOW_S", "WIND_W", "EARTHQUAKE_E", "USERDEFINED"];
    public const string Finishes = "finishes", Partitions = "partitions", Occupancy = "occupancy";
    public static readonly string[] DerivedKinds = [Finishes, Partitions, Occupancy];

    public static LoadCase Read(Entity e) => new(
        e.Id, e.Str("code") ?? e.Name, e.Name, e.Str("actionType") ?? Variable, e.Str("actionSource") ?? "USERDEFINED", e.Bool("selfWeight"),
        (e.Attrs["derives"] as JsonArray)?.Select(x => (string?)x).OfType<string>().Where(DerivedKinds.Contains).Distinct().ToList() ?? [],
        e.Str("category"), ReadPsi(e.Attrs["psi"]));

    static Psi? ReadPsi(JsonNode? n) =>
        n is JsonObject o && Json.Number(o["0"]) is { } a && Json.Number(o["1"]) is { } b && Json.Number(o["2"]) is { } c ? new Psi(a, b, c) : null;

    /// <summary>The project's cases in the order they were made.</summary>
    public static List<LoadCase> All(IModelView m) => m.OfClass(Class).Select(Read).ToList();

    public static LoadCase? Find(IModelView m, string? id) => id != null && m.Get(id) is { Class: Class } e ? Read(e) : null;

    /// <summary>The case taking a derived kind (the rooms' imposed loads: the one of that category).</summary>
    public static LoadCase? Taking(IEnumerable<LoadCase> cases, string kind, string? category = null) =>
        cases.FirstOrDefault(c => c.Takes(kind) && (kind != Occupancy || c.Category == category));

    sealed record Spec(string Code, string Name, string ActionType, string ActionSource, bool SelfWeight, string[] Derives, string? Category);

    static readonly Spec[] Defaults =
    [
        new("G1", "Self weight", Permanent, "DEAD_LOAD_G", true, [], null),
        new("G2", "Finishes and partitions", Permanent, "COMPLETION_G1", false, [Finishes, Partitions], null),
        new("Q-A", "Imposed, category A", Variable, "LIVE_LOAD_Q", false, [Occupancy], "A"),
        new("S", "Snow", Variable, "SNOW_S", false, [], Categories.Snow),
        new("W", "Wind", Variable, "WIND_W", false, [], Categories.Wind),
    ];

    static Entity Make(ICommandContext ctx, Spec s)
    {
        var attrs = new JsonObject { ["code"] = s.Code, ["actionType"] = s.ActionType, ["actionSource"] = s.ActionSource };
        if (s.SelfWeight) attrs["selfWeight"] = true;
        if (s.Derives.Length > 0) attrs["derives"] = new JsonArray(s.Derives.Select(d => (JsonNode)d).ToArray());
        if (s.Category != null) attrs["category"] = s.Category;
        var e = new Entity(ctx.NewId(), Class, s.Name, attrs);
        ctx.Add(e);
        return e;
    }

    /// <summary>The default cases, made only when the project has none; the ids made.</summary>
    public static List<string> EnsureDefaults(ICommandContext ctx) =>
        ctx.Model.OfClass(Class).Any() ? [] : Defaults.Select(s => Make(ctx, s).Id).ToList();

    /// <summary>The case a load goes in when none is named: Q-A, made if missing (all the defaults when the project has no case).</summary>
    public static string DefaultForLoads(ICommandContext ctx)
    {
        EnsureDefaults(ctx);
        if (All(ctx.Model).FirstOrDefault(c => string.Equals(c.Code, "Q-A", StringComparison.OrdinalIgnoreCase)) is { } qa) return qa.Id;
        return Make(ctx, Defaults[2] with { Derives = Taking(All(ctx.Model), Occupancy, "A") == null ? [Occupancy] : [] }).Id;
    }

    /// <summary>The case of a category's rooms (derives occupancy), made if missing: Q-&lt;category&gt;, its code unique.</summary>
    public static string EnsureCategory(ICommandContext ctx, string category)
    {
        EnsureDefaults(ctx);
        var cases = All(ctx.Model);
        if (Taking(cases, Occupancy, category) is { } has) return has.Id;
        var code = Unique(cases, $"Q-{category}");
        return Make(ctx, new Spec(code, $"Imposed, category {category}", Variable, "LIVE_LOAD_Q", false, [Occupancy], category)).Id;
    }

    static string Unique(IReadOnlyList<LoadCase> cases, string code)
    {
        if (!cases.Any(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))) return code;
        for (var i = 2; ; i++) if (!cases.Any(c => string.Equals(c.Code, $"{code}{i}", StringComparison.OrdinalIgnoreCase))) return $"{code}{i}";
    }

    /// <summary>A code of its own for a new case without one: G1, G2… for a permanent one, Q-&lt;category&gt; or Q1… for a variable one, A1… else.</summary>
    static string FreshCode(IReadOnlyList<LoadCase> cases, string actionType, string? category)
    {
        if (actionType == Variable && category != null)
            return Unique(cases, category switch { Categories.Snow => "S", Categories.Wind => "W", _ => $"Q-{category}" });
        var prefix = actionType switch { Permanent => "G", Variable => "Q", _ => "A" };
        for (var i = 1; ; i++) if (!cases.Any(c => string.Equals(c.Code, $"{prefix}{i}", StringComparison.OrdinalIgnoreCase))) return $"{prefix}{i}";
    }

    static string DefaultSource(string actionType, string? category) => actionType switch
    {
        Permanent => "DEAD_LOAD_G",
        Variable => category switch { Categories.Snow => "SNOW_S", Categories.Wind => "WIND_W", _ => "LIVE_LOAD_Q" },
        _ => "USERDEFINED",
    };

    /// <summary>
    /// A case's attributes from a command's arguments over what it had (<paramref name="was"/>; null: a new case), checked:
    /// the code unique, the action's type and source IFC's, the category a variable case's, one case with the self weight,
    /// a derived kind in one case (the rooms' in one a category), ψ between 0 and 1.
    /// </summary>
    public static (string Name, JsonObject Attrs) Write(JsonObject a, IModelView m, Entity? was)
    {
        var others = All(m).Where(c => c.Id != was?.Id).ToList();
        var attrs = was?.CloneAttrs() ?? [];
        var name = a.ContainsKey("name") ? ((string?)a["name"])?.Trim() : was?.Name;
        if (string.IsNullOrEmpty(name)) throw new CommandException("Every load case needs a name.");

        var actionType = a.ContainsKey("actionType") ? (string?)a["actionType"] : was?.Str("actionType");
        if (actionType == null || !ActionTypes.Contains(actionType)) throw new CommandException($"A load case's action is {string.Join(", ", ActionTypes)}.");
        attrs["actionType"] = actionType;

        if (a.ContainsKey("category"))
        {
            if ((string?)a["category"] is { Length: > 0 } c) attrs["category"] = c; else attrs.Remove("category");
        }
        var category = (string?)attrs["category"];
        if (category != null && !Categories.IsCaseCategory(category))
            throw new CommandException($"„{category}” is not a category: one of {string.Join(", ", Categories.All.Select(x => x.Key))}, snow or wind.");
        if (category != null && actionType != Variable) throw new CommandException("Only a variable case has a category.");

        if (a.ContainsKey("actionSource"))
        {
            if ((string?)a["actionSource"] is { Length: > 0 } s) attrs["actionSource"] = s; else attrs.Remove("actionSource");
        }
        var source = (string?)attrs["actionSource"] ?? DefaultSource(actionType, category);
        if (!ActionSources.Contains(source)) throw new CommandException($"A load case's source is {string.Join(", ", ActionSources)}.");
        attrs["actionSource"] = source;

        if (a.ContainsKey("selfWeight"))
        {
            if (a["selfWeight"] is JsonValue sv && sv.TryGetValue<bool>(out var sw) && sw) attrs["selfWeight"] = true; else attrs.Remove("selfWeight");
        }
        if (attrs["selfWeight"] is JsonValue swv && swv.TryGetValue<bool>(out var self) && self)
        {
            if (actionType != Permanent) throw new CommandException("The self weight is a permanent action.");
            if (others.FirstOrDefault(c => c.SelfWeight) is { } sw2) throw new CommandException($"The self weight is in „{sw2.Code}” already: one case carries it.");
        }

        if (a.ContainsKey("derives"))
        {
            if (a["derives"] is JsonArray d && d.Count > 0)
            {
                var kinds = new List<string>();
                foreach (var x in d)
                {
                    var k = (string?)x;
                    if (k == null || !DerivedKinds.Contains(k)) throw new CommandException($"A case derives {string.Join(", ", DerivedKinds)}.");
                    if (!kinds.Contains(k)) kinds.Add(k);
                }
                attrs["derives"] = new JsonArray(kinds.Select(k => (JsonNode)k).ToArray());
            }
            else if (a["derives"] is null or JsonArray) attrs.Remove("derives");
            else throw new CommandException("A case's derived loads are a list: finishes, partitions, occupancy.");
        }
        var derives = (attrs["derives"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [];
        foreach (var k in derives)
        {
            if (k == Occupancy)
            {
                if (actionType != Variable || category == null || Categories.Of(category) == null)
                    throw new CommandException("The rooms' imposed loads go in a variable case of their category of use.");
                if (Taking(others, Occupancy, category) is { } o) throw new CommandException($"The rooms of category {category} are in „{o.Code}” already.");
            }
            else if (Taking(others, k) is { } o) throw new CommandException($"The {k} are in „{o.Code}” already: a derived load goes in one case.");
        }

        if (a.ContainsKey("psi"))
        {
            if (a["psi"] is null) attrs.Remove("psi");
            else if (a["psi"] is JsonObject p)
            {
                var psi = new JsonObject();
                foreach (var k in new[] { "0", "1", "2" })
                {
                    if (Json.Number(p[k]) is not { } v || !(v >= 0 && v <= 1)) throw new CommandException("ψ0, ψ1 and ψ2 are numbers between 0 and 1.");
                    psi[k] = v;
                }
                attrs["psi"] = psi;
            }
            else throw new CommandException("ψ is {\"0\": ψ0, \"1\": ψ1, \"2\": ψ2}.");
        }

        var code = a.ContainsKey("code") ? ((string?)a["code"])?.Trim() : was?.Str("code");
        if (string.IsNullOrEmpty(code)) code = FreshCode(others, actionType, category);
        if (code.Length > 16) throw new CommandException("A load case's code is short: G1, Q-A, S.");
        if (others.Any(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))) throw new CommandException($"There is a load case „{code}” already.");
        attrs["code"] = code;
        return (name, attrs);
    }

    /// <summary>The user's loads in a case.</summary>
    public static IEnumerable<Entity> UserLoadsOf(IModelView m, string caseId) => UserLoads.Classes.SelectMany(m.OfClass).Where(e => e.Str("caseId") == caseId);
}
