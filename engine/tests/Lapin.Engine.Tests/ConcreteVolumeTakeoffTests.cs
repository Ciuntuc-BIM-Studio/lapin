using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Civil;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Audit 15.11.2026, follow-up of M2: a retaining wall's and a beam-and-block floor's whole set gave their concrete only as a
/// ConcreteVolume, which no takeoff column nor estimate article reads. Their whole set has its NetVolume and GrossVolume now
/// (ConcreteVolume kept), a retaining wall's with its material's id, so a takeoff and the estimate count them.
/// </summary>
public class ConcreteVolumeTakeoffTests
{
    static string Takeoff(Fixture f, JsonObject? options = null)
    {
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "quantities" })!["id"]!;
        if (options != null) { options["id"] = id; f.Run("architecture.updateSchedule", options); }
        return id;
    }

    static ScheduleGrid Grid(Fixture f, string id) => new QuantityTakeoff().Grid(f.Session.Reader, f.Model.Get(id)!);
    static double Cell(ScheduleGrid g, IReadOnlyList<string> row, string column) =>
        g.Columns.ToList().IndexOf(column) is var i and >= 0 && row[i].Length > 0 ? double.Parse(row[i], CultureInfo.InvariantCulture) : 0;
    static void Near(double expected, double actual, double tol) => Assert.True(Math.Abs(expected - actual) <= tol, $"{actual} for {expected}");
    static double Q(QuantitySet s, string name) => s.Quantities.Single(q => q.Name == name).Value;

    [Fact]
    public void A_retaining_walls_concrete_is_its_volume_with_its_materials_id_and_the_takeoff_counts_it()
    {
        var f = new Fixture(new CivilModule(), new ArchitectureModule());
        var w = (string)f.Run("civil.addRetainingWall", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0)), ["thickness"] = 0.3, ["height"] = 2.0, ["footingWidth"] = 1.0, ["footingThickness"] = 0.3 })!["id"]!;
        var built = f.Session.Geometry(w)!.Parts.Sum(p => Measure.Volume(p.Solid));
        Assert.True(built > 5);
        var main = f.Session.Quantities(w).First(s => s.Part == null);
        Assert.Equal("Lapin_RetainingWallQuantities", main.Name);
        Assert.Equal(built, Q(main, "ConcreteVolume"), 9);
        Assert.Equal(built, Q(main, "NetVolume"), 9);
        Assert.Equal(built, Q(main, "GrossVolume"), 9);
        var mat = f.Model.Get(w)!.Str("material")!;
        Assert.Equal(mat, main.Material);
        Assert.Equal(IfcClass.Material, f.Model.Get(mat)!.Class);
        // The takeoff: the wall's row has its concrete; by material, the concrete's row the same.
        var g = Grid(f, Takeoff(f, new JsonObject { ["classes"] = new JsonArray(f.Model.Get(w)!.Class) }));
        Near(built, Cell(g, g.Rows.First(r => r[0] != "Total"), "Net volume (m³)"), 0.006);
        var bm = Grid(f, Takeoff(f, new JsonObject { ["groupBy"] = new JsonArray("material"), ["classes"] = new JsonArray(f.Model.Get(w)!.Class) }));
        Near(built, Cell(bm, bm.Rows.Single(r => r[0] == f.Model.Get(mat)!.Name), "Net volume (m³)"), 0.006);
    }

    [Fact]
    public void A_beam_and_block_floors_whole_set_has_the_floors_volume_and_the_takeoff_counts_it()
    {
        var f = new Fixture(new ArchitectureModule());
        var pts = new JsonArray(new JsonArray(0, 0), new JsonArray(5, 0), new JsonArray(5, 4), new JsonArray(0, 4));
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = pts })!["id"]!;
        var type = f.Model.RelatingOf(slab, IfcClass.RelDefinesByType)!.Id;
        f.Run("architecture.setBeamBlock", new JsonObject { ["id"] = type, ["spec"] = BeamBlockSpec.Default.ToJson() });
        var g = f.Session.Geometry(slab)!;
        var net = g.Parts.Sum(p => Measure.Volume(p.Solid));
        var concrete = Measure.Volume(g.Parts.Single(p => p.Key == "bb-concrete").Solid);
        var sets = f.Session.Quantities(slab);
        var main = sets.First(s => s.Part == null);
        Assert.Equal("Lapin_BeamBlockQuantities", main.Name);
        Assert.Equal(concrete, Q(main, "ConcreteVolume"), 9);
        // The floor whole, as its slab set has it: what is built (the joists' bearings in the supports too), and its outline by its depth.
        var slabSet = sets.Single(s => s.Name == "Qto_SlabBaseQuantities");
        Assert.Equal(net, Q(main, "NetVolume"), 9);
        Assert.Equal(Q(slabSet, "GrossVolume"), Q(main, "GrossVolume"), 9);
        // The parts with their materials' ids, as before.
        Assert.All(sets.Where(s => s.Part is "bb-joists" or "bb-blocks" or "bb-concrete"), s => Assert.Equal(IfcClass.Material, f.Model.Get(s.Material!)!.Class));
        var grid = Grid(f, Takeoff(f, new JsonObject { ["classes"] = new JsonArray(IfcClass.Slab) }));
        Near(net, Cell(grid, grid.Rows.First(r => r[0] != "Total"), "Net volume (m³)"), 0.006);
    }
}
