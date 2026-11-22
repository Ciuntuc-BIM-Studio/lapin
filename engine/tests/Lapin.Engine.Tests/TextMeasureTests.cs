using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Documents;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The one measure of a drawing's text (audit 22.11.2026 §3.5, §4): six measures (the AFM tables, the PDF writer's rough
/// table, 0.6 and 0.62 of the height a character) gave way to <see cref="TextMeasure"/>, the AFM widths of the font the PDF
/// prints, so boxes, columns, wraps and the PDF's centred texts agree.
/// </summary>
public class TextMeasureTests
{
    [Fact]
    public void The_measure_is_the_afm_widths_of_the_font_the_pdf_prints()
    {
        // Ten digits of Helvetica are 0.556 em each: 2.5 mm high (2.5 / 0.718 em), 19.359 mm, where 0.6 a character said 15.
        Assert.Equal(10 * 0.556 * 2.5 / 0.718, TextMeasure.Width("1234567890", 2.5), 9);
        Assert.Equal(HelveticaMetrics.Advance("Wall W1", true) * 3.5 / 0.718, TextMeasure.Width("Wall W1", 3.5, bold: true), 9);
        // "times" prints in Times; any other font (sans, serif, mono) in Helvetica, as the PDF and SVG print them.
        Assert.Equal(TimesMetrics.Advance("Plan", false) * 2 / 0.718, TextMeasure.Width("Plan", 2, "times"), 9);
        Assert.Equal(TextMeasure.Width("Plan", 2), TextMeasure.Width("Plan", 2, "mono"), 9);
        // Italic is the upright slanted; a Greek letter the Symbol font's (λ 0.549 em), which the AFM tables alone measured as nothing.
        Assert.Equal(TextMeasure.Width("Plan", 2), TextMeasure.Width("Plan", 2, italic: true), 9);
        Assert.Equal(0.549 * 2.5 / 0.718, TextMeasure.Width("λ", 2.5), 9);
        // A text of several lines: its widest.
        Assert.Equal(TextMeasure.Width("Second row", 2.5), TextMeasure.Widest("A\nSecond row\nB", 2.5), 9);
    }

    [Fact]
    public void The_pdf_centres_and_ends_its_texts_by_the_same_measure()
    {
        double X(string anchor)
        {
            var pen = new Pen("TEXT", "#000000", 0, null, null);
            pen.Texts.Add(new PenText(new Vec2(50, 50), "MWMW 1234", 2.5, Anchor: anchor));
            var pdf = PdfWriter.Write([new PaperPage(100, 100, [pen])]);
            var text = System.Text.Encoding.Latin1.GetString(pdf);
            var at = text.IndexOf("stream\n", StringComparison.Ordinal) + 7;
            var length = int.Parse(System.Text.RegularExpressions.Regex.Match(text, @"/Length (\d+) /Filter /FlateDecode").Groups[1].Value);
            using var z = new System.IO.Compression.ZLibStream(new MemoryStream(pdf, at, length), System.IO.Compression.CompressionMode.Decompress);
            var content = new StreamReader(z, System.Text.Encoding.Latin1).ReadToEnd();
            var m = System.Text.RegularExpressions.Regex.Match(content, @"([-\d.]+) ([-\d.]+) Tm");
            return double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) * 25.4 / 72;
        }
        var w = TextMeasure.Width("MWMW 1234", 2.5);
        Assert.Equal(w / 2, X("start") - X("middle"), 2);
        Assert.Equal(w, X("start") - X("end"), 2);
    }

    [Fact]
    public void Wrapping_follows_the_printed_widths()
    {
        // "iiii iiii" is narrow and "MMMM MMMM" wide: at the same width the narrow one stays on a line, the wide one wraps.
        double W(string s) => TextMeasure.Width(s, 2.5);
        var width = W("iiii iiii") + 0.1;
        Assert.Single(TextWrap.Lines("iiii iiii", W, width));
        Assert.Equal(["MMMM", "MMMM"], TextWrap.Lines("MMMM MMMM", W, width));
        // The fixed-width overload stays as it was.
        Assert.Equal(["One two", "three four"], TextWrap.Lines("One two three four", 0.15, 1.5));
    }

    [Fact]
    public void A_label_s_box_holds_its_text()
    {
        Assert.Equal(TextMeasure.Width("1234", 2.5, null, true), LabelParts.Width("1234", 2.5, bold: true), 9);
        var (_, hw, _, _) = LabelParts.BoxOf(new PenText(default, "1234567890", 2.5, Bold: true), 1);
        Assert.True(2 * hw >= TextMeasure.Width("1234567890", 2.5, bold: true) - 1e-9);
    }
}
