using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Room separation lines in the architecture module (forum topic 80, <see cref="RoomDividers"/>): the class, drawn in plans of its
/// storey as a thin dashed line and in nothing else (it has no body), measured by its length; drawn with the room tools (a line,
/// a polyline, a rectangle, an arc, a circle) or made from drafted linework. Each command is one undo step; the rooms around it
/// find their outlines again on their own.
/// </summary>
public static class RoomDividerElements
{
    public const string Name = "Room separation line";

    /// <summary>The line's look in plan: dashed, in a thin pen (pen 2, as entourage).</summary>
    static readonly LineStyleSpec Dashed = new(Pen: 2, Pattern: "dashed");

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices services)
    {
        if (!RoomDividers.Is(e)) return null;
        var paths = RoomDividers.Paths(e.Attrs["shape"]);
        if (paths.Count == 0) return new ElementGeometry([], Diagnostics: ["The room separation line has no shape: draw it again."]);
        var plan = paths.Select(p => new PlanItem("symbol", p.Closed, p.Points, Style: Dashed)).ToList();
        return new ElementGeometry([], plan, [.. paths.SelectMany(p => p.Points)]);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (!RoomDividers.Is(e)) yield break;
        yield return new QuantitySet("Lapin_RoomSeparationQuantities", [new("Length", RoomDividers.LengthOf(e), "LENGTH", "Length")]);
    }

    public static Entity Add(ICommandContext ctx, JsonObject a)
    {
        var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
        Spatial.Require(ctx.Model, storeyId, IfcClass.BuildingStorey);
        var shape = RoomDividers.ShapeOf(a);
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : Name;
        var e = new Entity(ctx.NewId(), RoomDividers.Class, name, new JsonObject { ["shape"] = shape }, RoomDividers.Predefined);
        ctx.Add(e);
        Spatial.Contain(ctx, storeyId, e.Id);
        return e;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(RoomDividers.Class, "Room separation line", [], "#6b7a90"));
        r.Geometry(RoomDividers.Class, Geometry);
        r.Quantities(RoomDividers.Class, Quantities);

        // A room separation line on a storey ({storeyId, kind: line | polyline | rectangle | arc | circle, points, closed}, or a drafted {shape}).
        r.Command("addRoomDivider", "Room separation line", (ctx, a) => new JsonObject { ["id"] = Add(ctx, a).Id });

        // A room separation line drawn again ({id} and its kind and points, or a shape).
        r.Command("updateRoomDivider", "Change room separation line", (ctx, a) =>
        {
            var e = Spatial.Require(ctx.Model, (string?)a["id"], RoomDividers.Class);
            if (!RoomDividers.Is(e)) throw new CommandException("Not a room separation line.");
            var attrs = e.CloneAttrs();
            attrs["shape"] = RoomDividers.ShapeOf(a);
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // Drafted linework to room separation lines (Convert drafting to…): any line, polyline, arc or circle of a plan.
        r.Service<LineworkConverter>(new("roomDivider", "Room separation lines", "architecture", "any", ["plan"], [],
            (q, view, s, _) =>
            {
                var storey = LineworkConversion.PlanStorey(q.Model, view);
                return [new("architecture.addRoomDivider", new JsonObject { ["storeyId"] = storey.Id, ["shape"] = s.Shape.DeepClone() }, $"A room separation line along the {s.Kind} on {storey.Name}")];
            }, "A room separation line along each line, polyline, arc or circle: it bounds rooms as a wall would."));
    }
}
