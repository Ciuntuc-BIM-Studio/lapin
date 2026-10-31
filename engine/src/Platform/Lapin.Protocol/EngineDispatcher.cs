using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>A unsaved session the user can bring back, as the host's journal keeps it.</summary>
public sealed record RecoverableWork(string File, string? Path, DateTimeOffset At, int Edits, string? LastEdit);

/// <summary>The host's recovery journal, as the protocol offers it (recovery.list, restore, discard).</summary>
public interface IRecoveryJournal
{
    IReadOnlyList<RecoverableWork> List();
    Diff Restore(string file);
    void Discard(string file);
}

/// <summary>What only a host can do for the help menu: send a report, read and answer its replies, open one of its links.</summary>
public interface ISupportDesk
{
    JsonObject Report(JsonObject p);
    /// <summary>One of the replies' calls (<see cref="SupportMessages"/>): the server's answer, or the body for the page to post.</summary>
    JsonObject Messages(JsonObject p);
    void Open(string? link);
}

/// <summary>
/// Opening and saving projects: the host's file access by path, given to the
/// dispatcher so the protocol stays testable; and by bytes, for a host without
/// files of its own (the browser: the user picks a file, keeps a download).
/// It holds the open project's versions (docs/versions.md): read with the
/// file, carried into every save, and changed by the version calls, each of
/// which writes the file (a version lives in the project's file). And it
/// holds the files the project's issues keep (docs/plan-cde.md): made by an
/// issue, in memory until the next save, then in the file under
/// <c>issues/blobs/</c>.
/// </summary>
public sealed class ProjectFiles(Func<Session, string, Diff> open, Action<Session, string, PackageVersions, IssueFiles?> write)
{
    PackageVersions _versions = PackageVersions.None;
    /// <summary>The session path the versions belong to: another path (a recovered project's) has them read from its file.</summary>
    string? _versionsOf;
    PackageIssues _issues = PackageIssues.Empty();
    /// <summary>The session path the issues' files were read from (null: a project not saved yet, its issues' files in memory).</summary>
    string? _issuesOf;

    /// <summary>
    /// A project from its bytes; <paramref name="name"/> is the file's name, its
    /// title. An old project (format 2) is rebuilt as on the desktop and opens
    /// unsaved, what it left out in <see cref="LastNotes"/>.
    /// </summary>
    public Diff OpenBytes(Session session, string name, byte[] bytes)
    {
        LastNotes = [];
        using var input = new MemoryStream(bytes, writable: false);
        if (Lapin.Package.LegacyPackage.Is(input))
        {
            var imported = LegacyImport.Run(session, Lapin.Package.LegacyPackage.Read(input));
            LastNotes = imported.Notes;
            return imported.Diff;
        }
        var content = LapinPackage.Read(input);
        var diff = session.Load(content.Entities, content.Relations, name, content.Classes);
        Keep(name, PackageVersions.Of(bytes));
        KeepIssues(name, PackageIssues.Of(bytes));
        return diff;
    }

    /// <summary>The project as a package's bytes; the session counts as saved.</summary>
    public (byte[] Bytes, Diff Diff) SaveBytes(Session session, string name)
    {
        var versions = Versions(session);
        var issues = IssueFilesOf(session);
        using var ms = new MemoryStream();
        ProjectPackage.Write(session, ms, versions, issues);
        var bytes = ms.ToArray();
        var diff = session.Saved(name);
        Keep(name, versions.From(bytes));
        KeepIssues(name, issues.Store.From(bytes, issues.Written));
        return (bytes, diff);
    }

    /// <summary>What the last opening left out (an old project's parts that do not come over), for the interface to tell.</summary>
    public List<string> LastNotes { get; set; } = [];

    public Diff Open(Session session, string path)
    {
        var diff = open(session, path);
        Keep(path, PackageVersions.Of(path));
        KeepIssues(path, PackageIssues.Of(path));
        return diff;
    }

    public Diff Save(Session session, string? path)
    {
        var target = path ?? session.Path ?? throw new CommandException("Where to save: the project has no file yet.");
        var versions = Versions(session);
        var issues = IssueFilesOf(session);
        write(session, target, versions, issues);
        var diff = session.Saved(target);
        Keep(session.Path ?? target, versions.From(session.Path ?? target));
        KeepIssues(session.Path ?? target, issues.Store.From(session.Path ?? target, issues.Written));
        return diff;
    }

    /// <summary>A new project: no issue files of the last one carried over.</summary>
    public void Reset()
    {
        _issues = PackageIssues.Empty();
        _issuesOf = null;
    }

    // ------------------------------------------------------------ issues' files

    /// <summary>
    /// The files the open project's issues keep: those read with its file, plus those made since (in memory until the next save); read again
    /// from the file when the project's path is not the one they came with (a project recovered), empty for a new project.
    /// </summary>
    public PackageIssues Issues(Session session)
    {
        if (session.Path is { } path) { if (_issuesOf != path) KeepIssues(path, PackageIssues.Of(path)); }
        else if (_issuesOf != null) Reset();
        return _issues;
    }

    void KeepIssues(string? path, PackageIssues issues)
    {
        _issues = issues;
        _issuesOf = path;
    }

    /// <summary>What a save writes of the issues: the bytes every record keeps, and the readable index.</summary>
    public IssueFiles IssueFilesOf(Session session)
    {
        var store = Issues(session);
        var keep = Lapin.Platform.Cde.CdeIssues.StoredHashes(session.Model);
        var index = keep.Count > 0
            ? System.Text.Encoding.UTF8.GetBytes(Lapin.Platform.Cde.CdeIssues.Index(session.Model).ToJsonString(new JsonSerializerOptions { WriteIndented = true }))
            : null;
        return new IssueFiles(store, keep, index);
    }

