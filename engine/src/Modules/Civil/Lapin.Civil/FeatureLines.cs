using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A feature line (docs/plan-site-grading.md, T2; decision Î1: an element of its own): a 3D polyline in plan, its edges straight
/// or arcs (laid in chords by the one rule, <see cref="Chords"/>), each corner at an elevation of its own, at a grade from its
/// neighbour (%, 1:N), or on the ground; a contour is one at a single <c>elevation</c>; a draped one takes the ground's height
/// at every corner. It belongs to a terrain (<c>terrain</c>, the terrain's id) whose TIN takes it as a breakline: the
/// triangulation is constrained by its segments, so the surface passes exactly through it, the survey points between left
/// where they are and those within its tolerance of it dropped from the triangulation (said by the terrain). The terrain
/// holds the lines it is shaped by, resolved and clipped to its ground, in <see cref="Derived"/>: refreshed by
/// <see cref="FollowUp"/> in the same undo step as whatever changed a line or the terrain's ground, so every reader of the
/// terrain (its body, the paths and plants that sit on it, a split's halves, an export) sees one surface without asking the
/// model. A terrain without feature lines has none and builds exactly as before.
/// </summary>
public static class FeatureLines
{
    public const string Class = "LapinFeatureLine";
    public const string Color = "#b5541c";
    public const string TerrainKey = "terrain";
    /// <summary>The terrain's attribute holding the feature lines it is shaped by: [{id, points [[x, y, z surveyed]…], tol}].</summary>
    public const string Derived = "featureLines";
    /// <summary>How near a survey point may lie to a feature line in plan before it is dropped from the triangulation (m).</summary>
    public const double DefaultTolerance = 0.05;

    public static bool Is(Entity e) => e.Class == Class;

    /// <summary>The feature lines that belong to a terrain, in the model's order.</summary>
    public static IEnumerable<Entity> Of(IModelView m, string terrainId) => m.OfClass(Class).Where(e => e.Str(TerrainKey) == terrainId);

    /// <summary>What a feature line is: its corners, each one's own elevation (model height) or none, its edges' rises, the grades between corners, whether it is closed, draped, a contour at one elevation, and its tolerance.</summary>
    public sealed record Spec(List<Vec2> Corners, List<double?> Z, double[] Rises, double?[] Grades, bool Closed, bool Drape, double? Elevation, double Tolerance)
    {
        public int Segments => Closed ? Corners.Count : Corners.Count - 1;
        public PathCurve Edge(int k) => new(Corners[k], Corners[(k + 1) % Corners.Count], Rises[k]);
        /// <summary>A section line (T3): it follows the ground exactly until it is shaped, and the ground beside it follows its changes within <see cref="Reach"/>.</summary>
        public bool Section { get; init; }
        /// <summary>Each corner's parabolic vertical curve, its length in station (T3; 0: a grade break).</summary>
        public double[] Curves { get; init; } = [];
        /// <summary>The corners locked against the profile editor's changes (T3).</summary>
        public bool[] Locked { get; init; } = [];
        /// <summary>A section line's lateral influence width (m); null: three times its largest change, 2 m at least.</summary>
        public double? Reach { get; init; }
        public double CurveAt(int i) => i >= 0 && i < Curves.Length ? Curves[i] : 0;
        public bool LockedAt(int i) => i >= 0 && i < Locked.Length && Locked[i];
    }

    public static Spec SpecOf(Entity e) => SpecOf(e.Attrs);

    public static Spec SpecOf(JsonObject a)
    {
        var (corners, z) = ReadPoints(a["points"]);
        var closed = a["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c && corners.Count >= 3;
        var segs = Math.Max(0, closed ? corners.Count : corners.Count - 1);
        var locked = new bool[corners.Count];
        foreach (var x in (a["locked"] as JsonArray) ?? [])
            if (Json.Number(x) is { } li && li >= 0 && li < corners.Count && li == Math.Floor(li)) locked[(int)li] = true;
        return new Spec(corners, z, Numbers(a["arcs"], segs).Select(v => v ?? 0).ToArray(), Numbers(a["grades"], segs),
            closed, a["drape"] is JsonValue dv && dv.TryGetValue<bool>(out var d) && d, Json.Number(a["elevation"]),
            Math.Max(0.001, Json.Number(a["tolerance"]) ?? DefaultTolerance))
        {
            Section = a["section"] is JsonValue sv && sv.TryGetValue<bool>(out var sec) && sec,
            Curves = Numbers(a["curves"], corners.Count).Select(v => v is { } l && l > 0 ? l : 0).ToArray(),
            Locked = locked,
            Reach = Json.Number(a["reach"]) is { } rr && rr > 0 ? rr : null,
        };
    }

    /// <summary>Corners as stored or given: [x, y] or [x, y, z] (z a model height; null or missing: none of its own).</summary>
    public static (List<Vec2> Corners, List<double?> Z) ReadPoints(JsonNode? node)
    {
        var corners = new List<Vec2>();
        var zs = new List<double?>();
        foreach (var p in (node as JsonArray)?.OfType<JsonArray>() ?? [])
        {
            if (p.Count < 2 || Json.Number(p[0]) is not { } x || Json.Number(p[1]) is not { } y || !double.IsFinite(x) || !double.IsFinite(y)) continue;
            var z = p.Count > 2 && Json.Number(p[2]) is { } zz && double.IsFinite(zz) ? zz : (double?)null;
            // A corner repeated in place counts once.
            if (corners.Count > 0 && (corners[^1] - new Vec2(x, y)).Length <= 1e-9) { zs[^1] ??= z; continue; }
            corners.Add(new Vec2(x, y));
            zs.Add(z);
        }
        return (corners, zs);
    }

    static double?[] Numbers(JsonNode? node, int count)
    {
        var o = new double?[count];
        if (node is JsonArray arr)
            for (var i = 0; i < count && i < arr.Count; i++)
                if (Json.Number(arr[i]) is { } v && double.IsFinite(v)) o[i] = v;
        return o;
    }

    public static JsonArray PointsJson(IReadOnlyList<Vec2> corners, IReadOnlyList<double?> z) =>
        new([.. corners.Select((c, i) => (JsonNode)(z[i] is { } h ? new JsonArray(R(c.X), R(c.Y), R(h)) : new JsonArray(R(c.X), R(c.Y))))]);

    static double R(double v) => Math.Round(v, 9);

    /// <summary>
    /// A grade as typed: a number is a fraction (0.02), "2 %" a percent, "2 ‰" per mille, "1:50" one in fifty (−1:50 falling);
    /// null when it is none of these.
    /// </summary>
    public static double? Grade(JsonNode? node)
    {
        if (Json.Number(node) is { } n) return double.IsFinite(n) ? n : null;
        if (node is not JsonValue v || !v.TryGetValue<string>(out var s)) return null;
        s = s.Trim().Replace(',', '.').Replace(" ", "");
        static double? Num(string t) => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) ? d : null;
        if (s.EndsWith('%')) return Num(s[..^1]) is { } p ? p / 100 : null;
        if (s.EndsWith('‰')) return Num(s[..^1]) is { } m ? m / 1000 : null;
        if (s.Split(':') is { Length: 2 } parts && Num(parts[0]) is { } a && Num(parts[1]) is { } b && Math.Abs(b) > 1e-12) return a / b;
        return Num(s);
    }

    /// <summary>
    /// Every corner's elevation (model height): a contour's one elevation; a draped line's ground; else the corner's own, then
    /// the grade from the corner before (forward), then from the corner after (backward), then the ground's there.
    /// <paramref name="problems"/> names each corner left without one (off every terrain, with no elevation of its own).
    /// </summary>
    public static double?[] CornerZ(Spec s, Func<Vec2, double?> ground, List<string>? problems = null)
    {
        var n = s.Corners.Count;
        var z = new double?[n];
        if (s.Elevation is { } level) { Array.Fill(z, level); return z; }
        if (s.Drape) for (var i = 0; i < n; i++) z[i] = ground(s.Corners[i]);
        else
        {
            for (var i = 0; i < n; i++) z[i] = s.Z[i];
            var open = s.Segments;
            // Forward along the grades, then backward (a grade from a corner after), as long as one gives a height.
            for (var changed = true; changed;)
            {
                changed = false;
                for (var k = 0; k < open; k++)
                {
                    var j = (k + 1) % n;
                    if (s.Grades[k] is not { } g) continue;
                    var run = s.Edge(k).Length;
                    if (z[j] == null && z[k] is { } zk && !(s.Closed && j == 0)) { z[j] = zk + g * run; changed = true; }
                    else if (z[k] == null && z[j] is { } zj && !(s.Closed && k == 0 && j == 0)) { z[k] = zj - g * run; changed = true; }
                }
            }
            for (var i = 0; i < n; i++) z[i] ??= ground(s.Corners[i]);
        }
        if (problems != null)
            for (var i = 0; i < n; i++)
                if (z[i] == null) problems.Add(Off(i, s.Corners[i]));
        return z;
    }

    /// <summary>What a corner off every terrain, with no elevation of its own, is told (Î6: refused, never put at 0).</summary>
    public static string Off(int i, Vec2 p) =>
        $"Vertex {i + 1} ({p.X.ToString("0.00", CultureInfo.InvariantCulture)}, {p.Y.ToString("0.00", CultureInfo.InvariantCulture)}) has no elevation of its own and no terrain under it: give it one, or move it onto the terrain.";

    /// <summary>
    /// The line in space: its corners at <paramref name="cornerZ"/>, each arc laid in chords by the one rule, a chord vertex's
    /// height between its arc's two corners by how far along the arc it lies; a closed line ends on its first corner again.
    /// </summary>
    public static List<Vec3> Line(Spec s, IReadOnlyList<double?> cornerZ)
    {
        var (o, at) = Laid(s, cornerZ);
        return s.Curves.Any(l => l > 0) ? WithCurves(s, o, at, cornerZ, null) : o;
    }

