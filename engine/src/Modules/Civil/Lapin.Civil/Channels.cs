using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A standalone drainage channel (rigolă; docs/plan-site-grading.md, T6; forum 230 §5): a section from a catalogue swept along a
/// line drawn (<c>points</c>, <c>arcs</c>) or followed live (<c>along</c>: a feature line, a hardscape's edge, a path's edge, a kerb,
/// <see cref="SiteLines"/>), its top flush with what it lies in (<see cref="SiteLines.Surface"/>: a hardscape's top, else the
/// ground; or the heights of the line it follows), its invert at a constant grade from a start level or a depth under its top.
/// <para>
/// The sections (<see cref="Kinds"/>): an open concrete U channel, an open V channel, a slot drain (a body with a narrow slot to
/// the top), a grated linear drain (a U body and its grating), a cobble swale (a shallow paved dish), or a profile of the project's
/// (drawn in Manage → Profiles, u across the axis, v up from the invert). Its outlets (<c>outlets</c>: stations; none given, its
/// lowest point) get a gully pot; its low points are found each time it is built and marked in plan with its fall and inverts.
/// </para>
/// <para>
/// It cuts the terrain it is given (<c>terrain</c>: an attribute, as a grading names its terrain, so a copy cuts the same one) and
/// the body of any hardscape it runs through (found where it lies, not stored). IFC (Î5): an IfcEarthworksCut TRENCH with its body,
/// as the ditch is; a slot or grated drain in IFC 4.3 and IFC4 an IfcDistributionChamberElement TRENCH: the class IFC has for a
/// covered trench in the ground carrying a distribution service (a "trench drain"), where IfcPipeSegment GUTTER is a roof's gutter.
/// </para>
/// </summary>
public static class Channels
{
    public const string Class = "LapinChannel";
    public const string Predefined = "TRENCH";
    public const string Color = "#a8a59d";
    public const string GratingColor = "#4a4c50";
    /// <summary>How far above its top its cut into the ground and a hardscape reaches (m): what lies over its opening goes.</summary>
    public const double CutAbove = 0.3;
    /// <summary>The longest stretch between two of its sections (m).</summary>
    public const double Step = 0.5;
    /// <summary>The least depth left between its invert and its top (m).</summary>
    public const double MinDepth = 0.02;
    public static readonly string[] Kinds = ["u", "v", "slot", "grated", "swale", "profile"];
    public static readonly string[] Inverts = ["grade", "depth"];
    public static readonly string[] Tops = ["surface", "line"];

    public static bool Is(Entity e) => e.Class == Class;

    /// <summary>A slot or a grated drain: a drain IFC has a class for (<see cref="ExportClass"/>).</summary>
    public static bool IsDrain(string kind) => kind is "slot" or "grated";

    /// <summary>What it is written to IFC as.</summary>
    public static string ExportClass(string kind) => IsDrain(kind) ? "IfcDistributionChamberElement" : IfcClass.EarthworksCut;

    /// <summary>Its sizes: inner width (top opening for V and swale), wall (or paving) thickness, base thickness, nominal depth, grating thickness, slot width, the slot's neck height.</summary>
    public sealed record Size(double Width, double Wall, double Base, double Depth, double Grating, double Slot, double Neck);

    public static Size Defaults(string kind) => kind switch
    {
        "v" => new(0.60, 0.08, 0, 0.15, 0, 0, 0),
        "slot" => new(0.15, 0.05, 0.08, 0.25, 0, 0.015, 0.08),
        "grated" => new(0.15, 0.05, 0.08, 0.20, 0.025, 0, 0),
        "swale" => new(0.80, 0.12, 0, 0.08, 0, 0, 0),
        "profile" => new(0, 0, 0, 0.3, 0, 0, 0),
        _ => new(0.30, 0.10, 0.10, 0.30, 0, 0, 0),
    };

    /// <summary>What a channel asks: its section and sizes, how its invert runs (a grade from a start level, or a depth under its top), what gives its top.</summary>
    public sealed record Spec(string Kind, Size Size, string Invert, double Grade, double? StartZ, string? Top);

    public static Spec SpecOf(Entity e)
    {
        var kind = e.Str("kind") is { } k && Kinds.Contains(k) ? k : "u";
        var d = Defaults(kind);
        double N(string key, double dflt, double min) => Math.Max(min, e.NumOrNull(key) ?? dflt);
        var size = new Size(N("width", d.Width, 0.02), N("wall", d.Wall, 0), N("base", d.Base, 0), N("depth", d.Depth, MinDepth),
            N("grating", d.Grating, 0), N("slot", d.Slot, 0.005), N("neck", d.Neck, 0));
        return new Spec(kind, size, e.Str("invert") is "grade" ? "grade" : "depth", e.NumOrNull("grade") ?? 0, e.NumOrNull("startZ"),
            e.Str("top") is { } t && Tops.Contains(t) ? t : null);
    }

    // ─── Its line ───────────────────────────────────────────────────────

    /// <summary>The channel laid: its axis, its stations (start 0), its top and invert at each, its outlets and low points, what is wrong.</summary>
    public sealed record Laid(Chain Chain, List<double> Stations, List<double> Top, List<double> Invert, List<double> Outlets, List<double> Lows, List<string> Notes)
    {
        public double Length => Chain.Length;
        public double TopAt(double s) => Lerp(Top, s);
        public double InvertAt(double s) => Lerp(Invert, s);

