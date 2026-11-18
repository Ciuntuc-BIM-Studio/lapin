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

    static readonly ConditionalWeakTable<Entity, Terrain> Cache = new();

    /// <summary>The terrain in model heights (z − datum).</summary>
    public static Terrain Of(Entity e) => Cache.GetValue(e, x => BuildTerrain(x, sculpted: true));

    static readonly ConditionalWeakTable<Entity, Terrain> ExistingCache = new();

    /// <summary>The ground as surveyed (or snapshotted in <c>existingPoints</c>), before the brush (what cut and fill are measured against).</summary>
    public static Terrain Existing(Entity e) => ExistingCache.GetValue(e, x => BuildTerrain(x, sculpted: false));

    static Terrain BuildTerrain(Entity x, bool sculpted)
    {
        var datum = x.Num("datum");
        // Breaklines first (their heights win where a survey point is in the same place), their segments kept as edges.
        var all = new List<Vec3>();
        var segments = new List<(int, int)>();
        var lines = Breaklines(x);
        foreach (var line in lines)
            for (var i = 0; i < line.Count; i++) { if (i > 0) segments.Add((all.Count - 1, all.Count)); all.Add(line[i]); }
        var basePts = sculpted ? Points(x) : ExistingPoints(x);
        // A survey point on a breakline takes the breakline's height there: the line holds.
        all.AddRange(basePts.Select(p => OnBreakline(lines, p) is { } z ? p with { Z = z } : p));
        var pts = all.Select(p => new Vec3(p.X, p.Y, p.Z - datum)).ToList();
        if (sculpted) pts = Sculpt.Apply(pts, segments, Sculpt.Of(x));
        var tin = Tin.Delaunay(pts, segments);
        var boundary = BoundaryOutline(x);
        if (boundary.Count >= 3 && tin.Triangles.Count > 0)
            tin = ClipToBoundary(pts, segments, tin, boundary);
        return new Terrain(tin);
    }

    /// <summary>Constrains the TIN to <paramref name="boundary"/> and discards triangles outside it, compacting unused points.</summary>
    static Tin ClipToBoundary(List<Vec3> pts, List<(int, int)> segments, Tin baseTin, IReadOnlyList<Vec2> boundary)
    {
        var baseTerrain = new Terrain(baseTin);
        var bStart = pts.Count;
        for (var i = 0; i < boundary.Count; i++)
        {
            var v = boundary[i];
            var z = baseTerrain.ElevationAt(v.X, v.Y)
                ?? baseTin.Points.MinBy(p => (p.X - v.X) * (p.X - v.X) + (p.Y - v.Y) * (p.Y - v.Y)).Z;
            pts.Add(new Vec3(v.X, v.Y, z));
            segments.Add((bStart + i, bStart + (i + 1) % boundary.Count));
        }
        var constrained = Tin.Delaunay(pts, segments);
        var kept = new List<(int A, int B, int C)>();
        foreach (var (a, b, c) in constrained.Triangles)
        {
            var (pa, pb, pc) = (constrained.Points[a], constrained.Points[b], constrained.Points[c]);
            var mid = new Vec2((pa.X + pb.X + pc.X) / 3, (pa.Y + pb.Y + pc.Y) / 3);
            if (Polygon2.Contains(boundary, mid)) kept.Add((a, b, c));
        }
        if (kept.Count == 0) return constrained;
        var remap = new Dictionary<int, int>();
        var compactPts = new List<Vec3>();
        int Map(int idx)
        {
            if (!remap.TryGetValue(idx, out var n)) { n = compactPts.Count; remap[idx] = n; compactPts.Add(constrained.Points[idx]); }
            return n;
        }
        var compactTris = kept.Select(t => (Map(t.A), Map(t.B), Map(t.C))).ToList();
        return new Tin(compactPts, compactTris);
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

    /// <summary>The height of the breakline a point lies on in plan (within 1 mm), or null.</summary>
    static double? OnBreakline(List<IReadOnlyList<Vec3>> lines, Vec3 p)
    {
        foreach (var line in lines)
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var (a, b) = (line[i], line[i + 1]);
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
    /// The outline a command gives a platform or a parcel: its <c>points</c> and, optional, each edge's rise (<c>arcs</c>,
    /// <see cref="ArcOutline"/>); <paramref name="was"/>: the rises it had, kept when only its corners are given, as many.
    /// </summary>
    public static (IReadOnlyList<Vec2> Corners, double[]? Rises) OutlineArg(JsonObject a, double[]? was, string what = "A platform")
    {
        var pts = Args.Points(a);
        if (ArcOutline.RisesArg(a["arcs"], pts.Count, was) is not { } rises) return (CheckOutline(pts), null);
        var (c, r) = ArcOutline.Check(pts, rises, what);
        return (c, r);
    }

    /// <summary>A platform's outline: three distinct corners at least, made counter-clockwise.</summary>
    public static IReadOnlyList<Vec2> CheckOutline(IReadOnlyList<Vec2> pts)
    {
        var clean = new List<Vec2>();
        foreach (var p in pts) if (clean.Count == 0 || (p - clean[^1]).Length > 1e-6) clean.Add(p);
        if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 1e-6) clean.RemoveAt(clean.Count - 1);
        if (clean.Count < 3 || Math.Abs(Polygon2.SignedArea(clean)) < 1e-6) throw new CommandException("A platform needs an outline that encloses an area.");
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
        var hasDifference = Sculpt.Of(e).Count > 0 || e.Attrs["existingPoints"] is JsonArray { Count: >= 3 };
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
        for (var x = min.X; x < max.X; x += step)
            for (var y = min.Y; y < max.Y; y += step)
            {
                var (cx, cy) = (x + step / 2, y + step / 2);
                if (proposed.ElevationAt(cx, cy) is not { } zp || existing.ElevationAt(cx, cy) is not { } ze) continue;
                var dz = zp - ze;
                if (dz > 0) fillSum += dz * cellArea; else cutSum += -dz * cellArea;
            }
        return (cutSum, fillSum);
    }

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices? services)
    {
        var t = Of(e);
        if (t.Tin.Triangles.Count == 0) return new ElementGeometry([], Diagnostics: ["The terrain needs at least three points that are not in a line."]);
        var existing = Existing(e);
        var minZ = existing.Tin.Triangles.Count > 0 ? Math.Min(t.Bounds.Min.Z, existing.Bounds.Min.Z) : t.Bounds.Min.Z;
        var baseZ = minZ - Math.Max(e.NumOrNull("depth") ?? 2, 0.1);
        if (Body(t, baseZ) is not { } body) return new ElementGeometry([], Diagnostics: ["The terrain's surface could not be closed into a solid."]);
        var (sculptCut, sculptFill) = existing.Tin.Triangles.Count > 0 ? ComputeSculptEarthwork(e, existing, t, baseZ, services) : (0.0, 0.0);
        var problems = new List<string>();
        var works = new List<Earthwork>();
        // A platform's own holes (audit S4) keep their ground as it is.
        var pads = Pads(m, e.Id).Select(p => (Pad: p, Outline: ArcOutline.BuiltOf(p), Z: p.Num("elevation"), Holes: (IReadOnlyList<IReadOnlyList<Vec2>>)OutlineHoles.Built(p))).Where(p => p.Outline.Count >= 3).ToList();
        if (pads.Count > 0)
        {
            if (services?.Get<ISolidBooleans>() is { } booleans)
            {
                var top = t.Bounds.Max.Z + 1;
                foreach (var (pad, outline, z, holes) in pads)
                {
                    try
                    {
                        var cutSlope = Math.Max(0, pad.Num("cutSlope"));
                        var fillSlope = Math.Max(0, pad.Num("fillSlope"));
                        var cut = z < top
                            ? (cutSlope > 0 && holes.Count == 0
                                ? SlopedPrism(outline, z, Polyline2.Offset(outline, -(top - z) * cutSlope, closed: true, maxMitre: 3), top, "cut")
                                : Prism.ExtrudeFootprint(outline, holes, z, top - z, "cut"))
                            : null;
                        var fill = z > baseZ
                            ? (fillSlope > 0 && holes.Count == 0
                                ? SlopedPrism(Polyline2.Offset(outline, -(z - baseZ) * fillSlope, closed: true, maxMitre: 3), baseZ, outline, z, "fill")
                                : Prism.ExtrudeFootprint(outline, holes, baseZ, z - baseZ, "fill"))
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
                        works.Add(new Earthwork(pad.Id, cutVol, fillVol));
                    }
                    catch (InvalidOperationException ex) { problems.Add($"{pad.Name}: could not be levelled ({ex.Message})."); }
                }
            }
            else problems.Add("Platforms need the boolean library (manifold), missing here.");
        }
        // Ditches and paths take their volume out of the ground: the ditch's channel, the path's courses.
        var cutters = DitchesOf(m, e.Id).Select(d => (Element: d, Solids: Ditches.Cutters(d, t, m)))
            .Concat(PathsOf(m, e.Id).Select(pth => (Element: pth, Solids: Paths.GroundCutters(m, pth))))
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
            else problems.Add("Ditches and paths need the boolean library (manifold), missing here.");
        }
        var material = e.Str("material");
        var parts = new List<GeometryPart> { new("Ground", Materials.ColorOf(m, material) is "#c9ced6" ? Color : Materials.ColorOf(m, material), body, material, "ground") };
        foreach (var sr in SubregionsOf(e))
            if (SubregionPart(t, sr, m) is { } part) parts.Add(part);
        if (e.Str("analysis") is { } kind && kind != "none" && Analysis.Of(m, e, kind) is { } analysis) parts.AddRange(Analysis.Overlay(analysis));
        var g = new ElementGeometry(parts, Plan(e, t, pads.Select(p => (p.Outline, p.Z, p.Holes)).ToList(), DitchesOf(m, e.Id).ToList()), null, problems.Count > 0 ? problems : null);
        Works.AddOrUpdate(g, works);
        SculptWorks.AddOrUpdate(g, new SculptVolume(sculptCut, sculptFill));
        return g;
    }

    /// <summary>Chained contour polylines at elevation <paramref name="z"/>, clipped outside <paramref name="pads"/>.</summary>
    public static List<List<Vec2>> ChainedContours(Terrain t, double z, IEnumerable<(IReadOnlyList<Vec2> Outer, IReadOnlyList<IReadOnlyList<Vec2>> Holes)> pads) =>
        Chain(t.Contour(z).SelectMany(s => Outside(s.A, s.B, pads)).ToList());

    /// <summary>The plan: contours (the major ones heavier) outside the platforms, the terrain's edge, the platforms' outlines.</summary>
    static List<PlanItem> Plan(Entity e, Terrain t, List<(IReadOnlyList<Vec2> Outline, double Z, IReadOnlyList<IReadOnlyList<Vec2>> Holes)> pads, List<Entity> ditches)
    {
        var items = new List<PlanItem>();
        var interval = e.NumOrNull("interval") ?? 0.5;
        var major = Math.Max(1, (int)Math.Round(e.NumOrNull("majorEvery") ?? 5));
        var padAreas = pads.Select(x => (x.Outline, x.Holes)).ToList();
        if (interval > 0 && (t.Bounds.Max.Z - t.Bounds.Min.Z) / interval <= 2000)
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
        foreach (var (ia, ib, ic) in tin.Triangles)
        {
            var (pa, pb, pc) = (tin.Points[ia], tin.Points[ib], tin.Points[ic]);
            if (Math.Max(pa.X, Math.Max(pb.X, pc.X)) < minX || Math.Min(pa.X, Math.Min(pb.X, pc.X)) > maxX
                || Math.Max(pa.Y, Math.Max(pb.Y, pc.Y)) < minY || Math.Min(pa.Y, Math.Min(pb.Y, pc.Y)) > maxY) continue;
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
        return new GeometryPart(sr.Name, Materials.ColorOf(m, sr.Material), skin, sr.Material, "subregion:" + sr.Id);
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

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        var t = Of(e);
        var tin = t.Tin;
        double plan = 0, surface = 0;
        foreach (var (a, b, c) in tin.Triangles)
        {
            var (p, q, r) = (tin.Points[a], tin.Points[b], tin.Points[c]);
            plan += Math.Abs((q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X)) / 2;
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
            var area = Math.Abs(ArcOutline.SignedArea(sr.Contour, sr.Arcs));
            yield return new QuantitySet("Lapin_SubregionQuantities",
            [
                new("PlanArea", area, "AREA", "Plan area"),
            ], "subregion:" + sr.Id, sr.Name, sr.Material);
        }
    }
}
