using Lapin.Analysis.Design;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Lapin.Platform.Analysis;

namespace Lapin.Engine.Tests;

/// <summary>
/// S4's design checks on their own (docs/plan-fem.md §12), each number worked out by hand from the norm's formulas in the
/// comments (SR EN 1993-1-1 with γM0 = γM1 = 1.0, SR EN 1992-1-1 with γc = 1.5, γs = 1.15, αcc = 1.0), the clause in the name.
/// The sections' properties are the catalogue's (ArcelorMittal's tables) so the hand calculations can be followed.
/// </summary>
public class DesignCheckTests
{
    // IPE 300: A 53.81 cm², Iy 8356 cm⁴, Iz 603.8 cm⁴, Wel,y 557.1 cm³, Wpl,y 628.4 cm³, Wel,z 80.5 cm³, Wpl,z 125.2 cm³, It 20.12 cm⁴, Iw 125.9·10³ cm⁶, Avz 25.68 cm².
    static readonly SectionShape Ipe300 = new("I", 0.150, 0.300, 0.0071, 0.0107, Designation: "IPE 300") { Root = 0.015 };
    static readonly SectionProps Ipe300P = new(53.81e-4, 603.8e-8, 8356e-8, 80.5e-6, 557.1e-6, 125.2e-6, 628.4e-6, 20.12e-8, default, "table", "EN 10365", "IPE", 0.015)
    { Iw = 125.9e-9, Av2 = 25.68e-4, Av3 = 2 * 0.150 * 0.0107 };
    // HEB 200: A 78.1 cm², Iy 5696 cm⁴, Iz 2003 cm⁴, Wel,y 569.6, Wpl,y 642.5, Wel,z 200.3, Wpl,z 305.8 cm³, It 59.28 cm⁴, Iw 171.1·10³ cm⁶, Avz 24.83 cm².
    static readonly SectionShape Heb200 = new("I", 0.200, 0.200, 0.009, 0.015, Designation: "HEB 200") { Root = 0.018 };
    static readonly SectionProps Heb200P = new(78.1e-4, 2003e-8, 5696e-8, 200.3e-6, 569.6e-6, 305.8e-6, 642.5e-6, 59.28e-8, default, "table", "EN 10365", "HEB", 0.018)
    { Iw = 171.1e-9, Av2 = 24.83e-4, Av3 = 2 * 0.2 * 0.015 };

    /// <summary>A member's forces at 21 stations from functions of x.</summary>
    static ForceSet Set(double l, Func<double, double> n, Func<double, double> mz, Func<double, double> vy, Func<double, double>? my = null) =>
        new("uls/test", "test", Enumerable.Range(0, 21).Select(i => l * i / 20).Select(x => new Section(x, n(x), vy(x), 0, 0, my?.Invoke(x) ?? 0, mz(x))).ToList(), l);

    static Check Get(List<Check> cs, string key) => cs.Single(c => c.Key == key);
    static double V(Check c, string name) => c.Values.Single(v => v.Name == name).Value;

    [Fact]
    public void Table_5_2_classes_an_IPE_300_in_S275_as_class_1_in_bending()
    {
        // ε = √(235/275) = 0.9244. Web c = 300 − 2·10.7 − 2·15 = 248.6 mm, c/t = 35.01 ≤ 72ε = 66.56; flange c = (150 − 7.1 − 30)/2 = 56.45 mm,
        // c/t = 5.276 ≤ 9ε = 8.32: class 1, the flange the nearer its limit.
        var (cls, part, ratio, lim) = SteelDesign.Classify(Ipe300, Ipe300P, 275e6, 0, 50);
        Assert.Equal(1, cls);
        Assert.Equal("flange", part);
        Assert.Equal(5.2757, ratio, 3);
        Assert.Equal(9 * Math.Sqrt(235.0 / 275), lim[0], 6);
        // Pure compression: the web as an internal part in compression, 33ε = 30.5 < 35.01 ≤ 38ε = 35.13: class 2.
        Assert.Equal(2, SteelDesign.Classify(Ipe300, Ipe300P, 275e6, 500, 0).Class);
    }