        double Lerp(List<double> v, double s)
        {
            if (Stations.Count == 0) return 0;
            if (s <= Stations[0]) return v[0];
            for (var i = 1; i < Stations.Count; i++)
                if (s <= Stations[i] + 1e-12)
                {
                    var span = Stations[i] - Stations[i - 1];
                    return span < 1e-12 ? v[i] : v[i - 1] + (v[i] - v[i - 1]) * (s - Stations[i - 1]) / span;
                }
            return v[^1];
        }
    }

    /// <summary>Its line in plan and its heights where the line gives them: the line it follows (closed: round to its start again), else its own points.</summary>
    public static (List<Vec2> Plan, List<double?> Z, List<string> Problems) PathOf(IModelView m, Entity e)
    {
        var problems = new List<string>();
        if (e.Attrs["along"] is JsonObject along)
        {
            if (SiteLines.Along(m, along, problems) is not { } l) return ([], [], problems);
            var (plan, z) = (l.Plan.ToList(), l.Z.ToList());
            if (l.Closed && plan.Count > 2) { plan.Add(plan[0]); z.Add(z[0]); }
            return (plan, z, problems);
        }
        var s = FeatureLines.SpecOf(e.Attrs);
        if (s.Corners.Count < 2) return ([], [], ["A channel needs two points at least."]);
        var line = FeatureLines.Line(s, s.Z);
        return ([.. line.Select(p => p.Xy)], [.. line.Select(p => double.IsFinite(p.Z) ? p.Z : (double?)null)], problems);
    }

    /// <summary>The channel as it is laid now (worked out from the model each time it is built: what it follows, the ground under it).</summary>
    public static Laid LaidOf(IModelView m, Entity e)
    {
        var s = SpecOf(e);
        var (plan, z, notes) = PathOf(m, e);
        var clean = new List<Vec2>();
        var zs = new List<double?>();
        for (var i = 0; i < plan.Count; i++)
            if (clean.Count == 0 || (plan[i] - clean[^1]).Length > 1e-9) { clean.Add(plan[i]); zs.Add(z[i]); }
        var c = new Chain(clean);
        if (c.Segments == 0) return new Laid(c, [], [], [], [], [], notes.Count > 0 ? notes : ["A channel needs two points at least."]);
        // Each vertex, and a section every Step between.
        var stations = new List<double>();
        for (var seg = 0; seg < c.Segments; seg++)
            foreach (var st in c.StationsBetween(c.StationOf(seg), c.StationOf(seg + 1), Step))
                if (stations.Count == 0 || st - stations[^1] > 1e-9) stations.Add(st);
        // The top: the line's own heights when it has them all (and is asked to, or nothing says otherwise), else what lies there.
        var useLine = s.Top switch { "line" => zs.All(x => x != null), "surface" => false, _ => zs.All(x => x != null) };
        if (s.Top == "line" && !useLine) notes.Add("The line it follows has no heights everywhere: its top lies on the ground.");
        var vertexAt = Enumerable.Range(0, clean.Count).Select(c.StationOf).ToArray();
        double LineZ(double st)
        {
            for (var i = 1; i < vertexAt.Length; i++)
                if (st <= vertexAt[i] + 1e-12)
                {
                    var span = vertexAt[i] - vertexAt[i - 1];
                    return span < 1e-12 ? zs[i]!.Value : zs[i - 1]!.Value + (zs[i]!.Value - zs[i - 1]!.Value) * (st - vertexAt[i - 1]) / span;
                }
            return zs[^1]!.Value;
        }
        var top = new List<double>();
        double? last = null;
        var missing = false;
        foreach (var st in stations)
        {
            double? h = useLine ? LineZ(st) : SiteLines.Surface(m, c.PointAt(st));
            if (h == null) missing = true;
            var v = h ?? last ?? e.NumOrNull("startZ") ?? 0;
            top.Add(v);
            last = v;
        }
        if (missing) notes.Add("Part of it has no ground under it: its top there is taken from its neighbours.");
        var depth = s.Size.Depth;
        var start = stations[0];
        var z0 = s.StartZ ?? top[0] - depth;
        var invert = stations.Select((st, i) => s.Invert == "grade" ? z0 - s.Grade * (st - start) : top[i] - depth).ToList();
        var high = Enumerable.Range(0, invert.Count).FirstOrDefault(i => invert[i] > top[i] - MinDepth + 1e-9, -1);
        if (high >= 0) notes.Add($"Its invert reaches its top at {stations[high]:0.00} m: the channel is cut down to {MinDepth * 100:0} cm deep there; lower its start or its grade.");
        // Its low points: a stretch of the invert (one station, or several level) with the invert higher on both sides of it, or
        // at an end, higher on its one side; its middle.
        var lows = new List<double>();
        var runs = new List<(int From, int To)>();
        for (var i = 0; i < invert.Count; i++)
            if (runs.Count > 0 && Math.Abs(invert[i] - invert[runs[^1].To]) < 1e-9) runs[^1] = (runs[^1].From, i); else runs.Add((i, i));
        for (var k = 0; k < runs.Count && runs.Count > 1; k++)
        {
            var lowZ = invert[runs[k].From];
            var before = k == 0 || invert[runs[k - 1].From] > lowZ;
            var after = k == runs.Count - 1 || invert[runs[k + 1].From] > lowZ;
            if (before && after) lows.Add((stations[runs[k].From] + stations[runs[k].To]) / 2);
        }
        var n = invert.Count;
        var outlets = (e.Attrs["outlets"] as JsonArray)?.Select(Json.Number).OfType<double>().Where(x => x >= -1e-9 && x <= c.Length + 1e-9).Select(x => Math.Clamp(x, 0, c.Length)).ToList() ?? [];
        if (outlets.Count == 0 && n > 0 && e.Attrs["outlets"] is not JsonArray) outlets.Add(stations[invert.IndexOf(invert.Min())]);
        return new Laid(c, stations, top, invert, outlets, lows, notes);
    }

