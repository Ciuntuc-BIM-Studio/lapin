using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>A layer of a surface finish, from the face outward: its material and thickness (0: measured by its area only, decision 291).</summary>
public sealed record SurfaceLayer(string Material, double Thickness);

/// <summary>
/// A finish laid on a face rather than in a room (forum topic 83): a facade's ETICS, a wall's tiles, a stone panel. Its layers
/// run from the face outward; what it is meant for (<c>walls</c>, <c>floors</c>, <c>ceilings</c> or <c>any</c>) only orders the
/// painter's palette.
/// </summary>
public sealed record SurfaceFinishSpec(string Name, List<SurfaceLayer> Layers, string Use = "any")
{
    public static readonly string[] Uses = ["walls", "floors", "ceilings", "any"];

    static SurfaceFinishSpec S(string name, string use, params (string M, double T)[] layers) => new(name, layers.Select(l => new SurfaceLayer(l.M, l.T)).ToList(), use);

    /// <summary>The ready-made ones the painter offers (indicative build-ups; the user's own are made in the palette).</summary>
    public static readonly SurfaceFinishSpec[] Library =
    [
        S("ETICS EPS 100", "walls", ("Adhesive mortar", 0.005), ("EPS insulation", 0.10), ("Render", 0.01)),
        S("ETICS mineral wool 100", "walls", ("Adhesive mortar", 0.005), ("Mineral wool", 0.10), ("Render", 0.01)),
        S("Lime plaster 25", "walls", ("Lime plaster", 0.025)),
        S("Wall tiles", "walls", ("Tile adhesive", 0.005), ("Ceramic tiles", 0.01)),
        S("Stone cladding 30", "walls", ("Mortar", 0.01), ("Stone", 0.03)),
        S("Paint", "any", ("Paint", 0.001)),
        S("Floor tiles", "floors", ("Tile adhesive", 0.005), ("Ceramic tiles", 0.01)),
        S("Gypsum board ceiling", "ceilings", ("Gypsum board", 0.0125), ("Paint", 0.001)),
    ];

    public JsonObject ToJson() => new()
    {
        ["name"] = Name, ["use"] = Use,
        ["layers"] = new JsonArray(Layers.Select(l => (JsonNode)new JsonObject { ["material"] = l.Material, ["thickness"] = l.Thickness }).ToArray()),
    };

    /// <summary>A spec as a command gives it, checked: a name, layers each a material and a thickness.</summary>
    public static SurfaceFinishSpec From(JsonNode? node)
    {
        if (node is not JsonObject o || (string?)o["name"] is not { } name || string.IsNullOrWhiteSpace(name)) throw new CommandException("A surface finish needs a name.");
        var use = (string?)o["use"] ?? "any";
        if (!Uses.Contains(use)) throw new CommandException($"A surface finish is for {string.Join(", ", Uses)}.");
        return new(name.Trim(), FinishZones.ReadLayers(o["layers"]) is { Count: > 0 } l ? l : throw new CommandException("A surface finish needs a layer."), use);
    }

    public static SurfaceFinishSpec? Read(JsonNode? node)
    {
        try { return From(node); }
        catch (CommandException) { return null; }
    }
}

/// <summary>
/// The finish painter (forum topic 83, docs/plan-finish-painter.md): finishes painted on the named faces of elements
/// (<see cref="IElementFaces"/>: a wall's two sides, a floor's top, soffit and edges, a column's sides), whole or on the regions
/// the lines drawn on a face divide it into. Everything on a face is kept in the face's own frame (u to the right seen from
/// outside, v up it, w out of it), so it follows the host. A zone (<see cref="Class"/>) with a seed paints the region holding
/// it; one without, the rest of the face; its layers are bodies from the face outward, the face's openings and the regions
/// inside taken out, mitred at a wall's outer corner. Exported as IfcCovering (CLADDING, FLOORING, CEILING) covering its host.
/// </summary>
public static class FinishZones
{
    public const string Class = "LapinFinishZone";
    public const string SplitClass = "LapinFinishSplit";
    public const string TypeClass = "LapinSurfaceFinishType";
    const double Tol = 1e-6;

    // ------------------------------------------------------------ the face's own frame

    /// <summary>
    /// A face as the painter sees it: its frame (origin, u, v, out), its outline and openings in (u, v), whether its layers are
    /// mitred at its low or high end in u (a wall's outer corner), and its extent in u.
    /// </summary>
    public sealed record Frame(ElementFace Face, Vec3 O, Vec3 U, Vec3 V, Vec3 N, List<Vec2> Outline, List<List<Vec2>> Holes, bool MitreLow, bool MitreHigh)
    {
        public Vec2 Map(Vec3 p) { var d = p - O; return new(Vec3.Dot(d, U), Vec3.Dot(d, V)); }
        public double Depth(Vec3 p) => Vec3.Dot(p - O, N);
        public Vec3 At(Vec2 q, double w = 0) => O + U * q.X + V * q.Y + N * w;
        public bool Upright => Math.Abs(N.Z) < 0.3;
        public double U0 => Outline.Min(p => p.X);
        public double U1 => Outline.Max(p => p.X);
        public double V0 => Outline.Min(p => p.Y);
        public double V1 => Outline.Max(p => p.Y);
    }

