using Lapin.Drawing.Hatch;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What bounds rooms on a storey (docs/rooms.md): the walls, curtain walls and
/// columns standing across a plane <see cref="Height"/> over the storey,
/// their envelopes (their bodies without openings, so a door does not let a
/// room through) cut by that plane, and the free space between them. Solved
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

    sealed record Solved(long Revision, FreeSpace Space, HashSet<string> Reads);
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Entity, Solved> Cache = new();

    /// <summary>The free space of a storey's plan; null when the storey is not there.</summary>
    public static FreeSpace? Of(IModelView m, IServices services, string storeyId)
    {
        if (m.Get(storeyId) is not { Class: IfcClass.BuildingStorey } storey) return null;
        if (Cache.TryGetValue(storey, out var hit) && hit.Revision == m.Revision)
        {
            if (m is TrackingView tv) tv.Read.UnionWith(hit.Reads);
            return hit.Space;
        }
        var t = new TrackingView(m);
        t.Get(storeyId);
        var space = Solve(t, services, storey);
        Cache.AddOrUpdate(storey, new Solved(m.Revision, space, t.Read));
        return space;
    }

    static FreeSpace Solve(IModelView m, IServices services, Entity storey)
    {
        var z = storey.Num("elevation") + Height;
        var frame = new ViewFrame(new Vec3(0, 0, z), new Vec3(1, 0, 0), new Vec3(0, 1, 0), -Vec3.UnitZ);
        var cutters = services.Get<ICutters>();
        var areas = new List<IReadOnlyList<IReadOnlyList<Vec2>>>();
        if (cutters != null)
            foreach (var cls in Bounding)
                foreach (var e in m.OfClass(cls))
                {
                    Solid? env;
                    try { env = cutters.EnvelopeOf(e, m); }
                    catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException) { continue; }
                    if (env == null || Measure.Bounds(env) is not { } b || b.Min.Z >= z || b.Max.Z <= z) continue;
                    var loops = HiddenLines.Section(env, frame);
                    if (loops.Count > 0) areas.Add(loops.Select(l => (IReadOnlyList<Vec2>)l).ToList());
                }
        return FreeSpace.Of(areas);
    }

    /// <summary>The region round <paramref name="p"/> on the storey, or why there is none.</summary>
    public static (FreeRegion? Region, string? Problem) At(IModelView m, IServices services, string storeyId, Vec2 p)
    {
        var space = Of(m, services, storeyId);
        if (space == null) return (null, "The room's storey is gone.");
        if (space.InMaterial(p)) return (null, "That point is inside a wall or a column: click inside the room.");
        return space.Around(p) is { } r ? (r, null) : (null, "Nothing closes round that point: walls, curtain walls or columns must enclose the room. Close it, or draw the room's outline instead.");
    }
}
