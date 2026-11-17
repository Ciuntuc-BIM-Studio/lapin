using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Issue #83 (forum 137): a prefix and a suffix for each family of a grid's labels (Annotate → Axis grid…), and several
/// levels made at once from a list of storey heights (storey.createMany), one undo step, the levels already there untouched.
/// </summary>
public class AxisGridLevelsTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(params double[] v) => new(v.Select(x => (JsonNode)x).ToArray());

    [Fact]
    public void Each_family_of_a_grid_takes_its_own_prefix_and_suffix_around_the_count()
    {
        var f = F();
        var r = f.Run("drafting.makeGrid", new JsonObject
        {
            ["x"] = J(0.1, 0.3, 0.3, 0.2), ["y"] = J(5, 5),
            ["prefixX"] = "X-", ["suffixX"] = "'", ["prefixY"] = "", ["suffixY"] = "1",
        })!;
        var axes = Grids.Axes(f.Model.Get((string)r["gridId"]!)!);
        Assert.Equal(["X-1'", "X-2'", "X-3'", "X-4'", "X-5'", "A1", "B1", "C1"], axes.Select(a => a.Tag));
        // The count runs on the first label itself, whatever is around it: down from 10, letters from AA.
        var g = F();
        var axes2 = Grids.Axes(g.Model.Get((string)g.Run("drafting.makeGrid", new JsonObject
        {
            ["x"] = J(6, 6), ["y"] = J(5), ["firstX"] = "10", ["descendingX"] = true, ["prefixX"] = "N", ["firstY"] = "Z", ["suffixY"] = "/b",
        })!["gridId"]!)!);
        Assert.Equal(["N10", "N9", "N8", "Z/b", "AA/b"], axes2.Select(a => a.Tag));
    }

    [Fact]
    public void A_prefix_or_suffix_that_would_break_an_axis_is_refused()
    {
        var f = F();
        // # names an axis inside its grid (grid#tag).
        Assert.Throws<CommandException>(() => f.Run("drafting.makeGrid", new JsonObject { ["x"] = J(6), ["prefixX"] = "#" }));
        // Two families labelled alike collide.
        Assert.Throws<CommandException>(() => f.Run("drafting.makeGrid", new JsonObject { ["x"] = J(6), ["y"] = J(5), ["firstY"] = "1" }));
    }

    static List<Entity> Storeys(Fixture f) => Spatial.Storeys(f.Model).ToList();

    [Fact]
    public void Several_levels_are_made_at_once_from_their_heights_named_by_a_pattern_in_one_undo_step()
    {
        var f = F();
        var before = Storeys(f).Select(s => (s.Id, s.Name, s.Num("elevation"), s.Num("height", 3))).ToList();
        var top = Storeys(f).Max(s => s.Num("elevation") + s.Num("height", 3));
        var r = f.Run("storey.createMany", new JsonObject { ["heights"] = J(3, 3, 3, 2.8), ["name"] = "Floor {n}", ["start"] = 2 })!;
        Assert.Equal(4, r["ids"]!.AsArray().Count);
        var made = Storeys(f).Where(s => r["ids"]!.AsArray().Any(x => (string?)x == s.Id)).OrderBy(s => s.Num("elevation")).ToList();
        Assert.Equal(["Floor 2", "Floor 3", "Floor 4", "Floor 5"], made.Select(s => s.Name));
        Assert.Equal([top, top + 3, top + 6, top + 9], made.Select(s => Math.Round(s.Num("elevation"), 9)));
        Assert.Equal([3, 3, 3, 2.8], made.Select(s => s.Num("height", 3)));
        // The levels already there are as they were.
        Assert.Equal(before, Storeys(f).Where(s => made.All(m => m.Id != s.Id)).Select(s => (s.Id, s.Name, s.Num("elevation"), s.Num("height", 3))).ToList());
        // One undo step takes them all away.
        f.Session.Undo();
        Assert.Equal(before.Select(b => b.Id), Storeys(f).Select(s => s.Id));
    }

    [Fact]
    public void Levels_start_from_a_base_level_given_and_are_numbered_after_the_storeys_by_default()
    {
        var f = F();
        var count = Storeys(f).Count;
        var r = f.Run("storey.createMany", new JsonObject { ["heights"] = J(3.5, 3), ["elevation"] = 20 })!;
        var made = r["ids"]!.AsArray().Select(x => f.Model.Get((string)x!)!).ToList();
        Assert.Equal([$"Level {count}", $"Level {count + 1}"], made.Select(s => s.Name));
        Assert.Equal([20, 23.5], made.Select(s => s.Num("elevation")));
    }

    [Fact]
    public void Levels_with_no_heights_bad_heights_or_one_name_for_many_are_refused()
    {
        var f = F();
        var count = Storeys(f).Count;
        Assert.Throws<CommandException>(() => f.Run("storey.createMany", new JsonObject { ["heights"] = J() }));
        Assert.Throws<CommandException>(() => f.Run("storey.createMany", new JsonObject { ["heights"] = J(3, 0) }));
        Assert.Throws<CommandException>(() => f.Run("storey.createMany", new JsonObject { ["heights"] = J(3, 300) }));
        Assert.Throws<CommandException>(() => f.Run("storey.createMany", new JsonObject { ["heights"] = J(3, 3), ["name"] = "Attic" }));
        // One level may take a plain name.
        f.Run("storey.createMany", new JsonObject { ["heights"] = J(2.5), ["name"] = "Attic" });
        Assert.Equal(count + 1, Storeys(f).Count);
        Assert.Contains(Storeys(f), s => s.Name == "Attic");
    }
}
