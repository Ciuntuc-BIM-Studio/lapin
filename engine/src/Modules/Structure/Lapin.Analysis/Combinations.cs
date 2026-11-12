using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Platform.Analysis;

namespace Lapin.Analysis;

/// <summary>
/// The combinations of the load cases (docs/plan-fem.md §8.5), derived on every ask, after EN 1990 Annex A1 (buildings) with
/// its recommended values, indicative: the ultimate limit state's fundamental combination (6.10) with each variable case as the
/// leading one, Σ 1.35 Gk + 1.5 Qk,1 + Σ 1.5 ψ0,i Qk,i; the permanent cases alone, 1.35 Gk; with wind, 1.0 Gk + 1.5 W (the
/// permanent actions favourable); the serviceability characteristic (Σ Gk + Qk,1 + Σ ψ0,i Qk,i), frequent (Σ Gk + ψ1,1 Qk,1 +
/// Σ ψ2,i Qk,i) and quasi-permanent (Σ Gk + Σ ψ2,i Qk,i) combinations. Only the cases holding something take part; a factor of
/// zero is left out. Keys are stable: uls/&lt;caseId&gt;, uls/permanent, uls/wind-uplift, slsc/&lt;caseId&gt;, slsf/&lt;caseId&gt;, slsq.
/// </summary>
public static class Combinations
{
    public const double GammaG = 1.35, GammaQ = 1.5, GammaGFavourable = 1.0;

    public sealed record Factor(LoadCase Case, double Value);
    public sealed record Combination(string Key, string Kind, string Name, IReadOnlyList<Factor> Factors);

    static double R(double v) => Math.Round(v, 4);

    /// <summary>A factor as a name writes it: none when it is 1 ("G1"), else its digits ("1.35 G1").</summary>
    static string Term(Factor f) => Math.Abs(f.Value - 1) < 1e-9 ? f.Case.Code : $"{R(f.Value).ToString("0.####", CultureInfo.InvariantCulture)} {f.Case.Code}";

    static Combination Make(string key, string kind, IEnumerable<Factor> factors)
    {
        var list = factors.Where(f => Math.Abs(f.Value) > 1e-12).Select(f => f with { Value = R(f.Value) }).ToList();
        return new Combination(key, kind, string.Join(" + ", list.Select(Term)), list);
    }

    public static (List<Combination> Combinations, List<string> Notes) Of(LoadsReport report)
    {
        var notes = new List<string>();
        var used = new List<LoadCase>();
        foreach (var c in report.Cases)
        {
            if (report.Empty(c)) notes.Add($"„{c.Code}” holds nothing: left out of the combinations.");
            else used.Add(c);
        }
        var g = used.Where(c => c.Permanent).ToList();
        var q = used.Where(c => c.Variable).ToList();
        foreach (var c in used.Where(c => !c.Permanent && !c.Variable))
            notes.Add($"„{c.Code}” is an accidental action: the accidental combination is not made yet.");
        foreach (var c in q.Where(c => c.OwnPsi == null && Categories.TableOf(c.Category) == null))
            notes.Add($"„{c.Code}” has no category: its ψ are category A's (0.7, 0.5, 0.3).");

        var o = new List<Combination>();
        foreach (var lead in q)
            o.Add(Make($"uls/{lead.Id}", "uls", g.Select(c => new Factor(c, GammaG))
                .Concat(q.Select(c => new Factor(c, c.Id == lead.Id ? GammaQ : GammaQ * c.Psi.Psi0)))));
        if (g.Count > 0) o.Add(Make("uls/permanent", "uls", g.Select(c => new Factor(c, GammaG))));
        if (q.FirstOrDefault(c => c.Wind) is { } wind)
            o.Add(Make("uls/wind-uplift", "uls", g.Select(c => new Factor(c, GammaGFavourable)).Append(new Factor(wind, GammaQ))));
        foreach (var lead in q)
            o.Add(Make($"slsc/{lead.Id}", "sls-characteristic", g.Select(c => new Factor(c, 1)).Concat(q.Select(c => new Factor(c, c.Id == lead.Id ? 1 : c.Psi.Psi0)))));
        foreach (var lead in q)
            o.Add(Make($"slsf/{lead.Id}", "sls-frequent", g.Select(c => new Factor(c, 1)).Concat(q.Select(c => new Factor(c, c.Id == lead.Id ? c.Psi.Psi1 : c.Psi.Psi2)))));
        if (g.Count > 0 || q.Count > 0)
            o.Add(Make("slsq", "sls-quasi", g.Select(c => new Factor(c, 1)).Concat(q.Select(c => new Factor(c, c.Psi.Psi2)))));
        o.RemoveAll(c => c.Factors.Count == 0);

        notes.Add("Indicative: EN 1990 Annex A1, the recommended values (γG 1.35, γQ 1.5, ψ by Table A1.1, snow for sites up to 1000 m).");
        notes.Add("Not made yet: expressions 6.10a and 6.10b, snow with the imposed load on roofs (EN 1991-1-1 3.3.2), the seismic combination, CR 0-2012's values.");
        return (o, notes);
    }

    public static JsonObject ToJson(Combination c) => new()
    {
        ["key"] = c.Key, ["kind"] = c.Kind, ["name"] = c.Name,
        ["factors"] = new JsonArray(c.Factors.Select(f => (JsonNode)new JsonObject { ["caseId"] = f.Case.Id, ["code"] = f.Case.Code, ["factor"] = f.Value }).ToArray()),
    };

    public static StructuralCombinationData ToData(Combination c) => new(c.Key, c.Kind, c.Name, c.Factors.Select(f => (f.Case.Id, f.Value)).ToList());
}
