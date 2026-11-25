using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Platform;
using Lapin.PointClouds;
using Lapin.Host;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Gaussian splats (docs/plan-gaussian-splats.md, G1): the readers on tiny files written here after the formats' public
/// descriptions (a 3D Gaussian splatting PLY in INRIA's layout, Niantic's SPZ versions 2 and 3), the index taking a splat
/// file (its centres as points, its Gaussians beside them, a budget keeping the largest), and a Gaussian placed by its link:
/// a turned, scaled, stood-up link gives the covariance A Σ Aᵀ.
/// </summary>
public class SplatTests
{
    static SplatTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lapin-pointcloud-tests");
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("LAPIN_POINTCLOUD_CACHE", dir);
    }

    /// <summary>A Gaussian as a trainer writes it: centre, f_dc, opacity logit, log scales, quaternion w x y z, f_rest channel by channel.</summary>
    public sealed record G(double X, double Y, double Z, double[] Dc, double Opacity, double[] LogScale, double[] Rot, double[]? Rest = null);

    /// <summary>A binary little-endian 3DGS PLY: x y z nx ny nz f_dc_0..2 f_rest_* opacity scale_0..2 rot_0..3, all float.</summary>
    public static byte[] Ply(IReadOnlyList<G> gs, int rest = 0)
    {
        var head = new StringBuilder("ply\nformat binary_little_endian 1.0\ncomment written by Lapin's tests\nelement vertex " + gs.Count + "\n");
        foreach (var n in new[] { "x", "y", "z", "nx", "ny", "nz", "f_dc_0", "f_dc_1", "f_dc_2" }) head.Append($"property float {n}\n");
        for (var k = 0; k < rest; k++) head.Append($"property float f_rest_{k}\n");
        foreach (var n in new[] { "opacity", "scale_0", "scale_1", "scale_2", "rot_0", "rot_1", "rot_2", "rot_3" }) head.Append($"property float {n}\n");
        head.Append("end_header\n");
        var h = Encoding.ASCII.GetBytes(head.ToString());
        var stride = 4 * (17 + rest);
        var o = new byte[h.Length + gs.Count * stride];
        h.CopyTo(o, 0);
        for (var i = 0; i < gs.Count; i++)
        {
            var g = gs[i];
            var v = new List<double> { g.X, g.Y, g.Z, 0, 0, 0, g.Dc[0], g.Dc[1], g.Dc[2] };
            for (var k = 0; k < rest; k++) v.Add(g.Rest?[k] ?? 0);
            v.Add(g.Opacity); v.AddRange(g.LogScale); v.AddRange(g.Rot);
            for (var k = 0; k < v.Count; k++) BinaryPrimitives.WriteSingleLittleEndian(o.AsSpan(h.Length + i * stride + 4 * k), (float)v[k]);
        }
        return o;
    }

    static double Logit(double a) => Math.Log(a / (1 - a));

    static List<SplatBatch> Splats(ISplatReader r, bool sh = false) => r.ReadSplats(64, sh).Select(Copy).ToList();
    static SplatBatch Copy(SplatBatch b)
    {
        var c = new SplatBatch(b.Count, b.ShCoeffs);
        for (var i = 0; i < b.Count; i++) c.Add(b.X[i], b.Y[i], b.Z[i], b.S0[i], b.S1[i], b.S2[i], b.Qw[i], b.Qx[i], b.Qy[i], b.Qz[i], b.R[i], b.G[i], b.B[i], b.A[i]);
        b.Sh.AsSpan(0, b.Count * b.ShCoeffs * 3).CopyTo(c.Sh);
        return c;
    }

    [Fact]
    public void A_3dgs_ply_is_read_as_gaussians_its_centres_coloured_by_degree_zero_the_transparent_ones_left_out()
    {
        // Degree 1: three coefficients a channel, red's first in the file.
        double[] rest = [0.1, 0.2, 0.3, -0.1, -0.2, -0.3, 0.5, 0.25, -0.5];
        var gs = new List<G>
        {
            new(1, 2, 3, [0, 1, -1], Logit(0.8), [Math.Log(0.1), Math.Log(0.2), Math.Log(0.3)], [2, 0, 0, 0], rest),
            new(4, 5, 6, [5, -5, 0], Logit(0.001), [0, 0, 0], [1, 0, 0, 0], rest), // nearly transparent
            new(-1, 0, 7, [0.5, 0.5, 0.5], 4, [Math.Log(2), Math.Log(2), Math.Log(0.5)], [0, 0, 0, 3], rest),
        };
        var r = PointReaders.Open(new BytesSource("scene.ply", Ply(gs, 9)));
        var sr = Assert.IsType<GaussianPlyReader>(r);
        Assert.True(r.Header.Splats);
        Assert.Equal("-y", r.Header.Up);
        Assert.Equal(1, sr.ShDegree);
        Assert.Equal(3, r.Header.Count);
        // The points: two, coloured 0.5 + C0 · f_dc.
        var pts = r.Read(64).SelectMany(b => Enumerable.Range(0, b.Count).Select(i => (b.X[i], b.Y[i], b.Z[i], b.R[i], b.G[i], b.B[i]))).ToList();
        Assert.Equal(2, pts.Count);
        Assert.Equal((1d, 2d, 3d), (pts[0].Item1, pts[0].Item2, pts[0].Item3));
        Assert.Equal((ushort)128, pts[0].Item4);
        Assert.Equal((ushort)Math.Round((0.5 + Splat.C0) * 255), pts[0].Item5);
        Assert.Equal((ushort)Math.Round((0.5 - Splat.C0) * 255), pts[0].Item6);
        // The Gaussians: the same two, scales linear, rotations unit, opacity after the sigmoid, harmonics coefficient by coefficient.
        var s = Splats(sr, sh: true).Single();
        Assert.Equal(2, s.Count);
        Assert.Equal(0.1f, s.S0[0], 5); Assert.Equal(0.2f, s.S1[0], 5); Assert.Equal(0.3f, s.S2[0], 5);
        Assert.Equal((1f, 0f, 0f, 0f), (s.Qw[0], s.Qx[0], s.Qy[0], s.Qz[0]));
        Assert.Equal((0f, 0f, 0f, 1f), (s.Qw[1], s.Qx[1], s.Qy[1], s.Qz[1]));
        Assert.Equal((byte)Math.Round(0.8 * 255), s.A[0]);
        Assert.Equal((byte)Math.Round(Splat.Sigmoid(4) * 255), s.A[1]);
        // Coefficient 0: red 0.1, green −0.1, blue 0.5; coefficient 2: red 0.3, green −0.3, blue −0.5.
        Assert.Equal([Splat.ShByte(0.1), Splat.ShByte(-0.1), Splat.ShByte(0.5)], s.Sh[0..3]);
        Assert.Equal([Splat.ShByte(0.3), Splat.ShByte(-0.3), Splat.ShByte(-0.5)], s.Sh[6..9]);
        // A plain PLY of points stays a plain PLY.
        Assert.IsType<PlyReader>(PointReaders.Open(new BytesSource("p.ply", Encoding.ASCII.GetBytes("ply\nformat ascii 1.0\nelement vertex 1\nproperty float x\nproperty float y\nproperty float z\nend_header\n1 2 3\n"))));
    }

    /// <summary>An SPZ file (Niantic's layout, versions 1–3: gzip around a 16-byte header and the attributes one after another).</summary>
    public sealed record S(double X, double Y, double Z, byte Alpha, byte[] Rgb, byte[] Scale, double[] Quat /* x y z w */, byte[]? Sh = null);

    public static byte[] Spz(IReadOnlyList<S> gs, int version, int degree = 0, int fractional = 12)
    {
        var raw = new MemoryStream();
        var h = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(h, 0x5053474e);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(4), (uint)version);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(8), (uint)gs.Count);
        h[12] = (byte)degree; h[13] = (byte)fractional;
        raw.Write(h);
        foreach (var g in gs)
            foreach (var v in new[] { g.X, g.Y, g.Z })
            {
                var f = (int)Math.Round(v * (1 << fractional));
                raw.Write([(byte)(f & 0xff), (byte)((f >> 8) & 0xff), (byte)((f >> 16) & 0xff)]);
            }
        foreach (var g in gs) raw.WriteByte(g.Alpha);
        foreach (var g in gs) raw.Write(g.Rgb);
        foreach (var g in gs) raw.Write(g.Scale);
        foreach (var g in gs)
        {
            var q = g.Quat;
            var n = Math.Sqrt(q.Sum(c => c * c));
            var u = q.Select(c => c / n).ToArray();
            if (version >= 3)
            {
                // The smallest three: the largest's index in the top two bits, then the others' signs and magnitudes, 1 + 9 bits.
                var largest = Enumerable.Range(0, 4).MaxBy(i => Math.Abs(u[i]));
                if (u[largest] < 0) u = u.Select(c => -c).ToArray();
                uint comp = (uint)largest;
                for (var i = 0; i < 4; i++)
                {
                    if (i == largest) continue;
                    var mag = (uint)Math.Round(511 * Math.Abs(u[i]) / Math.Sqrt(0.5));
                    comp = (comp << 10) | ((u[i] < 0 ? 1u : 0u) << 9) | mag;
                }
                var b = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(b, comp);
                raw.Write(b);
            }
            else
            {
                if (u[3] < 0) u = u.Select(c => -c).ToArray();
                raw.Write([(byte)Math.Round(u[0] * 127.5 + 127.5), (byte)Math.Round(u[1] * 127.5 + 127.5), (byte)Math.Round(u[2] * 127.5 + 127.5)]);
            }
        }
        foreach (var g in gs) if (degree > 0) raw.Write(g.Sh!);
        var o = new MemoryStream();
        using (var z = new GZipStream(o, CompressionLevel.Fastest, leaveOpen: true)) raw.WriteTo(z);
        return o.ToArray();
    }

    static void Near(double want, double got, double tol, string what) => Assert.True(Math.Abs(want - got) <= tol, $"{what}: {got} is not {want}");

    /// <summary>The same turn whatever its sign.</summary>
    static void SameTurn(double[] wxyz, SplatBatch b, int i, double tol)
    {
        var dot = Math.Abs(wxyz[0] * b.Qw[i] + wxyz[1] * b.Qx[i] + wxyz[2] * b.Qy[i] + wxyz[3] * b.Qz[i]) / Math.Sqrt(wxyz.Sum(c => c * c));
        Assert.True(dot > 1 - tol, $"the turn is off ({dot})");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void An_spz_file_is_read_as_its_gaussians_positions_fixed_point_colours_scales_and_rotations_unpacked(int version)
    {
        var sh = Enumerable.Range(0, 9).Select(k => (byte)(100 + k)).ToArray();
        var gs = new List<S>
        {
            new(1.5, -2.25, 3.125, 204, [128, 200, 50], [160, 144, 176], [0, 0, Math.Sin(0.3), Math.Cos(0.3)], sh),
            new(0, 0, 0, 0, [0, 0, 0], [0, 0, 0], [0, 0, 0, 1], sh), // transparent
            new(-4, 0.5, -0.75, 255, [255, 0, 10], [120, 100, 80], [0.3, -0.5, 0.2, 0.6], sh),
        };
        var r = PointReaders.Open(new BytesSource("scene.spz", Spz(gs, version, degree: 1)));
        var sr = Assert.IsType<SpzReader>(r);
        Assert.Equal("y", r.Header.Up);
        Assert.Equal(1, sr.ShDegree);
        Assert.Contains($"SPZ version {version}", r.Header.Format);
        var b = Splats(sr, sh: true).Single();
        Assert.Equal(2, b.Count);
        Near(1.5, b.X[0], 1e-9, "x"); Near(-2.25, b.Y[0], 1e-9, "y"); Near(3.125, b.Z[0], 1e-9, "z");
        Near(-4, b.X[1], 1e-9, "x"); Near(0.5, b.Y[1], 1e-9, "y"); Near(-0.75, b.Z[1], 1e-9, "z");
        Assert.Equal((byte)204, b.A[0]);
        // Colour: f_dc = (b/255 − 0.5)/0.15, then 0.5 + C0 · f_dc.
        Assert.Equal(Splat.ColorOf((200 / 255.0 - 0.5) / 0.15), b.G[0]);
        // Scale: e^(b/16 − 10).
        Near(Math.Exp(160 / 16.0 - 10), b.S0[0], 1e-5, "scale");
        Near(Math.Exp(176 / 16.0 - 10), b.S2[0], 1e-5, "scale");
        SameTurn([Math.Cos(0.3), 0, 0, Math.Sin(0.3)], b, 0, version >= 3 ? 1e-5 : 1e-4);
        var q = new[] { 0.3, -0.5, 0.2, 0.6 };
        SameTurn([q[3], q[0], q[1], q[2]], b, 1, version >= 3 ? 1e-5 : 1e-4);
        Assert.Equal(sh, b.Sh[0..9]);
        Assert.Equal(sh, b.Sh[9..18]);
        // Its points, the same centres.
        Assert.Equal(2, r.Read().Sum(p => p.Count));
    }

    [Fact]
    public void A_broken_spz_version_4_and_a_gzip_that_is_no_spz_say_so()
    {
        // Version 4's header with a table of contents outside the file (G3 reads version 4: SplatFormatTests).
        var v4 = new byte[32];
        "NGSP"u8.CopyTo(v4);
        BinaryPrimitives.WriteUInt32LittleEndian(v4.AsSpan(4), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(v4.AsSpan(8), 10);
        v4[15] = 5;
        BinaryPrimitives.WriteUInt32LittleEndian(v4.AsSpan(16), 32);
        var r = PointReaders.Open(new BytesSource("a.spz", v4));
        Assert.Contains("table of contents", Assert.Throws<PointCloudException>(() => r.Read().ToList()).Message);
        var o = new MemoryStream();
        using (var z = new GZipStream(o, CompressionLevel.Fastest, leaveOpen: true)) z.Write(new byte[20]);
        Assert.Throws<PointCloudException>(() => PointReaders.Open(new BytesSource("b.spz", o.ToArray())));
    }

    static string TempDir() { var d = Path.Combine(Path.GetTempPath(), "lapin-splat-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact]
    public void The_index_takes_a_splat_file_its_centres_as_points_its_gaussians_beside_and_a_budget_keeps_the_largest()
    {
        // Ninety small Gaussians and ten large ones, the large ones scattered among them.
        var gs = Enumerable.Range(0, 100).Select(i => new G(i * 0.1, i % 7 * 0.2, i % 3 * 0.3, [0, 0, 0], 2,
            i % 10 == 3 ? [0.0, 0.0, 0.0] : [-5.0, -5.0, -5.0], [1, 0, 0, 0])).ToList();
        var dir = TempDir();
        var s = Indexer.Build(PointReaders.Open(new BytesSource("s.ply", Ply(gs))), dir, "k", 1);
        Assert.Equal(100, s.Points);
        var splats = SplatStore.Open(dir)!;
        Assert.Equal(100, splats.Count);
        Assert.Equal(100 * SplatStore.BytesPerSplat, new FileInfo(Path.Combine(dir, "splats.bin")).Length);
        Assert.False(File.Exists(Path.Combine(dir, "sh.bin")));
        var (idx, bytes) = splats.Read(0, 1000, 10);
        Assert.Equal(Enumerable.Range(0, 10).Select(k => (long)(k * 10 + 3)), idx);
        var g = SplatStore.Unpack(bytes.AsSpan(13 * SplatStore.BytesPerSplat));
        Assert.Equal(1.3f, g.X, 5);
        Assert.Equal(1f, g.S0, 5);
        // A budget of 15: the ten large, then the first five small ones.
        Assert.Equal(new long[] { 0, 1, 2, 3, 4, 5, 13 }, splats.Read(0, 1000, 15).Index.Take(7));
        // Paged, the same choice.
        var paged = splats.Read(0, 40, 15).Index.Concat(splats.Read(40, 60, 15).Index).ToList();
        Assert.Equal(splats.Read(0, 1000, 15).Index, paged);
        Assert.Equal(15, splats.Cut(15).Kept);
        Assert.Equal(100, splats.Cut(1000).Kept);
    }

    static Fixture F() => new(new ArchitectureModule());

    static (Fixture F, string Id) Linked(byte[] bytes, string name, JsonObject? placement = null)
    {
        var f = F();
        var r = (JsonObject)f.Run("pointcloud.link", new JsonObject { ["name"] = name, ["data"] = Convert.ToBase64String(bytes), ["placement"] = placement })!;
        var id = (string)r["id"]!;
        PointCloudSamples.WaitIndexed(f, id);
        f.Session.TakeDeliveries();
        return (f, id);
    }

    /// <summary>R · diag(s²) · Rᵀ of a unit quaternion w x y z.</summary>
    static double[,] Covariance(double w, double x, double y, double z, double s0, double s1, double s2)
    {
        double[,] r =
        {
            { 1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y) },
            { 2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x) },
            { 2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y) },
        };
        var s = new[] { s0 * s0, s1 * s1, s2 * s2 };
        var o = new double[3, 3];
        for (var i = 0; i < 3; i++) for (var j = 0; j < 3; j++) for (var k = 0; k < 3; k++) o[i, j] += r[i, k] * s[k] * r[j, k];
        return o;
    }

    [Fact]
    public void A_gaussian_is_placed_by_its_link_a_turned_scaled_stood_up_link_giving_the_covariance_expected()
    {
        // A quaternion a byte represents exactly (120° about 1, 1, 1), scales 10, 20, 30 cm in centimetres.
        var gs = new List<G>
        {
            new(100, -50, 20, [0, 0, 0], 3, [Math.Log(10), Math.Log(20), Math.Log(30)], [0.5, 0.5, 0.5, 0.5]),
            new(0, 0, 0, [0, 0, 0], 3, [0, 0, 0], [1, 0, 0, 0]),
            new(200, -300, 100, [0, 0, 0], 3, [0, 0, 0], [1, 0, 0, 0]),
        };
        var (f, id) = Linked(Ply(gs), "room.ply", new JsonObject { ["mode"] = "origin", ["units"] = "cm" });
        var link = Links.Require(f.Model, id);
        Assert.True(PointCloudLinks.IsSplats(link));
        Assert.Equal("splats", link.Str("display"));
        Assert.Equal("-y", (string?)link.Attrs["placement"]!["up"]);
        f.Run("pointcloud.place", new JsonObject { ["id"] = id, ["rotation"] = 30, ["offset"] = new JsonArray(1, 2, 3) });
        link = Links.Require(f.Model, id);
        var page = PointCloudLinks.Splats(f.Model, link, 0, 1000);
        Assert.Equal(3, page.Total);
        Assert.Null(page.Next);
        Assert.Equal(3, page.Colors.Length);
        // By hand: the file stood up (−y up: x stays, y goes down, z goes forward), in metres, turned 30°, moved.
        double c = Math.Cos(Math.PI / 6), sn = Math.Sin(Math.PI / 6), k = 0.01;
        double[,] rz = { { c, -sn, 0 }, { sn, c, 0 }, { 0, 0, 1 } };
        double[,] up = { { 1, 0, 0 }, { 0, 0, 1 }, { 0, -1, 0 } };
        var a = new double[3, 3];
        for (var i = 0; i < 3; i++) for (var j = 0; j < 3; j++) for (var m = 0; m < 3; m++) a[i, j] += k * rz[i, m] * up[m, j];
        // The centre: A · p + offset.
        double[] p = [100, -50, 20];
        for (var i = 0; i < 3; i++) Near(a[i, 0] * p[0] + a[i, 1] * p[1] + a[i, 2] * p[2] + (i + 1), page.Positions[i], 1e-4, $"centre {i}");
        // The covariance: A Σ Aᵀ.
        var sigma = Covariance(0.5, 0.5, 0.5, 0.5, 10, 20, 30);
        var want = new double[3, 3];
        for (var i = 0; i < 3; i++) for (var j = 0; j < 3; j++) for (var m = 0; m < 3; m++) for (var n = 0; n < 3; n++) want[i, j] += a[i, m] * sigma[m, n] * a[j, n];
        var got = Covariance(page.Rotations[0], page.Rotations[1], page.Rotations[2], page.Rotations[3], page.Scales[0], page.Scales[1], page.Scales[2]);
        for (var i = 0; i < 3; i++) for (var j = 0; j < 3; j++) Near(want[i, j], got[i, j], 1e-6, $"Σ[{i},{j}]");
        Near(0.1, page.Scales[0], 1e-6, "scale in metres");
        // The point index stands up the same way: the file's −y is the project's up.
        var h = (JsonObject)f.Session.Query("pointcloud.hierarchy", new JsonObject { ["id"] = id })!;
        var box = h["box"]!.AsArray().Select(v => (double)v!).ToArray();
        Near(3 + 3, box[5], 1e-3, "the top of the box");
    }

    [Fact]
    public void A_splat_link_is_drawn_as_splats_or_points_by_itself_and_by_a_view_and_its_page_crosses_the_protocol_as_raw_arrays()
    {
        var gs = Enumerable.Range(0, 5).Select(i => new G(i, 0, 0, [0, 0, 0], 2, [-2, -2, -2], [1, 0, 0, 0])).ToList();
        var (f, id) = Linked(Ply(gs), "s.ply");
        f.Run("pointcloud.update", new JsonObject { ["id"] = id, ["display"] = "points" });
        Assert.Equal("points", Links.Require(f.Model, id).Str("display"));
        Assert.Throws<CommandException>(() => f.Run("pointcloud.update", new JsonObject { ["id"] = id, ["display"] = "voxels" }));
        Assert.Equal("splats", PointCloudLinks.CheckViewRow(new JsonObject { ["display"] = "splats" })!["display"]!.GetValue<string>());
        Assert.Throws<CommandException>(() => PointCloudLinks.CheckViewRow(new JsonObject { ["display"] = "mesh" }));
        var look = PointCloudLinks.LookIn(new JsonObject { ["pointClouds"] = new JsonObject { [id] = new JsonObject { ["display"] = "splats" } } }, Links.Require(f.Model, id));
        Assert.Equal("splats", look.Display);
        Assert.Equal("points", PointCloudLinks.LookIn(null, Links.Require(f.Model, id)).Display);
        Assert.Throws<CommandException>(() => f.Run("pointcloud.place", new JsonObject { ["id"] = id, ["up"] = "x" }));
        // Through the dispatcher, as the 3D view asks: one frame, its arrays as buffers, written alike by the contracts alone.
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var frame = d.Handle(new JsonObject { ["id"] = 1, ["method"] = "pointcloud.splats", ["params"] = new JsonObject { ["id"] = id, ["start"] = 0, ["budget"] = 3 } }.ToJsonString()).Reply;
        var (header, _) = BinaryFrame.Decode(frame);
        var result = header.RootElement.GetProperty("result");
        Assert.Equal(5, result.GetProperty("total").GetInt64());
        Assert.Equal(3, result.GetProperty("kept").GetInt64());
        Assert.Equal("f32", result.GetProperty("positions").GetProperty("t").GetString());
        Assert.Equal(9, result.GetProperty("positions").GetProperty("n").GetInt32());
        Assert.Equal("i32", result.GetProperty("colors").GetProperty("t").GetString());
        Assert.Equal(JsonValueKind.Null, result.TryGetProperty("next", out var next) ? next.ValueKind : JsonValueKind.Null);
        var page = PointCloudLinks.Splats(f.Model, Links.Require(f.Model, id), 0, 3);
        Assert.Equal(BinaryFrame.Encode(new Reply(1, page)), BinaryFrame.Encode(new Reply(1, page), contractsOnly: true));
        // A plain cloud holds no splats.
        var las = (string)((JsonObject)f.Run("pointcloud.link", new JsonObject { ["name"] = "r.las", ["data"] = Convert.ToBase64String(PointCloudSamples.Las(PointCloudSamples.Room(5))) })!)["id"]!;
        Assert.Contains("Gaussian", Assert.Throws<CommandException>(() => f.Run("pointcloud.update", new JsonObject { ["id"] = las, ["display"] = "splats" })).Message);
    }
}
