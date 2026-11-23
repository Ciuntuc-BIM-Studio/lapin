using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A hardscape (docs/plan-site-grading.md, T4; forum 230 §3): a paved area over an outline (its edges straight or arcs), built
/// of its type's layers from the top down (paving, bedding, sub-base: an IfcMaterialLayerSet on a <see cref="TypeClass"/>, laid
/// by the element's IfcMaterialLayerSetUsage, as a floor's), its top level, a plane at a grade, or by its corners' heights
/// (<see cref="TopSurface"/>, the slope engine T7 shares). It stands alone (a sidewalk, a plaza, a platform without a survey)
/// or is cut into a terrain (<c>IfcRelVoidsElement</c>, as a path): then its top edges are the terrain's breaklines (T2), the
/// survey points under it leave the triangulation, so the ground meets its top edges exactly, and the terrain is cut by its body.
/// Its top edges are lines in space anyone reads (<see cref="Edge"/>, <c>civil.hardscapeEdges</c>): what T5 grades between.
/// A kerb along its edges is an element of its own (<see cref="Kerbs"/>). IFC: IfcPavement in 4.3, a slab "Pavement" in IFC4 (Î4).
/// </summary>
public static class Hardscapes
{
    public const string Class = "LapinHardscape";
    public const string TypeClass = "LapinHardscapeType";
    public const string Color = "#b9b2a6";
    /// <summary>How near a survey point may lie to a hardscape's edge before it leaves the triangulation (m).</summary>
    public const double Tolerance = 0.05;
    /// <summary>How far above its top a hardscape's cut into the ground reaches (m): the ground there is its top, so nothing is lost.</summary>
    public const double CutAbove = 0.005;

    public static bool Is(Entity e) => e.Class == Class;

    /// <summary>A ready-made build-up, top down (material, thickness, colour); shipped as indicative.</summary>
    public sealed record Preset(string Key, string Name, string Pavement, (string Material, double Thickness, string Color)[] Layers);

    public static readonly Preset[] Presets =
    [
        new("pavers", "Concrete pavers", "RIGID", [("Concrete pavers", 0.06, "#b9b2a6"), ("Sand bedding", 0.04, "#d8c89c"), ("Crushed stone", 0.20, "#9d978c")]),
        new("asphalt", "Asphalt", "FLEXIBLE", [("Asphalt wearing course", 0.04, "#4d4d4f"), ("Asphalt binder course", 0.06, "#5d5a57"), ("Crushed stone", 0.25, "#9d978c")]),
        new("gravel", "Gravel", "FLEXIBLE", [("Gravel", 0.10, "#c4baa4"), ("Crushed stone", 0.15, "#9d978c")]),
        new("grassPavers", "Grass pavers", "RIGID", [("Grass pavers", 0.08, "#86a05f"), ("Sand bedding", 0.04, "#d8c89c"), ("Crushed stone", 0.20, "#9d978c")]),
        new("stoneSlabs", "Stone slabs", "RIGID", [("Stone slabs", 0.05, "#aaa49a"), ("Mortar bed", 0.04, "#cdc8bd"), ("Concrete base", 0.12, "#b9b9b1"), ("Crushed stone", 0.15, "#9d978c")]),
    ];

    /// <summary>
    /// The graphics the ready-made build-ups' materials are made with where the material standards' names give them none or a
    /// wrong one (a material's own graphics, edited in Manage → Materials as any other's): concrete pavers in a 200 × 100 running
    /// bond, asphalt a sparse stipple, grass pavers their 600 × 400 blocks with openings, seen from above (a hardscape's plan and
    /// an elevation: <c>elevation</c>); asphalt's cut that stipple too, and crushed stone cut as gravel (its name says stone,
    /// masonry's 45° lines). Gravel, stone slabs, sand, mortar and concrete keep what the standard gives their names. A material
    /// gets them only when the project does not have it yet: one it has keeps what it has.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, JsonObject> PresetLooks = new Dictionary<string, JsonObject>
    {
        ["Concrete pavers"] = new() { ["elevation"] = new JsonObject { ["hatch"] = "PAVER_200X100" } },
        ["Asphalt wearing course"] = new() { ["elevation"] = new JsonObject { ["hatch"] = "ASPHALT" }, ["section"] = new JsonObject { ["hatch"] = "ASPHALT" } },
        ["Asphalt binder course"] = new() { ["elevation"] = new JsonObject { ["hatch"] = "ASPHALT" }, ["section"] = new JsonObject { ["hatch"] = "ASPHALT" } },
        ["Grass pavers"] = new() { ["elevation"] = new JsonObject { ["hatch"] = "GRASS_PAVER_600X400" } },
        ["Crushed stone"] = new() { ["elevation"] = new JsonObject { ["hatch"] = "GRAVEL" }, ["section"] = new JsonObject { ["hatch"] = "GRAVEL" } },
    };

