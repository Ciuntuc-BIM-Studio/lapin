using System.Text.Json.Nodes;

namespace Lapin.Package.Remote;

/// <summary>A ref as a remote lists it (§10.3): its name, the id it holds (<c>target</c>), when and by whom it last moved.</summary>
public sealed record RemoteRef(string Name, string Target, string? UpdatedAt = null, RemoteAuthor? UpdatedBy = null);

/// <summary>The refs of a remote: unchanged since the ETag given (<see cref="NotModified"/>), or every ref with the new ETag.</summary>
public sealed record RefsResult(bool NotModified, IReadOnlyList<RemoteRef> Refs, string? Etag);

/// <summary>One compare-and-swap (§10.4): the ref, the value expected (null: must not exist), the value to set (null: delete).</summary>
public sealed record RefUpdate(string Name, string? Old, string? New);

/// <summary>A project on a remote (§10.2): its id, name, default ref and the caller's role.</summary>
public sealed record RemoteProject(string Id, string Name, string DefaultRef, string? Role);

/// <summary>A detached entry of a folder remote's ref log (§7.3): not on the ref's chain, kept forever, handled once by its author.</summary>
public sealed record DetachedEntry(string File, string Name, string? New, RemoteAuthor Author);

/// <summary>
/// A refusal from a remote, as the protocol says it (§9.6, Appendix A): the HTTP status, the stable <see cref="Code"/> clients act
/// on, the human message and the body with its details (<c>refs</c> of a 409, <c>missing</c> of a 422).
/// </summary>
public sealed class RemoteException(int status, string code, string message, JsonObject? body = null) : IOException(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public JsonObject Body { get; } = body ?? [];
}

/// <summary>
/// A remote of lapin-remote 1.0: a folder (<see cref="FolderRemote"/>, §7) or a server (<see cref="HttpRemote"/>, §9–§10), seen the
/// same way by the operations (<see cref="RemoteOps"/>). Synchronous: the engine answers one call at a time. A token, when the
/// remote needs one, is handed to it by its maker and never written anywhere by the engine.
/// </summary>
public interface IRemote : IDisposable
{
    /// <summary><c>folder</c> or <c>server</c>.</summary>
    string Kind { get; }

    /// <summary>The most ids in one <see cref="Missing"/> or <see cref="GetMany"/> call (<c>maxBatch</c>).</summary>
    int MaxBatch { get; }

    RemoteProject Project();

    /// <summary>Every ref, or <see cref="RefsResult.NotModified"/> when the refs' ETag is still <paramref name="etag"/>.</summary>
    RefsResult Refs(string? etag);

    /// <summary>The updates, all or none (a folder: in order, §7.4); a ref whose value is not its <c>old</c> fails with a 409 <c>ref_moved</c>. The refs after, and their ETag.</summary>
    RefsResult UpdateRefs(IReadOnlyList<RefUpdate> updates);

    /// <summary>Of <paramref name="ids"/> (at most <see cref="MaxBatch"/>), those the remote lacks, in the order asked, each once.</summary>
    IReadOnlyList<string> Missing(IReadOnlyList<string> ids);

    /// <summary>One object stored; its kind tells a folder where (a commit or a tag in commits/, the rest in objects/).</summary>
    void Put(string id, byte[] bytes, ObjectKind kind);

    /// <summary>One object's bytes, verified; null when the remote lacks it.</summary>
    byte[]? Get(string id);

    /// <summary>Several objects' bytes (at most <see cref="MaxBatch"/>), each verified; refused when one is missing.</summary>
    IReadOnlyDictionary<string, byte[]> GetMany(IReadOnlyList<string> ids);

    /// <summary>The commits reachable from <paramref name="from"/> and not from any of <paramref name="stop"/>, each before its parents, as their canonical bytes (verified).</summary>
    IReadOnlyList<(string Id, byte[] Bytes)> Commits(string from, IReadOnlyList<string> stop);

    /// <summary>Whether the remote takes viewer copies (§12.2).</summary>
    bool Viewers { get; }

    /// <summary>A viewer copy attached to a commit (the IFC object uploaded first).</summary>
    void PutViewer(string commit, string ifc, long size, string schema);

    /// <summary>The detached entries of the refs (a folder's, §7.3); none on a server.</summary>
    IReadOnlyList<DetachedEntry> Detached();

    /// <summary>What the remote had to tell the user (a folder's stale lock broken).</summary>
    List<string> Notes { get; }
}
