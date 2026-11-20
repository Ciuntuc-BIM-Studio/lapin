using Lapin.Analysis.Solver;
using Lapin.Platform.Analysis;
using static Lapin.Analysis.Design.Checks;

namespace Lapin.Analysis.Design;

/// <summary>
/// A steel member as its checks need it: the section's shape and properties, fy and E (Pa), its length and the buckling
/// lengths about its strong axis (EN 1993's y-y: Lapin's local 3, I33, Mz) and its weak one (z-z: local 2, I22, My), the
/// laterally unrestrained length (0: restrained, no lateral torsional buckling) and where each length came from.
/// </summary>
public sealed record SteelMember(SectionShape Shape, SectionProps P, double Fy, double E, double L, double LcrY, double LcrZ, double Llt)
{
    public double G => E / (2 * 1.3);
    public string LengthsSource { get; init; } = "";
    /// <summary>The interaction factors of 6.3.3: "A" (Annex A, method 1: the default, the Romanian annex's) or "B" (Annex B, method 2).</summary>
    public string Method { get; init; } = "A";
}

/// <summary>
/// Steel members after SR EN 1993-1-1 (docs/plan-fem.md §12.2), the recommended partial factors (γM0 = γM1 = 1.0), indicative:
/// the section's class (Table 5.2), its resistance to N, V and M about both axes and their interaction (6.2.9.1 for I/H, RHS
/// and CHS of classes 1–2; the linear 6.2.1(7) otherwise), flexural buckling (6.3.1), lateral torsional buckling of I/H
/// sections (6.3.2.3 for rolled sections, 6.3.2.2 for the others; Mcr by NCCI SN003, C1 by the moment's shape) and the
/// members in bending and compression (6.3.3, the interaction factors of Annex B, method 2). Lapin's axes to EN 1993's: its
/// Mz (about local 3, the depth's normal) is EN's My, its My is EN's Mz; its Vy (along the depth) is EN's Vz.
/// </summary>
public static class SteelDesign
{
    public const double GammaM0 = 1.0, GammaM1 = 1.0;
    public const string Norm = "SR EN 1993-1-1";

    static readonly (double Psi, double C1)[] C1Table = [(-1, 2.55), (-0.75, 2.57), (-0.5, 2.33), (-0.25, 2.05), (0, 1.77), (0.25, 1.52), (0.5, 1.31), (0.75, 1.14), (1, 1.0)];
    public const double C1Uniform = 1.127, C1Point = 1.348;

    public static readonly Dictionary<string, double> Alpha = new() { ["a0"] = 0.13, ["a"] = 0.21, ["b"] = 0.34, ["c"] = 0.49, ["d"] = 0.76 };

    /// <summary>The kinds of section the checks cover.</summary>
    public static bool Covers(string kind) => kind is "I" or "asymI" or "RHS" or "CHS" or "U" or "T" or "L" or "rectangle" or "circle";

    static double Root(SectionShape s) => s.Root ?? SectionProperties.CatalogueOf(s.Designation)?.Radius ?? 0;

