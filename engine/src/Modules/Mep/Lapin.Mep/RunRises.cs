using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// Where a run rises or drops inside itself (its plane changed while it was
/// drawn: a point with two levels, <see cref="Runs.PointLevel"/>): the plan
/// shows the rise / drop node's sign there, so the vertical leg is read in
/// plan as one placed by hand is. Its jogs to the ports at its ends are not
/// marked (the node there is).
/// </summary>
public static partial class Runs
{
    /// <summary>The vertical legs of a centreline between its ends: the point in plan, and whether the run goes up there (in its direction).</summary>
    public static IEnumerable<(Vec2 At, bool Up)> Rises(IReadOnlyList<Vec3> line)
    {
        for (var i = 1; i + 2 < line.Count; i++)
            if ((line[i + 1].Xy - line[i].Xy).Length < 1e-9 && Math.Abs(line[i + 1].Z - line[i].Z) > 1e-6)
                yield return (line[i].Xy, line[i + 1].Z > line[i].Z);
    }

    /// <summary>ISO 4067, as the rise node draws it: a ring; a dot where the run rises, a cross where it drops.</summary>
    internal static IEnumerable<PlanItem> RiseMarks(IReadOnlyList<Vec3> line)
    {
        foreach (var (at, up) in Rises(line))
        {
            yield return new PlanItem("symbol", true, [.. Chords.Ring(at, 0.06)]);
            if (up) yield return new PlanItem("heavy", true, [.. Chords.Ring(at, 0.015)]);
            else
            {
                const double c = 0.042;
                yield return new PlanItem("symbol", false, [at + new Vec2(-c, -c), at + new Vec2(c, c)]);
                yield return new PlanItem("symbol", false, [at + new Vec2(-c, c), at + new Vec2(c, -c)]);
            }
        }
    }
}
