using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Package.Remote;
using Lapin.Platform.Cde.References;

namespace Lapin.Protocol;

/// <summary>
/// The project's reference documents synced with a CDE server's register (docs/plan-cde-references.md §2 "Both places", §3.5, stage R4;
/// decision 874), as <see cref="IssueSync"/> does for BCF topics. With a server remote the server is the truth: a document lives both in the
/// <c>.lapin</c> (<c>LapinReference</c>, by GlobalId) and in Atrium Server's register (<c>/atrium/1.0/projects/{p}/references</c>, the same
/// members). One sync:
/// <list type="number">
/// <item>whether the server keeps reference documents at all (<c>references</c> among <c>GET /atrium/1.0/</c>'s features; an older server:
/// nothing is done, silently), who the account is and its role;</item>
/// <item>the folder tree, the type list and the checklist offered (one version, <c>"s&lt;n&gt;"</c>), merged by key against the values last seen
/// and sent by an owner with <c>If-Match</c>; a first sync sends a project's own settings to a server still on the defaults;</item>
/// <item>the register read whole (every record with its ETag), the journal after the last seq read for who else changed what;</item>
/// <item>each document merged field by field against the server values last seen (the base): a field changed here is pushed on top of the
/// server's (last writer wins per field; <c>fields</c> key by key), a field changed there comes here. New documents go with their GlobalId
/// (those another names first), their files uploaded to lapin-remote's object store before they are named; superseding and withdrawing
/// through their own requests; deletions both ways as the role allows (a deletion refused brings the document back). Edits are JSON merge
/// patches of the changed fields with <c>If-Match</c>; a 412 reads the document again and sends again.</item>
/// <item>the server's files the project does not hold downloaded, up to the project's keep limit; above it only their hash and size are kept,
/// and <c>remote.referenceFile</c> fetches them when they are opened.</item>
/// </list>
/// Derived members (<c>expired</c>, <c>folderPath</c>, <c>typeName</c>, <c>supersedes</c>, <c>etag</c>, <c>textStatus</c>, <c>warnings</c>, the
/// names) are never written back; <c>expired</c> reads as <c>received</c>. The caller applies <see cref="Outcome.Documents"/> and
/// <see cref="Outcome.Settings"/> in one undo step and keeps <see cref="Outcome.State"/> with the history (remotes.json), never in a commit.
/// </summary>
public sealed class ReferenceSync(HttpRemote server, string project)
{
    /// <summary>The record's members synced (the server keeps the same; <c>addedAt</c>… are the server's and only come here).</summary>
    public static readonly string[] Fields =
        ["title", "type", "issuer", "number", "discipline", "date", "validFrom", "validUntil", "phase", "folder", "status", "files", "requires", "relatesTo", "fields",
         "requestedAt", "requestedBy", "supersededBy", "reason", "withdrawnAt"];

    /// <summary>The members a merge patch sets (<c>status</c> too, when requested or received; <c>fields</c> key by key).</summary>
    static readonly HashSet<string> Patchable =
        ["title", "type", "issuer", "number", "discipline", "date", "validFrom", "validUntil", "phase", "folder", "files", "requires", "relatesTo", "requestedAt", "requestedBy"];

    /// <summary>What the server sets and Lapin shows: taken from the server, never sent.</summary>
    static readonly string[] ServerOwn = ["addedAt", "addedBy", "modifiedAt", "modifiedBy"];

    /// <summary>The project's settings synced: the folder tree and its mappings (<c>PUT …/folders</c>), the types and the checklist offered (<c>PUT …/types</c>).</summary>
    public static readonly string[] SettingKeys = ["folders", "typeFolders", "roleFolders", "language", "types", "checklist"];

    static readonly string[] FolderKeys = ["folders", "typeFolders", "roleFolders", "language"];

    /// <summary>How many times an edit is sent again after a 412 before it is left for the next sync.</summary>
    public const int Retries = 3;

    /// <summary>A document as the project holds it: its GlobalId, title and attributes (the entity's).</summary>
    public sealed record Doc(string Id, string Title, JsonObject Attrs);

    /// <summary>
    /// What a sync did: the documents as they are to be here (null: removed), the settings to write (null: as they are), the state to keep,
    /// and the report for the interface.
    /// </summary>
    public sealed record Outcome(Dictionary<string, Doc?> Documents, JsonObject? Settings, JsonObject State, JsonObject Report);

