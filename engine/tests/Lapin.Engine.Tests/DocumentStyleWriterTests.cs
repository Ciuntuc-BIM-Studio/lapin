using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Lapin.Drawing.Documents;

namespace Lapin.Engine.Tests;

/// <summary>
/// A document's style in the writers (docs/plan-documents.md, stage 4): the font, size and headings' colour in DOCX's and ODT's
/// styles, the logo in each format's header (a DOCX header part with its picture, an ODT frame, a PDF image), Times in the PDF
/// when asked, and no style written exactly as before.
/// </summary>
public class DocumentStyleWriterTests
{
    static readonly XNamespace Wn = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace StyleNs = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    static readonly XNamespace FoNs = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    static readonly XNamespace DrawNs = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    /// <summary>A logo 300 × 100 px: a blue band with an orange square, so it is seen when looked at.</summary>
    internal static byte[] Logo()
    {
        const int w = 300, h = 100;
        var rgb = new byte[w * h * 3];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var k = (y * w + x) * 3;
                var orange = x is > 20 and < 100 && y is > 20 and < 80;
                (rgb[k], rgb[k + 1], rgb[k + 2]) = orange ? ((byte)0xE8, (byte)0x7A, (byte)0x1E) : ((byte)0x1F, (byte)0x4E, (byte)0x8C);
            }
        return DocPng.Encode(w, h, rgb);
    }

    internal static DocStyle Style(string? pdfFont = "times") => new("Georgia", 12, "#1f4e8c", pdfFont, new DocLogo(Logo(), 36, "left"));

    internal static FilledDocument Styled(DocStyle? style = null) => DocumentWriterTests.Sample() is var d ? d with { Page = d.Page with { Style = style ?? Style() } } : null!;

    static Dictionary<string, byte[]> Unzip(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(e => e.FullName, e => { using var s = e.Open(); using var m = new MemoryStream(); s.CopyTo(m); return m.ToArray(); });
    }

    static XDocument Parse(byte[] xml) => XDocument.Parse(Encoding.UTF8.GetString(xml));

    [Fact]
    public void Docx_takes_the_font_the_size_and_the_headings_colour_and_puts_the_logo_in_its_header()
    {
        var parts = Unzip(DocxWriter.Write(Styled()));
        var styles = Parse(parts["word/styles.xml"]);
        var fonts = styles.Descendants(Wn + "rPrDefault").Descendants(Wn + "rFonts").Single();
        Assert.Equal("Georgia", (string?)fonts.Attribute(Wn + "ascii"));
        Assert.Equal("24", (string?)styles.Descendants(Wn + "rPrDefault").Descendants(Wn + "sz").Single().Attribute(Wn + "val"));   // 12 pt in half points
        var h1 = styles.Descendants(Wn + "style").Single(s => (string?)s.Attribute(Wn + "styleId") == "Heading1");
        Assert.Equal("1F4E8C", (string?)h1.Descendants(Wn + "color").Single().Attribute(Wn + "val"));
        // The sizes follow the body's: 16 pt × 12 / 10.5.
        Assert.Equal(((int)Math.Round(16 * 12 / 10.5 * 2)).ToString(), (string?)h1.Descendants(Wn + "sz").Single().Attribute(Wn + "val"));

        // The header holds the logo by its own relationship, the picture in the package.
        Assert.True(parts.ContainsKey("word/media/logo.png"));
        var rels = Parse(parts["word/_rels/header1.xml.rels"]);
        Assert.Contains(rels.Root!.Elements(), r => (string?)r.Attribute("Id") == "rIdLogo" && (string?)r.Attribute("Target") == "media/logo.png");
        var header = Parse(parts["word/header1.xml"]);
        var blip = header.Descendants(XName.Get("blip", "http://schemas.openxmlformats.org/drawingml/2006/main")).Single();
        Assert.Equal("rIdLogo", (string?)blip.Attribute(XName.Get("embed", "http://schemas.openxmlformats.org/officeDocument/2006/relationships")));
        Assert.Contains("Memoriu tehnic", string.Concat(header.Descendants(Wn + "t").Select(t => t.Value)));
    }

    [Fact]
    public void Odt_takes_the_font_and_colour_and_frames_the_logo_in_the_header()
    {
        var parts = Unzip(OdtWriter.Write(Styled()));
        var styles = Parse(parts["styles.xml"]);
        Assert.Contains(styles.Descendants(StyleNs + "font-face"), f => (string?)f.Attribute(StyleNs + "name") == "Georgia");
        var def = styles.Descendants(StyleNs + "default-style").First(d => (string?)d.Attribute(StyleNs + "family") == "paragraph").Element(StyleNs + "text-properties")!;
        Assert.Equal("Georgia", (string?)def.Attribute(StyleNs + "font-name"));
        Assert.Equal("12pt", (string?)def.Attribute(FoNs + "font-size"));
        var h2 = styles.Descendants(StyleNs + "style").Single(s => (string?)s.Attribute(StyleNs + "name") == "Heading_20_2");
        Assert.Equal("#1f4e8c", (string?)h2.Element(StyleNs + "text-properties")!.Attribute(FoNs + "color"));
        var frame = styles.Descendants(StyleNs + "header").Descendants(DrawNs + "frame").Single();
        Assert.Equal("Pictures/logo.png", (string?)frame.Element(DrawNs + "image")!.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink")));
        Assert.Equal("30mm", (string?)frame.Attribute(XName.Get("width", "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0")));
        Assert.True(parts.ContainsKey("Pictures/logo.png"));
        Assert.Contains("Pictures/logo.png", Encoding.UTF8.GetString(parts["META-INF/manifest.xml"]));
    }

    [Fact]
    public void Pdf_writes_in_times_when_asked_with_the_logo_as_an_image()
    {
        var pdf = Encoding.Latin1.GetString(DocumentPdf.Write(Styled()));
        Assert.Contains("/BaseFont /Times-Roman", pdf);
        Assert.Contains("/BaseFont /Times-Bold", pdf);
        Assert.Contains("/Subtype /Image", pdf);
        Assert.Contains("/Im0 Do", Decoded(pdf));
        // Each page's /F3 and /F4 are the Times objects themselves.
        foreach (var (font, name) in new[] { ("F3", "Times-Roman"), ("F4", "Times-Bold") })
        {
            var m = System.Text.RegularExpressions.Regex.Match(pdf, $"/{font} (\\d+) 0 R");
            Assert.True(m.Success, font);
            Assert.Contains($"\n{m.Groups[1].Value} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /{name} ", pdf);
        }
        // Helvetica stays the sheets' font, and a document's without a style.
        var plain = Encoding.Latin1.GetString(DocumentPdf.Write(DocumentWriterTests.Sample()));
        Assert.DoesNotContain("Times-Roman", plain);
        Assert.DoesNotContain("/Subtype /Image", plain);
        // Helvetica asked for, with a serif font in DOCX.
        Assert.DoesNotContain("Times-Roman", Encoding.Latin1.GetString(DocumentPdf.Write(Styled(Style("helvetica")))));
    }

    [Fact]
    public void Times_measures_narrower_than_helvetica_and_wraps_by_its_own_widths()
    {
        Assert.True(TimesMetrics.Mm("Memoriu tehnic de arhitectură", false, 10) < HelveticaMetrics.Mm("Memoriu tehnic de arhitectură", false, 10));
        Assert.Equal(250, TimesMetrics.Width(' ', false));
        Assert.Equal(TimesMetrics.Width('s', true), TimesMetrics.Width('ș', true));
        Assert.Equal(944, TimesMetrics.Width('W', false));
        // The headings on their colour in the PDF's pens.
        var pages = DocumentPdf.Pages(Styled());
        Assert.Contains(pages.SelectMany(p => p.Pens), p => p.Stroke == "#1f4e8c" && p.Texts.Any(t => t.Text.Contains("Memoriu") && t.Font == "times"));
    }

    [Fact]
    public void No_style_writes_as_before()
    {
        var doc = DocumentWriterTests.Sample();
        var empty = doc with { Page = doc.Page with { Style = new DocStyle() } };
        Assert.Equal(Unzip(DocxWriter.Write(doc))["word/styles.xml"], Unzip(DocxWriter.Write(empty))["word/styles.xml"]);
        Assert.Equal(Unzip(DocxWriter.Write(doc))["word/header1.xml"], Unzip(DocxWriter.Write(empty))["word/header1.xml"]);
        Assert.Equal(Unzip(OdtWriter.Write(doc))["styles.xml"], Unzip(OdtWriter.Write(empty))["styles.xml"]);
        Assert.Equal(Unzip(OdtWriter.Write(doc))["content.xml"], Unzip(OdtWriter.Write(empty))["content.xml"]);
        Assert.Equal(DocumentPdf.Write(doc), DocumentPdf.Write(empty));
    }

    /// <summary>The PDF's page streams inflated, to read their operators.</summary>
    static string Decoded(string pdf)
    {
        var b = new StringBuilder();
        var at = 0;
        while ((at = pdf.IndexOf(">>\nstream\n", at, StringComparison.Ordinal)) >= 0)
        {
            var end = pdf.IndexOf("\nendstream", at, StringComparison.Ordinal);
            var data = Encoding.Latin1.GetBytes(pdf[(at + 10)..end]);
            try { using var z = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress); using var m = new MemoryStream(); z.CopyTo(m); b.Append(Encoding.Latin1.GetString(m.ToArray())); }
            catch (InvalidDataException) { }
            at = end;
        }
        return b.ToString();
    }
}
