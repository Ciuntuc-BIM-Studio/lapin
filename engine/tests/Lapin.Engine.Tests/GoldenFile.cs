using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Engine.Tests;

/// <summary>
/// The goldens' files (Golden/: what the code before a stage gave, docs/plan-fem.md §13, §14, §14.21) compared with what the
/// code gives now, portably (§14.23): the structure, the strings, the flags and the counts exactly; the numbers within the
/// noise of the machine's floating point. The goldens were written on an arm64 Mac; x64 (CI's Linux) sums the factors' dot
/// products in other lanes (AVX2: 4 doubles, NEON and SSE: 2) and has its own Math library, so its results differ: by
/// ~10⁻¹³ of a value on a well-conditioned model, up to ~10⁻⁷ on the guide's hall with its sheeting, up to ~4·10⁻⁴ for a
/// buckling mode's shape (an iteration converged to a tolerance). A number agrees when <c>|a − b| ≤ Rel·max(|a|, |b|) + Abs</c>,
/// or when it was rounded for its output and the two are one unit apart in its last decimal (a value on a rounding's
/// half-way point that the noise moved across). Where a maximum is (<see cref="Where"/>) is not compared: among equal
/// values (a constant axial force along a bar, a symmetric twin) the noise picks any. LAPIN_WRITE_GOLDEN=1 writes the files
/// again.
/// </summary>
public static class GoldenFile
{
    /// <summary>Relative and absolute tolerance of numbers, chosen by their path without the arrays' indices (<c>$.sets[].result.u.p[]</c>).</summary>
    public readonly record struct Tolerance(double Rel, double Abs);

    /// <summary>
    /// The default: a millionth of the number. The outputs are rounded (to 4–9 decimals), and the noise never moved one by
    /// more than a unit of its last decimal (§14.23); the raw results it moves by up to ~1.5·10⁻⁷.
    /// </summary>
    public static readonly Tolerance Default = new(1e-6, 0);

    /// <summary>Not compared.</summary>
    public static readonly Tolerance Any = new(0, double.PositiveInfinity);

    /// <summary>
    /// Where a maximum or a minimum is (a bar's extremes' <c>xMin</c>/<c>xMax</c>, its largest deflection's <c>x</c>, the
    /// maxima's <c>x</c> and <c>at</c>): <see cref="Any"/>; the values themselves and every station are compared.
    /// </summary>
    public static Tolerance? Where(string shape) =>
        shape.EndsWith(".xMin") || shape.EndsWith(".xMax") || shape.EndsWith(".deflection.x")
        || (shape.Contains("axima.") && (shape.EndsWith(".x") || shape.EndsWith(".at[]"))) ? Any : null;

    public static string Dir([CallerFilePath] string here = "") => Path.Combine(Path.GetDirectoryName(here)!, "Golden");

    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    static readonly JsonSerializerOptions Compact = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Compares <paramref name="actual"/> with Golden/<paramref name="name"/>.json (or writes it with LAPIN_WRITE_GOLDEN=1),
    /// the numbers under a key within its <paramref name="tolerance"/> (else <see cref="Default"/>); a large golden is
    /// written on one line (<paramref name="indented"/> false).
    /// </summary>
    public static void Check(string name, JsonObject actual, Func<string, Tolerance?>? tolerance = null, bool indented = true)
    {
        var path = Path.Combine(Dir(), name + ".json");
        var text = indented ? actual.ToJsonString(Indented) : actual.ToJsonString(Compact);
        if (Environment.GetEnvironmentVariable("LAPIN_WRITE_GOLDEN") == "1") { Directory.CreateDirectory(Dir()); File.WriteAllText(path, text); return; }
        Assert.True(File.Exists(path), $"{path} is missing: LAPIN_WRITE_GOLDEN=1 writes it.");
        var expected = JsonNode.Parse(File.ReadAllText(path));
        var differences = new List<string>();
        Compare("$", "$", expected, JsonNode.Parse(text), tolerance ?? Where, differences);
        Assert.True(differences.Count == 0, $"{name}: {differences.Count} differences from the golden:\n" + string.Join("\n", differences.Take(30)));
    }

    /// <summary>Compares two nodes at <paramref name="path"/> (<paramref name="shape"/>: the same without the arrays' indices, which the tolerances are chosen by).</summary>
    static void Compare(string path, string shape, JsonNode? e, JsonNode? a, Func<string, Tolerance?> tolerance, List<string> diffs)
    {
        switch (e, a)
        {
            case (null, null): return;
            case (JsonObject eo, JsonObject ao):
                foreach (var k in eo.Select(x => x.Key).Union(ao.Select(x => x.Key)))
                {
                    if (!eo.ContainsKey(k) || !ao.ContainsKey(k)) { diffs.Add($"{path}.{k}: {(eo.ContainsKey(k) ? "missing" : "not in the golden")}"); continue; }
                    Compare($"{path}.{k}", $"{shape}.{k}", eo[k], ao[k], tolerance, diffs);
                }
                return;
            case (JsonArray ea, JsonArray aa):
                if (ea.Count != aa.Count) { diffs.Add($"{path}: {aa.Count} items, the golden {ea.Count}"); return; }
                for (var i = 0; i < ea.Count; i++) Compare($"{path}[{i}]", shape + "[]", ea[i], aa[i], tolerance, diffs);
                return;
            case (JsonValue ev, JsonValue av) when ev.GetValueKind() == JsonValueKind.Number && av.GetValueKind() == JsonValueKind.Number:
                var (x, y) = (ev.GetValue<double>(), av.GetValue<double>());
                var t = tolerance(shape) ?? Default;
                if (!Agree(x, y, t, ev.ToJsonString(), av.ToJsonString())) diffs.Add($"{path}: {Show(y)}, the golden {Show(x)} (Δ {Show(y - x)}, rel {Show(Math.Abs(y - x) / Math.Max(Math.Abs(x), Math.Abs(y)))})");
                return;
            default:
                if (e?.ToJsonString() != a?.ToJsonString()) diffs.Add($"{path}: {a?.ToJsonString() ?? "null"}, the golden {e?.ToJsonString() ?? "null"}");
                return;
        }
    }

    static string Show(double v) => v.ToString("G6", CultureInfo.InvariantCulture);

    /// <summary>Whether two numbers agree within <paramref name="t"/>, or are a rounded output one unit apart in its last decimal.</summary>
    public static bool Agree(double x, double y, Tolerance t, string? xText = null, string? yText = null)
    {
        if (x == y) return true;
        var d = Math.Abs(x - y);
        if (d <= t.Rel * Math.Max(Math.Abs(x), Math.Abs(y)) + t.Abs) return true;
        if (xText == null || yText == null) return false;
        // A rounded output: the more precise of the two written with few decimals (at most 9), one unit of its last apart.
        var places = Math.Max(Decimals(xText), Decimals(yText));
        if (places is < 1 or > 9) return false;
        var unit = Math.Pow(10, -places);
        return Math.Abs(d - unit) <= unit * 1e-6;
    }

    /// <summary>Decimals written in a JSON number (-1 for an exponent form: not a rounded output).</summary>
    static int Decimals(string s)
    {
        if (s.Contains('e') || s.Contains('E')) return -1;
        var dot = s.IndexOf('.');
        return dot < 0 ? 0 : s.Length - dot - 1;
    }
}
