using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Host;

/// <summary>
/// The engine protocol (<see cref="EngineDispatcher"/>) over WebSockets: the
/// interface sends JSON text, the engine answers in binary frames, and pushes
/// the diff of every change to every interface it serves. Calls run one at a
/// time: the session is single-threaded by design.
/// </summary>
public sealed class EngineHub(Session session, ProjectFiles files, Recovery? recovery = null)
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly List<Client> _clients = [];
    readonly EngineDispatcher _dispatcher = new(session, files, recovery, new SupportDesk());

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
        var frame = _dispatcher.DiffFrame(diff);
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

    /// <summary>One call: its reply as a frame. The diff it caused has been broadcast before.</summary>
    public async Task<byte[]> Handle(string text)
    {
        await _gate.WaitAsync();
        try
        {
            var (reply, diff) = _dispatcher.Handle(text);
            if (diff != null) await Broadcast(diff);
            return reply;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The help menu's reports and links, sent and opened from this computer.</summary>
    sealed class SupportDesk : ISupportDesk
    {
        public JsonObject Report(JsonObject p) => Support.Report(p, typeof(EngineHub).Assembly.GetName().Version?.ToString() ?? "0").GetAwaiter().GetResult();
        public void Open(string? link) => Support.Open(link);
    }
}
