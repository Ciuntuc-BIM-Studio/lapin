using System.IO.Compression;
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

/// <summary>
/// A module the project was written with: its id and the schema of its part; <see cref="Extension"/> the extension it came
/// in (an extension module from Lapin Extensions, docs/plan-extension-modules.md §4.4), none for a module of the edition;
/// <see cref="Absent"/> true when the edition that wrote the file did not have it and carried the record over from the file
/// it read (the project still holds its content). Both left out otherwise, so a manifest reads and writes as before.
/// </summary>
public sealed record PackageModule(string Id, int SchemaVersion, PackageExtension? Extension = null, bool? Absent = null);

/// <summary>An extension a module came in: its catalogue id and version.</summary>
public sealed record PackageExtension(string Id, string Version);

/// <summary>A text a module writes into the package beside the model (<see cref="LapinPackage.TextFolders"/>): its path, its text (UTF-8), the module.</summary>
public sealed record ModuleText(string Path, string Text, string Module);

/// <summary>A project as read from a package.</summary>
public sealed record PackageContent(IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations, IReadOnlyDictionary<string, string> Classes, JsonObject Document, PackageManifest Manifest)
{
    /// <summary>The extension modules the file was written with (loaded then, or carried over), by module id.</summary>
    public IReadOnlyDictionary<string, Lapin.Model.FileExtension> Extensions =>
        (Manifest.Modules ?? []).Where(m => m.Extension is { Id.Length: > 0 }).GroupBy(m => m.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => new Lapin.Model.FileExtension(g.Key, g.First().SchemaVersion, g.First().Extension!.Id, g.First().Extension!.Version ?? ""), StringComparer.Ordinal);
}

