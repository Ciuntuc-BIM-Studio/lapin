using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Platform;
using Lapin.Protocol;
using Xunit;

namespace Lapin.Engine.Tests;

/// <summary>
/// Lapin Extensions, stage 0 (docs/plan-extensions.md): the desktop's interface reads the catalogue and its packages through
/// the host (the webview may reach no other site, and GitHub's releases answer no browser): https anywhere, http only on this
/// computer, a local catalogue's files by path; never a guest of a shared session. The SHA-256 is checked by the interface.
/// </summary>
public class ExtensionFetchTests
{
    static string Temp(string name, byte[] bytes)
    {
        var dir = Path.Combine(Path.GetTempPath(), "lapin-ext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public async Task A_local_catalogues_file_is_read_by_its_path_or_file_address()
    {
        var path = Temp("index.json", Encoding.UTF8.GetBytes("{\"format\":\"lapin-extensions-index\"}"));
        Assert.Equal("{\"format\":\"lapin-extensions-index\"}", Encoding.UTF8.GetString(await ExtensionFetch.Fetch(path)));
        Assert.Equal(await ExtensionFetch.Fetch(path), await ExtensionFetch.Fetch(new Uri(path).AbsoluteUri));
    }

    [Fact]
    public async Task Only_https_or_this_computer_is_reached()
    {
        var ex = await Assert.ThrowsAsync<CommandException>(() => ExtensionFetch.Fetch("http://example.com/index.json"));
        Assert.Equal("Extensions are read from https addresses (or files on this computer).", ex.Message);
        await Assert.ThrowsAsync<CommandException>(() => ExtensionFetch.Fetch("ftp://example.com/a.pat"));
        await Assert.ThrowsAsync<CommandException>(() => ExtensionFetch.Fetch("relative/index.json"));
        await Assert.ThrowsAsync<CommandException>(() => ExtensionFetch.Fetch(""));
        Assert.Equal("There is no such file: /nowhere/lapin-index.json.", (await Assert.ThrowsAsync<CommandException>(() => ExtensionFetch.Fetch("/nowhere/lapin-index.json"))).Message);
    }

    [Fact]
    public async Task A_file_bigger_than_an_extension_may_be_is_refused()
    {
        var path = Temp("big.lapinlib", new byte[2048]);
        var ex = await Assert.ThrowsAsync<CommandException>(() => ExtensionFetch.Fetch(path, max: 1024));
        Assert.Equal("The file is bigger than an extension may be (1 KB).", ex.Message);
    }

    [Fact]
    public async Task Http_on_this_computer_is_read_for_a_catalogue_served_while_developing()
    {
        // A tiny server on 127.0.0.1 answering one request.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serve = Task.Run(async () =>
        {
            using var c = await listener.AcceptTcpClientAsync();
            var s = c.GetStream();
            var buf = new byte[4096];
            _ = await s.ReadAsync(buf);
            var body = "*SOLID, solid\n45, 0,0, 0,1\n"u8.ToArray();
            await s.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"));
            await s.WriteAsync(body);
        });
        var bytes = await ExtensionFetch.Fetch($"http://127.0.0.1:{port}/pack.pat");
        await serve;
        listener.Stop();
        Assert.StartsWith("*SOLID", Encoding.UTF8.GetString(bytes));
    }

    sealed class Guest : EngineHub.Client
    {
        public override Task SendBinary(byte[] frame) => Task.CompletedTask;
        public override Task SendText(string text) => Task.CompletedTask;
        protected override void Drop() { }
    }

    static System.Text.Json.JsonElement Header(byte[] frame) => BinaryFrame.Decode(frame).Header.RootElement;

    [Fact]
    public async Task The_host_answers_extensions_fetch_and_source_for_its_own_interface_only()
    {
        var path = Temp("a.pat", "*X, x\n0, 0,0, 0,1\n"u8.ToArray());
        var hub = new EngineHub(new Fixture().Session, PackageFiles.Create());
        var call = new JsonObject { ["id"] = 1, ["method"] = "extensions.fetch", ["params"] = new JsonObject { ["url"] = path } }.ToJsonString();
        var r = Header((await hub.Handle(call))!);
        Assert.Equal("*X, x\n0, 0,0, 0,1\n", Encoding.UTF8.GetString(Convert.FromBase64String(r.GetProperty("result").GetProperty("data").GetString()!)));
        var source = Header((await hub.Handle("""{"id":2,"method":"extensions.source"}"""))!);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, source.GetProperty("result").ValueKind);
        var guest = new Guest { Caller = new Caller("guest-1", "editor") };
        Assert.Equal("Only the host can do that.", Header((await hub.Handle(call, guest))!).GetProperty("error").GetString());
    }

