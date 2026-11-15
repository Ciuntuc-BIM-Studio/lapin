using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A room's finish goes where its room goes (audit 15.11.2026, G3): deleted with it (its finish parts, its skirting and its
/// slab); laid again (each piece keeping its GlobalId) when the room is moved, turned or mirrored, so its slab and skirting
/// follow; laid for a copy of a finished room, unless its pieces were copied with it (they name the copy). A piece copied
/// without its room is not kept: it belongs to the room it was laid for. A copy in place (Copy to storeys) is left to Copy
/// to storeys, which takes the finish with the room (<see cref="StoreyCopies"/>).
/// </summary>
sealed class RoomFinishLinks : ITransformFollowers, IDeletionFollowers
{
    public void Deleting(ICommandContext ctx, IReadOnlySet<string> deleting)
    {
        foreach (var id in deleting)
            if (ctx.Model.Get(id) is { Class: Rooms.Class })
                foreach (var piece in RoomFinishes.Laid(ctx.Model, id).Where(x => !deleting.Contains(x.Id)).ToList()) ctx.Remove(piece.Id);
    }

    public object? Before(ICommandContext ctx, IReadOnlySet<string> moving, IReadOnlySet<string> selected) => null;

    public void After(ICommandContext ctx, object? state, Placement2D t, Placement2D plan, IReadOnlySet<string> moving, IReadOnlySet<string> selected, IReadOnlyDictionary<string, string> copies)
    {
        var m = ctx.Model;
        if (copies.Count == 0)
        {
            foreach (var id in moving)
                if (m.Get(id) is { Class: Rooms.Class } room && Type(m, room) is { } type) RoomFinishes.Apply(ctx, room, type);
            return;
        }
        var copiedRooms = copies.Where(p => m.Get(p.Value) is { Class: Rooms.Class }).Select(p => p.Value).ToHashSet();
        // A piece copied without its room.
        foreach (var c in copies.Values)
            if (m.Get(c) is { } piece && RoomOf(piece) is { } room && !copiedRooms.Contains(room)) ctx.Remove(piece.Id);
        if (InPlace(plan)) return;
        foreach (var id in copiedRooms)
            if (m.Get(id) is { } room && Type(m, room) is { } type && !RoomFinishes.Laid(m, id).Any()) RoomFinishes.Apply(ctx, room, type);
    }

    /// <summary>The room a finish piece was laid for (its finish part, the skirting or slab made for it); null for anything else.</summary>
    static string? RoomOf(Entity e) =>
        e.Class == RoomFinishes.Class ? e.Str("roomId") : e.Class is Skirtings.Class or IfcClass.Slab ? e.Str("finishOf") : null;

    static Entity? Type(IModelView m, Entity room) =>
        room.Str("finishType") is { } tid && m.Get(tid) is { Class: RoomFinishes.TypeClass } type ? type : null;

    static bool InPlace(Placement2D t) =>
        !t.Mirrored && (t.Point(new Lapin.Geometry.Vec2(0, 0)) - new Lapin.Geometry.Vec2(0, 0)).Length < 1e-12 && (t.Point(new Lapin.Geometry.Vec2(1, 0)) - new Lapin.Geometry.Vec2(1, 0)).Length < 1e-12;
}
