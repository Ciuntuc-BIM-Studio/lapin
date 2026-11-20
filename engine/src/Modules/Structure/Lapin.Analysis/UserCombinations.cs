using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis;

/// <summary>A combination the user made (docs/plan-fem.md §13.3): its entity, name, kind and factors on the cases by their ids.</summary>
public sealed record UserCombination(string Id, string Name, string Kind, IReadOnlyList<(string CaseId, double Factor)> Factors)
{
    public string Key => UserCombinations.KeyPrefix + Id;
}

/// <summary>
/// The user's combinations (<c>IfcStructuralLoadGroup</c>, Kind resource, as IFC calls a combination): a name, a kind (uls,
/// sls-characteristic, sls-frequent, sls-quasi, seismic, accidental) and a factor on each case it takes. Their key is
/// <c>user/&lt;id&gt;</c>. Made, changed and removed by analysis.addCombination, updateCombination, removeCombination, one
/// undo step each. A case removed while a combination names it leaves the combination in the model, out of the results, said.
/// </summary>
public static class UserCombinations
{
    public const string Class = "IfcStructuralLoadGroup";
    public const string KeyPrefix = "user/";
    public const string Uls = "uls", SlsCharacteristic = "sls-characteristic", SlsFrequent = "sls-frequent", SlsQuasi = "sls-quasi", Seismic = "seismic", Accidental = "accidental";
    public static readonly string[] Kinds = [Uls, SlsCharacteristic, SlsFrequent, SlsQuasi, Seismic, Accidental];

    /// <summary>The kinds whose combinations are ultimate design situations: the ULS envelope and the design checks.</summary>
    public static bool Ultimate(string kind) => kind is Uls or Accidental;

    public static UserCombination Read(Entity e) => new(e.Id, e.Name, e.Str("kind") is { } k && Kinds.Contains(k) ? k : Uls,
        (e.Attrs["factors"] as JsonArray)?.OfType<JsonObject>()
            .Select(x => ((string?)x["caseId"], Json.Number(x["factor"])))
            .Where(x => x.Item1 != null && x.Item2 is { } v && double.IsFinite(v))
            .Select(x => (x.Item1!, x.Item2!.Value)).ToList() ?? []);

    public static List<UserCombination> All(IModelView m) => m.OfClass(Class).Select(Read).ToList();

    /// <summary>{name?, kind?, factors?: [{caseId, factor}]} over what the combination had (null: a new one), checked: a name, a kind, cases of the project once each.</summary>
    public static (string Name, JsonObject Attrs) Write(JsonObject a, IModelView m, Entity? was)
    {
        var attrs = was?.CloneAttrs() ?? [];
        var name = a.ContainsKey("name") ? ((string?)a["name"])?.Trim() : was?.Name;
        if (string.IsNullOrEmpty(name)) throw new CommandException("Every combination needs a name.");
        if (All(m).Any(c => c.Id != was?.Id && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new CommandException($"There is a combination „{name}” already.");
        var kind = a.ContainsKey("kind") ? (string?)a["kind"] : was?.Str("kind") ?? Uls;
        if (kind == null || !Kinds.Contains(kind)) throw new CommandException($"A combination's kind is {string.Join(", ", Kinds)}.");
        attrs["kind"] = kind;
        if (a.ContainsKey("factors") || was == null)
        {
            if (a["factors"] is not JsonArray list) throw new CommandException("A combination's factors are a list: [{caseId, factor}].");
            var cases = m.OfClass(LoadCases.Class).Select(e => e.Id).ToHashSet();
            var seen = new HashSet<string>();
            var o = new JsonArray();
            foreach (var x in list)
            {
                var caseId = (string?)x?["caseId"];
                if (caseId == null || !cases.Contains(caseId)) throw new CommandException("A combination's factor is on a load case of the project (caseId).");
                if (!seen.Add(caseId)) throw new CommandException("A combination takes each load case once.");
                if (Json.Number(x!["factor"]) is not { } f || !double.IsFinite(f) || Math.Abs(f) > 100) throw new CommandException("A combination's factor is a number (from −100 to 100).");
                if (Math.Abs(f) < 1e-12) continue;
                o.Add((JsonNode)new JsonObject { ["caseId"] = caseId, ["factor"] = f });
            }
            if (o.Count == 0 && kind != Seismic) throw new CommandException("A combination takes at least one load case with a factor.");
            attrs["factors"] = o;
        }
        return (name, attrs);
    }
}