    [Fact]
    public void Clause_6_3_1_flexural_buckling_of_a_HEB_200_column_4_m_long()
    {
        // S235, pinned at both ends (Lcr = 4 m). h/b = 1.0 ≤ 1.2, tf ≤ 100 mm: Table 6.2 curve b about y, c about z (α = 0.49).
        // Ncr,z = π²·210 000·2003·10⁴/4000² = 2594.6 kN; λ̄z = √(7810·235/2 594 600) = 0.8410; Φ = 0.5[1 + 0.49(0.8410 − 0.2) + 0.8410²] = 1.0107;
        // χz = 1/(Φ + √(Φ² − λ̄²)) = 0.63642; Nb,Rd = 0.63642·7810·235 = 1168.05 kN; NEd = 1000 kN: 0.85613.
        var m = new SteelMember(Heb200, Heb200P, 235e6, 210e9, 4, 4, 4, 0);
        var cs = SteelDesign.Check(m, [Set(4, _ => -1000, _ => 0, _ => 0)]);
        var b = Get(cs, "steel.buckling");
        Assert.Equal(0.84105, V(b, "λ̄z"), 4);
        Assert.Equal(0.63642, V(b, "χz (curve c)"), 4);
        Assert.Equal(1168.05, V(b, "Nb,Rd"), 1);
        Assert.Equal(1000 / 1168.05, b.Utilisation, 4);
        // The section: Npl,Rd = 7810·235 = 1835.35 kN.
        Assert.Equal(1000 / 1835.35, Get(cs, "steel.section").Utilisation, 4);
    }

    [Fact]
    public void NCCI_SN003_gives_Mcr_of_an_IPE_300_over_6_m()
    {
        // Mcr = C1 π²EIz/L² √(Iw/Iz + L²GIt/(π²EIz)), E = 210 GPa, G = E/2.6 = 80.77 GPa:
        // π²·210·10⁶·603.8·10⁻⁸/36 = 347.62 kN; √(125.9·10⁻⁹/603.8·10⁻⁸ + 36·80 769·20.12·10⁻⁸/(π²·210·10⁶·603.8·10⁻⁸)) = √(0.020851 + 0.046748) = 0.26000 m: Mcr = 90.38 kN·m (C1 = 1).
        Assert.Equal(90.382, SteelDesign.Mcr(1, 210e9, 210e9 / 2.6, 603.8e-8, 125.9e-9, 20.12e-8, 6), 2);
        Assert.Equal(101.860, SteelDesign.Mcr(1.127, 210e9, 210e9 / 2.6, 603.8e-8, 125.9e-9, 20.12e-8, 6), 2);
        // C1 by ψ (SN003 Table 3.1): 1.77 for ψ = 0, 2.33 for ψ = −0.5, interpolated 1.31 + (1.52 − 1.31)/2 between 0.5 and 0.25.
        var sh = (double a, double b) => MomentShape.Of(Set(6, _ => 0, x => a + (b - a) * x / 6, _ => 0), 5);
        Assert.Equal(1.77, SteelDesign.C1Of(sh(100, 0)).C1, 6);
        Assert.Equal(2.33, SteelDesign.C1Of(sh(100, -50)).C1, 6);
        Assert.Equal(1.415, SteelDesign.C1Of(sh(100, 37.5)).C1, 6);
    }

