using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Package;

/// <summary>A part of the package: its path, and what its bytes hash to (SHA-256, hex).</summary>
public sealed record PackagePart(string Path, string Sha256, long Size, string? Module = null);

public sealed record PackageManifest(
    string Format,
    int Version,
    string App,
    string Product,
    IReadOnlyList<PackageModule> Modules,
    IReadOnlyList<PackagePart> Parts,
    /// <summary>Which module wrote each class: what a product without that module keeps, untouched.</summary>
    IReadOnlyDictionary<string, string> Classes,
    /// <summary>The entries of other tools kept with the project (<see cref="PackageForeign"/>); left out when there are none, so an older Lapin reads the manifest as before.</summary>
    IReadOnlyList<PackagePart>? Foreign = null);

public sealed record PackageModule(string Id, int SchemaVersion);

/// <summary>A text a module writes into the package beside the model (<see cref="LapinPackage.TextFolders"/>): its path, its text (UTF-8), the module.</summary>
public sealed record ModuleText(string Path, string Text, string Module);

/// <summary>A project as read from a package.</summary>
public sealed record PackageContent(IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations, IReadOnlyDictionary<string, string> Classes, JsonObject Document, PackageManifest Manifest);

/// <summary>A project made into its parts, before it is written: the manifest and every part's bytes.</summary>
public sealed record PackageBuild(PackageManifest Manifest, IReadOnlyList<(PackagePart Part, byte[] Bytes)> Parts);

/// <summary>
/// The .lapin package, format 3 (docs/format-lapin.md):
/// <code>
/// manifest.json                          format, version, modules, every part with its SHA-256
/// project/document.json                  the project document (views, sheets, standards: later)
/// model/&lt;module&gt;/&lt;class&gt;/&lt;storey&gt;.arrow   the entities of one class on one storey ("_": on none)
/// model/relations.arrow                  every relationship
/// versions/index.json                    the project's versions: each a manifest of its own (docs/versions.md)
/// versions/blobs/&lt;sha256&gt;               a version's part the live project no longer has
/// issues/index.json                      the project's issues and their files, for whoever opens the zip (the record is the model's)
/// issues/blobs/&lt;sha256&gt;                 a file an issue made, kept in the project (docs/plan-cde.md), once however many issues share it
/// scripts/&lt;name&gt;.py                      a project's script as text (the scripting module's, decision 489; its LapinScript entity is what Lapin reads)
/// &lt;anything else&gt;                        another tool's entries (dag/, source/…, a file of its own in scripts/), kept byte for byte (<see cref="PackageForeign"/>)
/// </code>
/// A package without <c>issues/</c> (older, or a project never issued) reads as before; an older Lapin skips the folder it does not know.
/// Writing goes to a temporary file first, then replaces the target: a crash
/// while saving never leaves half a project.
/// </summary>
public static class LapinPackage
{
    public const string Format = "lapin-project";
    public const int Version = 3;
    public const string Suffix = ".lapin";
    const string ManifestPath = "manifest.json";
    const string DocumentPath = "project/document.json";
    const string RelationsPath = "model/relations.arrow";
    internal const string VersionsPath = "versions/index.json";
    internal const string BlobsPrefix = "versions/blobs/";

