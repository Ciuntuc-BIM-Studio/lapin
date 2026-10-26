using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// View styles (docs/view-styles.md): how a kind of drawing shows the model,
/// as ArchiCAD's partial structure display and Revit's view templates. A
/// style is the project's (<c>viewStyles</c>), named, one per discipline by
/// default (Architectural, Structural, MEP), editable, and more can be made.
/// It says:
/// <list type="bullet">
/// <item><c>layers</c>: which layers of layered elements draw: all of them,
/// all but the finishes (<see cref="Materials.IsFinish"/>), or the core only;</item>
/// <item><c>joins</c>: whether one material goes on from one element into the
/// next without a line (the visual joins), on by default;</item>
/// <item><c>overhead</c>: whether a plan draws what lies above its cut, up to
/// its range's top.</item>
/// </list>
/// A view takes its style (<c>viewStyle</c>, else its discipline's), and may
/// set any of these itself: what it sets wins over the style.
/// </summary>
public static class ViewStyles
{
    public const string Key = "viewStyles";
    public static readonly string[] LayerModes = ["all", "noFinishes", "core"];
    public static readonly string[] Fields = ["layers", "joins", "overhead"];

    /// <summary>A style, or a view's settings as its style and its own choices make them.</summary>
    public sealed record Style(string Id, string Name, string? Discipline, string Layers, bool Joins, bool Overhead)
    {
        public JsonObject ToJson() => new()
        {
            ["id"] = Id, ["name"] = Name, ["discipline"] = Discipline, ["layers"] = Layers, ["joins"] = Joins, ["overhead"] = Overhead,
        };

        public static Style From(JsonObject o) => new(
            (string?)o["id"] ?? "", (string?)o["name"] ?? "Style", (string?)o["discipline"],
            (string?)o["layers"] is { } l && LayerModes.Contains(l) ? l : "all",
            o["joins"] is not JsonValue jv || !jv.TryGetValue<bool>(out var j) || j,
            o["overhead"] is not JsonValue ov || !ov.TryGetValue<bool>(out var h) || h);
    }

    /// <summary>The styles a project without its own reads as: one per discipline; the structural one draws the cores alone.</summary>
    public static readonly IReadOnlyList<Style> Defaults =
    [
        new("architectural", "Architectural", "Architecture", "all", true, true),
        new("structural", "Structural", "Structure", "core", true, true),
        new("mep", "MEP", "MEP", "all", true, true),
    ];

    public static IReadOnlyList<Style> All(IModelView m) =>
        Spatial.Project(m)?.Attrs[Key] is JsonArray arr && arr.Count > 0
            ? arr.OfType<JsonObject>().Select(Style.From).Where(s => s.Id.Length > 0).ToList()
            : Defaults;

    /// <summary>The style a view takes: the one it names, else its discipline's, else the first.</summary>
    public static Style StyleOf(IModelView m, Entity view)
    {
        var all = All(m);
        if (view.Str("viewStyle") is { } id && all.FirstOrDefault(s => s.Id == id) is { } named) return named;
        var discipline = view.Str("discipline") ?? "Architecture";
        return all.FirstOrDefault(s => string.Equals(s.Discipline, discipline, StringComparison.OrdinalIgnoreCase)) ?? all[0];
    }

    /// <summary>How a view draws: its style's settings, with what the view sets itself over them.</summary>
    public static Style Of(IModelView m, Entity view)
    {
        var s = StyleOf(m, view);
        var o = view.Attrs["styleOverrides"] as JsonObject;
        if (o == null) return s;
        return s with
        {
            Layers = (string?)o["layers"] is { } l && LayerModes.Contains(l) ? l : s.Layers,
            Joins = o["joins"] is JsonValue jv && jv.TryGetValue<bool>(out var j) ? j : s.Joins,
            Overhead = o["overhead"] is JsonValue ov && ov.TryGetValue<bool>(out var h) ? h : s.Overhead,
        };
    }

    /// <summary>Whether a view draws a part of a layered element (a part keyed layer:&lt;n&gt;; any other part always draws).</summary>
    public static bool Draws(IModelView m, Style s, string element, string? partKey, Dictionary<string, IReadOnlyList<LayerBand>?> cache)
    {
        if (s.Layers == "all" || partKey is not { } k || !k.StartsWith("layer:", StringComparison.Ordinal)
            || !int.TryParse(k.AsSpan(6), System.Globalization.CultureInfo.InvariantCulture, out var index)) return true;
        if (!cache.TryGetValue(element, out var bands)) cache[element] = bands = LayerSets.BandsOf(m, element);
        if (bands == null || index < 0 || index >= bands.Count) return true;
        var band = bands[index];
        // An element without a marked core keeps all its layers: there is nothing to tell its structure by.
        if (s.Layers == "core") return band.Core || !bands.Any(b => b.Core);
        return band.Core || !(m.Get(band.Material) is { } mat && Materials.IsFinish(mat));
    }

