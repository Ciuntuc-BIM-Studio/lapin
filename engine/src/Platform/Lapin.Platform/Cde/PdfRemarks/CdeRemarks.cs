using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Cde.PdfRemarks;

/// <summary>A file an issue manifest (<c>lapin-issue/1</c>) or the project's issue record lists: its name, the sheet it prints (null for the set and the model), the issue's revision and id.</summary>
public sealed record ManifestFile(string Name, string? SheetId, string Revision, string? IssueId);

/// <summary>
/// Remarks on sheets (docs/plan-cde.md, stage 3): what a reviewer marked on
/// an issued PDF — notes, free text, highlights and strikes, ink, squares,
/// circles, lines, clouds, stamps, Bluebeam's measurements — read back into
/// the project as <c>LapinSheetRemark</c> entities (resources, never exported to
/// IFC) on the sheet, at their place in paper millimetres, with the text,
/// who and when, the replies of the thread, and a status the user sets here
/// (open, resolved, closed). A remark remembers its source (the file, its
/// revision, the annotation's name or object), so reading the same PDF again
/// updates it instead of making another. The sheet a PDF belongs to comes
/// from the project's issue record or the folder's manifest first, then the
/// file name's convention, else the user's choice.
/// </summary>
public static class CdeRemarks
{
    public const string Class = "LapinSheetRemark";
    public static readonly string[] Statuses = ["open", "resolved", "closed"];

    // ------------------------------------------------------------ which sheet

    public sealed record SheetMatch(List<string?> PageSheets, string? Revision, string? IssueId, string By);

    /// <summary>
    /// The sheet of each page of a PDF: by the project's issues (the file's name among an issue's files; the set's pages in the issue's sheet order),
    /// by the folder's manifests the same way, by the file name's convention (the sheet whose name the convention gives), else none; or the sheet given.
    /// </summary>
    public static SheetMatch Match(IModelView m, string fileName, int pages, IReadOnlyList<ManifestFile>? manifest, string? sheetId, byte[]? pdf = null)
    {
        var name = Path.GetFileName(fileName);
        List<string?> All(string? id) => Enumerable.Repeat(id, Math.Max(pages, 1)).ToList();
        if (sheetId != null) return new SheetMatch(All(sheetId), RevisionOf(name), null, "chosen");

        // The project's own record of its issues, newest first.
        foreach (var issue in CdeIssues.All(m).AsEnumerable().Reverse())
        {
            var file = issue.Files.FirstOrDefault(f => f.Kind == "pdf" && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (file == null) continue;
            if (file.SheetId != null) return new SheetMatch(All(file.SheetId), issue.Revision, issue.Id, "issue");
            var set = issue.Files.Where(f => f.Kind == "pdf" && f.SheetId != null).Select(f => f.SheetId).ToList();
            if (set.Count > 0) return new SheetMatch(Enumerable.Range(0, Math.Max(pages, 1)).Select(i => i < set.Count ? set[i] : null).ToList(), issue.Revision, issue.Id, "issue");
        }
        // Renamed on the way: the issued bytes are where the file begins (a reviewer's program saves its marks as an incremental update, after them).
        if (pdf != null)
            foreach (var issue in CdeIssues.All(m).AsEnumerable().Reverse())
            {
                var file = issue.Files.FirstOrDefault(f => f.Kind == "pdf" && f.Size > 0 && f.Size <= pdf.LongLength && f.Sha256.Length == 64
                    && Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(pdf.AsSpan(0, (int)f.Size))) == f.Sha256);
                if (file == null) continue;
                if (file.SheetId != null) return new SheetMatch(All(file.SheetId), issue.Revision, issue.Id, "issue");
                // The transmittal (named as its manifest) prints no sheet.
                if (issue.Files.Any(j => j.Kind == "json" && Path.GetFileNameWithoutExtension(j.Name) == Path.GetFileNameWithoutExtension(file.Name))) break;
                var set = issue.Files.Where(f => f.Kind == "pdf" && f.SheetId != null).Select(f => f.SheetId).ToList();
                if (set.Count > 0) return new SheetMatch(Enumerable.Range(0, Math.Max(pages, 1)).Select(i => i < set.Count ? set[i] : null).ToList(), issue.Revision, issue.Id, "issue");
            }
        if (manifest != null)
        {
            var file = manifest.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (file != null)
            {
                if (file.SheetId != null && m.Get(file.SheetId) is { Class: Sheets.Class }) return new SheetMatch(All(file.SheetId), file.Revision, file.IssueId, "manifest");
                var set = manifest.Where(f => f.IssueId == file.IssueId && f.SheetId != null && f.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).Select(f => f.SheetId).ToList();
                if (file.SheetId == null && set.Count > 0) return new SheetMatch(Enumerable.Range(0, Math.Max(pages, 1)).Select(i => i < set.Count ? set[i] : null).ToList(), file.Revision, file.IssueId, "manifest");
            }
        }
        // The convention: the sheet whose name the naming gives, or failing the whole stem, the one with the same number field.
        var naming = CdeHooks.Naming?.Invoke(CdeNaming.Of(m)) ?? CdeNaming.Of(m);
        if (naming.Parse(name) is { } parsed)
        {
            var stem = Path.GetFileNameWithoutExtension(name).Split('_')[0];
            var sheets = m.OfClass(Sheets.Class).ToList();
            var byStem = sheets.FirstOrDefault(s => string.Equals(naming.For(m, s), stem, StringComparison.OrdinalIgnoreCase));
            var hit = byStem ?? sheets.FirstOrDefault(s => naming.For(m, s).Split('-').Last() == parsed.Number);
            if (hit != null) return new SheetMatch(All(hit.Id), parsed.Revision, null, "name");
        }
        return new SheetMatch(All(null), RevisionOf(name), null, "none");
    }

