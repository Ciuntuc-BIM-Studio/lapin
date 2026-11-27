using Lapin.Geometry;
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

    /// <summary>
    /// A closed shape drafted on an elevation or a section, made a wall opening of its own outline (audit S4): the straight wall the
    /// view looks at, parallel to it, nearest the view's plane, whose length holds the shape; the shape's corners and arcs read on the
    /// wall's face (along from its start, up from its base). A circle stays round (its four quarter arcs).
    /// </summary>
    public static readonly LineworkConverter WallOpenings = new("wallOpening", "Wall opening", "architecture", "closed", ["elevation", "section"], [],
    (q, view, s, o) =>
    {
        var (corners, rises) = s.Outline();
        if (Views.PlaneOf(q.Model, q.Services, view, s.Middle.X) is not { } plane) throw new CommandException("The view has no plane to read the shape on.");
        var world = corners.Select(plane.World).ToList();
        var mid = new Vec3(world.Average(p => p.X), world.Average(p => p.Y), world.Average(p => p.Z));
        var right = new Vec2(plane.Right.X, plane.Right.Y);
        Entity? best = null;
        var bestD = double.PositiveInfinity;
        foreach (var w in q.Model.OfClass(IfcClass.Wall))
        {
            if (PathCurve.Of(w).IsArc || StoredBodies.Has(w)) continue;
            var (a, b) = (Lapin.Architecture.Walls.Start(w), Lapin.Architecture.Walls.End(w));
            var dir = (b - a).Normalized();
            if (Math.Abs(Vec2.Cross(dir, right.Normalized())) > 1e-3) continue;
            var along = Vec2.Dot(new Vec2(mid.X, mid.Y) - a, dir);
            if (along < 0 || along > (b - a).Length) continue;
            var d = Math.Abs(Vec2.Cross(dir, new Vec2(mid.X, mid.Y) - a));
            if (d < bestD) (best, bestD) = (w, d);
        }
        if (best == null) throw new CommandException("No straight wall faces the view behind the shape: draw it over a wall seen square on.");
        var f = Openings.FrameOf(q.Model, best);
        var pts = world.Select(p => new Vec2(Vec2.Dot(new Vec2(p.X, p.Y) - f.Start, f.Dir), p.Z - f.Base)).ToList();
        // Seen from the wall's back the shape is mirrored on its face: each arc bulges the other way.
        var flip = Vec2.Dot(f.Dir, right.Normalized()) < 0;
        var args = new JsonObject { ["wallId"] = best.Id, ["profile"] = LineworkConversion.Points(pts) };
        if (rises != null) args["arcs"] = new JsonArray([.. rises.Select(r => (JsonNode)Math.Round(flip ? -r : r, 9))]);
        return [new("architecture.addWallOpening", args, $"An opening of its own outline in {best.Name}")];
    }, "A wall opening of the shape's outline, in the wall the view looks at.");

    /// <summary>
    /// A closed outline made an opening in the floor under it (audit S12, S14): the floor of the plan's storey whose outline holds
    /// the shape's middle; its edges straight, arcs or a whole ellipse, as a floor's (decision 589).
    /// </summary>
    public static readonly LineworkConverter FloorOpenings = new("floorOpening", "Floor opening", "architecture", "closed", Plan, [],
    (q, view, s, o) =>
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        var (corners, rises) = s.Outline();
        var mid = s.Middle;
        var slab = q.Model.OfClass(IfcClass.Slab).FirstOrDefault(x => Spatial.StoreyOf(q.Model, x.Id)?.Id == storey.Id && Polygon2.Contains(Slabs.Contour(x), mid))
            ?? throw new CommandException($"No floor on {storey.Name} under the shape: draw the opening inside a floor.");
        var args = new JsonObject { ["storeyId"] = storey.Id, ["slabId"] = slab.Id, ["points"] = LineworkConversion.Points(corners) };
        if (rises != null) args["arcs"] = new JsonArray([.. rises.Select(r => (JsonNode)Math.Round(r, 9))]);
        if (s.Ellipses() is { } ellipses) args["ellipses"] = ellipses;
        return [new("architecture.addSlabOpening", args, $"An opening of {corners.Count} corners in {slab.Name}")];
    }, "An opening in the floor under each closed outline; a circle makes a round one.");

    /// <summary>
    /// A stair of the type chosen along a drafted path (audit S12), as far as a stair takes one: its foot and the way it climbs.
    /// A line or polyline: the foot at its first point, climbing towards its second; a polyline turning at its second corner
    /// turns the stair that way (an L or U type's). A closed outline: the foot at the middle of its first edge, climbing across
    /// it into the outline, as wide as that edge. Its run, landings and turns are the type's (the configurator's).
    /// </summary>
    public static readonly LineworkConverter StairsAlong = new("stair", "Stair", "architecture", "any", Plan,
    [
        new("typeId", "Stair type", "type", TypeClass: IfcClass.StairType),
        new("width", "Width (empty: the type's; an outline's first edge)", "length"),
    ],
    (q, view, s, o) =>
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        LineworkConversion.Straight(s, "A stair's path");
        var pts = s.Points;
        if (pts.Count < 2) throw new CommandException("A stair's path needs two points at least.");
        Vec2 foot, to;
        string? turn = null;
        double? width = LineworkConversion.Num(o, "width");
        if (s.Closed && pts.Count >= 3)
        {
            var (a, b) = (pts[0], pts[1]);
            foot = (a + b) * 0.5;
            var along = (b - a).Normalized();
            // Into the outline: its left when it runs counter-clockwise, else its right.
            var area = 0.0;
            for (var i = 0; i < pts.Count; i++) area += Vec2.Cross(pts[i], pts[(i + 1) % pts.Count]);
            var inward = area >= 0 ? new Vec2(-along.Y, along.X) : new Vec2(along.Y, -along.X);
            to = foot + inward;
            width ??= (b - a).Length;
        }
        else
        {
            (foot, to) = (pts[0], pts[1]);
            if (pts.Count >= 3 && Vec2.Cross(pts[1] - pts[0], pts[2] - pts[1]) is var c && Math.Abs(c) > 1e-9) turn = c > 0 ? "left" : "right";
        }
        if ((to - foot).Length < 1e-6) throw new CommandException("A stair's path needs its first two points apart.");
        var args = new JsonObject { ["storeyId"] = storey.Id, ["x"] = Math.Round(foot.X, 9), ["y"] = Math.Round(foot.Y, 9), ["x2"] = Math.Round(to.X, 9), ["y2"] = Math.Round(to.Y, 9) };
        if ((string?)o["typeId"] is { Length: > 0 } t) args["typeId"] = t;
        if (turn != null) args["turn"] = turn;
        if (width is { } w && w > 0) args["width"] = Math.Round(w, 9);
        return [new("architecture.addStair", args, $"A stair on {storey.Name}")];
    }, "A stair of the type chosen: its foot at the path's start, climbing along it; an outline's first edge is its foot and width.");

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

    /// <summary>
    /// Curtain walls along drafted lines, polylines, arcs and circles (forum topic 282): one per straight segment and one per arc
    /// (an arc past a half turn in two, a circle in four quarters), in the shape's order, of the type chosen (else the project's
    /// first); a segment under 100 mm is left out and counted. Through <c>architecture.curtainWallsAlong</c>, so each wall is
    /// checked as the Curtain wall tool's.
    /// </summary>
    public static readonly LineworkConverter CurtainWallsAlong = new("curtainWalls", "Curtain walls", "architecture", "any", Plan,
    [
        new("typeId", "Curtain wall type", "type", TypeClass: CurtainWalls.TypeClass),
        new("height", "Height", "length", JsonValue.Create(Lapin.Architecture.Walls.DefaultHeight)),
        new("baseOffset", "Base offset", "length", JsonValue.Create(0.0)),
    ],
    (q, view, s, o) =>
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        if (s.Kind == "ellipse") LineworkConversion.Straight(s, "A curtain wall");
        var (pts, rises, closed) = CurtainPath(s);
        var args = new JsonObject { ["storeyId"] = storey.Id, ["points"] = LineworkConversion.Points(pts), ["closed"] = closed };
        if (rises.Any(r => r != 0)) args["arcs"] = new JsonArray([.. rises.Select(r => (JsonNode)Math.Round(r, 9))]);
        if ((string?)o["typeId"] is { Length: > 0 } t) args["typeId"] = t;
        if (LineworkConversion.Num(o, "height") is { } h) args["height"] = h;
        if (LineworkConversion.Num(o, "baseOffset") is { } b) args["baseOffset"] = b;
        var n = closed ? pts.Count : pts.Count - 1;
        return [new("architecture.curtainWallsAlong", args, n == 1 ? $"A curtain wall along the {s.Kind} on {storey.Name}" : $"{n} curtain walls along the {s.Kind} on {storey.Name}")];
    }, "A curtain wall along each segment of a line, polyline, arc or circle, of the type chosen.");

    /// <summary>
    /// A shape as a curtain wall path: its points, each segment's rise (0 straight) and whether it closes. A polyline's edges are
    /// straight; an arc is one segment, or as many as keep each under a half turn; a circle four quarter arcs.
    /// </summary>
    internal static (List<Vec2> Points, List<double> Rises, bool Closed) CurtainPath(LineworkShape s)
    {
        if (s.Kind == "circle" && s.Centre is { } cc && s.Radius is { } cr)
        {
            var (corners, r4) = ArcOutline.Circle(cc, cr);
            return (corners, [.. r4], true);
        }
        if (s.Kind == "arc" && s.Centre is { } c && s.Radius is { } r && s.Mid is { } mid && s.Points.Count >= 2)
        {
            var (p0, p1) = (s.Points[0], s.Points[^1]);
            double Angle(Vec2 p) => Math.Atan2(p.Y - c.Y, p.X - c.X);
            static double Ccw(double from, double to) { var d = (to - from) % (2 * Math.PI); return d < 0 ? d + 2 * Math.PI : d; }
            var (a0, sweep) = (Angle(p0), Ccw(Angle(p0), Angle(p1)));
            // Clockwise when its middle is not on the anticlockwise way from start to end.
            if (Ccw(a0, Angle(mid)) > sweep) sweep -= 2 * Math.PI;
            var n = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / Math.PI - 1e-9));
            Vec2 At(double t) => c + new Vec2(Math.Cos(a0 + sweep * t), Math.Sin(a0 + sweep * t)) * r;
            var pts = Enumerable.Range(0, n + 1).Select(k => k == 0 ? p0 : k == n ? p1 : At((double)k / n)).ToList();
            var rises = Enumerable.Range(0, n).Select(k => PathCurve.RiseThrough(pts[k], pts[k + 1], At((k + 0.5) / n))).ToList();
            return (pts, rises, false);
        }
        var closed = s.Closed && s.Points.Count > 2;
        return ([.. s.Points], [.. Enumerable.Repeat(0.0, closed ? s.Points.Count : Math.Max(0, s.Points.Count - 1))], closed);
    }

    public static void Register(IModuleRegistry r)
    {
        foreach (var c in new[] { Walls, Floors, FloorOpenings, Ceilings, Rooms, RoofsFromOutlines, Balconies, StairsAlong, Railings, SkirtingsAlong, WallOpenings, CurtainWallsAlong }) r.Service<LineworkConverter>(c);
    }
}
