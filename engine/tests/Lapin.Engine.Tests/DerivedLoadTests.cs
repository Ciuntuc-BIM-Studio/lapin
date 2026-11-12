using System.Text.Json.Nodes;
using Lapin.Analysis;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The loads derived from the model (docs/plan-fem.md §8.4), worked out on every ask: a floor's layers outside its core
/// (5 cm of screed at 2200 kg/m³ → 1.079 kN/m²), a wall outside the analytical model (an AAC partition 2.5 m high → its line
/// load along its axis), a room's imposed load by its category (4 × 5 m of category A → 40 kN), the category derived from the
/// room's function, and none for a room set to none.
/// </summary>
public class DerivedLoadTests
{
    const double G = 9.80665;

    static Fixture F() => LoadCaseTests.F();

    static void Density(Fixture f, string material, double rho)
    {
        var id = f.Model.OfClass(IfcClass.Material).First(m => m.Name == material).Id;
        f.Run("material.setData", new JsonObject
        {
            ["id"] = id,
            ["params"] = new JsonArray(new JsonObject { ["pset"] = "Pset_MaterialCommon", ["name"] = "MassDensity", ["value"] = rho }),
        });
    }

    static List<JsonObject> Derived(Fixture f, string kind) =>
        LoadCaseTests.Cases(f)["derived"]!.AsArray().Select(d => d!.AsObject()).Where(d => (string?)d["kind"] == kind).ToList();

    static JsonObject Preview(Fixture f, string code) => f.Session.Query("analysis.preview", new JsonObject { ["caseId"] = LoadCaseTests.IdOf(f, code) })!.AsObject();

