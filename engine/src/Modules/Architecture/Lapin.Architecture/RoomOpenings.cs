using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A room's doors and windows, chosen by type and side in its configurator (docs/rooms.md, Uși și ferestre din încăpere), off by
/// default and beside placing them on walls, which goes on as before. The room keeps a list (<c>roomOpenings</c>); each entry
/// makes an ordinary door or window in the wall on that side, tagged with the room (<c>roomOpening</c>) and found again by its
/// key. An entry remembers a point on its side, not the side's number: an outline found from the walls may start anywhere and
/// a mirror reverses it, while the point is carried with the room. Straight walls only.
/// </summary>
public static class RoomOpenings
{
    public const string Key = "roomOpenings";
    public const string FillTag = "roomOpening";

    public sealed record Side(int Index, Vec2 A, Vec2 B)
    {
        public Vec2 Dir => (B - A).Normalized();
        public Vec2 Mid => (A + B) * 0.5;
        public double Length => (B - A).Length;
    }

    public static bool IsOn(Entity room) => room.Attrs[Key] is JsonObject o && o["on"] is JsonValue v && v.TryGetValue<bool>(out var on) && on;

    static IEnumerable<JsonObject> Items(Entity room) => (room.Attrs[Key] as JsonObject)?["items"] is JsonArray a ? a.OfType<JsonObject>() : [];

    /// <summary>The sides of the room's outline, in its order, what lies in one line taken as one.</summary>
    public static List<Side> Sides(Entity room, IModelView m, IServices services)
    {
        if (Rooms.Live(room, m, services) is not { } outline) return [];
        var pts = new List<Vec2>();
        var n = outline.Outer.Count;
        for (var i = 0; i < n; i++)
        {
            var (prev, here, next) = (outline.Outer[(i + n - 1) % n], outline.Outer[i], outline.Outer[(i + 1) % n]);
            var (u, v) = (here - prev, next - here);
            if (u.Length < 1e-6 || v.Length < 1e-6) continue;
            if (Math.Abs(Vec2.Cross(u.Normalized(), v.Normalized())) > 1e-6) pts.Add(here);
        }
        var sides = new List<Side>();
        for (var i = 0; i < pts.Count; i++) sides.Add(new Side(i, pts[i], pts[(i + 1) % pts.Count]));
        return sides;
    }

    /// <summary>The straight wall of the room's storey running along a side, at a point on it: the nearest whose body the point lies in.</summary>
    public static Entity? WallAt(IModelView m, Entity room, Side side, Vec2 p)
    {
        if (Spatial.StoreyOf(m, room.Id) is not { } storey) return null;
        Entity? best = null;
        var bestDistance = double.MaxValue;
        foreach (var wall in m.OfClass(IfcClass.Wall))
        {
            if (PathCurve.Of(wall) is { IsArc: true } || Spatial.StoreyOf(m, wall.Id)?.Id != storey.Id) continue;
            var (a, b) = (Walls.Start(wall), Walls.End(wall));
            var d = b - a;
            if (d.Length < 1e-6 || Math.Abs(Vec2.Cross(d.Normalized(), side.Dir)) > 0.01) continue;
            var along = Vec2.Dot(p - a, d.Normalized());
            if (along < -1e-3 || along > d.Length + 1e-3) continue;
            var distance = Math.Abs(Vec2.Cross(d.Normalized(), p - a));
            var f = Openings.FrameOf(m, wall);
            if (distance > Math.Max(Math.Abs(f.YMin), Math.Abs(f.YMax)) + 0.02 || distance >= bestDistance) continue;
            (best, bestDistance) = (wall, distance);
        }
        return best;
    }

    static Side? NearestSide(IReadOnlyList<Side> sides, Vec2 p)
    {
        Side? best = null;
        var bestDistance = double.MaxValue;
        foreach (var s in sides)
        {
            var t = Math.Clamp(Vec2.Dot(p - s.A, s.Dir), 0, s.Length);
            var d = (s.A + s.Dir * t - p).Length;
            if (d < bestDistance) (best, bestDistance) = (s, d);
        }
        return best;
    }

