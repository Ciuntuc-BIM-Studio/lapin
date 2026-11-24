using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.PointClouds;

/// <summary>
/// Gaussians as a splat reader hands them over (docs/plan-gaussian-splats.md, G1), a few tens of thousands at a time, in the
/// file's own coordinates: the centre, the three scales (linear, not the file's logarithms), the rotation as a unit
/// quaternion (w, x, y, z), the colour of degree 0 and the opacity (0–255, after the sigmoid), and when asked the spherical
/// harmonics of degree 1–3 quantized to a byte each (round(v · 128 + 128), coefficient by coefficient, red, green, blue
/// inner, as SPZ keeps them).
/// </summary>
public sealed class SplatBatch(int capacity, int shCoeffs = 0)
{
    public readonly double[] X = new double[capacity], Y = new double[capacity], Z = new double[capacity];
    public readonly float[] S0 = new float[capacity], S1 = new float[capacity], S2 = new float[capacity];
    public readonly float[] Qw = new float[capacity], Qx = new float[capacity], Qy = new float[capacity], Qz = new float[capacity];
    public readonly byte[] R = new byte[capacity], G = new byte[capacity], B = new byte[capacity], A = new byte[capacity];
    /// <summary>The harmonics of degree 1–3, <see cref="ShCoeffs"/> × 3 bytes a splat; empty when not asked for.</summary>
    public readonly byte[] Sh = new byte[capacity * shCoeffs * 3];
    public readonly int ShCoeffs = shCoeffs;
    public int Count;
    public bool Full => Count >= X.Length;

    public int Add(double x, double y, double z, float s0, float s1, float s2, float qw, float qx, float qy, float qz, byte r, byte g, byte b, byte a)
    {
        var i = Count++;
        X[i] = x; Y[i] = y; Z[i] = z; S0[i] = s0; S1[i] = s1; S2[i] = s2;
        var n = MathF.Sqrt(qw * qw + qx * qx + qy * qy + qz * qz);
        if (!(n > 1e-12f)) { qw = 1; qx = qy = qz = 0; n = 1; }
        Qw[i] = qw / n; Qx[i] = qx / n; Qy[i] = qy / n; Qz[i] = qz / n;
        R[i] = r; G[i] = g; B[i] = b; A[i] = a;
        return i;
    }
}

/// <summary>A file of Gaussians: a point reader (its centres, coloured by degree 0, into the index) that also hands its Gaussians over.</summary>
public interface ISplatReader : IPointReader
{
    /// <summary>The degree of its spherical harmonics (0–3; a higher one's extra coefficients are not kept).</summary>
    int ShDegree { get; }
    /// <summary>Every Gaussian the index takes (the nearly transparent ones left out, as from <see cref="IPointReader.Read"/>), in the same order.</summary>
    IEnumerable<SplatBatch> ReadSplats(int batch = 65536, bool sh = false, CancellationToken cancel = default);
}

/// <summary>The rules every splat reader shares.</summary>
public static class Splat
{
    /// <summary>The spherical harmonic of degree 0: a colour is 0.5 + C0 · f_dc.</summary>
    public const double C0 = 0.28209479177387814;

    /// <summary>A Gaussian less opaque than this (after the sigmoid) adds nothing a screen shows: left out of the index and of the splats.</summary>
    public const double MinAlpha = 1.0 / 255;

    public static double Sigmoid(double v) => 1 / (1 + Math.Exp(-v));

    /// <summary>A colour channel (0–255) from its coefficient of degree 0.</summary>
    public static byte ColorOf(double fdc) => (byte)Math.Clamp(Math.Round((0.5 + C0 * fdc) * 255), 0, 255);

    /// <summary>The coefficients a colour channel has above degree 0: 3, 8, 15 for degrees 1, 2, 3.</summary>
    public static int ShCoeffs(int degree) => (degree + 1) * (degree + 1) - 1;

    /// <summary>A harmonic quantized to a byte (SPZ's rule).</summary>
    public static byte ShByte(double v) => (byte)Math.Clamp(Math.Round(v * 128 + 128), 0, 255);

    /// <summary>A byte of <c>sh.bin</c> as the harmonic it stands for.</summary>
    public static double ShValue(byte b) => (b - 128) / 128.0;

