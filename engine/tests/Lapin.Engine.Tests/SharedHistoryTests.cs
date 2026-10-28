using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A history several people edit (docs/plan-multiplayer.md): each undoes and
/// redoes only their own edits, as new transactions, and an undo whose edit
/// someone changed since is refused whole. Alone, undo is the stack it was
/// (every other test of undo holds that).
/// </summary>
public sealed class SharedHistoryTests
{
    const string Guest = "p1";

    static Fixture Shared(out string mine, out string theirs)
    {
        var f = new Fixture();
        mine = f.Wall(0, 0, 5, 0);
        f.Session.Author = Guest;
        theirs = f.Wall(0, 10, 5, 10);
        f.Session.Author = History.Me;
        return f;
    }

    [Fact]
    public void EachUndoesOnlyTheirOwn()
    {
        var f = Shared(out var mine, out var theirs);
        // My undo takes my wall away, though the guest's came after it.
        f.Session.Undo();
        Assert.Null(f.Model.Get(mine));
        Assert.NotNull(f.Model.Get(theirs));
        // The guest's takes theirs.
        f.Session.Undo(Guest);
        Assert.Null(f.Model.Get(theirs));
        // Each redo brings back only its author's.
        f.Session.Redo(Guest);
        Assert.NotNull(f.Model.Get(theirs));
        Assert.Null(f.Model.Get(mine));
        f.Session.Redo();
        Assert.NotNull(f.Model.Get(mine));
        // The log only grew: two edits, two undos, two redos.
        Assert.Equal(6, f.Session.History.Done.Count(t => t.Label == "Wall"));
    }

    [Fact]
    public void EachHasTheirOwnUndoState()
    {
        var f = Shared(out _, out _);
        f.Run("element.rename", new JsonObject { ["id"] = f.Storey, ["name"] = "Parter" });
        Assert.Equal("Rename", f.Session.StateFor(History.Me).UndoLabel);
        Assert.Equal("Wall", f.Session.StateFor(Guest).UndoLabel);
        f.Session.Undo(Guest);
        Assert.False(f.Session.StateFor(Guest).CanUndo);
        Assert.True(f.Session.StateFor(Guest).CanRedo);
        Assert.False(f.Session.StateFor(History.Me).CanRedo);
        // My next edit leaves the guest's redo alone.
        f.Wall(10, 0, 15, 0);
        Assert.True(f.Session.StateFor(Guest).CanRedo);
    }

    [Fact]
    public void AnUndoWhoseEditWasChangedSinceIsRefused()
    {
        var f = Shared(out var mine, out _);
        f.Session.History.AuthorName = id => id == Guest ? "Ana" : null;
        f.Session.Author = Guest;
        f.Run("element.rename", new JsonObject { ["id"] = mine, ["name"] = "Fațada" });
        f.Session.Author = History.Me;
        var ex = Assert.Throws<ModelException>(() => f.Session.Undo());
        Assert.Contains("Ana changed it after you", ex.Message);
        // Nothing half done: the wall and its name are as the guest left them.
        Assert.Equal("Fațada", f.Model.Get(mine)!.Name);
        // The guest's own undo still works, and then mine does.
        f.Session.Undo(Guest);
        f.Session.Undo();
        Assert.Null(f.Model.Get(mine));
    }

    [Fact]
    public void ARecoveredSharedHistoryUndoesAsBefore()
    {
        var f = new Fixture();
        var entities = f.Model.Entities.ToList();
        var relations = f.Model.Relations.ToList();
        var events = new List<HistoryEvent>();
        f.Session.HistoryChanged += events.Add;
        var mine = f.Wall(0, 0, 5, 0);
        f.Session.Author = Guest;
        var theirs = f.Wall(0, 10, 5, 10);
        f.Session.Undo(Guest);
        f.Session.Author = History.Me;

        var g = new Fixture();
        g.Session.Recover(entities, relations, null, null, events.Select(e => e with { Transaction = e.Transaction == null ? null : Reread(e.Transaction) }));
        Assert.NotNull(g.Model.Get(mine));
        Assert.Null(g.Model.Get(theirs));
        // The guest can redo what they undid; I can undo my wall.
        Assert.Equal("Wall", g.Session.StateFor(Guest).RedoLabel);
        g.Session.Redo(Guest);
        Assert.NotNull(g.Model.Get(theirs));
        g.Session.Undo();
        Assert.Null(g.Model.Get(mine));
    }

    /// <summary>A transaction as the journal reads it back: new objects with the same content.</summary>
    static Transaction Reread(Transaction t) => t with
    {
        Entities = t.Entities.Select(c => c with { Before = Copy(c.Before), After = Copy(c.After) }).ToList(),
        Relations = t.Relations.Select(c => c with { Before = c.Before == null ? null : c.Before with { Related = [.. c.Before.Related] }, After = c.After == null ? null : c.After with { Related = [.. c.After.Related] } }).ToList(),
    };

    static Entity? Copy(Entity? e) => e == null ? null : new Entity(e.Id, e.Class, e.Name, e.CloneAttrs(), e.PredefinedType);

    [Fact]
    public void AloneUndoIsTheStackItWas()
    {
        var f = new Fixture();
        var a = f.Wall(0, 0, 5, 0);
        var b = f.Wall(0, 10, 5, 10);
        var count = f.Session.History.Done.Count;
        f.Session.Undo();
        Assert.Null(f.Model.Get(b));
        Assert.Equal(count - 1, f.Session.History.Done.Count);
        f.Session.Redo();
        Assert.NotNull(f.Model.Get(b));
        Assert.NotNull(f.Model.Get(a));
        Assert.Equal(count, f.Session.History.Done.Count);
    }
}
