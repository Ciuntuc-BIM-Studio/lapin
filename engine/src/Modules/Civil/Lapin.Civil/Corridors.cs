using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// Stations along a path element's axis: the station of its start, then
/// metres along it. The axis is the PIs the user drew; a PI with a curve
/// (<see cref="PiCurve"/>: a radius, and clothoids either side) rounds the
/// corner into an IFC 4.3 alignment (<see cref="Lapin.Alignments.PiAlignment"/>),
/// a PI without one keeps it sharp. At a sharp vertex the cross-section
/// stands on the bisector and is stretched so its edges meet the neighbours'
/// (a mitre): offsets across stay true on both segments, so a strip, a wall,
/// a channel turns a corner without a gap or an overlap. Along a curve the
/// section follows the tangent. Segments are the alignment's (a line, an
/// arc, a clothoid), a plain polyline's being its straights.
/// </summary>
public sealed class Chain
{
    readonly double[] _at;
    readonly Lapin.Alignments.PreparedAlignment? _prep;
    readonly bool[] _curved;
    /// <summary>The segment joints: the PIs of a polyline; of a curved axis, every segment's start and the end.</summary>
    public IReadOnlyList<Vec2> Path { get; }
    public double Start { get; }
    public double Length => _at.Length == 0 ? 0 : _at[^1];
    public double End => Start + Length;
    /// <summary>The IFC 4.3 alignment of the axis, when a PI rounds it; null for a plain polyline.</summary>
    public Lapin.Alignments.Alignment? Alignment { get; }
    /// <summary>Why the curves asked for could not be built (the axis is then the plain polyline), or null.</summary>
    public string? Problem { get; }

    public Chain(IReadOnlyList<Vec2> path, double start = 0) : this(path, null, start) { }

    public Chain(IReadOnlyList<Vec2> path, IReadOnlyList<PiCurve>? curves, double start = 0)
    {
        var clean = new List<Vec2>();
        var kept = new List<PiCurve>();
        for (var i = 0; i < path.Count; i++)
            if (clean.Count == 0 || (path[i] - clean[^1]).Length > 1e-6) { clean.Add(path[i]); kept.Add(curves != null && i < curves.Count ? curves[i] : PiCurve.None); }
        Start = start;
        if (clean.Count >= 2 && kept.Any(c => !c.IsCorner))
        {
            try
            {
                var pi = Lapin.Alignments.PiAlignment.Build(Lapin.Alignments.PiAlignment.Of(clean, kept));
                (Alignment, _prep, Path, _at, _curved) = OnSegments(pi.ToSegments(), start);
                return;
            }
            catch (ArgumentException e) { Problem = e.Message; }
        }
        Path = clean;
        _at = new double[clean.Count];
        for (var i = 1; i < clean.Count; i++) _at[i] = _at[i - 1] + (clean[i] - clean[i - 1]).Length;
        _curved = new bool[Math.Max(0, clean.Count - 1)];
    }

    /// <summary>A chain on explicit IFC 4.3 segments (a generated axis: a track parallel to another, a turnout's branch).</summary>
    public static Chain FromSegments(IReadOnlyList<Lapin.Alignments.HorizontalSegment> segments, double start = 0) => new(segments, start, true);

    Chain(IReadOnlyList<Lapin.Alignments.HorizontalSegment> segments, double start, bool _)
    {
        Start = start;
        if (segments.Count == 0) { Path = []; _at = []; _curved = []; return; }
        (Alignment, _prep, Path, _at, _curved) = OnSegments(segments, start);
    }

    static (Lapin.Alignments.Alignment, Lapin.Alignments.PreparedAlignment, IReadOnlyList<Vec2>, double[], bool[]) OnSegments(IReadOnlyList<Lapin.Alignments.HorizontalSegment> segments, double start)
    {
        var alignment = new Lapin.Alignments.Alignment(segments, StartStation: start);
        var prep = Lapin.Alignments.PreparedAlignment.Of(alignment);
        var path = segments.Select(s => s.Start).Append(prep.Horizontal.At(prep.Length).P).ToList();
        double[] at = [.. prep.Horizontal.Starts, prep.Length];
        var curved = segments.Select(s => s.Type != Lapin.Alignments.HorizontalType.Line).ToArray();
        return (alignment, prep, path, at, curved);
    }

