using Lapin.Model;

namespace Lapin.Platform;

/// <summary>A row of a module's comparison: what it is (its id, when the project has it, and name), what changed, from and to (as shown), by how many days.</summary>
public sealed record VersionDiffRow(string? Id, string Name, string What, string? From = null, string? To = null, int? Days = null);

/// <summary>
/// What a module says changed between two versions beside the elements (docs/versions.md; the work schedule's dates,
/// docs/plan-4d5d.md, stage 5): a title, a few facts (a label, from, to) and the rows.
/// </summary>
public sealed record VersionDiffSection(string Key, string Title, IReadOnlyList<(string Label, string? From, string? To)> Facts, IReadOnlyList<VersionDiffRow> Rows);

/// <summary>A module's own comparison of two states of the project (Manage → Versions → Compare); null when it has nothing to say.</summary>
public interface IVersionCompare
{
    VersionDiffSection? Compare(IModelView a, IModelView b);
}

/// <summary>A state of the model held as lists (a version read from the file), read like the model.</summary>
public sealed class ModelSnapshot : IModelView
{
    readonly Dictionary<string, Entity> _entities = [];
    readonly Dictionary<string, Relation> _relations = [];
    readonly Dictionary<string, int> _order = [];
    readonly Dictionary<string, List<Entity>> _byClass = [];
    readonly Dictionary<string, List<Relation>> _touching = [];
    readonly List<Entity> _list;
    readonly List<Relation> _rels;

    public ModelSnapshot(IEnumerable<Entity> entities, IEnumerable<Relation> relations)
    {
        _list = [.. entities];
        _rels = [.. relations];
        var i = 0;
        foreach (var e in _list)
        {
            _entities[e.Id] = e;
            _order[e.Id] = i++;
            (_byClass.TryGetValue(e.Class, out var l) ? l : _byClass[e.Class] = []).Add(e);
        }
        foreach (var r in _rels)
        {
            _relations[r.Id] = r;
            _order[r.Id] = i++;
            foreach (var id in r.Related.Prepend(r.Relating).Distinct())
                (_touching.TryGetValue(id, out var l) ? l : _touching[id] = []).Add(r);
        }
    }

    public Entity? Get(string id) => _entities.GetValueOrDefault(id);
    public Relation? GetRelation(string id) => _relations.GetValueOrDefault(id);
    public IEnumerable<Entity> Entities => _list;
    public IEnumerable<Relation> Relations => _rels;
    public IEnumerable<Entity> OfClass(string @class) => _byClass.TryGetValue(@class, out var l) ? l : [];
    public IEnumerable<Relation> RelationsOf(string id, string? relationClass = null) =>
        _touching.TryGetValue(id, out var l) ? l.Where(r => relationClass == null || r.Class == relationClass) : [];
    public int Order(string id) => _order.GetValueOrDefault(id, int.MaxValue);
    public long Revision => 0;
}
