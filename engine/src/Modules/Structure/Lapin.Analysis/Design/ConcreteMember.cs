using Lapin.Analysis.Solver;
using static Lapin.Analysis.Design.Checks;

namespace Lapin.Analysis.Design;

/// <summary>
/// A reinforced concrete bar for its checks (§12.3): its rectangle (<see cref="B"/> along local 3, <see cref="H"/> along local
/// 2, the depth), its length, a beam or a column, the buckling lengths (a column's l0 about each axis: <see cref="L0Y"/> for
/// bending about its strong axis), the span/depth system's K (Table 7.4N), and what its type provides: the stirrups' Asw/s
/// (m²/m) zone by zone along it and a column's bars (m²), each null when the rebar module's type settings give none.
/// </summary>
public sealed record ConcreteMember(double B, double H, double L, bool Column, ConcreteValues C, double L0Y, double L0Z, double SystemK = 1.0, string System = "simply supported")
{
    public IReadOnlyList<StirrupZone>? ProvidedStirrups { get; init; }
    public double? ProvidedBars { get; init; }
    public string LengthsSource { get; init; } = "";
    public double Ac => B * H;
}

/// <summary>A stretch of a bar (m from its i end) and the Asw/s (m²/m) its type's stirrups give there.</summary>
public sealed record StirrupZone(double From, double To, double AswS);

/// <summary>What a concrete member needs: bottom and top bars (beams), the bars all round (columns), the stirrups (m², m²/m).</summary>
public sealed record Required(double Bottom, double Top, double Total, double Stirrups, double Side = 0);

/// <summary>The checks of a reinforced concrete beam or column (§12.3) and the reinforcement it needs.</summary>
public static class ConcreteMembers
{
    const string N = ConcreteDesign.Norm;
    static double Cm2(double m2) => m2 * 1e4;

    public static (List<Check> Checks, Required Need) Check(ConcreteMember m, IReadOnlyList<ForceSet> sets)
    {
        var need = new double[5]; // bottom, top, total, stirrups, side
        var checks = Worst(sets, f => m.Column ? Column(m, f, need) : Beam(m, f, need));
        var c = m.C;
        if (!m.Column)
        {
            // Span/depth (7.4.2) with the bottom bars the bending needs (ρ) and the compression bars (ρ′), against the actual L/d.
            var d = m.H - c.Edge;
            var (rho, rho2) = (Math.Max(need[0], need[1]) / (m.B * d), 0.0);
            var limit = ConcreteDesign.SpanDepth(rho, rho2, c.FckMpa, m.SystemK);
            var actual = m.L / d;
            checks.Add(new Check("concrete.deflection", "Deflection (span/depth)", $"{N} 7.4.2, eq. 7.16", actual / limit,
                [new("L/d", actual, ""), new("limit", limit, ""), new("K", m.SystemK, ""), new("ρ", rho * 100, "%"), new("ρ0", Math.Sqrt(c.FckMpa) * 0.1, "%")],
                $"{m.System} (Table 7.4N); without the 310/σs and long-span factors (7.17)."));
            if (m.ProvidedStirrups is { Count: > 0 } ps) checks.Add(StirrupsProvided(m, sets, ps));
        }
        else if (m.ProvidedBars is { } pb)
            checks.Add(new Check("concrete.barsProvided", "Bars provided", $"{N} 6.1, 5.8.8", need[2] / pb, [new("As required", Cm2(need[2]), "cm²"), new("As provided", Cm2(pb), "cm²")],
                "From the type's bars (the rebar module's settings)."));
        return (checks, new Required(need[0], need[1], need[2], need[3], need[4]));
    }

    /// <summary>
    /// The type's stirrups against what the shear needs where each zone lies: in each zone the largest Asw/s its sections ask for
    /// (its ends' and the stations' inside it) in any combination over the zone's own Asw/s.
    /// </summary>
    static Check StirrupsProvided(ConcreteMember m, IReadOnlyList<ForceSet> sets, IReadOnlyList<StirrupZone> zones)
    {
        var req = new double[zones.Count];
        var gov = new ForceSet?[zones.Count];
        foreach (var f in sets)
        {
            var (bottom, top) = BeamBars(m, f);
            for (var z = 0; z < zones.Count; z++)
            {
                var (a, b) = (zones[z].From, zones[z].To);
                var at = f.Sections.Where(s => s.X > a && s.X < b).Append(f.At(a)).Append(f.At(b));
                var r = at.Max(s => StirrupNeed(m, s, bottom, top));
                if (r > req[z] || gov[z] == null) (req[z], gov[z]) = (r, f);
            }
        }
        var u = zones.Select((z, i) => z.AswS > 0 ? req[i] / z.AswS : 99).ToArray();
        var w = Array.IndexOf(u, u.Max());
        var values = new List<Shown>();
        for (var i = 0; i < zones.Count; i++)
        {
            var label = zones.Count == 1 ? "" : $" ({F(zones[i].From, "0.##")}–{F(zones[i].To, "0.##")} m)";
            values.Add(new($"Asw/s required{label}", double.IsFinite(req[i]) ? Cm2(req[i]) : 0, "cm²/m"));
            values.Add(new($"Asw/s provided{label}", Cm2(zones[i].AswS), "cm²/m"));
        }
        return new Check("concrete.stirrupsProvided", "Stirrups provided", $"{N} 6.2.3", u[w], values,
            "From the type's stirrups (the rebar module's settings), each zone against the shear where it lies; the zones taken over the bar's analytical length.")
            { Combination = gov[w]?.Key, CombinationName = gov[w]?.Name, X = (zones[w].From + zones[w].To) / 2 };
    }

