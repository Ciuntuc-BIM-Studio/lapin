using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>A door or window placed in the hand and to the side the tool chose (Space, the cursor's side of the wall), one undo step.</summary>
public class FillPlacingTests
{
    [Theory]
    [InlineData("architecture.addDoor", false, false)]
    [InlineData("architecture.addDoor", true, false)]
    [InlineData("architecture.addDoor", false, true)]
    [InlineData("architecture.addWindow", true, true)]
    public void It_is_placed_flipped_as_asked(string command, bool hand, bool side)
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 8, 0);
        var id = (string)f.Run(command, new JsonObject { ["wallId"] = wall, ["along"] = 3, ["flipHand"] = hand, ["flipSide"] = side })!["id"]!;
        var fill = f.Model.Get(id)!;
        Assert.Equal(hand, fill.Bool("flipHand"));
        Assert.Equal(side, fill.Bool("flipSide"));
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
    }

    [Fact]
    public void Unasked_it_stays_as_it_always_was()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 8, 0);
        var fill = f.Model.Get((string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 3 })!["id"]!)!;
        Assert.False(fill.Bool("flipHand"));
        Assert.False(fill.Bool("flipSide"));
    }
}
