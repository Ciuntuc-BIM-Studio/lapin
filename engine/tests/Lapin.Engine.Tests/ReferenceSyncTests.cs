using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform.Cde.References;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The reference documents synced with a CDE server's register (<c>remote.syncReferences</c>; docs/plan-cde-references.md R4) against a
/// fake Atrium Server that keeps the records as R3 does (docs/atrium-server.md, "Reference documents (R3)"): lapin-remote's objects, the
/// records by GlobalId with ETags, merge patches with If-Match, supersede and withdraw by their own requests, owners alone deleting, the
/// folders and types with their shared version, the journal. A document added here reaches the server with its file and another user;
/// a change there comes here in one undo step; two users' edits of different fields are both kept, of one field the last writer's; a 412
/// reads again and sends again; a file above the keep limit stays on the server and is fetched when opened; deletions both ways, a refused
/// one bringing the document back; an older server is left alone; the folders, types and checklist go with them; a server's documents stay
/// out of "uncommitted".
/// </summary>
public sealed class ReferenceSyncTests
{
    const string Root = "http://127.0.0.1:5997";
    const string Project = "1c2d3e4f-1111-4222-8333-944455556666";

    sealed class FakeRegister : HttpMessageHandler
    {
        public readonly Dictionary<string, (string Email, string Name, string Role, string Author)> Tokens = new()
        {
            ["tok-mihai"] = ("mihai@example.com", "Mihai Ionescu", "owner", "atrium:5d2c8a4e-0000-4000-8000-000000000001"),
            ["tok-ana"] = ("ana@example.com", "Ana Popescu", "editor", "atrium:5d2c8a4e-0000-4000-8000-000000000002"),
            ["tok-vio"] = ("viewer@example.com", "Victor Viewer", "viewer", "atrium:5d2c8a4e-0000-4000-8000-000000000003"),
        };
        public bool Offered = true;
        public readonly Dictionary<string, byte[]> Objects = new(StringComparer.Ordinal);
        public readonly Dictionary<string, (JsonObject Record, string AddedBy)> Docs = new(StringComparer.Ordinal);
        public readonly List<JsonObject> Journal = [];
        public JsonObject Settings = new()
        {
            ["folders"] = ReferenceDefaults.Folders(), ["typeFolders"] = ReferenceDefaults.TypeFolders(ReferenceDefaults.Types()), ["roleFolders"] = new JsonObject(),
            ["language"] = "en", ["types"] = ReferenceDefaults.Types(), ["checklist"] = ReferenceDefaults.Checklist(),
        };
        public int SettingsVersion, Patches, Refused412, ReferenceCalls;
        public bool Custom;
        public Action? BeforePatch;
        int _tick;

