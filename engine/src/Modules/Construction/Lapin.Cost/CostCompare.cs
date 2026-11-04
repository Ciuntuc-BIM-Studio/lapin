using System.Globalization;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Cost;

/// <summary>
/// Two versions' estimates compared (docs/plan-4d5d.md, stage 7: Manage → Versions → Compare says what changed in cost):
/// the total with VAT and the direct costs, from and to, and the difference; then each chapter whose direct costs changed,
/// and each line added, removed or changed (its quantity or its value). Each side's estimate is worked out from that
/// version's model, by its own articles and price lists.
/// </summary>
public sealed class CostCompare : IVersionCompare
{
    public bool Measures => true;

    /// <summary>Without the quantities there is no estimate to compare.</summary>
    public VersionDiffSection? Compare(IModelView a, IModelView b) => null;

    static string M(double v) => v.ToString("N2", CultureInfo.InvariantCulture).Replace(",", " ");

    static string Signed(double v) => (v > 0 ? "+" : v < 0 ? "−" : "") + M(Math.Abs(v));

    public VersionDiffSection? Compare(IQueryContext a, IQueryContext b)
    {
        var ea = Costing.EstimateOf(a.Model);
        var eb = Costing.EstimateOf(b.Model);
        if (ea == null && eb == null) return null;
        var ra = ea != null ? Costing.Compute(a, ea) : null;
        var rb = eb != null ? Costing.Compute(b, eb) : null;
        var cur = rb?.Settings.Currency ?? ra!.Settings.Currency;
        var ta = ra?.Totals.Total ?? 0;
        var tb = rb?.Totals.Total ?? 0;
        var da = ra?.Totals.Direct.Direct ?? 0;
        var db = rb?.Totals.Direct.Direct ?? 0;
        var facts = new List<(string, string?, string?)>();
        if (ta != tb)
        {
            facts.Add(("Total with VAT", ra != null ? $"{M(ta)} {cur}" : null, rb != null ? $"{M(tb)} {cur}" : null));
            facts.Add(("Direct costs", ra != null ? M(da) : null, rb != null ? M(db) : null));
            var pct = ta != 0 ? $" ({(tb - ta) / ta * 100:+0.0;−0.0} %)" : "";
            facts.Add(("Difference", null, $"{Signed(Money.Round(tb - ta))} {cur}{pct}"));
        }
        var rows = new List<VersionDiffRow>();
        var chA = ra?.Chapters.ToDictionary(c => c.Name, c => c.Sums.Direct) ?? [];
        var chB = rb?.Chapters.ToDictionary(c => c.Name, c => c.Sums.Direct) ?? [];
        foreach (var name in chB.Keys.Concat(chA.Keys).Distinct())
        {
            var x = chA.GetValueOrDefault(name);
            var y = chB.GetValueOrDefault(name);
            if (x != y) rows.Add(new VersionDiffRow(null, name, "chapter", chA.ContainsKey(name) ? M(x) : null, chB.ContainsKey(name) ? $"{M(y)} ({Signed(Money.Round(y - x))})" : null));
        }
        var la = ra?.Lines.ToDictionary(l => l.Key) ?? [];
        var lb = rb?.Lines.ToDictionary(l => l.Key) ?? [];
        string Name(EstimateLine l) => (l.Code.Length > 0 ? $"{l.Code} " : "") + l.Name;
        string What(EstimateLine l) => $"{l.Qty.ToString("0.###", CultureInfo.InvariantCulture)} {l.Unit} · {M(l.Value)}";
        foreach (var l in lb.Values)
        {
            if (!la.TryGetValue(l.Key, out var was)) { rows.Add(new VersionDiffRow(null, Name(l), "added", null, What(l))); continue; }
            if (was.Qty != l.Qty || was.Value != l.Value) rows.Add(new VersionDiffRow(null, Name(l), "cost", What(was), $"{What(l)} ({Signed(Money.Round(l.Value - was.Value))})"));
        }
        foreach (var l in la.Values.Where(l => !lb.ContainsKey(l.Key))) rows.Add(new VersionDiffRow(null, Name(l), "removed", What(l)));
        if (facts.Count == 0 && rows.Count == 0) return null;
        return new VersionDiffSection("estimate", "Estimate", facts, rows);
    }
}
