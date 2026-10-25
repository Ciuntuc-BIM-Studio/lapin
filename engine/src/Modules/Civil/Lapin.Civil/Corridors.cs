using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>A PVI (point of vertical intersection): station, elevation, and the parabolic curve's length (0: a grade break).</summary>
public sealed record Pvi(double Station, double Elevation, double Length = 0);

/// <summary>
/// The longitudinal profile (the red line) in station / elevation: PVIs
/// joined by constant grades, each inner one rounded by a symmetric parabola
/// of length L spanning L/2 either side. Ported from BubbleBIM Pro's Profile.
/// </summary>
public sealed class VerticalProfile
{
    readonly Pvi[] _p;
    readonly double[] _grades;

    public IReadOnlyList<Pvi> Pvis => _p;
    public double StartStation => _p[0].Station;
    public double EndStation => _p[^1].Station;

    VerticalProfile(Pvi[] p)
    {
        _p = p;
        _grades = p.Skip(1).Select((q, i) => (q.Elevation - p[i].Elevation) / (q.Station - p[i].Station)).ToArray();
    }

    /// <summary>Why the PVIs make no profile, or null.</summary>
    public static string? Problem(IReadOnlyList<Pvi> pvis)
    {
        var p = pvis.OrderBy(v => v.Station).ToArray();
        if (p.Length < 2) return "A profile needs two points (PVIs) at least.";
        for (var i = 1; i < p.Length; i++)
        {
            if (p[i].Station - p[i - 1].Station < 1e-6) return $"PVIs {i} and {i + 1} are at the same station.";
            var l0 = i == 1 ? 0 : p[i - 1].Length / 2;
            var l1 = i == p.Length - 1 ? 0 : p[i].Length / 2;
            if (l0 + l1 > p[i].Station - p[i - 1].Station + 1e-9) return $"The vertical curves at PVIs {i} and {i + 1} overlap: shorten them.";
        }
        return null;
    }

    public static VerticalProfile Build(IReadOnlyList<Pvi> pvis) =>
        Problem(pvis) is { } why ? throw new CommandException(why) : new VerticalProfile(pvis.OrderBy(v => v.Station).ToArray());

    public double ElevationAt(double station)
    {
        var s = Math.Clamp(station, StartStation, EndStation);
        for (var i = 1; i < _p.Length - 1; i++)
        {
            var len = _p[i].Length;
            var bvc = _p[i].Station - len / 2;
            if (len > 0 && s >= bvc && s <= _p[i].Station + len / 2)
            {
                var x = s - bvc;
                var zBvc = _p[i].Elevation - _grades[i - 1] * len / 2;
                return zBvc + _grades[i - 1] * x + (_grades[i] - _grades[i - 1]) * x * x / (2 * len);
            }
        }
        for (var i = 1; i < _p.Length; i++)
            if (s <= _p[i].Station) return _p[i - 1].Elevation + _grades[i - 1] * (s - _p[i - 1].Station);
        return _p[^1].Elevation;
    }

    /// <summary>The grade (ratio) at a station; on a vertical curve it changes linearly.</summary>
    public double GradeAt(double station)
    {
        if (station < StartStation || station > EndStation) return 0;
        for (var i = 1; i < _p.Length - 1; i++)
        {
            var len = _p[i].Length;
            if (len > 0 && station >= _p[i].Station - len / 2 && station <= _p[i].Station + len / 2)
                return _grades[i - 1] + (_grades[i] - _grades[i - 1]) * (station - (_p[i].Station - len / 2)) / len;
        }
        for (var i = 1; i < _p.Length; i++) if (station < _p[i].Station) return _grades[i - 1];
        return _grades[^1];
    }

