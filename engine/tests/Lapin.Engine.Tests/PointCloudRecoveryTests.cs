using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Platform;
using Lapin.PointClouds;
using static Lapin.Engine.Tests.PointCloudSamples;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 222 (an E57 from a Leica scanner froze Lapin when linked, and in a plan): an E57 linked by its path as the
/// desktop links it, by the default placement and origin to origin, indexed and drawn in a plan within a time limit (a hang
/// fails the test instead of holding up the run); an index cut short is not trusted but made again; a failed indexing is
/// tried again when the cloud is linked again. The freeze itself was the plan's snap index in the interface
/// (ui/src/viewport/cad/snaps.ts, its test in cad.test.ts).
/// </summary>
public class PointCloudRecoveryTests
{
    static PointCloudRecoveryTests()
    {
        // The same cache as PointCloudTests (the variable is the process's): keys are by content, so the classes never meet.
        var dir = Path.Combine(Path.GetTempPath(), "lapin-pointcloud-tests");
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("LAPIN_POINTCLOUD_CACHE", dir);
    }

    static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    static string FixtureFile(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "pointclouds", name);

    /// <summary>A copy of the three scans' E57 under a name of its own: a key, and so an index, no other test has.</summary>
    static string FreshE57()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lapin-pc-recovery-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "scan.e57");
        File.Copy(FixtureFile("three-scans.e57"), path);
        return path;
    }

    /// <summary>The link's status polled until it is ready or failed, within the limit.</summary>
    static async Task<JsonObject> Settled(Fixture f, string id)
    {
        var until = DateTime.UtcNow + Limit;
        while (true)
        {
            var st = (JsonObject)f.Session.Query("pointcloud.status", new JsonObject { ["id"] = id })!;
            if ((string?)st["state"] is "ready" or "failed" or "missing") { f.Session.TakeDeliveries(); return st; }
            Assert.True(DateTime.UtcNow < until, $"the cloud is still {st.ToJsonString()} after {Limit.TotalSeconds} s");
            await Task.Delay(25);
        }
    }

    static async Task<T> Within<T>(Func<T> work, string what)
    {
        try { return await Task.Run(work).WaitAsync(Limit); }
        catch (TimeoutException) { Assert.Fail($"{what} took more than {Limit.TotalSeconds} s"); throw; }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("origin")]
    public async Task An_e57_linked_by_its_path_is_indexed_and_drawn_in_a_plan_in_time(string? mode)
    {
        var f = new Fixture(new ArchitectureModule());
        var path = FreshE57();
        JsonObject Placement() => mode == null ? new JsonObject() : new JsonObject { ["mode"] = mode };
        var r = await Within(() => (JsonObject)f.Run("pointcloud.link", new JsonObject { ["name"] = "scan.e57", ["path"] = path, ["placement"] = Placement() })!, "the link");
        Assert.Equal("origin", (string?)r["placement"]!["mode"]);
        var id = (string)r["id"]!;
        Assert.Equal("ready", (string?)(await Settled(f, id))["state"]);
        var h = await Within(() => (JsonObject)f.Session.Query("pointcloud.hierarchy", new JsonObject { ["id"] = id })!, "the hierarchy");
        Assert.True((long)h["points"]! > 0);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var drawing = await Within(() => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!, "the plan");
        Assert.NotNull(drawing["pens"]);
        // Linked again (the same file, its index there): at once ready, drawn again.
        var again = await Within(() => (JsonObject)f.Run("pointcloud.link", new JsonObject { ["name"] = "scan.e57", ["path"] = path, ["placement"] = Placement() })!, "the second link");
        Assert.Equal("ready", (string?)again["status"]!["state"]);
        await Within(() => f.Session.Query("view.drawing", new JsonObject { ["id"] = plan }), "the plan again");
    }

    [Fact]
    public async Task A_plan_draws_a_scans_slice_after_the_link_in_time()
    {
        // A room scanned (its walls through the plan's cut), linked by its bytes, then the plan.
        var f = new Fixture(new ArchitectureModule());
        var r = (JsonObject)f.Run("pointcloud.link", new JsonObject { ["name"] = "room-222.las", ["data"] = Convert.ToBase64String(Las(Room(80))) })!;
        var id = (string)r["id"]!;
        Assert.Equal("ready", (string?)(await Settled(f, id))["state"]);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var drawing = await Within(() => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!, "the plan");
        var mark = drawing["marks"]!.AsArray().OfType<JsonObject>().Single(x => (string?)x["id"] == id);
        // Its snaps are points (the interface's snap index must stay quick over them: cad.test.ts), at most 20 000.
        Assert.InRange(mark["snaps"]!.AsArray().Count / 2, 1, 20_000);
    }

    [Fact]
    public async Task An_index_cut_short_is_made_again_when_the_cloud_is_linked()
    {
        var f = new Fixture(new ArchitectureModule());
        var path = FreshE57();
        var key = PointCloudLinks.KeyOfPath(path);
        var dir = Path.Combine(PointCloudLinks.CacheRoot, key);
        // What a build stopped by a force-quit leaves: its points file half written, a meta of the whole beside it (the worst case).
        var whole = Indexer.Build(PointReaders.Open(new FileSource(path)), Path.Combine(Path.GetTempPath(), "lapin-pc-whole-" + Guid.NewGuid().ToString("N")[..8]), key, new FileInfo(path).Length);
        Directory.CreateDirectory(dir);
        File.Copy(Path.Combine(whole.Directory, "meta.json"), Path.Combine(dir, "meta.json"), true);
        var bytes = File.ReadAllBytes(Path.Combine(whole.Directory, "points.bin"));
        File.WriteAllBytes(Path.Combine(dir, "points.bin"), bytes[..(bytes.Length / 2)]);
        whole.Dispose();
        Assert.Null(PointStore.Open(dir));
        // A points file with no meta (stopped before its end) is none either.
        var bare = Path.Combine(Path.GetTempPath(), "lapin-pc-bare-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(bare);
        File.WriteAllBytes(Path.Combine(bare, "points.bin"), bytes);
        Assert.Null(PointStore.Open(bare));

        var r = await Within(() => (JsonObject)f.Run("pointcloud.link", new JsonObject { ["name"] = "scan.e57", ["path"] = path })!, "the link");
        var id = (string)r["id"]!;
        var st = await Settled(f, id);
        Assert.Equal("ready", (string?)st["state"]);
        Assert.Equal(whole.Points, (long)st["points"]!);
        Assert.NotNull(PointStore.Open(dir));
    }

    [Fact]
    public async Task A_failed_indexing_is_tried_again_when_the_cloud_is_linked_again()
    {
        var f = new Fixture(new ArchitectureModule());
        var bytes = Las(Room(12));
        const string Name = "blocked-222.las";
        var key = PointCloudLinks.KeyOfBytes(Name, bytes);
        var dir = Path.Combine(PointCloudLinks.CacheRoot, key);
        // The index's folder taken by a file: the build cannot be made there and fails.
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        File.WriteAllText(dir, "not a folder");
        try
        {
            var r = (JsonObject)f.Run("pointcloud.link", new JsonObject { ["name"] = Name, ["data"] = Convert.ToBase64String(bytes) })!;
            var st = await Settled(f, (string)r["id"]!);
            Assert.Equal("failed", (string?)st["state"]);
            Assert.False(string.IsNullOrEmpty((string?)st["error"]));
        }
        finally { File.Delete(dir); }
        // The way cleared, linked again: indexed, not the failure remembered.
        var again = (JsonObject)f.Run("pointcloud.link", new JsonObject { ["name"] = Name, ["data"] = Convert.ToBase64String(bytes) })!;
        Assert.Equal("ready", (string?)(await Settled(f, (string)again["id"]!))["state"]);
    }
}
