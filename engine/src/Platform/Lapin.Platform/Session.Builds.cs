using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What a session may keep in memory for the geometry it builds (decisions 425, 426): the earlier builds of elements, for undo and redo,
/// and the geometry of storeys nobody is working on, on a model large enough to matter. Sizes are estimated bytes of what a build holds
/// (<see cref="Session"/>'s <c>Estimate</c>, measured against the heap), so they mean the same on every platform.
/// </summary>
/// <param name="MemoBytes">Earlier builds kept for undo and redo, in all; the oldest go first. 0: none.</param>
/// <param name="MemoFromEntities">Earlier builds are kept (and stamped when built) only in a model of this many entities or more: keeping them costs a share of every edit (the heap they hold makes collections dearer, about 10 to 40 ms on a model of a thousand entities, which an edit there does not earn back), and pays where an edit takes tens of milliseconds or seconds and an undo repeats it.</param>
/// <param name="ReleaseAboveBytes">Past this many bytes of builds, the geometry of the storeys last worked on least is let go (to three quarters of it), and built again when asked for. <see cref="long.MaxValue"/>: never.</param>
/// <param name="KeepSeamPlanes">Each solid's faces for the seams kept as long as the solid stands (as before), not only while a diff is made.</param>
/// <param name="CacheCutters">The cutters of a wall worked out once for all the walls built at a revision (as before: each wall by itself).</param>
public sealed record SessionTuning(long MemoBytes, long ReleaseAboveBytes, bool KeepSeamPlanes, bool CacheCutters, int MemoFromEntities = 5000)
{
    /// <summary>A desktop: the builds of a model of 4,000 walls and 14,000 elements hold about 700 MB of heap by this measure.</summary>
    public static readonly SessionTuning Desktop = new(384L << 20, 640L << 20, false, true);
    /// <summary>The browser's engine has less memory and one thread: a quarter of it.</summary>
    public static readonly SessionTuning Browser = new(96L << 20, 160L << 20, false, true);
    /// <summary>The session as it was before: nothing kept, nothing let go. For the tests that check the rest changes nothing.</summary>
    public static readonly SessionTuning Reference = new(0, long.MaxValue, true, false);

    public static SessionTuning Default => OperatingSystem.IsBrowser() ? Browser : Desktop;
}

public sealed partial class Session
{
    SessionTuning _tuning = SessionTuning.Default;

    /// <summary>What the session keeps of its builds; <see cref="SessionTuning.Default"/> unless a test says otherwise.</summary>
    public SessionTuning Tuning
    {
        get => _tuning;
        set
        {
            _tuning = value;
            _catalog.CacheCutters = value.CacheCutters;
            if (value.MemoBytes <= 0) ForgetEarlier();
        }
    }

    /// <summary>How many builds were made from scratch (not taken from the earlier ones): what a test counts to see an undo build nothing.</summary>
    public long BuildsMade => Interlocked.Read(ref _buildsMade);
    long _buildsMade;

    /// <summary>How many builds have let their geometry go now.</summary>
    public int ReleasedBuilds => _releasedCount;

    /// <summary>Estimated bytes of the builds the session holds now, and of the earlier ones it keeps for undo.</summary>
    public (long Live, long Earlier) HeldBytes => (_liveBytes, _earlierBytes);

    /// <summary>An undo or a redo is being diffed: only then are the earlier builds looked at (a new edit rarely rebuilds an element as it was, and looking costs every build).</summary>
    volatile bool _undoing;

    Diff DiffAfterUndoing(Transaction t)
    {
        _undoing = true;
        try { return DiffAfter(t); }
        finally { _undoing = false; }
    }

    /// <summary>
    /// The faces kept for the seams, after the items of one diff: only those the diff looked at stay (the next edit is next to this one), so
    /// they are as many as the neighbourhood, not as the model (a table for every solid was a part of the heap). A whole model's
    /// (a snapshot) goes entirely. <see cref="SessionTuning.KeepSeamPlanes"/> keeps them all, as before.
    /// </summary>
    void TrimSeamPlanes(bool whole)
    {
        if (_tuning.KeepSeamPlanes) return;
        if (whole) _planes.Clear();
        else foreach (var s in _planes.Keys) if (!_planesUsed.ContainsKey(s)) _planes.TryRemove(s, out _);
        _planesUsed.Clear();
    }

    // ------------------------------------------------------------ what a build holds

    /// <summary>Estimated bytes of everything the session keeps of builds now (earlier ones for undo not included).</summary>
    long _liveBytes;
    /// <summary>How many builds have let their geometry go.</summary>
    int _releasedCount;

