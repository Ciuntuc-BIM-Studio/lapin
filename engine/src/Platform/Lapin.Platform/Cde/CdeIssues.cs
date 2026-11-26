using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Cde;

/// <summary>
/// One file an issue made: its name, what it is (pdf, ifc, dxf, csv, json), the sheet it prints, its SHA-256 and size, the sheet's drawing hash at
/// the time, and whether its bytes are kept in the project's file (<see cref="Stored"/>: under <c>issues/blobs/</c> by its hash; the IFC only on
/// request, and none once the user deletes an old issue's stored files, its name and hash staying in the record); a written document's (kind
/// <c>document</c>: its PDF, or its DOCX) names the document (<see cref="DocumentId"/>).
/// </summary>
public sealed record IssuedFile(string Name, string Kind, string? SheetId, string Sha256, long Size, string? DrawingHash = null, bool Stored = false, string? DocumentId = null, string? ReferenceId = null)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["name"] = Name, ["kind"] = Kind, ["sha256"] = Sha256, ["size"] = Size };
        if (SheetId != null) o["sheetId"] = SheetId;
        if (DrawingHash != null) o["drawingHash"] = DrawingHash;
        if (Stored) o["stored"] = true;
        if (DocumentId != null) o["documentId"] = DocumentId;
        if (ReferenceId != null) o["referenceId"] = ReferenceId;
        return o;
    }

    public static IssuedFile? From(JsonNode? n) => n is JsonObject o && (string?)o["name"] is { } name
        ? new IssuedFile(name, (string?)o["kind"] ?? "", (string?)o["sheetId"], (string?)o["sha256"] ?? "", (long)(Json.Number(o["size"]) ?? 0), (string?)o["drawingHash"],
            o["stored"] is JsonValue sv && sv.TryGetValue<bool>(out var stored) && stored, (string?)o["documentId"], (string?)o["referenceId"])
        : null;
}

/// <summary>
/// An issue as the project records it (docs/plan-cde.md): a <c>LapinIssue</c>
/// entity (a resource, never exported to IFC) with when, its revision and
/// status, the container it went into, the folder at the time, its note and
/// the files it wrote with their hashes. Numbered in order of making. The
/// sheets' revision tables and the "changed since its last issue" check read
/// these.
/// </summary>
public static class CdeIssues
{
    public const string Class = "LapinIssue";

    /// <summary>
    /// One issue record: a revision of issue <see cref="Number"/>. A new revision of an earlier issue (ISO 19650: the same documents issued
    /// again, P01 → P02) keeps its number and names the first record of the series (<see cref="Of"/>, kept as <c>issueId</c>), whose id
    /// is the issue's GlobalId on a CDE server (the issue tag's <c>issue</c>, a submission's <c>issueId</c>).
    /// </summary>
    public sealed record Issue(string Id, int Number, string Name, string At, string Revision, string Status, string Container, string? Folder, string? Note, IReadOnlyList<IssuedFile> Files, int? Version, string? Of = null)
    {
        /// <summary>The issue this record is a revision of: its first record's id (this one's for a first issue).</summary>
        public string IssueId => Of ?? Id;

        public JsonObject ToJson() => new()
        {
            ["id"] = Id, ["number"] = Number, ["name"] = Name, ["at"] = At, ["revision"] = Revision, ["status"] = Status, ["container"] = Container,
            ["folder"] = Folder, ["note"] = Note, ["version"] = Version, ["issueId"] = IssueId,
            ["files"] = new JsonArray(Files.Select(f => (JsonNode)f.ToJson()).ToArray()),
        };

        /// <summary>The sheets this issue printed.</summary>
        public IEnumerable<string> Sheets => Files.Where(f => f.SheetId != null).Select(f => f.SheetId!).Distinct();

        /// <summary>
        /// What the issue sent, as a set to compare issues by: its sheets, its written documents and the model (the set PDF, the DXFs and
        /// the transmittal follow from those; the reference documents sent with it go beside, never part of the set).
        /// </summary>
        public SortedSet<string> DocumentKeys => new(Files.Select(f => f.SheetId is { } s ? $"sheet:{s}" : f.DocumentId is { } d ? $"document:{d}" : f.Kind == "ifc" ? "model" : null)
            .OfType<string>(), StringComparer.Ordinal);
    }

    public static Issue Read(Entity e) => new(e.Id, (int)e.Num("number"), e.Name, e.Str("at") ?? "", e.Str("revision") ?? "", e.Str("status") ?? "", e.Str("container") ?? "",
        e.Str("folder"), e.Str("note"), e.Attrs["files"] is JsonArray a ? a.Select(IssuedFile.From).OfType<IssuedFile>().ToList() : [], e.NumOrNull("version") is { } v ? (int)v : null,
        e.Str("issueId") is { Length: > 0 } of && of != e.Id ? of : null);

