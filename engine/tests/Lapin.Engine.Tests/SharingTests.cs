using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Model;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// A shared session (docs/plan-multiplayer.md) end to end: the relay as it
/// runs on the server, this engine sharing through it, and a guest made here
/// as the interface makes one, encrypted with the invitation. What a guest
/// may call is a list of what is let through.
/// </summary>
public sealed class SharingTests : IAsyncLifetime
{
    Process? _relay;
    string _relayUrl = "";

    public async Task InitializeAsync()
    {
        var dll = Path.Combine(EngineDir(), "src/Relay/Lapin.Relay/bin", Config(), "net10.0/Lapin.Relay.dll");
        Assert.True(File.Exists(dll), $"Build the relay first: {dll}");
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        _relay = Process.Start(new ProcessStartInfo("dotnet", [dll, "--urls", $"http://127.0.0.1:{port}"]) { RedirectStandardOutput = true, RedirectStandardError = true })!;
        _relayUrl = $"ws://127.0.0.1:{port}/relay";
        using var http = new HttpClient();
        for (var i = 0; i < 100; i++)
        {
            try { if ((await http.GetAsync($"http://127.0.0.1:{port}/relay/health")).IsSuccessStatusCode) return; }
            catch (HttpRequestException) { }
            await Task.Delay(100);
        }
        throw new InvalidOperationException("The relay did not start.");
    }

    public Task DisposeAsync()
    {
        if (_relay is { HasExited: false }) _relay.Kill(true);
        _relay?.Dispose();
        return Task.CompletedTask;
    }

