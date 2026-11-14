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
    /// <summary>Open one of the help menu's links by name; the forum's new topic takes its title and body from <paramref name="p"/>.</summary>
    void Open(string? link, JsonObject p);
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
/// <c>issues/blobs/</c>. And it holds the entries other tools put in the
/// file (<see cref="PackageForeign"/>), written back as they were read.
/// </summary>
public sealed class ProjectFiles(Func<Session, string, Diff> open, Action<Session, string, PackageVersions, IssueFiles?, PackageForeign> write)
{
    PackageVersions _versions = PackageVersions.None;
    /// <summary>The session path the versions belong to: another path (a recovered project's) has them read from its file.</summary>
    string? _versionsOf;
    PackageIssues _issues = PackageIssues.Empty();
    /// <summary>The session path the issues' files were read from (null: a project not saved yet, its issues' files in memory).</summary>
    string? _issuesOf;
    PackageForeign _foreign = PackageForeign.None;
    /// <summary>The session path the foreign entries were read from.</summary>
    string? _foreignOf;

    /// <summary>
    /// A project from its bytes; <paramref name="name"/> is the file's name, its
    /// title. An old project (format 2) is rebuilt as on the desktop and opens
    /// unsaved, what it left out in <see cref="LastNotes"/>.
    /// </summary>
    public Diff OpenBytes(Session session, string name, byte[] bytes)
    {
        LastNotes = [];
        LastPage = null;
        _page = null;
        if (HtmlPackage.IsPage(bytes)) return OpenPage(session, name, bytes);
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
        KeepForeign(name, PackageForeign.Of(bytes));
        return diff;
    }

    /// <summary>The project as a package's bytes; the session counts as saved.</summary>
    public (byte[] Bytes, Diff Diff) SaveBytes(Session session, string name)
    {
        var versions = Versions(session);
        var issues = IssueFilesOf(session);
        var foreign = Foreign(session);
        using var ms = new MemoryStream();
        ProjectPackage.Write(session, ms, versions, issues, foreign);
        var bytes = ms.ToArray();
        _page = null;
        var diff = session.Saved(name);
        KeepForeign(name, foreign);
        Keep(name, versions.From(bytes));
        KeepIssues(name, issues.Store.From(bytes, issues.Written));
        return (bytes, diff);
    }

    /// <summary>What the last opening left out (an old project's parts that do not come over), for the interface to tell.</summary>
    public List<string> LastNotes { get; set; } = [];

    /// <summary>The page the last opening read the project from (a Lapin for the browser .html, decision 482): its file's name; null for a project's own file.</summary>
    public string? LastPage { get; private set; }

    /// <summary>
    /// What a page opened from carried beside the model (its versions and other tools' entries), kept for the first
    /// save: the project opens without a file of its own, so Save asks where, as a .lapin, never over the page.
    /// </summary>
    (PackageVersions Versions, PackageForeign Foreign)? _page;

    /// <summary>
    /// A project from a page Lapin exported for the browser (docs/html-export.md): the package after the page, read as a
    /// zip as it stands; a page of anyone else's, or Lapin's without its project, refused with why.
    /// </summary>
    Diff OpenPage(Session session, string name, byte[] bytes)
    {
        try { HtmlPackage.Check(bytes); }
        catch (InvalidDataException e) { throw new CommandException(e.Message); }
        var content = LapinPackage.Read(new MemoryStream(bytes, writable: false));
        var diff = session.Load(content.Entities, content.Relations, null, content.Classes);
        _page = (PackageVersions.Of(bytes), PackageForeign.Of(bytes));
        KeepIssues(null, PackageIssues.Of(bytes));
        LastPage = Path.GetFileName(name);
        return diff;
    }

    /// <summary>A project opened otherwise (recovered work): no page's versions carried over.</summary>
    public void ForgetPage() => _page = null;

