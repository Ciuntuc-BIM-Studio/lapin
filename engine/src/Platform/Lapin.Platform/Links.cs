using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Linked files (an IFC, a DXF), as Revit links them: what came from the file
/// is in the model and is drawn, but it belongs to the link (<c>linkId</c> on
/// each of its entities) and cannot be edited. A link is shown or hidden,
/// drawn halftone, unlinked (its entities go), converted to native (its
/// entities stay and become editable), reloaded from its file, or opened on
/// its own in another instance of the app, from Manage → Links.
/// </summary>
public static class Links
{
    public const string Class = "LapinLink";
    public const string Key = "linkId";
    /// <summary>What a halftone link's lines and fills draw in.</summary>
    public const string Halftone = "#9ca3af";

    public static IEnumerable<Entity> All(IModelView m) => m.OfClass(Class);
    public static IEnumerable<Entity> ElementsOf(IModelView m, string linkId) => m.Entities.Where(e => e.Str(Key) == linkId);
    /// <summary>The link an entity belongs to, if any.</summary>
    public static Entity? Of(IModelView m, Entity e) => e.Str(Key) is { } id ? m.Get(id) is { Class: Class } l ? l : null : null;
    public static bool Visible(Entity link) => link.Attrs["visible"] is not JsonValue v || !v.TryGetValue<bool>(out var b) || b;
    public static bool IsHalftone(Entity link) => link.Attrs["halftone"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public static Entity Require(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: Class } l ? l : throw new CommandException("There is no such link.");

    /// <summary>A new link: its kind (ifc, dxf), its file's name and path (null in a browser: the file cannot be read again), the options it was imported with, the view a DXF went into.</summary>
    public static Entity Create(IEdit edit, string kind, string file, string? path, JsonObject? options = null, string? viewId = null)
    {
        var attrs = new JsonObject { ["kind"] = kind, ["file"] = file, ["visible"] = true, ["halftone"] = false, ["linkedAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") };
        if (path != null) attrs["path"] = path;
        if (options != null) attrs["options"] = options.DeepClone();
        if (viewId != null) attrs["viewId"] = viewId;
        var link = new Entity(edit.NewId(), Class, Path.GetFileNameWithoutExtension(file), attrs);
        edit.Add(link);
        return link;
    }

    /// <summary>The entities given marked as the link's.</summary>
    public static int Tag(IEdit edit, string linkId, IEnumerable<string> ids)
    {
        var n = 0;
        foreach (var id in ids)
        {
            if (edit.Model.Get(id) is not { } e || e.Class == Class) continue;
            var attrs = e.CloneAttrs();
            attrs[Key] = linkId;
            edit.Replace(e.With(attrs: attrs));
            n++;
        }
        return n;
    }

    /// <summary>The link's entities taken out of the model (before a reload, on unlinking): the elements first, the storeys it made last, and only when nothing else is on them.</summary>
    public static int RemoveElements(IEdit edit, string linkId)
    {
        var own = ElementsOf(edit.Model, linkId).ToList();
        var n = 0;
        foreach (var e in own.Where(e => e.Class != IfcClass.BuildingStorey && e.Class != IfcClass.Building && e.Class != IfcClass.Site)) { edit.Remove(e.Id); n++; }
        foreach (var s in own.Where(e => e.Class == IfcClass.BuildingStorey))
            if (!edit.Model.Entities.Any(e => e.Id != s.Id && Spatial.StoreyOf(edit.Model, e.Id)?.Id == s.Id)) { edit.Remove(s.Id); n++; }
        return n;
    }

    public static JsonObject ToJson(IModelView m, Entity link) => new()
    {
        ["id"] = link.Id, ["name"] = link.Name, ["kind"] = link.Str("kind"), ["file"] = link.Str("file"), ["path"] = link.Str("path"),
        ["visible"] = Visible(link), ["halftone"] = IsHalftone(link), ["viewId"] = link.Str("viewId"), ["linkedAt"] = link.Str("linkedAt"),
        // Its elements: what stands on a storey (types, materials and layer sets it brought are not counted).
        ["elements"] = ElementsOf(m, link.Id).Count(e => Spatial.StoreyOf(m, e.Id) != null), ["storeys"] = ElementsOf(m, link.Id).Count(e => e.Class == IfcClass.BuildingStorey),
    };

    /// <summary>A hidden link's elements stay out of every view; a halftone link's draw grey.</summary>
    sealed class Filter : IViewElementFilter
    {
        public (bool Hide, string? Color)? For(IModelView model, Entity view, Entity element)
        {
            if (element.Str(Key) is not { } id) return null;
            var link = model.Get(id);
            if (link is not { Class: Class } || !Visible(link)) return (true, null);
            return IsHalftone(link) ? (false, Halftone) : null;
        }
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Link", [new("visible", "Shown", "bool"), new("halftone", "Halftone", "bool")], "#888888", Kind: "resource"));
        r.Service<IViewElementFilter>(new Filter());

        r.Query("link.list", (q, _) => new JsonArray(All(q.Model).Select(l => (JsonNode)ToJson(q.Model, l)).ToArray()));

        // Shown or hidden, halftone, renamed.
        r.Command("link.update", "Change link", (ctx, a) =>
        {
            var link = Require(ctx.Model, (string?)a["id"]);
            var attrs = link.CloneAttrs();
            if (a["visible"] is JsonValue vv && vv.TryGetValue<bool>(out var visible)) attrs["visible"] = visible;
            if (a["halftone"] is JsonValue hv && hv.TryGetValue<bool>(out var halftone)) attrs["halftone"] = halftone;
            ctx.Replace(link.With(name: ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : null, attrs: attrs));
            return null;
        });

        // The link and everything it brought go.
        r.Command("link.unlink", "Unlink", (ctx, a) =>
        {
            var link = Require(ctx.Model, (string?)a["id"]);
            var n = RemoveElements(ctx, link.Id);
            ctx.Remove(link.Id);
            return new JsonObject { ["removed"] = n };
        });

        // What the link brought stays, as the project's own, editable; the link goes.
        r.Command("link.convert", "Convert link to native", (ctx, a) =>
        {
            var link = Require(ctx.Model, (string?)a["id"]);
            var n = 0;
            foreach (var e in ElementsOf(ctx.Model, link.Id).ToList())
            {
                var attrs = e.CloneAttrs();
                attrs.Remove(Key);
                ctx.Replace(e.With(attrs: attrs));
                n++;
            }
            ctx.Remove(link.Id);
            return new JsonObject { ["converted"] = n };
        });
    }
}
