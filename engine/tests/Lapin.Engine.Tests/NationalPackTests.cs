using System.Text.Json.Nodes;
using Lapin.Analysis;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Loads as data in rule packs (docs/plan-national-packs.md, stage N1): Lapin's sets EN and CR 0 held as data give exactly the
/// combinations of before, through Lapin's pack and through a copy of it; a pack's own set (a Swedish-style example, its values
/// typed by the user) with γd by safety class, 6.10a and 6.10b and ψ of snow by the site's s0,k; the section's check refuses a
/// wrong value with why and fills a missing one from Lapin's; the imposed loads from the pack; the pack with its country,
/// edition and sources through a .lapinrules file and back.
/// </summary>
public class NationalPackTests
{
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
    static Dictionary<string, string> Names(Fixture f) => Q(f)["combinations"]!.AsArray().ToDictionary(c => (string)c!["key"]!, c => (string)c!["name"]!);
    static string Name(Fixture f, string key) => Names(f)[key];

    static string Copy(Fixture f, string name = "Mine") =>
        (string)f.Run("rules.create", new JsonObject { ["from"] = RulePacks.Builtin, ["name"] = name, ["activate"] = true })!["id"]!;

    [Fact]
    public void Lapin_s_pack_holds_EN_and_CR_0_as_the_code_s_values()
    {
        var d = LoadRuleSection.Instance.Default;
        foreach (var (name, set) in new[] { ("en", CombinationSettings.EnSet), ("cr0", CombinationSettings.Cr0Set) })
        {
            var s = d["sets"]![name]!;
            Assert.Equal(1.35, (double)s["gammaGSup"]!);
            Assert.Equal(1.0, (double)s["gammaGInf"]!);
            Assert.Equal(1.5, (double)s["gammaQ"]!);
            Assert.Equal("6.10", (string)s["expression"]!);
            Assert.Equal(0.85, (double)s["xi"]!);
            Assert.Empty(s["snowPsi"]!.AsArray());
            Assert.Empty(s["safetyClasses"]!.AsObject());
            foreach (var k in CombinationSettings.PsiKeys) Assert.Equal(set.Table(k), CombinationSettings.SetOf(name).Table(k));
            // Read back through the section's check: the same values.
            Assert.True(LoadRuleSection.CheckSet(name, s).SameValues(set));
        }
        // EN's ψ are Table A1.1's as the code has them; CR 0's snow, wind and roofs its Table 7.1's.
        foreach (var k in CombinationSettings.PsiKeys) Assert.Equal(Categories.TableOf(k), CombinationSettings.EnSet.Table(k));
        Assert.Equal(new Psi(0.7, 0.5, 0.4), CombinationSettings.Cr0Set.Table("snow"));
        Assert.Equal(new Psi(0.7, 0.2, 0), CombinationSettings.Cr0Set.Table("wind"));
        Assert.Equal(new Psi(0.7, 0, 0), CombinationSettings.Cr0Set.Table("H"));
        Assert.Equal(2.0, (double)d["imposed"]!["A"]!);
        Assert.Equal(7.5, (double)d["imposed"]!["E1"]!);
        Assert.True(JsonNode.DeepEquals(d, LoadRuleSection.Instance.Check(d)));
    }

