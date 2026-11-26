using System.Text.Json.Nodes;
using Lapin.Cost;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Time-related costs (docs/plan-4d5d.md, "Time-related costs"): an estimate item of the user's with a rate by the day,
/// working day, week or month, its quantity the time its task lasts (a level of effort task usually) or the whole
/// project's, worked out from the schedule on every ask, so a delay raises it; spread over those days in the S-curve and
/// the month table, in its own chapter, through IFC. The house of <see cref="CostTimeTests"/>: Masonry 26.04–04.05.2027,
/// Plaster 05.05–18.05.2027.
/// </summary>
public sealed class CostTimeRelatedTests
{
    static JsonObject Schedule(Fixture f) => (JsonObject)f.Session.Query("planning.schedule", new JsonObject())!;
    static JsonObject Row(JsonObject s, string id) => (JsonObject)s["tasks"]!.AsArray().First(t => (string)t!["id"]! == id)!;
    static JsonObject Estimate(Fixture f) => (JsonObject)f.Session.Query("cost.estimate", new JsonObject())!;
    static JsonObject OverTime(Fixture f) => (JsonObject)f.Session.Query("cost.overTime", new JsonObject())!;
    static JsonObject Line(JsonObject est, string name) => (JsonObject)est["chapters"]!.AsArray().SelectMany(c => c!["lines"]!.AsArray()).First(l => (string)l!["name"]! == name)!;
    static double N(JsonNode? n) => (double)n!;

    static string SiteManagement(Fixture f, Dictionary<string, string> t) =>
        (string)f.Run("planning.addTask", new JsonObject { ["name"] = "Site management", ["effort"] = "loe", ["effortFrom"] = t["masonry"], ["effortTo"] = t["plaster"] })!["id"]!;

    [Fact]
    public void A_weekly_rate_on_a_level_of_effort_task_costs_the_weeks_it_spans_and_a_delay_raises_it()
    {
        var (f, _, _, t) = CostTimeTests.House();
        var pm = SiteManagement(f, t);
        var budget = N(Estimate(f)["totals"]!["direct"]);
        f.Run("cost.addLine", new JsonObject { ["name"] = "Site manager", ["unit"] = "week", ["per"] = "weeks", ["m"] = 1000, ["tasks"] = new JsonArray(pm) });
        var est = Estimate(f);
        var line = Line(est, "Site manager");
        // 26.04 to 18.05.2027: 23 days, 3.286 weeks × 1000.
        Assert.Equal(3.286, N(line["qty"]));
        Assert.Equal(3286, N(line["value"]));
        Assert.Equal("weeks", (string)line["per"]!);
        Assert.Equal("2027-04-26", (string)line["spanFrom"]!);
        Assert.Equal("2027-05-18", (string)line["spanTo"]!);
        // Its own chapter, in the totals; the percentage overheads as they were (10 % of the direct costs and CAM).
        Assert.Contains(est["chapters"]!.AsArray(), c => (string)c!["name"]! == Costing.TimeRelatedChapter);
        Assert.Equal(budget + 3286, N(est["totals"]!["direct"]));
        var totals = (JsonObject)est["totals"]!;
        Assert.Equal(Money.Round((N(totals["directTotal"])) * 0.10), N(totals["overheads"]), 2);
        // In the Gantt on the task it is attached to, and in the S-curve over its working days, all scheduled.
        Assert.Equal(3286, N(Row(Schedule(f), pm)["cost"]));
        var o = OverTime(f);
        Assert.Equal(0, N(o["unscheduled"]));
        Assert.Equal(budget + 3286, N(o["scheduled"]));
        // Masonry slips by five working days: the plaster follows, the hammock with it, and the item costs a week more.
        f.Run("planning.updateTask", new JsonObject { ["id"] = t["masonry"], ["duration"] = 10 });
        Assert.Equal("2027-05-25", (string)Row(Schedule(f), pm)["finish"]!);
        line = Line(Estimate(f), "Site manager");
        Assert.Equal(4.286, N(line["qty"]));
        Assert.Equal(4286, N(line["value"]));
        Assert.Equal(budget + 4286, N(OverTime(f)["scheduled"]));
    }

