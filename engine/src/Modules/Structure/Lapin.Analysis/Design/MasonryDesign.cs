using static Lapin.Analysis.Design.Checks;

namespace Lapin.Analysis.Design;

/// <summary>
/// A masonry wall for its checks (§12.5): its solid length at the foot (m), thickness, storey height, the effective height's
/// ρ2, fk, E, fb (MPa), fvk0 (MPa) and γM (the seismic one, CR 6's 1.65 where ag ≤ 0.12 g, besides).
/// </summary>
public sealed record MasonryWall(double L, double T, double H, double Rho2, double Fk, double E, double Fb, double Fvk0, double GammaM, double GammaMSeismic)
{
    public double Hef => Rho2 * H;
    /// <summary>CR 6-2013's accidental eccentricity ea = max(t/30, hef/300, 10 mm).</summary>
    public double Ea => Math.Max(Math.Max(T / 30, Hef / 300), 0.010);
}

/// <summary>A wall's forces at its foot, middle and head in one combination.</summary>
public sealed record WallForceSet(string Key, string Name, bool Seismic, WallSection Foot, WallSection? Middle, WallSection? Head);

/// <summary>
/// Unreinforced masonry walls after CR 6-2013 and SR EN 1996-1-1 where CR 6 could not be read (docs/plan-fem.md §12.5),
/// indicative: the vertical load's resistance NRd = Φ A fd (CR 6 eq. 6.16) at the head and foot (Φi = 1 − 2 ei/t, ei = M/N + ea
/// ≥ 0.05 t, eq. 6.18) and at mid-height (EN 1996-1-1 Annex G), the shear in the wall's plane by sliding in the bed joints
/// (VRd = fvd t lc, fvk = fvk0 + 0.4 σd ≤ 0.065 fb, CR 6 eq. 4.4a), and the bending in its plane with the axial force (the
/// compressed zone a rectangular block of 0.85 fd: MRd = N (l − x)/2).
/// </summary>
public static class MasonryDesign
{
    public const string Norm = "CR 6-2013";

    /// <summary>EN 1996-1-1 Annex G: Φm = A1 e^(−u²/2), A1 = 1 − 2 emk/t, u = (λ − 0.063)/(0.73 − 1.17 emk/t), λ = (hef/tef) √(fk/E).</summary>
    public static double PhiM(double emk, double t, double hef, double fk, double e)
    {
        var lambda = hef / t * Math.Sqrt(fk / e);
        var a1 = 1 - 2 * emk / t;
        var u = (lambda - 0.063) / (0.73 - 1.17 * emk / t);
        return Math.Max(0, a1 * Math.Exp(-u * u / 2));
    }

    /// <summary>The compressed length lc of a wall l long under N with an in-plane eccentricity e = M/N (a linear stress, no tension).</summary>
    public static double CompressedLength(double l, double n, double m)
    {
        if (n <= 0) return 0;
        var e = Math.Abs(m) / n;
        return e <= l / 6 ? l : Math.Max(0, 3 * (l / 2 - e));
    }

    /// <summary>The seismic γM CR 6 2.4.2.3.1(2) gives where ag ≤ 0.12 g (indicative, the built-in default; analysis.setDesign gammaMSeismic).</summary>
    public const double GammaMSeismic = 1.65;

    public static List<Check> Check(MasonryWall w, IReadOnlyList<WallForceSet> sets) => Worst(sets, s => s.Key, s => s.Name, s => Set(w, s));

    /// <summary>
    /// The note when the elastic shells' moment gives an eccentricity past 0.4 t: a masonry joint carries no tension, so the
    /// eccentricity is taken as 0.4 t (EN 1996-1-1 Annex C(4)'s rule as Lapin reads it, from memory: to be confirmed).
    /// </summary>
    const string Capped = "The elastic analysis' moment gives an eccentricity over 0.4 t (a monolithic slab's end moment on a joint with no tension): taken as 0.4 t (EN 1996-1-1 Annex C(4), from memory, to be confirmed).";

    static double U(double effect, double resistance) => resistance > 1e-12 ? effect / resistance : effect > 1e-9 ? 99 : 0;

