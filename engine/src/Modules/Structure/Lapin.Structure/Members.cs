using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>
/// Columns: an IfcColumn stands at a point of its storey, turned by its
/// rotation, its section from its IfcColumnType, as tall as the storey (or
/// its type's or its own height).
/// </summary>
public static class Columns
{
    public static Entity CreateType(IEdit edit, string? name, Section section, double? height, string material = "Reinforced concrete")
    {
        var label = section.Label;
        var attrs = new JsonObject { ["section"] = section.ToJson(), ["material"] = Materials.Ensure(edit, material).Id };
        if (height != null) attrs["height"] = height;
        var type = new Entity(edit.NewId(), IfcClass.ColumnType, string.IsNullOrWhiteSpace(name) ? $"Column {label}" : name.Trim(), attrs, "COLUMN");
        edit.Add(type);
        return type;
    }

    public static Entity DefaultType(IEdit edit) => CreateType(edit, null, Section.Rectangle(0.3, 0.3), null);

    public static (Section Section, string? Material, double Height) Spec(IModelView m, Entity column)
    {
        var type = Place.TypeOf(m, column.Id);
        var section = Section.From(type?.Attrs["section"]) ?? Section.Rectangle(0.3, 0.3);
        var height = column.NumOrNull("height") ?? type?.NumOrNull("height") ?? Place.StoreyHeight(m, column);
        return (section, type?.Str("material"), height);
    }

    /// <summary>
    /// The column as built (docs/plan-attach-to.md §3.6): its ends attached to bodies read now. Its own body and what is
    /// measured from it (quantities, rebar, faces, the analytical line, IFC) read this; what other elements read of it (its
    /// envelope, its drawn height for a beam's span, a steel member's axis) stays on <see cref="Spec"/>, so no build goes round
    /// in a circle. Without services, or attachments, the column as drawn.
    /// </summary>
    public static BuiltColumn Built(IModelView m, Entity column, IServices? services) => ColumnEnds.Of(m, column, services);

    /// <summary>The envelope a column cuts other elements with: as drawn (<see cref="Spec"/>), never as attached.</summary>
    public static Solid? Envelope(Entity column, IModelView m) => Envelope(column, m, true);

    /// <param name="withLayers">With its finish layers (decision 719): their outline, so a wall round the column stops at its plaster; false: the core's.</param>
    static Solid? Envelope(Entity column, IModelView m, bool withLayers)
    {
        var (section, _, height) = Spec(m, column);
        var z = Place.Elevation(m, column) + column.Num("baseOffset");
        var outline = withLayers && Layers(m, column) is { Count: > 0 } layers ? ColumnWraps.Shapes(section, layers).Outline : section.Outline();
        return Prism.ExtrudeFootprint(Place.Placed(outline, Place.Point(column), column.Num("rotation")), z, height, "column");
    }

    /// <summary>The finish layers its type lays round it (decision 719); none for a column placed by an IFC or without a type.</summary>
    public static IReadOnlyList<WrapLayer> Layers(IModelView m, Entity column) =>
        column.Attrs["at"] is null || StoredBodies.Has(column) ? [] : ColumnLayers.Read(Place.TypeOf(m, column.Id));

    public static ElementGeometry? Geometry(Entity column, IModelView m, IServices? services)
    {
        var (section, material, _) = Spec(m, column);
        var built = Built(m, column, services);
        if (built.Shaped) return Attached(column, m, services, built);
        if (Envelope(column, m, false) is not { } solid) return new ElementGeometry([], Diagnostics: ["The column has no height."]);
        // At LOD 400 the real section (decision 415); every other reader the light one, with the same box.
        var fine = services?.Get<GeometryOptions>()?.Fine == true;
        var (outlineOf, holes) = fine ? section.Real() : (section.Outline(), section.Holes());
        if (fine && holes.Count == 0)
        {
            var (z, h) = (Place.Elevation(m, column) + column.Num("baseOffset"), Spec(m, column).Height);
            solid = Prism.ExtrudeFootprint(Polygon2.EnsureCcw(Place.Placed(outlineOf, Place.Point(column), column.Num("rotation")).ToList()), z, h, "column") ?? solid;
        }
        // A hollow section stands open inside (decision 268); the envelope a column cuts with stays whole.
        if (holes.Count > 0)
        {
            var z = Place.Elevation(m, column) + column.Num("baseOffset");
            IReadOnlyList<Vec3> Up(IEnumerable<Vec2> ring) => Place.Placed(ring, Place.Point(column), column.Num("rotation")).Select(p => p.At(z)).ToList();
            solid = Prism.Extrude(Up(outlineOf), holes.Select(Up).ToList(), new Vec3(0, 0, Spec(m, column).Height), "column") ?? solid;
        }
        var problems = new List<string>(built.Problems);
        return Finish(column, m, services, section, material, solid, problems);
    }

