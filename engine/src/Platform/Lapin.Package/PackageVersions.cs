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

    /// <summary>
    /// Why the file's versions could not be read (null: they were). Its versions are then not lost: a save carries the
    /// file's <c>versions/</c> over byte for byte (<see cref="CopyRawTo"/>), and they cannot be changed until they read again.
    /// </summary>
    public string? Unreadable { get; private init; }

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
            if (index?.Versions is not { } versions || versions.Any(v => v?.Manifest?.Parts == null)) return new PackageVersions([], open) { Unreadable = "its index is empty" };
            return new PackageVersions(versions, open);
        }
        // Never "no versions": a save would then write the file without them (audit 15.11.2026, P6).
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new PackageVersions([], open) { Unreadable = ex.Message };
        }
    }

    /// <summary>The same versions, their parts to be found in another package from now on (the file just written).</summary>
    public PackageVersions From(string path) => new(Versions, () => File.OpenRead(path)) { Unreadable = Unreadable };
    public PackageVersions From(byte[] bytes) => new(Versions, () => new MemoryStream(bytes, writable: false)) { Unreadable = Unreadable };

    public PackageVersion Require(int number) => Versions.FirstOrDefault(v => v.Number == number) ?? throw new InvalidDataException(Unreadable is { } why ? Refusal(why) : $"There is no version {number}.");

    static string Refusal(string why) => $"The project's versions could not be read ({why}): they are kept in its file as they are, and cannot be changed until they read again.";

    void Writable()
    {
        if (Unreadable is { } why) throw new InvalidDataException(Refusal(why));
    }

    /// <summary>
    /// The versions as the file had them, which could not be read (<see cref="Unreadable"/>): every entry under <c>versions/</c>
    /// handed to <paramref name="write"/> byte for byte, and every live part of the file the new one no longer holds
    /// (<paramref name="live"/>: the new live parts' hashes) as a blob, since a version may name it. Gives back why the file
    /// could not be read (nothing written then), or null; a failure of <paramref name="write"/> is thrown on.
    /// </summary>
    internal string? CopyRawTo(IReadOnlySet<string> live, Action<string, byte[]> write)
    {
        if (_source == null) return "the file the versions were in is gone";
        var entries = new List<(string Name, byte[] Bytes)>();
        try
        {
            using var input = _source();
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            var blobs = new HashSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in zip.Entries)
            {
                if (!e.FullName.StartsWith("versions/", StringComparison.Ordinal) || e.FullName.EndsWith('/') || !seen.Add(e.FullName)) continue;
                if (e.FullName.StartsWith(LapinPackage.BlobsPrefix, StringComparison.Ordinal)) blobs.Add(e.FullName[LapinPackage.BlobsPrefix.Length..]);
                entries.Add((e.FullName, LapinPackage.Bytes(zip, e.FullName)));
            }
            foreach (var p in LapinPackage.ReadManifest(zip).Parts)
            {
                if (live.Contains(p.Sha256) || !blobs.Add(p.Sha256)) continue;
                var bytes = LapinPackage.Bytes(zip, p.Path, p.Size);
                if (LapinPackage.Hash(bytes) == p.Sha256) entries.Add((LapinPackage.BlobsPrefix + p.Sha256, bytes));
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            return ex.Message;
        }
        foreach (var (name, bytes) in entries) write(name, bytes);
        return null;
    }

    /// <summary>Every part a version or a revision names, by hash.</summary>
    public IEnumerable<string> Hashes() =>
        Versions.SelectMany(v => v.Manifest.Parts.Select(p => p.Sha256).Concat(v.Revisions.SelectMany(r => r.Manifest.Parts.Select(p => p.Sha256)))).Distinct();

    /// <summary>The bytes of the parts asked for, from the package they live in: a blob, or a live part of the same hash; refused when one is missing or damaged.</summary>
    public IReadOnlyDictionary<string, byte[]> Blobs(IReadOnlyCollection<string> hashes)
    {
        var (found, missing) = TryBlobs(hashes);
        if (missing.Count > 0) throw new InvalidDataException(missing.Values.First());
        return found;
    }

    /// <summary>
    /// The bytes of the parts asked for that can be read and are what their hash says (audit 15.11.2026, P22), and why each
    /// of the others could not be: the file gone, the part not in it, or its bytes damaged.
    /// </summary>
    public (IReadOnlyDictionary<string, byte[]> Found, IReadOnlyDictionary<string, string> Missing) TryBlobs(IReadOnlyCollection<string> hashes)
    {
        var found = new Dictionary<string, byte[]>();
        var missing = new Dictionary<string, string>();
        if (hashes.Count == 0) return (found, missing);
        void AllMissing(string why) { foreach (var sha in hashes) missing.TryAdd(sha, why); }
        if (_source == null) { AllMissing("The file the versions' parts were in is gone."); return (found, missing); }
        Stream input;
        try { input = _source(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AllMissing($"The file the versions' parts were in cannot be read: {ex.Message}"); return (found, missing); }
        using (input)
        {
            try
            {
                using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
                var live = LiveByHash(zip);
                foreach (var sha in hashes)
                {
                    try
                    {
                        byte[] bytes;
                        if (zip.GetEntry(LapinPackage.BlobsPrefix + sha) != null) bytes = LapinPackage.Bytes(zip, LapinPackage.BlobsPrefix + sha);
                        else if (live.TryGetValue(sha, out var path)) bytes = LapinPackage.Bytes(zip, path);
                        else { missing[sha] = $"The file no longer holds a part a version needs ({sha[..12]}…)."; continue; }
                        if (LapinPackage.Hash(bytes) != sha) missing[sha] = $"The project is damaged: a part a version needs ({sha[..12]}…) is not what was saved.";
                        else found[sha] = bytes;
                    }
                    catch (InvalidDataException ex) { missing[sha] = ex.Message; }
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException) { AllMissing($"The file the versions' parts were in cannot be read: {ex.Message}"); }
        }
        return (found, missing);
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
        Writable();
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
        Writable();
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
        Writable();
        Require(number);
        return new PackageVersions(Versions.Where(v => v.Number != number).ToList(), _source);
    }

    static string? Clean(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