    public int Segments => Math.Max(0, Path.Count - 1);
    public double StationOf(int vertex) => Start + _at[vertex];
    /// <summary>Whether a segment is an arc or a clothoid (its direction turns along it).</summary>
    public bool IsCurved(int segment) => _curved[segment];
    /// <summary>The direction at a segment's start (constant along a straight).</summary>
    public Vec2 Direction(int segment) => _prep == null ? (Path[segment + 1] - Path[segment]).Normalized() : _prep.Horizontal.Segments[segment].At(0).Tangent;
    /// <summary>The direction at a station (unit, along the axis).</summary>
    public Vec2 DirectionAt(double station)
    {
        if (_prep != null) return _prep.Horizontal.At(Math.Clamp(station - Start, 0, Length)).Tangent;
        return Segments == 0 ? new Vec2(1, 0) : Direction(SegmentAt(station));
    }

    /// <summary>The segment a station lies on (a vertex belongs to the segment after it, the last to the one before).</summary>
    public int SegmentAt(double station)
    {
        var d = station - Start;
        for (var i = 0; i < Segments - 1; i++) if (d < _at[i + 1] - 1e-9) return i;
        return Math.Max(0, Segments - 1);
    }

    public Vec2 PointAt(double station)
    {
        if (_prep != null) return _prep.Horizontal.At(Math.Clamp(station - Start, 0, Length)).P;
        var i = SegmentAt(station);
        var d = Math.Clamp(station - Start - _at[i], 0, _at[i + 1] - _at[i]);
        return Path[i] + Direction(i) * d;
    }

    /// <summary>
    /// The vector across the path, one metre of offset to the left, at a station
    /// of <paramref name="segment"/>: the normal there, or at a vertex where the
    /// direction breaks the mitre (the bisector's normal over the cosine of half
    /// the turn, at most 4×).
    /// </summary>
    public Vec2 Across(double station, int segment)
    {
        var d = station - Start;
        int? vertex = Math.Abs(d - _at[segment]) < 1e-9 ? segment : Math.Abs(d - _at[segment + 1]) < 1e-9 ? segment + 1 : null;
        if (vertex is not { } v || v == 0 || v == Path.Count - 1) return _prep == null ? Direction(segment).Perp : DirectionAt(station).Perp;
        var (a, b) = (EndDirection(v - 1).Perp, Direction(v).Perp);
        var mid = a + b;
        if (mid.Length < 1e-9) return b;
        var m = mid.Normalized();
        var cos = Math.Max(0.25, Vec2.Dot(m, a));
        return m * (1 / cos);
    }

    /// <summary>The direction at a segment's end.</summary>
    Vec2 EndDirection(int segment) => _prep == null ? Direction(segment) : _prep.Horizontal.Segments[segment].At(_prep.Horizontal.Segments[segment].Length).Tangent;

