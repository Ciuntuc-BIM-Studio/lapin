using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Lapin.PointClouds;

/// <summary>
/// PLY (Greg Turk's polygon file format, as Paul Bourke documents it): ascii, binary little and big endian. The vertex
/// element's x, y, z, its colour (red/green/blue, r/g/b or diffuse_*, 8 or 16 bits, or 0–1 floats), its intensity and its
/// classification (scalar_* as CloudCompare writes them) are read; faces and other elements are skipped.
/// </summary>
public sealed class PlyReader : IPointReader
{
    enum Encoding { Ascii, Little, Big }
    sealed record Prop(string Name, string Type, string? ListCount = null);
    sealed record Element(string Name, long Count, List<Prop> Props);

    readonly IPointSource _source;
    readonly Encoding _enc;
    readonly long _dataAt;
    readonly List<Element> _elements = [];
    public PointCloudHeader Header { get; }

    public PlyReader(IPointSource source)
    {
        _source = source;
        using var s = source.Open();
        var lines = new List<string>();
        var line = new StringBuilder();
        long at = 0;
        while (true)
        {
            var c = s.ReadByte();
            if (c < 0) throw new PointCloudException($"{source.Name}: its PLY header has no end_header.");
            at++;
            if (c == '\n')
            {
                var l = line.ToString().TrimEnd('\r').Trim();
                line.Clear();
                lines.Add(l);
                if (l == "end_header") break;
                if (lines.Count > 10000) throw new PointCloudException($"{source.Name}: its PLY header does not end.");
            }
            else line.Append((char)c);
        }
        _dataAt = at;
        if (lines[0] != "ply") throw new PointCloudException($"{source.Name} is not a PLY file.");
        Encoding? enc = null;
        foreach (var l in lines.Skip(1))
        {
            var t = l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length == 0) continue;
            switch (t[0])
            {
                case "format":
                    enc = t.Length > 1 ? t[1] switch { "ascii" => Encoding.Ascii, "binary_little_endian" => Encoding.Little, "binary_big_endian" => Encoding.Big, _ => null } : null;
                    break;
                case "element" when t.Length >= 3:
                    _elements.Add(new Element(t[1], long.Parse(t[2], CultureInfo.InvariantCulture), []));
                    break;
                case "property" when _elements.Count > 0:
                    if (t.Length >= 5 && t[1] == "list") _elements[^1].Props.Add(new Prop(t[4], t[3], t[2]));
                    else if (t.Length >= 3) _elements[^1].Props.Add(new Prop(t[2], t[1]));
                    break;
            }
        }
        _enc = enc ?? throw new PointCloudException($"{source.Name}: its PLY header names no format Lapin knows (ascii, binary_little_endian, binary_big_endian).");
        var v = Vertex ?? throw new PointCloudException($"{source.Name}: the PLY file has no vertex element.");
        if (v.Props.Any(p => p.ListCount != null)) throw new PointCloudException($"{source.Name}: its vertices carry lists, which Lapin does not read.");
        var names = v.Props.Select(p => p.Name).ToHashSet();
        if (!names.Contains("x") || !names.Contains("y") || !names.Contains("z")) throw new PointCloudException($"{source.Name}: its vertices have no x, y and z.");
        foreach (var p in v.Props) if (Size(p.Type) == 0) throw new PointCloudException($"{source.Name}: the PLY type {p.Type} is not one Lapin knows.");
        Header = new PointCloudHeader
        {
            Format = $"PLY {(_enc == Encoding.Ascii ? "ascii" : "binary")}", Count = v.Count,
            HasColor = ColorProps(v) != null, HasIntensity = Find(v, IntensityNames) != null, HasClassification = Find(v, ClassNames) != null,
        };
    }

    Element? Vertex => _elements.FirstOrDefault(e => e.Name == "vertex");
    static readonly string[] IntensityNames = ["intensity", "scalar_intensity", "scalar_Intensity", "reflectance", "scalar_reflectance"];
    static readonly string[] ClassNames = ["classification", "scalar_classification", "scalar_Classification", "class", "label"];

    static int? Find(Element e, string[] names)
    {
        for (var i = 0; i < e.Props.Count; i++) if (names.Contains(e.Props[i].Name, StringComparer.OrdinalIgnoreCase)) return i;
        return null;
    }

    static int[]? ColorProps(Element e)
    {
        foreach (var set in new[] { new[] { "red", "green", "blue" }, ["r", "g", "b"], ["diffuse_red", "diffuse_green", "diffuse_blue"] })
        {
            var idx = set.Select(n => e.Props.FindIndex(p => p.Name.Equals(n, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (idx.All(i => i >= 0)) return idx;
        }
        return null;
    }

    static int Size(string type) => type switch
    {
        "char" or "int8" or "uchar" or "uint8" => 1,
        "short" or "int16" or "ushort" or "uint16" => 2,
        "int" or "int32" or "uint" or "uint32" or "float" or "float32" => 4,
        "double" or "float64" => 8,
        _ => 0,
    };

    double Value(ReadOnlySpan<byte> b, string type)
    {
        var big = _enc == Encoding.Big;
        return type switch
        {
            "char" or "int8" => (sbyte)b[0],
            "uchar" or "uint8" => b[0],
            "short" or "int16" => big ? BinaryPrimitives.ReadInt16BigEndian(b) : BinaryPrimitives.ReadInt16LittleEndian(b),
            "ushort" or "uint16" => big ? BinaryPrimitives.ReadUInt16BigEndian(b) : BinaryPrimitives.ReadUInt16LittleEndian(b),
            "int" or "int32" => big ? BinaryPrimitives.ReadInt32BigEndian(b) : BinaryPrimitives.ReadInt32LittleEndian(b),
            "uint" or "uint32" => big ? BinaryPrimitives.ReadUInt32BigEndian(b) : BinaryPrimitives.ReadUInt32LittleEndian(b),
            "float" or "float32" => big ? BinaryPrimitives.ReadSingleBigEndian(b) : BinaryPrimitives.ReadSingleLittleEndian(b),
            _ => big ? BinaryPrimitives.ReadDoubleBigEndian(b) : BinaryPrimitives.ReadDoubleLittleEndian(b),
        };
    }

    /// <summary>A colour channel as 8 or 16 bits: floats are 0–1, 16-bit types are kept 16 bits (the index scales them).</summary>
    static ushort Channel(double v, string type) => type is "float" or "float32" or "double" or "float64"
        ? (ushort)Math.Clamp(Math.Round(v * 255), 0, 255)
        : (ushort)Math.Clamp(v, 0, 65535);

    static ushort IntensityOf(double v, string type) => type is "float" or "float32" or "double" or "float64"
        ? (ushort)Math.Clamp(Math.Round(v <= 1.0001 ? v * 65535 : v), 0, 65535)
        : type is "uchar" or "uint8" or "char" or "int8" ? (ushort)Math.Clamp(v * 257, 0, 65535) : (ushort)Math.Clamp(v, 0, 65535);

    public IEnumerable<PointBatch> Read(int batch = 65536, CancellationToken cancel = default)
    {
        var v = Vertex!;
        int ix = v.Props.FindIndex(p => p.Name == "x"), iy = v.Props.FindIndex(p => p.Name == "y"), iz = v.Props.FindIndex(p => p.Name == "z");
        var rgb = ColorProps(v);
        var ii = Find(v, IntensityNames);
        var ic = Find(v, ClassNames);
        using var s = _source.Open();
        s.Position = _dataAt;
        var b = new PointBatch(batch);
        var values = new double[v.Props.Count];
        if (_enc == Encoding.Ascii)
        {
            using var reader = new StreamReader(s, System.Text.Encoding.ASCII, false, 1 << 16);
            // Elements before the vertices: their lines skipped.
            foreach (var e in _elements.TakeWhile(e => e != v)) for (long k = 0; k < e.Count; k++) reader.ReadLine();
            for (long n = 0; n < v.Count; n++)
            {
                if ((n & 0xFFFF) == 0) cancel.ThrowIfCancellationRequested();
                var line = reader.ReadLine();
                if (line == null) break;
                var t = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (t.Length < v.Props.Count) continue;
                for (var k = 0; k < values.Length; k++) values[k] = double.TryParse(t[k], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
                if (b.Full) { yield return b; b.Count = 0; }
                Add(b, values);
            }
        }
        else
        {
            // Elements before the vertices skipped: fixed-size ones at once, ones with lists item by item.
            foreach (var e in _elements.TakeWhile(e => e != v)) Skip(s, e);
            var offsets = new int[v.Props.Count];
            var stride = 0;
            for (var k = 0; k < offsets.Length; k++) { offsets[k] = stride; stride += Size(v.Props[k].Type); }
            var buf = new byte[stride * 8192];
            long left = v.Count;
            while (left > 0)
            {
                cancel.ThrowIfCancellationRequested();
                var take = (int)Math.Min(left, 8192);
                var got = s.ReadAtLeast(buf.AsSpan(0, take * stride), take * stride, throwOnEndOfStream: false) / stride;
                for (var i = 0; i < got; i++)
                {
                    var rec = buf.AsSpan(i * stride, stride);
                    for (var k = 0; k < values.Length; k++) values[k] = Value(rec[offsets[k]..], v.Props[k].Type);
                    if (b.Full) { yield return b; b.Count = 0; }
                    Add(b, values);
                }
                left -= got;
                if (got < take) break;
            }
        }
        if (b.Count > 0) yield return b;

        void Add(PointBatch to, double[] x)
        {
            ushort r = 0, g = 0, bl = 0;
            if (rgb != null) { r = Channel(x[rgb[0]], v.Props[rgb[0]].Type); g = Channel(x[rgb[1]], v.Props[rgb[1]].Type); bl = Channel(x[rgb[2]], v.Props[rgb[2]].Type); }
            to.Add(x[ix], x[iy], x[iz], r, g, bl, ii is { } i ? IntensityOf(x[i], v.Props[i].Type) : (ushort)0, ic is { } c ? (byte)Math.Clamp(x[c], 0, 255) : (byte)0);
        }
    }

    void Skip(Stream s, Element e)
    {
        var fixedSize = e.Props.All(p => p.ListCount == null) ? e.Props.Sum(p => Size(p.Type)) : -1;
        if (fixedSize >= 0) { s.Seek(fixedSize * e.Count, SeekOrigin.Current); return; }
        var tmp = new byte[8];
        for (long k = 0; k < e.Count; k++)
            foreach (var p in e.Props)
            {
                if (p.ListCount is { } lc)
                {
                    var n = Size(lc);
                    s.ReadExactly(tmp.AsSpan(0, n));
                    var count = (long)Value(tmp, lc);
                    s.Seek(count * Size(p.Type), SeekOrigin.Current);
                }
                else s.Seek(Size(p.Type), SeekOrigin.Current);
            }
    }
}