    /// <summary>
    /// A profile that follows the ground: sampled every <paramref name="every"/> m,
    /// simplified (Douglas–Peucker, <paramref name="tolerance"/> m) to the PVIs
    /// that matter, each rounded by a curve as long as its neighbours allow.
    /// </summary>
    public static List<Pvi> FromGround(double from, double to, Func<double, double?> groundAt, double fallback, double every = 5, double tolerance = 0.1)
    {
        var samples = new List<(double S, double Z)>();
        for (var s = from; s < to - 1e-9; s += every) if (groundAt(s) is { } z) samples.Add((s, z));
        if (groundAt(to) is { } ze) samples.Add((to, ze));
        if (samples.Count < 2) return [new(from, fallback), new(to, fallback)];
        var pvis = Simplify(samples, tolerance).Select(k => new Pvi(Math.Round(k.S * 1000) / 1000, Math.Round(k.Z * 1000) / 1000)).ToList();
        for (var i = 1; i < pvis.Count - 1; i++)
        {
            var room = Math.Min(pvis[i].Station - pvis[i - 1].Station, pvis[i + 1].Station - pvis[i].Station);
            pvis[i] = pvis[i] with { Length = Math.Round(room * 0.8 * 100) / 100 };
        }
        return pvis;
    }

    static List<(double S, double Z)> Simplify(List<(double S, double Z)> pts, double tol)
    {
        if (pts.Count <= 2) return pts;
        var (a, b) = (pts[0], pts[^1]);
        double worst = 0; var at = 0;
        for (var i = 1; i < pts.Count - 1; i++)
        {
            var t = (pts[i].S - a.S) / (b.S - a.S);
            var d = Math.Abs(pts[i].Z - (a.Z + (b.Z - a.Z) * t));
            if (d > worst) { worst = d; at = i; }
        }
        if (worst <= tol) return [a, b];
        return [.. Simplify(pts[..(at + 1)], tol).SkipLast(1), .. Simplify(pts[at..], tol)];
    }
}

/// <summary>
/// Stations along a polyline: the station of its start, then metres along it.
/// At an inner vertex the cross-section stands on the bisector and is
/// stretched so its edges meet the neighbours' (a mitre): offsets across stay
/// true on both segments, so a strip, a wall, a channel turns a corner without
/// a gap or an overlap.
/// </summary>
public sealed class Chain
{
    readonly double[] _at;
    public IReadOnlyList<Vec2> Path { get; }
    public double Start { get; }
    public double Length => _at.Length == 0 ? 0 : _at[^1];
    public double End => Start + Length;

    public Chain(IReadOnlyList<Vec2> path, double start = 0)
    {
        var clean = new List<Vec2>();
        foreach (var p in path) if (clean.Count == 0 || (p - clean[^1]).Length > 1e-6) clean.Add(p);
        Path = clean;
        Start = start;
        _at = new double[clean.Count];
        for (var i = 1; i < clean.Count; i++) _at[i] = _at[i - 1] + (clean[i] - clean[i - 1]).Length;
    }

    public int Segments => Math.Max(0, Path.Count - 1);
    public double StationOf(int vertex) => Start + _at[vertex];
    public Vec2 Direction(int segment) => (Path[segment + 1] - Path[segment]).Normalized();

    /// <summary>The segment a station lies on (a vertex belongs to the segment after it, the last to the one before).</summary>
    public int SegmentAt(double station)
    {
        var d = station - Start;
        for (var i = 0; i < Segments - 1; i++) if (d < _at[i + 1] - 1e-9) return i;
        return Math.Max(0, Segments - 1);
    }

    public Vec2 PointAt(double station)
    {
        var i = SegmentAt(station);
        var d = Math.Clamp(station - Start - _at[i], 0, _at[i + 1] - _at[i]);
        return Path[i] + Direction(i) * d;
    }

