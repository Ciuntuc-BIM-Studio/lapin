using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Floors. An IfcSlab is its contour in plan (storey-local metres, counter-
/// clockwise) and the offset of its top from its storey; its build-up comes
/// from its IfcSlabType's layer set, laid from the top down by its own
/// IfcMaterialLayerSetUsage (AXIS3, NEGATIVE). Layers are listed top to bottom.
/// </summary>
public static class Slabs
{
    /// <summary>The type new floors get when none is chosen: a finish, the screed, the reinforced concrete slab (the core).</summary>
    public static readonly IReadOnlyList<LayerSpec> DefaultLayers =
    [
        new("Floor finish", 0.02),
        new("Screed", 0.05),
        new("Reinforced concrete", 0.15, Core: true),
    ];

    /// <summary>The outline's corners as stored (its <c>contour</c>), whatever its edges: what grips, edge indices and layer extents count.</summary>
    public static IReadOnlyList<Vec2> Corners(Entity slab) =>
        slab.Attrs["contour"] is JsonArray arr
            ? arr.OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0)).ToList()
            : [];

    /// <summary>The rises of the outline's edges (forum topic 97, <see cref="ArcOutline"/>); null when every edge is straight.</summary>
    public static double[]? Rises(Entity e) => e.Attrs[ArcOutline.Key] is JsonArray ? ArcOutline.Rises(e.Attrs[ArcOutline.Key], Corners(e).Count) : null;

    /// <summary>
    /// The outline as built (storey-local metres, counter-clockwise): its corners, and along each curved edge the points of
    /// its arc by the chord rule (<see cref="Chords"/>). The same as <see cref="Corners"/> when every edge is straight.
    /// </summary>
    public static IReadOnlyList<Vec2> Contour(Entity slab)
    {
        var c = Corners(slab);
        // Its elliptical edges too (audit S4).
        if (Ellipses(slab) is { } el) return ArcOutline.Points(c, Rises(slab), null, el);
        return slab.Attrs[ArcOutline.Key] is JsonArray && ArcOutline.Rises(slab.Attrs[ArcOutline.Key], c.Count) is { } r ? ArcOutline.Points(c, r) : c;
    }

    /// <summary>The outline's edges that run along an ellipse (audit S4, <see cref="EllipseEdge"/>); null when none does.</summary>
    public static EllipseEdge?[]? Ellipses(Entity e) => e.Attrs[EllipseEdge.Key] is JsonArray ? EllipseEdge.Of(e) : null;

    /// <summary>The area the outline encloses, exactly: its arcs as arcs, its elliptical edges as the ellipse (unsigned).</summary>
    public static double Area(Entity e) => Math.Abs(ArcOutline.SignedArea(Corners(e), Rises(e), Ellipses(e)));

    /// <summary>The length round the outline, its arcs and elliptical edges at their own length.</summary>
    public static double Perimeter(Entity e) => ArcOutline.Perimeter(Corners(e), Rises(e), Ellipses(e));

    /// <summary>
    /// The outline as built and the layer extents on it: an extent at a curved edge goes on each chord of its arc, so the layer's
    /// edge follows the arc out or in. <paramref name="edgeOf"/>: the corner edge each built edge lies on.
    /// </summary>
    public static (IReadOnlyList<Vec2> Contour, IReadOnlyList<LayerExtents.Extent> Extents) Built(Entity slab, IReadOnlyList<LayerExtents.Extent> extents, List<int>? edgeOf = null)
    {
        var c = Corners(slab);
        var (r, el) = (Rises(slab), Ellipses(slab));
        if (r == null && el == null)
        {
            edgeOf?.AddRange(Enumerable.Range(0, c.Count));
            return (c, extents);
        }
        var of = edgeOf ?? [];
        var pts = ArcOutline.Points(c, r, of, el);
        var o = new List<LayerExtents.Extent>();
        foreach (var x in extents)
            if (x.Edge == LayerExtents.Edge && x.Index is { } i) { for (var k = 0; k < of.Count; k++) if (of[k] == i) o.Add(x with { Index = k }); }
            else o.Add(x);
        return (pts, o);
    }

    public static JsonArray ContourJson(IEnumerable<Vec2> pts) => new(pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray());

    /// <summary>Writes an outline into the attributes: its corners, and its edges' rises (taken away when every edge is straight).</summary>
    public static void SetOutline(JsonObject attrs, IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises)
    {
        attrs["contour"] = ContourJson(corners);
        ArcOutline.Write(attrs, rises);
    }

    /// <summary>
    /// An outline's elliptical edges (audit S4) written after its corners were: the command's <c>ellipses</c> (each edge's ends on its
    /// ellipse), else those <paramref name="was"/> had whose ends its corners still lie on; none, the attribute taken away.
    /// </summary>
    public static void SetEllipses(JsonObject attrs, JsonObject a, Entity? was)
    {
        var corners = Args.StoredPoints(attrs["contour"]);
        var el = EllipseEdge.Arg(a, corners, was);
        EllipseEdge.Write(attrs, el);
        // An edge along an ellipse is no arc.
        if (el != null && ArcOutline.Rises(attrs[ArcOutline.Key], corners.Count) is { } r)
            ArcOutline.Write(attrs, r.Select((x, i) => el[i] != null ? 0 : x).ToList());
    }

    /// <summary>
    /// The outline a command gives (<c>points</c> and, optional, <paramref name="arcs"/>: each edge's rise, <see cref="ArcOutline"/>),
    /// checked; <paramref name="was"/>: the element changed, whose arcs stay when only its corners are given, as many as it has.
    /// </summary>
    public static (List<Vec2> Corners, double[]? Rises) OutlineArg(JsonNode? arcs, IReadOnlyList<Vec2> points, Entity? was = null, string what = "A floor")
    {
        double[]? rises = arcs is JsonArray arr ? [.. arr.Select(x => Json.Number(x) ?? 0)]
            : was != null && Rises(was) is { } r && r.Length == points.Count ? r : null;
        if (rises == null)
        {
            var straight = what == "A floor" ? CheckContour(points) : ArcOutline.Check(points, null, what).Corners;
            return ([.. straight], null);
        }
        return ArcOutline.Check(points, rises, what);
    }

    /// <summary>A contour as a command gives it: at least three distinct points, made counter-clockwise; refused when it encloses nothing.</summary>
    public static IReadOnlyList<Vec2> CheckContour(IReadOnlyList<Vec2> pts)
    {
        var clean = new List<Vec2>();
        foreach (var p in pts)
            if (clean.Count == 0 || (p - clean[^1]).Length > 1e-6) clean.Add(p);
        if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 1e-6) clean.RemoveAt(clean.Count - 1);
        if (clean.Count < 3) throw new CommandException("A floor needs at least three corners.");
        if (Math.Abs(Polygon2.SignedArea(clean)) < 1e-6) throw new CommandException("The floor's outline encloses nothing.");
        return Polygon2.EnsureCcw(clean).ToList();
    }

    public static Entity EnsureDefaultType(IEdit edit) =>
        edit.Model.OfClass(IfcClass.SlabType).FirstOrDefault() ?? CreateType(edit, null, DefaultLayers);

    public static Entity CreateType(IEdit edit, string? name, IReadOnlyList<LayerSpec> layers)
    {
        LayerSets.Validate(layers, "A floor type");
        var core = layers.FirstOrDefault(l => l.Core) ?? layers.MaxBy(l => l.Thickness)!;
        var typeName = string.IsNullOrWhiteSpace(name) ? $"{core.Material} floor {LayerSets.Millimetres(layers.Sum(l => l.Thickness))}" : name.Trim();
        var type = new Entity(edit.NewId(), IfcClass.SlabType, typeName, [], "FLOOR");
        edit.Add(type);
        LayerSets.Attach(edit, type, layers);
        return type;
    }

    public static void UpdateType(IEdit edit, Entity type, string? name, IReadOnlyList<LayerSpec>? layers)
    {
        if (name != null)
        {
            if (name.Trim().Length == 0) throw new CommandException("A type's name cannot be empty.");
            edit.Replace(type.With(name: name.Trim()));
        }
        if (layers == null) return;
        LayerSets.Validate(layers, "A floor type");
        LayerSets.Rewrite(edit, edit.Model.Get(type.Id)!, layers);
        foreach (var slab in edit.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(s => s.Class == IfcClass.Slab).ToList())
            LayOut(edit, slab, type);
    }

    /// <summary>The floor's usage: the layers from its top down.</summary>
    static void LayOut(IEdit edit, Entity slab, Entity type) =>
        LayerSets.LayOut(edit, slab, type, "AXIS3", (_, _) => 0, negativeWhenNew: true);

    public static void SetType(IEdit edit, Entity slab, Entity type)
    {
        edit.Relate(IfcClass.RelDefinesByType, type.Id, slab.Id);
        LayOut(edit, slab, type);
    }

    public static Entity Add(IEdit edit, string storeyId, IReadOnlyList<Vec2> contour, double offset, Entity? type, IReadOnlyList<double>? rises = null)
    {
        var (pts, arcs) = rises != null ? ArcOutline.Check(contour, rises) : ([.. CheckContour(contour)], null);
        type ??= EnsureDefaultType(edit);
        var attrs = new JsonObject { ["offset"] = offset };
        SetOutline(attrs, pts, arcs);
        var slab = new Entity(edit.NewId(), IfcClass.Slab, type.Name, attrs, "FLOOR");
        edit.Add(slab);
        Spatial.Contain(edit, storeyId, slab.Id);
        SetType(edit, slab, type);
        return slab;
    }

    /// <summary>The floor's layers, top first: their bands below its top (negative), their materials.</summary>
    public static IReadOnlyList<(double From, double To, string? Material, string Name, int Index)> Section(IModelView m, Entity slab)
    {
        if (LayerSets.BandsOf(m, slab.Id) is { } bands) return bands.Select(b => (b.From, b.To, (string?)b.Material, b.Name, b.Index)).ToList();
        var t = slab.NumOrNull("thickness") ?? 0.2;
        return [(-t, 0.0, null, slab.Name, 0)];
    }

    /// <summary>The floor: a slab per layer, from its top down; in plan, its outline (it lies below the cut).</summary>
    public static ElementGeometry? Geometry(Entity slab, IModelView m, IServices? services = null)
    {
        // A layer's own extents short of the outline (forum topic 89); those past it are pieces of their own (ExtentPieces), added after the cut priorities.
        var (contour, extents) = Built(slab, LayerExtents.Effective(slab, m));
        if (contour.Count < 3) return new ElementGeometry([], Diagnostics: ["The floor has no outline."]);
        var top = Top(m, slab);
        var parts = new List<GeometryPart>();
        var problems = new List<string>();
        // Its own holes (audit S4): islands of its outline, each layer built round them (no boolean needed).
        var own = OutlineHoles.Built(slab);
        foreach (var (from, to, material, name, index) in Section(m, slab))
        {
            if (!(to - from > 1e-9)) continue;
            var layer = LayerExtents.SlabContour(contour, extents, index, Short);
            if (layer == null) problems.Add($"{name}: its edges moved as its extents say would leave no outline; built to the floor's.");
            if (Prism.ExtrudeFootprint(layer ?? contour, own, top + from, to - from, name) is { } solid)
                parts.Add(new GeometryPart(name, Materials.ColorOf(m, material), solid, material, $"layer:{index}"));
        }
        var holes = Holes(m, slab.Id).Select(h => h.Contour).ToList();
        var pieces = ExtentPieces(slab, m, services, cuttingOnly: false);
        if (holes.Count > 0 && (parts.Count > 0 || pieces.Count > 0) && (services?.Get<GeometryOptions>()?.CutOpenings ?? true))
        {
            if (services?.Get<ISolidBooleans>() is { } booleans)
            {
                // Each opening as a prism through the whole build-up, a little proud of it.
                var section = Section(m, slab);
                var (z0, z1) = (top + section.Min(b => b.From) - 0.05, top + section.Max(b => b.To) + 0.05);
                var cutters = holes.Select(h => Prism.ExtrudeFootprint(h, z0, z1 - z0, "opening")).OfType<Solid>().ToList();
                parts = parts.Select(p => booleans.Difference(p.Solid, cutters, p.Solid.Tag) is { } cut ? p with { Solid = cut } : null).OfType<GeometryPart>().ToList();
                pieces = pieces.Select(p => booleans.Difference(p.Solid, cutters, p.Solid.Tag) is { } cut ? p with { Solid = cut } : null).OfType<LayerExtents.Piece>().ToList();
            }
            else problems.Add("Openings in floors need the boolean library (manifold), missing here.");
        }
        parts = CutPriorities.Apply(slab, m, services, parts, problems);
        // The walls' layers going on into this floor's layers ranked under them (the forum's topic #30): what they take of it.
        if (WallExtensions(slab, m, services) is { Count: > 0 } taken && services?.Get<ISolidBooleans>() is { } cut)
            parts = parts.Select(p =>
            {
                var b = Measure.Bounds(p.Solid);
                var reach = taken.Where(x => b is { } pb && Measure.Bounds(x) is { } xb && xb.Min.Z < pb.Max.Z - 1e-6 && xb.Max.Z > pb.Min.Z + 1e-6).ToList();
                if (reach.Count == 0) return p;
                try { return cut.Difference(p.Solid, reach, p.Solid.Tag) is { } s && Measure.Volume(s) > 1e-9 ? p with { Solid = s } : null; }
                catch (InvalidOperationException ex) { problems.Add($"{p.Name}: a wall's layer could not be taken out of it ({ex.Message})."); return p; }
            }).OfType<GeometryPart>().ToList();
        // The layers past the outline (forum topic 89): one body with the layer each goes on from.
        foreach (var x in pieces)
            LayerExtents.AddJoined(parts, new GeometryPart(x.Name, Materials.ColorOf(m, x.Material), x.Solid, x.Material, x.Key), services?.Get<ISolidBooleans>());
        // What the walls and floors round reaching into this one and set to cut take out of it.
        parts = LayerExtents.CutBy(slab, m, services, parts, problems);
        // In plan, each layer's own outline, drawn once (the floor's alone when no layer moves its edges).
        var outlines = new List<IReadOnlyList<Vec2>>();
        foreach (var (from, to, _, _, index) in Section(m, slab))
            if (to - from > 1e-9 && (LayerExtents.SlabContour(contour, extents, index, x => x.Offset) ?? contour) is var c && !outlines.Any(o => Same(o, c)))
                outlines.Add(c);
        if (outlines.Count == 0) outlines.Add(contour);
        var plan = outlines.Select(c => new PlanItem("outline", true, c)).ToList();
        plan.AddRange(holes.Concat(own).Select(h => new PlanItem("outline", true, h)));
        var built = new ElementGeometry(parts, plan, contour, problems.Count > 0 ? problems : null);
        // A beam-and-block floor (report #134): its core as joists, blocks and cast concrete.
        return BeamBlock.Of(m, slab) is { } bb ? BeamBlock.Apply(slab, built, m, services, bb) : built;
    }

    static double Short(LayerExtents.Extent x) => Math.Min(0, x.Offset);

    static bool Same(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b) => a.Count == b.Count && a.Zip(b).All(p => (p.First - p.Second).Length < 1e-9);

    /// <summary>
    /// The pieces of a floor's layers past its outline (forum topic 89, <see cref="LayerExtents"/>): for each layer, what its
    /// outline with its edges moved out takes beyond the outline it is built to (its extents short of the edges taken off).
    /// <paramref name="cuttingOnly"/>: only the extents set to cut what they meet. None without the boolean library.
    /// </summary>
    public static List<LayerExtents.Piece> ExtentPieces(Entity slab, IModelView m, IServices? services, bool cuttingOnly, Func<LayerExtents.Extent, bool>? which = null)
    {
        var o = new List<LayerExtents.Piece>();
        var (contour, extents) = Built(slab, LayerExtents.Effective(slab, m));
        bool Takes(LayerExtents.Extent x) => x.Offset > 1e-9 && (!cuttingOnly || (x.Cuts && (which?.Invoke(x) ?? true)));
        if (!extents.Any(Takes) || services?.Get<ISolidBooleans>() is not { } booleans) return o;
        if (contour.Count < 3) return o;
        var top = Top(m, slab);
        double Past(LayerExtents.Extent x) => Takes(x) ? x.Offset : Short(x);
        foreach (var (from, to, material, name, index) in Section(m, slab))
        {
            if (!(to - from > 1e-9) || LayerExtents.SlabDistances(extents, index, contour.Count, x => Takes(x) ? x.Offset : 0) == null) continue;
            if (LayerExtents.SlabContour(contour, extents, index, Past) is not { } outer) continue;
            var inner = LayerExtents.SlabContour(contour, extents, index, Short) ?? contour;
            if (Prism.ExtrudeFootprint(outer, top + from, to - from, name) is not { } whole) continue;
            // The layer as built, a little proud above and below so no face of the two lies in another.
            var built = Prism.ExtrudeFootprint(inner, top + from - 1e-3, to - from + 2e-3, name);
            Solid? piece;
            try { piece = built != null ? booleans.Difference(whole, [built], whole.Tag) : whole; }
            catch (InvalidOperationException) { piece = null; }
            if (piece != null && Measure.Volume(piece) > 1e-9)
                o.Add(new LayerExtents.Piece(piece, material, name, $"layer:{index}", LayerSets.BandsOf(m, slab.Id)?.Any(b => b.Index == index && b.Core) == true, top + from, top + to));
        }
        return o;
    }

    /// <summary>
    /// The pieces of the walls' layers that go on into this floor (<see cref="Walls.Extensions"/>): read only when a wall type
    /// has such a layer, then of the walls of those types near the floor's outline.
    /// </summary>
    static List<Solid> WallExtensions(Entity slab, IModelView m, IServices? services)
    {
        var o = new List<Solid>();
        var types = m.OfClass(IfcClass.WallType).Where(t => LayerSets.LayersOf(m, t.Id).Any(l => l.ExtendDown == true || l.ExtendUp == true)).ToList();
        if (types.Count == 0) return o;
        var contour = Contour(slab);
        var (x0, y0, x1, y1) = (contour.Min(p => p.X) - 1, contour.Min(p => p.Y) - 1, contour.Max(p => p.X) + 1, contour.Max(p => p.Y) + 1);
        foreach (var type in types)
            foreach (var wall in m.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(w => w.Class == IfcClass.Wall))
            {
                var (a, b) = (Walls.Start(wall), Walls.End(wall));
                if (Math.Max(a.X, b.X) < x0 || Math.Min(a.X, b.X) > x1 || Math.Max(a.Y, b.Y) < y0 || Math.Min(a.Y, b.Y) > y1) continue;
                o.AddRange(Walls.Extensions(wall, m, services).Where(x => x.Slab == slab.Id).Select(x => x.Solid));
            }
        return o;
    }

    /// <summary>The prism an opening takes out of its floor, through the whole build-up and a little proud of it: what IFC writes as the opening's body.</summary>
    public static Solid? OpeningSolid(IModelView m, Entity slab, Entity opening)
    {
        var contour = HoleContour(m, opening);
        var section = Section(m, slab);
        if (contour.Count < 3 || section.Count == 0) return null;
        var top = Top(m, slab);
        var (z0, z1) = (top + section.Min(b => b.From) - 0.05, top + section.Max(b => b.To) + 0.05);
        return Prism.ExtrudeFootprint(contour, z0, z1 - z0, "opening");
    }

    /// <summary>The openings that void a floor (shafts, stairwells): each an IfcOpeningElement with its contour in plan.</summary>
    public static IEnumerable<(Entity Opening, IReadOnlyList<Vec2> Contour)> Holes(IModelView m, string slabId) =>
        m.RelatedOf(slabId, IfcClass.RelVoidsElement).Where(o => o.Class == IfcClass.OpeningElement)
            .Select(o => (o, HoleContour(m, o))).Where(h => h.Item2.Count >= 3);

    /// <summary>An opening's outline: its own, or, for the opening a stair asked for, the stair's as it is now.</summary>
    public static IReadOnlyList<Vec2> HoleContour(IModelView m, Entity opening) =>
        opening.Str("stairId") is { } stairId && m.Get(stairId) is { Class: IfcClass.Stair or IfcClass.Ramp } stair ? Stairs.StairBuilder.OpeningContour(m, stair) ?? [] : Contour(opening);

    public static Entity AddOpening(IEdit edit, Entity slab, IReadOnlyList<Vec2> contour, IReadOnlyList<double>? rises = null)
    {
        var (pts, arcs) = rises != null ? ArcOutline.Check(contour, rises, "An opening") : ([.. CheckContour(contour)], null);
        var attrs = new JsonObject();
        SetOutline(attrs, pts, arcs);
        var opening = new Entity(edit.NewId(), IfcClass.OpeningElement, "Floor opening", attrs, "OPENING");
        edit.Add(opening);
        edit.AddRelation(new Relation(edit.NewId(), IfcClass.RelVoidsElement, slab.Id, [opening.Id]));
        return opening;
    }

    public static double Top(IModelView m, Entity slab) => Spatial.Elevation(m, slab) + slab.Num("offset");

    /// <summary>The floor's whole thickness over its outline: what it cuts lower priorities with.</summary>
    public static Solid? Envelope(Entity slab, IModelView m)
    {
        var contour = Contour(slab);
        if (contour.Count < 3) return null;
        var section = Section(m, slab);
        var top = Top(m, slab);
        var bottom = section.Min(b => b.From);
        // Its own holes are not floor (audit S4): what it cuts has them.
        return Prism.ExtrudeFootprint(contour, OutlineHoles.Built(slab), top + bottom, section.Max(b => b.To) - bottom, "slab");
    }

    /// <summary>The area a layer of no thickness covers: the net area of the next layer with a thickness towards the core (the core always has one).</summary>
    static double? ZeroLayerArea(IModelView m, Entity slab, ElementGeometry g, IReadOnlyList<(double From, double To, string? Material, string Name, int Index)> section, int i)
    {
        if (LayerSets.BandsOf(m, slab.Id)?.FirstOrDefault(b => b.Core) is not { Core: true } coreBand) return null;
        var core = coreBand.Index;
        var step = i < core ? 1 : -1;
        for (var j = i + step; j >= 0 && j < section.Count; j += step)
        {
            var (from, to, _, _, index) = section[j];
            if (to - from <= 1e-9) continue;
            var v = g.Parts.Where(p => p.Key == $"layer:{index}").Sum(p => Measure.Volume(p.Solid));
            return v > 0 ? v / (to - from) : null;
        }
        return null;
    }

    public static IEnumerable<QuantitySet> Quantities(Entity slab, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        // Exactly, its arcs as arcs (forum topic 97).
        // Its own holes are not part of it (audit S4): the profile's area, as IFC measures an area with voids.
        var area = Area(slab) - OutlineHoles.Area(slab);
        var section = Section(m, slab);
        var width = section.Max(b => b.To) - section.Min(b => b.From);
        if (BeamBlock.Of(m, slab) is { } bb)
            foreach (var q in BeamBlock.Quantities(bb, g, m, area * width)) yield return q;
        var net = g.Parts.Sum(p => Measure.Volume(p.Solid));
        yield return new QuantitySet("Qto_SlabBaseQuantities",
        [
            new("Width", width, "LENGTH"),
            new("Perimeter", Perimeter(slab), "LENGTH"),
            new("GrossArea", area, "AREA", "Gross area"),
            new("NetArea", width > 0 ? net / width : 0, "AREA", "Net area"),
            new("GrossVolume", area * width, "VOLUME", "Gross volume"),
            new("NetVolume", net, "VOLUME", "Net volume"),
        ]);
        for (var i = 0; i < section.Count; i++)
        {
            var (from, to, material, name, index) = section[i];
            // A layer of no thickness (a membrane, paint; decision 291): the area of the layer it lies on (the next one with a thickness towards the core), no volume.
            if (to - from <= 1e-9)
            {
                if (ZeroLayerArea(m, slab, g, section, i) is { } za)
                    yield return new QuantitySet("Lapin_SlabLayerQuantities", [new("Width", 0, "LENGTH"), new("NetArea", za, "AREA", "Net area"), new("NetVolume", 0, "VOLUME", "Net volume")], $"layer:{index}", name, material);
                continue;
            }
            var part = g.Parts.FirstOrDefault(p => p.Key == $"layer:{index}");
            if (part == null) continue;
            var v = Measure.Volume(part.Solid);
            yield return new QuantitySet("Lapin_SlabLayerQuantities",
            [
                new("Width", to - from, "LENGTH"),
                new("NetArea", to - from > 0 ? v / (to - from) : 0, "AREA", "Net area"),
                new("NetVolume", v, "VOLUME", "Net volume"),
            ], $"layer:{index}", name, material);
        }
    }
}
