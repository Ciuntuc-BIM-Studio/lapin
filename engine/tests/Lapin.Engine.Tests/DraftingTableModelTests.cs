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

    // ------------------------------------------------------------ common names of quantities

    static double Q(Fixture f, string id, string name) =>
        f.Session.Quantities(id).SelectMany(s => s.Quantities).First(q => q.Name == name).Value;

    static string F2(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void A_room_reads_its_area_perimeter_and_volume_by_the_names_people_type()
    {
        var f = F();
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 5, ["name"] = "Living" })!["id"]!;
        var t = Table(f, Rows(
            ["{= el.NetArea | 0.00}", "{= el.area | 0.00}", "{= el.Area | 0.00}", "{= el.NetFloorArea | 0.00}", "{el.area}"],
            ["{= el.Perimeter | 0.00}", "{= el.perimeter | 0.00}", "{= el.GrossArea | 0.00}", "{= el.Volume | 0.00}", "{= el.Height | 0.00}"]), 2, 5);
        Edit(f, t, Op("link", ("on", "table"), ("el", room)));
        Assert.Equal(["20.00", "20.00", "20.00", "20.00", "20"], Enumerable.Range(0, 5).Select(c => Shown(f, t, 0, c)));
        Assert.Equal("18.00", Shown(f, t, 1, 0));
        Assert.Equal("18.00", Shown(f, t, 1, 1));
        Assert.Equal("20.00", Shown(f, t, 1, 2));
        Assert.Equal(F2(Q(f, room, "NetVolume")), Shown(f, t, 1, 3));
        Assert.Equal(F2(Q(f, room, "Height")), Shown(f, t, 1, 4));
        Assert.Empty(Errors(f, t));
        // A schedule column and a tag read the same names (the one field dictionary).
        var reader = new ElementFields(f.Session.Reader, f.Model.Get(room)!);
        Assert.Equal(20, reader.Value("Area").Number!.Value, 6);
        Assert.Equal(18, reader.Value("perimeter").Number!.Value, 6);
    }

    [Fact]
    public void A_wall_and_a_door_read_their_area_and_volume_by_the_same_names()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = w, ["along"] = 2.0 })!["id"]!;
        var t = Table(f, Rows(
            ["{= el.Area | 0.00}", "{= el.NetArea | 0.00}", "{= el.GrossArea | 0.00}", "{= el.Volume | 0.00}", "{= el.Length | 0.00}"],
            ["{= el.Area | 0.00}", "{= el.NetArea | 0.00}", "{= el.area | 0.00}", "{= el.Width | 0.00}", "{= el.Height | 0.00}"]), 2, 5);
        Edit(f, t, Op("link", ("on", "rows"), ("range", "A1"), ("el", w)), Op("link", ("on", "rows"), ("range", "A2"), ("el", door)));
        Assert.Equal(F2(Q(f, w, "NetSideArea")), Shown(f, t, 0, 0));
        Assert.Equal(F2(Q(f, w, "NetSideArea")), Shown(f, t, 0, 1));
        Assert.Equal(F2(Q(f, w, "GrossSideArea")), Shown(f, t, 0, 2));
        Assert.Equal(F2(Q(f, w, "NetVolume")), Shown(f, t, 0, 3));
        Assert.Equal("5.00", Shown(f, t, 0, 4));
        Assert.NotEqual(Shown(f, t, 0, 0), Shown(f, t, 0, 2));
        var area = F2(Q(f, door, "Area"));
        Assert.Equal([area, area, area], Enumerable.Range(0, 3).Select(c => Shown(f, t, 1, c)));
        Assert.Equal(F2(Q(f, door, "Width")), Shown(f, t, 1, 3));
        Assert.Equal(F2(Q(f, door, "Height")), Shown(f, t, 1, 4));
        Assert.Empty(Errors(f, t));
    }

    [Fact]
    public void The_fields_of_a_room_offer_its_quantities_and_their_common_names()
    {
        var f = F();
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 5, ["name"] = "Living" })!["id"]!;
        var all = f.Session.Query("fields.available", new JsonObject { ["id"] = room })!["groups"]!.AsArray()
            .SelectMany(g => g!["fields"]!.AsArray()).Select(x => ((string)x!["token"]!, (string?)x["value"])).ToList();
        Assert.Contains(all, x => x.Item1 == "NetFloorArea");
        Assert.Contains(all, x => x.Item1 == "Area" && x.Item2!.StartsWith("20"));
        Assert.Contains(all, x => x.Item1 == "Perimeter" && x.Item2!.StartsWith("18"));
        Assert.Contains(all, x => x.Item1 == "Volume");
    }

    // ------------------------------------------------------------ suggestions while typing

    static JsonObject Suggest(Fixture f, string id, string cell, string text, int? caret = null) =>
        (JsonObject)f.Session.Query("drafting.tableSuggest", new JsonObject { ["id"] = id, ["cell"] = cell, ["text"] = text, ["caret"] = caret ?? text.Length })!;

    static List<string> Inserts(JsonObject s) => [.. s["items"]!.AsArray().Select(x => (string)x!["insert"]!)];

    [Fact]
    public void Suggestions_offer_what_fits_where_the_caret_is_as_the_evaluator_reads_it()
    {
        var f = F();
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 5, ["name"] = "Living" })!["id"]!;
        f.Wall(0, 0, 5, 0);
        Schedule(f, "Wall schedule");
        f.Run("viewFilter.create", new JsonObject { ["name"] = "Load bearing", ["rules"] = J(new JsonObject { ["field"] = "name", ["op"] = "beginsWith", ["value"] = "LB" }) });
        Table(f, Rows(["1", "2"]), 1, 2, "Areas");
        var t = Table(f, Rows(["", ""]), 1, 2);
        Edit(f, t, Op("link", ("range", "A1"), ("el", room)));

        // Outside braces: nothing.
        Assert.Empty(Inserts(Suggest(f, t, "A1", "Area ")));
        // After {= : functions and the roots.
        var start = Suggest(f, t, "A1", "{= ");
        Assert.Contains("sum(", Inserts(start));
        Assert.Contains("el.", Inserts(start));
        Assert.Contains("schedule.", Inserts(start));
        Assert.Contains(start["items"]!.AsArray(), x => (string)x!["insert"]! == "round(" && ((string?)x["detail"])!.Contains("round("));
        // Typing filters, and what is replaced is the word typed.
        var su = Suggest(f, t, "A1", "{= su");
        Assert.Equal(["sum("], Inserts(su));
        Assert.Equal(3, (int)su["from"]!);
        Assert.Equal(5, (int)su["to"]!);
        // el. : the linked element's fields with their values now, its quantities and their common names too.
        var el = Suggest(f, t, "A1", "{= el.");
        Assert.Contains("el.NetFloorArea", Inserts(el));
        Assert.Contains(el["items"]!.AsArray(), x => (string)x!["insert"]! == "el.Area" && ((string?)x["detail"])!.StartsWith("20"));
        Assert.Contains("el.Perimeter", Inserts(Suggest(f, t, "A1", "{= el.per")));
        // A cell reading no element is told so.
        Assert.Empty(Inserts(Suggest(f, t, "B1", "{el.")));
        Assert.NotNull(Suggest(f, t, "B1", "{el.")["note"]);
        // schedule. : names, then what of it, then its columns.
        Assert.Contains("schedule.WallSchedule.", Inserts(Suggest(f, t, "A1", "{= schedule.")));
        Assert.Contains("schedule.WallSchedule.total.", Inserts(Suggest(f, t, "A1", "{= schedule.WallSchedule.")));
        Assert.Contains("schedule.WallSchedule.count", Inserts(Suggest(f, t, "A1", "{schedule.WallSchedule.c")));
        Assert.Contains("schedule.WallSchedule.total.Length", Inserts(Suggest(f, t, "A1", "{schedule.WallSchedule.total.")));
        // filter. : names, count and sums of fields.
        Assert.Contains("filter.LoadBearing.", Inserts(Suggest(f, t, "A1", "{= filter.")));
        Assert.Contains("filter.LoadBearing.count", Inserts(Suggest(f, t, "A1", "{= filter.LoadBearing.")));
        Assert.Contains("filter.LoadBearing.sum.Length", Inserts(Suggest(f, t, "A1", "{= filter.LoadBearing.sum.Le")));
        // table. : the other tables by name, then their cells with what they read.
        Assert.Contains("table.Areas.", Inserts(Suggest(f, t, "A1", "{= table.")));
        var cells = Suggest(f, t, "A1", "{= table.Areas.");
        Assert.Contains(cells["items"]!.AsArray(), x => (string)x!["insert"]! == "table.Areas.B1" && (string?)x["detail"] == "2");
        // Inside a function's parentheses, after an operator: the same.
        Assert.Equal(["el.NetFloorArea"], Inserts(Suggest(f, t, "A1", "{= round(el.NetFl")));
        // In a token (no =): no functions.
        Assert.DoesNotContain("sum(", Inserts(Suggest(f, t, "A1", "{")));
        Assert.Contains("el.", Inserts(Suggest(f, t, "A1", "{")));
        // The caret before the end: only the text before it counts.
        Assert.Equal(["sum("], Inserts(Suggest(f, t, "A1", "{= su} m²", 5)));
        // Every element field suggested reads (no error) when put in.
        foreach (var token in Inserts(el).Take(40))
        {
            Edit(f, t, Op("set", ("cell", "A1"), ("text", "{" + token + "}")));
            Assert.DoesNotContain(Errors(f, t), e => e.StartsWith("A1"));
        }
    }

    [Fact]
    public void A_cell_s_text_is_tried_without_being_kept_its_own_errors_alone()
    {
        var f = F();
        var t = Table(f, Rows(["2", "{= nothing + 1}", ""]), 1, 3);
        JsonObject Try(string text) => (JsonObject)f.Session.Query("drafting.tableTry", new JsonObject { ["id"] = t, ["cell"] = "C1", ["text"] = text })!;
        var ok = Try("{= A1 * 3}");
        Assert.Equal("6", (string)ok["shown"]!);
        // B1's error is not C1's.
        Assert.Empty(ok["errors"]!.AsArray());
        Assert.NotEmpty(Try("{= el.Area}")["errors"]!.AsArray());
        Assert.NotEmpty(Try("{= C1 + 1}")["errors"]!.AsArray());
        // Nothing kept.
        Assert.Equal("", Typed(f, t, 0, 2));
    }

    // ------------------------------------------------------------ AutoSum

    [Fact]
    public void AutoSum_in_a_cell_sums_the_numbers_above_it_else_those_left_of_it_as_a_spreadsheet_does()
    {
        var f = F();
        var t = Table(f, Rows(["Room", "Area", "Cost"], ["A", "10", "{= B2 * 2}"], ["B", "20", "{= B3 * 2}"], ["Total", "", ""], ["", "", ""], ["1", "2", ""]), 6, 3);
        Edit(f, t, Op("header", ("range", "A1:C1"), ("on", true)));
        // Up to the header row: B2:B3; over formulas too.
        Edit(f, t, Op("autoSum", ("range", "B4")));
        Assert.Equal("{= sum(B2:B3)}", Typed(f, t, 3, 1));
        Assert.Equal("30", Shown(f, t, 3, 1));
        Edit(f, t, Op("autoSum", ("range", "C4"), ("fn", "max")));
        Assert.Equal("{= max(C2:C3)}", Typed(f, t, 3, 2));
        Assert.Equal("40", Shown(f, t, 3, 2));
        // Nothing numeric above (an empty cell): the numbers left of it.
        Edit(f, t, Op("autoSum", ("range", "C6")));
        Assert.Equal("{= sum(A6:B6)}", Typed(f, t, 5, 2));
        Assert.Equal("3", Shown(f, t, 5, 2));
        // Nothing to sum (a text above, nothing left): refused, the table as it was.
        Assert.Throws<CommandException>(() => Edit(f, t, Op("autoSum", ("range", "A5"))));
        // One undo step each.
        f.Session.Undo();
        Assert.Equal("", Typed(f, t, 5, 2));
    }

    [Fact]
    public void AutoSum_over_columns_puts_a_total_under_each_in_the_first_empty_row_adding_one_if_none()
    {
        var f = F();
        var t = Table(f, Rows(["Area", "Cost"], ["10", "100"], ["20", "200"]), 3, 2);
        Edit(f, t, Op("autoSum", ("range", "A1:B3"), ("fn", "average")));
        Assert.Equal(4, (int)Info(f, t)["rows"]!);
        Assert.Equal("{= average(A2:A3)}", Typed(f, t, 3, 0));
        Assert.Equal("150", Shown(f, t, 3, 1));
        // One undo step takes the row and the totals back.
        f.Session.Undo();
        Assert.Equal(3, (int)Info(f, t)["rows"]!);
    }

    [Fact]
    public void A_total_follows_rows_put_in_above_it_and_elements_linked_a_row_each()
    {
        var f = F();
        var walls = new[] { f.Wall(0, 0, 5, 0), f.Wall(0, 3, 4, 3), f.Wall(0, 6, 3, 6) };
        var t = Table(f, Rows(["Wall", "Length"], ["{el.name}", "{= el.Length}"], ["Total", ""]), 3, 2);
        Edit(f, t, Op("linkRows", ("at", "A2"), ("els", J(walls[0]))));
        Edit(f, t, Op("autoSum", ("range", "B3")));
        Assert.Equal("{= sum(B2:B2)}", Typed(f, t, 2, 1));
        Assert.Equal("5", Shown(f, t, 2, 1));
        // Linked a row each again: the rows go in above the total (not over it), and its sum takes them in.
        Edit(f, t, Op("linkRows", ("at", "A2"), ("els", J([.. walls.Select(x => (JsonNode)x)]))));
        Assert.Equal(5, (int)Info(f, t)["rows"]!);
        Assert.Equal("Total", Typed(f, t, 4, 0));
        Assert.Equal("{= el.Length}", Typed(f, t, 3, 1));
        Assert.Equal("{= sum(B2:B4)}", Typed(f, t, 4, 1));
        Assert.Equal("12", Shown(f, t, 4, 1));
        // A row put in right above the total is taken in too; one taken out narrows it.
        Edit(f, t, Op("insertRows", ("at", 4)));
        Assert.Equal("{= sum(B2:B5)}", Typed(f, t, 5, 1));
        Edit(f, t, Op("deleteRows", ("at", 1), ("count", 1)));
        Assert.Equal("{= sum(B2:B4)}", Typed(f, t, 4, 1));
        Assert.Equal("7", Shown(f, t, 4, 1));
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

    [Fact]
    public void A_cell_counts_what_the_filter_editor_counts()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        f.Wall(0, 3, 3, 3);
        // A filter without rules takes nothing; one on the class takes the walls on storeys, as the editor counts.
        var empty = (string)f.Run("viewFilter.create", new JsonObject { ["name"] = "Empty", ["rules"] = new JsonArray() })!["id"]!;
        var walls = (string)f.Run("viewFilter.create", new JsonObject { ["name"] = "Walls", ["rules"] = J(new JsonObject { ["field"] = "class", ["op"] = "equals", ["value"] = "IfcWall" }) })!["id"]!;
        var t = Table(f, Rows(["{= filter.Empty.count}", "{= filter.Walls.count}"]), 1, 2);
        Assert.Equal(0, (int)f.Session.Query("viewFilter.count", new JsonObject { ["id"] = empty })!);
        Assert.Equal("0", Shown(f, t, 0, 0));
        Assert.Equal(2, (int)f.Session.Query("viewFilter.count", new JsonObject { ["id"] = walls })!);
        Assert.Equal("2", Shown(f, t, 0, 1));
    }

    [Fact]
    public void A_cell_that_is_one_formula_is_worked_out_once()
    {
        var f = F();
        var t = Table(f, Rows(["{= A1 + 1}", "{= 2 * 3}", " {= 1 / 4 | 0.00} "]), 1, 3);
        Assert.Equal("<A1 + 1>", Shown(f, t, 0, 0));
        Assert.Single(Errors(f, t), e => e.Contains("reads itself"));
        Assert.Equal("6", Shown(f, t, 0, 1));
        Assert.Equal("0.25", Shown(f, t, 0, 2).Trim());
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
