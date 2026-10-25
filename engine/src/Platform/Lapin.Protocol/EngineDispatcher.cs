using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Model;
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

/// <summary>Opening and saving projects: the host's file access, given to the dispatcher so the protocol stays testable.</summary>
public sealed class ProjectFiles(Func<Session, string, Diff> open, Action<Session, string> write)
{
    /// <summary>What the last opening left out (an old project's parts that do not come over), for the interface to tell.</summary>
    public List<string> LastNotes { get; set; } = [];

    public Diff Open(Session session, string path) => open(session, path);

    public Diff Save(Session session, string? path)
    {
        var target = path ?? session.Path ?? throw new CommandException("Where to save: the project has no file yet.");
        write(session, target);
        return session.Saved(target);
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
            case "project.new": return (null, session.New());
            case "project.open":
            {
                var diff = files.Open(session, PathParam());
                return (new JsonObject { ["notes"] = new JsonArray(files.LastNotes.Select(n => (JsonNode)n).ToArray()) }, diff);
            }
            case "project.save": return (null, files.Save(session, (string?)p["path"]));
            case "project.exportIfc":
            {
                // The project as IFC (docs/spec-ifc.md): IFC4, or IFC4X3; parts grouped under their element, or one body each; a phase, or everything.
                var options = new IfcExportOptions((string?)p["schema"] ?? "IFC4", (string?)p["detail"] ?? "group", (string?)p["phase"]);
                if ((string?)p["path"] is { Length: > 0 } ifcPath) return (IfcExport.Write(session, ifcPath, options).ToJson(), null);
                var temp = Path.Combine(Path.GetTempPath(), $"lapin-{Guid.NewGuid():N}.ifc");
                try { IfcExport.Write(session, temp, options); return (Deliver(File.ReadAllBytes(temp), "project.ifc"), null); }
                finally { File.Delete(temp); }
            }
            case "project.importIfc":
            {
                // An IFC file into the open project (docs/spec-ifc.md): Lapin's own files natively, others as references; "reference" keeps everything as it is.
                var result = IfcImport.Run(session, PathParam(), new IfcImportOptions((string?)p["mode"] ?? "auto"), out var imported);
                return (result.ToJson(), imported);
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
