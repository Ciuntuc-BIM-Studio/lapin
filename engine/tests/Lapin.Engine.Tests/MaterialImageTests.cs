using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Mesh;
using Lapin.Model;
using Lapin.Package;
using Lapin.Protocol;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A material's own texture (an image the user brings): kept in the project as
/// an image of its own, laid on in metres (its width and height on the model),
/// and carried by the 3D exports: glTF and GLB with the image inside and
/// texture coordinates on every vertex, USDZ with the image packed beside the
/// layer.
/// </summary>
public class MaterialImageTests
{
    /// <summary>A 1 × 1 PNG.</summary>
    const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    static (Fixture F, string Material, string Image) Textured(double w = 2, double h = 0.5)
    {
        var f = new Fixture();
        var type = f.Type("Timber", ("Timber cladding", 0.2, true));
        f.Wall(0, 0, 6, 0, type);
        var mat = Materials.Find(f.Model, "Timber cladding")!.Id;
        var img = (string)f.Run("image.add", new JsonObject { ["name"] = "boards.png", ["mime"] = "image/png", ["data"] = Png })!["id"]!;
        f.Run("material.update", new JsonObject { ["id"] = mat, ["textureImage"] = img, ["textureSize"] = new JsonArray(w, h) });
        return (f, mat, img);
    }

    [Fact]
    public void An_image_is_kept_in_the_project_and_a_material_lays_it_on_in_metres()
    {
        var (f, mat, img) = Textured();
        var image = f.Model.Get(img)!;
        Assert.Equal(Images.Class, image.Class);
        Assert.Equal("image/png", image.Str("mime"));
        Assert.Equal(Png, image.Str("data"));
        Assert.Equal(img, f.Model.Get(mat)!.Str("textureImage"));
        Assert.Equal(new[] { 2.0, 0.5 }, Images.SizeOf(f.Model.Get(mat)!));
        // Taken off, the image goes when no material uses it any more.
        f.Run("material.update", new JsonObject { ["id"] = mat, ["textureImage"] = null });
        Assert.Null(f.Model.Get(img));
    }