    /// <summary>
    /// The section's class (Table 5.2) under a compression <paramref name="nEd"/> (kN, ≥ 0) and a strong-axis moment
    /// <paramref name="mEd"/> (kN·m, its magnitude); with the governing part and its c/t over its class's limits.
    /// </summary>
    public static (int Class, string Part, double Ratio, double[] Limits) Classify(SectionShape s, SectionProps p, double fy, double nEd, double mEd)
    {
        var eps = Math.Sqrt(235e6 / fy);
        var fyk = fy / 1000;
        double b = s.Width, h = s.Depth, tw = s.Web, tf = s.Flange, r = Root(s);
        var parts = new List<(string Part, double Ratio, double[] Limits)>();
        // An internal part in bending and compression: α the plastic neutral axis' share of it in compression, ψ the elastic stresses' ratio.
        void Internal(string name, double c, double t, double area, double wel)
        {
            if (!(c > 0 && t > 0)) return;
            var alpha = nEd <= 0 && mEd <= 0 ? 1 : Math.Clamp(0.5 * (1 + nEd / (c * t * fyk)), 0, 1);
            double l1, l2;
            if (mEd < 1e-9) { l1 = 33 * eps; l2 = 38 * eps; }
            else if (alpha > 0.5) { l1 = 396 * eps / (13 * alpha - 1); l2 = 456 * eps / (13 * alpha - 1); }
            else { l1 = 36 * eps / alpha; l2 = 41.5 * eps / alpha; }
            var sc = nEd / area;
            var sb = wel > 0 ? mEd / wel * (c / h) : 0;
            var (s1, s2) = (sc + sb, sc - sb);
            double l3;
            if (s1 <= 0) l3 = double.PositiveInfinity;
            else
            {
                var psi = Math.Clamp(s2 / s1, -3, 1);
                l3 = psi > -1 ? 42 * eps / (0.67 + 0.33 * psi) : 62 * eps * (1 - psi) * Math.Sqrt(-psi);
            }
            parts.Add((name, c / t, [l1, l2, l3]));
        }
        void Outstand(string name, double c, double t) { if (c > 0 && t > 0) parts.Add((name, c / t, [9 * eps, 10 * eps, 14 * eps])); }
        switch (s.Kind)
        {
            case "I" or "asymI":
                Internal("web", h - 2 * tf - 2 * r, tw, p.Area, p.S33);
                Outstand("flange", (b - tw - 2 * r) / 2, tf);
                break;
            case "U":
                Internal("web", h - 2 * tf - 2 * r, tw, p.Area, p.S33);
                Outstand("flange", b - tw - r, tf);
                break;
            case "T":
                Outstand("flange", (b - tw - 2 * r) / 2, tf);
                Outstand("stem", h - tf - r, tw);
                break;
            case "RHS":
            {
                Internal("web", h - 3 * tw, tw, p.Area, p.S33);
                var c = b - 3 * tw;
                if (c > 0) parts.Add(("flange", c / tw, [33 * eps, 38 * eps, 42 * eps]));
                break;
            }
            case "CHS": parts.Add(("wall", b / tw, [50 * eps * eps, 70 * eps * eps, 90 * eps * eps])); break;
            case "L":
            {
                // Angles are class 3 at best (Table 5.2 sheet 3): h/t ≤ 15ε and (b + h)/2t ≤ 11.5ε.
                var t = Math.Max(tw, tf);
                var r1 = Math.Max(h / t / (15 * eps), (b + h) / (2 * t) / (11.5 * eps));
                return (r1 <= 1 ? 3 : 4, "legs", r1, [0, 0, 1]);
            }
            default: return (1, "solid", 0, [1, 1, 1]);
        }
        if (parts.Count == 0) return (1, "solid", 0, [1, 1, 1]);
        static int ClassOf(double ratio, double[] lim) => ratio <= lim[0] ? 1 : ratio <= lim[1] ? 2 : ratio <= lim[2] ? 3 : 4;
        var w = parts.MaxBy(x => ClassOf(x.Ratio, x.Limits) + x.Ratio / x.Limits[0] * 1e-6);
        return (ClassOf(w.Ratio, w.Limits), w.Part, w.Ratio, w.Limits);
    }

    /// <summary>Table 6.2's buckling curve about y-y (strong) and z-z (weak).</summary>
    public static (string Y, string Z) Curves(SectionShape s, double fy)
    {
        switch (s.Kind)
        {
            case "I":
            {
                var hb = s.Depth / s.Width;
                var tf = s.Flange;
                var s460 = fy >= 420e6;
                if (hb > 1.2) return tf <= 0.040 ? (s460 ? ("a0", "a0") : ("a", "b")) : tf <= 0.100 ? (s460 ? ("a", "a") : ("b", "c")) : ("d", "d");
                return tf <= 0.100 ? (s460 ? ("a", "a") : ("b", "c")) : (s460 ? ("c", "c") : ("d", "d"));
            }
            case "asymI": return ("b", "c");
            case "RHS" or "CHS":
            {
                var cold = (s.Standard ?? "").Contains("10219") || (s.Family ?? "").Contains("cold", StringComparison.OrdinalIgnoreCase);
                return cold ? ("c", "c") : fy >= 420e6 ? ("a0", "a0") : ("a", "a");
            }
            case "L": return ("b", "b");
            default: return ("c", "c");
        }
    }

