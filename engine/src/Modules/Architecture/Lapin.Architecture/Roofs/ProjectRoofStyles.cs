using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// The project's own roof styles (decision 233): a roof's look and framing saved under a name from the configurator, kept on
/// the project (<c>roofStyles</c>: <c>{id, label, shape, pitch, structure, attrs}</c>, ids starting <c>own:</c>), offered beside
/// the catalogue's and put on a roof the same way. Everything a style says is saved (shape, pitches, overhangs, covering,
/// drainage, the framing), not where the roof is (its outline, eave offset, edge rules); a vault's rise as a share of its span.
/// A style saved again under its name puts itself on again on every roof that has it.
/// </summary>
public static class ProjectRoofStyles
{
    public const string Key = "roofStyles";
    public const string Prefix = "own:";

    /// <summary>The fields a style carries.</summary>
    public static readonly string[] Fields =
    [
        "shape", "pitch", "overhang", "gableOverhang", "ridgeDirection", "shedFlip", "upperPitch", "mansardInset",
        "covering", RoofCoverings.Key, RoofCoverings.DeckKey, "deckThickness", "gutterProfile", "gutterMaterial", "gutterColor", "snowGuards",
        "structure", "rafterSection", "rafterSpacing", "purlins", "webSection", "timberGrade", "collarHeight", "rafterTail", "rafterSeat",
    ];

    public static bool IsOwn(string? id) => id?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    public static List<JsonObject> All(IModelView m) =>
        (Spatial.Project(m)?.Attrs[Key] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];

    public static JsonObject? Of(IModelView m, string? id) => IsOwn(id) ? All(m).FirstOrDefault(s => (string?)s["id"] == id) : null;

    /// <summary>A style made of a roof: its fields, and a vault's rise as a share of its span.</summary>
    public static JsonObject Snapshot(Entity roof, string id, string label)
    {
        var attrs = new JsonObject();
        foreach (var k in Fields) if (roof.Attrs[k] is { } v) attrs[k] = v.DeepClone();
        if (roof.Str("shape") == "barrel" && roof.NumOrNull("rise") is { } rise && Roof.BarrelSpan(Roof.Contour(roof).ToList()) is { } span && span > 1e-6)
            attrs["riseShare"] = Math.Round(rise / span, 4);
        return new JsonObject
        {
            ["id"] = id, ["label"] = label, ["shape"] = roof.Str("shape") ?? "hip", ["pitch"] = roof.NumOrNull("pitch") ?? 30,
            ["structure"] = roof.Str("structure") ?? "rafters", ["attrs"] = attrs,
        };
    }

    /// <summary>A project style put on a roof's attributes: its fields replace the roof's, the edge rules go, the roof says its style.</summary>
    public static void Apply(JsonObject attrs, JsonObject style, IReadOnlyList<Vec2> contour)
    {
        attrs.Remove("edges");
        foreach (var k in Fields) attrs.Remove(k);
        attrs.Remove("rise");
        foreach (var (k, v) in style["attrs"] as JsonObject ?? []) if (k != "riseShare") attrs[k] = v?.DeepClone();
        if ((string?)attrs["shape"] == "barrel" && Json.Number(style["attrs"]?["riseShare"]) is { } share && Roof.BarrelSpan(contour.ToList()) is { } span)
            attrs["rise"] = Math.Round(span * share, 3);
        attrs["style"] = (string?)style["id"];
    }

    /// <summary>
    /// Saves a roof as a project style under a name: a new one, or the one of that name (or id) saved again, which every roof
    /// of the style then takes again (refused when one of them would not build). The style's id.
    /// </summary>
    public static string Save(ICommandContext ctx, Entity roof, string label, string? id, Func<Entity, Entity> check)
    {
        label = label.Trim();
        if (label.Length == 0) throw new CommandException("A roof style needs a name.");
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var list = All(ctx.Model);
        var same = list.FirstOrDefault(s => id != null ? (string?)s["id"] == id : string.Equals((string?)s["label"], label, StringComparison.OrdinalIgnoreCase));
        if (id != null && same == null) throw new CommandException("The project has no such roof style.");
        var sid = (string?)same?["id"] ?? Prefix + ctx.NewId();
        var style = Snapshot(roof, sid, label);
        var next = list.Select(s => (string?)s["id"] == sid ? style : (JsonNode)s.DeepClone()).ToList();
        if (same == null) next.Add(style);
        var pa = project.CloneAttrs();
        pa[Key] = new JsonArray([.. next]);
        ctx.Replace(project.With(attrs: pa));
        // Every roof of the style takes it again (the one saved from too).
        foreach (var r in ctx.Model.OfClass(IfcClass.Roof).Where(r => r.Id == roof.Id || r.Str("style") == sid).ToList())
        {
            var attrs = (r.Id == roof.Id ? roof : r).CloneAttrs();
            Apply(attrs, style, Roof.Contour(r));
            ctx.Replace(check(r.With(attrs: attrs)));
        }
        return sid;
    }

    public static void Rename(ICommandContext ctx, string id, string label)
    {
        label = label.Trim();
        if (label.Length == 0) throw new CommandException("A roof style needs a name.");
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var list = All(ctx.Model);
        if (list.All(s => (string?)s["id"] != id)) throw new CommandException("The project has no such roof style.");
        var pa = project.CloneAttrs();
        pa[Key] = new JsonArray([.. list.Select(s => { var c = (JsonObject)s.DeepClone(); if ((string?)c["id"] == id) c["label"] = label; return (JsonNode)c; })]);
        ctx.Replace(project.With(attrs: pa));
    }

    /// <summary>Deletes a project style; its roofs keep how they look, shaped by hand from then on.</summary>
    public static void Delete(ICommandContext ctx, string id)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var list = All(ctx.Model);
        if (list.All(s => (string?)s["id"] != id)) throw new CommandException("The project has no such roof style.");
        var pa = project.CloneAttrs();
        var rest = list.Where(s => (string?)s["id"] != id).Select(s => (JsonNode)s.DeepClone()).ToArray();
        if (rest.Length > 0) pa[Key] = new JsonArray(rest); else pa.Remove(Key);
        ctx.Replace(project.With(attrs: pa));
        foreach (var r in ctx.Model.OfClass(IfcClass.Roof).Where(r => r.Str("style") == id).ToList())
        {
            var attrs = r.CloneAttrs();
            attrs.Remove("style");
            ctx.Replace(r.With(attrs: attrs));
        }
    }
}
