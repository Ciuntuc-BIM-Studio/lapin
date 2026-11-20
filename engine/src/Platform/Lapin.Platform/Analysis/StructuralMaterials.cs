using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;

namespace Lapin.Platform.Analysis;

/// <summary>One value of a material for analysis, SI (Pa, kg/m³, a ratio), and where it was read; null when nothing gives it.</summary>
public sealed record MaterialValue(double Value, string Source);

/// <summary>
/// A material as an analysis program needs it, SI: kind (steel, concrete, timber, masonry, other), E, ν, fy, f'c and the
/// density, each with its source, null when the project and the standard named say nothing. The note says what is missing.
/// </summary>
public sealed record StructuralMaterial(string Id, string Name, string Kind, MaterialValue? E, MaterialValue? Poisson, MaterialValue? Fy, MaterialValue? Fc,
    MaterialValue? Density, string? Grade, string? Standard, IReadOnlyList<string> Sources);

/// <summary>
/// A material's mechanical values (docs/jg-structures.md, Materials): first the project's own, IFC's standard sets
/// (Pset_MaterialMechanical.YoungModulus, PoissonRatio; Pset_MaterialSteel.YieldStress; Pset_MaterialConcrete.CompressiveStrength;
/// Pset_MaterialCommon.MassDensity) in IFC's SI units, a value outside what any building material has refused rather than
/// guessed at another unit; then the strength class its name says (C25/30, S355) by EN 1992-1-1 Table 3.1 and EN 1993-1-1
/// Table 3.1 and §3.2.6; the density of reinforced concrete and of steel by EN 1991-1-1 Annex A and EN 1993-1-1 §3.2.6.
/// Anything else stays null.
/// </summary>
public static partial class StructuralMaterials
{
    /// <summary>EN 1992-1-1 Table 3.1: fck (MPa) → Ecm (GPa) for the normal-weight classes.</summary>
    static readonly Dictionary<string, (double Fck, double Ecm)> Concrete = new()
    {
        ["C12/15"] = (12, 27), ["C16/20"] = (16, 29), ["C20/25"] = (20, 30), ["C25/30"] = (25, 31), ["C30/37"] = (30, 33), ["C35/45"] = (35, 34),
        ["C40/50"] = (40, 35), ["C45/55"] = (45, 36), ["C50/60"] = (50, 37), ["C55/67"] = (55, 38), ["C60/75"] = (60, 39), ["C70/85"] = (70, 41),
        ["C80/95"] = (80, 42), ["C90/105"] = (90, 44),
    };

    /// <summary>EN 1993-1-1 Table 3.1 (EN 10025-2, -4): fy (MPa) for a nominal thickness t ≤ 40 mm.</summary>
    static readonly Dictionary<string, double> Steel = new() { ["S235"] = 235, ["S275"] = 275, ["S355"] = 355, ["S420"] = 420, ["S450"] = 440, ["S460"] = 460 };

    /// <summary>
    /// Structural timber: EN 338:2016 Table 1 (softwood C classes) and EN 14080:2013 Table 5 (glued laminated GL): E0,mean (GPa)
    /// and the mean density ρmean (kg/m³). Timber's Poisson ratio is not in either (an orthotropic material): it stays null.
    /// </summary>
    static readonly Dictionary<string, (double E, double Rho, string Standard)> Timber = new()
    {
        ["C14"] = (7.0, 350, "EN 338:2016"), ["C16"] = (8.0, 370, "EN 338:2016"), ["C18"] = (9.0, 380, "EN 338:2016"), ["C20"] = (9.5, 400, "EN 338:2016"),
        ["C22"] = (10.0, 410, "EN 338:2016"), ["C24"] = (11.0, 420, "EN 338:2016"), ["C27"] = (11.5, 430, "EN 338:2016"), ["C30"] = (12.0, 460, "EN 338:2016"),
        ["GL24h"] = (11.5, 420, "EN 14080:2013"), ["GL28h"] = (12.6, 460, "EN 14080:2013"), ["GL30c"] = (13.0, 430, "EN 14080:2013"),
    };

    [GeneratedRegex(@"\b(C(?:14|16|18|20|22|24|27|30)|GL(?:24h|28h|30c))\b(?!\s?/)", RegexOptions.IgnoreCase)] private static partial Regex TimberClass();
    [GeneratedRegex(@"\bC\s?(\d{2})\s?/\s?(\d{2,3})\b", RegexOptions.IgnoreCase)] private static partial Regex ConcreteClass();
    [GeneratedRegex(@"\bS\s?(235|275|355|420|450|460)", RegexOptions.IgnoreCase)] private static partial Regex SteelGrade();

