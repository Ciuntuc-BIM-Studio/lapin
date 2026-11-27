using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.ConvergeIfc;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The Converge IFC connector (docs/plan-converge-ifc.md): sign in, the projects and IFC files, a file sent as a new file or a new
/// version, a version fetched, against an in-memory server that answers as ifc-hub does (same routes, same fields, same refusals).
/// </summary>
public sealed class ConvergeIfcTests
{
    sealed class Container(string id, string project, string name)
    {
        public string Id = id, Project = project, Name = name, DocType = "ifc";
        public List<byte[]> Versions = [];
    }

    /// <summary>ifc-hub's API in memory: /api/v1/me, projects and their containers, versions, downloads.</summary>
    sealed class FakeHub : HttpMessageHandler
    {
        public const string Token = "ifch_good";
        public string[] Roles = ["author"];
        public readonly List<string> Calls = [];
        public readonly Dictionary<string, Container> Containers = [];
        /// <summary>When set, the bytes every download serves instead of the true ones (a damaged transfer).</summary>
        public byte[]? Damaged;

        public Container Add(string name, params byte[][] versions)
        {
            var c = new Container($"c{Containers.Count + 1}", "p1", name);
            c.Versions.AddRange(versions);
            Containers[c.Id] = c;
            return c;
        }

        static string Sha(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));

        static JsonObject Json(Container c) => new()
        {
            ["container_id"] = c.Id, ["project_id"] = c.Project, ["filename"] = c.Name, ["doc_type"] = c.DocType, ["status"] = "WIP",
            ["version_no"] = c.Versions.Count, ["revision_code"] = $"P01.{c.Versions.Count}", ["sha256"] = Sha(c.Versions[^1]),
            ["size_bytes"] = c.Versions[^1].Length, ["updated_at"] = "2026-11-25T10:00:00+00:00",
        };

        static HttpResponseMessage Reply(HttpStatusCode code, JsonNode body) =>
            new(code) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

        static HttpResponseMessage Refuse(HttpStatusCode code, string message) => Reply(code, new JsonObject { ["message"] = message, ["detail"] = message });

        static byte[] Upload(HttpRequestMessage request)
        {
            var form = (MultipartFormDataContent)request.Content!;
            foreach (var part in form)
                if (part.Headers.ContentDisposition?.Name?.Trim('"') == "file") return part.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            throw new InvalidOperationException("no file part");
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(Handle(request));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken ct) => Handle(request);