    /// <summary>What a build holds, as the geometry and render arrays and the read set it keeps.</summary>
    static long Estimate(ElementGeometry? g, RenderItem? item, HashSet<string> reads)
    {
        // Measured against the heap (docs/experiment-editing-speed.md): a read is its set entry and the key's string (the keys are made
        // for each build), a face its record, its loops and its share of the vertices.
        long bytes = 512 + reads.Count * 96L;
        foreach (var p in g?.Parts ?? [])
        {
            bytes += 200;
            if (p.HasSolid) bytes += p.Solid.Faces.Length * 220L + p.Solid.Vertices.Length * 24L;
            if (p.Mesh is { } mesh) bytes += mesh.Xyz.Length * 8L + mesh.Tris.Length * 4L;
        }
        foreach (var i in g?.Plan ?? []) bytes += 120 + i.Points.Count * 16L;
        if (item != null)
        {
            bytes += (item.Positions.Length + item.Normals.Length + item.Edges.Length + item.Indices.Length) * 4L;
            foreach (var i in item.Plan) bytes += 120 + i.Points.Length * 8L;
            foreach (var p in item.Parts ?? [])
            {
                bytes += 200 + (p.Positions.Length + p.Normals.Length + p.Edges.Length + p.Indices.Length) * 4L;
                foreach (var l in p.Lods ?? []) bytes += 100 + (l.Positions.Length + l.Normals.Length + l.Edges.Length + l.Indices.Length) * 4L;
            }
        }
        return bytes;
    }

    // ------------------------------------------------------------ earlier builds, for undo and redo

    /// <summary>
    /// An element's builds from before, newest first, with a stamp of everything each read (<see cref="StampOf"/>): an edit that is undone
    /// (or done again) puts back the very entities and relationships it took out, so the stamp of an earlier build holds again and the
    /// element is not built a second time. A build is only taken when its stamp is the same now, which says every entity and relationship
    /// it read is the object it read.
    /// </summary>
    readonly LinkedList<(string Id, Built Built)> _earlier = new();
    readonly Dictionary<string, List<LinkedListNode<(string Id, Built Built)>>> _earlierOf = [];
    long _earlierBytes;
    const int EarlierPerElement = 3;

    /// <summary>A number that is the same exactly when everything in <paramref name="reads"/> is as it was (null: it stands for something that cannot be told, a cell derived from other elements' places).</summary>
    /// <summary>Whether builds are stamped now and kept when replaced (<see cref="SessionTuning.MemoFromEntities"/>).</summary>
    bool KeepsEarlier => _tuning.MemoBytes > 0 && _store.EntityCount >= _tuning.MemoFromEntities;

    (ulong A, ulong B)? StampOf(HashSet<string> reads)
    {
        ulong a = 0, b = 0;
        foreach (var key in reads)
        {
            (ulong A, ulong B) v;
            if (key.StartsWith("pcell:", StringComparison.Ordinal))
            {
                // A host's build looks at the runs round it by their built places: told only when there is none anywhere.
                if (ProvisionRunsExist()) return null;
                v = (11, 11);
            }
            else if (_store.ReadStamp(key) is { } stamp) v = stamp;
            else return null;
            // The key goes in by a hash of its own text, as a 64-bit value from the text itself, not a 32-bit string hash.
            var k = KeyHash(key);
            a += ModelStore.Mix(v.A * 0x9E3779B97F4A7C15UL ^ k);
            b += ModelStore.Mix2(v.B + k * 0xC2B2AE3D27D4EB4FUL);
        }
        return (a, b);
    }

    static ulong KeyHash(string key)
    {
        var h = 14695981039346656037UL;
        foreach (var c in key) h = (h ^ c) * 1099511628211UL;
        return ModelStore.Mix(h);
    }

    string[]? _provisionClasses;

    bool ProvisionRunsExist()
    {
        _provisionClasses ??= [.. _catalog.CutDefaults.Keys.Where(_catalog.CutsByProvisions)];
        foreach (var cls in _provisionClasses) if (_store.HasClass(cls)) return true;
        return false;
    }

    /// <summary>An earlier build of <paramref name="e"/> whose reads are all as they were; read only, safe while builds run on every core.</summary>
    Built? Remembered(Entity e)
    {
        if (!_earlierOf.TryGetValue(e.Id, out var list)) return null;
        foreach (var node in list)
        {
            var b = node.Value.Built;
            if (b.EntityVersion != e.Version || b.Stamp is not { } was || StampOf(b.Reads) != was) continue;
            // (What the render item says of its storey is read outside the build's reads.)
            if (b.Storey != (b.HadItem ? Spatial.ShownOn(_store, e)?.Id : null)) continue;
            return b;
        }
        return null;
    }

