using Lapin.Model;
using System.IO.Compression;

namespace Lapin.Package;

/// <summary>An entry another tool put in the package: its path, its bytes as they were read, when the zip says it was written.</summary>
public sealed record ForeignPart(string Path, byte[] Bytes, DateTimeOffset Modified, string? Module = null)
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
    public static readonly IReadOnlyList<string> Owned = ["manifest.json", "project", "model", "versions", "issues", "journal", "derived", "analytics", "history"];

    /// <summary>
    /// The project's history (docs/spec-lapin-remote.md §6), read with the file's other entries and written back with every save: like
    /// them, what the file holds beside the model. Empty for a project without one (nothing is written then).
    /// </summary>
    public Remote.PackageHistory History { get; private init; } = Remote.PackageHistory.Empty();

    /// <summary>The same entries with another history (a commit, a fetch, a clone).</summary>
    public PackageForeign WithHistory(Remote.PackageHistory history) => new(Parts, ModuleParts) { Unreadable = Unreadable, History = history };

    public IReadOnlyList<ForeignPart> Parts { get; }

    /// <summary>
    /// Lapin's own text parts as the file had them (<see cref="LapinPackage.TextFolders"/>: a project's scripts), each with the
    /// module that wrote it: an edition without that module writes them back unchanged (decision 484), one with it writes its own.
    /// </summary>
    public IReadOnlyList<ForeignPart> ModuleParts { get; }

    PackageForeign(IReadOnlyList<ForeignPart> parts, IReadOnlyList<ForeignPart>? moduleParts = null) { Parts = parts; ModuleParts = moduleParts ?? []; }

    /// <summary>Why the file's entries of other tools could not be read (null: they were, or there are none); a save over that file is then refused (audit 15.11.2026, P6).</summary>
    public string? Unreadable { get; private init; }

    /// <summary>A project with nothing of other tools.</summary>
    public static readonly PackageForeign None = new([]);

    /// <summary>The foreign entries of the package at <paramref name="path"/>; none when it has none, or is not a format 3 package.</summary>
    public static PackageForeign Of(string path)
    {
        if (!File.Exists(path)) return None;
        try
        {
            PackageForeign o;
            using (var file = File.OpenRead(path)) o = Of(file);
            var history = Remote.PackageHistory.Of(path);
            return history.Any || history.Unreadable != null ? o.WithHistory(history) : o;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new PackageForeign([]) { Unreadable = ex.Message }; }
    }

    /// <summary>The foreign entries in a package's bytes (the browser's).</summary>
    public static PackageForeign Of(byte[] bytes)
    {
        PackageForeign o;
        using (var ms = new MemoryStream(bytes, writable: false)) o = Of(ms);
        var history = Remote.PackageHistory.Of(bytes);
        return history.Any || history.Unreadable != null ? o.WithHistory(history) : o;
    }

    static PackageForeign Of(Stream input)
    {
        ZipArchive zip;
        PackageManifest manifest;
        try
        {
            zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            // Only a format 3 package: an old project's (format 2) entries are its model, read by LegacyPackage, not another tool's.
            manifest = LapinPackage.ReadManifest(zip);
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return None; }
        // A format 3 package whose entries cannot be read: never "none", which a save would then write (audit 15.11.2026, P6).
        using var _ = zip;
        try
        {
            // Lapin's own entries are those its manifest lists (a text folder, scripts/, holds them beside other tools' files).
            var own = manifest.Parts.ToDictionary(p => p.Path, p => p.Module, StringComparer.Ordinal);
            var parts = new List<ForeignPart>();
            var moduleParts = new List<ForeignPart>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in zip.Entries)
            {
                // Folders are implied by their entries; a name twice (a zip allows it) is kept once, the first, as a reader finds it.
                if (e.FullName.EndsWith('/') || !seen.Add(e.FullName)) continue;
                var mine = own.TryGetValue(e.FullName, out var module);
                if (mine ? module == null || !LapinPackage.IsTextPath(e.FullName) : !Keeps(e.FullName)) continue;
                byte[] bytes;
                using (var s = e.Open()) bytes = BoundedRead.Exactly(s, e.Length, $"The entry {e.FullName}");
                (mine ? moduleParts : parts).Add(new ForeignPart(e.FullName, bytes, e.LastWriteTime, module));
            }
            return parts.Count == 0 && moduleParts.Count == 0 ? None
                : new PackageForeign(parts.OrderBy(p => p.Path, StringComparer.Ordinal).ToList(), moduleParts.OrderBy(p => p.Path, StringComparer.Ordinal).ToList());
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or System.Text.Json.JsonException) { return new PackageForeign([]) { Unreadable = ex.Message }; }
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
