using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Package;
using Lapin.Package.Remote;
using Lapin.Platform.Cde;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// An issue put on the remote (Atrium docs/plan-cde-web.md W4): submitted for approval on a CDE server (<c>remote.submitIssue</c>: the
/// head commit saved there first, the issue's number, revision, Shared or Published with its suitability, its documents, and the files
/// the project's file keeps sent as objects before the submission names them), or the issue tag made and pushed directly
/// (<c>remote.issueTag</c>, lapin-remote §4.4) where the rules allow it; refusals come back as answers with the server's codes.
/// </summary>
public sealed partial class CdeTests
{
    const string CdeRoot = "http://127.0.0.1:5998";
    const string CdeProject = "0b7c6f8e-1111-4222-8333-944455556666";

    /// <summary>The fake Atrium Server of the issue sync with lapin-remote's refs and objects, and the publish queue's POST.</summary>
    sealed class PublishServer(IssueSyncTests.FakeCde inner) : DelegatingHandler(inner)
    {
        public readonly Dictionary<string, string> Refs = new(StringComparer.Ordinal);
        public readonly Dictionary<string, byte[]> Objects = new(StringComparer.Ordinal);
        public readonly List<JsonObject> Submissions = [];
        public string IssueTags = "owner";
        public Dictionary<string, JsonObject> InnerTopics => inner.Topics;

