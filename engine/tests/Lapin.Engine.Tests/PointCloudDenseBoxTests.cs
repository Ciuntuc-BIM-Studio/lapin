using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Platform;
using Lapin.PointClouds;
using static Lapin.Engine.Tests.PointCloudSamples;

namespace Lapin.Engine.Tests;

/// <summary>
/// Where a cloud is dense (<see cref="DenseBox"/>, a real 3DGS capture's floaters, decision in docs/plan-gaussian-splats.md):
/// counted when indexed, the same by the large clouds' path, worked out from the nodes for an index made before; a cloud with
/// 2 % of far outliers gets the box of its body, a clean one its whole box; the hierarchy's <c>core</c> (what Fit frames)
/// inside the crop; "Fit to cloud" proposes the dense box rounded, or the whole box when asked.
/// </summary>
public class PointCloudDenseBoxTests
{
    static PointCloudDenseBoxTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lapin-pointcloud-tests");
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("LAPIN_POINTCLOUD_CACHE", dir);
    }

    static string TempDir() { var d = Path.Combine(Path.GetTempPath(), "lapin-pc-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    /// <summary>40 000 points through a 10 × 6 × 3 m block at (10, 5, 0), and 2 % more scattered over 200 × 200 × 100 m round it.</summary>
    static List<P> Floaters(int body = 40_000, double share = 0.02, int seed = 1)
    {
        var rnd = new Random(seed);
        var o = new List<P>();
        for (var i = 0; i < body; i++) o.Add(new P(10 + 10 * rnd.NextDouble(), 5 + 6 * rnd.NextDouble(), 3 * rnd.NextDouble(), 120, 120, 120));
        var far = (int)Math.Round(body * share / (1 - share));
        for (var i = 0; i < far; i++) o.Add(new P(-100 + 200 * rnd.NextDouble(), -100 + 200 * rnd.NextDouble(), -50 + 100 * rnd.NextDouble(), 200, 220, 255));
        return o;
    }

    static void Within(double want, double got, double tol, string what) => Assert.True(Math.Abs(want - got) <= tol, $"{what}: {got} is not {want} ± {tol}");

    /// <summary>The block's box within a cell of the 128³ grid over the outliers' cube (200 m / 128).</summary>
    static void IsBlock(double[] b, double tol = 1.6)
    {
        Within(10, b[0], tol, "min x"); Within(5, b[1], tol, "min y"); Within(0, b[2], tol, "min z");
        Within(20, b[3], tol, "max x"); Within(11, b[4], tol, "max y"); Within(3, b[5], tol, "max z");
        Assert.True(b[0] <= 10.01 && b[1] <= 5.01 && b[2] <= 0.01 && b[3] >= 19.99 && b[4] >= 10.99 && b[5] >= 2.99, "the block is inside: " + string.Join(", ", b));
    }

    [Fact]
    public void A_cloud_with_two_percent_far_outliers_gets_the_box_of_its_body_the_same_by_both_ways_of_indexing()
    {
        var pts = Floaters();
        var small = Indexer.Build(PointReaders.Open(new BytesSource("f.las", Las(pts))), TempDir(), "k", 1);
        Assert.True(small.Bounds[3] - small.Bounds[0] > 190, "the outliers make the cloud 200 m wide");
        IsBlock(small.Core);
        // The large clouds' path (counted, sorted into chunks) counts the same cells: the same box.
        var dir = TempDir();
        var large = Indexer.Build(PointReaders.Open(new BytesSource("f.las", Las(pts))), dir, "k", 1, limits: new Indexer.Limits(InMemory: 5000, ChunkMax: 4000, LeafMax: 1000));
        Assert.Equal(small.Core, large.Core);
        // Kept in the index's meta: read back as counted.
        Assert.Equal(large.Core, PointStore.Open(dir)!.Core);
        // An index made before it was kept: worked out from its nodes, as fine as the nodes holding the block's points (a node
        // of 25 m holds a sample of it here): coarser, still a fraction of the 200 m, round the block.
        var old = DenseBox.Of(large);
        Assert.True(old[0] <= 10.01 && old[1] <= 5.01 && old[2] <= 0.01 && old[3] >= 19.99 && old[4] >= 10.99 && old[5] >= 2.99, "the block is inside: " + string.Join(", ", old));
        Assert.True(old[3] - old[0] < 50 && old[4] - old[1] < 50 && old[5] - old[2] < 50, "a fraction of the cloud: " + string.Join(", ", old));
    }

    [Fact]
    public void A_clean_cloud_keeps_its_whole_box()
    {
        foreach (var pts in new[] { Room(40), Floaters(20_000, 0) })
        {
            var s = Indexer.Build(PointReaders.Open(new BytesSource("c.las", Las(pts))), TempDir(), "k", 1);
            Assert.Equal(s.Bounds, s.Core);
        }
        // Too few points to tell: the whole box.
        Assert.Equal([0, 0, 0, 1, 1, 1], DenseBox.FromCells(new float[DenseBox.Grid * DenseBox.Grid * DenseBox.Grid], [0, 0, 0], 1, [0, 0, 0, 1, 1, 1]));
    }

    [Fact]
    public void The_hierarchy_gives_fit_the_dense_box_inside_the_crop_and_fit_to_cloud_proposes_it_rounded()
    {
        var f = new Fixture(new ArchitectureModule());
        var r = (JsonObject)f.Run("pointcloud.link", new JsonObject { ["name"] = "floaters.las", ["data"] = Convert.ToBase64String(Las(Floaters())), ["placement"] = new JsonObject { ["mode"] = "origin" } })!;
        var id = (string)r["id"]!;
        for (var k = 0; k < 400; k++)
        {
            var st = (JsonObject)f.Session.Query("pointcloud.status", new JsonObject { ["id"] = id })!;
            if ((string?)st["state"] == "ready") break;
            Assert.NotEqual("failed", (string?)st["state"]);
            Thread.Sleep(25);
        }
        f.Session.TakeDeliveries();
        double[] Arr(JsonNode? n) => n!.AsArray().Select(v => (double)v!).ToArray();
        JsonObject H() => (JsonObject)f.Session.Query("pointcloud.hierarchy", new JsonObject { ["id"] = id })!;
        var h = H();
        Assert.True(Arr(h["box"])[3] - Arr(h["box"])[0] > 190);
        IsBlock(Arr(h["core"]));
        // The link's summary carries it too.
        Assert.Equal(6, PointCloudLinks.Summary(PointCloudLinks.StoreOf(Links.Require(f.Model, id))!)["core"]?.AsArray().Count);

        // Fit to cloud: the dense box, its sides on a step of about a fiftieth of it (11–13 m: 50 cm), nothing of the block left out.
        var fit = CropBox.From((JsonObject)f.Run("pointcloud.crop", new JsonObject { ["id"] = id, ["fit"] = true })!)!;
        var b = fit.Box();
        IsBlock(b, 1.8);
        foreach (var v in b) Within(Math.Round(v / 0.5) * 0.5, v, 1e-6, "on the step");
        // The whole cloud when asked: round the outliers too, exactly.
        var whole = CropBox.From((JsonObject)f.Run("pointcloud.crop", new JsonObject { ["id"] = id, ["fit"] = true, ["whole"] = true })!)!;
        var w = whole.Box();
        var bounds = PointCloudLinks.BoundsOf(Links.Require(f.Model, id))!;
        for (var k = 0; k < 6; k++) Within(bounds[k], w[k], 1e-3, "the whole box");
        // One undo step each.
        f.Session.Undo();
        Within(fit.Size[0], PointCloudLinks.CropOf(Links.Require(f.Model, id))!.Size[0], 1e-9, "undone to the dense fit");

        // Cropped to a part of the block: Fit's box is inside the crop.
        f.Run("pointcloud.crop", new JsonObject { ["id"] = id, ["crop"] = new JsonObject { ["center"] = new JsonArray(12, 7, 1), ["size"] = new JsonArray(4, 4, 2), ["rotation"] = 0 } });
        var core = Arr(H()["core"]);
        Assert.True(core[0] >= 9.99 && core[1] >= 4.99 && core[2] >= -0.01 && core[3] <= 14.01 && core[4] <= 9.01 && core[5] <= 2.01, "inside the crop: " + string.Join(", ", core));
        // A crop away from the body: Fit frames the crop's own box.
        f.Run("pointcloud.crop", new JsonObject { ["id"] = id, ["crop"] = new JsonObject { ["center"] = new JsonArray(-60, -60, 0), ["size"] = new JsonArray(20, 20, 20), ["rotation"] = 0 } });
        h = H();
        Assert.Equal(Arr(h["box"]), Arr(h["core"]));
    }

    [Fact]
    public void A_nice_step_is_one_two_or_five_times_a_power_of_ten()
    {
        Within(0.2, PointCloudLinks.NiceStep(0.13), 1e-12, "0.13");
        Within(0.5, PointCloudLinks.NiceStep(0.3), 1e-12, "0.3");
        Within(1, PointCloudLinks.NiceStep(1), 1e-12, "1");
        Within(10, PointCloudLinks.NiceStep(6), 1e-12, "6");
        Within(0.001, PointCloudLinks.NiceStep(0), 1e-12, "0");
    }
}
