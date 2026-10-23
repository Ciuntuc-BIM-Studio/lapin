namespace Lapin.Model;

/// <summary>What a command may do to the model. Every change goes into one transaction: one undo step.</summary>
public interface IEdit
{
    IModelView Model { get; }
    /// <summary>A new stable id (an IFC GlobalId), for entities and relationships alike.</summary>
    string NewId();
    void Add(Entity entity);
    void Replace(Entity entity);
    /// <summary>
    /// Removes the entity and its part in every relationship: one it relates
    /// goes, one it is related by shrinks (and goes when empty). A layer set
    /// usage only it used goes with it: in Lapin a usage belongs to its occurrence.
    /// </summary>
    void Remove(string id);
    /// <summary>
    /// <paramref name="related"/> becomes related to <paramref name="relating"/>
    /// by a relationship of <paramref name="relationClass"/>, leaving any other
    /// of that class (an element has one storey, one type, one material
    /// association). The relating entity's existing relationship of the class
    /// grows; else a new one is made.
    /// </summary>
    Relation Relate(string relationClass, string relating, string related);
    /// <summary><paramref name="related"/> leaves the relationships of <paramref name="relationClass"/> it is related by.</summary>
    void Unrelate(string relationClass, string related);
}

/// <summary>An entity or relationship before and after a transaction (null: absent), with its place in the order.</summary>
public sealed record Change<T>(string Id, T? Before, T? After, int Order) where T : class;

/// <summary>
/// A committed command: its changes, enough to undo and redo it. The
/// sequence of these is the operation log the project journal keeps and a
/// future sync sends.
/// </summary>
public sealed record Transaction(long Seq, string Label, IReadOnlyList<Change<Entity>> Entities, IReadOnlyList<Change<Relation>> Relations, DateTimeOffset At)
{
    public int Count => Entities.Count + Relations.Count;

    /// <summary>Every id the transaction touched: its entities and relationships, and both ends of every relationship before and after.</summary>
    public IEnumerable<string> TouchedIds() =>
        Entities.Select(c => c.Id)
            .Concat(Relations.SelectMany(c => new[] { c.Id }
                .Concat(c.Before is { } b ? b.Related.Prepend(b.Relating) : [])
                .Concat(c.After is { } a ? a.Related.Prepend(a.Relating) : [])))
            .Distinct();
}

/// <summary>Applies one command's changes as it goes, recording them for undo.</summary>
sealed class TransactionBuilder(ModelStore store, Func<string> newId) : IEdit
{
    readonly Dictionary<string, Change<Entity>> _entities = [];
    readonly List<string> _entityOrder = [];
    readonly Dictionary<string, Change<Relation>> _relations = [];
    readonly List<string> _relationOrder = [];

    public IModelView Model => store;
    public string NewId() => newId();

    void Record(string id, Entity? after)
    {
        if (_entities.TryGetValue(id, out var c)) _entities[id] = c with { After = after };
        else { _entities[id] = new(id, store.Get(id), after, store.OrderOf(id) ?? -1); _entityOrder.Add(id); }
    }

    void Record(string id, Relation? after)
    {
        if (_relations.TryGetValue(id, out var c)) _relations[id] = c with { After = after };
        else { _relations[id] = new(id, store.GetRelation(id), after, store.OrderOf(id) ?? -1); _relationOrder.Add(id); }
    }

    public void Add(Entity entity)
    {
        if (store.Get(entity.Id) != null || store.GetRelation(entity.Id) != null) throw new ModelException($"There is already an entity {entity.Id}.");
        Record(entity.Id, entity);
        store.Put(entity);
        if (_entities[entity.Id].Order < 0) _entities[entity.Id] = _entities[entity.Id] with { Order = store.OrderOf(entity.Id)!.Value };
    }

    public void Replace(Entity entity)
    {
        var old = store.Get(entity.Id) ?? throw new ModelException($"There is no entity {entity.Id}.");
        if (old.Class != entity.Class) throw new ModelException("An entity cannot change its class.");
        Record(entity.Id, entity);
        store.Put(entity);
    }

    public void Remove(string id)
    {
        if (store.Get(id) == null) return;
        var usages = new List<string>();
        foreach (var r in store.RelationsOf(id).ToList())
        {
            if (r.Relating == id) { RemoveRelation(r.Id); continue; }
            var rest = r.Related.Where(x => x != id).ToList();
            if (r.Class == IfcClass.RelAssociatesMaterial && rest.Count == 0 && store.Get(r.Relating) is { Class: IfcClass.MaterialLayerSetUsage }) usages.Add(r.Relating);
            if (rest.Count == 0) RemoveRelation(r.Id);
            else PutRelation(r with { Related = rest });
        }
        Record(id, (Entity?)null);
        store.Delete(id);
        foreach (var u in usages) Remove(u);
    }

    public Relation Relate(string relationClass, string relating, string related)
    {
        if (store.Get(relating) == null) throw new ModelException($"There is no entity {relating}.");
        if (store.Get(related) == null) throw new ModelException($"There is no entity {related}.");
        if (store.RelationRelating(related, relationClass) is { } current && current.Relating == relating) return current;
        Unrelate(relationClass, related);
        var own = store.RelationsOf(relating, relationClass).FirstOrDefault(r => r.Relating == relating);
        var next = own == null ? new Relation(newId(), relationClass, relating, [related]) : own with { Related = [.. own.Related, related] };
        PutRelation(next);
        return next;
    }

