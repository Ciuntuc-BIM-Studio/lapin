using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// Grading (docs/plan-site-grading.md, T5; forum 230 §4): an element of its own, <see cref="Class"/>, that shapes the ground from
/// edges chosen in space, live: its edges are read from what they follow every time (a hardscape's top edges, a feature line, a
/// platform's outline, a path's, wall's or ditch's line, or points of its own with typed heights), so when a bordering hardscape
/// or line changes the grading follows in the same undo step (the terrain's derived lines, <see cref="FeatureLines.FollowUp"/>).
/// <list type="bullet">
/// <item><b>Grading between edges</b> (<c>patch</c>): two or more edges, joined round by straight connectors into a closed boundary,
/// its surface triangulated from those 3D edges only, so their heights hold exactly: a ruled surface between two edges
/// (<c>loft</c>: straight rulings matched by the fraction of each edge's length) or the constrained triangulation of the boundary
/// (<c>tin</c>). A closed hardscape edge contributes the stretch of it that faces the other edges, or the stretch asked.</item>
/// <item><b>Slope to the ground</b> (<c>slope</c>): from one edge, on one side, a slope at 1 : N in cut and 1 : M in fill (or over a
/// width, or down/up to a level) until it meets the ground; the daylight line solved exactly on the ground's triangles, ray by ray;
/// a radial fan at a convex corner, the two planes' meeting line (the bisector) at a concave one; where a ray does not meet the
/// ground within the maximum width, the slope stops there and says so.</item>
/// </list>
/// On a terrain it is part of the ground: its lines are the terrain's breaklines and the survey points inside it leave the
/// triangulation (as a hardscape's, T4), so the ground there is its surface; without one it is a surface of its own (a patch; a
/// slope needs a ground). Its material (grass) covers it; its cut and fill are measured against the existing ground.
/// IFC: part of the terrain's geometry, and an IfcEarthworksFill or IfcEarthworksCut (4.3) by which of the two it mostly is.
/// </summary>
public static class Gradings
{
    public const string Class = "LapinGrading";
    public const string Color = "#93c45f";
    public const string TerrainKey = "terrain";
    public const string Patch = "patch";
    public const string Slope = "slope";
    public const string Material = "Grass";
    /// <summary>How near a survey point may lie to a grading's line before it leaves the triangulation (m).</summary>
    public const double Tolerance = 0.05;
    public const double DefaultCut = 2;
    public const double DefaultFill = 3;
    public const double DefaultMaxWidth = 20;
    /// <summary>The longest stretch of a slope's edge between two rays (m).</summary>
    public const double DefaultSpacing = 1;
    /// <summary>The widest angle between two rays of a convex corner's fan (degrees).</summary>
    public const double FanStep = 10;
    public const string LineColor = "#4f7a35";

    public static readonly string[] Surfaces = ["auto", "loft", "tin"];
    public static readonly string[] Targets = ["ground", "width", "level"];
    public static readonly string[] Sides = ["out", "in", "left", "right"];

    public static bool Is(Entity e) => e.Class == Class;

    /// <summary>The gradings on a terrain.</summary>
    public static IEnumerable<Entity> Of(IModelView m, string terrainId) => m.OfClass(Class).Where(e => e.Str(TerrainKey) == terrainId);

    /// <summary>The gradings with an edge that follows an element.</summary>
    public static IEnumerable<Entity> Following(IModelView m, string id) =>
        m.OfClass(Class).Where(g => EdgeArgs(g).Any(a => (string?)a["id"] == id));

    public static Entity? TerrainOf(IModelView m, Entity e) => m.Get(e.Str(TerrainKey) ?? "") is { } t && Terrains.IsTerrain(t) ? t : null;

    // ─── Edges ──────────────────────────────────────────────────────────

    /// <summary>
    /// An edge as it is now, in model heights: its points (a closed one's first not repeated), whether it is closed, what it follows
    /// (null: points of its own), its name in messages.
    /// </summary>
    public sealed record EdgeLine(List<Vec3> Points, bool Closed, string? Source, string Name)
    {
        public bool Own => Source == null;
    }

