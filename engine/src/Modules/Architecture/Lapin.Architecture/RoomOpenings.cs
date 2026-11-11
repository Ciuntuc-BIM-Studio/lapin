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
/// a mirror reverses it, while the point is carried with the room, as are the way the door is hung (<c>hand</c>) and the side it
/// opens to (<c>face</c>), directions in the plan. Straight walls only. A room in an instance of a component (docs/plan-units.md)
/// has its list put right wherever the instance comes to stand, in whatever wall is there (<see cref="Settle"/>): the wall may
/// not be a member, so nothing else would bring the door along.
/// </summary>
/// <summary>A component's rooms keep their doors and windows with them.</summary>
sealed class RoomInstanceMembers : IInstanceMembers
{
    public void Edited(ICommandContext ctx, IEdit edit) => RoomOpenings.Edited(ctx, edit);

    public void Placed(ICommandContext ctx, Entity group)
    {
        foreach (var id in Groups.MembersOf(ctx.Model, group.Id).ToList())
            if (ctx.Model.Get(id) is { Class: Rooms.Class } room) RoomOpenings.Settle(ctx, room);
    }
}

public static class RoomOpenings
{
    public const string Key = "roomOpenings";
    public const string FillTag = "roomOpening";
    /// <summary>On the door or window: the entry's key, which is the same in every instance of a room.</summary>
    public const string KeyTag = "roomOpeningKey";

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

    static Entity Tagged(ICommandContext ctx, Entity fill, Entity room, string key)
    {
        if (fill.Str(FillTag) == room.Id && fill.Str(KeyTag) == key) return fill;
        var attrs = fill.CloneAttrs();
        attrs[FillTag] = room.Id;
        attrs[KeyTag] = key;
        var next = fill.With(attrs: attrs);
        ctx.Replace(next);
        return next;
    }

    /// <summary>The doors and windows tagged with a room.</summary>
    static List<Entity> Carried(IModelView m, Entity room) =>
        [.. m.OfClass(IfcClass.Door).Concat(m.OfClass(IfcClass.Window)).Where(f => f.Str(FillTag) == room.Id)];

    static JsonArray J(Vec2 v) => new(v.X, v.Y);

    /// <summary>The fill's own axes in the plan: where <c>flipHand</c> and <c>flipSide</c> leave them on this wall. A mirror carries these by itself.</summary>
    static (Vec2 Hand, Vec2 Face) Axes(Entity wall, Entity fill)
    {
        var d = (Walls.End(wall) - Walls.Start(wall)).Normalized();
        return (fill.Bool("flipHand") ? -d : d, fill.Bool("flipSide") ? -d.Perp : d.Perp);
    }

    /// <summary>The fill hung and turned as an entry says, on the wall it is in.</summary>
    static Entity Turned(ICommandContext ctx, Entity wall, Entity fill, Vec2 hand, Vec2 face)
    {
        var d = (Walls.End(wall) - Walls.Start(wall)).Normalized();
        var (flipHand, flipSide) = (Vec2.Dot(hand, d) < 0, Vec2.Dot(face, d.Perp) < 0);
        if (fill.Bool("flipHand") == flipHand && fill.Bool("flipSide") == flipSide) return fill;
        var attrs = fill.CloneAttrs();
        attrs["flipHand"] = flipHand;
        attrs["flipSide"] = flipSide;
        var next = fill.With(attrs: attrs);
        ctx.Replace(next);
        return next;
    }

    /// <summary>A point brought onto a side of the room, where it is nearest.</summary>
    static Vec2 OnSide(Side side, Vec2 p) => side.A + side.Dir * Math.Clamp(Vec2.Dot(p - side.A, side.Dir), 0, side.Length);

    /// <summary>The room's list as given, its doors and windows made, changed or taken away to match, in one step.</summary>
    public static void Set(ICommandContext ctx, Entity room, bool on, JsonArray? given) => Apply(ctx, room, on, given, false);

