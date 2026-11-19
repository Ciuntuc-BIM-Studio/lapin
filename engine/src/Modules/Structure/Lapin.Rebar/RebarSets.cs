using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// A rebar set (decision 730, forum topic 175, as Revit's rebar set with its multi-rebar annotation): one element that lays a
/// straight bar (with legs at its ends when asked) across a slab's, a footing's or a straight wall's extent, at a spacing or a
/// count, in one layer (a slab's or a footing's bottom B1, B2 or top T1, T2; a wall's exterior E1, E2 or interior I1, I2), in a
/// direction (an angle in plan for a slab, horizontal or vertical in a wall), the cover in from the faces; the host's openings
/// and holes stop the bars, the cover off their edges; a bar longer than the stock length is cut in pieces lapped by the
/// settings' lap. Its bars are derived every time they are asked for, never kept: the count, the lengths, the mass in the
/// schedule, the pieces in the bending schedule. It is drawn wherever its host is seen (a plan of its host's storey, a
/// section) and tagged once for the whole set as a distribution line with a tick at each end and the label „12Ø12/150 B1”.
/// Not exported to IFC: bars as elements of the model are the premium module's (decision 402).
/// </summary>
public static class RebarSets
{
    public const string Class = "LapinRebarSet";
    public static readonly string[] SlabLayers = ["B1", "B2", "T1", "T2"];
    public static readonly string[] WallLayers = ["E1", "E2", "I1", "I2"];
    public static readonly string[] Modes = ["spacing", "count"];
    public static readonly string[] HostClasses = [IfcClass.Slab, IfcClass.Footing, IfcClass.Wall];
    /// <summary>The shortest bar a set lays: a stub left between an opening and an edge is left out.</summary>
    public const double MinBar = 0.1;
    /// <summary>The most bars across a set: a mistyped spacing never asks for millions at every redraw.</summary>
    public const int MaxPositions = 5_000;

    /// <summary>What a set asks for, read from its attributes (defaults filled).</summary>
    public sealed record Spec(string Host, string Layer, double Angle, double Diameter, string Mode, double Step, int Count, double? Cover,
        IReadOnlyList<Vec2>? Region, double Legs, bool Splice, string Show)
    {
        public bool Top => Layer[0] is 'T' or 'I';
        public bool Second => Layer.EndsWith('2');
    }

    public static Spec SpecOf(Entity e)
    {
        var region = e.Attrs["region"] is JsonArray ra && ra.Count >= 3 ? ra.Select(Rebars.P).ToList() : null;
        return new Spec(e.Str("host") ?? "", e.Str("layer") ?? "B1", e.Num("angle"), e.NumOrNull("diameter") ?? 0.012, e.Str("mode") ?? "spacing",
            e.NumOrNull("step") ?? 0.15, (int)Math.Clamp(Math.Round(e.Num("count", 10)), 1, MaxPositions), e.NumOrNull("cover"), region, e.Num("legs"),
            !(e.Attrs["splice"] is JsonValue sv && sv.TryGetValue<bool>(out var sp) && !sp), e.Str("show") ?? "one");
    }

    /// <summary>
    /// One bar piece in space: its straight run from <see cref="A"/> to <see cref="B"/>, a leg at either end turned into the host
    /// (null: none), its nominal length (the run and its legs) and the place it is laid at across the set (an index into the positions).
    /// </summary>
    public sealed record Piece(Vec3 A, Vec3 B, Vec3? LegA, Vec3? LegB, double Length, int Position)
    {
        public Vec3[] Chain => [.. new[] { LegA }.OfType<Vec3>(), A, B, .. new[] { LegB }.OfType<Vec3>()];
        public string Shape => (LegA != null, LegB != null) switch { (true, true) => "U", (false, false) => "straight", _ => "L" };
    }

    /// <summary>
    /// A set laid: its plane (an origin, two axes, the bars' direction <see cref="U"/> and the direction across <see cref="V"/>,
    /// world), the places across it (v, metres), the pieces, the spacing as the label writes it, and the extent along the bars.
    /// </summary>
    public sealed record Laid(Spec Spec, Vec3 Origin, Vec3 X, Vec3 Y, Vec2 U, Vec2 V, List<double> Positions, List<Piece> Pieces, string StepText, double UMin, double UMax)
    {
        public Vec3 At(double u, double v) { var p = U * u + V * v; return Origin + X * p.X + Y * p.Y; }
        public Vec3 U3 => X * U.X + Y * U.Y;
        public Vec3 V3 => X * V.X + Y * V.Y;
        /// <summary>The places across that hold a bar.</summary>
        public int Lines => Pieces.Select(p => p.Position).Distinct().Count();
    }

