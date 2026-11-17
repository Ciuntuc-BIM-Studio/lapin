using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing.Hatch;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting.Cad;

/// <summary>
/// Boards for drawings kept as items in millimetres (audit S11 stage 2, docs/plan-sketch-canvas.md §4): a symbol's and a title
/// block's. The board is a paper view of its own (<see cref="PaperViews.IsBoard"/>), at 1:1, its metres the items' mm / 1000, so a
/// length typed in mm is the items' mm and a text's height is its height in them; every Draft command, snap and grip works on it.
///
/// <para><b>Laid</b> (<see cref="Lay"/>): each item becomes one drafted thing remembering which item it is (<see cref="ItemKey"/>)
/// and what it was when laid (<see cref="SumKey"/>): a line, an arc, a circle, a polyline (a rectangle a closed one); a filled or
/// hatched shape a hatch of its outline; a text a drafted text (its tokens as typed: no sheet fills them here); a title block's
/// logo a picture (its image the project's); a revision table, or a picture without its image, its box (a closed polyline).
/// An item of no kind the board knows is kept as it is, unseen.</para>
///
/// <para><b>Collected</b> (<see cref="Collect"/>): an item whose drafted thing was not touched is written back as it was, byte for
/// byte (a board opened and finished unchanged writes what it read); one that was changed keeps its look (widths, colours,
/// fonts, layer) with its new place and shape; one deleted is gone; what was drawn new is converted as Save as symbol converts
/// drafting (<see cref="SymbolInstances.ItemsOf"/>). Construction lines stay out: helpers on the board only.</para>
/// </summary>
public static class ItemBoards
{
    /// <summary>On a drafted thing of a board: the index of the item it stands for.</summary>
    public const string ItemKey = "boardItem";

    /// <summary>On a drafted thing of a board: what it was when laid (a digest of its attributes), to tell an untouched item.</summary>
    public const string SumKey = "boardSum";

    /// <summary>On the board: the items it could not show, kept as they are.</summary>
    const string KeptKey = "boardKept";

    /// <summary>A metre of the board per millimetre of the items.</summary>
    const double Mm = 0.001;

    /// <summary>A text's height when its item gives none (mm).</summary>
    const double TextHeight = 2.5;

    /// <summary>A board's view: a paper of its own at 1:1, marked with <paramref name="key"/> (what it is the board of) and its guides.</summary>
    public static Entity NewView(ICommandContext ctx, string name, string key, JsonObject of, JsonArray guides)
    {
        var attrs = new JsonObject { ["kind"] = PaperViews.Kind, ["scale"] = 1, [key] = of, [PaperViews.UnderlayKey] = guides };
        var view = new Entity(ctx.NewId(), Views.Class, name, attrs);
        ctx.Add(view);
        return view;
    }

    /// <summary>The board marked with <paramref name="key"/> whose mark <paramref name="match"/> takes, when one is open.</summary>
    public static Entity? Find(IModelView m, string key, Func<JsonObject, bool> match) =>
        m.OfClass(Views.Class).FirstOrDefault(v => v.Attrs[key] is JsonObject o && match(o));

