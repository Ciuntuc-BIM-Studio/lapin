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

    /// <summary>A glTF of nodes only, placed by <paramref name="roots"/>: what a crafted file holds.</summary>
    static byte[] Gltf(JsonArray nodes, JsonArray roots) => Encoding.UTF8.GetBytes(new JsonObject
    {
        ["asset"] = new JsonObject { ["version"] = "2.0" },
        ["scene"] = 0,
        ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = roots }),
        ["nodes"] = nodes,
    }.ToJsonString());

    [Fact]
    public void A_glTF_node_hierarchy_with_a_cycle_is_refused_not_recursed_forever()
    {
        var f = F();
        var self = Gltf(new JsonArray(new JsonObject { ["children"] = new JsonArray(0) }), new JsonArray(0));
        Assert.Contains("cycle", Assert.Throws<CommandException>(() => MeshImport.Run(f.Session, self, "self.gltf", new JsonObject(), null, out _)).Message);
        var loop = Gltf(new JsonArray(new JsonObject { ["children"] = new JsonArray(1) }, new JsonObject { ["children"] = new JsonArray(0) }), new JsonArray(0));
        Assert.Contains("cycle", Assert.Throws<CommandException>(() => MeshImport.Run(f.Session, loop, "loop.gltf", new JsonObject(), null, out _)).Message);
        // A chain deeper than any real file, refused before the stack runs out.
        var deep = new JsonArray();
        for (var i = 0; i < 10_000; i++) deep.Add(i + 1 < 10_000 ? new JsonObject { ["children"] = new JsonArray(i + 1) } : new JsonObject());
        Assert.Contains("deeper", Assert.Throws<CommandException>(() => MeshImport.Run(f.Session, Gltf(deep, new JsonArray(0)), "deep.gltf", new JsonObject(), null, out _)).Message);
    }

    [Fact]
    public void A_malformed_glTF_or_OBJ_is_refused_with_a_reason()
    {
        var f = F();
        var tri = new List<byte>();
        foreach (var v in new float[] { 0, 0, 0, 1, 0, 0, 0, 0, 1 }) tri.AddRange(BitConverter.GetBytes(v));
        var uri = "data:application/octet-stream;base64," + Convert.ToBase64String(tri.ToArray());
        byte[] Doc(JsonObject accessor) => Encoding.UTF8.GetBytes(new JsonObject
        {
            ["asset"] = new JsonObject { ["version"] = "2.0" },
            ["scene"] = 0,
            ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = new JsonArray(0) }),
            ["nodes"] = new JsonArray(new JsonObject { ["mesh"] = 0 }),
            ["meshes"] = new JsonArray(new JsonObject { ["primitives"] = new JsonArray(new JsonObject { ["attributes"] = new JsonObject { ["POSITION"] = 0 } }) }),
            ["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = 36, ["uri"] = uri }),
            ["bufferViews"] = new JsonArray(new JsonObject { ["buffer"] = 0, ["byteLength"] = 36 }),
            ["accessors"] = new JsonArray(accessor),
        }.ToJsonString());
        string Refused(byte[] bytes, string name) => Assert.Throws<CommandException>(() => MeshImport.Run(f.Session, bytes, name, new JsonObject(), null, out _)).Message;
        Assert.Equal(1, MeshImport.Run(f.Session, Doc(new JsonObject { ["bufferView"] = 0, ["componentType"] = 5126, ["count"] = 3, ["type"] = "VEC3" }), "ok.gltf", new JsonObject(), null, out _).Elements);
        // A sparse accessor has no view; an accessor may lack its count; or claim more than its buffer holds.
        Assert.Contains("bufferView", Refused(Doc(new JsonObject { ["componentType"] = 5126, ["count"] = 3, ["type"] = "VEC3", ["sparse"] = new JsonObject { ["count"] = 0 } }), "sparse.gltf"));
        Assert.Contains("count", Refused(Doc(new JsonObject { ["bufferView"] = 0, ["componentType"] = 5126, ["type"] = "VEC3" }), "nocount.gltf"));
        Assert.Contains("past the end", Refused(Doc(new JsonObject { ["bufferView"] = 0, ["componentType"] = 5126, ["count"] = 1_000_000_000, ["type"] = "VEC3" }), "big.gltf"));
        // A GLB chunk longer than the file.
        var glb = new byte[28];
        BitConverter.GetBytes(0x46546C67u).CopyTo(glb, 0); BitConverter.GetBytes(2u).CopyTo(glb, 4); BitConverter.GetBytes(28u).CopyTo(glb, 8);
        BitConverter.GetBytes(0xFFFFFFF0u).CopyTo(glb, 12); BitConverter.GetBytes(0x4E4F534Au).CopyTo(glb, 16);
        Assert.Contains("past the end", Refused(glb, "chunk.glb"));
        // An OBJ face naming a vertex past what an int holds.
        Refused(Encoding.UTF8.GetBytes("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 99999999999\n"), "huge.obj");
    }

    [Fact]
    public void A_binary_STL_with_bytes_after_its_triangles_or_a_header_saying_solid_is_read()
    {
        var f = F();
        var bin = new byte[84 + 50 * Tris.Length + 16];
        Encoding.ASCII.GetBytes("solid exported by a program that writes its name here").CopyTo(bin, 0);
        BitConverter.GetBytes((uint)Tris.Length).CopyTo(bin, 80);
        for (var i = 0; i < Tris.Length; i++)
            for (var k = 0; k < 3; k++)
                for (var c = 0; c < 3; c++)
                    BitConverter.GetBytes((float)Corners[Tris[i][k]][c]).CopyTo(bin, 84 + 50 * i + 12 + 12 * k + 4 * c);
        var r = MeshImport.Run(f.Session, bin, "padded.stl", new JsonObject(), null, out _);
        Assert.Equal(12, r.Triangles);
        Assert.Equal(1, Vol(f, Imported(f, "padded.stl").Single()), 6);
    }

    [Fact]
    public void STL_and_glTF_corners_are_welded_to_the_same_tolerance_as_the_Fragments_writer()
    {
        // Two triangles on one edge, its second copy off by float noise (1e-7): one edge, four corners.
        var text = "solid s\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\nendloop\nendfacet\n"
            + "facet normal 0 0 1\nouter loop\nvertex 1.0000001 0 0\nvertex 1 1 0\nvertex 0 1.0000001 0\nendloop\nendfacet\nendsolid s\n";
        Assert.Equal(4, MeshImport.Stl(Encoding.UTF8.GetBytes(text), "s").Single().Vertices.Count);
        // A flat-shaded glTF (a vertex per face corner, as Lapin's own export writes one): the cube's 36 corners are its 8.
        var data = new List<byte>();
        foreach (var t in Tris) foreach (var k in t) foreach (var v in new[] { Corners[k][0], Corners[k][2], -Corners[k][1] }) data.AddRange(BitConverter.GetBytes((float)v));
        var doc = new JsonObject
        {
            ["asset"] = new JsonObject { ["version"] = "2.0" },
            ["nodes"] = new JsonArray(new JsonObject { ["mesh"] = 0 }),
            ["meshes"] = new JsonArray(new JsonObject { ["primitives"] = new JsonArray(new JsonObject { ["attributes"] = new JsonObject { ["POSITION"] = 0 } }) }),
            ["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = data.Count, ["uri"] = "data:application/octet-stream;base64," + Convert.ToBase64String(data.ToArray()) }),
            ["bufferViews"] = new JsonArray(new JsonObject { ["buffer"] = 0, ["byteLength"] = data.Count }),
            ["accessors"] = new JsonArray(new JsonObject { ["bufferView"] = 0, ["componentType"] = 5126, ["count"] = 36, ["type"] = "VEC3" }),
        };
        var piece = MeshImport.Gltf(Encoding.UTF8.GetBytes(doc.ToJsonString()), false, null, []).Single();
        Assert.Equal(8, piece.Vertices.Count);
        Assert.Equal(12, piece.Triangles.Count);
    }

    [Fact]
    public void An_import_into_a_project_without_storeys_makes_the_products_first_storey()
    {
        var session = new Session(ModuleCatalog.Create([new ArchitectureModule()]), new ProductManifest("p", "P", ["core", "architecture"], Storeys: [new ProductStorey("Parter", 0, 2.8)]));
        session.Edit("No storeys", edit => { foreach (var s in Spatial.Storeys(edit.Model).ToList()) edit.Remove(s.Id); });
        Assert.Empty(Spatial.Storeys(session.Model));
        MeshImport.Run(session, Encoding.UTF8.GetBytes("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n"), "t.obj", new JsonObject(), null, out _);
        var storey = Spatial.Storeys(session.Model).Single();
        Assert.Equal("Parter", storey.Name);
        Assert.Equal(2.8, storey.Num("height"));
    }

    [Fact]
    public void OBJ_pieces_of_one_name_are_told_apart()
    {
        var obj = "v 0 0 0\nv 1 0 0\nv 0 1 0\no A\nf 1 2 3\no A\nf 1 2 3\no B\nf 1 2 3\no A\nf 1 2 3\n";
        Assert.Equal(["A", "A 2", "B", "A 4"], MeshImport.Obj(Encoding.UTF8.GetBytes(obj), []).Select(p => p.Name));
    }
}
