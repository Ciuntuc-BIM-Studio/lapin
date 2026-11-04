using Lapin.Model;

namespace Lapin.Platform;

/// <summary>The ground's height at a point in plan (model height), for what stands on it (a scaffold's feet); a service of the module that holds the terrain.</summary>
public interface IGround
{
    double? ElevationAt(IModelView model, double x, double y);
}

/// <summary>
/// The project's phases as other modules need them, a service of the module
/// that owns them: the execution phase temporary elements (formwork,
/// scaffolding) are created and demolished in.
/// </summary>
public interface IPhaseService
{
    /// <summary>The active phase if it is an execution phase, else the first execution phase; one is added (after the others) when the project has none.</summary>
    string EnsureExecutionPhase(ICommandContext ctx);
}

/// <summary>
/// A temporary element's life on the work schedule (docs/plan-4d5d.md, stage 3): a formwork or a scaffold has no task of
/// its own, it lives by its hosts' tasks. <see cref="Hosts"/> are the elements it serves (the floor a formwork forms, the
/// walls a scaffold stands along); it goes up when the first task building a host starts, and comes down, for a formwork,
/// <see cref="StripAfter"/> working days after the task casting its host's core finishes, for a scaffold
/// (<see cref="UntilLast"/>) when the last task on its hosts (their finishes too) finishes.
/// </summary>
public sealed record TemporaryLife(string Element, string Kind, IReadOnlyList<string> Hosts, int StripAfter, bool UntilLast);

/// <summary>The temporary elements of the model and the hosts they live by; a service of the module that makes them (site works).</summary>
public interface ITaskLife
{
    IEnumerable<TemporaryLife> Temporaries(IModelView model);
}