    /// <summary>
    /// The vector across the path, one metre of offset to the left, at a station
    /// of <paramref name="segment"/>: its normal, or at a vertex the mitre
    /// (the bisector's normal over the cosine of half the turn, at most 4×).
    /// </summary>
    public Vec2 Across(double station, int segment)
    {
        var n = Direction(segment).Perp;
        var d = station - Start;
        int? vertex = Math.Abs(d - _at[segment]) < 1e-9 ? segment : Math.Abs(d - _at[segment + 1]) < 1e-9 ? segment + 1 : null;
        if (vertex is not { } v || v == 0 || v == Path.Count - 1) return n;
        var (a, b) = (Direction(v - 1).Perp, Direction(v).Perp);
        var mid = a + b;
        if (mid.Length < 1e-9) return n;
        var m = mid.Normalized();
        var cos = Math.Max(0.25, Vec2.Dot(m, a));
        return m * (1 / cos);
    }

    /// <summary>The station nearest a plan point, and its offset (+ left).</summary>
    public (double Station, double Offset) Project(Vec2 q)
    {
        var best = (Station: Start, Offset: 0.0, Dist: double.MaxValue);
        for (var i = 0; i < Segments; i++)
        {
            var d = Direction(i);
            var len = _at[i + 1] - _at[i];
            var t = Math.Clamp(Vec2.Dot(q - Path[i], d), 0, len);
            var p = Path[i] + d * t;
            var dist = (q - p).Length;
            if (dist < best.Dist) best = (Start + _at[i] + t, Vec2.Dot(q - p, d.Perp), dist);
        }
        return (best.Station, best.Offset);
    }

    /// <summary>Station text: 1234.5 → 1+234.50.</summary>
    public static string Label(double station)
    {
        var sign = station < 0 ? "-" : "";
        var s = Math.Abs(station);
        var km = Math.Floor(s / 1000 + 1e-9);
        return $"{sign}{km:0}+{s - km * 1000:000.00}";
    }
}

/// <summary>
/// What a path element keeps besides its own section (stored as <c>corridor</c>):
/// the station of its start, the red line (following the ground, natural or
/// proposed, or designed by PVIs), the interval of its stations, the stations
/// chosen for cross-sections, and its joints (every so many metres, or at
/// given stations, each so wide). No joints: the element runs on unbroken.
/// </summary>
public sealed record CorridorSpec(double StationStart, string Mode, string Ground, IReadOnlyList<Pvi> Pvis, double Interval, IReadOnlyList<double> Sections, double JointEvery, double JointWidth, IReadOnlyList<double> JointsAt)
{
    public static readonly CorridorSpec Default = new(0, "ground", "proposed", [], 10, [], 0, 0.02, []);

    public static CorridorSpec Of(Entity e)
    {
        if (e.Attrs["corridor"] is not JsonObject o) return Default;
        var profile = o["profile"] as JsonObject;
        var joints = o["joints"] as JsonObject;
        return new(
            Json.Number(o["stationStart"]) ?? 0,
            (string?)profile?["mode"] == "design" ? "design" : "ground",
            (string?)profile?["ground"] == "existing" ? "existing" : "proposed",
            ReadPvis(profile?["pvis"]),
            Math.Max(0.5, Json.Number(o["interval"]) ?? 10),
            (o["sections"] as JsonArray)?.Select(Json.Number).OfType<double>().Order().ToList() ?? [],
            Math.Max(0, Json.Number(joints?["every"]) ?? 0),
            Math.Clamp(Json.Number(joints?["width"]) ?? 0.02, 0.001, 1),
            (joints?["at"] as JsonArray)?.Select(Json.Number).OfType<double>().Order().ToList() ?? []);
    }

    public static List<Pvi> ReadPvis(JsonNode? n) => n is JsonArray a
        ? a.OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => new Pvi(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0, Math.Max(0, p.Count > 2 ? Json.Number(p[2]) ?? 0 : 0))).OrderBy(p => p.Station).ToList()
        : [];

    public JsonObject ToJson() => new()
    {
        ["stationStart"] = StationStart, ["interval"] = Interval,
        ["profile"] = new JsonObject { ["mode"] = Mode, ["ground"] = Ground, ["pvis"] = new JsonArray(Pvis.Select(p => (JsonNode)new JsonArray(p.Station, p.Elevation, p.Length)).ToArray()) },
        ["sections"] = new JsonArray(Sections.Select(s => (JsonNode)s).ToArray()),
        ["joints"] = new JsonObject { ["every"] = JointEvery, ["width"] = JointWidth, ["at"] = new JsonArray(JointsAt.Select(s => (JsonNode)s).ToArray()) },
    };
}