    /// <summary>The revision in a file name by the convention (…_S2_P01.pdf), when it has one.</summary>
    static string? RevisionOf(string name)
    {
        var parts = Path.GetFileNameWithoutExtension(name).Split('_');
        return parts.Length >= 3 && CdeRevisions.IsRevision(parts[^1]) ? parts[^1] : null;
    }

    // ------------------------------------------------------------ from a PDF

    /// <summary>
    /// A PDF read for its remarks: the annotations of each page that has a sheet, mapped onto the sheet's paper, threaded by their replies,
    /// as the JSON <c>cde.importRemarks</c> takes, with what was matched and the warnings met. Never throws for a malformed file.
    /// </summary>
    public static JsonObject FromPdf(IModelView m, byte[] pdf, string fileName, IReadOnlyList<ManifestFile>? manifest, string? sheetId)
    {
        var name = Path.GetFileName(fileName);
        PdfRead read;
        try { read = PdfAnnotations.Read(pdf); }
        catch (Exception e) { read = new PdfRead([], [], [$"The file could not be read: {e.Message}"], 0); }
        var match = Match(m, name, read.Pages.Count, manifest, sheetId, pdf);
        var warnings = new List<string>(read.Warnings);
        var remarks = new JsonArray();
        var unplaced = 0;
        var readAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var sheets = new JsonArray();
        for (var pi = 0; pi < read.Pages.Count; pi++)
        {
            var sid = pi < match.PageSheets.Count ? match.PageSheets[pi] : null;
            var sheet = sid != null && m.Get(sid) is { Class: Sheets.Class } s ? s : null;
            var onPage = read.Annotations.Where(a => a.Page == pi).ToList();
            sheets.Add((JsonNode)new JsonObject { ["page"] = pi + 1, ["sheetId"] = sheet?.Id, ["number"] = sheet?.Str("number"), ["name"] = sheet?.Name, ["annotations"] = onPage.Count });
            if (sheet == null) { unplaced += onPage.Count; continue; }
            var (w, h) = Sheets.PaperOf(sheet);
            var map = new PdfPageMap(read.Pages[pi], w, h);
            if (map.Scaled) warnings.Add($"Page {pi + 1} is {map.Displayed.W / PdfPageMap.Pt:0} × {map.Displayed.H / PdfPageMap.Pt:0} mm, not the sheet's {w:0} × {h:0} mm: its remarks were scaled onto the paper.");
            foreach (var r in Thread(onPage, map, sheet.Id, name, match.Revision, match.IssueId, pi + 1, readAt)) remarks.Add((JsonNode)r);
        }
        return new JsonObject
        {
            ["file"] = name, ["pages"] = read.Pages.Count, ["annotations"] = read.Annotations.Count, ["skipped"] = read.Skipped, ["unplaced"] = unplaced,
            ["by"] = match.By, ["revision"] = match.Revision, ["issueId"] = match.IssueId, ["sheets"] = sheets, ["remarks"] = remarks,
            ["warnings"] = new JsonArray(warnings.Distinct().Select(x => (JsonNode)x).ToArray()),
        };
    }