    [Fact]
    public void Clause_6_3_2_3_lateral_torsional_buckling_of_a_rolled_IPE_300_under_a_uniform_load()
    {
        // S275, simply supported over 6 m, unrestrained, M = 80 kN·m at mid-span (q = 17.78 kN/m). C1 = 1.127: Mcr = 101.860 kN·m;
        // λ̄LT = √(628.4·275/101 860) = 1.30251; curve b (h/b = 2 ≤ 2), λ̄LT,0 = 0.4, β = 0.75: Φ = 0.5[1 + 0.34(0.90251) + 0.75·1.69654] = 1.28962,
        // χLT = 1/(Φ + √(Φ² − 0.75 λ̄²)) = 0.52227 (≤ 1/λ̄² = 0.58944); kc = 0.94: f = 1 − 0.5·0.06·[1 − 2(0.50251)²] = 0.98515;
        // χLT,mod = 0.53014; Mb,Rd = 0.53014·628.4·275 = 91.614 kN·m: utilisation 80/91.614 = 0.87323.
        var q = 8 * 80 / 36.0;
        var m = new SteelMember(Ipe300, Ipe300P, 275e6, 210e9, 6, 6, 6, 6);
        var cs = SteelDesign.Check(m, [Set(6, _ => 0, x => q * x * (6 - x) / 2, x => q * (3 - x))]);
        var ltb = Get(cs, "steel.ltb");
        Assert.Equal(1.127, V(ltb, "C1"), 6);
        Assert.Equal(1.30251, V(ltb, "λ̄LT"), 4);
        Assert.Equal(0.98515, V(ltb, "f"), 4);
        Assert.Equal(91.614, V(ltb, "Mb,Rd"), 2);
        Assert.Equal(80 / 91.614, ltb.Utilisation, 4);
        // The section (class 1): Mpl,Rd = 628.4·275 = 172.81 kN·m.
        Assert.Equal(80 / 172.81, Get(cs, "steel.section").Utilisation, 4);
        // Shear: Vpl,Rd = 25.68·10⁻⁴·275 000/√3 = 407.73 kN; VEd = 3q = 53.33 kN.
        Assert.Equal(3 * q / (25.68e-4 * 275e3 / Math.Sqrt(3)), Get(cs, "steel.shear").Utilisation, 4);
        // Restrained laterally: no LTB check.
        Assert.DoesNotContain(SteelDesign.Check(m with { Llt = 0 }, [Set(6, _ => 0, x => q * x * (6 - x) / 2, x => q * (3 - x))]), c => c.Key == "steel.ltb");
    }

    [Fact]
    public void Annex_B_interaction_of_a_HEB_200_column_under_compression_and_end_moment()
    {
        // HEB 200 S235, 4 m, N = 500 kN, My from 30 kN·m at the foot to 0 at the head (ψ = 0), laterally restrained (Llt = 0: Table B.1).
        // χy (curve b, λ̄y = 0.49874) = 0.88477, χz = 0.63642; NRk = 1835.35 kN; ny = 500/(0.88477·1835.35) = 0.30791, nz = 0.42806;
        // Cmy = 0.6 + 0.4·0 = 0.6; kyy = min(0.6(1 + (0.49874 − 0.2)·0.30791), 0.6(1 + 0.8·0.30791)) = 0.65519; kzy = 0.6 kyy = 0.39311;
        // My,Rk = 642.5·235 = 150.99 kN·m: eq. 6.61 = 0.30791 + 0.65519·30/150.99 = 0.43809; eq. 6.62 = 0.42808 + 0.39311·30/150.99 = 0.50619.
        var m = new SteelMember(Heb200, Heb200P, 235e6, 210e9, 4, 4, 4, 0);
        var cs = SteelDesign.Check(m, [Set(4, _ => -500, x => 30 * (1 - x / 4), _ => -7.5)]);
        var i = Get(cs, "steel.interaction");
        Assert.Equal(0.6, V(i, "Cmy"), 6);
        Assert.Equal(0.65519, V(i, "kyy"), 4);
        Assert.Equal(0.43809, V(i, "eq. 6.61"), 4);
        Assert.Equal(0.50617, V(i, "eq. 6.62"), 4);
        Assert.Equal(0.50617, i.Utilisation, 4);
    }

    static readonly ConcreteValues C25 = new(25e6);

