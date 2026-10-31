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
}
