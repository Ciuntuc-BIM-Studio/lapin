using System.Text.Json.Nodes;
using Lapin.Analysis;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The climate and accessibility sections of rule packs (docs/plan-national-packs.md, stages N2 and N3). Climate: a list of regions
/// (a Swedish kommun, here only an example with the user's values) giving sk and vb,0 (or qb); a region picked fills the site's
/// s0,k and qb, the other values kept, in one undo step; typing stays. Accessibility: a door's least clear width, a corridor's
/// least width, the turning circle, ramps against the stairs' set ramp; findings only, none under Lapin's own pack. Each section's
/// check refuses a wrong value with why and fills a missing one; both go through a .lapinrules file and back.
/// </summary>
public class ClimateAccessibilityPackTests
{
    static string Copy(Fixture f, string name = "Mine") =>
        (string)f.Run("rules.create", new JsonObject { ["from"] = RulePacks.Builtin, ["name"] = name, ["activate"] = true })!["id"]!;

    static JsonObject Kommuns() => new()
    {
        ["regionLabel"] = "Kommun",
        ["regions"] = new JsonArray(
            new JsonObject { ["name"] = "Kiruna", ["sk"] = 5.5, ["vb0"] = 22, ["note"] = "the user's values" },
            new JsonObject { ["name"] = "Malmö", ["sk"] = 1.0, ["vb0"] = 26 },
            new JsonObject { ["name"] = "Wind only", ["qb"] = 0.5 }),
    };

    static JsonObject Climate(Fixture f) => f.Session.Query("analysis.climate", [])!.AsObject();

    // ─── Climate ─────────────────────────────────────────────────────────────

    [Fact]
    public void The_climate_section_refuses_a_wrong_value_with_why_and_fills_a_missing_one()
    {
        var s = ClimateRuleSection.Instance;
        JsonObject One(JsonObject region) => new() { ["regions"] = new JsonArray(region) };
        Assert.Throws<CommandException>(() => s.Check(One(new JsonObject { ["name"] = "A", ["sk"] = 25 })));
        Assert.Throws<CommandException>(() => s.Check(One(new JsonObject { ["name"] = "A", ["vb0"] = 100 })));
        Assert.Throws<CommandException>(() => s.Check(One(new JsonObject { ["name"] = "A", ["vb0"] = 22, ["qb"] = 0.4 })));
        Assert.Throws<CommandException>(() => s.Check(One(new JsonObject { ["name"] = "A" })));
        Assert.Throws<CommandException>(() => s.Check(One(new JsonObject { ["name"] = " ", ["sk"] = 1 })));
        Assert.Throws<CommandException>(() => s.Check(One(new JsonObject { ["name"] = "A", ["sk"] = "much" })));
        Assert.Throws<CommandException>(() => s.Check(new JsonObject { ["regions"] = new JsonArray(new JsonObject { ["name"] = "A", ["sk"] = 1 }, new JsonObject { ["name"] = "a", ["sk"] = 2 }) }));
        Assert.Throws<CommandException>(() => s.Check(new JsonObject { ["regions"] = new JsonObject() }));
        Assert.Throws<CommandException>(() => s.Check(new JsonObject { ["regionLabel"] = new string('x', 41) }));
        Assert.Throws<CommandException>(() => s.Check(new JsonArray()));

        // Nothing given: no regions, labelled Region; a label left out takes Region; a region's own values kept, its name trimmed.
        Assert.True(JsonNode.DeepEquals(s.Default, s.Check(null)));
        Assert.True(JsonNode.DeepEquals(s.Default, s.Check(new JsonObject())));
        var c = s.Check(new JsonObject { ["regions"] = new JsonArray(new JsonObject { ["name"] = " Kiruna ", ["sk"] = 5.5 }) });
        Assert.Equal("Region", (string)c["regionLabel"]!);
        Assert.Equal("Kiruna", (string)c["regions"]![0]!["name"]!);
        Assert.Null(c["regions"]![0]!["vb0"]);
        Assert.True(JsonNode.DeepEquals(s.Check(Kommuns()), s.Check(s.Check(Kommuns()))));

        // qb from vb,0: ½ · 1.25 · 22² = 302.5 N/m².
        Assert.Equal(0.3025, ClimateRuleSection.ReadRegion(new JsonObject { ["name"] = "K", ["vb0"] = 22 }).Qb!.Value, 9);

        // Through the command: refused with why, the pack unchanged.
        var f = LoadCaseTests.F();
        var pack = Copy(f);
        var e = Assert.Throws<CommandException>(() => f.Run("rules.update", new JsonObject { ["id"] = pack, ["section"] = "climate", ["values"] = One(new JsonObject { ["name"] = "A", ["sk"] = 25 }) }));
        Assert.Contains("sk (kN/m²)", e.Message);
    }

