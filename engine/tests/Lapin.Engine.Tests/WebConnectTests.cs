using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Protocol;
using Lapin.Scripting;

namespace Lapin.Engine.Tests;

/// <summary>
/// Web apps on other sites connecting to the desktop engine (docs/plan-web-connect.md): origins compared exactly, the Host
/// checked, a site paired with nothing until the user lets it in, then a reader or an editor, never the host's own calls;
/// the list of sites the user keeps, a revoked site dropped at once, and messages bounded.
/// </summary>
public sealed class WebConnectTests : IDisposable
{
    const string Site = "https://ginnung.tech";
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-web-connect-").FullName;
    readonly CancellationTokenSource _cts = new();

    public void Dispose()
    {
        _cts.Cancel();
        Directory.Delete(_dir, recursive: true);
    }

    // ------------------------------------------------------------ origins and hosts

    [Theory]
    [InlineData("http://localhost:5180")]
    [InlineData("http://127.0.0.1:47810")]
    [InlineData("http://[::1]:5180")]
    [InlineData("tauri://localhost")]
    [InlineData("http://tauri.localhost")]
    [InlineData("https://tauri.localhost")]
    public void The_apps_own_origins_are_its_own(string origin)
    {
        Assert.True(EngineHub.AllowedOrigin(origin));
        Assert.Equal(EngineHub.OriginKind.Own, EngineHub.Classify(origin, out var site));
        Assert.Null(site);
    }

    [Theory]
    // What a prefix test let through: another site's name that starts like the loopback's.
    [InlineData("http://localhost.evil.com")]
    [InlineData("http://localhost.evil.com:5180")]
    [InlineData("http://127.0.0.1.evil.com")]
    [InlineData("http://127.0.0.1.evil.com:80")]
    [InlineData("http://localhost:5180.evil.com")]
    [InlineData("http://localhost:5180@evil.com")]
    [InlineData("tauri://localhost.evil.com")]
    [InlineData("http://tauri.localhost.evil.com")]
    // Not as a browser writes an origin, or not one at all.
    [InlineData("null")]
    [InlineData("HTTP://LOCALHOST:5180")]
    [InlineData("http://localhost:5180/")]
    [InlineData("http://localhost:5180/ws")]
    [InlineData("file:///etc/hosts")]
    public void An_origin_that_only_starts_like_the_apps_is_not_its_own(string origin)
    {
        Assert.False(EngineHub.AllowedOrigin(origin));
        Assert.NotEqual(EngineHub.OriginKind.Own, EngineHub.Classify(origin, out _));
    }

    [Fact]
    public void An_https_site_may_ask_and_anything_else_is_refused()
    {
        Assert.Equal(EngineHub.OriginKind.Site, EngineHub.Classify(Site, out var site));
        Assert.Equal(Site, site);
        Assert.Equal(EngineHub.OriginKind.Site, EngineHub.Classify("https://app.example.org:8443", out site));
        Assert.Equal("https://app.example.org:8443", site);
        // An http site's page could be changed on the way; a trailing slash or a default port spelled out is not an origin.
        foreach (var o in new[] { "http://ginnung.tech", "https://ginnung.tech/", "https://ginnung.tech:443", "https://user@ginnung.tech", "ws://ginnung.tech", "null" })
            Assert.Equal(EngineHub.OriginKind.Refused, EngineHub.Classify(o, out _));
        // No Origin: a program on this computer (Python, the MCP bridge), as ever.
        Assert.Equal(EngineHub.OriginKind.Own, EngineHub.Classify("", out _));
        Assert.Equal(EngineHub.OriginKind.Own, EngineHub.Classify(null, out _));
        // What the user types in Preferences is forgiven its slash and case.
        Assert.Equal(Site, WebOrigins.NormalizeTyped(" https://Ginnung.Tech/ "));
        Assert.Null(WebOrigins.NormalizeTyped("https://ginnung.tech/path"));
    }