    [Theory]
    [InlineData("en", "6.10")]
    [InlineData("cr0", "6.10")]
    [InlineData("en", "6.10ab")]
    [InlineData("cr0", "6.10ab")]
    public void EN_and_CR_0_as_data_give_exactly_the_combinations_of_before(string set, string expression)
    {
        // Golden: Lapin's pack, then a copy of it in use, then the copy brought into another project: the whole report the same.
        var (f, id) = Loaded();
        f.Run("analysis.setCombinationOptions", new JsonObject { ["set"] = set, ["expression"] = expression == "6.10" ? null : expression });
        var before = Q(f).ToJsonString();
        if (set == "en" && expression == "6.10")
            Assert.Equal("1.35 G1 + 1.35 G2 + 1.5 Q-A + 0.75 S + 0.9 W", Name(f, $"uls/{id["Q-A"]}"));
        if (set == "cr0" && expression == "6.10")
            Assert.Equal("1.35 G1 + 1.35 G2 + 1.5 Q-A + 1.05 S + 1.05 W", Name(f, $"uls/{id["Q-A"]}"));
        if (set == "en" && expression == "6.10ab")
            Assert.Equal("1.1475 G1 + 1.1475 G2 + 1.5 Q-A + 0.75 S + 0.9 W", Name(f, $"uls/{id["Q-A"]}"));
        var pack = Copy(f);
        Assert.Equal(before, Q(f).ToJsonString());
        Assert.NotNull(RulePacks.RawSection(f.Model, LoadRuleSection.SectionKey));
        var cases = LoadCaseTests.Cases(f).ToJsonString();

        var text = (string)f.Session.Query("rules.export", new JsonObject { ["id"] = pack })!["text"]!;
        var (g, _) = Loaded();
        g.Run("analysis.setCombinationOptions", new JsonObject { ["set"] = set, ["expression"] = expression == "6.10" ? null : expression });
        g.Run("rules.import", new JsonObject { ["text"] = text, ["activate"] = true });
        // The other project's ids differ: compare the names by the cases' codes.
        Assert.Equal(Names(f).Values.Order(), Names(g).Values.Order());
        Assert.Equal(Q(f)["notes"]!.ToJsonString(), Q(g)["notes"]!.ToJsonString());
        Assert.Equal(cases.Length, LoadCaseTests.Cases(g).ToJsonString().Length);
    }

    /// <summary>A set as a Swedish user might type it (the values theirs, here only an example): 6.10a/b with ξ, γd by safety class, ψ of snow by s0,k.</summary>
    static JsonObject SwedishStyle() => new()
    {
        ["sets"] = new JsonObject
        {
            ["se-example"] = new JsonObject
            {
                ["label"] = "Sweden, example (the user's values)",
                ["gammaGSup"] = 1.35, ["gammaGInf"] = 1.0, ["gammaQ"] = 1.5, ["expression"] = "6.10ab", ["xi"] = 0.89,
                ["safetyClasses"] = new JsonObject { ["1"] = 0.83, ["2"] = 0.91, ["3"] = 1.0 },
                ["snowPsi"] = new JsonArray(
                    new JsonObject { ["from"] = 0, ["psi"] = new JsonObject { ["0"] = 0.6, ["1"] = 0.3, ["2"] = 0.1 } },
                    new JsonObject { ["from"] = 2, ["psi"] = new JsonObject { ["0"] = 0.7, ["1"] = 0.4, ["2"] = 0.2 } },
                    new JsonObject { ["from"] = 3, ["psi"] = new JsonObject { ["0"] = 0.8, ["1"] = 0.6, ["2"] = 0.2 } }),
            },
        },
    };

