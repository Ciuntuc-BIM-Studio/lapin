using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Engine.Tests;

/// <summary>
/// A project that says nothing of the buckling lengths from αcr (docs/plan-fem.md §14.23: the user's k by default) keeps every
/// member check it had before them ("extend, never break"): every check of every member of the steel hall and of the guide's
/// floor (keys, utilisations, combinations, values, notes) and the checks' notes, hashed, compared with the files written by the
/// code before §14.23 (Golden/). LAPIN_WRITE_GOLDEN=1 writes them again.
/// </summary>
public class BucklingLengthGoldenTests
{
    static string Dir([CallerFilePath] string here = "") => Path.Combine(Path.GetDirectoryName(here)!, "Golden");

    static void Golden(string name, JsonObject actual)
    {
        var path = Path.Combine(Dir(), name + ".json");
        var text = actual.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        if (Environment.GetEnvironmentVariable("LAPIN_WRITE_GOLDEN") == "1") { Directory.CreateDirectory(Dir()); File.WriteAllText(path, text); return; }
        Assert.True(File.Exists(path), $"{path} is missing: LAPIN_WRITE_GOLDEN=1 writes it.");
        Assert.Equal(File.ReadAllText(path), text);
    }

    static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    /// <summary>A check without its combination's key (made of the cases' ids, new at every run).</summary>
    static JsonNode Plain(JsonNode c)
    {
        var o = c.DeepClone().AsObject();
        o.Remove("combination");
        return o;
    }

    /// <summary>Every member's checks in detail, in order of name and content, hashed; the members' count and the notes as they are.</summary>
    internal static JsonObject Digest(Fixture f)
    {
        var all = f.Session.Query("analysis.checks", [])!.AsObject();
        var rows = new List<string>();
        foreach (var sourceId in all["members"]!.AsArray().Select(m => (string)m!["sourceId"]!).Distinct())
        {
            var d = f.Session.Query("analysis.check", new JsonObject { ["id"] = sourceId })!.AsObject();
            foreach (var m in d["members"]!.AsArray().Select(x => x!.AsObject()))
                rows.Add(new JsonObject
                {
                    ["name"] = (string?)m["name"], ["utilisation"] = (double)m["utilisation"]!,
                    ["checks"] = new JsonArray(m["checks"]!.AsArray().Select(c => Plain(c!)).ToArray()), ["notes"] = m["notes"]!.DeepClone(),
                }.ToJsonString());
        }
        rows.Sort(StringComparer.Ordinal);
        return new JsonObject
        {
            ["members"] = rows.Count, ["checks"] = Hash(string.Join("\n", rows)),
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
