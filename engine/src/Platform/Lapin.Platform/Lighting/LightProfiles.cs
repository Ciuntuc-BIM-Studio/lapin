using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Lapin.Platform.Lighting;

/// <summary>
/// A light's distribution (docs/plan-render.md, S2): how much it sends each way, as a small table of relative intensity
/// over the fixture's own angles, and the scale that keeps its whole output at the fixture's lumens. After BimGo's
/// LightProfiles (MIT-0, Round X-D1), rewritten for Lapin.
/// <list type="bullet">
/// <item>Angles are IES type C: <b>gamma</b> 0° straight out of the fixture (down for a downlight), 180° straight back;
/// <b>C</b> 0° along the fixture's X, 90° a quarter turn anticlockwise seen from behind the light (from above, for a downlight).</item>
/// <item>The table is <see cref="Vertical"/> gamma rows (0–180° in even steps) × <see cref="Horizontal"/> C columns (0–337.5°,
/// wrapping), peak 1, bilinear between samples.</item>
/// <item><see cref="Scale"/> = 4π / ∫ table dΩ: the intensity in units of an even light of the same lumens, so a profile
/// changes where a fixture's light goes, never how much there is.</item>
/// </list>
/// </summary>
public sealed class LightProfile
{
    public const int Vertical = 32, Horizontal = 16, TableSize = Vertical * Horizontal;
    public const double GammaStep = 180.0 / (Vertical - 1), CStep = 360.0 / Horizontal;

    /// <summary><c>builtin:…</c>, <c>gen:…</c>, or <c>ies:&lt;hash&gt;</c> for a file.</summary>
    public required string Key { get; init; }
    public required string Name { get; init; }
    /// <summary>Row = gamma sample, column = C sample, peak 1.</summary>
    public required double[] Table { get; init; }
    public double Scale { get; init; } = 1;
    /// <summary>The luminaire's output a file implies (its candela integrated, lm); 0 for the built-in and generated ones.</summary>
    public double Lumens { get; init; }
    /// <summary>The full angle (°) inside which the C-averaged intensity stays above half its peak; 0 when it has no clear beam down.</summary>
    public double BeamAngle { get; init; }
    /// <summary>The full angle (°) past which the light falls under 2 % of its peak (where a spot drawing it may stop); 360 when it reaches behind.</summary>
    public double FieldAngle { get; init; }

    public bool IsFile => Key.StartsWith(LightProfiles.FilePrefix, StringComparison.Ordinal);

    /// <summary>The relative intensity (peak 1) at an angle, bilinear between the table's samples.</summary>
    public double Sample(double gamma, double c) => SampleTable(Table, gamma, c);

    internal static double SampleTable(double[] table, double gamma, double c)
    {
        var gv = Math.Clamp(gamma, 0, 180) / GammaStep;
        var v0 = Math.Min((int)gv, Vertical - 2);
        var fv = gv - v0;
        var cc = c % 360;
        if (cc < 0) cc += 360;
        var ch = cc / CStep;
        var h0 = (int)ch % Horizontal;
        var h1 = (h0 + 1) % Horizontal;
        var fh = ch - Math.Floor(ch);
        double a = table[v0 * Horizontal + h0], b = table[v0 * Horizontal + h1];
        double d = table[(v0 + 1) * Horizontal + h0], e = table[(v0 + 1) * Horizontal + h1];
        double top = a + (b - a) * fh, bottom = d + (e - d) * fh;
        return top + (bottom - top) * fv;
    }

    /// <summary>A profile from a function of (gamma, C) in degrees, any scale (made peak 1 here).</summary>
    public static LightProfile FromFunction(string key, string name, Func<double, double, double> intensity, double lumens = 0)
    {
        var table = new double[TableSize];
        double peak = 0;
        for (var v = 0; v < Vertical; v++)
            for (var h = 0; h < Horizontal; h++)
            {
                var x = Math.Max(0, intensity(v * GammaStep, h * CStep));
                if (!double.IsFinite(x)) x = 0;
                table[v * Horizontal + h] = x;
                peak = Math.Max(peak, x);
            }
        if (peak <= 0) Array.Fill(table, 1.0);
        else for (var i = 0; i < TableSize; i++) table[i] /= peak;
        var integral = Integrate(table);
        return new LightProfile
        {
            Key = key, Name = name, Table = table, Scale = integral > 1e-6 ? 4 * Math.PI / integral : 1, Lumens = lumens,
            BeamAngle = BeamOf(table), FieldAngle = FieldOf(table),
        };
    }

