using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Cost;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Cost over time, stage 7 of docs/plan-4d5d.md: each task's cost from the estimate (a layer's article in the task that
/// takes that layer), the Gantt's cost column and cost to date, the S-curve a working day over Romania's holidays, the
/// month table adding up to the estimate, the curve of the work done from progress and the baseline's, two versions
/// compared by cost, the cost items' tasks through IFC, and the phases (what exists is not new work; a demolition is
/// priced by its own article).
/// </summary>
public sealed class CostTimeTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-cost-time-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static Fixture F() => new(new ArchitectureModule(), new PhasesModule(), new PlanningModule(), new CostModule());

    const string Csv =
        "Cod articol;Denumire;U.M.;Material;Manoperă;Utilaj;Transport\n" +
        "ZID01;Zidărie din cărămidă 25 cm;mc;380,00;85,00;6,00;14,00\n" +
        "TEN01;Tencuială de var;mp;18;22;1;1\n" +
        "DEM01;Demolare zidărie;mc;0;120;30;10\n";

    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());

    /// <summary>
    /// The house of CostTests: a wall 10 m × 3 m, lime plaster 20 mm both sides of a brick core 250 mm. The core is ZID01,
    /// 7.5 m³ × 485 = 3637.50; the plaster TEN01, 60 m² × 42 = 2520.00; 6157.50 direct. The schedule starts on Monday
    /// 26.04.2027: Masonry (the walls' core, 5 days), then Plaster (their finishes, 10 days), under a summary Walls.
    /// </summary>
    static (Fixture F, string Wall, string Type, Dictionary<string, string> Tasks) House(string start = "2027-04-26")
    {
        var f = F();
        f.Run("cost.importPrices", new JsonObject { ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Csv)), ["name"] = "Prețuri.csv" });
        var type = f.Type("Brick 250 plastered", ("Lime plaster", 0.02, false), ("Brick", 0.25, true), ("Lime plaster", 0.02, false));
        var wall = f.Wall(0, 0, 10, 0, type);
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(new JsonObject { ["code"] = "ZID01", ["part"] = "core" }) });
        f.Run("cost.setRule", new JsonObject { ["material"] = f.Model.OfClass(IfcClass.Material).First(m => m.Name == "Lime plaster").Id, ["code"] = "TEN01" });
        f.Run("cost.createEstimate", new JsonObject { ["name"] = "Deviz casă" });
        f.Run("planning.createSchedule", new JsonObject { ["start"] = start, ["name"] = "Casa" });
        var t = new Dictionary<string, string>();
        string Task(string name, int days, JsonObject? extra = null)
        {
            var id = (string)f.Run("planning.addTask", new JsonObject { ["name"] = name, ["duration"] = days })!["id"]!;
            if (extra != null) { extra["id"] = id; f.Run("planning.updateTask", extra); }
            return id;
        }
        t["walls"] = Task("Walls", 1);
        t["masonry"] = Task("Masonry", 5, new JsonObject { ["rule"] = new JsonObject { ["classes"] = Ids("IfcWall") }, ["part"] = "core" });
        t["plaster"] = Task("Plaster", 10, new JsonObject { ["rule"] = new JsonObject { ["classes"] = Ids("IfcWall") }, ["part"] = "finishes" });
        f.Run("planning.indent", new JsonObject { ["ids"] = Ids(t["masonry"], t["plaster"]) });
        f.Run("planning.link", new JsonObject { ["from"] = t["masonry"], ["to"] = t["plaster"] });
        return (f, wall, type, t);
    }

    static JsonObject Schedule(Fixture f) => (JsonObject)f.Session.Query("planning.schedule", new JsonObject())!;
    static JsonObject Row(JsonObject s, string id) => (JsonObject)s["tasks"]!.AsArray().First(t => (string)t!["id"]! == id)!;
    static JsonObject OverTime(Fixture f) => (JsonObject)f.Session.Query("cost.overTime", new JsonObject())!;
    static double N(JsonNode? n) => (double)n!;

    /// <summary>The cumulative curve's value at the end of a day.</summary>
    static double At(JsonObject o, string series, string day) =>
        N(o[series]![DateOnly.Parse(day).DayNumber - DateOnly.Parse((string)o["from"]!).DayNumber]);

    [Fact]
    public void Each_task_costs_what_the_estimate_prices_of_the_layers_it_builds_and_a_summary_its_tasks()
    {
        var (f, _, _, t) = House();
        var s = Schedule(f);
        Assert.Equal(3637.5, N(Row(s, t["masonry"])["cost"]));          // the core: 7.5 m³ × 485
        Assert.Equal(2520, N(Row(s, t["plaster"])["cost"]));            // both plasters: 60 m² × 42
        Assert.Equal(6157.5, N(Row(s, t["walls"])["cost"]));
        Assert.Equal(0, N(Row(s, t["masonry"])["costDone"]));
        var c = (JsonObject)s["cost"]!;
        Assert.Equal("RON", (string)c["currency"]!);
        Assert.Equal(6157.5, N(c["budget"]));
        Assert.Equal(6157.5, N(c["scheduled"]));
        Assert.Equal(0, N(c["toDate"]));
        // No task takes the finishes: their cost is not scheduled.
        f.Run("planning.deleteTasks", new JsonObject { ["ids"] = Ids(t["plaster"]) });
        var o = OverTime(f);
        Assert.Equal(2520, N(o["unscheduled"]));
        Assert.Equal(3637.5, N(o["scheduled"]));
        // An article on the whole wall, built by two tasks (core and finishes): shared alike.
        var (g, _, type, u) = House();
        g.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(new JsonObject { ["code"] = "ZID01" }) });
        var sg = Schedule(g);
        var whole = (7.5 + 1.2) * 485;                                   // the wall's volume, its plaster with it: 4219.50
        Assert.Equal(whole / 2, N(Row(sg, u["masonry"])["cost"]), 2);
        Assert.Equal(whole / 2, N(Row(sg, u["plaster"])["cost"]), 2);
    }

    [Fact]
    public void The_S_curve_runs_over_working_days_and_stands_still_over_Good_Friday_the_weekend_and_Easter_Monday()
    {
        var (f, _, _, _) = House();
        var o = OverTime(f);
        Assert.Equal("2027-04-26", (string)o["from"]!);
        // Masonry: 26–29.04 and 04.05 (30.04 Good Friday, 1–2.05 the weekend, 3.05 Easter Monday), 727.50 a day.
        Assert.Equal(727.5, At(o, "planned", "2027-04-26"));
        Assert.Equal(2910, At(o, "planned", "2027-04-29"));
        Assert.Equal(2910, At(o, "planned", "2027-04-30"));
        Assert.Equal(2910, At(o, "planned", "2027-05-03"));
        Assert.Equal(3637.5, At(o, "planned", "2027-05-04"));
        // Plaster: ten working days from 05.05, 252.00 a day, to 18.05.
        Assert.Equal(3637.5 + 252, At(o, "planned", "2027-05-05"));
        Assert.Equal(6157.5, At(o, "planned", "2027-05-18"));
        Assert.Equal("2027-05-18", (string)o["to"]!);
        var months = o["months"]!.AsArray();
        Assert.Equal(["2027-04", "2027-05"], months.Select(m => (string)m!["month"]!));
        Assert.Equal(2910, N(months[0]!["planned"]));
        Assert.Equal(3247.5, N(months[1]!["planned"]));
        Assert.Equal(6157.5, N(months[1]!["plannedCum"]));
        Assert.Null(o["baseline"]);
    }

    [Fact]
    public void The_month_table_adds_up_to_the_estimate_by_chapter_without_VAT_and_with_it()
    {
        var (f, _, _, _) = House();
        // A line of the user's in no task: not scheduled, still in the totals.
        f.Run("cost.addLine", new JsonObject { ["name"] = "Organizare de șantier", ["qty"] = 1, ["m"] = 333.33 });
        var est = (JsonObject)f.Session.Query("cost.estimate", new JsonObject())!;
        var totals = (JsonObject)est["totals"]!;
        var form = CostForms.Make(f.Session.Reader, Costing.Compute(f.Session.Reader, Costing.EstimateOf(f.Model)!), "monthly", raw: true);
        Assert.Equal(["Chapter", "04.2027", "05.2027", "Not scheduled", "Total"], form.Columns);
        double Cell(string label, int col) => double.Parse(form.Rows.First(r => r.Cells[0] == label).Cells[col], System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(2910, Cell("Walls", 1));
        Assert.Equal(727.5, Cell("Walls", 2));
        Assert.Equal(3637.5, Cell("Walls", 4));
        Assert.Equal(2520, Cell("Finishes and insulation", 2));
        Assert.Equal(333.33, Cell("Other", 3));
        foreach (var (label, total) in new[] { ("Direct costs", N(totals["direct"])), ("Total without VAT", N(totals["net"])), ("Total with VAT", N(totals["total"])) })
        {
            Assert.Equal(total, Cell(label, 4));
            Assert.Equal(total, Math.Round(Cell(label, 1) + Cell(label, 2) + Cell(label, 3), 2));
        }
        Assert.Equal(6157.5 + 333.33, Cell("Direct costs", 4));
        Assert.Equal(6157.5, Cell("Cumulative direct costs", 2));
        // A schedule placed on sheets like any other form; without a schedule, a line saying so.
        Assert.Contains(f.Session.Catalog.All<IScheduleSource>(), x => x.Kind == "estimate-monthly");
        var g = new Fixture(new ArchitectureModule(), new CostModule());
        g.Run("cost.createEstimate", new JsonObject());
        Assert.StartsWith("No work schedule", CostForms.Make(g.Session.Reader, Costing.Compute(g.Session.Reader, Costing.EstimateOf(g.Model)!), "monthly").Rows[0].Cells[0]);
        Assert.NotNull(OverTimeWhy(g));
    }

    static string? OverTimeWhy(Fixture f) => (string?)((JsonObject)f.Session.Query("cost.overTime", new JsonObject())!)["why"];

    [Fact]
    public void The_work_done_is_its_tasks_cost_times_their_progress_spread_to_the_status_date_and_the_baseline_has_its_own_curve()
    {
        var (f, _, _, t) = House();
        f.Run("planning.saveBaseline", new JsonObject());
        f.Run("planning.updateSchedule", new JsonObject { ["dataDate"] = "2027-04-28" });
        f.Run("planning.setProgress", new JsonObject { ["id"] = t["masonry"], ["actualStart"] = "2027-04-26", ["complete"] = 60 });
        var s = Schedule(f);
        Assert.Equal(2182.5, N(Row(s, t["masonry"])["costDone"]));     // 60 % of 3637.50
        Assert.Equal(2182.5, N(Row(s, t["walls"])["costDone"]));
        Assert.Equal(2182.5, N(s["cost"]!["toDate"]));
        var o = OverTime(f);
        Assert.Equal(2182.5, N(o["toDate"]));
        Assert.Equal("2027-04-28", (string)o["doneThrough"]!);
        // From the actual start to the status date: three working days of 727.50.
        Assert.Equal(727.5, At(o, "done", "2027-04-26"));
        Assert.Equal(2182.5, At(o, "done", "2027-04-28"));
        Assert.Equal(3, o["done"]!.AsArray().Count);
        // The baseline, saved before: the same curve as planned then.
        Assert.Equal(2910, At(o, "baseline", "2027-04-29"));
        Assert.Equal(6157.5, At(o, "baseline", "2027-05-18"));
        Assert.Equal(2182.5, N(o["months"]![0]!["done"]));
    }

    [Fact]
    public void Versions_compared_by_cost_say_the_total_the_chapters_and_the_lines_added_removed_and_changed()
    {
        var (f, wall, type, _) = House();
        var files = PackageFiles.Create();
        files.Save(f.Session, Path.Combine(_dir, "p.lapin"));
        files.SaveVersion(f.Session, "first", null, asBytes: false);
        // The wall longer by half (15 m: ZID01 11.25 m³, TEN01 90 m²), the plaster's rule gone, a line of the user's added.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["x2"] = 15.0 });
        f.Run("cost.removeRule", new JsonObject { ["id"] = f.Model.OfClass(ArticleRule.Class).Single().Id });
        f.Run("cost.addLine", new JsonObject { ["name"] = "Provizion", ["qty"] = 1, ["m"] = 100 });
        var diff = files.CompareVersions(f.Session, 1, "current");
        var section = (JsonObject)diff["sections"]!.AsArray().Single(x => (string)x!["key"]! == "estimate")!;
        Assert.Equal("Estimate", (string)section["title"]!);
        var facts = section["facts"]!.AsArray().Select(x => ((string)x!["label"]!, (string?)x["from"], (string?)x["to"])).ToList();
        Assert.Contains(facts, x => x.Item1 == "Direct costs" && x.Item2 == "6 157.50" && x.Item3 == "5 556.25");   // 11.25 × 485 + 100
        Assert.Contains(facts, x => x.Item1 == "Difference" && x.Item3!.StartsWith("−"));
        var rows = section["rows"]!.AsArray().Select(x => ((string)x!["name"]!, (string)x["what"]!)).ToList();
        Assert.Contains(("Walls", "chapter"), rows);
        Assert.Contains(rows, r => r.Item1.StartsWith("ZID01") && r.Item2 == "cost");
        Assert.Contains(rows, r => r.Item1.StartsWith("TEN01") && r.Item2 == "removed");
        Assert.Contains(rows, r => r.Item1 == "Provizion" && r.Item2 == "added");
        // The same project: nothing about the estimate.
        files.SaveVersion(f.Session, "second", null, asBytes: false);
        Assert.DoesNotContain(files.CompareVersions(f.Session, 2, "current")["sections"]!.AsArray(), x => (string)x!["key"]! == "estimate");
        _ = type;
    }

    [Fact]
    public void The_cost_items_name_their_tasks_in_IFC_and_read_back_the_curve_is_the_same()
    {
        var (f, _, _, t) = House();
        var before = OverTime(f);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "casa.ifc");
        var step = StepFile.Read(ms.ToArray());
        var taskRefs = step.OfType("IFCTASK").ToDictionary(x => x.Id, x => (string?)x[0]);
        var toTasks = step.OfType("IFCRELASSIGNSTOCONTROL").Where(r => r[4] is List<object?> l && l.OfType<StepRef>().Any(x => taskRefs.ContainsKey(x.Id)) && step.Get(new StepRef(((StepRef)r[6]!).Id))!.Type == "IFCCOSTITEM").ToList();
        Assert.Equal(2, toTasks.Count);                                  // ZID01 in Masonry, TEN01 in Plaster
        Assert.All(toTasks, r => Assert.Equal("PROCESS", (r[5] as StepEnum)?.Value));
        var zid = step.OfType("IFCCOSTITEM").Single(i => (string?)i[5] == "ZID01").Id;
        var zidTasks = toTasks.Single(r => ((StepRef)r[6]!).Id == zid)[4] as List<object?>;
        Assert.Equal([t["masonry"]], zidTasks!.OfType<StepRef>().Select(x => taskRefs[x.Id]));
        // Into a new project: the schedule's tasks and the estimate's lines (its own now) with their tasks; the same curve.
        var g = F();
        IfcImport.Run(g.Session, ms.ToArray(), "casa.ifc", new IfcImportOptions(), out _);
        var est = (JsonObject)g.Session.Query("cost.estimate", new JsonObject())!;
        var line = est["chapters"]!.AsArray().SelectMany(c => c!["lines"]!.AsArray()).First(l => (string)l!["code"]! == "ZID01")!;
        Assert.Equal([t["masonry"]], line["tasks"]!.AsArray().Select(x => (string)x!));
        var after = OverTime(g);
        Assert.Equal(before["planned"]!.ToJsonString(), after["planned"]!.ToJsonString());
        Assert.Equal(N(before["scheduled"]), N(after["scheduled"]));
    }

    [Fact]
    public void Existing_elements_are_not_priced_and_a_demolished_one_only_by_its_demolition_article()
    {
        var (f, wall, type, _) = House();
        var old = f.Type("Old brick", ("Brick", 0.25, true));
        var gone = f.Wall(0, 5, 4, 5, old);
        var stays = f.Wall(0, 8, 4, 8, old);
        f.Run("cost.setArticles", new JsonObject { ["type"] = old, ["articles"] = new JsonArray(new JsonObject { ["code"] = "ZID01" }, new JsonObject { ["code"] = "DEM01", ["part"] = "demolition" }) });
        f.Run("phases.assign", new JsonObject { ["ids"] = Ids(gone, stays), ["created"] = "existing" });
        f.Run("phases.assign", new JsonObject { ["ids"] = Ids(gone), ["demolished"] = "new-construction" });
        var est = (JsonObject)f.Session.Query("cost.estimate", new JsonObject())!;
        var lines = est["chapters"]!.AsArray().SelectMany(c => c!["lines"]!.AsArray().Select(l => ((string)c["name"]!, (string)l!["code"]!, l))).ToList();
        // ZID01 only for the new wall (7.5 m³); DEM01 for the demolished one alone: 4 × 3 × 0.25 = 3 m³ at 160.
        var zid = lines.Single(x => x.Item2 == "ZID01").Item3!;
        Assert.Equal(7.5, N(zid["qty"]), 6);
        Assert.DoesNotContain(stays, zid["elements"]!.AsArray().Select(x => (string)x!));
        var dem = lines.Single(x => x.Item2 == "DEM01");
        Assert.Equal("Demolition", dem.Item1);
        Assert.Equal(3, N(dem.Item3!["qty"]), 6);
        Assert.Equal(480, N(dem.Item3["value"]));
        Assert.Equal([gone], dem.Item3["elements"]!.AsArray().Select(x => (string)x!));
        Assert.Equal("Demolition", (string)est["chapters"]![0]!["name"]!);
        Assert.Empty(est["unpriced"]!.AsArray());
        _ = (wall, type);
    }
}
