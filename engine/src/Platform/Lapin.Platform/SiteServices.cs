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
