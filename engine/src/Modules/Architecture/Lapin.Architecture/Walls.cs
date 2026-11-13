using System.Text.Json.Nodes;
using Lapin.Geometry;
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

    static JsonArray Array(Vec2 p) => new(p.X, p.Y);

    public static JsonObject Attrs(Vec2 start, Vec2 end, double height, double baseOffset, double? thickness = null)
    {
        var o = new JsonObject { ["start"] = Array(start), ["end"] = Array(end), ["height"] = height, ["baseOffset"] = baseOffset };
        if (thickness != null) o["thickness"] = thickness;
        return o;
    }

    /// <summary>The wall's height, worked out on every ask: to its top (<see cref="WallTop"/>), else its own.</summary>
    public static double HeightOf(IModelView m, Entity wall) => WallTop.Height(m, wall);

    public static Entity? TypeOf(IModelView m, string wallId) =>
        m.RelatingOf(wallId, IfcClass.RelDefinesByType) is { Class: IfcClass.WallType } t ? t : null;

    /// <summary>The wall's own layer set usage, if it has one.</summary>
    public static Entity? UsageOf(IModelView m, string wallId) => LayerSets.UsageOf(m, wallId);

    /// <summary>A new wall on the storey, of the type (the project's default when none), joined where it meets the walls already there.</summary>
    public static Entity Add(IEdit edit, IPathElements paths, string storeyId, Vec2 start, Vec2 end, double height, double baseOffset, Entity? type, JoinStyle joinStyle = JoinStyle.Mitre)
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
        // A wall still named after its old type (Add names it so) takes the new type's name; a name given to it stays (issue #16).
        if (TypeOf(edit.Model, wall.Id) is { } old && old.Id != type.Id && edit.Model.Get(wall.Id) is { } current && current.Name == old.Name)
            edit.Replace(wall = current.With(name: type.Name));
        edit.Relate(IfcClass.RelDefinesByType, type.Id, wall.Id);
        LayOut(edit, wall, type);
        // The type's sweeps and reveals (decision 298).
        WallSweepTypes.Rebuild(edit, wall);
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
        LayerSets.Flip(edit, wall.Id);
        // Its type's sweeps go with the type's faces.
        WallSweepTypes.Rebuild(edit, wall);
    }

    /// <summary>A layer band of the wall's section: across the axis (left of it positive), its material, whether it is the core, its priority at joins (<see cref="Layers.Rank"/>).</summary>
    public readonly record struct Band(double From, double To, string? Material, string Name, bool Core = false, int Index = 0, double? Z0 = null, double? Z1 = null, IReadOnlyList<LayerPart>? Parts = null, int Rank = Layers.CoreRank,
        bool ExtendDown = false, bool ExtendUp = false);

    /// <summary>The wall's section, first layer to last; one centred band for a wall without layers.</summary>
    public static IReadOnlyList<Band> Section(IModelView m, Entity wall)
    {
        if (LayerSets.BandsOf(m, wall.Id) is { } bands) return bands.Select(b => new Band(b.From, b.To, b.Material, b.Name, b.Core, b.Index, b.Z0, b.Z1, b.Parts, b.Rank, b.ExtendDown, b.ExtendUp)).ToList();
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

    /// <summary>
    /// A straight wall laid out as its joins leave it: its ends, its frame (start, direction, length), its base and height,
    /// its section, and each band's footprint in the pieces the walls meeting it leave of it.
    /// </summary>
    public sealed record Layout(Vec2 Start, Vec2 End, Vec2 Dir, double Length, double Z, double Height, IReadOnlyList<Band> Bands, Func<Band, List<IReadOnlyList<Vec2>>> Footprints);

    /// <summary>The wall's <see cref="Layout"/>; null for a curved wall or one of no length.</summary>
    public static Layout? LayoutOf(Entity wall, IModelView m, IServices? services, List<string> problems)
    {
        var start = Start(wall);
        var end = End(wall);
        if ((end - start).Length < MinLength || PathCurve.Of(wall) is { IsArc: true }) return null;
        var paths = services?.Get<IPathElements>();
        var ends = Joins.WithOwnEnds(paths != null ? Joins.EndsOf(m, wall, paths, problems) : new (Vec2 Point, EndCut Cut)?[2], wall, Frame(wall, m));
        if (ends[0] is { } s) start = s.Point;
        if (ends[1] is { } e) end = e.Point;
        var startCut = ends[0]?.Cut ?? EndCut.Square;
        var endCut = ends[1]?.Cut ?? EndCut.Square;
        var along = end - start;
        var length = along.Length;
        var dir = along.Normalized();
        var left = dir.Perp;
        var z = (Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");
        var bands = Section(m, wall);
        var yMin = bands.Min(b => b.From);
        var yMax = bands.Max(b => b.To);
        double At(EndCut c, double y) => yMax - yMin < 1e-12 ? c.Right : c.Right + (c.Left - c.Right) * (y - yMin) / (yMax - yMin);
        Vec2 P(double x, double y) => start + dir * x + left * y;
        var cutOrder = services?.Get<ICutters>();
        Func<Entity, int?> ranked = x => cutOrder?.PriorityOf(x, m);
        var corners = paths != null ? LayerCorners(m, wall, paths, start, dir, length, bands, ranked) : new Func<Band, (double From, double To)>?[2];
        var crossings = paths != null ? LayerCrossings(m, wall, paths, start, dir, bands, ranked) : _ => [];
        double Reach(int end, Band b, bool atFrom) =>
            corners[end] is { } f ? (atFrom ? f(b).From : f(b).To) : At(end == 0 ? startCut : endCut, atFrom ? b.From : b.To) + (ends[end] == null ? LayerEnd(wall, b.Index, end, atFrom) : 0);
        List<IReadOnlyList<Vec2>> Footprints(Band band)
        {
            Vec2[] whole =
            [
                P(-Reach(0, band, true), band.From), P(length + Reach(1, band, true), band.From),
                P(length + Reach(1, band, false), band.To), P(-Reach(0, band, false), band.To),
            ];
            return Pieces(whole, crossings(band), start, dir, length);
        }
        return new Layout(start, end, dir, length, z, HeightOf(m, wall), bands, Footprints);
    }

    /// <summary>
    /// A wall layer's piece in a floor's layers (the forum's topic #30): what the layer adds where it goes on down into the
    /// floor it stands on (<paramref name="Down"/>) or up into the floor over it, within that floor's outline; its band, and the
    /// floor it takes the piece out of.
    /// </summary>
    public sealed record Extension(string Slab, Band Band, Solid Solid, bool Down);

    /// <summary>How near (metres) a wall's base or top must come to a floor to stand on it or meet it.</summary>
    const double FloorTouch = 0.005;

    /// <summary>
    /// The pieces the wall's layers marked to extend (<see cref="MaterialLayer.ExtendDown"/>, <see cref="MaterialLayer.ExtendUp"/>)
    /// add in the floors they meet: down, from the wall's base through the layers of a floor it stands on (its base at the
    /// floor's top, or in it) ranked under the layer, to the top of the first ranked as high or higher (the floor's bottom when
    /// none is); up, from the wall's top (the floor's underside, when the floor outranks the wall and so cuts its head) through
    /// the layers of a floor its top meets, to the underside of the first ranked as high (its top when none is). Ranks are the
    /// layers' (<see cref="Layers.Rank"/>), as where walls join. Each piece is the band's footprint between the two heights, kept
    /// to the floor's outline. None for a curved wall, a wall without such a layer, or without the boolean library.
    /// </summary>
    public static List<Extension> Extensions(Entity wall, IModelView m, IServices? services, Layout? layout = null)
    {
        var o = new List<Extension>();
        var bands = layout?.Bands ?? Section(m, wall);
        if (!bands.Any(b => b.ExtendDown || b.ExtendUp) || services?.Get<ISolidBooleans>() is not { } booleans) return o;
        layout ??= LayoutOf(wall, m, services, []);
        if (layout == null) return o;
        var z = layout.Z;
        var top = z + layout.Height;
        var cutters = services.Get<ICutters>();
        var mine = cutters?.PriorityOf(wall, m);
        var footprints = layout.Bands.Where(b => (b.ExtendDown || b.ExtendUp) && b.To - b.From > 1e-9).Select(b => (Band: b, Pieces: layout.Footprints(b))).Where(x => x.Pieces.Count > 0).ToList();
        if (footprints.Count == 0) return o;
        var pts = footprints.SelectMany(x => x.Pieces.SelectMany(p => p)).ToList();
        var (x0, y0, x1, y1) = (pts.Min(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.X), pts.Max(p => p.Y));
        foreach (var slab in m.OfClass(IfcClass.Slab))
        {
            var contour = Slabs.Contour(slab);
            if (contour.Count < 3 || contour.Max(p => p.X) <= x0 || contour.Min(p => p.X) >= x1 || contour.Max(p => p.Y) <= y0 || contour.Min(p => p.Y) >= y1) continue;
            if (LayerSets.BandsOf(m, slab.Id) is not { Count: > 0 } layers) continue;
            var slabTop = Slabs.Top(m, slab);
            var bottom = slabTop + layers.Min(l => l.From);
            // Whether the floor cuts the wall where they overlap (its priority over the wall's): then the wall's body stops at its face.
            var outranks = cutters?.PriorityOf(slab, m) is { } theirs && mine is { } wallRank && theirs > wallRank;
            var below = z >= bottom - FloorTouch && z <= slabTop + FloorTouch;
            var above = !below && bottom > z + FloorTouch && bottom <= top + FloorTouch;
            if (!below && !above) continue;
            Solid? region = null;
            foreach (var (band, pieces) in footprints)
            {
                double lo, hi;
                if (below && band.ExtendDown)
                {
                    // Down from the top: through the layers ranked under the wall's, onto the first as high.
                    var stop = layers.OrderByDescending(l => l.To).FirstOrDefault(l => l.Rank >= band.Rank) is { To: var t } first && first.Material != null ? slabTop + t : bottom;
                    (lo, hi) = (stop, outranks && slabTop > z ? slabTop : z);
                }
                else if (above && band.ExtendUp)
                {
                    // Up from the underside: through the layers ranked under the wall's, to the underside of the first as high.
                    var stop = layers.OrderBy(l => l.From).FirstOrDefault(l => l.Rank >= band.Rank) is { From: var f } first && first.Material != null ? slabTop + f : slabTop;
                    (lo, hi) = (outranks && top > bottom ? bottom : top, stop);
                }
                else continue;
                if (hi - lo <= 1e-6) continue;
                region ??= Prism.ExtrudeFootprint(contour, bottom - 1, slabTop - bottom + 2, "floor");
                foreach (var piece in pieces)
                {
                    if (Prism.ExtrudeFootprint(piece, lo, hi - lo, band.Name) is not { } prism) continue;
                    // Kept to the floor's outline: a wall only half on this floor goes down only where the floor is.
                    var inside = piece.All(p => Polygon2.Contains(contour, p));
                    Solid? kept;
                    try { kept = inside ? prism : region != null ? booleans.Intersection(prism, region, prism.Tag) : null; }
                    catch (InvalidOperationException) { kept = null; }
                    if (kept != null && Measure.Volume(kept) > 1e-9) o.Add(new Extension(slab.Id, band, kept, below));
                }
            }
        }
        return o;
    }

    public static ElementGeometry? Geometry(Entity wall, IModelView m, IServices? services = null)
    {
        var start = Start(wall);
        var end = End(wall);
        if ((end - start).Length < MinLength) return new ElementGeometry([], Diagnostics: ["The wall has no length."]);
        if (PathCurve.Of(wall) is { IsArc: true } arc) return ArcGeometry(wall, arc, m, services);
        var problems = new List<string>();
        if (WallTop.Problem(m, wall) is { } topProblem) problems.Add(topProblem);
        var paths = services?.Get<IPathElements>();
        // Its ends where its joins put them; a free end cut at its own angle (startAngle, endAngle).
        var ends = Joins.WithOwnEnds(paths != null ? Joins.EndsOf(m, wall, paths, problems) : new (Vec2 Point, EndCut Cut)?[2], wall, Frame(wall, m));
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
        var height = HeightOf(m, wall);

        var bands = Section(m, wall);
        var yMin = bands.Min(b => b.From);
        var yMax = bands.Max(b => b.To);
        // A cut at a face: Right at yMin, Left at yMax, straight in between.
        double At(EndCut c, double y) => yMax - yMin < 1e-12 ? c.Right : c.Right + (c.Left - c.Right) * (y - yMin) / (yMax - yMin);
        Vec2 P(double x, double y) => start + dir * x + left * y;
        // At a corner, each layer turns it on its own (report #49): finishes meet their like, the cores meet as the join's style says.
        // Where a wall ends on this one or crosses it, the layers stop short of its core, or go through it (decision 255), each by its priority (report #81).
        var cutOrder = services?.Get<ICutters>();
        Func<Entity, int?> ranked = e => cutOrder?.PriorityOf(e, m);
        var corners = paths != null ? LayerCorners(m, wall, paths, start, dir, length, bands, ranked) : new Func<Band, (double From, double To)>?[2];
        var crossings = paths != null ? LayerCrossings(m, wall, paths, start, dir, bands, ranked) : _ => [];
        double Reach(int end, Band b, bool atFrom) =>
            corners[end] is { } f ? (atFrom ? f(b).From : f(b).To) : At(end == 0 ? startCut : endCut, atFrom ? b.From : b.To) + (ends[end] == null ? LayerEnd(wall, b.Index, end, atFrom) : 0);

        var zones = TypeOf(m, wall.Id) is { } type ? WallZones.Bands(WallZones.Of(type), height) : [];
        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();
        // Under a roof of its storey, or a body it is attached to: the wall is built up to the highest point of its top, then cut along it.
        var axisDir = (End(wall) - Start(wall)).Normalized();
        var roofTop = WallTops.Profile(wall, new RoofTop.WallAxis(Start(wall), axisDir, (End(wall) - Start(wall)).Length, z, height, yMax - yMin), m, services);
        var built = roofTop == null ? height : Math.Max(height, roofTop.Max(p => p.V));

        // Openings take their boxes out of every part they reach; the plan shows the gaps where they cross the cut.
        var openings = Openings.PlacedOn(m, wall.Id);
        var frame = new Openings.WallFrame(start, dir, left, z, yMin, yMax);
        var cutters = openings.SelectMany(o => Openings.Cutters(m, wall, frame, o with { Along = o.Along - Vec2.Dot(start - Start(wall), dir) })).Select(c => (o: c.Reach, Box: (Solid?)c.Box)).ToList();
        // An export wants the body the openings have not cut: IFC writes the openings and subtracts them itself.
        if (!(services?.Get<GeometryOptions>()?.CutOpenings ?? true)) cutters.Clear();
        var booleans = services?.Get<ISolidBooleans>();
        if (cutters.Count > 0 && booleans == null) problems.Add("Openings need the boolean library (manifold), missing here: the wall is drawn uncut.");
        // Lintels over the openings, through the core; the ring beam coming down over an opening close under it; at a corner, the longer of two that meet runs through.
        var shift = Vec2.Dot(start - Start(wall), dir);
        var lintelBoxes = new List<(Lintel Lintel, int Piece, Solid Box)>();
        var lintelsIn = LintelJoins.Resolve(wall, LintelPieces(wall, m, services), m, services, out var intoCore);
        foreach (var r in lintelsIn)
        {
            if (r.Piece.Box() is not { } box) continue;
            if (r.CutBy.Count > 0 && booleans != null)
            {
                try { box = booleans.Difference(box, r.CutBy, box.Tag) ?? box; }
                catch (InvalidOperationException ex) { problems.Add($"A lintel could not be cut at its corner ({ex.Message})."); }
            }
            if (Measure.Volume(box) > 1e-9) lintelBoxes.Add((r.Piece.Lintel, r.Piece.Piece, box));
        }
        GeometryPart Cut(GeometryPart part, double z0, double z1, bool isCore = false)
        {
            if (booleans == null) return part;
            var reach = cutters.Where(c => c.o.Sill < z1 - 1e-9 && c.o.Sill + c.o.Height > z0 + 1e-9).Select(c => c.Box!)
                .Concat(isCore ? lintelBoxes.Where(b => b.Lintel.Z0 < z1 - 1e-9 && b.Lintel.Z1 > z0 + 1e-9).Select(b => b.Box) : [])
                .Concat(isCore ? intoCore.Where(b => b.Z0 - z < z1 - 1e-9 && b.Z1 - z > z0 + 1e-9).Select(b => b.Box) : [])
                .ToList();
            if (reach.Count == 0) return part;
            try { return booleans.Difference(part.Solid, reach, part.Solid.Tag) is { } cut ? part with { Solid = cut } : part; }
            catch (InvalidOperationException ex) { problems.Add($"{part.Name}: an opening could not be cut ({ex.Message})."); return part; }
        }
        var layered = LayerSets.BandsOf(m, wall.Id);
        var gaps = openings.Where(o => o.Sill < PlanCutAbove(m, wall) && o.Sill + o.Height > PlanCutAbove(m, wall))
            .Select(o => (From: o.Along - o.Width / 2 - Vec2.Dot(start - Start(wall), dir), To: o.Along + o.Width / 2 - Vec2.Dot(start - Start(wall), dir))).OrderBy(g => g.From).ToList();
        foreach (var band in bands)
        {
            if (!(band.To - band.From > 1e-9)) continue;
            Vec2[] whole =
            [
                P(-Reach(0, band, true), band.From), P(length + Reach(1, band, true), band.From),
                P(length + Reach(1, band, false), band.To), P(-Reach(0, band, false), band.To),
            ];
            // The band in the pieces the walls meeting this one leave of it (one, when none interrupts it).
            foreach (var footprint in Pieces(whole, crossings(band), start, dir, length))
            {
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
                // A layer of its own height (report #44): from its bottom to its top over the wall's base, never above the wall.
                else if (LayerSpan(band, built) is var (lz0, lz1))
                {
                    // Its pieces in height (decision 264), each in its own material; one, of the layer's, for most layers.
                    foreach (var h in HeightPieces(m, band, lz0, lz1))
                        if (Prism.ExtrudeFootprint(footprint, z + h.Z0, h.Z1 - h.Z0, h.Name) is { } solid)
                            parts.Add(Cut(new GeometryPart(h.Name, Materials.ColorOf(m, h.Material), solid, h.Material, $"layer:{band.Index}"), h.Z0, h.Z1, isCore: band.Core));
                }
                // In plan, the band between the gaps the openings leave at the cut.
                var x0 = footprint.Min(q => Vec2.Dot(q - start, dir));
                var x1 = footprint.Max(q => Vec2.Dot(q - start, dir));
                var at = x0;
                foreach (var g in gaps.Append((From: x1, To: x1)))
                {
                    var piece = ClipX(footprint, start, dir, at, g.From);
                    // With its layer's subcategory, so a plan without the finishes snaps to what it draws (topic #30).
                    if (piece.Count >= 3) plan.Add(new PlanItem("cut", true, piece, Sub: layered != null && band.Index < layered.Count ? LayerSets.SubOf(m, layered[band.Index]) : null));
                    at = Math.Max(at, g.To);
                }
            }
        }
        // The roof first: the wall comes down under it, so the cut priorities after it have only what is left to settle (cut the other way round,
        // a wall built up to the ridge was first cut by the roof's framing into slivers the roof's own cut could not take).
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
        parts = CutPriorities.Apply(wall, m, services, parts, problems);
        // The layers going on into the floors' layers ranked under them (the forum's topic #30): added after the cut priorities, which
        // would take them out again (the floor outranks the wall); the floor takes the same pieces out of its own layers.
        foreach (var x in Extensions(wall, m, services))
        {
            var b = x.Band;
            GeometryPart piece;
            if (b.Core && zones.Count > 0)
            {
                var zone = (x.Down ? zones[0] : zones[^1]).Zone;
                piece = new GeometryPart(zone.Name, Materials.ColorOf(m, zone.Material), x.Solid, zone.Material, "zone:" + zone.Key);
            }
            else
            {
                // A layer of several materials in its height: its bottom piece's below, its own above.
                var material = x.Down && b.Parts is { Count: > 0 } ps ? ps[0].Material : b.Material;
                piece = new GeometryPart(b.Name, Materials.ColorOf(m, material), x.Solid, material, $"layer:{b.Index}");
            }
            // One body with the part of the layer it goes on from (no seam drawn across it at the floor's face).
            var xb = Measure.Bounds(x.Solid)!.Value;
            var at = parts.FindIndex(p => p.Key == piece.Key && p.Material == piece.Material && Measure.Bounds(p.Solid) is { } pb
                && pb.Min.X < xb.Max.X - 1e-6 && pb.Max.X > xb.Min.X + 1e-6 && pb.Min.Y < xb.Max.Y - 1e-6 && pb.Max.Y > xb.Min.Y + 1e-6
                && (x.Down ? Math.Abs(pb.Min.Z - xb.Max.Z) < 1e-6 : Math.Abs(pb.Max.Z - xb.Min.Z) < 1e-6));
            Solid? joined = null;
            if (at >= 0 && booleans != null)
            {
                try { joined = booleans.Union([parts[at].Solid, x.Solid], parts[at].Solid.Tag); }
                catch (InvalidOperationException) { joined = null; }
            }
            if (joined != null) parts[at] = parts[at] with { Solid = joined };
            else parts.Add(piece);
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
        // A bare opening's reveal finished (decision 392): a lining of its jambs and head, inside its hole.
        foreach (var o in openings)
            if (BareOpenings.Lining(m, o, frame, o.Along - shift) is { } lining) parts.Add(lining);
        return new ElementGeometry(parts, plan, [start, end], problems.Count > 0 ? problems : null);
    }

    /// <summary>
    /// How far each layer's two faces run on past the wall's free ends (<c>layerEnds</c>: per layer, [start at its first face,
    /// start at its second, end at its first, end at its second], metres beyond the end, negative short of it), as a file
    /// gives a wall whose layers end in steps or at a slant (ArchiCAD's parts: <see cref="ForeignWallParts"/>). An end a join
    /// takes is the join's.
    /// </summary>
    public const string LayerEndsKey = "layerEnds";

    static double LayerEnd(Entity wall, int layer, int end, bool atFrom) =>
        wall.Attrs[LayerEndsKey] is JsonArray all && layer < all.Count && all[layer] is JsonArray { Count: 4 } x && Json.Number(x[end * 2 + (atFrom ? 0 : 1)]) is { } v ? v : 0;

    /// <summary>
    /// A layer's pieces in its height between <paramref name="z0"/> and <paramref name="z1"/> (decision 264): its parts from
    /// its bottom up, each as high as it says, then the rest in the layer's own material; what goes past the top is cut there.
    /// </summary>
    internal static List<(double Z0, double Z1, string? Material, string Name)> HeightPieces(IModelView m, Band band, double z0, double z1)
    {
        var o = new List<(double, double, string?, string)>();
        var at = z0;
        foreach (var p in band.Parts ?? [])
        {
            if (at >= z1 - 1e-9) break;
            var top = Math.Min(z1, at + p.Height);
            if (top - at > 1e-9) o.Add((at, top, p.Material, p.Name ?? m.Get(p.Material)?.Name ?? band.Name));
            at = top;
        }
        if (z1 - at > 1e-9) o.Add((at, z1, band.Material, band.Name));
        return o;
    }

    /// <summary>A layer's height over the wall's base: its own bottom and top, else the whole of what is built; null when nothing of it is left.</summary>
    static (double Z0, double Z1)? LayerSpan(Band band, double built)
    {
        var z0 = Math.Clamp(band.Z0 ?? 0, 0, built);
        var z1 = Math.Clamp(band.Z1 ?? built, 0, built);
        return z1 - z0 > 1e-6 ? (z0, z1) : null;
    }

    /// <summary>
    /// How each layer ends at the wall's corners (L joins with a straight wall), report #49: a layer
    /// is cut by its own line, not by the one line the join cuts the whole body by, so the corner
    /// reads as built. The layers are paired from the core outwards on each side of the corner (the
    /// outer side's first finish with the other wall's first, and so on; one with no partner ends
    /// on the other wall's last face on that side), and each pair meets on the line between where
    /// their edges cross: a finish turns the corner unbroken. The cores meet as the join's style
    /// says: mitred, on their own diagonal; butt, the core of the wall that runs through goes on to
    /// the other core's outer face and the other core stops at its inner face. A core of a higher
    /// priority than the other's (report #81: its layer's, else its material's) runs through whatever
    /// the style; of equal ones at a butt corner, the wall of the higher element priority, else the
    /// one the join says. A wall of one layer ends exactly as the join cuts it. Per end, a band's
    /// reach past the joint at its From and To edges; null where the end has no such corner. At a T
    /// (decision 255) the ending wall's layers end each on its own line too: each goes on into the
    /// crossed wall through its layers of a lower priority and stops at the first as high or higher
    /// (<see cref="TeeStop"/>): by default its core runs through the crossed wall's finishes to that
    /// wall's core, its other layers stop at the crossed wall's outer face, where they meet their like.
    /// The element priorities never take a wall into the other's core: what is joined is settled here.
    /// </summary>
    static Func<Band, (double From, double To)>?[] LayerCorners(IModelView m, Entity wall, IPathElements paths, Vec2 start, Vec2 dir, double length, IReadOnlyList<Band> bands, Func<Entity, int?> ranked)
    {
        var o = new Func<Band, (double From, double To)>?[2];
        foreach (var r in Joins.Of(m, wall.Id))
        {
            var join = Joins.Compute(m, r, paths, out _);
            if (join == null || join.Kind is not (JoinKind.L or JoinKind.T)) continue;
            var side = r.Relating == wall.Id ? join.A : join.B;
            var i = side.Connection switch { Connection.AtStart => 0, Connection.AtEnd => 1, _ => -1 };
            if (i < 0 || o[i] != null || m.Get(Joins.Other(r, wall.Id)) is not { } other) continue;
            // A node of three walls or more: the layers follow the body's fan cut (Joins.Fan), not one corner of the pairs.
            if (Joins.IsNode(m, wall, i)) continue;
            var elements = Math.Sign((ranked(wall) ?? 0) - (ranked(other) ?? 0));
            o[i] = LayerCorner(m, start, dir, length, i == 0, bands, other, join.Style, side.RunsThrough, join.Kind == JoinKind.T, elements);
        }
        return o;
    }

    /// <summary>The rank of a wall's core at a join: its core layer's priority (the highest of its core's).</summary>
    static int CoreRank(IReadOnlyList<Band> bands) =>
        bands.Where(b => b.Core).Select(b => b.Rank).DefaultIfEmpty(bands.OrderByDescending(b => b.To - b.From).First().Rank).Max();

    /// <summary>
    /// Whether this wall's core runs through the other's where they meet at a corner or cross (report #81): the higher
    /// core priority does; of equal ones the higher element priority (<paramref name="elements"/>: the sign of mine less
    /// theirs) when <paramref name="byElement"/>; then <paramref name="fallback"/> (the join's own choice), null for none.
    /// </summary>
    static bool? CoreThrough(IReadOnlyList<Band> mine, IReadOnlyList<Band> theirs, int elements, bool byElement, bool? fallback)
    {
        var cores = CoreRank(mine).CompareTo(CoreRank(theirs));
        if (cores != 0) return cores > 0;
        if (byElement && elements != 0) return elements > 0;
        return fallback;
    }

    /// <summary>
    /// Where a layer of a wall coming into another (at a T, or crossing it) stops, as an edge of the other wall across its
    /// axis (report #81): it goes on from the face it comes in at through the other's layers ranked under it and stops at
    /// the first ranked as high or higher (an equal one holds it: the wall it comes into keeps its layer), else at the far
    /// face. <paramref name="side"/>: +1 when it comes in from the other wall's left (its higher edges), −1 from its right.
    /// </summary>
    static double TeeStop(Band layer, IReadOnlyList<Band> other, int side)
    {
        var order = side > 0 ? other.OrderByDescending(b => b.To).ThenByDescending(b => b.From) : other.OrderBy(b => b.From).ThenBy(b => b.To);
        foreach (var b in order)
            if (b.Rank >= layer.Rank) return side > 0 ? b.To : b.From;
        return side > 0 ? other.Min(b => b.From) : other.Max(b => b.To);
    }

    /// <summary>One corner's layer ends (see <see cref="LayerCorners"/>); null when the other wall is curved or parallel.</summary>
    static Func<Band, (double From, double To)>? LayerCorner(IModelView m, Vec2 start, Vec2 dir, double length, bool atStart, IReadOnlyList<Band> bands, Entity other, JoinStyle style, bool runsThrough, bool tee, int elements)
    {
        if (PathCurve.Of(other) is { IsArc: true }) return null;
        var (os, oe) = (Start(other), End(other));
        if ((oe - os).Length < MinLength) return null;
        var od = (oe - os).Normalized();
        var cross = Vec2.Cross(dir, od);
        if (Math.Abs(cross) < 1e-6) return null;
        var left = dir.Perp;
        var joint = atStart ? start : start + dir * length;
        var myFar = atStart ? start + dir * length : start;
        var otherFar = (os - joint).Length > (oe - joint).Length ? os : oe;
        // The side each wall's inner face is on (the other wall's side), in its own frame (+1: its left).
        var mine = Math.Sign(Vec2.Dot(otherFar - joint, left));
        var theirs = Math.Sign(Vec2.Dot(myFar - joint, od.Perp));
        if (mine == 0 || theirs == 0) return null;
        var theirBands = Section(m, other);
        var me = new CornerEdges(bands, mine);
        var them = new CornerEdges(theirBands, theirs);
        Vec2 Mine(double y) => start + left * y;
        Vec2 Theirs(double y) => os + od.Perp * y;
        double ReachTo(double y, double yOther)
        {
            // Where my edge line at y crosses the other wall's at yOther, as a reach past my end.
            var p = Mine(y);
            var s = Vec2.Cross(Theirs(yOther) - p, od) / cross;
            return atStart ? -s : s - length;
        }
        // At a corner, whether my core runs through the other's (null: they meet on the mitre).
        var through = CoreThrough(bands, theirBands, elements, style == JoinStyle.Butt, style == JoinStyle.Butt ? runsThrough : null);
        return band =>
        {
            if (band.To - band.From < 1e-12) return (0, 0);
            if (tee)
            {
                // Ending on the other wall's run: each layer into it as far as its priority takes it.
                var stop = TeeStop(band, theirBands, theirs);
                return (ReachTo(band.From, stop), ReachTo(band.To, stop));
            }
            var core = band.From >= me.CoreLo - 1e-9 && band.To <= me.CoreHi + 1e-9;
            if (core && through is { } t)
            {
                var stop = t ? them.Outer[0] : them.Inner[0];
                return (ReachTo(band.From, stop), ReachTo(band.To, stop));
            }
            return (ReachTo(band.From, them.Partner(me, band.From)), ReachTo(band.To, them.Partner(me, band.To)));
        };
    }

    /// <summary>A straight line of the plan, where a layer stops: a point on it and its unit direction.</summary>
    readonly record struct Cutter(Vec2 Point, Vec2 Dir);

    /// <summary>
    /// Where the layers are interrupted along the wall by the walls that meet its run (decision 255, report #81).
    /// At a T, each layer of the ending wall comes in from its side as far as its priority takes it
    /// (<see cref="TeeStop"/>), and my layers it passes are interrupted between its edges: by default the
    /// ending wall's core through my finishes on that side, to my core. At a crossing (X) the wall whose core
    /// ranks higher runs through (of equal ones, the higher element priority, else the one the join says);
    /// the other's layers come into it from both sides the same way, each interrupted between the edges it
    /// stops on (none when it gets through), mine interrupted where they pass. Per band, the pairs of lines
    /// between which nothing of it is built; none for a wall nothing meets so. Only straight walls interrupt.
    /// </summary>
    static Func<Band, List<(Cutter A, Cutter B)>> LayerCrossings(IModelView m, Entity wall, IPathElements paths, Vec2 start, Vec2 dir, IReadOnlyList<Band> bands, Func<Entity, int?> ranked)
    {
        var gaps = new Dictionary<int, List<(Cutter A, Cutter B)>>();
        void Gap(Band b, Cutter a, Cutter c) { if (!gaps.TryGetValue(b.Index, out var l)) gaps[b.Index] = l = []; l.Add((a, c)); }
        var left = dir.Perp;
        const double eps = 1e-9;
        foreach (var r in Joins.Of(m, wall.Id))
        {
            var join = Joins.Compute(m, r, paths, out _);
            if (join == null || join.Kind is not (JoinKind.T or JoinKind.X)) continue;
            var side = r.Relating == wall.Id ? join.A : join.B;
            if (join.Kind == JoinKind.T && !side.RunsThrough) continue;
            if (m.Get(Joins.Other(r, wall.Id)) is not { } other || PathCurve.Of(other) is { IsArc: true }) continue;
            var (os, oe) = (Start(other), End(other));
            if ((oe - os).Length < MinLength) continue;
            var od = (oe - os).Normalized();
            if (Math.Abs(Vec2.Cross(dir, od)) < 1e-6) continue;
            var theirBands = Section(m, other);
            Cutter Theirs(double y) => new(os + od.Perp * y, od);
            var elements = Math.Sign((ranked(wall) ?? 0) - (ranked(other) ?? 0));
            var through = join.Kind == JoinKind.T || CoreThrough(bands, theirBands, elements, true, side.RunsThrough) == true;
            if (through)
            {
                // The other's layers come into mine from its side (both sides at a crossing): each of mine they pass is interrupted between their edges.
                int[] sides;
                if (join.Kind == JoinKind.X) sides = [1, -1];
                else
                {
                    var otherFar = (os - join.Point).Length > (oe - join.Point).Length ? os : oe;
                    sides = [Math.Sign(Vec2.Dot(otherFar - join.Point, left))];
                    if (sides[0] == 0) continue;
                }
                var stops = theirBands.Where(e => e.To - e.From > 1e-12).Select(e => (Band: e, Stops: sides.Select(s => (Side: s, At: TeeStop(e, bands, s))).ToList())).ToList();
                foreach (var b in bands)
                {
                    var passing = stops.Where(x =>
                        (join.Kind == JoinKind.X && x.Stops[0].At <= x.Stops[1].At + eps)
                        || x.Stops.Any(s => s.Side > 0 ? b.From >= s.At - eps : b.To <= s.At + eps))
                        .Select(x => (x.Band.From, x.Band.To)).OrderBy(x => x.From).ToList();
                    // Their layers side by side make one gap.
                    var merged = new List<(double From, double To)>();
                    foreach (var p in passing)
                        if (merged.Count > 0 && p.From <= merged[^1].To + eps) merged[^1] = (merged[^1].From, Math.Max(merged[^1].To, p.To));
                        else merged.Add(p);
                    foreach (var (lo, hi) in merged) Gap(b, Theirs(lo), Theirs(hi));
                }
            }
            else
            {
                // Crossed by the wall that runs through: each of my layers comes into it from both sides and is interrupted between where it stops.
                foreach (var b in bands)
                {
                    var (fromLeft, fromRight) = (TeeStop(b, theirBands, 1), TeeStop(b, theirBands, -1));
                    if (fromLeft > fromRight + eps) Gap(b, Theirs(fromRight), Theirs(fromLeft));
                }
            }
        }
        return band => gaps.TryGetValue(band.Index, out var l) ? l : [];
    }

    /// <summary>The footprint with each gap taken out of it: what lies before the gap's first line and what lies after its second; pieces with no area dropped.</summary>
    static List<IReadOnlyList<Vec2>> Pieces(IReadOnlyList<Vec2> footprint, IReadOnlyList<(Cutter A, Cutter B)> gaps, Vec2 start, Vec2 dir, double length)
    {
        var pieces = new List<IReadOnlyList<Vec2>> { footprint };
        if (gaps.Count == 0) return pieces;
        var (behind, ahead) = (start - dir * (length + 1), start + dir * (2 * length + 1));
        static double Side(Cutter c, Vec2 q) => Vec2.Cross(c.Dir, q - c.Point);
        double Along(Cutter c) => Vec2.Cross(c.Point - start, c.Dir) / Vec2.Cross(dir, c.Dir);
        foreach (var (a, b) in gaps)
        {
            // The gap's lines in the order the axis meets them; what the far end behind sees of the first stays, what the far end ahead sees of the second stays.
            var (first, second) = Along(a) <= Along(b) ? (a, b) : (b, a);
            var (sa, sb) = (Math.Sign(Side(first, behind)), Math.Sign(Side(second, ahead)));
            var next = new List<IReadOnlyList<Vec2>>();
            foreach (var p in pieces)
            {
                var before = ClipHalf(p, q => Side(first, q) * sa);
                var after = ClipHalf(p, q => Side(second, q) * sb);
                if (before.Count >= 3 && Math.Abs(Polygon2.SignedArea(before)) > 1e-8) next.Add(before);
                if (after.Count >= 3 && Math.Abs(Polygon2.SignedArea(after)) > 1e-8) next.Add(after);
            }
            pieces = next;
        }
        return pieces;
    }

    /// <summary>
    /// A wall's layer edges seen from a corner: the core's span, and the edges on the corner's outer
    /// and inner sides, each list from the core's face outwards (<paramref name="inner"/>: the side,
    /// +1 left or −1 right, its inner face is on). The thickest layer stands for a core when none is marked.
    /// </summary>
    sealed class CornerEdges
    {
        public readonly double CoreLo, CoreHi;
        public readonly List<double> Outer, Inner;

        public CornerEdges(IReadOnlyList<Band> bands, int inner)
        {
            var cores = bands.Where(b => b.Core && b.To - b.From > 1e-12).ToList();
            if (cores.Count == 0) cores = [bands.OrderByDescending(b => b.To - b.From).First()];
            (CoreLo, CoreHi) = (cores.Min(b => b.From), cores.Max(b => b.To));
            var ys = bands.SelectMany(b => new[] { b.From, b.To }).Distinct().ToList();
            var below = ys.Where(y => y < CoreLo - 1e-9).OrderByDescending(y => y).Prepend(CoreLo).ToList();
            var above = ys.Where(y => y > CoreHi + 1e-9).OrderBy(y => y).Prepend(CoreHi).ToList();
            (Inner, Outer) = inner > 0 ? (above, below) : (below, above);
        }

        /// <summary>The edge of this wall that pairs with <paramref name="mine"/>'s edge at <paramref name="y"/>: the same rank from the core on the same side of the corner, or the last; inside the core, the same share of it.</summary>
        public double Partner(CornerEdges mine, double y)
        {
            int k;
            if ((k = mine.Outer.FindIndex(e => Math.Abs(e - y) < 1e-9)) >= 0) return Outer[Math.Min(k, Outer.Count - 1)];
            if ((k = mine.Inner.FindIndex(e => Math.Abs(e - y) < 1e-9)) >= 0) return Inner[Math.Min(k, Inner.Count - 1)];
            // An edge between two core layers: as far across the other core, from the outer side.
            var t = (y - mine.Outer[0]) / (mine.Inner[0] - mine.Outer[0]);
            return Outer[0] + (Inner[0] - Outer[0]) * t;
        }
    }

    /// <summary>
    /// A curved wall's body: its layers as bands along the arc, cut at the ends
    /// the way its joins say (along the tangent there), its zones, its
    /// openings taken out radially, the lintels over them through its core
    /// (following the arc), and its top under a roof: the roof is read along
    /// the arc in short straight steps, as along a straight wall.
    /// </summary>
    static ElementGeometry ArcGeometry(Entity wall, PathCurve c, IModelView m, IServices? services)
    {
        var problems = new List<string>();
        if (WallTop.Problem(m, wall) is { } topProblem) problems.Add(topProblem);
        var paths = services?.Get<IPathElements>();
        var ends = paths != null ? Joins.EndsOf(m, wall, paths, problems) : new (Vec2 Point, EndCut Cut)?[2];
        var length = c.Length;
        var z = (Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");
        var height = HeightOf(m, wall);
        var bands = Section(m, wall);
        var (yMin, yMax) = (bands.Min(x => x.From), bands.Max(x => x.To));
        // Each face ends where it meets its joint's cut (along the tangent past an end).
        var (From, To) = c.Ends(ends[0], ends[1], yMin, yMax);

        // Under a roof: the top read along each step of the arc; built up to its highest point, then cut along it.
        var reachLo = Math.Min(From(yMin), From(yMax));
        var reachHi = Math.Max(To(yMin), To(yMax));
        var stations = c.Stations(reachLo, reachHi);
        var steps = new List<(Vec2 A, Vec2 Dir, double Length, List<(double U, double V)>? Top)>();
        for (var i = 0; i + 1 < stations.Count; i++)
        {
            var (a, b) = (c.PointAt(stations[i]), c.PointAt(stations[i + 1]));
            var len = (b - a).Length;
            if (len < 1e-9) continue;
            var dir = (b - a) * (1 / len);
            steps.Add((a, dir, len, WallTops.Profile(wall, new RoofTop.WallAxis(a, dir, len, z, height, yMax - yMin), m, services)));
        }
        var roofed = steps.Any(x => x.Top != null);
        var built = roofed ? Math.Max(height, steps.Where(x => x.Top != null).Max(x => x.Top!.Max(p => p.V))) : height;

        var zones = TypeOf(m, wall.Id) is { } type ? WallZones.Bands(WallZones.Of(type), height) : [];
        var openings = Openings.PlacedOn(m, wall.Id);
        var booleans = services?.Get<ISolidBooleans>();
        var cutters = (services?.Get<GeometryOptions>()?.CutOpenings ?? true)
            ? openings.Select(o => (o, Box: Openings.ArcCutter(c, z, yMin, yMax, o))).Where(x => x.Box != null).ToList() : [];
        if (cutters.Count > 0 && booleans == null) problems.Add("Openings need the boolean library (manifold), missing here: the wall is drawn uncut.");

        // Lintels over the openings, through the core, along the arc.
        var core = bands.FirstOrDefault(b => b.Core);
        var lintelBoxes = new List<(Lintel Lintel, int Piece, Solid Box)>();
        if (core.To - core.From > 1e-9)
        {
            var (x0, x1) = (Math.Max(From(core.From), From(core.To)), Math.Min(To(core.From), To(core.To)));
            var lintels = Lintels.ForWall(
                openings.Select(o => new LintelOpening(o.Opening.Id, o.Along - o.Width / 2, o.Along + o.Width / 2, o.Sill, o.Sill + o.Height,
                    o.Spec?.Lintel is { Mode: not "none" } own ? own : null, o.Spec?.Lintel is { Mode: "none" } || BareOpenings.NoLintel(o))).ToList(),
                zones, LintelRuleOf(m, wall), (x0, x1, height));
            foreach (var l in lintels)
            {
                var slices = l.Kind == "downstand" ? [(core.From, core.To)] : Lintels.Slices(l.Rule, core.From, core.To, outerAtMin: true);
                for (var i = 0; i < slices.Count; i++)
                    if (Prism.ExtrudeFootprint(c.Band(_ => l.X0, _ => l.X1, slices[i].From, slices[i].To), z + l.Z0, l.Z1 - l.Z0, "lintel") is { } box)
                        lintelBoxes.Add((l, i, box));
            }
        }
        GeometryPart Cut(GeometryPart part, double z0, double z1, bool isCore = false)
        {
            if (booleans == null) return part;
            var reach = cutters.Where(x => x.o.Sill < z1 - 1e-9 && x.o.Sill + x.o.Height > z0 + 1e-9).Select(x => x.Box!)
                .Concat(isCore ? lintelBoxes.Where(b => b.Lintel.Z0 < z1 - 1e-9 && b.Lintel.Z1 > z0 + 1e-9).Select(b => b.Box) : [])
                .ToList();
            if (reach.Count == 0) return part;
            try { return booleans.Difference(part.Solid, reach, part.Solid.Tag) is { } cut ? part with { Solid = cut } : part; }
            catch (InvalidOperationException ex) { problems.Add($"{part.Name}: an opening could not be cut ({ex.Message})."); return part; }
        }
        var cutAt = PlanCutAbove(m, wall);
        var gaps = openings.Where(o => o.Sill < cutAt && o.Sill + o.Height > cutAt).Select(o => (From: o.Along - o.Width / 2, To: o.Along + o.Width / 2)).OrderBy(g => g.From).ToList();
        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();
        foreach (var band in bands)
        {
            if (!(band.To - band.From > 1e-9)) continue;
            var footprint = c.Band(From, To, band.From, band.To);
            if (band.Core && zones.Count > 0)
            {
                foreach (var zb in zones)
                    if (Prism.ExtrudeFootprint(footprint, z + zb.Z0, zb.Z1 - zb.Z0, zb.Zone.Name) is { } piece)
                        parts.Add(Cut(new GeometryPart(zb.Zone.Name, Materials.ColorOf(m, zb.Zone.Material), piece, zb.Zone.Material, "zone:" + zb.Zone.Key), zb.Z0, zb.Z1, isCore: true));
                // A gable rising over the storey: the masonry goes on above the ring beam.
                if (built > height + 1e-9 && (zones.LastOrDefault(zb => zb.Zone.Role == "masonry") ?? zones[^1]).Zone is var gz
                    && Prism.ExtrudeFootprint(footprint, z + height, built - height, "gable") is { } gable)
                    parts.Add(Cut(new GeometryPart($"Gable ({gz.Name})", Materials.ColorOf(m, gz.Material), gable, gz.Material, "zone:gable"), height, built, isCore: true));
            }
            // A layer of its own height (report #44): from its bottom to its top over the wall's base, never above the wall.
            else if (LayerSpan(band, built) is var (lz0, lz1))
            {
                foreach (var h in HeightPieces(m, band, lz0, lz1))
                    if (Prism.ExtrudeFootprint(footprint, z + h.Z0, h.Z1 - h.Z0, h.Name) is { } solid)
                        parts.Add(Cut(new GeometryPart(h.Name, Materials.ColorOf(m, h.Material), solid, h.Material, $"layer:{band.Index}"), h.Z0, h.Z1, isCore: band.Core));
            }
            // In plan, the band between the gaps the openings leave at the cut.
            var at = double.NegativeInfinity;
            foreach (var g in gaps.Append((From: double.PositiveInfinity, To: double.PositiveInfinity)))
            {
                var lo = at;
                double Lo(double y) => Math.Max(From(y), lo);
                double Hi(double y) => Math.Min(To(y), g.From);
                if (Hi(band.From) - Lo(band.From) > 1e-9 && Hi(band.To) - Lo(band.To) > 1e-9) plan.Add(new PlanItem("cut", true, c.Band(Lo, Hi, band.From, band.To)));
                at = Math.Max(at, g.To);
            }
        }
        // The roof first, then the cut priorities (as for a straight wall).
        if (roofed)
        {
            if (booleans == null) problems.Add("The roof needs the boolean library (manifold), missing here: the wall keeps its height.");
            else
            {
                // Each step's region above the roof (flat at the wall's height where no roof covers it), a little past its ends so the steps overlap.
                var reach = 0.05 + (yMax - yMin) * 0.1;
                var above = steps.Select(x => AboveRoof(x.Top ?? [(0, height), (x.Length, height)], 0, x.A, x.Dir, x.Dir.Perp, z, yMin, yMax, built, reach)).OfType<Solid>().ToList();
                parts = parts.Select(part =>
                {
                    try { return booleans.Difference(part.Solid, above, part.Solid.Tag) is { } cut && Measure.Volume(cut) > 1e-9 ? part with { Solid = cut } : null; }
                    catch (InvalidOperationException ex) { problems.Add($"{part.Name}: the roof could not be cut ({ex.Message})."); return part; }
                }).OfType<GeometryPart>().ToList();
            }
        }
        parts = CutPriorities.Apply(wall, m, services, parts, problems);
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
        return new ElementGeometry(parts, plan, [c.Start, c.End, c.PointAt(length / 2)], problems.Count > 0 ? problems : null);
    }

    /// <summary>
    /// What a roof takes off the wall: the region above its top profile (u along
    /// the wall's own axis, v above its base), across the whole thickness. The
    /// profile runs on in line past the wall's ends, where joins reach.
    /// </summary>
    static Solid? AboveRoof(List<(double U, double V)> profile, double shift, Vec2 start, Vec2 dir, Vec2 left, double z, double yMin, double yMax, double built, double reach = 2)
    {
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
        var c = PathCurve.Of(wall);
        IReadOnlyList<Vec2> fp = c.IsArc ? c.Band(_ => 0, _ => c.Length, y0, y1) : [start + left * y0, end + left * y0, end + left * y1, start + left * y1];
        return Prism.ExtrudeFootprint(fp, z, HeightOf(m, wall), "wall");
    }

    /// <summary>
    /// The wall's lintel pieces in its storey, before its corners (see
    /// <see cref="LintelJoins"/>): over its openings, through its core, inside
    /// its length as its joins leave it.
    /// </summary>
    public static List<LintelPiece> LintelPieces(Entity wall, IModelView m, IServices? services)
    {
        var start = Start(wall);
        var end = End(wall);
        if ((end - start).Length < MinLength || PathCurve.Of(wall).IsArc) return [];
        var paths = services?.Get<IPathElements>();
        var ends = paths != null ? Joins.EndsOf(m, wall, paths, []) : new (Vec2 Point, EndCut Cut)?[2];
        // A free end's own angle shapes the core's reach; only a joined end is a corner for the lintels.
        var cuts = Joins.WithOwnEnds(ends, wall, Frame(wall, m));
        if (cuts[0] is { } s) start = s.Point;
        if (cuts[1] is { } e) end = e.Point;
        var (startCut, endCut) = (cuts[0]?.Cut ?? EndCut.Square, cuts[1]?.Cut ?? EndCut.Square);
        var length = (end - start).Length;
        var dir = (end - start).Normalized();
        var bands = Section(m, wall);
        var core = bands.FirstOrDefault(b => b.Core);
        if (!(core.To - core.From > 1e-9)) return [];
        var (yMin, yMax) = (bands.Min(b => b.From), bands.Max(b => b.To));
        double At(EndCut c, double y) => yMax - yMin < 1e-12 ? c.Right : c.Right + (c.Left - c.Right) * (y - yMin) / (yMax - yMin);
        var z = (Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");
        var height = HeightOf(m, wall);
        var zones = TypeOf(m, wall.Id) is { } type ? WallZones.Bands(WallZones.Of(type), height) : [];
        var shift = Vec2.Dot(start - Start(wall), dir);
        // To the core's farther corner at a joint (the corner settles the rest), to its nearer at a free end cut at an angle (inside the wall).
        double Reach(EndCut c, bool joint) => joint ? Math.Max(At(c, core.From), At(c, core.To)) : Math.Min(At(c, core.From), At(c, core.To));
        var (x0, x1) = (-Reach(startCut, ends[0] != null), length + Reach(endCut, ends[1] != null));
        var lintels = Lintels.ForWall(
            Openings.PlacedOn(m, wall.Id).Select(o => new LintelOpening(o.Opening.Id, o.Along - o.Width / 2 - shift, o.Along + o.Width / 2 - shift, o.Sill, o.Sill + o.Height,
                o.Spec?.Lintel is { Mode: not "none" } own ? own : null, o.Spec?.Lintel is { Mode: "none" } || BareOpenings.NoLintel(o))).ToList(),
            zones, LintelRuleOf(m, wall), (x0, x1, height));
        var pieces = new List<LintelPiece>();
        foreach (var l in lintels)
        {
            var slices = l.Kind == "downstand" ? [(core.From, core.To)] : Lintels.Slices(l.Rule, core.From, core.To, outerAtMin: true);
            for (var i = 0; i < slices.Count; i++)
                pieces.Add(new LintelPiece(l, i, start, dir, l.X0, l.X1, slices[i].From, slices[i].To, z + l.Z0, z + l.Z1,
                    ends[0] != null && l.X0 <= x0 + 1e-6, ends[1] != null && l.X1 >= x1 - 1e-6));
        }
        return pieces;
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
        var first = ClipHalf(poly, p => Vec2.Dot(p - origin, dir) - from);
        return first.Count < 3 ? [] : ClipHalf(first, p => to - Vec2.Dot(p - origin, dir));
    }

    /// <summary>The part of a convex polygon where <paramref name="inside"/> is not negative (a half-plane), its edge cut straight.</summary>
    static List<Vec2> ClipHalf(IReadOnlyList<Vec2> pts, Func<Vec2, double> inside)
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

    /// <summary>
    /// What a wall measures: Qto_WallBaseQuantities for the wall; for each
    /// part, its zone's set (blocks, mortar, concrete, steel) or its layer's
    /// (Lapin_WallLayerQuantities). Net values come from the built parts.
    /// </summary>
    public static IEnumerable<QuantitySet> Quantities(Entity wall, ElementGeometry? geometry, IModelView m)
    {
        if (geometry == null || geometry.Parts.Count == 0) yield break;
        var length = PathCurve.Of(wall).Length;
        var height = HeightOf(m, wall);
        var bands = Section(m, wall);
        var width = bands.Max(b => b.To) - bands.Min(b => b.From);
        // A layer interrupted by a wall crossing it is several parts of one key: measured together.
        var volumes = geometry.Parts.GroupBy(p => p.Key ?? p.Name).ToDictionary(g => g.Key, g => g.Sum(p => Measure.Volume(p.Solid)));
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
            // A layer of several materials in its height (decision 264): a set per material, its own pieces measured.
            var byMaterial = geometry.Parts.Where(p => p.Key == $"layer:{band.Index}").GroupBy(p => p.Material).ToList();
            foreach (var g in byMaterial)
            {
                var gv = byMaterial.Count == 1 ? v : g.Sum(p => Measure.Volume(p.Solid));
                yield return new QuantitySet("Lapin_WallLayerQuantities",
                [
                    new("Width", t, "LENGTH"),
                    new("NetSideArea", t > 0 ? gv / t : 0, "AREA", "Net side area"),
                    new("NetVolume", gv, "VOLUME", "Net volume"),
                ], byMaterial.Count == 1 ? $"layer:{band.Index}" : $"layer:{band.Index}:{g.Key}", byMaterial.Count == 1 ? band.Name : g.First().Name, g.Key ?? band.Material);
            }
        }
        foreach (var set in ZeroLayerQuantities(m, bands, volumes, height)) yield return set;
    }

    /// <summary>
    /// The layers of no thickness (a membrane, paint; decision 291), measured by the area they cover: the side of the layer
    /// they lie on (the next one with a thickness towards the core) as it is built, net of openings and joins, within their
    /// own height and pieces; width and volume 0.
    /// </summary>
    static IEnumerable<QuantitySet> ZeroLayerQuantities(IModelView m, IReadOnlyList<Band> bands, Dictionary<string, double> volumes, double height)
    {
        var core = bands.ToList().FindIndex(b => b.Core);
        if (core < 0) yield break;
        for (var i = 0; i < bands.Count; i++)
        {
            var band = bands[i];
            if (band.Core || band.To - band.From > 1e-9) continue;
            var step = i < core ? 1 : -1;
            var on = -1;
            for (var j = i + step; j >= 0 && j < bands.Count; j += step)
                if (bands[j].To - bands[j].From > 1e-9) { on = j; break; }
            if (on < 0) continue;
            var o = bands[on];
            // The core of a zoned wall is measured by its zones.
            var ov = volumes.TryGetValue($"layer:{o.Index}", out var v) ? v
                : o.Core ? volumes.Where(kv => kv.Key.StartsWith("zone:", StringComparison.Ordinal)).Sum(kv => kv.Value) : 0;
            if (!(ov > 0) || LayerSpan(o, height) is not { } os) continue;
            var onArea = ov / (o.To - o.From);
            var (z0, z1) = (Math.Max(Math.Clamp(band.Z0 ?? 0, 0, height), os.Z0), Math.Min(Math.Clamp(band.Z1 ?? height, 0, height), os.Z1));
            if (z1 - z0 <= 1e-6) continue;
            var pieces = HeightPieces(m, band, z0, z1).GroupBy(p => p.Material).ToList();
            foreach (var g in pieces)
            {
                var area = onArea * g.Sum(p => p.Z1 - p.Z0) / (os.Z1 - os.Z0);
                yield return new QuantitySet("Lapin_WallLayerQuantities",
                [
                    new("Width", 0, "LENGTH"),
                    new("NetSideArea", area, "AREA", "Net side area"),
                    new("NetVolume", 0, "VOLUME", "Net volume"),
                ], pieces.Count == 1 ? $"layer:{band.Index}" : $"layer:{band.Index}:{g.Key}", pieces.Count == 1 ? band.Name : g.First().Name, g.Key ?? band.Material);
            }
        }
    }
}
