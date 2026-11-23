using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform.Cde.Remarks;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The remarks synced with a CDE server's issues (<c>remote.syncIssues</c>; Atrium docs/plan-cde-web.md W3) against a fake Atrium
/// Server that keeps BCF topics, comments, viewpoints and snapshots, the event journal, ETags with If-Match, the JSON merge patch and
/// the issue role rules (status_only, close_forbidden, comment_required, owners alone delete topics): a remark made here goes with its
/// GUID and reaches another user; two users' edits of different fields are both kept, of one field the last writer's; a 412 reads
/// again and sends again; refusals come back with their codes and the server's values; deletions both ways; a pull is one undo step;
/// the sync's state lives with the project's history, and a server's issues stay out of "uncommitted".
/// </summary>
public sealed class IssueSyncTests
{
    const string Root = "http://127.0.0.1:5998";
    const string Project = "0b7c6f8e-1111-4222-8333-944455556666";
    static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    /// <summary>A small Atrium Server: one project, three members (owner, editor, viewer), BCF in memory with its journal and rules.</summary>
    internal sealed class FakeCde : HttpMessageHandler
    {
        public readonly Dictionary<string, (string Email, string Name, string Role)> Tokens = new()
        {
            ["tok-mihai"] = ("mihai@example.com", "Mihai Ionescu", "owner"),
            ["tok-ana"] = ("ana@example.com", "Ana Popescu", "editor"),
            ["tok-vio"] = ("viewer@example.com", "Victor Viewer", "viewer"),
        };
        public readonly Dictionary<string, JsonObject> Topics = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, List<JsonObject>> Comments = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, List<(JsonObject Json, byte[]? Snapshot)>> Viewpoints = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<JsonObject> Journal = [];
        public readonly Dictionary<string, string> StatusChangedAt = new(StringComparer.OrdinalIgnoreCase);
        public int Patches, Refused412;
        /// <summary>Called once before the next PATCH is applied: another writer slipping in between a read and a write.</summary>
        public Action? BeforePatch;
        int _tick, _batch;