    /// <summary>The faces a finish may be painted on: every area face but a wall's top and its cast zones' (they lie in its sides).</summary>
    public static List<ElementFace> Paintable(IServices s, Entity host, IModelView m) =>
        ElementFaces.Of(s, host, m).Where(f => !f.IsEdge && !(host.Class == IfcClass.Wall && (f.Role == "Top" || f.Role.StartsWith("Zone[", StringComparison.Ordinal)))).ToList();

    /// <summary>The frame of <paramref name="role"/> on <paramref name="host"/>; null when it has no such face now.</summary>
    public static Frame? FrameOf(IModelView m, IServices s, Entity host, string? role)
    {
        if (role == null || Paintable(s, host, m).FirstOrDefault(f => f.Role == role) is not { } face || face.Outline.Count < 3) return null;
        var n = face.Normal.Normalized();
        var u = Math.Abs(n.Z) < 0.7 ? Vec3.Cross(Vec3.UnitZ, n).Normalized() : new Vec3(1, 0, 0);
        var v = Vec3.Cross(n, u);
        var o = face.Outline[0];
        // A wall's side: from its axis's start (dragging its end keeps what is on it in place); a floor's: from its first point.
        var side = host.Class == IfcClass.Wall && role is "Face.Left" or "Face.Right" && PathCurve.Of(host) is not { IsArc: true };
        if (side)
        {
            var start = Walls.Start(host);
            o = new Vec3(start.X, start.Y, face.Outline.Min(p => p.Z)) + n * Vec3.Dot(face.Outline[0] - new Vec3(start.X, start.Y, face.Outline[0].Z), n);
        }
        else if (host.Class == IfcClass.Slab && Slabs.Contour(host) is { Count: > 0 } c) o = new Vec3(c[0].X, c[0].Y, face.Outline[0].Z);
        Vec2 Map(Vec3 p) { var d = p - o; return new(Vec3.Dot(d, u), Vec3.Dot(d, v)); }
        var outline = face.Outline.Select(Map).ToList();
        var holes = (face.Holes ?? []).Select(h => h.Select(Map).ToList()).ToList();
        bool low = false, high = false;
        if (side && Walls.LayoutOf(host, m, s, []) is { } layout)
        {
            // The side as the joins leave it: the outermost layer's footprint along the face's line, past the axis's ends at an outer corner.
            var band = role == "Face.Left" ? layout.Bands.MaxBy(b => b.To) : layout.Bands.MinBy(b => b.From);
            var on = layout.Footprints(band).SelectMany(fp => fp).Where(p => Math.Abs(Vec3.Dot(new Vec3(p.X, p.Y, o.Z) - o, n)) < 2e-3).Select(p => Map(new Vec3(p.X, p.Y, o.Z)).X).ToList();
            if (on.Count >= 2)
            {
                var axis = new[] { 0.0, Vec3.Dot(new Vec3(Walls.End(host).X - Walls.Start(host).X, Walls.End(host).Y - Walls.Start(host).Y, 0), u) };
                var (a0, a1) = (axis.Min(), axis.Max());
                var (u0, u1) = (on.Min(), on.Max());
                var (v0, v1) = (outline.Min(p => p.Y), outline.Max(p => p.Y));
                outline = [new(u0, v0), new(u1, v0), new(u1, v1), new(u0, v1)];
                low = u0 < a0 - 1e-3;
                high = u1 > a1 + 1e-3;
            }
        }
        if (Polygon2.SignedArea(outline) < 0) outline.Reverse();
        return new Frame(face, o, u, v, n, outline, holes, low, high);
    }

    /// <summary>The face a command names: <c>face</c> (its role), else the one at <c>point</c> (3D with <c>normal</c>; a plan's x, y without).</summary>
    static (Entity Host, Frame Frame, Vec3? Point, bool PlanOnly) Resolve(ICommandContext ctx, JsonObject a, bool needPoint = false)
    {
        var m = ctx.Model;
        var host = m.Get((string?)a["hostId"] ?? "") ?? throw new CommandException("Which element's face?");
        // A stroke on a zone already painted goes to its host's face under it.
        string? zoneFace = null;
        if (host.Class == Class) (host, zoneFace) = (m.Get(host.Str("hostId") ?? "") ?? throw new CommandException("The finish's element is gone."), host.Str("face"));
        var faces = Paintable(ctx.Services, host, m);
        if (faces.Count == 0) throw new CommandException($"{host.Name} has no faces a finish can be painted on.");
        var planOnly = a["point"] is JsonArray { Count: 2 };
        var point = Args.TryPoint3(a["point"], needZ: false);
        if (needPoint && point == null) throw new CommandException("Where on the face? Give point [x, y, z].");
        string? role = (string?)a["face"] ?? zoneFace;
        if (role == null)
        {
            if (point is not { } p) throw new CommandException("Which face? Give face (its name) or point.");
            var normal = Args.TryPoint3(a["normal"]);
            if (normal == null && host.Class == IfcClass.Slab) role = (string?)a["look"] == "up" ? "Soffit" : "Top";
            else
            {
                var among = normal == null && host.Class == IfcClass.Wall ? faces.Where(f => f.Role.StartsWith("Face.", StringComparison.Ordinal)).ToList() : faces;
                role = (ElementFaces.At(among, p, normal, normal == null ? 1.0 : 0.3) ?? throw new CommandException("No face of the element there.")).Role;
            }
        }
        var frame = FrameOf(m, ctx.Services, host, role) ?? throw new CommandException($"{host.Name} has no face „{role}” to paint.");
        return (host, frame, point, planOnly);
    }

