using System.IO.Compression;
using System.Text;

namespace Lapin.Drawing.Documents;

/// <summary>
/// What the three writers share, so a document says the same thing in each: a run's text (a missing field empty, the page
/// fields filled where the writer knows them), XML escaping, the look (sizes in points) and a table's column widths,
/// measured with Helvetica's widths whatever the format's font (Arial has the same).
/// </summary>
public static class DocText
{
    public const double BodyPt = 10.5, CaptionPt = 9, NotePt = 9, HeaderPt = 9, TablePt = 9.5;
    public const string Font = "Arial";

    /// <summary>A heading's size by its level.</summary>
    public static double HeadingPt(int level) => level <= 1 ? 16 : level == 2 ? 13 : 11.5;

    /// <summary>A run as text: a missing field empty, "page" and "pages" the numbers given (empty when not known).</summary>
    public static string Of(DocRun r, int? page = null, int? pages = null) =>
        r.Missing ? "" : r.Field switch { "page" => page?.ToString() ?? "", "pages" => pages?.ToString() ?? "", _ => r.Text ?? "" };

    /// <summary>Runs as plain text.</summary>
    public static string Plain(IEnumerable<DocRun> runs) => string.Concat(runs.Select(r => Of(r)));

    /// <summary>Text for XML: escaped, the characters XML 1.0 cannot carry left out.</summary>
    public static string Xml(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var b = new StringBuilder(s.Length + 8);
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            switch (ch)
            {
                case '&': b.Append("&amp;"); break;
                case '<': b.Append("&lt;"); break;
                case '>': b.Append("&gt;"); break;
                case '"': b.Append("&quot;"); break;
                default:
                    if (char.IsHighSurrogate(ch) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { b.Append(ch).Append(s[++i]); }
                    else if (char.IsSurrogate(ch) || (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') || ch == '￾' || ch == '￿') { }
                    else b.Append(ch);
                    break;
            }
        }
        return b.ToString();
    }

    /// <summary>A table's cell as one text (its runs joined).</summary>
    public static string Cell(DocTable t, int row, int col) => col < t.Rows[row].Count ? Plain(t.Rows[row][col]) : "";

    /// <summary>
    /// A table's column widths (mm) filling <paramref name="width"/>: by its relative Widths when given, else by content,
    /// each column at least as wide as its longest word, the rest shared by how much text each holds on one line.
    /// </summary>
    public static double[] ColumnWidths(DocTable t, double width, double padding = 3)
    {
        var n = Math.Max(t.Header.Count, t.Rows.Count == 0 ? 0 : t.Rows.Max(r => r.Count));
        if (n == 0) return [];
        if (t.Widths is { } given && given.Count >= n && given.Take(n).All(w => w > 0))
        {
            var sum = given.Take(n).Sum();
            return [.. given.Take(n).Select(w => w / sum * width)];
        }
        var natural = new double[n];
        var least = new double[n];
        void Take(int c, string text, bool bold)
        {
            natural[c] = Math.Max(natural[c], HelveticaMetrics.Mm(text, bold, TablePt) + padding);
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                least[c] = Math.Max(least[c], Math.Min(HelveticaMetrics.Mm(word, bold, TablePt), width / n) + padding);
        }
        if (!t.Plain) for (var c = 0; c < t.Header.Count; c++) Take(c, t.Header[c], true);
        for (var r = 0; r < t.Rows.Count; r++)
            for (var c = 0; c < t.Rows[r].Count; c++) Take(c, Cell(t, r, c), t.TotalRow && r == t.Rows.Count - 1);
        for (var c = 0; c < n; c++) { natural[c] = Math.Max(natural[c], padding + 4); least[c] = Math.Max(least[c], padding + 4); }
        var total = natural.Sum();
        if (total <= width) return [.. natural.Select(w => w / total * width)];
        var floor = least.Sum();
        if (floor >= width) return [.. least.Select(w => w / floor * width)];
        var extra = natural.Select((w, c) => w - least[c]).ToArray();
        var spare = extra.Sum();
        return [.. least.Select((w, c) => w + (spare > 0 ? extra[c] / spare * (width - floor) : 0))];
    }

    /// <summary>A picture's size on the page (mm): its own, scaled down to the text width.</summary>
    public static (double W, double H) Fit(DocImage image, double textWidth)
    {
        if (image.W <= 0 || image.H <= 0) return (0, 0);
        var s = Math.Min(1, textWidth / image.W);
        return (image.W * s, image.H * s);
    }

    /// <summary>The headings of levels 1–2 for a table of contents, with their pages as the PDF lays them out (from 1).</summary>
    public static IReadOnlyList<(DocHeading Heading, int Page)> Contents(FilledDocument doc) =>
        [.. DocumentPdf.Headings(doc).Where(h => h.Heading.Level <= 2)];

    /// <summary>Millimetres in twentieths of a point (DOCX) and in EMU (DrawingML).</summary>
    public static long Twips(double mm) => (long)Math.Round(mm * 1440 / 25.4);
    public static long Emu(double mm) => (long)Math.Round(mm * 36000);

    /// <summary>A zip entry from text (UTF-8, no byte order mark), compressed.</summary>
    public static void Put(ZipArchive zip, string path, string text) => Put(zip, path, new UTF8Encoding(false).GetBytes(text));

    public static void Put(ZipArchive zip, string path, byte[] data, bool store = false)
    {
        using var s = zip.CreateEntry(path, store ? CompressionLevel.NoCompression : CompressionLevel.Optimal).Open();
        s.Write(data);
    }

    public const string XmlHead = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n";
}
