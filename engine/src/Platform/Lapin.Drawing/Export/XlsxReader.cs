using System.Globalization;
using System.IO.Compression;
using System.Xml;

namespace Lapin.Drawing.Export;

/// <summary>
/// A workbook's first worksheet read as a table of text (Office Open XML, no library): shared strings, inline strings,
/// numbers as written (invariant), booleans; a cell's place taken from its reference, so the empty cells a sheet leaves
/// out stay empty columns. Enough for the tables a user imports (norms, prices), not formulas' results beyond their cache.
/// </summary>
public static class XlsxReader
{
    const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>Whether the bytes are a zip (a workbook), not text.</summary>
    public static bool IsZip(ReadOnlySpan<byte> bytes) => bytes.Length > 4 && bytes[0] == (byte)'P' && bytes[1] == (byte)'K';

    public static List<List<string>> Read(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var shared = new List<string>();
        if (zip.GetEntry("xl/sharedStrings.xml") is { } ss)
        {
            var doc = Load(ss);
            foreach (XmlElement si in doc.GetElementsByTagName("si", Main)) shared.Add(TextOf(si));
        }
        var sheet = FirstSheet(zip) ?? throw new InvalidDataException("The workbook has no worksheet.");
        var rows = new List<List<string>>();
        var d = Load(sheet);
        foreach (XmlElement row in d.GetElementsByTagName("row", Main))
        {
            var r = int.TryParse(row.GetAttribute("r"), out var rn) ? rn - 1 : rows.Count;
            while (rows.Count <= r) rows.Add([]);
            var cells = rows[r];
            foreach (XmlElement c in row.GetElementsByTagName("c", Main))
            {
                var col = ColumnOf(c.GetAttribute("r"), cells.Count);
                var type = c.GetAttribute("t");
                var v = c.GetElementsByTagName("v", Main).Cast<XmlElement>().FirstOrDefault()?.InnerText;
                var text = type switch
                {
                    "s" => int.TryParse(v, out var i) && i >= 0 && i < shared.Count ? shared[i] : "",
                    "inlineStr" => c.GetElementsByTagName("is", Main).Cast<XmlElement>().FirstOrDefault() is { } isn ? TextOf(isn) : "",
                    "b" => v == "1" ? "TRUE" : "FALSE",
                    _ => v ?? "",
                };
                while (cells.Count <= col) cells.Add("");
                cells[col] = text;
            }
        }
        return rows;
    }

    static XmlDocument Load(ZipArchiveEntry entry)
    {
        var doc = new XmlDocument { XmlResolver = null };
        using var s = entry.Open();
        using var reader = XmlReader.Create(s, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        doc.Load(reader);
        return doc;
    }

    /// <summary>The workbook's first sheet, by its relationship; else the first worksheet part there is.</summary>
    static ZipArchiveEntry? FirstSheet(ZipArchive zip)
    {
        try
        {
            if (zip.GetEntry("xl/workbook.xml") is { } wb && zip.GetEntry("xl/_rels/workbook.xml.rels") is { } rels)
            {
                var first = Load(wb).GetElementsByTagName("sheet", Main).Cast<XmlElement>().FirstOrDefault();
                var rid = first?.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                if (!string.IsNullOrEmpty(rid))
                    foreach (XmlElement rel in Load(rels).GetElementsByTagName("Relationship", "http://schemas.openxmlformats.org/package/2006/relationships"))
                        if (rel.GetAttribute("Id") == rid)
                        {
                            var target = rel.GetAttribute("Target").TrimStart('/');
                            var path = target.StartsWith("xl/", StringComparison.Ordinal) ? target : "xl/" + target;
                            if (zip.GetEntry(path) is { } hit) return hit;
                        }
            }
        }
        catch (XmlException) { }
        return zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal))
            .OrderBy(e => e.FullName, StringComparer.Ordinal).FirstOrDefault();
    }

    /// <summary>A rich or plain string's text: its runs' t elements joined (the phonetic runs left out).</summary>
    static string TextOf(XmlElement si) =>
        string.Concat(si.GetElementsByTagName("t", Main).Cast<XmlElement>().Where(t => (t.ParentNode as XmlElement)?.LocalName != "rPh").Select(t => t.InnerText));

    /// <summary>The column of a reference ("C12": 2); the next one when it has none.</summary>
    static int ColumnOf(string reference, int next)
    {
        var n = 0;
        var any = false;
        foreach (var ch in reference)
        {
            if (ch is >= 'A' and <= 'Z') { n = n * 26 + (ch - 'A' + 1); any = true; }
            else break;
        }
        return any ? n - 1 : next;
    }

    /// <summary>A number as a sheet or a CSV writes it: invariant, or with a decimal comma (1,5) and a space or dot grouping thousands.</summary>
    public static double? Number(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim().Replace(" ", "").Replace(" ", "");
        // The mark written last of a dot and a comma is the decimal one (1,234.5 and 1.234,5); a lone comma is too (1,5).
        if (t.Contains(',') && t.LastIndexOf(',') > t.LastIndexOf('.')) t = t.Replace(".", "").Replace(',', '.');
        else t = t.Replace(",", "");
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
    }
}
