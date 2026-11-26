using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The location-based schedule (planning.lbs): a three-storey frame planned a task a storey reads as one segment a storey,
/// the storeys in order up the axis at their elevations and heights; a task over neighbouring storeys a block, over storeys
/// apart a segment a run; tasks with no located elements listed apart; a predecessor that slips moves the segments after it;
/// zones by a field; the levels folded; the view's settings one undo step; the chart on a sheet.
/// </summary>
public class PlanningLbsTests
{
    /// <summary>Ground floor ±0,00, first +3,00, second +6,00 (3 m each); the schedule from Monday 1 March 2027, every day worked.</summary>
    static Fixture F()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new PhasesModule(), new PlanningModule());
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["name"] = "Ground floor", ["elevation"] = 0, ["height"] = 3 });
        f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3, ["height"] = 3 });
        f.Run("storey.create", new JsonObject { ["name"] = "Second floor", ["elevation"] = 6, ["height"] = 3.5 });
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01" });
        f.Run("planning.setCalendar", new JsonObject { ["days"] = new JsonArray(1, 2, 3, 4, 5, 6, 7), ["holidays"] = "none" });
        return f;
    }

    static readonly string[] Floors = ["Ground floor", "First floor", "Second floor"];

    static string Storey(Fixture f, string name) => Spatial.Storeys(f.Model).First(s => s.Name == name).Id;

    static string Column(Fixture f, string storey, double x = 0) =>
        (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = Storey(f, storey), ["x"] = x, ["y"] = 0 })!["id"]!;

    static string Wall(Fixture f, string storey, double y = 5) =>
        (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = Storey(f, storey), ["x1"] = 0, ["y1"] = y, ["x2"] = 6, ["y2"] = y })!["id"]!;

    static string Task(Fixture f, string name, int duration, string[]? classes = null, params string[] storeys)
    {
        var id = (string)f.Run("planning.addTask", new JsonObject { ["name"] = name, ["duration"] = duration })!["id"]!;
        if (classes != null)
            f.Run("planning.updateTask", new JsonObject
            {
                ["id"] = id,
                ["rule"] = new JsonObject { ["classes"] = new JsonArray(classes.Select(c => (JsonNode)c).ToArray()), ["storeys"] = new JsonArray(storeys.Select(s => (JsonNode)Storey(f, s)).ToArray()) },
            });
        return id;
    }

    static void Link(Fixture f, string from, string to) => f.Run("planning.link", new JsonObject { ["from"] = from, ["to"] = to, ["type"] = "FS" });

    static JsonObject Lbs(Fixture f, JsonObject? a = null) => (JsonObject)f.Session.Query("planning.lbs", a ?? new JsonObject())!;
    static List<JsonObject> Segments(JsonObject c) => c["segments"]!.AsArray().OfType<JsonObject>().ToList();
    static List<JsonObject> Rows(JsonObject c) => c["locations"]!.AsArray().OfType<JsonObject>().Where(l => (int)l["row"]! >= 0).OrderBy(l => (int)l["row"]!).ToList();

    /// <summary>A frame: site set-up (no elements), the structure a storey at a time, then the masonry of all three storeys in one task.</summary>
    static (Fixture F, string Setup, string[] Structure, string Masonry) Frame()
    {
        var f = F();
        foreach (var s in Floors) { Column(f, s); Wall(f, s); }
        var setup = Task(f, "Site set-up", 3);
        var structure = Floors.Select(s => Task(f, $"Structure · {s}", 5, [IfcClass.Column], s)).ToArray();
        var masonry = Task(f, "Masonry", 6, [IfcClass.Wall], Floors);
        Link(f, setup, structure[0]);
        Link(f, structure[0], structure[1]);
        Link(f, structure[1], structure[2]);
        Link(f, structure[2], masonry);
        return (f, setup, structure, masonry);
    }

    [Fact]
    public void A_three_storey_frame_gives_a_segment_a_storey_in_order_up_the_axis_a_block_over_the_storeys_and_the_unlocated_apart()
    {
        var (f, setup, structure, masonry) = Frame();
        var c = Lbs(f);
        Assert.Equal("storey", (string)c["level"]!);
        // The rows: the three storeys from the foot, at their elevations, each as high as to the storey above (the top its own height).
        var rows = Rows(c);
        Assert.Equal(Floors, rows.Select(r => (string)r["name"]!));
        Assert.Equal([0.0, 3.0, 6.0], rows.Select(r => (double)r["at"]!));
        Assert.Equal([3.0, 3.0, 3.5], rows.Select(r => (double)r["extent"]!));
        Assert.Equal([0.0, 3.0, 6.0], rows.Select(r => (double)r["elevation"]!));
        Assert.Equal(9.5, (double)c["height"]!);
        // The storeys under their building (a tree the interface folds).
        var building = c["locations"]!.AsArray().OfType<JsonObject>().Single(l => (string)l["level"]! == "building");
        Assert.Equal(rows.Select(r => (string)r["id"]!), building["children"]!.AsArray().Select(x => (string)x!));
        Assert.All(rows, r => Assert.Equal((string)building["id"]!, (string)r["parent"]!));

        var segs = Segments(c);
        // The structure: a line a storey, one after the other up the building, one group („Structure”) in one colour.
        var st = segs.Where(s => (string)s["group"]! == "Structure").ToList();
        Assert.Equal(structure, st.Select(s => (string)s["task"]!));
        Assert.All(st, s => Assert.Equal("line", (string)s["rep"]!));
        Assert.Equal([0, 1, 2], st.Select(s => (int)s["row0"]!));
        Assert.Equal([0.0, 3.0, 6.0], st.Select(s => (double)s["y0"]!));
        Assert.Equal([3.0, 6.0, 9.5], st.Select(s => (double)s["y1"]!));
        Assert.Equal(["2027-03-04", "2027-03-09", "2027-03-14"], st.Select(s => (string)s["start"]!));
        Assert.Equal(["2027-03-08", "2027-03-13", "2027-03-18"], st.Select(s => (string)s["finish"]!));
        Assert.Single(st.Select(s => (string)s["colour"]!).Distinct());
        Assert.All(st, s => Assert.True((bool)s["critical"]!));
        // The masonry: one block over the three storeys.
        var m = Assert.Single(segs, s => (string)s["task"]! == masonry);
        Assert.Equal("block", (string)m["rep"]!);
        Assert.Equal((0, 2), ((int)m["row0"]!, (int)m["row1"]!));
        Assert.Equal((0.0, 9.5), ((double)m["y0"]!, (double)m["y1"]!));
        Assert.Equal(3, (int)m["count"]!);
        Assert.Equal(("2027-03-19", "2027-03-24"), ((string)m["start"]!, (string)m["finish"]!));
        // The site set-up takes no elements: listed apart, not drawn.
        var u = Assert.Single(c["unlocated"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(setup, (string)u["task"]!);
        Assert.Equal("none", (string)u["why"]!);
        Assert.DoesNotContain(segs, s => (string)s["task"]! == setup);
        // The legend: a group a trade.
        Assert.Equal(["Structure", "Masonry"], c["groups"]!.AsArray().Select(g => (string)g!["key"]!));
    }

    [Fact]
    public void A_task_over_storeys_apart_is_a_segment_a_run_of_neighbouring_storeys()
    {
        var f = F();
        foreach (var s in Floors) Wall(f, s);
        var t = Task(f, "Plaster", 4, [IfcClass.Wall], "Ground floor", "Second floor");
        var segs = Segments(Lbs(f)).Where(s => (string)s["task"]! == t).ToList();
        Assert.Equal(2, segs.Count);
        Assert.All(segs, s => Assert.Equal("line", (string)s["rep"]!));
        Assert.Equal([0, 2], segs.Select(s => (int)s["row0"]!));
        Assert.NotEqual((string)segs[0]["id"]!, (string)segs[1]["id"]!);
        Assert.Equal([(0, 2), (4, 6)], Lapin.Planning.Lbs.Runs([0, 2, 1, 4, 6, 5]).Select(r => (r.From, r.To)));
    }

    [Fact]
    public void A_delayed_predecessor_moves_the_segments_after_it()
    {
        var (f, setup, structure, masonry) = Frame();
        var before = Segments(Lbs(f)).ToDictionary(s => (string)s["id"]!, s => (string)s["start"]!);
        f.Run("planning.updateTask", new JsonObject { ["id"] = setup, ["duration"] = 7 });
        var after = Segments(Lbs(f)).ToDictionary(s => (string)s["id"]!, s => (string)s["start"]!);
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        foreach (var (id, s) in before)
            Assert.Equal(DateOnly.Parse(s).AddDays(4), DateOnly.Parse(after[id]));
        // Started no earlier than a later day (the segment's bottom handle dragged): it and what follows move.
        f.Run("planning.updateTask", new JsonObject { ["id"] = structure[1], ["start"] = "2027-04-01" });
        var moved = Segments(Lbs(f));
        Assert.Equal("2027-04-01", (string)moved.Single(s => (string)s["task"]! == structure[1])["start"]!);
        Assert.Equal("2027-04-06", (string)moved.Single(s => (string)s["task"]! == structure[2])["start"]!);
    }

    [Fact]
    public void Group_keys_take_the_locations_names_off_whole_words_only()
    {
        string[] names = ["Ground floor", "P+1", "Roof"];
        Assert.Equal("Masonry", Lapin.Planning.Lbs.GroupKey("Masonry · Ground floor", names));
        Assert.Equal("Plaster", Lapin.Planning.Lbs.GroupKey("P+1 - Plaster", names));
        Assert.Equal("Roofing", Lapin.Planning.Lbs.GroupKey("Roofing", names));
        Assert.Equal("Roof", Lapin.Planning.Lbs.GroupKey("Roof", names));
    }

    [Fact]
    public void Zones_by_a_field_split_the_storeys_and_a_folded_storey_is_one_row_with_its_zones_segments_as_one()
    {
        var f = F();
        var a = Wall(f, "Ground floor", 0);
        var b = Wall(f, "Ground floor", 5);
        var c1 = Wall(f, "First floor", 0);
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(a), ["name"] = "Zone", ["kind"] = "text", ["value"] = "A", ["pset"] = "Site" });
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(b), ["name"] = "Zone", ["kind"] = "text", ["value"] = "B", ["pset"] = "Site" });
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(c1), ["name"] = "Zone", ["kind"] = "text", ["value"] = "A", ["pset"] = "Site" });
        var t = Task(f, "Masonry · Ground floor", 4, [IfcClass.Wall], "Ground floor");
        var t1 = Task(f, "Masonry · First floor", 4, [IfcClass.Wall], "First floor");
        var c = Lbs(f, new JsonObject { ["level"] = "zone", ["zoneField"] = "Site.Zone" });
        Assert.Equal("zone", (string)c["level"]!);
        var rows = Rows(c);
        // Ground floor A and B (each half the storey), First floor A, the Second floor whole (no zones there).
        Assert.Equal(["A", "B", "A", "Second floor"], rows.Select(r => (string)r["name"]!));
        Assert.Equal([0.0, 1.5, 3.0, 6.0], rows.Select(r => (double)r["at"]!));
        var gf = Segments(c).Single(s => (string)s["task"]! == t);
        Assert.Equal("block", (string)gf["rep"]!);
        Assert.Equal((0, 1), ((int)gf["row0"]!, (int)gf["row1"]!));
        Assert.Equal(["Masonry"], c["groups"]!.AsArray().Select(g => (string)g!["key"]!));
        // The ground floor folded: one row for its zones.
        var folded = Lbs(f, new JsonObject { ["level"] = "zone", ["zoneField"] = "Site.Zone", ["collapsed"] = new JsonArray(Storey(f, "Ground floor")) });
        var fr = Rows(folded);
        Assert.Equal(["Ground floor", "A", "Second floor"], fr.Select(r => (string)r["name"]!));
        Assert.True((bool)fr[0]["folded"]!);
        var one = Segments(folded).Single(s => (string)s["task"]! == t);
        Assert.Equal("line", (string)one["rep"]!);
        Assert.Equal((0.0, 3.0), ((double)one["y0"]!, (double)one["y1"]!));
        Assert.Single(Segments(folded), s => (string)s["task"]! == t1);
    }

    [Fact]
    public void The_views_settings_are_one_undo_step_each_and_the_chart_goes_on_a_sheet_and_prints()
    {
        var (f, _, structure, _) = Frame();
        var id = (string)f.Run("planning.lbsView", new JsonObject { ["level"] = "storey" })!["id"]!;
        Assert.Equal(Lapin.Planning.Lbs.Class, f.Model.Get(id)!.Class);
        var seg = Segments(Lbs(f)).First(s => (string)s["task"]! == structure[0]);
        f.Run("planning.lbsView", new JsonObject { ["id"] = id, ["label"] = new JsonObject { ["id"] = (string)seg["id"]!, ["dx"] = 12, ["dy"] = -4, ["angle"] = 30 } });
        f.Run("planning.lbsView", new JsonObject { ["id"] = id, ["note"] = new JsonObject { ["date"] = "2027-03-10", ["location"] = (string)seg["from"]!, ["text"] = "Crane on site" } });
        var settings = (JsonObject)Lbs(f)["settings"]!;
        Assert.Equal(30, (double)settings["labels"]![(string)seg["id"]!]!["angle"]!);
        Assert.Equal("Crane on site", (string)settings["notes"]![0]!["text"]!);
        f.Session.Undo();
        Assert.Null(((JsonObject)Lbs(f)["settings"]!)["notes"]);
        Assert.NotNull(((JsonObject)Lbs(f)["settings"]!)["labels"]);
        Assert.Throws<CommandException>(() => f.Run("planning.lbsView", new JsonObject { ["id"] = id, ["level"] = "galaxy" }));

        // The chart on a sheet: the storeys, the trades, the dates; a PDF; one undo step.
        var r = (JsonObject)f.Run("planning.lbsSheet", new JsonObject())!;
        var sheet = f.Model.Get((string)r["sheetId"]!)!;
        var pens = Sheets.Compose(f.Session.Reader, sheet).Pens;
        var texts = pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        Assert.Contains("Ground floor", texts);
        Assert.Contains("Second floor", texts);
        Assert.Contains("Structure", texts);
        Assert.Contains("Masonry", texts);
        Assert.Contains("+3.00", texts);
        Assert.Contains(pens, p => p.Layer == "LBS-BLOCK" && p.Loops.Count >= 1);
        Assert.Contains(pens, p => p.Layer == "LBS-TASK" && p.Segments.Count >= 3);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(Sheets.Pdf(f.Session.Reader, [sheet])[..4]));
        f.Session.Undo();
        Assert.Null(f.Model.Get(sheet.Id));
    }

    [Fact]
    public void The_task_across_all_its_locations_changes_at_once_in_one_undo_step()
    {
        var (f, _, structure, _) = Frame();
        f.Run("planning.updateTasks", new JsonObject { ["ids"] = new JsonArray(structure.Select(x => (JsonNode)x).ToArray()), ["duration"] = 8 });
        var segs = Segments(Lbs(f)).Where(s => (string)s["group"]! == "Structure").ToList();
        Assert.All(segs, s => Assert.Equal(8, (int)s["duration"]!));
        f.Session.Undo();
        Assert.All(Segments(Lbs(f)).Where(s => (string)s["group"]! == "Structure"), s => Assert.Equal(5, (int)s["duration"]!));
        Assert.Throws<CommandException>(() => f.Run("planning.updateTasks", new JsonObject { ["ids"] = new JsonArray() }));
    }
}
