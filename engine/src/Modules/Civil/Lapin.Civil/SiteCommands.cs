using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>The site tools' commands: a flat terrain, the brush, ditches, paths, retaining walls and plantings (docs/spec-site.md).</summary>
public static class SiteCommands
{
    public static void Register(IModuleRegistry r)
    {
        // Level ground for a project without a survey: a rectangle at an elevation (model height).
        r.Command("addFlatTerrain", "Flat terrain", (ctx, a) =>
        {
            var storeyId = StoreyId(ctx.Model, a);
            var (p, q) = (Args.Point(a, "x1", "y1"), Args.Point(a, "x2", "y2"));
            if (Math.Abs(q.X - p.X) < 0.5 || Math.Abs(q.Y - p.Y) < 0.5) throw new CommandException("A flat terrain needs a rectangle of half a metre at least.");
            var z = Json.Number(a["elevation"]) ?? 0;
            var earth = Materials.Ensure(ctx, "Earth", Terrains.Color);
            double x0 = Math.Min(p.X, q.X), x1 = Math.Max(p.X, q.X), y0 = Math.Min(p.Y, q.Y), y1 = Math.Max(p.Y, q.Y);
            // A point every few metres, so the brush has something to work on.
            var pts = new List<Vec3>();
            var step = Math.Max(2, Math.Min(x1 - x0, y1 - y0) / 8);
            for (var x = x0; x <= x1 + 1e-9; x += step) for (var y = y0; y <= y1 + 1e-9; y += step) pts.Add(new Vec3(Math.Min(x, x1), Math.Min(y, y1), z));
            foreach (var cx in new[] { x0, x1 }) foreach (var cy in new[] { y0, y1 }) pts.Add(new Vec3(cx, cy, z));
            var attrs = new JsonObject
            {
                ["points"] = Terrains.PointsJson(pts), ["datum"] = 0.0, ["interval"] = Json.Number(a["interval"]) ?? 0.5, ["majorEvery"] = 5, ["depth"] = Json.Number(a["depth"]) ?? 2, ["material"] = earth.Id,
            };
            var t = new Entity(ctx.NewId(), IfcClass.GeographicElement, ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : "Terrain", attrs, Terrains.Predefined);
            ctx.Add(t);
            Spatial.Contain(ctx, storeyId, t.Id);
            return new JsonObject { ["id"] = t.Id };
        });

        // One stroke of the brush on a terrain: raise or lower by an amount, flatten to a level, smooth with a strength; all within a radius.
        r.Command("sculpt", "Sculpt terrain", (ctx, a) =>
        {
            var t = TerrainAt(ctx.Model, a, Args.Point(a, "x", "y"));
            var dab = SculptDab.From(new JsonObject
            {
                ["kind"] = (string?)a["kind"] ?? "raise", ["x"] = Json.Number(a["x"]) ?? 0, ["y"] = Json.Number(a["y"]) ?? 0, ["radius"] = Json.Number(a["radius"]) ?? 3,
                ["amount"] = Json.Number(a["amount"]) ?? 0.5, ["level"] = Json.Number(a["level"]) ?? Terrains.Of(t).ElevationAt(Json.Number(a["x"]) ?? 0, Json.Number(a["y"]) ?? 0) ?? 0,
                ["strength"] = Json.Number(a["strength"]) ?? 0.5,
            }) ?? throw new CommandException("A stroke is raise, lower, flatten or smooth, with a radius.");
            var attrs = t.CloneAttrs();
            var list = attrs["sculpts"] as JsonArray ?? new JsonArray();
            list.Add(dab.ToJson());
            attrs["sculpts"] = list;
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["strokes"] = list.Count };
        });

