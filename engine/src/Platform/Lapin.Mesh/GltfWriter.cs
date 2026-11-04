using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Lapin.Platform;

namespace Lapin.Mesh;

/// <summary>
/// A scene as glTF 2.0 (Khronos), written after the specification: one node
/// for the project (its offset to real coordinates), the building, a node per
/// storey, a node per element with its mesh (an assembly a node over its
/// parts' nodes), a primitive per material, and the element's identity in
/// <c>extras</c> (GlobalId, IFC class, type). Y up
/// as glTF asks (the model's Z up turns: x, z, −y), metres; base colours in
/// linear RGB with each material's metallic and roughness, glass through
/// KHR_materials_transmission and KHR_materials_ior, blended where a material is
/// transparent. Edges, when the scene has them, are a LINES primitive.
/// GLB is one binary file (JSON chunk, BIN chunk); .gltf keeps the buffer
/// inside as a data URI, so either is one file.
/// </summary>
public static class GltfWriter
{
    const int ArrayBuffer = 34962, ElementArrayBuffer = 34963;
    const int Float = 5126, UnsignedInt = 5125;
    const int Triangles = 4, Lines = 1;

    /// <summary>
    /// The most geometry one file holds: a GLB's offsets and lengths are 32-bit and its chunks live in one array (under
    /// 2 GB); a .gltf's buffer is one base64 string (under a billion characters), three bytes making four.
    /// </summary>
    public const long MaxGlbBytes = 1_500_000_000, MaxGltfBytes = 750_000_000;

    public static byte[] Glb(MeshScene scene) => Write(scene, binary: true, MaxGlbBytes);
    public static byte[] Gltf(MeshScene scene) => Write(scene, binary: false, MaxGltfBytes);

    sealed class Buffers(long limit, bool binary)
    {
        public readonly MemoryStream Bin = new();
        public readonly List<(int Offset, int Length, int Target)> Views = [];
        public readonly List<Action<Utf8JsonWriter>> Accessors = [];

        int View(ReadOnlySpan<byte> bytes, int target)
        {
            // Refused before an offset wraps past 2 GB (which wrote a file no reader opens) or the array cannot grow.
            if (Bin.Length + 3 + bytes.Length > limit)
                throw new CommandException($"The model is too large for one {(binary ? "GLB" : ".gltf")} file (at most {limit / 1_000_000} MB of geometry): export a part of it (a 3D view, some storeys), or as Fragments or USD.");
            while (Bin.Length % 4 != 0) Bin.WriteByte(0);
            var offset = (int)Bin.Length;
            // target 0: not a vertex or index buffer (an image); left out of the view.
            Bin.Write(bytes);
            Views.Add((offset, bytes.Length, target));
            return Views.Count - 1;
        }

        /// <summary>VEC3 floats, turned Y up; with their bounds when <paramref name="bounds"/> (positions must have them).</summary>
        public int Vec3(float[] zUp, bool bounds)
        {
            var n = zUp.Length / 3;
            var yUp = new float[n * 3];
            for (var i = 0; i < n; i++)
            {
                var (x, y, z) = MeshMath.ToYUp(zUp[i * 3], zUp[i * 3 + 1], zUp[i * 3 + 2]);
                yUp[i * 3] = (float)x; yUp[i * 3 + 1] = (float)y; yUp[i * 3 + 2] = (float)z;
            }
            var (min, max) = MeshMath.Bounds(yUp);
            var bytes = new byte[yUp.Length * 4];
            for (var i = 0; i < yUp.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), yUp[i]);
            var view = View(bytes, ArrayBuffer);
            Accessors.Add(w =>
            {
                w.WriteNumber("bufferView", view); w.WriteNumber("componentType", Float); w.WriteNumber("count", n); w.WriteString("type", "VEC3");
                if (bounds)
                {
                    w.WriteStartArray("min"); foreach (var v in min) w.WriteNumberValue(v); w.WriteEndArray();
                    w.WriteStartArray("max"); foreach (var v in max) w.WriteNumberValue(v); w.WriteEndArray();
                }
            });
            return Accessors.Count - 1;
        }

