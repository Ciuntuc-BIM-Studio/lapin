using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Furniture;

/// <summary>Where each piece stands: the room (its number and name) whose outline holds its middle, on its storey.</summary>
static class FurnitureRooms
{
    public static Dictionary<string, Entity?> Of(IQueryContext q)
    {
        var m = q.Model;
        var rooms = m.OfClass(Rooms.Class).Select(r => (Room: r, Storey: Spatial.StoreyOf(m, r.Id)?.Id, Outline: Rooms.Of(r, m, id => q.Geometry(id)))).Where(x => x.Outline != null).ToList();
        var o = new Dictionary<string, Entity?>();
        foreach (var e in m.OfClass(FurnitureModule.Class))
        {
            var p = Args.PointOf(e.Attrs["at"]);
            var storey = Spatial.StoreyOf(m, e.Id)?.Id;
            o[e.Id] = rooms.FirstOrDefault(r => r.Storey == storey && r.Outline!.Holds(p)).Room;
        }
        return o;
    }

    public static string Label(Entity? room) => room == null ? "" : room.Str("number") is { Length: > 0 } n ? $"{n} {room.Name}" : room.Name;

    /// <summary>A kind's label; a kitchen's with its layout when it is not the straight one.</summary>
    public static string KindLabel(Spec s) => s.Kind.Key == "kitchen" && KitchenLayouts.Of(s) is var l && l != "one-wall" ? $"{s.Kind.Label} · {KitchenLayouts.Label(l)}" : s.Kind.Label;

    public static string Mm(double v) => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);

    public static string Size(Entity e, Spec s) => s.Kind.Key == "kitchen"
        ? $"{Mm(e.NumOrNull("length") ?? s.W)} × {Mm(KitchenLayouts.Depth(s))} × {Mm(s.H)}"
        : $"{Mm(s.W)} × {Mm(s.D)} × {Mm(s.H)}";
}

/// <summary>The furniture schedule: a row per type (a kitchen per length too), its kind, size, how many and the rooms they stand in; the total.</summary>
public sealed class FurnitureSchedule : IScheduleSource
{
    public string Kind => "furniture";
    public string Label => "Furniture schedule";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        var rooms = FurnitureRooms.Of(q);
        var groups = m.OfClass(FurnitureModule.Class)
            .Select(e => (E: e, T: FurnitureModule.TypeOf(m, e)))
            .GroupBy(x => (x.T?.Id ?? "", Size: FurnitureRooms.Size(x.E, new Spec(x.T?.Attrs["spec"] as JsonObject))))
            .Select(g =>
            {
                var s = new Spec(g.First().T?.Attrs["spec"] as JsonObject);
                var where = g.Select(x => FurnitureRooms.Label(rooms.GetValueOrDefault(x.E.Id))).Where(x => x.Length > 0).Distinct().OrderBy(x => x, StringComparer.Ordinal);
                return (Group: FurnitureKinds.All.ToList().IndexOf(s.Kind), Row: (IReadOnlyList<string>)[g.First().T?.Name ?? g.First().E.Name, FurnitureRooms.KindLabel(s), g.Key.Size, g.Count().ToString(CultureInfo.InvariantCulture), string.Join(", ", where)], Key: g.First().T?.Id);
            })
            .OrderBy(x => x.Group).ThenBy(x => x.Row[0], StringComparer.Ordinal).ToList();
        var rows = groups.Select(x => x.Row).ToList();
        var keys = groups.Select(x => x.Key).ToList();
        if (rows.Count > 0) { rows.Add(["Total", "", "", groups.Sum(x => int.Parse(x.Row[3], CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture), ""]); keys.Add(null); }
        return new ScheduleGrid(Label, ["Type", "Kind", "Size (mm)", "Count", "Rooms"], rows, keys);
    }
}

/// <summary>Furniture by room: each room (in its storey's order, then by number) with each type standing in it and how many; the pieces in no room last.</summary>
public sealed class FurnitureByRoom : IScheduleSource
{
    public string Kind => "furnitureRooms";
    public string Label => "Furniture by room";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        var rooms = FurnitureRooms.Of(q);
        var storeys = Spatial.Storeys(m).Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var rows = new List<IReadOnlyList<string>>();
        var byRoom = m.OfClass(FurnitureModule.Class).GroupBy(e => rooms.GetValueOrDefault(e.Id)?.Id ?? "")
            .OrderBy(g => g.Key == "" ? 1 : 0)
            .ThenBy(g => g.Key == "" ? 0 : storeys.GetValueOrDefault(Spatial.StoreyOf(m, g.Key)?.Id ?? "", 99))
            .ThenBy(g => g.Key == "" ? "" : m.Get(g.Key)?.Str("number") ?? "", StringComparer.Ordinal);
        foreach (var g in byRoom)
        {
            var room = g.Key == "" ? null : m.Get(g.Key);
            var label = room == null ? "(no room)" : FurnitureRooms.Label(room);
            var first = true;
            foreach (var t in g.GroupBy(e => FurnitureModule.TypeOf(m, e)?.Id ?? e.Id).OrderBy(t => t.First().Name, StringComparer.Ordinal))
            {
                var s = new Spec(FurnitureModule.TypeOf(m, t.First())?.Attrs["spec"] as JsonObject);
                rows.Add([first ? label : "", t.First().Name, FurnitureRooms.KindLabel(s), t.Count().ToString(CultureInfo.InvariantCulture)]);
                first = false;
            }
        }
        return new ScheduleGrid(Label, ["Room", "Type", "Kind", "Count"], rows);
    }
}

/// <summary>
/// The kitchen units (decision 472): for each kitchen, its units from the left as seen from its front, base line then
/// wall line, with their kind, width, height and front (the ones its type lays, and those the rest of its run is split into).
/// </summary>
public sealed class KitchenUnitsSchedule : IScheduleSource
{
    public string Kind => "kitchenUnits";
    public string Label => "Kitchen units";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        var rooms = FurnitureRooms.Of(q);
        var rows = new List<IReadOnlyList<string>>();
        var keys = new List<string?>();
        foreach (var e in m.OfClass(FurnitureModule.Class).OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            var s = new Spec(FurnitureModule.TypeOf(m, e)?.Attrs["spec"] as JsonObject);
            if (s.Kind.Key != "kitchen") continue;
            var b = new Builder(new Vec2(0, 0), 0, 0, e.Bool("mirror") ^ FurnitureBodies.Handed(s));
            FurnitureBodies.Build(b, s, e.NumOrNull("length"));
            var label = FurnitureRooms.Label(rooms.GetValueOrDefault(e.Id)) is { Length: > 0 } room ? $"{e.Name} ({room})" : e.Name;
            var first = true;
            foreach (var u in b.Units)
            {
                rows.Add([first ? label : "", u.Line, u.Index.ToString(CultureInfo.InvariantCulture), u.Kind, FurnitureRooms.Mm(u.Width), u.Height > 0 ? FurnitureRooms.Mm(u.Height) : "", u.Detail]);
                keys.Add(e.Id);
                first = false;
            }
        }
        return new ScheduleGrid(Label, ["Kitchen", "Line", "No.", "Unit", "Width (mm)", "Height (mm)", "Front / appliance"], rows, keys);
    }
}
