using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Cost;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The cash flow (docs/plan-4d5d.md, cash flow): the payments laid on the planned costs over time — certificates, the
/// retention held and released, the advance paid and recovered, the payment terms, the suppliers paid — with a worked
/// example reproduced by hand, the terms that make the cash in the cost incurred, the terms on the estimate as one undo
/// step and through IFC, the hook for actual certificates.
/// </summary>
public sealed class CashFlowTests
{
    static DateOnly D(string s) => DateOnly.Parse(s);

    /// <summary>
    /// The worked example. Direct costs 10 000 on 15.01.2027, 20 000 on 15.02, 10 000 on 15.03 (40 000); the contract 44 000
    /// (×1.1). Advance 10 % = 4 400 paid 01.01.2027; retention 5 % released 50/50 at the taking-over (31.03.2027) and after a
    /// 12-month defects period; certificates at each month's end, paid 30 days after; the suppliers paid 30 days after the
    /// cost; no VAT. By hand:
    ///   C1 31.01: certified 11 000, retention 550, advance 1 100 (11 000 × 4 400 / 44 000), net 9 350, paid 02.03.2027
    ///   C2 28.02: 22 000, 1 100, 2 200, net 18 700, paid 30.03.2027
    ///   C3 31.03: 11 000, 550, the advance left 1 100, net 9 350, paid 30.04.2027
    ///   retention 2 200: 1 100 paid 30.04.2027, 1 100 paid 30.04.2028 (31.03.2028 + 30 days)
    ///   out: 10 000 on 14.02, 20 000 on 17.03, 10 000 on 14.04
    ///   Jan +4 400 (4 400) · Feb −10 000 (−5 600) · Mar 28 050 − 20 000 (2 450) · Apr 10 450 − 10 000 (2 900) · … · Apr 2028 +1 100 (4 000 = 44 000 − 40 000)
    /// The peak funding need is 5 600 in February 2027.
    /// </summary>
    [Fact]
    public void A_worked_example_by_hand_advance_10_retention_5_released_half_and_half_30_days_terms_and_its_peak_funding_need()
    {
        var s = CashFlowSettings.Defaults with
        {
            AdvancePercent = 10, RetentionPercent = 5, RetentionAtTakeover = 50, DefectsMonths = 12, PaymentDays = 30,
            SupplierDays = 30, CostBasis = "direct", CertificateDay = 0, Cycle = "monthly", Vat = false, Indicative = false,
        };
        var cost = new Dictionary<DateOnly, double> { [D("2027-01-15")] = 10_000, [D("2027-02-15")] = 20_000, [D("2027-03-15")] = 10_000 };
        var o = CashFlow.Compute(s, cost, 40_000, 44_000, 40_000, 21, D("2027-01-01"), D("2027-03-31"), "RON");

        Assert.Equal(4_400, o.Advance);
        Assert.Equal(3, o.Certificates.Count);
        var c = o.Certificates;
        Assert.Equal((D("2027-01-31"), 11_000.0, 550.0, 1_100.0, 9_350.0, D("2027-03-02")), (c[0].Date, c[0].Certified, c[0].Retention, c[0].Recovered, c[0].Net, c[0].Due));
        Assert.Equal((D("2027-02-28"), 22_000.0, 1_100.0, 2_200.0, 18_700.0, D("2027-03-30")), (c[1].Date, c[1].Certified, c[1].Retention, c[1].Recovered, c[1].Net, c[1].Due));
        Assert.Equal((D("2027-03-31"), 11_000.0, 550.0, 1_100.0, 9_350.0, D("2027-04-30")), (c[2].Date, c[2].Certified, c[2].Retention, c[2].Recovered, c[2].Net, c[2].Due));
        Assert.Equal([(D("2027-04-30"), 1_100.0, "takeover"), (D("2028-04-30"), 1_100.0, "defects")], o.Releases);

        // January 2027 to April 2028: 16 months, the quiet ones too.
        Assert.Equal(16, o.Periods.Count);
        var p = o.Periods.ToDictionary(x => x.Start.ToString("yyyy-MM"));
        Assert.Equal((4_400.0, 0.0, 4_400.0), (p["2027-01"].In, p["2027-01"].Out, p["2027-01"].Cumulative));
        Assert.Equal((0.0, 10_000.0, -5_600.0), (p["2027-02"].In, p["2027-02"].Out, p["2027-02"].Cumulative));
        Assert.Equal((28_050.0, 20_000.0, 2_450.0), (p["2027-03"].In, p["2027-03"].Out, p["2027-03"].Cumulative));
        Assert.Equal((10_450.0, 10_000.0, 2_900.0), (p["2027-04"].In, p["2027-04"].Out, p["2027-04"].Cumulative));
        Assert.Equal((1_100.0, 0.0, 4_000.0), (p["2028-04"].In, p["2028-04"].Out, p["2028-04"].Cumulative));
        Assert.Equal(2_900.0, p["2027-12"].Cumulative);
        // The certified and held a month, the balances.
        Assert.Equal((11_000.0, 550.0, 1_100.0), (p["2027-01"].Certified, p["2027-01"].Retention, p["2027-01"].Recovered));
        Assert.Equal(2_200.0, p["2027-03"].RetentionHeld);
        Assert.Equal(1_100.0, p["2027-04"].RetentionHeld);
        Assert.Equal(0.0, p["2028-04"].RetentionHeld);
        Assert.Equal(3_300.0, p["2027-01"].AdvanceLeft);
        Assert.Equal(0.0, p["2027-03"].AdvanceLeft);
        Assert.Equal(10_000.0, p["2027-01"].Cost);
        // The peak funding need: the lowest running total, and when.
        Assert.Equal(-5_600, o.Peak);
        Assert.Equal(D("2027-02-01"), o.PeakPeriod);
        Assert.Equal(44_000, o.Periods.Sum(x => x.In));
        Assert.Equal(40_000, o.Periods.Sum(x => x.Out));
    }

