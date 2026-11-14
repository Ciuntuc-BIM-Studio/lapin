using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 75: a grid's axes renumbered in order across their family (left to right, bottom to top), in numbers or
/// letters (I and O left out), from a start, after a prefix; dimensions on the axes follow, in one undo step.
/// </summary>
public class GridRenumberTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(params double[] v) => new(v.Select(x => (JsonNode)x).ToArray());
    static List<string> Tags(Fixture f, string grid) => Grids.Axes(f.Model.Get(grid)!).Select(a => a.Tag).ToList();
    static void Axis(Fixture f, string tag, double x1, double y1, double x2, double y2) =>
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["tag"] = tag });

    [Fact]
    public void Axes_drawn_out_of_order_are_numbered_left_to_right_and_lettered_bottom_to_top()
    {
        var f = F();
        // Drawn in any order, some the other way round, labelled anyhow.
        Axis(f, "3", 6, -1, 6, 10);
        Axis(f, "7", 0, 10, 0, -1);
        Axis(f, "x", 12, -1, 12, 10);
        Axis(f, "Q", -1, 8, 13, 8);
        Axis(f, "Z", 13, 0, -1, 0);
        var grid = f.Model.OfClass(IfcClass.Grid).Single().Id;
        var r = f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid, ["direction"] = "x" })!;
        Assert.Equal(["2", "1", "3", "Q", "Z"], Tags(f, grid));
        Assert.Equal("1", (string)r["tags"]!["7"]!);
        f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid, ["direction"] = "y" });
        Assert.Equal(["2", "1", "3", "B", "A"], Tags(f, grid));
        // One undo step takes the renumbering back.
        f.Session.Undo();
        Assert.Equal(["2", "1", "3", "Q", "Z"], Tags(f, grid));
    }

    [Fact]
    public void Letters_leave_out_I_and_O_and_a_start_a_prefix_and_a_selection_are_taken()
    {
        Assert.Equal(["G", "H", "J", "K", "L", "M", "N", "P"], Grids.Labels(8, "letters", "G"));
        Assert.Equal(["X", "Y", "Z", "AA", "AB"], Grids.Labels(5, "letters", "X"));
        Assert.Equal(["H", "I", "J"], Grids.Labels(3, "letters", "H", skipIO: false));
        Assert.Equal(["A-01", "A-02"], Grids.Labels(2, "numbers", "01", "A-"));
        Assert.Throws<CommandException>(() => Grids.Labels(2, "numbers", "A"));

        var f = F();
        var r = f.Run("drafting.makeGrid", new JsonObject { ["x"] = J(6, 6, 6), ["y"] = J(5, 5) })!;
        var grid = (string)r["gridId"]!;
        // Only the axes chosen, from 10, with a prefix: the others keep theirs.
        f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid, ["tags"] = new JsonArray("2", "3"), ["start"] = "10", ["prefix"] = "S" });
        Assert.Equal(["1", "S10", "S11", "4", "A", "B", "C"], Tags(f, grid));
        // Letters for the numbered ones, from H: H, J, K, L (no I).
        f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid, ["direction"] = "x", ["scheme"] = "letters", ["start"] = "H" });
        Assert.Equal(["H", "J", "K", "L", "A", "B", "C"], Tags(f, grid));
        // A label another axis keeps is refused; axes of two directions at once too.
        Assert.Throws<CommandException>(() => f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid, ["tags"] = new JsonArray("H"), ["scheme"] = "letters", ["start"] = "B" }));
        Assert.Throws<CommandException>(() => f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid, ["tags"] = new JsonArray("H", "A") }));
        Assert.Throws<CommandException>(() => f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid }));
    }

    [Fact]
    public void Dimensions_on_the_axes_follow_a_renumbering_and_a_renamed_axis()
    {
        var f = F();
        Axis(f, "B", 6, -1, 6, 10);
        Axis(f, "A", 0, -1, 0, 10);
        var grid = f.Model.OfClass(IfcClass.Grid).Single().Id;
        var view = "plan:" + f.Storey;
        var d = (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = view, ["a"] = J(0, 5), ["b"] = J(6, 5), ["at"] = J(3, 6) })!["id"]!;
        Assert.Equal(["A", "B"], Dimensions.Stations(f.Model.Get(d)!).Select(s => s.Ref!.Tag));
        f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid, ["direction"] = "x" });
        Assert.Equal(["2", "1"], Tags(f, grid));
        Assert.Equal(["1", "2"], Dimensions.Stations(f.Model.Get(d)!).Select(s => s.Ref!.Tag));
        // The dimension still measures them: moving axis 2 moves its end.
        f.Run("drafting.updateAxis", new JsonObject { ["gridId"] = grid, ["tag"] = "2", ["x1"] = 7, ["x2"] = 7 });
        var drawing = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
        Assert.Contains("7000", drawing["pens"]!.AsArray().OfType<JsonObject>().Where(p => p["texts"] != null).SelectMany(p => p["texts"]!.AsArray()).Select(t => (string)t!["text"]!));
        // An axis renamed by hand keeps its dimensions too.
        f.Run("drafting.updateAxis", new JsonObject { ["gridId"] = grid, ["tag"] = "1", ["newTag"] = "K" });
        Assert.Equal(["K", "2"], Dimensions.Stations(f.Model.Get(d)!).Select(s => s.Ref!.Tag));
        f.Session.Undo();
        Assert.Equal(["1", "2"], Dimensions.Stations(f.Model.Get(d)!).Select(s => s.Ref!.Tag));
    }
}