        string Now() => new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc).AddSeconds(++_tick).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        public static string ETag(JsonObject t) => "\"" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(t.ToJsonString())))[..16].ToLowerInvariant() + "\"";

        void Log(string type, string topic, string author, string? old = null, string? @new = null, string? comment = null)
        {
            Journal.Add(new JsonObject
            {
                ["seq"] = (long)(Journal.Count + 1), ["batch"] = _batch, ["type"] = type, ["topicGuid"] = topic, ["commentGuid"] = comment, ["old"] = old, ["new"] = @new,
                ["author"] = author, ["authorName"] = Tokens.Values.First(t => t.Email == author).Name, ["date"] = Now(),
            });
        }

        static HttpResponseMessage Json(HttpStatusCode status, JsonNode? o, string? etag = null)
        {
            var r = new HttpResponseMessage(status) { Content = new StringContent(o?.ToJsonString() ?? "{}", Encoding.UTF8, "application/json") };
            if (etag != null) r.Headers.TryAddWithoutValidation("ETag", etag);
            return r;
        }
        static HttpResponseMessage Error(int status, string code, string message) => Json((HttpStatusCode)status, new JsonObject { ["code"] = code, ["message"] = message });

        /// <summary>Changes a topic as another member would (through the journal), for the tests.</summary>
        public void ServerEdit(string guid, string field, string value, string author)
        {
            _batch++;
            var t = Topics[guid];
            var old = (string?)t[field];
            t[field] = value;
            t["modified_date"] = Now();
            t["modified_author"] = author;
            if (field == "topic_status") StatusChangedAt[guid] = (string)t["modified_date"]!;
            Log(field == "title" ? "title_updated" : field == "topic_status" ? "status_updated" : field.Replace("topic_", "") + "_updated", guid, author, old, value);
        }

        static readonly string[] Writable = ["title", "description", "topic_type", "topic_status", "priority", "assigned_to", "due_date", "stage", "labels", "reference_links", "index", "bim_snippet"];

        bool MayUpdate(JsonObject t, string me, string role) =>
            role == "owner" || (role == "editor" && string.Equals((string?)t["creation_author"], me, StringComparison.OrdinalIgnoreCase)) || string.Equals((string?)t["assigned_to"], me, StringComparison.OrdinalIgnoreCase);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Send(request, cancellationToken));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var query = request.RequestUri.Query;
            var method = request.Method.Method;
            var text = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? "";
            var body = text.Length > 0 ? JsonNode.Parse(text) : null;
            switch (path)
            {
                case "/foundation/versions":
                    return Json(HttpStatusCode.OK, new JsonObject { ["versions"] = new JsonArray(new JsonObject { ["api_id"] = "lapin-remote", ["version_id"] = "1.0", ["api_base_url"] = Root + "/lapin-remote/1.0" }) });
                case "/lapin-remote/1.0/capabilities":
                    return Json(HttpStatusCode.OK, new JsonObject { ["protocol"] = "lapin-remote", ["version"] = "1.0", ["majors"] = new JsonArray(1), ["features"] = new JsonArray(), ["auth"] = new JsonObject { ["foundation"] = "/foundation/1.1" } });
            }
            if (request.Headers.Authorization?.Parameter is not { } token || !Tokens.TryGetValue(token, out var who)) return Error(401, "unauthorized", "Sign in first.");
            var (me, name, role) = who;
            var a = $"/atrium/1.0/projects/{Project}";
            var b = $"/bcf/3.0/projects/{Project}";
            if (path == "/atrium/1.0/account") return Json(HttpStatusCode.OK, new JsonObject { ["id"] = me, ["email"] = me, ["name"] = name, ["isAdmin"] = role == "owner" });
            if (path == $"{a}/rules") return Json(HttpStatusCode.OK, new JsonObject { ["main"] = "editor", ["issueTags"] = "owner", ["comments"] = "viewer", ["close"] = "author", ["resolveNeedsComment"] = true, ["role"] = role });
            if (path == $"{a}/members")
                return Json(HttpStatusCode.OK, new JsonObject { ["role"] = role, ["members"] = new JsonArray([.. Tokens.Values.Select(t => (JsonNode)new JsonObject { ["email"] = t.Email, ["name"] = t.Name, ["role"] = t.Role })]) });
            if (path == $"{a}/topics" && method == "GET")
                return Json(HttpStatusCode.OK, new JsonObject
                {
                    ["last"] = (long)Journal.Count,
                    ["topics"] = new JsonArray([.. Topics.Values.Select(t => (JsonNode)new JsonObject { ["guid"] = (string?)t["guid"], ["etag"] = ETag(t) })]),
                });
            if (path == $"{a}/events")
            {
                var after = long.Parse(System.Web.HttpUtility.ParseQueryString(query)["after"] ?? "0", CultureInfo.InvariantCulture);
                var rows = Journal.Where(e => (long)e["seq"]! > after).ToList();
                return Json(HttpStatusCode.OK, new JsonObject { ["events"] = new JsonArray([.. rows.Select(r => (JsonNode)r.DeepClone())]), ["last"] = rows.Count > 0 ? (long)rows[^1]["seq"]! : after, ["more"] = false });
            }
            if (path.StartsWith($"{a}/topics/", StringComparison.Ordinal) && method == "PATCH")
            {
                Patches++;
                var g = path[($"{a}/topics/").Length..];
                BeforePatch?.Invoke();
                BeforePatch = null;
                if (!Topics.TryGetValue(g, out var t)) return Error(404, "not_found", "No topic.");
                if (!MayUpdate(t, me, role)) return Error(403, "forbidden", "You can't change this topic.");
                var ifMatch = request.Headers.TryGetValues("If-Match", out var im) ? im.First() : null;
                if (ifMatch != null && ifMatch != "*" && ifMatch != ETag(t)) { Refused412++; return Json(HttpStatusCode.PreconditionFailed, new JsonObject { ["code"] = "changed", ["message"] = "The topic has changed." }, ETag(t)); }
                var next = (JsonObject)t.DeepClone();
                foreach (var (k, v) in (JsonObject)body!) if (Writable.Contains(k)) { if (v == null) next.Remove(k); else next[k] = v.DeepClone(); }
                if (role == "viewer")
                {
                    var x = (JsonObject)t.DeepClone(); var y = (JsonObject)next.DeepClone(); x.Remove("topic_status"); y.Remove("topic_status");
                    if (!JsonNode.DeepEquals(x, y)) return Error(403, "status_only", "As the topic's assignee you may change its status, and nothing else of it.");
                }
                var oldStatus = (string?)t["topic_status"];
                var newStatus = (string?)next["topic_status"];
                if (!string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(newStatus, "Closed", StringComparison.OrdinalIgnoreCase) && role != "owner" && !string.Equals((string?)t["creation_author"], me, StringComparison.OrdinalIgnoreCase))
                        return Error(403, "close_forbidden", "Only the topic's author or a project owner closes it.");
                    if (string.Equals(newStatus, "Resolved", StringComparison.OrdinalIgnoreCase))
                    {
                        var since = StatusChangedAt.GetValueOrDefault(g) ?? (string)t["creation_date"]!;
                        if (!Comments[g].Any(c => (string?)c["author"] == me && string.CompareOrdinal((string)c["date"]!, since) >= 0))
                            return Error(403, "comment_required", "Say how it was resolved: add a comment first, then set the status.");
                    }
                }
                if (JsonNode.DeepEquals(t, next)) return Json(HttpStatusCode.OK, t, ETag(t));
                _batch++;
                foreach (var k in Writable)
                    if (!JsonNode.DeepEquals(t[k], next[k])) Log(k == "topic_status" ? "status_updated" : k + "_updated", g, me, t[k]?.ToJsonString(), next[k]?.ToJsonString());
                next["modified_date"] = Now();
                next["modified_author"] = me;
                if (!string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase)) StatusChangedAt[g] = (string)next["modified_date"]!;
                Topics[g] = next;
                return Json(HttpStatusCode.OK, next, ETag(next));
            }
            if (path == $"{b}/extensions")
                return Json(HttpStatusCode.OK, new JsonObject
                {
                    ["topic_type"] = new JsonArray("Issue", "Clash", "Request", "Remark"), ["topic_status"] = new JsonArray("Open", "In progress", "Resolved", "Closed"),
                    ["priority"] = new JsonArray("Low", "Normal", "High", "Critical"), ["topic_label"] = new JsonArray("Architecture", "Structure"),
                });
            if (path == $"{b}/topics" && method == "POST")
            {
                if (role == "viewer") return Error(403, "forbidden", "You can't create topics in this project.");
                var o = (JsonObject)body!;
                var g = (string)o["guid"]!;
                if (Topics.ContainsKey(g)) return Error(409, "conflict", "In use.");
                _batch++;
                var t = new JsonObject { ["guid"] = g, ["server_assigned_id"] = (Topics.Count + 1).ToString(CultureInfo.InvariantCulture), ["creation_date"] = Now(), ["creation_author"] = me };
                foreach (var (k, v) in o) if (Writable.Contains(k) && v != null) t[k] = v.DeepClone();
                Topics[g] = t; Comments[g] = []; Viewpoints[g] = [];
                Log("topic_created", g, me);
                return Json(HttpStatusCode.Created, t, ETag(t));
            }
            if (!path.StartsWith($"{b}/topics/", StringComparison.Ordinal)) return Error(404, "not_found", "Not here.");
            var rest = path[($"{b}/topics/").Length..].Split('/');
            var guid = rest[0];
            if (!Topics.TryGetValue(guid, out var topic)) return Error(404, "not_found", "No topic.");
            if (rest.Length == 1)
            {
                if (method == "GET") return Json(HttpStatusCode.OK, topic, ETag(topic));
                if (method == "DELETE")
                {
                    if (role != "owner") return Error(403, "forbidden", "Only the project's owners delete topics.");
                    _batch++;
                    Topics.Remove(guid);
                    Log("topic_deleted", guid, me);
                    return Json(HttpStatusCode.OK, null);
                }
            }
            if (rest[1] == "comments")
            {
                if (rest.Length == 2 && method == "GET") return Json(HttpStatusCode.OK, new JsonArray([.. Comments[guid].Select(c => (JsonNode)c.DeepClone())]));
                if (rest.Length == 2 && method == "POST")
                {
                    var o = (JsonObject)body!;
                    _batch++;
                    var c = new JsonObject { ["guid"] = (string?)o["guid"] ?? Guid.NewGuid().ToString(), ["date"] = Now(), ["author"] = me, ["comment"] = (string?)o["comment"], ["topic_guid"] = guid, ["viewpoint_guid"] = (string?)o["viewpoint_guid"] };
                    Comments[guid].Add(c);
                    topic["modified_date"] = (string)c["date"]!;
                    Log("comment_created", guid, me, comment: (string)c["guid"]!);
                    return Json(HttpStatusCode.Created, c);
                }
                if (rest.Length == 3 && method == "DELETE")
                {
                    var c = Comments[guid].FirstOrDefault(x => (string?)x["guid"] == rest[2]);
                    if (c == null) return Error(404, "not_found", "No comment.");
                    if (role != "owner" && (string?)c["author"] != me) return Error(403, "forbidden", "You can't delete this comment.");
                    _batch++;
                    Comments[guid].Remove(c);
                    Log("comment_deleted", guid, me, comment: rest[2]);
                    return Json(HttpStatusCode.OK, null);
                }
            }
            if (rest[1] == "viewpoints")
            {
                if (rest.Length == 2 && method == "GET")
                    return Json(HttpStatusCode.OK, new JsonArray([.. Viewpoints[guid].Select(v =>
                    {
                        var j = (JsonObject)v.Json.DeepClone();
                        j.Remove("components");
                        if (j["snapshot"] is JsonObject s) s.Remove("snapshot_data");
                        return (JsonNode)j;
                    })]));
                if (rest.Length == 2 && method == "POST")
                {
                    if (role == "viewer") return Error(403, "forbidden", "You can't add viewpoints to this topic.");
                    var o = (JsonObject)body!.DeepClone();
                    byte[]? snap = o["snapshot"]?["snapshot_data"] is { } d ? Convert.FromBase64String((string)d!) : null;
                    _batch++;
                    Viewpoints[guid].Add((o, snap));
                    Log("viewpoint_added", guid, me);
                    return Json(HttpStatusCode.Created, o);
                }
                var v = Viewpoints[guid].FirstOrDefault(x => (string?)x.Json["guid"] == rest[2]);
                if (v.Json == null) return Error(404, "not_found", "No viewpoint.");
                if (rest.Length == 4 && rest[3] == "snapshot") return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(v.Snapshot!) };
                if (rest.Length == 4 && rest[3] is "selection" or "visibility")
                    return Json(HttpStatusCode.OK, new JsonObject { [rest[3]] = v.Json["components"]?[rest[3]]?.DeepClone() });
            }
            return Error(404, "not_found", "Not here.");
        }
    }

    /// <summary>One Lapin with the server project as its remote, signed in with a token.</summary>
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

        public JsonObject Sync() => (JsonObject)Call("remote.syncIssues")["report"]!;
        public BcfTopic? Remark(Guid g) => F.Session.Model.Get(Remarks.IdOf(g)) is { } e ? Remarks.Read(e) : null;
        public void Update(Guid g, JsonObject fields) { fields["id"] = Remarks.IdOf(g); fields["author"] ??= "me"; F.Run("remarks.update", fields); }
    }

    static (FakeCde Server, HttpClient Http) Server() { var s = new FakeCde(); return (s, new HttpClient(s)); }

    static Guid NewRemark(Client c, string title, string? assignedTo = null, bool viewpoint = false)
    {
        var args = new JsonObject { ["title"] = title, ["author"] = "Mihai" };
        if (assignedTo != null) args["assignedTo"] = assignedTo;
        if (viewpoint)
            args["viewpoint"] = new JsonObject
            {
                ["camera"] = new BcfCamera(true, new(8, -8, 4), new(-0.6, 0.6, -0.5), new(0, 0, 1), 45, 0, 1.6).ToJson(),
                ["selection"] = new JsonArray("2O2Fr$t4X7Zf8NOew3FLOH"), ["snapshot"] = Convert.ToBase64String(Png),
            };
        var r = c.F.Run("remarks.create", args)!;
        return Guid.Parse((string)r["guid"]!);
    }

    static string Refused(JsonObject report) => string.Join(",", (report["refused"] as JsonArray ?? []).Select(x => (string?)x!["code"]));

    [Fact]
    public void A_remark_made_here_goes_with_its_GUID_comment_viewpoint_and_snapshot_and_reaches_the_other()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var g = NewRemark(mihai, "Check the lintel", viewpoint: true);
        mihai.F.Run("remarks.comment", new JsonObject { ["id"] = Remarks.IdOf(g), ["text"] = "Too short over the door.", ["author"] = "Mihai" });

        var r = mihai.Sync();
        Assert.Equal((1, 1, 1), ((int)r["created"]!, (int)r["comments"]!, (int)r["viewpoints"]!));
        Assert.True(server.Topics.ContainsKey(g.ToString("D")));
        Assert.Equal("mihai@example.com", (string?)server.Comments[g.ToString("D")][0]["author"]);
        // Here the comment is the server's now (its author the account), and a second sync has nothing to do.
        Assert.Equal("mihai@example.com", mihai.Remark(g)!.Comments[0].Author);
        var again = mihai.Sync();
        Assert.Equal(0, (int)again["changedHere"]!);
        Assert.Equal(0, server.Patches);

        var ana = new Client(http, "tok-ana");
        var first = ana.Sync();
        Assert.True((bool)first["first"]!);
        var t = ana.Remark(g)!;
        Assert.Equal("Check the lintel", t.Title);
        Assert.Equal("Too short over the door.", Assert.Single(t.Comments).Text);
        var vp = Assert.Single(t.Viewpoints);
        Assert.Equal(Png, vp.Snapshot);
        Assert.Equal(["2O2Fr$t4X7Zf8NOew3FLOH"], vp.Selection);
        Assert.Equal(8, vp.Camera!.ViewPoint.X, 6);

        // Who is who, for the interface: the members' names by their e-mail, the role and the rules.
        var info = ana.Call("remote.issueInfo");
        Assert.Equal("editor", (string?)info["role"]);
        Assert.Equal("Mihai Ionescu", (string?)info["people"]!["mihai@example.com"]);
        Assert.Equal(0, (int)info["pending"]!);
        Assert.Contains("Clash", info["extensions"]!["types"]!.AsArray().Select(x => (string?)x));
    }

    [Fact]
    public void Two_users_editing_different_fields_of_one_topic_both_keep_theirs()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var g = NewRemark(mihai, "Stair width", assignedTo: "ana@example.com");
        mihai.Sync();
        var ana = new Client(http, "tok-ana");
        ana.Sync();

        mihai.Update(g, new JsonObject { ["priority"] = "High" });
        ana.Update(g, new JsonObject { ["description"] = "1.10 m between the walls." });
        mihai.Sync();
        var r = ana.Sync();
        Assert.Equal("", Refused(r));
        Assert.Equal(("High", "1.10 m between the walls."), (ana.Remark(g)!.Priority, ana.Remark(g)!.Description));
        mihai.Sync();
        Assert.Equal(("High", "1.10 m between the walls."), (mihai.Remark(g)!.Priority, mihai.Remark(g)!.Description));
        Assert.Equal(("High", "1.10 m between the walls."), ((string?)server.Topics[g.ToString("D")]["priority"], (string?)server.Topics[g.ToString("D")]["description"]));
    }

    [Fact]
    public void The_same_field_changed_by_both_is_the_last_writers()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var g = NewRemark(mihai, "Window sill", assignedTo: "ana@example.com");
        mihai.Sync();
        var ana = new Client(http, "tok-ana");
        ana.Sync();

        mihai.Update(g, new JsonObject { ["title"] = "Window sill, north facade" });
        ana.Update(g, new JsonObject { ["title"] = "Window sill height" });
        mihai.Sync();
        ana.Sync();                                    // Ana writes last: her title stands
        mihai.Sync();
        Assert.Equal("Window sill height", (string?)server.Topics[g.ToString("D")]["title"]);
        Assert.Equal("Window sill height", mihai.Remark(g)!.Title);
        Assert.Equal("Window sill height", ana.Remark(g)!.Title);
        // The journal says who changed what.
        Assert.Equal(["mihai@example.com", "ana@example.com"], server.Journal.Where(e => (string?)e["type"] == "title_updated").Select(e => (string?)e["author"]));
    }

    [Fact]
    public void A_412_reads_the_topic_again_and_sends_the_change_again()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var g = NewRemark(mihai, "Beam depth", assignedTo: "ana@example.com");
        mihai.Sync();
        var ana = new Client(http, "tok-ana");
        ana.Sync();

        ana.Update(g, new JsonObject { ["priority"] = "Critical" });
        // Mihai writes between Ana's read and her write.
        server.BeforePatch = () => server.ServerEdit(g.ToString("D"), "description", "Check against the column.", "mihai@example.com");
        var r = ana.Sync();
        Assert.Equal(1, (int)r["conflicts"]!);
        Assert.Equal(1, server.Refused412);
        Assert.Equal("", Refused(r));
        Assert.Equal(("Critical", "Check against the column."), ((string?)server.Topics[g.ToString("D")]["priority"], (string?)server.Topics[g.ToString("D")]["description"]));
        Assert.Equal(("Critical", "Check against the column."), (ana.Remark(g)!.Priority, ana.Remark(g)!.Description));
    }

    [Fact]
    public void A_viewer_assigned_changes_the_status_only_and_a_refusal_brings_the_servers_values_back()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var g = NewRemark(mihai, "Railing", assignedTo: "viewer@example.com");
        mihai.Sync();
        var vio = new Client(http, "tok-vio");
        vio.Sync();

        vio.Update(g, new JsonObject { ["title"] = "Railing, my title", ["status"] = "In progress" });
        var r = vio.Sync();
        Assert.Equal("status_only", Refused(r));
        Assert.Equal("Railing", vio.Remark(g)!.Title);
        Assert.Equal("Railing", (string?)server.Topics[g.ToString("D")]["title"]);

        vio.Update(g, new JsonObject { ["status"] = "In progress" });
        Assert.Equal("", Refused(vio.Sync()));
        Assert.Equal("In progress", (string?)server.Topics[g.ToString("D")]["topic_status"]);

        // A viewer makes no topic: it stays here, unsent, and says why.
        var mine = NewRemark(vio, "A viewer's remark");
        Assert.Equal("forbidden", Refused(vio.Sync()));
        Assert.False(server.Topics.ContainsKey(mine.ToString("D")));
        Assert.NotNull(vio.Remark(mine));
    }

    [Fact]
    public void Resolved_goes_after_its_comment_and_without_one_waits_closing_is_the_authors()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var g = NewRemark(mihai, "Duct clash", assignedTo: "ana@example.com");
        var h = NewRemark(mihai, "Slab edge", assignedTo: "ana@example.com");
        mihai.Sync();
        var ana = new Client(http, "tok-ana");
        ana.Sync();

        // A comment and the status in one sync: the comment goes first, so the server takes "Resolved".
        ana.F.Run("remarks.comment", new JsonObject { ["id"] = Remarks.IdOf(g), ["text"] = "Moved the duct 20 cm up.", ["author"] = "Ana" });
        ana.Update(g, new JsonObject { ["status"] = "Resolved" });
        // Without a comment the server refuses; the status stays here, to send once a comment says how.
        ana.Update(h, new JsonObject { ["status"] = "Resolved" });
        var r = ana.Sync();
        Assert.Equal("Resolved", (string?)server.Topics[g.ToString("D")]["topic_status"]);
        Assert.Equal("comment_required", Refused(r));
        Assert.Equal("Resolved", ana.Remark(h)!.Status);
        Assert.Equal("Open", (string?)server.Topics[h.ToString("D")]["topic_status"]);
        Assert.Equal(1, (int)ana.Call("remote.issueInfo")["pending"]!);
        ana.F.Run("remarks.comment", new JsonObject { ["id"] = Remarks.IdOf(h), ["text"] = "Edge trimmed.", ["author"] = "Ana" });
        Assert.Equal("", Refused(ana.Sync()));
        Assert.Equal("Resolved", (string?)server.Topics[h.ToString("D")]["topic_status"]);

        // Closing is the author's (or an owner's): Ana's "Closed" comes back as the server has it.
        ana.Update(g, new JsonObject { ["status"] = "Closed" });
        Assert.Equal("close_forbidden", Refused(ana.Sync()));
        Assert.Equal("Resolved", ana.Remark(g)!.Status);
    }

    [Fact]
    public void Deletions_go_both_ways_as_the_roles_allow()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var g = NewRemark(mihai, "Door swing");
        var h = NewRemark(mihai, "Kerb height");
        mihai.Sync();
        var ana = new Client(http, "tok-ana");
        ana.Sync();

        // An editor's deletion is refused: the topic comes back.
        ana.F.Run("remarks.delete", new JsonObject { ["id"] = Remarks.IdOf(g) });
        Assert.Null(ana.Remark(g));
        var r = ana.Sync();
        Assert.Equal("delete_forbidden", Refused(r));
        Assert.NotNull(ana.Remark(g));
        Assert.True(server.Topics.ContainsKey(g.ToString("D")));

        // An owner's goes to the server, and from there to the others.
        mihai.F.Run("remarks.delete", new JsonObject { ["id"] = Remarks.IdOf(h) });
        Assert.Equal(1, (int)mihai.Sync()["deleted"]!);
        Assert.False(server.Topics.ContainsKey(h.ToString("D")));
        Assert.Equal(1, (int)ana.Sync()["removed"]!);
        Assert.Null(ana.Remark(h));
        Assert.NotNull(ana.Remark(g));
    }

    [Fact]
    public void What_a_sync_brings_is_one_undo_step_and_others_changes_are_reported()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var g = NewRemark(mihai, "Gutter");
        var h = NewRemark(mihai, "Downpipe");
        mihai.Sync();
        var ana = new Client(http, "tok-ana");
        ana.Sync();
        Assert.NotNull(ana.Remark(g));
        Assert.NotNull(ana.Remark(h));
        ana.F.Session.Undo();
        Assert.Null(ana.Remark(g));
        Assert.Null(ana.Remark(h));
        ana.F.Session.Redo();

        server.ServerEdit(g.ToString("D"), "title", "Gutter slope", "mihai@example.com");
        var r = ana.Sync();
        Assert.Equal(1, (int)r["changedHere"]!);
        var other = Assert.Single(r["byOthers"]!.AsArray());
        Assert.Equal("Mihai Ionescu", (string?)other!["who"]);
        Assert.Equal("Gutter slope", (string?)other["title"]);
        Assert.Equal("Gutter slope", ana.Remark(g)!.Title);
    }

    [Fact]
    public void The_sync_state_lives_with_the_history_and_a_servers_issues_stay_out_of_uncommitted()
    {
        var dir = Directory.CreateTempSubdirectory("lapin-issues-").FullName;
        try
        {
            var (server, http) = Server();
            var mihai = new Client(http, "tok-mihai");
            mihai.F.Wall(0, 0, 5, 0);
            var file = Path.Combine(dir, "p.lapin");
            mihai.Call("project.save", new JsonObject { ["path"] = file });
            mihai.Call("history.commit", new JsonObject { ["message"] = "First" });
            Assert.False((bool)mihai.Call("history.status")["uncommitted"]!);

            // A remark is the server's issue: not a change to commit.
            var g = NewRemark(mihai, "Parapet");
            Assert.False((bool)mihai.Call("history.status")["uncommitted"]!);
            mihai.Sync();
            mihai.Call("project.save", new JsonObject { ["path"] = file });
            mihai.F.Wall(0, 3, 5, 3);
            Assert.True((bool)mihai.Call("history.status")["uncommitted"]!);

            // Opened again: the sync's state is there (what the server holds, the journal's seq), and the remark.
            var again = new Client(http, "tok-mihai");
            again.Call("project.open", new JsonObject { ["path"] = file });
            var info = again.Call("remote.issueInfo");
            Assert.Equal([g.ToString("D")], info["known"]!.AsArray().Select(x => (string?)x));
            // The seq is the journal's as it was read before this copy's own writes: those are read back once, and change nothing.
            Assert.Equal(0, (long)info["last"]!);
            Assert.NotNull(again.Remark(g));
            Assert.Equal(0, (int)again.Sync()["changedHere"]!);
            Assert.Equal(server.Journal.Count, (long)again.Call("remote.issueInfo")["last"]!);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void The_servers_own_api_is_reached_through_the_engine_and_only_it()
    {
        var (_, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var me = mihai.Call("remote.atrium", new JsonObject { ["path"] = "atrium/1.0/account" });
        Assert.Equal(200, (int)me["status"]!);
        Assert.Equal("mihai@example.com", (string?)me["body"]!["email"]);
        foreach (var bad in new[] { "lapin-remote/1.0/projects/x/refs", "atrium/1.0/../lapin-remote/1.0/projects", "foundation/oauth2/token" })
            Assert.Throws<InvalidOperationException>(() => mihai.Call("remote.atrium", new JsonObject { ["path"] = bad }));
    }
}