/// <summary>One closed piece of a cross-section: (offset across, + left; absolute elevation).</summary>
public sealed record SectionPart(string Key, IReadOnlyList<Vec2> Polygon);

/// <summary>
/// The corridor of a path element (a path or road, a retaining wall, a
/// ditch): its stations, its red line over the natural and proposed ground,
/// the joints that break it, and its cross-section swept along it, segment by
/// segment, mitred at the corners.
/// </summary>
public static class Corridors
{
    public static bool Is(Entity e) => e.Class == IfcClass.Pavement || e.Class == RetainingWalls.Class || Ditches.Is(e);

    public static string KindOf(Entity e) => e.Class == IfcClass.Pavement ? "path" : e.Class == RetainingWalls.Class ? "retainingWall" : "ditch";

    public static Chain ChainOf(Entity e) => new(Terrains.Contour(e, "path"), CorridorSpec.Of(e).StationStart);

    /// <summary>The terrain under the element: the one it voids, else the first on its storey.</summary>
    public static Entity? TerrainEntity(IModelView m, Entity e) =>
        Terrains.TerrainOf(m, e.Id) ?? m.OfClass(IfcClass.GeographicElement).FirstOrDefault(t => t.PredefinedType == Terrains.Predefined && Spatial.StoreyOf(m, t.Id)?.Id == Spatial.StoreyOf(m, e.Id)?.Id);

    /// <summary>The ground at a plan point: natural (as surveyed) or proposed (as worked by the brush).</summary>
    public static Func<Vec2, double?> GroundOf(IModelView m, Entity e, string which)
    {
        var te = TerrainEntity(m, e);
        if (te == null) return _ => null;
        var t = which == "existing" ? Terrains.Existing(te) : Terrains.Of(te);
        return p => t.ElevationAt(p.X, p.Y);
    }

    public static double Fallback(IModelView m, Entity e) => Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0;

    /// <summary>
    /// The red line along the axis: the PVIs' profile when designed, else the
    /// ground (natural or proposed) under the axis, carried on where there is none.
    /// </summary>
    public static Func<double, double> RedLine(IModelView m, Entity e, Chain c)
    {
        var spec = CorridorSpec.Of(e);
        if (spec.Mode == "design" && spec.Pvis.Count >= 2 && VerticalProfile.Problem(spec.Pvis) == null)
        {
            var p = VerticalProfile.Build(spec.Pvis);
            return p.ElevationAt;
        }
        var ground = GroundOf(m, e, spec.Ground);
        var fallback = Fallback(m, e);
        // The ground sampled once along the axis (every metre and at the vertices), read linearly between.
        var stations = Densify(c, 0, c.Segments, 1.0).Distinct().Order().ToList();
        var z = new List<double>();
        double? last = null;
        foreach (var s in stations) { var g = ground(c.PointAt(s)) ?? last ?? fallback; last = g; z.Add(g); }
        return s =>
        {
            if (stations.Count == 0) return fallback;
            if (s <= stations[0]) return z[0];
            if (s >= stations[^1]) return z[^1];
            var k = stations.BinarySearch(s);
            if (k >= 0) return z[k];
            k = ~k;
            var t = (s - stations[k - 1]) / (stations[k] - stations[k - 1]);
            return z[k - 1] + (z[k] - z[k - 1]) * t;
        };
    }

