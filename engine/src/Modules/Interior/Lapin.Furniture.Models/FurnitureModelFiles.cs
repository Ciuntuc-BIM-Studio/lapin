namespace Lapin.Furniture.Models;

/// <summary>The furniture models' GLB files, as shipped (furniture-assets.json lists each with its source and licence).</summary>
public static class FurnitureModelFiles
{
    /// <summary>A model's GLB by its id, or null when there is none of that id.</summary>
    public static byte[]? Read(string id)
    {
        if (id.Length == 0 || id.Any(c => !char.IsAsciiLetterOrDigit(c))) return null;
        using var s = typeof(FurnitureModelFiles).Assembly.GetManifestResourceStream($"models/{id}.glb");
        if (s == null) return null;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
