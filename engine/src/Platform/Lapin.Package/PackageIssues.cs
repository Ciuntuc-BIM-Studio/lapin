using System.IO.Compression;

namespace Lapin.Package;

/// <summary>
/// The deliverables of the project's issues kept in its file (docs/plan-cde.md,
/// decision 185): every file an issue made (the sheets' PDFs, the set, the
/// transmittal, its CSV, the manifest, the DXFs, the IFC when asked) under
/// <c>issues/blobs/&lt;sha256&gt;</c>, content-addressed like the versions'
/// blobs, so identical bytes are in the file once however many issues share
/// them. Which file is which (its name, its issue) is the issue's record in the
/// model (<c>LapinIssue</c>); <c>issues/index.json</c> repeats it for whoever
/// opens the zip without Lapin. The bytes are found where they are: made by an
/// issue since the last save (held in memory), else in the package the project
/// was read from or last written to (a file, or the bytes a browser holds).
/// </summary>
public sealed class PackageIssues
{
    public const string Prefix = "issues/blobs/";
    public const string IndexPath = "issues/index.json";

    readonly Func<Stream>? _source;
    readonly Dictionary<string, long> _inSource;
    readonly Dictionary<string, byte[]> _pending;

    PackageIssues(Func<Stream>? source, Dictionary<string, long> inSource, Dictionary<string, byte[]> pending)
    {
        _source = source;
        _inSource = inSource;
        _pending = pending;
    }

    /// <summary>A store with nothing in it (a new project).</summary>
    public static PackageIssues Empty() => new(null, [], []);

    /// <summary>The deliverables in the package at <paramref name="path"/>; empty when it has none, or is not a format 3 package.</summary>
    public static PackageIssues Of(string path) => File.Exists(path) ? Read(() => File.OpenRead(path)) : Empty();

    /// <summary>The deliverables in a package's bytes (the browser's).</summary>
    public static PackageIssues Of(byte[] bytes) => Read(() => new MemoryStream(bytes, writable: false));

    static PackageIssues Read(Func<Stream> open)
    {
        try
        {
            using var input = open();
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            var held = new Dictionary<string, long>();
            foreach (var e in zip.Entries)
                if (e.FullName.StartsWith(Prefix, StringComparison.Ordinal) && e.FullName.Length > Prefix.Length) held[e.FullName[Prefix.Length..]] = e.Length;
            return new PackageIssues(held.Count > 0 ? open : null, held, []);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return Empty();
        }
    }

    /// <summary>Bytes kept: their SHA-256 (hex), the key they are found by.</summary>
    public string Put(byte[] bytes)
    {
        var sha = LapinPackage.Hash(bytes);
        if (!_inSource.ContainsKey(sha)) _pending.TryAdd(sha, bytes);
        return sha;
    }

    public bool Has(string sha) => _pending.ContainsKey(sha) || _inSource.ContainsKey(sha);

    /// <summary>The size of the bytes kept for a hash; null when they are not here.</summary>
    public long? SizeOf(string sha) => _pending.TryGetValue(sha, out var b) ? b.LongLength : _inSource.TryGetValue(sha, out var n) ? n : null;

    /// <summary>The bytes of one hash; refused when the file no longer holds them.</summary>
    public byte[] Get(string sha)
    {
        if (_pending.TryGetValue(sha, out var b)) return b;
        if (!_inSource.ContainsKey(sha) || _source == null) throw new InvalidDataException("The project file no longer holds this issued file (its stored files were deleted, or it was issued before they were kept).");
        using var input = _source();
        using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var bytes = LapinPackage.Bytes(zip, Prefix + sha);
        if (LapinPackage.Hash(bytes) != sha) throw new InvalidDataException($"The project is damaged: the issued file {sha[..12]}… is not what was kept.");
        return bytes;
    }

    /// <summary>
    /// The bytes of the hashes asked for that are here, each handed to <paramref name="write"/> as a stream (the package being written):
    /// from memory, or copied entry by entry from the package they were read from, opened once.
    /// </summary>
    internal void CopyTo(IReadOnlyCollection<string> hashes, Action<string, Stream> write)
    {
        var fromSource = new List<string>();
        foreach (var sha in hashes)
        {
            if (_pending.TryGetValue(sha, out var b)) { using var ms = new MemoryStream(b, writable: false); write(sha, ms); }
            else if (_inSource.ContainsKey(sha)) fromSource.Add(sha);
        }
        if (fromSource.Count == 0 || _source == null) return;
        using var input = _source();
        using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var sha in fromSource)
        {
            if (zip.GetEntry(Prefix + sha) is not { } entry) continue;
            using var s = entry.Open();
            write(sha, s);
        }
    }

    /// <summary>
    /// The store after a save: the bytes written are found in the new package from now on; those made since the last save that were not
    /// written (an issue undone, which a redo may bring back) stay in memory; those the old package held and the new one does not are gone.
    /// </summary>
    public PackageIssues From(string path, IReadOnlyCollection<string> written) => After(() => File.OpenRead(path), written);
    public PackageIssues From(byte[] bytes, IReadOnlyCollection<string> written) => After(() => new MemoryStream(bytes, writable: false), written);

    PackageIssues After(Func<Stream> source, IReadOnlyCollection<string> written)
    {
        var held = new Dictionary<string, long>();
        foreach (var sha in written) if (SizeOf(sha) is { } n) held[sha] = n;
        var pending = _pending.Where(kv => !held.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        return new PackageIssues(held.Count > 0 ? source : null, held, pending);
    }
}

/// <summary>What a save writes of the issues: the store, the hashes the project's issue records keep (those here are written), and the readable index.</summary>
public sealed record IssueFiles(PackageIssues Store, IReadOnlyCollection<string> Keep, byte[]? Index)
{
    /// <summary>The hashes that will be written: kept by a record and held by the store.</summary>
    public IReadOnlyCollection<string> Written => Keep.Where(Store.Has).Distinct().ToList();
}