    /// <summary>The station nearest a plan point, and its offset (+ left).</summary>
    public (double Station, double Offset) Project(Vec2 q)
    {
        if (_prep != null) { var r = _prep.StationOf(q); return (Start + r.S, r.Offset); }
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

    /// <summary>
    /// Stations from <paramref name="from"/> to <paramref name="to"/> (both included, inside
    /// the axis) no further apart than <paramref name="step"/>; on a curve as close as the
    /// chord needs (5 mm of sagitta), so a swept section follows it.
    /// </summary>
    public List<double> StationsBetween(double from, double to, double step)
    {
        var (a, b) = (Math.Max(Start, from), Math.Min(End, to));
        if (b - a < 1e-9) return [a];
        if (_prep != null) return _prep.SampleStations(new(MaxStep: step, From: a - Start, To: b - Start)).Select(s => Start + s).ToList();
        var n = Math.Max(1, (int)Math.Ceiling((b - a) / step - 1e-9));
        return Enumerable.Range(0, n + 1).Select(k => a + (b - a) * k / n).ToList();
    }

    /// <summary>
    /// The stations a plan outline needs, each with its segment: every vertex, and
    /// along a curve the points a chord of <paramref name="chord"/> m of sagitta asks for.
    /// A plain polyline gives just its vertices.
    /// </summary>
    public List<(double Station, int Segment)> PlanStations(double chord = 0.01)
    {
        var o = new List<(double, int)>();
        for (var i = 0; i < Segments; i++)
        {
            o.Add((StationOf(i), i));
            if (_curved[i] && _prep != null)
                foreach (var s in _prep.SampleStations(new(Chord: chord, From: _at[i], To: _at[i + 1])).Skip(1).SkipLast(1)) o.Add((Start + s, i));
        }
        if (Segments > 0) o.Add((End, Segments - 1));
        return o;
    }

    /// <summary>The axis in plan: its vertices, and the curves drawn through.</summary>
    public List<Vec2> Axis(double chord = 0.01) => _prep == null ? Path.ToList() : PlanStations(chord).Select(x => PointAt(x.Station)).ToList();

    /// <summary>A line offset across the axis (+ left), mitred at the sharp vertices and following the curves.</summary>
    public List<Vec2> Offset(double offset, double chord = 0.01) => PlanStations(chord).Select(x => PointAt(x.Station) + Across(x.Station, x.Segment) * offset).ToList();

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

    /// <summary>The curve at each vertex of the path (stored as <c>curves</c>: [radius, spiral in, spiral out] per vertex); none: a polyline.</summary>
    public IReadOnlyList<PiCurve> Curves { get; init; } = [];

    /// <summary>A generated axis: its IFC 4.3 segments as such (stored as <c>horizontal</c>), the path and curves then only its picture. Null: the axis is the path.</summary>
    public IReadOnlyList<Lapin.Alignments.HorizontalSegment>? Horizontal { get; init; }

    public static CorridorSpec Of(Entity e)
    {
        if (e.Attrs["corridor"] is not JsonObject o) return Default;
        var profile = o["profile"] as JsonObject;
        var joints = o["joints"] as JsonObject;
        return new CorridorSpec(
            Json.Number(o["stationStart"]) ?? 0,
            (string?)profile?["mode"] == "design" ? "design" : "ground",
            (string?)profile?["ground"] == "existing" ? "existing" : "proposed",
            ReadPvis(profile?["pvis"]),
            Math.Max(0.5, Json.Number(o["interval"]) ?? 10),
            (o["sections"] as JsonArray)?.Select(Json.Number).OfType<double>().Order().ToList() ?? [],
            Math.Max(0, Json.Number(joints?["every"]) ?? 0),
            Math.Clamp(Json.Number(joints?["width"]) ?? 0.02, 0.001, 1),
            (joints?["at"] as JsonArray)?.Select(Json.Number).OfType<double>().Order().ToList() ?? [])
        { Curves = ReadCurves(o["curves"]), Horizontal = Lapin.Alignments.AlignmentJson.ReadHorizontal(o["horizontal"]) };
    }

    /// <summary>Curves per vertex: [radius, spiral in, spiral out] (missing values 0), or {radius, spiralIn, spiralOut}.</summary>
    public static List<PiCurve> ReadCurves(JsonNode? n) => n is JsonArray a
        ? a.Select(c => c switch
        {
            JsonArray v => new PiCurve(Math.Max(0, Json.Number(v.Count > 0 ? v[0] : null) ?? 0), Math.Max(0, Json.Number(v.Count > 1 ? v[1] : null) ?? 0), Math.Max(0, Json.Number(v.Count > 2 ? v[2] : null) ?? 0)),
            JsonObject v => new PiCurve(Math.Max(0, Json.Number(v["radius"]) ?? 0), Math.Max(0, Json.Number(v["spiralIn"]) ?? 0), Math.Max(0, Json.Number(v["spiralOut"]) ?? 0)),
            _ => PiCurve.None,
        }).ToList()
        : [];

    public static JsonArray CurvesJson(IEnumerable<PiCurve> curves) => new(curves.Select(c => (JsonNode)new JsonArray(c.Radius, c.SpiralIn, c.SpiralOut)).ToArray());

    public static List<Pvi> ReadPvis(JsonNode? n) => n is JsonArray a
        ? a.OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => new Pvi(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0, Math.Max(0, p.Count > 2 ? Json.Number(p[2]) ?? 0 : 0))).OrderBy(p => p.Station).ToList()
        : [];