    static ElementGeometry Finish(Entity column, IModelView m, IServices? services, Section section, string? material, Solid solid, List<string> problems)
    {
        var own = new List<GeometryPart> { new(column.Name, Materials.ColorOf(m, material), solid, material, "body") };
        own.AddRange(LayerParts(column, m, services, section, problems));
        var parts = CutPriorities.Apply(column, m, services, own, problems);
        // The walls' and floors' layers reaching into it and set to cut (forum topic 89).
        parts = LayerExtentCuts.Apply(column, m, services, parts, problems);
        var footprint = Place.Placed(section.Outline(), Place.Point(column), column.Num("rotation"));
        return new ElementGeometry(parts, [new PlanItem("cut", true, footprint)], [Place.Point(column)], problems.Count > 0 ? problems : null) { FineShape = section.HasRealShape };
    }

    /// <summary>
    /// Its finish layers as parts (decision 719): each layer's rings round the core (<see cref="ColumnWraps.Shapes"/>) between the
    /// column's ends as built (attached ends cut them as they cut the core), keyed layer:1, layer:2… outward, in their materials.
    /// </summary>
    static IEnumerable<GeometryPart> LayerParts(Entity column, IModelView m, IServices? services, Section section, List<string> problems)
    {
        var layers = Layers(m, column);
        if (layers.Count == 0) yield break;
        var built = Built(m, column, services);
        var (shapes, _) = ColumnWraps.Shapes(section, layers);
        for (var i = 0; i < shapes.Count; i++)
        {
            var l = layers[i];
            var name = l.Name ?? m.Get(l.Material)?.Name ?? $"Layer {i + 1}";
            foreach (var region in shapes[i])
            {
                var placed = region.Select(r => ColumnWraps.Placed(column, r)).ToList();
                if (ColumnEnds.Body(built, placed[0], placed.Skip(1).ToList(), services, problems) is { } ring)
                    yield return new GeometryPart(name, Materials.ColorOf(m, l.Material), ring, l.Material, ColumnLayers.PartKey(i));
            }
        }
    }

    /// <summary>A column whose ends are attached (docs/plan-attach-to.md §3.3): the prism between its ends as built, cut by the cut order as any column.</summary>
    static ElementGeometry Attached(Entity column, IModelView m, IServices? services, BuiltColumn built)
    {
        var problems = new List<string>(built.Problems);
        var fine = services?.Get<GeometryOptions>()?.Fine == true;
        var (outer, holes) = ColumnEnds.Rings(column, built.Section, fine);
        if (ColumnEnds.Body(built, outer, holes, services, problems) is not { } solid)
            return new ElementGeometry([], Diagnostics: [.. problems, "The column has no height."]);
        return Finish(column, m, services, built.Section, built.Material, solid, problems);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity column, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        // Measured from the column as built (its attached ends), the one its geometry was just built from.
        var built = ColumnEnds.LastOf(m, column);
        var (section, height) = (built.Section, built.Height);
        // The body the views draw has sharp corners: the real section holds RealRatio more (decision 418).
        var net = g.Parts.Where(p => !IsLayer(p.Key)).Sum(p => Measure.Volume(p.Solid)) * section.RealRatio;
        yield return new QuantitySet("Qto_ColumnBaseQuantities",
        [
            new("Length", height, "LENGTH"),
            new("CrossSectionArea", section.Area, "AREA", "Cross section"),
            new("GrossVolume", section.Area * height, "VOLUME", "Gross volume"),
            new("NetVolume", net, "VOLUME", "Net volume"),
        ]);
        // Its finish layers (decision 719), each measured from its own parts: thickness, the area it covers, its volume.
        var layers = Layers(m, column);
        for (var i = 0; i < layers.Count; i++)
        {
            var key = ColumnLayers.PartKey(i);
            var mine = g.Parts.Where(p => p.Key == key).ToList();
            if (mine.Count == 0) continue;
            var v = mine.Sum(p => Measure.Volume(p.Solid));
            var t = layers[i].Thickness;
            yield return new QuantitySet("Lapin_ColumnLayerQuantities",
            [
                new("Width", t, "LENGTH"),
                new("NetSideArea", v / t, "AREA", "Net side area"),
                new("NetVolume", v, "VOLUME", "Net volume"),
            ], key, mine[0].Name, layers[i].Material);
        }
    }

