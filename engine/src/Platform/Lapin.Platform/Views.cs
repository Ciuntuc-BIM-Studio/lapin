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
        var hiddenCats = (view.Attrs["graphics"]?["categories"] as JsonObject)?.Where(c => c.Value?["hidden"] is JsonValue h && h.TryGetValue<bool>(out var b) && b).Select(c => c.Key).ToHashSet() ?? [];
        var hiddenElements = (view.Attrs["hiddenElements"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        foreach (var e in m.Entities)
        {
            if (e.Class is IfcClass.OpeningElement || hiddenElements.Contains(e.Id)) continue;
            if (LineStyles.CategoryOf(e.Class) is { } cat && hiddenCats.Contains(cat)) continue;
            if (q.Geometry(e.Id) is not { } g) continue;
            foreach (var p in g.Parts) parts.Add(new DrawPart(p.Solid, e.Id, e.Class, p.Key, p.Material));
            if (view.Str("kind") == "plan" && Generator.SymbolicInPlan.Contains(e.Class) && g.Plan != null && Spatial.StoreyOf(m, e.Id)?.Id is { } s && s == storey)
                foreach (var item in g.Plan)
                {
                    var role = item.Kind switch { "symbol" => "projection", "overhead" or "ridge" or "hip" or "valley" or "break" => "overhead", _ => null };
                    if (role != null) symbols.Add(new DrawSymbol(item.Points, item.Closed, e.Id, e.Class, role));
                }
        }
        return (parts, symbols);
    }

    /// <summary>The view drawn: its pens, bounds and problems.</summary>
    public static JsonObject Drawing(IQueryContext q, Entity view)
    {
        var m = q.Model;
        var (parts, symbols) = Gather(q, view);
        var (spec, problem) = Spec(m, view, parts);
        if (spec == null) return new JsonObject { ["problem"] = problem, ["pens"] = new JsonArray() };
        var drawing = Generator.Draw(parts, spec, symbols);
        var kind = spec.Kind == "plan" ? "plan" : spec.Kind == "elevation" ? "elevation" : "section";
        var materials = MaterialDrawing.Apply(drawing, p => p.Material != null && m.Get(p.Material) is { } mat ? MaterialGraphics.Resolve(mat, kind) : null);
        var project = Spatial.Project(m);
        var scale = view.NumOrNull("scale") ?? 100;
        var graphics = view.Attrs["graphics"] as JsonObject;
        var categories = (graphics?["categories"] as JsonObject)?.Select(c => (c.Key, G: CategoryGraphics.From(c.Value))).Where(x => x.G != null).ToDictionary(x => x.Key, x => x.G!);
        var styles = LineStyles.For(project?.Attrs["objectStyles"] as JsonObject, graphics?["categories"] as JsonObject, graphics?["elements"] as JsonObject, null, scale);
        var pens = Pens.Of(drawing, materials, new Pens.Options(scale, spec.Hidden, styles, categories, DraftingResources.Patterns(m)));
        var o = new JsonObject
        {
            ["kind"] = spec.Kind,
            ["scale"] = scale,
            ["pens"] = new JsonArray(pens.Select(p => (JsonNode)p.ToJson()).ToArray()),
        };
        if (drawing.Bounds is { } b) o["bounds"] = new JsonArray(b.Min.X, b.Min.Y, b.Max.X, b.Max.Y);
        if (spec.Kind == "plan") o["problems"] = new JsonArray(ViewRanges.Problems(Range(m, view)).Select(x => (JsonNode)x).ToArray());
        return o;
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
        r.Command("view.graphics", "View graphics", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("Not a view.");
            var attrs = v.CloneAttrs();
            var g = attrs["graphics"] as JsonObject ?? new JsonObject();
            void Set(string bucket, string? key)
            {
                if (key == null) return;
                var b = g[bucket] as JsonObject ?? new JsonObject();
                if (a["value"] is JsonObject val) b[key] = val.DeepClone(); else b.Remove(key);
                if (b.Count > 0) g[bucket] = b; else g.Remove(bucket);
            }
            if ((string?)a["category"] is { } cat)
            {
                if (!LineStyles.Categories.Contains(cat)) throw new CommandException($"„{cat}” is not a category.");
                Set("categories", cat);
            }
            Set("elements", (string?)a["element"]);
            if (g.Count > 0) attrs["graphics"] = g; else attrs.Remove("graphics");
            ctx.Replace(v.With(attrs: attrs));
            return null;
        });

        // The project's object styles: one role of a category ({pen, color, pattern}; null clears it), and its defaults for hidden lines by kind.
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
