using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mesh;

/// <summary>A mesh read from a file: its pieces (an OBJ object, a glTF node's mesh, an STL file), each a name, a colour and triangles in model metres, Z up.</summary>
public sealed record MeshPiece(string Name, string Color, List<Vec3> Vertices, List<(int A, int B, int C)> Triangles);

/// <summary>What a mesh import brought: the references made, the triangles, and its notes.</summary>
public sealed record MeshImportResult(int Elements, int Triangles, IReadOnlyList<string> Notes)
{
    public JsonObject ToJson() => new()
    {
        ["elements"] = Elements, ["triangles"] = Triangles,
        ["notes"] = new JsonArray(Notes.Select(n => (JsonNode)n).ToArray()),
    };
}

/// <summary>
/// A triangle mesh from another program (docs/plan-studio-editor.md): Wavefront
/// OBJ, STL (text or binary) or glTF 2.0 (.gltf with its buffers inside or
/// beside it, .glb), read after their public specifications with no
/// dependency. Each piece (an OBJ object or group, a glTF node's mesh, the
/// whole STL) becomes a reference drawn from its stored body
/// (<see cref="StoredBodies"/>), on the storey asked for (else the lowest),
/// shown and snapped to; Studio makes one an editable solid. glTF is metres
/// and Y up by its rule; OBJ and STL say neither, so the import takes the
/// unit (<c>scale</c>: 0.001 for millimetres) and which way is up (<c>up</c>:
/// y or z; OBJ is read Y up, STL Z up, unless told).
/// </summary>
public static class MeshImport
{
    public static readonly string[] Formats = ["obj", "stl", "gltf", "glb"];

