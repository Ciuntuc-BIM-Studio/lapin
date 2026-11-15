using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// What a plan's drafted linework becomes in architecture (docs/plan-user-extensibility.md §7.4): walls along lines, polylines,
/// arcs and circles (joined at their corners, as Studio's walls along a shape); floors and rooms from closed outlines; railings
/// along lines. Each on the plan's storey, one undo step for all.
/// </summary>
public static class LineworkConverters
{
    static readonly string[] Plan = ["plan"];

    public static readonly LineworkConverter Walls = new("walls", "Walls", "architecture", "any", Plan,
    [
        new("typeId", "Wall type", "type", TypeClass: IfcClass.WallType),
        new("height", "Height", "length", JsonValue.Create(Lapin.Architecture.Walls.DefaultHeight)),
        new("justify", "The line is the wall's", "choice", JsonValue.Create("centre"), ["centre", "left", "right", "inside", "outside"]),
    ],
    (q, view, s, o) =>
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        var args = new JsonObject { ["shape"] = s.Shape.DeepClone(), ["storeyId"] = storey.Id, ["justify"] = (string?)o["justify"] ?? "centre" };
        if ((string?)o["typeId"] is { Length: > 0 } t) args["typeId"] = t;
        if (LineworkConversion.Num(o, "height") is { } h) args["height"] = h;
        return [new("architecture.wallsAlong", args, $"Walls along the {s.Kind} on {storey.Name}")];
    }, "Walls along lines, polylines, arcs and circles, joined at their corners.");

    public static readonly LineworkConverter Floors = new("floor", "Floor", "architecture", "closed", Plan,
    [
        new("typeId", "Floor type", "type", TypeClass: IfcClass.SlabType),
        new("offset", "Offset from the level", "length", JsonValue.Create(0.0)),
    ],
    (q, view, s, o) =>
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        var args = new JsonObject { ["storeyId"] = storey.Id, ["points"] = LineworkConversion.Points(s.Points) };
        if ((string?)o["typeId"] is { Length: > 0 } t) args["typeId"] = t;
        if (LineworkConversion.Num(o, "offset") is { } off) args["offset"] = off;
        return [new("architecture.addSlab", args, $"A floor of {s.Points.Count} corners on {storey.Name}")];
    }, "A floor on each closed outline.");

    public static readonly LineworkConverter Rooms = new("room", "Room", "architecture", "closed", Plan, [],
        (q, view, s, _) =>
        {
            var storey = LineworkConversion.PlanStorey(q.Model, view);
            return [new("architecture.addRoom", new JsonObject { ["storeyId"] = storey.Id, ["points"] = LineworkConversion.Points(s.Points) }, $"A room of {s.Points.Count} corners on {storey.Name}")];
        }, "A room bounded by each closed outline.");

    public static readonly LineworkConverter Railings = new("railing", "Railing", "architecture", "open", Plan,
    [
        new("style", "Style", "choice", JsonValue.Create("balusters"), Lapin.Architecture.Stairs.StairSpec.RailingStyles),
        new("height", "Height", "length", JsonValue.Create(1.0)),
        new("join", "One railing where they meet end to end", "bool", JsonValue.Create(true)),
    ],
    (q, view, s, o) =>
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        // An arc (a fillet's) stays one: its ends and its rise, built along the true arc.
        var arc = s.Kind == "arc" && s.Mid is { } mid && s.Points.Count >= 2;
        List<Lapin.Geometry.Vec2> pts = arc ? [s.Points[0], s.Points[^1]] : s.Closed ? [.. s.Points, s.Points[0]] : [.. s.Points];
        var args = new JsonObject { ["storeyId"] = storey.Id, ["points"] = LineworkConversion.Points(pts), ["style"] = (string?)o["style"] ?? "balusters" };
        if (arc) args["arcs"] = new JsonArray(Math.Round(PathCurve.RiseThrough(pts[0], pts[1], s.Mid!.Value), 9));
        if (LineworkConversion.Num(o, "height") is { } h) args["height"] = h;
        // Lines, arcs and polylines meeting end to end (a polyline filleted: line, arc, line) become one railing.
        if ((o["join"] ?? JsonValue.Create(true)) is JsonValue jv && jv.TryGetValue<bool>(out var join) && join) args["join"] = true;
        return [new("architecture.addRailing", args, $"A railing along {pts.Count} points on {storey.Name}")];
    }, "A railing along each line, arc or polyline; those meeting end to end make one.");

    public static void Register(IModuleRegistry r)
    {
        foreach (var c in new[] { Walls, Floors, Rooms, Railings }) r.Service<LineworkConverter>(c);
    }
}