    // ------------------------------------------------------------ versions

    /// <summary>The open project's versions: those kept with its path, else read from its file (a project recovered, or opened by another way).</summary>
    public PackageVersions Versions(Session session)
    {
        if (session.Path is not { } path) return PackageVersions.None;
        if (_versionsOf != path) Keep(path, PackageVersions.Of(path));
        return _versions;
    }

    void Keep(string path, PackageVersions versions)
    {
        _versions = versions;
        _versionsOf = path;
    }

    public JsonArray VersionList(Session session) => new(Versions(session).Versions.Select(v => (JsonNode)ToJson(v)).ToArray());

    static JsonObject ToJson(PackageVersion v) => new()
    {
        ["number"] = v.Number, ["name"] = v.Name, ["note"] = v.Note, ["at"] = v.At, ["updatedAt"] = v.UpdatedAt,
        ["entities"] = v.Entities, ["size"] = v.Size, ["parts"] = v.Manifest.Parts.Count, ["app"] = v.Manifest.App,
        ["revisions"] = new JsonArray(v.Revisions.Select(r => (JsonNode)new JsonObject { ["at"] = r.At, ["note"] = r.Note, ["entities"] = r.Entities, ["size"] = r.Manifest.Parts.Sum(p => p.Size) }).ToArray()),
    };

    /// <summary>The project as it is now, saved as a new version of its file.</summary>
    public (JsonObject Result, Diff Diff) SaveVersion(Session session, string? name, string? note, bool asBytes)
    {
        var build = ProjectPackage.Build(session);
        var versions = Versions(session).Add(build.Manifest, session.Model.Entities.Count(), name, note, out var version);
        return Commit(session, build, versions, asBytes, ToJson(version));
    }

    /// <summary>A version renamed or annotated; <paramref name="replace"/>: the project as it is now takes its place, what it held kept as a revision.</summary>
    public (JsonObject Result, Diff Diff) UpdateVersion(Session session, int number, string? name, string? note, bool replace, bool asBytes)
    {
        var build = ProjectPackage.Build(session);
        var versions = Versions(session).Update(number, name, note, replace ? (build.Manifest, session.Model.Entities.Count()) : null, out var version);
        return Commit(session, build, versions, asBytes, ToJson(version));
    }

    public (JsonObject Result, Diff Diff) DeleteVersion(Session session, int number, bool asBytes) =>
        Commit(session, ProjectPackage.Build(session), Versions(session).Remove(number), asBytes, new JsonObject { ["removed"] = number });

    /// <summary>The file written with the project as it is and the versions given; as bytes for a host without files (the browser).</summary>
    (JsonObject Result, Diff Diff) Commit(Session session, PackageBuild build, PackageVersions versions, bool asBytes, JsonObject result)
    {
        var issues = IssueFilesOf(session);
        if (asBytes)
        {
            var name = session.Path ?? "project.lapin";
            using var ms = new MemoryStream();
            LapinPackage.Write(ms, build, versions, issues);
            var bytes = ms.ToArray();
            var diff = session.Saved(name);
            Keep(name, versions.From(bytes));
            KeepIssues(name, issues.Store.From(bytes, issues.Written));
            result["name"] = name;
            result["data"] = Convert.ToBase64String(bytes);
            return (result, diff);
        }
        var path = session.Path ?? throw new CommandException("Save the project first: its versions live in its file.");
        LapinPackage.Write(path, build, versions, issues);
        var saved = session.Saved(path);
        Keep(path, versions.From(path));
        KeepIssues(path, issues.Store.From(path, issues.Written));
        result["path"] = path;
        return (result, saved);
    }

    static PackageManifest ManifestOf(PackageVersion version, int? revision) =>
        revision is { } i ? (i >= 0 && i < version.Revisions.Count ? version.Revisions[i].Manifest : throw new CommandException($"Version {version.Number} has no revision {i}.")) : version.Manifest;

    /// <summary>The project brought back to a version (or one of its revisions), as one undo step.</summary>
    public Diff RestoreVersion(Session session, int number, int? revision)
    {
        var versions = Versions(session);
        var version = versions.Require(number);
        var content = versions.Content(ManifestOf(version, revision));
        session.AdoptClasses(content.Classes);
        var (entities, relations) = session.Upgraded(content.Entities, content.Relations);
        return session.Edit($"Restore version {number}", edit => ModelCompare.Apply(edit, entities, relations));
    }

    /// <summary>A version as a package of its own, to open elsewhere.</summary>
    public (string Name, byte[] Bytes) ExportVersion(Session session, int number, int? revision)
    {
        var versions = Versions(session);
        var version = versions.Require(number);
        var title = session.Path is { } p ? Path.GetFileNameWithoutExtension(p) : "project";
        var name = $"{title} v{number}{(revision is { } i ? $".{i + 1}" : "")}.lapin";
        return (name, versions.Standalone(ManifestOf(version, revision)));
    }