        // The strokes taken off a terrain: all of them, or the last.
        r.Command("clearSculpt", "Clear sculpting", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["id"]);
            var attrs = t.CloneAttrs();
            if (a["last"] is JsonValue lv && lv.TryGetValue<bool>(out var last) && last && attrs["sculpts"] is JsonArray arr && arr.Count > 0) arr.RemoveAt(arr.Count - 1);
            else attrs.Remove("sculpts");
            ctx.Replace(t.With(attrs: attrs));
            return null;
        });

        // A spot level on the plan, at a point of the terrain (removed by index, or the nearest to a point).
        r.Command("addSpot", "Spot level", (ctx, a) =>
        {
            var at = Args.Point(a, "x", "y");
            var t = TerrainAt(ctx.Model, a, at);
            var spots = Terrains.Spots(t).ToList();
            spots.Add(at);
            var attrs = t.CloneAttrs();
            attrs["spots"] = Terrains.ContourJson(spots);
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["count"] = spots.Count };
        });

        r.Command("removeSpot", "Remove spot level", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["id"]);
            var spots = Terrains.Spots(t).ToList();
            var attrs = t.CloneAttrs();
            if (a["all"] is JsonValue av && av.TryGetValue<bool>(out var all) && all) spots.Clear();
            else if (Json.Number(a["index"]) is { } i && i >= 0 && i < spots.Count) spots.RemoveAt((int)i);
            else if (a["x"] != null && spots.Count > 0) { var p = Args.Point(a, "x", "y"); spots.Remove(spots.MinBy(q => (q - p).Length)); }
            if (spots.Count > 0) attrs["spots"] = Terrains.ContourJson(spots); else attrs.Remove("spots");
            ctx.Replace(t.With(attrs: attrs));
            return null;
        });

        // The parcel (the property's boundary) on a terrain: a closed outline, or none.
        r.Command("setParcel", "Parcel", (ctx, a) =>
        {
            var attrsPts = a["points"] != null ? Terrains.CheckOutline(Args.Points(a)) : null;
            var t = attrsPts != null ? TerrainAt(ctx.Model, a, attrsPts[0]) : Terrain(ctx.Model, (string?)a["terrainId"] ?? (string?)a["id"]);
            var attrs = t.CloneAttrs();
            if (attrsPts != null) attrs["parcel"] = Terrains.ContourJson(attrsPts); else attrs.Remove("parcel");
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["id"] = t.Id, ["area"] = attrsPts == null ? 0 : Math.Round(Math.Abs(Polygon2.SignedArea(attrsPts)), 2) };
        });

        // What the colours mean, and how much ground each band covers.
        r.Query("analysis", (q, a) =>
        {
            var t = Terrain(q.Model, (string?)a["id"]);
            var kind = (string?)a["kind"] ?? t.Str("analysis") ?? "none";
            return Analysis.Of(q.Model, t, kind) is { } r ? Analysis.Legend(r) : null;
        });

        // The terrains as LandXML (those named, else all), surveyed elevations.
        r.Query("landXml", (q, a) =>
        {
            var ids = a["ids"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToHashSet() : null;
            var terrains = q.Model.OfClass(IfcClass.GeographicElement).Where(t => t.PredefinedType == Terrains.Predefined && (ids == null || ids.Contains(t.Id))).ToList();
            if (terrains.Count == 0) throw new CommandException("The project has no terrain to export.");
            var text = LandXml.Write(terrains.Select(t => (t.Name, Terrains.Of(t).Tin, t.Num("datum"))).ToList());
            return new JsonObject { ["name"] = (terrains.Count == 1 ? terrains[0].Name : "terrain") + ".xml", ["text"] = text, ["surfaces"] = terrains.Count };
        });

        // A ditch along a path, its bottom a depth under the ground, voiding the terrain it runs in.
        r.Command("addDitch", "Ditch", (ctx, a) =>
        {
            var path = Polyline(a);
            var t = TerrainAt(ctx.Model, a, path[0]);
            var n = Terrains.DitchesOf(ctx.Model, t.Id).Count() + 1;
            var ditch = new Entity(ctx.NewId(), IfcClass.EarthworksCut, ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Ditch {n}",
                Ditches.Attrs(path, Json.Number(a["depth"]) ?? 0.5, Json.Number(a["bottomWidth"]) ?? 0.4, Json.Number(a["slope"]) ?? 1), Ditches.Predefined);
            ctx.Add(ditch);
            ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, t.Id, [ditch.Id]));
            return new JsonObject { ["id"] = ditch.Id };
        });

        r.Command("updateDitch", "Change ditch", (ctx, a) =>
        {
            var d = ctx.Model.Get((string?)a["id"] ?? "") is { } x && Ditches.Is(x) ? x : throw new CommandException("There is no such ditch.");
            var attrs = d.CloneAttrs();
            if (a["points"] != null) attrs["path"] = Terrains.ContourJson(Polyline(a));
            foreach (var k in new[] { "depth", "bottomWidth", "slope" }) if (Json.Number(a[k]) is { } v) attrs[k] = v;
            ctx.Replace(d.With(attrs: attrs));
            return null;
        });

        // A path (an alley, a paved way) along a polyline, in the ground of the terrain under its start.
        r.Command("addPath", "Path", (ctx, a) =>
        {
            var path = Polyline(a);
            var storeyId = StoreyId(ctx.Model, a);
            var terrain = ctx.Model.OfClass(IfcClass.GeographicElement).FirstOrDefault(t => t.PredefinedType == Terrains.Predefined && Terrains.Of(t).ElevationAt(path[0].X, path[0].Y) != null);
            var c = new Paths.Courses(Json.Number(a["width"]) ?? 1.5, Json.Number(a["surfaceThickness"]) ?? 0.06, Json.Number(a["baseThickness"]) ?? 0.2,
                a["kerb"] is JsonValue kv && kv.TryGetValue<bool>(out var kerb) && kerb, Json.Number(a["kerbHeight"]) ?? 0.1, Json.Number(a["kerbWidth"]) ?? 0.1);
            var surface = Materials.Ensure(ctx, (string?)a["surfaceMaterial"] ?? "Paving stone", Paths.Color);
            var @base = Materials.Ensure(ctx, (string?)a["baseMaterial"] ?? "Gravel", "#a39888");
            var n = ctx.Model.OfClass(IfcClass.Pavement).Count() + 1;
            var e = new Entity(ctx.NewId(), IfcClass.Pavement, ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Path {n}", Paths.Attrs(path, c, surface.Id, @base.Id), "FLEXIBLE");
            ctx.Add(e);
            Spatial.Contain(ctx, terrain != null ? Spatial.StoreyOf(ctx.Model, terrain.Id)?.Id ?? storeyId : storeyId, e.Id);
            if (terrain != null) ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, terrain.Id, [e.Id]));
            return new JsonObject { ["id"] = e.Id };
        });

        r.Command("updatePath", "Change path", (ctx, a) =>
        {
            var e = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Pavement);
            var attrs = e.CloneAttrs();
            if (a["points"] != null) attrs["path"] = Terrains.ContourJson(Polyline(a));
            foreach (var k in new[] { "width", "surfaceThickness", "baseThickness", "kerbHeight", "kerbWidth" }) if (Json.Number(a[k]) is { } v) attrs[k] = v;
            if (a["kerb"] is JsonValue kv && kv.TryGetValue<bool>(out var kerb)) attrs["kerb"] = kerb;
            foreach (var k in new[] { "surfaceMaterial", "baseMaterial" }) if ((string?)a[k] is { } name) attrs[k] = Materials.Ensure(ctx, name).Id;
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // A retaining wall along a path, the high ground on the side given (left or right of the way it is drawn).
        r.Command("addRetainingWall", "Retaining wall", (ctx, a) =>
        {
            var path = Polyline(a);
            var storeyId = StoreyId(ctx.Model, a);
            var spec = new RetainingWalls.Spec(Json.Number(a["thickness"]) ?? 0.3, Json.Number(a["height"]) ?? 1.0, Json.Number(a["topElevation"]), Json.Number(a["embed"]) ?? 0.3,
                Json.Number(a["footingWidth"]) ?? 0.8, Json.Number(a["footingThickness"]) ?? 0.3, (string?)a["side"] != "right");
            var concrete = Materials.Ensure(ctx, (string?)a["material"] ?? "Reinforced concrete", RetainingWalls.Color);
            var n = ctx.Model.OfClass(RetainingWalls.Class).Count() + 1;
            var e = new Entity(ctx.NewId(), RetainingWalls.Class, ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Retaining wall {n}", RetainingWalls.Attrs(path, spec, concrete.Id), "RETAININGWALL");
            ctx.Add(e);
            Spatial.Contain(ctx, storeyId, e.Id);
            return new JsonObject { ["id"] = e.Id };
        });

        r.Command("updateRetainingWall", "Change retaining wall", (ctx, a) =>
        {
            var e = Spatial.Require(ctx.Model, (string?)a["id"], RetainingWalls.Class);
            var attrs = e.CloneAttrs();
            if (a["points"] != null) attrs["path"] = Terrains.ContourJson(Polyline(a));
            foreach (var k in new[] { "thickness", "height", "embed", "footingWidth", "footingThickness" }) if (Json.Number(a[k]) is { } v) attrs[k] = v;
            if (a.ContainsKey("topElevation")) { if (Json.Number(a["topElevation"]) is { } top) attrs["topElevation"] = top; else attrs.Remove("topElevation"); }
            if ((string?)a["side"] is { } side) attrs["side"] = side == "right" ? "right" : "left";
            if ((string?)a["material"] is { } name) attrs["material"] = Materials.Ensure(ctx, name).Id;
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // A plant at a point (species: the first of the list, or the catalogue's first), or a zone sown with a mix at a density.
        r.Command("addPlanting", "Planting", (ctx, a) =>
        {
            var storeyId = StoreyId(ctx.Model, a);
            var species = (a["species"] as JsonArray)?.Select(Plantings.Parse).OfType<Species>().ToList() is { Count: > 0 } list ? list : null;
            var attrs = new JsonObject { ["seed"] = Json.Number(a["seed"]) ?? Random.Shared.Next(1, 100000), ["exportAs"] = IfcClass.GeographicElement };
            if (species != null) attrs["species"] = Plantings.SpeciesJson(species);
            string name;
            if (a["points"] != null)
            {
                var contour = Terrains.CheckOutline(Args.Points(a));
                attrs["contour"] = Terrains.ContourJson(contour);
                attrs["density"] = Json.Number(a["density"]) ?? 3;
                name = $"Planting {ctx.Model.OfClass(Plantings.Class).Count(x => Plantings.IsZone(x)) + 1}";
            }
            else
            {
                var at = Args.Point(a, "x", "y");
                attrs["at"] = new JsonArray(at.X, at.Y);
                if (species == null) attrs["species"] = Plantings.SpeciesJson([Plantings.Catalog[0]]);
                name = (species ?? [Plantings.Catalog[0]])[0].Name;
            }
            var e = new Entity(ctx.NewId(), Plantings.Class, ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : name, attrs, "VEGETATION");
            ctx.Add(e);
            Spatial.Contain(ctx, storeyId, e.Id);
            return new JsonObject { ["id"] = e.Id };
        });

        r.Command("updatePlanting", "Change planting", (ctx, a) =>
        {
            var e = Spatial.Require(ctx.Model, (string?)a["id"], Plantings.Class);
            var attrs = e.CloneAttrs();
            if (a["points"] != null) attrs["contour"] = Terrains.ContourJson(Terrains.CheckOutline(Args.Points(a)));
            if (a.ContainsKey("x") && a.ContainsKey("y")) { var at = Args.Point(a, "x", "y"); attrs["at"] = new JsonArray(at.X, at.Y); }
            foreach (var k in new[] { "density", "seed" }) if (Json.Number(a[k]) is { } v) attrs[k] = v;
            if (a["species"] is JsonArray arr) attrs["species"] = Plantings.SpeciesJson(arr.Select(Plantings.Parse).OfType<Species>());
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });
    }

    static string StoreyId(IModelView m, JsonObject a) =>
        (string?)a["storeyId"] ?? Spatial.Storeys(m).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");

    static Entity Terrain(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: IfcClass.GeographicElement } t && t.PredefinedType == Terrains.Predefined ? t : throw new CommandException("There is no such terrain.");

    /// <summary>The terrain named, else the one under the point, else the project's first.</summary>
    static Entity TerrainAt(IModelView m, JsonObject a, Vec2 at)
    {
        if ((string?)a["terrainId"] is { } id) return Terrain(m, id);
        var terrains = m.OfClass(IfcClass.GeographicElement).Where(t => t.PredefinedType == Terrains.Predefined).ToList();
        return terrains.FirstOrDefault(t => Terrains.Of(t).ElevationAt(at.X, at.Y) != null) ?? terrains.FirstOrDefault() ?? throw new CommandException("The project has no terrain.");
    }

    static IReadOnlyList<Vec2> Polyline(JsonObject a)
    {
        var pts = Args.Points(a);
        var clean = new List<Vec2>();
        foreach (var p in pts) if (clean.Count == 0 || (p - clean[^1]).Length > 1e-6) clean.Add(p);
        if (clean.Count < 2) throw new CommandException("The path needs two points at least.");
        return clean;
    }
}
