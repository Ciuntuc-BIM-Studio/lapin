using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.PointClouds;

/// <summary>
/// PlayCanvas's SOG (Spatially Ordered Gaussians, as SuperSplat and SplatTransform write it), versions 2 and 3 after its
/// published specification (developer.playcanvas.com, "The SOG Format"): a <c>meta.json</c> and lossless WebP images, bundled
/// in a zip (<c>.sog</c>) or side by side in a folder. The same pixel of every image is one Gaussian (row by row from the top
/// left): <c>means_l</c> and <c>means_u</c> the low and high bytes of 16-bit positions over each axis' range, in a symmetric log
/// (sign(n)·(e^|n| − 1)); <c>scales</c> a codebook index an axis (log sizes); <c>quats</c> the rotation (version 3: a 24-bit
/// exponential map, (b − 128)/127 a component; version 2: the smallest three of w, x, y, z over ±√½ and the largest's index in
/// alpha − 252); <c>sh0</c> the colour's codebook indices and the opacity (version 3: (a/204)², above 1 kept at 1; version 2:
/// a/255); <c>shN</c>, when there, a palette of harmonics (64 entries a row, each its coefficients side by side, the channels
/// codebook indices) and a label a Gaussian (version 3: 24 bits; version 2: 16). WebP decoded by Lapin's own reader
/// (<see cref="WebPLossless"/>). Coordinates are the 3DGS PLY's: its link stands with −y up.
/// </summary>
public sealed class SogReader : ISplatReader
{
    readonly IPointSource _source;
    readonly JsonObject _meta;
    readonly int _version, _count, _fileBands;
    readonly Dictionary<string, WebPLossless.Image> _images = [];
    readonly Func<string, byte[]?> _file;

    public PointCloudHeader Header { get; }
    public int ShDegree { get; }

