using System.IO.Compression;
using System.Xml.Linq;
using Lapin.Drawing.Documents;
using Lapin.Drawing.Export;

namespace Lapin.Engine.Tests;

/// <summary>One XML escaping (audit 15.11.2026, D6): what XML 1.0 cannot carry is left out, so Excel opens the workbook.</summary>
public class XmlTextTests
{
    [Fact]
    public void A_name_with_a_control_character_gives_a_workbook_that_parses()
    {
        var bytes = XlsxWriter.Write([("Doors\u0001 & <co>", [["Name", "Note"], ["D\u00011 \"x\" 'y'", "lone \uD800 surrogate ￾"], ["😀 ok", "a\tb"]])]);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        foreach (var entry in zip.Entries)
        {
            using var r = new StreamReader(entry.Open());
            var xml = r.ReadToEnd();
            XDocument.Parse(xml); // throws on a character XML refuses
        }
        using var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var texts = XDocument.Parse(sheet.ReadToEnd()).Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value).ToList();
        Assert.Equal(["Name", "Note", "D1 \"x\" 'y'", "lone  surrogate ", "😀 ok", "a\tb"], texts);
        using var book = new StreamReader(zip.GetEntry("xl/workbook.xml")!.Open());
        Assert.Equal("Doors & <co>", XDocument.Parse(book.ReadToEnd()).Descendants().Single(e => e.Name.LocalName == "sheet").Attribute("name")!.Value);
    }

    [Theory]
    [InlineData("a < b & c > \"d\"", "a &lt; b &amp; c &gt; &quot;d&quot;")]
    [InlineData("x\u0001y\u001Fz", "xyz")]
    [InlineData("tab\tnew\nline", "tab\tnew\nline")]
    [InlineData("￿￾", "")]
    public void Svg_text_escapes_as_documents_do(string text, string escaped)
    {
        Assert.Equal(escaped, SvgWriter.Escape(text));
        Assert.Equal(DocText.Xml(text), SvgWriter.Escape(text));
    }
}
