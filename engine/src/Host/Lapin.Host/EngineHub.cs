using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Host;

/// <summary>
/// The engine protocol: JSON messages over one WebSocket, as in BubbleBIM Pro.
///   → {"id":1,"method":"doc.execute","params":{"command":"architecture.addWall","args":{…}}}
///   ← {"id":1,"result":…}            or  {"id":1,"error":"…"}
///   ← {"event":"diff","data":Diff}   after anything that changed the document
/// Calls run one at a time: the session is single-threaded by design.
/// </summary>
public sealed class EngineHub(Session session, ProjectFiles files)
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

    static async Task Send(WebSocket ws, JsonObject msg)
    {
        if (ws.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(msg.ToJsonString(Json.Options));
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    async Task Broadcast(Diff diff)
    {
        var msg = new JsonObject { ["event"] = "diff", ["data"] = JsonSerializer.SerializeToNode(diff, Json.Options) };
        WebSocket[] clients;
        lock (_clients) clients = [.. _clients];
        foreach (var c in clients) await Send(c, msg);
    }

    public async Task<JsonObject> Handle(string text)
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
            return new JsonObject { ["id"] = id, ["result"] = result };
        }
        catch (Exception ex) when (ex is ModelException or JsonException or InvalidOperationException or IOException or UnauthorizedAccessException or FormatException or InvalidDataException)
        {
            return new JsonObject { ["id"] = id, ["error"] = ex.Message };
        }
        catch (Exception ex)
        {
            // An engine bug: answer the call (the UI must not hang) and leave a trace.
            Console.Error.WriteLine($"[engine] {ex}");
            return new JsonObject { ["id"] = id, ["error"] = $"Internal engine error: {ex.Message}" };
        }
        finally
        {
            _gate.Release();
        }
    }

    (JsonNode? Result, Diff? Diff) Dispatch(string method, JsonObject p)
    {
        static JsonNode? J<T>(T v) => JsonSerializer.SerializeToNode(v, Json.Options);
        string PathParam() => (string?)p["path"] ?? throw new CommandException("The file's path is missing.");

        switch (method)
        {
            case "session.info": return (J(session.Info()), null);
            case "doc.snapshot": return (J(session.Snapshot()), null);
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
            case "project.new": return (null, session.New());
            case "project.open": return (null, files.Open(session, PathParam()));
            case "project.save": return (null, files.Save(session, (string?)p["path"]));
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
