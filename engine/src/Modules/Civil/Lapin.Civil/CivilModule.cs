using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>Civil: the terrain from survey points, its contours and the platforms levelled into it, with their cut and fill.</summary>
public sealed class CivilModule : ILapinModule, IViewAnnotations
{
    public string Id => "civil";
    public string Name => "Civil";

    public void Register(IModuleRegistry r)
    {
        // A terrain moved to another storey with it rises or falls by the levels' difference (its heights are absolute, below its datum).
        r.Service<IStoreyDatum>(new TerrainDatum());
        r.Class(new(IfcClass.GeographicElement, "Terrain",
        [
            new("datum", "Datum (±0.00 =)", "length"),
            new("interval", "Contour interval", "length", Min: 0.05),
            new("majorEvery", "Major contour every", "number", Min: 1, Max: 100),
            new("depth", "Depth below the lowest point", "length", Min: 0.1),
            new("analysis", "Analysis", "choice", Choices: Analysis.Kinds),
        ], Terrains.Color));
        r.Class(new(IfcClass.EarthworksCut, "Earthwork",
        [
            new("elevation", "Elevation (platform)", "length"),
            new("depth", "Depth under the ground (ditch)", "length", Min: 0.05),
            new("bottomWidth", "Bottom width (ditch)", "length", Min: 0.05),
            new("slope", "Side slope 1:n (ditch)", "number", Min: 0, Max: 10),
        ], "#b39b72"));
        r.Class(new(IfcClass.Pavement, "Path",
        [
            // A road's width is its two lanes; an alley's width and the old single kerb switch do not apply to it (report #136).
            new("width", "Width", "length", Min: 0.2, ShowIf: FieldDescriptor.When("template", "!road")),
            new("laneWidth", "Lane width", "length", Min: 1, Max: 6, ShowIf: FieldDescriptor.When("template", "road")),
            new("surfaceThickness", "Surface course", "length", Min: 0), new("baseThickness", "Base course", "length", Min: 0),
            new("kerb", "Kerbs", "bool", ShowIf: FieldDescriptor.When("template", "!road")),
            new("kerbHeight", "Kerb height", "length", Min: 0.02, ShowIf: FieldDescriptor.When("template", "!road")),
            new("kerbWidth", "Kerb width", "length", Min: 0.02, ShowIf: FieldDescriptor.When("template", "!road")),
        ], Paths.Color));
        r.Class(new(RetainingWalls.Class, "Retaining wall",
        [
            new("thickness", "Thickness", "length", Min: 0.1), new("height", "Height over the high ground", "length", Min: 0), new("topElevation", "Crown elevation (fixed)", "length"),
            new("embed", "Embedment under the low ground", "length", Min: 0), new("footingWidth", "Footing width", "length", Min: 0.1), new("footingThickness", "Footing thickness", "length", Min: 0),
            new("side", "High ground on the", "choice", Choices: ["left", "right"]),
        ], RetainingWalls.Color));
        r.Class(new(Plantings.Class, "Planting", [new("density", "Plants per 100 m²", "number", Min: 0.1, Max: 500), new("seed", "Seed", "number", Min: 0, Max: 1e9)], "#5a9440"));
        r.Class(new(Entourage.Class, "Entourage",
        [
            new("kind", "Kind", "text"), new("rotation", "Rotation (°)", "number"),
            new("height", "Height", "length", Min: 0.3, Max: 6), new("offset", "Above the ground", "length"), new("color", "Colour", "color"),
        ], "#9aa3ad"));
        r.Class(new(Entourage.KindClass, "Entourage kind",
        [
            new("image", "Picture", "text", ReadOnly: true), new("height", "Height", "length", Min: 0.2, Max: 10),
            new("width", "Width", "length", Min: 0.1, Max: 10), new("category", "Group", "choice", Choices: Entourage.Categories),
        ], "#9aa3ad", Kind: "type"));
        r.Geometry(IfcClass.GeographicElement, Terrains.Geometry);
        r.Quantities(IfcClass.GeographicElement, Terrains.Quantities);
        r.Geometry(IfcClass.Pavement, (e, m, _) => Paths.Geometry(e, m));
        r.Quantities(IfcClass.Pavement, Paths.Quantities);
        r.Geometry(RetainingWalls.Class, (e, m, _) => RetainingWalls.Geometry(e, m));
        r.Quantities(RetainingWalls.Class, RetainingWalls.Quantities);
        LineworkElements.Register(r);
        // What drafted linework becomes on the site, in Convert to… (audit S12).
        LineworkConverters.Register(r);
        r.Geometry(Plantings.Class, (e, m, _) => Plantings.Geometry(e, m));
        r.Quantities(Plantings.Class, Plantings.Quantities);
        r.Geometry(Entourage.Class, (e, m, _) => Entourage.Geometry(e, m));
        Entourage.Register(r);
        r.Service<IDeletion>(new KindDeletion());
        r.Service<IElementUpdate>(new KindUpdate());
        r.Service<IViewAnnotations>(this);
        r.Service<IGround>(new Ground());
        r.Service<IScheduleSource>(new PlantingSchedule());
        PlantingSchedule.RegisterTemplate(r);
        r.Service<IScheduleSource>(new ParcelCoordinates());
        r.Service<ISectionPaths>(new CivilSectionPaths());
        r.Service<IArrayPaths>(new CivilArrayPaths());
        // Split, Trim/Extend and Offset on paths, roads, retaining walls and ditches (audit §15).
        r.Service<IPolylinePieces>(new CorridorPieces());
        r.Service<IOutlinePieces>(new PlatformPieces());
        r.Service<IOutlinePieces>(new ParcelPieces());
        r.Catalog("plants", Plantings.SpeciesJson(Plantings.Catalog));
        r.Catalog("entourage", new JsonArray([.. Entourage.Kinds.Select(k => (JsonNode)new JsonObject { ["key"] = k.Key, ["label"] = k.Label, ["category"] = k.Category, ["height"] = k.Height, ["color"] = k.Color })]));
        r.Catalog("sculptKinds", new JsonArray(SculptDab.Kinds.Select(k => (JsonNode)k).ToArray()));
        SiteCommands.Register(r);
        GeoImportCommands.Register(r);
        CorridorCommands.Register(r);

        // A terrain from points ([x, y, z]) or from a survey file's text (xyz or PENZD).
        r.Command("addTerrain", "Terrain", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var pts = PointsOf(a);
            if (pts.Count < 3) throw new CommandException("The terrain needs at least three points.");
            if (!Tin.Spans(pts)) throw new CommandException("The terrain needs at least three points that are not in a line.");
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : "Terrain";
            var earth = Materials.Ensure(ctx, "Earth", Terrains.Color);
            var attrs = new JsonObject
            {
                ["points"] = Terrains.PointsJson(pts), ["datum"] = Json.Number(a["datum"]) ?? 0,
                ["interval"] = Terrains.Interval(Json.Number(a["interval"])) ?? 0.5, ["majorEvery"] = Json.Number(a["majorEvery"]) ?? 5,
                ["depth"] = Json.Number(a["depth"]) ?? 2, ["material"] = earth.Id,
            };
            if (a["boundary"] is JsonArray bArr && bArr.Count >= 3)
            {
                var (bPts, bArcs) = Terrains.OutlineArg(new JsonObject { ["points"] = bArr.DeepClone(), ["arcs"] = a["arcs"]?.DeepClone() }, null, "A terrain boundary");
                attrs[Terrains.BoundaryKey] = Terrains.ContourJson(bPts);
                ArcOutline.Write(attrs, bArcs, Terrains.BoundaryArcs);
            }
            var t = new Entity(ctx.NewId(), IfcClass.GeographicElement, name, attrs, Terrains.Predefined);
            ctx.Add(t);
            Spatial.Contain(ctx, storeyId, t.Id);
            return new JsonObject { ["id"] = t.Id, ["points"] = pts.Count };
        });

