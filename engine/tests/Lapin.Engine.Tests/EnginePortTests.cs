using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Lapin.Engine.Tests;

/// <summary>
/// The engine's port (docs/python.md, How a program finds Lapin): without one of its own, the first free of 14912,
/// 14913, …; a port named is that port. Servers like the host's (Kestrel on 127.0.0.1); whatever already holds a port
/// of the range on this computer (an open Lapin) only moves the expected ports on, so the test holds one itself.
/// </summary>
[Collection("EnginePort")]
public class EnginePortTests
{
    static WebApplication Server(int port)
    {
        var b = WebApplication.CreateSlimBuilder();
        b.Logging.SetMinimumLevel(LogLevel.Warning);
        b.WebHost.UseKestrel(o => o.Listen(IPAddress.Loopback, port));
        var app = b.Build();
        app.MapGet("/health", () => "ok");
        return app;
    }

    static TcpListener Hold(int port)
    {
        var l = new TcpListener(IPAddress.Loopback, port);
        l.Start();
        return l;
    }

    /// <summary>The free ports of the range, in order, as the engine looks at them.</summary>
    static List<int> Free(int from, int to) => Enumerable.Range(from, to - from + 1).Where(EnginePort.LooksFree).ToList();

    [Fact]
    public void Lapin_is_14912_and_the_range_is_a_hundred_ports()
    {
        Assert.Equal(14912, EnginePort.First);
        Assert.Equal(100, EnginePort.Count);
        Assert.Null(EnginePort.Parse(null));
        Assert.Null(EnginePort.Parse("0"));
        Assert.Null(EnginePort.Parse("auto"));
        Assert.Null(EnginePort.Parse("AUTO"));
        Assert.Equal(47810, EnginePort.Parse("47810"));
        Assert.Throws<ArgumentException>(() => EnginePort.Parse("x"));
        Assert.Throws<ArgumentException>(() => EnginePort.Parse("70000"));
    }

    [Fact]
    public async Task Two_engines_without_a_port_take_the_first_free_ones_from_14912()
    {
        // Hold the first free port of the range: the engines go past it, as past any program holding one.
        var held = Free(EnginePort.First, EnginePort.First + EnginePort.Count - 1)[0];
        using var _ = new DisposableListener(Hold(held));
        var expected = Free(EnginePort.First, EnginePort.First + EnginePort.Count - 1);
        Assert.DoesNotContain(held, expected);

        var a = await EnginePort.StartAsync(Server, null);
        try
        {
            var b = await EnginePort.StartAsync(Server, null);
            try
            {
                Assert.False(a.FellBack);
                Assert.False(b.FellBack);
                Assert.Equal(expected[0], a.Port);
                Assert.Equal(expected[1], b.Port);
                Assert.True(a.Port > held);
                Assert.InRange(b.Port, a.Port + 1, EnginePort.First + EnginePort.Count - 1);
                using var http = new HttpClient();
                Assert.Equal("ok", await http.GetStringAsync($"http://127.0.0.1:{b.Port}/health"));
            }
            finally { await b.App.DisposeAsync(); }
        }
        finally { await a.App.DisposeAsync(); }
    }

    [Fact]
    public async Task A_port_named_is_kept_exactly_and_a_taken_one_is_an_error()
    {
        int port;
        using (var probe = new DisposableListener(Hold(0))) port = ((IPEndPoint)probe.Listener.LocalEndpoint).Port;
        var s = await EnginePort.StartAsync(Server, port);
        try
        {
            Assert.Equal(port, s.Port);
            Assert.False(s.FellBack);
            await Assert.ThrowsAnyAsync<Exception>(() => EnginePort.StartAsync(Server, port));
        }
        finally { await s.App.DisposeAsync(); }
    }

    [Fact]
    public async Task A_full_range_falls_back_to_a_port_the_system_gives()
    {
        // A range of two ports, both held.
        using var one = new DisposableListener(Hold(0));
        var first = ((IPEndPoint)one.Listener.LocalEndpoint).Port;
        DisposableListener? two = null;
        try { two = new DisposableListener(Hold(first + 1)); }
        catch (SocketException) { }
        var count = two == null ? 1 : 2;
        var log = new StringWriter();
        var s = await EnginePort.StartAsync(Server, null, first, count, log);
        try
        {
            Assert.True(s.FellBack);
            Assert.NotEqual(first, s.Port);
            Assert.NotEqual(first + 1, s.Port);
            Assert.Contains("all taken", log.ToString());
        }
        finally { await s.App.DisposeAsync(); two?.Dispose(); }
    }

    sealed class Recorder : EngineHub.Client
    {
        public override Task SendBinary(byte[] frame) => Task.CompletedTask;
        public override Task SendText(string text) => Task.CompletedTask;
        protected override void Drop() { }
    }

    [Fact]
    public async Task The_hello_says_lapin_and_who_runs_it()
    {
        var f = new Fixture();
        var hub = new EngineHub(f.Session, PackageFiles.Create()) { Port = 14912 };
        var h = hub.Hello();
        Assert.Equal("lapin", (string?)h["engine"]);
        Assert.Equal(f.Session.Product.Id, (string?)h["product"]);
        Assert.Equal(14912, (int?)h["port"]);
        Assert.Equal(Environment.ProcessId, (int?)h["pid"]);
        Assert.True(h.ContainsKey("project"));
        Assert.True(EngineHub.LoopbackHost("127.0.0.1"));
        Assert.True(EngineHub.LoopbackHost("localhost"));
        Assert.False(EngineHub.LoopbackHost("evil.example"));
        // The same over the protocol, past the calls' queue.
        var client = new Recorder();
        hub.Add(client);
        var reply = BinaryFrame.Decode((await hub.Handle("{\"id\":1,\"method\":\"engine.hello\"}", client))!).Header.RootElement;
        Assert.Equal("lapin", reply.GetProperty("result").GetProperty("engine").GetString());
        Assert.Equal(14912, reply.GetProperty("result").GetProperty("port").GetInt32());
    }

    [Fact]
    public void Each_engine_has_its_host_file_and_host_json_names_the_latest()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lapin-hosts-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "hosts"));
            // A file left by an engine that is gone: removed when another starts.
            var stale = Path.Combine(dir, "hosts", "host-999999.json");
            File.WriteAllText(stale, "{\"port\":14913,\"pid\":999999}");
            var f = new EngineHostFiles(dir, 14912, "lapin");
            f.Write();
            Assert.True(File.Exists(f.Own));
            Assert.False(File.Exists(stale));
            var h = (JsonObject)JsonNode.Parse(File.ReadAllText(f.Latest))!;
            Assert.Equal(14912, (int?)h["port"]);
            Assert.Equal(Environment.ProcessId, (int?)h["pid"]);
            f.Remove();
            Assert.False(File.Exists(f.Own));
            Assert.False(File.Exists(f.Latest));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    sealed class DisposableListener(TcpListener l) : IDisposable
    {
        public TcpListener Listener => l;
        public void Dispose() => l.Stop();
    }
}
