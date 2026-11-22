using System.Text.Json.Nodes;

namespace Lapin.Engine.Tests;

/// <summary>
/// A project without combination settings keeps exactly the combinations it had before they became configurable
/// (docs/plan-fem.md §13.1, "extend, never break"): the guide's floor and the steel hall, with the site's snow and wind,
/// compared with JSON written by the code before §13 (Golden/), the numbers within the arch noise (<see cref="GoldenFile"/>,
/// §14.23). LAPIN_WRITE_GOLDEN=1 writes them again.
/// </summary>
public class CombinationGoldenTests
{
    static JsonObject Combos(Fixture f)
    {
        var r = f.Session.Query("analysis.combinations", [])!.AsObject();
        // The cases' ids differ at every run: written by their codes.
        var codes = LoadCaseTests.Cases(f)["cases"]!.AsArray().ToDictionary(c => (string)c!["id"]!, c => (string)c!["code"]!);
        string Key(string k) => codes.Aggregate(k, (s, x) => s.Replace(x.Key, x.Value));
        return new JsonObject
        {
            ["combinations"] = new JsonArray(r["combinations"]!.AsArray().Select(c => (JsonNode)new JsonObject
            {
                ["key"] = Key((string)c!["key"]!), ["kind"] = (string?)c["kind"], ["name"] = (string?)c["name"],
                ["factors"] = new JsonArray(c["factors"]!.AsArray().Select(x => (JsonNode)new JsonObject { ["code"] = (string?)x!["code"], ["factor"] = (double)x["factor"]! }).ToArray()),
            }).ToArray()),
        };
    }

    /// <summary>The ids (new at every run) taken out of a result.</summary>
    static JsonNode NoIds(JsonNode n)
    {
        if (n is JsonObject o)
            foreach (var k in o.Select(x => x.Key).ToList())
            {
                if (k is "frame" or "node" or "surface" or "sourceId") o.Remove(k);
                else if (o[k] is { } v) NoIds(v);
            }
        return n;
    }

    static void Golden(string name, JsonObject actual) => GoldenFile.Check(name, actual);

    static void Climate(Fixture f) =>
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0 }, ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "III" } });

    [Fact]
    public void The_guide_floor_without_settings_keeps_its_combinations_envelope_and_checks()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        Climate(f);
        var o = Combos(f);
        var env = f.Session.Query("analysis.results", new JsonObject { ["envelope"] = "uls" })!.AsObject();
        o["ulsMaxima"] = NoIds(env["maxima"]!.DeepClone());
        var checks = f.Session.Query("analysis.checks", [])!.AsObject();
        o["checks"] = new JsonArray(checks["members"]!.AsArray().OrderBy(m => (string)m!["name"]!, StringComparer.Ordinal).ThenBy(m => (double)m!["utilisation"]!)
            .Select(m => (JsonNode)new JsonObject { ["name"] = (string?)m!["name"], ["utilisation"] = (double)m["utilisation"]! }).ToArray());
        Golden("combinations-guide-floor", o);
    }

    [Fact]
    public void The_steel_hall_without_settings_keeps_its_combinations()
    {
        var f = AnalysisRunTests.Hall();
        Climate(f);
        Golden("combinations-steel-hall", Combos(f));
    }
}
