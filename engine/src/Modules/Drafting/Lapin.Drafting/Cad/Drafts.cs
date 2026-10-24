using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting.Cad;

/// <summary>A drafting layer: its colour, pen width (paper mm), line type, and whether it shows and can be edited.</summary>
public sealed record CadLayer(string Name, string Color = "#18181b", double Weight = 0.25, string LineType = "continuous", bool Visible = true, bool Locked = false)
{
    public static readonly string[] LineTypes = ["continuous", "dashed", "dotted", "dashdot"];

    /// <summary>The line type's dashes on paper (mm).</summary>
    public double[]? Dash => Dashes.Of(LineType) is { Length: > 0 } d ? d : null;

    public JsonObject ToJson() => new() { ["name"] = Name, ["color"] = Color, ["weight"] = Weight, ["lineType"] = LineType, ["visible"] = Visible, ["locked"] = Locked };

    public static CadLayer? From(JsonNode? n)
    {
        if (n is not JsonObject o || ((string?)o["name"])?.Trim() is not { Length: > 0 } name) return null;
        static bool B(JsonNode? x, bool d) => x is JsonValue v && v.TryGetValue<bool>(out var b) ? b : d;
        var lt = (string?)o["lineType"] is { } l && LineTypes.Contains(l) ? l : "continuous";
        return new CadLayer(name, (string?)o["color"] ?? "#18181b", Math.Clamp(Json.Number(o["weight"]) ?? 0.25, 0.05, 2), lt, B(o["visible"], true), B(o["locked"], false));
    }
}

/// <summary>
/// CAD drafting on a view: lines, polylines, circles, arcs and hatches as
/// IfcAnnotation (DRAFT) on a view, each on a drafting layer (the project's
/// <c>draftLayers</c>; "0" always there). They draw over the view's drawing
/// and on its sheets, and export with it (DXF on their layer).
/// </summary>
public static class Drafts
{
    public const string Predefined = "DRAFT";

    public static IReadOnlyList<CadLayer> Layers(IModelView m)
    {
        var list = (Spatial.Project(m)?.Attrs["draftLayers"] as JsonArray)?.Select(CadLayer.From).OfType<CadLayer>().ToList() ?? [];
        if (list.All(l => l.Name != "0")) list.Insert(0, new CadLayer("0"));
        return list;
    }

    public static CadLayer LayerOf(IModelView m, string? name) => Layers(m).FirstOrDefault(l => l.Name == name) ?? Layers(m)[0];

