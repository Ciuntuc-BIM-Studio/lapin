using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Mesh;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Materials for rendering (tester's report #76): a material's look (roughness, metalness, transparency, glass, a glow,
/// its texture's turn) beside its 2D graphics, guessed from its name where not set; duplicated, replaced everywhere in one
/// step, deleted with another in its place; doors, windows and curtain walls made of materials part by part, set for every
/// type at once; and every 3D export (GLB, USDA, DAE, OBJ) and IFC carrying one material per project material under its
/// exact name with its values.
/// </summary>
public class MaterialAppearanceTests
{
    static List<object?> Items(object? o) => o as List<object?> ?? [];

    static string Mat(Fixture f, string name) => Materials.Find(f.Model, name)!.Id;

    /// <summary>A wall of a brick type with a window and a door.</summary>
    static (Fixture F, string Wall, string Window, string Door) House()
    {
        var f = new Fixture();
        var type = f.Type("Brick 250", ("Brick", 0.25, true));
        var wall = f.Wall(0, 0, 8, 0, type);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 2 })!["id"]!;
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["x"] = 6, ["y"] = 0 })!["id"]!;
        return (f, wall, window, door);
    }

    [Theory]
    [InlineData("Oak parquet", 0.45, 0, 0)]
    [InlineData("Laminate", 0.45, 0, 0)]
    [InlineData("Glass blue", 0.05, 0, 0.6)]
    [InlineData("Glass wool", 0.97, 0, 0)]
    [InlineData("Aluminium anodised", 0.3, 1, 0)]
    [InlineData("Aluminium RAL 7016", 0.45, 0.1, 0)]
    [InlineData("Oțel", 0.35, 1, 0)]
    [InlineData("Sticlă", 0.05, 0, 0.6)]
    public void A_name_says_how_a_material_renders(string name, double roughness, double metalness, double transparency)
    {
        var l = MaterialLook.Guess(name);
        Assert.Equal(roughness, l.Roughness, 3);
        Assert.Equal(metalness, l.Metalness, 3);
        Assert.Equal(transparency, l.Transparency, 3);
    }

    [Fact]
    public void Parquet_and_laminate_are_wood_coloured_not_grey()
    {
        Assert.NotEqual("#c9ced6", Materials.DefaultColor("Parquet"));
        Assert.NotEqual("#c9ced6", Materials.DefaultColor("Laminate flooring"));
        Assert.Equal("#f2c46d", Materials.DefaultColor("Glass wool"));
    }

    [Fact]
    public void A_material_keeps_its_look_and_the_rest_comes_from_its_name()
    {
        var f = new Fixture();
        var id = (string)f.Run("material.create", new JsonObject
        {
            ["name"] = "Aluminium RAL 7016 anthracite", ["color"] = "#383e42", ["roughness"] = 0.45, ["metalness"] = 0.1, ["textureRotation"] = 90,
        })!["id"]!;
        var mat = f.Model.Get(id)!;
        Assert.Equal(0.45, mat.Num("roughness"));
        Assert.Equal(90, mat.Num("textureRotation"));
        var listed = ((JsonArray)f.Session.Query("material.list", new JsonObject())!).OfType<JsonObject>().Single(o => (string?)o["id"] == id);
        var look = (JsonObject)listed["look"]!;
        Assert.Equal(0.1, Json.Number(look["metalness"]));
        Assert.Equal(0.0, Json.Number(look["transparency"]));
        Assert.Contains("roughness", ((JsonArray)look["own"]!).Select(x => (string?)x));
        Assert.DoesNotContain("transparency", ((JsonArray)look["own"]!).Select(x => (string?)x));
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = id, ["roughness"] = 2 }));
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = id, ["emissive"] = "orange" }));
        // Taken off, a field is the name's again.
        f.Run("material.update", new JsonObject { ["id"] = id, ["roughness"] = null });
        Assert.Null(f.Model.Get(id)!.NumOrNull("roughness"));
    }

    [Fact]
    public void A_material_is_duplicated_with_its_look_under_a_new_name()
    {
        var f = new Fixture();
        var id = (string)f.Run("material.create", new JsonObject { ["name"] = "Glass blue", ["color"] = "#6f9fc8", ["transparency"] = 0.6 })!["id"]!;
        var copy = (string)f.Run("material.duplicate", new JsonObject { ["id"] = id })!["id"]!;
        var c = f.Model.Get(copy)!;
        Assert.Equal("Glass blue copy", c.Name);
        Assert.Equal("#6f9fc8", c.Str("color"));
        Assert.Equal(0.6, c.Num("transparency"));
        Assert.Equal("Glass blue copy 2", f.Model.Get((string)f.Run("material.duplicate", new JsonObject { ["id"] = id })!["id"]!)!.Name);
    }

    [Fact]
    public void New_doors_and_windows_are_made_of_shared_materials_part_by_part()
    {
        var (f, _, window, door) = House();
        var parts = f.Session.Geometry(window)!.Parts;
        Assert.Equal(Mat(f, "Window frame"), parts.First(p => p.Key == "frame").Material);
        Assert.Equal(Mat(f, "Glass"), parts.First(p => p.Key == "glass").Material);
        Assert.Equal(0.6, MaterialLook.Of(f.Model.Get(Mat(f, "Glass"))).Transparency);
        var doorParts = f.Session.Geometry(door)!.Parts;
        Assert.Equal(Mat(f, "Door leaf"), doorParts.First(p => p.Key == "leaf").Material);
        // The glass is the same material in both.
        Assert.Single(f.Model.OfClass(IfcClass.Material), m => m.Name == "Glass");
        // A part's colour is its material's.
        f.Run("material.update", new JsonObject { ["id"] = Mat(f, "Window frame"), ["color"] = "#383e42" });
        Assert.Equal("#383e42", f.Session.Geometry(window)!.Parts.First(p => p.Key == "frame").Color);
    }

    [Fact]
    public void Every_window_takes_one_frame_and_one_glass_material_in_one_step()
    {
        var (f, wall, window, door) = House();
        // A second window type without materials (as an old project's): its parts have colours only.
        var spec = JoinerySpecs.Window with { Name = "Window 600", Width = 0.6, Height = 0.6 };
        var old = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        f.Run("architecture.updateJoineryType", new JsonObject { ["id"] = old, ["spec"] = (spec with { Materials = null }).ToJson() });
        var second = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4, ["typeId"] = old })!["id"]!;
        Assert.Null(f.Session.Geometry(second)!.Parts.First(p => p.Key == "frame").Material);

        f.Run("architecture.joineryMaterials", new JsonObject { ["kind"] = "window", ["frame"] = "Aluminium RAL 7016 anthracite", ["glass"] = "Glass blue tinted" });
        foreach (var w in new[] { window, second })
        {
            var parts = f.Session.Geometry(w)!.Parts;
            Assert.Equal(Mat(f, "Aluminium RAL 7016 anthracite"), parts.First(p => p.Key == "frame").Material);
            Assert.Equal(Mat(f, "Glass blue tinted"), parts.First(p => p.Key == "glass").Material);
        }
        // The doors keep theirs.
        Assert.Equal(Mat(f, "Door frame"), f.Session.Geometry(door)!.Parts.First(p => p.Key == "frame").Material);
        // One undo step takes it all back.
        f.Session.Undo();
        Assert.Equal(Mat(f, "Window frame"), f.Session.Geometry(window)!.Parts.First(p => p.Key == "frame").Material);
        Assert.Null(Materials.Find(f.Model, "Glass blue tinted"));
    }

    [Fact]
    public void A_material_replaced_is_replaced_everywhere_in_one_step_and_may_go()
    {
        var (f, wall, window, _) = House();
        var blue = (string)f.Run("material.create", new JsonObject { ["name"] = "Glass blue", ["color"] = "#6f9fc8" })!["id"]!;
        var block = (string)f.Run("material.create", new JsonObject { ["name"] = "Clay block" })!["id"]!;
        var glass = Mat(f, "Glass");
        var brick = Mat(f, "Brick");
        f.Run("material.replace", new JsonObject { ["id"] = glass, ["with"] = blue, ["delete"] = true });
        Assert.Null(f.Model.Get(glass));
        Assert.Equal(blue, f.Session.Geometry(window)!.Parts.First(p => p.Key == "glass").Material);
        // A layer set refers to its material by id.
        f.Run("material.delete", new JsonObject { ["id"] = brick, ["replaceWith"] = block });
        Assert.Null(f.Model.Get(brick));
        Assert.All(f.Session.Geometry(wall)!.Parts.Where(p => p.Material != null), p => Assert.Equal(block, p.Material));
        Assert.Throws<CommandException>(() => f.Run("material.delete", new JsonObject { ["id"] = block }));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(brick));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(glass));
        Assert.Equal(glass, f.Session.Geometry(window)!.Parts.First(p => p.Key == "glass").Material);
    }

    [Fact]
    public void A_material_renamed_is_found_again_by_the_parts_that_name_it()
    {
        var (f, _, window, _) = House();
        f.Run("material.update", new JsonObject { ["id"] = Mat(f, "Window frame"), ["name"] = "Frames anthracite" });
        Assert.Equal(Mat(f, "Frames anthracite"), f.Session.Geometry(window)!.Parts.First(p => p.Key == "frame").Material);
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = Mat(f, "Frames anthracite"), ["name"] = "Glass" }));
        // Named by its name, it is in use: it goes only with another in its place.
        Assert.Throws<CommandException>(() => f.Run("material.delete", new JsonObject { ["id"] = Mat(f, "Frames anthracite") }));
    }

    [Fact]
    public void The_elements_made_of_a_material_are_found_to_select_them()
    {
        var (f, wall, window, door) = House();
        List<string?> Uses(string material) => ((JsonArray)f.Session.Query("material.uses", new JsonObject { ["id"] = Mat(f, material) })!).Select(x => (string?)x).ToList();
        Assert.Equal([window], Uses("Glass"));
        Assert.Equal([door], Uses("Door leaf"));
        Assert.Equal([wall], Uses("Brick"));
    }

    [Fact]
    public void A_curtain_wall_is_made_of_its_frame_and_the_glass_material()
    {
        var f = new Fixture();
        var cw = (string)f.Run("architecture.addCurtainWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["height"] = 3 })!["id"]!;
        var parts = f.Session.Geometry(cw)!.Parts;
        Assert.Equal(Mat(f, "Curtain wall frame"), parts.First(p => p.Name == "Mullion").Material);
        Assert.Equal(Mat(f, "Glass"), parts.First(p => p.Name == "Glass panel").Material);
        f.Run("architecture.joineryMaterials", new JsonObject { ["kind"] = "all", ["frame"] = "Aluminium anodised dark grey" });
        Assert.Equal(Mat(f, "Aluminium anodised dark grey"), f.Session.Geometry(cw)!.Parts.First(p => p.Name == "Mullion").Material);
    }

    /// <summary>The house with dark grey frames and blue glass, as the tester asked.</summary>
    static Fixture Rendered()
    {
        var (f, _, _, _) = House();
        f.Run("material.create", new JsonObject { ["name"] = "Aluminium RAL 7016 anthracite", ["color"] = "#383e42", ["roughness"] = 0.45, ["metalness"] = 0.1 });
        f.Run("material.create", new JsonObject { ["name"] = "Glass blue tinted", ["color"] = "#6f9fc8", ["roughness"] = 0.03, ["transparency"] = 0.6, ["transmission"] = 0.9, ["ior"] = 1.52 });
        f.Run("architecture.joineryMaterials", new JsonObject { ["kind"] = "all", ["frame"] = "Aluminium RAL 7016 anthracite", ["glass"] = "Glass blue tinted" });
        return f;
    }

    [Fact]
    public void A_glb_has_one_material_per_project_material_under_its_exact_name_with_its_values()
    {
        var f = Rendered();
        var (bytes, _) = MeshExport.Write(f.Session, new MeshExportOptions());
        var (doc, _) = GltfWriter.ReadGlb(bytes);
        var root = doc.RootElement;
        var materials = root.GetProperty("materials").EnumerateArray().ToList();
        var names = materials.Select(m => m.GetProperty("name").GetString()!).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.Contains("Brick", names);
        var frame = materials.Single(m => m.GetProperty("name").GetString() == "Aluminium RAL 7016 anthracite");
        var pbr = frame.GetProperty("pbrMetallicRoughness");
        Assert.Equal(0.1, pbr.GetProperty("metallicFactor").GetDouble(), 4);
        Assert.Equal(0.45, pbr.GetProperty("roughnessFactor").GetDouble(), 4);
        var glass = materials.Single(m => m.GetProperty("name").GetString() == "Glass blue tinted");
        Assert.Equal("BLEND", glass.GetProperty("alphaMode").GetString());
        Assert.Equal(0.4, glass.GetProperty("pbrMetallicRoughness").GetProperty("baseColorFactor")[3].GetDouble(), 3);
        Assert.Equal(0.9, glass.GetProperty("extensions").GetProperty("KHR_materials_transmission").GetProperty("transmissionFactor").GetDouble(), 4);
        Assert.Equal(1.52, glass.GetProperty("extensions").GetProperty("KHR_materials_ior").GetProperty("ior").GetDouble(), 4);
        var used = root.GetProperty("extensionsUsed").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("KHR_materials_transmission", used);
        Assert.Contains("KHR_materials_ior", used);
        // The window's mesh is split by material: a primitive for the frame, one for the glass.
        var window = root.GetProperty("nodes").EnumerateArray().First(n => n.TryGetProperty("extras", out var x) && x.GetProperty("ifcClass").GetString() == "IfcWindow");
        var prims = root.GetProperty("meshes")[window.GetProperty("mesh").GetInt32()].GetProperty("primitives").EnumerateArray()
            .Select(p => names[p.GetProperty("material").GetInt32()]).ToList();
        Assert.Contains("Aluminium RAL 7016 anthracite", prims);
        Assert.Contains("Glass blue tinted", prims);
        Assert.Equal(prims.Count, prims.Distinct().Count());
    }

    [Fact]
    public void Usd_collada_and_obj_carry_the_names_and_values_too()
    {
        var f = Rendered();
        var usda = Encoding.UTF8.GetString(MeshExport.Write(f.Session, new MeshExportOptions("usda")).Bytes);
        Assert.Contains("displayName = \"Aluminium RAL 7016 anthracite\"", usda);
        Assert.Contains("float inputs:roughness = 0.45", usda);
        Assert.Contains("float inputs:ior = 1.52", usda);
        Assert.Contains("float inputs:opacity = 0.4", usda);

        var dae = Encoding.UTF8.GetString(MeshExport.Write(f.Session, new MeshExportOptions("dae")).Bytes);
        Assert.Contains("name=\"Glass_blue_tinted\"", dae);
        Assert.Contains("<index_of_refraction>", dae);

        var (zip, result) = MeshExport.Write(f.Session, new MeshExportOptions("obj"));
        Assert.Equal("zip", new MeshExportOptions("obj").Extension);
        using var archive = new ZipArchive(new MemoryStream(zip));
        string Read(string n) { using var r = new StreamReader(archive.GetEntry(n)!.Open()); return r.ReadToEnd(); }
        var mtl = Read(ObjWriter.MtlName);
        var obj = Read(ObjWriter.ObjName);
        Assert.Contains("mtllib model.mtl", obj);
        Assert.Contains("newmtl Aluminium_RAL_7016_anthracite\n", mtl);
        Assert.Contains("newmtl Glass_blue_tinted\n", mtl);
        Assert.Equal(result.Materials, mtl.Split('\n').Count(l => l.StartsWith("newmtl ")));
        var glass = mtl[mtl.IndexOf("newmtl Glass_blue_tinted", StringComparison.Ordinal)..];
        Assert.Contains("d 0.4", glass);
        Assert.Contains("Ni 1.52", glass);
        Assert.Contains("usemtl Glass_blue_tinted", obj);
        // Every face's indices are within the vertices written.
        var vertices = obj.Split('\n').Count(l => l.StartsWith("v "));
        Assert.All(obj.Split('\n').Where(l => l.StartsWith("f ")).SelectMany(l => l[2..].Split(' ')), c => Assert.InRange(int.Parse(c.Split('/')[0]), 1, vertices));
    }

    [Fact]
    public void Ifc_names_each_material_style_after_it_with_its_rendering()
    {
        var f = Rendered();
        var path = Path.Combine(Directory.CreateTempSubdirectory("lapin-mat76").FullName, "house.ifc");
        IfcExport.Write(f.Session, path);
        var text = File.ReadAllText(path);
        var file = StepFile.Parse(text);
        var styles = file.OfType("IFCSURFACESTYLE").ToList();
        var glass = styles.Single(s => (string?)s[0] == "Glass blue tinted");
        var rendering = file.Get(Items(glass[2]).Single())!;
        Assert.Equal("IFCSURFACESTYLERENDERING", rendering.Type);
        Assert.Equal(0.6, Convert.ToDouble(rendering[1], System.Globalization.CultureInfo.InvariantCulture), 3);
        Assert.Equal("GLASS", (rendering[8] as StepEnum)?.Value);
        var frame = styles.Single(s => (string?)s[0] == "Aluminium RAL 7016 anthracite");
        Assert.Equal("PLASTIC", (file.Get(Items(frame[2]).Single())![8] as StepEnum)?.Value);
        // One style per material: the window's frame and the door's frame share it.
        Assert.Single(styles, s => (string?)s[0] == "Aluminium RAL 7016 anthracite");
    }

    [Fact]
    public void Ifc_carries_a_window_and_door_material_per_part_as_a_constituent_set_and_reads_it_back()
    {
        var f = Rendered();
        var path = Path.Combine(Directory.CreateTempSubdirectory("lapin-mat76").FullName, "house.ifc");
        IfcExport.Write(f.Session, path);
        var file = StepFile.Parse(File.ReadAllText(path));
        var sets = file.OfType("IFCMATERIALCONSTITUENTSET").ToList();
        var windowSet = sets.Single(s => (string?)s[0] == "Window");
        var constituents = Items(windowSet[2]).Select(c => file.Get(c)!).ToDictionary(c => (string)c[0]!, c => (string?)file.Get(c[2])![0]);
        Assert.Equal("Aluminium RAL 7016 anthracite", constituents["Frame"]);
        Assert.Equal("Glass blue tinted", constituents["Glazing"]);
        Assert.Contains("Board", constituents.Keys);
        Assert.Contains("Panel", sets.Where(s => (string?)s[0] == "Door").SelectMany(s => Items(s[2])).Select(c => (string?)file.Get(c)![0]));
        // Associated to the window and to its type.
        var rel = file.OfType("IFCRELASSOCIATESMATERIAL").Single(r => file.Get(r[5])?.Id == windowSet.Id);
        var related = Items(rel[4]).Select(r => file.Get(r)!.Type).ToList();
        Assert.Contains("IFCWINDOW", related);
        Assert.Contains("IFCWINDOWTYPE", related);

        // Read back into a new project: the window type is made of the same materials, part by part.
        var back = new Fixture();
        IfcImport.Run(back.Session, path, null, out _);
        var window = back.Model.OfClass(IfcClass.Window).Single();
        var spec = Openings.SpecOf(back.Model, window.Id)!;
        Assert.Equal("Aluminium RAL 7016 anthracite", spec.Materials!.Frame);
        Assert.Equal("Glass blue tinted", spec.Materials.Glass);
    }
}
