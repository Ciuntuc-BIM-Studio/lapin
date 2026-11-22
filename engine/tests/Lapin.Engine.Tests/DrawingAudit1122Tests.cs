using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Export;
using Lapin.Drawing.Import;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Documents;

namespace Lapin.Engine.Tests;

/// <summary>The drawings' and annotations' bugs of the audit of 22.11.2026 (docs/audit-2026-11-22.md §3.5, §4, §5.3), each as it was found.</summary>
public class DrawingAudit1122Tests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => new(x, y);
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static Views.Rendered Render(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);

    // ------------------------------------------------------------ grids

    [Fact]
    public void A_grid_draws_in_its_own_axis_style_and_its_overrides_else_the_project_s_default()
    {
        var f = F();
        var plan = Plan(f);
        f.Run("standards.set", new JsonObject { ["kind"] = "axis", ["entry"] = new JsonObject { ["id"] = "red", ["name"] = "Red", ["bubble"] = "square", ["color"] = "#cc0000" } });
        var grid = (string)f.Run("drafting.addAxis", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 0, ["y2"] = 10 })!["gridId"]!;
        string Color() => Render(f, plan).Marks.First(m => m.Owner?.StartsWith(grid) == true).Lines.First(l => l.Layer == "GRID").Color;
        // A grid from before (no style of its own): the project's default, as it was drawn.
        Assert.Null(f.Model.Get(grid)!.Str("axisStyle"));
        Assert.Equal("#000000", Color());
        // Its own style: every grid drew the default before, whatever it named.
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = 5, ["y1"] = 0, ["x2"] = 5, ["y2"] = 10, ["axisStyle"] = "red" });
        Assert.Equal("red", f.Model.Get(grid)!.Str("axisStyle"));
        Assert.Equal("#cc0000", Color());
        Assert.Throws<CommandException>(() => f.Run("drafting.addAxis", new JsonObject { ["x1"] = 9, ["y1"] = 0, ["x2"] = 9, ["y2"] = 10, ["axisStyle"] = "nope" }));
        // Its overrides (Standards.AxisOverrides) reach the style.
        var book = DraftingResources.Book(f.Model);
        var own = f.Model.Get(grid)!.With(attrs: new JsonObject { ["axisStyle"] = "red", ["overrides"] = new JsonObject { ["size"] = 12, ["bubble"] = "circle" } });
        Assert.Equal(12, Grids.StyleOf(book, own).Size);
        Assert.Equal("circle", Grids.StyleOf(book, own).Bubble);
        Assert.Equal("#cc0000", Grids.StyleOf(book, own).Color);
    }

    // ------------------------------------------------------------ dimensions

    [Fact]
    public void A_spot_coordinate_is_written_in_the_style_s_imperial_unit()
    {
        var f = F();
        var plan = Plan(f);
        f.Run("standards.options", new JsonObject { ["units"] = "ft-in" });
        f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["dimKind"] = "coordinate", ["a"] = J(20, 5), ["at"] = J(22, 7) });
        var texts = Render(f, plan).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        Assert.Contains("X " + Standards.FormatImperial(20, true, 2), texts);
        Assert.Contains("Y " + Standards.FormatImperial(5, true, 2), texts);
        // A metric project: metres, three decimals, as before.
        Assert.Equal(("X 20.000", "Y 5.000"), DimensionKinds.CoordinateText(f.Model, new Vec2(20, 5), "internal", "mm"));
    }

    [Fact]
    public void An_angle_and_a_slope_take_the_style_s_precision_and_minus_one_keeps_them_as_they_were()
    {
        Assert.Equal("7.1°", DimensionKinds.FormatSlope(0.125, "deg"));
        Assert.Equal("7.13°", DimensionKinds.FormatSlope(0.125, "deg", 2));
        Assert.Equal("12.500 %", DimensionKinds.FormatSlope(0.125, "%", 3));
        Assert.Equal("1:8.00", DimensionKinds.FormatSlope(0.125, "ratio", 2));
        Assert.Equal("90°", DimensionKinds.FormatAngle(90));
        Assert.Equal("90.00°", DimensionKinds.FormatAngle(90, 2));
        var f = F();
        var plan = Plan(f);
        f.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = new JsonObject { ["id"] = "two", ["name"] = "Two", ["precision"] = 2, ["unit"] = "m" } });
        f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["dimKind"] = "angular", ["dimStyle"] = "two", ["c"] = J(0, 0), ["a"] = J(2, 0), ["b"] = J(0, 2), ["at"] = J(1, 1) });
        Assert.Contains("90.00°", Render(f, plan).Pens.SelectMany(p => p.Texts).Select(t => t.Text));
    }

    [Fact]
    public void Ticks_take_the_style_s_tick_pen_and_an_angle_with_no_terminator_draws_none()
    {
        var f = F();
        var plan = Plan(f);
        var lin = (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["a"] = J(0, 0), ["b"] = J(4, 0), ["at"] = J(2, 1) })!["id"]!;
        double[] Weights(string id) => [.. Render(f, plan).Marks.Single(m => m.Owner == id).Lines.Where(l => l.Layer == "DIMENSION").Select(l => l.Width).Distinct().OrderBy(w => w)];
        // The Standard style: ticks of 0.25 mm, as always (and the 0.13 mm lines).
        Assert.Equal([Pens.Default.Hatch, DimStyle.DefaultTickWeight], Weights(lin));
        f.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = new JsonObject { ["id"] = "standard", ["tickWeight"] = 0.5 } });
        Assert.Equal([Pens.Default.Hatch, 0.5], Weights(lin));
        // "none": an angle drew arrows for it (its terminators were a second copy); now the arc and the extension lines only.
        f.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = new JsonObject { ["id"] = "bare", ["name"] = "Bare", ["arrow"] = "none" } });
        var ang = (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["dimKind"] = "angular", ["dimStyle"] = "bare", ["c"] = J(10, 0), ["a"] = J(12, 0), ["b"] = J(10, 2), ["at"] = J(11, 1) })!["id"]!;
        var lines = Render(f, plan).Marks.Single(m => m.Owner == ang).Lines.Where(l => l.Layer == "DIMENSION").ToList();
        // Only the arc's chords (a polyline) and two extension lines: every line is the dimension line's pen.
        Assert.All(lines, l => Assert.Equal(Pens.Default.Hatch, l.Width));
        // The dimension styles' overrides take issue #66's pens (and the ticks').
        foreach (var k in new[] { "extWeight", "extColor", "extLineType", "dimLineWeight", "dimLineColor", "dimLineType", "tickWeight" }) Assert.Contains(k, Standards.DimOverrides);
        var book = DraftingResources.Book(f.Model);
        Assert.Equal(0.7, Standards.ResolveDim(book, new JsonObject { ["o.tickWeight"] = 0.7 }).TickWeightOr());
        Assert.Equal("#00ff00", Standards.ResolveDim(book, new JsonObject { ["o.extColor"] = "#00ff00" }).ExtColor);
    }

    // ------------------------------------------------------------ the chord rule

    [Fact]
    public void A_dxf_arc_is_cut_by_the_chord_rule_and_a_clockwise_hatch_edge_reads_its_complements()
    {
        // A 50 m arc: 1 mm off its chords at most (it was 48 mm at 5° a piece).
        var arc = DxfCurves.Arc(default, 50, 0, 90);
        for (var i = 0; i + 1 < arc.Count; i++) Assert.True(50 - ((arc[i] + arc[i + 1]) * 0.5).Length <= Chords.Tolerance + 1e-9);
        // In millimetres the same arc is 50 000 units: as fine, by its metres.
        Assert.Equal(arc.Count, DxfCurves.Arc(default, 50_000, 0, 90, 0.001).Count);
        // A clockwise arc edge (73 = 0) stores 360 − its angles (as ezdxf reads them): 0 → 90 stored is 0° clockwise to 270°, below the x axis.
        var dxf = string.Join("\n", new[]
        {
            "0", "SECTION", "2", "HEADER", "9", "$INSUNITS", "70", "6", "0", "ENDSEC",
            "0", "SECTION", "2", "ENTITIES",
            "0", "HATCH", "8", "0", "2", "SOLID", "70", "1", "91", "1",
            "92", "1", "93", "1", "72", "2", "10", "0", "20", "0", "40", "10", "50", "0", "51", "90", "73", "0", "97", "0",
            "75", "0", "76", "1", "98", "0",
            "0", "ENDSEC", "0", "EOF",
        });
        var hatch = DxfReader.Read(dxf).Entities.OfType<DxfHatch>().Single();
        Assert.All(hatch.Loops[0], p => Assert.True(p.Y <= 1e-9, $"{p} is above the x axis"));
        Assert.Contains(hatch.Loops[0], p => (p - new Vec2(0, -10)).Length < 1e-6);
    }

    // ------------------------------------------------------------ DXF writing

    [Fact]
    public void Dxf_attribute_tags_have_no_spaces_and_a_fill_that_will_not_triangulate_keeps_its_outline()
    {
        var block = new Pen("TEXT", "#000000", 0, null, null) { Block = "TB" };
        block.Texts.Add(new PenText(new Vec2(10, 10), "A", 2.5) { Tag = "my tag" });
        block.Texts.Add(new PenText(new Vec2(10, 20), "B", 2.5) { Tag = "my tag" });
        var text = Encoding.Latin1.GetString(DxfWriter.Page(new PaperPage(100, 100, [block]), "test"));
        Assert.Contains("\nMY_TAG\n", text);
        Assert.Contains("\nMY_TAG_2\n", text);
        Assert.DoesNotContain("MY TAG", text);
        // A loop the triangulator refuses (not a number) was dropped without a word: now its outline, and a warning.
        var failed = new List<Vec2[]>();
        IReadOnlyList<Vec2> bad = [new(0, 0), new(double.NaN, 0), new(10, 10), new(0, 10)];
        var tris = DxfWriter.Triangles([bad], failed).ToList();
        if (failed.Count == 0) return;   // the triangulator took it: nothing to fall back from
        Assert.Empty(tris);
        var fill = new Pen("FILL", null, 0, "#ff0000", null);
        fill.Loops.Add(bad);
        var warnings = new List<string>();
        var dxf = Encoding.Latin1.GetString(DxfWriter.Page(new PaperPage(100, 100, [fill]), "test", warnings: warnings));
        Assert.Contains("LWPOLYLINE", dxf);
        Assert.Single(warnings);
    }

    // ------------------------------------------------------------ documents: numbers, levels, dates

    [Fact]
    public void A_document_writes_levels_as_the_drawings_do_and_a_thin_imperial_thickness_as_zero()
    {
        var en = new DocFormat("en", "metric");
        var ro = new DocFormat("ro", "metric");
        // As Standards.FormatLevel: a true minus, the project's decimals; only the decimal separator is the language's.
        Assert.Equal(Standards.FormatLevel(-1.5), DocNumbers.Level(-1.5, en));
        Assert.Equal("−1,50", DocNumbers.Level(-1.5, ro));
        Assert.Equal("±0,00", DocNumbers.Level(0, ro));
        Assert.Equal("+3,00", DocNumbers.Level(3, ro));
        var book = Standards.Read(new JsonObject { ["levelDecimals"] = 3 });
        Assert.Equal("+3,150", DocNumbers.Level(3.15, ro with { Book = book }));
        var imperial = new DocFormat("en", "ft-in");
        Assert.Equal(Standards.FormatLevel(3, "ft-in"), DocNumbers.Level(3, imperial));
        // 1 mm is 0.04 in: "0 in", not " in".
        Assert.Equal("0 in", DocNumbers.Thickness(0.001, imperial));
        Assert.Equal("2 in", DocNumbers.Thickness(0.0508, imperial));
    }

    [Fact]
    public void Dates_follow_the_project_s_format_in_title_blocks_and_documents_and_romanian_stays_as_it_was()
    {
        var d = new DateTime(2026, 11, 5);
        Assert.Equal("05.11.2026", DrawingDates.Format(null, d));
        Assert.Equal("05.11.2026", DrawingDates.Format(StyleBook.Default, d));
        Assert.Equal("05.11.2026", DocNumbers.Date(d, DocFormat.Ro));
        var f = F();
        f.Run("standards.options", new JsonObject { ["dateFormat"] = "yyyy-MM-dd" });
        var book = DraftingResources.Book(f.Model);
        Assert.Equal("yyyy-MM-dd", book.DateFormat);
        Assert.Equal("2026-11-05", DrawingDates.Format(book, d));
        // A Romanian document follows the project's format too, as its title blocks write it.
        Assert.Equal("2026-11-05", DocNumbers.Date(d, new DocFormat("ro", book.Units) { DateFormat = book.DateFormat }));
        // A sheet's title block and its print date: the project's format.
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        var resolve = TitleBlocks.Resolver(f.Session.Reader, f.Model.Get(sheet)!);
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), resolve("system.printDate").Value);
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), resolve("sheet.date").Value);
        Assert.Throws<CommandException>(() => f.Run("standards.options", new JsonObject { ["dateFormat"] = "yy" }));
        f.Run("standards.options", new JsonObject { ["dateFormat"] = null });
        Assert.Null(DraftingResources.Book(f.Model).DateFormat);
    }

    // ------------------------------------------------------------ texts with no height of their own

    [Fact]
    public void A_symbol_s_text_with_no_style_or_height_takes_the_standard_style_s_height()
    {
        var f = F();
        var def = SymbolDef.Parse(new JsonObject
        {
            ["scale"] = "annotation", ["category"] = "annotation",
            ["items"] = new JsonArray(new JsonObject { ["type"] = "text", ["at"] = J(0, 0), ["text"] = "A\nB" }),
        });
        SymbolDrawing Draw() => SymbolRender.Render(f.Model, def, _ => (false, null), 0.001);
        // 2.5 mm, the Standard style's default: as before; rows one pitch (1.4 heights) apart.
        Assert.All(Draw().Texts, t => Assert.Equal(2.5, t.Height));
        Assert.Equal(2.5 * TextMeasure.LinePitch, (Draw().Texts[0].At - Draw().Texts[1].At).Length, 9);
        f.Run("standards.set", new JsonObject { ["kind"] = "text", ["entry"] = new JsonObject { ["id"] = "standard", ["height"] = 3.5 } });
        Assert.All(Draw().Texts, t => Assert.Equal(3.5, t.Height));
    }

    [Fact]
    public void A_title_block_s_fit_measures_its_widest_row_in_the_row_s_font()
    {
        var f = F();
        var template = TitleBlockTemplate.Parse(new JsonObject
        {
            ["w"] = 100, ["h"] = 40, ["anchor"] = "bottomRight",
            ["items"] = new JsonArray(new JsonObject { ["type"] = "text", ["at"] = J(2, 10), ["text"] = "Short\nA much longer second row", ["h"] = 5, ["fit"] = 40, ["bold"] = true }),
        });
        var texts = TitleBlocks.Preview(f.Session.Reader, template, null).Pens.SelectMany(p => p.Texts).ToList();
        // The whole value ("Short\nA much…", measured as one line) shrank it too much; its widest row, bold, fits 40 mm.
        var h = 5 * 40 / TextMeasure.Width("A much longer second row", 5, bold: true);
        Assert.All(texts, t => Assert.Equal(h, t.Height, 9));
        Assert.Equal(40, TextMeasure.Width("A much longer second row", texts[0].Height, bold: true), 6);
    }
}
