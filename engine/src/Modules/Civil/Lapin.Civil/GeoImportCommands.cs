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
            var sample = raw.Select(p => (p.X, p.Y)).ToList();
            // Map-sized numbers in a system Lapin does not know (a DXF names none): kept as they are, and said.
            var unknown = GeoRef.Get((string?)a["crs"]) == null && GeoRef.LooksLikeMap(sample) && GeoImport.Guess(sample) == null && Location.Crs(ctx.Model) == null;
            var place = GeoImport.Placer(ctx, (string?)a["crs"], sample);
            var pts = raw.Select(p => { var q = place(p.X, p.Y); return new Vec3(q.X, q.Y, p.Z); }).ToList();
            var result = Terrain(ctx, a, pts, (string?)a["name"] ?? "Terrain from DXF");
            if (Location.Crs(ctx.Model) is { } system) result["crs"] = system;
            if (unknown) result["unknownSystem"] = true;
            return result;
        });

        // The layers of a DXF drawing with how many surveyed points each holds (to choose from before importing).
        r.Query("dxfPointLayers", (q, a) =>
        {
            var doc = DxfReader.Read(Text(a, "DXF"));
            return new JsonArray([.. GeoImport.DxfSurveyLayers(doc).Select(x => (JsonNode)new JsonObject { ["layer"] = x.Layer, ["points"] = x.Points })]);
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

        // An orthophoto: an image (imageId, or data + mime, or a GeoTIFF RGB/RGBA) laid by its world file's text, GeoTIFF tags, or its four map corners.
        r.Command("addOrthophoto", "Orthophoto", (ctx, a) =>
        {
            string imageId;
            (double X, double Y)[]? tiffCorners = null;
            string? tiffCrs = null;
            if ((string?)a["imageId"] is { } given) imageId = ctx.Model.Get(given) is { Class: Images.Class } ? given : throw new CommandException("There is no such image.");
            else
            {
                var bytes = Convert.FromBase64String((string?)a["data"] ?? "");
                if (bytes.Length == 0) throw new CommandException("The orthophoto file is empty.");
                string mime;
                if (GeoTiff.IsTiff(bytes))
                {
                    var wf = (string?)a["worldFile"] is { Length: > 0 } wft ? Orthophotos.WorldFile(wft) : null;
                    var rgb = GeoTiff.ReadOrthophoto(bytes, wf);
                    bytes = rgb.PngBytes;
                    mime = "image/png";
                    tiffCorners = rgb.MapCorners;
                    tiffCrs = rgb.Crs;
                }
                else
                {
                    mime = (string?)a["mime"] ?? "";
                    if (!Images.Mimes.Contains(mime)) throw new CommandException("An orthophoto is a PNG, JPEG (with its world file) or a GeoTIFF.");
                }
                if (bytes.Length > Images.MaxBytes) throw new CommandException($"An orthophoto is at most {Images.MaxBytes / 1024 / 1024} MB.");
                var image = new Entity(ctx.NewId(), Images.Class, ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : "Orthophoto", new JsonObject { ["mime"] = mime, ["data"] = Convert.ToBase64String(bytes) });
                ctx.Add(image);
                imageId = image.Id;
            }
            var img = ctx.Model.Get(imageId)!;
            (double X, double Y)[] map;
            if (a["corners"] is JsonArray cs && cs.Count == 4)
                map = cs.Select(x => x is JsonArray p && p.Count >= 2 ? (Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0) : (0.0, 0.0)).ToArray();
            else if (tiffCorners != null)
                map = tiffCorners;
            else
            {
                var wf = Orthophotos.WorldFile((string?)a["worldFile"] ?? "") ?? throw new CommandException("Give the image's world file (.jgw, .pgw, .wld) or its four corners.");
                var size = Orthophotos.PixelSize(Images.Bytes(img)) ?? throw new CommandException("The image's size cannot be read.");
                map = Orthophotos.MapCorners(wf, size.W, size.H);
            }
            var place = GeoImport.Placer(ctx, (string?)a["crs"] ?? tiffCrs, map.ToList());
            var corners = map.Select(p => place(p.X, p.Y)).ToList();
            var e = new Entity(ctx.NewId(), Orthophotos.Class, img.Name, new JsonObject
            {
                ["image"] = imageId, ["corners"] = new JsonArray(corners.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 4), Math.Round(p.Y, 4))).ToArray()), ["opacity"] = 1,
            });
            ctx.Add(e);
            Spatial.Contain(ctx, Storey(ctx, a), e.Id);
            return new JsonObject { ["id"] = e.Id };
        });

        // A terrain from a DEM raster: either a GeoTIFF file (data: base64) or a height grid (width, height, heights / heightsF32, corners or worldFile).
        r.Command("importDem", "Terrain from DEM", (ctx, a) =>
        {
            ElevationGrid grid;
            if ((string?)a["data"] is { Length: > 0 } b64)
            {
                var bytes = Convert.FromBase64String(b64);
                var wf = (string?)a["worldFile"] is { Length: > 0 } wft ? Orthophotos.WorldFile(wft) : null;
                grid = GeoTiff.ReadDem(bytes, wf);
            }
            else
            {
                var w = (int)(Json.Number(a["width"]) ?? 0);
                var h = (int)(Json.Number(a["height"]) ?? 0);
                if (w < 2 || h < 2) throw new CommandException("Give a GeoTIFF file or an elevation grid (width × height ≥ 2×2).");
                var heights = ReadHeights(a, w * h);
                double[] transform;
                if (a["corners"] is JsonArray cs && cs.Count == 4)
                {
                    var corners = cs.Select(x => x is JsonArray p && p.Count >= 2 ? (Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0) : (0.0, 0.0)).ToArray();
                    transform = ElevationGrid.TransformFromCorners(corners, w, h);
                }
                else if ((string?)a["worldFile"] is { Length: > 0 } wft && Orthophotos.WorldFile(wft) is { } wf)
                    transform = wf;
                else
                    throw new CommandException("Give the grid's four corners or a world file.");
                grid = new ElevationGrid(w, h, heights, transform, (string?)a["crs"]);
            }

            var maxError = Json.Number(a["maxError"]) ?? 0.25;
            var maxPoints = (int)(Json.Number(a["maxPoints"]) ?? 3000);
            var sample = grid.SampleMapCoords();
            if (sample.Count == 0) throw new CommandException("The elevation grid has no valid height values.");
            var crs = (string?)a["crs"] ?? grid.Crs;
            var place = GeoImport.Placer(ctx, crs, sample);
            var pts = grid.Simplify(place, maxError, maxPoints);
            if (pts.Count < 3) throw new CommandException("The elevation grid yielded fewer than three valid points.");
            var result = Terrain(ctx, a, pts, (string?)a["name"] ?? "Terrain from DEM");
            if (Location.Crs(ctx.Model) is { } system) result["crs"] = system;
            result["gridWidth"] = grid.Width;
            result["gridHeight"] = grid.Height;
            return result;
        });

        // Info about a linked point cloud before building a terrain from it (point count, ground class count, CRS).
        r.Query("pointCloudTerrainInfo", (q, a) =>
        {
            var id = (string?)a["linkId"] ?? throw new CommandException("Which point cloud?");
            var link = q.Model.Get(id);
            if (link == null || !PointCloudLinks.Is(link)) throw new CommandException("There is no such point cloud.");
            return PointCloudTerrain.Info(q.Model, link);
        });

        // A terrain from a linked LiDAR / point cloud: ground-classified points binned onto a grid and adaptively simplified.
        r.Command("terrainFromPointCloud", "Terrain from point cloud", (ctx, a) =>
        {
            var id = (string?)a["linkId"] ?? throw new CommandException("Which point cloud?");
            var link = ctx.Model.Get(id);
            if (link == null || !PointCloudLinks.Is(link)) throw new CommandException("There is no such point cloud.");
            var groundOnly = a["groundOnly"] is not JsonValue gv || !gv.TryGetValue<bool>(out var g) || g;
            HashSet<byte>? classes = null;
            if (a["classes"] is JsonArray ca && ca.Count > 0)
                classes = ca.Select(x => (int)(Json.Number(x) ?? -1)).Where(v => v is >= 0 and <= 255).Select(v => (byte)v).ToHashSet();
            var gridStep = Json.Number(a["gridStep"]) ?? 1.0;
            var maxError = Json.Number(a["maxError"]) ?? 0.15;
            var maxPoints = (int)(Json.Number(a["maxPoints"]) ?? 4000);
            var built = PointCloudTerrain.Build(ctx.Model, link, groundOnly, classes, gridStep, maxError, maxPoints);
            var result = Terrain(ctx, a, built.Points, (string?)a["name"] ?? $"Terrain from {link.Name}");
            result["sourcePoints"] = built.SourcePoints;
            result["usedGroundClass"] = built.UsedGroundClass;
            result["gridStep"] = built.GridStep;
            return result;
        });

        // Volumetric context buildings from map footprints (e.g. OpenStreetMap Overpass): grounded onto the project's terrain and extruded.
        r.Command("importContextBuildings", "Import context buildings", (ctx, a) =>
        {
            var items = (a["buildings"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
            if (items.Count == 0) throw new CommandException("No buildings to import.");
            var sample = new List<(double X, double Y)>();
            foreach (var b in items)
                if (b["footprint"] is JsonArray fp)
                    foreach (var pt in fp.OfType<JsonArray>())
                        if (pt.Count >= 2 && Json.Number(pt[0]) is { } px && Json.Number(pt[1]) is { } py)
                        {
                            sample.Add((px, py));
                            if (sample.Count >= 500) break;
                        }
            var place = GeoImport.Placer(ctx, (string?)a["crs"], sample);
            var terrains = ContextBuildings.AllTerrains(ctx.Model);
            var storey = Storey(ctx, a);
            var source = (string?)a["source"] ?? "OpenStreetMap";
            var defaultHeight = Math.Max(2.0, Json.Number(a["defaultHeight"]) ?? 7.5);
            var ids = new JsonArray();

            foreach (var b in items)
            {
                if (b["footprint"] is not JsonArray fp) continue;
                var ring = new List<Vec2>();
                foreach (var pt in fp.OfType<JsonArray>())
                    if (pt.Count >= 2 && Json.Number(pt[0]) is { } px && Json.Number(pt[1]) is { } py)
                        ring.Add(place(px, py));
                if (ring.Count < 3) continue;

                var height = Json.Number(b["height"]);
                if (height is not > 0.5 && Json.Number(b["levels"]) is { } lvls and > 0)
                    height = lvls * 3.2;
                var h = Math.Clamp(height ?? defaultHeight, 1.5, 400.0);

                // Embed base slightly below the lowest corner on a slope so the mass never floats; top measured from the highest corner.
                var lowG = ContextBuildings.GroundUnder(terrains, ring, 0);
                var highG = ContextBuildings.HighestGroundUnder(terrains, ring, lowG);
                var baseZ = Json.Number(b["baseZ"]) ?? (terrains.Count > 0 ? lowG - 0.5 : 0);
                var topZ = (terrains.Count > 0 ? highG : baseZ) + h;
                var name = ((string?)b["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Building {ids.Count + 1}";
                var body = ContextBuildings.Extrude(ring, baseZ, topZ, name);
                if (body == null) continue;
                var props = b["properties"] is JsonObject po ? (JsonObject)po.DeepClone() : new JsonObject();
                props["height"] = Math.Round(h, 2);
                var e = SiteFeatures.Add(ctx, storey, "building", name, ring.Select(p => new Vec3(p.X, p.Y, 0)).ToList(), false, source, props, body);
                ids.Add((JsonNode)e.Id);
            }
            return new JsonObject { ["ids"] = ids, ["count"] = ids.Count };
        });

        // Volumetric context buildings extracted from a linked classified LiDAR point cloud (ASPRS class 6).
        r.Command("buildingsFromPointCloud", "Buildings from point cloud", (ctx, a) =>
        {
            var id = (string?)a["linkId"] ?? throw new CommandException("Which point cloud?");
            var link = ctx.Model.Get(id);
            if (link == null || !PointCloudLinks.Is(link)) throw new CommandException("There is no such point cloud.");
            var gridStep = Json.Number(a["gridStep"]) ?? 1.0;
            var minArea = Json.Number(a["minArea"]) ?? 12.0;
            var minHeight = Json.Number(a["minHeight"]) ?? 2.0;
            var extracted = PointCloudBuildings.Extract(ctx.Model, link, gridStep, minArea, minHeight);
            var storey = Storey(ctx, a);
            var ids = new JsonArray();
            foreach (var b in extracted.Buildings)
            {
                var body = ContextBuildings.Extrude(b.Footprint, b.BaseZ, b.TopZ, b.Name);
                if (body == null) continue;
                var props = new JsonObject { ["height"] = Math.Round(b.TopZ - b.BaseZ, 2), ["area"] = Math.Round(b.Area, 1) };
                var e = SiteFeatures.Add(ctx, storey, "building", b.Name, b.Footprint.Select(p => new Vec3(p.X, p.Y, 0)).ToList(), false, "PointCloud", props, body);
                ids.Add((JsonNode)e.Id);
            }
            return new JsonObject { ["ids"] = ids, ["count"] = ids.Count, ["sourcePoints"] = extracted.SourcePoints, ["gridStep"] = extracted.GridStep };
        });
    }

    static double[] ReadHeights(JsonObject a, int count)
    {
        var nodata = Json.Number(a["nodata"]);
        var heights = new double[count];
        if ((string?)a["heightsF32"] is { Length: > 0 } b64)
        {
            var bytes = Convert.FromBase64String(b64);
            if (bytes.Length < count * 4) throw new CommandException("The elevation buffer is shorter than width × height.");
            for (var i = 0; i < count; i++)
            {
                double v = System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * 4, 4));
                heights[i] = !double.IsFinite(v) || (nodata is { } nd && Math.Abs(v - nd) <= 1e-5) ? double.NaN : v;
            }
            return heights;
        }
        if (a["heights"] is JsonArray arr && arr.Count >= count)
        {
            for (var i = 0; i < count; i++)
            {
                var v = Json.Number(arr[i]) ?? double.NaN;
                heights[i] = !double.IsFinite(v) || (nodata is { } nd && Math.Abs(v - nd) <= 1e-5) ? double.NaN : v;
            }
            return heights;
        }
        throw new CommandException("Give elevations as heights (number[]) or heightsF32 (base64 float32).");
    }

    static JsonObject Terrain(ICommandContext ctx, JsonObject a, List<Vec3> pts, string name)
    {
        if (!Tin.Spans(pts)) throw new CommandException("The points lie in a line: no terrain.");
        // More points on a terrain that is there, or a new one (optionally replacing points inside the new patch's footprint).
        if ((string?)a["terrainId"] is { } tid && ctx.Model.Get(tid) is { } t0 && Terrains.IsTerrain(t0))
        {
            var attrs = t0.CloneAttrs();
            var existing = Terrains.Points(t0);
            var replaceInside = a["replaceInside"] is JsonValue rv && rv.TryGetValue<bool>(out var ri) && ri;
            if (replaceInside && pts.Count >= 3)
            {
                var hull = Polygon2.ConvexHull(pts.Select(p => p.Xy).ToList()).ToList();
                if (hull.Count >= 3) existing = existing.Where(p => !Polygon2.Contains(hull, p.Xy)).ToList();
            }
            var all = existing.Concat(pts).ToList();
            attrs["points"] = Terrains.PointsJson(all);
            ctx.Replace(t0.With(attrs: attrs));
            return new JsonObject { ["id"] = t0.Id, ["points"] = pts.Count, ["totalPoints"] = all.Count };
        }
        var datum = Datum(ctx, a, pts.Select(p => p.Z));
        var e = new Entity(ctx.NewId(), IfcClass.GeographicElement, name, Terrains.NewAttrs(ctx, pts, datum), Terrains.Predefined);
        ctx.Add(e);
        Spatial.Contain(ctx, Storey(ctx, a), e.Id);
        var made = new JsonObject { ["id"] = e.Id, ["points"] = pts.Count, ["datum"] = datum };
        if (Terrains.AdoptAltitude(ctx, datum) is { } note) made["note"] = note;
        return made;
    }
}
