using Lapin.Architecture;
using Lapin.Package;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>The recovery journal on a storage that is not files (the browser's): the same work comes back.</summary>
public class JournalStoreTests
{
    /// <summary>Journals kept in memory, as a browser keeps them in its own storage; a journal is in use while a writer holds it.</summary>
    sealed class MemoryStore : IJournalStore
    {
        public readonly Dictionary<string, List<string>> Files = [];
        readonly HashSet<string> _open = [];

        sealed class Writer(MemoryStore s, string name) : IJournalWriter
        {
            public void WriteLine(string line) => s.Files[name].Add(line);
            public void Dispose() => s._open.Remove(name);
        }

        public (string Name, IJournalWriter Writer) Create(string fileName) { Files[fileName] = []; _open.Add(fileName); return (fileName, new Writer(this, fileName)); }
        public IJournalWriter Append(string name) { _open.Add(name); return new Writer(this, name); }
        public IEnumerable<string> Journals() => [.. Files.Keys];
        public bool InUse(string name) => _open.Contains(name);
        public bool Holds(string name) => Files.ContainsKey(name);
        public IReadOnlyList<string> ReadLines(string name) => Files[name];
        public DateTime LastWrite(string name) => DateTime.UtcNow;
        public void Delete(string name) => Files.Remove(name);
        public string? ProjectHash(string path) => null;
        public PackageContent ReadProject(string path) => throw new InvalidOperationException("No project files here.");
        /// <summary>What a crash leaves: the journals, no writer holding them.</summary>
        public void Crash() => _open.Clear();
    }

    [Fact]
    public void Unsaved_work_in_a_store_that_is_not_files_comes_back_whole()
    {
        var store = new MemoryStore();
        var f = new Fixture(new ArchitectureModule());
        var journal = new Recovery(store, f.Session);
        f.Wall(0, 0, 5, 0);
        f.Wall(5, 0, 5, 4);
        f.Session.Undo();
        store.Crash();

        var g = new Fixture(new ArchitectureModule());
        using var next = new Recovery(store, g.Session);
        var work = Assert.Single(next.List());
        Assert.Equal(3, work.Edits);
        next.Restore(work.File);
        Assert.Equal(f.Model.Entities.Select(e => e.Id), g.Model.Entities.Select(e => e.Id));
        Assert.Single(g.Model.OfClass("IfcWall"));
        Assert.True(g.Session.History.CanRedo);
        GC.KeepAlive(journal);
    }
}
