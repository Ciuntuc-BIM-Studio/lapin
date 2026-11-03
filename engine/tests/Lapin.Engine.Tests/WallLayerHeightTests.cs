using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Wall layers of their own height (report #44, decision 191): a tiled dado to 1.50 m, a cladding stopping short of the floor.</summary>
public class WallLayerHeightTests
{
    [Fact]
    public void A_layer_goes_only_as_high_as_its_type_says_and_the_rest_of_the_wall_stays_whole()
    {
        var f = new Fixture();
        var type = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Tiled dado",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Render", ["thickness"] = 0.02, ["from"] = 0.3 },
                new JsonObject { ["material"] = "Brick", ["thickness"] = 0.25, ["core"] = true },
                new JsonObject { ["material"] = "Tiles", ["thickness"] = 0.01, ["to"] = 1.5 }),
        })!["id"]!;
        var wall = f.Wall(0, 0, 4, 0, type);
        var parts = f.Session.Geometry(wall)!.Parts;
        (Vec3 Min, Vec3 Max) B(int i) => Measure.Bounds(parts.Single(p => p.Key == $"layer:{i}").Solid)!.Value;
        Assert.Equal(0.3, B(0).Min.Z, 6);
        Assert.Equal(3.0, B(0).Max.Z, 6);
        Assert.Equal(0, B(1).Min.Z, 6);
        Assert.Equal(1.5, B(2).Max.Z, 6);
        Assert.Equal(4 * 0.01 * 1.5, Measure.Volume(parts.Single(p => p.Key == "layer:2").Solid), 6);
        // Kept in the type's layer set, read back as written.
        var layers = LayerSets.LayersOf(f.Model, type);
        Assert.Equal((null, 1.5), (layers[2].From, layers[2].To));
        Assert.Equal((0.3, (double?)null), (layers[0].From, layers[0].To));
        // A top under the bottom is refused.
        Assert.Throws<CommandException>(() => LayerSets.Parse(new JsonArray(new JsonObject { ["material"] = "Tiles", ["thickness"] = 0.01, ["from"] = 1.0, ["to"] = 0.5 })));
    }

    /// <summary>
    /// A layer of several materials up its height (decision 264): a bathroom's finish, adhesive and tiles to 2.10 m and
    /// plaster over them, on either layer of finish; the core and the other layers untouched, every piece measured in its material.
    /// </summary>
    [Fact]
    public void A_layer_changes_material_up_its_height_and_each_piece_is_measured_in_its_own()
    {
        var f = new Fixture();
        var type = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Bathroom",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Render", ["thickness"] = 0.02 },
                new JsonObject { ["material"] = "Brick", ["thickness"] = 0.25, ["core"] = true },
                new JsonObject
                {
                    ["material"] = "Plaster", ["thickness"] = 0.015,
                    ["parts"] = new JsonArray(new JsonObject { ["material"] = "Ceramic tiles", ["height"] = 2.1 }),
                }),
        })!["id"]!;
        var wall = f.Wall(0, 0, 4, 0, type);
        var parts = f.Session.Geometry(wall)!.Parts.Where(p => p.Key == "layer:2").ToList();
        Assert.Equal(2, parts.Count);
        var tiles = parts.Single(p => f.Model.Get(p.Material!)!.Name == "Ceramic tiles");
        var plaster = parts.Single(p => f.Model.Get(p.Material!)!.Name == "Plaster");
        Assert.Equal((0, 2.1), (Measure.Bounds(tiles.Solid)!.Value.Min.Z, Math.Round(Measure.Bounds(tiles.Solid)!.Value.Max.Z, 6)));
        Assert.Equal((2.1, 3.0), (Math.Round(Measure.Bounds(plaster.Solid)!.Value.Min.Z, 6), Math.Round(Measure.Bounds(plaster.Solid)!.Value.Max.Z, 6)));
        // The quantities: one set per material of the layer, its side area its own.
        var sets = f.Session.Quantities(wall).Where(q => q.Name == "Lapin_WallLayerQuantities").ToList();
        Assert.Equal(4 * 2.1, sets.Single(q => q.Material == tiles.Material).Quantities.Single(x => x.Name == "NetSideArea").Value, 6);
        Assert.Equal(4 * 0.9, sets.Single(q => q.Material == plaster.Material).Quantities.Single(x => x.Name == "NetSideArea").Value, 6);
        // A layer stopping under its pieces' top cuts them there; the stored pieces read back as written.
        var layers = LayerSets.LayersOf(f.Model, type);
        Assert.Equal(2.1, Assert.Single(layers[2].Parts!).Height, 9);
        Assert.Null(layers[0].Parts);
        Assert.Throws<CommandException>(() => LayerSets.Parse(new JsonArray(new JsonObject
        {
            ["material"] = "Plaster", ["thickness"] = 0.015, ["parts"] = new JsonArray(new JsonObject { ["material"] = "Tiles" }),
        })));
    }
}
