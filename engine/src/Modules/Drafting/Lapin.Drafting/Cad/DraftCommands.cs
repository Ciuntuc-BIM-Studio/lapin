using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Drawing.Hatch;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting.Cad;

/// <summary>The CAD commands: draw, change, move / copy / rotate / mirror / scale, offset, trim, extend, fillet, chamfer, hatch, layers. Each is one undo step.</summary>
public static class DraftCommands
{
    static Vec2 P(JsonNode? n, string what = "point") => n is JsonArray a && a.Count >= 2 && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y
        ? new Vec2(x, y) : throw new CommandException($"The {what} is [x, y].");

    static Entity View(ICommandContext ctx, string? id) =>
        Views.Find(ctx.Model, id ?? "") ?? throw new CommandException("There is no such view.");

    static List<string> Ids(JsonObject a) =>
        a["ids"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToList() : (string?)a["id"] is { } one ? [one] : [];

    static Shape ShapeArg(JsonNode? n) => Shape.From(n) ?? throw new CommandException("A shape is a line {a, b}, a polyline {pts, closed}, a circle {c, r} or an arc {c, r, start, end}.");

    public static void Register(IModuleRegistry r)
    {
        // One or more shapes on a view, on a layer.
        r.Command("draw", "Draw", (ctx, a) =>
        {
            var view = View(ctx, (string?)a["viewId"]);
            var layer = (string?)a["layer"] ?? "0";
            if (Drafts.Layers(ctx.Model).All(l => l.Name != layer)) throw new CommandException($"There is no layer „{layer}”.");
            if (Drafts.LayerOf(ctx.Model, layer).Locked) throw new CommandException($"The layer „{layer}” is locked.");
            var shapes = a["shapes"] is JsonArray arr ? arr.Select(ShapeArg).ToList() : [ShapeArg(a["shape"])];
            var role = Drafts.CheckRole(a["role"]);
            var ids = shapes.Select(s => Drafts.Add(ctx, view.Id, layer, s, role).Id).ToList();
            return new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()) };
        });