    /// <summary>The project's issues in the order they were made (a new revision of an earlier issue comes after the issues made between).</summary>
    public static List<Issue> All(IModelView m) => m.OfClass(Class).Select(Read).OrderBy(i => i.At, StringComparer.Ordinal).ThenBy(i => i.Number).ToList();

    /// <summary>An issue's revisions so far: its number, its GlobalId (the first record's id), its name, and its records oldest first.</summary>
    public sealed record Series(int Number, string IssueId, string Name, IReadOnlyList<Issue> Revisions)
    {
        public Issue Last => Revisions[^1];

        /// <summary>The latest revision issued (P before C, then by number).</summary>
        public string LastRevision => Revisions.Select(r => r.Revision).Aggregate((best, r) => CdeRevisions.Compare(r, best) > 0 ? r : best);

        public JsonObject ToJson() => new()
        {
            ["number"] = Number, ["issueId"] = IssueId, ["name"] = Name, ["lastRevision"] = LastRevision, ["lastId"] = Last.Id, ["container"] = Last.Container, ["status"] = Last.Status,
            ["revisions"] = new JsonArray([.. Revisions.Select(r => (JsonNode)r.Revision)]),
            ["documents"] = new JsonArray([.. Last.DocumentKeys.Select(k => (JsonNode)k)]),
        };
    }

    /// <summary>The project's issues as series (one per issue number), in the order their first revision was made.</summary>
    public static List<Series> AllSeries(IModelView m) => All(m).GroupBy(i => i.Number)
        .Select(g => { var list = g.ToList(); return new Series(g.Key, list[0].IssueId, list[0].Name, list); }).ToList();

    /// <summary>The series an issue record (or its GlobalId, or its number) belongs to; null when none.</summary>
    public static Series? SeriesOf(IModelView m, string idOrNumber) => AllSeries(m).FirstOrDefault(s =>
        s.IssueId == idOrNumber || s.Revisions.Any(r => r.Id == idOrNumber) || s.Number.ToString(CultureInfo.InvariantCulture) == idOrNumber);

    /// <summary>
    /// The issue whose last revision sent exactly these documents (sheet ids, written document ids, and the model or not): the one a new issue
    /// of them would be a revision of. The latest such issue when several; null when none.
    /// </summary>
    public static Series? Matching(IModelView m, IEnumerable<string> sheets, IEnumerable<string> documents, bool model)
    {
        var keys = new SortedSet<string>(sheets.Select(s => $"sheet:{s}").Concat(documents.Select(d => $"document:{d}")), StringComparer.Ordinal);
        if (model) keys.Add("model");
        if (keys.Count == 0) return null;
        return AllSeries(m).LastOrDefault(s => s.Last.DocumentKeys.SetEquals(keys));
    }

    /// <summary>The last issue a sheet went out in, with its file there; null when it never did.</summary>
    public static (Issue Issue, IssuedFile File)? LastOf(IModelView m, string sheetId) =>
        All(m).AsEnumerable().Reverse().SelectMany(i => i.Files.Where(f => f.SheetId == sheetId).Take(1).Select(f => (i, f))).Cast<(Issue, IssuedFile)?>().FirstOrDefault();

    /// <summary>The next issue number.</summary>
    public static int NextNumber(IModelView m) => All(m).Select(i => i.Number).DefaultIfEmpty(0).Max() + 1;

    /// <summary>The highest revision issued in the project (any sheet; P before C, then by number), for the next issue to go on from.</summary>
    public static string? LastRevision(IModelView m) =>
        All(m).Select(i => i.Revision).Where(r => r.Length > 0).Aggregate((string?)null, (best, r) => best == null || CdeRevisions.Compare(r, best) > 0 ? r : best);

