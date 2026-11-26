using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform.Cde.References;

namespace Lapin.Platform.Documents;

/// <summary>
/// What documents read of the reference documents (docs/plan-cde-references.md §6, decision 878, stage R5), registered by core:
/// <list type="bullet">
/// <item>for each type of the project's list, <c>ref.&lt;type&gt;.number</c>, <c>.date</c>, <c>.issuer</c>, <c>.validUntil</c>,
/// <c>.title</c>: the latest received document of that type still valid (the latest by its own date, then by when it was added;
/// superseded, withdrawn, requested and expired ones never), its dates in the document's format; and <c>ref.&lt;type&gt;.count</c>,
/// how many such documents there are (0 when none, so a count is never "to complete");</item>
/// <item>the collection <c>references</c> (<c>{{reference.*}}</c>): every document, in the folder tree's order then by date, each
/// with <c>id</c>, <c>type</c>, <c>typeName</c>, <c>title</c>, <c>issuer</c>, <c>number</c>, <c>date</c>, <c>validFrom</c>,
/// <c>validUntil</c>, <c>status</c> (the code a <c>where</c> compares: requested, received, expired, superseded, withdrawn),
/// <c>statusText</c> (in the document's language), <c>folder</c> (its id), <c>topFolder</c> (the top folder's id, for a where
/// over a whole branch), <c>folderPath</c>, <c>phase</c>, <c>discipline</c>, <c>files</c>, <c>fileNames</c>, <c>pages</c> (its PDFs'
/// pages and its images, one each; empty when no file's pages are known), and its type's extra fields by their keys;</item>
/// <item>the collection <c>checklist</c> (<c>{{check.*}}</c>): the items of the project's urbanism certificate (the latest received,
/// valid <c>cu</c>, as <c>ref.cu.*</c>) with <c>key</c>, <c>label</c>, <c>state</c> (missing, requested, received, expired),
/// <c>stateText</c>, and of the document answering it <c>document</c>, <c>title</c>, <c>number</c>, <c>date</c>, <c>issuer</c>,
/// <c>validUntil</c>.</item>
/// </list>
/// "Today" is this computer's day, as the expiry warnings read it.
/// </summary>
public sealed class ReferenceDocumentFields : IDocumentFields
{
    public const string Certificate = "cu";

    /// <summary>The language the names (types, folders, checklist items) are read in: Romanian for a Romanian document, else English.</summary>
    public static string NameLang(DocFormat f) => f.Lang.StartsWith("ro", StringComparison.OrdinalIgnoreCase) ? "ro" : "en";

    public static string Today() => ReferenceDates.Iso8601(DateOnly.FromDateTime(DateTime.Now));

    /// <summary>An ISO day as the document writes dates; the text as kept when it is not one.</summary>
    public static string? Date(string? iso, DocFormat f) =>
        iso == null ? null : ReferenceDates.Day(iso) is { } d ? DocNumbers.Date(d.ToDateTime(TimeOnly.MinValue), f) : iso;

    /// <summary>The documents of a type that are received and valid today, the latest first (by their own date, then when they were added).</summary>
    public static List<Entity> Valid(IModelView m, string type, string today) => References.All(m)
        .Where(e => e.Str("type") == type && References.StatusOf(e, today) == "received")
        .OrderByDescending(e => e.Str("date") ?? "", StringComparer.Ordinal).ThenByDescending(e => e.Str("addedAt") ?? "", StringComparer.Ordinal).ToList();

    /// <summary>The project's urbanism certificate: the latest received, valid document of type <c>cu</c>; null when it has none.</summary>
    public static Entity? CertificateOf(IModelView m, string today) => Valid(m, Certificate, today).FirstOrDefault();

    public IEnumerable<DocField> Fields(IQueryContext q, DocFormat f)
    {
        var m = q.Model;
        var today = Today();
        foreach (var t in References.Types(m).OfType<JsonObject>())
        {
            if ((string?)t["id"] is not { Length: > 0 } id) continue;
            var name = References.NameOf(t["name"], "en");
            var valid = Valid(m, id, today);
            var e = valid.FirstOrDefault();
            yield return new($"ref.{id}.number", $"{name}: number", e?.Str("number"));
            yield return new($"ref.{id}.date", $"{name}: date", Date(e?.Str("date"), f));
            yield return new($"ref.{id}.issuer", $"{name}: issuer", e?.Str("issuer"));
            yield return new($"ref.{id}.validUntil", $"{name}: valid until", Date(e?.Str("validUntil"), f));
            yield return new($"ref.{id}.title", $"{name}: title", e?.Name);
            yield return new($"ref.{id}.count", $"{name}: how many", valid.Count.ToString(CultureInfo.InvariantCulture));
        }
    }

