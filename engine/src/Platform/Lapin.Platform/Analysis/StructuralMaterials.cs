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
        else if (lower.Contains("concrete") || lower.Contains("beton")) kind = "concrete";
        else if (lower.Contains("steel") || lower.Contains("oțel") || lower.Contains("otel")) kind = "steel";
        else if (lower.Contains("timber") || lower.Contains("wood") || lower.Contains("lemn") || lower.Contains("glulam") || lower.Contains("clt")) kind = "timber";
        else if (lower.Contains("brick") || lower.Contains("masonry") || lower.Contains("cărămidă") || lower.Contains("zidărie") || lower.Contains("block")) kind = "masonry";

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
