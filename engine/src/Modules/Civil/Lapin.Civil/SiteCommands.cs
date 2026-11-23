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
            double x0 = Math.Min(p.X, q.X), x1 = Math.Max(p.X, q.X), y0 = Math.Min(p.Y, q.Y), y1 = Math.Max(p.Y, q.Y);
            // A point every few metres, so the brush has something to work on.
            var pts = new List<Vec3>();
            var step = Math.Max(2, Math.Min(x1 - x0, y1 - y0) / 8);
            for (var x = x0; x <= x1 + 1e-9; x += step) for (var y = y0; y <= y1 + 1e-9; y += step) pts.Add(new Vec3(Math.Min(x, x1), Math.Min(y, y1), z));
            foreach (var cx in new[] { x0, x1 }) foreach (var cy in new[] { y0, y1 }) pts.Add(new Vec3(cx, cy, z));
            var attrs = Terrains.NewAttrs(ctx, pts, 0.0, Json.Number(a["interval"]), depth: Json.Number(a["depth"]));
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
            // A terrain whose outline is its own points keeps it so (T1, TerrainOutlines.cs): the new vertices become points, the gone ones go.
            TerrainOutlines.Follow(attrs, t, prune: true, byOrder: true);
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["id"] = t.Id };
        });

        // T1: Split a terrain into two along a line. The larger half stays the terrain (its GlobalId, relations and
        // everything voiding it); the other half is a new terrain like it, and what lies in it (platforms, ditches,
        // paths: by their outline's centroid or their path's midpoint) moves over to it. One undo step.
        r.Command("splitTerrain", "Split terrain", (ctx, a) =>
        {
            var (p, q) = a["points"] != null
                ? (Args.Points(a) is { Count: >= 2 } pts ? (pts[0], pts[1]) : throw new CommandException("A split line needs two points."))
                : (Args.Point(a, "x1", "y1"), Args.Point(a, "x2", "y2"));
            // The terrain named, else the one under the line's middle.
            var t = (string?)a["id"] is { } tid ? Terrain(ctx.Model, tid) : TerrainAt(ctx.Model, a, (p + q) * 0.5);
            var corners = ArcOutline.CornersOf(t, Terrains.BoundaryKey);
            double[]? rises = corners.Count >= 3 ? ArcOutline.RisesOf(t, Terrains.BoundaryKey, Terrains.BoundaryArcs) : null;
            if (corners.Count < 3) corners = Terrains.BoundaryLoop(Terrains.Of(t));
            if (corners.Count < 3) throw new CommandException("The terrain has no boundary to split.");
            var (left, right) = OutlineCuts.Split(corners, rises, p, q);
            var (lc, lr) = ArcOutline.Check(left.Corners, left.Rises, "A terrain boundary", simple: true);
            var (rc, rr) = ArcOutline.Check(right.Corners, right.Rises, "A terrain boundary", simple: true);
            // The larger half keeps the terrain.
            if (Math.Abs(Polygon2.SignedArea(rc)) > Math.Abs(Polygon2.SignedArea(lc))) (lc, lr, rc, rr) = (rc, rr, lc, lr);
            // The parcel (the property line, not the ground) stays whole, on the half that holds its centre; it was
            // dropped from both halves (audit 22.11.2026, G1).
            var parcel = ArcOutline.CornersOf(t, "parcel");
            var parcelInOther = parcel.Count >= 3 && Polygon2.Centroid(parcel) is var pc && !Polygon2.Contains(lc, pc) && Polygon2.Contains(rc, pc);
            JsonObject Half(IReadOnlyList<Vec2> bc, double[]? br, bool other)
            {
                var na = t.CloneAttrs();
                if (parcel.Count < 3 || parcelInOther != other) { na.Remove("parcel"); na.Remove(Terrains.ParcelArcs); }
                na[Terrains.BoundaryKey] = Terrains.ContourJson(bc);
                ArcOutline.Write(na, br, Terrains.BoundaryArcs);
                return na;
            }
            var keptAttrs = Half(lc, lr, false);
            var otherAttrs = Half(rc, rr, true);
            // Each half keeps the cage's controls inside it.
            var allPts = Terrains.Points(t);
            Cage.Prune(keptAttrs, allPts, lc);
            Cage.Prune(otherAttrs, allPts, rc);
            // A terrain whose outline is its own points: each half's too, the cut's ends at the ground's height (T1).
            TerrainOutlines.Follow(keptAttrs, t, prune: true);
            TerrainOutlines.Follow(otherAttrs, t, prune: true);
            // What is placed on the terrain (spot levels, slope tags, subregions) goes to the half it lies in.
            bool InOther(JsonNode? item) => PlacedAt(item) is { } at && !Polygon2.Contains(lc, at) && Polygon2.Contains(rc, at);
            foreach (var key in PlacedKeys)
            {
                if (t.Attrs[key] is not JsonArray all) continue;
                JsonArray Of(bool other) => new([.. all.Where(x => InOther(x) == other).Select(x => x?.DeepClone())]);
                foreach (var (target, other) in new[] { (keptAttrs, false), (otherAttrs, true) })
                    if (Of(other) is { Count: > 0 } mine) target[key] = mine; else target.Remove(key);
            }
            ctx.Replace(t.With(attrs: keptAttrs));
            var second = PathEdits.AddLike(ctx, t, otherAttrs);
            ctx.Replace(second.With(name: $"{t.Name} 2"));
            // What the terrain relates (what voids it) goes to the half it lies in.
            foreach (var rel in ctx.Model.RelationsOf(t.Id).Where(x => x.Relating == t.Id).ToList())
            {
                var moving = rel.Related.Where(id => ctx.Model.Get(id) is { } o && Anchor(o) is { } at && !Polygon2.Contains(lc, at) && Polygon2.Contains(rc, at)).ToList();
                if (moving.Count == 0) continue;
                var staying = rel.Related.Where(id => !moving.Contains(id)).ToList();
                if (staying.Count == 0) ctx.ReplaceRelation(rel with { Relating = second.Id });
                else
                {
                    ctx.ReplaceRelation(rel with { Related = staying });
                    ctx.AddRelation(new Relation(ctx.NewId(), rel.Class, second.Id, moving));
                }
            }
            return new JsonObject { ["ids"] = new JsonArray(t.Id, second.Id) };
        });

        // T1: Merge two or more terrains into one: the first keeps its GlobalId and takes the others' points,
        // breaklines, strokes, existing surfaces and what voids them (platforms, ditches, paths); the others go.
        r.Command("mergeTerrains", "Merge terrains", (ctx, a) =>
        {
            var ids = (a["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().Distinct().ToList() ?? [];
            if (ids.Count < 2) throw new CommandException("Select two terrains at least to merge.");
            var list = ids.Select(id => Terrain(ctx.Model, id)).ToList();
            var primary = list[0];
            var datum = primary.Num("datum");
            var seen = new HashSet<(long, long)>();
            var seenExisting = new HashSet<(long, long)>();
            var pts = new List<Vec3>();
            var existing = new List<Vec3>();
            var breaklines = new JsonArray();
            var sculpts = new JsonArray();
            var cage = new List<Vec2>();
            // The files' triangles, when every one has its own: each one's renumbered onto the merged points.
            var faces = list.All(tr => Terrains.FacesOf(tr).Count > 0) ? new List<(int, int, int)>() : null;
            var at = new Dictionary<(long, long), int>();
            static (long, long) Key(Vec3 p) => ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6));
            foreach (var tr in list)
            {
                // Heights are kept as model heights: a survey height z read from datum d is z - d + datum here.
                var d = tr.Num("datum");
                var mine = new List<int>();
                foreach (var p in Terrains.Points(tr))
                {
                    if (seen.Add(Key(p))) { at[Key(p)] = pts.Count; pts.Add(new Vec3(p.X, p.Y, p.Z - d + datum)); }
                    mine.Add(at[Key(p)]);
                }
                faces?.AddRange(Terrains.FacesOf(tr).Select(f => (mine[f.A], mine[f.B], mine[f.C])));
                // Each one's existing surface: its snapshot, else its survey (what its own cut and fill were measured against).
                foreach (var p in Terrains.ExistingPoints(tr))
                    if (seenExisting.Add(Key(p))) existing.Add(new Vec3(p.X, p.Y, p.Z - d + datum));
                foreach (var bl in Terrains.Breaklines(tr))
                    breaklines.Add((JsonNode)Terrains.PointsJson(bl.Select(p => new Vec3(p.X, p.Y, p.Z - d + datum))));
                // Strokes work in model heights (a flatten's level too), which the merge keeps: they carry over as they are.
                foreach (var sc in Sculpt.Of(tr))
                    sculpts.Add((JsonNode)sc.ToJson());
                cage.AddRange(Cage.Of(tr));
            }
            if (!Tin.Spans(pts)) throw new CommandException("The merged points need three that are not in a line.");
            var attrs = primary.CloneAttrs();
            attrs["points"] = Terrains.PointsJson(pts);
            // The boundary: the union of theirs (one with none counts as the outline its points cover). Two halves of a split
            // come back as the outline they were cut from; when the union is not one outline, the merged terrain covers all its
            // points and the result says so (none of them bounded: none, as before).
            var problems = new JsonArray();
            attrs.Remove(Terrains.BoundaryKey); attrs.Remove(Terrains.BoundaryArcs);
            if (list.Any(tr => ArcOutline.CornersOf(tr, Terrains.BoundaryKey).Count >= 3))
            {
                if (MergedBoundary(list) is { } outline)
                {
                    attrs[Terrains.BoundaryKey] = Terrains.ContourJson(outline.Corners);
                    ArcOutline.Write(attrs, outline.Rises, Terrains.BoundaryArcs);
                }
                else problems.Add((JsonNode)MergedBoundaryProblem);
            }
            // One of them with its outline as its own points (T1): the merged outline's vertices are points too, at the ground's height
            // where none stood; the points on the seam stay.
            attrs.Remove(TerrainOutlines.Flag);
            if (list.Any(TerrainOutlines.Holds) && attrs[Terrains.BoundaryKey] != null)
            {
                attrs[TerrainOutlines.Flag] = true;
                var grounds = list.Select(tr => Terrains.Of(tr)).ToList();
                TerrainOutlines.Follow(attrs, [], null, p => grounds.Select(g => g.ElevationAt(p.X, p.Y)).FirstOrDefault(z => z != null), prune: false);
            }
            if (faces is { Count: > 0 }) attrs["faces"] = Terrains.FacesJson(faces.Distinct()); else attrs.Remove("faces");
            attrs.Remove("existingFaces");
            // The parcel: the one there is; of several, the first terrain's that has one, and the result says so.
            var parcels = list.Where(tr => ArcOutline.CornersOf(tr, "parcel").Count >= 3).ToList();
            attrs.Remove("parcel"); attrs.Remove(Terrains.ParcelArcs);
            if (parcels.Count > 0)
            {
                attrs["parcel"] = parcels[0].Attrs["parcel"]!.DeepClone();
                if (parcels[0].Attrs[Terrains.ParcelArcs] is { } pa) attrs[Terrains.ParcelArcs] = pa.DeepClone();
                if (parcels.Count > 1) problems.Add((JsonNode)$"The terrains had {parcels.Count} parcels and a terrain keeps one: the merged terrain keeps {parcels[0].Name}'s. Draw the parcel again (Parcel) if it is another.");
            }
            if (breaklines.Count > 0) attrs["breaklines"] = breaklines; else attrs.Remove("breaklines");
            if (sculpts.Count > 0) attrs["sculpts"] = sculpts; else attrs.Remove("sculpts");
            // Every one's controls, those still on a point of the merged survey.
            Cage.Write(attrs, pts, Cage.IndicesOf(pts, cage));
            // Every one's spot levels, slope tags and subregions.
            foreach (var key in PlacedKeys)
            {
                var all = new JsonArray([.. list.SelectMany(tr => (tr.Attrs[key] as JsonArray)?.Select(x => x?.DeepClone()) ?? [])]);
                if (all.Count > 0) attrs[key] = all; else attrs.Remove(key);
            }
            // A snapshot on any of them: the merged one's existing surface is every one's, side by side.
            if (list.Any(tr => tr.Attrs["existingPoints"] is JsonArray { Count: >= 3 })) attrs["existingPoints"] = Terrains.PointsJson(existing);
            else attrs.Remove("existingPoints");
            ctx.Replace(primary.With(attrs: attrs));
            foreach (var tr in list.Skip(1))
            {
                foreach (var rel in ctx.Model.RelationsOf(tr.Id).Where(x => x.Relating == tr.Id).ToList())
                    ctx.ReplaceRelation(rel with { Relating = primary.Id });
                ctx.Remove(tr.Id);
            }
            var result = new JsonObject { ["id"] = primary.Id, ["points"] = pts.Count };
            if (problems.Count > 0) result["problems"] = problems;
            return result;
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
                attrs.Remove("existingFaces");
            }
            else
            {
                var datum = t.Num("datum");
                var tin = Terrains.Of(t).Tin;
                attrs["existingPoints"] = Terrains.PointsJson(tin.Points.Select(p => new Vec3(p.X, p.Y, p.Z + datum)));
                // Its triangles too: the existing ground is this surface exactly, not the points triangulated again.
                attrs["existingFaces"] = Terrains.FacesJson(tin.Triangles);
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
            // A control of the terrain's cage moved up or down: the free points follow (Cage.cs); a control moved in plan stays one.
            var wasControl = Cage.Indices(t).Contains(idx);
            Cage.Follow(t, pts, new Dictionary<int, double> { [idx] = z - p.Z }, new HashSet<int> { idx });
            pts[idx] = new Vec3(x, y, z);
            var attrs = t.CloneAttrs();
            attrs["points"] = Terrains.PointsJson(pts);
            if (wasControl && (x != p.X || y != p.Y))
                Cage.Write(attrs, pts, Cage.Indices(t).Where(i => i != idx).Append(idx));
            // A terrain whose outline is its points: the arcs' vertices follow their corners (T1, TerrainOutlines.Settle).
            TerrainOutlines.Settle(attrs, [idx]);
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
            var moves = new Dictionary<int, double>();
            foreach (var i in indices)
            {
                var p = pts[i];
                pts[i] = p with { Z = exact is { } z ? z + datum : p.Z + dz };
                moves[i] = pts[i].Z - p.Z;
            }
            // Controls among them: the free points not set here follow (Cage.cs).
            Cage.Follow(t, pts, moves, indices.ToHashSet());
            var attrs = t.CloneAttrs();
            attrs["points"] = Terrains.PointsJson(pts);
            TerrainOutlines.Settle(attrs, indices);
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
            // Without its height it takes the ground's there; off the ground there is none to take.
            var z = Json.Number(a["z"]) is { } exact ? exact + datum
                : (Terrains.Of(t).ElevationAt(xy.X, xy.Y) ?? throw new CommandException("The point is outside the terrain: give its elevation.")) + datum;
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
            // The file's triangles: those on the point go, the others renumbered.
            if (Terrains.FacesOf(t) is { Count: > 0 } faces)
            {
                int N(int i) => i > idx ? i - 1 : i;
                var kept = faces.Where(f => f.A != idx && f.B != idx && f.C != idx).Select(f => (N(f.A), N(f.B), N(f.C))).ToList();
                if (kept.Count > 0) attrs["faces"] = Terrains.FacesJson(kept); else attrs.Remove("faces");
            }
            Cage.Prune(attrs, pts);
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["points"] = pts.Count };
        });

        // Issue #113: the terrain's control cage, the survey points chosen as controls (Cage.cs). indices with mode set (the default),
        // add, remove or toggle; spacing: one point per cell of a plan grid; boundary: the points on its outline; clear: none.
        // reach: how far a control's move carries (0: derived). Moves no point; one undo step.
        r.Command("setTerrainControls", "Terrain control points", (ctx, a) =>
        {
            var t = Terrain(ctx.Model, (string?)a["id"]);
            var pts = Terrains.Points(t);
            var now = Cage.Indices(t).ToHashSet();
            var given = (a["indices"] as JsonArray)?.Select(x => Json.Number(x) is { } n ? (int)n : -1).Where(i => i >= 0 && i < pts.Count).Distinct().ToList();
            List<int> next;
            if (a["clear"] is JsonValue cv && cv.TryGetValue<bool>(out var clear) && clear) next = [];
            else if (Json.Number(a["spacing"]) is { } spacing)
            {
                if (spacing < 0.5) throw new CommandException("Controls on a grid need a spacing of half a metre at least.");
                next = Cage.Grid(pts, spacing);
            }
            else if (a["boundary"] is JsonValue bv && bv.TryGetValue<bool>(out var rim) && rim) next = Cage.Rim(t, pts);
            else if (given != null)
            {
                var mode = (string?)a["mode"] ?? "set";
                next = mode switch
                {
                    "set" => given,
                    "add" => [.. now.Union(given)],
                    "remove" => [.. now.Except(given)],
                    "toggle" => [.. now.Except(given).Union(given.Where(i => !now.Contains(i)))],
                    _ => throw new CommandException("Controls are set, added, removed or toggled."),
                };
            }
            else next = [.. now];
            var attrs = t.CloneAttrs();
            Cage.Write(attrs, pts, next);
            if (Json.Number(a["reach"]) is { } reach) { if (reach > 0) attrs[Cage.ReachKey] = reach; else attrs.Remove(Cage.ReachKey); }
            if (attrs[Cage.Key] == null) attrs.Remove(Cage.ReachKey);
            ctx.Replace(t.With(attrs: attrs));
            return new JsonObject { ["count"] = PointLists.Xy(attrs[Cage.Key]).Count };
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
            var terrains = Terrains.All(q.Model).Where(t => ids == null || ids.Contains(t.Id)).ToList();
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
            var attrs = RetainingWalls.Attrs(path, spec, concrete.Id);
            // Its batters, toe, heel and key, when given (docs/plan-wing-walls.md).
            foreach (var k in BatteredWalls.WallKeys) if (Json.Number(a[k]) is { } v) attrs[k] = Math.Max(0, v);
            var e = new Entity(ctx.NewId(), RetainingWalls.Class, ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Retaining wall {n}", attrs, "RETAININGWALL");
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
            // Its batters (n:1, 0 upright), the thickness at its base, its toe, heel and key: null clears one.
            foreach (var k in BatteredWalls.WallKeys)
                if (a.ContainsKey(k)) { if (Json.Number(a[k]) is { } v) attrs[k] = Math.Max(0, v); else attrs.Remove(k); }
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // One segment of a retaining wall (docs/plan-wing-walls.md): its batters, crown thickness, crown at its start and its end,
        // footing level, its warp (the face from one batter to another), null clearing one; its length and its turn from the segment
        // before (degrees, + to the left; the first segment's direction from east), the segments after it keeping theirs.
        r.Command("setRetainingWallSegment", "Change retaining wall segment", (ctx, a) =>
        {
            var e = Spatial.Require(ctx.Model, (string?)a["id"], RetainingWalls.Class);
            var path = Chain.Clean(RetainingWalls.Path(e), null).Path.ToList();
            var i = (int)(Json.Number(a["index"]) ?? -1);
            if (i < 0 || i >= path.Count - 1) throw new CommandException("The wall has no such segment.");
            var attrs = e.CloneAttrs();
            var segs = attrs["segments"] as JsonArray ?? [];
            while (segs.Count < path.Count - 1) segs.Add((JsonNode)new JsonObject());
            if (segs[i] is not JsonObject o) segs[i] = o = new JsonObject();
            foreach (var k in BatteredWalls.SegmentKeys)
                if (a.ContainsKey(k))
                {
                    if (Json.Number(a[k]) is { } v) o[k] = k is "topStart" or "topEnd" or "footingLevel" ? v : Math.Max(0, v);
                    else o.Remove(k);
                }
            if (a.ContainsKey("warp")) { if (a["warp"] is JsonValue w && w.TryGetValue<bool>(out var on) && on) o["warp"] = true; else o.Remove("warp"); }
            var length = Json.Number(a["length"]);
            var turn = Json.Number(a["angle"]);
            if (length != null || turn != null)
            {
                if (length is < 0.05) throw new CommandException("A segment is 5 cm long at least.");
                if (turn is { } tt && i > 0 && Math.Abs(tt) >= 179) throw new CommandException("A segment cannot turn back on the one before it.");
                attrs["path"] = Terrains.ContourJson(RetainingWallPaths.WithSegment(path, i, length, turn));
            }
            // Trailing empty overrides go; none at all, the key goes.
            while (segs.Count > 0 && segs[^1] is JsonObject { Count: 0 } or null) segs.RemoveAt(segs.Count - 1);
            if (segs.Count == 0) attrs.Remove("segments"); else attrs["segments"] = segs;
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
                var contour = Terrains.CheckOutline(Args.Points(a), "A planting zone");
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
            if (a["points"] != null) attrs["contour"] = Terrains.ContourJson(Terrains.CheckOutline(Args.Points(a), "A planting zone"));
            if (a.ContainsKey("x") && a.ContainsKey("y")) { var at = Args.Point(a, "x", "y"); attrs["at"] = new JsonArray(at.X, at.Y); }
            foreach (var k in new[] { "density", "seed" }) if (Json.Number(a[k]) is { } v) attrs[k] = k == "density" ? Plantings.Density(v) : v;
            if (a["species"] is JsonArray arr) attrs["species"] = Plantings.SpeciesJson(arr.Select(Plantings.Parse).OfType<Species>());
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });
    }

    static string StoreyId(IModelView m, JsonObject a) =>
        (string?)a["storeyId"] ?? Spatial.Storeys(m).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");

    /// <summary>What a merge says when the terrains' boundaries do not make one outline.</summary>
    public const string MergedBoundaryProblem = "The terrains' boundaries do not make one outline (they lie apart or leave a gap between them): the merged terrain covers all its points. Draw its boundary again (Terrain boundary) to clip it.";

    /// <summary>
    /// The boundary of terrains merged: the union of their drawn boundaries (arcs kept), a terrain with none taking the outline
    /// its triangles cover; null when that is not one outline with no gap.
    /// </summary>
    internal static OutlineUnion.Loop? MergedBoundary(IReadOnlyList<Entity> terrains)
    {
        var loops = new List<OutlineUnion.Loop>();
        foreach (var tr in terrains)
        {
            var corners = ArcOutline.CornersOf(tr, Terrains.BoundaryKey);
            if (corners.Count >= 3) loops.Add(new OutlineUnion.Loop(corners, ArcOutline.RisesOf(tr, Terrains.BoundaryKey, Terrains.BoundaryArcs)));
            else if (Terrains.BoundaryLoop(Terrains.Of(tr)) is { Count: >= 3 } loop) loops.Add(new OutlineUnion.Loop(loop, null));
        }
        if (OutlineUnion.Of(loops)?.Single is not { } one) return null;
        try
        {
            var (c, r) = ArcOutline.Check(one.Corners, one.Rises, "A terrain boundary", simple: true);
            return new OutlineUnion.Loop(c, r);
        }
        catch (CommandException) { return null; }
    }

    /// <summary>The terrain's lists of things placed on it in plan, which a split shares out and a merge gathers.</summary>
    static readonly string[] PlacedKeys = ["spots", "slopes", "subregions"];

    /// <summary>Where an item of those lists lies: a point [x, y], a grade line's middle [x1, y1, x2, y2], an outline's centroid.</summary>
    static Vec2? PlacedAt(JsonNode? item)
    {
        if (item is JsonObject o) return PointLists.Xy(o["contour"]) is { Count: >= 3 } c ? Polygon2.Centroid(c) : null;
        if (item is not JsonArray a || a.Count < 2) return null;
        var v = a.Select(x => Json.Number(x) ?? double.NaN).ToArray();
        if (!v.All(double.IsFinite)) return null;
        return v.Length >= 4 ? new Vec2((v[0] + v[2]) / 2, (v[1] + v[3]) / 2) : new Vec2(v[0], v[1]);
    }

    /// <summary>Where an element on a terrain lies in plan, to tell which half of a split it goes to: its outline's centroid, else its path's midpoint.</summary>
    static Vec2? Anchor(Entity e)
    {
        if (Terrains.Contour(e) is { Count: >= 3 } c) return Polygon2.Centroid(c);
        var path = Terrains.Contour(e, "path");
        if (path.Count < 2) return path.Count == 1 ? path[0] : null;
        var half = Polyline2.Length(path) / 2;
        for (var i = 0; i + 1 < path.Count; i++)
        {
            var len = (path[i + 1] - path[i]).Length;
            if (half <= len && len > 0) return path[i] + (path[i + 1] - path[i]) * (half / len);
            half -= len;
        }
        return path[^1];
    }

    static Entity Terrain(IModelView m, string? id) => Terrains.Require(m, id);

    /// <summary>The terrain named, else the one under the point, else the project's first.</summary>
    static Entity TerrainAt(IModelView m, JsonObject a, Vec2 at)
    {
        if ((string?)a["terrainId"] is { } id) return Terrain(m, id);
        return Terrains.Under(m, at) ?? Terrains.All(m).FirstOrDefault() ?? throw new CommandException("The project has no terrain.");
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
        var terrain = Terrains.Under(ctx.Model, path[0]);
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
