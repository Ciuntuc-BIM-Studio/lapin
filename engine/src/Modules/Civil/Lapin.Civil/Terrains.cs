using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// The terrain: an IfcGeographicElement TERRAIN holding its survey points
/// (x, y in plan, z the surveyed elevation) and the datum the elevations are
/// read from (the project's ±0.00 as a surveyed elevation: z − datum is the
/// model's height). Its body is the ground as a solid, the TIN on top, its
/// hull down to a base <c>depth</c> below the lowest point, so sections cut
/// it as earth. Platforms (IfcEarthworksCut voiding it) level it inside their
/// outline at their elevation: what is above is cut, what is below filled.
/// In plan it draws its contours, every <c>interval</c>, every
/// <c>majorEvery</c>-th one heavier, its edge and the platforms' outlines.
/// </summary>
public static class Terrains
{
    public const string Predefined = "TERRAIN";
    public const string Color = "#7a9a5a";
    public const string BoundaryKey = "boundary";
    public const string BoundaryArcs = "boundaryArcs";
    /// <summary>The finest contour interval (m), as the configurator's field: a finer one asked is taken as this.</summary>
    public const double MinInterval = 0.05;

    /// <summary>A contour interval asked, brought up to <see cref="MinInterval"/>; null when none was asked.</summary>
    public static double? Interval(double? asked) => asked is { } v ? Math.Max(MinInterval, v) : null;

    /// <summary>Whether an element is a terrain (not another geographic element).</summary>
    public static bool IsTerrain(Entity e) => e.Class == IfcClass.GeographicElement && e.PredefinedType == Predefined;

    public static List<Vec3> Points(Entity e) => PointLists.Xyz(e.Attrs["points"]);

    public static List<Vec3> ExistingPoints(Entity e) =>
        e.Attrs["existingPoints"] is JsonArray arr && arr.Count >= 3 ? PointLists.Xyz(arr) : Points(e);