    /// <summary>
    /// An instance's room put right where it stands: an entry's door or window, found by its key, moved or made in the wall
    /// there, turned as the entry says; one with no wall to take it stays on the list without one (nothing is refused: a flat
    /// may be placed before its building is drawn), one no longer on the list goes.
    /// </summary>
    public static void Settle(ICommandContext ctx, Entity room)
    {
        if (room.Attrs[Key] == null) return;
        Apply(ctx, room, IsOn(room), null, true);
    }

    static void Apply(ICommandContext ctx, Entity room, bool on, JsonArray? given, bool settling)
    {
        var m = ctx.Model;
        var sides = Sides(room, m, ctx.Services);
        var before = Items(room).ToList();
        var wanted = given?.OfType<JsonObject>().ToList() ?? before;
        var carried = settling ? Carried(m, room) : [];
        var kept = new List<JsonNode>();
        var used = new HashSet<string>();
        Entity? Found(JsonObject item) => FillOf(m, room, item) ?? carried.FirstOrDefault(f => f.Str(KeyTag) == (string?)item["key"] && m.Get(f.Id) != null);

        if (on)
            foreach (var item in wanted)
            {
                var key = (string?)item["key"] is { Length: > 0 } k ? k : Guid.NewGuid().ToString("N")[..8];
                var kind = (string?)item["kind"] == "window" ? "window" : "door";
                var type = ((string?)item["typeId"] ?? (string?)item["type"]) is { } named ? m.Get(named) is { Class: IfcClass.DoorType or IfcClass.WindowType } t ? t : throw new CommandException("There is no such door or window type.")
                    : Openings.EnsureType(ctx, kind);
                var was = before.FirstOrDefault(b => (string?)b["key"] == key);
                Side? side;
                Vec2 at;
                if (Json.Number(item["side"]) is { } index)
                {
                    side = sides.FirstOrDefault(s => s.Index == (int)index) ?? throw new CommandException($"The room has no side {(int)index + 1}.");
                    at = side.Mid + side.Dir * (Json.Number(item["offset"]) ?? 0);
                }
                else
                {
                    at = item["at"] is JsonArray { Count: >= 2 } p ? Args.PointOf(p) : throw new CommandException("Say on which side of the room it goes.");
                    side = NearestSide(sides, at);
                    if (side == null && !settling) throw new CommandException($"„{room.Name}” has no outline yet.");
                    if (side != null) at = OnSide(side, at);
                }
                var entry = new JsonObject { ["key"] = key, ["kind"] = kind, ["type"] = type.Id, ["at"] = J(at) };
                var sill = Json.Number(item["sill"]);
                if (sill != null) entry["sill"] = sill;
                foreach (var d in new[] { "hand", "face" })
                    if ((item[d] ?? was?[d]) is JsonArray { Count: >= 2 } v) entry[d] = v.DeepClone();
                kept.Add(entry);

                var wall = side == null ? null : WallAt(m, room, side, at);
                if (wall == null)
                {
                    if (!settling) throw new CommandException($"Side {side!.Index + 1} of „{room.Name}” has no straight wall there to take it.");
                    continue;
                }
                try
                {
                    var fill = Place(ctx, room, wall, side!, type, key, at, sill, was is { } w ? Found(w) : Found(item), entry, settling);
                    used.Add(fill.Id);
                    var (hand, face) = Axes(wall, fill);
                    entry["hand"] = J(hand);
                    entry["face"] = J(face);
                    entry["fill"] = fill.Id;
                }
                catch (CommandException) when (settling) { }
            }
        else
            foreach (var item in wanted)
            {
                var entry = (JsonObject)item.DeepClone();
                if (Found(item) is { } fill && PointOf(m, fill) is { } here) entry["at"] = J(here);
                entry.Remove("fill");
                kept.Add(entry);
            }

        foreach (var item in before)
            if (Found(item) is { } fill && !used.Contains(fill.Id)) RemoveFill(ctx, fill);
        // Off the list since the room came here (an edit in another instance).
        foreach (var fill in carried)
            if (!used.Contains(fill.Id) && m.Get(fill.Id) != null) RemoveFill(ctx, fill);

        var now = m.Get(room.Id)!;
        var attrs = now.CloneAttrs();
        attrs[Key] = new JsonObject { ["on"] = on, ["items"] = new JsonArray([.. kept]) };
        if (!JsonNode.DeepEquals(attrs, now.Attrs)) ctx.Replace(now.With(attrs: attrs));
    }