    [Fact]
    public void Lapin_s_pack_lists_no_regions_and_a_project_s_climate_stays_as_it_was()
    {
        var f = LoadCaseTests.F();
        var before = Climate(f);
        Assert.Empty(before["regions"]!.AsArray());
        Assert.Null(before["region"]);
        Assert.Equal("Region", (string)before["regionLabel"]!);
        Assert.Throws<CommandException>(() => f.Run("analysis.setClimate", new JsonObject { ["region"] = "Kiruna" }));
        // A copy of Lapin's (its climate section Lapin's, empty): nothing changes either.
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0 } });
        var typed = Climate(f).ToJsonString();
        Copy(f);
        Assert.Equal(typed, Climate(f).ToJsonString());
    }

    [Fact]
    public void A_region_picked_fills_the_site_s_snow_and_wind_and_typing_stays()
    {
        var f = LoadCaseTests.F();
        var pack = Copy(f, "Sweden (example)");
        f.Run("rules.update", new JsonObject { ["id"] = pack, ["section"] = "climate", ["values"] = Kommuns() });
        Assert.Equal("Kommun", (string)Climate(f)["regionLabel"]!);
        Assert.Equal(3, Climate(f)["regions"]!.AsArray().Count);
        Assert.Equal(0.3025, (double)Climate(f)["regions"]![0]!["qbDerived"]!, 9);

        // The snow's exposure and the wind's terrain typed before stay; s0,k and qb come from the region.
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 2.0, ["ce"] = 1.2 }, ["wind"] = new JsonObject { ["qb"] = 0.4, ["terrain"] = "III" } });
        var cases = LoadCaseTests.Cases(f)["cases"]!.AsArray().Count;
        f.Run("analysis.setClimate", new JsonObject { ["region"] = "kiruna" });
        var c = Climate(f);
        Assert.Equal("Kiruna", (string)c["region"]!);
        Assert.Equal(5.5, (double)c["snow"]!["s0k"]!);
        Assert.Equal(1.2, (double)c["snow"]!["ce"]!);
        Assert.Equal(0.3025, (double)c["wind"]!["qb"]!, 9);
        Assert.Equal("III", (string)c["wind"]!["terrain"]!);
        Assert.Equal(cases, LoadCaseTests.Cases(f)["cases"]!.AsArray().Count);
        Assert.Equal(5.5, Lapin.Analysis.Climate.Snow(f.Model)!.S0k);

        // One undo step: back to what was typed.
        f.Session.Undo();
        Assert.Equal(2.0, (double)Climate(f)["snow"]!["s0k"]!);
        Assert.Null(Climate(f)["region"]);
        f.Session.Redo();
        Assert.Equal("Kiruna", (string)Climate(f)["region"]!);

        // Another value typed over the region's snow: the region is no longer named; the exposure alone keeps it.
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 5.5, ["ce"] = 0.8 } });
        Assert.Equal("Kiruna", (string)Climate(f)["region"]!);
        f.Run("analysis.setClimate", new JsonObject { ["snow"] = new JsonObject { ["s0k"] = 4.0 } });
        Assert.Null(Climate(f)["region"]);
        Assert.Equal(4.0, (double)Climate(f)["snow"]!["s0k"]!);

        // A region that gives the wind alone leaves the snow; on a fresh project, a pick makes the snow and wind cases.
        f.Run("analysis.setClimate", new JsonObject { ["region"] = "Wind only" });
        Assert.Equal(4.0, (double)Climate(f)["snow"]!["s0k"]!);
        Assert.Equal(0.5, (double)Climate(f)["wind"]!["qb"]!);
        Assert.Throws<CommandException>(() => f.Run("analysis.setClimate", new JsonObject { ["region"] = "Nowhere" }));

        var g = LoadCaseTests.F();
        g.Run("rules.update", new JsonObject { ["id"] = Copy(g), ["section"] = "climate", ["values"] = Kommuns() });
        g.Run("analysis.setClimate", new JsonObject { ["region"] = "Malmö" });
        var made = LoadCaseTests.Cases(g)["cases"]!.AsArray();
        Assert.Contains(made, x => (string?)x!["category"] == "snow");
        Assert.Contains(made, x => (string?)x!["category"] == "wind");
        // ψ of snow follows the region's sk through the loads' zones (N1) as for a typed s0,k.
        Assert.Equal(1.0, Lapin.Analysis.Climate.Snow(g.Model)!.S0k);

        // The pack in use changed to Lapin's: the values stay, the region is no longer one of its list.
        g.Run("rules.setActive", new JsonObject { ["id"] = RulePacks.Builtin });
        Assert.Null(Climate(g)["region"]);
        Assert.Equal(1.0, (double)Climate(g)["snow"]!["s0k"]!);
    }

    // ─── Accessibility ───────────────────────────────────────────────────────

    [Fact]
    public void The_accessibility_section_refuses_a_wrong_value_with_why_and_fills_a_missing_one()
    {
        var s = AccessibilityRuleSection.Instance;
        Assert.Throws<CommandException>(() => s.Check(new JsonObject { ["doorClearWidth"] = 0.2 }));
        Assert.Throws<CommandException>(() => s.Check(new JsonObject { ["corridorWidth"] = 9 }));
        Assert.Throws<CommandException>(() => s.Check(new JsonObject { ["turningDiameter"] = 5 }));
        Assert.Throws<CommandException>(() => s.Check(new JsonObject { ["turningDiameter"] = "wide" }));
        Assert.Throws<CommandException>(() => s.Check(new JsonObject { ["checkRamps"] = "yes" }));
        Assert.Throws<CommandException>(() => s.Check(new JsonObject { ["turningFunctions"] = "Bath" }));
        Assert.Throws<CommandException>(() => s.Check(new JsonObject { ["corridorFunctions"] = new JsonArray("") }));
        Assert.Throws<CommandException>(() => s.Check(new JsonArray()));

        // Nothing given: nothing checked (Lapin's), the functions Lapin's.
        Assert.True(JsonNode.DeepEquals(s.Default, s.Check((JsonNode?)null)));
        Assert.True(JsonNode.DeepEquals(s.Default, s.Check(new JsonObject())));
        Assert.Null(s.Default["doorClearWidth"]);
        Assert.False((bool)s.Default["checkRamps"]!);
        var c = s.Check(new JsonObject { ["doorClearWidth"] = 0.8, ["turningFunctions"] = new JsonArray("Bath", " bath ", "WC") });
        Assert.Equal(0.8, (double)c["doorClearWidth"]!);
        Assert.Null(c["corridorWidth"]);
        Assert.Equal(["Bath", "WC"], c["turningFunctions"]!.AsArray().Select(x => (string)x!).ToArray());
        Assert.Equal(["Circulation"], c["corridorFunctions"]!.AsArray().Select(x => (string)x!).ToArray());
        Assert.True(JsonNode.DeepEquals(c, s.Check(c)));
    }

    /// <summary>A project with a door of 900 (clear 780), a corridor 1.0 m wide, a bathroom 1.4 × 1.4 m, a ramp at 12 %, a living room 4 × 4 m.</summary>
    static (Fixture F, string Door, string Corridor, string Bath, string Ramp) Project()
    {
        var f = LoadCaseTests.F();
        var wall = f.Wall(0, -3, 6, -3);
        var spec = JoinerySpec.DefaultDoor("Door 90") with { Frame = new JoineryFrame(0.1, 0.06, 0.06, 0.06, 0) };
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5, ["typeId"] = type })!["id"]!;
        string Room(string fn, params (double X, double Y)[] p) => (string)f.Run("architecture.addRoom", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray()), ["name"] = fn, ["function"] = fn,
        })!["id"]!;
        // An L corridor 1.0 m wide.
        var corridor = Room("Circulation", (0, 0), (6, 0), (6, 1), (1, 1), (1, 4), (0, 4));
        var bath = Room("Bath", (10, 0), (11.4, 0), (11.4, 1.4), (10, 1.4));
        Room("Living", (20, 0), (24, 0), (24, 4), (20, 4));
        // A storage room, small, of a function the turning circle does not ask of.
        Room("Storage", (30, 0), (31, 0), (31, 1), (30, 1));
        var ramp = (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["library"] = "entrance-ramp", ["x"] = 0, ["y"] = 10, ["x2"] = 1, ["y2"] = 10 })!["id"]!;
        f.Run("architecture.updateStair", new JsonObject { ["id"] = ramp, ["slope"] = 0.12 });
        return (f, door, corridor, bath, ramp);
    }

    static List<RuleFinding> Findings(Fixture f) =>
        f.Session.Query("rules.check", [])!["findings"]!.AsArray()
            .Select(x => new RuleFinding((string)x!["module"]!, (string?)x["element"], (string)x["severity"]!, (string)x["message"]!))
            .Where(x => x.Message.EndsWith("(accessibility).")).ToList();

    [Fact]
    public void Lapin_s_pack_finds_nothing_of_accessibility_and_a_pack_s_values_find_a_narrow_door_a_steep_ramp_and_a_narrow_corridor()
    {
        var (f, door, corridor, bath, ramp) = Project();
        Assert.Empty(Findings(f));
        var pack = Copy(f, "Sweden (example)");
        Assert.Empty(Findings(f));

        f.Run("rules.update", new JsonObject
        {
            ["id"] = pack, ["section"] = "accessibility",
            ["values"] = new JsonObject { ["doorClearWidth"] = 0.8, ["corridorWidth"] = 1.3, ["turningDiameter"] = 1.5, ["checkRamps"] = true },
        });
        var found = Findings(f);
        Assert.Contains(found, x => x.ElementId == door && x.Message.Contains("780 mm") && x.Message.Contains("800 mm"));
        Assert.Contains(found, x => x.ElementId == ramp && x.Message.Contains("12 %") && x.Message.Contains("8 %"));
        Assert.Contains(found, x => x.ElementId == corridor && x.Message.Contains("1000 mm wide"));
        Assert.Contains(found, x => x.ElementId == bath && x.Message.Contains("1400 mm") && x.Message.Contains("1500 mm"));
        // The living room holds the circle; the storage room is not asked to; the corridor (1 m) fails the circle too.
        Assert.Equal(5, found.Count);
        Assert.All(found, x => Assert.Equal("warning", x.Severity));

        // Within the limits: nothing.
        f.Run("rules.update", new JsonObject
        {
            ["id"] = pack, ["section"] = "accessibility",
            ["values"] = new JsonObject { ["doorClearWidth"] = 0.75, ["corridorWidth"] = 0.9, ["turningDiameter"] = 1.3, ["checkRamps"] = false, ["turningFunctions"] = new JsonArray("Bath", "Living") },
        });
        Assert.Empty(Findings(f));

        // The ramp follows the stairs' set ramp of the pack: made steeper there, the ramp passes.
        var stairs = f.Session.Query("rules.get", new JsonObject { ["id"] = pack })!["sections"]!["stairs"]!.DeepClone().AsObject();
        stairs["ramp"]!["maxSlope"] = 0.125;
        f.Run("rules.update", new JsonObject { ["id"] = pack, ["section"] = "stairs", ["values"] = stairs });
        f.Run("rules.update", new JsonObject { ["id"] = pack, ["section"] = "accessibility", ["values"] = new JsonObject { ["checkRamps"] = true } });
        Assert.Empty(Findings(f));
        // Nothing is refused: the door, the ramp and the rooms are as they were.
        Assert.Equal(0.12, f.Model.Get(ramp)!.Num("slope"), 9);
    }

    [Fact]
    public void A_room_s_least_width_and_largest_circle()
    {
        Vec2[] P(params (double X, double Y)[] p) => p.Select(q => new Vec2(q.X, q.Y)).ToArray();
        IReadOnlyList<IReadOnlyList<Vec2>> none = [];
        // A rectangle 1.2 × 6: 1.2 wide, holds 1.2.
        Assert.Equal(1.2, RoomWidths.Least(P((0, 0), (6, 0), (6, 1.2), (0, 1.2)), none)!.Value, 6);
        Assert.Equal(1.2, RoomWidths.LargestCircle(P((0, 0), (6, 0), (6, 1.2), (0, 1.2)), none), 2);
        // A hall 3 × 3 with a stretch 0.9 wide off it: 0.9 at its narrowest; it still holds 3.
        var hall = P((0, 0), (3, 0), (3, 3), (2.1, 3), (2.1, 5), (1.2, 5), (1.2, 3), (0, 3));
        Assert.Equal(0.9, RoomWidths.Least(hall, none)!.Value, 6);
        Assert.Equal(3.0, RoomWidths.LargestCircle(hall, none), 2);
        // A column 1 × 1 in a room 3 × 3: 1 m either side of it; the largest circle sits in a corner, touching two walls and the
        // column's corner: x = √2 (1 − x), a diameter of 2√2 / (1 + √2) = 1.1716.
        var room = P((0, 0), (3, 0), (3, 3), (0, 3));
        var column = P((1, 1), (1, 2), (2, 2), (2, 1));
        Assert.Equal(1.0, RoomWidths.Least(room, [column])!.Value, 6);
        Assert.Equal(2 * Math.Sqrt(2) / (1 + Math.Sqrt(2)), RoomWidths.LargestCircle(room, [column]), 2);
        // Too few points: nothing to measure.
        Assert.Null(RoomWidths.Least(P((0, 0), (1, 0)), none));
    }

    // ─── Through a file ──────────────────────────────────────────────────────

    [Fact]
    public void A_pack_with_its_climate_and_accessibility_goes_through_a_file_and_back()
    {
        var a = LoadCaseTests.F();
        var pack = Copy(a, "Sweden (example)");
        a.Run("rules.update", new JsonObject { ["id"] = pack, ["section"] = "climate", ["values"] = Kommuns() });
        a.Run("rules.update", new JsonObject { ["id"] = pack, ["section"] = "accessibility", ["values"] = new JsonObject { ["doorClearWidth"] = 0.8, ["turningDiameter"] = 1.5, ["checkRamps"] = true } });
        var text = (string)a.Session.Query("rules.export", new JsonObject { ["id"] = pack })!["text"]!;
        var file = JsonNode.Parse(text)!;
        Assert.True((bool)file["indicative"]!);
        Assert.Equal("Kiruna", (string)file["climate"]!["regions"]![0]!["name"]!);
        Assert.Equal(0.8, (double)file["accessibility"]!["doorClearWidth"]!);

        var b = LoadCaseTests.F();
        var made = (string)b.Run("rules.import", new JsonObject { ["text"] = text, ["activate"] = true })!["id"]!;
        var mine = a.Session.Query("rules.get", new JsonObject { ["id"] = pack })!["sections"]!;
        var got = b.Session.Query("rules.get", new JsonObject { ["id"] = made })!["sections"]!;
        Assert.True(JsonNode.DeepEquals(mine["climate"], got["climate"]));
        Assert.True(JsonNode.DeepEquals(mine["accessibility"], got["accessibility"]));
        b.Run("analysis.setClimate", new JsonObject { ["region"] = "Kiruna" });
        Assert.Equal(5.5, Lapin.Analysis.Climate.Snow(b.Model)!.S0k);
        // The same file a second time: the same pack, kept.
        Assert.Equal("same", (string?)b.Session.Query("library.preview", new JsonObject { ["text"] = text })!["rows"]![0]!["status"]);
    }
}
