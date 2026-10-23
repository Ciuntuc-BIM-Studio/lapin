using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Walls. An IfcWall is its axis (start → end, storey-local metres), its
/// height and its base offset over the storey; its section comes from its
/// type's layers, laid by the wall's own IfcMaterialLayerSetUsage with the
/// axis on the core. A wall without layers keeps its own thickness, centred.
/// </summary>
public static class Walls
{
    public const double DefaultHeight = 3.0;
    public const double DefaultThickness = 0.25;
    const double MinLength = 1e-3;

    public static Vec2 Start(Entity w) => Point(w.Attrs["start"]);
    public static Vec2 End(Entity w) => Point(w.Attrs["end"]);

    static Vec2 Point(JsonNode? n) =>
        n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : default;

    static JsonArray Array(Vec2 p) => [p.X, p.Y];

    public static JsonObject Attrs(Vec2 start, Vec2 end, double height, double baseOffset, double? thickness = null)
    {
        var o = new JsonObject { ["start"] = Array(start), ["end"] = Array(end), ["height"] = height, ["baseOffset"] = baseOffset };
        if (thickness != null) o["thickness"] = thickness;
        return o;
    }

    public static Entity? TypeOf(IModelView m, string wallId) =>
        m.RelatingOf(wallId, IfcClass.RelDefinesByType) is { Class: IfcClass.WallType } t ? t : null;

    /// <summary>The wall's own layer set usage, if it has one.</summary>
    public static Entity? UsageOf(IModelView m, string wallId) =>
        m.RelatingOf(wallId, IfcClass.RelAssociatesMaterial) is { Class: IfcClass.MaterialLayerSetUsage } u ? u : null;

    /// <summary>A new wall on the storey, of the type (the project's default when none), joined where it meets the walls already there.</summary>
    public static Entity Add(IEdit edit, string storeyId, Vec2 start, Vec2 end, double height, double baseOffset, Entity? type, JoinStyle joinStyle = JoinStyle.Butt)
    {
        var wall = AddUnjoined(edit, storeyId, start, end, height, baseOffset, type);
        Joins.Auto(edit, wall.Id, joinStyle);
        return edit.Model.Get(wall.Id)!;
    }

    public static Entity AddUnjoined(IEdit edit, string storeyId, Vec2 start, Vec2 end, double height, double baseOffset, Entity? type)
    {
        if ((end - start).Length < MinLength) throw new CommandException("A wall needs two different points.");
        if (!(height > 0)) throw new CommandException("A wall's height must be positive.");
        type ??= WallTypes.EnsureDefault(edit);
        var wall = new Entity(edit.NewId(), IfcClass.Wall, type.Name, Attrs(start, end, height, baseOffset), "STANDARD");
        edit.Add(wall);
        Spatial.Contain(edit, storeyId, wall.Id);
        SetType(edit, wall, type);
        return wall;
    }

    /// <summary>The wall becomes of <paramref name="type"/>: its usage points at the type's layers, its axis on their core.</summary>
    public static void SetType(IEdit edit, Entity wall, Entity type)
    {
        edit.Relate(IfcClass.RelDefinesByType, type.Id, wall.Id);
        LayOut(edit, wall, type);
    }

    /// <summary>
    /// Lays the type's layers on the wall: its own usage (made when it has
    /// none) points at the type's layer set, with the axis on the core,
    /// mirrored when the wall runs the negative way. A type without layers
    /// leaves the wall its own thickness.
    /// </summary>
    public static void LayOut(IEdit edit, Entity wall, Entity type)
    {
        var set = WallTypes.LayerSetOf(edit.Model, type.Id);
        var usage = UsageOf(edit.Model, wall.Id);
        if (set == null)
        {
            if (usage != null) edit.Remove(usage.Id);
            return;
        }
        var layers = Layers.Read(set);
        var negative = usage != null && Layers.ReadUsage(usage) is { Negative: true };
        var next = new LayerUsage(set.Id, negative, Layers.CoreOffset(layers, negative));
        if (usage == null)
        {
            var made = new Entity(edit.NewId(), IfcClass.MaterialLayerSetUsage, "", Layers.WriteUsage(next));
            edit.Add(made);
            edit.Relate(IfcClass.RelAssociatesMaterial, made.Id, wall.Id);
        }
        else edit.Replace(usage.With(attrs: Layers.WriteUsage(next)));
    }

    /// <summary>
    /// Flips the wall across its axis: its ends stay, its layers swap faces
    /// (the usage's sense turns and its offset changes sign), so the body is
    /// mirrored across the axis. A wall flips only that way.
    /// </summary>
    public static void Flip(IEdit edit, Entity wall, string axis = "y")
    {
        if (axis == "x") throw new CommandException("A wall flips across its axis (Flip Y): its ends stay where they are.");
        if (UsageOf(edit.Model, wall.Id) is not { } usage || Layers.ReadUsage(usage) is not { } u) return;
        edit.Replace(usage.With(attrs: Layers.WriteUsage(u with { Negative = !u.Negative, Offset = -u.Offset })));
    }

