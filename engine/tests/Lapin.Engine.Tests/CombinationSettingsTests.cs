using System.Text.Json.Nodes;
using Lapin.Analysis;
using Lapin.Analysis.Solver;
using Lapin.Ifc;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The configurable combinations (docs/plan-fem.md §13), the numbers worked by hand: a new project's CR 0-2012 set and an older
/// project's EN (none stored), the CR 0 values, the project's own γ and ψ, 6.10a and 6.10b, a generated combination switched
/// off, the user's combinations in results, envelopes, checks and the IFC structural analysis view, a removed case said,
/// each command one undo step.
/// </summary>
public class CombinationSettingsTests
{
    /// <summary>G1, G2, Q-A, S and W each holding 1 kN/m² on a square metre (as CombinationTests).</summary>
    static (Fixture F, Dictionary<string, string> Id) Loaded(params string[] codes)
    {
        var f = LoadCaseTests.F();
        f.Run("analysis.defaultCases", []);
        var ids = new[] { "G1", "G2", "Q-A", "S", "W" }.ToDictionary(c => c, c => LoadCaseTests.IdOf(f, c));
        foreach (var code in codes.Length == 0 ? ids.Keys.ToArray() : codes)
            f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = f.Storey, ["points"] = AnalysisRunTests.Rect(0, 0, 1, 1), ["value"] = 1, ["caseId"] = ids[code] });
        return (f, ids);
    }

    static JsonObject Q(Fixture f) => f.Session.Query("analysis.combinations", [])!.AsObject();
    static Dictionary<string, JsonObject> ByKey(Fixture f) => Q(f)["combinations"]!.AsArray().Select(c => c!.AsObject()).ToDictionary(c => (string)c["key"]!);
    static string Name(Fixture f, string key) => (string)ByKey(f)[key]["name"]!;
    static JsonObject Options(Fixture f, JsonObject a) { f.Run("analysis.setCombinationOptions", a); return Q(f)["options"]!.AsObject(); }
    static JsonObject Results(Fixture f, JsonObject a) => f.Session.Query("analysis.results", a)!.AsObject();

    [Fact]
    public void A_new_project_has_CR_0_and_one_made_before_has_nothing_stored_so_EN()
    {
        var f = LoadCaseTests.F();
        Assert.Equal("en", (string?)Q(f)["options"]!["set"]);
        Assert.Null(Spatial.Project(f.Model)!.Attrs[CombinationSettings.Key]);
        f.Session.New();
        Assert.Equal("cr0", (string?)Spatial.Project(f.Model)!.Attrs[CombinationSettings.Key]!["set"]);
        Assert.Equal("cr0", (string?)Q(f)["options"]!["set"]);
        // A project another file fills (an IFC opened as a project) is not a new one: nothing written.
        f.Session.New(plain: true);
        Assert.Null(Spatial.Project(f.Model)!.Attrs[CombinationSettings.Key]);
    }

    [Fact]
    public void The_CR_0_set_takes_its_Table_7_1_for_snow_wind_and_roofs()
    {
        var (f, id) = Loaded();
        var o = Options(f, new JsonObject { ["set"] = "cr0" });
        Assert.Equal(1.35, (double)o["gammaGSup"]!);
        Assert.Equal(new[] { 0.7, 0.5, 0.4 }, new[] { "0", "1", "2" }.Select(k => (double)o["psi"]!["snow"]![k]!));
        Assert.Equal(new[] { 0.7, 0.2, 0.0 }, new[] { "0", "1", "2" }.Select(k => (double)o["psi"]!["wind"]![k]!));
        Assert.Equal(0.7, (double)o["psi"]!["H"]!["0"]!);
        // ULS 6.9 (EN's 6.10): 1.35 G + 1.5 Q1 + 1.5 · 0.7 Qi for snow and wind alike.
        Assert.Equal("1.35 G1 + 1.35 G2 + 1.5 Q-A + 1.05 S + 1.05 W", Name(f, $"uls/{id["Q-A"]}"));
        Assert.Equal("1.35 G1 + 1.35 G2 + 1.05 Q-A + 1.5 S + 1.05 W", Name(f, $"uls/{id["S"]}"));
        Assert.Equal("G1 + G2 + 1.5 W", Name(f, "uls/wind-uplift"));
        Assert.Equal("G1 + G2 + Q-A + 0.7 S + 0.7 W", Name(f, $"slsc/{id["Q-A"]}"));
        // Frequent: ψ1 of the snow 0.5, ψ2 of the others (Q-A 0.3, wind 0); quasi-permanent: snow's ψ2 0.4.
        Assert.Equal("G1 + G2 + 0.3 Q-A + 0.5 S", Name(f, $"slsf/{id["S"]}"));
        Assert.Equal("G1 + G2 + 0.3 Q-A + 0.4 S", Name(f, "slsq"));
        Assert.Contains(Q(f)["notes"]!.AsArray(), n => ((string)n!).StartsWith("Indicative: CR 0-2012"));
        // The loads' table shows the ψ the combinations take.
        Assert.Equal(0.7, (double)LoadCaseTests.Cases(f)["cases"]!.AsArray().Single(c => (string?)c!["code"] == "S")!["psi"]!["0"]!);
        // Back to EN: exactly as before.
        Options(f, new JsonObject { ["set"] = null });
        Assert.Equal("1.35 G1 + 1.35 G2 + 1.5 Q-A + 0.75 S + 0.9 W", Name(f, $"uls/{id["Q-A"]}"));
        Assert.Null(Spatial.Project(f.Model)!.Attrs[CombinationSettings.Key]);
    }

    [Fact]
    public void The_projects_own_partial_factors_and_psi_go_over_the_set_and_a_cases_own_psi_over_them()
    {
        var (f, id) = Loaded();
        Options(f, new JsonObject { ["gammaGSup"] = 1.2, ["gammaGInf"] = 0.9, ["gammaQ"] = 1.4 });
        // 1.2 G + 1.4 Q-A + 1.4 · 0.5 S + 1.4 · 0.6 W.
        Assert.Equal("1.2 G1 + 1.2 G2 + 1.4 Q-A + 0.7 S + 0.84 W", Name(f, $"uls/{id["Q-A"]}"));
        Assert.Equal("1.2 G1 + 1.2 G2", Name(f, "uls/permanent"));
        Assert.Equal("0.9 G1 + 0.9 G2 + 1.4 W", Name(f, "uls/wind-uplift"));
        // The snow's ψ0 the project's: 1.4 · 0.6 = 0.84.
        Options(f, new JsonObject { ["psi"] = new JsonObject { ["snow"] = new JsonObject { ["0"] = 0.6, ["1"] = 0.2, ["2"] = 0 } } });
        Assert.Equal("1.2 G1 + 1.2 G2 + 1.4 Q-A + 0.84 S + 0.84 W", Name(f, $"uls/{id["Q-A"]}"));
        // The case's own ψ wins: 1.4 · 0.4 = 0.56.
        f.Run("analysis.updateCase", new JsonObject { ["id"] = id["S"], ["psi"] = new JsonObject { ["0"] = 0.4, ["1"] = 0.2, ["2"] = 0 } });
        Assert.Equal("1.2 G1 + 1.2 G2 + 1.4 Q-A + 0.56 S + 0.84 W", Name(f, $"uls/{id["Q-A"]}"));
        Assert.Contains(Q(f)["notes"]!.AsArray(), n => ((string)n!).Contains("with the project's own factors: γG,sup 1.2, γG,inf 0.9, γQ 1.4"));
        // Refused out of range; null back to the set's.
        Assert.Throws<CommandException>(() => f.Run("analysis.setCombinationOptions", new JsonObject { ["gammaQ"] = 0.5 }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setCombinationOptions", new JsonObject { ["psi"] = new JsonObject { ["Z"] = new JsonObject { ["0"] = 1, ["1"] = 1, ["2"] = 1 } } }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setCombinationOptions", new JsonObject { ["set"] = "bs" }));
        f.Run("analysis.updateCase", new JsonObject { ["id"] = id["S"], ["psi"] = null });
        Options(f, new JsonObject { ["gammaGSup"] = null, ["gammaGInf"] = null, ["gammaQ"] = null, ["psi"] = null });
        Assert.Equal("1.35 G1 + 1.35 G2 + 1.5 Q-A + 0.75 S + 0.9 W", Name(f, $"uls/{id["Q-A"]}"));
    }

    [Fact]
    public void Expressions_6_10a_and_6_10b_replace_6_10_with_xi()
    {
        var (f, id) = Loaded("G1", "Q-A", "S");
        Options(f, new JsonObject { ["expression"] = "6.10ab" });
        var c = ByKey(f);
        // 6.10a: 1.35 G + 1.5 · 0.7 Q-A + 1.5 · 0.5 S.
        Assert.Equal("1.35 G1 + 1.05 Q-A + 0.75 S", (string)c["uls/6.10a"]["name"]!);
        // 6.10b: 0.85 · 1.35 = 1.1475 G with each lead.
        Assert.Equal("1.1475 G1 + 1.5 Q-A + 0.75 S", (string)c[$"uls/{id["Q-A"]}"]["name"]!);
        Assert.Equal("1.1475 G1 + 1.05 Q-A + 1.5 S", (string)c[$"uls/{id["S"]}"]["name"]!);
        Assert.Equal("1.35 G1", (string)c["uls/permanent"]["name"]!);
        Assert.Equal(4, c.Values.Count(x => (string?)x["kind"] == "uls"));
        Options(f, new JsonObject { ["xi"] = 0.9 });
        Assert.Equal("1.215 G1 + 1.5 Q-A + 0.75 S", Name(f, $"uls/{id["Q-A"]}"));
        Assert.Throws<CommandException>(() => f.Run("analysis.setCombinationOptions", new JsonObject { ["xi"] = 0.3 }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setCombinationOptions", new JsonObject { ["expression"] = "6.11" }));
        Options(f, new JsonObject { ["expression"] = null });
        Assert.False(ByKey(f).ContainsKey("uls/6.10a"));
        Assert.Equal("1.35 G1 + 1.5 Q-A + 0.75 S", Name(f, $"uls/{id["Q-A"]}"));
    }

    [Fact]
    public void A_generated_combination_switched_off_is_listed_but_gone_from_the_run_and_the_envelopes_in_one_undo_step()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        var qa = LoadCaseTests.IdOf(f, "Q-A");
        var key = $"uls/{qa}";
        Assert.Contains(Results(f, new JsonObject { ["envelope"] = "uls" })["combinations"]!.AsArray(), x => (string?)x == key);
        f.Run("analysis.setCombinationEnabled", new JsonObject { ["key"] = key, ["enabled"] = false });
        Assert.False((bool)ByKey(f)[key]["enabled"]!);
        Assert.DoesNotContain(AnalysisRunTests.Run(f)["combinations"]!.AsArray(), x => (string?)x!["key"] == key);
        var env = Results(f, new JsonObject { ["envelope"] = "uls" });
        Assert.Equal(["uls/permanent"], env["combinations"]!.AsArray().Select(x => (string)x!).ToArray());
        Assert.Throws<CommandException>(() => Results(f, new JsonObject { ["combination"] = key }));
        Assert.Contains(Q(f)["notes"]!.AsArray(), n => (string?)n == "1 combination switched off: left out of the results, the envelopes, the checks and the export.");
        // The checks govern by what is left.
        var checks = f.Session.Query("analysis.checks", [])!.AsObject();
        Assert.All(checks["members"]!.AsArray(), m => Assert.NotEqual(key, (string?)m!["governing"]?["combination"]));
        f.Session.Undo();
        Assert.True((bool)ByKey(f)[key]["enabled"]!);
        Assert.Null(Spatial.Project(f.Model)!.Attrs[CombinationSettings.Key]);
    }

    [Fact]
    public void A_users_combination_takes_part_in_the_results_the_envelope_and_the_checks()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        var (g1, qa) = (LoadCaseTests.IdOf(f, "G1"), LoadCaseTests.IdOf(f, "Q-A"));
        var made = f.Run("analysis.addCombination", new JsonObject
        {
            ["name"] = "Heavy", ["kind"] = "uls",
            ["factors"] = new JsonArray(new JsonObject { ["caseId"] = g1, ["factor"] = 2.0 }, new JsonObject { ["caseId"] = qa, ["factor"] = 3.0 }),
        })!.AsObject();
        var key = (string)made["key"]!;
        Assert.Equal($"user/{made["id"]}", key);
        var c = ByKey(f)[key];
        Assert.Equal("Heavy", (string?)c["name"]);
        Assert.Equal("2 G1 + 3 Q-A", (string?)c["formula"]);
        Assert.Equal("user", (string?)c["source"]);

        // Its results are its factors on the cases' (superposition).
        string beam = t.Beams[0];
        double[] Along(JsonObject a) => Results(f, a)["frames"]!.AsArray().First(x => (string?)x!["sourceId"] == beam)!["stations"]!.AsArray().Select(s => (double)s!["My"]!).ToArray();
        double My(JsonObject a) => (double)Results(f, a)["frames"]!.AsArray().First(x => (string?)x!["sourceId"] == beam)!["extremes"]!["My"]!["max"]!;
        var (along, ag, aq) = (Along(new JsonObject { ["combination"] = key }), Along(new JsonObject { ["case"] = g1 }), Along(new JsonObject { ["case"] = qa }));
        for (var i = 0; i < along.Length; i++) Assert.Equal(2 * ag[i] + 3 * aq[i], along[i], 4);
        var both = My(new JsonObject { ["combination"] = key });
        Assert.Contains(Results(f, new JsonObject { ["envelope"] = "uls" })["combinations"]!.AsArray(), x => (string?)x == key);
        Assert.Equal(both, My(new JsonObject { ["envelope"] = "uls" }), 6);
        // It governs the beams' checks (2 G + 3 Q over 1.35 G + 1.5 Q).
        var checks = f.Session.Query("analysis.checks", [])!.AsObject();
        Assert.Contains(checks["members"]!.AsArray(), m => (string?)m!["governing"]?["combination"] == key);
        // Its duration for timber: its shortest action's (Q-A: medium); a permanent one alone: permanent.
        var (sets, durations, _, _) = AnalysisRun.For(f.Session.Reader).UlsTerms();
        Assert.Equal("medium", durations[sets.FindIndex(s => s.Key == key)]);

        // Characteristic SLS: in the SLS envelope, not the ULS one.
        var sls = (string)f.Run("analysis.addCombination", new JsonObject
        {
            ["name"] = "Service", ["kind"] = "sls-characteristic", ["factors"] = new JsonArray(new JsonObject { ["caseId"] = g1, ["factor"] = 1.0 }),
        })!["key"]!;
        Assert.Contains(Results(f, new JsonObject { ["envelope"] = "sls" })["combinations"]!.AsArray(), x => (string?)x == sls);
        Assert.DoesNotContain(Results(f, new JsonObject { ["envelope"] = "uls" })["combinations"]!.AsArray(), x => (string?)x == sls);

        // Changed, switched off, removed: each one undo step.
        f.Run("analysis.updateCombination", new JsonObject { ["id"] = (string)made["id"]!, ["factors"] = new JsonArray(new JsonObject { ["caseId"] = g1, ["factor"] = 1.1 }) });
        Assert.Equal("1.1 G1", (string?)ByKey(f)[key]["formula"]);
        f.Session.Undo();
        Assert.Equal("2 G1 + 3 Q-A", (string?)ByKey(f)[key]["formula"]);
        f.Run("analysis.setCombinationEnabled", new JsonObject { ["key"] = key, ["enabled"] = false });
        Assert.DoesNotContain(Results(f, new JsonObject { ["envelope"] = "uls" })["combinations"]!.AsArray(), x => (string?)x == key);
        f.Run("analysis.removeCombination", new JsonObject { ["id"] = (string)made["id"]! });
        Assert.False(ByKey(f).ContainsKey(key));
        Assert.Null(Spatial.Project(f.Model)!.Attrs[CombinationSettings.Key]);
        f.Session.Undo();
        Assert.True(ByKey(f).ContainsKey(key));
        f.Session.Undo();
        Assert.True((bool)ByKey(f)[key]["enabled"]!);
    }

    [Fact]
    public void A_users_combination_is_checked_and_one_naming_a_removed_case_is_said_not_dropped()
    {
        var (f, id) = Loaded("G1", "Q-A");
        Assert.Throws<CommandException>(() => f.Run("analysis.addCombination", new JsonObject { ["name"] = "X", ["kind"] = "uls", ["factors"] = new JsonArray() }));
        Assert.Throws<CommandException>(() => f.Run("analysis.addCombination", new JsonObject { ["name"] = "X", ["kind"] = "nope", ["factors"] = new JsonArray(new JsonObject { ["caseId"] = id["G1"], ["factor"] = 1 }) }));
        Assert.Throws<CommandException>(() => f.Run("analysis.addCombination", new JsonObject { ["name"] = "X", ["kind"] = "uls", ["factors"] = new JsonArray(new JsonObject { ["caseId"] = "none", ["factor"] = 1 }) }));
        Assert.Throws<CommandException>(() => f.Run("analysis.addCombination", new JsonObject
        {
            ["name"] = "X", ["kind"] = "uls", ["factors"] = new JsonArray(new JsonObject { ["caseId"] = id["G1"], ["factor"] = 1 }, new JsonObject { ["caseId"] = id["G1"], ["factor"] = 2 }),
        }));
        var made = f.Run("analysis.addCombination", new JsonObject
        {
            ["name"] = "With W", ["kind"] = "uls",
            ["factors"] = new JsonArray(new JsonObject { ["caseId"] = id["G1"], ["factor"] = 1.35 }, new JsonObject { ["caseId"] = id["W"], ["factor"] = 1.5 }),
        })!.AsObject();
        Assert.Throws<CommandException>(() => f.Run("analysis.addCombination", new JsonObject { ["name"] = "with w", ["kind"] = "uls", ["factors"] = new JsonArray(new JsonObject { ["caseId"] = id["G1"], ["factor"] = 1 }) }));
        // W holds nothing: its factor adds nothing, the combination stays.
        Assert.True(ByKey(f)[(string)made["key"]!]["missing"] is null);
        f.Run("analysis.removeCase", new JsonObject { ["id"] = id["W"] });
        var c = ByKey(f)[(string)made["key"]!];
        Assert.Equal([id["W"]], c["missing"]!.AsArray().Select(x => (string)x!).ToArray());
        Assert.Equal("1.35 G1", (string?)c["formula"]);
        Assert.Contains(Q(f)["notes"]!.AsArray(), n => (string?)n == "„With W” names a load case that is gone: left out of the results until its factors are set again.");
        var run = AnalysisRun.Of(f.Session.Reader);
        Assert.DoesNotContain(run.Combinations, x => x.Key == (string)made["key"]!);
    }

    [Fact]
    public void A_users_seismic_combination_is_its_factors_with_AEd_in_the_seismic_and_ULS_envelopes()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 2 });
        var g1 = LoadCaseTests.IdOf(f, "G1");
        var made = f.Run("analysis.addCombination", new JsonObject
        {
            ["name"] = "Seismic, G only", ["kind"] = "seismic", ["factors"] = new JsonArray(new JsonObject { ["caseId"] = g1, ["factor"] = 1.0 }),
        })!.AsObject();
        var id = (string)made["id"]!;
        // Without the seismic parameters: not in the ULS envelope, said.
        Assert.DoesNotContain(Results(f, new JsonObject { ["envelope"] = "uls" })["combinations"]!.AsArray(), x => ((string)x!).StartsWith("seismic/"));
        Assert.Contains(Q(f)["notes"]!.AsArray(), n => ((string)n!).StartsWith("„Seismic, G only” is a seismic combination"));
        f.Run("analysis.setSeismic", new JsonObject { ["ag"] = 0.30, ["tc"] = 0.7, ["q"] = 3 });
        var env = Results(f, new JsonObject { ["envelope"] = "seismic" })["combinations"]!.AsArray().Select(x => (string)x!).ToArray();
        Assert.Equal(["seismic/+", "seismic/-", $"seismic/{id}/+", $"seismic/{id}/-"], env);
        Assert.Contains(Results(f, new JsonObject { ["envelope"] = "uls" })["combinations"]!.AsArray(), x => (string?)x == $"seismic/{id}/+");
        // Its own results: G1 ± AEd; its axial force in a column between G1's ± the seismic action's.
        var col = t.Columns[0];
        double N(JsonObject a, string m) => (double)Results(f, a)["frames"]!.AsArray().First(x => (string?)x!["sourceId"] == col)!["extremes"]!["N"]![m]!;
        var gN = N(new JsonObject { ["case"] = g1 }, "max");
        var (hi, lo) = (N(new JsonObject { ["combination"] = $"user/{id}" }, "max"), N(new JsonObject { ["combination"] = $"user/{id}" }, "min"));
        Assert.True(hi > gN - 1e-9 && lo < gN + 1e-9, $"{lo} ≤ {gN} ≤ {hi}");
        // Not written to the IFC structural analysis view (AEd is no load case).
        Assert.DoesNotContain(Export(f).OfType("IFCSTRUCTURALLOADGROUP"), g => (string?)g[3] == $"user/{id}");
    }

    [Fact]
    public void The_settings_and_the_users_combinations_survive_the_project_saved_and_opened_again()
    {
        var (f, id) = Loaded("G1", "Q-A");
        Options(f, new JsonObject { ["set"] = "cr0", ["gammaQ"] = 1.6 });
        f.Run("analysis.setCombinationEnabled", new JsonObject { ["key"] = "uls/permanent", ["enabled"] = false });
        var key = (string)f.Run("analysis.addCombination", new JsonObject
        {
            ["name"] = "Mine", ["kind"] = "sls-quasi", ["factors"] = new JsonArray(new JsonObject { ["caseId"] = id["G1"], ["factor"] = 1.0 }, new JsonObject { ["caseId"] = id["Q-A"], ["factor"] = 0.3 }),
        })!["key"]!;
        var before = Q(f).ToJsonString();
        using var ms = new MemoryStream();
        Lapin.Protocol.ProjectPackage.Write(f.Session, ms);
        ms.Position = 0;
        var content = Lapin.Package.LapinPackage.Read(ms);
        var g = LoadCaseTests.F();
        g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Assert.Equal(before, Q(g).ToJsonString());
        Assert.Equal("G1 + 0.3 Q-A", (string?)ByKey(g)[key]["formula"]);
    }

    static StepFile Export(Fixture f)
    {
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "frame.ifc", new IfcExportOptions("IFC4", Structural: true));
        return StepFile.Read(ms.ToArray());
    }

    [Fact]
    public void The_structural_analysis_view_writes_the_users_combinations_and_leaves_out_those_switched_off()
    {
        var t = AnalysisRunTests.Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        var (g1, qa) = (LoadCaseTests.IdOf(f, "G1"), LoadCaseTests.IdOf(f, "Q-A"));
        var key = (string)f.Run("analysis.addCombination", new JsonObject
        {
            ["name"] = "Accidental, impact", ["kind"] = "accidental",
            ["factors"] = new JsonArray(new JsonObject { ["caseId"] = g1, ["factor"] = 1.0 }, new JsonObject { ["caseId"] = qa, ["factor"] = 0.5 }),
        })!["key"]!;
        f.Run("analysis.setCombinationEnabled", new JsonObject { ["key"] = "uls/permanent", ["enabled"] = false });
        var step = Export(f);
        var groups = step.OfType("IFCSTRUCTURALLOADGROUP").Where(g => (g[5] as StepEnum)?.Value == "LOAD_COMBINATION").ToList();
        var mine = groups.Single(g => (string?)g[3] == key);
        Assert.Equal("Accidental, impact", (string?)mine[2]);
        Assert.Equal("accidental", (string?)mine[9]);
        var factors = step.OfType("IFCRELASSIGNSTOGROUPBYFACTOR").Where(r => r[6] is StepRef sr && sr.Id == mine.Id).Select(r => (double)r[7]!).Order().ToArray();
        Assert.Equal([0.5, 1.0], factors);
        Assert.DoesNotContain(groups, g => (string?)g[3] == "uls/permanent");
        var active = ByKey(f).Values.Count(c => (bool)c["enabled"]! && c["missing"] is null);
        Assert.Equal(active, groups.Count);
        // An accidental one is an ultimate one: in the ULS envelope.
        Assert.Contains(Results(f, new JsonObject { ["envelope"] = "uls" })["combinations"]!.AsArray(), x => (string?)x == key);
    }
}
