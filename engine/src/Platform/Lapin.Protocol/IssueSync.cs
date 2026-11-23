using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Package.Remote;
using Lapin.Platform.Cde.Remarks;

namespace Lapin.Protocol;

/// <summary>
/// The project's remarks synced with a CDE server's issues (Atrium docs/plan-cde-web.md, stage W3; §7 Q6): BCF topics live both in
/// the <c>.lapin</c> (<c>LapinRemark</c>, by GUID) and on the server, and the server is the truth for a shared project. One sync:
/// <list type="number">
/// <item>who the account is, its role and the project's rules, members and BCF extensions (what the interface offers and greys out);</item>
/// <item>what changed on the server since the last sync: the BCF event journal after the last seq (<c>GET /atrium/1.0/projects/{p}/events</c>),
/// with every topic's ETag (<c>…/topics</c>) as a second check; the topics that changed read whole (comments, viewpoints, snapshots);</item>
/// <item>each topic merged by field against the server values last seen (the base): a field changed here is pushed on top of the
/// server's (last writer wins per field), a field changed there comes here; comments and viewpoints by GUID, new ones sent (comments
/// before a status, so that "Resolved" finds its comment), deletions both ways as the role rules allow (a deletion refused brings the
/// topic or comment back);</item>
/// <item>edits sent as JSON merge patches of the changed fields with <c>If-Match</c>; a 412 reads the topic again and sends again.</item>
/// </list>
/// The caller applies <see cref="Outcome.Topics"/> to the model in one undo step and keeps <see cref="Outcome.State"/> with the project's
/// history (remotes.json), never in a commit.
/// </summary>
public sealed class IssueSync(HttpRemote server, string project)
{
    /// <summary>The topic fields synced: BCF's writable ones Lapin keeps (reference links, index and BIM snippets stay the server's).</summary>
    public static readonly string[] Fields = ["title", "description", "topic_type", "topic_status", "priority", "assigned_to", "due_date", "stage", "labels"];

    /// <summary>How many times an edit is sent again after a 412 before it is left for the next sync.</summary>
    public const int Retries = 3;

    readonly string _bcf = $"bcf/3.0/projects/{Uri.EscapeDataString(project)}";
    readonly string _atrium = $"atrium/1.0/projects/{Uri.EscapeDataString(project)}";

    /// <summary>What a sync did: the remarks as they are to be (null: removed), the state to keep, and the report for the interface.</summary>
    public sealed record Outcome(Dictionary<Guid, BcfTopic?> Topics, JsonObject State, JsonObject Report);

    /// <summary>The server's version of one topic: its JSON, ETag, comments and viewpoints (with what Lapin keeps of them).</summary>
    sealed record ServerTopic(JsonObject Json, string? ETag, List<BcfComment> Comments, List<BcfViewpoint> Viewpoints);

    // ------------------------------------------------------------ the report

    int _created, _updated, _comments, _viewpoints, _deleted, _conflicts, _added, _removed, _changedHere;
    readonly JsonArray _refused = [];
    readonly Dictionary<string, (string Who, string? Title, HashSet<string> What)> _others = new(StringComparer.Ordinal);
    string _me = "";

    void Refused(Guid guid, string? title, string code, string message)
    {
        if (_refused.Count < 50) _refused.Add((JsonNode)new JsonObject { ["guid"] = guid.ToString("D"), ["title"] = title, ["code"] = code, ["message"] = message });
    }

    // ------------------------------------------------------------ requests

    async Task<ApiAnswer> Get(string path, bool bytes = false) => await server.ApiAsync(HttpMethod.Get, path, bytes: bytes);

    static RemoteException Fail(ApiAnswer a, string what) => new(a.Status, a.Code, a.Status == 404 && a.Code == "not_found" && what.Length > 0
        ? what
        : a.Message, a.Json as JsonObject);

    async Task<JsonObject> Need(string path, string notOffered = "")
    {
        var a = await Get(path);
        if (!a.Ok || a.Json is not JsonObject o) throw Fail(a, notOffered);
        return o;
    }

    // ------------------------------------------------------------ the sync