    /// <summary>A profile from an .ies file's text (IESNA LM-63 1986, 1991, 1995, 2002; type C), or null with why.</summary>
    public static LightProfile? FromIes(string key, string name, string text, out string? error)
    {
        var data = IesData.Parse(text, out error);
        return data == null ? null : FromFunction(key, name, data.Candela, data.TotalLumens());
    }

    /// <summary>The table's integral over the sphere (peak 1), finely sampled through the same bilinear lookup.</summary>
    public static double Integrate(double[] table)
    {
        const int G = 360, C = 64;
        double sum = 0, dG = Math.PI / G, dC = 2 * Math.PI / C;
        for (var i = 0; i < G; i++)
        {
            var gamma = (i + 0.5) * dG;
            var band = Math.Sin(gamma) * dG * dC;
            for (var j = 0; j < C; j++) sum += SampleTable(table, gamma * 180 / Math.PI, (j + 0.5) * 360.0 / C) * band;
        }
        return sum;
    }

    static double[] Rows(double[] table)
    {
        var row = new double[Vertical];
        for (var v = 0; v < Vertical; v++)
        {
            double s = 0;
            for (var h = 0; h < Horizontal; h++) s += table[v * Horizontal + h];
            row[v] = s / Horizontal;
        }
        return row;
    }

    static double BeamOf(double[] table)
    {
        var row = Rows(table);
        var peakRow = 0;
        for (var v = 1; v < Vertical; v++) if (row[v] > row[peakRow]) peakRow = v;
        var peak = row[peakRow];
        if (peak <= 0 || peakRow * GammaStep > 30) return 0;
        for (var v = peakRow; v < Vertical - 1; v++)
            if (row[v + 1] < peak * 0.5)
            {
                var t = (row[v] - peak * 0.5) / Math.Max(row[v] - row[v + 1], 1e-9);
                return 2 * (v + t) * GammaStep;
            }
        return 360;
    }

    static double FieldOf(double[] table)
    {
        var row = Rows(table);
        var peak = row.Max();
        for (var v = Vertical - 1; v >= 0; v--)
            if (row[v] >= peak * 0.02) return v >= Vertical - 1 ? 360 : Math.Min(360, 2 * (v + 1) * GammaStep);
        return 360;
    }
}

