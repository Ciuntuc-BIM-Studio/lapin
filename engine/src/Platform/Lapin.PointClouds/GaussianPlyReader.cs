using System.Globalization;

namespace Lapin.PointClouds;

/// <summary>
/// A 3D Gaussian splatting PLY (INRIA's layout, Kerbl et al. 2023, as its training code and most tools write it): a vertex a
/// Gaussian with <c>x y z</c>, the colour's harmonics of degree 0 <c>f_dc_0..2</c> and above <c>f_rest_*</c> (channel by
/// channel: red's coefficients, then green's, then blue's), <c>opacity</c> as a logit, <c>scale_0..2</c> as natural
/// logarithms and <c>rot_0..3</c> a quaternion w, x, y, z (not necessarily unit). Its coordinates are the reconstruction's
/// (COLMAP's: x right, y down, z forward), so its link stands with −y up unless the user says otherwise.
/// </summary>
public sealed class GaussianPlyReader : ISplatReader
{
    readonly IPointSource _source;
    readonly PlyReader _ply;
    readonly int[] _pos, _dc, _scale, _rot, _rest;
    readonly int _opacity;

    public PointCloudHeader Header { get; }
    public int ShDegree { get; }

    static readonly string[] Needed = ["x", "y", "z", "f_dc_0", "f_dc_1", "f_dc_2", "opacity", "scale_0", "scale_1", "scale_2", "rot_0", "rot_1", "rot_2", "rot_3"];

    /// <summary>Whether a PLY's vertices are Gaussians (they have every property above).</summary>
    internal static bool Is(PlyReader ply) => ply.Vertex is { } v && Needed.All(n => v.Props.Any(p => p.Name == n));

    public GaussianPlyReader(IPointSource source) : this(source, new PlyReader(source)) { }

    internal GaussianPlyReader(IPointSource source, PlyReader ply)
    {
        _source = source;
        _ply = ply;
        var v = ply.Vertex ?? throw new PointCloudException($"{source.Name}: the PLY file has no vertex element.");
        int I(string n) => v.Props.FindIndex(p => p.Name == n) is var i and >= 0 ? i : throw new PointCloudException($"{source.Name}: its Gaussians have no {n}.");
        _pos = [I("x"), I("y"), I("z")];
        _dc = [I("f_dc_0"), I("f_dc_1"), I("f_dc_2")];
        _scale = [I("scale_0"), I("scale_1"), I("scale_2")];
        _rot = [I("rot_0"), I("rot_1"), I("rot_2"), I("rot_3")];
        _opacity = I("opacity");
        var rest = new List<int>();
        for (var k = 0; ; k++) { var i = v.Props.FindIndex(p => p.Name == $"f_rest_{k}"); if (i < 0) break; rest.Add(i); }
        var per = rest.Count / 3;
        var degree = per >= 15 ? 3 : per >= 8 ? 2 : per >= 3 ? 1 : 0;
        // A degree above 3 (24 coefficients a channel) keeps its first three degrees.
        _rest = [.. rest];
        ShDegree = degree;
        var notes = new List<string>();
        if (per > 15) notes.Add($"{source.Name} has spherical harmonics of degree 4; Lapin keeps degrees 1–3.");
        Header = new PointCloudHeader
        {
            Format = $"PLY Gaussian splats{(degree > 0 ? $", harmonics of degree {degree}" : "")}", Count = v.Count, HasColor = true,
            Up = "-y", Splats = true, ShDegree = degree, Notes = notes,
        };
    }

