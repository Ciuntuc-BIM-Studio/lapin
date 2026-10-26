using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Lapin.Mesh;

/// <summary>
/// A scene as glTF 2.0 (Khronos), written after the specification: one node
/// for the project (its offset to real coordinates), the building, a node per
/// storey, a node per element with its mesh, a primitive per material, and
/// the element's identity in <c>extras</c> (GlobalId, IFC class, type). Y up
/// as glTF asks (the model's Z up turns: x, z, −y), metres; base colours in
/// linear RGB with metallic 0, rough 0.9, blended where a material is
/// transparent. Edges, when the scene has them, are a LINES primitive.
/// GLB is one binary file (JSON chunk, BIN chunk); .gltf keeps the buffer
/// inside as a data URI, so either is one file.
/// </summary>
public static class GltfWriter
{
    const int ArrayBuffer = 34962, ElementArrayBuffer = 34963;
    const int Float = 5126, UnsignedInt = 5125;
    const int Triangles = 4, Lines = 1;

    public static byte[] Glb(MeshScene scene) => Write(scene, binary: true);
    public static byte[] Gltf(MeshScene scene) => Write(scene, binary: false);

    sealed class Buffers
    {
        public readonly MemoryStream Bin = new();
        public readonly List<(int Offset, int Length, int Target)> Views = [];
        public readonly List<Action<Utf8JsonWriter>> Accessors = [];

        int View(ReadOnlySpan<byte> bytes, int target)
        {
            while (Bin.Length % 4 != 0) Bin.WriteByte(0);
            var offset = (int)Bin.Length;
            Bin.Write(bytes);
            Views.Add((offset, bytes.Length, target));
            return Views.Count - 1;
        }