/// <summary>
/// An IESNA LM-63 photometric file, parsed (1986, 1991, 1995, 2002). Type C only (types A and B, floodlights and road
/// lights, are refused); the symmetric layouts (one C plane, 0–90, 0–180, 90–270) and full files are expanded by
/// <see cref="Candela"/>. TILT=INCLUDE data is read past (kept in <see cref="TiltAngles"/>); a separate tilt file is read as none.
/// After BimGo's IesData (MIT-0).
/// </summary>
public sealed class IesData
{
    public const int MaxChars = 512 * 1024;
    public int Lamps { get; private set; }
    /// <summary>Lumens per lamp (-1: absolute photometry).</summary>
    public double LumensPerLamp { get; private set; }
    public double Multiplier { get; private set; }
    /// <summary>1 type C, 2 type B, 3 type A.</summary>
    public int PhotometricType { get; private set; }
    public double[] Vertical { get; private set; } = [];
    public double[] Horizontal { get; private set; } = [];
    /// <summary>Candela, [horizontal][vertical], the multiplier applied.</summary>
    public double[][] Values { get; private set; } = [];
    /// <summary>The input watts the file gives (0 when it gives none).</summary>
    public double InputWatts { get; private set; }
    public double[]? TiltAngles { get; private set; }
    public double[]? TiltFactors { get; private set; }
    public bool TiltFileIgnored { get; private set; }
    /// <summary>The keywords ([TEST], [MANUFAC], [LUMINAIRE]…), the first value of each.</summary>
    public Dictionary<string, string> Keywords { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static IesData? Parse(string? text, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(text)) { error = "The file is empty."; return null; }
        if (text.Length > MaxChars) { error = "The file is too big for an .ies file."; return null; }
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var data = new IesData();
        var line = 0;
        string? tilt = null;
        for (; line < lines.Length; line++)
        {
            var l = lines[line].Trim();
            if (l.StartsWith("TILT", StringComparison.OrdinalIgnoreCase))
            {
                var eq = l.IndexOf('=');
                tilt = eq >= 0 ? l[(eq + 1)..].Trim() : "";
                line++;
                break;
            }
            if (l.StartsWith('[') && l.IndexOf(']') is > 1 and var close)
                data.Keywords.TryAdd(l[1..close], l[(close + 1)..].Trim());
        }
        if (tilt == null) { error = "Not an IES photometric file (no TILT line)."; return null; }
        var numbers = new List<double>();
        for (; line < lines.Length; line++)
            foreach (var token in lines[line].Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries))
            {
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    error = $"Unreadable number „{(token.Length > 20 ? token[..20] + "…" : token)}” in the photometric data.";
                    return null;
                }
                numbers.Add(value);
            }
        var at = 0;
        double Next() => at < numbers.Count ? numbers[at++] : double.NaN;
        if (string.Equals(tilt, "INCLUDE", StringComparison.OrdinalIgnoreCase))
        {
            Next(); // the lamp-to-luminaire geometry
            var count = Next();
            if (!(count >= 0 && count < 1000) || numbers.Count - at < 2 * (int)count) { error = "Damaged TILT data."; return null; }
            var n = (int)count;
            var angles = new double[n];
            var factors = new double[n];
            for (var i = 0; i < n; i++) angles[i] = Next();
            for (var i = 0; i < n; i++) { var f = Next(); factors[i] = double.IsFinite(f) && f >= 0 ? Math.Min(f, 10) : 1; }
            var usable = n > 0;
            for (var i = 0; i < n; i++) if (!double.IsFinite(angles[i]) || (i > 0 && !(angles[i] > angles[i - 1]))) usable = false;
            if (usable) { data.TiltAngles = angles; data.TiltFactors = factors; }
        }
        else if (tilt.Length > 0 && !string.Equals(tilt, "NONE", StringComparison.OrdinalIgnoreCase)) data.TiltFileIgnored = true;

        double lamps = Next(), lumens = Next(), multiplier = Next(), nv = Next(), nh = Next(), type = Next();
        Next(); Next(); Next(); Next(); // units, width, length, height
        Next(); Next(); var watts = Next(); // ballast factor, future use, input watts
        if (double.IsNaN(type) || !(nv >= 2 && nv <= 2000) || !(nh >= 1 && nh <= 2000)) { error = "Damaged photometric header (angle counts)."; return null; }
        data.Lamps = (int)Math.Max(1, lamps);
        data.LumensPerLamp = lumens;
        data.Multiplier = multiplier > 0 && double.IsFinite(multiplier) ? multiplier : 1;
        data.InputWatts = double.IsFinite(watts) && watts > 0 ? watts : 0;
        data.PhotometricType = (int)type;
        if (data.PhotometricType != 1)
        {
            error = data.PhotometricType is 2 or 3
                ? $"Type {(data.PhotometricType == 2 ? "B" : "A")} photometry (floodlights, road lights) is not supported: only type C."
                : "Unknown photometric type.";
            return null;
        }
        int v = (int)nv, h = (int)nh;
        if (numbers.Count - at < v + h + v * h) { error = "The file ends before all its candela values."; return null; }
        data.Vertical = new double[v];
        data.Horizontal = new double[h];
        for (var i = 0; i < v; i++) data.Vertical[i] = Next();
        for (var i = 0; i < h; i++) data.Horizontal[i] = Next();
        data.Values = new double[h][];
        for (var j = 0; j < h; j++)
        {
            data.Values[j] = new double[v];
            for (var i = 0; i < v; i++) { var cd = Next() * data.Multiplier; data.Values[j][i] = double.IsFinite(cd) && cd > 0 ? cd : 0; }
        }
        static bool Increasing(double[] a) { for (var i = 1; i < a.Length; i++) if (!(a[i] > a[i - 1])) return false; return true; }
        if (!Increasing(data.Vertical) || !Increasing(data.Horizontal) || data.Vertical[0] < -0.01 || data.Vertical[^1] > 180.01
            || data.Horizontal[0] < -0.01 || data.Horizontal[^1] > 360.01)
        {
            error = "The angles are out of order or out of range.";
            return null;
        }
        return data;
    }

    /// <summary>Candela at an angle, the file's symmetry expanded, bilinear between its angles (0 outside its gamma range).</summary>
    public double Candela(double gamma, double c)
    {
        if (gamma < Vertical[0] - 1e-3 || gamma > Vertical[^1] + 1e-3) return 0;
        c %= 360;
        if (c < 0) c += 360;
        double first = Horizontal[0], last = Horizontal[^1];
        if (Horizontal.Length == 1) return Along(Values[0], gamma);
        if (first < 1e-3 && Math.Abs(last - 90) < 1e-3) { c %= 180; if (c > 90) c = 180 - c; }          // quadrants
        else if (first < 1e-3 && Math.Abs(last - 180) < 1e-3) { if (c > 180) c = 360 - c; }         // bilateral about 0–180
        else if (Math.Abs(first - 90) < 1e-3 && Math.Abs(last - 270) < 1e-3) { if (c < 90) c = 180 - c; else if (c > 270) c = 540 - c; } // about 90–270
        if (c <= first) return Along(Values[0], gamma);
        var j = 0;
        while (j < Horizontal.Length - 1 && Horizontal[j + 1] < c) j++;
        if (j >= Horizontal.Length - 1)
        {
            var span = first + 360 - last;
            if (span < 1e-3) return Along(Values[^1], gamma);
            return Lerp(Along(Values[^1], gamma), Along(Values[0], gamma), Math.Clamp((c - last) / span, 0, 1));
        }
        var t = Math.Clamp((c - Horizontal[j]) / (Horizontal[j + 1] - Horizontal[j]), 0, 1);
        return Lerp(Along(Values[j], gamma), Along(Values[j + 1], gamma), t);
    }

    double Along(double[] plane, double g)
    {
        if (g <= Vertical[0]) return plane[0];
        if (g >= Vertical[^1]) return plane[^1];
        var i = 0;
        while (i < Vertical.Length - 2 && Vertical[i + 1] < g) i++;
        return Lerp(plane[i], plane[i + 1], Math.Clamp((g - Vertical[i]) / (Vertical[i + 1] - Vertical[i]), 0, 1));
    }

    static double Lerp(double a, double b, double t) => a + (b - a) * t;

    /// <summary>The luminaire's output (lm): its candela integrated over the sphere.</summary>
    public double TotalLumens()
    {
        const int G = 360, C = 72;
        double sum = 0, dG = Math.PI / G, dC = 2 * Math.PI / C;
        for (var i = 0; i < G; i++)
        {
            var gamma = (i + 0.5) * dG;
            var band = Math.Sin(gamma) * dG * dC;
            for (var j = 0; j < C; j++) sum += Candela(gamma * 180 / Math.PI, (j + 0.5) * 360.0 / C) * band;
        }
        return sum;
    }
}

