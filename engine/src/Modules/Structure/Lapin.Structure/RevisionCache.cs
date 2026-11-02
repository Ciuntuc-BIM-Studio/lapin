using System.Runtime.CompilerServices;
using Lapin.Model;

namespace Lapin.Structure;

/// <summary>
/// A value derived from the model, kept per entity (immutable) and model
/// revision, with what was read to make it. A geometry producer sees the model
/// through a view that notes what it reads, and is rebuilt when one of those
/// changes; the first producer that misses the cache reads the model itself,
/// and every later one gets the same reads replayed into its view
/// (RoomBounds' pattern), so each depends on what the value depends on.
/// The same as Lapin.Architecture's (the two modules share only the platform).
/// </summary>
sealed class RevisionCache<T>
{
    sealed record Hit(long Revision, T Value, Reads Reads);
    readonly ConditionalWeakTable<Entity, Hit> _cache = new();

    public T Get(Entity key, IModelView m, Func<IModelView, T> compute)
    {
        if (_cache.TryGetValue(key, out var hit) && hit.Revision == m.Revision)
        {
            hit.Reads.ReplayInto(m);
            return hit.Value;
        }
        var rec = new Reads(m);
        rec.Get(key.Id);
        var value = compute(rec);
        _cache.AddOrUpdate(key, new Hit(m.Revision, value, rec));
        return value;
    }
}

/// <summary>A view that notes what is read through it, to be read again from another view.</summary>
sealed class Reads(IModelView inner) : IModelView
{
    readonly HashSet<string> _ids = [], _classes = [], _relations = [];
    bool _all;

    /// <summary>Reads again, through <paramref name="m"/>, everything read through this one.</summary>
    public void ReplayInto(IModelView m)
    {
        if (_all) _ = m.Entities;
        foreach (var id in _ids) m.Get(id);
        foreach (var c in _classes) m.OfClass(c);
        foreach (var id in _relations) m.RelationsOf(id);
    }

    public Entity? Get(string id) { _ids.Add(id); return inner.Get(id); }
    public Relation? GetRelation(string id) { _ids.Add(id); return inner.GetRelation(id); }
    public IEnumerable<Entity> Entities { get { _all = true; return inner.Entities; } }
    public IEnumerable<Relation> Relations { get { _all = true; return inner.Relations; } }
    public IEnumerable<Entity> OfClass(string @class) { _classes.Add(@class); return inner.OfClass(@class); }
    public IEnumerable<Relation> RelationsOf(string id, string? relationClass = null) { _relations.Add(id); return inner.RelationsOf(id, relationClass); }
    public IEnumerable<Relation> RelatedBy(string id, string? relationClass = null) { _relations.Add(id); return inner.RelatedBy(id, relationClass); }
    public int Order(string id) => inner.Order(id);
    public long Revision => inner.Revision;
}