    /// <summary>The line in space before its vertical curves, with where each corner lies in it (a closed line's first corner at 0).</summary>
    static (List<Vec3> Points, int[] CornerAt) Laid(Spec s, IReadOnlyList<double?> cornerZ)
    {
        var o = new List<Vec3>();
        var n = s.Corners.Count;
        var at = new int[n];
        if (n == 0) return (o, at);
        for (var k = 0; k < s.Segments; k++)
        {
            var j = (k + 1) % n;
            var (z0, z1) = (cornerZ[k] ?? double.NaN, cornerZ[j] ?? double.NaN);
            var c = s.Edge(k);
            at[k] = o.Count;
            o.Add(new Vec3(s.Corners[k].X, s.Corners[k].Y, z0));
            if (!c.IsArc) continue;
            var len = c.Length;
            var st = c.Stations(0, len);
            for (var q = 1; q + 1 < st.Count; q++)
            {
                var p = c.PointAt(st[q]);
                o.Add(new Vec3(p.X, p.Y, Math.Round(z0 + (z1 - z0) * st[q] / len, 9)));
            }
        }
        var last = s.Closed ? 0 : n - 1;
        if (!s.Closed) at[n - 1] = o.Count;
        o.Add(new Vec3(s.Corners[last].X, s.Corners[last].Y, cornerZ[last] ?? double.NaN));
        return (o, at);
    }

    /// <summary>The stations (plan distance from the start) of a polyline's points.</summary>
    public static double[] Stations<T>(IReadOnlyList<T> pts, Func<T, Vec2> xy)
    {
        var st = new double[pts.Count];
        for (var i = 1; i < pts.Count; i++) st[i] = st[i - 1] + (xy(pts[i]) - xy(pts[i - 1])).Length;
        return st;
    }

    /// <summary>
    /// The corners where a vertical curve may round the line: an inner corner of an open line (a closed line's corners but its
    /// first), with a length, whose two segments are designed (<paramref name="segDesigned"/>; null: all).
    /// </summary>
    static bool CurveCorner(Spec s, int k, bool[]? segDesigned) =>
        s.CurveAt(k) > 0 && k > 0 && (s.Closed ? k < s.Corners.Count : k < s.Corners.Count - 1)
        && (segDesigned == null || segDesigned[k - 1] && segDesigned[k % s.Segments]);

    /// <summary>
    /// The line with each corner that has one rounded by its symmetric parabolic vertical curve (T3, as the roads' red line,
    /// <see cref="Lapin.Alignments.VerticalProfile"/>): between the tangents' grades into and out of the corner, L/2 either side of
    /// it in station; laid in as many pieces as the one rule for curves gives (<see cref="Chords"/>, the parabola's radius
    /// L / |Δg|), the points already within it lifted onto it. The corner itself stays where it is in plan.
    /// </summary>
    static List<Vec3> WithCurves(Spec s, List<Vec3> pts, int[] at, IReadOnlyList<double?> cornerZ, bool[]? segDesigned)
    {
        var st = Stations(pts, p => p.Xy);
        var n = s.Corners.Count;
        var curves = new List<(double From, double To, Func<double, double> Z)>();
        for (var k = 1; k < n + (s.Closed ? 0 : -1); k++)
        {
            if (!CurveCorner(s, k, segDesigned)) continue;
            var (ip, ik, inx) = (at[k - 1], at[k], k + 1 < n ? at[k + 1] : pts.Count - 1);
            if (cornerZ[k - 1] is not { } za || cornerZ[k] is not { } zk || cornerZ[(k + 1) % n] is not { } zb) continue;
            var (s0, sk, s1) = (st[ip], st[ik], st[inx]);
            if (sk - s0 < 1e-9 || s1 - sk < 1e-9) continue;
            var g1 = (zk - za) / (sk - s0);
            var g2 = (zb - zk) / (s1 - sk);
            var len = Math.Min(s.CurveAt(k), 2 * Math.Min(sk - s0, s1 - sk));
            if (Math.Abs(g2 - g1) < 1e-12 || len < 1e-6) continue;
            var bvc = sk - len / 2;
            var zBvc = zk - g1 * len / 2;
            curves.Add((bvc, sk + len / 2, x => zBvc + g1 * (x - bvc) + (g2 - g1) * (x - bvc) * (x - bvc) / (2 * len)));
        }
        if (curves.Count == 0) return pts;
        // The stations a curve adds: its pieces by the one rule.
        var extra = new List<double>();
        foreach (var (from, to, z) in curves)
        {
            var len = to - from;
            var g1 = (z(from + 1e-6) - z(from)) / 1e-6;
            var g2 = (z(to) - z(to - 1e-6)) / 1e-6;
            var dg = Math.Abs(Math.Atan(g2) - Math.Atan(g1));
            var pieces = Chords.Count(len / Math.Max(1e-12, Math.Abs(g2 - g1)), dg, 2);
            for (var q = 0; q <= pieces; q++) extra.Add(from + len * q / pieces);
        }
        return Resample(pts, st, extra, x => curves.FirstOrDefault(c => x >= c.From - 1e-9 && x <= c.To + 1e-9) is { Z: not null } c ? c.Z(x) : null);
    }

    /// <summary>A polyline in space with points added at <paramref name="extra"/> stations (on it, between its points), each height <paramref name="z"/> gives replacing its own.</summary>
    static List<Vec3> Resample(List<Vec3> pts, double[] st, IEnumerable<double> extra, Func<double, double?> z)
    {
        var all = st.Select((v, i) => (S: v, I: i)).Concat(extra.Where(x => x > st[0] + 1e-9 && x < st[^1] - 1e-9).Select(x => (S: x, I: -1)))
            .OrderBy(x => x.S).ThenByDescending(x => x.I).ToList();
        var o = new List<Vec3>();
        var seg = 0;
        foreach (var (sv, i) in all)
        {
            Vec3 p;
            if (i >= 0) { p = pts[i]; seg = Math.Min(i, pts.Count - 2); }
            else
            {
                while (seg + 1 < st.Length - 1 && st[seg + 1] < sv) seg++;
                var t = st[seg + 1] - st[seg] < 1e-12 ? 0 : (sv - st[seg]) / (st[seg + 1] - st[seg]);
                p = pts[seg] + (pts[seg + 1] - pts[seg]) * t;
                if (o.Count > 0 && (o[^1].Xy - p.Xy).Length < 1e-6) continue;
            }
            if (z(sv) is { } h) p = p with { Z = Math.Round(h, 9) };
            if (o.Count > 0 && (o[^1].Xy - p.Xy).Length < 1e-9 && i < 0) continue;
            o.Add(p);
        }
        return o;
    }

    /// <summary>
    /// Which corners carry a height of the design (T3): their own, a grade's from a neighbour, a contour's; not a draped line's
    /// nor a corner left to the ground.
    /// </summary>
    public static bool[] Designed(Spec s)
    {
        var n = s.Corners.Count;
        if (s.Elevation != null) return [.. Enumerable.Repeat(true, n)];
        if (s.Drape) return new bool[n];
        return [.. CornerZ(s, _ => null).Select(v => v != null)];
    }