    /// <summary>The joints: every so many metres from the start, and at the stations given, inside the element.</summary>
    public static List<double> Joints(Chain c, CorridorSpec s)
    {
        var o = new List<double>(s.JointsAt.Where(j => j > c.Start + s.JointWidth && j < c.End - s.JointWidth));
        if (s.JointEvery > 0)
            for (var j = c.Start + s.JointEvery; j < c.End - s.JointWidth; j += s.JointEvery) o.Add(j);
        return o.Distinct().Order().ToList();
    }

    /// <summary>The stretches the element is built in, between its joints (a joint leaves a gap as wide as it is).</summary>
    public static List<(double From, double To)> Runs(Chain c, CorridorSpec s)
    {
        var runs = new List<(double, double)>();
        var at = c.Start;
        foreach (var j in Joints(c, s))
        {
            if (j - s.JointWidth / 2 > at + 1e-6) runs.Add((at, j - s.JointWidth / 2));
            at = j + s.JointWidth / 2;
        }
        if (c.End > at + 1e-6) runs.Add((at, c.End));
        return runs;
    }

    /// <summary>The stations ticked along the element: its start, every interval, its end.</summary>
    public static List<double> Stations(Chain c, CorridorSpec s)
    {
        var o = new List<double> { c.Start };
        for (var k = Math.Floor(c.Start / s.Interval + 1e-9) + 1; k * s.Interval < c.End - 1e-6; k++) o.Add(k * s.Interval);
        o.Add(c.End);
        return o;
    }

    /// <summary>Stations on segments <paramref name="from"/>..<paramref name="to"/> (exclusive) no further apart than <paramref name="step"/>, the vertices included.</summary>
    static IEnumerable<double> Densify(Chain c, int from, int to, double step)
    {
        for (var i = from; i < to; i++)
        {
            var (a, b) = (c.StationOf(i), c.StationOf(i + 1));
            var n = Math.Max(1, (int)Math.Ceiling((b - a) / step));
            for (var k = 0; k <= n; k++) yield return a + (b - a) * k / n;
        }
    }

    /// <summary>
    /// The cross-section swept along the element: for each stretch between
    /// joints, on each segment, rings every <paramref name="step"/> m lofted
    /// into closed solids that meet the next segment's on the mitre. A part
    /// whose ring changes its count at a station is built from the next ring on.
    /// </summary>
    public static Dictionary<string, List<Solid>> Sweep(Chain c, IReadOnlyList<(double From, double To)> runs, Func<double, int, IReadOnlyList<SectionPart>> sectionAt, double step)
    {
        var o = new Dictionary<string, List<Solid>>();
        if (c.Segments == 0) return o;
        foreach (var (from, to) in runs)
            for (var seg = 0; seg < c.Segments; seg++)
            {
                var (a, b) = (Math.Max(from, c.StationOf(seg)), Math.Min(to, c.StationOf(seg + 1)));
                if (b - a < 1e-6) continue;
                var n = Math.Max(1, (int)Math.Ceiling((b - a) / step));
                var rings = new List<IReadOnlyList<SectionPart>>();
                var stations = Enumerable.Range(0, n + 1).Select(k => a + (b - a) * k / n).ToList();
                foreach (var s in stations) rings.Add(sectionAt(s, seg));
                for (var k = 0; k + 1 < stations.Count; k++)
                {
                    var (p, q) = (c.PointAt(stations[k]), c.PointAt(stations[k + 1]));
                    var (u, v) = (c.Across(stations[k], seg), c.Across(stations[k + 1], seg));
                    foreach (var part in rings[k])
                    {
                        var next = rings[k + 1].FirstOrDefault(x => x.Key == part.Key);
                        if (next == null || next.Polygon.Count != part.Polygon.Count) continue;
                        var r0 = part.Polygon.Select(x => (p + u * x.X).At(x.Y)).ToList();
                        var r1 = next.Polygon.Select(x => (q + v * x.X).At(x.Y)).ToList();
                        if (SiteGeometry.Loft(r0, r1, part.Key) is { } solid)
                        {
                            if (!o.TryGetValue(part.Key, out var list)) o[part.Key] = list = [];
                            list.Add(solid);
                        }
                    }
                }
            }
        return o;
    }

