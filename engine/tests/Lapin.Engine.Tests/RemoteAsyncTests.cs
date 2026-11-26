using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Package;
using Lapin.Package.Remote;
using Lapin.Protocol;
using static Lapin.Engine.Tests.RemoteVectorTests;

namespace Lapin.Engine.Tests;

/// <summary>
/// The remote's asynchronous path (Atrium docs/plan-cde-web.md §2.1 A, stage W2): the client runs where <see cref="HttpClient"/> has
/// no synchronous <c>Send</c> and every answer comes later, as in the browser's engine (fetch in a single-threaded worker). The push and
/// fetch sessions of the vectors and a dispatcher call through <see cref="EngineDispatcher.HandleAsync(string)"/> pass over such a client;
/// every other call ends inside <see cref="EngineDispatcher.HandleAsync(string)"/>, as the browser's queue of calls expects.
/// </summary>
public sealed class RemoteAsyncTests
{
    /// <summary>A client as the browser's: <c>Send</c> refused, each answer after a yield (never on the caller's stack).</summary>
    sealed class BrowserLike(HttpMessageHandler inner) : HttpMessageHandler
    {
        readonly HttpMessageInvoker _inner = new(inner);
        public int Calls;

        /// <summary>
        /// When set, the server answers only once the test lets it: after the yield the answer is made on another thread, and could end
        /// the call before the caller looks at it (issue #122). Bounded: a call that held up its caller fails the test instead of hanging it.
        /// </summary>
        public TaskCompletionSource? Hold { get; init; }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new PlatformNotSupportedException("No synchronous HTTP in the browser.");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            await Task.Yield();
            if (Hold is { } hold) await hold.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return await _inner.SendAsync(request, cancellationToken);
        }
    }

    [Fact]
    public async Task The_push_session_runs_without_synchronous_http()
    {
        var server = VectorServer.Of("push");
        server.MayStayUnused(7);
        server.BreakAfter = (req, n) => req.Method == HttpMethod.Patch && n == 0;
        var h = Story("document.json", "relations.arrow", "T1", "c1", "walls.arrow", "hello.py", "T2", "c2", "issue.pdf", "tag-issue");
        h.Refs[RefNames.Main] = Id("c2");
        h.Refs["refs/tags/issue/1-P01"] = Id("tag-issue");
        h.Seen["origin"] = new RemoteSeen { Etag = "\"r-7\"" };
        h.Seen["origin"].Refs[RefNames.Main] = Id("c1");
        var browser = new BrowserLike(server);
        using var http = new HttpClient(browser);
        var remote = new HttpRemote(http, "http://cde.test", "/lapin-remote/1.0", Project, new RemoteTokens { Access = "test-access-token-mihai" })
        {
            UseUpload = (id, _) => id == Id("walls.arrow"),
            FirstPatchLimit = 100,
        };
        Assert.Equal("owner", (await remote.ProjectAsync()).Role);
        var o = await RemoteOps.PushAsync(remote, h, "origin", [(RefNames.Main, RefNames.Main), ("refs/tags/issue/1-P01", "refs/tags/issue/1-P01")], DateTime.UtcNow);
        Assert.True(o.Pushed);
        server.AssertDone();
        Assert.Equal([Id("c2"), Id("T2"), Id("walls.arrow"), Id("hello.py"), Id("tag-issue")], o.Uploaded);
        Assert.True(browser.Calls > 5);
    }

    [Fact]
    public async Task The_fetch_session_runs_without_synchronous_http()
    {
        var server = VectorServer.Of("fetch");
        server.Add(VectorServer.Load("push", 1));
        server.MayStayUnused(100);
        server.PackSubsets = true;
        var h = Story("document.json", "relations.arrow", "T1", "c1");
        h.Refs[RefNames.Main] = Id("c1");
        h.Seen["origin"] = new RemoteSeen { Etag = "\"r-7\"" };
        h.Seen["origin"].Refs[RefNames.Main] = Id("c1");
        using var http = new HttpClient(new BrowserLike(server));
        var remote = new HttpRemote(http, "http://cde.test", "http://cde.test/lapin-remote/1.0", Project, new RemoteTokens { Access = "test-access-token-viewer" });
        var f = await RemoteOps.FetchAsync(remote, h, "origin", new RemoteAuthor("atrium:viewer", "Viewer"), DateTime.UtcNow);
        Assert.Equal((1, 1, 3, 1), (f.Commits, f.Trees, f.Blobs, f.Tags));
        Assert.Equal((PullKind.FastForward, Id("c1"), Id("c2")), RemoteOps.Stand(h, "origin"));
    }

    static JsonObject Result(byte[] reply)
    {
        var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
        if (o["error"] is { } err) throw new InvalidOperationException((string)err!);
        return (JsonObject)o["result"]!;
    }

    static string Message(string method, JsonObject p) => new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString();

    [Fact]
    public async Task A_remote_call_is_awaited_by_the_dispatcher_and_its_tokens_come_back()
    {
        var server = new RemotePasswordTests.FakeAtrium { Access = "server-forgot-it" };
        var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new BrowserLike(server) { Hold = answer });
        var d = new EngineDispatcher(new Fixture().Session, PackageFiles.Create()) { RemoteHttp = http };
        var call = d.HandleAsync(Message("remote.changePassword", new JsonObject
        {
            ["url"] = "http://127.0.0.1:5999", ["currentPassword"] = "temporary1", ["newPassword"] = "my own password", ["token"] = "old-access", ["refreshToken"] = "old-refresh",
        }));
        // The answer waits for the server: the call has not ended on the caller's stack.
        Assert.False(call.IsCompleted);
        answer.SetResult();
        var r = Result((await call).Reply);
        Assert.True((bool)r["changed"]!);
        Assert.Equal("fresh-access", (string?)r["tokens"]!["accessToken"]);
        Assert.Equal("my own password", server.Password);
    }

    [Fact]
    public async Task Every_other_call_ends_inside_HandleAsync()
    {
        var d = new EngineDispatcher(new Fixture().Session, PackageFiles.Create());
        var status = d.HandleAsync(Message("history.status", []));
        Assert.True(status.IsCompletedSuccessfully);
        Assert.Equal("refs/heads/main", (string?)Result((await status).Reply)["branch"]);
        Assert.True(d.HandleAsync(Message("history.log", [])).IsCompletedSuccessfully);
    }

    [Fact]
    public async Task A_server_out_of_reach_is_said_not_an_engine_error()
    {
        using var http = new HttpClient(new BrowserLike(new Unreachable()));
        var d = new EngineDispatcher(new Fixture().Session, PackageFiles.Create()) { RemoteHttp = http };
        var (reply, _) = await d.HandleAsync(Message("remote.projects", new JsonObject { ["kind"] = "server", ["url"] = "http://127.0.0.1:5999" }));
        var error = (string?)JsonNode.Parse(BinaryFrame.HeaderText(reply))!["error"];
        Assert.StartsWith("The server could not be reached", error, StringComparison.Ordinal);
    }

    sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused (test)."));
    }
}
