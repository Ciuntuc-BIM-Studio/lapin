using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>False ceilings (report #44, decision 192): a grid of tiles hung from the structure, light panels and air diffusers in its cells, exported as IfcCovering.</summary>
public class CeilingTests
{
    static (Fixture F, string Id) Ceiling(double w = 3.6, double l = 4.8, JsonObject? spec = null)
    {
        var f = new Fixture();
        var args = new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(w, 0), new JsonArray(w, l), new JsonArray(0, l)), ["height"] = 2.6 };
        if (spec != null) args["typeId"] = (string)f.Run("architecture.createCeilingType", new JsonObject { ["spec"] = spec })!["id"]!;
        return (f, (string)f.Run("architecture.addCeiling", args)!["id"]!);
    }

    [Fact]
    public void A_grid_ceiling_lays_its_tiles_centred_hangs_on_rods_and_sits_at_its_height()
    {
        var (f, id) = Ceiling();
        var g = f.Session.Geometry(id)!;
        var tiles = g.Parts.Single(p => p.Key == "tiles");
        // 3.6 × 4.8 with the grid centred: 600 mm cells, the edge cells cut to 300 mm, the whole ceiling covered.
        Assert.Equal(3.6 * 4.8 * 0.015, Measure.Volume(tiles.Solid), 4);
        Assert.Equal(2.6 + 0.003, Measure.Bounds(tiles.Solid)!.Value.Min.Z, 6);
        Assert.Contains(g.Parts, p => p.Key == "grid");
        var hangers = Measure.Bounds(g.Parts.Single(p => p.Key == "hangers").Solid)!.Value;
        Assert.Equal(3.0, hangers.Max.Z, 6);
        var grid = Ceilings.GridOf(f.Model, f.Model.Get(id)!);
        // Centred: 3.6 / 0.6 = 6 cells, offset half a cell → 5 whole cells and two halves across.
        Assert.Equal(5 * 7, grid.WholeCells().Count());
    }

    [Fact]
    public void Light_panels_and_diffusers_take_whole_cells_by_a_click_or_laid_out_and_are_counted()
    {
        var (f, id) = Ceiling();
        var r = f.Run("architecture.ceilingFixture", new JsonObject { ["id"] = id, ["kind"] = "light", ["x"] = 1.8, ["y"] = 2.4 })!;
        Assert.Equal(1, (int)r["count"]!);
        Assert.Throws<CommandException>(() => f.Run("architecture.ceilingFixture", new JsonObject { ["id"] = id, ["kind"] = "light", ["x"] = 0.1, ["y"] = 0.1 }));
        f.Run("architecture.ceilingLayout", new JsonObject { ["id"] = id, ["kind"] = "diffuser", ["every"] = 3 });
        var g = f.Session.Geometry(id)!;
        Assert.Contains(g.Parts, p => p.Key == "lights");
        Assert.Contains(g.Parts, p => p.Key == "diffusers");
        var q = f.Session.Quantities(id).Single(s => s.Name == "Lapin_CeilingQuantities").Quantities.ToDictionary(x => x.Name, x => x.Value);
        Assert.True(q["AirDiffusers"] >= 4);
        Assert.Equal(1, q["LightPanels"]);
        // A tile gives way to each fixture.
        Assert.Equal(35 - q["AirDiffusers"] - 1, q["WholeTiles"]);
        // Clicking the cell again with none makes it a tile again.
        f.Run("architecture.ceilingFixture", new JsonObject { ["id"] = id, ["kind"] = "none", ["x"] = 1.8, ["y"] = 2.4 });
        Assert.Equal(0, f.Session.Quantities(id).Single(s => s.Name == "Lapin_CeilingQuantities").Quantities.Single(x => x.Name == "LightPanels").Value);
    }

    [Fact]
    public void A_ceiling_fills_the_room_clicked_and_a_plain_board_is_one_piece()
    {
        var f = new Fixture();
        foreach (var (a, b) in new[] { ((0.0, 0.0), (5.0, 0.0)), ((5.0, 0.0), (5.0, 4.0)), ((5.0, 4.0), (0.0, 4.0)), ((0.0, 4.0), (0.0, 0.0)) })
            f.Wall(a.Item1, a.Item2, b.Item1, b.Item2);
        f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 });
        var plain = (string)f.Run("architecture.createCeilingType", new JsonObject { ["spec"] = CeilingSpec.Plain().ToJson() })!["id"]!;
        var id = (string)f.Run("architecture.addCeiling", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2, ["typeId"] = plain })!["id"]!;
        var g = f.Session.Geometry(id)!;
        var board = Assert.Single(g.Parts);
        var box = Measure.Bounds(board.Solid)!.Value;
        // Inside the walls' faces, not over them.
        Assert.True(box.Max.X < 5 && box.Min.X > 0);
        Assert.Throws<CommandException>(() => f.Run("architecture.addCeiling", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 9, ["y"] = 9 }));
    }

    [Fact]
    public void A_ceiling_goes_to_IFC_as_a_covering_of_type_ceiling()
    {
        var (f, _) = Ceiling();
        var path = Path.Combine(Path.GetTempPath(), $"ceiling-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var covering = Assert.Single(StepFile.Read(path).OfType("IFCCOVERING"));
            Assert.Equal(new StepEnum("CEILING"), covering[8]);
        }
        finally { File.Delete(path); }
    }
}