    /// <summary>The kind a remark is, from the annotation's subtype (and its intent: a Bluebeam cloud is a polygon with /IT /PolygonCloud).</summary>
    public static string KindOf(string subtype, string? intent) => subtype switch
    {
        "Text" => "note", "FreeText" => intent == "FreeTextCallout" ? "callout" : "text", "Highlight" => "highlight", "Underline" => "underline", "Squiggly" => "squiggly",
        "StrikeOut" => "strike", "Ink" => "ink", "Square" => "rect", "Circle" => "ellipse", "Line" => intent == "LineArrow" ? "arrow" : "line",
        "Polygon" => intent == "PolygonCloud" ? "cloud" : "polygon", "PolyLine" => "polyline", "Stamp" => "stamp", "Caret" => "caret", "Redact" => "redact", "FileAttachment" => "attachment",
        _ => "other",
    };

    /// <summary>The annotations of one page as remark JSON: each root with its replies (by /IRT, nested ones folded to the root), review states as replies and the thread's state.</summary>
    static IEnumerable<JsonObject> Thread(List<PdfAnnotation> page, PdfPageMap map, string sheetId, string file, string? revision, string? issueId, int pageNumber, string readAt)
    {
        var byKey = page.ToDictionary(a => a.Key);
        string Root(PdfAnnotation a)
        {
            var k = a;
            for (var guard = 0; k.InReplyTo != null && byKey.TryGetValue(k.InReplyTo, out var parent) && guard < 32; guard++) k = parent;
            return k.Key;
        }
        var replies = page.Where(a => a.InReplyTo != null && byKey.ContainsKey(a.InReplyTo)).GroupBy(Root).ToDictionary(g => g.Key, g => g.OrderBy(a => a.Modified ?? a.Created ?? "").ToList());
        foreach (var a in page)
        {
            if (a.InReplyTo != null && byKey.ContainsKey(a.InReplyTo)) continue;   // folded into its root
            if (a.StateModel != null && a.InReplyTo != null) continue;              // a review state of a remark that is not here: nothing to show
            var thread = replies.GetValueOrDefault(a.Key) ?? [];
            var o = new JsonObject
            {
                ["sheetId"] = sheetId, ["kind"] = KindOf(a.Subtype, a.Intent), ["text"] = a.Contents, ["subject"] = a.Subject, ["author"] = a.Author,
                ["date"] = a.Modified ?? a.Created, ["color"] = a.Color,
                ["source"] = new JsonObject { ["file"] = file, ["revision"] = revision, ["issueId"] = issueId, ["page"] = pageNumber, ["annot"] = a.Name ?? a.Key, ["read"] = readAt },
            };
            if (Geometry(a, map, out var rect, out var paths))
            {
                o["rect"] = rect;
                if (paths.Count > 0) o["paths"] = paths;
            }
            var replyList = new JsonArray();
            string? state = null;
            foreach (var r in thread)
            {
                if (r.StateModel != null)
                {
                    // Acrobat's review states (Accepted, Rejected, Cancelled, Completed) and marked states (Marked, Unmarked): the thread's last one counts.
                    if (r.StateModel == "Review" && r.State is { Length: > 0 } st && st != "None") state = st;
                    continue;
                }
                replyList.Add((JsonNode)new JsonObject { ["annot"] = r.Name ?? r.Key, ["author"] = r.Author, ["date"] = r.Modified ?? r.Created, ["text"] = r.Contents });
            }
            o["replies"] = replyList;
            if (state != null) o["state"] = state;
            yield return o;
        }
    }

