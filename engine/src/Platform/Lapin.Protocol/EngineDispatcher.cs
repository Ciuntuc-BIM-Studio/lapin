using System.Text.Json;
using System.Text.Json.Nodes;
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

/// <summary>What only a host can do for the help menu: send a report, open one of its links.</summary>
public interface ISupportDesk
{
    JsonObject Report(JsonObject p);
    void Open(string? link);
}

/// <summary>
/// Opening and saving projects: the host's file access by path, given to the
/// dispatcher so the protocol stays testable; and by bytes, for a host without
/// files of its own (the browser: the user picks a file, keeps a download).
/// It holds the open project's versions (docs/versions.md): read with the
/// file, carried into every save, and changed by the version calls, each of
/// which writes the file (a version lives in the project's file).
/// </summary>
public sealed class ProjectFiles(Func<Session, string, Diff> open, Action<Session, string, PackageVersions> write)
{
    PackageVersions _versions = PackageVersions.None;
    /// <summary>The session path the versions belong to: another path (a recovered project's) has them read from its file.</summary>
    string? _versionsOf;

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
        return diff;
    }

    /// <summary>The project as a package's bytes; the session counts as saved.</summary>
    public (byte[] Bytes, Diff Diff) SaveBytes(Session session, string name)
    {
        var versions = Versions(session);
        using var ms = new MemoryStream();
        ProjectPackage.Write(session, ms, versions);
        var bytes = ms.ToArray();
        var diff = session.Saved(name);
        Keep(name, versions.From(bytes));
        return (bytes, diff);
    }

    /// <summary>What the last opening left out (an old project's parts that do not come over), for the interface to tell.</summary>
    public List<string> LastNotes { get; set; } = [];

    public Diff Open(Session session, string path)
    {
        var diff = open(session, path);
        Keep(path, PackageVersions.Of(path));
        return diff;
    }

    public Diff Save(Session session, string? path)
    {
        var target = path ?? session.Path ?? throw new CommandException("Where to save: the project has no file yet.");
        var versions = Versions(session);
        write(session, target, versions);
        var diff = session.Saved(target);
        Keep(session.Path ?? target, versions.From(session.Path ?? target));
        return diff;
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
        if (asBytes)
        {
            var name = session.Path ?? "project.lapin";
            using var ms = new MemoryStream();
            LapinPackage.Write(ms, build, versions);
            var bytes = ms.ToArray();
            var diff = session.Saved(name);
            Keep(name, versions.From(bytes));
            result["name"] = name;
            result["data"] = Convert.ToBase64String(bytes);
            return (result, diff);
        }
        var path = session.Path ?? throw new CommandException("Save the project first: its versions live in its file.");
        LapinPackage.Write(path, build, versions);
        var saved = session.Saved(path);
        Keep(path, versions.From(path));
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
        return session.Edit($"Restore version {number}", edit => ModelCompare.Apply(edit, content.Entities, content.Relations));
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

    /// <summary>One call: its reply frame, and the diff it caused (null: nothing changed).</summary>
    public (byte[] Reply, Diff? Diff) Handle(string text)
    {
        JsonNode? id = null;
        try
        {
            var msg = JsonNode.Parse(text) as JsonObject ?? throw new CommandException("Bad message.");
            id = msg["id"]?.DeepClone();
            var method = (string?)msg["method"] ?? throw new CommandException("No method.");
            var p = msg["params"] as JsonObject ?? [];
            var (result, diff) = Dispatch(method, p);
            return (BinaryFrame.Encode(new Reply(id, result), ContractsOnly), diff);
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
            case "doc.undo": return (null, session.Undo());
            case "doc.redo": return (null, session.Redo());
            case "doc.query":
                return (session.Query((string?)p["name"] ?? throw new CommandException("The query is missing."), p["args"] as JsonObject ?? [])?.DeepClone(), null);
            case "quantities.of": return (session.Quantities((string?)p["id"] ?? throw new CommandException("Which element?")), null);
            // A new project with the default storeys; `plain`: one ground floor, for a file that fills it (an IFC opened as a project).
            case "project.new": return (null, session.New(p["plain"]?.GetValue<bool>() == true));
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
                // The project as IFC (docs/spec-ifc.md): IFC4, or IFC4X3; parts grouped under their element, or one body each; a phase, or everything.
                var options = new IfcExportOptions((string?)p["schema"] ?? "IFC4", (string?)p["detail"] ?? "group", (string?)p["phase"]);
                if ((string?)p["path"] is { Length: > 0 } ifcPath) return (IfcExport.Write(session, ifcPath, options).ToJson(), null);
                using var ms = new MemoryStream();
                IfcExport.Write(session, ms, "project.ifc", options);
                return (Deliver(ms.ToArray(), "project.ifc"), null);
            }
            case "project.importIfc":
            {
                // An IFC file into the open project (docs/spec-ifc.md): Lapin's own files natively, others as references; "reference" keeps everything as it is;
                // "link" brings it in read-only, as a link (Manage → Links).
                var modeName = (string?)p["mode"] ?? "auto";
                var mode = new IfcImportOptions(modeName == "link" ? "auto" : modeName);
                // By path (the desktop), or its bytes, base64 in `data` (the browser).
                IfcImportResult result;
                Diff imported;
                if (modeName == "link")
                {
                    if ((string?)p["data"] is { } linkData) result = IfcImport.Link(session, Convert.FromBase64String(linkData), (string?)p["name"] ?? "file.ifc", null, mode, out imported);
                    else { var path = PathParam(); result = IfcImport.Link(session, File.ReadAllBytes(path), Path.GetFileName(path), path, mode, out imported); }
                }
                else if ((string?)p["data"] is { } data) result = IfcImport.Run(session, Convert.FromBase64String(data), (string?)p["name"] ?? "file.ifc", mode, out imported);
                else result = IfcImport.Run(session, PathParam(), mode, out imported);
                return (result.ToJson(), imported);
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
            case "support.report": return ((support ?? throw new CommandException("Reports are sent from the app.")).Report(p), null);
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
}
