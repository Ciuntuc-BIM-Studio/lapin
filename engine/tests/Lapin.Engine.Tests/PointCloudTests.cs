using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Platform;
using Lapin.PointClouds;
using static Lapin.Engine.Tests.PointCloudSamples;

namespace Lapin.Engine.Tests;

/// <summary>
/// Point clouds (docs/point-clouds.md): the readers of the open formats on tiny files written here, the octree they are
/// indexed into (every point once, each in its node's cube, samples on the grid), and the link: its placement, its unit
/// check, its status, its hierarchy and tiles in the project's coordinates.
/// </summary>
public class PointCloudTests
{
    static PointCloudTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lapin-pointcloud-tests");
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("LAPIN_POINTCLOUD_CACHE", dir);
    }

    static List<PointBatch> All(IPointReader r) => r.Read(500).Select(b => Copy(b)).ToList();
    static PointBatch Copy(PointBatch b)
    {
        var c = new PointBatch(b.Count);
        for (var i = 0; i < b.Count; i++) c.Add(b.X[i], b.Y[i], b.Z[i], b.R[i], b.G[i], b.B[i], b.Intensity[i], b.Class[i]);
        return c;
    }
    static IEnumerable<(double X, double Y, double Z, ushort R, ushort I, byte C)> Points(IPointReader r) =>
        All(r).SelectMany(b => Enumerable.Range(0, b.Count).Select(i => (b.X[i], b.Y[i], b.Z[i], b.R[i], b.Intensity[i], b.Class[i])));

    static string TempDir() { var d = Path.Combine(Path.GetTempPath(), "lapin-pc-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(6, 4)]
    [InlineData(7, 4)]
    [InlineData(8, 4)]
    public void A_las_file_is_read_with_its_coordinates_colour_intensity_and_class(int format, int minor)
    {
        var pts = Room(10, 500_000, 400_000, 100);
        var bytes = Las(pts, format, minor, offset: [500_000, 400_000, 0]);
        var r = PointReaders.Open(new BytesSource("room.las", bytes));
        Assert.Equal($"LAS 1.{minor}, point format {format}", r.Header.Format);
        Assert.Equal(pts.Count, r.Header.Count);
        Assert.Equal(format is 2 or 3 or 7 or 8, r.Header.HasColor);
        var read = Points(r).ToList();
        Assert.Equal(pts.Count, read.Count);
        for (var i = 0; i < pts.Count; i += 37)
        {
            Assert.Equal(pts[i].X, read[i].X, 3);
            Assert.Equal(pts[i].Y, read[i].Y, 3);
            Assert.Equal(pts[i].Z, read[i].Z, 3);
            Assert.Equal(pts[i].I, read[i].I);
            Assert.Equal(pts[i].C, read[i].C);
            if (r.Header.HasColor) Assert.Equal(pts[i].R * 257, read[i].R);
        }
        Assert.Equal(500_000, r.Header.Bounds![0], 3);
    }

    [Fact]
    public void A_las_coordinate_system_is_read_from_its_wkt_or_its_geotiff_keys()
    {
        var pts = Room(4);
        const string Wkt = "PROJCS[\"Pulkovo 1942(58) / Stereo70\",GEOGCS[\"Pulkovo 1942(58)\",DATUM[\"Pulkovo_1942_58\",SPHEROID[\"Krassowsky 1940\",6378245,298.3,AUTHORITY[\"EPSG\",\"7024\"]],AUTHORITY[\"EPSG\",\"6179\"]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433],AUTHORITY[\"EPSG\",\"4179\"]],PROJECTION[\"Oblique_Stereographic\"],UNIT[\"metre\",1,AUTHORITY[\"EPSG\",\"9001\"]],AUTHORITY[\"EPSG\",\"3844\"]]";
        var r = PointReaders.Open(new BytesSource("a.las", Las(pts, 2, 4, wkt: Wkt)));
        Assert.Equal("EPSG:3844", r.Header.Crs);
        Assert.Equal("m", r.Header.Units);
        // GeoTIFF keys: version 1.1.0, 2 keys: ProjectedCSTypeGeoKey 2240 (a US state plane in feet), ProjLinearUnitsGeoKey US survey foot.
        var keys = new ushort[] { 1, 1, 0, 2, 3072, 0, 1, 2240, 3076, 0, 1, 9003 };
        var g = PointReaders.Open(new BytesSource("b.las", Las(pts, 0, 2, geoKeys: keys)));
        Assert.Equal("EPSG:2240", g.Header.Crs);
        Assert.Equal("usft", g.Header.Units);
    }

    [Fact]
    public void Ply_files_ascii_and_binary_both_endians_are_read()
    {
        var pts = Room(6);
        foreach (var (name, bytes) in new[] { ("a.ply", PlyAscii(pts)), ("l.ply", PlyBinary(pts)), ("b.ply", PlyBinary(pts, big: true)) })
        {
            var r = PointReaders.Open(new BytesSource(name, bytes));
            Assert.Equal(pts.Count, r.Header.Count);
            Assert.True(r.Header.HasColor);
            var read = Points(r).ToList();
            Assert.Equal(pts.Count, read.Count);
            Assert.Equal(pts[17].X, read[17].X, 4);
            Assert.Equal(pts[17].Z, read[17].Z, 4);
            Assert.Equal(pts[17].R, read[17].R);
            if (name != "a.ply")
            {
                Assert.True(r.Header.HasIntensity && r.Header.HasClassification);
                Assert.Equal(pts[17].C, read[17].C);
                Assert.InRange(read[17].I, pts[17].I - 2, pts[17].I + 2);
            }
        }
    }

    [Theory]
    [InlineData("xyz")]
    [InlineData("pts")]
    [InlineData("csv")]
    public void Text_clouds_are_read_by_their_columns(string kind)
    {
        var pts = Room(5);
        var r = PointReaders.Open(new BytesSource($"cloud.{kind}", Text(pts, kind)));
        var read = Points(r).ToList();
        Assert.Equal(pts.Count, read.Count);
        Assert.True(r.Header.HasColor);
        Assert.Equal(pts[11].X, read[11].X, 3);
        Assert.Equal(pts[11].Y, read[11].Y, 3);
        Assert.Equal(pts[11].R, read[11].R);
        if (kind == "csv") { Assert.Equal(pts[11].C, read[11].C); Assert.Equal(pts[11].I, read[11].I); }
        if (kind == "pts") Assert.InRange(read[11].I, pts[11].I - 16, pts[11].I + 16);
    }

    [Fact]
    public void A_file_that_is_not_a_cloud_says_so()
    {
        Assert.Throws<PointCloudException>(() => PointReaders.Open(new BytesSource("a.las", "not a las file"u8.ToArray())));
        Assert.Throws<PointCloudException>(() => PointReaders.Open(new BytesSource("a.rcp", [1, 2, 3, 4, 5, 6, 7, 8])));
    }

    /// <summary>Every point is in exactly one node, inside that node's cube; an inner node keeps one point a cell of its grid.</summary>
    static void CheckIndex(PointStore s, IReadOnlyList<P> pts, double step)
    {
        Assert.Equal(pts.Count, s.Points);
        Assert.Equal(pts.Count, s.Nodes.Sum(n => n.Count));
        var names = s.Nodes.Select(n => n.Name).ToHashSet();
        Assert.Contains("r", names);
        var found = new List<(double, double, double)>();
        foreach (var n in s.Nodes)
        {
            // Every node's parent is a node.
            if (n.Name != "r") Assert.Contains(n.Name[..^1], names);
            var (x0, y0, z0, e) = s.CubeOf(n.Name);
            var p = s.Read(n.Name);
            var cells = new HashSet<(int, int, int)>();
            for (var i = 0; i < p.Count; i++)
            {
                var (x, y, z) = (p.X(i), p.Y(i), p.Z(i));
                Assert.InRange(x, x0 - step, x0 + e + step);
                Assert.InRange(y, y0 - step, y0 + e + step);
                Assert.InRange(z, z0 - step, z0 + e + step);
                found.Add((x, y, z));
                var cell = ((int)((x - x0) / e * PointStore.Resolution), (int)((y - y0) / e * PointStore.Resolution), (int)((z - z0) / e * PointStore.Resolution));
                if (names.Any(c => c.Length == n.Name.Length + 1 && c.StartsWith(n.Name, StringComparison.Ordinal))) Assert.True(cells.Add(cell), $"two points in one cell of {n.Name}");
            }
        }
        // The very points (to the step), each once.
        var left = new Dictionary<(long, long, long), List<(double X, double Y, double Z)>>();
        static (long, long, long) Key(double x, double y, double z) => ((long)Math.Floor(x / 0.01), (long)Math.Floor(y / 0.01), (long)Math.Floor(z / 0.01));
        foreach (var q in pts) { var k = Key(q.X, q.Y, q.Z); if (!left.TryGetValue(k, out var l)) left[k] = l = []; l.Add((q.X, q.Y, q.Z)); }
        foreach (var (x, y, z) in found)
        {
            var (kx, ky, kz) = Key(x, y, z);
            var hit = false;
            for (var dx = -1; dx <= 1 && !hit; dx++) for (var dy = -1; dy <= 1 && !hit; dy++) for (var dz = -1; dz <= 1 && !hit; dz++)
                if (left.TryGetValue((kx + dx, ky + dy, kz + dz), out var l))
                {
                    var i = l.FindIndex(q => Math.Abs(q.X - x) <= step && Math.Abs(q.Y - y) <= step && Math.Abs(q.Z - z) <= step);
                    if (i >= 0) { l.RemoveAt(i); hit = true; }
                }
            Assert.True(hit, $"a point at {x}, {y}, {z} that the file does not have");
        }
    }

    [Fact]
    public void A_small_cloud_is_indexed_in_memory_into_an_octree_holding_every_point_once()
    {
        var pts = Room(200);
        var s = Indexer.Build(PointReaders.Open(new BytesSource("room.las", Las(pts))), TempDir(), "k", 1, limits: new Indexer.Limits(LeafMax: 5000));
        Assert.True(s.Nodes.Count > 8);
        Assert.True(s.HasColor && s.HasIntensity && s.HasClassification);
        Assert.Equal(pts.Count(p => p.C == 6), s.Classes[6]);
        CheckIndex(s, pts, 0.002);
    }

    [Fact]
    public void A_large_cloud_is_counted_split_into_chunks_and_merged_bottom_up_with_the_same_result()
    {
        // Limits small enough that ten thousand points go the way a hundred million do.
        var pts = Room(60, 10, 20, 0).Concat(Room(30, 40, 20, 5)).ToList();
        var dir = TempDir();
        var stages = new List<string>();
        var s = Indexer.Build(PointReaders.Open(new BytesSource("big.las", Las(pts))), dir, "k", 1, (st, _) => { if (!stages.Contains(st)) stages.Add(st); },
            limits: new Indexer.Limits(InMemory: 1000, ChunkMax: 900, LeafMax: 300));
        Assert.Equal(["Reading", "Counting", "Sorting", "Indexing", "Done"], stages);
        Assert.False(Directory.Exists(Path.Combine(dir, "chunks")));
        CheckIndex(s, pts, 0.002);
        // Opened again from its folder, the same.
        var again = PointStore.Open(dir)!;
        Assert.Equal(s.Nodes.Count, again.Nodes.Count);
        Assert.Equal(s.Points, again.Points);
    }

    static Fixture F() => new(new ArchitectureModule());

    /// <summary>A cloud linked by its bytes, waited for until indexed.</summary>
    static (Fixture F, string Id, JsonObject Result) Linked(byte[] bytes, string name, JsonObject? placement = null, Fixture? f = null)
    {
        f ??= F();
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
        return (f, id, r);
    }

    [Fact]
    public void A_linked_cloud_is_a_link_of_its_own_indexed_in_the_background_and_served_in_tiles_in_the_projects_coordinates()
    {
        var pts = Room(20);
        var (f, id, r) = Linked(Las(pts), "scan.las");
        var link = Links.Require(f.Model, id);
        Assert.Equal("pointcloud", link.Str("kind"));
        Assert.Equal("scan", link.Name);
        Assert.Equal("origin", (string?)r["placement"]!["mode"]);
        // The index's summary delivered onto the link, as its own undo step.
        Assert.Equal(pts.Count, (long)link.Attrs["cloud"]!["points"]!);
        Assert.Equal("rgb", link.Str("colorMode"));
        var h = (JsonObject)f.Session.Query("pointcloud.hierarchy", new JsonObject { ["id"] = id })!;
        Assert.Equal(pts.Count, (long)h["points"]!);
        var nodes = h["nodes"]!.AsArray();
        Assert.Equal(pts.Count, nodes.Sum(n => (int)n![1]!));
        var tiles = PointCloudLinks.Tiles(f.Model, link, nodes.Select(n => (string)n![0]!));
        Assert.Equal(pts.Count, tiles.Sum(t => t.Colors.Length));
        var box = h["box"]!.AsArray().Select(v => (double)v!).ToArray();
        Assert.Equal(0, box[0], 2); Assert.Equal(5, box[3], 2); Assert.Equal(3, box[5], 2);
        // Not part of the model's elements: nothing on a storey.
        Assert.Equal(0, (int)f.Session.Query("link.list", new JsonObject())!.AsArray()[0]!["elements"]!);
    }

    [Fact]
    public void A_cloud_is_placed_centred_moved_turned_and_scaled_by_its_unit()
    {
        var pts = Room(10, 1000, 2000, 50);
        var (f, id, _) = Linked(Las(pts), "c.las", new JsonObject { ["mode"] = "center" });
        var link = Links.Require(f.Model, id);
        var t = PointCloudLinks.TransformOf(f.Model, link);
        var c = PointCloudLinks.Apply(t, 1002.5, 2002, 50);
        Assert.Equal(0, c.X, 6); Assert.Equal(0, c.Y, 6); Assert.Equal(0, c.Z, 6);
        // Millimetres, turned 90°, moved.
        f.Run("pointcloud.place", new JsonObject { ["id"] = id, ["mode"] = "origin", ["units"] = "mm", ["rotation"] = 90, ["offset"] = new JsonArray(10, 0, 1) });
        t = PointCloudLinks.TransformOf(f.Model, Links.Require(f.Model, id));
        var p = PointCloudLinks.Apply(t, 1000, 0, 0);
        Assert.Equal(10, p.X, 6); Assert.Equal(1, p.Y, 6); Assert.Equal(1, p.Z, 6);
        Assert.Throws<CommandException>(() => f.Run("pointcloud.place", new JsonObject { ["id"] = id, ["units"] = "furlong" }));
    }

    [Fact]
    public void A_cloud_in_map_coordinates_stands_on_the_projects_survey_point()
    {
        var f = F();
        f.Run("project.location", new JsonObject { ["survey"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["easting"] = 500_000, ["northing"] = 400_000, ["elevation"] = 100 }, ["crs"] = "EPSG:3844" });
        var pts = Room(10, 500_010, 400_020, 102);
        var (_, id, r) = Linked(Las(pts, 2, 2, offset: [500_000, 400_000, 0]), "map.las", null, f);
        // Far from zero and the project tied to the map: by the map.
        Assert.Equal("shared", (string?)r["placement"]!["mode"]);
        var t = PointCloudLinks.TransformOf(f.Model, Links.Require(f.Model, id));
        var p = PointCloudLinks.Apply(t, 500_010, 400_020, 102);
        Assert.Equal(10, p.X, 4); Assert.Equal(20, p.Y, 4); Assert.Equal(2, p.Z, 4);
    }

    [Fact]
    public void The_unit_check_warns_of_a_size_unlikely_for_a_scan_and_of_a_unit_the_file_contradicts()
    {
        Assert.Null(PointCloudLinks.UnitWarning([0, 0, 0, 20, 10, 6], "m", null));
        Assert.Contains("km", PointCloudLinks.UnitWarning([0, 0, 0, 200_000, 10, 6], "m", null));
        Assert.Contains("mm", PointCloudLinks.UnitWarning([0, 0, 0, 20, 10, 6], "mm", null));
        Assert.Contains("US survey feet", PointCloudLinks.UnitWarning([0, 0, 0, 20, 10, 6], "m", "usft"));
        var keys = new ushort[] { 1, 1, 0, 1, 3076, 0, 1, 9002 };
        var (f, _, r) = Linked(Las(Room(5), 0, 2, geoKeys: keys), "feet.las");
        Assert.Equal("ft", (string?)r["placement"]!["units"]);
        Assert.Empty(r["notes"]!.AsArray());
        _ = f;
    }

    [Fact]
    public void A_cloud_is_not_exported_to_ifc()
    {
        var (f, _, _) = Linked(Las(Room(5)), "never-exported.las");
        using var ms = new MemoryStream();
        Lapin.Ifc.IfcExport.Write(f.Session, ms, "x.ifc", new Lapin.Ifc.IfcExportOptions("IFC4", "group", null));
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.DoesNotContain("never-exported", text);
        Assert.DoesNotContain("pointcloud", text);
    }

    [Fact]
    public void A_cloud_unlinked_leaves_the_model_as_it_was_and_undo_brings_the_link_back()
    {
        var (f, id, _) = Linked(Las(Room(5)), "u.las");
        f.Run("link.unlink", new JsonObject { ["id"] = id });
        Assert.Null(f.Model.Get(id));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(id));
    }
}