    /// <param name="moduleOf">The module that owns a class ("core" for the platform's).</param>
    /// <param name="storeyOf">The storey an entity stands on, or null.</param>
    /// <param name="versions">The project's versions, carried over into the file (none: a project without versions).</param>
    public static void Write(string path, IModelView model, Func<string, string> moduleOf, Func<string, string?> storeyOf,
        string app, string product, IReadOnlyList<PackageModule> modules, JsonObject? document = null, PackageVersions? versions = null)
    {
        // Written beside, then moved over the old file: a failed save leaves the project as it was.
        var temp = path + ".saving";
        using (var file = File.Create(temp)) Write(file, model, moduleOf, storeyOf, app, product, modules, document, versions);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>The package into a stream (a browser hands the bytes to the user to keep).</summary>
    public static void Write(Stream output, IModelView model, Func<string, string> moduleOf, Func<string, string?> storeyOf,
        string app, string product, IReadOnlyList<PackageModule> modules, JsonObject? document = null, PackageVersions? versions = null) =>
        Write(output, Build(model, moduleOf, storeyOf, app, product, modules, document), versions);

    /// <summary>
    /// The top-level folders a module writes text into (<see cref="ModuleText"/>): copies of what its entities hold, for
    /// whoever opens the zip without Lapin (a project's scripts as <c>scripts/&lt;name&gt;.py</c>, decision 489; its graphs as <c>graphs/&lt;name&gt;.json</c>, decision 525). The model's
    /// Arrow tables stay what Lapin reads; another tool's entries in the same folder are kept beside them (<see cref="PackageForeign"/>).
    /// </summary>
    public static readonly IReadOnlyList<string> TextFolders = ["scripts", "graphs"];

    /// <summary>Whether a module may write a text part at <paramref name="path"/>: one plain file name in one of <see cref="TextFolders"/>.</summary>
    public static bool IsTextPath(string path)
    {
        var slash = path.IndexOf('/');
        if (slash <= 0 || !TextFolders.Contains(path[..slash], StringComparer.Ordinal)) return false;
        var name = path[(slash + 1)..];
        return name.Length is > 0 and <= 200 && name.IndexOfAny(['/', '\\', ':', '\0']) < 0 && name is not ("." or "..") && !name.StartsWith('.');
    }

    /// <summary>The project as the parts a package holds, before anything is written: what a version keeps as its manifest.</summary>
    /// <param name="texts">The modules' text parts (<see cref="ModuleText"/>), written after the model; a path outside <see cref="TextFolders"/>, or twice, is refused.</param>
    public static PackageBuild Build(IModelView model, Func<string, string> moduleOf, Func<string, string?> storeyOf,
        string app, string product, IReadOnlyList<PackageModule> modules, JsonObject? document = null, IEnumerable<ModuleText>? texts = null)
    {
        var parts = new List<(PackagePart Part, byte[] Bytes)>();
        void Add(string p, byte[] bytes, string? module = null) => parts.Add((new PackagePart(p, Hash(bytes), bytes.Length, module), bytes));

        Add(DocumentPath, JsonSerializer.SerializeToUtf8Bytes(document ?? [], PackageJson.Default.JsonObject));
        var classes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var chunks = model.Entities
            .GroupBy(e => (Module: moduleOf(e.Class), e.Class, Storey: storeyOf(e.Id) ?? "_"))
            .OrderBy(g => g.Key.Module, StringComparer.Ordinal).ThenBy(g => g.Key.Class, StringComparer.Ordinal).ThenBy(g => g.Key.Storey, StringComparer.Ordinal);
        foreach (var chunk in chunks)
        {
            classes[chunk.Key.Class] = chunk.Key.Module;
            Add($"model/{Safe(chunk.Key.Module)}/{Safe(chunk.Key.Class)}/{Safe(chunk.Key.Storey)}.arrow", ArrowTables.WriteEntities(chunk.ToList(), model.Order), chunk.Key.Module);
        }
        Add(RelationsPath, ArrowTables.WriteRelations(model.Relations.ToList(), model.Order));
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in (texts ?? []).OrderBy(t => t.Path, StringComparer.Ordinal))
        {
            if (!IsTextPath(t.Path)) throw new InvalidOperationException($"„{t.Module}” writes {t.Path}, outside the text folders.");
            // Compared without case: two scripts whose names differ only by case would be one file on most disks.
            if (!written.Add(t.Path)) throw new InvalidOperationException($"{t.Path} is written twice.");
            Add(t.Path, System.Text.Encoding.UTF8.GetBytes(t.Text), t.Module);
        }

        var manifest = new PackageManifest(Format, Version, app, product, modules, parts.Select(p => p.Part).ToList(), classes);
        return new PackageBuild(manifest, parts);
    }

    /// <summary>A built project written to a file, beside first, then moved over the old one.</summary>
    public static void Write(string path, PackageBuild build, PackageVersions? versions = null, IssueFiles? issues = null, PackageForeign? foreign = null)
    {
        var temp = path + ".saving";
        using (var file = File.Create(temp)) Write(file, build, versions, issues, foreign);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// A built project written as a package, with its versions: each version's
    /// manifest in the index, and the bytes of every part a version names that
    /// the live project does not hold (by hash) under <c>versions/blobs/</c>,
    /// once however many versions share it; the files the project's issues
    /// keep, under <c>issues/blobs/</c>, once each; and the entries of other
    /// tools, as they were read, listed in the manifest's <c>foreign</c>.
    /// </summary>
    public static void Write(Stream output, PackageBuild build, PackageVersions? versions = null, IssueFiles? issues = null, PackageForeign? foreign = null)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        void Entry(string name, byte[] bytes)
        {
            using var s = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
            s.Write(bytes);
        }
        // The text parts of a module this edition lacks (decision 484), as the file had them: written back with the model it keeps.
        var mine = build.Parts.Select(p => p.Part.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var loaded = build.Manifest.Modules.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        var absent = (foreign?.ModuleParts ?? []).Where(p => p.Module is { } m && !loaded.Contains(m) && IsTextPath(p.Path) && mine.Add(p.Path)).ToList();
        var allParts = absent.Count == 0 ? build.Parts : [.. build.Parts, .. absent.Select(p => (p.Part with { Module = p.Module }, p.Bytes))];
        // Another tool's entry where Lapin writes its own (a script of the same name in scripts/): Lapin's is the project's.
        var kept = foreign?.Parts.Where(p => PackageForeign.Keeps(p.Path) && !mine.Contains(p.Path)).ToList() ?? [];
        var manifest = build.Manifest with { Parts = allParts.Select(p => p.Part).ToList(), Foreign = kept.Count > 0 ? kept.Select(p => p.Part).ToList() : null };
        Entry(ManifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, PackageJson.Indented.PackageManifest));
        foreach (var (part, bytes) in allParts) Entry(part.Path, bytes);
        if (versions is { Versions.Count: > 0 })
        {
            Entry(VersionsPath, JsonSerializer.SerializeToUtf8Bytes(new PackageVersionIndex(versions.Versions), PackageJson.Indented.PackageVersionIndex));
            var live = build.Parts.Select(p => p.Part.Sha256).ToHashSet();
            var needed = versions.Hashes().Where(h => !live.Contains(h)).ToList();
            foreach (var (sha, bytes) in versions.Blobs(needed)) Entry(BlobsPrefix + sha, bytes);
        }
        if (issues is { Keep.Count: > 0 })
        {
            if (issues.Index is { } index) Entry(PackageIssues.IndexPath, index);
            // Compressed quickly: a PDF is compressed already, an IFC (text) shrinks well either way, and a save with many issues stays fast.
            issues.Store.CopyTo(issues.Written, (sha, bytes) =>
            {
                using var s = zip.CreateEntry(PackageIssues.Prefix + sha, CompressionLevel.Fastest).Open();
                bytes.CopyTo(s);
            });
        }
        // Last, after everything Lapin owns: none of these can shadow a name of Lapin's (PackageForeign.Keeps).
        foreach (var part in kept)
        {
            var entry = zip.CreateEntry(part.Path, CompressionLevel.Optimal);
            entry.LastWriteTime = part.Modified;
            using var s = entry.Open();
            s.Write(part.Bytes);
        }
    }

