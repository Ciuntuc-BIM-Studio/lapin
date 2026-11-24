using System.Buffers.Binary;
using System.IO.Compression;

namespace Lapin.PointClouds;

/// <summary>
/// Niantic's SPZ (github.com/nianticlabs/spz, MIT), versions 1–3: one gzip stream holding a 16-byte header (magic
/// <c>NGSP</c> as 0x5053474e, version, the Gaussians' count, the harmonics' degree, the positions' fractional bits, flags, a
/// reserved byte) and then each attribute for every Gaussian in turn: positions (version 1: three float16; 2 and 3: three
/// signed 24-bit fixed-point numbers), opacities (a byte, the sigmoid × 255), colours (three bytes, f_dc = (b/255 − 0.5)/0.15),
/// scales (three bytes, ln s = b/16 − 10), rotations (versions 1–2: x, y, z as bytes, b/127.5 − 1, w ≥ 0 worked out;
/// version 3: the smallest three of x, y, z, w in 32 bits, the largest's index in the top two, then 10 bits each, a sign
/// and a 9-bit magnitude of at most √½), harmonics (a byte each, (b − 128)/128, coefficient by coefficient, colours inner).
/// Its coordinates are RUB (x right, y up, z back), so its link stands with y up. Version 4 (ZSTD streams) is not read yet.
/// </summary>
public sealed class SpzReader : ISplatReader
{
    const uint Magic = 0x5053474e;
    const float ColorScale = 0.15f;

    readonly IPointSource _source;
    readonly int _version, _fractional, _fileDegree;
    readonly long _count;
    byte[]? _core;

    public PointCloudHeader Header { get; }
    public int ShDegree { get; }

    public SpzReader(IPointSource source)
    {
        _source = source;
        Span<byte> h = stackalloc byte[16];
        using (var s = Open())
            if (s.ReadAtLeast(h, 16, throwOnEndOfStream: false) < 16) throw new PointCloudException($"{source.Name}: its SPZ header is cut short.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(h) != Magic) throw new PointCloudException($"{source.Name} is not an SPZ file (its header has no NGSP).");
        _version = (int)BinaryPrimitives.ReadUInt32LittleEndian(h[4..]);
        if (_version is < 1 or > 3) throw new PointCloudException($"{source.Name} is SPZ version {_version}; Lapin reads versions 1 to 3.");
        _count = BinaryPrimitives.ReadUInt32LittleEndian(h[8..]);
        _fileDegree = h[12];
        if (_fileDegree > 4) throw new PointCloudException($"{source.Name}: its harmonics' degree, {_fileDegree}, is not one SPZ has.");
        _fractional = h[13];
        ShDegree = Math.Min(3, _fileDegree);
        var notes = new List<string>();
        if (_fileDegree > 3) notes.Add($"{source.Name} has spherical harmonics of degree {_fileDegree}; Lapin keeps degrees 1–3.");
        Header = new PointCloudHeader
        {
            Format = $"SPZ version {_version} Gaussian splats{(ShDegree > 0 ? $", harmonics of degree {ShDegree}" : "")}", Count = _count, HasColor = true,
            Up = "y", Splats = true, Notes = notes,
        };
    }

    Stream Open() => new GZipStream(_source.Open(), CompressionMode.Decompress);

    int PosBytes => _version == 1 ? 6 : 9;
    int RotBytes => _version >= 3 ? 4 : 3;
    long CoreBytes => 16 + _count * (PosBytes + 1 + 3 + 3 + RotBytes);

    /// <summary>The header and every attribute but the harmonics, decompressed once.</summary>
    byte[] Core()
    {
        if (_core != null) return _core;
        if (CoreBytes > Array.MaxLength) throw new PointCloudException($"{_source.Name} holds {_count:N0} Gaussians: more than Lapin reads from one SPZ file.");
        var core = new byte[CoreBytes];
        using (var s = Open())
            if (s.ReadAtLeast(core, core.Length, throwOnEndOfStream: false) < core.Length) throw new PointCloudException($"{_source.Name}: its data is cut short.");
        return _core = core;
    }

    /// <summary>Each Gaussian's centre, scales, rotation (w, x, y, z), colour and opacity, in the file's order.</summary>
    IEnumerable<(long I, double X, double Y, double Z, float S0, float S1, float S2, float Qw, float Qx, float Qy, float Qz, byte R, byte G, byte B, byte A)> Gaussians(CancellationToken cancel)
    {
        var d = Core();
        var n = _count;
        long pos = 16, alpha = pos + n * PosBytes, color = alpha + n, scale = color + 3 * n, rot = scale + 3 * n;
        var step = 1.0 / (1 << _fractional);
        for (long i = 0; i < n; i++)
        {
            if ((i & 0xFFFF) == 0) cancel.ThrowIfCancellationRequested();
            var a = d[alpha + i];
            if (a / 255.0 < Splat.MinAlpha) continue;
            double x, y, z;
            if (_version == 1)
            {
                var p = d.AsSpan((int)(pos + 6 * i));
                (x, y, z) = ((double)BinaryPrimitives.ReadHalfLittleEndian(p), (double)BinaryPrimitives.ReadHalfLittleEndian(p[2..]), (double)BinaryPrimitives.ReadHalfLittleEndian(p[4..]));
            }
            else
            {
                var p = pos + 9 * i;
                x = Fixed(d, p) * step; y = Fixed(d, p + 3) * step; z = Fixed(d, p + 6) * step;
            }
            var c = color + 3 * i;
            var sc = scale + 3 * i;
            float qx, qy, qz, qw;
            if (_version >= 3) (qx, qy, qz, qw) = SmallestThree(BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan((int)(rot + 4 * i))));
            else
            {
                var r = rot + 3 * i;
                qx = d[r] / 127.5f - 1; qy = d[r + 1] / 127.5f - 1; qz = d[r + 2] / 127.5f - 1;
                qw = MathF.Sqrt(MathF.Max(0, 1 - qx * qx - qy * qy - qz * qz));
            }
            yield return (i, x, y, z, Scale(d[sc]), Scale(d[sc + 1]), Scale(d[sc + 2]), qw, qx, qy, qz, Col(d[c]), Col(d[c + 1]), Col(d[c + 2]), a);
        }
        static float Scale(byte b) => MathF.Exp(b / 16f - 10);
        static byte Col(byte b) => Splat.ColorOf((b / 255.0 - 0.5) / ColorScale);
    }

