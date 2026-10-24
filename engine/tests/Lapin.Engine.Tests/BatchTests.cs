using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>edit.batch: several module commands as one edit, one undo step, all or nothing.</summary>
public class BatchTests
{
    static JsonObject Step(string command, JsonObject args) => new() { ["command"] = command, ["args"] = args };

    [Fact]
    public void Two_walls_ends_moved_together_are_one_undo_step_and_a_failing_step_changes_nothing()
    {
        var f = new Fixture(new ArchitectureModule());
        var a = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 0, ["join"] = false })!["id"]!;
        var b = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 4, ["y1"] = 0, ["x2"] = 4, ["y2"] = 3, ["join"] = false })!["id"]!;
        f.Run("edit.batch", new JsonObject { ["steps"] = new JsonArray(
            Step("architecture.updateWall", new JsonObject { ["id"] = a, ["x2"] = 5, ["y2"] = 1, ["moveJoinedEnds"] = false }),
            Step("architecture.updateWall", new JsonObject { ["id"] = b, ["x1"] = 5, ["y1"] = 1, ["moveJoinedEnds"] = false })) });
        Assert.Equal(5, (double)f.Model.Get(a)!.Attrs["end"]![0]!, 9);
        Assert.Equal(5, (double)f.Model.Get(b)!.Attrs["start"]![0]!, 9);
        f.Session.Undo();
        Assert.Equal(4, (double)f.Model.Get(a)!.Attrs["end"]![0]!, 9);
        Assert.Equal(4, (double)f.Model.Get(b)!.Attrs["start"]![0]!, 9);
        // The second step refused: the first is not kept either.
        Assert.ThrowsAny<Exception>(() => f.Run("edit.batch", new JsonObject { ["steps"] = new JsonArray(
            Step("architecture.updateWall", new JsonObject { ["id"] = a, ["x2"] = 6, ["y2"] = 0 }),
            Step("architecture.updateWall", new JsonObject { ["id"] = "nope", ["x1"] = 6 })) }));
        Assert.Equal(4, (double)f.Model.Get(a)!.Attrs["end"]![0]!, 9);
        Assert.Throws<CommandException>(() => f.Run("edit.batch", new JsonObject { ["steps"] = new JsonArray() }));
    }
}
