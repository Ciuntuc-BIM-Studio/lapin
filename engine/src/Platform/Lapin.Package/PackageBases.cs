using System.Diagnostics;
using Lapin.Model;

namespace Lapin.Package;

/// <summary>
/// Private copies of the project files that are open, so what a save reads from the file as it goes (the parts of the
/// versions, the files of the issues) and what the recovery journal starts from are still there when the file is renamed,
/// moved or deleted while the project is open (audit 15.11.2026, G1). A copy is named by its content and the process that
/// made it: <c>&lt;sha256&gt;.&lt;pid&gt;.lapin</c> in <see cref="Folder"/>. It is taken when a project is opened or saved, held
/// while it is the open project's (<see cref="Hold"/>), deleted when the project moves on (<see cref="Release"/>), and those
/// left by a process that ended are swept away (<see cref="Sweep"/>) unless a recovery journal still starts from them.
/// A file larger than <see cref="MaxCopy"/> is not copied: it is read from where it is, as before. On macOS (APFS) a copy is
/// a clone and costs no space until one of the two changes.
/// </summary>
public static class PackageBases
{
    /// <summary>Where the copies are kept (the host sets the user folder's; else the temporary folder's <c>lapin-bases</c>).</summary>
    public static string? Folder { get; set; }

    /// <summary>The largest file copied (bytes).</summary>
    public static long MaxCopy { get; set; } = 4L << 30;

    /// <summary>How old a copy left by a process that ended may get before it goes, when the journals are not known.</summary>
    public static readonly TimeSpan Unclaimed = TimeSpan.FromDays(3);

    static string Dir => Folder ?? Path.Combine(Path.GetTempPath(), "lapin-bases");

    static readonly object Sync = new();
    /// <summary>What was found of a file: its length and time, its hash, its copy; read again only when the file changed.</summary>
    static readonly Dictionary<string, (long Length, DateTime Written, string Sha, string? Copy)> Known = new(StringComparer.Ordinal);
    static readonly Dictionary<string, int> Holds = new(StringComparer.Ordinal);
    static bool _swept;

    /// <summary>A project file as it was taken: what it hashes to, and its private copy (null when it is too big, or could not be made).</summary>
    public sealed record Base(string Sha, string? Copy);

    /// <summary>The SHA-256 of a file (hex), without copying it; null when there is no such file. Remembered while the file stays as it is.</summary>
    public static string? Hash(string path)
    {
        var full = Path.GetFullPath(path);
        var info = new FileInfo(full);
        if (!info.Exists) return null;
        lock (Sync)
            if (Known.TryGetValue(full, out var k) && k.Length == info.Length && k.Written == info.LastWriteTimeUtc) return k.Sha;
        var sha = HashFile(full);
        lock (Sync)
            Known[full] = Known.TryGetValue(full, out var k) && k.Sha == sha ? k with { Length = info.Length, Written = info.LastWriteTimeUtc } : (info.Length, info.LastWriteTimeUtc, sha, null);
        return sha;
    }

    /// <summary>The file at <paramref name="path"/> taken: its hash and a private copy of it; null when there is no such file.</summary>
    public static Base? Keep(string path)
    {
        var full = Path.GetFullPath(path);
        var info = new FileInfo(full);
        if (!info.Exists) return null;
        lock (Sync)
        {
            if (!_swept) { _swept = true; SweepLocked(null); }
            if (Known.TryGetValue(full, out var k) && k.Length == info.Length && k.Written == info.LastWriteTimeUtc && k.Copy != null && File.Exists(k.Copy))
            {
                Touch(k.Copy);
                return new Base(k.Sha, k.Copy);
            }
        }
        string? copy = null;
        string sha;
        if (info.Length <= MaxCopy)
        {
            var temp = Path.Combine(Dir, $"{Guid.NewGuid():N}.{Environment.ProcessId}.tmp");
            try
            {
                Directory.CreateDirectory(Dir);
                File.Copy(full, temp);
                // The copy's hash is what it holds: a file changed while it was copied names the copy by what was copied.
                sha = HashFile(temp);
                copy = Path.Combine(Dir, $"{sha}.{Environment.ProcessId}.lapin");
                lock (Sync)
                {
                    if (File.Exists(copy)) { File.Delete(temp); Touch(copy); }
                    else File.Move(temp, copy);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
                copy = null;
                sha = HashFile(full);
            }
        }
        else sha = HashFile(full);
        lock (Sync) Known[full] = (info.Length, info.LastWriteTimeUtc, sha, copy);
        return new Base(sha, copy);
    }

    /// <summary>A copy of a file that hashed to <paramref name="sha"/>, this process's first; null when there is none.</summary>
    public static string? Find(string sha)
    {
        if (sha.Length != 64 || !sha.All(Uri.IsHexDigit) || !Directory.Exists(Dir)) return null;
        var own = Path.Combine(Dir, $"{sha}.{Environment.ProcessId}.lapin");
        if (File.Exists(own)) return own;
        return Directory.GetFiles(Dir, $"{sha}.*.lapin").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    /// <summary>A copy in use by an open project: kept until it is released as often as it was held.</summary>
    public static void Hold(string copy)
    {
        lock (Sync) Holds[copy] = Holds.GetValueOrDefault(copy) + 1;
    }

    /// <summary>A copy no longer in use: deleted once no open project holds it.</summary>
    public static void Release(string copy)
    {
        lock (Sync)
        {
            var n = Holds.GetValueOrDefault(copy) - 1;
            if (n > 0) { Holds[copy] = n; return; }
            Holds.Remove(copy);
            foreach (var key in Known.Where(kv => kv.Value.Copy == copy).Select(kv => kv.Key).ToList()) Known[key] = Known[key] with { Copy = null };
            try { if (File.Exists(copy)) File.Delete(copy); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// The copies of processes that ended taken away: those no journal starts from when <paramref name="journals"/> says
    /// which hashes the recovery journals start from, else those older than <see cref="Unclaimed"/>.
    /// </summary>
    public static void Sweep(IReadOnlySet<string>? journals)
    {
        lock (Sync)
        {
            _swept = true;
            SweepLocked(journals);
        }
    }

    static void SweepLocked(IReadOnlySet<string>? journals)
    {
        if (!Directory.Exists(Dir)) return;
        try
        {
            foreach (var file in Directory.GetFiles(Dir))
            {
                var parts = Path.GetFileName(file).Split('.');
                if (parts.Length != 3 || !int.TryParse(parts[1], out var pid) || pid == Environment.ProcessId || Alive(pid)) continue;
                var keep = parts[2] == "lapin" && (journals != null ? journals.Contains(parts[0]) : DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < Unclaimed);
                if (!keep) File.Delete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    static bool Alive(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or PlatformNotSupportedException) { return false; }
    }

    static void Touch(string file)
    {
        try { File.SetLastWriteTimeUtc(file, DateTime.UtcNow); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    static string HashFile(string path)
    {
        using var s = File.OpenRead(path);
        return Hashes.Sha256Hex(s);
    }
}
