using System.Reflection;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.RoofPanels;

/// <summary>
/// The roof panels' cost link (docs/plan-roof-panels.md §2.7, stage S4, decision 859): the lines a roof's panels are bought
/// by — the panels of each length (m² of sheet bought: the nominal length by the whole width, how they are priced), the
/// accessories, fixings, battens or boards and membrane of the takeoff — each keyed by its article code. They reach the
/// estimate as the roof's <c>Lapin_RoofPanelQuantities</c> (<see cref="Quantities"/>), and the Panel layout's Cost tab prices
/// them from the project's price lists (<see cref="Estimate"/>), as the Panotaj Bilka app's "Deviz RON" sheet does: an amount
/// is the quantity by the unit price rounded half up to the ban, a line with no price is to quote, the subtotal is only the
/// priced lines'. The module has no prices; what is made from flat sheet is not bought (its sheets are, stage S5), the joints
/// between ridge caps are operations, listed apart. Stage S5 adds the flat sheets and the rainwater system's lines, so the
/// subtotal is the app's whole <c>priced_subtotal_ron</c>. Worked out on every ask, never stored.
/// </summary>
public static class PanelCost
{
    public const string SetName = "Lapin_RoofPanelQuantities";

    /// <summary>
    /// A line to buy: its article code (and the variant that tells two lines of a code apart: a panel's length), text, group
    /// (<c>panels</c>, <c>support</c>, <c>fixings</c>, <c>flashings</c>), quantity in its unit, the pieces it is (panels), whether
    /// it is made from flat sheet rather than bought, and a note.
    /// </summary>
    public sealed record Line(string Code, string? Variant, string Name, string Group, decimal Quantity, string Unit, int? Pieces, string? MadeFrom, string Note);

    /// <summary>A line with its price (null: none in the project's lists), its amount (null: not priced) and whether the price's unit is another than the line's.</summary>
    public sealed record Priced(Line Line, ArticlePrice? Price, decimal? Amount, bool UnitDiffers);

    /// <summary>
    /// The roof's panels priced: the lines, the joints between ridge and hip caps (operations, their material in the caps), the
    /// subtotal of the priced lines by currency, how many lines are priced, to quote, cut from flat sheet; <see cref="Available"/>:
    /// whether the product has a cost module at all.
    /// </summary>
    public sealed record Result(List<Priced> Lines, int Joints, SortedDictionary<string, decimal> Subtotal, int PricedCount, int ToQuote, int FromSheet, bool Indicative, bool Available);

    /// <summary>The roof's lines, panels first (shortest first), then the takeoff's in its order.</summary>
    public static List<Line> Lines(RoofPanels.Layout l)
    {
        var p = l.Profile;
        var color = p.Colors.FirstOrDefault(c => c.Code == l.Color) ?? p.Colors.FirstOrDefault();
        var o = new List<Line>();
        foreach (var (length, count) in l.Purchase)
        {
            // decimal throughout: the app prices Decimal(pieces × length × width / 10⁶) by the m².
            var area = (decimal)count * length * p.GrossWidth / 1_000_000m;
            var name = $"{p.Name}{(color != null ? $", {color.Name}" : "")}, {length} × {p.GrossWidth} mm";
            o.Add(new Line(p.ArticleCode(l.Color, length), p.ArticleByLength ? null : $"{length} mm", name, "panels", area, "m²", count, null,
                $"{count} panel(s) of {length} mm: priced per m² of sheet bought (the nominal length by the {p.GrossWidth} mm width)."));
        }
        foreach (var i in l.Takeoff.Items)
            o.Add(new Line(i.Code, null, i.Name, i.Group, (decimal)i.Quantity, i.Unit, null, i.MadeFrom, i.Note));
        // Stage S5: the flat sheet the flashings are cut from (the sheets the roof's own combination takes), the rainwater system.
        if (l.Sheets is { Current.Nesting.Sheets: > 0 } sh)
            o.Add(new Line(sh.Sheet.Code, null, sh.Sheet.Name, "flashings", sh.Current.Nesting.Sheets, "sheet", null, null,
                $"{sh.Current.Nesting.Pieces.Count} piece(s) laid whole on {sh.Data.Width} × {sh.Data.Height} mm sheets: check the templates before cutting."));
        foreach (var i in l.Drainage?.Items ?? [])
            o.Add(new Line(i.Code, null, i.Name, i.Group, (decimal)i.Quantity, i.Unit, null, i.MadeFrom, i.Note));
        return o;
    }

    /// <summary>The joints between the ridge and hip caps (the app's „Îmbinări drepte între coame”): an operation by the lap, the material in the caps.</summary>
    public static int Joints(RoofPanels.Layout l) =>
        l.Edges.Where(e => e.Kind is EdgeKinds.Ridge or EdgeKinds.Hip)
            .Sum(e => Math.Max(0, RoofPanels.PieceCount(e.Length, l.Profile.AccessoryLength / 1000.0, l.Profile.AccessoryLap(e.Kind)) - 1));

