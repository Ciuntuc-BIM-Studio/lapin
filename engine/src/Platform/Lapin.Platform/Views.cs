using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The project's views (part of core): plans of a storey cut at a height with
/// a view range, sections along a line, elevations from a side, each with its
/// scale, hidden lines and graphics overrides. A view is a <c>LapinView</c>
/// entity (not exported to IFC), so it undoes and saves with the model. A
/// storey without a plan of its own still has one: <c>plan:&lt;storey id&gt;</c>.
/// Its drawing is generated on demand from the elements' geometry.
/// </summary>
public static class Views
{
    public const string Class = "LapinView";
    public static readonly string[] Kinds = ["plan", "section", "elevation", "3d", RefPlanes.Kind];
    /// <summary>The disciplines the browser's default sets group by, the first a view's when it names none; a view or sheet may name any other. The product may say others (<see cref="ProductManifest.Disciplines"/>).</summary>
    public static string[] Disciplines { get; private set; } = ["Architecture", "Structure", "MEP"];

    /// <summary>The product's own disciplines, said once by the host that runs it (one product per engine process).</summary>
    public static void UseProduct(ProductManifest product)
    {
        if (product.Disciplines is { Length: > 0 } own) Disciplines = own;
    }
    public static readonly string[] Directions = ["north", "south", "east", "west"];
    public const double DefaultCut = 1.2;

    /// <summary>Hidden lines by kind: shown in plans and sections, left out of elevations; the project's default over that, the view's over both.</summary>
    public static bool ShowsHidden(Entity? project, string kind, string? own)
    {
        if (own is "show" or "hide") return own == "show";
        var p = (string?)project?.Attrs["viewDefaults"]?["hiddenLines"]?[kind];
        if (p is "show" or "hide") return p == "show";
        return kind != "elevation";
    }

    static IReadOnlyList<double> Levels(IModelView m) => Spatial.Storeys(m).Select(s => s.Num("elevation")).ToList();

    /// <summary>A view by id, or the implicit plan of a storey.</summary>
    public static Entity? Find(IModelView m, string id)
    {
        if (m.Get(id) is { Class: Class } v) return v;
        if (id == GraphicsSets.View3d) return GraphicsSets.As3dView(m);
        if (id.StartsWith("plan:", StringComparison.Ordinal) && m.Get(id[5..]) is { Class: IfcClass.BuildingStorey } storey)
            return new Entity(id, Class, storey.Name, new JsonObject { ["kind"] = "plan", ["storeyId"] = storey.Id, ["cutHeight"] = DefaultCut, ["scale"] = 100 });
        return null;
    }

    public static ResolvedRange Range(IModelView m, Entity view)
    {
        var storey = m.Get(view.Str("storeyId") ?? "");
        return ViewRanges.Resolve(storey?.Num("elevation") ?? 0, view.NumOrNull("cutHeight") ?? DefaultCut, ViewRange.From(view.Attrs["viewRange"]), Levels(m));
    }

    static (Vec3 Min, Vec3 Max)? Bounds(IEnumerable<DrawPart> parts)
    {
        var bs = parts.Select(p => Measure.Bounds(p.Solid)).OfType<(Vec3 Min, Vec3 Max)>().ToList();
        if (bs.Count == 0) return null;
        return (new Vec3(bs.Min(b => b.Min.X), bs.Min(b => b.Min.Y), bs.Min(b => b.Min.Z)), new Vec3(bs.Max(b => b.Max.X), bs.Max(b => b.Max.Y), bs.Max(b => b.Max.Z)));
    }

    /// <summary>The view's spec: where it looks from and how deep; null with a reason when it cannot be drawn.</summary>
    public static (ViewSpec? Spec, string? Problem) Spec(IModelView m, Entity view, IReadOnlyList<DrawPart> parts, IServices? services = null)
    {
        var project = Spatial.Project(m);
        var kind = view.Str("kind") ?? "plan";
        var hidden = ShowsHidden(project, kind, view.Str("hiddenLines"));
        switch (kind)
        {
            case "plan":
            {
                if (m.Get(view.Str("storeyId") ?? "") is not { Class: IfcClass.BuildingStorey }) return (null, "The plan's storey is gone.");
                // A site plan: cut over the highest point, so everything is seen from above, and no hidden lines.
                if (SitePlans.Of(view) != null) return (ViewSpec.Plan((Bounds(parts)?.Max.Z ?? 0) + 1, new RangeBands(ViewRanges.Unlimited, 0, null), false), null);
                var r = Range(m, view);
                var bands = ViewRanges.Bands(r);
                // A view style may leave out what lies above the cut (its overhead).
                var up = view.Str("look") == "up";
                if (!up && !ViewStyles.Of(m, view).Overhead) bands = bands with { Above = 0 };
                return (ViewSpec.Plan(r.Cut, bands, hidden, up), null);
            }
            case "section":
            {
                // Across an element at a station: the plane perpendicular to its axis, the window round its section.
                if (AcrossSections.Is(view)) return AcrossSections.Spec(m, view, parts, services, hidden);
                // The first piece of its line; a developed section (more pieces) is drawn piece by piece (Generate).
                var (path, _, problem) = SectionPaths.Resolve(m, services, view);
                if (path == null) return (null, problem);
                var pieces = SectionPaths.Pieces(path);
                if (pieces.Count == 0) return (null, "A section needs two different points.");
                return (ViewSpec.Section(pieces[0].A, pieces[0].B, view.NumOrNull("depth") ?? ViewRanges.Unlimited, hidden, spanned: pieces.Count > 1), null);
            }
            case "elevation":
            {
                if (Bounds(parts) is not { } bounds)
                {
                    // An elevation with its own plane draws on it even with nothing to see yet.
                    if (view.NumOrNull("originX") == null) return (null, "There is nothing to see.");
                    bounds = (new Vec3(-1, -1, 0), new Vec3(1, 1, 1));
                }
                return (ViewSpec.Elevation(LookOf(view), bounds, hidden, view.NumOrNull("originX")), null);
            }
            case RefPlanes.Kind:
                return (RefPlanes.Spec(view, hidden), null);
        }
        if (kind == "3d") return (null, "A 3D view is not a drawing.");
        return (null, $"„{kind}” is not a kind of view.");
    }

    /// <summary>The plan direction a section or an elevation looks along (a section looks left of its line, drawn from start to end; a developed one, of its first piece); null for a plan or a 3D view.</summary>
    public static Vec2? Look(Entity view)
    {
        switch (view.Str("kind"))
        {
            case "section":
                var pts = SectionPaths.Points(view);
                if (pts.Count < 2) return null;
                var along = pts[1] - pts[0];
                return along.Length < 1e-9 ? null : along.Normalized().Perp;
            case "elevation":
                return LookOf(view);
            default: return null;
        }
    }

    /// <summary>The plan direction an elevation looks along.</summary>
    static Vec2 LookOf(Entity view) => view.Str("direction") switch { "south" => new(0, 1), "east" => new(-1, 0), "west" => new(1, 0), _ => new(0, -1) };

    /// <summary>
    /// The plane an elevation's drawing lies on in the model, once it has one (<see cref="FixElevationPlane"/>): standing
    /// across its look, <c>planeDepth</c> along it from the model's origin, facing the viewer; its x measured from
    /// <c>originX</c>, its y the height. Null before: an elevation's drawing then moves with the model's bounds.
    /// </summary>
    public static WorkPlane? ElevationPlane(Entity view)
    {
        if (view.Str("kind") != "elevation" || view.NumOrNull("originX") is not { } ox) return null;
        var d = LookOf(view);
        var dir = new Vec3(d.X, d.Y, 0);
        var right = new Vec3(d.Y, -d.X, 0);
        return new WorkPlane(right * ox + dir * view.Num("planeDepth"), right, Vec3.UnitZ, -dir);
    }

    /// <summary>
    /// Gives an elevation a plane of its own where it is drawn now (its x origin where the model's bounds put it, so
    /// nothing drawn on it moves), its depth at the front of the model, or <paramref name="depth"/>. Returns the view after.
    /// </summary>
    public static Entity FixElevationPlane(ICommandContext ctx, Entity view, double? depth = null)
    {
        if (view.Str("kind") != "elevation") throw new CommandException("Only an elevation takes a sketch plane: a plan, a section and a reference plane have theirs.");
        var attrs = view.CloneAttrs();
        var d = LookOf(view);
        var dir = new Vec3(d.X, d.Y, 0);
        var right = new Vec3(d.Y, -d.X, 0);
        if (view.NumOrNull("originX") == null || (depth == null && view.NumOrNull("planeDepth") == null))
        {
            var (parts, _, _, _) = Gather(ctx.Query, view);
            var bounds = Bounds(parts);
            if (view.NumOrNull("originX") == null)
                attrs["originX"] = bounds is { } b ? Math.Round(Vec3.Dot(ViewSpec.Elevation(d, b).Frame.Origin, right), 9) : 0;
            // The front of the model as the elevation sees it: the nearest corner of its bounds along the look.
            depth ??= view.NumOrNull("planeDepth") ?? (bounds is { } bb
                ? new[] { bb.Min.X, bb.Max.X }.SelectMany(x => new[] { bb.Min.Y, bb.Max.Y }.Select(y => Vec3.Dot(new Vec3(x, y, 0), dir))).Min()
                : 0);
        }
        attrs["planeDepth"] = Math.Round(depth ?? view.Num("planeDepth"), 9);
        var after = view.With(attrs: attrs);
        ctx.Replace(after);
        return after;
    }

    /// <summary>A reference plane's fields from <c>view.create</c>'s arguments: three <c>points</c>, a <c>point</c> and its <c>normal</c> (a face picked), or a view's plane (<c>fromView</c>) and an <c>offset</c>.</summary>
    static JsonObject PlaneFrom(ICommandContext ctx, JsonObject a)
    {
        static Vec3? V(JsonNode? n) => n is JsonArray { Count: >= 3 } v && Json.Number(v[0]) is { } x && Json.Number(v[1]) is { } y && Json.Number(v[2]) is { } z ? new Vec3(x, y, z) : null;
        if (a["points"] is JsonArray pts)
        {
            var ps = pts.Select(V).ToList();
            if (ps.Count != 3 || ps.Any(p => p == null)) throw new CommandException("A plane through points takes three [x, y, z].");
            return RefPlanes.Through(ps[0]!.Value, ps[1]!.Value, ps[2]!.Value);
        }
        if (V(a["point"]) is { } at)
        {
            var n = V(a["normal"]) ?? throw new CommandException("A plane on a face needs the face's normal [x, y, z].");
            // On an element's named face: its normal, exactly, rather than the triangle's.
            if ((string?)a["elementId"] is { } eid && ctx.Model.Get(eid) is { } el && ElementFaces.At(ElementFaces.Of(ctx.Services, el, ctx.Model), at, n) is { IsEdge: false } face)
                n = face.Normal;
            return RefPlanes.OnFace(at, n);
        }
        if ((string?)a["fromView"] is { } vid)
        {
            var v = Find(ctx.Model, vid) ?? throw new CommandException("There is no such view.");
            var p = PlaneOf(ctx.Model, ctx.Services, v) ?? throw new CommandException(v.Str("kind") == "elevation"
                ? "The elevation has no sketch plane yet." : "That view lies on no plane (a 3D view).");
            return RefPlanes.Offset(p, Json.Number(a["offset"]) ?? 0);
        }
        throw new CommandException("A reference plane is made through three points, on a face (a point and its normal), or from a view's plane.");
    }

    /// <summary>
    /// The plane a view's drawing lies on in the model, what is drawn on it stands on: a plan's its storey's level (facing
    /// up), a section's its line's first piece (facing the viewer; <paramref name="atX"/>, a developed section's piece at that
    /// station, its x running on from the station), an elevation's once it has one, a reference plane's. Null for a 3D view.
    /// </summary>
    public static WorkPlane? PlaneOf(IModelView m, IServices? services, Entity view, double? atX = null)
    {
        switch (view.Str("kind") ?? "plan")
        {
            case "plan":
            {
                var storey = m.Get(view.Str("storeyId") ?? "");
                return new WorkPlane(new Vec3(0, 0, storey?.Num("elevation") ?? 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0), Vec3.UnitZ);
            }
            case "section":
            {
                var (path, _, _) = SectionPaths.Resolve(m, services, view);
                if (path == null) return null;
                var pieces = SectionPaths.Pieces(path);
                if (pieces.Count == 0) return null;
                var piece = atX is { } x ? pieces.LastOrDefault(p => p.Station <= x + 1e-9) ?? pieces[0] : pieces[0];
                var f = ViewSpec.Section(piece.A, piece.B, 1).Frame;
                return new WorkPlane(f.Origin - f.Right * piece.Station, f.Right, f.Up, -f.Dir);
            }
            case "elevation": return ElevationPlane(view);
            case RefPlanes.Kind: return RefPlanes.Of(view);
        }
        return null;
    }

