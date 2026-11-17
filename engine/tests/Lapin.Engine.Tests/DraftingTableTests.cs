using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Drafting tables (issue #71, docs/plan-drafting-tables.md, stage 1): placed on a view or a sheet's paper, cells with
/// multiline text and formulas reading the table's cells (<c>{= A1 * 2}</c>, <c>sum(A1:A4)</c>), merges, widths, heights and
/// fitting to text, borders and colours, drawn by the engine for the view, the sheet and its PDF, one undo step per edit, saved.
/// </summary>
public class DraftingTableTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => new(x, y);
    static string Plan(Fixture f) => "plan:" + f.Storey;

    static string Add(Fixture f, string viewId, int rows = 3, int cols = 2, string preset = "grid", JsonArray? cells = null)
    {
        var a = new JsonObject { ["viewId"] = viewId, ["at"] = J(1, 5), ["rows"] = rows, ["cols"] = cols, ["preset"] = preset };
        if (cells != null) a["cells"] = cells;
        return (string)f.Run("drafting.addTable", a)!["id"]!;
    }

    static void Edit(Fixture f, string id, params JsonObject[] ops) =>
        f.Run("drafting.editTable", new JsonObject { ["id"] = id, ["ops"] = new JsonArray([.. ops]) });

    static JsonObject Op(string op, params (string K, JsonNode? V)[] kv)
    {
        var o = new JsonObject { ["op"] = op };
        foreach (var (k, v) in kv) o[k] = v;
        return o;
    }

    static JsonObject Info(Fixture f, string id) => (JsonObject)f.Session.Query("drafting.table", new JsonObject { ["id"] = id })!;
    static string Shown(Fixture f, string id, int r, int c) => (string)Info(f, id)["cells"]![r]![c]!["shown"]!;
    static ViewMark Mark(Fixture f, string view, string id) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!).Marks.Single(m => m.Owner == id);

    static JsonArray Rows(params string[][] rows) => new([.. rows.Select(r => (JsonNode)new JsonArray([.. r.Select(x => (JsonNode)x)]))]);

    // ------------------------------------------------------------ addresses and formulas

    [Fact]
    public void Addresses_read_as_a_spreadsheet_s()
    {
        Assert.Equal("A", DraftingTables.ColName(0));
        Assert.Equal("Z", DraftingTables.ColName(25));
        Assert.Equal("AA", DraftingTables.ColName(26));
        Assert.Equal("AZ", DraftingTables.ColName(51));
        Assert.Equal((2, 1), DraftingTables.Parse("B3"));
        Assert.Equal((0, 26), DraftingTables.Parse("aa1"));
        Assert.Null(DraftingTables.Parse("A0"));
        Assert.Null(DraftingTables.Parse("attr.width"));
        Assert.Equal((0, 0, 3, 2), DraftingTables.ParseRange("C4:A1"));
    }

    [Fact]
    public void Ranges_are_read_inside_a_function_s_values_and_nowhere_else()
    {
        IReadOnlyList<XValue>? Range(string a, string b) => a == "A1" && b == "A3" ? [XValue.Of(1), XValue.Of(2), XValue.Read("x"), XValue.Of(4)] : null;
        Assert.Equal(7, Expr.EvalValue("sum(A1:A3)", _ => null, Range).Number);
        Assert.Equal(17, Expr.EvalValue("sum(A1:A3; 10)", _ => null, Range).Number);
        Assert.Equal(3, Expr.EvalValue("count(A1:A3)", _ => null, Range).Number);
        Assert.Equal(7.0 / 3, Expr.EvalValue("average(A1 : A3)", _ => null, Range).Number!.Value, 9);
        Assert.Throws<ExprException>(() => Expr.EvalValue("sum(A1:B9)", _ => null, Range));
        // No range reader: a range is not a value (every formula elsewhere reads as it did).
        Assert.Throws<ExprException>(() => Expr.EvalValue("sum(A1:A3)", _ => null));
        Assert.Equal(6, Expr.EvalValue("sum(1; 2; 3)", _ => null).Number);
    }

    [Fact]
    public void Cells_hold_multiline_text_and_formulas_reading_other_cells()
    {
        var f = F();
        var id = Add(f, Plan(f), 4, 3, cells: Rows(
            ["Room", "Area", "Note"],
            ["Living", "24.5", "south\nfacing"],
            ["Kitchen", "{= 10.25 + 2}", ""],
            ["Total", "{= sum(B2:B3) | 0.00} m²", "{= B4 * 1}"]));
        Assert.Equal("12.25", Shown(f, id, 2, 1));
        Assert.Equal("36.75 m²", Shown(f, id, 3, 1));
        // A cell of text and a formula reads as the number it starts with in arithmetic.
        Assert.Equal("36.75", Shown(f, id, 3, 2));
        Assert.Equal("south\nfacing", Shown(f, id, 1, 2));
        Assert.Empty((JsonArray)Info(f, id)["errors"]!);
        // Changed, the cells reading it follow.
        Edit(f, id, Op("set", ("cell", "B2"), ("text", "30")));
        Assert.Equal("42.25 m²", Shown(f, id, 3, 1));
    }

    [Fact]
    public void A_formula_reading_itself_does_not_read_and_says_so()
    {
        var f = F();
        var id = Add(f, Plan(f), 2, 2, cells: Rows(["{= B1 + 1}", "{= A1 * 2}"], ["{= A2}", "{= nothing + 1}"]));
        Assert.Equal("<B1 + 1>", Shown(f, id, 0, 0));
        Assert.Equal("<A2>", Shown(f, id, 1, 0));
        Assert.Equal("<nothing + 1>", Shown(f, id, 1, 1));
        var errors = ((JsonArray)Info(f, id)["errors"]!).Select(x => (string)x!).ToList();
        Assert.Contains(errors, e => e.Contains("reads itself"));
        Assert.Contains(errors, e => e.StartsWith("B2:") && e.Contains("nothing"));
    }

    [Fact]
    public void An_empty_cell_is_0_alone_and_left_out_of_a_range()
    {
        var f = F();
        var id = Add(f, Plan(f), 3, 2, cells: Rows(["5", ""], ["", "{= A2 + 1}"], ["{= count(A1:A2)}", "{= average(A1:A2)}"]));
        Assert.Equal("1", Shown(f, id, 1, 1));
        Assert.Equal("1", Shown(f, id, 2, 0));
        Assert.Equal("5", Shown(f, id, 2, 1));
    }

    // ------------------------------------------------------------ placing, drawing, sheets

    [Fact]
    public void A_table_on_a_plan_draws_its_lines_fills_and_texts_at_the_view_s_scale()
    {
        var f = F();
        var id = Add(f, Plan(f), 2, 2, preset: "header", cells: Rows(["A", "B"], ["1", "2"]));
        var mk = Mark(f, Plan(f), id);
        // 1:100: a 25 mm column is 2.5 m, a 7 mm row 0.7 m; from (1, 5) to the right and down.
        var lines = mk.Lines.Where(l => l.Layer == "TABLE").ToList();
        Assert.Contains(lines, l => (l.A - new Vec2(1, 5)).Length < 1e-9 && (l.B - new Vec2(6, 5)).Length < 1e-9 && l.Width == 0.35);
        Assert.Contains(lines, l => Math.Abs(l.A.Y - 4.3) < 1e-9 && Math.Abs(l.B.Y - 4.3) < 1e-9 && l.Width == 0.18);
        // Three rules across (the grid's inner one joined in one line) and three down.
        Assert.Equal(6, lines.Count);
        // The header row shaded, its texts bold; texts in the cell's padding at the left, in the middle of the row.
        Assert.Equal(2, mk.Fills.Count);
        Assert.All(mk.Fills, x => Assert.Equal(DraftingTables.HeaderFill, x.Color));
        var a = mk.Texts.Single(t => t.Text.Text == "A").Text;
        Assert.True(a.Bold);
        Assert.Equal(1 + 0.1, a.At.X, 9);
        // Its baseline half its height under the row's middle: 3.5 + 1.25 mm down.
        Assert.Equal(5 - 0.475, a.At.Y, 9);
        Assert.False(mk.Texts.Single(t => t.Text.Text == "2").Text.Bold);
        Assert.NotNull(mk.Hit);
    }

    [Fact]
    public void On_a_sheet_s_paper_it_prints_in_paper_mm_reads_the_sheet_s_tokens_and_goes_to_the_PDF()
    {
        var f = F();
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        var paper = PaperViews.IdOf(s);
        var id = (string)f.Run("drafting.addTable", new JsonObject
        {
            ["viewId"] = paper, ["at"] = J(0.05, 0.1), ["rows"] = 1, ["cols"] = 2, ["preset"] = "grid",
            ["cells"] = Rows(["Sheet", "{sheet.number}, {= len(\"abc\") * 2}"]),
        })!["id"]!;
        Assert.Equal("A-101, 6", Shown(f, id, 0, 1));
        var pens = Sheets.Compose(f.Session.Reader, f.Model.Get(s)!).Pens;
        // The table's top edge: 50 mm in, 297 − 100 = 197 mm down the paper, 50 mm long.
        Assert.Contains(pens.Where(p => p.Layer == "TABLE").SelectMany(p => p.Segments), sg => (sg.A - new Vec2(50, 197)).Length < 1e-6 && (sg.B - new Vec2(100, 197)).Length < 1e-6);
        Assert.Contains(pens.SelectMany(p => p.Texts), t => t.Text == "A-101, 6");
        var pdf = Encoding.Latin1.GetString(Sheets.Pdf(f.Session.Reader, [f.Model.Get(s)!]));
        Assert.StartsWith("%PDF", pdf);
        // Duplicated with its sheet.
        var copy = (string)f.Run("sheet.duplicate", new JsonObject { ["id"] = s })!["id"]!;
        Assert.Contains(PaperViews.Owned(f.Model, copy), e => e.PredefinedType == DraftingTables.Predefined);
    }

    [Fact]
    public void A_table_moves_and_turns_with_the_Modify_tools()
    {
        var f = F();
        var id = Add(f, Plan(f));
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "move", ["by"] = J(2, 1) });
        Assert.Equal(new Vec2(3, 6), Args.PointOf(f.Model.Get(id)!.Attrs["at"]));
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "rotate", ["center"] = J(3, 6), ["angle"] = 90 });
        Assert.Equal(90, f.Model.Get(id)!.Num("angle"), 6);
        // Turned a quarter, its columns run up the view.
        var top = Mark(f, Plan(f), id).Lines.Where(l => l.Layer == "TABLE").ToList();
        Assert.Contains(top, l => (l.A - new Vec2(3, 6)).Length < 1e-9 && (l.B - new Vec2(3, 11)).Length < 1e-9);
    }

    // ------------------------------------------------------------ sizes

    [Fact]
    public void Columns_fit_their_text_rows_grow_with_wrapped_lines_and_fixed_sizes_hold()
    {
        var f = F();
        var id = Add(f, Plan(f), 2, 2, cells: Rows(["A long heading here", "x"], ["one two three four five six", "y"]));
        var info = Info(f, id);
        Assert.Equal(25, (double)info["colW"]![0]!);
        // 2.5 mm text in a 25 mm column (23 mm inside its padding, 15 characters of 1.5 mm): the long cells wrap to two lines;
        // their rows fit: 2.5 + 3.375 + 2 × 1 = 7.875 mm, over the 7 mm they had.
        Assert.Equal(7.875, (double)info["rowH"]![0]!, 6);
        Assert.Equal(7.875, (double)info["rowH"]![1]!, 6);
        // Column A fit to its text: its widest line (27 characters) and its padding; column B at a width of its own.
        Edit(f, id, Op("fit", ("range", "A1:A2"), ("axis", "cols")), Op("colWidth", ("range", "B1"), ("w", 12)));
        info = Info(f, id);
        Assert.Equal(27 * 1.5 + 2, (double)info["colW"]![0]!, 6);
        Assert.Equal(12, (double)info["colW"]![1]!);
        Assert.Equal(7, (double)info["rowH"]![1]!, 6);
        // A max holds it: then it wraps again.
        Edit(f, id, Op("fit", ("range", "A1"), ("axis", "cols"), ("max", 20)));
        Assert.Equal(20, (double)Info(f, id)["colW"]![0]!, 6);
        // A row of a height of its own stays at it.
        Edit(f, id, Op("rowHeight", ("range", "A2"), ("h", 5)));
        Assert.Equal(5, (double)Info(f, id)["rowH"]![1]!, 6);
        Assert.Throws<CommandException>(() => Edit(f, id, Op("colWidth", ("range", "A1"), ("w", 0))));
    }

    // ------------------------------------------------------------ merges

    [Fact]
    public void Merged_cells_draw_as_one_and_split_back()
    {
        var f = F();
        var id = Add(f, Plan(f), 2, 3, cells: Rows(["Title", "gone", ""], ["a", "b", "c"]));
        var before = Mark(f, Plan(f), id).Lines.Count(l => l.Layer == "TABLE");
        Edit(f, id, Op("merge", ("range", "A1:C1")));
        var info = Info(f, id);
        Assert.Equal(3, (int)info["cells"]![0]![0]!["cs"]!);
        Assert.Equal("A1", (string)info["cells"]![0]![1]!["covered"]!);
        // The top-left cell's text stays; the others it covers are emptied, and no line is drawn inside it.
        Assert.Equal("", (string)info["cells"]![0]![1]!["t"]!);
        var mk = Mark(f, Plan(f), id);
        Assert.DoesNotContain(mk.Texts, t => t.Text.Text == "gone");
        var verticals = mk.Lines.Where(l => l.Layer == "TABLE" && Math.Abs(l.A.X - l.B.X) < 1e-9).ToList();
        Assert.All(verticals.Where(l => l.A.X > 1 + 1e-9 && l.A.X < 8.5 - 1e-9), l => Assert.True(Math.Max(l.A.Y, l.B.Y) <= 4.3 + 1e-9));
        // Writing in a covered cell is refused, naming where to write; a range cutting through it too.
        Assert.Contains("write in A1", Assert.Throws<CommandException>(() => Edit(f, id, Op("set", ("cell", "B1"), ("text", "x")))).Message);
        Assert.Throws<CommandException>(() => Edit(f, id, Op("merge", ("range", "B1:B2"))));
        Edit(f, id, Op("split", ("cell", "A1")));
        Assert.Null(Info(f, id)["cells"]![0]![0]!["cs"]);
        Assert.Equal(before, Mark(f, Plan(f), id).Lines.Count(l => l.Layer == "TABLE"));
    }

    // ------------------------------------------------------------ rows and columns in and out

    [Fact]
    public void Rows_and_columns_put_in_or_taken_out_carry_the_formulas_with_them()
    {
        var f = F();
        var id = Add(f, Plan(f), 4, 2, cells: Rows(["1", "{= A1 * 10}"], ["2", ""], ["3", ""], ["{= sum(A1:A3)}", "{= A3 + B1}"]));
        Edit(f, id, Op("insertRows", ("at", 1), ("count", 2)));
        var info = Info(f, id);
        Assert.Equal(6, (int)info["rows"]!);
        Assert.Equal("{= sum(A1:A5)}", (string)info["cells"]![5]![0]!["t"]!);
        Assert.Equal("{= A5 + B1}", (string)info["cells"]![5]![1]!["t"]!);
        Assert.Equal("6", Shown(f, id, 5, 0));
        // Taken out (the rows of 2 and 3): a range narrows, a reference to a cell taken out says it is gone.
        Edit(f, id, Op("deleteRows", ("at", 3), ("count", 2)));
        info = Info(f, id);
        Assert.Equal("{= sum(A1:A3)}", (string)info["cells"]![3]![0]!["t"]!);
        Assert.Equal("{= REF + B1}", (string)info["cells"]![3]![1]!["t"]!);
        Assert.Equal("1", Shown(f, id, 3, 0));
        Assert.Equal("<REF + B1>", Shown(f, id, 3, 1));
        // A column in front: every reference moves right, texts in quotes and names with a dot stay.
        Edit(f, id, Op("set", ("cell", "B2"), ("text", "{= upper(\"A1\") & B1 & sheet.number}")), Op("insertCols", ("at", 0)));
        info = Info(f, id);
        Assert.Equal(3, (int)info["cols"]!);
        Assert.Equal("{= upper(\"A1\") & C1 & sheet.number}", (string)info["cells"]![1]![2]!["t"]!);
        Assert.Equal("{= B1 * 10}", (string)info["cells"]![0]![2]!["t"]!);
        Assert.Throws<CommandException>(() => Edit(f, id, Op("deleteCols", ("at", 0), ("count", 3))));
    }

    [Fact]
    public void A_merged_cell_grows_with_rows_put_in_across_it_and_shrinks_when_they_go()
    {
        var f = F();
        var id = Add(f, Plan(f), 4, 2, cells: Rows(["Side", "a"], ["", "b"], ["", "c"], ["end", "d"]));
        Edit(f, id, Op("merge", ("range", "A1:A3")));
        Edit(f, id, Op("insertRows", ("at", 2)));
        Assert.Equal(4, (int)Info(f, id)["cells"]![0]![0]!["rs"]!);
        // Its first row taken out: what is left of it starts on the next row, with its text.
        Edit(f, id, Op("deleteRows", ("at", 0)));
        var info = Info(f, id);
        Assert.Equal("Side", (string)info["cells"]![0]![0]!["t"]!);
        Assert.Equal(3, (int)info["cells"]![0]![0]!["rs"]!);
        Assert.Equal("end", Shown(f, id, 3, 0));
    }

    // ------------------------------------------------------------ styles and borders

    [Fact]
    public void Borders_text_and_background_colours_with_opacity()
    {
        var f = F();
        var id = Add(f, Plan(f), 3, 3, preset: "none");
        Assert.DoesNotContain(Mark(f, Plan(f), id).Lines, l => l.Layer == "TABLE");
        Edit(f, id,
            Op("borders", ("range", "A1:C3"), ("edges", "outer"), ("w", 0.5), ("c", "#ff0000")),
            Op("style", ("range", "B2"), ("style", new JsonObject { ["fill"] = "#00ff0080", ["color"] = "#0000ff", ["italic"] = true, ["align"] = "right", ["size"] = 3.5 })),
            Op("set", ("cell", "B2"), ("text", "x")));
        var mk = Mark(f, Plan(f), id);
        var lines = mk.Lines.Where(l => l.Layer == "TABLE").ToList();
        Assert.Equal(4, lines.Count);
        Assert.All(lines, l => { Assert.Equal(0.5, l.Width); Assert.Equal("#ff0000", l.Color); });
        Assert.Equal("#00ff0080", mk.Fills.Single().Color);
        var x = mk.Texts.Single(t => t.Text.Text == "x");
        Assert.Equal("#0000ff", x.Color);
        Assert.Equal("end", x.Text.Anchor);
        Assert.True(x.Text.Italic);
        Assert.Equal(3.5, x.Text.Height);
        // Inner lines, then none at all across B2's top: its side decides that line alone.
        Edit(f, id, Op("borders", ("range", "A1:C3"), ("edges", "inner"), ("w", 0.18)), Op("borders", ("range", "B2"), ("edges", "top"), ("w", 0)));
        lines = [.. Mark(f, Plan(f), id).Lines.Where(l => l.Layer == "TABLE")];
        Assert.DoesNotContain(lines, l => Math.Abs(l.A.Y - 4.3) < 1e-9 && Math.Min(l.A.X, l.B.X) < 3.5 + 1e-9 && Math.Max(l.A.X, l.B.X) > 6 - 1e-9);
        Assert.Throws<CommandException>(() => Edit(f, id, Op("style", ("range", "A1"), ("style", new JsonObject { ["fill"] = "red" }))));
        Assert.Throws<CommandException>(() => Edit(f, id, Op("style", ("range", "A1"), ("style", new JsonObject { ["font"] = "x" }))));
    }

    [Fact]
    public void A_preset_set_again_restyles_the_header_and_the_lines()
    {
        var f = F();
        var id = Add(f, Plan(f), 2, 2, preset: "grid");
        Assert.Empty(Mark(f, Plan(f), id).Fills);
        Edit(f, id, Op("preset", ("preset", "header")));
        Assert.Equal(2, Mark(f, Plan(f), id).Fills.Count);
        Edit(f, id, Op("preset", ("preset", "minimal")));
        var mk = Mark(f, Plan(f), id);
        Assert.Empty(mk.Fills);
        // Lines under the header only.
        Assert.Single(mk.Lines, l => l.Layer == "TABLE");
        Assert.Throws<CommandException>(() => Edit(f, id, Op("preset", ("preset", "fancy"))));
    }

    // ------------------------------------------------------------ undo, saving, refusals

    [Fact]
    public void Each_edit_is_one_undo_step_and_a_refused_one_changes_nothing()
    {
        var f = F();
        var id = Add(f, Plan(f), 2, 2);
        Edit(f, id, Op("set", ("cell", "A1"), ("text", "one")), Op("set", ("cell", "B1"), ("text", "two")), Op("merge", ("range", "A2:B2")));
        Edit(f, id, Op("set", ("cell", "A1"), ("text", "three")));
        Assert.Equal("three", Shown(f, id, 0, 0));
        f.Session.Undo();
        Assert.Equal("one", Shown(f, id, 0, 0));
        f.Session.Undo();
        Assert.Equal("", Shown(f, id, 0, 0));
        Assert.Null(Info(f, id)["cells"]![1]![0]!["cs"]);
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
        f.Session.Redo();
        var again = f.Model.Get(id)!;
        // A refused op among others: none of them is done.
        Assert.Throws<CommandException>(() => Edit(f, id, Op("set", ("cell", "A1"), ("text", "kept?")), Op("set", ("cell", "Z9"), ("text", "x"))));
        Assert.Same(again, f.Model.Get(id));
        Assert.Throws<CommandException>(() => f.Run("drafting.addTable", new JsonObject { ["viewId"] = Plan(f), ["at"] = J(0, 0), ["rows"] = 0, ["cols"] = 2 }));
        Assert.Throws<CommandException>(() => f.Run("drafting.addTable", new JsonObject { ["viewId"] = "nowhere", ["at"] = J(0, 0) }));
        Assert.Throws<CommandException>(() => Edit(f, id, Op("explode")));
    }

    [Fact]
    public void A_table_is_saved_with_the_project_and_opens_the_same()
    {
        var f = F();
        var id = Add(f, Plan(f), 2, 2, cells: Rows(["a", "{= 1 + 1}"], ["b", "c"]));
        Edit(f, id, Op("merge", ("range", "A2:B2")), Op("style", ("range", "A1"), ("style", new JsonObject { ["fill"] = "#ffcc00" })));
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        var g = F();
        var content = LapinPackage.Read(new MemoryStream(ms.ToArray()));
        g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Assert.Equal(Info(f, id).ToJsonString(), Info(g, id).ToJsonString());
        Assert.Equal("2", Shown(g, id, 0, 1));
        Assert.Equal("#ffcc00", Mark(g, Plan(g), id).Fills.Single().Color);
    }

    // ------------------------------------------------------------ stage 2: other tables' cells

    static string Named(Fixture f, string viewId, string name, JsonArray cells, int rows, int cols)
    {
        var a = new JsonObject { ["viewId"] = viewId, ["at"] = J(1, 5), ["rows"] = rows, ["cols"] = cols, ["preset"] = "grid", ["name"] = name, ["cells"] = cells };
        return (string)f.Run("drafting.addTable", a)!["id"]!;
    }

    static List<string> Errors(Fixture f, string id) => ((JsonArray)Info(f, id)["errors"]!).Select(x => (string)x!).ToList();

    [Fact]
    public void A_cell_reads_another_table_s_cells_and_ranges_by_its_name()
    {
        var f = F();
        var areas = Named(f, Plan(f), "Areas", Rows(["Room", "Area"], ["Living", "24.5"], ["Kitchen", "12.25"], ["Total", "{= sum(B2:B3)}"]), 4, 2);
        var sum = Named(f, Plan(f), "Summary", Rows(
            ["{= table.Areas.B4 * 2}", "{= sum(table.Areas.B2:B3) | 0.00}"],
            ["{table.Areas.A2} has {table.Areas.B2} m²", "{= average(table.areas.B2:table.Areas.B3) | 0.000}"]), 2, 2);
        Assert.Equal("73.5", Shown(f, sum, 0, 0));
        Assert.Equal("36.75", Shown(f, sum, 0, 1));
        Assert.Equal("Living has 24.5 m²", Shown(f, sum, 1, 0));
        Assert.Equal("18.375", Shown(f, sum, 1, 1));
        Assert.Empty(Errors(f, sum));
        // Changed there, read here.
        Edit(f, areas, Op("set", ("cell", "B2"), ("text", "30")));
        Assert.Equal("84.5", Shown(f, sum, 0, 0));
        // The engine draws what it reads.
        Assert.Contains(Mark(f, Plan(f), sum).Texts, t => t.Text.Text == "84.5");
        // A name no table has, or a cell that is not one, does not read and says why.
        Edit(f, sum, Op("set", ("cell", "A1"), ("text", "{= table.Nothing.A1 + 1}")), Op("set", ("cell", "B1"), ("text", "{table.Areas.ZZZ0}")));
        Assert.Equal("<table.Nothing.A1 + 1>", Shown(f, sum, 0, 0));
        Assert.Contains(Errors(f, sum), e => e.Contains("no table „Nothing”"));
        Assert.Equal("<table.Areas.ZZZ0>", Shown(f, sum, 0, 1));
    }

    [Fact]
    public void A_name_finds_the_table_on_the_same_sheet_first_so_a_duplicated_sheet_reads_its_own()
    {
        var f = F();
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "One", ["paper"] = "A3" })!["id"]!;
        var paper = PaperViews.IdOf(s);
        Named(f, paper, "Data", Rows(["5"]), 1, 1);
        var reader = Named(f, paper, "Reader", Rows(["{= table.Data.A1 + 1}"]), 1, 1);
        Assert.Equal("6", Shown(f, reader, 0, 0));
        var copy = (string)f.Run("sheet.duplicate", new JsonObject { ["id"] = s })!["id"]!;
        var tables = PaperViews.Owned(f.Model, copy).Where(e => e.PredefinedType == DraftingTables.Predefined).ToList();
        var data2 = tables.Single(e => e.Name == "Data").Id;
        var reader2 = tables.Single(e => e.Name == "Reader").Id;
        Edit(f, data2, Op("set", ("cell", "A1"), ("text", "40")));
        Assert.Equal("41", Shown(f, reader2, 0, 0));
        Assert.Equal("6", Shown(f, reader, 0, 0));
        // From a third place, the name is two tables: it does not read, and says to rename one.
        var other = Named(f, Plan(f), "Other", Rows(["{= table.Data.A1}"]), 1, 1);
        Assert.Equal("<table.Data.A1>", Shown(f, other, 0, 0));
        Assert.Contains(Errors(f, other), e => e.Contains("2 tables are called „Data”"));
    }

    [Fact]
    public void Formulas_going_round_through_two_tables_do_not_read_and_say_so()
    {
        var f = F();
        var a = Named(f, Plan(f), "First", Rows(["{= table.Second.A1 + 1}", "{= table.First.A1}"]), 1, 2);
        var b = Named(f, Plan(f), "Second", Rows(["{= table.First.A1 * 2}"]), 1, 1);
        Assert.StartsWith("<", Shown(f, a, 0, 0));
        Assert.StartsWith("<", Shown(f, b, 0, 0));
        Assert.Contains(Errors(f, a), e => e.Contains("reads back") || e.Contains("reads itself"));
        // The drawing of the view stops too, and draws what it can.
        Assert.NotNull(Mark(f, Plan(f), a));
    }

    [Fact]
    public void Renaming_a_table_rewrites_what_reads_it_and_a_name_is_one_word_no_other_table_has()
    {
        var f = F();
        var areas = Named(f, Plan(f), "Areas", Rows(["1", "2"]), 1, 2);
        var reader = Named(f, Plan(f), "Reader", Rows(["{= table.Areas.A1 + table.Areas.B1} \"x\"", "{table.Areas.B1}, {= sum(table.Areas.A1:table.Areas.B1)}"]), 1, 2);
        f.Run("drafting.renameTable", new JsonObject { ["id"] = areas, ["name"] = "Rooms" });
        Assert.Equal("Rooms", f.Model.Get(areas)!.Name);
        var info = Info(f, reader);
        Assert.Equal("{= table.Rooms.A1 + table.Rooms.B1} \"x\"", (string)info["cells"]![0]![0]!["t"]!);
        Assert.Equal("{table.Rooms.B1}, {= sum(table.Rooms.A1:B1)}", (string)info["cells"]![0]![1]!["t"]!);
        Assert.Equal("2, 3", Shown(f, reader, 0, 1));
        // One undo step: the name and the references together.
        f.Session.Undo();
        Assert.Equal("Areas", f.Model.Get(areas)!.Name);
        Assert.Equal("{table.Areas.B1}, {= sum(table.Areas.A1:table.Areas.B1)}", (string)Info(f, reader)["cells"]![0]![1]!["t"]!);
        Assert.Throws<CommandException>(() => f.Run("drafting.renameTable", new JsonObject { ["id"] = areas, ["name"] = "reader" }));
        Assert.Throws<CommandException>(() => f.Run("drafting.renameTable", new JsonObject { ["id"] = areas, ["name"] = "two words" }));
        Assert.Throws<CommandException>(() => f.Run("drafting.renameTable", new JsonObject { ["id"] = areas, ["name"] = "9lives" }));
        Assert.Throws<CommandException>(() => f.Run("drafting.addTable", new JsonObject { ["viewId"] = Plan(f), ["at"] = J(0, 0), ["name"] = "Areas" }));
        // Renamed in its properties, the same.
        f.Run("element.rename", new JsonObject { ["id"] = areas, ["name"] = "Spaces" });
        Assert.StartsWith("{table.Spaces.B1}", (string)Info(f, reader)["cells"]![0]![1]!["t"]!);
        Assert.Throws<CommandException>(() => f.Run("element.rename", new JsonObject { ["id"] = areas, ["name"] = "Two words" }));
    }

    [Fact]
    public void A_table_deleted_leaves_REF_where_it_was_read_and_undo_brings_both_back()
    {
        var f = F();
        var areas = Named(f, Plan(f), "Areas", Rows(["7"]), 1, 1);
        var reader = Named(f, Plan(f), "Reader", Rows(["{= table.Areas.A1 * 2}", "{table.Areas.A1}"]), 1, 2);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(areas) });
        var info = Info(f, reader);
        Assert.Equal("{= REF * 2}", (string)info["cells"]![0]![0]!["t"]!);
        Assert.Equal("{REF}", (string)info["cells"]![0]![1]!["t"]!);
        // A new table of that name is not read in its place.
        Named(f, Plan(f), "Areas", Rows(["100"]), 1, 1);
        Assert.Equal("<REF * 2>", Shown(f, reader, 0, 0));
        f.Session.Undo();
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(areas));
        Assert.Equal("14", Shown(f, reader, 0, 0));
    }

    [Fact]
    public void Rows_put_in_or_taken_out_of_a_table_move_the_references_other_tables_make_to_it()
    {
        var f = F();
        var areas = Named(f, Plan(f), "Areas", Rows(["1"], ["2"], ["3"]), 3, 1);
        var reader = Named(f, Plan(f), "Reader", Rows(["{= table.Areas.A3}", "{= sum(table.Areas.A1:A3)}", "{= A1 + table.Reader.A1}"]), 1, 3);
        Edit(f, areas, Op("insertRows", ("at", 0), ("count", 2)));
        var info = Info(f, reader);
        Assert.Equal("{= table.Areas.A5}", (string)info["cells"]![0]![0]!["t"]!);
        Assert.Equal("{= sum(table.Areas.A3:A5)}", (string)info["cells"]![0]![1]!["t"]!);
        Assert.Equal("3", Shown(f, reader, 0, 0));
        Assert.Equal("6", Shown(f, reader, 0, 1));
        // Its own name read inside it moves with its own columns.
        Edit(f, reader, Op("insertCols", ("at", 0)));
        Assert.Equal("{= B1 + table.Reader.B1}", (string)Info(f, reader)["cells"]![0]![3]!["t"]!);
        Assert.Equal("6", Shown(f, reader, 0, 3));
        // The row read taken out: REF.
        Edit(f, areas, Op("deleteRows", ("at", 4)));
        Assert.Equal("{= REF}", (string)Info(f, reader)["cells"]![0]![1]!["t"]!);
    }
}
