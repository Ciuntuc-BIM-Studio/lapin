using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Mesh;
using Lapin.Model;
using Lapin.Platform;
using Lapin.PointClouds;
using static Lapin.Engine.Tests.PointCloudSamples;

namespace Lapin.Engine.Tests;

/// <summary>
/// Point clouds and splats, G2 (docs/plan-gaussian-splats.md): a cloud calibrated by two points and their true distance (its
/// placement's factor, the first point kept, one undo step, the unit check following it, the splats scaled with it), a crop
/// box keeping what is inside it from the tiles, the splats, the slices and the exports, and a PLY mesh (MeshSplatting's
/// output) imported as one coloured element rather than linked as a cloud.
/// </summary>
public class SplatCalibrateCropTests
{
    static SplatCalibrateCropTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lapin-pointcloud-tests");
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("LAPIN_POINTCLOUD_CACHE", dir);
    }

    static Fixture F() => new(new ArchitectureModule());

    static string Linked(Fixture f, byte[] bytes, string name, JsonObject? placement = null)
    {
        var r = (JsonObject)f.Run("pointcloud.link", new JsonObject { ["name"] = name, ["data"] = Convert.ToBase64String(bytes), ["placement"] = placement })!;
        var id = (string)r["id"]!;
        for (var k = 0; k < 400; k++)
        {
            var st = (JsonObject)f.Session.Query("pointcloud.status", new JsonObject { ["id"] = id })!;
            if ((string?)st["state"] == "ready") break;
            Assert.NotEqual("failed", (string?)st["state"]);
            Thread.Sleep(25);
        }
        f.Session.TakeDeliveries();
        return id;
    }

    static void Near(double want, double got, double tol, string what) => Assert.True(Math.Abs(want - got) <= tol, $"{what}: {got} is not {want}");

    [Fact]
    public void A_cloud_is_calibrated_by_two_points_and_their_true_distance_the_first_point_kept_in_one_undo_step()
    {
        var f = F();
        // A room 5 × 4 × 3 m written as if it were millimetres too small: placed in metres, it is 1/2 its true size.
        var id = Linked(f, Las(Room(12)), "room.las", new JsonObject { ["mode"] = "origin", ["rotation"] = 30, ["offset"] = new JsonArray(1, 2, 0) });
        var link = Links.Require(f.Model, id);
        var t = PointCloudLinks.TransformOf(f.Model, link);
        var a = PointCloudLinks.Apply(t, 0, 0, 0);
        var b = PointCloudLinks.Apply(t, 5, 0, 0);
        var r = (JsonObject)f.Run("pointcloud.calibrate", new JsonObject { ["id"] = id, ["a"] = new JsonArray(a.X, a.Y, a.Z), ["b"] = new JsonArray(b.X, b.Y, b.Z), ["length"] = 10 })!;
        Near(2, (double)r["scale"]!, 1e-9, "the factor");
        link = Links.Require(f.Model, id);
        Near(2, PointCloudLinks.ScaleOf(link.Attrs["placement"] as JsonObject), 1e-9, "the placement's factor");
        var t2 = PointCloudLinks.TransformOf(f.Model, link);
        var a2 = PointCloudLinks.Apply(t2, 0, 0, 0);
        var b2 = PointCloudLinks.Apply(t2, 5, 0, 0);
        Near(a.X, a2.X, 1e-5, "a.x"); Near(a.Y, a2.Y, 1e-5, "a.y"); Near(a.Z, a2.Z, 1e-5, "a.z");
        Near(10, Math.Sqrt((b2.X - a2.X) * (b2.X - a2.X) + (b2.Y - a2.Y) * (b2.Y - a2.Y) + (b2.Z - a2.Z) * (b2.Z - a2.Z)), 1e-6, "the distance now");
        // The hierarchy (the 3D view's boxes) follows: the room is now 6 m high.
        var h = (JsonObject)f.Session.Query("pointcloud.hierarchy", new JsonObject { ["id"] = id })!;
        Near(6, (double)h["box"]![5]! - (double)h["box"]![2]!, 1e-3, "its height");
        // The unit check counts the factor: calibrated tiny, it warns.
        Assert.NotNull(PointCloudLinks.UnitWarning([0, 0, 0, 5, 4, 3], "m", null, 0.01));
        Assert.Null(PointCloudLinks.UnitWarning([0, 0, 0, 5, 4, 3], "m", null, 2));
        // One undo step brings the factor and the offset back.
        f.Session.Undo();
        Assert.Equal(1, PointCloudLinks.ScaleOf(Links.Require(f.Model, id).Attrs["placement"] as JsonObject));
        Assert.Throws<CommandException>(() => f.Run("pointcloud.calibrate", new JsonObject { ["id"] = id, ["a"] = new JsonArray(0, 0, 0), ["b"] = new JsonArray(0, 0, 0), ["length"] = 1 }));
        Assert.Throws<CommandException>(() => f.Run("pointcloud.place", new JsonObject { ["id"] = id, ["scale"] = -1 }));
        // Typed in the place dialog, the same factor.
        f.Run("pointcloud.place", new JsonObject { ["id"] = id, ["scale"] = 0.5 });
        Near(0.5, PointCloudLinks.ScaleOf(Links.Require(f.Model, id).Attrs["placement"] as JsonObject), 1e-12, "typed");
    }

    [Fact]
    public void A_calibrated_splat_cloud_scales_its_gaussians_with_it()
    {
        var f = F();
        var gs = new List<SplatTests.G> { new(0, 0, 0, [0, 0, 0], 3, [Math.Log(0.1), Math.Log(0.1), Math.Log(0.1)], [1, 0, 0, 0]), new(1, 0, 0, [0, 0, 0], 3, [0, 0, 0], [1, 0, 0, 0]) };
        var id = Linked(f, SplatTests.Ply(gs), "s.ply");
        var before = PointCloudLinks.Splats(f.Model, Links.Require(f.Model, id), 0, 100);
        f.Run("pointcloud.calibrate", new JsonObject { ["id"] = id, ["a"] = new JsonArray(before.Positions[0], before.Positions[1], before.Positions[2]), ["b"] = new JsonArray(before.Positions[3], before.Positions[4], before.Positions[5]), ["length"] = 3 });
        var after = PointCloudLinks.Splats(f.Model, Links.Require(f.Model, id), 0, 100);
        Near(0.3, after.Scales[0], 1e-6, "a splat's size, three times");
        Near(3, after.Positions[3] - after.Positions[0], 1e-5, "their distance");
    }

    [Fact]
    public void A_crop_box_keeps_what_is_inside_from_the_tiles_the_splats_the_exports_and_the_slices()
    {
        var f = F();
        var pts = Room(20);
        var id = Linked(f, Las(pts), "room.las");
        var link = Links.Require(f.Model, id);
        var names = ((JsonObject)f.Session.Query("pointcloud.hierarchy", new JsonObject { ["id"] = id })!)["nodes"]!.AsArray().Select(n => (string)n![0]!).ToList();
        Assert.Equal(pts.Count, PointCloudLinks.Tiles(f.Model, link, names).Sum(t => t.Colors.Length));
        // Fit to cloud: a box round all of it, nothing left out.
        var fit = (JsonObject)f.Run("pointcloud.crop", new JsonObject { ["id"] = id, ["fit"] = true })!;
        Near(5, (double)fit["size"]![0]!, 1e-3, "the fit's length");
        link = Links.Require(f.Model, id);
        Assert.Equal(pts.Count, PointCloudLinks.Tiles(f.Model, link, names).Sum(t => t.Colors.Length));
        // Half of it in x: about half of the points (the wall at x = 0 kept whole).
        f.Run("pointcloud.crop", new JsonObject { ["id"] = id, ["crop"] = new JsonObject { ["center"] = new JsonArray(1.25, 2, 1.5), ["size"] = new JsonArray(2.6, 4.2, 3.2), ["rotation"] = 0 } });
        link = Links.Require(f.Model, id);
        var kept = PointCloudLinks.Tiles(f.Model, link, names).SelectMany(t => Enumerable.Range(0, t.Colors.Length).Select(i => t.Positions[3 * i])).ToList();
        Assert.Equal(pts.Count(p => p.X <= 2.55), kept.Count);
        Assert.All(kept, x => Assert.True(x <= 2.551));
        // The export's sample too.
        var (positions, _) = PointCloudLinks.Sample(f.Model, link, PointCloudLinks.LookIn(null, link), 1_000_000);
        Assert.Equal(kept.Count, positions.Length / 3);
        // The hierarchy's box is the crop's.
        var box = ((JsonObject)f.Session.Query("pointcloud.hierarchy", new JsonObject { ["id"] = id })!)["box"]!.AsArray().Select(v => (double)v!).ToArray();
        Near(2.55, box[3], 1e-3, "the box's end");
        // Turned 90°: its x along the project's y.
        var turned = new CropBox([2.5, 2, 1.5], [1, 10, 10], 90);
        Assert.True(turned.Inside(0, 2.4, 1));
        Assert.False(turned.Inside(2.5, 2.6, 1));
        // A crop that is no box is refused; null takes it off, and undo puts it back.
        Assert.Throws<CommandException>(() => f.Run("pointcloud.crop", new JsonObject { ["id"] = id, ["crop"] = new JsonObject { ["center"] = new JsonArray(0, 0, 0), ["size"] = new JsonArray(0, 1, 1) } }));
        f.Run("pointcloud.crop", new JsonObject { ["id"] = id, ["crop"] = null });
        Assert.Null(PointCloudLinks.CropOf(Links.Require(f.Model, id)));
        f.Session.Undo();
        Assert.NotNull(PointCloudLinks.CropOf(Links.Require(f.Model, id)));
    }

    [Fact]
    public void A_cropped_splat_cloud_sends_only_the_gaussians_inside()
    {
        var f = F();
        var gs = Enumerable.Range(0, 10).Select(i => new SplatTests.G(i, 0, 0, [0, 0, 0], 3, [-2, -2, -2], [1, 0, 0, 0])).ToList();
        var id = Linked(f, SplatTests.Ply(gs), "s.ply", new JsonObject { ["up"] = "z" });
        f.Run("pointcloud.crop", new JsonObject { ["id"] = id, ["crop"] = new JsonObject { ["center"] = new JsonArray(2, 0, 0), ["size"] = new JsonArray(4.5, 1, 1) } });
        var page = PointCloudLinks.Splats(f.Model, Links.Require(f.Model, id), 0, 100);
        Assert.Equal(5, page.Colors.Length);
        Assert.All(Enumerable.Range(0, 5), i => Assert.InRange(page.Positions[3 * i], -0.3, 4.3));
    }

    const string AsciiMesh = """
        ply
        format ascii 1.0
        comment a quad and a triangle, coloured by vertex
        element vertex 5
        property float x
        property float y
        property float z
        property uchar red
        property uchar green
        property uchar blue
        element face 2
        property list uchar int vertex_indices
        end_header
        0 0 0 250 10 10
        1 0 0 250 10 10
        1 1 0 250 10 10
        0 1 0 250 10 10
        0 0 1 10 10 250
        4 0 1 2 3
        3 0 1 4
        """;

    static byte[] BinaryMesh()
    {
        var head = Encoding.ASCII.GetBytes("ply\nformat binary_little_endian 1.0\nelement vertex 3\nproperty float x\nproperty float y\nproperty float z\nelement face 1\nproperty list uchar uint vertex_index\nend_header\n");
        var body = new MemoryStream();
        var w = new BinaryWriter(body);
        foreach (var v in new[] { 0f, 0, 0, 2, 0, 0, 0, 3, 0 }) w.Write(v);
        w.Write((byte)3); w.Write(0u); w.Write(1u); w.Write(2u);
        return [.. head, .. body.ToArray()];
    }

    [Fact]
    public void A_ply_mesh_is_imported_as_one_element_its_parts_by_colour_and_refused_as_a_point_cloud()
    {
        var f = F();
        var r = MeshImport.Run(f.Session, Encoding.ASCII.GetBytes(AsciiMesh.Replace("\r", "")), "capture.ply", new JsonObject { ["up"] = "z" }, null, out _);
        Assert.Equal(1, r.Elements);
        Assert.Equal(3, r.Triangles);
        var e = f.Model.Entities.Single(x => x.Class == IfcClass.BuildingElementProxy);
        Assert.Equal("capture", e.Name);
        var parts = e.Attrs[StoredBodies.Key]!["parts"]!.AsArray();
        Assert.Equal(2, parts.Count);
        // Red (the quad) and the triangle's mean of red, red and blue: two colours.
        Assert.Contains(parts, p => (string?)p!["color"] == "#f05030" || (string?)p!["color"] == "#f01010");
        // Binary, −y up (a 3DGS capture's axes): its y goes down, so (0, 3, 0) ends at z = −3.
        var g = F();
        var b = MeshImport.Run(g.Session, BinaryMesh(), "tri.ply", new JsonObject { ["up"] = "-y" }, null, out _);
        Assert.Equal(1, b.Triangles);
        var body = g.Model.Entities.Single(x => x.Class == IfcClass.BuildingElementProxy).Attrs[StoredBodies.Key]!["parts"]![0]!.AsObject();
        var (v, _) = StoredBodies.ReadPart(body);
        Assert.Contains(Enumerable.Range(0, v.Length / 3), i => Math.Abs(v[3 * i + 2] + 3) < 1e-9);
        // Linked as a point cloud, it says it is a mesh; asked for its vertices, they are linked.
        var h = F();
        var ex = Assert.Throws<CommandException>(() => h.Run("pointcloud.link", new JsonObject { ["name"] = "capture.ply", ["data"] = Convert.ToBase64String(Encoding.ASCII.GetBytes(AsciiMesh.Replace("\r", ""))) }));
        Assert.Contains("holds a mesh", ex.Message);
        var ok = (JsonObject)h.Run("pointcloud.link", new JsonObject { ["name"] = "capture.ply", ["data"] = Convert.ToBase64String(Encoding.ASCII.GetBytes(AsciiMesh.Replace("\r", ""))), ["asPoints"] = true })!;
        Assert.Equal(5L, (long)ok["points"]!);
    }
}
