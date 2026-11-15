using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Audit 15.11.2026, M2: the quantity takeoff took the first set without a part as the element's, so a stair's and a ramp's
/// concrete (in a ConcreteVolume no takeoff column reads) was missed, and a balcony on supports showed only their count.
/// The flights named their concrete by name where an id is expected, so material rules and the estimate skipped them, and
/// the landings and winders had no set. The whole element's set comes first now with its volume, every concrete part
/// (flights, landings, winders) has its set with its volume and its material's id, and a balcony's sets are all parts.
/// </summary>
public class StairRampBalconyTakeoffTests
{
    /// <summary>A project with a concrete (the stairs' and ramps' material by default) and, when asked, a storey above.</summary>
    static Fixture Project(bool above = true)
    {
        var f = new Fixture(new ArchitectureModule());
        f.Run("material.create", new JsonObject { ["name"] = "Concrete" });
        if (above) f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 });
        return f;
    }

    static void Near(double expected, double actual, double tol) => Assert.True(Math.Abs(expected - actual) <= tol, $"{actual} for {expected}");

    static string FromLibrary(Fixture f, string key) =>
        (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["library"] = key, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0 })!["id"]!;

    static string Takeoff(Fixture f, JsonObject? options = null)
    {
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "quantities" })!["id"]!;
        if (options != null) { options["id"] = id; f.Run("architecture.updateSchedule", options); }
        return id;
    }

    static ScheduleGrid Grid(Fixture f, string id) => new QuantityTakeoff().Grid(f.Session.Reader, f.Model.Get(id)!);
    static double Cell(ScheduleGrid g, IReadOnlyList<string> row, string column) =>
        g.Columns.ToList().IndexOf(column) is var i and >= 0 && row[i].Length > 0 ? double.Parse(row[i], CultureInfo.InvariantCulture) : 0;

    /// <summary>The concrete of the element's geometry: its flights, landings and winders.</summary>
    static double Concrete(Fixture f, string id) => f.Session.Geometry(id)!.Parts
        .Where(p => p.Key != null && (p.Key.StartsWith("flight:", StringComparison.Ordinal) || p.Key.StartsWith("landing:", StringComparison.Ordinal) || p.Key == "winders"))
        .Sum(p => Measure.Volume(p.Solid));

    [Theory]
    [InlineData("dogleg", "Lapin_StairQuantities")]
    [InlineData("winder", "Lapin_StairQuantities")]
    public void A_stairs_concrete_is_in_its_whole_set_and_in_its_parts_with_the_materials_id(string key, string main)
    {
        var f = Project();
        var id = FromLibrary(f, key);
        var sets = f.Session.Quantities(id);
        var concrete = Concrete(f, id);
        Assert.True(concrete > 0.5);
        // The whole stair's set first, its concrete as its volume (and as ConcreteVolume, as before).
        Assert.Equal(main, sets[0].Name);
        Assert.Null(sets[0].Part);
        Assert.Equal(concrete, sets[0].Quantities.Single(q => q.Name == "NetVolume").Value, 9);
        Assert.Equal(concrete, sets[0].Quantities.Single(q => q.Name == "ConcreteVolume").Value, 9);
        // Every concrete part a set with its volume and the material's id: together the concrete.
        var parts = sets.Where(s => s.Part != null).ToList();
        Assert.Contains(parts, s => s.Part!.StartsWith("landing:", StringComparison.Ordinal) || s.Part == "winders");
        var built = f.Session.Geometry(id)!.Parts;
        Assert.All(parts, s =>
        {
            Assert.Equal(built.First(p => p.Key == s.Part).Material, s.Material);
            Assert.Equal(IfcClass.Material, f.Model.Get(s.Material!)!.Class);
        });
        var mat = parts[0].Material!;
        Assert.Equal(concrete, parts.Sum(s => s.Quantities.Single(q => q.Name == "NetVolume").Value), 9);
        // The takeoff: the stair's row has its concrete; by material, the concrete's row the same.
        var g = Grid(f, Takeoff(f));
        Near(concrete, Cell(g, g.Rows.Single(r => r[0] == "Stair"), "Net volume (m³)"), 0.006);
        var bm = Grid(f, Takeoff(f, new JsonObject { ["groupBy"] = new JsonArray("material"), ["classes"] = new JsonArray(IfcClass.Stair) }));
        Near(concrete, Cell(bm, bm.Rows.Single(r => r[0] == f.Model.Get(mat)!.Name), "Net volume (m³)"), 0.006);
    }

    [Fact]
    public void A_ramps_concrete_is_in_its_whole_set_and_its_flights_with_the_materials_id()
    {
        var f = Project(above: false);
        var id = FromLibrary(f, "entrance-ramp");
        var sets = f.Session.Quantities(id);
        var concrete = Concrete(f, id);
        Assert.Equal("Lapin_RampQuantities", sets[0].Name);
        Assert.Equal(concrete, sets[0].Quantities.Single(q => q.Name == "NetVolume").Value, 9);
        var mat = Materials.Find(f.Model, "Concrete")!.Id;
        Assert.All(sets.Where(s => s.Part != null), s => Assert.Equal(mat, s.Material));
        var g = Grid(f, Takeoff(f));
        Near(concrete, Cell(g, g.Rows.Single(r => r[0] == "Ramp"), "Net volume (m³)"), 0.006);
    }

    [Fact]
    public void A_balcony_on_supports_shows_its_slab_and_railing_as_one_without_them()
    {
        var f = new Fixture(new ArchitectureModule());
        var type = f.Type("Brick 25", ("Brick", 0.25, true));
        f.Wall(0, 0, 20, 0, type);
        var on = (string)f.Run("architecture.addBalcony", new JsonObject { ["storeyId"] = f.Storey, ["style"] = "brackets", ["x"] = 3, ["y"] = -0.5 })!["id"]!;
        var sets = f.Session.Quantities(on);
        // No set stands for the whole balcony: the supports' count is a part like the slab and the railing.
        Assert.All(sets, s => Assert.NotNull(s.Part));
        var slab = sets.Single(s => s.Name == "Qto_SlabBaseQuantities");
        var g = Grid(f, Takeoff(f, new JsonObject { ["classes"] = new JsonArray(f.Model.Get(on)!.Class) }));
        var row = g.Rows.First(r => r[0] != "Total");
        Near(slab.Quantities.Single(q => q.Name == "GrossArea").Value, Cell(g, row, "Gross area (m²)"), 0.006);
        Near(slab.Quantities.Single(q => q.Name == "NetVolume").Value, Cell(g, row, "Net volume (m³)"), 0.0006);
        Assert.True(Cell(g, row, "Length (m)") > 0);
        // The slab's material by its id.
        if (slab.Material != null) Assert.Equal(IfcClass.Material, f.Model.Get(slab.Material)!.Class);
    }
}
