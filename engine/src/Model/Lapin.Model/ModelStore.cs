namespace Lapin.Model;

/// <summary>Read access to the model.</summary>
public interface IModelView
{
    Entity? Get(string id);
    Relation? GetRelation(string id);
    /// <summary>Every entity, in creation order.</summary>
    IEnumerable<Entity> Entities { get; }
    IEnumerable<Relation> Relations { get; }
    IEnumerable<Entity> OfClass(string @class);
    /// <summary>The relationships <paramref name="id"/> takes part in, relating or related, in creation order.</summary>
    IEnumerable<Relation> RelationsOf(string id, string? relationClass = null);
    /// <summary>The relationships <paramref name="id"/> is related by (among their related, not relating), in creation order: an element's storey's, its type's.</summary>
    IEnumerable<Relation> RelatedBy(string id, string? relationClass = null) => RelationsOf(id, relationClass).Where(r => r.Related.Contains(id));
    /// <summary>Position in the document: creation order, where "older" decides.</summary>
    int Order(string id);
    /// <summary>Changes with every edit: what a derived value is cached against.</summary>
    long Revision { get; }
}

public static class ModelViewExtensions
{
    /// <summary>The entity relating <paramref name="id"/> through a relationship of <paramref name="relationClass"/>: a wall's storey, its type.</summary>
    public static Entity? RelatingOf(this IModelView m, string id, string relationClass) =>
        m.RelatedBy(id, relationClass).Select(r => m.Get(r.Relating)).FirstOrDefault(e => e != null);

    /// <summary>The entities <paramref name="id"/> relates through relationships of <paramref name="relationClass"/>, in order: a storey's elements.</summary>
    public static IEnumerable<Entity> RelatedOf(this IModelView m, string id, string relationClass) =>
        m.RelationsOf(id, relationClass).Where(r => r.Relating == id).SelectMany(r => r.Related).Select(m.Get).OfType<Entity>();

    /// <summary>The relationship of <paramref name="relationClass"/> that <paramref name="id"/> is related by, if any.</summary>
    public static Relation? RelationRelating(this IModelView m, string id, string relationClass) =>
        m.RelatedBy(id, relationClass).FirstOrDefault();
}

/// <summary>
/// The model in memory: entities and relationships by id, in creation order,
/// with an index of the relationships each entity takes part in. Changed only
/// through a <see cref="History"/>, which records every change for undo.
/// </summary>
public sealed class ModelStore : IModelView
{
    readonly Dictionary<string, Entity> _entities = [];
    readonly Dictionary<string, Relation> _relations = [];
    readonly Dictionary<string, int> _order = [];
    readonly Dictionary<string, HashSet<string>> _byClass = [];
    readonly Dictionary<string, HashSet<string>> _touching = [];
    // Each id's relationships where it is among the related: a storey's containment holds every element on it, too long to search for each one.
    readonly Dictionary<string, HashSet<string>> _relatedIn = [];
    int _next;

    public long Revision { get; private set; }

    public Entity? Get(string id) => _entities.GetValueOrDefault(id);
    public Relation? GetRelation(string id) => _relations.GetValueOrDefault(id);
    public IEnumerable<Entity> Entities => _entities.Values.OrderBy(e => _order[e.Id]);
    public IEnumerable<Relation> Relations => _relations.Values.OrderBy(r => _order[r.Id]);
    public int EntityCount => _entities.Count;
    public int RelationCount => _relations.Count;

    public IEnumerable<Entity> OfClass(string @class) =>
        _byClass.TryGetValue(@class, out var ids) ? ids.Select(id => _entities[id]).OrderBy(e => _order[e.Id]) : [];

    public IEnumerable<Relation> RelationsOf(string id, string? relationClass = null) =>
        _touching.TryGetValue(id, out var ids)
            ? ids.Select(r => _relations[r]).Where(r => relationClass == null || r.Class == relationClass).OrderBy(r => _order[r.Id])
            : [];

    public IEnumerable<Relation> RelatedBy(string id, string? relationClass = null) =>
        _relatedIn.TryGetValue(id, out var ids)
            ? ids.Select(r => _relations[r]).Where(r => relationClass == null || r.Class == relationClass).OrderBy(r => _order[r.Id])
            : [];

    public int Order(string id) => _order.GetValueOrDefault(id, int.MaxValue);
    internal int? OrderOf(string id) => _order.TryGetValue(id, out var o) ? o : null;

    internal void Put(Entity e, int? order = null)
    {
        if (_entities.TryGetValue(e.Id, out var old)) _byClass[old.Class].Remove(e.Id);
        _entities[e.Id] = e;
        Place(e.Id, order);
        (_byClass.TryGetValue(e.Class, out var set) ? set : _byClass[e.Class] = []).Add(e.Id);
        Revision++;
    }

    internal void Delete(string id)
    {
        if (!_entities.Remove(id, out var e)) return;
        _byClass[e.Class].Remove(id);
        _order.Remove(id);
        Revision++;
    }

    internal void Put(Relation r, int? order = null)
    {
        if (_relations.TryGetValue(r.Id, out var old)) Unindex(old);
        _relations[r.Id] = r;
        Place(r.Id, order);
        foreach (var id in r.Related.Prepend(r.Relating))
            (_touching.TryGetValue(id, out var set) ? set : _touching[id] = []).Add(r.Id);
        foreach (var id in r.Related)
            (_relatedIn.TryGetValue(id, out var set) ? set : _relatedIn[id] = []).Add(r.Id);
        Revision++;
    }

    internal void DeleteRelation(string id)
    {
        if (!_relations.Remove(id, out var r)) return;
        Unindex(r);
        _order.Remove(id);
        Revision++;
    }

    void Unindex(Relation r)
    {
        foreach (var id in r.Related.Prepend(r.Relating))
            if (_touching.TryGetValue(id, out var set)) { set.Remove(r.Id); if (set.Count == 0) _touching.Remove(id); }
        foreach (var id in r.Related)
            if (_relatedIn.TryGetValue(id, out var set)) { set.Remove(r.Id); if (set.Count == 0) _relatedIn.Remove(id); }
    }

    void Place(string id, int? order)
    {
        if (order is { } o) { _order[id] = o; _next = Math.Max(_next, o + 1); }
        else if (!_order.ContainsKey(id)) _order[id] = _next++;
    }

    internal void Clear()
    {
        _entities.Clear(); _relations.Clear(); _order.Clear(); _byClass.Clear(); _touching.Clear(); _relatedIn.Clear();
        _next = 0;
        Revision++;
    }
}