    /// <summary>The annotation's place on the sheet: its box (mm), and its strokes, lines, vertices or marked quads as polylines (mm).</summary>
    static bool Geometry(PdfAnnotation a, PdfPageMap map, out JsonArray rect, out JsonArray paths)
    {
        rect = []; paths = [];
        static JsonArray Pt((double X, double Y) p) => new JsonArray(JsonValue.Create(p.X), JsonValue.Create(p.Y));
        var xs = new List<double>(); var ys = new List<double>();
        void Take((double X, double Y) p) { xs.Add(p.X); ys.Add(p.Y); }
        JsonArray Path(IEnumerable<(double, double)> pts) { var arr = new JsonArray(); foreach (var p in pts) { Take(p); arr.Add((JsonNode)Pt(p)); } return arr; }
        IEnumerable<(double, double)> Pairs(double[] flat) { for (var i = 0; i + 1 < flat.Length; i += 2) yield return map.ToSheet(flat[i], flat[i + 1]); }

        if (a.Rect is { } r) { foreach (var (x, y) in new[] { (r[0], r[1]), (r[2], r[1]), (r[2], r[3]), (r[0], r[3]) }) Take(map.ToSheet(x, y)); }
        switch (a.Subtype)
        {
            case "Ink": foreach (var stroke in a.InkList ?? []) paths.Add((JsonNode)Path(Pairs(stroke))); break;
            case "Line": if (a.Line != null) paths.Add((JsonNode)Path(Pairs(a.Line))); break;
            case "Polygon" or "PolyLine": if (a.Vertices != null) paths.Add((JsonNode)Path(Pairs(a.Vertices))); break;
            case "Highlight" or "Underline" or "StrikeOut" or "Squiggly" or "Redact":
                if (a.QuadPoints != null)
                    for (var i = 0; i + 7 < a.QuadPoints.Length; i += 8)
                    {
                        var q = a.QuadPoints;
                        // Upper-left, upper-right, lower-left, lower-right: a loop goes round.
                        paths.Add((JsonNode)Path([map.ToSheet(q[i], q[i + 1]), map.ToSheet(q[i + 2], q[i + 3]), map.ToSheet(q[i + 6], q[i + 7]), map.ToSheet(q[i + 4], q[i + 5])]));
                    }
                break;
        }
        if (xs.Count == 0) return false;
        rect = new JsonArray(JsonValue.Create(Math.Round(xs.Min(), 3)), JsonValue.Create(Math.Round(ys.Min(), 3)), JsonValue.Create(Math.Round(xs.Max(), 3)), JsonValue.Create(Math.Round(ys.Max(), 3)));
        return true;
    }

    // ------------------------------------------------------------ the entities

    public static JsonObject ToJson(Entity e)
    {
        var o = e.CloneAttrs();
        o["id"] = e.Id;
        o["name"] = e.Name;
        return o;
    }

    public static List<Entity> Of(IModelView m, string? sheetId = null) =>
        m.OfClass(Class).Where(e => sheetId == null || e.Str("sheetId") == sheetId).OrderBy(e => e.Str("date") ?? "").ToList();

    static string Title(JsonObject r)
    {
        var text = ((string?)r["text"] ?? "").Trim();
        var line = text.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "";
        if (line.Length > 60) line = line[..59] + "…";
        return line.Length > 0 ? line : (string?)r["subject"] is { Length: > 0 } s ? s : ((string?)r["kind"] ?? "remark") switch { "note" => "Note", "ink" => "Pen mark", "cloud" => "Cloud", var k => char.ToUpperInvariant(k[0]) + k[1..] };
    }