    readonly string _r = $"atrium/1.0/projects/{Uri.EscapeDataString(project)}/references";
    readonly string _atrium = $"atrium/1.0/projects/{Uri.EscapeDataString(project)}";

    // ------------------------------------------------------------ the report

    int _created, _updated, _deleted, _conflicts, _added, _removed, _uploaded, _downloaded;
    string? _settings;
    readonly JsonArray _refused = [];
    readonly Dictionary<string, (string Who, string? Title, HashSet<string> What)> _others = new(StringComparer.Ordinal);
    readonly HashSet<string> _me = new(StringComparer.OrdinalIgnoreCase);

    void Refused(string id, string? title, string code, string message)
    {
        if (_refused.Count < 50) _refused.Add((JsonNode)new JsonObject { ["id"] = id, ["title"] = title, ["code"] = code, ["message"] = message });
    }

    // ------------------------------------------------------------ what the sync is given

    long _keepLimit;
    IReferenceFiles _store = new MemoryReferenceFiles();
    Func<Doc, ReferenceFile, byte[]?> _bytesOf = (_, _) => null;
    Dictionary<string, Doc> _local = new(StringComparer.Ordinal);

    static RemoteException Fail(ApiAnswer a) => new(a.Status, a.Code, a.Message, a.Json as JsonObject);

    async Task<JsonObject> Need(string path)
    {
        var a = await server.ApiAsync(HttpMethod.Get, path);
        if (!a.Ok || a.Json is not JsonObject o) throw Fail(a);
        return o;
    }

    /// <summary>Whether the server keeps reference documents (<c>references</c> among Atrium's features); false for an older server or another kind.</summary>
    public async Task<bool> OfferedAsync()
    {
        var a = await server.ApiAsync(HttpMethod.Get, "atrium/1.0/");
        return a.Ok && a.Json?["features"] is JsonArray f && f.Any(x => (string?)x == "references");
    }