    /// <summary>A bundled SOG (a zip whose root holds meta.json), or a folder's meta.json beside its images.</summary>
    public SogReader(IPointSource source)
    {
        _source = source;
        byte[]? metaBytes;
        if (Path.GetFileName(source.Name).Equals("meta.json", StringComparison.OrdinalIgnoreCase))
        {
            if (source is not FileSource fs) throw new PointCloudException($"{source.Name}: a SOG folder's meta.json is read beside its images, on the desktop; link the bundled .sog file instead.");
            var dir = Path.GetDirectoryName(fs.Path)!;
            _file = n => Path.GetFileName(n) is { Length: > 0 } f && File.Exists(Path.Combine(dir, f)) ? File.ReadAllBytes(Path.Combine(dir, f)) : null;
            metaBytes = File.ReadAllBytes(fs.Path);
        }
        else
        {
            var entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var s = source.Open();
                using var zip = new ZipArchive(s, ZipArchiveMode.Read);
                foreach (var e in zip.Entries)
                {
                    if (e.Length == 0 && e.FullName.EndsWith('/')) continue;
                    if (e.Length > 1L << 30) throw new PointCloudException($"{source.Name}: {e.FullName} is larger than Lapin reads from a SOG file.");
                    using var es = e.Open();
                    var b = new byte[e.Length];
                    es.ReadExactly(b);
                    // At the root as the format says; one folder down is taken too (a zip of a folder).
                    entries.TryAdd(e.FullName, b);
                    entries.TryAdd(Path.GetFileName(e.FullName), b);
                }
            }
            catch (InvalidDataException) { throw new PointCloudException($"{source.Name} is not a SOG file (it is not a zip)."); }
            _file = n => entries.TryGetValue(n, out var b) || entries.TryGetValue(Path.GetFileName(n), out b) ? b : null;
            metaBytes = _file("meta.json");
        }
        if (metaBytes == null) throw new PointCloudException($"{source.Name}: a SOG file without its meta.json.");
        try { _meta = JsonNode.Parse(metaBytes) as JsonObject ?? throw new PointCloudException($"{source.Name}: its meta.json is not an object."); }
        catch (JsonException e) { throw new PointCloudException($"{source.Name}: its meta.json cannot be read ({e.Message})."); }
        _version = (int?)Num(_meta["version"]) ?? 0;
        if (_version == 0 && _meta["means"]?["shape"] != null)
            throw new PointCloudException($"{source.Name} is SOG's first layout (before version 2); convert it again with SplatTransform or SuperSplat.");
        if (_version is not (2 or 3)) throw new PointCloudException($"{source.Name} is SOG version {_version}; Lapin reads versions 2 and 3.");
        _count = (int?)Num(_meta["count"]) ?? throw new PointCloudException($"{source.Name}: its meta.json has no count.");
        if (_count < 0) throw new PointCloudException($"{source.Name}: its count is negative.");
        _fileBands = _meta["shN"] is JsonObject shn ? (int?)Num(shn["bands"]) ?? 0 : 0;
        if (_fileBands is < 0 or > 4) throw new PointCloudException($"{source.Name}: {_fileBands} harmonic bands, which SOG does not have.");
        ShDegree = Math.Min(3, _fileBands);
        var notes = new List<string>();
        if (_fileBands > 3) notes.Add($"{source.Name} has spherical harmonics of degree {_fileBands}; Lapin keeps degrees 1–3.");
        if (_meta["blind"] != null) notes.Add($"{source.Name} carries blind data, which Lapin leaves aside.");
        if ((string?)_meta["model"] == "2dgs") notes.Add($"{source.Name} holds 2D Gaussian surfels; Lapin draws them as flat Gaussians.");
        double[]? bounds = null;
        if (Floats(_meta["means"]?["mins"], 3) is { } mn && Floats(_meta["means"]?["maxs"], 3) is { } mx)
            bounds = [Unlog(mn[0]), Unlog(mn[1]), Unlog(mn[2]), Unlog(mx[0]), Unlog(mx[1]), Unlog(mx[2])];
        Header = new PointCloudHeader
        {
            Format = $"SOG version {_version} Gaussian splats{(ShDegree > 0 ? $", harmonics of degree {ShDegree}" : "")}", Count = _count, HasColor = true,
            Up = "-y", Splats = true, ShDegree = ShDegree, Notes = notes, Bounds = bounds,
        };
    }

    static double? Num(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;

    static float[]? Floats(JsonNode? n, int count)
    {
        if (n is not JsonArray a || a.Count < count) return null;
        var o = new float[a.Count];
        for (var i = 0; i < a.Count; i++) o[i] = (float)(Num(a[i]) ?? 0);
        return o;
    }

    static double Unlog(double n) => Math.Sign(n) * (Math.Exp(Math.Abs(n)) - 1);

    /// <summary>A property's codebook (256 floats) and its i-th file, decoded once.</summary>
    float[] Codebook(string prop) => Floats(_meta[prop]?["codebook"], 256) ?? throw new PointCloudException($"{_source.Name}: its {prop} has no codebook of 256 values.");

    WebPLossless.Image Image(string prop, int i, bool perGaussian = true)
    {
        var name = (string?)(_meta[prop]?["files"] as JsonArray)?.ElementAtOrDefault(i) ?? throw new PointCloudException($"{_source.Name}: its meta.json names no file for {prop}.");
        if (_images.TryGetValue(name, out var im)) return im;
        var bytes = _file(name) ?? throw new PointCloudException($"{_source.Name}: {name} is missing.");
        try { im = WebPLossless.Decode(bytes); }
        catch (WebPLossless.WebPException e) { throw new PointCloudException($"{_source.Name}: {name} is {e.Message}."); }
        if (perGaussian && (long)im.Width * im.Height < _count) throw new PointCloudException($"{_source.Name}: {name} has fewer pixels than its {_count:N0} Gaussians.");
        return _images[name] = im;
    }

    static int R(uint p) => (int)(p >> 16 & 0xff);
    static int G(uint p) => (int)(p >> 8 & 0xff);
    static int B(uint p) => (int)(p & 0xff);
    static int A(uint p) => (int)(p >> 24);

    IEnumerable<(int I, double X, double Y, double Z, float S0, float S1, float S2, float Qw, float Qx, float Qy, float Qz, byte R, byte G, byte B, byte A)> Gaussians(CancellationToken cancel)
    {
        var mins = Floats(_meta["means"]?["mins"], 3) ?? throw new PointCloudException($"{_source.Name}: its means have no mins.");
        var maxs = Floats(_meta["means"]?["maxs"], 3) ?? throw new PointCloudException($"{_source.Name}: its means have no maxs.");
        var lo = Image("means", 0).Argb;
        var hi = Image("means", 1).Argb;
        var sc = Image("scales", 0).Argb;
        var qt = Image("quats", 0).Argb;
        var c0 = Image("sh0", 0).Argb;
        var scb = Codebook("scales");
        var ccb = Codebook("sh0");
        for (var i = 0; i < _count; i++)
        {
            if ((i & 0xFFFF) == 0) cancel.ThrowIfCancellationRequested();
            var c = c0[i];
            var alpha = _version >= 3 ? Math.Min(1, Math.Pow(A(c) / 204.0, 2)) : A(c) / 255.0;
            if (alpha < Splat.MinAlpha) continue;
            uint l = lo[i], u = hi[i];
            double Axis(int k, int ql, int qu) => Unlog(mins[k] + (maxs[k] - mins[k]) * ((qu << 8 | ql) / 65535.0));
            var x = Axis(0, R(l), R(u));
            var y = Axis(1, G(l), G(u));
            var z = Axis(2, B(l), B(u));
            var s = sc[i];
            float qw, qx, qy, qz;
            var q = qt[i];
            if (_version >= 3) (qw, qx, qy, qz) = ExpMap(R(q), G(q), B(q));
            else
            {
                static float Comp(int b) => (b / 255f - 0.5f) * 2f / MathF.Sqrt(2);
                float a = Comp(R(q)), bb = Comp(G(q)), cc = Comp(B(q));
                var d = MathF.Sqrt(MathF.Max(0, 1 - a * a - bb * bb - cc * cc));
                (qw, qx, qy, qz) = (A(q) - 252) switch { 0 => (d, a, bb, cc), 1 => (a, d, bb, cc), 2 => (a, bb, d, cc), 3 => (a, bb, cc, d), _ => (1f, 0f, 0f, 0f) };
            }
            yield return (i, x, y, z, MathF.Exp(scb[R(s)]), MathF.Exp(scb[G(s)]), MathF.Exp(scb[B(s)]), qw, qx, qy, qz,
                Splat.ColorOf(ccb[R(c)]), Splat.ColorOf(ccb[G(c)]), Splat.ColorOf(ccb[B(c)]), (byte)Math.Clamp(Math.Round(alpha * 255), 0, 255));
        }
    }

    /// <summary>Version 3's rotation: a point in the unit ball, a half turn on its sphere (§3.2).</summary>
    internal static (float W, float X, float Y, float Z) ExpMap(int r, int g, int b)
    {
        double px = (r - 128) / 127.0, py = (g - 128) / 127.0, pz = (b - 128) / 127.0;
        var len = Math.Sqrt(px * px + py * py + pz * pz);
        var theta = Math.PI / 2 * len;
        var k = len > 0 ? Math.Sin(theta) / len : 0;
        return ((float)Math.Cos(theta), (float)(px * k), (float)(py * k), (float)(pz * k));
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
        var coeffs = sh && ShDegree > 0 ? Splat.ShCoeffs(ShDegree) : 0;
        var b = new SplatBatch(batch, coeffs);
        uint[]? labels = null, centroids = null;
        float[]? cb = null;
        var fileCoeffs = Splat.ShCoeffs(_fileBands);
        var entries = 0;
        var width = 0;
        if (coeffs > 0)
        {
            labels = Image("shN", 1).Argb;
            var cim = Image("shN", 0, perGaussian: false);
            centroids = cim.Argb;
            width = cim.Width;
            cb = Codebook("shN");
            entries = (int?)Num(_meta["shN"]?["count"]) ?? 0;
            if (width != 64 * fileCoeffs) throw new PointCloudException($"{_source.Name}: its harmonics' palette is {width} pixels wide, not {64 * fileCoeffs}.");
        }
        foreach (var g in Gaussians(cancel))
        {
            if (b.Full) { yield return b; b.Count = 0; }
            var i = b.Add(g.X, g.Y, g.Z, g.S0, g.S1, g.S2, g.Qw, g.Qx, g.Qy, g.Qz, g.R, g.G, g.B, g.A);
            if (labels == null) continue;
            var p = labels[g.I];
            var label = _version >= 3 ? R(p) | G(p) << 8 | B(p) << 16 : R(p) | G(p) << 8;
            if (label >= entries || (label / 64 + 1) * width > centroids!.Length)
            {
                b.Sh.AsSpan(i * coeffs * 3, coeffs * 3).Fill(Splat.ShByte(0)); // a label outside the palette: the base colour only
                continue;
            }
            var row = label / 64 * width + label % 64 * fileCoeffs;
            for (var j = 0; j < coeffs; j++)
            {
                var px = centroids[row + j];
                var o = (i * coeffs + j) * 3;
                b.Sh[o] = Splat.ShByte(cb![R(px)]);
                b.Sh[o + 1] = Splat.ShByte(cb[G(px)]);
                b.Sh[o + 2] = Splat.ShByte(cb[B(px)]);
            }
        }
        if (b.Count > 0) yield return b;
    }
}