    static bool IsLayer(string? key) => key != null && key.StartsWith("layer:", StringComparison.Ordinal);
}

/// <summary>
/// Where a beam runs clear of the columns on its axis (decision 225): the
/// stations along its <see cref="PathCurve"/> inside each column that reaches
/// its height (<see cref="Columns"/>, merged, in order), the clear span
/// <see cref="S0"/>…<see cref="S1"/> between the faces of the columns its
/// ends stand in (the whole axis where none does), and the pieces of the span
/// outside the columns it passes through. The axis the user drew stays from
/// column axis to column axis; the body, the plan outline, the stirrups and
/// the quantities take the span.
/// </summary>
public sealed record BeamSpan(PathCurve Curve, double S0, double S1, IReadOnlyList<(double In, double Out)> Columns)
{
    public bool TrimmedStart => S0 > 1e-9;
    public bool TrimmedEnd => S1 < Curve.Length - 1e-9;
    /// <summary>Shorter than a millimetre: the beam lies in columns.</summary>
    public bool Empty => S1 - S0 <= 1e-3;
    public Vec2 Start => Curve.PointAt(S0);
    public Vec2 End => Curve.PointAt(S1);

    /// <summary>The parts of the span outside the columns it passes through, in order along the axis.</summary>
    public List<(double A, double B)> Pieces
    {
        get
        {
            var o = new List<(double, double)>();
            if (Empty) return o;
            var at = S0;
            foreach (var (s, e) in Columns)
            {
                if (e <= S0 || s >= S1) continue;
                if (s - at > 1e-6) o.Add((at, s));
                at = Math.Max(at, e);
            }
            if (S1 - at > 1e-6) o.Add((at, S1));
            return o;
        }
    }

    /// <summary>The clear length: the pieces together.</summary>
    public double ClearLength => Pieces.Sum(p => p.B - p.A);
}

/// <summary>
/// Beams: an IfcBeam runs along its axis, its section from its IfcBeamType,
/// its top at the level above its storey (plus its own offset). Beams join
/// in plan at corners and in T as walls do; a beam is a path element. Drawn
/// from column axis to column axis, a beam stops at the columns' faces
/// (<see cref="SpanOf"/>).
/// </summary>
public static class Beams
{
    /// <summary>The stirrups and the body sample a curved axis this far apart to find a column's faces.</summary>
    const double SampleStep = 0.005;

    /// <summary>
    /// The beam's clear span and the columns on its axis: every column whose
    /// height reaches the beam's (<see cref="Columns.Spec"/>, its storey's
    /// elevation and its base offset) and whose footprint the axis crosses.
    /// </summary>
    public static BeamSpan SpanOf(IModelView m, Entity beam) => Spans.Get(beam, m, v => Solve(v, beam));

    /// <summary>Every beam's span, once per revision with what it read: its body, its envelope (asked by every element it may cut), its faces, its stirrups and its quantities ask.</summary>
    static readonly RevisionCache<BeamSpan> Spans = new();
    static readonly RevisionCache<Solid?> Envelopes = new();

