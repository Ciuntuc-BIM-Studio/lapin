using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Package;
using Lapin.Platform.Documents;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The project's information synced with Atrium Server's project information record (<c>remote.syncProjectInfo</c>; docs/plan-archicad.md
/// A1b; Atrium docs/atrium-server.md, "Project information (A1b)") against a fake server that keeps the record as the server does: Lapin's
/// fields under <c>fields</c>, the project's own parameters by key under <c>custom</c>, a JSON merge patch with If-Match and 412
/// <c>changed</c>, viewers refused. A first sync sends what the server lacks and takes the rest; another user's change comes in one undo step;
/// two users' edits of different fields are both kept, of one field the last writer's; a 412 reads again and sends again; parameters map
/// list ↔ map by key, Lapin's order kept; <c>source</c> is never sent; an older server is left alone.
/// </summary>
public sealed class ProjectInfoSyncTests
{
    const string Root = "http://127.0.0.1:5996";
    const string Project = "2c2d3e4f-1111-4222-8333-944455556666";

    sealed class FakeInfo : HttpMessageHandler
    {
        public readonly Dictionary<string, (string Name, string Role)> Tokens = new()
        {
            ["tok-mihai"] = ("Mihai Ionescu", "owner"), ["tok-ana"] = ("Ana Popescu", "editor"), ["tok-vio"] = ("Victor Viewer", "viewer"),
        };
        public bool Offered = true;
        public JsonObject Record = new() { ["fields"] = new JsonObject(), ["custom"] = new JsonObject(), ["source"] = new JsonObject() };
        public string? ModifiedBy;
        public int Patches, Refused412;
        public readonly List<JsonObject> Bodies = [];
        public Action? BeforePatch;

        public string ETag() => "\"" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Record.ToJsonString())))[..16].ToLowerInvariant() + "\"";

        JsonObject Shown()
        {
            var o = (JsonObject)Record.DeepClone();
            o["keys"] = new JsonArray([.. ProjectInfoSync.FieldKeys.Select(k => (JsonNode)k)]);
            o["etag"] = ETag();
            o["modifiedByName"] = ModifiedBy;
            o["openProposals"] = 0;
            return o;
        }

        static HttpResponseMessage Json(HttpStatusCode status, JsonNode? o, string? etag = null)
        {
            var r = new HttpResponseMessage(status) { Content = new StringContent(o?.ToJsonString() ?? "{}", Encoding.UTF8, "application/json") };
            if (etag != null) r.Headers.TryAddWithoutValidation("ETag", etag);
            return r;
        }

        static HttpResponseMessage Error(int status, string code, string message) => Json((HttpStatusCode)status, new JsonObject { ["code"] = code, ["message"] = message });

        /// <summary>The record changed as another member would.</summary>
        public void ServerEdit(JsonObject patch, string who = "Ana Popescu") { Merge(patch); ModifiedBy = who; }

        void Merge(JsonObject patch)
        {
            foreach (var (k, v) in patch["fields"] as JsonObject ?? [])
                if (v == null || (string?)v == "") ((JsonObject)Record["fields"]!).Remove(k); else Record["fields"]![k] = ((string)v!).Trim();
            foreach (var (k, v) in patch["custom"] as JsonObject ?? [])
            {
                var custom = (JsonObject)Record["custom"]!;
                if (v == null) { custom.Remove(k); continue; }
                var cur = custom[k] as JsonObject ?? new JsonObject { ["label"] = k, ["type"] = "text", ["group"] = "other" };
                if (v is JsonObject o) foreach (var (ck, cv) in o) { if (cv == null) cur.Remove(ck); else cur[ck] = cv.DeepClone(); }
                else cur["value"] = (string?)v;
                custom[k] = cur;
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Send(request, cancellationToken));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);
            switch (path)
            {
                case "/foundation/versions":
                    return Json(HttpStatusCode.OK, new JsonObject { ["versions"] = new JsonArray(new JsonObject { ["api_id"] = "lapin-remote", ["version_id"] = "1.0", ["api_base_url"] = Root + "/lapin-remote/1.0" }) });
                case "/lapin-remote/1.0/capabilities":
                    return Json(HttpStatusCode.OK, new JsonObject { ["protocol"] = "lapin-remote", ["version"] = "1.0", ["majors"] = new JsonArray(1), ["features"] = new JsonArray(), ["auth"] = new JsonObject { ["foundation"] = "/foundation/1.1" } });
                case "/atrium/1.0/" or "/atrium/1.0":
                    return Json(HttpStatusCode.OK, new JsonObject { ["api"] = "atrium", ["version"] = "1.0", ["features"] = Offered ? new JsonArray("account", "references", "project.info") : new JsonArray("account", "references") });
            }
            if (request.Headers.Authorization?.Parameter is not { } token || !Tokens.TryGetValue(token, out var who)) return Error(401, "unauthorized", "Sign in first.");
            if (path != $"/atrium/1.0/projects/{Project}/info") return Error(404, "not_found", "Not here.");
            if (!Offered) return Error(404, "not_found", "Not here.");
            if (request.Method == HttpMethod.Get) return Json(HttpStatusCode.OK, Shown(), ETag());
            if (request.Method.Method != "PATCH") return Error(405, "method", "No.");
            var body = (JsonObject)JsonNode.Parse(request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult())!;
            Bodies.Add((JsonObject)body.DeepClone());
            if (who.Role == "viewer") return Error(403, "forbidden", "Viewers read the project information; editors and owners change it.");
            Patches++;
            BeforePatch?.Invoke();
            BeforePatch = null;
            var ifMatch = request.Headers.TryGetValues("If-Match", out var im) ? im.First() : null;
            if (ifMatch != null && ifMatch != "*" && ifMatch != ETag()) { Refused412++; return Json(HttpStatusCode.PreconditionFailed, new JsonObject { ["code"] = "changed", ["message"] = "Changed." }, ETag()); }
            if (body["source"] != null) return Error(400, "bad_request", "Lapin never sends source in this test.");
            Merge(body);
            ModifiedBy = who.Name;
            return Json(HttpStatusCode.OK, Shown(), ETag());
        }
    }

    sealed class Client
    {
        public Fixture F { get; } = new();
        public EngineDispatcher D { get; }
        readonly string _token;

        public Client(HttpClient http, string token)
        {
            _token = token;
            D = new EngineDispatcher(F.Session, PackageFiles.Create()) { RemoteHttp = http };
            Call("remote.add", new JsonObject { ["kind"] = "server", ["url"] = Root, ["project"] = Project });
        }

        public JsonObject Call(string method, JsonObject? p = null)
        {
            p ??= [];
            p["token"] = _token;
            var (reply, _) = D.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
            var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
            if (o["error"] is { } err) throw new InvalidOperationException((string)err!);
            return o["result"] as JsonObject ?? [];
        }

        public JsonObject Sync() => (JsonObject)Call("remote.syncProjectInfo")["report"]!;
        public void Set(JsonObject a) => F.Run("project.setInfo", a);
        public string? Get(string key) => ProjectInfo.FieldValue(F.Model, key);
    }

    static (FakeInfo Server, HttpClient Http) Server() { var s = new FakeInfo(); return (s, new HttpClient(s)); }

    static string Refused(JsonObject report) => string.Join(",", (report["refused"] as JsonArray ?? []).Select(x => (string?)x!["code"]));

    [Fact]
    public void A_first_sync_sends_what_the_server_lacks_and_the_other_user_gets_it_in_one_undo_step()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        mihai.Set(new JsonObject { ["name"] = "Locuință P+1 Huși", ["number"] = "104/2026", ["phase"] = "DTAC" });
        mihai.F.Run("project.setParameter", new JsonObject { ["key"] = "suprafata", ["label"] = "Suprafața terenului", ["type"] = "number", ["value"] = "450,5", ["group"] = "site" });

        var r = mihai.Sync();
        Assert.True((bool)r["offered"]!);
        Assert.True((bool)r["first"]!);
        Assert.Equal((4, 0, ""), ((int)r["sent"]!, (int)r["changedHere"]!, Refused(r)));
        Assert.Equal("104/2026", (string?)server.Record["fields"]!["number"]);
        Assert.Equal("Locuință P+1 Huși", (string?)server.Record["fields"]!["name"]);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"label":"Suprafața terenului","type":"number","group":"site","value":"450.5"}"""), server.Record["custom"]!["suprafata"]));
        Assert.Null(server.Bodies[0]["source"]);
        Assert.Equal(0, (int)mihai.Sync()["sent"]!);                  // nothing changed: nothing sent
        Assert.Equal(1, server.Patches);

        var ana = new Client(http, "tok-ana");
        ana.Set(new JsonObject { ["number"] = "an old number" });      // the server has one: its value wins on a first sync
        var got = ana.Sync();
        Assert.Equal(0, (int)got["sent"]!);
        Assert.Equal(("Locuință P+1 Huși", "104/2026", "DTAC"), (ana.Get("name"), ana.Get("number"), ana.Get("phase")));
        var p = Assert.Single(ProjectInfo.Customs(ana.F.Model));
        Assert.Equal(("suprafata", "Suprafața terenului", "number", "site", "450.5"), (p.Key, p.Label, p.Type, p.Group, p.Value));
        ana.F.Session.Undo();                                          // the server's changes were one undo step
        Assert.Equal(("an old number", null), (ana.Get("number"), ana.Get("phase")));
        Assert.Empty(ProjectInfo.Customs(ana.F.Model));
    }

    [Fact]
    public void Edits_of_different_fields_are_both_kept_and_of_one_field_the_last_writers()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        mihai.Set(new JsonObject { ["number"] = "104/2026", ["client"] = "Ion Popescu" });
        mihai.Sync();
        var ana = new Client(http, "tok-ana");
        ana.Sync();

        mihai.Set(new JsonObject { ["client"] = "Ion Popescu SRL", ["number"] = "105/2026" });
        ana.Set(new JsonObject { ["phase"] = "PT", ["number"] = "106/2026" });
        mihai.Sync();
        var r = ana.Sync();
        Assert.Equal(2, (int)r["sent"]!);                              // phase and number: Ana's number is the last written
        Assert.Equal(1, (int)r["changedHere"]!);                       // Mihai's client came
        Assert.Equal("Mihai Ionescu", (string?)r["by"]);
        Assert.Equal(("Ion Popescu SRL", "PT", "106/2026"), (ana.Get("client"), ana.Get("phase"), ana.Get("number")));
        mihai.Sync();
        Assert.Equal(("Ion Popescu SRL", "PT", "106/2026"), (mihai.Get("client"), mihai.Get("phase"), mihai.Get("number")));

        // A field cleared here is cleared there.
        mihai.Set(new JsonObject { ["phase"] = null });
        mihai.Sync();
        Assert.Null(server.Record["fields"]!["phase"]);
        ana.Sync();
        Assert.Null(ana.Get("phase"));
    }

    [Fact]
    public void A_412_reads_the_record_again_and_sends_again_keeping_the_other_change()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        mihai.Set(new JsonObject { ["number"] = "104/2026" });
        mihai.Sync();
        mihai.Set(new JsonObject { ["client"] = "Ion Popescu" });
        server.BeforePatch = () => server.ServerEdit(new JsonObject { ["fields"] = new JsonObject { ["permit"] = "AC 12/2026" } });
        var r = mihai.Sync();
        Assert.Equal((1, 1, ""), ((int)r["conflicts"]!, server.Refused412, Refused(r)));
        Assert.Equal(("Ion Popescu", "AC 12/2026"), ((string?)server.Record["fields"]!["client"], (string?)server.Record["fields"]!["permit"]));
        Assert.Equal("AC 12/2026", mihai.Get("permit"));
    }

    [Fact]
    public void Parameters_map_by_key_in_Lapins_order_and_come_and_go_both_ways()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        mihai.F.Run("project.setParameter", new JsonObject { ["key"] = "zeta", ["label"] = "Zeta", ["value"] = "z" });
        mihai.F.Run("project.setParameter", new JsonObject { ["key"] = "alpha", ["label"] = "Alpha", ["value"] = "a" });
        mihai.Sync();
        server.ServerEdit(new JsonObject { ["custom"] = new JsonObject { ["beta"] = new JsonObject { ["label"] = "Beta", ["type"] = "date", ["group"] = "project", ["value"] = "2026-11-26" }, ["zeta"] = "z2" } });
        var r = mihai.Sync();
        Assert.Equal(2, (int)r["changedHere"]!);
        Assert.Equal(["zeta", "alpha", "beta"], ProjectInfo.Customs(mihai.F.Model).Select(c => c.Key));
        Assert.Equal("z2", ProjectInfo.Customs(mihai.F.Model)[0].Value);
        mihai.F.Run("project.removeParameter", new JsonObject { ["key"] = "alpha" });
        mihai.Sync();
        Assert.Null(server.Record["custom"]!["alpha"]);
        server.ServerEdit(new JsonObject { ["custom"] = new JsonObject { ["zeta"] = null } });
        mihai.Sync();
        Assert.Equal(["beta"], ProjectInfo.Customs(mihai.F.Model).Select(c => c.Key));
    }

    [Fact]
    public void A_viewers_change_is_refused_and_the_servers_value_comes_back()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        mihai.Set(new JsonObject { ["number"] = "104/2026" });
        mihai.Sync();
        var vio = new Client(http, "tok-vio");
        vio.Sync();
        vio.Set(new JsonObject { ["number"] = "mine" });
        var r = vio.Sync();
        Assert.Equal("forbidden", Refused(r));
        Assert.Equal("104/2026", vio.Get("number"));
        Assert.Equal("104/2026", (string?)server.Record["fields"]!["number"]);
    }

    [Fact]
    public void An_older_server_is_left_alone_and_what_Archicad_left_apart_is_never_sent()
    {
        var (server, http) = Server();
        server.Offered = false;
        var mihai = new Client(http, "tok-mihai");
        mihai.Set(new JsonObject { ["number"] = "104/2026" });
        var r = mihai.Sync();
        Assert.False((bool)r["offered"]!);
        Assert.Empty(server.Bodies);

        server.Offered = true;
        mihai.F.Session.Edit("Archicad", edit => ProjectInfo.Take(edit, "archicad", new Dictionary<string, string>(), new Dictionary<string, SourceValue> { ["KEYWORDS"] = new("Keywords", "casă") }));
        Assert.True((bool)mihai.Sync()["first"]!);                    // the older server's answer kept no state
        Assert.False((bool)mihai.Sync()["first"]!);
        Assert.All(server.Bodies, b => Assert.Null(b["source"]));
        Assert.Equal("casă", (string?)ProjectInfo.Sources(mihai.F.Model)["archicad"]!["KEYWORDS"]!["value"]);
    }

    sealed class Live(Fixture f)
    {
        public Fixture F { get; } = f;
        readonly EngineDispatcher _d = new(f.Session, PackageFiles.Create());
        JsonObject _auth = [];

        public JsonNode? Call(string method, JsonObject? p = null)
        {
            p ??= [];
            foreach (var (k, v) in _auth) p[k] = v?.DeepClone();
            var (reply, _) = _d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
            var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
            if (o["error"] is { } err) throw new InvalidOperationException((string)err!);
            if (o["result"]?["tokens"] is JsonObject t) { _auth["token"] = t["accessToken"]?.DeepClone(); _auth["refreshToken"] = t["refreshToken"]?.DeepClone(); }
            return o["result"];
        }

        public void SignIn(string server, string user, string password, string name)
        {
            var t = Call("remote.signIn", new JsonObject { ["url"] = server, ["username"] = user, ["password"] = password })!;
            _auth = new JsonObject
            {
                ["token"] = t["accessToken"]?.DeepClone(), ["refreshToken"] = t["refreshToken"]?.DeepClone(),
                ["author"] = new JsonObject { ["id"] = t["user"]?["id"]?.DeepClone(), ["name"] = name },
            };
        }

        public JsonObject Sync() => (JsonObject)Call("remote.syncProjectInfo")!["report"]!;

        /// <summary>One request of Atrium's own API on this sign-in, as the desktop's interface makes it (remote.atrium).</summary>
        public JsonObject Atrium(string url, string method, string path, JsonNode? body = null) =>
            (JsonObject)Call("remote.atrium", new JsonObject { ["url"] = url, ["method"] = method, ["path"] = path, ["body"] = body })!;
    }

    /// <summary>
    /// Against a running Atrium Server (LAPIN_REMOTE_SERVER, _PROJECT, _PASSWORD, _MIHAI an owner, _ANA an editor; docs/atrium-server.md,
    /// "Testing it locally"): the project information goes up, a proposal from Archicad is applied by the owner and comes down, both
    /// users' edits are kept, and a pull leaves the information to the sync.
    /// </summary>
    [Fact]
    public void Against_a_running_server_the_information_syncs_a_proposal_is_applied_and_a_pull_leaves_it_alone()
    {
        var url = Environment.GetEnvironmentVariable("LAPIN_REMOTE_SERVER");
        if (string.IsNullOrEmpty(url)) return;
        var project = Environment.GetEnvironmentVariable("LAPIN_REMOTE_PROJECT")!;
        var password = Environment.GetEnvironmentVariable("LAPIN_REMOTE_PASSWORD")!;
        var dir = Directory.CreateTempSubdirectory("lapin-infosync-live-").FullName;
        try
        {
            var mihai = new Live(new Fixture());
            mihai.SignIn(url, Environment.GetEnvironmentVariable("LAPIN_REMOTE_MIHAI")!, password, "Mihai Ionescu");
            mihai.F.Wall(0, 0, 6, 0);
            mihai.F.Run("project.setInfo", new JsonObject { ["name"] = "Locuință P+1 Huși", ["number"] = "104/2026", ["phase"] = "DTAC" });
            mihai.F.Run("project.setParameter", new JsonObject { ["key"] = "suprafata", ["label"] = "Suprafața terenului", ["type"] = "number", ["value"] = "450,5", ["group"] = "site" });
            mihai.Call("project.save", new JsonObject { ["path"] = Path.Combine(dir, "mihai.lapin") });
            mihai.Call("remote.add", new JsonObject { ["kind"] = "server", ["url"] = url, ["project"] = project });
            mihai.Call("history.commit", new JsonObject { ["message"] = "Initial model" });
            Assert.True((bool)mihai.Call("remote.push")!["pushed"]!);
            var up = mihai.Sync();
            Assert.True((bool)up["offered"]!, up.ToJsonString());
            Assert.Equal("", Refused(up));
            Assert.Equal(4, (int)up["sent"]!);
            var info = (JsonObject)mihai.Atrium(url, "GET", $"atrium/1.0/projects/{project}/info")["body"]!;
            Assert.Equal(("104/2026", "450.5"), ((string?)info["fields"]!["number"], (string?)info["custom"]!["suprafata"]!["value"]));

            // A proposal from Archicad (Tapir's answer as it is), applied in part by the owner, comes down with the next sync.
            var raw = JsonNode.Parse("""[{"projectInfoId":"PROJECTNUMBER","projectInfoName":"Project Number","projectInfoValue":"105/2026"},{"projectInfoId":"CLIENT_FULLNAME","projectInfoName":"Client Full Name","projectInfoValue":"Ion Popescu"},{"projectInfoId":"KEYWORDS","projectInfoName":"Keywords","projectInfoValue":"casă"}]""");
            var made = mihai.Atrium(url, "POST", $"atrium/1.0/projects/{project}/info/proposals", new JsonObject { ["source"] = "archicad", ["raw"] = raw, ["sourceRef"] = new JsonObject { ["file"] = "casa.pln" } });
            Assert.Equal(201, (int)made["status"]!);
            var proposal = (JsonObject)made["body"]!;
            Assert.Equal(["number", "client", "source.archicad.KEYWORDS"], ((JsonArray)proposal["changes"]!).Select(c => (string)c!["key"]!));
            var applied = mihai.Atrium(url, "POST", $"atrium/1.0/projects/{project}/info/proposals/{proposal["id"]}/apply", new JsonObject { ["keys"] = new JsonArray("client", "source.archicad.KEYWORDS") });
            Assert.Equal(200, (int)applied["status"]!);
            var down = mihai.Sync();
            Assert.Equal(1, (int)down["changedHere"]!);
            Assert.Equal(("Ion Popescu", "104/2026"), (ProjectInfo.Get(mihai.F.Model, "client"), ProjectInfo.Get(mihai.F.Model, "number")));
            Assert.Empty(ProjectInfo.Sources(mihai.F.Model));               // the server's source stays the server's

            // Ana clones and syncs; she and Mihai edit different fields: both kept.
            var ana = new Live(new Fixture());
            ana.SignIn(url, Environment.GetEnvironmentVariable("LAPIN_REMOTE_ANA")!, password, "Ana Popescu");
            ana.Call("remote.clone", new JsonObject { ["kind"] = "server", ["url"] = url, ["project"] = project, ["path"] = Path.Combine(dir, "ana.lapin") });
            Assert.Equal("", Refused(ana.Sync()));
            Assert.Equal(("Locuință P+1 Huși", "Ion Popescu"), (ProjectInfo.Name(ana.F.Model), ProjectInfo.Get(ana.F.Model, "client")));
            Assert.False((bool)ana.Call("history.status")!["uncommitted"]!);   // what the sync brought is not a change to commit
            ana.F.Run("project.setInfo", new JsonObject { ["permit"] = "AC 12/2026" });
            mihai.F.Run("project.setInfo", new JsonObject { ["designer"] = "Ciuntuc BIM Studio SRL" });
            Assert.Equal("", Refused(ana.Sync()));
            Assert.Equal("", Refused(mihai.Sync()));
            ana.Sync();
            Assert.Equal(("AC 12/2026", "Ciuntuc BIM Studio SRL"), (ProjectInfo.Get(ana.F.Model, "permit"), ProjectInfo.Get(ana.F.Model, "designer")));
            Assert.Equal(("AC 12/2026", "Ciuntuc BIM Studio SRL"), (ProjectInfo.Get(mihai.F.Model, "permit"), ProjectInfo.Get(mihai.F.Model, "designer")));

            // Mihai commits a change of the number without syncing: Ana's pull leaves her information alone; the sync brings it.
            mihai.F.Run("project.setInfo", new JsonObject { ["number"] = "106/2026" });
            mihai.F.Wall(0, 3, 6, 3);
            mihai.Call("history.commit", new JsonObject { ["message"] = "A wall and the number" });
            Assert.True((bool)mihai.Call("remote.push")!["pushed"]!);
            Assert.False((bool)ana.Call("history.status")!["uncommitted"]!);   // her synced edit neither
            Assert.True((bool)ana.Call("remote.pull")!["pulled"]!);
            Assert.Equal("104/2026", ProjectInfo.Get(ana.F.Model, "number"));
            Assert.Equal(2, ana.F.Model.OfClass(Lapin.Model.IfcClass.Wall).Count());
            Assert.False((bool)ana.Call("history.status")!["uncommitted"]!);
            mihai.Sync();
            ana.Sync();
            Assert.Equal("106/2026", ProjectInfo.Get(ana.F.Model, "number"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
