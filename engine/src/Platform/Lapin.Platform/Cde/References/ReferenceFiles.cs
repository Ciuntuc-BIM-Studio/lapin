using Lapin.Model;

namespace Lapin.Platform.Cde.References;

/// <summary>
/// Where the bytes of the project's reference documents are kept (docs/plan-cde-references.md §3.5), by their SHA-256: the
/// project's file under <c>references/blobs/</c>, through the host (the dispatcher's <c>ProjectFiles</c>), or memory for a
/// session on its own. Content-addressed, so the same file added twice is kept once. A save writes only what a record names.
/// </summary>
public interface IReferenceFiles
{
    /// <summary>Bytes kept: their SHA-256 (lowercase hex).</summary>
    string Put(byte[] bytes);

    bool Has(string sha256);

    /// <summary>The bytes of one hash; refused (<see cref="InvalidDataException"/>) when they are not here.</summary>
    byte[] Get(string sha256);
}

/// <summary>The bytes in memory: a session without a host (tests, scripts), until something writes them out.</summary>
public sealed class MemoryReferenceFiles : IReferenceFiles
{
    readonly Dictionary<string, byte[]> _held = new(StringComparer.Ordinal);

    public string Put(byte[] bytes)
    {
        var sha = Hashes.Sha256Hex(bytes);
        _held.TryAdd(sha, bytes);
        return sha;
    }

    public bool Has(string sha256) => _held.ContainsKey(sha256);

    public byte[] Get(string sha256) => _held.TryGetValue(sha256, out var b) ? b : throw new InvalidDataException("This reference document's file is not held here.");
}
