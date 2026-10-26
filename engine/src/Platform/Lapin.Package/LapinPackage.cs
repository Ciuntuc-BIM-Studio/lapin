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
    IReadOnlyDictionary<string, string> Classes);

public sealed record PackageModule(string Id, int SchemaVersion);

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
/// </code>
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

    /// <summary>The project as the parts a package holds, before anything is written: what a version keeps as its manifest.</summary>
    public static PackageBuild Build(IModelView model, Func<string, string> moduleOf, Func<string, string?> storeyOf,
        string app, string product, IReadOnlyList<PackageModule> modules, JsonObject? document = null)
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

        var manifest = new PackageManifest(Format, Version, app, product, modules, parts.Select(p => p.Part).ToList(), classes);
        return new PackageBuild(manifest, parts);
    }

    /// <summary>A built project written to a file, beside first, then moved over the old one.</summary>
    public static void Write(string path, PackageBuild build, PackageVersions? versions = null)
    {
        var temp = path + ".saving";
        using (var file = File.Create(temp)) Write(file, build, versions);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// A built project written as a package, with its versions: each version's
    /// manifest in the index, and the bytes of every part a version names that
    /// the live project does not hold (by hash) under <c>versions/blobs/</c>,
    /// once however many versions share it.
    /// </summary>
    public static void Write(Stream output, PackageBuild build, PackageVersions? versions = null)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        void Entry(string name, byte[] bytes)
        {
            using var s = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
            s.Write(bytes);
        }
        Entry(ManifestPath, JsonSerializer.SerializeToUtf8Bytes(build.Manifest, PackageJson.Indented.PackageManifest));
        foreach (var (part, bytes) in build.Parts) Entry(part.Path, bytes);
        if (versions is not { Versions.Count: > 0 }) return;
        Entry(VersionsPath, JsonSerializer.SerializeToUtf8Bytes(new PackageVersionIndex(versions.Versions), PackageJson.Indented.PackageVersionIndex));
        var live = build.Parts.Select(p => p.Part.Sha256).ToHashSet();
        var needed = versions.Hashes().Where(h => !live.Contains(h)).ToList();
        foreach (var (sha, bytes) in versions.Blobs(needed)) Entry(BlobsPrefix + sha, bytes);
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
