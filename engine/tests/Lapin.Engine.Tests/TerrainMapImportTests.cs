using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.PointClouds;

namespace Lapin.Engine.Tests;

/// <summary>Adaptive grid-to-TIN simplification, GeoTIFF DEM & RGB orthophoto import, and LiDAR point-cloud-to-terrain.</summary>
public class TerrainMapImportTests
{
    static Fixture F() => new(new ArchitectureModule(), new CivilModule());

    [Fact]
    public void Adaptive_TIN_simplifies_flat_areas_and_preserves_a_hill_within_error()
    {
        // 64×64 grid (4096 pixels) with a Gaussian hill of height 12 m in the middle and flat surroundings.
        const int w = 64, h = 64;
        var heights = new double[w * h];
        for (var r = 0; r < h; r++)
            for (var c = 0; c < w; c++)
            {
                double dx = (c - 32.0) / 8.0, dy = (r - 32.0) / 8.0;
                heights[r * w + c] = 100.0 + 12.0 * Math.Exp(-(dx * dx + dy * dy));
            }
        double[] tr = [2.0, 0, 0, -2.0, 1.0, 127.0];
        var grid = new ElevationGrid(w, h, heights, tr);
        var pts = grid.Simplify((x, y) => new Vec2(x, y), maxError: 0.2, maxPoints: 800);

        // Far fewer than 4096 points, yet captures the 112 m peak accurately.
        Assert.InRange(pts.Count, 20, 800);
        var tin = new Terrain(Tin.Delaunay(pts));
        var peak = tin.ElevationAt(grid.MapAt(32, 32).X, grid.MapAt(32, 32).Y);
        Assert.NotNull(peak);
        Assert.InRange(peak!.Value, 111.6, 112.1);
        var corner = tin.ElevationAt(grid.MapAt(2, 2).X, grid.MapAt(2, 2).Y);
        Assert.NotNull(corner);
        Assert.InRange(corner!.Value, 99.8, 100.2);
    }

    [Fact]
    public void GeoTIFF_float32_deflate_DEM_imports_in_Stereo_70_with_nodata_ignored()
    {
        var f = F();
        const int w = 24, h = 24;
        var vals = new float[w * h];
        for (var r = 0; r < h; r++)
            for (var c = 0; c < w; c++)
                vals[r * w + c] = (r == 0 && c == 0) ? -9999f : (float)(200.0 + 0.5 * c + 0.3 * r);

        var tiff = BuildGeoTiffFloat32(w, h, vals, originX: 587900, originY: 325900, scaleX: 2, scaleY: 2, epsg: 3844, deflate: true, nodata: "-9999");
        var res = (JsonObject)f.Run("civil.importDem", new JsonObject
        {
            ["data"] = Convert.ToBase64String(tiff),
            ["name"] = "Copernicus DEM",
            ["maxError"] = 0.05,
            ["maxPoints"] = 500,
        })!;

        Assert.Equal(GeoRef.Stereo70, (string)res["crs"]!);
        Assert.Equal(GeoRef.Stereo70, Location.Crs(f.Model));
        Assert.Equal(24, (int)res["gridWidth"]!);
        Assert.Equal(24, (int)res["gridHeight"]!);

        var t = f.Model.Get((string)res["id"]!)!;
        var pts = Terrains.Points(t);
        Assert.True(pts.Count >= 4);
        // No nodata (-9999) leaked into terrain points.
        Assert.All(pts, p => Assert.InRange(p.Z, 199, 230));
    }