    [Fact]
    public void With_no_advance_retention_or_terms_the_cash_in_is_the_cost_certified_and_the_net_nought_a_month()
    {
        var cost = new Dictionary<DateOnly, double> { [D("2027-01-04")] = 1_234.56, [D("2027-01-20")] = 100, [D("2027-02-15")] = 2_000.01, [D("2027-04-30")] = 7 };
        var o = CashFlow.Compute(CashFlowSettings.None with { Basis = "direct" }, cost, 3_341.57, 3_341.57, 3_341.57, 21, D("2027-01-04"), D("2027-04-30"), "RON");
        Assert.Equal(["2027-01", "2027-02", "2027-03", "2027-04"], o.Periods.Select(x => x.Start.ToString("yyyy-MM")));
        Assert.Equal([1_334.56, 2_000.01, 0, 7], o.Periods.Select(x => x.Certified));
        Assert.Equal(o.Periods.Select(x => x.Cost), o.Periods.Select(x => x.Certified));
        Assert.Equal(o.Periods.Select(x => x.Cost), o.Periods.Select(x => x.In));
        Assert.Equal(o.Periods.Select(x => x.Cost), o.Periods.Select(x => x.Out));
        Assert.All(o.Periods, x => Assert.Equal(0, x.Net));
        Assert.Equal(0, o.Peak);
        Assert.Null(o.PeakPeriod);
        Assert.Empty(o.Releases);
    }