    /// <summary>Two states compared, element by element and part by part: each a version's number, or "current" for the project as it is now.</summary>
    public JsonObject CompareVersions(Session session, JsonNode? a, JsonNode? b)
    {
        var versions = Versions(session);
        (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations, PackageManifest Manifest, string Label) Side(JsonNode? n)
        {
            if (n is JsonValue v && v.TryGetValue<int>(out var number))
            {
                var version = versions.Require(number);
                var content = versions.Content(version.Manifest);
                return (content.Entities, content.Relations, version.Manifest, version.Name);
            }
            var build = ProjectPackage.Build(session);
            return (session.Model.Entities.ToList(), session.Model.Relations.ToList(), build.Manifest, "current");
        }
        var x = Side(a);
        var y = Side(b);
        var diff = ModelCompare.Compare(x.Entities, x.Relations, y.Entities, y.Relations);
        var parts = ModelCompare.Parts(x.Manifest, y.Manifest);
        static JsonArray Changes(IEnumerable<EntityChange> list) => new(list.Select(c => (JsonNode)new JsonObject
        {
            ["id"] = c.Id, ["class"] = c.Class, ["name"] = c.Name,
            ["fields"] = c.Fields.Count == 0 ? null : new JsonArray(c.Fields.Select(f => (JsonNode)f).ToArray()),
        }).ToArray());
        return new JsonObject
        {
            ["a"] = x.Label, ["b"] = y.Label, ["same"] = !diff.Any,
            ["entities"] = new JsonObject { ["added"] = Changes(diff.Added), ["removed"] = Changes(diff.Removed), ["changed"] = Changes(diff.Changed) },
            ["relations"] = new JsonObject { ["added"] = diff.RelationsAdded, ["removed"] = diff.RelationsRemoved, ["changed"] = diff.RelationsChanged },
            ["parts"] = new JsonObject
            {
                ["added"] = new JsonArray(parts.Added.Select(s => (JsonNode)s).ToArray()), ["removed"] = new JsonArray(parts.Removed.Select(s => (JsonNode)s).ToArray()),
                ["changed"] = new JsonArray(parts.Changed.Select(s => (JsonNode)s).ToArray()), ["same"] = parts.Same,
                ["areas"] = new JsonObject(parts.Areas.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
            },
        };
    }
}

/// <summary>
/// Who a call is from: the computer's own interfaces (the window, its
/// detached views, the MCP bridge), which may do anything, or a guest of a
/// shared session (docs/plan-multiplayer.md), an editor or a viewer, who may
/// only do what <see cref="EngineDispatcher.AllowedRemote"/> lets through.
/// </summary>
public sealed record Caller(string Author, string Role)
{
    public static readonly Caller Local = new(History.Me, "owner");
    public bool Remote => Author != History.Me;
}

/// <summary>A reply to one call: its id, and its result or why it failed.</summary>
public sealed record Reply(JsonNode? Id, object? Result, string? Error = null);

/// <summary>What the engine pushes after anything that changed the document.</summary>
public sealed record DiffEvent(string Event, Diff Data);

/// <summary>
/// The engine protocol without its transport. The interface sends JSON text:
///   → {"id":1,"method":"doc.execute","params":{"command":"architecture.addWall","args":{…}}}
/// and gets binary frames back (<see cref="BinaryFrame"/>: a JSON header,
/// meshes as raw arrays):
///   ← {"id":1,"result":…}            or  {"id":1,"error":"…"}
///   ← {"event":"diff","data":Diff}   after anything that changed the document
/// One call at a time: the session is single-threaded by design, and the
/// host serializes the calls. The host sends the diff (to every interface it
/// serves) before the reply.
/// </summary>
public sealed class EngineDispatcher(Session session, ProjectFiles files, IRecoveryJournal? recovery = null, ISupportDesk? support = null)
{
    public Session Session => session;

    /// <summary>Frames written by the generated contracts alone, as a trimmed engine writes them (for tests).</summary>
    public bool ContractsOnly { get; init; }

    /// <summary>Where a long call's progress goes (the host sends it to the caller); null: nobody listens.</summary>
    public Action<JsonObject>? Progress { get; set; }

    readonly object _progressLock = new();
    string? _progressStage;
    long _progressAt;

    /// <summary>A stage's progress, at most ten times a second (its end always).</summary>
    void ReportProgress(string stage, int done, int total)
    {
        var sink = Progress;
        if (sink == null) return;
        lock (_progressLock)
        {
            var now = Environment.TickCount64;
            if (stage == _progressStage && done < total && now - _progressAt < 100) return;
            _progressStage = stage; _progressAt = now;
        }
        sink(new JsonObject { ["stage"] = stage, ["done"] = done, ["total"] = total });
    }

    /// <summary>The last file surveyed (project.ifcSurvey), read and indexed, for the import that follows to take as it is.</summary>
    (string Key, byte[] Bytes, StepFile File, string Hash)? _surveyed;
    (string Key, Lapin.Mesh.Fragments.FragModel Model)? _fragSurveyed;

    /// <summary>An IFC file as a call gives it: by path (the desktop) or as base64 in data (the browser); its name and a key that tells the same file again.</summary>
    (byte[] Bytes, string Name, string? Path, string Key) IfcBytes(JsonObject p)
    {
        if ((string?)p["data"] is { } data)
        {
            var bytes = Convert.FromBase64String(data);
            var name = (string?)p["name"] ?? "file.ifc";
            return (bytes, name, null, $"data:{name}:{bytes.Length}");
        }
        var path = (string?)p["path"] ?? throw new CommandException("The file's path is missing.");
        var info = new FileInfo(path);
        if (!info.Exists) throw new CommandException($"There is no file at {path}.");
        return (File.ReadAllBytes(path), info.Name, path, $"{path}:{info.Length}:{info.LastWriteTimeUtc.Ticks}");
    }

    static IReadOnlySet<string>? Chosen(JsonNode? n) => n is JsonArray a ? a.Select(x => (string?)x ?? "").ToHashSet() : null;

