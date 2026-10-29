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
    /// goes, one it is related by shrinks (and goes when empty). What cannot
    /// be without it goes too, as IFC has it: a layer set usage only it used
    /// (in Lapin a usage belongs to its occurrence), the openings it is voided
    /// by, the door or window filling an opening, and the opening a door or
    /// window filled.
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
    /// <summary>
    /// A relationship of its own, beside any other of its class: one per pair,
    /// as IfcRelConnectsPathElements (a wall joined at both ends has two).
    /// </summary>
    void AddRelation(Relation relation);
    /// <summary>A relationship changed in place: its attributes, or which side relates (a join whose priority swapped).</summary>
    void ReplaceRelation(Relation relation);
    void RemoveRelation(string id);
}

/// <summary>An entity or relationship before and after a transaction (null: absent), with its place in the order.</summary>
public sealed record Change<T>(string Id, T? Before, T? After, int Order) where T : class;

/// <summary>
/// A committed command: its changes, enough to undo and redo it. The
/// sequence of these is the operation log the project journal keeps and a
/// shared session sends. <paramref name="Author"/>: who made it (the
/// computer's own user is <see cref="History.Me"/>, a guest of a shared
/// session its participant id); <paramref name="Reverts"/> and
/// <paramref name="Redoes"/>: the transaction it undid or did again, when it
/// is an undo or a redo made as a new transaction (a shared session's).
/// </summary>
public sealed record Transaction(long Seq, string Label, IReadOnlyList<Change<Entity>> Entities, IReadOnlyList<Change<Relation>> Relations, DateTimeOffset At,
    string Author = History.Me, long? Reverts = null, long? Redoes = null)
{
    public int Count => Entities.Count + Relations.Count;

    /// <summary>
    /// Every id the transaction touched: its entities and relationships, and
    /// whom each relationship change concerns. A new or removed relationship,
    /// or one whose relating entity changed, concerns everyone in it; one that
    /// only gained or lost members concerns its relating entity and those
    /// members, not the others (a storey's thousandth wall leaves the 999
    /// before it alone).
    /// </summary>
    public IEnumerable<string> TouchedIds() =>
        Entities.Select(c => c.Id).Concat(Relations.SelectMany(Concerned)).Distinct();

    public static IEnumerable<string> Concerned(Change<Relation> c)
    {
        yield return c.Id;
        if (c.Before is not { } b || c.After is not { } a || b.Relating != a.Relating || b.Class != a.Class)
        {
            foreach (var r in new[] { c.Before, c.After })
                if (r != null) { yield return r.Relating; foreach (var x in r.Related) yield return x; }
            yield break;
        }
        yield return a.Relating;
        var before = b.Related.ToHashSet();
        var after = a.Related.ToHashSet();
        foreach (var x in after) if (!before.Contains(x)) yield return x;
        foreach (var x in before) if (!after.Contains(x)) yield return x;
    }
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
            if (r.Class is IfcClass.RelVoidsElement or IfcClass.RelFillsElement && r.Relating == id) usages.AddRange(r.Related);
            if (r.Class == IfcClass.RelFillsElement && r.Related.Contains(id)) usages.Add(r.Relating);
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
        foreach (var r in store.RelatedBy(related, relationClass).ToList())
        {
            var rest = r.Related.Where(x => x != related).ToList();
            if (rest.Count == 0) RemoveRelation(r.Id);
            else PutRelation(r with { Related = rest });
        }
    }

    public void AddRelation(Relation relation)
    {
        if (store.GetRelation(relation.Id) != null || store.Get(relation.Id) != null) throw new ModelException($"There is already a relationship {relation.Id}.");
        foreach (var id in relation.Related.Prepend(relation.Relating))
            if (store.Get(id) == null) throw new ModelException($"There is no entity {id}.");
        PutRelation(relation);
    }

    public void ReplaceRelation(Relation relation)
    {
        var old = store.GetRelation(relation.Id) ?? throw new ModelException($"There is no relationship {relation.Id}.");
        if (old.Class != relation.Class) throw new ModelException("A relationship cannot change its class.");
        foreach (var id in relation.Related.Prepend(relation.Relating))
            if (store.Get(id) == null) throw new ModelException($"There is no entity {id}.");
        PutRelation(relation);
    }

    void PutRelation(Relation r)
    {
        Record(r.Id, r);
        store.Put(r);
        if (_relations[r.Id].Order < 0) _relations[r.Id] = _relations[r.Id] with { Order = store.OrderOf(r.Id)!.Value };
    }

    public void RemoveRelation(string id)
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
        Replaying.Revert(store, _entityOrder.Select(id => _entities[id]).ToList(), _relationOrder.Select(id => _relations[id]).ToList());
}

/// <summary>Applying and reverting recorded changes: shared by rollback, undo and redo.</summary>
static class Replaying
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
/// <para>
/// Each transaction has its author. While every transaction is one author's
/// (a project worked on alone), undo takes the last one off and redo puts it
/// back, as always. Once several have edited (a shared session,
/// docs/plan-multiplayer.md), each undoes only their own: the last of theirs
/// is put back as it was by a new transaction, provided what it changed is
/// still as they left it; if someone changed it since, the undo is refused
/// rather than half done. Redo likewise. The log only grows.
/// </para>
/// </summary>
public sealed class History(ModelStore store)
{
    /// <summary>The computer's own user: the author of every edit made on it.</summary>
    public const string Me = "me";