    [Fact]
    public void A_pack_s_own_set_with_a_safety_class_gives_6_10a_and_6_10b_with_gamma_d()
    {
        var (f, id) = Loaded("G1", "Q-A", "S");
        var pack = Copy(f, "Sweden (example)");
        f.Run("rules.update", new JsonObject { ["id"] = pack, ["section"] = "loads", ["values"] = SwedishStyle() });
        var o = Q(f)["options"]!;
        Assert.True(o["sets"]!.AsObject().ContainsKey("se-example"));
        // Lapin's sets stay offered beside the pack's own.
        Assert.True(o["sets"]!.AsObject().ContainsKey("cr0"));
        f.Run("analysis.setCombinationOptions", new JsonObject { ["set"] = "se-example", ["safetyClass"] = "2" });
        Assert.Equal(0.91, (double)Q(f)["options"]!["gammaD"]!);
        Assert.Equal("6.10ab", (string)Q(f)["options"]!["expression"]!);

        // No site snow: ψ of snow is the set's table's (EN's, 0.5): 6.10a γd 1.35 G + γd 1.5 ψ0 Q.
        // 0.91 · 1.35 = 1.2285; 0.91 · 1.5 · 0.7 = 0.9555; 0.91 · 1.5 · 0.5 = 0.6825.
        Assert.Equal("1.2285 G1 + 0.9555 Q-A + 0.6825 S", Name(f, "uls/6.10a"));
        // 6.10b: γd ξ 1.35 G = 0.91 · 0.89 · 1.35 = 1.093365 → 1.0934; the lead γd 1.5 = 1.365.
        Assert.Equal("1.0934 G1 + 1.365 Q-A + 0.6825 S", Name(f, $"uls/{id["Q-A"]}"));
        Assert.Equal("1.2285 G1", Name(f, "uls/permanent"));
        // Serviceability: no γd.
        Assert.Equal("G1 + 0.3 Q-A", Name(f, "slsq"));
        var notes = Q(f)["notes"]!.AsArray().Select(n => (string)n!).ToList();
        Assert.Contains(notes, n => n.StartsWith("Safety class 2: γd 0.91"));
        Assert.Contains(notes, n => n.StartsWith("Indicative: Sweden, example (the user's values), the rules' values"));
        Assert.Contains(notes, n => n.StartsWith("The set gives ψ of snow by the site's s0,k"));

        // Safety class 3: γd 1; 0.89 · 1.35 = 1.2015.
        f.Run("analysis.setCombinationOptions", new JsonObject { ["safetyClass"] = "3" });
        Assert.Equal("1.2015 G1 + 1.5 Q-A + 0.75 S", Name(f, $"uls/{id["Q-A"]}"));
        Assert.Throws<CommandException>(() => f.Run("analysis.setCombinationOptions", new JsonObject { ["safetyClass"] = "4" }));
        // One undo step: back to class 2.
        f.Session.Undo();
        Assert.Equal("1.0934 G1 + 1.365 Q-A + 0.6825 S", Name(f, $"uls/{id["Q-A"]}"));
        // Lapin's sets have no safety classes.
        Assert.Throws<CommandException>(() => f.Run("analysis.setCombinationOptions", new JsonObject { ["set"] = "en", ["safetyClass"] = "2" }));
    }