    /// <summary>
    /// An import's rules read into its options: dedupe, flat, far (metres, or absent), duplicates, pseudo, simplify
    /// (metres), numbers, and the sets left out by name; each absent one as the options have it.
    /// </summary>
    public static IfcImportOptions Rules(IfcImportOptions o, JsonObject? r)
    {
        if (r == null) return o;
        static bool B(JsonNode? n, bool d) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : d;
        return o with
        {
            Dedupe = B(r["dedupe"], o.Dedupe),
            Flat = B(r["flat"], o.Flat),
            FarAway = Json.Number(r["far"]) is { } far && far > 0 ? far : null,
            Duplicates = B(r["duplicates"], o.Duplicates),
            Pseudo = B(r["pseudo"], o.Pseudo),
            Simplify = Math.Clamp(Json.Number(r["simplify"]) ?? 0, 0, 0.1),
            Numbers = B(r["numbers"], o.Numbers),
            SkipSets = r["skipSets"] is JsonArray skip ? skip.Select(x => (string?)x ?? "").Where(x => x.Length > 0).ToHashSet() : o.SkipSets,
        };
    }

    /// <summary>The most nodes, render items or relationships one frame carries (<see cref="Session.Parts"/>).</summary>
    public int PartSize { get; init; } = 20_000;

    readonly List<byte[]> _callerFrames = [];

    /// <summary>A diff as the parts the host pushes, one frame each.</summary>
    public IReadOnlyList<Diff> Parts(Diff diff) => session.Parts(diff, PartSize);

    /// <summary>The frames due to the caller alone before the reply just handled (a big snapshot's parts but its last, which the reply carries); taken once.</summary>
    public IReadOnlyList<byte[]> TakeCallerFrames()
    {
        var o = _callerFrames.ToList();
        _callerFrames.Clear();
        return o;
    }

    /// <summary>One call: its reply frame, and the diff it caused (null: nothing changed).</summary>
    public (byte[] Reply, Diff? Diff) Handle(string text) => Handle(text, Caller.Local);

    /// <summary>One call from <paramref name="caller"/>: refused unless they may make it; their edits are theirs (their own undo).</summary>
    public (byte[] Reply, Diff? Diff) Handle(string text, Caller caller)
    {
        JsonNode? id = null;
        try
        {
            var msg = JsonNode.Parse(text) as JsonObject ?? throw new CommandException("Bad message.");
            id = msg["id"]?.DeepClone();
            var method = (string?)msg["method"] ?? throw new CommandException("No method.");
            var p = msg["params"] as JsonObject ?? [];
            if (caller.Remote && AllowedRemote(method, p, caller.Role) is { } refused) throw new CommandException(refused);
            session.Author = caller.Author;
            session.Progress = ReportProgress;
            try
            {
                var (result, diff) = Dispatch(method, p);
                if (result is Diff { Reset: true } snapshot && Parts(snapshot) is { Count: > 1 } parts)
                {
                    foreach (var part in parts.SkipLast(1)) _callerFrames.Add(DiffFrame(part, caller.Author));
                    result = parts[^1];
                }
                return (BinaryFrame.Encode(new Reply(id, result), ContractsOnly), diff);
            }
            finally { session.Author = History.Me; session.Progress = null; }
        }
        catch (Exception ex) when (ex is ModelException or JsonException or InvalidOperationException or IOException or UnauthorizedAccessException or FormatException or InvalidDataException)
        {
            return (BinaryFrame.Encode(new Reply(id, null, ex.Message), ContractsOnly), null);
        }
        catch (Exception ex)
        {
            // An engine bug: answer the call (the interface must not hang) and leave a trace.
            Console.Error.WriteLine($"[engine] {ex}");
            return (BinaryFrame.Encode(new Reply(id, null, $"Internal engine error: {ex.Message}")), null);
        }
    }

    /// <summary>A diff as the frame the host pushes.</summary>
    public byte[] DiffFrame(Diff diff) => BinaryFrame.Encode(new DiffEvent("diff", diff), ContractsOnly);

    /// <summary>A diff as <paramref name="author"/> gets it: with their own undo and redo.</summary>
    public byte[] DiffFrame(Diff diff, string author) => author == History.Me ? DiffFrame(diff) : DiffFrame(diff with { State = session.StateFor(author) });

    /// <summary>
    /// What a guest may call (null), else why not. A list of what is let
    /// through, not of what is kept out: a method added later is refused from
    /// the network until it is named here. Nothing that reads or writes a
    /// file on the host's disk, changes which project is open, or speaks for
    /// the host (reports, links opened, the recovery journal); exports come
    /// back as bytes, imports bring theirs. A viewer looks; an editor edits.
    /// </summary>
    public static string? AllowedRemote(string method, JsonObject p, string role)
    {
        static bool HasPath(JsonObject o) => o["path"] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0;
        if (HasPath(p)) return "A guest cannot reach files on the host's computer.";
        var editor = role is "editor" or "owner";
        switch (method)
        {
            case "session.info" or "doc.snapshot" or "doc.query" or "quantities.of" or "quantities.many" or "view.caps"
                or "project.versions" or "project.compareVersions"
                or "export.pdf" or "export.dxf" or "project.exportIfc" or "project.exportMesh" or "project.exportSurvey":
                return null;
            case "doc.execute":
                if (!editor) return "You joined to look: ask the host to let you edit.";
                return p["args"] is JsonObject args && HasPath(args) ? "A guest cannot reach files on the host's computer." : null;
            case "doc.undo" or "doc.redo" or "project.convertToNative":
                return editor ? null : "You joined to look: ask the host to let you edit.";
            case "project.importIfc" or "project.importFragments" or "project.importBcf" or "project.importMesh":
                if (!editor) return "You joined to look: ask the host to let you edit.";
                return p["data"] is JsonValue ? null : "A guest imports a file from their own computer.";
            case "project.exportBcf":
                return null;
            default:
                return "Only the host can do that.";
        }
    }

