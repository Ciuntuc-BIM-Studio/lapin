using Lapin.Analysis.Solver;
using static Lapin.Analysis.Design.Checks;

namespace Lapin.Analysis.Design;

/// <summary>A timber strength class's characteristic values (MPa; E in MPa; ρk kg/m³) and whether it is glued laminated.</summary>
public sealed record TimberClass(string Name, double Fmk, double Ft0k, double Fc0k, double Fvk, double E0mean, double E005, double Rhok, bool Glulam, string Source);

/// <summary>
/// A timber bar for its checks (§12.4): its rectangle (b along local 3, h along local 2), class, service class, the buckling
/// lengths about each axis and the lateral torsional buckling length (lef, the span by EN 1995-1-1 Table 6.1 unless given).
/// </summary>
public sealed record TimberMember(double B, double H, double L, TimberClass T, int ServiceClass, double LcrY, double LcrZ, double? Lef = null)
{
    public string LengthsSource { get; init; } = "";
    /// <summary>The span's deflection limits (EN 1995-1-1 Table 7.2's ranges, the lower ends): instantaneous L/300, final L/250 (a cantilever's twice the span).</summary>
    public bool Cantilever { get; init; }
    /// <summary>The lengths by design set's key when they come from αcr (§14.24); null, the lengths above for every set.</summary>
    public IReadOnlyDictionary<string, SetLengths>? BySet { get; init; }

    /// <summary>The member with a design set's own lengths (§14.24), or itself.</summary>
    public TimberMember ForSet(string key) => BySet != null && BySet.TryGetValue(key, out var x) ? this with { LcrY = x.Y.Lcr, LcrZ = x.Z.Lcr, LengthsSource = x.Text, BySet = null } : this;
}

/// <summary>
/// Timber members after SR EN 1995-1-1 (docs/plan-fem.md §12.4), the recommended values (γM 1.3 solid, 1.25 glulam: Table
/// 2.3; kmod Table 3.1, kdef Table 3.2), indicative: bending (6.1.6), shear (6.1.7, kcr = 0.67), tension and compression
/// along the grain (6.1.2, 6.1.4), their combinations with bending (6.2.3, 6.2.4), buckling (6.3.2) and lateral torsional
/// buckling (6.3.3), and the final deflection (2.3.2.2, 7.2).
/// </summary>
public static class TimberDesign
{
    public const string Norm = "SR EN 1995-1-1";

    /// <summary>EN 338:2016 Table 1 (C16–C30) and EN 14080:2013 Table 5 (GL, through a secondary source).</summary>
    public static readonly TimberClass[] Classes =
    [
        new("C16", 16, 8.5, 17, 3.2, 8000, 5400, 310, false, "EN 338:2016 Table 1"),
        new("C20", 20, 11.5, 19, 3.6, 9500, 6400, 330, false, "EN 338:2016 Table 1"),
        new("C24", 24, 14.5, 21, 4.0, 11000, 7400, 350, false, "EN 338:2016 Table 1"),
        new("C30", 30, 19, 24, 4.0, 12000, 8000, 380, false, "EN 338:2016 Table 1"),
        new("GL24h", 24, 19.2, 24, 3.5, 11500, 9600, 385, true, "EN 14080:2013 Table 5"),
        new("GL28h", 28, 22.3, 28, 3.5, 12600, 10500, 425, true, "EN 14080:2013 Table 5"),
        new("GL30h", 30, 24, 30, 3.5, 13600, 11300, 430, true, "EN 14080:2013 Table 5"),
        new("GL24c", 24, 17, 21.5, 3.5, 11000, 9100, 365, true, "EN 14080:2013 Table 5"),
        new("GL28c", 28, 19.5, 24, 3.5, 12500, 10400, 390, true, "EN 14080:2013 Table 5"),
        new("GL30c", 30, 19.5, 24.5, 3.5, 13000, 10800, 390, true, "EN 14080:2013 Table 5"),
    ];

    public static TimberClass? Find(string? grade) => Classes.FirstOrDefault(c => string.Equals(c.Name, grade, StringComparison.OrdinalIgnoreCase));

    /// <summary>kmod (Table 3.1, solid timber and glulam) by service class and load duration.</summary>
    public static double Kmod(int serviceClass, string duration)
    {
        var i = Math.Max(0, Array.IndexOf(AnalysisRun.Durations, duration));
        double[] sc12 = [0.60, 0.70, 0.80, 0.90, 1.10], sc3 = [0.50, 0.55, 0.65, 0.70, 0.90];
        return (serviceClass >= 3 ? sc3 : sc12)[i];
    }

    /// <summary>kdef (Table 3.2, solid timber and glulam): 0.6, 0.8, 2.0 for service classes 1, 2, 3.</summary>
    public static double Kdef(int serviceClass) => serviceClass switch { <= 1 => 0.6, 2 => 0.8, _ => 2.0 };