    // ─── Its section ────────────────────────────────────────────────────

    /// <summary>Half its width outside: its walls' outer faces (a V's and a swale's top edges).</summary>
    public static double OuterHalf(Spec s, IReadOnlyList<Vec2>? profile = null) => s.Kind switch
    {
        "v" or "swale" => s.Size.Width / 2 + s.Size.Wall,
        "profile" => profile is { Count: > 0 } p ? Math.Max(Math.Abs(p.Min(q => q.X)), Math.Abs(p.Max(q => q.X))) : 0.15,
        _ => s.Size.Width / 2 + s.Size.Wall,
    };

    /// <summary>Its section where its top is <paramref name="t"/> and its invert <paramref name="i"/>: u across the axis (+ left), v up; the body and the grating.</summary>
    public static List<SectionPart> SectionAt(Spec s, double t, double i, IReadOnlyList<Vec2>? profile = null)
    {
        i = Math.Min(i, t - MinDepth);
        var z = s.Size;
        var b = z.Width / 2;
        var w = z.Wall;
        var o = new List<SectionPart>();
        switch (s.Kind)
        {
            case "v":
                o.Add(new("body", [new(0, i - w), new(b + w, t), new(b, t), new(0, i), new(-b, t), new(-b - w, t)]));
                break;
            case "swale":
            {
                // A shallow dish: a parabola from its two edges at the top down to its invert on the axis; the cobbles under it, their thickness.
                const int k = 8;
                var top = Enumerable.Range(0, k + 1).Select(q => { var u = -b + 2 * b * q / k; return new Vec2(u, i + (t - i) * (u / b) * (u / b)); }).ToList();
                var ring = new List<Vec2>(top);
                ring.AddRange(Enumerable.Range(0, k + 1).Reverse().Select(q => top[q] with { Y = top[q].Y - w }));
                o.Add(new("body", ring));
                break;
            }
            case "slot":
            {
                var half = Math.Max(z.Slot / 2, 0.0025);
                var neck = Math.Clamp(z.Neck, 0, Math.Max(0, t - i - MinDepth));
                var nz = t - neck;
                o.Add(new("body", [new(-b - w, i - z.Base), new(b + w, i - z.Base), new(b + w, t), new(half, t), new(half, nz), new(b, nz), new(b, i), new(-b, i), new(-b, nz), new(-half, nz), new(-half, t), new(-b - w, t)]));
                break;
            }
            case "profile" when profile is { Count: >= 3 } p:
                o.Add(new("body", [.. p.Select(q => new Vec2(q.X, i + q.Y))]));
                break;
            default:
                // U (and the grated drain's body): two walls and a base, open between its walls up to its top.
                o.Add(new("body", [new(-b - w, i - z.Base), new(b + w, i - z.Base), new(b + w, t), new(b, t), new(b, i), new(-b, i), new(-b, t), new(-b - w, t)]));
                if (s.Kind == "grated" && z.Grating > 0) o.Add(new("grating", [new(-b, t - z.Grating), new(b, t - z.Grating), new(b, t), new(-b, t)]));
                break;
        }
        return o;
    }

    /// <summary>What it takes out of what it lies in at a section: its outer shape, closed over its top <paramref name="above"/> higher.</summary>
    public static List<Vec2> EnvelopeAt(Spec s, double t, double i, double above, IReadOnlyList<Vec2>? profile = null)
    {
        i = Math.Min(i, t - MinDepth);
        var z = s.Size;
        var h = OuterHalf(s, profile);
        var up = t + above;
        switch (s.Kind)
        {
            case "v":
                return [new(0, i - z.Wall), new(h, t), new(h, up), new(-h, up), new(-h, t)];
            case "swale":
            {
                const int k = 8;
                var b = z.Width / 2;
                var bottom = Enumerable.Range(0, k + 1).Select(q => { var u = -b + 2 * b * q / k; return new Vec2(u, i + (t - i) * (u / b) * (u / b) - z.Wall); }).ToList();
                return [.. bottom, new(b, up), new(-b, up)];
            }
            case "profile" when profile is { Count: >= 3 } p:
                return [new(-h, i + p.Min(q => q.Y)), new(h, i + p.Min(q => q.Y)), new(h, up), new(-h, up)];
            default:
                return [new(-h, i - z.Base), new(h, i - z.Base), new(h, up), new(-h, up)];
        }
    }

