using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The wholes an element is a part of (the interface's drill-down picking, View → Select assemblies step by step):
/// its aggregation chain through <c>IfcRelAggregates</c> up to the spatial structure (a steel hall over its members,
/// an IfcElementAssembly over its parts, an assembly in another), and above it the group the chain is in
/// (<c>IfcRelAssignsToGroup</c>, docs/groups.md; groups do not nest). The interface reads the same rule from the
/// relationships it holds (ui/src/platform/assemblies.ts) so a click needs no round trip; <c>core.assemblyPath</c> is
/// for the other clients (MCP) and is the rule's specification.
/// </summary>
public static class Assemblies
{
    /// <summary>The spatial structure, where an aggregation chain ends (an element's storey, a storey's building).</summary>
    public static readonly HashSet<string> SpatialClasses =
    [
        IfcClass.Project, IfcClass.Site, IfcClass.Building, IfcClass.BuildingStorey, IfcClass.Space,
        "IfcFacility", "IfcFacilityPart", "IfcBridge", "IfcBridgePart", "IfcRoad", "IfcRoadPart", "IfcRailway", "IfcRailwayPart",
        "IfcMarineFacility", "IfcMarinePart", "IfcSpatialZone", "IfcExternalSpatialElement",
    ];

    /// <summary>The whole <paramref name="id"/> is a part of, below the spatial structure; null at the top.</summary>
    public static Entity? WholeOf(IModelView m, string id) =>
        m.RelatingOf(id, IfcClass.RelAggregates) is { } w && !SpatialClasses.Contains(w.Class) ? w : null;

    /// <summary>
    /// The chain from the top to <paramref name="id"/>: the group (when the chain is in one), the outermost whole, …,
    /// the element itself; just the element when it is part of nothing; empty when there is no such entity.
    /// </summary>
    public static List<string> PathOf(IModelView m, string id)
    {
        if (m.Get(id) is null) return [];
        var path = new List<string> { id };
        var seen = new HashSet<string> { id };
        for (var w = WholeOf(m, id); w != null && seen.Add(w.Id); w = WholeOf(m, w.Id)) path.Insert(0, w.Id);
        if (m.Get(path[0]) is not { Class: Groups.Class })
            foreach (var x in path)
                if (Groups.Of(m, x) is { } g) { path.Insert(0, g.Id); break; }
        return path;
    }

    /// <summary>The query <c>core.assemblyPath {id}</c> → <c>{path: [top, …, id]}</c>, or null for an unknown id.</summary>
    public static void Register(IModuleRegistry r) =>
        r.Query("core.assemblyPath", (q, a) => PathOf(q.Model, (string?)a["id"] ?? "") is { Count: > 0 } p
            ? new JsonObject { ["path"] = new JsonArray([.. p.Select(x => (JsonNode)x)]) } : null);
}
