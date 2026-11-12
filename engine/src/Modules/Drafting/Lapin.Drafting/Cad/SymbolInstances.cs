using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting.Cad;

/// <summary>
/// Symbols placed on views and sheets (docs/plan-user-extensibility.md §3.1–3.2): IfcAnnotation <c>SYMBOL</c>, drawn by
/// <see cref="Symbols"/> on their drafting layer; drafted shapes and texts saved as a symbol (Save as symbol…), an instance
/// exploded back into drafting. Each command is one undo step.
/// </summary>
public static class SymbolInstances
{
    public const string Predefined = Symbols.Predefined;

    /// <summary>The view's symbols, on their layers' pens (a hidden layer hides them, a locked one keeps them where they are).</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        var layers = Drafts.Layers(c.Model).ToDictionary(l => l.Name);
        var hidden = (c.View.Attrs["hiddenLayers"] as JsonArray)?.Select(x => (string?)x).ToHashSet() ?? [];
        foreach (var e in Symbols.Instances(c.Model).Where(e => e.Str("viewId") == c.View.Id))
        {
            var layer = layers.GetValueOrDefault(e.Str("layer") ?? "0") ?? layers["0"];
            if (!layer.Visible || hidden.Contains(layer.Name)) continue;
            if (Symbols.Mark(c, e, layer.Name, layer.Color) is { } mk) yield return mk;
        }
    }

    static Entity Require(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: IfcClass.Annotation, PredefinedType: Predefined } e ? e : throw new CommandException("Not a placed symbol.");

    static List<string> Ids(JsonObject a) =>
        a["ids"] is JsonArray arr ? [.. arr.Select(x => (string?)x).OfType<string>()] : (string?)a["id"] is { Length: > 0 } id ? [id] : throw new CommandException("Which symbols?");

    static Vec2 P(JsonNode? n, string what) =>
        n is JsonArray a && a.Count >= 2 && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y && double.IsFinite(x) && double.IsFinite(y) ? new Vec2(x, y) : throw new CommandException($"{what} is [x, y].");

    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));

    static bool? B(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    /// <summary>An instance's pose and look from a command's arguments, over <paramref name="attrs"/>; what it does not name stays.</summary>
    static void Apply(IModelView m, JsonObject attrs, JsonObject a)
    {
        if ((string?)a["symbolId"] is { Length: > 0 } sid)
        {
            if (Symbols.DefOf(m, sid) == null) throw new CommandException("There is no such symbol.");
            attrs["symbolId"] = sid;
        }
        if (a.ContainsKey("at")) attrs["at"] = J(P(a["at"], "Where it stands"));
        if (Json.Number(a["angle"]) is { } angle) attrs["angle"] = Math.Round(((angle % 360) + 360) % 360, 9);
        if (a.ContainsKey("scale"))
            attrs["scale"] = Json.Number(a["scale"]) is > 0 and <= 1000 and var s ? s : throw new CommandException("A symbol's scale is above 0 and at most 1000.");
        if (B(a["flipX"]) is { } fx) attrs["flipX"] = fx;
        if (B(a["flipY"]) is { } fy) attrs["flipY"] = fy;
        if (a.ContainsKey("layer"))
        {
            var layer = (string?)a["layer"] is { Length: > 0 } l ? l : "0";
            if (Drafts.Layers(m).All(x => x.Name != layer)) throw new CommandException($"There is no drafting layer „{layer}”.");
            attrs["layer"] = layer;
        }
        if (a.ContainsKey("color"))
        {
            if ((string?)a["color"] is { Length: > 0 } col) attrs["color"] = col; else attrs.Remove("color");
        }
        // Attribute values merged: a key given null takes the symbol's default back.
        if (a["values"] is JsonObject values)
        {
            var own = attrs["values"] as JsonObject ?? new JsonObject();
            foreach (var (k, v) in values)
            {
                if (v == null) own.Remove(k);
                else own[k] = v.ToString();
            }
            if (own.Count > 0) attrs["values"] = own.DeepClone(); else attrs.Remove("values");
        }
    }

    // ------------------------------------------------------------ drafting → symbol

    /// <summary>
    /// Drafted shapes and texts of a view as a symbol's items, in its mm: seen from <paramref name="base"/>, its +x along
    /// <paramref name="angle"/> (radians); an annotation symbol's mm the view's paper mm, a model symbol's the building's.
    /// Construction lines are left out. Returns the items and the attributes the texts name (<c>{attr.key}</c>).
    /// </summary>
    public static (JsonArray Items, List<SymbolAttribute> Attributes) ItemsOf(IModelView m, Entity view, IEnumerable<Entity> sources, Vec2 @base, double angle, bool model)
    {
        var mm = (view.NumOrNull("scale") ?? 100) / 1000;
        var unit = model ? 0.001 : mm;
        var (c, s) = (Math.Cos(-angle), Math.Sin(-angle));
        Vec2 L(Vec2 p) { var d = p - @base; return new Vec2(d.X * c - d.Y * s, d.X * s + d.Y * c) * (1 / unit); }
        static JsonNode N(double v) => JsonValue.Create(Math.Round(v, 4))!;
        JsonArray Pt(Vec2 p) { var q = L(p); return new JsonArray(N(q.X), N(q.Y)); }
        double Deg(double rad) => ((rad - angle) * 180 / Math.PI % 360 + 360) % 360;
        var book = DraftingResources.Book(m);
        var items = new JsonArray();
        var keys = new List<string>();
        foreach (var e in sources)
        {
            if (e.Class != IfcClass.Annotation) continue;
            if (e.PredefinedType == Drafts.Predefined)
            {
                if (e.Str("role") == "draft") continue;
                var layer = Drafts.LayerOf(m, e.Str("layer"));
                var color = e.Str("color");
                JsonObject Pen(JsonObject o, string widthKey = "stroke")
                {
                    o[widthKey] = layer.Weight;
                    if (color != null) o["color"] = color;
                    if (layer.Dash is { } dash) o["dash"] = new JsonArray(dash.Select(d => (JsonNode)d).ToArray());
                    return o;
                }
                if (e.Attrs["hatch"] is JsonObject h)
                {
                    var pattern = (string?)h["pattern"] ?? "LINES45";
                    var solid = HatchLibrary.Find(pattern, DraftingResources.Patterns(m)) is null or { Solid: true };
                    foreach (var loop in Drafts.HatchLoops(e))
                    {
                        var o = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(loop.Select(p => (JsonNode)Pt(p)).ToArray()), ["closed"] = true, ["stroke"] = 0 };
                        if (solid) o["fill"] = color ?? "byInstance";
                        else
                        {
                            var hatch = new JsonObject { ["pattern"] = pattern, ["scale"] = Json.Number(h["scale"]) ?? 1, ["angle"] = Math.Round(((Json.Number(h["angle"]) ?? 0) - angle * 180 / Math.PI) % 360, 6) };
                            if (color != null) hatch["color"] = color;
                            o["hatch"] = hatch;
                        }
                        items.Add((JsonNode)o);
                    }
                    continue;
                }
                switch (Drafts.ShapeOf(e))
                {
                    case Line l: items.Add((JsonNode)Pen(new JsonObject { ["type"] = "line", ["a"] = Pt(l.A), ["b"] = Pt(l.B) }, "w")); break;
                    case Polyline p: items.Add((JsonNode)Pen(new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(p.Pts.Select(q => (JsonNode)Pt(q)).ToArray()), ["closed"] = p.Closed })); break;
                    case Circle ci: items.Add((JsonNode)Pen(new JsonObject { ["type"] = "circle", ["c"] = Pt(ci.C), ["r"] = N(ci.R / unit) })); break;
                    case Arc ar: items.Add((JsonNode)Pen(new JsonObject { ["type"] = "arc", ["c"] = Pt(ar.C), ["r"] = N(ar.R / unit), ["start"] = N(Deg(ar.Start)), ["end"] = N(Deg(ar.End)) }, "w")); break;
                }
            }
            else if (e.PredefinedType == "TEXT")
            {
                var style = Standards.ResolveText(book, Dimensions.ParamsOf(e));
                var text = e.Str("text") ?? "";
                var at = e.Attrs["at"] is JsonArray aa ? new Vec2(Json.Number(aa[0]) ?? 0, Json.Number(aa[1]) ?? 0) : default;
                // A text's height is paper mm: a model symbol's mm are the building's.
                var h = model ? style.Height * mm * 1000 : style.Height;
                var o = new JsonObject
                {
                    ["type"] = "text", ["at"] = Pt(at), ["text"] = text, ["h"] = N(h), ["angle"] = N(Deg(e.Num("angle") * Math.PI / 180)),
                    ["align"] = "start", ["valign"] = "bottom", ["bold"] = style.Bold, ["italic"] = style.Italic, ["font"] = style.Font,
                };
                if (style.Color != null) o["color"] = style.Color;
                items.Add((JsonNode)o);
                foreach (var token in TitleBlocks.Tokens(text).Concat(FormulaNames(text)))
                    if (token.StartsWith("attr.", StringComparison.Ordinal) && token[5..] is { Length: > 0 } k && !keys.Contains(k, StringComparer.OrdinalIgnoreCase)) keys.Add(k);
            }
        }
        return (items, [.. keys.Select(k => new SymbolAttribute(k, k, ""))]);
    }

    /// <summary>The names the formulas of a text read (<c>{= attr.w * 2}</c> → attr.w).</summary>
    static IEnumerable<string> FormulaNames(string text)
    {
        for (var i = text.IndexOf("{=", StringComparison.Ordinal); i >= 0; i = text.IndexOf("{=", i + 2, StringComparison.Ordinal))
        {
            var close = text.IndexOf('}', i);
            if (close < 0) yield break;
            var body = text[(i + 2)..close];
            var bar = body.LastIndexOf('|');
            foreach (var n in Expr.Names(bar >= 0 ? body[..bar] : body)) yield return n;
        }
    }

    // ------------------------------------------------------------ symbol → drafting

    /// <summary>An instance laid out as drafted shapes, hatches and texts on its view, as it draws (its texts readable); the instance goes.</summary>
    static List<string> Explode(ICommandContext ctx, Entity e)
    {
        var m = ctx.Model;
        var viewId = e.Str("viewId") ?? throw new CommandException("Only a symbol on a view is exploded into drafting.");
        var view = Views.Find(m, viewId) ?? throw new CommandException("The symbol's view is gone.");
        var def = Symbols.DefOf(m, e.Str("symbolId")) ?? throw new CommandException("The symbol's definition is gone.");
        var (at, angle, scale, fx, fy) = Symbols.Pose(e);
        var mm = (view.NumOrNull("scale") ?? 100) / 1000;
        var model = def.Scale == "model";
        var unit = (model ? 0.001 : mm) * scale;
        var x = new SymbolXf(at, angle * Math.PI / 180, unit, model ? scale * 0.001 / mm : scale, fx, fy);
        var layer = e.Str("layer") ?? "0";
        var color = e.Str("color");
        var made = new List<string>();
        Entity Add(Shape s)
        {
            var d = Drafts.Add(ctx, viewId, layer, s);
            if (color != null) { var at2 = d.CloneAttrs(); at2["color"] = color; d = d.With(attrs: at2); ctx.Replace(d); }
            made.Add(d.Id);
            return d;
        }
        static Vec2 Pnt(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
        // Circles and arcs stay circles and arcs; every other item as it draws.
        var rest = new JsonArray();
        foreach (var item in def.Items.OfType<JsonObject>())
        {
            var type = (string?)item["type"];
            if (type == "circle" && (string?)item["fill"] is null or "" or "none" && item["hatch"] == null && Json.Number(item["r"]) is > 0 and var r)
            {
                Add(new Circle(x.Point(Pnt(item["c"])), r * unit));
                continue;
            }
            if (type == "arc" && Json.Number(item["r"]) is > 0 and var ra)
            {
                var (s0, s1) = ((Json.Number(item["start"]) ?? 0) * Math.PI / 180, (Json.Number(item["end"]) ?? 0) * Math.PI / 180);
                var d0 = x.Dir(new Vec2(Math.Cos(s0), Math.Sin(s0)));
                var d1 = x.Dir(new Vec2(Math.Cos(s1), Math.Sin(s1)));
                var (a0, a1) = (Math.Atan2(d0.Y, d0.X), Math.Atan2(d1.Y, d1.X));
                // Turned over, an arc runs the other way round: its ends swap.
                Add(x.Mirrors ? new Arc(x.Point(Pnt(item["c"])), ra * unit, Geo.NormAngle(a1), Geo.NormAngle(a0)) : new Arc(x.Point(Pnt(item["c"])), ra * unit, Geo.NormAngle(a0), Geo.NormAngle(a1)));
                continue;
            }
            rest.Add((JsonNode)item.DeepClone());
        }
        var sd = SymbolRender.Render(m, def with { Items = rest }, Symbols.Resolver(m, def, e, view, at), unit);
        foreach (var s in sd.Strokes)
        {
            var pts = s.Pts.Select(x.Point).ToList();
            Add(pts.Count == 2 && !s.Closed ? new Line(pts[0], pts[1]) : new Polyline(pts, s.Closed));
        }
        foreach (var f in sd.Fills.Select(f => (f.Loops, Pattern: "SOLID", Scale: 1.0, Angle: 0.0, f.Color)).Concat(sd.Hatches.Select(h => (h.Loops, h.Pattern, h.Scale, Angle: h.Angle + angle, h.Color))))
        {
            var loops = f.Loops.Select(l => l.Select(x.Point).ToList()).ToList();
            var attrs = new JsonObject
            {
                ["viewId"] = viewId, ["layer"] = layer, ["shape"] = new Polyline(loops[0], true).ToJson(),
                ["hatch"] = new JsonObject { ["pattern"] = f.Pattern, ["scale"] = f.Scale, ["angle"] = f.Angle, ["loops"] = new JsonArray(loops.Select(l => (JsonNode)new JsonArray(l.Select(p => (JsonNode)J(p)).ToArray())).ToArray()) },
            };
            if ((f.Color ?? color) is { } hc) attrs["color"] = hc;
            var hatch = new Entity(ctx.NewId(), IfcClass.Annotation, Drafts.UniqueName(m, viewId, "Hatch"), attrs, Drafts.Predefined);
            ctx.Add(hatch);
            made.Add(hatch.Id);
        }
        foreach (var t in sd.Texts)
        {
            var (p, a, anchor, height) = SymbolRender.LayText(t, x);
            // A drafted text hangs from its start, on its baseline: an end or a middle one moved back along it.
            var w = t.Text.Length * height * 0.6 * mm;
            var r = a * Math.PI / 180;
            var u = new Vec2(Math.Cos(r), Math.Sin(r));
            var n = new Vec2(-Math.Sin(r), Math.Cos(r));
            var start = p - u * (anchor == "end" ? w : anchor == "middle" ? w / 2 : 0) - n * (t.Baseline == "middle" ? height * mm / 2 : 0);
            var attrs = new JsonObject { ["viewId"] = viewId, ["at"] = J(start), ["text"] = t.Text, ["angle"] = Math.Round(a, 6), ["textHeight"] = Math.Round(height, 4) };
            var text = new Entity(ctx.NewId(), IfcClass.Annotation, "Text", attrs, "TEXT");
            ctx.Add(text);
            made.Add(text.Id);
        }
        ctx.Remove(e.Id);
        return made;
    }

    // ------------------------------------------------------------ commands

    public static void Register(IModuleRegistry r)
    {
        // A symbol placed on a view ({viewId, at: [x, y] in the view's metres}) or on a sheet ({sheetId, at: [x, y] in paper mm
        // from its top-left}): its symbol, angle (°), scale, flips, attribute values, drafting layer and colour.
        r.Command("drafting.placeSymbol", "Place symbol", (ctx, a) =>
        {
            var m = ctx.Model;
            var sid = (string?)a["symbolId"] is { Length: > 0 } s ? s : throw new CommandException("Which symbol?");
            if (Symbols.DefOf(m, sid) == null) throw new CommandException("There is no such symbol.");
            var attrs = new JsonObject { ["symbolId"] = sid, ["layer"] = "0" };
            if ((string?)a["viewId"] is { Length: > 0 } viewId)
            {
                if (Views.Find(m, viewId) is not { } view || view.Str("kind") == "3d") throw new CommandException("A symbol is placed on a plan, a section, an elevation or a drafting view.");
                attrs["viewId"] = viewId;
            }
            else if ((string?)a["sheetId"] is { Length: > 0 } sheetId)
            {
                if (m.Get(sheetId) is not { Class: Sheets.Class }) throw new CommandException("There is no such sheet.");
                attrs["sheetId"] = sheetId;
            }
            else throw new CommandException("A symbol is placed on a view or a sheet.");
            if (!a.ContainsKey("at")) throw new CommandException("Where does the symbol stand?");
            Apply(m, attrs, a);
            var e = new Entity(ctx.NewId(), IfcClass.Annotation, Symbols.NameOf(m, sid) ?? "Symbol", attrs, Predefined);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });

        // Placed symbols changed together ({ids}): another symbol, where, angle, scale, flips, values (merged), layer, colour.
        r.Command("drafting.setSymbol", "Change symbol", (ctx, a) =>
        {
            foreach (var id in Ids(a))
            {
                var e = Require(ctx.Model, id);
                var attrs = e.CloneAttrs();
                Apply(ctx.Model, attrs, a);
                var name = (string?)a["symbolId"] is { Length: > 0 } sid ? Symbols.NameOf(ctx.Model, sid) : null;
                ctx.Replace(e.With(name: name, attrs: attrs));
            }
            return null;
        });

        // Placed symbols turned over across their own x (axis "x": left ↔ right) or y ("y": up ↔ down); texts stay readable.
        r.Command("drafting.flipSymbol", "Flip symbol", (ctx, a) =>
        {
            var key = (string?)a["axis"] switch { "x" => "flipX", "y" => "flipY", _ => throw new CommandException("Flip across x (left ↔ right) or y (up ↔ down)?") };
            foreach (var id in Ids(a))
            {
                var e = Require(ctx.Model, id);
                var attrs = e.CloneAttrs();
                attrs[key] = !e.Bool(key);
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        // Drafted shapes and texts of a view saved as a symbol (Save as symbol…): {viewId, ids, base: [x, y], angle (° of its +x),
        // name, category, scale: annotation | model, attributes?, replace (default true: the drafting becomes one instance)}.
        r.Command("drafting.toSymbol", "Save as symbol", (ctx, a) =>
        {
            var m = ctx.Model;
            var view = Views.Find(m, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
            var sources = Ids(a).Select(id => m.Get(id) ?? throw new CommandException("Something chosen is gone.")).ToList();
            if (sources.Any(e => e.Class != IfcClass.Annotation || e.PredefinedType is not (Drafts.Predefined or "TEXT") || e.Str("viewId") != view.Id))
                throw new CommandException("A symbol is made of drafted lines, shapes, hatches and texts of this view.");
            var @base = P(a["base"], "The insertion point");
            var angle = (Json.Number(a["angle"]) ?? 0) * Math.PI / 180;
            var scale = (string?)a["scale"] is "model" ? "model" : "annotation";
            var (items, found) = ItemsOf(m, view, sources, @base, angle, scale == "model");
            if (items.Count == 0) throw new CommandException("Nothing to save: construction lines are not part of a symbol.");
            var given = (a["attributes"] as JsonArray ?? []).Select(SymbolAttribute.From).OfType<SymbolAttribute>().ToList();
            var attributes = given.Concat(found.Where(f => !given.Any(g => g.Key.Equals(f.Key, StringComparison.OrdinalIgnoreCase)))).ToList();
            var def = SymbolDef.Parse(new SymbolDef(scale, (string?)a["category"] is { Length: > 0 } cat ? cat : "other", items, attributes, (string?)a["description"]).ToJson());
            var symbol = Symbols.Create(ctx, (string?)a["name"] ?? "Symbol", def);
            string? instance = null;
            if (B(a["replace"]) ?? true)
            {
                var layer = sources.Select(e => e.Str("layer")).OfType<string>().GroupBy(l => l).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? "0";
                foreach (var e in sources) ctx.Remove(e.Id);
                var attrs = new JsonObject { ["symbolId"] = symbol.Id, ["viewId"] = view.Id, ["at"] = J(@base), ["angle"] = Math.Round(((Json.Number(a["angle"]) ?? 0) % 360 + 360) % 360, 9), ["layer"] = layer };
                var e2 = new Entity(ctx.NewId(), IfcClass.Annotation, symbol.Name, attrs, Predefined);
                ctx.Add(e2);
                instance = e2.Id;
            }
            return new JsonObject { ["symbolId"] = symbol.Id, ["name"] = symbol.Name, ["instanceId"] = instance };
        });

        // Placed symbols exploded into drafting on their views (their texts filled as they read now).
        r.Command("drafting.explodeSymbol", "Explode symbol", (ctx, a) =>
        {
            var made = new JsonArray();
            foreach (var id in Ids(a))
                foreach (var x in Explode(ctx, Require(ctx.Model, id))) made.Add((JsonNode)x);
            return new JsonObject { ["ids"] = made };
        });
    }

    /// <summary>A placed symbol carried by a move, a turn or a mirror: its point, its angle, and a mirror turns it over (its texts stay readable).</summary>
    public static void Carry(JsonObject attrs, Placement2D t)
    {
        var at = attrs["at"] is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
        attrs["at"] = J(t.Point(at));
        var angle = (Json.Number(attrs["angle"]) ?? 0) * Math.PI / 180;
        var turned = t.Angle(angle) * 180 / Math.PI;
        attrs["angle"] = Math.Round((turned % 360 + 360) % 360, 9);
        // A mirror is a half turn about the mirror line's angle and a flip across the symbol's x.
        if (t.Mirrored) attrs["flipY"] = !(attrs["flipY"] is JsonValue fy && fy.TryGetValue<bool>(out var f) && f);
    }
}