    public JsonObject ToJson() => new()
    {
        ["stationStart"] = StationStart, ["interval"] = Interval,
        ["profile"] = new JsonObject { ["mode"] = Mode, ["ground"] = Ground, ["pvis"] = new JsonArray(Pvis.Select(p => (JsonNode)new JsonArray(p.Station, p.Elevation, p.Length)).ToArray()) },
        ["sections"] = new JsonArray(Sections.Select(s => (JsonNode)s).ToArray()),
        ["joints"] = new JsonObject { ["every"] = JointEvery, ["width"] = JointWidth, ["at"] = new JsonArray(JointsAt.Select(s => (JsonNode)s).ToArray()) },
        ["curves"] = CurvesJson(Curves),
        ["horizontal"] = Horizontal == null ? null : Lapin.Alignments.AlignmentJson.Horizontal(Horizontal),
    };
}

/// <summary>One closed piece of a cross-section: (offset across, + left; absolute elevation).</summary>
public sealed record SectionPart(string Key, IReadOnlyList<Vec2> Polygon);

/// <summary>What a cross-section's parts are made of, for the section drawn in the configurator: the material's name and its section hatch.</summary>
public static class SectionLooks
{
    /// <summary>A part's material (an id the project keeps, or a name its look is guessed from) and the hatch its section draws with; nulls for a part of no material.</summary>
    public static (string? Material, string? Hatch) Of(IModelView m, Entity e, string key)
    {
        var mat = CorridorKinds.Of(e) is { } kind ? kind.MaterialOf(e, key)
            : e.Class == IfcClass.Pavement ? key switch { "surface" or "kerb" or "sidewalk" or "gutter" => e.Str("surfaceMaterial"), "base" => e.Str("baseMaterial"), _ => null }
            : null;
        if (mat == null) return (null, null);
        var kept = m.Get(mat);
        var look = kept != null ? Lapin.Drawing.MaterialGraphics.Resolve(kept, "section") : GlobalId.IsValid(mat) ? null : Lapin.Drawing.MaterialGraphics.Resolve(null, "section", mat);
        return (kept?.Name ?? mat, look?.Hatch);
    }
}

/// <summary>
/// The corridor of a path element (a path or road, a retaining wall, a
/// ditch): its stations, its red line over the natural and proposed ground,
/// the joints that break it, and its cross-section swept along it, segment by
/// segment, mitred at the corners.
/// </summary>
public static class Corridors
{
    public static bool Is(Entity e) => e.Class == IfcClass.Pavement || e.Class == RetainingWalls.Class || Ditches.Is(e) || CorridorKinds.Of(e) != null;

    public static string KindOf(Entity e) => e.Class == IfcClass.Pavement ? "path" : e.Class == RetainingWalls.Class ? "retainingWall" : CorridorKinds.Of(e)?.Kind ?? "ditch";

    public static Chain ChainOf(Entity e)
    {
        var spec = CorridorSpec.Of(e);
        return spec.Horizontal != null ? Chain.FromSegments(spec.Horizontal, spec.StationStart) : new(Terrains.Contour(e, "path"), spec.Curves, spec.StationStart);
    }