    public async Task<Outcome> RunAsync(IReadOnlyList<BcfTopic> localTopics, JsonObject? previous, string serverUrl, DateTime now)
    {
        const string old = "This server does not sync issues (it needs Atrium Server with the issue journal).";
        var account = await Need("atrium/1.0/account", old);
        _me = (string?)account["id"] ?? (string?)account["email"] ?? "";
        var rules = await Need($"{_atrium}/rules", "The project does not exist on the server, or is not yours to see.");
        var role = (string?)rules["role"] ?? "viewer";
        var people = new JsonObject();
        var members = await Get($"{_atrium}/members");
        if (members.Ok && members.Json?["members"] is JsonArray ms)
            foreach (var m in ms.OfType<JsonObject>())
                if ((string?)m["email"] is { } email) people[email.ToLowerInvariant()] = (string?)m["name"] is { Length: > 0 } n ? n : email;
        if (_me.Length > 0) people[_me.ToLowerInvariant()] = (string?)account["name"] is { Length: > 0 } myName ? myName : _me;
        var extensions = await Get($"{_bcf}/extensions");

        var fresh = previous == null || (string?)previous["project"] != project || !string.Equals((string?)previous["account"], _me, StringComparison.OrdinalIgnoreCase)
            || !string.Equals((string?)previous["server"], serverUrl, StringComparison.OrdinalIgnoreCase);
        var baseTopics = new Dictionary<Guid, JsonObject>();
        if (!fresh && previous!["topics"] is JsonObject bt)
            foreach (var (k, v) in bt)
                if (Guid.TryParse(k, out var g) && v is JsonObject o) baseTopics[g] = o;

        // What the server holds now (every topic's ETag), then what changed since the last sync (the journal).
        var versions = await Need($"{_atrium}/topics", old);
        var onServer = new Dictionary<Guid, string?>();
        foreach (var t in (versions["topics"] as JsonArray ?? []).OfType<JsonObject>())
            if (Guid.TryParse((string?)t["guid"], out var g)) onServer[g] = (string?)t["etag"];
        long last = (long?)versions["last"] ?? 0;
        var changed = new HashSet<Guid>();
        if (fresh) changed.UnionWith(onServer.Keys);
        else
        {
            var after = (long?)previous!["last"] ?? 0;
            for (var page = 0; page < 1000; page++)
            {
                var j = await Need($"{_atrium}/events?after={after.ToString(CultureInfo.InvariantCulture)}&limit=1000", old);
                foreach (var e in (j["events"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    if (!Guid.TryParse((string?)e["topicGuid"], out var g)) continue;
                    changed.Add(g);
                    var type = (string?)e["type"] ?? "";
                    var who = (string?)e["author"] ?? "";
                    if (!string.Equals(who, _me, StringComparison.OrdinalIgnoreCase)) Other(g, (string?)e["authorName"] ?? who, type, e);
                }
                after = (long?)j["last"] ?? after;
                if (j["more"] is not JsonValue mv || !mv.TryGetValue<bool>(out var more) || !more) break;
            }
            last = after;
            // A topic whose ETag is not the one last seen changed too (a journal lost or cut is no reason to miss it).
            foreach (var (g, etag) in onServer)
                if (!baseTopics.TryGetValue(g, out var b) || (string?)b["etag"] != etag) changed.Add(g);
        }

        var local = localTopics.ToDictionary(t => t.Guid);
        var result = new Dictionary<Guid, BcfTopic?>();
        var state = new JsonObject();
        var all = new HashSet<Guid>(local.Keys);
        all.UnionWith(onServer.Keys);
        all.UnionWith(baseTopics.Keys);
        foreach (var g in all.OrderBy(x => x))
        {
            local.TryGetValue(g, out var mine);
            baseTopics.TryGetValue(g, out var known);
            var there = onServer.ContainsKey(g);
            try
            {
                var (topic, kept) = await One(g, mine, known, there, changed.Contains(g), role);
                if (!Same(mine, topic)) result[g] = topic;
                if (kept != null) state[g.ToString("D")] = kept;
            }
            catch (RemoteException e) when (e.Status is not 401)
            {
                // One topic the server refuses does not stop the others: it stays as it is here, and its base as it was.
                Refused(g, mine?.Title, e.Code, e.Message);
                if (known != null) state[g.ToString("D")] = known.DeepClone();
            }
        }

        _changedHere = result.Count;
        foreach (var (key, o) in _others.ToList())
            if (o.Title == null && Guid.TryParse(key, out var og))
                _others[key] = (o.Who, (result.TryGetValue(og, out var rt) ? rt?.Title : null) ?? local.GetValueOrDefault(og)?.Title, o.What);
        var report = new JsonObject
        {
            ["changedHere"] = _changedHere, ["added"] = _added, ["removed"] = _removed, ["created"] = _created, ["updated"] = _updated,
            ["comments"] = _comments, ["viewpoints"] = _viewpoints, ["deleted"] = _deleted, ["conflicts"] = _conflicts, ["refused"] = _refused,
            ["byOthers"] = new JsonArray([.. _others.Values.Take(30).Select(o => (JsonNode)new JsonObject
            {
                ["who"] = o.Who, ["title"] = o.Title, ["what"] = new JsonArray([.. o.What.Order(StringComparer.Ordinal).Select(x => (JsonNode)x)]),
            })]),
            ["first"] = fresh,
        };
        var stamp = now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var keptState = new JsonObject
        {
            ["server"] = serverUrl, ["project"] = project, ["account"] = _me, ["name"] = (string?)account["name"], ["last"] = last, ["syncedAt"] = stamp,
            ["role"] = role, ["isAdmin"] = account["isAdmin"]?.DeepClone(),
            ["rules"] = new JsonObject
            {
                ["comments"] = (string?)rules["comments"] ?? "viewer", ["close"] = (string?)rules["close"] ?? "author",
                ["resolveNeedsComment"] = rules["resolveNeedsComment"] is JsonValue rv && rv.TryGetValue<bool>(out var rn) ? rn : true,
                ["main"] = (string?)rules["main"], ["issueTags"] = (string?)rules["issueTags"],
            },
            ["people"] = people,
            ["extensions"] = extensions.Ok ? Extensions(extensions.Json as JsonObject) : null,
            ["topics"] = state,
        };
        return new Outcome(result, keptState, report);
    }

    /// <summary>The project's BCF extensions the interface offers (types, statuses, priorities, labels, stages, users).</summary>
    static JsonObject Extensions(JsonObject? e)
    {
        var o = new JsonObject();
        if (e == null) return o;
        foreach (var (from, to) in new[] { ("topic_type", "types"), ("topic_status", "statuses"), ("priority", "priorities"), ("topic_label", "labels"), ("stage", "stages"), ("users", "users") })
            if (e[from] is JsonArray a) o[to] = new JsonArray([.. a.Select(x => (string?)x).OfType<string>().Select(x => (JsonNode)x)]);
        return o;
    }

    void Other(Guid g, string who, string type, JsonObject e)
    {
        var key = g.ToString("D");
        if (!_others.TryGetValue(key, out var o)) _others[key] = o = (who, null, new HashSet<string>(StringComparer.Ordinal));
        var what = type switch
        {
            "topic_created" => "created",
            "topic_deleted" => "deleted",
            "comment_created" or "comment_text_updated" or "comment_deleted" => "commented",
            "status_updated" => $"status: {(string?)e["new"]}",
            "viewpoint_added" or "viewpoint_updated" or "viewpoint_removed" or "viewpoint_deleted" => "viewpoint",
            _ => "edited",
        };
        o.What.Add(what);
        if (type == "title_updated" && (string?)e["new"] is { } t) _others[key] = (o.Who, t, o.What);
    }

    // ------------------------------------------------------------ one topic

    /// <summary>
    /// One topic synced: <paramref name="mine"/> (null: not here, or deleted here), <paramref name="known"/> (the base: null when never
    /// synced), whether the server has it and whether it changed there. Gives the topic as it is to be here (null: none) and its new
    /// state (null: forgotten).
    /// </summary>
    async Task<(BcfTopic? Topic, JsonObject? State)> One(Guid g, BcfTopic? mine, JsonObject? known, bool there, bool changedThere, string role)
    {
        var path = $"{_bcf}/topics/{g:D}";
        if (!there)
        {
            if (mine == null) return (null, null);                                // gone both ways
            if (known != null)
            {
                _removed++;                                                       // deleted on the server: gone here too (the server is the truth)
                return (null, null);
            }
            return await Create(g, mine, role);                                    // new here: sent with its GUID
        }
        if (mine == null && known != null)
        {
            // Deleted here: only owners delete topics; a deletion refused brings the topic back.
            var del = await server.ApiAsync(HttpMethod.Delete, path);
            if (del.Ok || del.Status == 404) { _deleted++; return (null, null); }
            var back = await Read(g, null) ?? throw Fail(del, "");
            Refused(g, (string?)back.Json["title"], del.Status == 403 ? "delete_forbidden" : del.Code, del.Message);
            _added++;
            return (FromServer(back, null), Base(back));
        }

        // On both sides (or new on the server).
        var s = changedThere || known == null ? await Read(g, mine) : null;
        if (s == null && known == null) return (mine, null);                       // listed but gone meanwhile: next time
        if (mine == null)
        {
            _added++;
            return (FromServer(s!, null), Base(s!));
        }
        var baseFields = known?["base"] as JsonObject;
        var mineFields = FieldsOf(mine);
        // The fields changed here since the last sync (none on a first sync: the server is the truth).
        var changedHere = baseFields == null ? [] : Fields.Where(f => !SameValue(mineFields[f], baseFields[f])).ToList();
        var baseComments = Guids(known?["comments"]);
        var baseViewpoints = Guids(known?["viewpoints"]);
        var serverComments = s?.Comments ?? null;
        var serverViewpoints = s?.Viewpoints ?? null;
        var serverCommentIds = serverComments?.Select(c => c.Guid).ToHashSet() ?? baseComments;
        var serverViewpointIds = serverViewpoints?.Select(v => v.Guid).ToHashSet() ?? baseViewpoints;

        var newComments = mine.Comments.Where(c => !baseComments.Contains(c.Guid) && !serverCommentIds.Contains(c.Guid)).OrderBy(c => c.Date, StringComparer.Ordinal).ToList();
        var newViewpoints = mine.Viewpoints.Where(v => !baseViewpoints.Contains(v.Guid) && !serverViewpointIds.Contains(v.Guid)).ToList();
        var goneComments = baseComments.Where(c => mine.Comments.All(x => x.Guid != c) && serverCommentIds.Contains(c)).ToList();
        var goneViewpoints = baseViewpoints.Where(v => mine.Viewpoints.All(x => x.Guid != v) && serverViewpointIds.Contains(v)).ToList();
        if (s == null && changedHere.Count == 0 && newComments.Count == 0 && newViewpoints.Count == 0 && goneComments.Count == 0 && goneViewpoints.Count == 0)
            return (mine, known!.DeepClone() as JsonObject);                        // nothing on either side

        var comments = mine.Comments.ToList();
        var viewpoints = mine.Viewpoints.ToList();
        var touched = false;
        // Viewpoints first (a comment may point at one), then comments, then the deletions.
        foreach (var v in newViewpoints)
        {
            var a = await server.ApiAsync(HttpMethod.Post, $"{path}/viewpoints", ViewpointOut(v));
            if (a.Ok || a.Status == 409) { _viewpoints++; touched = true; serverViewpointIds.Add(v.Guid); }
            else { Refused(g, mine.Title, a.Code, a.Message); viewpoints.RemoveAll(x => x.Guid == v.Guid && a.Status == 403); }
        }
        foreach (var c in newComments)
        {
            var body = new JsonObject { ["guid"] = c.Guid.ToString("D"), ["comment"] = c.Text };
            if (c.Viewpoint is { } vg && serverViewpointIds.Contains(vg)) body["viewpoint_guid"] = vg.ToString("D");
            var a = await server.ApiAsync(HttpMethod.Post, $"{path}/comments", body);
            if (a.Ok && a.Json is JsonObject cj && CommentIn(cj) is { } sc) { Replace(comments, sc); _comments++; touched = true; serverCommentIds.Add(c.Guid); }
            else if (a.Status == 409) serverCommentIds.Add(c.Guid);
            else { Refused(g, mine.Title, a.Code, a.Message); if (a.Status == 403) comments.RemoveAll(x => x.Guid == c.Guid); }
        }
        foreach (var c in goneComments)
        {
            var a = await server.ApiAsync(HttpMethod.Delete, $"{path}/comments/{c:D}");
            if (a.Ok || a.Status == 404) { touched = true; serverCommentIds.Remove(c); }
            else
            {
                Refused(g, mine.Title, a.Code, a.Message);
                // Not the user's to delete: it comes back.
                s ??= await Read(g, mine);
                if (s?.Comments.FirstOrDefault(x => x.Guid == c) is { } back) comments.Add(back);
            }
        }
        foreach (var v in goneViewpoints)
        {
            var a = await server.ApiAsync(HttpMethod.Delete, $"{path}/viewpoints/{v:D}");
            if (a.Ok || a.Status == 404) { touched = true; serverViewpointIds.Remove(v); }
            else
            {
                Refused(g, mine.Title, a.Code, a.Message);
                s ??= await Read(g, mine);
                if (s?.Viewpoints.FirstOrDefault(x => x.Guid == v) is { } back) viewpoints.Add(back);
            }
        }
        // The server's own new comments and viewpoints come here; those deleted there go.
        if (s != null)
        {
            foreach (var c in s.Comments) if (!goneComments.Contains(c.Guid)) Replace(comments, c);
            comments.RemoveAll(c => baseComments.Contains(c.Guid) && !serverCommentIds.Contains(c.Guid) && s.Comments.All(x => x.Guid != c.Guid));
            foreach (var v in s.Viewpoints) if (!goneViewpoints.Contains(v.Guid) && viewpoints.All(x => x.Guid != v.Guid)) viewpoints.Add(v);
            viewpoints.RemoveAll(v => baseViewpoints.Contains(v.Guid) && !serverViewpointIds.Contains(v.Guid) && s.Viewpoints.All(x => x.Guid != v.Guid));
        }

        // The fields: mine on top of the server's, sent as a merge patch with If-Match; a 412 reads again and sends again.
        if (touched && changedHere.Count > 0) s = await Read(g, mine) ?? s;   // a comment changed the topic's ETag: read it once
        var serverJson = s?.Json ?? (JsonObject)known!["base"]!.DeepClone();
        var etag = s?.ETag ?? (string?)known?["etag"];
        var pending = new List<string>();
        if (changedHere.Count > 0)
        {
            var patch = new JsonObject();
            foreach (var f in changedHere)
                if (!SameValue(mineFields[f], serverJson[f])) patch[f] = mineFields[f]?.DeepClone();
            for (var attempt = 0; patch.Count > 0; attempt++)
            {
                var a = await server.ApiAsync(HttpMethod.Patch, $"{_atrium}/topics/{g:D}", patch, etag);
                if (a.Ok && a.Json is JsonObject tj) { serverJson = tj; etag = a.ETag ?? etag; _updated++; break; }
                if (a.Status == 412 && attempt < Retries)
                {
                    _conflicts++;
                    var again = await Read(g, mine);
                    if (again == null) break;
                    serverJson = again.Json;
                    etag = again.ETag;
                    patch = [];
                    foreach (var f in changedHere)
                        if (!SameValue(mineFields[f], serverJson[f])) patch[f] = mineFields[f]?.DeepClone();
                    continue;
                }
                Refused(g, mine.Title, a.Code, a.Message);
                // A rule refused it (your role, closing, a status only): the server's values come back. A missing comment, or a value the
                // project does not offer: the change stays here, to send again once it is fixed.
                if (a.Code is "comment_required" or "bad_request" or "changed") pending.AddRange(patch.Select(kv => kv.Key));
                break;
            }
        }
        else if (touched)
        {
            var again = await Read(g, mine);
            if (again != null) { serverJson = again.Json; etag = again.ETag; }
        }

        var st = new ServerTopic(serverJson, etag, [], []);
        var merged = FromServer(st, mine) with
        {
            Comments = comments.OrderBy(c => c.Date, StringComparer.Ordinal).ToList(),
            Viewpoints = viewpoints,
        };
        if (pending.Count > 0) merged = WithFields(merged, mineFields, pending);
        var kept = new JsonObject
        {
            ["etag"] = etag, ["base"] = Writable(serverJson),
            ["comments"] = new JsonArray([.. serverCommentIds.OrderBy(x => x).Select(x => (JsonNode)x.ToString("D"))]),
            ["viewpoints"] = new JsonArray([.. serverViewpointIds.OrderBy(x => x).Select(x => (JsonNode)x.ToString("D"))]),
        };
        return (merged, kept);
    }

    /// <summary>A topic made here, sent with its GUID, its viewpoints and comments.</summary>
    async Task<(BcfTopic? Topic, JsonObject? State)> Create(Guid g, BcfTopic mine, string role)
    {
        if (role == "viewer")
        {
            Refused(g, mine.Title, "forbidden", "Your role in this project (Viewer) does not create issues.");
            return (mine, null);
        }
        var body = FieldsOf(mine);
        body["guid"] = g.ToString("D");
        foreach (var f in Fields) if (body[f] == null) body.Remove(f);
        var a = await server.ApiAsync(HttpMethod.Post, $"{_bcf}/topics", body);
        if (!a.Ok || a.Json is not JsonObject tj)
        {
            Refused(g, mine.Title, a.Code, a.Message);
            return (mine, null);
        }
        _created++;
        var path = $"{_bcf}/topics/{g:D}";
        var vps = new HashSet<Guid>();
        var cs = new HashSet<Guid>();
        var comments = mine.Comments.ToList();
        foreach (var v in mine.Viewpoints)
        {
            var r = await server.ApiAsync(HttpMethod.Post, $"{path}/viewpoints", ViewpointOut(v));
            if (r.Ok) { vps.Add(v.Guid); _viewpoints++; } else Refused(g, mine.Title, r.Code, r.Message);
        }
        foreach (var c in mine.Comments.OrderBy(c => c.Date, StringComparer.Ordinal))
        {
            var cb = new JsonObject { ["guid"] = c.Guid.ToString("D"), ["comment"] = c.Text };
            if (c.Viewpoint is { } vg && vps.Contains(vg)) cb["viewpoint_guid"] = vg.ToString("D");
            var r = await server.ApiAsync(HttpMethod.Post, $"{path}/comments", cb);
            if (r.Ok && r.Json is JsonObject cj && CommentIn(cj) is { } sc) { Replace(comments, sc); cs.Add(c.Guid); _comments++; }
            else Refused(g, mine.Title, r.Code, r.Message);
        }
        var etag = a.ETag;
        if (vps.Count > 0 || cs.Count > 0)
        {
            var again = await Get(path);
            if (again.Ok && again.Json is JsonObject fresh) { tj = fresh; etag = again.ETag; }
        }
        var merged = FromServer(new ServerTopic(tj, etag, [], []), mine) with { Comments = comments, Viewpoints = mine.Viewpoints };
        return (merged, new JsonObject
        {
            ["etag"] = etag, ["base"] = Writable(tj),
            ["comments"] = new JsonArray([.. cs.OrderBy(x => x).Select(x => (JsonNode)x.ToString("D"))]),
            ["viewpoints"] = new JsonArray([.. vps.OrderBy(x => x).Select(x => (JsonNode)x.ToString("D"))]),
        });
    }

    /// <summary>The server's topic read whole: its JSON and ETag, comments, viewpoints (with their snapshots, unless held here already).</summary>
    async Task<ServerTopic?> Read(Guid g, BcfTopic? mine)
    {
        var path = $"{_bcf}/topics/{g:D}";
        var t = await Get(path);
        if (t.Status == 404) return null;
        if (!t.Ok || t.Json is not JsonObject tj) throw Fail(t, "");
        var comments = new List<BcfComment>();
        var c = await Get($"{path}/comments");
        if (c.Ok && c.Json is JsonArray ca) comments.AddRange(ca.OfType<JsonObject>().Select(CommentIn).OfType<BcfComment>());
        var viewpoints = new List<BcfViewpoint>();
        var v = await Get($"{path}/viewpoints");
        if (v.Ok && v.Json is JsonArray va)
            foreach (var vj in va.OfType<JsonObject>())
            {
                if (!Guid.TryParse((string?)vj["guid"], out var vg)) continue;
                if (mine?.Viewpoints.FirstOrDefault(x => x.Guid == vg) is { } held) { viewpoints.Add(held); continue; }
                viewpoints.Add(await ViewpointIn(vj, $"{path}/viewpoints/{vg:D}"));
            }
        return new ServerTopic(tj, t.ETag, comments, viewpoints);
    }

    JsonObject Base(ServerTopic s) => new()
    {
        ["etag"] = s.ETag, ["base"] = Writable(s.Json),
        ["comments"] = new JsonArray([.. s.Comments.Select(x => x.Guid).Order().Select(x => (JsonNode)x.ToString("D"))]),
        ["viewpoints"] = new JsonArray([.. s.Viewpoints.Select(x => x.Guid).Order().Select(x => (JsonNode)x.ToString("D"))]),
    };

    // ------------------------------------------------------------ BCF 3.0 JSON, both ways

    /// <summary>A remark's synced fields as BCF JSON (snake_case); a field it lacks is null.</summary>
    public static JsonObject FieldsOf(BcfTopic t) => new()
    {
        ["title"] = t.Title, ["description"] = Text(t.Description), ["topic_type"] = Text(t.Type), ["topic_status"] = Text(t.Status), ["priority"] = Text(t.Priority),
        ["assigned_to"] = Text(t.AssignedTo), ["due_date"] = Text(t.DueDate), ["stage"] = Text(t.Stage),
        ["labels"] = t.Labels.Count > 0 ? new JsonArray([.. t.Labels.Select(l => (JsonNode)l)]) : null,
    };

    static string? Text(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>The server topic's synced fields, normalised (absent and empty alike).</summary>
    static JsonObject Writable(JsonObject topic)
    {
        var o = new JsonObject();
        foreach (var f in Fields)
        {
            var v = topic[f];
            o[f] = f == "labels" ? (v is JsonArray { Count: > 0 } a ? a.DeepClone() : null) : (v is JsonValue sv && sv.TryGetValue<string>(out var s) && s.Length > 0 ? s : null);
        }
        // What the remark shows of the topic beside its fields.
        foreach (var k in new[] { "creation_date", "creation_author", "modified_date", "modified_author", "server_assigned_id" })
            if (topic[k] is JsonValue kv && kv.TryGetValue<string>(out var ks)) o[k] = ks;
        return o;
    }

    /// <summary>Two field values alike: strings (empty as none), lists of labels in order.</summary>
    public static bool SameValue(JsonNode? a, JsonNode? b)
    {
        static JsonNode? Norm(JsonNode? n) => n switch
        {
            JsonValue v when v.TryGetValue<string>(out var s) => s.Length == 0 ? null : n,
            JsonArray { Count: 0 } => null,
            _ => n,
        };
        a = Norm(a); b = Norm(b);
        if (a == null || b == null) return a == null && b == null;
        return JsonNode.DeepEquals(a, b);
    }

    /// <summary>The remark as the server has it: its fields and dates, and (from <paramref name="mine"/>) the comments and viewpoints held here.</summary>
    static BcfTopic FromServer(ServerTopic s, BcfTopic? mine)
    {
        var j = s.Json;
        string? S(string k) => j[k] is JsonValue v && v.TryGetValue<string>(out var x) && x.Length > 0 ? x : null;
        var labels = j["labels"] is JsonArray la ? la.Select(x => (string?)x).OfType<string>().Where(x => x.Length > 0).ToList() : [];
        return new BcfTopic(Bcf.ParseGuid(S("guid")) ?? mine?.Guid ?? Guid.NewGuid(), S("title") ?? mine?.Title ?? "", S("description"), S("topic_type"), S("topic_status"), S("priority"),
            j["index"] is JsonValue iv && iv.TryGetValue<int>(out var ix) ? ix : mine?.Index, S("creation_date") ?? mine?.CreationDate ?? Bcf.Now(), S("creation_author") ?? mine?.CreationAuthor ?? "",
            S("modified_date"), S("modified_author"), S("assigned_to"), S("due_date"), S("stage"), labels,
            s.Comments.Count > 0 || mine == null ? s.Comments : mine.Comments, s.Viewpoints.Count > 0 || mine == null ? s.Viewpoints : mine.Viewpoints);
    }

    /// <summary>The topic with some fields put back to <paramref name="fields"/>' (changes the server refused for now, kept to send again).</summary>
    static BcfTopic WithFields(BcfTopic t, JsonObject fields, IEnumerable<string> keys)
    {
        foreach (var k in keys)
        {
            string? v = fields[k] is JsonValue sv && sv.TryGetValue<string>(out var s) ? s : null;
            t = k switch
            {
                "title" => t with { Title = v ?? t.Title },
                "description" => t with { Description = v },
                "topic_type" => t with { Type = v },
                "topic_status" => t with { Status = v },
                "priority" => t with { Priority = v },
                "assigned_to" => t with { AssignedTo = v },
                "due_date" => t with { DueDate = v },
                "stage" => t with { Stage = v },
                "labels" => t with { Labels = Bcf.Strings(fields[k]) },
                _ => t,
            };
        }
        return t;
    }

    static BcfComment? CommentIn(JsonObject c) => Guid.TryParse((string?)c["guid"], out var g)
        ? new BcfComment(g, (string?)c["date"] ?? Bcf.Now(), (string?)c["author"] ?? "", (string?)c["comment"] ?? "", Bcf.ParseGuid((string?)c["viewpoint_guid"]),
            (string?)c["modified_date"], (string?)c["modified_author"])
        : null;

    static void Replace(List<BcfComment> list, BcfComment c)
    {
        var i = list.FindIndex(x => x.Guid == c.Guid);
        if (i >= 0) list[i] = c; else list.Add(c);
    }

    static JsonObject Xyz(BcfPoint p) => new() { ["x"] = p.X, ["y"] = p.Y, ["z"] = p.Z };
    static BcfPoint? Point(JsonNode? n) => n is JsonObject o ? new BcfPoint(Lapin.Model.Json.Number(o["x"]) ?? 0, Lapin.Model.Json.Number(o["y"]) ?? 0, Lapin.Model.Json.Number(o["z"]) ?? 0) : null;

    /// <summary>A viewpoint as BCF 3.0's API takes it (viewpoint_POST): its camera, lines, planes, components and snapshot.</summary>
    public static JsonObject ViewpointOut(BcfViewpoint v)
    {
        var o = new JsonObject { ["guid"] = v.Guid.ToString("D") };
        if (v.Index is { } i) o["index"] = i;
        if (v.Camera is { } c)
        {
            var cam = new JsonObject
            {
                ["camera_view_point"] = Xyz(c.ViewPoint), ["camera_direction"] = Xyz(c.Direction), ["camera_up_vector"] = Xyz(c.Up), ["aspect_ratio"] = c.AspectRatio ?? 1.5,
            };
            if (c.Perspective) { cam["field_of_view"] = c.FieldOfView; o["perspective_camera"] = cam; }
            else { cam["view_to_world_scale"] = c.ViewToWorldScale; o["orthogonal_camera"] = cam; }
            if (v.Lines.Count > 0) o["lines"] = new JsonArray([.. v.Lines.Select(l => (JsonNode)new JsonObject { ["start_point"] = Xyz(l.Start), ["end_point"] = Xyz(l.End) })]);
            if (v.ClippingPlanes.Count > 0) o["clipping_planes"] = new JsonArray([.. v.ClippingPlanes.Select(p => (JsonNode)new JsonObject { ["location"] = Xyz(p.Location), ["direction"] = Xyz(p.Direction) })]);
            o["components"] = new JsonObject
            {
                ["selection"] = new JsonArray([.. v.Selection.Select(s => (JsonNode)new JsonObject { ["ifc_guid"] = s })]),
                ["visibility"] = new JsonObject
                {
                    ["default_visibility"] = v.DefaultVisibility,
                    ["exceptions"] = new JsonArray([.. v.Exceptions.Select(s => (JsonNode)new JsonObject { ["ifc_guid"] = s })]),
                },
            };
        }
        if (v.Snapshot is { Length: > 0 } png) o["snapshot"] = new JsonObject { ["snapshot_type"] = png.Length > 3 && png[0] == 0xFF && png[1] == 0xD8 ? "jpg" : "png", ["snapshot_data"] = Convert.ToBase64String(png) };
        return o;
    }

    /// <summary>A server viewpoint as Lapin keeps it: the camera, lines and planes from its JSON, its components and snapshot from their routes.</summary>
    async Task<BcfViewpoint> ViewpointIn(JsonObject vj, string path)
    {
        var guid = Bcf.ParseGuid((string?)vj["guid"]) ?? Guid.NewGuid();
        BcfCamera? camera = null;
        if (vj["perspective_camera"] is JsonObject pc && Point(pc["camera_view_point"]) is { } pp && Point(pc["camera_direction"]) is { } pd)
            camera = new BcfCamera(true, pp, pd, Point(pc["camera_up_vector"]) ?? new(0, 0, 1), Lapin.Model.Json.Number(pc["field_of_view"]) ?? 60, 10, Lapin.Model.Json.Number(pc["aspect_ratio"]));
        else if (vj["orthogonal_camera"] is JsonObject oc && Point(oc["camera_view_point"]) is { } op && Point(oc["camera_direction"]) is { } od)
            camera = new BcfCamera(false, op, od, Point(oc["camera_up_vector"]) ?? new(0, 0, 1), 60, Lapin.Model.Json.Number(oc["view_to_world_scale"]) ?? 10, Lapin.Model.Json.Number(oc["aspect_ratio"]));
        var lines = (vj["lines"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(l => Point(l["start_point"]) is { } s && Point(l["end_point"]) is { } e ? new BcfLine(s, e) : null).OfType<BcfLine>().ToList();
        var planes = (vj["clipping_planes"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(p => Point(p["location"]) is { } l && Point(p["direction"]) is { } d ? new BcfClippingPlane(l, d) : null).OfType<BcfClippingPlane>().ToList();
        List<string> selection = [], exceptions = [];
        var defaultVisibility = true;
        if (camera != null)
        {
            var sel = await Get($"{path}/selection");
            if (sel.Ok && sel.Json?["selection"] is JsonArray sa) selection = sa.OfType<JsonObject>().Select(x => (string?)x["ifc_guid"]).OfType<string>().ToList();
            var vis = await Get($"{path}/visibility");
            if (vis.Ok && vis.Json?["visibility"] is JsonObject vo)
            {
                exceptions = (vo["exceptions"] as JsonArray ?? []).OfType<JsonObject>().Select(x => (string?)x["ifc_guid"]).OfType<string>().ToList();
                // "Nothing visible, no exception" is how a viewpoint without visibility reads: everything visible.
                defaultVisibility = vo["default_visibility"] is JsonValue dv && dv.TryGetValue<bool>(out var d) ? d || exceptions.Count == 0 : true;
            }
        }
        byte[]? snapshot = null;
        if (vj["snapshot"] is JsonObject)
        {
            var snap = await Get($"{path}/snapshot", bytes: true);
            if (snap.Ok) snapshot = snap.Bytes;
        }
        return new BcfViewpoint(guid, camera, selection, defaultVisibility, exceptions, planes, lines, snapshot, Lapin.Model.Json.Number(vj["index"]) is { } ix ? (int)ix : null);
    }

    static HashSet<Guid> Guids(JsonNode? n) => (n as JsonArray ?? []).Select(x => Bcf.ParseGuid((string?)x)).OfType<Guid>().ToHashSet();

    /// <summary>Two remarks alike, as the model would keep them (so that a sync that changes nothing makes no undo step).</summary>
    static bool Same(BcfTopic? a, BcfTopic? b)
    {
        if (a == null || b == null) return a == null && b == null;
        return a.Title == b.Title && JsonNode.DeepEquals(a.ToJson(), b.ToJson());
    }
}