    [Fact]
    public void The_host_header_must_be_a_loopback_name_and_this_port()
    {
        Assert.True(EngineHub.AllowedHost("127.0.0.1", 14912, 14912));
        Assert.True(EngineHub.AllowedHost("localhost", 14912, 14912));
        Assert.True(EngineHub.AllowedHost("[::1]", 14912, 14912));
        Assert.False(EngineHub.AllowedHost("ginnung.tech", 14912, 14912));
        Assert.False(EngineHub.AllowedHost("127.0.0.1.evil.com", 14912, 14912));
        Assert.False(EngineHub.AllowedHost("localhost.evil.com", 14912, 14912));
        Assert.False(EngineHub.AllowedHost("127.0.0.1", 14913, 14912));
        Assert.False(EngineHub.AllowedHost("127.0.0.1", null, 14912));
    }

    // ------------------------------------------------------------ pairing

    [Fact]
    public async Task A_site_not_let_in_gets_nothing_but_hello_and_asking()
    {
        var (f, hub) = Hub();
        await using var window = Connect(hub);
        await using var site = Connect(hub, Site);

        foreach (var (method, p) in new (string, JsonObject?)[]
        {
            ("doc.snapshot", null), ("session.info", null), ("session.catalogue", null), ("doc.query", new JsonObject { ["name"] = "quantities.summary" }),
            ("doc.execute", new JsonObject { ["command"] = "architecture.addWall", ["args"] = WallArgs(f, 0) }),
            ("presence.update", new JsonObject { ["view"] = "plan" }), ("script.run", new JsonObject { ["code"] = "print(1)" }),
            ("sites.list", null), ("pair.answer", new JsonObject { ["requestId"] = "x", ["decision"] = "always", ["edit"] = true }),
        })
            Assert.Contains("not connected", Error(await site.Call(method, p)));

        // Nothing of the project reaches it: no diff of the window's edit, no presence.
        await window.Call("doc.execute", new JsonObject { ["command"] = "architecture.addWall", ["args"] = WallArgs(f, 0) });
        await window.Call("presence.hello", null, wait: false);
        await Task.Delay(200);
        Assert.DoesNotContain(site.Received(), m => m["event"] != null);

        // Hello, redacted: that this is Lapin, the product and version, and that it is not let in.
        var hello = Result(await site.Call("engine.hello"));
        Assert.Equal("lapin", (string?)hello["engine"]);
        Assert.True((bool)hello["ok"]!);
        Assert.Equal("test", (string?)hello["product"]);
        Assert.NotNull(hello["version"]);
        Assert.False((bool)hello["paired"]!);
        foreach (var hidden in new[] { "project", "pid", "port", "modules", "productName" }) Assert.False(hello.ContainsKey(hidden), hidden);
        // The window's own hello keeps all of it.
        Assert.True(Result(await window.Call("engine.hello")).ContainsKey("pid"));
    }

    [Fact]
    public async Task Let_in_once_to_read_it_reads_and_cannot_edit()
    {
        var (f, hub) = Hub();
        await using var window = Connect(hub);
        await using var site = Connect(hub, Site);

        var asked = site.Call("pair.request", new JsonObject { ["name"] = "Ginnung", ["edit"] = true });
        var ask = await window.Event("pair.ask");
        Assert.Equal(Site, (string?)ask["origin"]);
        Assert.Equal("Ginnung", (string?)ask["name"]);
        Assert.True((bool)ask["wantsEdit"]!);
        // A site never answers its own question.
        Assert.NotNull(Error(await site.Call("pair.answer", new JsonObject { ["requestId"] = (string?)ask["requestId"], ["decision"] = "always", ["edit"] = true })));

        var answered = Result(await window.Call("pair.answer", new JsonObject { ["requestId"] = (string?)ask["requestId"], ["decision"] = "once", ["edit"] = false }));
        Assert.Equal("once", (string?)answered["decision"]);
        var granted = Result(await asked);
        Assert.True((bool)granted["granted"]!);
        Assert.False((bool)granted["edit"]!);
        Assert.False((bool)granted["remembered"]!);
        Assert.Equal("once", (string?)(await window.Event("pair.done"))["decision"]);
        // Once: nothing written to the list.
        Assert.Empty(new ConnectedSites(_dir).List());

        // Reads: the snapshot, the session, a query, an export as bytes.
        Assert.Null(Error(await site.Call("doc.snapshot")));
        Assert.Null(Error(await site.Call("session.info")));
        // Edits nothing.
        var before = f.Model.OfClass("IfcWall").Count();
        Assert.NotNull(Error(await site.Call("doc.execute", new JsonObject { ["command"] = "architecture.addWall", ["args"] = WallArgs(f, 0) })));
        Assert.NotNull(Error(await site.Call("doc.execute", new JsonObject { ["command"] = "edit.batch", ["args"] = Batch(f) })));
        Assert.NotNull(Error(await site.Call("doc.undo")));
        Assert.Equal(before, f.Model.OfClass("IfcWall").Count());

        // It now sees the window's edits.
        await window.Call("doc.execute", new JsonObject { ["command"] = "architecture.addWall", ["args"] = WallArgs(f, 0) });
        await site.Event("diff");
        Assert.True((bool)Result(await site.Call("engine.hello"))["paired"]!);
    }

