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

    /// <param name="userDir">The user's folder on this computer (the desktop's app data folder): the project templates live in its <c>templates</c>, my furniture in its <c>furniture</c>.</param>
    public EngineHub(Session session, ProjectFiles files, Recovery? recovery = null, string? userDir = null)
    {
        _session = session;
        _dispatcher = new(session, files, recovery, new SupportDesk())
        {
            Templates = userDir == null ? null : new TemplateLibrary(Path.Combine(userDir, "templates")),
            Furniture = userDir == null ? null : new FurnitureLibrary(Path.Combine(userDir, "furniture")),
        };
        Sharing = new Sharing(this);
        // Python in Lapin (docs/plan-python-scripting.md): where the edition has the scripting module.
        Scripts = new ScriptHost(session.Product.Modules.Contains("scripting"), userDir, o => TellLocal("script.output", o));
        // External programs' measures and watches, the selection told with what it is (docs/plan-external-geometry.md, L2–L3).
        Live = new LiveLinks(session);
        session.History.AuthorName = id => Participant(id)?.Name;
        // A module's background result (docs/overrides.md) waits for the calls' queue like a call, then goes out as a diff.
        session.DeliveryWaiting += () => _ = Deliveries();
    }

    /// <summary>
    /// The deliveries waiting, applied between calls (the session is single-threaded): each diff to every client, as a
    /// command's goes, and what became of each to the computer's own interfaces (an event, "delivery").
    /// </summary>
    public async Task Deliveries()
    {
        await _gate.WaitAsync();
        try
        {
            foreach (var o in _session.TakeDeliveries())
            {
                if (o.Diff != null) { Broadcast(o.Diff); AfterChange(o.Diff, null, byScript: false); }
                if (o.Status == "failed") Console.Error.WriteLine($"[engine] Delivery „{o.Label}” failed: {o.Error}");
                await TellLocal("delivery", new JsonObject { ["label"] = o.Label, ["status"] = o.Status, ["error"] = o.Error });
            }
        }
        catch (Exception ex) { Console.Error.WriteLine($"[engine] {ex}"); }
        finally { _gate.Release(); }
    }

    public Session Session => _session;
    public Sharing Sharing { get; }
    public ScriptHost Scripts { get; }
    public LiveLinks Live { get; }

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

        object? _stepKey;

        /// <summary>
        /// Whose an open step is when this client opens it or edits in it (<see cref="Session.Connection"/>): the client
        /// itself, or the Script panel's Python (<see cref="ScriptHost.KernelKey"/>) for every connection its process
        /// opened and joined with its token (<c>script.join</c>).
        /// </summary>
        public object StepKey { get => _stepKey ?? this; set => _stepKey = value; }
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

    /// <summary>Whether a request's Host names this computer's loopback (what /health answers to).</summary>
    public static bool LoopbackHost(string host) => host is "127.0.0.1" or "localhost" or "[::1]" or "::1";

    /// <summary>The port the engine listens on (0 before it does).</summary>
    public int Port { get; set; }

    /// <summary>
    /// Who this is, for a program looking for Lapin on this computer (docs/python.md, How a program finds Lapin): the
    /// <c>engine.hello</c> call's answer and /health's. <c>engine</c> is always "lapin", so another program listening on
    /// 14913 is never taken for one. Read without the calls' queue: the project's path is a reference read at once.
    /// </summary>
    public JsonObject Hello() => new()
    {
        ["engine"] = "lapin", ["ok"] = true, ["product"] = _session.Product.Id, ["productName"] = _session.Product.Name,
        ["version"] = Session.AppVersion, ["pid"] = Environment.ProcessId, ["port"] = Port,
        ["project"] = _session.Path, ["modules"] = new JsonArray(_session.Product.Modules.Select(m => (JsonNode?)JsonValue.Create(m)).ToArray()),
    };

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
        // The questions it asked (lapin.ui.ask) taken away: the windows close their dialogs.
        Scripts.Forget(c);
        // Its measures and watches with it.
        Live.Forget(c);
        if (had && c.Participant != null) await BroadcastPresence();
        // A script's connection lost (its Python killed, stopped, crashed) with its step open: the step taken back, every window told.
        // A script's Python with several connections (one it opened itself) keeps its step until the last of them goes.
        var key = c.StepKey;
        if (_session.OwnsStep(key) && !Clients().Any(o => ReferenceEquals(o.StepKey, key)))
        {
            await _gate.WaitAsync();
            // A script's step taken back is a script's change: it sets off no project script (AfterChange).
            try { if (_session.RollbackFor(key) is { } back) { Broadcast(back); AfterChange(back, null, byScript: true); } }
            finally { _gate.Release(); }
            if (_session.DeliveriesWaiting) _ = Deliveries();
        }
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
        // The connection is who a script's open step belongs to (Session.Step.cs): the client, or the Python it is one of.
        if (client != null) caller = caller with { Connection = client.StepKey };
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
            if (diff != null)
            {
                Broadcast(diff);
                AfterChange(diff, client, byScript: client != null && ReferenceEquals(client.StepKey, Scripts.KernelKey));
            }
            return reply;
        }
        finally
        {
            _gate.Release();
            // What waited for a script's step to end (a module's background result) goes now.
            if (!_session.StepOpen && _session.DeliveriesWaiting) _ = Deliveries();
        }
    }

    /// <summary>A share.*, presence.*, cancel.*, extensions.*, script.*, measure.* or engine.hello call, parsed; null for any other. They are answered at once, past the queue of calls (a cancel stops the one running).</summary>
    static JsonObject? Own(string text)
    {
        // Cheap test first: every other call goes on untouched.
        if (!text.Contains("\"share.", StringComparison.Ordinal) && !text.Contains("\"presence.", StringComparison.Ordinal) && !text.Contains("\"cancel.", StringComparison.Ordinal)
            && !text.Contains("\"extensions.", StringComparison.Ordinal) && !text.Contains("\"script.", StringComparison.Ordinal) && !text.Contains("\"measure.", StringComparison.Ordinal)
            && !text.Contains("\"engine.hello\"", StringComparison.Ordinal)) return null;
        try
        {
            return JsonNode.Parse(text) is JsonObject o && (string?)o["method"] is { } m
                && (m.StartsWith("share.", StringComparison.Ordinal) || m.StartsWith("presence.", StringComparison.Ordinal) || m.StartsWith("cancel.", StringComparison.Ordinal)
                    || m.StartsWith("extensions.", StringComparison.Ordinal) || m.StartsWith("script.", StringComparison.Ordinal) || m.StartsWith("measure.", StringComparison.Ordinal)
                    || m == "engine.hello") ? o : null;
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
                // Who this is (docs/python.md, How a program finds Lapin): answered at once, past the queue of calls, so a
                // program looking for Lapin is not held up by a long call. This computer's own clients only (the project's path).
                case "engine.hello":
                    if (caller.Remote) throw new CommandException("Only the host can do that.");
                    result = Hello();
                    break;
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
                    // The long call running (an IFC file being optimised, an SDNF import) stopped: this computer's own interface only.
                    if (caller.Remote) throw new CommandException("Only the host can do that.");
                    _dispatcher.CancelLong();
                    result = new JsonObject { ["cancelled"] = true };
                    break;
                // Lapin Extensions (docs/plan-extensions.md): the catalogue and its packages read for this computer's interface, past the
                // queue of calls (a download holds up no edit); the interface checks each package's SHA-256. Never a guest's.
                case "extensions.fetch":
                    if (caller.Remote) throw new CommandException("Only the host can do that.");
                    result = new JsonObject { ["data"] = Convert.ToBase64String(await ExtensionFetch.Fetch((string?)p["url"] ?? "")) };
                    break;
                case "extensions.source":
                    if (caller.Remote) throw new CommandException("Only the host can do that.");
                    result = new JsonObject { ["index"] = ExtensionFetch.IndexFromEnvironment };
                    break;
                // Python (docs/plan-python-scripting.md, stage 2): the host's own, past the queue of calls (a Stop must reach a script
                // whose commands hold the queue); what the script does comes back through its own connection as ordinary calls.
                // Stage 3: a connection of the Script panel's own Python (lapinbim.Lapin() in a script) shows its kernel's token and joins its
                // run's step; any other token, or none, is refused and the connection stays its own (refused while a step is open, as ever).
                case "script.join":
                    if (caller.Remote || client == null) throw new CommandException("Only the host can do that.");
                    if (!Scripts.IsKernel((string?)p["token"])) throw new CommandException(ScriptHost.NotKernel);
                    client.StepKey = Scripts.KernelKey;
                    result = new JsonObject { ["joined"] = true };
                    break;
                // Stage 4: lapin.ui.ask. The question goes to this computer's windows; the asking connection is answered when one of them
                // answers, its other calls going on meanwhile (a Stop's rollback comes through it). Never a guest's, neither the asking nor the answer.
                case "script.ask":
                {
                    if (caller.Remote) throw new CommandException("Only the host can do that.");
                    if (client == null) throw new CommandException("A question is asked over a connection.");
                    var answer = Scripts.Ask(p, client, ReferenceEquals(client.StepKey, Scripts.KernelKey));
                    if (id != null) _ = ReplyWhenAnswered(client, id, answer);
                    return null;
                }
                // External programs (docs/plan-external-geometry.md, L2–L3): measures and watches of the connection's own, a project
                // script's watch kept in the project (scripting.watch, one undo step). Never a guest's.
                case "measure.define" or "measure.get" or "measure.remove" or "measure.list" or "script.watch" or "script.unwatch":
                {
                    if (caller.Remote) throw new CommandException("Only the host can do that.");
                    if (client == null) throw new CommandException("Measures and watches are a connection's.");
                    if (method is "script.watch" or "script.unwatch" && p["script"] is JsonValue sv && sv.TryGetValue<string>(out var script))
                    {
                        // A project script's watch is the project's: the command, one undo step, answered as this call.
                        var args = new JsonObject { ["name"] = script };
                        if (_session.Model.Get(script) is { Class: LiveLinks.ScriptClass }) args = new JsonObject { ["id"] = script };
                        if (method == "script.watch") { args["ids"] = p["ids"]?.DeepClone(); args["measures"] = p["measures"]?.DeepClone(); }
                        var call = new JsonObject { ["id"] = id?.DeepClone(), ["method"] = "doc.execute", ["params"] = new JsonObject { ["command"] = "scripting.watch", ["args"] = args } };
                        return await Handle(call.ToJsonString(), client);
                    }
                    await _gate.WaitAsync();
                    try
                    {
                        result = method switch
                        {
                            "measure.define" => Live.Define(client, p),
                            "measure.get" => Live.Get(client, (string?)p["name"]),
                            "measure.remove" => Live.Remove(client, (string?)p["name"]),
                            "measure.list" => Live.List(client),
                            "script.watch" => Live.AddWatch(client, p),
                            _ => Live.Unwatch(client, (string?)p["watch"]),
                        };
                    }
                    finally { _gate.Release(); }
                    break;
                }
                case var _ when method.StartsWith("script.", StringComparison.Ordinal):
                    if (caller.Remote) throw new CommandException("Only the host can do that.");
                    result = await Scripts.Handle(method, p);
                    // The window's selection, told to every program on this computer with what it is (L2).
                    if (method == "script.selected") _ = TellSelection();
                    // With details: what each selected element is, as the selection event tells it.
                    if (method == "script.selection" && p["details"] is JsonValue dv && dv.TryGetValue<bool>(out var details) && details)
                    {
                        await _gate.WaitAsync();
                        try { result = Live.Selection(Scripts.Selection); }
                        finally { _gate.Release(); }
                    }
                    break;
                default:
                    if (caller.Remote) throw new CommandException("Only the host can do that.");
                    result = await Sharing.Handle(method, p);
                    break;
            }
            // A call without an id (the window's selection told as it changes) asks no answer.
            return id == null ? null : BinaryFrame.Encode(new Reply(id, result is JsonNode n ? n : result));
        }
        catch (Exception ex) when (ex is CommandException or ModelException or WebSocketException or InvalidOperationException or HttpRequestException or IOException)
        {
            return id == null ? null : BinaryFrame.Encode(new Reply(id, null, ex.Message));
        }
    }

    /// <summary>A script's question answered (or cancelled): its reply posted to the connection that asked.</summary>
    static async Task ReplyWhenAnswered(Client client, JsonNode id, Task<JsonObject> answer)
    {
        byte[] frame;
        try { frame = BinaryFrame.Encode(new Reply(id, await answer)); }
        catch (Exception ex) when (ex is CommandException or InvalidOperationException) { frame = BinaryFrame.Encode(new Reply(id, null, ex.Message)); }
        client.Post(frame);
    }

    // ------------------------------------------------------------ external programs (docs/plan-external-geometry.md, L2–L3)

    /// <summary>How long after the last change a project script watching it is run again (several quick edits run it once).</summary>
    public static TimeSpan RegenerateAfter { get; set; } = TimeSpan.FromMilliseconds(300);
    /// <summary>How long a script run again may take before it is stopped (and what it did taken back).</summary>
    public static TimeSpan RegenerateTimeout { get; set; } = TimeSpan.FromSeconds(60);

    readonly Dictionary<string, CancellationTokenSource> _regenerating = [];

    /// <summary>After a change (under the gate): measures and watches told to their connections, project scripts it hit run again.</summary>
    void AfterChange(Diff diff, Client? author, bool byScript)
    {
        List<LiveLinks.Note> notes;
        List<string> scripts;
        try { (notes, scripts) = Live.Changed(diff, author, byScript); }
        catch (Exception ex) { Console.Error.WriteLine($"[engine] Measures after a change: {ex}"); return; }
        var clients = Clients();
        foreach (var n in notes)
            if (n.Owner is Client c && clients.Contains(c)) c.Post(new JsonObject { ["event"] = n.Event, ["data"] = n.Data }.ToJsonString());
        if (Scripts.Enabled) foreach (var s in scripts) Regenerate(s);
    }

    /// <summary>A project script run again soon (<see cref="RegenerateAfter"/>), a run already waiting for it put off.</summary>
    void Regenerate(string scriptId)
    {
        var cts = new CancellationTokenSource();
        lock (_regenerating)
        {
            if (_regenerating.Remove(scriptId, out var before)) before.Cancel();
            _regenerating[scriptId] = cts;
        }
        _ = RegenerateSoon(scriptId, cts);
    }

    async Task RegenerateSoon(string scriptId, CancellationTokenSource cts)
    {
        try { await Task.Delay(RegenerateAfter, cts.Token); }
        catch (TaskCanceledException) { return; }
        lock (_regenerating) if (_regenerating.TryGetValue(scriptId, out var mine) && mine == cts) _regenerating.Remove(scriptId);
        for (var tries = 0; ; tries++)
        {
            string? code, name;
            await _gate.WaitAsync();
            try
            {
                if (_session.Model.Get(scriptId) is not { Class: LiveLinks.ScriptClass } s) return;
                (code, name) = (s.Str("code") ?? "", s.Name);
            }
            finally { _gate.Release(); }
            var stem = name.EndsWith(".py", StringComparison.OrdinalIgnoreCase) ? name[..^3] : name;
            // Never an untrusted project's code (decision 489, §7): the user runs it once from the Script panel and trusts it.
            if (!Scripts.IsTrusted(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(code)))))
            {
                await ScriptNote("err", $"„{name}” was not run again: it is not trusted on this computer. Run it once from the Script panel and choose Trust.");
                return;
            }
            // A run going on (the user's, or another script's) first: tried again each second, a minute at most.
            if (Scripts.RunningId != null)
            {
                if (tries >= 60) { await ScriptNote("err", $"„{name}” was not run again: another script kept running."); return; }
                await Task.Delay(1000);
                continue;
            }
            try
            {
                var r = await Scripts.Handle("script.run", new JsonObject { ["code"] = code, ["name"] = $"{stem} regenerated" });
                if (r?["run"] is JsonValue rv && rv.TryGetValue<int>(out var run)) _ = StopAfter(run, name);
            }
            catch (CommandException ex) { await ScriptNote("err", $"„{name}” was not run again: {ex.Message}"); }
            return;
        }
    }

    async Task StopAfter(int run, string name)
    {
        await Task.Delay(RegenerateTimeout);
        if (Scripts.RunningId != run) return;
        await ScriptNote("err", $"„{name}” took more than {RegenerateTimeout.TotalSeconds:0} s to run again: it was stopped, and nothing it did is kept.");
        try { await Scripts.Handle("script.stop", []); } catch (CommandException) { }
    }

    /// <summary>A line of Lapin's own in the Script panel's output.</summary>
    Task ScriptNote(string level, string text) => TellLocal("script.output", new JsonObject { ["type"] = "note", ["level"] = level, ["text"] = text });

    /// <summary>The window's selection with what it is, to every connection of this computer (the event <c>selection</c>).</summary>
    async Task TellSelection()
    {
        JsonObject data;
        await _gate.WaitAsync();
        try { data = Live.Selection(Scripts.Selection); }
        catch (Exception ex) { Console.Error.WriteLine($"[engine] The selection: {ex}"); return; }
        finally { _gate.Release(); }
        await TellLocal("selection", data);
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
        public void Open(string? link, JsonObject p) => Support.Open(link, p);
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
