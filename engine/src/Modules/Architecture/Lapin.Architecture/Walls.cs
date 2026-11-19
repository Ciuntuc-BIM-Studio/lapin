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

    public static Entity? TypeOf(IModelView m, string wallId) => Lapin.Platform.Types.Of(m, wallId, IfcClass.WallType);

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
    /// <remarks>A wall that keeps a reference line (<see cref="WallReference"/>) grows round it: its axis set off so the line stays.</remarks>
    public static void LayOut(IEdit edit, Entity wall, Entity type)
    {
        var before = edit.Model.Get(wall.Id) is { } current ? WallReference.Before(edit.Model, current) : null;
        LayerSets.LayOut(edit, wall, type, "AXIS2", Layers.CoreOffset);
        WallReference.Keep(edit, wall.Id, before);
    }

    /// <summary>
    /// Flips the wall across its axis: its ends stay, its layers swap faces
    /// (the usage's sense turns and its offset changes sign), so the body is
    /// mirrored across the axis. A wall flips only that way; one that keeps a
    /// reference line (<see cref="WallReference"/>) turns round that line.
    /// </summary>
    public static void Flip(IEdit edit, Entity wall, string axis = "y")
    {
        if (axis == "x") throw new CommandException("A wall flips across its axis (Flip Y): its ends stay where they are.");
        var before = edit.Model.Get(wall.Id) is { } current ? WallReference.Before(edit.Model, current) : null;
        LayerSets.Flip(edit, wall.Id);
        WallReference.Keep(edit, wall.Id, before);
        // Its type's sweeps go with the type's faces.
        WallSweepTypes.Rebuild(edit, edit.Model.Get(wall.Id) ?? wall);
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
    /// <remarks><paramref name="BaseReach"/>: a band's reach past an end (0 start, 1 end) at its From (true) or To edge as the joins leave it, before its own extent (<see cref="LayerExtents"/>).</remarks>
    public sealed record Layout(Vec2 Start, Vec2 End, Vec2 Dir, double Length, double Z, double Height, IReadOnlyList<Band> Bands, Func<Band, List<IReadOnlyList<Vec2>>> Footprints,
        Func<int, Band, bool, double>? BaseReach = null);

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
        var z = Spatial.Base(m, wall);
        var bands = Section(m, wall);
        var yMin = bands.Min(b => b.From);
        var yMax = bands.Max(b => b.To);
        double At(EndCut c, double y) => yMax - yMin < 1e-12 ? c.Right : c.Right + (c.Left - c.Right) * (y - yMin) / (yMax - yMin);
        Vec2 P(double x, double y) => start + dir * x + left * y;
        var cutOrder = services?.Get<ICutters>();
        Func<Entity, int?> ranked = x => cutOrder?.PriorityOf(x, m);
        var corners = paths != null ? LayerCorners(m, wall, paths, start, dir, length, bands, ranked) : new Func<Band, (double From, double To)>?[2];
        var crossings = paths != null ? LayerCrossings(m, wall, paths, start, dir, bands, ranked) : _ => [];
        double BaseReach(int end, Band b, bool atFrom) =>
            corners[end] is { } f ? (atFrom ? f(b).From : f(b).To) : At(end == 0 ? startCut : endCut, atFrom ? b.From : b.To) + (ends[end] == null ? LayerEnd(wall, b.Index, end, atFrom) : 0);
        var extents = LayerExtents.Effective(wall, m);
        // A layer's own extent short of an end (forum topic 89); one past it is a piece of its own (ExtentPieces).
        double Reach(int end, Band b, bool atFrom) => BaseReach(end, b, atFrom) + LayerExtents.Short(extents, b.Index, end == 0 ? LayerExtents.Start : LayerExtents.End);
        var fills = paths != null ? new[] { NodeFill(m, wall, 0, ranked), NodeFill(m, wall, 1, ranked) } : new (int Band, Vec2[] Insert)?[2];
        List<IReadOnlyList<Vec2>> Footprints(Band band)
        {
            Vec2[] whole =
            [
                P(-Reach(0, band, true), band.From), P(length + Reach(1, band, true), band.From),
                P(length + Reach(1, band, false), band.To), P(-Reach(0, band, false), band.To),
            ];
            return Pieces(WithFill(whole, band, fills), crossings(band), start, dir, length);
        }
        return new Layout(start, end, dir, length, z, HeightOf(m, wall), bands, Footprints, BaseReach);
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
        if (WallTops.Problem(m, wall, services) is { } attachProblem) problems.Add(attachProblem);
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
        // A layer's own extents short of the wall's edges (forum topic 89): at its ends, over its base, under its top. Those past
        // them are pieces of their own (ExtentPieces), added after the cut priorities, which would take them back.
        var extents = LayerExtents.Effective(wall, m);
        double Reach(int end, Band b, bool atFrom) =>
            (corners[end] is { } f ? (atFrom ? f(b).From : f(b).To) : At(end == 0 ? startCut : endCut, atFrom ? b.From : b.To) + (ends[end] == null ? LayerEnd(wall, b.Index, end, atFrom) : 0))
            + LayerExtents.Short(extents, b.Index, end == 0 ? LayerExtents.Start : LayerExtents.End);

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
        // The openings of the walls along this one that pass through it (forum topic 64): their box, through this wall's thickness.
        var through = ThroughOpenings.Into(m, wall).Select(x => x.Placed).ToList();
        foreach (var o in through)
            if (Openings.Cutter(frame, o with { Along = o.Along - Vec2.Dot(start - Start(wall), dir) }) is { } box) cutters.Add((o, box));
        // An export wants the body the openings have not cut: IFC writes the openings and subtracts them itself.
        if (!(services?.Get<GeometryOptions>()?.CutOpenings ?? true)) cutters.Clear();
        var booleans = services?.Get<ISolidBooleans>();
        if (cutters.Count > 0 && booleans == null) problems.Add("Openings need the boolean library (manifold), missing here: the wall is drawn uncut.");
        // Lintels over the openings, through the core; the ring beam coming down over an opening close under it; at a corner, the longer of two that meet runs through.
        var shift = Vec2.Dot(start - Start(wall), dir);
        var lintelBoxes = new List<(Lintel Lintel, int Piece, Solid Box)>();
        var lintelsIn = LintelJoins.Resolve(wall, LintelPieces(wall, m, services, through), m, services, out var intoCore);
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
        // At a node of three walls or more, the gap the cores leave there, filled by one of them.
        var fills = paths != null ? new[] { NodeFill(m, wall, 0, ranked), NodeFill(m, wall, 1, ranked) } : new (int Band, Vec2[] Insert)?[2];
        var gaps = openings.Concat(through).Where(o => o.Sill < PlanCutAbove(m, wall) && o.Sill + o.Height > PlanCutAbove(m, wall))
            .Select(o => (From: o.Along - o.Width / 2 - Vec2.Dot(start - Start(wall), dir), To: o.Along + o.Width / 2 - Vec2.Dot(start - Start(wall), dir))).OrderBy(g => g.From).ToList();
        foreach (var band in bands)
        {
            if (!(band.To - band.From > 1e-9)) continue;
            Vec2[] whole =
            [
                P(-Reach(0, band, true), band.From), P(length + Reach(1, band, true), band.From),
                P(length + Reach(1, band, false), band.To), P(-Reach(0, band, false), band.To),
            ];
            // In plan, the band with its extents past the ends too (its body's strips past them are added below, ExtentPieces).
            var (past0, past1) = (Math.Max(0, LayerExtents.Offset(extents, band.Index, LayerExtents.Start)), Math.Max(0, LayerExtents.Offset(extents, band.Index, LayerExtents.End)));
            Vec2[] planWhole = past0 + past1 <= 1e-9 ? whole :
            [
                P(-Reach(0, band, true) - past0, band.From), P(length + Reach(1, band, true) + past1, band.From),
                P(length + Reach(1, band, false) + past1, band.To), P(-Reach(0, band, false) - past0, band.To),
            ];
            var planPieces = Pieces(WithFill(planWhole, band, fills), crossings(band), start, dir, length);
            // The band in the pieces the walls meeting this one leave of it (one, when none interrupts it).
            var bandPieces = Pieces(WithFill(whole, band, fills), crossings(band), start, dir, length);
            for (var pi = 0; pi < bandPieces.Count; pi++)
            {
                var footprint = bandPieces[pi];
                var planFootprint = planPieces.Count == bandPieces.Count ? planPieces[pi] : footprint;
                // The core of a zoned wall is its zones, bottom to top; every other layer runs the whole height.
                // Under a roof a layer going up to it follows it, its top extent moving the roof's cut (stage 3).
                var (below, above) = HeightExtents(extents, band, height, roofTop != null);
                if (band.Core && zones.Count > 0)
                {
                    for (var zi = 0; zi < zones.Count; zi++)
                    {
                        var zb = zones[zi];
                        // The extents go with the first zone down and the last one up.
                        var (zz0, zz1) = (zb.Z0 - (zi == 0 ? below : 0), zb.Z1 + (zi == zones.Count - 1 && built <= height + 1e-9 ? above : 0));
                        if (zz1 - zz0 > 1e-6 && Prism.ExtrudeFootprint(footprint, z + zz0, zz1 - zz0, zb.Zone.Name) is { } piece)
                            parts.Add(Cut(new GeometryPart(zb.Zone.Name, Materials.ColorOf(m, zb.Zone.Material), piece, zb.Zone.Material, "zone:" + zb.Zone.Key), zz0, zz1, isCore: true));
                    }
                    // A gable rising over the storey: the masonry goes on above the ring beam.
                    if (built > height + 1e-9 && (zones.LastOrDefault(zb => zb.Zone.Role == "masonry") ?? zones[^1]).Zone is var gz
                        && Prism.ExtrudeFootprint(footprint, z + height, built + above - height, "gable") is { } gable)
                        parts.Add(Cut(new GeometryPart($"Gable ({gz.Name})", Materials.ColorOf(m, gz.Material), gable, gz.Material, "zone:gable"), height, built + above, isCore: true));
                }
                // A layer of its own height (report #44): from its bottom to its top over the wall's base, never above the wall.
                else if (LayerSpan(band, built, below, above) is var (lz0, lz1))
                {
                    // Its pieces in height (decision 264), each in its own material; one, of the layer's, for most layers.
                    foreach (var h in HeightPieces(m, band, lz0, lz1))
                        if (Prism.ExtrudeFootprint(footprint, z + h.Z0, h.Z1 - h.Z0, h.Name) is { } solid)
                            parts.Add(Cut(new GeometryPart(h.Name, Materials.ColorOf(m, h.Material), solid, h.Material, $"layer:{band.Index}"), h.Z0, h.Z1, isCore: band.Core));
                }
                // In plan, the band between the gaps the openings leave at the cut.
                var x0 = planFootprint.Min(q => Vec2.Dot(q - start, dir));
                var x1 = planFootprint.Max(q => Vec2.Dot(q - start, dir));
                var at = x0;
                foreach (var g in gaps.Append((From: x1, To: x1)))
                {
                    var piece = ClipX(planFootprint, start, dir, at, g.From);
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
            else
            {
                // Each layer along the roof moved by its own top extent (stage 3 of forum topic 89): most by none.
                var aboveBy = new Dictionary<double, Solid?>();
                Solid? Above(double by) => aboveBy.TryGetValue(by, out var x) ? x : aboveBy[by] = AboveRoof(Shifted(roofTop, by), shift, start, dir, left, z, yMin, yMax, built + Math.Max(0, by));
                parts = parts.Select(part =>
                {
                    if (Above(BandOfKey(bands, part.Key) is { } pb ? RoofShift(extents, pb, height) : 0) is not { } above) return part;
                    try { return booleans.Difference(part.Solid, [above], part.Solid.Tag) is { } cut && Measure.Volume(cut) > 1e-9 ? part with { Solid = cut } : null; }
                    catch (InvalidOperationException ex) { problems.Add($"{part.Name}: the roof could not be cut ({ex.Message})."); return part; }
                }).OfType<GeometryPart>().ToList();
            }
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
        // The layers' own extents past the wall's edges (forum topic 89), after the cut priorities too: the openings cut them,
        // and each is one body with the part of its layer it goes on from.
        foreach (var x in ExtentPieces(wall, m, services, cuttingOnly: false))
            LayerExtents.AddJoined(parts, Cut(new GeometryPart(x.Name, Materials.ColorOf(m, x.Material), x.Solid, x.Material, x.Key), x.Z0 - z, x.Z1 - z, isCore: x.Core), booleans);
        if (booleans != null)
        {
            var ring = zones.FirstOrDefault(zb => zb.Zone.Role == "ringBeam");
            foreach (var (l, i, box) in lintelBoxes)
            {
                var material = l.Kind == "downstand" ? ring?.Zone.Material : l.Rule?.Material is { } name ? Materials.Find(m, name)?.Id : Materials.Find(m, "Reinforced concrete")?.Id;
                var color = material != null ? Materials.ColorOf(m, material) : "#a8a8a8";
                var label = l.Kind == "downstand" ? "Ring beam over opening" : l.Precast ? "Lintel (precast)" : "Lintel (cast)";
                parts.Add(new GeometryPart(label, color, box, material, $"{l.Kind}:{l.Key}:{i}") { TypeId = l.Kind == "lintel" ? l.Rule?.BeamTypeIn(m) : null });
            }
        }
        // A bare opening's reveal finished (decision 392): a lining of its jambs and head, inside its hole.
        foreach (var o in openings)
            if (BareOpenings.Lining(m, o, frame, o.Along - shift) is { } lining) parts.Add(lining);
        // The layers of the walls and floors round reaching into this one and set to cut what they meet (forum topic 89).
        parts = LayerExtents.CutBy(wall, m, services, parts, problems);
        return new ElementGeometry(parts, plan, [start, end], problems.Count > 0 ? problems : null);
    }

    /// <summary>
    /// A wall as its layers' extents see it (forum topic 89): its axis as a curve (a straight one for a straight wall, from
    /// where its joins put its start), where each band ends along it at each face as the joins leave it (before its own extents;
    /// end 0 the start, 1 the end), its base, its height, how high it is built (under a roof, its highest point), its zones, each
    /// band's footprint with its extents short of the ends taken off, and under a roof the roof's line along the axis (stations
    /// and heights over the base) and what it takes off a layer whose top is moved by an offset (its top extent).
    /// </summary>
    public sealed record ExtentFrame(PathCurve Axis, Func<int, Band, bool, double> EndAt, IReadOnlyList<Band> Bands, double Z, double Height, double Built,
        IReadOnlyList<ZoneBand> Zones, Func<Band, List<IReadOnlyList<Vec2>>> Footprints, List<(double S, double V)>? RoofLine, Func<double, List<Solid>>? AboveRoof);

    /// <summary>The wall's <see cref="ExtentFrame"/>, straight or curved; null for one of no length.</summary>
    public static ExtentFrame? ExtentFrameOf(Entity wall, IModelView m, IServices? services)
    {
        var all = LayerExtents.Effective(wall, m);
        var zonesOf = (double height) => TypeOf(m, wall.Id) is { } type ? WallZones.Bands(WallZones.Of(type), height) : [];
        if (PathCurve.Of(wall) is { IsArc: true } c)
        {
            var paths = services?.Get<IPathElements>();
            var ends = paths != null ? Joins.EndsOf(m, wall, paths, []) : new (Vec2 Point, EndCut Cut)?[2];
            var bands = Section(m, wall);
            var (yMin, yMax) = (bands.Min(x => x.From), bands.Max(x => x.To));
            var (From, To) = c.Ends(ends[0], ends[1], yMin, yMax);
            var z = Spatial.Base(m, wall);
            var height = HeightOf(m, wall);
            var (steps, built) = ArcRoof(wall, c, m, services, From, To, yMin, yMax, z, height);
            List<(double S, double V)>? line = null;
            if (steps.Any(x => x.Top != null))
            {
                line = [];
                foreach (var x in steps)
                    foreach (var (u, v) in x.Top ?? [(0, height), (x.Length, height)])
                        if (line.Count == 0 || x.S + u - line[^1].S > 1e-9) line.Add((x.S + u, v));
            }
            var reach = 0.05 + (yMax - yMin) * 0.1;
            return new ExtentFrame(c, (end, b, atFrom) => end == 0 ? From(atFrom ? b.From : b.To) : To(atFrom ? b.From : b.To), bands, z, height, built, zonesOf(height),
                b => [c.Band(y => From(y) - LayerExtents.Short(all, b.Index, LayerExtents.Start), y => To(y) + LayerExtents.Short(all, b.Index, LayerExtents.End), b.From, b.To)],
                line, line == null ? null : s => steps.Select(x => AboveRoof(Shifted(x.Top ?? [(0, height), (x.Length, height)], s), 0, x.A, x.Dir, x.Dir.Perp, z, yMin, yMax, built + Math.Max(0, s), reach)).OfType<Solid>().ToList());
        }
        if (LayoutOf(wall, m, services, []) is not { BaseReach: { } baseReach } l) return null;
        var (sMin, sMax) = (l.Bands.Min(b => b.From), l.Bands.Max(b => b.To));
        var axisDir = (End(wall) - Start(wall)).Normalized();
        var roofTop = WallTops.Profile(wall, new RoofTop.WallAxis(Start(wall), axisDir, (End(wall) - Start(wall)).Length, l.Z, l.Height, sMax - sMin), m, services);
        var top = roofTop == null ? l.Height : Math.Max(l.Height, roofTop.Max(p => p.V));
        var shift = Vec2.Dot(l.Start - Start(wall), l.Dir);
        return new ExtentFrame(new PathCurve(l.Start, l.End, 0), (end, b, atFrom) => end == 0 ? -baseReach(0, b, atFrom) : l.Length + baseReach(1, b, atFrom), l.Bands, l.Z, l.Height, top,
            zonesOf(l.Height), l.Footprints, roofTop?.Select(p => (p.U - shift, p.V)).ToList(),
            roofTop == null ? null : s => AboveRoof(Shifted(roofTop, s), shift, l.Start, l.Dir, l.Dir.Perp, l.Z, sMin, sMax, top + Math.Max(0, s)) is { } x ? [x] : []);
    }

    /// <summary>A curved wall's steps along its arc (from its stations, by the chord rule) with the top a roof gives each, and how high it is built.</summary>
    internal static (List<(Vec2 A, Vec2 Dir, double Length, double S, List<(double U, double V)>? Top)> Steps, double Built) ArcRoof(Entity wall, PathCurve c, IModelView m, IServices? services,
        Func<double, double> From, Func<double, double> To, double yMin, double yMax, double z, double height)
    {
        var reachLo = Math.Min(From(yMin), From(yMax));
        var reachHi = Math.Max(To(yMin), To(yMax));
        var stations = c.Stations(reachLo, reachHi);
        var steps = new List<(Vec2 A, Vec2 Dir, double Length, double S, List<(double U, double V)>? Top)>();
        for (var i = 0; i + 1 < stations.Count; i++)
        {
            var (a, b) = (c.PointAt(stations[i]), c.PointAt(stations[i + 1]));
            var len = (b - a).Length;
            if (len < 1e-9) continue;
            var dir = (b - a) * (1 / len);
            steps.Add((a, dir, len, stations[i], WallTops.Profile(wall, new RoofTop.WallAxis(a, dir, len, z, height, yMax - yMin), m, services)));
        }
        var roofed = steps.Any(x => x.Top != null);
        return (steps, roofed ? Math.Max(height, steps.Where(x => x.Top != null).Max(x => x.Top!.Max(p => p.V))) : height);
    }

    /// <summary>A roof's line moved up (or down) by <paramref name="by"/>.</summary>
    static List<(double U, double V)> Shifted(List<(double U, double V)> line, double by) => Math.Abs(by) < 1e-12 ? line : [.. line.Select(p => (p.U, p.V + by))];

    /// <summary>Whether a layer goes up to the wall's top (no top of its own under it): under a roof it follows the roof.</summary>
    internal static bool FollowsRoof(Band b, double height) => b.Z1 is not { } z1 || z1 >= height - 1e-9;

    /// <summary>
    /// A layer's extents in its height as its body takes them (forum topic 89): below, its own short of the base; above, short of
    /// its top — under a roof, for a layer going up to it, past it too (its body built that much higher, then cut along the roof
    /// moved up by it, <see cref="RoofShift"/>).
    /// </summary>
    internal static (double Below, double Above) HeightExtents(IReadOnlyList<LayerExtents.Extent> xs, Band b, double height, bool roofed) =>
        (LayerExtents.Short(xs, b.Index, LayerExtents.Bottom),
         roofed && FollowsRoof(b, height) ? Math.Max(0, LayerExtents.Offset(xs, b.Index, LayerExtents.Top)) : LayerExtents.Short(xs, b.Index, LayerExtents.Top));

    /// <summary>How far a roof's cut moves up for a layer under it: its top extent (stage 3: the layer follows the roof that far over it, or short of it), 0 for a layer of a top of its own.</summary>
    internal static double RoofShift(IReadOnlyList<LayerExtents.Extent> xs, Band b, double height) => FollowsRoof(b, height) ? LayerExtents.Offset(xs, b.Index, LayerExtents.Top) : 0;

    /// <summary>The band a part of the wall's body is of, by its key: a layer's, or a zoned core's zone (the core); null for anything else (a lintel).</summary>
    static Band? BandOfKey(IReadOnlyList<Band> bands, string? key) =>
        key == null ? null
        : key.StartsWith("layer:", StringComparison.Ordinal) && int.TryParse(key[6..], out var i) ? bands.Cast<Band?>().FirstOrDefault(b => b!.Value.Index == i)
        : key.StartsWith("zone:", StringComparison.Ordinal) ? bands.Cast<Band?>().FirstOrDefault(b => b!.Value.Core) : null;

    /// <summary>
    /// The pieces of a wall's layers past its edges (forum topic 89, <see cref="LayerExtents"/>), straight or curved: past an
    /// end, a strip the layer's whole height (each of its pieces in height in its own material, a zoned core's zones and its
    /// gable); under its base and over its top, its footprint as the joins leave it, in the material of its bottom or top piece.
    /// Past an end of a curved wall the strip goes on along the end's tangent, as the wall's own body does past its axis. Under a
    /// roof a layer going up to the roof takes its top extent by following the roof (<see cref="RoofShift"/>), and what rises
    /// over the roof (moved by that extent) is cut off. <paramref name="cuttingOnly"/>: only the extents set to cut what they
    /// meet that <paramref name="which"/> keeps (what the elements round take out of themselves). Heights are absolute.
    /// </summary>
    public static List<LayerExtents.Piece> ExtentPieces(Entity wall, IModelView m, IServices? services, bool cuttingOnly, Func<LayerExtents.Extent, bool>? which = null)
    {
        var o = new List<LayerExtents.Piece>();
        var all = LayerExtents.Effective(wall, m);
        var past = all.Where(x => x.Offset > 1e-9 && (!cuttingOnly || (x.Cuts && (which?.Invoke(x) ?? true)))).ToList();
        if (past.Count == 0 || ExtentFrameOf(wall, m, services) is not { } f) return o;
        var (c, z, height, built, zones) = (f.Axis, f.Z, f.Height, f.Built, f.Zones);
        var roofed = f.AboveRoof != null;
        var booleans = services?.Get<ISolidBooleans>();
        foreach (var band in f.Bands)
        {
            if (band.To - band.From <= 1e-9) continue;
            double Past(string edge) => LayerExtents.Offset(past, band.Index, edge);
            var (ps, pe, pb) = (Past(LayerExtents.Start), Past(LayerExtents.End), Past(LayerExtents.Bottom));
            var pt = roofed && FollowsRoof(band, height) ? 0 : Past(LayerExtents.Top);
            if (ps + pe + pb + pt <= 1e-9) continue;
            // The layer in its height as the wall builds it (its extents short of the base and top taken off; under a roof, past it too).
            var (below, above) = HeightExtents(all, band, height, roofed);
            var stack = new List<(double Z0, double Z1, string? Material, string Name, string Key)>();
            if (band.Core && zones.Count > 0)
            {
                for (var zi = 0; zi < zones.Count; zi++)
                {
                    var zb = zones[zi];
                    stack.Add((zb.Z0 - (zi == 0 ? below : 0), zb.Z1 + (zi == zones.Count - 1 && built <= height + 1e-9 ? above : 0), zb.Zone.Material, zb.Zone.Name, "zone:" + zb.Zone.Key));
                }
                // A gable over the storey: its masonry above the ring beam.
                if (built > height + 1e-9 && (zones.LastOrDefault(zb => zb.Zone.Role == "masonry") ?? zones[^1]).Zone is var gz)
                    stack.Add((height, built + above, gz.Material, $"Gable ({gz.Name})", "zone:gable"));
            }
            else if (LayerSpan(band, built, below, above) is var (lz0, lz1))
                foreach (var h in HeightPieces(m, band, lz0, lz1)) stack.Add((h.Z0, h.Z1, h.Material, h.Name, $"layer:{band.Index}"));
            stack.RemoveAll(h => h.Z1 - h.Z0 <= 1e-6);
            if (stack.Count == 0) continue;
            var mine = new List<LayerExtents.Piece>();
            void Add(IReadOnlyList<Vec2> footprint, double h0, double h1, (double, double, string? Material, string Name, string Key) of)
            {
                if (h1 - h0 > 1e-6 && Prism.ExtrudeFootprint(footprint, z + h0, h1 - h0, of.Name) is { } solid)
                    mine.Add(new LayerExtents.Piece(solid, of.Material, of.Name, of.Key, band.Core, z + h0, z + h1));
            }
            // Past an end: a strip from where the layer ends now, its whole height (with its extents under and over).
            foreach (var (end, d) in new[] { (0, ps), (1, pe) })
            {
                if (d <= 1e-9) continue;
                double At(double y) => f.EndAt(end, band, Math.Abs(y - band.From) < Math.Abs(y - band.To));
                var strip = end == 0 ? c.Band(y => At(y) - d, At, band.From, band.To) : c.Band(At, y => At(y) + d, band.From, band.To);
                for (var i = 0; i < stack.Count; i++)
                    Add(strip, i == 0 ? stack[i].Z0 - pb : stack[i].Z0, i == stack.Count - 1 ? stack[i].Z1 + pt : stack[i].Z1, stack[i]);
            }
            // Under its base and over its top: its footprint.
            foreach (var footprint in f.Footprints(band))
            {
                if (pb > 1e-9) Add(footprint, stack[0].Z0 - pb, stack[0].Z0, stack[0]);
                if (pt > 1e-9) Add(footprint, stack[^1].Z1, stack[^1].Z1 + pt, stack[^1]);
            }
            // Under a roof: cut along it as the wall is (moved by the layer's top extent).
            if (roofed && booleans != null && mine.Count > 0 && f.AboveRoof!(RoofShift(all, band, height)) is { Count: > 0 } over)
                mine = mine.Select(p =>
                {
                    try { return booleans.Difference(p.Solid, over, p.Solid.Tag) is { } cut && Measure.Volume(cut) > 1e-9 ? p with { Solid = cut } : null; }
                    catch (InvalidOperationException) { return p; }
                }).OfType<LayerExtents.Piece>().ToList();
            o.AddRange(mine);
        }
        return o;
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

    /// <summary>
    /// A layer's height over the wall's base: its own bottom and top, else the whole of what is built, then its own extents
    /// (<paramref name="below"/> past its bottom, <paramref name="above"/> past its top, negative short of them); null when nothing of it is left.
    /// </summary>
    internal static (double Z0, double Z1)? LayerSpan(Band band, double built, double below = 0, double above = 0)
    {
        var z0 = Math.Clamp(band.Z0 ?? 0, 0, built) - below;
        var z1 = Math.Clamp(band.Z1 ?? built, 0, built) + above;
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
        // A node of three walls or more (the forum's topic 164): each layer edge meets its like on the neighbour on its side.
        for (var i = 0; i < 2; i++)
            if (Joins.NodeOf(m, wall, i) is { } node) o[i] = NodeCorner(m, i, start, dir, length, bands, node);
        foreach (var r in Joins.Of(m, wall.Id))
        {
            var join = Joins.Compute(m, r, paths, out _);
            if (join == null || join.Kind is not (JoinKind.L or JoinKind.T)) continue;
            var side = r.Relating == wall.Id ? join.A : join.B;
            var i = side.Connection switch { Connection.AtStart => 0, Connection.AtEnd => 1, _ => -1 };
            if (i < 0 || o[i] != null || m.Get(Joins.Other(r, wall.Id)) is not { } other) continue;
            // A node the layers could not be paired at (a wall of no length among them): the body's fan cut (Joins.Fan).
            if (Joins.IsNode(m, wall, i)) continue;
            var elements = Math.Sign((ranked(wall) ?? 0) - (ranked(other) ?? 0));
            o[i] = LayerCorner(m, start, dir, length, i == 0, bands, other, join.Style, side.RunsThrough, join.Kind == JoinKind.T, elements);
        }
        return o;
    }

    /// <summary>
    /// How each layer ends at a node, where three walls or more end at one point (the forum's topic 164: two walls joined
    /// clean, a third at the same point did not). Round the node, each wall's layer edges on one side meet the edges of its
    /// neighbour on that side as at a mitred corner of two (<see cref="CornerEdges"/>: from the core outwards, a finish with
    /// the neighbour's finish, the core's face with the neighbour's core face, the last on the outer face); a neighbour in
    /// line leaves them square at the node. The edges inside a core go straight across it. What the cores leave between them
    /// at the node is filled by one of them (<see cref="NodeFill"/>), so the cores meet. Null when it cannot be paired so.
    /// </summary>
    static Func<Band, (double From, double To)>? NodeCorner(IModelView m, int end, Vec2 start, Vec2 dir, double length, IReadOnlyList<Band> bands,
        IReadOnlyList<(Entity E, int End)> partners)
    {
        if (NodeEdges(m, end, start, dir, length, bands, partners) is not { } edge) return null;
        return band => band.To - band.From < 1e-12 ? (0, 0) : (edge(band.From), edge(band.To));
    }

    /// <summary>The direction a wall leaves a node by (its end there: 0 start, 1 end); null for one of no length.</summary>
    static Vec2? Leaving(Entity e, int end)
    {
        var along = End(e) - Start(e);
        if (along.Length < MinLength) return null;
        var d = along.Normalized();
        return end == 0 ? d : -d;
    }

    /// <summary>
    /// A wall's layer edges at a node (<see cref="NodeCorner"/>): for an edge at <c>y</c> across the wall, its reach past
    /// the wall's end there. The wall's frame is <paramref name="start"/>, <paramref name="dir"/>, <paramref name="length"/>.
    /// </summary>
    static Func<double, double>? NodeEdges(IModelView m, int end, Vec2 start, Vec2 dir, double length, IReadOnlyList<Band> bands,
        IReadOnlyList<(Entity E, int End)> partners)
    {
        if (bands.Count == 0) return null;
        var mine = end == 0 ? dir : -dir;
        var round = new List<(Entity? E, int End, Vec2 Dir, double Angle)> { (null, end, dir, Math.Atan2(mine.Y, mine.X)) };
        foreach (var (e, pe) in partners)
        {
            if (Leaving(e, pe) is not { } u) return null;
            round.Add((e, pe, (End(e) - Start(e)).Normalized(), Math.Atan2(u.Y, u.X)));
        }
        var sorted = round.OrderBy(x => x.Angle).ToList();
        var k = sorted.FindIndex(x => x.E == null);
        var (ccw, cw) = (sorted[(k + 1) % sorted.Count], sorted[(k - 1 + sorted.Count) % sorted.Count]);
        var thickness = bands.Max(b => b.To) - bands.Min(b => b.From);
        var left = dir.Perp;
        // The counter-clockwise side of a wall leaving the node: its left (+y) when it leaves by its start.
        var cs = end == 0 ? 1 : -1;
        Func<double, double> Facing((Entity? E, int End, Vec2 Dir, double Angle) n, int mySide, int theirSide)
        {
            var me = new CornerEdges(bands, mySide);
            var them = new CornerEdges(Section(m, n.E!), theirSide);
            var (os, od) = (Start(n.E!), n.Dir);
            var cross = Vec2.Cross(dir, od);
            return y =>
            {
                // In line: square at the node.
                if (Math.Abs(cross) < 1e-6) return 0;
                var p = start + left * y;
                var s = Vec2.Cross(os + od.Perp * them.Partner(me, y) - p, od) / cross;
                return Math.Min(end == 0 ? -s : s - length, 2 * thickness);
            };
        }
        // The neighbour counter-clockwise faces this wall with its clockwise side, and the other way round.
        var toCcw = Facing(ccw, cs, ccw.End == 0 ? -1 : 1);
        var toCw = Facing(cw, -cs, cw.End == 0 ? 1 : -1);
        var core = new CornerEdges(bands, 1);
        var (lo, hi) = (core.CoreLo, core.CoreHi);
        Func<double, double> loSide = cs > 0 ? toCw : toCcw, hiSide = cs > 0 ? toCcw : toCw;
        return y =>
        {
            if (y <= lo + 1e-9) return loSide(y);
            if (y >= hi - 1e-9) return hiSide(y);
            // Inside the core: straight across it.
            var (a, b) = (loSide(lo), hiSide(hi));
            return a + (b - a) * (y - lo) / (hi - lo);
        };
    }

    /// <summary>
    /// The walls round a node, each by the end it has there, in the frame its layer edges are reckoned in (<see cref="NodeEdges"/>):
    /// a wall ending there, its own axis; a wall running through it (in <see cref="Joins.NodeOf"/> twice), each half from the
    /// node on. Null when one has no length or cannot be paired.
    /// </summary>
    static List<(Entity E, int End, bool Half, Vec2 Start, Vec2 Dir, double Length, double Angle, IReadOnlyList<Band> Bands, Func<double, double> Edge)>? NodeRound(
        IModelView m, Vec2 node, IReadOnlyList<(Entity E, int End)> all)
    {
        var o = new List<(Entity E, int End, bool Half, Vec2 Start, Vec2 Dir, double Length, double Angle, IReadOnlyList<Band> Bands, Func<double, double> Edge)>();
        for (var i = 0; i < all.Count; i++)
        {
            var (e, pe) = all[i];
            if (Leaving(e, pe) is not { } u) return null;
            var half = all.Count(x => x.E.Id == e.Id) > 1;
            var (s, t) = (Start(e), End(e));
            var dir = (t - s).Normalized();
            if (half) (s, t) = pe == 0 ? (node, t) : (s, node);
            var length = (t - s).Length;
            var bands = Section(m, e);
            if (bands.Count == 0 || length < MinLength) return null;
            var others = all.Where((_, k) => k != i).ToList();
            if (NodeEdges(m, pe, s, dir, length, bands, others) is not { } edge) return null;
            o.Add((e, pe, half, s, dir, length, Math.Atan2(u.Y, u.X), bands, edge));
        }
        return o;
    }

    /// <summary>Where a wall's layer edge at <c>y</c> ends at a node (<see cref="NodeRound"/>), in plan.</summary>
    static Vec2 NodePoint((Entity E, int End, bool Half, Vec2 Start, Vec2 Dir, double Length, double Angle, IReadOnlyList<Band> Bands, Func<double, double> Edge) w, double y) =>
        (w.End == 0 ? w.Start - w.Dir * w.Edge(y) : w.Start + w.Dir * (w.Length + w.Edge(y))) + w.Dir.Perp * y;

    /// <summary>
    /// What the cores leave between them at a node of three walls or more (each core ending on its neighbours' cores, a
    /// gap stays round the point), given to one core so the cores meet: of the walls ending there, the one whose core
    /// ranks highest (its layer's priority), then the higher element priority, then the thicker core, then the first made.
    /// A wall running through the node keeps its core as it is: the gap is then what lies between its core's face and the
    /// cores ending on it. For that wall's end at the node: the band that takes it and the points to put in its outline
    /// between the band's two corners at that end (from the corner at its From edge at the end, at its To edge at the
    /// start); null for every other wall, or no gap.
    /// </summary>
    static (int Band, Vec2[] Insert)? NodeFill(IModelView m, Entity wall, int end, Func<Entity, int?> ranked)
    {
        if (Joins.NodeOf(m, wall, end) is not { } partners) return null;
        var node = end == 0 ? Start(wall) : End(wall);
        if (NodeRound(m, node, partners.Prepend((wall, end)).ToList()) is not { } round) return null;
        var main = round.Where(x => !x.Half).Select(x => (W: x, Rank: CoreRank(x.Bands), Element: ranked(x.E) ?? 0, Core: new CornerEdges(x.Bands, 1)))
            .OrderByDescending(x => x.Rank).ThenByDescending(x => x.Element).ThenByDescending(x => x.Core.CoreHi - x.Core.CoreLo)
            .ThenBy(x => x.W.E.Id, StringComparer.Ordinal).First();
        if (main.W.E.Id != wall.Id) return null;
        var sorted = round.OrderBy(x => x.Angle).ToList();
        // Round the node counter-clockwise, each wall's core corners there (clockwise, then counter-clockwise); a half of a
        // wall running through leaves out the corner facing its other half, which its core goes on through.
        var ring = new List<(Vec2 P, int Owner, bool Ccw)>();
        for (var k = 0; k < sorted.Count; k++)
        {
            var w = sorted[k];
            var core = new CornerEdges(w.Bands, 1);
            var (lo, hi) = (NodePoint(w, core.CoreLo), NodePoint(w, core.CoreHi));
            // Leaving by its start, its left (the core's high face) is its counter-clockwise side.
            var (cw, ccw) = w.End == 0 ? (lo, hi) : (hi, lo);
            bool Twin(int j) => sorted[j].E.Id == w.E.Id;
            if (!(w.Half && Twin((k - 1 + sorted.Count) % sorted.Count))) ring.Add((cw, k, false));
            if (!(w.Half && Twin((k + 1) % sorted.Count))) ring.Add((ccw, k, true));
        }
        if (Polygon2.SignedArea(ring.Select(x => x.P).ToList()) < 1e-9) return null;
        // Round from this wall's counter-clockwise corner to its clockwise one: the gap's outline less this wall's own edge.
        var me = sorted.FindIndex(x => x.E.Id == wall.Id && !x.Half);
        var from = ring.FindIndex(x => x.Owner == me && x.Ccw);
        if (from < 0) return null;
        var insert = new List<Vec2>();
        for (var i = 0; i < ring.Count; i++)
        {
            var p = ring[(from + i) % ring.Count].P;
            if (insert.Count == 0 || (insert[^1] - p).Length > 1e-9) insert.Add(p);
        }
        var bandsOf = main.W.Bands;
        var cores = bandsOf.Where(b => b.Core && b.To - b.From > 1e-12).ToList();
        var pool = (cores.Count > 0 ? cores : bandsOf.Where(b => b.To - b.From > 1e-12)).ToList();
        if (pool.Count == 0) return null;
        return (pool.OrderByDescending(b => b.To - b.From).First().Index, insert.ToArray());
    }

    /// <summary>A band's outline (start From, end From, end To, start To) with a node's gap put in at either end (<see cref="NodeFill"/>).</summary>
    static Vec2[] WithFill(Vec2[] whole, Band band, (int Band, Vec2[] Insert)?[] fills)
    {
        if (fills[0] is not { } f0 || f0.Band != band.Index) f0 = default;
        if (fills[1] is not { } f1 || f1.Band != band.Index) f1 = default;
        if (f0.Insert == null && f1.Insert == null) return whole;
        var o = new List<Vec2> { whole[0], whole[1] };
        if (f1.Insert != null) o.AddRange(f1.Insert);
        o.Add(whole[2]);
        o.Add(whole[3]);
        if (f0.Insert != null) o.AddRange(f0.Insert);
        // No point twice in a row (a band that is the whole core starts and ends the gap at its own corners).
        var clean = new List<Vec2>();
        foreach (var p in o)
            if (clean.Count == 0 || (clean[^1] - p).Length > 1e-9) clean.Add(p);
        while (clean.Count > 3 && (clean[^1] - clean[0]).Length <= 1e-9) clean.RemoveAt(clean.Count - 1);
        return clean.ToArray();
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
        var nodes = new List<Vec2>();
        foreach (var r in Joins.Of(m, wall.Id))
        {
            var join = Joins.Compute(m, r, paths, out _);
            if (join == null || join.Kind is not (JoinKind.T or JoinKind.X)) continue;
            var side = r.Relating == wall.Id ? join.A : join.B;
            if (join.Kind == JoinKind.T && !side.RunsThrough) continue;
            if (m.Get(Joins.Other(r, wall.Id)) is not { } other || PathCurve.Of(other) is { IsArc: true }) continue;
            // Two walls or more ending on this one at one point (the forum's topic 164): a node, each layer there cut where it meets its like.
            var theirSide = r.Relating == wall.Id ? join.B : join.A;
            var theirEnd = theirSide.Connection switch { Connection.AtStart => 0, Connection.AtEnd => 1, _ => -1 };
            if (join.Kind == JoinKind.T && theirEnd >= 0 && Joins.NodeOf(m, other, theirEnd) is { } node && node.Count(x => x.E.Id == wall.Id) == 2)
            {
                var at = theirEnd == 0 ? Start(other) : End(other);
                if (nodes.Any(p => (p - at).Length < 1e-6)) continue;
                nodes.Add(at);
                NodeGaps(m, wall, at, node.Where(x => x.E.Id != wall.Id).Prepend((other, theirEnd)).ToList(), bands, Gap);
                continue;
            }
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

    /// <summary>
    /// The layers of a wall that runs through a node (two walls or more ending on it at one point, on one side, the forum's
    /// topic 164) interrupted there: each layer on their side, between where it meets its like on the first of them and on
    /// the last, as each half of it would end at the node (<see cref="NodeEdges"/>); its core and the other side go on.
    /// </summary>
    static void NodeGaps(IModelView m, Entity wall, Vec2 node, IReadOnlyList<(Entity E, int End)> ends, IReadOnlyList<Band> bands, Action<Band, Cutter, Cutter> gap)
    {
        var all = ends.Append((wall, 0)).Append((wall, 1)).ToList();
        if (NodeRound(m, node, all) is not { } round) return;
        var halves = round.Where(x => x.Half).ToList();
        if (halves.Count != 2) return;
        var dir = (End(wall) - Start(wall)).Normalized();
        var far = ends[0].End == 0 ? End(ends[0].E) : Start(ends[0].E);
        var side = Math.Sign(Vec2.Cross(dir, far - node));
        var core = new CornerEdges(bands, 1);
        foreach (var b in bands)
        {
            if (b.To - b.From < 1e-12 || (side > 0 ? b.From < core.CoreHi - 1e-9 : b.To > core.CoreLo + 1e-9)) continue;
            Cutter? Line((Entity E, int End, bool Half, Vec2 Start, Vec2 Dir, double Length, double Angle, IReadOnlyList<Band> Bands, Func<double, double> Edge) h)
            {
                var (p, q) = (NodePoint(h, b.From), NodePoint(h, b.To));
                return (q - p).Length < 1e-9 ? null : new Cutter(p, (q - p).Normalized());
            }
            if (Line(halves[0]) is { } a && Line(halves[1]) is { } c) gap(b, a, c);
        }
    }

    /// <summary>The footprint with each gap taken out of it: what lies before the gap's first line and what lies after its second; pieces with no area dropped.</summary>
    static List<IReadOnlyList<Vec2>> Pieces(IReadOnlyList<Vec2> footprint, IReadOnlyList<(Cutter A, Cutter B)> gaps, Vec2 start, Vec2 dir, double length)
    {
        var pieces = new List<IReadOnlyList<Vec2>> { footprint };
        if (gaps.Count == 0) return pieces;
        // Reckoned along the band's own middle (a node's gap lines are slanted: on the axis they may cross in the other order).
        var mid = start + dir.Perp * Vec2.Dot(Polygon2.Centroid(footprint) - start, dir.Perp);
        var (behind, ahead) = (mid - dir * (length + 1), mid + dir * (2 * length + 1));
        static double Side(Cutter c, Vec2 q) => Vec2.Cross(c.Dir, q - c.Point);
        double Along(Cutter c) => Vec2.Cross(c.Point - mid, c.Dir) / Vec2.Cross(dir, c.Dir);
        foreach (var (a, b) in gaps)
        {
            // The gap's lines in the order the axis meets them; what the far end behind sees of the first stays, what the far end ahead sees of the second stays.
            var (first, second) = Along(a) <= Along(b) ? (a, b) : (b, a);
            var (sa, sb) = (Math.Sign(Side(first, behind)), Math.Sign(Side(second, ahead)));
            var next = new List<IReadOnlyList<Vec2>>();
            foreach (var p in pieces)
            {
                var before = Polygon2.ClipHalf(p, q => Side(first, q) * sa);
                var after = Polygon2.ClipHalf(p, q => Side(second, q) * sb);
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
        if (WallTops.Problem(m, wall, services) is { } attachProblem) problems.Add(attachProblem);
        var paths = services?.Get<IPathElements>();
        var ends = paths != null ? Joins.EndsOf(m, wall, paths, problems) : new (Vec2 Point, EndCut Cut)?[2];
        var length = c.Length;
        var z = Spatial.Base(m, wall);
        var height = HeightOf(m, wall);
        var bands = Section(m, wall);
        var (yMin, yMax) = (bands.Min(x => x.From), bands.Max(x => x.To));
        // Each face ends where it meets its joint's cut (along the tangent past an end).
        var (From, To) = c.Ends(ends[0], ends[1], yMin, yMax);

        // Under a roof: the top read along each step of the arc; built up to its highest point, then cut along it.
        var (steps, built) = ArcRoof(wall, c, m, services, From, To, yMin, yMax, z, height);
        var roofed = steps.Any(x => x.Top != null);
        // A layer's own extents short of the wall's edges (forum topic 89, stage 2): along the arc at its ends, over its base,
        // under its top; those past them are pieces of their own (ExtentPieces), added after the cut priorities.
        var extents = LayerExtents.Effective(wall, m);
        double Short(Band b, string edge) => LayerExtents.Short(extents, b.Index, edge);
        double Full(Band b, string edge) => LayerExtents.Offset(extents, b.Index, edge);

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
                    o.Spec?.Lintel is { Mode: not "none" } own ? own.Resolve(m) : null, o.Spec?.Lintel is { Mode: "none" } || BareOpenings.NoLintel(o))).ToList(),
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
            var (s0, s1) = (Short(band, LayerExtents.Start), Short(band, LayerExtents.End));
            var footprint = c.Band(y => From(y) - s0, y => To(y) + s1, band.From, band.To);
            var (below, above) = HeightExtents(extents, band, height, roofed);
            if (band.Core && zones.Count > 0)
            {
                for (var zi = 0; zi < zones.Count; zi++)
                {
                    var zb = zones[zi];
                    // The extents go with the first zone down and the last one up.
                    var (zz0, zz1) = (zb.Z0 - (zi == 0 ? below : 0), zb.Z1 + (zi == zones.Count - 1 && built <= height + 1e-9 ? above : 0));
                    if (zz1 - zz0 > 1e-6 && Prism.ExtrudeFootprint(footprint, z + zz0, zz1 - zz0, zb.Zone.Name) is { } piece)
                        parts.Add(Cut(new GeometryPart(zb.Zone.Name, Materials.ColorOf(m, zb.Zone.Material), piece, zb.Zone.Material, "zone:" + zb.Zone.Key), zz0, zz1, isCore: true));
                }
                // A gable rising over the storey: the masonry goes on above the ring beam.
                if (built > height + 1e-9 && (zones.LastOrDefault(zb => zb.Zone.Role == "masonry") ?? zones[^1]).Zone is var gz
                    && Prism.ExtrudeFootprint(footprint, z + height, built + above - height, "gable") is { } gable)
                    parts.Add(Cut(new GeometryPart($"Gable ({gz.Name})", Materials.ColorOf(m, gz.Material), gable, gz.Material, "zone:gable"), height, built + above, isCore: true));
            }
            // A layer of its own height (report #44): from its bottom to its top over the wall's base, never above the wall.
            else if (LayerSpan(band, built, below, above) is var (lz0, lz1))
            {
                foreach (var h in HeightPieces(m, band, lz0, lz1))
                    if (Prism.ExtrudeFootprint(footprint, z + h.Z0, h.Z1 - h.Z0, h.Name) is { } solid)
                        parts.Add(Cut(new GeometryPart(h.Name, Materials.ColorOf(m, h.Material), solid, h.Material, $"layer:{band.Index}"), h.Z0, h.Z1, isCore: band.Core));
            }
            // In plan, the band between the gaps the openings leave at the cut (with its extents past the ends too).
            var (f0, f1) = (Full(band, LayerExtents.Start), Full(band, LayerExtents.End));
            var at = double.NegativeInfinity;
            foreach (var g in gaps.Append((From: double.PositiveInfinity, To: double.PositiveInfinity)))
            {
                var lo = at;
                double Lo(double y) => Math.Max(From(y) - f0, lo);
                double Hi(double y) => Math.Min(To(y) + f1, g.From);
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
                // Each layer along the roof moved by its own top extent (stage 3 of forum topic 89): most by none.
                var aboveBy = new Dictionary<double, List<Solid>>();
                List<Solid> Above(double by) => aboveBy.TryGetValue(by, out var x) ? x
                    : aboveBy[by] = steps.Select(x => AboveRoof(Shifted(x.Top ?? [(0, height), (x.Length, height)], by), 0, x.A, x.Dir, x.Dir.Perp, z, yMin, yMax, built + Math.Max(0, by), reach)).OfType<Solid>().ToList();
                parts = parts.Select(part =>
                {
                    var above = Above(BandOfKey(bands, part.Key) is { } pb ? RoofShift(extents, pb, height) : 0);
                    try { return booleans.Difference(part.Solid, above, part.Solid.Tag) is { } cut && Measure.Volume(cut) > 1e-9 ? part with { Solid = cut } : null; }
                    catch (InvalidOperationException ex) { problems.Add($"{part.Name}: the roof could not be cut ({ex.Message})."); return part; }
                }).OfType<GeometryPart>().ToList();
            }
        }
        parts = CutPriorities.Apply(wall, m, services, parts, problems);
        // The layers' own extents past the wall's edges (forum topic 89, stage 2): after the cut priorities, cut by the openings,
        // one body with the part of its layer each goes on from.
        foreach (var x in ExtentPieces(wall, m, services, cuttingOnly: false))
            LayerExtents.AddJoined(parts, Cut(new GeometryPart(x.Name, Materials.ColorOf(m, x.Material), x.Solid, x.Material, x.Key), x.Z0 - z, x.Z1 - z, isCore: x.Core), booleans);
        if (booleans != null)
        {
            var ring = zones.FirstOrDefault(zb => zb.Zone.Role == "ringBeam");
            foreach (var (l, i, box) in lintelBoxes)
            {
                var material = l.Kind == "downstand" ? ring?.Zone.Material : l.Rule?.Material is { } name ? Materials.Find(m, name)?.Id : Materials.Find(m, "Reinforced concrete")?.Id;
                var color = material != null ? Materials.ColorOf(m, material) : "#a8a8a8";
                var label = l.Kind == "downstand" ? "Ring beam over opening" : l.Precast ? "Lintel (precast)" : "Lintel (cast)";
                parts.Add(new GeometryPart(label, color, box, material, $"{l.Kind}:{l.Key}:{i}") { TypeId = l.Kind == "lintel" ? l.Rule?.BeamTypeIn(m) : null });
            }
        }
        // The layers of the walls and floors round reaching into this one and set to cut what they meet (forum topic 89).
        parts = LayerExtents.CutBy(wall, m, services, parts, problems);
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
        var z = Spatial.Base(m, wall);
        var c = PathCurve.Of(wall);
        IReadOnlyList<Vec2> fp = c.IsArc ? c.Band(_ => 0, _ => c.Length, y0, y1) : [start + left * y0, end + left * y0, end + left * y1, start + left * y1];
        return Prism.ExtrudeFootprint(fp, z, HeightOf(m, wall), "wall");
    }

    /// <summary>
    /// The wall's lintel pieces in its storey, before its corners (see
    /// <see cref="LintelJoins"/>): over its openings, through its core, inside
    /// its length as its joins leave it.
    /// </summary>
    /// <remarks><paramref name="through"/>: the openings of other walls passing through it (<see cref="ThroughOpenings.Into"/>), when the caller has them already.</remarks>
    public static List<LintelPiece> LintelPieces(Entity wall, IModelView m, IServices? services, IReadOnlyList<Openings.Placed>? through = null)
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
        var z = Spatial.Base(m, wall);
        var height = HeightOf(m, wall);
        var zones = TypeOf(m, wall.Id) is { } type ? WallZones.Bands(WallZones.Of(type), height) : [];
        var shift = Vec2.Dot(start - Start(wall), dir);
        // To the core's farther corner at a joint (the corner settles the rest), to its nearer at a free end cut at an angle (inside the wall).
        double Reach(EndCut c, bool joint) => joint ? Math.Max(At(c, core.From), At(c, core.To)) : Math.Min(At(c, core.From), At(c, core.To));
        var (x0, x1) = (-Reach(startCut, ends[0] != null), length + Reach(endCut, ends[1] != null));
        var lintels = Lintels.ForWall(
            Openings.PlacedOn(m, wall.Id).Concat(through ?? ThroughOpenings.Into(m, wall).Select(x => x.Placed).ToList()).Select(o => new LintelOpening(o.Opening.Id, o.Along - o.Width / 2 - shift, o.Along + o.Width / 2 - shift, o.Sill, o.Sill + o.Height,
                o.Spec?.Lintel is { Mode: not "none" } own ? own.Resolve(m) : null, o.Spec?.Lintel is { Mode: "none" } || BareOpenings.NoLintel(o))).ToList(),
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
        // A beam type as its product (report #135): its section, lengths, material and bearing read now, so the walls follow the type.
        return rule is { Mode: not "none" } ? rule.Resolve(m) : null;
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
        var first = Polygon2.ClipHalf(poly, p => Vec2.Dot(p - origin, dir) - from);
        return first.Count < 3 ? [] : Polygon2.ClipHalf(first, p => to - Vec2.Dot(p - origin, dir));
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
