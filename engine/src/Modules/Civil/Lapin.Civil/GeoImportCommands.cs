using System.Text.Json.Nodes;
using Lapin.Drawing.Import;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>The Site tab's imports in real coordinates (docs/plan-urban.md, stage 1): survey points from DXF, GeoJSON, CityJSON, an orthophoto.</summary>
public static class GeoImportCommands
{
    static string Storey(ICommandContext ctx, JsonObject a) =>
        (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");

    static string Text(JsonObject a, string what)
    {
        if ((string?)a["text"] is { Length: > 0 } t) return t;
        if ((string?)a["data"] is { Length: > 0 } d) return DxfReader.Decode(Convert.FromBase64String(d));
        throw new CommandException($"Which {what} file?");
    }

    /// <summary>The surveyed elevation of ±0.00: the one given, else the first terrain's, else the lowest height rounded down to the metre.</summary>
    static double Datum(ICommandContext ctx, JsonObject a, IEnumerable<double> zs) =>
        Json.Number(a["datum"]) ?? ctx.Model.OfClass(IfcClass.GeographicElement).FirstOrDefault(Terrains.IsTerrain)?.NumOrNull("datum")
        ?? (zs.Any() ? Math.Floor(zs.Min()) : 0);

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(SiteFeatures.Class, "Site feature",
        [
            new("kind", "Kind", "choice", Choices: SiteFeatures.Kinds), new("source", "From", "text", ReadOnly: true),
        ], "#6b4e2e"));
        r.Geometry(SiteFeatures.Class, SiteFeatures.Geometry);
        r.Quantities(SiteFeatures.Class, SiteFeatures.Quantities);
        r.Service<IElementPlacement>(new SiteFeatures.Placement());
        Generator.SymbolicInPlan.Add(SiteFeatures.Class);

        r.Class(new(Orthophotos.Class, "Orthophoto", [new("opacity", "Opacity", "number", Min: 0.05, Max: 1)], "#888888"));
        r.Geometry(Orthophotos.Class, Orthophotos.Geometry);

