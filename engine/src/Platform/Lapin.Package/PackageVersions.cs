using System.IO.Compression;
using System.Text.Json;

namespace Lapin.Package;

/// <summary>What a version held before it was updated in place: its manifest then, when it was made, its note.</summary>
public sealed record PackageRevision(string At, string? Note, int Entities, PackageManifest Manifest);

/// <summary>
/// A version of the project, kept in its file: a manifest of its own (the
/// parts as they were, by hash), numbered 1, 2, 3… within the project, with a
/// name, a note and when it was made. Updating a version in place keeps what
/// it held before among its revisions, oldest first.
/// </summary>
public sealed record PackageVersion(int Number, string Name, string? Note, string At, string UpdatedAt, int Entities, PackageManifest Manifest, IReadOnlyList<PackageRevision> Revisions)
{
    /// <summary>The bytes of its parts.</summary>
    public long Size => Manifest.Parts.Sum(p => p.Size);
}

/// <summary>The file's <c>versions/index.json</c>.</summary>
public sealed record PackageVersionIndex(IReadOnlyList<PackageVersion> Versions);

/// <summary>
/// The versions of a project (docs/versions.md), and where the bytes of their
/// parts are: the package they were read from (a file, or the bytes a browser
/// holds), where a part is either still live or kept under
/// <c>versions/blobs/&lt;sha256&gt;</c>. Content-addressed, so a part shared by
/// the live project and any number of versions is in the file once.
/// Immutable: every change gives a new set, written with the next save.
/// </summary>
public sealed class PackageVersions
{
    public IReadOnlyList<PackageVersion> Versions { get; }
    readonly Func<Stream>? _source;

    PackageVersions(IReadOnlyList<PackageVersion> versions, Func<Stream>? source)
    {
        Versions = versions;
        _source = source;
    }

    /// <summary>A project without versions.</summary>
    public static readonly PackageVersions None = new([], null);

    /// <summary>The versions in the package at <paramref name="path"/> (none when it is not a format 3 package, or has none).</summary>
    public static PackageVersions Of(string path) => File.Exists(path) ? Read(() => File.OpenRead(path)) : None;

    /// <summary>The versions in a package's bytes (the browser's).</summary>
    public static PackageVersions Of(byte[] bytes) => Read(() => new MemoryStream(bytes, writable: false));