    [Fact]
    public void Weekly_periods_and_weekly_certificates_on_sundays_and_a_certificate_day_of_the_month()
    {
        var s = CashFlowSettings.None with { Cycle = "weekly" };
        Assert.Equal(D("2027-01-10"), CashFlow.CertificateOf(D("2027-01-06"), s));        // a Wednesday → its Sunday
        Assert.Equal(D("2027-01-10"), CashFlow.CertificateOf(D("2027-01-10"), s));
        var day25 = CashFlowSettings.None with { CertificateDay = 25 };
        Assert.Equal(D("2027-01-25"), CashFlow.CertificateOf(D("2027-01-25"), day25));
        Assert.Equal(D("2027-02-25"), CashFlow.CertificateOf(D("2027-01-26"), day25));
        Assert.Equal(D("2027-01-04"), CashFlow.PeriodOf(D("2027-01-10"), "week"));        // the week's Monday
        var o = CashFlow.Compute(s, new Dictionary<DateOnly, double> { [D("2027-01-06")] = 100, [D("2027-01-13")] = 50 }, 150, 150, 150, 0, D("2027-01-06"), D("2027-01-13"), "RON", "week");
        Assert.Equal([D("2027-01-04"), D("2027-01-11")], o.Periods.Select(x => x.Start));
        Assert.Equal([100.0, 50.0], o.Periods.Select(x => x.In));
    }

    [Fact]
    public void Vat_is_paid_with_each_payment_in_and_out()
    {
        var s = CashFlowSettings.None with { Vat = true, AdvancePercent = 10 };
        var o = CashFlow.Compute(s, new Dictionary<DateOnly, double> { [D("2027-01-15")] = 1_000 }, 1_000, 1_000, 1_000, 21, D("2027-01-01"), D("2027-01-31"), "RON");
        Assert.Equal(1_000 * 1.21, o.Periods.Sum(x => x.In), 6);
        Assert.Equal(1_000 * 1.21, o.Periods.Sum(x => x.Out), 6);
        Assert.Equal(189, o.Certificates[0].Vat);                                          // on its net 900 (1 000 − the advance's 100)
    }

    sealed class GivenCertificates(IReadOnlyList<CertifiedValue> list) : ICertifiedProgress
    {
        public IReadOnlyList<CertifiedValue>? Certificates(IQueryContext query, string estimateId) => list;
    }

    [Fact]
    public void Certificates_of_actual_progress_replace_the_planned_ones_up_to_the_last_and_the_rest_of_the_value_follows_the_plan()
    {
        var cost = new Dictionary<DateOnly, double> { [D("2027-01-15")] = 100, [D("2027-02-15")] = 100, [D("2027-03-15")] = 200 };
        var o = CashFlow.Compute(CashFlowSettings.None, cost, 400, 400, 400, 0, D("2027-01-01"), D("2027-03-31"), "RON", "month",
            [new CertifiedValue(D("2027-01-31"), 60), new CertifiedValue(D("2027-02-28"), 90)]);
        Assert.Equal([60.0, 90.0, 250.0], o.Certificates.Select(c => c.Certified));        // March carries 400 − 150
        Assert.Equal([true, true, false], o.Certificates.Select(c => c.Actual));
        Assert.True(o.HasActual);
    }

    // ------------------------------------------------------------------ on the project

    static Fixture F() => new(new ArchitectureModule(), new PhasesModule(), new PlanningModule(), new CostModule());