    /// <summary>The door or window an entry made, while it is still there and still this room's (a copied room's entries name the original's).</summary>
    static Entity? FillOf(IModelView m, Entity room, JsonObject item) =>
        (string?)item["fill"] is { } id && m.Get(id) is { } fill && fill.Str(FillTag) == room.Id ? fill : null;

    /// <summary>Where the door or window of an entry stands now, on its wall's axis.</summary>
    static Vec2? PointOf(IModelView m, Entity fill)
    {
        if (Openings.OpeningOf(m, fill.Id) is not { } opening || Openings.WallOf(m, opening.Id) is not { } wall) return null;
        var (a, b) = (Walls.Start(wall), Walls.End(wall));
        return a + (b - a).Normalized() * Openings.Place(m, opening).Along;
    }

    static void RemoveFill(ICommandContext ctx, Entity fill)
    {
        var opening = Openings.OpeningOf(ctx.Model, fill.Id);
        ctx.Remove(fill.Id);
        if (opening != null) ctx.Remove(opening.Id);
    }

    static Entity Tagged(ICommandContext ctx, Entity fill, Entity room)
    {
        var attrs = fill.CloneAttrs();
        attrs[FillTag] = room.Id;
        var next = fill.With(attrs: attrs);
        ctx.Replace(next);
        return next;
    }

    /// <summary>The room's list as given, its doors and windows made, changed or taken away to match, in one step.</summary>
    public static void Set(ICommandContext ctx, Entity room, bool on, JsonArray? given)
    {
        var sides = Sides(room, ctx.Model, ctx.Services);
        var before = Items(room).ToList();
        var wanted = given?.OfType<JsonObject>().ToList() ?? before;
        var kept = new List<JsonNode>();
        var used = new HashSet<string>();

        if (on)
            foreach (var item in wanted)
            {
                var key = (string?)item["key"] is { Length: > 0 } k ? k : Guid.NewGuid().ToString("N")[..8];
                var kind = (string?)item["kind"] == "window" ? "window" : "door";
                var type = ((string?)item["typeId"] ?? (string?)item["type"]) is { } named ? ctx.Model.Get(named) is { Class: IfcClass.DoorType or IfcClass.WindowType } t ? t : throw new CommandException("There is no such door or window type.")
                    : Openings.EnsureType(ctx, kind);
                Side side;
                Vec2 at;
                if (Json.Number(item["side"]) is { } index)
                {
                    side = sides.FirstOrDefault(s => s.Index == (int)index) ?? throw new CommandException($"The room has no side {(int)index + 1}.");
                    at = side.Mid + side.Dir * (Json.Number(item["offset"]) ?? 0);
                }
                else
                {
                    at = item["at"] is JsonArray { Count: >= 2 } p ? Args.PointOf(p) : throw new CommandException("Say on which side of the room it goes.");
                    side = NearestSide(sides, at) ?? throw new CommandException($"„{room.Name}” has no outline yet.");
                }
                var wall = WallAt(ctx.Model, room, side, at) ?? throw new CommandException($"Side {side.Index + 1} of „{room.Name}” has no straight wall there to take it.");
                var along = Openings.AlongOf(wall, at);
                var sill = Json.Number(item["sill"]);

                var fill = before.FirstOrDefault(b => (string?)b["key"] == key) is { } was ? FillOf(ctx.Model, room, was) : null;
                if (fill != null && Openings.OpeningOf(ctx.Model, fill.Id) is { } opening && Openings.WallOf(ctx.Model, opening.Id)?.Id != wall.Id)
                {
                    RemoveFill(ctx, fill);
                    fill = null;
                }
                var expected = type.Class == IfcClass.DoorType ? IfcClass.Door : IfcClass.Window;
                // One already there at this place (the room's copy, with its wall's own copy of the door): taken over, not doubled.
                fill ??= Openings.Of(ctx.Model, wall.Id)
                    .Where(o => Math.Abs(Openings.Place(ctx.Model, o).Along - along) < 0.005)
                    .Select(o => Openings.FillOf(ctx.Model, o.Id))
                    .FirstOrDefault(f => f?.Class == expected);
                if (fill == null)
                {
                    fill = Openings.AddFill(ctx, wall, type, along, sill);
                    // The room is the inside: a door opens into it, a window's exterior looks away from it.
                    var inside = side.Mid + new Vec2(-side.Dir.Y, side.Dir.X) * 0.1;
                    if (Vec2.Cross(Walls.End(wall) - Walls.Start(wall), inside - Walls.Start(wall)) < 0)
                    {
                        Openings.Flip(ctx, fill, "y");
                        fill = ctx.Model.Get(fill.Id)!;
                    }
                }
                else
                {
                    if (ctx.Model.RelatingOf(fill.Id, IfcClass.RelDefinesByType)?.Id != type.Id)
                    {
                        Openings.SetType(ctx, fill, type);
                        fill = ctx.Model.Get(fill.Id)!;
                    }
                    Openings.Move(ctx, fill, along, sill);
                    fill = ctx.Model.Get(fill.Id)!;
                }
                fill = Tagged(ctx, fill, room);
                used.Add(fill.Id);
                var entry = new JsonObject { ["key"] = key, ["kind"] = kind, ["type"] = type.Id, ["at"] = new JsonArray(at.X, at.Y), ["fill"] = fill.Id };
                if (sill != null) entry["sill"] = sill;
                kept.Add(entry);
            }
        else
            foreach (var item in wanted)
            {
                var entry = (JsonObject)item.DeepClone();
                if (FillOf(ctx.Model, room, item) is { } fill && PointOf(ctx.Model, fill) is { } here) entry["at"] = new JsonArray(here.X, here.Y);
                entry.Remove("fill");
                kept.Add(entry);
            }

        foreach (var item in before)
            if (FillOf(ctx.Model, room, item) is { } fill && !used.Contains(fill.Id)) RemoveFill(ctx, fill);

        var attrs = ctx.Model.Get(room.Id)!.CloneAttrs();
        attrs[Key] = new JsonObject { ["on"] = on, ["items"] = new JsonArray([.. kept]) };
        ctx.Replace(ctx.Model.Get(room.Id)!.With(attrs: attrs));
    }

