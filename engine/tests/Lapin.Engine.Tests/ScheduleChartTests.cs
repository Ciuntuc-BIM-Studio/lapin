using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A tester's questions (decision 251): a door's fire rating and the types' parameters in the joinery schedule, the types
/// nothing uses left out, rows and columns of the schedule's own in every schedule, and charts drawn from the table.
/// </summary>
public class ScheduleChartTests
{
    static string Type(Fixture f, JoinerySpec spec) => (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
    static string Place(Fixture f, string wall, string type, double along, bool door = false) =>
        (string)f.Run(door ? "architecture.addDoor" : "architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = along, ["typeId"] = type })!["id"]!;
    static string Schedule(Fixture f, string kind = "all") => (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = kind })!["id"]!;
    static JsonObject Layout(Fixture f, string id) => (JsonObject)f.Session.Query("schedule.layout", new JsonObject { ["id"] = id })!;
    static string Csv(Fixture f, string id) => (string)f.Session.Query("schedule.csv", new JsonObject { ["id"] = id })!;
    static void Param(Fixture f, string id, string name, string value, string? pset = null) =>
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(id), ["name"] = name, ["value"] = value, ["kind"] = pset == null ? "text" : null, ["pset"] = pset });

    /// <summary>W1 twice and D1 once in a 12 m wall; W2 and D2 made but placed nowhere.</summary>
    static (Fixture F, string W1, string D1, string W2, string Door) House()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 12, 0);
        var w1 = Type(f, JoinerySpec.DefaultWindow("Window 1200") with { Mark = "W1" });
        var d1 = Type(f, JoinerySpec.DefaultDoor("Door 900") with { Mark = "D1" });
        var w2 = Type(f, JoinerySpec.DefaultWindow("Window unused") with { Mark = "W2" });
        Type(f, JoinerySpec.DefaultDoor("Door unused") with { Mark = "D2" });
        Place(f, wall, w1, 2); Place(f, wall, w1, 5);
        var door = Place(f, wall, d1, 9, door: true);
        return (f, w1, d1, w2, door);
    }

    static Dictionary<string, string> Row(JsonObject layout, string mark) =>
        ((JsonArray)layout["entries"]!).OfType<JsonObject>().Single(e => (string?)e["mark"] == mark)["rows"]!.AsObject().ToDictionary(x => x.Key, x => (string?)x.Value ?? "");

    [Fact]
    public void A_fire_rating_typed_on_a_door_or_its_type_fills_the_fire_rating_row()
    {
        var (f, w1, _, _, door) = House();
        var s = Schedule(f);
        Assert.Equal("—", Row(Layout(f, s), "D1")["fire"]);
        // The tester's way: a parameter of their own on the door.
        Param(f, door, "Fire Rated", "2 h");
        Assert.Equal("2 h", Row(Layout(f, s), "D1")["fire"]);
        // IFC's own: Pset_WindowCommon.FireRating on the window type, which its windows inherit.
        Param(f, w1, "FireRating", "EI 30", "Pset_WindowCommon");
        Assert.Equal("EI 30", Row(Layout(f, s), "W1")["fire"]);
        Assert.Contains("EI 30", Csv(f, s));
        Assert.Contains("2 h", Csv(f, s));
    }

    [Fact]
    public void The_types_parameters_are_rows_and_a_placed_ones_own_value_shows_beside_the_types()
    {
        var (f, w1, d1, _, door) = House();
        Param(f, w1, "Manufacturer", "Rehau");
        Param(f, d1, "Manufacturer", "Porta");
        Param(f, d1, "Website", "porta.example");
        var s = Schedule(f);
        var layout = Layout(f, s);
        var rows = ((JsonArray)layout["rows"]!).OfType<JsonObject>().ToList();
        var manufacturer = rows.Single(r => (string?)r["id"] == "param:Manufacturer");
        Assert.Equal("Manufacturer", (string?)manufacturer["param"]);
        Assert.Contains(rows, r => (string?)r["id"] == "param:Website");
        Assert.Equal("Rehau", Row(layout, "W1")["param:Manufacturer"]);
        Assert.Equal("—", Row(layout, "W1")["param:Website"]);
        Assert.Equal("porta.example", Row(layout, "D1")["param:Website"]);
        // The door its own maker: both values show for its type.
        Param(f, door, "Manufacturer", "Other");
        Assert.Equal("Other", Row(Layout(f, s), "D1")["param:Manufacturer"]);
        Assert.Contains("Manufacturer", Csv(f, s).Split('\n')[0]);
        // The fire rating is a row of its own, not a parameter row besides.
        Param(f, d1, "Fire rating", "EI2 30");
        Assert.DoesNotContain(((JsonArray)Layout(f, s)["rows"]!).OfType<JsonObject>(), r => (string?)r["id"] == "param:Fire rating");
    }

    [Fact]
    public void Types_nothing_uses_are_left_out_unless_the_schedule_shows_them()
    {
        var (f, _, _, _, _) = House();
        var s = Schedule(f);
        string[] Marks() => ((JsonArray)Layout(f, s)["entries"]!).Select(e => (string)e!["mark"]!).ToArray();
        Assert.Equal(["W1", "D1"], Marks());
        Assert.Equal(2, (int)Layout(f, s)["unusedCount"]!);
        Assert.DoesNotContain("W2", Csv(f, s));
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = s, ["unused"] = true });
        Assert.Equal(["W1", "W2", "D1", "D2"], Marks());
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = s, ["unused"] = null });
        Assert.Equal(["W1", "D1"], Marks());
    }

    [Fact]
    public void A_column_of_the_joinery_schedules_own_is_a_card_of_typed_values()
    {
        var (f, _, _, _, _) = House();
        var s = Schedule(f);
        var column = (string)f.Run("architecture.addScheduleColumn", new JsonObject { ["id"] = s, ["label"] = "G1" })!["column"]!;
        Assert.StartsWith("manual:", column);
        f.Run("architecture.scheduleCell", new JsonObject { ["id"] = s, ["entry"] = column, ["row"] = "name", ["text"] = "Garage door" });
        f.Run("architecture.scheduleCell", new JsonObject { ["id"] = s, ["entry"] = column, ["row"] = "total", ["text"] = "1" });
        var layout = Layout(f, s);
        var g = ((JsonArray)layout["entries"]!).OfType<JsonObject>().Single(e => (string?)e["mark"] == "G1");
        Assert.True((bool)g["manual"]!);
        Assert.Equal("Garage door", Row(layout, "G1")["name"]);
        Assert.Equal("", Row(layout, "G1")["size"]);
        Assert.Contains("G1,Garage door", Csv(f, s));
        // It prints with the cards, its mark in the band.
        Assert.Contains("G1", ((JsonArray)layout["pens"]!).OfType<JsonObject>().SelectMany(p => (p["texts"] as JsonArray ?? []).Select(t => (string?)t!["text"])));
        f.Run("architecture.renameScheduleColumn", new JsonObject { ["id"] = s, ["column"] = column, ["label"] = "G-1" });
        Assert.Contains(((JsonArray)Layout(f, s)["entries"]!).OfType<JsonObject>(), e => (string?)e["mark"] == "G-1");
        f.Run("architecture.removeScheduleColumn", new JsonObject { ["id"] = s, ["column"] = column });
        Assert.DoesNotContain("Garage door", Csv(f, s));
        Assert.Null(f.Model.Get(s)!.Attrs["cells"]);
        Assert.Throws<CommandException>(() => f.Run("architecture.addScheduleColumn", new JsonObject { ["id"] = Schedule(f, "lintel"), ["label"] = "X" }));
    }

    static (Fixture F, string Schedule, string Kitchen) Rooms3()
    {
        var f = new Fixture(new ArchitectureModule());
        string Room(double x1, double x2, string name, string function) =>
            (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = 0, ["x2"] = x2, ["y2"] = 4, ["name"] = name, ["function"] = function })!["id"]!;
        var kitchen = Room(0, 3, "Kitchen", "Living");
        Room(3, 8, "Living room", "Living");
        Room(8, 10, "Bath", "Wet");
        return (f, Schedule(f, "rooms"), kitchen);
    }

    static JsonObject Grid(Fixture f, string s) => (JsonObject)Layout(f, s)["grid"]!;
    static List<string> Cells(JsonObject grid, int row) => ((JsonArray)((JsonArray)grid["rows"]!)[row]!["cells"]!).Select(c => (string)c!).ToList();

    [Fact]
    public void A_module_schedule_takes_columns_and_rows_of_its_own_and_cells_typed_over_the_models()
    {
        var (f, s, _) = Rooms3();
        var grid = Grid(f, s);
        Assert.Equal(4, ((JsonArray)grid["rows"]!).Count);
        Assert.Equal("Total", Cells(grid, 3)[0]);
        var column = (string)f.Run("architecture.addScheduleColumn", new JsonObject { ["id"] = s, ["label"] = "Floor finish" })!["column"]!;
        var kitchenKey = (string)((JsonArray)Grid(f, s)["rows"]!)[0]!["key"]!;
        f.Run("architecture.scheduleCell", new JsonObject { ["id"] = s, ["entry"] = kitchenKey, ["row"] = column, ["text"] = "Tiles" });
        f.Run("architecture.scheduleCell", new JsonObject { ["id"] = s, ["entry"] = kitchenKey, ["row"] = "c:Department", ["text"] = "Day" });
        var row = (string)f.Run("architecture.addScheduleManualRow", new JsonObject { ["id"] = s })!["row"]!;
        f.Run("architecture.scheduleCell", new JsonObject { ["id"] = s, ["entry"] = row, ["row"] = "c:Name", ["text"] = "Garden shed" });
        f.Run("architecture.scheduleCell", new JsonObject { ["id"] = s, ["entry"] = row, ["row"] = "c:Area (m²)", ["text"] = "6.00" });
        grid = Grid(f, s);
        Assert.Equal("Floor finish", (string)((JsonArray)grid["columns"]!)[^1]!["label"]!);
        Assert.Equal("Tiles", Cells(grid, 0)[^1]);
        Assert.Equal("Day", Cells(grid, 0)[4]);
        Assert.Contains("c:Department", ((JsonArray)((JsonArray)grid["rows"]!)[0]!["typed"]!).Select(t => (string)t!));
        // The row of its own before the totals line.
        Assert.Equal("Garden shed", Cells(grid, 3)[2]);
        Assert.Equal("Total", Cells(grid, 4)[0]);
        var csv = Csv(f, s);
        Assert.Contains("Floor finish", csv);
        Assert.Contains("Garden shed", csv);
        // Sorted by area, largest first; the totals line stays last.
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = s, ["sortBy"] = "c:Area (m²)", ["sortDesc"] = true });
        grid = Grid(f, s);
        Assert.Equal(["Living room", "Kitchen", "Bath", "Garden shed", ""], Enumerable.Range(0, 5).Select(i => Cells(grid, i)[2]));
        // The kitchen's typed values follow it while it moves down the list.
        Assert.Equal("Tiles", Cells(grid, 1)[^1]);
        f.Run("architecture.removeScheduleManualRow", new JsonObject { ["id"] = s, ["row"] = row });
        f.Run("architecture.removeScheduleColumn", new JsonObject { ["id"] = s, ["column"] = column });
        Assert.DoesNotContain("Garden shed", Csv(f, s));
        Assert.DoesNotContain("Tiles", Csv(f, s));
        Assert.Throws<CommandException>(() => f.Run("architecture.addScheduleManualRow", new JsonObject { ["id"] = Schedule(f, "all") }));
    }

    [Fact]
    public void A_chart_adds_up_a_column_by_another_and_is_drawn_after_the_table()
    {
        var (f, s, _) = Rooms3();
        f.Run("architecture.scheduleChart", new JsonObject { ["id"] = s, ["chart"] = new JsonObject { ["kind"] = "pie", ["category"] = "Function", ["value"] = "Area (m²)", ["aggregate"] = "sum" } });
        var (columns, rows) = ScheduleEntity.ChartGrid(new Q(f), f.Model.Get(s)!);
        var chart = ScheduleChart.Of(f.Model.Get(s)!).Single();
        Assert.Equal("Sum of Area (m²) by Function", chart.Heading);
        var values = ScheduleCharts.Values(chart, columns, rows);
        Assert.Equal([("Living", 32.0), ("Wet", 8.0)], values.Select(v => (v.Label, Math.Round(v.Value, 6))));
        var count = ScheduleCharts.Values(chart with { Aggregate = "count", Value = null }, columns, rows);
        Assert.Equal([2.0, 1.0], count.Select(v => v.Value));
        var top = ScheduleCharts.Values(chart with { Category = "Name", Top = 2 }, columns, rows);
        Assert.Equal(["Living room", "Other"], top.Select(v => v.Label));
        Assert.Equal(20.0, top[0].Value, 6);
        Assert.Equal(20.0, top[1].Value, 6);
        // Drawn after the table: its slices filled, its legend written (on screen, sheets and in the PDF alike).
        var layout = Layout(f, s);
        var pens = ((JsonArray)layout["pens"]!).OfType<JsonObject>().ToList();
        Assert.Contains(pens, p => (string?)p["layer"] == "SCHEDULE-CHART" && p["loops"] is JsonArray { Count: > 0 });
        var texts = pens.SelectMany(p => (p["texts"] as JsonArray ?? []).Select(t => (string?)t!["text"])).ToList();
        Assert.Contains("Sum of Area (m²) by Function", texts);
        Assert.Contains(texts, t => t!.StartsWith("Living  32  (80 %)", StringComparison.Ordinal));
        Assert.Single((JsonArray)layout["charts"]!);
        // Changed by its id, then removed.
        var id = chart.Id;
        f.Run("architecture.scheduleChart", new JsonObject { ["id"] = s, ["chart"] = new JsonObject { ["id"] = id, ["kind"] = "column", ["category"] = "Name", ["aggregate"] = "count", ["title"] = "Rooms" } });
        Assert.Equal("Rooms", ScheduleChart.Of(f.Model.Get(s)!).Single().Heading);
        Assert.Throws<CommandException>(() => f.Run("architecture.scheduleChart", new JsonObject { ["id"] = s, ["chart"] = new JsonObject { ["category"] = "Colour", ["aggregate"] = "count" } }));
        Assert.Throws<CommandException>(() => f.Run("architecture.scheduleChart", new JsonObject { ["id"] = s, ["chart"] = new JsonObject { ["category"] = "Name", ["aggregate"] = "sum" } }));
        f.Run("architecture.removeScheduleChart", new JsonObject { ["id"] = s, ["chartId"] = id });
        Assert.Empty(ScheduleChart.Of(f.Model.Get(s)!));
    }

    [Fact]
    public void A_joinery_schedule_charts_its_own_table()
    {
        var (f, _, _, _, _) = House();
        var s = Schedule(f);
        f.Run("architecture.scheduleChart", new JsonObject { ["id"] = s, ["chart"] = new JsonObject { ["kind"] = "bar", ["category"] = "Mark", ["value"] = "Total", ["aggregate"] = "sum" } });
        var (columns, rows) = ScheduleEntity.ChartGrid(new Q(f), f.Model.Get(s)!);
        Assert.Equal([("W1", 2.0), ("D1", 1.0)], ScheduleCharts.Values(ScheduleChart.Of(f.Model.Get(s)!).Single(), columns, rows).Select(v => (v.Label, v.Value)));
        var texts = ((JsonArray)Layout(f, s)["pens"]!).OfType<JsonObject>().SelectMany(p => (p["texts"] as JsonArray ?? []).Select(t => (string?)t!["text"])).ToList();
        Assert.Contains("Sum of Total by Mark", texts);
    }

    [Theory]
    [InlineData("12,5 m²", 12.5)]
    [InlineData("40 %", 40)]
    [InlineData("-3.25", -3.25)]
    [InlineData("1 250", 1250)]
    public void A_cell_reads_as_a_number_with_a_unit_after_it(string text, double value) => Assert.Equal(value, ScheduleCharts.Number(text));

    [Theory]
    [InlineData("—")]
    [InlineData("")]
    [InlineData("EI 30")]
    [InlineData("1.2.3")]
    public void A_cell_that_is_no_number_is_left_out(string text) => Assert.Null(ScheduleCharts.Number(text));

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
