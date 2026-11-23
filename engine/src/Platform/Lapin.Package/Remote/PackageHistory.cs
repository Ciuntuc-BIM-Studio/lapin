using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Package.Remote;

/// <summary>One local ref move (§6.2's <c>log</c>): which ref, from what to what, when, by whom, why.</summary>
public sealed record RefMove(string Name, string? Old, string? New, string At, RemoteAuthor By, string Why);

/// <summary>A remote of the project (§6.5): its name, <c>server</c> or <c>folder</c>, its URL and the project's id there. Never a token.</summary>
public sealed record RemoteEntry(string Name, string Kind, string Url, string Project);

/// <summary>A remote's refs as the last fetch or push left them (§6.2 <c>remotes.&lt;name&gt;</c>), with the refs' ETag and when.</summary>
public sealed class RemoteSeen
{
    public Dictionary<string, string> Refs { get; init; } = new(StringComparer.Ordinal);
    public string? Etag { get; set; }
    public string? FetchedAt { get; set; }
    /// <summary>On a folder remote, the detached ref entries (§7.3) this client has handled, by file name.</summary>
    public List<string> SeenDetached { get; init; } = [];
    public RemoteSeen Clone() => new() { Refs = new(Refs, StringComparer.Ordinal), Etag = Etag, FetchedAt = FetchedAt, SeenDetached = [.. SeenDetached] };
}

/// <summary>
/// A project's history inside its <c>.lapin</c> (docs/spec-lapin-remote.md §6): the entries under <c>history/</c> — the current
/// branch (<c>HEAD</c>), the local refs, the remotes' last-seen refs and the ref log (<c>refs.json</c>), the index of the objects
/// (<c>index.json</c>), the commits and tags (<c>commits/&lt;id&gt;</c>), the trees and the blobs no other place in the file holds
/// (<c>objects/&lt;id&gt;</c>), and the remotes (<c>remotes.json</c>, never a token). A blob is held once, in the first of: a live
/// part, <c>versions/blobs/</c>, <c>issues/blobs/</c>, <c>history/objects/</c> (§6.4). Commits, tags and trees are small and kept in
/// memory; blobs are read from the file they were read with (<see cref="From"/>), or held in memory once fetched, until the save
/// that writes them. Mutable: the dispatcher works on a <see cref="Clone"/> and keeps it once the file is written.
/// </summary>
public sealed class PackageHistory
{
    public const string Prefix = "history/";
    const string HeadPath = "history/HEAD", RefsPath = "history/refs.json", IndexPath = "history/index.json", RemotesPath = "history/remotes.json";
    const string CommitsPrefix = "history/commits/", ObjectsPrefix = "history/objects/";

    public string Head { get; set; } = RefNames.Main;
    public Dictionary<string, string> Refs { get; private init; } = new(StringComparer.Ordinal);
    public Dictionary<string, RemoteSeen> Seen { get; private init; } = new(StringComparer.Ordinal);
    public List<RefMove> Log { get; private init; } = [];
    public List<RemoteEntry> Remotes { get; private init; } = [];

    /// <summary>Commits and tags, by id: their canonical bytes.</summary>
    readonly Dictionary<string, byte[]> _commits = new(StringComparer.Ordinal);
    /// <summary>Trees, by id.</summary>
    readonly Dictionary<string, byte[]> _trees = new(StringComparer.Ordinal);
    /// <summary>Blobs fetched (or made) that the file does not hold yet: written with the next save.</summary>
    readonly Dictionary<string, byte[]> _pending = new(StringComparer.Ordinal);
    /// <summary>The objects the history knows to be absent (§6.3), with their kind and size when known.</summary>
    readonly Dictionary<string, (string Kind, long? Size)> _absent = new(StringComparer.Ordinal);
    /// <summary>Unknown top-level members of refs.json and remotes.json, kept (§6.1).</summary>
    JsonObject _refsExtra = [], _remotesExtra = [];
    /// <summary>The package the history was read with: where its blobs are.</summary>
    Func<Stream>? _source;

