using static Lapin.Analysis.Design.Checks;

namespace Lapin.Analysis.Design;

/// <summary>
/// The project's reinforced concrete values (docs/plan-fem.md §12.3): γc 1.5, γs 1.15, αcc 1.0 (EN 1992-1-1's recommended
/// values, which the Romanian annex keeps by SCIA's list of national annexes), B500 bars, the concrete cover to the stirrups and
/// the bars' and stirrups' diameters that give d; all the user's to change (analysis.setDesign).
/// </summary>
public sealed record ConcreteValues(double Fck, double Fyk = 500e6, double Cover = 0.030, double Bar = 0.016, double Stirrup = 0.008, double GammaC = 1.5, double GammaS = 1.15, double AlphaCc = 1.0)
{
    /// <summary>In kN/m² (the engine's units with metres and kN).</summary>
    public double Fcd => AlphaCc * Fck / GammaC / 1000;
    public double Fyd => Fyk / GammaS / 1000;
    public double FckMpa => Fck / 1e6;
    public double FykMpa => Fyk / 1e6;
    /// <summary>fctm (MPa), EN 1992-1-1 Table 3.1: 0.30 fck^(2/3) up to C50/60.</summary>
    public double Fctm => FckMpa <= 50 ? 0.30 * Math.Pow(FckMpa, 2.0 / 3) : 2.12 * Math.Log(1 + (FckMpa + 8) / 10);
    /// <summary>The distance from a face to the bars' axis (m): cover + stirrup + half a bar.</summary>
    public double Edge => Cover + Stirrup + Bar / 2;
    /// <summary>εyd = fyd/Es, Es = 200 GPa (3.2.7(4)).</summary>
    public double EpsYd => Fyd / 200e6;
}

/// <summary>
/// Reinforced concrete sections after SR EN 1992-1-1 (§12.3), indicative, in the engine's units (m, kN, kN·m; areas m²):
/// a rectangle's bending reinforcement by the rectangular stress block (singly or doubly reinforced, K' = 0.167), shear
/// (VRd,c, the stirrups for a variable strut 1 ≤ cot θ ≤ 2.5), the minimum and maximum areas, the span/depth ratio for
/// deflection, a column's N with biaxial bending by strain compatibility (5.39) with its minimum eccentricity and the
/// nominal curvature's second-order moment (5.8.8), and Wood–Armer's moments for slabs.
/// </summary>
public static class ConcreteDesign
{
    public const string Norm = "SR EN 1992-1-1";
    public const double KPrime = 0.167;

    /// <summary>Bending reinforcement of a rectangle b × (d) for M ≥ 0 (kN·m): As in tension, As2 in compression (m²), K, z (m).</summary>
    public static (double As, double As2, double K, double Z) Bending(double m, double b, double d, double d2, ConcreteValues c)
    {
        if (m <= 0) return (0, 0, 0, 0.95 * d);
        var fck = c.Fck / 1000;
        var k = m / (b * d * d * fck);
        var fyd = c.Fyd;
        if (k <= KPrime)
        {
            var z = Math.Min(0.95 * d, d * (0.5 + Math.Sqrt(0.25 - k / 1.134)));
            return (m / (fyd * z), 0, k, z);
        }
        var zp = d * (0.5 + Math.Sqrt(0.25 - KPrime / 1.134));
        var as2 = (k - KPrime) * fck * b * d * d / (fyd * (d - d2));
        return (KPrime * fck * b * d * d / (fyd * zp) + as2, as2, k, zp);
    }

    /// <summary>EN 1992-1-1 9.2.1.1(1), eq. 9.1N: As,min = 0.26 fctm/fyk bt d ≥ 0.0013 bt d.</summary>
    public static double MinAs(double b, double d, ConcreteValues c) => Math.Max(0.26 * c.Fctm / c.FykMpa, 0.0013) * b * d;

    /// <summary>VRd,c (kN), 6.2.2(1) eq. 6.2a/b, without axial force: [CRd,c k (100 ρl fck)^(1/3)] bw d ≥ vmin bw d.</summary>
    public static (double VRdc, double K, double Vmin) VRdc(double bw, double d, double asl, ConcreteValues c)
    {
        var k = Math.Min(2, 1 + Math.Sqrt(0.2 / d));
        var rho = Math.Min(0.02, asl / (bw * d));
        var fck = c.FckMpa;
        var crdc = 0.18 / c.GammaC;
        var vmin = 0.035 * Math.Pow(k, 1.5) * Math.Sqrt(fck);
        var v = Math.Max(crdc * k * Math.Pow(100 * rho * fck, 1.0 / 3), vmin);
        return (v * bw * d * 1000, k, vmin);
    }