    /// <summary>A beam's bottom and top bars in one combination: the bending's, at least As,min.</summary>
    static (double Bottom, double Top) BeamBars(ConcreteMember m, ForceSet f)
    {
        var c = m.C;
        var d = m.H - c.Edge;
        var asMin = ConcreteDesign.MinAs(m.B, d, c);
        var sag = ConcreteDesign.Bending(Math.Max(0, f.Sections.Max(s => s.Mz)), m.B, d, c.Edge, c);
        var hog = ConcreteDesign.Bending(Math.Max(0, -f.Sections.Min(s => s.Mz)), m.B, d, c.Edge, c);
        return (Math.Max(asMin, Math.Max(sag.As, hog.As2)), Math.Max(asMin, Math.Max(hog.As, sag.As2)));
    }

    /// <summary>The stirrups (m²/m) a beam's section needs for its Vy (6.2): the minimum where VEd ≤ VRd,c; infinite past VRd,max.</summary>
    static double StirrupNeed(ConcreteMember m, Section s, double bottom, double top)
    {
        var c = m.C;
        var d = m.H - c.Edge;
        var vEd = Math.Abs(s.Vy);
        var (vrdc, _, _) = ConcreteDesign.VRdc(m.B, d, s.Mz >= 0 ? bottom : top, c);
        return ConcreteDesign.Stirrups(vEd, m.B, d, c) is { } st ? (vEd <= vrdc ? st.AswSMin : st.AswS) : double.PositiveInfinity;
    }