    public static MeshImportResult Run(Session session, byte[] bytes, string name, JsonObject options, string? folder, out Diff diff)
    {
        var ext = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        var notes = new List<string>();
        List<MeshPiece> pieces;
        try
        {
            pieces = ext switch
            {
                "obj" => Obj(bytes, notes),
                "stl" => Stl(bytes, Path.GetFileNameWithoutExtension(name)),
                "gltf" or "glb" => Gltf(bytes, ext == "glb", folder, notes),
                _ => throw new CommandException($"„{name}” is not a mesh Lapin reads (OBJ, STL, glTF, GLB)."),
            };
        }
        catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or ArgumentOutOfRangeException or InvalidDataException or JsonException or KeyNotFoundException or InvalidCastException)
        {
            throw new CommandException($"„{name}” could not be read: {ex.Message}");
        }
        pieces = pieces.Where(p => p.Triangles.Count > 0).ToList();
        if (pieces.Count == 0) throw new CommandException($"„{name}” holds no triangles.");
        // Units and up: glTF's are its own; OBJ and STL as told.
        var scale = Json.Number(options["scale"]) is { } sc && sc > 0 ? sc : 1;
        var up = (string?)options["up"] ?? (ext == "obj" ? "y" : "z");
        if (ext is "gltf" or "glb") { up = "y"; scale = Json.Number(options["scale"]) is { } s2 && s2 > 0 ? s2 : 1; }
        var shift = options["at"] is JsonArray { Count: >= 3 } at && Json.Number(at[0]) is { } ax && Json.Number(at[1]) is { } ay && Json.Number(at[2]) is { } az ? new Vec3(ax, ay, az) : Vec3.Zero;
        Vec3 Map(Vec3 p) { var q = p * scale; if (up == "y") q = new Vec3(q.X, -q.Z, q.Y); return q + shift; }
        foreach (var p in pieces) for (var i = 0; i < p.Vertices.Count; i++) p.Vertices[i] = Map(p.Vertices[i]);
        if (Flag(options["oneElement"]) && pieces.Count > 1)
        {
            var all = new MeshPiece(Path.GetFileNameWithoutExtension(name), pieces[0].Color, [], []);
            foreach (var p in pieces)
            {
                var off = all.Vertices.Count;
                all.Vertices.AddRange(p.Vertices);
                all.Triangles.AddRange(p.Triangles.Select(t => (t.A + off, t.B + off, t.C + off)));
            }
            pieces = [all];
        }
        var elements = 0;
        diff = session.Edit($"Import {name}", edit =>
        {
            var storey = (string?)options["storeyId"] is { Length: > 0 } sid && edit.Model.Get(sid) is { Class: IfcClass.BuildingStorey } given ? given
                : Spatial.Storeys(edit.Model).OrderBy(s => s.Num("elevation")).FirstOrDefault();
            if (storey == null)
            {
                var building = Spatial.EnsureBuilding(edit);
                storey = new Entity(edit.NewId(), IfcClass.BuildingStorey, "Ground floor", new JsonObject { ["elevation"] = 0.0, ["height"] = 3.0 });
                edit.Add(storey);
                edit.Relate(IfcClass.RelAggregates, building.Id, storey.Id);
            }
            foreach (var p in pieces)
            {
                var attrs = new JsonObject
                {
                    [StoredBodies.Key] = StoredBodies.Write([(p.Name, p.Color, p.Vertices, p.Triangles)]),
                    ["source"] = name,
                };
                var e = new Entity(edit.NewId(), IfcClass.BuildingElementProxy, p.Name, attrs);
                edit.Add(e);
                Spatial.Contain(edit, storey.Id, e.Id);
                elements++;
            }
        });
        return new MeshImportResult(elements, pieces.Sum(p => p.Triangles.Count), notes);
    }

    static bool Flag(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;
    static double D(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    const string Grey = "#c9ced6";

    // ------------------------------------------------------------ OBJ

    /// <summary>
    /// Wavefront OBJ: <c>v x y z</c>, <c>f a b c …</c> (each corner <c>v</c>, <c>v/vt</c>, <c>v//vn</c> or
    /// <c>v/vt/vn</c>, from 1, or from the end when negative), a polygon fanned into triangles; <c>o</c> and
    /// <c>g</c> start a piece. Materials (<c>usemtl</c>) name the piece's colour by a hash of their name, the
    /// .mtl file not being read.
    /// </summary>
    public static List<MeshPiece> Obj(byte[] bytes, List<string> notes)
    {
        var all = new List<Vec3>();
        var pieces = new List<MeshPiece>();
        MeshPiece? cur = null;
        Dictionary<int, int> local = [];
        string name = "Object";
        string color = Grey;
        void Start(string n) { cur = null; name = n; }
        var text = Encoding.UTF8.GetString(bytes);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0])
            {
                case "v" when parts.Length >= 4: all.Add(new Vec3(D(parts[1]), D(parts[2]), D(parts[3]))); break;
                case "o" or "g": Start(parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : "Object"); break;
                case "usemtl" when parts.Length > 1:
                    color = HashColor(parts[1]);
                    if (cur != null && cur.Color != color) cur = null;
                    break;
                case "f" when parts.Length >= 4:
                {
                    if (cur == null)
                    {
                        cur = new MeshPiece(pieces.Any(p => p.Name == name) ? $"{name} {pieces.Count + 1}" : name, color, [], []);
                        pieces.Add(cur);
                        local = [];
                    }
                    var idx = new List<int>();
                    foreach (var c in parts.Skip(1))
                    {
                        var v = int.Parse(c.Split('/')[0], CultureInfo.InvariantCulture);
                        var at = v > 0 ? v - 1 : all.Count + v;
                        if (at < 0 || at >= all.Count) throw new FormatException($"a face names vertex {v}, which is not there.");
                        if (!local.TryGetValue(at, out var li)) { li = cur.Vertices.Count; cur.Vertices.Add(all[at]); local[at] = li; }
                        idx.Add(li);
                    }
                    for (var k = 1; k + 1 < idx.Count; k++) cur.Triangles.Add((idx[0], idx[k], idx[k + 1]));
                    break;
                }
            }
        }
        if (text.Contains("mtllib")) notes.Add("The OBJ's material library was not read: its pieces take a colour per material name.");
        return pieces;
    }

    static string HashColor(string s)
    {
        var h = 0u;
        foreach (var ch in s) h = h * 31 + ch;
        var (r, g, b) = (150 + (int)(h % 80), 150 + (int)(h / 80 % 80), 150 + (int)(h / 6400 % 80));
        return $"#{r:x2}{g:x2}{b:x2}";
    }

    // ------------------------------------------------------------ STL

    /// <summary>STL, binary (80 bytes of header, a count, 50 bytes a triangle) or text (<c>facet … vertex x y z …</c>): one piece, its corners welded where they are the same point.</summary>
    public static List<MeshPiece> Stl(byte[] bytes, string name)
    {
        var piece = new MeshPiece(name, Grey, [], []);
        var weld = new Dictionary<(double, double, double), int>();
        int V(double x, double y, double z)
        {
            if (!weld.TryGetValue((x, y, z), out var i)) { i = piece.Vertices.Count; piece.Vertices.Add(new Vec3(x, y, z)); weld[(x, y, z)] = i; }
            return i;
        }
        var binary = bytes.Length >= 84 && 84 + 50L * BitConverter.ToUInt32(bytes, 80) == bytes.Length;
        if (binary)
        {
            var n = BitConverter.ToUInt32(bytes, 80);
            for (var t = 0; t < n; t++)
            {
                var o = 84 + 50 * t + 12;
                int P(int k) => V(BitConverter.ToSingle(bytes, o + 12 * k), BitConverter.ToSingle(bytes, o + 12 * k + 4), BitConverter.ToSingle(bytes, o + 12 * k + 8));
                var (a, b, c) = (P(0), P(1), P(2));
                if (a != b && b != c && a != c) piece.Triangles.Add((a, b, c));
            }
            return [piece];
        }
        var corners = new List<int>();
        foreach (var raw in Encoding.UTF8.GetString(bytes).Split('\n'))
        {
            var parts = raw.Trim().Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4 && parts[0] == "vertex") corners.Add(V(D(parts[1]), D(parts[2]), D(parts[3])));
            else if (parts.Length >= 1 && parts[0] == "endfacet")
            {
                for (var k = 1; k + 1 < corners.Count; k++)
                    if (corners[0] != corners[k] && corners[k] != corners[k + 1] && corners[0] != corners[k + 1]) piece.Triangles.Add((corners[0], corners[k], corners[k + 1]));
                corners.Clear();
            }
        }
        if (piece.Triangles.Count == 0 && bytes.Length >= 84) throw new FormatException("it is neither a binary STL nor a text one.");
        return [piece];
    }

    // ------------------------------------------------------------ glTF

    /// <summary>
    /// glTF 2.0: the default scene's nodes (or every root node), each placed by its matrix or its translation,
    /// rotation and scale, every triangle primitive of its mesh (positions as floats, indices as bytes, shorts or
    /// ints, or none) in its material's base colour. Buffers inside the file (data URIs, a GLB's binary chunk) or
    /// beside it (<paramref name="folder"/>).
    /// </summary>
    public static List<MeshPiece> Gltf(byte[] bytes, bool glb, string? folder, List<string> notes)
    {
        JsonNode root;
        byte[]? bin = null;
        if (glb)
        {
            if (bytes.Length < 20 || BitConverter.ToUInt32(bytes, 0) != 0x46546C67) throw new FormatException("it is not a GLB (its magic number is wrong).");
            var at = 12;
            root = null!;
            while (at + 8 <= bytes.Length)
            {
                var len = (int)BitConverter.ToUInt32(bytes, at);
                var type = BitConverter.ToUInt32(bytes, at + 4);
                if (type == 0x4E4F534A) root = JsonNode.Parse(Encoding.UTF8.GetString(bytes, at + 8, len))!;
                else if (type == 0x004E4942) bin = bytes[(at + 8)..(at + 8 + len)];
                at += 8 + len;
            }
            if (root == null) throw new FormatException("the GLB has no JSON chunk.");
        }
        else root = JsonNode.Parse(Encoding.UTF8.GetString(bytes))!;

        var buffers = new List<byte[]>();
        foreach (var b in root["buffers"]?.AsArray() ?? [])
        {
            var uri = (string?)b?["uri"];
            if (uri == null) { buffers.Add(bin ?? throw new FormatException("a buffer without a uri needs a GLB's binary chunk.")); continue; }
            if (uri.StartsWith("data:", StringComparison.Ordinal)) { buffers.Add(Convert.FromBase64String(uri[(uri.IndexOf(',') + 1)..])); continue; }
            var file = folder != null ? Path.Combine(folder, Uri.UnescapeDataString(uri)) : null;
            if (file == null || !File.Exists(file)) throw new FormatException($"its buffer „{uri}” is a file beside it: open the .gltf from its folder on the desktop, or use a .glb.");
            buffers.Add(File.ReadAllBytes(file));
        }
        var views = root["bufferViews"]?.AsArray() ?? [];
        var accessors = root["accessors"]?.AsArray() ?? [];
        var meshes = root["meshes"]?.AsArray() ?? [];
        var materials = root["materials"]?.AsArray() ?? [];
        var nodes = root["nodes"]?.AsArray() ?? [];

        (byte[] Data, int Offset, int Stride) View(int accessor, int size)
        {
            var a = accessors[accessor]!;
            var v = views[(int)a["bufferView"]!]!;
            var data = buffers[(int)v["buffer"]!];
            var offset = ((int?)v["byteOffset"] ?? 0) + ((int?)a["byteOffset"] ?? 0);
            return (data, offset, (int?)v["byteStride"] ?? size);
        }
        List<Vec3> Positions(int accessor)
        {
            var a = accessors[accessor]!;
            if ((int)a["componentType"]! != 5126 || (string?)a["type"] != "VEC3") throw new FormatException("its positions are not floats in threes.");
            var (data, offset, stride) = View(accessor, 12);
            var n = (int)a["count"]!;
            var o = new List<Vec3>(n);
            for (var i = 0; i < n; i++) { var at = offset + i * stride; o.Add(new Vec3(BitConverter.ToSingle(data, at), BitConverter.ToSingle(data, at + 4), BitConverter.ToSingle(data, at + 8))); }
            return o;
        }
        List<int> Indices(int accessor)
        {
            var a = accessors[accessor]!;
            var type = (int)a["componentType"]!;
            var size = type switch { 5121 => 1, 5123 => 2, 5125 => 4, _ => throw new FormatException("its indices are not bytes, shorts or ints.") };
            var (data, offset, stride) = View(accessor, size);
            var n = (int)a["count"]!;
            var o = new List<int>(n);
            for (var i = 0; i < n; i++)
            {
                var at = offset + i * stride;
                o.Add(size == 1 ? data[at] : size == 2 ? BitConverter.ToUInt16(data, at) : (int)BitConverter.ToUInt32(data, at));
            }
            return o;
        }
        string ColorOf(JsonNode? prim)
        {
            if ((int?)prim?["material"] is not { } mi || mi >= materials.Count) return Grey;
            if (materials[mi]?["pbrMetallicRoughness"]?["baseColorFactor"] is JsonArray c && c.Count >= 3)
            {
                int C(int k) => (int)Math.Round(Math.Clamp((double)c[k]!, 0, 1) * 255);
                return $"#{C(0):x2}{C(1):x2}{C(2):x2}";
            }
            return Grey;
        }

        var pieces = new List<MeshPiece>();
        var skipped = 0;
        void Walk(int index, double[] parent)
        {
            var node = nodes[index]!;
            var local = Matrix(node);
            var world = Mul(parent, local);
            if ((int?)node["mesh"] is { } mi && mi < meshes.Count)
            {
                var mesh = meshes[mi]!;
                var label = (string?)node["name"] ?? (string?)mesh["name"] ?? $"Mesh {mi}";
                foreach (var prim in mesh["primitives"]?.AsArray() ?? [])
                {
                    if (((int?)prim?["mode"] ?? 4) != 4) { skipped++; continue; }
                    if ((int?)prim?["attributes"]?["POSITION"] is not { } pos) continue;
                    var pts = Positions(pos).Select(p => Apply(world, p)).ToList();
                    var idx = (int?)prim!["indices"] is { } ia ? Indices(ia) : Enumerable.Range(0, pts.Count).ToList();
                    var tris = new List<(int, int, int)>();
                    for (var k = 0; k + 2 < idx.Count; k += 3) tris.Add((idx[k], idx[k + 1], idx[k + 2]));
                    // A mirroring placement turns the triangles inside out: wound back.
                    if (Det(world) < 0) tris = tris.Select(t => (t.Item1, t.Item3, t.Item2)).ToList();
                    pieces.Add(new MeshPiece(label, ColorOf(prim), pts, tris));
                }
            }
            foreach (var child in node["children"]?.AsArray() ?? []) Walk((int)child!, world);
        }
        var identity = new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
        var scenes = root["scenes"]?.AsArray();
        var sceneIndex = (int?)root["scene"] ?? 0;
        var roots = scenes != null && sceneIndex < scenes.Count ? scenes[sceneIndex]?["nodes"]?.AsArray().Select(x => (int)x!).ToList() : null;
        if (roots == null)
        {
            var children = nodes.SelectMany(n => n?["children"]?.AsArray().Select(c => (int)c!) ?? []).ToHashSet();
            roots = Enumerable.Range(0, nodes.Count).Where(i => !children.Contains(i)).ToList();
        }
        foreach (var r in roots) Walk(r, identity);
        if (skipped > 0) notes.Add($"{skipped} primitive(s) of lines or points were left out.");
        return pieces;
    }

    /// <summary>A node's placement, column-major as glTF writes it: its matrix, or T · R · S.</summary>
    static double[] Matrix(JsonNode node)
    {
        if (node["matrix"] is JsonArray m && m.Count == 16) return m.Select(x => (double)x!).ToArray();
        var t = node["translation"] is JsonArray ta ? ta.Select(x => (double)x!).ToArray() : [0, 0, 0];
        var r = node["rotation"] is JsonArray ra ? ra.Select(x => (double)x!).ToArray() : [0, 0, 0, 1];
        var s = node["scale"] is JsonArray sa ? sa.Select(x => (double)x!).ToArray() : [1, 1, 1];
        var (x, y, z, w) = (r[0], r[1], r[2], r[3]);
        // Columns of R, each scaled.
        double[] c0 = [1 - 2 * (y * y + z * z), 2 * (x * y + z * w), 2 * (x * z - y * w)];
        double[] c1 = [2 * (x * y - z * w), 1 - 2 * (x * x + z * z), 2 * (y * z + x * w)];
        double[] c2 = [2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y)];
        return [c0[0] * s[0], c0[1] * s[0], c0[2] * s[0], 0, c1[0] * s[1], c1[1] * s[1], c1[2] * s[1], 0, c2[0] * s[2], c2[1] * s[2], c2[2] * s[2], 0, t[0], t[1], t[2], 1];
    }

    static double[] Mul(double[] a, double[] b)
    {
        var o = new double[16];
        for (var col = 0; col < 4; col++)
            for (var row = 0; row < 4; row++)
            {
                var v = 0.0;
                for (var k = 0; k < 4; k++) v += a[k * 4 + row] * b[col * 4 + k];
                o[col * 4 + row] = v;
            }
        return o;
    }

    static Vec3 Apply(double[] m, Vec3 p) => new(m[0] * p.X + m[4] * p.Y + m[8] * p.Z + m[12], m[1] * p.X + m[5] * p.Y + m[9] * p.Z + m[13], m[2] * p.X + m[6] * p.Y + m[10] * p.Z + m[14]);

    static double Det(double[] m) =>
        m[0] * (m[5] * m[10] - m[9] * m[6]) - m[4] * (m[1] * m[10] - m[9] * m[2]) + m[8] * (m[1] * m[6] - m[5] * m[2]);
}
