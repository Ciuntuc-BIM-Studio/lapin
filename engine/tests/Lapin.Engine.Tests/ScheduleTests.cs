using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Joinery and lintel schedules. Ported from the old Lapin's
/// schedule-layout.test.ts, schedule-table.test.ts, schedule-parts.test.ts
/// and lintel-schedule.test.ts (same cases and numbers where the model allows:
/// in Lapin a door or window is always typed, and "external" is its type's).
/// </summary>
public class ScheduleTests
{
    static string Type(Fixture f, JoinerySpec spec) => (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
    static void Place(Fixture f, string wall, string type, double along, bool door = false) =>
        f.Run(door ? "architecture.addDoor" : "architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = along, ["typeId"] = type });
    static string WallOn(Fixture f, string storey, double y) =>
        (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = storey, ["x1"] = 0, ["y1"] = y, ["x2"] = 12, ["y2"] = y })!["id"]!;

    /// <summary>
    /// Ground floor and Level 1: window type W1 (1.2 × 1.5) three times on the ground
    /// floor and twice upstairs, door type D1 (0.9 × 2.1) once on the ground floor.
    /// </summary>
    static (Fixture F, string W1, string D1) House()
    {
        var f = new Fixture();
        f.Run("storey.create", []);
        var (ground, first) = (Spatial.Storeys(f.Model).First().Id, Spatial.Storeys(f.Model).Last().Id);
        var w1 = Type(f, JoinerySpec.DefaultWindow("W") with { Mark = "W1" });
        var d1 = Type(f, JoinerySpec.DefaultDoor("D") with { Mark = "D1" });
        var g = WallOn(f, ground, 0);
        Place(f, g, w1, 2); Place(f, g, w1, 5); Place(f, g, w1, 8); Place(f, g, d1, 10.5, door: true);
        var u = WallOn(f, first, 0);
        Place(f, u, w1, 3); Place(f, u, w1, 7);
        return (f, w1, d1);
    }

    static string Schedule(Fixture f, string kind = "all") => (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = kind })!["id"]!;
    static ScheduleData Data(Fixture f, string kind = "all") => Schedules.Of(f.Session.Reader, kind);
    static List<string> Texts(IEnumerable<Pen> pens) => pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();
    static ScheduleTable Plain => new(null, [], new Dictionary<string, Dictionary<string, string>>(), null);

    [Fact]
    public void A_card_per_type_with_its_size_counts_per_level_and_total()
    {
        var (f, _, _) = House();
        var data = Data(f);
        Assert.Equal(["W1", "D1"], data.Entries.Select(e => e.Mark));
        Assert.Equal(["Ground floor", "Level 1"], data.Levels);
        var w = data.Entries[0];
        Assert.Equal((3, 2, 5), (w.Counts["Ground floor"], w.Counts["Level 1"], w.Total));
        Assert.Equal("W1.3", Schedules.OccurrenceMark(w, w.Occurrences[2]));
        Assert.Equal("Ground floor", w.Occurrences[2].Level);
        var bands = ScheduleLayout.Bands(data, ScheduleTable.Of(f.Model.Get(Schedule(f))!));
        var texts = Texts(bands.SelectMany(b => b.Pens));
        foreach (var t in new[] { "W1", "D1", "1200", "1500", "900", "2100", "Ground floor", "Level 1", "3", "2", "5", "tilt & turn left + side hung right" })
            Assert.Contains(t, texts);
        Assert.True(bands[0].W > 34 + 2 * 30 - 1);
        Assert.True(bands.SelectMany(b => b.Pens).Sum(p => p.Segments.Count) > 10);
    }

    [Fact]
    public void Drawn_at_the_schedule_s_scale_and_wrapped_to_the_width_asked()
    {
        var (f, _, _) = House();
        var data = Data(f);
        var at50 = ScheduleLayout.Bands(data, Plain, 50);
        var at20 = ScheduleLayout.Bands(data, Plain, 20);
        Assert.True(at20[0].W > at50[0].W);
        var narrow = ScheduleLayout.Bands(data, Plain, 50, 80);
        Assert.True(narrow.Sum(b => b.H) > 1.5 * at50.Sum(b => b.H));
    }