    /// <summary>Why the file's history could not be read (null: it was, or there is none); a save over that file is then refused.</summary>
    public string? Unreadable { get; private init; }

    /// <summary>What the check at opening found (§6.3, fsck): objects the index names that the file lacks, or the other way round. Repaired at the next save.</summary>
    public List<string> Problems { get; } = [];

    /// <summary>Whether there is anything to write: a project that never had a history writes no <c>history/</c>.</summary>
    public bool Any => Refs.Count > 0 || Remotes.Count > 0 || _commits.Count > 0 || Seen.Count > 0;

    public static PackageHistory Empty() => new();

    /// <summary>A copy to change: the dictionaries copied, the bytes shared (they never change).</summary>
    public PackageHistory Clone()
    {
        var c = new PackageHistory
        {
            Head = Head, Refs = new(Refs, StringComparer.Ordinal), Seen = Seen.ToDictionary(kv => kv.Key, kv => kv.Value.Clone(), StringComparer.Ordinal),
            Log = [.. Log], Remotes = [.. Remotes], _refsExtra = (JsonObject)_refsExtra.DeepClone(), _remotesExtra = (JsonObject)_remotesExtra.DeepClone(),
            _source = _source, Unreadable = Unreadable,
        };
        foreach (var (k, v) in _commits) c._commits[k] = v;
        foreach (var (k, v) in _trees) c._trees[k] = v;
        foreach (var (k, v) in _pending) c._pending[k] = v;
        foreach (var (k, v) in _absent) c._absent[k] = v;
        return c;
    }

    // ------------------------------------------------------------ reading

    /// <summary>The history in the package at <paramref name="path"/>; empty when it has none.</summary>
    public static PackageHistory Of(string path) => File.Exists(path) ? Read(() => File.OpenRead(path)) : Empty();

    public static PackageHistory Of(byte[] bytes) => Read(() => new MemoryStream(bytes, writable: false));

    /// <summary>The history in a package; empty when the package has none. One that cannot be read is never "none": a save would lose it.</summary>
    public static PackageHistory Read(Func<Stream> open)
    {
        try
        {
            using var input = open();
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            if (!zip.Entries.Any(e => e.FullName.StartsWith(Prefix, StringComparison.Ordinal))) return new PackageHistory { _source = open };
            var h = new PackageHistory { _source = open };
            h.ReadFrom(zip);
            return h;
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException or UnauthorizedAccessException or NotSupportedException or FormatException)
        {
            return new PackageHistory { _source = open, Unreadable = ex.Message };
        }
    }