    /// <summary>The board a command names (<c>viewId</c>), with its mark; refused for any other view.</summary>
    public static (Entity View, JsonObject Of) Board(IModelView m, JsonObject a, string key, string what)
    {
        var view = Views.Find(m, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
        return view.Attrs[key] is JsonObject of ? (view, of) : throw new CommandException($"That view is not {what}'s board.");
    }

    /// <summary>The board's mark changed (its fields: a name, a size), one undo step.</summary>
    public static void SetMark(ICommandContext ctx, Entity view, string key, JsonObject of, JsonArray? guides = null)
    {
        var attrs = view.CloneAttrs();
        attrs[key] = of;
        if (guides != null) attrs[PaperViews.UnderlayKey] = guides;
        ctx.Replace(view.With(attrs: attrs));
    }

    /// <summary>What is on the board (drafted shapes, hatches, texts, pictures and any other annotation), all removed with the view.</summary>
    static IEnumerable<Entity> On(IModelView m, string viewId) =>
        m.OfClass(IfcClass.Annotation).Where(e => e.Str("viewId") == viewId).Concat(ViewImages.On(m, viewId));

    public static void Drop(ICommandContext ctx, Entity view)
    {
        foreach (var e in On(ctx.Model, view.Id).ToList()) ctx.Remove(e.Id);
        ctx.Remove(view.Id);
    }

    // ------------------------------------------------------------ guides

    /// <summary>A run of a board's guides (its metres).</summary>
    public static JsonObject Guide(IEnumerable<Vec2> pts, bool closed, string color = "#9aa3ad", double[]? dash = null)
    {
        var o = new JsonObject { ["pts"] = new JsonArray([.. pts.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 9), Math.Round(p.Y, 9)))]), ["closed"] = closed, ["color"] = color };
        if (dash != null) o["dash"] = new JsonArray([.. dash.Select(d => (JsonNode)d)]);
        return o;
    }

    /// <summary>A box W × H mm from the origin, as a guide.</summary>
    public static JsonObject Box(double w, double h, string color = "#9aa3ad") =>
        Guide([new(0, 0), new(w * Mm, 0), new(w * Mm, h * Mm), new(0, h * Mm)], true, color, [2, 1.5]);

    // ------------------------------------------------------------ items → the board

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2((Json.Number(a[0]) ?? 0) * Mm, (Json.Number(a[1]) ?? 0) * Mm) : default;
    static double Num(JsonObject o, string k) => Json.Number(o[k]) ?? 0;
    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));
    static JsonNode N(double m) => JsonValue.Create(Math.Round(m / Mm, 4))!;
    static JsonArray Pt(Vec2 p) => new(N(p.X), N(p.Y));
    static bool Colour(string? c) => c is { Length: > 0 } && c.StartsWith('#');

    /// <summary>The fill or hatch a closed item has, as a drafted hatch takes it (pattern, scale, angle); null when it has none.</summary>
    static (string Pattern, double Scale, double Angle, string? Color)? FillOf(JsonObject item)
    {
        if (item["hatch"] is JsonObject h && (string?)h["pattern"] is { Length: > 0 } pattern)
            return (pattern, Json.Number(h["scale"]) ?? 1, Json.Number(h["angle"]) ?? 0, (string?)h["color"]);
        if ((string?)item["fill"] is { Length: > 0 } fill && fill != "none") return ("SOLID", 1, 0, Colour(fill) ? fill : null);
        return null;
    }

    /// <summary>The outline of a closed item (board metres, counter-clockwise as given); null for an open one.</summary>
    static List<Vec2>? LoopOf(JsonObject item)
    {
        switch ((string?)item["type"])
        {
            case "rect":
            {
                var (x, y, w, h) = (Num(item, "x") * Mm, Num(item, "y") * Mm, Num(item, "w") * Mm, Num(item, "h") * Mm);
                return [new(x, y), new(x + w, y), new(x + w, y + h), new(x, y + h)];
            }
            case "polyline" when item["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c:
                return [.. (item["pts"] as JsonArray ?? []).Select(P)];
            case "circle":
                var loop = Drafts.Flatten(new Circle(P(item["c"]), Num(item, "r") * Mm))[0];
                loop.RemoveAt(loop.Count - 1);
                return loop;
            default: return null;
        }
    }

    /// <summary>The drafted thing an item is laid as on the board (not yet added), or null for an item the board cannot show.</summary>
    static Entity? Laid(IModelView m, string viewId, JsonObject item, Func<string, string> name, Func<string> id)
    {
        var type = (string?)item["type"];
        var color = (string?)item["color"];
        Entity Draft(Shape s, string word, JsonObject? hatch = null)
        {
            var attrs = new JsonObject { ["viewId"] = viewId, ["layer"] = "0", ["shape"] = s.ToJson() };
            if (hatch != null) attrs["hatch"] = hatch;
            if (Colour(hatch != null ? (string?)hatch["color"] ?? color : color)) attrs["color"] = hatch != null ? (string?)hatch["color"] ?? color : color;
            return new Entity(id(), IfcClass.Annotation, name(word), attrs, Drafts.Predefined);
        }
        if (type is "rect" or "polyline" or "circle" && FillOf(item) is { } f && LoopOf(item) is { Count: >= 3 } loop)
        {
            var hatch = new JsonObject
            {
                ["pattern"] = f.Pattern, ["scale"] = f.Scale, ["angle"] = f.Angle,
                ["loops"] = new JsonArray((JsonNode)new JsonArray([.. loop.Select(p => (JsonNode)J(p))])),
            };
            if (f.Color != null) hatch["color"] = f.Color;
            return Draft(new Polyline(loop, true), "Hatch", hatch);
        }
        switch (type)
        {
            case "line": return Draft(new Line(P(item["a"]), P(item["b"])), "Line");
            case "rect": return Draft(new Polyline(LoopOf(item)!, true), "Polygon");
            case "polyline" when item["pts"] is JsonArray { Count: >= 2 } pts:
                var closed = item["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c;
                return Draft(new Polyline([.. pts.Select(P)], closed), closed ? "Polygon" : "Polyline");
            case "circle": return Draft(new Circle(P(item["c"]), Num(item, "r") * Mm), "Circle");
            case "arc": return Draft(new Arc(P(item["c"]), Num(item, "r") * Mm, Geo.NormAngle(Num(item, "start") * Math.PI / 180), Geo.NormAngle(Num(item, "end") * Math.PI / 180)), "Arc");
            case "text":
            {
                var text = item["text"]?.ToString() ?? "";
                var h = Json.Number(item["h"]) ?? TextHeight;
                var angle = Num(item, "angle");
                var attrs = new JsonObject { ["viewId"] = viewId, ["at"] = J(TextStart(item, text, h, angle)), ["text"] = text, ["angle"] = angle, ["textHeight"] = h };
                return new Entity(id(), IfcClass.Annotation, text.Length > 24 ? text[..24] + "…" : text.Length > 0 ? text : "Text", attrs, "TEXT");
            }
            // A drafting table (stage 4 of the drafting tables): a table on the board, at its top-left, its cells as typed.
            case "table" when item["table"] is JsonObject tj:
            {
                var attrs = new JsonObject { ["viewId"] = viewId, ["at"] = J(P(item["at"])), ["angle"] = Num(item, "angle"), ["table"] = tj.DeepClone() };
                if ((string?)item["style"] is { Length: > 0 } ts) attrs["textStyle"] = ts;
                return new Entity(id(), IfcClass.Annotation, name("Table"), attrs, DraftingTables.Predefined);
            }
            case "image" when (string?)item["imageId"] is { } iid && m.Get(iid) is { Class: Images.Class }:
                return new Entity(id(), ViewImages.Class, (string?)item["name"] is { Length: > 0 } n ? n : "Logo", new JsonObject
                {
                    ["viewId"] = viewId, ["image"] = iid, ["x"] = Num(item, "x") * Mm, ["y"] = Num(item, "y") * Mm,
                    ["width"] = Math.Max(Num(item, "w"), 0.01) * Mm, ["height"] = Math.Max(Num(item, "h"), 0.01) * Mm, ["rotation"] = 0, ["opacity"] = 1,
                });
            // A revision table, or a picture with no image: its box.
            case "image" or "revisions":
                return Draft(new Polyline(LoopOf(new JsonObject { ["type"] = "rect", ["x"] = Num(item, "x"), ["y"] = Num(item, "y"), ["w"] = Num(item, "w"), ["h"] = Num(item, "h") })!, true),
                    type == "image" ? "Image" : "Revision table");
            default: return null;
        }
    }

    /// <summary>
    /// Where a text item's drafted text starts (a drafted text hangs from its start, on its baseline): an item's middle or end
    /// anchor moved back along it, by the width its characters take (0.6 of the height each, as a placed symbol's explode reckons).
    /// </summary>
    static Vec2 TextStart(JsonObject item, string text, double h, double angle)
    {
        var (u, n) = Axes(angle);
        var w = text.Length * h * 0.6 * Mm;
        var at = P(item["at"]);
        return at - u * ((string?)item["align"] switch { "end" => w, "middle" => w / 2, _ => 0 }) - n * ((string?)item["valign"] == "middle" ? h * Mm / 2 : 0);
    }

    static (Vec2 U, Vec2 N) Axes(double deg)
    {
        var r = deg * Math.PI / 180;
        return (new Vec2(Math.Cos(r), Math.Sin(r)), new Vec2(-Math.Sin(r), Math.Cos(r)));
    }

    /// <summary>A digest of what a drafted thing is (its class, kind and attributes, but the board's marks).</summary>
    static string Sum(Entity e)
    {
        var attrs = e.CloneAttrs();
        attrs.Remove(ItemKey);
        attrs.Remove(SumKey);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{e.Class}|{e.PredefinedType}|{attrs.ToJsonString()}")))[..24];
    }

    /// <summary>The items laid on the board, one drafted thing each (one undo step with the command that opens it).</summary>
    public static void Lay(ICommandContext ctx, Entity view, JsonArray items)
    {
        var kept = new JsonArray();
        var names = new Dictionary<string, int>();
        string Name(string word) { names[word] = names.GetValueOrDefault(word) + 1; return $"{word} {names[word]}"; }
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is not JsonObject item || Laid(ctx.Model, view.Id, item, Name, ctx.NewId) is not { } e) { kept.Add((JsonNode)i); continue; }
            var attrs = e.CloneAttrs();
            attrs[SumKey] = Sum(e);
            attrs[ItemKey] = i;
            ctx.Add(e.With(attrs: attrs));
        }
        if (kept.Count > 0)
        {
            var now = ctx.Model.Get(view.Id)!;
            var va = now.CloneAttrs();
            va[KeptKey] = kept;
            ctx.Replace(now.With(attrs: va));
        }
    }

    // ------------------------------------------------------------ the board → items

    /// <summary>What a board holds as items: the original ones (untouched as they were, changed with their look kept, deleted gone), and the drafted things drawn new.</summary>
    public sealed record Collected(JsonArray Items, List<Entity> Fresh, List<Entity> Images);

    /// <summary>
    /// The board read back against the items it was laid from. <paramref name="curves"/>: whether the items take circles and arcs
    /// (a symbol's do, a title block's do not: there a changed one is its points by the one rule). Refused, naming it, for what is
    /// none of a line, a shape, a hatch, a text or a picture (a dimension, a tag, a placed symbol).
    /// </summary>
    public static Collected Collect(IModelView m, Entity view, JsonArray original, bool curves, string what, bool tables = false)
    {
        var byItem = new Dictionary<int, Entity>();
        var fresh = new List<Entity>();
        var images = new List<Entity>();
        foreach (var e in On(m, view.Id))
        {
            var drawn = e.Class == ViewImages.Class || e.PredefinedType is Drafts.Predefined or "TEXT" || tables && e.PredefinedType == DraftingTables.Predefined;
            if (!drawn) throw new CommandException($"{what} takes lines, shapes, hatches{(tables ? ", texts and tables" : " and texts")}: „{e.Name}” is none of them. Delete it, or draw it with those, then Finish.");
            if (e.PredefinedType == Drafts.Predefined && e.Str("role") == "draft") continue;
            if (e.Attrs[ItemKey] is JsonValue iv && iv.TryGetValue<int>(out var i) && i >= 0 && i < original.Count && original[i] is JsonObject && !byItem.ContainsKey(i)) byItem[i] = e;
            else if (e.Class == ViewImages.Class) images.Add(e);
            else fresh.Add(e);
        }
        var kept = (view.Attrs[KeptKey] as JsonArray ?? []).Select(x => Json.Number(x) is { } k ? (int)k : -1).ToHashSet();
        var items = new JsonArray();
        for (var i = 0; i < original.Count; i++)
        {
            if (kept.Contains(i)) { items.Add(original[i]?.DeepClone()); continue; }
            if (!byItem.TryGetValue(i, out var e) || original[i] is not JsonObject item) continue;
            items.Add((JsonNode)(e.Str(SumKey) == Sum(e) ? (JsonObject)item.DeepClone() : Patch(m, item, e, curves)));
        }
        return new Collected(items, fresh, images);
    }

    static readonly string[] GeometryKeys = ["a", "b", "x", "y", "w", "h", "pts", "closed", "c", "r", "start", "end"];

    /// <summary>An item changed on the board: its look kept, its place and shape the drafted thing's.</summary>
    static JsonObject Patch(IModelView m, JsonObject item, Entity e, bool curves)
    {
        var o = (JsonObject)item.DeepClone();
        var type = (string?)item["type"];
        if (e.Class == ViewImages.Class)
        {
            o["x"] = N(e.Num("x")); o["y"] = N(e.Num("y")); o["w"] = N(e.Num("width")); o["h"] = N(e.Num("height"));
            return o;
        }
        if (e.PredefinedType == "TEXT")
        {
            var text = e.Str("text") ?? "";
            var h = e.NumOrNull("textHeight") ?? Json.Number(item["h"]) ?? TextHeight;
            var angle = e.Num("angle");
            var (u, n) = Axes(angle);
            var w = text.Length * h * 0.6 * Mm;
            var start = Args.PointOf(e.Attrs["at"]);
            var at = start + u * ((string?)item["align"] switch { "end" => w, "middle" => w / 2, _ => 0 }) + n * ((string?)item["valign"] == "middle" ? h * Mm / 2 : 0);
            o["at"] = Pt(at); o["text"] = text; o["angle"] = Math.Round(angle, 6);
            if (item.ContainsKey("h") || Math.Abs(h - TextHeight) > 1e-9) o["h"] = Math.Round(h, 4);
            return o;
        }
        // A table: where it stands and turns, its cells as edited on the board.
        if (e.PredefinedType == DraftingTables.Predefined)
        {
            o["at"] = Pt(Args.PointOf(e.Attrs["at"]));
            o["angle"] = Math.Round(e.Num("angle"), 6);
            if (e.Attrs["table"] is JsonObject tj) o["table"] = tj.DeepClone();
            return o;
        }
        var shape = Drafts.ShapeOf(e);
        // A box (a revision table, a picture without its image): where it is and how big, its own fields kept.
        if (type is "revisions" or "image")
        {
            if (shape == null) return o;
            var (lo, hi) = Geo.Bounds(shape);
            o["x"] = N(lo.X); o["y"] = N(lo.Y); o["w"] = N(hi.X - lo.X); o["h"] = N(hi.Y - lo.Y);
            return o;
        }
        // The pen's width under the key its kind keeps it in (a line's and an arc's w, the others' stroke).
        var width = item["w"]?.DeepClone() ?? item["stroke"]?.DeepClone();
        void Geometry(string kind)
        {
            foreach (var k in GeometryKeys) o.Remove(k);
            o.Remove("stroke");
            o["type"] = kind;
            if (width != null) o[kind is "line" or "arc" ? "w" : "stroke"] = width;
        }
        void Points(IReadOnlyList<Vec2> pts, bool closed)
        {
            var axis = closed && pts.Count == 4 && Enumerable.Range(0, 4).All(k => { var (p, q) = (pts[k], pts[(k + 1) % 4]); return Math.Abs(p.X - q.X) < 1e-9 || Math.Abs(p.Y - q.Y) < 1e-9; });
            if (type == "rect" && axis)
            {
                var (x0, y0, x1, y1) = (pts.Min(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.X), pts.Max(p => p.Y));
                Geometry("rect");
                o["x"] = N(x0); o["y"] = N(y0); o["w"] = N(x1 - x0); o["h"] = N(y1 - y0);
                return;
            }
            Geometry("polyline");
            o["pts"] = new JsonArray([.. pts.Select(p => (JsonNode)Pt(p))]);
            o["closed"] = closed;
        }
        if (e.Attrs["hatch"] is JsonObject hatch)
        {
            var loop = Drafts.HatchLoops(e).FirstOrDefault() ?? (shape is Polyline hp ? hp.Pts : null);
            if (loop is { Count: >= 3 })
            {
                var was = LoopOf(item);
                var same = was != null && was.Count == loop.Count && was.Zip(loop).All(x => (x.First - x.Second).Length < 1e-9);
                if (!same) Points(loop, true);
            }
            var pattern = (string?)hatch["pattern"] ?? "SOLID";
            if (HatchLibrary.Find(pattern, DraftingResources.Patterns(m)) is null or { Solid: true })
            {
                o.Remove("hatch");
                o["fill"] = (string?)item["fill"] is { Length: > 0 } f && f != "none" ? f : e.Str("color") ?? "#000000";
            }
            else
            {
                var h = new JsonObject { ["pattern"] = pattern, ["scale"] = Json.Number(hatch["scale"]) ?? 1, ["angle"] = Json.Number(hatch["angle"]) ?? 0 };
                if ((string?)hatch["color"] is { } hc) h["color"] = hc;
                o["hatch"] = h;
                o.Remove("fill");
            }
            return o;
        }
        switch (shape)
        {
            case Line l: Geometry("line"); o["a"] = Pt(l.A); o["b"] = Pt(l.B); break;
            case Polyline p: Points(p.Pts, p.Closed); break;
            case Circle c when curves: Geometry("circle"); o["c"] = Pt(c.C); o["r"] = N(c.R); break;
            case Arc a when curves:
                Geometry("arc"); o["c"] = Pt(a.C); o["r"] = N(a.R);
                o["start"] = Math.Round(Geo.NormAngle(a.Start) * 180 / Math.PI, 4); o["end"] = Math.Round(Geo.NormAngle(a.End) * 180 / Math.PI, 4);
                break;
            case Ellipse el:
                var ep = el.Points();
                if (el.Whole && ep.Count > 1) ep.RemoveAt(ep.Count - 1);
                Points(ep, el.Whole);
                break;
            case Circle or Arc:
                var run = Drafts.Flatten(shape)[0];
                var whole = shape is Circle;
                if (whole) run.RemoveAt(run.Count - 1);
                Points(run, whole);
                break;
        }
        if (e.Str("color") is { } col && col != (string?)item["color"]) o["color"] = col;
        return o;
    }

    /// <summary>
    /// Items in mm of what was drawn new on the board (as Save as symbol converts drafting, seen from the origin), and the attributes
    /// their texts name. <paramref name="curves"/> false: a circle or an arc as its points by the one rule (a title block's).
    /// </summary>
    public static (JsonArray Items, List<SymbolAttribute> Attributes) Fresh(IModelView m, Entity view, IEnumerable<Entity> fresh, bool curves)
    {
        var (items, attrs) = SymbolInstances.ItemsOf(m, view, fresh, new Vec2(0, 0), 0, model: false);
        if (curves) return (items, attrs);
        var o = new JsonArray();
        foreach (var it in items.OfType<JsonObject>())
        {
            var type = (string?)it["type"];
            // A title block's fill is a colour: a symbol's "the instance's colour" is black there.
            if ((string?)it["fill"] == "byInstance") it["fill"] = "#000000";
            if (type is not ("circle" or "arc")) { o.Add((JsonNode)it.DeepClone()); continue; }
            var (c, r) = (P(it["c"]), Num(it, "r") * Mm);
            Shape s = type == "circle" ? new Circle(c, r) : new Arc(c, r, Num(it, "start") * Math.PI / 180, Num(it, "end") * Math.PI / 180);
            var run = Drafts.Flatten(s)[0];
            if (type == "circle") run.RemoveAt(run.Count - 1);
            var p = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray([.. run.Select(q => (JsonNode)Pt(q))]), ["closed"] = type == "circle" };
            foreach (var k in new[] { "color", "dash", "fill", "hatch" }) if (it[k] is { } v) p[k] = v.DeepClone();
            p["stroke"] = (it["w"] ?? it["stroke"])?.DeepClone();
            o.Add((JsonNode)p);
        }
        return (o, attrs);
    }

    /// <summary>Pictures put on the board (a title block's logo, Import image…) as image items: their image the project's.</summary>
    public static IEnumerable<JsonObject> FreshImages(IEnumerable<Entity> images) => images.Select(e => new JsonObject
    {
        ["type"] = "image", ["x"] = N(e.Num("x")), ["y"] = N(e.Num("y")), ["w"] = N(e.Num("width")), ["h"] = N(e.Num("height")),
        ["imageId"] = e.Str("image"), ["name"] = e.Name,
    });
}
