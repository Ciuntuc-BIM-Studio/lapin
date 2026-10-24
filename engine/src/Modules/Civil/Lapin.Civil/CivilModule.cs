using System.Text.Json.Nodes;
using BubbleBim.Geometry;
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
        r.Class(new(IfcClass.GeographicElement, "Terrain",
        [
            new("datum", "Datum (±0.00 =)", "length"),
            new("interval", "Contour interval", "length", Min: 0.05),
            new("majorEvery", "Major contour every", "number", Min: 1, Max: 100),
            new("depth", "Depth below the lowest point", "length", Min: 0.1),
        ], Terrains.Color));
        r.Class(new(IfcClass.EarthworksCut, "Platform", [new("elevation", "Elevation", "length")], "#b39b72"));
        r.Geometry(IfcClass.GeographicElement, Terrains.Geometry);
        r.Quantities(IfcClass.GeographicElement, Terrains.Quantities);
        r.Service<IViewAnnotations>(this);

        // A terrain from points ([x, y, z]) or from a survey file's text (xyz or PENZD).
        r.Command("addTerrain", "Terrain", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var pts = PointsOf(a);
            if (pts.Count < 3) throw new CommandException("The terrain needs at least three points.");
            if (Tin.Delaunay(pts).Triangles.Count == 0) throw new CommandException("The terrain needs at least three points that are not in a line.");
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : "Terrain";
            var earth = Materials.Ensure(ctx, "Earth", Terrains.Color);
            var attrs = new JsonObject
            {
                ["points"] = Terrains.PointsJson(pts), ["datum"] = Json.Number(a["datum"]) ?? 0,
                ["interval"] = Json.Number(a["interval"]) ?? 0.5, ["majorEvery"] = Json.Number(a["majorEvery"]) ?? 5,
                ["depth"] = Json.Number(a["depth"]) ?? 2, ["material"] = earth.Id,
            };
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
                if (Tin.Delaunay(pts).Triangles.Count == 0) throw new CommandException("The terrain needs at least three points that are not in a line.");
                attrs["points"] = Terrains.PointsJson(pts);
            }
            foreach (var k in new[] { "datum", "interval", "majorEvery", "depth" })
                if (Json.Number(a[k]) is { } v) attrs[k] = v;
            ctx.Replace(t.With(attrs: attrs));
            return null;
        });

        // A point moved, added or taken away (an index past the end adds).
        r.Command("setTerrainPoint", "Terrain point", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["id"]);
            var pts = Terrains.Points(t);
            var i = (int)(Json.Number(a["index"]) ?? pts.Count);
            if (a["remove"] is JsonValue rv && rv.TryGetValue<bool>(out var remove) && remove)
            {
                if (i < 0 || i >= pts.Count) throw new CommandException("There is no such point.");
                pts.RemoveAt(i);
            }
            else
            {
                var p = new Vec3(Json.Number(a["x"]) ?? 0, Json.Number(a["y"]) ?? 0, Json.Number(a["z"]) ?? 0);
                if (i >= 0 && i < pts.Count) pts[i] = p; else pts.Add(p);
            }
            if (Tin.Delaunay(pts).Triangles.Count == 0) throw new CommandException("The terrain needs at least three points that are not in a line.");
            var attrs = t.CloneAttrs();
            attrs["points"] = Terrains.PointsJson(pts);
            ctx.Replace(t.With(attrs: attrs));
            return null;
        });

        // A platform: its outline in plan, levelled at its elevation (model height).
        r.Command("addPlatform", "Platform", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["terrainId"] ?? ctx.Model.OfClass(IfcClass.GeographicElement).FirstOrDefault()?.Id);
            var outline = Terrains.CheckOutline(Outline(a));
            var z = Json.Number(a["elevation"]) ?? Terrains.Of(t).ElevationAt(outline.Average(p => p.X), outline.Average(p => p.Y)) ?? 0;
            var n = Terrains.Pads(ctx.Model, t.Id).Count() + 1;
            var pad = new Entity(ctx.NewId(), IfcClass.EarthworksCut, ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Platform {n}",
                new JsonObject { ["contour"] = Terrains.ContourJson(outline), ["elevation"] = Math.Round(z, 3) }, "USERDEFINED");
            ctx.Add(pad);
            ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, t.Id, [pad.Id]));
            return new JsonObject { ["id"] = pad.Id, ["elevation"] = Math.Round(z, 3) };
        });

        r.Command("updatePlatform", "Change platform", (ctx, a) =>
        {
            var pad = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.EarthworksCut);
            var attrs = pad.CloneAttrs();
            if (a["points"] != null || a["x1"] != null) attrs["contour"] = Terrains.ContourJson(Terrains.CheckOutline(Outline(a)));
            if (Json.Number(a["elevation"]) is { } z) attrs["elevation"] = z;
            ctx.Replace(pad.With(attrs: attrs));
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
        if (a["points"] is JsonArray arr)
            return arr.OfType<JsonArray>().Where(p => p.Count >= 3)
                .Select(p => new Vec3(Json.Number(p[0]) ?? double.NaN, Json.Number(p[1]) ?? double.NaN, Json.Number(p[2]) ?? double.NaN))
                .Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z)).ToList();
        throw new CommandException("A terrain needs points: [[x, y, z], …] or a survey file's text.");
    }

    static IReadOnlyList<Vec2> Outline(JsonObject a)
    {
        if (a["points"] is JsonArray arr) return arr.OfType<JsonArray>().Select(p => new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0)).ToList();
        var (p, q) = (new Vec2(Json.Number(a["x1"]) ?? 0, Json.Number(a["y1"]) ?? 0), new Vec2(Json.Number(a["x2"]) ?? 0, Json.Number(a["y2"]) ?? 0));
        return [p, new Vec2(q.X, p.Y), q, new Vec2(p.X, q.Y)];
    }

    /// <summary>In plans, the major contours' elevations (relative to ±0.00), along them; the platforms' elevations inside them.</summary>
    public IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.Kind != "plan") yield break;
        var storey = c.View.Str("storeyId");
        var text = Standards.ResolveText(c.Book, new JsonObject());
        foreach (var e in c.Model.OfClass(IfcClass.GeographicElement))
        {
            if (Spatial.StoreyOf(c.Model, e.Id)?.Id != storey) continue;
            var t = Terrains.Of(e);
            var interval = e.NumOrNull("interval") ?? 0.5;
            var major = Math.Max(1, (int)Math.Round(e.NumOrNull("majorEvery") ?? 5));
            if (interval <= 0 || (t.Bounds.Max.Z - t.Bounds.Min.Z) / interval > 2000) continue;
            var mk = new ViewMark(e.Id);
            foreach (var z in t.Levels(interval * major))
            {
                // On the contour's longest piece, along it.
                var segs = t.Contour(z);
                if (segs.Count == 0) continue;
                var (a, b) = segs.MaxBy(s => (s.B - s.A).Length);
                var angle = -Standards.ReadableDeg(-Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI);
                mk.Text(new PenText((a + b) * 0.5, Standards.FormatLevel(z), text.Height * 0.8, angle, "middle", "middle", false, text.Font), "#5d7a3e");
            }
            foreach (var pad in Terrains.Pads(c.Model, e.Id))
            {
                var o = Terrains.Contour(pad);
                if (o.Count < 3) continue;
                var mid = new Vec2(o.Average(p => p.X), o.Average(p => p.Y));
                mk.Text(new PenText(mid, $"{pad.Name} {Standards.FormatLevel(pad.Num("elevation"))}", text.Height, 0, "middle", "middle", true, text.Font), "#000000");
            }
            yield return mk;
        }
    }
}
