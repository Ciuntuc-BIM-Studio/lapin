using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// What a plan's drafted linework becomes in architecture (docs/plan-user-extensibility.md §7.4): walls along lines, polylines,
/// arcs and circles (joined at their corners, as Studio's walls along a shape, a closed shape's bottom and top floors too);
/// floors, ceilings and rooms from closed outlines (a circle round, by its arcs: decision 589); roofs and balconies from closed
/// outlines with straight edges; railings and skirtings along lines. Each on the plan's storey, one undo step for all
/// (audit S12: Convert to… is the one place that makes elements of drawn shapes).
/// </summary>
public static class LineworkConverters
{
    static readonly string[] Plan = ["plan"];

    public static readonly LineworkConverter Walls = new("walls", "Walls", "architecture", "any", Plan,
    [
        new("typeId", "Wall type", "type", TypeClass: IfcClass.WallType),
        new("height", "Height", "length", JsonValue.Create(Lapin.Architecture.Walls.DefaultHeight)),
        new("justify", "The line is the wall's", "choice", JsonValue.Create("centre"), ["centre", "left", "right", "inside", "outside"]),
        new("bottom", "A floor at the bottom of a closed shape", "bool", JsonValue.Create(false)),
        new("top", "A floor at the top of a closed shape", "bool", JsonValue.Create(false)),
    ],
    (q, view, s, o) =>
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        var args = new JsonObject { ["shape"] = s.Shape.DeepClone(), ["storeyId"] = storey.Id, ["justify"] = (string?)o["justify"] ?? "centre" };
        if ((string?)o["typeId"] is { Length: > 0 } t) args["typeId"] = t;
        if (LineworkConversion.Num(o, "height") is { } h) args["height"] = h;
        // Studio's BOX on a plan: the shape's bottom and top as floors (a closed shape only; an open one is still walls).
        var closed = s.Closed || s.Kind == "circle";
        if (closed && LineworkConversion.Flag(o, "bottom", false)) args["bottom"] = true;
        if (closed && LineworkConversion.Flag(o, "top", false)) args["top"] = true;
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
        var (corners, rises) = s.Outline();
        var args = new JsonObject { ["storeyId"] = storey.Id, ["points"] = LineworkConversion.Points(corners) };
        if (rises != null) args["arcs"] = new JsonArray([.. rises.Select(r => (JsonNode)Math.Round(r, 9))]);
        if (s.Ellipses() is { } ellipses) args["ellipses"] = ellipses;
        if ((string?)o["typeId"] is { Length: > 0 } t) args["typeId"] = t;
        if (LineworkConversion.Num(o, "offset") is { } off) args["offset"] = off;
        return [new("architecture.addSlab", args, s.Kind is "circle" or "ellipse" ? $"A round floor on {storey.Name}" : $"A floor of {corners.Count} corners on {storey.Name}")];
    }, "A floor on each closed outline; a circle makes a round one.");

    public static readonly LineworkConverter Ceilings = new("ceiling", "Ceiling", "architecture", "closed", Plan,
    [
        new("typeId", "Ceiling type", "type", TypeClass: Lapin.Architecture.Ceilings.TypeClass),
        new("height", "Height above the level", "length", JsonValue.Create(2.7)),
    ],
    (q, view, s, o) =>
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        var (corners, rises) = s.Outline();
        var args = new JsonObject { ["storeyId"] = storey.Id, ["points"] = LineworkConversion.Points(corners) };
        if (rises != null) args["arcs"] = new JsonArray([.. rises.Select(r => (JsonNode)Math.Round(r, 9))]);
        if (s.Ellipses() is { } ellipses) args["ellipses"] = ellipses;
        if ((string?)o["typeId"] is { Length: > 0 } t) args["typeId"] = t;
        if (LineworkConversion.Num(o, "height") is { } h) args["height"] = h;
        return [new("architecture.addCeiling", args, s.Kind is "circle" or "ellipse" ? $"A round ceiling on {storey.Name}" : $"A ceiling of {corners.Count} corners on {storey.Name}")];
    }, "A ceiling on each closed outline; a circle makes a round one.");

    /// <summary>The roof styles of the catalogue, by id, with their names.</summary>
    static readonly string[] RoofStyleIds = [.. Roofs.RoofCatalog.Styles.Select(x => x.Id)];
    static readonly string[] RoofStyleNames = [.. Roofs.RoofCatalog.Styles.Select(x => x.Label)];

    public static readonly LineworkConverter RoofsFromOutlines = new("roof", "Roof", "architecture", "closed", Plan,
        [new("style", "Roof style", "choice", JsonValue.Create(RoofStyleIds[0]), RoofStyleIds, ChoiceLabels: RoofStyleNames)],
    (q, view, s, o) =>
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        // A roof's eaves are straight: its outline has no arcs (decision 589 gave them to floors, floor openings and ceilings only).
        LineworkConversion.Straight(s, "A roof's outline");
        var args = new JsonObject { ["storeyId"] = storey.Id, ["points"] = LineworkConversion.Points(s.Points) };
        if ((string?)o["style"] is { Length: > 0 } st) args["style"] = st;
        return [new("architecture.addRoof", args, $"A roof of {s.Points.Count} eaves on {storey.Name}")];
    }, "A roof in the style chosen on each closed outline, its eaves the outline's edges.");

    public static readonly LineworkConverter Balconies = new("balcony", "Balcony", "architecture", "closed", Plan,
        [new("typeId", "Balcony style", "type", TypeClass: Lapin.Architecture.Balconies.BalconyModule.TypeClass)],
    (q, view, s, o) =>
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        LineworkConversion.Straight(s, "A balcony's outline");
        var args = new JsonObject { ["storeyId"] = storey.Id, ["contour"] = LineworkConversion.Points(s.Points) };
        if ((string?)o["typeId"] is { Length: > 0 } t) args["typeId"] = t;
        return [new("architecture.addBalcony", args, $"A balcony of {s.Points.Count} corners on {storey.Name}")];
    }, "A balcony slab on each closed outline, with its railing and supports from the style.");

    public static readonly LineworkConverter SkirtingsAlong = new("skirting", "Skirting", "architecture", "open", Plan,
    [
        new("typeId", "Skirting type", "type", TypeClass: Skirtings.TypeClass),
        new("side", "The board lies on the line's", "choice", JsonValue.Create("auto"), ["auto", "left", "right"]),
    ],
    (q, view, s, o) =>
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        LineworkConversion.Straight(s, "A skirting");
        var args = new JsonObject { ["storeyId"] = storey.Id, ["points"] = LineworkConversion.Points(s.Points), ["closed"] = s.Closed };
        if ((string?)o["typeId"] is { Length: > 0 } t) args["typeId"] = t;
        if ((string?)o["side"] is "left" or "right") args["side"] = (string?)o["side"];
        return [new("architecture.addSkirting", args, $"A skirting along {s.Points.Count} points on {storey.Name}")];
    }, "A skirting along each line or polyline, the board on the side away from the wall it runs by.");

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
        foreach (var c in new[] { Walls, Floors, Ceilings, Rooms, RoofsFromOutlines, Balconies, Railings, SkirtingsAlong }) r.Service<LineworkConverter>(c);
    }
}
