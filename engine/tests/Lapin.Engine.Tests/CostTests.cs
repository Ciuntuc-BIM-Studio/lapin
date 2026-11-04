using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Cost;
using Lapin.Drawing.Export;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The cost module, stage 6 of docs/plan-4d5d.md: price lists read from CSV and XLSX with Romanian or English headings,
/// articles on types and rules by material, the estimate's quantities times prices with totals worked by hand (the
/// contributions on labour, overheads, profit, VAT, EUR), layered walls priced layer by layer, the forms' contents, and
/// the estimate kept in the package, carried through IFC and undone.
/// </summary>
public class CostTests
{
    static Fixture F() => new(new ArchitectureModule(), new CostModule());

    static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    /// <summary>A Romanian price list: semicolons, decimal commas, diacritics in the headings.</summary>
    const string RoCsv =
        "Cod articol;Denumire;U.M.;Material;Manoperă;Utilaj;Transport;Normă de timp;Meserie\n" +
        "ZID01;Zidărie din cărămidă 25 cm;mc;380,00;85,00;6,00;14,00;4;Zidar\n" +
        "TEN01;Tencuială de var;mp;18;22;1;1;0,5;Tencuitor\n" +
        "USA01;Ușă interioară;buc;900;120;0;15;3;Tâmplar\n";

    static string Import(Fixture f, string csv, string name = "Prețuri 2026.csv", string? currency = null)
    {
        var a = new JsonObject { ["data"] = B64(csv), ["name"] = name };
        if (currency != null) a["currency"] = currency;
        return (string)f.Run("cost.importPrices", a)!["id"]!;
    }