    public static double GammaM(TimberClass t) => t.Glulam ? 1.25 : 1.3;

    /// <summary>kh (3.2, 3.3): solid timber min((150/h)^0.2, 1.3) below 150 mm, glulam min((600/h)^0.1, 1.1) below 600 mm.</summary>
    public static double Kh(TimberClass t, double h) =>
        t.Glulam ? (h < 0.6 ? Math.Min(Math.Pow(0.6 / h, 0.1), 1.1) : 1) : (h < 0.15 ? Math.Min(Math.Pow(0.15 / h, 0.2), 1.3) : 1);

    /// <summary>kc (6.3.2, eq. 6.25–6.29) for a slenderness λ.</summary>
    public static (double Kc, double LambdaRel) Kc(TimberClass t, double lambda)
    {
        var rel = lambda / Math.PI * Math.Sqrt(t.Fc0k / t.E005);
        if (rel <= 0.3) return (1, rel);
        var beta = t.Glulam ? 0.1 : 0.2;
        var k = 0.5 * (1 + beta * (rel - 0.3) + rel * rel);
        return (Math.Min(1, 1 / (k + Math.Sqrt(k * k - rel * rel))), rel);
    }

    /// <summary>kcrit (6.3.3, eq. 6.32–6.34) of a rectangle b × h over lef (softwood).</summary>
    public static (double Kcrit, double LambdaRelM, double SigmaCrit) Kcrit(TimberClass t, double b, double h, double lef)
    {
        var crit = 0.78 * b * b * t.E005 / (h * lef);
        var rel = Math.Sqrt(t.Fmk / crit);
        var k = rel <= 0.75 ? 1 : rel <= 1.4 ? 1.56 - 0.75 * rel : 1 / (rel * rel);
        return (k, rel, crit);
    }

    /// <summary>Every check of a timber member, the worst over the sets; with the deflections when the cases' are given.</summary>
    public static List<Check> Check(TimberMember m, IReadOnlyList<ForceSet> sets)
    {
        var checks = Worst(sets, f => Set(m.ForSet(f.Key), f));
        return checks;
    }