        string Now() => new DateTime(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc).AddSeconds(++_tick).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        public static string ETag(JsonObject r) => "\"" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(r.ToJsonString())))[..16].ToLowerInvariant() + "\"";

        void Log(string type, string id, string author, JsonNode? old = null, JsonNode? @new = null) => Journal.Add(new JsonObject
        {
            ["seq"] = (long)(Journal.Count + 1), ["type"] = type, ["topicGuid"] = "", ["referenceId"] = id, ["old"] = old?.ToJsonString(), ["new"] = @new?.ToJsonString(),
            ["author"] = author, ["authorName"] = Tokens.Values.FirstOrDefault(t => t.Author == author).Name ?? author, ["date"] = Now(),
        });

        static HttpResponseMessage Json(HttpStatusCode status, JsonNode? o, string? etag = null)
        {
            var r = new HttpResponseMessage(status) { Content = new StringContent(o?.ToJsonString() ?? "{}", Encoding.UTF8, "application/json") };
            if (etag != null) r.Headers.TryAddWithoutValidation("ETag", etag);
            return r;
        }
        static HttpResponseMessage Error(int status, string code, string message, JsonObject? more = null)
        {
            var o = new JsonObject { ["code"] = code, ["message"] = message };
            foreach (var (k, v) in more ?? []) o[k] = v?.DeepClone();
            return Json((HttpStatusCode)status, o);
        }

        public JsonObject Shown(string id)
        {
            var o = (JsonObject)Docs[id].Record.DeepClone();
            o["etag"] = ETag(Docs[id].Record);
            o["textStatus"] = "text";
            o["folderPath"] = "derived, never written back";
            return o;
        }

        /// <summary>A record changed as another member would (journaled).</summary>
        public void ServerEdit(string id, string field, JsonNode? value, string author)
        {
            var r = Docs[id].Record;
            var old = r[field]?.DeepClone();
            if (value == null) r.Remove(field); else r[field] = value.DeepClone();
            r["modifiedAt"] = Now();
            r["modifiedBy"] = author;
            Log("reference_changed", id, author, new JsonObject { [field] = old }, new JsonObject { [field] = value?.DeepClone() });
        }

        public void ServerDelete(string id, string author)
        {
            Log("reference_deleted", id, author, Docs[id].Record.DeepClone());
            Docs.Remove(id);
        }

        static readonly string[] Writable = ["title", "type", "issuer", "number", "discipline", "date", "validFrom", "validUntil", "phase", "folder", "requires", "relatesTo", "requestedAt", "requestedBy", "files"];

        /// <summary>A merge patch over a record, as R3's ReferenceModel.Apply; a problem as (status, code, extra).</summary>
        (int, string, JsonObject?)? Apply(JsonObject record, JsonObject given)
        {
            foreach (var (k, v) in given)
            {
                if (k == "status")
                {
                    var s = (string?)v;
                    if (s is "superseded" or "withdrawn") return (422, "use_action", null);
                    if (s is not ("requested" or "received")) return (400, "bad_request", null);
                    record["status"] = s;
                }
                else if (k == "fields")
                {
                    var kept = record["fields"] as JsonObject ?? [];
                    foreach (var (fk, fv) in (JsonObject)v!) if (fv == null || (string?)fv == "") kept.Remove(fk); else kept[fk] = fv.DeepClone();
                    if (kept.Count == 0) record.Remove("fields"); else record["fields"] = kept;
                }
                else if (k == "files")
                {
                    var missing = (v as JsonArray ?? []).OfType<JsonObject>().Select(f => (string)f["sha256"]!).Where(s => !Objects.ContainsKey(s)).ToList();
                    if (missing.Count > 0) return (422, "object_missing", new JsonObject { ["missing"] = new JsonArray([.. missing.Select(x => (JsonNode)x)]) });
                    record["files"] = new JsonArray([.. (v as JsonArray ?? []).OfType<JsonObject>().Select(f => (JsonNode)new JsonObject
                    {
                        ["name"] = (string?)f["name"], ["sha256"] = (string?)f["sha256"], ["size"] = (long)f["size"]!, ["mime"] = (string?)f["mime"] ?? "application/octet-stream",
                    })]);
                }
                else if (Writable.Contains(k))
                {
                    if (v == null || v is JsonValue sv && sv.TryGetValue<string>(out var t) && t.Length == 0) record.Remove(k); else record[k] = v.DeepClone();
                }
            }
            return null;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Send(request, cancellationToken));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);
            var method = request.Method.Method;
            var raw = request.Content?.ReadAsByteArrayAsync(cancellationToken).GetAwaiter().GetResult() ?? [];
            switch (path)
            {
                case "/foundation/versions":
                    return Json(HttpStatusCode.OK, new JsonObject { ["versions"] = new JsonArray(new JsonObject { ["api_id"] = "lapin-remote", ["version_id"] = "1.0", ["api_base_url"] = Root + "/lapin-remote/1.0" }) });
                case "/lapin-remote/1.0/capabilities":
                    return Json(HttpStatusCode.OK, new JsonObject { ["protocol"] = "lapin-remote", ["version"] = "1.0", ["majors"] = new JsonArray(1), ["features"] = new JsonArray(), ["auth"] = new JsonObject { ["foundation"] = "/foundation/1.1" } });
                case "/atrium/1.0/" or "/atrium/1.0":
                    return Offered ? Json(HttpStatusCode.OK, new JsonObject { ["api"] = "atrium", ["version"] = "1.0", ["features"] = new JsonArray("account", "topics", "events", "references") }) : Error(404, "not_found", "No.");
            }
            if (request.Headers.Authorization?.Parameter is not { } token || !Tokens.TryGetValue(token, out var who)) return Error(401, "unauthorized", "Sign in first.");
            var (email, name, role, me) = who;
            var a = $"/atrium/1.0/projects/{Project}";
            var o = $"/lapin-remote/1.0/projects/{Project}/objects";
            var r = $"{a}/references";
            var text = raw.Length > 0 && request.Content?.Headers.ContentType?.MediaType == "application/json" ? Encoding.UTF8.GetString(raw) : "";
            var body = text.Length > 0 ? JsonNode.Parse(text) as JsonObject : null;
            var ifMatch = request.Headers.TryGetValues("If-Match", out var im) ? im.First() : null;
            if (path == "/atrium/1.0/account") return Json(HttpStatusCode.OK, new JsonObject { ["id"] = email, ["email"] = email, ["name"] = name, ["authorId"] = me });
            if (path == $"{a}/rules") return Json(HttpStatusCode.OK, new JsonObject { ["role"] = role });
            if (path == $"{a}/events")
            {
                var after = long.Parse(System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["after"] ?? "0", CultureInfo.InvariantCulture);
                var rows = Journal.Where(e => (long)e["seq"]! > after).ToList();
                return Json(HttpStatusCode.OK, new JsonObject { ["events"] = new JsonArray([.. rows.Select(x => (JsonNode)x.DeepClone())]), ["last"] = rows.Count > 0 ? (long)rows[^1]["seq"]! : after, ["more"] = false });
            }
            if (path == $"{o}/missing" && method == "POST")
                return Json(HttpStatusCode.OK, new JsonObject { ["missing"] = new JsonArray([.. body!["hashes"]!.AsArray().Select(x => (string)x!).Where(x => !Objects.ContainsKey(x)).Select(x => (JsonNode)x)]) });
            if (path.StartsWith($"{o}/", StringComparison.Ordinal))
            {
                var sha = path[(o.Length + 1)..];
                if (method == "PUT")
                {
                    if (Hashes.Sha256Hex(raw) != sha) return Error(400, "hash_mismatch", "Not its hash.");
                    Objects[sha] = raw;
                    return Json(HttpStatusCode.Created, new JsonObject());
                }
                return Objects.TryGetValue(sha, out var bytes) ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) } : Error(404, "not_found", "No object.");
            }
            if (!path.StartsWith(r, StringComparison.Ordinal)) return Error(404, "not_found", "Not here.");
            ReferenceCalls++;
            var rest = path[r.Length..].TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            string SettingsTag() => $"\"s{SettingsVersion}\"";
            if (rest.Length == 0 && method == "GET")
                return Json(HttpStatusCode.OK, new JsonObject { ["references"] = new JsonArray([.. Docs.Keys.Select(id => (JsonNode)Shown(id))]), ["count"] = Docs.Count, ["today"] = "2026-11-25" });
            if (rest.Length == 0 && method == "POST")
            {
                if (role == "viewer") return Error(403, "forbidden", "Viewers read the reference documents.");
                var id = (string?)body!["id"] ?? GlobalId.New();
                if (Docs.ContainsKey(id)) return Error(409, "exists", "The project has it.");
                var rec = new JsonObject { ["id"] = id, ["title"] = (string?)body["title"] };
                if (Apply(rec, body) is var (st, code, more)) return Error(st, code, code, more);
                rec["status"] ??= rec["files"] is JsonArray { Count: > 0 } ? "received" : "requested";
                rec["addedAt"] = Now(); rec["addedBy"] = me; rec["modifiedAt"] = rec["addedAt"]!.DeepClone(); rec["modifiedBy"] = me;
                Docs[id] = (rec, me);
                Log("reference_added", id, me, null, rec);
                return Json(HttpStatusCode.Created, Shown(id), ETag(rec));
            }
            if (rest is ["folders"])
            {
                if (method == "PUT")
                {
                    if (role != "owner") return Error(403, "forbidden", "Owners change the folders.");
                    if (ifMatch != null && ifMatch != SettingsTag()) return Error(412, "changed", "Changed.");
                    foreach (var k in new[] { "folders", "language" }) if (body![k] is { } v) Settings[k] = v.DeepClone();
                    foreach (var k in new[] { "typeFolders", "roleFolders" })
                        if (body![k] is JsonObject map)
                        {
                            var cur = (JsonObject)Settings[k]!;
                            foreach (var (mk, mv) in map) if (mv == null || (string?)mv == "") cur.Remove(mk); else cur[mk] = mv.DeepClone();
                        }
                    SettingsVersion++; Custom = true;
                }
                var tree = (JsonArray)Settings["folders"]!.DeepClone();
                foreach (var n in tree.OfType<JsonObject>()) { n["count"] = 0; n["text"] = "x"; n["total"] = 0; }
                return Json(HttpStatusCode.OK, new JsonObject
                {
                    ["folders"] = tree, ["typeFolders"] = Settings["typeFolders"]!.DeepClone(), ["roleFolders"] = Settings["roleFolders"]!.DeepClone(), ["language"] = (string?)Settings["language"],
                    ["unfiled"] = 0, ["custom"] = Custom, ["etag"] = SettingsTag(),
                }, SettingsTag());
            }
            if (rest is ["types"])
            {
                if (method == "PUT")
                {
                    if (role != "owner") return Error(403, "forbidden", "Owners change the types.");
                    if (ifMatch != null && ifMatch != SettingsTag()) return Error(412, "changed", "Changed.");
                    foreach (var k in new[] { "types", "checklist" }) if (body![k] is { } v) Settings[k] = v.DeepClone();
                    SettingsVersion++; Custom = true;
                }
                var checklist = (JsonArray)Settings["checklist"]!.DeepClone();
                foreach (var c in checklist.OfType<JsonObject>()) c["text"] = "shown";
                return Json(HttpStatusCode.OK, new JsonObject
                {
                    ["types"] = Settings["types"]!.DeepClone(), ["typeFolders"] = Settings["typeFolders"]!.DeepClone(), ["checklist"] = checklist, ["issuers"] = new JsonArray(), ["custom"] = Custom, ["etag"] = SettingsTag(),
                }, SettingsTag());
            }
            var docId = rest[0];
            if (!Docs.TryGetValue(docId, out var d)) return Error(404, "no_reference", "No such document.");
            var mayChange = role == "owner" || role == "editor" && d.AddedBy == me;
            if (rest.Length == 1 && method == "GET") return Json(HttpStatusCode.OK, Shown(docId), ETag(d.Record));
            if (rest is [_, "files", var fileName] && method == "GET")
            {
                var f = (d.Record["files"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(x => (string?)x["name"] == fileName);
                return f == null ? Error(404, "no_file", "No file.") : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Objects[(string)f["sha256"]!]) };
            }
            if (rest.Length == 1 && method == "DELETE")
            {
                if (role != "owner") return Error(403, "forbidden", "Owners delete reference documents.");
                if (ifMatch != null && ifMatch != ETag(d.Record)) return Error(412, "changed", "Changed.");
                ServerDelete(docId, me);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (!mayChange) return Error(403, "forbidden", role == "viewer" ? "Viewers read the reference documents." : "Editors change the reference documents they added.");
            if (ifMatch != null && ifMatch != "*" && ifMatch != ETag(d.Record)) { Refused412++; return Json(HttpStatusCode.PreconditionFailed, new JsonObject { ["code"] = "changed", ["message"] = "Changed." }, ETag(d.Record)); }
            if (rest.Length == 1 && method == "PATCH")
            {
                Patches++;
                BeforePatch?.Invoke();
                BeforePatch = null;
                if (ifMatch != null && ifMatch != ETag(d.Record)) { Refused412++; return Json(HttpStatusCode.PreconditionFailed, new JsonObject { ["code"] = "changed", ["message"] = "Changed." }, ETag(d.Record)); }
                var next = (JsonObject)d.Record.DeepClone();
                if (Apply(next, body!) is var (st, code, more)) return Error(st, code, code, more);
                if (!JsonNode.DeepEquals(next, d.Record))
                {
                    next["modifiedAt"] = Now(); next["modifiedBy"] = me;
                    Docs[docId] = (next, d.AddedBy);
                    Log("reference_changed", docId, me, null, body);
                }
                return Json(HttpStatusCode.OK, Shown(docId), ETag(Docs[docId].Record));
            }
            if (rest is [_, "supersede"] && method == "POST")
            {
                var by = (string?)body!["by"];
                if (by == null || !Docs.ContainsKey(by)) return Error(422, "unknown_document", "No such successor.");
                d.Record["status"] = "superseded"; d.Record["supersededBy"] = by; d.Record["modifiedAt"] = Now(); d.Record["modifiedBy"] = me;
                Log("reference_superseded", docId, me, null, by);
                return Json(HttpStatusCode.OK, Shown(docId), ETag(d.Record));
            }
            if (rest is [_, "withdraw"] && method == "POST")
            {
                d.Record["status"] = "withdrawn"; d.Record["reason"] = (string?)body!["reason"]; d.Record["withdrawnAt"] = Now(); d.Record["modifiedAt"] = Now(); d.Record["modifiedBy"] = me;
                Log("reference_withdrawn", docId, me, null, (string?)body["reason"]);
                return Json(HttpStatusCode.OK, Shown(docId), ETag(d.Record));
            }
            return Error(404, "not_found", "Not here.");
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

        public JsonObject Sync() => (JsonObject)Call("remote.syncReferences")["report"]!;
        public Entity? Doc(string id) => F.Session.Model.Get(id) is { Class: References.Class } e ? e : null;
        public string Add(JsonObject a) => (string)F.Run("references.add", a)!["id"]!;
        public void Update(string id, JsonObject a) { a["id"] = id; F.Run("references.update", a); }
    }

    static (FakeRegister Server, HttpClient Http) Server() { var s = new FakeRegister(); return (s, new HttpClient(s)); }

    static JsonObject File(string name, byte[] bytes) => new() { ["name"] = name, ["data"] = Convert.ToBase64String(bytes) };

    static readonly byte[] Certificate = Encoding.UTF8.GetBytes("CERTIFICAT DE URBANISM Nr. 123 din 12.03.2026");

    static string Refused(JsonObject report) => string.Join(",", (report["refused"] as JsonArray ?? []).Select(x => (string?)x!["code"]));

    [Fact]
    public void A_document_added_here_goes_with_its_id_and_file_and_reaches_the_other_user()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var id = mihai.Add(new JsonObject { ["files"] = new JsonArray(File("CU 123.txt", Certificate)), ["type"] = "cu", ["number"] = "123", ["date"] = "12.03.2026", ["fields"] = new JsonObject { ["parcel"] = "12345" } });

        var r = mihai.Sync();
        Assert.True((bool)r["offered"]!);
        Assert.Equal((1, 1), ((int)r["created"]!, (int)r["uploaded"]!));
        Assert.Equal("", Refused(r));
        var rec = server.Docs[id].Record;
        Assert.Equal(("cu", "123", "2026-03-12", "input-cu"), ((string?)rec["type"], (string?)rec["number"], (string?)rec["date"], (string?)rec["folder"]));
        Assert.Equal("12345", (string?)rec["fields"]!["parcel"]);
        Assert.Equal(Certificate, server.Objects[Hashes.Sha256Hex(Certificate)]);
        Assert.Null(rec["held"]);
        // Here the server's own members came back (who added it, when), and a second sync does nothing.
        Assert.Equal("atrium:5d2c8a4e-0000-4000-8000-000000000001", mihai.Doc(id)!.Str("addedBy"));
        var again = mihai.Sync();
        Assert.Equal(0, (int)again["changedHere"]!);
        Assert.Equal(0, server.Patches);

        var ana = new Client(http, "tok-ana");
        var first = ana.Sync();
        Assert.True((bool)first["first"]!);
        Assert.Equal((1, 1), ((int)first["added"]!, (int)first["downloaded"]!));
        var doc = ana.Doc(id)!;
        Assert.Equal("CU 123", doc.Name);
        Assert.Equal("123", doc.Str("number"));
        var f = Assert.Single(References.Files(doc));
        Assert.True(f.Held);
        var bytes = Convert.FromBase64String((string)ana.F.Session.Query("references.file", new JsonObject { ["id"] = id })!["data"]!);
        Assert.Equal(Certificate, bytes);
    }

    [Fact]
    public void A_change_there_comes_here_in_one_undo_step_and_derived_members_are_not_kept()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var id = mihai.Add(new JsonObject { ["title"] = "Aviz apă", ["type"] = "aviz", ["issuer"] = "Apavital" });
        mihai.Sync();
        server.ServerEdit(id, "number", "45/2026", "atrium:5d2c8a4e-0000-4000-8000-000000000002");
        server.ServerEdit(id, "validUntil", "2027-01-31", "atrium:5d2c8a4e-0000-4000-8000-000000000002");

        var r = mihai.Sync();
        Assert.Equal(1, (int)r["changedHere"]!);
        var by = Assert.Single(r["byOthers"]!.AsArray());
        Assert.Equal("Ana Popescu", (string?)by!["who"]);
        Assert.Equal(("45/2026", "2027-01-31"), (mihai.Doc(id)!.Str("number"), mihai.Doc(id)!.Str("validUntil")));
        Assert.Null(mihai.Doc(id)!.Attrs["folderPath"]);
        Assert.Null(mihai.Doc(id)!.Attrs["etag"]);
        // One undo step takes the server's change back out (it would be sent again as a change here, by design).
        mihai.F.Session.Undo(Lapin.Model.History.Me);
        Assert.Null(mihai.Doc(id)!.Str("number"));
    }

    [Fact]
    public void Two_users_editing_different_fields_both_keep_theirs_and_the_same_field_is_the_last_writers()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var id = mihai.Add(new JsonObject { ["title"] = "Aviz ISU", ["type"] = "aviz", ["fields"] = new JsonObject { ["a"] = "1" } });
        mihai.Sync();
        var vio = new Client(http, "tok-vio");
        vio.Sync();
        var owner2 = new Client(http, "tok-mihai");
        owner2.Sync();

        mihai.Update(id, new JsonObject { ["number"] = "7", ["title"] = "Aviz ISU nr. 7", ["fields"] = new JsonObject { ["a"] = "2" } });
        owner2.Update(id, new JsonObject { ["issuer"] = "ISU Iași", ["title"] = "Aviz securitate la incendiu", ["fields"] = new JsonObject { ["b"] = "x" } });
        Assert.Equal("", Refused(mihai.Sync()));
        Assert.Equal("", Refused(owner2.Sync()));     // last writer of the title
        mihai.Sync();
        foreach (var c in new[] { mihai, owner2 })
        {
            var d = c.Doc(id)!;
            Assert.Equal(("7", "ISU Iași", "Aviz securitate la incendiu"), (d.Str("number"), d.Str("issuer"), d.Name));
            Assert.Equal(("2", "x"), ((string?)d.Attrs["fields"]!["a"], (string?)d.Attrs["fields"]!["b"]));
        }
        Assert.Equal("Aviz securitate la incendiu", (string?)server.Docs[id].Record["title"]);

        // A viewer's edit is refused, and the server's values come back.
        vio.Sync();
        vio.Update(id, new JsonObject { ["number"] = "999" });
        Assert.Equal("forbidden", Refused(vio.Sync()));
        Assert.Equal("7", vio.Doc(id)!.Str("number"));
    }

    [Fact]
    public void A_412_reads_the_document_again_and_sends_the_change_again()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var id = mihai.Add(new JsonObject { ["title"] = "Studiu geo", ["type"] = "studiu-geo" });
        mihai.Sync();
        mihai.Update(id, new JsonObject { ["number"] = "G-12" });
        server.BeforePatch = () => server.ServerEdit(id, "issuer", "GeoConsult", "atrium:5d2c8a4e-0000-4000-8000-000000000002");
        var r = mihai.Sync();
        Assert.Equal(1, (int)r["conflicts"]!);
        Assert.Equal(1, server.Refused412);
        Assert.Equal("", Refused(r));
        Assert.Equal(("G-12", "GeoConsult"), ((string?)server.Docs[id].Record["number"], (string?)server.Docs[id].Record["issuer"]));
        Assert.Equal(("G-12", "GeoConsult"), (mihai.Doc(id)!.Str("number"), mihai.Doc(id)!.Str("issuer")));
    }

    [Fact]
    public void A_file_above_the_keep_limit_stays_on_the_server_and_is_fetched_when_opened()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var big = new byte[4096];
        new Random(7).NextBytes(big);
        var id = mihai.Add(new JsonObject { ["files"] = new JsonArray(File("studiu.bin", big)), ["type"] = "studiu-geo" });
        mihai.Sync();

        var ana = new Client(http, "tok-ana");
        ana.F.Run("references.setKeepLimit", new JsonObject { ["bytes"] = 1000 });
        var r = ana.Sync();
        Assert.Equal(0, (int)r["downloaded"]!);
        var f = Assert.Single(References.Files(ana.Doc(id)!));
        Assert.False(f.Held);
        Assert.Equal(big.LongLength, f.Size);
        Assert.Throws<Lapin.Platform.CommandException>(() => ana.F.Session.Query("references.file", new JsonObject { ["id"] = id }));
        var fetched = ana.Call("remote.referenceFile", new JsonObject { ["id"] = id });
        Assert.Equal(big, Convert.FromBase64String((string)fetched["data"]!));
    }

    [Fact]
    public void Deletions_go_both_ways_as_the_role_allows_and_a_refused_one_brings_the_document_back()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var a = mihai.Add(new JsonObject { ["title"] = "Added by mistake" });
        var b = mihai.Add(new JsonObject { ["title"] = "Deleted on the server" });
        var c = mihai.Add(new JsonObject { ["title"] = "An editor cannot delete" });
        mihai.Sync();
        var ana = new Client(http, "tok-ana");
        ana.Sync();

        mihai.F.Run("references.delete", new JsonObject { ["id"] = a });
        var r = mihai.Sync();
        Assert.Equal(1, (int)r["deleted"]!);
        Assert.False(server.Docs.ContainsKey(a));

        server.ServerDelete(b, "atrium:5d2c8a4e-0000-4000-8000-000000000001");
        var ra = ana.Sync();
        Assert.Equal(2, (int)ra["removed"]!);
        Assert.Null(ana.Doc(a));
        Assert.Null(ana.Doc(b));

        ana.F.Run("references.delete", new JsonObject { ["id"] = c });
        var refused = ana.Sync();
        Assert.Equal("delete_forbidden", Refused(refused));
        Assert.NotNull(ana.Doc(c));
        Assert.True(server.Docs.ContainsKey(c));
    }

    [Fact]
    public void Superseding_and_withdrawing_go_through_their_own_requests_with_the_successor_first()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var old = mihai.Add(new JsonObject { ["title"] = "Aviz 2025", ["type"] = "aviz" });
        var renewed = mihai.Add(new JsonObject { ["title"] = "Aviz 2026", ["type"] = "aviz" });
        var wrong = mihai.Add(new JsonObject { ["title"] = "Wrong project" });
        mihai.F.Run("references.supersede", new JsonObject { ["id"] = old, ["by"] = renewed });
        mihai.F.Run("references.withdraw", new JsonObject { ["id"] = wrong, ["reason"] = "Sent to the wrong project." });
        var cu = mihai.Add(new JsonObject { ["title"] = "CU", ["type"] = "cu", ["requires"] = new JsonArray(new JsonObject { ["key"] = "water", ["document"] = renewed }) });

        Assert.Equal("", Refused(mihai.Sync()));
        Assert.Equal(("superseded", renewed), ((string?)server.Docs[old].Record["status"], (string?)server.Docs[old].Record["supersededBy"]));
        Assert.Equal(("withdrawn", "Sent to the wrong project."), ((string?)server.Docs[wrong].Record["status"], (string?)server.Docs[wrong].Record["reason"]));
        Assert.Equal(renewed, (string?)server.Docs[cu].Record["requires"]![0]!["document"]);
        Assert.Equal(0, (int)mihai.Sync()["changedHere"]!);

        // Withdrawn later, here: sent as a withdrawal.
        mihai.F.Run("references.withdraw", new JsonObject { ["id"] = renewed, ["reason"] = "Annulled." });
        Assert.Equal("", Refused(mihai.Sync()));
        Assert.Equal("withdrawn", (string?)server.Docs[renewed].Record["status"]);
    }

    [Fact]
    public void An_older_server_without_the_register_is_left_alone()
    {
        var (server, http) = Server();
        server.Offered = false;
        var mihai = new Client(http, "tok-mihai");
        mihai.Add(new JsonObject { ["title"] = "Kept here" });
        var r = mihai.Sync();
        Assert.False((bool)r["offered"]!);
        Assert.Equal(0, server.ReferenceCalls);
        Assert.Single(References.All(mihai.F.Session.Model));
    }

    [Fact]
    public void Folders_types_and_the_checklist_go_with_them()
    {
        var (server, http) = Server();
        var mihai = new Client(http, "tok-mihai");
        var tree = References.Folders(mihai.F.Session.Model);
        tree.Add((JsonNode)new JsonObject { ["id"] = "site", ["name"] = new JsonObject { ["ro"] = "07 Șantier", ["en"] = "07 Site" }, ["children"] = new JsonArray() });
        mihai.F.Run("references.setFolders", new JsonObject { ["folders"] = tree });
        mihai.F.Run("references.setChecklist", new JsonObject { ["checklist"] = new JsonArray(new JsonObject { ["key"] = "drumuri", ["label"] = "Aviz drumuri" }) });
        var id = mihai.Add(new JsonObject { ["title"] = "Proces verbal", ["folder"] = "site" });

        var r = mihai.Sync();
        Assert.Equal("", Refused(r));
        Assert.Equal("sent", (string?)r["settings"]);
        Assert.Contains(server.Settings["folders"]!.AsArray(), n => (string?)n!["id"] == "site");
        Assert.Equal("drumuri", (string?)server.Settings["checklist"]![0]!["key"]);
        Assert.Equal("site", (string?)server.Docs[id].Record["folder"]);

        // Another owner's copy takes them, and a change of the roles' folders there comes back.
        var other = new Client(http, "tok-mihai");
        var first = other.Sync();
        Assert.Equal("received", (string?)first["settings"]);
        Assert.Contains(References.Folders(other.F.Session.Model), n => (string?)n!["id"] == "site");
        Assert.Equal("drumuri", (string?)References.ChecklistOptions(other.F.Session.Model)[0]!["key"]);
        ((JsonObject)server.Settings["roleFolders"]!)["A"] = "issued-sheets";
        server.SettingsVersion++;
        mihai.Sync();
        Assert.Equal("issued-sheets", (string?)References.RoleFolders(mihai.F.Session.Model)["A"]);

        // An editor's change of the folders is refused: the server's come back.
        var ana = new Client(http, "tok-ana");
        ana.Sync();
        ana.F.Run("references.setFolders", new JsonObject { ["roleFolders"] = new JsonObject { ["S"] = "issued-sheets" } });
        Assert.Equal("forbidden", Refused(ana.Sync()));
        Assert.Null(References.RoleFolders(ana.F.Session.Model)["S"]);
    }

    [Fact]
    public void The_sync_state_lives_with_the_history_and_a_servers_documents_stay_out_of_uncommitted()
    {
        var (server, http) = Server();
        var dir = Directory.CreateTempSubdirectory("lapin-refsync-").FullName;
        try
        {
            var mihai = new Client(http, "tok-mihai");
            var file = Path.Combine(dir, "p.lapin");
            mihai.Call("project.save", new JsonObject { ["path"] = file });
            mihai.Call("history.commit", new JsonObject { ["message"] = "First" });
            Assert.False((bool)mihai.Call("history.status")["uncommitted"]!);
            var id = mihai.Add(new JsonObject { ["files"] = new JsonArray(File("cu.txt", Certificate)), ["type"] = "cu" });
            Assert.False((bool)mihai.Call("history.status")["uncommitted"]!);
            mihai.Sync();
            mihai.Call("project.save", new JsonObject { ["path"] = file });

            var again = new Client(http, "tok-mihai");
            again.Call("project.open", new JsonObject { ["path"] = file });
            Assert.NotNull(again.Doc(id));
            var r = again.Sync();
            Assert.False((bool)r["first"]!);
            Assert.Equal(0, (int)r["changedHere"]!);
            Assert.Equal(0, server.Patches);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// The same against a running Atrium Server with the register (R3), as <see cref="RemoteServerTests"/>: only when <c>LAPIN_REMOTE_SERVER</c>
    /// names it, with <c>LAPIN_REMOTE_PROJECT</c> (a project with no commit yet, Mihai its owner, Ana an editor), <c>LAPIN_REMOTE_MIHAI</c>,
    /// <c>LAPIN_REMOTE_ANA</c> and <c>LAPIN_REMOTE_PASSWORD</c>; passes at once without them. A document goes up with its file and reaches the
    /// other user; a pull leaves the documents as they are here whatever the commit held; the server's version then comes with a sync.
    /// </summary>
    [Fact]
    public void Against_a_running_server_documents_sync_and_a_pull_leaves_them_alone()
    {
        var url = Environment.GetEnvironmentVariable("LAPIN_REMOTE_SERVER");
        if (string.IsNullOrEmpty(url)) return;
        var project = Environment.GetEnvironmentVariable("LAPIN_REMOTE_PROJECT")!;
        var password = Environment.GetEnvironmentVariable("LAPIN_REMOTE_PASSWORD")!;
        var dir = Directory.CreateTempSubdirectory("lapin-refsync-live-").FullName;
        try
        {
            var mihai = new Live(new Fixture());
            mihai.SignIn(url, Environment.GetEnvironmentVariable("LAPIN_REMOTE_MIHAI")!, password, "Mihai Ionescu");
            mihai.F.Wall(0, 0, 6, 0);
            mihai.Call("project.save", new JsonObject { ["path"] = Path.Combine(dir, "mihai.lapin") });
            mihai.Call("remote.add", new JsonObject { ["kind"] = "server", ["url"] = url, ["project"] = project });
            var id = (string)mihai.F.Run("references.add", new JsonObject { ["files"] = new JsonArray(File("CU 123.txt", Certificate)), ["type"] = "cu", ["number"] = "123", ["issuer"] = "Primăria Iași" })!["id"]!;
            mihai.Call("history.commit", new JsonObject { ["message"] = "Initial model" });
            Assert.True((bool)mihai.Call("remote.push")!["pushed"]!);
            var up = (JsonObject)mihai.Call("remote.syncReferences")!["report"]!;
            Assert.True((bool)up["offered"]!, up.ToJsonString());
            Assert.Equal("", Refused(up));
            Assert.Equal(1, (int)up["created"]!);

            var ana = new Live(new Fixture());
            ana.SignIn(url, Environment.GetEnvironmentVariable("LAPIN_REMOTE_ANA")!, password, "Ana Popescu");
            ana.Call("remote.clone", new JsonObject { ["kind"] = "server", ["url"] = url, ["project"] = project, ["path"] = Path.Combine(dir, "ana.lapin") });
            var down = (JsonObject)ana.Call("remote.syncReferences")!["report"]!;
            Assert.Equal("", Refused(down));
            var doc = ana.F.Session.Model.Get(id)!;
            Assert.Equal("123", doc.Str("number"));
            Assert.Equal(Certificate, Convert.FromBase64String((string)ana.F.Session.Query("references.file", new JsonObject { ["id"] = id })!["data"]!));

            // Mihai changes the number here and commits it without syncing the documents: Ana's pull leaves her document alone.
            mihai.F.Run("references.update", new JsonObject { ["id"] = id, ["number"] = "123/2026" });
            mihai.F.Wall(0, 3, 6, 3);
            mihai.Call("history.commit", new JsonObject { ["message"] = "A wall and an edit" });
            Assert.True((bool)mihai.Call("remote.push")!["pushed"]!);
            var pulled = ana.Call("remote.pull")!;
            Assert.True((bool)pulled["pulled"]!, pulled.ToJsonString());
            Assert.Equal("123", ana.F.Session.Model.Get(id)!.Str("number"));
            // The server's register is the truth: a sync brings it.
            Assert.Equal("", Refused((JsonObject)mihai.Call("remote.syncReferences")!["report"]!));
            ana.Call("remote.syncReferences");
            Assert.Equal("123/2026", ana.F.Session.Model.Get(id)!.Str("number"));

            // Ana (an editor) edits a field, Mihai another: both kept.
            ana.F.Run("references.update", new JsonObject { ["id"] = id, ["validUntil"] = "2027-03-12" });
            mihai.F.Run("references.update", new JsonObject { ["id"] = id, ["discipline"] = "A" });
            Assert.Equal("forbidden", Refused((JsonObject)ana.Call("remote.syncReferences")!["report"]!));   // editors change what they added
            Assert.Equal("", Refused((JsonObject)mihai.Call("remote.syncReferences")!["report"]!));
            ana.Call("remote.syncReferences");
            Assert.Equal(("A", null), (ana.F.Session.Model.Get(id)!.Str("discipline"), ana.F.Session.Model.Get(id)!.Str("validUntil")));
        }
        finally { Directory.Delete(dir, recursive: true); }
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
    }
}