    /// <summary>A point given on the face, in its frame: a plan's point (no height) a metre up an upright face, or half way when it is lower.</summary>
    static Vec2 OnFace(Frame f, Vec3 p, bool planOnly)
    {
        var q = f.Map(p);
        if (planOnly && f.Upright) q = q with { Y = f.V0 + Math.Min(1.0, (f.V1 - f.V0) / 2) };
        return q;
    }

    // ------------------------------------------------------------ the regions

    /// <summary>A region of a face: its loop (counter-clockwise, in the face's frame), its area, a point inside it.</summary>
    public sealed record Region(List<Vec2> Loop, double Area, Vec2 Inside);

    /// <summary>The splits drawn on a face, as segments in its frame: a level line across the whole of an upright face, a line, a polyline, a closed shape.</summary>
    static List<(Vec2 A, Vec2 B)> SplitSegments(IModelView m, Frame f, string hostId, string role)
    {
        var segs = new List<(Vec2, Vec2)>();
        foreach (var s in m.OfClass(SplitClass).Where(x => x.Str("hostId") == hostId && x.Str("face") == role))
        {
            if (s.NumOrNull("height") is { } h) { segs.Add((new Vec2(f.U0 - 1, f.V0 + h), new Vec2(f.U1 + 1, f.V0 + h))); continue; }
            var pts = Args.StoredPoints(s.Attrs["points"]);
            for (var i = 0; i + 1 < pts.Count; i++) segs.Add((pts[i], pts[i + 1]));
            if (s.Bool("closed") && pts.Count >= 3) segs.Add((pts[^1], pts[0]));
        }
        return segs;
    }

    /// <summary>The regions the face's splits divide it into (the face itself when it has none).</summary>
    public static List<Region> Regions(IModelView m, Frame f, string hostId)
    {
        var splits = SplitSegments(m, f, hostId, f.Face.Role);
        var outline = f.Outline;
        Region Of(List<Vec2> loop) => new(loop, Math.Abs(Polygon2.SignedArea(loop)), Polygon2.InteriorPoint(loop));
        if (splits.Count == 0) return [Of(outline)];
        var segs = outline.Select((p, i) => (p, outline[(i + 1) % outline.Count])).Concat(splits).ToList();
        return SketchRegions.Regions(segs, 1e-6).Select(Of).Where(r => r.Area > 1e-9 && Polygon2.Contains(outline, r.Inside)).ToList();
    }

    /// <summary>The region holding a point: the smallest; -1 when none does.</summary>
    public static int RegionAt(List<Region> regions, Vec2 p)
    {
        var best = -1;
        for (var i = 0; i < regions.Count; i++)
            if (Polygon2.Contains(regions[i].Loop, p) && (best < 0 || regions[i].Area < regions[best].Area)) best = i;
        return best;
    }

    /// <summary>The regions inside region <paramref name="k"/> (islands drawn in it), the outermost of them only.</summary>
    static List<Region> Inner(List<Region> regions, int k)
    {
        var r = regions[k];
        var inside = regions.Where((q, i) => i != k && q.Area < r.Area - 1e-12 && Polygon2.Contains(r.Loop, q.Inside)).ToList();
        return inside.Where(q => !inside.Any(o => o != q && o.Area > q.Area && Polygon2.Contains(o.Loop, q.Inside))).ToList();
    }

    static Vec2? Seed(Entity zone) => Args.TryPoint(zone.Attrs["seed"]);

    /// <summary>The zones on a face, oldest first.</summary>
    public static List<Entity> OnFace(IModelView m, string hostId, string role) =>
        m.OfClass(Class).Where(z => z.Str("hostId") == hostId && z.Str("face") == role).OrderBy(z => z.Num("order")).ThenBy(z => z.Id, StringComparer.Ordinal).ToList();

    /// <summary>Which zone paints each region: the newest whose seed it holds, else the newest painting the rest of the face; null: none.</summary>
    public static Entity?[] Owners(List<Region> regions, List<Entity> zones)
    {
        var owners = new Entity?[regions.Count];
        var rest = zones.LastOrDefault(z => Seed(z) == null);
        for (var i = 0; i < regions.Count; i++) owners[i] = rest;
        foreach (var z in zones)
            if (Seed(z) is { } s && RegionAt(regions, s) is var k && k >= 0) owners[k] = z;
        return owners;
    }

    // ------------------------------------------------------------ the finish

