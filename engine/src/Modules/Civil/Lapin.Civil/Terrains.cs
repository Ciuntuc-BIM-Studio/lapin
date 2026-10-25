using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using BubbleBim.Geometry;
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

    public static List<Vec3> Points(Entity e) =>
        e.Attrs["points"] is JsonArray arr
            ? arr.OfType<JsonArray>().Where(p => p.Count >= 3).Select(p => new Vec3(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0, Json.Number(p[2]) ?? 0)).ToList()
            : [];

    public static JsonArray PointsJson(IEnumerable<Vec3> pts) => new(pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y, p.Z)).ToArray());

    static readonly ConditionalWeakTable<Entity, Terrain> Cache = new();

    /// <summary>The terrain in model heights (z − datum).</summary>
    public static Terrain Of(Entity e) => Cache.GetValue(e, x =>
    {
        var datum = x.Num("datum");
        // Breaklines first (their heights win where a survey point is in the same place), their segments kept as edges.
        var all = new List<Vec3>();
        var segments = new List<(int, int)>();
        var lines = Breaklines(x);
        foreach (var line in lines)
        {
            for (var i = 0; i < line.Count; i++) { if (i > 0) segments.Add((all.Count - 1, all.Count)); all.Add(line[i]); }
        }
        // A survey point on a breakline takes the breakline's height there: the line holds.
        all.AddRange(Points(x).Select(p => OnBreakline(lines, p) is { } z ? p with { Z = z } : p));
        // The brush's strokes, in order, over the survey: the ground as it was worked.
        var pts = Sculpt.Apply(all.Select(p => new Vec3(p.X, p.Y, p.Z - datum)).ToList(), segments, Sculpt.Of(x));
        return new Terrain(Tin.Delaunay(pts, segments));
    });

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
            .Select(l => (IReadOnlyList<Vec3>)l.OfType<JsonArray>().Where(p => p.Count >= 3).Select(p => new Vec3(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0, Json.Number(p[2]) ?? 0)).ToList())
            .Where(l => l.Count >= 2).ToList() ?? [];

    public static IEnumerable<Entity> Pads(IModelView m, string terrainId) =>
        m.RelatedOf(terrainId, IfcClass.RelVoidsElement).Where(o => o.Class == IfcClass.EarthworksCut && !Ditches.Is(o));

    public static IEnumerable<Entity> DitchesOf(IModelView m, string terrainId) =>
        m.RelatedOf(terrainId, IfcClass.RelVoidsElement).Where(Ditches.Is);

    public static IEnumerable<Entity> PathsOf(IModelView m, string terrainId) =>
        m.RelatedOf(terrainId, IfcClass.RelVoidsElement).Where(o => o.Class == IfcClass.Pavement);

    public static Entity? TerrainOf(IModelView m, string padId) => m.RelatingOf(padId, IfcClass.RelVoidsElement);

    public static IReadOnlyList<Vec2> Contour(Entity e, string key = "contour") =>
        e.Attrs[key] is JsonArray arr
            ? arr.OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0)).ToList()
            : [];

    public static JsonArray ContourJson(IEnumerable<Vec2> pts) => new(pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray());

    /// <summary>A platform's outline: three distinct corners at least, made counter-clockwise.</summary>
    public static IReadOnlyList<Vec2> CheckOutline(IReadOnlyList<Vec2> pts)
    {
        var clean = new List<Vec2>();
        foreach (var p in pts) if (clean.Count == 0 || (p - clean[^1]).Length > 1e-6) clean.Add(p);
        if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 1e-6) clean.RemoveAt(clean.Count - 1);
        if (clean.Count < 3 || Math.Abs(Polygon2.SignedArea(clean)) < 1e-6) throw new CommandException("A platform needs an outline that encloses an area.");
        return Polygon2.EnsureCcw(clean).ToList();
    }

    /// <summary>The ground as a closed solid: the TIN on top, its hull's sides, a flat base.</summary>
    public static Solid? Body(Terrain t, double baseZ)
    {
        var tin = t.Tin;
        if (tin.Triangles.Count == 0) return null;
        var n = tin.Points.Count;
        var verts = new List<Vec3>(tin.Points);
        var tris = new List<(int, int, int)>(tin.Triangles);
        var boundary = t.Boundary();
        // The hull's chain, counter-clockwise, from its directed edges.
        var next = boundary.GroupBy(e => e.A).ToDictionary(g => g.Key, g => g.First().B);
        var hull = new List<int>();
        if (boundary.Count > 0)
        {
            var start = boundary[0].A;
            var v = start;
            do { hull.Add(v); v = next.GetValueOrDefault(v, -1); } while (v >= 0 && v != start && hull.Count <= boundary.Count);
        }
        var below = new Dictionary<int, int>();
        foreach (var i in hull) { below[i] = verts.Count; verts.Add(new Vec3(tin.Points[i].X, tin.Points[i].Y, baseZ)); }
        for (var k = 0; k < hull.Count; k++)
        {
            int a = hull[k], b = hull[(k + 1) % hull.Count];
            tris.Add((a, below[a], b));
            tris.Add((b, below[a], below[b]));
        }
        // The base: a fan around the hull's centroid, facing down.
        var c = verts.Count;
        verts.Add(new Vec3(hull.Average(i => tin.Points[i].X), hull.Average(i => tin.Points[i].Y), baseZ));
        for (var k = 0; k < hull.Count; k++) tris.Add((c, below[hull[(k + 1) % hull.Count]], below[hull[k]]));
        _ = n;
        return SolidFromMesh.Build(verts, tris, "terrain");
    }

    /// <summary>What a platform takes away and brings: its cut and fill (m³).</summary>
    public sealed record Earthwork(string Pad, double Cut, double Fill);

    static readonly ConditionalWeakTable<ElementGeometry, List<Earthwork>> Works = new();

    public static IReadOnlyList<Earthwork> EarthworksOf(ElementGeometry g) => Works.TryGetValue(g, out var w) ? w : [];

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices? services)
    {
        var t = Of(e);
        if (t.Tin.Triangles.Count == 0) return new ElementGeometry([], Diagnostics: ["The terrain needs at least three points that are not in a line."]);
        var baseZ = t.Bounds.Min.Z - Math.Max(e.NumOrNull("depth") ?? 2, 0.1);
        if (Body(t, baseZ) is not { } body) return new ElementGeometry([], Diagnostics: ["The terrain's surface could not be closed into a solid."]);
        var problems = new List<string>();
        var works = new List<Earthwork>();
        var pads = Pads(m, e.Id).Select(p => (Pad: p, Outline: Contour(p), Z: p.Num("elevation"))).Where(p => p.Outline.Count >= 3).ToList();
        if (pads.Count > 0)
        {
            if (services?.Get<ISolidBooleans>() is { } booleans)
            {
                var top = t.Bounds.Max.Z + 1;
                foreach (var (pad, outline, z) in pads)
                {
                    try
                    {
                        var cut = z < top ? Prism.ExtrudeFootprint(outline, z, top - z, "cut") : null;
                        var fill = z > baseZ ? Prism.ExtrudeFootprint(outline, baseZ, z - baseZ, "fill") : null;
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
        var cutters = DitchesOf(m, e.Id).Select(d => (Element: d, Solids: Ditches.Cutters(d, t)))
            .Concat(PathsOf(m, e.Id).Select(pth => { var (surface, @base, _) = Paths.Solids(m, pth); return (Element: pth, Solids: surface.Concat(@base).ToList()); }))
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
        var g = new ElementGeometry(parts, Plan(e, t, pads.Select(p => (p.Outline, p.Z)).ToList(), DitchesOf(m, e.Id).ToList()), null, problems.Count > 0 ? problems : null);
        Works.AddOrUpdate(g, works);
        return g;
    }

    /// <summary>The plan: contours (the major ones heavier) outside the platforms, the terrain's edge, the platforms' outlines.</summary>
    static List<PlanItem> Plan(Entity e, Terrain t, List<(IReadOnlyList<Vec2> Outline, double Z)> pads, List<Entity> ditches)
    {
        var items = new List<PlanItem>();
        var interval = e.NumOrNull("interval") ?? 0.5;
        var major = Math.Max(1, (int)Math.Round(e.NumOrNull("majorEvery") ?? 5));
        if (interval > 0 && (t.Bounds.Max.Z - t.Bounds.Min.Z) / interval <= 2000)
            foreach (var z in t.Levels(interval))
            {
                var kind = Math.Abs(z / interval / major - Math.Round(z / interval / major)) < 1e-6 ? "contourMajor" : "contour";
                var pieces = t.Contour(z).SelectMany(s => Outside(s.A, s.B, pads.Select(x => x.Outline))).ToList();
                foreach (var line in Chain(pieces)) items.Add(new PlanItem(kind, false, line));
            }
        foreach (var (a, b) in t.Boundary())
            items.Add(new PlanItem("terrainEdge", false, [new Vec2(t.Tin.Points[a].X, t.Tin.Points[a].Y), new Vec2(t.Tin.Points[b].X, t.Tin.Points[b].Y)]));
        foreach (var (outline, _) in pads) items.Add(new PlanItem("heavy", true, outline));
        foreach (var d in ditches) items.AddRange(Ditches.Plan(d));
        return items;
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
    static IEnumerable<(Vec2, Vec2)> Outside(Vec2 a, Vec2 b, IEnumerable<IReadOnlyList<Vec2>> polygons)
    {
        var list = polygons.ToList();
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
            if (list.Any(poly => Polygon2.Contains(poly, mid))) continue;
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
        yield return new QuantitySet("Lapin_TerrainQuantities",
        [
            new("PlanArea", plan, "AREA", "Plan area"),
            new("SurfaceArea", surface, "AREA", "Surface area"),
            new("MinElevation", t.Bounds.Min.Z, "LENGTH", "Lowest point"),
            new("MaxElevation", t.Bounds.Max.Z, "LENGTH", "Highest point"),
            new("CutVolume", works.Sum(w => w.Cut), "VOLUME", "Cut"),
            new("FillVolume", works.Sum(w => w.Fill), "VOLUME", "Fill"),
        ]);
        foreach (var w in works)
            yield return new QuantitySet("Lapin_EarthworkQuantities",
            [
                new("CutVolume", w.Cut, "VOLUME", "Cut"),
                new("FillVolume", w.Fill, "VOLUME", "Fill"),
            ], "pad:" + w.Pad, m.Get(w.Pad)?.Name ?? "Earthwork");
    }
}