    void ReadFrom(ZipArchive zip)
    {
        if (zip.GetEntry(HeadPath) != null)
        {
            var name = Encoding.ASCII.GetString(LapinPackage.Bytes(zip, HeadPath)).Trim();
            Head = RefNames.Valid(name) && RefNames.IsBranch(name) ? name : RefNames.Main;
        }
        if (zip.GetEntry(RefsPath) != null)
        {
            var o = JsonNode.Parse(LapinPackage.Bytes(zip, RefsPath)) as JsonObject ?? throw new InvalidDataException("history/refs.json is not an object.");
            if ((int?)o["v"] is { } v && v != 1) throw new InvalidDataException($"history/refs.json is of version {v}: made by a newer Lapin.");
            foreach (var (k, val) in o)
                switch (k)
                {
                    case "format" or "v": break;
                    case "refs": ReadRefs(val, Refs); break;
                    case "remotes":
                        if (val is JsonObject ro)
                            foreach (var (rn, rv) in ro)
                            {
                                var seen = new RemoteSeen { Etag = (string?)rv?["etag"], FetchedAt = (string?)rv?["fetchedAt"] };
                                ReadRefs(rv?["refs"], seen.Refs);
                                if (rv?["seenDetached"] is JsonArray sd) seen.SeenDetached.AddRange(sd.Select(x => (string?)x).OfType<string>());
                                Seen[rn] = seen;
                            }
                        break;
                    case "log":
                        if (val is JsonArray log)
                            foreach (var e in log.OfType<JsonObject>())
                                if ((string?)e["name"] is { } n)
                                    Log.Add(new RefMove(n, Id(e["old"]), Id(e["new"]), (string?)e["at"] ?? "", RemoteAuthor.From(e["by"]) ?? new RemoteAuthor("local:unknown", "?"), (string?)e["why"] ?? ""));
                        break;
                    default: _refsExtra[k] = val?.DeepClone(); break;
                }
        }
        if (zip.GetEntry(RemotesPath) != null)
        {
            var o = JsonNode.Parse(LapinPackage.Bytes(zip, RemotesPath)) as JsonObject ?? throw new InvalidDataException("history/remotes.json is not an object.");
            if ((int?)o["v"] is { } v && v != 1) throw new InvalidDataException($"history/remotes.json is of version {v}: made by a newer Lapin.");
            foreach (var (k, val) in o)
                if (k == "remotes" && val is JsonArray list)
                {
                    foreach (var r in list.OfType<JsonObject>())
                        if ((string?)r["name"] is { } n && (string?)r["kind"] is { } kind && (string?)r["url"] is { } url && (string?)r["project"] is { } project)
                            Remotes.Add(new RemoteEntry(n, kind, url, project));
                }
                else if (k is not ("format" or "v")) _remotesExtra[k] = val?.DeepClone();
        }
        var held = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in zip.Entries)
        {
            if (e.FullName.StartsWith(CommitsPrefix, StringComparison.Ordinal) && ObjectIds.Valid(e.FullName[CommitsPrefix.Length..]))
            {
                var id = e.FullName[CommitsPrefix.Length..];
                var bytes = LapinPackage.Bytes(zip, e.FullName);
                if (ObjectIds.Of(bytes) != id) { Problems.Add($"history/commits/{id[..12]}… is not what its name says: left out."); continue; }
                _commits[id] = bytes;
                held.Add(id);
            }
            else if (e.FullName.StartsWith(ObjectsPrefix, StringComparison.Ordinal) && ObjectIds.Valid(e.FullName[ObjectsPrefix.Length..]))
            {
                var id = e.FullName[ObjectsPrefix.Length..];
                held.Add(id);
                // Trees are small and read now; a blob stays in the file until it is asked for.
                if (e.Length <= RemoteObjects.MaxJsonObject)
                {
                    var bytes = LapinPackage.Bytes(zip, e.FullName);
                    if (RemoteObjects.KindOf(bytes) == ObjectKind.Tree && ObjectIds.Of(bytes) == id) _trees[id] = bytes;
                }
            }
        }
        if (zip.GetEntry(IndexPath) != null)
        {
            // The index is a convenience (§6.3): checked against the file, never trusted over it.
            var o = JsonNode.Parse(LapinPackage.Bytes(zip, IndexPath)) as JsonObject;
            foreach (var a in (o?["absent"] as JsonArray ?? []).OfType<JsonObject>())
                if (Id(a["id"]) is { } id) _absent[id] = ((string?)a["kind"] ?? "blob", (long?)a["size"]);
            var where = Where(zip);
            foreach (var x in (o?["objects"] as JsonArray ?? []).OfType<JsonObject>())
                if (Id(x["id"]) is { } id && !held.Contains(id) && !where.Contains(id))
                    Problems.Add($"The history's index names {(string?)x["kind"]} {id[..12]}…, which the file does not hold.");
        }
    }

    static void ReadRefs(JsonNode? n, Dictionary<string, string> into)
    {
        if (n is not JsonObject o) return;
        foreach (var (k, v) in o) if (RefNames.Valid(k) && Id(v) is { } id) into[k] = id;
    }

    static string? Id(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && ObjectIds.Valid(s) ? s : null;

    /// <summary>The blobs the package holds outside history/: its live parts, versions/blobs/, issues/blobs/.</summary>
    static HashSet<string> Where(ZipArchive zip)
    {
        var o = new HashSet<string>(StringComparer.Ordinal);
        try { foreach (var p in LapinPackage.ReadManifest(zip).Parts) o.Add(p.Sha256); }
        catch (InvalidDataException) { }
        foreach (var e in zip.Entries)
        {
            if (e.FullName.StartsWith(LapinPackage.BlobsPrefix, StringComparison.Ordinal)) o.Add(e.FullName[LapinPackage.BlobsPrefix.Length..]);
            else if (e.FullName.StartsWith(PackageIssues.Prefix, StringComparison.Ordinal)) o.Add(e.FullName[PackageIssues.Prefix.Length..]);
        }
        return o;
    }

    /// <summary>The same history, its blobs read from another package from now on (the file just written): what it held in memory is in that file now.</summary>
    public PackageHistory From(Func<Stream> source) => Read(source) is { Unreadable: null } h ? h : this;

    // ------------------------------------------------------------ objects

    public bool HasCommit(string id) => _commits.ContainsKey(id);
    public bool HasTree(string id) => _trees.ContainsKey(id);
    public byte[]? CommitBytes(string id) => _commits.GetValueOrDefault(id);
    public byte[]? TreeBytes(string id) => _trees.GetValueOrDefault(id);
    public CommitObject Commit(string id) => RemoteObjects.ReadCommit(_commits.GetValueOrDefault(id) ?? throw new InvalidDataException($"The history has no commit {id[..12]}…."), id);
    public TagObject Tag(string id) => RemoteObjects.ReadTag(_commits.GetValueOrDefault(id) ?? throw new InvalidDataException($"The history has no tag {id[..12]}…."), id);
    public PackageManifest Tree(string id) => RemoteObjects.ReadTree(_trees.GetValueOrDefault(id) ?? throw new InvalidDataException($"The history has no tree {id[..12]}…."));
    public ObjectKind? KindOfHeld(string id) => _trees.ContainsKey(id) ? ObjectKind.Tree : _commits.TryGetValue(id, out var b) ? RemoteObjects.KindOf(b) : null;

    /// <summary>
    /// An object received (fetched, or made by a commit): hashed, and a tree, commit or tag checked canonical. A commit or tag goes
    /// with the commits, a tree with the trees, a blob into memory until the next save (unless <paramref name="inFile"/>: the save
    /// that follows writes it as a live part).
    /// </summary>
    public void Add(string id, byte[] bytes, ObjectKind kind, bool inFile = false)
    {
        if (ObjectIds.Of(bytes) != id) throw new InvalidDataException($"The bytes do not hash to {id}.");
        switch (kind)
        {
            case ObjectKind.Commit or ObjectKind.Tag:
                if (!Jcs.IsCanonical(bytes)) throw new InvalidDataException($"The {kind.ToString().ToLowerInvariant()} {id[..12]}… is not canonical JSON.");
                _commits[id] = bytes;
                break;
            case ObjectKind.Tree:
                RemoteObjects.ReadTree(bytes);
                _trees[id] = bytes;
                break;
            default:
                if (!inFile) _pending[id] = bytes;
                break;
        }
        _absent.Remove(id);
    }

    /// <summary>The blobs, as a reader over the package they live in and those held in memory: open it for as long as several are read.</summary>
    public BlobReader Blobs() => new(this);

    /// <summary>Reads blobs by the location rule (§6.4), each hash verified; one zip opened for all.</summary>
    public sealed class BlobReader : IDisposable
    {
        readonly PackageHistory _h;
        Stream? _input;
        ZipArchive? _zip;
        Dictionary<string, string>? _live;
        internal BlobReader(PackageHistory h) => _h = h;

        ZipArchive? Zip()
        {
            if (_zip != null || _h._source == null) return _zip;
            try
            {
                _input = _h._source();
                _zip = new ZipArchive(_input, ZipArchiveMode.Read, leaveOpen: true);
                _live = new Dictionary<string, string>(StringComparer.Ordinal);
                try { foreach (var p in LapinPackage.ReadManifest(_zip).Parts) _live.TryAdd(p.Sha256, p.Path); }
                catch (InvalidDataException) { }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { _zip = null; }
            return _zip;
        }

        /// <summary>The blob's bytes, or null when neither memory nor the file holds it (or what it holds is not what the id says).</summary>
        public byte[]? Get(string id)
        {
            if (_h._pending.TryGetValue(id, out var mem)) return mem;
            if (id == ObjectIds.Empty) return [];
            var zip = Zip();
            if (zip == null) return null;
            foreach (var name in Names(id))
            {
                if (name == null || zip.GetEntry(name) == null) continue;
                try
                {
                    var bytes = LapinPackage.Bytes(zip, name);
                    if (ObjectIds.Of(bytes) == id) return bytes;
                }
                catch (InvalidDataException) { }
            }
            return null;
        }

        IEnumerable<string?> Names(string id)
        {
            yield return _live!.GetValueOrDefault(id);
            yield return LapinPackage.BlobsPrefix + id;
            yield return PackageIssues.Prefix + id;
            yield return ObjectsPrefix + id;
        }

        public bool Has(string id) => _h._pending.ContainsKey(id) || Get(id) != null;

        /// <summary>Whether memory or the file holds the blob, by name only (its bytes are verified when it is read).</summary>
        public bool Contains(string id)
        {
            if (_h._pending.ContainsKey(id) || id == ObjectIds.Empty) return true;
            var zip = Zip();
            return zip != null && Names(id).Any(n => n != null && zip.GetEntry(n) != null);
        }

        public void Dispose()
        {
            _zip?.Dispose();
            _input?.Dispose();
        }
    }

    // ------------------------------------------------------------ refs and commits

    /// <summary>The current branch's value; null before the first commit.</summary>
    public string? HeadCommit => Refs.GetValueOrDefault(Head);

    public void Move(string name, string? value, RemoteAuthor by, string why, DateTime now)
    {
        var old = Refs.GetValueOrDefault(name);
        if (value == null) Refs.Remove(name); else Refs[name] = value;
        Log.Add(new RefMove(name, old, value, RemoteObjects.Stamp(now), by, why));
    }

    /// <summary>Whether two trees name the same parts (path and hash), whatever build wrote them: the project's state is the same.</summary>
    public static bool SameParts(PackageManifest a, PackageManifest b) =>
        a.Parts.Count == b.Parts.Count && a.Parts.Zip(b.Parts).All(x => x.First.Path == x.Second.Path && x.First.Sha256 == x.Second.Sha256);

    /// <summary>Whether the live project differs from the head commit's tree (§8.4: "uncommitted changes"); a project without commits has changes when it holds anything.</summary>
    public bool Uncommitted(PackageManifest live) => HeadCommit is not { } head || !_commits.ContainsKey(head) || !_trees.ContainsKey(Commit(head).Tree) || !SameParts(Tree(Commit(head).Tree), live);

    /// <summary>
    /// A commit of the project as <paramref name="manifest"/> describes it, on the current branch: the tree and the commit added,
    /// the branch moved (logged <c>commit</c>). Its blobs are the live parts the save that follows writes. Refused when nothing changed.
    /// </summary>
    public CommitObject CommitLive(PackageManifest manifest, RemoteAuthor author, string message, int entities, DateTime now)
    {
        var tree = RemoteObjects.Tree(manifest);
        var treeId = ObjectIds.Of(tree);
        var parent = HeadCommit;
        if (parent != null && _commits.ContainsKey(parent) && _trees.TryGetValue(Commit(parent).Tree, out var pt) && SameParts(RemoteObjects.ReadTree(pt), manifest))
            throw new InvalidDataException("Nothing changed since the last commit.");
        var bytes = RemoteObjects.Commit(treeId, parent is null ? [] : [parent], author, now, message.Trim(), manifest.App, manifest.Product, entities);
        var id = ObjectIds.Of(bytes);
        _trees[treeId] = tree;
        _commits[id] = bytes;
        Move(Head, id, author, "commit", now);
        return RemoteObjects.ReadCommit(bytes, id);
    }

    /// <summary>Whether <paramref name="ancestor"/> is <paramref name="of"/> or one of its ancestors, walking the commits held.</summary>
    public bool IsAncestor(string ancestor, string of)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(of);
        while (stack.Count > 0)
        {
            var c = stack.Pop();
            if (c == ancestor) return true;
            if (!seen.Add(c) || !_commits.TryGetValue(c, out var bytes)) continue;
            foreach (var p in RemoteObjects.ReadCommit(bytes, c).Parents) stack.Push(p);
        }
        return false;
    }

    /// <summary>The commits of a branch, newest first, along every parent held (at most <paramref name="limit"/>).</summary>
    public List<CommitObject> Walk(string from, int limit = 500)
    {
        var o = new List<CommitObject>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new PriorityQueue<string, string>(Comparer<string>.Create((a, b) => string.CompareOrdinal(b, a)));
        queue.Enqueue(from, "");
        while (queue.Count > 0 && o.Count < limit)
        {
            var id = queue.Dequeue();
            if (!seen.Add(id) || !_commits.TryGetValue(id, out var bytes)) continue;
            var c = RemoteObjects.ReadCommit(bytes, id);
            o.Add(c);
            foreach (var p in c.Parents) if (_commits.TryGetValue(p, out var pb)) queue.Enqueue(p, RemoteObjects.ReadCommit(pb, p).At);
        }
        return o;
    }

    /// <summary>The project a tree describes, its blobs read by the location rule; refused when one is absent (the commit is incomplete).</summary>
    public PackageContent Content(string treeId)
    {
        var manifest = Tree(treeId);
        using var blobs = Blobs();
        var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var p in manifest.Parts)
            if (!bytes.ContainsKey(p.Sha256))
                bytes[p.Sha256] = blobs.Get(p.Sha256) ?? throw new InvalidDataException($"The commit is incomplete: {p.Path} is not in the file. Fetch it from the remote first.");
        return LapinPackage.Content(manifest, p => bytes[p.Sha256]);
    }

    /// <summary>A package of exactly a tree's parts (§12.1): its manifest the tree, byte for byte.</summary>
    public byte[] Package(string treeId)
    {
        var tree = _trees.GetValueOrDefault(treeId) ?? throw new InvalidDataException($"The history has no tree {treeId[..12]}….");
        var manifest = RemoteObjects.ReadTree(tree);
        using var blobs = Blobs();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var s = zip.CreateEntry("manifest.json", CompressionLevel.Optimal).Open()) s.Write(tree);
            foreach (var p in manifest.Parts)
            {
                var bytes = blobs.Get(p.Sha256) ?? throw new InvalidDataException($"The commit is incomplete: {p.Path} is not in the file.");
                using var s = zip.CreateEntry(p.Path, CompressionLevel.Optimal).Open();
                s.Write(bytes);
            }
        }
        return ms.ToArray();
    }

    // ------------------------------------------------------------ closure and writing

    /// <summary>The roots whose closure the file keeps: every local ref, every kept log entry's old and new, and the remotes' last-seen refs (so a fetch survives a save before its pull).</summary>
    IEnumerable<string> Roots() =>
        Refs.Values.Concat(Log.SelectMany(l => new[] { l.Old, l.New }).OfType<string>()).Concat(Seen.Values.SelectMany(s => s.Refs.Values));

    /// <summary>Every object reachable from <paramref name="roots"/> through what the history holds, with its kind and (for a part or a file) its size.</summary>
    public Dictionary<string, (ObjectKind Kind, long? Size)> Closure(IEnumerable<string> roots, Func<string, bool>? stop = null)
    {
        var o = new Dictionary<string, (ObjectKind, long?)>(StringComparer.Ordinal);
        var stack = new Stack<string>(roots);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (o.ContainsKey(id) || stop?.Invoke(id) == true) continue;
            if (!_commits.TryGetValue(id, out var bytes)) { o[id] = (ObjectKind.Commit, null); continue; }
            switch (RemoteObjects.KindOf(bytes))
            {
                case ObjectKind.Tag:
                {
                    var t = RemoteObjects.ReadTag(bytes, id);
                    o[id] = (ObjectKind.Tag, bytes.Length);
                    foreach (var f in t.Files) o.TryAdd(f.Sha256, (ObjectKind.Blob, f.Size));
                    stack.Push(t.Commit);
                    break;
                }
                default:
                {
                    var c = RemoteObjects.ReadCommit(bytes, id);
                    o[id] = (ObjectKind.Commit, bytes.Length);
                    if (!o.ContainsKey(c.Tree))
                    {
                        o[c.Tree] = (ObjectKind.Tree, _trees.TryGetValue(c.Tree, out var tb) ? tb.Length : null);
                        if (tb != null) foreach (var p in RemoteObjects.ReadTree(tb).Parts) o.TryAdd(p.Sha256, (ObjectKind.Blob, p.Size));
                    }
                    foreach (var p in c.Parents) stack.Push(p);
                    break;
                }
            }
        }
        return o;
    }

    /// <summary>
    /// The history into a package being written (§6): HEAD, refs.json, remotes.json, every reachable commit and tag, every
    /// reachable tree, and every reachable blob that none of the first three places holds (<paramref name="live"/>: the live
    /// parts' hashes; <paramref name="elsewhere"/>: those written under versions/blobs/ and issues/blobs/), each once; what it
    /// cannot find is listed as absent. Gives back why it could not be read (nothing written then), or null.
    /// </summary>
    public string? WriteTo(Action<string, byte[]> entry, IReadOnlySet<string> live, IReadOnlySet<string> elsewhere)
    {
        if (Unreadable != null) return Unreadable;
        if (!Any) return null;
        var reachable = Closure(Roots());
        var objects = new SortedDictionary<string, (string Kind, long Size)>(StringComparer.Ordinal);
        var absent = new SortedDictionary<string, (string Kind, long? Size)>(StringComparer.Ordinal);
        var written = new List<(string Name, byte[] Bytes)>();
        using (var blobs = Blobs())
        {
            foreach (var (id, (kind, size)) in reachable)
            {
                var k = kind.ToString().ToLowerInvariant();
                switch (kind)
                {
                    case ObjectKind.Commit or ObjectKind.Tag:
                        if (_commits.TryGetValue(id, out var cb)) { written.Add((CommitsPrefix + id, cb)); objects[id] = (k, cb.Length); }
                        else absent[id] = (k, size);
                        break;
                    case ObjectKind.Tree:
                        if (_trees.TryGetValue(id, out var tb)) { written.Add((ObjectsPrefix + id, tb)); objects[id] = (k, tb.Length); }
                        else absent[id] = (k, size);
                        break;
                    default:
                        if (live.Contains(id) || elsewhere.Contains(id)) { objects[id] = (k, size ?? 0); break; }
                        if (blobs.Get(id) is { } bb) { written.Add((ObjectsPrefix + id, bb)); objects[id] = (k, bb.LongLength); }
                        else absent[id] = (k, size);
                        break;
                }
            }
        }
        entry(HeadPath, Encoding.ASCII.GetBytes(Head));
        entry(RefsPath, Indented(RefsJson()));
        entry(RemotesPath, Indented(RemotesJson()));
        foreach (var (name, bytes) in written) entry(name, bytes);
        entry(IndexPath, Indented(new JsonObject
        {
            ["format"] = "lapin-history-index", ["v"] = 1,
            ["objects"] = new JsonArray(objects.Select(kv => (JsonNode)new JsonObject { ["id"] = kv.Key, ["kind"] = kv.Value.Kind, ["size"] = kv.Value.Size }).ToArray()),
            ["absent"] = new JsonArray(absent.Select(kv => (JsonNode)Absent(kv.Key, kv.Value.Kind, kv.Value.Size)).ToArray()),
        }));
        return null;
    }

    static JsonObject Absent(string id, string kind, long? size)
    {
        var o = new JsonObject { ["id"] = id, ["kind"] = kind };
        if (size is { } s) o["size"] = s;
        return o;
    }

    static byte[] Indented(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

    static JsonObject RefsObject(Dictionary<string, string> refs) => new(refs.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)));

    JsonObject RefsJson()
    {
        var o = new JsonObject { ["format"] = "lapin-history-refs", ["v"] = 1, ["refs"] = RefsObject(Refs) };
        var remotes = new JsonObject();
        foreach (var (name, s) in Seen.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var r = new JsonObject { ["refs"] = RefsObject(s.Refs), ["etag"] = s.Etag, ["fetchedAt"] = s.FetchedAt };
            if (s.SeenDetached.Count > 0) r["seenDetached"] = new JsonArray(s.SeenDetached.Select(x => (JsonNode)x).ToArray());
            remotes[name] = r;
        }
        o["remotes"] = remotes;
        o["log"] = new JsonArray(Log.Select(l => (JsonNode)new JsonObject
        {
            ["name"] = l.Name, ["old"] = l.Old, ["new"] = l.New, ["at"] = l.At, ["by"] = l.By.ToJson(), ["why"] = l.Why,
        }).ToArray());
        foreach (var (k, v) in _refsExtra) o[k] = v?.DeepClone();
        return o;
    }

    JsonObject RemotesJson()
    {
        var o = new JsonObject
        {
            ["format"] = "lapin-history-remotes", ["v"] = 1,
            ["remotes"] = new JsonArray(Remotes.Select(r => (JsonNode)new JsonObject { ["name"] = r.Name, ["kind"] = r.Kind, ["url"] = r.Url, ["project"] = r.Project }).ToArray()),
        };
        foreach (var (k, v) in _remotesExtra) o[k] = v?.DeepClone();
        return o;
    }

    /// <summary>The history's entries copied as the file had them (it could not be read): never dropped by a save.</summary>
    public string? CopyRawTo(Action<string, byte[]> entry)
    {
        if (_source == null) return "the file the history was in is gone";
        var entries = new List<(string, byte[])>();
        try
        {
            using var input = _source();
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in zip.Entries)
                if (e.FullName.StartsWith(Prefix, StringComparison.Ordinal) && !e.FullName.EndsWith('/') && seen.Add(e.FullName))
                    entries.Add((e.FullName, LapinPackage.Bytes(zip, e.FullName)));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { return ex.Message; }
        foreach (var (n, b) in entries) entry(n, b);
        return null;
    }

    /// <summary>The ahead and behind counts of the current branch against a remote's last-seen value of the same name (§5.4).</summary>
    public (int Ahead, int Behind) AheadBehind(string remote)
    {
        var local = HeadCommit;
        var theirs = Seen.GetValueOrDefault(remote)?.Refs.GetValueOrDefault(Head);
        if (local == theirs) return (0, 0);
        var mine = local == null ? new HashSet<string>() : Walk(local, 100_000).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var other = theirs == null ? new HashSet<string>() : Walk(theirs, 100_000).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        // A remote commit not held yet counts as one behind (its ancestors are unknown until a fetch).
        var behind = theirs != null && !_commits.ContainsKey(theirs) ? 1 : other.Count(c => !mine.Contains(c));
        return (mine.Count(c => !other.Contains(c)), behind);
    }
}