        HttpResponseMessage Handle(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            Calls.Add($"{request.Method} {path}");
            if (request.Headers.Authorization?.Parameter != Token) return Refuse(HttpStatusCode.Unauthorized, "Not signed in");
            if (request.Method == HttpMethod.Get && path == "/api/v1/me")
                return Reply(HttpStatusCode.OK, new JsonObject
                {
                    ["email"] = "ana@firm.ro", ["name"] = "Ana",
                    ["projects"] = new JsonArray(new JsonObject { ["project_id"] = "p1", ["code"] = "HALL", ["name"] = "Hall", ["roles"] = new JsonArray(Roles.Select(r => (JsonNode)r).ToArray()) }),
                });
            var m = Regex.Match(path, @"^/api/v1/projects/(?<p>[^/]+)/containers$");
            if (m.Success)
            {
                if (request.Method == HttpMethod.Get)
                    return Reply(HttpStatusCode.OK, new JsonArray(Containers.Values.Where(c => c.Project == m.Groups["p"].Value).Select(c => (JsonNode)Json(c)).ToArray()));
                if (!Roles.Any(r => r is "author" or "reviewer" or "approver")) return Refuse(HttpStatusCode.Forbidden, "Requires the author, reviewer or approver role on this project");
                var bytes = Upload(request);
                var name = request.Content is MultipartFormDataContent f ? f.First().Headers.ContentDisposition!.FileName!.Trim('"') : "";
                if (Containers.Values.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                    return Refuse(HttpStatusCode.Conflict, "A file with this name already exists in the project; upload a new version of it instead");
                var c = Add(name, bytes);
                return Reply(HttpStatusCode.Created, Json(c));
            }
            m = Regex.Match(path, @"^/api/v1/containers/(?<c>[^/]+)(?<rest>/.*)?$");
            if (!m.Success || !Containers.TryGetValue(m.Groups["c"].Value, out var box)) return Refuse(HttpStatusCode.NotFound, "Container not found");
            var rest = m.Groups["rest"].Value;
            if (rest == "" && request.Method == HttpMethod.Get) return Reply(HttpStatusCode.OK, Json(box));
            if (rest == "/versions" && request.Method == HttpMethod.Get)
                return Reply(HttpStatusCode.OK, new JsonArray(Enumerable.Range(1, box.Versions.Count).Reverse().Select(n => (JsonNode)new JsonObject
                {
                    ["version_no"] = n, ["size_bytes"] = box.Versions[n - 1].Length, ["sha256"] = Sha(box.Versions[n - 1]), ["revision_code"] = $"P01.{n}",
                    ["uploaded_at"] = "2026-11-25T10:00:00+00:00", ["uploaded_by"] = "ana@firm.ro", ["is_current"] = n == box.Versions.Count,
                }).ToArray()));
            if (rest == "/versions" && request.Method == HttpMethod.Post)
            {
                if (!Roles.Any(r => r is "author" or "reviewer" or "approver")) return Refuse(HttpStatusCode.Forbidden, "Requires the author, reviewer or approver role on this project");
                var bytes = Upload(request);
                if (Sha(bytes) == Sha(box.Versions[^1])) return Refuse(HttpStatusCode.Conflict, "This file is identical to the current version");
                box.Versions.Add(bytes);
                return Reply(HttpStatusCode.Created, Json(box));
            }
            var v = Regex.Match(rest, @"^/versions/(?<n>\d+)/file$");
            if (v.Success && request.Method == HttpMethod.Get)
            {
                var n = int.Parse(v.Groups["n"].Value);
                if (n < 1 || n > box.Versions.Count) return Refuse(HttpStatusCode.NotFound, "Not found");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Damaged ?? box.Versions[n - 1]) };
            }
            return Refuse(HttpStatusCode.NotFound, "Not found");
        }
    }

    static (Fixture F, FakeHub Hub) Signed(Action<FakeHub>? arrange = null)
    {
        var hub = new FakeHub();
        arrange?.Invoke(hub);
        var f = new Fixture(new ConvergeIfcModule(hub));
        f.Session.Query("convergeifc.setAccount", new JsonObject { ["server"] = "https://cde.firm.ro", ["token"] = FakeHub.Token });
        return (f, hub);
    }

    static string Made(Fixture f, string name, string text)
    {
        var path = (string)f.Session.Query("convergeifc.tempPath", new JsonObject { ["name"] = name })!["path"]!;
        File.WriteAllText(path, text);
        return path;
    }

    static JsonNode Send(Fixture f, string path, string? name = null, string? containerId = null, bool discard = true)
    {
        var a = new JsonObject { ["projectId"] = "p1", ["path"] = path, ["discard"] = discard };
        if (name != null) a["fileName"] = name;
        if (containerId != null) a["containerId"] = containerId;
        return f.Session.Query("convergeifc.send", a)!;
    }

    [Fact]
    public void A_server_is_an_address_over_https_and_its_calls_are_the_hosts_alone()
    {
        Assert.Equal("https://cde.firm.ro", ConvergeServer.ServerOf(" cde.firm.ro/ "));
        Assert.Equal("https://cde.firm.ro", ConvergeServer.ServerOf("https://cde.firm.ro/api/v1"));
        Assert.Equal("http://127.0.0.1:8090", ConvergeServer.ServerOf("http://127.0.0.1:8090/x"));
        Assert.Throws<CommandException>(() => ConvergeServer.ServerOf(""));
        Assert.Throws<CommandException>(() => ConvergeServer.ServerOf("ftp://x"));
        Assert.Throws<CommandException>(() => ConvergeServer.ServerOf("http://cde.firm.ro"));
        foreach (var q in new[] { "convergeifc.send", "convergeifc.fetch", "convergeifc.setAccount", "convergeifc.projects", "convergeifc.tempPath", "convergeifc.discard" })
            foreach (var role in new[] { "editor", "owner", "viewer" })
                Assert.NotNull(EngineDispatcher.AllowedRemote("doc.query", new JsonObject { ["name"] = q }, role));
    }

    [Fact]
    public void Nothing_is_asked_of_the_server_before_signing_in()
    {
        var hub = new FakeHub();
        var f = new Fixture(new ConvergeIfcModule(hub));
        Assert.Empty(f.Session.Query("convergeifc.accounts", [])!.AsArray());
        Assert.Throws<CommandException>(() => f.Session.Query("convergeifc.projects", []));
        Assert.Throws<CommandException>(() => f.Session.Query("convergeifc.files", new JsonObject { ["projectId"] = "p1" }));
        Assert.Empty(hub.Calls);
    }

    [Fact]
    public void A_wrong_token_is_refused_and_the_right_one_is_never_told_back()
    {
        var hub = new FakeHub();
        var f = new Fixture(new ConvergeIfcModule(hub));
        var bad = Assert.Throws<CommandException>(() => f.Session.Query("convergeifc.setAccount", new JsonObject { ["server"] = "https://cde.firm.ro", ["token"] = "ifch_wrong" }));
        Assert.Contains("token", bad.Message);
        Assert.Empty(f.Session.Query("convergeifc.accounts", [])!.AsArray());
        var account = f.Session.Query("convergeifc.setAccount", new JsonObject { ["server"] = "cde.firm.ro", ["token"] = $" {FakeHub.Token} " })!;
        Assert.Equal("https://cde.firm.ro", (string?)account["server"]);
        Assert.Equal("Ana", (string?)account["name"]);
        Assert.DoesNotContain(FakeHub.Token, f.Session.Query("convergeifc.accounts", [])!.ToJsonString());
        f.Session.Query("convergeifc.forgetAccount", []);
        Assert.Empty(f.Session.Query("convergeifc.accounts", [])!.AsArray());
        Assert.Throws<CommandException>(() => f.Session.Query("convergeifc.projects", []));
    }

    [Fact]
    public void The_projects_say_whether_the_account_may_send_and_the_files_are_the_ifc_ones()
    {
        var (f, hub) = Signed(h => h.Roles = ["reviewer"]);
        hub.Add("b.ifc", "b1"u8.ToArray());
        hub.Add("A.ifc", "a1"u8.ToArray()).DocType = "ifc";
        var doc = hub.Add("notes.pdf", "pdf"u8.ToArray());
        doc.DocType = "document";
        var project = f.Session.Query("convergeifc.projects", [])!.AsArray().Single()!;
        Assert.Equal("HALL", (string?)project["code"]);
        Assert.True((bool)project["canUpload"]!);
        var files = f.Session.Query("convergeifc.files", new JsonObject { ["projectId"] = "p1" })!.AsArray();
        Assert.Equal(["A.ifc", "b.ifc"], files.Select(x => (string)x!["fileName"]!));
        Assert.Equal("P01.1", (string?)files[0]!["revisionCode"]);
    }

    [Fact]
    public void A_reader_of_a_project_cannot_send_and_is_told_why()
    {
        var (f, _) = Signed(h => h.Roles = ["information_manager"]);
        Assert.False((bool)f.Session.Query("convergeifc.projects", [])!.AsArray().Single()!["canUpload"]!);
        var path = Made(f, "Hall.ifc", "ISO-10303-21;");
        var e = Assert.Throws<CommandException>(() => Send(f, path, "Hall.ifc", discard: false));
        Assert.Contains("author", e.Message);
        Assert.True(File.Exists(path));
        ConvergeServer.Discard(path);
    }

    [Fact]
    public void A_new_file_is_made_then_each_change_is_a_new_version_and_the_same_bytes_send_nothing()
    {
        var (f, hub) = Signed();
        var first = Made(f, "Hall.ifc", "model one");
        var made = Send(f, first, "Hall.ifc");
        Assert.True((bool)made["created"]!);
        Assert.Equal("P01.1", (string?)made["revisionCode"]);
        Assert.False(File.Exists(first));
        var id = (string)made["id"]!;

        var second = Made(f, "Hall.ifc", "model two");
        var revised = Send(f, second, containerId: id);
        Assert.False((bool)revised["created"]!);
        Assert.Equal(2, (int)revised["versionNo"]!);
        Assert.Equal("P01.2", (string?)revised["revisionCode"]);

        hub.Calls.Clear();
        var again = Send(f, Made(f, "Hall.ifc", "model two"), containerId: id);
        Assert.True((bool)again["unchanged"]!);
        Assert.Equal(2, (int)again["versionNo"]!);
        Assert.DoesNotContain(hub.Calls, c => c.StartsWith("POST"));
        Assert.Equal(2, hub.Containers[id].Versions.Count);
    }

    [Fact]
    public void A_name_the_project_already_holds_is_refused_as_a_new_file_with_the_servers_words()
    {
        var (f, hub) = Signed();
        hub.Add("Hall.ifc", "x"u8.ToArray());
        var path = Made(f, "Hall.ifc", "model");
        var e = Assert.Throws<CommandException>(() => Send(f, path, "hall.IFC", discard: false));
        Assert.Contains("already exists", e.Message);
        Assert.Single(hub.Containers);
        ConvergeServer.Discard(path);
    }

    [Fact]
    public void A_send_needs_a_name_or_a_file_to_add_a_version_to()
    {
        var (f, _) = Signed();
        var path = Made(f, "Hall.ifc", "model");
        Assert.Throws<CommandException>(() => Send(f, path, discard: false));
        Assert.Throws<CommandException>(() => Send(f, Path.Combine(Path.GetTempPath(), "no-such-file.ifc"), "x.ifc"));
        ConvergeServer.Discard(path);
    }

    [Fact]
    public void A_version_is_fetched_whole_with_its_checksum_checked_and_cleared_away_after()
    {
        var (f, hub) = Signed();
        var c = hub.Add("Hall.ifc", "old model"u8.ToArray(), "new model"u8.ToArray());
        var versions = f.Session.Query("convergeifc.versions", new JsonObject { ["containerId"] = c.Id })!.AsArray();
        Assert.Equal([2, 1], versions.Select(v => (int)v!["versionNo"]!));
        Assert.True((bool)versions[0]!["isCurrent"]!);

        var current = f.Session.Query("convergeifc.fetch", new JsonObject { ["containerId"] = c.Id })!;
        Assert.Equal("Hall.ifc", (string?)current["fileName"]);
        Assert.Equal(2, (int)current["versionNo"]!);
        Assert.Equal("new model", File.ReadAllText((string)current["path"]!));
        var older = f.Session.Query("convergeifc.fetch", new JsonObject { ["containerId"] = c.Id, ["versionNo"] = 1 })!;
        Assert.Equal("old model", File.ReadAllText((string)older["path"]!));

        foreach (var got in new[] { current, older })
        {
            var path = (string)got["path"]!;
            Assert.True((bool)f.Session.Query("convergeifc.discard", new JsonObject { ["path"] = path })!["discarded"]!);
            Assert.False(File.Exists(path));
            Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
        }
        Assert.Throws<CommandException>(() => f.Session.Query("convergeifc.fetch", new JsonObject { ["containerId"] = c.Id, ["versionNo"] = 9 }));
    }

    [Fact]
    public void A_damaged_download_is_refused_and_nothing_is_left_on_disk()
    {
        var (f, hub) = Signed();
        var c = hub.Add("Hall.ifc", "the model"u8.ToArray());
        hub.Damaged = "the mo"u8.ToArray();
        var before = Directory.Exists(ConvergeServer.TempRoot) ? Directory.GetDirectories(ConvergeServer.TempRoot).Length : 0;
        var e = Assert.Throws<CommandException>(() => f.Session.Query("convergeifc.fetch", new JsonObject { ["containerId"] = c.Id }));
        Assert.Contains("damaged", e.Message);
        Assert.Equal(before, Directory.Exists(ConvergeServer.TempRoot) ? Directory.GetDirectories(ConvergeServer.TempRoot).Length : 0);
    }

    [Fact]
    public void A_version_saved_to_the_users_place_replaces_it_only_once_it_has_arrived_whole()
    {
        var (f, hub) = Signed();
        var c = hub.Add("Hall.ifc", "old model"u8.ToArray(), "new model"u8.ToArray());
        var place = Path.Combine(Path.GetTempPath(), $"lapin-converge-test-{Guid.NewGuid():N}.ifc");
        try
        {
            var got = f.Session.Query("convergeifc.fetch", new JsonObject { ["containerId"] = c.Id, ["versionNo"] = 1, ["path"] = place })!;
            Assert.Equal(place, (string?)got["path"]);
            Assert.Equal("old model", File.ReadAllText(place));

            hub.Damaged = "torn"u8.ToArray();
            Assert.Throws<CommandException>(() => f.Session.Query("convergeifc.fetch", new JsonObject { ["containerId"] = c.Id, ["path"] = place }));
            Assert.Equal("old model", File.ReadAllText(place));

            hub.Damaged = null;
            f.Session.Query("convergeifc.fetch", new JsonObject { ["containerId"] = c.Id, ["path"] = place });
            Assert.Equal("new model", File.ReadAllText(place));
            // The user's file is not the connector's to remove.
            Assert.False((bool)f.Session.Query("convergeifc.discard", new JsonObject { ["path"] = place })!["discarded"]!);
            Assert.True(File.Exists(place));
        }
        finally { File.Delete(place); }
    }

    static byte[] Zip(params (string Name, string Text)[] entries)
    {
        var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, text) in entries)
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open());
                w.Write(text);
            }
        return bytes.ToArray();
    }

    [Fact]
    public void An_ifczip_is_fetched_as_the_ifc_inside_it_whatever_its_entrys_name_says()
    {
        var (f, hub) = Signed();
        var c = hub.Add("Hall.ifczip", Zip(("readme.txt", "no"), ("../../escape.ifc", "the model inside")));
        var got = f.Session.Query("convergeifc.fetch", new JsonObject { ["containerId"] = c.Id })!;
        var path = (string)got["path"]!;
        Assert.Equal("Hall.ifc", Path.GetFileName(path));
        Assert.Equal("the model inside", File.ReadAllText(path));
        Assert.Equal([path], Directory.GetFiles(Path.GetDirectoryName(path)!));
        Assert.True(ConvergeServer.Discard(path));

        var empty = hub.Add("Empty.ifczip", Zip(("readme.txt", "no")));
        Assert.Contains("no .ifc", Assert.Throws<CommandException>(() => f.Session.Query("convergeifc.fetch", new JsonObject { ["containerId"] = empty.Id })).Message);
        var broken = hub.Add("Broken.ifczip", "not a zip"u8.ToArray());
        Assert.Contains("damaged", Assert.Throws<CommandException>(() => f.Session.Query("convergeifc.fetch", new JsonObject { ["containerId"] = broken.Id })).Message);
    }

    [Fact]
    public void Only_files_the_connector_made_are_ever_removed()
    {
        var (f, _) = Signed();
        var other = Path.Combine(Path.GetTempPath(), $"lapin-converge-test-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(other, "keep me");
        try
        {
            Assert.False(ConvergeServer.Discard(other));
            Assert.False((bool)f.Session.Query("convergeifc.discard", new JsonObject { ["path"] = other })!["discarded"]!);
            Assert.False(ConvergeServer.Discard(Path.Combine(ConvergeServer.TempRoot, "..", Path.GetFileName(other))));
            Assert.False(ConvergeServer.Discard(null));
            Assert.True(File.Exists(other));
            // Sent with `discard`, a file of another place stays too.
            Send(f, other, "Keep.ifc", discard: true);
            Assert.True(File.Exists(other));
        }
        finally { File.Delete(other); }
    }

    [Fact]
    public void A_server_that_is_not_one_is_told_so()
    {
        var f = new Fixture(new ConvergeIfcModule(new NotAServer()));
        var e = Assert.Throws<CommandException>(() => f.Session.Query("convergeifc.setAccount", new JsonObject { ["server"] = "https://example.com", ["token"] = "x" }));
        Assert.Contains("address", e.Message);
    }

    sealed class NotAServer : HttpMessageHandler
    {
        static HttpResponseMessage Page() => new(HttpStatusCode.OK) { Content = new StringContent("<html>hello</html>", Encoding.UTF8, "text/html") };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(Page());
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken ct) => Page();
    }

    /// <summary>
    /// A real server: signed in, a file sent as a new file then as a new version, fetched back and compared. Runs only when
    /// LAPIN_CONVERGE_TEST_URL, LAPIN_CONVERGE_TEST_TOKEN and LAPIN_CONVERGE_TEST_PROJECT (a project the token may upload to) are set (never in CI).
    /// </summary>
    [Fact]
    public void Sent_to_a_real_server_the_versions_come_back_the_same()
    {
        var url = Environment.GetEnvironmentVariable("LAPIN_CONVERGE_TEST_URL");
        var token = Environment.GetEnvironmentVariable("LAPIN_CONVERGE_TEST_TOKEN");
        var project = Environment.GetEnvironmentVariable("LAPIN_CONVERGE_TEST_PROJECT");
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(project)) return;
        var f = new Fixture(new ConvergeIfcModule());
        f.Session.Query("convergeifc.setAccount", new JsonObject { ["server"] = url, ["token"] = token });
        var name = $"lapin-test-{DateTime.UtcNow:yyyyMMdd-HHmmss}.ifc";
        var one = Made(f, name, "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=IFCPROJECT('1',$,$,$,$,$,$,$,$);\nENDSEC;\nEND-ISO-10303-21;\n");
        var made = f.Session.Query("convergeifc.send", new JsonObject { ["projectId"] = project, ["path"] = one, ["fileName"] = name, ["discard"] = true })!;
        Assert.True((bool)made["created"]!);
        var two = Made(f, name, "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=IFCPROJECT('2',$,$,$,$,$,$,$,$);\nENDSEC;\nEND-ISO-10303-21;\n");
        var revised = f.Session.Query("convergeifc.send", new JsonObject { ["projectId"] = project, ["path"] = two, ["containerId"] = (string)made["id"]!, ["discard"] = true })!;
        Assert.Equal(2, (int)revised["versionNo"]!);
        var back = f.Session.Query("convergeifc.fetch", new JsonObject { ["containerId"] = (string)made["id"]!, ["versionNo"] = 1 })!;
        Assert.Contains("'1'", File.ReadAllText((string)back["path"]!));
        f.Session.Query("convergeifc.discard", new JsonObject { ["path"] = (string)back["path"]! });
    }
}
