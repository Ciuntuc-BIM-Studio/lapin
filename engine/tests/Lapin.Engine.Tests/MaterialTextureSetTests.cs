using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Mesh;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// A material's physically based maps (docs/plan-render.md, S1): a texture set (the library Lapin ships, or the user's own
/// maps) chosen in one step, its colour, normal, height, roughness and occlusion maps kept as the project's images, laid on at
/// one size; carried by the .lapin, the 3D exports and IFC (IfcSurfaceStyleWithTextures with the images inside, read back by Lapin).
/// </summary>
public class MaterialTextureSetTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "lapin-texset-" + Guid.NewGuid().ToString("N"));
    public MaterialTextureSetTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    /// <summary>Two different 1 × 1 PNGs and a tiny JPEG.</summary>
    const string PngA = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
    const string PngB = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
    static readonly string Jpeg = Convert.ToBase64String([0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1, 0xFF, 0xD9]);

    static JsonObject File(string name, string mime, string data) => new() { ["name"] = name, ["mime"] = mime, ["data"] = data };

    /// <summary>A brick wall whose material takes the set: colour, normal, height, roughness, occlusion.</summary>
    static (Fixture F, string Material) Bricks()
    {
        var f = new Fixture();
        var type = f.Type("Brick 250", ("Brick", 0.25, true));
        f.Wall(0, 0, 6, 0, type);
        var mat = Materials.Find(f.Model, "Brick")!.Id;
        f.Run("material.textures", new JsonObject
        {
            ["id"] = mat, ["set"] = "ambientcg/Bricks085", ["size"] = new JsonArray(2.4, 1.2), ["roughness"] = 1, ["aoStrength"] = 0.7,
            ["maps"] = new JsonObject
            {
                ["color"] = File("Bricks085 Color.jpg", "image/jpeg", Jpeg), ["normal"] = File("Bricks085 Normal.png", "image/png", PngA),
                ["bump"] = File("Bricks085 Height.png", "image/png", PngB), ["roughness"] = File("Bricks085 Roughness.png", "image/png", PngB),
                ["ao"] = File("Bricks085 AO.png", "image/png", PngA),
            },
        });
        return (f, mat);
    }

    [Fact]
    public void A_texture_set_is_one_step_its_maps_the_projects_images_shared_when_the_same()
    {
        var (f, mat) = Bricks();
        var m = f.Model.Get(mat)!;
        Assert.Equal("ambientcg/Bricks085", m.Str("textureSet"));
        Assert.Equal(new[] { 2.4, 1.2 }, Images.SizeOf(m));
        Assert.Equal(1, MaterialLook.Of(m).Roughness);
        Assert.Equal(0.7, MaterialLook.Of(m).AoStrength, 6);
        foreach (var (_, key) in Images.MapKeys) Assert.NotNull(Images.Of(f.Model, m, key));
        // Same bytes, one image: the normal and occlusion maps share PngA, the height and roughness maps PngB.
        Assert.Equal(m.Str("normalImage"), m.Str("aoImage"));
        Assert.Equal(m.Str("bumpImage"), m.Str("roughnessImage"));
        Assert.Equal(3, f.Model.Entities.Count(e => e.Class == Images.Class));

        // One undo step takes it all away, images too.
        f.Session.Undo();
        Assert.Null(f.Model.Get(mat)!.Str("textureSet"));
        Assert.DoesNotContain(f.Model.Entities, e => e.Class == Images.Class);
        f.Session.Redo();

        // A second material taking the same set shares its images.
        var plaster = (string)f.Run("material.create", new JsonObject { ["name"] = "Plaster", ["color"] = "#eeeeee" })!["id"]!;
        f.Run("material.textures", new JsonObject { ["id"] = plaster, ["set"] = "ambientcg/Bricks085", ["maps"] = new JsonObject { ["color"] = File("c.jpg", "image/jpeg", Jpeg) } });
        Assert.Equal(f.Model.Get(mat)!.Str("textureImage"), f.Model.Get(plaster)!.Str("textureImage"));
        Assert.Equal(3, f.Model.Entities.Count(e => e.Class == Images.Class));

        // No maps and no set: the material's maps all go, and the images nothing uses any more.
        f.Run("material.textures", new JsonObject { ["id"] = mat, ["maps"] = new JsonObject() });
        var bare = f.Model.Get(mat)!;
        Assert.All(Images.MaterialKeys, k => Assert.Null(bare.Str(k)));
        Assert.Null(bare.Str("textureSet"));
        Assert.Single(f.Model.Entities, e => e.Class == Images.Class);
    }

    [Fact]
    public void A_texture_set_is_checked()
    {
        var (f, mat) = Bricks();
        Assert.Throws<CommandException>(() => f.Run("material.textures", new JsonObject { ["id"] = mat, ["maps"] = new JsonObject { ["gloss"] = File("g.png", "image/png", PngA) } }));
        Assert.Throws<CommandException>(() => f.Run("material.textures", new JsonObject { ["id"] = mat, ["maps"] = new JsonObject { ["color"] = File("g.png", "image/jpeg", PngA) } }));
        Assert.Throws<CommandException>(() => f.Run("material.textures", new JsonObject { ["id"] = mat, ["set"] = "../etc/passwd", ["maps"] = new JsonObject() }));
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = mat, ["aoStrength"] = 2 }));
        // The material as it was: a refused step changes nothing.
        Assert.Equal("ambientcg/Bricks085", f.Model.Get(mat)!.Str("textureSet"));
    }

    [Fact]
    public void A_material_without_maps_reads_as_before()
    {
        var f = new Fixture();
        var mat = (string)f.Run("material.create", new JsonObject { ["name"] = "Concrete", ["color"] = "#999999" })!["id"]!;
        var look = MaterialLook.Of(f.Model.Get(mat));
        Assert.Equal(1, look.AoStrength);
        Assert.Equal(0.9, look.Roughness);
        Assert.All(Images.MaterialKeys, k => Assert.Null(f.Model.Get(mat)!.Str(k)));
    }

    [Fact]
    public void The_maps_are_saved_in_the_lapin_and_opened_again()
    {
        var (f, mat) = Bricks();
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        var back = new Fixture();
        var content = LapinPackage.Read(new MemoryStream(ms.ToArray()));
        back.Session.Load(content.Entities, content.Relations, null, content.Classes);
        var m = back.Model.Get(mat)!;
        Assert.Equal("ambientcg/Bricks085", m.Str("textureSet"));
        Assert.Equal(PngB, Images.Of(back.Model, m, "roughnessImage")!.Str("data"));
        Assert.Equal(PngA, Images.Of(back.Model, m, "aoImage")!.Str("data"));
        Assert.Equal(0.7, MaterialLook.Of(m).AoStrength, 6);
    }

    [Fact]
    public void A_glb_carries_the_roughness_and_occlusion_maps()
    {
        var (f, _) = Bricks();
        var (bytes, _) = MeshExport.Write(f.Session, new MeshExportOptions());
        var (doc, _) = GltfWriter.ReadGlb(bytes);
        var root = doc.RootElement;
        var brick = root.GetProperty("materials").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "Brick");
        Assert.True(brick.GetProperty("pbrMetallicRoughness").TryGetProperty("metallicRoughnessTexture", out _));
        Assert.Equal(1, brick.GetProperty("pbrMetallicRoughness").GetProperty("roughnessFactor").GetDouble());
        var occlusion = brick.GetProperty("occlusionTexture");
        Assert.Equal(0.7, occlusion.GetProperty("strength").GetDouble(), 6);
        Assert.True(brick.TryGetProperty("normalTexture", out _));

        // USD: the roughness and the occlusion read from their images.
        var (usda, _) = MeshExport.Write(f.Session, new MeshExportOptions(Format: "usda"));
        var text = System.Text.Encoding.UTF8.GetString(usda);
        Assert.Contains("inputs:roughness.connect", text);
        Assert.Contains("inputs:occlusion.connect", text);
    }

    [Fact]
    public void IFC_carries_the_maps_as_blob_textures_and_Lapin_reads_them_back()
    {
        var (f, mat) = Bricks();
        var ifc = Path.Combine(_dir, "bricks.ifc");
        IfcExport.Write(f.Session, ifc, null);
        var text = System.IO.File.ReadAllText(ifc);
        Assert.Contains("IFCSURFACESTYLEWITHTEXTURES", text);
        foreach (var mode in IfcExport.TextureModes.Values) Assert.Contains($"IFCBLOBTEXTURE(.T.,.T.,'{mode}',", text);
        Assert.Contains("'JPG',\"0" + Convert.ToHexString(Convert.FromBase64String(Jpeg)) + "\"", text);
        // The tile the texture lies on: 2.4 m × 1.2 m.
        Assert.Contains("IFCCARTESIANTRANSFORMATIONOPERATOR2DNONUNIFORM(", text);
        Assert.Contains(",2.4,1.2);", text);

        var b = new Fixture();
        IfcImport.Run(b.Session, ifc, null, out _);
        var back = b.Model.Get(mat)!;
        Assert.Equal("ambientcg/Bricks085", back.Str("textureSet"));
        Assert.Equal(Jpeg, Images.Of(b.Model, back)!.Str("data"));
        Assert.Equal("image/jpeg", Images.Of(b.Model, back)!.Str("mime"));
        Assert.Equal(PngB, Images.Of(b.Model, back, "roughnessImage")!.Str("data"));
        Assert.Equal(PngA, Images.Of(b.Model, back, "aoImage")!.Str("data"));
        Assert.Equal(new[] { 2.4, 1.2 }, Images.SizeOf(back));

        // A file whose textures were taken out: the material keeps its look, without maps that are not there.
        var stripped = Path.Combine(_dir, "stripped.ifc");
        // (The height and roughness maps are one image here: both taken out.)
        System.IO.File.WriteAllText(stripped, text.Replace("IFCBLOBTEXTURE(.T.,.T.,'ROUGHNESS'", "IFCBLOBTEXTURE(.T.,.T.,'GLOSS'").Replace("IFCBLOBTEXTURE(.T.,.T.,'BUMP'", "IFCBLOBTEXTURE(.T.,.T.,'DISPLACE'"));
        var c = new Fixture();
        IfcImport.Run(c.Session, stripped, null, out _);
        var cm = c.Model.Get(mat)!;
        Assert.Null(cm.Str("roughnessImage"));
        Assert.Null(cm.Str("bumpImage"));
        Assert.NotNull(Images.Of(c.Model, cm, "aoImage"));
    }

    [Fact]
    public void A_project_without_maps_writes_no_textures_to_IFC()
    {
        var f = new Fixture();
        var type = f.Type("Brick 250", ("Brick", 0.25, true));
        f.Wall(0, 0, 6, 0, type);
        var ifc = Path.Combine(_dir, "plain.ifc");
        IfcExport.Write(f.Session, ifc, null);
        Assert.DoesNotContain("IFCSURFACESTYLEWITHTEXTURES", System.IO.File.ReadAllText(ifc));
    }
}
