using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>
/// A structure type changed after it was made (report #42: a size typed
/// wrong): a column's or a beam's section and height, a strip foundation's
/// stem, footing, core and bottom, a pad footing's steps and bottom, the name.
/// The elements of the type follow it. A type still named after its size
/// takes the new size's name, unless a name is given; elements named as
/// their type are renamed with it.
/// </summary>
public static class StructureTypes
{
    /// <summary>The name a type is given from its size when it is made without one.</summary>
    public static string? AutoName(IModelView m, Entity type) => type.Class switch
    {
        IfcClass.ColumnType => Section.From(type.Attrs["section"]) is { } s ? $"Column {s.Label}" : null,
        IfcClass.BeamType => Section.From(type.Attrs["section"]) is { } s ? $"Beam {s.Label}" : null,
        IfcClass.FootingType when StripFootings.IsStrip(type) => LayerSets.LayersOf(m, type.Id) is { Count: > 0 } layers
            ? $"SF {(layers.FirstOrDefault(l => l.Core) ?? layers.MaxBy(l => l.Thickness)!).Thickness * 100:0}" : null,
        IfcClass.FootingType when PadFootings.IsPad(type) => PadFootings.Steps(type) is { Count: > 0 } steps ? $"PF {steps[0].Length * 100:0}×{steps[0].Width * 100:0}" : null,
        _ => null,
    };

    public static void Update(IEdit edit, Entity type, JsonObject a)
    {
        var oldName = type.Name;
        var wasAuto = AutoName(edit.Model, type) == oldName;
        var attrs = type.CloneAttrs();
        switch (type.Class)
        {
            case IfcClass.ColumnType or IfcClass.BeamType:
                if (a["section"] != null) attrs["section"] = Section.Parse(a["section"]).ToJson();
                if (type.Class == IfcClass.ColumnType && a.ContainsKey("height"))
                {
                    // Empty: as tall as the storey again.
                    if (Json.Number(a["height"]) is { } h) attrs["height"] = h > 0 ? h : throw new CommandException("A column's height must be positive.");
                    else attrs.Remove("height");
                }
                break;
            case IfcClass.FootingType when StripFootings.IsStrip(type):
                foreach (var key in new[] { "stemHeight", "footingWidth", "footingHeight" })
                    if (Json.Number(a[key]) is { } v) attrs[key] = v > 0 ? v : throw new CommandException("The stem's height and the footing's width and height must be positive.");
                if (Json.Number(a["bottom"]) is { } sb) attrs["bottom"] = sb;
                break;
            case IfcClass.FootingType when PadFootings.IsPad(type):
                if (a["steps"] is JsonArray arr)
                {
                    var steps = arr.OfType<JsonObject>().Select(s => new PadStep(Json.Number(s["length"]) ?? 0, Json.Number(s["width"]) ?? 0, Json.Number(s["height"]) ?? 0)).ToList();
                    if (steps.Count == 0) throw new CommandException("A pad footing needs at least one step.");
                    if (steps.Any(s => !(s.Length > 0 && s.Width > 0 && s.Height > 0))) throw new CommandException("Every step needs a length, a width and a height.");
                    attrs["steps"] = new JsonArray(steps.Select(s => (JsonNode)new JsonObject { ["length"] = s.Length, ["width"] = s.Width, ["height"] = s.Height }).ToArray());
                }
                if (Json.Number(a["bottom"]) is { } pb) attrs["bottom"] = pb;
                break;
            default:
                throw new CommandException($"„{type.Name}” is not a column, beam or footing type.");
        }
        edit.Replace(type.With(attrs: attrs));

        // A strip foundation's core: the stem's concrete layer, the others kept.
        if (type.Class == IfcClass.FootingType && StripFootings.IsStrip(type) && Json.Number(a["core"]) is { } core)
        {
            if (!(core > 0)) throw new CommandException("The core must be thicker than nothing.");
            var layers = LayerSets.LayersOf(edit.Model, type.Id);
            if (layers.Count > 0)
            {
                var coreAt = layers.ToList().FindIndex(l => l.Core) is >= 0 and var i ? i : layers.ToList().IndexOf(layers.MaxBy(l => l.Thickness)!);
                var specs = layers.Select((l, k) => new LayerSpec(edit.Model.Get(l.Material)?.Name ?? "Reinforced concrete", k == coreAt ? core : l.Thickness, l.Core, Name: l.Name)).ToList();
                LayerSets.Rewrite(edit, edit.Model.Get(type.Id)!, specs);
                foreach (var f in edit.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(e => e.Class == IfcClass.Footing).ToList())
                    StripFootings.SetType(edit, f, edit.Model.Get(type.Id)!);
            }
        }

        var current = edit.Model.Get(type.Id)!;
        var name = a.ContainsKey("name") && !string.IsNullOrWhiteSpace((string?)a["name"]) ? ((string)a["name"]!).Trim()
            : a.ContainsKey("name") || wasAuto ? AutoName(edit.Model, current) ?? oldName
            : oldName;
        if (name == oldName) return;
        edit.Replace(current.With(name: name));
        foreach (var e in edit.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(e => e.Name == oldName).ToList())
            edit.Replace(e.With(name: name));
    }
}