    static IEnumerable<Check> Beam(ConcreteMember m, ForceSet f, double[] need)
    {
        var c = m.C;
        var d = m.H - c.Edge;
        var asMin = ConcreteDesign.MinAs(m.B, d, c);
        var asMax = 0.04 * m.Ac;
        // Bending about the strong axis (Lapin's Mz: sagging positive, the bottom in tension).
        var sag = f.Sections.MaxBy(s => s.Mz);
        var hog = f.Sections.MinBy(s => s.Mz);
        var (asB, as2B, kB, zB) = ConcreteDesign.Bending(Math.Max(0, sag.Mz), m.B, d, c.Edge, c);
        var (asT, as2T, kT, zT) = ConcreteDesign.Bending(Math.Max(0, -hog.Mz), m.B, d, c.Edge, c);
        double bottom = Math.Max(asMin, Math.Max(asB, as2T)), top = Math.Max(asMin, Math.Max(asT, as2B));
        need[0] = Math.Max(need[0], bottom);
        need[1] = Math.Max(need[1], top);
        need[2] = Math.Max(need[2], bottom + top);
        var gov = asB >= asT ? (sag, asB, as2B, kB, zB, "bottom") : (hog, asT, as2T, kT, zT, "top");
        yield return new Check("concrete.bending", "Bending reinforcement", $"{N} 6.1, 9.2.1.1", (bottom + top) / asMax,
            [new("MEd", gov.Item1.Mz, "kN·m"), new("K", gov.Item4, ""), new("K'", c.KPrime, ""), new("z", gov.Item5, "m"), new("As bottom", Cm2(bottom), "cm²"), new("As top", Cm2(top), "cm²"),
             new("As,min", Cm2(asMin), "cm²"), new("As,max", Cm2(asMax), "cm²")],
            (kB > c.KPrime || kT > c.KPrime ? "Doubly reinforced (K > K'). " : "") + "Utilisation: the bars needed over As,max = 0.04 Ac.") { X = gov.Item1.X };
        // About the weak axis (My), the same rule across the width.
        var myMax = f.MaxAbs(4);
        if (myMax > 1e-6)
        {
            var dw = m.B - c.Edge;
            var (asw, as2w, kw, _) = ConcreteDesign.Bending(myMax, m.H, dw, c.Edge, c);
            var side = asw + as2w;
            need[4] = Math.Max(need[4], side);
            yield return new Check("concrete.bendingWeak", "Bending about the weak axis", $"{N} 6.1", side / asMax,
                [new("Mz,Ed", myMax, "kN·m"), new("K", kw, ""), new("As each side", Cm2(side), "cm²")], "The same rule across the width; with the strong axis' bars, not summed as biaxial bending.");
        }
        // Shear (6.2): along the depth (Vy), with the tension bars the bending needs as ρl.
        var vs = f.Sections.MaxBy(s => Math.Abs(s.Vy));
        var vEd = Math.Abs(vs.Vy);
        var asl = vs.Mz >= 0 ? bottom : top;
        var (vrdc, k, vmin) = ConcreteDesign.VRdc(m.B, d, asl, c);
        var st = ConcreteDesign.Stirrups(vEd, m.B, d, c);
        var vMax1 = ConcreteDesign.VRdMax(m.B, d, 1, c);
        if (st is { } s1)
        {
            var asws = vEd <= vrdc ? s1.AswSMin : s1.AswS;
            need[3] = Math.Max(need[3], asws);
            yield return new Check("concrete.shear", "Shear (stirrups)", $"{N} 6.2.2, 6.2.3, 9.2.2", vEd / vMax1,
                [new("VEd", vEd, "kN"), new("VRd,c", vrdc, "kN"), new("k", k, ""), new("cot θ", s1.CotTheta, ""), new("VRd,max", s1.VRdMax, "kN"), new("Asw/s", Cm2(asws), "cm²/m"), new("Asw/s,min", Cm2(s1.AswSMin), "cm²/m")],
                (vEd <= vrdc ? "VEd ≤ VRd,c: the minimum stirrups. " : "") + "Utilisation: VEd over VRd,max at cot θ = 1. Torsion not checked.") { X = vs.X };
        }
        else yield return new Check("concrete.shear", "Shear (stirrups)", $"{N} 6.2.3", vEd / vMax1, [new("VEd", vEd, "kN"), new("VRd,max (cot θ = 1)", vMax1, "kN")], "VEd over VRd,max: the section is too small.") { X = vs.X };
        if (Math.Abs(f.Tension) > 0.1 * m.Ac * c.Fcd || f.Compression > 0.1 * m.Ac * c.Fcd)
            yield return new Check("concrete.axial", "Axial force in a beam", N, 0, [new("N", Math.Max(f.Tension, f.Compression), "kN")], "The beam carries an axial force over 0.1 Ac fcd: check it as a column.", false);
    }

    /// <summary>The design moment about one axis (5.8.8): end moments M01, M02, the largest along, e0, the imperfection l0/400 and the second order M2.</summary>
    static (double M, double M2, bool Slender, double Lambda, double LambdaLim) DesignMoment(ConcreteMember m, double nEd, double hAxis, double l0, double ma, double mb, double mSpan, double asTotal)
    {
        var c = m.C;
        var d = hAxis - c.Edge;
        var i = hAxis / Math.Sqrt(12);
        var lambda = l0 / i;
        var n = nEd / (m.Ac * c.Fcd);
        var lim = n > 0 ? 20 * 0.7 * 1.1 * 0.7 / Math.Sqrt(n) : double.PositiveInfinity;
        var ei = l0 / 400;
        var (m02, m01) = Math.Abs(ma) >= Math.Abs(mb) ? (ma, mb) : (mb, ma);
        var a02 = Math.Abs(m02) + nEd * ei;
        var a01 = Math.Sign(m01 * (m02 == 0 ? 1 : Math.Sign(m02))) * Math.Abs(m01) + nEd * ei;
        var e0 = Math.Max(hAxis / 30, 0.020);
        var first = Math.Max(Math.Max(a02, Math.Abs(mSpan) + nEd * ei), nEd * e0);
        if (lambda <= lim || nEd <= 0) return (first, 0, false, lambda, lim);
        var omega = asTotal * c.Fyd / (m.Ac * c.Fcd);
        var nu = 1 + omega;
        var kr = Math.Min(1, (nu - n) / (nu - 0.4));
        var curvature = Math.Max(0, kr) * c.EpsYd / (0.45 * d);
        var m2 = nEd * curvature * l0 * l0 / 10;
        var m0e = Math.Max(0.6 * a02 + 0.4 * a01, 0.4 * a02);
        var md = Math.Max(Math.Max(a02, m0e + m2), Math.Max(a01 + 0.5 * m2, nEd * e0));
        return (Math.Max(md, Math.Abs(mSpan) + nEd * ei + m2), m2, true, lambda, lim);
    }

