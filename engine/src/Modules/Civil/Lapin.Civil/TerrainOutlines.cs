using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A terrain whose outline is its own points (docs/plan-site-grading.md, T1; forum topic 230): made straight from a drawn or
/// picked outline (a polygon, a rectangle, a circle, edges that are arcs; a room, a parcel, a platform), every vertex of the
/// outline as built (each arc in chords by the one rule) is a survey point of its own, with a grip and a height that can be
/// typed, and its boundary is that outline, so the ground ends exactly there. A terrain clipped by a boundary drawn before
/// keeps its outline's heights as the ground gives them until <c>civil.outlineToPoints</c> makes them its points, on demand.
/// Such a terrain carries <see cref="Flag"/>; while it does, a boundary drawn again, a split and a merge keep the outline's
/// vertices as points (a corner moved keeps its height, a new vertex takes the ground's there, a vertex gone takes its point
/// with it). A terrain without the flag works as before.
/// </summary>
public static class TerrainOutlines
{
    /// <summary>The attribute saying the outline's vertices are the terrain's own points.</summary>
    public const string Flag = "outlinePoints";

    /// <summary>Most interior points a terrain made on an outline gets: a finer spacing asked is taken coarser.</summary>
    public const int MaxInterior = 20000;

    public static bool Holds(Entity e) => Holds(e.Attrs);

