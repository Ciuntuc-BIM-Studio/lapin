using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Rooms (IfcSpace, docs/rooms.md): an area of a storey with a name, a number,
/// a function and a department. Placed by a click inside walls, a room keeps
/// the point (<c>seed</c>) and finds its outline from the walls, curtain walls
/// and columns around it each time it is built (<see cref="RoomBounds"/>), so
/// it follows them; when nothing closes round its point any more it keeps the
/// outline it last stored and says so. Drawn as an outline, a room keeps it.
/// Its body (the outline up the room's height) is what IFC gets; plans draw a
/// room by its colour fill and its stamp, never as a body, and the 3D view
/// leaves rooms out until its graphics show them.
/// </summary>
public static class Rooms
{
    public const string Class = IfcClass.Space;
    public const string Color = "#9ec5e8";

    /// <summary>The functions offered first (any text is taken).</summary>
    public static readonly string[] Functions = ["Living", "Bedroom", "Kitchen", "Dining", "Bath", "WC", "Circulation", "Storage", "Technical", "Office", "Balcony", "Garage"];

    /// <summary>A room's outline as built: the outer loop, its islands, and whether it is the stored one because nothing closes round its point.</summary>
    public sealed record Outline(IReadOnlyList<Vec2> Outer, IReadOnlyList<IReadOnlyList<Vec2>> Islands, bool Lost)
    {
        public double GrossArea => Math.Abs(Polygon2.SignedArea(Outer));
        public double NetArea => GrossArea - Islands.Sum(i => Math.Abs(Polygon2.SignedArea(i)));
        public double Perimeter => Polygon2.Perimeter(Outer);
        public double NetPerimeter => Perimeter + Islands.Sum(Polygon2.Perimeter);
        public IReadOnlyList<IReadOnlyList<Vec2>> Loops => [Outer, .. Islands];

        /// <summary>Whether a point is in the room: in its outline, not in an island.</summary>
        public bool Holds(Vec2 p) => Polygon2.Contains(Outer, p) && !Islands.Any(i => Polygon2.Contains(i, p));
    }

    // ─── Attributes ──────────────────────────────────────────────────────

