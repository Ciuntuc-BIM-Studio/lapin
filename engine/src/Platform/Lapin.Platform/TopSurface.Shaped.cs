using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The slope engine's shaped tops (docs/plan-floor-slopes.md; site grading T7, forum 157 / issue #96): a top over an outline with
/// holes that is not only one plane but made of what a user sets on it. Floors use it; it extends <see cref="TopSurface"/> (the
/// same <see cref="Spec"/> for its plane, the same grades) and leaves its <see cref="TopSurface.Solve"/> as it was, so a hardscape's
/// top is built exactly as before.
/// <list type="bullet">
/// <item><b>The base</b>: level at an offset, a plane (a grade towards a direction, an arrow) or by the corners' heights, as a
/// hardscape's top (<see cref="Spec"/>; <c>elevation</c> is the offset from the element's reference height).</item>
/// <item><b>Edge slopes</b> (they replace the base's plane): from an edge of the outline, at a grade, falling away from it
/// (<c>in</c>) or towards it (<c>out</c>), starting at its offset, as far as a limit (<c>until</c>: an offset, or a distance in
/// plan or along the slope), then level; a limit acts only on the part its own slope covers (see <see cref="FieldOf"/>). Edges falling away from themselves meet in valleys (the higher of their planes holds,
/// so each edge keeps its height); edges falling towards themselves meet in ridges (the lower holds); with both, the lower of
/// the two.</item>
/// <item><b>Points</b> at an offset or an absolute level, <b>lines</b> with a height at each end (straight between), and
/// <b>drains</b> (a point, a square grate or a channel to a second point) sunk by a depth below the surface there: each makes the
/// surface pass through it; between them and the outline (held where the base or the edge slopes have it) the surface is
/// triangulated, Delaunay but for the lines, which stay edges.</item>
/// <item><b>Regions</b>: an outline inside, with its own base, slopes, points and drains; it steps from the rest along its
/// outline.</item>
/// </list>
/// The result is exact: every crease where two planes meet or a fall reaches its limit is an edge of the triangles, so a body built
/// on them has the true volume. Stored on the element's <c>surface</c> attribute (<see cref="ReadShaped"/>).
/// </summary>
public static partial class TopSurface
{
    public const string SurfaceKey = "surface";
    public const string Bend = "bend", Sculpt = "layer";

    /// <summary>A slope from an edge of an outline: its index, grade, way (falls <c>in</c>, away from the edge, or <c>out</c>, towards it), height at the edge, and where it stops.</summary>
    public sealed record EdgeSlope(int Edge, double Grade, bool FallsIn, double? Offset, double? UntilOffset, double? UntilDistance, bool AlongSlope);

    /// <summary>A point the surface passes through: at an offset from the reference, or (<paramref name="Level"/>) at an absolute height.</summary>
    public sealed record ShapePoint(Vec2 At, double? Offset, double? Level);

    /// <summary>A line the surface passes through, straight between its ends, each at an offset or an absolute level.</summary>
    public sealed record ShapeLine(Vec2 A, Vec2 B, double[]? Offset, double[]? Level);

    /// <summary>A drain sunk <paramref name="Depth"/> below the surface: a point, a square grate of side <paramref name="Grate"/>, or a channel to <paramref name="To"/>.</summary>
    public sealed record Drain(Vec2 At, Vec2? To, double Depth, double Grate, bool Centre = false);

    /// <summary>One shaped top: how a build-up follows it, its base, its edge slopes, points, lines and drains.</summary>
    public sealed record Shape(string? How, int? Layer, double? MinThickness, Spec Base, IReadOnlyList<EdgeSlope> Edges,
        IReadOnlyList<ShapePoint> Points, IReadOnlyList<ShapeLine> Lines, IReadOnlyList<Drain> Drains)
    {
        public bool Flat => Edges.Count == 0 && Points.Count == 0 && Lines.Count == 0 && Drains.Count == 0
            && Base.Mode == Level && Math.Abs(Base.Elevation) < 1e-12;
    }