    /// <summary>A tiny server on 127.0.0.1 answering one request with <paramref name="status"/> and <paramref name="body"/>; what it was sent.</summary>
    static (int Port, Task<string> Request) ServeOnce(int status, string type, byte[] body)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var request = Task.Run(async () =>
        {
            using var c = await listener.AcceptTcpClientAsync();
            var s = c.GetStream();
            var got = new StringBuilder();
            var buf = new byte[8192];
            // Headers, then the body its Content-Length says.
            while (true)
            {
                var n = await s.ReadAsync(buf);
                if (n == 0) break;
                got.Append(Encoding.UTF8.GetString(buf, 0, n));
                var text = got.ToString();
                var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (end < 0) continue;
                var m = System.Text.RegularExpressions.Regex.Match(text, "Content-Length: (\\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!m.Success || Encoding.UTF8.GetByteCount(text[(end + 4)..]) >= int.Parse(m.Groups[1].Value)) break;
            }
            await s.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status} X\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"));
            await s.WriteAsync(body);
            listener.Stop();
            return got.ToString();
        });
        return (port, request);
    }

    [Fact]
    public async Task A_licence_key_is_posted_in_the_body_and_the_stores_answer_comes_back_whatever_its_status()
    {
        var (port, request) = ServeOnce(403, "application/json", "{\"status\":\"revoked\"}"u8.ToArray());
        var (status, type, body) = await ExtensionFetch.Post($"http://127.0.0.1:{port}/api/licensed/ana.x/1.0.0", "{\"key\":\"K-123\"}");
        var sent = await request;
        Assert.Equal(403, status);
        Assert.Equal("application/json", type);
        Assert.Equal("{\"status\":\"revoked\"}", Encoding.UTF8.GetString(body));
        Assert.StartsWith("POST /api/licensed/ana.x/1.0.0 HTTP/1.1", sent);
        Assert.EndsWith("{\"key\":\"K-123\"}", sent);
        Assert.DoesNotContain("K-123", sent.Split("\r\n")[0]);
    }

    [Fact]
    public async Task A_licence_key_goes_to_https_or_this_computer_only_and_the_host_refuses_a_guest()
    {
        Assert.Equal("A licence key is sent to https addresses only.", (await Assert.ThrowsAsync<CommandException>(() => ExtensionFetch.Post("http://example.com/api/licence/check", "{}"))).Message);
        await Assert.ThrowsAsync<CommandException>(() => ExtensionFetch.Post("/tmp/x", "{}"));
        await Assert.ThrowsAsync<CommandException>(() => ExtensionFetch.Post("https://store.lapinbim.com/api/licence/check", new string('x', 5000)));
        var (port, request) = ServeOnce(200, "application/octet-stream", "*X, x\n"u8.ToArray());
        var hub = new EngineHub(new Fixture().Session, PackageFiles.Create());
        var call = new JsonObject { ["id"] = 1, ["method"] = "extensions.post", ["params"] = new JsonObject { ["url"] = $"http://127.0.0.1:{port}/api/licensed/a/1.0.0", ["body"] = "{\"key\":\"K\"}" } }.ToJsonString();
        var guest = new Guest { Caller = new Caller("guest-1", "editor") };
        Assert.Equal("Only the host can do that.", Header((await hub.Handle(call, guest))!).GetProperty("error").GetString());
        var r = Header((await hub.Handle(call))!).GetProperty("result");
        await request;
        Assert.Equal(200, r.GetProperty("status").GetInt32());
        Assert.Equal("*X, x\n", Encoding.UTF8.GetString(Convert.FromBase64String(r.GetProperty("data").GetString()!)));
    }

    [Fact]
    public void An_extensions_page_is_opened_only_at_an_https_address()
    {
        Assert.Equal("https://forum.lapinbim.com/t/mobilier/42", Support.LinkUrl("extensionPage", new JsonObject { ["url"] = "https://forum.lapinbim.com/t/mobilier/42" }));
        Assert.Throws<CommandException>(() => Support.LinkUrl("extensionPage", new JsonObject { ["url"] = "file:///tmp/a.txt" }));
        Assert.Throws<CommandException>(() => Support.LinkUrl("extensionPage", new JsonObject { ["url"] = "http://example.com" }));
        Assert.Throws<CommandException>(() => Support.LinkUrl("extensionPage"));
    }
}
