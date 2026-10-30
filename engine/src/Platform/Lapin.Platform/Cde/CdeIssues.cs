using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Cde;

/// <summary>One file an issue wrote: its name, what it is (pdf, ifc, dxf, csv, json), the sheet it prints, its SHA-256 and size, and the sheet's drawing hash at the time.</summary>
public sealed record IssuedFile(string Name, string Kind, string? SheetId, string Sha256, long Size, string? DrawingHash = null)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["name"] = Name, ["kind"] = Kind, ["sha256"] = Sha256, ["size"] = Size };
        if (SheetId != null) o["sheetId"] = SheetId;
        if (DrawingHash != null) o["drawingHash"] = DrawingHash;
        return o;
    }

    public static IssuedFile? From(JsonNode? n) => n is JsonObject o && (string?)o["name"] is { } name
        ? new IssuedFile(name, (string?)o["kind"] ?? "", (string?)o["sheetId"], (string?)o["sha256"] ?? "", (long)(Json.Number(o["size"]) ?? 0), (string?)o["drawingHash"])
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

    public sealed record Issue(string Id, int Number, string Name, string At, string Revision, string Status, string Container, string? Folder, string? Note, IReadOnlyList<IssuedFile> Files, int? Version)
    {
        public JsonObject ToJson() => new()
        {
            ["id"] = Id, ["number"] = Number, ["name"] = Name, ["at"] = At, ["revision"] = Revision, ["status"] = Status, ["container"] = Container,
            ["folder"] = Folder, ["note"] = Note, ["version"] = Version,
            ["files"] = new JsonArray(Files.Select(f => (JsonNode)f.ToJson()).ToArray()),
        };

        /// <summary>The sheets this issue printed.</summary>
        public IEnumerable<string> Sheets => Files.Where(f => f.SheetId != null).Select(f => f.SheetId!).Distinct();
    }

    public static Issue Read(Entity e) => new(e.Id, (int)e.Num("number"), e.Name, e.Str("at") ?? "", e.Str("revision") ?? "", e.Str("status") ?? "", e.Str("container") ?? "",
        e.Str("folder"), e.Str("note"), e.Attrs["files"] is JsonArray a ? a.Select(IssuedFile.From).OfType<IssuedFile>().ToList() : [], e.NumOrNull("version") is { } v ? (int)v : null);

    /// <summary>The project's issues, in order.</summary>
    public static List<Issue> All(IModelView m) => m.OfClass(Class).Select(Read).OrderBy(i => i.Number).ToList();

    /// <summary>The last issue a sheet went out in, with its file there; null when it never did.</summary>
    public static (Issue Issue, IssuedFile File)? LastOf(IModelView m, string sheetId) =>
        All(m).AsEnumerable().Reverse().SelectMany(i => i.Files.Where(f => f.SheetId == sheetId).Take(1).Select(f => (i, f))).Cast<(Issue, IssuedFile)?>().FirstOrDefault();

    /// <summary>The next issue number.</summary>
    public static int NextNumber(IModelView m) => All(m).Select(i => i.Number).DefaultIfEmpty(0).Max() + 1;

    /// <summary>The last revision issued in the project (any sheet), for the next issue to go on from.</summary>
    public static string? LastRevision(IModelView m) => All(m).LastOrDefault()?.Revision;

    /// <summary>
    /// Records an issue: the entity, and each printed sheet's title block given
    /// the issue's revision and status (its revision table lists the issue).
    /// </summary>
    public static Entity Record(ICommandContext ctx, string name, string revision, string status, string container, string? folder, string? note, IReadOnlyList<IssuedFile> files, int? version)
    {
        if (!CdeRevisions.IsRevision(revision)) throw new CommandException("A revision is P01, P02… (preliminary) or C01, C02… (contractual).");
        if (!CdeRevisions.IsStatus(status)) throw new CommandException("A status code is S0–S7, A1–A9, B1–B9 or CR.");
        if (!CdeRevisions.Containers.Contains(container)) throw new CommandException($"The container is one of {string.Join(", ", CdeRevisions.Containers)}.");
        var number = NextNumber(ctx.Model);
        var attrs = new JsonObject
        {
            ["number"] = number, ["at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), ["revision"] = revision, ["status"] = status, ["container"] = container,
            ["files"] = new JsonArray(files.Select(f => (JsonNode)f.ToJson()).ToArray()),
        };
        if (folder != null) attrs["folder"] = folder;
        if (note is { Length: > 0 }) attrs["note"] = note;
        if (version is { } v) attrs["version"] = v;
        var issue = new Entity(ctx.NewId(), Class, name is { Length: > 0 } ? name : $"Issue {number}", attrs);
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
        return issue;
    }

    /// <summary>The issues a sheet went out in, oldest first: for its revision table.</summary>
    public static List<Issue> OfSheet(IModelView m, string sheetId) => All(m).Where(i => i.Sheets.Contains(sheetId)).ToList();
}
