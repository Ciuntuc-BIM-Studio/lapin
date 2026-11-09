using System.Text.Json.Nodes;
using Lapin.Platform.Analysis;

namespace Lapin.Platform.ProfileLibraries;

/// <summary>A value a file gave that differs from Lapin's by more than its tolerance (a fraction): the key, both values, the difference.</summary>
public sealed record ProfileCheck(string Profile, string Key, double Given, double Computed, double Difference);

/// <summary>
/// A profile's properties as the tables give them (EN: y the strong axis, z the weak; SI units): computed from its
/// dimensions by <see cref="SectionProperties"/>, the file's own values kept beside them and the differences checked.
/// <c>A</c> m², <c>Iy</c>, <c>Iz</c> (about the geometric axes), <c>Iu</c>, <c>Iv</c> (the principal ones, when they are
/// not the geometric ones) and <c>It</c> m⁴, <c>alpha</c> degrees (tan α as EN 10056-1: from y to u), <c>Wely</c>,
/// <c>Welz</c>, <c>Wply</c>, <c>Wplz</c> m³, <c>Iw</c> m⁶, <c>iy</c>, <c>iz</c>, <c>cx</c>, <c>cy</c> (the centroid from
/// the box's centre) m, <c>mass</c> kg/m, <c>paint</c> m²/m.
/// </summary>
public static class ProfileProps
{
    public static readonly string[] Keys = ["A", "Iy", "Iz", "Wely", "Welz", "Wply", "Wplz", "iy", "iz", "It", "Iw", "Iu", "Iv", "alpha", "cx", "cy", "mass", "paint"];

    /// <summary>How far a file's value may stray from Lapin's before the check list says so.</summary>
    public static double Tolerance(string key) => key switch
    {
        "A" or "mass" or "paint" => 0.02,
        "Iy" or "Iz" or "Wely" or "Welz" or "Wply" or "Wplz" or "iy" or "iz" or "Iu" or "Iv" => 0.03,
        "It" or "Iw" => 0.10,
        _ => double.NaN,
    };

    public sealed record Result(IReadOnlyDictionary<string, double> Values, IReadOnlyDictionary<string, string> Methods, SectionProps? Section);

    /// <summary>What Lapin works out from the profile's dimensions (nothing when they cannot give a shape).</summary>
    public static Result Compute(SectionProfile p)
    {
        var v = new Dictionary<string, double>();
        var how = new Dictionary<string, string>();
        if (p.Problem() != null || SectionProperties.Of(p.Shape()) is not { } s) return new(v, how, null);
        var g = s.Geometric ?? s;
        v["A"] = s.Area;
        v["Iy"] = g.I33; v["Iz"] = g.I22;
        v["Wely"] = g.S33; v["Welz"] = g.S22;
        v["Wply"] = g.Z33; v["Wplz"] = g.Z22;
        v["iy"] = Math.Sqrt(g.I33 / s.Area); v["iz"] = Math.Sqrt(g.I22 / s.Area);
        if (s.Geometric != null)
        {
            v["Iu"] = s.I33; v["Iv"] = s.I22;
            // SectionProps' angle is from the depth to the principal axis taken as local 2 (the minor one): EN's α, from y to u, is the same turn.
            v["alpha"] = s.PrincipalDeg;
        }
        // Saint-Venant's estimate for an outline is too rough to give as its torsion constant.
        if (p.Kind != "outline") { v["It"] = s.J; how["It"] = TorsionMethod(s.Method); }
        if (s.Iw is { } iw) { v["Iw"] = iw; how["Iw"] = s.IwMethod ?? ""; }
        v["cx"] = s.Centroid.X; v["cy"] = s.Centroid.Y;
        if (p.DensityOrDefault > 0) v["mass"] = s.Area * p.DensityOrDefault;
        if (s.Perimeter > 0) v["paint"] = s.Perimeter;
        how["A"] = s.Method;
        return new(v, how, s);
    }

    static string TorsionMethod(string method) => method.Contains("J: ", StringComparison.Ordinal) ? method[(method.IndexOf("J: ", StringComparison.Ordinal) + 3)..] : method;

    /// <summary>The file's values that differ from Lapin's by more than their tolerance.</summary>
    public static List<ProfileCheck> Checks(SectionProfile p, IReadOnlyDictionary<string, double>? computed = null)
    {
        computed ??= Compute(p).Values;
        var o = new List<ProfileCheck>();
        foreach (var (k, given) in p.Given)
        {
            var tol = Tolerance(k);
            if (double.IsNaN(tol) || !computed.TryGetValue(k, out var c) || Math.Abs(c) < 1e-30) continue;
            // An angle's moduli and inertias are compared by size (a sign is a convention).
            var d = (Math.Abs(given) - Math.Abs(c)) / Math.Abs(c);
            if (Math.Abs(d) > tol) o.Add(new(p.Name, k, given, c, d));
        }
        return o;
    }

    /// <summary>The values a list shows: the file's where it gave them, Lapin's for the others; each saying which.</summary>
    public static JsonObject Table(SectionProfile p)
    {
        var r = Compute(p);
        var values = new JsonObject();
        foreach (var k in Keys)
        {
            var has = r.Values.TryGetValue(k, out var c);
            var given = p.Given.TryGetValue(k, out var gv);
            if (!has && !given) continue;
            var row = new JsonObject();
            if (has) row["computed"] = c;
            if (given) row["given"] = gv;
            if (r.Methods.TryGetValue(k, out var m) && m.Length > 0) row["method"] = m;
            values[k] = row;
        }
        return new JsonObject
        {
            ["values"] = values,
            ["checks"] = new JsonArray(Checks(p, r.Values).Select(x => (JsonNode)CheckJson(x)).ToArray()),
            ["rings"] = SectionProfile.RingsJson(p.Outline()),
            ["density"] = p.DensityOrDefault,
            ["problem"] = p.Problem(),
        };
    }

    public static JsonObject CheckJson(ProfileCheck c) => new()
    {
        ["profile"] = c.Profile, ["key"] = c.Key, ["given"] = c.Given, ["computed"] = c.Computed, ["difference"] = Math.Round(c.Difference, 4),
        ["unit"] = UnitGuess(c.Key, c.Given, c.Computed),
    };

    /// <summary>A ratio near a power of ten says what the file's column was probably in (×10⁴: cm⁴ read as m⁴…); null otherwise.</summary>
    public static string? UnitGuess(string key, double given, double computed)
    {
        if (!(Math.Abs(given) > 0 && Math.Abs(computed) > 0)) return null;
        var e = Math.Log10(Math.Abs(given / computed));
        var k = Math.Round(e);
        if (k == 0 || Math.Abs(e - k) > 0.02) return null;
        return $"×10^{k:0}";
    }
}