    public static void WriteLayers(ICommandContext ctx, IEnumerable<CadLayer> layers)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        attrs["draftLayers"] = new JsonArray(layers.Select(l => (JsonNode)l.ToJson()).ToArray());
        ctx.Replace(project.With(attrs: attrs));
    }

    public static IEnumerable<Entity> On(IModelView m, string viewId) =>
        m.OfClass(IfcClass.Annotation).Where(e => e.PredefinedType == Predefined && e.Str("viewId") == viewId);

    public static Shape? ShapeOf(Entity e) => Shape.From(e.Attrs["shape"]);

    public static Entity Require(IModelView m, string? id, bool editable = true)
    {
        var e = m.Get(id ?? "") is { Class: IfcClass.Annotation, PredefinedType: Predefined } x ? x : throw new CommandException("Not a drafted shape.");
        if (editable && LayerOf(m, e.Str("layer")).Locked) throw new CommandException($"The layer „{e.Str("layer")}” is locked.");
        return e;
    }

    public static Entity Add(ICommandContext ctx, string viewId, string? layer, Shape shape)
    {
        var attrs = new JsonObject { ["viewId"] = viewId, ["layer"] = layer ?? "0", ["shape"] = shape.ToJson() };
        var e = new Entity(ctx.NewId(), IfcClass.Annotation, Name(shape), attrs, Predefined);
        ctx.Add(e);
        return e;
    }

    public static void Set(ICommandContext ctx, Entity e, Shape shape)
    {
        var attrs = e.CloneAttrs();
        attrs["shape"] = shape.ToJson();
        ctx.Replace(e.With(name: Name(shape), attrs: attrs));
    }

    static string Name(Shape s) => s switch { Line => "Line", Polyline { Closed: true } => "Polygon", Polyline => "Polyline", Circle => "Circle", Arc => "Arc", _ => "Shape" };

    /// <summary>What drafting edits cut against on a view: its other shapes and the lines of its drawing.</summary>
    public static List<Shape> Boundaries(ICommandContext ctx, Entity view, string exceptId)
    {
        var o = On(ctx.Model, view.Id).Where(e => e.Id != exceptId).Select(ShapeOf).OfType<Shape>().ToList();
        if (Views.Generate(ctx.Query, view).Drawing is { } d)
        {
            o.AddRange(d.Lines.Select(l => (Shape)new Line(l.A, l.B)));
            o.AddRange(d.Cuts.SelectMany(c => c.Loops).Select(l => (Shape)new Polyline(l, true)));
        }
        return o;
    }

    /// <summary>The loops a hatch can fill on a view: closed shapes and the drawing's cut areas.</summary>
    public static List<IReadOnlyList<Vec2>> Loops(ICommandContext ctx, Entity view)
    {
        var loops = new List<IReadOnlyList<Vec2>>();
        foreach (var s in On(ctx.Model, view.Id).Where(e => e.Attrs["hatch"] == null).Select(ShapeOf))
            switch (s)
            {
                case Polyline { Closed: true } p when p.Pts.Count >= 3: loops.Add(p.Pts); break;
                case Circle c: loops.Add(Geo.Polylines(c)[0].SkipLast(1).ToList()); break;
            }
        if (Views.Generate(ctx.Query, view).Drawing is { } d) loops.AddRange(d.Cuts.SelectMany(c => c.Loops).Select(l => (IReadOnlyList<Vec2>)l));
        return loops;
    }

    public static IReadOnlyList<IReadOnlyList<Vec2>> HatchLoops(Entity e) =>
        (e.Attrs["hatch"]?["loops"] as JsonArray)?.OfType<JsonArray>()
            .Select(l => (IReadOnlyList<Vec2>)l.OfType<JsonArray>().Select(p => new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0)).ToList()).ToList() ?? [];

    /// <summary>The view's drafted shapes, on their layers' pens.</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        var layers = Layers(c.Model).ToDictionary(l => l.Name);
        var hidden = (c.View.Attrs["hiddenLayers"] as JsonArray)?.Select(x => (string?)x).ToHashSet() ?? [];
        var patterns = DraftingResources.Patterns(c.Model);
        foreach (var e in On(c.Model, c.View.Id))
        {
            var layer = layers.GetValueOrDefault(e.Str("layer") ?? "0") ?? layers["0"];
            if (!layer.Visible || hidden.Contains(layer.Name)) continue;
            var color = e.Str("color") ?? layer.Color;
            var mk = new ViewMark(e.Id);
            if (e.Attrs["hatch"] is JsonObject h)
            {
                var loops = HatchLoops(e);
                if (loops.Count == 0) continue;
                var pattern = HatchLibrary.Find((string?)h["pattern"] ?? "LINES45", patterns);
                if (pattern == null || pattern.Solid) mk.Fills.Add((loops, color));
                else
                {
                    var scale = Json.Number(h["scale"]) ?? 1;
                    var (segs, _) = HatchFill.Segments(loops, pattern, new HatchOptions(scale * c.Mm, Json.Number(h["angle"]) ?? 0, MaxSegments: 20_000));
                    foreach (var s in segs) mk.Line(s.A, s.B, layer.Name, Pens.Default.Hatch, color);
                }
                foreach (var l in loops) mk.Shapes.Add(new Polyline(l, true).ToJson().Also(j => j["hatch"] = true));
                mk.Snaps.AddRange(loops.SelectMany(l => l));
                yield return mk;
                continue;
            }
            if (ShapeOf(e) is not { } shape) continue;
            foreach (var line in Geo.Polylines(shape)) mk.Polyline(line, false, layer.Name, layer.Weight, color, layer.Dash);
            var k = Geo.Keys(shape);
            mk.Snaps.AddRange([.. k.Endpoints, .. k.Midpoints, .. k.Centers, .. k.Quadrants]);
            mk.Shapes.Add(shape.ToJson());
            yield return mk;
        }
    }

    static JsonObject Also(this JsonObject o, Action<JsonObject> f) { f(o); return o; }
}