    /// <summary>Where a polyline in plan crosses a ground's triangle edges, by station: the points between which the ground along it is straight.</summary>
    public static List<double> Crossings(Terrain ground, IReadOnlyList<Vec2> poly)
    {
        var o = new List<double>();
        var st = Stations(poly, p => p);
        for (var i = 0; i + 1 < poly.Count; i++)
        {
            var (a, b) = (poly[i], poly[i + 1]);
            var d = b - a;
            var len = d.Length;
            o.Add(st[i]);
            if (len < 1e-12) continue;
            var lo = new Vec2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y));
            var hi = new Vec2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
            var seen = new HashSet<(int, int)>();
            foreach (var ti in ground.TrianglesIn(lo, hi))
            {
                var tr = ground.Tin.Triangles[ti];
                foreach (var (u, v) in new[] { (tr.A, tr.B), (tr.B, tr.C), (tr.C, tr.A) })
                {
                    if (!seen.Add(u < v ? (u, v) : (v, u))) continue;
                    var (p, q) = (ground.Tin.Points[u].Xy, ground.Tin.Points[v].Xy);
                    var e = q - p;
                    var den = Vec2.Cross(d, e);
                    if (Math.Abs(den) < 1e-15) continue;
                    var t = Vec2.Cross(p - a, e) / den;
                    var w = Vec2.Cross(p - a, d) / den;
                    if (t > 1e-12 && t < 1 - 1e-12 && w >= -1e-12 && w <= 1 + 1e-12) o.Add(st[i] + t * len);
                }
            }
        }
        if (poly.Count > 0) o.Add(st[^1]);
        o.Sort();
        var clean = new List<double>();
        foreach (var x in o) if (clean.Count == 0 || x - clean[^1] > 1e-9) clean.Add(x);
        return clean;
    }

    /// <summary>A polyline's point at a station (clamped to it).</summary>
    public static Vec2 PointAt(IReadOnlyList<Vec2> poly, double[] st, double s)
    {
        if (poly.Count == 1 || s <= 0) return poly[0];
        for (var i = 1; i < poly.Count; i++)
            if (s <= st[i] || i == poly.Count - 1)
            {
                var len = st[i] - st[i - 1];
                var t = len < 1e-12 ? 0 : Math.Clamp((s - st[i - 1]) / len, 0, 1);
                return poly[i - 1] + (poly[i] - poly[i - 1]) * t;
            }
        return poly[^1];
    }

    /// <summary>
    /// A section line in space over its ground (T3), with the change it makes along it: a segment between two designed corners
    /// is the design's (straight at its grade, rounded by its vertical curves); any other follows the ground exactly (a point at
    /// every crossing of the ground's triangle edges) lifted by the change at its corners, linearly between them (none at a
    /// corner left to the ground). The change is the line's height less the ground's, at every point of it.
    /// </summary>
    public static (List<Vec3> Line, List<Vec3> Delta) SectionLine(Spec s, IReadOnlyList<double?> cornerZ, bool[] designed, Terrain ground)
    {
        var n = s.Corners.Count;
        var (flat3, at) = Laid(s, [.. s.Corners.Select(_ => (double?)0)]);
        var flat = flat3.Select(p => p.Xy).ToList();
        var st = Stations(flat, p => p);
        var cornerSt = Enumerable.Range(0, n).Select(i => st[at[i]]).ToArray();
        var endSt = st[^1];
        var segDesigned = Enumerable.Range(0, s.Segments).Select(k => designed[k] && designed[(k + 1) % n]).ToArray();
        double G(Vec2 p) => ground.ElevationAt(p.X, p.Y) ?? double.NaN;
        var dCorner = Enumerable.Range(0, n).Select(i => designed[i] && cornerZ[i] is { } z ? z - G(s.Corners[i]) : 0).ToArray();
        var stations = Crossings(ground, flat);
        var line = new List<Vec3>();
        foreach (var sv in stations.Concat(cornerSt).Append(endSt).Distinct().Order())
        {
            // The segment it lies on (the last that starts at or before it).
            var k = 0;
            for (var q = 0; q < s.Segments; q++) if (cornerSt[q] <= sv + 1e-9) k = q;
            var j = (k + 1) % n;
            var (a, b) = (cornerSt[k], k + 1 < n ? cornerSt[j] : endSt);
            var t = b - a < 1e-12 ? 0 : Math.Clamp((sv - a) / (b - a), 0, 1);
            var p = PointAt(flat, st, sv);
            double z;
            if (segDesigned[k]) z = cornerZ[k]!.Value + (cornerZ[j]!.Value - cornerZ[k]!.Value) * t;
            else z = G(p) + dCorner[k] + (dCorner[j] - dCorner[k]) * t;
            if (!double.IsFinite(z)) continue;
            if (line.Count > 0 && (line[^1].Xy - p).Length < 1e-9) { line[^1] = new Vec3(p.X, p.Y, Math.Round(z, 9)); continue; }
            line.Add(new Vec3(p.X, p.Y, Math.Round(z, 9)));
        }
        if (s.Curves.Any(l => l > 0) && line.Count >= 2)
        {
            // The corners' places in the line just laid.
            var lst = Stations(line, p => p.Xy);
            var cat = cornerSt.Select(c => Array.FindIndex(lst, x => Math.Abs(x - c) < 1e-6)).ToArray();
            if (cat.All(i => i >= 0)) line = WithCurves(s, line, cat, cornerZ, segDesigned);
        }
        var delta = line.Select(p => new Vec3(p.X, p.Y, double.IsFinite(G(p.Xy)) ? Math.Round(p.Z - G(p.Xy), 9) : 0)).ToList();
        return (line, delta);
    }

    /// <summary>A section line's reach: its own, else three times its largest change, 2 m at least.</summary>
    public static double ReachOf(Spec s, IReadOnlyList<Vec3> delta) => s.Reach ?? Math.Max(2, 3 * MaxChange(delta));

    public static double MaxChange(IReadOnlyList<Vec3> delta) => delta.Count == 0 ? 0 : delta.Max(d => Math.Abs(d.Z));

    /// <summary>The refusal of a reach too short for the change (Î3: the field's steepest is 2/R, so R ≥ 2 × the change, or the ground folds).</summary>
    public static string? ReachProblem(double reach, double change) =>
        reach + 1e-9 < 2 * change
            ? $"The influence width ({reach.ToString("0.##", CultureInfo.InvariantCulture)} m) must be twice the largest change ({change.ToString("0.##", CultureInfo.InvariantCulture)} m) at least, or the ground would fold over itself: widen it, or change the profile less."
            : null;

    /// <summary>The ground a feature line on a terrain reads its draped and unset corners from: the terrain without its feature lines.</summary>
    public static Func<Vec2, double?> GroundOf(IModelView m, Entity line)
    {
        if (m.Get(line.Str(TerrainKey) ?? "") is { } t && Terrains.IsTerrain(t)) { var g = Terrains.Base(t); return p => g.ElevationAt(p.X, p.Y); }
        return _ => null;
    }

    /// <summary>The line in space as its terrain's ground puts it, with what is wrong with it (a corner left without a height).</summary>
    public static (List<Vec3> Line, double?[] CornerZ, List<string> Problems) Resolve(IModelView m, Entity line)
    {
        var s = SpecOf(line);
        var problems = new List<string>();
        var z = CornerZ(s, GroundOf(m, line), problems);
        if (problems.Count > 0 || s.Corners.Count < 2) return ([], z, problems);
        if (s.Section && BaseOf(m, line) is { } g) return (SectionLine(s, z, Designed(s), g).Line, z, problems);
        return (Line(s, z), z, problems);
    }

    /// <summary>The ground of a feature line's terrain without its feature lines, or null.</summary>
    public static Terrain? BaseOf(IModelView m, Entity line) =>
        m.Get(line.Str(TerrainKey) ?? "") is { } t && Terrains.IsTerrain(t) ? Terrains.Base(t) : null;

    /// <summary>
    /// What a section line changes, as its terrain is to hold it: its line in space and the change along it (none when no corner is
    /// designed: it only reads the ground), with its reach; a line on another kind of ground than a terrain's has none.
    /// </summary>
    public static (List<Vec3> Line, List<Vec3> Delta, double Reach)? SectionChange(Spec s, Terrain ground)
    {
        if (!s.Section || s.Corners.Count < 2) return null;
        var designed = Designed(s);
        if (!designed.Any(d => d)) return null;
        var z = CornerZ(s, p => ground.ElevationAt(p.X, p.Y));
        if (z.Any(v => v == null)) return null;
        var (line, delta) = SectionLine(s, z, designed, ground);
        return (line, delta, ReachOf(s, delta));
    }

    /// <summary>
    /// The pieces of a line in space over a ground: each segment split where it crosses the ground's edge, the pieces whose middle
    /// is on the ground kept, joined where they meet; a new end's height is the line's there.
    /// </summary>
    public static List<List<Vec3>> Clip(IReadOnlyList<Vec3> line, Terrain ground)
    {
        var pieces = new List<List<Vec3>>();
        if (line.Count < 2 || ground.Tin.Triangles.Count == 0) return pieces;
        var edges = ground.Boundary().Select(e => (ground.Tin.Points[e.A].Xy, ground.Tin.Points[e.B].Xy)).ToList();
        List<Vec3>? run = null;
        for (var i = 0; i + 1 < line.Count; i++)
        {
            var (a, b) = (line[i], line[i + 1]);
            var d = b.Xy - a.Xy;
            var ts = new List<double> { 0, 1 };
            foreach (var (p, q) in edges)
            {
                var e = q - p;
                var den = Vec2.Cross(d, e);
                if (Math.Abs(den) < 1e-15) continue;
                var t = Vec2.Cross(p - a.Xy, e) / den;
                var u = Vec2.Cross(p - a.Xy, d) / den;
                if (t > 1e-12 && t < 1 - 1e-12 && u >= -1e-12 && u <= 1 + 1e-12) ts.Add(t);
            }
            ts.Sort();
            for (var k = 0; k + 1 < ts.Count; k++)
            {
                if (ts[k + 1] - ts[k] < 1e-12) continue;
                var mid = a.Xy + d * ((ts[k] + ts[k + 1]) / 2);
                var p0 = a + (b - a) * ts[k];
                var p1 = a + (b - a) * ts[k + 1];
                if (ground.ElevationAt(mid.X, mid.Y) == null) { run = null; continue; }
                if (run == null) { run = [p0]; pieces.Add(run); }
                run.Add(p1);
            }
        }
        return pieces.Where(p => p.Count >= 2).ToList();
    }

    /// <summary>The lines a terrain is shaped by (surveyed heights) and each one's tolerance, as it holds them.</summary>
    public static List<(IReadOnlyList<Vec3> Points, double Tolerance, string Id)> OnTerrain(Entity terrain)
    {
        var o = new List<(IReadOnlyList<Vec3>, double, string)>();
        foreach (var item in (terrain.Attrs[Derived] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var pts = PointLists.Xyz(item["points"]);
            if (pts.Count >= 2) o.Add((pts, Math.Max(0.001, Json.Number(item["tol"]) ?? DefaultTolerance), (string?)item["id"] ?? ""));
        }
        return o;
    }

    /// <summary>
    /// The terrain's <see cref="Derived"/> lines worked out again from its feature lines: each resolved on the terrain's ground
    /// without them, clipped to it, in surveyed heights; a line with a corner left without a height is left out (it says why).
    /// Replaces the terrain only when they changed; none: the attribute goes.
    /// </summary>
    public static void Refresh(ICommandContext ctx, Entity terrain)
    {
        var m = ctx.Model;
        var lines = Of(m, terrain.Id).ToList();
        var hardscapes = Hardscapes.Of(m, terrain.Id).ToList();
        if (lines.Count == 0 && hardscapes.Count == 0 && terrain.Attrs[Derived] == null) return;
        var ground = Terrains.Base(terrain);
        var datum = terrain.Num("datum");
        var list = new JsonArray();
        // The hardscapes' top edges first (T4): rigid, they hold where a feature line crosses them; the ground under each one goes.
        foreach (var h in hardscapes)
        {
            if (Hardscapes.Corners(h).Count < 3) continue;
            var ring = Hardscapes.Edge(h);
            var breakline = Hardscapes.Breakline(h);
            foreach (var piece in Clip([.. breakline, breakline[0]], ground))
                list.Add((JsonNode)new JsonObject
                {
                    ["id"] = h.Id,
                    ["points"] = new JsonArray([.. piece.Select(p => (JsonNode)new JsonArray(p.X, p.Y, R(p.Z + datum)))]),
                    ["tol"] = Hardscapes.Tolerance,
                });
            list.Add((JsonNode)new JsonObject { ["id"] = h.Id, [AreaKey] = new JsonArray([.. ring.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]) });
        }
        foreach (var line in lines)
        {
            var s = SpecOf(line);
            if (s.Corners.Count < 2) continue;
            var problems = new List<string>();
            var z = CornerZ(s, p => ground.ElevationAt(p.X, p.Y), problems);
            if (problems.Count > 0) continue;
            List<Vec3> laid;
            if (s.Section)
            {
                // A section line only reads the ground until a corner of it is designed; then it holds its line, and the ground
                // beside it takes its change within its reach (T3, Î3), worked out when the terrain is built.
                if (SectionChange(s, ground) is not { } change) continue;
                laid = change.Line;
                list.Add((JsonNode)new JsonObject
                {
                    ["id"] = line.Id,
                    [ShiftKey] = new JsonArray([.. change.Delta.Select(d => (JsonNode)new JsonArray(R(d.X), R(d.Y), R(d.Z)))]),
                    ["reach"] = R(change.Reach),
                    ["tol"] = s.Tolerance,
                });
            }
            else laid = Line(s, z);
            foreach (var piece in Clip(laid, ground))
                list.Add((JsonNode)new JsonObject
                {
                    ["id"] = line.Id,
                    ["points"] = new JsonArray([.. piece.Select(p => (JsonNode)new JsonArray(p.X, p.Y, R(p.Z + datum)))]),
                    ["tol"] = s.Tolerance,
                });
        }
        var attrs = terrain.CloneAttrs();
        if (list.Count > 0) attrs[Derived] = list; else attrs.Remove(Derived);
        if (attrs[Derived]?.ToJsonString() == terrain.Attrs[Derived]?.ToJsonString()) return;
        ctx.Replace(terrain.With(attrs: attrs));
    }

    /// <summary>A <see cref="Derived"/> item's change along a section line (T3): [[x, y, dz]…], with its <c>reach</c>.</summary>
    public const string ShiftKey = "shift";

    /// <summary>The section lines' changes a terrain holds: each one's points in plan with its change there, its reach and its tolerance.</summary>
    public static List<(List<Vec3> Delta, double Reach, double Tolerance)> Shifts(Entity terrain)
    {
        var o = new List<(List<Vec3>, double, double)>();
        foreach (var item in (terrain.Attrs[Derived] as JsonArray)?.OfType<JsonObject>() ?? [])
            if (item[ShiftKey] is JsonArray arr && PointLists.Xyz(arr) is { Count: >= 2 } d && Json.Number(item["reach"]) is { } r && r > 0)
                o.Add((d, r, Math.Max(0.001, Json.Number(item["tol"]) ?? DefaultTolerance)));
        return o;
    }

    /// <summary>The nearest point of a polyline to a point in plan: how far, and the polyline's third coordinate there (linear).</summary>
    public static (double Distance, double Value) Nearest(IReadOnlyList<Vec3> line, Vec2 p)
    {
        var best = (D: double.MaxValue, V: 0.0);
        for (var i = 0; i + 1 < line.Count; i++)
        {
            var (a, b) = (line[i], line[i + 1]);
            var d = b.Xy - a.Xy;
            var len2 = Vec2.Dot(d, d);
            var t = len2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p - a.Xy, d) / len2, 0, 1);
            var q = a.Xy + d * t;
            var dist = (p - q).Length;
            if (dist < best.D) best = (dist, a.Z + (b.Z - a.Z) * t);
        }
        if (line.Count == 1) best = ((p - line[0].Xy).Length, line[0].Z);
        return best;
    }

    /// <summary>
    /// How much the section lines raise the ground at a point (T3, Î3): each one's change at its nearest point, faded by Shepard's
    /// weighting across its reach (all of it on the line, none at the reach and beyond); the lines' add up.
    /// </summary>
    public static double ShiftAt(IReadOnlyList<(List<Vec3> Delta, double Reach, double Tolerance)> shifts, Vec2 p)
    {
        double dz = 0;
        foreach (var (d, reach, _) in shifts)
        {
            var (r, v) = Nearest(d, p);
            if (r < reach) dz += v * Shepard(r, reach);
        }
        return dz;
    }

    /// <summary>
    /// Points the ground within a section line's reach gets so that it can follow the change smoothly where the survey is sparse
    /// (derived, never stored): a grid a sixth of the reach apart (0.25 to 2 m) inside the reach, none within half a step of a
    /// survey point or within the line's tolerance, each at the ground's height there plus the change faded across the reach.
    /// </summary>
    public static List<Vec3> Band(IReadOnlyList<(List<Vec3> Delta, double Reach, double Tolerance)> shifts, Terrain ground, IReadOnlyList<Vec3> survey, Func<Vec2, bool> skip)
    {
        var o = new List<Vec3>();
        foreach (var (d, reach, tol) in shifts)
        {
            var h = Math.Clamp(reach / 6, 0.25, 2);
            var cells = new HashSet<(long, long)>();
            foreach (var q in survey) cells.Add(((long)Math.Floor(q.X / (h / 2)), (long)Math.Floor(q.Y / (h / 2))));
            var (lo, hi) = (new Vec2(d.Min(v => v.X) - reach, d.Min(v => v.Y) - reach), new Vec2(d.Max(v => v.X) + reach, d.Max(v => v.Y) + reach));
            for (var x = Math.Floor(lo.X / h) * h; x <= hi.X; x += h)
                for (var y = Math.Floor(lo.Y / h) * h; y <= hi.Y; y += h)
                {
                    if (o.Count > 40000) return o;
                    var p = new Vec2(x, y);
                    var (r, _) = Nearest(d, p);
                    if (r >= reach * 0.95 || r <= Math.Max(tol, h / 4)) continue;
                    if (cells.Contains(((long)Math.Floor(x / (h / 2)), (long)Math.Floor(y / (h / 2)))) || skip(p)) continue;
                    if (ground.ElevationAt(x, y) is not { } g) continue;
                    o.Add(new Vec3(x, y, g + ShiftAt(shifts, p)));
                }
        }
        return o;
    }

    /// <summary>A <see cref="Derived"/> item's outline in plan whose ground it covers (a hardscape's, T4): the survey points inside leave the triangulation.</summary>
    public const string AreaKey = "inside";

    /// <summary>The outlines a terrain's ground is covered by (its hardscapes', as it holds them).</summary>
    public static List<List<Vec2>> Areas(Entity terrain) =>
        [.. ((terrain.Attrs[Derived] as JsonArray)?.OfType<JsonObject>() ?? []).Select(o => PointLists.Xy(o[AreaKey]).ToList()).Where(a => a.Count >= 3)];

    /// <summary>Whether a point in plan lies inside any of the areas.</summary>
    public static bool InAreas(List<List<Vec2>> areas, Vec2 p) => areas.Count > 0 && areas.Any(a => Polygon2.Contains(a, p));

    /// <summary>
    /// Lines that cross each other in plan, each split at the crossing: a crossing becomes a vertex of both, at the height of
    /// the line given first (the other one bends to it there); <paramref name="clashes"/> counts the crossings where their
    /// heights differed by more than a millimetre.
    /// </summary>
    public static List<List<Vec3>> Split(IReadOnlyList<IReadOnlyList<Vec3>> lines, out int clashes)
    {
        var o = Split(lines, out List<Clash> found);
        clashes = found.Count;
        return o;
    }

    /// <summary>Where two feature lines cross at heights more than a millimetre apart (Î7): the place, the height that holds (line <see cref="First"/>, drawn first) and the other's there.</summary>
    public sealed record Clash(Vec2 At, double Held, double Other, int First, int Second);

    /// <summary><see cref="Split(IReadOnlyList{IReadOnlyList{Vec3}}, out int)"/>, with each crossing where the heights disagreed.</summary>
    public static List<List<Vec3>> Split(IReadOnlyList<IReadOnlyList<Vec3>> lines, out List<Clash> clashes)
    {
        clashes = [];
        var inserts = lines.Select(l => new List<(int Seg, double T, Vec3 P)>()).ToList();
        for (var i = 0; i < lines.Count; i++)
            for (var j = i + 1; j < lines.Count; j++)
            {
                var (a, b) = (lines[i], lines[j]);
                for (var p = 0; p + 1 < a.Count; p++)
                    for (var q = 0; q + 1 < b.Count; q++)
                    {
                        var (a0, a1, b0, b1) = (a[p], a[p + 1], b[q], b[q + 1]);
                        if (Math.Max(a0.X, a1.X) < Math.Min(b0.X, b1.X) || Math.Max(b0.X, b1.X) < Math.Min(a0.X, a1.X)
                            || Math.Max(a0.Y, a1.Y) < Math.Min(b0.Y, b1.Y) || Math.Max(b0.Y, b1.Y) < Math.Min(a0.Y, a1.Y)) continue;
                        var (da, db) = (a1.Xy - a0.Xy, b1.Xy - b0.Xy);
                        var den = Vec2.Cross(da, db);
                        if (Math.Abs(den) < 1e-15) continue;
                        var t = Vec2.Cross(b0.Xy - a0.Xy, db) / den;
                        var u = Vec2.Cross(b0.Xy - a0.Xy, da) / den;
                        if (t < -1e-12 || t > 1 + 1e-12 || u < -1e-12 || u > 1 + 1e-12) continue;
                        var at = a0 + (a1 - a0) * Math.Clamp(t, 0, 1);
                        var other = b0.Z + (b1.Z - b0.Z) * Math.Clamp(u, 0, 1);
                        if (Math.Abs(other - at.Z) > 1e-3 && !clashes.Any(c => (c.At - at.Xy).Length < 1e-6)) clashes.Add(new Clash(at.Xy, at.Z, other, i, j));
                        inserts[i].Add((p, t, at));
                        inserts[j].Add((q, u, at));
                    }
            }
        var outLines = new List<List<Vec3>>();
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            if (inserts[i].Count == 0) { outLines.Add([.. l]); continue; }
            var bySeg = inserts[i].GroupBy(x => x.Seg).ToDictionary(g => g.Key, g => g.OrderBy(x => x.T).ToList());
            var pts = new List<Vec3>();
            for (var k = 0; k < l.Count; k++)
            {
                var v = l[k];
                // A crossing on a vertex gives that vertex its height.
                if (k > 0 && bySeg.TryGetValue(k - 1, out var endOn) && endOn[^1].T >= 1 - 1e-12) v = endOn[^1].P;
                if (k + 1 < l.Count && bySeg.TryGetValue(k, out var startOn) && startOn[0].T <= 1e-12) v = startOn[0].P;
                if (pts.Count == 0 || (pts[^1].Xy - v.Xy).Length > 1e-9) pts.Add(v); else pts[^1] = v;
                if (k + 1 < l.Count && bySeg.TryGetValue(k, out var on))
                    foreach (var x in on)
                        if (x.T > 1e-12 && x.T < 1 - 1e-12 && (pts[^1].Xy - x.P.Xy).Length > 1e-9) pts.Add(x.P);
            }
            outLines.Add(pts);
        }
        return outLines;
    }

    /// <summary>The crossings on a terrain where its feature lines disagree (model heights), with the two lines' ids.</summary>
    public static List<(Clash Clash, string Held, string Other)> Clashes(Entity terrain)
    {
        var on = OnTerrain(terrain);
        if (on.Count < 2) return [];
        var datum = terrain.Num("datum");
        Split([.. on.Select(f => f.Points)], out List<Clash> found);
        return [.. found.Select(c => (c with { Held = c.Held - datum, Other = c.Other - datum }, on[c.First].Id, on[c.Second].Id))];
    }

    /// <summary>
    /// A terrain split in two (T2, Î8): each of its feature lines goes to the half it lies in; one across the cut (the line through
    /// <paramref name="p"/> and <paramref name="q"/>) is cut there, as laid (its arcs in chords), every corner keeping its height
    /// (the cut's at the line's height there), the part on the kept half keeping the line's id, each other part a line of its own.
    /// </summary>
    public static void FollowSplit(ICommandContext ctx, Entity was, string otherId, IReadOnlyList<Vec2> kept, IReadOnlyList<Vec2> other, Vec2 p, Vec2 q)
    {
        var ground = Terrains.Base(was);
        foreach (var line in Of(ctx.Model, was.Id).ToList())
        {
            var s = SpecOf(line);
            if (s.Corners.Count < 2) continue;
            var z = CornerZ(s, v => ground.ElevationAt(v.X, v.Y));
            var laid = Line(s, z);
            var d = q - p;
            double Side(Vec2 v) => Vec2.Cross(d, v - p);
            bool InOther(Vec2 v) => !Polygon2.Contains(kept, v) && Polygon2.Contains(other, v);
            // Cut where it crosses the line, then gathered into runs by the half each piece's middle lies in.
            var runs = new List<(bool Other, List<Vec3> Pts)>();
            for (var i = 0; i + 1 < laid.Count; i++)
            {
                var (a, b) = (laid[i], laid[i + 1]);
                var cuts = new List<Vec3> { a };
                var (sa, sb) = (Side(a.Xy), Side(b.Xy));
                if (sa * sb < 0) cuts.Add(a + (b - a) * (sa / (sa - sb)));
                cuts.Add(b);
                for (var k = 0; k + 1 < cuts.Count; k++)
                {
                    var half = InOther((cuts[k].Xy + cuts[k + 1].Xy) * 0.5);
                    if (runs.Count == 0 || runs[^1].Other != half) runs.Add((half, [cuts[k]]));
                    runs[^1].Pts.Add(cuts[k + 1]);
                }
            }
            if (runs.All(r => !r.Other)) continue;
            if (runs.All(r => r.Other))
            {
                var moved = line.CloneAttrs();
                moved[TerrainKey] = otherId;
                ctx.Replace(line.With(attrs: moved));
                continue;
            }
            // A closed line cut becomes open runs; the last joins the first when they are on the same half.
            if (s.Closed && runs.Count > 2 && runs[0].Other == runs[^1].Other)
            {
                runs[0] = (runs[0].Other, [.. runs[^1].Pts.Take(runs[^1].Pts.Count - 1), .. runs[0].Pts]);
                runs.RemoveAt(runs.Count - 1);
            }
            var first = true;
            var storey = Spatial.StoreyOf(ctx.Model, line.Id)?.Id;
            foreach (var (half, pts) in runs.Where(r => r.Pts.Count >= 2).OrderBy(r => r.Other))
            {
                var attrs = line.CloneAttrs();
                attrs[TerrainKey] = half ? otherId : was.Id;
                attrs.Remove("arcs"); attrs.Remove("grades"); attrs.Remove("closed"); attrs.Remove("curves"); attrs.Remove("locked");
                var own = s.Elevation != null || s.Drape;
                attrs["points"] = PointsJson([.. pts.Select(v => v.Xy)], [.. pts.Select(v => own ? (double?)null : R(v.Z))]);
                if (first && !half) { ctx.Replace(line.With(attrs: attrs)); first = false; continue; }
                var e = new Entity(ctx.NewId(), Class, line.Name, attrs);
                ctx.Add(e);
                if (storey != null) Spatial.Contain(ctx, storey, e.Id);
            }
            if (first) ctx.Remove(line.Id);
        }
    }

    /// <summary>The segments of lines in a plan grid, to tell the points within a distance of any of them without trying them all.</summary>
    public sealed class Near
    {
        readonly double _cell;
        readonly Dictionary<(long, long), List<(Vec2 A, Vec2 B, double Tol)>> _cells = [];

        public Near(IEnumerable<(IReadOnlyList<Vec3> Points, double Tolerance)> lines)
        {
            var segs = new List<(Vec2, Vec2, double)>();
            foreach (var (pts, tol) in lines) for (var i = 0; i + 1 < pts.Count; i++) segs.Add((pts[i].Xy, pts[i + 1].Xy, tol));
            _cell = Math.Max(segs.Count > 0 ? segs.Average(s => (s.Item2 - s.Item1).Length) : 1, segs.Count > 0 ? segs.Max(s => s.Item3) * 4 : 1);
            _cell = Math.Max(_cell, 1e-3);
            foreach (var (a, b, tol) in segs)
                for (var i = C(Math.Min(a.X, b.X) - tol); i <= C(Math.Max(a.X, b.X) + tol); i++)
                    for (var j = C(Math.Min(a.Y, b.Y) - tol); j <= C(Math.Max(a.Y, b.Y) + tol); j++)
                        (_cells.TryGetValue((i, j), out var l) ? l : _cells[(i, j)] = []).Add((a, b, tol));
        }

        long C(double v) => (long)Math.Floor(v / _cell);

        /// <summary>Whether a point lies within a line's tolerance of it, in plan.</summary>
        public bool Within(Vec2 p) =>
            _cells.TryGetValue((C(p.X), C(p.Y)), out var list) && list.Any(s => Polyline2.SegmentDistance(p, s.A, s.B) <= s.Tol);
    }

    /// <summary>The survey points a terrain's feature lines drop from its triangulation: those within a line's tolerance of it.</summary>
    public static int Dropped(Entity terrain)
    {
        var lines = OnTerrain(terrain);
        if (lines.Count == 0) return 0;
        var near = new Near(lines.Select(l => (l.Points, l.Tolerance)));
        var areas = Areas(terrain);
        return Terrains.Points(terrain).Count(p => near.Within(p.Xy) && !InAreas(areas, p.Xy));
    }

    /// <summary>The survey points under a terrain's hardscapes (T4), left out of its triangulation: the ground there is their top.</summary>
    public static int Covered(Entity terrain)
    {
        var areas = Areas(terrain);
        return areas.Count == 0 ? 0 : Terrains.Points(terrain).Count(p => InAreas(areas, p.Xy));
    }

    // ─── The element ────────────────────────────────────────────────────

    /// <summary>A feature line seen: its line in space (a hair above the ground, for the screen only), in plan its own line style; its corners snap.</summary>
    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices _)
    {
        var s = SpecOf(e);
        if (s.Corners.Count < 2) return new ElementGeometry([], Diagnostics: ["A feature line needs two corners at least."]);
        var (line, _, problems) = Resolve(m, e);
        var plan = new List<PlanItem>();
        var flat = new List<Vec2>();
        for (var k = 0; k < s.Segments; k++)
        {
            var c = s.Edge(k);
            if (!c.IsArc) { flat.Add(s.Corners[k]); continue; }
            var st = c.Stations(0, c.Length);
            for (var q = 0; q + 1 < st.Count; q++) flat.Add(q == 0 ? s.Corners[k] : c.PointAt(st[q]));
        }
        if (!s.Closed) flat.Add(s.Corners[^1]);
        plan.Add(new PlanItem("symbol", s.Closed, flat, Style: PlanStyle(s)));
        var lines = line.Count >= 2 ? new List<IReadOnlyList<Vec3>> { line.Select(p => p with { Z = p.Z + 0.02 }).ToList() } : null;
        return new ElementGeometry([], plan, s.Corners, problems.Count > 0 ? problems : null, Lines: lines, LineColor: s.Section ? SectionColor : Color);
    }

    /// <summary>Its own line in plan: a contour solid and heavier, any other dash-dot-dot, both in the feature lines' colour.</summary>
    public static LineStyleSpec PlanStyle(Spec s) => s.Elevation != null ? new LineStyleSpec(3, Color, "solid")
        : s.Section ? new LineStyleSpec(2, SectionColor, "dashed") : new LineStyleSpec(2, Color, "dashdotdot");

    /// <summary>A section line's colour (T3): it reads the ground, so it is told apart from the lines that shape it.</summary>
    public const string SectionColor = "#6d4c9f";

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        var (line, _, _) = Resolve(m, e);
        if (line.Count < 2) yield break;
        double plan = 0, slope = 0;
        for (var i = 1; i < line.Count; i++) { plan += (line[i].Xy - line[i - 1].Xy).Length; slope += (line[i] - line[i - 1]).Length; }
        yield return new QuantitySet("Lapin_FeatureLineQuantities",
        [
            new("Length", plan, "LENGTH", "Length in plan"),
            new("SlopeLength", slope, "LENGTH", "Length along its slope"),
            new("MinElevation", line.Min(p => p.Z), "LENGTH", "Lowest point"),
            new("MaxElevation", line.Max(p => p.Z), "LENGTH", "Highest point"),
        ]);
    }

    /// <summary>What the interface draws its grips and fields from: every corner's elevation as resolved, and which are its own.</summary>
    public sealed class Props : INodeProps
    {
        public void Add(Entity entity, IModelView model, JsonObject props)
        {
            if (!Is(entity)) return;
            var s = SpecOf(entity);
            var z = CornerZ(s, GroundOf(model, entity));
            props["cornerZ"] = new JsonArray([.. z.Select(v => v is { } h ? (JsonNode)Math.Round(h, 6) : null)]);
            props["ownZ"] = new JsonArray([.. s.Corners.Select((_, i) => (JsonNode)(s.Elevation == null && !s.Drape && s.Z[i] != null))]);
            var lengths = new JsonArray();
            for (var k = 0; k < s.Segments; k++) lengths.Add((JsonNode)Math.Round(s.Edge(k).Length, 6));
            props["segmentLengths"] = lengths;
            // Where it crosses another line of its terrain at another height (Î7): the place, the height that holds, its own there, the other line.
            if (model.Get(entity.Str(TerrainKey) ?? "") is { } t && Terrains.IsTerrain(t))
            {
                var clashes = new JsonArray();
                foreach (var (c, held, other) in Clashes(t))
                    if (held == entity.Id || other == entity.Id)
                        clashes.Add((JsonNode)new JsonObject
                        {
                            ["x"] = Math.Round(c.At.X, 4), ["y"] = Math.Round(c.At.Y, 4), ["held"] = Math.Round(c.Held, 4), ["other"] = Math.Round(c.Other, 4),
                            ["holds"] = held == entity.Id, ["with"] = held == entity.Id ? other : held,
                        });
                if (clashes.Count > 0) props["clashes"] = clashes;
            }
        }
    }

    /// <summary>A feature line moves, turns and mirrors as any element: its corners (a mirror turns its arcs the other way); its heights stay.</summary>
    public sealed class Placement : IElementPlacement
    {
        public bool Handles(Entity e) => Is(e);

        public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
        {
            var s = SpecOf(e);
            var attrs = e.CloneAttrs();
            attrs["points"] = PointsJson([.. s.Corners.Select(c => t.Point(c))], s.Z);
            if (t.Mirrored && s.Rises.Any(r => Math.Abs(r) > 1e-12)) attrs["arcs"] = new JsonArray([.. s.Rises.Select(r => (JsonNode)(-r))]);
            ctx.Replace(e.With(attrs: attrs));
        }
    }

    /// <summary>A 3D move's vertical part raises its corners' own heights (a contour's elevation); a draped line stays on the ground.</summary>
    public sealed class Rise : IElementRise
    {
        public bool Handles(Entity e) => Is(e);

        public bool Lift(ICommandContext ctx, Entity e, double dz)
        {
            var s = SpecOf(e);
            if (s.Drape) return false;
            var attrs = e.CloneAttrs();
            if (s.Elevation is { } level) attrs["elevation"] = R(level + dz);
            else
            {
                if (s.Z.All(z => z == null)) return false;
                attrs["points"] = PointsJson(s.Corners, [.. s.Z.Select(z => z is { } h ? h + dz : (double?)null)]);
            }
            ctx.Replace(e.With(attrs: attrs));
            return true;
        }
    }

    /// <summary>
    /// After every command, in its undo step: the terrains whose feature lines changed (a line added, edited, moved, deleted, or
    /// given to another terrain), or whose own ground changed under draped lines, get their <see cref="Derived"/> lines again.
    /// </summary>
    public sealed class FollowUp : ICommandFollowUp
    {
        public JsonNode? After(ICommandContext ctx, IEdit edit, string command, JsonObject args, JsonNode? result)
        {
            HashSet<string>? touched = null;
            foreach (var ch in edit.EntityChanges)
            {
                foreach (var x in new[] { ch.Before, ch.After })
                    if (x != null && Is(x) && x.Str(TerrainKey) is { } tid) (touched ??= []).Add(tid);
                if ((ch.After ?? ch.Before) is { } t && Terrains.IsTerrain(t) && GroundChanged(ch.Before, ch.After)) (touched ??= []).Add(t.Id);
                // A hardscape changed (T4): every terrain it is, or was, cut into gets its edges again.
                if ((ch.After ?? ch.Before) is { } h && Hardscapes.Is(h))
                    foreach (var t2 in Terrains.All(ctx.Model))
                        if (Hardscapes.Of(ctx.Model, t2.Id).Any() || Areas(t2).Count > 0) (touched ??= []).Add(t2.Id);
            }
            // A hardscape cut into a terrain or taken out of it.
            foreach (var rc in edit.RelationChanges)
                if ((rc.After ?? rc.Before) is { Class: IfcClass.RelVoidsElement } rel && ctx.Model.Get(rel.Relating) is { } host && Terrains.IsTerrain(host)) (touched ??= []).Add(host.Id);
            if (touched == null) return result;
            foreach (var id in touched)
                if (ctx.Model.Get(id) is { } t && Terrains.IsTerrain(t)) Refresh(ctx, t);
            return result;
        }

        static readonly string[] GroundKeys = ["points", "faces", "breaklines", "sculpts", "datum", Terrains.BoundaryKey, Terrains.BoundaryArcs];

        static bool GroundChanged(Entity? before, Entity? after) =>
            before == null || after == null || GroundKeys.Any(k => before.Attrs[k]?.ToJsonString() != after.Attrs[k]?.ToJsonString());
    }

    // ─── Commands ───────────────────────────────────────────────────────

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Feature line",
        [
            new("elevation", "Elevation (contour)", "length"),
            new("drape", "On the ground", "bool"),
            new("tolerance", "Points dropped within", "length", Min: 0.001, Max: 5),
        ], Color));
        r.Geometry(Class, Geometry);
        r.Quantities(Class, Quantities);
        r.Service<INodeProps>(new Props());
        r.Service<IElementPlacement>(new Placement());
        r.Service<IElementRise>(new Rise());
        r.Service<ICommandFollowUp>(new FollowUp());
        r.Service<IIfcAnnotationCurves>(new IfcCurves());

        // A feature line on a terrain: {points [[x, y] | [x, y, z]], arcs?, grades?, closed?, elevation? (a contour), drape?, tolerance?,
        // terrainId? (else the terrain under it), name?}. A corner without a height of its own takes the ground's; one off every
        // terrain without one is refused, named (Î6).
        r.Command("addFeatureLine", "Feature line", (ctx, a) => FeatureLines.Add(ctx, a));

        // Its corners, arcs, grades, closed, elevation (null: no longer a contour), drape, tolerance, name, terrain.
        r.Command("updateFeatureLine", "Change feature line", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var attrs = e.CloneAttrs();
            var terrain = (string?)a["terrainId"] is { } tid ? Terrains.Require(ctx.Model, tid) : TerrainOf(ctx.Model, e);
            attrs[TerrainKey] = terrain.Id;
            Write(attrs, a, e);
            Check(attrs, terrain);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : null;
            ctx.Replace(e.With(name: name, attrs: attrs));
            return null;
        });

        // Corners' elevations typed (T2, as T1's points): {id, indices, z (absolute, model height) | dz | ground: true (back to the ground)}.
        // A contour's corners become corners of their own at its elevation first; a draped line's are let go of the ground.
        r.Command("setFeatureLinePoints", "Feature line elevations", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var s = SpecOf(e);
            var n = s.Corners.Count;
            var indices = (a["indices"] as JsonArray)?.Select(x => Json.Number(x) is { } v ? (int)v : -1).Where(i => i >= 0 && i < n).Distinct().ToList()
                ?? (Json.Number(a["index"]) is { } one && one >= 0 && one < n ? [(int)one] : []);
            if (indices.Count == 0) throw new CommandException("Which corners? The line has " + n + ", from 0.");
            if (indices.FirstOrDefault(s.LockedAt, -1) is var li && li >= 0) throw new CommandException(LockedMessage(li));
            var resolved = CornerZ(s, GroundOf(ctx.Model, e));
            var own = s.Elevation is { } level ? s.Corners.Select(_ => (double?)level).ToList() : s.Drape ? s.Corners.Select(_ => (double?)null).ToList() : [.. s.Z];
            var grades = s.Grades.ToArray();
            var toGround = a["ground"] is JsonValue gv && gv.TryGetValue<bool>(out var g) && g;
            var exact = Json.Number(a["z"]);
            var dz = Json.Number(a["dz"]);
            if (!toGround && exact == null && dz == null) throw new CommandException("Give the elevation (z), a change (dz), or ground: true.");
            foreach (var i in indices)
            {
                own[i] = toGround ? null : exact ?? (resolved[i] ?? throw new CommandException(Off(i, s.Corners[i]))) + dz!.Value;
                // Its height is its own now: the grade that gave it goes.
                if (i > 0) grades[i - 1] = null;
            }
            var attrs = e.CloneAttrs();
            attrs.Remove("elevation");
            attrs.Remove("drape");
            attrs["points"] = PointsJson(s.Corners, own);
            WriteGrades(attrs, grades);
            Check(attrs, TerrainOf(ctx.Model, e));
            ctx.Replace(e.With(attrs: attrs));
            var after = CornerZ(SpecOf(attrs), GroundOf(ctx.Model, e));
            return new JsonObject { ["z"] = new JsonArray([.. indices.Select(i => (JsonNode?)(after[i] is { } h ? Math.Round(h, 6) : null))]) };
        });

        // A grade typed between two corners: {id, segment (from corner segment to the next), grade (0.02, "2 %", "1:50"; null clears),
        // keep: "start" (default: the next corner's height follows) | "end" (the corner's follows from the next one's)}.
        r.Command("setFeatureLineGrade", "Feature line grade", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var s = SpecOf(e);
            var n = s.Corners.Count;
            if (Json.Number(a["segment"]) is not { } sv || sv < 0 || sv >= s.Segments || sv != Math.Floor(sv)) throw new CommandException($"Which segment? The line has {s.Segments}, from 0.");
            var k = (int)sv;
            if (s.Elevation != null) throw new CommandException("A contour is level: it has no grade. Clear its elevation to give it one.");
            if (s.Closed && k == n - 1) throw new CommandException("The closing segment's grade follows from its two ends: type it on another segment.");
            var grades = s.Grades.ToArray();
            var own = s.Drape ? s.Corners.Select(_ => (double?)null).ToList() : [.. s.Z];
            var keepEnd = (string?)a["keep"] == "end";
            var cleared = a["grade"] == null;
            double? grade = cleared ? null : Grade(a["grade"]) ?? throw new CommandException("A grade is a number (0.02), a percent (2 %) or one in N (1:50).");
            if (!cleared)
            {
                // The corner that follows keeps no height of its own: its height is the grade's.
                var resolved = CornerZ(s, GroundOf(ctx.Model, e));
                var (from, to) = keepEnd ? (k + 1, k) : (k, k + 1);
                if (s.LockedAt(to % n)) throw new CommandException(LockedMessage(to % n));
                own[from] ??= resolved[from] ?? throw new CommandException(Off(from, s.Corners[from]));
                own[to] = null;
                // Nor does another grade give it a height: a grade into the corner kept from the other side goes.
                if (keepEnd && k > 0) grades[k - 1] = null;
            }
            grades[k] = grade;
            var attrs = e.CloneAttrs();
            attrs.Remove("drape");
            attrs["points"] = PointsJson(s.Corners, own);
            WriteGrades(attrs, grades);
            Check(attrs, TerrainOf(ctx.Model, e));
            ctx.Replace(e.With(attrs: attrs));
            var z = CornerZ(SpecOf(attrs), GroundOf(ctx.Model, e));
            return new JsonObject { ["z"] = new JsonArray([.. z.Select(v => (JsonNode?)(v is { } h ? Math.Round(h, 6) : null))]) };
        });

        // A contour dragged in plan (T2; Î3): {id: the terrain, elevation, from [x, y] on the contour, to [x, y], reach?} or
        // {lineId: a feature line (a contour drawn), from, to, reach?}. The part of the contour within the reach of the point
        // grabbed becomes a feature line at its elevation, moved by the drag faded by Shepard's weighting (power 2) between the
        // grab (the whole drag) and the reach's edge (nothing); the survey points within the reach take the height the ground
        // had where the same field carries them from, so the ground between the contour and its neighbours follows; nothing
        // beyond the reach moves. The ground before is kept as the existing one (cut and fill measure the drag). One undo step.
        r.Command("dragContour", "Drag contour", (ctx, a) =>
        {
            var m = ctx.Model;
            var from = Vec(a["from"]) ?? throw new CommandException("Where is the contour grabbed? from: [x, y].");
            var to = Vec(a["to"]) ?? throw new CommandException("Where to? to: [x, y].");
            var drag = to - from;
            if (drag.Length < 1e-6) throw new CommandException("The contour was not moved.");
            var reach = Json.Number(a["reach"]) is { } rv && rv > 0 ? rv : Math.Max(2, 3 * drag.Length);
            // The field folds the ground over itself where it changes faster than the drag (its steepest is 2/R): a reach of twice the drag at least.
            if (reach < 2 * drag.Length) throw new CommandException($"The reach ({reach:0.##} m) must be twice the drag ({drag.Length:0.##} m) at least, or the ground would fold over itself.");
            Entity? line = (string?)a["lineId"] is { } lid ? Require(m, lid) : null;
            var t = line != null ? TerrainOf(m, line) : Terrains.Require(m, (string?)a["id"] ?? (string?)a["terrainId"] ?? Terrains.Under(m, from)?.Id);
            var ground = Terrains.Of(t);
            double Fade(Vec2 p) => Shepard((p - from).Length, reach);
            Vec2 Moved(Vec2 p) => p + drag * Fade(p);
            string id;
            if (line == null)
            {
                var z = Json.Number(a["elevation"]) ?? throw new CommandException("Which contour? Give its elevation.");
                // A contour drawn at this elevation where it was grabbed is dragged as itself.
                line = Of(m, t.Id).FirstOrDefault(x => SpecOf(x) is { Elevation: { } ze } sx && Math.Abs(ze - z) < 1e-6 && Distance(sx, from) <= 0.05);
                if (line == null)
                {
                    var pads = Terrains.Pads(m, t.Id).Select(p => (Outer: ArcOutline.BuiltOf(p), Holes: (IReadOnlyList<IReadOnlyList<Vec2>>)OutlineHoles.Built(p))).Where(p => p.Outer.Count >= 3);
                    var chains = Terrains.ChainedContours(ground, z, pads);
                    var best = chains.Select(c => (Chain: c, D: PolyDistance(c, from))).OrderBy(x => x.D).FirstOrDefault();
                    if (best.Chain == null || best.D > reach) throw new CommandException($"There is no contour at {z:0.00} near the point grabbed.");
                    var (piece, closed) = Within(best.Chain, from, reach);
                    if (piece.Count < 2) throw new CommandException("The contour grabbed has too little within the reach.");
                    var moved = piece.Select(Moved).ToList();
                    var attrs = new JsonObject
                    {
                        [TerrainKey] = t.Id, ["points"] = PointsJson(moved, moved.Select(_ => (double?)null).ToList()), ["elevation"] = R(z),
                    };
                    if (closed) attrs["closed"] = true;
                    var e = new Entity(ctx.NewId(), Class, $"Contour {z.ToString("0.00", CultureInfo.InvariantCulture)}", attrs);
                    ctx.Add(e);
                    if (Spatial.StoreyOf(m, t.Id) is { } storey) Spatial.Contain(ctx, storey.Id, e.Id);
                    id = e.Id;
                }
                else id = line.Id;
            }
            else id = line.Id;
            if (m.Get(id) is { } existing && existing == line)
            {
                // A feature line dragged: its corners by the same field (its arcs as they were). Its straight segments within the reach
                // get corners every eighth of the reach first, so the line bends with the ground as the field does, not as a chord.
                var s = Densified(SpecOf(line), from, reach);
                var attrs = line.CloneAttrs();
                attrs["points"] = PointsJson([.. s.Corners.Select(Moved)], s.Z);
                if (s.Rises.Any(r => Math.Abs(r) > 1e-12)) attrs["arcs"] = new JsonArray([.. s.Rises.Select(r => (JsonNode)r)]); else attrs.Remove("arcs");
                WriteGrades(attrs, s.Grades);
                WriteCurves(attrs, s.Curves);
                WriteLocked(attrs, s.Locked);
                ctx.Replace(line.With(attrs: attrs));
            }
            // The ground within the reach follows: its points take the height from where the field carries them.
            var pts = Terrains.Points(t);
            var tattrs = t.CloneAttrs();
            if (tattrs["existingPoints"] is not JsonArray { Count: >= 3 })
            {
                var before = Terrains.Base(t).Tin;
                var datum0 = t.Num("datum");
                tattrs["existingPoints"] = Terrains.PointsJson(before.Points.Select(p => new Vec3(p.X, p.Y, p.Z + datum0)));
                tattrs["existingFaces"] = Terrains.FacesJson(before.Triangles);
            }
            var shifted = 0;
            for (var i = 0; i < pts.Count; i++)
            {
                var p = pts[i].Xy;
                var f = Fade(p);
                if (f <= 0) continue;
                if (ground.ElevationAt(p.X, p.Y) is not { } here) continue;
                var src = p - drag * f;
                if (ground.ElevationAt(src.X, src.Y) is not { } there) continue;
                var dz = Math.Round(there - here, 9);
                if (Math.Abs(dz) < 1e-12) continue;
                pts[i] = pts[i] with { Z = pts[i].Z + dz };
                shifted++;
            }
            if (shifted > 0) tattrs["points"] = Terrains.PointsJson(pts);
            if (tattrs.ToJsonString() != t.Attrs.ToJsonString()) ctx.Replace(t.With(attrs: tattrs));
            return new JsonObject { ["id"] = id, ["moved"] = shifted, ["reach"] = Math.Round(reach, 4) };
        });

        // The contour of a terrain nearest a point in plan (for a tool that grabs one): its elevation and its line; null when none is within `within` (m, default 2).
        r.Query("contourNear", (q, a) =>
        {
            var p = new Vec2(Json.Number(a["x"]) ?? 0, Json.Number(a["y"]) ?? 0);
            var within = Json.Number(a["within"]) ?? 2;
            var terrains = (string?)a["id"] is { } tid ? [Terrains.Require(q.Model, tid)] : Terrains.All(q.Model).ToList();
            (double D, double Z, List<Vec2> Line, string Id, string? LineId)? best = null;
            foreach (var t in terrains)
            {
                // A contour drawn (a feature line at one elevation) first, where it is.
                foreach (var fl in Of(q.Model, t.Id))
                    if (SpecOf(fl) is { Elevation: { } ze } s && Distance(s, p) is var d && d <= within && (best == null || d < best.Value.D))
                        best = (d, ze, Flat(s), t.Id, fl.Id);
                var g = Terrains.Of(t);
                var interval = Terrains.DrawnInterval(t, g);
                if (interval <= 0 || g.ElevationAt(p.X, p.Y) is not { } here) continue;
                foreach (var z in new[] { Math.Floor(here / interval) * interval, Math.Ceiling(here / interval) * interval }.Distinct())
                    foreach (var c in Terrains.ChainedContours(g, z, []))
                        if (PolyDistance(c, p) is var dc && dc <= within && (best == null || dc < best.Value.D - 1e-9))
                            best = (dc, z, c, t.Id, null);
            }
            if (best is not { } b) return null;
            var o = new JsonObject
            {
                ["id"] = b.Id, ["elevation"] = Math.Round(b.Z, 6), ["distance"] = Math.Round(b.D, 4),
                ["points"] = new JsonArray([.. b.Line.Select(v => (JsonNode)new JsonArray(Math.Round(v.X, 6), Math.Round(v.Y, 6)))]),
            };
            if (b.LineId != null) o["lineId"] = b.LineId;
            return o;
        });
    }

    /// <summary>A feature line added (the command <c>civil.addFeatureLine</c>).</summary>
    public static JsonObject Add(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        var (corners, _) = ReadPoints(a["points"]);
        var terrain = (string?)a["terrainId"] is { } tid ? Terrains.Require(m, tid)
            : corners.Select(c => Terrains.Under(m, c)).FirstOrDefault(t => t != null)
              ?? throw new CommandException("A feature line is drawn on a terrain: there is none under it.");
        var attrs = new JsonObject { [TerrainKey] = terrain.Id };
        Write(attrs, a, null);
        var s = Check(attrs, terrain);
        var n = m.OfClass(Class).Count() + 1;
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm
            : s.Elevation is { } z ? $"Contour {z.ToString("0.00", CultureInfo.InvariantCulture)}"
            : s.Section ? $"Section line {m.OfClass(Class).Count(x => SpecOf(x).Section) + 1}" : $"Feature line {n}";
        var e = new Entity(ctx.NewId(), Class, name, attrs);
        ctx.Add(e);
        if (Spatial.StoreyOf(m, terrain.Id) is { } storey) Spatial.Contain(ctx, storey.Id, e.Id);
        return new JsonObject { ["id"] = e.Id };
    }

    /// <summary>
    /// How much of a drag a point carries at <paramref name="r"/> from the grab, with reach <paramref name="reach"/>: Shepard's
    /// inverse-distance weighting (power 2) between the grab, moved by the whole drag, and the reach's edge, which stays:
    /// (R − r)² / ((R − r)² + r²). 1 at the grab, ½ halfway, 0 at the reach and beyond, falling monotonically and smoothly.
    /// </summary>
    public static double Shepard(double r, double reach)
    {
        if (r <= 1e-12) return 1;
        if (r >= reach) return 0;
        var o = reach - r;
        return o * o / (o * o + r * r);
    }

    /// <summary>
    /// A line with corners added on its straight segments that come within <paramref name="reach"/> of <paramref name="at"/>, one every
    /// eighth of the reach, each at the height between its segment's ends (none of its own when they have none); a segment with a grade
    /// or an arc is left as it is. The line in space is the same.
    /// </summary>
    public static Spec Densified(Spec s, Vec2 at, double reach)
    {
        var step = reach / 8;
        var corners = new List<Vec2>();
        var zs = new List<double?>();
        var rises = new List<double>();
        var grades = new List<double?>();
        var curves = new List<double>();
        var locked = new List<bool>();
        var n = s.Corners.Count;
        for (var k = 0; k < n; k++)
        {
            corners.Add(s.Corners[k]);
            zs.Add(s.Z[k]);
            curves.Add(s.CurveAt(k));
            locked.Add(s.LockedAt(k));
            if (k >= s.Segments) break;
            var (a, b) = (s.Corners[k], s.Corners[(k + 1) % n]);
            var len = (b - a).Length;
            var split = Math.Abs(s.Rises[k]) <= 1e-12 && s.Grades[k] == null && Polyline2.SegmentDistance(at, a, b) < reach && len > step;
            var parts = split ? (int)Math.Ceiling(len / step) : 1;
            for (var q = 1; q < parts; q++)
            {
                var f = (double)q / parts;
                corners.Add(a + (b - a) * f);
                zs.Add(s.Z[k] is { } z0 && s.Z[(k + 1) % n] is { } z1 ? R(z0 + (z1 - z0) * f) : null);
                rises.Add(0); grades.Add(null); curves.Add(0); locked.Add(false);
            }
            rises.Add(s.Rises[k]); grades.Add(s.Grades[k]);
        }
        return s with { Corners = corners, Z = zs, Rises = [.. rises], Grades = [.. grades], Curves = [.. curves], Locked = [.. locked] };
    }

    /// <summary>
    /// The run of a chained contour within <paramref name="reach"/> of a point, its ends where it leaves the circle; the whole
    /// of it when it closes inside (then <c>closed</c>). The run taken is the one passing nearest the point.
    /// </summary>
    public static (List<Vec2> Piece, bool Closed) Within(IReadOnlyList<Vec2> chain, Vec2 at, double reach)
    {
        var closedChain = chain.Count > 3 && (chain[0] - chain[^1]).Length < 1e-9;
        if (closedChain && chain.All(p => (p - at).Length < reach)) return ([.. chain.Take(chain.Count - 1)], true);
        bool In(Vec2 p) => (p - at).Length < reach;
        Vec2 Cross(Vec2 a, Vec2 b)
        {
            // Where a–b meets the circle (one inside, one outside).
            var d = b - a;
            var f = a - at;
            double qa = Vec2.Dot(d, d), qb = 2 * Vec2.Dot(f, d), qc = Vec2.Dot(f, f) - reach * reach;
            var disc = Math.Max(0, qb * qb - 4 * qa * qc);
            var s = Math.Sqrt(disc);
            var t1 = (-qb - s) / (2 * qa);
            var t2 = (-qb + s) / (2 * qa);
            var t = t1 >= -1e-12 && t1 <= 1 + 1e-12 ? t1 : t2;
            return a + d * Math.Clamp(t, 0, 1);
        }
        // The vertex nearest the point, then out both ways while inside (round a closed chain).
        var n = closedChain ? chain.Count - 1 : chain.Count;
        var near = Enumerable.Range(0, n).MinBy(i => (chain[i] - at).Length);
        if (!In(chain[near])) return ([], false);
        int Step(int i, int d) => closedChain ? ((i + d) % n + n) % n : i + d;
        bool Valid(int i) => closedChain || i >= 0 && i < n;
        var back = new List<Vec2>();
        var i0 = near;
        for (var guard = 0; guard < n; guard++)
        {
            var j = Step(i0, -1);
            if (!Valid(j)) break;
            if (!In(chain[j])) { back.Add(Cross(chain[i0], chain[j])); break; }
            back.Add(chain[j]);
            i0 = j;
        }
        var fwd = new List<Vec2>();
        var i1 = near;
        for (var guard = 0; guard < n; guard++)
        {
            var j = Step(i1, 1);
            if (!Valid(j)) break;
            if (!In(chain[j])) { fwd.Add(Cross(chain[i1], chain[j])); break; }
            fwd.Add(chain[j]);
            i1 = j;
        }
        back.Reverse();
        var o = new List<Vec2>(back) { chain[near] };
        o.AddRange(fwd);
        var clean = new List<Vec2>();
        foreach (var p in o) if (clean.Count == 0 || (clean[^1] - p).Length > 1e-9) clean.Add(p);
        return (clean, false);
    }

    /// <summary>How far a point lies from a polyline in plan.</summary>
    public static double PolyDistance(IReadOnlyList<Vec2> line, Vec2 p)
    {
        if (line.Count == 1) return (line[0] - p).Length;
        var d = double.MaxValue;
        for (var i = 0; i + 1 < line.Count; i++) d = Math.Min(d, Polyline2.SegmentDistance(p, line[i], line[i + 1]));
        return d;
    }

    static Vec2? Vec(JsonNode? n) =>
        n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y)
        : n is JsonObject o && Json.Number(o["x"]) is { } ox && Json.Number(o["y"]) is { } oy ? new Vec2(ox, oy) : null;

    /// <summary>A feature line's plan polyline (its arcs in chords), a closed one ending where it began.</summary>
    public static List<Vec2> Flat(Spec s) => Line(s, s.Corners.Select(_ => (double?)0).ToList()).Select(p => p.Xy).ToList();

    static double Distance(Spec s, Vec2 p) => s.Corners.Count < 2 ? double.MaxValue : PolyDistance(Flat(s), p);

    public static string LockedMessage(int i) => $"Point {i + 1} is locked: unlock it to change it.";

    public static Entity Require(IModelView m, string? id) =>
        m.Get(id ?? "") is { } e && Is(e) ? e : throw new CommandException("There is no such feature line.");

    static Entity TerrainOf(IModelView m, Entity line) =>
        m.Get(line.Str(TerrainKey) ?? "") is { } t && Terrains.IsTerrain(t) ? t : throw new CommandException("The feature line's terrain is gone: give it another (terrainId).");

    static void WriteGrades(JsonObject attrs, double?[] grades)
    {
        if (grades.Any(g => g != null)) attrs["grades"] = new JsonArray([.. grades.Select(g => g is { } v ? (JsonNode)R(v) : null)]);
        else attrs.Remove("grades");
    }

    /// <summary>The arguments of an add or an update written into the attributes (<paramref name="was"/>: the line before, whose arcs and grades stay with as many corners).</summary>
    static void Write(JsonObject attrs, JsonObject a, Entity? was)
    {
        if (a["points"] != null)
        {
            var (corners, z) = ReadPoints(a["points"]);
            attrs["points"] = PointsJson(corners, z);
            if (was != null && SpecOf(was).Corners.Count != corners.Count)
            {
                if (a["arcs"] == null) attrs.Remove("arcs");
                if (a["grades"] == null) attrs.Remove("grades");
                if (a["curves"] == null) attrs.Remove("curves");
                if (a["locked"] == null) attrs.Remove("locked");
            }
        }
        if (a["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var closed)) { if (closed) attrs["closed"] = true; else attrs.Remove("closed"); }
        var n = ReadPoints(attrs["points"]).Corners.Count;
        var segs = attrs["closed"] is JsonValue c2 && c2.TryGetValue<bool>(out var cl) && cl ? n : n - 1;
        if (a["arcs"] is JsonArray arcs)
        {
            var rises = Numbers(arcs, Math.Max(0, segs));
            for (var k = 0; k < rises.Length; k++)
                if (rises[k] is { } rise && Math.Abs(rise) > 1e-9)
                {
                    var (p, q) = (ReadPoints(attrs["points"]).Corners[k], ReadPoints(attrs["points"]).Corners[(k + 1) % n]);
                    rises[k] = PathCurve.CheckRise(rise, (q - p).Length);
                }
            if (rises.Any(x => x is { } v && Math.Abs(v) > 1e-9)) attrs["arcs"] = new JsonArray([.. rises.Select(x => (JsonNode)R(x ?? 0))]); else attrs.Remove("arcs");
        }
        if (a["grades"] is JsonArray gs) WriteGrades(attrs, [.. gs.Take(Math.Max(0, segs)).Select(Grade)]);
        if (a.ContainsKey("elevation")) { if (Json.Number(a["elevation"]) is { } z) attrs["elevation"] = R(z); else attrs.Remove("elevation"); }
        if (a["drape"] is JsonValue dv && dv.TryGetValue<bool>(out var drape)) { if (drape) attrs["drape"] = true; else attrs.Remove("drape"); }
        if (Json.Number(a["tolerance"]) is { } tol) attrs["tolerance"] = Math.Clamp(tol, 0.001, 5);
        if (a["section"] is JsonValue sv && sv.TryGetValue<bool>(out var section)) { if (section) attrs["section"] = true; else attrs.Remove("section"); }
        if (a.ContainsKey("reach")) { if (Json.Number(a["reach"]) is { } r && r > 0) attrs["reach"] = R(r); else attrs.Remove("reach"); }
        if (a["curves"] is JsonArray cs) WriteCurves(attrs, [.. Numbers(cs, n).Select(v => v is { } l && l > 0 ? l : 0)]);
        if (a["locked"] is JsonArray ls) WriteLocked(attrs, [.. Enumerable.Range(0, n).Select(i => ls.Any(x => Json.Number(x) == i))]);
    }

    public static void WriteCurves(JsonObject attrs, double[] curves)
    {
        if (curves.Any(l => l > 0)) attrs["curves"] = new JsonArray([.. curves.Select(l => (JsonNode)R(l))]); else attrs.Remove("curves");
    }

    public static void WriteLocked(JsonObject attrs, bool[] locked)
    {
        if (locked.Any(l => l)) attrs["locked"] = new JsonArray([.. locked.Select((l, i) => (l, i)).Where(x => x.l).Select(x => (JsonNode)x.i)]); else attrs.Remove("locked");
    }

    /// <summary>A line's attributes checked on its terrain: two corners at least (three closed), and every corner with a height (Î6).</summary>
    public static Spec Check(JsonObject attrs, Entity terrain)
    {
        var s = SpecOf(attrs);
        if (s.Corners.Count < 2) throw new CommandException("A feature line needs two corners at least.");
        if (attrs["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c && s.Corners.Count < 3) throw new CommandException("A closed feature line needs three corners at least.");
        var problems = new List<string>();
        var g = Terrains.Base(terrain);
        CornerZ(s, p => g.ElevationAt(p.X, p.Y), problems);
        if (problems.Count > 0) throw new CommandException(problems[0]);
        // Its vertical curves fit between their neighbours (T3), as a road's red line's must.
        for (var k = 0; k < s.Corners.Count; k++)
        {
            var l = s.CurveAt(k);
            if (l <= 0) continue;
            if (k == 0 || !s.Closed && k == s.Corners.Count - 1) throw new CommandException("A vertical curve rounds a corner between two segments: an end has none.");
            var before = s.Edge(k - 1).Length;
            var after = s.Edge(k % s.Segments).Length;
            var prev = s.CurveAt(k - 1) / 2;
            var next = s.CurveAt((k + 1) % s.Corners.Count) / 2;
            if (l / 2 + prev > before + 1e-9 || l / 2 + next > after + 1e-9)
                throw new CommandException($"The vertical curve at point {k + 1} ({l.ToString("0.##", CultureInfo.InvariantCulture)} m) does not fit between its neighbours: shorten it.");
        }
        // A section line's change within a reach long enough not to fold the ground (Î3).
        if (s.Section && s.Reach is { } reach && SectionChange(s, g) is { } change && ReachProblem(reach, MaxChange(change.Delta)) is { } why)
            throw new CommandException(why);
        return s;
    }

    /// <summary>A feature line in IFC (docs/plan-site-grading.md §4): an IfcAnnotation, its line in space as its 3D axis; a contour is IFC 4.3's CONTOURLINE.</summary>
    sealed class IfcCurves : IIfcAnnotationCurves
    {
        public IfcAnnotationCurve? CurveOf(IModelView m, Entity e)
        {
            if (!Is(e)) return null;
            var (line, _, problems) = Resolve(m, e);
            if (problems.Count > 0 || line.Count < 2) return null;
            return new IfcAnnotationCurve([line], SpecOf(e).Elevation != null ? "CONTOURLINE" : "USERDEFINED", "FeatureLine");
        }
    }
}
