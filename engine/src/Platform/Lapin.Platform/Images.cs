using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Images the user brings into the project (a material's own texture): each a
/// resource of its own (<see cref="Class"/>) holding the file as it came, PNG
/// or JPEG, base64, its name and MIME type. Kept in the project like any other
/// entity: saved in the .lapin, undone, versioned. A material refers to one
/// by <c>textureImage</c> and lays it on <c>textureSize</c> metres (width,
/// height); an image no material uses any more goes with the change that let
/// it go.
/// </summary>
public static class Images
{
    public const string Class = "LapinImage";
    public const int MaxBytes = 8 * 1024 * 1024;
    public static readonly string[] Mimes = ["image/png", "image/jpeg"];

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Image", [new("mime", "Type", "text", ReadOnly: true)], "#888888", Kind: "resource"));

        // An image file as base64 (PNG or JPEG, what its bytes say it is), at most 8 MB.
        r.Command("image.add", "Import image", (ctx, a) =>
        {
            var mime = (string?)a["mime"] ?? "";
            if (!Mimes.Contains(mime)) throw new CommandException("An image is a PNG or a JPEG.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String((string?)a["data"] ?? ""); }
            catch (FormatException) { throw new CommandException("The image's data is not base64."); }
            if (bytes.Length == 0 || bytes.Length > MaxBytes) throw new CommandException($"An image is at most {MaxBytes / 1024 / 1024} MB.");
            if (Sniff(bytes) != mime) throw new CommandException($"The file is not a {(mime == "image/png" ? "PNG" : "JPEG")} image.");
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : "Image";
            var image = new Entity(ctx.NewId(), Class, name, new JsonObject { ["mime"] = mime, ["data"] = Convert.ToBase64String(bytes) });
            ctx.Add(image);
            return new JsonObject { ["id"] = image.Id };
        });
    }

    /// <summary>What an image's first bytes say it is: PNG, JPEG, or neither.</summary>
    public static string? Sniff(byte[] b) =>
        b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 ? "image/png"
        : b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF ? "image/jpeg"
        : null;

    /// <summary>A material's own image, or null.</summary>
    public static Entity? Of(IModelView m, Entity material) =>
        material.Str("textureImage") is { } id && m.Get(id) is { Class: Class } image ? image : null;

    /// <summary>How large a material's image lies on the model, metres (width, height); 1 × 1 when unset.</summary>
    public static double[] SizeOf(Entity material) =>
        material.Attrs["textureSize"] is JsonArray { Count: 2 } s && Json.Number(s[0]) is { } w && Json.Number(s[1]) is { } h && w > 0 && h > 0 ? [w, h] : [1.0, 1.0];

    /// <summary>The file's bytes.</summary>
    public static byte[] Bytes(Entity image) => Convert.FromBase64String(image.Str("data") ?? "");

    /// <summary>Removes the image when no material refers to it (but <paramref name="except"/>, the one changing).</summary>
    public static void DropIfUnused(ICommandContext ctx, string? imageId, string except)
    {
        if (imageId == null || ctx.Model.Get(imageId) is not { Class: Class }) return;
        if (ctx.Model.OfClass(IfcClass.Material).Any(mat => mat.Id != except && mat.Str("textureImage") == imageId)) return;
        ctx.Remove(imageId);
    }
}
