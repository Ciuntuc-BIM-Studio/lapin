using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Geometry;
using Lapin.Host;
using Lapin.Mesh;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The 3D mesh exports (docs/plan-mesh-export.md): glTF 2.0 / GLB and OpenUSD
/// (USDA, USDZ), read back and checked against the model: the hierarchy, one
/// node per element with its GlobalId, a primitive per material, the volumes,
/// Y up for glTF and Z up for USD, the origin, the filters, the packaging.
/// </summary>
public sealed class MeshExportTests
{
    /// <summary>Two storeys: a layered wall and a plain one on the ground floor, a wall on the first floor; glass (a transparent material) on one.</summary>
    static (Fixture F, string Layered, string Plain, string Upper) Project()
    {
        var f = new Fixture();
        var ground = f.Storey;
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor" })!["id"]!;
        var type = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));
        var layered = f.Wall(0, 0, 5, 0, type);
        var plain = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = ground, ["x1"] = 0, ["y1"] = 4, ["x2"] = 5, ["y2"] = 4 })!["id"]!;
        var up = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = upper, ["x1"] = 0, ["y1"] = 0, ["x2"] = 0, ["y2"] = 4 })!["id"]!;
        f.Run("material.update", new JsonObject { ["id"] = Materials.Find(f.Model, "Insulation")!.Id, ["transparency"] = 0.5 });
        return (f, layered, plain, up);
    }

    static (JsonElement Root, byte[] Bin) Glb(Fixture f, MeshExportOptions? o = null)
    {
        var (bytes, _) = MeshExport.Write(f.Session, o ?? new MeshExportOptions());
        var (doc, bin) = GltfWriter.ReadGlb(bytes);
        return (doc.RootElement, bin);
    }

    /// <summary>How many materials an element's parts have: its primitives, its USD meshes.</summary>
    static int MaterialCount(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Select(p => p.Material ?? p.Color).Distinct().Count();

    static IEnumerable<JsonElement> Arr(JsonElement e, string name) => e.TryGetProperty(name, out var a) ? a.EnumerateArray() : [];

    static JsonElement NodeOf(JsonElement root, string globalId) =>
        Arr(root, "nodes").Single(n => n.TryGetProperty("extras", out var x) && x.TryGetProperty("globalId", out var g) && g.GetString() == globalId);

    /// <summary>An accessor's floats (VEC3) or indices, read from the buffer.</summary>
    static float[] Floats(JsonElement root, byte[] bin, int accessor)
    {
        var a = Arr(root, "accessors").ElementAt(accessor);
        var v = Arr(root, "bufferViews").ElementAt(a.GetProperty("bufferView").GetInt32());
        var (offset, count) = (v.GetProperty("byteOffset").GetInt32(), a.GetProperty("count").GetInt32() * 3);
        return Enumerable.Range(0, count).Select(i => BitConverter.ToSingle(bin, offset + i * 4)).ToArray();
    }

    static int[] Ints(JsonElement root, byte[] bin, int accessor)
    {
        var a = Arr(root, "accessors").ElementAt(accessor);
        var v = Arr(root, "bufferViews").ElementAt(a.GetProperty("bufferView").GetInt32());
        var offset = v.GetProperty("byteOffset").GetInt32();
        return Enumerable.Range(0, a.GetProperty("count").GetInt32()).Select(i => (int)BitConverter.ToUInt32(bin, offset + i * 4)).ToArray();
    }

    /// <summary>The volume a closed triangle mesh holds (divergence theorem).</summary>
    static double Volume(float[] p, int[] ix)
    {
        double v = 0;
        for (var t = 0; t + 2 < ix.Length; t += 3)
        {
            var (a, b, c) = (ix[t] * 3, ix[t + 1] * 3, ix[t + 2] * 3);
            v += (p[a] * (p[b + 1] * p[c + 2] - p[b + 2] * p[c + 1]) - p[a + 1] * (p[b] * p[c + 2] - p[b + 2] * p[c]) + p[a + 2] * (p[b] * p[c + 1] - p[b + 1] * p[c])) / 6.0;
        }
        return v;
    }

    [Fact]
    public void A_glb_holds_the_storeys_and_one_node_per_element_with_its_identity()
    {
        var (f, layered, plain, upper) = Project();
        var (root, bin) = Glb(f);
        Assert.Equal("2.0", root.GetProperty("asset").GetProperty("version").GetString());
        var nodes = Arr(root, "nodes").ToList();
        // The project, the building, then the storeys with their elements.
        Assert.Equal(1, nodes[0].GetProperty("children").GetArrayLength());
        Assert.Equal("IfcBuilding", nodes[1].GetProperty("extras").GetProperty("ifcClass").GetString());
        Assert.Equal(2, nodes[1].GetProperty("children").GetArrayLength());
        foreach (var id in new[] { layered, plain, upper })
        {
            var n = NodeOf(root, id);
            Assert.Equal(IfcClass.Wall, n.GetProperty("extras").GetProperty("ifcClass").GetString());
            Assert.Equal(f.Model.Get(id)!.Name, n.GetProperty("name").GetString());
        }
        // A primitive per material of the element: the layered wall's three layers, whatever the default type holds.
        var meshes = Arr(root, "meshes").ToList();
        Assert.Equal(3, meshes[NodeOf(root, layered).GetProperty("mesh").GetInt32()].GetProperty("primitives").GetArrayLength());
        foreach (var id in new[] { plain, upper })
            Assert.Equal(MaterialCount(f, id), meshes[NodeOf(root, id).GetProperty("mesh").GetInt32()].GetProperty("primitives").GetArrayLength());
        // Positions carry their bounds; the buffer is the BIN chunk.
        Assert.All(meshes.SelectMany(m => Arr(m, "primitives")), p =>
            Assert.True(Arr(root, "accessors").ElementAt(p.GetProperty("attributes").GetProperty("POSITION").GetInt32()).TryGetProperty("min", out _)));
        Assert.Equal(bin.Length, Arr(root, "buffers").Single().GetProperty("byteLength").GetInt32());
        // The transparent insulation blends.
        var insulation = Arr(root, "materials").Single(m => m.GetProperty("name").GetString() == "Insulation");
        Assert.Equal("BLEND", insulation.GetProperty("alphaMode").GetString());
        Assert.Equal(0.5, insulation.GetProperty("pbrMetallicRoughness").GetProperty("baseColorFactor")[3].GetDouble(), 6);
    }

    [Fact]
    public void A_glb_is_y_up_and_holds_each_elements_volume()
    {
        var (f, layered, _, upper) = Project();
        var (root, bin) = Glb(f, new MeshExportOptions(Origin: "internal"));
        foreach (var id in new[] { layered, upper })
        {
            var mesh = Arr(root, "meshes").ElementAt(NodeOf(root, id).GetProperty("mesh").GetInt32());
            double volume = 0;
            var ys = new List<float>();
            foreach (var p in Arr(mesh, "primitives"))
            {
                var pos = Floats(root, bin, p.GetProperty("attributes").GetProperty("POSITION").GetInt32());
                volume += Volume(pos, Ints(root, bin, p.GetProperty("indices").GetInt32()));
                for (var i = 1; i < pos.Length; i += 3) ys.Add(pos[i]);
            }
            var solids = f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
            Assert.Equal(solids, volume, 4);
            // The model's Z is glTF's Y: the first floor's wall stands from +3.00 up.
            var storey = Spatial.StoreyOf(f.Model, id)!.Num("elevation");
            Assert.Equal(storey, ys.Min(), 4);
        }
    }

    [Fact]
    public void The_origin_is_the_base_point_or_the_survey_points_coordinates_on_the_root()
    {
        var (f, layered, _, _) = Project();
        f.Run("project.location", new JsonObject { ["base"] = new JsonObject { ["x"] = 10, ["y"] = 20, ["elevation"] = 1 }, ["survey"] = new JsonObject { ["x"] = 10, ["y"] = 20, ["easting"] = 500000, ["northing"] = 4900000, ["elevation"] = 80 } });
        float MinX(MeshExportOptions o)
        {
            var (root, bin) = Glb(f, o);
            var mesh = Arr(root, "meshes").ElementAt(NodeOf(root, layered).GetProperty("mesh").GetInt32());
            return Arr(mesh, "primitives").Min(p => Arr(root, "accessors").ElementAt(p.GetProperty("attributes").GetProperty("POSITION").GetInt32()).GetProperty("min")[0].GetSingle());
        }
        Assert.Equal(0, MinX(new MeshExportOptions(Origin: "internal")), 4);
        Assert.Equal(-10, MinX(new MeshExportOptions()), 4);
        // Survey: the vertices from the survey point, its real coordinates on the root (Y up: easting, elevation, −northing).
        var (r, _) = Glb(f, new MeshExportOptions(Origin: "survey"));
        var t = Arr(r, "nodes").First().GetProperty("translation");
        Assert.Equal([500000, 80, -4900000], t.EnumerateArray().Select(x => x.GetDouble()));
    }

    [Fact]
    public void A_3d_view_phase_and_link_filters_choose_what_goes()
    {
        var (f, layered, plain, upper) = Project();
        Assert.Equal(3, MeshScene.Build(f.Session, new MeshExportOptions()).ElementCount);
        // {3D} hiding the walls' class: nothing left to export.
        f.Run("view.graphics", new JsonObject { ["id"] = "3d", ["class"] = IfcClass.Wall, ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Throws<CommandException>(() => MeshExport.Write(f.Session, new MeshExportOptions(ViewId: "3d")));
        Assert.Equal(3, MeshScene.Build(f.Session, new MeshExportOptions()).ElementCount);
        // A saved 3D view hiding one element.
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "3d" })!["id"]!;
        var attrs = f.Model.Get(view)!.CloneAttrs();
        attrs["hiddenElements"] = new JsonArray(plain);
        f.Session.Edit("hide", e => e.Replace(f.Model.Get(view)!.With(attrs: attrs)));
        var scene = MeshScene.Build(f.Session, new MeshExportOptions(ViewId: view));
        Assert.Equal(2, scene.ElementCount);
        Assert.DoesNotContain(scene.Groups.SelectMany(g => g.Elements), e => e.GlobalId == plain);
        Assert.Throws<CommandException>(() => MeshScene.Build(f.Session, new MeshExportOptions(ViewId: "nope")));
        Assert.NotNull(layered + upper);
    }

    [Fact]
    public void Edges_go_as_lines_when_asked_and_gltf_keeps_its_buffer_inside()
    {
        var (f, layered, _, _) = Project();
        var (root, _) = Glb(f, new MeshExportOptions(Edges: true));
        var mesh = Arr(root, "meshes").ElementAt(NodeOf(root, layered).GetProperty("mesh").GetInt32());
        Assert.Contains(Arr(mesh, "primitives"), p => p.GetProperty("mode").GetInt32() == 1);
        Assert.Contains(Arr(root, "materials"), m => m.GetProperty("name").GetString() == "Edges");

        var (gltf, _) = MeshExport.Write(f.Session, new MeshExportOptions(Format: "gltf"));
        var doc = JsonDocument.Parse(gltf).RootElement;
        var buffer = Arr(doc, "buffers").Single();
        var uri = buffer.GetProperty("uri").GetString()!;
        Assert.StartsWith("data:application/octet-stream;base64,", uri);
        Assert.Equal(buffer.GetProperty("byteLength").GetInt32(), Convert.FromBase64String(uri[(uri.IndexOf(',') + 1)..]).Length);
    }

    [Fact]
    public void A_usda_stage_is_z_up_in_metres_with_its_meshes_bound_to_their_materials()
    {
        var (f, layered, plain, upper) = Project();
        var (bytes, result) = MeshExport.Write(f.Session, new MeshExportOptions(Format: "usda"));
        var text = Encoding.UTF8.GetString(bytes);
        Assert.StartsWith("#usda 1.0", text);
        Assert.Contains("upAxis = \"Z\"", text);
        Assert.Contains("metersPerUnit = 1", text);
        Assert.Contains("defaultPrim = \"Project\"", text);
        foreach (var id in new[] { layered, plain, upper }) Assert.Contains($"custom string lapin:globalId = \"{id}\"", text);
        // A Mesh per material of each wall.
        Assert.Equal(new[] { layered, plain, upper }.Sum(id => MaterialCount(f, id)), Regex.Matches(text, "def Mesh ").Count);
        // Every binding names a material the stage defines.
        var defined = Regex.Matches(text, "def Material \"([^\"]+)\"").Select(m => "/Project/Materials/" + m.Groups[1].Value).ToHashSet();
        Assert.All(Regex.Matches(text, "rel material:binding = <([^>]+)>"), m => Assert.Contains(m.Groups[1].Value, defined));
        // Each mesh's counts agree: three indices per face.
        foreach (Match m in Regex.Matches(text, @"int\[\] faceVertexCounts = \[([^\]]*)\]\s+int\[\] faceVertexIndices = \[([^\]]*)\]"))
            Assert.Equal(m.Groups[1].Value.Split(',').Length * 3, m.Groups[2].Value.Split(',').Length);
        Assert.Equal(3, result.Elements);
        // The transparent insulation has its opacity.
        Assert.Contains("float inputs:opacity = 0.5", text);
        // Names are identifiers; braces balance.
        Assert.All(Regex.Matches(text, "def \\w+ \"([^\"]+)\""), m => Assert.Matches("^[A-Za-z_][A-Za-z0-9_]*$", m.Groups[1].Value));
        Assert.Equal(text.Count(c => c == '{'), text.Count(c => c == '}'));
    }

    [Fact]
    public void A_usdz_is_an_uncompressed_zip_with_its_layer_on_a_64_byte_boundary()
    {
        var (f, _, _, _) = Project();
        var (usdz, _) = MeshExport.Write(f.Session, new MeshExportOptions(Format: "usdz"));
        var usda = UsdWriter.Usda(MeshScene.Build(f.Session, new MeshExportOptions()));
        using (var zip = new ZipArchive(new MemoryStream(usdz), ZipArchiveMode.Read))
        {
            var entry = Assert.Single(zip.Entries);
            Assert.Equal(UsdWriter.LayerName, entry.FullName);
            Assert.Equal(entry.Length, entry.CompressedLength);
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            Assert.Equal(usda, ms.ToArray());
        }
        // The local header: 30 bytes, the name, the extra field; the data right after, on 64 bytes.
        var nameLength = BitConverter.ToUInt16(usdz, 26);
        var extraLength = BitConverter.ToUInt16(usdz, 28);
        Assert.Equal(0, (30 + nameLength + extraLength) % 64);
        Assert.Equal(0, BitConverter.ToUInt16(usdz, 8));
        // Several files: each one's data aligned.
        var two = UsdWriter.Package([("a.usda", new byte[] { 1, 2, 3 }), ("textures/b.png", new byte[100])]);
        using var z2 = new ZipArchive(new MemoryStream(two), ZipArchiveMode.Read);
        Assert.Equal(["a.usda", "textures/b.png"], z2.Entries.Select(e => e.FullName));
        var second = 30 + BitConverter.ToUInt16(two, 26) + BitConverter.ToUInt16(two, 28) + 3;
        Assert.Equal(0x04034B50u, BitConverter.ToUInt32(two, second));
        Assert.Equal(0, (second + 30 + BitConverter.ToUInt16(two, second + 26) + BitConverter.ToUInt16(two, second + 28)) % 64);
    }

    [Fact]
    public void The_protocol_exports_a_mesh_as_bytes_or_to_a_path()
    {
        var (f, _, _, _) = Project();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var (reply, diff) = d.Handle("""{"id":1,"method":"project.exportMesh","params":{"format":"glb"}}""");
        Assert.Null(diff);
        var text = Encoding.UTF8.GetString(reply);
        Assert.Contains("\"name\":\"project.glb\"", text);
        Assert.Contains("\"elements\":3", text);
        var (bad, _) = d.Handle("""{"id":2,"method":"project.exportMesh","params":{"format":"skp"}}""");
        Assert.Contains("is not a 3D format", Encoding.UTF8.GetString(bad));
        Assert.Equal("_3_Rooms", UsdWriter.Identifier("3 Rooms"));
        Assert.Equal("Perete_exterior", UsdWriter.Identifier("Perete exterior"));
        Assert.Equal("Fundatie", UsdWriter.Identifier("Fundație"));
    }

    [Fact]
    public void A_collada_file_is_z_up_in_metres_with_a_geometry_per_element_and_its_identity()
    {
        var (f, layered, plain, upper) = Project();
        var (bytes, result) = MeshExport.Write(f.Session, new MeshExportOptions(Format: "dae", Edges: true));
        var doc = System.Xml.Linq.XDocument.Parse(Encoding.UTF8.GetString(bytes));
        System.Xml.Linq.XNamespace c = "http://www.collada.org/2005/11/COLLADASchema";
        var root = doc.Root!;
        Assert.Equal("1.4.1", (string?)root.Attribute("version"));
        Assert.Equal("Z_UP", root.Element(c + "asset")!.Element(c + "up_axis")!.Value);
        Assert.Equal("1", (string?)root.Element(c + "asset")!.Element(c + "unit")!.Attribute("meter"));
        Assert.Equal(3, result.Elements);
        var geometries = root.Element(c + "library_geometries")!.Elements(c + "geometry").ToList();
        Assert.Equal(3, geometries.Count);
        // Every element's node carries its GlobalId and binds every material its triangles name.
        var nodes = root.Descendants(c + "node").Where(n => n.Element(c + "instance_geometry") != null).ToList();
        Assert.Equal(new[] { layered, plain, upper }.Order(), nodes.Select(n => n.Element(c + "extra")!.Descendants(c + "globalId").Single().Value).Order());
        foreach (var n in nodes)
        {
            var url = ((string)n.Element(c + "instance_geometry")!.Attribute("url")!)[1..];
            var mesh = geometries.Single(g => (string?)g.Attribute("id") == url).Element(c + "mesh")!;
            var bound = n.Descendants(c + "instance_material").Select(x => (string)x.Attribute("symbol")!).ToHashSet();
            Assert.All(mesh.Elements().Where(x => x.Name == c + "triangles" || x.Name == c + "lines"), t => Assert.Contains((string)t.Attribute("material")!, bound));
            // The counts agree: three indices a triangle, every index within the points.
            var points = int.Parse((string)mesh.Elements(c + "source").First().Element(c + "float_array")!.Attribute("count")!) / 3;
            foreach (var t in mesh.Elements(c + "triangles"))
            {
                var ix = t.Element(c + "p")!.Value.Split(' ').Select(int.Parse).ToList();
                Assert.Equal(int.Parse((string)t.Attribute("count")!) * 3, ix.Count);
                Assert.All(ix, i => Assert.InRange(i, 0, points - 1));
            }
            Assert.Single(mesh.Elements(c + "lines"));
        }
        // The layered wall: its triangles by its three materials; the transparent insulation's opacity.
        var layeredMesh = geometries.Single(g => (string?)g.Attribute("id") == ((string)nodes.Single(n => n.Element(c + "extra")!.Descendants(c + "globalId").Single().Value == layered).Element(c + "instance_geometry")!.Attribute("url")!)[1..]);
        Assert.Equal(3, layeredMesh.Element(c + "mesh")!.Elements(c + "triangles").Count());
        Assert.Contains(root.Descendants(c + "transparent"), t => t.Element(c + "color")!.Value == "1 1 1 0.5");
        // Ids are unique; names are NCNames.
        var ids = root.Descendants().Select(x => (string?)x.Attribute("id")).OfType<string>().ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(root.Descendants().Select(x => (string?)x.Attribute("name")).OfType<string>(), nm => Assert.Matches("^[A-Za-z_][A-Za-z0-9_]*$", nm));
    }
}