    [Fact]
    public void Raw_float32_elevation_grid_and_replaceInside_refinement_work()
    {
        var f = F();
        // Coarse 100×100 m base terrain at elevation 50 m.
        var baseRes = (JsonObject)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 100, ["y2"] = 100, ["elevation"] = 50 })!;
        var terrainId = (string)baseRes["id"]!;

        // High-res 16×16 local DEM patch covering [20..60]×[20..60] at elevation 55 m.
        const int w = 16, h = 16;
        var buf = new byte[w * h * 4];
        for (var i = 0; i < w * h; i++) BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(i * 4, 4), 55f);

        var r = (JsonObject)f.Run("civil.importDem", new JsonObject
        {
            ["width"] = w,
            ["height"] = h,
            ["heightsF32"] = Convert.ToBase64String(buf),
            ["corners"] = new JsonArray(new JsonArray(20, 60), new JsonArray(60, 60), new JsonArray(60, 20), new JsonArray(20, 20)),
            ["terrainId"] = terrainId,
            ["replaceInside"] = true,
            ["maxError"] = 0.1,
        })!;

        Assert.Equal(terrainId, (string)r["id"]!);
        var t = f.Model.Get(terrainId)!;
        var terrain = Terrains.Of(t);
        var centerZ = terrain.ElevationAt(40, 40);
        Assert.NotNull(centerZ);
        Assert.InRange(centerZ!.Value, 54.5, 55.5);
    }

    [Fact]
    public void RGB_GeoTIFF_imports_directly_as_an_orthophoto_without_world_file()
    {
        var f = F();
        var tiff = BuildGeoTiffRgb(8, 6, originX: 587900, originY: 325900, scaleX: 1, scaleY: 1, epsg: 3844);
        var r = (JsonObject)f.Run("civil.addOrthophoto", new JsonObject
        {
            ["data"] = Convert.ToBase64String(tiff),
            ["name"] = "Aerial GeoTIFF",
        })!;

        var ortho = f.Model.Get((string)r["id"]!)!;
        Assert.Equal(Orthophotos.Class, ortho.Class);
        Assert.Equal(GeoRef.Stereo70, Location.Crs(f.Model));
        var corners = Orthophotos.Corners(ortho);
        Assert.Equal(4, corners.Count);
        Assert.Equal(8.0, corners[1].X - corners[0].X, 2);
        Assert.Equal(6.0, corners[0].Y - corners[3].Y, 2);
    }

    [Fact]
    public void Terrain_from_LiDAR_point_cloud_filters_ground_class_and_ignores_trees()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "lapin-test-pc-" + Guid.NewGuid().ToString("N")[..8]);
        var prevCache = PointCloudLinks.CacheDir;
        PointCloudLinks.CacheDir = cacheDir;
        try
        {
            var f = F();
            // Build a LAS 1.2 file with 100 Ground points (class 2) at z = 10 + 0.1*x and 50 Vegetation points (class 5) at z = 25 m.
            var pts = new List<(double X, double Y, double Z, byte Class)>();
            for (var iy = 0; iy < 10; iy++)
                for (var ix = 0; ix < 10; ix++)
                {
                    pts.Add((ix * 2.0, iy * 2.0, 10.0 + 0.1 * ix * 2.0, 2)); // Ground
                    if ((ix + iy) % 2 == 0) pts.Add((ix * 2.0 + 0.3, iy * 2.0 + 0.3, 25.0, 5)); // High vegetation
                }
            var lasPath = Path.Combine(cacheDir, "site.las");
            Directory.CreateDirectory(cacheDir);
            File.WriteAllBytes(lasPath, BuildLas12(pts));

            // Link and index the point cloud synchronously.
            var linkRes = (JsonObject)f.Run("pointcloud.link", new JsonObject { ["path"] = lasPath, ["placement"] = new JsonObject { ["mode"] = "origin" } })!;
            var linkId = (string)linkRes["id"]!;
            // Wait briefly for background indexer to finish.
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var st = (JsonObject)f.Session.Query("pointcloud.status", new JsonObject { ["id"] = linkId })!;
                if ((string?)st["state"] == "ready") break;
                Thread.Sleep(50);
            }

            var info = (JsonObject)f.Session.Query("civil.pointCloudTerrainInfo", new JsonObject { ["linkId"] = linkId })!;
            Assert.Equal(150, (long)info["points"]!);
            Assert.Equal(100, (long)info["groundPoints"]!);

            var tr = (JsonObject)f.Run("civil.terrainFromPointCloud", new JsonObject
            {
                ["linkId"] = linkId,
                ["groundOnly"] = true,
                ["gridStep"] = 2.0,
                ["maxError"] = 0.05,
                ["datum"] = 0,
            })!;

            Assert.True((bool)tr["usedGroundClass"]!);
            Assert.Equal(100, (long)tr["sourcePoints"]!);
            var terrain = f.Model.Get((string)tr["id"]!)!;
            var tPts = Terrains.Points(terrain);
            // Vegetation points at z=25 must be completely excluded.
            Assert.All(tPts, p => Assert.InRange(p.Z, 9.5, 12.5));
            var mid = Terrains.Of(terrain).ElevationAt(10, 10);
            Assert.NotNull(mid);
            Assert.InRange(mid!.Value, 10.7, 11.3);
        }
        finally
        {
            PointCloudLinks.CacheDir = prevCache;
            try { if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true); } catch { }
        }
    }

    [Fact]
    public void Context_buildings_from_map_extrude_and_ground_onto_sloped_terrain()
    {
        var f = F();
        // Sloped terrain: z = 10 + 0.1 * x on [0..40]×[0..40].
        var tPts = new JsonArray();
        for (var y = 0; y <= 40; y += 20)
            for (var x = 0; x <= 40; x += 20)
                tPts.Add(new JsonArray(x, y, 10.0 + 0.1 * x));
        f.Run("civil.addTerrain", new JsonObject { ["points"] = tPts, ["datum"] = 0 });

        var buildings = new JsonArray(
            new JsonObject
            {
                ["name"] = "Villa",
                ["footprint"] = new JsonArray(new JsonArray(10, 10), new JsonArray(20, 10), new JsonArray(20, 20), new JsonArray(10, 20)),
                ["levels"] = 3,
            });
        var res = (JsonObject)f.Run("civil.importContextBuildings", new JsonObject { ["buildings"] = buildings, ["source"] = "OpenStreetMap" })!;
        Assert.Equal(1, (int)res["count"]!);
        var bId = (string)((JsonArray)res["ids"]!)[0]!;
        var b = f.Model.Get(bId)!;
        Assert.Equal(SiteFeatures.Class, b.Class);
        Assert.Equal("building", SiteFeatures.Kind(b));
        Assert.True(StoredBodies.Has(b));
        var area = f.Session.Quantities(bId).Single().Quantities.Single(q => q.Name == "Area").Value;
        Assert.InRange(area, 99, 101);
    }

    [Fact]
    public void Buildings_from_LiDAR_point_cloud_extracts_separated_class_6_footprints_and_heights()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "lapin-test-bldg-" + Guid.NewGuid().ToString("N")[..8]);
        var prevCache = PointCloudLinks.CacheDir;
        PointCloudLinks.CacheDir = cacheDir;
        try
        {
            var f = F();
            var pts = new List<(double X, double Y, double Z, byte Class)>();
            // Ground plane at z = 2.0 across [0..40]×[0..30]
            for (var y = 0; y <= 30; y += 2)
                for (var x = 0; x <= 40; x += 2)
                    pts.Add((x, y, 2.0, 2));
            // Building A: 8×6 m at [4..12]×[4..10], roof z = 11.0 (height = 9 m)
            for (var y = 4.0; y <= 10.0; y += 0.5)
                for (var x = 4.0; x <= 12.0; x += 0.5)
                    pts.Add((x, y, 11.0, 6));
            // Building B: 10×8 m at [24..34]×[14..22], roof z = 17.0 (height = 15 m)
            for (var y = 14.0; y <= 22.0; y += 0.5)
                for (var x = 24.0; x <= 34.0; x += 0.5)
                    pts.Add((x, y, 17.0, 6));

            var lasPath = Path.Combine(cacheDir, "buildings.las");
            Directory.CreateDirectory(cacheDir);
            File.WriteAllBytes(lasPath, BuildLas12(pts));

            var linkRes = (JsonObject)f.Run("pointcloud.link", new JsonObject { ["path"] = lasPath, ["placement"] = new JsonObject { ["mode"] = "origin" } })!;
            var linkId = (string)linkRes["id"]!;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var st = (JsonObject)f.Session.Query("pointcloud.status", new JsonObject { ["id"] = linkId })!;
                if ((string?)st["state"] == "ready") break;
                Thread.Sleep(50);
            }

            var info = (JsonObject)f.Session.Query("civil.pointCloudTerrainInfo", new JsonObject { ["linkId"] = linkId })!;
            Assert.True((long)info["buildingPoints"]! > 400);

            var res = (JsonObject)f.Run("civil.buildingsFromPointCloud", new JsonObject { ["linkId"] = linkId, ["gridStep"] = 1.0, ["minArea"] = 15.0 })!;
            Assert.Equal(2, (int)res["count"]!);
            var ids = ((JsonArray)res["ids"]!).Select(x => (string)x!).ToList();
            var heights = ids.Select(id => (double)f.Model.Get(id)!.Attrs["properties"]!["height"]!).Order().ToList();
            Assert.InRange(heights[0], 8.5, 9.5);
            Assert.InRange(heights[1], 14.5, 15.5);
        }
        finally
        {
            PointCloudLinks.CacheDir = prevCache;
            try { if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true); } catch { }
        }
    }

    // ─── Minimal GeoTIFF & LAS builders for deterministic tests ───

    static byte[] BuildGeoTiffFloat32(int w, int h, float[] values, double originX, double originY, double scaleX, double scaleY, int epsg, bool deflate, string nodata)
    {
        var raw = new byte[w * h * 4];
        for (var i = 0; i < values.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(i * 4, 4), values[i]);
        byte[] payload;
        if (deflate)
        {
            using var ms = new MemoryStream();
            using (var zs = new ZLibStream(ms, CompressionLevel.Fastest, leaveOpen: true)) zs.Write(raw);
            payload = ms.ToArray();
        }
        else payload = raw;

        return WriteTiff(w, h, bitsPerSample: 32, samplesPerPixel: 1, sampleFormat: 3, compression: deflate ? 8 : 1,
            payload, originX, originY, scaleX, scaleY, epsg, nodata);
    }

    static byte[] BuildGeoTiffRgb(int w, int h, double originX, double originY, double scaleX, double scaleY, int epsg)
    {
        var raw = new byte[w * h * 3];
        for (var i = 0; i < w * h; i++) { raw[i * 3] = 80; raw[i * 3 + 1] = 160; raw[i * 3 + 2] = 60; }
        return WriteTiff(w, h, bitsPerSample: 8, samplesPerPixel: 3, sampleFormat: 1, compression: 1,
            raw, originX, originY, scaleX, scaleY, epsg, null);
    }

    static byte[] WriteTiff(int w, int h, int bitsPerSample, int samplesPerPixel, int sampleFormat, int compression, byte[] payload,
        double originX, double originY, double scaleX, double scaleY, int epsg, string? nodata)
    {
        var entries = new List<(ushort Tag, ushort Type, uint Count, byte[] Data)>();
        byte[] U16(params ushort[] vs) { var b = new byte[vs.Length * 2]; for (var i = 0; i < vs.Length; i++) BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(i * 2), vs[i]); return b; }
        byte[] U32(params uint[] vs) { var b = new byte[vs.Length * 4]; for (var i = 0; i < vs.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i * 4), vs[i]); return b; }
        byte[] F64(params double[] vs) { var b = new byte[vs.Length * 8]; for (var i = 0; i < vs.Length; i++) BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(i * 8), vs[i]); return b; }

        // Placeholder for StripOffsets (filled once layout is known).
        entries.Add((256, 4, 1, U32((uint)w)));
        entries.Add((257, 4, 1, U32((uint)h)));
        entries.Add((258, 3, (uint)samplesPerPixel, U16(Enumerable.Repeat((ushort)bitsPerSample, samplesPerPixel).ToArray())));
        entries.Add((259, 3, 1, U16((ushort)compression)));
        entries.Add((273, 4, 1, U32(0))); // StripOffsets
        entries.Add((277, 3, 1, U16((ushort)samplesPerPixel)));
        entries.Add((278, 4, 1, U32((uint)h)));
        entries.Add((279, 4, 1, U32((uint)payload.Length)));
        entries.Add((339, 3, (uint)samplesPerPixel, U16(Enumerable.Repeat((ushort)sampleFormat, samplesPerPixel).ToArray())));
        entries.Add((33550, 12, 3, F64(scaleX, scaleY, 0)));
        entries.Add((33922, 12, 6, F64(0, 0, 0, originX, originY, 0)));
        entries.Add((34735, 3, 8, U16(1, 1, 0, 1, 3072, 0, 1, (ushort)epsg)));
        if (nodata != null)
        {
            var nd = Encoding.ASCII.GetBytes(nodata + "\0");
            entries.Add((42113, 2, (uint)nd.Length, nd));
        }

        entries.Sort((a, b) => a.Tag.CompareTo(b.Tag));
        var ifdStart = 8;
        var ifdSize = 2 + entries.Count * 12 + 4;
        var extraStart = ifdStart + ifdSize;
        var extra = new MemoryStream();
        var dataOffsets = new int[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Data.Length > 4)
            {
                dataOffsets[i] = extraStart + (int)extra.Length;
                extra.Write(entries[i].Data);
            }
        }
        var stripOffset = extraStart + (int)extra.Length;
        for (var i = 0; i < entries.Count; i++)
            if (entries[i].Tag == 273) entries[i] = (273, 4, 1, U32((uint)stripOffset));

        using var ms = new MemoryStream();
        ms.Write([(byte)'I', (byte)'I', 42, 0]);
        ms.Write(U32((uint)ifdStart));
        ms.Write(U16((ushort)entries.Count));
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            ms.Write(U16(e.Tag));
            ms.Write(U16(e.Type));
            ms.Write(U32(e.Count));
            if (e.Data.Length <= 4)
            {
                var pad = new byte[4];
                Buffer.BlockCopy(e.Data, 0, pad, 0, e.Data.Length);
                ms.Write(pad);
            }
            else ms.Write(U32((uint)dataOffsets[i]));
        }
        ms.Write(U32(0)); // next IFD = 0
        ms.Write(extra.ToArray());
        ms.Write(payload);
        return ms.ToArray();
    }

    static byte[] BuildLas12(IReadOnlyList<(double X, double Y, double Z, byte Class)> pts)
    {
        const int headerSize = 227, recordLen = 20;
        var buf = new byte[headerSize + pts.Count * recordLen];
        "LASF"u8.CopyTo(buf);
        buf[24] = 1; buf[25] = 2; // LAS 1.2
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(94), headerSize);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(96), headerSize);
        buf[104] = 0; // Point format 0
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(105), recordLen);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(107), (uint)pts.Count);
        for (var k = 0; k < 3; k++) BinaryPrimitives.WriteDoubleLittleEndian(buf.AsSpan(131 + 8 * k), 0.001);
        double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X);
        double minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
        double minZ = pts.Min(p => p.Z), maxZ = pts.Max(p => p.Z);
        BinaryPrimitives.WriteDoubleLittleEndian(buf.AsSpan(179), maxX);
        BinaryPrimitives.WriteDoubleLittleEndian(buf.AsSpan(187), minX);
        BinaryPrimitives.WriteDoubleLittleEndian(buf.AsSpan(195), maxY);
        BinaryPrimitives.WriteDoubleLittleEndian(buf.AsSpan(203), minY);
        BinaryPrimitives.WriteDoubleLittleEndian(buf.AsSpan(211), maxZ);
        BinaryPrimitives.WriteDoubleLittleEndian(buf.AsSpan(219), minZ);

        var off = headerSize;
        foreach (var (x, y, z, cls) in pts)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(off), (int)Math.Round(x / 0.001));
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(off + 4), (int)Math.Round(y / 0.001));
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(off + 8), (int)Math.Round(z / 0.001));
            buf[off + 15] = cls;
            off += recordLen;
        }
        return buf;
    }
}
