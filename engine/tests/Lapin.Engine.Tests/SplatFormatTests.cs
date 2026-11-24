using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Platform;
using Lapin.PointClouds;
using Lapin.Host;
using Lapin.Protocol;
using G = Lapin.Engine.Tests.SplatTests.G;

namespace Lapin.Engine.Tests;

/// <summary>
/// G3 of docs/plan-gaussian-splats.md: the other splat formats and the colour by direction. One synthetic capture (Gaussians
/// of every size and turn, harmonics of degree 3, more than one chunk of 256) is written here as a 3DGS PLY and, after each
/// format's public description, as SOG (versions 2 and 3, zipped and as a folder; its WebP images made by a tiny lossless
/// encoder written for the tests), SuperSplat's compressed PLY and SPZ version 4 (Zstandard frames: raw ones written here, and
/// the reference zstd's where it is on this computer); each reader gives back the same Gaussians as the PLY's within its
/// format's quantization. Then the harmonics: the basis (Kerbl et al.'s), a colour seen from two sides, and a link's page
/// carrying them with the turn from the project's directions to the file's.
/// </summary>
public class SplatFormatTests
{
    static SplatFormatTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lapin-pointcloud-tests");
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("LAPIN_POINTCLOUD_CACHE", dir);
    }

    const int N = 600;
    const int Rest = 45;

    /// <summary>The capture: positions over ±6 (some near 0), sizes from 1 mm to 1 m, any turn, colours and harmonics in range.</summary>
    internal static List<G> Capture(int n = N, int seed = 5)
    {
        var r = new Random(seed);
        double U(double a, double b) => a + (b - a) * r.NextDouble();
        var gs = new List<G>();
        for (var i = 0; i < n; i++)
        {
            double[] q = [U(-1, 1), U(-1, 1), U(-1, 1), U(-1, 1)];
            var rest = Enumerable.Range(0, Rest).Select(_ => U(-0.6, 0.6)).ToArray();
            gs.Add(new G(U(-6, 6), U(-3, 3) * (i % 5 == 0 ? 0.01 : 1), U(-6, 6), [U(-1.5, 1.5), U(-1.5, 1.5), U(-1.5, 1.5)], U(-2, 5),
                [U(Math.Log(0.001), 0), U(Math.Log(0.001), 0), U(Math.Log(0.001), 0)], q, rest));
        }
        return gs;
    }

    static double Sigmoid(double v) => 1 / (1 + Math.Exp(-v));
    static double Signed(double v) => Math.Sign(v) * Math.Log(Math.Abs(v) + 1);

    static double[] Unit(double[] q) { var n = Math.Sqrt(q.Sum(c => c * c)); return q.Select(c => c / n).ToArray(); }

    /// <summary>The Gaussians a reader hands over, all together.</summary>
    static SplatBatch All(ISplatReader r)
    {
        var all = new SplatBatch(r.Header.Count is { } c ? (int)c : N, Splat.ShCoeffs(r.ShDegree));
        foreach (var b in r.ReadSplats(128, sh: true))
            for (var i = 0; i < b.Count; i++)
            {
                var k = all.Add(b.X[i], b.Y[i], b.Z[i], b.S0[i], b.S1[i], b.S2[i], b.Qw[i], b.Qx[i], b.Qy[i], b.Qz[i], b.R[i], b.G[i], b.B[i], b.A[i]);
                b.Sh.AsSpan(i * b.ShCoeffs * 3, b.ShCoeffs * 3).CopyTo(all.Sh.AsSpan(k * all.ShCoeffs * 3));
            }
        return all;
    }

    /// <summary>The same Gaussians as the PLY's within tolerances: positions (m), relative sizes, the turn (1 − |q·q'|), colour and opacity bytes, harmonic bytes.</summary>
    static void Same(SplatBatch want, SplatBatch got, double pos, double size, double turn, int colour, int alpha, int sh, string what)
    {
        Assert.True(want.Count == got.Count, $"{what}: {got.Count} Gaussians, not {want.Count}");
        for (var i = 0; i < want.Count; i++)
        {
            Assert.True(Math.Abs(want.X[i] - got.X[i]) <= pos && Math.Abs(want.Y[i] - got.Y[i]) <= pos && Math.Abs(want.Z[i] - got.Z[i]) <= pos,
                $"{what} #{i}: at ({got.X[i]}, {got.Y[i]}, {got.Z[i]}), not ({want.X[i]}, {want.Y[i]}, {want.Z[i]})");
            foreach (var (a, b) in new[] { (want.S0[i], got.S0[i]), (want.S1[i], got.S1[i]), (want.S2[i], got.S2[i]) })
                Assert.True(Math.Abs(Math.Log(b / a)) <= size, $"{what} #{i}: a size of {b}, not {a}");
            var dot = Math.Abs(want.Qw[i] * got.Qw[i] + want.Qx[i] * got.Qx[i] + want.Qy[i] * got.Qy[i] + want.Qz[i] * got.Qz[i]);
            Assert.True(1 - dot <= turn, $"{what} #{i}: turned off by {1 - dot}");
            Assert.True(Math.Abs(want.R[i] - got.R[i]) <= colour && Math.Abs(want.G[i] - got.G[i]) <= colour && Math.Abs(want.B[i] - got.B[i]) <= colour,
                $"{what} #{i}: colour {got.R[i]},{got.G[i]},{got.B[i]}, not {want.R[i]},{want.G[i]},{want.B[i]}");
            Assert.True(Math.Abs(want.A[i] - got.A[i]) <= alpha, $"{what} #{i}: opacity {got.A[i]}, not {want.A[i]}");
            var n = Math.Min(want.ShCoeffs, got.ShCoeffs) * 3;
            for (var k = 0; k < n; k++)
                Assert.True(Math.Abs(want.Sh[i * want.ShCoeffs * 3 + k] - got.Sh[i * got.ShCoeffs * 3 + k]) <= sh,
                    $"{what} #{i}: harmonic {k} is {got.Sh[i * got.ShCoeffs * 3 + k]}, not {want.Sh[i * want.ShCoeffs * 3 + k]}");
        }
    }

    static SplatBatch Reference(List<G> gs)
    {
        var r = Assert.IsType<GaussianPlyReader>(PointReaders.Open(new BytesSource("ref.ply", SplatTests.Ply(gs, Rest))));
        Assert.Equal(3, r.ShDegree);
        return All(r);
    }

    // ─── A WebP lossless encoder for the tests: no transforms, every channel's 256 values 8 bits long ───

    sealed class BitWriter
    {
        readonly List<byte> _o = [];
        ulong _acc;
        int _n;
        public void Write(uint v, int bits)
        {
            _acc |= (ulong)v << _n;
            _n += bits;
            while (_n >= 8) { _o.Add((byte)_acc); _acc >>= 8; _n -= 8; }
        }
        public byte[] Done() { if (_n > 0) _o.Add((byte)_acc); return [.. _o]; }
    }

    /// <summary>A VP8L image of ARGB pixels: each literal a code of 8 bits (its value, read high bit first), nothing else used.</summary>
    internal static byte[] WebP(int w, int h, uint[] argb)
    {
        var bw = new BitWriter();
        bw.Write(0x2f, 8);
        bw.Write((uint)(w - 1), 14); bw.Write((uint)(h - 1), 14); bw.Write(1, 1); bw.Write(0, 3);
        bw.Write(0, 1); // no transform
        bw.Write(0, 1); // no colour cache
        bw.Write(0, 1); // one prefix code group
        // Green (280 symbols), red, blue, alpha (256): a normal code; the code length code has two symbols, 0 and 8, one bit each.
        void Literals(int alphabet)
        {
            bw.Write(0, 1);
            bw.Write(12 - 4, 4); // the first twelve code length codes, down to 8 (17, 18, 0, 1, 2, 3, 4, 5, 16, 6, 7, 8)
            foreach (var sym in new[] { 17, 18, 0, 1, 2, 3, 4, 5, 16, 6, 7, 8 }) bw.Write(sym is 0 or 8 ? 1u : 0u, 3);
            bw.Write(0, 1); // max_symbol: the whole alphabet
            for (var s = 0; s < alphabet; s++) bw.Write(s < 256 ? 1u : 0u, 1); // code 1 → length 8, code 0 → length 0
        }
        Literals(280); Literals(256); Literals(256); Literals(256);
        // Distance: a simple code of one symbol (0).
        bw.Write(1, 1); bw.Write(0, 1); bw.Write(0, 1); bw.Write(0, 1);
        static uint Rev(uint v) { uint r = 0; for (var i = 0; i < 8; i++) { r = r << 1 | v & 1; v >>= 1; } return r; }
        foreach (var p in argb)
        {
            bw.Write(Rev(p >> 8 & 0xff), 8);
            bw.Write(Rev(p >> 16 & 0xff), 8);
            bw.Write(Rev(p & 0xff), 8);
            bw.Write(Rev(p >> 24), 8);
        }
        var data = bw.Done();
        var o = new MemoryStream();
        var pad = data.Length & 1;
        o.Write("RIFF"u8); o.Write(BitConverter.GetBytes(4 + 8 + data.Length + pad)); o.Write("WEBP"u8);
        o.Write("VP8L"u8); o.Write(BitConverter.GetBytes(data.Length)); o.Write(data);
        if (pad == 1) o.WriteByte(0);
        return o.ToArray();
    }

    [Fact]
    public void The_tests_webp_encoder_round_trips_through_lapins_decoder()
    {
        var px = Enumerable.Range(0, 7 * 5).Select(i => (uint)(i * 0x01030507 ^ 0x80402010)).ToArray();
        var im = WebPLossless.Decode(WebP(7, 5, px));
        Assert.Equal(px, im.Argb);
    }

    // ─── SOG ───

    static uint Px(int r, int g, int b, int a = 255) => (uint)(a << 24 | r << 16 | g << 8 | b);
    static int Q8(double t) => (int)Math.Clamp(Math.Round(t * 255), 0, 255);

    /// <summary>A codebook of 256 values evenly over [lo, hi], and a value's nearest index.</summary>
    static (float[] Book, Func<double, int> Index) Codebook(double lo, double hi)
    {
        var book = Enumerable.Range(0, 256).Select(k => (float)(lo + (hi - lo) * k / 255)).ToArray();
        return (book, v => (int)Math.Clamp(Math.Round((v - lo) / (hi - lo) * 255), 0, 255));
    }

    /// <summary>A SOG of the capture after the format's specification, version 2 or 3: its files by name.</summary>
    internal static Dictionary<string, byte[]> Sog(List<G> gs, int version, int width = 32)
    {
        var n = gs.Count;
        var height = (n + width - 1) / width;
        uint[] Img() => new uint[width * height];
        var (lo, hi, scl, qts, c0, lab) = (Img(), Img(), Img(), Img(), Img(), Img());
        var mins = new double[3];
        var maxs = new double[3];
        for (var k = 0; k < 3; k++)
        {
            mins[k] = gs.Min(g => Signed(k == 0 ? g.X : k == 1 ? g.Y : g.Z));
            maxs[k] = gs.Max(g => Signed(k == 0 ? g.X : k == 1 ? g.Y : g.Z));
        }
        var (sBook, sIdx) = Codebook(gs.Min(g => g.LogScale.Min()), gs.Max(g => g.LogScale.Max()));
        var (cBook, cIdx) = Codebook(-1.6, 1.6);
        var (hBook, hIdx) = Codebook(-0.6, 0.6);
        var coeffs = Rest / 3;
        var palette = new uint[64 * coeffs * ((n + 63) / 64)];
        for (var i = 0; i < n; i++)
        {
            var g = gs[i];
            var q16 = new int[3];
            for (var k = 0; k < 3; k++) q16[k] = (int)Math.Round((Signed(k == 0 ? g.X : k == 1 ? g.Y : g.Z) - mins[k]) / (maxs[k] - mins[k]) * 65535);
            lo[i] = Px(q16[0] & 0xff, q16[1] & 0xff, q16[2] & 0xff);
            hi[i] = Px(q16[0] >> 8, q16[1] >> 8, q16[2] >> 8);
            scl[i] = Px(sIdx(g.LogScale[0]), sIdx(g.LogScale[1]), sIdx(g.LogScale[2]));
            var q = Unit(g.Rot); // w x y z
            if (version >= 3)
            {
                if (q[0] < 0) q = q.Select(c => -c).ToArray();
                var v = Math.Sqrt(q[1] * q[1] + q[2] * q[2] + q[3] * q[3]);
                var kk = v > 0 ? Math.Atan2(v, q[0]) / v / (Math.PI / 2) : 2 / Math.PI;
                int B(double c) => (int)Math.Clamp(Math.Round(c * kk * 127 + 128), 0, 255);
                qts[i] = Px(B(q[1]), B(q[2]), B(q[3]));
            }
            else
            {
                var largest = Enumerable.Range(0, 4).MaxBy(k => Math.Abs(q[k]));
                if (q[largest] < 0) q = q.Select(c => -c).ToArray();
                var kept = Enumerable.Range(0, 4).Where(k => k != largest).Select(k => Q8(q[k] * Math.Sqrt(2) / 2 + 0.5)).ToArray();
                qts[i] = Px(kept[0], kept[1], kept[2], 252 + largest);
            }
            var alpha = Sigmoid(g.Opacity);
            var a = version >= 3 ? (int)Math.Min(255, Math.Round(204 * Math.Sqrt(alpha))) : Q8(alpha);
            c0[i] = Px(cIdx(g.Dc[0]), cIdx(g.Dc[1]), cIdx(g.Dc[2]), a);
            // Each Gaussian its own palette entry; the labels 24 bits (version 3) or 16 (version 2).
            lab[i] = version >= 3 ? Px(i & 0xff, i >> 8 & 0xff, i >> 16 & 0xff) : Px(i & 0xff, i >> 8 & 0xff, 0);
            var row = i / 64 * 64 * coeffs + i % 64 * coeffs;
            for (var j = 0; j < coeffs; j++) palette[row + j] = Px(hIdx(g.Rest![j]), hIdx(g.Rest[coeffs + j]), hIdx(g.Rest[2 * coeffs + j]));
        }
        JsonArray Arr(IEnumerable<double> v) => new(v.Select(x => (JsonNode)x).ToArray());
        JsonArray Files(params string[] f) => new(f.Select(x => (JsonNode)x).ToArray());
        var meta = new JsonObject
        {
            ["version"] = version, ["asset"] = new JsonObject { ["generator"] = "Lapin's tests" }, ["count"] = n,
            ["means"] = new JsonObject { ["mins"] = Arr(mins), ["maxs"] = Arr(maxs), ["files"] = Files("means_l.webp", "means_u.webp") },
            ["scales"] = new JsonObject { ["codebook"] = Arr(sBook.Select(v => (double)v)), ["files"] = Files("scales.webp") },
            ["quats"] = new JsonObject { ["files"] = Files("quats.webp") },
            ["sh0"] = new JsonObject { ["codebook"] = Arr(cBook.Select(v => (double)v)), ["files"] = Files("sh0.webp") },
            ["shN"] = new JsonObject { ["count"] = n, ["bands"] = 3, ["codebook"] = Arr(hBook.Select(v => (double)v)), ["files"] = Files("shN_centroids.webp", "shN_labels.webp") },
        };
        return new Dictionary<string, byte[]>
        {
            ["meta.json"] = Encoding.UTF8.GetBytes(meta.ToJsonString()),
            ["means_l.webp"] = WebP(width, height, lo), ["means_u.webp"] = WebP(width, height, hi), ["scales.webp"] = WebP(width, height, scl),
            ["quats.webp"] = WebP(width, height, qts), ["sh0.webp"] = WebP(width, height, c0), ["shN_labels.webp"] = WebP(width, height, lab),
            ["shN_centroids.webp"] = WebP(64 * coeffs, (n + 63) / 64, palette),
        };
    }

    internal static byte[] Zip(Dictionary<string, byte[]> files)
    {
        var o = new MemoryStream();
        using (var z = new ZipArchive(o, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, bytes) in files)
            {
                using var e = z.CreateEntry(name, CompressionLevel.NoCompression).Open();
                e.Write(bytes);
            }
        return o.ToArray();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(2)]
    public void A_sog_zipped_or_as_a_folder_gives_back_the_plys_gaussians_within_its_quantization(int version)
    {
        var gs = Capture();
        var want = Reference(gs);
        var files = Sog(gs, version);
        var r = PointReaders.Open(new BytesSource("scene.sog", Zip(files)));
        var sog = Assert.IsType<SogReader>(r);
        Assert.Equal("-y", r.Header.Up);
        Assert.Equal(3, sog.ShDegree);
        Assert.Equal(3, r.Header.ShDegree);
        Assert.Contains($"SOG version {version}", r.Header.Format);
        // Positions: 16 bits over the log range (the far ones coarser); sizes and colours: 256 levels; turns: version 3's
        // exponential map about 1.2° at worst, version 2's 8-bit smallest three; harmonics: a codebook step of 1.2/255.
        Same(want, All(sog), pos: 3e-3, size: 0.03, turn: version >= 3 ? 3e-4 : 1e-3, colour: 1, alpha: version >= 3 ? 2 : 1, sh: 1, $"SOG {version}");
        // The points: the same centres.
        Assert.Equal(want.Count, r.Read().Sum(b => b.Count));
        // As a folder: its meta.json beside the images.
        var dir = Directory.CreateTempSubdirectory("lapin-sog-");
        try
        {
            foreach (var (name, bytes) in files) File.WriteAllBytes(Path.Combine(dir.FullName, name), bytes);
            var fr = Assert.IsType<SogReader>(PointReaders.Open(new FileSource(Path.Combine(dir.FullName, "meta.json"))));
            Same(want, All(fr), pos: 3e-3, size: 0.03, turn: version >= 3 ? 3e-4 : 1e-3, colour: 1, alpha: version >= 3 ? 2 : 1, sh: 1, "SOG folder");
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void A_sog_of_the_first_layout_a_lossy_image_or_a_missing_file_says_so()
    {
        var gs = Capture(40);
        var files = Sog(gs, 3);
        var old = new Dictionary<string, byte[]>(files) { ["meta.json"] = Encoding.UTF8.GetBytes("""{"means":{"shape":[40,3]}}""") };
        Assert.Contains("first layout", Assert.Throws<PointCloudException>(() => PointReaders.Open(new BytesSource("a.sog", Zip(old)))).Message);
        var lossy = new Dictionary<string, byte[]>(files) { ["quats.webp"] = [.. "RIFF\x0c\0\0\0WEBPVP8 \0\0\0\0"u8] };
        var r = PointReaders.Open(new BytesSource("b.sog", Zip(lossy)));
        Assert.Contains("lossy", Assert.Throws<PointCloudException>(() => r.Read().ToList()).Message);
        var missing = new Dictionary<string, byte[]>(files);
        missing.Remove("scales.webp");
        Assert.Contains("scales.webp is missing", Assert.Throws<PointCloudException>(() => PointReaders.Open(new BytesSource("c.sog", Zip(missing))).Read().ToList()).Message);
        Assert.Contains("desktop", Assert.Throws<PointCloudException>(() => PointReaders.Open(new BytesSource("meta.json", files["meta.json"]))).Message);
    }

    // ─── SuperSplat's compressed PLY ───

    /// <summary>The capture as a compressed PLY (its chunks with colour bounds, packed vertices, an sh element of bytes).</summary>
    internal static byte[] CompressedPly(List<G> gs)
    {
        var n = gs.Count;
        var chunks = (n + 255) / 256;
        var head = new StringBuilder("ply\nformat binary_little_endian 1.0\ncomment written by Lapin's tests\n");
        head.Append($"element chunk {chunks}\n");
        foreach (var p in new[] { "min_x", "min_y", "min_z", "max_x", "max_y", "max_z", "min_scale_x", "min_scale_y", "min_scale_z", "max_scale_x", "max_scale_y", "max_scale_z", "min_r", "min_g", "min_b", "max_r", "max_g", "max_b" })
            head.Append($"property float {p}\n");
        head.Append($"element vertex {n}\nproperty uint packed_position\nproperty uint packed_rotation\nproperty uint packed_scale\nproperty uint packed_color\n");
        head.Append($"element sh {n}\n");
        for (var k = 0; k < Rest; k++) head.Append($"property uchar f_rest_{k}\n");
        head.Append("end_header\n");
        var o = new MemoryStream();
        o.Write(Encoding.ASCII.GetBytes(head.ToString()));
        var bounds = new float[chunks, 18];
        double Colour(G g, int c) => Math.Clamp(0.5 + Splat.C0 * g.Dc[c], 0, 1);
        for (var c = 0; c < chunks; c++)
        {
            var part = gs.Skip(c * 256).Take(256).ToList();
            Func<G, double>[] f = [g => g.X, g => g.Y, g => g.Z, g => g.LogScale[0], g => g.LogScale[1], g => g.LogScale[2], g => Colour(g, 0), g => Colour(g, 1), g => Colour(g, 2)];
            for (var k = 0; k < 3; k++) { bounds[c, k] = (float)part.Min(f[k]); bounds[c, 3 + k] = (float)part.Max(f[k]); }
            for (var k = 0; k < 3; k++) { bounds[c, 6 + k] = (float)part.Min(f[3 + k]); bounds[c, 9 + k] = (float)part.Max(f[3 + k]); }
            for (var k = 0; k < 3; k++) { bounds[c, 12 + k] = (float)part.Min(f[6 + k]); bounds[c, 15 + k] = (float)part.Max(f[6 + k]); }
            for (var k = 0; k < 18; k++) o.Write(BitConverter.GetBytes(bounds[c, k]));
        }
        static uint Norm(double v, double lo, double hi, int bits) => (uint)Math.Clamp(Math.Round((hi > lo ? (v - lo) / (hi - lo) : 0) * ((1 << bits) - 1)), 0, (1 << bits) - 1);
        for (var i = 0; i < n; i++)
        {
            var g = gs[i];
            var c = i / 256;
            var pp = Norm(g.X, bounds[c, 0], bounds[c, 3], 11) << 21 | Norm(g.Y, bounds[c, 1], bounds[c, 4], 10) << 11 | Norm(g.Z, bounds[c, 2], bounds[c, 5], 11);
            var ps = Norm(g.LogScale[0], bounds[c, 6], bounds[c, 9], 11) << 21 | Norm(g.LogScale[1], bounds[c, 7], bounds[c, 10], 10) << 11 | Norm(g.LogScale[2], bounds[c, 8], bounds[c, 11], 11);
            var q = Unit(g.Rot);
            var largest = Enumerable.Range(0, 4).MaxBy(k => Math.Abs(q[k]));
            if (q[largest] < 0) q = q.Select(v => -v).ToArray();
            uint pr = (uint)largest;
            foreach (var k in Enumerable.Range(0, 4).Where(k => k != largest)) pr = pr << 10 | Norm(q[k] / Math.Sqrt(2) + 0.5, 0, 1, 10);
            var pc = Norm(Colour(g, 0), bounds[c, 12], bounds[c, 15], 8) << 24 | Norm(Colour(g, 1), bounds[c, 13], bounds[c, 16], 8) << 16
                | Norm(Colour(g, 2), bounds[c, 14], bounds[c, 17], 8) << 8 | Norm(Sigmoid(g.Opacity), 0, 1, 8);
            foreach (var v in new[] { pp, pr, ps, pc }) o.Write(BitConverter.GetBytes(v));
        }
        for (var i = 0; i < n; i++)
            for (var k = 0; k < Rest; k++)
                o.WriteByte((byte)Math.Clamp(Math.Round((gs[i].Rest![k] / 8 + 0.5) * 256 - 0.5), 0, 255));
        return o.ToArray();
    }

    [Fact]
    public void A_compressed_ply_gives_back_the_plys_gaussians_within_its_quantization()
    {
        var gs = Capture();
        var want = Reference(gs);
        var r = PointReaders.Open(new BytesSource("scene.compressed.ply", CompressedPly(gs)));
        var c = Assert.IsType<CompressedPlyReader>(r);
        Assert.Equal("-y", r.Header.Up);
        Assert.Equal(3, c.ShDegree);
        Assert.NotNull(r.Header.Bounds);
        // Positions: 10–11 bits over a chunk's box (12 m / 1023); sizes 10–11 bits of the logs; turns 10 bits; colours a byte
        // over the chunk's range; harmonics a byte over ±4, about four of ours.
        Same(want, All(c), pos: 12.0 / 1023, size: 7.0 / 1023, turn: 2e-5 * 100, colour: 1, alpha: 1, sh: 3, "compressed PLY");
    }

    // ─── SPZ version 4 ───

    /// <summary>A Zstandard frame of raw blocks (128 KB at most each), its content size in the header.</summary>
    internal static byte[] RawFrame(byte[] data)
    {
        var o = new MemoryStream();
        o.Write([0x28, 0xB5, 0x2F, 0xFD, 0xA0 /* 4-byte content size, single segment */]);
        o.Write(BitConverter.GetBytes(data.Length));
        var at = 0;
        do
        {
            var n = Math.Min(128 * 1024, data.Length - at);
            var last = at + n >= data.Length;
            var bh = (last ? 1 : 0) | 0 << 1 | n << 3;
            o.Write([(byte)bh, (byte)(bh >> 8), (byte)(bh >> 16)]);
            o.Write(data, at, n);
            at += n;
        } while (at < data.Length);
        return o.ToArray();
    }

    /// <summary>The capture as SPZ version 4, each attribute a stream of its own, compressed by <paramref name="compress"/>.</summary>
    internal static byte[] Spz4(List<G> gs, Func<byte[], byte[]> compress, int fractional = 12, bool extension = false)
    {
        var n = gs.Count;
        var coeffs = Rest / 3;
        var streams = new List<byte[]>();
        var pos = new byte[9 * n];
        var alpha = new byte[n];
        var colour = new byte[3 * n];
        var scale = new byte[3 * n];
        var rot = new byte[4 * n];
        var sh = new byte[n * coeffs * 3];
        for (var i = 0; i < n; i++)
        {
            var g = gs[i];
            var xyz = new[] { g.X, g.Y, g.Z };
            for (var k = 0; k < 3; k++)
            {
                var f = (int)Math.Round(xyz[k] * (1 << fractional));
                pos[9 * i + 3 * k] = (byte)f; pos[9 * i + 3 * k + 1] = (byte)(f >> 8); pos[9 * i + 3 * k + 2] = (byte)(f >> 16);
            }
            alpha[i] = (byte)Math.Clamp(Math.Round(Sigmoid(g.Opacity) * 255), 0, 255);
            for (var k = 0; k < 3; k++)
            {
                colour[3 * i + k] = (byte)Math.Clamp(Math.Round((g.Dc[k] * 0.15 + 0.5) * 255), 0, 255);
                scale[3 * i + k] = (byte)Math.Clamp(Math.Round((g.LogScale[k] + 10) * 16), 0, 255);
            }
            // The smallest three of x, y, z, w (the order SPZ keeps): the largest's index in the top two bits.
            var u = Unit(g.Rot);
            var xyzw = new[] { u[1], u[2], u[3], u[0] };
            var largest = Enumerable.Range(0, 4).MaxBy(k => Math.Abs(xyzw[k]));
            if (xyzw[largest] < 0) xyzw = xyzw.Select(c => -c).ToArray();
            uint comp = (uint)largest;
            for (var k = 0; k < 4; k++)
            {
                if (k == largest) continue;
                var mag = (uint)Math.Round(511 * Math.Abs(xyzw[k]) / Math.Sqrt(0.5));
                comp = comp << 10 | (xyzw[k] < 0 ? 1u : 0u) << 9 | mag;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(rot.AsSpan(4 * i), comp);
            for (var j = 0; j < coeffs; j++)
                for (var c = 0; c < 3; c++)
                    sh[(i * coeffs + j) * 3 + c] = Splat.ShByte(g.Rest![c * coeffs + j]);
        }
        var raw = new[] { pos, alpha, colour, scale, rot, sh };
        var comps = raw.Select(compress).ToList();
        var ext = extension ? new byte[] { 1, 0, 0, 0, 4, 0, 0, 0, 9, 9, 9, 9 } : [];
        var toc = 32 + ext.Length;
        var o = new MemoryStream();
        var h = new byte[32];
        BinaryPrimitives.WriteUInt32LittleEndian(h, 0x5053474e);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(4), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(8), (uint)n);
        h[12] = 3; h[13] = (byte)fractional; h[14] = (byte)(extension ? 2 : 0); h[15] = 6;
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(16), (uint)toc);
        o.Write(h);
        o.Write(ext);
        for (var k = 0; k < 6; k++) { o.Write(BitConverter.GetBytes((ulong)comps[k].Length)); o.Write(BitConverter.GetBytes((ulong)raw[k].Length)); }
        foreach (var c in comps) o.Write(c);
        return o.ToArray();
    }

    static void SameAsSpz(SplatBatch want, SplatBatch got, string what) =>
        // Positions 1/4096 m; sizes e^(1/32); colours through 0.15·f_dc a byte (≈ 2 of ours); turns 9 bits; harmonics exact bytes.
        Same(want, got, pos: 0.5 / 4096 + 1e-9, size: 1.0 / 32 + 1e-6, turn: 2e-5, colour: 3, alpha: 1, sh: 0, what);

    [Fact]
    public void An_spz_version_4_of_raw_zstandard_frames_gives_back_the_plys_gaussians_and_skips_its_extensions()
    {
        var gs = Capture();
        var want = Reference(gs);
        var r = PointReaders.Open(new BytesSource("scene.spz", Spz4(gs, RawFrame, extension: true)));
        var spz = Assert.IsType<SpzReader>(r);
        Assert.Equal("y", r.Header.Up);
        Assert.Equal(3, spz.ShDegree);
        Assert.Contains("SPZ version 4", r.Header.Format);
        Assert.Contains(r.Header.Notes, n => n.Contains("extensions"));
        SameAsSpz(want, All(spz), "SPZ 4");
        Assert.Equal(want.Count, r.Read().Sum(b => b.Count));
    }

    [Fact]
    public void An_spz_version_4_compressed_by_the_reference_zstd_reads_the_same()
    {
        var zstd = WebPTests.Tool("zstd");
        if (zstd == null) return; // Not on this computer: the raw frames above stand alone.
        var dir = Directory.CreateTempSubdirectory("lapin-spz4-");
        try
        {
            var k = 0;
            byte[] Compress(byte[] data)
            {
                var src = Path.Combine(dir.FullName, $"s{k}");
                var dst = Path.Combine(dir.FullName, $"s{k++}.zst");
                File.WriteAllBytes(src, data);
                WebPTests.Run(zstd, "-q", "-f", "-12", src, "-o", dst);
                return File.ReadAllBytes(dst);
            }
            var gs = Capture(3000, seed: 9);
            var want = Reference(gs);
            var spz = Assert.IsType<SpzReader>(PointReaders.Open(new BytesSource("big.spz", Spz4(gs, Compress))));
            SameAsSpz(want, All(spz), "SPZ 4 by zstd");
        }
        finally { dir.Delete(true); }
    }

    // ─── The colour by direction ───

    [Fact]
    public void The_harmonics_basis_is_kerbls_and_a_colour_changes_with_the_side_it_is_seen_from()
    {
        Span<double> b = stackalloc double[15];
        Splat.ShBasis(0, 0, 1, b);
        // Seen along z: degree 1 only its z term; degree 2 its zz term 2·0.3154; degree 3 its z³ term 2·0.3732.
        Assert.Equal(0, b[0], 12); Assert.Equal(0.4886025119029199, b[1], 12); Assert.Equal(0, b[2], 12);
        Assert.Equal(2 * 0.31539156525252005, b[5], 12);
        Assert.Equal(2 * 0.3731763325901154, b[11], 12);
        Splat.ShBasis(1, 0, 0, b);
        Assert.Equal(-0.4886025119029199, b[2], 12);
        Assert.Equal(0.5462742152960396, b[7], 12);
        Assert.Equal(-0.5900435899266435, b[14], 12);
        // Orthonormal over the sphere: ∫ Yᵢ Yⱼ dΩ = δᵢⱼ (a Fibonacci sphere of 20 000 directions, the mean × 4π).
        var m = 20000;
        var gram = new double[15, 15];
        for (var i = 0; i < m; i++)
        {
            var z = 1 - (2.0 * i + 1) / m;
            var rr = Math.Sqrt(1 - z * z);
            var phi = i * Math.PI * (3 - Math.Sqrt(5));
            Splat.ShBasis(rr * Math.Cos(phi), rr * Math.Sin(phi), z, b);
            for (var p = 0; p < 15; p++) for (var q = 0; q < 15; q++) gram[p, q] += b[p] * b[q] * 4 * Math.PI / m;
        }
        for (var p = 0; p < 15; p++) for (var q = 0; q < 15; q++) Assert.True(Math.Abs(gram[p, q] - (p == q ? 1 : 0)) < 2e-3, $"⟨Y{p}, Y{q}⟩ = {gram[p, q]}");
        // A Gaussian red from +x and blue from −x: its degree-1 x coefficient (−C1·x) is −0.5 for red, +0.5 for blue.
        var sh = new byte[9];
        Array.Fill(sh, Splat.ShByte(0));
        sh[6] = Splat.ShByte(-0.5); sh[8] = Splat.ShByte(0.5);
        var fromMinusX = Splat.Colour((0.5, 0.5, 0.5), sh, 1, (1, 0, 0));   // looking along +x: seen from −x
        var fromPlusX = Splat.Colour((0.5, 0.5, 0.5), sh, 1, (-1, 0, 0));
        Assert.True(fromMinusX.R > 0.7 && fromMinusX.B < 0.3, $"{fromMinusX}");
        Assert.True(fromPlusX.B > 0.7 && fromPlusX.R < 0.3, $"{fromPlusX}");
        Assert.Equal((0.5, 0.5, 0.5), Splat.Colour((0.5, 0.5, 0.5), sh, 0, (1, 0, 0)));
    }

    [Fact]
    public void A_links_page_carries_its_harmonics_at_the_degree_the_view_affords_and_the_turn_into_the_files_frame()
    {
        var gs = Capture(300);
        var f = new Fixture(new ArchitectureModule());
        var r = (JsonObject)f.Run("pointcloud.link", new JsonObject { ["name"] = "scene.sog", ["data"] = Convert.ToBase64String(Zip(Sog(gs, 3))) })!;
        var id = (string)r["id"]!;
        Assert.Contains("SOG version 3", (string?)r["format"]);
        for (var k = 0; k < 400 && (string?)((JsonObject)f.Session.Query("pointcloud.status", new JsonObject { ["id"] = id })!)["state"] != "ready"; k++) Thread.Sleep(25);
        f.Run("pointcloud.place", new JsonObject { ["id"] = id, ["rotation"] = 90, ["units"] = "cm" });
        var link = Links.Require(f.Model, id);
        Assert.Equal(3, (int?)link.Attrs["cloud"]?["shDegree"]);
        var store = PointCloudLinks.SplatsOf(link)!;
        Assert.Equal(3, store.ShDegree);
        // No harmonics asked: none sent.
        var plain = PointCloudLinks.Splats(f.Model, link, 0, 1000);
        Assert.Equal(0, plain.ShDegree);
        Assert.Null(plain.Sh);
        // Degree 3 asked, room for it: 45 bytes a Gaussian, as sh.bin has them.
        var page = PointCloudLinks.Splats(f.Model, link, 0, 1000, shDegree: 3);
        Assert.Equal(3, page.ShDegree);
        var n = page.Colors.Length;
        Assert.Equal((n * 45 + 3) / 4, page.Sh!.Length);
        var bytes = new byte[page.Sh.Length * 4];
        Buffer.BlockCopy(page.Sh, 0, bytes, 0, bytes.Length);
        Assert.Equal(store.ReadSh(0, 1).AsSpan(0, 45).ToArray(), bytes[..45]);
        // Room for degree 1 only (9 bytes a Gaussian): degree 1, its first three coefficients of each.
        var small = PointCloudLinks.Splats(f.Model, link, 0, 1000, shDegree: 3, shBytes: 9L * n + 5);
        Assert.Equal(1, small.ShDegree);
        var sb = new byte[small.Sh!.Length * 4];
        Buffer.BlockCopy(small.Sh, 0, sb, 0, sb.Length);
        Assert.Equal(bytes[45..54], sb[9..18]);
        Assert.Equal(2, SplatStore.DegreeFor(3, 3, 1000, 24_000));
        Assert.Equal(0, SplatStore.DegreeFor(3, 3, 1000, 8_000));
        // The turn: a direction in the project back into the file's (the link turned 90°, stood up from −y, in centimetres).
        var t = PointCloudLinks.TransformOf(f.Model, link);
        double[] v = [0.3, -0.5, 0.8];
        double[] p = [t[0] * v[0] + t[3] * v[1] + t[6] * v[2], t[1] * v[0] + t[4] * v[1] + t[7] * v[2], t[2] * v[0] + t[5] * v[1] + t[8] * v[2]];
        var m = page.Turn!;
        double[] back = [m[0] * p[0] + m[3] * p[1] + m[6] * p[2], m[1] * p[0] + m[4] * p[1] + m[7] * p[2], m[2] * p[0] + m[5] * p[1] + m[8] * p[2]];
        var scale = Math.Sqrt(back.Sum(c => c * c)) / Math.Sqrt(v.Sum(c => c * c));
        for (var k = 0; k < 3; k++) Assert.Equal(v[k] * scale, back[k], 6);
        // The link's colour: full by default, base on asking, a view's row over it.
        Assert.Equal("full", PointCloudLinks.LookIn(null, link).SplatColor);
        f.Run("pointcloud.update", new JsonObject { ["id"] = id, ["splatColor"] = "base" });
        link = Links.Require(f.Model, id);
        Assert.Equal("base", PointCloudLinks.LookIn(null, link).SplatColor);
        Assert.Equal("full", PointCloudLinks.LookIn(new JsonObject { ["pointClouds"] = new JsonObject { [id] = new JsonObject { ["splatColor"] = "full" } } }, link).SplatColor);
        Assert.Throws<CommandException>(() => f.Run("pointcloud.update", new JsonObject { ["id"] = id, ["splatColor"] = "sepia" }));
        Assert.Throws<CommandException>(() => PointCloudLinks.CheckViewRow(new JsonObject { ["splatColor"] = "sepia" }));
        // One undo step puts it back.
        f.Session.Undo();
        Assert.Equal("full", PointCloudLinks.LookIn(null, Links.Require(f.Model, id)).SplatColor);
        // Through the dispatcher: the harmonics as a buffer of ints, the turn as floats.
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var frame = d.Handle(new JsonObject { ["id"] = 1, ["method"] = "pointcloud.splats", ["params"] = new JsonObject { ["id"] = id, ["start"] = 0, ["budget"] = 1000, ["sh"] = 2 } }.ToJsonString()).Reply;
        var res = BinaryFrame.Decode(frame).Header.RootElement.GetProperty("result");
        Assert.Equal(2, res.GetProperty("shDegree").GetInt32());
        Assert.Equal("i32", res.GetProperty("sh").GetProperty("t").GetString());
        Assert.Equal(9, res.GetProperty("turn").GetProperty("n").GetInt32());
        Assert.Equal(BinaryFrame.Encode(new Reply(1, page)), BinaryFrame.Encode(new Reply(1, page), contractsOnly: true));
    }
}