    static bool IsWall(Entity host) => host.Class == IfcClass.Wall;

    /// <summary>The layers a host takes: a slab's and a footing's bottom and top, a wall's exterior and interior faces.</summary>
    public static string[] LayersOf(Entity host) => IsWall(host) ? WallLayers : SlabLayers;

    /// <summary>Lays a set on its host; null when the host is gone or cannot be measured (no body, a curved wall).</summary>
    public static Laid? Lay(IModelView m, Func<string, ElementGeometry?>? geometry, Entity e, RebarSettings st)
    {
        var s = SpecOf(e);
        if (m.Get(s.Host) is not { } host || geometry?.Invoke(host.Id) is not { Parts.Count: > 0 } g) return null;
        var body = Rebar3D.Body(host, g).SelectMany(p => p.Solid.Vertices).ToList();
        if (body.Count == 0) return null;
        var cover = s.Cover ?? st.Cover;
        var d = s.Diameter;
        // The first layer's diameter, under a second one: the set in it on the same host, else the same bar.
        var first = s.Second ? m.OfClass(Class).Where(x => x.Str("host") == host.Id && x.Str("layer") == s.Layer[..1] + "1").Select(x => x.NumOrNull("diameter") ?? 0.012).FirstOrDefault(d) : 0;
        var inset = cover + first + d / 2;
        List<Vec2[]> concrete;
        Vec3 origin, x, y;
        double angle;
        if (IsWall(host))
        {
            var path = PathCurve.Of(host);
            if (path.IsArc || path.Chord < 1e-6) return null;
            var t = (path.End - path.Start).Normalized();
            var n = t.Perp;
            var a = path.Start;
            var (s0, s1) = (body.Min(p => Vec2.Dot(p.Xy - a, t)), body.Max(p => Vec2.Dot(p.Xy - a, t)));
            var (z0, z1) = (body.Min(p => p.Z), body.Max(p => p.Z));
            var (n0, n1) = (body.Min(p => Vec2.Dot(p.Xy - a, n)), body.Max(p => Vec2.Dot(p.Xy - a, n)));
            var depth = LeftFace(m, host, s) ? n1 - inset : n0 + inset;
            origin = new Vec3(a.X, a.Y, 0) + new Vec3(n.X, n.Y, 0) * depth;
            x = new Vec3(t.X, t.Y, 0);
            y = new Vec3(0, 0, 1);
            concrete = [[new(s0, z0), new(s1, z0), new(s1, z1), new(s0, z1)]];
            // A door's or a window's hole (its place along the wall and its sill over the wall's base), or an opening's own body.
            var baseZ = Spatial.Elevation(m, host) + host.Num("baseOffset");
            foreach (var o in Openings(m, host.Id))
            {
                var placed = Lapin.Architecture.Openings.Place(m, o);
                if (placed.Width > 0 && placed.Height > 0)
                    concrete.Add(Box([new(placed.Along - placed.Width / 2, baseZ + placed.Sill), new(placed.Along + placed.Width / 2, baseZ + placed.Sill + placed.Height)]));
                else if (geometry(o.Id) is { Parts.Count: > 0 } og && og.Parts.SelectMany(p => p.Solid.Vertices).Select(p => new Vec2(Vec2.Dot(p.Xy - a, t), p.Z)).ToList() is { Count: > 2 } pts)
                    concrete.Add(Box(pts));
            }
            angle = s.Angle;
            if (n1 - n0 < 2 * inset) return null;
        }
        else
        {
            var (z0, z1) = (body.Min(p => p.Z), body.Max(p => p.Z));
            var z = s.Top ? z1 - inset : z0 + inset;
            if (z1 - z0 < 2 * inset) return null;
            origin = new Vec3(0, 0, z);
            x = new Vec3(1, 0, 0);
            y = new Vec3(0, 1, 0);
            var outline = ArcOutline.BuiltOf(host);
            concrete = [outline.Count >= 3 ? [.. outline] : Polygon2.ConvexHull(body.Select(p => p.Xy))];
            foreach (var hole in OutlineHoles.Built(host)) if (hole.Count >= 3) concrete.Add([.. hole]);
            foreach (var o in Openings(m, host.Id))
            {
                var ring = ArcOutline.BuiltOf(o);
                if (ring.Count >= 3) concrete.Add([.. ring]);
                else if (geometry(o.Id) is { Parts.Count: > 0 } og) concrete.Add(Polygon2.ConvexHull(og.Parts.SelectMany(p => p.Solid.Vertices).Select(p => p.Xy)));
            }
            angle = s.Angle;
        }
        var rad = angle * Math.PI / 180;
        var u = new Vec2(Math.Cos(rad), Math.Sin(rad));
        var v = u.Perp;
        var region = s.Region is { Count: >= 3 } r && !IsWall(host) ? new[] { r.ToArray() } : null;
        var outer = concrete[0];
        // Across: from the concrete's edge plus the cover and half a bar, within the region when one is drawn.
        var (v0, v1) = (outer.Min(p => Vec2.Dot(p, v)) + cover + d / 2, outer.Max(p => Vec2.Dot(p, v)) - cover - d / 2);
        if (region != null) (v0, v1) = (Math.Max(v0, region[0].Min(p => Vec2.Dot(p, v))), Math.Min(v1, region[0].Max(p => Vec2.Dot(p, v))));
        var (u0, u1) = (outer.Min(p => Vec2.Dot(p, u)) - 1, outer.Max(p => Vec2.Dot(p, u)) + 1);
        var positions = new List<double>();
        string stepText;
        if (v1 - v0 < -1e-9) { positions = []; stepText = RebarShapes.Mm(s.Step); }
        else if (s.Mode == "count")
        {
            positions = Rebar3D.Spread(s.Count, v0, v1);
            stepText = RebarShapes.Mm(s.Count > 1 ? (v1 - v0) / (s.Count - 1) : 0);
        }
        else
        {
            var w = Math.Max(0, v1 - v0);
            var n = w < 1e-9 ? 1 : (int)Math.Clamp(Math.Ceiling(w / s.Step + 1 - 1e-9), 2, MaxPositions);
            positions = n == 1 ? [v0] : [.. Enumerable.Range(0, n).Select(k => v0 + w * k / (n - 1))];
            stepText = RebarShapes.Mm(s.Step);
        }
        var stock = st.StockLength;
        var lap = st.LapOf(d);
        // A leg turns into the host: up from the bottom, down from the top, towards the other face of a wall.
        var legDir = new Vec3(0, 0, s.Top ? -1 : 1);
        if (IsWall(host))
        {
            var n = (PathCurve.Of(host).End - PathCurve.Of(host).Start).Normalized().Perp;
            legDir = new Vec3(n.X, n.Y, 0) * (LeftFace(m, host, s) ? -1 : 1);
        }
        var pieces = new List<Piece>();
        double uMin = double.PositiveInfinity, uMax = double.NegativeInfinity;
        for (var k = 0; k < positions.Count; k++)
        {
            var t0 = positions[k];
            // A bar on a region's edge (the first and the last of a count) is tested a hair inside it.
            var tt = Math.Clamp(t0, v0 + 1e-7, Math.Max(v0 + 1e-7, v1 - 1e-7));
            var a2 = u * u0 + v * tt;
            var b2 = u * u1 + v * tt;
            var cuts = new List<(double, double)>();
            Polygon2.InsideIntervals(concrete, a2, b2, 0, 1, cuts);
            // The cover off every concrete edge the bar meets (the outline's, a hole's, an opening's).
            var runs = cuts.Select(c => (A: u0 + (u1 - u0) * c.Item1 + cover, B: u0 + (u1 - u0) * c.Item2 - cover)).Where(c => c.B - c.A > 1e-9).ToList();
            if (region != null)
            {
                var within = new List<(double, double)>();
                Polygon2.InsideIntervals(region, a2, b2, 0, 1, within);
                runs = [.. runs.SelectMany(rn => within.Select(w => (A: Math.Max(rn.A, u0 + (u1 - u0) * w.Item1), B: Math.Min(rn.B, u0 + (u1 - u0) * w.Item2)))).Where(c => c.B - c.A > 1e-9)];
            }
            foreach (var (ra, rb) in runs.Where(c => c.B - c.A >= MinBar))
            {
                uMin = Math.Min(uMin, ra);
                uMax = Math.Max(uMax, rb);
                var legs = Math.Max(0, s.Legs);
                foreach (var (pa, pb, la, lb) in Splits(ra, rb, legs, s.Splice ? stock : double.PositiveInfinity, lap))
                {
                    Vec3 P(double uu) => Map(origin, x, y, u * uu + v * t0);
                    var (A, B) = (P(pa), P(pb));
                    pieces.Add(new Piece(A, B, la ? A + legDir * legs : null, lb ? B + legDir * legs : null, pb - pa + (la ? legs : 0) + (lb ? legs : 0), k));
                }
            }
        }
        if (pieces.Count == 0) (uMin, uMax) = (0, 0);
        return new Laid(s, origin, x, y, u, v, positions, pieces, stepText, uMin, uMax);
    }

