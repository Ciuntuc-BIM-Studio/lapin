using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Ifc;

/// <summary>
/// The project before an export, in the import survey's shape (File → Export IFC or Fragments): its elements by kind
/// and discipline, its storeys with how many each holds, so an export is chosen as an import is. The kinds and storeys
/// left out become the elements the export leaves out (<see cref="Excluded"/>).
/// </summary>
public static class ModelSurvey
{
    /// <summary>What an export counts: an element with a body (Lapin's own, or a reference's), not an opening, a grid or an annotation.</summary>
    public static bool IsElement(Session s, Entity e) =>
        (s.Catalog.Geometry(e.Class) != null || StoredBodies.Has(e))
        && e.Class is not (IfcClass.OpeningElement or IfcClass.Grid or IfcClass.Annotation)
        && (s.Catalog.Class(e.Class)?.Kind ?? "element") == "element";

    public static string KindOf(Entity e) => IfcSurvey.KindOf(e.Class, e.Name);

    public static JsonObject Of(Session s)
    {
        var m = s.Model;
        var kinds = new Dictionary<string, int>();
        var per = new Dictionary<string, int>();
        var none = 0;
        foreach (var e in m.Entities)
        {
            if (!IsElement(s, e)) continue;
            var kind = KindOf(e);
            kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
            if (Spatial.StoreyOf(m, e.Id) is { } st) per[st.Id] = per.GetValueOrDefault(st.Id) + 1; else none++;
        }
        var storeys = Spatial.Storeys(m).Where(st => per.ContainsKey(st.Id)).Select(st => (st.Id, st.Name, st.Num("elevation"), per[st.Id]));
        return IfcSurvey.Shape(Spatial.Project(m)?.Name ?? "Project", 0, "Lapin", null, kinds, storeys, none);
    }

    /// <summary>The elements an export leaves out: of a kind or on a storey not chosen ("" for those on none), and those given as hidden.</summary>
    public static HashSet<string> Excluded(Session s, JsonObject p)
    {
        static IReadOnlySet<string>? Set(JsonNode? n) => n is JsonArray a ? a.Select(x => (string?)x ?? "").ToHashSet() : null;
        var kinds = Set(p["kinds"]);
        var storeys = Set(p["storeys"]);
        var o = Set(p["hidden"])?.ToHashSet() ?? [];
        if (kinds == null && storeys == null) return o;
        foreach (var e in s.Model.Entities)
        {
            if (!IsElement(s, e)) continue;
            if (kinds != null && !kinds.Contains(KindOf(e))) { o.Add(e.Id); continue; }
            if (storeys != null && !storeys.Contains(Spatial.StoreyOf(s.Model, e.Id)?.Id ?? "")) o.Add(e.Id);
        }
        return o;
    }
}
