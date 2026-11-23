using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Package.Remote;

/// <summary>A folder remote's ref entry (§7.3): one update of one ref, never changed or deleted.</summary>
public sealed record FolderRefEntry(string File, string Name, int Seq, string? Old, string? New, RemoteAuthor Author, DateTimeOffset At, bool Force);

/// <summary>A ref resolved from its entries (§7.3): its value, the chain's length, the chain and the detached entries.</summary>
public sealed record FolderRefState(string? Value, int Seq, IReadOnlyList<FolderRefEntry> Chain, IReadOnlyList<FolderRefEntry> Detached);

/// <summary>
/// The folder remote of lapin-remote 1.0 (docs/spec-lapin-remote.md §7): a folder on a disk, a NAS or a synced folder holding
/// <c>lapin-remote.json</c> and one folder per project, its objects gzipped under <c>objects/&lt;ab&gt;/&lt;id&gt;.gz</c>, its commits
/// and tags under <c>commits/&lt;id&gt;.json</c>, each ref an append-only log of entries under <c>refs/&lt;name&gt;/</c>, compare-and-swap
/// under an exclusive lock. Files are written to a temporary name and renamed. Anyone who can write the folder is its owner.
/// </summary>
public sealed class FolderRemote : IRemote
{
    public const string MarkerFile = "lapin-remote.json";
    static readonly byte[] Marker = Encoding.UTF8.GetBytes("{\"format\":\"lapin-remote\",\"v\":1}");
    public static readonly TimeSpan StaleLock = TimeSpan.FromMinutes(10);

    readonly string _root, _dir;
    readonly RemoteAuthor _me;
    readonly Func<DateTime> _now;

    public string Kind => "folder";
    public int MaxBatch => 10_000;
    public bool Viewers => true;
    public List<string> Notes { get; } = [];
    public string ProjectId { get; }

    FolderRemote(string root, string project, RemoteAuthor me, Func<DateTime>? now)
    {
        _root = root;
        ProjectId = project;
        _dir = Path.Combine(root, project);
        _me = me;
        _now = now ?? (() => DateTime.UtcNow);
    }

    /// <summary>A folder remote's <c>file:</c> URI (§6.5) as a path, and back.</summary>
    public static string PathOf(string url) => url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? new Uri(url).LocalPath : url;
    public static string UrlOf(string path) => new Uri(Path.GetFullPath(path)).AbsoluteUri;

    /// <summary>The folder's project, checked: the folder's <c>lapin-remote.json</c> of a version this Lapin knows, and the project's folder there.</summary>
    public static FolderRemote Open(string folder, string project, RemoteAuthor me, Func<DateTime>? now = null)
    {
        CheckMarker(folder);
        if (!Guid.TryParse(project, out _) || project != project.ToLowerInvariant()) throw new InvalidDataException($"Not a project id: {project}.");
        if (!File.Exists(Path.Combine(folder, project, "project.json"))) throw new InvalidDataException($"The folder holds no project {project}.");
        return new FolderRemote(folder, project, me, now);
    }

    static void CheckMarker(string folder)
    {
        var marker = Path.Combine(folder, MarkerFile);
        if (!File.Exists(marker)) throw new InvalidDataException($"{folder} is not a Lapin remote (it has no {MarkerFile}).");
        var o = JsonNode.Parse(File.ReadAllBytes(marker)) as JsonObject;
        if ((string?)o?["format"] != "lapin-remote") throw new InvalidDataException($"{folder}'s {MarkerFile} is not a Lapin remote's.");
        if ((int?)o?["v"] != 1) throw new InvalidDataException($"{folder} is a Lapin remote of version {o?["v"]}: update Lapin to use it.");
    }

