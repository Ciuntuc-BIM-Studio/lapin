using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The small shared helpers (audit 15.11.2026): whole millimetres written one way, a storey's elevation and an element's base.</summary>
public class SmallFormatsTests
{
    [Theory]
    [InlineData(1.25, "1250")]
    [InlineData(0.0125, "12")]      // half a millimetre to even, as all the copies but CurtainCells' did
    [InlineData(0.0135, "14")]
    [InlineData(-0.0001, "0")]      // never „-0”
    [InlineData(-0.25, "-250")]
    [InlineData(0, "0")]
    public void Millimetres_are_whole_and_rounded_half_to_even(double metres, string mm) => Assert.Equal(mm, Numbers.Mm(metres));

    [Fact]
    public void An_elements_base_is_its_storeys_elevation_and_its_offset()
    {
        var f = new Fixture(new Lapin.Architecture.ArchitectureModule());
        var storey = (string)f.Run("storey.create", new System.Text.Json.Nodes.JsonObject { ["name"] = "L1", ["elevation"] = 3.2 })!["id"]!;
        var wall = (string)f.Run("architecture.addWall", new System.Text.Json.Nodes.JsonObject { ["storeyId"] = storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 0, ["baseOffset"] = 0.1 })!["id"]!;
        var w = f.Model.Get(wall)!;
        Assert.Equal(3.2, Spatial.Elevation(f.Model, w), 9);
        Assert.Equal(3.2 + w.Num("baseOffset"), Spatial.Base(f.Model, w), 9);
        Assert.Equal(0, Spatial.Elevation(f.Model, f.Model.Get(storey)!), 9);
    }
}