    static Vec3 Map(Vec3 o, Vec3 x, Vec3 y, Vec2 p) => o + x * p.X + y * p.Y;

    /// <summary>Whether a wall's set lies by its left face (its axis's left normal).</summary>
    static bool LeftFace(IModelView m, Entity wall, Spec s)
    {
        var negative = LayerSets.UsageOf(m, wall.Id) is { } usage && Layers.ReadUsage(usage) is { Negative: true };
        // Exterior (E) is the face the first layer is on: right of the axis, left when the layers run the negative way.
        return s.Top ? !negative : negative;
    }

    /// <summary>The axis-aligned box round points, as a ring.</summary>
    static Vec2[] Box(IReadOnlyList<Vec2> pts)
    {
        var (x0, x1, y0, y1) = (pts.Min(p => p.X), pts.Max(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.Y));
        return [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];
    }

    /// <summary>The openings voiding a host.</summary>
    static IEnumerable<Entity> Openings(IModelView m, string hostId) =>
        m.OfClass(IfcClass.OpeningElement).Where(o => m.RelatingOf(o.Id, IfcClass.RelVoidsElement)?.Id == hostId);

    /// <summary>
    /// A run from <paramref name="a"/> to <paramref name="b"/> cut in pieces no longer than the stock length (its legs counted),
    /// each next one starting the lap back from where the last ended; the legs on the first piece's start and the last one's end.
    /// One piece when it fits, or when the lap leaves less than half a metre to gain.
    /// </summary>
    public static List<(double A, double B, bool LegA, bool LegB)> Splits(double a, double b, double legs, double stock, double lap)
    {
        var o = new List<(double, double, bool, bool)>();
        if (double.IsInfinity(stock) || stock - lap < 0.5 || b - a + 2 * legs <= stock + 1e-9) { o.Add((a, b, legs > 0, legs > 0)); return o; }
        var at = a;
        for (var guard = 0; guard < 1000; guard++)
        {
            var first = o.Count == 0;
            var own = first && legs > 0 ? legs : 0;
            if (b - at + own + (legs > 0 ? legs : 0) <= stock + 1e-9) { o.Add((at, b, first && legs > 0, legs > 0)); break; }
            var end = at + stock - own;
            o.Add((at, end, first && legs > 0, false));
            at = end - lap;
        }
        return o;
    }