    /// <summary>A project profile it is swept with, its points (u across, v up from the invert); null for the catalogue's sections.</summary>
    public static List<Vec2>? Profile(IModelView m, Entity e, Spec s) =>
        s.Kind == "profile" && Profiles.Of(m, e) is { } own && own.Outline() is { Count: >= 3 } o ? [.. o] : null;

    /// <summary>
    /// Parts swept along the axis: each part's rings at every station of a stretch between two vertices, lofted into one tube per
    /// stretch (mitred where it turns, as a corridor is), a part whose ring changes its count starting a new tube.
    /// </summary>
    static Dictionary<string, List<Solid>> Tubes(Laid l, Func<int, IReadOnlyList<SectionPart>> at)
    {
        var o = new Dictionary<string, List<Solid>>();
        var c = l.Chain;
        for (var seg = 0; seg < c.Segments; seg++)
        {
            var (a, b) = (c.StationOf(seg), c.StationOf(seg + 1));
            var idx = Enumerable.Range(0, l.Stations.Count).Where(k => l.Stations[k] >= a - 1e-9 && l.Stations[k] <= b + 1e-9).ToList();
            if (idx.Count < 2) continue;
            var open = new Dictionary<(string, int), List<IReadOnlyList<Vec3>>>();
            void Close((string, int) key)
            {
                if (open.Remove(key, out var rings) && SiteGeometry.LoftMany(rings, key.Item1) is { } tube)
                    (o.TryGetValue(key.Item1, out var list) ? list : o[key.Item1] = []).Add(tube);
            }
            foreach (var k in idx)
            {
                var st = l.Stations[k];
                var (p, u) = (c.PointAt(st), c.Across(st, seg));
                var seen = new Dictionary<string, int>();
                foreach (var part in at(k))
                {
                    var key = (part.Key, seen[part.Key] = seen.GetValueOrDefault(part.Key, -1) + 1);
                    var ring = part.Polygon.Select(x => (p + u * x.X).At(x.Y)).ToList();
                    if (open.TryGetValue(key, out var rings) && rings[^1].Count != ring.Count) Close(key);
                    (open.TryGetValue(key, out var r) ? r : open[key] = []).Add(ring);
                }
            }
            foreach (var key in open.Keys.ToList()) Close(key);
        }
        return o;
    }

    /// <summary>A gully's pot at an outlet: its square in plan, the side of it.</summary>
    static (List<Vec2> Square, double Side) Pot(Laid l, Spec s, double station, IReadOnlyList<Vec2>? profile)
    {
        var side = Math.Max(0.4, 2 * OuterHalf(s, profile) + 0.1);
        var p = l.Chain.PointAt(station);
        var d = l.Chain.DirectionAt(station);
        var n = d.Perp;
        var h = side / 2;
        return ([p - d * h - n * h, p + d * h - n * h, p + d * h + n * h, p - d * h + n * h], side);
    }

    /// <summary>How deep a gully's pot goes under the invert at its outlet (m), and its walls' and floor's thickness.</summary>
    const double PotDepth = 0.45, PotWall = 0.06;

    /// <summary>The channel as solids: its body and grating swept along it, a gully's pot at each outlet.</summary>
    public static Dictionary<string, List<Solid>> Solids(IModelView m, Entity e)
    {
        var l = LaidOf(m, e);
        var s = SpecOf(e);
        if (l.Stations.Count < 2) return [];
        var profile = Profile(m, e, s);
        var o = Tubes(l, k => SectionAt(s, l.Top[k], l.Invert[k], profile));
        foreach (var st in l.Outlets)
        {
            var (sq, side) = Pot(l, s, st, profile);
            var (t, i) = (l.TopAt(st), Math.Min(l.InvertAt(st), l.TopAt(st) - MinDepth));
            var inner = Polyline2.Offset(sq, -PotWall, closed: true);
            // Its walls round an open pit, its floor under it.
            if (Prism.ExtrudeFootprint(Polygon2.EnsureCcw(sq), [Polygon2.EnsureCcw(inner)], i - PotDepth, t - (i - PotDepth), "outlet") is { } walls) Add(o, "outlet", walls);
            if (Prism.ExtrudeFootprint(Polygon2.EnsureCcw(inner), i - PotDepth - PotWall, PotWall, "outlet") is { } floor) Add(o, "outlet", floor);
            if (IsDrain(s.Kind) && Prism.ExtrudeFootprint(Polygon2.EnsureCcw(inner), t - Math.Max(0.02, s.Size.Grating), Math.Max(0.02, s.Size.Grating), "grating") is { } grate) Add(o, "grating", grate);
        }
        return o;
    }

    static void Add(Dictionary<string, List<Solid>> o, string key, Solid s) => (o.TryGetValue(key, out var list) ? list : o[key] = []).Add(s);

