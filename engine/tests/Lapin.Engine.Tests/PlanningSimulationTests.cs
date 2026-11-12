using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;
using Lapin.SiteWorks;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The 4D simulation, stage 4 of docs/plan-4d5d.md: each element's state on a day from the tasks that take it — not started,
/// under way, built — a layered wall's core and plaster apart, a demolition red while it runs then gone, a formwork standing
/// only between its hosts' casting and its striking; a view at a date drawing the same (State at date), its layers not yet
/// laid left out and the phases stepping aside; the Gantt on sheets, paged; the stages sheet, drawn at its dates or from the
/// 3D's pictures; the simulation's colours on the schedule, an old schedule reading the defaults.
/// </summary>
public class PlanningSimulationTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new PhasesModule(), new SiteWorksModule(), new PlanningModule());

    static JsonArray Pts(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());

    static string Task(Fixture f, string name, int days, JsonObject? extra = null)
    {
        var id = (string)f.Run("planning.addTask", new JsonObject { ["name"] = name, ["duration"] = days })!["id"]!;
        if (extra != null) { extra["id"] = id; f.Run("planning.updateTask", extra); }
        return id;
    }

    static void Link(Fixture f, string from, string to) => f.Run("planning.link", new JsonObject { ["from"] = from, ["to"] = to });

    static void Assign(Fixture f, string task, params string[] ids) =>
        f.Run("planning.assign", new JsonObject { ["task"] = task, ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()) });

    static string Slab(Fixture f, double x) =>
        (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((x, 0), (x + 4, 0), (x + 4, 4), (x, 4)) })!["id"]!;

    static JsonObject Classes(params string[] c) => new() { ["classes"] = new JsonArray(c.Select(x => (JsonNode)x).ToArray()) };

    /// <summary>
    /// Plastered brick walls and a slab on a schedule from Monday 1 March 2027: the old slab demolished 1–5 March, the walls'
    /// brick 8–12, their plaster 15–19, the new slab cast 15–19 with its formwork; one slab no task takes.
    /// </summary>
    static (Fixture F, Dictionary<string, string> Ids) Sample()
    {
        var f = F();
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01" });
        var type = f.Type("Plastered brick", ("Lime plaster", 0.015, false), ("Brick", 0.25, true), ("Gypsum plaster", 0.015, false));
        var ids = new Dictionary<string, string>
        {
            ["w1"] = f.Wall(0, 0, 6, 0, type),
            ["w2"] = f.Wall(6, 0, 6, 5, type),
            ["slab"] = Slab(f, 10),
            ["old"] = Slab(f, 20),
            ["loose"] = Slab(f, 30),
        };
        ids["demolish"] = Task(f, "Demolish the old slab", 5, new JsonObject { ["kind"] = "DEMOLITION" });
        Assign(f, ids["demolish"], ids["old"]);
        ids["masonry"] = Task(f, "Masonry", 5, new JsonObject { ["rule"] = Classes("IfcWall"), ["part"] = "core" });
        ids["plaster"] = Task(f, "Plaster", 5, new JsonObject { ["rule"] = Classes("IfcWall"), ["part"] = "finishes" });
        ids["cast"] = Task(f, "Slab", 5);
        Assign(f, ids["cast"], ids["slab"]);
        Link(f, ids["demolish"], ids["masonry"]);
        Link(f, ids["masonry"], ids["plaster"]);
        Link(f, ids["masonry"], ids["cast"]);
        ids["formwork"] = ((JsonArray)f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(ids["slab"]) })!["ids"]!)[0]!.ToString();
        return (f, ids);
    }

    static JsonObject StateAt(Fixture f, string date) => (JsonObject)f.Session.Query("planning.stateAt", new JsonObject { ["date"] = date })!;
    static string State(JsonObject s, string id) => (string?)s["elements"]![id]?["state"] ?? Status.Unlinked;
    static List<string> Layers(JsonObject s, string id) => s["elements"]![id]!["layers"]!.AsArray().Select(x => (string)x!).ToList();

    [Fact]
    public void An_element_is_not_started_under_way_then_built_its_layers_apart_a_demolition_red_then_gone_a_formwork_only_while_it_stands()
    {
        var (f, ids) = Sample();
        // 3 March: the old slab coming down; the walls and the new slab not started; the formwork not up.
        var s = StateAt(f, "2027-03-03");
        Assert.Equal(Status.Demolishing, State(s, ids["old"]));
        Assert.Equal(Status.Planned, State(s, ids["w1"]));
        Assert.Equal(Status.Planned, State(s, ids["slab"]));
        Assert.Equal(Status.Gone, State(s, ids["formwork"]));
        Assert.Equal(Status.Unlinked, State(s, ids["loose"]));
        Assert.Equal([ids["demolish"]], s["tasks"]!.AsArray().Select(x => (string)x!));
        // Before the works: the old slab stands as it was.
        Assert.Equal(Status.Existing, State(StateAt(f, "2027-02-26"), ids["old"]));
        // 10 March: the brick laid, the plaster not; the old slab gone.
        s = StateAt(f, "2027-03-10");
        Assert.Equal(Status.Gone, State(s, ids["old"]));
        Assert.Equal(Status.Active, State(s, ids["w1"]));
        Assert.Equal([Status.Planned, Status.Active, Status.Planned], Layers(s, ids["w1"]));
        // 16 March: the plaster under way over the brick built; the slab cast in its formwork.
        s = StateAt(f, "2027-03-16");
        Assert.Equal([Status.Active, Status.Done, Status.Active], Layers(s, ids["w2"]));
        Assert.Equal(Status.Active, State(s, ids["slab"]));
        Assert.Equal(Status.Temporary, State(s, ids["formwork"]));
        // 22 March: everything built; the formwork stands until struck, then goes.
        s = StateAt(f, "2027-03-22");
        Assert.Equal(Status.Done, State(s, ids["w1"]));
        Assert.Equal(Status.Done, State(s, ids["slab"]));
        Assert.Equal(Status.Temporary, State(s, ids["formwork"]));
        var timeline = (JsonObject)f.Session.Query("planning.timeline", new JsonObject())!;
        var fw = timeline["elements"]!.AsArray().OfType<JsonObject>().First(e => (string)e["id"]! == ids["formwork"]);
        Assert.Equal("formwork", (string)fw["temporary"]!);
        Assert.Equal("2027-03-15", (string)fw["present"]![0]!);
        var struck = DateOnly.Parse((string)fw["present"]![1]!).AddDays(1).ToString("yyyy-MM-dd");
        Assert.Equal(Status.Gone, State(StateAt(f, struck), ids["formwork"]));
        // The timeline gives the layers' days by their keys, the demolition's, the tasks and the stages' dates.
        var w1 = timeline["elements"]!.AsArray().OfType<JsonObject>().First(e => (string)e["id"]! == ids["w1"]);
        Assert.Equal(["2027-03-15", "2027-03-19"], w1["layers"]!["layer:0"]!.AsArray().Select(x => (string)x!));
        Assert.Equal(["2027-03-08", "2027-03-12"], w1["layers"]!["layer:1"]!.AsArray().Select(x => (string)x!));
        Assert.Equal(["2027-03-08", "2027-03-19"], w1["build"]!.AsArray().Select(x => (string)x!));
        var old = timeline["elements"]!.AsArray().OfType<JsonObject>().First(e => (string)e["id"]! == ids["old"]);
        Assert.Equal(["2027-03-01", "2027-03-05"], old["demolish"]!.AsArray().Select(x => (string)x!));
        Assert.Equal(4, timeline["tasks"]!.AsArray().Count);
        Assert.Equal(["2027-03-05", "2027-03-12", "2027-03-19"], timeline["stages"]!["chapters"]!.AsArray().Select(x => (string)x!));
        // An old schedule has no simulation settings: the defaults.
        Assert.Equal("hide", (string)timeline["look"]!["plannedMode"]!);
        Assert.Equal("#e8a33d", (string)timeline["look"]!["active"]!);
    }

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;

    static int Cut(JsonObject d) => d["pens"]!.AsArray().OfType<JsonObject>().Where(p => (string)p["layer"]! == "CUT").Sum(p => p["segments"]!.AsArray().Count / 4);

    static bool Stroked(JsonObject d, string colour) => d["pens"]!.AsArray().OfType<JsonObject>().Any(p => (string?)p["stroke"] == colour || (string?)p["fill"] == colour);

    [Fact]
    public void A_view_at_a_date_draws_what_the_simulation_shows_and_the_phases_step_aside()
    {
        var (f, ids) = Sample();
        f.EveryLayerInPlans();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var all = Cut(Drawing(f, plan));
        // 3 March: the walls not started are not drawn.
        f.Run("planning.viewDate", new JsonObject { ["id"] = plan, ["date"] = "2027-03-03" });
        Assert.Equal("2027-03-03", f.Model.Get(plan)!.Str("stateDate"));
        Assert.Equal(0, Cut(Drawing(f, plan)));
        // 10 March: the brick under way in its colour, the plaster not laid yet (fewer cut lines than when all is built).
        f.Run("planning.viewDate", new JsonObject { ["id"] = plan, ["date"] = "2027-03-10" });
        var brick = Drawing(f, plan);
        Assert.True(Stroked(brick, "#e8a33d"));
        var core = Cut(brick);
        Assert.InRange(core, 1, all - 1);
        f.Run("planning.viewDate", new JsonObject { ["id"] = plan, ["date"] = "2027-03-25" });
        var built = Drawing(f, plan);
        Assert.Equal(all, Cut(built));
        Assert.False(Stroked(built, "#e8a33d"));
        // Not started as ghosts, in the planned colour; the colours are the project's.
        f.Run("planning.setSimulation", new JsonObject { ["plannedMode"] = "ghost", ["planned"] = "#123456", ["active"] = "#00ff00" });
        f.Run("planning.viewDate", new JsonObject { ["id"] = plan, ["date"] = "2027-03-03" });
        Assert.True(Stroked(Drawing(f, plan), "#123456"));
        f.Run("planning.viewDate", new JsonObject { ["id"] = plan, ["date"] = "2027-03-10" });
        Assert.True(Stroked(Drawing(f, plan), "#00ff00"));
        Assert.Throws<CommandException>(() => f.Run("planning.setSimulation", new JsonObject { ["active"] = "orange" }));
        // A view on the existing phase still draws the walls built by the date: the phases step aside.
        f.Run("phases.viewPhase", new JsonObject { ["id"] = plan, ["phase"] = "existing" });
        Assert.InRange(Cut(Drawing(f, plan)), 1, all);
        // Back to the model as it is: one undo step each.
        f.Run("planning.viewDate", new JsonObject { ["id"] = plan, ["date"] = null });
        Assert.Null(f.Model.Get(plan)!.Str("stateDate"));
        f.Session.Undo();
        Assert.Equal("2027-03-10", f.Model.Get(plan)!.Str("stateDate"));
        Assert.Throws<CommandException>(() => f.Run("planning.viewDate", new JsonObject { ["id"] = ids["w1"], ["date"] = "2027-03-10" }));
    }

    static List<string> Texts(Fixture f, string sheet) => Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();

    [Fact]
    public void The_Gantt_goes_on_sheets_as_vectors_paged_under_its_header_and_prints()
    {
        var (f, ids) = Sample();
        var made = ((JsonArray)((JsonObject)f.Run("planning.ganttSheet", new JsonObject())!)["ids"]!).Select(x => (string)x!).ToList();
        var sheet = Assert.Single(made);
        var texts = Texts(f, sheet);
        Assert.Contains("Task", texts);
        Assert.Contains(texts, t => t.EndsWith("Masonry", StringComparison.Ordinal));
        Assert.Contains("08.03.2027", texts);
        Assert.Contains("19.03.2027", texts);
        // The bars: the critical ones in red (the chain demolition → masonry → plaster is critical).
        var pens = Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!).Pens;
        Assert.Contains(pens, p => p.Fill == "#d64545" && p.Loops.Count >= 3);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(Sheets.Pdf(f.Session.Reader, [f.Model.Get(sheet)!])[..4]));
        // A long schedule on A3: pages, each a sheet, the header repeated.
        for (var i = 0; i < 60; i++) Task(f, $"Task {i + 1}", 2);
        made = ((JsonArray)((JsonObject)f.Run("planning.ganttSheet", new JsonObject { ["paper"] = "A3" })!)["ids"]!).Select(x => (string)x!).ToList();
        Assert.True(made.Count >= 2);
        Assert.All(made, s => Assert.Contains("Task", Texts(f, s)));
        Assert.Equal($"Work schedule (2/{made.Count})", f.Model.Get(made[1])!.Name);
        // One undo step.
        f.Session.Undo();
        Assert.All(made, s => Assert.Null(f.Model.Get(s)));
    }

    [Fact]
    public void A_stages_sheet_draws_a_view_at_each_date_or_the_3Ds_pictures_each_with_its_date_and_the_tasks_under_way()
    {
        var (f, ids) = Sample();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var r = (JsonObject)f.Run("planning.stagesSheet", new JsonObject { ["viewId"] = plan, ["dates"] = new JsonArray("2027-03-03", "2027-03-10", "2027-03-16") })!;
        var texts = Texts(f, (string)r["sheetId"]!);
        Assert.Contains("1. 03.03.2027", texts);
        Assert.Contains("Demolish the old slab", texts);
        Assert.Contains("2. 10.03.2027", texts);
        Assert.Contains("Masonry", texts);
        Assert.Contains("Plaster, Slab", texts);
        // The frames drawn at their dates: the brick under way in its colour in the second.
        var pens = Sheets.Compose(f.Session.Reader, f.Model.Get((string)r["sheetId"]!)!).Pens;
        Assert.Contains(pens, p => p.Stroke == "#e8a33d" || p.Fill == "#e8a33d");
        // The view itself stays as it is.
        Assert.Null(f.Model.Get(plan)!.Str("stateDate"));
        // No dates: the chapters' finishes.
        r = (JsonObject)f.Run("planning.stagesSheet", new JsonObject { ["viewId"] = plan })!;
        Assert.Equal(3, StagesSheet.Frames(f.Model.Get((string)r["id"]!)!).Count);
        // The 3D's pictures: kept as images, laid by their corners, sized by their own proportions.
        var png = Convert.ToBase64String(TinyPng(40, 20));
        r = (JsonObject)f.Run("planning.stagesSheet", new JsonObject
        {
            ["snapshots"] = new JsonArray(new JsonObject { ["date"] = "2027-03-10", ["data"] = png }, new JsonObject { ["date"] = "2027-03-22", ["data"] = png }),
            ["paper"] = "A3",
        })!;
        var frames = StagesSheet.Frames(f.Model.Get((string)r["id"]!)!);
        Assert.Equal(2, frames.Count);
        Assert.All(frames, x => Assert.Equal(Images.Class, f.Model.Get(x.Image!)!.Class));
        var images = Sheets.Compose(f.Session.Reader, f.Model.Get((string)r["sheetId"]!)!).Pens.SelectMany(p => p.Images).ToList();
        Assert.Equal(2, images.Count);
        var c = images[0].Corners;
        Assert.Equal(2, (c[1].X - c[0].X) / (c[3].Y - c[0].Y), 3);
        Assert.Equal((40.0, 20.0), StagesSheet.PictureSize(TinyPng(40, 20)));
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(Sheets.Pdf(f.Session.Reader, [f.Model.Get((string)r["sheetId"]!)!])[..4]));
        Assert.Throws<CommandException>(() => f.Run("planning.stagesSheet", new JsonObject { ["viewId"] = "3d" }));
        Assert.Throws<CommandException>(() => f.Run("planning.stagesSheet", new JsonObject { ["snapshots"] = new JsonArray(new JsonObject { ["date"] = "2027-03-10", ["data"] = "bm90IGFuIGltYWdl" }) }));
    }

    /// <summary>A grey PNG of a size (no compression needed by the tests: its header is what they read).</summary>
    static byte[] TinyPng(int w, int h)
    {
        var raw = new List<byte>();
        for (var y = 0; y < h; y++) { raw.Add(0); for (var x = 0; x < w; x++) raw.AddRange([128, 128, 128]); }
        using var z = new MemoryStream();
        using (var d = new System.IO.Compression.ZLibStream(z, System.IO.Compression.CompressionLevel.Fastest, true)) d.Write(raw.ToArray());
        static byte[] Chunk(string type, byte[] data)
        {
            var o = new List<byte>();
            o.AddRange(BitConverter.GetBytes(data.Length).Reverse());
            var td = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
            o.AddRange(td);
            o.AddRange(BitConverter.GetBytes(Crc(td)).Reverse());
            return o.ToArray();
        }
        static uint Crc(byte[] b)
        {
            var c = 0xFFFFFFFFu;
            foreach (var x in b) { c ^= x; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; }
            return c ^ 0xFFFFFFFFu;
        }
        var ihdr = new byte[13];
        BitConverter.GetBytes(w).Reverse().ToArray().CopyTo(ihdr, 0);
        BitConverter.GetBytes(h).Reverse().ToArray().CopyTo(ihdr, 4);
        ihdr[8] = 8; ihdr[9] = 2;
        return [137, 80, 78, 71, 13, 10, 26, 10, .. Chunk("IHDR", ihdr), .. Chunk("IDAT", z.ToArray()), .. Chunk("IEND", [])];
    }
}
