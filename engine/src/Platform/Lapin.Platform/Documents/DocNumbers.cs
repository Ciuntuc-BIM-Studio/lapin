using System.Globalization;

namespace Lapin.Platform.Documents;

/// <summary>
/// Numbers, areas, lengths, levels and dates as a document writes them, by its language: Romanian with a decimal comma and a
/// thousands dot (1.234,56 m²), English with a point and a comma (1,234.56 m²); an imperial project's areas in ft² and lengths in ft.
/// </summary>
public static class DocNumbers
{
    static readonly NumberFormatInfo Ro = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = ".", NegativeSign = "-" };
    static readonly NumberFormatInfo En = new() { NumberDecimalSeparator = ".", NumberGroupSeparator = ",", NegativeSign = "-" };

    public static NumberFormatInfo FormatOf(DocFormat f) => f.Lang.StartsWith("ro", StringComparison.OrdinalIgnoreCase) ? Ro : En;

    static bool Imperial(DocFormat f) => f.Units is "ft-in" or "in";

    /// <summary>A number with its decimals, grouped by thousands.</summary>
    public static string Number(double v, DocFormat f, int decimals = 2) => Math.Round(v, decimals).ToString("N" + decimals, FormatOf(f));

    /// <summary>An area (m²): to the hundredth, in m² (ft² in an imperial project); to the project's step when it rounds its areas (decision 471).</summary>
    public static string Area(double m2, DocFormat f)
    {
        var shown = Lapin.Drawing.AreaRounding.Shown(m2, f.AreaStep, f.Units);
        return Imperial(f)
            ? Number(shown * 10.763910417, f, Lapin.Drawing.AreaRounding.Decimals(f.AreaStep, 1)) + " ft²"
            : Number(shown, f, Lapin.Drawing.AreaRounding.Decimals(f.AreaStep, 2)) + " m²";
    }

    /// <summary>A length (m): to the hundredth of a metre (ft in an imperial project).</summary>
    public static string Length(double m, DocFormat f) => Imperial(f) ? Number(m / 0.3048, f) + " ft" : Number(m, f) + " m";

    /// <summary>A thickness (m) as a build-up names it: whole centimetres (half ones kept), millimetres under a centimetre.</summary>
    public static string Thickness(double m, DocFormat f)
    {
        if (Imperial(f)) return Number(m / 0.0254, f, 1).TrimEnd('0').TrimEnd(',', '.') + " in";
        var cm = m * 100;
        if (cm < 1 - 1e-9) return Math.Round(m * 1000).ToString(CultureInfo.InvariantCulture) + " mm";
        return (Math.Abs(cm - Math.Round(cm)) < 1e-6 ? Number(cm, f, 0) : Number(cm, f, 1)) + " cm";
    }

    /// <summary>A level from ±0.00, signed: ±0,00, +3,00, -1,50 (m).</summary>
    public static string Level(double z, DocFormat f) => Math.Abs(z) < 0.005 ? "±" + Number(0, f) : (z > 0 ? "+" : "") + Number(z, f);

    /// <summary>A date: 05.11.2026 in Romanian, 2026-11-05 otherwise.</summary>
    public static string Date(DateTime d, DocFormat f) =>
        f.Lang.StartsWith("ro", StringComparison.OrdinalIgnoreCase) ? d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Yes or no in the document's language.</summary>
    public static string YesNo(bool b, DocFormat f) => f.Lang.StartsWith("ro", StringComparison.OrdinalIgnoreCase) ? (b ? "da" : "nu") : (b ? "yes" : "no");

    /// <summary>
    /// A cell's text read as a number with an optional unit after it ("12.50", "1,234.5 m²", "3 pcs", "12,50"), its decimals
    /// counted; null when it is not one. Both separators: the last is the decimal one; one of them more than once: thousands;
    /// once: a point is a decimal point (schedules write invariant numbers), a comma too unless three digits follow it (1,234).
    /// </summary>
    public static (double Value, string Unit, int Decimals)? Parse(string text)
    {
        var t = text.Trim();
        if (t.Length == 0) return null;
        var i = 0;
        var negative = false;
        if (t[0] is '-' or '+' or '−') { negative = t[0] != '+'; i = 1; }
        var start = i;
        while (i < t.Length && (char.IsAsciiDigit(t[i]) || t[i] is '.' or ',' or '\'' || (t[i] == ' ' && i + 1 < t.Length && char.IsAsciiDigit(t[i + 1])))) i++;
        var num = t[start..i].Replace(" ", "").Replace("'", "");
        var unit = t[i..].Trim();
        if (num.Length == 0 || !char.IsAsciiDigit(num[0]) || !char.IsAsciiDigit(num[^1])) return null;
        // A unit is a few letters (m, m², pcs, kg, %); a digit after the number makes it text (a mark "2x3", a date).
        if (unit.Length > 8 || unit.Any(char.IsAsciiDigit) || unit.Any(char.IsWhiteSpace)) return null;
        int dots = num.Count(c => c == '.'), commas = num.Count(c => c == ',');
        char? dec = null;
        if (dots > 0 && commas > 0) dec = num.LastIndexOf('.') > num.LastIndexOf(',') ? '.' : ',';
        else if (dots == 1) dec = '.';
        else if (commas == 1 && num.Length - num.IndexOf(',') - 1 != 3) dec = ',';
        var whole = dec is { } d ? num[..num.LastIndexOf(d)] : num;
        var frac = dec is { } d2 ? num[(num.LastIndexOf(d2) + 1)..] : "";
        // Thousands are groups of three after a first of one to three (30.10.2026 is a date, not a number).
        var groups = whole.Split('.', ',');
        if (groups.Length > 1 && (groups[0].Length is 0 or > 3 || groups.Skip(1).Any(g => g.Length != 3))) return null;
        whole = string.Concat(groups);
        if (whole.Length == 0 || !whole.All(char.IsAsciiDigit) || !frac.All(char.IsAsciiDigit)) return null;
        if (!double.TryParse(whole + (frac.Length > 0 ? "." + frac : ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return null;
        return (negative ? -v : v, unit, frac.Length);
    }

    /// <summary>A number cell written again in the document's language, its decimals and unit kept.</summary>
    public static string Reformat(string text, DocFormat f) =>
        Parse(text) is { } p ? Number(p.Value, f, p.Decimals) + (p.Unit.Length > 0 ? " " + p.Unit : "") : text;

    /// <summary>Whether a filled value is nothing: empty, or a zero however written (0, 0,00, 0.00 m²).</summary>
    public static bool IsEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) || (Parse(value) is { } p && Math.Abs(p.Value) < 1e-12);
}
