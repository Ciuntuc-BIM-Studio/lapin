using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Cost;
using Lapin.Model;
using Lapin.Platform;
using Lapin.RoofPanels;

namespace Lapin.Engine.Tests;

/// <summary>
/// The roof panels' cost link (docs/plan-roof-panels.md §2.7, stage S4). The numbers are the Panotaj Bilka app's (v1.3.3, the
/// owner's Python app): the reference roofs of <see cref="RoofPanelTakeoffTests"/> exported to IFC by Lapin on 25.11.2026 and run
/// through its <c>analyze(…)</c> (a copy, outside its folder) with its own catalogue (prices read on 24.11.2026); each line is a
/// row of its "Deviz RON" sheet (the amount: the quantity by the unit price, ROUND_HALF_UP to the ban), the subtotal its
/// <c>priced_subtotal_ron</c> less what Lapin counts in stage S5 (the flat sheet and the rainwater system, named in each case).
/// </summary>
public class RoofPanelCostTests
{
    const string Template = PanelCost.BilkaTemplate.TemplateId;

    static (Fixture F, string Roof) Make(string roof) => RoofPanelTakeoffTests.Make(roof, new CostModule());

    static JsonObject Layout(Fixture f, string roof) => (JsonObject)f.Session.Query("roofpanels.layout", new JsonObject { ["id"] = roof })!;

    static void Panels(Fixture f, string roof, string profile, string? color, string? ridge = null, string? fixing = null, string? support = null)
    {
        var a = new JsonObject { ["id"] = roof, ["profile"] = profile };
        if (color != null) a["color"] = color;
        if (ridge != null) a["ridge"] = ridge;
        if (fixing != null) a["fixing"] = fixing;
        if (support != null) a["support"] = support;
        f.Run("roofpanels.setRoofPanels", a);
    }

