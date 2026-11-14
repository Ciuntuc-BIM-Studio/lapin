using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The elements of a class whose layers reach past their edges and cut what they meet (forum topic 89, docs/plan-layer-extents.md),
/// read by the builds of their neighbours under a narrow key: an edit that gives an element such extents, changes them or takes
/// them away touches <see cref="ReadKey"/>, so the walls and floors round are built again; an edit of anything else of the class
/// is not their business. A build reads each holder it uses through the model (<c>m.Get</c>), which follows that one's changes.
/// </summary>
public static class LayerExtentReads
{
    /// <summary>The element's attribute holding its layers' extents.</summary>
    public const string AttrKey = "layerExtents";

    /// <summary>The key touched when an element of the class gets, changes or loses its layers' extents.</summary>
    public static string ReadKey(string ifcClass) => "extents:" + ifcClass;

    /// <summary>Whether an edit changed an element's extents (it got them, lost them, or they differ).</summary>
    public static bool Changed(Entity? before, Entity? after) => !JsonNode.DeepEquals(before?.Attrs[AttrKey], after?.Attrs[AttrKey]);

    static readonly ConditionalWeakTable<IModelView, Dictionary<string, (long Revision, List<string> Ids)>> Cache = new();

    /// <summary>
    /// The ids of the class's elements with extents, unnoted but for <see cref="ReadKey"/>; listed once per revision of the store.
    /// The caller reads those it keeps through <paramref name="m"/>.
    /// </summary>
    public static IReadOnlyList<string> Holders(IModelView m, string ifcClass)
    {
        var inner = m;
        if (m is TrackingView tv) { tv.Read.Add(ReadKey(ifcClass)); inner = tv.Inner; }
        if (inner is not ModelStore) return [.. inner.OfClass(ifcClass).Where(e => e.Attrs[AttrKey] is JsonArray { Count: > 0 }).Select(e => e.Id)];
        var byClass = Cache.GetOrCreateValue(inner);
        lock (byClass)
        {
            if (byClass.TryGetValue(ifcClass, out var hit) && hit.Revision == inner.Revision) return hit.Ids;
            var ids = inner.OfClass(ifcClass).Where(e => e.Attrs[AttrKey] is JsonArray { Count: > 0 }).Select(e => e.Id).ToList();
            byClass[ifcClass] = (inner.Revision, ids);
            return ids;
        }
    }
}