    // ─── The schedule ──────────────────────────────────────────────────────

    /// <summary>The pieces of a set grouped by shape and length, each with its bar shape (straight, L or U with its legs) and how many.</summary>
    public static List<(RebarSpec Spec, int Count)> PieceSpecs(IModelView m, RebarSettings st, Entity e, Func<string, ElementGeometry?>? geometry)
    {
        if (Lay(m, geometry, e, st) is not { } laid) return [];
        var s = laid.Spec;
        return [.. laid.Pieces.GroupBy(p => (p.Shape, Math.Round(p.Length, 3))).OrderByDescending(g => g.Key.Item2).Select(g =>
        {
            var p0 = g.First();
            var run = (p0.B - p0.A).Length;
            var spec = g.Key.Shape switch
            {
                "U" => new RebarSpec("U", new Dictionary<string, double> { ["a"] = run, ["b"] = s.Legs }, null, s.Diameter, null, null),
                "L" => new RebarSpec("L", new Dictionary<string, double> { ["a"] = run, ["b"] = s.Legs }, null, s.Diameter, null, null),
                _ => new RebarSpec("straight", new Dictionary<string, double> { ["length"] = run }, null, s.Diameter, null, null),
            };
            return (spec, g.Count());
        })];
    }

    /// <summary>The schedule's entries: one per shape and length a set's bars come in, under its mark.</summary>
    /// <remarks>A set is measured on its host's body: without the geometry (<paramref name="geometry"/> null) it has none.</remarks>
    public static IEnumerable<(Entity Owner, RebarSchedule.Entry Entry)> Owned(IModelView m, RebarSettings st, Func<string, ElementGeometry?>? geometry)
    {
        foreach (var e in m.OfClass(Class))
        {
            if (e.Bool("excludeSchedule")) continue;
            foreach (var (spec, count) in PieceSpecs(m, st, e, geometry))
                yield return (e, new RebarSchedule.Entry(Rebars.Mark(e), spec.Diameter, RebarShapes.Def(spec.Shape).Label, count, RebarShapes.Length(spec), Derived: true, Partition: Rebars.Partition(e))
                { MarkText = RebarMarks.TextOf(st, e) });
        }
    }

    // ─── Drawing ───────────────────────────────────────────────────────────

