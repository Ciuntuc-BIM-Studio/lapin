using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Archicad;

/// <summary>
/// Archicad's attributes matched across receives by their GUIDs (docs/plan-archicad.md A2): a wall or slab type made from a composite
/// keeps the composite's GUID and a hash of it (<see cref="TagKey"/>), and a Lapin material made from a building material keeps the
/// building material's GUID. At the next receive a renamed building material renames its material (the layers name materials by id),
/// and a changed composite rewrites its type's layers in place (<see cref="Prepare"/>): the type stays the same entity, and the
/// elements made of it come in again, so they are rebuilt on the new layers and measured again by the fidelity gate. Without a GUID
/// (a type made before A2, or by another import), the recognisers' own match by name and layers holds, as before.
/// </summary>
public static class ArchicadTypes
{
    /// <summary>The attribute a type or material keeps its Archicad attribute's GUID in: <c>{guid, hash, name}</c>.</summary>
    public const string TagKey = "archicadAttribute";

    /// <summary>A composite element's composite (its attribute GUID); null for a basic or profiled one.</summary>
    public static string? CompositeOf(ArchicadElement el) =>
        (string?)el.Details["structureType"] == "Composite" ? (string?)el.Details["compositeId"]?["guid"] : null;

    public static string Hash(JsonObject attribute) => Hashes.Sha256Hex(attribute.ToJsonString())[..32];

    static string? GuidOf(Entity e) => (string?)(e.Attrs[TagKey] as JsonObject)?["guid"];

    static IEnumerable<Entity> TypesOf(IModelView m, string guid) =>
        m.OfClass(IfcClass.WallType).Concat(m.OfClass(IfcClass.SlabType)).Where(t => string.Equals(GuidOf(t), guid, StringComparison.OrdinalIgnoreCase));

