using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Host;

/// <summary>
/// The engine protocol (<see cref="EngineDispatcher"/>) over WebSockets: the
/// interface sends JSON text, the engine answers in binary frames, and pushes
/// the diff of every change to every interface it serves. Calls run one at a
/// time: the session is single-threaded by design.
/// <para>
/// Its clients are the computer's own interfaces (the window, its detached
/// views, the MCP bridge) and, while the project is shared
/// (docs/plan-multiplayer.md, <see cref="Sharing"/>), the guests: each a
/// participant with a name, a colour and a role, whose calls are theirs (their
/// own undo) and limited to what a guest may do. Presence (where each is,
/// what each has selected) goes round as text events, outside the history.
/// </para>
/// </summary>
public sealed class EngineHub
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly List<Client> _clients = [];
    readonly EngineDispatcher _dispatcher;
    readonly Session _session;

    public EngineHub(Session session, ProjectFiles files, Recovery? recovery = null)
    {
        _session = session;
        _dispatcher = new(session, files, recovery, new SupportDesk());
        Sharing = new Sharing(this);
        session.History.AuthorName = id => Participant(id)?.Name;
    }

    public Session Session => _session;
    public Sharing Sharing { get; }

    /// <summary>
    /// One connected interface: who it is, and how a frame reaches it. What the hub sends it waits in its own queue,
    /// sent in order by its own loop (<see cref="Post"/>), so a client that stops reading holds up only itself; one
    /// that falls too far behind is dropped (<see cref="Drop"/>), its frames with it.
    /// </summary>
    public abstract class Client
    {
        /// <summary>The most frames, and bytes, a client may have waiting before it is dropped.</summary>
        public const int MaxFrames = 4096;
        public const long MaxBytes = 1L << 30;

        readonly Channel<Func<Task>> _out = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });
        long _waitingBytes;
        int _waitingFrames;
        int _dropped;
        Task? _pump;

        public Caller Caller { get; set; } = Caller.Local;
        /// <summary>The participant it shows (null: a local window that has not said who it is, a detached view).</summary>
        public Participant? Participant { get; set; }
        public abstract Task SendBinary(byte[] frame);
        public abstract Task SendText(string text);

        /// <summary>The client fell behind: its connection is ended, and its own loop removes it.</summary>
        protected abstract void Drop();

        /// <summary>Whether it was dropped for falling behind.</summary>
        public bool Dropped => Volatile.Read(ref _dropped) != 0;

        /// <summary>A frame sent after those posted before it, without waiting for the peer to take it.</summary>
        public void Post(byte[] frame) => Post(() => SendBinary(frame), frame.Length);
        public void Post(string text) => Post(() => SendText(text), text.Length);

        void Post(Func<Task> send, long size)
        {
            if (Dropped) return;
            if (Interlocked.Increment(ref _waitingFrames) > MaxFrames || Interlocked.Add(ref _waitingBytes, size) > MaxBytes)
            {
                if (Interlocked.Exchange(ref _dropped, 1) == 0)
                {
                    Console.Error.WriteLine("[engine] A client fell behind and was let go.");
                    _out.Writer.TryComplete();
                    Drop();
                }
                return;
            }
            _out.Writer.TryWrite(async () =>
            {
                try { await Safe(send()); }
                finally { Interlocked.Decrement(ref _waitingFrames); Interlocked.Add(ref _waitingBytes, -size); }
            });
            if (_pump == null) lock (_out) _pump ??= Task.Run(Pump);
        }

        async Task Pump()
        {
            await foreach (var send in _out.Reader.ReadAllAsync()) await send();
        }

        /// <summary>The client gone: nothing more is posted to it; what is queued still goes, as far as it can.</summary>
        public void Complete() => _out.Writer.TryComplete();

        /// <summary>What is queued, sent (tests; a client that ends).</summary>
        public Task Flushed()
        {
            var done = new TaskCompletionSource();
            if (!_out.Writer.TryWrite(() => { done.TrySetResult(); return Task.CompletedTask; })) return Task.CompletedTask;
            if (_pump == null) lock (_out) _pump ??= Task.Run(Pump);
            return done.Task;
        }
    }

    /// <summary>A local interface on its WebSocket, one frame at a time (a reply and another client's broadcast may be due together).</summary>
    sealed class SocketClient(WebSocket socket) : Client
    {
        readonly SemaphoreSlim _sending = new(1, 1);
        public override Task SendBinary(byte[] frame) => Send(frame, WebSocketMessageType.Binary);
        public override Task SendText(string text) => Send(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text);
        protected override void Drop() => socket.Abort();

        async Task Send(byte[] bytes, WebSocketMessageType type)
        {
            await _sending.WaitAsync();
            try { if (socket.State == WebSocketState.Open) await socket.SendAsync(bytes, type, true, CancellationToken.None); }
            finally { _sending.Release(); }
        }
    }

    public static bool AllowedOrigin(string origin) =>
        origin.StartsWith("http://localhost:", StringComparison.Ordinal)
        || origin.StartsWith("http://127.0.0.1:", StringComparison.Ordinal)
        || origin is "tauri://localhost" or "http://tauri.localhost" or "https://tauri.localhost";

    public async Task Serve(WebSocket ws, CancellationToken ct)
    {
        var client = new SocketClient(ws);
        Add(client);
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

                if (await Handle(Encoding.UTF8.GetString(ms.ToArray()), client) is { } reply) client.Post(reply);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            await Remove(client);
        }
    }

    public void Add(Client c) { lock (_clients) _clients.Add(c); }

    public async Task Remove(Client c)
    {
        bool had;
        lock (_clients) had = _clients.Remove(c);
        c.Complete();
        if (had && c.Participant != null) await BroadcastPresence();
    }

    Client[] Clients() { lock (_clients) return [.. _clients]; }

    /// <summary>A send that may fail because its peer is gone (its own loop ends it): the others still get theirs.</summary>
    internal static async Task Safe(Task send)
    {
        try { await send; }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or InvalidOperationException or OperationCanceledException) { }
    }

    /// <summary>A diff to every client, queued for each (<see cref="Client.Post"/>): no client's pace holds up the next call.</summary>
    void Broadcast(Diff diff)
    {
        // Each author's own undo and redo in their frame; one frame per author.
        foreach (var part in _dispatcher.Parts(diff))
        {
            var frames = new Dictionary<string, byte[]>();
            foreach (var c in Clients())
            {
                var author = c.Caller.Author;
                if (!frames.TryGetValue(author, out var frame)) frames[author] = frame = _dispatcher.DiffFrame(part, author);
                c.Post(frame);
            }
        }
    }

    /// <summary>One call from a local interface: its reply as a frame (null when it asks none). The diff it caused has been broadcast before.</summary>
    public Task<byte[]?> Handle(string text) => Handle(text, null);

    /// <summary>
    /// One call from <paramref name="client"/>: the session's own (share.*,
    /// presence.*) here, the rest through the dispatcher, as the client's
    /// caller. Null: nothing to answer (presence is fire and forget).
    /// </summary>
    public async Task<byte[]?> Handle(string text, Client? client)
    {
        var caller = client?.Caller ?? Caller.Local;
        if (Own(text) is { } own) return await HandleOwn(own, client, caller);
        await _gate.WaitAsync();
        try
        {
            // A long call's progress (an import's stages) goes to the local interface that asked, as text events, while the call runs.
            _dispatcher.Progress = client is SocketClient asking ? o => asking.Post(new JsonObject { ["event"] = "progress", ["data"] = o }.ToJsonString()) : null;
            var (reply, diff) = _dispatcher.Handle(text, caller);
            _dispatcher.Progress = null;
            // A big snapshot's first parts, to the one who asked, before the reply that carries its last.
            foreach (var frame in _dispatcher.TakeCallerFrames()) client?.Post(frame);
            if (diff != null) Broadcast(diff);
            return reply;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>A share.*, presence.* or cancel.* call, parsed; null for any other. They are answered at once, past the queue of calls (a cancel stops the one running).</summary>
    static JsonObject? Own(string text)
    {
        // Cheap test first: every other call goes on untouched.
        if (!text.Contains("\"share.", StringComparison.Ordinal) && !text.Contains("\"presence.", StringComparison.Ordinal) && !text.Contains("\"cancel.", StringComparison.Ordinal)) return null;
        try
        {
            return JsonNode.Parse(text) is JsonObject o && (string?)o["method"] is { } m
                && (m.StartsWith("share.", StringComparison.Ordinal) || m.StartsWith("presence.", StringComparison.Ordinal) || m.StartsWith("cancel.", StringComparison.Ordinal)) ? o : null;
        }
        catch (System.Text.Json.JsonException) { return null; }
    }

    async Task<byte[]?> HandleOwn(JsonObject msg, Client? client, Caller caller)
    {
        var id = msg["id"]?.DeepClone();
        var method = (string)msg["method"]!;
        var p = msg["params"] as JsonObject ?? [];
        try
        {
            object? result;
            switch (method)
            {
                case "presence.update":
                    if (client?.Participant is { } me) { me.Presence = (JsonObject)p.DeepClone(); await BroadcastPresence(); }
                    return null;
                case "presence.hello":
                    // The window says who works at this computer: it shows as the host among the participants.
                    if (client != null && !caller.Remote)
                    {
                        client.Participant = Sharing.Host;
                        if ((string?)p["name"] is { Length: > 0 } name) Sharing.Host.Name = name.Trim();
                        await BroadcastPresence();
                    }
                    return null;
                case "cancel.long":
                    // The long call running (an IFC file being optimised) stopped: this computer's own interface only.
                    if (caller.Remote) throw new CommandException("Only the host can do that.");
                    _dispatcher.CancelLong();
                    result = new JsonObject { ["cancelled"] = true };
                    break;
                default:
                    if (caller.Remote) throw new CommandException("Only the host can do that.");
                    result = await Sharing.Handle(method, p);
                    break;
            }
            return BinaryFrame.Encode(new Reply(id, result is JsonNode n ? n : result));
        }
        catch (Exception ex) when (ex is CommandException or ModelException or WebSocketException or InvalidOperationException or HttpRequestException or IOException)
        {
            return id == null ? null : BinaryFrame.Encode(new Reply(id, null, ex.Message));
        }
    }

    /// <summary>The participants now: the host and each guest connected, with where each is.</summary>
    public JsonObject PresenceJson()
    {
        var seen = new HashSet<Participant>();
        var list = new JsonArray();
        foreach (var c in Clients())
            if (c.Participant is { } p && seen.Add(p)) list.Add(p.ToJson());
        return new JsonObject { ["participants"] = list, ["sharing"] = Sharing.Active };
    }

    public Participant? Participant(string id) =>
        id == History.Me ? Sharing.Host : Clients().Select(c => c.Participant).FirstOrDefault(p => p?.Id == id);

    public Task BroadcastPresence()
    {
        var text = new JsonObject { ["event"] = "presence", ["data"] = PresenceJson() }.ToJsonString();
        foreach (var c in Clients()) c.Post(text);
        return Task.CompletedTask;
    }

    /// <summary>An event to the computer's own interfaces (the sharing's state, for the window's dialog and bar).</summary>
    public Task TellLocal(string name, JsonNode data)
    {
        var text = new JsonObject { ["event"] = name, ["data"] = data }.ToJsonString();
        foreach (var c in Clients().Where(c => !c.Caller.Remote)) c.Post(text);
        return Task.CompletedTask;
    }

    /// <summary>The help menu's reports and links, sent and opened from this computer.</summary>
    sealed class SupportDesk : ISupportDesk
    {
        public JsonObject Report(JsonObject p) => Support.Report(p, Session.AppVersion).GetAwaiter().GetResult();
        public JsonObject Messages(JsonObject p) => Support.Messages(p).GetAwaiter().GetResult();
        public void Open(string? link) => Support.Open(link);
    }
}

/// <summary>
/// Someone in a shared session: the host (the computer's own user) or a
/// guest. Their id is the author of their edits; their presence (the view
/// they look at, the cursor, the selection, the camera) is theirs to send.
/// </summary>
public sealed class Participant(string id, string name, string color, string role)
{
    public string Id { get; } = id;
    public string Name { get; set; } = name;
    public string Color { get; } = color;
    public string Role { get; set; } = role;
    /// <summary>What a guest who reconnects shows to be the same person (their undo, their colour).</summary>
    public string Token { get; init; } = "";
    public JsonObject Presence { get; set; } = [];

    public JsonObject ToJson() => new()
    {
        ["id"] = Id, ["name"] = Name, ["color"] = Color, ["role"] = Role, ["host"] = Id == History.Me,
        ["presence"] = Presence.DeepClone(),
    };
}