    /// <summary>The vertices' values one by one (the same array filled again each time).</summary>
    IEnumerable<double[]> Records(CancellationToken cancel)
    {
        var v = _ply.Vertex!;
        using var s = _source.Open();
        s.Position = _ply._dataAt;
        var values = new double[v.Props.Count];
        if (_ply._enc == PlyReader.Encoding.Ascii)
        {
            using var reader = new StreamReader(s, System.Text.Encoding.ASCII, false, 1 << 16);
            foreach (var e in _ply._elements.TakeWhile(e => e != v)) for (long k = 0; k < e.Count; k++) reader.ReadLine();
            for (long n = 0; n < v.Count; n++)
            {
                if ((n & 0xFFFF) == 0) cancel.ThrowIfCancellationRequested();
                var line = reader.ReadLine();
                if (line == null) yield break;
                var t = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (t.Length < values.Length) continue;
                for (var k = 0; k < values.Length; k++) values[k] = double.TryParse(t[k], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
                yield return values;
            }
            yield break;
        }
        foreach (var e in _ply._elements.TakeWhile(e => e != v)) _ply.Skip(s, e);
        var offsets = new int[v.Props.Count];
        var stride = 0;
        for (var k = 0; k < offsets.Length; k++) { offsets[k] = stride; stride += PlyReader.Size(v.Props[k].Type); }
        var buf = new byte[stride * 4096];
        long left = v.Count;
        while (left > 0)
        {
            cancel.ThrowIfCancellationRequested();
            var take = (int)Math.Min(left, 4096);
            var got = s.ReadAtLeast(buf.AsSpan(0, take * stride), take * stride, throwOnEndOfStream: false) / stride;
            for (var i = 0; i < got; i++)
            {
                for (var k = 0; k < values.Length; k++) values[k] = _ply.Value(buf.AsSpan(i * stride + offsets[k]), v.Props[k].Type);
                yield return values;
            }
            left -= got;
            if (got < take) yield break;
        }
    }

    bool Shown(double[] x, out double alpha)
    {
        alpha = Splat.Sigmoid(x[_opacity]);
        return alpha >= Splat.MinAlpha && double.IsFinite(x[_pos[0]]) && double.IsFinite(x[_pos[1]]) && double.IsFinite(x[_pos[2]]);
    }

    public IEnumerable<PointBatch> Read(int batch = 65536, CancellationToken cancel = default)
    {
        var b = new PointBatch(batch);
        foreach (var x in Records(cancel))
        {
            if (!Shown(x, out _)) continue;
            if (b.Full) { yield return b; b.Count = 0; }
            b.Add(x[_pos[0]], x[_pos[1]], x[_pos[2]], Splat.ColorOf(x[_dc[0]]), Splat.ColorOf(x[_dc[1]]), Splat.ColorOf(x[_dc[2]]));
        }
        if (b.Count > 0) yield return b;
    }

    public IEnumerable<SplatBatch> ReadSplats(int batch = 65536, bool sh = false, CancellationToken cancel = default)
    {
        var coeffs = sh ? Splat.ShCoeffs(ShDegree) : 0;
        var per = _rest.Length / 3;
        var b = new SplatBatch(batch, coeffs);
        foreach (var x in Records(cancel))
        {
            if (!Shown(x, out var alpha)) continue;
            if (b.Full) { yield return b; b.Count = 0; }
            var i = b.Add(x[_pos[0]], x[_pos[1]], x[_pos[2]],
                (float)Math.Exp(x[_scale[0]]), (float)Math.Exp(x[_scale[1]]), (float)Math.Exp(x[_scale[2]]),
                (float)x[_rot[0]], (float)x[_rot[1]], (float)x[_rot[2]], (float)x[_rot[3]],
                Splat.ColorOf(x[_dc[0]]), Splat.ColorOf(x[_dc[1]]), Splat.ColorOf(x[_dc[2]]), (byte)Math.Clamp(Math.Round(alpha * 255), 0, 255));
            // The file's channel by channel (red's coefficients, green's, blue's) into coefficient by coefficient, colours inner.
            for (var j = 0; j < coeffs; j++)
                for (var c = 0; c < 3; c++)
                    b.Sh[(i * coeffs + j) * 3 + c] = Splat.ShByte(x[_rest[c * per + j]]);
        }
        if (b.Count > 0) yield return b;
    }
}
