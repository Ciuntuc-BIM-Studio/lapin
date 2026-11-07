using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Visibility / Graphics (the old Lapin's): what a view shows and how, by
/// category (hidden, line colour and weight, fill, cut hatch), by IFC class
/// (hidden) and by element. A view keeps its own, or takes a named set of the
/// project's (<c>graphicsSets</c>): every change to a set reaches all its
/// views. The 3D view keeps its graphics on the project (<c>view3d</c>), and
/// is addressed as the view "3d".
/// </summary>
public static class GraphicsSets
{
    public const string View3d = "3d";

    static JsonArray Sets(Entity? project) => project?.Attrs["graphicsSets"] as JsonArray ?? [];

    static JsonObject? FindSet(IModelView m, string? setId) =>
        setId == null ? null : Sets(Spatial.Project(m)).OfType<JsonObject>().FirstOrDefault(s => (string?)s["id"] == setId);

    /// <summary>What a view's graphics are: its set's, else its own, else its view style's set (decision 391: the style as Revit's view template).</summary>
    public static JsonObject? Of(IModelView m, Entity view) =>
        EffectiveSet(m, view) is { } setId && FindSet(m, setId) is { } set ? set["graphics"] as JsonObject : view.Attrs["graphics"] as JsonObject;

    /// <summary>The set a view's graphics come from: the one it takes, else, with no graphics of its own, its style's; null: its own.</summary>
    public static string? EffectiveSet(IModelView m, Entity view)
    {
        if (view.Str("graphicsSet") is { } setId && FindSet(m, setId) != null) return setId;
        if (view.Attrs["graphics"] is JsonObject) return null;
        return ViewStyles.StyleOf(m, view).GraphicsSet is { } styleSet && FindSet(m, styleSet) != null ? styleSet : null;
    }

    /// <summary>The 3D view's own settings (its set, its graphics).</summary>
    static JsonObject Of3d(IModelView m) => Spatial.Project(m)?.Attrs["view3d"] as JsonObject ?? [];

    /// <summary>{3D} as a view: its settings from the project, its kind "3d".</summary>
    public static Entity As3dView(IModelView m)
    {
        var attrs = Of3d(m).DeepClone() as JsonObject ?? [];
        attrs["kind"] = "3d";
        return new(View3d, Views.Class, "3D", attrs);
    }

    /// <summary>What a view's graphics hide: its categories and IFC classes.</summary>
    public static (HashSet<string> Categories, HashSet<string> Classes) Hidden(JsonObject? g)
    {
        static HashSet<string> Of(JsonNode? n) => (n as JsonObject)?.Where(c => c.Value?["hidden"] is JsonValue h && h.TryGetValue<bool>(out var b) && b).Select(c => c.Key).ToHashSet() ?? [];
        return (Of(g?["categories"]), Of(g?["classes"]));
    }

    public static bool Hides(JsonObject? g, Entity e)
    {
        var (cats, classes) = Hidden(g);
        return classes.Contains(e.Class) || (LineStyles.CategoryOf(e.Class) is { } c && cats.Contains(c));
    }

    /// <summary>The categories a 3D view leaves out until its graphics show them: rooms, whose volumes would hide the building; CAD linework (a DXF's 3D lines), a drawing over the model.</summary>
    public static readonly string[] HiddenIn3d = ["spaces", "linework"];

    /// <summary>What a 3D view hides: what its graphics hide, and the categories hidden in 3D unless its graphics show them.</summary>
    public static bool Hides3d(JsonObject? g, Entity e)
    {
        if (Hides(g, e)) return true;
        if (LineStyles.CategoryOf(e.Class) is not { } c || !HiddenIn3d.Contains(c)) return false;
        return !(g?["categories"]?[c]?["hidden"] is JsonValue h && h.TryGetValue<bool>(out var hidden) && !hidden);
    }

    /// <summary>The view (or "3d") a command names, and how to write its attributes back.</summary>
    internal static (Entity View, Action<JsonObject> Write) Target(ICommandContext ctx, string? id)
    {
        if (id == View3d)
            return (As3dView(ctx.Model), attrs =>
            {
                var project = Spatial.Project(ctx.Model)!;
                var pa = project.CloneAttrs();
                pa["view3d"] = attrs.DeepClone();
                ctx.Replace(project.With(attrs: pa));
            });
        var v = ctx.Model.Get(id ?? "") is { Class: Views.Class } x ? x : throw new CommandException("Not a view.");
        return (v, attrs => ctx.Replace(ctx.Model.Get(v.Id)!.With(attrs: attrs)));
    }

    static void WriteSets(ICommandContext ctx, JsonArray sets)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var pa = project.CloneAttrs();
        pa["graphicsSets"] = sets;
        ctx.Replace(project.With(attrs: pa));
    }

    public static void Register(IModuleRegistry r)
    {
        // One row of a view's graphics: a category, an IFC class or an element ({hidden, lineColor, lineWeight, fillColor, cutHatch}; null clears it).
        // A view that takes a set changes the set, for all its views.
        // A category's subcategory (sub: core, finishes…) is a row of its own under the category's (its sub). A link's row
        // (link alone: {display: host | custom, hidden, halftone}) is in links; with a category, a subcategory or a class too,
        // the row is the link's own, which rules its elements when the link is displayed custom (decision 391).
        // A view that takes a set, or none and its style's, changes the set, for all its views.
        r.Command("view.graphics", "View graphics", (ctx, a) =>
        {
            var (v, write) = Target(ctx, (string?)a["id"]);
            var setId = EffectiveSet(ctx.Model, v);
            var sets = Sets(Spatial.Project(ctx.Model)).DeepClone().AsArray();
            var set = setId == null ? null : sets.OfType<JsonObject>().FirstOrDefault(s => (string?)s["id"] == setId);
            var g = (set?["graphics"] ?? v.Attrs["graphics"]) is JsonObject cur ? (JsonObject)cur.DeepClone() : new JsonObject();
            var value = a["value"] as JsonObject;
            // Where the rows go: the view's graphics, or a link's row in them.
            var linkId = (string?)a["link"];
            if (linkId != null && ctx.Model.Get(linkId) is not { Class: Links.Class }) throw new CommandException("There is no such link.");
            var links = g["links"]?.DeepClone() as JsonObject ?? new JsonObject();
            var into = linkId == null ? g : links[linkId]?.DeepClone() as JsonObject ?? new JsonObject();
            static void Put(JsonObject o, string key, JsonObject? val)
            {
                if (val != null) o[key] = val.DeepClone(); else o.Remove(key);
            }
            static void SetIn(JsonObject o, string bucket, string key, JsonObject? val)
            {
                var b = o[bucket] as JsonObject ?? new JsonObject();
                Put(b, key, val);
                if (b.Count > 0) o[bucket] = b; else o.Remove(bucket);
            }
            var cat = (string?)a["category"];
            var cls = (string?)a["class"];
            if (cat != null)
            {
                if (!LineStyles.Categories.Contains(cat)) throw new CommandException($"„{cat}” is not a category.");
                if ((string?)a["sub"] is { } sub)
                {
                    if (!VisibilityGraphics.Subcategories.Contains(sub)) throw new CommandException($"„{sub}” is not a subcategory.");
                    var cats = into["categories"] as JsonObject ?? new JsonObject();
                    var row = cats[cat] as JsonObject ?? new JsonObject();
                    SetIn(row, "sub", sub, value);
                    Put(cats, cat, row.Count > 0 ? row : null);
                    if (cats.Count > 0) into["categories"] = cats; else into.Remove("categories");
                }
                else
                {
                    // The category's own row keeps its subcategories.
                    var keep = into["categories"]?[cat]?["sub"]?.DeepClone();
                    var row = value?.DeepClone() as JsonObject;
                    if (keep != null) { row ??= new JsonObject(); row["sub"] = keep; }
                    SetIn(into, "categories", cat, row);
                }
            }
            if (cls != null) SetIn(into, "classes", cls, value);
            // Stage B: an annotation category's row; an import's ({hidden, halftone}) or one of its layers'; the view's (or a link's) filters, in order.
            // The view's detail level (coarse, medium, fine; null: by its scale).
            if (a.ContainsKey("detailLevel"))
            {
                if ((string?)a["detailLevel"] is { Length: > 0 } dl)
                    g["detailLevel"] = dl is "coarse" or "medium" or "fine" ? dl : throw new CommandException($"„{dl}” is not coarse, medium or fine.");
                else g.Remove("detailLevel");
            }
            if ((string?)a["annotation"] is { } ann)
            {
                if (!VisibilityGraphics.AnnotationCategories.Contains(ann)) throw new CommandException($"„{ann}” is not an annotation category.");
                SetIn(g, "annotations", ann, value);
            }
            if ((string?)a["import"] is { } imp)
            {
                var imports = g["imports"]?.DeepClone() as JsonObject ?? new JsonObject();
                var row = imports[imp]?.DeepClone() as JsonObject ?? new JsonObject();
                if ((string?)a["layer"] is { } layer) SetIn(row, "layers", layer, value);
                else
                {
                    var layers = row["layers"]?.DeepClone();
                    row = value?.DeepClone() as JsonObject ?? new JsonObject();
                    row.Remove("layers");
                    if (layers != null) row["layers"] = layers;
                }
                Put(imports, imp, row.Count > 0 ? row : null);
                if (imports.Count > 0) g["imports"] = imports; else g.Remove("imports");
            }
            if (a["filters"] is JsonArray fl)
            {
                var known = ViewFilters.All(ctx.Model).Select(f => f.Id).ToHashSet();
                var rows = new JsonArray();
                foreach (var row in fl.OfType<JsonObject>())
                {
                    if ((string?)row["id"] is not { } fid || !known.Contains(fid)) throw new CommandException("There is no such filter.");
                    rows.Add((JsonNode)row.DeepClone());
                }
                if (rows.Count > 0) into["filters"] = rows; else into.Remove("filters");
            }
            if (linkId == null) { if ((string?)a["element"] is { } el) SetIn(g, "elements", el, value); }
            else
            {
                if (cat == null && cls == null && a["filters"] == null)
                {
                    // The link's own row: its display, shown and halftone in this view; its categories and classes stay.
                    var keep = new JsonObject();
                    foreach (var k in new[] { "categories", "classes", "filters" }) if (into[k] is { } kv) keep[k] = kv.DeepClone();
                    if (value != null)
                        foreach (var (k, val) in value)
                        {
                            if (k == "display" && (string?)val is { } d && !VisibilityGraphics.LinkDisplays.Contains(d)) throw new CommandException($"„{d}” is not host or custom.");
                            if (k is "display" or "hidden" or "halftone") keep[k] = val?.DeepClone();
                        }
                    into = keep;
                }
                if (into.Count > 0) links[linkId] = into; else links.Remove(linkId);
                if (links.Count > 0) g["links"] = links; else g.Remove("links");
            }
            if (set != null) { set["graphics"] = g; WriteSets(ctx, sets); return null; }
            var attrs = v.CloneAttrs();
            // A view whose style names a set keeps graphics of its own, even empty, so it does not fall back to the style's.
            if (g.Count > 0 || ViewStyles.StyleOf(ctx.Model, v).GraphicsSet != null) attrs["graphics"] = g; else attrs.Remove("graphics");
            write(attrs);
            return null;
        });

        // The view's graphics saved as a new set, which the view then takes.
        r.Command("view.saveGraphicsSet", "Save graphics set", (ctx, a) =>
        {
            var (v, write) = Target(ctx, (string?)a["id"]);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A graphics set needs a name.");
            var sets = Sets(Spatial.Project(ctx.Model)).DeepClone().AsArray();
            if (sets.OfType<JsonObject>().Any(s => (string?)s["name"] == name)) throw new CommandException($"There is a set „{name}” already.");
            var id = ctx.NewId();
            sets.Add((JsonNode)new JsonObject { ["id"] = id, ["name"] = name, ["graphics"] = Of(ctx.Model, v)?.DeepClone() ?? new JsonObject() });
            WriteSets(ctx, sets);
            var attrs = v.CloneAttrs();
            attrs["graphicsSet"] = id;
            attrs.Remove("graphics");
            write(attrs);
            return new JsonObject { ["id"] = id };
        });

        // A view takes a set (null: its own settings again, starting from the set's).
        r.Command("view.useGraphicsSet", "Use graphics set", (ctx, a) =>
        {
            var (v, write) = Target(ctx, (string?)a["id"]);
            var setId = (string?)a["setId"];
            if (setId != null && Sets(Spatial.Project(ctx.Model)).OfType<JsonObject>().All(s => (string?)s["id"] != setId)) throw new CommandException("There is no such graphics set.");
            var attrs = v.CloneAttrs();
            if (setId != null) { attrs["graphicsSet"] = setId; attrs.Remove("graphics"); }
            else
            {
                // Its own again, starting from what it showed (its set's, or its style's set's); empty when the style names a set.
                if (Of(ctx.Model, v) is { } g) attrs["graphics"] = g.DeepClone();
                else if (ViewStyles.StyleOf(ctx.Model, v).GraphicsSet != null) attrs["graphics"] = new JsonObject();
                attrs.Remove("graphicsSet");
            }
            write(attrs);
            return null;
        });

        // A set removed: its views keep its settings as their own.
        r.Command("view.removeGraphicsSet", "Remove graphics set", (ctx, a) =>
        {
            var setId = (string?)a["setId"];
            var sets = Sets(Spatial.Project(ctx.Model)).DeepClone().AsArray();
            var set = sets.OfType<JsonObject>().FirstOrDefault(s => (string?)s["id"] == setId) ?? throw new CommandException("There is no such graphics set.");
            foreach (var v in ctx.Model.OfClass(Views.Class).Where(v => v.Str("graphicsSet") == setId).ToList())
            {
                var attrs = v.CloneAttrs();
                attrs.Remove("graphicsSet");
                if (set["graphics"] is JsonObject g) attrs["graphics"] = g.DeepClone();
                ctx.Replace(v.With(attrs: attrs));
            }
            if (Of3d(ctx.Model) is { } v3 && (string?)v3["graphicsSet"] == setId)
            {
                var (_, write) = Target(ctx, View3d);
                var attrs = (JsonObject)v3.DeepClone();
                attrs.Remove("graphicsSet");
                if (set["graphics"] is JsonObject g) attrs["graphics"] = g.DeepClone();
                write(attrs);
            }
            // Views that took it through their style keep its settings as their own; the styles forget it.
            var styled = ViewStyles.All(ctx.Model).Where(s => s.GraphicsSet == setId).Select(s => s.Id).ToHashSet();
            if (styled.Count > 0)
            {
                foreach (var v in ctx.Model.OfClass(Views.Class).Where(v => v.Str("graphicsSet") == null && v.Attrs["graphics"] == null && styled.Contains(ViewStyles.StyleOf(ctx.Model, v).Id)).ToList())
                {
                    var attrs = v.CloneAttrs();
                    attrs["graphics"] = set["graphics"]?.DeepClone() ?? new JsonObject();
                    ctx.Replace(v.With(attrs: attrs));
                }
                ViewStyles.ForgetSet(ctx, setId!);
            }
            sets.Remove(set);
            WriteSets(ctx, sets);
            return null;
        });

        // What the dialog shows: the view's graphics, its set, the sets, the categories.
        r.Query("view.visibility", (q, a) =>
        {
            var id = (string?)a["id"];
            var v = id == View3d ? As3dView(q.Model) : Views.Find(q.Model, id ?? "") ?? throw new CommandException("Not a view.");
            var setId = EffectiveSet(q.Model, v);
            return new JsonObject
            {
                ["graphics"] = Of(q.Model, v)?.DeepClone(),
                ["graphicsSet"] = setId,
                // The set comes from the view's style (its template), not chosen on the view.
                ["fromStyle"] = setId != null && v.Str("graphicsSet") != setId,
                ["style"] = ViewStyles.StyleOf(q.Model, v).Name,
                ["subcategories"] = new JsonObject(VisibilityGraphics.SubcategoriesOf.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)new JsonArray(kv.Value.Select(x => (JsonNode)x).ToArray())))),
                ["annotations"] = new JsonArray(VisibilityGraphics.AnnotationCategories.Select(c => (JsonNode)c).ToArray()),
                ["filters"] = new JsonArray(ViewFilters.All(q.Model).Select(f => (JsonNode)new JsonObject { ["id"] = f.Id, ["name"] = f.Name }).ToArray()),
                ["imports"] = Imports(q.Model, v),
                ["links"] = new JsonArray(Links.All(q.Model).Select(l => (JsonNode)new JsonObject
                {
                    ["id"] = l.Id, ["name"] = l.Name, ["kind"] = l.Str("kind"), ["visible"] = Links.Visible(l), ["halftone"] = Links.IsHalftone(l),
                    // The IFC classes its elements have, for its own classes tab.
                    ["classes"] = new JsonArray(Links.ElementsOf(q.Model, l.Id).Where(e => Spatial.StoreyOf(q.Model, e.Id) != null).Select(e => e.Class).Distinct().Order().Select(c => (JsonNode)c).ToArray()),
                }).ToArray()),
                ["sets"] = new JsonArray(Sets(Spatial.Project(q.Model)).OfType<JsonObject>().Select(s => (JsonNode)new JsonObject { ["id"] = s["id"]?.DeepClone(), ["name"] = s["name"]?.DeepClone() }).ToArray()),
                ["categories"] = new JsonArray(LineStyles.Categories.Select(c => (JsonNode)c).ToArray()),
                // Hidden until the view's graphics show them (rooms, in 3D).
                ["hiddenByDefault"] = new JsonArray((v.Str("kind") == "3d" ? HiddenIn3d : []).Select(c => (JsonNode)c).ToArray()),
            };
        });

        // What the 3D view hides: the elements its categories and classes leave out.
        // What the 3D hides: {3D}'s, or a saved 3D view's (id).
        r.Query("view.hidden3d", (q, a) =>
        {
            var id = (string?)a["id"];
            var view = id == null || id == View3d ? As3dView(q.Model) : q.Model.Get(id) is { Class: Views.Class } v && v.Str("kind") == "3d" ? v : throw new CommandException("Not a 3D view.");
            var vg = VisibilityGraphics.Of(q.Model, view);
            return new JsonArray(q.Model.Entities.Where(e => Spatial.StoreyOf(q.Model, e.Id) != null && vg.Hides(e)).Select(e => (JsonNode)e.Id).ToArray());
        });

        // How the 3D view shows its elements by its Visibility / Graphics (decision 391), as looks: {id: {hidden} | {opacity} | {parts: {key: {hidden}}}}.
        r.Query("view.looks3d", (q, a) =>
        {
            var id = (string?)a["id"];
            var view = id == null || id == View3d ? As3dView(q.Model) : q.Model.Get(id) is { Class: Views.Class } v && v.Str("kind") == "3d" ? v : throw new CommandException("Not a 3D view.");
            return Looks3d(q, view);
        });
    }

    /// <summary>
    /// What a view's Imported categories tab lists: the project's DXF imports (a 2D view's own first; the 3D shows them
    /// all), each with the layers its shapes are on, and the PDFs and images traced over the view.
    /// </summary>
    static JsonArray Imports(IModelView m, Entity view)
    {
        var o = new JsonArray();
        var recorded = Spatial.Project(m)?.Attrs[VisibilityGraphics.DxfImportsKey] as JsonObject ?? [];
        var layersOf = new Dictionary<string, SortedSet<string>>();
        foreach (var e in m.OfClass(IfcClass.Annotation))
            if (e.Str(VisibilityGraphics.DxfImportKey) is { } imp)
                (layersOf.TryGetValue(imp, out var set) ? set : layersOf[imp] = []).Add(e.Str("layer") ?? "0");
        foreach (var (id, rec) in recorded)
        {
            var viewId = (string?)rec?["viewId"];
            if (view.Str("kind") != "3d" && viewId != view.Id) continue;
            o.Add((JsonNode)new JsonObject
            {
                ["id"] = id, ["kind"] = "dxf", ["name"] = (string?)rec?["file"] ?? "DXF", ["viewId"] = viewId,
                ["layers"] = new JsonArray((layersOf.GetValueOrDefault(id) ?? []).Select(l => (JsonNode)l).ToArray()),
            });
        }
        foreach (var img in m.OfClass("LapinViewImage").Where(e => e.Str("viewId") == view.Id))
            o.Add((JsonNode)new JsonObject { ["id"] = img.Id, ["kind"] = "image", ["name"] = img.Name, ["layers"] = new JsonArray() });
        return o;
    }

    /// <summary>A 3D view's looks: what it hides, a link halftone in it, the parts its subcategories hide.</summary>
    public static JsonObject Looks3d(IQueryContext q, Entity view)
    {
        var m = q.Model;
        var vg = VisibilityGraphics.Of(m, view);
        var o = new JsonObject();
        foreach (var e in m.Entities)
        {
            if (Spatial.StoreyOf(m, e.Id) == null) continue;
            if (vg.Hides(e)) { o[e.Id] = new JsonObject { ["hidden"] = true }; continue; }
            JsonObject? look = null;
            // A halftone, a transparency (opacity), a filter's colour (decision 394).
            if (vg.OpacityOf(e) is { } op) look = new JsonObject { ["opacity"] = Math.Round(op, 3) };
            if (vg.ColorOf(e) is { } color && color != Links.Halftone) (look ??= new JsonObject())["color"] = color;
            if (vg.HiddenSubs(e) != null && q.Geometry(e.Id) is { } geo)
            {
                var parts = new JsonObject();
                foreach (var p in geo.Parts)
                    if (p.Key is { } k && !parts.ContainsKey(k) && vg.HidesPart(e, k)) parts[k] = new JsonObject { ["hidden"] = true };
                if (parts.Count > 0) (look ??= new JsonObject())["parts"] = parts;
            }
            if (look != null) o[e.Id] = look;
        }
        return o;
    }
}
