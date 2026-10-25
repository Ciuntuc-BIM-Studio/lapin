using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The project browser's sets (asked 25.10.2026, pending decision 125): a
/// level of the tree above the kinds of drawings, each set taking the views
/// and sheets its filter matches. A filter names what must hold, every
/// field given at once: <c>disciplines</c>, <c>kinds</c> (plan, section,
/// elevation, 3d, sheet, schedule), <c>storeys</c> (ids), <c>scales</c>,
/// <c>phases</c>, <c>nameContains</c>, <c>params</c> (the user's own
/// parameters, name to value); none takes everything. Kept on the project
/// (<c>browserSets</c>), so in the package; a project without any reads as
/// the three defaults, one per discipline.
/// </summary>
public static class BrowserSets
{
    static readonly string[] ListKeys = ["disciplines", "kinds", "storeys", "scales", "phases"];

    public static JsonArray Of(IModelView m) => Spatial.Project(m)?.Attrs["browserSets"] as JsonArray ?? [];

    /// <summary>The sets a project without its own reads as: one per discipline.</summary>
    public static JsonArray Defaults() => new(Views.Disciplines.Select(d => (JsonNode)new JsonObject
    {
        ["id"] = d.ToLowerInvariant(), ["name"] = d, ["filter"] = new JsonObject { ["disciplines"] = new JsonArray((JsonNode)d) },
    }).ToArray());

    /// <summary>The project's sets, or the defaults; a copy to change.</summary>
    static JsonArray Current(IModelView m) => Of(m) is { Count: > 0 } own ? (JsonArray)own.DeepClone() : Defaults();

    static JsonObject Filter(JsonNode? n)
    {
        var o = new JsonObject();
        if (n is not JsonObject f) return o;
        foreach (var key in ListKeys)
            if (f[key] is JsonArray list)
            {
                var values = list.Select(x => x is JsonValue v && v.TryGetValue<string>(out var str) ? str : Json.Number(x)?.ToString(System.Globalization.CultureInfo.InvariantCulture)).OfType<string>().Where(x => x.Length > 0).ToList();
                if (values.Count > 0) o[key] = new JsonArray(values.Select(x => (JsonNode)x).ToArray());
            }
        if ((string?)f["nameContains"] is { Length: > 0 } text) o["nameContains"] = text;
        if (f["params"] is JsonObject ps)
        {
            var kept = new JsonObject();
            foreach (var (k, v) in ps) if (k.Length > 0 && v != null) kept[k] = (string?)v is { } sv ? sv : v.DeepClone();
            if (kept.Count > 0) o["params"] = kept;
        }
        return o;
    }

    static void Write(ICommandContext ctx, JsonArray sets)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var pa = project.CloneAttrs();
        pa["browserSets"] = sets;
        ctx.Replace(project.With(attrs: pa));
    }

    public static void Register(IModuleRegistry r)
    {
        // A set added (no id) or changed: its name and filter.
        r.Command("browser.setSet", "Browser set", (ctx, a) =>
        {
            var sets = Current(ctx.Model);
            var id = (string?)a["id"];
            var set = id == null ? null : sets.OfType<JsonObject>().FirstOrDefault(s => (string?)s["id"] == id);
            if (id != null && set == null) throw new CommandException("There is no such set.");
            var name = ((string?)a["name"])?.Trim();
            if (set == null && string.IsNullOrEmpty(name)) throw new CommandException("A set needs a name.");
            if (set == null) { set = new JsonObject { ["id"] = ctx.NewId(), ["name"] = name, ["filter"] = Filter(a["filter"]) }; sets.Add((JsonNode)set); }
            else
            {
                if (!string.IsNullOrEmpty(name)) set["name"] = name;
                if (a.ContainsKey("filter")) set["filter"] = Filter(a["filter"]);
            }
            Write(ctx, sets);
            return new JsonObject { ["id"] = set["id"]?.DeepClone() };
        });

        r.Command("browser.removeSet", "Remove browser set", (ctx, a) =>
        {
            var sets = Current(ctx.Model);
            var set = sets.OfType<JsonObject>().FirstOrDefault(s => (string?)s["id"] == (string?)a["id"]) ?? throw new CommandException("There is no such set.");
            sets.Remove(set);
            Write(ctx, sets);
            return null;
        });

        // A set moved before another (or, without "before", to the end).
        r.Command("browser.moveSet", "Move browser set", (ctx, a) =>
        {
            var sets = Current(ctx.Model);
            var set = sets.OfType<JsonObject>().FirstOrDefault(s => (string?)s["id"] == (string?)a["id"]) ?? throw new CommandException("There is no such set.");
            sets.Remove(set);
            var before = (string?)a["before"];
            var at = before == null ? sets.Count : sets.ToList().FindIndex(s => (string?)s?["id"] == before);
            if (at < 0) throw new CommandException("There is no such set to go before.");
            sets.Insert(at, set);
            Write(ctx, sets);
            return null;
        });

        // The sets as the browser shows them, and whether they are the project's own or the defaults.
        r.Query("browser.sets", (q, _) => new JsonObject
        {
            ["sets"] = Current(q.Model), ["own"] = Of(q.Model).Count > 0,
            ["disciplines"] = new JsonArray(Views.Disciplines.Select(d => (JsonNode)d).ToArray()),
        });
    }
}
