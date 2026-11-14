using Lapin.Drawing.Hatch;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What bounds rooms on a storey (docs/rooms.md): the walls, curtain walls and
/// columns standing across a plane <see cref="Height"/> over the storey,
/// their envelopes (their bodies without openings, so a door does not let a
/// room through) cut by that plane, its room separation lines
/// (<see cref="RoomDividers"/>, forum topic 80), and the free space between them. Solved
/// once per model revision and storey, and shared by every room of it: a
/// geometry producer that asks gets the reads of the first one that solved
/// it, so it is rebuilt when a wall moves just the same.
/// </summary>
public static class RoomBounds
{
    /// <summary>The classes whose elements bound rooms.</summary>
    public static readonly string[] Bounding = [IfcClass.Wall, "IfcCurtainWall", IfcClass.Column];
    /// <summary>The plane rooms are found in, over their storey's elevation (m).</summary>
    public const double Height = 1.2;

    static readonly RevisionCache<FreeSpace> Cache = new();

    /// <summary>The free space of a storey's plan; null when the storey is not there.</summary>
    public static FreeSpace? Of(IModelView m, IServices services, string storeyId) =>
        m.Get(storeyId) is { Class: IfcClass.BuildingStorey } storey ? Cache.Get(storey, m, t => Solve(t, services, storey)) : null;

    static FreeSpace Solve(IModelView m, IServices services, Entity storey)
    {
        var z = storey.Num("elevation") + Height;
        var frame = new ViewFrame(new Vec3(0, 0, z), new Vec3(1, 0, 0), new Vec3(0, 1, 0), -Vec3.UnitZ);
        var cutters = services.Get<ICutters>();
        var paths = services.Get<IPathElements>();
        var areas = new List<IReadOnlyList<IReadOnlyList<Vec2>>>();
        if (cutters != null)
            foreach (var cls in Bounding)
                foreach (var e in m.OfClass(cls))
                {
                    Solid? env;
                    try { env = cutters.EnvelopeOf(e, m); }
                    catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException) { continue; }
                    if (env == null || Measure.Bounds(env) is not { } b || b.Min.Z >= z || b.Max.Z <= z) continue;
                    // A straight wall's footprint as its joins cut it: the envelope ends square at the axis, so at a corner
                    // the two left out the corner's outer square, and a room on that side (its corner over 180°) ran into it.
                    if (paths != null && JoinedFootprint(e, m, paths) is { } joined) { areas.Add([joined]); continue; }
                    var loops = HiddenLines.Section(env, frame);
                    if (loops.Count > 0) areas.Add(loops.Select(l => (IReadOnlyList<Vec2>)l).ToList());
                }
        // Room separation lines (forum topic 80): virtual walls with no body, bounding rooms as a wall would.
        areas.AddRange(RoomDividers.Areas(m, storey.Id));
        return FreeSpace.Of(areas);
    }

    /// <summary>
    /// A straight path element's outline in plan with its ends where its joins put them (a mitre, a butt corner, a T),
    /// as its body is built; null for an arc or an element that is not a path.
    /// </summary>
    public static IReadOnlyList<Vec2>? JoinedFootprint(Entity e, IModelView m, IPathElements paths)
    {
        if (!paths.IsPath(e.Class) || PathCurve.Of(e).IsArc || paths.FrameOf(e, m) is not { } f) return null;
        var ends = Joins.WithOwnEnds(Joins.EndsOf(m, e, paths, []), e, f);
        var start = ends[0]?.Point ?? f.Start;
        var along = (ends[1]?.Point ?? f.End) - start;
        var length = along.Length;
        if (length < 1e-9) return null;
        var dir = along.Normalized();
        var left = dir.Perp;
        var (sc, ec) = (ends[0]?.Cut ?? EndCut.Square, ends[1]?.Cut ?? EndCut.Square);
        Vec2 P(double x, double y) => start + dir * x + left * y;
        // A cut's Right is at the right face (YMin), its Left at the left face (YMax).
        return [P(-sc.Right, f.YMin), P(length + ec.Right, f.YMin), P(length + ec.Left, f.YMax), P(-sc.Left, f.YMax)];
    }

    /// <summary>The region round <paramref name="p"/> on the storey, or why there is none.</summary>
    public static (FreeRegion? Region, string? Problem) At(IModelView m, IServices services, string storeyId, Vec2 p)
    {
        var space = Of(m, services, storeyId);
        if (space == null) return (null, "The room's storey is gone.");
        if (space.InMaterial(p)) return (null, "That point is inside a wall or a column: click inside the room.");
        // Made exact on the room separation lines: the rooms on either side meet on the line (forum topic 80).
        return space.Around(p) is { } r ? (RoomDividers.Exact(m, storeyId, r), null) : (null, "Nothing closes round that point: walls, curtain walls or columns must enclose the room. Close it, or draw the room's outline instead.");
    }
}