    /// <summary>
    /// The real spherical harmonics of degrees 1–3 at a unit direction (x, y, z), with the Condon–Shortley phase, as 3D Gaussian
    /// splatting uses them (Kerbl et al. 2023, its reference renderer; SOG's and glTF's KHR_gaussian_splatting's basis): 3, 5
    /// and 7 values, band by band, into <paramref name="o"/> (its length the coefficients wanted). The interface's shader
    /// evaluates the same (ui/src/viewport/splatSh.ts).
    /// </summary>
    public static void ShBasis(double x, double y, double z, Span<double> o)
    {
        const double C1 = 0.4886025119029199;
        double xx = x * x, yy = y * y, zz = z * z, xy = x * y, yz = y * z, xz = x * z;
        Span<double> b =
        [
            -C1 * y, C1 * z, -C1 * x,
            1.0925484305920792 * xy, -1.0925484305920792 * yz, 0.31539156525252005 * (2 * zz - xx - yy), -1.0925484305920792 * xz, 0.5462742152960396 * (xx - yy),
            -0.5900435899266435 * y * (3 * xx - yy), 2.890611442640554 * xy * z, -0.4570457994644658 * y * (4 * zz - xx - yy),
            0.3731763325901154 * z * (2 * zz - 3 * xx - 3 * yy), -0.4570457994644658 * x * (4 * zz - xx - yy), 1.445305721320277 * z * (xx - yy),
            -0.5900435899266435 * x * (xx - 3 * yy),
        ];
        b[..Math.Min(o.Length, 15)].CopyTo(o);
    }

    /// <summary>
    /// A Gaussian's colour seen along <paramref name="dir"/> (from the eye to it, in the file's frame; any length): its base
    /// colour (0–1, degree 0) plus its harmonics (<paramref name="sh"/>: <c>sh.bin</c>'s bytes, coefficient by coefficient,
    /// colours inner) of degree up to <paramref name="degree"/>, at least 0.
    /// </summary>
    public static (double R, double G, double B) Colour((double R, double G, double B) baseColour, ReadOnlySpan<byte> sh, int degree, (double X, double Y, double Z) dir)
    {
        var n = ShCoeffs(Math.Clamp(degree, 0, 3));
        n = Math.Min(n, sh.Length / 3);
        var len = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y + dir.Z * dir.Z);
        if (n == 0 || !(len > 0)) return baseColour;
        Span<double> basis = stackalloc double[15];
        ShBasis(dir.X / len, dir.Y / len, dir.Z / len, basis);
        var (r, g, b) = baseColour;
        for (var j = 0; j < n; j++)
        {
            r += basis[j] * ShValue(sh[3 * j]);
            g += basis[j] * ShValue(sh[3 * j + 1]);
            b += basis[j] * ShValue(sh[3 * j + 2]);
        }
        return (Math.Max(0, r), Math.Max(0, g), Math.Max(0, b));
    }
}

/// <summary>
/// The Gaussians of a splat file beside its point index, in the same folder (docs/plan-gaussian-splats.md, G1):
/// <list type="bullet">
/// <item><c>splats.bin</c>: 32 bytes a Gaussian (antimatter15's layout): its centre as 3 × float32 and its scales as 3 ×
/// float32 (the file's units, linear), its colour and opacity as RGBA8, its rotation (w, x, y, z; w ≥ 0) as 4 bytes, each
/// round(q · 128 + 128);</item>
/// <item><c>sh.bin</c>: its harmonics of degree 1–3, a byte each, for the later stages (G3);</item>
/// <item><c>rank.bin</c>: a byte a Gaussian, how much it shows (its opacity × its mean area, on a log scale), so that a budget
/// smaller than the file takes the largest and most opaque first;</item>
/// <item><c>splats.json</c>: how many, the harmonics' degree, the ranks' histogram. Written last: there is no store without it.</item>
/// </list>
/// </summary>
public sealed class SplatStore
{
    public const int BytesPerSplat = 32;
    public const int Bins = 256;
    const double LogLo = -64, LogHi = 32;

    public string Directory { get; }
    public long Count { get; private init; }
    public int ShDegree { get; private init; }
    /// <summary>How many Gaussians have each rank.</summary>
    public long[] Histogram { get; private init; } = new long[Bins];

    byte[]? _ranks;
    SplatStore(string dir) { Directory = dir; }

    static readonly string[] Files = ["splats.json", "splats.bin", "sh.bin", "rank.bin"];

    public static bool Exists(string dir) => File.Exists(Path.Combine(dir, "splats.json"));

    public static void Delete(string dir)
    {
        foreach (var f in Files) { var p = Path.Combine(dir, f); if (File.Exists(p)) File.Delete(p); }
    }

    /// <summary>A Gaussian's rank (0–255): log₂ of its opacity (0–1) times its mean area (s0·s1·s2)^⅔, on a fixed scale.</summary>
    public static byte Rank(double alpha, double s0, double s1, double s2)
    {
        var v = alpha * Math.Pow(Math.Max(s0 * s1 * s2, 1e-300), 2.0 / 3);
        var l = Math.Log2(Math.Max(v, 1e-300));
        return (byte)Math.Clamp((int)Math.Floor((l - LogLo) / (LogHi - LogLo) * Bins), 0, Bins - 1);
    }

