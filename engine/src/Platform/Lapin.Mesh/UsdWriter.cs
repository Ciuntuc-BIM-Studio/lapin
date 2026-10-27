using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Lapin.Mesh;

/// <summary>
/// A scene as OpenUSD, after the AOUSD Core Specification 1.0: the text
/// format (USDA) and its package (USDZ). Z up and metres, as the stage's
/// metadata say, so nothing turns. The project is the default prim with the
/// offset to real coordinates; under it the materials (UsdPreviewSurface,
/// linear colours) and the building, an Xform per storey, an Xform per
/// element carrying its identity (lapin:globalId, lapin:ifcClass,
/// lapin:type), and a Mesh per material bound to it. Edges, when the scene
/// has them, are linear BasisCurves. Prim names are made valid identifiers
/// and unique among their siblings; the element's own name is its displayName.
/// </summary>
public static class UsdWriter
{
    public const string LayerName = "model.usda";

    public static byte[] Usda(MeshScene scene) => Encoding.UTF8.GetBytes(Text(scene));

    /// <summary>A USDZ package: an uncompressed zip whose files' data each start on a 64-byte boundary, the root layer first.</summary>
    public static byte[] Usdz(MeshScene scene) =>
        Package([(LayerName, Usda(scene)), .. TextureFiles(scene).Select(t => (t.Path, t.Texture.Bytes))]);

    /// <summary>The materials' own images as the layer refers to them (textures/…), packed beside it in a USDZ.</summary>
    public static List<(int Material, string Path, MeshTexture Texture)> TextureFiles(MeshScene scene) =>
        scene.Materials.Select((m, i) => (m, i)).Where(x => x.m.Texture != null)
            .Select(x => (x.i, $"textures/{Identifier(x.m.Name)}_{x.i}.{(x.m.Texture!.Mime == "image/jpeg" ? "jpg" : "png")}", x.m.Texture!)).ToList();

    static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string F(float v) => (v == 0 ? 0f : v).ToString("R", CultureInfo.InvariantCulture);

    static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";

    /// <summary>A valid prim name: letters, digits and underscores, not starting with a digit.</summary>
    public static string Identifier(string name)
    {
        var sb = new StringBuilder();
        // Letters with diacritics as their base letter (the engine runs without ICU: no Unicode normalisation to lean on).
        foreach (var ch in name)
        {
            var c = Fold.TryGetValue(ch, out var b) ? b : ch;
            sb.Append(c < 128 && (char.IsLetterOrDigit(c) || c == '_') ? c : '_');
        }
        var id = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
        if (id.Length == 0) id = "Prim";
        return char.IsDigit(id[0]) ? "_" + id : id;
    }

    static readonly Dictionary<char, char> Fold = BuildFold();

    static Dictionary<char, char> BuildFold()
    {
        var map = new Dictionary<char, char>();
        void Add(string from, char to) { foreach (var c in from) map[c] = to; }
        Add("ăâàáäãåā", 'a'); Add("ĂÂÀÁÄÃÅĀ", 'A'); Add("îìíïī", 'i'); Add("ÎÌÍÏĪ", 'I');
        Add("șşśš", 's'); Add("ȘŞŚŠ", 'S'); Add("țţť", 't'); Add("ȚŢŤ", 'T');
        Add("èéêëēę", 'e'); Add("ÈÉÊËĒĘ", 'E'); Add("òóôöõøō", 'o'); Add("ÒÓÔÖÕØŌ", 'O');
        Add("ùúûüū", 'u'); Add("ÙÚÛÜŪ", 'U'); Add("çćč", 'c'); Add("ÇĆČ", 'C'); Add("ñń", 'n'); Add("ÑŃ", 'N');
        Add("žźż", 'z'); Add("ŽŹŻ", 'Z'); Add("ýÿ", 'y'); Add("Ý", 'Y'); Add("ďđ", 'd'); Add("ĎĐ", 'D'); Add("łľ", 'l'); Add("ŁĽ", 'L'); Add("řŕ", 'r'); Add("ŘŔ", 'R'); Add("ğ", 'g'); Add("Ğ", 'G');
        return map;
    }

    sealed class Names
    {
        readonly HashSet<string> _taken = [];
        public string Of(string name)
        {
            var id = Identifier(name);
            var unique = id;
            for (var k = 2; !_taken.Add(unique); k++) unique = $"{id}_{k}";
            return unique;
        }
    }