    /// <summary>A layer band of the wall's section: across the axis (left of it positive), its material, whether it is the core.</summary>
    public readonly record struct Band(double From, double To, string? Material, string Name, bool Core = false, int Index = 0);

    /// <summary>The wall's section, first layer to last; one centred band for a wall without layers.</summary>
    public static IReadOnlyList<Band> Section(IModelView m, Entity wall)
    {
        if (UsageOf(m, wall.Id) is { } usageEntity && Layers.ReadUsage(usageEntity) is { } usage && m.Get(usage.ForLayerSet) is { } set)
        {
            var layers = Layers.Read(set);
            var bands = Layers.Bands(layers, usage);
            var core = Layers.CoreIndex(layers);
            return layers.Select((l, i) => new Band(bands[i].From, bands[i].To, l.Material, l.Name ?? m.Get(l.Material)?.Name ?? $"Layer {i + 1}", i == core, i)).ToList();
        }
        var t = wall.NumOrNull("thickness") ?? DefaultThickness;
        return [new Band(-t / 2, t / 2, null, wall.Name, true)];
    }

    /// <summary>
    /// The wall's body: a prism per layer, cut at each end the way its joins
    /// say (the cut runs straight across the thickness, so each layer takes its
    /// share of it), standing on its storey.
    /// </summary>
    public static ElementGeometry? Geometry(Entity wall, IModelView m, IServices? services = null)
    {
        var start = Start(wall);
        var end = End(wall);
        if ((end - start).Length < MinLength) return new ElementGeometry([], Diagnostics: ["The wall has no length."]);
        var problems = new List<string>();
        var ends = Joins.EndsOf(m, wall, problems);
        if (ends[0] is { } s) start = s.Point;
        if (ends[1] is { } e) end = e.Point;
        var startCut = ends[0]?.Cut ?? EndCut.Square;
        var endCut = ends[1]?.Cut ?? EndCut.Square;
        var along = end - start;
        var length = along.Length;
        var dir = along.Normalized();
        var left = dir.Perp;
        var storey = Spatial.StoreyOf(m, wall.Id);
        var z = (storey?.Num("elevation") ?? 0) + wall.Num("baseOffset");
        var height = wall.Num("height", DefaultHeight);

        var bands = Section(m, wall);
        var yMin = bands.Min(b => b.From);
        var yMax = bands.Max(b => b.To);
        // A cut at a face: Right at yMin, Left at yMax, straight in between.
        double At(EndCut c, double y) => yMax - yMin < 1e-12 ? c.Right : c.Right + (c.Left - c.Right) * (y - yMin) / (yMax - yMin);
        Vec2 P(double x, double y) => start + dir * x + left * y;

        var zones = TypeOf(m, wall.Id) is { } type ? WallZones.Bands(WallZones.Of(type), height) : [];
        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();

        // Openings take their boxes out of every part they reach; the plan shows the gaps where they cross the cut.
        var openings = Openings.PlacedOn(m, wall.Id);
        var frame = new Openings.WallFrame(start, dir, left, z, yMin, yMax);
        var cutters = openings.Select(o => (o, Box: Openings.Cutter(frame, o with { Along = o.Along - Vec2.Dot(start - Start(wall), dir) }))).Where(c => c.Box != null).ToList();
        var booleans = services?.Get<ISolidBooleans>();
        if (cutters.Count > 0 && booleans == null) problems.Add("Openings need the boolean library (manifold), missing here: the wall is drawn uncut.");
        GeometryPart Cut(GeometryPart part, double z0, double z1)
        {
            if (booleans == null) return part;
            var reach = cutters.Where(c => c.o.Sill + z < z1 - 1e-9 && c.o.Sill + c.o.Height + z > z0 + 1e-9).Select(c => c.Box!).ToList();
            if (reach.Count == 0) return part;
            try { return booleans.Difference(part.Solid, reach, part.Solid.Tag) is { } cut ? part with { Solid = cut } : part; }
            catch (InvalidOperationException ex) { problems.Add($"{part.Name}: an opening could not be cut ({ex.Message})."); return part; }
        }
        var gaps = openings.Where(o => o.Sill < PlanCutAbove(m, wall) && o.Sill + o.Height > PlanCutAbove(m, wall))
            .Select(o => (From: o.Along - o.Width / 2 - Vec2.Dot(start - Start(wall), dir), To: o.Along + o.Width / 2 - Vec2.Dot(start - Start(wall), dir))).OrderBy(g => g.From).ToList();
        foreach (var band in bands)
        {
            if (!(band.To - band.From > 1e-9)) continue;
            Vec2[] footprint =
            [
                P(-At(startCut, band.From), band.From), P(length + At(endCut, band.From), band.From),
                P(length + At(endCut, band.To), band.To), P(-At(startCut, band.To), band.To),
            ];
            // The core of a zoned wall is its zones, bottom to top; every other layer runs the whole height.
            if (band.Core && zones.Count > 0)
                foreach (var zb in zones)
                {
                    if (Prism.ExtrudeFootprint(footprint, z + zb.Z0, zb.Z1 - zb.Z0, zb.Zone.Name) is { } piece)
                        parts.Add(Cut(new GeometryPart(zb.Zone.Name, Materials.ColorOf(m, zb.Zone.Material), piece, zb.Zone.Material, "zone:" + zb.Zone.Key), zb.Z0, zb.Z1));
                }
            else if (Prism.ExtrudeFootprint(footprint, z, height, band.Name) is { } solid)
                parts.Add(Cut(new GeometryPart(band.Name, Materials.ColorOf(m, band.Material), solid, band.Material, $"layer:{band.Index}"), 0, height));
            // In plan, the band between the gaps the openings leave at the cut.
            var x0 = -Math.Max(At(startCut, band.From), At(startCut, band.To));
            var x1 = length + Math.Max(At(endCut, band.From), At(endCut, band.To));
            var at = x0;
            foreach (var g in gaps.Append((From: x1, To: x1)))
            {
                var piece = ClipX(footprint, start, dir, at, g.From);
                if (piece.Count >= 3) plan.Add(new PlanItem("cut", true, piece));
                at = Math.Max(at, g.To);
            }
        }
        return new ElementGeometry(parts, plan, [start, end], problems.Count > 0 ? problems : null);
    }

