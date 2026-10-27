using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Mesh;
using Lapin.Model;
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
}