    static BeamSpan Solve(IModelView m, Entity beam)
    {
        var c = PathCurve.Of(beam);
        var length = c.Length;
        var top = Top(m, beam);
        var bottom = top - Spec(m, beam).Section.Depth;
        var hits = new List<(double In, double Out)>();
        if (length > 1e-9)
            // A project of many columns: only those near the axis are read (report #148: a beam read every column, so thousands of
            // beams built in a time growing with the square of the frame). Sorted by the place in their own attributes first, a
            // footprint reaching at most ColumnReach from its point. A few hundred are read whole, as before (NearReads.WholeUpTo).
            foreach (var column in NearReads.Many(m, IfcClass.Column) ? NearReads.Candidates(m, IfcClass.Column, AxisBox(c, ColumnReach)).Where(x => c.Distance(Place.Point(x)) <= ColumnReach) : m.OfClass(IfcClass.Column))
            {
                var (section, _, height) = Columns.Spec(m, column);
                if (!(height > 0)) continue;
                var z0 = Place.Elevation(m, column) + column.Num("baseOffset");
                if (z0 >= top - 1e-6 || z0 + height <= bottom + 1e-6) continue;
                var at = Place.Point(column);
                var outline = section.Outline();
                if (outline.Count < 3) continue;
                var reach = outline.Max(p => p.Length);
                if (c.Distance(at) > reach + 1e-6) continue;
                Inside(c, Place.Placed(outline, at, column.Num("rotation")), hits);
            }
        hits.Sort((x, y) => x.In.CompareTo(y.In));
        var merged = new List<(double In, double Out)>();
        foreach (var h in hits)
        {
            if (h.Out - h.In < 1e-9) continue;
            if (merged.Count > 0 && h.In <= merged[^1].Out + 1e-9) merged[^1] = (merged[^1].In, Math.Max(merged[^1].Out, h.Out));
            else merged.Add(h);
        }
        var (s0, s1) = (0.0, length);
        if (merged.Count > 0 && merged[0].In <= 1e-6) s0 = merged[0].Out;
        if (merged.Count > 0 && merged[^1].Out >= length - 1e-6) s1 = merged[^1].In;
        // A beam of a material that stays out of joins (forum topic 59, a steel girder) runs whole through the columns it passes,
        // as nothing cuts it; it still stops at the faces of those its ends stand in.
        if (!Materials.Joins(m, beam)) merged = [.. merged.Where(h => h.In <= 1e-6 || h.Out >= length - 1e-6)];
        return new BeamSpan(c, s0, Math.Max(s0, s1), merged);
    }

    /// <summary>How far from its point a column's footprint may reach for a beam to find it (m): a column 7 m across.</summary>
    public const double ColumnReach = 5;

    /// <summary>The box in plan round an axis, <paramref name="margin"/> wider all round (an arc's by its rise too), at height 0.</summary>
    static (Vec3 Min, Vec3 Max) AxisBox(PathCurve c, double margin)
    {
        var r = margin + Math.Abs(c.Rise);
        return (new Vec3(Math.Min(c.Start.X, c.End.X) - r, Math.Min(c.Start.Y, c.End.Y) - r, 0), new Vec3(Math.Max(c.Start.X, c.End.X) + r, Math.Max(c.Start.Y, c.End.Y) + r, 0));
    }

    /// <summary>The stations of the axis (between its ends) inside the footprint: exactly on a straight axis, by samples refined to a micron on an arc.</summary>
    static void Inside(PathCurve c, Vec2[] footprint, List<(double In, double Out)> into)
    {
        var length = c.Length;
        if (!c.IsArc)
        {
            var raw = new List<(double, double)>();
            Polygon2.InsideIntervals([footprint], c.Start, c.End, 0, 1, raw);
            foreach (var (t0, t1) in raw) into.Add((t0 * length, t1 * length));
            return;
        }
        var n = Math.Max(1, (int)Math.Ceiling(length / SampleStep));
        bool In(double s) => Polygon2.Contains(footprint, c.PointAt(s));
        // Where the axis crosses the footprint's boundary between two samples, to a micron.
        double Edge(double outside, double inside)
        {
            for (var i = 0; i < 40 && Math.Abs(inside - outside) > 1e-6; i++)
            {
                var mid = (outside + inside) / 2;
                if (In(mid)) inside = mid; else outside = mid;
            }
            return inside;
        }
        var was = In(0);
        var start = was ? 0.0 : double.NaN;
        for (var k = 1; k <= n; k++)
        {
            var s = length * k / n;
            var now = In(s);
            if (now == was) continue;
            if (now) start = Edge(length * (k - 1) / n, s);
            else { into.Add((start, Edge(s, length * (k - 1) / n))); start = double.NaN; }
            was = now;
        }
        if (was && !double.IsNaN(start)) into.Add((start, length));
    }

