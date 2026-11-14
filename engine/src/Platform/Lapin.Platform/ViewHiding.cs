using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Hide in view (forum topic 73, Revit's): elements a plan, section or elevation leaves out, kept on the view
/// (<c>hiddenElements</c>, which every drawing, sheet and export of it reads), one step each; a level's default plan is saved
/// as its plan view to keep them (<see cref="Views.OwnView"/>). Over them, for the interface only, a view's temporary hide
/// and isolate (not saved, not undone, as the 3D view's HH, HI, HR) and Reveal hidden, given with the drawing asked for
/// (<see cref="Temporary"/>): what the view hides then drawn in <see cref="RevealColor"/>, so it can be picked and shown again.
/// </summary>
public static class ViewHiding
{
    public const string Key = "hiddenElements";
    /// <summary>The ids drawn revealed (a view asked for with Reveal hidden): never kept on a view.</summary>
    public const string RevealKey = "revealedElements";
    /// <summary>What Reveal hidden draws the hidden elements in (Revit's magenta, lighter).</summary>
    public const string RevealColor = "#d33fb0";

    public static HashSet<string> Hidden(Entity view) => (view.Attrs[Key] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];

    static List<string> Ids(JsonNode? n) => (n as JsonArray)?.Select(x => (string?)x).OfType<string>().Where(s => s.Length > 0).Distinct().ToList() ?? [];

    /// <summary>
    /// The view as a drawing asked for with its temporary state shows it: <c>temp: {hide: [ids], isolate: [ids]}</c> (isolate:
    /// every element on a storey but those left out) and <c>reveal: true</c> (what it hides kept, and drawn revealed). The view
    /// itself when nothing is asked; the copy is never kept.
    /// </summary>
    public static Entity Temporary(IModelView m, Entity view, JsonObject args)
    {
        var temp = args["temp"] as JsonObject;
        var hide = Ids(temp?["hide"]);
        var isolate = temp?["isolate"] is JsonArray ? Ids(temp["isolate"]).ToHashSet() : null;
        var reveal = args["reveal"] is JsonValue rv && rv.TryGetValue<bool>(out var r) && r;
        if (hide.Count == 0 && isolate == null && !reveal) return view;
        var own = Hidden(view);
        var hidden = reveal ? new HashSet<string>() : new HashSet<string>(own);
        hidden.UnionWith(hide);
        if (isolate != null)
            foreach (var e in m.Entities)
                if (!isolate.Contains(e.Id) && Spatial.StoreyOf(m, e.Id) != null) hidden.Add(e.Id);
        var attrs = view.CloneAttrs();
        attrs[Key] = new JsonArray(hidden.Order(StringComparer.Ordinal).Select(x => (JsonNode)x).ToArray());
        if (reveal) attrs[RevealKey] = new JsonArray(own.Where(id => !hidden.Contains(id)).Order(StringComparer.Ordinal).Select(x => (JsonNode)x).ToArray());
        return view.With(attrs: attrs);
    }

    /// <summary>The elements a drawing draws revealed (hidden in the view, shown because Reveal hidden is on).</summary>
    public static HashSet<string> Revealed(Entity view) => (view.Attrs[RevealKey] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];

    public static void Register(IModuleRegistry r)
    {
        // Hide in view: {viewId, ids}. A 3D view's are its own (its Visibility / Graphics); a level's default plan becomes its plan view.
        r.Command("view.hideElements", "Hide in view", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
            var ids = Ids(a["ids"]);
            if (ids.Count == 0) throw new CommandException("Select what to hide in the view first.");
            foreach (var id in ids) if (ctx.Model.Get(id) is null) throw new CommandException("There is no such element.");
            if (view.Id == GraphicsSets.View3d) throw new CommandException("The project's 3D view hides for now (HH): save it as a 3D view of its own to keep what it hides.");
            var id0 = Views.OwnView(ctx, view, attrs =>
            {
                var set = (attrs[Key] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [];
                set.AddRange(ids.Where(i => !set.Contains(i)));
                attrs[Key] = new JsonArray(set.Select(x => (JsonNode)x).ToArray());
            });
            return new JsonObject { ["id"] = id0 };
        });

        // Unhide in view: {viewId, ids} or {viewId, all: true}.
        r.Command("view.unhideElements", "Unhide in view", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
            var all = a["all"] is JsonValue av && av.TryGetValue<bool>(out var b) && b;
            var ids = Ids(a["ids"]).ToHashSet();
            if (!all && ids.Count == 0) throw new CommandException("Say what to show again (ids), or all.");
            // Nothing hidden here: nothing to keep, no view made for it.
            var own = Hidden(view);
            if (all ? own.Count == 0 : !own.Overlaps(ids)) return new JsonObject { ["id"] = view.Id };
            var id0 = Views.OwnView(ctx, view, attrs =>
            {
                var left = all ? [] : (attrs[Key] as JsonArray)?.Select(x => (string?)x).OfType<string>().Where(x => !ids.Contains(x)).ToList() ?? [];
                if (left.Count == 0) attrs.Remove(Key);
                else attrs[Key] = new JsonArray(left.Select(x => (JsonNode)x).ToArray());
            });
            return new JsonObject { ["id"] = id0 };
        });
    }
}