    static string Text(MeshScene scene)
    {
        var o = new StringBuilder();
        var root = Identifier(scene.Project);
        o.Append("#usda 1.0\n(\n");
        o.Append($"    defaultPrim = {Quote(root)}\n");
        o.Append("    doc = \"Lapin\"\n");
        o.Append("    metersPerUnit = 1\n");
        o.Append("    upAxis = \"Z\"\n");
        o.Append(")\n\n");
        o.Append($"def Xform {Quote(root)} (\n    kind = \"assembly\"\n)\n{{\n");
        var (rx, ry, rz) = scene.RootOffset;
        if (rx != 0 || ry != 0 || rz != 0)
        {
            o.Append($"    double3 xformOp:translate = ({F(rx)}, {F(ry)}, {F(rz)})\n");
            o.Append("    uniform token[] xformOpOrder = [\"xformOp:translate\"]\n");
        }

        // The materials: one UsdPreviewSurface each.
        var materialNames = new Names();
        var materialPaths = new List<string>();
        o.Append("\n    def Scope \"Materials\"\n    {\n");
        var files = TextureFiles(scene).ToDictionary(t => t.Material, t => t.Path);
        for (var mi = 0; mi < scene.Materials.Count; mi++)
        {
            var m = scene.Materials[mi];
            var name = materialNames.Of(m.Name);
            var path = $"/{root}/Materials/{name}";
            materialPaths.Add(path);
            o.Append($"        def Material {Quote(name)} (\n            displayName = {Quote(m.Name)}\n        )\n        {{\n");
            o.Append($"            token outputs:surface.connect = <{path}/Surface.outputs:surface>\n");
            if (m.Category != null) o.Append($"            custom string lapin:category = {Quote(m.Category)}\n");
            o.Append("\n            def Shader \"Surface\"\n            {\n");
            o.Append("                uniform token info:id = \"UsdPreviewSurface\"\n");
            // Its own image: the colour comes from it, read at the mesh's st.
            if (files.TryGetValue(mi, out var file)) o.Append($"                color3f inputs:diffuseColor.connect = <{path}/Image.outputs:rgb>\n");
            else o.Append($"                color3f inputs:diffuseColor = ({F(GltfWriter.Linear(m.R))}, {F(GltfWriter.Linear(m.G))}, {F(GltfWriter.Linear(m.B))})\n");
            o.Append("                float inputs:metallic = 0\n");
            if (m.Alpha < 1) o.Append($"                float inputs:opacity = {F(m.Alpha)}\n");
            o.Append("                float inputs:roughness = 0.9\n");
            o.Append("                token outputs:surface\n");
            o.Append("            }\n");
            if (files.TryGetValue(mi, out var image))
            {
                o.Append("\n            def Shader \"Image\"\n            {\n");
                o.Append("                uniform token info:id = \"UsdUVTexture\"\n");
                o.Append($"                asset inputs:file = @{image}@\n");
                o.Append($"                float2 inputs:st.connect = <{path}/St.outputs:result>\n");
                o.Append("                token inputs:wrapS = \"repeat\"\n                token inputs:wrapT = \"repeat\"\n");
                o.Append("                float3 outputs:rgb\n");
                o.Append("            }\n");
                o.Append("\n            def Shader \"St\"\n            {\n");
                o.Append("                uniform token info:id = \"UsdPrimvarReader_float2\"\n");
                o.Append("                string inputs:varname = \"st\"\n");
                o.Append("                float2 outputs:result\n");
                o.Append("            }\n");
            }
            o.Append("        }\n");
        }
        var edgePath = $"/{root}/Materials/{materialNames.Of("Edges")}";
        if (scene.Groups.Any(g => g.Elements.Any(e => e.Edges.Length >= 6)))
        {
            o.Append($"        def Material {Quote(edgePath[(edgePath.LastIndexOf('/') + 1)..])}\n        {{\n");
            o.Append($"            token outputs:surface.connect = <{edgePath}/Surface.outputs:surface>\n");
            o.Append("\n            def Shader \"Surface\"\n            {\n                uniform token info:id = \"UsdPreviewSurface\"\n                color3f inputs:diffuseColor = (0, 0, 0)\n                token outputs:surface\n            }\n        }\n");
        }
        o.Append("    }\n");

        // The building, its storeys, their elements.
        var top = new Names();
        top.Of("Materials");
        o.Append($"\n    def Xform {Quote(top.Of(scene.Building))} (\n        displayName = {Quote(scene.Building)}\n    )\n    {{\n");
        var groups = new Names();
        foreach (var g in scene.Groups)
        {
            o.Append($"        def Xform {Quote(groups.Of(g.Name))} (\n            displayName = {Quote(g.Name)}\n        )\n        {{\n");
            o.Append($"            custom string lapin:globalId = {Quote(g.GlobalId)}\n");
            o.Append($"            custom string lapin:ifcClass = {Quote(g.IfcClass)}\n");
            o.Append($"            custom double lapin:elevation = {F(g.Elevation)}\n");
            var elements = new Names();
            foreach (var e in g.Elements)
            {
                o.Append($"\n            def Xform {Quote(elements.Of(e.Name))} (\n                displayName = {Quote(e.Name)}\n            )\n            {{\n");
                o.Append($"                custom string lapin:globalId = {Quote(e.GlobalId)}\n");
                o.Append($"                custom string lapin:ifcClass = {Quote(e.IfcClass)}\n");
                if (e.Type != null) o.Append($"                custom string lapin:type = {Quote(e.Type)}\n");
                var parts = new Names();
                foreach (var p in e.Parts) Mesh(o, parts.Of(scene.Materials[p.Material].Name), p, materialPaths[p.Material]);
                if (e.Edges.Length >= 6) Curves(o, parts.Of("Edges"), e.Edges, edgePath);
                o.Append("            }\n");
            }
            o.Append("        }\n");
        }
        o.Append("    }\n}\n");
        return o.ToString();
    }