    /// <summary>
    /// The stirrups (6.2.3) for VEd (kN): Asw/s (m²/m) at the largest cot θ in [1, 2.5] for which VEd ≤ VRd,max, z = 0.9 d;
    /// null when VEd is over VRd,max even at cot θ = 1. ρw,min = 0.08 √fck/fyk (9.5N) gives the least.
    /// </summary>
    public static (double AswS, double CotTheta, double VRdMax, double AswSMin)? Stirrups(double vEd, double bw, double d, ConcreteValues c)
    {
        var z = 0.9 * d;
        var nu1 = 0.6 * (1 - c.FckMpa / 250);
        double VMax(double cot) => bw * z * nu1 * c.Fcd / (cot + 1 / cot);
        var min = 0.08 * Math.Sqrt(c.FckMpa) / c.FykMpa * bw;
        if (vEd > VMax(1)) return null;
        double cotT;
        if (vEd <= VMax(2.5)) cotT = 2.5;
        else
        {
            // VRd,max falls as cot θ grows past 1: the cot θ where VRd,max = VEd, 1/(cot + tan) = VEd/(bw z ν1 fcd).
            var r = bw * z * nu1 * c.Fcd / vEd;
            cotT = (r + Math.Sqrt(Math.Max(0, r * r - 4))) / 2;
            cotT = Math.Clamp(cotT, 1, 2.5);
        }
        var fywd = c.Fyd;
        return (Math.Max(min, vEd / (z * fywd * cotT)), cotT, VMax(cotT), min);
    }

    /// <summary>The limiting span/effective depth (7.4.2, eq. 7.16a/b) for ρ, ρ′ (ratios) and the structural system's K (Table 7.4N).</summary>
    public static double SpanDepth(double rho, double rho2, double fckMpa, double k)
    {
        var rho0 = Math.Sqrt(fckMpa) * 1e-3;
        rho = Math.Max(rho, 1e-6);
        var sq = Math.Sqrt(fckMpa);
        return rho <= rho0
            ? k * (11 + 1.5 * sq * rho0 / rho + 3.2 * sq * Math.Pow(rho0 / rho - 1, 1.5))
            : k * (11 + 1.5 * sq * rho0 / Math.Max(1e-9, rho - rho2) + sq / 12 * Math.Sqrt(rho2 / rho0));
    }

    /// <summary>
    /// A rectangle b × h (bending about the axis along b, so h the lever's direction) with As/2 at each face d2 from it: the
    /// moment of resistance (kN·m) at the axial force n (kN, compression positive) by strain compatibility (εcu3 = 3.5 ‰ with
    /// the rectangular block λ = 0.8, η = 1; above x = h the pivot εc2 = 2 ‰ at 3h/7, 6.1(5)), the steel elastic–plastic.
    /// Null when n is beyond the section's axial resistance either way.
    /// </summary>
    public static double? MomentResistance(double n, double b, double h, double d2, double asTotal, ConcreteValues c)
    {
        var (fcd, fyd) = (c.Fcd, c.Fyd);
        var es = 200e6; // kN/m²
        var half = asTotal / 2;
        (double N, double M) At(double x)
        {
            double Eps(double y) => x <= h ? 0.0035 * (x - y) / x : 0.002 * (x - y) / (x - 3 * h / 7);
            var block = Math.Min(0.8 * x, h);
            var nc = block * b * fcd;
            double Sig(double e) => Math.Clamp(e * es, -fyd, fyd);
            var (s1, s2) = (Sig(Eps(d2)), Sig(Eps(h - d2)));
            var nn = nc + half * s1 + half * s2;
            var mm = nc * (h / 2 - block / 2) + half * s1 * (h / 2 - d2) - half * s2 * (h / 2 - d2);
            return (nn, mm);
        }
        var nMax = b * h * fcd + asTotal * Math.Min(fyd, 0.002 * es);
        var nMin = -asTotal * fyd;
        if (n >= nMax || n <= nMin) return null;
        double lo = 1e-6, hi = 1e3;
        if (At(lo).N > n) return Math.Max(0, At(lo).M);
        for (var i = 0; i < 200; i++)
        {
            var mid = Math.Sqrt(lo * hi);
            if (At(mid).N < n) lo = mid; else hi = mid;
            if (hi / lo < 1 + 1e-12) break;
        }
        return Math.Max(0, At(Math.Sqrt(lo * hi)).M);
    }

    /// <summary>EN 1992-1-1 5.8.8.2(3) / 5.39's exponent a for a rectangle by NEd/NRd (0.1 → 1.0, 0.7 → 1.5, 1.0 → 2.0).</summary>
    public static double BiaxialA(double ratio) => Interp([(0.1, 1.0), (0.7, 1.5), (1.0, 2.0)], ratio);

    /// <summary>
    /// Wood–Armer's design moments (Wood, Concrete 2, 1968) from mx, my, mxy, the sign the engineer's (positive: the bottom in
    /// tension): the bottom's (≥ 0) and the top's (≥ 0, as magnitudes) in x and y.
    /// </summary>
    public static (double XBottom, double YBottom, double XTop, double YTop) WoodArmer(double mx, double my, double mxy)
    {
        var a = Math.Abs(mxy);
        double xb = mx + a, yb = my + a;
        if (xb < 0) { xb = 0; yb = Math.Abs(mx) > 1e-12 ? my + Math.Abs(mxy * mxy / mx) : my + a; }
        else if (yb < 0) { yb = 0; xb = Math.Abs(my) > 1e-12 ? mx + Math.Abs(mxy * mxy / my) : mx + a; }
        double xt = mx - a, yt = my - a;
        if (xt > 0) { xt = 0; yt = Math.Abs(mx) > 1e-12 ? my - Math.Abs(mxy * mxy / mx) : my - a; }
        else if (yt > 0) { yt = 0; xt = Math.Abs(my) > 1e-12 ? mx - Math.Abs(mxy * mxy / my) : mx - a; }
        return (Math.Max(0, xb), Math.Max(0, yb), Math.Max(0, -xt), Math.Max(0, -yt));
    }
}
