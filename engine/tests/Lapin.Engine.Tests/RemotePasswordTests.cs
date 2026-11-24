using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Package;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// <c>remote.changePassword</c> against a fake Atrium Server (POST /atrium/1.0/account/password): the fresh pair handed back, a 401
/// refreshed once on the way, the refusals answered (not thrown) with their code and the tokens a refresh replaced, and an older
/// server without the endpoint.
/// </summary>
public sealed class RemotePasswordTests
{
    const string Root = "http://127.0.0.1:5999";

    internal sealed class FakeAtrium : HttpMessageHandler
    {
        public string Access = "old-access";
        public string Password = "temporary1";
        public bool HasEndpoint = true;
        public List<(string Path, string? Auth, string Body)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Send(request, cancellationToken));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? "";
            Requests.Add((path, request.Headers.Authorization?.Parameter, body));
            HttpResponseMessage Json(HttpStatusCode status, JsonObject o) => new(status) { Content = new StringContent(o.ToJsonString(), Encoding.UTF8, "application/json") };
            switch (path)
            {
                case "/foundation/versions":
                    return Json(HttpStatusCode.OK, new JsonObject { ["versions"] = new JsonArray(new JsonObject { ["api_id"] = "lapin-remote", ["version_id"] = "1.0", ["api_base_url"] = Root + "/lapin-remote/1.0" }) });
                case "/lapin-remote/1.0/capabilities":
                    return Json(HttpStatusCode.OK, new JsonObject { ["protocol"] = "lapin-remote", ["version"] = "1.0", ["majors"] = new JsonArray(1), ["features"] = new JsonArray("uploads", "pack"), ["auth"] = new JsonObject { ["foundation"] = "/foundation/1.1" } });
                case "/foundation/1.1/auth":
                    return Json(HttpStatusCode.OK, new JsonObject { ["oauth2_token_url"] = Root + "/foundation/oauth2/token", ["supported_oauth2_flows"] = new JsonArray("resource_owner_password_credentials_grant") });
                case "/foundation/oauth2/token" when body.Contains("refresh_token=old-refresh", StringComparison.Ordinal):
                    Access = "refreshed-access";
                    return Json(HttpStatusCode.OK, new JsonObject { ["access_token"] = "refreshed-access", ["refresh_token"] = "refreshed-refresh" });
                case "/atrium/1.0/account/password" when HasEndpoint:
                    if (request.Headers.Authorization?.Parameter != Access) return Json(HttpStatusCode.Unauthorized, new JsonObject { ["code"] = "unauthorized", ["message"] = "Sign in first." });
                    var o = (JsonObject)JsonNode.Parse(body)!;
                    if ((string?)o["currentPassword"] != Password)
                    {
                        return Json(HttpStatusCode.Forbidden, new JsonObject { ["code"] = "wrong_password", ["message"] = "The current password is wrong." });
                    }
                    Password = (string)o["newPassword"]!;
                    Access = "fresh-access";
                    return Json(HttpStatusCode.OK, new JsonObject { ["access_token"] = "fresh-access", ["token_type"] = "bearer", ["expires_in"] = 3600, ["refresh_token"] = "fresh-refresh" });
                default:
                    return Json(HttpStatusCode.NotFound, new JsonObject { ["message"] = "Not found." });
            }
        }
    }

    static JsonObject Call(EngineDispatcher d, string method, JsonObject p)
    {
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
        var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
        if (o["error"] is { } err) throw new InvalidOperationException((string)err!);
        return (JsonObject)o["result"]!;
    }

    static JsonObject Change(EngineDispatcher d, string current, string next, string token = "old-access") => Call(d, "remote.changePassword", new JsonObject
    {
        ["url"] = Root, ["currentPassword"] = current, ["newPassword"] = next, ["token"] = token, ["refreshToken"] = "old-refresh",
    });

    [Fact]
    public void Changed_and_the_fresh_pair_handed_back()
    {
        var server = new FakeAtrium();
        using var http = new HttpClient(server);
        var d = new EngineDispatcher(new Fixture().Session, PackageFiles.Create()) { RemoteHttp = http };

        var r = Change(d, "temporary1", "my own password");
        Assert.True((bool)r["changed"]!);
        Assert.Equal("fresh-access", (string?)r["tokens"]!["accessToken"]);
        Assert.Equal("fresh-refresh", (string?)r["tokens"]!["refreshToken"]);
        Assert.Equal("my own password", server.Password);
        var post = server.Requests.Single(q => q.Path == "/atrium/1.0/account/password");
        Assert.Equal("old-access", post.Auth);
        Assert.Equal("""{"currentPassword":"temporary1","newPassword":"my own password"}""", post.Body);
    }

    [Fact]
    public void An_expired_access_token_is_refreshed_once_on_the_way()
    {
        var server = new FakeAtrium { Access = "server-forgot-it" };
        using var http = new HttpClient(server);
        var d = new EngineDispatcher(new Fixture().Session, PackageFiles.Create()) { RemoteHttp = http };

        var r = Change(d, "temporary1", "my own password");
        Assert.True((bool)r["changed"]!);
        Assert.Equal("fresh-access", (string?)r["tokens"]!["accessToken"]);
        Assert.Equal(2, server.Requests.Count(q => q.Path == "/atrium/1.0/account/password"));
    }

    [Fact]
    public void A_wrong_password_is_an_answer_with_the_tokens_a_refresh_replaced()
    {
        var server = new FakeAtrium { Access = "server-forgot-it" };
        using var http = new HttpClient(server);
        var d = new EngineDispatcher(new Fixture().Session, PackageFiles.Create()) { RemoteHttp = http };

        var r = Change(d, "not it", "my own password");
        Assert.False((bool)r["changed"]!);
        Assert.Equal("wrong_password", (string?)r["code"]);
        Assert.Equal("The current password is wrong.", (string?)r["message"]);
        Assert.Equal("refreshed-refresh", (string?)r["tokens"]!["refreshToken"]);
        Assert.Equal("temporary1", server.Password);
    }

    [Fact]
    public void An_older_server_and_a_short_password()
    {
        var server = new FakeAtrium { HasEndpoint = false };
        using var http = new HttpClient(server);
        var d = new EngineDispatcher(new Fixture().Session, PackageFiles.Create()) { RemoteHttp = http };

        var r = Change(d, "temporary1", "my own password");
        Assert.False((bool)r["changed"]!);
        Assert.Equal("not_supported", (string?)r["code"]);
        Assert.Null(r["tokens"]);

        var e = Assert.Throws<InvalidOperationException>(() => Change(d, "temporary1", "short"));
        Assert.Contains("8 characters", e.Message);
        Assert.DoesNotContain(server.Requests, q => q.Path == "/atrium/1.0/account/password" && q.Body.Contains("short", StringComparison.Ordinal));
    }
}