    [Fact]
    public async Task Let_in_for_good_to_edit_it_edits_through_a_batch_one_undo_step_and_is_remembered()
    {
        var (f, hub) = Hub();
        await using var window = Connect(hub);
        await using var site = Connect(hub, Site);

        var asked = site.Call("pair.request", new JsonObject { ["name"] = "Ginnung" });
        var ask = await window.Event("pair.ask");
        await window.Call("pair.answer", new JsonObject { ["requestId"] = (string?)ask["requestId"], ["decision"] = "always", ["edit"] = true });
        var granted = Result(await asked);
        Assert.True((bool)granted["edit"]!);
        Assert.True((bool)granted["remembered"]!);

        var before = f.Model.OfClass("IfcWall").Count();
        Assert.Null(Error(await site.Call("doc.execute", new JsonObject { ["command"] = "edit.batch", ["args"] = Batch(f) })));
        Assert.Equal(before + 2, f.Model.OfClass("IfcWall").Count());
        // The site's own undo: the whole batch, one step.
        Assert.Null(Error(await site.Call("doc.undo")));
        Assert.Equal(before, f.Model.OfClass("IfcWall").Count());

        // Written to the user's list, and a second engine on the same folder lets the site in without asking.
        var listed = Assert.Single(new ConnectedSites(_dir).List());
        Assert.Equal(Site, listed.Origin);
        Assert.True(listed.Edit);
        var sites = Result(await window.Call("sites.list"));
        Assert.Equal(1, (int)sites["sites"]![0]!["connected"]!);

        var (_, next) = Hub();
        await using var again = Connect(next, Site);
        var at = Result(await again.Call("pair.request"));
        Assert.True((bool)at["granted"]!);
        Assert.True((bool)at["remembered"]!);
    }