    static string Material(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;

    /// <summary>
    /// A wall 10 m long, 3 m high: lime plaster 20 mm, brick 250 mm (the core), lime plaster 20 mm. Its type prices the
    /// core by ZID01 (7.5 m³); a rule prices lime plaster by TEN01 (two layers of 30 m²).
    /// </summary>
    static (Fixture F, string Wall, string Type, string Estimate) House()
    {
        var f = F();
        Import(f, RoCsv);
        var type = f.Type("Brick 250 plastered", ("Lime plaster", 0.02, false), ("Brick", 0.25, true), ("Lime plaster", 0.02, false));
        var wall = f.Wall(0, 0, 10, 0, type);
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(new JsonObject { ["code"] = "ZID01", ["part"] = "core" }) });
        f.Run("cost.setRule", new JsonObject { ["material"] = Material(f, "Lime plaster"), ["code"] = "TEN01" });
        var est = (string)f.Run("cost.createEstimate", new JsonObject { ["name"] = "Deviz casă" })!["id"]!;
        return (f, wall, type, est);
    }

    static JsonObject Estimate(Fixture f) => (JsonObject)f.Session.Query("cost.estimate", new JsonObject())!;

    static JsonObject Line(JsonObject est, string code) =>
        (JsonObject)est["chapters"]!.AsArray().SelectMany(c => c!["lines"]!.AsArray()).First(l => (string)l!["code"]! == code)!;

    static double N(JsonNode? n) => (double)n!;

    // ------------------------------------------------------------------ price lists

    [Fact]
    public void A_Romanian_price_list_is_read_by_its_headings_with_decimal_commas()
    {
        var table = PriceLists.Csv(RoCsv);
        var (header, map) = PriceLists.Guess(table);
        Assert.Equal(0, header);
        Assert.Equal(0, map["code"]); Assert.Equal(1, map["name"]); Assert.Equal(2, map["unit"]);
        Assert.Equal(3, map["material"]); Assert.Equal(4, map["labour"]); Assert.Equal(5, map["equipment"]); Assert.Equal(6, map["transport"]);
        Assert.Equal(7, map["hours"]); Assert.Equal(8, map["trade"]);
        Assert.Null(map["price"]); Assert.Null(map["article"]);
        var res = PriceLists.Import(table, header, map);
        Assert.Equal(3, res.Rows.Count);
        var z = res.Rows[0];
        Assert.Equal(("ZID01", "mc", 380.0, 85.0, 6.0, 14.0, 4.0, "Zidar"), (z.Code, z.Unit, z.Material, z.Labour, z.Equipment, z.Transport, z.Hours!.Value, z.Trade));
        Assert.Equal(0.5, res.Rows[1].Hours);
    }

    [Fact]
    public void An_English_workbook_is_read_by_its_headings_a_title_row_above_them()
    {
        var bytes = XlsxWriter.Write([("Prices", (IReadOnlyList<IReadOnlyList<string>>)[
            ["Contractor's rates 2026"],
            ["Item code", "Description", "Unit", "Materials", "Labour", "Plant", "Transport", "Man-hours"],
            ["CONC1", "Concrete C25/30 in columns", "m3", "410", "95", "22", "18", "6"],
            ["FORM1", "Plywood formwork", "m2", "35", "48", "4", "2", "1.2"],
        ])]);
        var table = PriceLists.Table(bytes);
        var (header, map) = PriceLists.Guess(table);
        Assert.Equal(1, header);
        Assert.Equal(3, map["material"]); Assert.Equal(5, map["equipment"]); Assert.Equal(7, map["hours"]);
        var f = F();
        var r = (JsonObject)f.Run("cost.importPrices", new JsonObject { ["data"] = Convert.ToBase64String(bytes), ["name"] = "rates.xlsx", ["currency"] = "EUR" })!;
        Assert.Equal(2, (int)r["count"]!);
        var list = PriceLists.All(f.Model).Single();
        Assert.Equal(("rates", "EUR"), (list.Name, list.Currency));
        Assert.Equal(410 + 95 + 22 + 18, list.Articles["conc1"].Total);
    }

    [Fact]
    public void A_list_with_a_total_alone_counts_it_as_material_and_says_so_and_columns_can_be_matched_by_hand()
    {
        var csv = "Code,Name,Unit,Unit price\nA1,Paint,m2,21.5\n";
        var res = PriceLists.Import(PriceLists.Csv(csv), 0, PriceLists.Guess(PriceLists.Csv(csv)).Map);
        Assert.Equal(21.5, res.Rows[0].Material);
        Assert.Contains(res.Notes, n => n.Contains("only a total"));
        // Headings Lapin does not know: the dialog's matching is passed in.
        var f = F();
        var odd = "Nr;Ce;Cât;Lei\nX1;Vopsea;mp;30\n";
        Assert.Equal(-1, PriceLists.Guess(PriceLists.Csv(odd)).Header);
        f.Run("cost.importPrices", new JsonObject { ["data"] = B64(odd), ["name"] = "odd.csv", ["header"] = 0, ["map"] = new JsonObject { ["code"] = 0, ["name"] = 1, ["unit"] = 2, ["price"] = 3 } });
        Assert.Equal(30, PriceLists.All(f.Model).Single().Articles["X1"].Material);
    }

    [Fact]
    public void The_template_is_Lapins_own_indicative_values_and_reads_back()
    {
        var csv = PriceLists.TemplateCsv();
        Assert.All(csv.Split('\n').Skip(1).Where(l => l.Length > 0), l => Assert.Contains("indicative", l));
        var res = PriceLists.Import(PriceLists.Csv(csv), 0, PriceLists.Guess(PriceLists.Csv(csv)).Map);
        Assert.Equal(PriceLists.Indicative.Count, res.Rows.Count);
        var f = F();
        f.Run("cost.addPriceList", new JsonObject { ["template"] = true });
        Assert.True(PriceLists.All(f.Model).Single().Indicative);
    }

    // ------------------------------------------------------------------ the estimate

    [Fact]
    public void Quantities_times_prices_give_the_totals_worked_by_hand()
    {
        var (f, wall, _, _) = House();
        var e = Estimate(f);
        // The core: 10 × 3 × 0.25 = 7.5 m³ at 380 + 85 + 6 + 14.
        var z = Line(e, "ZID01");
        Assert.Equal(7.5, N(z["qty"]), 6);
        Assert.Equal(485, N(z["unitPrice"]));
        Assert.Equal(3637.5, N(z["value"]));
        Assert.Contains(wall, z["elements"]!.AsArray().Select(x => (string)x!));
        // The plaster, both faces: 2 × 30 m² at 18 + 22 + 1 + 1.
        var t = Line(e, "TEN01");
        Assert.Equal(60, N(t["qty"]), 6);
        Assert.Equal(2520, N(t["value"]));
        var tot = (JsonObject)e["totals"]!;
        Assert.Equal(3930, N(tot["m"])); Assert.Equal(1957.5, N(tot["l"])); Assert.Equal(105, N(tot["e"])); Assert.Equal(165, N(tot["t"]));
        Assert.Equal(6157.5, N(tot["direct"]));
        Assert.Equal(44.04, N(tot["labourTax"]));       // 2.25 % of 1957.50
        Assert.Equal(6201.54, N(tot["directTotal"]));
        Assert.Equal(620.15, N(tot["overheads"]));      // 10 %
        Assert.Equal(341.08, N(tot["profit"]));         // 5 % of 6821.69
        Assert.Equal(7162.77, N(tot["net"]));
        Assert.Equal(1504.18, N(tot["vat"]));           // 21 %
        Assert.Equal(8666.95, N(tot["total"]));
        Assert.Equal(1733.39, N(tot["other"]));         // in EUR at 5 RON
        Assert.Empty(e["unpriced"]!.AsArray());
    }

    [Fact]
    public void A_layered_wall_is_priced_layer_by_layer_its_core_under_walls_its_plaster_under_finishes()
    {
        var (f, _, type, _) = House();
        var chapters = Estimate(f)["chapters"]!.AsArray().Select(c => $"{(string)c!["name"]!}: {string.Join(", ", c["lines"]!.AsArray().Select(l => (string)l!["code"]!))}").ToList();
        Assert.Equal(["Walls: ZID01", "Finishes and insulation: TEN01"], chapters);
        // A rule for brick too: the type's article on the core comes first, so the brick is not priced twice.
        f.Run("cost.setRule", new JsonObject { ["material"] = Material(f, "Brick"), ["code"] = "USA01" });
        Assert.DoesNotContain(Estimate(f)["chapters"]!.AsArray().SelectMany(c => c!["lines"]!.AsArray()), l => (string)l!["code"]! == "USA01");
        // An article on the whole wall prices all of it: the rules on its layers stand aside.
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(new JsonObject { ["code"] = "ZID01" }) });
        var e = Estimate(f);
        Assert.Equal(7.5 + 2 * 10 * 3 * 0.02, N(Line(e, "ZID01")["qty"]), 6);
        Assert.Single(e["chapters"]!.AsArray().SelectMany(c => c!["lines"]!.AsArray()));
    }

    [Fact]
    public void Waste_a_factor_and_a_measure_change_the_quantity_and_a_type_without_articles_is_listed_unpriced()
    {
        var (f, _, type, _) = House();
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(
            new JsonObject { ["code"] = "ZID01", ["part"] = "core", ["waste"] = 4 },
            new JsonObject { ["code"] = "USA01", ["basis"] = "count", ["factor"] = 2 }) });
        var e = Estimate(f);
        Assert.Equal(7.8, N(Line(e, "ZID01")["qty"]), 6);   // 7.5 + 4 %
        Assert.Equal(2, N(Line(e, "USA01")["qty"]), 6);
        var bare = f.Type("Partition", ("Gypsum board", 0.1, true));
        f.Wall(0, 5, 4, 5, bare);
        var unpriced = Estimate(f)["unpriced"]!.AsArray().Single()!;
        Assert.Equal(("Partition", 1), ((string)unpriced["name"]!, (int)unpriced["count"]!));
        // An article whose code no list has: a line with no price, its code listed.
        f.Run("cost.setArticles", new JsonObject { ["type"] = bare, ["articles"] = new JsonArray(new JsonObject { ["code"] = "NOPE" }) });
        e = Estimate(f);
        Assert.False((bool)Line(e, "NOPE")["priced"]!);
        Assert.Equal(["NOPE"], e["missing"]!.AsArray().Select(x => (string)x!));
    }

    [Fact]
    public void A_list_in_EUR_is_converted_at_the_projects_rate_and_an_estimate_in_EUR_shows_RON_as_its_equivalent()
    {
        var f = F();
        Import(f, "code;name;unit;material;labour\nP1;Paint;m2;2;1\n", "eur.csv", "EUR");
        var type = f.Type("Painted", ("Concrete", 0.2, true));
        f.Wall(0, 0, 5, 0, type);
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(new JsonObject { ["code"] = "P1", ["basis"] = "grossArea" }) });
        var est = (string)f.Run("cost.createEstimate", new JsonObject())!["id"]!;
        f.Run("cost.updateEstimate", new JsonObject { ["id"] = est, ["eurRate"] = 4.97, ["labourTax"] = 0, ["overheads"] = 0, ["profit"] = 0, ["vat"] = 0 });
        var e = Estimate(f);
        var p = Line(e, "P1");
        Assert.Equal(15, N(p["qty"]), 6);                     // 5 × 3 m²
        Assert.Equal((9.94, 4.97), (N(p["m"]), N(p["l"])));   // EUR 2 and 1 at 4.97
        Assert.Equal(223.65, N(e["totals"]!["total"]));       // 15 × 14.91
        f.Run("cost.updateEstimate", new JsonObject { ["id"] = est, ["currency"] = "EUR", ["equivalent"] = true });
        e = Estimate(f);
        Assert.Equal(45, N(e["totals"]!["total"]));            // 15 × 3 EUR
        Assert.Equal(223.65, N(e["totals"]!["other"]));        // in RON
        Assert.Equal("RON", (string)e["settings"]!["other"]!);
    }

    [Fact]
    public void Lines_of_the_users_own_are_kept_and_one_naming_a_code_takes_its_price()
    {
        var (f, _, _, est) = House();
        var id = (string)f.Run("cost.addLine", new JsonObject { ["id"] = est, ["chapter"] = "Site works", ["name"] = "Site fence", ["unit"] = "m", ["qty"] = 40, ["m"] = 25, ["l"] = 10 })!["line"]!;
        f.Run("cost.addLine", new JsonObject { ["id"] = est, ["code"] = "USA01", ["qty"] = 3, ["unit"] = "buc" });
        var e = Estimate(f);
        var fence = (JsonObject)e["chapters"]!.AsArray().Single(c => (string)c!["name"]! == "Site works")!["lines"]!.AsArray().Single()!;
        Assert.Equal((1400.0, id), (N(fence["value"]), (string)fence["manual"]!));
        Assert.Equal(3105, N(Line(e, "USA01")["value"]));
        f.Run("cost.updateLine", new JsonObject { ["id"] = est, ["line"] = id, ["qty"] = 50 });
        Assert.Equal(1750, N(((JsonObject)Estimate(f)["chapters"]!.AsArray().Single(c => (string)c!["name"]! == "Site works")!)["lines"]![0]!["value"]));
        f.Run("cost.removeLine", new JsonObject { ["id"] = est, ["line"] = id });
        Assert.DoesNotContain(Estimate(f)["chapters"]!.AsArray(), c => (string)c!["name"]! == "Site works");
    }

    [Fact]
    public void An_estimate_by_storey_puts_the_storey_before_each_chapter()
    {
        var (f, _, _, est) = House();
        f.Run("cost.updateEstimate", new JsonObject { ["id"] = est, ["grouping"] = "storey" });
        var storey = Spatial.Storeys(f.Model).First().Name;
        Assert.Contains(Estimate(f)["chapters"]!.AsArray(), c => (string)c!["name"]! == $"{storey} · Walls");
    }

    // ------------------------------------------------------------------ the forms

    static CostForm Form(Fixture f, string form)
    {
        var q = f.Session.Reader;
        return CostForms.Make(q, Costing.Compute(q, Costing.EstimateOf(f.Model)!), form);
    }

    static IReadOnlyList<string> RowOf(CostForm form, string label) => form.Rows.First(r => r.Cells.Contains(label)).Cells;

    [Fact]
    public void The_F3_lists_each_chapter_its_lines_split_and_the_totals_under_them()
    {
        var (f, _, _, _) = House();
        var f3 = Form(f, "f3");
        Assert.StartsWith("Formularul F3", f3.Title);
        Assert.Equal(14, f3.Columns.Count);
        Assert.Contains(f3.Rows, r => r.Style == "chapter" && r.Cells[2] == "Capitolul 1: Zidării și pereți");
        Assert.Contains(f3.Rows, r => r.Style == "chapter" && r.Cells[2] == "Capitolul 2: Finisaje și izolații");
        var z = f3.Rows.First(r => r.Cells[1] == "ZID01").Cells;
        Assert.Equal(["1.1", "ZID01", "Zidărie din cărămidă 25 cm", "mc", "7.500", "380.00", "85.00", "6.00", "14.00", "2850.00", "637.50", "45.00", "105.00", "3637.50"], z);
        Assert.Equal(["3930.00", "1957.50", "105.00", "165.00", "6157.50"], RowOf(f3, "Total cheltuieli directe").Skip(9));
        Assert.Equal("44.04", RowOf(f3, "Contribuția asiguratorie pentru muncă (CAM) 2.25 % din manoperă")[^1]);
        Assert.Equal("620.15", RowOf(f3, "Cheltuieli indirecte 10 %")[^1]);
        Assert.Equal("7162.77", RowOf(f3, "TOTAL fără TVA")[^1]);
        Assert.Equal("8666.95", RowOf(f3, "TOTAL cu TVA")[^1]);
    }

    [Fact]
    public void The_object_estimate_shares_the_value_by_chapter_and_its_rows_add_up_to_the_totals()
    {
        var (f, _, _, _) = House();
        var f2 = Form(f, "object");
        var rows = f2.Rows.Where(r => r.Key != null).ToList();
        Assert.Equal(["Zidării și pereți", "Finisaje și izolații"], rows.Select(r => r.Cells[1]));
        // 3637.50 of 6157.50 of the 7162.77 without VAT.
        Assert.Equal("4231.36", rows[0].Cells[2]);
        Assert.Equal(7162.77, rows.Sum(r => double.Parse(r.Cells[2], System.Globalization.CultureInfo.InvariantCulture)), 6);
        Assert.Equal(1504.18, rows.Sum(r => double.Parse(r.Cells[3], System.Globalization.CultureInfo.InvariantCulture)), 6);
        Assert.Equal(["", "TOTAL DEVIZ PE OBIECT", "7162.77", "1504.18", "8666.95"], f2.Rows[^1].Cells);
    }

    [Fact]
    public void The_resource_lists_add_up_to_the_estimates_split_labour_by_trade_with_its_hours()
    {
        var (f, _, _, _) = House();
        var c6 = Form(f, "c6");
        Assert.Equal(["1", "Tencuitor", "30.00", "44.00", "1320.00"], c6.Rows.First(r => r.Cells[1] == "Tencuitor").Cells);
        Assert.Equal(new[] { "Zidar", "30.00", "21.25", "637.50" }, c6.Rows.First(r => r.Cells[1] == "Zidar").Cells.Skip(1));
        Assert.Equal(["", "TOTAL", "60.00", "", "1957.50"], c6.Rows[^1].Cells);
        Assert.Equal("3930.00", Form(f, "c7").Rows[^1].Cells[^1]);
        Assert.Equal("105.00", Form(f, "c8").Rows[^1].Cells[^1]);
        Assert.Equal("165.00", Form(f, "c9").Rows[^1].Cells[^1]);
    }

    [Fact]
    public void An_article_given_by_its_resources_is_priced_by_them_and_the_materials_list_lists_them()
    {
        var f = F();
        Import(f, "Cod;Denumire;UM;Material;Manopera;Articol parinte;Tip resursa;Consum\n" +
            "ZR;Zidărie;mc;;;;;\n" +
            "CARA;Cărămidă;buc;1,2;;ZR;M;400\n" +
            "MORT;Mortar;mc;300;;ZR;M;0,25\n" +
            "ZIDAR;Zidar;ora;;25;ZR;Man;4\n");
        var list = PriceLists.All(f.Model).Single();
        Assert.Equal(3, list.Resources["ZR"].Count());
        var type = f.Type("Brick", ("Brick", 0.25, true));
        f.Wall(0, 0, 10, 0, type);
        f.Run("cost.setArticles", new JsonObject { ["type"] = type, ["articles"] = new JsonArray(new JsonObject { ["code"] = "ZR", ["basis"] = "volume" }) });
        f.Run("cost.createEstimate", new JsonObject());
        var z = Line(Estimate(f), "ZR");
        Assert.Equal((555.0, 100.0), (N(z["m"]), N(z["l"])));   // 400 × 1.2 + 0.25 × 300; 4 × 25
        var c7 = Form(f, "c7");
        Assert.Equal(["CARA", "Cărămidă", "buc", "3000.000", "1.20", "3600.00"], c7.Rows.First(r => r.Cells[1] == "CARA").Cells.Skip(1));
        Assert.Equal(["MORT", "Mortar", "mc", "1.875", "300.00", "562.50"], c7.Rows.First(r => r.Cells[1] == "MORT").Cells.Skip(1));
        Assert.Equal(["Zidar", "30.00", "25.00", "750.00"], Form(f, "c6").Rows.First(r => r.Cells[1] == "Zidar").Cells.Skip(1));
    }

    [Fact]
    public void The_forms_go_to_a_workbook_and_a_pdf_and_are_schedules_to_place_on_sheets()
    {
        var (f, _, _, est) = House();
        var xlsx = Convert.FromBase64String((string)f.Session.Query("cost.xlsx", new JsonObject { ["id"] = est })!);
        Assert.Equal(7, XlsxReaderSheets(xlsx));
        var first = XlsxReader.Read(xlsx);
        Assert.Contains(first, r => r.Contains("8666.95"));
        var pdf = Convert.FromBase64String((string)f.Session.Query("cost.pdf", new JsonObject { ["id"] = est, ["form"] = "f3" })!);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        var kinds = ((JsonArray)f.Session.Query("schedule.kinds", new JsonObject())!).Select(k => (string)k!["kind"]!).ToList();
        Assert.Contains("estimate", kinds);
        Assert.Contains("estimate-f3", kinds);
        var sid = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "estimate-f3" })!["id"]!;
        var csv = (string)f.Session.Query("schedule.csv", new JsonObject { ["id"] = sid })!;
        Assert.Contains("TOTAL cu TVA", csv);
    }

    static int XlsxReaderSheets(byte[] xlsx)
    {
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(xlsx));
        return zip.Entries.Count(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ mapping

    [Fact]
    public void Map_articles_suggests_by_the_words_proposes_the_core_and_applies_in_one_step()
    {
        var f = F();
        Import(f, RoCsv);
        var type = f.Type("Zidărie cărămidă 25 tencuită", ("Lime plaster", 0.02, false), ("Brick", 0.25, true));
        f.Wall(0, 0, 10, 0, type);
        var plaster = Material(f, "Lime plaster");
        f.Run("element.rename", new JsonObject { ["id"] = plaster, ["name"] = "Tencuială var" });
        var map = (JsonObject)f.Session.Query("cost.mapping", new JsonObject())!;
        var t = map["types"]!.AsArray().Single()!;
        Assert.Equal(("ZID01", "core", "volume"), ((string)t["suggested"]!["code"]!, (string)t["suggested"]!["part"]!, (string)t["suggested"]!["basis"]!));
        var m = map["materials"]!.AsArray().First(x => (string)x!["id"]! == plaster)!;
        Assert.Equal("TEN01", (string)m["suggested"]!["code"]!);
        f.Run("cost.applyMapping", new JsonObject
        {
            ["types"] = new JsonArray(new JsonObject { ["id"] = type, ["articles"] = new JsonArray(new JsonObject { ["code"] = "ZID01", ["part"] = "core" }) }),
            ["rules"] = new JsonArray(new JsonObject { ["material"] = plaster, ["code"] = "TEN01" }),
        });
        Assert.Single(ArticleRule.All(f.Model));
        Assert.Equal("ZID01", ArticleRef.Of(f.Model.Get(type)).Single().Code);
        // One step: undone together.
        f.Session.Undo();
        Assert.Empty(ArticleRule.All(f.Model));
        Assert.Empty(ArticleRef.Of(f.Model.Get(type)));
    }

    [Fact]
    public void A_site_works_types_deviz_article_of_decision_152_is_an_article_on_the_type()
    {
        var formwork = new Entity("t", "LapinFormworkType", "Plywood", new JsonObject { ["spec"] = new JsonObject { ["article"] = "CB04A1", ["articleText"] = "Cofraj" } });
        Assert.Equal([new ArticleRef("CB04A1")], ArticleRef.Of(formwork));
        // Its own articles, once set, come first.
        var set = formwork.With(attrs: new JsonObject { ["spec"] = new JsonObject { ["article"] = "CB04A1" }, ["articles"] = new JsonArray(new JsonObject { ["code"] = "X", ["basis"] = "ContactArea" }) });
        Assert.Equal([new ArticleRef("X", "ContactArea")], ArticleRef.Of(set));
    }

    // ------------------------------------------------------------------ keeping and exchanging

    [Fact]
    public void Commands_are_undone_one_step_each()
    {
        var (f, _, type, est) = House();
        Assert.NotEmpty(Estimate(f)["chapters"]!.AsArray());
        f.Session.Undo();   // the estimate
        Assert.Null(f.Session.Query("cost.estimate", new JsonObject()));
        f.Session.Undo();   // the rule
        Assert.Empty(ArticleRule.All(f.Model));
        f.Session.Undo();   // the articles
        Assert.Empty(ArticleRef.Of(f.Model.Get(type)));
        Assert.Null(f.Model.Get(est));
    }

    [Fact]
    public void The_estimate_and_price_lists_are_kept_in_the_package_and_open_again_the_same()
    {
        var (f, _, _, est) = House();
        f.Run("cost.addLine", new JsonObject { ["id"] = est, ["name"] = "Provision", ["qty"] = 1, ["m"] = 500 });
        var before = Estimate(f).ToJsonString();
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        ms.Position = 0;
        var content = LapinPackage.Read(ms);
        Assert.Equal("cost", content.Classes[PriceLists.Class]);
        var g = F();
        g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Assert.Equal(before, Estimate(g).ToJsonString());
    }

    [Fact]
    public void The_estimate_goes_to_IFC_as_a_cost_schedule_and_comes_back_with_the_same_totals()
    {
        var (f, wall, _, _) = House();
        var expected = (JsonObject)Estimate(f)["totals"]!;
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "deviz.ifc");
        var step = StepFile.Read(ms.ToArray());
        Assert.Single(step.OfType("IFCCOSTSCHEDULE"));
        Assert.Equal(4, step.OfType("IFCCOSTITEM").Count());             // two chapters, two lines
        Assert.Equal(8, step.OfType("IFCCOSTVALUE").Count());            // four kinds a line
        Assert.Contains(step.OfType("IFCMONETARYUNIT"), u => (string?)u[0] == "RON");
        Assert.Contains(step.OfType("IFCQUANTITYVOLUME"), q => q[3] is double v && Math.Abs(v - 7.5) < 1e-9);
        // The wall is controlled by both lines.
        var wallRef = step.OfType("IFCWALL").Single(w => (string?)w[0] == wall).Id;
        Assert.Equal(2, step.OfType("IFCRELASSIGNSTOCONTROL").Count(r => r[4] is List<object?> l && l.OfType<StepRef>().Any(x => x.Id == wallRef)));
        var g = F();
        IfcImport.Run(g.Session, ms.ToArray(), "deviz.ifc", new IfcImportOptions(), out _);
        var back = Estimate(g);
        Assert.False((bool)back["settings"]!["derive"]!);
        Assert.Equal(expected.ToJsonString(), back["totals"]!.ToJsonString());
        Assert.Equal(7.5, N(Line(back, "ZID01")["qty"]), 6);
        Assert.Contains(wall, Line(back, "ZID01")["elements"]!.AsArray().Select(x => (string)x!));
        Assert.Equal(["Walls", "Finishes and insulation"], back["chapters"]!.AsArray().Select(c => (string)c!["name"]!));
        // Read again into the same project: the estimate is there already.
        IfcImport.Run(g.Session, ms.ToArray(), "deviz.ifc", new IfcImportOptions(), out _);
        Assert.Single(g.Model.OfClass(Costing.Class));
    }

    [Fact]
    public void An_old_project_without_prices_or_an_estimate_answers_with_nothing_to_price()
    {
        var f = F();
        var type = f.Type("Old wall", ("Brick", 0.25, true));
        f.Wall(0, 0, 4, 0, type);
        Assert.Null(f.Session.Query("cost.estimate", new JsonObject()));
        Assert.Empty((JsonArray)f.Session.Query("cost.priceLists", new JsonObject())!);
        var map = (JsonObject)f.Session.Query("cost.mapping", new JsonObject())!;
        Assert.Null(map["types"]![0]!["suggested"]);
        f.Run("cost.createEstimate", new JsonObject());
        var e = Estimate(f);
        Assert.Empty(e["chapters"]!.AsArray());
        Assert.Equal("Old wall", (string)e["unpriced"]![0]!["name"]!);
        Assert.Equal(0, N(e["totals"]!["total"]));
    }
}
