using System.Buffers.Binary;

namespace Lapin.PointClouds;

/// <summary>
/// SuperSplat's compressed PLY (PlayCanvas, `.compressed.ply`, as its editor and SplatTransform write it and its engine reads
/// it): a <c>chunk</c> element, one row a run of 256 Gaussians, holding their bounds as floats (<c>min_x … max_z</c>, the
/// log scales' <c>min_scale_x … max_scale_z</c>, and optionally the colours' <c>min_r … max_b</c>); a <c>vertex</c> element of
/// four uint32 a Gaussian, each read in its chunk's bounds: <c>packed_position</c> 11, 10, 11 bits (x in the top bits),
/// <c>packed_rotation</c> the smallest three of w, x, y, z (the largest's index in the top two bits, then three 10-bit
/// values over ±√½), <c>packed_scale</c> 11, 10, 11 bits of the log scales, <c>packed_color</c> four bytes, red the top, the
/// colour (0.5 + C0 · f_dc, within the chunk's colour bounds when it has them) and the opacity after the sigmoid; and an
/// optional <c>sh</c> element of bytes <c>f_rest_*</c> (9, 24 or 45, channel by channel), each (n − 0.5) · 8 with n its byte
/// over 256 (0 and 255 the ends). Coordinates are the 3DGS PLY's: its link stands with −y up.
/// </summary>
public sealed class CompressedPlyReader : ISplatReader
{
    const int ChunkSize = 256;
    static readonly string[] ChunkProps = ["min_x", "min_y", "min_z", "max_x", "max_y", "max_z", "min_scale_x", "min_scale_y", "min_scale_z", "max_scale_x", "max_scale_y", "max_scale_z"];
    static readonly string[] ColorProps = ["min_r", "min_g", "min_b", "max_r", "max_g", "max_b"];
    static readonly string[] Packed = ["packed_position", "packed_rotation", "packed_scale", "packed_color"];

    readonly IPointSource _source;
    readonly PlyReader _ply;
    readonly PlyReader.Element _chunk, _vertex;
    readonly PlyReader.Element? _sh;
    readonly int _fileCoeffs;
    float[]? _chunks;     // a chunk's 18 values: the 12 bounds, then the colour bounds (or none)
    bool _chunkColors;

    public PointCloudHeader Header { get; }
    public int ShDegree { get; }

    /// <summary>Whether a PLY is a compressed splat file: a chunk element of float bounds and a vertex element of the four packed uint32.</summary>
    internal static bool Is(PlyReader ply)
    {
        var chunk = ply._elements.FirstOrDefault(e => e.Name == "chunk");
        var vertex = ply.Vertex;
        if (chunk == null || vertex == null) return false;
        bool Has(PlyReader.Element e, string n, params string[] types) => e.Props.Any(p => p.Name == n && p.ListCount == null && types.Contains(p.Type));
        return ChunkProps.All(n => Has(chunk, n, "float", "float32")) && Packed.All(n => Has(vertex, n, "uint", "uint32"));
    }

    public CompressedPlyReader(IPointSource source, PlyReader ply)
    {
        _source = source;
        _ply = ply;
        if (ply._enc == PlyReader.Encoding.Ascii) throw new PointCloudException($"{source.Name}: a compressed PLY is binary; this one says ascii.");
        _chunk = ply._elements.First(e => e.Name == "chunk");
        _vertex = ply.Vertex!;
        _sh = ply._elements.FirstOrDefault(e => e.Name == "sh");
        if (_chunk.Count != (_vertex.Count + ChunkSize - 1) / ChunkSize)
            throw new PointCloudException($"{source.Name}: {_chunk.Count:N0} chunks for {_vertex.Count:N0} Gaussians (a chunk is 256 of them).");
        foreach (var e in ply._elements)
            if (e.Props.Any(p => p.ListCount != null)) throw new PointCloudException($"{source.Name}: its {e.Name} element carries lists, which a compressed PLY does not have.");
        var notes = new List<string>();
        if (_sh != null)
        {
            _fileCoeffs = _sh.Props.Count / 3;
            if (_sh.Count != _vertex.Count || _sh.Props.Count % 3 != 0 || _sh.Props.Any(p => p.Type is not ("uchar" or "uint8")))
                throw new PointCloudException($"{source.Name}: its sh element is not one byte a coefficient for every Gaussian.");
        }
        ShDegree = _fileCoeffs >= 15 ? 3 : _fileCoeffs >= 8 ? 2 : _fileCoeffs >= 3 ? 1 : 0;
        Header = new PointCloudHeader
        {
            Format = $"Compressed PLY Gaussian splats{(ShDegree > 0 ? $", harmonics of degree {ShDegree}" : "")}", Count = _vertex.Count, HasColor = true,
            Up = "-y", Splats = true, ShDegree = ShDegree, Notes = notes, Bounds = BoundsOfChunks(),
        };
    }

