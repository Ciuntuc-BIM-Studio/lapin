using System.Globalization;
using System.Text;

namespace Lapin.Model;

/// <summary>How much of a text <see cref="Numbers.Parse"/> reads as the number.</summary>
public enum NumberMode
{
    /// <summary>The whole text and nothing else: a decimal point or comma, a sign, an exponent; no thousands groups („12,5”, „1e3”, „−3”).</summary>
    Strict,
    /// <summary>The whole text, thousands groups allowed („1 250”, „1'250”, „1.234,5”, „1,234.5”).</summary>
    Grouped,
    /// <summary>The number the text starts with, groups allowed, whatever follows it („12,5 m²”, „40 %”, „1:100” → 1).</summary>
    Leading,
    /// <summary>The first number anywhere in the text, groups allowed („L = 12 m” → 12, „+3.20” → 3.2).</summary>
    First,
}

/// <summary>
/// The one reader of numbers typed or written in text: tables, spreadsheets, CSV cells, formulas' values, filters.
/// A decimal point or a decimal comma; a plus, a minus or a true minus (U+2212); an exponent (<c>1e3</c>). Where groups are allowed,
/// thousands are grouped by a space (also no-break and narrow no-break), an apostrophe, or the other mark of a dot and a comma:
/// when both marks are written the last one is the decimal one („1.234,5”, „1,234.5”), a lone mark is always decimal („1,200” is 1,2),
/// and a mark written twice groups („1.234.567”). Groups after the first hold three digits, the first one to three.
/// Only finite numbers read; NaN, infinities and words never do.
/// </summary>
public static class Numbers
{
    /// <summary>The text as a number by <paramref name="mode"/>, else null.</summary>
    public static double? Parse(string? text, NumberMode mode = NumberMode.Strict)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        switch (mode)
        {
            case NumberMode.Strict:
            case NumberMode.Grouped:
            {
                var v = Scan(t, 0, mode == NumberMode.Grouped, whole: true, out var end);
                return v != null && end == t.Length ? v : null;
            }
            case NumberMode.Leading:
                return Scan(t, 0, true, whole: false, out _);
            default:
                for (var i = 0; i < t.Length; i++)
                {
                    var c = t[i];
                    if (!(char.IsAsciiDigit(c) || (IsSign(c) && i + 1 < t.Length && char.IsAsciiDigit(t[i + 1])))) continue;
                    if (Scan(t, i, true, whole: false, out _) is { } v) return v;
                    // Not a number here: past its digits and marks, so a code („v2.1.3”) gives none from its middle.
                    while (i + 1 < t.Length && (char.IsAsciiDigit(t[i + 1]) || t[i + 1] is '.' or ',')) i++;
                }
                return null;
        }
    }

    /// <summary>The number <paramref name="text"/> starts with (groups allowed, after any spaces) and the text after it; null when it starts with none.</summary>
    public static double? ParseLeading(string text, out string rest)
    {
        var start = 0;
        while (start < text.Length && char.IsWhiteSpace(text[start])) start++;
        var v = Scan(text, start, true, whole: false, out var end);
        rest = v == null ? text : text[end..];
        return v;
    }

    static bool IsSign(char c) => c is '+' or '-' or '−';
    static bool IsSpaceGroup(char c) => c is ' ' or ' ' or ' ' or '\'' or '’';

    /// <summary>
    /// A number at <paramref name="start"/>: its value and where it ends. <paramref name="whole"/>: the text should end with it, so its
    /// groups have to be right (else it is none); without, a number whose groups are wrong reads as far as it is one („12 5 m” → 12).
    /// </summary>
    static double? Scan(string t, int start, bool grouping, bool whole, out int end)
    {
        end = start;
        var i = start;
        var negative = false;
        if (i < t.Length && IsSign(t[i])) { negative = t[i] != '+'; i++; }
        // The digit runs and the marks between them, each mark followed by a digit.
        var runs = new List<(int From, int To)>();
        var marks = new List<char>();
        var j = i;
        while (j < t.Length && char.IsAsciiDigit(t[j])) j++;
        runs.Add((i, j));
        while (j + 1 < t.Length && (t[j] is '.' or ',' || (grouping && IsSpaceGroup(t[j]))) && char.IsAsciiDigit(t[j + 1]))
        {
            if (!grouping && marks.Count == 1) break;
            marks.Add(t[j]);
            var k = j + 1;
            while (k < t.Length && char.IsAsciiDigit(t[k])) k++;
            runs.Add((j + 1, k));
            j = k;
        }
        int Len(int r) => runs[r].To - runs[r].From;
        if (Len(0) == 0 && (marks.Count == 0 || marks[0] is not ('.' or ','))) return null;

        // The decimal mark among the first n marks (-1: none), or -2 when those marks do not make a number.
        int DecimalOf(int n)
        {
            var dec = n > 0 && marks[n - 1] is '.' or ',' && marks.Take(n).Count(m => m == marks[n - 1]) == 1 ? n - 1 : -1;
            if (Len(0) == 0 && dec != 0) return -2;
            char? group = null;
            for (var m = 0; m < n; m++)
            {
                if (m == dec) continue;
                var cls = IsSpaceGroup(marks[m]) ? ' ' : marks[m];
                if ((group != null && group != cls) || Len(m + 1) != 3) return -2;
                group = cls;
            }
            return group != null && Len(0) > 3 ? -2 : dec;
        }
        var used = marks.Count;
        var decimalAt = DecimalOf(used);
        if (decimalAt == -2)
        {
            if (whole) return null;
            // As far as it reads, when a space ends it („12 5 m” → 12); digits run on by dots or commas are a code („1.2.3”).
            do { used--; decimalAt = used >= 0 && IsSpaceGroup(marks[used]) ? DecimalOf(used) : -2; }
            while (used > 0 && decimalAt == -2);
            if (decimalAt == -2) return null;
        }

        var sb = new StringBuilder();
        if (negative) sb.Append('-');
        sb.Append(Len(0) == 0 ? "0" : t[runs[0].From..runs[0].To]);
        for (var m = 0; m < used; m++)
        {
            if (m == decimalAt) sb.Append('.');
            sb.Append(t, runs[m + 1].From, Len(m + 1));
        }
        end = runs[used].To;
        // A mark that ends the text closes a whole number („5.”).
        if (whole && decimalAt < 0 && end == t.Length - 1 && t[end] is '.' or ',') end++;
        // An exponent: e or E, a sign, digits.
        if (end < t.Length && t[end] is 'e' or 'E')
        {
            var k = end + 1;
            var sign = "";
            if (k < t.Length && IsSign(t[k])) { sign = t[k] == '+' ? "" : "-"; k++; }
            var d = k;
            while (k < t.Length && char.IsAsciiDigit(t[k])) k++;
            if (k > d) { sb.Append('e').Append(sign).Append(t, d, k - d); end = k; }
        }
        return double.TryParse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
    }

    /// <summary>
    /// A length in metres as whole millimetres, the one way names and labels write it („1250”): rounded half to even as
    /// most of the copies it replaces did (12.5 mm → 12), never „-0”.
    /// </summary>
    public static string Mm(double metres)
    {
        var mm = Math.Round(metres * 1000);
        return (mm == 0 ? 0 : mm).ToString("0", CultureInfo.InvariantCulture);
    }
}
