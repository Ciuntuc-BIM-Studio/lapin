using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>A piece of a finish layer up its height (decision 265): a material (empty: nothing there) as high as it says, of its own thickness when it has one.</summary>
public sealed record FinishPart(string Material, double Height, double? Thickness = null);

/// <summary>A layer of a room's finish: a material and a thickness; on the walls, its pieces up the height, the rest in its own material.</summary>
public sealed record FinishLayer(string Material, double Thickness, List<FinishPart>? Parts = null);

/// <summary>The structural slab a room's finish may lay under its floor: material, thickness, how far past the room's outline it goes (under the walls).</summary>
public sealed record FinishSlab(string Material = "Reinforced concrete", double Thickness = 0.15, double Extend = 0);

/// <summary>
/// How a finish's floor (or ceiling, in a plan seen from below) is hatched in plans (decision 299): <c>material</c> (the default)
/// the face material's surface pattern (its own Elevation hatch in Manage → Materials, else one its name suggests: tiles, wood,
/// carpet, vinyl, concrete; else diagonal lines); <c>own</c> the pattern, scale, angle and colour given here; <c>none</c>.
/// </summary>
public sealed record FinishHatch(string Mode = "material", string? Pattern = null, double Scale = 1, double Angle = 0, string? Color = null)
{
    public static readonly string[] Modes = ["material", "own", "none"];
}

/// <summary>
/// What a room is finished with (decision 265): the floor's layers from the top down, the walls' from the wall's face into the room
/// (each with its pieces up the height: tiles to 2.10 m, paint over them), the ceiling's from the slab down; how high the walls'
/// go (null: up to the ceiling); a skirting type by name; whether the floor runs on through the doors to the wall's axis; and
/// a structural slab under the floor, made a floor of its own.
/// </summary>
public sealed record RoomFinishSpec(string Name, List<FinishLayer>? Floor = null, List<FinishLayer>? Walls = null, List<FinishLayer>? Ceiling = null,
    double? WallHeight = null, string? Skirting = null, bool Thresholds = true, FinishSlab? Slab = null,
    FinishHatch? FloorHatch = null, FinishHatch? CeilingHatch = null)
{
    static FinishLayer L(string material, double t, params FinishPart[] parts) => new(material, t, parts.Length > 0 ? [.. parts] : null);

    /// <summary>The ready-made finishes the configurator offers.</summary>
    public static readonly RoomFinishSpec[] Library =
    [
        new("Living room", [L("Laminate", 0.008), L("Underlay", 0.002), L("Screed", 0.05)], [L("Plaster", 0.015), L("Paint", 0.001)], [L("Plaster", 0.01), L("Paint", 0.001)], Skirting: "MDF 80 bevelled"),
        new("Bedroom", [L("Parquet", 0.015), L("Underlay", 0.003), L("Screed", 0.05)], [L("Plaster", 0.015), L("Paint", 0.001)], [L("Plaster", 0.01), L("Paint", 0.001)], Skirting: "Oak 70 moulded"),
        new("Bathroom", [L("Ceramic tiles", 0.01), L("Tile adhesive", 0.005), L("Waterproofing", 0.002), L("Screed", 0.05)],
            [L("Plaster", 0.015), L("Paint", 0.001, new FinishPart("Ceramic tiles", 2.1, 0.013))], [L("Plaster", 0.01), L("Paint", 0.001)], Thresholds: false),
        new("Kitchen", [L("Ceramic tiles", 0.01), L("Tile adhesive", 0.005), L("Screed", 0.05)],
            [L("Plaster", 0.015), L("Paint", 0.001, new FinishPart("Paint", 0.9), new FinishPart("Ceramic tiles", 0.6, 0.01))], [L("Plaster", 0.01), L("Paint", 0.001)], Skirting: "Tile 80 flush"),
        new("Ground floor on slab", [L("Ceramic tiles", 0.01), L("Tile adhesive", 0.005), L("Screed", 0.05), L("Insulation", 0.1)],
            [L("Plaster", 0.015), L("Paint", 0.001)], [L("Plaster", 0.01), L("Paint", 0.001)], Skirting: "Tile 80 flush", Slab: new FinishSlab("Reinforced concrete", 0.15, 0)),
        new("Technical room", [L("Epoxy coating", 0.003), L("Screed", 0.05)], [L("Paint", 0.001)], null),
    ];

    public static RoomFinishSpec Default(string name = "Room finish") => Library[0] with { Name = name };

    public JsonObject ToJson() => (JsonObject)JsonSerializer.SerializeToNode(this, ArchitectureJson.Default.RoomFinishSpec)!;

    /// <summary>A spec as a command gives it, checked: every layer a material and a thickness, every piece a height.</summary>
    public static RoomFinishSpec From(JsonNode? node)
    {
        RoomFinishSpec? s;
        try { s = node?.Deserialize(ArchitectureJson.Default.RoomFinishSpec); }
        catch (JsonException) { throw new CommandException("The room finish cannot be read."); }
        if (s is null || string.IsNullOrWhiteSpace(s.Name)) throw new CommandException("A room finish needs a name.");
        // A thickness of 0 is a layer measured by its area (decision 291); one not given at all is still refused.
        if (node is JsonObject spec)
            foreach (var (key, what) in new[] { ("floor", "floor"), ("walls", "walls"), ("ceiling", "ceiling") })
                if (spec.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)).Value is JsonArray given)
                    foreach (var l in given)
                        if (l is JsonObject lo && !lo.Any(kv => string.Equals(kv.Key, "thickness", StringComparison.OrdinalIgnoreCase) && kv.Value is JsonValue))
                            throw new CommandException($"Every layer of the {what} needs a thickness.");
        List<FinishLayer>? Check(List<FinishLayer>? layers, string what)
        {
            if (layers is not { Count: > 0 }) return null;
            foreach (var l in layers)
            {
                if (string.IsNullOrWhiteSpace(l.Material)) throw new CommandException($"Every layer of the {what} needs a material.");
                // Zero is a layer measured by its area only (paint, a membrane; decision 291).
                if (!(l.Thickness >= 0 && l.Thickness < 1)) throw new CommandException($"Every layer of the {what} needs a thickness.");
                foreach (var p in l.Parts ?? [])
                {
                    if (!(p.Height > 0)) throw new CommandException($"Every piece of a layer of the {what} needs a height.");
                    if (p.Thickness is { } t && !(t > 0 && t < 1)) throw new CommandException($"A piece's own thickness must be more than nothing.");
                }
            }
            return [.. layers.Select(l => l with { Material = l.Material.Trim(), Parts = l.Parts is { Count: > 0 } ps ? [.. ps.Select(p => p with { Material = p.Material?.Trim() ?? "" })] : null })];
        }
        if (s.WallHeight is { } wh && !(wh > 0)) throw new CommandException("The walls' finish height must be more than nothing.");
        if (s.Slab is { } sl && !(sl.Thickness > 0)) throw new CommandException("The slab needs a thickness.");
        FinishHatch? Hatch(FinishHatch? h, string what)
        {
            if (h is null) return null;
            if (!FinishHatch.Modes.Contains(h.Mode)) throw new CommandException($"The {what}'s plan hatch is from its material, its own or none.");
            if (h.Mode == "own" && string.IsNullOrWhiteSpace(h.Pattern)) throw new CommandException($"The {what}'s own plan hatch needs a pattern.");
            if (!(h.Scale > 0 && double.IsFinite(h.Scale))) throw new CommandException($"The {what}'s plan hatch needs a scale above nothing.");
            // The default (from the material, as it is) is kept as nothing, so the spec reads as before.
            return h is { Mode: "material", Pattern: null, Scale: 1, Angle: 0, Color: null } ? null
                : h with { Pattern = string.IsNullOrWhiteSpace(h.Pattern) ? null : h.Pattern.Trim().ToUpperInvariant(), Color = string.IsNullOrWhiteSpace(h.Color) ? null : h.Color.Trim() };
        }
        return s with
        {
            FloorHatch = Hatch(s.FloorHatch, "floor"), CeilingHatch = Hatch(s.CeilingHatch, "ceiling"),
            Name = s.Name.Trim(), Floor = Check(s.Floor, "floor"), Walls = Check(s.Walls, "walls"), Ceiling = Check(s.Ceiling, "ceiling"),
            Skirting = string.IsNullOrWhiteSpace(s.Skirting) ? null : s.Skirting.Trim(),
            Slab = s.Slab is { } slab ? slab with { Material = string.IsNullOrWhiteSpace(slab.Material) ? "Reinforced concrete" : slab.Material.Trim(), Extend = Math.Clamp(slab.Extend, 0, 1) } : null,
        };
    }

    /// <summary>Every material the finish names, for the project to have them.</summary>
    public IEnumerable<string> Materials() =>
        new[] { Floor, Walls, Ceiling }.SelectMany(x => x ?? []).SelectMany(l => (l.Parts ?? []).Select(p => p.Material).Prepend(l.Material))
            .Concat(Slab is { } s ? [s.Material] : []).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Room finishes (decision 265): a <c>LapinRoomFinishType</c> (its <see cref="RoomFinishSpec"/>) applied to rooms lays, for each,