    static string EngineDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "Lapin.slnx"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("No engine folder.");
    }

    static string Config() => AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}") ? "Release" : "Debug";

    /// <summary>A guest as the interface is one: joins with the code, says hello, calls, gets diffs and presence.</summary>
    sealed class TestGuest : IAsyncDisposable
    {
        readonly ClientWebSocket _ws = new();
        ShareCrypto _crypto = null!;
        int _id;
        public List<JsonObject> Events { get; } = [];
        public JsonObject Welcome { get; private set; } = [];
        readonly Dictionary<int, TaskCompletionSource<JsonObject>> _pending = [];
        Task? _loop;

        public static async Task<TestGuest> Join(string code, string relay, string name, string? token = null)
        {
            var g = new TestGuest();
            var parts = code.Split('.');
            var secret = Convert.FromBase64String(parts[1].Replace('-', '+').Replace('_', '/') + new string('=', (4 - parts[1].Length % 4) % 4));
            await g._ws.ConnectAsync(new Uri($"{relay}/join?room={parts[0]}"), CancellationToken.None);
            var salt = RandomNumberGenerator.GetBytes(16);
            await g._ws.SendAsync((byte[])[0, .. salt], WebSocketMessageType.Binary, true, CancellationToken.None);
            g._crypto = new ShareCrypto(secret, parts[0], salt, host: false);
            var welcome = new TaskCompletionSource<JsonObject>();
            g._loop = Task.Run(() => g.Loop(welcome));
            await g.SendText(new JsonObject { ["hello"] = new JsonObject { ["name"] = name, ["protocol"] = Sharing.Protocol, ["token"] = token } }.ToJsonString());
            var w = await welcome.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (w["refused"] != null) throw new InvalidOperationException((string?)w["refused"]);
            g.Welcome = (JsonObject)w["welcome"]!;
            return g;
        }

        Task SendText(string text) => _ws.SendAsync(_crypto.Seal(1, Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Binary, true, CancellationToken.None);

        public async Task<JsonObject> Call(string method, JsonObject? p = null)
        {
            var id = ++_id;
            var tcs = new TaskCompletionSource<JsonObject>();
            lock (_pending) _pending[id] = tcs;
            await SendText(new JsonObject { ["id"] = id, ["method"] = method, ["params"] = p ?? [] }.ToJsonString());
            return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }

        public Task Presence(JsonObject p) => SendText(new JsonObject { ["method"] = "presence.update", ["params"] = p }.ToJsonString());

        async Task Loop(TaskCompletionSource<JsonObject> welcome)
        {
            var buffer = new byte[1 << 16];
            while (_ws.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult r;
                try
                {
                    do { r = await _ws.ReceiveAsync(buffer, CancellationToken.None); ms.Write(buffer, 0, r.Count); }
                    while (!r.EndOfMessage && r.MessageType != WebSocketMessageType.Close);
                }
                catch (WebSocketException) { return; }
                if (r.MessageType == WebSocketMessageType.Close) return;
                var plain = _crypto.Open(ms.ToArray()) ?? throw new InvalidOperationException("A frame that does not open.");
                JsonObject msg = plain[0] == 1
                    ? JsonNode.Parse(Encoding.UTF8.GetString(plain, 1, plain.Length - 1))!.AsObject()
                    : JsonNode.Parse(BinaryFrame.Decode(plain[1..]).Header.RootElement.GetRawText())!.AsObject();
                if (msg["welcome"] != null || msg["refused"] != null) { welcome.TrySetResult(msg); continue; }
                if (msg["event"] != null) { lock (Events) Events.Add(msg); continue; }
                if ((int?)msg["id"] is { } id) { TaskCompletionSource<JsonObject>? tcs; lock (_pending) _pending.Remove(id, out tcs); tcs?.TrySetResult(msg); }
            }
        }

        public JsonObject? LastEvent(string name) { lock (Events) return Events.LastOrDefault(e => (string?)e["event"] == name); }

        public async ValueTask DisposeAsync()
        {
            // The host may have ended the session: the relay closed the socket already.
            try { if (_ws.State == WebSocketState.Open) await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException) { }
            _ws.Dispose();
        }
    }

    static async Task Until(Func<bool> done, string what)
    {
        for (var i = 0; i < 100 && !done(); i++) await Task.Delay(50);
        Assert.True(done(), what);
    }

    [Fact]
    public async Task AGuestJoinsDrawsAndUndoesOnlyTheirOwn()
    {
        var f = new Fixture();
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        var status = (JsonObject)(await hub.Sharing.Handle("share.start", new JsonObject { ["name"] = "Ionuț", ["relay"] = _relayUrl }))!;
        Assert.True((bool)status["active"]!);
        var code = (string)status["code"]!;
        Assert.Contains("#join=" + code, (string)status["link"]!);

        await using var ana = await TestGuest.Join(code, _relayUrl, "Ana");
        Assert.Equal("p1", (string)ana.Welcome["id"]!);
        Assert.Equal("Ionuț", (string)ana.Welcome["host"]!);

        // The model, as the window gets it at the start.
        var snapshot = await ana.Call("doc.snapshot");
        Assert.True((bool)snapshot["result"]!["reset"]!);

        // My wall, then hers: she gets the diff of mine, I see hers in the model.
        var mine = f.Wall(0, 0, 5, 0);
        var reply = await ana.Call("doc.execute", new JsonObject { ["command"] = "architecture.addWall", ["args"] = new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 10, ["x2"] = 5, ["y2"] = 10 } });
        var hers = (string)reply["result"]!["id"]!;
        Assert.NotNull(f.Model.Get(hers));
        Assert.Equal("p1", f.Session.History.Done[^1].Author);
        // Her diffs carry her own undo.
        await Until(() => ana.LastEvent("diff") is { } d && (string?)d["data"]!["state"]!["undoLabel"] == "Wall", "her diff with her undo");

        // Her undo takes her wall, not mine.
        await ana.Call("doc.undo");
        Assert.Null(f.Model.Get(hers));
        Assert.NotNull(f.Model.Get(mine));

        // Presence goes round.
        await ana.Presence(new JsonObject { ["cursor"] = new JsonArray(1.5, 2.5) });
        await Until(() => hub.PresenceJson()["participants"]!.AsArray().Any(p => (string?)p!["id"] == "p1" && p["presence"]?["cursor"] != null), "her cursor at the host");

        await hub.Sharing.Stop("done");
        Assert.False(hub.Sharing.Active);
    }

    [Fact]
    public async Task AGuestCannotReachTheHostsFiles()
    {
        var f = new Fixture();
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        var status = (JsonObject)(await hub.Sharing.Handle("share.start", new JsonObject { ["relay"] = _relayUrl }))!;
        await using var guest = await TestGuest.Join((string)status["code"]!, _relayUrl, "Ion");
        foreach (var (method, p) in new (string, JsonObject)[]
        {
            ("project.open", new JsonObject { ["path"] = "/etc/passwd" }),
            ("project.save", new JsonObject { ["path"] = "/tmp/x.lapin" }),
            ("project.new", []),
            ("recovery.list", []),
            ("support.open", new JsonObject { ["link"] = "https://example.com" }),
            ("project.exportIfc", new JsonObject { ["path"] = "/tmp/x.ifc" }),
            ("doc.execute", new JsonObject { ["command"] = "drafting.importDxf", ["args"] = new JsonObject { ["path"] = "/etc/hosts" } }),
            ("share.stop", []),
        })
        {
            var r = await guest.Call(method, p);
            Assert.True(r["error"] != null, $"{method} should be refused to a guest");
        }
        Assert.True(hub.Sharing.Active);
        // A viewer looks, and does not edit.
        await hub.Sharing.Handle("share.setRole", new JsonObject { ["id"] = "p1", ["role"] = "viewer" });
        var edit = await guest.Call("doc.execute", new JsonObject { ["command"] = "storey.create", ["args"] = new JsonObject() });
        Assert.Contains("look", (string)edit["error"]!);
        Assert.Null(edit["result"]);
        await hub.Sharing.Stop("done");
    }

    [Fact]
    public void EveryMethodIsDecidedForGuests()
    {
        // The dispatcher's methods, each refused from the network unless named: a new one stays refused.
        var allowed = new[] { "session.info", "doc.snapshot", "doc.query", "quantities.of", "project.versions", "project.compareVersions", "export.pdf", "export.dxf", "project.exportIfc", "project.exportMesh", "doc.execute", "doc.undo", "doc.redo" };
        foreach (var m in allowed) Assert.Null(EngineDispatcher.AllowedRemote(m, new JsonObject(), "editor"));
        foreach (var m in new[] { "project.open", "project.save", "project.new", "project.openBytes", "project.saveBytes", "project.saveVersion", "project.restoreVersion", "project.deleteVersion", "project.exportVersion", "project.reloadLink", "recovery.restore", "support.report", "support.messages", "something.new" })
            Assert.NotNull(EngineDispatcher.AllowedRemote(m, new JsonObject(), "editor"));
        Assert.NotNull(EngineDispatcher.AllowedRemote("project.importIfc", new JsonObject(), "editor"));
        Assert.Null(EngineDispatcher.AllowedRemote("project.importIfc", new JsonObject { ["data"] = "AAAA" }, "editor"));
        Assert.NotNull(EngineDispatcher.AllowedRemote("doc.undo", new JsonObject(), "viewer"));
    }

    [Fact]
    public void AFrameCannotBeReplayedNorTurnedRound()
    {
        var secret = RandomNumberGenerator.GetBytes(16);
        var salt = RandomNumberGenerator.GetBytes(16);
        var host = new ShareCrypto(secret, "room0000", salt, host: true);
        var guest = new ShareCrypto(secret, "room0000", salt, host: false);
        var frame = guest.Seal(1, "{\"id\":1}"u8.ToArray());
        Assert.Equal("{\"id\":1}", Encoding.UTF8.GetString(host.Open(frame)![1..]));
        Assert.Null(host.Open(frame));
        Assert.Null(guest.Open(frame));
        // Another connection's key opens nothing.
        var other = new ShareCrypto(secret, "room0000", RandomNumberGenerator.GetBytes(16), host: true);
        Assert.Null(other.Open(guest.Seal(1, "x"u8.ToArray())));
    }
}
