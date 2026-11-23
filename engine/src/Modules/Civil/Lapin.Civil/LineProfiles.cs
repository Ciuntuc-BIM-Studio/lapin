using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Alignments;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// The profile editor's engine side (docs/plan-site-grading.md, T3): one long profile for any line — a feature line (a section
/// line over the terrain is a feature line in section mode), a hardscape's top edge, a path's, wall's or ditch's red line (the
/// corridor's own, not a copy) — and cross sections at stations across any of them. A profile is what the line is in station
/// and elevation: its points (corners or PVIs), its laid line, and the existing (as surveyed) and the design ground under it,
/// both sampled exactly on their triangulations (a point at every crossing of a triangle edge, so a straight piece between two
/// samples is the ground itself). The edits are the feature lines' own commands, plus the point commands here, each one undo
/// step; a hardscape's go through <c>updateHardscape {cornerZ}</c>, a corridor's through <c>setCorridor {profile}</c>.
/// </summary>
public static class LineProfiles
{
    /// <summary>What a cross line square to an axis keeps of where it is: <c>{id: the axis, station}</c>.</summary>
    public const string CrossKey = "crossOf";

    /// <summary>A line as the profile sees it: its plan polyline and stations, its laid line, its points, and the ground's terrain.</summary>
    public sealed record Line(string Kind, Entity Entity, List<Vec2> Path, double[] Stations, double Start, List<(double S, double Z)> Laid,
        List<Point> Points, Entity? Terrain, bool Closed)
    {
        public double Length => Stations.Length == 0 ? 0 : Stations[^1];
        public Vec2 At(double s) => FeatureLines.PointAt(Path, Stations, s - Start);

        /// <summary>The direction along the line at a station (plan, unit).</summary>
        public Vec2 Direction(double s)
        {
            var local = Math.Clamp(s - Start, 0, Length);
            for (var i = 1; i < Path.Count; i++)
                if (local <= Stations[i] + 1e-9 || i == Path.Count - 1)
                {
                    var d = Path[i] - Path[i - 1];
                    if (d.Length > 1e-12) return d * (1 / d.Length);
                }
            return new Vec2(1, 0);
        }

        /// <summary>The laid line's height at a station (linear between its points).</summary>
        public double? ZAt(double s)
        {
            if (Laid.Count == 0) return null;
            if (s <= Laid[0].S) return Laid[0].Z;
            for (var i = 1; i < Laid.Count; i++)
                if (s <= Laid[i].S)
                {
                    var (a, b) = (Laid[i - 1], Laid[i]);
                    return b.S - a.S < 1e-12 ? b.Z : a.Z + (b.Z - a.Z) * (s - a.S) / (b.S - a.S);
                }
            return Laid[^1].Z;
        }
    }

    /// <summary>A point of a profile: its station and height, whether the design gives it (else the ground does), locked, its vertical curve, whether it may move along the station, and its typed grade onwards.</summary>
    public sealed record Point(double Station, double Z, bool Designed, bool Locked, double Curve, bool Movable, double? Grade);

    public static bool Has(Entity e) => FeatureLines.Is(e) || Hardscapes.Is(e) || Corridors.Is(e) || Channels.Is(e);

    /// <summary>The line of an element, or a refusal naming what has a profile.</summary>
    public static Line Of(IModelView m, Entity e)
    {
        if (FeatureLines.Is(e)) return OfFeatureLine(m, e);
        if (Hardscapes.Is(e)) return OfHardscape(m, e);
        if (Corridors.Is(e)) return OfCorridor(m, e);
        if (Channels.Is(e)) return OfChannel(m, e);
        throw new CommandException("A profile is drawn along a feature line, a section line, a hardscape's edge, a channel, or a path's, wall's or ditch's axis.");
    }

    /// <summary>The stations of a feature line's corners along its plan polyline (its arcs in chords).</summary>
    public static double[] CornerStations(FeatureLines.Spec s)
    {
        var flat = FeatureLines.Flat(s);
        var st = FeatureLines.Stations(flat, p => p);
        var o = new double[s.Corners.Count];
        var i = 0;
        for (var k = 0; k < s.Corners.Count; k++)
        {
            while (i < flat.Count && (flat[i] - s.Corners[k]).Length > 1e-9) i++;
            o[k] = i < flat.Count ? st[i] : st[^1];
            if (k == 0 && s.Closed) continue;
        }
        return o;
    }

