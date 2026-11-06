namespace Lapin.Platform;

/// <summary>
/// Results a module works out in the background and hands back later (docs/overrides.md, Background results): a command or
/// a query takes a <see cref="Stamp"/> of the model, starts its work on another thread and returns; once ready, the work
/// calls <see cref="Deliver"/> from any thread. The session applies it on its own thread, between calls, as an edit of its
/// own: one undo step under <c>label</c>, by whoever ran the call that took the stamp, journalled and sent to every
/// interface as an ordinary diff. A delivery the model has moved past is dropped (stale), never merged.
/// </summary>
public interface IDeliveries
{
    /// <summary>
    /// The model now, to check a delivery against. Taken on the session's thread (in the command or query that starts the
    /// work); inside a command it stands after that command's own edit, so the command does not make its own work stale.
    /// </summary>
    DeliveryStamp Stamp();

    /// <summary>
    /// <paramref name="apply"/> queued for the session's thread, as one undo step named <paramref name="label"/>. Stale, and
    /// dropped, when a project was opened or made since <paramref name="since"/>, or when anything changed since it (with
    /// <paramref name="reads"/>: any of those entity or relationship ids). May be called from any thread.
    /// </summary>
    void Deliver(string label, DeliveryStamp since, Action<ICommandContext> apply, IReadOnlyCollection<string>? reads = null);
}

/// <summary>The model as a delivery started from it: the session's change count then, and who the work is for.</summary>
public readonly record struct DeliveryStamp(long Value, string Author);

/// <summary>
/// What became of one delivery: <c>applied</c> (with its diff), <c>empty</c> (it changed nothing), <c>stale</c> (the model
/// moved past it) or <c>failed</c> (it threw: the model is as it was, <see cref="Error"/> says why).
/// </summary>
public sealed record DeliveryOutcome(string Label, string Status, string Author, Diff? Diff = null, string? Error = null);
