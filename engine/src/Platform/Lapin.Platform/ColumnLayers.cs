using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>A finish layer round a column's core (decision 719): its material (an id), its thickness, its name and category, the faces it covers (null: all round).</summary>
public sealed record WrapLayer(string Material, double Thickness, string? Name = null, string? Category = null, IReadOnlyList<string>? Faces = null)
{
    /// <summary>Whether it covers the faces of a side ("+x", "-x", "+y", "-y": the section's own axes, x along its width).</summary>
    public bool Covers(string face) => Faces == null || Faces.Contains(face);
}

/// <summary>
/// A column type's finish layers (decision 719, forum topic 169, issue #108): plaster, fireproofing boards, insulation laid
/// round its core, outward, each a material and a thickness, all round or on some of its faces. Kept on the IfcColumnType as
/// <c>finishLayers</c> ([{material, thickness, name?, category?, faces?}]), the core staying the type's own section and material
/// (its IfcMaterialProfileSet). Each layer is a part of the column keyed <c>layer:&lt;n&gt;</c> (n from 1; the core keeps its
/// key <c>body</c>), so the views draw it with its material's hatch, Visibility / Graphics' subcategories and the view styles'
/// layers modes take it as a wall's layer (<see cref="BandsOf"/>), and its quantities are measured by layer. A type without
/// them builds and draws as before.
/// </summary>
public static class ColumnLayers
{
    public const string Key = "finishLayers";

    /// <summary>The faces a layer can cover, by the section's own axes: +x and -x across its width, +y and -y across its depth.</summary>
    public static readonly string[] Faces = ["+x", "-x", "+y", "-y"];

    /// <summary>A layer's part key: <c>layer:1</c> for the first outside the core.</summary>
    public static string PartKey(int index) => $"layer:{index + 1}";

    /// <summary>The layers a type keeps (none: an empty list).</summary>
    public static IReadOnlyList<WrapLayer> Read(Entity? type)
    {
        if (type?.Attrs[Key] is not JsonArray arr) return [];
        var o = new List<WrapLayer>();
        foreach (var n in arr.OfType<JsonObject>())
        {
            if ((string?)n["material"] is not { Length: > 0 } mat || Json.Number(n["thickness"]) is not { } t || !(t > 0)) continue;
            var faces = n["faces"] is JsonArray fa ? fa.Select(x => (string?)x).OfType<string>().Where(Faces.Contains).Distinct().ToList() : null;
            o.Add(new WrapLayer(mat, t, (string?)n["name"] is { Length: > 0 } nm ? nm : null, (string?)n["category"] is { Length: > 0 } c ? c : null, faces is { Count: > 0 and < 4 } ? faces : null));
        }
        return o;
    }

    public static JsonArray Write(IEnumerable<WrapLayer> layers) => new([.. layers.Select(l =>
    {
        var o = new JsonObject { ["material"] = l.Material, ["thickness"] = l.Thickness };
        if (l.Name != null) o["name"] = l.Name;
        if (l.Category != null) o["category"] = l.Category;
        if (l.Faces != null) o["faces"] = new JsonArray([.. l.Faces.Select(f => (JsonNode)f)]);
        return (JsonNode)o;
    })]);

    /// <summary>
    /// Layers as a command gives them, checked: [{material (a name, made when the project has none, or a material's id),
    /// thickness (m, over 0, at most 0.5), name?, category? (one of the materials' categories), faces? (a subset of
    /// <see cref="Faces"/>; all or none: all round)}]; null or [] none.
    /// </summary>
    public static List<WrapLayer> Parse(IEdit edit, JsonNode? node)
    {
        if (node is null) return [];
        if (node is not JsonArray arr) throw new CommandException("A column's finish layers are a list: [{material, thickness, faces?}].");
        var o = new List<WrapLayer>();
        foreach (var n in arr)
        {
            if (n is not JsonObject l) throw new CommandException("Every finish layer is an object: material and thickness.");
            var material = ((string?)l["material"])?.Trim();
            if (string.IsNullOrEmpty(material)) throw new CommandException("Every finish layer needs a material.");
            if (Json.Number(l["thickness"]) is not { } t || !(t > 0) || t > 0.5) throw new CommandException("Every finish layer needs a thickness, over 0 and at most 500 mm.");
            var id = edit.Model.Get(material) is { Class: IfcClass.Material } known ? known.Id : Materials.Ensure(edit, material).Id;
            string? category = null;
            if ((string?)l["category"] is { } cs && cs.Trim().Length > 0)
                category = Materials.Categories.FirstOrDefault(x => string.Equals(x, cs.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new CommandException($"„{cs.Trim()}” is not a layer's category ({string.Join(", ", Materials.Categories)}).");
            List<string>? faces = null;
            if (l["faces"] is JsonArray fa)
            {
                faces = [];
                foreach (var f in fa)
                    faces.Add((string?)f is { } fs && Faces.Contains(fs) ? fs : throw new CommandException($"A finish layer's faces are {string.Join(", ", Faces)}."));
                faces = faces.Distinct().ToList();
                if (faces.Count == 0) throw new CommandException("A finish layer covers at least one face (leave faces out for all round).");
                if (faces.Count == 4) faces = null;
            }
            var name = (string?)l["name"] is { } nm && nm.Trim().Length > 0 ? nm.Trim() : null;
            o.Add(new WrapLayer(id, Math.Round(t, 6), name, category, faces));
        }
        return o;
    }

    /// <summary>
    /// A column's core and finish layers as bands (the core first, then outward), for what reads an element's layers by its
    /// part keys (subcategories, layers modes, the core of a small-scale cut): <c>From</c>, <c>To</c> the distances from the
    /// core's outline; null for anything that is not a column with finish layers.
    /// </summary>
    public static IReadOnlyList<LayerBand>? BandsOf(IModelView m, string id)
    {
        if (m.Get(id) is not { Class: IfcClass.Column } column || column.Attrs["at"] is null) return null;
        var type = Types.Of(m, id, IfcClass.ColumnType);
        var layers = Read(type);
        if (layers.Count == 0) return null;
        var core = type?.Str("material") ?? "";
        var o = new List<LayerBand> { new(0, 0, core, m.Get(core)?.Name ?? "Core", 0, true) };
        var at = 0.0;
        for (var i = 0; i < layers.Count; i++)
        {
            var l = layers[i];
            o.Add(new LayerBand(at, at + l.Thickness, l.Material, l.Name ?? m.Get(l.Material)?.Name ?? $"Layer {i + 1}", i + 1, false, Category: l.Category));
            at += l.Thickness;
        }
        return o;
    }
}
