using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace Lapin.Drawing.Export;

/// <summary>
/// A workbook written by hand (Office Open XML, no library): one worksheet a
/// table, its first row bold and frozen, numbers as numbers, text inline.
/// </summary>
public static class XlsxWriter
{
    public static byte[] Write(IReadOnlyList<(string Name, IReadOnlyList<IReadOnlyList<string>> Rows)> sheets)
    {
        if (sheets.Count == 0) sheets = [("Sheet1", [])];
        var names = new List<string>();
        foreach (var (name, _) in sheets)
        {
            var clean = new string(name.Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim();
            if (clean.Length == 0) clean = "Sheet";
            if (clean.Length > 31) clean = clean[..31];
            var unique = clean;
            for (var i = 2; names.Contains(unique, StringComparer.OrdinalIgnoreCase); i++) unique = $"{clean[..Math.Min(clean.Length, 28)]} {i}";
            names.Add(unique);
        }
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Put(string path, string xml)
            {
                using var s = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
                var bytes = Encoding.UTF8.GetBytes(xml);
                s.Write(bytes);
            }
            const string head = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n";
            Put("[Content_Types].xml", head + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
                + string.Concat(names.Select((_, i) => $"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"))
                + "</Types>");
            Put("_rels/.rels", head + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Put("xl/workbook.xml", head + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>"
                + string.Concat(names.Select((n, i) => $"<sheet name=\"{SecurityElement.Escape(n)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>")) + "</sheets></workbook>");
            Put("xl/_rels/workbook.xml.rels", head + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + string.Concat(names.Select((_, i) => $"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>"))
                + $"<Relationship Id=\"rId{names.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            Put("xl/styles.xml", head + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                + "<fonts count=\"2\"><font><sz val=\"10\"/><name val=\"Arial\"/></font><font><b/><sz val=\"10\"/><name val=\"Arial\"/></font></fonts>"
                + "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>"
                + "<borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs>"
                + "<cellXfs count=\"2\"><xf fontId=\"0\" xfId=\"0\"/><xf fontId=\"1\" xfId=\"0\" applyFont=\"1\"/></cellXfs>"
                + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
            for (var i = 0; i < sheets.Count; i++)
            {
                var sb = new StringBuilder(head);
                sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews><sheetData>");
                var rows = sheets[i].Rows;
                for (var r = 0; r < rows.Count; r++)
                {
                    sb.Append($"<row r=\"{r + 1}\">");
                    for (var c = 0; c < rows[r].Count; c++)
                    {
                        var v = rows[r][c] ?? "";
                        var reference = Column(c) + (r + 1);
                        var style = r == 0 ? " s=\"1\"" : "";
                        if (r > 0 && IsNumber(v, out var d))
                            sb.Append($"<c r=\"{reference}\"{style}><v>{d.ToString(CultureInfo.InvariantCulture)}</v></c>");
                        else if (v.Length > 0)
                            sb.Append($"<c r=\"{reference}\"{style} t=\"inlineStr\"><is><t xml:space=\"preserve\">{SecurityElement.Escape(v)}</t></is></c>");
                    }
                    sb.Append("</row>");
                }
                sb.Append("</sheetData></worksheet>");
                Put($"xl/worksheets/sheet{i + 1}.xml", sb.ToString());
            }
        }
        return ms.ToArray();
    }

    /// <summary>Whether a cell is a number: only when the number writes back as the same text (\"007\", \"+5\" or \"1e3\" stay text).</summary>
    static bool IsNumber(string v, out double d) =>
        double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && double.IsFinite(d) && d.ToString(CultureInfo.InvariantCulture) == v;

    /// <summary>A column's letters: 0 → A, 25 → Z, 26 → AA.</summary>
    public static string Column(int i)
    {
        var s = "";
        for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s;
        return s;
    }
}