    static Line OfFeatureLine(IModelView m, Entity e)
    {
        var s = FeatureLines.SpecOf(e);
        var (laid, z, _) = FeatureLines.Resolve(m, e);
        var path = FeatureLines.Flat(s);
        var st = FeatureLines.Stations(path, p => p);
        var cs = CornerStations(s);
        var designed = FeatureLines.Designed(s);
        var points = new List<Point>();
        for (var k = 0; k < s.Corners.Count; k++)
            points.Add(new Point(cs[k], z[k] ?? double.NaN, designed[k], s.LockedAt(k), s.CurveAt(k), Movable(s, k), k < s.Segments ? s.Grades[k] : null));
        var lst = FeatureLines.Stations(laid, p => p.Xy);
        var terrain = m.Get(e.Str(FeatureLines.TerrainKey) ?? "") is { } t && Terrains.IsTerrain(t) ? t : null;
        return new Line("featureLine", e, path, st, 0, [.. laid.Select((p, i) => (lst[i], p.Z))], points, terrain, s.Closed);
    }

    /// <summary>Whether a corner may move along its station: an inner corner on a straight run (its two segments straight and in line), not locked.</summary>
    public static bool Movable(FeatureLines.Spec s, int k)
    {
        var n = s.Corners.Count;
        if (s.LockedAt(k)) return false;
        if (!s.Closed && (k == 0 || k == n - 1)) return false;
        if (s.Closed && n < 4) return false;
        var (a, b, c) = (s.Corners[(k - 1 + n) % n], s.Corners[k], s.Corners[(k + 1) % n]);
        if (Math.Abs(s.Rises[(k - 1 + s.Segments) % s.Segments]) > 1e-9 || Math.Abs(s.Rises[k % s.Segments]) > 1e-9) return false;
        var (u, v) = (b - a, c - b);
        return u.Length > 1e-9 && v.Length > 1e-9 && Math.Abs(Vec2.Cross(u, v)) / (u.Length * v.Length) < 1e-6 && Vec2.Dot(u, v) > 0;
    }

    static Line OfHardscape(IModelView m, Entity e)
    {
        if (Hardscapes.Corners(e).Count < 3) throw new CommandException("A hardscape needs an outline of three corners at least.");
        var top = Hardscapes.Top(e);
        var ring = top.Ring;
        var path = ring.Select(p => p.Xy).Append(ring[0].Xy).ToList();
        var st = FeatureLines.Stations(path, p => p);
        var laid = ring.Append(ring[0]).Select((p, i) => (st[i], p.Z)).ToList();
        var points = top.CornerIndex.Select(i => new Point(st[i], top.Z[i], true, false, 0, false, null)).ToList();
        var terrain = Hardscapes.TerrainOf(m, e) ?? Terrains.Under(m, path[0]);
        return new Line("hardscape", e, path, st, 0, laid, points, terrain, true);
    }

    /// <summary>
    /// A channel's long section (T6): its invert as the line, its ends and its outlets as points (shown, not dragged here: its grade
    /// and start level are set in its properties), the ground under it.
    /// </summary>
    static Line OfChannel(IModelView m, Entity e)
    {
        var l = Channels.LaidOf(m, e);
        if (l.Stations.Count < 2) throw new CommandException(l.Notes.FirstOrDefault() ?? "A channel needs two points at least.");
        var path = l.Stations.Select(l.Chain.PointAt).ToList();
        var st = FeatureLines.Stations(path, p => p);
        var laid = l.Stations.Select((s, i) => (s, l.Invert[i])).ToList();
        var at = new SortedSet<double>([0, l.Length, .. l.Outlets]);
        var points = at.Select(s => new Point(s, l.InvertAt(s), true, true, 0, false, null)).ToList();
        var terrain = m.Get(e.Str("terrain") ?? "") is { } t && Terrains.IsTerrain(t) ? t : Terrains.Under(m, path[0]);
        return new Line("channel", e, path, st, 0, laid, points, terrain, false);
    }

    static Line OfCorridor(IModelView m, Entity e)
    {
        var c = Corridors.ChainOf(e);
        if (c.Segments == 0) throw new CommandException("The element needs two points at least.");
        var stations = c.StationsBetween(c.Start, c.End, Math.Max(0.25, c.Length / 400));
        var path = stations.Select(c.PointAt).ToList();
        var st = FeatureLines.Stations(path, p => p);
        var red = Corridors.RedLine(m, e, c);
        var spec = CorridorSpec.Of(e);
        var design = spec.Mode == "design" && spec.Pvis.Count >= 2;
        // The red line at the axis' stations and through its vertical curves finely.
        var at = new SortedSet<double>(stations);
        if (design)
            foreach (var p in spec.Pvis.Where(p => p.Length > 0))
                for (var q = 0; q <= 16; q++) at.Add(Math.Clamp(p.Station - p.Length / 2 + p.Length * q / 16, c.Start, c.End));
        var laid = at.Select(s => (s - c.Start, red(s))).ToList();
        var points = design ? spec.Pvis.Select((p, i) => new Point(p.Station - c.Start, p.Elevation, true, false, p.Length, i > 0 && i < spec.Pvis.Count - 1, null)).ToList() : [];
        return new Line("corridor", e, path, st, c.Start, laid, points, Corridors.TerrainEntity(m, e), false);
    }