    /// <summary>One entry's door or window in its wall: the one it has, moved or changed, or one already standing there (a copy), or a new one.</summary>
    static Entity Place(ICommandContext ctx, Entity room, Entity wall, Side side, Entity type, string key, Vec2 at, double? sill, Entity? fill, JsonObject entry, bool settling)
    {
        var m = ctx.Model;
        var along = Openings.AlongOf(wall, at);
        if (fill != null && Openings.OpeningOf(m, fill.Id) is { } opening && Openings.WallOf(m, opening.Id)?.Id != wall.Id)
        {
            RemoveFill(ctx, fill);
            fill = null;
        }
        var expected = type.Class == IfcClass.DoorType ? IfcClass.Door : IfcClass.Window;
        // One already there at this place (the room's copy, with its wall's own copy of the door): taken over, not doubled.
        fill ??= Openings.Of(m, wall.Id)
            .Where(o => Math.Abs(Openings.Place(m, o).Along - along) < 0.005)
            .Select(o => Openings.FillOf(m, o.Id))
            .FirstOrDefault(f => f?.Class == expected);
        var turning = entry["hand"] is JsonArray { Count: >= 2 } h && entry["face"] is JsonArray { Count: >= 2 } g ? (Args.PointOf(h), Args.PointOf(g)) : ((Vec2, Vec2)?)null;
        if (fill == null)
        {
            fill = Openings.AddFill(ctx, wall, type, along, sill);
            if (turning is { } axes) fill = Turned(ctx, wall, fill, axes.Item1, axes.Item2);
            else
            {
                // The room is the inside: a door opens into it, a window's exterior looks away from it.
                var inside = side.Mid + new Vec2(-side.Dir.Y, side.Dir.X) * 0.1;
                if (Vec2.Cross(Walls.End(wall) - Walls.Start(wall), inside - Walls.Start(wall)) < 0)
                {
                    Openings.Flip(ctx, fill, "y");
                    fill = m.Get(fill.Id)!;
                }
            }
        }
        else
        {
            if (m.RelatingOf(fill.Id, IfcClass.RelDefinesByType)?.Id != type.Id)
            {
                Openings.SetType(ctx, fill, type);
                fill = m.Get(fill.Id)!;
            }
            if (Openings.OpeningOf(m, fill.Id) is { } own && (Math.Abs(Openings.Place(m, own).Along - along) > 1e-6 || (sill is { } s && Math.Abs(Openings.Place(m, own).Sill + Openings.Place(m, own).FillOffset - s) > 1e-6)))
            {
                Openings.Move(ctx, fill, along, sill);
                fill = m.Get(fill.Id)!;
            }
            if (settling && turning is { } axes) fill = Turned(ctx, wall, fill, axes.Item1, axes.Item2);
        }
        return Tagged(ctx, fill, room, key);
    }

    /// <summary>
    /// What was done by hand to a door or window a room of an instance carries, written on its entry so that it is carried as an
    /// edit of the room: moved along its wall, turned, its sill or type changed; deleted, it leaves the list (unless its wall went
    /// with it). A copy of one, with a wall copied (the entry names the original), is the room's no longer.
    /// </summary>
    internal static void Edited(ICommandContext ctx, IEdit edit)
    {
        var m = ctx.Model;
        var look = new List<Entity>();
        foreach (var c in edit.EntityChanges)
        {
            var e = c.After ?? c.Before;
            if (e?.Class is IfcClass.Door or IfcClass.Window)
            {
                if (c.After == null) Deleted(ctx, edit, c.Before!);
                else if (c.Before == null) Copied(ctx, c.After);
                else look.Add(c.After);
            }
            else if (e?.Class == IfcClass.OpeningElement && c.After != null && c.Before != null && Openings.FillOf(m, c.Id) is { } fill) look.Add(fill);
        }
        foreach (var r in edit.RelationChanges)
            if ((r.After ?? r.Before)?.Class == IfcClass.RelDefinesByType)
                foreach (var id in (r.After?.Related ?? []).Except(r.Before?.Related ?? []))
                    if (m.Get(id) is { Class: IfcClass.Door or IfcClass.Window } fill && edit.EntityChanges.All(c => c.Id != id || c.Before != null)) look.Add(fill);
        foreach (var fill in look.DistinctBy(f => f.Id)) WriteBack(ctx, fill);
    }