/// <summary>The built-in profiles (made here from formulas, no third-party photometry), the generated ones and the keys.</summary>
public static class LightProfiles
{
    public const string FilePrefix = "ies:", BuiltInPrefix = "builtin:", GeneratedPrefix = "gen:";
    public const string DownlightNarrow = BuiltInPrefix + "downlight-narrow", DownlightMedium = BuiltInPrefix + "downlight-medium",
        Flood = BuiltInPrefix + "flood", WallWasher = BuiltInPrefix + "wall-washer", Pendant = BuiltInPrefix + "pendant";
    /// <summary>An even (Lambertian, cosine) half sphere out of the fixture: a panel's.</summary>
    public const string Hemispherical = GeneratedPrefix + "hemi";
    const string SpotPrefix = GeneratedPrefix + "spot:";

    /// <summary>A file's key: <c>ies:</c> and the first 16 hex characters of the SHA-256 of its text (line ends made \n): one file brought twice is one profile.</summary>
    public static string KeyOf(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes((text ?? "").Replace("\r\n", "\n")));
        return FilePrefix + Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    static LightProfile[]? builtIns;

    /// <summary>
    /// The five built-ins, in the order they are offered: a narrow downlight (a 25° beam, cos²⁹, a little spill to 60°), a medium one
    /// (40°, cos¹¹), a wide flood (a soft batwing to about 120°), a wall washer (down and towards C 0°, peaking 35° off the nadir)
    /// and a pendant (most down in a broad cone, some up, an opal diffuser's).
    /// </summary>
    public static IReadOnlyList<LightProfile> BuiltIns => builtIns ??=
    [
        LightProfile.FromFunction(DownlightNarrow, "Downlight, narrow (25°)", (g, c) => Down(g, 29) + 0.04 * Down(g, 2) * Fade(g, 60)),
        LightProfile.FromFunction(DownlightMedium, "Downlight, medium (40°)", (g, c) => Down(g, 11) + 0.05 * Down(g, 1.5) * Fade(g, 70)),
        LightProfile.FromFunction(Flood, "Wide flood (120°)", (g, c) => g < 90 ? (0.75 + 0.45 * Math.Sin(Rad(g)) * Math.Sin(Rad(g))) * Fade(g, 82) : 0.02),
        LightProfile.FromFunction(WallWasher, "Wall washer (C 0° towards the wall)", (g, c) =>
            0.25 * Down(g, 2) + Math.Pow(0.5 + 0.5 * Math.Cos(Rad(c)), 3) * Math.Exp(-Math.Pow((g - 35) / 18, 2)) * Fade(g, 85)),
        LightProfile.FromFunction(Pendant, "Pendant (up and down)", (g, c) =>
        {
            var cos = Math.Cos(Rad(g));
            return cos > 0 ? 0.18 + 0.82 * Math.Pow(cos, 1.6) : 0.18 + 0.45 * Math.Pow(-cos, 1.6);
        }),
    ];