        /// <summary>VEC3 floats, turned Y up; with their bounds when <paramref name="bounds"/> (positions must have them).</summary>
        public int Vec3(float[] zUp, bool bounds)
        {
            var n = zUp.Length / 3;
            var yUp = new float[n * 3];
            float[] min = [float.MaxValue, float.MaxValue, float.MaxValue], max = [float.MinValue, float.MinValue, float.MinValue];
            for (var i = 0; i < n; i++)
            {
                var (x, y, z) = (zUp[i * 3], zUp[i * 3 + 2], -zUp[i * 3 + 1]);
                yUp[i * 3] = x; yUp[i * 3 + 1] = y; yUp[i * 3 + 2] = z == 0 ? 0 : z;
                for (var k = 0; k < 3; k++) { min[k] = Math.Min(min[k], yUp[i * 3 + k]); max[k] = Math.Max(max[k], yUp[i * 3 + k]); }
            }
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

    static byte[] Write(MeshScene scene, bool binary)
    {
        var buffers = new Buffers();
        // Meshes first: their accessors and views fill the buffer.
        var meshes = new List<(string Name, List<(int Position, int Normal, int Indices, int Material)> Triangles, int? EdgePositions)>();
        var meshOf = new Dictionary<MeshElement, int>();
        var edgeMaterial = scene.Materials.Count;
        foreach (var g in scene.Groups)
            foreach (var e in g.Elements)
            {
                var prims = e.Parts.Select(p => (buffers.Vec3(p.Positions, true), buffers.Vec3(p.Normals, false), buffers.Indices(p.Indices), p.Material)).ToList();
                int? edges = e.Edges.Length >= 6 ? buffers.Vec3(e.Edges, true) : null;
                meshes.Add((e.Name, prims, edges));
                meshOf[e] = meshes.Count - 1;
            }
        var anyEdges = meshes.Any(x => x.EdgePositions != null);

        using var json = new MemoryStream();
        using (var w = new Utf8JsonWriter(json))
        {
            w.WriteStartObject();
            w.WriteStartObject("asset"); w.WriteString("version", "2.0"); w.WriteString("generator", "Lapin"); w.WriteEndObject();
            w.WriteNumber("scene", 0);
            w.WriteStartArray("scenes"); w.WriteStartObject(); w.WriteString("name", scene.Project); w.WriteStartArray("nodes"); w.WriteNumberValue(0); w.WriteEndArray(); w.WriteEndObject(); w.WriteEndArray();

            // Nodes: the project (0), the building (1), then each group and its elements.
            w.WriteStartArray("nodes");
            var groupNodes = new List<int>();
            var next = 2;
            foreach (var g in scene.Groups) { groupNodes.Add(next); next += 1 + g.Elements.Count; }
            w.WriteStartObject();
            w.WriteString("name", scene.Project);
            var (rx, ry, rz) = scene.RootOffset;
            if (rx != 0 || ry != 0 || rz != 0) { w.WriteStartArray("translation"); w.WriteNumberValue(rx); w.WriteNumberValue(rz); w.WriteNumberValue(-ry == 0 ? 0 : -ry); w.WriteEndArray(); }
            w.WriteStartArray("children"); w.WriteNumberValue(1); w.WriteEndArray();
            w.WriteEndObject();
            w.WriteStartObject(); w.WriteString("name", scene.Building);
            w.WriteStartArray("children"); foreach (var n in groupNodes) w.WriteNumberValue(n); w.WriteEndArray();
            w.WriteStartObject("extras"); w.WriteString("ifcClass", "IfcBuilding"); w.WriteEndObject();
            w.WriteEndObject();
            var at = 2;
            foreach (var g in scene.Groups)
            {
                w.WriteStartObject(); w.WriteString("name", g.Name);
                w.WriteStartArray("children"); for (var i = 0; i < g.Elements.Count; i++) w.WriteNumberValue(at + 1 + i); w.WriteEndArray();
                w.WriteStartObject("extras"); w.WriteString("globalId", g.GlobalId); w.WriteString("ifcClass", g.IfcClass); w.WriteNumber("elevation", g.Elevation); w.WriteEndObject();
                w.WriteEndObject();
                foreach (var e in g.Elements)
                {
                    w.WriteStartObject(); w.WriteString("name", e.Name); w.WriteNumber("mesh", meshOf[e]);
                    w.WriteStartObject("extras"); w.WriteString("globalId", e.GlobalId); w.WriteString("ifcClass", e.IfcClass); if (e.Type != null) w.WriteString("type", e.Type); w.WriteString("storey", g.Name); w.WriteEndObject();
                    w.WriteEndObject();
                }
                at += 1 + g.Elements.Count;
            }
            w.WriteEndArray();

            w.WriteStartArray("meshes");
            foreach (var (name, prims, edges) in meshes)
            {
                w.WriteStartObject(); w.WriteString("name", name);
                w.WriteStartArray("primitives");
                foreach (var (pos, nor, idx, mat) in prims)
                {
                    w.WriteStartObject();
                    w.WriteStartObject("attributes"); w.WriteNumber("POSITION", pos); w.WriteNumber("NORMAL", nor); w.WriteEndObject();
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

            w.WriteStartArray("materials");
            foreach (var mat in scene.Materials)
                Material(w, mat.Name, Linear(mat.R), Linear(mat.G), Linear(mat.B), mat.Alpha, mat.Category);
            if (anyEdges) Material(w, "Edges", 0, 0, 0, 1, null);
            w.WriteEndArray();

            w.WriteStartArray("accessors");
            foreach (var a in buffers.Accessors) { w.WriteStartObject(); a(w); w.WriteEndObject(); }
            w.WriteEndArray();

            w.WriteStartArray("bufferViews");
            foreach (var (offset, length, target) in buffers.Views)
            {
                w.WriteStartObject(); w.WriteNumber("buffer", 0); w.WriteNumber("byteOffset", offset); w.WriteNumber("byteLength", length); w.WriteNumber("target", target); w.WriteEndObject();
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

    static void Material(Utf8JsonWriter w, string name, float r, float g, float b, float a, string? category)
    {
        w.WriteStartObject();
        w.WriteString("name", name);
        w.WriteStartObject("pbrMetallicRoughness");
        w.WriteStartArray("baseColorFactor"); w.WriteNumberValue(r); w.WriteNumberValue(g); w.WriteNumberValue(b); w.WriteNumberValue(a); w.WriteEndArray();
        w.WriteNumber("metallicFactor", 0); w.WriteNumber("roughnessFactor", 0.9);
        w.WriteEndObject();
        if (a < 1) w.WriteString("alphaMode", "BLEND");
        w.WriteBoolean("doubleSided", true);
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
