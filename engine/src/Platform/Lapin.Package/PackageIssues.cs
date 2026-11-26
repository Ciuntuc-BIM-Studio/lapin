using Lapin.Model;
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
/// The reference documents' files (docs/plan-cde-references.md §3.5, decision 878) are kept the same way under
/// <c>references/blobs/</c>, in a store of their own (<see cref="ReferencesPrefix"/>, <see cref="ReferencesOf(string)"/>).
/// </summary>
public sealed class PackageIssues
{
    public const string Prefix = "issues/blobs/";
    public const string IndexPath = "issues/index.json";
    public const string ReferencesPrefix = "references/blobs/";
    public const string ReferencesIndexPath = "references/index.json";

    readonly Func<Stream>? _source;
    readonly Dictionary<string, long> _inSource;
    readonly Dictionary<string, byte[]> _pending;
    readonly string _prefix;

    PackageIssues(Func<Stream>? source, Dictionary<string, long> inSource, Dictionary<string, byte[]> pending, string prefix = Prefix)
    {
        _source = source;
        _inSource = inSource;
        _pending = pending;
        _prefix = prefix;
    }

    /// <summary>Where in the package this store's bytes are: <c>issues/blobs/</c> or <c>references/blobs/</c>.</summary>
    public string BlobPrefix => _prefix;

    /// <summary>The readable index beside the blobs.</summary>
    public string IndexName => _prefix == ReferencesPrefix ? ReferencesIndexPath : IndexPath;

    /// <summary>What one of its files is called in a message.</summary>
    string Noun => _prefix == ReferencesPrefix ? "reference document file" : "issued file";

    /// <summary>
    /// Why the file's issued files could not be listed (null: they were). They are not lost: a save looks for every file a
    /// record keeps in the package they came from, and saving over a file it cannot read is refused (audit 15.11.2026, P6).
    /// </summary>
    public string? Unreadable { get; private init; }

    /// <summary>A store with nothing in it (a new project).</summary>
    public static PackageIssues Empty() => new(null, [], []);

    /// <summary>A reference documents' store with nothing in it.</summary>
    public static PackageIssues EmptyReferences() => new(null, [], [], ReferencesPrefix);

    /// <summary>The reference documents' files in the package at <paramref name="path"/> (<c>references/blobs/</c>); empty when it has none.</summary>
    public static PackageIssues ReferencesOf(string path) => File.Exists(path) ? Read(() => File.OpenRead(path), ReferencesPrefix) : EmptyReferences();

    /// <summary>The reference documents' files in a package's bytes.</summary>
    public static PackageIssues ReferencesOf(byte[] bytes) => Read(() => new MemoryStream(bytes, writable: false), ReferencesPrefix);

    /// <summary>The deliverables in the package at <paramref name="path"/>; empty when it has none, or is not a format 3 package.</summary>
    public static PackageIssues Of(string path) => File.Exists(path) ? Read(() => File.OpenRead(path)) : Empty();

    /// <summary>The deliverables in a package's bytes (the browser's).</summary>
    public static PackageIssues Of(byte[] bytes) => Read(() => new MemoryStream(bytes, writable: false));