    public IEnumerable<DocCollection> Collections =>
    [
        new("references", "Reference documents", "reference"),
        new("checklist", "Urbanism certificate's checklist", "check"),
    ];

    public IEnumerable<IReadOnlyDictionary<string, string>> Items(IQueryContext q, string collection, DocFormat f) => collection switch
    {
        "references" => ReferenceItems(q, f),
        "checklist" => ChecklistItems(q, f),
        _ => [],
    };

    static readonly Dictionary<string, (string Ro, string En)> StatusWords = new(StringComparer.Ordinal)
    {
        ["requested"] = ("solicitat", "requested"), ["received"] = ("primit", "received"), ["expired"] = ("expirat", "expired"),
        ["superseded"] = ("înlocuit", "superseded"), ["withdrawn"] = ("retras", "withdrawn"), ["missing"] = ("lipsă", "missing"),
    };

    /// <summary>A status or a checklist state in the document's language.</summary>
    public static string StatusText(string code, DocFormat f) =>
        StatusWords.TryGetValue(code, out var w) ? (NameLang(f) == "ro" ? w.Ro : w.En) : code;

    /// <summary>The documents in the folder tree's order (unfiled last), then by their date and title.</summary>
    public static List<Entity> Ordered(IModelView m, Dictionary<string, References.FolderInfo> folders)
    {
        var order = folders.Keys.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i, StringComparer.Ordinal);
        return References.All(m).OrderBy(e => order.TryGetValue(e.Str("folder") ?? "", out var i) ? i : int.MaxValue)
            .ThenBy(e => e.Str("date") ?? "￿", StringComparer.Ordinal).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>The top folder of a folder (itself when it is one).</summary>
    public static string? TopOf(Dictionary<string, References.FolderInfo> folders, string? id)
    {
        var at = id;
        for (var guard = 0; at != null && folders.TryGetValue(at, out var fi) && fi.Parent != null && guard < 32; guard++) at = fi.Parent;
        return at;
    }

