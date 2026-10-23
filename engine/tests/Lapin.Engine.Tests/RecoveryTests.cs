using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The recovery journal: a session that ends without saving loses nothing.</summary>
public sealed class RecoveryTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-recovery-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static void Same(IModelView a, IModelView b)
    {
        Assert.Equal(a.Entities.Select(e => e.Id), b.Entities.Select(e => e.Id));
        foreach (var e in a.Entities) Assert.True(JsonNode.DeepEquals(e.Attrs, b.Get(e.Id)!.Attrs), e.Class);
        Assert.Equal(a.Relations.Select(r => (r.Id, r.Relating, string.Join(",", r.Related))), b.Relations.Select(r => (r.Id, r.Relating, string.Join(",", r.Related))));
    }

    [Fact]
    public void A_new_projects_unsaved_work_comes_back_with_its_undo_history()
    {
        var f = new Fixture();
        var journal = new Recovery(_dir, f.Session);   // never disposed: the session "crashed"
        var a = f.Wall(0, 0, 5, 0);
        f.Wall(5, 0, 5, 4);
        f.Run("architecture.flipWall", new JsonObject { ["id"] = a });
        f.Session.Undo();          // the flip undone…
        f.Session.Redo();          // …and done again
        f.Wall(0, 4, 5, 4);
        f.Session.Undo();          // the last wall undone: not in the model, but redoable

        var g = new Fixture();
        using var next = new Recovery(_dir, g.Session);
        var work = Assert.Single(next.List());
        Assert.Null(work.Path);
        Assert.Equal(7, work.Edits);   // 3 walls, a flip, an undo, a redo, an undo
        Assert.Equal("Wall", work.LastEdit);
        next.Restore(work.File);

        Same(f.Model, g.Model);
        Assert.True(g.Session.History.Dirty);
        Assert.Equal(f.Usage(a), g.Usage(a));
        Assert.True(g.Session.History.CanRedo);
        g.Session.Redo();
        Assert.Equal(3, g.Model.OfClass(IfcClass.Wall).Count());
        GC.KeepAlive(journal);
    }

    [Fact]
    public void Work_on_a_saved_project_comes_back_on_top_of_its_file_and_not_on_a_changed_one()
    {
        var path = Path.Combine(_dir, "house.lapin");
        var f = new Fixture();
        var journal = new Recovery(_dir, f.Session);
        f.Wall(0, 0, 5, 0);
        PackageFiles.Create().Save(f.Session, path);   // saving starts the journal again
        Assert.Single(Directory.GetFiles(Path.Combine(_dir, "recovery")));
        f.Wall(0, 3, 5, 3);

        var g = new Fixture();
        using var next = new Recovery(_dir, g.Session);
        var work = Assert.Single(next.List());
        Assert.Equal(path, work.Path);
        Assert.Equal(1, work.Edits);
        next.Restore(work.File);
        Same(f.Model, g.Model);
        Assert.Equal(path, g.Session.Path);

        // The same work on a file saved since by someone else: refused.
        var h = new Fixture();
        using var other = new Recovery(_dir, h.Session);
        var stale = Assert.Single(other.List());
        h.Wall(9, 9, 10, 9);
        PackageFiles.Create().Save(h.Session, path);
        Assert.Throws<CommandException>(() => other.Restore(stale.File));
        GC.KeepAlive(journal);
    }

    [Fact]
    public void A_line_cut_off_by_the_crash_is_left_out()
    {
        var f = new Fixture();
        var journal = new Recovery(_dir, f.Session);
        f.Wall(0, 0, 5, 0);
        f.Wall(0, 2, 5, 2);
        journal.Dispose();   // (keeps the file: there are unsaved edits)
        var file = Directory.GetFiles(Path.Combine(_dir, "recovery")).Single();
        File.AppendAllText(file, """{"op":"tx","label":"Wall","entit""");

        var g = new Fixture();
        using var next = new Recovery(_dir, g.Session);
        next.Restore(Assert.Single(next.List()).File);
        Assert.Equal(2, g.Model.OfClass(IfcClass.Wall).Count());
    }

    [Fact]
    public void A_clean_ending_or_a_journal_without_edits_leaves_nothing_behind()
    {
        var f = new Fixture();
        using (new Recovery(_dir, f.Session)) { }
        Assert.Empty(Directory.GetFiles(Path.Combine(_dir, "recovery")));

        var g = new Fixture();
        var crashed = new Recovery(_dir, g.Session);   // started, no edit, then "crashed"
        var h = new Fixture();
        using var next = new Recovery(_dir, h.Session);
        Assert.Empty(next.List());
        Assert.Single(Directory.GetFiles(Path.Combine(_dir, "recovery")));   // only the open session's own
        GC.KeepAlive(crashed);
    }
}