    /// <summary>Where an element's rows start in the file (every element before it has fixed rows).</summary>
    long Start(PlyReader.Element el)
    {
        var at = _ply._dataAt;
        foreach (var e in _ply._elements)
        {
            if (e == el) return at;
            at += e.Count * e.Props.Sum(p => PlyReader.Size(p.Type));
        }
        throw new InvalidOperationException();
    }

    float[] Chunks()
    {
        if (_chunks != null) return _chunks;
        var props = _chunk.Props;
        var stride = props.Sum(p => PlyReader.Size(p.Type));
        var bytes = new byte[checked((int)(_chunk.Count * stride))];
        using (var s = _source.Open())
        {
            s.Position = Start(_chunk);
            if (s.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false) < bytes.Length) throw new PointCloudException($"{_source.Name}: its chunks are cut short.");
        }
        _chunkColors = ColorProps.All(n => props.Any(p => p.Name == n));
        var names = ChunkProps.Concat(_chunkColors ? ColorProps : []).ToArray();
        var offsets = names.Select(n =>
        {
            var o = 0;
            foreach (var p in props) { if (p.Name == n) return (o, p.Type); o += PlyReader.Size(p.Type); }
            throw new InvalidOperationException();
        }).ToArray();
        var c = new float[_chunk.Count * 18];
        for (long i = 0; i < _chunk.Count; i++)
            for (var k = 0; k < names.Length; k++)
                c[i * 18 + k] = (float)_ply.Value(bytes.AsSpan((int)(i * stride) + offsets[k].o), offsets[k].Type);
        return _chunks = c;
    }

    double[]? BoundsOfChunks()
    {
        try
        {
            var c = Chunks();
            if (_chunk.Count == 0) return null;
            double[] b = [double.MaxValue, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue, double.MinValue];
            for (long i = 0; i < _chunk.Count; i++)
                for (var k = 0; k < 3; k++)
                {
                    b[k] = Math.Min(b[k], c[i * 18 + k]);
                    b[3 + k] = Math.Max(b[3 + k], c[i * 18 + 3 + k]);
                }
            return b;
        }
        catch (Exception e) when (e is IOException or PointCloudException) { return null; }
    }

    static float Unorm(uint v, int bits) => (v & ((1u << bits) - 1)) / (float)((1u << bits) - 1);
    static float Lerp(float a, float b, float t) => a * (1 - t) + b * t;

    /// <summary>The Gaussians one by one, with their index in the file (the transparent ones left out).</summary>
    IEnumerable<(long I, double X, double Y, double Z, float S0, float S1, float S2, float Qw, float Qx, float Qy, float Qz, byte R, byte G, byte B, byte A)> Gaussians(CancellationToken cancel)
    {
        var c = Chunks();
        var props = _vertex.Props;
        var stride = props.Sum(p => PlyReader.Size(p.Type));
        var off = Packed.Select(n => { var o = 0; foreach (var p in props) { if (p.Name == n) return o; o += PlyReader.Size(p.Type); } return -1; }).ToArray();
        var big = _ply._enc == PlyReader.Encoding.Big;
        using var s = _source.Open();
        s.Position = Start(_vertex);
        var buf = new byte[stride * 4096];
        long i = 0;
        var sqrtHalfInv = MathF.Sqrt(2);
        while (i < _vertex.Count)
        {
            cancel.ThrowIfCancellationRequested();
            var take = (int)Math.Min(_vertex.Count - i, 4096);
            if (s.ReadAtLeast(buf.AsSpan(0, take * stride), take * stride, throwOnEndOfStream: false) < take * stride) throw new PointCloudException($"{_source.Name}: its Gaussians are cut short.");
            for (var k = 0; k < take; k++, i++)
            {
                uint U(int j) { var sp = buf.AsSpan(k * stride + off[j]); return big ? BinaryPrimitives.ReadUInt32BigEndian(sp) : BinaryPrimitives.ReadUInt32LittleEndian(sp); }
                uint pp = U(0), pr = U(1), ps = U(2), pc = U(3);
                var a = (byte)(pc & 0xff);
                if (a / 255.0 < Splat.MinAlpha) continue;
                var ch = (int)(i / ChunkSize) * 18;
                var x = Lerp(c[ch], c[ch + 3], Unorm(pp >> 21, 11));
                var y = Lerp(c[ch + 1], c[ch + 4], Unorm(pp >> 11, 10));
                var z = Lerp(c[ch + 2], c[ch + 5], Unorm(pp, 11));
                var s0 = MathF.Exp(Lerp(c[ch + 6], c[ch + 9], Unorm(ps >> 21, 11)));
                var s1 = MathF.Exp(Lerp(c[ch + 7], c[ch + 10], Unorm(ps >> 11, 10)));
                var s2 = MathF.Exp(Lerp(c[ch + 8], c[ch + 11], Unorm(ps, 11)));
                // The rotation: three 10-bit values over ±√½ in the order they come, the largest (top two bits) worked out.
                var qa = (Unorm(pr >> 20, 10) - 0.5f) * sqrtHalfInv;
                var qb = (Unorm(pr >> 10, 10) - 0.5f) * sqrtHalfInv;
                var qc = (Unorm(pr, 10) - 0.5f) * sqrtHalfInv;
                var m = MathF.Sqrt(MathF.Max(0, 1 - (qa * qa + qb * qb + qc * qc)));
                var (qw, qx, qy, qz) = (pr >> 30) switch { 0 => (m, qa, qb, qc), 1 => (qa, m, qb, qc), 2 => (qa, qb, m, qc), _ => (qa, qb, qc, m) };
                float cr = Unorm(pc >> 24, 8), cg = Unorm(pc >> 16, 8), cb = Unorm(pc >> 8, 8);
                if (_chunkColors) { cr = Lerp(c[ch + 12], c[ch + 15], cr); cg = Lerp(c[ch + 13], c[ch + 16], cg); cb = Lerp(c[ch + 14], c[ch + 17], cb); }
                yield return (i, x, y, z, s0, s1, s2, qw, qx, qy, qz, Byte(cr), Byte(cg), Byte(cb), a);
            }
        }
        static byte Byte(float v) => (byte)Math.Clamp(MathF.Round(v * 255), 0, 255);
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

    /// <summary>A harmonic's byte as the format stores it, as the value it stands for.</summary>
    internal static double ShValue(byte b) => ((b == 0 ? 0 : b == 255 ? 1 : (b + 0.5) / 256) - 0.5) * 8;

    public IEnumerable<SplatBatch> ReadSplats(int batch = 65536, bool sh = false, CancellationToken cancel = default)
    {
        var coeffs = sh && _sh != null ? Splat.ShCoeffs(ShDegree) : 0;
        var b = new SplatBatch(batch, coeffs);
        using var shStream = coeffs > 0 ? _source.Open() : null;
        var row = new byte[_fileCoeffs * 3];
        long at = 0;
        if (shStream != null) shStream.Position = Start(_sh!);
        foreach (var g in Gaussians(cancel))
        {
            if (b.Full) { yield return b; b.Count = 0; }
            var i = b.Add(g.X, g.Y, g.Z, g.S0, g.S1, g.S2, g.Qw, g.Qx, g.Qy, g.Qz, g.R, g.G, g.B, g.A);
            if (shStream == null) continue;
            if (g.I > at) { shStream.Seek((g.I - at) * row.Length, SeekOrigin.Current); at = g.I; }
            shStream.ReadExactly(row);
            at++;
            // The file's channel by channel into coefficient by coefficient, colours inner.
            for (var j = 0; j < coeffs; j++)
                for (var ch = 0; ch < 3; ch++)
                    b.Sh[(i * coeffs + j) * 3 + ch] = Splat.ShByte(ShValue(row[ch * _fileCoeffs + j]));
        }
        if (b.Count > 0) yield return b;
    }
}
