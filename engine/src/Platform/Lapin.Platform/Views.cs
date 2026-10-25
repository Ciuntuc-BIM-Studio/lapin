using System.Text.Json.Nodes;
using BubbleBim.Geometry;
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
    public static readonly string[] Kinds = ["plan", "section", "elevation"];
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
    public static (ViewSpec? Spec, string? Problem) Spec(IModelView m, Entity view, IReadOnlyList<DrawPart> parts)
    {
        var project = Spatial.Project(m);
        var kind = view.Str("kind") ?? "plan";
        var hidden = ShowsHidden(project, kind, view.Str("hiddenLines"));
        switch (kind)
        {
            case "plan":
            {
                if (m.Get(view.Str("storeyId") ?? "") is not { Class: IfcClass.BuildingStorey }) return (null, "The plan's storey is gone.");
                var r = Range(m, view);
                return (ViewSpec.Plan(r.Cut, ViewRanges.Bands(r), hidden), null);
            }
            case "section":
            {
                if (view.Attrs["start"] is not JsonArray s || view.Attrs["end"] is not JsonArray e) return (null, "A section needs its line.");
                var (a, b) = (new Vec2(Json.Number(s[0]) ?? 0, Json.Number(s[1]) ?? 0), new Vec2(Json.Number(e[0]) ?? 0, Json.Number(e[1]) ?? 0));
                if ((b - a).Length < 1e-3) return (null, "A section needs two different points.");
                return (ViewSpec.Section(a, b, view.NumOrNull("depth") ?? ViewRanges.Unlimited, hidden), null);
            }
            case "elevation":
            {
                if (Bounds(parts) is not { } bounds) return (null, "There is nothing to see.");
                Vec2 dir = view.Str("direction") switch { "south" => new(0, 1), "east" => new(-1, 0), "west" => new(1, 0), _ => new(0, -1) };
                return (ViewSpec.Elevation(dir, bounds, hidden), null);
            }
        }
        return (null, $"„{kind}” is not a kind of view.");
    }

    /// <summary>Every element's parts, and in a plan the symbols of the doors, windows and roofs of its storey.</summary>
    static (List<DrawPart> Parts, List<DrawSymbol> Symbols) Gather(IQueryContext q, Entity view)
    {
        var m = q.Model;
        var parts = new List<DrawPart>();
        var symbols = new List<DrawSymbol>();
        var storey = view.Str("storeyId");
        var viewGraphics = GraphicsSets.Of(m, view);
        var hiddenElements = (view.Attrs["hiddenElements"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        var filters = q.Services.All<IViewElementFilter>();
        foreach (var e in m.Entities)
        {
            if (e.Class is IfcClass.OpeningElement || hiddenElements.Contains(e.Id)) continue;
            if (filters.Select(f => f.For(m, view, e)).FirstOrDefault(x => x != null) is { Hide: true }) continue;
            if (GraphicsSets.Hides(viewGraphics, e)) continue;
            if (q.Geometry(e.Id) is not { } g) continue;
            foreach (var p in g.Parts) parts.Add(new DrawPart(p.Solid, e.Id, e.Class, p.Key, p.Material));
            if (view.Str("kind") == "plan" && Generator.SymbolicInPlan.Contains(e.Class) && g.Plan != null && Spatial.StoreyOf(m, e.Id)?.Id is { } s && s == storey)
                foreach (var item in g.Plan)
                {
                    var role = item.Kind switch { "symbol" or "contour" or "terrainEdge" => LineRole.Projection, "contourMajor" => LineRole.Cut, "heavy" => LineRole.Cut, "overhead" or "ridge" or "hip" or "valley" or "break" => LineRole.Overhead, _ => null };
                    if (role != null) symbols.Add(new DrawSymbol(item.Points, item.Closed, e.Id, e.Class, role));
                }
        }
        return (parts, symbols);
    }

    /// <summary>The view's generated drawing (cut areas and lines, before materials and pens), or why there is none.</summary>
    public static (ViewDrawing? Drawing, ViewSpec? Spec, string? Problem) Generate(IQueryContext q, Entity view)
    {
        var (parts, symbols) = Gather(q, view);
        var (spec, problem) = Spec(q.Model, view, parts);
        return spec == null ? (null, null, problem) : (Generator.Draw(parts, spec, symbols), spec, null);
    }

    /// <summary>A view rendered: its pens (view coordinates, m; widths paper mm), annotations, bounds and scale, or why it cannot be drawn.</summary>
    public sealed record Rendered(List<Pen> Pens, List<ViewMark> Marks, (Vec2 Min, Vec2 Max)? Bounds, double Scale, string Kind, string? Problem, List<string> RangeProblems, List<string>? LineworkOrphans = null);

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
            l => l.Thickness < MaterialGraphics.MembraneThickness || Resolved(l.Material)?.Membrane == true);
        var project = Spatial.Project(m);
        var graphics = GraphicsSets.Of(m, view);
        var categories = (graphics?["categories"] as JsonObject)?.Select(c => (c.Key, G: CategoryGraphics.From(c.Value))).Where(x => x.G != null).ToDictionary(x => x.Key, x => x.G!);
        var table = LineStyles.ReadTable(project?.Attrs["penTable"]);
        var styles = LineStyles.For(project?.Attrs["objectStyles"] as JsonObject, graphics?["categories"] as JsonObject, graphics?["elements"] as JsonObject, table, scale);
        // Elements a module draws in another colour (a phase's override): their lines and fills.
        var colors = new Dictionary<string, string>();
        var filters = q.Services.All<IViewElementFilter>();
        if (filters.Count > 0)
            foreach (var id in drawing.Cuts.Select(c => c.Element).Concat(drawing.Lines.Select(l => l.Element)).Distinct())
                if (m.Get(id) is { } el && filters.Select(f => f.For(m, view, el)).FirstOrDefault(x => x != null) is { Color: { } c }) colors[id] = c;
        var pens = Pens.Of(drawing, materials, new Pens.Options(scale, spec.Hidden, styles, categories, DraftingResources.Patterns(m), ElementColors: colors, Table: table));

        // Annotations: the views' own marks, then each module's (grids, dimensions, texts), over the drawing.
        var ctx = new ViewContext(m, view, spec.Kind, spec.Frame, scale, DraftingResources.Book(m), drawing);
        var marks = Marks(ctx).Concat(Location.Marks(ctx)).ToList();
        foreach (var source in q.Services.All<IViewAnnotations>())
            try { marks.AddRange(source.Marks(ctx)); }
            catch (CommandException) { }
        var markPens = new Dictionary<string, Pen>();
        foreach (var mk in marks)
        {
            foreach (var (loops, color) in mk.Fills)
            {
                var key = "FILL:" + color;
                if (!markPens.TryGetValue(key, out var pen)) markPens[key] = pen = new Pen("DRAFT-FILL", null, 0, color, null);
                pen.Loops.AddRange(loops);
            }
            foreach (var (la, lb, layer, width, color, dash) in mk.Lines)
            {
                if (layer == "PICK" || (print && layer == ConstructionLayer)) continue;
                var key = $"{layer}:{color}:{width}:{(dash == null ? "" : string.Join(" ", dash))}";
                if (!markPens.TryGetValue(key, out var pen)) markPens[key] = pen = new Pen(layer, color, width, null, dash);
                pen.Segments.Add((la, lb));
            }
            foreach (var (t, color) in mk.Texts)
            {
                var key = "TEXT:" + color;
                if (!markPens.TryGetValue(key, out var pen)) markPens[key] = pen = new Pen("TEXT", color, 0, null, null);
                pen.Texts.Add(t);
            }
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
        return new Rendered(pens, marks, bounds, scale, spec.Kind, null, spec.Kind == "plan" ? ViewRanges.Problems(Range(m, view)) : [], lineworkOrphans);
    }

    /// <summary>The view drawn, for the interface: its pens, annotations, bounds and problems.</summary>
    public static JsonObject Drawing(IQueryContext q, Entity view)
    {
        var r = Render(q, view);
        if (r.Problem != null) return new JsonObject { ["problem"] = r.Problem, ["pens"] = new JsonArray() };
        var o = new JsonObject
        {
            ["kind"] = r.Kind,
            ["scale"] = r.Scale,
            ["pens"] = new JsonArray(r.Pens.Select(p => (JsonNode)p.ToJson()).ToArray()),
            // What a click on an annotation selects, and where its points snap.
            ["marks"] = new JsonArray(r.Marks.Where(x => x.Owner != null).Select(x => (JsonNode)new JsonObject
            {
                ["id"] = x.Owner,
                ["segments"] = new JsonArray(x.Lines.SelectMany(l => new[] { l.A.X, l.A.Y, l.B.X, l.B.Y }).Select(v => (JsonNode)Math.Round(v, 5)).ToArray()),
                ["snaps"] = new JsonArray(x.Snaps.SelectMany(p => new[] { p.X, p.Y }).Select(v => (JsonNode)Math.Round(v, 5)).ToArray()),
                ["shapes"] = x.Shapes.Count > 0 ? new JsonArray(x.Shapes.Select(sh => (JsonNode)sh.DeepClone()).ToArray()) : null,
            }).ToArray()),
        };
        if (r.Bounds is { } b) o["bounds"] = new JsonArray(b.Min.X, b.Min.Y, b.Max.X, b.Max.Y);
        if (r.Kind == "plan") o["problems"] = new JsonArray(r.RangeProblems.Select(x => (JsonNode)x).ToArray());
        if (r.LineworkOrphans is { Count: > 0 } lo) o["lineworkOrphans"] = new JsonArray(lo.Select(x => (JsonNode)x).ToArray());
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
                if (v.Attrs["start"] is not JsonArray s || v.Attrs["end"] is not JsonArray e) continue;
                var (a, b) = (new Vec2(Json.Number(s[0]) ?? 0, Json.Number(s[1]) ?? 0), new Vec2(Json.Number(e[0]) ?? 0, Json.Number(e[1]) ?? 0));
                var len = (b - a).Length;
                if (len < 1e-6) continue;
                var u = (b - a) * (1 / len);
                var look = u.Perp;
                var mk = new ViewMark(v.Id);
                var (stroke, arrow, head) = (Math.Min(0.8 * k, len / 3), 0.7 * k, 0.25 * k);
                mk.Line(a, b, "SECTION", Pens.Default.Hatch, dash: LineStyles.DashOf("center"));
                mk.Line(a, a + u * stroke, "SECTION", Pens.Default.Cut).Line(b, b - u * stroke, "SECTION", Pens.Default.Cut);
                foreach (var end in new[] { a, b })
                {
                    var tip = end + look * arrow;
                    mk.Line(end, tip, "SECTION", Pens.Default.Seen)
                      .Line(tip, tip - look * head + look.Perp * (head * 0.5), "SECTION", Pens.Default.Seen)
                      .Line(tip, tip - look * head - look.Perp * (head * 0.5), "SECTION", Pens.Default.Seen)
                      .Text(new PenText(tip + look * (0.35 * k), v.Name, text.Height, Baseline: "middle", Font: text.Font));
                }
                mk.Snaps.AddRange([a, b]);
                yield return mk;
            }
        }
        else if (c.Drawing.Bounds is { } bounds)
        {
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
        ], "#555555", Kind: "view"));

        r.Command("view.create", "New view", (ctx, a) =>
        {
            var kind = (string?)a["kind"] ?? "plan";
            var attrs = new JsonObject { ["kind"] = kind, ["scale"] = Json.Number(a["scale"]) ?? 100 };
            string name;
            switch (kind)
            {
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
                    attrs["start"] = P2(a["start"], "start");
                    attrs["end"] = P2(a["end"], "end");
                    if (Json.Number(a["depth"]) is { } d) attrs["depth"] = d;
                    name = (string?)a["name"] ?? $"Section {ctx.Model.OfClass(Class).Count(v => v.Str("kind") == "section") + 1}";
                    break;
                case "elevation":
                    var dir = (string?)a["direction"] ?? "south";
                    if (!Directions.Contains(dir)) throw new CommandException("An elevation looks north, south, east or west.");
                    attrs["direction"] = dir;
                    name = (string?)a["name"] ?? $"{char.ToUpperInvariant(dir[0])}{dir[1..]} elevation";
                    break;
                default: throw new CommandException("A view is a plan, a section or an elevation.");
            }
            var v = new Entity(ctx.NewId(), Class, name, attrs);
            ctx.Add(v);
            return new JsonObject { ["id"] = v.Id };
        });

        // Move a section's line.
        r.Command("view.setLine", "Move section", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x && x.Str("kind") == "section" ? x : throw new CommandException("Not a section.");
            var attrs = v.CloneAttrs();
            if (a["start"] != null) attrs["start"] = P2(a["start"], "start");
            if (a["end"] != null) attrs["end"] = P2(a["end"], "end");
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

        r.Command("view.setRange", "View range", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x && x.Str("kind") == "plan" ? x : throw new CommandException("Not a plan.");
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
