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

    /// <summary>What a view's graphics are: its set's, else its own.</summary>
    public static JsonObject? Of(IModelView m, Entity view)
    {
        if (view.Str("graphicsSet") is { } setId && Sets(Spatial.Project(m)).OfType<JsonObject>().FirstOrDefault(s => (string?)s["id"] == setId) is { } set)
            return set["graphics"] as JsonObject;
        return view.Attrs["graphics"] as JsonObject;
    }

    /// <summary>The 3D view's own settings (its set, its graphics).</summary>
    static JsonObject Of3d(IModelView m) => Spatial.Project(m)?.Attrs["view3d"] as JsonObject ?? [];

    static Entity As3dView(IModelView m) => new(View3d, Views.Class, "3D", Of3d(m).DeepClone() as JsonObject ?? []);

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

    /// <summary>The view (or "3d") a command names, and how to write its attributes back.</summary>
    static (Entity View, Action<JsonObject> Write) Target(ICommandContext ctx, string? id)
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
        r.Command("view.graphics", "View graphics", (ctx, a) =>
        {
            var (v, write) = Target(ctx, (string?)a["id"]);
            var setId = v.Str("graphicsSet");
            var sets = Sets(Spatial.Project(ctx.Model)).DeepClone().AsArray();
            var set = setId == null ? null : sets.OfType<JsonObject>().FirstOrDefault(s => (string?)s["id"] == setId);
            var g = (set?["graphics"] ?? v.Attrs["graphics"]) is JsonObject cur ? (JsonObject)cur.DeepClone() : new JsonObject();
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
            if ((string?)a["class"] is { } cls) Set("classes", cls);
            Set("elements", (string?)a["element"]);
            if (set != null) { set["graphics"] = g; WriteSets(ctx, sets); return null; }
            var attrs = v.CloneAttrs();
            if (g.Count > 0) attrs["graphics"] = g; else attrs.Remove("graphics");
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
            sets.Add(new JsonObject { ["id"] = id, ["name"] = name, ["graphics"] = Of(ctx.Model, v)?.DeepClone() ?? new JsonObject() });
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
                if (Of(ctx.Model, v) is { } g) attrs["graphics"] = g.DeepClone();
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
            sets.Remove(set);
            WriteSets(ctx, sets);
            return null;
        });

        // What the dialog shows: the view's graphics, its set, the sets, the categories.
        r.Query("view.visibility", (q, a) =>
        {
            var id = (string?)a["id"];
            var v = id == View3d ? As3dView(q.Model) : Views.Find(q.Model, id ?? "") ?? throw new CommandException("Not a view.");
            return new JsonObject
            {
                ["graphics"] = Of(q.Model, v)?.DeepClone(),
                ["graphicsSet"] = v.Str("graphicsSet"),
                ["sets"] = new JsonArray(Sets(Spatial.Project(q.Model)).OfType<JsonObject>().Select(s => (JsonNode)new JsonObject { ["id"] = s["id"]?.DeepClone(), ["name"] = s["name"]?.DeepClone() }).ToArray()),
                ["categories"] = new JsonArray(LineStyles.Categories.Select(c => (JsonNode)c).ToArray()),
            };
        });

        // What the 3D view hides: the elements its categories and classes leave out.
        r.Query("view.hidden3d", (q, _) =>
        {
            var g = Of(q.Model, As3dView(q.Model));
            return new JsonArray(q.Model.Entities.Where(e => Spatial.StoreyOf(q.Model, e.Id) != null && Hides(g, e)).Select(e => (JsonNode)e.Id).ToArray());
        });
    }
}