    static Vec2? Pt(JsonNode? n) => n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : null;
    static IReadOnlyList<Vec2> Loop(JsonNode? n) => n is JsonArray arr ? arr.Select(Pt).OfType<Vec2>().ToList() : [];
    public static JsonArray Json2(IEnumerable<Vec2> pts) => new(pts.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 6), Math.Round(p.Y, 6))).ToArray());
    static JsonArray Json2(IEnumerable<IReadOnlyList<Vec2>> loops) => new(loops.Select(l => (JsonNode)Json2(l)).ToArray());

    public static Vec2? Seed(Entity room) => Pt(room.Attrs["seed"]);
    public static Vec2? StampAt(Entity room) => Pt(room.Attrs["stampAt"]);

    /// <summary>The outline the room stores: drawn, or the last one found from its walls.</summary>
    public static Outline? Stored(Entity room)
    {
        var outer = Loop(room.Attrs["contour"]);
        if (outer.Count < 3) return null;
        var islands = room.Attrs["islands"] is JsonArray isl ? isl.Select(Loop).Where(l => l.Count >= 3).ToList() : [];
        return new Outline(outer, islands, false);
    }

    /// <summary>
    /// The room's outline now: found round its point from the walls (a room
    /// placed by a click), else the one it stores (drawn, or the last found,
    /// then <see cref="Outline.Lost"/>).
    /// </summary>
    public static Outline? Live(Entity room, IModelView m, IServices? services)
    {
        var stored = Stored(room);
        if (!room.Bool("auto") || Seed(room) is not { } seed || services == null || Spatial.StoreyOf(m, room.Id) is not { } storey) return stored;
        var (region, _) = RoomBounds.At(m, services, storey.Id, seed);
        if (region != null) return new Outline(region.Outer, region.Islands, false);
        return stored == null ? null : stored with { Lost = true };
    }

    /// <summary>The outline a built geometry carries (its plan items), when it has one.</summary>
    public static Outline? OfGeometry(ElementGeometry? g)
    {
        if (g?.Plan is not { } plan || plan.FirstOrDefault(p => p.Kind == "room") is not { } outer) return null;
        return new Outline(outer.Points, plan.Where(p => p.Kind == "roomIsland").Select(p => p.Points).ToList(), g.Diagnostics?.Any(d => d.StartsWith(LostNote, StringComparison.Ordinal)) == true);
    }

    /// <summary>The room's outline as its geometry has it (the cache the views use), else as it stores it.</summary>
    public static Outline? Of(Entity room, IModelView m, Func<string, ElementGeometry?>? geometry) => OfGeometry(geometry?.Invoke(room.Id)) ?? Stored(room);

    /// <summary>The room's height: its own, else its storey's, else 3 m.</summary>
    public static double HeightOf(Entity room, IModelView m)
    {
        if (room.NumOrNull("height") is { } h && h > 0) return h;
        return Spatial.StoreyOf(m, room.Id)?.NumOrNull("height") is { } sh && sh > 0 ? sh : 3.0;
    }

    const string LostNote = "Nothing closes round the room's point";

    // ─── Geometry and quantities ─────────────────────────────────────────

    /// <summary>The room's body (its outline up its height, from its storey's level), its outline and its stamp's point as plan items.</summary>
    public static ElementGeometry? Geometry(Entity room, IModelView m, IServices services)
    {
        var o = Live(room, m, services);
        if (o == null) return new ElementGeometry([], Diagnostics: ["The room has no outline: place it inside walls, or draw it."]);
        var z = Spatial.StoreyOf(m, room.Id)?.Num("elevation") ?? 0;
        var h = HeightOf(room, m);
        var solid = Prism.Extrude(o.Outer.Select(p => p.At(z)).ToArray(), o.Islands.Select(i => (IReadOnlyList<Vec3>)i.Select(p => p.At(z)).ToArray()).ToList(), new Vec3(0, 0, h), "room");
        var plan = new List<PlanItem> { new("room", true, o.Outer) };
        plan.AddRange(o.Islands.Select(i => new PlanItem("roomIsland", true, i)));
        plan.Add(new PlanItem("roomStamp", false, [StampPoint(room, o)]));
        var diagnostics = o.Lost ? new[] { $"{LostNote} any more: it keeps the outline it had. Close it with walls, move its point (or redraw it)." } : null;
        return new ElementGeometry(solid == null ? [] : [new GeometryPart("Room", Color, solid, Key: "room")], plan, [.. o.Outer], diagnostics);
    }

    /// <summary>IFC's base quantities of a space, from the outline: floor areas net (without the islands) and gross, perimeters, height and volumes.</summary>
    public static IEnumerable<QuantitySet> Quantities(Entity room, ElementGeometry? g, IModelView m)
    {
        if ((OfGeometry(g) ?? Stored(room)) is not { } o) yield break;
        var h = HeightOf(room, m);
        yield return new QuantitySet("Qto_SpaceBaseQuantities",
        [
            new("NetFloorArea", o.NetArea, "AREA", "Net floor area"),
            new("GrossFloorArea", o.GrossArea, "AREA", "Gross floor area"),
            new("GrossPerimeter", o.Perimeter, "LENGTH", "Perimeter"),
            new("NetPerimeter", o.NetPerimeter, "LENGTH", "Net perimeter"),
            new("Height", h, "LENGTH", "Height"),
            new("NetVolume", o.NetArea * h, "VOLUME", "Net volume"),
            new("GrossVolume", o.GrossArea * h, "VOLUME", "Gross volume"),
        ]);
    }

    /// <summary>
    /// Where the stamp goes: where the user put it, else the outline's centre
    /// when it is in the room, else the middle of the longest run across the
    /// room through that centre, else the room's point.
    /// </summary>
    public static Vec2 StampPoint(Entity room, Outline o)
    {
        if (StampAt(room) is { } at) return at;
        var area = Polygon2.SignedArea(o.Outer);
        Vec2 c;
        if (Math.Abs(area) > 1e-12)
        {
            double cx = 0, cy = 0;
            for (var i = 0; i < o.Outer.Count; i++)
            {
                var (p, q) = (o.Outer[i], o.Outer[(i + 1) % o.Outer.Count]);
                var k = p.X * q.Y - q.X * p.Y;
                cx += (p.X + q.X) * k; cy += (p.Y + q.Y) * k;
            }
            c = new Vec2(cx / (6 * area), cy / (6 * area));
        }
        else c = new Vec2(o.Outer.Average(p => p.X), o.Outer.Average(p => p.Y));
        if (o.Holds(c)) return c;
        // The longest stretch of the room along the horizontal line through the centre.
        var rings = o.Loops.Select(l => l.ToArray()).ToList();
        var minX = o.Outer.Min(p => p.X) - 1; var maxX = o.Outer.Max(p => p.X) + 1;
        var spans = new List<(double, double)>();
        Polygon2.InsideIntervals(rings, new Vec2(minX, c.Y), new Vec2(maxX, c.Y), 0, 1, spans);
        if (spans.Count > 0)
        {
            var (t0, t1) = spans.MaxBy(s => s.Item2 - s.Item1);
            return new Vec2(minX + (maxX - minX) * (t0 + t1) / 2, c.Y);
        }
        return Seed(room) ?? o.Outer[0];
    }

    // ─── Making them ─────────────────────────────────────────────────────

    /// <summary>A drawn outline: three distinct corners or more, counter-clockwise; refused when it encloses nothing.</summary>
    public static IReadOnlyList<Vec2> CheckOutline(IReadOnlyList<Vec2> pts)
    {
        var clean = new List<Vec2>();
        foreach (var p in pts)
            if (clean.Count == 0 || (p - clean[^1]).Length > 1e-6) clean.Add(p);
        if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 1e-6) clean.RemoveAt(clean.Count - 1);
        if (clean.Count < 3) throw new CommandException("A room's outline needs at least three corners.");
        if (Math.Abs(Polygon2.SignedArea(clean)) < 1e-4) throw new CommandException("The room's outline encloses nothing.");
        return Polygon2.EnsureCcw(clean).ToList();
    }

    /// <summary>The next free number on a storey: its place among the storeys, then two digits (101, 102… on the second storey from the bottom).</summary>
    public static string NextNumber(IModelView m, string storeyId)
    {
        var storeys = Spatial.Storeys(m).ToList();
        var prefix = Math.Max(0, storeys.FindIndex(s => s.Id == storeyId)).ToString(CultureInfo.InvariantCulture);
        var taken = m.OfClass(Class).Select(r => r.Str("number")).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var n = 1; ; n++)
        {
            var candidate = prefix + n.ToString("00", CultureInfo.InvariantCulture);
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    /// <summary>The room already standing at a point of a storey (as the views have it), if any.</summary>
    static Entity? RoomAt(ICommandContext ctx, string storeyId, Vec2 p, string? except = null) =>
        ctx.Model.OfClass(Class).FirstOrDefault(r => r.Id != except && Spatial.StoreyOf(ctx.Model, r.Id)?.Id == storeyId
            && (OfGeometry(ctx.Query.Geometry(r.Id)) ?? Stored(r)) is { } o && o.Holds(p));

    /// <summary>The region round a point of a storey, refused with the reason when there is none or a room has it already.</summary>
    static FreeRegion Region(ICommandContext ctx, string storeyId, Vec2 p, string? except = null)
    {
        if (RoomAt(ctx, storeyId, p, except) is { } there) throw new CommandException($"There is a room here already: „{there.Name}” ({there.Str("number")}).");
        var (region, problem) = RoomBounds.At(ctx.Model, ctx.Services, storeyId, p);
        return region ?? throw new CommandException(problem ?? "No room can be placed there.");
    }

    public static Entity Add(ICommandContext ctx, JsonObject a)
    {
        var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
        Spatial.Require(ctx.Model, storeyId, IfcClass.BuildingStorey);
        var attrs = new JsonObject();
        if (a["points"] != null || a["x1"] != null)
        {
            attrs["contour"] = Json2(CheckOutline(Args.Points(a)));
            attrs["auto"] = false;
        }
        else
        {
            var p = Args.Point(a, "x", "y");
            var region = Region(ctx, storeyId, p);
            attrs["auto"] = true;
            attrs["seed"] = new JsonArray(p.X, p.Y);
            attrs["contour"] = Json2(region.Outer);
            if (region.Islands.Count > 0) attrs["islands"] = Json2(region.Islands);
        }
        attrs["number"] = ((string?)a["number"])?.Trim() is { Length: > 0 } num ? num : NextNumber(ctx.Model, storeyId);
        foreach (var k in new[] { "function", "department", "finishFloor" })
            if (((string?)a[k])?.Trim() is { Length: > 0 } v) attrs[k] = v;
        if (Json.Number(a["height"]) is { } h && h > 0) attrs["height"] = h;
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : (string?)attrs["function"] ?? "Room";
        var room = new Entity(ctx.NewId(), Class, name, attrs);
        ctx.Add(room);
        Spatial.Contain(ctx, storeyId, room.Id);
        return room;
    }

    /// <summary>
    /// A room's outline or stamp changed: a drawn outline (it keeps it), a new
    /// point to find its outline round, the stamp moved (or put back with
    /// <c>stamp: null</c>).
    /// </summary>
    public static void Update(ICommandContext ctx, Entity room, JsonObject a)
    {
        var attrs = room.CloneAttrs();
        var storeyId = Spatial.StoreyOf(ctx.Model, room.Id)?.Id ?? throw new CommandException("The room stands on no storey.");
        if (a["points"] != null || a["x1"] != null)
        {
            attrs["contour"] = Json2(CheckOutline(Args.Points(a)));
            attrs["auto"] = false;
            attrs.Remove("seed");
            attrs.Remove("islands");
        }
        else if (a["x"] != null || a["y"] != null)
        {
            var p = Args.Point(a, "x", "y");
            var region = Region(ctx, storeyId, p, room.Id);
            attrs["auto"] = true;
            attrs["seed"] = new JsonArray(p.X, p.Y);
            attrs["contour"] = Json2(region.Outer);
            if (region.Islands.Count > 0) attrs["islands"] = Json2(region.Islands); else attrs.Remove("islands");
        }
        if (a["stampX"] != null || a["stampY"] != null) attrs["stampAt"] = new JsonArray(Args.Number(a, "stampX"), Args.Number(a, "stampY"));
        else if (a.ContainsKey("stamp") && a["stamp"] == null) attrs.Remove("stampAt");
        ctx.Replace(room.With(attrs: attrs));
    }

    /// <summary>The outlines found from the walls stored on the rooms placed by a click (what they keep should their walls go): how many, and how many found nothing round their point.</summary>
    public static (int Stored, int Lost) Store(ICommandContext ctx, IEnumerable<Entity> rooms)
    {
        var (stored, lost) = (0, 0);
        foreach (var room in rooms.ToList())
        {
            if (!room.Bool("auto") || Seed(room) is not { } seed || Spatial.StoreyOf(ctx.Model, room.Id) is not { } storey) continue;
            var (region, _) = RoomBounds.At(ctx.Model, ctx.Services, storey.Id, seed);
            if (region == null) { lost++; continue; }
            var attrs = room.CloneAttrs();
            attrs["contour"] = Json2(region.Outer);
            if (region.Islands.Count > 0) attrs["islands"] = Json2(region.Islands); else attrs.Remove("islands");
            if (attrs.ToJsonString() != room.Attrs.ToJsonString()) { ctx.Replace(room.With(attrs: attrs)); stored++; }
        }
        return (stored, lost);
    }

    // ─── In plans ────────────────────────────────────────────────────────

    /// <summary>An area as a stamp writes it: square metres to the hundredth, or square feet to the tenth in an imperial project.</summary>
    public static string FormatArea(double m2, string units) =>
        units is "ft-in" or "in" ? (m2 * 10.763910417).ToString("0.0", CultureInfo.InvariantCulture) + " ft²" : m2.ToString("0.00", CultureInfo.InvariantCulture) + " m²";

    static string FormatLength(double m, string units) => units is "ft-in" or "in" ? Standards.FormatImperial(m, units == "ft-in", 2) : m.ToString("0.00", CultureInfo.InvariantCulture) + " m";

    /// <summary>What a stamp's template can read of a room besides its attributes: {area}, {grossArea}, {perimeter}, {height}, {volume}.</summary>
    public static Dictionary<string, string> Values(Entity room, Outline o, IModelView m, string units)
    {
        var h = HeightOf(room, m);
        return new()
        {
            ["area"] = FormatArea(o.NetArea, units),
            ["grossArea"] = FormatArea(o.GrossArea, units),
            ["perimeter"] = FormatLength(o.Perimeter, units),
            ["height"] = FormatLength(h, units),
            ["volume"] = units is "ft-in" or "in" ? (o.NetArea * h * 35.314666721).ToString("0.0", CultureInfo.InvariantCulture) + " ft³" : (o.NetArea * h).ToString("0.00", CultureInfo.InvariantCulture) + " m³",
        };
    }

    /// <summary>
    /// A plan's rooms: on its storey, each filled as the view's colour scheme
    /// (or the room's own colour) says, under the walls, with its stamp over
    /// them: the room's tag type's lines (name, number, area by default),
    /// centred on its stamp point. A click on the stamp picks the room.
    /// </summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.Kind != "plan") yield break;
        var m = c.Model;
        var storeyId = c.View.Str("storeyId");
        var hiddenElements = (c.View.Attrs["hiddenElements"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        var graphics = GraphicsSets.Of(m, c.View);
        var scheme = ColorSchemes.OfView(m, c.View);
        var patterns = DraftingResources.Patterns(m);
        var type = Standards.TagTypeOf(c.Book, null, Class) is { Shape: "room" } t ? t : Standards.DefaultRoomTag;
        var style = Standards.TextOf(c.Book, type.TextStyle);
        foreach (var room in m.OfClass(Class))
        {
            if (hiddenElements.Contains(room.Id) || GraphicsSets.Hides(graphics, room) || Spatial.StoreyOf(m, room.Id)?.Id != storeyId) continue;
            if (Of(room, m, c.Geometry) is not { } o) continue;
            if (scheme != null && ColorSchemes.LookOf(scheme, room) is { } look && Fill(c, o, look, patterns) is { } fill) yield return fill;
            yield return Stamp(c, room, o, type, style);
        }
    }

    /// <summary>A room's fill and hatch, under the model's pens; null when it has neither.</summary>
    static ViewMark? Fill(ViewContext c, Outline o, ColorSchemes.Look look, IReadOnlyList<HatchPattern> patterns)
    {
        var mark = new ViewMark(null) { Under = true };
        var loops = o.Loops;
        if (look.Color != null) mark.Fills.Add((loops, look.Color));
        if (look.Hatch != null && HatchLibrary.Find(look.Hatch, patterns) is { } pattern)
        {
            if (pattern.Solid) mark.Fills.Add((loops, look.HatchColor));
            else
            {
                var (segs, _) = HatchFill.Segments(loops, pattern, new HatchOptions(look.HatchScale * c.Mm, 0, null, 40_000));
                foreach (var s in segs) mark.Line(s.A, s.B, "ROOM-HATCH", Pens.Default.Hatch, look.HatchColor);
            }
        }
        return mark.Fills.Count > 0 || mark.Lines.Count > 0 ? mark : null;
    }

    static ViewMark Stamp(ViewContext c, Entity room, Outline o, TagType type, TextStyle style)
    {
        var at = StampPoint(room, o);
        var lines = TagText.Lines(type.Template, c.Model, room, Values(room, o, c.Model, c.Book.Units));
        var h = style.Height * c.Mm;
        var step = h * 1.5;
        var color = o.Lost ? "#c0392b" : style.Color ?? "#000000";
        var mark = new ViewMark(room.Id);
        var top = at.Y + step * (lines.Count - 1) / 2;
        var width = 0.0;
        for (var i = 0; i < lines.Count; i++)
        {
            var y = top - step * i;
            mark.Text(new PenText(new Vec2(at.X, y), lines[i], style.Height, 0, "middle", "middle", style.Bold || i == 0, style.Font), color);
            var w = lines[i].Length * h * 0.6;
            width = Math.Max(width, w);
            mark.Line(new Vec2(at.X - w / 2, y), new Vec2(at.X + w / 2, y), "PICK", 0);
        }
        // What a click takes: the box round the text.
        var (x0, x1, y0, y1) = (at.X - width / 2 - h * 0.3, at.X + width / 2 + h * 0.3, top - step * (lines.Count - 1) - h * 0.7, top + h * 0.7);
        mark.Polyline([new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)], true, "PICK", 0);
        mark.Snaps.Add(at);
        return mark;
    }
}

