using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Host;

namespace Lapin.Engine.Tests;

/// <summary>The wire: the hub answers in binary frames whose meshes are raw arrays, referenced from the JSON header.</summary>
public class ProtocolTests
{
    [Fact]
    public async Task A_reply_is_a_frame_with_its_meshes_as_buffers()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0);
        var expected = f.Session.Snapshot().Render.Single(r => r.NodeId == wall);
        var hub = new EngineHub(f.Session, PackageFiles.Create());

        var frame = await hub.Handle("""{"id":7,"method":"doc.snapshot"}""");
        var (header, buffers) = BinaryFrame.Decode(frame);
        var root = header.RootElement;
        Assert.Equal(7, root.GetProperty("id").GetInt32());
        var item = root.GetProperty("result").GetProperty("render").EnumerateArray().Single(r => r.GetProperty("nodeId").GetString() == wall);
        var parts = item.GetProperty("parts").EnumerateArray().ToList();
        Assert.Equal(expected.Parts!.Count, parts.Count);
        for (var i = 0; i < parts.Count; i++)
        {
            var positions = parts[i].GetProperty("positions");
            Assert.Equal("f32", positions.GetProperty("t").GetString());
            Assert.Equal(0, positions.GetProperty("$buf").GetInt32() % 4);
            Assert.Equal(expected.Parts[i].Positions, BinaryFrame.Floats(positions, buffers));
            Assert.Equal(expected.Parts[i].Indices, BinaryFrame.Ints(parts[i].GetProperty("indices"), buffers));
            Assert.Equal(expected.Parts[i].Normals, BinaryFrame.Floats(parts[i].GetProperty("normals"), buffers));
        }
        // The header is padded to 4 bytes, so every buffer view is aligned in the frame too.
        Assert.Equal(0, (frame.Length - buffers.Length) % 4);
    }

    [Fact]
    public async Task An_error_comes_back_with_its_call_id()
    {
        var hub = new EngineHub(new Fixture().Session, PackageFiles.Create());
        var (header, _) = BinaryFrame.Decode(await hub.Handle("""{"id":3,"method":"doc.execute","params":{"command":"architecture.flipWall","args":{"id":"nope"}}}"""));
        Assert.Equal(3, header.RootElement.GetProperty("id").GetInt32());
        Assert.Contains("no IfcWall", header.RootElement.GetProperty("error").GetString());
        (header, _) = BinaryFrame.Decode(await hub.Handle("""{"id":4,"method":"nothing"}"""));
        Assert.Contains("Unknown method", header.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void Plain_numbers_stay_json_only_arrays_of_floats_and_ints_become_buffers()
    {
        var frame = BinaryFrame.Encode(new { a = 1.5, list = new[] { 1.0, 2.0 }, floats = new[] { 1f, 2f, 3f }, ints = new[] { 4, 5 } });
        var (header, buffers) = BinaryFrame.Decode(frame);
        var r = header.RootElement;
        Assert.Equal(1.5, r.GetProperty("a").GetDouble());
        Assert.Equal(JsonValueKind.Array, r.GetProperty("list").ValueKind);
        Assert.Equal([1f, 2f, 3f], BinaryFrame.Floats(r.GetProperty("floats"), buffers));
        Assert.Equal([4, 5], BinaryFrame.Ints(r.GetProperty("ints"), buffers));
        Assert.Equal(12, r.GetProperty("ints").GetProperty("$buf").GetInt32());
        _ = JsonNode.Parse(BinaryFrame.HeaderText(frame));
    }

    // ------------------------------------------------------------ two clients on one hub

    [Fact]
    public async Task A_reply_and_a_broadcast_due_together_on_one_socket_take_turns()
    {
        var f = new Fixture();
        for (var i = 0; i < 4; i++) f.Wall(0, i, 5, i);   // a snapshot large enough to be mid-send for a while
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var a = Link.Connect(pauseAfter: 1024);   // the interface: a slow reader
        var b = Link.Connect();                    // the MCP bridge
        var serving = Task.WhenAll(hub.Serve(a.Server, cts.Token), hub.Serve(b.Server, cts.Token));

        // A asks for the snapshot and does not read yet: its reply stalls on the wire, mid-send.
        await Call(a.Client, """{"id":1,"method":"doc.snapshot"}""", cts.Token);
        while (!a.ServerSide.Sending) await Task.Delay(10, cts.Token);

        // B adds a wall meanwhile: its diff is for A too, whose socket is busy with the reply.
        await Call(b.Client, $$$$"""{"id":2,"method":"doc.execute","params":{"command":"architecture.addWall","args":{"storeyId":"{{{{f.Storey}}}}","x1":0,"y1":9,"x2":5,"y2":9}}}""", cts.Token);
        await Task.Delay(200, cts.Token);
        var fromA = Task.Run(async () => new[] { await Receive(a.Client, cts.Token), await Receive(a.Client, cts.Token) });

        // B's command applied and is answered without error, after its diff; A gets its reply, then the diff.
        Assert.Equal("diff", (await Receive(b.Client, cts.Token)).RootElement.GetProperty("event").GetString());
        var reply = (await Receive(b.Client, cts.Token)).RootElement;
        Assert.Equal(2, reply.GetProperty("id").GetInt32());
        Assert.False(reply.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null, reply.ToString());
        var toA = await fromA;
        Assert.Equal(1, toA[0].RootElement.GetProperty("id").GetInt32());
        Assert.Equal("diff", toA[1].RootElement.GetProperty("event").GetString());
        Assert.Equal(5, f.Model.OfClass(Lapin.Model.IfcClass.Wall).Count());

        cts.Cancel();
        await serving;
    }

    [Fact]
    public async Task A_peer_whose_wire_broke_fails_neither_the_caller_nor_the_others()
    {
        var f = new Fixture();
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var a = Link.Connect();
        var b = Link.Connect();
        var serving = Task.WhenAll(hub.Serve(a.Server, cts.Token), hub.Serve(b.Server, cts.Token));

        // A's wire breaks without a close frame: the hub still lists it when B's diff goes out.
        a.ServerSide.Fail();
        await Call(b.Client, $$$$"""{"id":2,"method":"doc.execute","params":{"command":"architecture.addWall","args":{"storeyId":"{{{{f.Storey}}}}","x1":0,"y1":0,"x2":5,"y2":0}}}""", cts.Token);
        Assert.Equal("diff", (await Receive(b.Client, cts.Token)).RootElement.GetProperty("event").GetString());
        var reply = (await Receive(b.Client, cts.Token)).RootElement;
        Assert.Equal(2, reply.GetProperty("id").GetInt32());
        Assert.False(reply.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null, reply.ToString());

        cts.Cancel();
        await serving;
    }

    static Task Call(WebSocket ws, string json, CancellationToken ct) =>
        ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);

    /// <summary>The next frame's header.</summary>
    static async Task<JsonDocument> Receive(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[1 << 16];
        using var ms = new MemoryStream();
        WebSocketReceiveResult r;
        do
        {
            r = await ws.ReceiveAsync(buffer, ct);
            ms.Write(buffer, 0, r.Count);
        } while (!r.EndOfMessage);
        return BinaryFrame.Decode(ms.ToArray()).Header;
    }

    /// <summary>
    /// A connection in memory: the two sockets Kestrel and a browser would hold,
    /// over a pair of pipes. The same socket class as in the host, so what it
    /// refuses (two sends at once) is refused here too.
    /// </summary>
    sealed record Link(WebSocket Server, WebSocket Client, Wire ServerSide)
    {
        /// <param name="pauseAfter">Bytes the client leaves unread before the server's send stalls.</param>
        public static Link Connect(long pauseAfter = 1 << 16)
        {
            var toClient = new Pipe(new PipeOptions(pauseWriterThreshold: pauseAfter, resumeWriterThreshold: pauseAfter / 2, useSynchronizationContext: false));
            var toServer = new Pipe(new PipeOptions(useSynchronizationContext: false));
            var serverSide = new Wire(toServer.Reader, toClient.Writer);
            var clientSide = new Wire(toClient.Reader, toServer.Writer);
            return new Link(
                WebSocket.CreateFromStream(serverSide, isServer: true, null, TimeSpan.Zero),
                WebSocket.CreateFromStream(clientSide, isServer: false, null, TimeSpan.Zero),
                serverSide);
        }
    }

    /// <summary>One end of a connection: reads one pipe, writes the other. <see cref="Fail"/> breaks it, as a peer gone without a word does.</summary>
    sealed class Wire(PipeReader input, PipeWriter output) : Stream
    {
        volatile bool _failed;
        int _writesInFlight;

        /// <summary>A write is under way: the reader has not taken it yet.</summary>
        public bool Sending => Volatile.Read(ref _writesInFlight) > 0;

        public void Fail() => _failed = true;

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            if (_failed) throw new IOException("The peer is gone.");
            Interlocked.Increment(ref _writesInFlight);
            try { await output.WriteAsync(buffer, ct); }
            finally { Interlocked.Decrement(ref _writesInFlight); }
        }

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