    /// <summary>An element's build, about to be replaced: kept for a time, if it can be found again.</summary>
    void KeepBuild(string id, Built old)
    {
        if (!KeepsEarlier || old.Stamp == null || old.Released || old.Pinned || old.Bytes > _tuning.MemoBytes / 4) return;
        var node = _earlier.AddFirst((id, old));
        if (!_earlierOf.TryGetValue(id, out var list)) _earlierOf[id] = list = [];
        list.Insert(0, node);
        _earlierBytes += old.Bytes;
        while (list.Count > EarlierPerElement) DropEarlier(list[^1]);
        while (_earlierBytes > _tuning.MemoBytes && _earlier.Last is { } last) DropEarlier(last);
    }

    void DropEarlier(LinkedListNode<(string Id, Built Built)> node)
    {
        _earlier.Remove(node);
        _earlierBytes -= node.Value.Built.Bytes;
        if (_earlierOf.TryGetValue(node.Value.Id, out var list)) { list.Remove(node); if (list.Count == 0) _earlierOf.Remove(node.Value.Id); }
    }

    /// <summary>A build taken again is the element's current one: it is no longer one of the earlier.</summary>
    void TookBuild(string id, Built built)
    {
        if (!_earlierOf.TryGetValue(id, out var list)) return;
        var node = list.FirstOrDefault(n => ReferenceEquals(n.Value.Built, built));
        if (node != null) DropEarlier(node);
    }

    void ForgetEarlier()
    {
        _earlier.Clear();
        _earlierOf.Clear();
        _earlierBytes = 0;
    }

    // ------------------------------------------------------------ storeys nobody is working on

    /// <summary>When a storey was last worked on: its elements built, asked for, drawn.</summary>
    readonly Dictionary<string, long> _storeyUse = [];
    long _useClock;
    const int HotStoreys = 3;

    void Touch(string? storey)
    {
        if (storey != null) _storeyUse[storey] = ++_useClock;
    }

    /// <summary>
    /// Past <see cref="SessionTuning.ReleaseAboveBytes"/> of builds, the geometry and render arrays of the storeys worked on least are
    /// let go, down to three quarters of it, the last few storeys worked on kept. What is kept of them is what the rest of the session needs
    /// without them: the box, what it read, whether it has geometry. Anything that asks for them builds them again
    /// (<see cref="Rehydrate"/>), identical, as the model it was built from has not changed since or it would have been built again.
    /// </summary>
    void MaintainMemory()
    {
        if (_liveBytes <= _tuning.ReleaseAboveBytes) return;
        var hot = _storeyUse.OrderByDescending(kv => kv.Value).Take(HotStoreys).Select(kv => kv.Key).ToHashSet();
        var byStorey = new Dictionary<string, List<string>>();
        foreach (var (id, b) in _built)
            if (!b.Released && !b.Pinned && b.Storey is { } s && !hot.Contains(s) && (b.Geometry != null || b.Item != null)) (byStorey.TryGetValue(s, out var l) ? l : byStorey[s] = []).Add(id);
        var target = _tuning.ReleaseAboveBytes / 4 * 3;
        foreach (var (storey, ids) in byStorey.OrderBy(kv => _storeyUse.GetValueOrDefault(kv.Key)).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (_liveBytes <= target) break;
            foreach (var id in ids)
            {
                var b = _built[id];
                _liveBytes -= b.Bytes;
                _built[id] = b with { Geometry = null, Item = null, Released = true, Bytes = 0 };
                _releasedCount++;
                foreach (var p in b.Geometry?.Parts ?? []) _planes.TryRemove(p.Solid, out _);
            }
        }
    }

    /// <summary>The builds of <paramref name="ids"/> that let their geometry go, made again (on every core), as the session's own.</summary>
    void Rehydrate(IEnumerable<string> ids)
    {
        if (_releasedCount == 0) return;
        var es = ids.Distinct().Where(id => _built.TryGetValue(id, out var b) && b.Released).Select(_store.Get).OfType<Entity>().ToList();
        if (es.Count == 0) return;
        foreach (var made in BuildMany(es)) Touch(made?.Storey);
    }

    /// <summary>The elements an item's seams look at, and itself: their geometry must be there before the items are made on every core.</summary>
    void RehydrateAround(IReadOnlyList<string> ids, BoxGrid grid)
    {
        if (_releasedCount == 0) return;
        var need = new HashSet<string>();
        foreach (var id in ids)
        {
            need.Add(id);
            if (_built.TryGetValue(id, out var b) && b.Bounds is { } box) foreach (var (nid, _) in grid.Near([box])) need.Add(nid);
        }
        Rehydrate(need);
    }
}