    /// <summary>"name copy", then "name copy 2", "name copy 3"…, the first not taken by an entity of <paramref name="cls"/>.</summary>
    public static string CopyName(IModelView m, string name, string cls)
    {
        var taken = m.OfClass(cls).Select(e => e.Name).ToHashSet();
        var stem = System.Text.RegularExpressions.Regex.Replace(name, @" copy( \d+)?$", "");
        var candidate = stem + " copy";
        for (var n = 2; taken.Contains(candidate); n++) candidate = $"{stem} copy {n}";
        return candidate;
    }

    /// <summary>
    /// A 3D view's section box as a command gives it: {min: [x, y, z], max: [x, y, z], rot?} (metres; rot,
    /// radians, turns the box about the vertical through the world's origin: min and max are in its turned axes).
    /// </summary>
    public static JsonObject SectionBox(JsonNode? n)
    {
        const string Shape = "A section box is {min: [x, y, z], max: [x, y, z], rot}.";
        if (n is not JsonObject o || o["min"] is not JsonArray lo || o["max"] is not JsonArray hi || lo.Count < 3 || hi.Count < 3) throw new CommandException(Shape);
        double At(JsonArray v, int k) => Json.Number(v[k]) is { } d && double.IsFinite(d) ? d : throw new CommandException(Shape);
        var min = new double[3];
        var max = new double[3];
        for (var k = 0; k < 3; k++)
        {
            double p = At(lo, k), q = At(hi, k);
            min[k] = Math.Min(p, q);
            max[k] = Math.Max(Math.Max(p, q), Math.Min(p, q) + 0.05);
        }
        var box = new JsonObject { ["min"] = new JsonArray(min[0], min[1], min[2]), ["max"] = new JsonArray(max[0], max[1], max[2]) };
        if (Json.Number(o["rot"]) is { } rot && double.IsFinite(rot) && Math.Abs(rot) > 1e-9) box["rot"] = Math.IEEERemainder(rot, 2 * Math.PI);
        return box;
    }

    /// <summary>A 3D view's camera as a command gives it: {azimuth, pitch, distance, target: [x, y, z], ortho?} (radians, metres).</summary>
    public static JsonObject Camera(JsonNode? n)
    {
        if (n is not JsonObject o) throw new CommandException("A camera is {azimuth, pitch, distance, target: [x, y, z], ortho}.");
        double Num(string k) => Json.Number(o[k]) ?? throw new CommandException($"The camera needs its {k}.");
        if (o["target"] is not JsonArray t || t.Count < 3) throw new CommandException("The camera needs its target [x, y, z].");
        var camera = new JsonObject
        {
            ["azimuth"] = Num("azimuth"), ["pitch"] = Num("pitch"), ["distance"] = Math.Max(0.01, Num("distance")),
            ["target"] = new JsonArray(Json.Number(t[0]) ?? 0, Json.Number(t[1]) ?? 0, Json.Number(t[2]) ?? 0),
        };
        if (o["ortho"] is JsonValue ov && ov.TryGetValue<bool>(out var ortho)) camera["ortho"] = ortho;
        return camera;
    }

    /// <summary>
    /// What a plan shows of the other storeys (as ArchiCAD's "show on
    /// stories"): its own storey's elements only ("home", the default), those
    /// of the storeys listed besides ("some"), or every storey's ("all");
    /// <see cref="Halftone"/>: the other storeys' elements draw grey. What
    /// stands on no storey (the site's) is always shown. A plan view keeps it
    /// in <c>storeys</c>; it is drawn, so it prints on sheets as on screen.
    /// </summary>
    public sealed record StoreyDisplay(string Mode, IReadOnlySet<string> Storeys, bool Halftone)
    {
        public static readonly StoreyDisplay Home = new("home", new HashSet<string>(), false);
        public static readonly string[] Modes = ["home", "some", "all"];

