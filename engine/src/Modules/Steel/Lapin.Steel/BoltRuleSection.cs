using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Steel;

/// <summary>
/// The bolts' rules in a rule pack (docs/plan-user-extensibility.md §5.4): the grades offered (Lapin's own 4.6, 5.6, 8.8 and 10.9,
/// indicative; a project adds its country's, A325, A490…) and the holes' clearance by the bolt's diameter (EN 1090-2 Table 11's,
/// indicative: 1 mm up to M14, 2 mm up to M24, 3 mm beyond). A grade is a name the drawings and lists write; the check lists nodes of
/// a grade the pack does not offer.
/// </summary>
public sealed class BoltRuleSection : IRuleSection, IRuleCheck
{
    public const string SectionKey = "bolts";
    public string Key => SectionKey;
    public string Label => "Bolts";

    public JsonObject Default => new()
    {
        ["grades"] = new JsonArray(NodeParams.Grades.Select(g => (JsonNode)g).ToArray()),
        ["holeClearance"] = new JsonArray(NodeSpec.Clearances.Select(c => (JsonNode)(c.UpTo is { } u ? new JsonObject { ["upTo"] = u, ["clearance"] = c.Clearance } : new JsonObject { ["clearance"] = c.Clearance })).ToArray()),
    };

    public JsonObject Check(JsonNode? section)
    {
        var d = Default;
        var grades = NodeParams.Grades.ToList();
        if (section?["grades"] is JsonArray a)
        {
            grades = a.Select(x => ((string?)x)?.Trim()).OfType<string>().Where(g => g.Length is > 0 and <= 20).Distinct().ToList();
            if (grades.Count == 0) throw new CommandException("The rules offer one bolt grade at least.");
        }
        var holes = (JsonArray)d["holeClearance"]!.DeepClone();
        if (section?["holeClearance"] is JsonArray h)
        {
            if (h.Count is 0 or > 12) throw new CommandException("The holes' clearance is one to twelve steps.");
            holes = [];
            double? last = null;
            for (var i = 0; i < h.Count; i++)
            {
                if (h[i] is not JsonObject s || Json.Number(s["clearance"]) is not { } c || !(c >= 0 && c <= 10)) throw new CommandException("Each step of the holes' clearance is 0 to 10 mm.");
                var step = new JsonObject { ["clearance"] = c };
                if (i < h.Count - 1)
                {
                    var up = Json.Number(s["upTo"]) ?? throw new CommandException("Each step of the holes' clearance but the last says up to what bolt (mm).");
                    if (!(up > 0 && up <= 100) || (last is { } l && up <= l)) throw new CommandException("The holes' clearance steps go up, each above the one before.");
                    step["upTo"] = up;
                    last = up;
                }
                holes.Add((JsonNode)step);
            }
        }
        return new JsonObject { ["grades"] = new JsonArray(grades.Select(g => (JsonNode)g).ToArray()), ["holeClearance"] = holes };
    }

    /// <summary>The holes' clearances the project's rules give (mm by bolt diameter, mm); EN 1090-2 Table 11's by default.</summary>
    public static IReadOnlyList<(double? UpTo, double Clearance)> Clearances(IModelView m)
    {
        JsonObject r;
        try { r = new BoltRuleSection().Check(RulePacks.RawSection(m, SectionKey)); }
        catch (CommandException) { return NodeSpec.Clearances; }
        return [.. (r["holeClearance"] as JsonArray ?? []).OfType<JsonObject>().Select(s => (Json.Number(s["upTo"]), Json.Number(s["clearance"]) ?? 2))];
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
