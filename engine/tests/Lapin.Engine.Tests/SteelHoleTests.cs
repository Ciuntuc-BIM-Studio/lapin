using System.Text.Json.Nodes;
using Lapin.Drafting;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// A bolt's hole (audit 22.11.2026 §3.7): EN 1090-2 Table 11's normal round holes, 1 mm over M12 and M14, 2 mm over M16–M24,
/// 3 mm past, the built-in default; a rule pack gives its own clearances.
/// </summary>
public class SteelHoleTests
{
    [Fact]
    public void Holes_take_EN_1090_2_clearances_and_a_cleats_edges_follow()
    {
        Assert.Equal(0.013, NodeSpec.Hole(0.012), 9);
        Assert.Equal(0.015, NodeSpec.Hole(0.014), 9);
        Assert.Equal(0.018, NodeSpec.Hole(0.016), 9);
        Assert.Equal(0.026, NodeSpec.Hole(0.024), 9);
        Assert.Equal(0.030, NodeSpec.Hole(0.027), 9);
        // A cleat on an IPE 200 (h < 0.25): M12, d0 = 13 mm; edge 1.5 d0 = 19.5 → 20 mm (was 21 → 25 with 2 mm), pitch max(3 d0, 60) = 60 mm.
        var spec = NodeSpec.Resolve("cleat", Section.Catalogue["IPE 200"], new NodeParams());
        Assert.Equal(0.012, spec.Bolt, 9);
        Assert.Equal(0.020, spec.Edge, 9);
        Assert.Equal(0.060, spec.Pitch, 9);
    }

    [Fact]
    public void A_rule_pack_gives_its_own_clearances()
    {
        var f = new Fixture(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());
        Assert.Equal(NodeSpec.Clearances, BoltRuleSection.Clearances(f.Model));
        var id = (string)f.Run("rules.create", new JsonObject { ["from"] = RulePacks.Builtin, ["name"] = "Holes", ["activate"] = true })!["id"]!;
        f.Run("rules.update", new JsonObject
        {
            ["id"] = id, ["section"] = "bolts",
            ["values"] = new JsonObject { ["holeClearance"] = new JsonArray(new JsonObject { ["upTo"] = 24, ["clearance"] = 2 }, new JsonObject { ["clearance"] = 3 }) },
        });
        var own = BoltRuleSection.Clearances(f.Model);
        Assert.Equal(0.014, NodeSpec.Hole(0.012, own), 9);
        Assert.Equal(0.030, NodeSpec.Hole(0.027, own), 9);
        Assert.Throws<CommandException>(() => f.Run("rules.update", new JsonObject
        {
            ["id"] = id, ["section"] = "bolts",
            ["values"] = new JsonObject { ["holeClearance"] = new JsonArray(new JsonObject { ["upTo"] = 24, ["clearance"] = 2 }, new JsonObject { ["upTo"] = 20, ["clearance"] = 3 }, new JsonObject { ["clearance"] = 3 }) },
        }));
    }
}
