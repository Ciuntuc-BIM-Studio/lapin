using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The tester's report #68: a whole grid made from its spacings (Annotate → Grid…), and levels written in the project's
/// units (feet and inches in an imperial project) in sections, elevations and level dimensions.
/// </summary>
public class GridAndLevelUnitTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static List<string> Texts(Fixture f, string view) => ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!)["pens"]!.AsArray()
        .OfType<JsonObject>().Where(p => p["texts"] != null).SelectMany(p => p["texts"]!.AsArray()).Select(t => (string)t!["text"]!).ToList();
    static JsonArray J(params double[] v) => new(v.Select(x => (JsonNode)x).ToArray());

    [Fact]
    public void A_grid_is_made_from_its_spacings_numbered_across_and_lettered_along()
    {
        var f = F();
        var r = f.Run("drafting.makeGrid", new JsonObject { ["x"] = J(6, 6, 4.5), ["y"] = J(5, 5), ["origin"] = J(1, 2), ["extend"] = 1 })!;
        var grid = f.Model.Get((string)r["gridId"]!)!;
        var axes = Grids.Axes(grid);
        Assert.Equal(["1", "2", "3", "4", "A", "B", "C"], axes.Select(a => a.Tag));
        var four = axes.Single(a => a.Tag == "4");
        Assert.Equal(1 + 16.5, four.A.X, 9);
        Assert.Equal(2 - 1, four.A.Y, 9);                    // past the first lettered axis by the extension
        Assert.Equal(2 + 10 + 1, four.B.Y, 9);
        var c = axes.Single(a => a.Tag == "C");
        Assert.Equal(12, c.A.Y, 9);
        Assert.Equal(1 + 16.5 + 1, c.B.X, 9);
        // Again with the same labels: refused, unless the grid is replaced; then labels of one's own.
        Assert.Throws<CommandException>(() => f.Run("drafting.makeGrid", new JsonObject { ["x"] = J(3) }));
        f.Run("drafting.makeGrid", new JsonObject { ["x"] = J(3), ["y"] = J(4), ["replace"] = true, ["firstX"] = "A", ["firstY"] = "1" });
        Assert.Equal(["A", "B", "1", "2"], Grids.Axes(f.Model.Get(grid.Id)!).Select(a => a.Tag));
        Assert.Throws<CommandException>(() => f.Run("drafting.makeGrid", new JsonObject { ["x"] = J(0) }));
        Assert.Throws<CommandException>(() => f.Run("drafting.makeGrid", new JsonObject()));
    }

    [Fact]
    public void Levels_are_written_in_feet_and_inches_in_an_imperial_project()
    {
        Assert.Equal("±0\"", Standards.FormatLevel(0, "ft-in"));
        Assert.Equal("+10'-0\"", Standards.FormatLevel(3.048, "ft-in"));
        Assert.Equal("−18\"", Standards.FormatLevel(-0.4572, "in"));
        Assert.Equal("+3.06", Standards.FormatLevel(3.06, "mm"));         // a metric project keeps metres, as drawings do
        var f = F();
        f.Run("storey.create", new JsonObject { ["elevation"] = 3.048 });
        f.Wall(0, 0, 6, 0);
        var s = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(3, -2), ["end"] = J(3, 2) })!["id"]!;
        Assert.Contains("Ground floor +0.00", Texts(f, s));
        f.Run("standards.options", new JsonObject { ["units"] = "ft-in" });
        var texts = Texts(f, s);
        Assert.Contains("Ground floor ±0\"", texts);
        Assert.Contains(texts, t => t.EndsWith("+10'-0\""));
        // A level dimension in a plan follows too.
        var plan = "plan:" + f.Storey;
        f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["dimKind"] = "level", ["a"] = J(1, 1), ["at"] = J(3, 1), ["z"] = -0.4572 });
        Assert.Contains("−1'-6\"", Texts(f, plan));
    }
}