    (object? Result, Diff? Diff) Dispatch(string method, JsonObject p)
    {
        // A file written where asked (the desktop's save dialog), else handed back to download (a browser).
        JsonObject Deliver(byte[] bytes, string name)
        {
            if ((string?)p["path"] is { Length: > 0 } path)
            {
                File.WriteAllBytes(path, bytes);
                return new JsonObject { ["path"] = path, ["bytes"] = bytes.Length };
            }
            return new JsonObject { ["name"] = name, ["data"] = Convert.ToBase64String(bytes) };
        }

        string PathParam() => (string?)p["path"] ?? throw new CommandException("The file's path is missing.");
        int Number() => p["number"] is JsonValue nv && nv.TryGetValue<int>(out var n) ? n : throw new CommandException("Which version?");

        switch (method)
        {
            case "session.info": return (session.Info(), null);
            case "doc.snapshot": return (session.Snapshot(), null);
            case "doc.execute":
            {
                var command = (string?)p["command"] ?? throw new CommandException("The command is missing.");
                var diff = session.Execute(command, p["args"] as JsonObject ?? [], out var result);
                return (result?.DeepClone(), diff);
            }
            case "doc.undo": return (null, session.Undo(session.Author));
            case "doc.redo": return (null, session.Redo(session.Author));
            case "doc.query":
                return (session.Query((string?)p["name"] ?? throw new CommandException("The query is missing."), p["args"] as JsonObject ?? [])?.DeepClone(), null);
            case "view.caps": return (session.Caps(ClipPlanes(p["planes"]), drawing: (string?)p["style"] == "drawing", scale: Json.Number(p["scale"]) is { } sc && sc > 0 ? sc : 50, layers: ViewStyles.LayersOf(p["layers"])), null);
            case "quantities.of": return (session.Quantities((string?)p["id"] ?? throw new CommandException("Which element?")), null);
            // Several elements' quantity sets in one call (the model assistant's lists and sums): id → sets; an id the model lacks is left out.
            case "quantities.many":
            {
                var ids = (p["ids"] as JsonArray ?? throw new CommandException("Which elements?")).Select(x => (string?)x).OfType<string>().Distinct().Take(5000);
                return (ids.Where(id => session.Model.Get(id) != null).ToDictionary(id => id, id => session.Quantities(id)), null);
            }
            // A new project with the default storeys; `plain`: one ground floor, for a file that fills it (an IFC opened as a project).
            case "project.new": files.Reset(); return (null, session.New(p["plain"]?.GetValue<bool>() == true));
            case "project.open":
            {
                var diff = files.Open(session, PathParam());
                return (new JsonObject { ["notes"] = new JsonArray(files.LastNotes.Select(n => (JsonNode)n).ToArray()) }, diff);
            }
            case "project.save": return (null, files.Save(session, (string?)p["path"]));
            case "project.openBytes":
            {
                // A project from its bytes (a host without files: the browser), base64 in `data`.
                var name = (string?)p["name"] ?? "project.lapin";
                var diff = files.OpenBytes(session, name, Convert.FromBase64String((string?)p["data"] ?? throw new CommandException("The project's bytes are missing.")));
                return (new JsonObject { ["notes"] = new JsonArray(files.LastNotes.Select(n => (JsonNode)n).ToArray()) }, diff);
            }
            case "project.saveBytes":
            {
                var name = (string?)p["name"] ?? (session.Path is { } current ? Path.GetFileName(current) : "project.lapin");
                if (!name.EndsWith(".lapin", StringComparison.OrdinalIgnoreCase)) name += ".lapin";
                var (bytes, diff) = files.SaveBytes(session, name);
                return (new JsonObject { ["name"] = name, ["data"] = Convert.ToBase64String(bytes) }, diff);
            }
            // ---- versions (docs/versions.md): the project's versions, in its file; each change writes the file (as bytes for the browser: `bytes: true`).
            case "project.versions": return (files.VersionList(session), null);
            case "project.saveVersion":
            {
                var (r, d) = files.SaveVersion(session, (string?)p["name"], (string?)p["note"], p["bytes"]?.GetValue<bool>() == true);
                return (r, d);
            }
            case "project.updateVersion":
            {
                var (r, d) = files.UpdateVersion(session, Number(), (string?)p["name"], (string?)p["note"], p["replace"]?.GetValue<bool>() == true, p["bytes"]?.GetValue<bool>() == true);
                return (r, d);
            }
            case "project.deleteVersion":
            {
                var (r, d) = files.DeleteVersion(session, Number(), p["bytes"]?.GetValue<bool>() == true);
                return (r, d);
            }
            case "project.restoreVersion": return (null, files.RestoreVersion(session, Number(), (int?)p["revision"]));
            case "project.exportVersion":
            {
                // To the path asked for; to a file of its own in the temporary folder (`temp`: to open it in another instance); else handed back as bytes.
                var (name, bytes) = files.ExportVersion(session, Number(), (int?)p["revision"]);
                if (p["temp"]?.GetValue<bool>() == true)
                {
                    var dir = Path.Combine(Path.GetTempPath(), "lapin-versions");
                    Directory.CreateDirectory(dir);
                    var tempPath = Path.Combine(dir, name);
                    File.WriteAllBytes(tempPath, bytes);
                    return (new JsonObject { ["path"] = tempPath, ["bytes"] = bytes.Length }, null);
                }
                return (Deliver(bytes, name), null);
            }
            case "project.compareVersions": return (files.CompareVersions(session, p["a"], p["b"]), null);
            case "project.exportIfc":
            {
                // The project as IFC (docs/spec-ifc.md): IFC4, or IFC4X3; parts grouped under their element, or one body each; a phase, or everything;
                // bodies as B-reps or face sets, simplified within a tolerance (metres) or exact; the kinds, storeys and hidden left out (ModelSurvey).
                var exclude = Lapin.Ifc.ModelSurvey.Excluded(session, p);
                var options = new IfcExportOptions((string?)p["schema"] ?? "IFC4", (string?)p["detail"] ?? "group", (string?)p["phase"],
                    (string?)p["geometry"] ?? "brep", Math.Clamp(Json.Number(p["tolerance"]) ?? 0, 0, 0.1), exclude.Count > 0 ? exclude : null);
                if ((string?)p["path"] is { Length: > 0 } ifcPath) return (IfcExport.Write(session, ifcPath, options).ToJson(), null);
                using var ms = new MemoryStream();
                IfcExport.Write(session, ms, "project.ifc", options);
                return (Deliver(ms.ToArray(), "project.ifc"), null);
            }
            case "project.exportMesh":
            {
                // The model as a 3D mesh (docs/plan-mesh-export.md): glb, gltf, usdz, usda or dae; a 3D view's visible elements, or all; a phase; edges; the origin.
                var options = Lapin.Mesh.MeshExportOptions.From(p);
                // The kinds and storeys chosen (the export survey's), left out as the hidden are.
                if (p["kinds"] is JsonArray || p["storeys"] is JsonArray)
                    options = options with { Hidden = Lapin.Ifc.ModelSurvey.Excluded(session, p) };
                var (bytes, result) = Lapin.Mesh.MeshExport.Write(session, options);
                var name = $"{(session.Path is { } sp ? Path.GetFileNameWithoutExtension(sp) : "project")}.{options.Extension}";
                var delivered = Deliver(bytes, name);
                foreach (var (k, v) in result.ToJson()) delivered[k] = v?.DeepClone();
                return (delivered, null);
            }
            case "project.ifcSurvey":
            {
                // What a file holds, for the user to choose from before it is imported: read and indexed here, kept for the import that follows.
                var (bytes, name, _, key) = IfcBytes(p);
                ReportProgress("Indexing the file", 0, 0);
                var file = StepFile.Read(bytes);
                _surveyed = (key, bytes, file, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
                return (IfcSurvey.Of(file, name, bytes.Length), null);
            }
            case "project.importIfc":
            {
                // An IFC file into the open project (docs/spec-ifc.md): Lapin's own files natively, others as references; "reference" keeps everything as it is;
                // "link" brings it in read-only, as a link (Manage → Links); `plan2d: false` leaves the elements' own 2D out, `only2d: false` the elements drawn only in 2D.
                var modeName = (string?)p["mode"] ?? "auto";
                var kinds = Chosen(p["kinds"]);
                var storeys = Chosen(p["storeys"]);
                var plan2d = (bool?)p["plan2d"] ?? true;
                var only2d = (bool?)p["only2d"] ?? true;
                // The import's rules (docs/spec-ifc.md, Regulile importului), as the dialog sends them; their defaults when it does not.
                // What the file places comes in as one group (docs/groups.md), unless asked otherwise; never a link's.
                var group = modeName != "link" && (p["group"] is not JsonValue gv || !gv.TryGetValue<bool>(out var gb) || gb);
                // "editable": recognised as Lapin elements, the rest made Studio solids where they can be (EditableReferences).
                var mode = Rules(new IfcImportOptions(modeName is "link" or "editable" ? "auto" : modeName, kinds, storeys, plan2d, only2d, Group: group, Editable: modeName == "editable"), p["rules"] as JsonObject);
                // By path (the desktop), or its bytes, base64 in `data` (the browser); the file as the survey read it, when it is the same.
                ReportProgress("Reading the file", 0, 0);
                var (bytes, name, path, key) = IfcBytes(p);
                (string Key, byte[] Bytes, StepFile File, string Hash)? surveyed = _surveyed is { } sv && sv.Key == key ? sv : null;
                _surveyed = null;
                StepFile file;
                if (surveyed != null) file = surveyed.Value.File;
                else { ReportProgress("Indexing the file", 0, 0); file = StepFile.Read(bytes); }
                IfcImportResult result;
                Diff imported;
                if (path == null)
                    result = modeName == "link" ? IfcImport.Link(session, file, name, null, mode, out imported) : IfcImport.Run(session, file, name, mode, out imported);
                else
                {
                    // From a file on this computer: the journal records the import by the file (its hash), its choices and the seed of its ids, not by its every entity.
                    var seed = Random.Shared.NextInt64();
                    var hash = surveyed?.Hash ?? Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
                    session.EditSource = new JsonObject { ["import"] = path, ["sha256"] = hash, ["mode"] = modeName, ["seed"] = seed, ["group"] = group };
                    if (kinds != null) session.EditSource["kinds"] = new JsonArray(kinds.Select(k => (JsonNode)k).ToArray());
                    if (storeys != null) session.EditSource["storeys"] = new JsonArray(storeys.Select(k => (JsonNode)k).ToArray());
                    if (!plan2d) session.EditSource["plan2d"] = false;
                    if (!only2d) session.EditSource["only2d"] = false;
                    if (p["rules"] is JsonObject rules) session.EditSource["rules"] = rules.DeepClone();
                    try
                    {
                        using var seeded = session.SeededIds(seed);
                        result = modeName == "link" ? IfcImport.Link(session, file, name, path, mode, out imported) : IfcImport.Run(session, file, name, mode, out imported);
                    }
                    finally { session.EditSource = null; }
                }
                return (result.ToJson(), imported);
            }
            case "project.exportSurvey":
                // The project's elements and storeys as an import survey shows a file's, for an export's choice.
                return (Lapin.Ifc.ModelSurvey.Of(session), null);
            case "project.fragSurvey":
            {
                // What a Fragments file holds, in the IFC survey's shape, kept read for the import that follows.
                var (bytes, name, _, key) = IfcBytes(p);
                ReportProgress("Reading the file", 0, 0);
                var model = Lapin.Mesh.Fragments.FragImport.Read(bytes, name);
                _fragSurveyed = (key, model);
                return (Lapin.Mesh.Fragments.FragSurvey.Of(model, name, bytes.Length), null);
            }
            case "project.importFragments":
            {
                // A That Open Fragments file (.frag, docs/fragments.md) into the open project: its items as references with their bodies, by path or by its bytes;
                // the kinds and storeys chosen (the survey's), as one group unless asked otherwise.
                ReportProgress("Reading the file", 0, 0);
                var (bytes, name, _, key) = IfcBytes(p);
                var options = new Lapin.Mesh.Fragments.FragImportOptions(Chosen(p["kinds"]), Chosen(p["storeys"]), p["group"] is not JsonValue gv || !gv.TryGetValue<bool>(out var gb) || gb,
                    Editable: (string?)p["mode"] == "editable" || p["editable"] is JsonValue ev && ev.TryGetValue<bool>(out var eb) && eb,
                    // "auto" and "editable": recognised from their triangles where a module takes them; "reference" (the default): as the file draws them.
                    Recognize: (string?)p["mode"] is "auto" or "editable");
                var model = _fragSurveyed is { } fs && fs.Key == key ? fs.Model : Lapin.Mesh.Fragments.FragImport.Read(bytes, name);
                _fragSurveyed = null;
                var r = Lapin.Mesh.Fragments.FragImport.Run(session, model, name, out var imported, options);
                return (r.ToJson(), imported);
            }
            case "project.importBcf":
            {
                // A BCF file (2.1 or 3.0, docs/plan-cde.md stage 2) into the project's remarks: topics by GUID, updated when known, added when new; by path or by its bytes.
                var (bytes, name, _, _) = IfcBytes(p);
                var file = Lapin.Platform.Cde.Remarks.BcfReader.Read(bytes);
                Lapin.Platform.Cde.Remarks.Remarks.ImportResult r = null!;
                var diff = session.Edit($"Import {name}", edit => r = Lapin.Platform.Cde.Remarks.Remarks.Import(edit, file));
                var result = r.ToJson();
                result["name"] = name;
                return (result, diff);
            }
            case "project.exportBcf":
            {
                // The remarks as a BCF file: all, the open ones, or those asked (ids); 2.1 unless version is 3.0. To a path (the desktop) or as bytes.
                var file = Lapin.Platform.Cde.Remarks.Remarks.Export(session.Model, p);
                var bytes = Lapin.Platform.Cde.Remarks.BcfWriter.Write(file);
                var delivered = Deliver(bytes, "remarks.bcfzip");
                delivered["topics"] = file.Topics.Count;
                delivered["version"] = file.Version;
                return (delivered, null);
            }
            case "project.convertToNative":
            {
                // References made native (decision 207): recognised from their triangles where a module takes them (a wall with its
                // doors and windows, a slab, a column), the rest made Studio solids when editable is asked; one undo step.
                var ids = (p["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [];
                if (ids.Count == 0) throw new CommandException("Select the references to convert.");
                var editable = p["editable"] is not JsonValue ev || !ev.TryGetValue<bool>(out var eb) || eb;
                MeshRecognitionReport rec = MeshRecognitionReport.None;
                EditableReport made = EditableReport.None;
                var diff = session.Edit("Convert to native", edit =>
                {
                    rec = MeshRecognition.Recognize(edit, session.Catalog, ids);
                    if (editable) made = EditableReferences.Make(edit, session.Catalog, ids);
                });
                return (new JsonObject { ["recognition"] = rec.ToJson(), ["editable"] = made.ToJson() }, diff);
            }
            case "project.importMesh":
            {
                // A mesh from another program (OBJ, STL, glTF, GLB) into the open project as references (MeshImport); a .gltf's buffers beside it read from its folder.
                ReportProgress("Reading the file", 0, 0);
                var (bytes, name, path, _) = IfcBytes(p);
                var r = Lapin.Mesh.MeshImport.Run(session, bytes, name, p, path != null ? Path.GetDirectoryName(path) : null, out var imported);
                return (r.ToJson(), imported);
            }
            case "project.reloadLink":
            {
                // A link read again from its file (the desktop: a link made from a path): what it held goes, what the file holds now comes in its place.
                var link = Links.Require(session.Model, (string?)p["id"]);
                var path = link.Str("path") ?? throw new CommandException("This link has no file to read again (it was linked from the browser).");
                if (!File.Exists(path)) throw new CommandException($"The file is gone: {path}.");
                if (link.Str("kind") == "dxf")
                {
                    var args = (link.Attrs["options"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
                    args["path"] = path; args["link"] = true; args["linkId"] = link.Id;
                    if (link.Str("viewId") is { } v) args["viewId"] = v;
                    var d = session.Execute("drafting.importDxf", args, out var dr);
                    return (dr, d);
                }
                var r = IfcImport.Link(session, File.ReadAllBytes(path), Path.GetFileName(path), path, new IfcImportOptions("auto"), out var reloaded, link.Id);
                return (r.ToJson(), reloaded);
            }
            case "export.pdf":
            {
                // Sheets as one PDF: those asked for, else all of them in order.
                var ids = p["sheets"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToList() : null;
                var sheets = session.Model.OfClass(Sheets.Class).Where(s => ids == null || ids.Contains(s.Id)).ToList();
                if (sheets.Count == 0) throw new CommandException("There is no sheet to print.");
                return (Deliver(Sheets.Pdf(session.Reader, sheets), "sheets.pdf"), null);
            }
            case "export.dxf":
            {
                var id = (string?)p["id"] ?? throw new CommandException("Which sheet or view?");
                var bytes = session.Model.Get(id) is { Class: Sheets.Class } sheet ? Sheets.Dxf(session.Reader, sheet)
                    : Views.Find(session.Model, id) is { } view ? Views.Dxf(session.Reader, view)
                    : throw new CommandException("There is no such sheet or view.");
                return (Deliver(bytes, "drawing.dxf"), null);
            }
            // An issue (docs/plan-cde.md): the files made and hashed, kept in the project's file (and written into the project folder's container
            // when there is one), the transmittal, the issue recorded (one undo step) and the project saved as a version named after it, in the
            // same call, so the record names its version and the project is saved after an issue. `bytes`: the file comes back (the browser);
            // `saveVersion: false` leaves the version out (the project stays unsaved). An unsaved project on the desktop keeps the files in memory
            // until its first save, without a version.
            case "cde.issue":
            {
                var asBytes = p["bytes"]?.GetValue<bool>() == true;
                var withVersion = p["saveVersion"]?.GetValue<bool>() != false && (session.Path != null || asBytes);
                int? version = withVersion ? files.Versions(session).NextNumber : null;
                var (r, d) = CdeIssue.Run(session, files.Issues(session), p, version);
                if (version == null) return (r, d);
                try
                {
                    var (vr, vd) = files.SaveVersion(session, (string?)r["name"], $"Issue {(int?)r["number"]} · {(string?)r["revision"]} {(string?)r["status"]} → {(string?)r["container"]}", asBytes);
                    if ((string?)vr["data"] is { } data) r["file"] = new JsonObject { ["name"] = (string?)vr["name"], ["data"] = data };
                    r["saved"] = true;
                    return (r, d with { Version = vd.Version, State = vd.State });
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or CommandException)
                {
                    // The issue stands (its record, its files in memory); the version did not go: the project stays unsaved and says why.
                    r["version"] = null;
                    r["versionError"] = e.Message;
                    return (r, d);
                }
            }
            // An issue's deliverables from the project's file: one file (to a path, to the temporary folder to open it, or as bytes), all of them
            // as one zip, or written out to a folder (the project folder's container, or a folder picked: `flat`).
            case "cde.issueFile":
            {
                var (name, bytes) = CdeDeliverables.File(session.Model, files.Issues(session), (string?)p["id"], (string?)p["name"]);
                if (p["temp"]?.GetValue<bool>() == true)
                {
                    var dir = Path.Combine(Path.GetTempPath(), "lapin-issues");
                    Directory.CreateDirectory(dir);
                    var tempPath = Path.Combine(dir, name);
                    File.WriteAllBytes(tempPath, bytes);
                    return (new JsonObject { ["path"] = tempPath, ["bytes"] = bytes.Length }, null);
                }
                return (Deliver(bytes, name), null);
            }
            case "cde.issueZip":
            {
                var (name, bytes, missing) = CdeDeliverables.Zip(session.Model, files.Issues(session), (string?)p["id"]);
                var delivered = Deliver(bytes, name);
                delivered["missing"] = new JsonArray(missing.Select(x => (JsonNode)x).ToArray());
                return (delivered, null);
            }
            case "cde.publishIssue":
                return (CdeDeliverables.Publish(session.Model, files.Issues(session), (string?)p["id"], (string?)p["folder"], p["flat"]?.GetValue<bool>() == true), null);
            // The hashes an issue record says are kept that the project's file does not hold (a version restored from before they were let go).
            case "cde.missing":
            {
                var store = files.Issues(session);
                return (new JsonArray(Lapin.Platform.Cde.CdeIssues.StoredHashes(session.Model).Where(h => !store.Has(h)).Select(h => (JsonNode)h).ToArray()), null);
            }
            // A reviewer's PDF read for its remarks (by path, or its bytes), and the project folder scanned for the issued PDFs touched since (stage 3).
            case "cde.readPdf": return (CdePdfRemarks.Read(session, p), null);
            case "cde.scanRemarks": return (CdePdfRemarks.Scan(session, p), null);
            case "support.report": return ((support ?? throw new CommandException("Reports are sent from the app.")).Report(p), null);
            case "support.messages": return ((support ?? throw new CommandException("Messages are read from the app.")).Messages(p), null);
            case "support.open":
                (support ?? throw new CommandException("Nothing to open here.")).Open((string?)p["link"]);
                return (null, null);
            case "recovery.list": return (recovery?.List() ?? [], null);
            case "recovery.restore": return (null, (recovery ?? throw new CommandException("No recovery here.")).Restore(PathParam()));
            case "recovery.discard":
                (recovery ?? throw new CommandException("No recovery here.")).Discard(PathParam());
                return (null, null);
            default: throw new CommandException($"Unknown method „{method}”.");
        }
    }

    /// <summary>A section's planes as the 3D view sends them: [{normal: [x, y, z], constant}], kept where normal·p + constant ≥ 0 (a section and its depth with a box: eight); at most twelve.</summary>
    static List<ClipPlane> ClipPlanes(JsonNode? n)
    {
        var o = new List<ClipPlane>();
        if (n is not JsonArray list) return o;
        foreach (var item in list)
        {
            if (o.Count == 12) break;
            if (item is not JsonObject p || p["normal"] is not JsonArray { Count: 3 } v || p["constant"] is not JsonValue c) continue;
            double At(int k) => v[k] is JsonValue x && x.TryGetValue<double>(out var d) && double.IsFinite(d) ? d : 0;
            if (!c.TryGetValue<double>(out var k0) || !double.IsFinite(k0)) continue;
            o.Add(new ClipPlane(new Vec3(At(0), At(1), At(2)), k0));
        }
        return o;
    }
}
