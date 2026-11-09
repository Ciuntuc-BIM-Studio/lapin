using System.Security.Cryptography;
using Lapin.Package;

namespace Lapin.Protocol;

/// <summary>An open journal: each line written goes to lasting storage before the call returns (a crash right after loses nothing).</summary>
public interface IJournalWriter : IDisposable
{
    void WriteLine(string line);
}

/// <summary>
/// Where the recovery journals live, and what <see cref="Recovery"/> needs of
/// the project files they start from: files in the user folder on the desktop
/// (<see cref="FileJournalStore"/>); the browser's own storage there.
/// A journal is named by what the store gives back (on the desktop, its path).
/// </summary>
public interface IJournalStore
{
    /// <summary>A new journal, held exclusively while it is written.</summary>
    (string Name, IJournalWriter Writer) Create(string fileName);
    /// <summary>An earlier journal opened again to go on in it.</summary>
    IJournalWriter Append(string name);
    IEnumerable<string> Journals();
    /// <summary>Held by a running Lapin.</summary>
    bool InUse(string name);
    /// <summary>A name this store gave (and no other path).</summary>
    bool Holds(string name);
    IReadOnlyList<string> ReadLines(string name);
    DateTime LastWrite(string name);
    void Delete(string name);
    /// <summary>The SHA-256 of the project file a journal started from; null when there is no such file.</summary>
    string? ProjectHash(string path);
    PackageContent ReadProject(string path);
}

/// <summary>The journals as files in a folder (the desktop's user folder): each held with an exclusive share while written, flushed to disk on every line.</summary>
public sealed class FileJournalStore : IJournalStore
{
    readonly string _dir;

    public FileJournalStore(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// A line is with the operating system when <see cref="WriteLine"/> returns (a crash of Lapin loses nothing) and on the disk at most
    /// <see cref="DiskWithin"/> later, or when the journal is closed (a save, closing the project, quitting): the call on the session's
    /// thread no longer waits for the disk (an <c>fsync</c> after every edit was 3 to 5 ms of every one, decision 424). Only a power cut
    /// or a crash of the system in that moment loses the last line or two.
    /// </summary>
    sealed class Writer(FileStream file) : IJournalWriter
    {
        /// <summary>How long a written line may wait to be on the disk.</summary>
        public static readonly TimeSpan DiskWithin = TimeSpan.FromMilliseconds(100);

        readonly StreamWriter _w = new(file);
        readonly object _sync = new();
        bool _unsynced, _scheduled, _closed;

        public void WriteLine(string line)
        {
            lock (_sync)
            {
                _w.WriteLine(line);
                _w.Flush();
                _unsynced = true;
                if (_scheduled) return;
                _scheduled = true;
            }
            // Holding the writer only weakly: a session dropped without closing (a crash the tests make) must still let its file go.
            var me = new WeakReference<Writer>(this);
            _ = Task.Delay(DiskWithin).ContinueWith(_ => { if (me.TryGetTarget(out var w)) w.ToDisk(); });
        }

        void ToDisk()
        {
            lock (_sync)
            {
                _scheduled = false;
                if (_closed || !_unsynced) return;
                _unsynced = false;
                file.Flush(flushToDisk: true);
            }
        }

        /// <summary>Closing puts what is written on the disk first: the journal of a project closed cleanly is complete.</summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_closed) return;
                _closed = true;
                _w.Flush();
                if (_unsynced) file.Flush(flushToDisk: true);
                _w.Dispose();
            }
        }
    }

    /// <summary>The journal, held exclusively while this session writes in it.</summary>
    static FileStream Open(string file, FileMode mode) => new(file, mode, FileAccess.Write, FileShare.None);

    public (string Name, IJournalWriter Writer) Create(string fileName)
    {
        var path = Path.Combine(_dir, fileName);
        return (path, new Writer(Open(path, FileMode.CreateNew)));
    }

    public IJournalWriter Append(string name) => new Writer(Open(name, FileMode.Append));

    public IEnumerable<string> Journals() => Directory.GetFiles(_dir, "*.jsonl");

    /// <summary>Held open by a running Lapin: its exclusive share (a lock, on macOS and Linux) refuses us.</summary>
    public bool InUse(string name)
    {
        try
        {
            using var _ = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException) { return File.Exists(name); }
    }

    public bool Holds(string name) => Path.GetDirectoryName(Path.GetFullPath(name)) == Path.GetFullPath(_dir);

    public IReadOnlyList<string> ReadLines(string name) => File.ReadAllLines(name);

    public DateTime LastWrite(string name) => File.GetLastWriteTimeUtc(name);

    public void Delete(string name)
    {
        if (File.Exists(name)) File.Delete(name);
    }

    public string? ProjectHash(string path)
    {
        if (!File.Exists(path)) return null;
        using var s = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(s));
    }

    public PackageContent ReadProject(string path) => LapinPackage.Read(path);
}