    static void Points(StringBuilder o, string type, string name, float[] xyz, string? meta = null)
    {
        o.Append($"                    {type} {name} = [");
        for (var i = 0; i + 2 < xyz.Length; i += 3)
        {
            if (i > 0) o.Append(", ");
            o.Append('(').Append(F(xyz[i])).Append(", ").Append(F(xyz[i + 1])).Append(", ").Append(F(xyz[i + 2])).Append(')');
        }
        o.Append(']');
        if (meta != null) o.Append(" (\n                        ").Append(meta).Append("\n                    )");
        o.Append('\n');
    }

    static void Extent(StringBuilder o, float[] xyz)
    {
        float[] min = [float.MaxValue, float.MaxValue, float.MaxValue], max = [float.MinValue, float.MinValue, float.MinValue];
        for (var i = 0; i + 2 < xyz.Length; i += 3)
            for (var k = 0; k < 3; k++) { min[k] = Math.Min(min[k], xyz[i + k]); max[k] = Math.Max(max[k], xyz[i + k]); }
        o.Append($"                    float3[] extent = [({F(min[0])}, {F(min[1])}, {F(min[2])}), ({F(max[0])}, {F(max[1])}, {F(max[2])})]\n");
    }

    static void Mesh(StringBuilder o, string name, MeshPart p, string material)
    {
        o.Append($"\n                def Mesh {Quote(name)} (\n                    prepend apiSchemas = [\"MaterialBindingAPI\"]\n                )\n                {{\n");
        Extent(o, p.Positions);
        o.Append("                    int[] faceVertexCounts = [");
        for (var i = 0; i < p.Indices.Length / 3; i++) o.Append(i > 0 ? ", 3" : "3");
        o.Append("]\n");
        o.Append("                    int[] faceVertexIndices = [");
        for (var i = 0; i < p.Indices.Length; i++) { if (i > 0) o.Append(", "); o.Append(p.Indices[i]); }
        o.Append("]\n");
        o.Append($"                    rel material:binding = <{material}>\n");
        // Flat shaded: every vertex belongs to one face, so its normal is per vertex.
        Points(o, "normal3f[]", "normals", p.Normals, "interpolation = \"vertex\"");
        o.Append("                    uniform token orientation = \"rightHanded\"\n");
        Points(o, "point3f[]", "points", p.Positions);
        if (p.Uvs is { } uv)
        {
            o.Append("                    texCoord2f[] primvars:st = [");
            for (var i = 0; i + 1 < uv.Length; i += 2) { if (i > 0) o.Append(", "); o.Append($"({F(uv[i])}, {F(uv[i + 1])})"); }
            o.Append("] (\n                        interpolation = \"vertex\"\n                    )\n");
        }
        o.Append("                    uniform token subdivisionScheme = \"none\"\n");
        o.Append("                }\n");
    }