    static JsonArray Write(IEnumerable<Style> styles) => new(styles.Select(s => (JsonNode)s.ToJson()).ToArray());

    static void Save(ICommandContext ctx, IEnumerable<Style> styles)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var pa = project.CloneAttrs();
        pa[Key] = Write(styles);
        ctx.Replace(project.With(attrs: pa));
    }

    static Style Patch(Style s, JsonObject a)
    {
        if (((string?)a["name"])?.Trim() is { Length: > 0 } name) s = s with { Name = name };
        if (a.ContainsKey("discipline")) s = s with { Discipline = ((string?)a["discipline"])?.Trim() is { Length: > 0 } d ? d : null };
        if ((string?)a["layers"] is { } l) s = s with { Layers = LayerModes.Contains(l) ? l : throw new CommandException($"„{l}” is not all, noFinishes or core.") };
        if (a["joins"] is JsonValue jv && jv.TryGetValue<bool>(out var j)) s = s with { Joins = j };
        if (a["overhead"] is JsonValue ov && ov.TryGetValue<bool>(out var h)) s = s with { Overhead = h };
        return s;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Query("viewStyle.list", (q, _) => Write(All(q.Model)));

        // A view's settings as it draws, and where each comes from (its style, or itself).
        r.Query("view.style", (q, a) =>
        {
            if (Views.Find(q.Model, (string?)a["id"] ?? "") is not { } v) return null;
            var style = StyleOf(q.Model, v);
            var own = v.Attrs["styleOverrides"] as JsonObject ?? [];
            return new JsonObject
            {
                ["style"] = style.ToJson(), ["named"] = v.Str("viewStyle") != null,
                ["resolved"] = Of(q.Model, v).ToJson(),
                ["overrides"] = new JsonArray(Fields.Where(own.ContainsKey).Select(f => (JsonNode)f).ToArray()),
            };
        });

        // A new style: a copy of one (from), else of the first; its name.
        r.Command("viewStyle.create", "New view style", (ctx, a) =>
        {
            var all = All(ctx.Model).ToList();
            var source = (string?)a["from"] is { } fid ? all.FirstOrDefault(s => s.Id == fid) ?? throw new CommandException("There is no such view style.") : all[0];
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : $"{source.Name} copy";
            for (var k = 2; all.Any(x => x.Name == name); k++) name = $"{source.Name} copy {k}";
            var made = Patch(source with { Id = ctx.NewId(), Name = name }, a);
            all.Add(made);
            Save(ctx, all);
            return new JsonObject { ["id"] = made.Id };
        });

        r.Command("viewStyle.update", "Change view style", (ctx, a) =>
        {
            var all = All(ctx.Model).ToList();
            var i = all.FindIndex(s => s.Id == (string?)a["id"]);
            if (i < 0) throw new CommandException("There is no such view style.");
            all[i] = Patch(all[i], a);
            Save(ctx, all);
            return null;
        });

        // A style gone: its views take their discipline's again.
        r.Command("viewStyle.delete", "Delete view style", (ctx, a) =>
        {
            var all = All(ctx.Model).ToList();
            var id = (string?)a["id"];
            if (all.All(s => s.Id != id)) throw new CommandException("There is no such view style.");
            if (all.Count == 1) throw new CommandException("A project keeps at least one view style.");
            Save(ctx, all.Where(s => s.Id != id));
            foreach (var v in ctx.Model.OfClass(Views.Class).Where(v => v.Str("viewStyle") == id).ToList())
            {
                var attrs = v.CloneAttrs();
                attrs.Remove("viewStyle");
                ctx.Replace(v.With(attrs: attrs));
            }
            return null;
        });

        // A view's style (style: an id; null: its discipline's) and its own settings over it (a field: its value; null: the style's again).
        r.Command("view.setStyle", "View style", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Views.Class } x ? x : throw new CommandException("Not a view.");
            var attrs = v.CloneAttrs();
            if (a.ContainsKey("style"))
            {
                if ((string?)a["style"] is { Length: > 0 } sid)
                    attrs["viewStyle"] = All(ctx.Model).Any(s => s.Id == sid) ? sid : throw new CommandException("There is no such view style.");
                else attrs.Remove("viewStyle");
            }
            var own = attrs["styleOverrides"]?.DeepClone() as JsonObject ?? [];
            foreach (var f in Fields)
            {
                if (!a.ContainsKey(f)) continue;
                var value = a[f];
                if (value == null) { own.Remove(f); continue; }
                if (f == "layers") own[f] = (string?)value is { } l && LayerModes.Contains(l) ? l : throw new CommandException($"„{value}” is not all, noFinishes or core.");
                else own[f] = value is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : throw new CommandException($"{f} is true or false.");
            }
            if (own.Count > 0) attrs["styleOverrides"] = own; else attrs.Remove("styleOverrides");
            ctx.Replace(v.With(attrs: attrs));
            return null;
        });
    }
}