    public static PackageContent Read(string path)
    {
        using var file = File.OpenRead(path);
        return Read(file);
    }

    /// <summary>A package read from a stream (bytes a browser was given).</summary>
    public static PackageContent Read(Stream input)
    {
        using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var manifest = ReadManifest(zip);
        return Content(manifest, part => Bytes(zip, part.Path));
    }

    internal static byte[] Bytes(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"The project has no {name}.");
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    internal static PackageManifest ReadManifest(ZipArchive zip)
    {
        var manifest = JsonSerializer.Deserialize(Bytes(zip, ManifestPath), PackageJson.Default.PackageManifest)
                       ?? throw new InvalidDataException("The project's manifest is unreadable.");
        if (manifest.Format != Format) throw new InvalidDataException($"Not a Lapin project ({manifest.Format}).");
        if (manifest.Version > Version) throw new InvalidDataException($"The project was saved by a newer Lapin (format {manifest.Version}); update Lapin to open it.");
        if (manifest.Version < Version) throw new InvalidDataException($"Projects of format {manifest.Version} (the old Lapin) open from milestone M6 on.");
        return manifest;
    }

    /// <summary>The project a manifest describes, its parts' bytes from <paramref name="bytesOf"/> (the live parts, or a version's blobs).</summary>
    internal static PackageContent Content(PackageManifest manifest, Func<PackagePart, byte[]> bytesOf)
    {
        var entities = new List<(Entity Entity, int Order)>();
        var relations = new List<(Relation Relation, int Order)>();
        JsonObject document = [];
        foreach (var part in manifest.Parts)
        {
            var bytes = bytesOf(part);
            if (bytes.Length != part.Size || Hash(bytes) != part.Sha256) throw new InvalidDataException($"The project is damaged: {part.Path} is not what was saved.");
            if (part.Path == DocumentPath) document = JsonNode.Parse(bytes) as JsonObject ?? [];
            else if (part.Path == RelationsPath) relations.AddRange(ArrowTables.ReadRelations(bytes));
            else if (part.Path.StartsWith("model/", StringComparison.Ordinal) && part.Path.EndsWith(".arrow", StringComparison.Ordinal)) entities.AddRange(ArrowTables.ReadEntities(bytes));
        }
        // Creation order survives the chunks: every row carries its place.
        return new PackageContent(
            entities.OrderBy(x => x.Order).Select(x => x.Entity).ToList(),
            relations.OrderBy(x => x.Order).Select(x => x.Relation).ToList(),
            manifest.Classes, document, manifest);
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>A path segment: GlobalIds may hold '$', which zip and file systems keep, but never '/'.</summary>
    static string Safe(string s) => s.Replace('/', '_').Replace('\\', '_');
}

/// <summary>The package's JSON contracts, generated at build time with the engine's options (web names, numbers read from strings, nulls left out).</summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(JsonSerializerDefaults.Web, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
[System.Text.Json.Serialization.JsonSerializable(typeof(PackageManifest))]
[System.Text.Json.Serialization.JsonSerializable(typeof(PackageVersionIndex))]
[System.Text.Json.Serialization.JsonSerializable(typeof(JsonObject))]
public partial class PackageJson : System.Text.Json.Serialization.JsonSerializerContext
{
    /// <summary>The manifest as people read it: indented.</summary>
    public static PackageJson Indented { get; } = new(new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    });
}
