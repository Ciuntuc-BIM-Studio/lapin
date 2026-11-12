using System.Text.Json.Nodes;

namespace Lapin.Engine.Tests;

/// <summary>
/// The combinations (docs/plan-fem.md §8.5), EN 1990 Annex A1 with its recommended values: with G1, G2, Q-A, S and W each
/// holding a load, the fundamental combination with each variable case leading (1.35 G + 1.5 Q1 + 1.5 ψ0 Qi), the permanent
/// cases alone, the wind with the permanent ones favourable, and the characteristic, frequent and quasi-permanent ones;
/// keys stable, zero factors left out, an empty case left out and said.
/// </summary>
public class CombinationTests
{
    static (Fixture F, Dictionary<string, string> Ids) Loaded(params string[] codes)
    {
        var f = LoadCaseTests.F();
        f.Run("analysis.defaultCases", []);
        var ids = new[] { "G1", "G2", "Q-A", "S", "W" }.ToDictionary(c => c, c => LoadCaseTests.IdOf(f, c));
        foreach (var code in codes)
            f.Run("analysis.addAreaLoad", new JsonObject
            {
                ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(1, 0), new JsonArray(1, 1), new JsonArray(0, 1)),
                ["value"] = 1, ["caseId"] = ids[code],
            });
        return (f, ids);
    }

    static (Dictionary<string, JsonObject> ByKey, List<string> Keys, List<string> Notes) Combinations(Fixture f)
    {
        var r = f.Session.Query("analysis.combinations", [])!.AsObject();
        var list = r["combinations"]!.AsArray().Select(c => c!.AsObject()).ToList();
        return (list.ToDictionary(c => (string)c["key"]!), list.Select(c => (string)c["key"]!).ToList(), r["notes"]!.AsArray().Select(n => (string)n!).ToList());
    }

    static Dictionary<string, double> Factors(JsonObject c) => c["factors"]!.AsArray().ToDictionary(x => (string)x!["code"]!, x => (double)x!["factor"]!);

    [Fact]
    public void Five_cases_make_the_EN_1990_combinations_with_their_factors_and_stable_keys()
    {
        var (f, id) = Loaded("G1", "G2", "Q-A", "S", "W");
        var (c, keys, notes) = Combinations(f);
        Assert.Equal(
        [
            $"uls/{id["Q-A"]}", $"uls/{id["S"]}", $"uls/{id["W"]}", "uls/permanent", "uls/wind-uplift",
            $"slsc/{id["Q-A"]}", $"slsc/{id["S"]}", $"slsc/{id["W"]}",
            $"slsf/{id["Q-A"]}", $"slsf/{id["S"]}", $"slsf/{id["W"]}",
            "slsq",
        ], keys);
        Assert.Equal(5, c.Values.Count(x => (string?)x["kind"] == "uls"));
        Assert.Equal(3, c.Values.Count(x => (string?)x["kind"] == "sls-characteristic"));
        Assert.Equal(3, c.Values.Count(x => (string?)x["kind"] == "sls-frequent"));
        Assert.Equal(1, c.Values.Count(x => (string?)x["kind"] == "sls-quasi"));

        // ULS, 6.10: 1.35 G + 1.5 Q1 + 1.5 ψ0 Qi (ψ0: A 0.7, snow 0.5, wind 0.6).
        var qa = c[$"uls/{id["Q-A"]}"];
        Assert.Equal("1.35 G1 + 1.35 G2 + 1.5 Q-A + 0.75 S + 0.9 W", (string?)qa["name"]);
        Assert.Equal(new Dictionary<string, double> { ["G1"] = 1.35, ["G2"] = 1.35, ["Q-A"] = 1.5, ["S"] = 0.75, ["W"] = 0.9 }, Factors(qa));
        Assert.Equal(id["Q-A"], (string?)qa["factors"]![2]!["caseId"]);
        Assert.Equal("1.35 G1 + 1.35 G2 + 1.05 Q-A + 1.5 S + 0.9 W", (string?)c[$"uls/{id["S"]}"]["name"]);
        Assert.Equal("1.35 G1 + 1.35 G2 + 1.05 Q-A + 0.75 S + 1.5 W", (string?)c[$"uls/{id["W"]}"]["name"]);
        Assert.Equal("1.35 G1 + 1.35 G2", (string?)c["uls/permanent"]["name"]);
        Assert.Equal(new Dictionary<string, double> { ["G1"] = 1.0, ["G2"] = 1.0, ["W"] = 1.5 }, Factors(c["uls/wind-uplift"]));
        Assert.Equal("G1 + G2 + 1.5 W", (string?)c["uls/wind-uplift"]["name"]);

        // SLS characteristic: G + Q1 + ψ0 Qi.
        Assert.Equal("G1 + G2 + Q-A + 0.5 S + 0.6 W", (string?)c[$"slsc/{id["Q-A"]}"]["name"]);
        Assert.Equal("G1 + G2 + 0.7 Q-A + S + 0.6 W", (string?)c[$"slsc/{id["S"]}"]["name"]);
        // SLS frequent: G + ψ1 Q1 + ψ2 Qi (ψ2 of snow and wind 0: left out).
        Assert.Equal(new Dictionary<string, double> { ["G1"] = 1, ["G2"] = 1, ["Q-A"] = 0.5 }, Factors(c[$"slsf/{id["Q-A"]}"]));
        Assert.Equal("G1 + G2 + 0.3 Q-A + 0.2 S", (string?)c[$"slsf/{id["S"]}"]["name"]);
        Assert.Equal("G1 + G2 + 0.3 Q-A + 0.2 W", (string?)c[$"slsf/{id["W"]}"]["name"]);
        // SLS quasi-permanent: G + ψ2 Qi.
        Assert.Equal("G1 + G2 + 0.3 Q-A", (string?)c["slsq"]["name"]);
        Assert.Contains(notes, n => n.StartsWith("Indicative"));

        // Asked again, the same keys and names.
        Assert.Equal(keys, Combinations(f).Keys);
    }

    [Fact]
    public void An_empty_case_is_left_out_and_said()
    {
        var (f, id) = Loaded("G1", "Q-A");
        var (c, keys, notes) = Combinations(f);
        Assert.Equal([$"uls/{id["Q-A"]}", "uls/permanent", $"slsc/{id["Q-A"]}", $"slsf/{id["Q-A"]}", "slsq"], keys);
        Assert.Equal("1.35 G1 + 1.5 Q-A", (string?)c[$"uls/{id["Q-A"]}"]["name"]);
        Assert.Equal("G1 + 0.3 Q-A", (string?)c["slsq"]["name"]);
        foreach (var code in new[] { "G2", "S", "W" }) Assert.Contains(notes, n => n.Contains($"„{code}” holds nothing"));
        // A case's own ψ is the one used.
        f.Run("analysis.updateCase", new JsonObject { ["id"] = id["Q-A"], ["psi"] = new JsonObject { ["0"] = 0.7, ["1"] = 0.5, ["2"] = 0.6 } });
        Assert.Equal("G1 + 0.6 Q-A", (string?)Combinations(f).ByKey["slsq"]["name"]);
    }

    [Fact]
    public void A_variable_case_without_a_category_takes_As_factors_and_the_report_says_so()
    {
        var (f, id) = Loaded("G1");
        var q = (string)f.Run("analysis.addCase", new JsonObject { ["name"] = "Equipment", ["code"] = "Q1", ["actionType"] = "VARIABLE_Q", ["actionSource"] = "USERDEFINED" })!["id"]!;
        f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = f.Storey, ["at"] = new JsonArray(0.5, 0.5), ["value"] = 3, ["caseId"] = q });
        var (c, _, notes) = Combinations(f);
        Assert.Equal("G1 + 0.5 Q1", (string?)c[$"slsf/{q}"]["name"]);
        Assert.Contains(notes, n => n.Contains("„Q1” has no category"));
    }
}