        // A shape replaced (a grip moved), moved to another layer, or given its own colour.
        r.Command("updateDraft", "Change", (ctx, a) =>
        {
            foreach (var id in Ids(a))
            {
                var e = Drafts.Require(ctx.Model, id);
                var attrs = e.CloneAttrs();
                if (a["shape"] != null) attrs["shape"] = ShapeArg(a["shape"]).ToJson();
                if ((string?)a["layer"] is { } layer)
                {
                    if (Drafts.Layers(ctx.Model).All(l => l.Name != layer)) throw new CommandException($"There is no layer „{layer}”.");
                    attrs["layer"] = layer;
                }
                if (a.ContainsKey("color")) { if ((string?)a["color"] is { } c) attrs["color"] = c; else attrs.Remove("color"); }
                if (a.ContainsKey("role")) { if (Drafts.CheckRole(a["role"]) is { } role) attrs["role"] = role; else attrs.Remove("role"); }
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        // Move, copy, rotate, mirror, scale: drafted shapes and texts. A copy returns the new ids.
        r.Command("transform", "Transform", (ctx, a) =>
        {
            var op = (string?)a["op"] ?? "move";
            Func<Vec2, Vec2> point;
            Func<Shape, Shape> shape;
            double turn = 0;
            switch (op)
            {
                case "move" or "copy":
                {
                    var d = P(a["by"], "displacement");
                    point = p => p + d; shape = s => Geo.Translate(s, d);
                    break;
                }
                case "rotate":
                {
                    var c = P(a["center"], "centre");
                    var ang = (Json.Number(a["angle"]) ?? 0) * Math.PI / 180;
                    turn = ang;
                    point = p => Geo.Rotate(p, c, ang); shape = s => Geo.Rotate(s, c, ang);
                    break;
                }
                case "mirror":
                {
                    var (p1, p2) = (P(a["a"]), P(a["b"]));
                    if ((p2 - p1).Length < 1e-9) throw new CommandException("A mirror line needs two different points.");
                    point = p => Geo.Mirror(p, p1, p2); shape = s => Geo.Mirror(s, p1, p2);
                    break;
                }
                case "scale":
                {
                    var c = P(a["center"], "centre");
                    var k = Json.Number(a["factor"]) ?? 1;
                    if (!(Math.Abs(k) > 1e-9)) throw new CommandException("The scale factor cannot be 0.");
                    point = p => c + (p - c) * k; shape = s => Geo.Scale(s, c, k);
                    break;
                }
                default: throw new CommandException($"„{op}” is not move, copy, rotate, mirror or scale.");
            }
            var copy = op == "copy" || (a["copy"] is JsonValue cv && cv.TryGetValue<bool>(out var cb) && cb);
            var made = new JsonArray();
            foreach (var id in Ids(a))
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
                if (e.Class != IfcClass.Annotation) continue; // model elements move by their own tools
                var attrs = e.CloneAttrs();
                if (e.PredefinedType == Drafts.Predefined)
                {
                    Drafts.Require(ctx.Model, id);
                    if (Drafts.ShapeOf(e) is { } s) attrs["shape"] = shape(s).ToJson();
                    if (e.Attrs["hatch"] is JsonObject h)
                    {
                        var loops = Drafts.HatchLoops(e).Select(l => (JsonNode)new JsonArray(l.Select(q => (JsonNode)new JsonArray(point(q).X, point(q).Y)).ToArray())).ToArray();
                        var nh = (JsonObject)h.DeepClone();
                        nh["loops"] = new JsonArray(loops);
                        attrs["hatch"] = nh;
                    }
                }
                else if (e.PredefinedType == "TEXT" && attrs["at"] is JsonArray at)
                {
                    var q = point(new Vec2(Json.Number(at[0]) ?? 0, Json.Number(at[1]) ?? 0));
                    attrs["at"] = new JsonArray(q.X, q.Y);
                    attrs["angle"] = (Json.Number(attrs["angle"]) ?? 0) + turn * 180 / Math.PI;
                }
                else if (e.PredefinedType == "DIMENSION")
                {
                    foreach (var k in new[] { "a", "b", "at" })
                        if (attrs[k] is JsonArray pa) { var q = point(new Vec2(Json.Number(pa[0]) ?? 0, Json.Number(pa[1]) ?? 0)); attrs[k] = new JsonArray(q.X, q.Y); }
                    // A moved dimension measures where it is now: it lets go of its elements.
                    attrs.Remove("refA"); attrs.Remove("refB");
                }
                else continue;
                if (copy)
                {
                    var n = new Entity(ctx.NewId(), e.Class, e.Name, attrs, e.PredefinedType);
                    ctx.Add(n);
                    made.Add((JsonNode)n.Id);
                }
                else ctx.Replace(e.With(attrs: attrs));
            }
            return new JsonObject { ["ids"] = made };
        });

        r.Command("offset", "Offset", (ctx, a) =>
        {
            var e = Drafts.Require(ctx.Model, (string?)a["id"]);
            var d = Json.Number(a["distance"]) ?? throw new CommandException("Offset by how much?");
            if (d <= 0) throw new CommandException("The offset distance must be positive.");
            var s = Drafts.ShapeOf(e) ?? throw new CommandException("Only lines, polylines, circles and arcs offset.");
            var o = Ops.Offset(s, d, P(a["side"], "side")) ?? throw new CommandException("The offset would collapse the shape.");
            return new JsonObject { ["id"] = Drafts.Add(ctx, e.Str("viewId")!, e.Str("layer"), o).Id };
        });

        // The piece under the pick goes, between the shapes and the drawing's lines that cross it.
        r.Command("trim", "Trim", (ctx, a) =>
        {
            var e = Drafts.Require(ctx.Model, (string?)a["id"]);
            var s = Drafts.ShapeOf(e) ?? throw new CommandException("Only lines, polylines, circles and arcs trim.");
            var view = View(ctx, e.Str("viewId"));
            var pieces = Ops.Trim(s, Drafts.Boundaries(ctx, view, e.Id), P(a["at"], "pick")) ?? throw new CommandException("Nothing crosses it there.");
            if (pieces.Count == 0) { ctx.Remove(e.Id); return new JsonObject { ["ids"] = new JsonArray() }; }
            Drafts.Set(ctx, e, pieces[0]);
            var ids = new JsonArray(e.Id);
            foreach (var p in pieces.Skip(1))
            {
                var n = Drafts.Add(ctx, e.Str("viewId")!, e.Str("layer"), p);
                if (e.Str("color") is { } c) { var at = n.CloneAttrs(); at["color"] = c; ctx.Replace(n.With(attrs: at)); }
                ids.Add((JsonNode)n.Id);
            }
            return new JsonObject { ["ids"] = ids };
        });

        r.Command("extend", "Extend", (ctx, a) =>
        {
            var e = Drafts.Require(ctx.Model, (string?)a["id"]);
            var s = Drafts.ShapeOf(e) ?? throw new CommandException("Only lines, open polylines and arcs extend.");
            var view = View(ctx, e.Str("viewId"));
            var o = Ops.Extend(s, Drafts.Boundaries(ctx, view, e.Id), P(a["at"], "pick")) ?? throw new CommandException("It meets nothing that way.");
            Drafts.Set(ctx, e, o);
            return null;
        });

        // Two lines joined by an arc (radius 0: a sharp corner) or by a line (chamfer).
        r.Command("fillet", "Fillet", (ctx, a) => Corner(ctx, a, false));
        r.Command("chamfer", "Chamfer", (ctx, a) => Corner(ctx, a, true));

        // A hatch filling the region around a point: the smallest closed outline there, its islands left out.
        r.Command("hatch", "Hatch", (ctx, a) =>
        {
            var view = View(ctx, (string?)a["viewId"]);
            var at = P(a["at"], "point");
            var region = HatchRegion.At(at, Drafts.Loops(ctx, view)) ?? throw new CommandException("There is no closed outline around that point.");
            var pattern = ((string?)a["pattern"])?.Trim().ToUpperInvariant() is { Length: > 0 } pn ? pn : "LINES45";
            if (HatchLibrary.Find(pattern, DraftingResources.Patterns(ctx.Model)) == null) throw new CommandException($"There is no hatch pattern „{pattern}”.");
            var layer = (string?)a["layer"] ?? "0";
            var hatch = new JsonObject
            {
                ["pattern"] = pattern, ["scale"] = Json.Number(a["scale"]) ?? 1, ["angle"] = Json.Number(a["angle"]) ?? 0,
                ["loops"] = new JsonArray(region.Select(l => (JsonNode)new JsonArray(l.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray())).ToArray()),
            };
            var attrs = new JsonObject { ["viewId"] = view.Id, ["layer"] = layer, ["hatch"] = hatch };
            if ((string?)a["color"] is { } c) attrs["color"] = c;
            var e = new Entity(ctx.NewId(), IfcClass.Annotation, $"Hatch {pattern}", attrs, Drafts.Predefined);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id, ["loops"] = region.Count };
        });

        // ------------------------------------------------------------ layers

        r.Query("drafting.layers", (q, _) => new JsonArray(Drafts.Layers(q.Model).Select(l => (JsonNode)l.ToJson()).ToArray()));

        // A layer made or changed (by its name; rename with newName).
        r.Command("setLayer", "Layer", (ctx, a) =>
        {
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A layer needs a name.");
            var layers = Drafts.Layers(ctx.Model).ToList();
            var i = layers.FindIndex(l => l.Name == name);
            var cur = i >= 0 ? layers[i] : new CadLayer(name);
            var json = cur.ToJson();
            foreach (var k in new[] { "color", "weight", "lineType", "visible", "locked" }) if (a[k] != null) json[k] = a[k]!.DeepClone();
            var newName = ((string?)a["newName"])?.Trim();
            if (newName is { Length: > 0 } && newName != name)
            {
                if (name == "0") throw new CommandException("Layer 0 keeps its name.");
                if (layers.Any(l => l.Name == newName)) throw new CommandException($"There is a layer „{newName}” already.");
                json["name"] = newName;
                foreach (var e in ctx.Model.OfClass(IfcClass.Annotation).Where(e => e.PredefinedType == Drafts.Predefined && e.Str("layer") == name).ToList())
                {
                    var at = e.CloneAttrs(); at["layer"] = newName; ctx.Replace(e.With(attrs: at));
                }
            }
            var layer = CadLayer.From(json)!;
            if (i >= 0) layers[i] = layer; else layers.Add(layer);
            Drafts.WriteLayers(ctx, layers);
            return null;
        });

        // A layer removed: its shapes go to layer 0.
        r.Command("removeLayer", "Remove layer", (ctx, a) =>
        {
            var name = (string?)a["name"];
            if (name == "0") throw new CommandException("Layer 0 cannot be removed.");
            var layers = Drafts.Layers(ctx.Model).ToList();
            if (layers.RemoveAll(l => l.Name == name) == 0) throw new CommandException("There is no such layer.");
            foreach (var e in ctx.Model.OfClass(IfcClass.Annotation).Where(e => e.PredefinedType == Drafts.Predefined && e.Str("layer") == name).ToList())
            {
                var at = e.CloneAttrs(); at["layer"] = "0"; ctx.Replace(e.With(attrs: at));
            }
            Drafts.WriteLayers(ctx, layers);
            return null;
        });
    }