    /// <summary>
    /// The sets a view sees (decision 730): in a plan, its storey's (the host's storey, as the stirrups'); in a section, those in
    /// front of it within its depth. Seen face on (a slab's in plan, a wall's in an elevation or a section along it), the bars the
    /// set shows (all, the ends and the middle, or one in the middle) and its tag along the places across: the distribution line
    /// with a tick at each end, the label „12Ø12/150 B1” and the mark's balloon; seen across, the bars as dots in a row, tagged
    /// along the row; seen along, as lines with the label beside them. The label's place is the view's (<c>rebarLabels</c>, key <c>set</c>).
    /// </summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c, RebarSettings st, List<LabelRequest> labels, Func<Entity, bool> hidden)
    {
        var m = c.Model;
        if (!Rebar3D.Shows(c, out var limit, out var plan)) yield break;
        var storey = c.View.Str("storeyId");
        foreach (var e in m.OfClass(Class))
        {
            if (hidden(e) || m.Get(e.Str("host") ?? "") is not { } host || hidden(host)) continue;
            if (plan && Spatial.StoreyOf(m, host.Id)?.Id != storey) continue;
            if (Lay(m, c.Geometry, e, st) is not { Pieces.Count: > 0 } laid) continue;
            var s = laid.Spec;
            var o0 = c.Map(laid.Origin);
            var uSeen = (c.Map(laid.Origin + laid.U3) - o0).Length;
            var vSeen = (c.Map(laid.Origin + laid.V3) - o0).Length;
            var faceOn = uSeen > 0.5 && vSeen > 0.5;
            // Face on, only what it shows; seen any other way, all of them (a cross-section's dots).
            IEnumerable<Piece> shown = laid.Pieces;
            if (faceOn)
            {
                var places = laid.Pieces.Select(p => p.Position).Distinct().Order().ToList();
                var keep = s.Show switch
                {
                    "all" => places,
                    "ends" => places.Count > 3 ? [places[0], places[places.Count / 2], places[^1]] : places,
                    _ => [places[places.Count / 2]],
                };
                shown = laid.Pieces.Where(p => keep.Contains(p.Position));
            }
            var (dots, lines) = Rebar3D.Project(c, shown.Select(p => faceOn ? new[] { p.A, p.B } : p.Chain), limit, plan);
            if (dots.Count == 0 && lines.Count == 0) continue;
            var mk = new ViewMark(e.Id);
            var color = Rebar3D.Draw(mk, c, st, dots, lines, s.Diameter, RebarShapes.LongitudinalLayer);
            var style = LabelStyle.Resolve("sets", st, element: RebarLabels.StyleOf(e));
            var text = style.Text(new LabelFacts(laid.Lines.ToString(CultureInfo.InvariantCulture), s.Diameter, laid.StepText, null, RebarMarks.TextOf(st, e), e.Str("steel") ?? st.Steel, s.Layer));
            LabelString? path = null;
            if (faceOn && laid.Positions.Count >= 2)
            {
                // The distribution line across the bars, a share of the way along them (the top or interior layer's further on, so two layers' tags stand apart).
                var along = laid.UMin + (laid.UMax - laid.UMin) * (s.Top ? 0.3 : 0.5);
                var used = laid.Pieces.Select(p => laid.Positions[p.Position]).ToList();
                var (p0, p1) = (c.Map(laid.At(along, used.Min())), c.Map(laid.At(along, used.Max())));
                if ((p1 - p0).Length > 1e-6) path = new LabelString(new PathCurve(p0, p1, 0), [], 0, 0);
            }
            else if (!faceOn && dots.Count >= 2 && vSeen > 0.5)
            {
                // Across the bars: the row of dots, along the places.
                var dir = (c.Map(laid.Origin + laid.V3) - o0).Normalized();
                var row = dots.OrderBy(p => Vec2.Dot(p, dir)).ToList();
                if ((row[^1] - row[0]).Length > 1e-6) path = new LabelString(new PathCurve(row[0], row[^1], 0), [], 0, 0);
            }
            var request = new LabelRequest(mk, e.Id, "set", style, text, Rebars.Mark(e), color, lines, path == null ? dots : [], RebarLabels.AlongOf(lines), RebarLabels.PlacedOn(c.View, e.Id, "set"))
            { MarkText = RebarMarks.TextOf(st, e), String = path };
            labels.Add(request);
            mk.Data = new JsonObject { ["set"] = true, ["layer"] = s.Layer, ["bars"] = laid.Lines };
            yield return mk;
        }
    }

    /// <summary>The bars of every set in space as chains (3D, <c>rebar.bodies</c>).</summary>
    public static IEnumerable<RebarBodies.Chain> Chains(IModelView m, Func<string, ElementGeometry?>? geometry, RebarSettings st, ISet<string>? ids, Func<Entity, bool>? hides)
    {
        foreach (var e in m.OfClass(Class))
        {
            if ((ids != null && !ids.Contains(e.Id) && !ids.Contains(e.Str("host") ?? "")) || hides?.Invoke(e) == true) continue;
            if (m.Get(e.Str("host") ?? "") is { } host && hides?.Invoke(host) == true) continue;
            if (Lay(m, geometry, e, st) is not { } laid) continue;
            var color = st.ColorByDiameter ? RebarShapes.ColorOf(laid.Spec.Diameter) : "#000000";
            foreach (var p in laid.Pieces) yield return new RebarBodies.Chain(p.Chain, laid.Spec.Diameter, color);
        }
    }

