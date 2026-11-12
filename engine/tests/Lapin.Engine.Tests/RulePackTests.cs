using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Rule packs (docs/plan-user-extensibility.md §5.4): Lapin's own holds the values the code has always had; a project's copy is
/// changed, made the project's, exported and brought into another project; stairs, bars and roofs read it; the check reports what
/// differs; nothing is refused for it.
/// </summary>
public sealed class RulePackTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new RebarModule());

    [Fact]
    public void Lapin_s_pack_is_the_code_s_values_and_reads_back_the_same()
    {
        var f = F();
        var get = f.Session.Query("rules.get", [])!;
        Assert.Equal(RulePacks.Builtin, (string)get["id"]!);
        Assert.True((bool)get["indicative"]!);
        var stairs = get["sections"]!["stairs"]!;
        Assert.Equal(StairRules.Residential.RiserMax, (double)stairs["residential"]!["riserMax"]!, 9);
        Assert.Equal(StairRules.Public.MinWidth, (double)stairs["public"]!["minWidth"]!, 9);
        Assert.Equal("B500C", (string)get["sections"]!["rebar"]!["defaultSteel"]!);
        Assert.Equal(22, (double)get["sections"]!["roofs"]!["minPitch"]!["Clay tile"]!, 9);
        // With Lapin's pack, nothing changes: a bar's bend is the code's, its hook the code's.
        Assert.Equal(RebarShapes.BendRadius(0.020), RebarRuleSection.BendRadius(f.Model, 0.020), 12);
        Assert.Equal(RebarShapes.HookLength(0.012, 135), RebarRuleSection.HookLength(f.Model, 0.012, 135), 12);
        Assert.Throws<CommandException>(() => f.Run("rules.update", new JsonObject { ["id"] = RulePacks.Builtin, ["name"] = "x" }));
    }

    static string OwnPack(Fixture f)
    {
        var id = (string)f.Run("rules.create", new JsonObject { ["from"] = RulePacks.Builtin, ["name"] = "Office (DE)", ["activate"] = true })!["id"]!;
        f.Run("rules.update", new JsonObject { ["id"] = id, ["section"] = "stairs", ["values"] = new JsonObject { ["residential"] = new JsonObject { ["riserMax"] = 0.19, ["treadMin"] = 0.23 } } });
        f.Run("rules.update", new JsonObject
        {
            ["id"] = id, ["section"] = "rebar",
            ["values"] = new JsonObject
            {
                ["steels"] = new JsonArray("B500A", "B500B"), ["defaultSteel"] = "B500B", ["diameters"] = new JsonArray(6, 8, 10, 12, 14, 16, 20, 25, 28),
                ["mandrel"] = new JsonArray(new JsonObject { ["upTo"] = 16, ["factor"] = 4 }, new JsonObject { ["factor"] = 7.5 }),
            },
        });
        f.Run("rules.update", new JsonObject { ["id"] = id, ["section"] = "roofs", ["values"] = new JsonObject { ["minPitch"] = new JsonObject { ["Clay tile"] = 30 } } });
        return id;
    }

    [Fact]
    public void A_project_s_own_pack_drives_stairs_bars_and_roofs_and_the_check_says_what_differs()
    {
        var f = F();
        var stairType = (string)f.Run("architecture.createStairType", new JsonObject { ["spec"] = StairSpec.Default().ToJson() })!["id"]!;
        Assert.Empty(f.Session.Query("rules.check", [])!["findings"]!.AsArray());
        OwnPack(f);
        Assert.Equal("Office (DE)", (string)f.Session.Query("rules.list", [])!["items"]!.AsArray().Single(i => (bool)i!["active"]!)!["name"]!);

        // Stairs: a new type's rules; a type made before differs, the check says so, Apply copies them in.
        var rules = f.Session.Query("architecture.stairRules", new JsonObject { ["set"] = "residential" })!;
        Assert.Equal(0.19, (double)rules["riserMax"]!, 9);
        Assert.Equal(StairRules.Residential.RiserMin, (double)rules["riserMin"]!, 9);
        var findings = f.Session.Query("rules.check", [])!["findings"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains(findings, x => (string?)x["element"] == stairType && ((string)x["message"]!).Contains("riserMax"));
        Assert.Equal(1, (int)f.Run("architecture.applyStairRules", [])!["changed"]!);
        Assert.Equal(0.19, StairSpec.From(f.Model.Get(stairType)!.Attrs["spec"])!.Rules.RiserMax, 9);

        // Bars: a new one takes the pack's steel and, Ø20 over Ø16, its mandrel's bend; a Ø18 is not a diameter it offers.
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(5, 0) })!["id"]!;
        var bar = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = section, ["shape"] = "L", ["at"] = new JsonArray(0, 0), ["diameter"] = 0.020 })!["id"]!;
        var e = f.Model.Get(bar)!;
        Assert.Equal("B500B", e.Str("steel"));
        Assert.Equal(7.5 * 0.020 / 2, e.Num("bendRadius"), 9);
        var odd = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = section, ["shape"] = "straight", ["at"] = new JsonArray(0, 1), ["diameter"] = 0.018, ["steel"] = "PC52" })!["id"]!;
        var barFindings = f.Session.Query("rules.check", [])!["findings"]!.AsArray().OfType<JsonObject>().Where(x => (string?)x["element"] == odd).ToList();
        Assert.Equal(2, barFindings.Count);

        // Roofs: a clay tile's least pitch is the pack's.
        Assert.Equal(30, Lapin.Architecture.Roofs.RoofRuleSection.MinPitch(f.Model, Lapin.Architecture.Roofs.RoofCatalog.CoveringOf("Clay tile")), 9);
    }

    [Fact]
    public void A_pack_goes_to_another_project_as_a_file_and_a_wrong_value_is_refused_with_why()
    {
        var a = F();
        var id = OwnPack(a);
        var text = (string)a.Session.Query("rules.export", new JsonObject { ["id"] = id })!["text"]!;
        Assert.Contains("\"indicative\": true", text);

        var b = F();
        var made = b.Run("rules.import", new JsonObject { ["text"] = text, ["activate"] = true })!;
        Assert.Equal("Office (DE)", (string)made["name"]!);
        Assert.Equal(0.19, (double)b.Session.Query("architecture.stairRules", new JsonObject { ["set"] = "residential" })!["riserMax"]!, 9);
        Assert.Equal("B500B", (string)b.Session.Query("rebar.rules", [])!["defaultSteel"]!);
        // One undo step takes the pack away; Lapin's rules again.
        b.Session.Undo();
        Assert.Equal(StairRules.Residential.RiserMax, (double)b.Session.Query("architecture.stairRules", new JsonObject { ["set"] = "residential" })!["riserMax"]!, 9);

        Assert.Throws<CommandException>(() => a.Run("rules.update", new JsonObject { ["id"] = id, ["section"] = "stairs", ["values"] = new JsonObject { ["residential"] = new JsonObject { ["riserMax"] = 0.9 } } }));
        Assert.Throws<CommandException>(() => a.Run("rules.update", new JsonObject { ["id"] = id, ["section"] = "rebar", ["values"] = new JsonObject { ["steels"] = new JsonArray("X"), ["defaultSteel"] = "Y" } }));
        Assert.Throws<CommandException>(() => b.Run("rules.import", new JsonObject { ["text"] = "{\"format\":\"lapin-standards\"}" }));
        // Deleted while used: the project goes back to Lapin's.
        a.Run("rules.delete", new JsonObject { ["id"] = id });
        Assert.Equal(RulePacks.Builtin, RulePacks.ActiveId(a.Model));
    }

    [Fact]
    public void A_pack_names_the_bolt_grades_to_offer()
    {
        var f = new Fixture(new StructureModule(), new Lapin.Steel.SteelModule());
        Assert.Equal(["4.6", "5.6", "8.8", "10.9"], f.Session.Query("steel.boltGrades", [])!["grades"]!.AsArray().Select(x => (string)x!).ToArray());
        var id = (string)f.Run("rules.create", new JsonObject { ["from"] = RulePacks.Builtin, ["name"] = "US", ["activate"] = true })!["id"]!;
        f.Run("rules.update", new JsonObject { ["id"] = id, ["section"] = "bolts", ["values"] = new JsonObject { ["grades"] = new JsonArray("A325", "A490") } });
        Assert.Equal(["A325", "A490"], f.Session.Query("steel.boltGrades", [])!["grades"]!.AsArray().Select(x => (string)x!).ToArray());
    }
}
