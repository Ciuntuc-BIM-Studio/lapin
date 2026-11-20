namespace Lapin.Analysis.Seismic;

/// <summary>
/// The response spectra (docs/plan-fem.md §11.3), in m/s². P100-1/2013 ch. 3: β0 = 2.5, TB = 0.2 TC, TD = 3 s (TC 0.7, 1.0 s)
/// or 2 s (TC 1.6 s) (Table 3.1); β(T) = 1 + (β0 − 1) T/TB to TB, β0 to TC, β0 TC/T to TD, β0 TC TD/T² past it; Se = ag β;
/// the design spectrum Sd = ag [1 + (β0/q − 1) T/TB] to TB, ag β/q past it, at least 0.2 ag; the forces γI,e times it.
/// EN 1998-1 3.2.2.2 and 3.2.2.5 (recommended values, Tables 3.2 and 3.3): ag = γI agR; Se = ag S [1 + T/TB (2.5 η − 1)] …;
/// Sd = ag S [2/3 + T/TB (2.5/q − 2/3)] to TB, ag S 2.5/q to TC, ag S 2.5/q TC/T ≥ 0.2 ag to TD, ag S 2.5/q TC TD/T² ≥ 0.2 ag.
/// </summary>
public sealed record Spectrum(string Code, double Ag, double S, double TB, double TC, double TD, double Q, double Factor, double Beta0 = 2.5, double Lower = 0.2)
{
    public const double G = 9.80665;

    /// <summary>EN 1998-1 Table 3.2 (Type 1) and 3.3 (Type 2): S, TB, TC, TD by ground type.</summary>
    public static readonly IReadOnlyDictionary<(int, string), (double S, double TB, double TC, double TD)> EnTable = new Dictionary<(int, string), (double, double, double, double)>
    {
        [(1, "A")] = (1.0, 0.15, 0.4, 2.0), [(1, "B")] = (1.2, 0.15, 0.5, 2.0), [(1, "C")] = (1.15, 0.20, 0.6, 2.0), [(1, "D")] = (1.35, 0.20, 0.8, 2.0), [(1, "E")] = (1.4, 0.15, 0.5, 2.0),
        [(2, "A")] = (1.0, 0.05, 0.25, 1.2), [(2, "B")] = (1.35, 0.05, 0.25, 1.2), [(2, "C")] = (1.5, 0.10, 0.25, 1.2), [(2, "D")] = (1.8, 0.10, 0.30, 1.2), [(2, "E")] = (1.6, 0.05, 0.25, 1.2),
    };

    public bool IsP100 => Code == SeismicSettings.P100;

    public static Spectrum Of(SeismicSettings s)
    {
        var ag = (s.Ag ?? 0) * G;
        if (s.IsP100)
        {
            var tc = s.Tc;
            return new Spectrum(s.Code, ag, 1, 0.2 * tc, tc, tc >= 1.6 - 1e-9 ? 2 : 3, s.Q, s.Gamma);
        }
        var (S, tb, tcc, td) = EnTable[(s.SpectrumType, s.Ground)];
        return new Spectrum(s.Code, ag * s.Gamma, S, tb, tcc, td, s.Q, 1);
    }

    /// <summary>P100's normalized elastic spectrum β(T).</summary>
    public double Beta(double t) =>
        t <= TB ? 1 + (Beta0 - 1) * t / TB : t <= TC ? Beta0 : t <= TD ? Beta0 * TC / t : Beta0 * TC * TD / (t * t);

    /// <summary>The elastic spectrum Se(T) (5 % damping).</summary>
    public double Se(double t) => IsP100 ? Ag * Beta(t) : Ag * S * (t <= TB ? 1 + t / TB * (2.5 - 1) : t <= TC ? 2.5 : t <= TD ? 2.5 * TC / t : 2.5 * TC * TD / (t * t));

    /// <summary>The design spectrum Sd(T) (m/s²), before P100's γI,e (<see cref="Factor"/>).</summary>
    public double Sd(double t)
    {
        if (IsP100)
            return t <= TB ? Ag * (1 + (Beta0 / Q - 1) * t / TB) : Math.Max(Ag * Beta(t) / Q, Lower * Ag);
        if (t <= TB) return Ag * S * (2.0 / 3 + t / TB * (2.5 / Q - 2.0 / 3));
        if (t <= TC) return Ag * S * 2.5 / Q;
        if (t <= TD) return Math.Max(Ag * S * 2.5 / Q * TC / t, Lower * Ag);
        return Math.Max(Ag * S * 2.5 / Q * TC * TD / (t * t), Lower * Ag);
    }

    /// <summary>The acceleration that loads a mode of period T: the design spectrum times P100's γI,e (EN's γI is already in ag).</summary>
    public double Design(double t) => Factor * Sd(t);

    /// <summary>Der Kiureghian's CQC correlation of two modes of equal damping ξ (EESD 9, 1981): ρ = 8ξ²(1 + r) r^1.5 / ((1 − r²)² + 4ξ² r (1 + r)²), r = ωj/ωi.</summary>
    public static double Rho(double wi, double wj, double xi)
    {
        var r = wj / wi;
        return 8 * xi * xi * (1 + r) * Math.Pow(r, 1.5) / ((1 - r * r) * (1 - r * r) + 4 * xi * xi * r * (1 + r) * (1 + r));
    }

    /// <summary>The complete quadratic combination of modal values (SRSS when the modes are far apart): √(Σi Σj ρij ri rj).</summary>
    public static double Cqc(IReadOnlyList<double> values, IReadOnlyList<double> omegas, double xi)
    {
        var s = 0.0;
        for (var i = 0; i < values.Count; i++)
            for (var j = 0; j < values.Count; j++)
                s += Rho(omegas[i], omegas[j], xi) * values[i] * values[j];
        return Math.Sqrt(Math.Max(s, 0));
    }
}