        public static StoreyDisplay Of(Entity view)
        {
            // A site plan shows every storey as it is.
            if (SitePlans.Of(view) != null) return new StoreyDisplay("all", new HashSet<string>(), false);
            if (view.Attrs["storeys"] is not JsonObject o) return Home;
            var mode = (string?)o["mode"] is { } md && Modes.Contains(md) ? md : "home";
            var ids = (o["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
            var halftone = o["halftone"] is JsonValue hv && hv.TryGetValue<bool>(out var h) && h;
            return new StoreyDisplay(mode, ids, halftone);
        }

        public JsonObject ToJson() => new()
        {
            ["mode"] = Mode, ["ids"] = new JsonArray(Storeys.Order(StringComparer.Ordinal).Select(x => (JsonNode)x).ToArray()), ["halftone"] = Halftone,
        };

        /// <summary>How a plan of <paramref name="home"/> shows an element on <paramref name="storey"/> (null: on none): null hidden, false as it is, true halftone.</summary>
        public bool? Shows(string? home, string? storey)
        {
            if (storey == null || storey == home) return false;
            var shown = Mode switch { "all" => true, "some" => Storeys.Contains(storey), _ => false };
            return shown ? Halftone : null;
        }
    }

    /// <summary>
    /// Which storeys a 3D view shows (report #29): every storey ("all", the
    /// default), those listed ("some"), the active storey alone ("active") or
    /// the active storey and those below it ("below", a building cut at a
    /// level). The active storey is the interface's, so the view follows it.
    /// <see cref="Unplaced"/>: what stands on no storey (the site, the terrain,
    /// links) shows too. <see cref="Cut"/>: the model cut above the highest
    /// storey shown, that far over its level (1.20 m: like a plan), or not cut.
    /// A saved 3D view keeps it in <c>storeys</c>; {3D} in the project's <c>view3d</c>.
    /// </summary>
    public sealed record Storeys3d(string Mode, IReadOnlySet<string> Storeys, bool Unplaced, double? Cut)
    {
        public static readonly Storeys3d All = new("all", new HashSet<string>(), true, null);
        public static readonly string[] Modes = ["all", "some", "active", "below"];

        public static Storeys3d Of(Entity view)
        {
            if (view.Attrs["storeys"] is not JsonObject o) return All;
            var mode = (string?)o["mode"] is { } md && Modes.Contains(md) ? md : "all";
            var ids = (o["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
            var unplaced = !(o["unplaced"] is JsonValue uv && uv.TryGetValue<bool>(out var u) && !u);
            return new Storeys3d(mode, ids, unplaced, Json.Number(o["cut"]));
        }

        public bool IsDefault => Mode == "all" && Unplaced && Cut == null;

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["mode"] = Mode, ["ids"] = new JsonArray(Storeys.Order(StringComparer.Ordinal).Select(x => (JsonNode)x).ToArray()), ["unplaced"] = Unplaced };
            if (Cut is { } c) o["cut"] = c;
            return o;
        }
    }

    /// <summary>A 3D view's storeys shown set: {id ("3d" or a saved 3D view), mode?, ids?, unplaced?, cut? (metres; null: no cut)}.</summary>
    static void SetStoreys3d(ICommandContext ctx, JsonObject a, Entity view, Action<JsonObject> write)
    {
        var old = Storeys3d.Of(view);
        var mode = (string?)a["mode"] ?? old.Mode;
        if (!Storeys3d.Modes.Contains(mode)) throw new CommandException($"„{mode}” is not all, some, active or below.");
        var ids = a["ids"] is JsonArray arr ? arr.Select(n => (string?)n).OfType<string>().ToHashSet() : old.Storeys.ToHashSet();
        foreach (var id in ids) Spatial.Require(ctx.Model, id, IfcClass.BuildingStorey);
        if (mode == "some" && ids.Count == 0) throw new CommandException("Tick at least one storey.");
        var unplaced = a["unplaced"] is JsonValue uv && uv.TryGetValue<bool>(out var u) ? u : old.Unplaced;
        var cut = a.ContainsKey("cut") ? Json.Number(a["cut"]) : old.Cut;
        if (cut is { } c && (c < -5 || c > 50)) throw new CommandException("The cut is between −5 and 50 m above the storey's level.");
        var next = new Storeys3d(mode, ids, unplaced, cut);
        var attrs = view.CloneAttrs();
        if (next.IsDefault) attrs.Remove("storeys"); else attrs["storeys"] = next.ToJson();
        // {3D}'s settings live on the project, without a kind; a saved view keeps its own.
        if (view.Id == GraphicsSets.View3d) attrs.Remove("kind");
        write(attrs);
    }

    /// <summary>How a view shows an element by its storey (null: hidden; true: halftone); every element shows in a section, an elevation or a 3D view.</summary>
    public static bool? ShowsByStorey(IModelView m, Entity view, Entity element) =>
        view.Str("kind") is "plan" or null ? StoreyDisplay.Of(view).Shows(view.Str("storeyId"), Spatial.StoreyOf(m, element.Id)?.Id) : false;

    /// <summary>
    /// Every element's parts, and in a plan the symbols of the doors, windows and roofs of its storey; an element with its
    /// own 2D from an IFC file (<see cref="Plan2d"/>) draws that in a plan instead of its parts, unless the view's style says not.
    /// </summary>
    static (List<DrawPart> Parts, List<DrawSymbol> Symbols, List<DFill> Fills, List<DText> Texts) Gather(IQueryContext q, Entity view) =>
        Gather(q, view, null);

    /// <summary>As <see cref="Gather(IQueryContext, Entity)"/>, the silhouettes an elevation or a section draws added to <paramref name="silhouettes"/> (decision 341).</summary>
    static (List<DrawPart> Parts, List<DrawSymbol> Symbols, List<DFill> Fills, List<DText> Texts) Gather(IQueryContext q, Entity view, List<DrawSilhouette>? silhouettes)
    {
        var m = q.Model;
        var parts = new List<DrawPart>();
        var symbols = new List<DrawSymbol>();
        var fills = new List<DFill>();
        var texts = new List<DText>();
        var storey = view.Str("storeyId");
        var vg = VisibilityGraphics.Of(m, view);
        var hiddenElements = (view.Attrs["hiddenElements"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        var filters = q.Services.All<IViewElementFilter>();
        var partFilters = q.Services.All<IViewPartFilter>();
        bool PartShown(Entity e, string? key) => (partFilters.Count == 0 || !partFilters.Any(f => f.Hides(q, view, e, key))) && !vg.HidesPart(e, key);
        var plan = view.Str("kind") is "plan" or null;
        // A site plan: the model from above, what lies under the ground left out, the site's own symbols from every storey.
        var site = plan && SitePlans.Of(view) != null;
        var display = StoreyDisplay.Of(view);
        // The view's level of development (decision 404), never by its scale.
        var lod = ViewDetail.Of(m, view, vg);
        // How much a plan's drawn symbols draw (a track's sleepers, a plant's leaves): by the view's own level when it sets one,
        // else by the scale, as their legibility on paper asks.
        var detail = ViewDetail.Own(view, vg) is { } ownLod ? PlanDetail.SymbolsOf(ownLod) : PlanDetail.Of(view.NumOrNull("scale") ?? 100);
        // How much of the building's elements draws (decision 401): coarse, one outline hatched as the core; medium, the
        // layers; fine, the layers with the details of doors and windows, and the bars a linked structural model brings.
        var building = lod;
        var coreMaterials = new Dictionary<string, string?>();
        string? MaterialOf(Entity e, GeometryPart p) =>
            building <= PlanDetail.Lod100 && p.Key is { } k && k.StartsWith("layer:", StringComparison.Ordinal)
                ? (coreMaterials.TryGetValue(e.Id, out var cm) ? cm : coreMaterials[e.Id] = CoreMaterial(m, e)) ?? p.Material
                : p.Material;
        bool Detailed(Entity e, GeometryPart p) => PlanDetail.Shows(e.Class, p.Key, building);
        // At LOD 100 a steel member is a single line on its axis (decision 405), where the view sees it along its length.
        var sticks = building <= PlanDetail.Lod100 ? q.Services.All<IStickAxes>() : [];
        // Doors' and windows' plan symbols at the view's level (frame and sashes simplified at 200, the opening and swing at 100).
        var joinerySymbols = PlanDetail.SymbolsOf(building);
        ViewFrame? frame = null;
        // The view's style: which layers of layered elements draw (all, all but the finishes, the cores).
        var style = ViewStyles.Of(m, view);
        // People, cars and plants (decision 341): their symbols and silhouettes in the style's mode, thin.
        var entourage = style.Entourage;
        var thin = new LineStyleSpec(Pen: EntouragePen);
        var bandCache = new Dictionary<string, IReadOnlyList<LayerBand>?>();
        double? cutZ = plan && !site && style.Ifc2d && m.Get(storey ?? "") is { Class: IfcClass.BuildingStorey } ? Range(m, view).Cut : null;
        // Elements standing on several storeys (a lift's shaft): drawn by the plans of each storey they reach.
        IReadOnlyList<IStoreySpans> spans = plan ? q.Services.All<IStoreySpans>() : [];
        var planStorey = plan && storey != null && m.Get(storey) is { Class: IfcClass.BuildingStorey } ps ? ps : null;
        foreach (var e in m.Entities)
        {
            // Rooms draw as their fills and stamps (their module's marks), never as a body cut or seen.
            if (e.Class is IfcClass.OpeningElement or IfcClass.Space || hiddenElements.Contains(e.Id)) continue;
            var span = planStorey != null && spans.Count > 0 ? spans.FirstOrDefault(x => x.Handles(e)) : null;
            var spanned = span != null && span.On(m, e, planStorey!);
            // A plan shows its own storey's elements, and the other storeys' only as it says (StoreyDisplay).
            if (plan && !spanned && display.Shows(storey, Spatial.StoreyOf(m, e.Id)?.Id) == null) continue;
            if (filters.Select(f => f.For(q, view, e)).FirstOrDefault(x => x != null) is { Hide: true }) continue;
            if (vg.Hides(e)) continue;
            var stored = StoredBodies.Has(e);
            // A linked or imported model's bars: thousands of small cuts, drawn only in a fine view.
            if (stored && building < PlanDetail.Lod400 && ReferenceLayers.CategoryOf(e) == "rebar") continue;
            // Connections (steel nodes, fasteners) from LOD 350.
            if (building < PlanDetail.Lod350 && ReferenceLayers.CategoryOf(e) == "steelConnections") continue;
            // LOD 400 draws a member's real section, its root fillets and sloping flanges (decision 415); every lower level the light one.
            if ((building >= PlanDetail.Lod400 ? q.FineGeometry(e.Id) : q.Geometry(e.Id)) is not { } g) continue;
            var smooth = building >= PlanDetail.Lod400 && g.FineShape;
            if (cutZ is { } cz && stored && Plan2d.Read(e) is { Count: > 0 } own)
            {
                OwnPlan(e, g, own, cz, symbols, fills, texts);
                continue;
            }
            if (site)
            {
                // By its symbols when it has them (a stored terrain from an IFC file has only its body).
                var bySymbol = SiteSymbols.Contains(e.Class) && g.Plan is { Count: > 0 };
                foreach (var p in g.Parts)
                    if (!bySymbol && Measure.Bounds(p.Solid) is { } b && b.Max.Z > SitePlans.Underground && ViewStyles.Draws(m, style, e.Id, p.Key, bandCache) && PartShown(e, p.Key) && Detailed(e, p))
                        parts.Add(new DrawPart(p.Solid, e.Id, e.Class, p.Key, MaterialOf(e, p), CutInPlan: true) { Smooth = smooth });
                if (bySymbol)
                    foreach (var item in g.Plan!)
                    {
                        if (!item.At(detail) || !item.In(entourage)) continue;
                        if (SymbolFill(item, e) is { } sf) { fills.Add(sf); continue; }
                        var role = item.Kind switch { "symbol" or "contour" or "terrainEdge" => LineRole.Projection, "contourMajor" or "heavy" => LineRole.Cut, _ => null };
                        if (role != null) symbols.Add(new DrawSymbol(item.Points, item.Closed, e.Id, e.Class, role, item.Mode != null ? item.Style ?? thin : null));
                    }
                continue;
            }
            // An element with silhouettes (decision 341) draws them in an elevation or a section, unless the style draws the
            // entourage detailed (its body's edges, as before) or not at all; its parts still size the view.
            var bySilhouette = !plan && !stored && g.Silhouettes != null && entourage != "detailed";
            if (sticks.Count > 0 && sticks.Select(s => s.StickOf(m, e)).FirstOrDefault(x => x != null) is var (sa, sb) && Stick(e, sa, sb) is { } stick)
            {
                symbols.Add(stick);
                continue;
            }
            foreach (var p in g.Parts)
                if (ViewStyles.Draws(m, style, e.Id, p.Key, bandCache) && PartShown(e, p.Key) && Detailed(e, p)) parts.Add(new DrawPart(p.Solid, e.Id, e.Class, p.Key, MaterialOf(e, p), CutInPlan: stored || p.CutInPlan) { BySilhouette = bySilhouette, Smooth = smooth });
            if (bySilhouette && silhouettes != null)
                foreach (var sh in g.Silhouettes!)
                    if (sh.Mode == null || sh.Mode == entourage) silhouettes.Add(new DrawSilhouette(sh, e.Id, e.Class, style.EntourageFill, thin));
            var symbolsHere = spanned ? span!.PlanOn(m, e, planStorey!) ?? g.Plan : Spatial.StoreyOf(m, e.Id)?.Id is { } s && s == storey ? g.Plan : null;
            if (view.Str("kind") == "plan" && !stored && Generator.SymbolicInPlan.Contains(e.Class) && symbolsHere != null)
                foreach (var item in symbolsHere)
                {
                    if (!item.At(e.Class is IfcClass.Door or IfcClass.Window ? joinerySymbols : detail) || !item.In(entourage)) continue;
                    if (SymbolFill(item, e) is { } sf) { fills.Add(sf); continue; }
                    var role = item.Kind switch { "symbol" or "contour" or "terrainEdge" => LineRole.Projection, "contourMajor" => LineRole.Cut, "heavy" => LineRole.Cut, "dashed" or "reach" => LineRole.Hidden, "overhead" or "ridge" or "hip" or "valley" or "break" => LineRole.Overhead, _ => null };
                    if (role != null) symbols.Add(new DrawSymbol(item.Points, item.Closed, e.Id, e.Class, role, item.Mode != null ? item.Style ?? thin : item.Style));
                }
        }
        return (parts, symbols, fills, texts);

        // A member's axis as the view sees it (its role worked out by the drawing: cut, seen or overhead); none when it is seen end
        // on (a column in plan, a beam across a section), its body drawing then.
        DrawSymbol? Stick(Entity e, Vec3 a, Vec3 b)
        {
            frame ??= StickFrame(q, view);
            if (frame == null || (frame.Map(b) - frame.Map(a)).Length < 0.05) return null;
            return new DrawSymbol([], false, e.Id, e.Class, Generator.StickRole) { Points3 = [a, b] };
        }
    }


    /// <summary>Which way a view looks, for a stick seen end on: its spec's frame (an elevation with nothing yet, along its look).</summary>
    static ViewFrame? StickFrame(IQueryContext q, Entity view)
    {
        if (Spec(q.Model, view, [], q.Services).Spec is { } s) return s.Frame;
        return view.Str("kind") == "elevation" ? ViewSpec.Elevation(LookOf(view), (new Vec3(-1, -1, 0), new Vec3(1, 1, 1)), false, null).Frame : null;
    }

    /// <summary>The material a layered element's core is of (a native one's band, a reference's layer by its material's name), for a coarse view's one hatch.</summary>
    static string? CoreMaterial(IModelView m, Entity e)
    {
        if (LayerSets.BandsOf(m, e.Id) is { Count: > 0 } bands) return (bands.FirstOrDefault(b => b.Core) is { Core: true } c ? c : bands.OrderByDescending(b => Math.Abs(b.To - b.From)).First()).Material;
        return ReferenceLayers.Of(m, e) is { Layers.Count: > 0 } rs ? (rs.Layers.FirstOrDefault(l => l.Core) ?? rs.Layers.OrderByDescending(l => l.Thickness).First()).Material : null;
    }

    /// <summary>The pen people, cars and plants draw with unless the object styles say another (decision 341): pen 2, 0.13 mm at 1:100.</summary>
    public const int EntouragePen = 2;

    /// <summary>A symbol's solid area (a plant's dark core, a trunk's dot): its plan item of kind <c>fill</c>, in its style's colour, black by default.</summary>
    static DFill? SymbolFill(PlanItem item, Entity e) =>
        item.Kind == "fill" && item.Points.Count >= 3 ? new DFill([item.Points.ToArray()], e.Id, e.Class, item.Style?.Color ?? "#000000") : null;

    /// <summary>What a site plan draws by its symbols rather than its body: the terrain, paths, plants and the site's features (parcels, neighbours); the roofs and the rest by their bodies, seen from above.</summary>
    static readonly HashSet<string> SiteSymbols = SitePlans.SiteClasses;

    /// <summary>
    /// An element's own 2D in a plan (docs/spec-ifc.md, Reprezentările 2D): its lines with the file's colour and pattern, its
    /// fills and texts. A footprint's lines are cut lines where the element's body crosses the plan's cut (a wall's outline),
    /// seen lines otherwise; any other line (a door's swing, a fixture's symbol) is seen.
    /// </summary>
    static void OwnPlan(Entity e, ElementGeometry g, IReadOnlyList<Plan2dItem> items, double cutZ, List<DrawSymbol> symbols, List<DFill> fills, List<DText> texts)
    {
        double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
        foreach (var p in g.Parts) foreach (var v in p.Solid.Vertices) { lo = Math.Min(lo, v.Z); hi = Math.Max(hi, v.Z); }
        var crosses = lo < cutZ && hi > cutZ && !Generator.SymbolicInPlan.Contains(e.Class);
        foreach (var i in items)
            switch (i.Kind)
            {
                case Plan2dItem.Fill:
                    fills.Add(new DFill(i.Loops.Select(l => l.ToArray()).ToList(), e.Id, e.Class, i.Color));
                    break;
                case Plan2dItem.Label when i.Text != null:
                    texts.Add(new DText(i.Points[0], i.Text, i.Height, i.Angle, e.Id, e.Class, i.Color));
                    break;
                case Plan2dItem.Line when i.Points.Count >= 2:
                    var own = i.Color != null || i.Pattern != null ? new LineStyleSpec(Color: i.Color, Pattern: i.Pattern) : null;
                    symbols.Add(new DrawSymbol(i.Points, i.Closed, e.Id, e.Class, i.Footprint && crosses ? LineRole.Cut : LineRole.Projection, own));
                    break;
            }
    }

    /// <summary>The view's generated drawing (cut areas and lines, before materials and pens), or why there is none.</summary>
    public static (ViewDrawing? Drawing, ViewSpec? Spec, string? Problem) Generate(IQueryContext q, Entity view)
    {
        var (drawing, spec, problem, _, _) = GenerateWith(q, view);
        return (drawing, spec, problem);
    }

    /// <summary>As <see cref="Generate"/>, with the parts it drew and, laid in pieces (a developed section), each piece's spec, station and own drawing (before it was laid).</summary>
    static (ViewDrawing? Drawing, ViewSpec? Spec, string? Problem, List<DrawPart> Parts, List<(ViewSpec Spec, double Station, ViewDrawing Drawing)>? Pieces) GenerateWith(IQueryContext q, Entity view)
    {
        var silhouettes = new List<DrawSilhouette>();
        var (parts, symbols, fills, texts) = Gather(q, view, silhouettes);
        var (spec, problem) = Spec(q.Model, view, parts, q.Services);
        // What is drawn by its silhouettes only sizes the view: the surface hatches and shadows never see its body.
        var drawn = parts.Any(p => p.BySilhouette) ? parts.Where(p => !p.BySilhouette).ToList() : parts;
        if (spec == null) return (null, null, problem, drawn, null);
        var keys = new Dictionary<string, string?>();
        // Without the view style's joins, a surface goes on only within its own element.
        var joins = ViewStyles.Of(q.Model, view).Joins;
        string? Key(DrawPart p) => SeamKeyOf(q.Model, p.Material, p.Class, keys) is { } k ? (joins ? k : k + "\u0000" + p.Element) : null;
        ViewDrawing drawing;
        List<(ViewSpec, double, ViewDrawing)>? laid = null;
        if (spec.Kind == "section" && Pieces(q, view) is { Count: > 1 } pieces)
        {
            // A developed section: each piece a planar cut seen between its ends, laid at its station.
            laid = new(pieces.Count);
            foreach (var (piece, station) in pieces) laid.Add((piece, station, Generator.Draw(parts, piece, symbols, Key, silhouettes)));
            drawing = ViewDrawing.Join(laid.Select(p => p.Item3.Shifted(p.Item2)).ToList());
        }
        else drawing = Generator.Draw(parts, spec, symbols, Key, silhouettes);
        if (spec.Kind == "plan" && (fills.Count > 0 || texts.Count > 0)) drawing = drawing with { Fills = fills, Texts = texts };
        return (drawing, spec, null, drawn, laid);
    }

    /// <summary>A developed section's pieces: each straight piece's spec (seeing only between its ends) and the station it is laid at; one piece (or none) for a plain section.</summary>
    public static List<(ViewSpec Spec, double Station)> Pieces(IQueryContext q, Entity view)
    {
        var o = new List<(ViewSpec, double)>();
        if (view.Str("kind") != "section") return o;
        var (path, _, _) = SectionPaths.Resolve(q.Model, q.Services, view);
        if (path == null) return o;
        var pieces = SectionPaths.Pieces(path);
        var hidden = ShowsHidden(Spatial.Project(q.Model), "section", view.Str("hiddenLines"));
        var depth = view.NumOrNull("depth") ?? ViewRanges.Unlimited;
        foreach (var p in pieces) o.Add((ViewSpec.Section(p.A, p.B, depth, hidden, spanned: pieces.Count > 1), p.Station));
        return o;
    }

    /// <summary>
    /// What an element's part joins by where its surface goes on in one plane
    /// (<see cref="MaterialDrawing.SeamKey"/>): the same for the elevations'
    /// seams and the 3D view's. <paramref name="cache"/> keeps one answer per
    /// material and class for a drawing.
    /// </summary>
    public static string? SeamKeyOf(IModelView m, string? material, string ifcClass, Dictionary<string, string?> cache)
    {
        var k = material + "|" + ifcClass;
        if (cache.TryGetValue(k, out var key)) return key;
        return cache[k] = MaterialDrawing.SeamKey(material == null ? null : m.Get(material) is { } mat ? MaterialGraphics.Resolve(mat, "elevation") : null, ifcClass);
    }

    /// <summary>
    /// A view rendered: its pens (view coordinates, m; widths paper mm), annotations, bounds and scale, or why it cannot be drawn;
    /// where it looks from (its frame, so the drawing can be laid back into the model) and, for a section, how deep it sees (null: unlimited).
    /// </summary>
    public sealed record Rendered(List<Pen> Pens, List<ViewMark> Marks, (Vec2 Min, Vec2 Max)? Bounds, double Scale, string Kind, string? Problem, List<string> RangeProblems, List<string>? LineworkOrphans = null, ViewFrame? Frame = null, double? Depth = null, List<(ViewFrame Frame, double X0, double X1)>? Frames = null)
    {
        /// <summary>A section across an element: the window it sees (its span and rise), whose middle is the element's section; null for any other view.</summary>
        public (Vec2 Min, Vec2 Max)? Window { get; init; }
    }

    /// <summary>The mark layer of construction lines (drafted shapes whose role is draft): on screen, never printed.</summary>
    public const string ConstructionLayer = "CONSTRUCTION";

    /// <summary>A view's drawing with its annotations; for print (sheets, PDF, DXF) construction lines are left out.</summary>
    /// <param name="insets">Whether the views inset in this one are drawn (an inset does not draw its source's own insets).</param>
    /// <param name="withMarks">Whether the annotations' pens join the drawing's (an inset takes the drawing alone and lays the marks out itself); the marks are returned either way.</param>
    public static Rendered Render(IQueryContext q, Entity view, bool print = false, bool insets = true, bool withMarks = true)
    {
        var m = q.Model;
        var scale = view.NumOrNull("scale") ?? 100;
        var (generated, spec, problem, drawnParts, laid) = GenerateWith(q, view);
        if (generated == null || spec == null) return new Rendered([], [], null, scale, view.Str("kind") ?? "plan", problem, []);
        // The view's line edits (Linework) over its generated lines.
        var (drawing, lineworkOrphans) = Linework.Apply(generated, ViewLinework.Edits(view));
        var kind = spec.Kind == "plan" ? "plan" : spec.Kind == "elevation" ? "elevation" : "section";
        var resolved = new Dictionary<string, ResolvedMaterial?>();
        ResolvedMaterial? Resolved(string? id) => id == null ? null
            // A material named rather than kept (a track's ballast, its rails): its look guessed from the name.
            : resolved.TryGetValue(id, out var r) ? r : resolved[id] = m.Get(id) is { } mat ? MaterialGraphics.Resolve(mat, kind) : GlobalId.IsValid(id) ? null : MaterialGraphics.Resolve(null, kind, id);
        // Each element's layers, so a layer thinner than a membrane (or of a membrane's material) draws as one.
        var layerSets = new Dictionary<string, IReadOnlyList<LayerRef>?>();
        IReadOnlyList<LayerRef>? LayersOf(string element) => layerSets.TryGetValue(element, out var ls) ? ls
            : layerSets[element] = LayerSets.BandsOf(m, element)?.Select(b => new LayerRef(b.Material, b.Name, Math.Abs(b.To - b.From))).ToList();
        // An element's own hatch alignment over its materials' (decision 369): along its axis in a plan, or from the view's origin.
        var ownHatch = new Dictionary<string, ElementHatch?>();
        ElementHatch? OwnHatch(string element)
        {
            if (ownHatch.TryGetValue(element, out var h)) return h;
            h = null;
            if (m.Get(element) is { } e)
                switch (e.Str("hatchAlign"))
                {
                    case HatchAlign.View: h = new ElementHatch(0, null); break;
                    case HatchAlign.Axis when kind == "plan" && e.Attrs["start"] != null && e.Attrs["end"] != null:
                    {
                        var (a, b) = (Args.PointOf(e.Attrs["start"]), Args.PointOf(e.Attrs["end"]));
                        if ((b - a).Length < 1e-9) break;
                        var along = Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI;
                        // The axis's direction, not its sense: a wall drawn either way hatches alike.
                        if (along > 90 + 1e-9 || along <= -90 + 1e-9) along += along > 0 ? -180 : 180;
                        h = new ElementHatch(along, along == Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI ? a : b);
                        break;
                    }
                }
            return ownHatch[element] = h;
        }
        // A coarse view (decision 401): a layered element's layers one area, no membranes drawn between them.
        var coarse = ViewDetail.Of(m, view) <= PlanDetail.Lod100;
        var materials = MaterialDrawing.Apply(drawing, p => Resolved(p.Material), coarse ? null : LayersOf,
            l => l.Thickness < MaterialGraphics.MembraneThickness || Resolved(l.Material)?.Membrane == true, joins: ViewStyles.Of(m, view).Joins, elementHatch: OwnHatch, mergeOwn: coarse);
        // A cut left without a fill: the fill a module gives its elements (a Studio solid's white), asked once per element.
        if (q.Services.All<ICutFill>() is { Count: > 0 } cutFills)
        {
            var fillOf = new Dictionary<string, string?>();
            for (var i = 0; i < drawing.Cuts.Count; i++)
            {
                if (materials.HidePolygons.Contains(i) || materials.Fills.GetValueOrDefault(i) != null) continue;
                var el = drawing.Cuts[i].Element;
                if (!fillOf.TryGetValue(el, out var fill))
                    fillOf[el] = fill = m.Get(el) is { } e ? cutFills.Select(f => f.FillOf(m, e)).FirstOrDefault(x => x != null) : null;
                if (fill != null) materials.Fills[i] = fill;
            }
        }
        var project = Spatial.Project(m);
        var graphics = GraphicsSets.Of(m, view);
        var categories = (graphics?["categories"] as JsonObject)?.Select(c => (c.Key, G: CategoryGraphics.From(c.Value))).Where(x => x.G != null).ToDictionary(x => x.Key, x => x.G!);
        var table = LineStyles.ReadTable(project?.Attrs["penTable"]);
        // Elements a module draws in another colour (a phase's override): their lines and fills.
        var colors = new Dictionary<string, string>();
        var filters = q.Services.All<IViewElementFilter>();
        var halftoneOthers = spec.Kind == "plan" && StoreyDisplay.Of(view).Halftone;
        var vg = VisibilityGraphics.Of(m, view);
        // The view's filters' line styles (decision 394), per element, under the elements' own overrides.
        var elementLines = graphics?["elements"]?.DeepClone() as JsonObject;
        if (filters.Count > 0 || halftoneOthers || vg.HasOverrides)
            foreach (var id in drawing.Cuts.Select(c => c.Element).Concat(drawing.Lines.Select(l => l.Element)).Concat((drawing.Fills ?? []).Select(f => f.Element)).Concat((drawing.Texts ?? []).Select(t => t.Element)).Distinct())
            {
                if (m.Get(id) is not { } el) continue;
                if (filters.Select(f => f.For(q, view, el)).FirstOrDefault(x => x != null) is { Color: { } c }) colors[id] = c;
                // A filter's colour or halftone, a category's halftone, a link halftone in this view (decisions 391, 394).
                else if (vg.ColorOf(el) is { } vc) colors[id] = vc;
                // Another storey's element in a plan that shows it halftone: grey, as a halftone link.
                else if (halftoneOthers && ShowsByStorey(m, view, el) == true) colors[id] = Links.Halftone;
                if (vg.LinesOf(el) is { } lines && !(elementLines?.ContainsKey(id) ?? false)) (elementLines ??= [])[id] = lines;
            }
        var styles = LineStyles.For(project?.Attrs["objectStyles"] as JsonObject, graphics?["categories"] as JsonObject, elementLines, table, scale);
        var penOptions = new Pens.Options(scale, spec.Hidden, styles, categories, DraftingResources.Patterns(m), ElementColors: colors, Table: table);
        var pens = Pens.Of(drawing, materials, penOptions);
        // The faces the drawing sees, worked out once for its shadows and its surface hatches: a developed section's on each
        // piece (its own cut areas hiding what is behind them), the parts' faces gathered once for all of them.
        List<ViewFaces.Face>? allFaces = null;
        List<ViewFaces.Face> AllFaces() => allFaces ??= ViewFaces.FacesOf(drawnParts.Select((p, i) => (p.Solid, i)).ToList());
        SurfaceHatches.Piece PieceOf(ViewSpec s, double station, ViewDrawing d, int index)
        {
            ViewFaces? scene = null;
            return new SurfaceHatches.Piece(s, station, () => scene ??= ViewShadows.Scene(s, drawnParts, d, AllFaces()), index);
        }
        List<SurfaceHatches.Piece> scenes = laid == null ? [PieceOf(spec, 0, drawing, 0)] : laid.Select((p, i) => PieceOf(p.Spec, p.Station, p.Drawing, i)).ToList();
        // The faces' surface hatches (SurfaceHatches): under the lines, the hatches over their fills.
        var surfaces = SurfaceHatches.PensOf(m, view, scenes, drawnParts, penOptions, new Dictionary<string, string?>());
        // The sun's shadows on what the view sees (ViewShadows), filled under everything it draws: over the surfaces' fills, under their hatches.
        var shadows = ViewShadows.PenOf(m, view, scenes);
        pens.InsertRange(0, [.. surfaces.Where(p => p.Layer == SurfaceHatches.FillLayer), .. shadows != null ? [shadows] : Array.Empty<Pen>(), .. surfaces.Where(p => p.Layer != SurfaceHatches.FillLayer)]);

        // Annotations: the views' own marks, then each module's (grids, dimensions, texts), over the drawing.
        var ctx = new ViewContext(m, view, spec.Kind, spec.Frame, scale, DraftingResources.Book(m), drawing, q.Geometry) { Services = q.Services };
        var marks = Marks(ctx).Concat(Location.Marks(ctx)).Concat(SitePlans.Marks(ctx)).Concat(insets ? Insets.Marks(q, ctx) : []).ToList();
        // The reference planes where they cut this view, on the screen only: never on a sheet.
        if (!print) marks.AddRange(RefPlaneMarks(ctx));
        // The view's named boundaries (decision 236), on the screen only.
        if (!print) marks.AddRange(ViewBoundaries.Marks(ctx));
        foreach (var source in q.Services.All<IViewAnnotations>())
            try { marks.AddRange(source.Marks(ctx)); }
            catch (CommandException) { }
        // The openings' voids when the view's style draws them (decision 290): a coordination drawing's provisions for voids.
        if (ViewStyles.Of(m, view) is { Voids: true } voidStyle)
            try { marks.AddRange(Coordination.VoidDrawings.Marks(q, ctx, spec, Pieces(q, view), voidStyle.VoidSizes, styles, categories?.GetValueOrDefault(Coordination.VoidDrawings.Category), marks)); }
            catch (CommandException) { }
        // The clash report's open problems as red clouds with their numbers (decision 293): on the screen, printed only when asked.
        if (Coordination.ClashMarks.Shows(view, print))
            try { marks.AddRange(Coordination.ClashMarks.Marks(q, ctx, spec, Pieces(q, view))); }
            catch (CommandException) { }
        // Every element the drawing shows can be picked by what it drew: its cut outlines and its seen lines, as a
        // mark on the PICK layer (never drawn, only hit and highlighted). Hidden and overhead lines pick nothing.
        marks.AddRange(PickMarks(drawing));
        // What the view's Visibility / Graphics hides goes with its marks (decision 391): a hidden category's tags and bars, a link hidden here.
        marks.RemoveAll(vg.HidesMark);
        // The point clouds the view's cut takes, as dots under the drawing (decision 396; the view's Point clouds tab hides them, not its categories): a plan's or a section's slice, an elevation's front.
        if (laid == null)
            try { marks.AddRange(PointCloudSlices.Marks(ctx, spec, vg)); }
            catch (CommandException) { }
        // An annotation category's colour or halftone (decision 394).
        if (vg.Graphics?["annotations"] is JsonObject { Count: > 0 }) foreach (var mk in marks) vg.Recolor(mk);
        // The marks' pens: those drawn under the model (rooms' fills) go first, the others over it; fills before lines in each.
        // A drafted element given a place in the view's draw order (decision 339) draws in that order instead, in runs: a
        // run takes the next element until one with a fill or a picture comes after lines, so a fill covers what is under it.
        var underPens = new Dictionary<string, Pen>();
        var markPens = new Dictionary<string, Pen>();
        static void Into(Dictionary<string, Pen> into, ViewMark mk, string fillLayer, bool print)
        {
            foreach (var (loops, color) in mk.Fills)
            {
                var key = "FILL:" + color;
                if (!into.TryGetValue(key, out var pen)) into[key] = pen = new Pen(fillLayer, null, 0, color, null);
                pen.Loops.AddRange(loops);
            }
            foreach (var (la, lb, layer, width, color, dash) in mk.Lines)
            {
                if (layer == "PICK" || (print && layer == ConstructionLayer)) continue;
                var key = $"{layer}:{color}:{width}:{(dash == null ? "" : string.Join(" ", dash))}";
                if (!into.TryGetValue(key, out var pen)) into[key] = pen = new Pen(layer, color, width, null, dash);
                pen.Segments.Add((la, lb));
            }
            foreach (var (t, color) in mk.Texts)
            {
                var key = "TEXT:" + color;
                if (!into.TryGetValue(key, out var pen)) into[key] = pen = new Pen("TEXT", color, 0, null, null);
                pen.Texts.Add(t);
            }
        }
        double OrderOf(ViewMark mk) => !mk.Under && !mk.Pick && mk.Owner != null && m.Get(mk.Owner) is { } owner && DrawOrder.OnView(owner, view.Id) ? DrawOrder.Of(owner) : 0;
        var ordered = new List<(ViewMark Mark, double Order)>();
        var free = new List<ViewMark>();
        foreach (var mk in marks)
        {
            var o = OrderOf(mk);
            if (o != 0) ordered.Add((mk, o)); else free.Add(mk);
        }
        ordered = [.. ordered.OrderBy(x => x.Order)];
        var behindPens = new List<Pen>();
        var overPens = new List<Pen>();
        if (withMarks)
        {
            Dictionary<string, Pen>? run = null;
            var (inked, runBehind) = (false, false);
            void Close()
            {
                if (run == null) return;
                (runBehind ? behindPens : overPens).AddRange(run.Values.OrderBy(p => p.Images.Count > 0 ? 0 : p.Fill != null ? 1 : 2));
                run = null;
            }
            foreach (var (mk, order) in ordered)
            {
                var covers = mk.Fills.Count > 0 || mk.Images.Count > 0;
                if (run == null || runBehind != order < 0 || covers && inked) { Close(); run = []; (inked, runBehind) = (false, order < 0); }
                Into(run, mk, "DRAFT-FILL", print);
                if (mk.Images.Count > 0)
                {
                    if (!run.TryGetValue("IMAGE", out var pic)) run["IMAGE"] = pic = new Pen("IMAGE", null, 0, null, null);
                    pic.Images.AddRange(mk.Images);
                }
                inked |= mk.Lines.Any(l => l.Layer != "PICK") || mk.Texts.Count > 0;
            }
            Close();
        }
        foreach (var mk in withMarks ? free : [])
            Into(mk.Under ? underPens : markPens, mk, mk.Under ? "ROOM-FILL" : "DRAFT-FILL", print);
        pens.InsertRange(0, underPens.Values.OrderBy(p => p.Fill != null ? 0 : 1));
        // Drafting sent behind the model: over the rooms' fills, under the model's own pens.
        pens.InsertRange(underPens.Count, behindPens);
        // Pictures (an orthophoto) under all of it.
        if (withMarks && free.SelectMany(mk => mk.Images).ToList() is { Count: > 0 } images)
        {
            var pictures = new Pen("IMAGE", null, 0, null, null);
            pictures.Images.AddRange(images);
            pens.Insert(0, pictures);
        }
        pens.AddRange(overPens);
        pens.AddRange(markPens.Values.OrderBy(p => p.Fill != null ? 0 : 1));
        // What a click takes first: what is on top (the unnumbered marks, then the numbered ones from the top down, those behind the model last).
        marks = [.. free, .. Enumerable.Reverse(ordered).Select(x => x.Mark)];
        // What the view shows: the drawing and its annotations (a text counted by its anchor and height).
        var pts = new List<Vec2>();
        if (drawing.Bounds is { } b) pts.AddRange([b.Min, b.Max]);
        foreach (var mk in marks)
        {
            foreach (var l in mk.Lines) pts.AddRange([l.A, l.B]);
            foreach (var (t, _) in mk.Texts) pts.AddRange([t.At, t.At + new Vec2(0, t.Height * scale / 1000)]);
            // A point cloud's slice is what the view shows of it (an orthophoto is not counted: it reaches far past the model).
            foreach (var img in mk.Images) if (img.ImageId.StartsWith("pointcloud:", StringComparison.Ordinal)) pts.AddRange(img.Corners);
        }
        (Vec2, Vec2)? bounds = pts.Count > 0 ? (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y))) : null;
        double? depth = spec.Kind == "section" && spec.Below < ViewRanges.Unlimited ? spec.Below : null;
        // A developed section: where each piece lies in the model, and the stations (developed x) it covers.
        var frames = spec.Kind == "section" && Pieces(q, view) is { Count: > 1 } ps ? ps.Select(p => (p.Spec.Frame, p.Station, p.Station + p.Spec.Span!.Value.Max)).ToList() : null;
        return new Rendered(pens, marks, bounds, scale, spec.Kind, null, spec.Kind == "plan" ? ViewRanges.Problems(Range(m, view)) : [], lineworkOrphans, spec.Frame, depth, frames)
        {
            Window = spec.Span is { } sp && spec.Rise is { } rs ? (new Vec2(sp.Min, rs.Min), new Vec2(sp.Max, rs.Max)) : null,
        };
    }

    /// <summary>The view drawn, for the interface: its pens, annotations, bounds and problems.</summary>
    public static JsonObject Drawing(IQueryContext q, Entity view)
    {
        var r = Render(q, view);
        if (r.Problem != null) return new JsonObject { ["problem"] = r.Problem, ["pens"] = new JsonArray() };
        var book = DraftingResources.Book(q.Model);
        var o = new JsonObject
        {
            ["kind"] = r.Kind,
            ["scale"] = r.Scale,
            ["lineCap"] = book.LineCap,
            ["units"] = book.Units,
            ["pens"] = new JsonArray(r.Pens.Select(p => (JsonNode)p.ToJson()).ToArray()),
            // What a click on an annotation selects, and where its points snap.
            ["marks"] = new JsonArray(r.Marks.Where(x => x.Owner != null).Select(x => (JsonNode)new JsonObject
            {
                ["id"] = x.Owner,
                ["segments"] = new JsonArray(x.Lines.Select(l => (l.A, l.B)).Concat(x.Edges).SelectMany(l => new[] { l.A.X, l.A.Y, l.B.X, l.B.Y }).Select(v => (JsonNode)Math.Round(v, 5)).ToArray()),
                ["hit"] = x.Hit is { } hit ? new JsonObject
                {
                    ["solid"] = hit.Solid,
                    ["loops"] = new JsonArray(hit.Loops.Select(l => (JsonNode)new JsonArray(l.SelectMany(p => new[] { p.X, p.Y }).Select(v => (JsonNode)Math.Round(v, 5)).ToArray())).ToArray()),
                } : null,
                ["snaps"] = new JsonArray(x.Snaps.SelectMany(p => new[] { p.X, p.Y }).Select(v => (JsonNode)Math.Round(v, 5)).ToArray()),
                ["shapes"] = x.Shapes.Count > 0 ? new JsonArray(x.Shapes.Select(sh => (JsonNode)sh.DeepClone()).ToArray()) : null,
                ["data"] = x.Data?.DeepClone(),
                ["pick"] = x.Pick ? true : null,
                // Drafting sent behind the model (decision 339): a click takes the model's element there first.
                ["behind"] = x.Owner != null && q.Model.Get(x.Owner) is { } oe && DrawOrder.OnView(oe, view.Id) && DrawOrder.Of(oe) < 0 ? true : null,
            }).ToArray()),
        };
        if (r.Bounds is { } b) o["bounds"] = new JsonArray(b.Min.X, b.Min.Y, b.Max.X, b.Max.Y);
        if (r.Kind == "plan") o["problems"] = new JsonArray(r.RangeProblems.Select(x => (JsonNode)x).ToArray());
        if (r.LineworkOrphans is { Count: > 0 } lo) o["lineworkOrphans"] = new JsonArray(lo.Select(x => (JsonNode)x).ToArray());
        // Where the drawing lies in the model: a drawing point (x, y) is origin + right·x + up·y; dir looks into the scene (the 3D view's section box).
        if (r.Frame is { } fr)
        {
            static JsonArray V(Vec3 v) => new(Math.Round(v.X, 9), Math.Round(v.Y, 9), Math.Round(v.Z, 9));
            o["frame"] = new JsonObject { ["origin"] = V(fr.Origin), ["right"] = V(fr.Right), ["up"] = V(fr.Up), ["dir"] = V(fr.Dir) };
            if (r.Depth is { } dp) o["depth"] = dp;
            // A developed section: each piece's frame and the stretch of x (its stations) it covers; a drawing point (x, y) lies on the piece whose stretch holds x, at x − x0.
            if (r.Frames is { Count: > 1 } frames)
                o["frames"] = new JsonArray(frames.Select(f => (JsonNode)new JsonObject
                {
                    ["origin"] = V(f.Frame.Origin), ["right"] = V(f.Frame.Right), ["up"] = V(f.Frame.Up), ["dir"] = V(f.Frame.Dir), ["x0"] = Math.Round(f.X0, 6), ["x1"] = Math.Round(f.X1, 6),
                }).ToArray());
        }
        return o;
    }

    /// <summary>The systems a plan's DXF can be written in: the plan's own (its internal origin), the project base point's, the map's.</summary>
    public static readonly string[] DxfCoords = ["internal", "project", "map"];

    /// <summary>
    /// A view as DXF at real size, y up, its texts and dashes as high as on paper at its scale. In the plan's own
    /// coordinates (millimetres from the internal origin), the project base point's (millimetres from it), or, for
    /// a plan of a georeferenced project, the map's (decision 447): eastings and northings in metres through the
    /// survey point, turned to true north (<see cref="GeoRef.ToMap"/>), so the drawing lands on a cadastral DXF.
    /// </summary>
    public static byte[] Dxf(IQueryContext q, Entity view, string coords = "internal")
    {
        var r = Render(q, view);
        if (!DxfCoords.Contains(coords)) throw new CommandException($"A DXF is written in internal, project or map coordinates, not {coords}.");
        if (coords != "internal" && r.Kind != "plan") throw new CommandException("Only a plan is written in project or map coordinates: a section or elevation has no place on the map.");
        if (coords == "map" && Location.Survey(q.Model) == null) throw new CommandException("The project has no survey point: Manage → Location ties it to the map first.");
        var basePt = Location.Base(q.Model).At;
        var north = Location.Base(q.Model).NorthAngle ?? 0;
        // A model point (metres) in the file's units; an angle (degrees, anticlockwise) turned as the plan turns to the map.
        Func<Vec2, Vec2> place = coords switch
        {
            "map" => p => { var (e, n) = GeoRef.ToMap(q.Model, p); return new Vec2(e, n); },
            "project" => p => (p - basePt) * 1000,
            _ => p => p * 1000,
        };
        var unit = coords == "map" ? 1.0 : 1000.0;
        var turn = coords == "map" ? -north : 0;
        var pens = r.Pens.Select(p =>
        {
            var o = new Pen(p.Layer, p.Stroke, p.Width, p.Fill, p.Dash?.Select(v => v * r.Scale * unit / 1000).ToArray());
            o.Segments.AddRange(p.Segments.Select(s => (place(s.A), place(s.B))));
            o.Loops.AddRange(p.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(place).ToList()));
            o.Texts.AddRange(p.Texts.Select(t => t with { At = place(t.At), Height = t.Height * r.Scale * unit / 1000, Angle = t.Angle + turn }));
            return o;
        }).ToList();
        // DxfWriter.Page flips y on a page; a model drawing is y up already: give it a page of height 0 mirrored back.
        var flipped = pens.Select(p =>
        {
            var o = new Pen(p.Layer, p.Stroke, p.Width, p.Fill, p.Dash);
            o.Segments.AddRange(p.Segments.Select(s => (new Vec2(s.A.X, -s.A.Y), new Vec2(s.B.X, -s.B.Y))));
            o.Loops.AddRange(p.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(v => new Vec2(v.X, -v.Y)).ToList()));
            o.Texts.AddRange(p.Texts.Select(t => t with { At = new Vec2(t.At.X, -t.At.Y) }));
            return o;
        }).ToList();
        var where = coords switch
        {
            "map" => $" · {Location.Crs(q.Model) ?? "map"} coordinates, metres",
            "project" => " · from the project base point, mm",
            _ => "",
        };
        return Lapin.Drawing.Export.DxfWriter.Page(new Lapin.Drawing.Export.PaperPage(0, 0, flipped), $"Lapin · {view.Name} · 1:{r.Scale:0}{where}", coords == "map" ? 6 : 4);
    }