    /// <summary>The projects of a folder remote, by name: [id, name].</summary>
    public static List<(string Id, string Name)> Projects(string folder)
    {
        CheckMarker(folder);
        var o = new List<(string, string)>();
        foreach (var dir in Directory.EnumerateDirectories(folder))
        {
            var file = Path.Combine(dir, "project.json");
            if (!File.Exists(file)) continue;
            try
            {
                var p = JsonNode.Parse(File.ReadAllBytes(file)) as JsonObject;
                if ((string?)p?["id"] is { } id && id == Path.GetFileName(dir)) o.Add((id, (string?)p?["name"] ?? id));
            }
            catch (JsonException) { }
        }
        return o.OrderBy(p => p.Item2, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>A new project in the folder (the folder made a remote first when it is not one): its id, a lowercase UUID.</summary>
    public static string Create(string folder, string name, DateTime now, string? id = null)
    {
        Directory.CreateDirectory(folder);
        var marker = Path.Combine(folder, MarkerFile);
        if (!File.Exists(marker)) WriteOnce(marker, Marker);
        CheckMarker(folder);
        id ??= Guid.NewGuid().ToString("D");
        var dir = Path.Combine(folder, id);
        Directory.CreateDirectory(dir);
        var project = new JsonObject
        {
            ["format"] = "lapin-remote-project", ["v"] = 1, ["id"] = id, ["name"] = string.IsNullOrWhiteSpace(name) ? "Project" : name.Trim(),
            ["defaultRef"] = RefNames.Main, ["createdAt"] = RemoteObjects.Stamp(now),
        };
        WriteOnce(Path.Combine(dir, "project.json"), Jcs.Canonicalize(project));
        return id;
    }

    public RemoteProject Project()
    {
        var p = JsonNode.Parse(File.ReadAllBytes(Path.Combine(_dir, "project.json"))) as JsonObject ?? [];
        return new RemoteProject(ProjectId, (string?)p["name"] ?? ProjectId, (string?)p["defaultRef"] ?? RefNames.Main, "owner");
    }

    // ------------------------------------------------------------ files

    /// <summary>Bytes written under a temporary name in the same folder, flushed, then renamed; a final name that exists is not written again.</summary>
    static void WriteOnce(string path, byte[] bytes)
    {
        if (File.Exists(path)) return;
        var temp = Temp(path);
        WriteFlushed(temp, bytes);
        try { File.Move(temp, path); }
        catch (IOException) when (File.Exists(path)) { File.Delete(temp); }
    }

    static void WriteReplacing(string path, byte[] bytes)
    {
        var temp = Temp(path);
        WriteFlushed(temp, bytes);
        File.Move(temp, path, overwrite: true);
    }

    static string Temp(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
    }

    static void WriteFlushed(string path, byte[] bytes)
    {
        using var f = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        f.Write(bytes);
        f.Flush(flushToDisk: true);
    }

    string ObjectPath(string id) => Path.Combine(_dir, "objects", id[..2], id + ".gz");
    string CommitPath(string id) => Path.Combine(_dir, "commits", id + ".json");

    bool Holds(string id) => File.Exists(ObjectPath(id)) || File.Exists(CommitPath(id));

    public IReadOnlyList<string> Missing(IReadOnlyList<string> ids)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return ids.Where(id => ObjectIds.Valid(id) && seen.Add(id) && !Holds(id)).ToList();
    }

    public void Put(string id, byte[] bytes, ObjectKind kind)
    {
        ObjectIds.Require(id);
        if (ObjectIds.Of(bytes) != id) throw new RemoteException(400, "hash_mismatch", "The bytes do not hash to the id.");
        if (kind is ObjectKind.Commit or ObjectKind.Tag)
        {
            if (!Jcs.IsCanonical(bytes)) throw new RemoteException(422, "invalid_object", "Not canonical JSON.");
            WriteOnce(CommitPath(id), bytes);
            return;
        }
        if (File.Exists(ObjectPath(id))) return;
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true)) gz.Write(bytes);
        WriteOnce(ObjectPath(id), ms.ToArray());
    }

    public byte[]? Get(string id)
    {
        if (!ObjectIds.Valid(id)) return null;
        try
        {
            if (File.Exists(CommitPath(id)))
            {
                var bytes = File.ReadAllBytes(CommitPath(id));
                if (ObjectIds.Of(bytes) == id && Jcs.IsCanonical(bytes)) return bytes;
                Notes.Add($"The remote's commit {id[..12]}… is damaged: left aside.");
                return null;
            }
            if (!File.Exists(ObjectPath(id))) return null;
            using var f = File.OpenRead(ObjectPath(id));
            using var gz = new GZipStream(f, CompressionMode.Decompress);
            using var ms = new MemoryStream();
            gz.CopyTo(ms);
            var o = ms.ToArray();
            if (ObjectIds.Of(o) != id || RemoteObjects.KindOf(o) == ObjectKind.Tree && !Jcs.IsCanonical(o))
            {
                Notes.Add($"The remote's object {id[..12]}… is damaged: left aside.");
                return null;
            }
            return o;
        }
        catch (InvalidDataException) { Notes.Add($"The remote's object {id[..12]}… cannot be read: left aside."); return null; }
    }

    public IReadOnlyDictionary<string, byte[]> GetMany(IReadOnlyList<string> ids)
    {
        var o = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
            o[id] = Get(id) ?? throw new RemoteException(404, "object_missing", $"The remote lacks {id[..12]}….");
        return o;
    }

    public IReadOnlyList<(string Id, byte[] Bytes)> Commits(string from, IReadOnlyList<string> stop)
    {
        var stops = stop.ToHashSet(StringComparer.Ordinal);
        if (!File.Exists(CommitPath(from))) throw new RemoteException(404, "object_missing", $"The remote has no commit {from[..12]}….");
        // Reverse post-order of a walk from `from`: each commit before all of its parents.
        var post = new List<(string, byte[])>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (stops.Contains(id) || !seen.Add(id)) return;
            var bytes = Get(id);
            if (bytes == null) return;
            foreach (var p in RemoteObjects.ReadCommit(bytes, id).Parents) Visit(p);
            post.Add((id, bytes));
        }
        Visit(from);
        post.Reverse();
        return post;
    }

    // ------------------------------------------------------------ refs

    /// <summary>Where the refs are: a ref's entries in the folder named as the ref, under the project (<c>refs/heads/main/</c>).</summary>
    string RefsDir => Path.Combine(_dir, "refs");

    /// <summary>A ref entry's file name (§7.3): 8-digit seq, the new id (64 zeros for a deletion), the author's key.</summary>
    public static string EntryName(int seq, string? @new, RemoteAuthor author) => $"{seq.ToString("D8", CultureInfo.InvariantCulture)}-{@new ?? ObjectIds.Zero}-{author.Key}.ref";

    /// <summary>A ref entry's canonical content (§7.3).</summary>
    public static byte[] EntryBytes(string name, int seq, string? old, string? @new, RemoteAuthor author, DateTime at, bool force = false)
    {
        var o = new JsonObject
        {
            ["format"] = "lapin-ref", ["v"] = 1, ["name"] = name, ["seq"] = seq, ["old"] = old, ["new"] = @new,
            ["author"] = author.ToJson(), ["at"] = RemoteObjects.Stamp(at),
        };
        if (force) o["force"] = true;
        return Jcs.Canonicalize(o);
    }

    /// <summary>One entry read from its file name (relative to the project folder) and bytes; null (and a note) when it does not parse or disagrees with its path.</summary>
    public static FolderRefEntry? ParseEntry(string file, byte[] content, List<string>? notes = null)
    {
        FolderRefEntry? Bad(string why) { notes?.Add($"The remote's ref entry {file} was ignored: {why}."); return null; }
        var slash = file.LastIndexOf('/');
        if (slash < 0 || !file.StartsWith("refs/", StringComparison.Ordinal)) return Bad("not under refs/");
        var refName = file[..slash];
        var fileName = file[(slash + 1)..];
        // By position: 8 digits, '-', 64 characters, '-', the rest up to ".ref".
        if (fileName.Length < 8 + 1 + 64 + 1 + 1 + 4 || !fileName.EndsWith(".ref", StringComparison.Ordinal) || fileName[8] != '-' || fileName[73] != '-'
            || !int.TryParse(fileName[..8], NumberStyles.None, CultureInfo.InvariantCulture, out var seqOfName))
            return Bad("its name is not <seq>-<id>-<author>.ref");
        JsonObject? o;
        try { o = JsonNode.Parse(content) as JsonObject; }
        catch (JsonException) { return Bad("its content is not JSON"); }
        if (o == null || (string?)o["format"] != "lapin-ref" || (int?)o["v"] != 1) return Bad("not a lapin-ref of version 1");
        if ((string?)o["name"] != refName) return Bad("its name disagrees with its folder");
        if ((int?)o["seq"] != seqOfName) return Bad("its seq disagrees with its file name");
        string? Id(string k) => o[k] is JsonValue v && v.TryGetValue<string>(out var s) ? (ObjectIds.Valid(s) ? s : "!") : null;
        var old = Id("old");
        var @new = Id("new");
        if (old == "!" || @new == "!") return Bad("an id is not one");
        var author = RemoteAuthor.From(o["author"]);
        if (author == null || !DateTimeOffset.TryParse((string?)o["at"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)) return Bad("no author or time");
        return new FolderRefEntry(file, refName, seqOfName, old, @new, author, at, o["force"] is JsonValue fv && fv.TryGetValue<bool>(out var f) && f);
    }

    /// <summary>
    /// A ref's value from its entries (§7.3, the vector <c>folder/fork-detection.json</c>): from nothing, each next link the entry of the
    /// next seq whose old is the value so far; of several (a fork), the earliest <c>at</c>, then the smallest file name. The rest are detached.
    /// </summary>
    public static FolderRefState Resolve(IReadOnlyList<FolderRefEntry> entries)
    {
        string? value = null;
        var seq = 0;
        var chain = new List<FolderRefEntry>();
        while (true)
        {
            var candidates = entries.Where(e => e.Seq == seq + 1 && e.Old == value).ToList();
            if (candidates.Count == 0) break;
            var next = candidates.OrderBy(e => e.At).ThenBy(e => e.File, StringComparer.Ordinal).First();
            chain.Add(next);
            value = next.New;
            seq++;
        }
        var on = chain.Select(c => c.File).ToHashSet(StringComparer.Ordinal);
        return new FolderRefState(value, seq, chain, entries.Where(e => !on.Contains(e.File)).OrderBy(e => e.File, StringComparer.Ordinal).ToList());
    }

    /// <summary>Every ref's entries, by ref name: the folders under refs/ holding .ref files whose path is a valid ref name.</summary>
    Dictionary<string, List<FolderRefEntry>> Entries()
    {
        var o = new Dictionary<string, List<FolderRefEntry>>(StringComparer.Ordinal);
        if (!Directory.Exists(RefsDir)) return o;
        foreach (var path in Directory.EnumerateFiles(RefsDir, "*.ref", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(path).StartsWith('.')) continue;
            var rel = Path.GetRelativePath(_dir, path).Replace(Path.DirectorySeparatorChar, '/');
            var refName = rel[..rel.LastIndexOf('/')];
            if (!RefNames.Valid(refName)) continue;
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (IOException) { continue; }
            if (ParseEntry(rel, bytes, Notes) is { } e) (o.TryGetValue(refName, out var list) ? list : o[refName] = []).Add(e);
        }
        return o;
    }

    List<FolderRefEntry> EntriesOf(string name)
    {
        var dir = Path.Combine(_dir, name.Replace('/', Path.DirectorySeparatorChar));
        var o = new List<FolderRefEntry>();
        if (!Directory.Exists(dir)) return o;
        foreach (var path in Directory.EnumerateFiles(dir, "*.ref", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetFileName(path).StartsWith('.')) continue;
            if (ParseEntry($"{name}/{Path.GetFileName(path)}", File.ReadAllBytes(path), Notes) is { } e) o.Add(e);
        }
        return o;
    }

    public RefsResult Refs(string? etag)
    {
        var refs = new List<RemoteRef>();
        foreach (var (name, entries) in Entries().OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var state = Resolve(entries);
            if (state.Value is { } v)
            {
                var last = state.Chain[^1];
                refs.Add(new RemoteRef(name, v, RemoteObjects.Stamp(last.At.UtcDateTime), last.Author));
            }
        }
        // An ETag of the folder: the hash of the refs as resolved (a folder has no counter).
        var tag = "\"f-" + ObjectIds.Of(Encoding.UTF8.GetBytes(string.Join('\n', refs.Select(r => r.Name + " " + r.Target))))[..16] + "\"";
        return etag == tag ? new RefsResult(true, [], tag) : new RefsResult(false, refs, tag);
    }

    public IReadOnlyList<DetachedEntry> Detached() =>
        Entries().SelectMany(kv => Resolve(kv.Value).Detached).Select(e => new DetachedEntry(e.File, e.Name, e.New, e.Author)).ToList();

    /// <summary>The project's lock (§7.2), taken exclusively; a lock older than 10 minutes is broken, and said.</summary>
    sealed class Lock : IDisposable
    {
        readonly string _path;
        public Lock(FolderRemote r)
        {
            _path = Path.Combine(r._dir, "lock");
            var body = Jcs.Canonicalize(new JsonObject { ["by"] = r._me.Id, ["name"] = r._me.Name, ["host"] = Environment.MachineName, ["at"] = RemoteObjects.Stamp(r._now()) });
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    using var f = new FileStream(_path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    f.Write(body);
                    f.Flush(flushToDisk: true);
                    return;
                }
                catch (IOException) when (File.Exists(_path))
                {
                    var (at, who, host) = Holder();
                    if (r._now() - at > StaleLock)
                    {
                        try { File.Delete(_path); } catch (IOException) { }
                        r.Notes.Add($"The remote's lock from {who} on {host} was older than 10 minutes and was broken.");
                        continue;
                    }
                    if (attempt >= 20) throw new RemoteException(423, "locked", $"The remote is being updated by {who} on {host}: try again in a moment.");
                    Thread.Sleep(250);
                }
            }
        }

        (DateTime At, string Who, string Host) Holder()
        {
            try
            {
                var o = JsonNode.Parse(File.ReadAllBytes(_path)) as JsonObject;
                var at = DateTimeOffset.TryParse((string?)o?["at"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t.UtcDateTime : File.GetLastWriteTimeUtc(_path);
                return (at, (string?)o?["name"] ?? "someone", (string?)o?["host"] ?? "another computer");
            }
            catch (Exception ex) when (ex is IOException or JsonException) { return (File.GetLastWriteTimeUtc(_path), "someone", "another computer"); }
        }

        public void Dispose()
        {
            try { File.Delete(_path); } catch (IOException) { }
        }
    }

    /// <summary>Whether <paramref name="old"/> is an ancestor of <paramref name="new"/> through the folder's commits.</summary>
    bool Descends(string @new, string old)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>([@new]);
        while (stack.Count > 0)
        {
            var c = stack.Pop();
            if (c == old) return true;
            if (!seen.Add(c) || Get(c) is not { } bytes) continue;
            foreach (var p in RemoteObjects.ReadCommit(bytes, c).Parents) stack.Push(p);
        }
        return false;
    }

    /// <summary>
    /// The updates applied under one lock (§7.4), in order: each resolved, compared with its <c>old</c> (409 <c>ref_moved</c>), checked
    /// against the rules a folder can check (fast-forward on main, an issue tag never moves, main never deleted), written, and resolved
    /// again (a synced copy of another computer's entry may have won meanwhile). The first that fails stops the rest.
    /// </summary>
    public RefsResult UpdateRefs(IReadOnlyList<RefUpdate> updates)
    {
        foreach (var u in updates)
        {
            if (!RefNames.Valid(u.Name)) throw new RemoteException(400, "bad_ref_name", $"Not a ref name: {u.Name}.", new JsonObject { ["name"] = u.Name });
            if (u.New is { } n && !Holds(n)) throw new RemoteException(422, "object_missing", $"The remote lacks {n[..12]}….");
        }
        using (new Lock(this))
        {
            foreach (var u in updates)
            {
                var state = Resolve(EntriesOf(u.Name));
                if (state.Value != u.Old) throw Moved(u, state);
                if (u.Name == RefNames.Main && u.New == null) throw new RemoteException(422, "protected_ref", "The main branch is never deleted.", new JsonObject { ["name"] = u.Name });
                if (u.Name.StartsWith("refs/tags/issue/", StringComparison.Ordinal) && u.Old != null && u.New != null)
                    throw new RemoteException(422, "tag_immutable", "An issue tag cannot be moved.", new JsonObject { ["name"] = u.Name });
                if (u.Name == RefNames.Main && u.Old is { } o && u.New is { } nw && !Descends(nw, o))
                    throw new RemoteException(422, "not_fast_forward", "The main branch only moves forward.", new JsonObject { ["name"] = u.Name });
                var seq = state.Seq + 1;
                var file = EntryName(seq, u.New, _me);
                var path = Path.Combine(_dir, u.Name.Replace('/', Path.DirectorySeparatorChar), file);
                WriteOnce(path, EntryBytes(u.Name, seq, u.Old, u.New, _me, _now()));
                var after = Resolve(EntriesOf(u.Name));
                if (!after.Chain.Any(e => e.File == $"{u.Name}/{file}")) throw Moved(u, after);
            }
        }
        return Refs(null);
    }

    static RemoteException Moved(RefUpdate u, FolderRefState state)
    {
        var last = state.Chain.Count > 0 ? state.Chain[^1] : null;
        return new RemoteException(409, "ref_moved", $"{u.Name} has moved: fetch and pull first.", new JsonObject
        {
            ["code"] = "ref_moved",
            ["refs"] = new JsonArray(new JsonObject
            {
                ["name"] = u.Name, ["expected"] = u.Old, ["current"] = state.Value,
                ["updatedAt"] = last == null ? null : RemoteObjects.Stamp(last.At.UtcDateTime), ["updatedBy"] = last?.Author.ToJson(),
            }),
        });
    }

    public void PutViewer(string commit, string ifc, long size, string schema)
    {
        var record = new JsonObject
        {
            ["commit"] = commit, ["ifc"] = ifc, ["schema"] = schema, ["size"] = size,
            ["createdAt"] = RemoteObjects.Stamp(_now()), ["createdBy"] = _me.ToJson(),
        };
        WriteReplacing(Path.Combine(_dir, "viewers", commit + ".json"), Jcs.Canonicalize(record));
    }

    public void Dispose() { }
}
