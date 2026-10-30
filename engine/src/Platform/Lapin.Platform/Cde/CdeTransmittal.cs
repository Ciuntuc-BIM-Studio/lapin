using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;

namespace Lapin.Platform.Cde;

/// <summary>
/// What goes with an issue besides the drawings: the transmittal (a plain A4
/// PDF and a CSV: project, issue, date, revision, status, the files with
/// their hashes, the note) and the manifest (JSON, one entry per file with
/// its SHA-256), so whoever receives the folder can tell what was sent and
/// that it arrived whole.
/// </summary>
public static class CdeTransmittal
{
    public sealed record Header(string Project, string Issue, int Number, string Date, string Revision, string Status, string Container, string? Note, string Originator);

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static string Sha256(string text) => Sha256(Encoding.UTF8.GetBytes(text));

    static readonly string[] Columns = ["File", "Kind", "Sheet", "Title", "Revision", "Status", "SHA-256", "Size"];

    /// <summary>A row of the transmittal: the file, what it is, the sheet number and title (empty for the model).</summary>
    public sealed record Row(IssuedFile File, string? SheetNumber, string? SheetTitle);

    static string Csv(string s) => s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

    /// <summary>The transmittal as CSV (UTF-8, a header row, one row per file).</summary>
    public static string Csv(Header h, IReadOnlyList<Row> rows)
    {
        var sb = new StringBuilder();
        sb.Append("Project,").Append(Csv(h.Project)).Append('\n');
        sb.Append("Issue,").Append(Csv(h.Issue)).Append('\n');
        sb.Append("Date,").Append(h.Date).Append('\n');
        sb.Append("Originator,").Append(Csv(h.Originator)).Append('\n');
        sb.Append("Container,").Append(h.Container).Append('\n');
        if (h.Note is { Length: > 0 }) sb.Append("Note,").Append(Csv(h.Note)).Append('\n');
        sb.Append('\n');
        sb.Append(string.Join(",", Columns)).Append('\n');
        foreach (var r in rows)
            sb.Append(string.Join(",", [Csv(r.File.Name), r.File.Kind.ToUpperInvariant(), Csv(r.SheetNumber ?? ""), Csv(r.SheetTitle ?? ""), h.Revision, h.Status, r.File.Sha256, r.File.Size.ToString(CultureInfo.InvariantCulture)])).Append('\n');
        return sb.ToString();
    }

    /// <summary>The manifest: the issue and every file with its hash, for a check of the folder later.</summary>
    public static string Manifest(Header h, IReadOnlyList<Row> rows, string transmittalName)
    {
        var o = new JsonObject
        {
            ["format"] = "lapin-issue/1", ["project"] = h.Project, ["issue"] = h.Issue, ["number"] = h.Number, ["date"] = h.Date, ["originator"] = h.Originator,
            ["revision"] = h.Revision, ["status"] = h.Status, ["container"] = h.Container, ["note"] = h.Note, ["transmittal"] = transmittalName,
            ["files"] = new JsonArray(rows.Select(r =>
            {
                var f = r.File.ToJson();
                if (r.SheetNumber != null) f["sheetNumber"] = r.SheetNumber;
                if (r.SheetTitle != null) f["sheetTitle"] = r.SheetTitle;
                return (JsonNode)f;
            }).ToArray()),
        };
        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>The transmittal as a PDF: an A4 portrait page (more when the files run on), the header, the table, the note.</summary>
    public static byte[] Pdf(Header h, IReadOnlyList<Row> rows)
    {
        const double W = 210, H = 297, M = 18, Line = 6;
        var pages = new List<PaperPage>();
        var perPage = (int)((H - 2 * M - 70) / Line);
        var chunks = rows.Count == 0 ? [new List<Row>()] : rows.Chunk(Math.Max(1, perPage)).Select(c => c.ToList()).ToList();
        for (var pi = 0; pi < chunks.Count; pi++)
        {
            var text = new Pen("TEXT", "#000000", 0, null, null);
            var rules = new Pen("TITLE", "#000000", 0.25, null, null);
            double y = M;
            void T(double x, string s, double size = 3, bool bold = false, string anchor = "start") => text.Texts.Add(new PenText(new Vec2(x, y), s, size, Anchor: anchor, Bold: bold));
            y += 6; T(M, "TRANSMITTAL", 6, true); T(W - M, h.Project, 4, true, "end");
            y += 7; T(M, $"{h.Issue} · issue {h.Number} · {h.Date}", 3.2); T(W - M, $"from {h.Originator}", 3, anchor: "end");
            y += 5; T(M, $"Revision {h.Revision} · status {h.Status} · {h.Container}", 3.2);
            if (chunks.Count > 1) { T(W - M, $"page {pi + 1} / {chunks.Count}", 2.6, anchor: "end"); }
            y += 4; rules.Segments.Add((new Vec2(M, y), new Vec2(W - M, y)));
            // The table: file, kind, sheet, title, size; the hashes in the manifest.
            double[] cols = [M, M + 78, M + 90, M + 108, W - M];
            y += 5; T(cols[0], "File", 2.6, true); T(cols[1], "Kind", 2.6, true); T(cols[2], "Sheet", 2.6, true); T(cols[3], "Title", 2.6, true); T(cols[4], "Size", 2.6, true, "end");
            y += 1.5; rules.Segments.Add((new Vec2(M, y), new Vec2(W - M, y)));
            foreach (var r in chunks[pi])
            {
                y += Line;
                T(cols[0], r.File.Name, 2.6); T(cols[1], r.File.Kind.ToUpperInvariant(), 2.6); T(cols[2], r.SheetNumber ?? "", 2.6);
                T(cols[3], Fit(r.SheetTitle ?? (r.File.Kind == "ifc" ? "Model (IFC)" : ""), 40), 2.6); T(cols[4], Size(r.File.Size), 2.6, anchor: "end");
            }
            y += 2; rules.Segments.Add((new Vec2(M, y), new Vec2(W - M, y)));
            if (pi == chunks.Count - 1)
            {
                y += 7; T(M, $"{rows.Count} file(s). Each file's SHA-256 is in the manifest beside this transmittal.", 2.6);
                if (h.Note is { Length: > 0 })
                {
                    y += 7; T(M, "Note", 3, true);
                    foreach (var line in Wrap(h.Note, 110)) { y += 4.5; T(M, line, 2.8); }
                }
            }
            pages.Add(new PaperPage(W, H, [rules, text], $"Transmittal {h.Issue}"));
        }
        return PdfWriter.Write(pages);
    }

    static string Fit(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
    static string Size(long bytes) => bytes >= 1_048_576 ? $"{bytes / 1_048_576.0:0.0} MB" : $"{Math.Max(1, bytes / 1024)} kB";

    static IEnumerable<string> Wrap(string text, int width)
    {
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var line = new StringBuilder();
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width) { yield return line.ToString(); line.Clear(); }
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }
            yield return line.ToString();
        }
    }
}