    static List<JsonObject> EdgeArgs(Entity e) => (e.Attrs["edges"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];

    static bool Same(Vec2 a, Vec2 b) => (a - b).Length <= 1e-6;

    /// <summary>The element an edge follows read as a line in space (model heights), closed or not; null with a reason when it is none of them.</summary>
    static (List<Vec3> Points, bool Closed)? LineOf(IModelView m, Entity x, List<string> problems, string name)
    {
        if (Hardscapes.Is(x))
            return Hardscapes.Corners(x).Count >= 3 ? (Hardscapes.Breakline(x), true) : null;
        if (FeatureLines.Is(x))
        {
            var (line, _, probs) = FeatureLines.Resolve(m, x);
            if (probs.Count > 0 || line.Count < 2) { problems.Add($"{name}: {x.Name} has a corner without a height."); return null; }
            var closed = FeatureLines.SpecOf(x).Closed;
            var pts = line.ToList();
            if (closed && pts.Count > 1 && Same(pts[0].Xy, pts[^1].Xy)) pts.RemoveAt(pts.Count - 1);
            return (pts, closed);
        }
        if (x.Class == IfcClass.EarthworksCut && !Ditches.Is(x))
        {
            var o = ArcOutline.BuiltOf(x);
            var z = x.Num("elevation");
            return o.Count >= 3 ? ([.. Polygon2.EnsureCcw(o).Select(p => p.At(z))], true) : null;
        }
        if (Corridors.Is(x))
        {
            var l = LineProfiles.Of(m, x);
            var pts = new List<Vec3>();
            for (var i = 0; i < l.Path.Count; i++) if (l.ZAt(l.Stations[i]) is { } z) pts.Add(l.Path[i].At(z));
            return pts.Count >= 2 ? (pts, false) : null;
        }
        problems.Add($"{name}: a grading edge follows a hardscape, a feature line, a platform, or a path's, wall's or ditch's line.");
        return null;
    }

    /// <summary>The edges as they are now, each one's stretch taken (a closed one facing the others, or between the points asked).</summary>
    public static List<EdgeLine> Edges(IModelView m, Entity e, Func<Vec2, double?> ground, List<string> problems)
    {
        var args = EdgeArgs(e);
        var raw = new List<(EdgeLine Line, JsonObject Arg)>();
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            var name = $"Edge {i + 1}";
            if ((string?)a["id"] is { Length: > 0 } id)
            {
                if (m.Get(id) is not { } x) { problems.Add($"{name}: what it followed is gone."); continue; }
                if (LineOf(m, x, problems, name) is not { } l) continue;
                raw.Add((new EdgeLine(l.Points, l.Closed, id, $"{name} ({x.Name})"), a));
            }
            else
            {
                var (corners, zs) = FeatureLines.ReadPoints(a["points"]);
                if (corners.Count < 2) { problems.Add($"{name}: it needs two points at least."); continue; }
                var pts = new List<Vec3>();
                var ok = true;
                for (var k = 0; k < corners.Count; k++)
                {
                    var z = zs[k] ?? ground(corners[k]);
                    if (z == null) { problems.Add($"{name}: point {k + 1} has no elevation of its own and no terrain under it: give it one."); ok = false; break; }
                    pts.Add(corners[k].At(z.Value));
                }
                if (!ok) continue;
                var closed = a["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c;
                raw.Add((new EdgeLine(pts, closed, null, name), a));
            }
        }
        var o = new List<EdgeLine>();
        for (var i = 0; i < raw.Count; i++)
        {
            var (line, a) = raw[i];
            if (!line.Closed) { o.Add(line); continue; }
            if (Vec(a["from"]) is { } f && Vec(a["to"]) is { } t) o.Add(line with { Points = Stretch(line.Points, f, t), Closed = false });
            else if (raw.Count > 1 && (string?)e.Attrs["kind"] == Patch)
            {
                var others = raw.Where((_, k) => k != i).SelectMany(x => x.Line.Points).Select(p => p.Xy).ToList();
                o.Add(line with { Points = Facing(line.Points, others), Closed = false });
            }
            else o.Add(line);
        }
        return o;
    }

    static Vec2? Vec(JsonNode? n) => n is JsonArray { Count: >= 2 } q && Json.Number(q[0]) is { } x && Json.Number(q[1]) is { } y ? new Vec2(x, y) : null;

    /// <summary>The stations round a closed polyline (its closing segment included), the last the whole length.</summary>
    static double[] RingStations(IReadOnlyList<Vec3> ring)
    {
        var st = new double[ring.Count + 1];
        for (var i = 0; i < ring.Count; i++) st[i + 1] = st[i] + (ring[(i + 1) % ring.Count].Xy - ring[i].Xy).Length;
        return st;
    }

    /// <summary>Where a point projects onto a closed polyline: its station round it.</summary>
    static double Project(IReadOnlyList<Vec3> ring, double[] st, Vec2 p)
    {
        var best = (D: double.MaxValue, S: 0.0);
        for (var i = 0; i < ring.Count; i++)
        {
            var (a, b) = (ring[i].Xy, ring[(i + 1) % ring.Count].Xy);
            var d = b - a;
            var len2 = Vec2.Dot(d, d);
            var t = len2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p - a, d) / len2, 0, 1);
            var dist = (a + d * t - p).Length;
            if (dist < best.D) best = (dist, st[i] + t * (st[i + 1] - st[i]));
        }
        return best.S;
    }

    static Vec3 RingAt(IReadOnlyList<Vec3> ring, double[] st, double s)
    {
        var total = st[^1];
        s = ((s % total) + total) % total;
        for (var i = 0; i < ring.Count; i++)
            if (s <= st[i + 1] + 1e-12)
            {
                var len = st[i + 1] - st[i];
                var t = len < 1e-12 ? 0 : (s - st[i]) / len;
                var (a, b) = (ring[i], ring[(i + 1) % ring.Count]);
                return a + (b - a) * t;
            }
        return ring[0];
    }

    /// <summary>The stretch of a closed polyline from one point to another, forwards round it (its direction), with its ends where they project.</summary>
    public static List<Vec3> Stretch(IReadOnlyList<Vec3> ring, Vec2 from, Vec2 to)
    {
        var st = RingStations(ring);
        var total = st[^1];
        var s0 = Project(ring, st, from);
        var s1 = Project(ring, st, to);
        if (s1 <= s0 + 1e-9) s1 += total;
        var o = new List<Vec3> { RingAt(ring, st, s0) };
        for (var k = 0; k < 2 * ring.Count; k++)
        {
            var i = k % ring.Count;
            var s = st[i] + (k >= ring.Count ? total : 0);
            if (s > s0 + 1e-9 && s < s1 - 1e-9) o.Add(ring[i]);
        }
        var end = RingAt(ring, st, s1);
        if (!Same(end.Xy, o[^1].Xy)) o.Add(end);
        return o;
    }

    /// <summary>
    /// The stretch of a closed edge that faces the others: its points whose outward normal looks towards the nearest other point
    /// (within 80°) by a line that does not pass through it, the longest such run round it.
    /// </summary>
    public static List<Vec3> Facing(IReadOnlyList<Vec3> ring, IReadOnlyList<Vec2> others)
    {
        var n = ring.Count;
        var flat = ring.Select(p => p.Xy).ToList();
        var ccw = Polygon2.SignedArea(flat) > 0;
        var faces = new bool[n];
        for (var i = 0; i < n; i++)
        {
            var p = flat[i];
            var q = others.MinBy(o => (o - p).Length);
            var dir = q - p;
            if (dir.Length < 1e-9) { faces[i] = true; continue; }
            dir = dir.Normalized();
            var (a, b) = ((p - flat[(i - 1 + n) % n]).Normalized(), (flat[(i + 1) % n] - p).Normalized());
            var t = (a + b).Length > 1e-9 ? (a + b).Normalized() : a;
            var outward = ccw ? new Vec2(t.Y, -t.X) : new Vec2(-t.Y, t.X);
            if (Vec2.Dot(outward, dir) < Math.Cos(80 * Math.PI / 180)) continue;
            var blocked = false;
            for (var k = 0; k < n && !blocked; k++)
            {
                if (k == i || (k + 1) % n == i) continue;
                blocked = Polyline2.Crosses(p, q, flat[k], flat[(k + 1) % n]);
            }
            faces[i] = !blocked;
        }
        if (faces.All(f => f) || !faces.Any(f => f)) return [.. ring];
        // The longest run round the ring.
        var (bestStart, bestLen) = (0, 0);
        for (var i = 0; i < n; i++)
        {
            if (!faces[i] || faces[(i - 1 + n) % n]) continue;
            var len = 0;
            while (len < n && faces[(i + len) % n]) len++;
            if (len > bestLen) (bestStart, bestLen) = (i, len);
        }
        return [.. Enumerable.Range(0, bestLen).Select(k => ring[(bestStart + k) % n])];
    }

    // ─── The patch ──────────────────────────────────────────────────────

    /// <summary>A grading's shape as it is now: its boundary (a patch) or its strips (a slope), its rulings, its own surface, what is wrong.</summary>
    public sealed record Shape(string Kind, List<Vec3> Ring, List<List<Vec3>> Rulings, List<Strip> Strips, Tin? Surface, List<string> Problems, List<EdgeLine> Edges, string Mode)
    {
        /// <summary>The plan areas it covers, each a simple polygon (a closed slope in two halves).</summary>
        public List<List<Vec2>> Areas()
        {
            if (Kind == Patch) return Ring.Count >= 3 ? [[.. Ring.Select(p => p.Xy)]] : [];
            var o = new List<List<Vec2>>();
            foreach (var s in Strips)
            {
                if (!s.Closed) { o.Add(s.Polygon(0, s.Origins.Count - 1)); continue; }
                var half = s.Origins.Count / 2;
                o.Add(s.Polygon(0, half));
                o.Add(s.Polygon(half, s.Origins.Count));
            }
            return o.Where(a => a.Count >= 3).ToList();
        }

        /// <summary>Its lines in space (model heights) as the ground is to hold them: the boundary or the edge and daylight lines, the rulings or rays.</summary>
        public List<List<Vec3>> Lines()
        {
            var o = new List<List<Vec3>>();
            if (Kind == Patch)
            {
                if (Ring.Count >= 3) o.Add([.. Ring, Ring[0]]);
                o.AddRange(Rulings);
                return o;
            }
            foreach (var s in Strips)
            {
                var top = Dedup(s.Origins);
                if (s.Closed && top.Count > 2) top.Add(top[0]);
                var day = s.Ends.ToList();
                if (s.Closed) day.Add(day[0]);
                if (top.Count >= 2) o.Add(top);
                o.Add(day);
                for (var i = 0; i < s.Origins.Count; i++)
                    if ((s.Ends[i].Xy - s.Origins[i].Xy).Length > 1e-6) o.Add([s.Origins[i], s.Ends[i]]);
            }
            return o;
        }

        public double PlanArea => Surface == null ? 0 : Terrains.PlanArea(Surface);
    }

    /// <summary>A run of a slope's rays: ray i from <see cref="Origins"/>[i] on the edge to <see cref="Ends"/>[i] on the daylight line, cut or fill.</summary>
    public sealed record Strip(List<Vec3> Origins, List<Vec3> Ends, List<bool> Cut, bool Closed)
    {
        /// <summary>The plan polygon between rays <paramref name="from"/> and <paramref name="to"/> (to may be the count: back to the first).</summary>
        public List<Vec2> Polygon(int from, int to)
        {
            var n = Origins.Count;
            var top = new List<Vec2>();
            var bottom = new List<Vec2>();
            for (var k = from; k <= to; k++)
            {
                var i = k % n;
                if (top.Count == 0 || !Same(top[^1], Origins[i].Xy)) top.Add(Origins[i].Xy);
                bottom.Add(Ends[i].Xy);
            }
            bottom.Reverse();
            var poly = top.Concat(bottom).ToList();
            var clean = new List<Vec2>();
            foreach (var p in poly) if (clean.Count == 0 || !Same(clean[^1], p)) clean.Add(p);
            if (clean.Count > 1 && Same(clean[0], clean[^1])) clean.RemoveAt(clean.Count - 1);
            return [.. Polygon2.EnsureCcw(clean)];
        }
    }

    static List<Vec3> Dedup(IEnumerable<Vec3> pts)
    {
        var o = new List<Vec3>();
        foreach (var p in pts) if (o.Count == 0 || !Same(o[^1].Xy, p.Xy)) o.Add(p);
        return o;
    }

    static double PlanLength(IReadOnlyList<Vec3> pts)
    {
        double l = 0;
        for (var i = 1; i < pts.Count; i++) l += (pts[i].Xy - pts[i - 1].Xy).Length;
        return l;
    }

    static Vec3 AlongFraction(IReadOnlyList<Vec3> pts, double u)
    {
        var total = PlanLength(pts);
        var s = Math.Clamp(u, 0, 1) * total;
        double acc = 0;
        for (var i = 1; i < pts.Count; i++)
        {
            var len = (pts[i].Xy - pts[i - 1].Xy).Length;
            if (acc + len >= s - 1e-12 && len > 1e-12) return pts[i - 1] + (pts[i] - pts[i - 1]) * Math.Clamp((s - acc) / len, 0, 1);
            acc += len;
        }
        return pts[^1];
    }

    /// <summary>
    /// The edges joined round into a boundary: each in turn the way round that starts nearest the end of the one before (for two or
    /// three, every way tried, the simple one with the shortest joins kept), joined by straight connectors in space.
    /// </summary>
    static (List<Vec3> Ring, List<int> EdgeOf)? Join(List<EdgeLine> edges)
    {
        if (edges.Count == 1 && edges[0].Closed) return (Dedup(edges[0].Points), [.. edges[0].Points.Select(_ => 0)]);
        var lines = edges.Select(x => x.Points).ToList();
        (List<Vec3>, List<int>)? best = null;
        var bestJoin = double.MaxValue;
        var combos = lines.Count <= 3 ? 1 << lines.Count : 1;
        for (var mask = 0; mask < combos; mask++)
        {
            var ring = new List<Vec3>();
            var of = new List<int>();
            for (var i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                bool reverse;
                if (lines.Count <= 3) reverse = (mask & (1 << i)) != 0;
                else reverse = i > 0 && (l[^1].Xy - ring[^1].Xy).Length < (l[0].Xy - ring[^1].Xy).Length;
                foreach (var p in reverse ? Enumerable.Reverse(l) : l)
                {
                    if (ring.Count > 0 && Same(ring[^1].Xy, p.Xy)) continue;
                    ring.Add(p);
                    of.Add(i);
                }
            }
            if (ring.Count > 1 && Same(ring[0].Xy, ring[^1].Xy)) { ring.RemoveAt(ring.Count - 1); of.RemoveAt(of.Count - 1); }
            if (ring.Count < 3) continue;
            var flat = ring.Select(p => p.Xy).ToList();
            if (Math.Abs(Polygon2.SignedArea(flat)) < 1e-6 || !ArcOutline.Simple(flat)) continue;
            double join = 0;
            for (var i = 0; i < ring.Count; i++) if (of[i] != of[(i + 1) % ring.Count]) join += (ring[(i + 1) % ring.Count].Xy - ring[i].Xy).Length;
            if (join < bestJoin - 1e-9) { bestJoin = join; best = (ring, of); }
        }
        if (best is not { } b) return null;
        if (Polygon2.SignedArea(b.Item1.Select(p => p.Xy).ToList()) < 0) { b.Item1.Reverse(); b.Item2.Reverse(); }
        return b;
    }

    /// <summary>
    /// The rulings of a ruled surface between two edges as they run round the boundary (the second runs back along the first): from
    /// every inner point of each, a straight line in space to the point at the same fraction of the other's length; none crossing
    /// another or the boundary, else null.
    /// </summary>
    static List<List<Vec3>>? Rulings(List<Vec3> a, List<Vec3> b, List<Vec3> ring)
    {
        var o = new List<List<Vec3>>();
        var (la, lb) = (PlanLength(a), PlanLength(b));
        if (la < 1e-9 || lb < 1e-9) return null;
        var seen = new HashSet<(long, long, long, long)>();
        void Add(Vec3 p, Vec3 q)
        {
            if ((p.Xy - q.Xy).Length < 1e-6) return;
            var k = ((long)Math.Round(p.X * 1e5), (long)Math.Round(p.Y * 1e5), (long)Math.Round(q.X * 1e5), (long)Math.Round(q.Y * 1e5));
            if (seen.Add(k)) o.Add([p, q]);
        }
        double acc = 0;
        for (var i = 1; i + 1 < a.Count; i++) { acc += (a[i].Xy - a[i - 1].Xy).Length; Add(a[i], AlongFraction(b, 1 - acc / la)); }
        acc = 0;
        for (var j = 1; j + 1 < b.Count; j++) { acc += (b[j].Xy - b[j - 1].Xy).Length; Add(AlongFraction(a, 1 - acc / lb), b[j]); }
        var flat = ring.Select(p => p.Xy).ToList();
        foreach (var r in o)
        {
            var (p, q) = (r[0].Xy, r[1].Xy);
            if (!Polygon2.Contains(flat, (p + q) * 0.5)) return null;
            for (var k = 0; k < flat.Count; k++)
                if (Polyline2.Crosses(p, q, flat[k], flat[(k + 1) % flat.Count])) return null;
        }
        for (var i = 0; i < o.Count; i++)
            for (var j = i + 1; j < o.Count; j++)
                if (Polyline2.Crosses(o[i][0].Xy, o[i][1].Xy, o[j][0].Xy, o[j][1].Xy)) return null;
        return o;
    }

    static Shape PatchShape(IModelView m, Entity e, Func<Vec2, double?> ground)
    {
        var problems = new List<string>();
        var edges = Edges(m, e, ground, problems);
        var mode = e.Str("surface") is { } sm && Surfaces.Contains(sm) ? sm : "auto";
        if (edges.Count == 0) return new Shape(Patch, [], [], [], null, problems.Count > 0 ? problems : ["A grading between edges needs two edges at least."], edges, mode);
        if (Join(edges) is not { } joined)
            return new Shape(Patch, [], [], [], null, [.. problems, "The edges do not close round an area: their joins would cross. Pick them in order round the patch, or pick the stretches that face each other."], edges, mode);
        var (ring, of) = joined;
        var rulings = new List<List<Vec3>>();
        var used = "tin";
        if (mode != "tin" && edges.Count == 2 && !edges.Any(x => x.Closed))
        {
            List<Vec3> Part(int k) => [.. ring.Where((_, i) => of[i] == k)];
            if (Rulings(Part(0), Part(1), ring) is { } r) { rulings = r; used = "loft"; }
            else if (mode == "loft") problems.Add("The two edges are too unlike for a ruled surface (its rulings would cross): the patch is triangulated from its edges instead.");
        }
        else if (mode == "loft") problems.Add("A ruled surface runs between two open edges: with more, the patch is triangulated from its edges.");
        var pts = new List<Vec3>(ring);
        var segs = new List<(int, int)>();
        for (var i = 0; i < ring.Count; i++) segs.Add((i, (i + 1) % ring.Count));
        foreach (var r in rulings)
        {
            pts.Add(r[0]); pts.Add(r[1]);
            segs.Add((pts.Count - 2, pts.Count - 1));
        }
        var tin = Tin.Delaunay(pts, segs, ring.Select(p => p.Xy).ToList());
        return new Shape(Patch, ring, rulings, [], tin.Triangles.Count > 0 ? tin : null, problems, edges, used);
    }

    // ─── The slope ──────────────────────────────────────────────────────

    /// <summary>What a slope asks: its side, its cut and fill (1 : N), its target (the ground, a width, a level), its widest reach, its rays' spacing.</summary>
    public sealed record SlopeSpec(string Side, double Cut, double Fill, string Target, double? Width, double? Level, double MaxWidth, double Spacing)
    {
        public static SlopeSpec Of(Entity e) => new(
            e.Str("side") is { } s && Sides.Contains(s) ? s : "out",
            e.NumOrNull("cutSlope") is { } c && c > 0 ? c : DefaultCut,
            e.NumOrNull("fillSlope") is { } f && f > 0 ? f : DefaultFill,
            e.Str("target") is { } t && Targets.Contains(t) ? t : "ground",
            e.NumOrNull("width"), e.NumOrNull("level"),
            e.NumOrNull("maxWidth") is { } w && w > 0 ? w : DefaultMaxWidth,
            e.NumOrNull("spacing") is { } sp && sp > 0.05 ? sp : DefaultSpacing);
    }

    /// <summary>A ray of a slope: where it starts on the edge, its way in plan, how fast it climbs (cut) or falls (fill) per metre of it, its priority where two cross.</summary>
    readonly record struct Ray(Vec3 O, Vec2 Dir, double Factor, int Vertex, double Turn);

    /// <summary>The edge with a point every <paramref name="spacing"/> at most along each stretch (heights linear).</summary>
    static List<Vec3> Densify(IReadOnlyList<Vec3> pts, bool closed, double spacing)
    {
        var o = new List<Vec3>();
        var n = pts.Count;
        var segs = closed ? n : n - 1;
        for (var i = 0; i < segs; i++)
        {
            var (a, b) = (pts[i], pts[(i + 1) % n]);
            o.Add(a);
            var k = (int)Math.Ceiling((b.Xy - a.Xy).Length / spacing);
            for (var q = 1; q < k; q++) o.Add(a + (b - a) * ((double)q / k));
        }
        if (!closed) o.Add(pts[^1]);
        return o;
    }

    /// <summary>
    /// Where a ray from <paramref name="o"/> along <paramref name="dir"/> meets the ground, climbing <paramref name="up"/> per metre
    /// in cut (the ground above the edge) or falling <paramref name="down"/> in fill: solved exactly on the ground's triangles (the
    /// ground along the ray is straight between its crossings of their edges); null when it does not within <paramref name="max"/>
    /// or leaves the ground first. Its end and whether it cut.
    /// </summary>
    public static (Vec3 End, bool Cut)? Daylight(Terrain ground, Vec3 o, Vec2 dir, double up, double down, double max)
    {
        if (ground.ElevationAt(o.X, o.Y) is not { } g0) return null;
        if (Math.Abs(g0 - o.Z) <= 1e-9) return (o.Xy.At(g0), false);
        var cut = g0 > o.Z;
        var rate = cut ? up : -down;
        var path = new List<Vec2> { o.Xy, o.Xy + dir * max };
        var prev = (S: 0.0, F: g0 - o.Z);
        foreach (var s in FeatureLines.Crossings(ground, path).Where(s => s > 1e-12))
        {
            var p = o.Xy + dir * s;
            if (ground.ElevationAt(p.X, p.Y) is not { } g) return null;
            var f = g - (o.Z + rate * s);
            if (cut ? f <= 0 : f >= 0)
            {
                var t = Math.Abs(prev.F - f) < 1e-15 ? 1 : prev.F / (prev.F - f);
                var at = prev.S + t * (s - prev.S);
                return ((o.Xy + dir * at).At(o.Z + rate * at), cut);
            }
            prev = (s, f);
        }
        return null;
    }

    static Shape SlopeShape(IModelView m, Entity e, Terrain? ground)
    {
        var problems = new List<string>();
        var edges = Edges(m, e, p => ground?.ElevationAt(p.X, p.Y), problems);
        if (edges.Count == 0) return new Shape(Slope, [], [], [], null, problems.Count > 0 ? problems : ["A slope to the ground needs an edge."], edges, "");
        if (ground == null) return new Shape(Slope, [], [], [], null, [.. problems, "A slope to the ground needs a terrain under its edge."], edges, "");
        var spec = SlopeSpec.Of(e);
        var strips = new List<Strip>();
        foreach (var edge in edges.Take(1))
        {
            var closed = edge.Closed && edge.Points.Count >= 3;
            var raw = Dedup(edge.Points);
            if (closed && raw.Count > 1 && Same(raw[0].Xy, raw[^1].Xy)) raw.RemoveAt(raw.Count - 1);
            // A closed edge runs anticlockwise: outside is on its right.
            if (closed && Polygon2.SignedArea(raw.Select(p => p.Xy).ToList()) < 0) raw.Reverse();
            var pts = Densify(raw, closed, spec.Spacing);
            var right = spec.Side switch { "left" => false, "right" => true, "in" => false, _ => true };
            if (!closed && spec.Side is "out" or "in") right = spec.Side == "out";
            var rays = Rays(pts, closed, right);
            strips.AddRange(Solve(rays, closed, ground, spec, problems, edge.Name));
        }
        // Its own surface: each strip's quads between its rays, in two triangles (one where two rays leave the same corner).
        var points = new List<Vec3>();
        var tris = new List<(int, int, int)>();
        foreach (var s in strips)
        {
            var n = s.Origins.Count;
            var count = s.Closed ? n : n - 1;
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % n;
                var (o0, o1, d0, d1) = (s.Origins[i], s.Origins[j], s.Ends[i], s.Ends[j]);
                void Tri(Vec3 a, Vec3 b, Vec3 c)
                {
                    var area = Vec2.Cross(b.Xy - a.Xy, c.Xy - a.Xy);
                    if (Math.Abs(area) < 1e-10) return;
                    var at = points.Count;
                    points.Add(a); points.Add(b); points.Add(c);
                    tris.Add(area > 0 ? (at, at + 1, at + 2) : (at, at + 2, at + 1));
                }
                if (!Same(o0.Xy, o1.Xy)) Tri(o0, o1, d1);
                Tri(o0, d1, d0);
            }
        }
        var tin = tris.Count > 0 ? Weld(points, tris) : null;
        if (tin == null && problems.Count == 0) problems.Add("The slope meets the ground right at its edge: there is nothing to grade.");
        return new Shape(Slope, [], [], strips, tin, problems, edges, spec.Target);
    }

    static Tin Weld(List<Vec3> points, List<(int A, int B, int C)> tris)
    {
        var at = new Dictionary<(long, long), int>();
        var pts = new List<Vec3>();
        int W(int i)
        {
            var p = points[i];
            var k = ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6));
            if (!at.TryGetValue(k, out var v)) { v = pts.Count; at[k] = v; pts.Add(p); }
            return v;
        }
        var o = new List<(int, int, int)>();
        foreach (var (a, b, c) in tris) { var (x, y, z) = (W(a), W(b), W(c)); if (x != y && y != z && x != z) o.Add((x, y, z)); }
        return new Tin(pts, o);
    }

    /// <summary>
    /// The rays from an edge's points on one side: square to it at a straight point; at a convex corner a fan, every
    /// <see cref="FanStep"/>° at most, each a cone's line (its full slope); at a concave corner, or a slight bend, along the
    /// bisector, where the two planes meet (slope × cos of half the angle between their normals).
    /// </summary>
    static List<Ray> Rays(List<Vec3> pts, bool closed, bool right)
    {
        var o = new List<Ray>();
        var n = pts.Count;
        Vec2 Normal(Vec2 t) => right ? new Vec2(t.Y, -t.X) : new Vec2(-t.Y, t.X);
        for (var i = 0; i < n; i++)
        {
            Vec2? tin = closed || i > 0 ? (pts[i].Xy - pts[(i - 1 + n) % n].Xy) : null;
            Vec2? tout = closed || i < n - 1 ? (pts[(i + 1) % n].Xy - pts[i].Xy) : null;
            if (tin is { } a && a.Length < 1e-12) tin = null;
            if (tout is { } b && b.Length < 1e-12) tout = null;
            if (tin == null && tout == null) continue;
            var nin = tin is { } u ? Normal(u.Normalized()) : Normal(tout!.Value.Normalized());
            var nout = tout is { } w ? Normal(w.Normalized()) : nin;
            var turn = Vec2.Cross(tin?.Normalized() ?? nin, tout?.Normalized() ?? nin);
            // A left turn has its right side outside (convex), a right turn its left.
            var convex = right ? turn > 1e-12 : turn < -1e-12;
            var angle = Math.Acos(Math.Clamp(Vec2.Dot(nin, nout), -1, 1));
            if (convex && angle > FanStep * Math.PI / 180 * 0.999)
            {
                var steps = (int)Math.Ceiling(angle / (FanStep * Math.PI / 180));
                var a0 = Math.Atan2(nin.Y, nin.X);
                var sign = Vec2.Cross(nin, nout) >= 0 ? 1 : -1;
                for (var k = 0; k <= steps; k++)
                {
                    var th = a0 + sign * angle * k / steps;
                    o.Add(new Ray(pts[i], new Vec2(Math.Cos(th), Math.Sin(th)), 1, i, 0));
                }
                continue;
            }
            var bis = nin + nout;
            if (bis.Length < 1e-9) bis = nin;
            bis = bis.Normalized();
            o.Add(new Ray(pts[i], bis, Math.Max(1e-6, Vec2.Dot(bis, nin)), i, convex ? 0 : angle));
        }
        return o;
    }

    /// <summary>
    /// Each ray solved to its target; where two cross before their ends (a concave bend wider than its curve), the sharper corner's
    /// bisector holds and the other goes; the rays that reach nothing break the slope into runs, named in the problems.
    /// </summary>
    static List<Strip> Solve(List<Ray> rays, bool closed, Terrain ground, SlopeSpec spec, List<string> problems, string name)
    {
        var ends = new (Vec3 End, bool Cut)?[rays.Count];
        for (var i = 0; i < rays.Count; i++)
        {
            var r = rays[i];
            var (up, down) = (r.Factor / spec.Cut, r.Factor / spec.Fill);
            switch (spec.Target)
            {
                case "width":
                {
                    var w = Math.Clamp(spec.Width ?? 3, 0.01, spec.MaxWidth) * (r.Factor < 1 ? 1 / r.Factor : 1);
                    var p = r.O.Xy + r.Dir * w;
                    ends[i] = ground.ElevationAt(p.X, p.Y) is { } g ? (p.At(g), g > r.O.Z) : null;
                    break;
                }
                case "level":
                {
                    var level = spec.Level ?? r.O.Z;
                    var cut = level > r.O.Z;
                    var span = Math.Abs(level - r.O.Z) / (cut ? up : down);
                    ends[i] = span <= spec.MaxWidth / Math.Max(r.Factor, 1e-6) ? ((r.O.Xy + r.Dir * span).At(level), cut) : null;
                    break;
                }
                default:
                    ends[i] = Daylight(ground, r.O, r.Dir, up, down, spec.MaxWidth / Math.Max(r.Factor, 1e-6));
                    break;
            }
        }
        // Crossing rays: kept in order of priority (the sharpest concave corner first, then the shortest), each kept only when it crosses none kept.
        var order = Enumerable.Range(0, rays.Count).Where(i => ends[i] != null)
            .OrderByDescending(i => rays[i].Turn).ThenBy(i => (ends[i]!.Value.End.Xy - rays[i].O.Xy).Length).ToList();
        var kept = new List<int>();
        var alive = new bool[rays.Count];
        var crossed = 0;
        foreach (var i in order)
        {
            var (a, b) = (rays[i].O.Xy, ends[i]!.Value.End.Xy);
            var clash = kept.Any(j =>
            {
                var (c, d) = (rays[j].O.Xy, ends[j]!.Value.End.Xy);
                if (Same(a, c)) return false;
                return Polyline2.Crosses(a, b, c, d);
            });
            if (clash) { crossed++; continue; }
            kept.Add(i);
            alive[i] = true;
        }
        if (crossed > 0) problems.Add($"{name}: {crossed} ray(s) of the slope meet another before the ground at a concave bend: left out, the slope there is the bend's.");
        // Runs of the rays that reach their target (those left out of a crossing are skipped, not breaks).
        var reached = Enumerable.Range(0, rays.Count).Select(i => ends[i] != null).ToArray();
        var failed = rays.Count - reached.Count(x => x);
        if (failed > 0)
        {
            var what = spec.Target switch { "level" => $"reach its level within {spec.MaxWidth:0.##} m", "width" => "find the ground at its width", _ => $"meet the ground within {spec.MaxWidth:0.##} m" };
            problems.Add($"{name}: the slope does not {what} at {failed} of its {rays.Count} ray(s) (it leaves the terrain, or the ground is too far): it stops there.");
        }
        var strips = new List<Strip>();
        if (failed == 0 && closed)
        {
            var ids = Enumerable.Range(0, rays.Count).Where(i => alive[i]).ToList();
            if (ids.Count >= 3) strips.Add(Make(ids, true));
            return strips;
        }
        // Start a closed edge's runs after a ray that failed, so a run is not cut at the ring's seam.
        var start = closed ? Array.FindIndex(reached, x => !x) + 1 : 0;
        var run = new List<int>();
        for (var k = 0; k < rays.Count; k++)
        {
            var i = (start + k) % rays.Count;
            if (!reached[i]) { if (run.Count >= 2) strips.Add(Make(run, false)); run = []; continue; }
            if (alive[i]) run.Add(i);
        }
        if (run.Count >= 2) strips.Add(Make(run, false));
        return strips;

        Strip Make(List<int> ids, bool ring) =>
            new([.. ids.Select(i => rays[i].O)], [.. ids.Select(i => ends[i]!.Value.End)], [.. ids.Select(i => ends[i]!.Value.Cut)], ring);
    }

    // ─── Its shape, on its terrain or alone ─────────────────────────────

    /// <summary>The grading as it is now: on its terrain's ground without its derived lines (what it is graded from), or alone.</summary>
    public static Shape ShapeOf(IModelView m, Entity e)
    {
        var terrain = TerrainOf(m, e);
        var ground = terrain != null ? Terrains.Base(terrain) : null;
        return (string?)e.Attrs["kind"] == Slope ? SlopeShape(m, e, ground) : PatchShape(m, e, p => ground?.ElevationAt(p.X, p.Y));
    }

    /// <summary>
    /// What a terrain holds of its gradings (<see cref="FeatureLines.Derived"/> items): each one's lines clipped to its ground, in
    /// surveyed heights, and its areas (marked <c>graded</c>: the ground there is its surface, its contours drawn on).
    /// </summary>
    public static IEnumerable<JsonObject> Derived(IModelView m, Entity terrain, Terrain ground, double datum)
    {
        foreach (var g in Of(m, terrain.Id).ToList())
        {
            var s = ShapeOf(m, g);
            if (s.Surface == null) continue;
            foreach (var line in s.Lines())
                foreach (var piece in FeatureLines.Clip(line, ground))
                    yield return new JsonObject
                    {
                        ["id"] = g.Id,
                        ["points"] = new JsonArray([.. piece.Select(p => (JsonNode)new JsonArray(p.X, p.Y, Math.Round(p.Z + datum, 9)))]),
                        ["tol"] = Tolerance,
                    };
            foreach (var area in s.Areas())
                yield return new JsonObject { ["id"] = g.Id, [FeatureLines.AreaKey] = new JsonArray([.. area.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]), [GradedKey] = true };
        }
    }

    /// <summary>A derived area that is graded ground, not covered by a hardscape: the survey points in it go, its contours stay.</summary>
    public const string GradedKey = "graded";

    // ─── Cut and fill ───────────────────────────────────────────────────

    /// <summary>
    /// Its cut and fill against a ground (m³): over each of its own triangles, split into small ones (a quarter of a metre or so,
    /// fewer on a big grading) each counted at its middle, the triangle's height there less the ground's; the parts off the ground
    /// count nothing.
    /// </summary>
    public static (double Cut, double Fill) Earthwork(Tin surface, Terrain existing)
    {
        double cut = 0, fill = 0;
        var area = Terrains.PlanArea(surface);
        var step = Math.Max(0.25, Math.Sqrt(area / 200_000));
        foreach (var (ia, ib, ic) in surface.Triangles)
        {
            var (a, b, c) = (surface.Points[ia], surface.Points[ib], surface.Points[ic]);
            var longest = Math.Max((b.Xy - a.Xy).Length, Math.Max((c.Xy - b.Xy).Length, (a.Xy - c.Xy).Length));
            var k = Math.Clamp((int)Math.Ceiling(longest / step), 1, 200);
            var whole = Math.Abs(Vec2.Cross(b.Xy - a.Xy, c.Xy - a.Xy)) / 2;
            var piece = whole / (k * k);
            for (var i = 0; i < k; i++)
                for (var j = 0; j < k - i; j++)
                {
                    // The upward and (when there) downward small triangles of row i, column j, by their middles in barycentric terms.
                    Count((i + 1.0 / 3) / k, (j + 1.0 / 3) / k);
                    if (j < k - i - 1) Count((i + 2.0 / 3) / k, (j + 2.0 / 3) / k);
                }
            void Count(double u, double v)
            {
                var p = a + (b - a) * u + (c - a) * v;
                if (existing.ElevationAt(p.X, p.Y) is not { } z) return;
                var dz = p.Z - z;
                if (dz > 0) fill += dz * piece; else cut += -dz * piece;
            }
        }
        return (cut, fill);
    }

    // ─── The element ────────────────────────────────────────────────────

    /// <summary>
    /// A grading seen: in its material, a skin over the ground it shapes (the terrain's own triangles there, so it lies on it) or,
    /// alone, over its own surface; its boundary or daylight lines in 3D; in plan its boundary or its daylight line (the slope's
    /// hachures and its material's hatch come with the plan's marks, at the drawing's scale).
    /// </summary>
    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices _)
    {
        var s = ShapeOf(m, e);
        var problems = s.Problems.ToList();
        if (s.Surface == null) return new ElementGeometry([], Diagnostics: problems.Count > 0 ? problems : ["The grading has no surface."]);
        var terrain = TerrainOf(m, e);
        var material = e.Str("material");
        var color = Materials.ColorOf(m, material) is var c && c != "#c9ced6" ? c : Color;
        Tin? skin = null;
        if (terrain != null)
        {
            var t = Terrains.Of(terrain);
            var parts = new List<Tin>();
            foreach (var area in s.Areas()) if (Terrains.SubregionSurface(t, area) is { } on) parts.Add(on);
            skin = parts.Count > 0 ? Merge(parts) : null;
        }
        skin ??= s.Surface;
        var list = new List<GeometryPart>();
        if (Terrains.Skin(skin, e.Name, color, material, "grading") is { } part) list.Add(part);
        var plan = new List<PlanItem>();
        var lines = new List<IReadOnlyList<Vec3>>();
        var snaps = new List<Vec2>();
        if (s.Kind == Patch)
        {
            var ring = s.Ring.Select(p => p.Xy).ToList();
            plan.Add(new PlanItem("symbol", true, ring, Style: new Lapin.Drawing.LineStyleSpec(1, LineColor, "solid")));
            lines.Add([.. s.Ring.Append(s.Ring[0]).Select(p => p with { Z = p.Z + 0.02 })]);
            snaps.AddRange(ring);
        }
        foreach (var st in s.Strips)
        {
            var day = st.Ends.Select(p => p.Xy).ToList();
            plan.Add(new PlanItem("symbol", st.Closed, day, Style: new Lapin.Drawing.LineStyleSpec(2, LineColor, "dashed")));
            var d3 = st.Ends.Select(p => p with { Z = p.Z + 0.02 }).ToList();
            if (st.Closed) d3.Add(d3[0]);
            lines.Add(d3);
            snaps.AddRange(day);
        }
        return new ElementGeometry(list, plan, snaps, problems.Count > 0 ? problems : null, Lines: lines, LineColor: LineColor);
    }

    static Tin Merge(List<Tin> parts)
    {
        var pts = new List<Vec3>();
        var tris = new List<(int, int, int)>();
        foreach (var p in parts)
        {
            var at = pts.Count;
            pts.AddRange(p.Points);
            tris.AddRange(p.Triangles.Select(t => (t.A + at, t.B + at, t.C + at)));
        }
        return Weld(pts, tris);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        var s = ShapeOf(m, e);
        if (s.Surface is not { } tin) yield break;
        double surface = 0;
        foreach (var (a, b, c) in tin.Triangles)
        {
            var (p, q, r) = (tin.Points[a], tin.Points[b], tin.Points[c]);
            surface += Vec3.Cross(q - p, r - p).Length / 2;
        }
        var zs = tin.Points;
        var list = new List<Quantity>
        {
            new("PlanArea", s.PlanArea, "AREA", "Plan area"),
            new("SurfaceArea", surface, "AREA", "Surface area"),
            new("MinElevation", zs.Min(p => p.Z), "LENGTH", "Lowest point"),
            new("MaxElevation", zs.Max(p => p.Z), "LENGTH", "Highest point"),
        };
        if (s.Kind == Slope)
        {
            var day = s.Strips.Sum(x => PlanLength(x.Closed ? [.. x.Ends, x.Ends[0]] : x.Ends));
            var widest = s.Strips.SelectMany(x => x.Origins.Zip(x.Ends, (o, d) => (d.Xy - o.Xy).Length)).DefaultIfEmpty(0).Max();
            list.Add(new("DaylightLength", day, "LENGTH", "Daylight line"));
            list.Add(new("MaxWidth", widest, "LENGTH", "Widest"));
        }
        yield return new QuantitySet("Lapin_GradingQuantities", list);
        if (TerrainOf(m, e) is { } terrain)
        {
            var (cut, fill) = Earthwork(tin, Terrains.Existing(terrain));
            yield return new QuantitySet("Lapin_EarthworkQuantities",
            [
                new("CutVolume", cut, "VOLUME", "Cut"),
                new("FillVolume", fill, "VOLUME", "Fill"),
                new("NetVolume", cut - fill, "VOLUME", "Net cut − fill"),
            ]);
        }
    }

    /// <summary>What the interface draws its fields and grips from: each edge as it is now (its points, what it follows), the boundary, the daylight lines.</summary>
    sealed class Props : INodeProps
    {
        public void Add(Entity e, IModelView m, JsonObject props)
        {
            if (!Is(e)) return;
            var s = ShapeOf(m, e);
            JsonArray P(IEnumerable<Vec3> pts) => new([.. pts.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 6), Math.Round(p.Y, 6), Math.Round(p.Z, 6)))]);
            var args = EdgeArgs(e);
            var edges = new JsonArray();
            for (var i = 0; i < args.Count; i++)
            {
                var id = (string?)args[i]["id"];
                var o = new JsonObject { ["own"] = id == null };
                if (id != null) { o["source"] = id; o["name"] = m.Get(id)?.Name ?? "(gone)"; }
                if (id == null && args[i]["points"] is JsonArray own) o["points"] = own.DeepClone();
                edges.Add((JsonNode)o);
            }
            props["gradingEdges"] = edges;
            props["resolvedEdges"] = new JsonArray([.. s.Edges.Select(x => (JsonNode)new JsonObject { ["points"] = P(x.Points), ["closed"] = x.Closed, ["own"] = x.Own })]);
            if (s.Ring.Count >= 3) props["ring"] = P(s.Ring);
            if (s.Strips.Count > 0) props["daylight"] = new JsonArray([.. s.Strips.Select(x => (JsonNode)P(x.Ends))]);
            props["surfaceUsed"] = s.Mode;
            props["planArea"] = Math.Round(s.PlanArea, 6);
            if (TerrainOf(m, e) is { } t) props["terrainId"] = t.Id;
        }
    }

    /// <summary>Moved, turned and mirrored: its own points and the points its stretches are taken between; the edges it follows stay theirs.</summary>
    sealed class Placement : IElementPlacement
    {
        public bool Handles(Entity e) => Is(e);

        public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
        {
            var attrs = e.CloneAttrs();
            var edges = new JsonArray();
            foreach (var a in EdgeArgs(e))
            {
                var o = (JsonObject)a.DeepClone();
                if (o["points"] != null)
                {
                    var (c, z) = FeatureLines.ReadPoints(o["points"]);
                    o["points"] = FeatureLines.PointsJson([.. c.Select(t.Point)], z);
                }
                foreach (var k in new[] { "from", "to" })
                    if (Vec(o[k]) is { } p) { var q = t.Point(p); o[k] = new JsonArray(q.X, q.Y); }
                edges.Add((JsonNode)o);
            }
            attrs["edges"] = edges;
            ctx.Replace(e.With(attrs: attrs));
        }
    }

    /// <summary>A 3D move's vertical part raises its own points' heights and its target level.</summary>
    sealed class Rise : IElementRise
    {
        public bool Handles(Entity e) => Is(e);

        public bool Lift(ICommandContext ctx, Entity e, double dz)
        {
            var attrs = e.CloneAttrs();
            var any = false;
            var edges = new JsonArray();
            foreach (var a in EdgeArgs(e))
            {
                var o = (JsonObject)a.DeepClone();
                if (o["points"] != null)
                {
                    var (c, z) = FeatureLines.ReadPoints(o["points"]);
                    if (z.Any(v => v != null)) any = true;
                    o["points"] = FeatureLines.PointsJson(c, [.. z.Select(v => v is { } h ? h + dz : (double?)null)]);
                }
                edges.Add((JsonNode)o);
            }
            if (e.NumOrNull("level") is { } level) { attrs["level"] = Math.Round(level + dz, 9); any = true; }
            if (!any) return false;
            attrs["edges"] = edges;
            ctx.Replace(e.With(attrs: attrs));
            return true;
        }
    }

    // ─── IFC: which earthwork it is ─────────────────────────────────────

    /// <summary>
    /// The IFC 4.3 class and type it is written as, by what it mostly is against the existing ground: a fill (IfcEarthworksFill; a
    /// slope SLOPEFILL, a patch user-defined „Grading”) or a cut (IfcEarthworksCut CUT). Kept on the element (<c>exportAs</c>,
    /// its predefined type and object type) and brought up to date with its terrain.
    /// </summary>
    public static void Classify(ICommandContext ctx, Entity e)
    {
        var s = ShapeOf(ctx.Model, e);
        var (cut, fill) = s.Surface != null && TerrainOf(ctx.Model, e) is { } t ? Earthwork(s.Surface, Terrains.Existing(t)) : (0.0, 0.0);
        var isFill = fill >= cut;
        var cls = isFill ? "IfcEarthworksFill" : IfcClass.EarthworksCut;
        var pre = isFill ? (s.Kind == Slope ? "SLOPEFILL" : "USERDEFINED") : "CUT";
        var attrs = e.CloneAttrs();
        attrs["exportAs"] = cls;
        if (pre == "USERDEFINED") attrs["objectType"] = "Grading"; else attrs.Remove("objectType");
        if (attrs.ToJsonString() == e.Attrs.ToJsonString() && e.PredefinedType == pre) return;
        ctx.Replace(e.With(attrs: attrs, predefinedType: pre));
    }

    // ─── Commands ───────────────────────────────────────────────────────

    public static Entity Require(IModelView m, string? id) =>
        m.Get(id ?? "") is { } e && Is(e) ? e : throw new CommandException("There is no such grading.");

    /// <summary>The edges an argument gives: [{id, from?, to?} | {points [[x, y] | [x, y, z]…], closed?}…], checked.</summary>
    static JsonArray EdgesArg(IModelView m, JsonNode? node)
    {
        if (node is not JsonArray arr || arr.Count == 0) throw new CommandException("A grading needs its edges: [{id} | {points}…].");
        var o = new JsonArray();
        foreach (var item in arr)
        {
            if (item is JsonValue v && v.TryGetValue<string>(out var sid)) { o.Add((JsonNode)new JsonObject { ["id"] = sid }); continue; }
            if (item is not JsonObject a) throw new CommandException("An edge is {id} (what it follows) or {points}.");
            if ((string?)a["id"] is { Length: > 0 } id)
            {
                if (m.Get(id) is not { } x) throw new CommandException("An edge follows an element that is not in the project.");
                if (!(Hardscapes.Is(x) || FeatureLines.Is(x) || Corridors.Is(x) || (x.Class == IfcClass.EarthworksCut && !Ditches.Is(x))))
                    throw new CommandException($"„{x.Name}” cannot be a grading edge: pick a hardscape, a feature line, a platform, or a path, wall or ditch.");
                var e = new JsonObject { ["id"] = id };
                foreach (var k in new[] { "from", "to" }) if (Vec(a[k]) is { } p) e[k] = new JsonArray(p.X, p.Y);
                o.Add((JsonNode)e);
            }
            else
            {
                var (c, z) = FeatureLines.ReadPoints(a["points"]);
                if (c.Count < 2) throw new CommandException("An edge of points of its own needs two at least.");
                var e = new JsonObject { ["points"] = FeatureLines.PointsJson(c, z) };
                if (a["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var closed) && closed) e["closed"] = true;
                o.Add((JsonNode)e);
            }
        }
        return o;
    }

    static void WriteOptions(ICommandContext ctx, JsonObject attrs, JsonObject a)
    {
        if ((string?)a["surface"] is { } sf) { if (!Surfaces.Contains(sf)) throw new CommandException("A patch's surface is auto, loft or tin."); attrs["surface"] = sf; }
        if ((string?)a["side"] is { } sd) { if (!Sides.Contains(sd)) throw new CommandException("A slope's side is out, in, left or right."); attrs["side"] = sd; }
        if ((string?)a["target"] is { } tg) { if (!Targets.Contains(tg)) throw new CommandException("A slope goes to the ground, over a width or to a level."); attrs["target"] = tg; }
        foreach (var k in new[] { "cutSlope", "fillSlope" })
            if (a[k] != null)
            {
                var v = SlopeArg(a[k]) ?? throw new CommandException("A slope is 1:N: give N (2), „1:2” or a percentage (50 %).");
                attrs[k] = v;
            }
        foreach (var k in new[] { "width", "maxWidth", "spacing" })
            if (a[k] != null)
            {
                if (Json.Number(a[k]) is not { } v || v <= 0) throw new CommandException($"The {k} is a length over 0.");
                attrs[k] = v;
            }
        if (a["level"] != null) { if (Json.Number(a["level"]) is { } lv) attrs["level"] = lv; else attrs.Remove("level"); }
        if ((string?)a["material"] is { Length: > 0 } mat)
            attrs["material"] = ctx.Model.Get(mat) is { Class: IfcClass.Material } me ? me.Id : mat == Material ? Grass(ctx).Id : Materials.Ensure(ctx, mat).Id;
    }

    /// <summary>
    /// The project's grass: the material it has by that name as it is, else a new one in a fresh green with the grass hatch (tufts)
    /// over it in plan, as the ready-made pavings' materials get theirs (<see cref="Hardscapes.PresetLooks"/>).
    /// </summary>
    public static Entity Grass(ICommandContext ctx)
    {
        if (Materials.Find(ctx.Model, Material) is { } had) return had;
        var e = new Entity(ctx.NewId(), IfcClass.Material, Material, new JsonObject { ["color"] = Color, ["elevation"] = new JsonObject { ["hatch"] = "GRASS", ["hatchColor"] = LineColor } });
        ctx.Add(e);
        return e;
    }

    /// <summary>A slope 1 : N as its N: a number, „1:N”, or a grade („50 %”, a fraction under 1 read as rise over run).</summary>
    public static double? SlopeArg(JsonNode? node)
    {
        if (Json.Number(node) is { } n) return n > 0 ? n : null;
        var t = ((string?)node)?.Trim().Replace(" ", "");
        if (string.IsNullOrEmpty(t)) return null;
        if (t.StartsWith("1:") && double.TryParse(t[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0) return d;
        if (t.EndsWith('%') && double.TryParse(t[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var p) && p > 0) return 100 / p;
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && x > 0 ? x : null;
    }

    /// <summary>A grading added (<c>civil.addGrading</c>): refused when it has no surface (its edges do not close, its slope meets nothing).</summary>
    public static JsonObject Add(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        var kind = (string?)a["kind"] ?? Patch;
        if (kind != Patch && kind != Slope) throw new CommandException("A grading is a patch between edges or a slope to the ground.");
        var edges = EdgesArg(m, a["edges"]);
        if (kind == Slope && edges.Count != 1) throw new CommandException("A slope to the ground runs from one edge.");
        var attrs = new JsonObject { ["kind"] = kind, ["edges"] = edges, ["exportAs"] = "IfcEarthworksFill" };
        var standalone = a["standalone"] is JsonValue sv && sv.TryGetValue<bool>(out var st) && st;
        Entity? terrain = null;
        if (!standalone)
        {
            if ((string?)a["terrainId"] is { Length: > 0 } tid) terrain = Terrains.Require(m, tid);
            else
            {
                var probe = new Entity("probe", Class, "probe", (JsonObject)attrs.DeepClone());
                var pts = Edges(m, probe, _ => 0, []).SelectMany(x => x.Points).Select(p => p.Xy).ToList();
                terrain = pts.Select(p => Terrains.Under(m, p)).FirstOrDefault(t => t != null);
            }
        }
        if (terrain != null) attrs[TerrainKey] = terrain.Id;
        WriteOptions(ctx, attrs, a);
        if (attrs["material"] == null) attrs["material"] = Grass(ctx).Id;
        var n = m.OfClass(Class).Count() + 1;
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : kind == Slope ? $"Slope {n}" : $"Grading {n}";
        var e = new Entity(ctx.NewId(), Class, name, attrs, kind == Slope ? "SLOPEFILL" : "USERDEFINED");
        var shape = ShapeOf(m, e);
        if (shape.Surface == null) throw new CommandException(shape.Problems.FirstOrDefault() ?? "The grading has no surface.");
        ctx.Add(e);
        var storey = (string?)a["storeyId"] ?? (terrain != null ? Spatial.StoreyOf(m, terrain.Id)?.Id : null) ?? Spatial.Storeys(m).FirstOrDefault()?.Id
            ?? throw new CommandException("The project has no storey.");
        Spatial.Contain(ctx, storey, e.Id);
        var o = new JsonObject { ["id"] = e.Id };
        if (terrain != null) o["terrainId"] = terrain.Id;
        if (shape.Problems.Count > 0) o["notes"] = new JsonArray([.. shape.Problems.Select(p => (JsonNode)p)]);
        return o;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Grading",
        [
            new("surface", "Surface between its edges", "choice", Choices: Surfaces),
            new("side", "Side", "choice", Choices: Sides),
            new("cutSlope", "Cut slope 1:N", "number", Min: 0.1, Max: 100),
            new("fillSlope", "Fill slope 1:N", "number", Min: 0.1, Max: 100),
            new("target", "Down to", "choice", Choices: Targets),
            new("width", "Width (to a width)", "length", Min: 0.01),
            new("level", "Level (to a level)", "length"),
            new("maxWidth", "At most", "length", Min: 0.1),
        ], Color));
        r.Geometry(Class, Geometry);
        r.Quantities(Class, Quantities);
        // Drawn in plan by its own lines whatever its height (it lies on the ground, under a storey's range), on site plans as the site.
        Lapin.Drawing.Views.Generator.SymbolicInPlan.Add(Class);
        SitePlans.SiteClasses.Add(Class);
        r.Service<INodeProps>(new Props());
        r.Service<IElementPlacement>(new Placement());
        r.Service<IElementRise>(new Rise());

        // A grading (T5): {kind: patch | slope, edges [{id, from?, to?} | {points [[x, y] | [x, y, z]…], closed?}…], terrainId? |
        // standalone?, name?, storeyId?, material? (a name or id; Grass), surface? (auto | loft | tin), side? (out | in | left |
        // right), cutSlope? (N of 1:N, „1:2”), fillSlope?, target? (ground | width | level), width?, level?, maxWidth?, spacing?}.
        r.Command("addGrading", "Grading", (ctx, a) => Gradings.Add(ctx, a));

        // Changed any time: {id, edges?, name?, material?, surface?, side?, cutSlope?, fillSlope?, target?, width?, level?, maxWidth?,
        // spacing?, terrainId? | standalone?}.
        r.Command("updateGrading", "Change grading", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var attrs = e.CloneAttrs();
            if (a["edges"] != null)
            {
                var edges = EdgesArg(ctx.Model, a["edges"]);
                if ((string?)attrs["kind"] == Slope && edges.Count != 1) throw new CommandException("A slope to the ground runs from one edge.");
                attrs["edges"] = edges;
            }
            if (a["standalone"] is JsonValue sv && sv.TryGetValue<bool>(out var st) && st) attrs.Remove(TerrainKey);
            else if ((string?)a["terrainId"] is { Length: > 0 } tid) attrs[TerrainKey] = Terrains.Require(ctx.Model, tid).Id;
            WriteOptions(ctx, attrs, a);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : null;
            var next = e.With(attrs: attrs, name: name);
            if (ShapeOf(ctx.Model, next).Surface == null) throw new CommandException(ShapeOf(ctx.Model, next).Problems.FirstOrDefault() ?? "The grading would have no surface.");
            ctx.Replace(next);
            return null;
        });

        // The heights of an edge's own points: {id, edge, indices? (all), z | dz | ground: true}. An edge that follows an element
        // takes that element's heights: refused, naming it (detach it first).
        r.Command("setGradingPoints", "Grading heights", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var args = EdgeArgs(e);
            var k = (int?)Json.Number(a["edge"]) ?? 0;
            if (k < 0 || k >= args.Count) throw new CommandException("The grading has no such edge.");
            if ((string?)args[k]["id"] is { } src)
                throw new CommandException($"Edge {k + 1} follows {ctx.Model.Get(src)?.Name ?? "an element"}: its heights are that element's. Change them there, or detach the edge to type its own.");
            var (c, z) = FeatureLines.ReadPoints(args[k]["points"]);
            var idx = a["indices"] is JsonArray ia ? ia.Select(v => (int?)Json.Number(v)).OfType<int>().ToList() : [.. Enumerable.Range(0, c.Count)];
            var ground = TerrainOf(ctx.Model, e) is { } t ? Terrains.Base(t) : null;
            foreach (var i in idx)
            {
                if (i < 0 || i >= c.Count) throw new CommandException($"Edge {k + 1} has no point {i + 1}.");
                if (a["ground"] is JsonValue gv && gv.TryGetValue<bool>(out var g) && g) z[i] = null;
                else if (Json.Number(a["z"]) is { } v) z[i] = v;
                else if (Json.Number(a["dz"]) is { } d) z[i] = (z[i] ?? ground?.ElevationAt(c[i].X, c[i].Y) ?? 0) + d;
                else throw new CommandException("Give an elevation (z), a change (dz) or ground: true.");
            }
            var edges = new JsonArray([.. args.Select((x, i) => (JsonNode)(i == k ? new JsonObject { ["points"] = FeatureLines.PointsJson(c, z) } : x.DeepClone()))]);
            if (args[k]["closed"] != null) ((JsonObject)edges[k]!)["closed"] = args[k]["closed"]!.DeepClone();
            var attrs = e.CloneAttrs();
            attrs["edges"] = edges;
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // An edge that follows an element made points of its own, as it is now (their heights then typed): {id, edge}.
        r.Command("detachGradingEdge", "Detach grading edge", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var args = EdgeArgs(e);
            var k = (int?)Json.Number(a["edge"]) ?? 0;
            if (k < 0 || k >= args.Count) throw new CommandException("The grading has no such edge.");
            if ((string?)args[k]["id"] == null) return null;
            var shape = ShapeOf(ctx.Model, e);
            var src = (string?)args[k]["id"];
            var line = shape.Edges.FirstOrDefault(x => x.Source == src) ?? throw new CommandException($"Edge {k + 1} has no line now: what it follows is gone or has no heights.");
            var edges = new JsonArray([.. args.Select((x, i) => (JsonNode)(i == k
                ? new JsonObject { ["points"] = FeatureLines.PointsJson([.. line.Points.Select(p => p.Xy)], [.. line.Points.Select(p => (double?)Math.Round(p.Z, 9))]), ["closed"] = line.Closed }
                : x.DeepClone()))]);
            var attrs = e.CloneAttrs();
            attrs["edges"] = edges;
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // A grading as it is now: its edges in space, its boundary and rulings, its strips' rays and daylight lines, its notes.
        r.Query("gradingShape", (q, a) =>
        {
            var e = Require(q.Model, (string?)a["id"]);
            var s = ShapeOf(q.Model, e);
            JsonArray P(IEnumerable<Vec3> pts) => new([.. pts.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 9), Math.Round(p.Y, 9), Math.Round(p.Z, 9)))]);
            return new JsonObject
            {
                ["kind"] = s.Kind, ["surface"] = s.Mode,
                ["edges"] = new JsonArray([.. s.Edges.Select(x => (JsonNode)new JsonObject { ["points"] = P(x.Points), ["closed"] = x.Closed, ["source"] = x.Source })]),
                ["ring"] = P(s.Ring),
                ["rulings"] = new JsonArray([.. s.Rulings.Select(x => (JsonNode)P(x))]),
                ["strips"] = new JsonArray([.. s.Strips.Select(x => (JsonNode)new JsonObject { ["origins"] = P(x.Origins), ["ends"] = P(x.Ends), ["closed"] = x.Closed, ["cut"] = new JsonArray([.. x.Cut.Select(c => (JsonNode)c)]) })]),
                ["planArea"] = s.PlanArea,
                ["notes"] = new JsonArray([.. s.Problems.Select(p => (JsonNode)p)]),
            };
        });
    }
}
