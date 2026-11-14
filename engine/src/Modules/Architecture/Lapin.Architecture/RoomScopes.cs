using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// The rooms a formula or a tag of an element reads (decision 551, <see cref="ParamScopes"/>): <c>room</c>, the room an element
/// stands in (by its plan point: a path's middle, a placed element's point) on its storey; for a door or a window, the room on
/// its interior side, else on its exterior one; <c>fromRoom</c> and <c>toRoom</c>, a door's or window's rooms on its exterior and
/// interior side (as IFC's and other tools' from and to rooms: a door in an outside wall leads to its room). Read from the rooms'
/// outlines as they are kept, on every ask.
/// </summary>
public static class RoomScopes
{
    /// <summary>How far past a wall's faces a door's rooms are looked for (m).</summary>
    const double Past = 0.25;

    public static void Register()
    {
        ParamScopes.Add("room", Room);
        ParamScopes.Add("fromRoom", (m, e) => Sides(m, e) is { } s ? At(m, s.Storey, s.Exterior) : null);
        ParamScopes.Add("toRoom", (m, e) => Sides(m, e) is { } s ? At(m, s.Storey, s.Interior) : null);
        // A room's fields from its finish (forum topic 84), worked out on every ask: a tag or a schedule shows what the finish says now.
        DerivedFields.Add(Rooms.Class, new("finish.name", "Finish", (m, r) => FinishType(m, r)?.Name));
        DerivedFields.Add(Rooms.Class, new("finish.floor", "Finish: floor", (m, r) => Face(m, r, "floor")));
        DerivedFields.Add(Rooms.Class, new("finish.walls", "Finish: walls", (m, r) => Face(m, r, "walls")));
        DerivedFields.Add(Rooms.Class, new("finish.ceiling", "Finish: ceiling", (m, r) => Face(m, r, "ceiling")));
        // The floor finish a room is given inherits its finish's floor while it has none of its own.
        DerivedFields.Add(Rooms.Class, new("finishFloor", "Floor finish", (m, r) => Face(m, r, "floor")));
    }

    /// <summary>The room finish type a room is finished with; null when it has none.</summary>
    public static Entity? FinishType(IModelView m, Entity room) =>
        room.Str("finishType") is { } id && m.Get(id) is { Class: RoomFinishes.TypeClass } t ? t : null;

    /// <summary>
    /// The material a room's finish shows on a part: the floor's top layer, the walls' and the ceiling's layer facing the room (their
    /// last, laid from the wall's face and from the slab); null when the room has no finish or the finish nothing on that part.
    /// </summary>
    public static string? Face(IModelView m, Entity room, string part)
    {
        if (FinishType(m, room) is not { } type || RoomFinishes.SpecOf(m, type) is not { } spec) return null;
        var layers = part switch { "floor" => spec.Floor, "walls" => spec.Walls, _ => spec.Ceiling };
        if (layers is not { Count: > 0 }) return null;
        return part == "floor" ? layers[0].Material : layers[^1].Material;
    }

    /// <summary>The room <paramref name="e"/> stands in; a door's or window's on its interior side, else its exterior one; null for a room or what stands in none.</summary>
    public static Entity? Room(IModelView m, Entity e)
    {
        if (e.Class == Rooms.Class) return null;
        if (Sides(m, e) is { } s) return At(m, s.Storey, s.Interior) ?? At(m, s.Storey, s.Exterior);
        if (PointOf(e) is not { } p || ParamScopes.Of(m, e, "storey") is not { } storey) return null;
        return At(m, storey, p);
    }

    /// <summary>The room on <paramref name="storey"/> whose outline holds <paramref name="p"/>; null when none does.</summary>
    public static Entity? At(IModelView m, Entity storey, Vec2 p) =>
        m.RelatedOf(storey.Id, IfcClass.RelContainedInSpatialStructure).Where(r => r.Class == Rooms.Class)
            .FirstOrDefault(r => Rooms.Stored(r) is { } o && o.Holds(p));

    /// <summary>A door's or window's two sides in plan: a point past its wall's exterior face and one past its interior face, on the wall's storey.</summary>
    static (Entity Storey, Vec2 Exterior, Vec2 Interior)? Sides(IModelView m, Entity fill)
    {
        if (fill.Class is not (IfcClass.Door or IfcClass.Window) || Openings.OpeningOf(m, fill.Id) is not { } opening
            || Openings.WallOf(m, opening.Id) is not { Class: IfcClass.Wall } wall || Spatial.StoreyOf(m, wall.Id) is not { } storey) return null;
        var placed = Openings.Place(m, opening);
        var f = Openings.FrameOf(m, wall, placed.Along);
        // The type's +y (its exterior) is the wall's right, turned over with flipSide (as Openings.Geometry lays it).
        var sy = fill.Bool("flipSide") ? -1 : 1;
        var reach = (f.YMax - f.YMin) / 2 + Past;
        Vec2 Across(double y) => f.Start + f.Dir * placed.Along + f.Left * y;
        return (storey, Across(f.Centre - sy * reach), Across(f.Centre + sy * reach));
    }

    /// <summary>An element's point in plan: a path's middle, else its own point (<c>at</c>, or <c>x</c> and <c>y</c>); null when it has none.</summary>
    static Vec2? PointOf(Entity e)
    {
        static Vec2? Pt(JsonNode? n) => n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : null;
        if (Pt(e.Attrs["start"]) is { } s && Pt(e.Attrs["end"]) is { } t) return (s + t) * 0.5;
        if (Pt(e.Attrs["at"]) is { } at) return at;
        if (Json.Number(e.Attrs["x"]) is { } px && Json.Number(e.Attrs["y"]) is { } py) return new Vec2(px, py);
        return null;
    }
}