    static int Fixed(byte[] d, long at)
    {
        var v = d[at] | d[at + 1] << 8 | d[at + 2] << 16;
        return (v & 0x800000) != 0 ? v | unchecked((int)0xFF000000) : v;
    }

    /// <summary>Version 3's rotation: the largest of x, y, z, w left out (its index in the top two bits), the others 10 bits each, the last in the lowest.</summary>
    internal static (float X, float Y, float Z, float W) SmallestThree(uint comp)
    {
        const uint mask = (1u << 9) - 1;
        var largest = (int)(comp >> 30);
        Span<float> q = stackalloc float[4];
        var sum = 0f;
        for (var i = 3; i >= 0; i--)
        {
            if (i == largest) continue;
            var mag = comp & mask;
            var neg = (comp >> 9) & 1;
            comp >>= 10;
            q[i] = MathF.Sqrt(0.5f) * mag / mask * (neg == 1 ? -1 : 1);
            sum += q[i] * q[i];
        }
        q[largest] = MathF.Sqrt(MathF.Max(0, 1 - sum));
        return (q[0], q[1], q[2], q[3]);
    }

    public IEnumerable<PointBatch> Read(int batch = 65536, CancellationToken cancel = default)
    {
        var b = new PointBatch(batch);
        foreach (var g in Gaussians(cancel))
        {
            if (b.Full) { yield return b; b.Count = 0; }
            b.Add(g.X, g.Y, g.Z, g.R, g.G, g.B);
        }
        if (b.Count > 0) yield return b;
    }

    public IEnumerable<SplatBatch> ReadSplats(int batch = 65536, bool sh = false, CancellationToken cancel = default)
    {
        var coeffs = sh ? Splat.ShCoeffs(ShDegree) : 0;
        var fileCoeffs = Splat.ShCoeffs(_fileDegree);
        var b = new SplatBatch(batch, coeffs);
        // The harmonics follow everything else: read from a stream of their own, Gaussian by Gaussian, the left-out ones skipped.
        using var shStream = coeffs > 0 ? Open() : null;
        var rec = new byte[fileCoeffs * 3];
        long at = 0;
        if (shStream != null) Skip(shStream, CoreBytes);
        foreach (var g in Gaussians(cancel))
        {
            if (b.Full) { yield return b; b.Count = 0; }
            var i = b.Add(g.X, g.Y, g.Z, g.S0, g.S1, g.S2, g.Qw, g.Qx, g.Qy, g.Qz, g.R, g.G, g.B, g.A);
            if (shStream == null) continue;
            if (g.I > at) { Skip(shStream, (g.I - at) * rec.Length); at = g.I; }
            shStream.ReadExactly(rec);
            at++;
            rec.AsSpan(0, coeffs * 3).CopyTo(b.Sh.AsSpan(i * coeffs * 3));
        }
        if (b.Count > 0) yield return b;
    }

    static void Skip(Stream s, long n)
    {
        var buf = new byte[Math.Min(n, 1 << 16)];
        while (n > 0)
        {
            var got = s.Read(buf, 0, (int)Math.Min(n, buf.Length));
            if (got <= 0) throw new EndOfStreamException();
            n -= got;
        }
    }
}