    static PackageVersions Read(Func<Stream> open)
    {
        try
        {
            using var input = open();
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.GetEntry("manifest.json") == null) return None;
            if (zip.GetEntry(LapinPackage.VersionsPath) is not { } entry) return new PackageVersions([], open);
            using var s = entry.Open();
            var index = JsonSerializer.Deserialize(s, PackageJson.Default.PackageVersionIndex);
            return new PackageVersions(index?.Versions ?? [], open);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException)
        {
            return None;
        }
    }

    /// <summary>The same versions, their parts to be found in another package from now on (the file just written).</summary>
    public PackageVersions From(string path) => new(Versions, () => File.OpenRead(path));
    public PackageVersions From(byte[] bytes) => new(Versions, () => new MemoryStream(bytes, writable: false));

    public PackageVersion Require(int number) => Versions.FirstOrDefault(v => v.Number == number) ?? throw new InvalidDataException($"There is no version {number}.");

    /// <summary>Every part a version or a revision names, by hash.</summary>
    public IEnumerable<string> Hashes() =>
        Versions.SelectMany(v => v.Manifest.Parts.Select(p => p.Sha256).Concat(v.Revisions.SelectMany(r => r.Manifest.Parts.Select(p => p.Sha256)))).Distinct();

    /// <summary>The bytes of the parts asked for, from the package they live in: a blob, or a live part of the same hash.</summary>
    public IReadOnlyDictionary<string, byte[]> Blobs(IReadOnlyCollection<string> hashes)
    {
        var found = new Dictionary<string, byte[]>();
        if (hashes.Count == 0) return found;
        if (_source == null) throw new InvalidDataException("The file the versions' parts were in is gone.");
        using var input = _source();
        using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var live = LiveByHash(zip);
        foreach (var sha in hashes)
        {
            if (zip.GetEntry(LapinPackage.BlobsPrefix + sha) != null) found[sha] = LapinPackage.Bytes(zip, LapinPackage.BlobsPrefix + sha);
            else if (live.TryGetValue(sha, out var path)) found[sha] = LapinPackage.Bytes(zip, path);
            else throw new InvalidDataException($"The file no longer holds a part a version needs ({sha[..12]}…).");
        }
        return found;
    }

    static Dictionary<string, string> LiveByHash(ZipArchive zip)
    {
        var live = new Dictionary<string, string>();
        foreach (var p in LapinPackage.ReadManifest(zip).Parts) live.TryAdd(p.Sha256, p.Path);
        return live;
    }

    /// <summary>The project as a version (or a revision) held it.</summary>
    public PackageContent Content(PackageManifest manifest)
    {
        var bytes = Blobs(manifest.Parts.Select(p => p.Sha256).Distinct().ToList());
        return LapinPackage.Content(manifest, p => bytes[p.Sha256]);
    }

    /// <summary>A package holding that version alone, to open on its own (its own versions left out).</summary>
    public byte[] Standalone(PackageManifest manifest)
    {
        var bytes = Blobs(manifest.Parts.Select(p => p.Sha256).Distinct().ToList());
        using var ms = new MemoryStream();
        LapinPackage.Write(ms, new PackageBuild(manifest, manifest.Parts.Select(p => (p, bytes[p.Sha256])).ToList()));
        return ms.ToArray();
    }

    /// <summary>When, in UTC, ISO 8601 ("2026-10-26T08:28:11Z"): the interface shows it in the user's time.</summary>
    static string Stamp(DateTime? now) => (now ?? DateTime.UtcNow).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    /// <summary>The number the next version takes (an issue records it before the version is saved, so the two are written together).</summary>
    public int NextNumber => Versions.Count == 0 ? 1 : Versions.Max(v => v.Number) + 1;

    /// <summary>A new version, numbered after the last, named "Version n" when no name is given.</summary>
    public PackageVersions Add(PackageManifest manifest, int entities, string? name, string? note, out PackageVersion version, DateTime? now = null)
    {
        var number = NextNumber;
        var at = Stamp(now);
        version = new PackageVersion(number, string.IsNullOrWhiteSpace(name) ? $"Version {number}" : name.Trim(), Clean(note), at, at, entities, manifest, []);
        return new PackageVersions([.. Versions, version], _source);
    }

    /// <summary>
    /// A version renamed or annotated; with <paramref name="replace"/>, the
    /// project as it is now takes its place, what it held kept as a revision.
    /// </summary>
    public PackageVersions Update(int number, string? name, string? note, (PackageManifest Manifest, int Entities)? replace, out PackageVersion version, DateTime? now = null)
    {
        var old = Require(number);
        version = old with
        {
            Name = string.IsNullOrWhiteSpace(name) ? old.Name : name.Trim(),
            Note = note == null ? old.Note : Clean(note),
        };
        if (replace is { } r)
            version = version with
            {
                Manifest = r.Manifest, Entities = r.Entities, UpdatedAt = Stamp(now),
                Revisions = [.. old.Revisions, new PackageRevision(old.UpdatedAt, old.Note, old.Entities, old.Manifest)],
            };
        var v = version;
        return new PackageVersions(Versions.Select(x => x.Number == number ? v : x).ToList(), _source);
    }

    /// <summary>The version gone; the parts only it named go with the next save.</summary>
    public PackageVersions Remove(int number)
    {
        Require(number);
        return new PackageVersions(Versions.Where(v => v.Number != number).ToList(), _source);
    }

    static string? Clean(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