    public static List<SurfaceLayer> ReadLayers(JsonNode? node)
    {
        var o = new List<SurfaceLayer>();
        foreach (var l in (node as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var material = ((string?)l["material"])?.Trim();
            if (string.IsNullOrEmpty(material)) throw new CommandException("Every layer needs a material.");
            if (Json.Number(l["thickness"]) is not { } t || !(t >= 0 && t < 1)) throw new CommandException("Every layer needs a thickness (m, under 1).");
            o.Add(new SurfaceLayer(material, t));
        }
        return o;
    }

    /// <summary>The part of a room finish a face takes by default: the walls' on an upright face, the floor's on a top, the ceiling's on a soffit.</summary>
    static string PartFor(Frame f) => f.Upright ? "walls" : f.N.Z > 0 ? "floor" : "ceiling";

    /// <summary>
    /// A zone's finish: its name and layers from the face outward. A room finish's part as its type says now (its floor turned
    /// over: listed from the top down); a surface finish's; its own layers. Bare: none.
    /// </summary>
    public static (string Name, List<SurfaceLayer> Layers) FinishOf(IModelView m, Entity zone)
    {
        if (zone.Bool("bare")) return ("Bare", []);
        if (zone.Attrs["layers"] is JsonArray own)
        {
            var l = ReadLayers(own);
            return (zone.Str("finishName") ?? string.Join(" + ", l.Select(x => x.Material)), l);
        }
        if (zone.Str("finishType") is { } tid && m.Get(tid) is { } type)
        {
            if (type.Class == TypeClass && SurfaceFinishSpec.Read(type.Attrs["spec"]) is { } s) return (type.Name, s.Layers);
            if (type.Class == RoomFinishes.TypeClass && RoomFinishes.SpecOf(m, type) is { } spec)
            {
                var part = zone.Str("part") ?? "walls";
                var layers = part switch { "floor" => spec.Floor?.AsEnumerable().Reverse(), "ceiling" => spec.Ceiling, _ => spec.Walls };
                return ($"{type.Name} · {part}", (layers ?? []).Select(x => new SurfaceLayer(x.Material, x.Thickness)).ToList());
            }
        }
        return ("", []);
    }

    // ------------------------------------------------------------ the bodies

    /// <summary>What a zone measured by layer and material: its gross (its regions'), net (openings out) area, its volume.</summary>
    public sealed record ZoneMeasure(int Layer, string Material, double Thickness, double Gross, double Net, double Volume);

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ElementGeometry, List<ZoneMeasure>> Built = new();

    public static List<ZoneMeasure> Measures(ElementGeometry? g) => g != null && Built.TryGetValue(g, out var o) ? o : [];

    /// <summary>A loop of the face swept along its normal from w0 by dw (a cutter: through the zone and past it).</summary>
    static Solid? Sweep(Frame f, IReadOnlyList<Vec2> loop, double w0, double dw) =>
        Prism.Extrude(loop.Select(q => f.At(q, w0)).ToList(), null, f.N * dw, "finish");

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices services)
    {
        if (m.Get(e.Str("hostId") ?? "") is not { } host) return new ElementGeometry([], Diagnostics: ["The finish's element is gone."]);
        if (FrameOf(m, services, host, e.Str("face")) is not { } f) return new ElementGeometry([], Diagnostics: [$"{host.Name} has no face „{e.Str("face")}” now."]);
        var (_, layers) = FinishOf(m, e);
        var regions = Regions(m, f, host.Id);
        var owners = Owners(regions, OnFace(m, host.Id, f.Face.Role));
        var booleans = services.Get<ISolidBooleans>();
        var problems = new List<string>();
        var parts = new List<GeometryPart>();
        double gross = 0, net = 0;
        var volumes = new double[layers.Count];
        for (var k = 0; k < regions.Count; k++)
        {
            if (owners[k]?.Id != e.Id) continue;
            var r = regions[k];
            var inner = Inner(regions, k);
            gross += r.Area - inner.Sum(q => q.Area);
            if (layers.Count == 0) continue;
            // The region one unit deep, the face's openings and the islands drawn in it taken out: its volume is its net area.
            var unit = Sweep(f, r.Loop, 0, 1);
            if (unit == null) continue;
            var (lo, hi) = (new Vec2(r.Loop.Min(p => p.X), r.Loop.Min(p => p.Y)), new Vec2(r.Loop.Max(p => p.X), r.Loop.Max(p => p.Y)));
            var cutters = f.Holes.Where(h => h.Max(p => p.X) > lo.X + Tol && h.Min(p => p.X) < hi.X - Tol && h.Max(p => p.Y) > lo.Y + Tol && h.Min(p => p.Y) < hi.Y - Tol)
                .Concat(inner.Select(q => q.Loop)).Select(l => Sweep(f, l, -0.5, 2)).OfType<Solid>().ToList();
            if (cutters.Count > 0)
            {
                if (booleans == null) problems.Add("Openings need the boolean library (manifold), missing here: the finish is drawn uncut.");
                else
                {
                    try { unit = booleans.Difference(unit, cutters, "finish"); }
                    catch (InvalidOperationException ex) { problems.Add($"An opening could not be cut ({ex.Message})."); }
                }
            }
            if (unit == null) continue;
            var area = Measure.Volume(unit);
            net += area;
            var d = 0.0;
            for (var i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                if (l.Thickness > 0)
                {
                    var (d0, t) = (d, l.Thickness);
                    // From the face outward, d0 to d0 + t; at an outer corner the end leans out with the depth (45°), so two facades close it.
                    var body = SolidBuilder.Transform(unit, p =>
                    {
                        var w = d0 + f.Depth(p) * t;
                        var q = f.Map(p);
                        var shift = f.MitreHigh && q.X > f.U1 - 1e-6 ? w : f.MitreLow && q.X < f.U0 + 1e-6 ? -w : 0;
                        return f.At(q with { X = q.X + shift }, w);
                    });
                    var mat = Materials.Find(m, l.Material)?.Id;
                    parts.Add(new GeometryPart(l.Material, Materials.ColorOf(m, mat), body, mat, $"finish:{i}"));
                    volumes[i] += Measure.Volume(body);
                }
                d += l.Thickness;
            }
        }
        var g = new ElementGeometry(parts, Diagnostics: problems.Count > 0 ? problems : null);
        Built.AddOrUpdate(g, layers.Select((l, i) => new ZoneMeasure(i, l.Material, l.Thickness, gross, net, volumes[i])).ToList());
        return g;
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        var measures = Measures(g);
        if (measures.Count == 0) yield break;
        yield return new QuantitySet("Qto_CoveringBaseQuantities",
        [
            new("Width", measures.Sum(x => x.Thickness), "LENGTH"),
            new("GrossArea", measures[0].Gross, "AREA", "Gross area"),
            new("NetArea", measures[0].Net, "AREA", "Net area"),
        ]);
        foreach (var x in measures)
            yield return new QuantitySet("Lapin_FinishLayerQuantities",
            [
                new("Width", x.Thickness, "LENGTH"),
                new("GrossArea", x.Gross, "AREA", "Gross area"),
                new("NetArea", x.Net, "AREA", "Net area"),
                new("NetVolume", x.Volume, "VOLUME", "Net volume"),
            ], $"finish:{x.Layer}", x.Material, Materials.Find(m, x.Material)?.Id);
    }