    public static StructuralMaterial Of(IModelView m, Entity material)
    {
        var name = material.Name;
        var fields = MaterialFields.Of(material);
        var sources = new List<string>();
        MaterialValue? Own(string key, double min, double max, string unit)
        {
            if (fields.Param(key) is not { } p || Json.Number(p.Value) is not { } v) return null;
            if (!(v >= min && v <= max) || !double.IsFinite(v))
            {
                sources.Add($"{key} = {v.ToString(CultureInfo.InvariantCulture)} is not a value in {unit} (IFC's unit): left out");
                return null;
            }
            return new(v, $"project: material „{name}”, {key} ({unit})");
        }
        var e = Own("Pset_MaterialMechanical.YoungModulus", 1e6, 1e12, "Pa");
        var nu = Own("Pset_MaterialMechanical.PoissonRatio", 0, 0.5, "a ratio");
        var fy = Own("Pset_MaterialSteel.YieldStress", 1e6, 5e9, "Pa");
        var fc = Own("Pset_MaterialConcrete.CompressiveStrength", 1e6, 5e8, "Pa");
        MaterialValue? density = MaterialMeasure.DensityOf(material) is { } rho && rho is > 1 and < 25000
            ? new(rho, $"project: material „{name}”, density (kg/m³)") : null;

        var lower = $"{name} {fields.Category}".ToLowerInvariant();
        string kind = "other";
        string? grade = null, standard = null;
        if (ConcreteClass().Match(name) is { Success: true } cm && Concrete.TryGetValue($"C{cm.Groups[1].Value}/{cm.Groups[2].Value}", out var cc))
        {
            kind = "concrete";
            grade = $"C{cm.Groups[1].Value}/{cm.Groups[2].Value}";
            standard = "EN 1992-1-1:2004";
            e ??= new(cc.Ecm * 1e9, $"EN 1992-1-1:2004 Table 3.1, {grade}: Ecm = {cc.Ecm} GPa");
            fc ??= new(cc.Fck * 1e6, $"EN 1992-1-1:2004 Table 3.1, {grade}: fck = {cc.Fck} MPa (characteristic cylinder strength)");
            nu ??= new(0.2, "EN 1992-1-1:2004 §3.1.3(4): ν = 0.2 for uncracked concrete");
        }
        else if (SteelGrade().Match(name) is { Success: true } sm && !lower.Contains("rebar") && !lower.Contains("b500"))
        {
            kind = "steel";
            grade = $"S{sm.Groups[1].Value}";
            standard = "EN 1993-1-1:2005";
            var f = Steel[grade];
            fy ??= new(f * 1e6, $"EN 1993-1-1:2005 Table 3.1, {grade}: fy = {f} MPa for t ≤ 40 mm");
            e ??= new(210e9, "EN 1993-1-1:2005 §3.2.6: E = 210 000 MPa");
            nu ??= new(0.3, "EN 1993-1-1:2005 §3.2.6: ν = 0.3");
        }
        else if (TimberClass().Match(name) is { Success: true } tm && Timber.FirstOrDefault(t => t.Key.Equals(tm.Groups[1].Value, StringComparison.OrdinalIgnoreCase)) is { Key: not null } tc)
        {
            kind = "timber";
            grade = tc.Key;
            standard = tc.Value.Standard;
            var table = standard.StartsWith("EN 338") ? "Table 1" : "Table 5";
            e ??= new(tc.Value.E * 1e9, $"{standard} {table}, {grade}: E0,mean = {tc.Value.E} GPa (parallel to the grain)");
            density ??= new(tc.Value.Rho, $"{standard} {table}, {grade}: ρmean = {tc.Value.Rho} kg/m³");
        }
        else if (Masonry.GroupOf(lower) is { } group)
        {
            kind = "masonry";
            var m0 = Masonry.Of(fields, group, sources);
            grade = m0.Label;
            standard = "EN 1996-1-1:2005";
            e ??= m0.E;
            nu ??= new(Masonry.Poisson, "EN 1996-1-1:2005 §3.7.3: G = 0.4 E, so ν = 0.25 for an isotropic shell (indicative)");
            density ??= m0.Density;
        }
        else if (lower.Contains("concrete") || lower.Contains("beton")) kind = "concrete";
        else if (lower.Contains("steel") || lower.Contains("oțel") || lower.Contains("otel")) kind = "steel";
        else if (lower.Contains("timber") || lower.Contains("wood") || lower.Contains("lemn") || lower.Contains("glulam") || lower.Contains("clt")) kind = "timber";

        if (density == null && kind == "steel")
            density = new(7850, "EN 1993-1-1:2005 §3.2.6: ρ = 7850 kg/m³");
        if (density == null && kind == "concrete" && (lower.Contains("reinforced") || lower.Contains("armat")))
            density = new(25000 / Units.G, "EN 1991-1-1:2002 Table A.1: reinforced normal-weight concrete 25 kN/m³");
        return new(material.Id, name, kind, e, nu, fy, fc, density, grade, standard, sources);
    }
}

