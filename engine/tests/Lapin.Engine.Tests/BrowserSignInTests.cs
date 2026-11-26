using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Package;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The desktop's sign-in through the browser (BrowserSignIn; OAuth 2.1 code + PKCE with a loopback answer, RFC 8252) against a fake
/// Atrium Server: the server's page asked with the challenge and the state, the browser's answer taken once for that state only, the
/// code exchanged with its verifier for the session's tokens, a denial and a stranger's issuer said, and a server without the flow.
/// </summary>
[Collection(nameof(BrowserSignInTests))]
public sealed class BrowserSignInTests
{
    const string Root = "http://127.0.0.1:5998";
    const string Callback = "http://127.0.0.1:14999/oauth/callback";

    sealed class FakeAtrium : HttpMessageHandler
    {
        public bool Offers = true;
        public string? Challenge;
        public List<string> TokenBodies { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Send(request, cancellationToken));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? "";
            HttpResponseMessage Json(HttpStatusCode status, JsonObject o) => new(status) { Content = new StringContent(o.ToJsonString(), Encoding.UTF8, "application/json") };
            switch (path)
            {
                case "/foundation/1.1/auth":
                    return Json(HttpStatusCode.OK, new JsonObject
                    {
                        ["oauth2_token_url"] = Root + "/foundation/oauth2/token",
                        ["supported_oauth2_flows"] = Offers ? new JsonArray("resource_owner_password_credentials_grant", "authorization_code_grant") : new JsonArray("resource_owner_password_credentials_grant"),
                    });
                case "/.well-known/oauth-authorization-server":
                    return Json(HttpStatusCode.OK, new JsonObject
                    {
                        ["issuer"] = Root, ["authorization_endpoint"] = Root + "/oauth/authorize", ["token_endpoint"] = Root + "/oauth/token",
                        ["scopes_supported"] = new JsonArray("cde.read", "cde.write"), ["authorization_response_iss_parameter_supported"] = true,
                    });
                case "/atrium/1.0/":
                    return Json(HttpStatusCode.OK, new JsonObject { ["api"] = "atrium", ["features"] = Offers ? new JsonArray("references", "oauth", "oauth.session") : new JsonArray("references", "oauth") });
                case "/foundation/oauth2/token":
                    TokenBodies.Add(body);
                    var form = body.Split('&').Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => Uri.UnescapeDataString(x[1].Replace('+', ' ')));
                    if (form["grant_type"] != "authorization_code" || form["code"] != "the-code" || form["redirect_uri"] != Callback || form["client_id"] != "lapin-desktop"
                        || BrowserSignIn.Challenge(form["code_verifier"]) != Challenge)
                        return Json(HttpStatusCode.BadRequest, new JsonObject { ["error"] = "invalid_grant", ["error_description"] = "The code is not valid." });
                    return Json(HttpStatusCode.OK, new JsonObject { ["access_token"] = "session-access", ["token_type"] = "bearer", ["expires_in"] = 3600, ["refresh_token"] = "session-refresh" });
                case "/foundation/1.1/current-user":
                    return request.Headers.Authorization?.Parameter == "session-access"
                        ? Json(HttpStatusCode.OK, new JsonObject { ["id"] = "5d2c8a4e-0000-4000-8000-000000000001", ["name"] = "Ana Popescu" })
                        : Json(HttpStatusCode.Unauthorized, new JsonObject { ["message"] = "Sign in." });
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
        return o["result"] as JsonObject ?? [];
    }

    static (EngineDispatcher D, FakeAtrium Server, List<string> Opened) Setup()
    {
        var opened = new List<string>();
        BrowserSignIn.CallbackUrl = Callback;
        BrowserSignIn.OpenBrowser = opened.Add;
        var server = new FakeAtrium();
        var d = new EngineDispatcher(new Fixture().Session, PackageFiles.Create()) { RemoteHttp = new HttpClient(server) };
        return (d, server, opened);
    }

