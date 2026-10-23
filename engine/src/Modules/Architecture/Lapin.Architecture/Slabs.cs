using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Floors. An IfcSlab is its contour in plan (storey-local metres, counter-
/// clockwise) and the offset of its top from its storey; its build-up comes
/// from its IfcSlabType's layer set, laid from the top down by its own
/// IfcMaterialLayerSetUsage (AXIS3, NEGATIVE). Layers are listed top to bottom.
/// </summary>
public static class Slabs
{
    /// <summary>The type new floors get when none is chosen: a finish, the screed, the reinforced concrete slab (the core).</summary>
    public static readonly IReadOnlyList<LayerSpec> DefaultLayers =
    [
        new("Floor finish", 0.02),
        new("Screed", 0.05),
        new("Reinforced concrete", 0.15, Core: true),
    ];

    public static IReadOnlyList<Vec2> Contour(Entity slab) =>
        slab.Attrs["contour"] is JsonArray arr
            ? arr.OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0)).ToList()
            : [];

    public static JsonArray ContourJson(IEnumerable<Vec2> pts) => new(pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray());

    /// <summary>A contour as a command gives it: at least three distinct points, made counter-clockwise; refused when it encloses nothing.</summary>
    public static IReadOnlyList<Vec2> CheckContour(IReadOnlyList<Vec2> pts)
    {
        var clean = new List<Vec2>();
        foreach (var p in pts)
            if (clean.Count == 0 || (p - clean[^1]).Length > 1e-6) clean.Add(p);
        if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 1e-6) clean.RemoveAt(clean.Count - 1);
        if (clean.Count < 3) throw new CommandException("A floor needs at least three corners.");
        if (Math.Abs(Polygon2.SignedArea(clean)) < 1e-6) throw new CommandException("The floor's outline encloses nothing.");
        return Polygon2.EnsureCcw(clean).ToList();
    }

    public static Entity EnsureDefaultType(IEdit edit) =>
        edit.Model.OfClass(IfcClass.SlabType).FirstOrDefault() ?? CreateType(edit, null, DefaultLayers);

    public static Entity CreateType(IEdit edit, string? name, IReadOnlyList<LayerSpec> layers)
    {
        LayerSets.Validate(layers, "A floor type");
        var core = layers.FirstOrDefault(l => l.Core) ?? layers.MaxBy(l => l.Thickness)!;
        var typeName = string.IsNullOrWhiteSpace(name) ? $"{core.Material} floor {Math.Round(layers.Sum(l => l.Thickness) * 1000)}" : name.Trim();
        var type = new Entity(edit.NewId(), IfcClass.SlabType, typeName, [], "FLOOR");
        edit.Add(type);
        LayerSets.Attach(edit, type, layers);
        return type;
    }

    public static void UpdateType(IEdit edit, Entity type, string? name, IReadOnlyList<LayerSpec>? layers)
    {
        if (name != null)
        {
            if (name.Trim().Length == 0) throw new CommandException("A type's name cannot be empty.");
            edit.Replace(type.With(name: name.Trim()));
        }
        if (layers == null) return;
        LayerSets.Validate(layers, "A floor type");
        LayerSets.Rewrite(edit, edit.Model.Get(type.Id)!, layers);
        foreach (var slab in edit.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(s => s.Class == IfcClass.Slab).ToList())
            LayOut(edit, slab, type);
    }

    /// <summary>The floor's usage: the layers from its top down.</summary>
    static void LayOut(IEdit edit, Entity slab, Entity type) =>
        LayerSets.LayOut(edit, slab, type, "AXIS3", (_, _) => 0, negativeWhenNew: true);

    public static void SetType(IEdit edit, Entity slab, Entity type)
    {
        edit.Relate(IfcClass.RelDefinesByType, type.Id, slab.Id);
        LayOut(edit, slab, type);
    }

    public static Entity Add(IEdit edit, string storeyId, IReadOnlyList<Vec2> contour, double offset, Entity? type)
    {
        var pts = CheckContour(contour);
        type ??= EnsureDefaultType(edit);
        var slab = new Entity(edit.NewId(), IfcClass.Slab, type.Name, new JsonObject { ["contour"] = ContourJson(pts), ["offset"] = offset }, "FLOOR");
        edit.Add(slab);
        Spatial.Contain(edit, storeyId, slab.Id);
        SetType(edit, slab, type);
        return slab;
    }

    /// <summary>The floor's layers, top first: their bands below its top (negative), their materials.</summary>
    public static IReadOnlyList<(double From, double To, string? Material, string Name, int Index)> Section(IModelView m, Entity slab)
    {
        if (LayerSets.UsageOf(m, slab.Id) is { } u && Layers.ReadUsage(u) is { } usage && m.Get(usage.ForLayerSet) is { } set)
        {
            var layers = Layers.Read(set);
            var bands = Layers.Bands(layers, usage);
            return layers.Select((l, i) => (bands[i].From, bands[i].To, (string?)l.Material, l.Name ?? m.Get(l.Material)?.Name ?? $"Layer {i + 1}", i)).ToList();
        }
        var t = slab.NumOrNull("thickness") ?? 0.2;
        return [(-t, 0.0, null, slab.Name, 0)];
    }

    /// <summary>The floor: a slab per layer, from its top down; in plan, its outline (it lies below the cut).</summary>
    public static ElementGeometry? Geometry(Entity slab, IModelView m, IServices? services = null)
    {
        var contour = Contour(slab);
        if (contour.Count < 3) return new ElementGeometry([], Diagnostics: ["The floor has no outline."]);
        var top = Top(m, slab);
        var parts = new List<GeometryPart>();
        foreach (var (from, to, material, name, index) in Section(m, slab))
            if (to - from > 1e-9 && Prism.ExtrudeFootprint(contour, top + from, to - from, name) is { } solid)
                parts.Add(new GeometryPart(name, Materials.ColorOf(m, material), solid, material, $"layer:{index}"));
        var problems = new List<string>();
        parts = Cuts.Apply(slab, m, services, parts, problems);
        return new ElementGeometry(parts, [new PlanItem("outline", true, contour)], contour, problems.Count > 0 ? problems : null);
    }

    static double Top(IModelView m, Entity slab) => (Spatial.StoreyOf(m, slab.Id)?.Num("elevation") ?? 0) + slab.Num("offset");

    /// <summary>The floor's whole thickness over its outline: what it cuts lower priorities with.</summary>
    public static Solid? Envelope(Entity slab, IModelView m)
    {
        var contour = Contour(slab);
        if (contour.Count < 3) return null;
        var section = Section(m, slab);
        var top = Top(m, slab);
        var bottom = section.Min(b => b.From);
        return Prism.ExtrudeFootprint(contour, top + bottom, section.Max(b => b.To) - bottom, "slab");
    }

    public static IEnumerable<QuantitySet> Quantities(Entity slab, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        var contour = Contour(slab);
        var area = Math.Abs(Polygon2.SignedArea(contour));
        var section = Section(m, slab);
        var width = section.Max(b => b.To) - section.Min(b => b.From);
        var net = g.Parts.Sum(p => Measure.Volume(p.Solid));
        yield return new QuantitySet("Qto_SlabBaseQuantities",
        [
            new("Width", width, "LENGTH"),
            new("Perimeter", Polygon2.Perimeter(contour), "LENGTH"),
            new("GrossArea", area, "AREA", "Gross area"),
            new("NetArea", width > 0 ? net / width : 0, "AREA", "Net area"),
            new("GrossVolume", area * width, "VOLUME", "Gross volume"),
            new("NetVolume", net, "VOLUME", "Net volume"),
        ]);
        foreach (var (from, to, material, name, index) in section)
        {
            var part = g.Parts.FirstOrDefault(p => p.Key == $"layer:{index}");
            if (part == null) continue;
            var v = Measure.Volume(part.Solid);
            yield return new QuantitySet("Lapin_SlabLayerQuantities",
            [
                new("Width", to - from, "LENGTH"),
                new("NetArea", to - from > 0 ? v / (to - from) : 0, "AREA", "Net area"),
                new("NetVolume", v, "VOLUME", "Net volume"),
            ], $"layer:{index}", name, material);
        }
    }
}
