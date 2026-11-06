using System.IO.Compression;

namespace Lapin.Package;

/// <summary>An entry another tool put in the package: its path, its bytes as they were read, when the zip says it was written.</summary>
public sealed record ForeignPart(string Path, byte[] Bytes, DateTimeOffset Modified)
{
    public PackagePart Part => new(Path, LapinPackage.Hash(Bytes), Bytes.LongLength);
}

/// <summary>
/// The entries of a package Lapin does not own (docs/format-lapin.md, Părți
/// străine): another tool's folder beside the project (<c>dag/</c>,
/// <c>source/</c>, any top-level folder or file but Lapin's own), read with
/// the file and written back byte for byte with every save, listed in the
/// manifest's <c>foreign</c> with their hashes. Never a part of a version: a
/// version keeps the project, not what other tools left beside it. Held in
/// memory, as read when the project was opened: a tool writing into the file
/// while Lapin has it open loses its change at the next save, as with any
/// other editor of the same file.
/// </summary>
public sealed class PackageForeign
{
    /// <summary>The top-level names Lapin owns: written by it, or kept for what it will write (the layout in docs/format-lapin.md). An entry under one of them is never kept.</summary>
    public static readonly IReadOnlyList<string> Owned = ["manifest.json", "project", "model", "versions", "issues", "journal", "derived", "analytics"];

    public IReadOnlyList<ForeignPart> Parts { get; }

    PackageForeign(IReadOnlyList<ForeignPart> parts) => Parts = parts;

    /// <summary>A project with nothing of other tools.</summary>
    public static readonly PackageForeign None = new([]);

    /// <summary>The foreign entries of the package at <paramref name="path"/>; none when it has none, or is not a format 3 package.</summary>
    public static PackageForeign Of(string path)
    {
        if (!File.Exists(path)) return None;
        try
        {
            using var file = File.OpenRead(path);
            return Of(file);
        }
        catch (IOException) { return None; }
    }

    /// <summary>The foreign entries in a package's bytes (the browser's).</summary>
    public static PackageForeign Of(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes, writable: false);
        return Of(ms);
    }

    static PackageForeign Of(Stream input)
    {
        try
        {
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            // Only a format 3 package: an old project's (format 2) entries are its model, read by LegacyPackage, not another tool's.
            LapinPackage.ReadManifest(zip);
            var parts = new List<ForeignPart>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in zip.Entries)
            {
                // Folders are implied by their entries; a name twice (a zip allows it) is kept once, the first, as a reader finds it.
                if (e.FullName.EndsWith('/') || !Keeps(e.FullName) || !seen.Add(e.FullName)) continue;
                using var s = e.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                parts.Add(new ForeignPart(e.FullName, ms.ToArray(), e.LastWriteTime));
            }
            return parts.Count == 0 ? None : new PackageForeign(parts.OrderBy(p => p.Path, StringComparer.Ordinal).ToList());
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return None; }
    }

    /// <summary>
    /// Whether an entry is kept: a relative path of plain segments whose first is not Lapin's (compared without case, so <c>Model/x</c>
    /// written on a file system that ignores case can never shadow <c>model/</c>); no '\', no empty, '.' or '..' segment.
    /// </summary>
    public static bool Keeps(string path)
    {
        if (path.Length == 0 || path.Contains('\\') || path.Contains('\0')) return false;
        var segments = path.Split('/');
        if (segments.Any(s => s is "" or "." or "..")) return false;
        return !Owned.Contains(segments[0], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The manifest's list: path, hash and size of each, by path.</summary>
    public IReadOnlyList<PackagePart> Manifest => Parts.Select(p => p.Part).ToList();
}