    /// <summary>C1 for a moment's shape (NCCI SN003 Tables 3.1–3.2, k = 1): linear by ψ, a simple span's uniform or mid-span point load, else 1.0 (the safe side).</summary>
    public static (double C1, string How) C1Of(MomentShape m)
    {
        if (m.Linear) return (Interp(C1Table, m.Psi), $"end moments, ψ = {F(m.Psi, "0.##")}");
        if (m.SimpleSpan) return m.Uniform ? (C1Uniform, "uniform load on a simple span") : (C1Point, "point load at mid-span of a simple span");
        return (1.0, "end moments and a span load: C1 = 1.0 (the safe side)");
    }

    /// <summary>Cm of Table B.3 for a moment's shape.</summary>
    public static double Cm(MomentShape m)
    {
        var psi = m.Psi;
        if (m.Linear || Math.Abs(m.Ms) < 1e-12) return Math.Max(0.4, 0.6 + 0.4 * psi);
        var mh = m.EndMax;
        if (Math.Abs(m.Ms) >= mh)
        {
            var ah = Math.Abs(m.Ms) > 1e-12 ? mh / Math.Abs(m.Ms) * (Math.Sign(m.Ms) == Math.Sign(Math.Abs(m.Ma) >= Math.Abs(m.Mb) ? m.Ma : m.Mb) ? 1 : -1) : 0;
            var k = psi >= 0 ? 1 : 1 + 2 * psi;
            return m.Uniform ? 0.95 + 0.05 * ah * k : 0.90 + 0.10 * ah * k;
        }
        var large = Math.Abs(m.Ma) >= Math.Abs(m.Mb) ? m.Ma : m.Mb;
        var a = m.Ms / large;
        if (a >= 0) return Math.Max(0.4, 0.2 + 0.8 * a);
        if (psi >= 0) return Math.Max(0.4, m.Uniform ? 0.1 - 0.8 * a : -0.8 * a);
        return Math.Max(0.4, m.Uniform ? 0.1 * (1 - psi) - 0.8 * a : 0.2 * -psi - 0.8 * a);
    }

    /// <summary>Mcr (kN·m) of a doubly symmetric I by NCCI SN003 eq. (3): C1 π²EIz/L² √(Iw/Iz + L²GIt/(π²EIz)), loaded at the shear centre, k = kw = 1.</summary>
    public static double Mcr(double c1, double e, double g, double iz, double iw, double it, double l)
    {
        var pe = Math.PI * Math.PI * e * iz / (l * l);
        return c1 * pe * Math.Sqrt(iw / iz + l * l * g * it / (Math.PI * Math.PI * e * iz)) / 1000;
    }

    /// <summary>Every check of a steel member, the worst over the sets.</summary>
    public static List<Check> Check(SteelMember m, IReadOnlyList<ForceSet> sets)
    {
        var s = m.Shape;
        if (!Covers(s.Kind))
            return [new Check("steel.shape", "Steel section", Norm, 0, [], $"The section „{s.Kind}” has no rules in Lapin's checks (cold-formed and arbitrary sections: EN 1993-1-3): not verified.", false)];
        return Worst(sets, f => Set(m, f));
    }

    static IEnumerable<Check> Set(SteelMember m, ForceSet f)
    {
        var (s, p) = (m.Shape, m.P);
        var fy = m.Fy / 1000; // kN/m²
        double npl = p.Area * fy / GammaM0;
        var nc = f.Compression;
        var myMax = f.MaxAbs(5); // EN My: Lapin Mz
        var mzMax = f.MaxAbs(4);
        var (cls, part, ratio, lim) = Classify(s, p, m.Fy, nc, myMax);
        var plastic = cls <= 2;
        var (wy, wz) = plastic ? (p.Z33, p.Z22) : (p.S33, p.S22);
        double mrdY = wy * fy / GammaM0, mrdZ = wz * fy / GammaM0;
        var classNote = cls == 4 ? "Class 4: effective widths (EN 1993-1-5) not done; the elastic section's utilisation shown as an orientation only." : null;
        var done = cls < 4;
        yield return new Check("steel.class", $"Section class {cls}", $"{Norm} Table 5.2", cls == 4 ? ratio / lim[2] : 0,
            [new("class", cls, ""), new($"c/t ({part})", ratio, ""), new("limit class 1", lim[0], ""), new("limit class 2", lim[1], ""), new("limit class 3", double.IsFinite(lim[2]) ? lim[2] : 0, "")], classNote, done);

        // Shear (6.2.6): Vpl,Rd = Av (fy/√3)/γM0 along the depth (Lapin's Vy) and across it (Vz).
        double av2 = p.Av2 ?? p.Area, av3 = p.Av3 ?? p.Area;
        double vplZ = av2 * fy / Math.Sqrt(3) / GammaM0, vplY = av3 * fy / Math.Sqrt(3) / GammaM0;
        var vMaxZ = f.MaxAbs(1);
        var vMaxY = f.MaxAbs(2);
        var vAt = f.Sections.MaxBy(x => Math.Max(Math.Abs(x.Vy) / vplZ, Math.Abs(x.Vz) / vplY));
        string? shearNote = null;
        if (s.Kind is "I" or "U" && s.Web > 0 && (s.Depth - 2 * s.Flange) / s.Web > 72 * Math.Sqrt(235e6 / m.Fy) / 1.2) shearNote = "hw/tw > 72ε/η: shear buckling (EN 1993-1-5 §5) not checked.";
        yield return new Check("steel.shear", "Shear resistance", $"{Norm} 6.2.6", Math.Max(vMaxZ / vplZ, vMaxY / vplY),
            [new("Vz,Ed", vMaxZ, "kN"), new("Vpl,z,Rd", vplZ, "kN"), new("Vy,Ed", vMaxY, "kN"), new("Vpl,y,Rd", vplY, "kN"), new("Av,z", av2 * 1e4, "cm²")], shearNote) { X = vAt.X };

        // Cross-section: N, My, Mz at every station (6.2.9.1 plastic for I/H, RHS, CHS of classes 1–2; else the linear 6.2.1(7)).
        Check? worst = null;
        foreach (var sec in f.Sections)
        {
            var nEd = sec.N;
            var (myEd, mzEd) = (Math.Abs(sec.Mz), Math.Abs(sec.My));
            // High shear (6.2.8): fy reduced by (1 − ρ) on the moments it acts with.
            var rhoZ = Math.Abs(sec.Vy) > 0.5 * vplZ ? Math.Pow(2 * Math.Abs(sec.Vy) / vplZ - 1, 2) : 0;
            var rhoY = Math.Abs(sec.Vz) > 0.5 * vplY ? Math.Pow(2 * Math.Abs(sec.Vz) / vplY - 1, 2) : 0;
            double mry = mrdY, mrz = mrdZ;
            if (rhoZ > 0) mry = s.Kind == "I" && plastic ? Math.Min(mry, (p.Z33 - rhoZ * Math.Pow((s.Depth - 2 * s.Flange) * s.Web, 2) / (4 * s.Web)) * fy / GammaM0) : mry * (1 - rhoZ);
            if (rhoY > 0) mrz *= 1 - rhoY;
            double u;
            string how;
            var n = Math.Abs(nEd) / npl;
            if (plastic && s.Kind is "I" or "RHS" or "CHS")
            {
                double mny, mnz, a, b;
                if (s.Kind == "I")
                {
                    var aa = Math.Min(0.5, (p.Area - 2 * s.Width * s.Flange) / p.Area);
                    mny = Math.Min(mry, mry * (1 - n) / (1 - 0.5 * aa));
                    mnz = n <= aa ? mrz : mrz * (1 - Math.Pow((n - aa) / (1 - aa), 2));
                    (a, b) = (2, Math.Max(1, 5 * n));
                    how = "6.2.9.1(5): (My/MN,y)² + (Mz/MN,z)^(5n)";
                }
                else if (s.Kind == "RHS")
                {
                    var aw = Math.Min(0.5, (p.Area - 2 * s.Width * s.Web) / p.Area);
                    var af = Math.Min(0.5, (p.Area - 2 * s.Depth * s.Web) / p.Area);
                    mny = Math.Min(mry, mry * (1 - n) / (1 - 0.5 * aw));
                    mnz = Math.Min(mrz, mrz * (1 - n) / (1 - 0.5 * af));
                    a = b = Math.Min(6, 1.66 / (1 - 1.13 * n * n));
                    how = "6.2.9.1(6): RHS, α = β = 1.66/(1 − 1.13n²)";
                }
                else
                {
                    mny = mry * (1 - Math.Pow(n, 1.7));
                    mnz = mrz * (1 - Math.Pow(n, 1.7));
                    (a, b) = (2, 2);
                    how = "6.2.9.1(6): CHS, MN = Mpl(1 − n^1.7)";
                }
                if (n >= 1 || mny <= 0 || mnz <= 0) u = Math.Max(n, 1.0001) + myEd / mry + mzEd / mrz;
                else
                {
                    var e = Math.Pow(myEd / mny, a) + Math.Pow(mzEd / mnz, b);
                    u = Math.Max(n, Math.Pow(e, 1 / Math.Max(a, b)));
                }
            }
            else
            {
                u = n + myEd / mry + mzEd / mrz;
                how = "6.2.1(7): N/NRd + My/My,Rd + Mz/Mz,Rd";
            }
            if (worst == null || u > worst.Utilisation)
                worst = new Check("steel.section", "Cross-section resistance (N, M, V)", $"{Norm} 6.2", u,
                    [new("NEd", nEd, "kN"), new("Npl,Rd", npl, "kN"), new("My,Ed", myEd, "kN·m"), new("My,Rd", mry, "kN·m"), new("Mz,Ed", mzEd, "kN·m"), new("Mz,Rd", mrz, "kN·m"), new("class", cls, "")],
                    how + (rhoZ > 0 || rhoY > 0 ? "; fy reduced for high shear (6.2.8)" : ""), done) { X = sec.X };
        }
        if (worst != null) yield return worst;

        // Flexural buckling (6.3.1).
        var (cy, cz) = Curves(s, m.Fy);
        double lamY = 0, lamZ = 0, chiY = 1, chiZ = 1;
        if (nc > 1e-9)
        {
            double ncrY = Math.PI * Math.PI * m.E * p.I33 / (m.LcrY * m.LcrY) / 1000, ncrZ = Math.PI * Math.PI * m.E * p.I22 / (m.LcrZ * m.LcrZ) / 1000;
            lamY = Math.Sqrt(p.Area * fy / ncrY);
            lamZ = Math.Sqrt(p.Area * fy / ncrZ);
            chiY = lamY <= 0.2 ? 1 : Chi(lamY, Alpha[cy]);
            chiZ = lamZ <= 0.2 ? 1 : Chi(lamZ, Alpha[cz]);
            var nbRd = Math.Min(chiY, chiZ) * p.Area * fy / GammaM1;
            yield return new Check("steel.buckling", "Flexural buckling", $"{Norm} 6.3.1", nc / nbRd,
                [new("NEd", nc, "kN"), new("Lcr,y", m.LcrY, "m"), new("Lcr,z", m.LcrZ, "m"), new("λ̄y", lamY, ""), new("λ̄z", lamZ, ""), new($"χy (curve {cy})", chiY, ""), new($"χz (curve {cz})", chiZ, ""), new("Nb,Rd", nbRd, "kN")],
                (s.Kind is "U" or "T" or "L" ? "Torsional and flexural-torsional buckling (6.3.1.4) not checked. " : "") + m.LengthsSource, done);
        }

        // Lateral torsional buckling (6.3.2) of doubly symmetric I sections bent about their strong axis.
        var chiLT = 1.0;
        var shapeY = MomentShape.Of(f, 5);
        var susceptible = s.Kind == "I" && m.Llt > 1e-9 && p.Iw is { } && myMax > 1e-9;
        if (susceptible)
        {
            var (c1, c1How) = C1Of(shapeY);
            var mcr = Mcr(c1, m.E, m.G, p.I22, p.Iw!.Value, p.J, m.Llt);
            var lamLT = Math.Sqrt(wy * fy / mcr);
            var hb = s.Depth / s.Width;
            var rolled = s.Designation != null || s.Standard != null;
            double f1 = 1;
            string curve;
            if (rolled)
            {
                curve = hb <= 2 ? "b" : "c";
                chiLT = lamLT <= 0.4 ? 1 : Math.Min(1, Math.Min(Chi(lamLT, Alpha[curve], 0.4, 0.75), 1 / (lamLT * lamLT)));
                var kc = shapeY.Linear ? 1 / (1.33 - 0.33 * shapeY.Psi) : shapeY.SimpleSpan ? (shapeY.Uniform ? 0.94 : 0.90) : 1.0;
                f1 = Math.Min(1, 1 - 0.5 * (1 - kc) * (1 - 2 * Math.Pow(lamLT - 0.8, 2)));
                chiLT = Math.Min(Math.Min(1, 1 / (lamLT * lamLT)), chiLT / f1);
            }
            else
            {
                curve = hb <= 2 ? "c" : "d";
                chiLT = lamLT <= 0.2 ? 1 : Chi(lamLT, Alpha[curve]);
            }
            var mbRd = chiLT * wy * fy / GammaM1;
            yield return new Check("steel.ltb", "Lateral torsional buckling", $"{Norm} {(rolled ? "6.3.2.3" : "6.3.2.2")}", myMax / mbRd,
                [new("My,Ed", myMax, "kN·m"), new("L (unrestrained)", m.Llt, "m"), new("C1", c1, ""), new("Mcr", mcr, "kN·m"), new("λ̄LT", lamLT, ""), new($"χLT (curve {curve})", chiLT, ""), new("f", f1, ""), new("Mb,Rd", mbRd, "kN·m")],
                $"C1: {c1How}; Mcr by NCCI SN003, the load at the shear centre.", done);
        }
        else if (s.Kind is "U" or "T" or "L" or "asymI" && myMax > 1e-9 && m.Llt > 1e-9)
            yield return new Check("steel.ltb", "Lateral torsional buckling", $"{Norm} 6.3.2", 0, [], $"Lateral torsional buckling of a „{s.Kind}” section is not done by Lapin: check it.", false);

        // Bending and compression (6.3.3, eq. 6.61 and 6.62): Annex A (method 1) by default, Annex B (method 2) as the project says.
        if (nc > 1e-9 && (myMax > 1e-9 || mzMax > 1e-9))
        {
            var shapeZ = MomentShape.Of(f, 4);
            if (m.Method == "B") yield return AnnexB(m, nc, myMax, mzMax, chiY, chiZ, lamY, lamZ, chiLT, susceptible, plastic, wy, wz, shapeY, shapeZ, done);
            else yield return AnnexA(m, nc, myMax, mzMax, chiY, chiZ, lamY, lamZ, chiLT, plastic, shapeY, shapeZ, done);
        }
    }

    static Check AnnexB(SteelMember m, double nc, double myMax, double mzMax, double chiY, double chiZ, double lamY, double lamZ, double chiLT, bool susceptible, bool plastic,
        double wy, double wz, MomentShape shapeY, MomentShape shapeZ, bool done)
    {
        var (s, p) = (m.Shape, m.P);
        var fy = m.Fy / 1000;
        var nrk = p.Area * fy;
        double ny = nc / (chiY * nrk / GammaM1), nz = nc / (chiZ * nrk / GammaM1);
        double cmy = Cm(shapeY), cmz = Cm(shapeZ), cmlt = Math.Max(0.4, Cm(shapeY));
        double kyy, kzz, kyz, kzy;
        var isI = s.Kind is "I" or "asymI" or "U" or "T";
        if (plastic)
        {
            kyy = Math.Min(cmy * (1 + (lamY - 0.2) * ny), cmy * (1 + 0.8 * ny));
            kzz = isI ? Math.Min(cmz * (1 + (2 * lamZ - 0.6) * nz), cmz * (1 + 1.4 * nz)) : Math.Min(cmz * (1 + (lamZ - 0.2) * nz), cmz * (1 + 0.8 * nz));
            kyz = 0.6 * kzz;
            if (susceptible)
            {
                kzy = Math.Max(1 - 0.1 * lamZ / (cmlt - 0.25) * nz, 1 - 0.1 / (cmlt - 0.25) * nz);
                if (lamZ < 0.4) kzy = Math.Min(0.6 + lamZ, 1 - 0.1 * lamZ / (cmlt - 0.25) * nz);
            }
            else kzy = 0.6 * kyy;
        }
        else
        {
            kyy = Math.Min(cmy * (1 + 0.6 * lamY * ny), cmy * (1 + 0.6 * ny));
            kzz = Math.Min(cmz * (1 + 0.6 * lamZ * nz), cmz * (1 + 0.6 * nz));
            kyz = kzz;
            kzy = susceptible ? Math.Max(1 - 0.05 * lamZ / (cmlt - 0.25) * nz, 1 - 0.05 / (cmlt - 0.25) * nz) : 0.8 * kyy;
        }
        double myrk = wy * fy, mzrk = wz * fy;
        var u61 = ny + kyy * myMax / (chiLT * myrk / GammaM1) + kyz * mzMax / (mzrk / GammaM1);
        var u62 = nz + kzy * myMax / (chiLT * myrk / GammaM1) + kzz * mzMax / (mzrk / GammaM1);
        return new Check("steel.interaction", "Bending and compression (buckling)", $"{Norm} 6.3.3, Annex B", Math.Max(u61, u62),
            [new("eq. 6.61", u61, ""), new("eq. 6.62", u62, ""), new("kyy", kyy, ""), new("kyz", kyz, ""), new("kzy", kzy, ""), new("kzz", kzz, ""), new("Cmy", cmy, ""), new("Cmz", cmz, ""), new("CmLT", cmlt, ""), new("χLT", chiLT, "")],
            "Annex B (method 2), as the project chose (analysis.setDesign steelInteraction); SR EN 1993-1-1/NA's is Annex A.", done);
    }

    /// <summary>Cmi,0 of Table A.2: linear end moments 0.79 + 0.21ψ + 0.36(ψ − 0.33) N/Ncr; a simple span's uniform load 1 + 0.03 N/Ncr, its mid-span point load 1 − 0.18 N/Ncr; otherwise 1.0.</summary>
    public static double Cm0(MomentShape m, double nOverNcr) =>
        m.Linear || Math.Abs(m.Ms) < 1e-12 ? 0.79 + 0.21 * m.Psi + 0.36 * (m.Psi - 0.33) * nOverNcr
        : m.SimpleSpan ? (m.Uniform ? 1 + 0.03 * nOverNcr : 1 - 0.18 * nOverNcr)
        : 1.0;

    /// <summary>
    /// Annex A (method 1), Tables A.1 and A.2, as corrected (Corrigendum to EN 1993-1-1; the ECCS design manual, Simões da Silva et
    /// al., Tables 3.13–3.15): μ, w, npl, aLT, λ0 (lateral torsional buckling under a uniform moment), Ncr,T, the C factors and kij.
    /// </summary>
    static Check AnnexA(SteelMember m, double nc, double myMax, double mzMax, double chiY, double chiZ, double lamY, double lamZ, double chiLT, bool plastic,
        MomentShape shapeY, MomentShape shapeZ, bool done)
    {
        var p = m.P;
        var fy = m.Fy / 1000;
        var e = m.E / 1000;
        var g = m.G / 1000;
        double ncrY = Math.PI * Math.PI * e * p.I33 / (m.LcrY * m.LcrY), ncrZ = Math.PI * Math.PI * e * p.I22 / (m.LcrZ * m.LcrZ);
        var lt = m.Llt > 1e-9 ? m.Llt : m.L;
        var i0 = (p.I33 + p.I22) / p.Area;
        var ncrT = (g * p.J + Math.PI * Math.PI * e * (p.Iw ?? 0) / (lt * lt)) / i0;
        double ry = nc / ncrY, rz = nc / ncrZ, rt = nc / ncrT;
        var nrk = p.Area * fy;
        double muY = (1 - ry) / (1 - chiY * ry), muZ = (1 - rz) / (1 - chiZ * rz);
        double wy = plastic ? Math.Min(1.5, p.Z33 / p.S33) : 1, wz = plastic ? Math.Min(1.5, p.Z22 / p.S22) : 1;
        var npl = nc / (nrk / GammaM1);
        var aLT = Math.Max(0, 1 - p.J / p.I33);
        // λ0: lateral torsional buckling under a uniform moment over the unrestrained length (none when restrained).
        var lam0 = 0.0;
        var c1 = C1Of(shapeY).C1;
        if (m.Shape.Kind == "I" && m.Llt > 1e-9 && p.Iw is { } iw)
            lam0 = Math.Sqrt((plastic ? p.Z33 : p.S33) * fy / Mcr(1, m.E, m.G, p.I22, iw, p.J, m.Llt));
        var limit = 0.2 * Math.Sqrt(c1) * Math.Pow(Math.Max(0, (1 - rz) * (1 - rt)), 0.25);
        var torsional = !(p.J >= p.I33) && lam0 > limit;
        double cmy0 = Cm0(shapeY, ry), cmz0 = Cm0(shapeZ, rz);
        double cmy, cmz = cmz0, cmlt = 1;
        if (!torsional) cmy = cmy0;
        else
        {
            var epsY = myMax / nc * p.Area / p.S33;
            var r = Math.Sqrt(epsY * aLT);
            cmy = cmy0 + (1 - cmy0) * r / (1 + r);
            cmlt = Math.Max(1, cmy * cmy * aLT / Math.Sqrt(Math.Max(1e-12, (1 - rz) * (1 - rt))));
        }
        var chiLt = torsional ? chiLT : 1.0;
        double mplY = p.Z33 * fy / GammaM0, mplZ = p.Z22 * fy / GammaM0;
        var lamMax = Math.Max(lamY, lamZ);
        double kyy, kyz, kzy, kzz;
        double cyy = 1, cyz = 1, czy = 1, czz = 1;
        if (plastic)
        {
            var z4 = Math.Pow(lamZ, 4);
            var bLT = 0.5 * aLT * lam0 * lam0 * myMax / (chiLt * mplY) * mzMax / mplZ;
            var cLT = 10 * aLT * lam0 * lam0 / (5 + z4) * myMax / (cmy * chiLt * mplY);
            var dLT = 2 * aLT * lam0 / (0.1 + z4) * myMax / (cmy * chiLt * mplY) * mzMax / (cmz * mplZ);
            var eLT = 1.7 * aLT * lam0 / (0.1 + z4) * myMax / (cmy * chiLt * mplY);
            cyy = Math.Max(p.S33 / p.Z33, 1 + (wy - 1) * ((2 - 1.6 / wy * cmy * cmy * lamMax - 1.6 / wy * cmy * cmy * lamMax * lamMax) * npl - bLT));
            cyz = Math.Max(0.6 * Math.Sqrt(wz / wy) * p.S22 / p.Z22, 1 + (wz - 1) * ((2 - 14 * cmz * cmz * lamMax * lamMax / Math.Pow(wz, 5)) * npl - cLT));
            czy = Math.Max(0.6 * Math.Sqrt(wy / wz) * p.S33 / p.Z33, 1 + (wy - 1) * ((2 - 14 * cmy * cmy * lamMax * lamMax / Math.Pow(wy, 5)) * npl - dLT));
            czz = Math.Max(p.S22 / p.Z22, 1 + (wz - 1) * ((2 - 1.6 / wz * cmz * cmz * lamMax - 1.6 / wz * cmz * cmz * lamMax * lamMax - eLT) * npl));
            kyy = cmy * cmlt * muY / (1 - ry) / cyy;
            kyz = cmz * muY / (1 - rz) / cyz * 0.6 * Math.Sqrt(wz / wy);
            kzy = cmy * cmlt * muZ / (1 - ry) / czy * 0.6 * Math.Sqrt(wy / wz);
            kzz = cmz * muZ / (1 - rz) / czz;
        }
        else
        {
            kyy = cmy * cmlt * muY / (1 - ry);
            kyz = cmz * muY / (1 - rz);
            kzy = cmy * cmlt * muZ / (1 - ry);
            kzz = cmz * muZ / (1 - rz);
        }
        double myrk = (plastic ? p.Z33 : p.S33) * fy, mzrk = (plastic ? p.Z22 : p.S22) * fy;
        var u61 = nc / (chiY * nrk / GammaM1) + kyy * myMax / (chiLt * myrk / GammaM1) + kyz * mzMax / (mzrk / GammaM1);
        var u62 = nc / (chiZ * nrk / GammaM1) + kzy * myMax / (chiLt * myrk / GammaM1) + kzz * mzMax / (mzrk / GammaM1);
        var beyond = ry >= 1 || rz >= 1;
        return new Check("steel.interaction", "Bending and compression (buckling)", $"{Norm} 6.3.3, Annex A", beyond ? 99 : Math.Max(u61, u62),
            [new("eq. 6.61", u61, ""), new("eq. 6.62", u62, ""), new("kyy", kyy, ""), new("kyz", kyz, ""), new("kzy", kzy, ""), new("kzz", kzz, ""),
             new("Cmy", cmy, ""), new("Cmz", cmz, ""), new("CmLT", cmlt, ""), new("μy", muY, ""), new("μz", muZ, ""), new("Cyy", cyy, ""), new("Czy", czy, ""),
             new("npl", npl, ""), new("λ0", lam0, ""), new("Ncr,T", ncrT, "kN"), new("χLT", chiLt, "")],
            $"Annex A (method 1, the Romanian annex's); {(torsional ? "susceptible to torsional deformations (λ0 over 0.2√C1·⁴√((1 − N/Ncr,z)(1 − N/Ncr,T)))" : "not susceptible to torsional deformations: χLT = 1")}; Cmi,0 by Table A.2.", done);
    }
}
