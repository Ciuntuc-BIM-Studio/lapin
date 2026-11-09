using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Lapin.Drawing.Documents;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Platform.Cde.PdfRemarks;

namespace Lapin.Engine.Tests;

/// <summary>
/// The documents' writers (docs/plan-documents.md, Exportul): one filled document written as DOCX, ODT and PDF and read
/// back: the packages and their XML, the text with the Romanian letters, the tables' rows, the pictures, the PDF's pages
/// with the header repeated, the page numbers and the table of contents' real pages. With LAPIN_DOCS_SAMPLES set to a
/// folder, the samples are written there (scripts/documents-check.mjs opens them with LibreOffice).
/// </summary>
public class DocumentWriterTests
{
    static readonly XNamespace Wn = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    static readonly XNamespace TableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    static readonly XNamespace DrawNs = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    const string Long = "Clădirea propusă are regimul de înălțime P+1E, cu structura din zidărie portantă confinată cu sâmburi și centuri de beton armat, "
        + "planșee din beton armat monolit și acoperiș tip șarpantă din lemn, cu învelitoare din țiglă ceramică. Închiderile exterioare sunt din blocuri ceramice "
        + "cu goluri verticale de 30 cm, termoizolate la exterior cu vată minerală bazaltică de 10 cm, finisate cu tencuială decorativă siliconică.";

    static DocRun R(string s, bool bold = false, bool italic = false) => new(s, bold, italic);

    internal static FilledDocument Sample()
    {
        var page = DocPage.A4 with
        {
            Header = [R("Memoriu tehnic - Casă unifamilială, Huși")],
            Footer = [R("Pagina "), new DocRun("", Field: "page"), R(" din "), new DocRun("", Field: "pages")],
        };
        var rows = new List<IReadOnlyList<IReadOnlyList<DocRun>>>();
        double total = 0;
        for (var i = 1; i <= 60; i++)
        {
            var area = 4.5 + i * 0.37;
            total += area;
            rows.Add([[R($"{(i <= 30 ? "P" : "E")}.{i:D2}")], [R(i % 3 == 0 ? "Dormitor și baie" : i % 3 == 1 ? "Bucătărie" : "Hol de trecere spre terasă")], [R(area.ToString("0.00"))], [R((area * 2.7).ToString("0.00"))]]);
        }
        rows.Add([[R("Total")], [R("")], [R(total.ToString("0.00"))], [R((total * 2.7).ToString("0.00"))]]);

        var pens = new List<Pen>();
        var wall = new Pen("CUT", "#000000", 0.5, null, null);
        wall.Segments.AddRange([(new Vec2(10, 10), new Vec2(150, 10)), (new Vec2(150, 10), new Vec2(150, 90)), (new Vec2(150, 90), new Vec2(10, 90)), (new Vec2(10, 90), new Vec2(10, 10))]);
        var fill = new Pen("FILL", null, 0, "#c8d8e8", null);
        fill.Loops.Add([new Vec2(20, 20), new Vec2(80, 20), new Vec2(80, 60), new Vec2(20, 60)]);
        var hidden = new Pen("HIDDEN", "#444444", 0.25, null, [2, 1]);
        hidden.Segments.Add((new Vec2(10, 50), new Vec2(150, 50)));
        var label = new Pen("TEXT", "#000000", 0.18, null, null);
        label.Texts.Add(new PenText(new Vec2(50, 42), "Cameră de zi 24,50 m²", 2.5));
        pens.AddRange([fill, wall, hidden, label]);

        var blocks = new List<DocBlock>
        {
            new DocHeading(1, [R("Memoriu tehnic de arhitectură")]),
            new DocParagraph([R("Beneficiar: "), R("Ionescu Ștefan", bold: true), R(", cu sediul în "), new DocRun("project.clientAddress", Missing: true), R(".")]),
            new DocToc(),
            new DocPageBreak(),
            new DocHeading(1, [R("1. Date generale")]),
            new DocTable([], [
                [[R("Denumirea proiectului")], [R("Casă unifamilială P+1E")]],
                [[R("Amplasament")], [R("Huși, str. Ștefan cel Mare nr. 12, jud. Vaslui")]],
                [[R("Faza")], [R("DTAC")]],
                [[R("Proiectant")], [new DocRun("project.designer", Missing: true)]],
            ], new HashSet<int>(), Plain: true),
            new DocHeading(2, [R("1.1. Descrierea construcției")]),
            new DocParagraph([R(Long), R(" "), R("Text cursiv la final.", italic: true)]),
            new DocList(false, [[R("Aria construită: 142,30 m²")], [R("Aria desfășurată: 268,75 m²")], [R("Înălțimea maximă: "), R("8,45 m", bold: true)]]),
            new DocList(true, [[R("Fundații continue din beton armat")], [R("Pereți din zidărie confinată")], [R("Șarpantă din lemn de rășinoase")]]),
            new DocHeading(1, [R("2. Spațiile pe niveluri")]),
            new DocTable(["Nr.", "Denumire", "Suprafață (m²)", "Volum (m³)"], rows, new HashSet<int> { 2, 3 }, null, TotalRow: true, Title: "Tabelul 1. Încăperile"),
            new DocParagraph([R("Suprafețele sunt măsurate la fața finită a pereților.")], "note"),
            new DocHeading(2, [R("2.1. Planul parterului")]),
            new DocImage("plan", 160, 100, pens, "Figura 1. Planul parterului, scara 1:100"),
            new DocImage("svgOnly", 160, 100, pens, "Figura 2. Doar SVG"),
            new DocImage("absent", 160, 100, pens, "Figura 3. Fără fișiere"),
            new DocHeading(3, [R("Observații")]),
            new DocParagraph([R("Legendă sub tabel")], "caption"),
            new DocParagraph([R("Țiglă, șarpantă, învelitoare, ușă, fereastră: ă â î ș ț Ă Â Î Ș Ț.")]),
        };
        return new FilledDocument("Memoriu tehnic", page, blocks, new DocMeta("Ionuț Ciuntuc", "Casă unifamilială", "B", "S2"));
    }

