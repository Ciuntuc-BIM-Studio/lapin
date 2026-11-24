using System.Text.Json.Nodes;

namespace Lapin.Package.Remote;

/// <summary>What a push did (§8.1): the refs moved, what was uploaded, or why it stopped (the remote moved: pull first).</summary>
public sealed record PushOutcome(bool Pushed, IReadOnlyList<string> Uploaded, IReadOnlyList<string> Asked, string? Moved = null, string? Current = null, RemoteAuthor? MovedBy = null, string? MovedAt = null, bool Behind = false)
{
    public List<string> Notes { get; init; } = [];
}

/// <summary>What a fetch brought (§8.2): the objects downloaded, by kind, and whether the refs had changed at all.</summary>
public sealed record FetchOutcome(bool Unchanged, int Commits, int Trees, int Blobs, int Tags)
{
    public List<string> Notes { get; init; } = [];
}

/// <summary>Where the current branch stands against the remote's after a fetch (§8.4).</summary>
public enum PullKind { UpToDate, FastForward, Ahead, Diverged }

/// <summary>
/// The client's algorithms of lapin-remote 1.0 (docs/spec-lapin-remote.md §8) over a <see cref="PackageHistory"/> and an
/// <see cref="IRemote"/>: push (negotiate what is missing, upload only that, compare-and-swap the refs), fetch (the refs with their
/// ETag, the commits walked back to what is held, the objects lacked), clone, and where a pull stands. They change the history
/// they are given (a clone of the open one) and never the model. Asynchronous, as <see cref="IRemote"/> is: the same code on the desktop
/// and in the browser's engine.
/// </summary>
public static class RemoteOps
{
    static RemoteSeen SeenOf(PackageHistory h, string remote)
    {
        if (!h.Seen.TryGetValue(remote, out var s)) h.Seen[remote] = s = new RemoteSeen();
        return s;
    }

    static void TakeRefs(RemoteSeen seen, RefsResult refs, DateTime now)
    {
        if (!refs.NotModified)
        {
            seen.Refs.Clear();
            foreach (var r in refs.Refs) seen.Refs[r.Name] = r.Target;
        }
        if (refs.Etag != null) seen.Etag = refs.Etag;
        seen.FetchedAt = RemoteObjects.Stamp(now);
    }

    /// <summary>The remote's refs read (with the ETag of the last time) into its last-seen refs; whether they had changed.</summary>
    public static async Task<bool> FetchRefsAsync(IRemote remote, PackageHistory h, string name, DateTime now)
    {
        var seen = SeenOf(h, name);
        var refs = await remote.RefsAsync(seen.Refs.Count > 0 ? seen.Etag : null);
        TakeRefs(seen, refs, now);
        return !refs.NotModified;
    }

    /// <summary>
    /// The objects to send for <paramref name="values"/> (§8.1, step 2): the commits reachable from them that are not reachable from
    /// any of the remote's last-seen values, each with its tree and the tree's parts, less the parts of the boundary commits' trees
    /// (the remote holds them), and every tag with its files. In order: each commit, its tree, its parts; then each tag and its files.
    /// </summary>
    public static List<(string Id, ObjectKind Kind)> ToSend(PackageHistory h, IEnumerable<string> values, IEnumerable<string> remoteValues)
    {
        var known = h.Closure(remoteValues.Where(v => h.HasCommit(v))).Where(kv => kv.Value.Kind is ObjectKind.Commit).Select(kv => kv.Key).ToHashSet(StringComparer.Ordinal);
        var o = new List<(string, ObjectKind)>();
        var added = new HashSet<string>(StringComparer.Ordinal);
        void Add(string id, ObjectKind k) { if (added.Add(id)) o.Add((id, k)); }
        var tags = new List<TagObject>();
        var commits = new List<CommitObject>();
        var starts = new List<string>();
        foreach (var v in values)
        {
            if (h.KindOfHeld(v) == ObjectKind.Tag) { var t = h.Tag(v); tags.Add(t); starts.Add(t.Commit); }
            else starts.Add(v);
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        // Newest first: a branch's head, then its parents, as the server walks them.
        foreach (var start in starts)
        {
            var q = new Queue<string>([start]);
            while (q.Count > 0)
            {
                var id = q.Dequeue();
                if (known.Contains(id) || !seen.Add(id)) continue;
                if (!h.HasCommit(id)) throw new InvalidDataException($"The history lacks commit {id[..12]}…: it cannot be pushed.");
                var c = h.Commit(id);
                commits.Add(c);
                foreach (var p in c.Parents) q.Enqueue(p);
            }
        }
        var boundary = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in commits)
            foreach (var p in c.Parents.Where(known.Contains))
                if (h.HasCommit(p) && h.HasTree(h.Commit(p).Tree))
                    foreach (var part in h.Tree(h.Commit(p).Tree).Parts) boundary.Add(part.Sha256);
        foreach (var c in commits)
        {
            Add(c.Id, ObjectKind.Commit);
            Add(c.Tree, ObjectKind.Tree);
            if (!h.HasTree(c.Tree)) throw new InvalidDataException($"The history lacks the tree of commit {c.Id[..12]}….");
            foreach (var part in h.Tree(c.Tree).Parts) if (!boundary.Contains(part.Sha256)) Add(part.Sha256, ObjectKind.Blob);
        }
        foreach (var t in tags)
        {
            Add(t.Id, ObjectKind.Tag);
            foreach (var f in t.Files) Add(f.Sha256, ObjectKind.Blob);
        }
        return o;
    }

