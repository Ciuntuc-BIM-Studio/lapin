using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Host;

/// <summary>
/// The engine protocol, over one WebSocket. The interface sends JSON text:
///   → {"id":1,"method":"doc.execute","params":{"command":"architecture.addWall","args":{…}}}
/// The engine answers in binary frames (<see cref="BinaryFrame"/>: a JSON
/// header, meshes as raw arrays):
///   ← {"id":1,"result":…}            or  {"id":1,"error":"…"}
///   ← {"event":"diff","data":Diff}   after anything that changed the document
/// Calls run one at a time: the session is single-threaded by design.
/// </summary>
public sealed class EngineHub(Session session, ProjectFiles files, Recovery? recovery = null)
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly List<Client> _clients = [];

    /// <summary>A connected interface: its socket, and the turn to send on it (a reply and another client's broadcast may be due together).</summary>
    sealed class Client(WebSocket socket)
    {
        public WebSocket Socket => socket;
        public SemaphoreSlim Sending { get; } = new(1, 1);
    }

    public static bool AllowedOrigin(string origin) =>
        origin.StartsWith("http://localhost:", StringComparison.Ordinal)
        || origin.StartsWith("http://127.0.0.1:", StringComparison.Ordinal)
        || origin is "tauri://localhost" or "http://tauri.localhost" or "https://tauri.localhost";

    public async Task Serve(WebSocket ws, CancellationToken ct)
    {
        var client = new Client(ws);
        lock (_clients) _clients.Add(client);
        try
        {
            var buffer = new byte[1 << 16];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(buffer, ct);
                    if (r.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buffer, 0, r.Count);
                } while (!r.EndOfMessage);

                var reply = await Handle(Encoding.UTF8.GetString(ms.ToArray()));
                await Send(client, reply);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            lock (_clients) _clients.Remove(client);
        }
    }

    /// <summary>One frame at a time on a socket: the next waits for its turn instead of overlapping.</summary>
    static async Task Send(Client c, byte[] frame)
    {
        await c.Sending.WaitAsync();
        try
        {
            if (c.Socket.State == WebSocketState.Open) await c.Socket.SendAsync(frame, WebSocketMessageType.Binary, true, CancellationToken.None);
        }
        finally
        {
            c.Sending.Release();
        }
    }

    async Task Broadcast(Diff diff)
    {
        var frame = BinaryFrame.Encode(new DiffEvent("diff", diff));
        Client[] clients;
        lock (_clients) clients = [.. _clients];
        foreach (var c in clients)
        {
            try { await Send(c, frame); }
            catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
            {
                // A peer gone or going, which its own Serve ends: the caller's command applied, and the others still get the diff.
            }
        }
    }

    sealed record DiffEvent(string Event, Diff Data);
    sealed record Reply(JsonNode? Id, object? Result, string? Error = null);

    /// <summary>One call: its reply as a frame. The diff it caused has been broadcast before.</summary>
    public async Task<byte[]> Handle(string text)
    {
        JsonNode? id = null;
        await _gate.WaitAsync();
        try
        {
            var msg = JsonNode.Parse(text) as JsonObject ?? throw new CommandException("Bad message.");
            id = msg["id"]?.DeepClone();
            var method = (string?)msg["method"] ?? throw new CommandException("No method.");
            var p = msg["params"] as JsonObject ?? [];
            var (result, diff) = Dispatch(method, p);
            if (diff != null) await Broadcast(diff);
            return BinaryFrame.Encode(new Reply(id, result));
        }
        catch (Exception ex) when (ex is ModelException or JsonException or InvalidOperationException or IOException or UnauthorizedAccessException or FormatException or InvalidDataException)
        {
            return BinaryFrame.Encode(new Reply(id, null, ex.Message));
        }
        catch (Exception ex)
        {
            // An engine bug: answer the call (the UI must not hang) and leave a trace.
            Console.Error.WriteLine($"[engine] {ex}");
            return BinaryFrame.Encode(new Reply(id, null, $"Internal engine error: {ex.Message}"));
        }
        finally
        {
            _gate.Release();
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
                return (new JsonObject { ["notes"] = new JsonArray(PackageFiles.LastNotes.Select(n => (JsonNode)n).ToArray()) }, diff);
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
            case "support.report":
                return (Support.Report(p, typeof(EngineHub).Assembly.GetName().Version?.ToString() ?? "0").GetAwaiter().GetResult(), null);
            case "support.open":
                Support.Open((string?)p["link"]);
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

/// <summary>Opening and saving projects: the host's only file access, given to the hub so the protocol stays testable.</summary>
public sealed class ProjectFiles(Func<Session, string, Diff> open, Action<Session, string> write)
{
    public Diff Open(Session session, string path) => open(session, path);

    public Diff Save(Session session, string? path)
    {
        var target = path ?? session.Path ?? throw new CommandException("Where to save: the project has no file yet.");
        write(session, target);
        return session.Saved(target);
    }
}
