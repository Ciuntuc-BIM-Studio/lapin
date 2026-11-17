using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Long drafting tables and exchange (docs/plan-drafting-tables.md, stage 5): a table broken into parts after so many rows or
/// at a height, each part with the header rows again, laid beside the first or placed on a sheet of its own; its cells out as
/// CSV and XLSX and in from them, as one undo step.
/// </summary>
public class DraftingTableLongTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray P(double x, double y) => new(x, y);
    static string Plan(Fixture f) => "plan:" + f.Storey;

    static JsonArray Rows(int body)
    {
        var o = new JsonArray((JsonNode)new JsonArray("Room", "Area"));
        for (var i = 1; i <= body; i++) o.Add((JsonNode)new JsonArray($"R{i}", $"{i}"));
        return o;
    }

    static string Long(Fixture f, int body = 10)
    {
        var id = (string)f.Run("drafting.addTable", new JsonObject { ["viewId"] = Plan(f), ["at"] = P(0, 0), ["rows"] = body + 1, ["cols"] = 2, ["preset"] = "header", ["cells"] = Rows(body) })!["id"]!;
        return id;
    }

    static void Edit(Fixture f, string id, params JsonObject[] ops) =>
        f.Run("drafting.editTable", new JsonObject { ["id"] = id, ["ops"] = new JsonArray([.. ops]) });

    static JsonObject Op(string op, params (string K, JsonNode? V)[] kv)
    {
        var o = new JsonObject { ["op"] = op };
        foreach (var (k, v) in kv) o[k] = v;
        return o;
    }

    static List<ViewMark> Marks(Fixture f, string view) => [.. Views.Render(f.Session.Reader, Views.Find(f.Model, view)!).Marks];
    static JsonObject Info(Fixture f, string id) => (JsonObject)f.Session.Query("drafting.table", new JsonObject { ["id"] = id })!;

    [Fact]
    public void A_long_table_breaks_after_so_many_rows_each_part_with_its_header_again()
    {
        var f = F();
        var id = Long(f, 10);
        Edit(f, id, Op("parts", ("rows", 4), ("gap", 5)));
        Assert.Equal(3, (int)Info(f, id)["parts"]!);
        var mk = Marks(f, Plan(f)).Single(m => m.Owner == id);
        // The header three times, every room once.
        Assert.Equal(3, mk.Texts.Count(t => t.Text.Text == "Room"));
        Assert.Single(mk.Texts, t => t.Text.Text == "R10");
        // Laid right of each other: 50 mm a part and 5 mm between, at 1:100 (the second part's header 5.5 m along).
        var second = mk.Texts.Where(t => t.Text.Text == "Room").Select(t => t.Text.At.X).OrderBy(x => x).ToList();
        Assert.Equal(5.5, second[1] - second[0], 6);
        // R5 heads the second part's body: right under its header.
        var r1 = mk.Texts.Single(t => t.Text.Text == "R1").Text.At;
        var r5 = mk.Texts.Single(t => t.Text.Text == "R5").Text.At;
        Assert.Equal(r1.Y, r5.Y, 6);
        Assert.Equal(3, mk.Hit!.Value.Loops.Count);
        // Down, the parts stack.
        Edit(f, id, Op("parts", ("rows", 4), ("gap", 5), ("dir", "down")));
        mk = Marks(f, Plan(f)).Single(m => m.Owner == id);
        Assert.Equal(mk.Texts.Single(t => t.Text.Text == "R1").Text.At.X, mk.Texts.Single(t => t.Text.Text == "R5").Text.At.X, 6);
        // Off: one table again.
        Edit(f, id, Op("parts", ("off", true)));
        Assert.Equal(1, (int)Info(f, id)["parts"]!);
        Assert.Throws<CommandException>(() => Edit(f, id, Op("parts")));
    }

    [Fact]
    public void A_table_breaks_at_a_height_and_never_through_a_merged_cell()
    {
        var f = F();
        var id = Long(f, 6);
        // 7 mm rows: a part of 30 mm holds the header and three rows.
        Edit(f, id, Op("parts", ("height", 30)));
        Assert.Equal(2, (int)Info(f, id)["parts"]!);
        var mk = Marks(f, Plan(f)).Single(m => m.Owner == id);
        // The second part starts with R4, right under its header (as R1 is under the first's).
        Assert.Equal(mk.Texts.Single(t => t.Text.Text == "R1").Text.At.Y, mk.Texts.Single(t => t.Text.Text == "R4").Text.At.Y, 6);
        // A merge across the break (rows 4 and 5, R3 and R4): the break comes after it, the second part starts with R5.
        Edit(f, id, Op("merge", ("range", "A4:A5")));
        mk = Marks(f, Plan(f)).Single(m => m.Owner == id);
        var r1 = mk.Texts.Single(t => t.Text.Text == "R1").Text.At;
        var r5 = mk.Texts.Single(t => t.Text.Text == "R5").Text.At;
        Assert.Equal(r1.Y, r5.Y, 6);
        Assert.NotEqual(r1.X, r5.X, 6);
    }

    [Fact]
    public void A_part_placed_on_a_sheet_of_its_own_is_left_out_beside_the_table_and_goes_back_when_deleted()
    {
        var f = F();
        var id = Long(f, 8);
        Edit(f, id, Op("parts", ("rows", 4)));
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Rooms 2", ["paper"] = "A4" })!["id"]!;
        var paper = PaperViews.IdOf(sheet);
        var part = (string)f.Run("drafting.placeTablePart", new JsonObject { ["id"] = id, ["part"] = 1, ["viewId"] = paper, ["at"] = P(0.02, 0.28) })!["id"]!;
        var here = Marks(f, Plan(f)).Single(m => m.Owner == id);
        Assert.DoesNotContain(here.Texts, t => t.Text.Text == "R5");
        var there = Marks(f, paper).Single(m => m.Owner == part);
        Assert.Contains(there.Texts, t => t.Text.Text == "R5");
        Assert.Contains(there.Texts, t => t.Text.Text == "Room");
        // It reads its table live.
        Edit(f, id, Op("set", ("cell", "A6"), ("text", "Hall")));
        Assert.Contains(Marks(f, paper).Single(m => m.Owner == part).Texts, t => t.Text.Text == "Hall");
        // The sheet prints it.
        Assert.Contains(Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!).Pens.SelectMany(p => p.Texts), t => t.Text == "Hall");
        Assert.Throws<CommandException>(() => f.Run("drafting.placeTablePart", new JsonObject { ["id"] = id, ["part"] = 1, ["viewId"] = paper, ["at"] = P(0, 0) }));
        Assert.Throws<CommandException>(() => f.Run("drafting.placeTablePart", new JsonObject { ["id"] = id, ["part"] = 5, ["viewId"] = paper, ["at"] = P(0, 0) }));
        // Deleted: the part is beside the table again.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(part) });
        Assert.Contains(Marks(f, Plan(f)).Single(m => m.Owner == id).Texts, t => t.Text.Text == "Hall");
        // The table deleted takes its placed parts with it.
        part = (string)f.Run("drafting.placeTablePart", new JsonObject { ["id"] = id, ["part"] = 1, ["viewId"] = paper, ["at"] = P(0.02, 0.28) })!["id"]!;
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(id) });
        Assert.Null(f.Model.Get(part));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(part));
        // It moves with the Modify tools.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(part), ["op"] = "move", ["by"] = P(0.01, 0) });
        Assert.Equal(0.03, Args.PointOf(f.Model.Get(part)!.Attrs["at"]).X, 9);
    }

    [Fact]
    public void Cells_go_out_as_CSV_and_XLSX_as_they_read_or_as_typed()
    {
        var f = F();
        var id = (string)f.Run("drafting.addTable", new JsonObject
        {
            ["viewId"] = Plan(f), ["at"] = P(0, 0), ["rows"] = 2, ["cols"] = 2, ["name"] = "Areas",
            ["cells"] = new JsonArray((JsonNode)new JsonArray("Room; A", "Area"), (JsonNode)new JsonArray("x", "{= 2 * 3}")),
        })!["id"]!;
        var csv = (string)f.Session.Query("drafting.tableExport", new JsonObject { ["id"] = id, ["format"] = "csv" })!["text"]!;
        Assert.Equal("\"Room; A\";Area\r\nx;6\r\n", csv);
        var typed = (string)f.Session.Query("drafting.tableExport", new JsonObject { ["id"] = id, ["format"] = "csv", ["typed"] = true })!["text"]!;
        Assert.Contains("{= 2 * 3}", typed);
        var x = (JsonObject)f.Session.Query("drafting.tableExport", new JsonObject { ["id"] = id, ["format"] = "xlsx" })!;
        Assert.Equal("Areas.xlsx", (string)x["file"]!);
        var back = Lapin.Drawing.Export.XlsxReader.Read(Convert.FromBase64String((string)x["data"]!));
        Assert.Equal(["Room; A", "Area"], back[0]);
        Assert.Equal(["x", "6"], back[1]);
    }

    [Fact]
    public void Cells_come_in_from_CSV_or_a_workbook_the_table_growing_in_one_undo_step()
    {
        var f = F();
        var id = (string)f.Run("drafting.addTable", new JsonObject { ["viewId"] = Plan(f), ["at"] = P(0, 0), ["rows"] = 2, ["cols"] = 2 })!["id"]!;
        Edit(f, id, Op("load", ("text", "Room;Area;Floor\r\nLiving;24,5;oak\r\nKitchen;12;tiles\r\n"), ("at", "A1")));
        var info = Info(f, id);
        Assert.Equal(3, (int)info["rows"]!);
        Assert.Equal(3, (int)info["cols"]!);
        Assert.Equal("tiles", (string)info["cells"]![2]![2]!["t"]!);
        f.Session.Undo();
        Assert.Equal(2, (int)Info(f, id)["rows"]!);
        // A workbook (as Lapin writes one), from B2 on.
        var book = Lapin.Drawing.Export.XlsxWriter.Write([("S", (IReadOnlyList<IReadOnlyList<string>>)[["a", "1"], ["b", "2"]])]);
        Edit(f, id, Op("load", ("data", Convert.ToBase64String(book)), ("at", "B2")));
        info = Info(f, id);
        Assert.Equal(3, (int)info["rows"]!);
        Assert.Equal(3, (int)info["cols"]!);
        Assert.Equal("a", (string)info["cells"]![1]![1]!["t"]!);
        Assert.Equal("2", (string)info["cells"]![2]![2]!["t"]!);
        Assert.Throws<CommandException>(() => Edit(f, id, Op("load", ("text", ""))));
        Assert.Throws<CommandException>(() => Edit(f, id, Op("load", ("data", "not base64!"))));
    }
}