    public static Entity CreateType(IEdit edit, string? name, Section section, string material = "Reinforced concrete")
    {
        var type = new Entity(edit.NewId(), IfcClass.BeamType,
            string.IsNullOrWhiteSpace(name) ? $"Beam {section.Label}" : name.Trim(),
            new JsonObject { ["section"] = section.ToJson(), ["material"] = Materials.Ensure(edit, material).Id }, "BEAM");
        edit.Add(type);
        return type;
    }

    public static Entity DefaultType(IEdit edit) => CreateType(edit, null, Section.Rectangle(0.25, 0.4));

    public static (Section Section, string? Material) Spec(IModelView m, Entity beam)
    {
        var type = Place.TypeOf(m, beam.Id);
        return (Section.From(type?.Attrs["section"]) ?? Section.Rectangle(0.25, 0.4), type?.Str("material"));
    }

    public static double Top(IModelView m, Entity beam) => Place.Elevation(m, beam) + Place.StoreyHeight(m, beam) + beam.Num("topOffset");

    public static JoinWall? Frame(Entity beam, IModelView m)
    {
        var (s, e) = (Joins.Start(beam), Joins.End(beam));
        if ((e - s).Length < 1e-3) return null;
        var w = Spec(m, beam).Section.Width;
        return new JoinWall(s, e, -w / 2, w / 2);
    }

    /// <summary>The body a beam cuts with (a wall's head under it): between the column faces, like the body itself.</summary>
    public static Solid? Envelope(Entity beam, IModelView m) => Envelopes.Get(beam, m, v => SolveEnvelope(beam, v));

    static Solid? SolveEnvelope(Entity beam, IModelView m)
    {
        if (Frame(beam, m) is not { } f) return null;
        var span = SpanOf(m, beam);
        if (span.Empty) return null;
        var left = (f.End - f.Start).Normalized().Perp;
        var depth = Spec(m, beam).Section.Depth;
        var c = span.Curve;
        var (a, b) = (span.Start, span.End);
        IReadOnlyList<Vec2> fp = c.IsArc ? c.Band(_ => span.S0, _ => span.S1, f.YMin, f.YMax) : [a + left * f.YMin, b + left * f.YMin, b + left * f.YMax, a + left * f.YMax];
        return Prism.ExtrudeFootprint(fp, Top(m, beam) - depth, depth, "beam");
    }