    public static LightProfile? BuiltIn(string? key) => BuiltIns.FirstOrDefault(p => p.Key == key);

    /// <summary>A spot's key, <c>gen:spot:&lt;beam&gt;:&lt;field&gt;</c> (whole degrees, full angles; the beam ≥ 1°, the field ≥ the beam, both ≤ 179°).</summary>
    public static string? SpotKey(double beam, double field)
    {
        if (!double.IsFinite(beam) || !double.IsFinite(field) || field <= 0) return null;
        var f = (int)Math.Clamp(Math.Round(field), 1, 179);
        var b = (int)Math.Clamp(Math.Round(beam), 1, f);
        return string.Create(CultureInfo.InvariantCulture, $"{SpotPrefix}{b}:{f}");
    }

    static bool TryParseSpot(string? key, out int beam, out int field)
    {
        beam = field = 0;
        if (key == null || !key.StartsWith(SpotPrefix, StringComparison.Ordinal)) return false;
        var parts = key[SpotPrefix.Length..].Split(':');
        return parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out beam)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out field) && beam >= 1 && field >= beam && field <= 179;
    }

    /// <summary>
    /// A generated profile from its key, or null: a spot (full inside half its beam, a smooth fall to nothing at half its field,
    /// at least 2° of fall) or the hemisphere (cos γ out of the fixture, nothing behind).
    /// </summary>
    public static LightProfile? Generated(string? key)
    {
        if (key == Hemispherical) return LightProfile.FromFunction(key, "Even half sphere (a panel's)", (g, c) => g < 90 ? Math.Cos(Rad(g)) : 0);
        if (!TryParseSpot(key, out var beam, out var field)) return null;
        double inner = beam * 0.5, outer = Math.Max(field * 0.5, inner + 2);
        return LightProfile.FromFunction(key!, string.Create(CultureInfo.InvariantCulture, $"Spot ({beam}° beam, {field}° field)"), (g, c) =>
        {
            if (g <= inner) return 1;
            if (g >= outer) return 0;
            var t = (outer - g) / (outer - inner);
            return t * t * (3 - 2 * t);
        });
    }

    static double Rad(double d) => d * Math.PI / 180;
    static double Down(double g, double power) => g < 90 ? Math.Pow(Math.Cos(Rad(g)), power) : 0;
    static double Fade(double g, double end) => Math.Clamp((end - g) / 10, 0, 1);
}