    /// <summary>A Gaussian's record: centre, scales, colour and opacity, rotation quantized.</summary>
    public static void Pack(Span<byte> o, float x, float y, float z, float s0, float s1, float s2, byte r, byte g, byte b, byte a, float qw, float qx, float qy, float qz)
    {
        BinaryPrimitives.WriteSingleLittleEndian(o, x);
        BinaryPrimitives.WriteSingleLittleEndian(o[4..], y);
        BinaryPrimitives.WriteSingleLittleEndian(o[8..], z);
        BinaryPrimitives.WriteSingleLittleEndian(o[12..], s0);
        BinaryPrimitives.WriteSingleLittleEndian(o[16..], s1);
        BinaryPrimitives.WriteSingleLittleEndian(o[20..], s2);
        o[24] = r; o[25] = g; o[26] = b; o[27] = a;
        if (qw < 0) (qw, qx, qy, qz) = (-qw, -qx, -qy, -qz);
        o[28] = Q(qw); o[29] = Q(qx); o[30] = Q(qy); o[31] = Q(qz);
        static byte Q(float v) => (byte)Math.Clamp(MathF.Round(v * 128 + 128), 0, 255);
    }

    /// <summary>A record read back: its rotation unit again.</summary>
    public static (float X, float Y, float Z, float S0, float S1, float S2, byte R, byte G, byte B, byte A, float Qw, float Qx, float Qy, float Qz) Unpack(ReadOnlySpan<byte> o)
    {
        float qw = (o[28] - 128) / 128f, qx = (o[29] - 128) / 128f, qy = (o[30] - 128) / 128f, qz = (o[31] - 128) / 128f;
        var n = MathF.Sqrt(qw * qw + qx * qx + qy * qy + qz * qz);
        if (n < 1e-6f) (qw, qx, qy, qz, n) = (1, 0, 0, 0, 1);
        return (BinaryPrimitives.ReadSingleLittleEndian(o), BinaryPrimitives.ReadSingleLittleEndian(o[4..]), BinaryPrimitives.ReadSingleLittleEndian(o[8..]),
            BinaryPrimitives.ReadSingleLittleEndian(o[12..]), BinaryPrimitives.ReadSingleLittleEndian(o[16..]), BinaryPrimitives.ReadSingleLittleEndian(o[20..]),
            o[24], o[25], o[26], o[27], qw / n, qx / n, qy / n, qz / n);
    }

