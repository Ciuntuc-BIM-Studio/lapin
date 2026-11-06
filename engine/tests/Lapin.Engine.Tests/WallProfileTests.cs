using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>A wall by its profile (a tester's wish, #100, decision 372): a gable, steps, a slope; still a wall, its openings cut; flat again.</summary>
public class WallProfileTests
{
    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static JsonArray P(params double[] uv) => new([.. Enumerable.Range(0, uv.Length / 2).Select(i => (JsonNode)new JsonArray(uv[2 * i], uv[2 * i + 1]))]);

    [Fact]
    public void A_wall_takes_a_gable_or_steps_keeps_its_window_and_goes_flat_again()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 6, 0);
        var flat = Volume(f, wall);
        var height = f.Bounds(wall).Max.Z;
        // A gable: 5 m at the middle.
        f.Run("architecture.setWallProfile", new JsonObject { ["id"] = wall, ["top"] = P(0, height, 3, 5, 6, height) });
        Assert.Equal(5, f.Bounds(wall).Max.Z, 3);
        Assert.True(Volume(f, wall) > flat * 1.2);
        // A window in it still cuts the wall.
        var gable = Volume(f, wall);
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3.0 });
        Assert.True(Volume(f, wall) < gable - 0.05);
        // Steps: 3 m for the first 2 m, then 1.5 m.
        f.Run("architecture.setWallProfile", new JsonObject { ["id"] = wall, ["top"] = P(0, 3, 2, 3, 2, 1.5, 6, 1.5) });
        Assert.Equal(3, f.Bounds(wall).Max.Z, 3);
        // Flat again.
        f.Run("architecture.setWallProfile", new JsonObject { ["id"] = wall, ["top"] = null });
        Assert.Equal(height, f.Bounds(wall).Max.Z, 3);
        // Points out of order are refused.
        Assert.ThrowsAny<Exception>(() => f.Run("architecture.setWallProfile", new JsonObject { ["id"] = wall, ["top"] = P(0, 3, 4, 3, 2, 3) }));
    }
}
