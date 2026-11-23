using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Package.Remote;

namespace Lapin.Engine.Tests;

/// <summary>
/// The conformance vectors of lapin-remote 1.0 (docs/spec-lapin-remote/vectors, spec §16) run by Lapin's client: the ids, the JCS
/// cases, every object parsed and re-canonicalised to its bytes, the folder remote's fork detection and layout, and the sessions'
/// requests sent to a fake server that answers with the vectors' responses (upload only what <c>missing</c> lists; stop at 409;
/// resume an upload from HEAD's offset).
/// </summary>
public sealed class RemoteVectorTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-remote-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    internal static string Vectors
    {
        get
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            {
                var v = Path.Combine(d.FullName, "docs", "spec-lapin-remote", "vectors");
                if (Directory.Exists(v)) return v;
            }
            throw new DirectoryNotFoundException("docs/spec-lapin-remote/vectors");
        }
    }

    internal static JsonObject Ids => (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(Vectors, "ids.json")))!;
    internal static string Id(string name) => (string)(Ids["objects"]![name] ?? Ids["blobs"]![name])!["id"]!;
    internal static byte[] Bytes(string name) => Ids["objects"]![name] is { } o ? File.ReadAllBytes(Path.Combine(Vectors, (string)o["file"]!)) : File.ReadAllBytes(Path.Combine(Vectors, "blobs", name));
    internal static string Project => (string)Ids["project"]!;

    [Fact]
    public void Every_blob_and_object_hashes_to_its_id()
    {
        var ids = Ids;
        foreach (var (name, v) in (JsonObject)ids["blobs"]!)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Vectors, "blobs", name));
            Assert.Equal((string)v!["id"]!, ObjectIds.Of(bytes));
            Assert.Equal((long)v["size"]!, bytes.LongLength);
        }
        foreach (var (name, v) in (JsonObject)ids["objects"]!)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Vectors, (string)v!["file"]!));
            Assert.Equal((string)v["id"]!, ObjectIds.Of(bytes));
            Assert.Equal((long)v["size"]!, bytes.LongLength);
        }
        Assert.Equal(ObjectIds.Empty, ObjectIds.Of([]));
        Assert.False(ObjectIds.Valid(ObjectIds.Empty.ToUpperInvariant()));
        Assert.False(ObjectIds.Valid(ObjectIds.Empty[..63]));
    }

    [Theory]
    [InlineData("keys-order")]
    [InlineData("nested")]
    [InlineData("numbers")]
    [InlineData("strings")]
    public void Canonical_form_is_rfc_8785(string name)
    {
        var input = File.ReadAllBytes(Path.Combine(Vectors, "canonical", name + ".input.json"));
        var expected = File.ReadAllBytes(Path.Combine(Vectors, "canonical", name + ".expected.json"));
        Assert.Equal(Encoding.UTF8.GetString(expected), Encoding.UTF8.GetString(Jcs.Canonicalize(input)));
        Assert.True(Jcs.IsCanonical(expected));
        Assert.False(Jcs.IsCanonical(input));
    }

    [Fact]
    public void Json_that_is_not_an_object_of_the_protocol_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => Jcs.Canonicalize("{\"a\":1,\"a\":2}"u8));
        Assert.Throws<InvalidDataException>(() => Jcs.Canonicalize("\"\\ud800\""u8));
        Assert.False(Jcs.IsCanonical("{\"b\":1,\"a\":2}"u8));
        Assert.False(Jcs.IsCanonical("{\"a\": 2}"u8));
        Assert.Equal("1e+21", Jcs.Number(1e21));
        Assert.Equal("1e-7", Jcs.Number(1e-7));
        Assert.Equal("0.000001", Jcs.Number(1e-6));
        Assert.Equal("123456789012345680", Jcs.Number(123456789012345678d));
        Assert.Equal("-1500", Jcs.Number(-1.5e3));
        Assert.Equal("5e-324", Jcs.Number(double.Epsilon));
    }

    [Fact]
    public void Every_object_parses_and_canonicalises_to_its_own_bytes_unknown_members_kept()
    {
        foreach (var (name, v) in (JsonObject)Ids["objects"]!)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Vectors, (string)v!["file"]!));
            Assert.Equal(bytes, Jcs.Canonicalize(bytes));
            var id = (string)v["id"]!;
            switch ((string)v["kind"]!)
            {
                case "tree":
                    var m = RemoteObjects.ReadTree(bytes);
                    // A tree made again from its manifest is the same bytes: the tree is the manifest without foreign, canonical.
                    Assert.Equal(bytes, RemoteObjects.Tree(m));
                    break;
                case "commit":
                    var c = RemoteObjects.ReadCommit(bytes, id);
                    Assert.Equal(1, c.V);
                    break;
                case "tag":
                    var t = RemoteObjects.ReadTag(bytes, id);
                    Assert.True(t.Number >= 1);
                    break;
            }
            Assert.Equal((string)v["kind"]!, RemoteObjects.KindOf(bytes)!.Value.ToString().ToLowerInvariant());
            _ = name;
        }
        // commit-future: its two unknown members are part of the bytes, hence of the id; kept by keeping the bytes.
        var future = Bytes("commit-future");
        var h = PackageHistory.Empty();
        h.Add(Id("commit-future"), future, ObjectKind.Commit);
        Assert.Equal(future, h.CommitBytes(Id("commit-future")));
        var parsed = (JsonObject)JsonNode.Parse(future)!;
        Assert.True(parsed.Count > 10, "commit-future carries members a 1.0 reader does not know");
    }

    [Fact]
    public void A_tree_is_the_manifest_without_foreign_and_its_paths_are_checked()
    {
        var m = RemoteObjects.ReadTree(Bytes("T2"));
        var withForeign = m with { Foreign = [new Lapin.Package.PackagePart("dag/x.json", ObjectIds.Empty, 0)] };
        Assert.Equal(Bytes("T2"), RemoteObjects.Tree(withForeign));
        Assert.False(RemoteObjects.TreePath("history/x"));
        Assert.False(RemoteObjects.TreePath("Versions/x"));
        Assert.False(RemoteObjects.TreePath("a//b"));
        Assert.False(RemoteObjects.TreePath("../a"));
        Assert.False(RemoteObjects.TreePath("a\\b"));
        Assert.True(RemoteObjects.TreePath("model/core/IfcWall/_.arrow"));
    }

    [Fact]
    public void Ref_names_follow_the_grammar()
    {
        Assert.True(RefNames.Valid("refs/heads/main"));
        Assert.True(RefNames.Valid("refs/heads/wip/ana-popescu/2026-11-23-1046"));
        Assert.True(RefNames.Valid("refs/tags/v/3"));
        Assert.True(RefNames.Valid("refs/tags/issue/1-P01"));
        Assert.False(RefNames.Valid("refs/heads/feature"));
        Assert.False(RefNames.Valid("refs/heads/wip/Ana/x"));
        Assert.False(RefNames.Valid("refs/tags/v/0"));
        Assert.False(RefNames.Valid("refs/tags/issue/1-X01"));
        Assert.Equal("ana-popescu", RefNames.Slug("Ana Popescu"));
        Assert.Equal("ionut-ciuntuc", RefNames.Slug("Ionuț Ciuntuc"));
        Assert.Equal("refs/heads/wip/ana-popescu/2026-11-23-1046-2", RefNames.Wip("Ana Popescu", new DateTime(2026, 11, 23, 10, 46, 0, DateTimeKind.Utc), n => n.EndsWith("1046", StringComparison.Ordinal)));
    }

    // ------------------------------------------------------------ folder

    [Fact]
    public void Folder_fork_detection_resolves_as_the_vector_says()
    {
        var v = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(Vectors, "folder", "fork-detection.json")))!;
        var notes = new List<string>();
        var entries = ((JsonArray)v["entries"]!).Select(e => FolderRemote.ParseEntry((string)e!["file"]!, Encoding.UTF8.GetBytes((string)e["content"]!), notes)!).ToList();
        Assert.Empty(notes);
        // The contents are canonical, as a folder remote writes them.
        foreach (var e in (JsonArray)v["entries"]!) Assert.True(Jcs.IsCanonical(Encoding.UTF8.GetBytes((string)e!["content"]!)));
        var state = FolderRemote.Resolve(entries);
        var expected = (JsonObject)v["expected"]!;
        Assert.Equal((string)expected["head"]!, state.Value);
        Assert.Equal((int)expected["seq"]!, state.Seq);
        Assert.Equal(((JsonArray)expected["chain"]!).Select(x => (string)x!), state.Chain.Select(c => c.File));
        var detached = (JsonArray)expected["detached"]!;
        Assert.Equal(detached.Select(d => (string)d!["file"]!), state.Detached.Select(d => d.File));
        Assert.Equal(detached.Select(d => (string)d!["new"]!), state.Detached.Select(d => d.New));
        Assert.Equal(detached.Select(d => (string)d!["author"]!["id"]!), state.Detached.Select(d => d.Author.Id));
        // What stays reachable: every entry's new, the detached included.
        Assert.Equal(((JsonArray)expected["reachable"]!).Select(x => (string)x!).Order(), entries.Select(e => e.New!).Distinct().Order());
        // An entry written by hand is the same bytes as the vector's.
        var first = entries[0];
        Assert.Equal((string)((JsonArray)v["entries"]!)[0]!["content"]!, Encoding.UTF8.GetString(FolderRemote.EntryBytes(first.Name, first.Seq, first.Old, first.New, first.Author, first.At.UtcDateTime)));
        Assert.Equal(first.File["refs/heads/main/".Length..], FolderRemote.EntryName(first.Seq, first.New, first.Author));
    }

    /// <summary>The story's objects in a history: every blob in memory, every tree, commit and tag held.</summary>
    internal static PackageHistory Story(params string[] names)
    {
        var h = PackageHistory.Empty();
        foreach (var n in names)
        {
            var bytes = Bytes(n);
            var kind = Ids["objects"]![n] is { } o ? Enum.Parse<ObjectKind>((string)o["kind"]!, ignoreCase: true) : ObjectKind.Blob;
            h.Add(Id(n), bytes, kind);
        }
        return h;
    }

    static readonly RemoteAuthor Mihai = new("local:9f2b6c1d0e8a4b7c", "Mihai Ionescu");

    [Fact]
    public void Folder_layout_after_the_push_session_is_the_vectors()
    {
        var v = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(Vectors, "folder", "layout.json")))!;
        var folder = Path.Combine(_dir, "remote");
        var at = new DateTime(2026, 11, 23, 9, 59, 0, DateTimeKind.Utc);
        Assert.Equal(Project, FolderRemote.Create(folder, "Bloc P+4", at, Project));
        var h = Story("document.json", "relations.arrow", "T1", "c1", "walls.arrow", "hello.py", "T2", "c2", "issue.pdf", "tag-issue");
        var now = new DateTime(2026, 11, 23, 10, 0, 5, DateTimeKind.Utc);
        using (var remote = FolderRemote.Open(folder, Project, Mihai, () => now))
        {
            h.Refs[RefNames.Main] = Id("c1");
            Assert.True(RemoteOps.Push(remote, h, "origin", [(RefNames.Main, RefNames.Main)], now).Pushed);
            h.Refs[RefNames.Main] = Id("c2");
            h.Refs["refs/tags/issue/1-P01"] = Id("tag-issue");
            var second = RemoteOps.Push(remote, h, "origin", [(RefNames.Main, RefNames.Main), ("refs/tags/issue/1-P01", "refs/tags/issue/1-P01")], now);
            Assert.True(second.Pushed);
            // Only what the folder lacked: c2, T2, the two new parts, the tag and its file.
            Assert.Equal(6, second.Uploaded.Count);
            Assert.Equal(Id("c2"), remote.Refs(null).Refs.Single(r => r.Name == RefNames.Main).Target);
        }
        foreach (var (name, content) in (JsonObject)v["files"]!)
            Assert.Equal((string)content!, File.ReadAllText(Path.Combine(folder, name)));
        string[] Listed(string key) => ((JsonArray)v[key]!).Select(x => (string)x!).Order(StringComparer.Ordinal).ToArray();
        string[] OnDisk(string sub, string pattern) => Directory.EnumerateFiles(Path.Combine(folder, Project, sub), pattern, SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(folder, f).Replace(Path.DirectorySeparatorChar, '/')).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(Listed("objects"), OnDisk("objects", "*.gz"));
        Assert.Equal(Listed("commits"), OnDisk("commits", "*.json"));
        Assert.Equal(Listed("refs"), OnDisk("refs", "*.ref"));
        Assert.False(File.Exists(Path.Combine(folder, Project, "lock")));
        // A gzipped object reads back to its bytes.
        using (var remote = FolderRemote.Open(folder, Project, Mihai)) Assert.Equal(Bytes("walls.arrow"), remote.Get(Id("walls.arrow")));
    }

    [Fact]
    public void Folder_compare_and_swap_refuses_a_stale_old_and_the_loser_keeps_its_work_as_a_branch()
    {
        var folder = Path.Combine(_dir, "remote");
        FolderRemote.Create(folder, "Bloc P+4", DateTime.UtcNow, Project);
        var now = new DateTime(2026, 11, 23, 10, 46, 0, DateTimeKind.Utc);
        var mihai = Story("document.json", "relations.arrow", "T1", "c1", "walls.arrow", "hello.py", "T2", "c2");
        var ana = Story("document.json", "relations.arrow", "T1", "c1", "hello.py", "T3", "c3");
        var anaId = new RemoteAuthor("local:3a7d5e9f1b2c4d6e", "Ana Popescu");
        using (var r = FolderRemote.Open(folder, Project, Mihai, () => now))
        {
            mihai.Refs[RefNames.Main] = Id("c1");
            Assert.True(RemoteOps.Push(r, mihai, "origin", [(RefNames.Main, RefNames.Main)], now).Pushed);
        }
        using (var r = FolderRemote.Open(folder, Project, anaId, () => now))
        {
            ana.Refs[RefNames.Main] = Id("c1");
            RemoteOps.Fetch(r, ana, "origin", anaId, now);
        }
        using (var r = FolderRemote.Open(folder, Project, Mihai, () => now))
        {
            mihai.Refs[RefNames.Main] = Id("c2");
            Assert.True(RemoteOps.Push(r, mihai, "origin", [(RefNames.Main, RefNames.Main)], now).Pushed);
        }
        using (var r = FolderRemote.Open(folder, Project, anaId, () => now))
        {
            ana.Refs[RefNames.Main] = Id("c3");
            // Ana pushes without fetching (her last-seen main is c1): the compare-and-swap refuses, nothing moves.
            var refused = RemoteOps.Push(r, ana, "origin", [(RefNames.Main, RefNames.Main)], now, fetchRefs: false);
            Assert.False(refused.Pushed);
            Assert.Equal(Id("c2"), refused.Current);
            Assert.Equal(Id("c2"), r.Refs(null).Refs.Single(x => x.Name == RefNames.Main).Target);
            // Pushing with the refs fetched first stops before uploading: the remote moved, pull first.
            var behind = RemoteOps.Push(r, ana, "origin", [(RefNames.Main, RefNames.Main)], now);
            Assert.True(behind.Behind);
        }
        // A synced folder let both entries in (the lock is not atomic between computers): the earlier wins, the later is detached,
        // and its author, at the next fetch, keeps it as a wip branch, once.
        var dir = Path.Combine(folder, Project, "refs", "heads", "main");
        var late = new DateTime(2026, 11, 23, 11, 0, 0, DateTimeKind.Utc);
        File.WriteAllBytes(Path.Combine(dir, FolderRemote.EntryName(2, Id("c3"), anaId)), FolderRemote.EntryBytes(RefNames.Main, 2, Id("c1"), Id("c3"), anaId, late));
        using (var r = FolderRemote.Open(folder, Project, anaId, () => now))
        {
            var f = RemoteOps.Fetch(r, ana, "origin", anaId, now);
            Assert.Contains(f.Notes, n => n.Contains("wip/ana-popescu/2026-11-23-1046", StringComparison.Ordinal));
            Assert.Equal(Id("c3"), r.Refs(null).Refs.Single(x => x.Name == "refs/heads/wip/ana-popescu/2026-11-23-1046").Target);
            Assert.Single(ana.Seen["origin"].SeenDetached);
            ana.Seen["origin"].Etag = null;
            var again = RemoteOps.Fetch(r, ana, "origin", anaId, now);
            Assert.DoesNotContain(again.Notes, n => n.Contains("wip/", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void A_stale_folder_lock_is_broken_and_said()
    {
        var folder = Path.Combine(_dir, "remote");
        FolderRemote.Create(folder, "P", DateTime.UtcNow, Project);
        File.WriteAllText(Path.Combine(folder, Project, "lock"), "{\"at\":\"2026-11-23T09:00:00Z\",\"by\":\"local:x\",\"host\":\"mac\",\"name\":\"Ana\"}");
        var now = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
        var h = Story("document.json", "relations.arrow", "T1", "c1");
        h.Refs[RefNames.Main] = Id("c1");
        using var r = FolderRemote.Open(folder, Project, Mihai, () => now);
        var o = RemoteOps.Push(r, h, "origin", [(RefNames.Main, RefNames.Main)], now);
        Assert.True(o.Pushed);
        Assert.Contains(o.Notes, n => n.Contains("older than 10 minutes", StringComparison.Ordinal) && n.Contains("Ana", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------ sessions against a fake server

    [Fact]
    public void Push_session_sends_what_the_vectors_say()
    {
        var server = VectorServer.Of("push");
        // Step 7 (a PUT of an object the server holds) is the server's case: a client uploads only what missing lists.
        server.MayStayUnused(7);
        server.BreakAfter = (req, n) => req.Method == HttpMethod.Patch && n == 0;
        var h = Story("document.json", "relations.arrow", "T1", "c1", "walls.arrow", "hello.py", "T2", "c2", "issue.pdf", "tag-issue");
        h.Refs[RefNames.Main] = Id("c2");
        h.Refs["refs/tags/issue/1-P01"] = Id("tag-issue");
        h.Seen["origin"] = new RemoteSeen { Etag = "\"r-7\"" };
        h.Seen["origin"].Refs[RefNames.Main] = Id("c1");
        using var http = new HttpClient(server);
        var remote = new HttpRemote(http, "http://cde.test", "/lapin-remote/1.0", Project, new RemoteTokens { Access = "test-access-token-mihai" })
        {
            UseUpload = (id, _) => id == Id("walls.arrow"),
            FirstPatchLimit = 100,
        };
        Assert.Equal("1.0", remote.Capabilities().Version);
        Assert.Equal("owner", remote.Project().Role);
        var o = RemoteOps.Push(remote, h, "origin", [(RefNames.Main, RefNames.Main), ("refs/tags/issue/1-P01", "refs/tags/issue/1-P01")], DateTime.UtcNow);
        Assert.True(o.Pushed);
        server.AssertDone();
        Assert.Equal([Id("c2"), Id("T2"), Id("walls.arrow"), Id("hello.py"), Id("tag-issue")], o.Uploaded);
        Assert.DoesNotContain(server.Requests, r => r.Path.EndsWith(Id("issue.pdf"), StringComparison.Ordinal));
        Assert.Equal(Id("c2"), h.Seen["origin"].Refs[RefNames.Main]);
        Assert.Equal(Id("tag-issue"), h.Seen["origin"].Refs["refs/tags/issue/1-P01"]);
        Assert.Equal("\"r-8\"", h.Seen["origin"].Etag);
    }

    [Fact]
    public void Fetch_session_takes_what_the_vectors_say()
    {
        var server = VectorServer.Of("fetch");
        server.Add(VectorServer.Load("push", 1));
        server.MayStayUnused(100);
        // The vector's one pack is the union of the client's two (the tag and the tree first: the tag names its files).
        server.PackSubsets = true;
        var h = Story("document.json", "relations.arrow", "T1", "c1");
        h.Refs[RefNames.Main] = Id("c1");
        h.Seen["origin"] = new RemoteSeen { Etag = "\"r-7\"" };
        h.Seen["origin"].Refs[RefNames.Main] = Id("c1");
        using var http = new HttpClient(server);
        var remote = new HttpRemote(http, "http://cde.test", "http://cde.test/lapin-remote/1.0", Project, new RemoteTokens { Access = "test-access-token-viewer" });
        var me = new RemoteAuthor("atrium:viewer", "Viewer");
        var f = RemoteOps.Fetch(remote, h, "origin", me, DateTime.UtcNow);
        Assert.False(f.Unchanged);
        Assert.Equal((1, 1, 3, 1), (f.Commits, f.Trees, f.Blobs, f.Tags));
        // A fetch changes no local ref; the last-seen ones are the remote's.
        Assert.Equal(Id("c1"), h.Refs[RefNames.Main]);
        Assert.Equal(Id("c2"), h.Seen["origin"].Refs[RefNames.Main]);
        Assert.Equal((PullKind.FastForward, Id("c1"), Id("c2")), RemoteOps.Stand(h, "origin"));
        var again = RemoteOps.Fetch(remote, h, "origin", me, DateTime.UtcNow);
        Assert.True(again.Unchanged);
        Assert.Equal(Bytes("hello.py"), remote.Get(Id("hello.py")));
        Assert.Null(remote.Get(Id("missing.py")));
        server.AssertDone();
        Assert.Equal(server.PackVector, server.Packed);
        // Everything T2 names and the tag's file are held now.
        using var blobs = h.Blobs();
        foreach (var p in h.Tree(Id("T2")).Parts) Assert.NotNull(blobs.Get(p.Sha256));
        Assert.Equal(Bytes("issue.pdf"), blobs.Get(Id("issue.pdf")));
    }

    [Fact]
    public void Push_conflict_session_stops_at_409_and_keeps_the_work_as_a_branch()
    {
        var server = VectorServer.Of("push-conflict");
        server.Add(VectorServer.Load("push", 1));
        server.MayStayUnused(100);
        // Ana's last look at the refs, before Mihai's push: unchanged (the vector's story starts after it).
        server.Add(VectorServer.Synthetic("GET", $"/lapin-remote/1.0/projects/{Project}/refs", 304, "\"r-7\"", "test-access-token-ana"));
        var h = Story("document.json", "relations.arrow", "T1", "c1", "hello.py", "T3", "c3");
        h.Refs[RefNames.Main] = Id("c3");
        h.Seen["origin"] = new RemoteSeen { Etag = "\"r-7\"" };
        h.Seen["origin"].Refs[RefNames.Main] = Id("c1");
        using var http = new HttpClient(server);
        var remote = new HttpRemote(http, "http://cde.test", "/lapin-remote/1.0", Project, new RemoteTokens { Access = "test-access-token-ana" });
        var held = new HashSet<string>(StringComparer.Ordinal);
        var o = RemoteOps.Push(remote, h, "origin", [(RefNames.Main, RefNames.Main)], DateTime.UtcNow, uploaded: held);
        Assert.False(o.Pushed);
        Assert.Equal(RefNames.Main, o.Moved);
        Assert.Equal(Id("c2"), o.Current);
        Assert.Equal("Mihai Ionescu", o.MovedBy?.Name);
        Assert.Equal(new[] { Id("T3"), Id("c3") }.Order(StringComparer.Ordinal), o.Uploaded.Order(StringComparer.Ordinal));
        // Keep my work as a branch: a new ref, old null, on wip/<me>/<date>; nothing uploaded again.
        var wip = RefNames.Wip("Ana Popescu", new DateTime(2026, 11, 23, 10, 46, 0, DateTimeKind.Utc), _ => false);
        h.Refs[wip] = Id("c3");
        var kept = RemoteOps.Push(remote, h, "origin", [(wip, wip)], DateTime.UtcNow, fetchRefs: false, uploaded: held);
        Assert.True(kept.Pushed);
        Assert.Empty(kept.Uploaded);
        server.AssertDone();
        Assert.Equal(Id("c2"), h.Seen["origin"].Refs[RefNames.Main]);
    }

    [Fact]
    public void Error_session_codes_reach_the_client()
    {
        var server = VectorServer.Of("errors");
        server.Add(VectorServer.Load("push", 1));
        foreach (var n in new[] { 2, 3, 8, 9, 10, 11, 12, 100 }) server.MayStayUnused(n);
        using var http = new HttpClient(server);
        HttpRemote As(string? who) => new(http, "http://cde.test", "/lapin-remote/1.0", Project, new RemoteTokens { Access = who == null ? null : $"test-access-token-{who}" });
        var bad = Assert.Throws<RemoteException>(() => As("mihai").Put(Id("hello.py"), Bytes("hello.txt"), ObjectKind.Blob));
        Assert.Equal((400, "hash_mismatch"), (bad.Status, bad.Code));
        Assert.Equal(Id("hello.txt"), (string)bad.Body["actual"]!);
        var missing = Assert.Throws<RemoteException>(() => As("mihai").UpdateRefs([new RefUpdate(RefNames.Main, Id("c2"), Id("c4"))]));
        Assert.Equal((422, "object_missing"), (missing.Status, missing.Code));
        Assert.Equal(Id("missing.py"), (string)missing.Body["missing"]![0]!["id"]!);
        var viewer = Assert.Throws<RemoteException>(() => As("viewer").UpdateRefs([new RefUpdate(RefNames.Main, Id("c2"), Id("c4"))]));
        Assert.Equal((403, "forbidden"), (viewer.Status, viewer.Code));
        var none = Assert.Throws<RemoteException>(() => As(null).Refs(null));
        Assert.Equal((401, "unauthorized"), (none.Status, none.Code));
        var stranger = Assert.Throws<RemoteException>(() => As("stranger").Refs(null));
        Assert.Equal((404, "not_found"), (stranger.Status, stranger.Code));
        var tag = Assert.Throws<RemoteException>(() => As("mihai").UpdateRefs([new RefUpdate("refs/tags/issue/1-P01", Id("tag-issue"), Id("tag-version"))]));
        Assert.Equal((422, "tag_immutable"), (tag.Status, tag.Code));
        server.AssertDone();
    }

    [Fact]
    public void A_401_refreshes_the_tokens_once_and_repeats_the_request()
    {
        var server = new VectorServer([]);
        server.Add(VectorServer.Load("push", 1));
        server.Add(VectorServer.Synthetic("GET", $"/lapin-remote/1.0/projects/{Project}/refs", 401, null, "old-token"));
        server.Add(VectorServer.Synthetic("GET", "/foundation/1.1/auth", 200, null, null, new JsonObject { ["oauth2_token_url"] = "/foundation/1.1/auth/token", ["supported_oauth2_flows"] = new JsonArray("resource_owner_password_credentials_grant") }));
        server.Add(VectorServer.Synthetic("POST", "/foundation/1.1/auth/token", 200, null, null, new JsonObject { ["access_token"] = "new-token", ["token_type"] = "bearer", ["expires_in"] = 3600, ["refresh_token"] = "refresh-2" }));
        server.Add(VectorServer.Synthetic("GET", $"/lapin-remote/1.0/projects/{Project}/refs", 200, null, "new-token", new JsonObject { ["refs"] = new JsonArray() }));
        using var http = new HttpClient(server);
        var tokens = new RemoteTokens { Access = "old-token", Refresh = "refresh-1" };
        var remote = new HttpRemote(http, "http://cde.test", "/lapin-remote/1.0", Project, tokens);
        Assert.Empty(remote.Refs(null).Refs);
        Assert.True(tokens.Changed);
        Assert.Equal(("new-token", "refresh-2"), (tokens.Access, tokens.Refresh));
        Assert.Contains("refresh_token=refresh-1", Encoding.UTF8.GetString(server.Requests.Single(r => r.Path.EndsWith("/token", StringComparison.Ordinal)).Body));
        server.AssertDone();
    }
}

/// <summary>
/// A fake lapin-remote server that answers with a session's recorded responses (docs/spec-lapin-remote/vectors/session): each request
/// is matched to the first unused exchange of the same method and path (the order of independent requests may differ), its body
/// checked against the vector's, and answered with the vector's status, headers and body.
/// </summary>
sealed class VectorServer(List<JsonObject> exchanges) : HttpMessageHandler
{
    const string Upload = "u-1";
    readonly List<JsonObject> _exchanges = exchanges;
    readonly HashSet<int> _used = [];
    readonly HashSet<int> _mayStay = [];
    readonly List<string> _problems = [];
    int _patches;
    public List<(string Method, string Path, byte[] Body)> Requests { get; } = [];
    /// <summary>A request whose response is lost (the connection broke after the server took it): the n-th of its kind.</summary>
    public Func<HttpRequestMessage, int, bool>? BreakAfter { get; set; }
    /// <summary>Pack requests may ask any subset of the vector's pack; <see cref="Packed"/> collects what they asked, in order.</summary>
    public bool PackSubsets { get; set; }
    public List<string> Packed { get; } = [];
    public List<string> PackVector { get; private set; } = [];

    public static VectorServer Of(string session)
    {
        var dir = Path.Combine(RemoteVectorTests.Vectors, "session", session);
        var list = Directory.EnumerateFiles(dir, "*.json").Order(StringComparer.Ordinal).Select(f => Parse(File.ReadAllText(f), dir)).ToList();
        return new VectorServer(list);
    }

    static JsonObject Parse(string text, string dir)
    {
        var o = (JsonObject)JsonNode.Parse(text.Replace("{upload}", Upload, StringComparison.Ordinal))!;
        o["dir"] = dir;
        return o;
    }

    public static JsonObject Load(string session, int step)
    {
        var dir = Path.Combine(RemoteVectorTests.Vectors, "session", session);
        var f = Directory.EnumerateFiles(dir, $"{step:00}-*.json").Single();
        var o = Parse(File.ReadAllText(f), dir);
        o["step"] = 100;
        return o;
    }

    public static JsonObject Synthetic(string method, string path, int status, string? etag, string? token = null, JsonObject? body = null)
    {
        var headers = new JsonObject();
        if (token != null) headers["authorization"] = $"Bearer {token}";
        var rh = new JsonObject();
        if (etag != null) rh["etag"] = etag;
        if (status == 401) rh["www-authenticate"] = "Bearer";
        return new JsonObject
        {
            ["step"] = 200, ["request"] = new JsonObject { ["method"] = method, ["path"] = path, ["headers"] = headers },
            ["response"] = new JsonObject { ["status"] = status, ["headers"] = rh, ["body"] = body },
        };
    }

    public void Add(JsonObject exchange) => _exchanges.Add(exchange);
    public void MayStayUnused(int step) => _mayStay.Add(step);

    public void AssertDone()
    {
        Assert.Empty(_problems);
        var unused = _exchanges.Select((e, i) => (e, i)).Where(x => !_used.Contains(x.i) && !_mayStay.Contains((int)x.e["step"]!) && (int)x.e["step"]! != 200)
            .Select(x => $"{(int)x.e["step"]!}: {(string?)x.e["title"]}").ToList();
        Assert.Empty(unused);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Send(request, cancellationToken));

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.PathAndQuery;
        var body = request.Content?.ReadAsByteArrayAsync(cancellationToken).GetAwaiter().GetResult() ?? [];
        Requests.Add((request.Method.Method, path, body));
        var auth = request.Headers.Authorization?.ToString();
        int index = -1;
        for (var i = 0; i < _exchanges.Count; i++)
        {
            if (_used.Contains(i)) continue;
            var req = (JsonObject)_exchanges[i]["request"]!;
            if ((string)req["method"]! != request.Method.Method || (string)req["path"]! != path) continue;
            var want = (string?)req["headers"]?["authorization"];
            if (want != auth) continue;
            index = i;
            break;
        }
        if (index < 0)
        {
            _problems.Add($"No exchange for {request.Method} {path} ({auth}).");
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
        var ex = _exchanges[index];
        var r = (JsonObject)ex["request"]!;
        if (r["headers"]?["if-none-match"] is { } inm && request.Headers.IfNoneMatch.ToString() != (string)inm!)
            _problems.Add($"{(int)ex["step"]!}: If-None-Match {request.Headers.IfNoneMatch} instead of {(string)inm!}.");
        if (r["headers"]?["upload-offset"] is { } uo && (request.Headers.TryGetValues("Upload-Offset", out var vals) ? vals.First() : null) != (string)uo!)
            _problems.Add($"{(int)ex["step"]!}: Upload-Offset differs.");
        var pack = path.EndsWith("/objects/pack", StringComparison.Ordinal);
        if (!CheckBody(ex, body, pack)) { _problems.Add($"{(int)ex["step"]!}: the body of {request.Method} {path} is not the vector's."); }
        // A pack served in subsets stays available until all its entries are asked for.
        if (!(pack && PackSubsets) || Packed.Count >= PackVector.Count) _used.Add(index);
        var n = Requests.Count(q => q.Method == request.Method.Method) - 1;
        if (request.Method == HttpMethod.Patch) n = _patches++;
        var response = Response(ex, pack && PackSubsets ? body : null);
        if (BreakAfter?.Invoke(request, n) == true) throw new IOException("The connection broke (test).");
        return response;
    }

    bool CheckBody(JsonObject ex, byte[] body, bool pack)
    {
        var r = (JsonObject)ex["request"]!;
        var dir = (string)ex["dir"]!;
        if (r["bodyFile"] is { } file)
        {
            var bytes = File.ReadAllBytes(Path.GetFullPath(Path.Combine(dir, (string)file!)));
            if (r["bodyRange"] is JsonObject range) bytes = bytes.AsSpan((int)range["offset"]!, (int)range["length"]!).ToArray();
            return bytes.AsSpan().SequenceEqual(body);
        }
        if (r["body"] is not { } want) return true;
        var have = JsonNode.Parse(body);
        if (pack && PackSubsets)
        {
            if (PackVector.Count == 0) PackVector = ((JsonArray)want["hashes"]!).Select(x => (string)x!).ToList();
            var asked = ((JsonArray)have!["hashes"]!).Select(x => (string)x!).ToList();
            Packed.AddRange(asked);
            return asked.All(PackVector.Contains);
        }
        // objects/missing may list the same hashes in another order.
        if (want["hashes"] is JsonArray wh && have?["hashes"] is JsonArray hh)
            return wh.Select(x => (string)x!).Order().SequenceEqual(hh.Select(x => (string)x!).Order());
        return JsonNode.DeepEquals(want, have);
    }

    HttpResponseMessage Response(JsonObject ex, byte[]? packAsked)
    {
        var r = (JsonObject)ex["response"]!;
        var dir = (string?)ex["dir"];
        var res = new HttpResponseMessage((HttpStatusCode)(int)r["status"]!);
        HttpContent? content = null;
        if (r["bodyFile"] is { } file) content = new ByteArrayContent(File.ReadAllBytes(Path.GetFullPath(Path.Combine(dir!, (string)file!))));
        else if (r["bodyZipEntries"] is JsonArray entries)
        {
            var asked = packAsked == null ? null : ((JsonArray)JsonNode.Parse(packAsked)!["hashes"]!).Select(x => (string)x!).ToHashSet();
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                foreach (var e in entries.OfType<JsonObject>())
                {
                    if (asked != null && !asked.Contains((string)e["name"]!)) continue;
                    using var s = zip.CreateEntry((string)e["name"]!).Open();
                    s.Write(File.ReadAllBytes(Path.GetFullPath(Path.Combine(dir!, (string)e["file"]!))));
                }
            content = new ByteArrayContent(ms.ToArray());
        }
        else if (r["body"] is { } body) content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        res.Content = content ?? new ByteArrayContent([]);
        foreach (var (k, v) in (JsonObject?)r["headers"] ?? [])
        {
            if (k == "content-type") { res.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse((string)v!); continue; }
            res.Headers.TryAddWithoutValidation(k, (string)v!);
        }
        return res;
    }
}