    /// <summary>The reader's Gaussians written into <paramref name="dir"/>, streamed (memory bounded by a batch); what was there replaced.</summary>
    public static SplatStore Write(ISplatReader reader, string dir, Action<double>? progress = null, CancellationToken cancel = default)
    {
        System.IO.Directory.CreateDirectory(dir);
        Delete(dir);
        var degree = Math.Clamp(reader.ShDegree, 0, 3);
        var hist = new long[Bins];
        long n = 0;
        var expected = Math.Max(1, reader.Header.Count ?? 1);
        using (var data = new FileStream(Path.Combine(dir, "splats.bin"), FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
        using (var sh = degree > 0 ? new FileStream(Path.Combine(dir, "sh.bin"), FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16) : null)
        using (var rank = new FileStream(Path.Combine(dir, "rank.bin"), FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
        {
            byte[] buf = [];
            byte[] ranks = [];
            foreach (var b in reader.ReadSplats(65536, sh: degree > 0, cancel))
            {
                if (buf.Length < b.Count * BytesPerSplat) { buf = new byte[b.Count * BytesPerSplat]; ranks = new byte[b.Count]; }
                for (var i = 0; i < b.Count; i++)
                {
                    Pack(buf.AsSpan(i * BytesPerSplat), (float)b.X[i], (float)b.Y[i], (float)b.Z[i], b.S0[i], b.S1[i], b.S2[i], b.R[i], b.G[i], b.B[i], b.A[i], b.Qw[i], b.Qx[i], b.Qy[i], b.Qz[i]);
                    var k = Rank(b.A[i] / 255.0, b.S0[i], b.S1[i], b.S2[i]);
                    ranks[i] = k;
                    hist[k]++;
                }
                data.Write(buf, 0, b.Count * BytesPerSplat);
                rank.Write(ranks, 0, b.Count);
                if (sh != null)
                {
                    var per = Splat.ShCoeffs(degree) * 3;
                    if (b.ShCoeffs * 3 == per) sh.Write(b.Sh, 0, b.Count * per);
                    else for (var i = 0; i < b.Count; i++) sh.Write(b.Sh, i * b.ShCoeffs * 3, Math.Min(per, b.ShCoeffs * 3));
                }
                n += b.Count;
                progress?.Invoke(Math.Min(1, (double)n / expected));
            }
        }
        var meta = new JsonObject
        {
            ["version"] = 1, ["count"] = n, ["shDegree"] = degree,
            ["histogram"] = new JsonArray(hist.Select(v => (JsonNode)v).ToArray()),
        };
        File.WriteAllText(Path.Combine(dir, "splats.json"), meta.ToJsonString());
        return Open(dir) ?? throw new PointCloudException("The splats could not be read back.");
    }

    public static SplatStore? Open(string dir)
    {
        var path = Path.Combine(dir, "splats.json");
        if (!File.Exists(path) || !File.Exists(Path.Combine(dir, "splats.bin"))) return null;
        try
        {
            var j = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            if (j == null || (int?)j["version"] != 1) return null;
            var count = (long?)j["count"] ?? 0;
            if (new FileInfo(Path.Combine(dir, "splats.bin")).Length != count * BytesPerSplat) return null;
            return new SplatStore(dir)
            {
                Count = count, ShDegree = (int?)j["shDegree"] ?? 0,
                Histogram = (j["histogram"] as JsonArray)?.Select(v => (long?)v ?? 0).ToArray() is { Length: Bins } h ? h : new long[Bins],
            };
        }
        catch (Exception e) when (e is JsonException or IOException or FormatException or InvalidOperationException) { return null; }
    }

    /// <summary>Every Gaussian's rank, read once.</summary>
    public byte[] Ranks => _ranks ??= File.ReadAllBytes(Path.Combine(Directory, "rank.bin"));

    /// <summary>
    /// What a budget keeps: every Gaussian of a rank above <c>Rank</c>, and of that rank the first <c>Partial</c> in the
    /// file's order; a budget the file fits in keeps them all (<c>Rank</c> −1).
    /// </summary>
    public (int Rank, long Partial, long Kept) Cut(long budget)
    {
        if (budget >= Count) return (-1, 0, Count);
        long above = 0;
        for (var k = Bins - 1; k >= 0; k--)
        {
            if (above + Histogram[k] > budget) return (k, budget - above, budget);
            above += Histogram[k];
        }
        return (-1, 0, Count);
    }

    /// <summary>
    /// The harmonics of the records [<paramref name="start"/>, <paramref name="start"/> + <paramref name="count"/>) as
    /// <c>sh.bin</c> keeps them (<see cref="ShDegree"/>'s coefficients × 3 bytes a Gaussian); empty when it has none.
    /// </summary>
    public byte[] ReadSh(long start, int count)
    {
        var per = Splat.ShCoeffs(ShDegree) * 3;
        start = Math.Clamp(start, 0, Count);
        var end = Math.Min(Count, start + count);
        var path = Path.Combine(Directory, "sh.bin");
        if (per == 0 || end <= start || !File.Exists(path)) return [];
        var bytes = new byte[(end - start) * per];
        using (var h = File.OpenHandle(path))
            if (RandomAccess.Read(h, bytes, start * per) < bytes.Length) return [];
        return bytes;
    }

    /// <summary>
    /// The degree of harmonics a cloud can be drawn with: at most <paramref name="asked"/> and the file's, lowered until the
    /// kept Gaussians' coefficients (3 bytes each a Gaussian) fit in <paramref name="bytes"/>.
    /// </summary>
    public static int DegreeFor(int fileDegree, int asked, long kept, long bytes)
    {
        var d = Math.Clamp(Math.Min(asked, fileDegree), 0, 3);
        while (d > 0 && kept * 3L * Splat.ShCoeffs(d) > bytes) d--;
        return d;
    }

    /// <summary>
    /// The Gaussians a budget keeps among the records [<paramref name="start"/>, <paramref name="start"/> + <paramref name="count"/>),
    /// as their indices and their raw records.
    /// </summary>
    public (List<long> Index, byte[] Records) Read(long start, int count, long budget)
    {
        start = Math.Clamp(start, 0, Count);
        var end = Math.Min(Count, start + count);
        var (cut, partial, _) = Cut(budget);
        var bytes = new byte[(end - start) * BytesPerSplat];
        if (bytes.Length > 0)
            using (var h = File.OpenHandle(Path.Combine(Directory, "splats.bin")))
                RandomAccess.Read(h, bytes, start * BytesPerSplat);
        var idx = new List<long>((int)(end - start));
        if (cut < 0) { for (var i = start; i < end; i++) idx.Add(i); return (idx, bytes); }
        var ranks = Ranks;
        long before = 0;
        for (long i = 0; i < start; i++) if (ranks[i] == cut) before++;
        for (var i = start; i < end; i++)
        {
            var r = ranks[i];
            if (r > cut || r == cut && before++ < partial) idx.Add(i);
        }
        return (idx, bytes);
    }
}