    /// <summary>The units a price and a line are in, compared as words (case, dots and diacritics aside).</summary>
    static string Fold(string u) => string.Join(' ', u.ToLowerInvariant().Replace('.', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static Result Estimate(RoofPanels.Layout l, IArticlePrices? prices, IModelView m)
    {
        var priced = new List<Priced>();
        var subtotal = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
        int count = 0, quote = 0, sheet = 0;
        var indicative = false;
        foreach (var line in Lines(l))
        {
            if (line.MadeFrom != null) { sheet++; priced.Add(new Priced(line, null, null, false)); continue; }
            var price = prices?.Price(m, line.Code);
            if (price == null) { quote++; priced.Add(new Priced(line, null, null, false)); continue; }
            // ROUND_HALF_UP to the ban, of the quantity by the price as written (the app's money()).
            var amount = Math.Round(line.Quantity * (decimal)price.Price, 2, MidpointRounding.AwayFromZero);
            subtotal[price.Currency] = subtotal.GetValueOrDefault(price.Currency) + amount;
            indicative |= price.Indicative;
            count++;
            priced.Add(new Priced(line, price, amount, price.Unit.Length > 0 && Fold(price.Unit) != Fold(line.Unit)));
        }
        return new Result(priced, Joints(l), subtotal, count, quote, sheet, indicative, prices != null);
    }

    /// <summary>The Cost tab's table, as the interface reads it.</summary>
    public static JsonObject Json(Result r) => new()
    {
        ["available"] = r.Available,
        ["lines"] = new JsonArray([.. r.Lines.Select(x => (JsonNode)new JsonObject
        {
            ["code"] = x.Line.Code, ["variant"] = x.Line.Variant, ["name"] = x.Line.Name, ["group"] = x.Line.Group,
            ["quantity"] = (double)Math.Round(x.Line.Quantity, 6), ["unit"] = x.Line.Unit, ["pieces"] = x.Line.Pieces, ["madeFrom"] = x.Line.MadeFrom, ["note"] = x.Line.Note,
            ["price"] = x.Price?.Price, ["priceUnit"] = x.Price?.Unit, ["currency"] = x.Price?.Currency, ["amount"] = x.Amount is { } a ? (double)a : null,
            ["source"] = x.Price?.Source, ["url"] = x.Price?.Url, ["checked"] = x.Price?.Checked, ["list"] = x.Price?.List, ["indicative"] = x.Price?.Indicative,
            ["unitDiffers"] = x.UnitDiffers ? true : null,
        })]),
        ["joints"] = r.Joints,
        ["subtotal"] = new JsonObject([.. r.Subtotal.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)(double)kv.Value))]),
        ["priced"] = r.PricedCount, ["toQuote"] = r.ToQuote, ["fromSheet"] = r.FromSheet, ["indicative"] = r.Indicative,
    };

    /// <summary>
    /// The roof's <c>Lapin_RoofPanelQuantities</c> (<see cref="ArticleQuantities"/>): a quantity a line bought, named by its article
    /// code (and variant), in its article's unit (m² of sheet for the panels), labelled with its text and unit. What is cut from
    /// flat sheet is left out.
    /// </summary>
    public static QuantitySet? Quantities(RoofPanels.Layout l)
    {
        var q = Lines(l).Where(x => x.MadeFrom == null && x.Quantity > 0)
            .Select(x => new Quantity(ArticleQuantities.Name(x.Code, x.Variant), (double)x.Quantity, x.Unit == "m²" ? "AREA" : "COUNT", $"{x.Name}, {x.Unit}")).ToList();
        return q.Count == 0 ? null : new QuantitySet(SetName, q, ArticleQuantities.Part, "Roof panels");
    }

    /// <summary>The roof panels' quantities on the roof (stage S4), for every module that reads a roof's quantities: schedules, formulas, IFC, the estimate.</summary>
    public sealed class Filter : IQuantityFilter
    {
        public IEnumerable<QuantitySet> Add(Entity e, IModelView m)
        {
            if (e.Class != IfcClass.Roof || RoofPanels.ChoiceOf(e) == null || RoofPanels.Of(e, m) is not { } l) return [];
            return Quantities(l) is { } s ? [s] : [];
        }
    }

    /// <summary>
    /// The Bilka price list (decision 859, the owner's answer to the plan's question 2): the Panotaj Bilka app's catalogue — public
    /// prices read on 24.11.2026, mostly at Dedeman — as an indicative template under the profiles' and accessories' codes, each
    /// row with its source (the merchant and its product code), its page and the day it was read. Added to a project only when
    /// the user asks (Price lists → Templates); never loaded by itself.
    /// </summary>
    public sealed class BilkaTemplate : IPriceListTemplate
    {
        public const string TemplateId = "bilka-dedeman-2026-11-24";
        public string Id => TemplateId;
        public string Name => "Bilka – Dedeman (indicative, 24.11.2026)";
        public string Note => "The Bilka roof panels' prices read on 24.11.2026, mostly at Dedeman: indicative, check them when ordering.";

        static readonly Lazy<string> Text = new(() =>
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Lapin.RoofPanels.bilka-dedeman-2026-11-24.lapinprices")
                ?? throw new InvalidOperationException("The Bilka price list is not embedded.");
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        });

        public JsonObject Read() => (JsonObject)JsonNode.Parse(Text.Value)!;
    }
}
