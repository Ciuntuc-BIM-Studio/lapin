using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Lapin.Geometry;

namespace Lapin.Mesh;

/// <summary>
/// A PLY mesh (Greg Turk's polygon file format, as Paul Bourke documents it; docs/plan-gaussian-splats.md, G2): ascii, binary
/// little and big endian; the vertices' x, y, z and their colour (red/green/blue as bytes, or 0–1 floats), the faces' lists of
/// vertex indices (<c>vertex_indices</c> or <c>vertex_index</c>, fanned into triangles) and a face's own colour when it has one.
/// MeshSplatting and photogrammetry programs write such meshes coloured by vertex; Lapin colours a body by part, so the faces
/// are gathered by their colour, 3 bits a channel (at most 512 parts), into one element. Other elements are skipped.
/// </summary>
public static class PlyMesh
{
    sealed record Prop(string Name, string Type, string? ListCount);
    sealed record Element(string Name, long Count, List<Prop> Props);

    public static List<MeshPiece> Read(byte[] bytes, string name, List<string> notes)
    {
        // The header: lines up to end_header.
        var end = IndexOf(bytes, "end_header"u8);
        if (end < 0 || !bytes.AsSpan().StartsWith("ply"u8)) throw new FormatException("it is not a PLY file");
        var at = end + "end_header".Length;
        while (at < bytes.Length && bytes[at] != '\n') at++;
        at++;
        var lines = Encoding.ASCII.GetString(bytes, 0, end).Split('\n').Select(l => l.Trim()).ToList();
        string? format = null;
        var elements = new List<Element>();
        foreach (var l in lines)
        {
            var t = l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length == 0) continue;
            if (t[0] == "format" && t.Length > 1) format = t[1];
            else if (t[0] == "element" && t.Length >= 3) elements.Add(new Element(t[1], long.Parse(t[2], CultureInfo.InvariantCulture), []));
            else if (t[0] == "property" && elements.Count > 0)
            {
                if (t.Length >= 5 && t[1] == "list") elements[^1].Props.Add(new Prop(t[4], t[3], t[2]));
                else if (t.Length >= 3) elements[^1].Props.Add(new Prop(t[2], t[1], null));
            }
        }
        if (format is not ("ascii" or "binary_little_endian" or "binary_big_endian")) throw new FormatException($"its PLY format „{format}” is not one Lapin knows");
        var vertex = elements.FirstOrDefault(e => e.Name == "vertex") ?? throw new FormatException("it has no vertices");
        var face = elements.FirstOrDefault(e => e.Name == "face") ?? throw new FormatException("it has no faces: link it as a point cloud");
        if (vertex.Count > 50_000_000 || face.Count > 100_000_000) throw new FormatException("it is larger than Lapin imports as a mesh");

        var reader = new Cursor(bytes, at, format);
        var verts = new List<Vec3>((int)vertex.Count);
        var vcol = new List<(byte R, byte G, byte B)?>((int)vertex.Count);
        var faces = new List<(int[] Corners, (byte R, byte G, byte B)? Color)>((int)face.Count);
        foreach (var e in elements)
        {
            int ix = e.Props.FindIndex(p => p.Name == "x"), iy = e.Props.FindIndex(p => p.Name == "y"), iz = e.Props.FindIndex(p => p.Name == "z");
            var rgb = Rgb(e);
            var list = e.Props.FindIndex(p => p.ListCount != null && p.Name is "vertex_indices" or "vertex_index");
            var values = new double[e.Props.Count];
            int[]? corners = null;
            for (long k = 0; k < e.Count; k++)
            {
                reader.Record();
                for (var j = 0; j < e.Props.Count; j++)
                {
                    var p = e.Props[j];
                    if (p.ListCount is { } lc)
                    {
                        var n = (int)reader.Value(lc);
                        if (n < 0 || n > 1_000_000) throw new FormatException("a face has a list of corners Lapin cannot read");
                        if (e == face && j == list)
                        {
                            corners = new int[n];
                            for (var c = 0; c < n; c++) corners[c] = (int)reader.Value(p.Type);
                        }
                        else for (var c = 0; c < n; c++) reader.Value(p.Type);
                    }
                    else values[j] = reader.Value(p.Type);
                }
                if (e == vertex)
                {
                    if (ix < 0 || iy < 0 || iz < 0) throw new FormatException("its vertices have no x, y and z");
                    verts.Add(new Vec3(values[ix], values[iy], values[iz]));
                    vcol.Add(rgb is { } v ? (Byte(values[v[0]], e.Props[v[0]].Type), Byte(values[v[1]], e.Props[v[1]].Type), Byte(values[v[2]], e.Props[v[2]].Type)) : null);
                }
                else if (e == face)
                {
                    if (corners == null) throw new FormatException("its faces have no vertex_indices");
                    faces.Add((corners, rgb is { } v ? (Byte(values[v[0]], e.Props[v[0]].Type), Byte(values[v[1]], e.Props[v[1]].Type), Byte(values[v[2]], e.Props[v[2]].Type)) : null));
                    corners = null;
                }
            }
        }