    public static JsonArray PointsJson(IEnumerable<Vec3> pts) => new(pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y, p.Z)).ToArray());

    public static IReadOnlyList<Vec2> BoundaryOutline(Entity e) => ArcOutline.BuiltOf(e, BoundaryKey, BoundaryArcs);

    /// <summary>What a new terrain is made with unless asked otherwise: its contour interval, every how many a major one, its depth under the lowest point, its material.</summary>
    public static class Defaults
    {
        public const double Interval = 0.5;
        public const int MajorEvery = 5;
        public const double Depth = 2;
        public const string Material = "Earth";
    }

    /// <summary>
    /// A new terrain's attributes: its points (surveyed heights), its datum, and the interval, major contours and depth asked
    /// or <see cref="Defaults"/>, in earth. Every command that makes a terrain starts from these.
    /// </summary>
    public static JsonObject NewAttrs(ICommandContext ctx, IEnumerable<Vec3> points, double datum, double? interval = null, double? majorEvery = null, double? depth = null)
    {
        var earth = Materials.Ensure(ctx, Defaults.Material, Color);
        return new JsonObject
        {
            ["points"] = PointsJson(points), ["datum"] = datum,
            ["interval"] = Interval(interval) ?? Defaults.Interval, ["majorEvery"] = majorEvery ?? Defaults.MajorEvery,
            ["depth"] = depth ?? Defaults.Depth, ["material"] = earth.Id,
        };
    }

    /// <summary>The terrain an id names; refused when it names none (or another geographic element).</summary>
    public static Entity Require(IModelView m, string? id) =>
        m.Get(id ?? "") is { } t && IsTerrain(t) ? t : throw new CommandException("There is no such terrain.");

    /// <summary>The project's terrains.</summary>
    public static IEnumerable<Entity> All(IModelView m) => m.OfClass(IfcClass.GeographicElement).Where(IsTerrain);

    /// <summary>The first terrain whose ground reaches a point in plan, or null.</summary>
    public static Entity? Under(IModelView m, Vec2 p) => All(m).FirstOrDefault(t => Of(t).ElevationAt(p.X, p.Y) != null);

    static readonly ConditionalWeakTable<Entity, Terrain> Cache = new();

    /// <summary>The terrain in model heights (z − datum).</summary>
    public static Terrain Of(Entity e) => Cache.GetValue(e, x => Built(x, sculpted: true));

    static readonly ConditionalWeakTable<Entity, Terrain> ExistingCache = new();

    /// <summary>The ground as surveyed (or snapshotted in <c>existingPoints</c>), before the brush (what cut and fill are measured against).</summary>
    public static Terrain Existing(Entity e) => ExistingCache.GetValue(e, x => Built(x, sculpted: false));

    static readonly ConditionalWeakTable<Entity, Terrain> BaseCache = new();

    /// <summary>
    /// The ground as worked, without its feature lines (T2): what a draped feature line, and a corner without a height of its own,
    /// read their heights from. The same as <see cref="Of"/> for a terrain with none.
    /// </summary>
    public static Terrain Base(Entity e) => e.Attrs[FeatureLines.Derived] == null ? Of(e) : BaseCache.GetValue(e, x => Built(x, sculpted: true, features: false));

    /// <summary>The attributes a terrain's surface is made of; the others (spot levels, tags, subregions, the parcel, labels) leave it as it is.</summary>
    static readonly string[] SurfaceKeys = ["points", "faces", "breaklines", "sculpts", "datum", BoundaryKey, BoundaryArcs, FeatureLines.Derived];
    static readonly string[] ExistingKeys = ["points", "faces", "existingPoints", "existingFaces", "breaklines", "datum", BoundaryKey, BoundaryArcs];

    /// <summary>The surfaces built lately, by what they are made of: a terrain changed only in what it shows keeps its surface.</summary>
    static readonly Dictionary<string, Terrain> ByContent = [];

    static Terrain Built(Entity x, bool sculpted, bool features = true)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (var k in sculpted ? SurfaceKeys : ExistingKeys)
            if (features || k != FeatureLines.Derived)
                hash.AppendData(System.Text.Encoding.UTF8.GetBytes($"{k}={x.Attrs[k]?.ToJsonString()}\n"));
        var key = (sculpted ? features ? "s" : "b" : "e") + Convert.ToHexString(hash.GetHashAndReset());
        lock (ByContent) if (ByContent.TryGetValue(key, out var known)) return known;
        var t = BuildTerrain(x, sculpted, features);
        lock (ByContent)
        {
            if (ByContent.Count >= 32) ByContent.Clear();
            ByContent[key] = t;
        }
        return t;
    }

    /// <summary>The faces a terrain keeps from its file (LandXML): triples of indices into <paramref name="count"/> points; null when there are none or one is out of range.</summary>
    static List<int>? Faces(JsonNode? node, int count)
    {
        if (node is not JsonArray arr || arr.Count == 0) return null;
        var o = new List<int>(arr.Count * 3);
        foreach (var f in arr)
        {
            if (f is not JsonArray { Count: 3 } tri) return null;
            foreach (var v in tri)
            {
                if (Json.Number(v) is not { } d || d < 0 || d >= count || d != Math.Floor(d)) return null;
                o.Add((int)d);
            }
        }
        return o;
    }

    /// <summary>Faces (triples of point indices) as stored.</summary>
    public static JsonArray FacesJson(IEnumerable<(int A, int B, int C)> faces) => new(faces.Select(f => (JsonNode)new JsonArray(f.A, f.B, f.C)).ToArray());

    /// <summary>The faces a terrain keeps from its file, or none.</summary>
    public static List<(int A, int B, int C)> FacesOf(Entity e)
    {
        var f = Faces(e.Attrs["faces"], Points(e).Count);
        return f == null ? [] : Enumerable.Range(0, f.Count / 3).Select(i => (f[i * 3], f[i * 3 + 1], f[i * 3 + 2])).ToList();
    }

    static Tin FromFaces(IReadOnlyList<Vec3> pts, List<int> faces, double datum) =>
        Tin.FromMesh(pts.SelectMany(p => new[] { p.X, p.Y, p.Z - datum }).ToList(), faces);

    static Terrain BuildTerrain(Entity x, bool sculpted, bool features = true)
    {
        var datum = x.Num("datum");
        var survey = Points(x);
        var ownBase = sculpted || x.Attrs["existingPoints"] is not JsonArray { Count: >= 3 };
        // A snapshot taken with its triangles is that surface again.
        if (!ownBase && Faces(x.Attrs["existingFaces"], ((JsonArray)x.Attrs["existingPoints"]!).Count) is { } ef)
            return new Terrain(FromFaces(ExistingPoints(x), ef, datum));
        var faces = Faces(x.Attrs["faces"], survey.Count);
        var footprint = faces != null ? FromFaces(survey, faces, datum) : null;
        var lines = Breaklines(x);
        // The feature lines it is shaped by (T2): the proposed ground's only, never the existing one's.
        var featured = sculpted && features ? FeatureLines.OnTerrain(x) : [];
        var dabs = sculpted ? Sculpt.Of(x) : [];
        var boundary = BoundaryOutline(x);
        // The file's triangles as they are (audit G9): nothing added to them, every point on one.
        if (footprint != null && lines.Count == 0 && featured.Count == 0 && dabs.Count == 0 && ownBase && faces!.Distinct().Count() == survey.Count)
            return new Terrain(boundary.Count >= 3 && SubregionSurface(new Terrain(footprint), boundary) is { } clipped ? clipped : footprint);
        // Breaklines first (their heights win where a survey point is in the same place), their segments kept as edges.
        var all = new List<Vec3>();
        var segments = new List<(int, int)>();
        foreach (var line in lines)
            for (var i = 0; i < line.Count; i++) { if (i > 0) segments.Add((all.Count - 1, all.Count)); all.Add(line[i]); }
        // The feature lines after them, split where they cross one another; their points hold their heights through the brush.
        var fromFeatures = all.Count;
        FeatureLines.Near? near = null;
        if (featured.Count > 0)
        {
            foreach (var line in FeatureLines.Split([.. featured.Select(f => f.Points)], out int _))
                for (var i = 0; i < line.Count; i++) { if (i > 0) segments.Add((all.Count - 1, all.Count)); all.Add(line[i]); }
            near = new FeatureLines.Near(featured.Select(f => (f.Points, f.Tolerance)));
        }
        var featureEnd = all.Count;
        // What the hardscapes cut into it cover (T4): the ground there is their top, its survey points left out.
        var areas = sculpted && features ? FeatureLines.Areas(x) : [];
        var basePts = ownBase ? survey : ExistingPoints(x);
        // A survey point on a breakline takes the breakline's height there: the line holds.
        var onLine = LineIndex.Of(lines);
        // One within a feature line's tolerance conflicts with it and is left out of the triangulation (the terrain says how many).
        all.AddRange(basePts.Where(p => near?.Within(p.Xy) != true && !FeatureLines.InAreas(areas, p.Xy)).Select(p => onLine?.HeightAt(p) is { } z ? p with { Z = z } : p));
        var pts = all.Select(p => new Vec3(p.X, p.Y, p.Z - datum)).ToList();
        if (dabs.Count > 0)
        {
            var held = pts.GetRange(fromFeatures, featureEnd - fromFeatures);
            pts = Sculpt.Apply(pts, segments, dabs);
            for (var i = 0; i < held.Count; i++) pts[fromFeatures + i] = held[i];
            // What the brush added within a feature line's tolerance goes too.
            if (near != null) pts = [.. pts.Where((p, i) => i < featureEnd || (!near.Within(p.Xy) && !FeatureLines.InAreas(areas, p.Xy)))];
        }
        var tin = Tin.Delaunay(pts, segments, boundary.Count >= 3 ? boundary : null);
        // A surface from a file with holes or a concave edge keeps to where its triangles were.
        if (footprint != null && tin.Triangles.Count > 0)
        {
            var within = new Terrain(footprint);
            var kept = tin.Triangles.Where(tr =>
            {
                var (a, b, c) = (tin.Points[tr.A], tin.Points[tr.B], tin.Points[tr.C]);
                return within.ElevationAt((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3) != null;
            }).ToList();
            if (kept.Count > 0) tin = Tin.Compact(tin.Points, kept);
        }
        return new Terrain(tin);
    }

    /// <summary>The breaklines' segments in a plan grid, to find the one a point lies on without trying them all.</summary>
    sealed class LineIndex
    {
        readonly double _cell;
        readonly Dictionary<(long, long), List<(int Order, Vec3 A, Vec3 B)>> _cells = [];

        LineIndex(List<IReadOnlyList<Vec3>> lines)
        {
            var segs = new List<(Vec3, Vec3)>();
            foreach (var line in lines) for (var i = 0; i + 1 < line.Count; i++) segs.Add((line[i], line[i + 1]));
            _cell = Math.Max(segs.Count > 0 ? segs.Average(s => (s.Item2.Xy - s.Item1.Xy).Length) : 1, 1e-3);
            for (var k = 0; k < segs.Count; k++)
            {
                var (a, b) = segs[k];
                for (var i = C(Math.Min(a.X, b.X) - 1e-3); i <= C(Math.Max(a.X, b.X) + 1e-3); i++)
                    for (var j = C(Math.Min(a.Y, b.Y) - 1e-3); j <= C(Math.Max(a.Y, b.Y) + 1e-3); j++)
                        (_cells.TryGetValue((i, j), out var l) ? l : _cells[(i, j)] = []).Add((k, a, b));
            }
        }

        long C(double v) => (long)Math.Floor(v / _cell);

        public static LineIndex? Of(List<IReadOnlyList<Vec3>> lines) => lines.Count == 0 ? null : new LineIndex(lines);

        /// <summary>The height of the first breakline segment a point lies on in plan (within 1 mm), or null.</summary>
        public double? HeightAt(Vec3 p)
        {
            if (!_cells.TryGetValue((C(p.X), C(p.Y)), out var list)) return null;
            foreach (var (_, a, b) in list.OrderBy(x => x.Order))
            {
                var (dx, dy) = (b.X - a.X, b.Y - a.Y);
                var len2 = dx * dx + dy * dy;
                if (len2 < 1e-12) continue;
                var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
                if (t < 0 || t > 1) continue;
                var (qx, qy) = (a.X + dx * t - p.X, a.Y + dy * t - p.Y);
                if (qx * qx + qy * qy <= 1e-6) return a.Z + (b.Z - a.Z) * t;
            }
            return null;
        }
    }

    /// <summary>Ordered 2D boundary loop of a terrain's TIN (for splitting when no explicit boundary is set).</summary>
    public static List<Vec2> BoundaryLoop(Terrain t)
    {
        var boundary = t.Boundary();
        if (boundary.Count == 0) return [];
        var next = boundary.GroupBy(e => e.A).ToDictionary(g => g.Key, g => g.First().B);
        var loop = new List<Vec2>();
        var start = boundary[0].A;
        var v = start;
        do { loop.Add(t.Tin.Points[v].Xy); v = next.GetValueOrDefault(v, -1); } while (v >= 0 && v != start && loop.Count <= boundary.Count);
        return loop;
    }

    /// <summary>Slope tags on the terrain: [x, y] (point slope) or [x1, y1, x2, y2] (two-point grade line).</summary>
    public static List<double[]> SlopeTags(Entity e) =>
        (e.Attrs["slopes"] as JsonArray)?.OfType<JsonArray>()
            .Select(a => a.Select(v => Json.Number(v) ?? double.NaN).ToArray())
            .Where(a => (a.Length == 2 || a.Length == 4) && a.All(double.IsFinite))
            .ToList() ?? [];

    /// <summary>A material sub-region on the terrain (T4): changes surface material without modifying elevation.</summary>
    public sealed record Subregion(string Id, string Name, IReadOnlyList<Vec2> Contour, double[]? Arcs, string Material);

    public static List<Subregion> SubregionsOf(Entity e)
    {
        if (e.Attrs["subregions"] is not JsonArray arr) return [];
        var list = new List<Subregion>();
        foreach (var item in arr.OfType<JsonObject>())
        {
            var pts = PointLists.Xy(item["contour"]);
            if (pts.Count < 3) continue;
            var arcs = ArcOutline.Rises(item["arcs"], pts.Count);
            list.Add(new Subregion((string?)item["id"] ?? $"sr{list.Count + 1}", (string?)item["name"] ?? $"Subregion {list.Count + 1}", pts, arcs, (string?)item["material"] ?? ""));
        }
        return list;
    }

    public static IReadOnlyList<Vec2> Spots(Entity e) => Contour(e, "spots");

    /// <summary>A terrain's breaklines: polylines of surveyed points [[x, y, z], …].</summary>
    public static List<IReadOnlyList<Vec3>> Breaklines(Entity e) =>
        (e.Attrs["breaklines"] as JsonArray)?.OfType<JsonArray>()
            .Select(l => (IReadOnlyList<Vec3>)PointLists.Xyz(l))
            .Where(l => l.Count >= 2).ToList() ?? [];

    public static IEnumerable<Entity> Pads(IModelView m, string terrainId) =>
        m.RelatedOf(terrainId, IfcClass.RelVoidsElement).Where(o => o.Class == IfcClass.EarthworksCut && !Ditches.Is(o));

    public static IEnumerable<Entity> DitchesOf(IModelView m, string terrainId) =>
        m.RelatedOf(terrainId, IfcClass.RelVoidsElement).Where(Ditches.Is);

    public static IEnumerable<Entity> PathsOf(IModelView m, string terrainId) =>
        m.RelatedOf(terrainId, IfcClass.RelVoidsElement).Where(o => o.Class == IfcClass.Pavement);

    public static Entity? TerrainOf(IModelView m, string padId) => m.RelatingOf(padId, IfcClass.RelVoidsElement);

    public static IReadOnlyList<Vec2> Contour(Entity e, string key = "contour") => PointLists.Xy(e.Attrs[key]);

    public static JsonArray ContourJson(IEnumerable<Vec2> pts) => new(pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray());

    /// <summary>The attribute beside a terrain's <c>parcel</c> holding its edges' rises (<see cref="ArcOutline"/>).</summary>
    public const string ParcelArcs = "parcelArcs";

    /// <summary>
    /// The outline a command gives a platform, a parcel, a terrain's boundary or a subregion: its <c>points</c> and, optional,
    /// each edge's rise (<c>arcs</c>, <see cref="ArcOutline"/>); <paramref name="was"/>: the rises it had, kept when only its
    /// corners are given, as many. Refused when it crosses itself; <paramref name="what"/> names it in the refusal.
    /// </summary>
    public static (IReadOnlyList<Vec2> Corners, double[]? Rises) OutlineArg(JsonObject a, double[]? was, string what = "A platform")
    {
        var pts = Args.Points(a);
        if (ArcOutline.RisesArg(a["arcs"], pts.Count, was) is not { } rises) return (CheckOutline(pts, what), null);
        var (c, r) = ArcOutline.Check(pts, rises, what, simple: true);
        return (c, r);
    }

    /// <summary>An outline of straight edges: three distinct corners at least, enclosing an area, crossing itself nowhere, made counter-clockwise.</summary>
    public static IReadOnlyList<Vec2> CheckOutline(IReadOnlyList<Vec2> pts, string what = "A platform")
    {
        var clean = new List<Vec2>();
        foreach (var p in pts) if (clean.Count == 0 || (p - clean[^1]).Length > 1e-6) clean.Add(p);
        if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 1e-6) clean.RemoveAt(clean.Count - 1);
        if (clean.Count < 3 || Math.Abs(Polygon2.SignedArea(clean)) < 1e-6) throw new CommandException($"{what} needs an outline that encloses an area.");
        if (!ArcOutline.Simple(clean)) throw new CommandException(ArcOutline.Crossing(what));
        return Polygon2.EnsureCcw(clean).ToList();
    }

    /// <summary>The ground as a closed solid: the TIN on top, its boundary sides, a flat base (works for concave boundaries and multiple loops).</summary>
    public static Solid? Body(Terrain t, double baseZ)
    {
        var tin = t.Tin;
        if (tin.Triangles.Count == 0) return null;
        var remap = new Dictionary<int, int>();
        var top = new List<Vec3>();
        int Map(int idx)
        {
            if (!remap.TryGetValue(idx, out var n)) { n = top.Count; remap[idx] = n; top.Add(tin.Points[idx]); }
            return n;
        }
        var topTris = tin.Triangles.Select(tr => (Map(tr.A), Map(tr.B), Map(tr.C))).ToList();
        var n = top.Count;
        var verts = new List<Vec3>(n * 2);
        verts.AddRange(top);
        foreach (var p in top) verts.Add(new Vec3(p.X, p.Y, baseZ));
        var tris = new List<(int, int, int)>(topTris.Count * 2 + n * 2);
        tris.AddRange(topTris);
        // Bottom face: same triangulation at baseZ with reversed winding, facing down.
        foreach (var (a, b, c) in topTris) tris.Add((n + a, n + c, n + b));
        foreach (var (a0, b0) in t.Boundary())
        {
            int a = remap[a0], b = remap[b0];
            tris.Add((a, n + a, b));
            tris.Add((b, n + a, n + b));
        }
        return SolidFromMesh.Build(verts, tris, "terrain");
    }

    /// <summary>What a platform takes away and brings: its cut and fill (m³).</summary>
    public sealed record Earthwork(string Pad, double Cut, double Fill);

    public sealed record SculptVolume(double Cut, double Fill);

    static readonly ConditionalWeakTable<ElementGeometry, List<Earthwork>> Works = new();
    static readonly ConditionalWeakTable<ElementGeometry, SculptVolume> SculptWorks = new();

    public static IReadOnlyList<Earthwork> EarthworksOf(ElementGeometry g) => Works.TryGetValue(g, out var w) ? w : [];

    public static (double Cut, double Fill) SculptEarthworkOf(ElementGeometry g) => SculptWorks.TryGetValue(g, out var s) ? (s.Cut, s.Fill) : (0, 0);

    static (double Cut, double Fill) ComputeSculptEarthwork(Entity e, Terrain existing, Terrain proposed, double baseZ, IServices? services)
    {
        var hasDifference = Sculpt.Of(e).Count > 0 || e.Attrs["existingPoints"] is JsonArray { Count: >= 3 } || e.Attrs[FeatureLines.Derived] != null;
        if (!hasDifference) return (0, 0);
        if (services?.Get<ISolidBooleans>() is { } booleans && Body(existing, baseZ) is { } exBody && Body(proposed, baseZ) is { } prBody)
        {
            try
            {
                var cut = booleans.Difference(exBody, [prBody], "cut") is { } c ? Measure.Volume(c) : 0;
                var fill = booleans.Difference(prBody, [exBody], "fill") is { } f ? Measure.Volume(f) : 0;
                return (cut, fill);
            }
            catch (InvalidOperationException) { }
        }
        // Fallback grid integration when manifold is unavailable.
        var (min, max) = proposed.Bounds;
        var span = Math.Max(max.X - min.X, max.Y - min.Y);
        var step = Math.Max(span / 80, 0.25);
        double cutSum = 0, fillSum = 0, cellArea = step * step;
        foreach (var (_, c) in Analysis.Cells(proposed, step))
        {
            if (proposed.ElevationAt(c.X, c.Y) is not { } zp || existing.ElevationAt(c.X, c.Y) is not { } ze) continue;
            var dz = zp - ze;
            if (dz > 0) fillSum += dz * cellArea; else cutSum += -dz * cellArea;
        }
        return (cutSum, fillSum);
    }

    /// <summary>The level of the terrain's flat base: its depth under the lowest point of the ground, worked or as surveyed.</summary>
    public static double BaseZ(Entity e)
    {
        var (t, existing) = (Of(e), Existing(e));
        var minZ = existing.Tin.Triangles.Count > 0 ? Math.Min(t.Bounds.Min.Z, existing.Bounds.Min.Z) : t.Bounds.Min.Z;
        return minZ - Math.Max(e.NumOrNull("depth") ?? Defaults.Depth, 0.1);
    }

    /// <summary>
    /// A platform as it levels the ground: its outline at its elevation, its own holes, and its batters. The cut goes up to
    /// <see cref="Top"/>, a metre over the highest ground it can reach (audit G6), widening by its slope (the ring
    /// <see cref="CutRing"/> there); the fill goes down to <see cref="Bottom"/>, a metre under the lowest, spreading by
    /// its slope (<see cref="FillRing"/>). <see cref="Problem"/>: why it cannot level the ground (a batter that would cross
    /// itself), and then it does not.
    /// </summary>
    public sealed record PadShape(Entity Pad, IReadOnlyList<Vec2> Outline, double Z, IReadOnlyList<IReadOnlyList<Vec2>> Holes,
        double Top, IReadOnlyList<Vec2>? CutRing, double Bottom, IReadOnlyList<Vec2>? FillRing, string? Problem)
    {
        public bool Inside(Vec2 p) => Polygon2.Contains(Outline, p) && !Holes.Any(h => Polygon2.Contains(h, p));

        /// <summary>
        /// The ground as the platform leaves it at <paramref name="p"/>, the ground being <paramref name="g"/> there: its level
        /// inside, its cut batter where the ground was above it, its fill batter where it was below; null where it leaves the
        /// ground as it is. The batters are read on the faces of the solids it cuts and fills with.
        /// </summary>
        public double? DesignedAt(Vec2 p, double g)
        {
            if (Problem != null) return null;
            if (Inside(p)) return Z;
            if (g > Z && CutRing != null && Side(CutRing, Top, p) is { } cut && g > cut) return cut;
            if (g < Z && FillRing != null && Side(FillRing, Bottom, p) is { } fill && g < fill) return fill;
            return null;
        }

        /// <summary>The height of the batter from the outline (at <see cref="Z"/>) to <paramref name="ring"/> (at <paramref name="zRing"/>) over a point, as the solid's side triangles have it; null off it.</summary>
        double? Side(IReadOnlyList<Vec2> ring, double zRing, Vec2 p)
        {
            var n = Outline.Count;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                if (Planar(Outline[i], Z, Outline[j], Z, ring[j], zRing, p) is { } a) return a;
                if (Planar(Outline[i], Z, ring[j], zRing, ring[i], zRing, p) is { } b) return b;
            }
            return null;
        }

        static double? Planar(Vec2 a, double za, Vec2 b, double zb, Vec2 c, double zc, Vec2 p)
        {
            var d = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(d) < 1e-12) return null;
            var l1 = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / d;
            var l2 = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / d;
            var l3 = 1 - l1 - l2;
            return l1 >= -1e-9 && l2 >= -1e-9 && l3 >= -1e-9 ? l1 * za + l2 * zb + l3 * zc : null;
        }
    }

    /// <summary>The terrain's platforms as they level it (<see cref="PadShape"/>), those with an outline, in order.</summary>
    public static List<PadShape> PadShapes(IModelView m, Entity e, Terrain t, double baseZ) =>
        Pads(m, e.Id).Select(p => Shape(p, t, baseZ)).OfType<PadShape>().ToList();

    static PadShape? Shape(Entity pad, Terrain t, double baseZ)
    {
        var outline = ArcOutline.BuiltOf(pad);
        if (outline.Count < 3) return null;
        // A platform's own holes (audit S4) keep their ground as it is, and its sides vertical.
        var holes = (IReadOnlyList<IReadOnlyList<Vec2>>)OutlineHoles.Built(pad);
        var z = pad.Num("elevation");
        var cutSlope = holes.Count == 0 ? Math.Max(0, pad.Num("cutSlope")) : 0;
        var fillSlope = holes.Count == 0 ? Math.Max(0, pad.Num("fillSlope")) : 0;
        // The cut goes a metre over the highest ground it takes away, the fill a metre under the lowest it stands on.
        var top = (Reached(t, outline, z, cutSlope, up: true) ?? z) + 1;
        var bottom = Math.Max(baseZ, (Reached(t, outline, z, fillSlope, up: false) ?? z) - 1);
        var cutRing = cutSlope > 0 && z < top ? Polyline2.Offset(outline, -(top - z) * cutSlope, closed: true, maxMitre: 3) : null;
        var fillRing = fillSlope > 0 && z > bottom ? Polyline2.Offset(outline, -(z - bottom) * fillSlope, closed: true, maxMitre: 3) : null;
        string? problem = null;
        if (cutRing != null && Folded(outline, cutRing)) problem = BatterProblem(pad, "cut", cutSlope, (top - z) * cutSlope);
        else if (fillRing != null && Folded(outline, fillRing)) problem = BatterProblem(pad, "fill", fillSlope, (z - bottom) * fillSlope);
        return new PadShape(pad, outline, z, holes, top, cutRing, bottom, fillRing, problem);
    }

    /// <summary>
    /// The highest (<paramref name="up"/>) or lowest ground a platform's cut or fill reaches: the corners of the triangles
    /// that have one inside its outline or past its slope from it (above the slope rising 1 : <paramref name="slope"/>
    /// from the outline for a cut, below the one falling for a fill, a metre's margin), among those as far as the slope
    /// could reach on this terrain; null when none does. Not the whole terrain's top: a hill further off would carry a
    /// cut's slope round the outline far enough to cross itself (audit G6).
    /// </summary>
    static double? Reached(Terrain t, IReadOnlyList<Vec2> outline, double z, double slope, bool up)
    {
        var reach = slope * Math.Max(0, up ? t.Bounds.Max.Z - z : z - t.Bounds.Min.Z);
        var lo = new Vec2(outline.Min(p => p.X) - reach, outline.Min(p => p.Y) - reach);
        var hi = new Vec2(outline.Max(p => p.X) + reach, outline.Max(p => p.Y) + reach);
        var tin = t.Tin;
        var taken = new Dictionary<int, bool>();
        bool Takes(int v)
        {
            if (taken.TryGetValue(v, out var known)) return known;
            var p = tin.Points[v];
            var d = Polygon2.Contains(outline, p.Xy) ? 0 : Enumerable.Range(0, outline.Count).Min(i => Polyline2.SegmentDistance(p.Xy, outline[i], outline[(i + 1) % outline.Count]));
            var takes = d <= 1e-9 || slope > 0 && (up ? p.Z > z + d / slope - 1 : p.Z < z - d / slope + 1);
            return taken[v] = takes;
        }
        // Every triangle about the outline itself (one across its edge holds ground inside it with its corners outside).
        var under = t.TrianglesIn(new Vec2(outline.Min(p => p.X), outline.Min(p => p.Y)), new Vec2(outline.Max(p => p.X), outline.Max(p => p.Y))).ToHashSet();
        double? found = null;
        foreach (var tr in t.TrianglesIn(lo, hi))
        {
            var (a, b, c) = tin.Triangles[tr];
            if (!under.Contains(tr) && !Takes(a) && !Takes(b) && !Takes(c)) continue;
            foreach (var v in new[] { a, b, c })
                found = found is { } f ? (up ? Math.Max(f, tin.Points[v].Z) : Math.Min(f, tin.Points[v].Z)) : tin.Points[v].Z;
        }
        return found;
    }

    /// <summary>Whether a ring offset from an outline crosses itself or turns an edge back (between two inner corners, past their width: it lies on its neighbours, not across them).</summary>
    static bool Folded(IReadOnlyList<Vec2> outline, IReadOnlyList<Vec2> ring)
    {
        for (var i = 0; i < outline.Count; i++)
        {
            var j = (i + 1) % outline.Count;
            if (Vec2.Dot(ring[j] - ring[i], outline[j] - outline[i]) <= 1e-12) return true;
        }
        return !ArcOutline.Simple(ring);
    }

    static string BatterProblem(Entity pad, string which, double slope, double reach) =>
        $"{pad.Name}: its {which} slope (1:{slope:0.##}) runs {reach:0.##} m out, too far for its outline's inner corners: the slope would cross itself, so the platform is not levelled. Give it a steeper slope (a smaller N) or a simpler outline.";

    /// <summary>Most contour levels a terrain draws: a finer interval is drawn coarser (a whole multiple of it), and the terrain says so.</summary>
    public const int MaxContourLevels = 2000;

    /// <summary>Most cut/fill grid points a plan writes: a finer grid is written coarser (a whole multiple of it), and the terrain says so.</summary>
    public const int MaxGridPoints = 2500;

    /// <summary>The contour interval drawn: the terrain's, or a whole multiple of it when it would draw more than <see cref="MaxContourLevels"/> levels; 0: none.</summary>
    public static double DrawnInterval(Entity e, Terrain t)
    {
        var interval = e.NumOrNull("interval") ?? Defaults.Interval;
        if (!(interval > 0) || t.Tin.Triangles.Count == 0) return 0;
        var levels = (t.Bounds.Max.Z - t.Bounds.Min.Z) / interval;
        return levels <= MaxContourLevels ? interval : interval * Math.Ceiling(levels / MaxContourLevels);
    }

    /// <summary>The cut/fill grid's step written in plan: the terrain's, or a whole multiple of it when it would write more than <see cref="MaxGridPoints"/>; null when it has none.</summary>
    public static double? DrawnGrid(Entity e, Terrain t)
    {
        if (e.NumOrNull("cutFillGrid") is not { } grid || grid < 0.5 || t.Tin.Triangles.Count == 0) return null;
        var (min, max) = t.Bounds;
        double Count(double g) => (Math.Floor(max.X / g + 1e-9) - Math.Ceiling(min.X / g - 1e-9) + 1) * (Math.Floor(max.Y / g + 1e-9) - Math.Ceiling(min.Y / g - 1e-9) + 1);
        for (var k = Math.Max(1, (int)Math.Floor(Math.Sqrt(Count(grid) / MaxGridPoints))); ; k++)
            if (Count(grid * k) <= MaxGridPoints) return grid * k;
    }

    /// <summary>What the terrain says about the contours and the grid it draws coarser than asked.</summary>
    static IEnumerable<string> DrawnNotes(Entity e, Terrain t)
    {
        var interval = e.NumOrNull("interval") ?? Defaults.Interval;
        if (DrawnInterval(e, t) is var drawn && drawn > interval * (1 + 1e-9))
            yield return $"Contours every {interval:0.###} m would be more than {MaxContourLevels} lines over this terrain's height: they are drawn every {drawn:0.###} m.";
        if (e.NumOrNull("cutFillGrid") is { } grid && DrawnGrid(e, t) is { } g && g > grid * (1 + 1e-9))
            yield return $"A cut/fill grid every {grid:0.###} m would write more than {MaxGridPoints} values: it is written every {g:0.###} m.";
    }

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices? services)
    {
        var t = Of(e);
        if (t.Tin.Triangles.Count == 0) return new ElementGeometry([], Diagnostics: ["The terrain needs at least three points that are not in a line."]);
        var existing = Existing(e);
        var baseZ = BaseZ(e);
        if (Body(t, baseZ) is not { } body) return new ElementGeometry([], Diagnostics: ["The terrain's surface could not be closed into a solid."]);
        var (sculptCut, sculptFill) = existing.Tin.Triangles.Count > 0 ? ComputeSculptEarthwork(e, existing, t, baseZ, services) : (0.0, 0.0);
        var problems = new List<string>(DrawnNotes(e, t));
        // What its feature lines did to its survey (T2): the points they dropped, the crossings where two disagreed.
        if (e.Attrs[FeatureLines.Derived] != null)
        {
            if (FeatureLines.Covered(e) is var covered and > 0)
                problems.Add($"{covered} survey point(s) lie under a hardscape: they are left out of the triangulation, the ground there is its top.");
            if (FeatureLines.Dropped(e) is var dropped and > 0)
                problems.Add($"{dropped} survey point(s) lie within a feature line's tolerance of it: they are left out of the triangulation, the line holds there.");
            // Î7: the line drawn first holds; each crossing is marked in plan too, with its two heights.
            foreach (var (c, held, other) in FeatureLines.Clashes(e).Take(10))
                problems.Add($"Feature lines cross at ({c.At.X:0.00}, {c.At.Y:0.00}) at different heights: {m.Get(held)?.Name ?? "one"} holds {c.Held:0.000}, {m.Get(other)?.Name ?? "the other"} would be {c.Other:0.000} and bends to it.");
        }
        var works = new List<Earthwork>();
        var pads = PadShapes(m, e, t, baseZ);
        if (pads.Count > 0)
        {
            if (services?.Get<ISolidBooleans>() is { } booleans)
            {
                foreach (var pad in pads)
                {
                    // A batter that would cross itself: said, not levelled with a wrong volume (audit G6).
                    if (pad.Problem != null) { problems.Add(pad.Problem); continue; }
                    var (outline, z, holes) = (pad.Outline, pad.Z, pad.Holes);
                    try
                    {
                        var cut = z < pad.Top
                            ? (pad.CutRing != null ? SlopedPrism(outline, z, pad.CutRing, pad.Top, "cut") : Prism.ExtrudeFootprint(outline, holes, z, pad.Top - z, "cut"))
                            : null;
                        var fill = z > pad.Bottom
                            ? (pad.FillRing != null ? SlopedPrism(pad.FillRing, pad.Bottom, outline, z, "fill") : Prism.ExtrudeFootprint(outline, holes, pad.Bottom, z - pad.Bottom, "fill"))
                            : null;
                        var cutVol = cut != null && booleans.Intersection(body, cut) is { } ci ? Measure.Volume(ci) : 0;
                        var fillVol = 0.0;
                        if (fill != null)
                        {
                            var under = booleans.Intersection(body, fill) is { } fi ? Measure.Volume(fi) : 0;
                            fillVol = Math.Max(0, Measure.Volume(fill) - under);
                        }
                        if (cut != null) body = booleans.Difference(body, [cut], "terrain") ?? body;
                        if (fill != null && fillVol > 1e-9) body = booleans.Union([body, fill], "terrain") ?? body;
                        works.Add(new Earthwork(pad.Pad.Id, cutVol, fillVol));
                    }
                    catch (InvalidOperationException ex) { problems.Add($"{pad.Pad.Name}: could not be levelled ({ex.Message})."); }
                }
            }
            else problems.Add("Platforms need the boolean library (manifold), missing here.");
        }
        // Ditches and paths take their volume out of the ground: the ditch's channel, the path's courses.
        var cutters = DitchesOf(m, e.Id).Select(d => (Element: d, Solids: Ditches.Cutters(d, t, m)))
            .Concat(PathsOf(m, e.Id).Select(pth => (Element: pth, Solids: Paths.GroundCutters(m, pth))))
            // A hardscape (T4): its layers and its kerb, under a ground that meets its top edges.
            .Concat(Hardscapes.Of(m, e.Id).Select(h => (Element: h, Solids: Hardscapes.GroundCutters(m, h))))
            .Where(x => x.Solids.Count > 0).ToList();
        if (cutters.Count > 0)
        {
            if (services?.Get<ISolidBooleans>() is { } booleans)
                foreach (var (element, solids) in cutters)
                {
                    try
                    {
                        var cutVol = 0.0;
                        foreach (var c in solids) if (booleans.Intersection(body, c) is { } ci) cutVol += Measure.Volume(ci);
                        body = booleans.Difference(body, solids, "terrain") ?? body;
                        works.Add(new Earthwork(element.Id, cutVol, 0));
                    }
                    catch (InvalidOperationException ex) { problems.Add($"{element.Name}: could not be cut into the ground ({ex.Message})."); }
                }
            else problems.Add("Ditches, paths and hardscapes need the boolean library (manifold), missing here.");
        }
        var material = e.Str("material");
        var parts = new List<GeometryPart> { new("Ground", Materials.ColorOf(m, material) is "#c9ced6" ? Color : Materials.ColorOf(m, material), body, material, "ground") { LodKeepsEdges = true } };
        foreach (var sr in SubregionsOf(e))
            if (SubregionPart(t, sr, m) is { } part) parts.Add(part);
        if (e.Str("analysis") is { } kind && kind != "none" && Analysis.Of(m, e, kind) is { } analysis) parts.AddRange(Analysis.Overlay(analysis));
        var g = new ElementGeometry(parts, Plan(e, t, pads.Select(p => (p.Outline, p.Z, p.Holes)).ToList(), DitchesOf(m, e.Id).ToList(), FeatureLines.Areas(e)), null, problems.Count > 0 ? problems : null);
        Works.AddOrUpdate(g, works);
        SculptWorks.AddOrUpdate(g, new SculptVolume(sculptCut, sculptFill));
        return g;
    }

    /// <summary>Chained contour polylines at elevation <paramref name="z"/>, clipped outside <paramref name="pads"/>.</summary>
    public static List<List<Vec2>> ChainedContours(Terrain t, double z, IEnumerable<(IReadOnlyList<Vec2> Outer, IReadOnlyList<IReadOnlyList<Vec2>> Holes)> pads) =>
        Chain(t.Contour(z).SelectMany(s => Outside(s.A, s.B, pads)).ToList());

    /// <summary>The plan: contours (the major ones heavier) outside the platforms, the terrain's edge, the platforms' outlines.</summary>
    static List<PlanItem> Plan(Entity e, Terrain t, List<(IReadOnlyList<Vec2> Outline, double Z, IReadOnlyList<IReadOnlyList<Vec2>> Holes)> pads, List<Entity> ditches, List<List<Vec2>>? covered = null)
    {
        var items = new List<PlanItem>();
        var interval = DrawnInterval(e, t);
        var major = Math.Max(1, (int)Math.Round(e.NumOrNull("majorEvery") ?? Defaults.MajorEvery));
        // The contours stop at the platforms and at the hardscapes cut into it (T4), which draw their own outlines.
        var padAreas = pads.Select(x => (x.Outline, x.Holes)).Concat((covered ?? []).Select(a => ((IReadOnlyList<Vec2>)a, (IReadOnlyList<IReadOnlyList<Vec2>>)[]))).ToList();
        if (interval > 0)
            foreach (var z in t.Levels(interval))
            {
                var kind = Math.Abs(z / interval / major - Math.Round(z / interval / major)) < 1e-6 ? "contourMajor" : "contour";
                foreach (var line in ChainedContours(t, z, padAreas)) items.Add(new PlanItem(kind, false, line));
            }
        foreach (var (a, b) in t.Boundary())
            items.Add(new PlanItem("terrainEdge", false, [new Vec2(t.Tin.Points[a].X, t.Tin.Points[a].Y), new Vec2(t.Tin.Points[b].X, t.Tin.Points[b].Y)]));
        foreach (var (outline, _, holes) in pads) { items.Add(new PlanItem("heavy", true, outline)); items.AddRange(holes.Select(h => new PlanItem("heavy", true, h))); }
        // A subregion's outline, a seen line (a kind the drawing has no role for would not be drawn).
        foreach (var sr in SubregionsOf(e)) items.Add(new PlanItem("symbol", true, ArcOutline.Points(sr.Contour, sr.Arcs)));
        foreach (var d in ditches) items.AddRange(Ditches.Plan(d));
        return items;
    }

    static Solid? SlopedPrism(IReadOnlyList<Vec2> bottomRing, double z0, IReadOnlyList<Vec2> topRing, double z1, string tag)
    {
        var n = bottomRing.Count;
        if (n < 3 || topRing.Count != n || z1 <= z0 + 1e-9) return null;
        var verts = new List<Vec3>(n * 2);
        foreach (var p in bottomRing) verts.Add(new Vec3(p.X, p.Y, z0));
        foreach (var p in topRing) verts.Add(new Vec3(p.X, p.Y, z1));
        var botIds = Enumerable.Range(0, n).ToArray();
        var topIds = Enumerable.Range(n, n).ToArray();
        var tris = new List<(int, int, int)>();
        tris.AddRange(Triangulator.Face(verts, botIds, null, new Vec3(0, 0, -1)));
        tris.AddRange(Triangulator.Face(verts, topIds, null, new Vec3(0, 0, 1)));
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            tris.Add((i, j, n + j));
            tris.Add((i, n + j, n + i));
        }
        return SolidFromMesh.Build(verts, tris, tag);
    }

    /// <summary>
    /// The terrain's own surface inside <paramref name="poly"/>: each of its triangles kept whole when inside, else
    /// clipped to the polygon (Sutherland–Hodgman, the triangle being convex) and the piece triangulated in its plane.
    /// No new triangulation, so the terrain's breaklines and edges hold exactly; null when nothing is inside.
    /// </summary>
    public static Tin? SubregionSurface(Terrain t, IReadOnlyList<Vec2> poly)
    {
        var tin = t.Tin;
        if (poly.Count < 3 || tin.Triangles.Count == 0) return null;
        var ring = Polygon2.EnsureCcw(poly).ToList();
        double minX = ring.Min(p => p.X), maxX = ring.Max(p => p.X), minY = ring.Min(p => p.Y), maxY = ring.Max(p => p.Y);
        var points = new List<Vec3>();
        var at = new Dictionary<(long, long), int>();
        int Weld(Vec3 p)
        {
            var k = ((long)Math.Round(p.X * 1e5), (long)Math.Round(p.Y * 1e5));
            if (!at.TryGetValue(k, out var i)) { i = points.Count; at[k] = i; points.Add(p); }
            return i;
        }
        var tris = new List<(int A, int B, int C)>();
        static bool Crosses(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
        {
            double d1 = Vec2.Cross(b - a, c - a), d2 = Vec2.Cross(b - a, d - a), d3 = Vec2.Cross(d - c, a - c), d4 = Vec2.Cross(d - c, b - c);
            return ((d1 > 0) != (d2 > 0) || Math.Abs(d1) < 1e-12 || Math.Abs(d2) < 1e-12) && ((d3 > 0) != (d4 > 0) || Math.Abs(d3) < 1e-12 || Math.Abs(d4) < 1e-12);
        }
        // Only the triangles the terrain's grid has about the polygon's box, in their order.
        foreach (var ti in t.TrianglesIn(new Vec2(minX, minY), new Vec2(maxX, maxY)).Order())
        {
            var (ia, ib, ic) = tin.Triangles[ti];
            var (pa, pb, pc) = (tin.Points[ia], tin.Points[ib], tin.Points[ic]);
            Vec2[] tri = [pa.Xy, pb.Xy, pc.Xy];
            var corners = tri.Count(c => Polygon2.Contains(ring, c));
            var crossing = false;
            for (var i = 0; i < ring.Count && !crossing; i++)
                for (var j = 0; j < 3 && !crossing; j++)
                    crossing = Crosses(ring[i], ring[(i + 1) % ring.Count], tri[j], tri[(j + 1) % 3]);
            var cornerInside = !crossing && ring.Any(r => Polygon2.Contains(tri, r));
            if (!crossing && !cornerInside)
            {
                if (corners == 3) tris.Add((Weld(pa), Weld(pb), Weld(pc)));
                continue;
            }
            // The polygon clipped by the triangle's three half-planes.
            var piece = ring;
            for (var j = 0; j < 3 && piece.Count >= 3; j++)
            {
                var (e0, e1) = (tri[j], tri[(j + 1) % 3]);
                var next = new List<Vec2>(piece.Count + 2);
                for (var i = 0; i < piece.Count; i++)
                {
                    var (s, q) = (piece[i], piece[(i + 1) % piece.Count]);
                    double ds = Vec2.Cross(e1 - e0, s - e0), dq = Vec2.Cross(e1 - e0, q - e0);
                    if (ds >= 0) next.Add(s);
                    if ((ds >= 0) != (dq >= 0)) next.Add(s + (q - s) * (ds / (ds - dq)));
                }
                piece = next;
            }
            if (piece.Count < 3 || Math.Abs(Polygon2.SignedArea(piece)) < 1e-9) continue;
            // Heights from the triangle's plane; a point within 1e-6 of a corner is that corner.
            var den = (pb.Y - pc.Y) * (pa.X - pc.X) + (pc.X - pb.X) * (pa.Y - pc.Y);
            Vec3 Lift(Vec2 p)
            {
                foreach (var c in new[] { pa, pb, pc }) if ((c.Xy - p).Length < 1e-6) return c;
                var u = ((pb.Y - pc.Y) * (p.X - pc.X) + (pc.X - pb.X) * (p.Y - pc.Y)) / den;
                var v = ((pc.Y - pa.Y) * (p.X - pc.X) + (pa.X - pc.X) * (p.Y - pc.Y)) / den;
                return new Vec3(p.X, p.Y, u * pa.Z + v * pb.Z + (1 - u - v) * pc.Z);
            }
            var flat = piece.Select(p => Lift(p)).ToList();
            var ids = Enumerable.Range(0, flat.Count).ToArray();
            foreach (var (a, b, c) in Triangulator.Face(flat, ids, null, new Vec3(0, 0, 1)))
            {
                var (qa, qb, qc) = (flat[a], flat[b], flat[c]);
                if (Math.Abs((qb.X - qa.X) * (qc.Y - qa.Y) - (qb.Y - qa.Y) * (qc.X - qa.X)) < 1e-10) continue;
                var (wa, wb, wc) = (Weld(qa), Weld(qb), Weld(qc));
                if (wa != wb && wb != wc && wa != wc) tris.Add((wa, wb, wc));
            }
        }
        return tris.Count == 0 ? null : new Tin(points, tris);
    }

    static GeometryPart? SubregionPart(Terrain t, Subregion sr, IModelView m)
    {
        var poly = ArcOutline.Points(sr.Contour, sr.Arcs);
        if (poly.Count < 3 || SubregionSurface(t, poly) is not { } subTin) return null;
        var n = subTin.Points.Count;
        var verts = new List<Vec3>(n * 2);
        foreach (var p in subTin.Points) verts.Add(new Vec3(p.X, p.Y, p.Z + 0.015));
        foreach (var p in subTin.Points) verts.Add(new Vec3(p.X, p.Y, p.Z - 0.005));
        var tris = new List<(int, int, int)>(subTin.Triangles.Count * 2 + n * 2);
        tris.AddRange(subTin.Triangles);
        foreach (var (a, b, c) in subTin.Triangles) tris.Add((n + a, n + c, n + b));
        foreach (var (a, b) in new Terrain(subTin).Boundary())
        {
            tris.Add((a, n + a, b));
            tris.Add((b, n + a, n + b));
        }
        if (SolidFromMesh.Build(verts, tris, "subregion") is not { } skin) return null;
        return new GeometryPart(sr.Name, Materials.ColorOf(m, sr.Material), skin, sr.Material, "subregion:" + sr.Id) { LodKeepsEdges = true };
    }

    /// <summary>Segments joined end to end into polylines (a contour's pieces, one per triangle, into its lines).</summary>
    public static List<List<Vec2>> Chain(IReadOnlyList<(Vec2 A, Vec2 B)> segs)
    {
        static (long, long) K(Vec2 p) => ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6));
        var at = new Dictionary<(long, long), List<int>>();
        for (var i = 0; i < segs.Count; i++)
            foreach (var p in new[] { segs[i].A, segs[i].B }) (at.TryGetValue(K(p), out var l) ? l : at[K(p)] = []).Add(i);
        var used = new bool[segs.Count];
        var lines = new List<List<Vec2>>();
        int? Next(Vec2 p) => at.TryGetValue(K(p), out var l) ? l.FirstOrDefault(i => !used[i], -1) is var n && n >= 0 ? n : null : null;
        for (var i = 0; i < segs.Count; i++)
        {
            if (used[i]) continue;
            used[i] = true;
            var line = new List<Vec2> { segs[i].A, segs[i].B };
            // Grow forward, then backward.
            for (var dir = 0; dir < 2; dir++)
            {
                while (Next(line[^1]) is { } n)
                {
                    used[n] = true;
                    var (a, b) = segs[n];
                    line.Add(K(a) == K(line[^1]) ? b : a);
                }
                line.Reverse();
            }
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>The pieces of a segment outside every polygon.</summary>
    /// <summary>The pieces of a segment outside the platforms (in a platform's own hole counts as outside it).</summary>
    static IEnumerable<(Vec2, Vec2)> Outside(Vec2 a, Vec2 b, IEnumerable<(IReadOnlyList<Vec2> Outer, IReadOnlyList<IReadOnlyList<Vec2>> Holes)> pads)
    {
        var areas = pads.ToList();
        var list = areas.SelectMany(x => x.Holes.Prepend(x.Outer)).ToList();
        if (list.Count == 0) { yield return (a, b); yield break; }
        var ts = new List<double> { 0, 1 };
        var d = b - a;
        foreach (var poly in list)
            for (var i = 0; i < poly.Count; i++)
            {
                var (p, q) = (poly[i], poly[(i + 1) % poly.Count]);
                var e = q - p;
                var den = Vec2.Cross(d, e);
                if (Math.Abs(den) < 1e-15) continue;
                var t = Vec2.Cross(p - a, e) / den;
                var u = Vec2.Cross(p - a, d) / den;
                if (t > 0 && t < 1 && u >= 0 && u <= 1) ts.Add(t);
            }
        ts.Sort();
        for (var k = 0; k + 1 < ts.Count; k++)
        {
            if (ts[k + 1] - ts[k] < 1e-12) continue;
            var mid = a + d * ((ts[k] + ts[k + 1]) / 2);
            if (areas.Any(x => Polygon2.Contains(x.Outer, mid) && !x.Holes.Any(h => Polygon2.Contains(h, mid)))) continue;
            yield return (a + d * ts[k], a + d * ts[k + 1]);
        }
    }

    /// <summary>A TIN's area in plan.</summary>
    public static double PlanArea(Tin tin) => tin.Triangles.Sum(tr =>
    {
        var (p, q, r) = (tin.Points[tr.A], tin.Points[tr.B], tin.Points[tr.C]);
        return Math.Abs((q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X)) / 2;
    });

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        var t = Of(e);
        var tin = t.Tin;
        var plan = PlanArea(tin);
        double surface = 0;
        foreach (var (a, b, c) in tin.Triangles)
        {
            var (p, q, r) = (tin.Points[a], tin.Points[b], tin.Points[c]);
            surface += Vec3.Cross(q - p, r - p).Length / 2;
        }
        var works = EarthworksOf(g);
        var (sculptCut, sculptFill) = SculptEarthworkOf(g);
        var totalCut = sculptCut + works.Sum(w => w.Cut);
        var totalFill = sculptFill + works.Sum(w => w.Fill);
        yield return new QuantitySet("Lapin_TerrainQuantities",
        [
            new("PlanArea", plan, "AREA", "Plan area"),
            new("SurfaceArea", surface, "AREA", "Surface area"),
            new("MinElevation", t.Bounds.Min.Z, "LENGTH", "Lowest point"),
            new("MaxElevation", t.Bounds.Max.Z, "LENGTH", "Highest point"),
            new("CutVolume", totalCut, "VOLUME", "Cut"),
            new("FillVolume", totalFill, "VOLUME", "Fill"),
            new("NetVolume", totalCut - totalFill, "VOLUME", "Net cut − fill"),
            .. sculptCut > 0 || sculptFill > 0 ? new Quantity[] { new("SculptCut", sculptCut, "VOLUME", "Grading cut"), new("SculptFill", sculptFill, "VOLUME", "Grading fill") } : [],
            // Exactly, its arcs as arcs (decision 589).
            .. Contour(e, "parcel") is { Count: >= 3 } ? new Quantity[] { new("ParcelArea", ArcOutline.AreaOf(e, "parcel", ParcelArcs), "AREA", "Parcel"), new("ParcelPerimeter", ArcOutline.PerimeterOf(e, "parcel", ParcelArcs), "LENGTH", "Parcel boundary") } : [],
        ]);
        foreach (var w in works)
            yield return new QuantitySet("Lapin_EarthworkQuantities",
            [
                new("CutVolume", w.Cut, "VOLUME", "Cut"),
                new("FillVolume", w.Fill, "VOLUME", "Fill"),
            ], "pad:" + w.Pad, m.Get(w.Pad)?.Name ?? "Earthwork");
        foreach (var sr in SubregionsOf(e))
        {
            // The part of it on the ground: what is drawn outside the terrain is no subregion of it.
            var area = SubregionSurface(t, ArcOutline.Points(sr.Contour, sr.Arcs)) is { } on ? PlanArea(on) : 0;
            yield return new QuantitySet("Lapin_SubregionQuantities",
            [
                new("PlanArea", area, "AREA", "Plan area"),
            ], "subregion:" + sr.Id, sr.Name, sr.Material);
        }
    }
}