        static HttpResponseMessage Json(HttpStatusCode s, JsonNode o) => new(s) { Content = new StringContent(o.ToJsonString(), Encoding.UTF8, "application/json") };
        static HttpResponseMessage Error(int s, string code, string message, JsonObject? more = null)
        {
            var o = new JsonObject { ["code"] = code, ["message"] = message };
            foreach (var (k, v) in more ?? []) o[k] = v?.DeepClone();
            return Json((HttpStatusCode)s, o);
        }
        /// <summary>The server's publish gate: the open topics (neither Closed nor Resolved) with a label or a reference link naming a submitted document.</summary>
        JsonObject Gate(JsonArray? documents)
        {
            var docs = (documents ?? []).Select(x => (string?)x).OfType<string>().ToList();
            var topics = new JsonArray();
            foreach (var t in inner.Topics.Values)
            {
                if ((string?)t["topic_status"] is { } st && (st.Equals("Closed", StringComparison.OrdinalIgnoreCase) || st.Equals("Resolved", StringComparison.OrdinalIgnoreCase))) continue;
                var labels = (t["labels"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>();
                var links = (t["reference_links"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>();
                var doc = docs.FirstOrDefault(d => labels.Any(l => l.Equals(d, StringComparison.OrdinalIgnoreCase)) || links.Any(l => l.Contains(d, StringComparison.OrdinalIgnoreCase)));
                if (doc != null) topics.Add((JsonNode)new JsonObject { ["guid"] = (string?)t["guid"], ["title"] = (string?)t["title"], ["document"] = doc });
            }
            return new JsonObject { ["passed"] = topics.Count == 0, ["topics"] = topics };
        }

        JsonObject RefsJson() => new() { ["refs"] = new JsonArray([.. Refs.Select(r => (JsonNode)new JsonObject { ["name"] = r.Key, ["target"] = r.Value })]) };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var p = $"/lapin-remote/1.0/projects/{CdeProject}";
            var role = request.Headers.Authorization?.Parameter switch { "tok-mihai" => "owner", "tok-ana" => "editor", "tok-vio" => "viewer", _ => null };
            if (role == null || !(path.StartsWith(p, StringComparison.Ordinal) || path.EndsWith("/submissions", StringComparison.Ordinal))) return await base.SendAsync(request, cancellationToken);
            var body = request.Content == null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var json = body is { Length: > 0 } && body[0] == '{' ? JsonNode.Parse(body) as JsonObject : null;
            if (path == $"/atrium/1.0/projects/{CdeProject}/submissions" && request.Method == HttpMethod.Post)
            {
                if (role == "viewer") return Error(403, "forbidden", "Viewers do not submit.");
                if (!Objects.ContainsKey((string)json!["commit"]!)) return Error(422, "unknown_commit", "Not a commit the server holds.");
                var lacking = (json["files"] as JsonArray ?? []).Select(f => (string?)f!["sha256"]).Where(s => !Objects.ContainsKey(s!)).ToList();
                if (lacking.Count > 0) return Error(422, "object_missing", "A file's blob is not on the server.", new JsonObject { ["missing"] = new JsonArray([.. lacking.Select(x => (JsonNode)x!)]) });
                if (Submissions.Any(s => (int?)s["issue"] == (int?)json["issue"] && (string?)s["revision"] == (string?)json["revision"]))
                    return Error(409, "already_submitted", "A pending submission asks for the same issue and revision.", new JsonObject { ["submission"] = "s1" });
                var s = (JsonObject)json.DeepClone();
                s["id"] = $"s{Submissions.Count + 1}";
                s["number"] = Submissions.Count + 1;
                s["status"] = "pending";
                s["gate"] = Gate(json["documents"] as JsonArray);
                Submissions.Add(s);
                return Json(HttpStatusCode.Created, s);
            }
            if (path == $"{p}/refs") return Json(HttpStatusCode.OK, RefsJson());
            if (path == $"{p}/objects/missing")
                return Json(HttpStatusCode.OK, new JsonObject { ["missing"] = new JsonArray([.. (json!["hashes"] as JsonArray)!.Select(x => (string?)x).Where(x => !Objects.ContainsKey(x!)).Select(x => (JsonNode)x!)]) });
            if (path.StartsWith($"{p}/objects/", StringComparison.Ordinal) && request.Method == HttpMethod.Get)
                return Objects.TryGetValue(path[($"{p}/objects/").Length..], out var held)
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(held) }
                    : Error(404, "object_missing", "Not held.");
            if (path.StartsWith($"{p}/objects/", StringComparison.Ordinal) && request.Method == HttpMethod.Put)
            {
                var id = path[($"{p}/objects/").Length..];
                if (ObjectIds.Of(body!) != id) return Error(422, "hash_mismatch", "Not its id.");
                Objects[id] = body!;
                return new HttpResponseMessage(HttpStatusCode.Created);
            }
            if (path == $"{p}/refs/update")
            {
                if (role == "viewer") return Error(403, "forbidden", "Viewers do not push.");
                foreach (var u in (json!["updates"] as JsonArray)!.OfType<JsonObject>())
                {
                    var name = (string)u["name"]!;
                    if (name.StartsWith("refs/tags/issue/", StringComparison.Ordinal) && IssueTags == "owner" && role != "owner") return Error(403, "forbidden", "Only owners make issue tags in this project.");
                    if (Refs.GetValueOrDefault(name) != (string?)u["old"]) return Error(409, "ref_moved", "Moved.", new JsonObject { ["refs"] = new JsonArray(new JsonObject { ["name"] = name, ["current"] = Refs.GetValueOrDefault(name) }) });
                    var target = (string?)u["new"];
                    if (target != null && !Objects.ContainsKey(target)) return Error(422, "object_missing", "Missing.", new JsonObject { ["missing"] = new JsonArray(new JsonObject { ["id"] = target }) });
                    if (target != null && RemoteObjects.KindOf(Objects[target]) == ObjectKind.Tag)
                        foreach (var file in RemoteObjects.ReadTag(Objects[target]).Files)
                            if (!Objects.ContainsKey(file.Sha256)) return Error(422, "object_missing", "A tag's file is missing.");
                    if (target == null) Refs.Remove(name); else Refs[name] = target;
                }
                return Json(HttpStatusCode.OK, RefsJson());
            }
            return Error(404, "not_found", "Not here.");
        }
    }

    sealed class CdeClient
    {
        public Fixture F { get; }
        public EngineDispatcher D { get; }
        public string Token = "tok-mihai";
        public CdeClient(Fixture f, HttpClient? http) { F = f; D = new EngineDispatcher(f.Session, PackageFiles.Create()) { RemoteHttp = http }; }

        public JsonObject Call(string method, JsonObject? p = null)
        {
            p ??= [];
            p["token"] = Token;
            p["author"] = new JsonObject { ["id"] = "local:a1a1a1a1a1a1a1a1", ["name"] = "Mihai Ionescu" };
            var (reply, _) = D.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
            var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
            if (o["error"] is { } err) throw new InvalidOperationException((string)err!);
            return o["result"] as JsonObject ?? [];
        }
    }

    /// <summary>A project with a sheet, saved, its remote added, committed and pushed, then issued (Shared, S3, P01) and saved to the remote again.</summary>
    CdeClient Issued(HttpClient? http, Func<CdeClient, JsonObject> remote, string container = "Shared", string status = "S3")
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var s = Sheet(f);
        var c = new CdeClient(f, http);
        c.Call("project.save", new JsonObject { ["path"] = Path.Combine(_dir, $"p{Guid.NewGuid():N}.lapin") });
        c.Call("history.commit", new JsonObject { ["message"] = "Model" });
        c.Call("remote.add", remote(c));
        Assert.True((bool)c.Call("remote.push", new JsonObject { ["noViewer"] = true })["pushed"]!);
        c.Call("cde.issue", new JsonObject { ["sheets"] = new JsonArray(s), ["revision"] = "P01", ["status"] = status, ["container"] = container, ["toFolder"] = false, ["note"] = "For coordination" });
        return c;
    }