    static Dictionary<string, string> Query(string url) =>
        new Uri(url).Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => Uri.UnescapeDataString(x[1]));

    [Fact]
    public void The_browser_signs_in_on_the_servers_page_and_the_code_becomes_the_sessions_tokens()
    {
        var (d, server, opened) = Setup();
        var start = Call(d, "remote.browserSignInStart", new JsonObject { ["url"] = Root });
        var id = (string)start["id"]!;
        // The server's own page opened in the browser, with PKCE's S256 challenge, the state and the loopback answer address.
        Assert.Equal((string)start["authorizeUrl"]!, Assert.Single(opened));
        Assert.StartsWith(Root + "/oauth/authorize?", opened[0]);
        var q = Query(opened[0]);
        Assert.Equal(("code", "lapin-desktop", Callback, "S256", "session", id), (q["response_type"], q["client_id"], q["redirect_uri"], q["code_challenge_method"], q["scope"], q["state"]));
        Assert.Equal(43, q["code_challenge"].Length);
        server.Challenge = q["code_challenge"];

        // Until the browser comes back, the sign-in waits.
        Assert.Equal("waiting", (string?)Call(d, "remote.browserSignInResult", new JsonObject { ["id"] = id })["status"]);
        // Another state is not taken; the right one is, once.
        Assert.False(BrowserSignIn.Arrive("someone-elses", "the-code", null, null, Root).Taken);
        Assert.True(BrowserSignIn.Arrive(id, "the-code", null, null, Root).Taken);
        Assert.False(BrowserSignIn.Arrive(id, "another-code", null, null, Root).Taken);

        var done = Call(d, "remote.browserSignInResult", new JsonObject { ["id"] = id });
        Assert.Equal("done", (string?)done["status"]);
        Assert.Equal(("session-access", "session-refresh"), ((string?)done["accessToken"], (string?)done["refreshToken"]));
        Assert.Equal("atrium:5d2c8a4e-0000-4000-8000-000000000001", (string?)done["user"]!["id"]);
        Assert.Equal("Ana Popescu", (string?)done["user"]!["name"]);
        // The verifier went with the code, and only there; the sign-in is then forgotten.
        Assert.Contains("code_verifier=", Assert.Single(server.TokenBodies));
        Assert.DoesNotContain(server.Challenge!, server.TokenBodies[0]);
        Assert.Equal("gone", (string?)Call(d, "remote.browserSignInResult", new JsonObject { ["id"] = id })["status"]);
    }

    [Fact]
    public void A_denial_and_an_answer_from_another_issuer_are_said_and_end_the_sign_in()
    {
        var (d, _, _) = Setup();
        var denied = (string)Call(d, "remote.browserSignInStart", new JsonObject { ["url"] = Root, ["open"] = false })["id"]!;
        Assert.True(BrowserSignIn.Arrive(denied, null, "access_denied", null, Root).Taken);
        var r = Call(d, "remote.browserSignInResult", new JsonObject { ["id"] = denied });
        Assert.Equal(("failed", "The sign-in was denied in the browser."), ((string?)r["status"], (string?)r["message"]));

        // The server says it names itself in the answer (RFC 9207): an answer without its issuer, or with another, is refused.
        var stranger = (string)Call(d, "remote.browserSignInStart", new JsonObject { ["url"] = Root, ["open"] = false })["id"]!;
        Assert.False(BrowserSignIn.Arrive(stranger, "the-code", null, null, "https://evil.example").Taken);
        Assert.Equal("failed", (string?)Call(d, "remote.browserSignInResult", new JsonObject { ["id"] = stranger })["status"]);
        var missing = (string)Call(d, "remote.browserSignInStart", new JsonObject { ["url"] = Root, ["open"] = false })["id"]!;
        Assert.False(BrowserSignIn.Arrive(missing, "the-code", null, null, null).Taken);

        // Cancelled: forgotten.
        var cancelled = (string)Call(d, "remote.browserSignInStart", new JsonObject { ["url"] = Root, ["open"] = false })["id"]!;
        Call(d, "remote.browserSignInCancel", new JsonObject { ["id"] = cancelled });
        Assert.Null(BrowserSignIn.Get(cancelled));
    }

    [Fact]
    public void A_sign_in_waits_ten_minutes_at_most()
    {
        var (d, _, _) = Setup();
        var now = DateTime.UtcNow;
        BrowserSignIn.Now = () => now;
        try
        {
            var id = (string)Call(d, "remote.browserSignInStart", new JsonObject { ["url"] = Root, ["open"] = false })["id"]!;
            now += TimeSpan.FromMinutes(11);
            Assert.Equal("gone", (string?)Call(d, "remote.browserSignInResult", new JsonObject { ["id"] = id })["status"]);
        }
        finally { BrowserSignIn.Now = () => DateTime.UtcNow; }
    }

    [Fact]
    public void A_server_without_the_flow_keeps_the_password_form()
    {
        var (d, server, opened) = Setup();
        server.Offers = false;
        var e = Assert.Throws<InvalidOperationException>(() => Call(d, "remote.browserSignInStart", new JsonObject { ["url"] = Root }));
        Assert.Contains("sign in with your password", e.Message);
        Assert.Empty(opened);
    }

    [Fact]
    public void The_challenge_is_PKCEs_S256()
    {
        // RFC 7636 Appendix B.
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", BrowserSignIn.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
        Assert.Equal(43, BrowserSignIn.Random32().Length);
    }
}