    [Fact]
    public void A_monthly_rate_over_the_whole_project_is_spread_over_its_working_days_and_accrues_to_the_status_date()
    {
        var (f, _, _, _) = CostTimeTests.House();
        var budget = N(Estimate(f)["totals"]!["direct"]);
        f.Run("cost.addLine", new JsonObject { ["name"] = "Site office", ["unit"] = "month", ["per"] = "month", ["m"] = 2000 });
        var line = Line(Estimate(f), "Site office");
        // 26.04 to 18.05.2027: 23 days of the month from 26.04 (30 days) = 0.767 months × 2000.
        Assert.Equal(0.767, N(line["qty"]));
        Assert.Equal(1534, N(line["value"]));
        var o = OverTime(f);
        Assert.Equal(budget + 1534, N(o["scheduled"]));
        var chapter = o["chapters"]!.AsArray().First(c => (string)c!["name"]! == Costing.TimeRelatedChapter)!;
        Assert.Equal(0, N(chapter["notScheduled"]));
        // Fifteen working days, four of them in April (Good Friday off): 4/15 of it in April.
        Assert.Equal(Money.Round(1534.0 * 4 / 15), N(chapter["months"]![0]), 2);
        // Status date Tuesday 04.05, nothing reported done: the work starts on 05.05 and the project ends a week later, so the
        // item lasts longer (26.04 to 25.05: 30 days of 30) and five of its twenty working days are gone.
        f.Run("planning.updateSchedule", new JsonObject { ["dataDate"] = "2027-05-04" });
        line = Line(Estimate(f), "Site office");
        Assert.Equal(1, N(line["qty"]));
        Assert.Equal(2000, N(line["value"]));
        Assert.Equal(Money.Round(2000.0 * 5 / 20), N(OverTime(f)["toDate"]), 2);
    }

    [Fact]
    public void Without_a_schedule_a_time_related_item_keeps_its_typed_quantity_and_says_why()
    {
        var f = CostTimeTests.F();
        f.Run("cost.createEstimate", new JsonObject { ["name"] = "Deviz" });
        f.Run("cost.addLine", new JsonObject { ["name"] = "Site manager", ["qty"] = 6, ["unit"] = "week", ["per"] = "weeks", ["m"] = 1000 });
        var line = Line(Estimate(f), "Site manager");
        Assert.Equal(6, N(line["qty"]));
        Assert.Equal(6000, N(line["value"]));
        Assert.Contains("no work schedule", (string)line["notes"]![0]!);
        Assert.ThrowsAny<Exception>(() => f.Run("cost.addLine", new JsonObject { ["name"] = "Bad", ["per"] = "fortnights" }));
    }

    [Fact]
    public void A_time_related_item_goes_to_IFC_with_its_rate_unit_and_comes_back_the_same()
    {
        var (f, _, _, t) = CostTimeTests.House();
        var pm = SiteManagement(f, t);
        f.Run("cost.addLine", new JsonObject { ["name"] = "Site manager", ["unit"] = "week", ["per"] = "weeks", ["m"] = 1000, ["tasks"] = new JsonArray(pm) });
        var before = OverTime(f);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "casa.ifc");
        var g = CostTimeTests.F();
        IfcImport.Run(g.Session, ms.ToArray(), "casa.ifc", new IfcImportOptions(), out _);
        var line = Line(Estimate(g), "Site manager");
        Assert.Equal("weeks", (string)line["per"]!);
        Assert.Equal([pm], line["tasks"]!.AsArray().Select(x => (string)x!));
        Assert.Equal(3286, N(line["value"]));
        Assert.Equal("loe", (string)Row(Schedule(g), pm)["effort"]!);
        Assert.Equal(before["planned"]!.ToJsonString(), OverTime(g)["planned"]!.ToJsonString());
    }
}