        /// <summary>VEC2 floats as they are (texture coordinates; glTF's v runs down, so it is turned).</summary>
        public int Vec2Flip(float[] uv)
        {
            var bytes = new byte[uv.Length * 4];
            for (var i = 0; i < uv.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), i % 2 == 1 ? -uv[i] : uv[i]);
            var view = View(bytes, ArrayBuffer);
            Accessors.Add(w => { w.WriteNumber("bufferView", view); w.WriteNumber("componentType", Float); w.WriteNumber("count", uv.Length / 2); w.WriteString("type", "VEC2"); });
            return Accessors.Count - 1;
        }

        /// <summary>A file's bytes as a view of their own (an image in a GLB): no target.</summary>
        public int Raw(byte[] bytes) => View(bytes, 0);

        public int Indices(int[] indices)
        {
            var bytes = new byte[indices.Length * 4];
            for (var i = 0; i < indices.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), (uint)indices[i]);
            var view = View(bytes, ElementArrayBuffer);
            Accessors.Add(w => { w.WriteNumber("bufferView", view); w.WriteNumber("componentType", UnsignedInt); w.WriteNumber("count", indices.Length); w.WriteString("type", "SCALAR"); });
            return Accessors.Count - 1;
        }
    }

    /// <summary>sRGB (0–1) to linear, as glTF's base colour factor takes it.</summary>
    public static float Linear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    /// <summary>The file, its geometry at most <paramref name="limit"/> bytes (tests take a small one).</summary>
    internal static byte[] Write(MeshScene scene, bool binary, long limit)
    {
        var buffers = new Buffers(limit, binary);
        // Meshes first: their accessors and views fill the buffer.
        var meshes = new List<(string Name, List<(int Position, int Normal, int Indices, int Material, int? Uv)> Triangles, int? EdgePositions)>();
        var meshOf = new Dictionary<MeshElement, int>();
        var edgeMaterial = scene.Materials.Count;
        foreach (var g in scene.Groups)
            foreach (var e in g.Elements.SelectMany(x => x.Flatten()))
            {
                // An assembly has no mesh of its own: it is a node over its parts' nodes.
                if (e.Parts.Count == 0 && e.Edges.Length < 6) continue;
                var prims = e.Parts.Select(p => (buffers.Vec3(p.Positions, true), buffers.Vec3(p.Normals, false), buffers.Indices(p.Indices), p.Material, p.Uvs is { } uv ? buffers.Vec2Flip(uv) : (int?)null)).ToList();
                int? edges = e.Edges.Length >= 6 ? buffers.Vec3(e.Edges, true) : null;
                meshes.Add((e.Name, prims, edges));
                meshOf[e] = meshes.Count - 1;
            }
        var anyEdges = meshes.Any(x => x.EdgePositions != null);
        // The materials' own images: one each, in the buffer (GLB) or as data URIs (.gltf); a texture per image, all on one repeating sampler.
        var textured = scene.Materials.Select((m, i) => (m.Texture, i)).Where(x => x.Texture != null).ToList();
        var textureOf = new Dictionary<int, int>();
        var imageViews = new List<int>();
        foreach (var (t, i) in textured)
        {
            textureOf[i] = imageViews.Count;
            imageViews.Add(binary ? buffers.Raw(t!.Bytes) : -1);
        }

        using var json = new MemoryStream();
        using (var w = new Utf8JsonWriter(json))
        {
            w.WriteStartObject();
            w.WriteStartObject("asset"); w.WriteString("version", "2.0"); w.WriteString("generator", "Lapin"); w.WriteEndObject();
            // The material extensions the materials use (glass, an index of refraction, a light's strength): a reader without them still reads the core values.
            var used = Extensions(scene);
            if (used.Count > 0) { w.WriteStartArray("extensionsUsed"); foreach (var x in used) w.WriteStringValue(x); w.WriteEndArray(); }
            w.WriteNumber("scene", 0);
            w.WriteStartArray("scenes"); w.WriteStartObject(); w.WriteString("name", scene.Project); w.WriteStartArray("nodes"); w.WriteNumberValue(0); w.WriteEndArray(); w.WriteEndObject(); w.WriteEndArray();

            // Nodes: the project (0), the building (1), then each group, its elements, and an assembly's parts under it.
            // Every node takes its index before its children are numbered, then all are written in order.
            var nodes = new List<Action<Utf8JsonWriter>>();
            int Node(MeshElement e, MeshGroup g)
            {
                var index = nodes.Count;
                nodes.Add(null!);
                var children = (e.Children ?? []).Select(c => Node(c, g)).ToList();
                nodes[index] = w =>
                {
                    w.WriteStartObject(); w.WriteString("name", e.Name);
                    if (meshOf.TryGetValue(e, out var mesh)) w.WriteNumber("mesh", mesh);
                    if (children.Count > 0) { w.WriteStartArray("children"); foreach (var c in children) w.WriteNumberValue(c); w.WriteEndArray(); }
                    w.WriteStartObject("extras"); w.WriteString("globalId", e.GlobalId); w.WriteString("ifcClass", e.IfcClass); if (e.Type != null) w.WriteString("type", e.Type); w.WriteString("storey", g.Name); w.WriteEndObject();
                    w.WriteEndObject();
                };
                return index;
            }
            nodes.Add(null!); nodes.Add(null!);
            var groupNodes = new List<int>();
            foreach (var g in scene.Groups)
            {
                var index = nodes.Count;
                nodes.Add(null!);
                groupNodes.Add(index);
                var children = g.Elements.Select(e => Node(e, g)).ToList();
                nodes[index] = w =>
                {
                    w.WriteStartObject(); w.WriteString("name", g.Name);
                    w.WriteStartArray("children"); foreach (var c in children) w.WriteNumberValue(c); w.WriteEndArray();
                    w.WriteStartObject("extras"); w.WriteString("globalId", g.GlobalId); w.WriteString("ifcClass", g.IfcClass); w.WriteNumber("elevation", g.Elevation); w.WriteEndObject();
                    w.WriteEndObject();
                };
            }
            nodes[0] = w =>
            {
                w.WriteStartObject();
                w.WriteString("name", scene.Project);
                var (rx, ry, rz) = MeshMath.ToYUp(scene.RootOffset.X, scene.RootOffset.Y, scene.RootOffset.Z);
                if (rx != 0 || ry != 0 || rz != 0) { w.WriteStartArray("translation"); w.WriteNumberValue(rx); w.WriteNumberValue(ry); w.WriteNumberValue(rz); w.WriteEndArray(); }
                w.WriteStartArray("children"); w.WriteNumberValue(1); w.WriteEndArray();
                w.WriteEndObject();
            };
            nodes[1] = w =>
            {
                w.WriteStartObject(); w.WriteString("name", scene.Building);
                w.WriteStartArray("children"); foreach (var n in groupNodes) w.WriteNumberValue(n); w.WriteEndArray();
                w.WriteStartObject("extras"); w.WriteString("ifcClass", "IfcBuilding"); w.WriteEndObject();
                w.WriteEndObject();
            };
            w.WriteStartArray("nodes");
            foreach (var node in nodes) node(w);
            w.WriteEndArray();

            w.WriteStartArray("meshes");
            foreach (var (name, prims, edges) in meshes)
            {
                w.WriteStartObject(); w.WriteString("name", name);
                w.WriteStartArray("primitives");
                foreach (var (pos, nor, idx, mat, uv) in prims)
                {
                    w.WriteStartObject();
                    w.WriteStartObject("attributes"); w.WriteNumber("POSITION", pos); w.WriteNumber("NORMAL", nor); if (uv is { } u) w.WriteNumber("TEXCOORD_0", u); w.WriteEndObject();
                    w.WriteNumber("indices", idx); w.WriteNumber("material", mat); w.WriteNumber("mode", Triangles);
                    w.WriteEndObject();
                }
                if (edges is { } ep)
                {
                    w.WriteStartObject();
                    w.WriteStartObject("attributes"); w.WriteNumber("POSITION", ep); w.WriteEndObject();
                    w.WriteNumber("material", edgeMaterial); w.WriteNumber("mode", Lines);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();

            if (textured.Count > 0)
            {
                w.WriteStartArray("images");
                for (var k = 0; k < textured.Count; k++)
                {
                    var t = textured[k].Texture!;
                    w.WriteStartObject(); w.WriteString("name", t.Name);
                    if (binary) { w.WriteNumber("bufferView", imageViews[k]); w.WriteString("mimeType", t.Mime); }
                    else w.WriteString("uri", $"data:{t.Mime};base64,{Convert.ToBase64String(t.Bytes)}");
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteStartArray("samplers");
                w.WriteStartObject(); w.WriteNumber("magFilter", 9729); w.WriteNumber("minFilter", 9987); w.WriteNumber("wrapS", 10497); w.WriteNumber("wrapT", 10497); w.WriteEndObject();
                w.WriteEndArray();
                w.WriteStartArray("textures");
                for (var k = 0; k < textured.Count; k++) { w.WriteStartObject(); w.WriteNumber("sampler", 0); w.WriteNumber("source", k); w.WriteEndObject(); }
                w.WriteEndArray();
            }

            w.WriteStartArray("materials");
            for (var mi = 0; mi < scene.Materials.Count; mi++)
            {
                var mat = scene.Materials[mi];
                // Textured, the image carries the colour: the factor stays white.
                if (textureOf.TryGetValue(mi, out var tex)) Material(w, mat.Name, 1, 1, 1, mat.Alpha, mat.Category, tex, mat.Pbr);
                else Material(w, mat.Name, Linear(mat.R), Linear(mat.G), Linear(mat.B), mat.Alpha, mat.Category, null, mat.Pbr);
            }
            if (anyEdges) Material(w, "Edges", 0, 0, 0, 1, null);
            w.WriteEndArray();

            w.WriteStartArray("accessors");
            foreach (var a in buffers.Accessors) { w.WriteStartObject(); a(w); w.WriteEndObject(); }
            w.WriteEndArray();

            w.WriteStartArray("bufferViews");
            foreach (var (offset, length, target) in buffers.Views)
            {
                w.WriteStartObject(); w.WriteNumber("buffer", 0); w.WriteNumber("byteOffset", offset); w.WriteNumber("byteLength", length); if (target != 0) w.WriteNumber("target", target); w.WriteEndObject();
            }
            w.WriteEndArray();

            while (buffers.Bin.Length % 4 != 0) buffers.Bin.WriteByte(0);
            w.WriteStartArray("buffers");
            w.WriteStartObject();
            w.WriteNumber("byteLength", buffers.Bin.Length);
            if (!binary) w.WriteString("uri", "data:application/octet-stream;base64," + Convert.ToBase64String(buffers.Bin.GetBuffer(), 0, (int)buffers.Bin.Length));
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteEndObject();
        }
        if (!binary) return json.ToArray();

        // GLB: a 12-byte header, the JSON chunk padded with spaces, the BIN chunk padded with zeros, each to 4 bytes.
        var jsonBytes = json.ToArray();
        var jsonPadded = (jsonBytes.Length + 3) & ~3;
        var bin = buffers.Bin.ToArray();
        var total = 12 + 8 + jsonPadded + (bin.Length > 0 ? 8 + bin.Length : 0);
        var o = new byte[total];
        BinaryPrimitives.WriteUInt32LittleEndian(o, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(8), (uint)total);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(12), (uint)jsonPadded);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(16), 0x4E4F534A);
        jsonBytes.CopyTo(o, 20);
        o.AsSpan(20 + jsonBytes.Length, jsonPadded - jsonBytes.Length).Fill((byte)' ');
        if (bin.Length > 0)
        {
            var b = 20 + jsonPadded;
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(b), (uint)bin.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(b + 4), 0x004E4942);
            bin.CopyTo(o, b + 8);
        }
        return o;
    }

    /// <summary>The glTF extensions a scene's materials need.</summary>
    static List<string> Extensions(MeshScene scene)
    {
        var o = new List<string>();
        if (scene.Materials.Any(m => m.Pbr.Transmission > 0)) o.Add("KHR_materials_transmission");
        if (scene.Materials.Any(m => Math.Abs(m.Pbr.Ior - 1.5) > 1e-6)) o.Add("KHR_materials_ior");
        if (scene.Materials.Any(m => m.Pbr.Emissive != null && m.Pbr.EmissiveStrength > 1)) o.Add("KHR_materials_emissive_strength");
        return o;
    }

    /// <summary>
    /// A material after glTF's metallic-roughness model: its name exactly as the project has it, the base colour (linear) and
    /// opacity, metallic and roughness from its look; glass with KHR_materials_transmission (and its alpha, for readers without it),
    /// its index of refraction (KHR_materials_ior), a light's emissive colour.
    /// </summary>
    static void Material(Utf8JsonWriter w, string name, float r, float g, float b, float a, string? category, int? texture = null, MaterialLook? look = null)
    {
        var l = look ?? MaterialLook.Plain;
        w.WriteStartObject();
        w.WriteString("name", name);
        w.WriteStartObject("pbrMetallicRoughness");
        w.WriteStartArray("baseColorFactor"); w.WriteNumberValue(r); w.WriteNumberValue(g); w.WriteNumberValue(b); w.WriteNumberValue(a); w.WriteEndArray();
        if (texture is { } t) { w.WriteStartObject("baseColorTexture"); w.WriteNumber("index", t); w.WriteEndObject(); }
        w.WriteNumber("metallicFactor", Math.Round(l.Metalness, 4)); w.WriteNumber("roughnessFactor", Math.Round(l.Roughness, 4));
        w.WriteEndObject();
        if (l.Emissive != null)
        {
            var (er, eg, eb) = MeshScene.Rgb(l.Emissive);
            w.WriteStartArray("emissiveFactor"); w.WriteNumberValue(Linear(er)); w.WriteNumberValue(Linear(eg)); w.WriteNumberValue(Linear(eb)); w.WriteEndArray();
        }
        if (a < 1) w.WriteString("alphaMode", "BLEND");
        w.WriteBoolean("doubleSided", true);
        var ext = l.Transmission > 0 || Math.Abs(l.Ior - 1.5) > 1e-6 || l.Emissive != null && l.EmissiveStrength > 1;
        if (ext)
        {
            w.WriteStartObject("extensions");
            if (l.Transmission > 0) { w.WriteStartObject("KHR_materials_transmission"); w.WriteNumber("transmissionFactor", Math.Round(l.Transmission, 4)); w.WriteEndObject(); }
            if (Math.Abs(l.Ior - 1.5) > 1e-6) { w.WriteStartObject("KHR_materials_ior"); w.WriteNumber("ior", Math.Round(l.Ior, 4)); w.WriteEndObject(); }
            if (l.Emissive != null && l.EmissiveStrength > 1) { w.WriteStartObject("KHR_materials_emissive_strength"); w.WriteNumber("emissiveStrength", Math.Round(l.EmissiveStrength, 4)); w.WriteEndObject(); }
            w.WriteEndObject();
        }
        if (category != null) { w.WriteStartObject("extras"); w.WriteString("category", category); w.WriteEndObject(); }
        w.WriteEndObject();
    }

    /// <summary>The JSON of a GLB, for tests and tools: checked header and chunks.</summary>
    public static (JsonDocument Json, byte[] Bin) ReadGlb(byte[] glb)
    {
        if (glb.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(glb) != 0x46546C67) throw new InvalidDataException("Not a GLB file.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(8)) != glb.Length) throw new InvalidDataException("The GLB's length is wrong.");
        var jsonLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));
        if (BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(16)) != 0x4E4F534A || jsonLength % 4 != 0) throw new InvalidDataException("The GLB's JSON chunk is wrong.");
        var doc = JsonDocument.Parse(Encoding.UTF8.GetString(glb, 20, jsonLength));
        var b = 20 + jsonLength;
        if (b >= glb.Length) return (doc, []);
        var binLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(b));
        if (BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(b + 4)) != 0x004E4942 || binLength % 4 != 0) throw new InvalidDataException("The GLB's BIN chunk is wrong.");
        return (doc, glb.AsSpan(b + 8, binLength).ToArray());
    }
}
