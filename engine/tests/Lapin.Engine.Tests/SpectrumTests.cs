using Lapin.Analysis.Seismic;

namespace Lapin.Engine.Tests;

/// <summary>
/// The response spectra (docs/plan-fem.md §11.3, §11.6) at their characteristic periods, worked by hand: P100-1/2013 with
/// TC = 0.7 s, ag = 0.30 g, q = 3 (TB = 0.14 s, TD = 3 s, the 0.2 ag floor reached at TD), EN 1998-1 Type 1 on ground C with
/// agR = 0.25 g, q = 1.5; the importance factors; Der Kiureghian's CQC correlation and the combination of two modes far apart
/// (≈ SRSS) and close together.
/// </summary>
public class SpectrumTests
{
    const double G = Spectrum.G;

    [Fact]
    public void P100_s_design_spectrum_at_its_corner_periods_and_its_floor()
    {
        var s = Spectrum.Of(new SeismicSettings { Ag = 0.30, Tc = 0.7, Q = 3 });
        Assert.Equal(0.14, s.TB, 12);
        Assert.Equal(3, s.TD, 12);
        Assert.Equal(0.30 * G, s.Sd(0), 9);
        // β0/q = 2.5/3 on the plateau.
        Assert.Equal(0.25 * G, s.Sd(0.14), 9);
        Assert.Equal(0.25 * G, s.Sd(0.7), 9);
        Assert.Equal(0.125 * G, s.Sd(1.4), 9);
        // At TD: 0.3 · 2.5 · 0.7 / 3 / 3 = 0.0583 g, under the floor 0.2 ag = 0.06 g.
        Assert.Equal(0.06 * G, s.Sd(3), 9);
        Assert.Equal(0.06 * G, s.Sd(4), 9);
        // The elastic spectrum: β0 ag on the plateau, ag at T = 0.
        Assert.Equal(0.75 * G, s.Se(0.5), 9);
        Assert.Equal(0.30 * G, s.Se(0), 9);
        // TC = 1.6 s: TB = 0.32 s, TD = 2 s.
        var s16 = Spectrum.Of(new SeismicSettings { Ag = 0.20, Tc = 1.6, Q = 2 });
        Assert.Equal(0.32, s16.TB, 12);
        Assert.Equal(2, s16.TD, 12);
        Assert.Equal(0.2 * 2.5 * 1.6 * 2 / (2.5 * 2.5) / 2 * G, s16.Sd(2.5), 9);
    }

    [Fact]
    public void P100_s_importance_factor_multiplies_the_forces_EN_s_the_ground_acceleration()
    {
        Assert.Equal(1.4, new SeismicSettings { Ag = 0.3, Importance = "I" }.Gamma);
        Assert.Equal(1.2, new SeismicSettings { Ag = 0.3, Importance = "II" }.Gamma);
        Assert.Equal(1.0, new SeismicSettings { Ag = 0.3, Importance = "III" }.Gamma);
        Assert.Equal(0.8, new SeismicSettings { Ag = 0.3, Importance = "IV" }.Gamma);
        var p = Spectrum.Of(new SeismicSettings { Ag = 0.30, Tc = 0.7, Q = 3, Importance = "II" });
        Assert.Equal(1.2 * 0.25 * G, p.Design(0.5), 9);
        var e = Spectrum.Of(new SeismicSettings { Code = SeismicSettings.En1998, Ag = 0.25, Ground = "C", Q = 1.5, Importance = "III" });
        Assert.Equal(1.2 * 0.25 * G, e.Ag, 9);
        Assert.Equal(e.Sd(0.5), e.Design(0.5), 12);
    }

    [Fact]
    public void EN_1998_type_1_on_ground_C()
    {
        var s = Spectrum.Of(new SeismicSettings { Code = SeismicSettings.En1998, Ag = 0.25, Ground = "C", SpectrumType = 1, Q = 1.5, Importance = "II" });
        Assert.Equal((1.15, 0.2, 0.6, 2.0), (s.S, s.TB, s.TC, s.TD));
        Assert.Equal(0.25 * 1.15 * 2 / 3 * G, s.Sd(0), 9);
        Assert.Equal(0.25 * 1.15 * 2.5 / 1.5 * G, s.Sd(0.2), 9);
        Assert.Equal(0.25 * 1.15 * 2.5 / 1.5 * G, s.Sd(0.6), 9);
        Assert.Equal(0.25 * 1.15 * 2.5 / 1.5 * 0.6 / 1.2 * G, s.Sd(1.2), 9);
        Assert.Equal(0.25 * 1.15 * 2.5 / 1.5 * 0.6 * 2 / 9 * G, s.Sd(3), 9);
        Assert.Equal(0.05 * G, s.Sd(8), 9);
        var t2 = Spectrum.Of(new SeismicSettings { Code = SeismicSettings.En1998, Ag = 0.25, Ground = "D", SpectrumType = 2, Q = 1 });
        Assert.Equal((1.8, 0.1, 0.3, 1.2), (t2.S, t2.TB, t2.TC, t2.TD));
    }

    [Fact]
    public void CQC_correlates_close_modes_and_reduces_to_SRSS_for_distant_ones()
    {
        // Equal frequencies: fully correlated.
        Assert.Equal(1, Spectrum.Rho(10, 10, 0.05), 12);
        // r = 0.9, ξ = 5 %: 8ξ²(1 + r) r^1.5 / ((1 − r²)² + 4ξ² r (1 + r)²) = 0.473028.
        Assert.Equal(0.473028, Spectrum.Rho(10, 9, 0.05), 6);
        // Far apart (ω 10 and 30): ρ = 0.0064, CQC within 0.3 % of SRSS.
        var far = Spectrum.Cqc([1.0, 0.5], [10, 30], 0.05);
        Assert.InRange(far / Math.Sqrt(1.25), 1.0, 1.003);
        // Close (ω 10 and 10.5): ρ by hand, CQC = √(1 + 0.25 + 2 ρ · 0.5).
        var r = 10.5 / 10;
        var rho = 8 * 0.0025 * (1 + r) * Math.Pow(r, 1.5) / ((1 - r * r) * (1 - r * r) + 4 * 0.0025 * r * (1 + r) * (1 + r));
        Assert.Equal(Math.Sqrt(1.25 + rho), Spectrum.Cqc([1.0, 0.5], [10, 10.5], 0.05), 12);
        Assert.True(rho > 0.7);
    }
}
