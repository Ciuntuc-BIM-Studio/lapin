using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Protocol;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The recovery journal: a session that ends without saving loses nothing.</summary>
public sealed class RecoveryTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-recovery-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    string[] Journals => Directory.GetFiles(Path.Combine(_dir, "recovery"));

    static void Same(IModelView a, IModelView b)
    {
        Assert.Equal(a.Entities.Select(e => e.Id), b.Entities.Select(e => e.Id));
        foreach (var e in a.Entities) Assert.True(JsonNode.DeepEquals(e.Attrs, b.Get(e.Id)!.Attrs), e.Class);
        Assert.Equal(a.Relations.Select(r => (r.Id, r.Relating, string.Join(",", r.Related))), b.Relations.Select(r => (r.Id, r.Relating, string.Join(",", r.Related))));
    }

    /// <summary>
    /// A session that ends as a crash ends it: nothing disposed, the journal's
    /// handle gone with the process. The process lives on here, so the session
    /// and its journal are dropped and collected (the handle's finaliser lets
    /// the file go); <paramref name="work"/> hands back what the test still needs.
    /// </summary>
    T Crashed<T>(Func<Fixture, T> work)
    {
        var result = Run(work);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return result;

        [MethodImpl(MethodImplOptions.NoInlining)]
        T Run(Func<Fixture, T> w)
        {
            var f = new Fixture();
            _ = new Recovery(_dir, f.Session);
            return w(f);
        }
    }

    [Fact]
    public void A_new_projects_unsaved_work_comes_back_with_its_undo_history()
    {
        var (model, a, usage) = Crashed(f =>
        {
            var a = f.Wall(0, 0, 5, 0);
            f.Wall(5, 0, 5, 4);
            f.Run("architecture.flipWall", new JsonObject { ["id"] = a });
            f.Session.Undo();          // the flip undone…
            f.Session.Redo();          // …and done again
            f.Wall(0, 4, 5, 4);
            f.Session.Undo();          // the last wall undone: not in the model, but redoable
            return (f.Model, a, f.Usage(a));
        });

        var g = new Fixture();
        using var next = new Recovery(_dir, g.Session);
        var work = Assert.Single(next.List());
        Assert.Null(work.Path);
        Assert.Equal(7, work.Edits);   // 3 walls, a flip, an undo, a redo, an undo
        Assert.Equal("Wall", work.LastEdit);
        next.Restore(work.File);

        Same(model, g.Model);
        Assert.True(g.Session.History.Dirty);
        Assert.Equal(usage, g.Usage(a));
        Assert.True(g.Session.History.CanRedo);
        g.Session.Redo();
        Assert.Equal(3, g.Model.OfClass(IfcClass.Wall).Count());
    }

    [Fact]
    public void Work_on_a_saved_project_comes_back_on_top_of_its_file_and_not_on_a_changed_one()
    {
        var path = Path.Combine(_dir, "house.lapin");
        var model = Crashed(f =>
        {
            f.Wall(0, 0, 5, 0);
            PackageFiles.Create().Save(f.Session, path);   // saving starts the journal again
            Assert.Single(Journals);
            f.Wall(0, 3, 5, 3);
            return f.Model;
        });

        var g = new Fixture();
        using (var next = new Recovery(_dir, g.Session))
        {
            var work = Assert.Single(next.List());
            Assert.Equal(path, work.Path);
            Assert.Equal(1, work.Edits);
            next.Restore(work.File);
            Same(model, g.Model);
            Assert.Equal(path, g.Session.Path);
        }   // ends with the work unsaved: its journal stays

        // The same work on a file saved since by someone else: refused.
        var h = new Fixture();
        using var other = new Recovery(_dir, h.Session);
        var stale = Assert.Single(other.List());
        h.Wall(9, 9, 10, 9);
        PackageFiles.Create().Save(h.Session, path);
        Assert.Throws<CommandException>(() => other.Restore(stale.File));
    }

    [Fact]
    public void A_line_cut_off_by_the_crash_is_left_out()
    {
        var f = new Fixture();
        var journal = new Recovery(_dir, f.Session);
        f.Wall(0, 0, 5, 0);
        f.Wall(0, 2, 5, 2);
        journal.Dispose();   // (keeps the file: there are unsaved edits)
        var file = Journals.Single();
        File.AppendAllText(file, """{"op":"tx","label":"Wall","entit""");

        var g = new Fixture();
        using var next = new Recovery(_dir, g.Session);
        next.Restore(Assert.Single(next.List()).File);
        Assert.Equal(2, g.Model.OfClass(IfcClass.Wall).Count());
    }

    [Fact]
    public void A_clean_ending_or_an_old_journal_without_edits_leaves_nothing_behind()
    {
        var f = new Fixture();
        using (new Recovery(_dir, f.Session)) { }
        Assert.Empty(Journals);

        // A journal a session started, never wrote in, and left behind (its lock gone with it).
        var stale = Path.Combine(_dir, "recovery", "20260101-000000-00000000.jsonl");
        File.WriteAllText(stale, """{"op":"start","at":"2026-01-01T00:00:00+00:00","product":"test","path":null,"base":{"entities":[],"relations":[],"classes":{}}}""" + "\n");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddMinutes(-1));
        var g = new Fixture();
        using var next = new Recovery(_dir, g.Session);
        Assert.Empty(next.List());
        Assert.False(File.Exists(stale));
        Assert.Equal([next.CurrentFile!], Journals);   // only the open session's own
    }

    [Fact]
    public void A_second_instance_leaves_the_first_ones_live_journal_alone()
    {
        var f = new Fixture();
        using var first = new Recovery(_dir, f.Session);   // open, no edit yet: an empty file, but in use
        var g = new Fixture();
        using (var second = new Recovery(_dir, g.Session))
        {
            Assert.Empty(second.List());
            Assert.True(File.Exists(first.CurrentFile!));
            Assert.Throws<CommandException>(() => second.Discard(first.CurrentFile!));
        }

        // The first goes on writing in its journal, which is still where it was.
        f.Wall(0, 0, 5, 0);
        f.Wall(0, 2, 5, 2);
        first.Dispose();   // unsaved edits: kept
        var h = new Fixture();
        using var next = new Recovery(_dir, h.Session);
        Assert.Equal(2, Assert.Single(next.List()).Edits);
    }

    [Fact]
    public void Another_project_over_unsaved_work_keeps_its_journal_a_save_does_not()
    {
        var f = new Fixture();
        using var journal = new Recovery(_dir, f.Session);
        f.Wall(0, 0, 5, 0);
        var unsaved = journal.CurrentFile!;
        f.Session.New();   // the work is gone from the session: its journal is the only trace of it
        Assert.NotEqual(unsaved, journal.CurrentFile);
        Assert.True(File.Exists(unsaved));

        f.Wall(1, 1, 4, 1);
        PackageFiles.Create().Save(f.Session, Path.Combine(_dir, "a.lapin"));   // saved: that journal is not needed
        Assert.Equal(new[] { unsaved, journal.CurrentFile! }.Order(), Journals.Order());

        var g = new Fixture();
        using var next = new Recovery(_dir, g.Session);
        var work = Assert.Single(next.List());
        Assert.Equal(unsaved, work.File);
        Assert.Equal(1, work.Edits);
    }

    [Fact]
    public void Restoring_earlier_work_over_unsaved_work_keeps_the_latters_journal()
    {
        Crashed(f => f.Wall(0, 0, 5, 0));
        var g = new Fixture();
        using (var next = new Recovery(_dir, g.Session))
        {
            g.Wall(9, 9, 10, 9);   // unsaved work of its own
            var own = next.CurrentFile!;
            next.Restore(Assert.Single(next.List()).File);
            Assert.NotEqual(own, next.CurrentFile);
            Assert.True(File.Exists(own));
            var h = new Fixture();
            using var third = new Recovery(_dir, h.Session);
            var work = Assert.Single(third.List());   // the restored journal is in use again; the other is on offer
            Assert.Equal(own, work.File);
            Assert.Equal(1, work.Edits);
        }
    }
}
