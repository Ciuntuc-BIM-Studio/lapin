using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.SiteWorks;

/// <summary>
/// Formwork on the named faces of a host (docs/plan-site-works.md, S2–S3).
/// A floor's formwork covers its soffit — less the beams under it, the walls
/// and columns it rests on, its openings — with plywood on secondary beams on
/// primary beams on props, laid out by its type's spacings for the floor's
/// thickness, the props reaching down to the floor below (or the ground, or
/// the storey under it); and its free edges, the floor's thickness and an
/// upstand high. A beam's formwork is its bottom (less the columns and deeper
/// beams it meets, and the walls it sits on) on bearers and props, and its
/// sides, each as high as the beam below the floors cast with it, with
/// chamfer fillets along the bottom edges chosen. The host is read as it is
/// now: move the floor and its formwork follows. A Lapin class exported as
/// IfcElementAssembly USERDEFINED "FORMWORK".
/// </summary>
public static class Formwork
{
    public const string Class = "LapinFormwork";
    public const string TypeClass = "LapinFormworkType";
    public static readonly string[] Kinds = ["slab", "beam"];
    public static readonly string[] SlabSystems = ["beams", "panels"];
    public const string Color = "#d9b77a";
    const string TimberColor = "#c48a3a", PropColor = "#d0502a";

    public sealed record PropSize(string Name, double Min, double Max);
    public sealed record Row(double Thickness, double Secondary, double Primary, double Prop);
    public sealed record Yoke(double Depth, double Spacing);

    static JsonArray PropsJson() => new(
        new JsonObject { ["name"] = "Prop 1.5–2.5 m", ["min"] = 1.48, ["max"] = 2.50 },
        new JsonObject { ["name"] = "Prop 1.7–3.0 m", ["min"] = 1.72, ["max"] = 3.00 },
        new JsonObject { ["name"] = "Prop 2.0–3.5 m", ["min"] = 1.97, ["max"] = 3.50 },
        new JsonObject { ["name"] = "Prop 2.5–4.1 m", ["min"] = 2.47, ["max"] = 4.10 },
        new JsonObject { ["name"] = "Prop 3.0–5.5 m", ["min"] = 3.04, ["max"] = 5.50 });

    /// <summary>A generic system for the kind: timber beams and props under floors (spacings by the floor's thickness); a plywood box on bearers under beams.</summary>
    public static JsonObject DefaultSpec(string kind) => kind == "beam"
        ? new()
        {
            ["plywood"] = 0.021, ["bearerHeight"] = 0.20, ["bearerWidth"] = 0.08, ["bearers"] = 2, ["propSpacing"] = 1.2,
            ["yokes"] = new JsonArray(Y(0.5, 0.6), Y(0.8, 0.5), Y(1.2, 0.4), Y(2.0, 0.3)), ["chamfer"] = 0.02, ["props"] = PropsJson(), ["reuses"] = 4,
        }
        : new()
        {
            ["system"] = "beams", ["plywood"] = 0.021, ["secondaryHeight"] = 0.20, ["primaryHeight"] = 0.20, ["beamWidth"] = 0.08,
            ["lengths"] = new JsonArray(2.45, 2.65, 2.90, 3.30, 3.60, 3.90, 4.50, 4.90, 5.90),
            ["spacing"] = new JsonArray(R(0.20, 0.50, 2.00, 1.50), R(0.25, 0.50, 1.50, 1.25), R(0.30, 0.40, 1.50, 1.00), R(0.40, 0.40, 1.25, 0.90), R(0.50, 0.33, 1.00, 0.80)),
            ["edgeInset"] = 0.15, ["upstand"] = 0.05, ["props"] = PropsJson(),
            ["panelLength"] = 1.50, ["panelWidth"] = 0.75, ["panelThickness"] = 0.12, ["reuses"] = 4,
        };

    static JsonObject Y(double depth, double spacing) => new() { ["depth"] = depth, ["spacing"] = spacing };
    static JsonObject R(double t, double s, double p, double prop) => new() { ["thickness"] = t, ["secondary"] = s, ["primary"] = p, ["prop"] = prop };

    public sealed record SlabSpec(string System, double Plywood, double SecondaryHeight, double PrimaryHeight, double BeamWidth, double[] Lengths, IReadOnlyList<Row> Rows,
        double EdgeInset, double Upstand, IReadOnlyList<PropSize> Props, double PanelLength, double PanelWidth, double PanelThickness, int Reuses);

    public sealed record BeamSpec(double Plywood, double BearerHeight, double BearerWidth, int Bearers, double PropSpacing, IReadOnlyList<Yoke> Yokes, double Chamfer, IReadOnlyList<PropSize> Props, int Reuses);

    static List<PropSize> Props(JsonObject s) =>
        (s["props"] as JsonArray)?.OfType<JsonObject>().Select(p => new PropSize(Lay.Str(p, "name", "Prop"), Lay.Num(p, "min", 0), Lay.Num(p, "max", 0)))
            .Where(p => p.Max > p.Min && p.Min > 0).OrderBy(p => p.Max).ToList() ?? [];