    /// <summary>A region inside the outline with its own top: drawn (<paramref name="Outline"/>) or a room's (<paramref name="RoomId"/>, its outline found on every ask).</summary>
    public sealed record ShapeRegion(Shape Shape, IReadOnlyList<Vec2>? Outline, string? RoomId);

    /// <summary>An element's shaped top: the main one and its regions.</summary>
    public sealed record Shaped(Shape Main, IReadOnlyList<ShapeRegion> Regions);

    // ─── Reading and writing ─────────────────────────────────────────────

    static Vec2? Pt(JsonNode? n) => n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y && double.IsFinite(x) && double.IsFinite(y) ? new Vec2(x, y) : null;
    static double[]? Pair(JsonNode? n) => n is JsonArray { Count: 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? [x, y] : null;
    static List<Vec2> Loop(JsonNode? n) => n is JsonArray a ? [.. a.Select(Pt).OfType<Vec2>()] : [];

    /// <summary>The shape a JSON object says (a <c>surface</c>, or one of its regions); <paramref name="corners"/>: its outline's corner count, for <c>cornerZ</c>.</summary>
    public static Shape ReadShape(JsonObject o, int corners)
    {
        var edges = new List<EdgeSlope>();
        foreach (var x in (o["edges"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Json.Number(x["edge"]) is not { } e || Grade(x["grade"]) is not { } g) continue;
            var until = x["until"] as JsonObject;
            edges.Add(new EdgeSlope((int)e, Math.Abs(g), (string?)x["fall"] != "out", Json.Number(x["offset"]), Json.Number(until?["offset"]),
                Json.Number(until?["distance"]), (string?)until?["along"] == "slope"));
        }
        var points = (o["points"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(x => Pt(x["at"]) is { } at ? new ShapePoint(at, Json.Number(x["offset"]), Json.Number(x["level"])) : null).OfType<ShapePoint>().ToList();
        var lines = (o["lines"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(x => Pt(x["a"]) is { } a && Pt(x["b"]) is { } b ? new ShapeLine(a, b, Pair(x["offset"]), Pair(x["level"])) : null).OfType<ShapeLine>().ToList();
        // A drain at the middle (centre: true, a room's) is placed where its outline's middle is on every ask.
        var drains = (o["drains"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(x => (Pt(x["at"]), x["centre"] is JsonValue cv && cv.TryGetValue<bool>(out var cb) && cb) is var (at, centre) && (at != null || centre)
                ? new Drain(at ?? default, centre ? null : Pt(x["to"]), Math.Max(0, Json.Number(x["depth"]) ?? 0.02), Math.Max(0, Json.Number(x["grate"]) ?? 0), centre) : null)
            .OfType<Drain>().ToList();
        var how = (string?)o["how"] is Bend or Sculpt ? (string?)o["how"] : null;
        var layer = Json.Number(o["layer"]) is { } l && l >= 0 ? (int)l : (int?)null;
        return new Shape(how, layer, Json.Number(o["minThickness"]), Read(o, corners), edges, points, lines, drains);
    }

    /// <summary>The shaped top an element's attributes hold; null when it has none (a flat element, built as before).</summary>
    public static Shaped? ReadShaped(JsonObject attrs, int corners)
    {
        if (attrs[SurfaceKey] is not JsonObject s) return null;
        var regions = new List<ShapeRegion>();
        foreach (var r in (s["regions"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var outline = Loop(r["outline"]);
            var roomId = (string?)r["roomId"];
            if (outline.Count < 3 && roomId == null) continue;
            regions.Add(new ShapeRegion(ReadShape(r, Math.Max(outline.Count, 0)), outline.Count >= 3 ? outline : null, roomId));
        }
        return new Shaped(ReadShape(s, corners), regions);
    }

    // ─── Solving ─────────────────────────────────────────────────────────

    /// <summary>A field over the plan: a plane, or the lower or higher of two.</summary>
    abstract record Field
    {
        public abstract double At(Vec2 p);
    }

    sealed record PlaneField(double A, double B, double C) : Field
    {
        public override double At(Vec2 p) => A + B * p.X + C * p.Y;
    }

    sealed record MinField(Field L, Field R) : Field
    {
        public override double At(Vec2 p) => Math.Min(L.At(p), R.At(p));
    }

    sealed record MaxField(Field L, Field R) : Field
    {
        public override double At(Vec2 p) => Math.Max(L.At(p), R.At(p));
    }

    /// <summary>The mesh split along every crease of a field, so the field is linear on each of its triangles.</summary>
    static void Refine(PlanMesh m, Field f)
    {
        switch (f)
        {
            case MinField(var l, var r): Refine(m, l); Refine(m, r); m.Split(i => l.At(m.Points[i]) - r.At(m.Points[i])); break;
            case MaxField(var l, var r): Refine(m, l); Refine(m, r); m.Split(i => l.At(m.Points[i]) - r.At(m.Points[i])); break;
        }
    }

    /// <summary>
    /// One piece of a shaped top worked out: its mesh (channel 0 the height above the reference at each point), the shape it
    /// follows, the region it is (-1 the main one), the plane it is when it is one, and what it says.
    /// </summary>
    public sealed record Piece(int Region, Shape Shape, PlanMesh Mesh, (double A, double B, double C)? Plane, IReadOnlyList<string> Notes)
    {
        public double H(int i) => Mesh.Values[i][0];
        public double Min => Mesh.Values.Count == 0 ? 0 : Mesh.Values.Min(v => v[0]);
        public double Max => Mesh.Values.Count == 0 ? 0 : Mesh.Values.Max(v => v[0]);

        /// <summary>The height at a point in plan, linear over the triangle it lies in; null outside the piece.</summary>
        public double? At(Vec2 p) => Mesh.ValuesAt(p)?[0];

        /// <summary>The true area of the shaped top (on the slope).</summary>
        public double SurfaceArea
        {
            get
            {
                var s = 0.0;
                foreach (var (a, b, c) in Mesh.Triangles)
                {
                    var (pa, pb, pc) = (Mesh.Points[a].At(H(a)), Mesh.Points[b].At(H(b)), Mesh.Points[c].At(H(c)));
                    s += Vec3.Cross(pb - pa, pc - pa).Length / 2;
                }
                return s;
            }
        }
    }

    /// <summary>
    /// A shaped top over an outline (its points as laid, counter-clockwise; <paramref name="corners"/> its corners, which edge
    /// indices count) with holes: the main piece and each region's, each its own mesh. <paramref name="reference"/> is the model
    /// height of offset 0, what an absolute level is measured from; <paramref name="regionOutline"/> finds a room's outline for a
    /// region linked to one. A region that does not lie inside the outline, clear of its holes and the other regions, is left out
    /// with a note.
    /// </summary>
    public static List<Piece> SolveShaped(Shaped s, IReadOnlyList<Vec2> laid, IReadOnlyList<Vec2> corners, IReadOnlyList<IReadOnlyList<Vec2>> holes,
        double reference, Func<string, IReadOnlyList<Vec2>?>? regionOutline = null, IReadOnlyList<double>? rises = null)
    {
        var notes = new List<string>();
        var regions = new List<(int Index, ShapeRegion Region, List<Vec2> Outline)>();
        for (var r = 0; r < s.Regions.Count; r++)
        {
            var reg = s.Regions[r];
            var outline = reg.RoomId is { } room ? regionOutline?.Invoke(room)?.ToList() : reg.Outline?.ToList();
            if (outline == null || outline.Count < 3) { notes.Add($"Region {r + 1}: its room has no outline now; left out."); continue; }
            if (Polygon2.SignedArea(outline) < 0) outline.Reverse();
            if (!Inside(outline, laid, holes) || regions.Any(o => Overlap(o.Outline, outline)))
            {
                notes.Add($"Region {r + 1} does not lie inside the outline clear of its holes and the other regions; left out (split the element by a line instead).");
                continue;
            }
            regions.Add((r, reg, outline));
        }
        var o = new List<Piece>();
        var mainHoles = holes.Concat(regions.Select(x => (IReadOnlyList<Vec2>)x.Outline)).ToList();
        if (Piece1(-1, s.Main, laid, corners, rises, mainHoles, reference, notes) is { } main) o.Add(main);
        foreach (var (index, reg, outline) in regions)
            if (Piece1(index, reg.Shape, outline, outline, null, [], reference, []) is { } p) o.Add(p);
        if (o.Count > 0 && notes.Count > 0) o[0] = o[0] with { Notes = [.. o[0].Notes, .. notes] };
        return o;
    }

    static bool Inside(IReadOnlyList<Vec2> inner, IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> holes)
    {
        if (!inner.All(p => Polygon2.Contains(outer, p) && Distance(outer, p) > 1e-4)) return false;
        if (Crosses(inner, outer)) return false;
        foreach (var h in holes) if (Overlap(h, inner)) return false;
        return true;
    }

    static double Distance(IReadOnlyList<Vec2> loop, Vec2 p)
    {
        var d = double.PositiveInfinity;
        for (var i = 0; i < loop.Count; i++) d = Math.Min(d, Polyline2.SegmentDistance(p, loop[i], loop[(i + 1) % loop.Count]));
        return d;
    }

    static bool Crosses(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b)
    {
        for (var i = 0; i < a.Count; i++)
            for (var j = 0; j < b.Count; j++)
                if (Polyline2.SegmentsDistance(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count]) < 1e-4) return true;
        return false;
    }

    static bool Overlap(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b) =>
        Crosses(a, b) || a.Any(p => Polygon2.Contains(b, p)) || b.Any(p => Polygon2.Contains(a, p));

    /// <summary>The field of the base or the edge slopes over an outline whose corners are <paramref name="corners"/> (counter-clockwise).</summary>
    static Field FieldOf(Shape sh, IReadOnlyList<Vec2> corners, IReadOnlyList<Vec2> laid, List<string> notes, out Solved? cornersTop, IReadOnlyList<double>? rises)
    {
        cornersTop = null;
        if (sh.Edges.Count == 0)
        {
            var spec = sh.Base;
            switch (spec.Mode)
            {
                case Plane:
                {
                    var origin = spec.Origin ?? Polygon2.Centroid(laid);
                    var d = spec.Down;
                    return new PlaneField(spec.Elevation + spec.Grade * (origin.X * d.X + origin.Y * d.Y), -spec.Grade * d.X, -spec.Grade * d.Y);
                }
                case Corners:
                {
                    var solved = Solve(spec, corners, rises);
                    if (solved.PlaneOf is var (a, b, c)) return new PlaneField(a, b, c);
                    // Not one plane: the outline holds the corners' heights, the inside triangulated (as a hardscape's, with Delaunay's triangles).
                    cornersTop = solved;
                    notes.Add(solved.Note ?? "The corners' heights are not in one plane: the top is triangulated between them.");
                    var fit = Fit(corners, [.. corners.Select((_, i) => i < spec.CornerZ.Count && spec.CornerZ[i] is { } v ? v : spec.Elevation)]);
                    return fit is var (fa, fb, fc, _) ? new PlaneField(fa, fb, fc) : new PlaneField(spec.Elevation, 0, 0);
                }
                default:
                    return new PlaneField(spec.Elevation, 0, 0);
            }
        }
        // Each edge's plane and its limit (the level it stops at), in its group: those falling away from their edge, those falling towards it.
        var slopes = new List<(bool FallsIn, PlaneField Plane, double? Stop)>();
        foreach (var e in sh.Edges)
        {
            if (e.Edge < 0 || e.Edge >= corners.Count) { notes.Add($"Edge {e.Edge + 1} is not an edge of the outline now; its slope is left out."); continue; }
            var (a, b) = (corners[e.Edge], corners[(e.Edge + 1) % corners.Count]);
            if ((b - a).Length < 1e-9) continue;
            var n = (b - a).Normalized().Perp; // inward, the outline running counter-clockwise
            var h = e.Offset ?? sh.Base.Elevation;
            var sgn = e.FallsIn ? -1.0 : 1.0;
            var g = e.Grade;
            var plane = new PlaneField(h - sgn * g * (n.X * a.X + n.Y * a.Y), sgn * g * n.X, sgn * g * n.Y);
            double? target = e.UntilOffset;
            if (target == null && e.UntilDistance is { } dist && dist > 0)
            {
                var plan = e.AlongSlope ? dist * Math.Cos(Math.Atan(g)) : dist;
                target = h + sgn * g * plan;
            }
            // A limit on the wrong side of the edge's height (a fall that would rise to it) is the edge's height: level from the edge.
            slopes.Add((e.FallsIn, plane, target is { } t ? (e.FallsIn ? Math.Min(t, h) : Math.Max(t, h)) : null));
        }
        Field? falls = null, rises2 = null;
        for (var i = 0; i < slopes.Count; i++)
        {
            var (fallsIn, p, stop) = slopes[i];
            Field f = p;
            if (stop is { } t)
            {
                // A limit acts near its own edge only (follow-ups of 23.11.2026): on the part of the outline its plane covers, where
                // it holds among its group's planes (the higher of those falling away from their edges, the lower of those falling
                // towards them). There the slope stops at the limit and the top is level, until, near the valley or ridge where its
                // plane met the next one, it comes down (or up) to it along that plane mirrored in its own (2P − Q), so the valley or
                // ridge stays where it was and the other edges' planes are not touched. An edge alone in its group: level beyond its
                // limit everywhere, as before.
                Field? mirror = null;
                for (var k = 0; k < slopes.Count; k++)
                {
                    if (k == i || slopes[k].FallsIn != fallsIn) continue;
                    var q = slopes[k].Plane;
                    Field m = new PlaneField(2 * p.A - q.A, 2 * p.B - q.B, 2 * p.C - q.C);
                    mirror = mirror == null ? m : fallsIn ? new MinField(mirror, m) : new MaxField(mirror, m);
                }
                Field level = new PlaneField(t, 0, 0);
                if (mirror != null) level = fallsIn ? new MinField(level, mirror) : new MaxField(level, mirror);
                f = fallsIn ? new MaxField(p, level) : new MinField(p, level);
            }
            if (fallsIn) falls = falls == null ? f : new MaxField(falls, f);
            else rises2 = rises2 == null ? f : new MinField(rises2, f);
        }
        return (falls, rises2) switch
        {
            ({ } f1, { } r1) => new MinField(r1, f1),
            ({ } f1, null) => f1,
            (null, { } r1) => r1,
            _ => new PlaneField(sh.Base.Elevation, 0, 0),
        };
    }

    static Piece? Piece1(int region, Shape sh, IReadOnlyList<Vec2> laid, IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises,
        IReadOnlyList<IReadOnlyList<Vec2>> holes, double reference, List<string> notes)
    {
        // Channels: 0 the height (worked out last), 1 the correction the points, lines and drains make over the field.
        if (PlanMesh.Of(laid, holes, 2) is not { } m) return null;
        var own = new List<string>();
        // A drain at the middle: the outline's middle as it is now (its centroid, or a point inside when that falls outside).
        if (sh.Drains.Any(d => d.Centre))
        {
            var mid = Polygon2.Centroid(laid);
            if (!Polygon2.Contains(laid, mid) || holes.Any(h => Polygon2.Contains(h, mid))) mid = Polygon2.InteriorPoint(laid);
            sh = sh with { Drains = [.. sh.Drains.Select(d => d.Centre ? d with { At = mid } : d)] };
        }
        var field = FieldOf(sh, corners, laid, own, out var cornersTop, rises);
        var boundary = m.Points.Count;
        // The controls: their points in first, then the lines and channels as edges.
        double Target(ShapePoint p) => p.Level is { } lv ? lv - reference : p.Offset ?? 0;
        var fixedAt = new Dictionary<int, Func<Vec2, double>>();
        var lineRules = new List<(Vec2 A, Vec2 B, Func<Vec2, double> Correction)>();
        void Missing(string what) => own.Add($"{what} lies outside the outline; left out.");
        foreach (var p in sh.Points)
        {
            var k = m.Insert(p.At);
            if (k < 0) { Missing($"The point at ({p.At.X:0.###}, {p.At.Y:0.###})"); continue; }
            var t = Target(p);
            fixedAt[k] = q => t - field.At(q);
        }
        foreach (var d in sh.Drains)
        {
            var depth = d.Depth;
            if (d.To is { } to)
            {
                if (!m.InsertSegment(d.At, to)) { Missing("A channel drain"); continue; }
                lineRules.Add((d.At, to, _ => -depth));
                continue;
            }
            if (d.Grate > 1e-6)
            {
                var r = d.Grate / 2;
                Vec2[] sq = [d.At + new Vec2(-r, -r), d.At + new Vec2(r, -r), d.At + new Vec2(r, r), d.At + new Vec2(-r, r)];
                if (sq.Any(q => m.Locate(q) == null)) { Missing("A drain's grate"); continue; }
                for (var i = 0; i < 4; i++) { m.InsertSegment(sq[i], sq[(i + 1) % 4]); lineRules.Add((sq[i], sq[(i + 1) % 4], _ => -depth)); }
                var c = m.Insert(d.At);
                if (c >= 0) fixedAt[c] = _ => -depth;
                continue;
            }
            var k = m.Insert(d.At);
            if (k < 0) { Missing($"The drain at ({d.At.X:0.###}, {d.At.Y:0.###})"); continue; }
            fixedAt[k] = _ => -depth;
        }
        foreach (var l in sh.Lines)
        {
            if (!m.InsertSegment(l.A, l.B)) { Missing("A line"); continue; }
            var (ta, tb) = l.Level is { } lv ? (lv[0] - reference, lv[1] - reference) : l.Offset is { } of ? (of[0], of[1]) : (0.0, 0.0);
            var (a, b) = (l.A, l.B);
            var len2 = Vec2.Dot(b - a, b - a);
            lineRules.Add((a, b, q =>
            {
                var s = len2 > 0 ? Math.Clamp(Vec2.Dot(q - a, b - a) / len2, 0, 1) : 0;
                return ta + (tb - ta) * s - field.At(q);
            }));
        }
        // The corrections at every point there is now: the outline's (0, or the corners' heights off the field), the controls', the lines'.
        void Corrections()
        {
            for (var i = 0; i < m.Points.Count; i++)
            {
                var q = m.Points[i];
                double? v = null;
                foreach (var (a, b, rule) in lineRules) if (OnLine(q, a, b)) { v = rule(q); break; }
                if (v == null && fixedAt.TryGetValue(i, out var f)) v = f(q);
                if (v == null && i < boundary) v = cornersTop != null ? cornersTop.At(q) - field.At(q) : 0;
                if (v is { } x) m.Values[i][1] = x;
            }
        }
        Corrections();
        // The field's creases made edges (the corrections carried linearly along them), the lines held again where a crease met them.
        Refine(m, field);
        for (var i = 0; i < m.Points.Count; i++)
        {
            var q = m.Points[i];
            foreach (var (a, b, rule) in lineRules) if (OnLine(q, a, b)) { m.Values[i][1] = rule(q); break; }
            m.Values[i][0] = field.At(q) + m.Values[i][1];
        }
        // One plane when every point lies on the plane of the field's first three (within a micrometre).
        (double, double, double)? plane = null;
        if (TopSurface.Fit(m.Points, [.. m.Values.Select(v => v[0])]) is var (pa, pb, pc, off) && off < 1e-6) plane = (pa, pb, pc);
        return new Piece(region, sh, m, plane, [.. own, .. notes]);
    }

    static bool OnLine(Vec2 p, Vec2 a, Vec2 b) => Polyline2.SegmentDistance(p, a, b) < 1e-7;

    /// <summary>
    /// A piece's heights held from below at <paramref name="floor"/> (a sculpted layer's minimum): the mesh split where the height
    /// crosses it, so the held part is exact; the plan area held there.
    /// </summary>
    public static double Hold(Piece p, double floor)
    {
        p.Mesh.Split(i => p.H(i) - floor);
        var held = 0.0;
        foreach (var (a, b, c) in p.Mesh.Triangles)
            if (p.H(a) <= floor + 1e-9 && p.H(b) <= floor + 1e-9 && p.H(c) <= floor + 1e-9 && (p.H(a) < floor - 1e-9 || p.H(b) < floor - 1e-9 || p.H(c) < floor - 1e-9))
                held += Math.Abs(Vec2.Cross(p.Mesh.Points[b] - p.Mesh.Points[a], p.Mesh.Points[c] - p.Mesh.Points[a])) / 2;
        return held;
    }

    // ─── Following edits ─────────────────────────────────────────────────

    /// <summary>
    /// A <c>surface</c> carried by a move, a turn or a mirror: its points, lines, drains, regions, its plane's origin and
    /// direction; mirrored, the outline's corners run back (<paramref name="corners"/> of them), so its edge indices and its
    /// corners' heights follow, and each region's too. A vertical move raises its absolute levels with it.
    /// </summary>
    public static void Carry(JsonObject surface, Placement2D t, int corners)
    {
        static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));
        void One(JsonObject o, int n)
        {
            foreach (var k in new[] { "at", "to", "a", "b", "slopeOrigin" })
                foreach (var x in Items(o, k)) if (Pt(x.Node) is { } p) x.Set(J(t.Point(p)));
            if (Json.Number(o["slopeDirection"]) is { } dir)
                o["slopeDirection"] = Math.Round(t.Angle(dir * Math.PI / 180) * 180 / Math.PI, 9);
            if (Math.Abs(t.Rise) > 0)
                foreach (var list in new[] { "points", "lines" })
                    foreach (var item in (o[list] as JsonArray ?? []).OfType<JsonObject>())
                        if (item["level"] is JsonArray lv) item["level"] = new JsonArray([.. lv.Select(v => (JsonNode?)(Json.Number(v) + t.Rise))]);
                        else if (Json.Number(item["level"]) is { } l) item["level"] = l + t.Rise;
            if (!t.Mirrored) return;
            foreach (var e in (o["edges"] as JsonArray ?? []).OfType<JsonObject>())
                if (Json.Number(e["edge"]) is { } i && n > 0) e["edge"] = ((n - 2 - (int)i) % n + n) % n;
            if (o["cornerZ"] is JsonArray cz) o["cornerZ"] = new JsonArray([.. cz.Reverse().Select(v => v?.DeepClone())]);
        }
        One(surface, corners);
        foreach (var r in (surface["regions"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var loop = Loop(r["outline"]);
            if (loop.Count >= 3)
            {
                var moved = loop.Select(t.Point).ToList();
                if (t.Mirrored) moved.Reverse();
                r["outline"] = new JsonArray([.. moved.Select(p => (JsonNode)J(p))]);
            }
            One(r, loop.Count);
        }
    }

    /// <summary>Every point-valued entry under a key: the object's own, and each item's in its lists (points, lines, drains).</summary>
    static IEnumerable<(JsonNode? Node, Action<JsonNode> Set)> Items(JsonObject o, string key)
    {
        if (o.ContainsKey(key)) yield return (o[key], v => o[key] = v);
        foreach (var list in new[] { "points", "lines", "drains" })
            foreach (var item in (o[list] as JsonArray ?? []).OfType<JsonObject>())
                if (item.ContainsKey(key)) { var it = item; yield return (it[key], v => it[key] = v); }
    }

    /// <summary>
    /// The edge slopes and corners' heights of a <c>surface</c> kept on the edges an outline edit keeps: an edge goes to the new
    /// edge on the same line that overlaps it (the first one), a corner's height to the corner at the same place; the rest go.
    /// </summary>
    public static void RemapEdges(JsonObject surface, IReadOnlyList<Vec2> was, IReadOnlyList<Vec2> now)
    {
        if (surface["edges"] is JsonArray edges)
        {
            var kept = new JsonArray();
            foreach (var e in edges.OfType<JsonObject>())
            {
                if (Json.Number(e["edge"]) is not { } ix || ix < 0 || ix >= was.Count) continue;
                var (a, b) = (was[(int)ix], was[((int)ix + 1) % was.Count]);
                var to = -1;
                for (var j = 0; j < now.Count && to < 0; j++)
                {
                    var (c, d) = (now[j], now[(j + 1) % now.Count]);
                    if (SameLine(a, b, c, d)) to = j;
                }
                if (to < 0) continue;
                var copy = (JsonObject)e.DeepClone();
                copy["edge"] = to;
                kept.Add((JsonNode)copy);
            }
            if (kept.Count > 0) surface["edges"] = kept; else surface.Remove("edges");
        }
        if (surface["cornerZ"] is JsonArray cz)
        {
            var next = new JsonArray();
            foreach (var p in now)
            {
                var k = -1;
                for (var i = 0; i < was.Count && k < 0; i++) if ((was[i] - p).Length < 1e-6) k = i;
                next.Add(k >= 0 && k < cz.Count ? cz[k]?.DeepClone() : (JsonNode?)null);
            }
            surface["cornerZ"] = next;
        }
    }

    static bool SameLine(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        var u = b - a;
        var len = u.Length;
        if (len < 1e-9 || (d - c).Length < 1e-9) return false;
        // Both ends of the new edge on the old edge's line, the same way, and overlapping it.
        if (Math.Abs(Vec2.Cross(u, c - a)) / len > 1e-6 || Math.Abs(Vec2.Cross(u, d - a)) / len > 1e-6) return false;
        if (Vec2.Dot(u, d - c) <= 0) return false;
        double s0 = Vec2.Dot(c - a, u) / (len * len), s1 = Vec2.Dot(d - a, u) / (len * len);
        return Math.Min(s1, 1) - Math.Max(s0, 0) > 1e-6;
    }

    /// <summary>
    /// What of a <c>surface</c> goes with one part of a split element (<paramref name="on"/>: whether a plan point is on its side):
    /// the points and drains on it, the lines on it cut where they leave it (their heights there kept), the regions whose middle
    /// is on it, the edge slopes on the edges it keeps; the plane stays the same plane, so the parts meet as before.
    /// </summary>
    public static JsonObject Share(JsonObject surface, Func<Vec2, bool> on, IReadOnlyList<Vec2> was, IReadOnlyList<Vec2> now)
    {
        var o = (JsonObject)surface.DeepClone();
        foreach (var list in new[] { "points", "drains" })
            if (o[list] is JsonArray arr)
                o[list] = new JsonArray([.. arr.OfType<JsonObject>().Where(x => x["centre"] != null && x["at"] == null || Pt(x["at"]) is { } p && on(p) && (Pt(x["to"]) is not { } q || on(q))).Select(x => (JsonNode)x.DeepClone())]);
        if (o["lines"] is JsonArray lines)
        {
            var kept = new JsonArray();
            foreach (var x in lines.OfType<JsonObject>())
            {
                if (Pt(x["a"]) is not { } a || Pt(x["b"]) is not { } b) continue;
                var (ia, ib) = (on(a), on(b));
                if (!ia && !ib) continue;
                var copy = (JsonObject)x.DeepClone();
                if (!(ia && ib))
                {
                    // Where it leaves this part, by halving: its height there as the line has it.
                    double lo = 0, hi = 1;
                    for (var k = 0; k < 60; k++) { var mid = (lo + hi) / 2; if (on(a + (b - a) * mid) == ia) lo = mid; else hi = mid; }
                    var cut = a + (b - a) * lo;
                    double[]? Lerp(double[]? v) => v == null ? null : ia ? [v[0], v[0] + (v[1] - v[0]) * lo] : [v[0] + (v[1] - v[0]) * lo, v[1]];
                    var key = (string)(copy["level"] != null ? "level" : "offset");
                    var vals = Lerp(Pair(copy[key]));
                    if (ia) copy["b"] = new JsonArray(cut.X, cut.Y); else copy["a"] = new JsonArray(cut.X, cut.Y);
                    if (vals != null) copy[key] = new JsonArray(vals[0], vals[1]);
                }
                kept.Add((JsonNode)copy);
            }
            o["lines"] = kept;
        }
        if (o["regions"] is JsonArray regs)
            o["regions"] = new JsonArray([.. regs.OfType<JsonObject>().Where(r => Loop(r["outline"]) is { Count: >= 3 } l ? on(Polygon2.Centroid(l)) : true).Select(r => (JsonNode)r.DeepClone())]);
        RemapEdges(o, was, now);
        return o;
    }
}
