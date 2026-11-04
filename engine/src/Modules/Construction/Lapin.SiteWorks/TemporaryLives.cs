using Lapin.Model;
using Lapin.Platform;

namespace Lapin.SiteWorks;

/// <summary>
/// The site works' temporaries on the work schedule (docs/plan-4d5d.md, stage 3): each formwork lives by the element it
/// forms (up when its casting starts, struck <see cref="Formwork.StripAfter"/> working days after it finishes), each
/// scaffold by the walls it stands along (from the first task on them to the last, their finishes too).
/// </summary>
public sealed class TemporaryLives : ITaskLife
{
    public IEnumerable<TemporaryLife> Temporaries(IModelView m)
    {
        foreach (var f in m.OfClass(Formwork.Class))
            if (Formwork.HostOf(m, f) is { } host) yield return new(f.Id, "formwork", [host.Id], Formwork.StripAfter(m, f), false);
        foreach (var s in m.OfClass(Scaffolds.Class))
        {
            var walls = Scaffolds.WallsOf(m, s).Select(w => w.Id).ToList();
            if (walls.Count > 0) yield return new(s.Id, "scaffold", walls, 0, true);
        }
    }
}