/// a <c>LapinRoomFinish</c> per part it has (<c>part</c> floor, walls or ceiling; <c>roomId</c>), each following the room's outline
/// as the room does: the floor's layers from the room's level down, through the doors to the wall's axis when it says, the
/// floor's openings (a stairwell) taken out; the walls' as rings inside the outline (round its islands too), their pieces up the
/// height, the doors and windows of the walls along it cut out; the ceiling's under the slab above (else at the room's height),
/// its openings taken out. A skirting of its type round the room and a structural slab under the floor, both elements of their own
/// (<c>finishOf</c>: the room), are laid again when the finish is. Each measures gross (the outline, no opening deducted) and net
/// (as built). Exported as IfcCovering FLOORING, CLADDING and CEILING.
/// </summary>
public static class RoomFinishes
{
    public const string Class = "LapinRoomFinish";
    public const string TypeClass = "LapinRoomFinishType";
    public static readonly string[] PartNames = ["floor", "walls", "ceiling"];

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(TypeClass, "Room finish", [], "#d9cdb8", Kind: "type"));
        r.Class(new(Class, "Room finish", [], "#e6dccb"));
        r.Geometry(Class, Geometry);
        r.Quantities(Class, Quantities);
        r.Service<IScheduleSource>(new RoomFinishSchedule());
        r.Catalog("roomFinishes", new JsonObject
        {
            ["spec"] = RoomFinishSpec.Default().ToJson(),
            ["library"] = new JsonArray(RoomFinishSpec.Library.Select(s => (JsonNode)s.ToJson()).ToArray()),
        });

        r.Command("createRoomFinishType", "New room finish", (ctx, a) => new JsonObject { ["id"] = CreateType(ctx, RoomFinishSpec.From(a["spec"])).Id });
        r.Command("updateRoomFinishType", "Change room finish", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["id"], TypeClass);
            var spec = RoomFinishSpec.From(a["spec"]);
            EnsureMaterials(ctx, spec);
            var attrs = type.CloneAttrs();
            attrs["spec"] = spec.ToJson();
            ctx.Replace(type.With(name: spec.Name, attrs: attrs));
            // The rooms finished so are laid again: a part added or taken away, the skirting and the slab as the finish now says.
            var changed = ctx.Model.Get(type.Id)!;
            foreach (var room in ctx.Model.OfClass(Rooms.Class).Where(x => x.Str("finishType") == type.Id).ToList()) Apply(ctx, room, changed);
            return null;
        });
        r.Command("roomFinishTypeFromLibrary", "Room finish from the library", (ctx, a) =>
        {
            var name = (string?)a["name"] ?? throw new CommandException("Which library finish?");
            var spec = RoomFinishSpec.Library.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new CommandException($"The library has no room finish „{name}”.");
            var found = ctx.Model.OfClass(TypeClass).FirstOrDefault(t => string.Equals(t.Name, spec.Name, StringComparison.OrdinalIgnoreCase));
            return new JsonObject { ["id"] = (found ?? CreateType(ctx, spec)).Id, ["created"] = found == null };
        });
        // The finish laid in rooms: {roomIds} or {roomId}, or the room at a point of a storey ({storeyId, x, y}).
        r.Command("applyRoomFinish", "Finish rooms", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["typeId"], TypeClass);
            var rooms = RoomsOf(ctx, a);
            foreach (var room in rooms) Apply(ctx, room, type);
            return new JsonObject { ["rooms"] = new JsonArray(rooms.Select(x => (JsonNode)x.Id).ToArray()) };
        });
        r.Command("removeRoomFinish", "Take finishes off rooms", (ctx, a) =>
        {
            foreach (var room in RoomsOf(ctx, a))
            {
                Clear(ctx, room);
                var attrs = room.CloneAttrs();
                attrs.Remove("finishType");
                ctx.Replace(room.With(attrs: attrs));
            }
            return null;
        });
    }

    static List<Entity> RoomsOf(ICommandContext ctx, JsonObject a)
    {
        var ids = a["roomIds"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToList() : (string?)a["roomId"] is { } one ? [one] : [];
        if (ids.Count > 0) return ids.Select(id => Spatial.Require(ctx.Model, id, Rooms.Class)).ToList();
        var storeyId = (string?)a["storeyId"] ?? throw new CommandException("Which rooms?");
        var at = Args.Point(a, "x", "y");
        var room = ctx.Model.OfClass(Rooms.Class).Where(x => Spatial.StoreyOf(ctx.Model, x.Id)?.Id == storeyId)
            .FirstOrDefault(x => Rooms.Of(x, ctx.Model, ctx.Query.Geometry)?.Holds(at) == true)
            ?? throw new CommandException("No room there: place a room first (Architecture → Room), then finish it.");
        return [room];
    }

    static Entity CreateType(ICommandContext ctx, RoomFinishSpec spec)
    {
        EnsureMaterials(ctx, spec);
        var type = new Entity(ctx.NewId(), TypeClass, spec.Name, new JsonObject { ["spec"] = spec.ToJson(), ["exportAs"] = "IfcCoveringType" });
        ctx.Add(type);
        return type;
    }

    /// <summary>The finish's materials in the project, a new one made a finish (the slab's left as it is).</summary>
    static void EnsureMaterials(ICommandContext ctx, RoomFinishSpec spec)
    {
        foreach (var name in spec.Materials())
        {
            if (Materials.Find(ctx.Model, name) != null) continue;
            var made = Materials.Ensure(ctx, name);
            if (spec.Slab?.Material.Equals(name, StringComparison.OrdinalIgnoreCase) == true) continue;
            var attrs = made.CloneAttrs();
            attrs["category"] = "Finish";
            ctx.Replace(made.With(attrs: attrs));
        }
    }

    public static RoomFinishSpec? SpecOf(IModelView m, Entity finishOrType)
    {
        var type = finishOrType.Class == TypeClass ? finishOrType : m.RelatingOf(finishOrType.Id, IfcClass.RelDefinesByType);
        if (type?.Attrs["spec"] is not { } node) return null;
        try { return node.Deserialize(ArchitectureJson.Default.RoomFinishSpec); }
        catch (JsonException) { return null; }
    }

    /// <summary>What a room's finish laid: its finish parts, and the skirting and slab made for it.</summary>
    public static IEnumerable<Entity> Laid(IModelView m, string roomId) =>
        m.OfClass(Class).Where(e => e.Str("roomId") == roomId)
            .Concat(m.OfClass(Skirtings.Class).Where(e => e.Str("finishOf") == roomId))
            .Concat(m.OfClass(IfcClass.Slab).Where(e => e.Str("finishOf") == roomId));

    static void Clear(ICommandContext ctx, Entity room)
    {
        foreach (var e in Laid(ctx.Model, room.Id).ToList()) ctx.Remove(e.Id);
    }

    /// <summary>The room finished with the type: what it had laid before goes, each part the type has is laid, its skirting and slab made.</summary>
    public static void Apply(ICommandContext ctx, Entity room, Entity type)
    {
        var spec = SpecOf(ctx.Model, type) ?? throw new CommandException("The room finish cannot be read.");
        Clear(ctx, room);
        var storeyId = Spatial.StoreyOf(ctx.Model, room.Id)?.Id ?? throw new CommandException("The room is on no storey.");
        var label = string.Join(" ", new[] { room.Str("number"), room.Name }.Where(x => !string.IsNullOrWhiteSpace(x)));
        foreach (var (part, layers, predefined) in new[] { ("floor", spec.Floor, "FLOORING"), ("walls", spec.Walls, "CLADDING"), ("ceiling", spec.Ceiling, "CEILING") })
        {
            if (layers is not { Count: > 0 }) continue;
            var e = new Entity(ctx.NewId(), Class, $"{label} · {part}", new JsonObject { ["roomId"] = room.Id, ["part"] = part, ["exportAs"] = "IfcCovering" }, predefined);
            ctx.Add(e);
            Spatial.Contain(ctx, storeyId, e.Id);
            ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        }
        if (spec.Skirting is { } sk && Skirtings.TypeNamed(ctx, sk) is { } skType)
        {
            var s = Skirtings.Add(ctx, new JsonObject { ["roomId"] = room.Id, ["typeId"] = skType.Id, ["storeyId"] = storeyId });
            var sa = s.CloneAttrs();
            sa["finishOf"] = room.Id;
            ctx.Replace(s.With(attrs: sa));
        }
        if (spec.Slab is { } slab && Rooms.Of(room, ctx.Model, ctx.Query.Geometry) is { } o)
        {
            var contour = slab.Extend > 0 ? Polygon2.Offset(Polygon2.EnsureCcw(o.Outer), [slab.Extend]) : Polygon2.EnsureCcw(o.Outer);
            var thickness = Math.Round(slab.Thickness * 1000);
            var slabType = ctx.Model.OfClass(IfcClass.SlabType).FirstOrDefault(t => LayerSets.LayersOf(ctx.Model, t.Id) is [var only]
                    && Math.Abs(only.Thickness - slab.Thickness) < 1e-6 && string.Equals(ctx.Model.Get(only.Material)?.Name, slab.Material, StringComparison.OrdinalIgnoreCase))
                ?? Slabs.CreateType(ctx, $"{slab.Material} {thickness}", [new LayerSpec(slab.Material, slab.Thickness, Core: true)]);
            var made = Slabs.Add(ctx, storeyId, contour, room.Num("baseOffset") - Thickness(spec.Floor), slabType);
            var attrs = made.CloneAttrs();
            attrs["finishOf"] = room.Id;
            ctx.Replace(made.With(name: $"{label} · slab", attrs: attrs));
        }
        var ra = ctx.Model.Get(room.Id)!.CloneAttrs();
        ra["finishType"] = type.Id;
        ctx.Replace(ctx.Model.Get(room.Id)!.With(attrs: ra));
    }

    static double Thickness(IReadOnlyList<FinishLayer>? layers) => layers?.Sum(l => l.Thickness) ?? 0;

    // ------------------------------------------------------------ where things are

    static double Level(IModelView m, Entity room) => (Spatial.StoreyOf(m, room.Id)?.Num("elevation") ?? 0) + room.Num("baseOffset");

    static Vec2 Centre(IReadOnlyList<Vec2> pts) => new(pts.Average(p => p.X), pts.Average(p => p.Y));

    /// <summary>The floor's openings in the room: those of the floors at the room's level whose middle lies in it.</summary>
    static List<IReadOnlyList<Vec2>> FloorHoles(IModelView m, Rooms.Outline o, double level) =>
        m.OfClass(IfcClass.Slab).Where(s => Math.Abs(Slabs.Top(m, s) - level) < 0.6)
            .SelectMany(s => Slabs.Holes(m, s.Id)).Select(h => h.Contour).Where(h => o.Holds(Centre(h))).ToList();

    /// <summary>The slab over the room (the lowest whose outline holds the room's middle, its underside above the floor) and its underside.</summary>
    static (Entity Slab, double Soffit)? SlabAbove(IModelView m, Entity room, Rooms.Outline o, double level)
    {
        var at = Rooms.StampPoint(room, o);
        (Entity, double)? best = null;
        foreach (var s in m.OfClass(IfcClass.Slab))
        {
            if (s.Str("finishOf") != null) continue;
            var contour = Slabs.Contour(s);
            if (contour.Count < 3 || !Polygon2.Contains(contour, at)) continue;
            var soffit = Slabs.Top(m, s) + Slabs.Section(m, s).Min(b => b.From);
            if (soffit > level + 1.0 && (best == null || soffit < best.Value.Item2)) best = (s, soffit);
        }
        return best;
    }

    /// <summary>The underside the ceiling hangs from: the slab's over the room, else the room's height over its floor.</summary>
    static (double Soffit, List<IReadOnlyList<Vec2>> Holes) Ceiling(IModelView m, Entity room, Rooms.Outline o, double level)
    {
        if (SlabAbove(m, room, o, level) is { } above)
            return (above.Soffit, Slabs.Holes(m, above.Slab.Id).Select(h => h.Contour).Where(h => o.Holds(Centre(h))).ToList());
        return (level + Rooms.HeightOf(room, m), []);
    }

    /// <summary>The openings of the walls along the room's outline: each with its wall's frame and where the outline's face is across the wall.</summary>
    static List<(Openings.WallFrame F, Openings.Placed P, double Face)> OpeningsAlong(IModelView m, string? storeyId, Rooms.Outline o)
    {
        var found = new List<(Openings.WallFrame, Openings.Placed, double)>();
        var walls = m.OfClass(IfcClass.Wall).Where(w => Spatial.StoreyOf(m, w.Id)?.Id == storeyId && w.Attrs["start"] != null && PathCurve.Of(w) is not { IsArc: true }).ToList();
        foreach (var loop in o.Loops)
            for (var k = 0; k < loop.Count; k++)
            {
                var (a, b) = (loop[k], loop[(k + 1) % loop.Count]);
                var len = (b - a).Length;
                if (len < 1e-6) continue;
                var u = (b - a) * (1 / len);
                foreach (var w in walls)
                    foreach (var p in Openings.PlacedOn(m, w.Id))
                    {
                        var f = Openings.FrameOf(m, w, p.Along);
                        if (Math.Abs(Vec2.Cross(u, f.Dir)) > 0.05) continue;
                        var y = Vec2.Dot((a + b) * 0.5 - f.Start, f.Left);
                        if (y < f.YMin - 0.02 || y > f.YMax + 0.02) continue;
                        // The opening along this stretch of the outline.
                        var c = f.Start + f.Dir * p.Along;
                        var t = Vec2.Dot(c - a, u);
                        if (t < -p.Width / 2 || t > len + p.Width / 2) continue;
                        if (found.Any(x => x.Item2.Opening.Id == p.Opening.Id && Math.Abs(x.Item3 - y) < 1e-3)) continue;
                        found.Add((f, p, y));
                    }
            }
        return found;
    }

    /// <summary>A rectangle in a wall's frame: along between x0 and x1, across between y0 and y1.</summary>
    static List<Vec2> Rect(Openings.WallFrame f, double x0, double x1, double y0, double y1) =>
        [f.Start + f.Dir * x0 + f.Left * y0, f.Start + f.Dir * x1 + f.Left * y0, f.Start + f.Dir * x1 + f.Left * y1, f.Start + f.Dir * x0 + f.Left * y1];

    /// <summary>
    /// A door's or window's reveal as the room's finish lines it, in the wall's frame: the opening's sides along it, the room's face
    /// and the frame's face across it (a bare opening's: the wall's axis), its bottom and top; whether its bottom is lined (a
    /// window's, when no sill board covers it on the room's side). A shaped head is left to the wall's own cut.
    /// </summary>
    sealed record RevealLining(Openings.WallFrame F, double X0, double X1, double Face, double Frame, double Z0, double Z1, bool Bottom)
    {
        Solid? Box(double x0, double x1, double y0, double y1, double z0, double z1) =>
            x1 - x0 > 1e-6 && Math.Abs(y1 - y0) > 1e-6 && z1 - z0 > 1e-6
                ? Prism.ExtrudeFootprint(Polygon2.EnsureCcw(Rect(F, x0, x1, Math.Min(y0, y1), Math.Max(y0, y1))), z0, z1 - z0, "reveal") : null;

        /// <summary>
        /// One layer's lining (from <paramref name="d0"/> to <paramref name="d1"/> in from the reveal's faces) where the piece it is
        /// stands, between <paramref name="za"/> and <paramref name="zb"/>: the jambs there, the head when the piece reaches it (the
        /// layer's last piece always does), the bottom when the piece starts under it. Across, from the frame to the layer's own face
        /// in the room, so it meets the walls' ring at the corner.
        /// </summary>
        public IEnumerable<Solid> Boxes(double d0, double d1, double za, double zb, bool last)
        {
            var side = Face >= Frame ? 1 : -1;
            var (y0, y1) = (Frame, Face + side * d1);
            var (lo, hi) = (Bottom ? Z0 + d0 : Z0, Z1 - d0);
            var (a, b) = (Math.Max(lo, za), Math.Min(hi, zb));
            if (b - a > 1e-6)
            {
                if (Box(X0 + d0, X0 + d1, y0, y1, a, b) is { } left) yield return left;
                if (Box(X1 - d1, X1 - d0, y0, y1, a, b) is { } right) yield return right;
            }
            if (Z1 - d1 >= za - 1e-9 && (Z1 - d1 < zb || last) && Box(X0 + d1, X1 - d1, y0, y1, Z1 - d1, Z1 - d0) is { } head) yield return head;
            if (Bottom && Z0 + d0 >= za - 1e-9 && Z0 + d0 < zb && Box(X0 + d1, X1 - d1, y0, y1, Z0 + d0, Z0 + d1) is { } bottom) yield return bottom;
        }
    }

    static RevealLining? Lining((Openings.WallFrame F, Openings.Placed P, double Face) x)
    {
        var (f, p, face) = x;
        if (p.Width < 1e-3 || p.Height < 1e-3) return null;
        if (p.Spec is { } shaped ? JoineryShapes.Shaped(shaped) : p.Shape is { Kind: not "rect" }) return null;
        // The frame's face on the room's side: the type's frame across the wall (Y to the exterior, mapped as the joinery maps it), else the axis.
        var frame = 0.0;
        bool bottom;
        if (p.Spec is { } spec && p.Fill != null)
        {
            var sy = p.Fill.Bool("flipSide") ? -1 : 1;
            var (ya, yb) = (f.Centre - sy * (spec.Frame.Offset + spec.Frame.Depth / 2), f.Centre - sy * (spec.Frame.Offset - spec.Frame.Depth / 2));
            frame = face >= f.Centre ? Math.Max(ya, yb) : Math.Min(ya, yb);
            // The room on the interior side (the type's −Y) has the inside sill; else the outside one.
            var interior = Math.Sign(face - f.Centre) == sy;
            var sill = interior ? spec.Details?.SillInside : spec.Details?.SillOutside;
            bottom = !spec.IsDoor && p.Sill > 1e-3 && sill is not { On: true };
        }
        else bottom = p.Sill > 1e-3;
        if (Math.Abs(face - frame) < 1e-3) return null;
        var z0 = f.Base + p.Sill;
        return new RevealLining(f, p.Along - p.Width / 2, p.Along + p.Width / 2, face, frame, z0, z0 + p.Height, bottom);
    }

    // ------------------------------------------------------------ in plans (decision 299)

    /// <summary>A finish's hatch as a plan draws it: a pattern, its scale on paper, its angle, its colour.</summary>
    public sealed record PlanHatch(string Pattern, double Scale, double Angle, string Color);

    public const string HatchColor = "#7a7a7a";

    /// <summary>What a material's name suggests for its surface seen from above: tiles, boards, carpet, sheet flooring, a poured floor.</summary>
    static readonly (System.Text.RegularExpressions.Regex Re, string Pattern)[] Surfaces =
    [
        (new(@"tile|ceramic|porcelain|stone|marble|granit|terrazzo|gresie|faianta|piatra|marmura"), "TILES"),
        (new(@"parquet|laminat|wood|timber|oak|beech|plank|board|deck|lemn|parchet|stejar"), "WOOD"),
        (new(@"carpet|textile|mocheta|covor"), "CROSS"),
        (new(@"vinyl|linoleum|pvc|rubber|lvt|cauciuc"), "HORIZONTAL"),
        (new(@"epoxy|resin|concrete|screed|terrazzo|beton|sapa|rasina"), "CONCRETE"),
    ];

    static string? SurfaceOf(string name)
    {
        var n = Lapin.Drawing.MaterialGraphics.Normalise(name).Replace('ă', 'a').Replace('â', 'a').Replace('î', 'i').Replace('ș', 's').Replace('ş', 's').Replace('ț', 't').Replace('ţ', 't');
        foreach (var (re, pattern) in Surfaces) if (re.IsMatch(n)) return pattern;
        return null;
    }

    /// <summary>
    /// The hatch a finish's floor (or ceiling) draws in plan: none when the part has no layers or its hatch is off; its own
    /// pattern when it has one; else the face layer's material's (the floor's top layer, the ceiling's last), the first going
    /// inwards whose surface pattern is set (Manage → Materials, Elevation) or suggested by its name; else diagonal lines.
    /// </summary>
    public static PlanHatch? HatchOf(IModelView m, RoomFinishSpec spec, string part)
    {
        var (layers, h) = part == "ceiling" ? (spec.Ceiling, spec.CeilingHatch) : (spec.Floor, spec.FloorHatch);
        if (layers is not { Count: > 0 }) return null;
        h ??= new FinishHatch();
        if (h.Mode == "none") return null;
        var color = h.Color ?? HatchColor;
        if (h.Mode == "own" && h.Pattern is { } own) return new PlanHatch(own, h.Scale, h.Angle, color);
        // From the face inwards: the floor's top layer first, the ceiling's lowest.
        foreach (var l in part == "ceiling" ? layers.AsEnumerable().Reverse() : layers)
        {
            var material = Materials.Find(m, l.Material);
            if (Lapin.Drawing.ViewGraphics.From(material?.Attrs["elevation"]) is { Hatch: { Length: > 0 } mine } g)
                return new PlanHatch(mine.ToUpperInvariant(), (g.HatchScale ?? 1) * h.Scale, (g.HatchAngle ?? 0) + h.Angle, color);
            if (SurfaceOf(l.Material) is { } guess) return new PlanHatch(guess, h.Scale, h.Angle, color);
        }
        return new PlanHatch("LINES45", h.Scale, h.Angle, color);
    }

    /// <summary>
    /// The regions a room's finish covers in plan, each a set of loops (even-odd): the floor over the room's outline with its
    /// islands and the floor's openings out, and each threshold through a door when the finish takes the floor there; the
    /// ceiling over the outline with the openings of the slab above out.
    /// </summary>
    public static List<IReadOnlyList<IReadOnlyList<Vec2>>> PlanRegions(IModelView m, Entity room, Rooms.Outline o, RoomFinishSpec spec, string part)
    {
        var level = Level(m, room);
        var loops = o.Loops.ToList();
        if (part == "ceiling")
        {
            loops.AddRange(Ceiling(m, room, o, level).Holes);
            return [loops];
        }
        loops.AddRange(FloorHoles(m, o, level));
        var regions = new List<IReadOnlyList<IReadOnlyList<Vec2>>> { loops };
        if (spec.Thresholds)
            foreach (var x in OpeningsAlong(m, Spatial.StoreyOf(m, room.Id)?.Id, o).Where(x => x.P.Sill < 0.05))
                regions.Add([Rect(x.F, x.P.Along - x.P.Width / 2, x.P.Along + x.P.Width / 2, x.Face, 0)]);
        return regions;
    }

    // ------------------------------------------------------------ the body

    static Solid? Slab(IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> holes, double z0, double t, string tag) =>
        Prism.Extrude(outer.Select(p => p.At(z0)).ToArray(), holes.Select(h => (IReadOnlyList<Vec3>)h.Select(p => p.At(z0)).ToArray()).ToList(), new Vec3(0, 0, t), tag);

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices services)
    {
        if (e.Str("roomId") is not { } rid || m.Get(rid) is not { Class: Rooms.Class } room) return new ElementGeometry([], Diagnostics: ["The finish's room is gone."]);
        if (Rooms.Live(room, m, services) is not { } o) return new ElementGeometry([], Diagnostics: ["The room has no outline."]);
        if (SpecOf(m, e) is not { } spec) return new ElementGeometry([], Diagnostics: ["The finish's type cannot be read."]);
        var part = e.Str("part") ?? "floor";
        var level = Level(m, room);
        var storeyId = Spatial.StoreyOf(m, room.Id)?.Id;
        var outer = Polygon2.EnsureCcw(o.Outer);
        var islands = o.Islands.Select(i => (IReadOnlyList<Vec2>)Polygon2.EnsureCcw(i)).ToList();
        var parts = new List<GeometryPart>();
        var problems = new List<string>();
        var booleans = services.Get<ISolidBooleans>();
        string? Mat(string name) => name.Length == 0 ? null : Materials.Find(m, name)?.Id;
        // What each layer's material covers by the outline, nothing deducted: its gross.
        var gross = new List<(int Layer, string Material, double Thickness, double Area, double Uncut)>();
        // What lines the doors' and windows' reveals, by layer and material: added to the walls' net.
        var reveals = new List<(int Layer, string Material, double Volume)>();
        void Add(string name, string material, Solid? solid, string key)
        {
            if (solid != null && Measure.Volume(solid) > 1e-12) parts.Add(new GeometryPart(name, Materials.ColorOf(m, Mat(material)), solid, Mat(material), key));
        }
        if (part == "floor" && spec.Floor is { Count: > 0 } floor)
        {
            var holes = islands.Concat(FloorHoles(m, o, level)).ToList();
            // Through the doors, to the wall's axis (the next room's floor comes to it from the other side): the floor goes on under the door's leaf (unless the finish says not).
            var thresholds = spec.Thresholds
                ? OpeningsAlong(m, storeyId, o).Where(x => x.P.Sill < 0.05).Select(x => Rect(x.F, x.P.Along - x.P.Width / 2, x.P.Along + x.P.Width / 2, x.Face, 0)).ToList()
                : [];
            var top = level;
            for (var i = 0; i < floor.Count; i++)
            {
                var l = floor[i];
                // A layer of no thickness (paint, a membrane; decision 291) builds nothing: it is measured by its area only.
                if (l.Thickness > 0) Add(l.Material, l.Material, Slab(outer, holes, top - l.Thickness, l.Thickness, "floor"), $"floor:{i}");
                gross.Add((i, l.Material, l.Thickness, o.GrossArea, -1));
                if (l.Thickness > 0) foreach (var t in thresholds) Add(l.Material, l.Material, Prism.ExtrudeFootprint(Polygon2.EnsureCcw(t), top - l.Thickness, l.Thickness, "threshold"), $"floor:{i}");
                top -= l.Thickness;
            }
        }
        else if (part == "ceiling" && spec.Ceiling is { Count: > 0 } ceiling)
        {
            var (soffit, above) = Ceiling(m, room, o, level);
            var holes = islands.Concat(above).ToList();
            var at = soffit;
            for (var i = 0; i < ceiling.Count; i++)
            {
                var l = ceiling[i];
                if (l.Thickness > 0) Add(l.Material, l.Material, Slab(outer, holes, at - l.Thickness, l.Thickness, "ceiling"), $"ceiling:{i}");
                gross.Add((i, l.Material, l.Thickness, o.GrossArea, -1));
                at -= l.Thickness;
            }
        }
        else if (part == "walls" && spec.Walls is { Count: > 0 } walls)
        {
            var (soffit, _) = Ceiling(m, room, o, level);
            var z1 = spec.WallHeight is { } wh ? level + wh : soffit - Thickness(spec.Ceiling);
            if (z1 - level < 1e-6) return new ElementGeometry([], Diagnostics: ["The walls' finish has no height: the ceiling is at the floor."]);
            var total = walls.Sum(l => Math.Max(l.Thickness, l.Parts?.Max(p => p.Thickness ?? 0) ?? 0));
            // The doors and windows of the walls along the outline, through every layer of the finish.
            var along = OpeningsAlong(m, storeyId, o);
            var cutters = along.Select(x =>
            {
                var side = x.Face >= 0 ? 1 : -1;
                var footprint = Polygon2.EnsureCcw(Rect(x.F, x.P.Along - x.P.Width / 2, x.P.Along + x.P.Width / 2, x.Face - side * 0.01, x.Face + side * (total + 0.01)));
                return Prism.ExtrudeFootprint(footprint, x.F.Base + x.P.Sill, x.P.Height, "opening");
            }).OfType<Solid>().ToList();
            if (cutters.Count > 0 && booleans == null) problems.Add("Openings need the boolean library (manifold), missing here: the walls' finish is drawn uncut.");
            // A ring between d0 and d1 in from the outline, and out from each island.
            List<Solid> Rings(double d0, double d1, double z0, double h)
            {
                var o2 = new List<Solid>();
                if (Slab(Polygon2.Offset(outer, [-d0]), [Polygon2.Offset(outer, [-d1])], z0, h, "walls") is { } s) o2.Add(s);
                foreach (var isl in islands)
                    if (Slab(Polygon2.Offset(isl, [d1]), [Polygon2.Offset(isl, [d0])], z0, h, "walls") is { } si) o2.Add(si);
                return o2;
            }
            var linings = along.Select(Lining).OfType<RevealLining>().ToList();
            var d = 0.0;
            for (var i = 0; i < walls.Count; i++)
            {
                var l = walls[i];
                var at = level;
                foreach (var p in (l.Parts ?? []).Append(new FinishPart(l.Material, double.PositiveInfinity)))
                {
                    if (at >= z1 - 1e-9) break;
                    var top = Math.Min(z1, at + p.Height);
                    var thick = (p.Thickness ?? l.Thickness) > 0;
                    if (p.Material.Length > 0 && top - at > 1e-9 && thick)
                        foreach (var ring in Rings(d, d + (p.Thickness ?? l.Thickness), at, top - at))
                        {
                            gross.Add((i, p.Material, p.Thickness ?? l.Thickness, 0, Measure.Volume(ring)));
                            var cut = ring;
                            if (cutters.Count > 0 && booleans != null)
                            {
                                try { cut = booleans.Difference(ring, cutters, ring.Tag) ?? ring; }
                                catch (InvalidOperationException ex) { problems.Add($"{p.Material}: an opening could not be cut ({ex.Message})."); }
                            }
                            Add(p.Material, p.Material, cut, $"walls:{i}");
                        }
                    if (p.Material.Length > 0 && top - at > 1e-9)
                    {
                        gross.Add((i, p.Material, p.Thickness ?? l.Thickness, o.NetPerimeter * (top - at), 0));
                        // The reveals of the doors and windows lined too, from the room's face to the frame, this piece where it reaches.
                        foreach (var lining in thick ? linings : [])
                            foreach (var box in lining.Boxes(d, d + (p.Thickness ?? l.Thickness), at, top, top >= z1 - 1e-9))
                            {
                                Add(p.Material, p.Material, box, $"walls:{i}");
                                reveals.Add((i, p.Material, Measure.Volume(box)));
                            }
                    }
                    at = top;
                }
                d += l.Thickness;
            }
        }
        var g = new ElementGeometry(parts, [], [.. outer], problems.Count > 0 ? problems : null);
        // The measures, by layer and material: the gross as laid out, the net as built (what the openings left).
        var measures = gross.GroupBy(x => (x.Layer, x.Material)).Select(gr =>
        {
            var id = Mat(gr.Key.Material);
            var v = parts.Where(q => q.Key == $"{part}:{gr.Key.Layer}" && q.Material == id).Sum(q => Measure.Volume(q.Solid));
            var t = gr.First().Thickness;
            var area = gr.Sum(x => x.Area);
            if (!(t > 0)) return new FinishMeasure(part, gr.Key.Layer, gr.Key.Material, 0, area, double.NaN, 0);
            // A floor's or a ceiling's net is what is built; the walls' is their gross less what the openings took out of it.
            var lined = reveals.Where(x => x.Layer == gr.Key.Layer && x.Material == gr.Key.Material).Sum(x => x.Volume);
            var net = gr.Any(x => x.Uncut < 0) ? (t > 0 ? v / t : 0) : Math.Max(0, area - (gr.Sum(x => x.Uncut) - (v - lined)) / t) + lined / t;
            return new FinishMeasure(part, gr.Key.Layer, gr.Key.Material, t, area, net, v, t > 0 ? lined / t : 0);
        }).ToList();
        // A layer of no thickness (decision 291): its net as the layer it lies on sees it (a floor's the one under it, the walls'
        // and a ceiling's the one before it), that layer's net over its gross times its own gross; its own gross when it lies alone.
        var below = part == "floor" ? 1 : -1;
        for (var k = 0; k < measures.Count; k++)
        {
            if (!double.IsNaN(measures[k].Net)) continue;
            var x = measures[k];
            List<FinishMeasure> On(int layer) => measures.Where(y => y.Layer == layer && y.Thickness > 0).ToList();
            var maxLayer = measures.Max(y => y.Layer);
            var on = Enumerable.Range(1, maxLayer + 1).SelectMany(n => new[] { x.Layer + below * n, x.Layer - below * n }).Select(On).FirstOrDefault(l => l.Count > 0);
            var ratio = on is { Count: > 0 } && on.Sum(y => y.Gross) > 1e-9 ? on.Sum(y => y.Net) / on.Sum(y => y.Gross) : 1;
            measures[k] = x with { Net = x.Gross * ratio };
        }
        Built.AddOrUpdate(g, measures);
        return g;
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ElementGeometry, List<FinishMeasure>> Built = new();

    // ------------------------------------------------------------ what it measures

    /// <summary>A finish's measures by layer and material: gross (the outline's, nothing deducted) and net (as built) area, net volume.</summary>
    public sealed record FinishMeasure(string Part, int Layer, string Material, double Thickness, double Gross, double Net, double Volume, double Reveals = 0);

    /// <summary>What the finish's geometry measured when it was built (none for a geometry it did not build).</summary>
    public static List<FinishMeasure> Measures(ElementGeometry? g) => g != null && Built.TryGetValue(g, out var o) ? o : [];

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        var measures = Measures(g);
        if (measures.Count == 0) yield break;
        // The covering's own: its first layer's (the face one sees), the whole build-up's thickness.
        var face = measures.Where(x => x.Layer == (e.Str("part") == "walls" ? measures.Max(y => y.Layer) : 0)).ToList();
        yield return new QuantitySet("Qto_CoveringBaseQuantities",
        [
            new("Width", measures.GroupBy(x => x.Layer).Sum(gr => gr.Max(x => x.Thickness)), "LENGTH"),
            new("GrossArea", face.Sum(x => x.Gross), "AREA", "Gross area"),
            new("NetArea", face.Sum(x => x.Net), "AREA", "Net area"),
        ]);
        foreach (var x in measures)
            yield return new QuantitySet("Lapin_FinishLayerQuantities",
            [
                new("Width", x.Thickness, "LENGTH"),
                new("GrossArea", x.Gross, "AREA", "Gross area"),
                new("NetArea", x.Net, "AREA", "Net area"),
                new("NetVolume", x.Volume, "VOLUME", "Net volume"),
                .. x.Part == "walls" ? [new Quantity("RevealArea", x.Reveals, "AREA", "Reveals")] : Array.Empty<Quantity>(),
            ], $"{x.Part}:{x.Layer}:{x.Material}", x.Material, Materials.Find(m, x.Material)?.Id);
    }
}