    [Fact]
    public void Clause_6_1_bending_reinforcement_of_a_300_by_500_beam_for_150_kNm()
    {
        // d = 500 − 30 − 8 − 8 = 454 mm; K = 150·10⁶/(300·454²·25) = 0.097033 ≤ K' = 0.167; z = d(0.5 + √(0.25 − K/1.134)) = 0.41110 m (≤ 0.95 d);
        // As = 150/(434 783·0.41110) = 8.392 cm². As,min (9.1N) = 0.26·2.565/500·300·454 = 1.817 cm² (fctm = 0.30·25^(2/3) = 2.565 MPa).
        var d = 0.5 - C25.Edge;
        Assert.Equal(0.454, d, 9);
        var (asT, as2, k, z) = ConcreteDesign.Bending(150, 0.3, d, C25.Edge, C25);
        Assert.Equal(0.097033, k, 5);
        Assert.Equal(0.41110, z, 4);
        Assert.Equal(8.3921, asT * 1e4, 3);
        Assert.Equal(0, as2);
        Assert.Equal(1.8166, ConcreteDesign.MinAs(0.3, d, C25) * 1e4, 3);
        // Doubly reinforced past K': for 300 kN·m, K = 0.194: As2 = (0.194 − 0.167)·25·300·454²/(434.78·(454 − 46)) > 0.
        Assert.True(ConcreteDesign.Bending(300, 0.3, d, C25.Edge, C25).As2 > 0);
    }

    [Fact]
    public void Clause_6_2_shear_of_the_beam_and_its_stirrups()
    {
        // VRd,c (6.2a): k = 1 + √(200/454) = 1.6637; ρl = 8.392/(300·454) = 0.0061616; 0.12·1.6637·(100·0.0061616·25)^(1/3) = 0.49673 MPa
        // ≥ vmin = 0.035·1.6637^1.5·5 = 0.37557: VRd,c = 0.49673·300·454 = 67.66 kN.
        var d = 0.454;
        var (vrdc, k, vmin) = ConcreteDesign.VRdc(0.3, d, 8.3921e-4, C25);
        Assert.Equal(1.66372, k, 4);
        Assert.Equal(67.658, vrdc, 2);
        // VEd = 200 kN: z = 0.9 d = 0.4086 m, ν1 = 0.6(1 − 25/250) = 0.54, VRd,max(cot θ = 2.5) = 0.3·0.4086·0.54·16 667/2.9 = 380.42 kN ≥ VEd,
        // so cot θ = 2.5 and Asw/s = 200/(0.4086·434 783·2.5) = 4.503 cm²/m (ρw,min: 0.08·5/500·300 = 2.4 cm²/m).
        var st = ConcreteDesign.Stirrups(200, 0.3, d, C25)!.Value;
        Assert.Equal(2.5, st.CotTheta, 9);
        Assert.Equal(380.42, st.VRdMax, 1);
        Assert.Equal(4.5032, st.AswS * 1e4, 3);
        Assert.Equal(2.4, st.AswSMin * 1e4, 6);
        // 500 kN: past VRd,max at 2.5, under 551.61 kN at cot θ = 1: the cot θ where VRd,max = 500 kN.
        var st2 = ConcreteDesign.Stirrups(500, 0.3, d, C25)!.Value;
        Assert.Equal(500, st2.VRdMax, 6);
        Assert.Null(ConcreteDesign.Stirrups(560, 0.3, d, C25));
    }

    [Fact]
    public void Clause_7_4_2_span_to_depth_ratio()
    {
        // ρ = 0.0061616 > ρ0 = √25·10⁻³ = 0.005: eq. 7.16b with ρ′ = 0, K = 1: 11 + 1.5·5·0.005/0.0061616 = 17.086.
        Assert.Equal(17.0860, ConcreteDesign.SpanDepth(0.0061616, 0, 25, 1), 3);
        // ρ = 0.5 % = ρ0: 11 + 7.5 + 0 = 18.5 (Table 7.4N gives 20 for ρ = 0.5 %, with C30: √30 = 5.48: 11 + 8.22 = 19.2... the formula, not the table).
        Assert.Equal(18.5, ConcreteDesign.SpanDepth(0.005, 0, 25, 1), 6);
        Assert.Equal(0.4 * 18.5, ConcreteDesign.SpanDepth(0.005, 0, 25, 0.4), 6);
    }