/// <summary>
/// The units an exchange file declares, and SI values (m, N, Pa, kg/m³) converted to them: the contract's (cm, kgf; 1 kgf =
/// 9.80665 N exactly) or SI with kN (proposed for the contract's 1.1).
/// </summary>
public sealed record Units(string Name, string Length, string Force, string StressUnit, string WeightDensityUnit, string Area, string Inertia, string Modulus, string Warping,
    double PerMetre, double PerNewton)
{
    /// <summary>Standard gravity, m/s² (CGPM 1901): 1 kgf = 9.80665 N.</summary>
    public const double G = 9.80665;

    public static readonly Units Contract = new("contract", "cm", "kgf", "kgf/cm2", "kgf/cm3", "cm2", "cm4", "cm3", "cm6", 100, 1 / G);
    public static readonly Units Si = new("si", "m", "kN", "kN/m2", "kN/m3", "m2", "m4", "m3", "m6", 1, 1e-3);

    public static Units Of(string? name) => name?.ToLowerInvariant() is "si" ? Si : Contract;

    public double L(double metres) => metres * PerMetre;
    public double L2(double m2) => m2 * PerMetre * PerMetre;
    public double L3(double m3) => m3 * PerMetre * PerMetre * PerMetre;
    public double L4(double m4) => m4 * Math.Pow(PerMetre, 4);
    /// <summary>A stress (Pa = N/m²) in force per length².</summary>
    public double Stress(double pa) => pa * PerNewton / (PerMetre * PerMetre);
    /// <summary>A density (kg/m³) as a weight per volume: ρ·g in force per length³.</summary>
    public double WeightDensity(double kgPerM3) => kgPerM3 * G * PerNewton / Math.Pow(PerMetre, 3);

    public JsonObject ToJson() => new()
    {
        ["length"] = Length, ["force"] = Force, ["mass"] = "kg", ["temperature"] = "C", ["stress"] = StressUnit, ["weight_density"] = WeightDensityUnit,
        ["section_area"] = Area, ["section_inertia"] = Inertia, ["section_modulus"] = Modulus, ["warping_constant"] = Warping,
    };
}

/// <summary>
/// Masonry as a linear elastic, isotropic shell (docs/plan-fem.md §11.1): E = KE · fk (EN 1996-1-1 §3.7.2, KE = 1000
/// recommended), fk = K · fb^0.7 · fm^0.3 (EN 1996-1-1 eq. 3.1, general purpose mortar, fm at most 2 fb and 20 MPa) with K by the
/// unit's group (EN 1996-1-1 Table 3.3), G = 0.4 E (§3.7.3; ν = 0.25). The material's own parameters (Lapin's set
/// <c>Lapin_Masonry</c>: UnitGroup, fb, fm, fk, KE, in MPa) come before the indicative defaults of its group, read from its name.
/// CR 6-2013 takes the same relations; its own values are the user's to confirm (§11.9).
/// </summary>
public static class Masonry
{
    public const string Set = "Lapin_Masonry";
    public const double Poisson = 0.25, DefaultKE = 1000;

    /// <summary>A unit group: EN 1996-1-1 Table 3.3's K for general purpose mortar, and the indicative fb, fm (MPa) and density (kg/m³) of masonry of such units.</summary>
    public sealed record Group(string Key, string Label, double K, double Fb, double Fm, double Density);

    public static readonly IReadOnlyList<Group> Groups =
    [
        new("clay1", "clay units, group 1 (solid brick)", 0.55, 10, 5, 1800),
        new("clay2", "clay units, group 2 (hollow blocks)", 0.45, 10, 10, 1000),
        new("aac", "autoclaved aerated concrete units", 0.55, 4, 5, 650),
        new("concrete1", "aggregate concrete units, group 1", 0.55, 10, 10, 2000),
        new("concrete2", "aggregate concrete units, group 2", 0.52, 10, 10, 1400),
    ];