    public static Dictionary<string, string> Item(IQueryContext q, Entity e, DocFormat f, string today, Dictionary<string, References.FolderInfo> folders, JsonArray types)
    {
        var lang = NameLang(f);
        var type = types.OfType<JsonObject>().FirstOrDefault(t => (string?)t["id"] == e.Str("type"));
        var files = References.Files(e);
        var status = References.StatusOf(e, today);
        var folder = e.Str("folder");
        var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = e.Id, ["type"] = e.Str("type") ?? "", ["typeName"] = type == null ? e.Str("type") ?? "" : References.NameOf(type["name"], lang),
            ["title"] = e.Name, ["issuer"] = e.Str("issuer") ?? "", ["number"] = e.Str("number") ?? "",
            ["date"] = Date(e.Str("date"), f) ?? "", ["validFrom"] = Date(e.Str("validFrom"), f) ?? "", ["validUntil"] = Date(e.Str("validUntil"), f) ?? "",
            ["status"] = status, ["statusText"] = StatusText(status, f),
            ["folder"] = folder ?? "", ["topFolder"] = TopOf(folders, folder) ?? "",
            ["folderPath"] = folder != null && folders.TryGetValue(folder, out var fi) ? fi.Path(lang) : "",
            ["phase"] = string.Join(", ", (e.Attrs["phase"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>()),
            ["discipline"] = e.Str("discipline") ?? "",
            ["files"] = files.Count.ToString(CultureInfo.InvariantCulture), ["fileNames"] = string.Join(", ", files.Select(x => x.Name)),
            ["pages"] = Pages(q, e) is { } p ? p.ToString(CultureInfo.InvariantCulture) : "",
        };
        // The type's extra fields (the project's own, decision 878 §3.1) by their keys, never over the fields above.
        foreach (var (k, v) in e.Attrs["fields"] as JsonObject ?? [])
            if (v != null) o.TryAdd(k, v is JsonValue sv && sv.TryGetValue<string>(out var s) ? s : v.ToJsonString());
        foreach (var fd in type?["fields"] as JsonArray ?? [])
            if ((string?)fd?["key"] is { Length: > 0 } key) o.TryAdd(key, "");
        return o;
    }

    static List<Dictionary<string, string>> ReferenceItems(IQueryContext q, DocFormat f)
    {
        var m = q.Model;
        var today = Today();
        var folders = References.Index(References.Folders(m));
        var types = References.Types(m);
        return Ordered(m, folders).Select(e => Item(q, e, f, today, folders, types)).ToList();
    }

    static List<Dictionary<string, string>> ChecklistItems(IQueryContext q, DocFormat f)
    {
        var m = q.Model;
        var today = Today();
        if (CertificateOf(m, today) is not { } cu) return [];
        var lang = NameLang(f);
        var list = References.Checklist(m, cu, today);
        var o = new List<Dictionary<string, string>>();
        foreach (var item in (list["items"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var state = (string?)item["state"] ?? "missing";
            var doc = (string?)item["document"] is { } id ? m.Get(id) : null;
            o.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = $"{cu.Id}:{(string?)item["key"]}", ["key"] = (string?)item["key"] ?? "", ["label"] = References.NameOf(item["label"], lang),
                ["state"] = state, ["stateText"] = StatusText(state, f),
                ["document"] = doc?.Id ?? "", ["title"] = doc?.Name ?? "", ["number"] = (string?)item["number"] ?? "", ["date"] = Date((string?)item["date"], f) ?? "",
                ["issuer"] = (string?)item["issuer"] ?? "", ["validUntil"] = Date((string?)item["validUntil"], f) ?? "",
            });
        }
        return o;
    }

    // ------------------------------------------------------------ pages

    static readonly ConcurrentDictionary<string, int?> _pages = new(StringComparer.Ordinal);

    /// <summary>
    /// A document's pages where they are known: each PDF's pages (read once per file, by its hash, with the PDF reader the remarks use)
    /// and one for each image; null when none of its files' pages is known (a DWG, an IFC, a file not held here).
    /// </summary>
    public static int? Pages(IQueryContext q, Entity e)
    {
        int? sum = null;
        foreach (var file in References.Files(e))
        {
            var n = PagesOf(q, e, file);
            if (n is { } k) sum = (sum ?? 0) + k;
        }
        return sum;
    }

    static int? PagesOf(IQueryContext q, Entity e, ReferenceFile file)
    {
        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp") return 1;
        if (!(ext == ".pdf" || file.Mime == "application/pdf")) return null;
        if (_pages.TryGetValue(file.Sha256, out var known)) return known;
        int? n;
        try
        {
            var (_, bytes) = References.Bytes(q, e, file.Name);
            n = Cde.PdfRemarks.PdfDocument.Load(bytes).Pages().Count;
        }
        // A file not held here is not counted yet (it may be later); a PDF the reader cannot read has no known pages.
        catch (CommandException) { return null; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { n = null; }
        _pages[file.Sha256] = n;
        return n;
    }
}

/// <summary>
/// The tables documents show of the reference documents (stage R5), beside the module's schedules:
/// <list type="bullet">
/// <item><c>references:permits</c>, <i>Permits and agreements</i>: the urbanism certificate's checklist (<see cref="ReferenceDocumentFields.CertificateOf"/>), each
/// item with the document that answers it (issuer, number, date, valid until) and its state; none (to complete) without a certificate;</item>
/// <item><c>references:documents</c>, <i>Reference documents</i>: the documents with their type, title, issuer, number, date,
/// validity, state and pages, filtered by what follows a <c>?</c>: <c>type=aviz,acord</c>, <c>folder=permits</c> (with its
/// subfolders), <c>status=received</c> (or <c>all</c>); without a status, the live ones (superseded and withdrawn left out).</item>
/// </list>
/// Its headings are in the document's language (Romanian or English).
/// </summary>
public static class ReferenceTables
{
    public const string Prefix = "references:";
    public const string Permits = "references:permits";
    public const string List = "references:documents";

    public static IEnumerable<DocTableChoice> Choices =>
    [
        new(Permits, "Permits and agreements"),
        new(List, "Reference documents"),
    ];

    /// <summary>A source's filter: what follows its <c>?</c>, as <c>key=value</c> pairs joined by <c>&amp;</c>.</summary>
    public static Dictionary<string, string> Filter(string source)
    {
        var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var at = source.IndexOf('?');
        if (at < 0) return o;
        foreach (var pair in source[(at + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            o[Uri.UnescapeDataString(pair[..eq]).Trim()] = Uri.UnescapeDataString(pair[(eq + 1)..]).Trim();
        }
        return o;
    }

    static bool Ro(DocFormat f) => ReferenceDocumentFields.NameLang(f) == "ro";

    public static ScheduleGrid? Table(IQueryContext q, string source, DocFormat f)
    {
        var at = source.IndexOf('?');
        var kind = at < 0 ? source : source[..at];
        return kind switch
        {
            Permits => PermitsTable(q, f),
            List => DocumentsTable(q, f, Filter(source)),
            _ => null,
        };
    }

    static ScheduleGrid? PermitsTable(IQueryContext q, DocFormat f)
    {
        var m = q.Model;
        var today = ReferenceDocumentFields.Today();
        if (ReferenceDocumentFields.CertificateOf(m, today) is not { } cu) return null;
        var ro = Ro(f);
        var lang = ReferenceDocumentFields.NameLang(f);
        var list = References.Checklist(m, cu, today);
        var rows = new List<IReadOnlyList<string>>();
        var n = 0;
        foreach (var item in (list["items"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var state = (string?)item["state"] ?? "missing";
            rows.Add([
                (++n).ToString(CultureInfo.InvariantCulture), References.NameOf(item["label"], lang), (string?)item["issuer"] ?? "", (string?)item["number"] ?? "",
                ReferenceDocumentFields.Date((string?)item["date"], f) ?? "", ReferenceDocumentFields.Date((string?)item["validUntil"], f) ?? "", ReferenceDocumentFields.StatusText(state, f),
            ]);
        }
        var title = ro ? $"Avize și acorduri cerute prin certificatul de urbanism{(cu.Str("number") is { } num ? " nr. " + num : "")}"
            : $"Permits and agreements the urbanism certificate{(cu.Str("number") is { } num2 ? " no. " + num2 : "")} requires";
        string[] cols = ro ? ["Nr.", "Aviz / acord", "Emitent", "Număr", "Data", "Valabil până la", "Stare"] : ["No.", "Permit or agreement", "Issuer", "Number", "Date", "Valid until", "State"];
        return new ScheduleGrid(title, cols, rows);
    }

    static ScheduleGrid DocumentsTable(IQueryContext q, DocFormat f, Dictionary<string, string> filter)
    {
        var m = q.Model;
        var today = ReferenceDocumentFields.Today();
        var folders = References.Index(References.Folders(m));
        var types = References.Types(m);
        var ro = Ro(f);
        var wantTypes = filter.TryGetValue("type", out var ts) && ts.Length > 0 ? ts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal) : null;
        HashSet<string>? inFolder = null;
        if (filter.TryGetValue("folder", out var folder) && folder.Length > 0)
        {
            inFolder = [folder];
            foreach (var fi in folders.Values)
                for (var p = fi.Parent; p != null; p = folders.GetValueOrDefault(p)?.Parent)
                    if (p == folder) { inFolder.Add(fi.Id); break; }
        }
        var status = filter.TryGetValue("status", out var st) && st.Length > 0 ? st : null;
        var rows = new List<IReadOnlyList<string>>();
        var n = 0;
        foreach (var e in ReferenceDocumentFields.Ordered(m, folders))
        {
            if (wantTypes != null && !wantTypes.Contains(e.Str("type") ?? "")) continue;
            if (inFolder != null && !inFolder.Contains(e.Str("folder") ?? "")) continue;
            var shown = References.StatusOf(e, today);
            if (status == null ? shown is "superseded" or "withdrawn" : status != "all" && !status.Split(',').Select(x => x.Trim()).Contains(shown)) continue;
            var item = ReferenceDocumentFields.Item(q, e, f, today, folders, types);
            rows.Add([(++n).ToString(CultureInfo.InvariantCulture), item["typeName"], item["title"], item["issuer"], item["number"], item["date"], item["validUntil"], item["statusText"], item["pages"]]);
        }
        string[] cols = ro ? ["Nr.", "Tip", "Titlu", "Emitent", "Număr", "Data", "Valabil până la", "Stare", "Pagini"] : ["No.", "Type", "Title", "Issuer", "Number", "Date", "Valid until", "State", "Pages"];
        return new ScheduleGrid(ro ? "Documente de referință" : "Reference documents", cols, rows);
    }
}

/// <summary>
/// The tables a document can show (<see cref="IDocumentTables"/>): the module's (architecture's schedules, else each module's
/// <see cref="IScheduleSource"/>) and the reference documents' (<see cref="ReferenceTables"/>), which every product has.
/// </summary>
public static class DocumentTables
{
    public static IDocumentTables Module(IQueryContext q) => q.Services.Get<IDocumentTables>() ?? new ScheduleSourceTables();

    public static IEnumerable<DocTableChoice> Choices(IQueryContext q) => Module(q).Choices(q).Concat(ReferenceTables.Choices);

    /// <summary>The table for a source in a document's format; null when there is none.</summary>
    public static ScheduleGrid? Table(IQueryContext q, string source, DocFormat f) =>
        source.StartsWith(ReferenceTables.Prefix, StringComparison.Ordinal) ? ReferenceTables.Table(q, source, f) : Module(q).Table(q, source);
}
