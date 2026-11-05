using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing.Documents;
using Lapin.Model;

namespace Lapin.Platform.Documents;

/// <summary>
/// A filled document as the interface reads it (<c>document.preview</c>): its page, its blocks by kind with their runs (a run:
/// text, and bold, italic, field, missing only when set), its pictures with their pens in paper mm as <c>schedule.layout</c> gives them.
/// </summary>
public static class DocumentJson
{
    public static JsonObject Run(DocRun r)
    {
        var o = new JsonObject { ["text"] = r.Text };
        if (r.Bold) o["bold"] = true;
        if (r.Italic) o["italic"] = true;
        if (r.Field != null) o["field"] = r.Field;
        if (r.Missing) o["missing"] = true;
        return o;
    }

    public static JsonArray Runs(IEnumerable<DocRun> runs) => new(runs.Select(r => (JsonNode)Run(r)).ToArray());

    public static JsonObject Image(DocImage i)
    {
        var o = new JsonObject { ["key"] = i.Key, ["w"] = Math.Round(i.W, 3), ["h"] = Math.Round(i.H, 3), ["pens"] = new JsonArray(i.Pens.Select(p => (JsonNode)p.ToJson()).ToArray()) };
        if (i.Caption != null) o["caption"] = i.Caption;
        return o;
    }

    public static JsonObject Block(DocBlock b) => b switch
    {
        DocHeading h => new JsonObject { ["kind"] = "heading", ["level"] = h.Level, ["runs"] = Runs(h.Runs) },
        DocParagraph p => new JsonObject { ["kind"] = "paragraph", ["style"] = p.Style, ["runs"] = Runs(p.Runs) },
        DocList l => new JsonObject { ["kind"] = "list", ["ordered"] = l.Ordered, ["items"] = new JsonArray(l.Items.Select(i => (JsonNode)Runs(i)).ToArray()) },
        DocTable t => new JsonObject
        {
            ["kind"] = "table", ["title"] = t.Title, ["plain"] = t.Plain, ["total"] = t.TotalRow,
            ["header"] = new JsonArray(t.Header.Select(x => (JsonNode)x).ToArray()),
            ["numeric"] = new JsonArray(t.Numeric.Order().Select(x => (JsonNode)x).ToArray()),
            ["widths"] = t.Widths == null ? null : new JsonArray(t.Widths.Select(x => (JsonNode)x).ToArray()),
            ["rows"] = new JsonArray(t.Rows.Select(r => (JsonNode)new JsonArray(r.Select(c => (JsonNode)Runs(c)).ToArray())).ToArray()),
        },
        DocImage i => Image(i).Also(o => o["kind"] = "image"),
        DocToc toc => new JsonObject { ["kind"] = "toc", ["title"] = toc.Title },
        DocPageBreak => new JsonObject { ["kind"] = "pageBreak" },
        _ => new JsonObject { ["kind"] = "unknown" },
    };

    static JsonObject Also(this JsonObject o, Action<JsonObject> a) { a(o); return o; }

    /// <summary>The blocks' pictures, wherever they are.</summary>
    public static IEnumerable<DocImage> Images(FilledDocument d) => d.Blocks.OfType<DocImage>();

    public static JsonObject Document(FilledDocument d, IReadOnlyList<string> missing, Entity doc) => new()
    {
        ["id"] = doc.Id, ["title"] = d.Title,
        ["page"] = new JsonObject
        {
            ["w"] = d.Page.W, ["h"] = d.Page.H, ["margins"] = new JsonArray(d.Page.Top, d.Page.Right, d.Page.Bottom, d.Page.Left),
            ["header"] = Runs(d.Page.Header), ["footer"] = Runs(d.Page.Footer),
            // The style as kept (fonts, size, the headings' colour, the logo), for the preview to look as the files will.
            ["style"] = (doc.Attrs["page"] as JsonObject)?["style"]?.DeepClone(),
        },
        ["meta"] = new JsonObject { ["author"] = d.Meta.Author, ["subject"] = d.Meta.Subject, ["revision"] = d.Meta.Revision, ["status"] = d.Meta.Status, ["lang"] = d.Meta.Lang },
        ["blocks"] = new JsonArray(d.Blocks.Select(b => (JsonNode)Block(b)).ToArray()),
        ["missing"] = new JsonArray(missing.Select(x => (JsonNode)x).ToArray()),
        ["inputs"] = new JsonArray(DocumentFill.Inputs(doc).Select(x => (JsonNode)x.Input.ToJson().Also(o => o["value"] = x.Value)).ToArray()),
    };