    /// <summary>What the configurator shows: the sides, the walls on them, and each entry on its side as it stands now.</summary>
    public static JsonObject Read(Entity room, IModelView m, IServices services)
    {
        var sides = Sides(room, m, services);
        var items = new JsonArray();
        foreach (var item in Items(room))
        {
            var fill = FillOf(m, room, item);
            var at = (fill != null ? PointOf(m, fill) : null) ?? (item["at"] is JsonArray { Count: >= 2 } p ? Args.PointOf(p) : (Vec2?)null);
            var side = at is { } q ? NearestSide(sides, q) : null;
            var wasPlaced = (string?)item["fill"] != null;
            items.Add((JsonNode)new JsonObject
            {
                ["key"] = (string?)item["key"],
                ["kind"] = (string?)item["kind"],
                ["typeId"] = fill != null ? m.RelatingOf(fill.Id, IfcClass.RelDefinesByType)?.Id : (string?)item["type"],
                ["side"] = side?.Index,
                ["offset"] = side != null && at is { } r ? Vec2.Dot(r - side.Mid, side.Dir) : null,
                ["sill"] = Json.Number(item["sill"]),
                ["fill"] = fill?.Id,
                ["fillName"] = fill?.Name,
                ["gone"] = wasPlaced && fill == null,
            });
        }
        return new JsonObject
        {
            ["on"] = IsOn(room),
            ["sides"] = new JsonArray([.. sides.Select(s => (JsonNode)new JsonObject
            {
                ["index"] = s.Index,
                ["a"] = new JsonArray(s.A.X, s.A.Y),
                ["b"] = new JsonArray(s.B.X, s.B.Y),
                ["length"] = s.Length,
                ["wall"] = WallAt(m, room, s, s.Mid)?.Name,
            })]),
            ["items"] = items,
        };
    }

    public static void Register(IModuleRegistry r)
    {
        r.Command("setRoomOpenings", "Room's doors and windows", (ctx, a) =>
        {
            var room = Spatial.Require(ctx.Model, (string?)a["id"], Rooms.Class);
            Set(ctx, room, a["on"] is JsonValue v && v.TryGetValue<bool>(out var on) ? on : IsOn(room), a["items"] as JsonArray);
            return null;
        });
        r.Query("roomOpenings", (q, a) => Read(Spatial.Require(q.Model, (string?)a["id"], Rooms.Class), q.Model, q.Services));
    }
}