    /// <summary>
    /// One sync. <paramref name="localSettings"/> are the project's six settings as it reads them (its own or the defaults), and
    /// <paramref name="localCustom"/> whether it has its own copy; <paramref name="bytesOf"/> finds a file's bytes here (the project's file,
    /// the project folder, where it was added from), null when they are not.
    /// </summary>
    public async Task<Outcome> RunAsync(IReadOnlyList<Doc> localDocs, JsonObject localSettings, bool localCustom, JsonObject? previous, string serverUrl, long keepLimit,
        IReferenceFiles store, Func<Doc, ReferenceFile, byte[]?> bytesOf, DateTime now)
    {
        _keepLimit = keepLimit;
        _store = store;
        _bytesOf = bytesOf;
        _local = localDocs.ToDictionary(d => d.Id, StringComparer.Ordinal);
        if (!await OfferedAsync())
            return new Outcome([], null, previous?.DeepClone() as JsonObject ?? [], new JsonObject { ["offered"] = false, ["changedHere"] = 0, ["refused"] = new JsonArray(), ["byOthers"] = new JsonArray() });

        var account = await Need("atrium/1.0/account");
        foreach (var k in new[] { "id", "email", "authorId" }) if ((string?)account[k] is { Length: > 0 } v) _me.Add(v);
        var me = (string?)account["authorId"] ?? (string?)account["id"] ?? "";
        var rules = await Need($"{_atrium}/rules");
        var role = (string?)rules["role"] ?? "viewer";
        await server.ReadyAsync();

        var fresh = previous == null || (string?)previous["project"] != project || !string.Equals((string?)previous["account"], me, StringComparison.OrdinalIgnoreCase)
            || !string.Equals((string?)previous["server"], serverUrl, StringComparison.OrdinalIgnoreCase);
        var baseDocs = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        if (!fresh && previous!["documents"] is JsonObject bd)
            foreach (var (k, v) in bd) if (v is JsonObject o) baseDocs[k] = o;

        // The settings first: a document may be in a folder or of a type the server learns now.
        var (settings, settingsState) = await SyncSettings(localSettings, localCustom, fresh ? null : previous!["settings"] as JsonObject, role);

        // Who else changed what since the last sync (the journal), and its last seq.
        long last = fresh ? 0 : (long?)previous!["last"] ?? 0;
        for (var page = 0; page < 1000; page++)
        {
            var j = await Need($"{_atrium}/events?after={last.ToString(CultureInfo.InvariantCulture)}&limit=1000");
            foreach (var e in (j["events"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if ((string?)e["referenceId"] is not { Length: > 0 } rid) continue;
                var who = (string?)e["author"] ?? "";
                if (!fresh && !_me.Contains(who)) Other(rid, (string?)e["authorName"] ?? who, (string?)e["type"] ?? "", e);
            }
            last = (long?)j["last"] ?? last;
            if (j["more"] is not JsonValue mv || !mv.TryGetValue<bool>(out var more) || !more) break;
        }

        // The register, whole: every record with its ETag.
        var list = await Need(_r);
        var onServer = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var d in (list["references"] as JsonArray ?? []).OfType<JsonObject>())
            if ((string?)d["id"] is { } id) onServer[id] = d;

        var result = new Dictionary<string, Doc?>(StringComparer.Ordinal);
        var state = new JsonObject();
        var all = new HashSet<string>(_local.Keys, StringComparer.Ordinal);
        all.UnionWith(onServer.Keys);
        all.UnionWith(baseDocs.Keys);
        // New documents first, those another names (in its checklist, as its successor) before it; then the others.
        var creates = CreateOrder(all.Where(id => _local.ContainsKey(id) && !onServer.ContainsKey(id) && !baseDocs.ContainsKey(id)).ToList());
        foreach (var id in creates.Concat(all.Where(id => !creates.Contains(id)).Order(StringComparer.Ordinal)))
        {
            _local.TryGetValue(id, out var mine);
            baseDocs.TryGetValue(id, out var known);
            onServer.TryGetValue(id, out var there);
            try
            {
                var (doc, kept, touched) = await One(id, mine, known, there, role);
                if (touched) result[id] = doc;
                if (kept != null) state[id] = kept;
                if (doc != null && there == null && kept != null) onServer[id] = (JsonObject)kept["base"]!;
            }
            catch (RemoteException e) when (e.Status is not 401)
            {
                // One document the server refuses does not stop the others: it stays as it is here, and its base as it was.
                Refused(id, mine?.Title, e.Code, e.Message);
                if (known != null) state[id] = known.DeepClone();
            }
        }

        foreach (var (key, o) in _others.ToList())
            if (o.Title == null)
                _others[key] = (o.Who, (result.TryGetValue(key, out var rd) ? rd?.Title : null) ?? _local.GetValueOrDefault(key)?.Title ?? (string?)onServer.GetValueOrDefault(key)?["title"], o.What);
        var report = new JsonObject
        {
            ["offered"] = true, ["changedHere"] = result.Count + (settings != null ? 1 : 0), ["added"] = _added, ["removed"] = _removed, ["created"] = _created, ["updated"] = _updated,
            ["deleted"] = _deleted, ["conflicts"] = _conflicts, ["uploaded"] = _uploaded, ["downloaded"] = _downloaded, ["settings"] = _settings, ["refused"] = _refused,
            ["byOthers"] = new JsonArray([.. _others.Values.Take(30).Select(o => (JsonNode)new JsonObject
            {
                ["who"] = o.Who, ["title"] = o.Title, ["what"] = new JsonArray([.. o.What.Order(StringComparer.Ordinal).Select(x => (JsonNode)x)]),
            })]),
            ["first"] = fresh,
        };
        var keptState = new JsonObject
        {
            ["server"] = serverUrl, ["project"] = project, ["account"] = me, ["role"] = role, ["last"] = last,
            ["syncedAt"] = now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["settings"] = settingsState, ["documents"] = state,
        };
        return new Outcome(result, settings, keptState, report);
    }

    void Other(string id, string who, string type, JsonObject e)
    {
        if (!_others.TryGetValue(id, out var o)) _others[id] = o = (who, null, new HashSet<string>(StringComparer.Ordinal));
        o.What.Add(type switch
        {
            "reference_added" => "added",
            "reference_deleted" => "deleted",
            "reference_superseded" => "superseded",
            "reference_withdrawn" => "withdrawn",
            "reference_expiring" => "expiring",
            "reference_read" => "read",
            _ => "edited",
        });
        if (type == "reference_added" && e["new"] is { } n && (n is JsonObject ? n : TryParse((string?)n)) is JsonObject rec && (string?)rec["title"] is { } t) _others[id] = (o.Who, t, o.What);
    }

    static JsonNode? TryParse(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        try { return JsonNode.Parse(s); }
        catch (System.Text.Json.JsonException) { return null; }
    }

    /// <summary>New documents in an order the server takes: one another names (as its successor, in its checklist) before it.</summary>
    List<string> CreateOrder(List<string> ids)
    {
        var set = ids.ToHashSet(StringComparer.Ordinal);
        var o = new List<string>();
        var done = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id, int depth)
        {
            if (done.Contains(id) || depth > 64) return;
            done.Add(id);
            var d = _local[id];
            var needs = new List<string>();
            if ((string?)d.Attrs["supersededBy"] is { } by) needs.Add(by);
            foreach (var item in (d.Attrs["requires"] as JsonArray ?? []).OfType<JsonObject>()) if ((string?)item["document"] is { } doc) needs.Add(doc);
            foreach (var n in needs) if (set.Contains(n)) Visit(n, depth + 1);
            o.Add(id);
        }
        foreach (var id in ids.Order(StringComparer.Ordinal)) Visit(id, 0);
        return o;
    }

