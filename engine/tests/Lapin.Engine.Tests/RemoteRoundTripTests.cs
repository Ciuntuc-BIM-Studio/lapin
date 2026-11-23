using System.IO.Compression;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Package;
using Lapin.Package.Remote;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The history in the project's file and a folder remote, through the engine's calls as the interface makes them
/// (docs/spec-lapin-remote.md, stage S1 of docs/plan-atrium-cde.md): commit, push, clone into a new file with identical parts,
/// a second push uploading only what changed, a concurrent push refused then pulled, a fast-forward pull as one undo step.
/// </summary>
public sealed class RemoteRoundTripTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-roundtrip-").FullName;
    string PathOf(string name) => Path.Combine(_dir, name);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static readonly JsonObject Ana = new() { ["id"] = "local:a1a1a1a1a1a1a1a1", ["name"] = "Ana Popescu" };
    static readonly JsonObject Mihai = new() { ["id"] = "local:b2b2b2b2b2b2b2b2", ["name"] = "Mihai Ionescu" };

    sealed class Client(string dir, JsonObject author, Fixture? fixture = null)
    {
        public Fixture F { get; } = fixture ?? Project();
        public EngineDispatcher D { get; private set; } = null!;
        public JsonObject Author => author;
        public string Folder => Path.Combine(dir, "remote");

        public Client Start()
        {
            D = new EngineDispatcher(F.Session, PackageFiles.Create());
            return this;
        }

        public JsonNode? Call(string method, JsonObject? p = null)
        {
            p ??= [];
            p["author"] = author.DeepClone();
            var (reply, _) = D.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
            var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
            if (o["error"] is { } err) throw new InvalidOperationException((string)err!);
            return o["result"];
        }
    }

    static Fixture Project(int walls = 2)
    {
        var f = new Fixture();
        var type = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, true));
        for (var i = 0; i < walls; i++) f.Wall(0, i * 3.0, 6, i * 3.0, type);
        return f;
    }

    static List<(string Path, string Sha)> Parts(string file)
    {
        using var zip = ZipFile.OpenRead(file);
        return LapinPackage.Read(file).Manifest.Parts.Select(p => (p.Path, p.Sha256)).ToList();
    }

    static List<string> Entries(string file, string prefix)
    {
        using var zip = ZipFile.OpenRead(file);
        return zip.Entries.Select(e => e.FullName).Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>A's project saved, committed and pushed to a new folder remote; the remote's project id.</summary>
    static string Publish(Client a, string file)
    {
        a.Call("project.save", new JsonObject { ["path"] = file });
        a.Call("history.commit", new JsonObject { ["message"] = "Initial model" });
        var status = a.Call("remote.add", new JsonObject { ["kind"] = "folder", ["url"] = FolderRemote.UrlOf(a.Folder), ["projectName"] = "Bloc P+4" })!;
        var pushed = a.Call("remote.push")!;
        Assert.True((bool)pushed["pushed"]!, pushed.ToJsonString());
        return (string)status["remotes"]![0]!["project"]!;
    }

    static Client Clone(string dir, JsonObject author, string url, string project, string file)
    {
        var b = new Client(dir, author, new Fixture()).Start();
        b.Call("remote.clone", new JsonObject { ["kind"] = "folder", ["url"] = url, ["project"] = project, ["path"] = file });
        return b;
    }

    [Fact]
    public void A_project_without_history_writes_none_and_opens_as_before()
    {
        var a = new Client(_dir, Ana).Start();
        var file = PathOf("plain.lapin");
        a.Call("project.save", new JsonObject { ["path"] = file });
        Assert.Empty(Entries(file, "history/"));
        var status = a.Call("history.status")!;
        Assert.Null((string?)status["head"]);
        Assert.True((bool)status["uncommitted"]!);
        var b = new Client(_dir, Ana, new Fixture()).Start();
        b.Call("project.open", new JsonObject { ["path"] = file });
        Assert.Equal(a.F.Session.Model.Entities.Count(), b.F.Session.Model.Entities.Count());
    }

    [Fact]
    public void Commit_push_to_a_folder_then_clone_gives_identical_parts()
    {
        var a = new Client(_dir, Ana).Start();
        var fileA = PathOf("a.lapin");
        var project = Publish(a, fileA);
        // The history is in the file: HEAD, refs, remotes, index, the commit and its tree; the blobs are the live parts (held once).
        Assert.Contains("history/HEAD", Entries(fileA, "history/"));
        var head = (string)a.Call("history.status")!["head"]!;
        Assert.Contains($"history/commits/{head}", Entries(fileA, "history/"));
        Assert.DoesNotContain(Entries(fileA, "history/objects/"), e => Parts(fileA).Any(p => e.EndsWith(p.Sha, StringComparison.Ordinal)));
        // The remote's file lists no token: url and project only.
        using (var zip = ZipFile.OpenRead(fileA))
        {
            using var s = zip.GetEntry("history/remotes.json")!.Open();
            var remotes = JsonNode.Parse(s)!;
            Assert.Equal(["kind", "name", "project", "url"], ((JsonObject)remotes["remotes"]![0]!).Select(kv => kv.Key).Order());
        }
        // The viewer copy of the head: an IFC4 of the model, beside the commit.
        var viewer = JsonNode.Parse(File.ReadAllText(Path.Combine(a.Folder, project, "viewers", head + ".json")))!;
        Assert.Equal("IFC4", (string)viewer["schema"]!);
        using (var r = FolderRemote.Open(a.Folder, project, new RemoteAuthor("local:a1a1a1a1a1a1a1a1", "Ana")))
            Assert.StartsWith("ISO-10303-21", System.Text.Encoding.ASCII.GetString(r.Get((string)viewer["ifc"]!)!.AsSpan(0, 12)));

        var fileB = PathOf("b.lapin");
        var b = Clone(_dir, Mihai, FolderRemote.UrlOf(a.Folder), project, fileB);
        Assert.Equal(Parts(fileA), Parts(fileB));
        var diff = ModelCompare.Compare(a.F.Session.Model.Entities.ToList(), a.F.Session.Model.Relations.ToList(), b.F.Session.Model.Entities.ToList(), b.F.Session.Model.Relations.ToList());
        Assert.False(diff.Any);
        var sb = b.Call("history.status")!;
        Assert.Equal(head, (string)sb["head"]!);
        Assert.False((bool)sb["uncommitted"]!);
        Assert.Equal(0, (int)sb["remotes"]![0]!["ahead"]!);
        Assert.Equal(0, (int)sb["remotes"]![0]!["behind"]!);
        // The clone's file carries the same history.
        var hb = PackageHistory.Of(fileB);
        Assert.Equal(head, hb.Refs[RefNames.Main]);
        Assert.Empty(hb.Problems);
    }

    [Fact]
    public void A_second_push_uploads_only_the_changed_parts()
    {
        var a = new Client(_dir, Ana).Start();
        var fileA = PathOf("a.lapin");
        Publish(a, fileA);
        var before = Parts(fileA);
        a.F.Wall(0, 9, 6, 9);
        a.Call("history.commit", new JsonObject { ["message"] = "One wall more" });
        var after = Parts(fileA);
        var changed = after.Count(p => !before.Contains(p));
        var pushed = a.Call("remote.push", new JsonObject { ["noViewer"] = true })!;
        Assert.True((bool)pushed["pushed"]!);
        // The commit, its tree and the parts that changed: nothing else.
        Assert.Equal(changed + 2, (int)pushed["uploaded"]!);
        Assert.True(changed < after.Count, "some parts are unchanged");
        var log = (JsonArray)a.Call("history.log")!;
        Assert.Equal(["One wall more", "Initial model"], log.Select(c => (string)c!["message"]!));
        Assert.All(log, c => Assert.True((bool)c!["pushed"]!));
        // A commit with nothing changed is refused.
        Assert.Throws<InvalidOperationException>(() => a.Call("history.commit", new JsonObject { ["message"] = "again" }));
    }

    [Fact]
    public void A_concurrent_push_is_refused_then_kept_as_a_branch_and_pulled()
    {
        var a = new Client(_dir, Ana).Start();
        var project = Publish(a, PathOf("a.lapin"));
        var url = FolderRemote.UrlOf(a.Folder);
        var b = Clone(_dir, Mihai, url, project, PathOf("b.lapin"));
        // Mihai pushes first.
        b.F.Wall(10, 0, 10, 6);
        b.Call("history.commit", new JsonObject { ["message"] = "Mihai's wall" });
        Assert.True((bool)b.Call("remote.push")!["pushed"]!);
        // Ana committed meanwhile: her push is refused, nothing moved on the remote.
        a.F.Wall(-5, 0, -5, 6);
        a.Call("history.commit", new JsonObject { ["message"] = "Ana's wall" });
        var refused = a.Call("remote.push")!;
        Assert.False((bool)refused["pushed"]!);
        Assert.Contains("pull first", (string)refused["message"]!, StringComparison.Ordinal);
        // A pull cannot merge in S1: it says so, and offers the two ways.
        var diverged = a.Call("remote.pull")!;
        Assert.False((bool)diverged["pulled"]!);
        Assert.Equal("diverged", (string)diverged["kind"]!);
        // Keep my work as a branch: pushed as wip/ana-popescu/…, then Ana's project is Mihai's.
        var kept = a.Call("remote.pull", new JsonObject { ["resolve"] = "branch" })!;
        Assert.True((bool)kept["pulled"]!);
        Assert.StartsWith("wip/ana-popescu/", (string)kept["branch"]!, StringComparison.Ordinal);
        var diff = ModelCompare.Compare(a.F.Session.Model.Entities.ToList(), a.F.Session.Model.Relations.ToList(), b.F.Session.Model.Entities.ToList(), b.F.Session.Model.Relations.ToList());
        Assert.False(diff.Any);
        using var r = FolderRemote.Open(a.Folder, project, new RemoteAuthor("local:a1a1a1a1a1a1a1a1", "Ana"));
        Assert.Contains(r.Refs(null).Refs, x => x.Name == "refs/heads/" + (string)kept["branch"]!);
        Assert.Equal((string)b.Call("history.status")!["head"]!, (string)a.Call("history.status")!["head"]!);
    }

    [Fact]
    public void A_fast_forward_pull_is_one_undo_step_and_refused_with_changes_not_committed()
    {
        var a = new Client(_dir, Ana).Start();
        var project = Publish(a, PathOf("a.lapin"));
        var url = FolderRemote.UrlOf(a.Folder);
        var b = Clone(_dir, Mihai, url, project, PathOf("b.lapin"));
        a.F.Wall(10, 0, 10, 6);
        a.Call("history.commit", new JsonObject { ["message"] = "A wall" });
        a.Call("remote.push", new JsonObject { ["noViewer"] = true });

        var fetched = b.Call("remote.fetch")!;
        Assert.Equal(1, (int)fetched["commits"]!);
        Assert.Equal(1, (int)fetched["status"]!["remotes"]![0]!["behind"]!);
        var count = b.F.Session.Model.Entities.Count();
        b.F.Wall(20, 0, 20, 6);
        var refused = Assert.Throws<InvalidOperationException>(() => b.Call("remote.pull"));
        Assert.Contains("commit first", refused.Message, StringComparison.Ordinal);
        b.Call("doc.undo");
        Assert.Equal(count, b.F.Session.Model.Entities.Count());

        var pulled = b.Call("remote.pull")!;
        Assert.True((bool)pulled["pulled"]!);
        Assert.Equal("fastForward", (string)pulled["kind"]!);
        Assert.False(ModelCompare.Compare(a.F.Session.Model.Entities.ToList(), a.F.Session.Model.Relations.ToList(), b.F.Session.Model.Entities.ToList(), b.F.Session.Model.Relations.ToList()).Any);
        Assert.False(b.F.Session.History.Dirty);
        Assert.Equal(Parts(PathOf("a.lapin")), Parts(PathOf("b.lapin")));
        // One undo step brings the project back to before the pull.
        b.Call("doc.undo");
        Assert.Equal(count, b.F.Session.Model.Entities.Count());
    }

    [Fact]
    public void A_blob_no_longer_live_moves_into_history_objects_once_and_fsck_finds_one_missing()
    {
        var a = new Client(_dir, Ana).Start();
        var file = PathOf("a.lapin");
        a.Call("project.save", new JsonObject { ["path"] = file });
        a.Call("history.commit", new JsonObject { ["message"] = "First" });
        var first = Parts(file);
        a.F.Wall(0, 20, 6, 20);
        a.Call("project.save");
        var live = Parts(file).Select(p => p.Sha).ToHashSet();
        var gone = first.Select(p => p.Sha).Where(s => !live.Contains(s)).Distinct().ToList();
        Assert.NotEmpty(gone);
        var objects = Entries(file, "history/objects/").Select(e => e["history/objects/".Length..]).ToList();
        // Each blob the first commit needs and the live project no longer has is in history/objects/, once; no live part is there.
        Assert.All(gone, g => Assert.Single(objects, o => o == g));
        Assert.DoesNotContain(objects, o => live.Contains(o));
        var index = JsonNode.Parse(Read(file, "history/index.json"))!;
        var ids = ((JsonArray)index["objects"]!).Select(o => (string)o!["id"]!).ToList();
        Assert.Equal(ids.Order(StringComparer.Ordinal), ids);
        Assert.Empty((JsonArray)index["absent"]!);
        // The first commit still reads, from its tree and the blobs where they are.
        var h = PackageHistory.Of(file);
        var c = h.Commit(h.Refs[RefNames.Main]);
        Assert.Equal(first.Count, h.Content(c.Tree).Manifest.Parts.Count);

        // A blob taken out of the file by another tool: the check at opening finds it, and the project still opens.
        var damaged = PathOf("damaged.lapin");
        using (var src = ZipFile.OpenRead(file))
        using (var dst = ZipFile.Open(damaged, ZipArchiveMode.Create))
            foreach (var e in src.Entries.Where(e => e.FullName != "history/objects/" + gone[0]))
            {
                using var i = e.Open();
                using var o = dst.CreateEntry(e.FullName).Open();
                i.CopyTo(o);
            }
        var hd = PackageHistory.Of(damaged);
        Assert.Contains(hd.Problems, p => p.Contains(gone[0][..12], StringComparison.Ordinal));
        var b = new Client(_dir, Ana, new Fixture()).Start();
        b.Call("project.open", new JsonObject { ["path"] = damaged });
        Assert.NotEmpty((JsonArray)b.Call("history.status")!["problems"]!);
        // Saved again, the absent blob is listed as absent: the commit is incomplete, never lost silently.
        b.Call("project.save");
        var again = JsonNode.Parse(Read(damaged, "history/index.json"))!;
        Assert.Contains((JsonArray)again["absent"]!, x => (string)x!["id"]! == gone[0]);
    }

    static byte[] Read(string file, string entry)
    {
        using var zip = ZipFile.OpenRead(file);
        using var s = zip.GetEntry(entry)!.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void A_project_opened_and_written_again_gives_the_same_parts_and_shows_no_change()
    {
        var a = new Client(_dir, Ana).Start();
        var file = PathOf("a.lapin");
        a.Call("project.save", new JsonObject { ["path"] = file });
        a.Call("history.commit", new JsonObject { ["message"] = "First" });
        var b = new Client(_dir, Ana, new Fixture()).Start();
        b.Call("project.open", new JsonObject { ["path"] = file });
        // The rows' places are their ranks, not the model's running counter: the same bytes after a load.
        Assert.Equal(Parts(file), ProjectPackage.Build(b.F.Session).Manifest.Parts.Select(p => (p.Path, p.Sha256)).ToList());
        Assert.False((bool)b.Call("history.status")!["uncommitted"]!);
    }

    [Fact]
    public void Remote_calls_are_never_a_guests()
    {
        foreach (var m in new[] { "history.commit", "remote.push", "remote.pull", "remote.fetch", "remote.add", "remote.clone", "history.status" })
            Assert.NotNull(EngineDispatcher.AllowedRemote(m, [], "editor"));
    }
}
