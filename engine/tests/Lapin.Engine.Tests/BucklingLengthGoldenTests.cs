using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Engine.Tests;

/// <summary>
/// A project that says nothing of the buckling lengths from αcr (docs/plan-fem.md §14.24: the user's k by default) keeps every
/// member check it had before them ("extend, never break"): every check of every member of the steel hall and of the guide's
/// floor (keys, utilisations, combinations, values, notes) and the checks' notes, compared with the files written by the code
/// before §14.24 (Golden/) within the floating-point noise between machines (§14.23). LAPIN_WRITE_GOLDEN=1 writes them again.
/// </summary>
public class BucklingLengthGoldenTests
{
    static void Golden(string name, JsonObject actual) => GoldenFile.Check(name, actual, indented: false);

    /// <summary>
    /// A check without what a tie decides: its combination's key (made of the cases' ids, new at every run), the governing
    /// combination's name, where along the member it governs, and the values that go with that combination. On a symmetric
    /// model twin combinations (wind +X and −X) give a member the same utilisation, and the machine's floating-point noise
    /// picks either (CI's x64 picked the other one on the guide floor); the utilisation, the verdict, the clause and the note
    /// stay compared.
    /// </summary>
    static JsonNode Plain(JsonNode c)
    {
        var o = c.DeepClone().AsObject();
        foreach (var k in new[] { "combination", "combinationName", "x", "values" }) o.Remove(k);
        return o;
    }

    /// <summary>
    /// Every member's checks in detail, in order of name (then utilisation, then content), and the checks' notes as they are;
    /// compared within the machine's floating-point noise (<see cref="GoldenFile"/>, docs/plan-fem.md §14.23).
    /// </summary>
    internal static JsonObject Digest(Fixture f)
    {
        var all = f.Session.Query("analysis.checks", [])!.AsObject();
        var rows = new List<JsonObject>();
        foreach (var sourceId in all["members"]!.AsArray().Select(m => (string)m!["sourceId"]!).Distinct())
        {
            var d = f.Session.Query("analysis.check", new JsonObject { ["id"] = sourceId })!.AsObject();
            foreach (var m in d["members"]!.AsArray().Select(x => x!.AsObject()))
                rows.Add(new JsonObject
                {
                    ["name"] = (string?)m["name"], ["utilisation"] = (double)m["utilisation"]!,
                    ["checks"] = new JsonArray(m["checks"]!.AsArray().Select(c => Plain(c!)).ToArray()), ["notes"] = m["notes"]!.DeepClone(),
                });
        }
        var ordered = rows
            .OrderBy(r => (string?)r["name"], StringComparer.Ordinal)
            .ThenBy(r => Math.Round((double)r["utilisation"]!, 4))
            .ThenBy(r => r.ToJsonString(), StringComparer.Ordinal)
            .Select(r => (JsonNode)r).ToArray();
        return new JsonObject
        {
            ["members"] = rows.Count, ["rows"] = new JsonArray(ordered),
            ["notes"] = all["notes"]!.DeepClone(),
        };
    }

    [Fact]
    public void The_steel_halls_checks_are_unchanged_without_the_option()
    {
        var f = AnalysisRunTests.Hall();
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0 }, ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "III" } });
        Golden("lcr-off-steel-hall", Digest(f));
    }

    [Fact]
    public void The_guide_floors_checks_are_unchanged_without_the_option()
    {
        var t = AnalysisRunTests.Frame6x4(beamMaterial: "S355");
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0 }, ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "III" } });
        Golden("lcr-off-guide-floor", Digest(f));
    }
}