    // ------------------------------------------------------------ the records, normalised

    static JsonNode Canon(JsonNode n) => JsonNode.Parse(n.ToJsonString())!;

    static JsonNode? Clean(JsonNode? v) => v switch
    {
        null => null,
        JsonValue s when s.TryGetValue<string>(out var t) => t.Trim().Length == 0 ? null : Canon(s),
        JsonArray { Count: 0 } => null,
        JsonObject { Count: 0 } => null,
        _ => Canon(v),
    };

    /// <summary>A record's synced members as both sides compare them: absent and empty alike, files as {name, sha256, size, mime}, <c>expired</c> as <c>received</c>.</summary>
    public static JsonObject Norm(string title, JsonObject a)
    {
        var o = new JsonObject { ["title"] = title };
        foreach (var f in Fields)
        {
            if (f == "title") continue;
            var v = a[f];
            if (f == "files")
            {
                var files = (v as JsonArray ?? []).OfType<JsonObject>().Select(x => (string?)x["name"] is { } name && (string?)x["sha256"] is { } sha
                    ? (JsonNode)new JsonObject
                    {
                        ["name"] = name, ["sha256"] = sha, ["size"] = (long)(Json.Number(x["size"]) ?? 0),
                        ["mime"] = (string?)x["mime"] is { Length: > 0 } m ? m : References.MimeOf(name),
                    }
                    : null).OfType<JsonNode>().ToArray();
                if (files.Length > 0) o["files"] = Canon(new JsonArray(files));
                continue;
            }
            if (Clean(v) is { } c) o[f] = c;
        }
        var status = (string?)o["status"];
        o["status"] = status is null or "expired" ? "received" : status;
        return o;
    }

