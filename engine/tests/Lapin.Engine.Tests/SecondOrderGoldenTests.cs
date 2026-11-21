using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Engine.Tests;

/// <summary>
/// A project with none of S5's options (docs/plan-fem.md §14: no second order, no springs, no tension- or compression-only
/// member or support) keeps exactly the results it had before S5 ("extend, never break"): the guide's floor with the site's
/// snow, wind and seismic parameters, and the steel hall with its bracing, compared with JSON written by the code before S5
/// (Golden/). LAPIN_WRITE_GOLDEN=1 writes them again.
/// </summary>
public class SecondOrderGoldenTests
{
    static string Dir([CallerFilePath] string here = "") => Path.Combine(Path.GetDirectoryName(here)!, "Golden");

    /// <summary>The ids (new at every run) taken out of a result.</summary>
    static JsonNode NoIds(JsonNode n)
    {
        if (n is JsonObject o)
            foreach (var k in o.Select(x => x.Key).ToList())
            {
                if (k is "frame" or "node" or "surface" or "sourceId" or "id" or "key" or "combinations" or "combination") o.Remove(k);
                else if (o[k] is { } v) NoIds(v);
            }
        else if (n is JsonArray a) foreach (var x in a) if (x != null) NoIds(x);
        return n;
    }

    static void Golden(string name, JsonObject actual)
    {
        var path = Path.Combine(Dir(), name + ".json");
        var text = actual.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        if (Environment.GetEnvironmentVariable("LAPIN_WRITE_GOLDEN") == "1") { Directory.CreateDirectory(Dir()); File.WriteAllText(path, text); return; }
        Assert.True(File.Exists(path), $"{path} is missing: LAPIN_WRITE_GOLDEN=1 writes it.");
        Assert.Equal(File.ReadAllText(path), text);
    }

    static void Climate(Fixture f) =>
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0 }, ["wind"] = new JsonObject { ["qb"] = 0.5, ["terrain"] = "III" } });

    /// <summary>A result's frames and reactions in order of name and place (the ids differ at every run), its maxima.</summary>
    static JsonObject Digest(JsonObject r, bool hashFrames = false)
    {
        var frames = r["frames"]!.AsArray().Select(x => x!.AsObject()).OrderBy(x => (string?)x["name"], StringComparer.Ordinal).ThenBy(x => x["stations"]!.ToJsonString(), StringComparer.Ordinal)
            .Select(x => (JsonNode)new JsonObject { ["name"] = (string?)x["name"], ["stations"] = x["stations"]!.DeepClone(), ["extremes"] = x["extremes"]!.DeepClone(), ["deflection"] = NoIds(x["deflection"]!.DeepClone()) }).ToArray();
        var reactions = r["reactions"]!.AsArray().Select(x => x!.AsObject()).OrderBy(x => x["at"]!.ToJsonString(), StringComparer.Ordinal)
            .Select(x => (JsonNode)new JsonObject { ["at"] = x["at"]!.DeepClone(), ["f"] = x["f"]?.DeepClone(), ["min"] = x["min"]?.DeepClone(), ["max"] = x["max"]?.DeepClone() }).ToArray();
        JsonNode framesNode = new JsonArray(frames);
        if (hashFrames) framesNode = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(framesNode.ToJsonString())));
        return new JsonObject { ["frames"] = framesNode, ["reactions"] = new JsonArray(reactions), ["maxima"] = NoIds(r["maxima"]!.DeepClone()) };
    }

    static JsonArray Checks(Fixture f) =>
        new(f.Session.Query("analysis.checks", [])!.AsObject()["members"]!.AsArray().OrderBy(m => (string)m!["name"]!, StringComparer.Ordinal).ThenBy(m => (double)m!["utilisation"]!)
            .Select(m => (JsonNode)new JsonObject { ["name"] = (string?)m!["name"], ["utilisation"] = (double)m["utilisation"]! }).ToArray());

    [Fact]
    public void The_guide_floor_without_S5s_options_keeps_its_results_modes_and_checks()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        Climate(f);
        f.Run("analysis.setSeismic", new JsonObject { ["ag"] = 0.30, ["tc"] = 0.7, ["q"] = 3 });
        var o = new JsonObject
        {
            ["uls"] = Digest(f.Session.Query("analysis.results", new JsonObject { ["envelope"] = "uls" })!.AsObject()),
            ["sls"] = Digest(f.Session.Query("analysis.results", new JsonObject { ["envelope"] = "sls" })!.AsObject()),
            ["case"] = Digest(f.Session.Query("analysis.results", new JsonObject { ["case"] = "Q-A" })!.AsObject()),
            ["modes"] = new JsonArray(f.Session.Query("analysis.modal", [])!.AsObject()["modes"]!.AsArray().Take(6).Select(m => (JsonNode)Math.Round((double)m!["T"]!, 9)).ToArray()),
            ["base"] = f.Session.Query("analysis.seismic", [])!.AsObject()["base"]!.DeepClone(),
            ["checks"] = Checks(f),
        };
        Golden("s5-off-guide-floor", o);
    }

    [Fact]
    public void The_steel_hall_without_S5s_options_keeps_its_results_and_checks()
    {
        var f = AnalysisRunTests.Hall();
        Climate(f);
        var o = new JsonObject
        {
            ["uls"] = Digest(f.Session.Query("analysis.results", new JsonObject { ["envelope"] = "uls" })!.AsObject(), hashFrames: true),
            ["checks"] = Checks(f),
        };
        Golden("s5-off-steel-hall", o);
    }
}