    [Fact]
    public void Psi_of_snow_follows_the_site_s_s0k_through_the_set_s_zones()
    {
        var f = LoadCaseTests.F();
        var pack = Copy(f, "Sweden (example)");
        f.Run("rules.update", new JsonObject { ["id"] = pack, ["section"] = "loads", ["values"] = SwedishStyle() });
        f.Run("analysis.setCombinationOptions", new JsonObject { ["set"] = "se-example" });
        Psi Snow() { var p = Q(f)["options"]!["psi"]!["snow"]!; return new((double)p["0"]!, (double)p["1"]!, (double)p["2"]!); }
        Assert.Equal(new Psi(0.5, 0.2, 0), Snow());
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.5 } });
        Assert.Equal(new Psi(0.7, 0.4, 0.2), Snow());
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 3.0 } });
        Assert.Equal(new Psi(0.8, 0.6, 0.2), Snow());
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 1.0 } });
        Assert.Equal(new Psi(0.6, 0.3, 0.1), Snow());
        Assert.Equal(0, (double)Q(f)["options"]!["snowZone"]!["from"]!);
        // The loads' table shows the ψ a snow case takes.
        var snowCase = LoadCaseTests.Cases(f)["cases"]!.AsArray().First(c => (string?)c!["category"] == "snow")!;
        Assert.Equal(0.6, (double)snowCase["psi"]!["0"]!);
    }

    [Fact]
    public void The_loads_section_refuses_a_wrong_value_with_why_and_fills_a_missing_one_from_Lapin_s()
    {
        var s = LoadRuleSection.Instance;
        JsonObject One(string name, JsonObject set) => new() { ["sets"] = new JsonObject { [name] = set } };
        Assert.Throws<CommandException>(() => s.Check(One("x", new JsonObject { ["gammaQ"] = 5 })));
        Assert.Throws<CommandException>(() => s.Check(One("x", new JsonObject { ["gammaGInf"] = -1 })));
        Assert.Throws<CommandException>(() => s.Check(One("x", new JsonObject { ["expression"] = "6.11" })));
        Assert.Throws<CommandException>(() => s.Check(One("x", new JsonObject { ["psi"] = new JsonObject { ["A"] = new JsonObject { ["0"] = 1.5, ["1"] = 0, ["2"] = 0 } } })));
        Assert.Throws<CommandException>(() => s.Check(One("x", new JsonObject { ["psi"] = new JsonObject { ["Z"] = new JsonObject { ["0"] = 0.5, ["1"] = 0, ["2"] = 0 } } })));
        Assert.Throws<CommandException>(() => s.Check(One("x", new JsonObject { ["safetyClasses"] = new JsonObject { ["2"] = 3 } })));
        Assert.Throws<CommandException>(() => s.Check(One("x", new JsonObject { ["snowPsi"] = new JsonArray(new JsonObject { ["from"] = 1, ["psi"] = new JsonObject { ["0"] = 0.5, ["1"] = 0, ["2"] = 0 } }, new JsonObject { ["from"] = 1, ["psi"] = new JsonObject { ["0"] = 0.6, ["1"] = 0, ["2"] = 0 } }) })));
        Assert.Throws<CommandException>(() => s.Check(One(" ", new JsonObject())));
        Assert.Throws<CommandException>(() => s.Check(new JsonObject { ["imposed"] = new JsonObject { ["A"] = -2 } }));
        Assert.Throws<CommandException>(() => s.Check(new JsonObject { ["imposed"] = new JsonObject { ["Z9"] = 2 } }));
        Assert.Throws<CommandException>(() => s.Check(new JsonArray()));

        // Missing values: a new set's from EN, cr0's from CR 0, the imposed loads not given from Lapin's.
        var c = s.Check(new JsonObject
        {
            ["sets"] = new JsonObject { ["mine"] = new JsonObject { ["gammaQ"] = 1.4 }, ["cr0"] = new JsonObject { ["gammaGSup"] = 1.3 } },
            ["imposed"] = new JsonObject { ["A"] = 1.5 },
        });
        Assert.Equal(1.4, (double)c["sets"]!["mine"]!["gammaQ"]!);
        Assert.Equal(1.35, (double)c["sets"]!["mine"]!["gammaGSup"]!);
        Assert.Equal("mine", (string)c["sets"]!["mine"]!["label"]!);
        Assert.Equal(0.5, (double)c["sets"]!["mine"]!["psi"]!["snow"]!["0"]!);
        Assert.Equal(1.3, (double)c["sets"]!["cr0"]!["gammaGSup"]!);
        Assert.Equal(0.7, (double)c["sets"]!["cr0"]!["psi"]!["snow"]!["0"]!);
        Assert.Equal("CR 0-2012", (string)c["sets"]!["cr0"]!["label"]!);
        Assert.Equal(1.5, (double)c["imposed"]!["A"]!);
        Assert.Equal(3.0, (double)c["imposed"]!["B"]!);
        // Nothing given: Lapin's.
        Assert.True(JsonNode.DeepEquals(s.Default, s.Check(new JsonObject())));
        Assert.True(JsonNode.DeepEquals(s.Default, s.Check(null)));

        // Through the command: refused with the section's label, the pack unchanged.
        var f = LoadCaseTests.F();
        var pack = Copy(f);
        var e = Assert.Throws<CommandException>(() => f.Run("rules.update", new JsonObject { ["id"] = pack, ["section"] = "loads", ["values"] = One("x", new JsonObject { ["gammaQ"] = 5 }) }));
        Assert.Contains("γQ", e.Message);
    }

    [Fact]
    public void The_imposed_loads_come_from_the_pack_and_a_project_s_own_goes_back_to_the_pack_s()
    {
        var f = LoadCaseTests.F();
        Assert.Equal(2.0, Categories.Qk(f.Model, "A"));
        var pack = Copy(f);
        f.Run("rules.update", new JsonObject { ["id"] = pack, ["section"] = "loads", ["values"] = new JsonObject { ["imposed"] = new JsonObject { ["A"] = 1.5 } } });
        Assert.Equal(1.5, Categories.Qk(f.Model, "A"));
        Assert.Equal(3.0, Categories.Qk(f.Model, "B"));
        JsonObject Cat(string k) => f.Session.Query("analysis.categories", [])!.AsArray().Single(c => (string?)c!["key"] == k)!.AsObject();
        Assert.Equal(1.5, (double)Cat("A")["default"]!);
        f.Run("analysis.setImposedLoad", new JsonObject { ["category"] = "A", ["qk"] = 1.8 });
        Assert.Equal(1.8, Categories.Qk(f.Model, "A"));
        f.Run("analysis.setImposedLoad", new JsonObject { ["category"] = "A", ["qk"] = null });
        Assert.Equal(1.5, Categories.Qk(f.Model, "A"));
        // Lapin's pack again: the code's values.
        f.Run("rules.setActive", new JsonObject { ["id"] = RulePacks.Builtin });
        Assert.Equal(2.0, Categories.Qk(f.Model, "A"));
    }

    [Fact]
    public void A_pack_with_its_country_edition_and_sources_goes_through_a_file_and_back()
    {
        var a = LoadCaseTests.F();
        var pack = Copy(a, "Sweden (example)");
        a.Run("rules.update", new JsonObject
        {
            ["id"] = pack, ["description"] = "An example", ["country"] = "SE", ["edition"] = "EKS 11 (example)", ["language"] = "sv",
            ["sources"] = new JsonArray("Boverket, the user's reading", " "),
        });
        a.Run("rules.update", new JsonObject { ["id"] = pack, ["section"] = "loads", ["values"] = SwedishStyle() });
        Assert.Throws<CommandException>(() => a.Run("rules.update", new JsonObject { ["id"] = pack, ["language"] = "Swedish" }));
        var meta = a.Session.Query("rules.get", new JsonObject { ["id"] = pack })!["meta"]!;
        Assert.Equal("SE", (string)meta["country"]!);
        Assert.Equal(["Boverket, the user's reading"], meta["sources"]!.AsArray().Select(x => (string)x!).ToArray());

        var text = (string)a.Session.Query("rules.export", new JsonObject { ["id"] = pack })!["text"]!;
        var file = JsonNode.Parse(text)!;
        Assert.True((bool)file["indicative"]!);
        Assert.Equal("EKS 11 (example)", (string)file["edition"]!);
        Assert.Equal("An example", (string)file["description"]!);
        Assert.Equal(0.91, (double)file["loads"]!["sets"]!["se-example"]!["safetyClasses"]!["2"]!);

        var b = LoadCaseTests.F();
        var made = (string)b.Run("rules.import", new JsonObject { ["text"] = text, ["activate"] = true })!["id"]!;
        var got = b.Session.Query("rules.get", new JsonObject { ["id"] = made })!;
        Assert.True(JsonNode.DeepEquals(a.Session.Query("rules.get", new JsonObject { ["id"] = pack })!["sections"]!["loads"], got["sections"]!["loads"]));
        Assert.True(JsonNode.DeepEquals(meta, got["meta"]));
        b.Run("analysis.setCombinationOptions", new JsonObject { ["set"] = "se-example", ["safetyClass"] = "1" });
        Assert.Equal(0.83, (double)Q(b)["options"]!["gammaD"]!);
        // Exported again, the same file but its header.
        var again = JsonNode.Parse((string)b.Session.Query("rules.export", new JsonObject { ["id"] = made })!["text"]!)!.AsObject();
        var first = file.AsObject();
        first.Remove("lapin"); again.Remove("lapin");
        Assert.True(JsonNode.DeepEquals(first, again));
        // The same file a second time: the same pack, kept.
        var preview = b.Session.Query("library.preview", new JsonObject { ["text"] = text })!;
        Assert.Equal("same", (string?)preview["rows"]![0]!["status"]);
    }

    [Fact]
    public void A_project_naming_a_set_the_pack_in_use_lacks_takes_EN_and_says_so()
    {
        var (f, _) = Loaded("G1", "Q-A");
        var pack = Copy(f, "Sweden (example)");
        f.Run("rules.update", new JsonObject { ["id"] = pack, ["section"] = "loads", ["values"] = SwedishStyle() });
        f.Run("analysis.setCombinationOptions", new JsonObject { ["set"] = "se-example" });
        f.Run("rules.setActive", new JsonObject { ["id"] = RulePacks.Builtin });
        var o = Q(f);
        Assert.Equal("en", (string)o["options"]!["set"]!);
        Assert.Equal("se-example", (string)o["options"]!["missingSet"]!);
        Assert.Contains(o["notes"]!.AsArray(), n => ((string)n!).StartsWith("The rules in use have no set of factors „se-example”"));
    }
}
