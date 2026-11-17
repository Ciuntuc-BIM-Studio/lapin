using System.Text.Json.Nodes;
using Lapin.Geometry;
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
                ["points"] = Terrains.PointsJson(pts), ["datum"] = 0.0, ["interval"] = Terrains.Interval(Json.Number(a["interval"])) ?? 0.5, ["majorEvery"] = 5, ["depth"] = Json.Number(a["depth"]) ?? 2, ["material"] = earth.Id,
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
            list.Add((JsonNode)dab.ToJson());
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
            // Its edges may be arcs (decision 589): {points, arcs?}; the arcs it had stay when only its corners are given, as many.
            var given = a["points"] != null;
            var first = given ? Args.Points(a) : null;
            var t = first is { Count: > 0 } ? TerrainAt(ctx.Model, a, first[0]) : Terrain(ctx.Model, (string?)a["terrainId"] ?? (string?)a["id"]);
            var (attrsPts, arcs) = given ? Terrains.OutlineArg(a, ArcOutline.RisesOf(t, "parcel", Terrains.ParcelArcs), "A parcel") : (null, null);
            var attrs = t.CloneAttrs();
            if (attrsPts != null) attrs["parcel"] = Terrains.ContourJson(attrsPts); else attrs.Remove("parcel");
            ArcOutline.Write(attrs, arcs, Terrains.ParcelArcs);
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["id"] = t.Id, ["area"] = attrsPts == null ? 0 : Math.Round(Math.Abs(ArcOutline.SignedArea(attrsPts, arcs)), 2) };
        });

        // T1: An irregular boundary clipping the terrain's TIN, or none (back to convex hull); fromParcel copies its parcel.
        r.Command("setBoundary", "Terrain boundary", (ctx, a) =>
        {
            var fromParcel = a["fromParcel"] is JsonValue fp && fp.TryGetValue<bool>(out var b) && b;
            var given = a["points"] != null;
            var first = given ? Args.Points(a) : null;
            var t = first is { Count: > 0 } ? TerrainAt(ctx.Model, a, first[0]) : Terrain(ctx.Model, (string?)a["terrainId"] ?? (string?)a["id"]);
            var attrs = t.CloneAttrs();
            if (fromParcel)
            {
                var parcel = ArcOutline.CornersOf(t, "parcel");
                if (parcel.Count < 3) throw new CommandException("The terrain has no parcel to copy.");
                attrs[Terrains.BoundaryKey] = Terrains.ContourJson(parcel);
                ArcOutline.Write(attrs, ArcOutline.RisesOf(t, "parcel", Terrains.ParcelArcs), Terrains.BoundaryArcs);
            }
            else if (given)
            {
                var (pts, arcs) = Terrains.OutlineArg(a, ArcOutline.RisesOf(t, Terrains.BoundaryKey, Terrains.BoundaryArcs), "A terrain boundary");
                attrs[Terrains.BoundaryKey] = Terrains.ContourJson(pts);
                ArcOutline.Write(attrs, arcs, Terrains.BoundaryArcs);
            }
            else
            {
                attrs.Remove(Terrains.BoundaryKey);
                attrs.Remove(Terrains.BoundaryArcs);
            }
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["id"] = t.Id };
        });

        // T1: Split a terrain into two along a line (each half gets its clipped boundary).
        r.Command("splitTerrain", "Split terrain", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["id"]);
            var (p, q) = a["points"] != null
                ? (Args.Points(a) is { Count: >= 2 } pts ? (pts[0], pts[1]) : throw new CommandException("A split line needs two points."))
                : (Args.Point(a, "x1", "y1"), Args.Point(a, "x2", "y2"));
            var corners = ArcOutline.CornersOf(t, Terrains.BoundaryKey);
            double[]? rises = corners.Count >= 3 ? ArcOutline.RisesOf(t, Terrains.BoundaryKey, Terrains.BoundaryArcs) : null;
            if (corners.Count < 3) corners = Terrains.BoundaryLoop(Terrains.Of(t));
            if (corners.Count < 3) throw new CommandException("The terrain has no boundary to split.");
            var (left, right) = OutlineCuts.Split(corners, rises, p, q);
            var (lc, lr) = ArcOutline.Check(left.Corners, left.Rises, "A terrain boundary");
            var (rc, rr) = ArcOutline.Check(right.Corners, right.Rises, "A terrain boundary");
            var storeyId = Spatial.StoreyOf(ctx.Model, t.Id)?.Id ?? StoreyId(ctx.Model, a);
            Entity MakePart(string suffix, IReadOnlyList<Vec2> bc, double[]? br)
            {
                var na = t.CloneAttrs();
                na.Remove("parcel"); na.Remove(Terrains.ParcelArcs);
                na[Terrains.BoundaryKey] = Terrains.ContourJson(bc);
                ArcOutline.Write(na, br, Terrains.BoundaryArcs);
                var part = new Entity(ctx.NewId(), IfcClass.GeographicElement, $"{t.Name} {suffix}", na, Terrains.Predefined);
                ctx.Add(part);
                Spatial.Contain(ctx, storeyId, part.Id);
                return part;
            }
            var first = MakePart("1", lc, lr);
            var second = MakePart("2", rc, rr);
            ctx.Remove(t.Id);
            return new JsonObject { ["ids"] = new JsonArray(first.Id, second.Id) };
        });

        // T1: Merge two or more terrains into one (combines points, breaklines, sculpts).
        r.Command("mergeTerrains", "Merge terrains", (ctx, a) =>
        {
            var ids = (a["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().Distinct().ToList() ?? [];
            if (ids.Count < 2) throw new CommandException("Select two terrains at least to merge.");
            var list = ids.Select(id => Terrain(ctx.Model, id)).ToList();
            var primary = list[0];
            var datum = primary.Num("datum");
            var seen = new HashSet<(long, long)>();
            var pts = new List<Vec3>();
            var breaklines = new JsonArray();
            var sculpts = new JsonArray();
            foreach (var tr in list)
            {
                var d = tr.Num("datum");
                foreach (var p in Terrains.Points(tr))
                {
                    var key = ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6));
                    if (seen.Add(key)) pts.Add(new Vec3(p.X, p.Y, p.Z - d + datum));
                }
                foreach (var bl in Terrains.Breaklines(tr))
                    breaklines.Add((JsonNode)Terrains.PointsJson(bl.Select(p => new Vec3(p.X, p.Y, p.Z - d + datum))));
                foreach (var sc in Sculpt.Of(tr))
                    sculpts.Add((JsonNode)sc.ToJson());
            }
            if (!Tin.Spans(pts)) throw new CommandException("The merged points need three that are not in a line.");
            var attrs = primary.CloneAttrs();
            attrs["points"] = Terrains.PointsJson(pts);
            attrs.Remove(Terrains.BoundaryKey); attrs.Remove(Terrains.BoundaryArcs);
            if (breaklines.Count > 0) attrs["breaklines"] = breaklines;
            if (sculpts.Count > 0) attrs["sculpts"] = sculpts;
            var merged = new Entity(ctx.NewId(), IfcClass.GeographicElement, primary.Name, attrs, Terrains.Predefined);
            var storeyId = Spatial.StoreyOf(ctx.Model, primary.Id)?.Id ?? StoreyId(ctx.Model, a);
            ctx.Add(merged);
            Spatial.Contain(ctx, storeyId, merged.Id);
            foreach (var tr in list) ctx.Remove(tr.Id);
            return new JsonObject { ["id"] = merged.Id, ["points"] = pts.Count };
        });

        // T2: A slope tag on the terrain (point slope [x, y] or two-point grade line [x1, y1, x2, y2]).
        r.Command("addSlope", "Slope tag", (ctx, a) =>
        {
            var hasLine = a["x2"] != null && a["y2"] != null;
            var p1 = hasLine ? Args.Point(a, "x1", "y1") : Args.Point(a, "x", "y");
            var t = TerrainAt(ctx.Model, a, p1);
            var slopes = (t.Attrs["slopes"] as JsonArray)?.Select(x => x!.DeepClone()).ToList() ?? [];
            if (hasLine)
            {
                var p2 = Args.Point(a, "x2", "y2");
                if ((p2 - p1).Length < 0.05) throw new CommandException("A grade line needs two distinct points.");
                slopes.Add(new JsonArray(p1.X, p1.Y, p2.X, p2.Y));
            }
            else slopes.Add(new JsonArray(p1.X, p1.Y));
            var attrs = t.CloneAttrs();
            attrs["slopes"] = new JsonArray([.. slopes]);
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["count"] = slopes.Count };
        });

        r.Command("removeSlope", "Remove slope tag", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["id"]);
            var slopes = Terrains.SlopeTags(t).ToList();
            var attrs = t.CloneAttrs();
            if (a["all"] is JsonValue av && av.TryGetValue<bool>(out var all) && all) slopes.Clear();
            else if (Json.Number(a["index"]) is { } i && i >= 0 && i < slopes.Count) slopes.RemoveAt((int)i);
            else if (a["x"] != null && slopes.Count > 0)
            {
                var p = Args.Point(a, "x", "y");
                slopes.Remove(slopes.MinBy(s => (new Vec2(s[0], s[1]) - p).Length)!);
            }
            if (slopes.Count > 0) attrs["slopes"] = new JsonArray(slopes.Select(s => (JsonNode)new JsonArray([.. s.Select(v => (JsonNode)v)])).ToArray());
            else attrs.Remove("slopes");
            ctx.Replace(t.With(attrs: attrs));
            return null;
        });

        // T5: Freeze the current ground as the existing surface for cut/fill comparison (or reset to the original survey).
        r.Command("snapshotExisting", "Graded region snapshot", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["id"]);
            var attrs = t.CloneAttrs();
            if (a["reset"] is JsonValue rv && rv.TryGetValue<bool>(out var reset) && reset)
            {
                attrs.Remove("existingPoints");
            }
            else
            {
                var datum = t.Num("datum");
                var current = Terrains.Of(t).Tin.Points.Select(p => new Vec3(p.X, p.Y, p.Z + datum)).ToList();
                attrs["existingPoints"] = Terrains.PointsJson(current);
            }
            ctx.Replace(t.With(attrs: attrs));
            return null;
        });

        // T3: Move a single terrain survey point (vertical dz or exact model elevation z, optionally x/y) in one undo step.
        r.Command("moveTerrainPoint", "Move terrain point", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["id"]);
            var pts = Terrains.Points(t);
            var idx = Json.Number(a["index"]) is { } ni ? (int)ni : -1;
            if (idx < 0 || idx >= pts.Count) throw new CommandException("There is no such terrain point.");
            var datum = t.Num("datum");
            var p = pts[idx];
            var x = Json.Number(a["x"]) ?? p.X;
            var y = Json.Number(a["y"]) ?? p.Y;
            var z = Json.Number(a["z"]) is { } exact ? exact + datum : p.Z + (Json.Number(a["dz"]) ?? 0);
            pts[idx] = new Vec3(x, y, z);
            var attrs = t.CloneAttrs();
            attrs["points"] = Terrains.PointsJson(pts);
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["index"] = idx, ["elevation"] = Math.Round(z - datum, 4) };
        });

        // T3: Batch-set or shift multiple terrain points' elevations in one undo step.
        r.Command("setTerrainPoints", "Set terrain points", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["id"]);
            var pts = Terrains.Points(t);
            var indices = (a["indices"] as JsonArray)?.Select(x => Json.Number(x) is { } n ? (int)n : -1).Where(i => i >= 0 && i < pts.Count).Distinct().ToList() ?? [];
            if (indices.Count == 0) throw new CommandException("No terrain points selected.");
            var datum = t.Num("datum");
            var exact = Json.Number(a["z"]);
            var dz = Json.Number(a["dz"]) ?? 0;
            foreach (var i in indices)
            {
                var p = pts[i];
                pts[i] = p with { Z = exact is { } z ? z + datum : p.Z + dz };
            }
            var attrs = t.CloneAttrs();
            attrs["points"] = Terrains.PointsJson(pts);
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["count"] = indices.Count };
        });

        // T3: Add a survey point to an existing terrain (samples ground if z omitted).
        r.Command("addTerrainPoint", "Add terrain point", (ctx, a) =>
        {
            var xy = Args.Point(a, "x", "y");
            var t = TerrainAt(ctx.Model, a, xy);
            var pts = Terrains.Points(t);
            var datum = t.Num("datum");
            var z = Json.Number(a["z"]) is { } exact ? exact + datum : (Terrains.Of(t).ElevationAt(xy.X, xy.Y) ?? 0) + datum;
            pts.Add(new Vec3(xy.X, xy.Y, z));
            var attrs = t.CloneAttrs();
            attrs["points"] = Terrains.PointsJson(pts);
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["index"] = pts.Count - 1, ["points"] = pts.Count };
        });

        // T3: Remove a survey point from a terrain.
        r.Command("removeTerrainPoint", "Remove terrain point", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["id"]);
            var pts = Terrains.Points(t);
            var idx = Json.Number(a["index"]) is { } ni ? (int)ni : -1;
            if (idx < 0 || idx >= pts.Count) throw new CommandException("There is no such terrain point.");
            pts.RemoveAt(idx);
            if (!Tin.Spans(pts)) throw new CommandException("The terrain needs at least three points that are not in a line.");
            var attrs = t.CloneAttrs();
            attrs["points"] = Terrains.PointsJson(pts);
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["points"] = pts.Count };
        });

        // T4: A material sub-region on the terrain (changes surface material without modifying elevation).
        r.Command("addSubregion", "Terrain subregion", (ctx, a) =>
        {
            var pts = Args.Points(a);
            if (pts.Count < 3) throw new CommandException("A subregion needs three corners at least.");
            var t = TerrainAt(ctx.Model, a, pts[0]);
            var (outline, arcs) = Terrains.OutlineArg(a, null, "A subregion");
            var list = (t.Attrs["subregions"] as JsonArray)?.Select(x => x!.DeepClone()).ToList() ?? [];
            var id = ctx.NewId();
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Subregion {list.Count + 1}";
            var mat = Materials.Ensure(ctx, (string?)a["material"] ?? "Gravel", "#9e9b8f");
            var obj = new JsonObject
            {
                ["id"] = id, ["name"] = name,
                ["contour"] = Terrains.ContourJson(outline),
                ["material"] = mat.Id,
            };
            if (arcs != null) obj["arcs"] = new JsonArray([.. arcs.Select(r0 => (JsonNode)Math.Round(r0, 9))]);
            list.Add(obj);
            var attrs = t.CloneAttrs();
            attrs["subregions"] = new JsonArray([.. list]);
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["id"] = id, ["terrainId"] = t.Id };
        });

        r.Command("removeSubregion", "Remove subregion", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["id"]);
            var list = (t.Attrs["subregions"] as JsonArray)?.OfType<JsonObject>().Select(x => x.DeepClone().AsObject()).ToList() ?? [];
            if (a["all"] is JsonValue av && av.TryGetValue<bool>(out var all) && all) list.Clear();
            else if ((string?)a["subregionId"] is { } sid) list.RemoveAll(x => (string?)x["id"] == sid);
            else if (Json.Number(a["index"]) is { } idx && idx >= 0 && idx < list.Count) list.RemoveAt((int)idx);
            var attrs = t.CloneAttrs();
            if (list.Count > 0) attrs["subregions"] = new JsonArray([.. list.Cast<JsonNode>()]); else attrs.Remove("subregions");
            ctx.Replace(t.With(attrs: attrs));
            return null;
        });

        // What the colours mean, and how much ground each band covers.
        r.Query("analysis", (q, a) =>
        {
            var t = Terrain(q.Model, (string?)a["id"]);
            var kind = (string?)a["kind"] ?? t.Str("analysis") ?? "none";
            return Analysis.Of(q.Model, t, kind) is { } r ? Analysis.Legend(r) : null;
        });

        // The terrains and the alignments (the tracks and their branches) as LandXML — those named, else all; surveyed elevations.
        r.Query("landXml", (q, a) =>
        {
            var ids = a["ids"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToHashSet() : null;
            var terrains = q.Model.OfClass(IfcClass.GeographicElement).Where(t => t.PredefinedType == Terrains.Predefined && (ids == null || ids.Contains(t.Id))).ToList();
            var alignments = LandXml.AlignmentSources.SelectMany(s => s(q.Model)).Where(x => ids == null || ids.Contains(x.Id)).ToList();
            if (terrains.Count == 0 && alignments.Count == 0) throw new CommandException("The project has no terrain or alignment to export.");
            var text = LandXml.Write(terrains.Select(t => (t.Name, Terrains.Of(t).Tin, t.Num("datum"))).ToList(), alignments);
            var name = terrains.Count == 1 && alignments.Count == 0 ? terrains[0].Name : alignments.Count == 1 && terrains.Count == 0 ? alignments[0].Name : terrains.Count > 0 && alignments.Count == 0 ? "terrain" : "project";
            return new JsonObject { ["name"] = name + ".xml", ["text"] = text, ["surfaces"] = terrains.Count, ["alignments"] = alignments.Count };
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
        r.Command("addPath", "Path", AddPath);

        r.Command("updatePath", "Change path", (ctx, a) =>
        {
            var e = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Pavement);
            var attrs = e.CloneAttrs();
            if (a["points"] != null) attrs["path"] = Terrains.ContourJson(Polyline(a));
            foreach (var k in new[] { "width", "surfaceThickness", "baseThickness", "kerbHeight", "kerbWidth" }) if (Json.Number(a[k]) is { } v) attrs[k] = v;
            if (a["kerb"] is JsonValue kv && kv.TryGetValue<bool>(out var kerb)) attrs["kerb"] = kerb;
            foreach (var k in new[] { "surfaceMaterial", "baseMaterial" }) if ((string?)a[k] is { } name) attrs[k] = Materials.Ensure(ctx, name).Id;
            CorridorCommands.WithTemplate(attrs, a);
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
                attrs["density"] = Plantings.Density(Json.Number(a["density"]) ?? 3);
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
            foreach (var k in new[] { "density", "seed" }) if (Json.Number(a[k]) is { } v) attrs[k] = k == "density" ? Plantings.Density(v) : v;
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
        var clean = Polyline2.Dedupe(Args.Points(a));
        if (clean.Count < 2) throw new CommandException("The path needs two points at least.");
        return clean;
    }

    /// <summary>A path along points (a road, by its template): the command, and what the civil linework makes of a line.</summary>
    internal static JsonNode? AddPath(ICommandContext ctx, JsonObject a)
    {
        var path = Polyline(a);
        var storeyId = StoreyId(ctx.Model, a);
        var terrain = ctx.Model.OfClass(IfcClass.GeographicElement).FirstOrDefault(t => t.PredefinedType == Terrains.Predefined && Terrains.Of(t).ElevationAt(path[0].X, path[0].Y) != null);
        var c = new Paths.Courses(Json.Number(a["width"]) ?? 1.5, Json.Number(a["surfaceThickness"]) ?? 0.06, Json.Number(a["baseThickness"]) ?? 0.2,
            a["kerb"] is JsonValue kv && kv.TryGetValue<bool>(out var kerb) && kerb, Json.Number(a["kerbHeight"]) ?? 0.1, Json.Number(a["kerbWidth"]) ?? 0.1);
        var surface = Materials.Ensure(ctx, (string?)a["surfaceMaterial"] ?? "Paving stone", Paths.Color);
        var @base = Materials.Ensure(ctx, (string?)a["baseMaterial"] ?? "Gravel", "#a39888");
        var n = ctx.Model.OfClass(IfcClass.Pavement).Count() + 1;
        var e = new Entity(ctx.NewId(), IfcClass.Pavement, ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Path {n}", CorridorCommands.WithTemplate(Paths.Attrs(path, c, surface.Id, @base.Id), a), "FLEXIBLE");
        ctx.Add(e);
        Spatial.Contain(ctx, terrain != null ? Spatial.StoreyOf(ctx.Model, terrain.Id)?.Id ?? storeyId : storeyId, e.Id);
        if (terrain != null) ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, terrain.Id, [e.Id]));
        return new JsonObject { ["id"] = e.Id };
    }
}