    public static SlabSpec ReadSlab(JsonObject? o)
    {
        var s = Lay.Merge(DefaultSpec("slab"), o);
        var rows = (s["spacing"] as JsonArray)?.OfType<JsonObject>()
            .Select(r => new Row(Lay.Num(r, "thickness", 0.2), Math.Max(0.1, Lay.Num(r, "secondary", 0.5)), Math.Max(0.3, Lay.Num(r, "primary", 1.5)), Math.Max(0.3, Lay.Num(r, "prop", 1.2))))
            .OrderBy(r => r.Thickness).ToList() ?? [];
        if (rows.Count == 0) rows.Add(new(0.2, 0.5, 1.5, 1.2));
        var system = Lay.Str(s, "system", "beams");
        return new(SlabSystems.Contains(system) ? system : "beams", Math.Max(0.005, Lay.Num(s, "plywood", 0.021)), Math.Max(0, Lay.Num(s, "secondaryHeight", 0.2)), Math.Max(0, Lay.Num(s, "primaryHeight", 0.2)),
            Math.Max(0.02, Lay.Num(s, "beamWidth", 0.08)), Lay.Nums(s, "lengths", [2.45, 2.90, 3.30, 3.90, 4.90]), rows, Math.Max(0, Lay.Num(s, "edgeInset", 0.15)), Math.Max(0, Lay.Num(s, "upstand", 0.05)), Props(s),
            Math.Max(0.3, Lay.Num(s, "panelLength", 1.5)), Math.Max(0.3, Lay.Num(s, "panelWidth", 0.75)), Math.Max(0.02, Lay.Num(s, "panelThickness", 0.12)), Math.Max(1, (int)Lay.Num(s, "reuses", 4)));
    }

    public static BeamSpec ReadBeam(JsonObject? o)
    {
        var s = Lay.Merge(DefaultSpec("beam"), o);
        var yokes = (s["yokes"] as JsonArray)?.OfType<JsonObject>().Select(y => new Yoke(Lay.Num(y, "depth", 0.5), Math.Max(0.1, Lay.Num(y, "spacing", 0.5)))).OrderBy(y => y.Depth).ToList() ?? [];
        if (yokes.Count == 0) yokes.Add(new(1, 0.5));
        return new(Math.Max(0.005, Lay.Num(s, "plywood", 0.021)), Math.Max(0, Lay.Num(s, "bearerHeight", 0.2)), Math.Max(0.02, Lay.Num(s, "bearerWidth", 0.08)), Math.Max(1, (int)Lay.Num(s, "bearers", 2)),
            Math.Max(0.3, Lay.Num(s, "propSpacing", 1.2)), yokes, Math.Max(0, Lay.Num(s, "chamfer", 0.02)), Props(s), Math.Max(1, (int)Lay.Num(s, "reuses", 4)));
    }