    /// <summary>The plain text of runs (a test's, a list's).</summary>
    public static string Text(IEnumerable<DocRun> runs) => string.Concat(runs.Select(r => r.Missing ? $"⟨{r.Text}⟩" : r.Text));
}

/// <summary>A document written out: DOCX, ODT or PDF from the same filled document; its file name; the pictures the interface made.</summary>
public static class DocumentExport
{
    public static readonly string[] Formats = ["docx", "odt", "pdf"];

    /// <summary>Writes a filled document in a format (the writers of <c>Lapin.Drawing.Documents</c>).</summary>
    public delegate byte[] Writer(FilledDocument doc, string format, IReadOnlyDictionary<string, DocPicture>? pictures);

    public static byte[] Write(FilledDocument doc, string format, IReadOnlyDictionary<string, DocPicture>? pictures) => format switch
    {
        "docx" => DocxWriter.Write(doc, pictures),
        "odt" => OdtWriter.Write(doc, pictures),
        "pdf" => DocumentPdf.Write(doc),
        _ => throw new CommandException($"A document is exported as {string.Join(", ", Formats)}."),
    };

    /// <summary>A file name from the document's name: what a file system refuses replaced, spaces kept, never empty.</summary>
    public static string FileName(string name, string ext)
    {
        var bad = new HashSet<char>(Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*']));
        var sb = new StringBuilder();
        foreach (var c in name.Trim()) sb.Append(bad.Contains(c) || char.IsControl(c) ? '_' : c);
        var stem = sb.ToString().Trim().TrimEnd('.');
        if (stem.Length > 120) stem = stem[..120].TrimEnd();
        return (stem.Length == 0 ? "Document" : stem) + "." + ext;
    }

    /// <summary>The pictures the interface made of a document's images: {key: {svg?, png? (base64)}}.</summary>
    public static Dictionary<string, DocPicture>? Pictures(JsonNode? images)
    {
        if (images is not JsonObject o) return null;
        var d = new Dictionary<string, DocPicture>();
        foreach (var (key, v) in o)
        {
            if (v is not JsonObject p) continue;
            byte[]? png = null;
            if ((string?)p["png"] is { Length: > 0 } b64)
            {
                try { png = Convert.FromBase64String(b64.Contains(',') ? b64[(b64.IndexOf(',') + 1)..] : b64); }
                catch (FormatException) { throw new CommandException($"The picture „{key}” is not base64."); }
            }
            d[key] = new DocPicture((string?)p["svg"], png);
        }
        return d;
    }

    /// <summary>The document filled and written: its file's name, its bytes as base64, and the fields left to complete.</summary>
    public static JsonObject Export(IQueryContext q, Entity doc, string? format, JsonNode? images, Writer? writer = null)
    {
        var fmt = format?.Trim().ToLowerInvariant() is { } x && Formats.Contains(x) ? x : throw new CommandException($"A document is exported as {string.Join(", ", Formats)}.");
        var (filled, missing) = DocumentFill.Fill(q, doc);
        var bytes = (writer ?? Write)(filled, fmt, Pictures(images));
        return new JsonObject
        {
            ["name"] = FileName(doc.Name, fmt), ["data"] = Convert.ToBase64String(bytes),
            ["missing"] = new JsonArray(missing.Select(m => (JsonNode)m).ToArray()),
        };
    }
}