    static string Amount(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>The estimate's Roof lines as "code#variant:amount", by key.</summary>
    static (string Lines, double Sum) RoofLines(JsonObject estimate)
    {
        var roof = estimate["chapters"]!.AsArray().Single(c => (string)c!["name"]! == "Roof")!;
        var lines = roof["lines"]!.AsArray().Select(l =>
        {
            var parts = ((string)l!["key"]!).Split('\u001f');
            var key = parts.Length > 2 ? $"{(string)l["code"]!}#{parts[2]}" : (string)l["code"]!;
            return (Key: key, Value: (double)l["value"]!);
        }).OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
        return (string.Join(" ", lines.Select(x => $"{x.Key}:{Amount(x.Value)}")), lines.Sum(x => x.Value));
    }

    [Theory]
    // roof, profile, colour, ridge, fixing, support, the app's subtotal less S5, its lines
    [InlineData("gable", "bilka-balcanic", "8017", null, null, null, "11011.03", "batten:1577.70 bilka-balcanic/8017/2230:4759.68 counter-batten:701.20 eave:492.96 gable-cap:83.10 membrane:758.00 membrane-tape:232.00 ridge-large:478.38 ridge-roll-large:287.07 screw-long:309.72 screw-short:103.24 snow-guard:492.96 verge:662.52 wood-nails:72.50")] // the app: 12931.17
    [InlineData("gable", "bilka-clasic-35", "7016", null, null, null, "11603.36", "batten:1577.70 bilka-clasic-35/7016#4350 mm:5300.39 counter-batten:701.20 eave:492.96 gable-cap:83.10 membrane:758.00 membrane-tape:232.00 ridge-large:478.38 ridge-roll-large:287.07 screw-long:361.34 screw-short:103.24 snow-guard:492.96 verge:662.52 wood-nails:72.50")] // the app: 13523.5
    [InlineData("gable", "bilka-clasic-35", "8017", null, null, null, "11603.36", "batten:1577.70 bilka-clasic-35/8017#4350 mm:5300.39 counter-batten:701.20 eave:492.96 gable-cap:83.10 membrane:758.00 membrane-tape:232.00 ridge-large:478.38 ridge-roll-large:287.07 screw-long:361.34 screw-short:103.24 snow-guard:492.96 verge:662.52 wood-nails:72.50")] // the app: 13523.5
    [InlineData("gable", "bilka-clasic-35", "7016", "small", "screws", null, "11747.85", "batten:1577.70 bilka-clasic-35/7016#4350 mm:5300.39 counter-batten:701.20 eave:492.96 gable-cap:83.10 membrane:758.00 membrane-tape:232.00 ridge-roll-small:258.96 ridge-small:411.00 screw-long:361.34 screw-short:103.24 snow-guard:492.96 verge:662.52 wood-screws:312.48")] // the app: 13667.99
    [InlineData("gable", "bilka-retro-panel", "8019", null, null, null, "15456.03", "bilka-retro-panel/8019#4100 mm:6350.60 board:4153.50 butyl-tape:432.00 counter-batten:701.20 eave-retro:662.52 gable-cap:83.10 membrane:758.00 membrane-tape:232.00 ridge-retro:380.76 ridge-support:264.96 screw-flat:197.37 screw-sealed:103.24 snow-guard-retro:492.96 verge-retro:571.32 wood-nails:72.50")] // the app: 17523.17
    [InlineData("gable", "bilka-retro-panel", "8019", null, null, "attached", "12809.51", "batten:1577.70 bilka-retro-panel/8019#4100 mm:6350.60 butyl-tape:432.00 counter-batten:701.20 eave-retro:662.52 gable-cap:83.10 membrane:758.00 membrane-tape:232.00 ridge-retro:380.76 ridge-support:264.96 screw-flat:126.65 screw-sealed:103.24 snow-guard-retro:492.96 verge-retro:571.32 wood-nails:72.50")] // the app: 14876.65
    [InlineData("lhip", "bilka-balcanic", "8017", null, null, null, "21662.03", "batten:2892.45 bilka-balcanic/8017/1180:1197.65 bilka-balcanic/8017/2230:6306.57 bilka-balcanic/8017/2930:1871.97 counter-batten:1192.04 eave:1191.32 hip-end-large:738.65 hip-large:1195.95 membrane:1137.00 membrane-tape:464.00 ridge-large:558.11 ridge-roll-large:861.21 screw-long:567.82 screw-short:103.24 snow-guard:985.92 valley:244.14 valley-seal:45.24 wood-nails:108.75")] // the app: 27747.95
    [InlineData("lhip", "bilka-clasic-35", "7016", null, null, null, "22517.86", "batten:2892.45 bilka-clasic-35/7016#1200 mm:584.87 bilka-clasic-35/7016#2600 mm:1900.83 bilka-clasic-35/7016#4000 mm:2924.35 bilka-clasic-35/7016#4350 mm:4770.35 counter-batten:1192.04 eave:1191.32 hip-end-large:738.65 hip-large:1195.95 membrane:1137.00 membrane-tape:464.00 ridge-large:558.11 ridge-roll-large:861.21 screw-long:619.44 screw-short:103.24 snow-guard:985.92 valley:244.14 valley-seal:45.24 wood-nails:108.75")] // the app: 28603.78
    [InlineData("lhip", "bilka-retro-panel", "8019", null, null, null, "29107.32", "bilka-retro-panel/8019#1300 mm:549.17 bilka-retro-panel/8019#1900 mm:802.63 bilka-retro-panel/8019#2500 mm:1056.09 bilka-retro-panel/8019#3100 mm:1309.55 bilka-retro-panel/8019#3700 mm:1563.01 bilka-retro-panel/8019#4300 mm:6357.64 bilka-retro-panel/8019#700 mm:295.70 board:7455.00 butyl-tape:1008.00 counter-batten:1192.04 eave-retro:1601.09 hip-retro:951.90 lap-small:66.30 membrane:1137.00 membrane-tape:464.00 ridge-retro:444.22 ridge-support:971.52 screw-flat:406.30 screw-sealed:154.86 snow-guard-retro:985.92 valley-retro:190.38 wood-nails:145.00")] // the app: 35340.24
    [InlineData("lhip", "bilka-clasic-35", "7016", "small", "screws", null, "22574.07", "batten:2892.45 bilka-clasic-35/7016#1200 mm:584.87 bilka-clasic-35/7016#2600 mm:1900.83 bilka-clasic-35/7016#4000 mm:2924.35 bilka-clasic-35/7016#4350 mm:4770.35 counter-batten:1192.04 eave:1191.32 hip-end-small:675.00 hip-small:1027.50 membrane:1137.00 membrane-tape:464.00 ridge-roll-small:776.88 ridge-small:479.50 screw-long:619.44 screw-short:103.24 snow-guard:985.92 valley:244.14 valley-seal:45.24 wood-screws:560.00")] // the app: 28659.99
    [InlineData("wallgable", "bilka-balcanic", "8017", null, null, null, "10601.37", "batten:1472.52 bilka-balcanic/8017/2230:4759.68 counter-batten:648.61 eave:492.96 gable-cap:41.55 membrane:758.00 membrane-tape:232.00 ridge-large:478.38 ridge-roll-large:287.07 screw-long:309.72 screw-short:103.24 snow-guard:451.88 verge:331.26 wall-side:162.00 wood-nails:72.50")] // the app: 12496.89
    [InlineData("wallgable", "bilka-clasic-35", "7016", null, null, null, "10612.04", "batten:1472.52 bilka-clasic-35/7016#4350 mm:4770.35 counter-batten:648.61 eave:492.96 gable-cap:41.55 membrane:758.00 membrane-tape:232.00 ridge-large:478.38 ridge-roll-large:287.07 screw-long:309.72 screw-short:103.24 snow-guard:451.88 verge:331.26 wall-side:162.00 wood-nails:72.50")] // the app: 12507.56
    [InlineData("wallgable", "bilka-retro-panel", "8019", null, null, null, "14500.05", "bilka-retro-panel/8019#4100 mm:5773.28 board:3940.50 butyl-tape:432.00 counter-batten:648.61 eave-retro:662.52 gable-cap:41.55 membrane:758.00 membrane-tape:232.00 ridge-retro:380.76 ridge-support:264.96 screw-flat:187.51 screw-sealed:103.24 snow-guard-retro:451.88 verge-retro:285.66 wall-side-retro:265.08 wood-nails:72.50")] // the app: 16542.57
    [InlineData("leanto", "bilka-balcanic", "8017", null, null, null, "8617.55", "batten:1314.75 bilka-balcanic/8017/2230:2379.84 bilka-balcanic/8017/2930:1559.98 counter-batten:596.02 eave:246.48 membrane:758.00 membrane-tape:232.00 screw-long:309.72 screw-short:51.62 snow-guard:492.96 verge:441.68 wall-top:162.00 wood-nails:72.50")] // the app: 9844.55
    [InlineData("leanto", "bilka-clasic-35", "7016", null, null, null, "8942.41", "batten:1314.75 bilka-clasic-35/7016#7000 mm:4264.68 counter-batten:596.02 eave:246.48 membrane:758.00 membrane-tape:232.00 screw-long:309.72 screw-short:51.62 snow-guard:492.96 verge:441.68 wall-top:162.00 wood-nails:72.50")] // the app: 10169.41
    [InlineData("leanto", "bilka-retro-panel", "8019", null, null, null, "12780.52", "bilka-retro-panel/8019#7000 mm:5421.25 board:3479.00 butyl-tape:288.00 counter-batten:596.02 eave-retro:331.26 membrane:758.00 membrane-tape:232.00 ridge-support:132.48 screw-flat:161.67 screw-sealed:103.24 snow-guard-retro:492.96 verge-retro:380.88 wall-top-retro:331.26 wood-nails:72.50")] // the app: 14007.52
    public void The_estimate_is_the_Panotaj_Bilka_apps_Deviz_RON(string roof, string profile, string color, string? ridge, string? fixing, string? support, string subtotal, string lines)
    {
        var (f, id) = Make(roof);
        f.Run("cost.addPriceList", new JsonObject { ["template"] = Template });
        Panels(f, id, profile, color, ridge, fixing, support);
        // The Panel layout's Cost tab: each line priced, the subtotal of the priced ones.
        var cost = Layout(f, id)["cost"]!.AsObject();
        Assert.True((bool)cost["available"]!);
        Assert.True((bool)cost["indicative"]!);
        var tab = cost["lines"]!.AsArray().Where(l => l!["amount"] != null)
            .Select(l => (Key: (string?)l!["variant"] is { } v ? $"{(string)l["code"]!}#{v}" : (string)l["code"]!, Value: (double)l["amount"]!))
            .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}:{Amount(x.Value)}");
        Assert.Equal(lines, string.Join(" ", tab));
        Assert.Equal(subtotal, Amount((double)cost["subtotal"]!["RON"]!));
        // The estimate of the cost module, from the roof's Lapin_RoofPanelQuantities: the same lines under Roof, the same sum.
        f.Run("cost.createEstimate", new JsonObject());
        var estimate = (JsonObject)f.Session.Query("cost.estimate", new JsonObject())!;
        var (estimated, sum) = RoofLines(estimate);
        Assert.Equal(lines, estimated);
        Assert.Equal(subtotal, Amount(sum));
        Assert.Equal(subtotal, Amount((double)estimate["chapters"]!.AsArray().Single(c => (string)c!["name"]! == "Roof")!["m"]!));
        // The roof is priced: not listed among what has no price.
        Assert.DoesNotContain(estimate["unpriced"]!.AsArray(), u => (string)u!["class"]! == IfcClass.Roof);
    }