    /// <summary>
    /// Remarks into the project: one whose sheet, file and annotation are already there is updated (its geometry, text, replies and source; its
    /// status kept), the others are added (open, or resolved when the thread's review state says completed or accepted). One undo step.
    /// </summary>
    public static (int Added, int Updated, List<string> Ids) Import(ICommandContext ctx, JsonArray remarks)
    {
        var existing = Of(ctx.Model).ToDictionary(KeyOf, e => e);
        int added = 0, updated = 0;
        var ids = new List<string>();
        foreach (var node in remarks)
        {
            if (node is not JsonObject r) continue;
            var sheetId = (string?)r["sheetId"] ?? throw new CommandException("A remark names its sheet.");
            if (ctx.Model.Get(sheetId) is not { Class: Sheets.Class }) throw new CommandException("There is no such sheet.");
            var attrs = (JsonObject)r.DeepClone();
            attrs.Remove("id"); attrs.Remove("name");
            var state = (string?)attrs["state"];
            attrs.Remove("state");
            var key = KeyOf(attrs);
            if (existing.TryGetValue(key, out var old))
            {
                attrs["status"] = old.Str("status") ?? "open";
                ctx.Replace(old.With(name: Title(attrs), attrs: attrs));
                ids.Add(old.Id);
                updated++;
            }
            else
            {
                attrs["status"] = state is "Completed" or "Accepted" ? "resolved" : "open";
                var e = new Entity(ctx.NewId(), Class, Title(attrs), attrs);
                ctx.Add(e);
                existing[key] = e;
                ids.Add(e.Id);
                added++;
            }
        }
        return (added, updated, ids);
    }

    static string KeyOf(Entity e) => KeyOf(e.Attrs);
    static string KeyOf(JsonObject attrs)
    {
        var source = attrs["source"] as JsonObject;
        return $"{(string?)attrs["sheetId"]}|{((string?)source?["file"] ?? "").ToLowerInvariant()}|{(string?)source?["annot"]}";
    }

    static List<Entity> Chosen(ICommandContext ctx, JsonObject a)
    {
        var ids = a["ids"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToList() : (string?)a["id"] is { } one ? [one] : [];
        var list = ids.Select(id => ctx.Model.Get(id) is { Class: Class } e ? e : throw new CommandException("There is no such remark.")).ToList();
        if (list.Count == 0) throw new CommandException("Which remark?");
        return list;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Remark", [], "#c0392b", Kind: "resource"));

        // Remarks as cde.readPdf (the host) or cde.pdfRemarks read them: added or, read from the same file again, updated.
        r.Command("cde.importRemarks", "Read PDF remarks", (ctx, a) =>
        {
            var (added, updated, ids) = Import(ctx, a["remarks"] as JsonArray ?? []);
            return new JsonObject { ["added"] = added, ["updated"] = updated, ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()) };
        });

        r.Command("cde.setRemarkStatus", "Remark status", (ctx, a) =>
        {
            var status = (string?)a["status"] ?? "";
            if (!Statuses.Contains(status)) throw new CommandException("A remark is open, resolved or closed.");
            foreach (var e in Chosen(ctx, a))
            {
                var attrs = e.CloneAttrs();
                attrs["status"] = status;
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        r.Command("cde.deleteRemarks", "Delete remarks", (ctx, a) =>
        {
            foreach (var e in Chosen(ctx, a)) ctx.Remove(e.Id);
            return null;
        });

        r.Query("cde.remarks", (q, a) => new JsonArray(Of(q.Model, (string?)a["sheetId"]).Select(e => (JsonNode)ToJson(e)).ToArray()));

        // A PDF's remarks from its bytes (base64), for a browser that has no file to point at; the host's cde.readPdf also takes a path and the folder's manifests.
        r.Query("cde.pdfRemarks", (q, a) =>
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String((string?)a["data"] ?? ""); }
            catch (FormatException) { throw new CommandException("The file's bytes are not base64."); }
            if (bytes.Length == 0) throw new CommandException("The file is empty.");
            return FromPdf(q.Model, bytes, (string?)a["name"] ?? "file.pdf", null, (string?)a["sheetId"]);
        });
    }
}