        // Survey points from a DXF drawing (POINTs, blocks at a height, heights written beside points, contours): a terrain, or more points on one.
        r.Command("importDxfPoints", "Terrain from DXF", (ctx, a) =>
        {
            var doc = DxfReader.Read(Text(a, "DXF"));
            var layers = (a["layers"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet();
            var raw = GeoImport.DxfSurvey(doc, layers, a["contours"] is not JsonValue cv || !cv.TryGetValue<bool>(out var c) || c);
            if (raw.Count < 3) throw new CommandException($"The drawing has {raw.Count} surveyed point(s) with a height: a terrain needs three.");
            var place = GeoImport.Placer(ctx, (string?)a["crs"], raw.Select(p => (p.X, p.Y)).ToList());
            var pts = raw.Select(p => { var q = place(p.X, p.Y); return new Vec3(q.X, q.Y, p.Z); }).ToList();
            return Terrain(ctx, a, pts, (string?)a["name"] ?? "Terrain from DXF");
        });

        // The layers of a DXF drawing with how many surveyed points each holds (to choose from before importing).
        r.Query("dxfPointLayers", (q, a) =>
        {
            var doc = DxfReader.Read(Text(a, "DXF"));
            var o = new JsonArray();
            foreach (var layer in doc.Entities.Select(e => e.Layer).Distinct().OrderBy(x => x))
            {
                var n = GeoImport.DxfSurvey(doc, [layer]).Count;
                if (n > 0) o.Add((JsonNode)new JsonObject { ["layer"] = layer, ["points"] = n });
            }
            return o;
        });

        // GeoJSON: areas (one alone: the parcel), lines, points; a parcel also outlines the terrain's parcel when it has none.
        r.Command("importGeoJson", "Import GeoJSON", (ctx, a) =>
        {
            var (features, fileCrs) = GeoImport.GeoJson(Text(a, "GeoJSON"));
            if (features.Count == 0) throw new CommandException("The file has no feature with coordinates.");
            var crs = (string?)a["crs"] ?? fileCrs;
            var place = GeoImport.Placer(ctx, crs, features.SelectMany(f => f.Rings.SelectMany(rg => rg)).Select(p => (p.X, p.Y)).Take(1000).ToList());
            var storey = Storey(ctx, a);
            var areas = features.Count(f => f.Kind == "area");
            var parcelAll = a["parcels"] is JsonValue pv && pv.TryGetValue<bool>(out var pa) && pa;
            var ids = new JsonArray();
            Entity? parcel = null;
            foreach (var f in features)
            {
                var kind = f.Kind == "area" && (parcelAll || areas == 1) ? "parcel" : f.Kind;
                var pts = f.Rings[0].Select(p => { var q = place(p.X, p.Y); return new Vec3(q.X, q.Y, p.Z); }).ToList();
                var e = SiteFeatures.Add(ctx, storey, kind, f.Name, pts, f.HasZ, "GeoJSON", f.Properties);
                parcel ??= kind == "parcel" ? e : null;
                ids.Add((JsonNode)e.Id);
            }
            // The terrain takes the parcel as its own when it has none (its area and outline on the site plan).
            if (parcel != null && ctx.Model.OfClass(IfcClass.GeographicElement).FirstOrDefault(t => Terrains.IsTerrain(t) && t.Attrs["parcel"] == null) is { } terrain)
            {
                var attrs = terrain.CloneAttrs();
                attrs["parcel"] = Terrains.ContourJson(SiteFeatures.Points(parcel).Select(p => p.Xy).ToList());
                ctx.Replace(terrain.With(attrs: attrs));
            }
            return new JsonObject { ["ids"] = ids, ["crs"] = Location.Crs(ctx.Model), ["parcel"] = parcel?.Id };
        });

        // CityJSON: the neighbouring buildings with their surfaces, read-only, at their place and height.
        r.Command("importCityJson", "Import CityJSON", (ctx, a) =>
        {
            var (buildings, fileCrs) = GeoImport.CityJson(Text(a, "CityJSON"));
            if (buildings.Count == 0) throw new CommandException("The file has no building with surfaces.");
            var all = buildings.SelectMany(b => b.Surfaces.SelectMany(s => s.Outer)).ToList();
            var place = GeoImport.Placer(ctx, (string?)a["crs"] ?? fileCrs, all.Select(p => (p.X, p.Y)).Take(1000).ToList());
            var datum = Datum(ctx, a, all.Select(p => p.Z));
            var storey = Storey(ctx, a);
            var ids = new JsonArray();
            foreach (var b in buildings)
            {
                if (GeoImport.Body(b, place, datum) is not { } body) continue;
                // Its footprint: the hull of its lowest points, for the plan and the area.
                var low = b.Surfaces.SelectMany(s => s.Outer).Select(p => place(p.X, p.Y)).ToList();
                var hull = Polygon2.ConvexHull(low).Select(p => new Vec3(p.X, p.Y, 0)).ToList();
                var e = SiteFeatures.Add(ctx, storey, "building", b.Name, hull, false, "CityJSON", b.Attributes, body);
                ids.Add((JsonNode)e.Id);
            }
            return new JsonObject { ["ids"] = ids, ["datum"] = datum };
        });

        // An orthophoto: an image (imageId, or data + mime) laid by its world file's text, or by its four map corners (top left, top right, bottom right, bottom left).
        r.Command("addOrthophoto", "Orthophoto", (ctx, a) =>
        {
            string imageId;
            if ((string?)a["imageId"] is { } given) imageId = ctx.Model.Get(given) is { Class: Images.Class } ? given : throw new CommandException("There is no such image.");
            else
            {
                var mime = (string?)a["mime"] ?? "";
                if (!Images.Mimes.Contains(mime)) throw new CommandException("An orthophoto is a PNG or a JPEG (with its world file).");
                var bytes = Convert.FromBase64String((string?)a["data"] ?? "");
                if (bytes.Length == 0 || bytes.Length > Images.MaxBytes) throw new CommandException($"An orthophoto is at most {Images.MaxBytes / 1024 / 1024} MB.");
                var image = new Entity(ctx.NewId(), Images.Class, ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : "Orthophoto", new JsonObject { ["mime"] = mime, ["data"] = Convert.ToBase64String(bytes) });
                ctx.Add(image);
                imageId = image.Id;
            }
            var img = ctx.Model.Get(imageId)!;
            (double X, double Y)[] map;
            if (a["corners"] is JsonArray cs && cs.Count == 4)
                map = cs.Select(x => x is JsonArray p && p.Count >= 2 ? (Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0) : (0.0, 0.0)).ToArray();
            else
            {
                var wf = Orthophotos.WorldFile((string?)a["worldFile"] ?? "") ?? throw new CommandException("Give the image's world file (.jgw, .pgw, .wld) or its four corners.");
                var size = Orthophotos.PixelSize(Images.Bytes(img)) ?? throw new CommandException("The image's size cannot be read.");
                map = Orthophotos.MapCorners(wf, size.W, size.H);
            }
            var place = GeoImport.Placer(ctx, (string?)a["crs"], map.ToList());
            var corners = map.Select(p => place(p.X, p.Y)).ToList();
            var e = new Entity(ctx.NewId(), Orthophotos.Class, img.Name, new JsonObject
            {
                ["image"] = imageId, ["corners"] = new JsonArray(corners.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 4), Math.Round(p.Y, 4))).ToArray()), ["opacity"] = 1,
            });
            ctx.Add(e);
            Spatial.Contain(ctx, Storey(ctx, a), e.Id);
            return new JsonObject { ["id"] = e.Id };
        });
    }

    static JsonObject Terrain(ICommandContext ctx, JsonObject a, List<Vec3> pts, string name)
    {
        if (Tin.Delaunay(pts).Triangles.Count == 0) throw new CommandException("The points lie in a line: no terrain.");
        // More points on a terrain that is there, or a new one.
        if ((string?)a["terrainId"] is { } tid && ctx.Model.Get(tid) is { } t0 && Terrains.IsTerrain(t0))
        {
            var attrs = t0.CloneAttrs();
            var all = Terrains.Points(t0).Concat(pts).ToList();
            attrs["points"] = Terrains.PointsJson(all);
            ctx.Replace(t0.With(attrs: attrs));
            return new JsonObject { ["id"] = t0.Id, ["points"] = pts.Count };
        }
        var earth = Materials.Ensure(ctx, "Earth", Terrains.Color);
        var datum = Datum(ctx, a, pts.Select(p => p.Z));
        var e = new Entity(ctx.NewId(), IfcClass.GeographicElement, name, new JsonObject
        {
            ["points"] = Terrains.PointsJson(pts), ["datum"] = datum, ["interval"] = 0.5, ["majorEvery"] = 5, ["depth"] = 2, ["material"] = earth.Id,
        }, Terrains.Predefined);
        ctx.Add(e);
        Spatial.Contain(ctx, Storey(ctx, a), e.Id);
        return new JsonObject { ["id"] = e.Id, ["points"] = pts.Count, ["datum"] = datum };
    }
}
