using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Analysis.Solver;

namespace Lapin.Analysis.Design;

/// <summary>One value a check shows: its name (a symbol of the norm), the number and its unit.</summary>
public readonly record struct Shown(string Name, double Value, string Unit);

/// <summary>
/// A design check's result (docs/plan-fem.md §12): its key, its name, the norm's clause, the utilisation (effect over
/// resistance; above 1 it fails), the combination it governs in and where along the member (metres from its i end), the values
/// that went into it, and a note (what it leaves out). <see cref="Done"/> false: not verified (a class 4 section, a shape
/// without rules), its utilisation an orientation only.
/// </summary>
public sealed record Check(string Key, string Name, string Clause, double Utilisation, IReadOnlyList<Shown> Values, string? Note = null, bool Done = true)
{
    public string? Combination { get; init; }
    public string? CombinationName { get; init; }
    public double X { get; init; }

    static double R(double v) => double.IsFinite(v) ? Math.Round(v, 4) : 0;

    public JsonObject ToJson() => new()
    {
        ["key"] = Key, ["name"] = Name, ["clause"] = Clause, ["utilisation"] = double.IsFinite(Utilisation) ? Math.Round(Utilisation, 4) : 999,
        ["ok"] = Done && Utilisation <= 1 + 1e-9, ["done"] = Done, ["combination"] = Combination, ["combinationName"] = CombinationName, ["x"] = R(X),
        ["values"] = new JsonArray(Values.Select(v => (JsonNode)new JsonObject { ["name"] = v.Name, ["value"] = R(v.Value), ["unit"] = v.Unit }).ToArray()),
        ["note"] = Note,
    };
}

/// <summary>A combination's (or a seismic bound's) internal forces along a member: its key, its name, the sections (sorted by x) and the member's length.</summary>
public sealed record ForceSet(string Key, string Name, IReadOnlyList<Section> Sections, double L)
{
    /// <summary>The actions in the combination by their duration (timber's kmod): the shortest of them.</summary>
    public string Duration { get; init; } = "medium";
    /// <summary>The seismic design situation's bounds.</summary>
    public bool Seismic { get; init; }

    public Section At(double x)
    {
        var s = Sections;
        if (x <= s[0].X) return s[0];
        for (var i = 1; i < s.Count; i++)
            if (x <= s[i].X)
            {
                var t = (x - s[i - 1].X) / Math.Max(1e-12, s[i].X - s[i - 1].X);
                Section a = s[i - 1], b = s[i];
                return new Section(x, a.N + (b.N - a.N) * t, a.Vy + (b.Vy - a.Vy) * t, a.Vz + (b.Vz - a.Vz) * t, a.T + (b.T - a.T) * t, a.My + (b.My - a.My) * t, a.Mz + (b.Mz - a.Mz) * t);
            }
        return s[^1];
    }

    /// <summary>The largest compression along the member (kN, positive), zero when it is in tension throughout.</summary>
    public double Compression => Math.Max(0, -Sections.Min(s => s.N));
    public double Tension => Math.Max(0, Sections.Max(s => s.N));
    public double MaxAbs(int q) => Sections.Max(s => Math.Abs(s[q]));
}

/// <summary>
/// A bending moment's shape along a member (for C1, Cm, kc, timber's lef): its end values, its interior extreme, whether it is
/// linear (end moments only) and, if not, whether the span load looks uniform or a point load at mid-span (the deviation from
/// the chord at a quarter of the span: 0.75 of the mid-span's for a uniform load, 0.5 for a point load).
/// </summary>
public sealed record MomentShape(double Ma, double Mb, double Ms, bool Linear, bool Uniform, double Max)
{
    /// <summary>ψ: the smaller end moment over the larger, signed (−1 … 1); 1 when both are nought.</summary>
    public double Psi => Math.Abs(Ma) >= Math.Abs(Mb) ? (Math.Abs(Ma) < 1e-12 ? 1 : Mb / Ma) : Ma / Mb;
    public double EndMax => Math.Max(Math.Abs(Ma), Math.Abs(Mb));
    /// <summary>The span moment dominates: the ends nearly nought (a simple span).</summary>
    public bool SimpleSpan => !Linear && EndMax <= 0.05 * Math.Abs(Ms);

