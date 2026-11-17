using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Issue #66 (forum #131): a dimension style, as AutoCAD's, leaves out a string's first extension line, its last or both (a
/// dimension's own choice over its style's), gives its extension lines one fixed length on paper back from the dimension line,
/// and gives the extension lines and the dimension line their own pen, colour and line type. A style that says none of it
/// draws exactly as before. At 1:100 a paper mm is 0.1 m; the Standard style's gap is 1 mm, its overshoot 1.5 mm.
/// </summary>
public class DimensionExtensionLineTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Plan(Fixture f) => "plan:" + f.Storey;

    /// <summary>A string along x through the points at x = <paramref name="xs"/> (y = 0), its line at y = <paramref name="at"/>.</summary>
    static string String(Fixture f, double at, params double[] xs) => (string)f.Run("drafting.addDimension", new JsonObject
    {
        ["viewId"] = Plan(f), ["pts"] = new JsonArray(xs.Select(x => (JsonNode)new JsonArray(x, 0)).ToArray()),
        ["at"] = new JsonArray(0, at), ["dir"] = new JsonArray(1, 0),
    })!["id"]!;

    static ViewMark Mark(Fixture f, string dim) => Views.Render(f.Session.Reader, Views.Find(f.Model, Plan(f))!, false).Marks.Single(m => m.Owner == dim);
    static string Drawing(Fixture f) => f.Session.Query("view.drawing", new JsonObject { ["id"] = Plan(f) })!.ToJsonString();
    static void Style(Fixture f, JsonObject entry) { entry["id"] ??= Standards.Standard; f.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = entry }); }

    static void Near(Vec2 want, Vec2 got) => Assert.True((want - got).Length < 1e-9, $"{want} expected, {got} drawn");

    /// <summary>The dimension's extension lines: those across its line (vertical here), not its ticks.</summary>
    static List<(Vec2 A, Vec2 B, string Layer, double Width, string Color, double[]? Dash)> Extensions(ViewMark m) =>
        m.Lines.Where(l => Math.Abs(l.A.X - l.B.X) < 1e-9 && Math.Abs(l.A.Y - l.B.Y) > 1e-9).ToList();

    /// <summary>The dimension line: the one along x.</summary>
    static (Vec2 A, Vec2 B, string Layer, double Width, string Color, double[]? Dash) DimLine(ViewMark m) =>
        m.Lines.Single(l => Math.Abs(l.A.Y - l.B.Y) < 1e-9 && Math.Abs(l.A.X - l.B.X) > 0.5);

    [Fact]
    public void By_default_every_extension_line_runs_from_the_gap_past_the_line_in_the_old_pens()
    {
        var f = F();
        var dim = String(f, 2, 0, 1, 3);
        var m = Mark(f, dim);
        var ext = Extensions(m);
        Assert.Equal(3, ext.Count);
        foreach (var (l, x) in ext.Zip(new[] { 0.0, 1, 3 }))
        {
            Near(new Vec2(x, 0.1), l.A);
            Near(new Vec2(x, 2.15), l.B);
            Assert.Equal((Pens.Default.Hatch, "#000000"), (l.Width, l.Color));
            Assert.Null(l.Dash);
        }
        var line = DimLine(m);
        Assert.Equal((Pens.Default.Hatch, "#000000"), (line.Width, line.Color));
        Assert.Null(line.Dash);
        var book = DraftingResources.Book(f.Model).DimStyles.Single();
        Assert.Null(book.ExtSuppress);
        Assert.Null(book.ExtFixed);
        // Nothing new is written for a style that says none of it.
        var json = Standards.ToJson(DraftingResources.Book(f.Model)).ToJsonString();
        foreach (var key in new[] { "extSuppress", "extFixed", "extWeight", "extColor", "extLineType", "dimLineWeight", "dimLineColor", "dimLineType" })
            Assert.DoesNotContain(key, json);
    }

    [Fact]
    public void A_style_leaves_out_the_first_the_second_or_both_extension_lines_of_a_string()
    {
        var f = F();
        var dim = String(f, 2, 0, 1, 3);
        Style(f, new JsonObject { ["extSuppress"] = "first" });
        Assert.Equal([1.0, 3], Extensions(Mark(f, dim)).Select(l => l.A.X));
        Style(f, new JsonObject { ["extSuppress"] = "second" });
        Assert.Equal([0.0, 1], Extensions(Mark(f, dim)).Select(l => l.A.X));
        Style(f, new JsonObject { ["extSuppress"] = "both" });
        Assert.Equal([1.0], Extensions(Mark(f, dim)).Select(l => l.A.X));
        // The ticks stay, and so does the dimension line.
        Assert.Equal(3, Mark(f, dim).Lines.Count(l => Math.Abs(Math.Abs(l.B.X - l.A.X) - Math.Abs(l.B.Y - l.A.Y)) < 1e-9));
        DimLine(Mark(f, dim));
        // Anything else reads as none.
        Style(f, new JsonObject { ["extSuppress"] = "sometimes" });
        Assert.Equal(3, Extensions(Mark(f, dim)).Count);
    }

    [Fact]
    public void A_dimension_says_its_own_over_its_style()
    {
        var f = F();
        var dim = String(f, 2, 0, 1, 3);
        var other = String(f, 4, 0, 3);
        Style(f, new JsonObject { ["extSuppress"] = "both" });
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(dim), ["attrs"] = new JsonObject { ["extSuppress"] = "none" } });
        Assert.Equal(3, Extensions(Mark(f, dim)).Count);
        Assert.Empty(Extensions(Mark(f, other)));
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(dim), ["attrs"] = new JsonObject { ["extSuppress"] = "second" } });
        Assert.Equal([0.0, 1], Extensions(Mark(f, dim)).Select(l => l.A.X));
        // Cleared: its style's again.
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(dim), ["attrs"] = new JsonObject { ["extSuppress"] = null } });
        Assert.Equal([1.0], Extensions(Mark(f, dim)).Select(l => l.A.X));
    }

    [Fact]
    public void A_fixed_length_extension_line_runs_that_far_back_from_the_dimension_line()
    {
        var f = F();
        // Points at y = 0 and y = -1.5, the line at y = 2: 5 mm fixed is 0.5 m back from it, past it by the 1.5 mm overshoot.
        var dim = (string)f.Run("drafting.addDimension", new JsonObject
        {
            ["viewId"] = Plan(f), ["pts"] = new JsonArray(new JsonArray(0, 0), new JsonArray(2, -1.5), new JsonArray(3, 1.95)),
            ["at"] = new JsonArray(0, 2), ["dir"] = new JsonArray(1, 0),
        })!["id"]!;
        Style(f, new JsonObject { ["extFixed"] = 5 });
        var ext = Extensions(Mark(f, dim));
        Assert.Equal(2, ext.Count);
        Near(new Vec2(0, 1.5), ext[0].A);
        Near(new Vec2(0, 2.15), ext[0].B);
        Near(new Vec2(2, 1.5), ext[1].A);
        Near(new Vec2(2, 2.15), ext[1].B);
        // A point nearer its line than the fixed length: from the gap as before (here within the gap: none).
        Style(f, new JsonObject { ["extFixed"] = 40 });
        ext = Extensions(Mark(f, dim));
        Near(new Vec2(0, 0.1), ext[0].A);
        Near(new Vec2(2, -1.4), ext[1].A);
        // 0 (or cleared): off.
        Style(f, new JsonObject { ["extFixed"] = 0 });
        Near(new Vec2(0, 0.1), Extensions(Mark(f, dim))[0].A);
        Assert.Null(DraftingResources.Book(f.Model).DimStyles.Single().ExtFixed);
    }

    [Fact]
    public void The_extension_lines_and_the_dimension_line_take_their_own_pens_colours_and_line_types()
    {
        var f = F();
        var dim = String(f, 2, 0, 1, 3);
        Style(f, new JsonObject
        {
            ["extWeight"] = 0.18, ["extColor"] = "#ff0000", ["extLineType"] = "dashed",
            ["dimLineWeight"] = 0.35, ["dimLineColor"] = "#0000ff", ["dimLineType"] = "dotted",
        });
        var m = Mark(f, dim);
        Assert.All(Extensions(m), l => Assert.Equal((0.18, "#ff0000"), (l.Width, l.Color)));
        Assert.Equal(Standards.DashOf("dashed"), Extensions(m)[0].Dash);
        var line = DimLine(m);
        Assert.Equal((0.35, "#0000ff"), (line.Width, line.Color));
        Assert.Equal(Standards.DashOf("dotted"), line.Dash);
        // The ticks take the dimension line's colour, the values the style's.
        Assert.All(m.Lines.Where(l => l.Width == Pens.Default.Seen), l => Assert.Equal("#0000ff", l.Color));
        Assert.All(m.Texts, t => Assert.Equal("#000000", t.Color));
        // Unreadable pens and line types read as unset.
        var odd = Standards.Read(new JsonObject { ["dimStyles"] = new JsonArray(new JsonObject { ["id"] = "standard", ["extWeight"] = -1, ["dimLineWeight"] = 99, ["extLineType"] = "wavy", ["extColor"] = "red?" }) }).DimStyles.Single();
        Assert.Equal((null, null, null, null), (odd.ExtWeight, odd.DimLineWeight, odd.ExtLineType, odd.ExtColor));
    }

    [Fact]
    public void An_angular_dimension_leaves_out_its_extension_lines_and_takes_the_pens()
    {
        var f = F();
        var dim = (string)f.Run("drafting.addDimension", new JsonObject
        {
            ["viewId"] = Plan(f), ["dimKind"] = "angular", ["c"] = new JsonArray(0, 0), ["a"] = new JsonArray(1, 0), ["b"] = new JsonArray(0, 1), ["at"] = new JsonArray(2, 2),
        })!["id"]!;
        int Count() => Mark(f, dim).Lines.Count;
        var all = Count();
        Style(f, new JsonObject { ["extSuppress"] = "both", ["extColor"] = "#ff0000" });
        Assert.Equal(all - 2, Count());
        Style(f, new JsonObject { ["extSuppress"] = "first" });
        Assert.Equal(all - 1, Count());
        Assert.Single(Mark(f, dim).Lines, l => l.Color == "#ff0000");
    }

    [Fact]
    public void A_view_drawn_with_a_style_that_says_none_of_it_is_drawn_exactly_as_before()
    {
        var f = F();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        f.Wall(0, 0, 8, 0, type, height: 3.0);
        String(f, -1, 0, 2.4, 3.6, 8);
        String(f, 2, 0, 0.1, 2.5);
        var before = Drawing(f);
        // Said explicitly as the defaults: the same drawing.
        Style(f, new JsonObject { ["extSuppress"] = "none", ["extFixed"] = 0, ["extWeight"] = null, ["dimLineType"] = "" });
        Assert.Equal(before, Drawing(f));
    }

    [Fact]
    public void The_standards_file_carries_the_new_fields()
    {
        var f = F();
        Style(f, new JsonObject
        {
            ["extSuppress"] = "second", ["extFixed"] = 4, ["extWeight"] = 0.18, ["extColor"] = "#ff0000", ["extLineType"] = "dashed",
            ["dimLineWeight"] = 0.35, ["dimLineColor"] = "#0000ff", ["dimLineType"] = "dotted",
        });
        var text = (string)f.Session.Query("standards.export", new JsonObject())!["text"]!;
        var g = F();
        g.Run("standards.importFrom", new JsonObject { ["text"] = text, ["options"] = true });
        var s = DraftingResources.Book(g.Model).DimStyles.Single(x => x.Id == Standards.Standard);
        Assert.Equal(("second", 4.0, 0.18, "#ff0000", "dashed"), (s.ExtSuppress, s.ExtFixed, s.ExtWeight, s.ExtColor, s.ExtLineType));
        Assert.Equal((0.35, "#0000ff", "dotted"), (s.DimLineWeight, s.DimLineColor, s.DimLineType));
        Assert.Equal(DraftingResources.Book(f.Model).DimStyles.Single(x => x.Id == Standards.Standard), s);
    }
}