    public static Group? Find(string? key) => Groups.FirstOrDefault(g => string.Equals(g.Key, key, StringComparison.OrdinalIgnoreCase));

    static bool Any(string s, params string[] words) => words.Any(s.Contains);

    /// <summary>The group a material's name and category (lower case) say, or null when it is not masonry.</summary>
    public static Group? GroupOf(string lower)
    {
        if (Any(lower, "bca", "aac", "ytong", "autoclav", "beton celular", "celular")) return Find("aac");
        if (Any(lower, "concrete block", "bloc de beton", "blocuri de beton", "bolțar", "boltar")) return Find("concrete2");
        if (Any(lower, "gvp", "porotherm", "hollow", "goluri", "bloc ceramic", "blocuri ceramice", "ceramic block", "clay block")) return Find("clay2");
        if (Any(lower, "brick", "cărămidă", "caramida", "cărămizi", "caramizi")) return Find("clay1");
        if (Any(lower, "masonry", "zidărie", "zidarie", "block", "bloc")) return Find("clay1");
        return null;
    }

    public sealed record Values(MaterialValue E, MaterialValue Fk, MaterialValue Density, string Label)
    {
        /// <summary>The unit's normalised strength fb (MPa) and the group, for the design checks (fvk ≤ 0.065 fb).</summary>
        public double Fb { get; init; }
        public Group? Of { get; init; }
    }

    /// <summary>
    /// KE when the material gives none: CR 6-2013 4.1.2.2.1(3), E = 1000 fk for clay (and the other) units, 800 fk for
    /// autoclaved aerated concrete units (EN 1996-1-1 §3.7.2 recommends 1000 for all).
    /// </summary>
    public static double KeOf(Group g) => g.Key == "aac" ? 800 : DefaultKE;

    /// <summary>The masonry's values: the material's own parameters first, the group's indicative ones otherwise, each with its source.</summary>
    public static Values Of(MaterialFieldSet fields, Group named, List<string> notes)
    {
        double? Own(string key, double min, double max)
        {
            if (fields.Param($"{Set}.{key}") is not { } p) return null;
            var v = Json.Number(p.Value) ?? (p.Value is JsonValue jv && jv.TryGetValue<string>(out var txt) && double.TryParse(txt.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : null);
            if (v is { } d && double.IsFinite(d) && d >= min && d <= max) return d;
            notes.Add($"{Set}.{key} is not a value from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}: left out");
            return null;
        }
        var ownGroup = fields.Param($"{Set}.UnitGroup") is { } gp ? Find(gp.Value?.ToString()) : null;
        var g = ownGroup ?? named;
        var fb = Own("fb", 0.5, 100);
        var fm = Own("fm", 0.5, 40);
        var fkOwn = Own("fk", 0.2, 50);
        var ke = Own("KE", 100, 3000);
        var (fbv, fmv) = (fb ?? g.Fb, fm ?? g.Fm);
        var fmUsed = Math.Min(fmv, Math.Min(2 * fbv, 20));
        var fk = fkOwn ?? g.K * Math.Pow(fbv, 0.7) * Math.Pow(fmUsed, 0.3);
        string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        var given = fb != null || fm != null || ownGroup != null;
        var fkSource = fkOwn != null
            ? $"project: material „{fields.Name}”, {Set}.fk = {F(fk)} MPa"
            : $"EN 1996-1-1:2005 eq. 3.1, {g.Label}: fk = {F(g.K)} · {F(fbv)}^0.7 · {F(fmUsed)}^0.3 = {F(fk)} MPa{(given ? "" : " (indicative: fb, fm of the group)")}";
        var kev = ke ?? KeOf(g);
        var eSource = $"CR 6-2013 4.1.2.2.1(3) (EN 1996-1-1:2005 §3.7.2): E = KE · fk = {F(kev)} · {F(fk)} MPa = {F(kev * fk / 1000)} GPa{(ke == null ? " (KE of CR 6 for the unit)" : "")}; {fkSource}";
        return new(new(kev * fk * 1e6, eSource), new(fk * 1e6, fkSource),
            new(g.Density, $"indicative: masonry of {g.Label}, ρ = {F(g.Density)} kg/m³"), $"{g.Key}, fk = {F(fk)} MPa") { Fb = fbv, Of = g };
    }
}
