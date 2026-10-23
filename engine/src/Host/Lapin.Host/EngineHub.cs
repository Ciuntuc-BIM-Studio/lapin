using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    readonly List<WebSocket> _clients = [];

    public static bool AllowedOrigin(string origin) =>
        origin.StartsWith("http://localhost:", StringComparison.Ordinal)
        || origin.StartsWith("http://127.0.0.1:", StringComparison.Ordinal)
        || origin is "tauri://localhost" or "http://tauri.localhost" or "https://tauri.localhost";

    public async Task Serve(WebSocket ws, CancellationToken ct)
    {
        lock (_clients) _clients.Add(ws);
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
                await Send(ws, reply);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            lock (_clients) _clients.Remove(ws);
        }
    }

    static async Task Send(WebSocket ws, byte[] frame)
    {
        if (ws.State != WebSocketState.Open) return;
        await ws.SendAsync(frame, WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    async Task Broadcast(Diff diff)
    {
        var frame = BinaryFrame.Encode(new DiffEvent("diff", diff));
        WebSocket[] clients;
        lock (_clients) clients = [.. _clients];
        foreach (var c in clients) await Send(c, frame);
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
            case "project.open": return (null, files.Open(session, PathParam()));
            case "project.save": return (null, files.Save(session, (string?)p["path"]));
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
