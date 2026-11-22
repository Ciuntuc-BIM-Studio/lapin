using Lapin.Drawing.Documents;
using Lapin.Drawing.Export;

namespace Lapin.Drawing;

/// <summary>
/// The one measure of a drawing's text (audit 22.11.2026 §4): how wide a text prints on paper, in the font the PDF prints it
/// in, from Adobe's AFM widths (<see cref="HelveticaMetrics"/>, <see cref="TimesMetrics"/>; Greek letters the Symbol font's).
/// A text's height is its cap height (paper mm, or whatever unit the caller's height is in): the PDF sets the font's size to
/// the height over Helvetica's cap height (0.718 em), for both its fonts. "times" prints in Times; every other font ("sans",
/// "serif", "mono") in Helvetica, as the PDF and the SVG print them. Italic is the upright slanted, so it has the upright's
/// widths. Everything that sizes a box, a column or a wrap around a text, and the PDF's centred and end-anchored texts, use it.
/// </summary>
public static class TextMeasure
{
    /// <summary>Helvetica's cap height (em): a text's font size is its height over it.</summary>
    public const double CapHeight = 0.718;

    /// <summary>How far apart a text's lines stand, in its heights (baseline to baseline), wherever a drawing stacks lines: notes, tables, tags, title blocks.</summary>
    public const double LinePitch = 1.4;

    /// <summary>Whether a font prints in Times (else Helvetica).</summary>
    public static bool IsTimes(string? font) => font == "times";

    /// <summary>A text's advance in em of its font (one line; a newline measures nothing).</summary>
    public static double Em(string s, string? font = null, bool bold = false)
    {
        var times = IsTimes(font);
        var w = 0.0;
        foreach (var ch in s)
        {
            if (PdfWriter.GreekWidth(ch) is { } g) { w += g; continue; }
            w += (times ? TimesMetrics.Width(ch, bold) : HelveticaMetrics.Width(ch, bold)) / 1000.0;
        }
        return w;
    }

    /// <summary>A line's width at cap height <paramref name="h"/>, in h's unit.</summary>
    public static double Width(string s, double h, string? font = null, bool bold = false, bool italic = false) => Em(s, font, bold) * h / CapHeight;

    /// <summary>The widest of a text's lines (split on its newlines) at cap height <paramref name="h"/>.</summary>
    public static double Widest(string text, double h, string? font = null, bool bold = false, bool italic = false) =>
        text.Length == 0 ? 0 : text.Replace("\r\n", "\n").Split('\n').Max(l => Width(l, h, font, bold, italic));

    /// <summary>The widest of several lines at cap height <paramref name="h"/> (none: 0).</summary>
    public static double Widest(IEnumerable<string> lines, double h, string? font = null, bool bold = false, bool italic = false) =>
        lines.Select(l => Width(l, h, font, bold, italic)).DefaultIfEmpty(0).Max();

    /// <summary>A text style's line width at its own height (or <paramref name="h"/> when given).</summary>
    public static double Width(string s, TextStyle style, double? h = null) => Width(s, h ?? style.Height, style.Font, style.Bold, style.Italic);
}