    static void SaveToRemote(CdeClient c)
    {
        c.Call("history.commit", new JsonObject { ["message"] = "Issue 1" });
        Assert.True((bool)c.Call("remote.push", new JsonObject { ["noViewer"] = true })["pushed"]!);
    }

    static JsonObject Server() => new() { ["kind"] = "server", ["url"] = CdeRoot, ["project"] = CdeProject };

    [Fact]
    public void An_issue_is_submitted_with_its_commit_documents_and_files_the_server_was_sent_first()
    {
        var server = new PublishServer(new IssueSyncTests.FakeCde());
        var c = Issued(new HttpClient(server), _ => Server());
        // Not on the server yet: the issue's commit must be there first.
        Assert.Contains("save to the server first", Assert.Throws<InvalidOperationException>(() => c.Call("remote.submitIssue")).Message);
        SaveToRemote(c);

        var record = CdeIssues.All(c.F.Model).Single();
        var r = c.Call("remote.submitIssue", new JsonObject { ["note"] = "Please check" });
        Assert.True((bool)r["ok"]!, r.ToJsonString());
        Assert.Equal("refs/tags/issue/1-P01", (string?)r["tag"]);
        var s = server.Submissions.Single();
        Assert.Equal((string)c.Call("history.status")["head"]!, (string?)s["commit"]);
        Assert.Equal((1, "P01", "shared", "S3", "Please check"), ((int)s["issue"]!, (string?)s["revision"], (string?)s["purpose"], (string?)s["suitability"], (string?)s["note"]));
        Assert.Equal(record.Id, (string?)s["issueId"]);
        // The files the project's file keeps, each sent as an object before the submission named it; the documents without status and revision.
        var sent = (s["files"] as JsonArray)!.Select(x => (string)x!["sha256"]!).ToList();
        Assert.Equal(record.Files.Where(x => x.Stored).Select(x => x.Sha256).Distinct().Order(), sent.Order());
        Assert.All(sent, id => Assert.True(server.Objects.ContainsKey(id)));
        var pdf = record.Files.First(x => x.Name.EndsWith(".pdf", StringComparison.Ordinal) && x.SheetId != null).Name;
        Assert.Contains(pdf[..pdf.IndexOf("_S3_P01", StringComparison.Ordinal)], (s["documents"] as JsonArray)!.Select(x => (string?)x));
        Assert.DoesNotContain((s["documents"] as JsonArray)!, x => ((string?)x)!.Contains("_P01", StringComparison.Ordinal));

        // The same issue again: the server's refusal is an answer with its code.
        var again = c.Call("remote.submitIssue");
        Assert.Equal((false, "already_submitted", 409), ((bool)again["ok"]!, (string?)again["code"], (int)again["status"]!));
        Assert.Equal("s1", (string?)again["submission"]);
    }

    [Fact]
    public void A_WIP_issue_or_a_status_that_does_not_fit_is_refused_and_a_published_one_takes_its_A_code()
    {
        var server = new PublishServer(new IssueSyncTests.FakeCde());
        var c = Issued(new HttpClient(server), _ => Server(), container: "WIP", status: "S0");
        SaveToRemote(c);
        Assert.Contains("WIP", Assert.Throws<InvalidOperationException>(() => c.Call("remote.submitIssue")).Message);
        Assert.Contains("not a status for Published", Assert.Throws<InvalidOperationException>(() => c.Call("remote.submitIssue", new JsonObject { ["purpose"] = "published", ["suitability"] = "S2" })).Message);
        var r = c.Call("remote.submitIssue", new JsonObject { ["purpose"] = "published", ["next"] = true });
        Assert.True((bool)r["ok"]!, r.ToJsonString());
        Assert.Equal(("published", "A1"), ((string?)server.Submissions[0]["purpose"], (string?)server.Submissions[0]["suitability"]));
        Assert.Null(server.Submissions[0]["issue"]);
    }