    static IEnumerable<Check> Column(ConcreteMember m, ForceSet f, double[] need)
    {
        var c = m.C;
        var nEd = f.Sections.Max(s => -s.N); // compression positive (may be negative: tension)
        var (a0, a1) = (f.Sections[0], f.Sections[^1]);
        var spanY = f.Sections.Max(s => Math.Abs(s.Mz));
        var spanZ = f.Sections.Max(s => Math.Abs(s.My));
        var asMin = Math.Max(0.10 * Math.Max(0, nEd) / c.Fyd, 0.002 * m.Ac);
        var asMax = 0.04 * m.Ac;
        double? found = null;
        (double U, double MY, double MZ, double MRY, double MRZ, double A, double M2Y, double M2Z, bool SY, bool SZ, double LY, double LZ, double LimY) last = default;
        for (var k = 0; k <= 400; k++)
        {
            var asT = Math.Max(asMin, m.Ac * 0.001 * k);
            if (asT > asMax + 1e-12) break;
            // About the strong axis (Lapin's Mz, the depth H the lever) and the weak one (My, the width B).
            var (my, m2y, sy, ly, limy) = DesignMoment(m, Math.Max(0, nEd), m.H, m.L0Y, a0.Mz, a1.Mz, spanY, asT);
            var (mz, m2z, sz, lz, _) = DesignMoment(m, Math.Max(0, nEd), m.B, m.L0Z, a0.My, a1.My, spanZ, asT);
            var mry = ConcreteDesign.MomentResistance(nEd, m.B, m.H, c.Edge, asT, c);
            var mrz = ConcreteDesign.MomentResistance(nEd, m.H, m.B, c.Edge, asT, c);
            var nrd = m.Ac * c.Fcd + asT * c.Fyd;
            double u;
            var a = ConcreteDesign.BiaxialA(Math.Max(0, nEd) / nrd);
            if (mry is not { } ry || mrz is not { } rz) u = 99;
            else u = Math.Pow(Math.Pow(my / Math.Max(ry, 1e-9), a) + Math.Pow(mz / Math.Max(rz, 1e-9), a), 1 / a);
            last = (u, my, mz, mry ?? 0, mrz ?? 0, asT, m2y, m2z, sy, sz, ly, lz, limy);
            if (u <= 1) { found = asT; break; }
        }
        var need2 = found ?? asMax;
        need[2] = Math.Max(need[2], need2);
        yield return new Check("concrete.column", "Axial force and biaxial bending", $"{N} 5.8.8, 6.1, 5.39, 9.5.2", found is { } fa ? fa / asMax : Math.Max(1.0001, last.U),
            [new("NEd", nEd, "kN"), new("My,Ed (strong)", last.MY, "kN·m"), new("Mz,Ed (weak)", last.MZ, "kN·m"), new("MRd,y", last.MRY, "kN·m"), new("MRd,z", last.MRZ, "kN·m"),
             new("M2 (strong)", last.M2Y, "kN·m"), new("M2 (weak)", last.M2Z, "kN·m"), new("λ (strong)", last.LY, ""), new("λ (weak)", last.LZ, ""), new("λlim", double.IsFinite(last.LimY) ? last.LimY : 0, ""),
             new("As required", Cm2(need2), "cm²"), new("As,min", Cm2(asMin), "cm²"), new("As,max", Cm2(asMax), "cm²")],
            (found == null ? "Over 4 %: the section is too small. " : "") + (last.SY || last.SZ ? "Slender: second order by nominal curvature, Kφ = 1 (creep not taken). " : "First order only: λ ≤ λlim. ") +
            "e0 = max(h/30, 20 mm), imperfection l0/400; bars symmetric, half on each face; utilisation: the bars needed over As,max. " + m.LengthsSource);
        // Shear in a column: the larger of the two directions with the minimum stirrups.
        var vEd = Math.Max(f.MaxAbs(1), f.MaxAbs(2));
        var d = m.H - c.Edge;
        var vMax1 = ConcreteDesign.VRdMax(Math.Min(m.B, m.H), d, 1, c);
        if (ConcreteDesign.Stirrups(vEd, Math.Min(m.B, m.H), d, c) is { } st)
        {
            need[3] = Math.Max(need[3], st.AswS);
            yield return new Check("concrete.shear", "Shear (stirrups)", $"{N} 6.2.3", vEd / vMax1, [new("VEd", vEd, "kN"), new("cot θ", st.CotTheta, ""), new("Asw/s", Cm2(st.AswS), "cm²/m")],
                "Axial compression's benefit (σcp) not taken.");
        }
        else yield return new Check("concrete.shear", "Shear (stirrups)", $"{N} 6.2.3", vEd / vMax1, [new("VEd", vEd, "kN"), new("VRd,max", vMax1, "kN")], "VEd over VRd,max: the section is too small.");
    }
}
