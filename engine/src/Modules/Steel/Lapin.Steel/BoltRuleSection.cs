using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Steel;

/// <summary>
/// The bolts' rules in a rule pack (docs/plan-user-extensibility.md §5.4): the grades offered (Lapin's own 4.6, 5.6, 8.8 and 10.9,
/// indicative; a project adds its country's, A325, A490…). A grade is a name the drawings and lists write; the check lists nodes of
/// a grade the pack does not offer.
/// </summary>
public sealed class BoltRuleSection : IRuleSection, IRuleCheck
{
    public const string SectionKey = "bolts";
    public string Key => SectionKey;
    public string Label => "Bolts";

    public JsonObject Default => new() { ["grades"] = new JsonArray(NodeParams.Grades.Select(g => (JsonNode)g).ToArray()) };

    public JsonObject Check(JsonNode? section)
    {
        if (section?["grades"] is not JsonArray a) return Default;
        var grades = a.Select(x => ((string?)x)?.Trim()).OfType<string>().Where(g => g.Length is > 0 and <= 20).Distinct().ToList();
        if (grades.Count == 0) throw new CommandException("The rules offer one bolt grade at least.");
        return new JsonObject { ["grades"] = new JsonArray(grades.Select(g => (JsonNode)g).ToArray()) };
    }

    public static IReadOnlyList<string> Grades(IModelView m)
    {
        var s = new BoltRuleSection();
        try { return [.. (s.Check(RulePacks.RawSection(m, SectionKey))["grades"] as JsonArray ?? []).Select(x => (string)x!)]; }
        catch (CommandException) { return NodeParams.Grades; }
    }

    public IEnumerable<RuleFinding> Check(IQueryContext q)
    {
        var grades = Grades(q.Model).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var n in q.Model.OfClass(SteelNodes.Class))
            if (SteelNodes.ParamsOf(n).Grade is { } g && !grades.Contains(g))
                yield return new RuleFinding("steel", n.Id, "info", $"„{n.Name}” has bolts of grade {g}, one the project's rules do not offer.");
    }

    public static void Register(IModuleRegistry r)
    {
        var s = new BoltRuleSection();
        r.Service<IRuleSection>(s);
        r.Service<IRuleCheck>(s);
        // The project's bolt grades to offer.
        r.Query("boltGrades", (q, _) => new JsonObject { ["grades"] = new JsonArray(Grades(q.Model).Select(g => (JsonNode)g).ToArray()) });
    }
}