    /// <summary>What it takes out of the terrain or a hardscape: its outer shape from its bottom to <see cref="CutAbove"/> over its top, and its gullies' pits.</summary>
    public static List<Solid> Cutters(IModelView m, Entity e, double above = CutAbove)
    {
        var l = LaidOf(m, e);
        var s = SpecOf(e);
        if (l.Stations.Count < 2) return [];
        var profile = Profile(m, e, s);
        var o = Tubes(l, k => [new("cut", EnvelopeAt(s, l.Top[k], l.Invert[k], above, profile))]).Values.SelectMany(x => x).ToList();
        foreach (var st in l.Outlets)
        {
            var (sq, _) = Pot(l, s, st, profile);
            var (t, i) = (l.TopAt(st), Math.Min(l.InvertAt(st), l.TopAt(st) - MinDepth));
            var z0 = i - PotDepth - PotWall;
            if (Prism.ExtrudeFootprint(Polygon2.EnsureCcw(sq), z0, t + above - z0, "cut") is { } pit) o.Add(pit);
        }
        return o;
    }

    /// <summary>The channels on a terrain (those it is given to cut).</summary>
    public static IEnumerable<Entity> Of(IModelView m, string terrainId) => m.OfClass(Class).Where(e => e.Str("terrain") == terrainId);

    /// <summary>The channels whose plan box meets a plan outline's: those that may run through a hardscape.</summary>
    public static IEnumerable<Entity> Over(IModelView m, IReadOnlyList<Vec2> outline)
    {
        if (outline.Count < 3) yield break;
        var (x0, y0, x1, y1) = (outline.Min(p => p.X), outline.Min(p => p.Y), outline.Max(p => p.X), outline.Max(p => p.Y));
        foreach (var e in m.OfClass(Class))
        {
            var l = LaidOf(m, e);
            if (l.Stations.Count < 2) continue;
            var h = OuterHalf(SpecOf(e)) + 0.5;
            var pts = l.Chain.Path;
            if (pts.Max(p => p.X) + h < x0 || pts.Min(p => p.X) - h > x1 || pts.Max(p => p.Y) + h < y0 || pts.Min(p => p.Y) - h > y1) continue;
            yield return e;
        }
    }

    // ─── Geometry, plan, quantities ─────────────────────────────────────