    /// <summary>The shipped materials among <paramref name="layers"/> the project does not have yet, made with their looks (<see cref="PresetLooks"/>).</summary>
    static void EnsureLooks(ICommandContext ctx, IEnumerable<LayerSpec> layers)
    {
        foreach (var l in layers)
        {
            if (!PresetLooks.TryGetValue(l.Material, out var look) || Materials.Find(ctx.Model, l.Material) != null) continue;
            var attrs = new JsonObject { ["color"] = l.Color ?? Presets.SelectMany(p => p.Layers).FirstOrDefault(x => x.Material == l.Material).Color ?? Materials.DefaultColor(l.Material) };
            foreach (var (k, v) in look) attrs[k] = v!.DeepClone();
            ctx.Add(new Entity(ctx.NewId(), IfcClass.Material, l.Material, attrs));
        }
    }

    public static JsonArray PresetsJson() => new([.. Presets.Select(p => (JsonNode)new JsonObject
    {
        ["key"] = p.Key, ["name"] = p.Name, ["pavement"] = p.Pavement, ["indicative"] = true,
        ["layers"] = new JsonArray([.. p.Layers.Select(l => (JsonNode)new JsonObject { ["material"] = l.Material, ["thickness"] = l.Thickness })]),
    })]);

    // ─── What it is ─────────────────────────────────────────────────────

    public static List<Vec2> Corners(Entity e) => ArcOutline.CornersOf(e);
    public static double[]? Rises(Entity e) => ArcOutline.RisesOf(e);

    static readonly ConditionalWeakTable<Entity, TopSurface.Solved> TopCache = new();

    /// <summary>Its top over its outline as laid (model heights).</summary>
    public static TopSurface.Solved Top(Entity e) => TopCache.GetValue(e, x =>
    {
        var c = Corners(x);
        return TopSurface.Solve(TopSurface.Read(x.Attrs, c.Count), c, Rises(x));
    });

    /// <summary>Its top edges in space, closed (the first point not repeated): the line the terrain meets it on, and what T5 grades from.</summary>
    public static List<Vec3> Edge(Entity e) => Top(e).Ring;

    /// <summary>The longest stretch of a top edge the terrain is given without a point on it (metres).</summary>
    public const double BreaklineStep = 1.0;

    /// <summary>
    /// Its top edges as the terrain is given them (<see cref="Edge"/>, closed, the first point not repeated), each straight stretch
    /// longer than <see cref="BreaklineStep"/> split evenly, the points on it at its heights: the ground then fans from many points
    /// along an edge, in triangles that lie in one plane with it, instead of from its two corners in long slivers whose folds
    /// drew as stray lines in 3D. The same edge, so the ground meets it exactly as before.
    /// </summary>
    public static List<Vec3> Breakline(Entity e)
    {
        var ring = Edge(e);
        var o = new List<Vec3>(ring.Count * 2);
        for (var i = 0; i < ring.Count; i++)
        {
            var (a, b) = (ring[i], ring[(i + 1) % ring.Count]);
            o.Add(a);
            var n = (int)Math.Ceiling(new Vec2(b.X - a.X, b.Y - a.Y).Length / BreaklineStep);
            for (var k = 1; k < n; k++) o.Add(a + (b - a) * ((double)k / n));
        }
        return o;
    }