    public static ElementGeometry? Geometry(Entity beam, IModelView m, IServices? services)
    {
        var problems = new List<string>();
        var (section, material) = Spec(m, beam);
        if (Frame(beam, m) is not { } f) return new ElementGeometry([], Diagnostics: ["The beam has no length."]);
        var span = SpanOf(m, beam);
        if (span.Empty) return new ElementGeometry([], Diagnostics: ["The beam lies entirely in columns."]);
        var ends = services?.Get<IPathElements>() is { } paths ? Joins.EndsOf(m, beam, paths, problems) : new (Vec2 Point, EndCut Cut)?[2];
        // An end standing in a column is not joined there: the body stops square at the column's face (decision 225).
        if (span.TrimmedStart) ends[0] = null;
        if (span.TrimmedEnd) ends[1] = null;
        var c = span.Curve;
        var trimmed = span.TrimmedStart || span.TrimmedEnd;
        if (trimmed && !c.IsArc) f = f with { Start = span.Start, End = span.End };
        var (from, to) = c.Ends(ends[0], ends[1], f.YMin, f.YMax);
        if (span.TrimmedStart) from = _ => span.S0;
        if (span.TrimmedEnd) to = _ => span.S1;
        IReadOnlyList<Vec2> fp = c.IsArc ? c.Band(from, to, f.YMin, f.YMax) : Strips.Footprint(f, ends, f.YMin, f.YMax);
        // At LOD 400 the real section (decision 415); every other reader the light one, with the same box.
        var fine = services?.Get<GeometryOptions>()?.Fine == true;
        var (ownOutline, ownHoles) = fine ? section.Real() : (section.Outline(), section.Holes());
        // Flipped, the section is mirrored across the axis (an angle or a channel faces the other way).
        var outline = ownOutline.Select(p => beam.Bool("flipped") ? p with { X = -p.X } : p).ToList();
        Solid? solid;
        if (section.IsProfile && c.IsArc)
        {
            // A steel profile swept along the arc, its web upright.
            var mid = Top(m, beam) - section.Depth / 2;
            var (s0, s1) = (from(0), to(0));
            var rings = c.Stations(s0, s1).Select(st =>
            {
                var (at, side) = (c.PointAt(st), c.TangentAt(st).Perp);
                return (IReadOnlyList<Vec3>)outline.Select(p => new Vec3(at.X + side.X * p.X, at.Y + side.Y * p.X, mid + p.Y)).ToList();
            }).ToList();
            solid = Lofts.Rings(rings, "beam");
        }
        else if (section.IsProfile)
        {
            // A steel profile runs along the axis, its web upright, its top at the beam's top.
            var u = (f.End - f.Start).Normalized();
            var side = new Vec3(-u.Y, u.X, 0);
            var mid = Top(m, beam) - section.Depth / 2;
            Vec3 At(Vec2 p) => new Vec3(f.Start.X, f.Start.Y, mid) + side * (beam.Bool("flipped") ? -p.X : p.X) + new Vec3(0, 0, p.Y);
            var ring = ownOutline.Select(At).ToList();
            // A hollow section's inside runs through it (decision 268).
            var holes = ownHoles.Select(h => (IReadOnlyList<Vec3>)h.Select(At).ToList()).ToList();
            solid = Prism.Extrude(ring, holes, new Vec3(u.X, u.Y, 0) * (f.End - f.Start).Length, "beam");
        }
        else solid = Prism.ExtrudeFootprint(fp, Top(m, beam) - section.Depth, section.Depth, "beam");
        if (solid == null) return new ElementGeometry([], Diagnostics: ["The beam has no section."]);
        var parts = CutPriorities.Apply(beam, m, services, [new GeometryPart(beam.Name, Materials.ColorOf(m, material), solid, material, "body")], problems);
        // The walls' and floors' layers reaching into it and set to cut (forum topic 89).
        parts = LayerExtentCuts.Apply(beam, m, services, parts, problems);
        // Snaps on the axis's ends (the column centres) and, trimmed, on the faces it stops at.
        IReadOnlyList<Vec2> snaps = trimmed ? [c.Start, c.End, span.Start, span.End] : [c.Start, c.End];
        return new ElementGeometry(parts, [new PlanItem("outline", true, fp)], snaps, problems.Count > 0 ? problems : null) { FineShape = section.HasRealShape };
    }

    /// <summary>The beam's quantities over its clear length: the span between the column faces less the columns it passes through.</summary>
    public static IEnumerable<QuantitySet> Quantities(Entity beam, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        var section = Spec(m, beam).Section;
        var length = SpanOf(m, beam).ClearLength;
        var net = g.Parts.Sum(p => Measure.Volume(p.Solid)) * section.RealRatio;
        yield return new QuantitySet("Qto_BeamBaseQuantities",
        [
            new("Length", length, "LENGTH"),
            new("CrossSectionArea", section.Area, "AREA", "Cross section"),
            new("GrossVolume", section.Area * length, "VOLUME", "Gross volume"),
            new("NetVolume", net, "VOLUME", "Net volume"),
        ]);
    }
}

/// <summary>
/// Straight beams and columns as IFC's swept solids with their parametric profile (decision 416): what the steel module's ends do not
/// already write (<see cref="IIfcSweptBodies"/>); the export keeps its B-rep where this is not the element's body (cut by the cut
/// order, mitred at a join).
/// </summary>
sealed class FrameIfcBodies : IIfcSweptBodies
{
    public IfcSweptBody? BodyOf(IModelView m, Entity e) => BodyOf(m, null, e);

