using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// A downpipe hangs on its roof's gutter (<c>roofId</c>) and goes with its roof (audit 15.11.2026): selected with the roof,
/// it is moved, mirrored or copied by the roof (<see cref="RoofPlacement"/> takes its downpipes along with a copy), never a
/// second time on its own, which put two on one gutter. Selected alone, it stays on its gutter: no copy is made.
/// </summary>
sealed class DownpipeHosting : IHostedPlacement
{
    public bool Handles(IModelView m, Entity e) => e.Class == Drainage.DownpipeClass;

    public string? HostOf(IModelView m, Entity e) =>
        e.Class == Drainage.DownpipeClass && e.Str("roofId") is { } rid && m.Get(rid) is { Class: IfcClass.Roof } ? rid : null;

    public IReadOnlyList<string> Transform(ICommandContext ctx, IReadOnlyList<Entity> elements, Placement2D t, bool copy) => [];
}
