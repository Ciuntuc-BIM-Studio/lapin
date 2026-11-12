using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A step held open on the engine (docs/plan-python-scripting.md §8, decision 489): <c>doc.begin {label}</c> opens it,
/// <c>doc.commit</c> keeps it as one undo step named by the label, <c>doc.rollback</c> takes it back. It belongs to the
/// connection that opened it (<see cref="Connection"/>, set by the host per call): that connection's commands join it,
/// each command's diff going out as it is made; anyone else's edit, undo or redo, a new or opened project, is refused
/// while it is open (<see cref="ScriptEditing"/>), and reads go on. The host rolls it back when its connection is lost.
/// A step begun inside the open one by its own connection joins it, as a batch inside a batch does: only the outermost
/// commit or rollback ends it (stage 3, <c>lapin.step</c>).
/// </summary>
public sealed partial class Session
{
    /// <summary>What an edit is told while a script's step is open.</summary>
    public const string ScriptEditing = "A script is editing the model: wait for it to finish, or stop it.";

    /// <summary>What ending a step is told when none is open.</summary>
    public const string NoStep = "No step is open.";

    /// <summary>
    /// Who the call being handled is from (the host's client: one connection, or the connections of one script's
    /// Python, which share one), set by the host round each call; null for a call made in-process. An open step is its
    /// connection's.
    /// </summary>
    public object? Connection { get; set; }

    object? _stepOwner;

    /// <summary>How many steps begun inside the open one are still open (they join it; the outermost ends it).</summary>
    int _stepDepth;

    /// <summary>Whether a step is open.</summary>
    public bool StepOpen => _history.StepOpen;

    /// <summary>Whether <paramref name="connection"/> opened the step that is open.</summary>
    public bool OwnsStep(object? connection) => _history.StepOpen && Equals(_stepOwner, connection);

    /// <summary>
    /// A step opened for the calling connection, named <paramref name="label"/> (cut as a batch's label is). Begun while
    /// the connection's own step is open, it joins that one (true returned): its end only counts down to the outermost's.
    /// </summary>
    public bool Begin(string label)
    {
        if (OwnsStep(Connection)) { _stepDepth++; return true; }
        RefuseDuringStep(ownerToo: true);
        var named = string.IsNullOrWhiteSpace(label) ? "Script" : label.Trim();
        _history.Begin(named.Length > MaxBatchLabel ? named[..MaxBatchLabel] : named, Author);
        _stepOwner = Connection;
        _stepDepth = 0;
        return false;
    }

    /// <summary>
    /// The calling connection's step kept: one undo step, journalled once. Its diff carries the new undo state only
    /// (each command's went out already). An inner step's commit ends only it: nothing is kept until the outermost's.
    /// </summary>
    public Diff Commit()
    {
        RefuseIfNotOwner();
        if (_stepDepth > 0) { _stepDepth--; return Empty(); }
        _stepOwner = null;
        if (_history.Commit() is { } t) Emit("tx", t);
        return Empty();
    }

    /// <summary>
    /// The calling connection's step taken back: the model as it was before it; its diff puts every window back. An
    /// inner step's rollback ends only it and takes nothing back: what it did stays in the outer step, which decides (an
    /// exception it let through rolls the whole step back there).
    /// </summary>
    public Diff Rollback()
    {
        RefuseIfNotOwner();
        if (_stepDepth > 0) { _stepDepth--; return Empty(); }
        return RollbackStep();
    }

    /// <summary>The step of <paramref name="connection"/> taken back, when it has one (its connection was lost); null otherwise.</summary>
    public Diff? RollbackFor(object connection) => OwnsStep(connection) ? RollbackStep() : null;

    Diff RollbackStep()
    {
        _stepOwner = null;
        _stepDepth = 0;
        if (_history.Rollback() is not { } t) return Empty();
        Interlocked.Increment(ref _changes);
        Changed(t);
        return DiffAfterUndoing(t);
    }

    void RefuseIfNotOwner()
    {
        if (!_history.StepOpen) throw new CommandException(NoStep);
        if (!Equals(_stepOwner, Connection)) throw new CommandException(ScriptEditing);
    }

    /// <summary>
    /// Refused while a step is open, unless the call is its own connection's (<paramref name="ownerToo"/>: refused to it
    /// too, as undo, redo or another project are, which the step would not survive).
    /// </summary>
    public void RefuseDuringStep(bool ownerToo = false)
    {
        if (!_history.StepOpen) return;
        if (ownerToo || !Equals(_stepOwner, Connection))
            throw new CommandException(Equals(_stepOwner, Connection) ? $"The script's step „{_history.StepLabel}” is open: that waits for it to end." : ScriptEditing);
    }
}
