using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Cost;

/// <summary>
/// The estimate in the page Lapin exports for the browser (decision 482): its chapters, each heading its lines (code,
/// name, unit, quantity, unit price, value, the elements measured), and the totals after Romanian practice, as worked
/// out from the model when the page is written. Marked indicative when it prices by Lapin's own values, as the app marks
/// them; the project's without an estimate puts no table.
/// </summary>
public sealed class CostPageTable : IPageTables
{
    public PageTable? Table(IQueryContext q)
    {
        if (Costing.EstimateOf(q.Model) is not { } estimate) return null;
        var r = Costing.Compute(q, estimate);
        if (!r.Lines.Any()) return null;
        var indicative = Costing.ListsFor(q.Model, r.Settings).Any(l => l.Indicative);
        var rows = new List<PageRow>();
        foreach (var c in r.Chapters)
        {
            rows.Add(new PageRow([c.Name, null, null, null, null, c.Sums.Direct], [.. c.Lines.SelectMany(l => l.Elements).Distinct()], Heading: true));
            foreach (var l in c.Lines)
                rows.Add(new PageRow([l.Code, l.Name, l.Unit, l.Qty, l.UnitPrice, l.Value], [.. l.Elements]));
        }
        var t = r.Totals;
        var s = r.Settings;
        List<PageTotal> totals =
        [
            new("Direct costs", t.DirectTotal),
            new("Overheads", t.Overheads, s.Overheads),
            new("Profit", t.Profit, s.Profit),
            new("Total without VAT", t.Net),
            new("VAT", t.Vat, s.Vat),
            new("Total", t.Total),
        ];
        return new PageTable("cost", "Estimate",
            "Worked out from the model when the page was exported.", indicative,
            [
                new("code", "Code"), new("name", "Description"), new("unit", "Unit"),
                new("qty", "Quantity", "number"), new("unitPrice", "Unit price", "money"), new("value", "Value", "money"),
            ], rows, totals, s.Currency, estimate.Name);
    }
}