        r.Command("updateTerrain", "Change terrain", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["id"]);
            var attrs = t.CloneAttrs();
            if (a["points"] != null || a["text"] != null)
            {
                var pts = PointsOf(a);
                if (!Tin.Spans(pts)) throw new CommandException("The terrain needs at least three points that are not in a line.");
                attrs["points"] = Terrains.PointsJson(pts);
                // The cage's controls that no longer stand on a point go.
                Cage.Prune(attrs, pts);
            }
            foreach (var k in new[] { "datum", "interval", "majorEvery", "depth" })
                if (Json.Number(a[k]) is { } v) attrs[k] = k == "interval" ? Terrains.Interval(v) : v;
            if (Json.Number(a["cutFillGrid"]) is { } grid)
            {
                if (grid <= 0) attrs.Remove("cutFillGrid"); else attrs["cutFillGrid"] = Math.Max(0.5, grid);
            }
            if (a["contourLabels"] is JsonValue clv && clv.TryGetValue<bool>(out var cl)) attrs["contourLabels"] = cl;
            if ((string?)a["analysis"] is { } analysis)
            {
                if (!Analysis.Kinds.Contains(analysis)) throw new CommandException("An analysis shows slopes, cut and fill, or elevations; or none.");
                if (analysis == "none") attrs.Remove("analysis"); else attrs["analysis"] = analysis;
            }
            ctx.Replace(t.With(attrs: attrs));
            return null;
        });

        // The surfaces of a LandXML file, each a terrain (on the storey; the datum as given).
        r.Command("importLandXml", "Terrain from LandXML", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var surfaces = LandXml.Surfaces((string?)a["text"] ?? throw new CommandException("Which file?"));
            if (surfaces.Count == 0) throw new CommandException("The file has no surface with points.");
            var earth = Materials.Ensure(ctx, "Earth", Terrains.Color);
            var ids = new JsonArray();
            foreach (var s in surfaces)
            {
                if (!Tin.Spans(s.Points)) continue;
                var t = new Entity(ctx.NewId(), IfcClass.GeographicElement, s.Name, new JsonObject
                {
                    ["points"] = Terrains.PointsJson(s.Points), ["datum"] = Json.Number(a["datum"]) ?? 0, ["interval"] = 0.5, ["majorEvery"] = 5, ["depth"] = 2, ["material"] = earth.Id,
                }, Terrains.Predefined);
                ctx.Add(t);
                Spatial.Contain(ctx, storeyId, t.Id);
                ids.Add((JsonNode)t.Id);
            }
            return new JsonObject { ["ids"] = ids };
        });

        // A breakline the terrain must keep (a kerb, a ditch's bottom): surveyed points [[x, y, z], …]; remove: its index.
        r.Command("setBreakline", "Breakline", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["id"]);
            var lines = (t.Attrs["breaklines"] as JsonArray)?.Select(x => x!.DeepClone()).ToList() ?? [];
            if (Json.Number(a["remove"]) is { } ri) { if (ri < 0 || ri >= lines.Count) throw new CommandException("There is no such breakline."); lines.RemoveAt((int)ri); }
            else
            {
                var pts = PointsOf(new JsonObject { ["points"] = a["points"]?.DeepClone() });
                if (pts.Count < 2) throw new CommandException("A breakline needs two points at least.");
                lines.Add(Terrains.PointsJson(pts));
            }
            var attrs = t.CloneAttrs();
            if (lines.Count > 0) attrs["breaklines"] = new JsonArray([.. lines]); else attrs.Remove("breaklines");
            ctx.Replace(t.With(attrs: attrs));
            return null;
        });

        // A platform: its outline in plan, levelled at its elevation (model height).
        r.Command("addPlatform", "Platform", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["terrainId"] ?? ctx.Model.OfClass(IfcClass.GeographicElement).FirstOrDefault()?.Id);
            var (outline, arcs) = Terrains.OutlineArg(a, null);
            var z = Json.Number(a["elevation"]) ?? Terrains.Of(t).ElevationAt(outline.Average(p => p.X), outline.Average(p => p.Y)) ?? 0;
            var n = Terrains.Pads(ctx.Model, t.Id).Count() + 1;
            var attrs = new JsonObject { ["contour"] = Terrains.ContourJson(outline), ["elevation"] = Math.Round(z, 3) };
            ArcOutline.Write(attrs, arcs);
            if (a[OutlineHoles.Key] != null) OutlineHoles.Apply(attrs, a);
            var pad = new Entity(ctx.NewId(), IfcClass.EarthworksCut, ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Platform {n}", attrs, "USERDEFINED");
            ctx.Add(pad);
            ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, t.Id, [pad.Id]));
            return new JsonObject { ["id"] = pad.Id, ["elevation"] = Math.Round(z, 3) };
        });

        r.Command("updatePlatform", "Change platform", (ctx, a) =>
        {
            var pad = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.EarthworksCut);
            var attrs = pad.CloneAttrs();
            if (a["points"] != null || a["x1"] != null)
            {
                var (outline, arcs) = Terrains.OutlineArg(a, ArcOutline.RisesOf(pad));
                attrs["contour"] = Terrains.ContourJson(outline);
                ArcOutline.Write(attrs, arcs);
            }
            // Its own holes (audit S4): given, or kept and checked against the outline as it now is.
            if (a[OutlineHoles.Key] != null || a["points"] != null || a["x1"] != null) OutlineHoles.Apply(attrs, a);
            if (Json.Number(a["elevation"]) is { } z) attrs["elevation"] = z;
            foreach (var k in new[] { "cutSlope", "fillSlope" })
                if (Json.Number(a[k]) is { } s) { if (s <= 0) attrs.Remove(k); else attrs[k] = s; }
            ctx.Replace(pad.With(attrs: attrs));
            return null;
        });

        // A hole in a platform's outline (audit S4), its ground kept as it is: {id? (else the platform that holds the loop), points,
        // arcs?} → {id}; {id, hole} takes one away.
        r.Command("addOutlineHole", "Hole in platform", (ctx, a) =>
        {
            var pts = Args.Points(a);
            var pad = (string?)a["id"] is { } id ? Spatial.Require(ctx.Model, id, IfcClass.EarthworksCut)
                : ctx.Model.OfClass(IfcClass.EarthworksCut).FirstOrDefault(x => !Ditches.Is(x) && OutlineHoles.Holds(x, pts)) ?? throw new CommandException("Draw the hole inside a platform.");
            if (Ditches.Is(pad)) throw new CommandException("A ditch has no holes.");
            ctx.Replace(pad.With(attrs: OutlineHoles.Added(pad, a)));
            return new JsonObject { ["id"] = pad.Id };
        });
        r.Command("removeOutlineHole", "Remove hole", (ctx, a) =>
        {
            var pad = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.EarthworksCut);
            var k = OutlineHoles.Index(a, pad) ?? throw new CommandException("Which hole? Its index, from 0.");
            ctx.Replace(pad.With(attrs: OutlineHoles.Removed(pad, k)));
            return null;
        });

        // A platform's or a parcel's corner rounded (a fillet tangent to its two straight edges) or cut straight across, or an edge
        // bent into an arc or made straight (decision 589, as a floor's): {id (the platform, or the terrain for its parcel), index
        // (the corner; the edge from it), radius | distance | rise}.
        r.Command("roundCorner", "Fillet corner", (ctx, a) =>
        {
            var (e, key, arcsKey, corners, rises) = OutlineOf(ctx, a);
            var (c, r2) = ArcOutline.Fillet(corners, rises, CornerIndex(a, corners.Count), Json.Number(a["radius"]) ?? throw new CommandException("What radius? A length in metres."));
            ReplaceOutline(ctx, e, key, arcsKey, c, r2);
            return null;
        });
        r.Command("chamferCorner", "Chamfer corner", (ctx, a) =>
        {
            var (e, key, arcsKey, corners, rises) = OutlineOf(ctx, a);
            var (c, r2) = ArcOutline.Chamfer(corners, rises, CornerIndex(a, corners.Count), Json.Number(a["distance"]) ?? throw new CommandException("What distance? A length in metres."));
            ReplaceOutline(ctx, e, key, arcsKey, c, r2);
            return null;
        });
        r.Command("setEdgeArc", "Edge arc", (ctx, a) =>
        {
            var (e, key, arcsKey, corners, rises) = OutlineOf(ctx, a);
            var next = rises?.ToArray() ?? new double[corners.Count];
            next[CornerIndex(a, corners.Count)] = Json.Number(a["rise"]) ?? 0;
            ReplaceOutline(ctx, e, key, arcsKey, corners, next);
            return null;
        });

        // The ground's height at a point (model height), for tools that place on it.
        r.Query("civil.elevationAt", (q, a) =>
        {
            var t = Terrain(q.Model, (string?)a["id"] ?? q.Model.OfClass(IfcClass.GeographicElement).FirstOrDefault()?.Id);
            return Terrains.Of(t).ElevationAt(Json.Number(a["x"]) ?? 0, Json.Number(a["y"]) ?? 0) is { } z ? JsonValue.Create(z) : null;
        });
    }

    static Entity Terrain(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: IfcClass.GeographicElement } t ? t : throw new CommandException("There is no such terrain.");

    static List<Vec3> PointsOf(JsonObject a)
    {
        if ((string?)a["text"] is { } text) return Tin.ParseSurveyPoints(text);
        if (a["points"] is JsonArray arr) return PointLists.Xyz(arr);
        throw new CommandException("A terrain needs points: [[x, y, z], …] or a survey file's text.");
    }

    /// <summary>The outline an edit names: a platform's (<c>contour</c>, <c>arcs</c>) or, a terrain named, its parcel's (<c>parcel</c>, <c>parcelArcs</c>).</summary>
    static (Entity E, string Key, string ArcsKey, List<Vec2> Corners, double[]? Rises) OutlineOf(ICommandContext ctx, JsonObject a)
    {
        var e = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("The element is not in the project.");
        var (key, arcsKey) = e.Class == IfcClass.EarthworksCut && !Ditches.Is(e) ? ("contour", ArcOutline.Key)
            : Terrains.IsTerrain(e) ? ("parcel", Terrains.ParcelArcs)
            : throw new CommandException("Only a platform or a terrain's parcel has its outline's edges bent and its corners rounded here.");
        var corners = ArcOutline.CornersOf(e, key);
        if (corners.Count < 3) throw new CommandException(key == "parcel" ? "The terrain has no parcel." : "The platform has no outline.");
        // One of a platform's own holes (audit S4): {hole: its index}.
        if (key == "contour" && OutlineHoles.Index(a, e) is { } k) { var h = OutlineHoles.Of(e)[k]; return (e, "hole:" + k, arcsKey, [.. h.Corners], h.Rises); }
        return (e, key, arcsKey, corners, ArcOutline.RisesOf(e, key, arcsKey));
    }

    static int CornerIndex(JsonObject a, int n) =>
        Json.Number(a["index"]) is { } ix && ix >= 0 && ix < n && ix == Math.Floor(ix) ? (int)ix : throw new CommandException($"Which corner? The outline has {n}, from 0.");

    static void ReplaceOutline(ICommandContext ctx, Entity e, string key, string arcsKey, IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises)
    {
        if (key.StartsWith("hole:", StringComparison.Ordinal)) { ctx.Replace(e.With(attrs: OutlineHoles.Replaced(e, int.Parse(key[5..], System.Globalization.CultureInfo.InvariantCulture), corners, rises))); return; }
        var (c, r) = ArcOutline.Check(corners, rises, key == "parcel" ? "A parcel" : "A platform");
        var attrs = e.CloneAttrs();
        attrs[key] = Terrains.ContourJson(c);
        ArcOutline.Write(attrs, r, arcsKey);
        if (key == "contour") OutlineHoles.Apply(attrs, new JsonObject());
        ctx.Replace(e.With(attrs: attrs));
    }

    /// <summary>In plans, the orthophotos; the major contours' elevations (relative to ±0.00), along them; the platforms' elevations inside them.</summary>
    public IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.Kind != "plan") yield break;
        var storey = c.View.Str("storeyId");
        // The orthophotos under it all, laid by their corners: a site view shows every one, any other plan those of its storey (or of none).
        foreach (var o in c.Model.OfClass(Orthophotos.Class))
        {
            if (SitePlans.Of(c.View) == null && Spatial.StoreyOf(c.Model, o.Id) is { } on && on.Id != storey) continue;
            if (o.Str("image") is not { } image || Orthophotos.Corners(o) is not { Count: 4 } corners) continue;
            var mk = new ViewMark(o.Id);
            mk.Images.Add(new PenImage(image, [.. corners.Select(p => c.Map(p))], Math.Clamp(o.NumOrNull("opacity") ?? 1, 0.05, 1)));
            yield return mk;
        }
        var text = Standards.ResolveText(c.Book, new JsonObject());
        foreach (var e in c.Model.OfClass(IfcClass.GeographicElement))
        {
            // A site plan shows every terrain; any other plan its own storey's.
            var site = SitePlans.Of(c.View);
            if (site == null && Spatial.StoreyOf(c.Model, e.Id)?.Id != storey) continue;
            var t = Terrains.Of(e);
            var interval = e.NumOrNull("interval") ?? 0.5;
            var major = Math.Max(1, (int)Math.Round(e.NumOrNull("majorEvery") ?? 5));
            if (interval <= 0 || (t.Bounds.Max.Z - t.Bounds.Min.Z) / interval > 2000) continue;
            var mk = new ViewMark(e.Id);
            var padAreas = Terrains.Pads(c.Model, e.Id)
                .Select(p => (Outer: ArcOutline.BuiltOf(p), Holes: (IReadOnlyList<IReadOnlyList<Vec2>>)OutlineHoles.Built(p)))
                .Where(p => p.Outer.Count >= 3).ToList();
            if (e.Attrs["contourLabels"] is not JsonValue clv || !clv.TryGetValue<bool>(out var showLabels) || showLabels)
            {
                foreach (var z in t.Levels(interval * major))
                {
                    foreach (var line in Terrains.ChainedContours(t, z, padAreas))
                    {
                        if (MidpointTangent(line) is not { } mid || mid.Length < Math.Max(1.5, text.Height * 4)) continue;
                        var angle = -Standards.ReadableDeg(-Math.Atan2(mid.Dir.Y, mid.Dir.X) * 180 / Math.PI);
                        mk.Text(new PenText(mid.Pos, Standards.FormatLevel(z, c.Book), text.Height * 0.8, angle, "middle", "middle", true, text.Font), "#5d7a3e");
                    }
                }
            }
            foreach (var pad in Terrains.Pads(c.Model, e.Id))
            {
                var o = ArcOutline.BuiltOf(pad);
                if (o.Count < 3) continue;
                var mid = new Vec2(o.Average(p => p.X), o.Average(p => p.Y));
                mk.Text(new PenText(mid, $"{pad.Name} {Standards.FormatLevel(pad.Num("elevation"), c.Book)}", text.Height, 0, "middle", "middle", true, text.Font), "#000000");
            }
            // Spot levels: a cross and the designed ground's height beside it.
            var r = 1.2 * c.Mm;
            foreach (var p in Terrains.Spots(e))
            {
                if (Analysis.Proposed(c.Model, e, t, p) is not { } z) continue;
                mk.Line(p - new Vec2(r, 0), p + new Vec2(r, 0), "LEVELS", Pens.Default.Seen, "#000000");
                mk.Line(p - new Vec2(0, r), p + new Vec2(0, r), "LEVELS", Pens.Default.Seen, "#000000");
                mk.Text(new PenText(p + new Vec2(r * 1.2, r * 0.3), Standards.FormatLevel(z, c.Book), text.Height * 0.8, 0, "start", "bottom", false, text.Font), "#000000");
                mk.Snaps.Add(p);
            }
            // Slope tags: point gradient arrow or two-point grade line.
            foreach (var s in Terrains.SlopeTags(e))
            {
                if (s.Length == 2)
                {
                    var p = new Vec2(s[0], s[1]);
                    if (Analysis.Proposed(c.Model, e, t, p) is not { } z0) continue;
                    var h = Math.Max(0.25, Math.Min(t.Bounds.Max.X - t.Bounds.Min.X, t.Bounds.Max.Y - t.Bounds.Min.Y) / 100);
                    var zx1 = Analysis.Proposed(c.Model, e, t, new Vec2(p.X + h, p.Y)) ?? z0;
                    var zx0 = Analysis.Proposed(c.Model, e, t, new Vec2(p.X - h, p.Y)) ?? z0;
                    var zy1 = Analysis.Proposed(c.Model, e, t, new Vec2(p.X, p.Y + h)) ?? z0;
                    var zy0 = Analysis.Proposed(c.Model, e, t, new Vec2(p.X, p.Y - h)) ?? z0;
                    var grad = new Vec2((zx1 - zx0) / (2 * h), (zy1 - zy0) / (2 * h));
                    var pct = grad.Length * 100;
                    var dir = grad.Length > 1e-6 ? (-grad).Normalized() : new Vec2(1, 0);
                    var len = 3.0 * c.Mm;
                    var a = p - dir * (len * 0.5);
                    var b = p + dir * (len * 0.5);
                    DrawSlopeArrow(mk, a, b, c.Mm, $"{pct:0.0} %", text.Height, text.Font);
                    mk.Snaps.Add(p);
                }
                else if (s.Length == 4)
                {
                    var (p1, p2) = (new Vec2(s[0], s[1]), new Vec2(s[2], s[3]));
                    var run = (p2 - p1).Length;
                    if (run < 1e-6 || Analysis.Proposed(c.Model, e, t, p1) is not { } z1 || Analysis.Proposed(c.Model, e, t, p2) is not { } z2) continue;
                    var (hi, lo) = z1 >= z2 ? (p1, p2) : (p2, p1);
                    var dz = Math.Abs(z1 - z2);
                    var pct = dz / run * 100;
                    var label = dz > 1e-4 ? $"{pct:0.0} % (1:{run / dz:0.0})" : "0.0 %";
                    DrawSlopeArrow(mk, hi, lo, c.Mm, label, text.Height, text.Font);
                    mk.Snaps.Add(p1); mk.Snaps.Add(p2);
                }
            }
            // Cut/fill grid labels in plan (T5).
            if (e.NumOrNull("cutFillGrid") is { } grid && grid >= 0.5)
            {
                var existing = Terrains.Existing(e);
                var (min, max) = t.Bounds;
                var nx = (int)Math.Ceiling((max.X - min.X) / grid);
                var ny = (int)Math.Ceiling((max.Y - min.Y) / grid);
                if ((long)nx * ny <= 2500)
                {
                    var x0 = Math.Ceiling(min.X / grid) * grid;
                    var y0 = Math.Ceiling(min.Y / grid) * grid;
                    var cr = 0.6 * c.Mm;
                    for (var gx = x0; gx <= max.X + 1e-9; gx += grid)
                        for (var gy = y0; gy <= max.Y + 1e-9; gy += grid)
                        {
                            var gp = new Vec2(gx, gy);
                            if (Analysis.Proposed(c.Model, e, t, gp) is not { } zp || existing.ElevationAt(gx, gy) is not { } ze) continue;
                            var dz = zp - ze;
                            var col = dz > 0.01 ? "#2166ac" : dz < -0.01 ? "#c0392b" : "#666666";
                            mk.Line(gp - new Vec2(cr, 0), gp + new Vec2(cr, 0), "LEVELS", Pens.Default.Seen, col);
                            mk.Line(gp - new Vec2(0, cr), gp + new Vec2(0, cr), "LEVELS", Pens.Default.Seen, col);
                            var txt = dz >= 0 ? $"+{dz:0.00}" : $"{dz:0.00}";
                            mk.Text(new PenText(gp + new Vec2(cr * 1.2, 0), txt, text.Height * 0.7, 0, "start", "middle", false, text.Font), col);
                        }
                }
            }
            // The parcel: its boundary, dash-dotted, and its area.
            if (ArcOutline.BuiltOf(e, "parcel", Terrains.ParcelArcs) is { Count: >= 3 } parcel)
            {
                // A site view that highlights the project draws the parcel in its colour, heavier.
                if (site != null && SitePlans.Highlight(c.View)) mk.Polyline(parcel, true, "PARCEL", Pens.Default.Cut * 2, SitePlans.ProjectColor, [6, 2, 1, 2]);
                else mk.Polyline(parcel, true, "PARCEL", Pens.Default.Cut, "#6b4e2e", [6, 2, 1, 2]);
                var centre = new Vec2(parcel.Average(p => p.X), parcel.Average(p => p.Y));
                mk.Text(new PenText(centre, $"S = {ArcOutline.AreaOf(e, "parcel", Terrains.ParcelArcs):0.0} m²", text.Height, 0, "middle", "middle", true, text.Font), "#6b4e2e");
                // On a site plan its corners are numbered, as the coordinate inventory lists them (ParcelCoordinates).
                if (site == SitePlans.Situation)
                {
                    var corners = ArcOutline.CornersOf(e, "parcel");
                    for (var k = 0; k < corners.Count; k++)
                    {
                        var away = corners[k] - centre;
                        var at = corners[k] + (away.Length > 1e-9 ? away.Normalized() : new Vec2(1, 0)) * (2.5 * c.Mm);
                        mk.Text(new PenText(at, (k + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), text.Height * 0.8, 0, "middle", "middle", false, text.Font), "#6b4e2e");
                    }
                }
            }
            // The analysis, as fills under the lines.
            if (e.Str("analysis") is { } kind && kind != "none" && Analysis.Of(c.Model, e, kind) is { } analysis)
                foreach (var (loops, color) in Analysis.Fills(analysis)) mk.Fills.Add((loops, color));
            yield return mk;
        }
    }

    static (Vec2 Pos, Vec2 Dir, double Length)? MidpointTangent(IReadOnlyList<Vec2> line)
    {
        if (line.Count < 2) return null;
        double total = 0;
        for (var i = 1; i < line.Count; i++) total += (line[i] - line[i - 1]).Length;
        if (total < 1e-6) return null;
        var half = total * 0.5;
        double acc = 0;
        for (var i = 1; i < line.Count; i++)
        {
            var seg = line[i] - line[i - 1];
            var len = seg.Length;
            if (acc + len >= half && len > 1e-9)
            {
                var t = (half - acc) / len;
                return (line[i - 1] + seg * t, seg * (1 / len), total);
            }
            acc += len;
        }
        var last = line[^1] - line[^2];
        return (line[^1], last.Length > 1e-9 ? last.Normalized() : new Vec2(1, 0), total);
    }

    static void DrawSlopeArrow(ViewMark mk, Vec2 a, Vec2 b, double mm, string label, double textHeight, string font)
    {
        var dir = (b - a).Normalized();
        var perp = new Vec2(-dir.Y, dir.X);
        var head = 1.2 * mm;
        mk.Line(a, b, "LEVELS", Pens.Default.Seen, "#2d5a27");
        mk.Line(b, b - dir * head + perp * (head * 0.45), "LEVELS", Pens.Default.Seen, "#2d5a27");
        mk.Line(b, b - dir * head - perp * (head * 0.45), "LEVELS", Pens.Default.Seen, "#2d5a27");
        var angle = -Standards.ReadableDeg(-Math.Atan2(dir.Y, dir.X) * 180 / Math.PI);
        mk.Text(new PenText((a + b) * 0.5 + perp * (1.1 * mm), label, textHeight * 0.8, angle, "middle", "middle", true, font), "#2d5a27");
    }
}