    static readonly (string Key, string Name)[] PartNames = [("body", "Channel"), ("grating", "Grating"), ("outlet", "Gully")];

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices _)
    {
        var l = LaidOf(m, e);
        if (l.Stations.Count < 2) return new ElementGeometry([], Diagnostics: l.Notes.Count > 0 ? l.Notes : ["A channel needs two points at least."]);
        var s = SpecOf(e);
        var solids = Solids(m, e);
        var material = e.Str("material");
        var grating = e.Str("gratingMaterial");
        var parts = new List<GeometryPart>();
        foreach (var (key, name) in PartNames)
        {
            if (!solids.TryGetValue(key, out var list) || list.Count == 0) continue;
            var mat = key == "grating" ? grating : material;
            var color = mat != null ? Materials.ColorOf(m, mat) : key == "grating" ? GratingColor : Color;
            parts.Add(new GeometryPart(name, color, Paths.Combine(list, key), mat, key));
        }
        return new ElementGeometry(parts, Plan(m, e, l, s), [l.Chain.Path[0], l.Chain.Path[^1]], l.Notes.Count > 0 ? l.Notes : null);
    }

    /// <summary>In plan: its outer edges, its inner edges (a slot drain's slot), a grating's bars across, a swale's middle, its gullies.</summary>
    static List<PlanItem> Plan(IModelView m, Entity e, Laid l, Spec s)
    {
        var c = l.Chain;
        var profile = Profile(m, e, s);
        var outer = OuterHalf(s, profile);
        var o = new List<PlanItem> { new("heavy", false, c.Offset(outer)), new("heavy", false, c.Offset(-outer)) };
        switch (s.Kind)
        {
            case "slot":
                o.Add(new("symbol", false, c.Offset(Math.Max(s.Size.Slot / 2, 0.0025))));
                o.Add(new("symbol", false, c.Offset(-Math.Max(s.Size.Slot / 2, 0.0025))));
                break;
            case "swale":
                o.Add(new("symbol", false, c.Axis()));
                break;
            case "profile":
                break;
            default:
            {
                var inner = s.Size.Width / 2;
                o.Add(new("symbol", false, c.Offset(inner)));
                o.Add(new("symbol", false, c.Offset(-inner)));
                if (s.Kind == "grated")
                {
                    // The grating's bars across, every 10 cm (at most 2000).
                    var step = Math.Max(0.1, c.Length / 2000);
                    for (var st = step / 2; st < c.Length; st += step)
                    {
                        var p = c.PointAt(st);
                        var n = c.DirectionAt(st).Perp;
                        o.Add(new("symbol", false, [p + n * inner, p - n * inner]));
                    }
                }
                break;
            }
        }
        foreach (var st in l.Outlets)
        {
            var (sq, _) = Pot(l, s, st, profile);
            o.Add(new("heavy", true, sq));
            o.Add(new("symbol", false, [sq[0], sq[2]]));
            o.Add(new("symbol", false, [sq[1], sq[3]]));
        }
        return o;
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null) yield break;
        var l = LaidOf(m, e);
        if (l.Stations.Count < 2) yield break;
        var s = SpecOf(e);
        double Vol(string key) => g.Parts.Where(p => p.Key == key).Sum(p => Measure.Volume(p.Solid));
        var depths = l.Stations.Select((_, i) => l.Top[i] - l.Invert[i]).ToList();
        var excavation = Cutters(m, e, 0).Sum(Measure.Volume);
        yield return new QuantitySet("Lapin_ChannelQuantities",
        [
            new("Length", l.Length, "LENGTH", "Length"),
            new("Volume", Vol("body"), "VOLUME", "Volume"),
            new("ExcavationVolume", excavation, "VOLUME", "Excavation"),
            new("GratingLength", s.Kind == "grated" ? l.Length : 0, "LENGTH", "Grating"),
            new("StartInvert", l.Invert[0], "LENGTH", "Invert at start"),
            new("EndInvert", l.Invert[^1], "LENGTH", "Invert at end"),
            new("Fall", l.Invert[0] - l.Invert[^1], "LENGTH", "Fall"),
            new("MinDepth", depths.Min(), "LENGTH", "Least depth"),
            new("MaxDepth", depths.Max(), "LENGTH", "Greatest depth"),
            new("Outlets", l.Outlets.Count, "COUNT", "Gullies"),
        ]);
    }

    // ─── Commands ───────────────────────────────────────────────────────

    static double R(double v) => Math.Round(v, 9);

    /// <summary>
    /// What a channel command sets: {kind?, width?, wall?, base?, depth?, grating?, slot?, neck?, invert? (grade | depth), grade? (0.005,
    /// „0.5 %”, „1:200"; positive falls from its start), startZ? (null: the top at its start less its depth), top? (surface | line),
    /// outlets? [stations] (empty: none; null: its lowest point), points? + arcs? | along? {id, edge?, side?, offset?}, profileId?,
    /// material?, gratingMaterial?, terrainId? | standalone?}.
    /// </summary>
    static void Apply(ICommandContext ctx, JsonObject attrs, JsonObject a)
    {
        var m = ctx.Model;
        if ((string?)a["kind"] is { } kind)
        {
            if (!Kinds.Contains(kind)) throw new CommandException($"A channel is {string.Join(", ", Kinds)}.");
            if (attrs["kind"]?.ToString() != kind)
                // Another section: its own sizes, unless given.
                foreach (var k in new[] { "width", "wall", "base", "depth", "grating", "slot", "neck" }) attrs.Remove(k);
            attrs["kind"] = kind;
        }
        foreach (var k in new[] { "width", "wall", "base", "depth", "grating", "slot", "neck" })
            if (a[k] != null)
            {
                if (Json.Number(a[k]) is not { } v || v < 0 || v > 5) throw new CommandException($"A channel's {k} is between 0 and 5 m.");
                attrs[k] = R(v);
            }
        if ((string?)a["invert"] is { } inv) { if (!Inverts.Contains(inv)) throw new CommandException("A channel's invert runs at a grade or at a depth under its top."); attrs["invert"] = inv; }
        if (a["grade"] != null)
        {
            var g = TopSurface.Grade(a["grade"]) ?? throw new CommandException("A grade is a fraction (0.005), a percentage (0.5 %) or one in n (1:200).");
            if (Math.Abs(g) > 0.5) throw new CommandException("A channel's grade is at most 50 %.");
            attrs["grade"] = R(g);
            if ((string?)a["invert"] == null) attrs["invert"] = "grade";
        }
        if (a.ContainsKey("startZ")) { if (Json.Number(a["startZ"]) is { } z) attrs["startZ"] = R(z); else attrs.Remove("startZ"); }
        if (a.ContainsKey("top")) { if ((string?)a["top"] is { } t && Tops.Contains(t)) attrs["top"] = t; else attrs.Remove("top"); }
        if (a.ContainsKey("outlets"))
        {
            if (a["outlets"] is JsonArray oa) attrs["outlets"] = new JsonArray([.. oa.Select(Json.Number).OfType<double>().Where(x => x >= 0).Select(x => (JsonNode)R(x))]);
            else attrs.Remove("outlets");
        }
        if (a["along"] != null)
        {
            attrs["along"] = SiteLines.AlongArg(m, a["along"]);
            attrs.Remove("points");
            attrs.Remove("arcs");
        }
        else if (a["points"] != null)
        {
            var (c, z) = FeatureLines.ReadPoints(a["points"]);
            if (c.Count < 2) throw new CommandException("A channel needs two points at least.");
            attrs["points"] = FeatureLines.PointsJson(c, z);
            if (a["arcs"] is JsonArray arcs) attrs["arcs"] = arcs.DeepClone(); else attrs.Remove("arcs");
            attrs.Remove("along");
        }
        if (a.ContainsKey(Profiles.IdKey))
        {
            if ((string?)a[Profiles.IdKey] is { Length: > 0 } pid)
            {
                var p = Spatial.Require(m, pid, Profiles.Class);
                Profiles.Assign(attrs, Profiles.Read(p) ?? throw new CommandException($"„{p.Name}” has no outline."), p.Id);
                attrs["kind"] = "profile";
            }
            else { attrs.Remove(Profiles.IdKey); attrs.Remove(Profiles.Key); if (attrs["kind"]?.ToString() == "profile") attrs["kind"] = "u"; }
        }
        var kindNow = (string?)attrs["kind"] ?? "u";
        if (kindNow == "profile" && attrs[Profiles.Key] == null) throw new CommandException("A channel of a drawn profile needs the profile: give its profileId.");
        // Its material: the one given (its own from then on), else its section's own until one is given.
        if ((string?)a["material"] is { Length: > 0 } mat) { attrs["material"] = Materials.Ensure(ctx, mat, "#a5a29b").Id; attrs.Remove("materialAuto"); }
        else if (attrs["material"] == null || attrs["materialAuto"] is JsonValue)
        {
            attrs["material"] = DefaultMaterial(ctx, kindNow).Id;
            attrs["materialAuto"] = true;
        }
        if ((string?)a["gratingMaterial"] is { Length: > 0 } gm) attrs["gratingMaterial"] = Materials.Ensure(ctx, gm, GratingColor).Id;
        else if (attrs["gratingMaterial"] == null && IsDrain(kindNow)) attrs["gratingMaterial"] = Materials.Ensure(ctx, "Cast iron grating", GratingColor).Id;
        attrs["exportAs"] = ExportClass(kindNow);
    }

    /// <summary>The material a section is made of until another is given: concrete, polymer concrete for a drain, cobbles for a swale.</summary>
    static Entity DefaultMaterial(ICommandContext ctx, string kind) => kind switch
    {
        "slot" or "grated" => Materials.Ensure(ctx, "Polymer concrete", "#8f8c86"),
        "swale" => Materials.Ensure(ctx, "Cobbles", "#9b938a"),
        _ => Materials.Ensure(ctx, "Concrete channel", "#a5a29b"),
    };

    /// <summary>civil.addChannel: the channel made, on the terrain under it unless standalone. Its id and its terrain.</summary>
    public static JsonObject Add(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        if (a["points"] == null && a["along"] == null) throw new CommandException("A channel runs along points drawn or along an element: give points or along.");
        var attrs = new JsonObject { ["kind"] = "u" };
        Channels.Apply(ctx, attrs, a);
        var n = m.OfClass(Class).Count() + 1;
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Channel {n}";
        var probe = new Entity("probe", Class, name, (JsonObject)attrs.DeepClone(), Predefined);
        var (plan, _, problems) = PathOf(m, probe);
        if (plan.Count < 2) throw new CommandException(problems.FirstOrDefault() ?? "A channel needs two points at least.");
        var standalone = a["standalone"] is JsonValue sv && sv.TryGetValue<bool>(out var st) && st;
        var terrain = standalone ? null : (string?)a["terrainId"] is { Length: > 0 } tid ? Terrains.Require(m, tid) : SiteLines.TerrainUnder(m, plan);
        if (terrain != null) attrs["terrain"] = terrain.Id;
        var e = new Entity(ctx.NewId(), Class, name, attrs, Predefined);
        ctx.Add(e);
        var storey = (string?)a["storeyId"] ?? (terrain != null ? Spatial.StoreyOf(m, terrain.Id)?.Id : null) ?? Spatial.Storeys(m).FirstOrDefault()?.Id
            ?? throw new CommandException("The project has no storey.");
        Spatial.Contain(ctx, storey, e.Id);
        var o = new JsonObject { ["id"] = e.Id };
        if (terrain != null) o["terrainId"] = terrain.Id;
        var notes = LaidOf(ctx.Model, ctx.Model.Get(e.Id)!).Notes;
        if (notes.Count > 0) o["notes"] = new JsonArray([.. notes.Select(x => (JsonNode)x)]);
        return o;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Channel",
        [
            new("kind", "Section", "choice", Choices: Kinds),
            new("width", "Width", "length", Min: 0.02, Max: 5),
            new("depth", "Depth", "length", Min: MinDepth, Max: 5),
            new("invert", "Invert", "choice", Choices: Inverts),
        ], Color));
        r.Geometry(Class, Geometry);
        r.Quantities(Class, Quantities);
        Lapin.Drawing.Views.Generator.SymbolicInPlan.Add(Class);
        SitePlans.SiteClasses.Add(Class);
        r.Service<INodeProps>(new Props());
        r.Service<IElementPlacement>(new LinePlacement(Class));
        r.Service<IElementRise>(new Rise());
        r.Service<IScheduleSource>(new Schedule());
        r.Catalog("channelKinds", new JsonArray([.. Kinds.Select(k =>
        {
            var d = Defaults(k);
            return (JsonNode)new JsonObject { ["key"] = k, ["width"] = d.Width, ["wall"] = d.Wall, ["base"] = d.Base, ["depth"] = d.Depth, ["grating"] = d.Grating, ["slot"] = d.Slot, ["drain"] = IsDrain(k) };
        })]));

        // A channel (T6): {points [[x, y] | [x, y, z]…], arcs? | along {id, edge?, side?, offset?}, kind?, sizes?, invert?, grade?,
        // startZ?, top?, outlets?, profileId?, material?, gratingMaterial?, terrainId? | standalone?, name?, storeyId?}.
        r.Command("addChannel", "Channel", (ctx, a) => Channels.Add(ctx, a));

        // Changed any time, one undo step: {id, …as addChannel, terrainId? (cut into that one), standalone? (cuts nothing)}.
        r.Command("updateChannel", "Change channel", (ctx, a) =>
        {
            var e = Spatial.Require(ctx.Model, (string?)a["id"], Class);
            var attrs = e.CloneAttrs();
            Channels.Apply(ctx, attrs, a);
            if (a["standalone"] is JsonValue sv && sv.TryGetValue<bool>(out var st) && st) attrs.Remove("terrain");
            else if ((string?)a["terrainId"] is { Length: > 0 } tid) attrs["terrain"] = Terrains.Require(ctx.Model, tid).Id;
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : null;
            ctx.Replace(e.With(attrs: attrs, name: name));
            return null;
        });
    }

    /// <summary>What the interface shows of it: its length, inverts, fall, depths, outlets and low points (stations), its notes.</summary>
    sealed class Props : INodeProps
    {
        public void Add(Entity e, IModelView m, JsonObject props)
        {
            if (!Is(e)) return;
            var l = LaidOf(m, e);
            if (l.Stations.Count < 2) return;
            props["length"] = Math.Round(l.Length, 6);
            props["startInvert"] = Math.Round(l.Invert[0], 6);
            props["endInvert"] = Math.Round(l.Invert[^1], 6);
            props["startTop"] = Math.Round(l.Top[0], 6);
            props["endTop"] = Math.Round(l.Top[^1], 6);
            var depths = l.Stations.Select((_, i) => l.Top[i] - l.Invert[i]).ToList();
            props["minDepth"] = Math.Round(depths.Min(), 6);
            props["maxDepth"] = Math.Round(depths.Max(), 6);
            props["outletStations"] = new JsonArray([.. l.Outlets.Select(x => (JsonNode)Math.Round(x, 6))]);
            props["lowStations"] = new JsonArray([.. l.Lows.Select(x => (JsonNode)Math.Round(x, 6))]);
            if (l.Notes.Count > 0) props["notes"] = new JsonArray([.. l.Notes.Select(x => (JsonNode)x)]);
            if (e.Str("terrain") is { } t) props["terrainId"] = t;
        }
    }

    /// <summary>A 3D move's vertical part raises its start level (a channel at a grade); one at a depth follows its top.</summary>
    sealed class Rise : IElementRise
    {
        public bool Handles(Entity e) => Is(e);

        public bool Lift(ICommandContext ctx, Entity e, double dz)
        {
            var s = SpecOf(e);
            if (s.Invert != "grade") return false;
            var attrs = e.CloneAttrs();
            attrs["startZ"] = R((s.StartZ ?? LaidOf(ctx.Model, e).Invert.FirstOrDefault()) + dz);
            ctx.Replace(e.With(attrs: attrs));
            return true;
        }
    }

    /// <summary>The channel schedule: each one's section, length, grade, inverts, volume, excavation, grating.</summary>
    sealed class Schedule : IScheduleSource
    {
        public string Kind => "channel";
        public string Label => "Channel schedule";

        public ScheduleGrid Grid(IQueryContext q)
        {
            var m = q.Model;
            string N(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);
            var rows = new List<IReadOnlyList<string>>();
            foreach (var e in m.OfClass(Class))
            {
                var l = LaidOf(m, e);
                if (l.Stations.Count < 2) continue;
                var s = SpecOf(e);
                var set = q.Quantities(e.Id).FirstOrDefault(x => x.Name == "Lapin_ChannelQuantities");
                double Qv(string name) => set?.Quantities.FirstOrDefault(x => x.Name == name)?.Value ?? 0;
                var grade = s.Invert == "grade" ? $"{N(s.Grade * 100, "0.0#")} %" : $"{N(s.Size.Depth * 100, "0.#")} cm deep";
                rows.Add([e.Name, s.Kind, N(l.Length), grade, N(l.Invert[0], "0.000"), N(l.Invert[^1], "0.000"), N(Qv("Volume"), "0.000"), N(Qv("ExcavationVolume"), "0.000"), s.Kind == "grated" ? N(l.Length) : ""]);
            }
            return new ScheduleGrid(Label, ["Name", "Section", "Length (m)", "Grade", "Start invert", "End invert", "Volume (m³)", "Excavation (m³)", "Grating (m)"], rows);
        }
    }
}

/// <summary>
/// Moved, turned and mirrored as any element: a channel's or a standalone kerb's own points (their heights kept), its arcs bulging
/// the other way when mirrored. One that follows another element's line follows it still.
/// </summary>
public sealed class LinePlacement(string cls) : IElementPlacement
{
    public bool Handles(Entity e) => e.Class == cls && (cls != Kerbs.Class || e.Str(Kerbs.HostKey) == null);

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        if (e.Attrs["points"] == null) return;
        var s = FeatureLines.SpecOf(e.Attrs);
        var attrs = e.CloneAttrs();
        attrs["points"] = FeatureLines.PointsJson([.. s.Corners.Select(c => t.Point(c))], s.Z);
        if (t.Mirrored && attrs["arcs"] is JsonArray arcs) attrs["arcs"] = new JsonArray([.. arcs.Select(x => Json.Number(x) is { } v ? (JsonNode)(-v) : null)]);
        // A standalone kerb's side: mirrored, its paving is on the other side of its line.
        if (t.Mirrored && cls == Kerbs.Class) attrs["side"] = e.Str("side") == "left" ? "right" : "left";
        ctx.Replace(e.With(attrs: attrs));
    }
}