    /// <summary>The keys a merge compares: every member, and each extra field on its own (<c>fields.&lt;key&gt;</c>).</summary>
    static IEnumerable<string> Keys(params JsonObject?[] records)
    {
        foreach (var f in Fields) if (f != "fields") yield return f;
        var extra = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var r in records) if (r?["fields"] is JsonObject fo) foreach (var (k, _) in fo) extra.Add(k);
        foreach (var k in extra) yield return "fields." + k;
    }

    static JsonNode? Get(JsonObject? o, string key) => o == null ? null : key.StartsWith("fields.", StringComparison.Ordinal) ? (o["fields"] as JsonObject)?[key[7..]] : o[key];

    static void Set(JsonObject o, string key, JsonNode? v)
    {
        v = Clean(v);
        if (key.StartsWith("fields.", StringComparison.Ordinal))
        {
            var fo = o["fields"] as JsonObject ?? new JsonObject();
            if (v == null) fo.Remove(key[7..]); else fo[key[7..]] = v;
            if (fo.Count == 0) o.Remove("fields"); else o["fields"] = fo;
            return;
        }
        if (v == null) o.Remove(key); else o[key] = v;
    }

    static bool SameValue(JsonNode? a, JsonNode? b)
    {
        a = Clean(a); b = Clean(b);
        if (a == null || b == null) return a == null && b == null;
        return JsonNode.DeepEquals(a, b);
    }

    static JsonObject NormOf(Doc d) => Norm(d.Title, d.Attrs);

    static JsonObject NormOfServer(JsonObject rec) => Norm((string?)rec["title"] ?? "", rec);

    // ------------------------------------------------------------ one document

    /// <summary>
    /// One document synced: <paramref name="mine"/> (null: not here, or deleted here), <paramref name="known"/> (its state: the base and ETag;
    /// null when never synced), <paramref name="there"/> (the server's record; null when it has none). Gives the document as it is to be here,
    /// its new state (null: forgotten), and whether the project changes.
    /// </summary>
    async Task<(Doc? Doc, JsonObject? State, bool Touched)> One(string id, Doc? mine, JsonObject? known, JsonObject? there, string role)
    {
        if (there == null)
        {
            if (mine == null) return (null, null, false);
            if (known != null) { _removed++; return (null, null, true); }      // deleted on the server: gone here too
            return await Create(id, mine, role);
        }
        if (mine == null)
        {
            if (known != null)
            {
                // Deleted here: owners alone delete; a deletion refused brings the document back.
                var del = await server.ApiAsync(HttpMethod.Delete, $"{_r}/{Uri.EscapeDataString(id)}", null, (string?)there["etag"]);
                if (del.Ok || del.Status == 404) { _deleted++; return (null, null, false); }
                if (del.Status == 412)
                {
                    // Changed there since: theirs comes back, to be deleted again knowingly.
                    _conflicts++;
                    var back412 = await Read(id) ?? there;
                    return (await FromServer(back412, null), State(back412), true);
                }
                Refused(id, (string?)there["title"], del.Status == 403 ? "delete_forbidden" : del.Code, del.Message);
                _added++;
                return (await FromServer(there, null), State(there), true);
            }
            _added++;
            return (await FromServer(there, null), State(there), true);
        }

        var local = NormOf(mine);
        var baseRec = known?["base"] as JsonObject;
        var server0 = NormOfServer(there);
        // The members changed here since the last sync (none on a first sync: the server is the truth).
        var changed = baseRec == null ? [] : Keys(local, baseRec).Where(k => !SameValue(Get(local, k), Get(baseRec, k))).ToList();
        if (changed.Count == 0)
        {
            var doc = await FromServer(there, mine);
            return (doc, State(there), !Same(mine, doc));
        }

        var current = there;
        var etag = (string?)there["etag"];
        var pending = new List<string>();
        if (changed.Contains("files")) await Upload(mine, local);
        for (var attempt = 0; ; attempt++)
        {
            var s = NormOfServer(current);
            var patch = new JsonObject();
            foreach (var k in changed)
            {
                if (SameValue(Get(local, k), Get(s, k))) continue;
                if (k.StartsWith("fields.", StringComparison.Ordinal))
                {
                    var fo = patch["fields"] as JsonObject ?? new JsonObject();
                    fo[k[7..]] = Get(local, k)?.DeepClone();
                    patch["fields"] = fo;
                }
                else if (Patchable.Contains(k)) patch[k] = Get(local, k)?.DeepClone();
                else if (k == "status" && (string?)local["status"] is "requested" or "received") patch["status"] = (string?)local["status"];
            }
            if (patch["title"] == null) patch.Remove("title");                    // a record keeps its title
            ApiAnswer? refusal = null;
            var conflicted = false;
            if (patch.Count > 0)
            {
                var a = await server.ApiAsync(HttpMethod.Patch, $"{_r}/{Uri.EscapeDataString(id)}", patch, etag);
                if (a.Ok && a.Json is JsonObject pj) { current = pj; etag = a.ETag ?? (string?)pj["etag"] ?? etag; _updated++; }
                else if (a.Status == 412) conflicted = true;
                else refusal = a;
            }
            // Superseding and withdrawing go through their own requests.
            if (!conflicted && refusal == null && changed.Contains("status"))
            {
                var want = (string?)local["status"];
                var have = (string?)NormOfServer(current)["status"];
                if (want != have && want is "superseded" or "withdrawn")
                {
                    var (path, body) = want == "superseded"
                        ? ("supersede", new JsonObject { ["by"] = (string?)local["supersededBy"] })
                        : ("withdraw", new JsonObject { ["reason"] = (string?)local["reason"] ?? "Withdrawn in Lapin." });
                    var a = await server.ApiAsync(HttpMethod.Post, $"{_r}/{Uri.EscapeDataString(id)}/{path}", body, etag);
                    if (a.Ok && a.Json is JsonObject aj) { current = aj; etag = a.ETag ?? (string?)aj["etag"] ?? etag; _updated++; }
                    else if (a.Status == 412) conflicted = true;
                    else refusal = a;
                }
            }
            if (conflicted && attempt < Retries)
            {
                _conflicts++;
                current = await Read(id) ?? current;
                etag = (string?)current["etag"];
                continue;
            }
            if (refusal != null || conflicted)
            {
                var a = refusal;
                Refused(id, mine.Title, a?.Code ?? "changed", a?.Message ?? "The document kept changing on the server: sync again.");
                // A rule refused it (your role, a value the server refuses): the server's values come back. A file not on the server yet, or a
                // document it names that it does not have yet: the change stays here, to send again.
                if (a == null || a.Code is "object_missing" or "unknown_document" or "changed") pending.AddRange(changed);
            }
            break;
        }
        var merged = await FromServer(current, mine, pending.Count > 0 ? (local, pending) : null);
        return (merged, State(current), !Same(mine, merged));
    }

    /// <summary>A document made here, sent with its GlobalId (its files uploaded first), then superseded or withdrawn when it is.</summary>
    async Task<(Doc? Doc, JsonObject? State, bool Touched)> Create(string id, Doc mine, string role)
    {
        if (role == "viewer")
        {
            Refused(id, mine.Title, "forbidden", "Your role in this project (Viewer) does not add reference documents.");
            return (mine, null, false);
        }
        var local = NormOf(mine);
        await Upload(mine, local);
        var body = (JsonObject)local.DeepClone();
        foreach (var k in new[] { "supersededBy", "reason", "withdrawnAt" }) body.Remove(k);
        if ((string?)body["status"] is not ("requested" or "received")) body["status"] = body["files"] is JsonArray { Count: > 0 } ? "received" : "requested";
        body["id"] = id;
        var a = await server.ApiAsync(HttpMethod.Post, _r, body);
        JsonObject rec;
        if (a.Ok && a.Json is JsonObject cj) { rec = cj; _created++; }
        else if (a.Status == 409 && a.Code == "exists" && await Read(id) is { } existing) rec = existing;   // made meanwhile: the server's
        else
        {
            Refused(id, mine.Title, a.Code, a.Message);
            return (mine, null, false);
        }
        var want = (string?)local["status"];
        if (want is "superseded" or "withdrawn")
        {
            var (path, b) = want == "superseded"
                ? ("supersede", new JsonObject { ["by"] = (string?)local["supersededBy"] })
                : ("withdraw", new JsonObject { ["reason"] = (string?)local["reason"] ?? "Withdrawn in Lapin." });
            var s = await server.ApiAsync(HttpMethod.Post, $"{_r}/{Uri.EscapeDataString(id)}/{path}", b, a.ETag ?? (string?)rec["etag"]);
            if (s.Ok && s.Json is JsonObject sj) rec = sj;
            else Refused(id, mine.Title, s.Code, s.Message);
        }
        var doc = await FromServer(rec, mine, want is "superseded" or "withdrawn" && (string?)NormOfServer(rec)["status"] != want ? (local, ["status", "supersededBy", "reason", "withdrawnAt"]) : null);
        return (doc, State(rec), !Same(mine, doc));
    }

    async Task<JsonObject?> Read(string id)
    {
        var a = await server.ApiAsync(HttpMethod.Get, $"{_r}/{Uri.EscapeDataString(id)}");
        if (a.Status == 404) return null;
        if (!a.Ok || a.Json is not JsonObject o) throw Fail(a);
        if (a.ETag != null) o["etag"] = a.ETag;
        return o;
    }

    static JsonObject State(JsonObject rec) => new() { ["etag"] = (string?)rec["etag"], ["base"] = NormOfServer(rec) };

    /// <summary>The files a document names that the server's object store lacks, uploaded (lapin-remote's objects, as a push sends them).</summary>
    async Task Upload(Doc mine, JsonObject local)
    {
        var files = (local["files"] as JsonArray ?? []).OfType<JsonObject>().Select(f => (Sha: (string)f["sha256"]!, Name: (string)f["name"]!)).ToList();
        if (files.Count == 0) return;
        var shas = files.Select(f => f.Sha).Where(ObjectIds.Valid).Distinct(StringComparer.Ordinal).ToList();
        var missing = new List<string>();
        for (var i = 0; i < shas.Count; i += Math.Max(1, server.MaxBatch))
            missing.AddRange(await server.MissingAsync(shas.Skip(i).Take(Math.Max(1, server.MaxBatch)).ToList()));
        var records = References.Files(new Entity(mine.Id, References.Class, mine.Title, mine.Attrs));
        foreach (var sha in missing)
        {
            var file = records.FirstOrDefault(f => f.Sha256 == sha);
            if (file == null) continue;
            if (_bytesOf(mine, file) is not { } bytes || ObjectIds.Of(bytes) != sha)
            {
                Refused(mine.Id, mine.Title, "file_not_here", $"{file.Name} is not held in this project nor found on this computer: it cannot be sent to the server.");
                continue;
            }
            await server.PutAsync(sha, bytes, ObjectKind.Blob);
            _uploaded++;
        }
    }

    /// <summary>
    /// The document as the server has it, as an entity's attributes: its synced members, the server's own (<c>addedAt</c>…), and its files
    /// with what is held here (the project's held files kept; a file the project lacks downloaded up to the keep limit, else only its hash and
    /// size). <paramref name="keep"/> puts back members changed here the server did not take yet.
    /// </summary>
    async Task<Doc> FromServer(JsonObject rec, Doc? mine, (JsonObject Local, IReadOnlyList<string> Keys)? keep = null)
    {
        var id = (string)rec["id"]!;
        var norm = NormOfServer(rec);
        if (keep is { } kp) foreach (var k in kp.Keys) Set(norm, k, Get(kp.Local, k)?.DeepClone());
        var attrs = new JsonObject();
        foreach (var (k, v) in norm) if (k != "title" && k != "files") attrs[k] = v?.DeepClone();
        foreach (var k in ServerOwn) if (Clean(rec[k]) is { } v) attrs[k] = v;
        var mineFiles = mine == null ? [] : References.Files(new Entity(mine.Id, References.Class, mine.Title, mine.Attrs));
        var files = new JsonArray();
        foreach (var f in (norm["files"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var name = (string)f["name"]!;
            var sha = (string)f["sha256"]!;
            var size = (long)(Json.Number(f["size"]) ?? 0);
            var here = mineFiles.FirstOrDefault(x => x.Sha256 == sha);
            var held = _store.Has(sha);
            if (!held && size <= _keepLimit)
            {
                var bytes = await server.GetAsync(sha);
                if (bytes == null)
                {
                    var a = await server.ApiAsync(HttpMethod.Get, $"{_r}/{Uri.EscapeDataString(id)}/files/{Uri.EscapeDataString(name)}", bytes: true);
                    if (a.Ok && a.Bytes is { } b && ObjectIds.Of(b) == sha) bytes = b;
                }
                if (bytes != null) { _store.Put(bytes); held = true; _downloaded++; }
            }
            files.Add((JsonNode)new ReferenceFile(name, sha, size, (string?)f["mime"], held, held ? null : here?.Source).ToJson());
        }
        attrs["files"] = files;
        return new Doc(id, (string?)norm["title"] ?? mine?.Title ?? id, attrs);
    }

    /// <summary>Two documents alike, as the model would keep them (so that a sync that changes nothing makes no undo step).</summary>
    static bool Same(Doc? a, Doc? b)
    {
        if (a == null || b == null) return a == null && b == null;
        return a.Title == b.Title && JsonNode.DeepEquals(Canon(a.Attrs), Canon(b.Attrs));
    }

    // ------------------------------------------------------------ the folders, types and checklist

    /// <summary>A folder tree as kept: each folder's id, name, children and <c>derived</c>, without the counts the server adds.</summary>
    static JsonArray StripFolders(JsonArray? tree) => new([.. (tree ?? []).OfType<JsonObject>().Select(n =>
    {
        var o = new JsonObject { ["id"] = n["id"]?.DeepClone(), ["name"] = n["name"]?.DeepClone(), ["children"] = StripFolders(n["children"] as JsonArray) };
        if ((string?)n["derived"] is { Length: > 0 } d) o["derived"] = d;
        return (JsonNode)o;
    })]);

    static JsonArray StripChecklist(JsonArray? items) => new([.. (items ?? []).OfType<JsonObject>().Select(x =>
    {
        var o = (JsonObject)x.DeepClone();
        o.Remove("text");
        return (JsonNode)o;
    })]);

    static JsonObject ServerSettings(JsonObject folders, JsonObject types) => new()
    {
        ["folders"] = StripFolders(folders["folders"] as JsonArray), ["typeFolders"] = folders["typeFolders"]?.DeepClone() ?? new JsonObject(),
        ["roleFolders"] = folders["roleFolders"]?.DeepClone() ?? new JsonObject(), ["language"] = (string?)folders["language"] ?? "en",
        ["types"] = types["types"]?.DeepClone() ?? new JsonArray(), ["checklist"] = StripChecklist(types["checklist"] as JsonArray),
    };

    static bool Custom(JsonObject o) => o["custom"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>
    /// The settings merged by key against the values last seen: those changed here sent (owners only; If-Match on their shared version, a 412
    /// read again), those changed there taken. A first sync sends a project's own settings to a server still on the defaults, and otherwise
    /// takes the server's. Gives the settings to write here (null: none) and their state.
    /// </summary>
    async Task<(JsonObject? Settings, JsonObject State)> SyncSettings(JsonObject local, bool localCustom, JsonObject? known, string role)
    {
        async Task<(JsonObject Values, string? ETag, bool Custom)> ReadAll()
        {
            var f = await server.ApiAsync(HttpMethod.Get, $"{_r}/folders");
            if (!f.Ok || f.Json is not JsonObject fo) throw Fail(f);
            var t = await server.ApiAsync(HttpMethod.Get, $"{_r}/types");
            if (!t.Ok || t.Json is not JsonObject to) throw Fail(t);
            return (ServerSettings(fo, to), f.ETag ?? (string?)fo["etag"], Custom(fo) || Custom(to));
        }
        var (values, etag, custom) = await ReadAll();
        var baseValues = known?["base"] as JsonObject;
        var changed = baseValues != null
            ? SettingKeys.Where(k => !SameValue(local[k], baseValues[k])).ToList()
            : localCustom && !custom ? SettingKeys.Where(k => !SameValue(local[k], values[k])).ToList() : [];
        if (changed.Count > 0 && role != "owner")
        {
            Refused("settings", null, "forbidden", "Only the project's owners change the folders and the document types on the server: the server's come back.");
            changed = [];
        }
        for (var attempt = 0; changed.Count > 0; attempt++)
        {
            ApiAnswer? refusal = null;
            var conflicted = false;
            var folderBody = new JsonObject();
            foreach (var k in FolderKeys.Where(changed.Contains).Where(k => !SameValue(local[k], values[k])))
            {
                if (k is "typeFolders" or "roleFolders")
                {
                    // Given over the server's mapping: the keys this project lacks taken out.
                    var map = (JsonObject)(local[k]?.DeepClone() ?? new JsonObject());
                    foreach (var (sk, _) in values[k] as JsonObject ?? []) if (!map.ContainsKey(sk)) map[sk] = null;
                    folderBody[k] = map;
                }
                else folderBody[k] = local[k]?.DeepClone();
            }
            if (folderBody.Count > 0)
            {
                var a = await server.ApiAsync(HttpMethod.Put, $"{_r}/folders", folderBody, etag);
                if (a.Ok) { etag = a.ETag ?? etag; _settings = "sent"; }
                else if (a.Status == 412) conflicted = true;
                else refusal = a;
            }
            var typesBody = new JsonObject();
            foreach (var k in new[] { "types", "checklist" }.Where(changed.Contains).Where(k => !SameValue(local[k], values[k]))) typesBody[k] = local[k]?.DeepClone();
            if (!conflicted && refusal == null && typesBody.Count > 0)
            {
                var a = await server.ApiAsync(HttpMethod.Put, $"{_r}/types", typesBody, etag);
                if (a.Ok) { etag = a.ETag ?? etag; _settings = "sent"; }
                else if (a.Status == 412) conflicted = true;
                else refusal = a;
            }
            (values, etag, custom) = await ReadAll();
            if (conflicted && attempt < Retries) { _conflicts++; continue; }
            if (refusal != null) Refused("settings", null, refusal.Code, refusal.Message);
            break;
        }
        var state = new JsonObject { ["etag"] = etag, ["base"] = values.DeepClone() };
        if (SettingKeys.All(k => SameValue(local[k], values[k]))) return (null, state);
        _settings ??= "received";
        return ((JsonObject)values.DeepClone(), state);
    }
}