    /// <summary>
    /// Records an issue: the entity, and each printed sheet's title block given
    /// the issue's revision and status (its revision table lists the issue).
    /// </summary>
    /// <param name="of">A new revision of this earlier issue (any of its records' ids, or its GlobalId): its number is kept, and the revision must come after its last.</param>
    public static Entity Record(ICommandContext ctx, string name, string revision, string status, string container, string? folder, string? note, IReadOnlyList<IssuedFile> files, int? version, string? of = null)
    {
        if (!CdeRevisions.IsRevision(revision)) throw new CommandException("A revision is P01, P02… (preliminary) or C01, C02… (contractual).");
        if (!CdeRevisions.IsStatus(status)) throw new CommandException("A status code is S0–S7, A1–A9, B1–B9 or CR.");
        if (!CdeRevisions.Containers.Contains(container)) throw new CommandException($"The container is one of {string.Join(", ", CdeRevisions.Containers)}.");
        var series = of is { Length: > 0 } ? CheckRevision(ctx.Model, of, revision) : null;
        var number = series?.Number ?? NextNumber(ctx.Model);
        var attrs = new JsonObject
        {
            ["number"] = number, ["at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), ["revision"] = revision, ["status"] = status, ["container"] = container,
            ["files"] = new JsonArray(files.Select(f => (JsonNode)f.ToJson()).ToArray()),
        };
        if (folder != null) attrs["folder"] = folder;
        if (note is { Length: > 0 }) attrs["note"] = note;
        if (version is { } v) attrs["version"] = v;
        if (series != null) attrs["issueId"] = series.IssueId;
        var issue = new Entity(ctx.NewId(), Class, name is { Length: > 0 } ? name : series?.Name ?? $"Issue {number}", attrs);
        ctx.Add(issue);
        foreach (var sheetId in files.Where(f => f.SheetId != null).Select(f => f.SheetId!).Distinct())
        {
            if (ctx.Model.Get(sheetId) is not { Class: Sheets.Class } sheet) continue;
            var sa = sheet.CloneAttrs();
            var tb = sa["titleBlock"] as JsonObject ?? new JsonObject();
            tb["revision"] = revision;
            tb["status"] = status;
            sa["titleBlock"] = tb;
            ctx.Replace(sheet.With(attrs: sa));
        }
        // Each written document issued takes the issue's revision and status (docs/plan-documents.md).
        foreach (var docId in files.Where(f => f.DocumentId != null).Select(f => f.DocumentId!).Distinct())
        {
            if (ctx.Model.Get(docId) is not { Class: Documents.Documents.Class } doc) continue;
            var da = doc.CloneAttrs();
            da["revision"] = revision;
            da["status"] = status;
            ctx.Replace(doc.With(attrs: da));
        }
        return issue;
    }

    /// <summary>The issue a new revision is of, checked: it exists, and <paramref name="revision"/> comes after its last revision.</summary>
    public static Series CheckRevision(IModelView m, string of, string revision)
    {
        var series = SeriesOf(m, of) ?? throw new CommandException("There is no such issue to issue a new revision of.");
        if (CdeRevisions.Compare(revision, series.LastRevision) <= 0)
            throw new CommandException($"Issue {series.Number} is at {series.LastRevision} already: its new revision comes after it ({CdeRevisions.Next(series.LastRevision)}, or C01 once contractual).");
        return series;
    }

    /// <summary>Every hash the project's issues keep in its file: what a save writes under <c>issues/blobs/</c>, once each.</summary>
    public static HashSet<string> StoredHashes(IModelView m) =>
        All(m).SelectMany(i => i.Files).Where(f => f.Stored && f.Sha256.Length == 64).Select(f => f.Sha256).ToHashSet();

    /// <summary>The issues and their files as <c>issues/index.json</c> lists them, for whoever opens the package without Lapin (Lapin reads the records).</summary>
    public static JsonObject Index(IModelView m) => new()
    {
        ["format"] = "lapin-issues/1",
        ["blobs"] = "issues/blobs/<sha256>",
        ["issues"] = new JsonArray(All(m).Select(i => (JsonNode)new JsonObject
        {
            ["number"] = i.Number, ["name"] = i.Name, ["at"] = i.At, ["revision"] = i.Revision, ["status"] = i.Status, ["container"] = i.Container, ["version"] = i.Version,
            ["files"] = new JsonArray(i.Files.Select(f => (JsonNode)new JsonObject { ["name"] = f.Name, ["kind"] = f.Kind, ["sha256"] = f.Sha256, ["size"] = f.Size, ["stored"] = f.Stored }).ToArray()),
        }).ToArray()),
    };

    /// <summary>
    /// An issue's stored files let go (all, or those of the kinds given): the record keeps their names and hashes, the next save leaves their bytes
    /// out of the file unless another issue keeps the same bytes. One undo step.
    /// </summary>
    public static int DropStored(ICommandContext ctx, string id, IReadOnlyCollection<string>? kinds)
    {
        var e = ctx.Model.Get(id) is { Class: Class } i ? i : throw new CommandException("There is no such issue.");
        var files = Read(e).Files;
        var dropped = 0;
        var kept = files.Select(f =>
        {
            if (!f.Stored || (kinds != null && !kinds.Contains(f.Kind))) return f;
            dropped++;
            return f with { Stored = false };
        }).ToList();
        if (dropped == 0) return 0;
        var attrs = e.CloneAttrs();
        attrs["files"] = new JsonArray(kept.Select(f => (JsonNode)f.ToJson()).ToArray());
        ctx.Replace(e.With(attrs: attrs));
        return dropped;
    }

    /// <summary>The issues a sheet went out in, oldest first: for its revision table.</summary>
    public static List<Issue> OfSheet(IModelView m, string sheetId) => All(m).Where(i => i.Sheets.Contains(sheetId)).ToList();
}
