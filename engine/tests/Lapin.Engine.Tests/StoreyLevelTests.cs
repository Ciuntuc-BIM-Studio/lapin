using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>A storey between its base level and its top level, either above or below ±0.00: its height is what lies between.</summary>
public class StoreyLevelTests
{
    [Fact]
    public void A_storey_is_set_by_its_base_and_top_levels_positive_or_negative()
    {
        var f = new Fixture();
        double Num(string k) => f.Model.Get(f.Storey)!.Num(k);
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["elevation"] = -3.2, ["top"] = -0.15 });
        Assert.Equal(-3.2, Num("elevation"), 9);
        Assert.Equal(3.05, Num("height"), 9);
        // The top alone: the base stays.
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["top"] = 0.0 });
        Assert.Equal(3.2, Num("height"), 9);
        // The base alone keeps the top where it is.
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["elevation"] = -2.8, ["keepTop"] = true });
        Assert.Equal(2.8, Num("height"), 9);
        Assert.Throws<CommandException>(() => f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["top"] = -2.9 }));
        Assert.Throws<CommandException>(() => f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["elevation"] = 1, ["keepTop"] = true }));
        // A new storey can be given its levels too.
        var up = (string)f.Run("storey.create", new JsonObject { ["name"] = "Attic", ["elevation"] = 3.0, ["top"] = 5.6 })!["id"]!;
        Assert.Equal(2.6, f.Model.Get(up)!.Num("height"), 9);
    }
}
