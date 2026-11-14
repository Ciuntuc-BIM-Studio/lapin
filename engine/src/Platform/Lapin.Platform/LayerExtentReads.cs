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

    /// <summary>The type class whose extents are the defaults of a class's elements (stage 4): a wall type's for walls, a floor type's for floors.</summary>
    public static string? TypeClassOf(string ifcClass) => ifcClass switch
    {
        IfcClass.Wall => IfcClass.WallType,
        IfcClass.Slab => IfcClass.SlabType,
        _ => null,
    };

    /// <summary>The class a type class gives its extents to; null for any other class.</summary>
    public static string? ElementClassOf(string typeClass) => typeClass switch
    {
        IfcClass.WallType => IfcClass.Wall,
        IfcClass.SlabType => IfcClass.Slab,
        _ => null,
    };

    static bool Holds(Entity e) => e.Attrs[AttrKey] is JsonArray { Count: > 0 };

    static readonly ConditionalWeakTable<IModelView, Dictionary<string, (long Revision, List<string> Ids, List<string> Types)>> Cache = new();

    /// <summary>
    /// The ids of the class's elements with extents of their own or from their type, unnoted but for <see cref="ReadKey"/> (and,
    /// when types give extents, those types and the relationships typing their elements); listed once per revision of the store.
    /// The caller reads those it keeps through <paramref name="m"/>.
    /// </summary>
    public static IReadOnlyList<string> Holders(IModelView m, string ifcClass)
    {
        var inner = m;
        var tv = m as TrackingView;
        if (tv != null) { tv.Read.Add(ReadKey(ifcClass)); inner = tv.Inner; }
        (List<string> Ids, List<string> Types) List()
        {
            var types = TypeClassOf(ifcClass) is { } tc ? inner.OfClass(tc).Where(Holds).Select(t => t.Id).ToList() : [];
            var ids = inner.OfClass(ifcClass).Where(Holds).Select(e => e.Id).ToHashSet();
            foreach (var t in types)
                foreach (var r in inner.RelationsOf(t, IfcClass.RelDefinesByType).Where(r => r.Relating == t))
                    foreach (var id in r.Related)
                        if (inner.Get(id) is { } e && e.Class == ifcClass) ids.Add(id);
            return ([.. inner.OfClass(ifcClass).Select(e => e.Id).Where(ids.Contains)], types);
        }
        (List<string> Ids, List<string> Types) got;
        if (inner is not ModelStore) got = List();
        else
        {
            var byClass = Cache.GetOrCreateValue(inner);
            lock (byClass)
            {
                if (byClass.TryGetValue(ifcClass, out var hit) && hit.Revision == inner.Revision) got = (hit.Ids, hit.Types);
                else
                {
                    got = List();
                    byClass[ifcClass] = (inner.Revision, got.Ids, got.Types);
                }
            }
        }
        // A type giving extents: its own changes and who it types are read too.
        if (tv != null)
            foreach (var t in got.Types) { tv.Read.Add(t); tv.Read.Add(TrackingView.RelationsKey(t)); }
        return got.Ids;
    }
}

/// <summary>
/// What the walls' and floors' layers reaching past their edges and set to cut take out of an element of another module (a
/// column, a beam, a footing; forum topic 89, stage 4): provided by the architecture module, which builds those layers; without
/// it nothing is taken.
/// </summary>
public interface ILayerExtentCuts
{
    List<GeometryPart> Apply(Entity target, IModelView m, IServices? services, List<GeometryPart> parts, List<string> problems);
}

public static class LayerExtentCuts
{
    /// <summary>The element's parts less what the layers round reaching into it and set to cut take; the parts as they are without the provider.</summary>
    public static List<GeometryPart> Apply(Entity target, IModelView m, IServices? services, List<GeometryPart> parts, List<string> problems) =>
        services?.Get<ILayerExtentCuts>() is { } c ? c.Apply(target, m, services, parts, problems) : parts;
}