    /// <summary>The terrain it is cut into, or none (standalone).</summary>
    public static Entity? TerrainOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelVoidsElement) is { } t && Terrains.IsTerrain(t) ? t : null;

    /// <summary>The hardscapes cut into a terrain.</summary>
    public static IEnumerable<Entity> Of(IModelView m, string terrainId) => m.RelatedOf(terrainId, IfcClass.RelVoidsElement).Where(Is);

    public static Entity? TypeOf(IModelView m, Entity e) => Types.Of(m, e, TypeClass);

    /// <summary>Its layers from the top down (its type's).</summary>
    public static IReadOnlyList<MaterialLayer> LayersOf(IModelView m, Entity e) => TypeOf(m, e) is { } t ? LayerSets.LayersOf(m, t.Id) : [];

    /// <summary>A plan area enclosed exactly (its arcs as arcs).</summary>
    public static double Area(Entity e) => ArcOutline.AreaOf(e);

    /// <summary>The laid points of edge <paramref name="k"/>, from its start corner to its end corner, as indices of the laid outline.</summary>
    public static List<int> EdgePoints(TopSurface.Solved top, int k)
    {
        var n = top.Points.Count;
        var (a, b) = (top.CornerIndex[k], top.CornerIndex[(k + 1) % top.CornerIndex.Count]);
        var o = new List<int>();
        for (var i = a; ; i = (i + 1) % n) { o.Add(i); if (i == b && o.Count > 1) break; if (o.Count > n + 1) break; }
        return o;
    }

    // ─── Body ───────────────────────────────────────────────────────────

    /// <summary>
    /// A layer of the build-up as a solid: between the top lowered by <paramref name="d0"/> and by <paramref name="d1"/>, plumb
    /// sides on the outline as laid; the top's own triangles, so a top on two planes has its fold.
    /// </summary>
    public static Solid? Slab(TopSurface.Solved top, double d0, double d1, string tag)
    {
        var n = top.Points.Count;
        if (n < 3 || d1 <= d0 + 1e-9) return null;
        var verts = new List<Vec3>(n * 2);
        for (var i = 0; i < n; i++) verts.Add(top.Points[i].At(top.Z[i] - d1));
        for (var i = 0; i < n; i++) verts.Add(top.Points[i].At(top.Z[i] - d0));
        var tris = new List<(int, int, int)>();
        foreach (var (a, b, c) in top.Triangles) { tris.Add((n + a, n + b, n + c)); tris.Add((a, c, b)); }
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            tris.Add((i, j, n + j));
            tris.Add((i, n + j, n + i));
        }
        if (SolidFromMesh.Build(verts, tris, tag) is not { } s) return null;
        return Measure.SignedVolume(s) < 0 ? SolidBuilder.Flip(s) : s;
    }

    /// <summary>Its layers as solids, top down, with their place in the set (a layer of no thickness has none).</summary>
    public static List<(int Index, MaterialLayer Layer, Solid Solid)> Bodies(IModelView m, Entity e)
    {
        var top = Top(e);
        var o = new List<(int, MaterialLayer, Solid)>();
        var down = 0.0;
        var layers = LayersOf(m, e);
        for (var i = 0; i < layers.Count; i++)
        {
            var l = layers[i];
            if (l.Thickness > 0 && Slab(top, down, down + l.Thickness, "layer:" + i) is { } s) o.Add((i, l, s));
            down += Math.Max(0, l.Thickness);
        }
        return o;
    }

    /// <summary>What it takes out of the terrain it is cut into: its layers and its kerb's.</summary>
    public static List<Solid> GroundCutters(IModelView m, Entity e)
    {
        // One solid from a hair above its top (the ground meets the top exactly: a cut flush with it would leave a skin of ground
        // over a sloped top, coplanar faces a boolean cannot settle) down to the bottom of its build-up.
        var depth = LayersOf(m, e).Sum(l => Math.Max(0, l.Thickness));
        var o = new List<Solid>();
        if (depth > 0 && Slab(Top(e), -CutAbove, depth, "hardscape") is { } body) o.Add(body);
        if (Kerbs.Of(m, e.Id) is { } k) o.AddRange(Kerbs.Solids(m, k, e).Select(x => x.Solid));
        return o;
    }

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices _)
    {
        var corners = Corners(e);
        if (corners.Count < 3) return new ElementGeometry([], Diagnostics: ["A hardscape needs an outline of three corners at least."]);
        var top = Top(e);
        var parts = new List<GeometryPart>();
        foreach (var (i, l, s) in Bodies(m, e))
            parts.Add(new GeometryPart(l.Name ?? m.Get(l.Material)?.Name ?? $"Layer {i + 1}", Materials.ColorOf(m, l.Material), s, l.Material, "layer:" + i));
        var problems = new List<string>();
        if (top.Note != null) problems.Add(top.Note);
        if (parts.Count == 0) problems.Add("The hardscape has no type with layers: give it one.");
        var plan = new List<PlanItem> { new("heavy", true, top.Points) };
        return new ElementGeometry(parts, plan, corners, problems.Count > 0 ? problems : null);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null || Corners(e).Count < 3) yield break;
        var top = Top(e);
        var area = Area(e);
        var surface = 0.0;
        foreach (var (a, b, c) in top.Triangles)
        {
            var (p, q, r) = (top.Points[a].At(top.Z[a]), top.Points[b].At(top.Z[b]), top.Points[c].At(top.Z[c]));
            surface += Vec3.Cross(q - p, r - p).Length / 2;
        }
        var layers = LayersOf(m, e);
        var depth = layers.Sum(l => Math.Max(0, l.Thickness));
        yield return new QuantitySet("Lapin_HardscapeQuantities",
        [
            new("Area", area, "AREA", "Paved area"),
            new("SurfaceArea", surface, "AREA", "Top surface area"),
            new("Perimeter", ArcOutline.PerimeterOf(e), "LENGTH", "Perimeter"),
            new("Depth", depth, "LENGTH", "Build-up depth"),
            new("Volume", area * depth, "VOLUME", "Volume"),
            new("MinElevation", top.Min, "LENGTH", "Lowest top"),
            new("MaxElevation", top.Max, "LENGTH", "Highest top"),
        ]);
        // Each layer: plumb sides under the top, so its volume is the plan area times its thickness, exactly.
        for (var i = 0; i < layers.Count; i++)
        {
            var l = layers[i];
            yield return new QuantitySet("Lapin_HardscapeLayerQuantities",
            [
                new("Thickness", l.Thickness, "LENGTH", "Thickness"),
                new("Area", area, "AREA", "Area"),
                new("Volume", area * Math.Max(0, l.Thickness), "VOLUME", "Volume"),
            ], "layer:" + i, l.Name ?? m.Get(l.Material)?.Name ?? $"Layer {i + 1}", l.Material);
        }
    }

    // ─── Types ──────────────────────────────────────────────────────────

    /// <summary>A hardscape type: its name, its pavement kind (RIGID, FLEXIBLE: IFC 4.3's), its layers top down.</summary>
    public static Entity CreateType(ICommandContext ctx, string name, IReadOnlyList<LayerSpec> layers, string? pavement, string? preset = null)
    {
        LayerSets.Validate(layers, "A hardscape type");
        EnsureLooks(ctx, layers);
        var attrs = new JsonObject();
        if (preset != null) { attrs["preset"] = preset; attrs["indicative"] = true; }
        var type = new Entity(ctx.NewId(), TypeClass, string.IsNullOrWhiteSpace(name) ? "Hardscape" : name.Trim(), attrs, Pavement(pavement));
        ctx.Add(type);
        LayerSets.Attach(ctx, type, layers);
        return type;
    }

    static string Pavement(string? p) => p?.ToUpperInvariant() is "FLEXIBLE" ? "FLEXIBLE" : "RIGID";

    /// <summary>A ready-made type in the project: the one made from that preset already, else a new one (marked indicative).</summary>
    public static Entity EnsurePreset(ICommandContext ctx, string key)
    {
        var p = Presets.FirstOrDefault(x => x.Key == key) ?? throw new CommandException($"There is no ready-made hardscape „{key}” ({string.Join(", ", Presets.Select(x => x.Key))}).");
        if (ctx.Model.OfClass(TypeClass).FirstOrDefault(t => t.Str("preset") == key) is { } had) return had;
        return CreateType(ctx, p.Name, [.. p.Layers.Select(l => new LayerSpec(l.Material, l.Thickness, Color: l.Color))], p.Pavement, key);
    }

    /// <summary>The type an argument names: <c>typeId</c>, else <c>type</c> (a preset's key, or a type's name), else the project's first, else the pavers.</summary>
    public static Entity TypeArg(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        if ((string?)a["typeId"] is { Length: > 0 } id) return Spatial.Require(m, id, TypeClass);
        if ((string?)a["type"] is { Length: > 0 } t)
            return m.OfClass(TypeClass).FirstOrDefault(x => x.Name == t) ?? EnsurePreset(ctx, t);
        return m.OfClass(TypeClass).FirstOrDefault() ?? EnsurePreset(ctx, "pavers");
    }

    /// <summary>The element of the type, its layers laid from its top down; its predefined type the type's pavement.</summary>
    public static void SetType(ICommandContext ctx, Entity e, Entity type)
    {
        ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        LayerSets.LayOut(ctx, e, type, "AXIS3", (_, _) => 0, negativeWhenNew: true);
        if (e.PredefinedType != type.PredefinedType && ctx.Model.Get(e.Id) is { } now) ctx.Replace(now.With(predefinedType: type.PredefinedType ?? "RIGID"));
    }

    // ─── Commands ───────────────────────────────────────────────────────

    /// <summary>The top an argument gives, over what the element had: its mode, elevation, grade, direction, origin, corners' heights, or a slope arrow.</summary>
    static TopSurface.Spec TopArg(JsonObject a, TopSurface.Spec was, IReadOnlyList<Vec2> corners)
    {
        var s = was;
        if ((string?)a["topMode"] is { } mode)
        {
            if (!TopSurface.Modes.Contains(mode)) throw new CommandException("A top is level, a slope or by its corners' heights.");
            s = s with { Mode = mode };
        }
        if (Json.Number(a["elevation"]) is { } z) s = s with { Elevation = z };
        if (a["grade"] != null)
        {
            var g = TopSurface.Grade(a["grade"]) ?? throw new CommandException("A grade is a fraction (0.02), a percentage (2 %) or one in n (1:50).");
            if (Math.Abs(g) > 1) throw new CommandException("A hardscape's grade is at most 100 %.");
            s = s with { Grade = g };
            if ((string?)a["topMode"] == null) s = s with { Mode = TopSurface.Plane };
        }
        if (Json.Number(a["direction"]) is { } dir) s = s with { Direction = ((dir % 360) + 360) % 360 };
        if (a["origin"] is JsonArray { Count: 2 } o && Json.Number(o[0]) is { } ox && Json.Number(o[1]) is { } oy) s = s with { Origin = new Vec2(ox, oy) };
        if (a["cornerZ"] is JsonArray cz)
        {
            var list = corners.Select((_, i) => i < cz.Count ? Json.Number(cz[i]) : (i < s.CornerZ.Count ? s.CornerZ[i] : null)).ToList();
            s = s with { CornerZ = list };
            if ((string?)a["topMode"] == null) s = s with { Mode = TopSurface.Corners };
        }
        if (a["arrow"] is JsonObject arrow)
        {
            Vec2 P(string k) => arrow[k] is JsonArray { Count: >= 2 } q && Json.Number(q[0]) is { } x && Json.Number(q[1]) is { } y ? new Vec2(x, y) : throw new CommandException("A slope arrow is {from: [x, y], to: [x, y]}.");
            s = TopSurface.Arrow(s, P("from"), P("to"), arrow["grade"] != null ? TopSurface.Grade(arrow["grade"]) : null, Json.Number(arrow["fromZ"]), Json.Number(arrow["toZ"]));
        }
        // A plane holds its elevation where it was set: its origin, kept once it is a plane.
        if (s.Mode == TopSurface.Plane && s.Origin == null) s = s with { Origin = Polygon2.Centroid(ArcOutline.Points(corners, null)) };
        if (s.CornerZ.Count != corners.Count) s = s with { CornerZ = [.. corners.Select((_, i) => i < s.CornerZ.Count ? s.CornerZ[i] : null)] };
        return s;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Hardscape",
        [
            new("elevation", "Elevation", "length"),
            new("topMode", "Top", "choice", Choices: TopSurface.Modes),
        ], Color));
        r.Class(new(TypeClass, "Hardscape type", [], Color, Kind: "type"));
        r.Geometry(Class, Geometry);
        r.Quantities(Class, Quantities);
        r.Service<INodeProps>(new Props());
        r.Service<IElementPlacement>(new Placement());
        r.Service<IElementRise>(new Rise());
        r.Service<IDeletionFollowers>(new Followers());
        r.Service<IDeletion>(new TypeDeletion());
        r.Service<IScheduleSource>(new Schedule());
        r.Catalog("hardscapeTypes", PresetsJson());
        Kerbs.Register(r);

        // A hardscape: {points, arcs?, typeId? | type? (a preset's key or a type's name), storeyId?, name?, terrainId? | standalone?,
        // topMode?, elevation?, grade?, direction?, origin?, cornerZ?, arrow?, kerb? {…, as setHardscapeKerb}}. On the terrain under
        // it unless standalone; its elevation, unless given, the ground's in its middle (0 without one).
        r.Command("addHardscape", "Hardscape", (ctx, a) => Hardscapes.AddHardscape(ctx, a));

        // Changed any time: {id, points?, arcs?, name?, typeId? | type?, topMode?, elevation?, grade?, direction?, origin?, cornerZ?,
        // arrow? {from, to, grade? | fromZ?, toZ?}, terrainId? (cut into that one), standalone? (true: out of its terrain)}.
        r.Command("updateHardscape", "Change hardscape", (ctx, a) =>
        {
            var m = ctx.Model;
            var e = Spatial.Require(m, (string?)a["id"], Class);
            var attrs = e.CloneAttrs();
            var corners = Corners(e);
            if (a["points"] != null)
            {
                var (c, rises) = Terrains.OutlineArg(a, ArcOutline.RisesOf(e), "A hardscape");
                var was = corners;
                corners = [.. c];
                attrs["contour"] = Terrains.ContourJson(corners);
                ArcOutline.Write(attrs, rises);
                // The corners' own heights stay with the corners that stayed (by place), a new one has none.
                if (attrs["cornerZ"] is JsonArray old)
                {
                    var kept = corners.Select(p => was.FindIndex(q => (q - p).Length < 1e-6) is var k and >= 0 && k < old.Count ? Json.Number(old[k]) : null).ToList();
                    if (kept.Any(z => z != null)) attrs["cornerZ"] = new JsonArray([.. kept.Select(z => z is { } v ? (JsonNode)v : null)]); else attrs.Remove("cornerZ");
                }
            }
            else if (a["arcs"] != null)
            {
                var (c, rises) = Terrains.OutlineArg(new JsonObject { ["points"] = Terrains.ContourJson(corners), ["arcs"] = a["arcs"]!.DeepClone() }, null, "A hardscape");
                attrs["contour"] = Terrains.ContourJson(c);
                ArcOutline.Write(attrs, rises);
            }
            TopSurface.Write(attrs, TopArg(a, TopSurface.Read(attrs, corners.Count), corners));
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : null;
            ctx.Replace(e.With(attrs: attrs, name: name));
            if (a["typeId"] != null || a["type"] != null) SetType(ctx, ctx.Model.Get(e.Id)!, TypeArg(ctx, a));
            var standalone = a["standalone"] is JsonValue sv && sv.TryGetValue<bool>(out var st) && st;
            if (standalone) ctx.Unrelate(IfcClass.RelVoidsElement, e.Id);
            else if ((string?)a["terrainId"] is { Length: > 0 } tid)
            {
                var t = Terrains.Require(m, tid);
                ctx.Unrelate(IfcClass.RelVoidsElement, e.Id);
                ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, t.Id, [e.Id]));
            }
            return null;
        });

        // A type: {name?, layers [{material, thickness}…] top down, pavement? (RIGID | FLEXIBLE)}, or {preset} (a ready-made one).
        r.Command("addHardscapeType", "Hardscape type", (ctx, a) =>
        {
            if ((string?)a["preset"] is { Length: > 0 } p) return new JsonObject { ["id"] = EnsurePreset(ctx, p).Id };
            var layers = LayerSets.Parse(a["layers"], "A hardscape type");
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"{layers[0].Material} {LayerSets.Millimetres(layers.Sum(l => l.Thickness))}";
            return new JsonObject { ["id"] = CreateType(ctx, name, layers, (string?)a["pavement"]).Id };
        });
        // {id, name?, layers?, pavement?}: every hardscape of the type follows.
        r.Command("updateHardscapeType", "Change hardscape type", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["id"], TypeClass);
            var attrs = type.CloneAttrs();
            string? name = null;
            if ((string?)a["name"] is { } n) { if (n.Trim().Length == 0) throw new CommandException("A type's name cannot be empty."); name = n.Trim(); }
            // Edited, it is the project's own: no longer the indicative preset it came from.
            attrs.Remove("indicative");
            var pre = (string?)a["pavement"] is { } pv ? Pavement(pv) : type.PredefinedType;
            ctx.Replace(type.With(name: name, attrs: attrs, predefinedType: pre));
            if (a["layers"] != null)
            {
                var layers = LayerSets.Parse(a["layers"], "A hardscape type");
                EnsureLooks(ctx, layers);
                LayerSets.Rewrite(ctx, ctx.Model.Get(type.Id)!, layers);
            }
            foreach (var e in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(Is).ToList())
                SetType(ctx, e, ctx.Model.Get(type.Id)!);
            // The paths built of it (PathTypes) read its layers as they are; its pavement kind they take.
            foreach (var p in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(x => x.Class == IfcClass.Pavement).ToList())
                PathTypes.Follow(ctx, p, ctx.Model.Get(type.Id)!);
            return null;
        });

        // A terrain's subregion made a hardscape (T4): {terrainId, subregionId, typeId? | type?, name?}. The same outline, its top by
        // its corners on the ground (a plane when they lie in one), cut into the terrain; the subregion goes. Without a type, one of
        // the subregion's material over the pavers' bedding and sub-base.
        r.Command("subregionToHardscape", "Subregion to hardscape", (ctx, a) =>
        {
            var m = ctx.Model;
            var t = Terrains.Require(m, (string?)a["terrainId"]);
            var sid = (string?)a["subregionId"] ?? throw new CommandException("Which subregion?");
            var sr = Terrains.SubregionsOf(t).FirstOrDefault(x => x.Id == sid) ?? throw new CommandException("The terrain has no such subregion.");
            var ground = Terrains.Base(t);
            var cz = new JsonArray();
            foreach (var c in sr.Contour) cz.Add(ground.ElevationAt(c.X, c.Y) is { } z ? (JsonNode)Math.Round(z, 6) : null);
            Entity type;
            if (a["typeId"] != null || a["type"] != null) type = TypeArg(ctx, a);
            else
            {
                var mat = m.Get(sr.Material);
                var matName = mat?.Name ?? "Concrete pavers";
                var typeName = $"{matName} paving";
                type = m.OfClass(TypeClass).FirstOrDefault(x => x.Name == typeName)
                    ?? CreateType(ctx, typeName, [new LayerSpec(matName, 0.06), new LayerSpec("Sand bedding", 0.04, Color: "#d8c89c"), new LayerSpec("Crushed stone", 0.20, Color: "#9d978c")], "RIGID");
            }
            var args = new JsonObject
            {
                ["points"] = Terrains.ContourJson(sr.Contour), ["terrainId"] = t.Id, ["typeId"] = type.Id, ["topMode"] = TopSurface.Corners, ["cornerZ"] = cz,
                ["name"] = (string?)a["name"] ?? sr.Name,
            };
            if (sr.Arcs != null) args["arcs"] = ArcOutline.ToJson(sr.Arcs);
            if (Spatial.StoreyOf(m, t.Id) is { } storey) args["storeyId"] = storey.Id;
            // A corner off the terrain takes the ground's in the middle.
            if (cz.Any(z => z == null)) args["elevation"] = ground.ElevationAt(Polygon2.Centroid(sr.Contour).X, Polygon2.Centroid(sr.Contour).Y) ?? 0;
            var made = (JsonObject)AddHardscape(ctx, args)!;
            var list = (t.Attrs["subregions"] as JsonArray)?.OfType<JsonObject>().Where(x => (string?)x["id"] != sid).Select(x => (JsonNode)x.DeepClone()).ToList() ?? [];
            var ta = ctx.Model.Get(t.Id)!.CloneAttrs();
            if (list.Count > 0) ta["subregions"] = new JsonArray([.. list]); else ta.Remove("subregions");
            ctx.Replace(ctx.Model.Get(t.Id)!.With(attrs: ta));
            return made;
        });

        // Its top edges in space (model heights), closed: what grading between hardscapes (T5) reads.
        r.Query("hardscapeEdges", (q, a) =>
        {
            var e = Spatial.Require(q.Model, (string?)a["id"], Class);
            var top = Top(e);
            return new JsonObject
            {
                ["closed"] = true,
                ["points"] = new JsonArray([.. top.Ring.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 6), Math.Round(p.Y, 6), Math.Round(p.Z, 6)))]),
                ["corners"] = new JsonArray([.. top.CornerIndex.Select(i => (JsonNode)i)]),
            };
        });
    }

    public static JsonNode? AddHardscape(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        var (corners, rises) = Terrains.OutlineArg(a, null, "A hardscape");
        var cs = corners.ToList();
        var mid = Polygon2.Centroid(ArcOutline.Points(cs, rises));
        var standalone = a["standalone"] is JsonValue sv && sv.TryGetValue<bool>(out var st) && st;
        var terrain = standalone ? null : (string?)a["terrainId"] is { Length: > 0 } tid ? Terrains.Require(m, tid) : Terrains.Under(m, mid) ?? cs.Select(c => Terrains.Under(m, c)).FirstOrDefault(t => t != null);
        var ground = terrain != null ? Terrains.Base(terrain).ElevationAt(mid.X, mid.Y) : null;
        var attrs = new JsonObject { ["contour"] = Terrains.ContourJson(cs), ["exportAs"] = IfcClass.Pavement };
        ArcOutline.Write(attrs, rises);
        var spec0 = new TopSurface.Spec(TopSurface.Level, Math.Round(ground ?? 0, 3), 0, 0, null, [.. cs.Select(_ => (double?)null)]);
        TopSurface.Write(attrs, TopArg(a, spec0, cs));
        var type = TypeArg(ctx, a);
        var n = m.OfClass(Class).Count() + 1;
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Hardscape {n}";
        var e = new Entity(ctx.NewId(), Class, name, attrs, type.PredefinedType ?? "RIGID");
        ctx.Add(e);
        var storey = (string?)a["storeyId"] ?? (terrain != null ? Spatial.StoreyOf(m, terrain.Id)?.Id : null) ?? Spatial.Storeys(m).FirstOrDefault()?.Id
            ?? throw new CommandException("The project has no storey.");
        Spatial.Contain(ctx, storey, e.Id);
        SetType(ctx, e, type);
        if (terrain != null) ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, terrain.Id, [e.Id]));
        var o = new JsonObject { ["id"] = e.Id };
        if (a["kerb"] is JsonObject kerb) o["kerbId"] = Kerbs.SetKerb(ctx, ctx.Model.Get(e.Id)!, kerb);
        if (terrain != null) o["terrainId"] = terrain.Id;
        return o;
    }

    /// <summary>What the interface draws its fields from: the corners' heights as the top puts them, the type, the layers, the kerb, the terrain.</summary>
    sealed class Props : INodeProps
    {
        public void Add(Entity e, IModelView m, JsonObject props)
        {
            if (!Is(e) || Corners(e).Count < 3) return;
            var top = Top(e);
            props["topZ"] = new JsonArray([.. top.CornerIndex.Select(i => (JsonNode)Math.Round(top.Z[i], 6))]);
            props["topPlanar"] = top.Planar;
            if (top.Note != null) props["topNote"] = top.Note;
            props["area"] = Math.Round(Area(e), 6);
            var c = Corners(e);
            var rises = Rises(e);
            props["edgeLengths"] = new JsonArray([.. c.Select((_, k) => (JsonNode)Math.Round(ArcOutline.Edge(c, rises, k).Length, 6))]);
            if (TypeOf(m, e) is { } t)
            {
                props["typeId"] = t.Id;
                props["typeName"] = t.Name;
                props["layers"] = new JsonArray([.. LayersOf(m, e).Select(l => (JsonNode)new JsonObject { ["material"] = m.Get(l.Material)?.Name ?? l.Material, ["thickness"] = l.Thickness })]);
            }
            if (Kerbs.Of(m, e.Id) is { } k) props["kerbId"] = k.Id;
            if (TerrainOf(m, e) is { } terrain) props["terrainId"] = terrain.Id;
        }
    }

    /// <summary>Moved, turned and mirrored as any element: its corners, its plane's origin and direction; a mirror runs it the other way round, its corners' data with it.</summary>
    sealed class Placement : IElementPlacement
    {
        public bool Handles(Entity e) => Is(e);

        public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
        {
            var c = Corners(e);
            var rises = Rises(e);
            var spec = TopSurface.Read(e.Attrs, c.Count);
            var attrs = e.CloneAttrs();
            var moved = c.Select(t.Point).ToList();
            var cz = spec.CornerZ.ToList();
            if (t.Mirrored)
            {
                // Mirrored it runs clockwise: turned back, corners reversed and each edge's data with it (a rise keeps its sign: the mirror and the reversal each turn it).
                moved.Reverse();
                cz.Reverse();
                if (rises != null) rises = ArcOutline.RunBack(rises);
                if (Kerbs.Of(ctx.Model, e.Id) is { } k) Kerbs.RunBack(ctx, k, c.Count);
            }
            attrs["contour"] = Terrains.ContourJson(moved);
            ArcOutline.Write(attrs, rises);
            var o = spec.Origin is { } p ? t.Point(p) : (Vec2?)null;
            var d = t.Dir(spec.Down);
            TopSurface.Write(attrs, spec with { Origin = o, Direction = Math.Atan2(d.Y, d.X) * 180 / Math.PI, CornerZ = cz });
            ctx.Replace(e.With(attrs: attrs));
        }
    }

    /// <summary>A 3D move's vertical part raises its top: its elevation and its corners' own heights.</summary>
    sealed class Rise : IElementRise
    {
        public bool Handles(Entity e) => Is(e);

        public bool Lift(ICommandContext ctx, Entity e, double dz)
        {
            var c = Corners(e);
            var s = TopSurface.Read(e.Attrs, c.Count);
            var attrs = e.CloneAttrs();
            TopSurface.Write(attrs, s with { Elevation = s.Elevation + dz, CornerZ = [.. s.CornerZ.Select(z => z is { } v ? v + dz : (double?)null)] });
            ctx.Replace(e.With(attrs: attrs));
            return true;
        }
    }

    /// <summary>A hardscape deleted takes its kerb with it.</summary>
    sealed class Followers : IDeletionFollowers
    {
        public void Deleting(ICommandContext ctx, IReadOnlySet<string> deleting)
        {
            foreach (var k in ctx.Model.OfClass(Kerbs.Class).ToList())
                if (k.Str(Kerbs.HostKey) is { } h && deleting.Contains(h) && !deleting.Contains(k.Id)) ctx.Remove(k.Id);
        }
    }

    /// <summary>A hardscape type in use stays, as a wall type in use does.</summary>
    sealed class TypeDeletion : IDeletion
    {
        public void Check(IModelView m, Entity e, IReadOnlySet<string> deleting)
        {
            if (e.Class != TypeClass) return;
            var n = m.RelatedOf(e.Id, IfcClass.RelDefinesByType).Count(x => !deleting.Contains(x.Id));
            if (n > 0) throw new CommandException($"„{e.Name}” is used by {n} hardscape(s) or path(s): give them another type first.");
        }

        public bool Delete(ICommandContext ctx, string id) => false;
    }

    /// <summary>The hardscape schedule: each one's type, area, build-up, every layer's volume, its kerb.</summary>
    sealed class Schedule : IScheduleSource
    {
        public string Kind => "hardscape";
        public string Label => "Hardscape schedule";

        public ScheduleGrid Grid(IQueryContext q)
        {
            var m = q.Model;
            string N(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);
            var rows = new List<IReadOnlyList<string>>();
            foreach (var e in m.OfClass(Class))
            {
                if (Corners(e).Count < 3) continue;
                var area = Area(e);
                var layers = LayersOf(m, e);
                var kerb = Kerbs.Of(m, e.Id) is { } k ? Kerbs.Length(m, k, e) : 0;
                rows.Add([e.Name, TypeOf(m, e)?.Name ?? "", N(area), N(layers.Sum(l => l.Thickness) * 100, "0.#"),
                    string.Join("; ", layers.Select(l => $"{m.Get(l.Material)?.Name ?? l.Material} {N(area * l.Thickness, "0.000")} m³")), kerb > 0 ? N(kerb) : ""]);
            }
            return new ScheduleGrid(Label, ["Name", "Type", "Area (m²)", "Build-up (cm)", "Layers", "Kerb (m)"], rows);
        }
    }

}