    [Fact]
    public void Only_png_and_jpeg_images_that_are_what_they_say_come_in()
    {
        var f = new Fixture();
        Assert.Throws<CommandException>(() => f.Run("image.add", new JsonObject { ["name"] = "x.gif", ["mime"] = "image/gif", ["data"] = Png }));
        Assert.Throws<CommandException>(() => f.Run("image.add", new JsonObject { ["name"] = "x.jpg", ["mime"] = "image/jpeg", ["data"] = Png }));
        Assert.Throws<CommandException>(() => f.Run("image.add", new JsonObject { ["name"] = "x.png", ["mime"] = "image/png", ["data"] = "not base64!" }));
        var mat = Materials.Find(f.Model, "Brick")?.Id ?? (string)f.Run("material.create", new JsonObject { ["name"] = "Brick" })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = mat, ["textureImage"] = mat }));
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = mat, ["textureSize"] = new JsonArray(0, 1) }));
    }

    [Fact]
    public void A_glb_carries_the_image_its_texture_and_coordinates_in_metres()
    {
        var (f, _, _) = Textured(2, 0.5);
        var (bytes, _) = MeshExport.Write(f.Session, new MeshExportOptions());
        var (doc, bin) = GltfWriter.ReadGlb(bytes);
        var root = doc.RootElement;
        var image = root.GetProperty("images")[0];
        Assert.Equal("image/png", image.GetProperty("mimeType").GetString());
        var view = root.GetProperty("bufferViews")[image.GetProperty("bufferView").GetInt32()];
        var stored = bin.AsSpan(view.GetProperty("byteOffset").GetInt32(), view.GetProperty("byteLength").GetInt32()).ToArray();
        Assert.Equal(Convert.FromBase64String(Png), stored);
        var mat = root.GetProperty("materials").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "Timber cladding");
        var tex = mat.GetProperty("pbrMetallicRoughness").GetProperty("baseColorTexture").GetProperty("index").GetInt32();
        Assert.Equal(0, root.GetProperty("textures")[tex].GetProperty("source").GetInt32());
        Assert.Equal(10497, root.GetProperty("samplers")[0].GetProperty("wrapS").GetInt32());
        // The wall's primitive has texture coordinates: along the wall x / 2 m, up z / 0.5 m; the 6 m wall spans 3 tiles.
        var prim = root.GetProperty("meshes").EnumerateArray().SelectMany(m => m.GetProperty("primitives").EnumerateArray())
            .First(p => p.GetProperty("attributes").TryGetProperty("TEXCOORD_0", out _));
        var acc = root.GetProperty("accessors")[prim.GetProperty("attributes").GetProperty("TEXCOORD_0").GetInt32()];
        Assert.Equal("VEC2", acc.GetProperty("type").GetString());
        var uv = root.GetProperty("bufferViews")[acc.GetProperty("bufferView").GetInt32()];
        var floats = new float[acc.GetProperty("count").GetInt32() * 2];
        Buffer.BlockCopy(bin, uv.GetProperty("byteOffset").GetInt32(), floats, 0, floats.Length * 4);
        var us = floats.Where((_, i) => i % 2 == 0).ToList();
        Assert.InRange(us.Max() - us.Min(), 2.9, 3.3);
    }

    [Fact]
    public void A_gltf_keeps_the_image_inside_and_a_usdz_packs_it_beside_its_layer()
    {
        var (f, _, _) = Textured();
        var (gltf, _) = MeshExport.Write(f.Session, new MeshExportOptions(Format: "gltf"));
        var root = JsonDocument.Parse(gltf).RootElement;
        Assert.StartsWith("data:image/png;base64,", root.GetProperty("images")[0].GetProperty("uri").GetString());

        var (usdz, _) = MeshExport.Write(f.Session, new MeshExportOptions(Format: "usdz"));
        using var zip = new ZipArchive(new MemoryStream(usdz));
        var texture = Assert.Single(zip.Entries, e => e.FullName.StartsWith("textures/") && e.FullName.EndsWith(".png"));
        using var layer = new StreamReader(zip.Entries[0].Open(), Encoding.UTF8);
        var text = layer.ReadToEnd();
        Assert.Contains("UsdUVTexture", text);
        Assert.Contains($"@{texture.FullName}@", text);
        Assert.Contains("texCoord2f[] primvars:st", text);
    }

    /// <summary>The textured wall's material with a normal map (and a bump map) of its own, at a strength.</summary>
    static (Fixture F, string Material, string Normal, string Bump) Relief()
    {
        var (f, mat, _) = Textured(2, 0.5);
        var normal = (string)f.Run("image.add", new JsonObject { ["name"] = "boards-normal.png", ["mime"] = "image/png", ["data"] = Png })!["id"]!;
        var bump = (string)f.Run("image.add", new JsonObject { ["name"] = "boards-bump.png", ["mime"] = "image/png", ["data"] = Png })!["id"]!;
        f.Run("material.update", new JsonObject { ["id"] = mat, ["normalImage"] = normal, ["normalStrength"] = 0.8, ["bumpImage"] = bump, ["bumpStrength"] = 0.3 });
        return (f, mat, normal, bump);
    }

    [Fact]
    public void A_normal_map_is_kept_with_its_strength_saved_and_opened_again()
    {
        var (f, mat, normal, bump) = Relief();
        var m = f.Model.Get(mat)!;
        Assert.Equal(normal, m.Str("normalImage"));
        Assert.Equal(bump, m.Str("bumpImage"));
        var look = MaterialLook.Of(m);
        Assert.Equal(0.8, look.NormalStrength, 6);
        Assert.Equal(0.3, look.BumpStrength, 6);
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = mat, ["normalStrength"] = 11 }));
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = mat, ["normalImage"] = mat }));

        // Saved in the .lapin and opened again: the maps and their strengths come back.
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        var back = new Fixture();
        var content = LapinPackage.Read(new MemoryStream(ms.ToArray()));
        back.Session.Load(content.Entities, content.Relations, null, content.Classes);
        var again = back.Model.Get(mat)!;
        Assert.Equal(Png, Images.Of(back.Model, again, "normalImage")!.Str("data"));
        Assert.Equal(Png, Images.Of(back.Model, again, "bumpImage")!.Str("data"));
        Assert.Equal(0.8, MaterialLook.Of(again).NormalStrength, 6);

        // Taken off, the image goes when nothing uses it; the material's own texture stays.
        f.Run("material.update", new JsonObject { ["id"] = mat, ["normalImage"] = null });
        Assert.Null(f.Model.Get(normal));
        Assert.NotNull(Images.Of(f.Model, f.Model.Get(mat)!));
    }

    [Fact]
    public void A_normal_map_goes_into_the_glb_obj_and_usd_exports()
    {
        var (f, _, _, _) = Relief();
        var (bytes, _) = MeshExport.Write(f.Session, new MeshExportOptions());
        var root = GltfWriter.ReadGlb(bytes).Json.RootElement;
        var mat = root.GetProperty("materials").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "Timber cladding");
        var normal = mat.GetProperty("normalTexture");
        Assert.Equal(0.8, normal.GetProperty("scale").GetDouble(), 4);
        var source = root.GetProperty("textures")[normal.GetProperty("index").GetInt32()].GetProperty("source").GetInt32();
        Assert.Equal("boards-normal.png", root.GetProperty("images")[source].GetProperty("name").GetString());
        Assert.NotEqual(mat.GetProperty("pbrMetallicRoughness").GetProperty("baseColorTexture").GetProperty("index").GetInt32(), normal.GetProperty("index").GetInt32());

        var (zip, _) = MeshExport.Write(f.Session, new MeshExportOptions("obj"));
        using var archive = new ZipArchive(new MemoryStream(zip));
        string Read(string n) { using var r = new StreamReader(archive.GetEntry(n)!.Open()); return r.ReadToEnd(); }
        var mtl = Read(ObjWriter.MtlName);
        var timber = mtl[mtl.IndexOf("newmtl Timber_cladding", StringComparison.Ordinal)..];
        var norm = timber.Split('\n').Single(l => l.StartsWith("norm ", StringComparison.Ordinal))[5..];
        var bumpLine = timber.Split('\n').Single(l => l.StartsWith("map_Bump ", StringComparison.Ordinal));
        Assert.StartsWith("map_Bump -bm 0.3 textures/", bumpLine);
        Assert.EndsWith("_bump.png", bumpLine);
        Assert.EndsWith("_normal.png", norm);
        Assert.NotNull(archive.GetEntry(norm));
        Assert.NotNull(archive.GetEntry(bumpLine.Split(' ')[^1]));

        // A normal map without a bump map is written as map_Bump too, as Blender reads it.
        var (g, mat2, _, _) = Relief();
        g.Run("material.update", new JsonObject { ["id"] = mat2, ["bumpImage"] = null });
        var (zip2, _) = MeshExport.Write(g.Session, new MeshExportOptions("obj"));
        using var archive2 = new ZipArchive(new MemoryStream(zip2));
        using var r2 = new StreamReader(archive2.GetEntry(ObjWriter.MtlName)!.Open());
        Assert.Contains("map_Bump -bm 0.8 textures/Timber_cladding_", r2.ReadToEnd());

        var usda = Encoding.UTF8.GetString(MeshExport.Write(f.Session, new MeshExportOptions("usda")).Bytes);
        Assert.Contains("normal3f inputs:normal.connect", usda);
        Assert.Contains("token inputs:sourceColorSpace = \"raw\"", usda);
        var (usdz, _) = MeshExport.Write(f.Session, new MeshExportOptions("usdz"));
        using var pkg = new ZipArchive(new MemoryStream(usdz));
        Assert.Single(pkg.Entries, e => e.FullName.EndsWith("_normal.png", StringComparison.Ordinal));
    }

    [Fact]
    public void A_normal_map_alone_still_gives_the_faces_their_coordinates()
    {
        var f = new Fixture();
        var type = f.Type("Stone", ("Stone wall", 0.3, true));
        f.Wall(0, 0, 4, 0, type);
        var mat = Materials.Find(f.Model, "Stone wall")!.Id;
        var img = (string)f.Run("image.add", new JsonObject { ["name"] = "stone-normal.png", ["mime"] = "image/png", ["data"] = Png })!["id"]!;
        f.Run("material.update", new JsonObject { ["id"] = mat, ["normalImage"] = img, ["textureSize"] = new JsonArray(1, 1) });
        var root = GltfWriter.ReadGlb(MeshExport.Write(f.Session, new MeshExportOptions()).Bytes).Json.RootElement;
        var stone = root.GetProperty("materials").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "Stone wall");
        Assert.True(stone.TryGetProperty("normalTexture", out _));
        Assert.False(stone.GetProperty("pbrMetallicRoughness").TryGetProperty("baseColorTexture", out _));
        Assert.Contains(root.GetProperty("meshes").EnumerateArray().SelectMany(m => m.GetProperty("primitives").EnumerateArray()),
            p => p.GetProperty("attributes").TryGetProperty("TEXCOORD_0", out _));
    }
}