    /// <summary>The plan's cut above the wall's base: the storey's cut height less the wall's base offset.</summary>
    static double PlanCutAbove(IModelView m, Entity wall) => Openings.PlanCut - wall.Num("baseOffset");

    /// <summary>The part of a convex plan polygon between two distances along the wall (from <paramref name="origin"/>, along <paramref name="dir"/>).</summary>
    static List<Vec2> ClipX(IReadOnlyList<Vec2> poly, Vec2 origin, Vec2 dir, double from, double to)
    {
        if (to - from <= 1e-9) return [];
        List<Vec2> Clip(IReadOnlyList<Vec2> pts, Func<Vec2, double> inside)
        {
            var o = new List<Vec2>();
            for (var i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                double da = inside(a), db = inside(b);
                if (da >= 0) o.Add(a);
                if ((da >= 0) != (db >= 0)) o.Add(a + (b - a) * (da / (da - db)));
            }
            return o;
        }
        var first = Clip(poly, p => Vec2.Dot(p - origin, dir) - from);
        return first.Count < 3 ? [] : Clip(first, p => to - Vec2.Dot(p - origin, dir));
    }

    /// <summary>
    /// What a wall measures: Qto_WallBaseQuantities for the wall; for each
    /// part, its zone's set (blocks, mortar, concrete, steel) or its layer's
    /// (Lapin_WallLayerQuantities). Net values come from the built parts.
    /// </summary>
    public static IEnumerable<QuantitySet> Quantities(Entity wall, ElementGeometry? geometry, IModelView m)
    {
        if (geometry == null || geometry.Parts.Count == 0) yield break;
        var length = (End(wall) - Start(wall)).Length;
        var height = wall.Num("height", DefaultHeight);
        var bands = Section(m, wall);
        var width = bands.Max(b => b.To) - bands.Min(b => b.From);
        var volumes = geometry.Parts.ToDictionary(p => p.Key ?? p.Name, p => Measure.Volume(p.Solid));
        var net = volumes.Values.Sum();
        yield return new QuantitySet("Qto_WallBaseQuantities",
        [
            new("Length", length, "LENGTH"),
            new("Width", width, "LENGTH"),
            new("Height", height, "LENGTH"),
            new("GrossSideArea", length * height, "AREA", "Gross side area"),
            new("NetSideArea", width > 0 ? net / width : 0, "AREA", "Net side area"),
            new("GrossVolume", length * height * width, "VOLUME", "Gross volume"),
            new("NetVolume", net, "VOLUME", "Net volume"),
        ]);
        var core = bands.FirstOrDefault(b => b.Core);
        var zones = TypeOf(m, wall.Id) is { } type ? WallZones.Bands(WallZones.Of(type), height) : [];
        foreach (var zb in zones)
            if (volumes.TryGetValue("zone:" + zb.Zone.Key, out var v))
                yield return WallZones.Quantities(zb.Zone, zb.Z0, zb.Z1, length, core.To - core.From, v);
        foreach (var band in bands)
        {
            if (band.Core && zones.Count > 0) continue;
            if (!volumes.TryGetValue($"layer:{band.Index}", out var v)) continue;
            var t = band.To - band.From;
            yield return new QuantitySet("Lapin_WallLayerQuantities",
            [
                new("Width", t, "LENGTH"),
                new("NetSideArea", t > 0 ? v / t : 0, "AREA", "Net side area"),
                new("NetVolume", v, "VOLUME", "Net volume"),
            ], $"layer:{band.Index}", band.Name, band.Material);
        }
    }
}
