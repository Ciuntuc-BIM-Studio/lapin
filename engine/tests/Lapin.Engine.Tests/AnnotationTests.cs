using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Annotations on views: section marks and levels (core), grid axes,
/// associative dimensions and texts (drafting). The dimension cases are the
/// old Lapin's associative-dims.test.ts, on real walls.
/// </summary>
public class AnnotationTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonObject Draw(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static List<JsonObject> Pens(JsonObject d, string layer) => d["pens"]!.AsArray().OfType<JsonObject>().Where(p => (string)p["layer"]! == layer).ToList();
    static List<string> Texts(JsonObject d) => Pens(d, "TEXT").SelectMany(p => p["texts"]!.AsArray()).Select(t => (string)t!["text"]!).ToList();
    static JsonObject? Mark(JsonObject d, string id) => d["marks"]!.AsArray().OfType<JsonObject>().FirstOrDefault(m => (string)m["id"]! == id);

    // ------------------------------------------------------------ section marks and levels

    [Fact]
    public void A_plan_shows_every_section_s_line_with_its_arrows_and_name()
    {
        var f = F();
        f.Wall(0, 0, 6, 0);
        var s = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(3, -2), ["end"] = new JsonArray(3, 2), ["name"] = "A-A" })!["id"]!;
        var plan = Draw(f, "plan:" + f.Storey);
        Assert.Equal(2, Texts(plan).Count(t => t == "A-A"));
        var section = Pens(plan, "SECTION");
        Assert.Contains(section, p => p["dash"] != null);                              // the chain line
        Assert.Contains(section, p => (double)p["width"]! == Lapin.Drawing.Views.Pens.Default.Cut);         // the heavy ends
        Assert.NotNull(Mark(plan, s));                                                 // a click on it selects the section
    }

    [Fact]
    public void A_section_shows_the_levels_with_their_names_and_elevations()
    {
        var f = F();
        f.Run("storey.create", []);
        f.Wall(0, 0, 6, 0);
        var s = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(3, -2), ["end"] = new JsonArray(3, 2) })!["id"]!;
        var texts = Texts(Draw(f, s));
        Assert.Contains("Ground floor +0.00", texts);
        Assert.Contains("Level 1 +3.00", texts);
    }

    // ------------------------------------------------------------ grids

    [Fact]
    public void Axes_count_on_in_their_direction_letters_for_the_other_and_draw_in_the_axis_style()
    {
        Assert.Equal(["2", "10", "B", "AA", "A2", "b"], new[] { "1", "9", "A", "Z", "A1", "a" }.Select(Grids.Next));
        var f = F();
        string Tag(double x1, double y1, double x2, double y2) => (string)f.Run("drafting.addAxis", new JsonObject { ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 })!["tag"]!;
        Assert.Equal("1", Tag(0, -1, 0, 6));
        Assert.Equal("2", Tag(5, -1, 5, 6));
        Assert.Equal("A", Tag(-1, 0, 6, 0));
        Assert.Equal("B", Tag(-1, 4, 6, 4));
        Assert.Throws<CommandException>(() => f.Run("drafting.addAxis", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 1, ["y2"] = 0, ["tag"] = "A" }));
        var plan = Draw(f, "plan:" + f.Storey);
        // Bubbles at both ends (Standard: both), the tag in each.
        Assert.Equal(2, Texts(plan).Count(t => t == "1"));
        Assert.All(Pens(plan, "GRID").Where(p => p["dash"] != null), p => Assert.Equal(new[] { 4, 1, 0.3, 1 }, p["dash"]!.AsArray().Select(x => (double)x!)));
        // A section across the 1–2 axes sees them, one bubble on top each.
        var s = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, 2), ["end"] = new JsonArray(6, 2) })!["id"]!;
        f.Wall(0, 5, 6, 5);
        var texts = Texts(Draw(f, s));
        Assert.Contains("1", texts);
        Assert.Contains("2", texts);
        Assert.DoesNotContain("A", texts);
        // The project's default axis type changes how they look: square bubbles.
        f.Run("standards.set", new JsonObject { ["kind"] = "axis", ["entry"] = new JsonObject { ["id"] = "standard", ["bubble"] = "square", ["ends"] = "start" } });
        Assert.Equal(1, Texts(Draw(f, "plan:" + f.Storey)).Count(t => t == "1"));
        var grid = f.Model.OfClass(IfcClass.Grid).Single().Id;
        f.Run("drafting.removeAxis", new JsonObject { ["gridId"] = grid, ["tag"] = "B" });
        Assert.Equal(3, Grids.Axes(f.Model.Get(grid)!).Count);
    }

    // ------------------------------------------------------------ associative dimensions (associative-dims.test.ts)

    /// <summary>Walls 1 (x 0..0.3) and 2 (x 5..5.3), 4 m long north, centred on their axes; a dimension from wall 1's inner face to wall 2's.</summary>
    static (Fixture F, string W1, string W2, string Dim) Placed()
    {
        var f = F();
        var type = f.Type("Block 30", ("Concrete", 0.3, true));
        var w1 = f.Wall(0.15, 0, 0.15, 4, type);
        var w2 = f.Wall(5.15, 0, 5.15, 4, type);
        var r = f.Run("drafting.addDimension", new JsonObject
        {
            ["viewId"] = "plan:" + f.Storey, ["a"] = new JsonArray(0.3, 2), ["b"] = new JsonArray(5, 2), ["at"] = new JsonArray(2.5, 5),
        })!;
        Assert.Equal([true, true], r["associated"]!.AsArray().Select(x => (bool)x!));
        return (f, w1, w2, (string)r["id"]!);
    }

    static (Vec2 A, Vec2 B, bool Orphan) Ends(Fixture f, string dim)
    {
        var drawing = Views.Generate(f.Session.Reader, Views.Find(f.Model, "plan:" + f.Storey)!).Drawing!;
        return Dimensions.Ends(f.Model.Get(dim)!, drawing);
    }

    static void Near(Vec2 p, double x, double y) => Assert.True((p - new Vec2(x, y)).Length < 1e-6, $"({p.X}, {p.Y}) ≉ ({x}, {y})");

    [Fact]
    public void A_dimension_measures_between_the_faces_and_reads_its_length()
    {
        var (f, _, _, dim) = Placed();
        var (a, b, orphan) = Ends(f, dim);
        Near(a, 0.3, 2); Near(b, 5, 2);
        Assert.False(orphan);
        Assert.Contains("4.70", Texts(Draw(f, "plan:" + f.Storey)));
    }

    [Fact]
    public void A_wall_moved_takes_its_end_along()
    {
        var (f, _, w2, dim) = Placed();
        f.Run("architecture.updateWall", new JsonObject { ["id"] = w2, ["x1"] = 6.15, ["y1"] = 0, ["x2"] = 6.15, ["y2"] = 4 });
        var (a, b, orphan) = Ends(f, dim);
        Near(a, 0.3, 2); Near(b, 6, 2);
        Assert.False(orphan);
    }

    [Fact]
    public void A_wall_made_thicker_keeps_the_end_on_its_face()
    {
        var (f, _, w2, dim) = Placed();
        var thick = f.Type("Block 40", ("Concrete", 0.4, true));
        f.Run("architecture.setWallType", new JsonObject { ["id"] = w2, ["typeId"] = thick });
        var (_, b, _) = Ends(f, dim);
        Near(b, 4.95, 2);
    }

    [Fact]
    public void A_wall_gone_leaves_the_end_where_it_was_and_the_dimension_an_orphan_drawn_red()
    {
        var (f, _, w2, dim) = Placed();
        f.Run("element.delete", new JsonObject { ["id"] = w2 });
        var (_, b, orphan) = Ends(f, dim);
        Near(b, 5, 2);
        Assert.True(orphan);
        Assert.Contains(Pens(Draw(f, "plan:" + f.Storey), "DIMENSION"), p => (string)p["stroke"]! == "#c0392b");
    }

    [Fact]
    public void Ends_on_no_element_are_left_as_drawn_and_a_dimension_moves_its_line()
    {
        var f = F();
        var r = f.Run("drafting.addDimension", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(1, 0), ["at"] = new JsonArray(0, 1) })!;
        Assert.Equal([false, false], r["associated"]!.AsArray().Select(x => (bool)x!));
        var id = (string)r["id"]!;
        f.Run("drafting.moveDimension", new JsonObject { ["id"] = id, ["at"] = new JsonArray(0, -2) });
        var segs = Mark(Draw(f, "plan:" + f.Storey), id)!["segments"]!.AsArray().Select(x => (double)x!).ToList();
        Assert.Contains(-2.0, segs);
        Assert.Contains("1.00", Texts(Draw(f, "plan:" + f.Storey)));
        // Only on its own view.
        var other = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        Assert.Null(Mark(Draw(f, other), id));
    }

    [Fact]
    public void A_text_is_written_in_its_style_on_its_view()
    {
        var f = F();
        f.Run("standards.set", new JsonObject { ["kind"] = "text", ["entry"] = new JsonObject { ["id"] = "title", ["name"] = "Title", ["height"] = 5, ["bold"] = true } });
        var id = (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["at"] = new JsonArray(1, 1), ["text"] = "Living room", ["textStyle"] = "title" })!["id"]!;
        var t = Pens(Draw(f, "plan:" + f.Storey), "TEXT").SelectMany(p => p["texts"]!.AsArray()).OfType<JsonObject>().Single(x => (string)x["text"]! == "Living room");
        Assert.Equal((5.0, true), ((double)t["height"]!, (bool)t["bold"]!));
        Assert.NotNull(Mark(Draw(f, "plan:" + f.Storey), id));
        Assert.Throws<CommandException>(() => f.Run("drafting.addText", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["at"] = new JsonArray(0, 0), ["text"] = " " }));
    }
}