    static string Room(Fixture f, double x, double w, double d, string? function = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x, ["y1"] = 0, ["x2"] = x + w, ["y2"] = d };
        if (function != null) a["function"] = function;
        return (string)f.Run("architecture.addRoom", a)!["id"]!;
    }

    [Fact]
    public void A_floors_screed_is_its_thickness_by_its_density_over_its_outline_at_its_top()
    {
        var f = F();
        f.Run("analysis.defaultCases", []);
        var type = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Floor 20 + screed",
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Screed", ["thickness"] = 0.05 }, new JsonObject { ["material"] = "Concrete C25/30", ["thickness"] = 0.2, ["core"] = true }),
        })!["id"]!;
        var slab = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 5), new JsonArray(0, 5)), ["typeId"] = type,
        })!["id"]!;
        // Without the screed's density, nothing: said in the notes.
        Assert.Empty(Derived(f, "finishes"));
        Assert.Contains(LoadCaseTests.Cases(f)["notes"]!.AsArray(), n => ((string)n!).Contains("Screed"));
        Density(f, "Screed", 2200);
        var d = Assert.Single(Derived(f, "finishes"));
        Assert.Equal(slab, (string?)d["sourceId"]);
        Assert.Equal(LoadCaseTests.IdOf(f, "G2"), (string?)d["caseId"]);
        Assert.Equal("kN/m²", (string?)d["unit"]);
        Assert.Equal(0.05 * 2200 * G / 1000, (double)d["value"]!, 6);
        Assert.Equal(1.079, Math.Round((double)d["value"]!, 3));
        Assert.Equal(0.05 * 2200 * G / 1000 * 30, (double)d["total"]!, 4);
        // At the floor's top face (the storey's level, offset 0), over its outline.
        var area = Assert.Single(Preview(f, "G2")["areas"]!.AsArray())!;
        Assert.Equal(0, (double)area["z"]!, 9);
        Assert.Equal(4, area["points"]!.AsArray().Count);
        Assert.True((bool)area["derived"]!);
        // The concrete of the core has no density by its name alone (plain concrete): its self weight is counted out and said.
        Assert.Empty(Derived(f, "selfWeight"));
        Assert.Contains(LoadCaseTests.Cases(f)["notes"]!.AsArray(), n => ((string)n!).Contains("no density"));
        Density(f, "Concrete C25/30", 2500);
        var sw = Assert.Single(Derived(f, "selfWeight"));
        Assert.Equal(6 * 5 * 0.2 * 2500 * G / 1000, (double)sw["total"]!, 3);
        Assert.Equal(6 * 5 * 0.2 * 2500 * G / 1000, (double)LoadCaseTests.Cases(f)["cases"]!.AsArray().Single(c => (string?)c!["code"] == "G1")!["total"]!, 3);
    }

    [Fact]
    public void A_partition_is_its_bodys_weight_along_its_axis_at_its_base()
    {
        var f = F();
        f.Run("analysis.defaultCases", []);
        var type = f.Type("AAC 25", ("BCA", 0.25, true));
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 0, ["typeId"] = type, ["height"] = 2.5 })!["id"]!;
        Density(f, "BCA", 600);
        var d = Assert.Single(Derived(f, "partitions"));
        Assert.Equal(wall, (string?)d["sourceId"]);
        Assert.Equal(LoadCaseTests.IdOf(f, "G2"), (string?)d["caseId"]);
        Assert.Equal("kN/m", (string?)d["unit"]);
        // 0.25 m × 2.5 m × 600 kg/m³ × g = 3.677 kN/m, over 4 m.
        Assert.Equal(0.25 * 2.5 * 600 * G / 1000, (double)d["value"]!, 6);
        Assert.Equal(3.677, Math.Round((double)d["value"]!, 3));
        Assert.Equal(0.25 * 2.5 * 600 * G / 1000 * 4, (double)d["total"]!, 4);
        var line = Assert.Single(Preview(f, "G2")["lines"]!.AsArray())!;
        Assert.Equal(0, (double)line["z"]!, 9);
        Assert.Equal([0.0, 0.0], line["a"]!.AsArray().Select(x => (double)x!));
        Assert.Equal([4.0, 0.0], line["b"]!.AsArray().Select(x => (double)x!));
        // Load-bearing, it is in the analytical model: no partition any more, its weight is the self weight's.
        f.Run("element.setParam", new JsonObject { ["id"] = wall, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        Assert.Empty(Derived(f, "partitions"));
        Assert.Equal(0.25 * 2.5 * 600 * G / 1000 * 4, (double)Assert.Single(Derived(f, "selfWeight"))["total"]!, 4);
    }

    [Fact]
    public void A_room_of_category_A_carries_its_imposed_load_over_its_outline()
    {
        var f = F();
        f.Run("analysis.defaultCases", []);
        var room = Room(f, 0, 4, 5, "Living");
        var d = Assert.Single(Derived(f, "occupancy"));
        Assert.Equal(room, (string?)d["sourceId"]);
        Assert.Equal(LoadCaseTests.IdOf(f, "Q-A"), (string?)d["caseId"]);
        Assert.Equal(2.0, (double)d["value"]!, 9);
        Assert.Equal(40.0, (double)d["total"]!, 9);
        Assert.Equal(40.0, (double)LoadCaseTests.Cases(f)["cases"]!.AsArray().Single(c => (string?)c!["code"] == "Q-A")!["total"]!, 9);
        Assert.Equal(40.0, (double)LoadCaseTests.Cases(f)["storeys"]!.AsArray().Single(s => (string?)s!["id"] == f.Storey)!["byCase"]![LoadCaseTests.IdOf(f, "Q-A")]!, 9);
        var area = Assert.Single(Preview(f, "Q-A")["areas"]!.AsArray())!;
        Assert.Equal(2.0, (double)area["value"]!);
        Assert.Equal(0, (double)area["z"]!, 9);
        // The national annex's value, then back to the recommended one.
        f.Run("analysis.setImposedLoad", new JsonObject { ["category"] = "A", ["qk"] = 1.5 });
        Assert.Equal(30.0, (double)Assert.Single(Derived(f, "occupancy"))["total"]!, 9);
        var cat = f.Session.Query("analysis.categories", [])!.AsArray().Single(c => (string?)c!["key"] == "A")!;
        Assert.Equal(1.5, (double)cat["qk"]!);
        Assert.Equal(2.0, (double)cat["default"]!);
        Assert.True((bool)cat["overridden"]!);
        f.Run("analysis.setImposedLoad", new JsonObject { ["category"] = "A", ["qk"] = null });
        Assert.Equal(40.0, (double)Assert.Single(Derived(f, "occupancy"))["total"]!, 9);
        Assert.Equal(15, f.Session.Query("analysis.categories", [])!.AsArray().Count);
    }

    [Fact]
    public void A_rooms_category_comes_from_its_function_unless_it_has_its_own()
    {
        var f = F();
        f.Run("analysis.defaultCases", []);
        var office = Room(f, 0, 4, 5, "Office");
        var store = Room(f, 10, 2, 2, "Storage");
        var garage = Room(f, 20, 3, 6, "Garage");
        var balcony = Room(f, 30, 2, 1, "Balcony");
        var other = Room(f, 40, 1, 1, "Sauna");
        Assert.Equal("B", Categories.OfRoom(f.Model.Get(office)!));
        Assert.Equal("E1", Categories.OfRoom(f.Model.Get(store)!));
        Assert.Equal("F", Categories.OfRoom(f.Model.Get(garage)!));
        Assert.Equal("A-balcony", Categories.OfRoom(f.Model.Get(balcony)!));
        Assert.Equal("A", Categories.OfRoom(f.Model.Get(other)!));
        var rows = Derived(f, "occupancy").ToDictionary(d => (string)d["sourceId"]!);
        Assert.Equal(20 * 3.0, (double)rows[office]["total"]!, 9);
        Assert.Equal(4 * 7.5, (double)rows[store]["total"]!, 9);
        Assert.Equal(18 * 2.5, (double)rows[garage]["total"]!, 9);
        Assert.Equal(2 * 2.5, (double)rows[balcony]["total"]!, 9);
        // Only category A has a case: the others are missing, by category.
        Assert.Null((string?)rows[office]["caseId"]);
        Assert.Equal(LoadCaseTests.IdOf(f, "Q-A"), (string?)rows[other]["caseId"]);
        var missing = LoadCaseTests.Cases(f)["missing"]!.AsArray().ToDictionary(m => (string)m!["category"]!, m => m!["rooms"]!.AsArray().Select(x => (string)x!).ToList());
        Assert.Equal(["A-balcony", "B", "E1", "F"], missing.Keys.Order(StringComparer.Ordinal));
        Assert.Equal([office], missing["B"]);
        // Its own category wins over its function's, and makes the case it needs.
        f.Run("analysis.setOccupancy", new JsonObject { ["ids"] = new JsonArray(office), ["category"] = "C1" });
        Assert.Equal("C1", f.Model.Get(office)!.Str("loadCategory"));
        Assert.Equal(LoadCaseTests.IdOf(f, "Q-C1"), (string?)Derived(f, "occupancy").Single(d => (string?)d["sourceId"] == office)["caseId"]);
        Assert.Throws<CommandException>(() => f.Run("analysis.setOccupancy", new JsonObject { ["ids"] = new JsonArray(office), ["category"] = "Z" }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setOccupancy", new JsonObject { ["ids"] = new JsonArray(f.Storey), ["category"] = "A" }));
        f.Run("analysis.setOccupancy", new JsonObject { ["ids"] = new JsonArray(office), ["category"] = null });
        Assert.Null(f.Model.Get(office)!.Str("loadCategory"));
        Assert.Equal("B", Categories.OfRoom(f.Model.Get(office)!));
    }

    [Fact]
    public void A_room_set_to_none_carries_no_load()
    {
        var f = F();
        f.Run("analysis.defaultCases", []);
        var room = Room(f, 0, 4, 5, "Living");
        f.Run("analysis.setOccupancy", new JsonObject { ["id"] = room, ["category"] = "none" });
        Assert.Equal("none", f.Model.Get(room)!.Str("loadCategory"));
        Assert.Empty(Derived(f, "occupancy"));
        Assert.Empty(LoadCaseTests.Cases(f)["missing"]!.AsArray());
        Assert.Equal(0.0, (double)LoadCaseTests.Cases(f)["cases"]!.AsArray().Single(c => (string?)c!["code"] == "Q-A")!["total"]!);
        Assert.Empty(Preview(f, "Q-A")["areas"]!.AsArray());
    }
}