    static byte[] Png()
    {
        const int w = 40, h = 25;
        var rgb = new byte[w * h * 3];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++) { var k = (y * w + x) * 3; rgb[k] = (byte)(x * 6); rgb[k + 1] = (byte)(y * 10); rgb[k + 2] = 200; }
        return DocPng.Encode(w, h, rgb);
    }

    const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"160mm\" height=\"100mm\" viewBox=\"0 0 160 100\"><rect x=\"20\" y=\"20\" width=\"60\" height=\"40\" fill=\"#c8d8e8\"/>"
        + "<rect x=\"10\" y=\"10\" width=\"140\" height=\"80\" fill=\"none\" stroke=\"#000\" stroke-width=\"0.5\"/><text x=\"50\" y=\"42\" font-size=\"3.5\" font-family=\"Arial\">Cameră de zi 24,50 m²</text></svg>";

    static Dictionary<string, DocPicture> Pictures() => new() { ["plan"] = new DocPicture(Svg, Png()), ["svgOnly"] = new DocPicture(Svg, null) };

    static Dictionary<string, byte[]> Unzip(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(e => e.FullName, e => { using var s = e.Open(); using var m = new MemoryStream(); s.CopyTo(m); return m.ToArray(); });
    }

    static XDocument Parse(byte[] xml) => XDocument.Parse(Encoding.UTF8.GetString(xml));

    [Fact]
    public void A_png_written_by_hand_reads_back()
    {
        var r = Raster.Read(Png());
        Assert.NotNull(r);
        Assert.Equal((40, 25), (r!.W, r.H));
        Assert.NotNull(Raster.Read(DocPng.White()));
    }

    [Fact]
    public void A_png_keeps_its_see_through_pixels()
    {
        byte[] pixels = [255, 0, 0, 255, 0, 0, 0, 0];
        var picture = Raster.Read(PdfImport.Png(pixels, 2, 1, 4))!;
        Assert.Equal(2, picture.W);
        Assert.Equal(new byte[] { 255, 0 }, picture.Alpha);
    }

    [Fact]
    public void Docx_is_a_package_of_valid_parts_with_the_text_tables_and_pictures()
    {
        var parts = Unzip(DocxWriter.Write(Sample(), Pictures()));
        foreach (var name in new[] { "[Content_Types].xml", "_rels/.rels", "word/document.xml", "word/styles.xml", "word/numbering.xml", "word/settings.xml", "word/header1.xml", "word/footer1.xml", "word/_rels/document.xml.rels", "docProps/core.xml", "docProps/app.xml" })
            Assert.True(parts.ContainsKey(name), name);
        foreach (var (name, data) in parts.Where(p => p.Key.EndsWith(".xml") || p.Key.EndsWith(".rels") || p.Key.EndsWith(".svg"))) Parse(data);

        var docXml = Parse(parts["word/document.xml"]);
        var text = string.Concat(docXml.Descendants(Wn + "t").Select(t => t.Value));
        Assert.Contains("Memoriu tehnic de arhitectură", text);
        Assert.Contains("Ionescu Ștefan", text);
        Assert.Contains("ă â î ș ț Ă Â Î Ș Ț", text);
        Assert.DoesNotContain("project.clientAddress", text);
        Assert.DoesNotContain("project.designer", text);

        var tables = docXml.Descendants(Wn + "tbl").ToList();
        Assert.Equal(2, tables.Count);
        var big = tables.Single(t => t.Descendants(Wn + "tblHeader").Any());
        Assert.Equal(62, big.Elements(Wn + "tr").Count());   // the header, 60 rows, the total
        Assert.Single(big.Elements(Wn + "tr").First().Descendants(Wn + "tblHeader"));
        Assert.Empty(tables.Single(t => t != big).Descendants(Wn + "tblHeader"));
        Assert.Contains(big.Elements(Wn + "tr").Skip(1).First().Descendants(Wn + "jc"), j => (string?)j.Attribute(Wn + "val") == "right");

        // Two pictures: PNG and SVG; SVG alone with a 1×1 PNG beside it; the third left out, its caption kept.
        Assert.Equal(2, parts.Keys.Count(k => k.StartsWith("word/media/") && k.EndsWith(".png")));
        Assert.Equal(2, parts.Keys.Count(k => k.StartsWith("word/media/") && k.EndsWith(".svg")));
        Assert.Equal(2, docXml.Descendants().Count(e => e.Name.LocalName == "svgBlip"));
        Assert.Contains("Figura 3. Fără fișiere", text);

        Assert.Contains("TOC \\o \"1-2\"", string.Concat(docXml.Descendants(Wn + "instrText").Select(t => t.Value)));
        Assert.Contains("NUMPAGES", Encoding.UTF8.GetString(parts["word/footer1.xml"]));
        Assert.Contains("updateFields", Encoding.UTF8.GetString(parts["word/settings.xml"]));
        var core = Encoding.UTF8.GetString(parts["docProps/core.xml"]);
        Assert.Contains("Ionuț Ciuntuc", core);
        Assert.Contains("<dc:title>Memoriu tehnic</dc:title>", core);
        var sect = docXml.Descendants(Wn + "pgSz").Single();
        Assert.Equal("11906", (string?)sect.Attribute(Wn + "w"));
        Assert.Equal("16838", (string?)sect.Attribute(Wn + "h"));
    }

    [Fact]
    public void Odt_is_a_package_with_its_mimetype_first_and_stored()
    {
        var bytes = OdtWriter.Write(Sample(), Pictures());
        using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        {
            var first = zip.Entries[0];
            Assert.Equal("mimetype", first.FullName);
            Assert.Equal(first.Length, first.CompressedLength);
        }
        // The mimetype's bytes at offset 38, as a reader sniffs them.
        Assert.Equal("application/vnd.oasis.opendocument.text", Encoding.ASCII.GetString(bytes, 38, 39));
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 8));   // method 0: stored

        var parts = Unzip(bytes);
        foreach (var name in new[] { "content.xml", "styles.xml", "meta.xml", "META-INF/manifest.xml" }) Assert.True(parts.ContainsKey(name), name);
        foreach (var (name, data) in parts.Where(p => p.Key.EndsWith(".xml") || p.Key.EndsWith(".svg"))) Parse(data);

        var content = Parse(parts["content.xml"]);
        var text = content.Root!.Value;
        Assert.Contains("Memoriu tehnic de arhitectură", text);
        Assert.Contains("ă â î ș ț Ă Â Î Ș Ț", text);
        Assert.DoesNotContain("project.designer", text);
        var tables = content.Descendants(TableNs + "table").ToList();
        Assert.Equal(2, tables.Count);
        var big = tables.Single(t => t.Element(TableNs + "table-header-rows") != null);
        Assert.Equal(61, big.Elements(TableNs + "table-row").Count());
        Assert.Single(big.Element(TableNs + "table-header-rows")!.Elements(TableNs + "table-row"));

        var frames = content.Descendants(DrawNs + "frame").ToList();
        Assert.Equal(2, frames.Count);
        var images = frames[0].Elements(DrawNs + "image").ToList();
        Assert.Equal(2, images.Count);
        Assert.EndsWith(".svg", (string?)images[0].Attribute(XName.Get("href", "http://www.w3.org/1999/xlink")));
        Assert.EndsWith(".png", (string?)images[1].Attribute(XName.Get("href", "http://www.w3.org/1999/xlink")));
        Assert.Equal(3, parts.Keys.Count(k => k.StartsWith("Pictures/")));
        Assert.Single(content.Descendants(TextNs + "table-of-content"));
        Assert.Contains("1. Date generale", content.Descendants(TextNs + "index-body").Single().Value);

        var styles = Parse(parts["styles.xml"]);
        Assert.Single(styles.Descendants(TextNs + "page-count"));
        Assert.Contains("Ionuț Ciuntuc", Encoding.UTF8.GetString(parts["meta.xml"]));
        Assert.Contains("Pictures/picture1.svg", Encoding.UTF8.GetString(parts["META-INF/manifest.xml"]));
    }

    /// <summary>Each page's content stream, inflated.</summary>
    static List<string> Streams(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var list = new List<string>();
        foreach (Match m in Regex.Matches(text, @"/Length (\d+) /Filter /FlateDecode >>\nstream\n"))
        {
            var at = m.Index + m.Length;
            using var z = new ZLibStream(new MemoryStream(pdf, at, int.Parse(m.Groups[1].Value)), CompressionMode.Decompress);
            list.Add(new StreamReader(z, Encoding.Latin1).ReadToEnd());
        }
        return list;
    }

    [Fact]
    public void Pdf_lays_the_pages_out_with_the_header_repeated_and_the_pages_counted()
    {
        var doc = Sample();
        var pdf = DocumentPdf.Write(doc);
        var raw = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF", raw);
        var pages = Regex.Matches(raw, @"/Type /Page\b(?!s)").Count;
        Assert.True(pages >= 4, $"{pages} pages");
        var streams = Streams(pdf);
        Assert.Equal(pages, streams.Count);
        for (var i = 0; i < pages; i++)
        {
            Assert.Contains("(Memoriu tehnic - Cas\\201 unifamilial\\201, Hu\\217i)", streams[i]);
            Assert.Contains($"(Pagina {i + 1} din {pages})", streams[i]);
        }
        // The long table runs over pages, its header row on each.
        Assert.True(streams.Count(s => s.Contains("(Denumire)")) >= 2);
        Assert.Contains(streams, s => s.Contains("(Total)"));
        // The table of contents gives the headings' real pages.
        var headings = DocumentPdf.Headings(doc);
        var spaces = headings.Single(h => DocText.Plain(h.Heading.Runs) == "2. Spațiile pe niveluri");
        Assert.True(spaces.Page >= 2);
        Assert.Matches($@"\({spaces.Page}\) Tj", streams[0]);
        // The bookmarks and the Info.
        Assert.Contains("/Outlines", raw);
        Assert.Contains("/Title <FEFF", raw);
        // The missing field is not printed.
        Assert.DoesNotContain(streams, s => s.Contains("project.designer"));
        // The picture's pens: its fill and its text.
        Assert.Contains(streams, s => s.Contains("(Camer\\201 de zi 24,50 m\\262)"));
    }

    [Fact]
    public void Pdf_wraps_a_long_paragraph_within_the_text_width()
    {
        var pages = DocumentPdf.Pages(Sample());
        var texts = pages.SelectMany(p => p.Pens).SelectMany(p => p.Texts).ToList();
        var page = DocPage.A4;
        foreach (var t in texts.Where(t => t.Anchor == "start" && Math.Abs(t.Height - DocText.BodyPt * 25.4 / 72 * 0.718) < 1e-6))
        {
            var w = HelveticaMetrics.Mm(t.Text, t.Bold, DocText.BodyPt);
            Assert.True(t.At.X + w <= page.W - page.Right + 0.01, $"'{t.Text}' runs past the margin");
        }
        Assert.True(texts.Count(t => Long.Contains(t.Text.Trim()) && t.Text.Length > 20) >= 3, "the long paragraph wraps on several lines");
    }

    [Fact]
    public void Samples_are_written_when_asked()
    {
        var folder = Environment.GetEnvironmentVariable("LAPIN_DOCS_SAMPLES");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        var doc = Sample();
        File.WriteAllBytes(Path.Combine(folder, "sample.docx"), DocxWriter.Write(doc, Pictures()));
        File.WriteAllBytes(Path.Combine(folder, "sample.odt"), OdtWriter.Write(doc, Pictures()));
        File.WriteAllBytes(Path.Combine(folder, "sample.pdf"), DocumentPdf.Write(doc));
        // The same with a company's style (stage 4): Georgia 12 pt, blue headings, a logo on the left, Times in the PDF.
        var styled = DocumentStyleWriterTests.Styled();
        File.WriteAllBytes(Path.Combine(folder, "sample-styled.docx"), DocxWriter.Write(styled, Pictures()));
        File.WriteAllBytes(Path.Combine(folder, "sample-styled.odt"), OdtWriter.Write(styled, Pictures()));
        File.WriteAllBytes(Path.Combine(folder, "sample-styled.pdf"), DocumentPdf.Write(styled));
    }
}
