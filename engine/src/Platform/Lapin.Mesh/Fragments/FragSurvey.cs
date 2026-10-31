using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Model;

namespace Lapin.Mesh.Fragments;

/// <summary>
/// What a Fragments file holds, before it is imported, in the IFC survey's shape (docs/fragments.md): its items with
/// geometry by kind and discipline, and its storeys with how many each contains, so File → Import Fragments… offers the
/// same choice as an IFC import. The import reads the kinds and storeys by the same rules.
/// </summary>
public static class FragSurvey
{
    /// <summary>An item's IFC class, from its category ("IFCWALL" → IfcWall); a proxy when it has none.</summary>
    public static string ClassOf(FragModel m, int item) =>
        item < m.Categories.Length && m.Categories[item] is { Length: > 3 } c && c.StartsWith("IFC", StringComparison.OrdinalIgnoreCase) ? IfcModel.ClassName(c) : IfcClass.BuildingElementProxy;

    public static string KindOf(FragModel m, int item) =>
        IfcSurvey.KindOf(ClassOf(m, item), m.AttributesOf(item).TryGetValue("Name", out var n) && n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null);

    /// <summary>The file's storeys: their local id, the key the interface chooses them by (their GlobalId, else "local:id"), name and elevation.</summary>
    public static List<(uint Local, string Key, string Name, double? Elevation)> Storeys(FragModel m)
    {
        var o = new List<(uint, string, string, double?)>();
        for (var i = 0; i < m.LocalIds.Length; i++)
        {
            if (i >= m.Categories.Length || m.Categories[i] != "IFCBUILDINGSTOREY") continue;
            var attrs = m.AttributesOf(i);
            string? Text(string k) => attrs.TryGetValue(k, out var v) && v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : null;
            double? Number(string k) => attrs.TryGetValue(k, out var v) && v is JsonValue jv && jv.TryGetValue<double>(out var d) ? d : null;
            var local = m.LocalIds[i];
            o.Add((local, KeyOf(m, local), Text("Name") ?? $"Storey {local}", Number("Elevation")));
        }
        return o;
    }

    /// <summary>A storey's key: its GlobalId, else its local id.</summary>
    public static string KeyOf(FragModel m, uint local)
    {
        var k = Array.IndexOf(m.GuidsItems, local);
        return k >= 0 && k < m.Guids.Length && m.Guids[k].Length == 22 ? m.Guids[k] : $"local:{local}";
    }

    /// <summary>The storey (its local id) each item the file contains in one stands on.</summary>
    public static Dictionary<uint, uint> StoreyOfItems(FragModel m, IEnumerable<uint> items, IReadOnlySet<uint> storeys)
    {
        var o = new Dictionary<uint, uint>();
        foreach (var local in items)
            foreach (var id in m.RelationsOf(local).GetValueOrDefault("ContainedInStructure") ?? [])
                if (storeys.Contains(id)) { o[local] = id; break; }
        return o;
    }

    /// <summary>The items with geometry, by local id, with their index among the file's items.</summary>
    public static Dictionary<uint, int> ItemsWithGeometry(FragModel m)
    {
        var o = new Dictionary<uint, int>();
        foreach (var item in m.Meshes.MeshesItems) if (item < m.LocalIds.Length) o[m.LocalIds[item]] = (int)item;
        return o;
    }

    public static JsonObject Of(FragModel m, string name, long size)
    {
        var items = ItemsWithGeometry(m);
        var storeys = Storeys(m);
        var storeyOf = StoreyOfItems(m, items.Keys, storeys.Select(s => s.Local).ToHashSet());
        var kinds = new Dictionary<string, int>();
        var per = storeys.ToDictionary(s => s.Local, _ => 0);
        var none = 0;
        foreach (var (local, item) in items)
        {
            var kind = KindOf(m, item);
            kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
            if (storeyOf.TryGetValue(local, out var s) && per.ContainsKey(s)) per[s]++; else none++;
        }
        var application = m.MetadataJson?["application"]?.ToString() ?? m.MetadataJson?["software"]?.ToString();
        return IfcSurvey.Shape(name, size, "Fragments", application, kinds,
            storeys.Where(s => per[s.Local] > 0).Select(s => (s.Key, s.Name, s.Elevation ?? 0, per[s.Local])), none);
    }
}
