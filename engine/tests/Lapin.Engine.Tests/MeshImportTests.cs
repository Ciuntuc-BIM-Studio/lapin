using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Mesh;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>Meshes from other programs (OBJ, STL, glTF, GLB) imported as references, and one made into a Studio solid that takes steps.</summary>
public class MeshImportTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    // A unit cube's corners and its twelve triangles, outward.
    static readonly double[][] Corners = [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]];
    static readonly int[][] Tris = [[0, 2, 1], [0, 3, 2], [4, 5, 6], [4, 6, 7], [0, 1, 5], [0, 5, 4], [1, 2, 6], [1, 6, 5], [2, 3, 7], [2, 7, 6], [3, 0, 4], [3, 4, 7]];

    static List<string> Imported(Fixture f, string before) => f.Model.Entities.Where(e => e.Str("source") == before).Select(e => e.Id).ToList();

    [Fact]
    public void An_OBJ_cube_in_millimetres_Y_up_comes_in_as_a_reference_and_becomes_a_Studio_solid()
    {
        var f = F();
        // 1000 mm cube, Y up: its height is along y.
        var sb = new StringBuilder("# a cube\no Cube\n");
        foreach (var c in Corners) sb.Append($"v {c[0] * 1000} {c[2] * 1000} {-c[1] * 1000}\n");
        foreach (var t in Tris) sb.Append($"f {t[0] + 1} {t[1] + 1} {t[2] + 1}\n");
        var r = MeshImport.Run(f.Session, Encoding.UTF8.GetBytes(sb.ToString()), "cube.obj", new JsonObject { ["scale"] = 0.001 }, null, out _);
        Assert.Equal(1, r.Elements);
        Assert.Equal(12, r.Triangles);
        var id = Imported(f, "cube.obj").Single();
        Assert.Equal("Cube", f.Model.Get(id)!.Name);
        Assert.Equal(1, Vol(f, id), 6);
        var b = f.Bounds(id);
        Assert.Equal(0, b.Min.Z, 6); Assert.Equal(1, b.Max.Z, 6);
        // Made into a Studio solid, it takes a fillet on its top edge.
        var solid = (string)f.Run("studio.fromElement", new JsonObject { ["ids"] = new JsonArray(id) })!["ids"]![0]!;
        f.Run("studio.addFeature", new JsonObject { ["id"] = solid, ["feature"] = new JsonObject { ["op"] = "chamfer", ["distance"] = 0.2, ["point"] = new JsonArray(0.5, 0.05, 1), ["normal"] = new JsonArray(0, 0, 1) } });
        Assert.Equal(0.98, Vol(f, solid), 6);
    }

    [Fact]
    public void An_STL_in_text_and_in_binary()
    {
        var f = F();
        var text = new StringBuilder("solid cube\n");
        foreach (var t in Tris)
        {
            text.Append("facet normal 0 0 0\nouter loop\n");
            foreach (var k in t) text.Append($"vertex {Corners[k][0]} {Corners[k][1]} {Corners[k][2]}\n");
            text.Append("endloop\nendfacet\n");
        }
        text.Append("endsolid cube\n");
        MeshImport.Run(f.Session, Encoding.UTF8.GetBytes(text.ToString()), "a.stl", new JsonObject(), null, out _);
        Assert.Equal(1, Vol(f, Imported(f, "a.stl").Single()), 6);
        var bin = new byte[84 + 50 * Tris.Length];
        BitConverter.GetBytes((uint)Tris.Length).CopyTo(bin, 80);
        for (var i = 0; i < Tris.Length; i++)
            for (var k = 0; k < 3; k++)
                for (var c = 0; c < 3; c++)
                    BitConverter.GetBytes((float)(Corners[Tris[i][k]][c] * 2)).CopyTo(bin, 84 + 50 * i + 12 + 12 * k + 4 * c);
        MeshImport.Run(f.Session, bin, "b.stl", new JsonObject { ["at"] = new JsonArray(10, 0, 0) }, null, out _);
        var b = Imported(f, "b.stl").Single();
        Assert.Equal(8, Vol(f, b), 6);
        Assert.Equal(10, f.Bounds(b).Min.X, 6);
    }

    [Fact]
    public void A_glTF_with_its_buffer_inside_and_a_GLB_placed_by_their_nodes()
    {
        var f = F();
        // The cube's positions (floats) and indices (shorts), in glTF's frame (Y up): (x, y, z) model → (x, z, −y).
        var data = new List<byte>();
        foreach (var c in Corners) foreach (var v in new[] { c[0], c[2], -c[1] }) data.AddRange(BitConverter.GetBytes((float)v));
        var posLen = data.Count;
        foreach (var t in Tris) foreach (var k in t) data.AddRange(BitConverter.GetBytes((ushort)k));
        while (data.Count % 4 != 0) data.Add(0);
        JsonObject Doc(JsonNode buffer) => new()
        {
            ["asset"] = new JsonObject { ["version"] = "2.0" },
            ["scene"] = 0,
            ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = new JsonArray(0) }),
            ["nodes"] = new JsonArray(new JsonObject { ["name"] = "Block", ["mesh"] = 0, ["translation"] = new JsonArray(5, 0, 0), ["scale"] = new JsonArray(2, 2, 2) }),
            ["meshes"] = new JsonArray(new JsonObject { ["primitives"] = new JsonArray(new JsonObject { ["attributes"] = new JsonObject { ["POSITION"] = 0 }, ["indices"] = 1, ["material"] = 0 }) }),
            ["materials"] = new JsonArray(new JsonObject { ["pbrMetallicRoughness"] = new JsonObject { ["baseColorFactor"] = new JsonArray(1, 0, 0, 1) } }),
            ["buffers"] = new JsonArray(buffer),
            ["bufferViews"] = new JsonArray(
                new JsonObject { ["buffer"] = 0, ["byteOffset"] = 0, ["byteLength"] = posLen },
                new JsonObject { ["buffer"] = 0, ["byteOffset"] = posLen, ["byteLength"] = Tris.Length * 6 }),
            ["accessors"] = new JsonArray(
                new JsonObject { ["bufferView"] = 0, ["componentType"] = 5126, ["count"] = 8, ["type"] = "VEC3" },
                new JsonObject { ["bufferView"] = 1, ["componentType"] = 5123, ["count"] = Tris.Length * 3, ["type"] = "SCALAR" }),
        };
        var gltf = Doc(new JsonObject { ["byteLength"] = data.Count, ["uri"] = "data:application/octet-stream;base64," + Convert.ToBase64String(data.ToArray()) });
        MeshImport.Run(f.Session, Encoding.UTF8.GetBytes(gltf.ToJsonString()), "a.gltf", new JsonObject(), null, out _);
        var a = Imported(f, "a.gltf").Single();
        Assert.Equal(8, Vol(f, a), 6);
        Assert.Equal("Block", f.Model.Get(a)!.Name);
        var b = f.Bounds(a);
        Assert.Equal(5, b.Min.X, 6); Assert.Equal(7, b.Max.X, 6); Assert.Equal(0, b.Min.Z, 6); Assert.Equal(2, b.Max.Z, 6);
        Assert.Equal("#ff0000", f.Session.Geometry(a)!.Parts[0].Color);
        // The same as a GLB: its JSON chunk, then its binary chunk.
        var json = Encoding.UTF8.GetBytes(Doc(new JsonObject { ["byteLength"] = data.Count }).ToJsonString());
        var jsonPadded = json.Concat(Enumerable.Repeat((byte)' ', (4 - json.Length % 4) % 4)).ToArray();
        var glb = new List<byte>();
        glb.AddRange(BitConverter.GetBytes(0x46546C67u)); glb.AddRange(BitConverter.GetBytes(2u)); glb.AddRange(BitConverter.GetBytes((uint)(12 + 8 + jsonPadded.Length + 8 + data.Count)));
        glb.AddRange(BitConverter.GetBytes((uint)jsonPadded.Length)); glb.AddRange(BitConverter.GetBytes(0x4E4F534Au)); glb.AddRange(jsonPadded);
        glb.AddRange(BitConverter.GetBytes((uint)data.Count)); glb.AddRange(BitConverter.GetBytes(0x004E4942u)); glb.AddRange(data);
        MeshImport.Run(f.Session, glb.ToArray(), "b.glb", new JsonObject(), null, out _);
        Assert.Equal(8, Vol(f, Imported(f, "b.glb").Single()), 6);
        Assert.Throws<CommandException>(() => MeshImport.Run(f.Session, Encoding.UTF8.GetBytes("nothing"), "c.ply", new JsonObject(), null, out _));
    }
}
