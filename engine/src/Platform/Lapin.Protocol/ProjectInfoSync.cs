using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Package.Remote;
using Lapin.Platform.Documents;

namespace Lapin.Protocol;

/// <summary>
/// The project's information synced with Atrium Server's project information record (docs/plan-archicad.md A1b; Atrium docs/atrium-server.md,
/// "Project information (A1b)", "What Lapin implements"), as <see cref="ReferenceSync"/> does for the register. The record has Lapin's keys and
/// shapes: the project's <c>name</c> and the <see cref="ProjectInfo.Grouped"/> fields under <c>fields</c> (texts), the project's own parameters
/// under <c>custom</c> by key (<c>{label, type, group, value?}</c>; Lapin keeps them as a list in its own order). One sync:
/// <list type="number">
/// <item>whether the server keeps the record (<c>project.info</c> among <c>GET /atrium/1.0/</c>'s features; an older server: nothing is done,
/// silently);</item>
/// <item>the record read (<c>GET {p}/info</c>, its ETag);</item>
/// <item>each member compared with the value last seen (the base): one changed here is sent on top of the server's (last writer wins per field),
/// one changed there comes here. A first sync takes the server's values and sends only what the server lacks. The changes go as one JSON merge
/// patch of the changed members (<c>null</c> clears one) with <c>If-Match</c>; a 412 reads the record again and sends again.</item>
/// </list>
/// <c>source</c> (what Archicad and IFC models said that no field took) is the server's and is never sent; Lapin's own <c>info.source</c> is
/// what its receives kept and is never overwritten by a sync. The caller applies <see cref="Outcome.Fields"/> and <see cref="Outcome.Custom"/>
/// in one undo step and keeps <see cref="Outcome.State"/> with the history (remotes.json), never in a commit.
/// </summary>
public sealed class ProjectInfoSync(HttpRemote server, string project)
{
    /// <summary>How many times a patch is sent again after a 412 before it is left for the next sync.</summary>
    public const int Retries = 3;

    public const string Feature = "project.info";

    /// <summary>What a sync did: the fields to set here (key → value, null clears; null: none), the parameters to write (null: as they are), the state, the report.</summary>
    public sealed record Outcome(JsonObject? Fields, List<ProjectInfo.Custom>? Custom, JsonObject State, JsonObject Report);

    readonly string _info = $"atrium/1.0/projects/{Uri.EscapeDataString(project)}/info";

    static RemoteException Fail(ApiAnswer a) => new(a.Status, a.Code, a.Message, a.Json as JsonObject);

    /// <summary>The field keys the record has, in Lapin's order: the name, then <see cref="ProjectInfo.Grouped"/>.</summary>
    public static IEnumerable<string> FieldKeys => new[] { "name" }.Concat(ProjectInfo.Fields.Select(f => f.Key));

    /// <summary>Whether the server keeps the project information (<c>project.info</c> among Atrium's features).</summary>
    public async Task<bool> OfferedAsync()
    {
        var a = await server.ApiAsync(HttpMethod.Get, "atrium/1.0/");
        return a.Ok && a.Json?["features"] is JsonArray f && f.Any(x => (string?)x == Feature);
    }

    // ------------------------------------------------------------ the values, flat

    static string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim().Length > 0 ? s.Trim() : null;

    /// <summary>A parameter as both sides compare it: label (the key by default), type (text), group (other), value when it has one.</summary>
    static JsonObject CustomNorm(string key, JsonObject? o)
    {
        var type = Text(o?["type"]) is { } t && ProjectInfo.CustomTypes.Contains(t) ? t : "text";
        var n = new JsonObject { ["label"] = Text(o?["label"]) ?? key, ["type"] = type, ["group"] = Text(o?["group"]) ?? "other" };
        if (Text(o?["value"]) is { } v) n["value"] = v;
        return n;
    }