/// <summary>
/// What a save could not carry over from the package the project was read from (a version's part or an issued file, gone or
/// damaged), said to the user; what of the issues' files it wrote; and, when something the file holds could not be read at
/// all, why writing over an existing file is refused (<see cref="Refuse"/>).
/// </summary>
public sealed class PackageWriteReport
{
    public List<string> Warnings { get; } = [];
    /// <summary>The issued files written, by hash, with their size: those the new package holds.</summary>
    public Dictionary<string, long> Issues { get; } = new(StringComparer.Ordinal);
    /// <summary>The reference documents' files written (<c>references/blobs/</c>), by hash, with their size.</summary>
    public Dictionary<string, long> References { get; } = new(StringComparer.Ordinal);
    /// <summary>Why the package must not replace an existing file (it would lose what could not be read); null: it may.</summary>
    public string? Refuse { get; set; }
}

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
/// references/index.json                  the project's reference documents and their files, for whoever opens the zip (docs/plan-cde-references.md)
/// references/blobs/&lt;sha256&gt;             a reference document's file (a certificate, a permit), once however many documents name it
/// scripts/&lt;name&gt;.py                      a project's script as text (the scripting module's, decision 489; its LapinScript entity is what Lapin reads)
/// &lt;anything else&gt;                        another tool's entries (dag/, source/…, a file of its own in scripts/), kept byte for byte (<see cref="PackageForeign"/>)
/// </code>
/// A package without <c>issues/</c> or <c>references/</c> (older, or a project never issued) reads as before; an older Lapin skips the folders it does not know.
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
        Replace(path, file => Write(file, model, moduleOf, storeyOf, app, product, modules, document, versions));
    }

    /// <summary>
    /// A file written beside <paramref name="path"/> (<c>.saving</c>), on the disk before it replaces the old one (audit
    /// 15.11.2026, P23: a power cut right after a save never leaves an empty file), and taken away when the write fails.
    /// A report that refuses (<see cref="PackageWriteReport.Refuse"/>) leaves an existing file as it was.
    /// </summary>
    static PackageWriteReport Replace(string path, Func<FileStream, PackageWriteReport> write)
    {
        var temp = path + ".saving";
        try
        {
            PackageWriteReport report;
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                report = write(file);
                file.Flush(flushToDisk: true);
            }
            if (report.Refuse is { } why && File.Exists(path)) throw new InvalidDataException(why);
            File.Move(temp, path, overwrite: true);
            return report;
        }
        catch
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    /// <summary>The package into a stream (a browser hands the bytes to the user to keep).</summary>
    public static PackageWriteReport Write(Stream output, IModelView model, Func<string, string> moduleOf, Func<string, string?> storeyOf,
        string app, string product, IReadOnlyList<PackageModule> modules, JsonObject? document = null, PackageVersions? versions = null) =>
        Write(output, Build(model, moduleOf, storeyOf, app, product, modules, document), versions);

    /// <summary>
    /// The top-level folders a module writes text into (<see cref="ModuleText"/>): copies of what its entities hold, for
    /// whoever opens the zip without Lapin (a project's scripts as <c>scripts/&lt;name&gt;.py</c>, decision 489; its graphs as <c>graphs/&lt;name&gt;.json</c>, decision 527). The model's
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
        // Each row's place as its rank among the entities (and, apart, among the relationships) in creation order, not the model's
        // running counter: the same project written again after it is opened gives the same bytes (the counter starts again at a load),
        // so a commit's tree (docs/spec-lapin-remote.md) and a version's parts stay the same until the project changes.
        var entityRank = Ranks(model.Entities.Select(e => e.Id), model.Order);
        var relationRank = Ranks(model.Relations.Select(r => r.Id), model.Order);
        var chunks = model.Entities
            .GroupBy(e => (Module: moduleOf(e.Class), e.Class, Storey: storeyOf(e.Id) ?? "_"))
            .OrderBy(g => g.Key.Module, StringComparer.Ordinal).ThenBy(g => g.Key.Class, StringComparer.Ordinal).ThenBy(g => g.Key.Storey, StringComparer.Ordinal);
        foreach (var chunk in chunks)
        {
            classes[chunk.Key.Class] = chunk.Key.Module;
            Add($"model/{Safe(chunk.Key.Module)}/{Safe(chunk.Key.Class)}/{Safe(chunk.Key.Storey)}.arrow", ArrowTables.WriteEntities(chunk.ToList(), id => entityRank[id]), chunk.Key.Module);
        }
        Add(RelationsPath, ArrowTables.WriteRelations(model.Relations.ToList(), id => relationRank[id]));
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

    static Dictionary<string, int> Ranks(IEnumerable<string> ids, Func<string, int> order)
    {
        var o = new Dictionary<string, int>(StringComparer.Ordinal);
        var i = 0;
        foreach (var id in ids.OrderBy(order).ThenBy(x => x, StringComparer.Ordinal)) o.TryAdd(id, i++);
        return o;
    }

    /// <summary>A built project written to a file, beside first, then moved over the old one.</summary>
    public static PackageWriteReport Write(string path, PackageBuild build, PackageVersions? versions = null, IssueFiles? issues = null, PackageForeign? foreign = null) =>
        Replace(path, file => Write(file, build, versions, issues, foreign));

    /// <summary>
    /// A built project written as a package, with its versions: each version's
    /// manifest in the index, and the bytes of every part a version names that
    /// the live project does not hold (by hash) under <c>versions/blobs/</c>,
    /// once however many versions share it; the files the project's issues
    /// keep, under <c>issues/blobs/</c>, once each; and the entries of other
    /// tools, as they were read, listed in the manifest's <c>foreign</c>.
    /// </summary>
    public static PackageWriteReport Write(Stream output, PackageBuild build, PackageVersions? versions = null, IssueFiles? issues = null, PackageForeign? foreign = null)
    {
        var report = new PackageWriteReport();
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        // The blobs written under versions/blobs/ and issues/blobs/: the history writes none of them again (§6.4 of docs/spec-lapin-remote.md).
        var elsewhere = new HashSet<string>(StringComparer.Ordinal);
        void Entry(string name, byte[] bytes)
        {
            if (name.StartsWith(BlobsPrefix, StringComparison.Ordinal)) elsewhere.Add(name[BlobsPrefix.Length..]);
            using var s = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
            s.Write(bytes);
        }
        // The text parts of a module this edition lacks (decision 484), as the file had them: written back with the model it keeps.
        var mine = build.Parts.Select(p => p.Part.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var loaded = build.Manifest.Modules.Where(m => m.Absent != true).Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        var absent = (foreign?.ModuleParts ?? []).Where(p => p.Module is { } m && !loaded.Contains(m) && IsTextPath(p.Path) && mine.Add(p.Path)).ToList();
        var allParts = absent.Count == 0 ? build.Parts : [.. build.Parts, .. absent.Select(p => (p.Part with { Module = p.Module }, p.Bytes))];
        // Another tool's entry where Lapin writes its own (a script of the same name in scripts/): Lapin's is the project's.
        var kept = foreign?.Parts.Where(p => PackageForeign.Keeps(p.Path) && !mine.Contains(p.Path)).ToList() ?? [];
        var manifest = build.Manifest with { Parts = allParts.Select(p => p.Part).ToList(), Foreign = kept.Count > 0 ? kept.Select(p => p.Part).ToList() : null };
        Entry(ManifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, PackageJson.Indented.PackageManifest));
        foreach (var (part, bytes) in allParts) Entry(part.Path, bytes);
        if (versions is { Unreadable: { } unreadable })
        {
            // Versions that could not be read are carried over as the file had them (audit 15.11.2026, P6), never dropped.
            var live = build.Parts.Select(p => p.Part.Sha256).ToHashSet();
            if (versions.CopyRawTo(live, Entry) is { } why)
            {
                report.Refuse ??= $"The project's versions could not be read ({unreadable}) nor carried over ({why}): saving over its file would lose them. Save it under another name.";
                report.Warnings.Add($"The project's versions could not be read ({unreadable}) nor carried over ({why}): this file was written without them.");
            }
            else report.Warnings.Add($"The project's versions could not be read ({unreadable}): they were kept in the file as they were.");
        }
        else if (versions is { Versions.Count: > 0 })
        {
            Entry(VersionsPath, JsonSerializer.SerializeToUtf8Bytes(new PackageVersionIndex(versions.Versions), PackageJson.Indented.PackageVersionIndex));
            var live = build.Parts.Select(p => p.Part.Sha256).ToHashSet();
            var needed = versions.Hashes().Where(h => !live.Contains(h)).ToList();
            // A part that cannot be read, or is not what its hash says, is left out and said (audit 15.11.2026, G1 and P22): the save goes on.
            var (found, missing) = versions.TryBlobs(needed);
            foreach (var (sha, bytes) in found) Entry(BlobsPrefix + sha, bytes);
            if (missing.Count > 0)
            {
                var hit = versions.Versions.Where(v => v.Manifest.Parts.Any(p => missing.ContainsKey(p.Sha256)) || v.Revisions.Any(r => r.Manifest.Parts.Any(p => missing.ContainsKey(p.Sha256)))).Select(v => v.Number);
                report.Warnings.Add($"{missing.Count} part(s) of version {string.Join(", ", hit)} could not be carried over ({missing.Values.First()}): those versions can no longer be restored.");
            }
        }
        void Blobs(IssueFiles files, Dictionary<string, long> into)
        {
            if (files.Index is { } index) Entry(files.Store.IndexName, index);
            // Compressed quickly: a PDF is compressed already, an IFC (text) shrinks well either way, and a save with many issues stays fast.
            var written = files.Store.CopyTo(files.Written, (sha, bytes) =>
            {
                elsewhere.Add(sha);
                using var s = zip.CreateEntry(files.Store.BlobPrefix + sha, CompressionLevel.Fastest).Open();
                bytes.CopyTo(s);
            }, report);
            foreach (var (sha, size) in written) into[sha] = size;
        }
        if (issues is { Keep.Count: > 0 }) Blobs(issues, report.Issues);
        // The reference documents (docs/plan-cde-references.md §3.5): their index whenever the project has any, the files their records hold.
        if (issues?.References is { } references && (references.Keep.Count > 0 || references.Index != null)) Blobs(references, report.References);
        // The project's history (docs/spec-lapin-remote.md §6): its commits, trees and the blobs no other place in the file holds.
        if (foreign?.History is { } history)
        {
            var live = allParts.Select(p => p.Part.Sha256).ToHashSet(StringComparer.Ordinal);
            if (history.Unreadable is { } historyWhy)
            {
                if (history.CopyRawTo(Entry) is { } copyWhy)
                {
                    report.Refuse ??= $"The project's history could not be read ({historyWhy}) nor carried over ({copyWhy}): saving over its file would lose it. Save it under another name.";
                    report.Warnings.Add($"The project's history could not be read ({historyWhy}) nor carried over ({copyWhy}): this file was written without it.");
                }
                else report.Warnings.Add($"The project's history could not be read ({historyWhy}): it was kept in the file as it was.");
            }
            else history.WriteTo(Entry, live, elsewhere);
        }
        if (foreign is { Unreadable: { } foreignWhy })
        {
            report.Refuse ??= $"Other tools' entries in the project's file could not be read ({foreignWhy}): saving over it would lose them. Save it under another name.";
            report.Warnings.Add($"Other tools' entries in the project's file could not be read ({foreignWhy}): this file was written without them.");
        }
        // Last, after everything Lapin owns: none of these can shadow a name of Lapin's (PackageForeign.Keeps).
        foreach (var part in kept)
        {
            var entry = zip.CreateEntry(part.Path, CompressionLevel.Optimal);
            entry.LastWriteTime = part.Modified;
            using var s = entry.Open();
            s.Write(part.Bytes);
        }
        return report;
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
        return Content(manifest, part => Bytes(zip, part.Path, part.Size));
    }

    /// <summary>
    /// An entry's bytes, never more than its declared size (audit 22.11.2026, G19: a crafted entry inflating past it was a
    /// zip bomb); with <paramref name="size"/> (the manifest's), an entry of another size is refused before it is opened.
    /// </summary>
    internal static byte[] Bytes(ZipArchive zip, string name, long? size = null)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"The project has no {name}.");
        if (size is { } expected && entry.Length != expected) throw new InvalidDataException($"The project is damaged: {name} is not what was saved.");
        using var s = entry.Open();
        return BoundedRead.Exactly(s, entry.Length, $"The project's {name}");
    }

    internal static PackageManifest ReadManifest(ZipArchive zip)
    {
        var manifest = JsonSerializer.Deserialize(Bytes(zip, ManifestPath), PackageJson.Default.PackageManifest)
                       ?? throw new InvalidDataException("The project's manifest is unreadable.");
        if (manifest.Format != Format) throw new InvalidDataException($"Not a Lapin project ({manifest.Format}).");
        if (manifest.Version > Version) throw new InvalidDataException($"The project was saved by a newer Lapin (format {manifest.Version}); update Lapin to open it.");
        if (manifest.Version < Version) throw new InvalidDataException($"Projects of format {manifest.Version} (the old Lapin) open from milestone M6 on.");
        if (manifest.Parts is null) throw new InvalidDataException("The project is damaged: its manifest lists no parts.");
        // A part listed twice (audit 22.11.2026: an internal error where its path was looked up) is a damaged manifest.
        if (manifest.Parts.GroupBy(p => p.Path, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1) is { } twice)
            throw new InvalidDataException($"The project is damaged: its manifest lists {twice.Key} twice.");
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

    internal static string Hash(byte[] bytes) => Hashes.Sha256Hex(bytes);

    /// <summary>
    /// A path segment: GlobalIds may hold '$', which zip and file systems keep, but never '/'. Escaped one to one (audit
    /// 22.11.2026: mapping both slashes to '_' made storeys „a/b” and „a_b” one entry, and the project unreadable): '%', '/'
    /// and the backslash as %25, %2F and %5C, every other character as it is, so the names of the files written before stay.
    /// </summary>
    static string Safe(string s) => s.IndexOfAny(['%', '/', '\\']) < 0 ? s : s.Replace("%", "%25").Replace("/", "%2F").Replace("\\", "%5C");
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