    [Fact]
    public void Strain_compatibility_gives_a_columns_moment_of_resistance()
    {
        // 300 × 300, As = 8 cm² (4 cm² at each face, d2 = 50 mm), C25/30: with x = 150 mm the bars strain ±3.5‰·100/150 = ±2.33‰ > εyd = 2.17‰,
        // both yield; N = 0.8·0.15·0.3·16 667 = 600 kN; M = 600(0.15 − 0.06) + 8·10⁻⁴·434 783·0.10 = 54 + 34.78 = 88.78 kN·m.
        Assert.Equal(88.783, ConcreteDesign.MomentResistance(600, 0.3, 0.3, 0.05, 8e-4, C25)!.Value, 2);
        // Beyond the axial resistance either way: none.
        Assert.Null(ConcreteDesign.MomentResistance(2000, 0.3, 0.3, 0.05, 8e-4, C25));
        Assert.Null(ConcreteDesign.MomentResistance(-400, 0.3, 0.3, 0.05, 8e-4, C25));
        // 5.39's exponent: 1.0 at NEd/NRd = 0.1, 1.5 at 0.7, 2.0 at 1.0.
        Assert.Equal(1.25, ConcreteDesign.BiaxialA(0.4), 9);
    }

    [Fact]
    public void Wood_Armer_moments_for_slabs()
    {
        // No twist: the moments themselves, the bottom's positive.
        Assert.Equal((10.0, 5.0, 0.0, 0.0), ConcreteDesign.WoodArmer(10, 5, 0));
        // mx = my = 10, mxy = 4: bottom 14, 14; top mx − 4 > 0, so x 0 and y 10 − 16/10 > 0: none.
        Assert.Equal((14.0, 14.0, 0.0, 0.0), ConcreteDesign.WoodArmer(10, 10, 4));
        // mx = 10, my = −5, mxy = 5: bottom 15, 0; top: mx − 5 > 0, so x 0 and y −5 − 25/10 = −7.5: 7.5 on top in y.
        Assert.Equal((15.0, 0.0, 0.0, 7.5), ConcreteDesign.WoodArmer(10, -5, 5));
        // mx = 10, my = −20, mxy = 5: bottom: my + 5 < 0, so y 0 and x 10 + 25/20 = 11.25; top x 10 − 5 > 0 → 0, y −20 − 2.5 → 22.5.
        Assert.Equal((11.25, 0.0, 0.0, 22.5), ConcreteDesign.WoodArmer(10, -20, 5));
    }

    [Fact]
    public void A_concrete_beams_checks_give_its_reinforcement()
    {
        // The 300 × 500 beam simply supported over 6 m with q = 33.33 kN/m: M = 150 kN·m at mid-span, V = 100 kN at the ends.
        var q = 8 * 150 / 36.0;
        var m = new ConcreteMember(0.3, 0.5, 6, false, C25, 6, 6);
        var (cs, need) = ConcreteMembers.Check(m, [Set(6, _ => 0, x => q * x * (6 - x) / 2, x => q * (3 - x))]);
        Assert.Equal(8.3921, need.Bottom * 1e4, 3);
        Assert.Equal(1.8166, need.Top * 1e4, 3);
        Assert.Equal((need.Bottom + need.Top) / (0.04 * 0.15), Get(cs, "concrete.bending").Utilisation, 6);
        // L/d = 6/0.454 = 13.216 against 17.086.
        Assert.Equal(6 / 0.454 / 17.0860, Get(cs, "concrete.deflection").Utilisation, 4);
        // V = 100 kN > VRd,c = 67.66 kN: Asw/s = 100/(0.4086·434 783·2.5) = 2.2516 cm²/m < the minimum 2.4.
        Assert.Equal(2.4, need.Stirrups * 1e4, 6);
    }
}