    /// <summary>
    /// With the services, a column's attached ends: under planes, the sweep from its lowest corner to its highest clipped by
    /// each plane (IfcBooleanClippingResult of IfcHalfSpaceSolid); on the boolean path, or cut by the cut order besides, null (the
    /// B-rep it is).
    /// </summary>
    public IfcSweptBody? BodyOf(IModelView m, IServices? services, Entity e)
    {
        if (services != null && e.Class == IfcClass.Column && e.Attrs["at"] is not null && !StoredBodies.Has(e) && e.Attrs["steelEnds"] is not JsonObject && Columns.Layers(m, e).Count == 0
            && Columns.Built(m, e, services) is { Shaped: true } built)
            return Clipped(m, services, e, built);
        if (e.Class is not (IfcClass.Column or IfcClass.Beam) || StoredBodies.Has(e) || e.Attrs["steelEnds"] is JsonObject) return null;
        if (e.Class == IfcClass.Column)
        {
            // With finish layers (decision 719) its body is several solids, written as they are built.
            if (e.Attrs["at"] is null || Columns.Layers(m, e).Count > 0) return null;
            var (section, _, height) = Columns.Spec(m, e);
            if (!(height > 0)) return null;
            var at = Place.Point(e);
            var r = e.Num("rotation") * Math.PI / 180;
            return new IfcSweptBody(section.Sweep(m, at.At(Place.Elevation(m, e) + e.Num("baseOffset")), new Vec3(Math.Cos(r), Math.Sin(r), 0), Vec3.UnitZ, height, false), [], []);
        }
        if (e.Attrs["start"] is null || Beams.Frame(e, m) is null) return null;
        var span = Beams.SpanOf(m, e);
        if (span.Curve.IsArc || span.Empty) return null;
        var (beamSection, _) = Beams.Spec(m, e);
        var u = (span.End - span.Start).Normalized();
        var origin = span.Start.At(Beams.Top(m, e) - beamSection.Depth / 2);
        return new IfcSweptBody(beamSection.Sweep(m, origin, new Vec3(-u.Y, u.X, 0), new Vec3(u.X, u.Y, 0), (span.End - span.Start).Length, e.Bool("flipped")), [], []);
    }

    static IfcSweptBody? Clipped(IModelView m, IServices services, Entity e, BuiltColumn b)
    {
        if (b.Top.Boolean || b.Bottom.Boolean) return null;
        var (outer, holes) = ColumnEnds.Rings(e, b.Section);
        // Only when nothing else cut it: the body the views draw is the clipped sweep.
        var problems = new List<string>();
        if (ColumnEnds.Body(b, outer, holes, services, problems) is not { } own || Columns.Geometry(e, m, services) is not { Parts.Count: 1 } g) return null;
        if (Math.Abs(Measure.Volume(own) - Measure.Volume(g.Parts[0].Solid)) > Math.Max(1e-9, Measure.Volume(own) * 1e-6)) return null;
        // A plane cuts unless it is level (the sweep's own end then lies on it).
        static bool Level(ColumnPlane q) => Math.Abs(q.Bx) < 1e-12 && Math.Abs(q.By) < 1e-12;
        double Extreme(ColumnEnd x, bool top)
        {
            if (!x.Shaped) return x.Axis;
            var zs = outer.Select(p => x.Planes.Count == 1 ? x.Planes[0].Z(p) : x.Reach);
            return top ? zs.Max() : zs.Min();
        }
        var (z0, z1) = (Extreme(b.Bottom, false), Extreme(b.Top, true));
        if (!(z1 - z0 > 1e-6)) return null;
        var clips = new List<IfcClip>();
        var at = Place.Point(e);
        foreach (var (x, top) in new[] { (b.Top, true), (b.Bottom, false) })
            foreach (var q in x.Planes)
                if (x.Planes.Count > 1 || !Level(q)) clips.Add(new IfcClip(at.At(q.Z(at)), q.Normal(top)));
        var r = e.Num("rotation") * Math.PI / 180;
        return new IfcSweptBody(b.Section.Sweep(m, at.At(z0), new Vec3(Math.Cos(r), Math.Sin(r), 0), Vec3.UnitZ, z1 - z0, false), clips, []);
    }
}
