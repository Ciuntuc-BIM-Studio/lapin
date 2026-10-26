using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Package;

/// <summary>An entity in one model and not the other, or in both and different (<see cref="Fields"/>: what differs).</summary>
public sealed record EntityChange(string Id, string Class, string Name, IReadOnlyList<string> Fields);

/// <summary>Two models compared element by element, by GlobalId.</summary>
public sealed record ModelDiff(
    IReadOnlyList<EntityChange> Added, IReadOnlyList<EntityChange> Removed, IReadOnlyList<EntityChange> Changed,
    int RelationsAdded, int RelationsRemoved, int RelationsChanged)
{
    public bool Any => Added.Count + Removed.Count + Changed.Count + RelationsAdded + RelationsRemoved + RelationsChanged > 0;
}

/// <summary>Two manifests compared part by part: which parts came, went or changed, and how many per area (a module; "project" for the document).</summary>
public sealed record PartsDiff(IReadOnlyList<string> Added, IReadOnlyList<string> Removed, IReadOnlyList<string> Changed, int Same, IReadOnlyDictionary<string, int> Areas);

/// <summary>
/// Models compared and brought to one another: what a version's comparison
/// shows, and what restoring a version does, in one undo step, through the
/// edit (no reload: the history stays).
/// </summary>
public static class ModelCompare
{
    public static bool Same(Entity a, Entity b) =>
        a.Class == b.Class && a.Name == b.Name && a.PredefinedType == b.PredefinedType && JsonNode.DeepEquals(a.Attrs, b.Attrs);

    public static bool Same(Relation a, Relation b) =>
        a.Class == b.Class && a.Relating == b.Relating && a.Related.SequenceEqual(b.Related) && JsonNode.DeepEquals(a.Attrs, b.Attrs);

    /// <summary>What differs between two states of one entity: "name", "predefinedType", "class", and each attribute that differs.</summary>
    public static IReadOnlyList<string> Fields(Entity a, Entity b)
    {
        var fields = new List<string>();
        if (a.Class != b.Class) fields.Add("class");
        if (a.Name != b.Name) fields.Add("name");
        if (a.PredefinedType != b.PredefinedType) fields.Add("predefinedType");
        foreach (var key in a.Attrs.Select(k => k.Key).Union(b.Attrs.Select(k => k.Key)).Order(StringComparer.Ordinal))
            if (!JsonNode.DeepEquals(a.Attrs[key], b.Attrs[key])) fields.Add(key);
        return fields;
    }

    /// <summary>From <paramref name="a"/> to <paramref name="b"/>: what b added, removed and changed.</summary>
    public static ModelDiff Compare(IEnumerable<Entity> aEntities, IEnumerable<Relation> aRelations, IEnumerable<Entity> bEntities, IEnumerable<Relation> bRelations)
    {
        var a = aEntities.ToDictionary(e => e.Id);
        var b = bEntities.ToDictionary(e => e.Id);
        var added = new List<EntityChange>();
        var removed = new List<EntityChange>();
        var changed = new List<EntityChange>();
        foreach (var e in b.Values)
        {
            if (!a.TryGetValue(e.Id, out var was)) added.Add(new EntityChange(e.Id, e.Class, e.Name, []));
            else if (!Same(was, e)) changed.Add(new EntityChange(e.Id, e.Class, e.Name, Fields(was, e)));
        }
        foreach (var e in a.Values) if (!b.ContainsKey(e.Id)) removed.Add(new EntityChange(e.Id, e.Class, e.Name, []));

        var ra = aRelations.ToDictionary(r => r.Id);
        var rb = bRelations.ToDictionary(r => r.Id);
        var rAdded = rb.Keys.Count(id => !ra.ContainsKey(id));
        var rRemoved = ra.Keys.Count(id => !rb.ContainsKey(id));
        var rChanged = rb.Values.Count(r => ra.TryGetValue(r.Id, out var was) && !Same(was, r));
        return new ModelDiff(added, removed, changed, rAdded, rRemoved, rChanged);
    }

    /// <summary>Two manifests part by part: the same path with another hash is a changed part.</summary>
    public static PartsDiff Parts(PackageManifest a, PackageManifest b)
    {
        var pa = a.Parts.ToDictionary(p => p.Path);
        var pb = b.Parts.ToDictionary(p => p.Path);
        var added = pb.Keys.Where(p => !pa.ContainsKey(p)).Order(StringComparer.Ordinal).ToList();
        var removed = pa.Keys.Where(p => !pb.ContainsKey(p)).Order(StringComparer.Ordinal).ToList();
        var changed = pb.Where(kv => pa.TryGetValue(kv.Key, out var was) && was.Sha256 != kv.Value.Sha256).Select(kv => kv.Key).Order(StringComparer.Ordinal).ToList();
        var same = pb.Count(kv => pa.TryGetValue(kv.Key, out var was) && was.Sha256 == kv.Value.Sha256);
        var areas = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var path in added.Concat(removed).Concat(changed))
        {
            var area = (pb.GetValueOrDefault(path) ?? pa[path]).Module ?? (path.StartsWith("project/", StringComparison.Ordinal) ? "project" : "model");
            areas[area] = areas.GetValueOrDefault(area) + 1;
        }
        return new PartsDiff(added, removed, changed, same, areas);
    }

    /// <summary>
    /// The model brought to <paramref name="entities"/> and <paramref name="relations"/>
    /// through the edit: entities added, replaced and removed by id, then the
    /// relationships. What a removal took with it and the target still has
    /// comes back; relationships are settled last, once every entity is there.
    /// </summary>
    public static void Apply(IEdit edit, IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
    {
        var target = entities.ToDictionary(e => e.Id);
        foreach (var e in entities)
        {
            if (edit.Model.Get(e.Id) is not { } now) edit.Add(e);
            else if (now.Class != e.Class) { edit.Remove(e.Id); edit.Add(e); }
            else if (!Same(now, e)) edit.Replace(e);
        }
        foreach (var e in edit.Model.Entities.ToList())
            if (!target.ContainsKey(e.Id) && edit.Model.Get(e.Id) != null) edit.Remove(e.Id);
        foreach (var e in entities)
            if (edit.Model.Get(e.Id) == null) edit.Add(e);

        var wanted = relations.ToDictionary(r => r.Id);
        foreach (var r in edit.Model.Relations.ToList())
            if (!wanted.ContainsKey(r.Id) && edit.Model.GetRelation(r.Id) != null) edit.RemoveRelation(r.Id);
        foreach (var r in relations)
        {
            if (edit.Model.GetRelation(r.Id) is not { } now) edit.AddRelation(r);
            else if (now.Class != r.Class) { edit.RemoveRelation(r.Id); edit.AddRelation(r); }
            else if (!Same(now, r)) edit.ReplaceRelation(r);
        }
    }
}
