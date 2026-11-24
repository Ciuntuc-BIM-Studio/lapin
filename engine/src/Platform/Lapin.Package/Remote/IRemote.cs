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
/// A remote of lapin-remote 1.0: a folder (<see cref="FolderRemote"/>, §7) or a server (<c>HttpRemote</c>, in Lapin.Package.Http: §9–§10), seen the
/// same way by the operations (<see cref="RemoteOps"/>). Asynchronous, so the one client runs on the desktop and in the browser's
/// engine (WebAssembly, where <c>HttpClient</c> is the page's fetch and nothing may block: Atrium docs/plan-cde-web.md §2.1 A);
/// the engine still answers one call at a time (a remote call holds the queue of calls until it ends). A folder answers at once. A
/// token, when the remote needs one, is handed to it by its maker and never written anywhere by the engine.
/// </summary>
public interface IRemote : IDisposable
{
    /// <summary><c>folder</c> or <c>server</c>.</summary>
    string Kind { get; }

    /// <summary>What the remote must know before the operations start (a server's capabilities: its limits and features). Called first by every operation.</summary>
    Task ReadyAsync();

    /// <summary>The most ids in one <see cref="MissingAsync"/> or <see cref="GetManyAsync"/> call (<c>maxBatch</c>); known once <see cref="ReadyAsync"/> has run.</summary>
    int MaxBatch { get; }

    Task<RemoteProject> ProjectAsync();

    /// <summary>Every ref, or <see cref="RefsResult.NotModified"/> when the refs' ETag is still <paramref name="etag"/>.</summary>
    Task<RefsResult> RefsAsync(string? etag);

    /// <summary>The updates, all or none (a folder: in order, §7.4); a ref whose value is not its <c>old</c> fails with a 409 <c>ref_moved</c>. The refs after, and their ETag.</summary>
    Task<RefsResult> UpdateRefsAsync(IReadOnlyList<RefUpdate> updates);

    /// <summary>Of <paramref name="ids"/> (at most <see cref="MaxBatch"/>), those the remote lacks, in the order asked, each once.</summary>
    Task<IReadOnlyList<string>> MissingAsync(IReadOnlyList<string> ids);

    /// <summary>One object stored; its kind tells a folder where (a commit or a tag in commits/, the rest in objects/).</summary>
    Task PutAsync(string id, byte[] bytes, ObjectKind kind);

    /// <summary>One object's bytes, verified; null when the remote lacks it.</summary>
    Task<byte[]?> GetAsync(string id);

    /// <summary>Several objects' bytes (at most <see cref="MaxBatch"/>), each verified; refused when one is missing.</summary>
    Task<IReadOnlyDictionary<string, byte[]>> GetManyAsync(IReadOnlyList<string> ids);

    /// <summary>The commits reachable from <paramref name="from"/> and not from any of <paramref name="stop"/>, each before its parents, as their canonical bytes (verified).</summary>
    Task<IReadOnlyList<(string Id, byte[] Bytes)>> CommitsAsync(string from, IReadOnlyList<string> stop);

    /// <summary>Whether the remote takes viewer copies (§12.2); known once <see cref="ReadyAsync"/> has run.</summary>
    bool Viewers { get; }

    /// <summary>A viewer copy attached to a commit (the IFC object uploaded first).</summary>
    Task PutViewerAsync(string commit, string ifc, long size, string schema);

    /// <summary>The detached entries of the refs (a folder's, §7.3); none on a server.</summary>
    IReadOnlyList<DetachedEntry> Detached();

    /// <summary>What the remote had to tell the user (a folder's stale lock broken).</summary>
    List<string> Notes { get; }
}
