using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Drawing.Import;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// Site data in real coordinates (docs/plan-urban.md, stage 1): survey points
/// from DXF, GeoJSON features, CityJSON buildings, an orthophoto's corners.
/// Every file's coordinates go through the project's map: a file in another
/// system is converted, and the first import into a project that is not yet
/// tied to the map ties it (the survey point at the plan's origin, on the
/// file's first coordinates rounded to the metre), so the model stays near
/// its origin and the coordinates stay exact.
/// </summary>
public static class GeoImport
{
    /// <summary>
    /// How the file's coordinates land in the plan: converted from the file's
    /// system to the project's, then through the survey point; small local
    /// coordinates with no system stay as they are.
    /// </summary>
    public static Func<double, double, Vec2> Placer(ICommandContext ctx, string? fileCrs, IReadOnlyList<(double X, double Y)> sample)
    {
        if (sample.Count == 0) return (x, y) => new Vec2(x, y);
        var known = GeoRef.Get(fileCrs);
        var local = known == null && !GeoRef.LooksLikeMap(sample);
        if (local) return (x, y) => new Vec2(x, y);
        var from = known?.Id ?? Guess(sample) ?? Location.Crs(ctx.Model);
        var project = Location.Crs(ctx.Model) ?? from;
        if (from != null && GeoRef.Get(from)?.Geographic == true && project == from) project = GeoRef.Stereo70;
        (double, double) ToProject(double x, double y) => from != null && project != null && GeoRef.Get(from) != null && GeoRef.Get(project) != null ? GeoRef.Convert(from, project, x, y) : (x, y);
        if (!GeoRef.Georeferenced(ctx.Model))
        {
            var (e0, n0) = ToProject(sample[0].X, sample[0].Y);
            Location.Georeference(ctx, project, Math.Round(e0), Math.Round(n0));
        }
        var m = ctx.Model;
        return (x, y) => { var (e, n) = ToProject(x, y); return GeoRef.ToPlan(m, e, n); };
    }

    /// <summary>The system map coordinates look like (<see cref="GeoRef.Guess"/>).</summary>
    public static string? Guess(IReadOnlyList<(double X, double Y)> pts) => GeoRef.Guess(pts);

    // ─── Survey points from DXF ───

    /// <summary>
    /// The surveyed points of a DXF drawing: POINT entities with their height;
    /// blocks inserted at a height, or carrying their height as an attribute;
    /// texts that read as a height beside a point without one (a Romanian
    /// survey's "245.37" next to its cross), or standing alone where no point
    /// is; and, when asked, the vertices of contour lines at their elevation.
    /// Only the layers named, when some are.
    /// </summary>
    public static List<Vec3> DxfSurvey(DxfDocument doc, IReadOnlyCollection<string>? layers = null, bool contours = true)
    {
        bool On(DxfEntity e) => layers == null || layers.Count == 0 || layers.Contains(e.Layer);
        static double? Height(string s) => double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
        var withZ = new List<Vec3>();
        var bare = new List<Vec2>();
        var texts = new List<(Vec2 At, double Z, double H)>();
        foreach (var e in doc.Entities.Where(On))
            switch (e)
            {
                case DxfPoint p:
                    if (Math.Abs(p.P.Z) > 1e-9) withZ.Add(p.P); else bare.Add(p.P.Xy);
                    break;
                case DxfInsert ins:
                    if (ins.Z is { } z && Math.Abs(z) > 1e-9) withZ.Add(new Vec3(ins.At.X, ins.At.Y, z));
                    else if (ins.Attribs.Select(t => Height(t.Value)).FirstOrDefault(h => h != null) is { } ah) withZ.Add(new Vec3(ins.At.X, ins.At.Y, ah));
                    else bare.Add(ins.At);
                    break;
                // A height is written with its decimals ("245.37"), not as a point number.
                case DxfText t when Height(t.Value) is { } hv && (t.Value.Contains('.') || t.Value.Contains(',')):
                    texts.Add((t.At, hv, Math.Max(t.Height, 0.1)));
                    break;
                case DxfPolyline pl when contours && (pl.Elevation is { } ev && Math.Abs(ev) > 1e-9 || pl.Zs != null):
                    for (var i = 0; i < pl.Vertices.Count; i++)
                    {
                        var vz = pl.Zs != null && i < pl.Zs.Count ? pl.Zs[i] : pl.Elevation ?? 0;
                        if (Math.Abs(vz) > 1e-9) withZ.Add(new Vec3(pl.Vertices[i].P.X, pl.Vertices[i].P.Y, vz));
                    }
                    break;
            }
        // A height written beside a point without one: the nearest such point within a few text heights takes it; one far from any stands where it is written.
        var taken = new HashSet<int>();
        foreach (var (at, z, h) in texts)
        {
            var (best, bd) = (-1, double.PositiveInfinity);
            for (var i = 0; i < bare.Count; i++)
            {
                if (taken.Contains(i)) continue;
                var d = (bare[i] - at).Length;
                if (d < bd) (best, bd) = (i, d);
            }
            if (best >= 0 && bd <= 4 * h) { taken.Add(best); withZ.Add(new Vec3(bare[best].X, bare[best].Y, z)); }
            else if (bare.Count == 0) withZ.Add(new Vec3(at.X, at.Y, z));
        }
        // The same spot twice (a point and its block): once.
        var o = new List<Vec3>();
        var seen = new HashSet<(long, long)>();
        foreach (var p in withZ)
            if (seen.Add(((long)Math.Round(p.X * 1000), (long)Math.Round(p.Y * 1000)))) o.Add(p);
        return o;
    }

