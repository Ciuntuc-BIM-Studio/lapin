using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Graphs;

/// <summary>
/// The model's elements read as references for a graph (decision 534): an axis, an outline, a placement, a box, a drafted
/// shape. Read from the model as it is at the run (the preview's copy, the bake's edit), so what a graph makes follows the
/// element when it moves. Curves are made into points by <see cref="Chords"/>, the one rule for every curve. An element
/// none of these fits fails on its item, saying what it is and what to use instead.
/// </summary>
public static class References
{
    public static Entity Element(NodeCall c, string port)
    {
        var id = Values.Text(c[port]);
        if (id.Length == 0) throw new CommandException($"{port}: an element is missing.");
        return c.Model.Get(id) ?? throw new CommandException($"The model has no element {id}.");
    }

    /// <summary>The level an element stands on: its storey's, else the view's storey's, else 0.</summary>
    static Entity? StoreyOf(NodeCall c, Entity e) =>
        Spatial.StoreyOf(c.Model, e.Id) is { Class: IfcClass.BuildingStorey } s ? s
        : c.Storey != null && c.Model.Get(c.Storey) is { Class: IfcClass.BuildingStorey } v ? v : null;

    static double Level(NodeCall c, Entity e) => StoreyOf(c, e)?.Num("elevation") ?? 0;

    /// <summary>An element's base: its storey's level and its own offset from it (baseOffset, a floor's offset).</summary>
    static double Base(NodeCall c, Entity e) => Level(c, e) + (e.NumOrNull("baseOffset") ?? e.NumOrNull("offset") ?? 0);

    static Vec3 At(Vec2 p, double z) => new(p.X, p.Y, z);

    /// <summary>
    /// The axis of an element: a path element's (a wall, a beam, a strip footing: start, end and an arc's rise; at its base,
    /// a beam's at its top under the level above), a railing's points, each axis of a grid, a steel member's stick. One
    /// polyline each, with its start and end; a grid gives a list of each.
    /// </summary>
    public static object?[] Axis(NodeCall c)
    {
        var e = Element(c, "id");
        List<List<Vec3>> runs;
        if (e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray)
        {
            var curve = PathCurve.Of(e);
            double z;
            if (e.NumOrNull("baseOffset") is null && e.NumOrNull("topOffset") is { } top && StoreyOf(c, e) is { } s)
                z = s.Num("elevation") + s.Num("height", 3) + top;
            else z = Base(c, e);
            var pts = curve.IsArc ? curve.Line(0, curve.Length, 0) : [curve.Start, curve.End];
            runs = [[.. pts.Select(p => At(p, z))]];
        }
        else if (e.Attrs["points"] is JsonArray points && points.Count >= 2 && e.Attrs["contour"] == null)
            runs = [[.. points.Select(p => At(Args.PointOf(p), Base(c, e)))]];
        else if (e.Class == IfcClass.Grid && e.Attrs["axes"] is JsonArray axes)
        {
            var z = Level(c, e);
            runs = [.. axes.OfType<JsonObject>().Where(a => a["a"] is JsonArray && a["b"] is JsonArray).Select(a => new List<Vec3> { At(Args.PointOf(a["a"]), z), At(Args.PointOf(a["b"]), z) })];
            return [runs.Select(r => (object?)new Polyline(r)).ToList(), runs.Select(r => (object?)r[0]).ToList(), runs.Select(r => (object?)r[^1]).ToList()];
        }
        else if (c.Query?.Services.All<IStickAxes>().Select(s => s.StickOf(c.Model, e)).FirstOrDefault(x => x != null) is { } stick)
            runs = [[stick.A, stick.B]];
        else throw new CommandException($"„{e.Name}” ({e.Class}) has no axis: walls, beams, footings, railings and grids have one (a floor's edge: Outline; a drafted line: Drafting geometry).");
        var run = runs[0];
        return [new Polyline(run), run[0], run[^1]];
    }

    /// <summary>The closed outline of an element drawn by one (a floor, a room, a roof, a planting, a parcel), at its base: its points too.</summary>
    public static object?[] Outline(NodeCall c)
    {
        var e = Element(c, "id");
        if (e.Attrs["contour"] is not JsonArray contour || contour.Count < 3)
            throw new CommandException($"„{e.Name}” ({e.Class}) has no outline: floors, rooms, roofs and site zones have one (a wall: Axis; any element: Bounding box).");
        var z = Base(c, e);
        var pts = contour.Select(p => At(Args.PointOf(p), z)).ToList();
        if (pts.Count > 3 && (pts[0] - pts[^1]).Length < 1e-9) pts.RemoveAt(pts.Count - 1);
        return [new Polyline(pts, true), pts.Cast<object?>().ToList()];
    }