    /// <summary>
    /// A ground along a polyline in plan, sampled exactly on its triangulation: at the polyline's points and at every crossing of
    /// a triangle edge, so the ground between two samples is straight; a stretch off the ground is a gap (null).
    /// </summary>
    public static List<(double S, double? Z)> Sample(Terrain ground, IReadOnlyList<Vec2> path)
    {
        var st = FeatureLines.Stations(path, p => p);
        var o = new List<(double, double?)>();
        foreach (var s in FeatureLines.Crossings(ground, path))
        {
            var p = FeatureLines.PointAt(path, st, s);
            o.Add((s, ground.ElevationAt(p.X, p.Y)));
        }
        return o;
    }

    static JsonArray Pairs(IEnumerable<(double S, double? Z)> xs, double start = 0) =>
        new([.. xs.Select(x => (JsonNode)new JsonArray(Math.Round(x.S + start, 9), x.Z is { } z ? Math.Round(z, 9) : null))]);

    /// <summary>The long profile's data, as the interface and the sheet draw it.</summary>
    public static JsonObject ToJson(IModelView m, Line l)
    {
        var o = new JsonObject
        {
            ["kind"] = l.Kind, ["id"] = l.Entity.Id, ["name"] = l.Entity.Name, ["start"] = Math.Round(l.Start, 6), ["length"] = Math.Round(l.Length, 6),
            ["closed"] = l.Closed,
            ["path"] = new JsonArray([.. l.Path.Select((p, i) => (JsonNode)new JsonArray(Math.Round(p.X, 6), Math.Round(p.Y, 6), Math.Round(l.Stations[i] + l.Start, 6)))]),
            ["line"] = Pairs(l.Laid.Select(x => (x.S, (double?)x.Z)), l.Start),
            ["points"] = new JsonArray([.. l.Points.Select(p => (JsonNode)new JsonObject
            {
                ["station"] = Math.Round(p.Station + l.Start, 6), ["z"] = double.IsFinite(p.Z) ? Math.Round(p.Z, 6) : null, ["designed"] = p.Designed,
                ["locked"] = p.Locked, ["curve"] = Math.Round(p.Curve, 6), ["movable"] = p.Movable, ["grade"] = p.Grade is { } g ? Math.Round(g, 9) : null,
            })]),
        };
        if (l.Terrain is { } t)
        {
            o["terrainId"] = t.Id;
            o["existing"] = Pairs(Sample(Terrains.Existing(t), l.Path), l.Start);
            o["design"] = Pairs(Sample(Terrains.Of(t), l.Path), l.Start);
        }
        if (l.Kind == "featureLine")
        {
            var s = FeatureLines.SpecOf(l.Entity);
            o["section"] = s.Section;
            o["drape"] = s.Drape;
            o["contour"] = s.Elevation != null;
            if (s.Reach is { } r) o["reach"] = r;
            if (s.Section && l.Terrain is { } tt && FeatureLines.SectionChange(s, Terrains.Base(tt)) is { } ch)
            {
                o["reachUsed"] = Math.Round(ch.Reach, 4);
                o["change"] = Math.Round(FeatureLines.MaxChange(ch.Delta), 6);
            }
            if (l.Entity.Attrs[CrossKey] is JsonObject cross) o[CrossKey] = cross.DeepClone();
        }
        if (l.Kind == "corridor") o["mode"] = CorridorSpec.Of(l.Entity).Mode;
        if (l.Kind == "channel")
        {
            // Its top (what it lies in, or the line it follows) above its invert.
            var c = Channels.LaidOf(m, l.Entity);
            o["top"] = Pairs(c.Stations.Select((s, i) => (s, (double?)c.Top[i])));
        }
        return o;
    }

    /// <summary>The cross lines square to an axis (feature lines in section mode made by the cross-section editor), by station.</summary>
    public static IEnumerable<(Entity Line, double Station)> CrossLines(IModelView m, string axisId) =>
        m.OfClass(FeatureLines.Class)
            .Select(e => (e, e.Attrs[CrossKey] as JsonObject))
            .Where(x => x.Item2 != null && (string?)x.Item2["id"] == axisId && Json.Number(x.Item2["station"]) is not null)
            .Select(x => (x.e, Json.Number(x.Item2!["station"])!.Value));

    /// <summary>A cross section's line in plan: from its left end to its right, square to the axis at the station, <paramref name="width"/> each way.</summary>
    public static (Vec2 From, Vec2 To, Vec2 At) Across(Line l, double station, double width)
    {
        var p = l.At(station);
        var d = l.Direction(station);
        var left = new Vec2(-d.Y, d.X);
        return (p + left * width, p - left * width, p);
    }