    /// <summary>The splits drawn on a face, as lines in 3D (a hair off it), so the regions show before they are painted.</summary>
    static ElementGeometry? SplitGeometry(Entity e, IModelView m, IServices services)
    {
        if (m.Get(e.Str("hostId") ?? "") is not { } host || FrameOf(m, services, host, e.Str("face")) is not { } f) return new ElementGeometry([]);
        List<Vec3> Line(IEnumerable<Vec2> pts) => pts.Select(q => f.At(q, 0.003)).ToList();
        var lines = new List<IReadOnlyList<Vec3>>();
        if (e.NumOrNull("height") is { } h) lines.Add(Line([new(f.U0, f.V0 + h), new(f.U1, f.V0 + h)]));
        else
        {
            var pts = Args.StoredPoints(e.Attrs["points"]);
            if (pts.Count >= 2) lines.Add(Line(e.Bool("closed") ? pts.Append(pts[0]) : pts));
        }
        return new ElementGeometry([], Lines: lines, LineColor: "#d0502a");
    }

    // ------------------------------------------------------------ the commands

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(TypeClass, "Surface finish", [], "#d9cdb8", Kind: "type"));
        r.Class(new(Class, "Finish zone", [], "#e6dccb"));
        r.Class(new(SplitClass, "Finish split", [], "#d0502a", Kind: "annotation"));
        r.Geometry(Class, Geometry);
        r.Geometry(SplitClass, SplitGeometry);
        r.Quantities(Class, Quantities);
        r.Service<IScheduleSource>(new FinishZoneSchedule());
        r.Service<ICommandFollowUp>(new HostGone());
        r.Catalog("surfaceFinishes", new JsonObject { ["library"] = new JsonArray(SurfaceFinishSpec.Library.Select(s => (JsonNode)s.ToJson()).ToArray()) });

        r.Command("createSurfaceFinishType", "New surface finish", (ctx, a) => new JsonObject { ["id"] = CreateType(ctx, SurfaceFinishSpec.From(a["spec"])).Id });
        r.Command("updateSurfaceFinishType", "Change surface finish", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["id"], TypeClass);
            var spec = SurfaceFinishSpec.From(a["spec"]);
            foreach (var l in spec.Layers) EnsureMaterial(ctx, l.Material);
            var attrs = type.CloneAttrs();
            attrs["spec"] = spec.ToJson();
            ctx.Replace(type.With(name: spec.Name, attrs: attrs));
            return null;
        });
        r.Command("surfaceFinishTypeFromLibrary", "Surface finish from the library", (ctx, a) =>
        {
            var name = (string?)a["name"] ?? throw new CommandException("Which library finish?");
            var spec = SurfaceFinishSpec.Library.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new CommandException($"The library has no surface finish „{name}”.");
            var found = ctx.Model.OfClass(TypeClass).FirstOrDefault(t => string.Equals(t.Name, spec.Name, StringComparison.OrdinalIgnoreCase));
            return new JsonObject { ["id"] = (found ?? CreateType(ctx, spec)).Id, ["created"] = found == null };
        });

        // A stroke of the brush: {hostId, face | point (+ normal in 3D), whole? (every zone of the face replaced) | rest? (the regions painted kept), finishType (+ part) | layers}.
        r.Command("paintFinish", "Paint finish", (ctx, a) =>
        {
            static bool Flag(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;
            var (whole, rest) = (Flag(a["whole"]), Flag(a["rest"]));
            var (host, f, point, planOnly) = Resolve(ctx, a, needPoint: !whole && !rest);
            var attrs = FinishArgs(ctx, a, f);
            var zone = Paint(ctx, host, f, whole || rest ? null : OnFace(f, point!.Value, planOnly), attrs, keepRegions: rest);
            return new JsonObject { ["id"] = zone.Id, ["face"] = f.Face.Role };
        });
        // A split drawn on a face: {hostId, face | point (+ normal), points [[x, y, z], …] (closed?) | height (m above the face's bottom)}.
        r.Command("splitFinishFace", "Split face", (ctx, a) =>
        {
            var (host, f, _, _) = Resolve(ctx, a);
            var attrs = new JsonObject { ["hostId"] = host.Id, ["face"] = f.Face.Role };
            if (Json.Number(a["height"]) is { } h)
            {
                if (!f.Upright) throw new CommandException("A split at a height is for an upright face (a wall's side).");
                if (!(h > 1e-3 && h < f.V1 - f.V0 - 1e-3)) throw new CommandException($"The height is within the face: between 0 and {(f.V1 - f.V0).ToString("0.###", CultureInfo.InvariantCulture)} m.");
                attrs["height"] = h;
            }
            else
            {
                var pts = Args.StoredPoints3(a["points"], needZ: false);
                if (pts.Count < 2) throw new CommandException("A split is two points at least: points [[x, y, z], …].");
                var on = pts.Select(p => f.Map(p)).ToList();
                if (on.Zip(on.Skip(1)).All(z => (z.Second - z.First).Length < 1e-4)) throw new CommandException("The split has no length.");
                attrs["points"] = new JsonArray(on.Select(q => (JsonNode)new JsonArray(Math.Round(q.X, 6), Math.Round(q.Y, 6))).ToArray());
                if (a["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var closed) && closed && pts.Count >= 3) attrs["closed"] = true;
            }
            var split = new Entity(ctx.NewId(), SplitClass, $"{host.Name} · {f.Face.Label} · split", attrs);
            ctx.Add(split);
            return new JsonObject { ["id"] = split.Id, ["face"] = f.Face.Role };
        });
        // Cleared: {hostId, face | point (+ normal), all?, splits?}: the region under the point (bare when the rest of the face paints it), or every zone of the face.
        r.Command("clearFinish", "Clear finish", (ctx, a) =>
        {
            var all = a["all"] is JsonValue av && av.TryGetValue<bool>(out var ab) && ab;
            var (host, f, point, planOnly) = Resolve(ctx, a, needPoint: !all);
            var zones = OnFace(ctx.Model, host.Id, f.Face.Role);
            if (all)
            {
                foreach (var z in zones) ctx.Remove(z.Id);
                if (a["splits"] is JsonValue sv && sv.TryGetValue<bool>(out var sb) && sb)
                    foreach (var s in ctx.Model.OfClass(SplitClass).Where(x => x.Str("hostId") == host.Id && x.Str("face") == f.Face.Role).ToList()) ctx.Remove(s.Id);
                return null;
            }
            var regions = Regions(ctx.Model, f, host.Id);
            var at = OnFace(f, point!.Value, planOnly);
            var k = RegionAt(regions, at);
            if (k < 0) throw new CommandException("That point is off the face.");
            var owner = Owners(regions, zones)[k] ?? throw new CommandException("Nothing is painted there.");
            if (Seed(owner) != null)
            {
                foreach (var z in zones.Where(z => Seed(z) is { } s && RegionAt(regions, s) == k)) ctx.Remove(z.Id);
                return null;
            }
            Paint(ctx, host, f, at, new JsonObject { ["bare"] = true });
            return null;
        });

        // The face (or the region of it) under a pick, lit by the painter, and what is painted there (the eyedropper).
        r.Query("finishFaceAt", (q, a) =>
        {
            var m = q.Model;
            if (m.Get((string?)a["id"] ?? "") is not { } host || Args.TryPoint3(a["point"], needZ: false) is not { } p) return null;
            // On a zone: its host's face under it.
            var onZone = host.Class == Class ? host : null;
            if (onZone != null)
            {
                if (m.Get(onZone.Str("hostId") ?? "") is not { } zoneHost) return null;
                host = zoneHost;
            }
            var faces = Paintable(q.Services, host, m);
            var normal = Args.TryPoint3(a["normal"]);
            var face = onZone != null ? faces.FirstOrDefault(x => x.Role == onZone.Str("face"))
                : normal == null && host.Class == IfcClass.Slab ? faces.FirstOrDefault(x => x.Role == "Top")
                : ElementFaces.At(normal == null && host.Class == IfcClass.Wall ? faces.Where(x => x.Role.StartsWith("Face.", StringComparison.Ordinal)).ToList() : faces, p, normal, normal == null ? 1.0 : 0.3);
            if (face == null || FrameOf(m, q.Services, host, face.Role) is not { } f) return null;
            var regions = Regions(m, f, host.Id);
            var at = OnFace(f, p, a["point"] is JsonArray { Count: 2 });
            var k = RegionAt(regions, at);
            var region = k >= 0 ? regions[k].Loop : f.Outline;
            var owner = k >= 0 ? Owners(regions, OnFace(m, host.Id, f.Face.Role))[k] : null;
            var o = new JsonObject
            {
                ["role"] = face.Role, ["label"] = face.Label, ["normal"] = new JsonArray(f.N.X, f.N.Y, f.N.Z),
                // Lit on the outside of what is painted there, so it shows over the zone's body.
                ["outline"] = new JsonArray(region.Select(x => f.At(x, owner == null ? 0 : FinishOf(m, owner).Layers.Sum(l => l.Thickness))).Select(v => (JsonNode)new JsonArray(v.X, v.Y, v.Z)).ToArray()),
                ["face"] = new JsonArray(f.Outline.Select(x => f.At(x)).Select(v => (JsonNode)new JsonArray(v.X, v.Y, v.Z)).ToArray()),
                ["regions"] = regions.Count,
            };
            if (owner != null)
            {
                var (name, layers) = FinishOf(m, owner);
                o["zoneId"] = owner.Id;
                o["finish"] = name;
                o["bare"] = owner.Bool("bare");
                if (owner.Str("finishType") is { } t) o["finishType"] = t;
                if (owner.Str("part") is { } part) o["part"] = part;
                o["layers"] = new JsonArray(layers.Select(l => (JsonNode)new JsonObject { ["material"] = l.Material, ["thickness"] = l.Thickness }).ToArray());
            }
            return o;
        });
        // What the painter offers: the project's room finishes by part, its surface finishes, the library's not yet in it.
        r.Query("finishPalette", (q, _) =>
        {
            var m = q.Model;
            static JsonArray Layers(IEnumerable<(string M, double T)> l) => new(l.Select(x => (JsonNode)new JsonObject { ["material"] = x.M, ["thickness"] = x.T }).ToArray());
            var o = new JsonArray();
            foreach (var t in m.OfClass(TypeClass).OrderBy(t => t.Name, StringComparer.CurrentCulture))
                if (SurfaceFinishSpec.Read(t.Attrs["spec"]) is { } s)
                    o.Add((JsonNode)new JsonObject { ["key"] = t.Id, ["name"] = t.Name, ["kind"] = "surface", ["typeId"] = t.Id, ["use"] = s.Use, ["layers"] = Layers(s.Layers.Select(l => (l.Material, l.Thickness))) });
            foreach (var t in m.OfClass(RoomFinishes.TypeClass).OrderBy(t => t.Name, StringComparer.CurrentCulture))
                if (RoomFinishes.SpecOf(m, t) is { } spec)
                    foreach (var (part, layers, use) in new[] { ("walls", spec.Walls, "walls"), ("floor", spec.Floor?.AsEnumerable().Reverse().ToList(), "floors"), ("ceiling", spec.Ceiling, "ceilings") })
                        if (layers is { Count: > 0 })
                            o.Add((JsonNode)new JsonObject { ["key"] = $"{t.Id}|{part}", ["name"] = $"{t.Name} · {part}", ["kind"] = "room", ["typeId"] = t.Id, ["part"] = part, ["use"] = use, ["layers"] = Layers(layers.Select(l => (l.Material, l.Thickness))) });
            foreach (var s in SurfaceFinishSpec.Library.Where(s => !m.OfClass(TypeClass).Any(t => string.Equals(t.Name, s.Name, StringComparison.OrdinalIgnoreCase))))
                o.Add((JsonNode)new JsonObject { ["key"] = $"library|{s.Name}", ["name"] = s.Name, ["kind"] = "surface", ["library"] = true, ["use"] = s.Use, ["layers"] = Layers(s.Layers.Select(l => (l.Material, l.Thickness))) });
            return o;
        });
    }

    static Entity CreateType(ICommandContext ctx, SurfaceFinishSpec spec)
    {
        foreach (var l in spec.Layers) EnsureMaterial(ctx, l.Material);
        var type = new Entity(ctx.NewId(), TypeClass, spec.Name, new JsonObject { ["spec"] = spec.ToJson(), ["exportAs"] = "IfcCoveringType" });
        ctx.Add(type);
        return type;
    }

    /// <summary>A material in the project, a new one made a finish (an insulation by its name).</summary>
    static void EnsureMaterial(ICommandContext ctx, string name)
    {
        if (Materials.Find(ctx.Model, name) != null) return;
        var made = Materials.Ensure(ctx, name);
        var attrs = made.CloneAttrs();
        attrs["category"] = name.Contains("insulation", StringComparison.OrdinalIgnoreCase) || name.Contains("wool", StringComparison.OrdinalIgnoreCase) ? "Insulation" : "Finish";
        ctx.Replace(made.With(attrs: attrs));
    }

    /// <summary>The finish a stroke paints with, as a zone's attributes: a type (and a room finish's part), or layers of its own.</summary>
    static JsonObject FinishArgs(ICommandContext ctx, JsonObject a, Frame f)
    {
        var o = new JsonObject();
        if ((string?)a["finishType"] is { } tid)
        {
            var type = ctx.Model.Get(tid) ?? throw new CommandException("The finish is not in the project.");
            if (type.Class == TypeClass) o["finishType"] = tid;
            else if (type.Class == RoomFinishes.TypeClass)
            {
                var part = (string?)a["part"] ?? PartFor(f);
                if (!RoomFinishes.PartNames.Contains(part)) throw new CommandException("A room finish's part is floor, walls or ceiling.");
                o["finishType"] = tid;
                o["part"] = part;
            }
            else throw new CommandException("Paint with a surface finish or a room finish.");
        }
        else if (a["layers"] is JsonArray layers)
        {
            var l = ReadLayers(layers);
            if (l.Count == 0) throw new CommandException("Paint with a layer at least.");
            foreach (var x in l) EnsureMaterial(ctx, x.Material);
            o["layers"] = new JsonArray(l.Select(x => (JsonNode)new JsonObject { ["material"] = x.Material, ["thickness"] = x.Thickness }).ToArray());
            if ((string?)a["name"] is { Length: > 0 } name) o["finishName"] = name;
        }
        else throw new CommandException("Choose a finish to paint with.");
        return o;
    }

    /// <summary>
    /// A zone painted on the face: the region holding <paramref name="seed"/> (the seeded zones already there go), or the whole
    /// face (null: every zone of the face goes; with <paramref name="keepRegions"/>, the regions painted stay and it takes the rest).
    /// </summary>
    static Entity Paint(ICommandContext ctx, Entity host, Frame f, Vec2? seed, JsonObject finish, bool keepRegions = false)
    {
        var m = ctx.Model;
        var zones = OnFace(m, host.Id, f.Face.Role);
        if (seed is { } s)
        {
            var regions = Regions(m, f, host.Id);
            var k = RegionAt(regions, s);
            if (k < 0) throw new CommandException("That point is off the face.");
            foreach (var z in zones.Where(z => Seed(z) is { } zs && RegionAt(regions, zs) == k)) ctx.Remove(z.Id);
        }
        else foreach (var z in zones.Where(z => !keepRegions || Seed(z) == null)) ctx.Remove(z.Id);
        var attrs = finish;
        attrs["hostId"] = host.Id;
        attrs["face"] = f.Face.Role;
        if (seed is { } at) attrs["seed"] = new JsonArray(Math.Round(at.X, 6), Math.Round(at.Y, 6));
        attrs["order"] = m.OfClass(Class).Select(z => z.Num("order")).DefaultIfEmpty(0).Max() + 1;
        attrs["exportAs"] = "IfcCovering";
        attrs["covers"] = host.Id;
        var predefined = f.Upright ? "CLADDING" : f.N.Z > 0 ? "FLOORING" : "CEILING";
        var probe = new Entity("", Class, "", attrs);
        var name = FinishOf(m, probe).Name;
        var zone = new Entity(ctx.NewId(), Class, $"{host.Name} · {f.Face.Label} · {name}", attrs, predefined);
        ctx.Add(zone);
        if (Spatial.StoreyOf(m, host.Id) is { } storey) Spatial.Contain(ctx, storey.Id, zone.Id);
        return zone;
    }

    /// <summary>A host deleted takes its zones and its splits with it, in the same step.</summary>
    sealed class HostGone : ICommandFollowUp
    {
        public JsonNode? After(ICommandContext ctx, IEdit edit, string command, JsonObject args, JsonNode? result)
        {
            var gone = edit.EntityChanges.Where(c => c.Before != null && c.After == null && c.Before.Class is not (Class or SplitClass)).Select(c => c.Id).ToHashSet();
            if (gone.Count == 0) return result;
            foreach (var e in ctx.Model.Entities.Where(x => x.Class is Class or SplitClass && x.Str("hostId") is { } h && gone.Contains(h)).ToList())
                ctx.Remove(e.Id);
            return result;
        }
    }
}

/// <summary>The surface finishes schedule (forum topic 83): each zone's layers by host, face and material, net of openings; the totals by material.</summary>
public sealed class FinishZoneSchedule : IScheduleSource
{
    public string Kind => "finishZones";
    public string Label => "Surface finishes";

    public ScheduleGrid Grid(IQueryContext q)
    {
        static string N(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);
        var m = q.Model;
        var rows = new List<IReadOnlyList<string>>();
        var keys = new List<string?>();
        var all = new List<(string Material, double Thickness, double Net, double Volume)>();
        foreach (var z in m.OfClass(FinishZones.Class).OrderBy(z => m.Get(z.Str("hostId") ?? "")?.Name ?? "", StringComparer.CurrentCulture).ThenBy(z => z.Num("order")))
        {
            var host = m.Get(z.Str("hostId") ?? "");
            var (finish, _) = FinishZones.FinishOf(m, z);
            foreach (var x in FinishZones.Measures(q.Geometry(z.Id)))
            {
                rows.Add([host?.Name ?? "", z.Str("face") ?? "", finish, x.Material, N(x.Thickness * 1000, "0.#"), N(x.Gross), N(x.Net), N(x.Volume, "0.000")]);
                keys.Add(z.Id);
                all.Add((x.Material, x.Thickness, x.Net, x.Volume));
            }
        }
        foreach (var t in all.GroupBy(x => (x.Material, Math.Round(x.Thickness, 6))).OrderBy(g => g.Key.Material, StringComparer.CurrentCulture))
        {
            rows.Add(["Total", "", "", t.Key.Material, N(t.Key.Item2 * 1000, "0.#"), "", N(t.Sum(x => x.Net)), N(t.Sum(x => x.Volume), "0.000")]);
            keys.Add(null);
        }
        return new ScheduleGrid(Label, ["Element", "Face", "Finish", "Material", "Thickness (mm)", "Gross (m²)", "Net (m²)", "Volume (m³)"], rows, keys);
    }
}