    /// <summary>An outline in plan offset across the path, mitred at its vertices: its left edge at <paramref name="left"/>, its right at <paramref name="right"/> (both + left).</summary>
    public static List<Vec2> Outline(Chain c, double left, double right)
    {
        if (c.Segments == 0) return [];
        var l = new List<Vec2>(); var r = new List<Vec2>();
        for (var i = 0; i < c.Path.Count; i++)
        {
            var s = c.StationOf(i);
            var seg = Math.Min(i, c.Segments - 1);
            var u = c.Across(s, seg);
            l.Add(c.Path[i] + u * left);
            r.Add(c.Path[i] + u * right);
        }
        r.Reverse();
        return [.. l, .. r];
    }

    /// <summary>A line across the element at a station, for a section view: from <paramref name="right"/> to <paramref name="left"/> (+ left), looking along the element.</summary>
    public static (Vec2 Start, Vec2 End) LineAcross(Chain c, double station, double left, double right)
    {
        var seg = c.SegmentAt(station);
        var p = c.PointAt(station);
        var n = c.Direction(seg).Perp;
        return (p + n * right, p + n * left);
    }

    /// <summary>The element's cross-section at a station, and its width either side (+ left, − right).</summary>
    public static (IReadOnlyList<SectionPart> Parts, double Left, double Right) Section(IModelView m, Entity e, double station)
    {
        var c = ChainOf(e);
        var seg = c.SegmentAt(station);
        if (e.Class == IfcClass.Pavement) { var t = Paths.TemplateOf(e); var red = RedLine(m, e, c); return (Paths.SectionAt(t, red(station)), t.Left, -t.Right); }
        if (e.Class == RetainingWalls.Class) { var s = RetainingWalls.Of(e); var levels = RetainingWalls.Levels(m, e, c); return (RetainingWalls.SectionAt(s, levels, station, seg), s.FootingWidth / 2, -s.FootingWidth / 2); }
        var d = Ditches.SectionAt(m, e, c, station);
        var half = d.Count > 0 ? d[0].Polygon.Max(p => Math.Abs(p.X)) : 0;
        return (d, half, -half);
    }

    /// <summary>The ground across a station, every <paramref name="step"/> m from <paramref name="right"/> to <paramref name="left"/>: (offset, elevation).</summary>
    public static List<Vec2> GroundAcross(Chain c, Func<Vec2, double?> ground, double station, double left, double right, double step = 0.25)
    {
        var seg = c.SegmentAt(station);
        var p = c.PointAt(station);
        var n = c.Direction(seg).Perp;
        var o = new List<Vec2>();
        var count = Math.Max(2, (int)Math.Ceiling((left - right) / step));
        for (var k = 0; k <= count; k++)
        {
            var off = right + (left - right) * k / count;
            if (ground(p + n * off) is { } z) o.Add(new Vec2(off, z));
        }
        return o;
    }

    /// <summary>The elevations of the design (red line) and of both grounds along the axis, every <paramref name="step"/> m.</summary>
    public static (List<Vec2> Existing, List<Vec2> Proposed, List<Vec2> Design) LongSection(IModelView m, Entity e, double step)
    {
        var c = ChainOf(e);
        var red = RedLine(m, e, c);
        var (natural, proposed) = (GroundOf(m, e, "existing"), GroundOf(m, e, "proposed"));
        var ex = new List<Vec2>(); var pr = new List<Vec2>(); var de = new List<Vec2>();
        var stations = Densify(c, 0, c.Segments, step).Distinct().Order();
        foreach (var s in stations)
        {
            var p = c.PointAt(s);
            if (natural(p) is { } zn) ex.Add(new Vec2(s, zn));
            if (proposed(p) is { } zp) pr.Add(new Vec2(s, zp));
            de.Add(new Vec2(s, red(s)));
        }
        return (ex, pr, de);
    }
}
