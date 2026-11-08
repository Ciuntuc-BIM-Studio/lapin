using System.Globalization;

namespace Lapin.PointClouds;

/// <summary>
/// Points as text, a point a line: XYZ, PTS (Leica's open text: a count line, then x y z intensity r g b, intensity −2048…2047)
/// and CSV/TXT with or without a heading line. Separated by spaces, tabs, commas or semicolons (a decimal comma when the
/// separator is a semicolon or a tab). Columns by their heading (x, y, z, r/red, g/green, b/blue, i/intensity,
/// class/classification) or, without one, by their count: 3 x y z; 4 x y z i; 6 x y z r g b; 7 x y z i r g b.
/// </summary>
public sealed class TextPointReader : IPointReader
{
    readonly IPointSource _source;
    readonly char[] _separators;
    readonly bool _decimalComma;
    readonly int _skip;
    readonly int _x, _y, _z, _r = -1, _g = -1, _b = -1, _i = -1, _c = -1;
    /// <summary>How intensity is read: 0 as 16 bits, 1 a 0–1 float, 2 PTS's −2048…2047, 3 0–255.</summary>
    readonly int _intensityKind;
    readonly bool _colorFloat;
    public PointCloudHeader Header { get; }

    public TextPointReader(IPointSource source)
    {
        _source = source;
        var sample = new List<string>();
        using (var r = new StreamReader(source.Open()))
            for (string? l; sample.Count < 2000 && (l = r.ReadLine()) != null;)
                if (l.Trim().Length > 0) sample.Add(l.Trim());
        if (sample.Count == 0) throw new PointCloudException($"{source.Name} is empty.");
        var ext = Path.GetExtension(source.Name).ToLowerInvariant();
        var first = 0;
        long? count = null;
        // PTS: its first line is the number of points.
        if (sample[0].All(char.IsDigit) && sample.Count > 1) { count = long.Parse(sample[0], CultureInfo.InvariantCulture); first = 1; }
        var probe = sample.Skip(first).Take(50).ToList();
        _separators = probe.Any(l => l.Contains(';')) ? [';'] : probe.Any(l => l.Contains('\t')) ? ['\t'] : probe.Any(l => l.Contains(',')) && !ext.Equals(".pts") ? [','] : [' '];
        _decimalComma = _separators[0] is ';' or '\t' && probe.Any(l => l.Contains(','));
        var head = Split(probe[0]);
        string[]? names = null;
        if (head.Any(t => !double.TryParse(Fix(t), NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
        {
            names = head.Select(t => t.Trim().Trim('"', '/', '#', '/').ToLowerInvariant()).ToArray();
            first++;
            probe = probe.Skip(1).ToList();
        }
        _skip = first;
        if (probe.Count == 0) throw new PointCloudException($"{source.Name} has no points.");
        var cols = Split(probe[0]).Length;
        if (cols < 3) throw new PointCloudException($"{source.Name}: a line has fewer than three numbers (x, y, z).");
        if (names != null)
        {
            int At(params string[] keys) { foreach (var k in keys) { var i = Array.IndexOf(names, k); if (i >= 0) return i; } return -1; }
            _x = At("x", "//x", "easting", "e"); _y = At("y", "northing", "n"); _z = At("z", "elevation", "h", "height");
            if (_x < 0 || _y < 0 || _z < 0) (_x, _y, _z) = (0, 1, 2);
            _r = At("r", "red"); _g = At("g", "green"); _b = At("b", "blue");
            _i = At("i", "intensity", "scalar_intensity", "reflectance");
            _c = At("class", "classification", "scalar_classification", "label");
        }
        else
        {
            (_x, _y, _z) = (0, 1, 2);
            switch (cols)
            {
                case 4: _i = 3; break;
                case 6: (_r, _g, _b) = (3, 4, 5); break;
                case >= 7: _i = 3; (_r, _g, _b) = (4, 5, 6); break;
            }
        }
        if (_r < 0 || _g < 0 || _b < 0) _r = _g = _b = -1;
        // Intensity and colour ranges from the sample.
        var nums = probe.Select(l => Split(l).Select(t => double.TryParse(Fix(t), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0).ToArray()).Where(a => a.Length >= cols).ToList();
        if (_i >= 0 && nums.Count > 0)
        {
            var (lo, hi) = (nums.Min(a => a[_i]), nums.Max(a => a[_i]));
            var fractional = nums.Any(a => a[_i] != Math.Floor(a[_i]));
            _intensityKind = ext == ".pts" || lo < 0 ? 2 : hi <= 1.0001 && fractional ? 1 : hi <= 255 ? 3 : 0;
        }
        if (_r >= 0 && nums.Count > 0) _colorFloat = nums.All(a => a[_r] <= 1.0001 && a[_g] <= 1.0001 && a[_b] <= 1.0001) && nums.Any(a => a[_r] != Math.Floor(a[_r]));
        Header = new PointCloudHeader
        {
            Format = ext switch { ".pts" => "PTS", ".csv" => "CSV", ".xyz" => "XYZ", _ => "Text" },
            Count = count, HasColor = _r >= 0, HasIntensity = _i >= 0, HasClassification = _c >= 0,
        };
    }

    string Fix(string t) => _decimalComma ? t.Replace(',', '.') : t;
    string[] Split(string l) => l.Split(_separators[0] == ' ' ? [' ', '\t'] : _separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IEnumerable<PointBatch> Read(int batch = 65536, CancellationToken cancel = default)
    {
        using var r = new StreamReader(_source.Open(), bufferSize: 1 << 16);
        var b = new PointBatch(batch);
        var skipped = 0;
        var n = 0L;
        for (string? l; (l = r.ReadLine()) != null;)
        {
            if ((++n & 0xFFFF) == 0) cancel.ThrowIfCancellationRequested();
            var span = l.AsSpan().Trim();
            if (span.Length == 0) continue;
            if (skipped < _skip) { skipped++; continue; }
            var t = Split(l);
            var need = Math.Max(Math.Max(_x, _y), Math.Max(_z, Math.Max(_r, Math.Max(_i, _c))));
            if (t.Length <= need) continue;
            if (!Num(t[_x], out var x) || !Num(t[_y], out var y) || !Num(t[_z], out var z)) continue;
            ushort cr = 0, cg = 0, cb = 0, it = 0;
            byte cls = 0;
            if (_r >= 0 && Num(t[_r], out var rv) && Num(t[_g], out var gv) && Num(t[_b], out var bv))
            {
                if (_colorFloat) (rv, gv, bv) = (rv * 255, gv * 255, bv * 255);
                (cr, cg, cb) = ((ushort)Math.Clamp(rv, 0, 65535), (ushort)Math.Clamp(gv, 0, 65535), (ushort)Math.Clamp(bv, 0, 65535));
            }
            if (_i >= 0 && Num(t[_i], out var iv))
                it = (ushort)Math.Clamp(_intensityKind switch { 1 => iv * 65535, 2 => (iv + 2048) * 16, 3 => iv * 257, _ => iv }, 0, 65535);
            if (_c >= 0 && Num(t[_c], out var cv)) cls = (byte)Math.Clamp(cv, 0, 255);
            if (b.Full) { yield return b; b.Count = 0; }
            b.Add(x, y, z, cr, cg, cb, it, cls);
        }
        if (b.Count > 0) yield return b;
    }

    bool Num(string t, out double v) => double.TryParse(Fix(t), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
}