    /// <summary>A cross section's data: both grounds across (offset: − left, + right), the axis' point and height, and the cross line there if one was made.</summary>
    public static JsonObject Cross(IModelView m, Line l, double station, double width)
    {
        station = Math.Clamp(station, l.Start, l.Start + l.Length);
        var (from, to, at) = Across(l, station, width);
        var path = new List<Vec2> { from, to };
        var o = new JsonObject
        {
            ["station"] = Math.Round(station, 6), ["label"] = Chain.Label(station), ["width"] = width,
            ["at"] = new JsonArray(Math.Round(at.X, 6), Math.Round(at.Y, 6)),
            ["from"] = new JsonArray(Math.Round(from.X, 6), Math.Round(from.Y, 6)), ["to"] = new JsonArray(Math.Round(to.X, 6), Math.Round(to.Y, 6)),
            ["z"] = l.ZAt(station - l.Start) is { } z ? Math.Round(z, 6) : null,
        };
        if (l.Terrain is { } t)
        {
            o["existing"] = Pairs(Sample(Terrains.Existing(t), path), -width);
            o["design"] = Pairs(Sample(Terrains.Of(t), path), -width);
        }
        if (CrossLines(m, l.Entity.Id).Where(x => Math.Abs(x.Station - station) < 1e-3).Select(x => x.Line).FirstOrDefault() is { } cross)
        {
            var cl = OfFeatureLine(m, cross);
            var j = ToJson(m, cl);
            o["crossId"] = cross.Id;
            // In offsets from the axis.
            o["points"] = new JsonArray([.. cl.Points.Select(p => (JsonNode)new JsonObject
            {
                ["offset"] = Math.Round(p.Station - width, 6), ["z"] = double.IsFinite(p.Z) ? Math.Round(p.Z, 6) : null, ["designed"] = p.Designed,
                ["locked"] = p.Locked, ["movable"] = p.Movable,
            })]);
            o["line"] = Pairs(cl.Laid.Select(x => (x.S, (double?)x.Z)), -width);
            if (j["reachUsed"] is { } ru) o["reachUsed"] = ru.DeepClone();
        }
        return o;
    }

    /// <summary>Stations at an interval from the line's start (and its end), and those given, sorted, inside the line.</summary>
    public static List<double> CrossStations(Line l, double? interval, IEnumerable<double> picked)
    {
        var o = new SortedSet<double>();
        var (a, b) = (l.Start, l.Start + l.Length);
        if (interval is { } every && every > 0)
        {
            for (var s = Math.Ceiling(a / every - 1e-9) * every; s <= b + 1e-9; s += every) o.Add(Math.Round(s, 6));
            if (o.Count > 200) o = new SortedSet<double>(o.Take(200));
        }
        foreach (var p in picked) if (p >= a - 1e-9 && p <= b + 1e-9) o.Add(Math.Round(p, 6));
        return [.. o];
    }

    // ─── Edits of a feature line's profile ──────────────────────────────

    static FeatureLines.Spec Spec(Entity e) => FeatureLines.SpecOf(e);

    static int Index(JsonObject a, FeatureLines.Spec s)
    {
        if (Json.Number(a["index"]) is not { } v || v < 0 || v >= s.Corners.Count || v != Math.Floor(v))
            throw new CommandException($"Which point? The line has {s.Corners.Count}, from 0.");
        return (int)v;
    }

    static Entity TerrainOf(IModelView m, Entity line) =>
        m.Get(line.Str(FeatureLines.TerrainKey) ?? "") is { } t && Terrains.IsTerrain(t) ? t : throw new CommandException("The feature line's terrain is gone.");

    static double R(double v) => Math.Round(v, 9);

    /// <summary>The corners' own heights as an edit starts from them: a contour's become its corners' own, a draped line's none.</summary>
    static List<double?> Own(FeatureLines.Spec s) =>
        s.Elevation is { } level ? s.Corners.Select(_ => (double?)level).ToList() : s.Drape ? s.Corners.Select(_ => (double?)null).ToList() : [.. s.Z];

    /// <summary>The attributes of a line edited in its corners: the corners and every per-corner and per-segment list, a contour and a drape given up when heights became the corners' own.</summary>
    static JsonObject Rewrite(Entity e, FeatureLines.Spec was, List<Vec2> corners, List<double?> z, double[] rises, double?[] grades, double[] curves, bool[] locked, bool keepKind)
    {
        var attrs = e.CloneAttrs();
        if (!keepKind) { attrs.Remove("elevation"); attrs.Remove("drape"); }
        attrs["points"] = FeatureLines.PointsJson(corners, z);
        if (rises.Any(r => Math.Abs(r) > 1e-12)) attrs["arcs"] = new JsonArray([.. rises.Select(r => (JsonNode)R(r))]); else attrs.Remove("arcs");
        if (grades.Any(g => g != null)) attrs["grades"] = new JsonArray([.. grades.Select(g => g is { } v ? (JsonNode)R(v) : null)]); else attrs.Remove("grades");
        FeatureLines.WriteCurves(attrs, curves);
        FeatureLines.WriteLocked(attrs, locked);
        return attrs;
    }