    static PackageIssues Read(Func<Stream> open, string prefix = Prefix)
    {
        try
        {
            using var input = open();
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            var held = new Dictionary<string, long>();
            foreach (var e in zip.Entries)
                if (e.FullName.StartsWith(prefix, StringComparison.Ordinal) && e.FullName.Length > prefix.Length) held[e.FullName[prefix.Length..]] = e.Length;
            return new PackageIssues(held.Count > 0 ? open : null, held, [], prefix);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new PackageIssues(open, [], [], prefix) { Unreadable = ex.Message };
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
        if (!_inSource.ContainsKey(sha) || _source == null) throw new InvalidDataException(_prefix == ReferencesPrefix
            ? "The project file does not hold this reference document's file (too large to keep, or kept on the server)."
            : "The project file no longer holds this issued file (its stored files were deleted, or it was issued before they were kept).");
        using var input = _source();
        using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var bytes = LapinPackage.Bytes(zip, _prefix + sha);
        if (LapinPackage.Hash(bytes) != sha) throw new InvalidDataException($"The project is damaged: the {Noun} {sha[..12]}… is not what was kept.");
        return bytes;
    }

    /// <summary>
    /// The bytes of the hashes asked for that are here, each handed to <paramref name="write"/> as a stream (the package being written):
    /// from memory, or copied entry by entry from the package they were read from, opened once, each checked against its hash first
    /// (audit 15.11.2026, P22). Gives back what was written (hash → size); what could not be is said in <paramref name="report"/>,
    /// and when the store could not be read (<see cref="Unreadable"/>) nor its package opened now, a save over a file is refused.
    /// </summary>
    internal IReadOnlyDictionary<string, long> CopyTo(IReadOnlyCollection<string> hashes, Action<string, Stream> write, PackageWriteReport report)
    {
        var written = new Dictionary<string, long>();
        var fromSource = new List<string>();
        foreach (var sha in hashes)
        {
            if (_pending.TryGetValue(sha, out var b)) { using var ms = new MemoryStream(b, writable: false); write(sha, ms); written[sha] = b.LongLength; }
            else if (_inSource.ContainsKey(sha) || Unreadable != null) fromSource.Add(sha);
        }
        if (fromSource.Count == 0) return written;
        var lost = new List<string>();
        // A failure of the package being written is the save's (thrown on); only one of the package read from is caught here.
        var writing = false;
        try
        {
            if (_source == null) throw new FileNotFoundException("The file they were in is gone.");
            using var input = _source();
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var sha in fromSource)
            {
                if (zip.GetEntry(_prefix + sha) is not { } entry) { lost.Add($"{sha[..12]}… is not in the file"); continue; }
                string hash;
                using (var check = entry.Open()) hash = Hashes.Sha256Hex(check);
                if (hash != sha) { lost.Add($"{sha[..12]}… is damaged"); continue; }
                using var s = entry.Open();
                writing = true;
                write(sha, s);
                writing = false;
                written[sha] = entry.Length;
            }
        }
        catch (Exception ex) when (!writing && ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            var why = Unreadable ?? ex.Message;
            if (Unreadable != null) report.Refuse ??= $"The project's {Noun}s could not be read ({why}): saving over its file would lose them. Save it under another name.";
            report.Warnings.Add($"{fromSource.Count(s => !written.ContainsKey(s))} {Noun}(s) could not be carried over ({why}): the records are kept without them.");
            return written;
        }
        if (lost.Count > 0) report.Warnings.Add($"{lost.Count} {Noun}(s) could not be carried over ({string.Join(", ", lost)}): the records are kept without them.");
        return written;
    }

    /// <summary>
    /// The store after a save: the bytes written are found in the new package from now on; those made since the last save that were not
    /// written (an issue undone, which a redo may bring back) stay in memory; those the old package held and the new one does not are gone.
    /// </summary>
    public PackageIssues From(string path, IReadOnlyCollection<string> written) => After(() => File.OpenRead(path), Sizes(written));
    public PackageIssues From(byte[] bytes, IReadOnlyCollection<string> written) => After(() => new MemoryStream(bytes, writable: false), Sizes(written));

    /// <summary>The store after a save, with what the save wrote of it (<see cref="PackageWriteReport.Issues"/>): only those are held from now on.</summary>
    public PackageIssues From(string path, IReadOnlyDictionary<string, long> written) => After(() => File.OpenRead(path), written);
    public PackageIssues From(byte[] bytes, IReadOnlyDictionary<string, long> written) => After(() => new MemoryStream(bytes, writable: false), written);

    Dictionary<string, long> Sizes(IReadOnlyCollection<string> written)
    {
        var o = new Dictionary<string, long>();
        foreach (var sha in written) if (SizeOf(sha) is { } n) o[sha] = n;
        return o;
    }

    PackageIssues After(Func<Stream> source, IReadOnlyDictionary<string, long> written)
    {
        var held = new Dictionary<string, long>(written);
        var pending = _pending.Where(kv => !held.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        return new PackageIssues(held.Count > 0 ? source : null, held, pending, _prefix);
    }
}

/// <summary>
/// What a save writes of the issues: the store, the hashes the project's issue records keep (those here are written), and the readable index;
/// and, the same way, of the reference documents (<see cref="References"/>: their store, the hashes their records hold, <c>references/index.json</c>).
/// </summary>
public sealed record IssueFiles(PackageIssues Store, IReadOnlyCollection<string> Keep, byte[]? Index)
{
    /// <summary>The reference documents' files and index (docs/plan-cde-references.md §3.5); null: none written.</summary>
    public IssueFiles? References { get; init; }

    /// <summary>The hashes that will be written: kept by a record and held by the store.</summary>
    public IReadOnlyCollection<string> Written => Keep.Where(h => Store.Has(h) || Store.Unreadable != null).Distinct().ToList();
}
