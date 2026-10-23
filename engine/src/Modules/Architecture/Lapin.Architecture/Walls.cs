using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Architecture.Roofs;
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
    public static Entity? UsageOf(IModelView m, string wallId) => LayerSets.UsageOf(m, wallId);

    /// <summary>A new wall on the storey, of the type (the project's default when none), joined where it meets the walls already there.</summary>
    public static Entity Add(IEdit edit, IPathElements paths, string storeyId, Vec2 start, Vec2 end, double height, double baseOffset, Entity? type, JoinStyle joinStyle = JoinStyle.Butt)
    {
        var wall = AddUnjoined(edit, storeyId, start, end, height, baseOffset, type);
        Joins.Auto(edit, paths, wall.Id, joinStyle);
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
    public static void LayOut(IEdit edit, Entity wall, Entity type) =>
        LayerSets.LayOut(edit, wall, type, "AXIS2", Layers.CoreOffset);

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
    /// <summary>The wall as joins see it: its axis, its body across it.</summary>
    public static JoinWall? Frame(Entity wall, IModelView m)
    {
        var bands = Section(m, wall);
        return (End(wall) - Start(wall)).Length < MinLength ? null : new JoinWall(Start(wall), End(wall), bands.Min(b => b.From), bands.Max(b => b.To));
    }

    public static ElementGeometry? Geometry(Entity wall, IModelView m, IServices? services = null)
    {
        var start = Start(wall);
        var end = End(wall);
        if ((end - start).Length < MinLength) return new ElementGeometry([], Diagnostics: ["The wall has no length."]);
        var problems = new List<string>();
        var paths = services?.Get<IPathElements>();
        var ends = paths != null ? Joins.EndsOf(m, wall, paths, problems) : new (Vec2 Point, EndCut Cut)?[2];
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
        // Under a roof of its storey: the wall is built up to the highest point of its top, then cut along it.
        var axisDir = (End(wall) - Start(wall)).Normalized();
        var roofTop = RoofTop.Profile(wall, new RoofTop.WallAxis(Start(wall), axisDir, (End(wall) - Start(wall)).Length, z, height, yMax - yMin), m);
        var built = roofTop == null ? height : Math.Max(height, roofTop.Max(p => p.V));

        // Openings take their boxes out of every part they reach; the plan shows the gaps where they cross the cut.
        var openings = Openings.PlacedOn(m, wall.Id);
        var frame = new Openings.WallFrame(start, dir, left, z, yMin, yMax);
        var cutters = openings.Select(o => (o, Box: Openings.Cutter(frame, o with { Along = o.Along - Vec2.Dot(start - Start(wall), dir) }))).Where(c => c.Box != null).ToList();
        var booleans = services?.Get<ISolidBooleans>();
        if (cutters.Count > 0 && booleans == null) problems.Add("Openings need the boolean library (manifold), missing here: the wall is drawn uncut.");
        // Lintels over the openings, through the core; the ring beam coming down over an opening close under it.
        var core = bands.FirstOrDefault(b => b.Core);
        var shift = Vec2.Dot(start - Start(wall), dir);
        var lintels = core.To - core.From > 1e-9 ? Lintels.ForWall(
            openings.Select(o => new LintelOpening(o.Opening.Id, o.Along - o.Width / 2 - shift, o.Along + o.Width / 2 - shift, o.Sill, o.Sill + o.Height,
                o.Spec?.Lintel is { Mode: not "none" } own ? own : null, o.Spec?.Lintel is { Mode: "none" })).ToList(),
            zones, LintelRuleOf(m, wall), (-Math.Max(At(startCut, core.From), At(startCut, core.To)), length + Math.Max(At(endCut, core.From), At(endCut, core.To)), height)) : [];
        var lintelBoxes = new List<(Lintel Lintel, int Piece, Solid Box)>();
        foreach (var l in lintels)
        {
            var slices = l.Kind == "downstand" ? [(core.From, core.To)] : Lintels.Slices(l.Rule, core.From, core.To, outerAtMin: true);
            for (var i = 0; i < slices.Count; i++)
            {
                var (y0, y1) = slices[i];
                Vec2[] fp = [P(l.X0, y0), P(l.X1, y0), P(l.X1, y1), P(l.X0, y1)];
                if (Prism.ExtrudeFootprint(fp, z + l.Z0, l.Z1 - l.Z0, "lintel") is { } box) lintelBoxes.Add((l, i, box));
            }
        }
        GeometryPart Cut(GeometryPart part, double z0, double z1, bool isCore = false)
        {
            if (booleans == null) return part;
            var reach = cutters.Where(c => c.o.Sill + z < z1 - 1e-9 && c.o.Sill + c.o.Height + z > z0 + 1e-9).Select(c => c.Box!)
                .Concat(isCore ? lintelBoxes.Where(b => b.Lintel.Z0 < z1 - 1e-9 && b.Lintel.Z1 > z0 + 1e-9).Select(b => b.Box) : [])
                .ToList();
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
            {
                foreach (var zb in zones)
                {
                    if (Prism.ExtrudeFootprint(footprint, z + zb.Z0, zb.Z1 - zb.Z0, zb.Zone.Name) is { } piece)
                        parts.Add(Cut(new GeometryPart(zb.Zone.Name, Materials.ColorOf(m, zb.Zone.Material), piece, zb.Zone.Material, "zone:" + zb.Zone.Key), zb.Z0, zb.Z1, isCore: true));
                }
                // A gable rising over the storey: the masonry goes on above the ring beam.
                if (built > height + 1e-9 && (zones.LastOrDefault(zb => zb.Zone.Role == "masonry") ?? zones[^1]).Zone is var gz
                    && Prism.ExtrudeFootprint(footprint, z + height, built - height, "gable") is { } gable)
                    parts.Add(Cut(new GeometryPart($"Gable ({gz.Name})", Materials.ColorOf(m, gz.Material), gable, gz.Material, "zone:gable"), height, built, isCore: true));
            }
            else if (Prism.ExtrudeFootprint(footprint, z, built, band.Name) is { } solid)
                parts.Add(Cut(new GeometryPart(band.Name, Materials.ColorOf(m, band.Material), solid, band.Material, $"layer:{band.Index}"), 0, built, isCore: band.Core));
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
        parts = Cuts.Apply(wall, m, services, parts, problems);
        if (roofTop != null)
        {
            if (booleans == null) problems.Add("The roof needs the boolean library (manifold), missing here: the wall keeps its height.");
            else if (AboveRoof(roofTop, shift, start, dir, left, z, yMin, yMax, built) is { } above)
                parts = parts.Select(part =>
                {
                    try { return booleans.Difference(part.Solid, [above], part.Solid.Tag) is { } cut && Measure.Volume(cut) > 1e-9 ? part with { Solid = cut } : null; }
                    catch (InvalidOperationException ex) { problems.Add($"{part.Name}: the roof could not be cut ({ex.Message})."); return part; }
                }).OfType<GeometryPart>().ToList();
        }
        if (booleans != null)
        {
            var ring = zones.FirstOrDefault(zb => zb.Zone.Role == "ringBeam");
            foreach (var (l, i, box) in lintelBoxes)
            {
                var material = l.Kind == "downstand" ? ring?.Zone.Material : l.Rule?.Material is { } name ? Materials.Find(m, name)?.Id : Materials.Find(m, "Reinforced concrete")?.Id;
                var color = material != null ? Materials.ColorOf(m, material) : "#a8a8a8";
                var label = l.Kind == "downstand" ? "Ring beam over opening" : l.Precast ? "Lintel (precast)" : "Lintel (cast)";
                parts.Add(new GeometryPart(label, color, box, material, $"{l.Kind}:{l.Key}:{i}"));
            }
        }
        return new ElementGeometry(parts, plan, [start, end], problems.Count > 0 ? problems : null);
    }

    /// <summary>
    /// What a roof takes off the wall: the region above its top profile (u along
    /// the wall's own axis, v above its base), across the whole thickness. The
    /// profile runs on in line past the wall's ends, where joins reach.
    /// </summary>
    static Solid? AboveRoof(List<(double U, double V)> profile, double shift, Vec2 start, Vec2 dir, Vec2 left, double z, double yMin, double yMax, double built)
    {
        const double reach = 2;
        var sky = built + 1;
        double Run((double U, double V) a, (double U, double V) b, double u) => Math.Abs(b.U - a.U) < 1e-9 ? a.V : a.V + (b.V - a.V) * (u - a.U) / (b.U - a.U);
        var (first, last) = (profile[0], profile[^1]);
        var pts = new List<(double U, double V)> { (first.U - reach, sky) };
        pts.Add((first.U - reach, profile.Count > 1 ? Math.Min(sky - 0.5, Run(first, profile[1], first.U - reach)) : first.V));
        pts.AddRange(profile);
        pts.Add((last.U + reach, profile.Count > 1 ? Math.Min(sky - 0.5, Run(profile[^2], last, last.U + reach)) : last.V));
        pts.Add((last.U + reach, sky));
        const double pad = 0.05;
        // Clockwise seen from the left face: the extrusion orients it.
        var ring = pts.Select(p => { var q = start + dir * (p.U - shift) + left * (yMin - pad); return new Vec3(q.X, q.Y, z + p.V); }).ToList();
        var across = left * (yMax - yMin + 2 * pad);
        return Prism.Extrude(ring, null, new Vec3(across.X, across.Y, 0), "roof");
    }

    /// <summary>The wall's body as drawn, its joins aside: what it cuts lower priorities with.</summary>
    public static Solid? Envelope(Entity wall, IModelView m)
    {
        var start = Start(wall);
        var end = End(wall);
        if ((end - start).Length < MinLength) return null;
        var left = (end - start).Normalized().Perp;
        var bands = Section(m, wall);
        double y0 = bands.Min(b => b.From), y1 = bands.Max(b => b.To);
        var z = (Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");
        Vec2[] fp = [start + left * y0, end + left * y0, end + left * y1, start + left * y1];
        return Prism.ExtrudeFootprint(fp, z, wall.Num("height", DefaultHeight), "wall");
    }

    /// <summary>The lintel rule of the wall's type: its own, else the default (precast, 25 cm bearing, 25 cm high); mode none, none.</summary>
    public static LintelRule? LintelRuleOf(IModelView m, Entity wall)
    {
        var rule = TypeOf(m, wall.Id) is { } type && type.Attrs["lintels"] is { } node ? LintelRule.From(node) : LintelRule.Default;
        return rule is { Mode: not "none" } ? rule : null;
    }

    /// <summary>A prism's width across its length: its volume over its height and its longest horizontal extent.</summary>
    static double SectionWidth(Solid s, double height)
    {
        var b = Measure.Bounds(s)!.Value;
        var longest = Math.Max(b.Max.X - b.Min.X, b.Max.Y - b.Min.Y);
        return longest > 0 && height > 0 ? Measure.Volume(s) / (height * longest) : 0;
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
        // Lintels and downstands: one set per lintel, its pieces together.
        foreach (var group in geometry.Parts.Where(p => p.Key is { } k && (k.StartsWith("lintel:") || k.StartsWith("downstand:"))).GroupBy(p => p.Key![..p.Key!.LastIndexOf(':')]))
        {
            var volume = group.Sum(p => Measure.Volume(p.Solid));
            var bounds = group.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
            var h = bounds.Max(b => b.Max.Z) - bounds.Min(b => b.Min.Z);
            var lintelLength = h > 0 && volume > 0 ? volume / (h * Math.Max(1e-9, group.Sum(p => SectionWidth(p.Solid, h)))) : 0;
            var q = new List<Quantity>
            {
                new("Length", lintelLength, "LENGTH"),
                new("CrossSectionArea", lintelLength > 0 ? volume / lintelLength : 0, "AREA", "Cross section"),
                new("NetVolume", volume, "VOLUME", "Net volume"),
            };
            yield return new QuantitySet("Qto_BeamBaseQuantities", q, group.Key, group.First().Name, group.First().Material);
        }
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
