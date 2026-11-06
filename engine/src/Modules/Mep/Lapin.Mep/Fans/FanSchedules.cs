using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep.Fans;

/// <summary>Where each fan is: the room whose outline holds a point just in front of it (its face's room), on its storey.</summary>
static class FanRooms
{
    public static List<(Entity Room, string? Storey, Rooms.Outline Outline)> Of(IQueryContext q)
    {
        var m = q.Model;
        return m.OfClass(Rooms.Class).Select(r => (Room: r, Storey: Spatial.StoreyOf(m, r.Id)?.Id, Outline: Rooms.Of(r, m, id => q.Geometry(id))))
            .Where(x => x.Outline != null).Select(x => (x.Room, x.Storey, x.Outline!)).ToList();
    }

    /// <summary>The point the fan's room is found by: 100 mm in front of its face (the face itself is on the room's edge).</summary>
    public static Vec2 Probe(Entity fan)
    {
        var at = Args.PointOf(fan.Attrs["at"]);
        var rot = fan.Num("rotation") * Math.PI / 180;
        return at + new Vec2(Math.Sin(rot), -Math.Cos(rot)) * 0.1;
    }

    public static Entity? RoomOf(IModelView m, List<(Entity Room, string? Storey, Rooms.Outline Outline)> rooms, Entity fan)
    {
        var storey = Spatial.StoreyOf(m, fan.Id)?.Id;
        var p = Probe(fan);
        return rooms.FirstOrDefault(r => r.Storey == storey && r.Outline.Holds(p)).Room;
    }

    public static string Label(Entity? room) => room == null ? "" : room.Str("number") is { Length: > 0 } n ? $"{n} {room.Name}" : room.Name;

    public static string N(double v, string format = "0") => v.ToString(format, CultureInfo.InvariantCulture);
}

/// <summary>The fan schedule: a row per fan (its mark, room, kind, size, airflow, mounting, control, height, outlet), the total airflow.</summary>
public sealed class FanSchedule : IScheduleSource
{
    public string Kind => "fans";
    public string Label => "Fan schedule";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        var rooms = FanRooms.Of(q);
        var rows = new List<IReadOnlyList<string>>();
        var keys = new List<string?>();
        double total = 0;
        foreach (var fan in m.OfClass(ExhaustFans.Class).OrderBy(f => Spatial.StoreyOf(m, f.Id)?.Num("elevation") ?? 0).ThenBy(f => f.Name, StringComparer.Ordinal))
        {
            var s = ExhaustFans.SpecOf(m, fan);
            var size = s.Kind.Key == "louvrePanel" ? $"Ø{FanRooms.N(s.Diameter * 1000)}, panel {FanRooms.N(s.FaceSize.W * 1000)} × {FanRooms.N(s.FaceSize.H * 1000)}"
                : $"Ø{FanRooms.N(s.Diameter * 1000)}, grille {(s.Square ? $"{FanRooms.N(s.Face * 1000)} × {FanRooms.N(s.Face * 1000)}" : $"Ø{FanRooms.N(s.Face * 1000)}")}";
            total += s.Airflow;
            rows.Add([fan.Name, FanRooms.Label(FanRooms.RoomOf(m, rooms, fan)), s.Kind.Label, size, FanRooms.N(s.Airflow), s.Mounting, s.Control, FanRooms.N(fan.Num("height") * 1000),
                fan.Str("ductId") != null && m.Get(fan.Str("ductId")!) != null ? fan.Str("outlet") ?? "wall" : s.Mounting == "wall" ? "through the wall" : ""]);
            keys.Add(fan.Id);
        }
        if (rows.Count > 0) { rows.Add(["Total", "", $"{rows.Count} pcs", "", FanRooms.N(total), "", "", "", ""]); keys.Add(null); }
        return new ScheduleGrid(Label, ["Mark", "Room", "Kind", "Size (mm)", "Airflow (m³/h)", "Mounting", "Control", "Height (mm)", "Outlet"], rows, keys);
    }
}

/// <summary>
/// The rooms' extract check (report #55, optional): for each bathroom, WC and kitchen, and each room with a fan, its
/// volume, the extract it needs and what its fans give. Needed: the room's volume times its air changes (a bathroom 8 an
/// hour, a WC 6, a kitchen 6, another room with a fan 4), at least the usual minimum (a bathroom 54 m³/h, 15 l/s; a WC
/// 25 m³/h; a kitchen 108 m³/h, 30 l/s). Indicative values, to check against the norm the project follows.
/// </summary>
public sealed class FanRoomCheck : IScheduleSource
{
    public string Kind => "fanRooms";
    public string Label => "Room extract check";

    public static (double AirChanges, double Minimum) Rule(string? function) => function switch
    {
        "Bath" => (8, 54),
        "WC" => (6, 25),
        "Kitchen" => (6, 108),
        _ => (4, 0),
    };

    /// <summary>A room's needed extract (m³/h) for its volume and function.</summary>
    public static double Required(double volume, string? function)
    {
        var (ach, min) = Rule(function);
        return Math.Max(min, ach * volume);
    }

    static bool Wet(Entity room)
    {
        var f = room.Str("function");
        if (f is "Bath" or "WC" or "Kitchen") return true;
        var n = room.Name.ToLowerInvariant();
        return n.Contains("bath") || n.Contains("baie") || n.Contains("wc") || n.Contains("toilet") || n.Contains("shower") || n.Contains("dus");
    }

    static string? Function(Entity room)
    {
        if (room.Str("function") is { Length: > 0 } f) return f;
        var n = room.Name.ToLowerInvariant();
        return n.Contains("bath") || n.Contains("baie") || n.Contains("shower") || n.Contains("dus") ? "Bath" : n.Contains("wc") || n.Contains("toilet") ? "WC" : n.Contains("kitchen") || n.Contains("bucat") ? "Kitchen" : null;
    }

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        var rooms = FanRooms.Of(q);
        var byRoom = new Dictionary<string, double>();
        foreach (var fan in m.OfClass(ExhaustFans.Class))
            if (FanRooms.RoomOf(m, rooms, fan) is { } room) byRoom[room.Id] = byRoom.GetValueOrDefault(room.Id) + ExhaustFans.SpecOf(m, fan).Airflow;
        var rows = new List<IReadOnlyList<string>>();
        var keys = new List<string?>();
        foreach (var (room, _, outline) in rooms.OrderBy(r => r.Room.Str("number") ?? r.Room.Name, StringComparer.Ordinal))
        {
            if (!Wet(room) && !byRoom.ContainsKey(room.Id)) continue;
            var volume = outline.NetArea * Rooms.HeightOf(room, m);
            var function = Function(room);
            var need = Required(volume, function);
            var given = byRoom.GetValueOrDefault(room.Id);
            rows.Add([FanRooms.Label(room), function ?? "", FanRooms.N(volume, "0.0"), FanRooms.N(Rule(function).AirChanges), FanRooms.N(need), FanRooms.N(given), given + 1e-6 >= need ? "OK" : given > 0 ? "Too little" : "No fan"]);
            keys.Add(room.Id);
        }
        return new ScheduleGrid(Label, ["Room", "Function", "Volume (m³)", "Air changes (1/h)", "Needed (m³/h)", "Fans (m³/h)", "Check"], rows, keys);
    }
}