        // Each face's colour (its own, else its corners' mean), 3 bits a channel: one part a colour.
        var coloured = faces.Any(f => f.Color != null) || vcol.Any(c => c != null);
        var parts = new Dictionary<int, (List<Vec3> V, Dictionary<int, int> Map, List<(int, int, int)> T)>();
        var skipped = 0;
        foreach (var (c, own) in faces)
        {
            if (c.Length < 3 || c.Any(i => i < 0 || i >= verts.Count)) { skipped++; continue; }
            var key = 0;
            if (coloured)
            {
                var col = own ?? Mean(c.Select(i => vcol[i]));
                key = col is { } m ? (m.R >> 5) << 6 | (m.G >> 5) << 3 | (m.B >> 5) : -1;
            }
            if (!parts.TryGetValue(key, out var part)) parts[key] = part = ([], [], []);
            int Corner(int i)
            {
                if (part.Map.TryGetValue(i, out var j)) return j;
                part.Map[i] = part.V.Count;
                part.V.Add(verts[i]);
                return part.V.Count - 1;
            }
            for (var k = 1; k + 1 < c.Length; k++) part.T.Add((Corner(c[0]), Corner(c[k]), Corner(c[k + 1])));
        }
        if (skipped > 0) notes.Add($"{skipped} face{(skipped == 1 ? "" : "s")} of {name} named vertices it does not have, and were left out.");
        if (coloured && parts.Count > 1) notes.Add($"{name}'s colours were gathered into {parts.Count} parts (3 bits a channel).");
        var element = Path.GetFileNameWithoutExtension(name);
        return parts.OrderBy(kv => kv.Key).Select(kv => new MeshPiece(parts.Count == 1 ? element : $"{element} {Hex(kv.Key)}", Hex(kv.Key), kv.Value.V, kv.Value.T, Element: element)).ToList();
    }

    static string Hex(int key)
    {
        if (key < 0) return "#c9ced6";
        // The middle of the bin.
        static int Mid(int v) => v * 32 + 16;
        return $"#{Mid((key >> 6) & 7):x2}{Mid((key >> 3) & 7):x2}{Mid(key & 7):x2}";
    }

    static (byte R, byte G, byte B)? Mean(IEnumerable<(byte R, byte G, byte B)?> cs)
    {
        int r = 0, g = 0, b = 0, n = 0;
        foreach (var c in cs) if (c is { } v) { r += v.R; g += v.G; b += v.B; n++; }
        return n == 0 ? null : ((byte)(r / n), (byte)(g / n), (byte)(b / n));
    }

    static int[]? Rgb(Element e)
    {
        foreach (var set in new[] { new[] { "red", "green", "blue" }, ["r", "g", "b"], ["diffuse_red", "diffuse_green", "diffuse_blue"] })
        {
            var idx = set.Select(n => e.Props.FindIndex(p => p.ListCount == null && p.Name.Equals(n, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (idx.All(i => i >= 0)) return idx;
        }
        return null;
    }

    static byte Byte(double v, string type) => type is "float" or "float32" or "double" or "float64"
        ? (byte)Math.Clamp(Math.Round(v * 255), 0, 255)
        : type is "ushort" or "uint16" ? (byte)Math.Clamp(v / 257, 0, 255) : (byte)Math.Clamp(v, 0, 255);

    static int IndexOf(byte[] b, ReadOnlySpan<byte> what)
    {
        var limit = Math.Min(b.Length, 1 << 20);
        var i = b.AsSpan(0, limit).IndexOf(what);
        return i;
    }

    /// <summary>The body's values one after another: tokens of ascii lines, or binary numbers either way round.</summary>
    sealed class Cursor(byte[] b, int at, string format)
    {
        int _at = at;
        string[] _tokens = [];
        int _next;

        /// <summary>A new record: in ascii, its line.</summary>
        public void Record()
        {
            if (format != "ascii") return;
            while (true)
            {
                if (_at >= b.Length) throw new FormatException("its data is cut short");
                var nl = Array.IndexOf(b, (byte)'\n', _at);
                var stop = nl < 0 ? b.Length : nl;
                var line = Encoding.ASCII.GetString(b, _at, stop - _at);
                _at = stop + 1;
                _tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                _next = 0;
                if (_tokens.Length > 0) return;
            }
        }

        public double Value(string type)
        {
            if (format == "ascii")
            {
                if (_next >= _tokens.Length) throw new FormatException("a line has fewer values than its element");
                return double.Parse(_tokens[_next++], NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            var big = format == "binary_big_endian";
            var size = type switch { "char" or "int8" or "uchar" or "uint8" => 1, "short" or "int16" or "ushort" or "uint16" => 2, "int" or "int32" or "uint" or "uint32" or "float" or "float32" => 4, "double" or "float64" => 8, _ => throw new FormatException($"the PLY type {type} is not one Lapin knows") };
            if (_at + size > b.Length) throw new FormatException("its data is cut short");
            var s = b.AsSpan(_at, size);
            _at += size;
            return type switch
            {
                "char" or "int8" => (sbyte)s[0],
                "uchar" or "uint8" => s[0],
                "short" or "int16" => big ? BinaryPrimitives.ReadInt16BigEndian(s) : BinaryPrimitives.ReadInt16LittleEndian(s),
                "ushort" or "uint16" => big ? BinaryPrimitives.ReadUInt16BigEndian(s) : BinaryPrimitives.ReadUInt16LittleEndian(s),
                "int" or "int32" => big ? BinaryPrimitives.ReadInt32BigEndian(s) : BinaryPrimitives.ReadInt32LittleEndian(s),
                "uint" or "uint32" => big ? BinaryPrimitives.ReadUInt32BigEndian(s) : BinaryPrimitives.ReadUInt32LittleEndian(s),
                "float" or "float32" => big ? BinaryPrimitives.ReadSingleBigEndian(s) : BinaryPrimitives.ReadSingleLittleEndian(s),
                _ => big ? BinaryPrimitives.ReadDoubleBigEndian(s) : BinaryPrimitives.ReadDoubleLittleEndian(s),
            };
        }
    }
}
