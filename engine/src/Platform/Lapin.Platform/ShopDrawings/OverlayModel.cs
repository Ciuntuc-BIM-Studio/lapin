using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The model with a few entities added, replaced or left out, never kept: what a dialog's preview draws before its command runs
/// (a shop drawing's sheet and views, decision 581), read by the same code as the real thing.
/// </summary>
public sealed class OverlayModel(IModelView inner, IEnumerable<Entity> put, IEnumerable<string>? gone = null) : IModelView
{
    readonly Dictionary<string, Entity> over = put.ToDictionary(e => e.Id);
    readonly HashSet<string> removed = [.. gone ?? []];

    public Entity? Get(string id) => removed.Contains(id) ? null : over.TryGetValue(id, out var e) ? e : inner.Get(id);
    public Relation? GetRelation(string id) => inner.GetRelation(id);
    public IEnumerable<Entity> Entities =>
        inner.Entities.Where(e => !removed.Contains(e.Id)).Select(e => over.TryGetValue(e.Id, out var o) ? o : e)
            .Concat(over.Values.Where(e => inner.Get(e.Id) == null));
    public IEnumerable<Relation> Relations => inner.Relations;
    public IEnumerable<Entity> OfClass(string @class) =>
        inner.OfClass(@class).Where(e => !removed.Contains(e.Id)).Select(e => over.TryGetValue(e.Id, out var o) ? o : e)
            .Where(e => e.Class == @class).Concat(over.Values.Where(e => e.Class == @class && inner.Get(e.Id) == null));
    public IEnumerable<Relation> RelationsOf(string id, string? relationClass = null) => inner.RelationsOf(id, relationClass);
    public IEnumerable<Relation> RelatedBy(string id, string? relationClass = null) => inner.RelatedBy(id, relationClass);
    public int Order(string id) => over.ContainsKey(id) && inner.Get(id) == null ? int.MaxValue : inner.Order(id);
    public long Revision => inner.Revision;
}

/// <summary>A query context over an <see cref="OverlayModel"/>: the model's geometry as it is, the overlay's entities.</summary>
public sealed class OverlayQuery(IQueryContext inner, IModelView model) : IQueryContext
{
    public IModelView Model => model;
    public IServices Services => inner.Services;
    public ElementGeometry? Geometry(string entityId) => inner.Geometry(entityId);
    public ElementGeometry? FineGeometry(string entityId) => inner.FineGeometry(entityId);
    public string? Title => inner.Title;
    public IReadOnlyList<QuantitySet> Quantities(string id) => inner.Quantities(id);
    public ClassDescriptor? Describe(string ifcClass) => inner.Describe(ifcClass);
}