    /// <summary>The project's information as flat members: a field's key → its text, <c>custom.&lt;key&gt;</c> → the parameter.</summary>
    public static Dictionary<string, JsonNode> Flat(IReadOnlyDictionary<string, string?> fields, IEnumerable<ProjectInfo.Custom> custom)
    {
        var o = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var k in FieldKeys) if (fields.TryGetValue(k, out var v) && v?.Trim() is { Length: > 0 } t) o[k] = JsonValue.Create(t);
        foreach (var c in custom) o["custom." + c.Key] = CustomNorm(c.Key, c.ToJson());
        return o;
    }

    /// <summary>A server record (or a kept base) as flat members; what is not Lapin's (<c>source</c>, the derived members) is left out.</summary>
    public static Dictionary<string, JsonNode> FlatOfRecord(JsonObject? record)
    {
        var o = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        var keys = FieldKeys.ToHashSet(StringComparer.Ordinal);
        foreach (var (k, v) in record?["fields"] as JsonObject ?? []) if (keys.Contains(k) && Text(v) is { } t) o[k] = JsonValue.Create(t);
        foreach (var (k, v) in record?["custom"] as JsonObject ?? []) o["custom." + k] = CustomNorm(k, v as JsonObject ?? (Text(v) is { } tv ? new JsonObject { ["value"] = tv } : null));
        return o;
    }

    static JsonObject RecordOf(Dictionary<string, JsonNode> flat)
    {
        var fields = new JsonObject();
        var custom = new JsonObject();
        foreach (var (k, v) in flat)
            if (k.StartsWith("custom.", StringComparison.Ordinal)) custom[k[7..]] = v.DeepClone(); else fields[k] = v.DeepClone();
        return new JsonObject { ["fields"] = fields, ["custom"] = custom };
    }

    static bool Same(JsonNode? a, JsonNode? b) => a == null || b == null ? a == null && b == null : JsonNode.DeepEquals(a, b);

    // ------------------------------------------------------------ one sync

    async Task<(JsonObject Record, string? ETag)> Read()
    {
        var a = await server.ApiAsync(HttpMethod.Get, _info);
        if (!a.Ok || a.Json is not JsonObject o) throw Fail(a);
        return (o, a.ETag ?? (string?)o["etag"]);
    }

    /// <summary>
    /// One sync of the project's <paramref name="fields"/> (<c>name</c> and the grouped fields; null or empty is none) and its own parameters
    /// (<paramref name="custom"/>, in its order), against the state <paramref name="previous"/> kept from the last one.
    /// </summary>
    public async Task<Outcome> RunAsync(IReadOnlyDictionary<string, string?> fields, IReadOnlyList<ProjectInfo.Custom> custom, JsonObject? previous, string serverUrl, DateTime now)
    {
        if (!await OfferedAsync())
            return new Outcome(null, null, previous?.DeepClone() as JsonObject ?? [], new JsonObject { ["offered"] = false, ["changedHere"] = 0, ["sent"] = 0, ["refused"] = new JsonArray() });

        var fresh = previous == null || (string?)previous["project"] != project || !string.Equals((string?)previous["server"], serverUrl, StringComparison.OrdinalIgnoreCase);
        var local = Flat(fields, custom);
        var baseFlat = fresh ? null : FlatOfRecord(previous!["base"] as JsonObject);
        var (record, etag) = await Read();
        var there = FlatOfRecord(record);
        var by = (string?)record["modifiedByName"];

        var all = new SortedSet<string>(local.Keys, StringComparer.Ordinal);
        all.UnionWith(there.Keys);
        if (baseFlat != null) all.UnionWith(baseFlat.Keys);
        // Changed here since the last sync; on a first one, only what the server lacks (the server is the truth).
        var changed = baseFlat != null
            ? all.Where(k => !Same(local.GetValueOrDefault(k), baseFlat.GetValueOrDefault(k))).ToList()
            : all.Where(k => local.ContainsKey(k) && !there.ContainsKey(k)).ToList();

        var refused = new JsonArray();
        int sent = 0, conflicts = 0;
        var pending = new List<string>();
        for (var attempt = 0; changed.Count > 0; attempt++)
        {
            var patchFields = new JsonObject();
            var patchCustom = new JsonObject();
            var keys = new List<string>();
            foreach (var k in changed)
            {
                var mine = local.GetValueOrDefault(k);
                if (Same(mine, there.GetValueOrDefault(k))) continue;
                keys.Add(k);
                if (k.StartsWith("custom.", StringComparison.Ordinal)) patchCustom[k[7..]] = mine?.DeepClone();
                else patchFields[k] = mine?.DeepClone();
            }
            if (keys.Count == 0) break;
            var patch = new JsonObject();
            if (patchFields.Count > 0) patch["fields"] = patchFields;
            if (patchCustom.Count > 0) patch["custom"] = patchCustom;
            var a = await server.ApiAsync(HttpMethod.Patch, _info, patch, etag);
            if (a.Ok && a.Json is JsonObject pj)
            {
                record = pj;
                etag = a.ETag ?? (string?)pj["etag"] ?? etag;
                there = FlatOfRecord(record);
                sent += keys.Count;
                break;
            }
            if (a.Status == 412)
            {
                conflicts++;
                (record, etag) = await Read();
                there = FlatOfRecord(record);
                if (attempt < Retries) continue;
                refused.Add((JsonNode)new JsonObject { ["code"] = "changed", ["message"] = "The project information kept changing on the server: sync again." });
                pending.AddRange(keys);      // kept here, to send again
                break;
            }
            if (a.Status == 401) throw Fail(a);
            // Only owners rename the project (the record's name is its name): the rest goes without the name, which the server's replaces.
            if (a.Status == 403 && a.Code == "owners_rename" && keys.Contains("name"))
            {
                refused.Add((JsonNode)new JsonObject { ["code"] = a.Code, ["message"] = a.Message, ["keys"] = new JsonArray("name") });
                changed.Remove("name");
                continue;
            }
            // A rule refused it (a viewer, a value the server refuses): the server's values come back.
            refused.Add((JsonNode)new JsonObject { ["code"] = a.Code, ["message"] = a.Message, ["keys"] = new JsonArray([.. keys.Select(k => (JsonNode)k)]) });
            break;
        }

        // What the project is to have: the server's, with what could not be sent kept.
        var target = new Dictionary<string, JsonNode>(there, StringComparer.Ordinal);
        foreach (var k in pending)
            if (local.TryGetValue(k, out var v)) target[k] = v.DeepClone(); else target.Remove(k);

        JsonObject? setFields = null;
        var changedHere = 0;
        foreach (var k in FieldKeys)
        {
            var want = target.GetValueOrDefault(k);
            if (Same(want, local.GetValueOrDefault(k))) continue;
            setFields ??= new JsonObject();
            setFields[k] = want?.DeepClone();
            changedHere++;
        }
        // The parameters in Lapin's order: those kept, changed in place; those the server added after them, in its order.
        List<ProjectInfo.Custom>? list = null;
        var customKeys = target.Keys.Where(k => k.StartsWith("custom.", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        var localCustom = local.Keys.Where(k => k.StartsWith("custom.", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        var customChanged = customKeys.Union(localCustom).Count(k => !Same(target.GetValueOrDefault(k), local.GetValueOrDefault(k)));
        if (customChanged > 0)
        {
            changedHere += customChanged;
            list = [];
            static ProjectInfo.Custom Of(string key, JsonNode n) =>
                new(key, Text(n["label"]) ?? key, Text(n["type"]) ?? "text", Text(n["group"]) ?? "other", Text(n["value"]));
            var serverOrder = (record["custom"] as JsonObject ?? []).Select(kv => "custom." + kv.Key).ToList();
            foreach (var c in custom) if (target.TryGetValue("custom." + c.Key, out var n)) list.Add(Of(c.Key, n));
            foreach (var k in serverOrder.Concat(customKeys.Order(StringComparer.Ordinal)).Distinct())
                if (customKeys.Contains(k) && !list.Any(c => "custom." + c.Key == k)) list.Add(Of(k[7..], target[k]));
        }

        var report = new JsonObject
        {
            ["offered"] = true, ["first"] = fresh, ["changedHere"] = changedHere, ["sent"] = sent, ["conflicts"] = conflicts, ["refused"] = refused,
            ["by"] = changedHere > 0 ? by : null, ["openProposals"] = (int?)record["openProposals"] ?? 0,
        };
        var state = new JsonObject
        {
            ["server"] = serverUrl, ["project"] = project, ["etag"] = etag,
            ["syncedAt"] = now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["base"] = RecordOf(there),
        };
        return new Outcome(setFields, list, state, report);
    }
}