    [Fact]
    public void Exports_CSV_with_a_column_per_level()
    {
        var (f, _, _) = House();
        var csv = ScheduleEntity.ToCsv(f.Model.Get(Schedule(f))!, Data(f)).Split('\n');
        Assert.Equal("Mark,Name,Width (mm),Height (mm),Opening,Sill,Ground floor,Level 1,Total,U,Fire", csv[0]);
        Assert.Equal("W1,W,1200,1500,tilt & turn left + side hung right,900,3,2,5,1.1,", csv[1]);
        Assert.StartsWith("D1,D,900,2100,swing left,,1,0,1,", csv[2]);
    }

    [Fact]
    public void Custom_rows_row_order_card_order_and_typed_cells()
    {
        var f = new Fixture();
        var wall = WallOn(f, f.Storey, 0);
        var a = Type(f, JoinerySpec.DefaultWindow("Window W2") with { Mark = "W2" });
        var b = Type(f, JoinerySpec.DefaultWindow("Window W1") with { Mark = "W1", Width = 0.9 });
        Place(f, wall, a, 2);
        foreach (var x in new[] { 4.0, 6, 8, 10 }) Place(f, wall, b, x);
        var id = Schedule(f, "window");
        var row = (string)f.Run("architecture.addScheduleRow", new JsonObject { ["id"] = id, ["label"] = "Manufacturer" })!["rowId"]!;
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["rows"] = new JsonArray(row, "name", "total"), ["sort"] = "width" });
        f.Run("architecture.scheduleCell", new JsonObject { ["id"] = id, ["entry"] = b, ["row"] = row, ["text"] = "Rehau" });
        var data = Data(f, "window");
        Assert.Equal(["Manufacturer", "Name", "Total"], ScheduleLayout.Rows(data, ScheduleTable.Of(f.Model.Get(id)!)).Select(r => r.Label));
        var texts = Texts(ScheduleLayout.Bands(data, ScheduleTable.Of(f.Model.Get(id)!)).SelectMany(x => x.Pens));
        Assert.Contains("Rehau", texts);
        Assert.DoesNotContain("Opening", texts);
        Assert.True(texts.IndexOf("W1") < texts.IndexOf("W2"));
        f.Run("architecture.scheduleCell", new JsonObject { ["id"] = id, ["entry"] = a, ["row"] = "name", ["text"] = "Fereastră living" });
        Assert.Contains("Fereastră living", ScheduleEntity.ToCsv(f.Model.Get(id)!, data));
        f.Run("architecture.scheduleCell", new JsonObject { ["id"] = id, ["entry"] = a, ["row"] = "name", ["text"] = null });
        Assert.Contains("Window W2", Texts(ScheduleLayout.Bands(data, ScheduleTable.Of(f.Model.Get(id)!)).SelectMany(x => x.Pens)));
        var notes = (string)f.Run("architecture.addScheduleRow", new JsonObject { ["id"] = id, ["label"] = "Notes" })!["rowId"]!;
        f.Run("architecture.scheduleCell", new JsonObject { ["id"] = id, ["entry"] = a, ["row"] = notes, ["text"] = "see detail 3" });
        var csv = ScheduleEntity.ToCsv(f.Model.Get(id)!, data).Split('\n');
        Assert.EndsWith(",Notes", csv[0]);
        Assert.Contains(csv, l => l.EndsWith(",see detail 3"));
        f.Run("architecture.removeScheduleRow", new JsonObject { ["id"] = id, ["rowId"] = notes });
        f.Run("architecture.removeScheduleRow", new JsonObject { ["id"] = id, ["rowId"] = row });
        Assert.Empty(f.Model.Get(id)!.Attrs["customRows"]!.AsArray());
        Assert.Null(f.Model.Get(id)!.Attrs["cells"]);
    }

    [Fact]
    public void Tables_by_kind_and_level_and_the_check_of_what_two_list()
    {
        var (f, _, d1) = House();
        var id = Schedule(f);
        var data = Data(f);
        var windowsUp = ScheduleEntity.PartData(data, new SchedulePart("a", "Up", Kind: "window", Levels: ["Level 1"]));
        Assert.Equal([("W1", 2)], windowsUp.Entries.Select(e => (e.Mark, e.Total)));
        Assert.Equal(["Level 1"], windowsUp.Levels);
        Assert.Equal(["D1"], ScheduleEntity.PartData(data, new SchedulePart("b", "D", Types: [d1])).Entries.Select(e => e.Mark));
        // The default window type is external, the default door type is not.
        Assert.Equal([("W1", 5)], ScheduleEntity.PartData(data, new SchedulePart("c", "Ext", External: true)).Entries.Select(e => (e.Mark, e.Total)));
        Assert.Equal([("D1", 1)], ScheduleEntity.PartData(data, new SchedulePart("i", "Int", External: false)).Entries.Select(e => (e.Mark, e.Total)));
        f.Run("architecture.updateSchedule", new JsonObject
        {
            ["id"] = id,
            ["parts"] = new JsonArray(new JsonObject { ["id"] = "w", ["title"] = "Windows", ["kind"] = "window" }, new JsonObject { ["id"] = "g", ["title"] = "Ground floor", ["levels"] = new JsonArray("Ground floor") }),
        });
        var s = f.Model.Get(id)!;
        var (twice, none, marks) = ScheduleEntity.Check(s, data);
        Assert.Equal(3, twice.Count);       // the ground floor's windows
        Assert.Empty(none);
        Assert.Empty(marks);
        Assert.Equal(["Windows", "Ground floor"], ScheduleEntity.Tables(s, data).Select(t => t.Title));
        Type(f, JoinerySpec.DefaultWindow("Other") with { Mark = "W1" });
        Assert.Contains(ScheduleEntity.Check(f.Model.Get(Schedule(f))!, Data(f)).Marks, m => m.Mark == "W1" && m.Count == 2);
    }

    [Fact]
    public void Bands_page_with_continued_titles()
    {
        var f = new Fixture();
        var wall = WallOn(f, f.Storey, 0);
        for (var i = 0; i < 12; i++) Place(f, wall, Type(f, JoinerySpec.DefaultWindow($"Window {i}") with { Mark = $"W{i}", Width = 0.6 }), 0.5 + i * 0.95);
        var bands = ScheduleLayout.Bands(Data(f, "window"), Plain, 50, 120);
        Assert.True(bands.Count > 2);
        Assert.Single(ScheduleLayout.Pages([("Windows", bands)], null));
        var max = bands[0].H * 2 + 30;
        var pages = ScheduleLayout.Pages([("Windows", bands)], max);
        Assert.True(pages.Count > 1);
        Assert.Equal(["Windows", "Windows (continued)"], pages.Take(2).Select(p => p.Pens.SelectMany(x => x.Texts).First(t => t.Height == 5).Text));
        Assert.All(pages, p => Assert.True(p.H <= max + 1e-6));
    }

    [Fact]
    public void Lintels_are_grouped_and_marked_and_the_joinery_rows_name_them()
    {
        var f = new Fixture();
        var wall = WallOn(f, f.Storey, 0);
        var w1 = Type(f, JoinerySpec.DefaultWindow("W1") with { Mark = "W1", Width = 1, Height = 1.2 });
        var w2 = Type(f, JoinerySpec.DefaultWindow("W2") with
        {
            Mark = "W2", Width = 1, Height = 1.2,
            Lintel = new LintelRule("cast", 0.25, 0.25, Reinforcement: new LintelReinforcement(Bottom: new LintelBars(2, 0.012))),
        });
        Place(f, wall, w1, 2); Place(f, wall, w1, 5); Place(f, wall, w2, 8);
        var lintels = Data(f, "lintel");
        Assert.Equal(["B1", "B2"], lintels.Entries.Select(e => e.Mark));
        Assert.True(lintels.Entries[0].Lintel!.Precast);
        Assert.False(lintels.Entries[1].Lintel!.Precast);
        Assert.True(lintels.Entries[1].Lintel!.SteelKg > 0);
        Assert.Equal([["W1"], ["W2"]], lintels.Entries.Select(e => e.Lintel!.Openings));
        var joinery = Data(f, "window");
        var lintelRow = ScheduleLayout.Rows(joinery, Plain).Single(r => r.Id == "lintel");
        Assert.Equal([("W1", "B1"), ("W2", "B2")], joinery.Entries.Select(e => (e.Mark, lintelRow.Value(e))));
        var s = f.Model.Get(Schedule(f, "lintel"))!;
        var csv = ScheduleEntity.ToCsv(s, lintels).Split('\n');
        Assert.StartsWith("Mark,Precast or cast,Product,Section W × H (mm),\"Length (mm; ! no stock length)\"", csv[0]);   // quoted: it holds a ;
        Assert.StartsWith("B1,Precast", csv[1]);
        Assert.Contains("B1", Texts(ScheduleLayout.Bands(lintels, ScheduleTable.Of(s)).SelectMany(b => b.Pens)));
    }

    [Fact]
    public void A_schedule_is_placed_on_a_sheet_and_undoes_with_the_model()
    {
        var (f, _, _) = House();
        var id = Schedule(f);
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Joinery", ["paper"] = "A3" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = id, ["x"] = 200, ["y"] = 130 });
        var page = Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!);
        Assert.Contains("W1", Texts(page.Pens));
        Assert.Contains("Joinery schedule", Texts(page.Pens));
        f.Session.Undo(); f.Session.Undo(); f.Session.Undo();
        Assert.Null(f.Model.Get(id));
    }

    [Fact]
    public void A_schedule_goes_to_XLSX_a_sheet_a_table_numbers_as_numbers()
    {
        var (f, _, _) = House();
        var id = Schedule(f);
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["parts"] = new JsonArray(new JsonObject { ["id"] = "d", ["title"] = "Doors", ["kind"] = "door" }, new JsonObject { ["id"] = "w", ["title"] = "Windows", ["kind"] = "window" }) });
        var bytes = Convert.FromBase64String((string)f.Session.Query("schedule.xlsx", new JsonObject { ["id"] = id })!);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
        string Read(string n) { using var r = new StreamReader(zip.GetEntry(n)!.Open()); return r.ReadToEnd(); }
        var book = Read("xl/workbook.xml");
        Assert.Contains("name=\"Doors\"", book);
        Assert.Contains("name=\"Windows\"", book);
        var sheet = Read("xl/worksheets/sheet1.xml");
        Assert.Contains("<t xml:space=\"preserve\">Mark</t>", sheet);
        Assert.Matches("<c r=\"C2\"><v>\\d+</v></c>", sheet); // the width, a number
        Assert.Equal("AA", Lapin.Drawing.Export.XlsxWriter.Column(26));
        if (Environment.GetEnvironmentVariable("LAPIN_EXPORT_DIR") is { Length: > 0 } dir) File.WriteAllBytes(Path.Combine(dir, "schedule.xlsx"), bytes);
    }

    [Fact]
    public void A_card_can_draw_its_type_in_plan_beside_its_views()
    {
        var (f, _, _) = House();
        var id = Schedule(f, "door");
        double W() => (double)((JsonObject)f.Session.Query("schedule.layout", new JsonObject { ["id"] = id })!)["w"]!;
        var one = W();
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["drawings"] = new JsonArray("interior", "plan") });
        Assert.True(W() > one);
        // The door's plan: its leaf open and its arc, besides the wall stubs.
        var spec = Schedules.Of(f.Session.Reader, "door").Entries[0].Spec!;
        var plan = JoineryPlan.Of(spec, 0.3);
        Assert.Contains(plan, st => st.Weight == "thin");
        Assert.True(plan.Min(st => Math.Min(st.A.Y, st.B.Y)) < -0.15 - spec.Width / 2);
    }
}