    /// <summary>An arc split at a fraction of its length: the two arcs' rises (sagittas), signed as the arc's.</summary>
    public static (double A, double B) SplitRise(PathCurve c, double f)
    {
        if (!c.IsArc) return (0, 0);
        var (r, sweep) = (c.Radius, c.Sweep);
        double Rise(double part) => Math.Sign(c.Rise) * r * (1 - Math.Cos(sweep * part / 2));
        return (Rise(f), Rise(1 - f));
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(ViewClass, "Profile drawing",
        [
            new("exaggeration", "Vertical exaggeration", "number", Min: 1, Max: 20),
            new("scale", "Scale 1:", "number", Min: 1, Max: 5000),
        ], "#b5541c"));
        r.Service<ISheetContent>(new ProfileSheet());

        // The long profile of a line: {id (a feature line, section line, hardscape, path, wall or ditch)}.
        r.Query("profile", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("There is no such element.");
            return ToJson(q.Model, Of(q.Model, e));
        });

        // A cross section across a line at a station: {id, station, width? (each way, default 10 m)}; or {id, stations [..] | interval} for several.
        r.Query("crossProfile", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("There is no such element.");
            var l = Of(q.Model, e);
            var width = Math.Clamp(Json.Number(a["width"]) ?? 10, 0.5, 500);
            if (a["stations"] is JsonArray || Json.Number(a["interval"]) != null)
            {
                var picked = (a["stations"] as JsonArray)?.Select(x => Json.Number(x)).OfType<double>() ?? [];
                return new JsonArray([.. CrossStations(l, Json.Number(a["interval"]), picked).Select(s => (JsonNode)Cross(q.Model, l, s, width))]);
            }
            return Cross(q.Model, l, Json.Number(a["station"]) ?? l.Start, width);
        });

        // A point inserted on a feature line's profile: {id, station, z?}. On a straight segment the corner goes in line; on an arc
        // the arc is split there, each part an arc of the same circle. Without z the line stays as it was (the point takes the line's
        // height there, its own only where the line's design gave it one); a grade over the segment holds on both parts.
        r.Command("insertProfilePoint", "Profile point", (ctx, a) => new JsonObject { ["index"] = LineProfiles.Insert(ctx, a) });

        // A point taken out of a feature line's profile: {id, index}. Its two segments become one, straight unless both were arcs of
        // one circle; a grade over them holds when they had the same one. A line keeps two points at least (three closed).
        r.Command("deleteProfilePoint", "Delete profile point", (ctx, a) =>
        {
            var e = FeatureLines.Require(ctx.Model, (string?)a["id"]);
            var s = Spec(e);
            var k = Index(a, s);
            var n = s.Corners.Count;
            if (s.LockedAt(k)) throw new CommandException(FeatureLines.LockedMessage(k));
            if (n <= (s.Closed ? 3 : 2)) throw new CommandException(s.Closed ? "A closed line keeps three points at least." : "A line keeps its two ends.");
            var own = Own(s);
            var corners = s.Corners.ToList();
            var rises = s.Rises.ToList();
            var grades = s.Grades.ToList();
            var curves = Enumerable.Range(0, n).Select(s.CurveAt).ToList();
            var locked = Enumerable.Range(0, n).Select(s.LockedAt).ToList();
            corners.RemoveAt(k); own.RemoveAt(k); curves.RemoveAt(k); locked.RemoveAt(k);
            if (!s.Closed && k == 0) { rises.RemoveAt(0); grades.RemoveAt(0); curves[0] = 0; }
            else if (!s.Closed && k == n - 1) { rises.RemoveAt(n - 2); grades.RemoveAt(n - 2); curves[^1] = 0; }
            else
            {
                var before = (k - 1 + s.Segments) % s.Segments;
                var merged = s.Grades[before] is { } g1 && s.Grades[k % s.Segments] is { } g2 && Math.Abs(g1 - g2) < 1e-12 ? g1 : (double?)null;
                // One arc again when both parts were of one circle (the same turn, as a split leaves them); else straight.
                var (e0, e1) = (s.Edge(before), s.Edge(k % s.Segments));
                double rise = 0;
                if (e0.IsArc && e1.IsArc && Math.Abs(e0.Radius - e1.Radius) < 1e-6 && (e0.Centre - e1.Centre).Length < 1e-6 && Math.Sign(e0.Rise) == Math.Sign(e1.Rise))
                    rise = PathCurve.RiseThrough(corners[(k - 1 + corners.Count) % corners.Count], corners[k % corners.Count], s.Corners[k]);
                rises[before] = rise; grades[before] = merged;
                rises.RemoveAt(k % s.Segments); grades.RemoveAt(k % s.Segments);
            }
            var attrs = Rewrite(e, s, corners, s.Elevation != null || s.Drape ? [.. corners.Select(_ => (double?)null)] : own, [.. rises], [.. grades], [.. curves], [.. locked], true);
            FeatureLines.Check(attrs, TerrainOf(ctx.Model, e));
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // A point of a feature line's profile dragged: {id, index, z? (its height, own from now), station? (along a straight run,
        // between its neighbours)}: one undo step for both.
        r.Command("moveProfilePoint", "Move profile point", (ctx, a) => { LineProfiles.Move(ctx, a); return null; });

        // A point locked against the profile's edits, or let go: {id, index, locked}.
        r.Command("lockProfilePoint", "Lock profile point", (ctx, a) =>
        {
            var e = FeatureLines.Require(ctx.Model, (string?)a["id"]);
            var s = Spec(e);
            var k = Index(a, s);
            var locked = Enumerable.Range(0, s.Corners.Count).Select(s.LockedAt).ToArray();
            locked[k] = a["locked"] is not JsonValue lv || !lv.TryGetValue<bool>(out var l) || l;
            var attrs = e.CloneAttrs();
            FeatureLines.WriteLocked(attrs, locked);
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // A parabolic vertical curve at a point: {id, index, length (m along the station; 0: none)}.
        r.Command("setProfileCurve", "Vertical curve", (ctx, a) =>
        {
            var e = FeatureLines.Require(ctx.Model, (string?)a["id"]);
            var s = Spec(e);
            var k = Index(a, s);
            if (s.LockedAt(k)) throw new CommandException(FeatureLines.LockedMessage(k));
            var len = Json.Number(a["length"]) ?? throw new CommandException("How long is the curve? length: metres.");
            if (!(len >= 0)) throw new CommandException("A vertical curve's length is 0 or more.");
            var curves = Enumerable.Range(0, s.Corners.Count).Select(s.CurveAt).ToArray();
            curves[k] = len;
            var attrs = e.CloneAttrs();
            FeatureLines.WriteCurves(attrs, curves);
            FeatureLines.Check(attrs, TerrainOf(ctx.Model, e));
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // A section line drawn over a terrain: {points [[x, y]…], arcs?, reach?, terrainId?, name?}: a feature line in section mode.
        r.Command("addSectionLine", "Section line", (ctx, a) =>
        {
            var args = (JsonObject)a.DeepClone();
            args["section"] = true;
            args.Remove("drape"); args.Remove("elevation"); args.Remove("grades");
            // Its corners read the ground: no heights of their own.
            var (corners, _) = FeatureLines.ReadPoints(a["points"]);
            args["points"] = FeatureLines.PointsJson(corners, [.. corners.Select(_ => (double?)null)]);
            return FeatureLines.Add(ctx, args);
        });

        // A point of a cross section dragged or typed: {id (the axis), station, width? (each way, default 10 m), offset (− left, + right),
        // z, reach?}. The first edit at a station makes the cross line there (a section line square to the axis, from its left end to
        // its right, its ends on the ground); a point at the offset is inserted when it has none within a centimetre; its height is set.
        // The ground beside the cross line follows within its reach, as a section line's. One undo step.
        r.Command("setCrossPoint", "Cross section point", (ctx, a) =>
        {
            var m = ctx.Model;
            var axis = m.Get((string?)a["id"] ?? "") ?? throw new CommandException("There is no such element.");
            var l = Of(m, axis);
            var station = Math.Clamp(Json.Number(a["station"]) ?? throw new CommandException("At which station?"), l.Start, l.Start + l.Length);
            var width = Math.Clamp(Json.Number(a["width"]) ?? 10, 0.5, 500);
            var offset = Json.Number(a["offset"]) ?? throw new CommandException("Where across? offset: metres, − left, + right.");
            var z = Json.Number(a["z"]) ?? throw new CommandException("At what height? z.");
            var cross = CrossLines(m, axis.Id).Where(x => Math.Abs(x.Station - station) < 1e-3).Select(x => x.Line).FirstOrDefault();
            if (cross == null)
            {
                if (Math.Abs(offset) >= width - 1e-3) throw new CommandException("The point lies at the cross section's end or beyond: widen it.");
                var (from, to, _) = Across(l, station, width);
                var terrain = l.Terrain ?? Terrains.Under(m, l.At(station)) ?? throw new CommandException("There is no terrain under the cross section.");
                var made = FeatureLines.Add(ctx, new JsonObject
                {
                    ["points"] = new JsonArray(new JsonArray(from.X, from.Y), new JsonArray(to.X, to.Y)), ["section"] = true, ["terrainId"] = terrain.Id,
                    ["name"] = $"Cross section {Chain.Label(station)} · {axis.Name}",
                });
                cross = m.Get((string)made["id"]!)!;
                var attrs = cross.CloneAttrs();
                attrs[CrossKey] = new JsonObject { ["id"] = axis.Id, ["station"] = Math.Round(station, 6), ["width"] = width };
                ctx.Replace(cross.With(attrs: attrs));
                cross = m.Get(cross.Id)!;
            }
            if (a.ContainsKey("reach"))
            {
                var attrs = cross.CloneAttrs();
                if (Json.Number(a["reach"]) is { } rr && rr > 0) attrs["reach"] = R(rr); else attrs.Remove("reach");
                ctx.Replace(cross.With(attrs: attrs));
                cross = m.Get(cross.Id)!;
            }
            var w0 = (cross.Attrs[CrossKey] as JsonObject)?["width"] is { } wv ? Json.Number(wv) ?? width : width;
            var at = offset + w0;
            var cl = OfFeatureLine(m, cross);
            var index = cl.Points.FindIndex(p => Math.Abs(p.Station - at) < 0.01);
            if (index < 0)
            {
                index = Insert(ctx, new JsonObject { ["id"] = cross.Id, ["station"] = at, ["z"] = z });
            }
            else Move(ctx, new JsonObject { ["id"] = cross.Id, ["index"] = index, ["z"] = z });
            return new JsonObject { ["id"] = cross.Id, ["index"] = index };
        });

        // A profile drawing for sheets: {lineId, kind: long | cross, exaggeration?, scale?, interval?, stations?, width?, name?}.
        r.Command("addProfileView", "Profile drawing", (ctx, a) =>
        {
            var line = ctx.Model.Get((string?)a["lineId"] ?? "") ?? throw new CommandException("Which line? lineId.");
            var l = Of(ctx.Model, line);
            var kind = (string?)a["kind"] == "cross" ? "cross" : "long";
            var attrs = new JsonObject { ["line"] = line.Id, ["kind"] = kind };
            WriteView(attrs, a, kind);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : kind == "cross" ? $"Cross sections · {line.Name}" : $"Long profile · {line.Name}";
            var e = new Entity(ctx.NewId(), ViewClass, name, attrs);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id, ["stations"] = kind == "cross" ? new JsonArray([.. ViewStations(l, attrs).Select(s => (JsonNode)s)]) : null };
        });

        // Its settings: {id, exaggeration?, scale?, interval?, stations?, width?, name?}.
        r.Command("updateProfileView", "Change profile drawing", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: ViewClass } v ? v : throw new CommandException("There is no such profile drawing.");
            var attrs = e.CloneAttrs();
            WriteView(attrs, a, e.Str("kind") ?? "long");
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : null;
            ctx.Replace(e.With(name: name, attrs: attrs));
            return null;
        });
    }

    /// <summary>A point inserted (<c>civil.insertProfilePoint</c>): its index.</summary>
    public static int Insert(ICommandContext ctx, JsonObject a)
    {
        var e = FeatureLines.Require(ctx.Model, (string?)a["id"]);
        var s = Spec(e);
        var station = Json.Number(a["station"]) ?? throw new CommandException("Where? station: metres along the line.");
        var cs = CornerStations(s);
        var end = s.Closed ? FeatureLines.Stations(FeatureLines.Flat(s), p => p)[^1] : cs[^1];
        if (station <= 1e-6 || station >= end - 1e-6) throw new CommandException("A point goes between the line's ends.");
        var k = Enumerable.Range(0, s.Segments).Last(q => cs[q] <= station + 1e-9);
        var segEnd = k + 1 < s.Corners.Count ? cs[k + 1] : end;
        if (Math.Abs(station - cs[k]) < 1e-3 || Math.Abs(station - segEnd) < 1e-3) throw new CommandException("There is a point there already.");
        var f = (station - cs[k]) / (segEnd - cs[k]);
        var edge = s.Edge(k);
        var at = edge.PointAt(edge.Length * f);
        var (ra, rb) = SplitRise(edge, f);
        var l = OfFeatureLine(ctx.Model, e);
        var designed = FeatureLines.Designed(s);
        var n = s.Corners.Count;
        var segDesigned = designed[k] && designed[(k + 1) % n];
        double? z = Json.Number(a["z"]) is { } zz ? zz
            : s.Grades[k] != null ? null
            : segDesigned || !s.Section && !s.Drape && s.Elevation == null && (s.Z[k] != null || s.Z[(k + 1) % n] != null) ? l.ZAt(station) : null;
        var own = Own(s);
        var corners = s.Corners.ToList();
        corners.Insert(k + 1, at);
        own.Insert(k + 1, z is { } h ? R(h) : null);
        var rises = s.Rises.ToList(); rises[k] = ra; rises.Insert(k + 1, rb);
        var grades = s.Grades.ToList(); grades.Insert(k + 1, s.Grades[k]);
        if (z != null && a["z"] != null) { grades[k] = null; grades[k + 1] = null; }
        var curves = Enumerable.Range(0, n).Select(s.CurveAt).ToList(); curves.Insert(k + 1, 0);
        var locked = Enumerable.Range(0, n).Select(s.LockedAt).ToList(); locked.Insert(k + 1, false);
        var keepKind = z == null && (s.Drape || s.Elevation != null);
        var attrs = Rewrite(e, s, corners, keepKind ? [.. corners.Select(_ => (double?)null)] : own, [.. rises], [.. grades], [.. curves], [.. locked], keepKind);
        FeatureLines.Check(attrs, TerrainOf(ctx.Model, e));
        ctx.Replace(e.With(attrs: attrs));
        return k + 1;
    }

    /// <summary>A point moved (<c>civil.moveProfilePoint</c>).</summary>
    public static void Move(ICommandContext ctx, JsonObject a)
    {
        var e = FeatureLines.Require(ctx.Model, (string?)a["id"]);
        var s = Spec(e);
        var k = Index(a, s);
        var n = s.Corners.Count;
        if (s.LockedAt(k)) throw new CommandException(FeatureLines.LockedMessage(k));
        var z = Json.Number(a["z"]);
        var station = Json.Number(a["station"]);
        if (z == null && station == null) throw new CommandException("Give the point's height (z), its station, or both.");
        var corners = s.Corners.ToList();
        var cs = CornerStations(s);
        if (station is { } sv && Math.Abs(sv - cs[k]) > 1e-9)
        {
            if (!Movable(s, k)) throw new CommandException(k == 0 || !s.Closed && k == n - 1 ? "A line's ends stay where they are along it: move them in plan." : "A corner where the line bends in plan stays at its station: move it in plan, or insert a point on a straight run.");
            var lo = k == 0 ? 0 : cs[k - 1];
            var hi = k + 1 < n ? cs[k + 1] : FeatureLines.Stations(FeatureLines.Flat(s), p => p)[^1];
            if (sv <= lo + 1e-3 || sv >= hi - 1e-3) throw new CommandException("A point moves between its neighbours along the line.");
            var (a0, b0) = (s.Corners[(k - 1 + n) % n], s.Corners[(k + 1) % n]);
            corners[k] = a0 + (b0 - a0) * ((sv - lo) / (hi - lo));
        }
        var own = Own(s);
        var grades = s.Grades.ToArray();
        var keepKind = z == null;
        if (z is { } h)
        {
            own[k] = R(h);
            if (k > 0) grades[k - 1] = null;
            if (s.Closed && k == 0) grades[s.Segments - 1] = null;
            // A grade out of it gave the next corner its height: that corner keeps the height it had.
            if (k < s.Segments && grades[k] != null)
            {
                var resolved = FeatureLines.CornerZ(s, FeatureLines.GroundOf(ctx.Model, e));
                var j = (k + 1) % n;
                if (own[j] == null && resolved[j] is { } zj) own[j] = R(zj);
                grades[k] = null;
            }
        }
        var attrs = Rewrite(e, s, corners, keepKind && (s.Elevation != null || s.Drape) ? [.. corners.Select(_ => (double?)null)] : own,
            s.Rises, grades, [.. Enumerable.Range(0, n).Select(s.CurveAt)], [.. Enumerable.Range(0, n).Select(s.LockedAt)], keepKind);
        FeatureLines.Check(attrs, TerrainOf(ctx.Model, e));
        ctx.Replace(e.With(attrs: attrs));
    }

    public const string ViewClass = "LapinProfileView";
    public static readonly int[] Exaggerations = [1, 2, 5, 10];

    static void WriteView(JsonObject attrs, JsonObject a, string kind)
    {
        if (Json.Number(a["exaggeration"]) is { } ex) attrs["exaggeration"] = Math.Clamp(ex, 1, 20);
        if (Json.Number(a["scale"]) is { } sc) attrs["scale"] = Math.Clamp(Math.Round(sc), 1, 5000);
        if (a.ContainsKey("interval")) { if (Json.Number(a["interval"]) is { } iv && iv > 0) attrs["interval"] = Math.Max(0.5, iv); else attrs.Remove("interval"); }
        if (a["stations"] is JsonArray st) attrs["stations"] = new JsonArray([.. st.Select(x => Json.Number(x)).OfType<double>().Where(double.IsFinite).Distinct().Order().Select(x => (JsonNode)x)]);
        if (Json.Number(a["width"]) is { } w) attrs["width"] = Math.Clamp(w, 0.5, 500);
        attrs["exaggeration"] ??= kind == "cross" ? 1 : 5;
        attrs["scale"] ??= kind == "cross" ? 100 : 500;
        if (kind == "cross") { attrs["width"] ??= 10; if (attrs["interval"] == null && attrs["stations"] is not JsonArray { Count: > 0 }) attrs["interval"] = 20; }
    }

    public static List<double> ViewStations(Line l, JsonObject attrs) =>
        CrossStations(l, Json.Number(attrs["interval"]), (attrs["stations"] as JsonArray)?.Select(x => Json.Number(x)).OfType<double>() ?? []);
}
