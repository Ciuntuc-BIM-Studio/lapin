using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture.Schedules;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The quantity takeoff (decision 287, the tester's report #75: "how do I get quantities from the model?"): the model's
/// quantity sets added up by kind, type, storey or material, the same numbers the properties panel shows, on a sheet,
/// in its PDF, and following the model.
/// </summary>
public class QuantityTakeoffTests
{
    /// <summary>A 12 × 8 m box of walls of a plastered brick type with an inner wall, a floor, a door and a window.</summary>
    static (Fixture F, List<string> Walls, string Slab) House()
    {
        var f = new Fixture();
        var type = f.Type("Brick 30", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
        var walls = new List<string> { f.Wall(0, 0, 12, 0, type), f.Wall(12, 0, 12, 8, type), f.Wall(12, 8, 0, 8, type), f.Wall(0, 8, 0, 0, type), f.Wall(7, 0, 7, 8, type) };
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["x"] = 3, ["y"] = 0 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = walls[0], ["x"] = 9.5, ["y"] = 0 });
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(12, 0), new JsonArray(12, 8), new JsonArray(0, 8)) })!["id"]!;
        return (f, walls, slab);
    }

    static string Takeoff(Fixture f, JsonObject? options = null)
    {
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "quantities" })!["id"]!;
        if (options != null) { options["id"] = id; f.Run("architecture.updateSchedule", options); }
        return id;
    }

    static ScheduleGrid Grid(Fixture f, string id) => new QuantityTakeoff().Grid(f.Session.Reader, f.Model.Get(id)!);
    static string Cell(ScheduleGrid g, IReadOnlyList<string> row, string column) => row[g.Columns.ToList().IndexOf(column)];
    static double Sum(Fixture f, IEnumerable<string> ids, string name) =>
        ids.Sum(id => f.Session.Quantities(id).First(s => s.Part == null).Quantities.First(q => q.Name == name).Value);
    static string N(double v, string format = "0.00") => v.ToString(format, CultureInfo.InvariantCulture);

    [Fact]
    public void Adds_up_the_elements_quantities_by_kind_and_type()
    {
        var (f, walls, slab) = House();
        var g = Grid(f, Takeoff(f));
        Assert.Equal(["Kind", "Type", "Count", "Length (m)", "Gross area (m²)", "Net area (m²)", "Gross volume (m³)", "Net volume (m³)"], g.Columns.Take(8));
        var wall = g.Rows.Single(r => r[0] == "Wall");
        Assert.Equal("Brick 30", wall[1]);
        Assert.Equal("5", Cell(g, wall, "Count"));
        // The same numbers as each wall's own quantities (the properties panel's), added up.
        Assert.Equal(N(Sum(f, walls, "Length")), Cell(g, wall, "Length (m)"));
        Assert.Equal(N(Sum(f, walls, "NetSideArea")), Cell(g, wall, "Net area (m²)"));
        Assert.Equal(N(Sum(f, walls, "NetVolume"), "0.000"), Cell(g, wall, "Net volume (m³)"));
        Assert.Equal(N(12 + 8 + 12 + 8 + 8), Cell(g, wall, "Length (m)"));
        var floor = g.Rows.Single(r => r[0] == "Slab" || r[0] == "Floor");
        Assert.Equal(N(Sum(f, [slab], "GrossArea")), Cell(g, floor, "Gross area (m²)"));
        Assert.Equal("1", Cell(g, g.Rows.Single(r => r[0] == "Door"), "Count"));
        Assert.Equal("1", Cell(g, g.Rows.Single(r => r[0] == "Window"), "Count"));
        Assert.Equal("Total", g.Rows[^1][0]);
        Assert.Null(g.Keys![^1]);
    }

    [Fact]
    public void By_material_each_layer_counts_under_its_own_material()
    {
        var (f, walls, _) = House();
        var g = Grid(f, Takeoff(f, new JsonObject { ["groupBy"] = new JsonArray("material"), ["classes"] = new JsonArray(IfcClass.Wall) }));
        Assert.Equal("Material", g.Columns[0]);
        var brick = g.Rows.Single(r => r[0] == "Brick");
        var plaster = g.Rows.Single(r => r[0] == "Plaster");
        Assert.Equal("5", Cell(g, brick, "Count"));
        double V(IReadOnlyList<string> r) => double.Parse(Cell(g, r, "Net volume (m³)"), CultureInfo.InvariantCulture);
        // The materials together are the walls (the lintels' concrete too); the brick about 0.25 / 0.285 of the layers.
        Assert.Equal(Sum(f, walls, "NetVolume"), g.Rows.Where(r => r[0] != "Total").Sum(V), 2);
        Assert.Equal(Sum(f, walls, "NetVolume"), V(g.Rows[^1]), 2);
        Assert.InRange(V(brick) / (V(brick) + V(plaster)), 0.85, 0.90);
        Assert.DoesNotContain(g.Rows, r => r[0] == "Door");
    }

    [Fact]
    public void Groups_by_storey_then_kind_in_the_order_asked()
    {
        var (f, _, _) = House();
        f.Run("storey.create", []);
        var upper = Spatial.Storeys(f.Model).Last();
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = upper.Id, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0 });
        var g = Grid(f, Takeoff(f, new JsonObject { ["groupBy"] = new JsonArray("storey", "kind") }));
        Assert.Equal(["Storey", "Kind", "Count"], g.Columns.Take(3));
        var walls = g.Rows.Where(r => r[1] == "Wall").ToList();
        Assert.Equal([Spatial.Storeys(f.Model).First().Name, upper.Name], walls.Select(r => r[0]));
        Assert.Equal(["5", "1"], walls.Select(r => Cell(g, r, "Count")));
    }

    [Fact]
    public void Lists_what_it_can_and_the_kinds_of_schedule()
    {
        var (f, _, _) = House();
        var classes = (JsonArray)f.Session.Query("schedule.takeoffClasses", [])!;
        Assert.Contains(classes, c => (string?)c!["class"] == IfcClass.Wall && (int)c["count"]! == 5);
        var kinds = (JsonArray)f.Session.Query("schedule.kinds", [])!;
        Assert.Contains(kinds, k => (string?)k!["kind"] == "quantities" && (string?)k["label"] == "Quantity takeoff");
        Assert.Contains(kinds, k => (string?)k!["kind"] == "rooms");
        Assert.Contains(kinds, k => (string?)k!["kind"] == "all");
    }

    [Fact]
    public void Is_drawn_on_a_sheet_and_in_its_PDF_and_follows_the_model()
    {
        var (f, walls, _) = House();
        var id = Takeoff(f);
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Quantities", ["paper"] = "A3" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = id, ["x"] = 200, ["y"] = 130 });
        List<string> Texts() => Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        var texts = Texts();
        foreach (var t in new[] { "Quantity takeoff", "Wall", "Brick 30", "Net volume (m³)", N(Sum(f, walls, "NetVolume"), "0.000"), "Total" }) Assert.Contains(t, texts);
        // The PDF writes the table's words and numbers.
        var pdf = PdfContent(Sheets.Pdf(f.Session.Reader, [f.Model.Get(sheet)!]));
        Assert.Contains("Quantity takeoff", pdf);
        Assert.Contains(N(Sum(f, walls, "Length")), pdf);
        // Another wall: the sheet's table counts it.
        walls.Add(f.Wall(0, 4, 7, 4));
        Assert.Contains(N(Sum(f, walls, "Length")), Texts());
    }

    [Fact]
    public void Leaves_rooms_out_and_counts_a_door_whole_by_material()
    {
        var (f, _, _) = House();
        f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 3, ["y"] = 4 });
        // A room is air, not built: its volume would swell the totals (it has its own schedule).
        Assert.DoesNotContain(Grid(f, Takeoff(f)).Rows, r => r[0] == "Room" || r[0] == "Space");
        var g = Grid(f, Takeoff(f, new JsonObject { ["groupBy"] = new JsonArray("kind", "material") }));
        // A door measured by its opening: one row, its area, under no one material.
        var door = g.Rows.Single(r => r[0] == "Door");
        Assert.Equal("—", door[1]);
        Assert.Equal(N(0.9 * 2.1), Cell(g, door, "Gross area (m²)"));
    }

    [Fact]
    public void A_schedule_placed_without_a_point_starts_at_the_frames_top_left()
    {
        var (f, _, _) = House();
        var id = Takeoff(f);
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Quantities", ["paper"] = "A3" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = id });
        var placed = Sheets.Place(f.Session.Reader, f.Model.Get(sheet)!).Single();
        // Inside the frame (10 mm in), not centred on the paper and past its left edge.
        Assert.Equal(Sheets.Margin + 4, placed.X0, 3);
        Assert.Equal(Sheets.Margin + 4, placed.Y0, 3);
        Assert.True(placed.X1 < Sheets.PaperOf(f.Model.Get(sheet)!).W - Sheets.Margin);
    }

    /// <summary>A PDF's page contents, inflated.</summary>
    static string PdfContent(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var o = new StringBuilder();
        for (var at = text.IndexOf("/FlateDecode >>\nstream\n", StringComparison.Ordinal); at >= 0; at = text.IndexOf("/FlateDecode >>\nstream\n", at + 1, StringComparison.Ordinal))
        {
            var start = at + "/FlateDecode >>\nstream\n".Length;
            var end = text.IndexOf("\nendstream", start, StringComparison.Ordinal);
            using var z = new System.IO.Compression.ZLibStream(new MemoryStream(pdf[start..end]), System.IO.Compression.CompressionMode.Decompress);
            using var r = new StreamReader(z, Encoding.Latin1);
            o.Append(r.ReadToEnd());
        }
        return o.ToString();
    }

    [Fact]
    public void Exports_to_CSV_and_XLSX_with_its_grouping()
    {
        var (f, _, _) = House();
        var id = Takeoff(f, new JsonObject { ["groupBy"] = new JsonArray("kind", "material") });
        var csv = ((string)f.Session.Query("schedule.csv", new JsonObject { ["id"] = id })!).Split('\n');
        Assert.StartsWith("Kind,Material,Count,", csv[0]);
        Assert.Contains(csv, l => l.StartsWith("Wall,Brick,", StringComparison.Ordinal));
        var xlsx = Convert.FromBase64String((string)f.Session.Query("schedule.xlsx", new JsonObject { ["id"] = id })!);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(xlsx));
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
    }
}