/// <summary>The room schedule: every room with its storey, number, name, function, department, net area, perimeter, height and volume, and the total area.</summary>
public sealed class RoomSchedule : IScheduleSource
{
    public string Kind => "rooms";
    public string Label => "Room schedule";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        var storeys = Spatial.Storeys(m).Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var rows = new List<IReadOnlyList<string>>();
        double total = 0;
        string N(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        foreach (var r in m.OfClass(Rooms.Class)
                     .Select(r => (Room: r, Storey: Spatial.StoreyOf(m, r.Id)))
                     .OrderBy(x => x.Storey == null ? int.MaxValue : storeys.GetValueOrDefault(x.Storey.Id))
                     .ThenBy(x => x.Room.Str("number") ?? "", StringComparer.OrdinalIgnoreCase))
        {
            if (Rooms.Of(r.Room, m, q.Geometry) is not { } o) continue;
            var h = Rooms.HeightOf(r.Room, m);
            total += o.NetArea;
            rows.Add([r.Storey?.Name ?? "", r.Room.Str("number") ?? "", r.Room.Name, r.Room.Str("function") ?? "", r.Room.Str("department") ?? "", N(o.NetArea), N(o.Perimeter), N(h), N(o.NetArea * h)]);
        }
        if (rows.Count > 0) rows.Add(["Total", "", "", "", "", N(total), "", "", ""]);
        return new ScheduleGrid(Label, ["Storey", "Number", "Name", "Function", "Department", "Area (m²)", "Perimeter (m)", "Height (m)", "Volume (m³)"], rows);
    }
}