    // ─── Commands ──────────────────────────────────────────────────────────

    /// <summary>The host a set is asked for: given, or the slab or footing under a point (a region's middle) on the view's storey.</summary>
    static Entity HostOf(ICommandContext ctx, JsonObject a)
    {
        if ((string?)a["host"] is { Length: > 0 } id)
            return ctx.Model.Get(id) is { } h && HostClasses.Contains(h.Class) ? h : throw new CommandException("A set is laid in a slab, a footing or a wall.");
        Vec2? at = Args.TryPoint(a["at"]);
        if (at == null && a["region"] is JsonArray ra && ra.Count >= 3) at = Centroid(ra.Select(Rebars.P).ToList());
        if (at == null) throw new CommandException("Say which slab, footing or wall the set is laid in.");
        var storey = Views.Find(ctx.Model, (string?)a["viewId"] ?? "")?.Str("storeyId");
        Entity? best = null;
        foreach (var e in ctx.Model.OfClass(IfcClass.Slab).Concat(ctx.Model.OfClass(IfcClass.Footing)))
        {
            if (storey != null && Spatial.StoreyOf(ctx.Model, e.Id)?.Id != storey) continue;
            var ring = ArcOutline.BuiltOf(e);
            var inside = ring.Count >= 3 ? Polygon2.Contains(ring, at.Value)
                : ctx.Query.Geometry(e.Id) is { Parts.Count: > 0 } g && Polygon2.Contains(Polygon2.ConvexHull(g.Parts.SelectMany(p => p.Solid.Vertices).Select(p => p.Xy)), at.Value);
            if (inside) { best = e; break; }
        }
        return best ?? throw new CommandException("There is no slab or footing there on this storey.");
    }

    static Vec2 Centroid(IReadOnlyList<Vec2> pts) => new(pts.Average(p => p.X), pts.Average(p => p.Y));