    /// <summary>
    /// Where an element placed at a point stands, and its turn (degrees): a column, a pad footing, furniture (at its base);
    /// a door or a window at its wall's axis, at its sill, turned as the wall runs there.
    /// </summary>
    public static object?[] Location(NodeCall c)
    {
        var e = Element(c, "id");
        if (e.Attrs["at"] is JsonArray at)
            return [At(Args.PointOf(at), Base(c, e)), e.NumOrNull("rotation") ?? 0];
        if (c.Model.RelatingOf(e.Id, IfcClass.RelFillsElement) is { } opening && c.Model.RelatingOf(opening.Id, IfcClass.RelVoidsElement) is { } wall
            && wall.Attrs["start"] is JsonArray && wall.Attrs["end"] is JsonArray)
        {
            var curve = PathCurve.Of(wall);
            var along = Math.Clamp(opening.Num("along"), 0, curve.Length);
            var t = curve.TangentAt(along);
            return [At(curve.PointAt(along), Base(c, wall) + opening.Num("sill")), Math.Atan2(t.Y, t.X) * 180 / Math.PI];
        }
        throw new CommandException($"„{e.Name}” ({e.Class}) is not placed at a point: columns, pad footings, furniture, doors and windows are (a wall: Axis; any element: Bounding box).");
    }

    /// <summary>The box round an element's body as the engine builds it (the views' and the exports' geometry): its lowest and highest corners, its centre and its size.</summary>
    public static object?[] Box(NodeCall c)
    {
        var e = Element(c, "id");
        var g = c.Query?.Geometry(e.Id);
        (Vec3 Min, Vec3 Max)? box = null;
        void Take(Vec3 lo, Vec3 hi) => box = box is { } b ? (new Vec3(Math.Min(b.Min.X, lo.X), Math.Min(b.Min.Y, lo.Y), Math.Min(b.Min.Z, lo.Z)), new Vec3(Math.Max(b.Max.X, hi.X), Math.Max(b.Max.Y, hi.Y), Math.Max(b.Max.Z, hi.Z))) : (lo, hi);
        foreach (var p in g?.Parts ?? [])
            if (Measure.Bounds(p.Solid) is { } b) Take(b.Min, b.Max);
        foreach (var l in g?.Lines ?? [])
            foreach (var p in l) Take(p, p);
        if (box == null && c.Query?.Services.All<IDraftedGeometry>().Select(d => d.RunsOf(c.Model, c.Query.Services, e)).FirstOrDefault(r => r != null) is { } runs)
            foreach (var p in runs.SelectMany(r => r.Points)) Take(p, p);
        if (box is not { } bb) throw new CommandException($"„{e.Name}” ({e.Class}) has no body to measure.");
        var size = bb.Max - bb.Min;
        return [bb.Min, bb.Max, (bb.Min + bb.Max) * 0.5, size.X, size.Y, size.Z];
    }

    /// <summary>
    /// A drafted shape (a line, a polyline, a rectangle, an arc, a circle, a DXF's linework) as a polyline in the model, on
    /// its view's plane (a plan's at its storey's level), curves in chords: its line, its points, its start and end, and the
    /// centre of an arc or a circle (nothing for the rest).
    /// </summary>
    public static object?[] Drafted(NodeCall c)
    {
        var e = Element(c, "id");
        var services = c.Query?.Services;
        var runs = services?.All<IDraftedGeometry>().Select(d => d.RunsOf(c.Model, services, e)).FirstOrDefault(r => r != null);
        if (runs is not { Count: > 0 })
            throw new CommandException(e.Class == IfcClass.Annotation
                ? $"„{e.Name}” is not a drafted line, polyline, arc or circle on a plan, section or elevation (texts, dimensions and tags have no line)."
                : $"„{e.Name}” ({e.Class}) is not drafted: Axis, Outline or Location read a model element.");
        var run = runs[0];
        return [new Polyline(run.Points, run.Closed), run.Points.Cast<object?>().ToList(), run.Points[0], run.Closed ? run.Points[0] : run.Points[^1], run.Centre];
    }
}