    /// <summary>The terrain a corridor stands on: the one it voids, else the one on its storey, else the site's terrain on another storey (a site has one ground, whatever level it was drawn on).</summary>
    public static Entity? TerrainEntity(IModelView m, Entity e)
    {
        if (Terrains.TerrainOf(m, e.Id) is { } own) return own;
        var terrains = m.OfClass(IfcClass.GeographicElement).Where(t => t.PredefinedType == Terrains.Predefined).ToList();
        var storey = Spatial.StoreyOf(m, e.Id)?.Id;
        return terrains.FirstOrDefault(t => Spatial.StoreyOf(m, t.Id)?.Id == storey) ?? terrains.FirstOrDefault();
    }

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
        var stations = c.StationsBetween(c.Start, c.End, 1.0);
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

    /// <summary>
    /// The cross-section swept along the element: for each stretch between
    /// joints, on each segment, rings every <paramref name="step"/> m lofted
    /// into closed solids that meet the next segment's on the mitre. A part
    /// whose ring changes its count at a station is built from the next ring on.
    /// </summary>
    public static Dictionary<string, List<Solid>> Sweep(Chain c, IReadOnlyList<(double From, double To)> runs, Func<double, int, IReadOnlyList<SectionPart>> sectionAt, double step,
        Func<double, double, List<double>>? stationsBetween = null)
    {
        var o = new Dictionary<string, List<Solid>>();
        if (c.Segments == 0) return o;
        foreach (var (from, to) in runs)
            for (var seg = 0; seg < c.Segments; seg++)
            {
                var (a, b) = (Math.Max(from, c.StationOf(seg)), Math.Min(to, c.StationOf(seg + 1)));
                if (b - a < 1e-6) continue;
                var rings = new List<IReadOnlyList<SectionPart>>();
                var stations = stationsBetween?.Invoke(a, b) ?? c.StationsBetween(a, b, step);
                if (stations.Count < 2) continue;
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

    /// <summary>An outline in plan offset across the path, mitred at its sharp vertices and following its curves: its left edge at <paramref name="left"/>, its right at <paramref name="right"/> (both + left).</summary>
    public static List<Vec2> Outline(Chain c, double left, double right)
    {
        if (c.Segments == 0) return [];
        var l = c.Offset(left); var r = c.Offset(right);
        r.Reverse();
        return [.. l, .. r];
    }

    /// <summary>A line across the element at a station, for a section view: from <paramref name="right"/> to <paramref name="left"/> (+ left), looking along the element.</summary>
    public static (Vec2 Start, Vec2 End) LineAcross(Chain c, double station, double left, double right)
    {
        var p = c.PointAt(station);
        var n = c.DirectionAt(station).Perp;
        return (p + n * right, p + n * left);
    }

    /// <summary>The element's cross-section at a station, and its width either side (+ left, − right).</summary>
    public static (IReadOnlyList<SectionPart> Parts, double Left, double Right) Section(IModelView m, Entity e, double station)
    {
        var c = ChainOf(e);
        var seg = c.SegmentAt(station);
        if (e.Class == IfcClass.Pavement) { var t = Paths.TemplateOf(e); var red = RedLine(m, e, c); return (Paths.SectionAt(t, red(station)), t.Left, -t.Right); }
        if (e.Class == RetainingWalls.Class) { var s = RetainingWalls.Of(e); var levels = RetainingWalls.Levels(m, e, c); return (RetainingWalls.SectionAt(s, levels, station, seg), s.FootingWidth / 2, -s.FootingWidth / 2); }
        if (CorridorKinds.Of(e) is { } kind) return kind.Section(m, e, c, station);
        var d = Ditches.SectionAt(m, e, c, station);
        var half = d.Count > 0 ? d[0].Polygon.Max(p => Math.Abs(p.X)) : 0;
        return (d, half, -half);
    }

    /// <summary>The ground across a station, every <paramref name="step"/> m from <paramref name="right"/> to <paramref name="left"/>: (offset, elevation).</summary>
    public static List<Vec2> GroundAcross(Chain c, Func<Vec2, double?> ground, double station, double left, double right, double step = 0.25)
    {
        var p = c.PointAt(station);
        var n = c.DirectionAt(station).Perp;
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
        var stations = c.StationsBetween(c.Start, c.End, step);
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