    static void Curves(StringBuilder o, string name, float[] segments, string material)
    {
        o.Append($"\n                def BasisCurves {Quote(name)} (\n                    prepend apiSchemas = [\"MaterialBindingAPI\"]\n                )\n                {{\n");
        o.Append("                    uniform token type = \"linear\"\n");
        o.Append("                    int[] curveVertexCounts = [");
        for (var i = 0; i < segments.Length / 6; i++) o.Append(i > 0 ? ", 2" : "2");
        o.Append("]\n");
        Extent(o, segments);
        o.Append($"                    rel material:binding = <{material}>\n");
        Points(o, "point3f[]", "points", segments);
        o.Append("                    float[] widths = [0.005] (\n                        interpolation = \"constant\"\n                    )\n");
        o.Append("                }\n");
    }

    // ─── USDZ: an uncompressed zip, each file's data aligned to 64 bytes ────

    static readonly uint[] Crc = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    static uint Crc32(ReadOnlySpan<byte> data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in data) c = Crc[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    public static byte[] Package(IReadOnlyList<(string Name, byte[] Data)> files)
    {
        using var o = new MemoryStream();
        var central = new List<(string Name, uint Crc, int Size, int Offset, int Extra)>();
        // DOS time and date of 1.1.1980: a package says nothing of when it was made.
        const ushort Time = 0, Date = 0x21;
        foreach (var (name, data) in files)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);
            var offset = (int)o.Length;
            // The data starts after the 30-byte header, the name and an extra field padded so it lands on 64 bytes (an extra field is 4 bytes at least).
            var start = offset + 30 + nameBytes.Length;
            var extra = (64 - (start + 4) % 64) % 64 + 4;
            var crc = Crc32(data);
            var h = new byte[30].AsSpan();
            BinaryPrimitives.WriteUInt32LittleEndian(h, 0x04034B50);
            BinaryPrimitives.WriteUInt16LittleEndian(h[4..], 20);   // version needed
            BinaryPrimitives.WriteUInt16LittleEndian(h[6..], 0x0800); // UTF-8 names
            BinaryPrimitives.WriteUInt16LittleEndian(h[8..], 0);     // stored
            BinaryPrimitives.WriteUInt16LittleEndian(h[10..], Time);
            BinaryPrimitives.WriteUInt16LittleEndian(h[12..], Date);
            BinaryPrimitives.WriteUInt32LittleEndian(h[14..], crc);
            BinaryPrimitives.WriteUInt32LittleEndian(h[18..], (uint)data.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(h[22..], (uint)data.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(h[26..], (ushort)nameBytes.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(h[28..], (ushort)extra);
            o.Write(h);
            o.Write(nameBytes);
            var pad = new byte[extra];
            BinaryPrimitives.WriteUInt16LittleEndian(pad, 0x1986);   // an unregistered id, as the USD tools pad with
            BinaryPrimitives.WriteUInt16LittleEndian(pad.AsSpan(2), (ushort)(extra - 4));
            o.Write(pad);
            o.Write(data);
            central.Add((name, crc, data.Length, offset, extra));
        }
        var cdStart = (int)o.Length;
        foreach (var (name, crc, size, offset, _) in central)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);
            var c = new byte[46].AsSpan();
            BinaryPrimitives.WriteUInt32LittleEndian(c, 0x02014B50);
            BinaryPrimitives.WriteUInt16LittleEndian(c[4..], 20);
            BinaryPrimitives.WriteUInt16LittleEndian(c[6..], 20);
            BinaryPrimitives.WriteUInt16LittleEndian(c[8..], 0x0800);
            BinaryPrimitives.WriteUInt16LittleEndian(c[10..], 0);
            BinaryPrimitives.WriteUInt16LittleEndian(c[12..], Time);
            BinaryPrimitives.WriteUInt16LittleEndian(c[14..], Date);
            BinaryPrimitives.WriteUInt32LittleEndian(c[16..], crc);
            BinaryPrimitives.WriteUInt32LittleEndian(c[20..], (uint)size);
            BinaryPrimitives.WriteUInt32LittleEndian(c[24..], (uint)size);
            BinaryPrimitives.WriteUInt16LittleEndian(c[28..], (ushort)nameBytes.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(c[42..], (uint)offset);
            o.Write(c);
            o.Write(nameBytes);
        }
        var cdSize = (int)o.Length - cdStart;
        Span<byte> end = stackalloc byte[22];
        BinaryPrimitives.WriteUInt32LittleEndian(end, 0x06054B50);
        BinaryPrimitives.WriteUInt16LittleEndian(end[8..], (ushort)central.Count);
        BinaryPrimitives.WriteUInt16LittleEndian(end[10..], (ushort)central.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(end[12..], (uint)cdSize);
        BinaryPrimitives.WriteUInt32LittleEndian(end[16..], (uint)cdStart);
        o.Write(end);
        return o.ToArray();
    }
}
