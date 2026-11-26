using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Cost;
using Lapin.Planning;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Articles priced by the time an element is on site (docs/plan-forum-round-1126.md, R7; Fabio Emmolo: site safety items
/// hired per month, per day or per hour): an article's quantity times the span of the tasks that take its element, in
/// months, working days (the calendar's weekdays, holidays and exceptions), calendar days or working hours, by the price's
/// unit (m × lună, buc/zi) or the article's own choice; an element no task takes keeps its quantity and its line says so;
/// without the Planning module an article is priced as before; old articles and units without a time are unchanged.
/// </summary>
public sealed class CostDurationTests
{
    const string Csv =
        "Cod articol;Denumire;U.M.;Material;Manoperă;Utilaj;Transport\n" +
        "GARD;Gard de șantier închiriat;m × lună;4,00;0;0;0\n" +
        "SEMN;Semnalizare provizorie;buc/zi lucrătoare;2;0;0;0\n" +
        "MACARA;Macara închiriată;oră;0;0;150;0\n" +
        "ZID;Zidărie;m;10;0;0;0\n";

    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());

    /// <summary>A fence: a wall 20 m long of its own type, the prices above, an estimate; maybe a schedule with one task taking every wall.</summary>
    static (Fixture F, string Type, string Wall, string? Task) Site(JsonObject article, int? days = null, string start = "2027-03-01", string? holidays = "none", bool planning = true)
    {
        var f = planning ? new Fixture(new ArchitectureModule(), new PlanningModule(), new CostModule()) : new Fixture(new ArchitectureModule(), new CostModule());
        f.Run("cost.importPrices", new JsonObject { ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Csv)), ["name"] = "Șantier.csv" });
        var type = f.Type("Site fence", ("Steel", 0.05, true));
        var wall = f.Wall(0, 0, 20, 0, type);
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(article) });
        f.Run("cost.createEstimate", new JsonObject { ["name"] = "Organizare de șantier" });
        string? task = null;
        if (days is { } d)
        {
            f.Run("planning.createSchedule", new JsonObject { ["start"] = start, ["name"] = "Șantier" });
            if (holidays != null) f.Run("planning.setCalendar", new JsonObject { ["holidays"] = holidays });
            task = (string)f.Run("planning.addTask", new JsonObject { ["name"] = "Site set-up", ["duration"] = d })!["id"]!;
            f.Run("planning.updateTask", new JsonObject { ["id"] = task, ["rule"] = new JsonObject { ["classes"] = Ids("IfcWall") } });
        }
        return (f, type, wall, task);
    }

    static JsonObject Line(Fixture f, string code) => (JsonObject)((JsonObject)f.Session.Query("cost.estimate", new JsonObject())!)["chapters"]!.AsArray()
        .SelectMany(c => c!["lines"]!.AsArray()).First(l => (string)l!["code"]! == code)!;

    static JsonObject Code(string code, string? basis = null, string? time = null)
    {
        var o = new JsonObject { ["code"] = code };
        if (basis != null) o["basis"] = basis;
        if (time != null) o["time"] = time;
        return o;
    }

    [Fact]
    public void A_20_m_fence_on_site_for_a_task_of_3_months_priced_m_by_month_is_60()
    {
        // 1 March to 31 May 2027, five days a week, no holidays: 66 working days, three calendar months.
        var (f, _, wall, _) = Site(Code("GARD"), days: 66);
        var l = Line(f, "GARD");
        Assert.Equal("length", (string)l["basis"]!);          // m × lună: a length, no longer a count
        Assert.Equal("months", (string)l["time"]!);
        Assert.Equal(60, (double)l["qty"]!, 6);
        Assert.Equal(240, (double)l["value"]!, 2);
        Assert.Equal("3 months", (string)l["duration"]!);
        Assert.Null(l["notes"]);
        Assert.Equal([wall], l["elements"]!.AsArray().Select(x => (string)x!));
        // The estimate and its forms show the duration used: the estimate's description, F3 in Romanian, and the workbook's.
        var r = Costing.Compute(f.Session.Reader, Costing.EstimateOf(f.Model)!);
        var est = CostForms.Make(f.Session.Reader, r, "estimate");
        Assert.Contains(est.Rows, x => x.Cells[2] == "Gard de șantier închiriat (× 3 months)" && x.Cells[4] == "60.000");
        Assert.Contains(CostForms.Make(f.Session.Reader, r, "f3").Rows, x => x.Cells[2] == "Gard de șantier închiriat (× 3 luni)");
        Assert.Contains(CostForms.Make(f.Session.Reader, r, "estimate", raw: true).Rows, x => x.Cells[2].EndsWith("(× 3 months)") && x.Cells[4] == "60");
        // IFC: the cost item carries its time and duration in Lapin_CostItem.
        var item = new CostIfc().CostSchedules(f.Session.Reader).Single().Items.Single(i => i.Identification == "GARD");
        Assert.Equal(60, item.Quantity!.Value, 6);
        Assert.Contains(("Time", "months"), item.Properties);
        Assert.Contains(("Duration", "3 months"), item.Properties);
        // The duration is worked out on every ask: a longer task, a longer hire (to 30 June: 4 months).
        var task = f.Model.OfClass("IfcTask").Single().Id;
        f.Run("planning.updateTask", new JsonObject { ["id"] = task, ["duration"] = 88 });
        Assert.Equal(80, (double)Line(f, "GARD")["qty"]!, 6);
    }

    [Fact]
    public void A_piece_on_site_for_10_working_days_counts_10_over_the_calendar_and_its_holidays_and_hours_by_the_day()
    {
        // From Monday 26 April 2027, Romania's holidays: 26–29.04, Good Friday 30.04 and Easter Monday 3.05 off, to 11.05.
        var (f, type, _, _) = Site(Code("SEMN"), days: 10, start: "2027-04-26", holidays: null);
        var l = Line(f, "SEMN");
        Assert.Equal("count", (string)l["basis"]!);
        Assert.Equal("workingDays", (string)l["time"]!);
        Assert.Equal(10, (double)l["qty"]!, 6);
        Assert.Equal("10 working days", (string)l["duration"]!);
        // Calendar days: the 16 days from 26.04 to 11.05, both counted (the article's own choice over its unit's).
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(Code("SEMN", time: "days")) });
        Assert.Equal(16, (double)Line(f, "SEMN")["qty"]!, 6);
        Assert.Equal("16 calendar days", (string)Line(f, "SEMN")["duration"]!);
        // Hours: by the price's unit (oră), 10 working days of the calendar's 8 hours; a day of 10 hours, 100.
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(Code("MACARA", basis: "count")) });
        Assert.Equal(80, (double)Line(f, "MACARA")["qty"]!, 6);
        Assert.Equal(12000, (double)Line(f, "MACARA")["value"]!, 2);
        f.Run("planning.setCalendar", new JsonObject { ["hours"] = 10 });
        Assert.Equal(100, (double)Line(f, "MACARA")["qty"]!, 6);
        // An exception worked (Good Friday) is one more working day: 10 working days now end on 10.05, 9 days of 10 hours before it and one on it.
        f.Run("planning.setCalendar", new JsonObject { ["exceptions"] = new JsonArray(new JsonObject { ["from"] = "2027-04-30", ["working"] = true, ["name"] = "Worked" }) });
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(Code("SEMN", time: "days")) });
        Assert.Equal(15, (double)Line(f, "SEMN")["qty"]!, 6);
        // "none": the unit's time left aside, a plain count.
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(Code("SEMN", time: "none")) });
        var plain = Line(f, "SEMN");
        Assert.Equal(1, (double)plain["qty"]!, 6);
        Assert.Null(plain["time"]);
        Assert.Null(plain["duration"]);
    }

    [Fact]
    public void An_element_no_task_takes_keeps_its_quantity_and_its_line_says_why_instead_of_a_silent_zero()
    {
        // A schedule whose one task takes the slabs: the fence is in no task.
        var (f, _, _, task) = Site(Code("GARD"), days: 66);
        f.Run("planning.updateTask", new JsonObject { ["id"] = task, ["rule"] = new JsonObject { ["classes"] = Ids("IfcSlab") } });
        var l = Line(f, "GARD");
        Assert.Equal(20, (double)l["qty"]!, 6);
        Assert.Null(l["duration"]);
        Assert.Equal(["1 element(s) in no task of the schedule: their quantity is not multiplied by the duration."], l["notes"]!.AsArray().Select(x => (string)x!));
        var r = Costing.Compute(f.Session.Reader, Costing.EstimateOf(f.Model)!);
        Assert.Contains(CostForms.Make(f.Session.Reader, r, "estimate").Rows, x => x.Cells[2].Contains("[1 element(s) in no task", StringComparison.Ordinal));
        Assert.Contains(CostForms.Make(f.Session.Reader, r, "f3").Rows, x => x.Cells[2].Contains("[durată lipsă: 1 element(e) fără sarcină]", StringComparison.Ordinal));
        // No schedule at all: the same quantity, and the line says the project has none.
        var (g, _, _, _) = Site(Code("GARD"));
        var lg = Line(g, "GARD");
        Assert.Equal(20, (double)lg["qty"]!, 6);
        Assert.Equal(["The project has no work schedule: the quantity is not multiplied by the duration."], lg["notes"]!.AsArray().Select(x => (string)x!));
    }

    [Fact]
    public void Without_the_Planning_module_an_article_is_priced_as_before_and_one_asking_for_a_duration_says_why_it_has_none()
    {
        var (f, type, _, _) = Site(Code("GARD"), planning: false);
        var l = Line(f, "GARD");
        Assert.Equal(20, (double)l["qty"]!, 6);
        Assert.Null(l["time"]);
        Assert.Null(l["notes"]);
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(Code("ZID", time: "months")) });
        var asked = Line(f, "ZID");
        Assert.Equal(20, (double)asked["qty"]!, 6);
        Assert.Equal(["The Planning module is not in this edition: the quantity is not multiplied by the duration."], asked["notes"]!.AsArray().Select(x => (string)x!));
    }

    [Fact]
    public void Old_articles_and_units_without_a_time_are_unchanged_and_a_time_is_one_undo_step()
    {
        // A unit with no time is not multiplied, even with a schedule taking the element.
        var (f, type, _, _) = Site(Code("ZID"), days: 66);
        var l = Line(f, "ZID");
        Assert.Equal(20, (double)l["qty"]!, 6);
        Assert.Null(l["time"]);
        Assert.Null(l["notes"]);
        // An article written before keeps its JSON: no time stored when it is auto; read back, auto.
        Assert.Equal("""{"code":"ZID"}""", f.Model.Get(type)!.Attrs["articles"]![0]!.ToJsonString());
        Assert.Equal(ArticleRef.AutoTime, ArticleRef.Read(JsonNode.Parse("""{"code":"X","basis":"length","factor":2}"""))!.Time);
        // A time chosen is stored, is one step, and is undone.
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(Code("ZID", time: "months")) });
        Assert.Equal("""{"code":"ZID","time":"months"}""", f.Model.Get(type)!.Attrs["articles"]![0]!.ToJsonString());
        Assert.Equal(60, (double)Line(f, "ZID")["qty"]!, 6);
        f.Session.Undo();
        Assert.Equal(20, (double)Line(f, "ZID")["qty"]!, 6);
        // What is not a time is refused.
        Assert.ThrowsAny<Exception>(() => f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(Code("ZID", time: "weeks")) }));
        // A rule on a material takes a time too.
        var steel = f.Model.OfClass(Lapin.Model.IfcClass.Material).First(m => m.Name == "Steel").Id;
        f.Run("cost.setRule", new JsonObject { ["material"] = steel, ["code"] = "SEMN", ["time"] = "workingDays" });
        Assert.Equal("workingDays", (string)ArticleRule.All(f.Model).Single().Article.Time);
    }

    [Theory]
    [InlineData("m × lună", "m", "months")]
    [InlineData("m x month", "m", "months")]
    [InlineData("cad × mese", "cad", "months")]
    [InlineData("buc/zi", "buc", "days")]
    [InlineData("buc/zi lucrătoare", "buc", "workingDays")]
    [InlineData("100 mp x luna", "100mp", "months")]
    [InlineData("month", "", "months")]
    [InlineData("oră", "", "hours")]
    [InlineData("h", "", "hours")]
    [InlineData("giorni", "", "days")]
    [InlineData("mc", "mc", null)]
    [InlineData("box", "box", null)]
    [InlineData("om-zi", "om-zi", null)]
    public void Units_split_into_their_measure_and_their_time(string unit, string measure, string? time)
    {
        var (m, t) = MeasureUnits.SplitTime(unit);
        Assert.Equal(time, t);
        if (time != null) Assert.Equal(measure, m);
    }

    [Fact]
    public void Units_of_time_measure_by_their_measure_and_no_longer_fall_back_to_a_count_on_import()
    {
        Assert.Equal(("length", 1.0), Units.BasisOf("m × lună"));
        Assert.Equal(("area", 0.01), Units.BasisOf("100 mp x luna"));
        Assert.Equal(("count", 1.0), Units.BasisOf("month"));
        Assert.Equal(("count", 1.0), Units.BasisOf("cad × month"));
        Assert.Null(Units.BasisOf("om-zi"));
        // The planning module's norms read units as before (a norm per month is no measure).
        Assert.Null(MeasureUnits.BasisOf("month"));
        // Importing prices: no note that Lapin does not measure them.
        var f = new Fixture(new ArchitectureModule(), new CostModule());
        var r = (JsonObject)f.Run("cost.importPrices", new JsonObject { ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Csv)), ["name"] = "x.csv" })!;
        Assert.DoesNotContain(r["notes"]!.AsArray(), n => ((string)n!).Contains("does not measure", StringComparison.Ordinal));
        Assert.Equal(3, ScheduleTimes.Months(new DateOnly(2027, 3, 1), new DateOnly(2027, 6, 1)));
        Assert.Equal(0.5, ScheduleTimes.Months(new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 16)));
        Assert.Equal(1, ScheduleTimes.Months(new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28)));
    }
}