    static JsonNode? Corner(ICommandContext ctx, JsonObject a, bool chamfer)
    {
        var (ea, eb) = (Drafts.Require(ctx.Model, (string?)a["a"]), Drafts.Require(ctx.Model, (string?)a["b"]));
        if (Drafts.ShapeOf(ea) is not Line la || Drafts.ShapeOf(eb) is not Line lb) throw new CommandException("Fillet and chamfer join two lines.");
        var (pa, pb) = (P(a["atA"], "first pick"), P(a["atB"], "second pick"));
        if (chamfer)
        {
            var c = Ops.Chamfer(la, pa, lb, pb, Json.Number(a["d1"]) ?? 0, Json.Number(a["d2"]) ?? Json.Number(a["d1"]) ?? 0)
                ?? throw new CommandException("The lines are parallel, or too short for that chamfer.");
            Drafts.Set(ctx, ea, c.First); Drafts.Set(ctx, eb, c.Second);
            if ((c.Line.B - c.Line.A).Length > 1e-9) return new JsonObject { ["id"] = Drafts.Add(ctx, ea.Str("viewId")!, ea.Str("layer"), c.Line).Id };
            return null;
        }
        var f = Ops.Fillet(la, pa, lb, pb, Json.Number(a["radius"]) ?? 0) ?? throw new CommandException("The lines are parallel, or too short for that radius.");
        Drafts.Set(ctx, ea, f.First); Drafts.Set(ctx, eb, f.Second);
        return f.Arc is { } arc ? new JsonObject { ["id"] = Drafts.Add(ctx, ea.Str("viewId")!, ea.Str("layer"), arc).Id } : null;
    }
}