    readonly List<Transaction> _undo = [];
    readonly List<Transaction> _redo = [];
    /// <summary>In a shared history, the transactions undone by a later one (their <see cref="Transaction.Reverts"/>).</summary>
    readonly HashSet<long> _undone = [];
    long _seq;
    long _savedAt;

    public ModelStore Store => store;
    public bool CanUndo => CanUndoOf(Me);
    public bool CanRedo => CanRedoOf(Me);
    public string? UndoLabel => UndoLabelOf(Me);
    public string? RedoLabel => RedoLabelOf(Me);

    /// <summary>Only <paramref name="author"/> has edited: undo and redo work as a stack.</summary>
    bool Solo(string author) => _undo.All(t => t.Author == author) && _redo.All(t => t.Author == author);

    /// <summary>The transaction <paramref name="author"/>'s undo would undo.</summary>
    Transaction? LastOf(string author)
    {
        if (Solo(author)) return _undo.Count > 0 ? _undo[^1] : null;
        for (var i = _undo.Count - 1; i >= 0; i--)
            if (_undo[i] is { } t && t.Author == author && t.Reverts == null && !_undone.Contains(t.Seq)) return t;
        return null;
    }

    Transaction? NextOf(string author)
    {
        for (var i = _redo.Count - 1; i >= 0; i--) if (_redo[i].Author == author) return _redo[i];
        return null;
    }

    public bool CanUndoOf(string author) => LastOf(author) != null;
    public bool CanRedoOf(string author) => NextOf(author) != null;
    public string? UndoLabelOf(string author) => LastOf(author)?.Label;
    public string? RedoLabelOf(string author) => NextOf(author)?.Label;
    /// <summary>The transactions since the project was opened, oldest first: what the journal writes.</summary>
    public IReadOnlyList<Transaction> Done => _undo;
    /// <summary>Changed since the last save (or open).</summary>
    public bool Dirty => (_undo.Count > 0 ? _undo[^1].Seq : 0) != _savedAt;

    /// <summary>Runs <paramref name="edit"/> as one transaction; nothing changed means no transaction (null). A throw leaves the model as it was.</summary>
    public Transaction? Execute(string label, Action<IEdit> edit, string author = Me)
    {
        var b = new TransactionBuilder(store, GlobalId.New);
        try { edit(b); }
        catch { b.Rollback(); throw; }
        if (b.EntityChanges.Count == 0 && b.RelationChanges.Count == 0) return null;
        var t = new Transaction(++_seq, label, b.EntityChanges, b.RelationChanges, DateTimeOffset.UtcNow, author);
        _undo.Add(t);
        // A new edit ends what its author could redo (and no one else's).
        _redo.RemoveAll(x => x.Author == author);
        return t;
    }

    /// <summary>
    /// <paramref name="author"/>'s last edit undone. Alone: taken off the
    /// history (the transaction returned is the one undone). Shared: a new
    /// transaction putting back what it changed (the one returned), refused
    /// when someone changed any of it since.
    /// </summary>
    public Transaction? Undo(string author = Me)
    {
        if (Solo(author))
        {
            if (_undo.Count == 0) return null;
            var t = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
            Replaying.Revert(store, t.Entities, t.Relations);
            _redo.Add(t);
            return t;
        }
        if (LastOf(author) is not { } last) return null;
        var undo = Compensate(last, back: true);
        _undone.Add(last.Seq);
        _redo.Add(last);
        return undo;
    }

    /// <summary><paramref name="author"/>'s last undo done again: alone, put back on the history; shared, as a new transaction (refused, as undo, when what it changes has changed since).</summary>
    public Transaction? Redo(string author = Me)
    {
        if (Solo(author))
        {
            if (_redo.Count == 0) return null;
            var t = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
            Replaying.Apply(store, t.Entities, t.Relations);
            _undo.Add(t);
            return t;
        }
        if (NextOf(author) is not { } next) return null;
        var redo = Compensate(next, back: false);
        _redo.Remove(next);
        return redo;
    }

