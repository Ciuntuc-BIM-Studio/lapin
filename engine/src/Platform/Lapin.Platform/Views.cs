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
    public static readonly string[] Kinds = ["plan", "section", "elevation", "3d"];
    /// <summary>The disciplines the browser's default sets group by; a view or sheet may name any other.</summary>
    public static readonly string[] Disciplines = ["Architecture", "Structure", "MEP"];
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
                // The first piece of its line; a developed section (more pieces) is drawn piece by piece (Generate).
                var (path, _, problem) = SectionPaths.Resolve(m, services, view);
                if (path == null) return (null, problem);
                var pieces = SectionPaths.Pieces(path);
                if (pieces.Count == 0) return (null, "A section needs two different points.");
                return (ViewSpec.Section(pieces[0].A, pieces[0].B, view.NumOrNull("depth") ?? ViewRanges.Unlimited, hidden, spanned: pieces.Count > 1), null);
            }
            case "elevation":
            {
                if (Bounds(parts) is not { } bounds) return (null, "There is nothing to see.");
                Vec2 dir = view.Str("direction") switch { "south" => new(0, 1), "east" => new(-1, 0), "west" => new(1, 0), _ => new(0, -1) };
                return (ViewSpec.Elevation(dir, bounds, hidden), null);
            }
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
                return view.Str("direction") switch { "south" => new(0, 1), "east" => new(-1, 0), "west" => new(1, 0), _ => new(0, -1) };
            default: return null;
        }
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
    static (List<DrawPart> Parts, List<DrawSymbol> Symbols, List<DFill> Fills, List<DText> Texts) Gather(IQueryContext q, Entity view)
    {
        var m = q.Model;
        var parts = new List<DrawPart>();
        var symbols = new List<DrawSymbol>();
        var fills = new List<DFill>();
        var texts = new List<DText>();
        var storey = view.Str("storeyId");
        var viewGraphics = GraphicsSets.Of(m, view);
        var hiddenElements = (view.Attrs["hiddenElements"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        var filters = q.Services.All<IViewElementFilter>();
        var plan = view.Str("kind") is "plan" or null;
        // A site plan: the model from above, what lies under the ground left out, the site's own symbols from every storey.
        var site = plan && SitePlans.Of(view) != null;
        var display = StoreyDisplay.Of(view);
        // The view's style: which layers of layered elements draw (all, all but the finishes, the cores).
        var style = ViewStyles.Of(m, view);
        var bandCache = new Dictionary<string, IReadOnlyList<LayerBand>?>();
        double? cutZ = plan && !site && style.Ifc2d && m.Get(storey ?? "") is { Class: IfcClass.BuildingStorey } ? Range(m, view).Cut : null;
        foreach (var e in m.Entities)
        {
            // Rooms draw as their fills and stamps (their module's marks), never as a body cut or seen.
            if (e.Class is IfcClass.OpeningElement or IfcClass.Space || hiddenElements.Contains(e.Id)) continue;
            // A plan shows its own storey's elements, and the other storeys' only as it says (StoreyDisplay).
            if (plan && display.Shows(storey, Spatial.StoreyOf(m, e.Id)?.Id) == null) continue;
            if (filters.Select(f => f.For(m, view, e)).FirstOrDefault(x => x != null) is { Hide: true }) continue;
            if (GraphicsSets.Hides(viewGraphics, e)) continue;
            if (q.Geometry(e.Id) is not { } g) continue;
            var stored = StoredBodies.Has(e);
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
                    if (!bySymbol && Measure.Bounds(p.Solid) is { } b && b.Max.Z > SitePlans.Underground && ViewStyles.Draws(m, style, e.Id, p.Key, bandCache))
                        parts.Add(new DrawPart(p.Solid, e.Id, e.Class, p.Key, p.Material, CutInPlan: true));
                if (bySymbol)
                    foreach (var item in g.Plan!)
                    {
                        var role = item.Kind switch { "symbol" or "contour" or "terrainEdge" => LineRole.Projection, "contourMajor" or "heavy" => LineRole.Cut, _ => null };
                        if (role != null) symbols.Add(new DrawSymbol(item.Points, item.Closed, e.Id, e.Class, role));
                    }
                continue;
            }
            foreach (var p in g.Parts)
                if (ViewStyles.Draws(m, style, e.Id, p.Key, bandCache)) parts.Add(new DrawPart(p.Solid, e.Id, e.Class, p.Key, p.Material, CutInPlan: stored));
            if (view.Str("kind") == "plan" && !stored && Generator.SymbolicInPlan.Contains(e.Class) && g.Plan != null && Spatial.StoreyOf(m, e.Id)?.Id is { } s && s == storey)
                foreach (var item in g.Plan)
                {
                    var role = item.Kind switch { "symbol" or "contour" or "terrainEdge" => LineRole.Projection, "contourMajor" => LineRole.Cut, "heavy" => LineRole.Cut, "dashed" => LineRole.Hidden, "overhead" or "ridge" or "hip" or "valley" or "break" => LineRole.Overhead, _ => null };
                    if (role != null) symbols.Add(new DrawSymbol(item.Points, item.Closed, e.Id, e.Class, role));
                }
        }
        return (parts, symbols, fills, texts);
    }

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
        var (parts, symbols, fills, texts) = Gather(q, view);
        var (spec, problem) = Spec(q.Model, view, parts, q.Services);
        if (spec == null) return (null, null, problem);
        var keys = new Dictionary<string, string?>();
        // Without the view style's joins, a surface goes on only within its own element.
        var joins = ViewStyles.Of(q.Model, view).Joins;
        string? Key(DrawPart p) => SeamKeyOf(q.Model, p.Material, p.Class, keys) is { } k ? (joins ? k : k + "\u0000" + p.Element) : null;
        ViewDrawing drawing;
        if (spec.Kind == "section" && Pieces(q, view) is { Count: > 1 } pieces)
        {
            // A developed section: each piece a planar cut seen between its ends, laid at its station.
            var drawn = new List<ViewDrawing>(pieces.Count);
            foreach (var (piece, station) in pieces) drawn.Add(Generator.Draw(parts, piece, symbols, Key).Shifted(station));
            drawing = ViewDrawing.Join(drawn);
        }
        else drawing = Generator.Draw(parts, spec, symbols, Key);
        if (spec.Kind == "plan" && (fills.Count > 0 || texts.Count > 0)) drawing = drawing with { Fills = fills, Texts = texts };
        return (drawing, spec, null);
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
    public sealed record Rendered(List<Pen> Pens, List<ViewMark> Marks, (Vec2 Min, Vec2 Max)? Bounds, double Scale, string Kind, string? Problem, List<string> RangeProblems, List<string>? LineworkOrphans = null, ViewFrame? Frame = null, double? Depth = null, List<(ViewFrame Frame, double X0, double X1)>? Frames = null);

    /// <summary>The mark layer of construction lines (drafted shapes whose role is draft): on screen, never printed.</summary>
    public const string ConstructionLayer = "CONSTRUCTION";

    /// <summary>A view's drawing with its annotations; for print (sheets, PDF, DXF) construction lines are left out.</summary>
    public static Rendered Render(IQueryContext q, Entity view, bool print = false)
    {
        var m = q.Model;
        var scale = view.NumOrNull("scale") ?? 100;
        var (generated, spec, problem) = Generate(q, view);
        if (generated == null || spec == null) return new Rendered([], [], null, scale, view.Str("kind") ?? "plan", problem, []);
        // The view's line edits (Linework) over its generated lines.
        var (drawing, lineworkOrphans) = Linework.Apply(generated, ViewLinework.Edits(view));
        var kind = spec.Kind == "plan" ? "plan" : spec.Kind == "elevation" ? "elevation" : "section";
        var resolved = new Dictionary<string, ResolvedMaterial?>();
        ResolvedMaterial? Resolved(string? id) => id == null ? null
            : resolved.TryGetValue(id, out var r) ? r : resolved[id] = m.Get(id) is { } mat ? MaterialGraphics.Resolve(mat, kind) : null;
        // Each element's layers, so a layer thinner than a membrane (or of a membrane's material) draws as one.
        var layerSets = new Dictionary<string, IReadOnlyList<LayerRef>?>();
        IReadOnlyList<LayerRef>? LayersOf(string element) => layerSets.TryGetValue(element, out var ls) ? ls
            : layerSets[element] = LayerSets.BandsOf(m, element)?.Select(b => new LayerRef(b.Material, b.Name, Math.Abs(b.To - b.From))).ToList();
        var materials = MaterialDrawing.Apply(drawing, p => Resolved(p.Material), LayersOf,
            l => l.Thickness < MaterialGraphics.MembraneThickness || Resolved(l.Material)?.Membrane == true, joins: ViewStyles.Of(m, view).Joins);
        var project = Spatial.Project(m);
        var graphics = GraphicsSets.Of(m, view);
        var categories = (graphics?["categories"] as JsonObject)?.Select(c => (c.Key, G: CategoryGraphics.From(c.Value))).Where(x => x.G != null).ToDictionary(x => x.Key, x => x.G!);
        var table = LineStyles.ReadTable(project?.Attrs["penTable"]);
        var styles = LineStyles.For(project?.Attrs["objectStyles"] as JsonObject, graphics?["categories"] as JsonObject, graphics?["elements"] as JsonObject, table, scale);
        // Elements a module draws in another colour (a phase's override): their lines and fills.
        var colors = new Dictionary<string, string>();
        var filters = q.Services.All<IViewElementFilter>();
        var halftoneOthers = spec.Kind == "plan" && StoreyDisplay.Of(view).Halftone;
        if (filters.Count > 0 || halftoneOthers)
            foreach (var id in drawing.Cuts.Select(c => c.Element).Concat(drawing.Lines.Select(l => l.Element)).Concat((drawing.Fills ?? []).Select(f => f.Element)).Concat((drawing.Texts ?? []).Select(t => t.Element)).Distinct())
            {
                if (m.Get(id) is not { } el) continue;
                if (filters.Select(f => f.For(m, view, el)).FirstOrDefault(x => x != null) is { Color: { } c }) colors[id] = c;
                // Another storey's element in a plan that shows it halftone: grey, as a halftone link.
                else if (halftoneOthers && ShowsByStorey(m, view, el) == true) colors[id] = Links.Halftone;
            }
        var pens = Pens.Of(drawing, materials, new Pens.Options(scale, spec.Hidden, styles, categories, DraftingResources.Patterns(m), ElementColors: colors, Table: table));

        // Annotations: the views' own marks, then each module's (grids, dimensions, texts), over the drawing.
        var ctx = new ViewContext(m, view, spec.Kind, spec.Frame, scale, DraftingResources.Book(m), drawing, q.Geometry) { Services = q.Services };
        var marks = Marks(ctx).Concat(Location.Marks(ctx)).Concat(SitePlans.Marks(ctx)).ToList();
        foreach (var source in q.Services.All<IViewAnnotations>())
            try { marks.AddRange(source.Marks(ctx)); }
            catch (CommandException) { }
        // Every element the drawing shows can be picked by what it drew: its cut outlines and its seen lines, as a
        // mark on the PICK layer (never drawn, only hit and highlighted). Hidden and overhead lines pick nothing.
        marks.AddRange(PickMarks(drawing));
        // The marks' pens: those drawn under the model (rooms' fills) go first, the others over it; fills before lines in each.
        var underPens = new Dictionary<string, Pen>();
        var markPens = new Dictionary<string, Pen>();
        foreach (var mk in marks)
        {
            var into = mk.Under ? underPens : markPens;
            var fillLayer = mk.Under ? "ROOM-FILL" : "DRAFT-FILL";
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
        pens.InsertRange(0, underPens.Values.OrderBy(p => p.Fill != null ? 0 : 1));
        // Pictures (an orthophoto) under all of it.
        if (marks.SelectMany(mk => mk.Images).ToList() is { Count: > 0 } images)
        {
            var pictures = new Pen("IMAGE", null, 0, null, null);
            pictures.Images.AddRange(images);
            pens.Insert(0, pictures);
        }
        pens.AddRange(markPens.Values.OrderBy(p => p.Fill != null ? 0 : 1));
        // What the view shows: the drawing and its annotations (a text counted by its anchor and height).
        var pts = new List<Vec2>();
        if (drawing.Bounds is { } b) pts.AddRange([b.Min, b.Max]);
        foreach (var mk in marks)
        {
            foreach (var l in mk.Lines) pts.AddRange([l.A, l.B]);
            foreach (var (t, _) in mk.Texts) pts.AddRange([t.At, t.At + new Vec2(0, t.Height * scale / 1000)]);
        }
        (Vec2, Vec2)? bounds = pts.Count > 0 ? (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y))) : null;
        double? depth = spec.Kind == "section" && spec.Below < ViewRanges.Unlimited ? spec.Below : null;
        // A developed section: where each piece lies in the model, and the stations (developed x) it covers.
        var frames = spec.Kind == "section" && Pieces(q, view) is { Count: > 1 } ps ? ps.Select(p => (p.Spec.Frame, p.Station, p.Station + p.Spec.Span!.Value.Max)).ToList() : null;
        return new Rendered(pens, marks, bounds, scale, spec.Kind, null, spec.Kind == "plan" ? ViewRanges.Problems(Range(m, view)) : [], lineworkOrphans, spec.Frame, depth, frames);
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
                ["segments"] = new JsonArray(x.Lines.SelectMany(l => new[] { l.A.X, l.A.Y, l.B.X, l.B.Y }).Select(v => (JsonNode)Math.Round(v, 5)).ToArray()),
                ["snaps"] = new JsonArray(x.Snaps.SelectMany(p => new[] { p.X, p.Y }).Select(v => (JsonNode)Math.Round(v, 5)).ToArray()),
                ["shapes"] = x.Shapes.Count > 0 ? new JsonArray(x.Shapes.Select(sh => (JsonNode)sh.DeepClone()).ToArray()) : null,
                ["data"] = x.Data?.DeepClone(),
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

    /// <summary>A view as DXF at real size (1 m = 1000 mm, y up), its texts and dashes as high as on paper at its scale.</summary>
    public static byte[] Dxf(IQueryContext q, Entity view)
    {
        var r = Render(q, view);
        var pens = r.Pens.Select(p =>
        {
            var o = new Pen(p.Layer, p.Stroke, p.Width, p.Fill, p.Dash?.Select(v => v * r.Scale).ToArray());
            o.Segments.AddRange(p.Segments.Select(s => (s.A * 1000, s.B * 1000)));
            o.Loops.AddRange(p.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(v => v * 1000).ToList()));
            o.Texts.AddRange(p.Texts.Select(t => t with { At = t.At * 1000, Height = t.Height * r.Scale }));
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
        return Lapin.Drawing.Export.DxfWriter.Page(new Lapin.Drawing.Export.PaperPage(0, 0, flipped), $"Lapin · {view.Name} · 1:{r.Scale:0}");
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
            foreach (var v in m.OfClass(Class).Where(v => v.Str("kind") == "section"))
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
        else if (c.Drawing.Bounds is { } bounds)
        {
            if (c.Kind == "section" && SectionMarks(c, bounds, k, text) is { } section) yield return section;
            var (x0, x1) = (bounds.Min.X - 1.0 * k, bounds.Max.X + 1.5 * k);
            foreach (var storey in Spatial.Storeys(m))
            {
                var z = storey.Num("elevation");
                var y = c.Frame.Map(new Vec3(c.Frame.Origin.X, c.Frame.Origin.Y, z)).Y;
                var mk = new ViewMark(storey.Id);
                mk.Line(new Vec2(x0, y), new Vec2(x1, y), "LEVEL", Pens.Default.Hidden, "#555555", LineStyles.DashOf("center"));
                // The marker: a small triangle standing on the line, the level's name and elevation above it.
                var t = 1.2 * k;
                mk.Polyline([new Vec2(x1, y), new Vec2(x1 - t * 0.6, y + t), new Vec2(x1 + t * 0.6, y + t)], true, "LEVEL", Pens.Default.Seen, "#555555");
                mk.Text(new PenText(new Vec2(x1 + t, y + t * 0.2), $"{storey.Name} {(z >= 0 ? "+" : "−")}{Math.Abs(z):0.00}", text.Height, Anchor: "start", Font: text.Font), "#555555");
                yield return mk;
            }
        }
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

    /// <summary>A mark per element from its cut loops and seen lines, so a section or an elevation picks what it draws.</summary>
    static IEnumerable<ViewMark> PickMarks(Lapin.Drawing.Views.ViewDrawing drawing)
    {
        var byElement = new Dictionary<string, ViewMark>();
        ViewMark Of(string id) => byElement.TryGetValue(id, out var mk) ? mk : byElement[id] = new ViewMark(id);
        foreach (var c in drawing.Cuts)
            foreach (var loop in c.Loops)
                if (loop.Length >= 2) Of(c.Element).Polyline(loop, true, "PICK", 0);
        foreach (var l in drawing.Lines)
            if (l.Role is not (LineRole.Hidden or LineRole.Overhead)) Of(l.Element).Line(l.A, l.B, "PICK", 0);
        foreach (var f in drawing.Fills ?? [])
            foreach (var loop in f.Loops)
                if (loop.Length >= 2) Of(f.Element).Polyline(loop, true, "PICK", 0);
        return byElement.Values;
    }

    static JsonArray P2(JsonNode? n, string what) => n is JsonArray a && a.Count >= 2 && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y
        ? new JsonArray(x, y) : throw new CommandException($"A section needs its {what} point [x, y].");

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "View",
        [
            new("kind", "Kind", "choice", Choices: Kinds, ReadOnly: true),
            new("scale", "Scale 1:", "number", Min: 1, Max: 1000),
            new("cutHeight", "Cut height", "length"),
            new("depth", "Depth", "length", Min: 0),
            new("direction", "Looking", "choice", Choices: Directions),
            new("hiddenLines", "Hidden lines", "choice", Choices: ["auto", "show", "hide"]),
            new("discipline", "Discipline", "text"),
            new("openingSymbols", "Opening symbols", "choice", Choices: ["show", "hide"]),
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
                        attrs["cutHeight"] = -0.5;
                        attrs["viewRange"] = new ViewRange(new("self", 0), new("self", -3.0), new("self", -3.0)).ToJson();
                    }
                    name = (string?)a["name"] ?? storey.Name;
                    break;
                }
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
                default: throw new CommandException("A view is a plan, a section, an elevation or a 3D view.");
            }
            var v = new Entity(ctx.NewId(), Class, name, attrs);
            ctx.Add(v);
            return new JsonObject { ["id"] = v.Id };
        });

        // A site plan's or a location plan's settings: {id, site?, gridStep?, northArrow?, scaleBar?} (SitePlans).
        r.Command("view.setSite", "Site plan", (ctx, a) => { SitePlans.Set(ctx, a); return null; });

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
