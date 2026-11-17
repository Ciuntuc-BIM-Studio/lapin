using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Drafting tables from the model (docs/plan-drafting-tables.md §3b, stage 4): a table in a symbol whose cells read each placed
/// copy's live text and lay its own sizes; cells linked to elements reading their fields through the one field dictionary, live
/// as the model changes, saying when the element is gone; a schedule's count, totals and cells; a view filter's count and sums.
/// </summary>
public class DraftingTableModelTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(params JsonNode?[] items) => new(items);
    static JsonArray P(double x, double y) => new(x, y);
    static string Plan(Fixture f) => "plan:" + f.Storey;
    static JsonArray Rows(params string[][] rows) => new([.. rows.Select(r => (JsonNode)new JsonArray([.. r.Select(x => (JsonNode)x)]))]);

    static string Table(Fixture f, JsonArray cells, int rows, int cols, string? name = null)
    {
        var a = new JsonObject { ["viewId"] = Plan(f), ["at"] = P(1, 5), ["rows"] = rows, ["cols"] = cols, ["preset"] = "grid", ["cells"] = cells };
        if (name != null) a["name"] = name;
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
    static string Typed(Fixture f, string id, int r, int c) => (string?)Info(f, id)["cells"]![r]![c]!["t"] ?? "";
    static List<string> Errors(Fixture f, string id) => ((JsonArray)Info(f, id)["errors"]!).Select(x => (string)x!).ToList();

    static void Param(Fixture f, string id, string name, JsonNode value, string? pset = null)
    {
        var a = new JsonObject { ["id"] = id, ["name"] = name, ["value"] = value, ["kind"] = value.GetValueKind() == System.Text.Json.JsonValueKind.Number ? "number" : "text" };
        if (pset != null) a["pset"] = pset;
        f.Run("element.setParam", a);
    }

    // ------------------------------------------------------------ cells linked to elements

    [Fact]
    public void A_cell_linked_to_an_element_reads_its_fields_as_a_tag_and_a_formula_would()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        Param(f, w, "Ratio", 2.5);
        Param(f, w, "FireRating", "REI 90", "Pset_WallCommon");
        f.Run("element.rename", new JsonObject { ["id"] = w, ["name"] = "W-01" });
        var t = Table(f, Rows(["{el.name}", "{el.FireRating}", "{= el.Ratio * 2}", "{= el.Length | 0.00} m"]), 1, 4);
        // Not linked yet: it does not read, and says how.
        Assert.Equal("<el.name>", Shown(f, t, 0, 0));
        Assert.Contains(Errors(f, t), e => e.Contains("reads no element"));
        Edit(f, t, Op("link", ("range", "A1:D1"), ("el", w)));
        Assert.Equal("W-01", Shown(f, t, 0, 0));
        Assert.Equal("REI 90", Shown(f, t, 0, 1));
        Assert.Equal("5", Shown(f, t, 0, 2));
        Assert.Equal("5.00 m", Shown(f, t, 0, 3));
        Assert.Empty(Errors(f, t));
        // The query says which element each cell reads.
        Assert.Equal(w, (string)Info(f, t)["cells"]![0]![0]!["el"]!);
        Assert.Equal("W-01", (string)Info(f, t)["cells"]![0]![0]!["elName"]!);
        // Live: the element changes, the cell follows; the drawing too.
        Param(f, w, "Ratio", 4);
        f.Run("architecture.updateWall", new JsonObject { ["id"] = w, ["x1"] = 0, ["y1"] = 0, ["x2"] = 7, ["y2"] = 0 });
        Assert.Equal("8", Shown(f, t, 0, 2));
        Assert.Equal("7.00 m", Shown(f, t, 0, 3));
        Assert.Contains(Views.Render(f.Session.Reader, Views.Find(f.Model, Plan(f))!).Marks.Single(m => m.Owner == t).Texts, x => x.Text.Text == "7.00 m");
        // Linking to what is not there is refused; one undo step takes the link off.
        Assert.Throws<CommandException>(() => Edit(f, t, Op("link", ("range", "A1"), ("el", "no-such-element"))));
    }

    [Fact]
    public void A_row_or_the_table_links_its_cells_the_nearest_link_winning()
    {
        var f = F();
        var a = f.Wall(0, 0, 5, 0);
        var b = f.Wall(0, 3, 4, 3);
        var c = f.Wall(0, 6, 3, 6);
        foreach (var (id, n) in new[] { (a, "A"), (b, "B"), (c, "C") }) f.Run("element.rename", new JsonObject { ["id"] = id, ["name"] = n });
        var t = Table(f, Rows(["{el.name}", "{el.name}"], ["{el.name}", "{el.name}"]), 2, 2);
        Edit(f, t, Op("link", ("on", "table"), ("el", a)), Op("link", ("on", "rows"), ("range", "A2"), ("el", b)), Op("link", ("range", "B2"), ("el", c)));
        Assert.Equal("A", Shown(f, t, 0, 0));
        Assert.Equal("B", Shown(f, t, 1, 0));
        Assert.Equal("C", Shown(f, t, 1, 1));
        Assert.Equal("row", (string)Info(f, t)["cells"]![1]![0]!["elFrom"]!);
        // Unlinked (el null), the row's link reads again.
        Edit(f, t, Op("link", ("range", "B2"), ("el", null)));
        Assert.Equal("B", Shown(f, t, 1, 1));
    }

    [Fact]
    public void Elements_linked_a_row_each_fill_the_first_row_s_formulas_down()
    {
        var f = F();
        var walls = new[] { f.Wall(0, 0, 5, 0), f.Wall(0, 3, 4, 3), f.Wall(0, 6, 3, 6) };
        for (var i = 0; i < walls.Length; i++) f.Run("element.rename", new JsonObject { ["id"] = walls[i], ["name"] = $"W{i + 1}" });
        var t = Table(f, Rows(["Wall", "Length", "Twice"], ["{el.name}", "{= el.Length | 0.0}", "{= B2 * 2}"], ["", "", ""]), 3, 3);
        Edit(f, t, Op("linkRows", ("at", "A2"), ("els", J([.. walls.Select(x => (JsonNode)x)]))));
        // A row each (one added past the end), the first row's texts copied down with their references moved.
        Assert.Equal(4, (int)Info(f, t)["rows"]!);
        Assert.Equal(["W1", "W2", "W3"], Enumerable.Range(1, 3).Select(r => Shown(f, t, r, 0)));
        Assert.Equal(["5.0", "4.0", "3.0"], Enumerable.Range(1, 3).Select(r => Shown(f, t, r, 1)));
        Assert.Equal("{= B4 * 2}", Typed(f, t, 3, 2));
        Assert.Equal("6", Shown(f, t, 3, 2));
        // One undo step takes it all back.
        f.Session.Undo();
        Assert.Equal(3, (int)Info(f, t)["rows"]!);
        Assert.Equal("", Typed(f, t, 2, 0));
    }

    [Fact]
    public void A_deleted_element_s_cell_says_it_is_gone_and_undo_brings_its_value_back()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        f.Run("element.rename", new JsonObject { ["id"] = w, ["name"] = "Kept" });
        var t = Table(f, Rows(["{el.name}"]), 1, 1);
        Edit(f, t, Op("link", ("range", "A1"), ("el", w)));
        f.Run("element.delete", new JsonObject { ["ids"] = J(w) });
        Assert.Equal("<el.name>", Shown(f, t, 0, 0));
        Assert.Contains(Errors(f, t), e => e.Contains("its element is gone"));
        Assert.True((bool)Info(f, t)["cells"]![0]![0]!["elGone"]!);
        f.Session.Undo();
        Assert.Equal("Kept", Shown(f, t, 0, 0));
    }

    [Fact]
    public void Links_are_saved_with_the_project_and_read_the_same_once_opened()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        Param(f, w, "Ratio", 3);
        var t = Table(f, Rows(["{= el.Ratio + 1}"]), 1, 1);
        Edit(f, t, Op("link", ("on", "table"), ("el", w)));
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        var g = F();
        var content = LapinPackage.Read(new MemoryStream(ms.ToArray()));
        g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Assert.Equal("4", Shown(g, t, 0, 0));
    }

    // ------------------------------------------------------------ schedules and filters

    static string Schedule(Fixture f, string name) =>
        (string)f.Run("architecture.createSchedule", new JsonObject
        {
            ["kind"] = "fields", ["name"] = name,
            ["fields"] = new JsonObject
            {
                ["filter"] = new JsonObject { ["classes"] = J("IfcWall") },
                ["columns"] = J(
                    new JsonObject { ["field"] = "name" },
                    new JsonObject { ["field"] = "Length", ["heading"] = "Length", ["unit"] = "m", ["decimals"] = 2, ["aggregate"] = "sum" }),
            },
        })!["id"]!;

    [Fact]
    public void A_cell_reads_a_schedule_s_count_total_aggregates_and_cells_by_its_name()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        f.Wall(0, 3, 3, 3);
        Schedule(f, "Wall schedule");
        var t = Table(f, Rows(
            ["{= schedule.WallSchedule.count}", "{schedule.WallSchedule.total.Length}", "{= schedule.wallschedule.max.length * 10}"],
            ["{schedule.WallSchedule.B1}", "{= schedule.WallSchedule.average.B | 0.0}", "{= schedule.Nothing.count}"]), 2, 3);
        Assert.Equal("2", Shown(f, t, 0, 0));
        Assert.Equal("8.00", Shown(f, t, 0, 1));
        Assert.Equal("50", Shown(f, t, 0, 2));
        Assert.Equal("Length (m)", Shown(f, t, 1, 0));
        Assert.Equal("4.0", Shown(f, t, 1, 1));
        Assert.Equal("<schedule.Nothing.count>", Shown(f, t, 1, 2));
        Assert.Contains(Errors(f, t), e => e.Contains("no schedule „Nothing”"));
        // The model changes, the schedule and the cell follow.
        f.Wall(0, 6, 2, 6);
        Assert.Equal("3", Shown(f, t, 0, 0));
        Assert.Equal("10.00", Shown(f, t, 0, 1));
    }

    [Fact]
    public void A_cell_counts_and_adds_up_what_a_view_filter_takes()
    {
        var f = F();
        var a = f.Wall(0, 0, 5, 0);
        var b = f.Wall(0, 3, 3, 3);
        var c = f.Wall(0, 6, 2, 6);
        foreach (var (w, n) in new[] { (a, "LB1"), (b, "LB2"), (c, "X1") }) f.Run("element.rename", new JsonObject { ["id"] = w, ["name"] = n });
        f.Run("viewFilter.create", new JsonObject { ["name"] = "Load bearing", ["rules"] = J(new JsonObject { ["field"] = "name", ["op"] = "beginsWith", ["value"] = "LB" }) });
        var t = Table(f, Rows(["{= filter.LoadBearing.count}", "{= filter.LoadBearing.sum.Length | 0.0}", "{= filter.Missing.count}"]), 1, 3);
        Assert.Equal("2", Shown(f, t, 0, 0));
        Assert.Equal("8.0", Shown(f, t, 0, 1));
        Assert.StartsWith("<", Shown(f, t, 0, 2));
    }

    // ------------------------------------------------------------ tables in symbols

    static JsonObject TableItem(params string[][] rows) => new()
    {
        ["type"] = "table", ["at"] = P(0, 0),
        ["table"] = new JsonObject
        {
            ["cols"] = J(new JsonObject { ["w"] = 10, ["fit"] = true }, new JsonObject { ["w"] = 10, ["fit"] = true }),
            ["rows"] = new JsonArray([.. rows.Select(r => (JsonNode)new JsonObject { ["h"] = 5, ["fit"] = true, ["cells"] = new JsonArray([.. r.Select(x => (JsonNode)new JsonObject { ["t"] = x })]) })]),
            ["style"] = new JsonObject { ["size"] = 2.5, ["pad"] = 1, ["grid"] = new JsonObject { ["w"] = 0.18, ["c"] = "#000000" }, ["outline"] = new JsonObject { ["w"] = 0.35, ["c"] = "#000000" } },
        },
    };

    [Fact]
    public void A_table_in_a_symbol_reads_each_copy_s_attributes_and_lays_its_own_sizes()
    {
        var f = F();
        var def = new JsonObject
        {
            ["scale"] = "annotation", ["category"] = "annotation",
            ["items"] = J(TableItem(["{attr.name}", "{= attr.area | 0.00} m²"])),
            ["attributes"] = J(new JsonObject { ["key"] = "name", ["default"] = "Room" }, new JsonObject { ["key"] = "area", ["default"] = "0" }),
        };
        var sym = (string)f.Run("symbols.create", new JsonObject { ["name"] = "Room box", ["def"] = def })!["id"]!;
        string Place(double x, string name, string area) => (string)f.Run("drafting.placeSymbol", new JsonObject
        {
            ["symbolId"] = sym, ["viewId"] = Plan(f), ["at"] = P(x, 0), ["values"] = new JsonObject { ["name"] = name, ["area"] = area },
        })!["id"]!;
        var small = Place(0, "WC", "3.5");
        var big = Place(20, "Living room", "24.456");
        var r = Views.Render(f.Session.Reader, Views.Find(f.Model, Plan(f))!);
        var ms = r.Marks.Single(m => m.Owner == small);
        var mb = r.Marks.Single(m => m.Owner == big);
        Assert.Contains(ms.Texts, t => t.Text.Text == "WC");
        Assert.Contains(mb.Texts, t => t.Text.Text == "24.46 m²");
        // Each copy as wide as its own texts: the living room's first column 11 characters, the WC's 2 (fit, with padding).
        double Width(ViewMark mk) => mk.Lines.Where(l => l.Layer != "PICK").Max(l => Math.Max(l.A.X, l.B.X)) - mk.Lines.Where(l => l.Layer != "PICK").Min(l => Math.Min(l.A.X, l.B.X));
        Assert.Equal((11 * 1.5 + 2 + 8 * 1.5 + 2) * 0.1, Width(mb) - 0, 6);
        Assert.Equal((2 * 1.5 + 2 + 7 * 1.5 + 2) * 0.1, Width(ms), 6);
        // A tag's fields come first in its cells (a tag symbol's {number}), as in its texts.
        var d = SymbolDef.Parse(def);
        var sd = SymbolRender.Render(f.Model, d with { Items = J(TableItem(["{number}", "{= attr.area * 2}"])) }, k => k == "attr.area" ? (true, "2") : (false, null), 0.001, null, raw => raw.Replace("{number}", "7"));
        Assert.Contains(sd.Texts, t => t.Text == "7");
        Assert.Contains(sd.Texts, t => t.Text == "4");
        Assert.NotEmpty(sd.Strokes);
        Assert.Throws<CommandException>(() => SymbolDef.Parse(new JsonObject { ["items"] = J(new JsonObject { ["type"] = "table", ["at"] = P(0, 0) }) }));
    }

    [Fact]
    public void A_table_drawn_on_a_symbol_s_board_becomes_its_item_and_an_untouched_one_stays_byte_for_byte()
    {
        var f = F();
        var def = new JsonObject { ["scale"] = "annotation", ["category"] = "annotation", ["items"] = J(new JsonObject { ["type"] = "line", ["a"] = P(-5, 0), ["b"] = P(5, 0) }) };
        var sym = (string)f.Run("symbols.create", new JsonObject { ["name"] = "Tag box", ["def"] = def })!["id"]!;
        var view = (string)f.Run("drafting.editSymbol", new JsonObject { ["symbolId"] = sym })!["viewId"]!;
        // A table drawn on the board (its metres the symbol's mm / 1000).
        f.Run("drafting.addTable", new JsonObject { ["viewId"] = view, ["at"] = P(0.002, 0.004), ["rows"] = 1, ["cols"] = 2, ["preset"] = "grid", ["cells"] = Rows(["{attr.no}", "x"]) });
        f.Run("drafting.finishSymbol", new JsonObject { ["viewId"] = view });
        var items = f.Model.Get(sym)!.Attrs["def"]!["items"]!.AsArray();
        var table = items.Single(i => (string)i!["type"]! == "table")!;
        Assert.Equal(2, (double)table["at"]![0]!, 6);
        Assert.Equal(4, (double)table["at"]![1]!, 6);
        // The attribute its cell names is asked for.
        Assert.Contains(f.Model.Get(sym)!.Attrs["def"]!["attributes"]!.AsArray(), a => (string)a!["key"]! == "no");
        // Opened and finished again unchanged: the same definition.
        var before = f.Model.Get(sym)!.Attrs["def"]!.ToJsonString();
        view = (string)f.Run("drafting.editSymbol", new JsonObject { ["symbolId"] = sym })!["viewId"]!;
        Assert.Contains(f.Model.OfClass(IfcClass.Annotation), e => e.Str("viewId") == view && e.PredefinedType == DraftingTables.Predefined);
        f.Run("drafting.finishSymbol", new JsonObject { ["viewId"] = view });
        Assert.Equal(before, f.Model.Get(sym)!.Attrs["def"]!.ToJsonString());
    }

    [Fact]
    public void Save_as_symbol_takes_a_table_and_explode_turns_it_back_into_lines_and_texts()
    {
        var f = F();
        var t = Table(f, Rows(["a", "b"]), 1, 2);
        var made = f.Run("drafting.toSymbol", new JsonObject { ["viewId"] = Plan(f), ["ids"] = J(t), ["base"] = P(1, 5), ["name"] = "Two cells" })!;
        var instance = (string)made["instanceId"]!;
        Assert.Null(f.Model.Get(t));
        var exploded = f.Run("drafting.explodeSymbol", new JsonObject { ["ids"] = J(instance) })!["ids"]!.AsArray();
        var texts = exploded.Select(x => f.Model.Get((string)x!)!).Where(e => e.PredefinedType == "TEXT").Select(e => e.Str("text")).ToList();
        Assert.Contains("a", texts);
        Assert.Contains("b", texts);
        Assert.Contains(exploded.Select(x => f.Model.Get((string)x!)!), e => e.PredefinedType == Drafts.Predefined);
    }
}