/// <summary>The ground's height under a point: the first terrain that reaches there, with its brush strokes (model height).</summary>
sealed class Ground : IGround
{
    public double? ElevationAt(IModelView model, double x, double y)
    {
        foreach (var t in model.OfClass(IfcClass.GeographicElement))
            if (t.PredefinedType == Terrains.Predefined && Terrains.Of(t).ElevationAt(x, y) is { } z) return z;
        return null;
    }
}

/// <summary>What a section can follow on the site: a path's, a retaining wall's or a ditch's axis (from the station it starts at), a site feature's line.</summary>
sealed class CivilSectionPaths : ISectionPaths
{
    public FollowedPath? PathOf(IModelView m, Entity e)
    {
        if (Corridors.Is(e)) return new FollowedPath(Corridors.ChainOf(e).Path, CorridorSpec.Of(e).StationStart);
        if (e.Class == SiteFeatures.Class && SiteFeatures.Kind(e) is "line" or "parcel" or "area") return new FollowedPath(SiteFeatures.Points(e).Select(p => p.Xy).ToList());
        return null;
    }
}

/// <summary>
/// What an array can follow on the site (decision 481): a road's, a path's, a retaining wall's or a ditch's axis exactly,
/// along its curves and clothoids (its alignment), from its start; a site feature's line by its points.
/// </summary>
sealed class CivilArrayPaths : IArrayPaths
{
    public ArrayPath? PathOf(IModelView m, Entity e)
    {
        if (Corridors.Is(e))
        {
            var c = Corridors.ChainOf(e);
            if (c.Length < 1e-9) return null;
            var axis = c.Axis(Chords.Tolerance);
            return new ArrayPath(c.Length, s => (c.PointAt(c.Start + s), c.DirectionAt(c.Start + s)), axis, (axis[0] - axis[^1]).Length < 1e-6);
        }
        if (e.Class == SiteFeatures.Class && SiteFeatures.Kind(e) is "line" or "parcel" or "area" && SiteFeatures.Points(e).Select(p => p.Xy).ToList() is { Count: >= 2 } pts)
            return ArrayPath.Polyline(pts);
        return null;
    }
}

sealed class TerrainDatum : IStoreyDatum
{
    public IEnumerable<string> DatumsOf(Entity e) => Terrains.IsTerrain(e) ? ["datum"] : [];
}