    /// <summary>The room a door or window is tagged with, when that room is in an instance, and its entry.</summary>
    static (Entity Room, JsonObject Entry)? EntryOf(IModelView m, Entity fill)
    {
        if (fill.Str(FillTag) is not { } id || m.Get(id) is not { Class: Rooms.Class } room || Groups.Of(m, id) is not { } g || Components.Of(g) == null) return null;
        var entry = Items(room).FirstOrDefault(i => (string?)i["fill"] == fill.Id);
        return entry == null ? null : (room, entry);
    }

    static void Copied(ICommandContext ctx, Entity fill)
    {
        // The room's own (just made, or copied with the room, the entry naming the copy), in or out of an instance.
        if (fill.Str(FillTag) is not { } tag || ctx.Model.Get(tag) is not { Class: Rooms.Class } room || Items(room).Any(i => (string?)i["fill"] == fill.Id)) return;
        var attrs = fill.CloneAttrs();
        attrs.Remove(FillTag);
        attrs.Remove(KeyTag);
        ctx.Replace(fill.With(attrs: attrs));
    }

    static void Deleted(ICommandContext ctx, IEdit edit, Entity fill)
    {
        var m = ctx.Model;
        if (EntryOf(m, fill) is not { } found) return;
        var changes = edit.RelationChanges;
        var opening = changes.FirstOrDefault(r => r.Before is { Class: IfcClass.RelFillsElement } b && b.Related.Contains(fill.Id))?.Before!.Relating;
        var wall = changes.FirstOrDefault(r => r.Before is { Class: IfcClass.RelVoidsElement } b && b.Related.Contains(opening ?? ""))?.Before!.Relating;
        if (wall == null || m.Get(wall) == null) return;
        var now = found.Room;
        var attrs = now.CloneAttrs();
        var items = (JsonArray)attrs[Key]!["items"]!;
        items.Remove(items.OfType<JsonObject>().First(i => (string?)i["fill"] == fill.Id));
        ctx.Replace(now.With(attrs: attrs));
    }

    static void WriteBack(ICommandContext ctx, Entity fill)
    {
        var m = ctx.Model;
        if (EntryOf(m, fill) is not { } found || Openings.OpeningOf(m, fill.Id) is not { } opening || Openings.WallOf(m, opening.Id) is not { } wall) return;
        var attrs = found.Room.CloneAttrs();
        var entry = ((JsonArray)attrs[Key]!["items"]!).OfType<JsonObject>().First(i => (string?)i["fill"] == fill.Id);
        var was = entry.ToJsonString();
        var place = Openings.Place(m, opening);
        if (entry["at"] is not JsonArray { Count: >= 2 } at || Math.Abs(Openings.AlongOf(wall, Args.PointOf(at)) - place.Along) > 1e-3)
            if (PointOf(m, fill) is { } here) entry["at"] = J(here);
        var (hand, face) = Axes(wall, fill);
        entry["hand"] = J(hand);
        entry["face"] = J(face);
        if (m.RelatingOf(fill.Id, IfcClass.RelDefinesByType) is { } type) entry["type"] = type.Id;
        var sill = place.Sill + place.FillOffset;
        var given = Json.Number(entry["sill"]) ?? Openings.SpecOf(m, fill.Id)?.SillHeight;
        if (given is not { } g || Math.Abs(g - sill) > 1e-6) entry["sill"] = sill;
        if (entry.ToJsonString() != was) ctx.Replace(found.Room.With(attrs: attrs));
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
                ["noWall"] = IsOn(room) && !wasPlaced,
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