    /// <summary>A set's fields from a command, checked (only what it gives).</summary>
    static void Write(JsonObject attrs, JsonObject a, Entity host)
    {
        var layers = LayersOf(host);
        if (a.ContainsKey("layer"))
        {
            var layer = (string?)a["layer"] ?? layers[0];
            if (!layers.Contains(layer)) throw new CommandException(IsWall(host) ? "A wall's set lies by its exterior (E1, E2) or interior face (I1, I2)." : "A slab's set lies at its bottom (B1, B2) or top (T1, T2).");
            attrs["layer"] = layer;
        }
        if (a.ContainsKey("direction") || a.ContainsKey("angle"))
        {
            var angle = (string?)a["direction"] switch
            {
                "x" or "horizontal" => 0.0,
                "y" or "vertical" => 90.0,
                null => Json.Number(a["angle"]) ?? 0,
                var other => throw new CommandException($"A set runs along x or y (horizontal or vertical in a wall), or at an angle, not „{other}”."),
            };
            if (IsWall(host) && angle is not (0 or 90)) throw new CommandException("A wall's set runs horizontally or vertically.");
            attrs["angle"] = Math.Round(((angle % 180) + 180) % 180, 6);
        }
        if (a.ContainsKey("diameter"))
        {
            var d = Json.Number(a["diameter"]) ?? throw new CommandException("The diameter is a length.");
            if (!(d >= 0.004 && d <= 0.05)) throw new CommandException("A bar is between 4 and 50 mm thick.");
            attrs["diameter"] = Math.Round(d, 6);
        }
        if (a.ContainsKey("mode"))
        {
            var mode = (string?)a["mode"] ?? "spacing";
            if (!Modes.Contains(mode)) throw new CommandException("A set is laid at a spacing or as a count of bars.");
            attrs["mode"] = mode;
        }
        if (a.ContainsKey("step"))
        {
            var step = Json.Number(a["step"]) ?? throw new CommandException("The spacing is a length.");
            if (!(step >= 0.03 && step <= 2)) throw new CommandException("The spacing is between 30 mm and 2 m.");
            attrs["step"] = Math.Round(step, 6);
        }
        if (a.ContainsKey("count"))
        {
            var n = Json.Number(a["count"]) ?? throw new CommandException("The count is a number.");
            if (!(n >= 1 && n <= MaxPositions)) throw new CommandException($"A set has 1 to {MaxPositions} bars across.");
            attrs["count"] = Math.Round(n);
        }
        if (a.ContainsKey("cover"))
        {
            if (a["cover"] == null) attrs.Remove("cover");
            else
            {
                var cv = Json.Number(a["cover"]) ?? throw new CommandException("The cover is a length.");
                if (!(cv >= 0 && cv <= 0.2)) throw new CommandException("The cover is between 0 and 200 mm.");
                attrs["cover"] = Math.Round(cv, 6);
            }
        }
        if (a.ContainsKey("region"))
        {
            if (a["region"] is JsonArray ra && ra.Count > 0)
            {
                if (IsWall(host)) throw new CommandException("A wall's set covers its whole face (a region is drawn in a slab).");
                var pts = ra.Select(n => Args.Point(n, "a corner of the region")).ToList();
                if (pts.Count < 3 || Math.Abs(Polygon2.SignedArea(pts)) < 1e-6) throw new CommandException("A region has three corners at least and encloses something.");
                attrs["region"] = new JsonArray(pts.Select(p => (JsonNode)Rebars.J(p)).ToArray());
            }
            else attrs.Remove("region");
        }
        if (a.ContainsKey("legs"))
        {
            var legs = Json.Number(a["legs"]) ?? 0;
            if (!(legs >= 0 && legs <= 2)) throw new CommandException("A leg is 0 to 2 m long.");
            if (legs > 0) attrs["legs"] = Math.Round(legs, 6); else attrs.Remove("legs");
        }
        if (a.ContainsKey("splice")) { if (a["splice"] is JsonValue sv && sv.TryGetValue<bool>(out var sp) && !sp) attrs["splice"] = false; else attrs.Remove("splice"); }
        if (a.ContainsKey("show"))
        {
            var sh = (string?)a["show"] ?? "one";
            if (!Rebars.Shows.Contains(sh)) throw new CommandException("A set shows all its bars, its ends, or one.");
            if (sh == "one") attrs.Remove("show"); else attrs["show"] = sh;
        }
        if (a.ContainsKey("mark"))
        {
            var n = Json.Number(a["mark"]) ?? throw new CommandException("mark is a number.");
            if (!(n >= 1)) throw new CommandException("mark is 1 at least.");
            attrs["mark"] = Math.Round(n);
        }
        if (a.ContainsKey(RebarMarks.PrefixKey)) RebarModule.WritePrefix(attrs, a);
        if (a.ContainsKey(Rebars.PartitionKey)) { if (((string?)a[Rebars.PartitionKey])?.Trim() is { Length: > 0 } pa) attrs[Rebars.PartitionKey] = pa; else attrs.Remove(Rebars.PartitionKey); }
        if (a.ContainsKey("steel")) { if ((string?)a["steel"] is { Length: > 0 } stl) attrs["steel"] = stl; else attrs.Remove("steel"); }
        if (a.ContainsKey("excludeSchedule")) { if (a["excludeSchedule"] is JsonValue ev && ev.TryGetValue<bool>(out var ex) && ex) attrs["excludeSchedule"] = true; else attrs.Remove("excludeSchedule"); }
        if (a.ContainsKey(LabelStyle.Key))
        {
            if (a[LabelStyle.Key] is JsonObject lo && lo.ContainsKey("style"))
            {
                if (attrs[LabelStyle.Key] is not JsonObject label) attrs[LabelStyle.Key] = label = new JsonObject();
                if (LabelStyle.Patch(label["style"] as JsonObject, lo["style"]) is { } stl) label["style"] = stl; else label.Remove("style");
                if (label.Count == 0) attrs.Remove(LabelStyle.Key);
            }
            else if (a[LabelStyle.Key] == null) attrs.Remove(LabelStyle.Key);
        }
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Rebar set", [
            new("layer", "Layer", "choice", Choices: [.. SlabLayers, .. WallLayers]), new("angle", "Direction (°)", "angle"),
            new("diameter", "Diameter", "length", Min: 0.004, Max: 0.05), new("mode", "Laid by", "choice", Choices: Modes),
            new("step", "Spacing", "length", Min: 0.03), new("count", "Bars", "number", Min: 1), new("cover", "Cover", "length", Min: 0),
            new("legs", "Legs", "length", Min: 0), new("splice", "Lapped at the stock length", "bool"), new("show", "Shown", "choice", Choices: Rebars.Shows),
            new("mark", "Mark", "number", Min: 1), new(RebarMarks.PrefixKey, "Mark prefix", "text"), new("steel", "Steel", "choice", Choices: RebarShapes.Steels),
            new("excludeSchedule", "Not in the schedule", "bool"), new(Rebars.PartitionKey, "Partition", "text"),
        ], "#0e7490", Kind: "annotation"));

        // A set in a slab, a footing or a straight wall: {host | viewId + at | viewId + region, layer?, direction? (x, y, horizontal, vertical) | angle?,
        // diameter?, mode? (spacing, count), step?, count?, cover?, region?: [[x, y]…] (a slab's, in plan), legs?, splice?, show?, mark?, markPrefix?, partition?, steel?}.
        r.Command("addSet", "Rebar set", (ctx, a) =>
        {
            var host = HostOf(ctx, a);
            if (IsWall(host) && PathCurve.Of(host).IsArc) throw new CommandException("A set is laid in a straight wall (a curved one is not done yet).");
            var layer = (string?)a["layer"] ?? LayersOf(host)[0];
            var attrs = new JsonObject { ["host"] = host.Id, ["layer"] = layer, ["diameter"] = 0.012, ["step"] = 0.15 };
            var args = (JsonObject)a.DeepClone();
            args["layer"] = layer;
            // A second layer crosses the first: along y in a slab, vertical in a wall, unless told.
            if (!args.ContainsKey("direction") && !args.ContainsKey("angle")) args["angle"] = layer.EndsWith('2') ? 90 : 0;
            RebarRuleSection.Complete(ctx.Model, args, Json.Number(args["diameter"]) ?? 0.012, "straight");
            Write(attrs, args, host);
            var partition = Rebars.Partition(new Entity("", Class, "", attrs));
            if (attrs["mark"] == null) attrs["mark"] = Rebars.NextMarkFor(ctx.Model, partition, "straight", attrs[RebarMarks.PrefixKey] is { } own ? (string?)own : null);
            var e = new Entity(ctx.NewId(), Class, $"Rebar set {layer}", attrs);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });
        r.Command("updateSet", "Rebar set", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("Not a rebar set.");
            var host = ctx.Model.Get(e.Str("host") ?? "") ?? throw new CommandException("The set's host is gone.");
            var attrs = e.CloneAttrs();
            Write(attrs, a, host);
            ctx.Replace(e.With(attrs: attrs, name: a.ContainsKey("layer") ? $"Rebar set {attrs["layer"]}" : null));
            return null;
        });
        // What a set lays: {bars (places across), pieces, step, lengths: [{shape, length, count}], total (m), mass (kg), label, layers (the host's), cover}.
        r.Query("describeSet", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("Not a rebar set.");
            var st = RebarSettings.Of(q.Model);
            var host = q.Model.Get(e.Str("host") ?? "");
            var o = new JsonObject { ["layers"] = new JsonArray((host != null ? LayersOf(host) : SlabLayers).Select(l => (JsonNode)l).ToArray()), ["wall"] = host != null && IsWall(host),
                ["mark"] = RebarMarks.TextOf(st, e), ["cover"] = SpecOf(e).Cover ?? st.Cover };
            if (Lay(q.Model, q.Geometry, e, st) is not { } laid) { o["bars"] = 0; o["error"] = host == null ? "The set's host is gone." : "The host cannot be measured."; return o; }
            var specs = PieceSpecs(q.Model, st, e, q.Geometry);
            var total = specs.Sum(x => x.Count * RebarShapes.Length(x.Spec));
            o["bars"] = laid.Lines;
            o["pieces"] = laid.Pieces.Count;
            o["step"] = laid.StepText;
            o["lengths"] = new JsonArray(specs.Select(x => (JsonNode)new JsonObject { ["shape"] = x.Spec.Shape, ["length"] = Math.Round(RebarShapes.Length(x.Spec), 3), ["count"] = x.Count }).ToArray());
            o["total"] = Math.Round(total, 3);
            o["mass"] = Math.Round(total * RebarShapes.MassPerMetre(laid.Spec.Diameter), 2);
            o["label"] = LabelStyle.Resolve("sets", st, element: RebarLabels.StyleOf(e)).Text(new LabelFacts(laid.Lines.ToString(CultureInfo.InvariantCulture), laid.Spec.Diameter, laid.StepText, null, RebarMarks.TextOf(st, e), e.Str("steel") ?? st.Steel, laid.Spec.Layer));
            return o;
        });
    }
}

/// <summary>A host deleted takes its sets with it: they are laid in it and have nothing to stand for without it.</summary>
sealed class RebarSetHosts : IDeletionFollowers
{
    public void Deleting(ICommandContext ctx, IReadOnlySet<string> deleting)
    {
        foreach (var set in ctx.Model.OfClass(RebarSets.Class).Where(s => s.Str("host") is { } h && deleting.Contains(h) && !deleting.Contains(s.Id)).ToList())
            ctx.Remove(set.Id);
    }
}
