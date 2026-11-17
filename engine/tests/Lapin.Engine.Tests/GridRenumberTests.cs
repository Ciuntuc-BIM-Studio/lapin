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

    // Issue #64: descending order and the reverse directions, in Renumber axes and in Axis grid's first labels.
    [Fact]
    public void Labels_count_down_and_Prev_is_Next_s_inverse()
    {
        Assert.Equal(["5", "4", "3"], Grids.Labels(3, "numbers", "5", descending: true));
        Assert.Equal(["10", "9", "8"], Grids.Labels(3, "numbers", "10", descending: true));
        Assert.Equal(["05", "04", "03"], Grids.Labels(3, "numbers", "05", descending: true));
        Assert.Equal(["S2", "S1", "S0"], Grids.Labels(3, "numbers", "2", "S", descending: true));
        // No start: down to 1 or A, the ascending labels the other way round.
        Assert.Equal(["4", "3", "2", "1"], Grids.Labels(4, "numbers", null, descending: true));
        Assert.Equal(["K", "J", "H", "G", "F", "E", "D", "C", "B", "A"], Grids.Labels(10, "letters", "", descending: true));
        Assert.Equal(["P", "N", "M"], Grids.Labels(3, "letters", "P", descending: true));
        Assert.Equal(["P", "O", "N"], Grids.Labels(3, "letters", "P", skipIO: false, descending: true));
        Assert.Equal(["AB", "AA", "Z", "Y"], Grids.Labels(4, "letters", "AB", descending: true));
        Assert.Throws<CommandException>(() => Grids.Labels(3, "numbers", "1", descending: true));
        Assert.Throws<CommandException>(() => Grids.Labels(3, "letters", "B", descending: true));
        // Ascending as before.
        Assert.Equal(["9", "10", "11"], Grids.Labels(3, "numbers", "9"));

        foreach (var t in new[] { "2", "10", "B", "AA", "BA", "Z", "A2", "b", "05" }) Assert.Equal(t, Grids.Next(Grids.Prev(t)!).PadLeft(t.Length, '0'));
        Assert.Equal("AZ", Grids.Prev("BA"));
        Assert.Equal("Z", Grids.Prev("AA"));
        Assert.Null(Grids.Prev("A"));
        Assert.Null(Grids.Prev("0"));
        Assert.Null(Grids.Prev("-"));
        Assert.Equal(["C", "B", "A"], Grids.Series("C", 3, descending: true));
        Assert.Throws<CommandException>(() => Grids.Series("B", 3, descending: true));
    }

    [Fact]
    public void Axes_are_renumbered_right_to_left_top_to_bottom_and_counting_down_in_one_undo_step()
    {
        var f = F();
        var r = f.Run("drafting.makeGrid", new JsonObject { ["x"] = J(6, 6, 6), ["y"] = J(5, 5) })!;
        var grid = (string)r["gridId"]!;
        Assert.Equal(["1", "2", "3", "4", "A", "B", "C"], Tags(f, grid));
        // Right to left: the axis on the right is 1.
        f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid, ["direction"] = "x", ["reverse"] = true });
        Assert.Equal(["4", "3", "2", "1", "A", "B", "C"], Tags(f, grid));
        // Top to bottom: the top axis is A.
        f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid, ["direction"] = "y", ["reverse"] = true });
        Assert.Equal(["4", "3", "2", "1", "C", "B", "A"], Tags(f, grid));
        f.Session.Undo();
        Assert.Equal(["4", "3", "2", "1", "A", "B", "C"], Tags(f, grid));
        // Left to right, counting down from 14.
        f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid, ["direction"] = "x", ["start"] = "14", ["descending"] = true });
        Assert.Equal(["14", "13", "12", "11", "A", "B", "C"], Tags(f, grid));
        // Right to left counting down from the default: the same as left to right counting up.
        f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid, ["direction"] = "x", ["reverse"] = true, ["descending"] = true });
        Assert.Equal(["1", "2", "3", "4", "A", "B", "C"], Tags(f, grid));
        // The selected axes, top to bottom, counting down from E.
        f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid, ["tags"] = new JsonArray("A", "B"), ["scheme"] = "letters", ["start"] = "E", ["reverse"] = true, ["descending"] = true });
        Assert.Equal(["1", "2", "3", "4", "D", "E", "C"], Tags(f, grid));
        // Counting down past 0 is refused, and nothing changes.
        Assert.Throws<CommandException>(() => f.Run("drafting.renumberAxes", new JsonObject { ["gridId"] = grid, ["direction"] = "x", ["start"] = "2", ["descending"] = true }));
        Assert.Equal(["1", "2", "3", "4", "D", "E", "C"], Tags(f, grid));
    }

    [Fact]
    public void A_grid_s_first_labels_run_the_other_way_and_count_down()
    {
        var f = F();
        var r = f.Run("drafting.makeGrid", new JsonObject { ["x"] = J(6, 6, 6), ["y"] = J(5, 5), ["reverseX"] = true, ["reverseY"] = true })!;
        var grid = (string)r["gridId"]!;
        // Made left to right and bottom to top, labelled right to left and top to bottom.
        Assert.Equal(["4", "3", "2", "1", "C", "B", "A"], Tags(f, grid));
        Assert.Equal(18, Grids.Axes(f.Model.Get(grid)!).Single(a => a.Tag == "1").A.X, 6);
        f.Run("drafting.makeGrid", new JsonObject { ["x"] = J(6, 6), ["y"] = J(5), ["firstX"] = "10", ["descendingX"] = true, ["firstY"] = "C", ["descendingY"] = true, ["replace"] = true });
        Assert.Equal(["10", "9", "8", "C", "B"], Tags(f, grid));
        // Both: counting down from 3, right to left, ends on 3 at the right (1 2 3).
        f.Run("drafting.makeGrid", new JsonObject { ["x"] = J(6, 6), ["y"] = J(5), ["firstX"] = "3", ["descendingX"] = true, ["reverseX"] = true, ["replace"] = true });
        Assert.Equal(["1", "2", "3", "A", "B"], Tags(f, grid));
        // Running out of labels is refused.
        Assert.Throws<CommandException>(() => f.Run("drafting.makeGrid", new JsonObject { ["x"] = J(6, 6), ["firstX"] = "1", ["descendingX"] = true, ["replace"] = true }));
        // The default as before.
        f.Run("drafting.makeGrid", new JsonObject { ["x"] = J(6), ["y"] = J(5), ["replace"] = true });
        Assert.Equal(["1", "2", "A", "B"], Tags(f, grid));
    }
}
