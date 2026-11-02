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

    public static Solid? Envelope(Entity column, IModelView m)
    {
        var (section, _, height) = Spec(m, column);
        var z = Place.Elevation(m, column) + column.Num("baseOffset");
        return Prism.ExtrudeFootprint(Place.Placed(section.Outline(), Place.Point(column), column.Num("rotation")), z, height, "column");
    }

    public static ElementGeometry? Geometry(Entity column, IModelView m, IServices? services)
    {
        var (section, material, _) = Spec(m, column);
        if (Envelope(column, m) is not { } solid) return new ElementGeometry([], Diagnostics: ["The column has no height."]);
        var problems = new List<string>();
        var parts = CutPriorities.Apply(column, m, services, [new GeometryPart(column.Name, Materials.ColorOf(m, material), solid, material, "body")], problems);
        var footprint = Place.Placed(section.Outline(), Place.Point(column), column.Num("rotation"));
        return new ElementGeometry(parts, [new PlanItem("cut", true, footprint)], [Place.Point(column)], problems.Count > 0 ? problems : null);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity column, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        var (section, _, height) = Spec(m, column);
        var net = g.Parts.Sum(p => Measure.Volume(p.Solid));
        yield return new QuantitySet("Qto_ColumnBaseQuantities",
        [
            new("Length", height, "LENGTH"),
            new("CrossSectionArea", section.Area, "AREA", "Cross section"),
            new("GrossVolume", section.Area * height, "VOLUME", "Gross volume"),
            new("NetVolume", net, "VOLUME", "Net volume"),
        ]);
    }
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
            foreach (var column in m.OfClass(IfcClass.Column))
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
        return new BeamSpan(c, s0, Math.Max(s0, s1), merged);
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
        // Flipped, the section is mirrored across the axis (an angle or a channel faces the other way).
        var outline = section.Outline().Select(p => beam.Bool("flipped") ? p with { X = -p.X } : p).ToList();
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
            var ring = outline.Select(p => new Vec3(f.Start.X, f.Start.Y, mid) + side * p.X + new Vec3(0, 0, p.Y)).ToList();
            solid = Prism.Extrude(ring, null, new Vec3(u.X, u.Y, 0) * (f.End - f.Start).Length, "beam");
        }
        else solid = Prism.ExtrudeFootprint(fp, Top(m, beam) - section.Depth, section.Depth, "beam");
        if (solid == null) return new ElementGeometry([], Diagnostics: ["The beam has no section."]);
        var parts = CutPriorities.Apply(beam, m, services, [new GeometryPart(beam.Name, Materials.ColorOf(m, material), solid, material, "body")], problems);
        // Snaps on the axis's ends (the column centres) and, trimmed, on the faces it stops at.
        IReadOnlyList<Vec2> snaps = trimmed ? [c.Start, c.End, span.Start, span.End] : [c.Start, c.End];
        return new ElementGeometry(parts, [new PlanItem("outline", true, fp)], snaps, problems.Count > 0 ? problems : null);
    }

    /// <summary>The beam's quantities over its clear length: the span between the column faces less the columns it passes through.</summary>
    public static IEnumerable<QuantitySet> Quantities(Entity beam, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        var section = Spec(m, beam).Section;
        var length = SpanOf(m, beam).ClearLength;
        var net = g.Parts.Sum(p => Measure.Volume(p.Solid));
        yield return new QuantitySet("Qto_BeamBaseQuantities",
        [
            new("Length", length, "LENGTH"),
            new("CrossSectionArea", section.Area, "AREA", "Cross section"),
            new("GrossVolume", section.Area * length, "VOLUME", "Gross volume"),
            new("NetVolume", net, "VOLUME", "Net volume"),
        ]);
    }
}