/// <summary>
/// The room finishes schedule (decision 265): for each finished room, each material of its floor, walls and ceiling with its gross
/// and net area and its volume, its skirting's length and its slab's volume; the totals by part and material at the end.
/// </summary>
public sealed class RoomFinishSchedule : IScheduleSource
{
    public string Kind => "roomFinishes";
    public string Label => "Room finishes";

    public sealed record Row(string Storey, string Room, string Finish, string Part, string Material, double? Gross, double? Net, double? Length, double? Volume, string? RoomId);

    public static List<Row> Rows(IQueryContext q)
    {
        var m = q.Model;
        var o = new List<Row>();
        foreach (var room in m.OfClass(Rooms.Class).Where(r => r.Str("finishType") != null)
                     .OrderBy(r => Spatial.StoreyOf(m, r.Id)?.Num("elevation") ?? 0).ThenBy(r => r.Str("number") ?? "", StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering)))
        {
            var storey = Spatial.StoreyOf(m, room.Id)?.Name ?? "";
            var label = string.Join(" ", new[] { room.Str("number"), room.Name }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var finish = m.Get(room.Str("finishType")!)?.Name ?? "";
            foreach (var e in m.OfClass(RoomFinishes.Class).Where(x => x.Str("roomId") == room.Id).OrderBy(x => Array.IndexOf(RoomFinishes.PartNames, x.Str("part"))))
                foreach (var x in RoomFinishes.Measures(q.Geometry(e.Id)))
                    o.Add(new(storey, label, finish, Capital(x.Part), ByThickness(m, x.Material, x.Thickness), x.Gross, x.Net, null, x.Volume, room.Id));
            foreach (var s in m.OfClass(Skirtings.Class).Where(x => x.Str("finishOf") == room.Id))
                o.Add(new(storey, label, finish, "Skirting", Skirtings.SpecOf(m, s).Material, null, null, Skirtings.LengthOf(q.Geometry(s.Id)), null, room.Id));
            foreach (var s in m.OfClass(IfcClass.Slab).Where(x => x.Str("finishOf") == room.Id))
                if (q.Geometry(s.Id) is { } g)
                    foreach (var p in g.Parts)
                    {
                        var v = Measure.Volume(p.Solid);
                        var t = Slabs.Section(m, s).Sum(b => b.To - b.From);
                        o.Add(new(storey, label, finish, "Slab", m.Get(p.Material ?? "")?.Name ?? p.Name, Math.Abs(Polygon2.SignedArea(Slabs.Contour(s))), t > 0 ? v / t : null, null, v, room.Id));
                    }
        }
        return o;
    }