    /// <summary>
    /// A push of the local refs named (§8.1): the remote's refs fetched (unless <paramref name="fetchRefs"/> is false); stopped when a
    /// branch's remote value is not an ancestor of the local one (pull first); the closure to send asked against objects/missing, only
    /// what is missing uploaded (blobs, trees, tags, commits); every ref moved in one compare-and-swap. A 409 stops it (nothing moved),
    /// a 422 <c>object_missing</c> uploads what it lists and tries once more. <paramref name="viewer"/> makes the IFC viewer copy of a
    /// commit (§12.2), attached after the push to every branch head and tag pushed; it never fails the push.
    /// </summary>
    public static async Task<PushOutcome> PushAsync(IRemote remote, PackageHistory h, string remoteName, IReadOnlyList<(string Local, string Remote)> refs, DateTime now,
        Func<string, (byte[] Ifc, string Schema)?>? viewer = null, bool fetchRefs = true, ISet<string>? uploaded = null)
    {
        uploaded ??= new HashSet<string>(StringComparer.Ordinal);
        await remote.ReadyAsync();
        if (fetchRefs) await FetchRefsAsync(remote, h, remoteName, now);
        var seen = SeenOf(h, remoteName);
        var updates = new List<RefUpdate>();
        foreach (var (local, name) in refs)
        {
            var mine = h.Refs.GetValueOrDefault(local) ?? throw new InvalidDataException($"There is nothing on {local} to push: commit first.");
            var theirs = seen.Refs.GetValueOrDefault(name);
            if (theirs == mine) continue;
            if (theirs != null && RefNames.IsBranch(name) && (!h.HasCommit(theirs) || !h.IsAncestor(theirs, mine)))
                return new PushOutcome(false, [], [], Moved: name, Current: theirs, Behind: true);
            updates.Add(new RefUpdate(name, theirs, mine));
        }
        if (updates.Count == 0) return new PushOutcome(true, [], []);
        var send = ToSend(h, updates.Select(u => u.New!), seen.Refs.Values);
        var asked = send.Where(s => !uploaded.Contains(s.Id)).ToList();
        var missing = new List<string>();
        for (var i = 0; i < asked.Count; i += remote.MaxBatch)
            missing.AddRange(await remote.MissingAsync(asked.Skip(i).Take(remote.MaxBatch).Select(s => s.Id).ToList()));
        // What the remote said it holds is not asked again by a push that follows in the same operation (keep my work as a branch).
        var lacking = missing.ToHashSet(StringComparer.Ordinal);
        foreach (var a in asked) if (!lacking.Contains(a.Id)) uploaded.Add(a.Id);
        var kinds = send.ToDictionary(s => s.Id, s => s.Kind, StringComparer.Ordinal);
        await Upload(remote, h, missing, kinds, uploaded);
        RefsResult after;
        try { after = await remote.UpdateRefsAsync(updates); }
        catch (RemoteException ex) when (ex is { Status: 409, Code: "ref_moved" })
        {
            var first = (ex.Body["refs"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault();
            return new PushOutcome(false, [.. missing], [.. asked.Select(a => a.Id)], Moved: (string?)first?["name"] ?? updates[0].Name, Current: (string?)first?["current"],
                MovedBy: RemoteAuthor.From(first?["updatedBy"]), MovedAt: (string?)first?["updatedAt"]);
        }
        catch (RemoteException ex) when (ex is { Status: 422, Code: "object_missing" })
        {
            var more = (ex.Body["missing"] as JsonArray ?? []).OfType<JsonObject>().Select(m => (string?)m["id"]).OfType<string>().Where(ObjectIds.Valid).ToList();
            foreach (var m in more) kinds.TryAdd(m, ObjectKind.Blob);
            await Upload(remote, h, more, kinds, uploaded);
            missing.AddRange(more);
            after = await remote.UpdateRefsAsync(updates);
        }
        TakeRefs(seen, after, now);
        var outcome = new PushOutcome(true, [.. missing], [.. asked.Select(a => a.Id)]);
        if (viewer != null && remote.Viewers) await AttachViewers(remote, h, updates, viewer, outcome.Notes);
        outcome.Notes.AddRange(remote.Notes);
        return outcome;
    }

    static async Task Upload(IRemote remote, PackageHistory h, IReadOnlyList<string> ids, IReadOnlyDictionary<string, ObjectKind> kinds, ISet<string> uploaded)
    {
        // Blobs, then trees, then tags and commits: a ref names nothing the remote is still waiting for.
        static int Rank(ObjectKind k) => k switch { ObjectKind.Blob => 0, ObjectKind.Tree => 1, ObjectKind.Tag => 2, _ => 3 };
        using var blobs = h.Blobs();
        foreach (var id in ids.Distinct(StringComparer.Ordinal).OrderBy(i => Rank(kinds.GetValueOrDefault(i, ObjectKind.Blob))))
        {
            var kind = kinds.GetValueOrDefault(id, ObjectKind.Blob);
            var bytes = kind switch
            {
                ObjectKind.Commit or ObjectKind.Tag => h.CommitBytes(id),
                ObjectKind.Tree => h.TreeBytes(id),
                _ => blobs.Get(id),
            } ?? throw new InvalidDataException($"The project's file lacks {kind.ToString().ToLowerInvariant()} {id[..12]}…: it cannot be pushed (fetch it first).");
            await remote.PutAsync(id, bytes, kind);
            uploaded.Add(id);
        }
    }

    static async Task AttachViewers(IRemote remote, PackageHistory h, IReadOnlyList<RefUpdate> updates, Func<string, (byte[] Ifc, string Schema)?> viewer, List<string> notes)
    {
        var commits = new List<string>();
        foreach (var u in updates.Where(u => u.New != null))
            commits.Add(h.KindOfHeld(u.New!) == ObjectKind.Tag ? h.Tag(u.New!).Commit : u.New!);
        foreach (var commit in commits.Distinct(StringComparer.Ordinal))
        {
            try
            {
                if (viewer(commit) is not { } copy) continue;
                var id = ObjectIds.Of(copy.Ifc);
                if ((await remote.MissingAsync([id])).Count > 0) await remote.PutAsync(id, copy.Ifc, ObjectKind.Blob);
                await remote.PutViewerAsync(commit, id, copy.Ifc.LongLength, copy.Schema);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or HttpRequestException or InvalidOperationException)
            {
                notes.Add($"The viewer copy of {commit[..8]} was not attached: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// A fetch (§8.2): the refs with the last ETag (304: done); the commits walked from every remote value not held back to what is
    /// held; the tags and the new commits' trees, then the parts and the tags' files the file does not hold; each verified, then kept
    /// in memory until the next save, and the last-seen refs set. A fetch never changes the model or a local ref; on a folder remote,
    /// a detached entry of this installation's (§7.3) is kept as a wip branch, once, and said.
    /// </summary>
    public static async Task<FetchOutcome> FetchAsync(IRemote remote, PackageHistory h, string remoteName, RemoteAuthor me, DateTime now)
    {
        await remote.ReadyAsync();
        var seen = SeenOf(h, remoteName);
        var refs = await remote.RefsAsync(seen.Refs.Count > 0 ? seen.Etag : null);
        // Unchanged since the last look, but that look (a push's) may not have brought the objects: fetched from the refs last seen.
        if (refs.NotModified && seen.Refs.Values.Any(v => !h.HasCommit(v)))
            refs = new RefsResult(false, [.. seen.Refs.Select(kv => new RemoteRef(kv.Key, kv.Value))], refs.Etag ?? seen.Etag);
        if (refs.NotModified)
        {
            seen.FetchedAt = RemoteObjects.Stamp(now);
            var same = new FetchOutcome(true, 0, 0, 0, 0);
            await Detached(remote, h, seen, me, now, same.Notes);
            same.Notes.AddRange(remote.Notes);
            return same;
        }
        int commits = 0, trees = 0, blobsCount = 0, tagCount = 0;
        // Tags first (they are small): their commits join the wanted ones.
        var tagIds = refs.Refs.Where(r => RefNames.IsTag(r.Name) && !h.HasCommit(r.Target)).Select(r => r.Target).Distinct(StringComparer.Ordinal).ToList();
        var wanted = refs.Refs.Where(r => RefNames.IsBranch(r.Name)).Select(r => r.Target).ToList();
        foreach (var t in refs.Refs.Where(r => RefNames.IsTag(r.Name) && h.HasCommit(r.Target))) wanted.Add(h.Tag(t.Target).Commit);
        var newCommits = new List<CommitObject>();
        async Task Walk(IEnumerable<string> heads)
        {
            foreach (var head in heads.Distinct(StringComparer.Ordinal).ToList())
            {
                if (h.HasCommit(head)) continue;
                var stop = h.Refs.Values.Concat(seen.Refs.Values).Where(h.HasCommit).Distinct(StringComparer.Ordinal).Take(100).ToList();
                foreach (var (id, bytes) in await remote.CommitsAsync(head, stop))
                {
                    if (h.HasCommit(id)) continue;
                    h.Add(id, bytes, ObjectKind.Commit);
                    newCommits.Add(RemoteObjects.ReadCommit(bytes, id));
                    commits++;
                }
            }
        }
        await Walk(wanted);
        // The tags and the trees of the new commits, in one batch.
        var first = tagIds.Concat(newCommits.Select(c => c.Tree).Where(t => !h.HasTree(t))).Distinct(StringComparer.Ordinal).ToList();
        var newTags = new List<TagObject>();
        foreach (var (id, bytes) in await GetAll(remote, first))
        {
            var kind = RemoteObjects.KindOf(bytes);
            if (kind == ObjectKind.Tag) { h.Add(id, bytes, ObjectKind.Tag); newTags.Add(RemoteObjects.ReadTag(bytes, id)); tagCount++; }
            else if (kind == ObjectKind.Tree) { h.Add(id, bytes, ObjectKind.Tree); trees++; }
            else throw new InvalidDataException($"The remote sent {id[..12]}… as a tree or a tag, which it is not.");
        }
        // A tag's commit not on any branch: walked now.
        var tagCommits = newTags.Select(t => t.Commit).Where(c => !h.HasCommit(c)).ToList();
        if (tagCommits.Count > 0)
        {
            var before = newCommits.Count;
            await Walk(tagCommits);
            var moreTrees = newCommits.Skip(before).Select(c => c.Tree).Where(t => !h.HasTree(t)).Distinct(StringComparer.Ordinal).ToList();
            foreach (var (id, bytes) in await GetAll(remote, moreTrees)) { h.Add(id, bytes, ObjectKind.Tree); trees++; }
        }
        // The parts of the new trees and the tags' files the file does not hold anywhere (§6.4).
        var needed = new List<string>();
        using (var blobs = h.Blobs())
        {
            foreach (var c in newCommits)
                foreach (var p in h.Tree(c.Tree).Parts)
                    if (!blobs.Contains(p.Sha256)) needed.Add(p.Sha256);
            foreach (var t in newTags)
                foreach (var f in t.Files)
                    if (!blobs.Contains(f.Sha256)) needed.Add(f.Sha256);
        }
        foreach (var (id, bytes) in await GetAll(remote, needed.Distinct(StringComparer.Ordinal).ToList())) { h.Add(id, bytes, ObjectKind.Blob); blobsCount++; }
        TakeRefs(seen, refs, now);
        var outcome = new FetchOutcome(false, commits, trees, blobsCount, tagCount);
        await Detached(remote, h, seen, me, now, outcome.Notes);
        outcome.Notes.AddRange(remote.Notes);
        return outcome;
    }

    static async Task<List<(string Id, byte[] Bytes)>> GetAll(IRemote remote, IReadOnlyList<string> ids)
    {
        var o = new List<(string, byte[])>();
        for (var i = 0; i < ids.Count; i += remote.MaxBatch)
            foreach (var (id, bytes) in await remote.GetManyAsync(ids.Skip(i).Take(remote.MaxBatch).ToList()))
                o.Add((id, bytes));
        return o;
    }

    /// <summary>A folder's detached entries of this installation (§7.3): a branch's kept as <c>refs/heads/wip/&lt;me&gt;/&lt;name&gt;</c>, a tag's said; each once.</summary>
    static async Task Detached(IRemote remote, PackageHistory h, RemoteSeen seen, RemoteAuthor me, DateTime now, List<string> notes)
    {
        foreach (var d in remote.Detached())
        {
            if (d.Author.Id != me.Id || seen.SeenDetached.Contains(d.File)) continue;
            if (RefNames.IsBranch(d.Name) && d.New != null)
            {
                var name = RefNames.Wip(me.Name, now, n => seen.Refs.ContainsKey(n));
                try
                {
                    var after = await remote.UpdateRefsAsync([new RefUpdate(name, null, d.New)]);
                    TakeRefs(seen, after, now);
                    notes.Add($"The remote had moved: your work is kept as branch {name["refs/heads/".Length..]}; pull to go on from the remote's.");
                }
                catch (RemoteException ex) { notes.Add($"Your work that the remote did not take could not be kept as a branch: {ex.Message}"); continue; }
            }
            else notes.Add($"The remote did not take your {d.Name["refs/".Length..]}: another one was made first, and stands.");
            seen.SeenDetached.Add(d.File);
        }
    }

    /// <summary>
    /// A clone (§8.3) into an empty history: the project, every ref fetched, the local main set to the remote's default ref (HEAD on
    /// it), the remote recorded. Gives back the tree to open as the project; null when the remote's project has no commit yet.
    /// </summary>
    public static async Task<string?> CloneAsync(IRemote remote, PackageHistory h, RemoteEntry entry, RemoteAuthor me, DateTime now)
    {
        await remote.ReadyAsync();
        var project = await remote.ProjectAsync();
        h.Remotes.Clear();
        h.Remotes.Add(entry);
        await FetchAsync(remote, h, entry.Name, me, now);
        var head = SeenOf(h, entry.Name).Refs.GetValueOrDefault(project.DefaultRef);
        h.Head = RefNames.Main;
        if (head == null) return null;
        h.Move(RefNames.Main, head, me, "clone", now);
        foreach (var (name, value) in SeenOf(h, entry.Name).Refs.Where(r => RefNames.IsTag(r.Key))) h.Refs[name] = value;
        return h.Commit(head).Tree;
    }

    /// <summary>Where the current branch stands against the remote's last-seen value of the same name (after a fetch).</summary>
    public static (PullKind Kind, string? Local, string? Remote) Stand(PackageHistory h, string remoteName)
    {
        var l = h.HeadCommit;
        var r = SeenOf(h, remoteName).Refs.GetValueOrDefault(h.Head);
        if (r == null || l == r) return (r == null && l != null ? PullKind.Ahead : PullKind.UpToDate, l, r);
        if (l == null || h.IsAncestor(l, r)) return (PullKind.FastForward, l, r);
        if (h.IsAncestor(r, l)) return (PullKind.Ahead, l, r);
        return (PullKind.Diverged, l, r);
    }
}