    /// <summary>
    /// <paramref name="t"/>'s changes put back (<paramref name="back"/>) or done
    /// again, as a new transaction of its author, provided every entity and
    /// relationship it touched is still as it left it (or as it found it).
    /// </summary>
    Transaction Compensate(Transaction t, bool back)
    {
        foreach (var c in t.Entities)
            if (!Same(store.Get(c.Id), back ? c.After : c.Before)) throw new ModelException(Conflict(t, c.Id, back));
        var entities = t.Entities.Select(c => back ? new Change<Entity>(c.Id, c.After, c.Before, c.Order) : c).ToList();
        var relations = new List<Change<Relation>>();
        foreach (var c in t.Relations)
        {
            var (from, to) = back ? (c.After, c.Before) : (c.Before, c.After);
            var now = store.GetRelation(c.Id);
            var made = Members(from, to, now) ?? (Same(now, from) ? to : throw new ModelException(Conflict(t, c.Id, back)));
            relations.Add(new(c.Id, now, ReferenceEquals(made, Gone) ? null : made, c.Order));
        }
        // Undoing runs the changes the other way round, as Revert does: relationships never point at a missing entity.
        if (back) { entities.Reverse(); relations.Reverse(); }
        Replaying.Apply(store, entities, relations);
        var done = new Transaction(++_seq, t.Label, entities, relations, DateTimeOffset.UtcNow, t.Author, back ? t.Seq : null, back ? null : t.Seq);
        _undo.Add(done);
        return done;
    }

    /// <summary>
    /// A relationship whose change was only of its members (a storey's
    /// containment gaining a wall): that change made again, from
    /// <paramref name="from"/> to <paramref name="to"/>, on its members as they
    /// are <paramref name="now"/>, so the others' members stay (a guest's wall
    /// on the same storey). Null when the change was more than its members, or
    /// the relationship is no longer the one it was; then it must be as left.
    /// </summary>
    static Relation? Members(Relation? from, Relation? to, Relation? now)
    {
        var shape = to ?? from;
        if (shape == null) return null;
        bool Like(Relation? r) => r == null || r.Class == shape.Class && r.Relating == shape.Relating && System.Text.Json.Nodes.JsonNode.DeepEquals(r.Attrs, shape.Attrs);
        if (!Like(from) || !Like(to) || !Like(now)) return null;
        var before = from?.Related ?? [];
        var after = to?.Related ?? [];
        var members = (now?.Related ?? []).Where(x => !before.Contains(x) || after.Contains(x)).ToList();
        foreach (var x in after) if (!before.Contains(x) && !members.Contains(x)) members.Add(x);
        // A relationship left without members goes, as when its last member is removed.
        return members.Count == 0 ? Gone : (now ?? shape) with { Related = members };
    }

    /// <summary>What <see cref="Members"/> answers for a relationship left empty: it is removed.</summary>
    static readonly Relation Gone = new("", "", "", []);

    /// <summary>The same state: the same object, or equal field by field (a recovered history reads its transactions anew).</summary>
    static bool Same(Entity? a, Entity? b) =>
        ReferenceEquals(a, b) || a != null && b != null && a.Id == b.Id && a.Class == b.Class && a.Name == b.Name && a.PredefinedType == b.PredefinedType
            && System.Text.Json.Nodes.JsonNode.DeepEquals(a.Attrs, b.Attrs);

    static bool Same(Relation? a, Relation? b) =>
        ReferenceEquals(a, b) || a != null && b != null && a.Id == b.Id && a.Class == b.Class && a.Relating == b.Relating && a.Related.SequenceEqual(b.Related)
            && System.Text.Json.Nodes.JsonNode.DeepEquals(a.Attrs, b.Attrs);

    /// <summary>Who changed <paramref name="id"/> after <paramref name="t"/>, in words.</summary>
    string Conflict(Transaction t, string id, bool back)
    {
        var by = _undo.LastOrDefault(x => x.Seq > t.Seq && x.TouchedIds().Contains(id))?.Author;
        var who = by == null || by == t.Author ? "Someone" : AuthorName?.Invoke(by) ?? by;
        return $"{(back ? "Undo" : "Redo")} „{t.Label}”: {who} changed it after you; it is left as it is.";
    }

    /// <summary>An author's name for the messages (a shared session's participants); their id when null.</summary>
    public Func<string, string?>? AuthorName { get; set; }

    /// <summary>A new or opened project: an empty history, nothing to save.</summary>
    public void Reset()
    {
        _undo.Clear();
        _redo.Clear();
        _undone.Clear();
        _replayed.Clear();
        _savedAt = 0;
        _seq = 0;
    }

    /// <summary>The sequence numbers of the transactions replayed, as they were recorded: an undo recorded as a transaction names what it reverts by them.</summary>
    readonly Dictionary<long, long> _replayed = [];

    /// <summary>A transaction recorded earlier (the recovery journal), done again: applied, and the top of the undo stack.</summary>
    public Transaction Replay(Transaction recorded)
    {
        Replaying.Apply(store, recorded.Entities, recorded.Relations);
        long? Mapped(long? seq) => seq is { } x ? _replayed.GetValueOrDefault(x, x) : null;
        var t = recorded with { Seq = ++_seq, Reverts = Mapped(recorded.Reverts), Redoes = Mapped(recorded.Redoes) };
        if (recorded.Seq > 0) _replayed[recorded.Seq] = t.Seq;
        if (t.Reverts is { } undone)
        {
            _undone.Add(undone);
            if (_undo.FirstOrDefault(x => x.Seq == undone) is { } original) _redo.Add(original);
        }
        else if (t.Redoes is { } redone) _redo.RemoveAll(x => x.Seq == redone);
        else _redo.RemoveAll(x => x.Author == t.Author);
        _undo.Add(t);
        return t;
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