    /// <summary>The types tagged with a composite: their composites' GUIDs and the hash each was tagged with.</summary>
    public static Dictionary<string, string?> Tagged(IModelView m)
    {
        var o = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in m.OfClass(IfcClass.WallType).Concat(m.OfClass(IfcClass.SlabType)))
            if (GuidOf(t) is { } g) o[g] = (string?)(t.Attrs[TagKey] as JsonObject)?["hash"];
        return o;
    }

    /// <summary>A composite's layers as the wall recogniser makes them (Archicad's order, outside first; one core, the thickest core skin).</summary>
    static List<LayerSpec> WallLayers(List<ArchicadSkin> skins) => [.. ArchicadForeign.Section(skins, 0, false).Layers];

    /// <summary>A composite's layers as the slab recogniser makes them (top down, its core skins marked).</summary>
    static List<LayerSpec> SlabLayers(List<ArchicadSkin> skins) => [.. skins.Select(s => new LayerSpec(s.Material, Math.Round(s.Thickness, 4), s.Core))];

    /// <summary>
    /// Before the elements come in: each tagged type whose composite changed (its hash) has its layers rewritten from the composite and
    /// its name made the composite's. Gives the composites that changed (GUID → name), whose elements come in again.
    /// </summary>
    public static Dictionary<string, string> Prepare(IEdit edit, ArchicadModel a)
    {
        var m = edit.Model;
        var changed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (guid, comp) in a.Composites)
        {
            var hash = Hash(comp);
            foreach (var t in TypesOf(m, guid).ToList())
            {
                if ((string?)(t.Attrs[TagKey] as JsonObject)?["hash"] == hash) continue;
                if (ArchicadForeign.CompositeSkins(a, comp) is not { } skins) continue;
                LayerSets.Rewrite(edit, t, t.Class == IfcClass.SlabType ? SlabLayers(skins) : WallLayers(skins));
                var now = m.Get(t.Id)!;
                var attrs = now.CloneAttrs();
                var name = (string?)comp["name"] is { Length: > 0 } n ? n : now.Name;
                attrs[TagKey] = new JsonObject { ["guid"] = guid, ["hash"] = hash, ["name"] = name };
                edit.Replace(now.With(name: name, attrs: attrs));
                changed[guid] = name;
            }
        }
        return changed;
    }

    /// <summary>The materials tagged with a building material, by its GUID.</summary>
    static Dictionary<string, Entity> MaterialsTagged(IModelView m)
    {
        var o = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
        foreach (var mat in m.OfClass(IfcClass.Material)) if (GuidOf(mat) is { } g) o.TryAdd(g, mat);
        return o;
    }

    /// <summary>Before the elements come in: a material tagged with a building material Archicad renamed takes its new name (unless another material has it).</summary>
    public static List<(string From, string To)> PrepareMaterials(IEdit edit, ArchicadModel a) => RenameMaterials(edit, a.BuildingMaterials);

    /// <summary>The building materials' GUIDs the project's materials are tagged with.</summary>
    public static IReadOnlyList<string> TaggedMaterials(IModelView m) => [.. MaterialsTagged(m).Keys.Order(StringComparer.Ordinal)];

    /// <summary>The materials tagged with these building materials renamed as Archicad now names them (unless another material has the name).</summary>
    public static List<(string From, string To)> RenameMaterials(IEdit edit, IReadOnlyDictionary<string, string> names)
    {
        var m = edit.Model;
        var renamed = new List<(string, string)>();
        var tagged = MaterialsTagged(m);
        foreach (var (guid, name) in names)
        {
            if (!tagged.TryGetValue(guid, out var mat) || string.Equals(mat.Name, name, StringComparison.Ordinal)) continue;
            if (Materials.Find(m, name) is { } other && other.Id != mat.Id) continue;
            var attrs = mat.CloneAttrs();
            attrs[TagKey] = new JsonObject { ["guid"] = guid, ["name"] = name };
            edit.Replace(mat.With(name: name, attrs: attrs));
            renamed.Add((mat.Name, name));
        }
        return renamed;
    }

    /// <summary>
    /// After recognition: the types the native walls and slabs took tagged with their composites (a type made now, or one named as the
    /// composite whose occurrences all came from Archicad: a type an earlier receive made), and the materials named as building materials
    /// tagged with theirs. A type already tagged keeps its tag. Gives the report: types updated, materials renamed, attributes tagged.
    /// </summary>
    public static JsonObject Tag(IEdit edit, ArchicadModel a, IReadOnlyList<(string Id, ArchicadElement El)> offered, IReadOnlySet<string> created,
        IReadOnlyDictionary<string, string> changed, List<(string From, string To)> renamed)
    {
        var m = edit.Model;
        var tagged = 0;
        foreach (var (id, el) in offered)
        {
            if (el.Type is not ("Wall" or "Slab") || CompositeOf(el) is not { } guid || !a.Composites.TryGetValue(guid, out var comp)) continue;
            if (m.Get(id) is not { } e || StoredBodies.Has(e) || m.RelatingOf(id, IfcClass.RelDefinesByType) is not { } t || GuidOf(t) != null) continue;
            var name = (string?)comp["name"];
            var ours = created.Contains(t.Id) || string.Equals(t.Name, name, StringComparison.Ordinal)
                && m.RelatedOf(t.Id, IfcClass.RelDefinesByType).All(x => x.Attrs[ArchicadReceive.Key] is JsonObject);
            if (!ours) continue;
            var attrs = t.CloneAttrs();
            attrs[TagKey] = new JsonObject { ["guid"] = guid, ["hash"] = Hash(comp), ["name"] = name };
            edit.Replace(t.With(attrs: attrs));
            tagged++;
        }
        var have = MaterialsTagged(m);
        var taggedGuids = have.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (guid, name) in a.BuildingMaterials)
        {
            if (taggedGuids.Contains(guid) || Materials.Find(m, name) is not { } mat || GuidOf(mat) != null) continue;
            var attrs = mat.CloneAttrs();
            attrs[TagKey] = new JsonObject { ["guid"] = guid, ["name"] = name };
            edit.Replace(mat.With(attrs: attrs));
            taggedGuids.Add(guid);
            tagged++;
        }
        return new JsonObject
        {
            ["updated"] = new JsonArray([.. changed.Values.Distinct().Select(n => (JsonNode)n)]),
            ["materialsRenamed"] = new JsonArray([.. renamed.Select(r => (JsonNode)new JsonObject { ["from"] = r.From, ["to"] = r.To })]),
            ["tagged"] = tagged,
        };
    }
}