    static bool Holds(JsonObject attrs) => attrs[Flag] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    static (long, long) Key(Vec2 p) => ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6));

    /// <summary>An outline's vertices as built, each with the edge it starts or lies on and whether it is one of the corners.</summary>
    public static List<(Vec2 P, int Edge, bool Corner)> Vertices(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises)
    {
        var edgeOf = new List<int>();
        var pts = ArcOutline.Points(corners, rises, edgeOf);
        return [.. pts.Select((p, k) => (p, edgeOf[k], (p - corners[edgeOf[k]]).Length <= 1e-9))];
    }

    /// <summary>
    /// The heights of an outline's vertices from its corners' (<paramref name="cornerZ"/>): a corner its own, a vertex along an
    /// edge (an arc's chords) the height between its edge's two corners, by how far along the edge's chords it lies.
    /// </summary>
    public static List<double> AlongEdges(List<(Vec2 P, int Edge, bool Corner)> verts, IReadOnlyList<double> cornerZ)
    {
        var n = cornerZ.Count;
        var o = new double[verts.Count];
        for (var k = 0; k < verts.Count; k++)
        {
            var e = verts[k].Edge;
            if (verts[k].Corner) { o[k] = cornerZ[e]; continue; }
            // The run of vertices on this edge: from its corner (before k) to the next corner (after the run).
            var start = k; while (start > 0 && verts[start].Edge == e && !verts[start].Corner) start--;
            var end = k; while (end + 1 < verts.Count && verts[end + 1].Edge == e) end++;
            var next = verts[(end + 1) % verts.Count].P;
            double total = 0, upTo = 0;
            for (var i = start; i <= end; i++)
            {
                var b = i < end ? verts[i + 1].P : next;
                var d = (b - verts[i].P).Length;
                if (i < k) upTo += d;
                total += d;
            }
            var f = total > 1e-12 ? upTo / total : 0;
            o[k] = Math.Round(cornerZ[e] + (cornerZ[(e + 1) % n] - cornerZ[e]) * f, 9);
        }
        return [.. o];
    }

    public static void Register(IModuleRegistry r)
    {
        // A terrain straight from an outline (T1): {points [[x, y] | [x, y, z]], arcs?} or {fromId: a room, a parcel or area, a
        // terrain's parcel, a platform}; heights: a corner's own z, else the ground under it (ground: true, the terrain there),
        // else elevation (model height, 0); spacing: the interior points' grid (0: none; default as a flat terrain's).
        r.Command("addTerrainOnOutline", "Terrain on outline", (ctx, a) =>
        {
            var m = ctx.Model;
            var (corners, rises, cornerZ, storeyHint, what) = OutlineOf(m, a);
            var storeyId = (string?)a["storeyId"] ?? storeyHint ?? Spatial.Storeys(m).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var ground = a["ground"] is JsonValue gv && gv.TryGetValue<bool>(out var g) && g;
            var elevation = Json.Number(a["elevation"]) ?? 0;
            var verts = Vertices(corners, rises);
            // The ground under a point: the first terrain there (model heights), as surveyed or as worked.
            double? Under(Vec2 p) => Terrains.Under(m, p) is { } t ? Terrains.Of(t).ElevationAt(p.X, p.Y) : null;
            var cz = corners.Select((c, i) => cornerZ[i] ?? (ground ? Under(c) : null) ?? elevation).ToList();
            var zs = AlongEdges(verts, cz);
            // On the ground, a vertex along an edge without heights of its own reads the ground too.
            if (ground)
                for (var k = 0; k < verts.Count; k++)
                {
                    var e = verts[k].Edge;
                    if (!verts[k].Corner && cornerZ[e] == null && cornerZ[(e + 1) % corners.Count] == null && Under(verts[k].P) is { } z) zs[k] = z;
                }
            var outline = verts.Select((v, k) => new Vec3(v.P.X, v.P.Y, zs[k])).ToList();
            var ring = verts.Select(v => v.P).ToList();
            var interior = Interior(ring, outline, Json.Number(a["spacing"]), ground ? Under : null);
            var datum = Json.Number(a["datum"]) ?? 0;
            var pts = outline.Concat(interior).Select(p => p with { Z = p.Z + datum }).ToList();
            if (!Tin.Spans(pts)) throw new CommandException("The terrain needs at least three points that are not in a line.");
            var attrs = Terrains.NewAttrs(ctx, pts, datum, Json.Number(a["interval"]), Json.Number(a["majorEvery"]), Json.Number(a["depth"]));
            attrs[Terrains.BoundaryKey] = Terrains.ContourJson(corners);
            ArcOutline.Write(attrs, rises, Terrains.BoundaryArcs);
            attrs[Flag] = true;
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : "Terrain";
            var terrain = new Entity(ctx.NewId(), IfcClass.GeographicElement, name, attrs, Terrains.Predefined);
            ctx.Add(terrain);
            Spatial.Contain(ctx, storeyId, terrain.Id);
            return new JsonObject { ["id"] = terrain.Id, ["points"] = pts.Count, ["outline"] = outline.Count, ["from"] = what };
        });

        // The outline of a terrain clipped by a boundary made its own points, on demand: each vertex of the boundary as built that is
        // no point yet becomes one at the ground's height there, so it has a grip and a height to type. The ground stays as it was
        // (a terrain worked with the brush may move by a little at the outline: the result says how much). One undo step.
        r.Command("outlineToPoints", "Outline as points", (ctx, a) =>
        {
            var t = Terrains.Require(ctx.Model, (string?)a["id"]);
            var corners = ArcOutline.CornersOf(t, Terrains.BoundaryKey);
            if (corners.Count < 3) throw new CommandException("The terrain has no boundary: its outline runs through its points already.");
            if (Terrains.FacesOf(t).Count > 0) throw new CommandException("A terrain kept as its file's triangles (LandXML) keeps its outline as the file has it.");
            var before = Terrains.Of(t);
            var survey = SurveyGround(t);
            var pts = Terrains.Points(t);
            var datum = t.Num("datum");
            var at = pts.Select(p => Key(p.Xy)).ToHashSet();
            var added = 0;
            foreach (var (p, _, _) in Vertices(corners, ArcOutline.RisesOf(t, Terrains.BoundaryKey, Terrains.BoundaryArcs)))
            {
                if (!at.Add(Key(p))) continue;
                var z = survey.ElevationAt(p.X, p.Y) ?? Nearest(pts, p) - datum;
                pts.Add(new Vec3(p.X, p.Y, z + datum));
                added++;
            }
            var attrs = t.CloneAttrs();
            attrs["points"] = Terrains.PointsJson(pts);
            attrs[Flag] = true;
            var next = t.With(attrs: attrs);
            ctx.Replace(next);
            // How far the ground moved at the outline (only a terrain worked with the brush moves: the strokes now work on these points too).
            var after = Terrains.Of(next);
            var moved = Terrains.BoundaryOutline(next).Max(p => before.ElevationAt(p.X, p.Y) is { } z0 && after.ElevationAt(p.X, p.Y) is { } z1 ? Math.Abs(z1 - z0) : 0);
            var result = new JsonObject { ["id"] = t.Id, ["added"] = added, ["points"] = pts.Count };
            if (moved > 5e-4) result["moved"] = Math.Round(moved, 4);
            return result;
        });
    }

    /// <summary>The nearest point's height (as stored) to a place in plan.</summary>
    static double Nearest(IReadOnlyList<Vec3> pts, Vec2 p) => pts.MinBy(q => (q.Xy - p).Length)!.Z;

    /// <summary>The ground of a terrain's survey and breaklines alone (no brush, no snapshot), clipped to its boundary, in model heights.</summary>
    static Terrain SurveyGround(Entity t)
    {
        var attrs = t.CloneAttrs();
        attrs.Remove("sculpts"); attrs.Remove("existingPoints"); attrs.Remove("existingFaces");
        return Terrains.Of(t.With(attrs: attrs));
    }

    /// <summary>
    /// Points inside an outline on a grid of <paramref name="spacing"/> (default as a flat terrain's: an eighth of the
    /// shorter side, 2 m at least; 0: none), none nearer the outline than a third of the step; their heights from the outline's
    /// own surface (its vertices triangulated within it), or the ground under them when <paramref name="ground"/> is given.
    /// </summary>
    static List<Vec3> Interior(List<Vec2> ring, List<Vec3> outline, double? spacing, Func<Vec2, double?>? ground)
    {
        double x0 = ring.Min(p => p.X), x1 = ring.Max(p => p.X), y0 = ring.Min(p => p.Y), y1 = ring.Max(p => p.Y);
        var step = spacing ?? Math.Max(2, Math.Min(x1 - x0, y1 - y0) / 8);
        if (!(step > 0)) return [];
        step = Math.Max(step, 0.1);
        while ((x1 - x0) / step * ((y1 - y0) / step) > MaxInterior) step *= 1.5;
        var edges = Enumerable.Range(0, outline.Count).Select(i => (i, (i + 1) % outline.Count)).ToList();
        var surface = new Terrain(Tin.Delaunay(outline, edges, ring));
        var o = new List<Vec3>();
        for (var x = x0 + step; x < x1 - 1e-9; x += step)
            for (var y = y0 + step; y < y1 - 1e-9; y += step)
            {
                var p = new Vec2(x, y);
                if (!Polygon2.Contains(ring, p)) continue;
                var d = Enumerable.Range(0, ring.Count).Min(i => Polyline2.SegmentDistance(p, ring[i], ring[(i + 1) % ring.Count]));
                if (d < step / 3) continue;
                // To the nanometre: a level outline's inside is level exactly, or a contour at its height would wander through it.
                if ((ground?.Invoke(p) ?? surface.ElevationAt(x, y)) is { } z) o.Add(new Vec3(x, y, Math.Round(z, 9)));
            }
        return o;
    }

    /// <summary>
    /// The outline a terrain is made on: the points given (and their arcs), or the element named by <c>fromId</c>: a room's outline
    /// (on its storey), a parcel's or an area's from a GIS file (with the heights it brought), a terrain's parcel, a platform's
    /// outline (at its level). Each corner's height when it has one; counter-clockwise, crossing itself nowhere.
    /// </summary>
    static (List<Vec2> Corners, double[]? Rises, List<double?> Z, string? Storey, string What) OutlineOf(IModelView m, JsonObject a)
    {
        List<Vec2> pts;
        double[]? rises = null;
        var zs = new List<double?>();
        string? storey = null;
        string what = "outline";
        if ((string?)a["fromId"] is { } id)
        {
            var e = m.Get(id) ?? throw new CommandException("There is no such element.");
            storey = Spatial.StoreyOf(m, id)?.Id;
            if (e.Class == IfcClass.Space)
            {
                if (e.Attrs["ellipses"] is JsonArray { Count: > 0 }) throw new CommandException("A room with elliptical edges: draw the terrain's outline instead.");
                pts = ArcOutline.CornersOf(e); rises = ArcOutline.RisesOf(e); what = "room";
            }
            else if (e.Class == SiteFeatures.Class && SiteFeatures.Kind(e) is "parcel" or "area")
            {
                var p3 = SiteFeatures.Points(e);
                pts = [.. p3.Select(p => p.Xy)];
                // The heights a file brought: a feature stored in plan only has none.
                if (e.Attrs["pts"] is JsonArray raw && raw.All(x => x is JsonArray { Count: >= 3 })) zs = [.. p3.Select(p => (double?)p.Z)];
                what = SiteFeatures.Kind(e);
            }
            else if (Terrains.IsTerrain(e) && ArcOutline.CornersOf(e, "parcel") is { Count: >= 3 } parcel)
            {
                pts = parcel; rises = ArcOutline.RisesOf(e, "parcel", Terrains.ParcelArcs); what = "parcel";
                storey = Spatial.StoreyOf(m, id)?.Id;
            }
            else if (e.Class == IfcClass.EarthworksCut && !Ditches.Is(e) && ArcOutline.CornersOf(e) is { Count: >= 3 } pad)
            {
                pts = pad; rises = ArcOutline.RisesOf(e); what = "platform";
                var z = e.NumOrNull("elevation");
                zs = [.. pad.Select(_ => z)];
                storey = Terrains.TerrainOf(m, id) is { } tt ? Spatial.StoreyOf(m, tt.Id)?.Id : storey;
            }
            else throw new CommandException("A terrain is made on a room, a parcel, an area, a terrain's parcel or a platform: that element has no outline to take.");
        }
        else
        {
            pts = [.. Args.Points(a)];
            rises = ArcOutline.RisesArg(a["arcs"], pts.Count);
            if (a["points"] is JsonArray raw) zs = [.. raw.Select(x => x is JsonArray { Count: >= 3 } p ? Json.Number(p[2]) : null)];
        }
        while (zs.Count < pts.Count) zs.Add(null);
        var given = pts.Select((p, i) => (p, z: zs[i])).ToList();
        var (corners, r) = rises is null ? (Terrains.CheckOutline(pts, "A terrain").ToList(), null) : ArcOutline.Check(pts, rises, "A terrain", simple: true);
        // Each corner kept its height wherever the check put it (made counter-clockwise, repeated corners dropped).
        var cz = corners.Select(c => given.FirstOrDefault(x => (x.p - c).Length <= 1e-6).z).ToList();
        if (cz.Any(z => z is { } v && !double.IsFinite(v))) throw new CommandException("A corner's height must be a number.");
        return (corners, r, cz, storey, what);
    }

    /// <summary>
    /// Keeps a terrain's outline its own points after its boundary changed (a boundary drawn again, a split, a merge), when it
    /// <see cref="Holds"/> them: every vertex of the new outline that is no point yet becomes one, a corner taking the height of the
    /// corner it replaces (<paramref name="byOrder"/>: as many corners, in order, one or two moved: a corner or an edge dragged keeps
    /// its height) or else the ground's there
    /// (<paramref name="ground"/>, model heights), a vertex along an edge the height between its corners; with
    /// <paramref name="prune"/>, the points that stood on the old outline's vertices and are on the new one no more go.
    /// <paramref name="attrs"/> holds the new boundary and the points (survey heights); <paramref name="was"/>, the outline before.
    /// </summary>
    public static void Follow(JsonObject attrs, IReadOnlyList<Vec2> wasCorners, double[]? wasRises, Func<Vec2, double?> ground, bool prune, bool byOrder = false)
    {
        if (!Holds(attrs)) return;
        var corners = PointLists.Xy(attrs[Terrains.BoundaryKey]);
        if (corners.Count < 3) { attrs.Remove(Flag); return; }
        var rises = ArcOutline.Rises(attrs[Terrains.BoundaryArcs], corners.Count);
        var datum = Json.Number(attrs["datum"]) ?? 0;
        var pts = PointLists.Xyz(attrs["points"]);
        var verts = Vertices(corners, rises);
        var fresh = verts.Select(v => Key(v.P)).ToHashSet();
        if (prune && wasCorners.Count >= 3)
        {
            var gone = Vertices(wasCorners, wasRises).Select(v => Key(v.P)).Where(k => !fresh.Contains(k)).ToHashSet();
            if (gone.Count > 0) pts = [.. pts.Where(p => !gone.Contains(Key(p.Xy)))];
        }
        var at = new Dictionary<(long, long), double>();
        foreach (var p in pts) at.TryAdd(Key(p.Xy), p.Z - datum);
        // The corners' heights: their point's, else the corner replaced's (same count, same place in the order), else the ground's.
        // Only for a drag: as many corners, in order, one or two of them moved (a corner, an edge); an outline drawn anew takes the ground's.
        var moved = wasCorners.Count == corners.Count ? Enumerable.Range(0, corners.Count).Count(i => (wasCorners[i] - corners[i]).Length > 1e-6) : int.MaxValue;
        var wasZ = byOrder && moved <= 2 ? wasCorners.Select(c => at.TryGetValue(Key(c), out var z) ? z : ground(c)).ToList() : null;
        var cz = corners.Select((c, i) => at.TryGetValue(Key(c), out var z) ? z : wasZ?[i] ?? ground(c) ?? (pts.Count > 0 ? Nearest(pts, c) - datum : 0)).ToList();
        var along = AlongEdges(verts, cz);
        var added = false;
        for (var k = 0; k < verts.Count; k++)
        {
            if (at.ContainsKey(Key(verts[k].P))) continue;
            pts.Add(new Vec3(verts[k].P.X, verts[k].P.Y, along[k] + datum));
            at[Key(verts[k].P)] = along[k];
            added = true;
        }
        if (added || prune) attrs["points"] = Terrains.PointsJson(pts);
        Cage.Prune(attrs, pts);
    }

    /// <summary><see cref="Follow"/> for a terrain whose boundary was just set in <paramref name="attrs"/>: the heights from its own survey ground.</summary>
    public static void Follow(JsonObject attrs, Entity was, bool prune, bool byOrder = false)
    {
        if (!Holds(attrs)) return;
        if (attrs[Terrains.BoundaryKey] == null) { attrs.Remove(Flag); return; }
        var survey = SurveyGround(was);
        // Off the old boundary the survey ground has nothing: the ground its points make without it.
        var unclipped = was.CloneAttrs();
        unclipped.Remove("sculpts"); unclipped.Remove("existingPoints"); unclipped.Remove("existingFaces");
        unclipped.Remove(Terrains.BoundaryKey); unclipped.Remove(Terrains.BoundaryArcs);
        var open = Terrains.Of(was.With(attrs: unclipped));
        Follow(attrs, ArcOutline.CornersOf(was, Terrains.BoundaryKey), ArcOutline.RisesOf(was, Terrains.BoundaryKey, Terrains.BoundaryArcs),
            p => survey.ElevationAt(p.X, p.Y) ?? open.ElevationAt(p.X, p.Y), prune, byOrder);
    }
}