    public static Entity? TypeOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType);

    public static string KindOf(Entity e) => e.Str("kind") == "beam" ? "beam" : "slab";

    public static Entity? HostOf(IModelView m, Entity e) => e.Str("host") is { } id ? m.Get(id) : null;

    /// <summary>The faces a formwork forms: its own list, else all (a floor's soffit and free edges; a beam's bottom, sides and bottom edges).</summary>
    public static IReadOnlyList<string> FacesOf(Entity e) =>
        (e.Attrs["faces"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() is { Count: > 0 } l ? l
            : KindOf(e) == "beam" ? ["Bottom", "Side.Left", "Side.Right", "Edge.BottomLeft", "Edge.BottomRight"] : ["Soffit", "Edges"];

    /// <summary>The shortest prop whose range takes a length; null when none does.</summary>
    public static PropSize? PropFor(IReadOnlyList<PropSize> sizes, double length) => sizes.FirstOrDefault(p => length >= p.Min - 1e-9 && length <= p.Max + 1e-9);

    public sealed record Prop(Vec2 At, double Bottom, double Top, PropSize? Size)
    {
        public double Length => Top - Bottom;
    }

    public sealed record Member(Vec3 A, Vec3 B, double Width, double Height)
    {
        public double Length => (B - A).Length;
    }

    /// <summary>What stands in the way: an element's footprint and height span, from its faces.</summary>
    public sealed record Body(Entity Element, Vec2[] Footprint, double Bottom, double Top);

    static List<Body> Bodies(IModelView m, IServices services, string except, params string[] classes) =>
        classes.SelectMany(m.OfClass).Where(e => e.Id != except)
            .Select(e => Lay.Extent(ElementFaces.Of(services, e, m)) is { } x ? new Body(e, x.Footprint, x.Bottom, x.Top) : null).OfType<Body>().ToList();

    // ─────────────────────────────────────────────────────────── floors

    public sealed record Edge(string Role, Vec2 A, Vec2 B, Vec2 Out, double Height);

    public sealed record SlabLayout(SlabSpec Spec, Row Row, double Soffit, double Top, double Direction, Region Region, bool FormSoffit, double SoffitArea, IReadOnlyList<Edge> Edges,
        IReadOnlyList<Member> Primary, IReadOnlyList<Member> Secondary, IReadOnlyList<Prop> Props, IReadOnlyList<double> PrimaryPieces, IReadOnlyList<double> SecondaryPieces, IReadOnlyList<string> Problems)
    {
        public double EdgeArea => Edges.Sum(e => (e.B - e.A).Length * (Top - Soffit));
        public double EdgeLength => Edges.Sum(e => (e.B - e.A).Length);
    }

    public static SlabLayout? SlabLayoutOf(Entity e, IModelView m, IServices services)
    {
        if (HostOf(m, e) is not { Class: IfcClass.Slab } slab) return null;
        var faces = ElementFaces.Of(services, slab, m);
        if (faces.FirstOrDefault(f => f.Role == "Soffit") is not { } soffit || faces.FirstOrDefault(f => f.Role == "Top") is not { } topFace) return null;
        var spec = ReadSlab(TypeOf(m, e)?.Attrs["spec"] as JsonObject);
        var problems = new List<string>();
        var (zs, zt) = (soffit.Outline[0].Z, topFace.Outline[0].Z);
        var thickness = zt - zs;
        var row = spec.Rows.FirstOrDefault(r => thickness <= r.Thickness + 1e-9);
        if (row == null) { row = spec.Rows[^1]; problems.Add($"The floor is {Lay.M(thickness)} m thick, thicker than the type's spacing table goes ({Lay.M(row.Thickness)} m): its last row is used."); }
        var outline = soffit.Outline.Select(p => p.Xy).ToArray();
        var rings = new List<Vec2[]> { outline };
        rings.AddRange((soffit.Holes ?? []).Select(h => h.Select(p => p.Xy).ToArray()));
        // What the floor rests on or is cast with: beams, walls and columns reaching its soffit.
        var obstacles = Bodies(m, services, slab.Id, IfcClass.Beam, IfcClass.Wall, IfcClass.Column)
            .Where(b => b.Top >= zs - 0.01 && b.Bottom < zs - 0.01).Select(b => b.Footprint).ToList();
        var region = new Region(rings, obstacles);
        var want = FacesOf(e);
        var formSoffit = want.Contains("Soffit");
        // The direction the primary beams run: its own, else along the outline's longest side.
        var longest = Enumerable.Range(0, outline.Length).Select(i => outline[(i + 1) % outline.Length] - outline[i]).MaxBy(v => v.Length);
        var angle = e.NumOrNull("direction") ?? Math.Round(Math.Atan2(longest.Y, longest.X) * 180 / Math.PI, 6);
        var u = new Vec2(Math.Cos(angle * Math.PI / 180), Math.Sin(angle * Math.PI / 180));
        var v = u.Perp;
        var us = outline.Select(p => Vec2.Dot(p, u)).ToList();
        var vs = outline.Select(p => Vec2.Dot(p, v)).ToList();
        var (u0, u1, v0, v1) = (us.Min(), us.Max(), vs.Min(), vs.Max());
        Vec2 P(double a, double b) => u * a + v * b;
        var primary = new List<Member>();
        var secondary = new List<Member>();
        var props = new List<Prop>();
        var support = new Support(m, services, slab.Id);
        var panels = spec.System == "panels";
        var under = panels ? zs - spec.PanelThickness : zs - spec.Plywood - spec.SecondaryHeight - spec.PrimaryHeight;
        Prop PropAt(Vec2 p)
        {
            var bottom = support.At(p, under);
            return new(p, bottom, under, PropFor(spec.Props, under - bottom));
        }
        if (formSoffit && panels)
        {
            foreach (var a in Lay.Spread(u0, u1, spec.PanelLength, Math.Min(spec.EdgeInset, spec.PanelLength / 2)))
                foreach (var b in Lay.Spread(v0, v1, spec.PanelWidth, Math.Min(spec.EdgeInset, spec.PanelWidth / 2)))
                    if (region.Contains(P(a, b))) props.Add(PropAt(P(a, b)));
        }
        else if (formSoffit)
        {
            var zSec = zs - spec.Plywood - spec.SecondaryHeight / 2;
            var zPri = zs - spec.Plywood - spec.SecondaryHeight - spec.PrimaryHeight / 2;
            foreach (var b in Lay.Spread(v0, v1, row.Primary, spec.EdgeInset))
            {
                var (a0, a1) = (P(u0 - 1, b), P(u1 + 1, b));
                foreach (var (t0, t1) in region.Intervals(a0, a1))
                {
                    var (pa, pb) = (a0 + (a1 - a0) * t0, a0 + (a1 - a0) * t1);
                    var len = (pb - pa).Length;
                    if (len < 0.1) continue;
                    primary.Add(new(pa.At(zPri), pb.At(zPri), spec.BeamWidth, spec.PrimaryHeight));
                    foreach (var x in Lay.Spread(0, len, row.Prop, Math.Min(0.3, len / 2))) props.Add(PropAt(pa + (pb - pa) * (x / len)));
                }
            }
            foreach (var a in Lay.Spread(u0, u1, row.Secondary, Math.Min(spec.EdgeInset, row.Secondary / 2)))
            {
                var (b0, b1) = (P(a, v0 - 1), P(a, v1 + 1));
                foreach (var (t0, t1) in region.Intervals(b0, b1))
                {
                    var (pa, pb) = (b0 + (b1 - b0) * t0, b0 + (b1 - b0) * t1);
                    if ((pb - pa).Length >= 0.1) secondary.Add(new(pa.At(zSec), pb.At(zSec), spec.BeamWidth, spec.SecondaryHeight));
                }
            }
        }
        // The free edges: not where the floor runs on into another at its level.
        var edges = new List<Edge>();
        var others = m.OfClass(IfcClass.Slab).Where(s => s.Id != slab.Id).Select(s => ElementFaces.Find(services, s, m, "Top")).OfType<ElementFace>()
            .Where(f => Math.Abs(f.Outline[0].Z - zt) < 0.05).Select(f => f.Outline.Select(p => p.Xy).ToList()).ToList();
        foreach (var f in faces.Where(f => f.Role.StartsWith("Edge[", StringComparison.Ordinal)))
        {
            if (!want.Contains(f.Role) && !want.Contains("Edges")) continue;
            var bottom = f.Outline.OrderBy(p => p.Z).Take(2).Select(p => p.Xy).ToList();
            var n = new Vec2(f.Normal.X, f.Normal.Y).Normalized();
            var mid = (bottom[0] + bottom[1]) * 0.5 + n * 0.05;
            if (!want.Contains(f.Role) && others.Any(o => Polygon2.Contains(o, mid))) continue;
            edges.Add(new(f.Role, bottom[0], bottom[1], n, thickness + spec.Upstand));
        }
        foreach (var p in props.Where(p => p.Size == null).Take(1))
            problems.Add($"{props.Count(x => x.Size == null)} prop(s) need a length no prop of the type takes ({Lay.M(p.Length)} m here): a tower or a longer prop is needed.");
        return new(spec, row, zs, zt, angle, region, formSoffit, formSoffit ? region.Area() : 0, edges, primary, secondary, props,
            primary.SelectMany(b => Lay.Cover(b.Length, spec.Lengths)).ToList(), secondary.SelectMany(b => Lay.Cover(b.Length, spec.Lengths)).ToList(), problems);
    }

    // ─────────────────────────────────────────────────────────── beams

    /// <summary>A stretch of one side of a beam that is formed: along the side from A to B (straight), facing <see cref="Out"/>, this high from the bottom.</summary>
    public sealed record SidePiece(string Side, Vec2 A, Vec2 B, Vec2 Out, double Height)
    {
        public double Length => (B - A).Length;
    }

    /// <summary>
    /// A beam's formwork laid along its axis (a polyline: two points for a
    /// straight beam, the arc's for a curved one), stations in metres from its
    /// start: the stretches its bottom is formed over (<see cref="Free"/>), its
    /// sides' pieces, its chamfer fillets' length, its props and yokes.
    /// </summary>
    public sealed record BeamLayout(BeamSpec Spec, Axis Axis, double Width, double Bottom, double Top, IReadOnlyList<(double A, double B)> Free,
        bool FormBottom, IReadOnlyList<SidePiece> Sides, double ChamferLength, IReadOnlyList<Prop> Props, int Yokes, IReadOnlyList<string> Problems)
    {
        public double Length => Axis.Length;
        public double FreeLength => Free.Sum(f => f.B - f.A);
        public double BottomArea => FormBottom ? FreeLength * Width : 0;
        public double SideArea => Sides.Sum(s => s.Length * s.Height);
    }

    /// <summary>A polyline with stations: where a station lies, which way it runs there, and lines offset to either side (mitred at the vertices).</summary>
    public sealed class Axis
    {
        public IReadOnlyList<Vec2> Points { get; }
        readonly double[] at;
        public Axis(IReadOnlyList<Vec2> points)
        {
            Points = points;
            at = new double[points.Count];
            for (var i = 1; i < points.Count; i++) at[i] = at[i - 1] + (points[i] - points[i - 1]).Length;
        }
        public double Length => at[^1];
        public int Segments => Points.Count - 1;
        public double StationOf(int vertex) => at[vertex];
        public Vec2 Dir(int seg) => (Points[seg + 1] - Points[seg]).Normalized();
        int SegAt(double s) { for (var k = 0; k < Segments - 1; k++) if (s < at[k + 1]) return k; return Segments - 1; }
        public Vec2 PointAt(double s) { var k = SegAt(s); return Points[k] + Dir(k) * (s - at[k]); }
        /// <summary>The vertex's offset direction to the left, mitred (its length 1/cos of half the turn).</summary>
        public Vec2 Miter(int vertex)
        {
            if (vertex == 0) return Dir(0).Perp;
            if (vertex == Segments) return Dir(Segments - 1).Perp;
            var (n0, n1) = (Dir(vertex - 1).Perp, Dir(vertex).Perp);
            return (n0 + n1) * (1 / (1 + Vec2.Dot(n0, n1)));
        }
        public List<Vec2> Offset(double y) => Enumerable.Range(0, Points.Count).Select(i => Points[i] + Miter(i) * y).ToList();
        /// <summary>The band between two offsets from station s0 to s1, counter-clockwise.</summary>
        public List<Vec2> Band(double s0, double s1, double y0, double y1)
        {
            var stations = new List<double> { s0 };
            for (var i = 1; i < Points.Count - 1; i++) if (at[i] > s0 + 1e-9 && at[i] < s1 - 1e-9) stations.Add(at[i]);
            stations.Add(s1);
            Vec2 Off(double s, double y)
            {
                for (var i = 1; i < Points.Count - 1; i++) if (Math.Abs(at[i] - s) < 1e-9) return Points[i] + Miter(i) * y;
                return PointAt(s) + Dir(SegAt(s)).Perp * y;
            }
            var right = stations.Select(st => Off(st, y0)).ToList();
            var left = stations.Select(st => Off(st, y1)).Reverse();
            return Polygon2.EnsureCcw([.. right, .. left]).ToList();
        }
    }

    public static BeamLayout? BeamLayoutOf(Entity e, IModelView m, IServices services)
    {
        if (HostOf(m, e) is not { Class: IfcClass.Beam } beam) return null;
        var f = ElementFaces.Of(services, beam, m).ToDictionary(x => x.Role);
        if (!f.TryGetValue("Axis", out var axisFace) || !f.TryGetValue("End.Start", out var es) || !f.TryGetValue("Bottom", out var bottomFace) || !f.TryGetValue("Top", out var topFace)) return null;
        var spec = ReadBeam(TypeOf(m, e)?.Attrs["spec"] as JsonObject);
        var problems = new List<string>();
        var (zb, zt) = (bottomFace.Outline.Min(p => p.Z), topFace.Outline.Max(p => p.Z));
        var axis = new Axis(axisFace.Outline.Select(p => p.Xy).ToList());
        var L = axis.Length;
        // Its width: across its start face, corner to corner in plan.
        var ends = es.Outline.Select(p => p.Xy).ToList();
        var width = ends.SelectMany(a => ends.Select(b => (b - a).Length)).Max();
        var want = FacesOf(e);
        // What the bottom stops at: columns, beams as deep or deeper, walls it sits on.
        var bodies = Bodies(m, services, beam.Id, IfcClass.Column, IfcClass.Beam, IfcClass.Wall);
        var through = bodies.Where(b => b.Element.Class == IfcClass.Column && b.Top > zb + 0.01 && b.Bottom < zt
            || b.Element.Class == IfcClass.Beam && b.Bottom <= zb + 0.01 && b.Top > zb + 0.01
            || b.Element.Class == IfcClass.Wall && b.Top >= zb - 0.01 && b.Top <= zb + 0.01).ToList();
        var blocked = new List<(double, double)>();
        for (var k = 0; k < axis.Segments; k++)
        {
            var (s0, s1) = (axis.StationOf(k), axis.StationOf(k + 1));
            var hits = new List<(double, double)>();
            foreach (var b in through) Polygon2.InsideIntervals([b.Footprint], axis.Points[k], axis.Points[k + 1], 0, 1, hits);
            foreach (var (h0, h1) in hits) blocked.Add(((s0 + (s1 - s0) * h0) / L, (s0 + (s1 - s0) * h1) / L));
        }
        var free = Polygon2.Outside(blocked).Select(x => (A: x.A * L, B: x.B * L)).Where(x => x.B - x.A > 0.01).ToList();
        double FreeIn(double s0, double s1) => free.Sum(x => Math.Max(0, Math.Min(x.B, s1) - Math.Max(x.A, s0)));
        // The sides: each stretch as high as the beam below whatever is cast against it there (a floor, a shallower beam framing in).
        var slabs = m.OfClass(IfcClass.Slab).Select(sl => (Soffit: ElementFaces.Find(services, sl, m, "Soffit"), Top: ElementFaces.Find(services, sl, m, "Top")))
            .Where(x => x.Soffit != null && x.Top != null).Select(x => (Ring: x.Soffit!.Outline.Select(p => p.Xy).ToArray(), Soffit: x.Soffit.Outline[0].Z, Top: x.Top!.Outline[0].Z))
            .Where(x => x.Soffit > zb + 0.01 && x.Soffit <= zt + 0.01 && x.Top > zb).ToList();
        var shallow = bodies.Where(b => b.Element.Class == IfcClass.Beam && b.Bottom > zb + 0.01 && b.Bottom < zt && b.Top > zb).ToList();
        var sides = new List<SidePiece>();
        var chamfer = 0.0;
        var chamfers = spec.Chamfer > 0 ? want.Where(r => r is "Edge.BottomLeft" or "Edge.BottomRight").ToList() : [];
        var endFace = f.TryGetValue("End.End", out var ee) ? ee.Outline.Select(p => p.Xy).ToList() : null;
        foreach (var (side, sign) in new[] { ("Side.Left", 1.0), ("Side.Right", -1.0) })
        {
            // Mitred offsets along the axis, their ends where the beam's end faces are (square to the arc, not to its first and last chords).
            var line = axis.Offset(sign * width / 2);
            line[0] = ends.MinBy(p => (p - line[0]).Length);
            if (endFace != null) line[^1] = endFace.MinBy(p => (p - line[^1]).Length);
            var edge = sign > 0 ? "Edge.BottomLeft" : "Edge.BottomRight";
            for (var k = 0; k < axis.Segments; k++)
            {
                var (sa, sb) = (line[k], line[k + 1]);
                var (s0, s1) = (axis.StationOf(k), axis.StationOf(k + 1));
                var ratio = (sb - sa).Length / Math.Max(s1 - s0, 1e-12);
                if (chamfers.Contains(edge)) chamfer += FreeIn(s0, s1) * ratio;
                if (!want.Contains(side)) continue;
                var outward = axis.Dir(k).Perp * sign;
                var probe = outward * 0.02;
                var cuts = new List<double> { 0, 1 };
                foreach (var x in free) foreach (var st in new[] { x.A, x.B }) if (st > s0 && st < s1) cuts.Add((st - s0) / (s1 - s0));
                var hits = new List<(double, double)>();
                foreach (var sl in slabs) Polygon2.InsideIntervals([sl.Ring], sa + probe, sb + probe, 0, 1, hits);
                foreach (var o in shallow) Polygon2.InsideIntervals([o.Footprint], sa + probe, sb + probe, 0, 1, hits);
                foreach (var (h0, h1) in hits) { cuts.Add(h0); cuts.Add(h1); }
                cuts = cuts.Distinct().Order().ToList();
                for (var i = 0; i + 1 < cuts.Count; i++)
                {
                    var (t0, t1) = (cuts[i], cuts[i + 1]);
                    if (t1 - t0 < 1e-6) continue;
                    var mid = s0 + (s1 - s0) * (t0 + t1) / 2;
                    if (!free.Any(x => mid > x.A && mid < x.B)) continue;
                    var at = sa + (sb - sa) * ((t0 + t1) / 2) + probe;
                    var top = zt;
                    foreach (var sl in slabs) if (Polygon2.Contains(sl.Ring, at)) top = Math.Min(top, sl.Soffit);
                    foreach (var o in shallow) if (Polygon2.Contains(o.Footprint, at)) top = Math.Min(top, o.Bottom);
                    var h = top - zb;
                    if (h <= 1e-6) continue;
                    var (pa, pb) = (sa + (sb - sa) * t0, sa + (sb - sa) * t1);
                    if (sides.Count > 0 && sides[^1].Side == side && (sides[^1].B - pa).Length < 1e-6 && Math.Abs(sides[^1].Height - h) < 1e-6 && Vec2.Cross(sides[^1].B - sides[^1].A, pb - pa) is var cr && Math.Abs(cr) < 1e-9)
                        sides[^1] = sides[^1] with { B = pb };
                    else sides.Add(new(side, pa, pb, outward, h));
                }
            }
        }
        var formBottom = want.Contains("Bottom");
        var support = new Support(m, services);
        var under = zb - spec.Plywood - spec.BearerHeight;
        var props = new List<Prop>();
        var yokes = 0;
        var yoke = spec.Yokes.FirstOrDefault(y => zt - zb <= y.Depth + 1e-9) ?? spec.Yokes[^1];
        foreach (var (a, b) in free)
        {
            if (formBottom)
                foreach (var x in Lay.Spread(a, b, spec.PropSpacing, Math.Min(0.3, (b - a) / 2)))
                {
                    var p = axis.PointAt(x);
                    var z = support.At(p, under);
                    props.Add(new(p, z, under, PropFor(spec.Props, under - z)));
                }
            if (sides.Count > 0) yokes += Lay.Spread(a, b, yoke.Spacing, Math.Min(0.15, (b - a) / 2)).Count;
        }
        foreach (var p in props.Where(p => p.Size == null).Take(1))
            problems.Add($"{props.Count(x => x.Size == null)} prop(s) need a length no prop of the type takes ({Lay.M(p.Length)} m here).");
        return new(spec, axis, width, zb, zt, free, formBottom, sides, chamfer, props, yokes, problems);
    }

    // ─────────────────────────────────────────────────────────── geometry and quantities

    static readonly ConditionalWeakTable<ElementGeometry, object> Built = new();

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices services)
    {
        if (HostOf(m, e) == null) return new ElementGeometry([], Diagnostics: ["The element this formwork was on is gone."]);
        ElementGeometry g;
        object layout;
        if (KindOf(e) == "beam")
        {
            if (BeamLayoutOf(e, m, services) is not { } b) return new ElementGeometry([], Diagnostics: ["The beam has no faces to form."]);
            (g, layout) = (BeamGeometry(b), b);
        }
        else
        {
            if (SlabLayoutOf(e, m, services) is not { } sl) return new ElementGeometry([], Diagnostics: ["The floor has no soffit to form."]);
            (g, layout) = (SlabGeometry(sl, services), sl);
        }
        Built.AddOrUpdate(g, layout);
        return g;
    }

    static ElementGeometry SlabGeometry(SlabLayout l, IServices services)
    {
        var s = l.Spec;
        var parts = new Parts();
        var plan = new List<PlanItem>();
        if (l.FormSoffit)
        {
            var thick = s.System == "panels" ? s.PanelThickness : s.Plywood;
            var outer = l.Region.Rings[0];
            var holes = l.Region.Rings.Skip(1).Select(h => (IReadOnlyList<Vec3>)h.Select(p => p.At(l.Soffit - thick)).ToList()).ToList();
            if (Prism.Extrude(Polygon2.EnsureCcw(outer).Select(p => p.At(l.Soffit - thick)).ToList(), holes, new Vec3(0, 0, thick), "plywood") is { } sheet)
            {
                var cut = l.Region.Obstacles.Count > 0 && services.Get<ISolidBooleans>() is { } booleans
                    ? booleans.Difference(sheet, l.Region.Obstacles.Select(o => Prism.ExtrudeFootprint(o, l.Soffit - thick - 0.05, thick + 0.1)).OfType<Solid>().ToList(), "plywood") : sheet;
                parts.Add("plywood", s.System == "panels" ? "Panels" : "Plywood", Color, cut);
            }
            plan.Add(new("outline", true, outer));
        }
        foreach (var b in l.Secondary) parts.Add("secondary", "Secondary beams", TimberColor, Prism.SweepBox(b.A, b.B, b.Width, b.Height));
        foreach (var b in l.Primary) parts.Add("primary", "Primary beams", TimberColor, Prism.SweepBox(b.A, b.B, b.Width, b.Height));
        foreach (var p in l.Props) PropSolid(parts, p);
        foreach (var edge in l.Edges)
        {
            var o = edge.Out * s.Plywood;
            parts.Add("edges", "Edge formwork", Color, Prism.ExtrudeFootprint([edge.A, edge.B, edge.B + o, edge.A + o], l.Soffit - s.Plywood, edge.Height + s.Plywood));
            plan.Add(new("outline", false, [edge.A + o, edge.B + o]));
        }
        return new ElementGeometry(parts.Build(), plan, null, l.Problems.Count > 0 ? l.Problems : null);
    }

    static void PropSolid(Parts parts, Prop p)
    {
        if (p.Length <= 0.01) return;
        parts.Add("props", "Props", PropColor, Prism.Cylinder(p.At, p.Bottom, 0.03, p.Length, 8));
        parts.Add("props", "Props", PropColor, Prism.ExtrudeFootprint(Sq(p.At, 0.15), p.Bottom, 0.01));
        parts.Add("props", "Props", PropColor, Prism.ExtrudeFootprint(Sq(p.At, 0.12), p.Top - 0.01, 0.01));
    }

    static Vec2[] Sq(Vec2 c, double size) { var h = size / 2; return [new(c.X - h, c.Y - h), new(c.X + h, c.Y - h), new(c.X + h, c.Y + h), new(c.X - h, c.Y + h)]; }

    static ElementGeometry BeamGeometry(BeamLayout l)
    {
        var s = l.Spec;
        var parts = new Parts();
        var plan = new List<PlanItem>();
        var half = l.Width / 2 + s.Plywood;
        var ax = l.Axis;
        foreach (var (a, b) in l.Free)
        {
            if (!l.FormBottom) continue;
            var band = ax.Band(a, b, -half, half);
            parts.Add("bottom", "Bottom", Color, Prism.ExtrudeFootprint(band, l.Bottom - s.Plywood, s.Plywood));
            plan.Add(new("outline", true, band));
            var n = s.Bearers;
            var z = l.Bottom - s.Plywood - s.BearerHeight / 2;
            for (var k = 0; k < ax.Segments; k++)
            {
                var (s0, s1) = (Math.Max(a, ax.StationOf(k)), Math.Min(b, ax.StationOf(k + 1)));
                if (s1 - s0 < 1e-3) continue;
                var across = ax.Dir(k).Perp;
                for (var i = 0; i < n; i++)
                {
                    var y = n == 1 ? 0 : -l.Width / 2 + s.BearerWidth / 2 + i * (l.Width - s.BearerWidth) / (n - 1);
                    parts.Add("bearers", "Bearers", TimberColor, Prism.SweepBox((ax.PointAt(s0) + across * y).At(z), (ax.PointAt(s1) + across * y).At(z), s.BearerWidth, s.BearerHeight));
                }
            }
        }
        foreach (var p in l.Props) PropSolid(parts, p);
        foreach (var side in l.Sides)
        {
            var o = side.Out * s.Plywood;
            parts.Add("sides", "Sides", Color, Prism.ExtrudeFootprint(Polygon2.EnsureCcw([side.A, side.B, side.B + o, side.A + o]).ToArray(), l.Bottom - s.Plywood, side.Height + s.Plywood));
        }
        if (l.ChamferLength > 0)
            foreach (var (edge, sign) in new[] { ("Edge.BottomLeft", 1.0), ("Edge.BottomRight", -1.0) })
            {
                var line = ax.Offset(sign * l.Width / 2);
                var c = s.Chamfer;
                for (var k = 0; k < ax.Segments; k++)
                    foreach (var (a, b) in l.Free)
                    {
                        var (s0, s1) = (Math.Max(a, ax.StationOf(k)), Math.Min(b, ax.StationOf(k + 1)));
                        if (s1 - s0 < 1e-3) continue;
                        var seg = ax.StationOf(k + 1) - ax.StationOf(k);
                        var (pa, pb) = (line[k] + (line[k + 1] - line[k]) * ((s0 - ax.StationOf(k)) / seg), line[k] + (line[k + 1] - line[k]) * ((s1 - ax.StationOf(k)) / seg));
                        // A fillet: a triangle in the corner, swept along.
                        var inward = -ax.Dir(k).Perp * sign;
                        var d = pb - pa;
                        var profile = new List<Vec3> { pa.At(l.Bottom), (pa + inward * c).At(l.Bottom), pa.At(l.Bottom + c) };
                        if (Vec3.Dot(VecMath.NewellNormal(profile), new Vec3(d.X, d.Y, 0)) < 0) profile.Reverse();
                        parts.Add("chamfers", "Chamfer fillets", TimberColor, Prism.Extrude(profile, null, new Vec3(d.X, d.Y, 0)));
                    }
            }
        return new ElementGeometry(parts.Build(), plan, [ax.Points[0], ax.Points[^1]], l.Problems.Count > 0 ? l.Problems : null);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null || !Built.TryGetValue(g, out var layout)) yield break;
        var q = new List<Quantity>();
        IReadOnlyList<Prop> props;
        int reuses;
        if (layout is SlabLayout l)
        {
            q.AddRange([
                new("ContactArea", l.SoffitArea + l.EdgeArea, "AREA", "Contact area"), new("SoffitArea", l.SoffitArea, "AREA", "Soffit"),
                new("EdgeArea", l.EdgeArea, "AREA", "Edges"), new("EdgeLength", l.EdgeLength, "LENGTH", "Edges (length)"),
            ]);
            if (l.Spec.System == "beams")
                q.AddRange([
                    new("PrimaryLength", l.Primary.Sum(b => b.Length), "LENGTH", "Primary beams (length)"), new("PrimaryPieces", l.PrimaryPieces.Count, "COUNT", "Primary beams"),
                    new("SecondaryLength", l.Secondary.Sum(b => b.Length), "LENGTH", "Secondary beams (length)"), new("SecondaryPieces", l.SecondaryPieces.Count, "COUNT", "Secondary beams"),
                ]);
            foreach (var (len, n) in l.PrimaryPieces.Concat(l.SecondaryPieces).GroupBy(x => x).OrderBy(x => x.Key).Select(x => (x.Key, x.Count())))
                q.Add(new($"Beams{Math.Round(len * 100)}", n, "COUNT", $"Beams {Lay.M(len)} m"));
            props = l.Props;
            reuses = l.Spec.Reuses;
        }
        else if (layout is BeamLayout b)
        {
            q.AddRange([
                new("ContactArea", b.BottomArea + b.SideArea, "AREA", "Contact area"), new("BottomArea", b.BottomArea, "AREA", "Bottom"), new("SideArea", b.SideArea, "AREA", "Sides"),
                new("FormedLength", b.FreeLength, "LENGTH", "Formed length"), new("ChamferLength", b.ChamferLength, "LENGTH", "Chamfer fillets"), new("Yokes", b.Yokes, "COUNT"),
                new("BearerLength", b.FormBottom ? b.FreeLength * b.Spec.Bearers : 0, "LENGTH", "Bearers (length)"),
            ]);
            props = b.Props;
            reuses = b.Spec.Reuses;
        }
        else yield break;
        q.Add(new("Props", props.Count, "COUNT"));
        foreach (var grp in props.GroupBy(p => p.Size?.Name ?? "—").OrderBy(x => x.Key))
            q.Add(new($"Props:{grp.Key}", grp.Count(), "COUNT", grp.Key == "—" ? "Props out of range" : grp.Key));
        q.Add(new("Reuses", reuses, "COUNT", "Reuses of the type"));
        yield return new QuantitySet("Lapin_FormworkQuantities", q);
    }

    /// <summary>The configurator's section: the layers under a floor of the thickness given (with the spacings used), or the box around a beam of the section given.</summary>
    public static JsonObject Preview(string kind, JsonObject? spec, double thickness, double width, double height, double depth)
    {
        if (kind == "beam")
        {
            var b = ReadBeam(spec);
            var yoke = b.Yokes.FirstOrDefault(y => depth <= y.Depth + 1e-9) ?? b.Yokes[^1];
            var length = Math.Max(0.1, height - b.Plywood - b.BearerHeight);
            return new JsonObject
            {
                ["plywood"] = b.Plywood, ["bearerHeight"] = b.BearerHeight, ["bearerWidth"] = b.BearerWidth, ["bearers"] = b.Bearers,
                ["yokeSpacing"] = yoke.Spacing, ["propSpacing"] = b.PropSpacing, ["chamfer"] = b.Chamfer, ["prop"] = PropFor(b.Props, length)?.Name, ["propLength"] = Math.Round(length, 3),
            };
        }
        var s = ReadSlab(spec);
        var row = s.Rows.FirstOrDefault(r => thickness <= r.Thickness + 1e-9) ?? s.Rows[^1];
        var stack = s.System == "panels" ? s.PanelThickness : s.Plywood + s.SecondaryHeight + s.PrimaryHeight;
        var propLength = Math.Max(0.1, height - stack);
        return new JsonObject
        {
            ["system"] = s.System, ["plywood"] = s.Plywood, ["secondaryHeight"] = s.SecondaryHeight, ["primaryHeight"] = s.PrimaryHeight,
            ["secondary"] = row.Secondary, ["primary"] = row.Primary, ["prop"] = row.Prop, ["rowThickness"] = row.Thickness,
            ["panelLength"] = s.PanelLength, ["panelWidth"] = s.PanelWidth, ["panelThickness"] = s.PanelThickness,
            ["propSize"] = PropFor(s.Props, propLength)?.Name, ["propLength"] = Math.Round(propLength, 3),
        };
    }
}