    [Fact]
    public async Task Even_let_in_to_edit_a_site_never_reaches_scripts_extensions_sharing_the_list_or_the_hosts_files()
    {
        var (f, hub) = Hub();
        await using var window = Connect(hub);
        await using var site = Connect(hub, Site);
        await Pair(window, site, edit: true);

        var file = Path.Combine(_dir, "x.pdf");
        foreach (var (method, p) in new (string, JsonObject?)[]
        {
            ("script.run", new JsonObject { ["code"] = "print(1)" }), ("script.list", null), ("script.join", new JsonObject { ["token"] = "x" }),
            ("extensions.fetch", new JsonObject { ["url"] = "https://lapinbim.com/extensions/index.json" }), ("extensions.source", null),
            ("share.start", null), ("share.status", null), ("cancel.long", null), ("measure.list", null), ("presence.hello", new JsonObject { ["name"] = "Host" }),
            ("sites.list", null), ("sites.set", new JsonObject { ["origin"] = "https://evil.example", ["edit"] = true }), ("sites.revoke", new JsonObject { ["origin"] = Site }),
            ("pair.answer", new JsonObject { ["requestId"] = "x", ["decision"] = "always" }),
            ("doc.begin", null), ("doc.commit", null),
            ("project.open", new JsonObject { ["path"] = file }), ("project.save", null), ("project.new", null),
            ("export.pdf", new JsonObject { ["path"] = file }), ("project.exportIfc", new JsonObject { ["path"] = file }),
            ("project.importIfc", new JsonObject { ["path"] = "/etc/hosts" }),
            ("doc.query", new JsonObject { ["name"] = "drafting.dxfInfo", ["args"] = new JsonObject { ["path"] = "/etc/hosts" } }),
            ("doc.query", new JsonObject { ["name"] = "graphs.evaluate", ["args"] = new JsonObject() }),
            ("doc.execute", new JsonObject { ["command"] = "scripting.save", ["args"] = new JsonObject { ["name"] = "a.py", ["code"] = "print(1)" } }),
            ("doc.execute", new JsonObject { ["command"] = "edit.batch", ["args"] = new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["command"] = "scripting.save", ["args"] = new JsonObject { ["name"] = "a.py", ["code"] = "1" } }) } }),
            ("recovery.list", null), ("support.open", new JsonObject { ["link"] = "x" }),
        })
            Assert.NotNull(Error(await site.Call(method, p)));
        Assert.False(File.Exists(file));
        Assert.Empty(new ConnectedSites(_dir).List());
    }

    [Fact]
    public async Task A_site_refused_is_answered_so_and_not_asked_about_again_at_once()
    {
        var (_, hub) = Hub();
        await using var window = Connect(hub);
        await using var site = Connect(hub, Site);

        var asked = site.Call("pair.request");
        var ask = await window.Event("pair.ask");
        await window.Call("pair.answer", new JsonObject { ["requestId"] = (string?)ask["requestId"], ["decision"] = "deny" });
        var refused = Result(await asked);
        Assert.False((bool)refused["granted"]!);
        Assert.Equal("denied", (string?)refused["reason"]);
        window.Clear();
        Assert.Equal("denied", (string?)Result(await site.Call("pair.request"))["reason"]);
        await Task.Delay(100);
        Assert.DoesNotContain(window.Received(), m => (string?)m["event"] == "pair.ask");
        Assert.NotNull(Error(await site.Call("doc.snapshot")));
    }

    [Fact]
    public async Task Revoking_a_site_drops_its_connections_at_once()
    {
        var (_, hub) = Hub();
        await using var window = Connect(hub);
        await using var site = Connect(hub, Site);
        await Pair(window, site, edit: true, decision: "always");
        Assert.Single(new ConnectedSites(_dir).List());

        var revoked = Result(await window.Call("sites.revoke", new JsonObject { ["origin"] = Site }));
        Assert.True((bool)revoked["revoked"]!);
        Assert.Equal(1, (int)revoked["dropped"]!);
        Assert.Equal(WebSocketCloseStatus.PolicyViolation, await site.ClosedWith());
        Assert.Empty(new ConnectedSites(_dir).List());
        Assert.Empty(Result(await window.Call("sites.list"))["sites"]!.AsArray());
    }

    [Fact]
    public async Task Sites_set_puts_a_site_on_the_list_and_changes_its_live_grant()
    {
        var (f, hub) = Hub();
        await using var window = Connect(hub);
        await using var site = Connect(hub, Site);
        await Pair(window, site, edit: false);
        Assert.NotNull(Error(await site.Call("doc.execute", new JsonObject { ["command"] = "architecture.addWall", ["args"] = WallArgs(f, 0) })));

        await window.Call("sites.set", new JsonObject { ["origin"] = "https://Ginnung.tech/", ["edit"] = true });
        Assert.True(Assert.Single(new ConnectedSites(_dir).List()).Edit);
        Assert.Null(Error(await site.Call("doc.execute", new JsonObject { ["command"] = "architecture.addWall", ["args"] = WallArgs(f, 0) })));
        Assert.NotNull(Error(await window.Call("sites.set", new JsonObject { ["origin"] = "http://ginnung.tech" })));
        Assert.NotNull(Error(await window.Call("sites.set", new JsonObject { ["origin"] = "http://localhost:5180" })));
    }

    [Fact]
    public void The_list_of_sites_is_kept_in_the_users_folder_and_read_back()
    {
        var sites = new ConnectedSites(_dir);
        Assert.Empty(sites.List());
        sites.Set(Site, edit: false);
        sites.Set("https://other.example:8443", edit: true);
        var added = sites.Find(Site)!.AddedAt;
        sites.Set(Site, edit: true);

        var back = new ConnectedSites(_dir).List();
        Assert.Equal(2, back.Count);
        Assert.True(back.Single(s => s.Origin == Site).Edit);
        Assert.Equal(added, back.Single(s => s.Origin == Site).AddedAt);
        Assert.True(new ConnectedSites(_dir).Revoke("https://other.example:8443"));
        Assert.Equal(Site, Assert.Single(new ConnectedSites(_dir).List()).Origin);

        // Entries that are not a site's origin as a browser sends it are left out; an unreadable file lets no site in.
        var file = Path.Combine(_dir, ConnectedSites.FileName);
        File.WriteAllText(file, """{"version":1,"sites":[{"origin":"http://plain.example","edit":true},{"origin":"https://a.example/","edit":true},{"origin":"http://localhost:5180","edit":true},{"origin":"https://ok.example","edit":"yes"}]}""");
        var read = Assert.Single(new ConnectedSites(_dir).List());
        Assert.Equal("https://ok.example", read.Origin);
        Assert.False(read.Edit);
        File.WriteAllText(file, "{not json");
        Assert.Empty(new ConnectedSites(_dir).List());
    }

    // ------------------------------------------------------------ message sizes

    [Fact]
    public async Task A_message_over_the_limit_closes_the_connection_with_1009_and_the_engine_goes_on()
    {
        Assert.Equal(100L << 20, EngineHub.MaxSiteMessage);
        Assert.Equal(512L << 20, EngineHub.MaxOwnMessage);
        Assert.True(EngineHub.MaxUnpairedMessage < EngineHub.MaxSiteMessage);

        var (_, hub) = Hub();
        await using var window = Connect(hub);
        await using var site = Connect(hub, Site);
        // A site not let in may only ask: 64 KB at most, sent in pieces as a browser sends a big one.
        var big = Encoding.UTF8.GetBytes(new JsonObject { ["id"] = 1, ["method"] = "pair.request", ["params"] = new JsonObject { ["name"] = new string('x', 70_000) } }.ToJsonString());
        try
        {
            for (var at = 0; at < big.Length; at += 16_384)
                await site.Ws.SendAsync(big.AsMemory(at, Math.Min(16_384, big.Length - at)), WebSocketMessageType.Text, at + 16_384 >= big.Length, CancellationToken.None);
        }
        catch (WebSocketException) { }
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, await site.ClosedWith());
        // The engine still answers.
        Assert.Null(Error(await window.Call("engine.hello")));
    }

    // ------------------------------------------------------------ helpers

    (Fixture, EngineHub) Hub()
    {
        var f = new Fixture(new ArchitectureModule(), new ScriptingModule());
        return (f, new EngineHub(f.Session, PackageFiles.Create(), userDir: _dir));
    }

    static JsonObject WallArgs(Fixture f, double y) => new() { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = y, ["x2"] = 4, ["y2"] = y };

    static JsonObject Batch(Fixture f) => new()
    {
        ["steps"] = new JsonArray(
            new JsonObject { ["command"] = "architecture.addWall", ["args"] = WallArgs(f, 10) },
            new JsonObject { ["command"] = "architecture.addWall", ["args"] = WallArgs(f, 12) }),
    };

    static async Task Pair(Peer window, Peer site, bool edit, string decision = "once")
    {
        var asked = site.Call("pair.request");
        var ask = await window.Event("pair.ask");
        await window.Call("pair.answer", new JsonObject { ["requestId"] = (string?)ask["requestId"], ["decision"] = decision, ["edit"] = edit });
        Assert.True((bool)Result(await asked)["granted"]!);
    }

    static string? Error(JsonObject reply) => (string?)reply["error"];

    static JsonObject Result(JsonObject reply)
    {
        Assert.Null(reply["error"]);
        return (JsonObject)reply["result"]!;
    }

    Peer Connect(EngineHub hub, string? site = null)
    {
        var toClient = new Pipe(new PipeOptions(useSynchronizationContext: false));
        var toServer = new Pipe(new PipeOptions(useSynchronizationContext: false));
        var server = WebSocket.CreateFromStream(new Wire(toServer.Reader, toClient.Writer), isServer: true, null, TimeSpan.Zero);
        var client = WebSocket.CreateFromStream(new Wire(toClient.Reader, toServer.Writer), isServer: false, null, TimeSpan.Zero);
        _ = hub.Serve(server, _cts.Token, site);
        return new Peer(client);
    }

    /// <summary>A page's end of the connection: what it sends, and every frame it gets (replies and events) kept to look through.</summary>
    sealed class Peer : IAsyncDisposable
    {
        public WebSocket Ws { get; }
        readonly List<JsonObject> _received = [];
        readonly TaskCompletionSource<WebSocketCloseStatus?> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _id;

        public Peer(WebSocket ws)
        {
            Ws = ws;
            _ = Task.Run(Read);
        }

        async Task Read()
        {
            var buffer = new byte[1 << 16];
            try
            {
                while (true)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult r;
                    do
                    {
                        r = await Ws.ReceiveAsync(buffer, CancellationToken.None);
                        if (r.MessageType == WebSocketMessageType.Close) { _closed.TrySetResult(r.CloseStatus); return; }
                        ms.Write(buffer, 0, r.Count);
                    } while (!r.EndOfMessage);
                    var bytes = ms.ToArray();
                    var text = r.MessageType == WebSocketMessageType.Binary ? BinaryFrame.HeaderText(bytes) : Encoding.UTF8.GetString(bytes);
                    if (JsonNode.Parse(text) is JsonObject o) lock (_received) _received.Add(o);
                }
            }
            catch (Exception) { _closed.TrySetResult(null); }
        }

        public JsonObject[] Received() { lock (_received) return [.. _received]; }
        public void Clear() { lock (_received) _received.Clear(); }

        public async Task<WebSocketCloseStatus?> ClosedWith()
        {
            var done = await Task.WhenAny(_closed.Task, Task.Delay(5000));
            Assert.True(done == _closed.Task, "The connection was not closed.");
            return await _closed.Task;
        }

        public async Task<JsonObject> Call(string method, JsonObject? p = null, bool wait = true)
        {
            var id = Interlocked.Increment(ref _id) + 1000;
            var msg = new JsonObject { ["id"] = id, ["method"] = method };
            if (p != null) msg["params"] = p.DeepClone();
            await Ws.SendAsync(Encoding.UTF8.GetBytes(msg.ToJsonString()), WebSocketMessageType.Text, true, CancellationToken.None);
            if (!wait) return [];
            return await Wait(m => m["id"] is JsonValue v && v.TryGetValue<int>(out var n) && n == id, $"the reply to {method}");
        }

        public async Task<JsonObject> Event(string name) => (await Wait(m => (string?)m["event"] == name, $"the event {name}"))["data"] as JsonObject ?? [];

        async Task<JsonObject> Wait(Func<JsonObject, bool> match, string what)
        {
            for (var i = 0; i < 500; i++)
            {
                lock (_received)
                {
                    var at = _received.FindIndex(m => match(m));
                    if (at >= 0) { var o = _received[at]; _received.RemoveAt(at); return o; }
                }
                await Task.Delay(10);
            }
            throw new TimeoutException($"No {what}.");
        }

        public async ValueTask DisposeAsync()
        {
            try { if (Ws.State == WebSocketState.Open) Ws.Abort(); } catch (Exception) { }
            Ws.Dispose();
            await Task.CompletedTask;
        }
    }

    /// <summary>One end of a connection in memory: reads one pipe, writes the other.</summary>
    sealed class Wire(PipeReader input, PipeWriter output) : Stream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => await output.WriteAsync(buffer, ct);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            while (true)
            {
                var r = await input.ReadAsync(ct);
                var n = (int)Math.Min(buffer.Length, r.Buffer.Length);
                r.Buffer.Slice(0, n).CopyTo(buffer.Span);
                input.AdvanceTo(r.Buffer.GetPosition(n));
                if (n > 0 || r.IsCompleted) return n;
            }
        }

        public override Task WriteAsync(byte[] b, int offset, int count, CancellationToken ct) => WriteAsync(b.AsMemory(offset, count), ct).AsTask();
        public override void Write(byte[] b, int offset, int count) => WriteAsync(b.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override Task<int> ReadAsync(byte[] b, int offset, int count, CancellationToken ct) => ReadAsync(b.AsMemory(offset, count), ct).AsTask();
        public override int Read(byte[] b, int offset, int count) => ReadAsync(b.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            output.Complete();
            input.Complete();
            base.Dispose(disposing);
        }
    }
}