    const string Csv =
        "Cod articol;Denumire;U.M.;Material;Manoperă;Utilaj;Transport\n" +
        "ZID01;Zidărie din cărămidă 25 cm;mc;380,00;85,00;6,00;14,00\n" +
        "TEN01;Tencuială de var;mp;18;22;1;1\n";

    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());

    /// <summary>The house of CostTimeTests: 6 157.50 direct, 2 910 planned in April 2027 (masonry), 3 247.50 in May (plaster).</summary>
    static (Fixture F, string Masonry, string Plaster) House()
    {
        var f = F();
        f.Run("cost.importPrices", new JsonObject { ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Csv)), ["name"] = "Prețuri.csv" });
        var type = f.Type("Brick 250 plastered", ("Lime plaster", 0.02, false), ("Brick", 0.25, true), ("Lime plaster", 0.02, false));
        f.Wall(0, 0, 10, 0, type);
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(new JsonObject { ["code"] = "ZID01", ["part"] = "core" }) });
        f.Run("cost.setRule", new JsonObject { ["material"] = f.Model.OfClass(IfcClass.Material).First(m => m.Name == "Lime plaster").Id, ["code"] = "TEN01" });
        f.Run("cost.createEstimate", new JsonObject { ["name"] = "Deviz casă" });
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-04-26", ["name"] = "Casa" });
        string Task(string name, int days, JsonObject extra)
        {
            var id = (string)f.Run("planning.addTask", new JsonObject { ["name"] = name, ["duration"] = days })!["id"]!;
            extra["id"] = id;
            f.Run("planning.updateTask", extra);
            return id;
        }
        var masonry = Task("Masonry", 5, new JsonObject { ["rule"] = new JsonObject { ["classes"] = Ids("IfcWall") }, ["part"] = "core" });
        var plaster = Task("Plaster", 10, new JsonObject { ["rule"] = new JsonObject { ["classes"] = Ids("IfcWall") }, ["part"] = "finishes" });
        f.Run("planning.link", new JsonObject { ["from"] = masonry, ["to"] = plaster });
        return (f, masonry, plaster);
    }

    static JsonObject Cash(Fixture f, string period = "month") => (JsonObject)f.Session.Query("cost.cashFlow", new JsonObject { ["period"] = period })!;
    static double N(JsonNode? n) => Json.Number(n)!.Value;

    [Fact]
    public void On_the_project_the_terms_set_to_nothing_reproduce_the_cost_over_time_a_month()
    {
        var (f, _, _) = House();
        var over = (JsonObject)f.Session.Query("cost.overTime", new JsonObject())!;
        f.Run("cost.setCashFlow", new JsonObject
        {
            ["basis"] = "direct", ["costBasis"] = "direct", ["retention"] = 0, ["advance"] = 0, ["paymentDays"] = 0, ["supplierDays"] = 0,
        });
        var c = Cash(f);
        Assert.False((bool)c["settings"]!["indicative"]!);
        var months = over["months"]!.AsArray().Select(m => N(m!["planned"])).ToList();
        Assert.Equal([2_910, 3_247.5], months);
        Assert.Equal(months, c["periods"]!.AsArray().Select(p => N(p!["certified"])));
        Assert.Equal(months, c["periods"]!.AsArray().Select(p => N(p!["in"])));
        Assert.Equal(months, c["periods"]!.AsArray().Select(p => N(p!["out"])));
        Assert.All(c["periods"]!.AsArray(), p => Assert.Equal(0, N(p!["net"])));
        Assert.Equal(0, N(c["peak"]));
    }

    [Fact]
    public void With_nothing_set_the_terms_are_lapins_indicative_defaults_and_the_contractor_needs_funding_before_the_first_payment()
    {
        var (f, _, _) = House();
        var c = Cash(f);
        var s = (JsonObject)c["settings"]!;
        Assert.True((bool)s["indicative"]!);
        Assert.Equal(("net", 5.0, 50.0, 30.0, "monthly"), ((string)s["basis"]!, N(s["retention"]), N(s["retentionAtTakeover"]), N(s["paymentDays"]), (string)s["cycle"]!));
        var est = (JsonObject)f.Session.Query("cost.estimate", new JsonObject())!;
        Assert.Equal(N(est["totals"]!["net"]), N(c["contractValue"]));
        Assert.True(N(c["peak"]) < 0);
        Assert.Equal("2027-05-01", (string)c["peakPeriod"]!);                             // the April certificate is paid at the end of May; the May suppliers too
        Assert.Equal(N(c["contractValue"]), N(c["cashIn"]), 2);                           // all of it in, at the end
        // Weekly periods: the same money.
        Assert.Equal(N(c["cashIn"]), N(Cash(f, "week")["cashIn"]), 2);
    }

    [Fact]
    public void The_terms_are_one_undo_step_checked_reset_and_written_to_IFC_as_lapin_properties()
    {
        var (f, masonry, plaster) = House();
        Assert.Throws<CommandException>(() => f.Run("cost.setCashFlow", new JsonObject { ["retention"] = 120 }));
        Assert.Throws<CommandException>(() => f.Run("cost.setCashFlow", new JsonObject { ["cycle"] = "daily" }));
        Assert.Throws<CommandException>(() => f.Run("cost.setCashFlow", new JsonObject { ["advanceTask"] = "nope" }));
        f.Run("cost.setCashFlow", new JsonObject { ["advance"] = 10, ["retention"] = 10, ["advanceTask"] = masonry, ["takeoverTask"] = plaster, ["vat"] = true });
        var c = Cash(f);
        Assert.Equal("2027-04-26", (string)c["advanceDate"]!);
        Assert.Equal((string)((JsonObject)f.Session.Query("planning.schedule", new JsonObject())!)["finish"]!, (string)c["takeover"]!);
        f.Session.Undo();
        Assert.True((bool)Cash(f)["settings"]!["indicative"]!);
        f.Session.Redo();
        Assert.Equal(10, N(Cash(f)["settings"]!["retention"]));
        f.Run("cost.setCashFlow", new JsonObject { ["retention"] = null });
        Assert.Equal(5, N(Cash(f)["settings"]!["retention"]));

        // IFC: the terms in Lapin_Estimate, read back the same (the tasks named stay out).
        f.Run("cost.setCashFlow", new JsonObject { ["paymentDays"] = 45, ["basis"] = "contract", ["contractValue"] = 9_000, ["advanceDate"] = "2027-04-20" });
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "casa.ifc");
        var text = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("'CashFlow.PaymentDays'", text);
        Assert.Contains("'CashFlow.Basis'", text);
        var g = F();
        IfcImport.Run(g.Session, ms.ToArray(), "casa.ifc", new IfcImportOptions(), out _);
        var e = g.Model.OfClass(Costing.Class).Single();
        var s = CashFlowSettings.Of(e);
        Assert.Equal((45, "contract", 9_000.0, "2027-04-20", true, 10.0), (s.PaymentDays, s.Basis, s.ContractValue!.Value, s.AdvanceDate!, s.Vat, s.AdvancePercent));
        Assert.Null(s.AdvanceTask);

        f.Run("cost.setCashFlow", new JsonObject { ["reset"] = true });
        Assert.Null(f.Model.OfClass(Costing.Class).Single().Attrs[CashFlowSettings.Attr]);
    }

    [Fact]
    public void An_estimate_without_terms_writes_no_cash_flow_property_to_IFC()
    {
        var (f, _, _) = House();
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "casa.ifc");
        Assert.DoesNotContain("CashFlow.", Encoding.UTF8.GetString(ms.ToArray()));
    }

    [Fact]
    public void The_cash_flow_is_a_form_to_place_on_sheets_a_workbook_and_a_csv()
    {
        var (f, _, _) = House();
        var form = (JsonObject)f.Session.Query("cost.form", new JsonObject { ["form"] = "cashflow" })!;
        Assert.Equal("Cash flow", (string)form["title"]!);
        Assert.Contains(form["rows"]!.AsArray(), r => ((string)r!["cells"]![0]!).StartsWith("Peak funding need", StringComparison.Ordinal));
        var xlsx = Convert.FromBase64String((string)f.Session.Query("cost.cashFlowXlsx", new JsonObject())!);
        Assert.True(xlsx.Length > 100);
        var csv = (string)f.Session.Query("cost.cashFlowCsv", new JsonObject())!;
        Assert.StartsWith("Period;Cost incurred;Certified;", csv);
        Assert.Contains("\n2027-04-01;", csv);
        // A schedule of kind estimate-cashflow, as the S-curve's month table.
        var made = f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "estimate-cashflow", ["name"] = "Cash flow" });
        Assert.NotNull(made?["id"]);
        // The estimate's own workbook keeps its forms (no cash-flow sheet added).
        var forms = f.Session.Query("cost.xlsx", new JsonObject());
        Assert.NotNull(forms);
    }

    [Fact]
    public void Without_a_schedule_the_cash_flow_says_why()
    {
        var f = F();
        f.Run("cost.createEstimate", new JsonObject());
        Assert.NotNull(Cash(f)["why"]);
    }
}