    /// <summary>
    /// The views' own marks: on a plan, every section's line (a thin chain line,
    /// a heavy stroke at either end, an arrow at either end looking the way the
    /// section looks, its name beyond); on a section or elevation, the levels
    /// (a fine line across, a marker and the level's name and elevation).
    /// Sized from the view's scale, so they read the same on paper.
    /// </summary>
    static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        var m = c.Model;
        var text = Standards.ResolveText(c.Book, []);
        var k = c.Scale / 100;
        if (c.Kind == "plan")
        {
            foreach (var v in m.OfClass(Class).Where(v => v.Str("kind") == "section" && SectionMarkers.Shows(c.View, v)))
            {
                // Its line (a jogged or curved one as drawn, an alignment as it is now), the strokes and arrows at its ends, each arrow looking left of its piece.
                var (path, _, _) = SectionPaths.Resolve(m, c.Services, v);
                if (path == null) continue;
                var pieces = SectionPaths.Pieces(path);
                if (pieces.Count == 0) continue;
                var (first, last) = (pieces[0], pieces[^1]);
                var mk = new ViewMark(v.Id);
                var (arrow, head) = (0.7 * k, 0.25 * k);
                mk.Polyline(path, false, "SECTION", Pens.Default.Hatch, dash: LineStyles.DashOf("center"));
                var u0 = (first.B - first.A) * (1 / first.Length);
                var u1 = (last.B - last.A) * (1 / last.Length);
                mk.Line(first.A, first.A + u0 * Math.Min(0.8 * k, first.Length / 3), "SECTION", Pens.Default.Cut).Line(last.B, last.B - u1 * Math.Min(0.8 * k, last.Length / 3), "SECTION", Pens.Default.Cut);
                foreach (var (end, look) in new[] { (first.A, u0.Perp), (last.B, u1.Perp) })
                {
                    var tip = end + look * arrow;
                    mk.Line(end, tip, "SECTION", Pens.Default.Seen)
                      .Line(tip, tip - look * head + look.Perp * (head * 0.5), "SECTION", Pens.Default.Seen)
                      .Line(tip, tip - look * head - look.Perp * (head * 0.5), "SECTION", Pens.Default.Seen)
                      .Text(new PenText(tip + look * (0.35 * k), v.Name, text.Height, Baseline: "middle", Font: text.Font));
                }
                mk.Snaps.AddRange(path);
                yield return mk;
            }
        }
        else if (c.Drawing.Bounds is { } bounds && c.View.Str("kind") != RefPlanes.Kind)
        {
            if (c.Kind == "section" && SectionMarks(c, bounds, k, text) is { } section) yield return section;
            // The other sections' markers where their planes cut this view (decision 223).
            foreach (var trace in SectionMarkers.Traces(c)) yield return trace;
            var (x0, x1) = (bounds.Min.X - 1.0 * k, bounds.Max.X + 1.5 * k);
            // The levels across a drawing whose up is the model's (not on a level or a leaning work plane).
            foreach (var storey in Math.Abs(c.Frame.Up.Z) > 0.999 ? Spatial.Storeys(m) : [])
            {
                var z = storey.Num("elevation");
                var y = c.Frame.Map(new Vec3(c.Frame.Origin.X, c.Frame.Origin.Y, z)).Y;
                // A section across an element is a window: only the levels within it, and none on a horizontal cut.
                if (AcrossSections.Is(c.View) && (Math.Abs(c.Frame.Dir.Z) > 0.5 || y < bounds.Min.Y - 1.0 * k || y > bounds.Max.Y + 1.0 * k)) continue;
                var mk = new ViewMark(storey.Id);
                mk.Line(new Vec2(x0, y), new Vec2(x1, y), "LEVEL", Pens.Default.Hidden, "#555555", LineStyles.DashOf("center"));
                // The marker standing on the line (its style and size the project's: an open triangle, one half black as the
                // Romanian standard has it, a black one, or a tick), the level's name and elevation beside it.
                var (style, size) = LevelMark(Spatial.Project(m));
                var t = size * c.Mm;
                var w = t / Math.Sqrt(3);
                var (apex, left, right, mid) = (new Vec2(x1, y), new Vec2(x1 - w, y + t), new Vec2(x1 + w, y + t), new Vec2(x1, y + t));
                const string ink = "#222222";
                switch (style)
                {
                    case "tick": mk.Line(apex, mid, "LEVEL", Pens.Default.Cut, ink); break;
                    case "filled": mk.Fills.Add(([new[] { apex, left, right }], ink)); mk.Polyline([apex, left, right], true, "LEVEL", Pens.Default.Seen, ink); break;
                    case "half": mk.Fills.Add(([new[] { apex, left, mid }], ink)); mk.Polyline([apex, left, right], true, "LEVEL", Pens.Default.Seen, ink); mk.Line(apex, mid, "LEVEL", Pens.Default.Seen, ink); break;
                    default: mk.Polyline([apex, left, right], true, "LEVEL", Pens.Default.Seen, ink); break;
                }
                mk.Text(new PenText(new Vec2(x1 + w + 1.0 * c.Mm, y + t * 0.2), $"{storey.Name} {(Standards.IsImperial(c.Book.Units) ? Standards.FormatLevel(z, c.Book.Units) : $"{(z >= 0 ? "+" : "−")}{Math.Abs(z):0.00}")}", text.Height, Anchor: "start", Font: text.Font), "#555555");
                yield return mk;
            }
        }
    }

    public static readonly string[] LevelMarkStyles = ["open", "half", "filled", "tick"];
    public const double DefaultLevelMarkSize = 5;

    /// <summary>The level marker's style and size (paper mm) from the project's view defaults (<c>viewDefaults.levelMark</c>): an open triangle of 5 mm unless set.</summary>
    public static (string Style, double Size) LevelMark(Entity? project)
    {
        var o = project?.Attrs["viewDefaults"]?["levelMark"] as JsonObject;
        var style = (string?)o?["style"] is { } s && LevelMarkStyles.Contains(s) ? s : "open";
        var size = Json.Number(o?["size"]) is { } n && n >= 1 && n <= 20 ? n : DefaultLevelMarkSize;
        return (style, size);
    }

    /// <summary>The colour reference planes are drawn in, in 2D (the 3D view draws them in the same).</summary>
    public const string RefPlaneColor = "#2b7a9e";

    /// <summary>
    /// The reference planes (docs/plan-studio-thick.md) where they cut the view's plane: a dashed line across the view's
    /// extent with the plane's name beyond its end, picked as the plane (the mark's owner), on the construction layer and
    /// only for the screen, so it is never printed. A plane parallel to the view is not drawn (it cuts nothing of it); a
    /// developed section shows the cut on each of its pieces; a plane's own view draws no other plane.
    /// </summary>
    static IEnumerable<ViewMark> RefPlaneMarks(ViewContext c)
    {
        var m = c.Model;
        if (c.Kind == "3d" || c.View.Str("kind") == RefPlanes.Kind) yield break;
        var planes = m.OfClass(Class).Where(v => v.Str("kind") == RefPlanes.Kind).ToList();
        if (planes.Count == 0 || c.Drawing.Bounds is not { } bounds) yield break;
        var k = c.Scale / 100;
        var text = Standards.ResolveText(c.Book, []);
        var margin = 1.0 * k;
        var (x0, x1, y0, y1) = (bounds.Min.X - margin, bounds.Max.X + margin, bounds.Min.Y - margin, bounds.Max.Y + margin);
        var frames = ViewPlanes(c, x0, x1);
        foreach (var v in planes)
        {
            var plane = RefPlanes.Of(v);
            var mk = new ViewMark(v.Id);
            foreach (var (view, fx0, fx1) in frames)
            {
                if (fx1 - fx0 < 1e-6 || PlaneCut(view, plane, fx0, fx1, y0, y1) is not var (a, b)) continue;
                mk.Line(a, b, ConstructionLayer, Pens.Default.Hatch, RefPlaneColor, LineStyles.DashOf("dashed"));
                // The name past the end that lies up or to the right.
                var (tail, head) = b.Y > a.Y + 1e-9 || (Math.Abs(b.Y - a.Y) <= 1e-9 && b.X >= a.X) ? (a, b) : (b, a);
                var u = (head - tail).Normalized();
                mk.Text(new PenText(head + u * (0.3 * k), v.Name, text.Height, Anchor: u.X < -0.5 ? "end" : "start", Baseline: "middle", Font: text.Font), RefPlaneColor);
                mk.Snaps.AddRange([a, b]);
            }
            if (mk.Lines.Count > 0) yield return mk;
        }
    }

    /// <summary>
    /// The view's plane (its drawing's x and y on it), or a developed section's pieces, each over the x it covers within
    /// <paramref name="x0"/>..<paramref name="x1"/>: what the reference planes' and the sections' traces are cut against.
    /// </summary>
    internal static List<(WorkPlane Plane, double X0, double X1)> ViewPlanes(ViewContext c, double x0, double x1)
    {
        var m = c.Model;
        var frames = new List<(WorkPlane Plane, double X0, double X1)>();
        var developed = c.Kind == "section" && SectionPaths.Resolve(m, c.Services, c.View).Path is { } path ? SectionPaths.Pieces(path) : null;
        if (developed is { Count: > 1 })
        {
            foreach (var piece in developed)
                if (PlaneOf(m, c.Services, c.View, piece.Station + piece.Length / 2) is { } wp)
                    frames.Add((wp, Math.Max(x0, piece.Station), Math.Min(x1, piece.Station + piece.Length)));
        }
        else if (c.Kind == "plan") { if (PlaneOf(m, c.Services, c.View) is { } wp) frames.Add((wp, x0, x1)); }
        else frames.Add((new WorkPlane(c.Frame.Origin, c.Frame.Right, c.Frame.Up, -c.Frame.Dir), x0, x1));
        return frames;
    }

    /// <summary>
    /// Where <paramref name="plane"/> cuts the <paramref name="view"/>'s plane, in the view's drawing coordinates, clipped to the
    /// rectangle; null when the planes are parallel or the cut misses the rectangle.
    /// </summary>
    public static (Vec2 A, Vec2 B)? PlaneCut(WorkPlane view, WorkPlane plane, double x0, double x1, double y0, double y1)
    {
        var d = Vec3.Cross(plane.Normal, view.Normal);
        if (d.Length < 1e-6) return null;
        // A point on both: from the view's origin, within the view's plane, straight towards the other plane.
        var n = plane.Normal;
        var w = n - view.Normal * Vec3.Dot(n, view.Normal);
        var t = Vec3.Dot(plane.Origin - view.Origin, n) / Vec3.Dot(w, n);
        var at = view.Local(view.Origin + w * t).At;
        var dir = new Vec2(Vec3.Dot(d, view.Right), Vec3.Dot(d, view.Up)).Normalized();
        // Liang–Barsky: the line at + dir·s against the rectangle.
        double smin = double.NegativeInfinity, smax = double.PositiveInfinity;
        foreach (var (p0, dd, lo, hi) in new[] { (at.X, dir.X, x0, x1), (at.Y, dir.Y, y0, y1) })
        {
            if (Math.Abs(dd) < 1e-12) { if (p0 < lo || p0 > hi) return null; continue; }
            var (s0, s1) = ((lo - p0) / dd, (hi - p0) / dd);
            if (s0 > s1) (s0, s1) = (s1, s0);
            smin = Math.Max(smin, s0);
            smax = Math.Min(smax, s1);
        }
        if (smin >= smax) return null;
        return (at + dir * smin, at + dir * smax);
    }

    /// <summary>
    /// What a developed section writes over itself: a fine dashed line down each jog (where one piece ends and the next
    /// begins), and the stations along the bottom when it has them (a tick and "0+020" every step, from the station its
    /// line starts at). Null when it is a plain section with no stations.
    /// </summary>
    static ViewMark? SectionMarks(ViewContext c, (Vec2 Min, Vec2 Max) bounds, double k, TextStyle text)
    {
        var (path, station0, _) = SectionPaths.Resolve(c.Model, c.Services, c.View);
        if (path == null) return null;
        var pieces = SectionPaths.Pieces(path);
        var step = SectionPaths.Stations(c.View, c.Scale);
        if (pieces.Count < 2 && step == null) return null;
        var mk = new ViewMark(null);
        var (y0, y1) = (bounds.Min.Y - 0.5 * k, bounds.Max.Y + 0.5 * k);
        foreach (var p in pieces.Skip(1))
            mk.Line(new Vec2(p.Station, y0), new Vec2(p.Station, y1), "SECTION", Pens.Default.Hidden, "#777777", LineStyles.DashOf("dashed"));
        if (step is { } st)
        {
            var length = pieces.Count == 0 ? 0 : pieces[^1].Station + pieces[^1].Length;
            var tick = 1.0 * k;
            // Every round station from the line's start, and its two ends.
            var at = new SortedSet<double> { 0, length };
            for (var s = Math.Ceiling(station0 / st) * st; s <= station0 + length + 1e-9; s += st) at.Add(Math.Round(s - station0, 6));
            foreach (var x in at)
            {
                if (x < -1e-9 || x > length + 1e-9) continue;
                mk.Line(new Vec2(x, y0), new Vec2(x, y0 - tick), "SECTION", Pens.Default.Seen, "#333333");
                mk.Text(new PenText(new Vec2(x, y0 - tick - 0.4 * k), SectionPaths.Station(station0 + x), text.Height * 0.8, 0, "middle", "bottom", false, text.Font), "#333333");
            }
            mk.Line(new Vec2(0, y0), new Vec2(length, y0), "SECTION", Pens.Default.Seen, "#333333");
        }
        return mk;
    }

    /// <summary>
    /// A mark per element from its cut loops and seen lines, so a section or an elevation picks what it draws; its cut and
    /// filled loops go in the mark's data too (<c>loops</c>: x y x y … each), so a click inside a cut takes the element.
    /// </summary>
    static IEnumerable<ViewMark> PickMarks(Lapin.Drawing.Views.ViewDrawing drawing)
    {
        var byElement = new Dictionary<string, ViewMark>();
        var loops = new Dictionary<string, JsonArray>();
        ViewMark Of(string id) => byElement.TryGetValue(id, out var mk) ? mk : byElement[id] = new ViewMark(id) { Pick = true };
        void Loop(string id, IReadOnlyList<Vec2> loop)
        {
            if (loop.Count < 3) return;
            if (!loops.TryGetValue(id, out var all)) loops[id] = all = [];
            all.Add((JsonNode)new JsonArray(loop.SelectMany(p => new[] { p.X, p.Y }).Select(v => (JsonNode)Math.Round(v, 4)).ToArray()));
        }
        foreach (var c in drawing.Cuts)
            foreach (var loop in c.Loops)
                if (loop.Length >= 2) { Of(c.Element).Polyline(loop, true, "PICK", 0); Loop(c.Element, loop); }
        foreach (var l in drawing.Lines)
            if (l.Role is not (LineRole.Hidden or LineRole.Overhead)) Of(l.Element).Line(l.A, l.B, "PICK", 0);
        foreach (var f in drawing.Fills ?? [])
            foreach (var loop in f.Loops)
                if (loop.Length >= 2) { Of(f.Element).Polyline(loop, true, "PICK", 0); Loop(f.Element, loop); }
        foreach (var (id, all) in loops) Of(id).Data = new JsonObject { ["loops"] = all };
        return byElement.Values;
    }

    static JsonArray P2(JsonNode? n, string what) => n is JsonArray a && a.Count >= 2 && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y
        ? new JsonArray(x, y) : throw new CommandException($"A section needs its {what} point [x, y].");

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "View",
        [
            new("kind", "Kind", "choice", Choices: Kinds, ReadOnly: true),
            new("scale", "Scale 1:", "number", Min: 1, Max: 10000),
            new("cutHeight", "Cut height", "length"),
            new("depth", "Depth", "length", Min: 0),
            new("direction", "Looking", "choice", Choices: Directions),
            new("hiddenLines", "Hidden lines", "choice", Choices: ["auto", "show", "hide"]),
            new("discipline", "Discipline", "text"),
            new("openingSymbols", "Opening symbols", "choice", Choices: ["show", "hide"]),
            // The layer a section's marker is on, so views hide markers by layer (SectionMarkers).
            new("markerLayer", "Marker layer", "text"),
            // An elevation's sketch plane, along its look; a reference plane's place and turn (docs/plan-studio-thick.md).
            new("planeDepth", "Sketch plane depth", "length"),
            new("x", "Origin x", "length"), new("y", "Origin y", "length"), new("z", "Origin z", "length"),
            new("bearing", "Faces (° from +X)", "number", Min: -360, Max: 360),
            new("tilt", "Tilt (°)", "number", Min: -90, Max: 90),
            new("spin", "Spin (°)", "number", Min: -360, Max: 360),
            // How large the 3D view draws a reference plane (empty: as large as the model, 2 m at least).
            new("width", "Drawn width (3D)", "length", Min: 0), new("height", "Drawn height (3D)", "length", Min: 0),
        ], "#555555", Kind: "view"));

        r.Command("view.create", "New view", (ctx, a) =>
        {
            var kind = (string?)a["kind"] ?? "plan";
            var attrs = new JsonObject { ["kind"] = kind, ["scale"] = Json.Number(a["scale"]) ?? 100 };
            if ((string?)a["discipline"] is { Length: > 0 } discipline) attrs["discipline"] = discipline;
            string name;
            switch (kind)
            {
                case "3d":
                    // A saved 3D view: its camera (when given), its own hidden elements and graphics; {3D} itself stays on the project.
                    attrs.Remove("scale");
                    if (a["camera"] != null) attrs["camera"] = Camera(a["camera"]);
                    if (a["sectionBox"] is JsonObject) attrs["sectionBox"] = SectionBox(a["sectionBox"]);
                    name = (string?)a["name"] ?? $"3D view {ctx.Model.OfClass(Class).Count(v => v.Str("kind") == "3d") + 1}";
                    break;
                case "plan" when (string?)a["site"] is { } purpose:
                {
                    if (!SitePlans.Purposes.Contains(purpose)) throw new CommandException("A site view is a site plan (situation) or a location plan (location).");
                    var host = (string?)a["storeyId"] is { Length: > 0 } sid ? Spatial.Require(ctx.Model, sid, IfcClass.BuildingStorey) : SitePlans.Host(ctx.Model) ?? throw new CommandException("The project has no storey.");
                    var (siteAttrs, siteName) = SitePlans.New(purpose, host.Id, Json.Number(a["scale"]), (string?)a["name"]);
                    foreach (var (k, val) in attrs) if (!siteAttrs.ContainsKey(k)) siteAttrs[k] = val?.DeepClone();
                    attrs = siteAttrs;
                    name = siteName;
                    break;
                }
                case "plan":
                {
                    var storey = Spatial.Require(ctx.Model, (string?)a["storeyId"], IfcClass.BuildingStorey);
                    attrs["storeyId"] = storey.Id;
                    attrs["cutHeight"] = Json.Number(a["cutHeight"]) ?? DefaultCut;
                    // A foundation plan: cut 0.5 m under its level, seeing 2.5 m down.
                    if (a["foundation"] is JsonValue fv && fv.TryGetValue<bool>(out var f) && f)
                    {
                        attrs["foundation"] = true;
                        attrs["cutHeight"] = -0.5;
                        attrs["viewRange"] = new ViewRange(new("self", 0), new("self", -3.0), new("self", -3.0)).ToJson();
                        // It shows the level under it too (the Foundations of a new project): what is drawn in it goes there (decision 437, report #117).
                        var elevation = storey.Num("elevation");
                        if (Spatial.Storeys(ctx.Model).Where(x => x.Num("elevation") < elevation - 1e-6).OrderByDescending(x => x.Num("elevation")).FirstOrDefault() is { } below)
                            attrs["storeys"] = new StoreyDisplay("some", new HashSet<string> { below.Id }, false).ToJson();
                    }
                    name = (string?)a["name"] ?? storey.Name;
                    break;
                }
                case "section" when a[AcrossSections.Key] != null:
                    // Across an element at a station (across, station, flip, margin, depth), drawn at 1:20 unless asked otherwise.
                    if (a["scale"] == null) attrs["scale"] = 20;
                    name = AcrossSections.Set(ctx, attrs, a);
                    break;
                case "section":
                    // Its line: start and end, or pts (jogged), an arc through three points (curve), or an element it follows (along: a road, a wall, a drafted polyline); its depth.
                    SectionPaths.Set(ctx.Model, ctx.Services, attrs, a, create: true);
                    if (Json.Number(a["depth"]) is { } d) attrs["depth"] = d;
                    name = (string?)a["name"] ?? $"Section {ctx.Model.OfClass(Class).Count(v => v.Str("kind") == "section") + 1}";
                    break;
                case "elevation":
                    var dir = (string?)a["direction"] ?? "south";
                    if (!Directions.Contains(dir)) throw new CommandException("An elevation looks north, south, east or west.");
                    attrs["direction"] = dir;
                    name = (string?)a["name"] ?? $"{char.ToUpperInvariant(dir[0])}{dir[1..]} elevation";
                    break;
                case RefPlanes.Kind:
                    // A reference plane: through three points, on a face, or a view's plane moved by an offset.
                    attrs["scale"] = Json.Number(a["scale"]) ?? 50;
                    foreach (var (k, val) in PlaneFrom(ctx, a)) attrs[k] = val?.DeepClone();
                    if (Json.Number(a["depth"]) is { } pd) attrs["depth"] = pd;
                    name = (string?)a["name"] ?? $"Plane {ctx.Model.OfClass(Class).Count(v => v.Str("kind") == RefPlanes.Kind) + 1}";
                    break;
                default: throw new CommandException("A view is a plan, a section, an elevation, a reference plane or a 3D view.");
            }
            // The level's first floor plan view becomes its plan: what was drawn on its default plan goes onto it (decision 386).
            var first = kind == "plan" && attrs["site"] == null && !attrs.ContainsKey("foundation") && (string?)attrs["storeyId"] is { } onStorey && StoreyPlans.Of(ctx.Model, onStorey) == null ? onStorey : null;
            var v = new Entity(ctx.NewId(), Class, name, attrs);
            ctx.Add(v);
            if (first != null) StoreyPlans.Rehome(ctx, first, v.Id);
            return new JsonObject { ["id"] = v.Id };
        });

        Insets.Register(r);
        ViewBoundaries.Register(r);
        SectionMarkers.Register(r);

        // A section across an element moved along it or widened: {id, station?, marginA?, marginB?} (AcrossSections).
        r.Command("view.updateAcross", "Move section", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x && AcrossSections.Is(x) ? x : throw new CommandException("Not a section across an element.");
            var attrs = v.CloneAttrs();
            var auto = AcrossSections.Set(ctx, attrs, new JsonObject { [AcrossSections.Key] = v.Str(AcrossSections.Key) });
            var args = new JsonObject();
            foreach (var key in new[] { "station", "marginA", "marginB" }) if (a.ContainsKey(key)) args[key] = a[key]?.DeepClone();
            var name = AcrossSections.Set(ctx, attrs, args);
            // A name of the engine's own follows the station; the user's stays.
            ctx.Replace(v.With(name: v.Name == auto ? name : v.Name, attrs: attrs));
            return null;
        });

        // A site plan's or a location plan's settings: {id, site?, gridStep?, northArrow?, scaleBar?} (SitePlans).
        r.Command("view.setSite", "Site plan", (ctx, a) => { SitePlans.Set(ctx, a); return null; });

        // An elevation's sketch plane fixed where it is drawn now ({id, depth?}): what is sketched on it then stands on that plane.
        r.Command("view.fixPlane", "Sketch plane", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("Not a view.");
            var after = FixElevationPlane(ctx, v, Json.Number(a["depth"]));
            return new JsonObject { ["originX"] = after.Num("originX"), ["planeDepth"] = after.Num("planeDepth") };
        });

        // A reference plane moved ({id, by?: [dx, dy, dz], along?: m towards its viewer}) and turned about the vertical ({turn?: °, about?: [x, y, z]}).
        r.Command("view.movePlane", "Move plane", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x && x.Str("kind") == RefPlanes.Kind ? x : throw new CommandException("Not a reference plane.");
            static Vec3 V(JsonNode? n) => n is JsonArray { Count: >= 3 } q ? new Vec3(Json.Number(q[0]) ?? 0, Json.Number(q[1]) ?? 0, Json.Number(q[2]) ?? 0) : default;
            var by = V(a["by"]) + RefPlanes.Of(v).Normal * (Json.Number(a["along"]) ?? 0);
            Vec3? about = a["about"] is JsonArray ? V(a["about"]) : null;
            var attrs = v.CloneAttrs();
            foreach (var (k, val) in RefPlanes.Moved(v, by, Json.Number(a["turn"]) ?? 0, about)) attrs[k] = val?.DeepClone();
            ctx.Replace(v.With(attrs: attrs));
            return null;
        });

        // A saved 3D view's camera: where it looks from.
        r.Command("view.setCamera", "View camera", (ctx, a) =>
        {
            var id = (string?)a["id"] ?? "";
            if (id == GraphicsSets.View3d) throw new CommandException("The project's 3D view keeps no camera: save it as a 3D view of its own first.");
            var v = ctx.Model.Get(id) is { Class: Class } x && x.Str("kind") == "3d" ? x : throw new CommandException("Not a 3D view.");
            var attrs = v.CloneAttrs();
            attrs["camera"] = Camera(a["camera"]);
            ctx.Replace(v.With(attrs: attrs));
            return null;
        });

        // A saved 3D view's section box: what of the model it shows, cut ({min, max, rot}; null: none).
        r.Command("view.setSectionBox", "Section box", (ctx, a) =>
        {
            var id = (string?)a["id"] ?? "";
            if (id == GraphicsSets.View3d) throw new CommandException("The project's 3D view keeps no section box: save it as a 3D view of its own first.");
            var v = ctx.Model.Get(id) is { Class: Class } x && x.Str("kind") == "3d" ? x : throw new CommandException("Not a 3D view.");
            var attrs = v.CloneAttrs();
            if (a["box"] is JsonObject) attrs["sectionBox"] = SectionBox(a["box"]); else attrs.Remove("sectionBox");
            ctx.Replace(v.With(attrs: attrs));
            return null;
        });

        // A copy of a view: its settings (kind, storey, range, scale, line, graphics, hidden elements, line edits,
        // discipline); with detailing, its annotations too (dimensions, texts, tags, drafted shapes). The implicit
        // plan of a storey copies as a plan view of its own; {3D} as a saved 3D view.
        r.Command("view.duplicate", "Duplicate view", (ctx, a) =>
        {
            var id = (string?)a["id"] ?? throw new CommandException("Which view?");
            var src = Find(ctx.Model, id) ?? throw new CommandException("There is no such view.");
            var attrs = src.CloneAttrs();
            if (id == GraphicsSets.View3d) attrs["kind"] = "3d";
            if ((string?)a["discipline"] is { Length: > 0 } discipline) attrs["discipline"] = discipline;
            var copy = new Entity(ctx.NewId(), Class, (string?)a["name"] is { Length: > 0 } nm ? nm : CopyName(ctx.Model, src.Name, Class), attrs);
            ctx.Add(copy);
            var annotations = 0;
            if (a["withDetailing"] is JsonValue dv && dv.TryGetValue<bool>(out var detailing) && detailing)
                foreach (var ann in ctx.Model.OfClass(IfcClass.Annotation).Where(x => x.Str("viewId") == id).ToList())
                {
                    var aa = ann.CloneAttrs();
                    aa["viewId"] = copy.Id;
                    var ac = new Entity(ctx.NewId(), ann.Class, ann.Name, aa, ann.PredefinedType);
                    ctx.Add(ac);
                    foreach (var rel in ctx.Model.RelationsOf(ann.Id).Where(x => x.Related.Contains(ann.Id)).ToList()) ctx.Relate(rel.Class, rel.Relating, ac.Id);
                    annotations++;
                }
            return new JsonObject { ["id"] = copy.Id, ["annotations"] = annotations };
        });

        // A section's line moved or redrawn: {id, start?, end?} or {id, pts}, {curve: "arc" | null}, {along: element | null}, {stations: m | null}.
        r.Command("view.setLine", "Move section", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x && x.Str("kind") == "section" ? x : throw new CommandException("Not a section.");
            var attrs = v.CloneAttrs();
            SectionPaths.Set(ctx.Model, ctx.Services, attrs, a, create: false);
            ctx.Replace(v.With(attrs: attrs));
            return null;
        });

        // A plan's view range: Top, Bottom and Depth, each a level and an offset; reset: the default. Its problems are reported, not refused (Revit's).
        // A plan's view range as set (its edges on levels) and what it resolves to, with its problems.
        r.Query("view.range", (q, a) =>
        {
            if (Find(q.Model, (string?)a["id"] ?? "") is not { } v || v.Str("kind") != "plan") return null;
            var cut = v.NumOrNull("cutHeight") ?? DefaultCut;
            var range = ViewRange.From(v.Attrs["viewRange"]) ?? ViewRanges.Default(cut);
            var resolved = Range(q.Model, v);
            return new JsonObject
            {
                ["cutHeight"] = cut, ["range"] = range.ToJson(), ["custom"] = v.Attrs["viewRange"] != null,
                ["resolved"] = new JsonObject { ["cut"] = resolved.Cut, ["top"] = resolved.Top, ["bottom"] = resolved.Bottom, ["depth"] = resolved.Depth },
                ["problems"] = new JsonArray(ViewRanges.Problems(resolved).Select(p => (JsonNode)p).ToArray()),
            };
        });

        // What a plan shows of the other storeys: {id, mode: home|some|all, ids?: [storey ids], halftone?}; the plan's own storey is always shown.
        // A 3D view's ({3D}'s, id "3d", or a saved one's): {id, mode: all|some|active|below, ids?, unplaced?, cut?} (Storeys3d).
        r.Command("view.setStoreys", "Storeys shown", (ctx, a) =>
        {
            var vid = (string?)a["id"] ?? "";
            if (vid == GraphicsSets.View3d || ctx.Model.Get(vid) is { Class: Class } v3 && v3.Str("kind") == "3d")
            {
                var (view3d, write) = GraphicsSets.Target(ctx, vid);
                SetStoreys3d(ctx, a, view3d, write);
                return null;
            }
            var v = ctx.Model.Get(vid) is { Class: Class } x && x.Str("kind") == "plan" ? x : throw new CommandException("Not a plan or a 3D view.");
            if (SitePlans.Of(v) != null) throw new CommandException("A site plan shows every storey.");
            var old = StoreyDisplay.Of(v);
            var mode = (string?)a["mode"] ?? old.Mode;
            if (!StoreyDisplay.Modes.Contains(mode)) throw new CommandException($"„{mode}” is not home, some or all.");
            var ids = a["ids"] is JsonArray arr ? arr.Select(n => (string?)n).OfType<string>().ToHashSet() : old.Storeys.ToHashSet();
            foreach (var id in ids) Spatial.Require(ctx.Model, id, IfcClass.BuildingStorey);
            ids.Remove(v.Str("storeyId") ?? "");
            var halftone = a["halftone"] is JsonValue hv && hv.TryGetValue<bool>(out var h) ? h : old.Halftone;
            var attrs = v.CloneAttrs();
            var next = new StoreyDisplay(mode, ids, halftone);
            if (next.Mode == "home" && next.Storeys.Count == 0 && !next.Halftone) attrs.Remove("storeys");
            else attrs["storeys"] = next.ToJson();
            ctx.Replace(v.With(attrs: attrs));
            return null;
        });

        // A plan's reference level: another storey's plan drawn under this one on screen, faded, snapped to, never printed ({id, storeyId}; null: none).
        r.Command("view.setUnderlay", "Reference level", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x && x.Str("kind") == "plan" ? x : throw new CommandException("Not a plan view.");
            var attrs = v.CloneAttrs();
            if ((string?)a["storeyId"] is { Length: > 0 } sid)
            {
                if (sid == v.Str("storeyId")) throw new CommandException("A plan cannot reference its own level.");
                attrs["underlay"] = Spatial.Require(ctx.Model, sid, IfcClass.BuildingStorey).Id;
            }
            else attrs.Remove("underlay");
            ctx.Replace(v.With(attrs: attrs));
            return null;
        });

        r.Command("view.setRange", "View range", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x && x.Str("kind") == "plan" ? x : throw new CommandException("Not a plan.");
            if (SitePlans.Of(v) != null) throw new CommandException("A site plan sees the whole model from above: it has no view range.");
            var attrs = v.CloneAttrs();
            if (a["reset"] is JsonValue rv && rv.TryGetValue<bool>(out var reset) && reset) attrs.Remove("viewRange");
            else
            {
                var current = ViewRange.From(attrs["viewRange"]) ?? new ViewRange(new("above", 0), new("self", 0), new("self", 0));
                RangeEdge Edge(JsonNode? n, RangeEdge old) => n is JsonObject o
                    ? new RangeEdge((string?)o["level"] is { } l ? (RangeEdge.Levels.Contains(l) ? l : throw new CommandException($"„{l}” is not self, above, below or unlimited.")) : old.Level, Json.Number(o["offset"]) ?? 0)
                    : old;
                attrs["viewRange"] = new ViewRange(Edge(a["top"], current.Top), Edge(a["bottom"], current.Bottom), Edge(a["depth"], current.Depth)).ToJson();
            }
            if (Json.Number(a["cutHeight"]) is { } cut) attrs["cutHeight"] = cut;
            // Seen from above (a plan) or from below (a reflected ceiling plan).
            if ((string?)a["look"] is { } look)
            {
                if (look is not ("up" or "down")) throw new CommandException("A plan is seen from above (down) or from below (up).");
                if (look == "up") attrs["look"] = "up"; else attrs.Remove("look");
            }
            var next = v.With(attrs: attrs);
            ctx.Replace(next);
            return new JsonObject { ["problems"] = new JsonArray(ViewRanges.Problems(Range(ctx.Model, next)).Select(p => (JsonNode)p).ToArray()) };
        });

        // A view's graphics: a category's line colour, weight, fill, cut hatch, lines by role, or hidden; an element's lines by role.
        // The project's object styles: one role of a category ({pen, color, pattern}; null clears it), and its defaults for hidden lines by kind.
        // The project's pen table: one pen's width at one of the table's scales; reset: ISO again.
        r.Command("view.penTable", "Pen table", (ctx, a) =>
        {
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = project.CloneAttrs();
            if (a["reset"] is JsonValue rv && rv.TryGetValue<bool>(out var reset) && reset) attrs.Remove("penTable");
            else
            {
                var t = LineStyles.ReadTable(attrs["penTable"]) ?? LineStyles.DefaultPenTable();
                var pen = (int)(Json.Number(a["pen"]) ?? 0);
                var i = t.Scales.ToList().IndexOf((int)(Json.Number(a["scale"]) ?? 0));
                var w = Json.Number(a["width"]) ?? 0;
                if (pen < 1 || pen > LineStyles.PenCount) throw new CommandException("A pen is 1 to 16.");
                if (i < 0) throw new CommandException($"The pen table has scales {string.Join(", ", t.Scales.Select(x => "1:" + x))}.");
                if (!(w > 0 && w <= 10)) throw new CommandException("A pen's width is more than 0 and at most 10 mm.");
                var widths = t.Widths.Select(r => r.ToList()).ToList();
                widths[pen - 1][i] = w;
                attrs["penTable"] = LineStyles.ToJson(new LineStyles.PenTable(t.Scales, widths.Select(r => (IReadOnlyList<double>)r).ToList()));
            }
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });

        // What the Object Styles dialog shows: the styles, the pen table in force, the hidden-line defaults, and the lists they choose from.
        r.Query("view.objectStyles", (q, _) =>
        {
            var project = Spatial.Project(q.Model);
            return new JsonObject
            {
                ["objectStyles"] = project?.Attrs["objectStyles"]?.DeepClone() ?? new JsonObject(),
                ["penTable"] = LineStyles.ToJson(LineStyles.ReadTable(project?.Attrs["penTable"]) ?? LineStyles.DefaultPenTable()),
                ["customTable"] = project?.Attrs["penTable"] != null,
                ["hiddenLines"] = project?.Attrs["viewDefaults"]?["hiddenLines"]?.DeepClone() ?? new JsonObject(),
                ["levelMark"] = new JsonObject { ["style"] = LevelMark(project).Style, ["size"] = LevelMark(project).Size },
                ["categories"] = new JsonArray(LineStyles.Categories.Select(c => (JsonNode)c).ToArray()),
                ["roles"] = new JsonArray(LineStyles.Roles.Select(c => (JsonNode)c).ToArray()),
                ["patterns"] = new JsonArray(LineStyles.Patterns.Select(c => (JsonNode)c).ToArray()),
            };
        });

        r.Command("view.objectStyle", "Object styles", (ctx, a) =>
        {
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = project.CloneAttrs();
            if ((string?)a["category"] is { } cat)
            {
                var role = (string?)a["role"] ?? "";
                if (!LineStyles.Categories.Contains(cat) || !LineStyles.Roles.Contains(role)) throw new CommandException("An object style is a category and a role (cut, projection, hidden, overhead, beyond).");
                var styles = attrs["objectStyles"] as JsonObject ?? new JsonObject();
                var c = styles[cat] as JsonObject ?? new JsonObject();
                if (a["spec"] is JsonObject spec)
                {
                    if ((string?)spec["pattern"] is { } pat && !LineStyles.Patterns.Contains(pat)) throw new CommandException($"„{pat}” is not a line pattern.");
                    if (Json.Number(spec["pen"]) is { } pen && (pen < 1 || pen > LineStyles.PenCount)) throw new CommandException("A pen is 1 to 16.");
                    c[role] = spec.DeepClone();
                }
                else c.Remove(role);
                if (c.Count > 0) styles[cat] = c; else styles.Remove(cat);
                if (styles.Count > 0) attrs["objectStyles"] = styles; else attrs.Remove("objectStyles");
            }
            // The level marker in sections and elevations: {levelMark: {style, size}}.
            if (a["levelMark"] is JsonObject lm)
            {
                var style = (string?)lm["style"] ?? LevelMark(project).Style;
                if (!LevelMarkStyles.Contains(style)) throw new CommandException("A level marker is an open, half or filled triangle, or a tick.");
                var size = Json.Number(lm["size"]) ?? LevelMark(project).Size;
                if (size < 1 || size > 20) throw new CommandException("A level marker is 1 to 20 mm on paper.");
                var defaults = attrs["viewDefaults"] as JsonObject ?? new JsonObject();
                defaults["levelMark"] = new JsonObject { ["style"] = style, ["size"] = size };
                attrs["viewDefaults"] = defaults;
            }
            if ((string?)a["kind"] is { } kind && (string?)a["hiddenLines"] is var hl)
            {
                var defaults = attrs["viewDefaults"] as JsonObject ?? new JsonObject();
                var h = defaults["hiddenLines"] as JsonObject ?? new JsonObject();
                if (hl is "show" or "hide") h[kind] = hl; else h.Remove(kind);
                defaults["hiddenLines"] = h;
                attrs["viewDefaults"] = defaults;
            }
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });

        r.Query("view.drawing", (q, a) =>
        {
            var id = (string?)a["id"] ?? ((string?)a["storeyId"] is { } s ? "plan:" + s : "");
            var view = Find(q.Model, id) ?? throw new CommandException("There is no such view.");
            return Drawing(q, view);
        });
    }
}