    [Fact]
    public void The_issue_tag_is_made_and_pushed_directly_where_the_rules_allow_and_refused_with_the_servers_code_where_not()
    {
        var server = new PublishServer(new IssueSyncTests.FakeCde());
        var c = Issued(new HttpClient(server), _ => Server());
        SaveToRemote(c);
        // An editor below the project's issueTags rule (owners only): refused, nothing kept here.
        c.Token = "tok-ana";
        var refused = c.Call("remote.issueTag");
        Assert.Equal((false, "forbidden", 403), ((bool)refused["ok"]!, (string?)refused["code"], (int)refused["status"]!));
        Assert.False(server.Refs.ContainsKey("refs/tags/issue/1-P01"));

        c.Token = "tok-mihai";
        var r = c.Call("remote.issueTag");
        Assert.True((bool)r["ok"]!, r.ToJsonString());
        var id = server.Refs["refs/tags/issue/1-P01"];
        Assert.Equal((string?)r["id"], id);
        var tag = RemoteObjects.ReadTag(server.Objects[id], id);
        var record = CdeIssues.All(c.F.Model).Single();
        Assert.Equal(("issue", 1, "P01", (string)c.Call("history.status")["head"]!), (tag.Kind, tag.Number, tag.Revision, tag.Commit));
        var json = JsonNode.Parse(server.Objects[id])!;
        Assert.Equal(("S3", "Shared", record.Id, "For coordination"), ((string?)json["status"], (string?)json["container"], (string?)json["issue"], (string?)json["note"]));
        Assert.Equal(record.Files.Where(x => x.Stored).Select(x => x.Sha256).Distinct().Order(), tag.Files.Select(x => x.Sha256).Order());
        Assert.All(tag.Files, x => Assert.True(server.Objects.ContainsKey(x.Sha256)));
        // The tag is the project's too, and never changes.
        Assert.Contains("refs/tags/issue/1-P01", (c.Call("history.status")["commit"]!["tags"] as JsonArray)!.Select(x => (string?)x));
        Assert.Contains("never changes", Assert.Throws<InvalidOperationException>(() => c.Call("remote.issueTag", new JsonObject { ["note"] = "other" })).Message);
    }

    [Fact]
    public void A_folder_remote_takes_the_issue_tag_directly()
    {
        var folder = Path.Combine(_dir, "remote");
        var c = Issued(null, _ => new JsonObject { ["kind"] = "folder", ["url"] = FolderRemote.UrlOf(folder), ["projectName"] = "Bloc P+4" });
        SaveToRemote(c);
        var r = c.Call("remote.issueTag");
        Assert.True((bool)r["ok"]!, r.ToJsonString());
        var project = (string)c.Call("history.status")["remotes"]![0]!["project"]!;
        using var remote = FolderRemote.Open(folder, project, new RemoteAuthor("local:b2b2b2b2b2b2b2b2", "Ana"), () => DateTime.UtcNow);
        Assert.Contains(remote.Refs(null).Refs, x => x.Name == "refs/tags/issue/1-P01" && x.Target == (string?)r["id"]);
    }

    [Fact]
    public void The_desktop_reads_an_issued_file_from_the_server_through_the_engine_and_nothing_else_of_lapin_remote()
    {
        var server = new PublishServer(new IssueSyncTests.FakeCde());
        var c = Issued(new HttpClient(server), _ => Server());
        SaveToRemote(c);
        Assert.True((bool)c.Call("remote.issueTag")["ok"]!);
        var file = CdeIssues.All(c.F.Model).Single().Files.First(x => x.Stored);
        var r = c.Call("remote.atrium", new JsonObject { ["path"] = $"lapin-remote/1.0/projects/{CdeProject}/objects/{file.Sha256}", ["bytes"] = true });
        Assert.Equal(200, (int)r["status"]!);
        Assert.Equal(file.Sha256, ObjectIds.Of(Convert.FromBase64String((string)r["data"]!)));
        var to = Path.Combine(_dir, "copy.pdf");
        Assert.Equal(to, (string?)c.Call("remote.atrium", new JsonObject { ["path"] = $"lapin-remote/1.0/projects/{CdeProject}/objects/{file.Sha256}", ["bytes"] = true, ["saveTo"] = to })["saved"]);
        foreach (var bad in new[] { $"lapin-remote/1.0/projects/{CdeProject}/refs", $"lapin-remote/1.0/projects/{CdeProject}/objects/{file.Sha256}/x", $"lapin-remote/1.0/projects/{CdeProject}/objects/zz" })
            Assert.Throws<InvalidOperationException>(() => c.Call("remote.atrium", new JsonObject { ["path"] = bad, ["bytes"] = true }));
    }
}