    static IEnumerable<Check> Set(MasonryWall w, WallForceSet s)
    {
        var gm = s.Seismic ? w.GammaMSeismic : w.GammaM;
        var fd = w.Fk / gm * 1000; // kN/m²
        var (l, t) = (w.L, w.T);
        Shown G() => new("γM", gm, "");

        // Vertical load at the head and the foot (Φi) and at mid-height (Φm).
        Check Ends(string where, WallSection x)
        {
            var nl = x.N / l;
            var raw = nl > 1e-9 ? Math.Max(Math.Abs(x.MOut) / nl + w.Ea, 0.05 * t) : 0.05 * t;
            var ei = Math.Min(raw, 0.4 * t);
            var phi = Math.Max(0, 1 - 2 * ei / t);
            var nrd = phi * l * t * fd;
            return new Check($"masonry.vertical.{where}", $"Vertical load ({where})", $"{Norm} 6.16, 6.18", x.N > 0 ? U(x.N, nrd) : 0,
                [new("NEd", x.N, "kN"), new("m (out of plane)", x.MOut, "kN·m/m"), new("ea", w.Ea * 1000, "mm"), new("ei", ei * 1000, "mm"), new("Φ", phi, ""), new("fd", fd / 1000, "MPa"), new("NRd", nrd, "kN"), G()],
                raw > 0.4 * t ? Capped : null);
        }
        yield return Ends("foot", s.Foot);
        if (s.Head is { } h) yield return Ends("head", h);
        if (s.Middle is { } mid)
        {
            var nl = mid.N / l;
            var rawM = nl > 1e-9 ? Math.Max(Math.Abs(mid.MOut) / nl + w.Ea, 0.05 * t) : 0.05 * t;
            var emk = Math.Min(rawM, 0.4 * t);
            var phiM = PhiM(emk, t, w.Hef, w.Fk, w.E);
            var nrd = phiM * l * t * fd;
            yield return new Check("masonry.vertical.middle", "Vertical load (mid-height)", "EN 1996-1-1 6.1.2.2, Annex G", mid.N > 0 ? U(mid.N, nrd) : 0,
                [new("NEd", mid.N, "kN"), new("hef", w.Hef, "m"), new("λ", w.Hef / t * Math.Sqrt(w.Fk / w.E), ""), new("emk", emk * 1000, "mm"), new("Φm", phiM, ""), new("NRd", nrd, "kN"), G()],
                $"hef = ρ2 h, ρ2 = {F(w.Rho2, "0.##")} (EN 1996-1-1 5.5.1.2; the walls' sides not taken); creep (ek) not taken; CR 6 Table 6.2 not read." + (rawM > 0.4 * t ? " " + Capped : ""));
        }

        // Shear in the wall's plane at its foot (sliding in the bed joints).
        var f = s.Foot;
        var lc = CompressedLength(l, f.N, f.M);
        var sd = lc > 1e-9 ? f.N / (lc * t) / 1000 : 0; // MPa
        var fvk = Math.Min(w.Fvk0 + 0.4 * sd, 0.065 * w.Fb);
        var vrd = fvk / gm * 1000 * t * lc;
        yield return new Check("masonry.shear", "Shear in the wall's plane", $"{Norm} 4.4a, 6.6.4", U(Math.Abs(f.V), vrd),
            [new("VEd", Math.Abs(f.V), "kN"), new("NEd", f.N, "kN"), new("lc", lc, "m"), new("σd", sd, "MPa"), new("fvk", fvk, "MPa"), new("VRd", vrd, "kN"), G()],
            "Sliding in the bed joints; the diagonal tension failure (CR 6 6.6.4.1.2) not checked.");

        // Bending in the wall's plane with the axial force: the compressed zone a rectangular block of 0.85 fd.
        var x = f.N > 0 ? f.N / (0.85 * fd * t) : double.PositiveInfinity;
        var mrd = f.N > 0 && x < l ? f.N * (l - x) / 2 : 0;
        yield return new Check("masonry.bending", "Bending in the wall's plane", $"{Norm} 6.6.3 (EN 1996-1-1 6.1.2.1)", U(Math.Abs(f.M), mrd),
            [new("MEd", Math.Abs(f.M), "kN·m"), new("NEd", f.N, "kN"), new("x", double.IsFinite(x) ? x : 0, "m"), new("MRd", mrd, "kN·m"), G()],
            "MRd = N (l − x)/2, x = N/(0.85 fd t): the rectangular block's balance (CR 6 6.6.3.2 not read: to confirm).");
    }
}