    /// <summary>
    /// A material measured by area (docs/plan-buildup-tags.md) listed by its thickness, so the totals keep each thickness apart
    /// ("Ceramic tiles · 10 mm"), flagged when the thickness is not in its catalogue; any other material by its name.
    /// </summary>
    static string ByThickness(IModelView m, string material, double thickness)
    {
        if (thickness <= 1e-9 || MaterialMeasure.Of(Materials.Find(m, material)) is not { By: "area" or "count" } rule) return material;
        var mm = thickness * 1000;
        return $"{material} · {MaterialFields.Mm(mm)} mm" + (MaterialMeasure.InCatalogue(rule, mm) ? "" : " (not a catalogue thickness)");
    }

    static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    public ScheduleGrid Grid(IQueryContext q)
    {
        static string N(double? v) => v is { } x ? x.ToString("0.00", CultureInfo.InvariantCulture) : "";
        var rows = Rows(q);
        var grid = rows.Select(r => (IReadOnlyList<string>)[r.Storey, r.Room, r.Finish, r.Part, r.Material, N(r.Gross), N(r.Net), N(r.Length), r.Volume is { } v ? v.ToString("0.000", CultureInfo.InvariantCulture) : ""]).ToList();
        var keys = rows.Select(r => (string?)$"{r.RoomId}|{r.Part}|{r.Material}").ToList();
        // The totals: each part's material over every room.
        foreach (var t in rows.GroupBy(r => (r.Part, r.Material)).OrderBy(g => Array.IndexOf(["Floor", "Walls", "Ceiling", "Skirting", "Slab"], g.Key.Part)).ThenBy(g => g.Key.Material))
        {
            double? Sum(Func<Row, double?> f) => t.Any(r => f(r) != null) ? t.Sum(r => f(r) ?? 0) : null;
            grid.Add(["Total", "", "", t.Key.Part, t.Key.Material, N(Sum(r => r.Gross)), N(Sum(r => r.Net)), N(Sum(r => r.Length)), Sum(r => r.Volume) is { } v ? v.ToString("0.000", CultureInfo.InvariantCulture) : ""]);
            keys.Add(null);
        }
        return new ScheduleGrid(Label, ["Storey", "Room", "Finish", "Part", "Material", "Gross (m²)", "Net (m²)", "Length (m)", "Volume (m³)"], grid, keys);
    }
}

/// <summary>
/// A room finish's build-up for a build-up tag (docs/plan-buildup-tags.md): its part's layers as its type lists them, the
/// floor's and the ceiling's from the top down, the walls' from the wall's face into the room; pieces up the walls'
/// height a line each.
/// </summary>
public sealed class RoomFinishBuildUps : IElementBuildUps
{
    public BuildUp? Of(IModelView m, Entity e)
    {
        if (e.Class != RoomFinishes.Class || RoomFinishes.SpecOf(m, e) is not { } spec) return null;
        var part = e.Str("part");
        var layers = part switch { "floor" => spec.Floor, "walls" => spec.Walls, "ceiling" => spec.Ceiling, _ => null };
        if (layers is not { Count: > 0 }) return null;
        string? Id(string name) => name.Length == 0 ? null : Materials.Find(m, name)?.Id;
        return new BuildUp(layers.Select((l, i) => new BuildupLayer(i, Id(l.Material), l.Thickness, false, l.Material,
            PartMaterials: l.Parts?.Select(p => Id(p.Material)).OfType<string>().Where(x => x != Id(l.Material)).Distinct().ToList())).ToList(), part != "walls");
    }
}