    [Fact]
    public void The_Bilka_list_is_a_template_added_only_when_asked_indicative_with_its_sources()
    {
        var (f, id) = Make("gable");
        Panels(f, id, "bilka-clasic-35", "7016");
        // Nothing is loaded by itself: the roof has panels, the project no price list.
        Assert.Empty(f.Model.OfClass(PriceLists.Class));
        var templates = f.Session.Query("cost.templates", new JsonObject())!.AsArray();
        var bilka = templates.Single(t => (string)t!["id"]! == Template)!;
        Assert.Equal("Bilka – Dedeman (indicative, 24.11.2026)", (string)bilka["name"]!);
        Assert.Equal(79, (int)bilka["rows"]!);
        Assert.Equal("2026-11-24", (string)bilka["checked"]!);
        Assert.True((bool)bilka["indicative"]!);
        Assert.Contains(templates, t => (string)t!["id"]! == "lapin");
        // Asked: one undo step, an indicative list, each row with its source, page and date; the merchant only in the source.
        f.Run("cost.addPriceList", new JsonObject { ["template"] = Template });
        var list = PriceLists.All(f.Model).Single();
        Assert.True(list.Indicative);
        Assert.Equal("RON", list.Currency);
        Assert.Equal("Bilka – Dedeman (indicative, 24.11.2026)", list.Name);
        Assert.All(list.Rows, r => Assert.True(r.Source is { Length: > 0 } && r.Url!.StartsWith("https://", StringComparison.Ordinal) && r.Checked == "2026-11-24", r.Code));
        Assert.All(list.Rows, r => Assert.EndsWith("(indicative)", r.Name));
        Assert.All(list.Rows, r => Assert.DoesNotContain("Dedeman", r.Name));
        var tile = list.Articles["bilka-balcanic/8017/2930"];
        Assert.Equal((45.12, "m²", "Dedeman 6037244"), (tile.Material, tile.Unit, tile.Source));
        // The tile per m² by colour: Clasic 7016 and 8017 alike, Retro 8019.
        Assert.Equal(50.77, list.Articles["bilka-clasic-35/7016"].Material);
        Assert.Equal(50.77, list.Articles["bilka-clasic-35/8017"].Material);
        Assert.Equal(64.95, list.Articles["bilka-retro-panel/8019"].Material);
        Assert.Equal("eAcoperiș", list.Articles["hip-end-large"].Source);
        // Each priced line of the Cost tab names where its price is from.
        var line = Layout(f, id)["cost"]!["lines"]!.AsArray().First(l => (string)l!["code"]! == "bilka-clasic-35/7016")!;
        Assert.Equal(("Dedeman 6061465", "2026-11-24", "m²"), ((string)line["source"]!, (string)line["checked"]!, (string)line["priceUnit"]!));
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(PriceLists.Class));
        Assert.Throws<CommandException>(() => f.Run("cost.addPriceList", new JsonObject { ["template"] = "nowhere" }));
    }

    [Fact]
    public void Without_a_price_list_the_roof_has_its_quantities_and_no_prices()
    {
        var (f, id) = Make("lhip");
        Panels(f, id, "bilka-clasic-35", "7016");
        // The roof's Lapin_RoofPanelQuantities: m² of sheet a length, the accessories in their units, by their codes.
        var set = f.Session.Quantities(id).Single(s => s.Name == PanelCost.SetName);
        Assert.Equal(ArticleQuantities.Part, set.Part);
        var q = set.Quantities.ToDictionary(x => x.Name);
        Assert.Equal(93.96, q["bilka-clasic-35/7016#4350 mm"].Value, 9);
        Assert.Equal("AREA", q["bilka-clasic-35/7016#4350 mm"].Kind);
        Assert.Equal(15, q["hip-large"].Value);
        Assert.Equal(12, q["screw-long"].Value);
        // What is cut from flat sheet is not bought: not in the quantities.
        Assert.DoesNotContain("ridge-nodes", q.Keys);
        var cost = Layout(f, id)["cost"]!.AsObject();
        Assert.True((bool)cost["available"]!);
        Assert.Equal(0, (int)cost["priced"]!);
        Assert.Empty(cost["subtotal"]!.AsObject());
        Assert.Equal(set.Quantities.Count, (int)cost["toQuote"]!);
        Assert.Equal(1, (int)cost["fromSheet"]!);
        // The joints between the ridge caps, apart (the app's 15 on this roof).
        Assert.Equal(15, (int)cost["joints"]!);
        // The estimate has the lines, to quote: no value, their codes missing from the lists.
        f.Run("cost.createEstimate", new JsonObject());
        var estimate = (JsonObject)f.Session.Query("cost.estimate", new JsonObject())!;
        var roof = estimate["chapters"]!.AsArray().Single(c => (string)c!["name"]! == "Roof")!["lines"]!.AsArray();
        Assert.Equal(set.Quantities.Count, roof.Count);
        Assert.All(roof, l => Assert.False((bool)l!["priced"]!));
        Assert.Contains(estimate["missing"]!.AsArray(), x => (string)x! == "hip-large");
        Assert.Equal(93.96, (double)roof.Single(l => ((string)l!["key"]!).EndsWith("4350 mm", StringComparison.Ordinal))!["qty"]!, 9);
        // Another colour is another article, live: the preview and the roof alike.
        var preview = (JsonObject)f.Session.Query("roofpanels.layout", new JsonObject { ["id"] = id, ["profile"] = "bilka-clasic-35", ["color"] = "8017" })!;
        Assert.Contains(preview["cost"]!["lines"]!.AsArray(), l => (string)l!["code"]! == "bilka-clasic-35/8017");
        Panels(f, id, "bilka-clasic-35", "8017", fixing: "screws");
        var names = f.Session.Quantities(id).Single(s => s.Name == PanelCost.SetName).Quantities.Select(x => x.Name).ToList();
        Assert.Contains("bilka-clasic-35/8017#4350 mm", names);
        Assert.Contains("wood-screws", names);
        Assert.DoesNotContain("wood-nails", names);
        // No profile, no panel quantities.
        f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = null });
        Assert.DoesNotContain(f.Session.Quantities(id), s => s.Name == PanelCost.SetName);
    }

    [Fact]
    public void Retro_Panels_flat_sheet_pieces_are_not_bought_and_your_own_prices_come_first()
    {
        var (f, id) = Make("lhip");
        f.Run("cost.addPriceList", new JsonObject { ["template"] = Template });
        Panels(f, id, "bilka-retro-panel", null);
        var cost = Layout(f, id)["cost"]!.AsObject();
        var sheet = cost["lines"]!.AsArray().Where(l => (string?)l!["madeFrom"] == "flatSheet").Select(l => (string)l!["code"]!).Order().ToList();
        Assert.Equal(["hip-end-retro", "ridge-nodes", "valley-clips"], sheet);
        Assert.Equal(0, (int)cost["toQuote"]!);
        Assert.Equal("29107.32", Amount((double)cost["subtotal"]!["RON"]!));
        // A list of your own, named first by the estimate, prices what it has; the template the rest.
        var mine = (string)f.Run("cost.addPriceList", new JsonObject
        {
            ["name"] = "My roofer", ["currency"] = "RON",
            ["rows"] = new JsonArray(new JsonObject { ["code"] = "board", ["name"] = "Board 24 × 150 × 4000", ["unit"] = "board 4 m", ["m"] = 30, ["source"] = "Local timber yard" }),
        })!["id"]!;
        var template = PriceLists.All(f.Model).Single(l => l.Name.StartsWith("Bilka", StringComparison.Ordinal)).Id;
        f.Run("cost.createEstimate", new JsonObject());
        f.Run("cost.updateEstimate", new JsonObject { ["priceLists"] = new JsonArray(mine, template) });
        var board = Layout(f, id)["cost"]!["lines"]!.AsArray().Single(l => (string)l!["code"]! == "board")!;
        Assert.Equal((30.0, 6300.0, "Local timber yard", false), ((double)board["price"]!, (double)board["amount"]!, (string)board["source"]!, (bool)board["indicative"]!));
        Assert.Equal(Amount(29107.32 - 7455 + 6300), Amount((double)Layout(f, id)["cost"]!["subtotal"]!["RON"]!));
    }

    [Fact]
    public void Without_the_cost_module_the_layout_says_so()
    {
        var (f, id) = RoofPanelTakeoffTests.Make("gable");
        Panels(f, id, "bilka-balcanic", null);
        var cost = Layout(f, id)["cost"]!.AsObject();
        Assert.False((bool)cost["available"]!);
        Assert.Equal(0, (int)cost["priced"]!);
        // The quantities are the roof's all the same.
        Assert.Contains(f.Session.Quantities(id), s => s.Name == PanelCost.SetName);
    }

    sealed class Plain(bool indicative) : IPriceListTemplate
    {
        public string Id => "plain";
        public string Name => "Plain";
        public string Note => "";
        public JsonObject Read() => new() { ["format"] = "lapin-price-list", ["version"] = 1, ["indicative"] = indicative, ["rows"] = new JsonArray(new JsonObject { ["c"] = "x", ["u"] = "piece", ["m"] = 1 }) };
    }

    [Fact]
    public void A_template_that_is_not_marked_indicative_is_refused_and_a_tables_sources_are_read()
    {
        Assert.Throws<CommandException>(() => PriceLists.FromTemplate(new Plain(false)));
        Assert.Single(PriceLists.FromTemplate(new Plain(true)).Rows);
        // A table of your own with its sources: the columns guessed from Romanian headings.
        var table = PriceLists.Csv("Cod;Denumire;UM;Preț;Sursa;Link;Data\nridge-large;Coamă mare;piece 2 m;75,5;Magazin X;https://x.ro/coama;2026-11-20\n");
        var (header, map) = PriceLists.Guess(table);
        var row = PriceLists.Import(table, header, map).Rows.Single();
        Assert.Equal(("ridge-large", 75.5, "Magazin X", "https://x.ro/coama", "2026-11-20"), (row.Code, row.Material, row.Source, row.Url, row.Checked));
    }
}