    static IEnumerable<Check> Set(TimberMember m, ForceSet f)
    {
        var t = m.T;
        var kmod = Kmod(m.ServiceClass, f.Duration);
        var gm = GammaM(t);
        double Fd(double fk) => kmod * fk / gm * 1000; // kN/m²
        var (b, h) = (m.B, m.H);
        var a = b * h;
        var wy = b * h * h / 6; // about the strong axis (Lapin's Mz)
        var wz = h * b * b / 6;
        double khY = Kh(t, h), khZ = Kh(t, b);
        double fmy = Fd(t.Fmk) * khY, fmz = Fd(t.Fmk) * khZ, ft0 = Fd(t.Ft0k) * Kh(t, Math.Max(b, h)), fc0 = Fd(t.Fc0k), fv = Fd(t.Fvk);
        const double km = 0.7;
        IReadOnlyList<Shown> Base() => [new("kmod", kmod, ""), new("γM", gm, ""), new("duration", Array.IndexOf(AnalysisRun.Durations, f.Duration), "")];
        string Dur = $"kmod {F(kmod, "0.00")} ({f.Duration}, service class {m.ServiceClass}); γM {F(gm, "0.00")}.";

        // Bending, tension, compression at every section (6.1.6, 6.2.3, 6.2.4).
        Check? worst = null;
        foreach (var s in f.Sections)
        {
            double smy = Math.Abs(s.Mz) / wy, smz = Math.Abs(s.My) / wz;
            double u1 = smy / fmy + km * smz / fmz, u2 = km * smy / fmy + smz / fmz;
            double u, axial;
            string how;
            if (s.N > 0) { axial = s.N / a / ft0; u = axial + Math.Max(u1, u2); how = "6.2.3: σt,0/ft,0 + σm/fm"; }
            else { axial = Math.Pow(-s.N / a / fc0, 2); u = axial + Math.Max(u1, u2); how = "6.2.4: (σc,0/fc,0)² + σm/fm"; }
            if (worst == null || u > worst.Utilisation)
                worst = new Check("timber.section", "Bending and axial force", $"{Norm} 6.1.6, 6.2.3, 6.2.4", u,
                    [new("N", s.N, "kN"), new("σm,y,d", smy / 1000, "MPa"), new("fm,y,d", fmy / 1000, "MPa"), new("σm,z,d", smz / 1000, "MPa"), new("fm,z,d", fmz / 1000, "MPa"), new("kh", khY, ""), .. Base()],
                    $"{how}; km = 0.7. {Dur}") { X = s.X };
        }
        if (worst != null) yield return worst;

        // Shear (6.1.7): τ = 1.5 V/(kcr b h), kcr = 0.67, both directions.
        var vs = f.Sections.MaxBy(s => Math.Max(Math.Abs(s.Vy), Math.Abs(s.Vz)));
        var v = Math.Max(Math.Abs(vs.Vy), Math.Abs(vs.Vz));
        var tau = 1.5 * v / (0.67 * a);
        yield return new Check("timber.shear", "Shear", $"{Norm} 6.1.7", tau / fv, [new("VEd", v, "kN"), new("τd", tau / 1000, "MPa"), new("fv,d", fv / 1000, "MPa"), new("kcr", 0.67, ""), .. Base()], Dur) { X = vs.X };

        // Buckling (6.3.2) under compression with bending (eq. 6.23, 6.24).
        var nc = f.Compression;
        var mz = f.MaxAbs(5);
        var my = f.MaxAbs(4);
        if (nc > 1e-9)
        {
            var (kcy, ry) = Kc(t, m.LcrY / (h / Math.Sqrt(12)));
            var (kcz, rz) = Kc(t, m.LcrZ / (b / Math.Sqrt(12)));
            var sc = nc / a;
            double smy = mz / wy, smz = my / wz;
            var u23 = sc / (kcy * fc0) + smy / fmy + km * smz / fmz;
            var u24 = sc / (kcz * fc0) + km * smy / fmy + smz / fmz;
            yield return new Check("timber.buckling", "Compression with buckling", $"{Norm} 6.3.2", Math.Max(u23, u24),
                [new("NEd", nc, "kN"), new("λrel,y", ry, ""), new("λrel,z", rz, ""), new("kc,y", kcy, ""), new("kc,z", kcz, ""), new("eq. 6.23", u23, ""), new("eq. 6.24", u24, ""), .. Base()],
                $"{Dur} {m.LengthsSource}".Trim());
        }

        // Lateral torsional buckling (6.3.3) of a beam bent about its strong axis, h > b.
        if (mz > 1e-9 && h > b)
        {
            var shape = MomentShape.Of(f, 5);
            var lef = m.Lef ?? (m.Cantilever ? (shape.Uniform ? 0.5 : 0.8) * m.L : shape.Linear ? m.L : shape.SimpleSpan ? (shape.Uniform ? 0.9 : 0.8) * m.L : m.L);
            if (lef > 1e-9)
            {
                var (kcrit, rel, crit) = Kcrit(t, b, h, lef);
                var smd = mz / wy;
                var (kcz, _) = Kc(t, m.LcrZ / (b / Math.Sqrt(12)));
                var u = Math.Pow(smd / (kcrit * fmy), 2) + (nc > 0 ? nc / a / (kcz * fc0) : 0);
                yield return new Check("timber.ltb", "Lateral torsional buckling", $"{Norm} 6.3.3", nc > 0 ? u : smd / (kcrit * fmy),
                    [new("lef", lef, "m"), new("σm,crit", crit, "MPa"), new("λrel,m", rel, ""), new("kcrit", kcrit, ""), new("σm,d", smd / 1000, "MPa"), .. Base()],
                    (nc > 0 ? "eq. 6.35 with compression. " : "eq. 6.33. ") + "lef by Table 6.1 from the moment's shape (the load at the centre of gravity) unless given. " + Dur);
            }
        }
    }

    /// <summary>
    /// The final deflection (2.3.2.2, eq. 2.2–2.3 with ψ2): wfin = Σ winst,G (1 + kdef) + winst,Q1 (1 + ψ2,1 kdef) + Σ winst,Qi (ψ0,i + ψ2,i kdef),
    /// the worst over the characteristic combinations, against L/250 (a cantilever's 2L/250); and winst against L/300.
    /// </summary>
    public static Check Deflection(TimberMember m, IReadOnlyList<(string Key, string Name, double[] Inst, double[] Fin)> combos)
    {
        Check? worst = null;
        var span = m.Cantilever ? 2 * m.L : m.L;
        foreach (var (key, name, inst, fin) in combos)
        {
            var wi = inst.Max(Math.Abs);
            var wf = fin.Max(Math.Abs);
            var u = Math.Max(wi / (span / 300), wf / (span / 250));
            if (worst == null || u > worst.Utilisation)
                worst = new Check("timber.deflection", "Deflection (SLS)", $"{Norm} 2.3.2.2, 7.2", u,
                    [new("winst", wi * 1000, "mm"), new("limit inst", span / 300 * 1000, "mm"), new("wfin", wf * 1000, "mm"), new("limit fin", span / 250 * 1000, "mm"), new("kdef", Kdef(m.ServiceClass), "")],
                    "Limits L/300 and L/250 (Table 7.2's ranges, indicative; the designer's to set); E0,mean, shear deformation included as the analysis has it.")
                { Combination = key, CombinationName = name };
        }
        return worst ?? new Check("timber.deflection", "Deflection (SLS)", $"{Norm} 7.2", 0, [], "No characteristic combination.", false);
    }
}