    public Diff Open(Session session, string path)
    {
        LastPage = null;
        _page = null;
        if (path.EndsWith(HtmlPackage.Suffix, StringComparison.OrdinalIgnoreCase) || path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) || File.Exists(path) && HtmlPackage.IsPage(path))
        {
            LastNotes = [];
            return OpenPage(session, path, File.ReadAllBytes(path));
        }
        var diff = open(session, path);
        Keep(path, PackageVersions.Of(path));
        KeepIssues(path, PackageIssues.Of(path));
        KeepForeign(path, PackageForeign.Of(path));
        return diff;
    }

    public Diff Save(Session session, string? path)
    {
        var target = path ?? session.Path ?? throw new CommandException("Where to save: the project has no file yet.");
        var versions = Versions(session);
        var issues = IssueFilesOf(session);
        var foreign = Foreign(session);
        write(session, target, versions, issues, foreign);
        _page = null;
        var diff = session.Saved(target);
        KeepForeign(session.Path ?? target, foreign);
        Keep(session.Path ?? target, versions.From(session.Path ?? target));
        KeepIssues(session.Path ?? target, issues.Store.From(session.Path ?? target, issues.Written));
        return diff;
    }

    /// <summary>A new project: no issue files of the last one carried over.</summary>
    public void Reset()
    {
        _page = null;
        _issues = PackageIssues.Empty();
        _issuesOf = null;
        _foreign = PackageForeign.None;
        _foreignOf = null;
    }

    /// <summary>The entries other tools put in the open project's file: those read with it, else read from its file (a project recovered); none for a new project.</summary>
    public PackageForeign Foreign(Session session)
    {
        if (session.Path is not { } path) return _page?.Foreign ?? PackageForeign.None;
        if (_foreignOf != path) KeepForeign(path, PackageForeign.Of(path));
        return _foreign;
    }

    void KeepForeign(string path, PackageForeign foreign)
    {
        _foreign = foreign;
        _foreignOf = path;
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
        if (session.Path is not { } path) return _page?.Versions ?? PackageVersions.None;
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
        var foreign = Foreign(session);
        if (asBytes)
        {
            var name = session.Path ?? "project.lapin";
            using var ms = new MemoryStream();
            LapinPackage.Write(ms, build, versions, issues, foreign);
            var bytes = ms.ToArray();
            _page = null;
            var diff = session.Saved(name);
            KeepForeign(name, foreign);
            Keep(name, versions.From(bytes));
            KeepIssues(name, issues.Store.From(bytes, issues.Written));
            result["name"] = name;
            result["data"] = Convert.ToBase64String(bytes);
            return (result, diff);
        }
        var path = session.Path ?? throw new CommandException("Save the project first: its versions live in its file.");
        LapinPackage.Write(path, build, versions, issues, foreign);
        var saved = session.Saved(path);
        KeepForeign(path, foreign);
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
        var (entities, relations) = session.Upgraded(content.Entities, content.Relations);
        // The classes it brought are adopted only once it is restored (a restore that throws adopts none); undone, they stay
        // known, which writes nothing while none of them is in the model.
        return session.Edit($"Restore version {number}", edit => { ModelCompare.Apply(edit, entities, relations); session.AdoptClasses(content.Classes); });
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
            // What the modules say changed beside the elements (the work schedule: tasks moved, durations changed, the finish).
            ["sections"] = Sections(session, x.Entities, x.Relations, y.Entities, y.Relations),
        };
    }

    static JsonArray Sections(Session session, IReadOnlyList<Entity> ae, IReadOnlyList<Relation> ar, IReadOnlyList<Entity> be, IReadOnlyList<Relation> br)
    {
        var o = new JsonArray();
        var compares = session.Catalog.All<IVersionCompare>();
        if (compares.Count == 0) return o;
        var a = new ModelSnapshot(ae, ar);
        var b = new ModelSnapshot(be, br);
        // A comparison that measures (the estimate) reads each state through a session of its own, made once when asked.
        IQueryContext? qa = null, qb = null;
        IQueryContext Measured(IReadOnlyList<Entity> es, IReadOnlyList<Relation> rs)
        {
            var side = new Session(session.Catalog, session.Product);
            side.Load(es, rs, session.Path);
            return side.Reader;
        }
        foreach (var c in compares)
        {
            VersionDiffSection? s;
            try { s = c.Measures ? c.Compare(qa ??= Measured(ae, ar), qb ??= Measured(be, br)) : c.Compare(a, b); }
            catch (CommandException) { continue; }
            if (s == null) continue;
            o.Add((JsonNode)new JsonObject
            {
                ["key"] = s.Key, ["title"] = s.Title,
                ["facts"] = new JsonArray(s.Facts.Select(f => (JsonNode)new JsonObject { ["label"] = f.Label, ["from"] = f.From, ["to"] = f.To }).ToArray()),
                ["rows"] = new JsonArray(s.Rows.Select(r => (JsonNode)new JsonObject { ["id"] = r.Id, ["name"] = r.Name, ["what"] = r.What, ["from"] = r.From, ["to"] = r.To, ["days"] = r.Days }).ToArray()),
            });
        }
        return o;
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
    /// <summary>The connection the call came on (the host's client), whose a script's open step is (<see cref="Session.Connection"/>); null in-process.</summary>
    public object? Connection { get; init; }
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
public sealed partial class EngineDispatcher(Session session, ProjectFiles files, IRecoveryJournal? recovery = null, ISupportDesk? support = null)
{
    public Session Session => session;

    /// <summary>Frames written by the generated contracts alone, as a trimmed engine writes them (for tests).</summary>
    public bool ContractsOnly { get; init; }

    /// <summary>The project templates kept on this computer (decision 470): the desktop's user folder; null in a host without one (the browser keeps its own).</summary>
    public TemplateLibrary? Templates { get; init; }

    /// <summary>My furniture library kept on this computer (decision 476): the desktop's user folder; null in a host without one (the browser keeps its own).</summary>
    public FurnitureLibrary? Furniture { get; init; }

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

    /// <summary>The long call that can be stopped now (an IFC file being optimised, an SDNF import), and stopping it: the host calls it from outside the calls' queue.</summary>
    CancellationTokenSource? _long;
    public void CancelLong() { try { _long?.Cancel(); } catch (ObjectDisposedException) { } }

    /// <summary>The last file surveyed (project.ifcSurvey), read and indexed, for the import that follows to take as it is.</summary>
    (string Key, byte[] Bytes, StepFile File, string Hash)? _surveyed;
    // The Fragments and BOS files surveyed (EngineDispatcher.Mesh.cs): as objects, so that this class touches nothing of Lapin.Mesh.
    object? _fragSurveyed;
    object? _bosSurveyed;

    /// <summary>An IFC file as a call gives it: by path (the desktop) or as base64 in data (the browser); its name and a key that tells the same file again.</summary>
    (byte[] Bytes, string Name, string? Path, string Key) IfcBytes(JsonObject p)
    {
        if ((string?)p["data"] is { } data)
        {
            var bytes = Convert.FromBase64String(data);
            var name = (string?)p["name"] ?? "file.ifc";
            return (bytes, name, null, $"data:{name}:{bytes.Length}");
        }
        var path = HostFiles.Check((string?)p["path"] ?? throw new CommandException("The file's path is missing."));
        var info = new FileInfo(path);
        if (!info.Exists) throw new CommandException($"There is no file at {path}.");
        return (File.ReadAllBytes(path), info.Name, path, $"{path}:{info.Length}:{info.LastWriteTimeUtc.Ticks}");
    }

    /// <summary>A start screen template's levels (decision 261): each a name, an elevation and a height in metres; null when none or one is unreadable.</summary>
    public static List<(string Name, double Elevation, double Height)>? TemplateStoreys(JsonNode? n)
    {
        if (n is not JsonArray a || a.Count == 0) return null;
        var list = new List<(string, double, double)>();
        foreach (var x in a)
        {
            var name = (string?)x?["name"];
            if (string.IsNullOrWhiteSpace(name) || x?["elevation"] is not JsonValue e || !e.TryGetValue<double>(out var elevation)
                || x["height"] is not JsonValue h || !h.TryGetValue<double>(out var height) || height <= 0) return null;
            list.Add((name.Trim(), elevation, height));
        }
        return list;
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
            // The fidelity gate (docs/spec-ifc.md, Poarta de fidelitate): "enforce" (the default), "report" or "off"; its tolerances in mm and percent.
            Fidelity = r["fidelity"] is null && r["fidelityMm"] is null && r["fidelityVolume"] is null ? o.Fidelity : new FidelityOptions(
                FidelityOptions.ModeOf((string?)r["fidelity"] ?? o.Gate.Mode),
                Json.Number(r["fidelityMm"]) is { } mm && mm > 0 ? mm : o.Gate.Mm,
                Json.Number(r["fidelityVolume"]) is { } pc && pc > 0 ? pc / 100 : o.Gate.Volume),
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
            session.Connection = caller.Connection;
            session.Progress = ReportProgress;
            HostFiles.GuestCall = caller.Remote;
            // Any call may be stopped by cancel.long: a command that looks at the session's token (an SDNF import) stops, its edit undone.
            using var stop = new CancellationTokenSource();
            _long = stop;
            session.Cancellation = stop.Token;
            try
            {
                var (result, diff) = Dispatch(method, p);
                if (result is Diff { Reset: true } snapshot)
                {
                    if (Parts(snapshot) is { Count: > 1 } parts)
                    {
                        foreach (var part in parts.SkipLast(1)) _callerFrames.Add(DiffFrame(part, caller.Author));
                        snapshot = parts[^1];
                    }
                    // The last part, carried by the reply: with the caller's own undo and redo, as the parts before it.
                    result = caller.Remote ? snapshot with { State = session.StateFor(caller.Author) } : snapshot;
                }
                return (BinaryFrame.Encode(new Reply(id, result), ContractsOnly), diff);
            }
            finally { session.Author = History.Me; session.Connection = null; session.Progress = null; session.Cancellation = default; _long = null; HostFiles.GuestCall = false; }
        }
        // A refusal the user reads. InvalidOperationException is not one: it is what an empty First(), a JsonNode given two parents or a
        // null Value throws, an engine bug, logged below.
        catch (Exception ex) when (ex is ModelException or JsonException or IOException or UnauthorizedAccessException or FormatException or InvalidDataException)
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
    /// <summary>The library files a firm's folder may hold (docs/plan-user-extensibility.md §4.2), by extension.</summary>
    public static readonly string[] LibraryKinds = ["lapinsymbols", "lapintypes", "lapinstandards", "lapinrules", "lapinbarshapes", "lapinparams", "lapinmapping", "lapinfamily", "lapinpart", "lapinlib", "lapintitleblock", "lapinprofiles", "lapinprices", "pat", "lin"];

    public static string? AllowedRemote(string method, JsonObject p, string role)
    {
        // Anywhere in the call (a query's args, a batch's steps, an attribute set): a file or folder named by its path.
        if (NamesHostFile(p)) return HostFiles.Refused;
        var editor = role is "editor" or "owner";
        // A project's graphs are the host's (docs/plan-visual-graphs.md §7, the user's answer of 13.11.2026): a guest neither evaluates one.
        if (method == "doc.query" && (string?)p["name"] is { } q && HostOnlyQueries.Any(x => q.StartsWith(x, StringComparison.Ordinal))) return "Only the host can do that.";
        switch (method)
        {
            case "session.info" or "session.catalogue" or "doc.snapshot" or "doc.query" or "quantities.of" or "quantities.many" or "view.caps" or "view.fine" or "pointcloud.tiles"
                or "project.versions" or "project.compareVersions"
                or "export.pdf" or "export.dxf" or "project.exportIfc" or "project.exportMesh" or "project.exportSurvey" or "project.exportHtml" or "project.htmlPlan":
                return null;
            case "doc.execute":
                // A project's scripts are the host's (docs/plan-python-scripting.md §7): a guest never puts one in, alone or in a batch.
                if (HostOnly(p)) return "Only the host can do that.";
                // Marking progress on site (docs/plan-4d5d.md, stage 5) is open to every guest, one who joined to look too.
                if (!editor && (string?)p["command"] is { } command && SiteCommands.Contains(command)) return null;
                return editor ? null : "You joined to look: ask the host to let you edit.";
            case "doc.undo" or "doc.redo" or "project.convertToNative":
                return editor ? null : "You joined to look: ask the host to let you edit.";
            case "project.importIfc" or "project.importFragments" or "project.importBos" or "project.importBcf" or "project.importMesh":
                if (!editor) return "You joined to look: ask the host to let you edit.";
                return p["data"] is JsonValue ? null : "A guest imports a file from their own computer.";
            case "project.exportBcf":
                return null;
            default:
                return "Only the host can do that.";
        }
    }

    /// <summary>
    /// The commands any guest may run, one who joined to look too: progress marked on site (the planning module's
    /// <c>markDone</c>, <c>markStarted</c>, <c>unmark</c> and <c>setProgress</c>; docs/plan-4d5d.md, stage 5), and a site photo
    /// or a daily report added (the premium planning edition's, stage 8). Named here rather than read from the module, so a
    /// product without them refuses them like any command it lacks.
    /// </summary>
    /// <summary>
    /// The namespaces of commands no guest runs: a project's scripts (<c>scripting.save</c>, <c>scripting.rename</c>,
    /// <c>scripting.delete</c>) and its graphs (<c>graphs.*</c>, decision 527: the host's, like scripts).
    /// </summary>
    public static readonly string[] HostOnlyCommands = ["scripting.", "graphs."];

    /// <summary>The namespaces of queries no guest asks: a project's graphs (their evaluation runs the host's commands on a copy of the model).</summary>
    public static readonly string[] HostOnlyQueries = ["graphs."];

    /// <summary>Whether a <c>doc.execute</c> names a host-only command, itself or as a step of its batch.</summary>
    static bool HostOnly(JsonObject p)
    {
        static bool Is(string? c) => c != null && HostOnlyCommands.Any(x => c.StartsWith(x, StringComparison.Ordinal));
        if (Is((string?)p["command"])) return true;
        return (string?)p["command"] == "edit.batch" && p["args"]?["steps"] is JsonArray steps && steps.Any(s => s is JsonObject o && Is((string?)o["command"]));
    }

    public static readonly HashSet<string> SiteCommands =["planning.markDone", "planning.markStarted", "planning.unmark", "planning.setProgress", "planning-pro.addPhoto", "planning-pro.addReport"];

    /// <summary>The keys that name a file or a folder on the computer that reads them.</summary>
    static readonly HashSet<string> PathKeys = new(StringComparer.OrdinalIgnoreCase) { "path", "paths", "folder", "directory", "dir" };

    /// <summary>Whether a string under one of <see cref="PathKeys"/> is anywhere in <paramref name="node"/>, at any depth.</summary>
    static bool NamesHostFile(JsonNode? node, bool underPathKey = false, int depth = 0)
    {
        // Deeper than any call nests: refused rather than walked.
        if (depth > 64) return true;
        switch (node)
        {
            case JsonObject o:
                foreach (var (k, v) in o) if (NamesHostFile(v, PathKeys.Contains(k), depth + 1)) return true;
                return false;
            case JsonArray a:
                foreach (var v in a) if (NamesHostFile(v, underPathKey, depth + 1)) return true;
                return false;
            case JsonValue v:
                return underPathKey && v.TryGetValue<string>(out var s) && s.Trim().Length > 0;
            default:
                return false;
        }
    }

    (object? Result, Diff? Diff) Dispatch(string method, JsonObject p)
    {
        // A script's step open: the project is not saved half way through it (a rollback would leave the file ahead of the model).
        if (method is "project.save" or "project.saveBytes" or "project.saveVersion" or "project.updateVersion") session.RefuseDuringStep(ownerToo: true);
        // Another project: a file surveyed for this one (its bytes, its index) is not kept for it.
        if (method is "project.new" or "project.newFromTemplate" or "project.open" or "project.openBytes" or "recovery.restore") { _surveyed = null; _fragSurveyed = null; _bosSurveyed = null; }
        // A file written where asked (the desktop's save dialog), else handed back to download (a browser).
        JsonObject Deliver(byte[] bytes, string name)
        {
            if ((string?)p["path"] is { Length: > 0 } path)
            {
                File.WriteAllBytes(HostFiles.Check(path), bytes);
                return new JsonObject { ["path"] = path, ["bytes"] = bytes.Length };
            }
            return new JsonObject { ["name"] = name, ["data"] = Convert.ToBase64String(bytes) };
        }

        TemplateLibrary Library() => Templates ?? throw new CommandException("Templates are kept by the desktop app; in the browser the page keeps them.");
        static byte[] Base64(JsonNode? data)
        {
            try { return Convert.FromBase64String((string?)data ?? throw new CommandException("The file's bytes are missing.")); }
            catch (FormatException) { throw new CommandException("The file's bytes are not base64."); }
        }
        FurnitureLibrary FurnitureShelf() => Furniture ?? throw new CommandException("My furniture is kept by the desktop app; in the browser the page keeps it.");
        string FurnitureId() => (string?)p["id"] ?? throw new CommandException("Which furniture?");
        string PathParam() => HostFiles.Check((string?)p["path"] ?? throw new CommandException("The file's path is missing."));
        int Number() => p["number"] is JsonValue nv && nv.TryGetValue<int>(out var n) ? n : throw new CommandException("Which version?");

        switch (method)
        {
            case "session.info": return (session.Info(), null);
            // Every command and query with the arguments it reads (decision 489): the MCP bridge's and the Python package's (docs/python.md).
            case "session.catalogue": return (CommandCatalogue.For(session), null);
            case "doc.snapshot": return (session.Snapshot(), null);
            case "doc.execute":
            {
                var command = (string?)p["command"] ?? throw new CommandException("The command is missing.");
                var diff = session.Execute(command, p["args"] as JsonObject ?? [], out var result);
                return (result?.DeepClone(), diff);
            }
            // A step held open by a script's run (docs/plan-python-scripting.md §8): its commands one undo step, the connection's own;
            // the host's alone (a guest is refused by AllowedRemote), rolled back by the host when the connection is lost.
            // A begin inside the connection's own open step joins it ({joined: true}): only the outermost commit or rollback ends it.
            case "doc.begin": return (new JsonObject { ["joined"] = session.Begin((string?)p["label"] ?? "Script") }, null);
            case "doc.commit": return (null, session.Commit());
            case "doc.rollback": return (null, session.Rollback());
            case "doc.undo": return (null, session.Undo(session.Author));
            case "doc.redo": return (null, session.Redo(session.Author));
            case "doc.query":
                return (session.Query((string?)p["name"] ?? throw new CommandException("The query is missing."), p["args"] as JsonObject ?? [])?.DeepClone(), null);
            case "view.caps": return (session.Caps(ClipPlanes(p["planes"]), drawing: (string?)p["style"] == "drawing", scale: Json.Number(p["scale"]) is { } sc && sc > 0 ? sc : 50, layers: ViewStyles.LayersOf(p["layers"]), fine: p["fine"] is JsonValue fv && fv.TryGetValue<bool>(out var fb) && fb), null);
            // What a 3D view at LOD 400 draws finer than the render (decision 415): {id, ids}; no ids, every such element.
            case "view.fine": return (session.FineBodies((string?)p["id"], (p["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList()), null);
            // A point cloud's tiles for the 3D view, by name, as raw arrays (docs/point-clouds.md).
            case "pointcloud.tiles":
                return (PointCloudLinks.Tiles(session.Model, Links.Require(session.Model, (string?)p["id"]), (p["nodes"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>()), null);
            case "quantities.of": return (session.Quantities((string?)p["id"] ?? throw new CommandException("Which element?")), null);
            // Several elements' quantity sets in one call (the model assistant's lists and sums): id → sets; an id the model lacks is left out.
            case "quantities.many":
            {
                var ids = (p["ids"] as JsonArray ?? throw new CommandException("Which elements?")).Select(x => (string?)x).OfType<string>().Distinct().Take(5000);
                return (ids.Where(id => session.Model.Get(id) != null).ToDictionary(id => id, id => session.Quantities(id)), null);
            }
            // A new project with the default storeys; `plain`: one ground floor, for a file that fills it (an IFC opened as a project).
            // `profileStandards`: the profile standards its pickers offer (decision 457; none: all).
            case "project.new": files.Reset(); return (null, session.New(p["plain"]?.GetValue<bool>() == true, TemplateStoreys(p["storeys"]), Lapin.Platform.ProfileLibraries.ProfileStandards.Read(p["profileStandards"])));
            // ---- project templates (issues #9 and #20, decision 470): a new project from one (the library's by `id`, a file by `path`, bytes in `data`).
            case "project.newFromTemplate":
            {
                byte[] bytes = (string?)p["id"] is { Length: > 0 } tid ? Library().Bytes(tid)
                    : (string?)p["path"] is { Length: > 0 } tpath ? File.ReadAllBytes(HostFiles.Check(tpath))
                    : Base64(p["data"]);
                files.Reset();
                return (null, TemplateLibrary.NewProject(session, bytes));
            }
            // The open project as a template's bytes (the browser keeps them); with `data`, a file's (a template or any project) instead, the open project left alone.
            // `furniture`: items of my furniture library put in too (decision 476).
            case "template.make":
            {
                var furniture = p["furniture"] as JsonArray;
                var made = p["data"] is JsonValue
                    ? furniture is { Count: > 0 } ? TemplateLibrary.AddFurniture(session, Base64(p["data"]), furniture) : TemplateLibrary.FromFile(session, Base64(p["data"]))
                    : TemplateLibrary.Make(session, furniture);
                return (new JsonObject { ["data"] = Convert.ToBase64String(made) }, null);
            }
            case "templates.list": return (Templates?.List() ?? [], null);
            case "templates.save":
            {
                // The open project kept as a template; one of that name is replaced only when asked (`overwrite`).
                var id = Library().Keep((string?)p["name"] ?? "", TemplateLibrary.Make(session, p["furniture"] as JsonArray), p["overwrite"]?.GetValue<bool>() == true);
                return (id == null ? new JsonObject { ["exists"] = true } : new JsonObject { ["id"] = id }, null);
            }
            case "templates.import":
            {
                // A template's file (or any project's) taken into the library, by `path` or as `data`; named after the file unless `name` says.
                var path = (string?)p["path"] is { Length: > 0 } ip ? HostFiles.Check(ip) : null;
                var bytes = path != null ? File.ReadAllBytes(path) : Base64(p["data"]);
                var name = (string?)p["name"] is { Length: > 0 } n ? n : path != null ? Path.GetFileNameWithoutExtension(path) : "Template";
                var id = Library().Import(session, name, bytes, p["overwrite"]?.GetValue<bool>() == true);
                return (id == null ? new JsonObject { ["exists"] = true } : new JsonObject { ["id"] = id }, null);
            }
            case "templates.export":
            {
                var id = (string?)p["id"] ?? throw new CommandException("Which template?");
                return (Deliver(Library().Bytes(id), TemplateLibrary.IdOf(id) + ".lapin"), null);
            }
            // Items of my furniture library put into a template of the library (decision 476): {id, furniture: [entries or types]}.
            case "templates.addFurniture":
            {
                var id = (string?)p["id"] ?? throw new CommandException("Which template?");
                var furniture = p["furniture"] as JsonArray ?? throw new CommandException("Which furniture?");
                Library().Keep(id, TemplateLibrary.AddFurniture(session, Library().Bytes(id), furniture), overwrite: true);
                return (null, null);
            }
            // ---- my furniture library (decision 476): kept by the desktop's engine; the browser keeps its entries and asks only to check, pack and unpack them.
            case "furnitureLibrary.list": return (new JsonArray([.. (Furniture?.List() ?? []).Select(e => (JsonNode)e)]), null);
            case "furnitureLibrary.get": return (FurnitureShelf().Get(FurnitureId()), null);
            case "furnitureLibrary.add": return (new JsonObject { ["id"] = FurnitureShelf().Add(p["entry"] as JsonObject ?? throw new CommandException("Which furniture?")) }, null);
            case "furnitureLibrary.update": return (FurnitureShelf().Update(FurnitureId(), p["patch"] as JsonObject ?? []), null);
            case "furnitureLibrary.duplicate": return (new JsonObject { ["id"] = FurnitureShelf().Duplicate(FurnitureId()) }, null);
            case "furnitureLibrary.delete": FurnitureShelf().Delete(FurnitureId()); return (null, null);
            case "furnitureLibrary.export":
            {
                // `ids`: those entries (none: the whole library), to `path` or handed back as bytes.
                var ids = p["ids"] is JsonArray ia ? ia.Select(x => (string?)x).OfType<string>().ToList() : null;
                var name = ids is { Count: 1 } ? TemplateLibrary.IdOf((string?)FurnitureShelf().Get(ids[0])["name"] ?? "Furniture") : "My furniture";
                return (Deliver(FurnitureShelf().Export(ids), name + FurnitureLibrary.Suffix), null);
            }
            case "furnitureLibrary.import":
            {
                var bytes = (string?)p["path"] is { Length: > 0 } fp ? File.ReadAllBytes(HostFiles.Check(fp)) : Base64(p["data"]);
                var (added, skipped) = FurnitureShelf().Import(bytes);
                return (new JsonObject { ["added"] = new JsonArray([.. added.Select(x => (JsonNode)x)]), ["skipped"] = skipped }, null);
            }
            case "furnitureLibrary.check": return (FurnitureLibrary.Check(p["entry"] as JsonObject ?? throw new CommandException("Which furniture?")), null);
            case "furnitureLibrary.pack":
                return (new JsonObject { ["data"] = Convert.ToBase64String(FurnitureLibrary.Pack((p["entries"] as JsonArray ?? []).OfType<JsonObject>())) }, null);
            case "furnitureLibrary.unpack": return (new JsonArray([.. FurnitureLibrary.Unpack(Base64(p["data"])).Select(e => (JsonNode)e)]), null);
            case "templates.delete":
                Library().Delete((string?)p["id"] ?? throw new CommandException("Which template?"));
                return (null, null);
            case "templates.rename":
                return (new JsonObject { ["id"] = Library().Rename((string?)p["id"] ?? throw new CommandException("Which template?"), (string?)p["name"] ?? "") }, null);
            case "project.open":
            {
                var diff = files.Open(session, PathParam());
                return (new JsonObject { ["notes"] = new JsonArray(files.LastNotes.Select(n => (JsonNode)n).ToArray()), ["page"] = files.LastPage, ["absent"] = new JsonArray([.. session.AbsentModules().Select(x => (JsonNode)x)]) }, diff);
            }
            case "project.save": return (null, files.Save(session, (string?)p["path"] is { } savePath ? HostFiles.Check(savePath) : null));
            case "project.openBytes":
            {
                // A project from its bytes (a host without files: the browser), base64 in `data`.
                var name = (string?)p["name"] ?? "project.lapin";
                var diff = files.OpenBytes(session, name, Convert.FromBase64String((string?)p["data"] ?? throw new CommandException("The project's bytes are missing.")));
                return (new JsonObject { ["notes"] = new JsonArray(files.LastNotes.Select(n => (JsonNode)n).ToArray()), ["page"] = files.LastPage, ["absent"] = new JsonArray([.. session.AbsentModules().Select(x => (JsonNode)x)]) }, diff);
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
            case "profiles.readIfc":
            {
                // An IFC file's profiles as a library (docs/plan-profile-libraries.md), nothing kept: the interface keeps it. By its bytes or path.
                var (bytes, name, _, _) = IfcBytes(p);
                ReportProgress("Reading the profiles", 0, 0);
                var model = new IfcModel(StepFile.Read(bytes));
                var material = (string?)p["material"] is { Length: > 0 } pm && Lapin.Platform.ProfileLibraries.SectionProfile.Densities.ContainsKey(pm) ? pm : "steel";
                var read = model.ReadProfiles(material, name);
                if (read.Profiles.Count == 0) throw new CommandException(read.Notes.Count > 0 ? $"No profile could be read from the file. {string.Join(" ", read.Notes)}" : "The file holds no IFC profile.");
                var lib = new Lapin.Platform.ProfileLibraries.ProfileLibrary(Path.GetFileNameWithoutExtension(name), null, material, read.Profiles).ToJson(summaries: true);
                lib["skipped"] = read.Skipped;
                lib["notes"] = new JsonArray(read.Notes.Select(x => (JsonNode)x).ToArray());
                lib["checks"] = new JsonArray(read.Checks.Take(500).Select(x => (JsonNode)Lapin.Platform.ProfileLibraries.ProfileProps.CheckJson(x)).ToArray());
                lib["checkCount"] = read.Checks.Count;
                return (lib, null);
            }
            case "library.readFolder":
            {
                // A firm's folder of libraries (docs/plan-user-extensibility.md §4.2): every file of a library kind in it (or of the kinds
                // asked), with its kind, name, date and text (at most 16 MB each); the interface reads each through its kind's own importer.
                var folder = HostFiles.Check((string?)p["path"] ?? throw new CommandException("The folder's path is missing."));
                var kinds = p["kinds"] is JsonArray ka ? ka.Select(x => ((string?)x)?.TrimStart('.').ToLowerInvariant()).OfType<string>().Where(LibraryKinds.Contains).ToHashSet() : [.. LibraryKinds];
                var files = new JsonArray();
                var problems = new JsonArray();
                var exists = Directory.Exists(folder);
                if (!exists) problems.Add((JsonNode)$"The folder {folder} cannot be reached.");
                else
                    foreach (var file in Directory.EnumerateFiles(folder).Where(f => kinds.Contains(Path.GetExtension(f).TrimStart('.').ToLowerInvariant())).Order(StringComparer.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var info = new FileInfo(file);
                            if (info.Length > 16 * 1024 * 1024) { problems.Add((JsonNode)$"{info.Name}: larger than 16 MB."); continue; }
                            files.Add((JsonNode)new JsonObject
                            {
                                ["file"] = file, ["name"] = Path.GetFileNameWithoutExtension(file), ["kind"] = Path.GetExtension(file).TrimStart('.').ToLowerInvariant(),
                                ["modified"] = info.LastWriteTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), ["text"] = File.ReadAllText(file),
                            });
                        }
                        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { problems.Add((JsonNode)$"{Path.GetFileName(file)}: {e.Message}"); }
                    }
                return (new JsonObject { ["folder"] = folder, ["exists"] = exists, ["files"] = files, ["problems"] = problems }, null);
            }
            case "library.writeFolder":
            {
                // A library file written into a firm's folder ({path, name, kind, text}) through a temporary file, one of that name replaced.
                var folder = HostFiles.Check((string?)p["path"] ?? throw new CommandException("The folder's path is missing."));
                var kind = ((string?)p["kind"])?.TrimStart('.').ToLowerInvariant() is { } k && LibraryKinds.Contains(k) ? k : throw new CommandException($"A library file is one of {string.Join(", ", LibraryKinds.Select(x => "." + x))}.");
                var text = (string?)p["text"] ?? throw new CommandException("Nothing to write.");
                if (!Directory.Exists(folder)) throw new CommandException($"The folder {folder} cannot be reached.");
                var name = (string?)p["name"] ?? "library";
                var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '/' or '\\' or ':' ? '_' : c)).Trim();
                var target = Path.Combine(folder, $"{(safe.Length > 0 ? safe : "library")}.{kind}");
                try
                {
                    var temp = target + ".tmp";
                    File.WriteAllText(temp, text);
                    File.Move(temp, target, true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw new CommandException($"The file could not be written there: {e.Message}"); }
                return (new JsonObject { ["file"] = target }, null);
            }
            case "profiles.readFolder":
            {
                // A firm's folder of profile libraries (a local, network or synced one): each .lapinprofiles file in it; a bad file said, never fatal.
                var folder = HostFiles.Check((string?)p["path"] ?? throw new CommandException("The folder's path is missing."));
                var libraries = new JsonArray();
                var problems = new JsonArray();
                var exists = Directory.Exists(folder);
                if (!exists) problems.Add((JsonNode)$"The folder {folder} cannot be reached.");
                else
                    foreach (var file in Directory.EnumerateFiles(folder, $"*.{Lapin.Platform.ProfileLibraries.ProfileLibrary.Extension}").Order(StringComparer.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var lib = Lapin.Platform.ProfileLibraries.ProfileLibrary.From(JsonNode.Parse(File.ReadAllText(file))) ?? throw new InvalidDataException("not a profile library");
                            libraries.Add((JsonNode)new JsonObject { ["file"] = file, ["modified"] = File.GetLastWriteTime(file).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), ["library"] = lib.ToJson(summaries: true) });
                        }
                        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
                        {
                            problems.Add((JsonNode)$"{Path.GetFileName(file)}: {e.Message}");
                        }
                    }
                return (new JsonObject { ["folder"] = folder, ["exists"] = exists, ["libraries"] = libraries, ["problems"] = problems }, null);
            }
            case "profiles.writeFolder":
            {
                // A library written into a folder as <its name>.lapinprofiles (one of that name replaced), through a temporary file so a reader never sees half of it.
                var folder = HostFiles.Check((string?)p["path"] ?? throw new CommandException("The folder's path is missing."));
                var lib = Lapin.Platform.ProfileLibraries.ProfileLibrary.From(p["library"]) ?? throw new CommandException("No library was sent.");
                if (!Directory.Exists(folder)) throw new CommandException($"The folder {folder} cannot be reached.");
                var safe = string.Concat(lib.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '/' or '\\' or ':' ? '_' : c)).Trim();
                var target = Path.Combine(folder, $"{(safe.Length > 0 ? safe : "profiles")}.{Lapin.Platform.ProfileLibraries.ProfileLibrary.Extension}");
                try
                {
                    var temp = target + ".tmp";
                    File.WriteAllText(temp, lib.ToJson().ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                    File.Move(temp, target, true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw new CommandException($"The library could not be written there: {e.Message}"); }
                return (new JsonObject { ["file"] = target }, null);
            }
            case "profiles.exportIfc":
            {
                // A library (or the project's profiles) as an IFC4 profile library file.
                var lib = Lapin.Platform.ProfileLibraries.ProfileLibrary.From(p["library"]) ?? throw new CommandException("No library was sent.");
                if (lib.Profiles.Count == 0) throw new CommandException("The library has no profile to export.");
                return (Deliver(Lapin.Ifc.ProfileIfc.Write(lib), $"{lib.Name}.ifc"), null);
            }
            case "project.exportIfc":
            {
                // The project as IFC (docs/spec-ifc.md): IFC4, or IFC4X3; parts grouped under their element, or one body each; a phase, or everything;
                // bodies as B-reps or face sets, simplified within a tolerance (metres) or exact; the kinds, storeys and hidden left out (ModelSurvey).
                var exclude = Lapin.Ifc.ModelSurvey.Excluded(session, p);
                // `voids`: the openings also as solid voids for clash tools (PROVISIONFORVOID, decision 289), of `voidKinds` grown by `voidOffset` (the project's settings when not sent).
                var voidOptions = Lapin.Platform.Coordination.VoidOptions.From(session.Model, new JsonObject { ["kinds"] = p["voidKinds"]?.DeepClone(), ["offset"] = p["voidOffset"]?.DeepClone() });
                var options = new IfcExportOptions((string?)p["schema"] ?? "IFC4", (string?)p["detail"] ?? "group", (string?)p["phase"],
                    (string?)p["geometry"] ?? "brep", Math.Clamp(Json.Number(p["tolerance"]) ?? 0, 0, 0.1), exclude.Count > 0 ? exclude : null,
                    p["voids"] is JsonValue vv && vv.TryGetValue<bool>(out var withVoids) && withVoids, voidOptions.Kinds, voidOptions.Offset,
                    // `bonsaiStyles`: Bonsai's drawing styles (a stylesheet and its patterns) written beside the file, on the desktop (a path given).
                    p["bonsaiStyles"] is JsonValue bv && bv.TryGetValue<bool>(out var bonsai) && bonsai,
                    // `structural`: IFC's structural analysis view (the analytical model, the load cases, actions and combinations; docs/plan-fem.md §8.7).
                    p["structural"] is JsonValue sv && sv.TryGetValue<bool>(out var structural) && structural);
                if ((string?)p["path"] is { Length: > 0 } ifcPath) return (IfcExport.Write(session, ifcPath, options).ToJson(), null);
                using var ms = new MemoryStream();
                IfcExport.Write(session, ms, "project.ifc", options);
                return (Deliver(ms.ToArray(), "project.ifc"), null);
            }
            case "project.exportMesh": return ExportMesh(p, Deliver);
            // Lapin for the browser (decision 482): the project as one .html, a viewer and the package; what its dialog shows first.
            case "project.exportHtml": return ExportHtml(p, Deliver);
            case "project.htmlPlan": return HtmlPlan(p);
            case "ifc.optimize":
            {
                // An IFC file cleaned and optimised into a copy (docs/ifc-optimize.md): a pass from file to file, no project needed, the original
                // never written. By path (the desktop: the copy beside it or in `folder`, its name the original's with `suffix`; one already there is
                // overwritten only with `overwrite`, else the reply says it exists), or by its bytes (the browser: the copy comes back to download).
                var options = IfcOptimizeOptions.FromJson(p["options"] as JsonObject);
                var suffix = IfcOptimize.CleanSuffix((string?)p["suffix"]) ?? throw new CommandException("The copy needs an ending to tell it from the original.");
                using var cts = new CancellationTokenSource();
                _long = cts;
                try
                {
                    if ((string?)p["data"] is { } data)
                    {
                        var name = (string?)p["name"] ?? "model.ifc";
                        using var ms = new MemoryStream();
                        var r = IfcOptimize.Run(Convert.FromBase64String(data), ms, options, ReportProgress, cts.Token, name);
                        var o = r.ToJson();
                        o["name"] = Path.GetFileName(IfcOptimize.Target(name, suffix, null, options.Zip));
                        o["data"] = Convert.ToBase64String(ms.ToArray());
                        return (o, null);
                    }
                    var source = PathParam();
                    if (!File.Exists(source)) throw new CommandException($"There is no file at {source}.");
                    var folder = (string?)p["folder"] is { Length: > 0 } f ? HostFiles.Check(f) : null;
                    var target = IfcOptimize.Target(source, suffix, folder, options.Zip);
                    if (IfcOptimize.SamePath(source, target)) throw new CommandException("The copy would overwrite the original: choose another ending or folder.");
                    var overwrite = p["overwrite"] is JsonValue ov && ov.TryGetValue<bool>(out var yes) && yes;
                    if (File.Exists(target) && !overwrite)
                        return (new JsonObject { ["exists"] = true, ["target"] = target }, null);
                    if (folder != null) Directory.CreateDirectory(folder);
                    var report = IfcOptimize.RunFile(source, target, options, ReportProgress, cts.Token).ToJson();
                    report["source"] = source;
                    report["target"] = target;
                    return (report, null);
                }
                catch (OperationCanceledException) { throw new CommandException("Cancelled: nothing was written."); }
                catch (InvalidOperationException e) { throw new CommandException(e.Message); }
                finally { _long = null; }
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
                // `asProject`: the file opened as a project (the start screen's IFC, a file the app was started for): a new project made from its
                // spatial structure, its project, site, building and storeys under the file's GlobalIds, then the import into it (docs/spec-ifc.md, Structura spațială).
                var asProject = modeName != "link" && p["asProject"] is JsonValue apv && apv.TryGetValue<bool>(out var ap) && ap;
                Diff? start = null;
                if (asProject) { files.Reset(); start = session.New(IfcImport.SeedOf(file)); }
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
                        result = modeName == "link" ? IfcImport.Link(session, file, name, path, mode, out imported) : IfcImport.Run(session, file, name, mode with { Source = path }, out imported);
                    }
                    finally { session.EditSource = null; }
                }
                return (result.ToJson(), start?.Then(imported) ?? imported);
            }
            case "project.exportSurvey":
                // The project's elements and storeys as an import survey shows a file's, for an export's choice.
                return (Lapin.Ifc.ModelSurvey.Of(session), null);
            case "project.fragSurvey": return FragSurvey(p);
            case "project.importFragments": return ImportFragments(p);
            case "project.bosSurvey": return BosSurvey(p);
            case "project.importBos": return ImportBos(p);
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
                // References made native (decision 214): recognised from their triangles where a module takes them (a wall with its
                // doors and windows, a slab, a column), the rest made Studio solids when editable is asked; one undo step.
                var ids = (p["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [];
                if (ids.Count == 0) throw new CommandException("Select the references to convert.");
                // A program's bodies stay its own (docs/plan-external-geometry.md): left out, or refused when nothing else is asked.
                var own = ids.Where(id => session.Model.Get(id) is { } x && ExternalBodies.IsOwned(x)).ToHashSet();
                if (own.Count > 0 && own.Count == ids.Count) throw new CommandException(ExternalBodies.Refused);
                ids = ids.Where(id => !own.Contains(id)).ToList();
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
            case "project.importMesh": return ImportMesh(p);
            case "project.linkLapin":
            {
                // Another Lapin project linked in, read-only, placed where asked (docs/links.md, Hotlinks); by path, or its bytes in a browser.
                ReportProgress("Reading the project", 0, 0);
                var (bytes, name, path, _) = IfcBytes(p);
                var r = HotLinks.Link(session, bytes, name, path, new HotLinks.Placing(Json.Number(p["x"]) ?? 0, Json.Number(p["y"]) ?? 0, Json.Number(p["rotation"]) ?? 0, (bool?)p["mirror"] ?? false), out var linked);
                return (r.ToJson(), linked);
            }
            case "project.linksChanged":
                // The linked projects whose files changed since they were read: Manage → Links says so, and a click reads them again.
                return (new JsonArray([.. session.Model.OfClass(Links.Class).Where(HotLinks.Changed).Select(l => (JsonNode)l.Id)]), null);
            case "project.reloadLink":
            {
                // A link read again from its file (the desktop: a link made from a path): what it held goes, what the file holds now comes in its place.
                var link = Links.Require(session.Model, (string?)p["id"]);
                // A linked project: read again, or placed anew (x, y, rotation); from its file, or from the bytes given (a browser keeps no path).
                if (link.Str("kind") == HotLinks.Kind && (p["data"] != null || p["x"] != null || p["y"] != null || p["rotation"] != null || p["mirror"] != null))
                {
                    var was = HotLinks.Placing.Of(link.Attrs["options"] as JsonObject);
                    var place = new HotLinks.Placing(Json.Number(p["x"]) ?? was.X, Json.Number(p["y"]) ?? was.Y, Json.Number(p["rotation"]) ?? was.Degrees, (bool?)p["mirror"] ?? was.Mirrored);
                    var from = (string?)p["data"] is { } data ? Convert.FromBase64String(data)
                        : link.Str("path") is { } known && File.Exists(HostFiles.Check(known)) ? File.ReadAllBytes(known)
                        : throw new CommandException("This link's file cannot be read here: choose it again.");
                    var moved = HotLinks.Link(session, from, link.Str("file") ?? link.Name, link.Str("path"), place, out var movedDiff, link.Id);
                    return (moved.ToJson(), movedDiff);
                }
                var path = HostFiles.Check(link.Str("path") ?? throw new CommandException("This link has no file to read again (it was linked from the browser)."));
                if (!File.Exists(path)) throw new CommandException($"The file is gone: {path}.");
                if (link.Str("kind") == "dxf")
                {
                    var args = (link.Attrs["options"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
                    args["path"] = path; args["link"] = true; args["linkId"] = link.Id;
                    if (link.Str("viewId") is { } v) args["viewId"] = v;
                    var d = session.Execute("drafting.importDxf", args, out var dr);
                    return (dr, d);
                }
                if (link.Str("kind") == HotLinks.Kind)
                {
                    var h = HotLinks.Link(session, File.ReadAllBytes(path), Path.GetFileName(path), path, HotLinks.Placing.Of(link.Attrs["options"] as JsonObject), out var again, link.Id);
                    return (h.ToJson(), again);
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
                    : Views.Find(session.Model, id) is { } view ? Views.Dxf(session.Reader, view, (string?)p["coords"] ?? "internal")
                    : throw new CommandException("There is no such sheet or view.");
                return (Deliver(bytes, "drawing.dxf"), null);
            }
            // Bytes the interface made (a schedule's CSV or XLSX, exported types) written where the desktop's save dialog says (report #66).
            case "file.write":
            {
                var path = PathParam();
                var data = (string?)p["data"] ?? throw new CommandException("Nothing to write.");
                byte[] bytes;
                try { bytes = Convert.FromBase64String(data); }
                catch (FormatException) { throw new CommandException("The file's content is not base64."); }
                File.WriteAllBytes(path, bytes);
                return (new JsonObject { ["path"] = path, ["bytes"] = bytes.Length }, null);
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
                return (CdeDeliverables.Publish(session.Model, files.Issues(session), (string?)p["id"], (string?)p["folder"] is { } publishTo ? HostFiles.Check(publishTo) : null, p["flat"]?.GetValue<bool>() == true), null);
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
                (support ?? throw new CommandException("Nothing to open here.")).Open((string?)p["link"], p);
                return (null, null);
            case "recovery.list": return (recovery?.List() ?? [], null);
            case "recovery.restore": files.ForgetPage(); return (null, (recovery ?? throw new CommandException("No recovery here.")).Restore(PathParam()));
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