    public static MomentShape Of(ForceSet f, int q)
    {
        var l = f.L;
        var n = 21;
        var xs = Enumerable.Range(0, n).Select(i => l * i / (n - 1)).ToArray();
        var ms = xs.Select(x => f.At(x)[q]).ToArray();
        double ma = ms[0], mb = ms[^1];
        var max = ms.Max(Math.Abs);
        double Dev(int i) => ms[i] - (ma + (mb - ma) * xs[i] / l);
        var devs = Enumerable.Range(0, n).Select(Dev).ToArray();
        var dmax = devs.Max(Math.Abs);
        var linear = dmax <= 1e-3 * max + 1e-9;
        var k = Array.IndexOf(devs.Select(Math.Abs).ToArray(), dmax);
        var ms0 = linear ? 0 : ms[k];
        var quarter = Math.Abs(Dev(n / 4));
        var mid = Math.Abs(Dev(n / 2));
        var uniform = mid > 1e-12 && quarter / mid > 0.625;
        return new MomentShape(ma, mb, ms0, linear, uniform, max);
    }
}

/// <summary>Helpers the checks share.</summary>
public static class Checks
{
    public static string F(double v, string fmt = "0.###") => v.ToString(fmt, CultureInfo.InvariantCulture);

    /// <summary>Linear interpolation in a table of (x, y) sorted by x, clamped at its ends.</summary>
    public static double Interp(IReadOnlyList<(double X, double Y)> t, double x)
    {
        if (x <= t[0].X) return t[0].Y;
        for (var i = 1; i < t.Count; i++)
            if (x <= t[i].X) return t[i - 1].Y + (t[i].Y - t[i - 1].Y) * (x - t[i - 1].X) / (t[i].X - t[i - 1].X);
        return t[^1].Y;
    }

    /// <summary>
    /// The worst of each check over the sets: <paramref name="each"/> gives a set's checks (null: none), the largest utilisation
    /// of each key kept with its set's key and name.
    /// </summary>
    public static List<Check> Worst(IEnumerable<ForceSet> sets, Func<ForceSet, IEnumerable<Check>> each) => Worst(sets, s => s.Key, s => s.Name, each);

    /// <summary>The worst of each check over any sets, each set's key and name as <paramref name="key"/> and <paramref name="name"/> say.</summary>
    public static List<Check> Worst<T>(IEnumerable<T> sets, Func<T, string> key, Func<T, string> name, Func<T, IEnumerable<Check>> each)
    {
        var best = new Dictionary<string, Check>();
        var order = new List<string>();
        foreach (var s in sets)
            foreach (var c in each(s))
            {
                var c2 = c with { Combination = key(s), CombinationName = name(s) };
                if (!best.TryGetValue(c.Key, out var had)) { best[c.Key] = c2; order.Add(c.Key); }
                else if (c.Utilisation > had.Utilisation) best[c.Key] = c2;
            }
        return order.Select(k => best[k]).ToList();
    }

    /// <summary>The flexural buckling reduction χ (EN 1993-1-1 6.3.1.2, eq. 6.49): Φ = 0.5[1 + α(λ̄ − 0.2) + λ̄²], χ = 1/(Φ + √(Φ² − λ̄²)) ≤ 1.</summary>
    public static double Chi(double lambda, double alpha, double lambda0 = 0.2, double beta = 1)
    {
        var phi = 0.5 * (1 + alpha * (lambda - lambda0) + beta * lambda * lambda);
        var chi = 1 / (phi + Math.Sqrt(Math.Max(0, phi * phi - beta * lambda * lambda)));
        return Math.Min(1, chi);
    }
}