    public void Unrelate(string relationClass, string related)
    {
        foreach (var r in store.RelationsOf(related, relationClass).Where(r => r.Related.Contains(related)).ToList())
        {
            var rest = r.Related.Where(x => x != related).ToList();
            if (rest.Count == 0) RemoveRelation(r.Id);
            else PutRelation(r with { Related = rest });
        }
    }

    void PutRelation(Relation r)
    {
        Record(r.Id, r);
        store.Put(r);
        if (_relations[r.Id].Order < 0) _relations[r.Id] = _relations[r.Id] with { Order = store.OrderOf(r.Id)!.Value };
    }

    void RemoveRelation(string id)
    {
        if (store.GetRelation(id) == null) return;
        Record(id, (Relation?)null);
        store.DeleteRelation(id);
    }

    public IReadOnlyList<Change<Entity>> EntityChanges =>
        _entityOrder.Select(id => _entities[id]).Where(c => !(c.Before == null && c.After == null)).ToList();

    public IReadOnlyList<Change<Relation>> RelationChanges =>
        _relationOrder.Select(id => _relations[id]).Where(c => !(c.Before == null && c.After == null)).ToList();

    /// <summary>Put the model back as it was (a command threw half way).</summary>
    public void Rollback() =>
        Replay.Revert(store, _entityOrder.Select(id => _entities[id]).ToList(), _relationOrder.Select(id => _relations[id]).ToList());
}

/// <summary>Applying and reverting recorded changes: shared by rollback, undo and redo.</summary>
static class Replay
{
    public static void Revert(ModelStore s, IReadOnlyList<Change<Entity>> entities, IReadOnlyList<Change<Relation>> relations)
    {
        // Relationships out, entities back, then relationships back: a relationship never points at a missing entity.
        foreach (var c in relations.Reverse()) if (c.Before == null) s.DeleteRelation(c.Id);
        foreach (var c in entities.Reverse()) if (c.Before == null) s.Delete(c.Id); else s.Put(c.Before, c.Order);
        foreach (var c in relations.Reverse()) if (c.Before != null) s.Put(c.Before, c.Order);
    }

    public static void Apply(ModelStore s, IReadOnlyList<Change<Entity>> entities, IReadOnlyList<Change<Relation>> relations)
    {
        foreach (var c in relations) if (c.After == null) s.DeleteRelation(c.Id);
        foreach (var c in entities) if (c.After == null) s.Delete(c.Id); else s.Put(c.After, c.Order);
        foreach (var c in relations) if (c.After != null) s.Put(c.After, c.Order);
    }
}

/// <summary>
/// The model's history: every edit is one transaction, undone and redone
/// whole. The store is changed only here.
/// </summary>
public sealed class History(ModelStore store)
{
    readonly List<Transaction> _undo = [];
    readonly List<Transaction> _redo = [];
    long _seq;
    long _savedAt;

    public ModelStore Store => store;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoLabel => _undo.Count > 0 ? _undo[^1].Label : null;
    public string? RedoLabel => _redo.Count > 0 ? _redo[^1].Label : null;
    /// <summary>The transactions since the project was opened, oldest first: what the journal writes.</summary>
    public IReadOnlyList<Transaction> Done => _undo;
    /// <summary>Changed since the last save (or open).</summary>
    public bool Dirty => (_undo.Count > 0 ? _undo[^1].Seq : 0) != _savedAt;

    /// <summary>Runs <paramref name="edit"/> as one transaction; nothing changed means no transaction (null). A throw leaves the model as it was.</summary>
    public Transaction? Execute(string label, Action<IEdit> edit)
    {
        var b = new TransactionBuilder(store, GlobalId.New);
        try { edit(b); }
        catch { b.Rollback(); throw; }
        if (b.EntityChanges.Count == 0 && b.RelationChanges.Count == 0) return null;
        var t = new Transaction(++_seq, label, b.EntityChanges, b.RelationChanges, DateTimeOffset.UtcNow);
        _undo.Add(t);
        _redo.Clear();
        return t;
    }

    public Transaction? Undo()
    {
        if (_undo.Count == 0) return null;
        var t = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Replay.Revert(store, t.Entities, t.Relations);
        _redo.Add(t);
        return t;
    }

    public Transaction? Redo()
    {
        if (_redo.Count == 0) return null;
        var t = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        Replay.Apply(store, t.Entities, t.Relations);
        _undo.Add(t);
        return t;
    }

    /// <summary>A new or opened project: an empty history, nothing to save.</summary>
    public void Reset()
    {
        _undo.Clear();
        _redo.Clear();
        _savedAt = 0;
        _seq = 0;
    }

    public void MarkSaved() => _savedAt = _undo.Count > 0 ? _undo[^1].Seq : 0;

    /// <summary>Loads entities and relationships as they were saved: no transaction, no undo step.</summary>
    public void Load(IEnumerable<Entity> entities, IEnumerable<Relation> relations)
    {
        store.Clear();
        foreach (var e in entities) store.Put(e);
        foreach (var r in relations) store.Put(r);
        Reset();
    }
}

/// <summary>An edit refused for a reason the user should read.</summary>
public class ModelException(string message) : Exception(message);