    // ─── GeoJSON ───

    public sealed record Feature(string Kind, string Name, List<List<Vec3>> Rings, bool HasZ, JsonObject Properties);

    /// <summary>The features of a GeoJSON text and its declared system ("crs" member; none: WGS 84 as the standard says).</summary>
    public static (List<Feature> Features, string Crs) GeoJson(string text)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(text); }
        catch (JsonException e) { throw new CommandException($"The file is not GeoJSON: {e.Message}"); }
        var crs = GeoRef.Wgs84;
        if ((string?)root?["crs"]?["properties"]?["name"] is { } name)
        {
            var m = System.Text.RegularExpressions.Regex.Match(name, @"(\d{4,5})\s*$");
            if (m.Success) crs = "EPSG:" + m.Groups[1].Value;
        }
        var o = new List<Feature>();
        var items = (string?)root?["type"] switch
        {
            "FeatureCollection" => (root!["features"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [],
            "Feature" => [(JsonObject)root!],
            _ => root is JsonObject g && g["coordinates"] != null ? [new JsonObject { ["type"] = "Feature", ["geometry"] = g.DeepClone() }] : [],
        };
        foreach (var f in items)
        {
            var props = f["properties"] as JsonObject ?? [];
            var label = FeatureName(props) ?? $"{o.Count + 1}";
            if (f["geometry"] is JsonObject geom) Geometry(geom, label, (JsonObject)props.DeepClone(), o);
        }
        return (o, crs);
    }

    static string? FeatureName(JsonObject props)
    {
        foreach (var k in new[] { "name", "Name", "NAME", "nr_cad", "NR_CAD", "nrcad", "cadastral", "id", "ID", "label" })
            if (props[k] is JsonValue v && v.ToString() is { Length: > 0 } s) return s;
        return null;
    }

    static List<Vec3> Ring(JsonNode? n, out bool z)
    {
        var o = new List<Vec3>();
        z = false;
        foreach (var c in (n as JsonArray)?.OfType<JsonArray>() ?? [])
        {
            if (c.Count < 2) continue;
            var hasZ = c.Count > 2 && Json.Number(c[2]) is not null;
            z |= hasZ;
            o.Add(new Vec3(Json.Number(c[0]) ?? 0, Json.Number(c[1]) ?? 0, hasZ ? Json.Number(c[2]) ?? 0 : 0));
        }
        // A closed ring repeats its first point last: once.
        if (o.Count > 2 && (o[0] - o[^1]).Length < 1e-9) o.RemoveAt(o.Count - 1);
        return o;
    }

    static void Geometry(JsonObject g, string name, JsonObject props, List<Feature> into)
    {
        var c = g["coordinates"];
        switch ((string?)g["type"])
        {
            case "Point": { var r = Ring(new JsonArray(c?.DeepClone()), out var z); if (r.Count == 1) into.Add(new("point", name, [r], z, props)); break; }
            case "MultiPoint": { var r = Ring(c, out var z); foreach (var p in r) into.Add(new("point", name, [[p]], z, props)); break; }
            case "LineString": { var r = Ring(c, out var z); if (r.Count >= 2) into.Add(new("line", name, [r], z, props)); break; }
            case "MultiLineString": foreach (var l in (c as JsonArray) ?? []) { var r = Ring(l, out var z); if (r.Count >= 2) into.Add(new("line", name, [r], z, props)); } break;
            case "Polygon": Polygon(c, name, props, into); break;
            case "MultiPolygon": foreach (var p in (c as JsonArray) ?? []) Polygon(p, name, props, into); break;
            case "GeometryCollection": foreach (var x in (g["geometries"] as JsonArray)?.OfType<JsonObject>() ?? []) Geometry(x, name, props, into); break;
        }
    }

    static void Polygon(JsonNode? c, string name, JsonObject props, List<Feature> into)
    {
        var rings = new List<List<Vec3>>();
        var z = false;
        foreach (var r in (c as JsonArray) ?? []) { var ring = Ring(r, out var rz); z |= rz; if (ring.Count >= 3) rings.Add(ring); }
        if (rings.Count > 0) into.Add(new("area", name, rings, z, props));
    }

    // ─── CityJSON ───

    public sealed record CityBuilding(string Id, string Name, List<(List<Vec3> Outer, List<List<Vec3>> Holes)> Surfaces, JsonObject Attributes);

    /// <summary>
    /// The buildings of a CityJSON file (1.0 to 2.0): each object of type
    /// Building or BuildingPart, its surfaces at its highest level of detail
    /// (MultiSurface, CompositeSurface, Solid's outer shell), vertices
    /// un-compressed by the file's transform; and the file's system.
    /// </summary>
    public static (List<CityBuilding> Buildings, string? Crs) CityJson(string text)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(text); }
        catch (JsonException e) { throw new CommandException($"The file is not CityJSON: {e.Message}"); }
        if ((string?)root?["type"] != "CityJSON") throw new CommandException("The file is not CityJSON.");
        var (scale, translate) = (new[] { 1.0, 1.0, 1.0 }, new[] { 0.0, 0.0, 0.0 });
        if (root!["transform"] is JsonObject t)
        {
            scale = (t["scale"] as JsonArray)?.Select(x => Json.Number(x) ?? 1).ToArray() ?? scale;
            translate = (t["translate"] as JsonArray)?.Select(x => Json.Number(x) ?? 0).ToArray() ?? translate;
        }
        var verts = (root["vertices"] as JsonArray)?.OfType<JsonArray>().Select(v => new Vec3(
            (Json.Number(v[0]) ?? 0) * scale[0] + translate[0], (Json.Number(v[1]) ?? 0) * scale[1] + translate[1], (Json.Number(v[2]) ?? 0) * scale[2] + translate[2])).ToList() ?? [];
        string? crs = null;
        if ((string?)root["metadata"]?["referenceSystem"] is { } rs && System.Text.RegularExpressions.Regex.Match(rs, @"(\d{4,5})\s*$") is { Success: true } m) crs = "EPSG:" + m.Groups[1].Value;
        var o = new List<CityBuilding>();
        foreach (var (id, node) in (root["CityObjects"] as JsonObject) ?? [])
        {
            if (node is not JsonObject obj || (string?)obj["type"] is not ("Building" or "BuildingPart")) continue;
            var geoms = (obj["geometry"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
            if (geoms.Count == 0) continue;
            var best = geoms.OrderByDescending(g => double.TryParse(g["lod"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var l) ? l : 0).First();
            var surfaces = new List<(List<Vec3>, List<List<Vec3>>)>();
            void Surface(JsonNode? s)
            {
                var rings = (s as JsonArray)?.OfType<JsonArray>().Select(r => r.Select(i => (int?)(Json.Number(i) is { } d ? (int)d : null)).Where(i => i is >= 0 && i < verts.Count).Select(i => verts[i!.Value]).ToList()).Where(r => r.Count >= 3).ToList() ?? [];
                if (rings.Count > 0) surfaces.Add((rings[0], rings.Skip(1).ToList()));
            }
            var b = best["boundaries"];
            switch ((string?)best["type"])
            {
                case "MultiSurface" or "CompositeSurface": foreach (var s in (b as JsonArray) ?? []) Surface(s); break;
                case "Solid": foreach (var s in ((b as JsonArray)?.FirstOrDefault() as JsonArray) ?? []) Surface(s); break;
                case "MultiSolid" or "CompositeSolid": foreach (var solid in (b as JsonArray) ?? []) foreach (var s in ((solid as JsonArray)?.FirstOrDefault() as JsonArray) ?? []) Surface(s); break;
            }
            if (surfaces.Count == 0) continue;
            var attrs = obj["attributes"] as JsonObject ?? [];
            var name = attrs["name"]?.ToString() is { Length: > 0 } n ? n : id;
            o.Add(new CityBuilding(id, name, surfaces, (JsonObject)attrs.DeepClone()));
        }
        return (o, crs);
    }

    /// <summary>A building's surfaces as one stored body (triangles), in plan coordinates.</summary>
    public static JsonObject? Body(CityBuilding b, Func<double, double, Vec2> place, double datum)
    {
        var vs = new List<Vec3>();
        var tris = new List<(int, int, int)>();
        foreach (var (outer, holes) in b.Surfaces)
        {
            var pts = outer.Concat(holes.SelectMany(h => h)).Select(p => { var q = place(p.X, p.Y); return new Vec3(q.X, q.Y, p.Z - datum); }).ToList();
            var n = Newell(pts.Take(outer.Count).ToList());
            if (n.Length < 1e-12) continue;
            var outerIx = Enumerable.Range(0, outer.Count).ToArray();
            var holeIx = new List<int[]>();
            var k = outer.Count;
            foreach (var h in holes) { holeIx.Add(Enumerable.Range(k, h.Count).ToArray()); k += h.Count; }
            var baseIx = vs.Count;
            vs.AddRange(pts);
            foreach (var (a, b2, c) in Triangulator.Face(pts, outerIx, [.. holeIx], n.Normalized())) tris.Add((baseIx + a, baseIx + b2, baseIx + c));
        }
        return tris.Count == 0 ? null : StoredBodies.Write([(b.Name, SiteFeatures.Color("building"), vs, tris)]);
    }

    static Vec3 Newell(IReadOnlyList<Vec3> p)
    {
        double x = 0, y = 0, z = 0;
        for (var i = 0; i < p.Count; i++)
        {
            var (a, b) = (p[i], p[(i + 1) % p.Count]);
            x += (a.Y - b.Y) * (a.Z + b.Z); y += (a.Z - b.Z) * (a.X + b.X); z += (a.X - b.X) * (a.Y + b.Y);
        }
        return new Vec3(x, y, z);
    }
}
