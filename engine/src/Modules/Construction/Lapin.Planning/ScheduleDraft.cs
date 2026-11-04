using Lapin.Model;

namespace Lapin.Planning;

/// <summary>
/// The model as it would be with another schedule (the generator's preview, docs/plan-4d5d.md, stage 3): the project's
/// elements as they are, its work schedule, tasks and links those given. Read like any model, so the critical path and
/// the tasks' elements are worked out on the draft exactly as they will be once it is applied.
/// </summary>
public sealed class ScheduleDraft : IModelView
{
    static readonly HashSet<string> Drafted = [Plan.ScheduleClass, Plan.TaskClass];

    readonly IModelView model;
    readonly Dictionary<string, Entity> entities = [];
    readonly Dictionary<string, Relation> relations = [];
    readonly List<Entity> order = [];

    public ScheduleDraft(IModelView model, IEnumerable<Entity> entities, IEnumerable<Relation> links)
    {
        this.model = model;
        foreach (var e in entities) { this.entities[e.Id] = e; order.Add(e); }
        foreach (var r in links) relations[r.Id] = r;
    }

    public Entity? Get(string id) => entities.TryGetValue(id, out var e) ? e : model.Get(id) is { } m && !Drafted.Contains(m.Class) ? m : null;

    public Relation? GetRelation(string id) => relations.TryGetValue(id, out var r) ? r : model.GetRelation(id) is { } m && m.Class != Plan.SequenceClass ? m : null;

    public IEnumerable<Entity> Entities => model.Entities.Where(e => !Drafted.Contains(e.Class)).Concat(order);

    public IEnumerable<Relation> Relations => model.Relations.Where(r => r.Class != Plan.SequenceClass).Concat(relations.Values);

    public IEnumerable<Entity> OfClass(string @class) => Drafted.Contains(@class) ? order.Where(e => e.Class == @class) : model.OfClass(@class);

    public IEnumerable<Relation> RelationsOf(string id, string? relationClass = null)
    {
        if (relationClass == Plan.SequenceClass) return relations.Values.Where(r => r.Touches(id));
        var own = model.RelationsOf(id, relationClass).Where(r => r.Class != Plan.SequenceClass);
        return relationClass == null ? own.Concat(relations.Values.Where(r => r.Touches(id))) : own;
    }

    public int Order(string id) => entities.ContainsKey(id) ? int.MaxValue / 2 + order.FindIndex(e => e.Id == id) : model.Order(id);

    public long Revision => model.Revision;
}
