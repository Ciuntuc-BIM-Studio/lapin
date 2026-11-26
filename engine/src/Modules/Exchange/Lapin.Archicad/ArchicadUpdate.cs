using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Archicad;

/// <summary>
/// A project's link to an Archicad model it received (docs/plan-archicad.md A2): the Archicad state captured when it was received
/// (Tapir's <c>CaptureElementState</c>, by modification stamps; it lives in Archicad's memory for that session and project), the port,
/// the project's name and stories (which tell it from another), when, the receive's options, and a stamp of what the project held of
/// it then. Kept as this copy's state (<see cref="ICopyState"/>, the history's <c>connectors</c>): never in the model, a commit or an
/// undo step. The stamp tells an update whether the project still holds what the state was captured with: an undo, or elements of
/// the model deleted here, make it read the whole model again.
/// </summary>
public sealed record ArchicadLink(string Key, string StateId, int Port, string? Project, bool Untitled, IReadOnlyList<string> Stories, string At, string Stamp, JsonObject Options)
{
    public const string StateKey = "archicad";

    public JsonObject ToJson() => new()
    {
        ["stateId"] = StateId, ["port"] = Port, ["project"] = Project, ["untitled"] = Untitled,
        ["stories"] = new JsonArray([.. Stories.Select(s => (JsonNode)s)]), ["at"] = At, ["stamp"] = Stamp, ["options"] = Options.DeepClone(),
    };

    public static ArchicadLink? From(string key, JsonObject? o) =>
        o == null || (string?)o["stateId"] is not { Length: > 0 } id ? null
        : new(key, id, (int)(Json.Number(o["port"]) ?? ArchicadClient.FirstPort), (string?)o["project"], o["untitled"] is JsonValue u && u.TryGetValue<bool>(out var ub) && ub,
            [.. (o["stories"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>()], (string?)o["at"] ?? "", (string?)o["stamp"] ?? "", o["options"] as JsonObject ?? new JsonObject());

    /// <summary>Every link the project keeps, by model key.</summary>
    public static Dictionary<string, ArchicadLink> All(ICopyState? state)
    {
        var o = new Dictionary<string, ArchicadLink>(StringComparer.Ordinal);
        if (state?.Get(StateKey)?["links"] is JsonObject links)
            foreach (var (k, v) in links) if (From(k, v as JsonObject) is { } l) o[k] = l;
        return o;
    }

    public static void Keep(ICopyState state, ArchicadLink link)
    {
        var o = state.Get(StateKey) ?? new JsonObject();
        if (o["links"] is not JsonObject links) o["links"] = links = new JsonObject();
        links[link.Key] = link.ToJson();
        state.Set(StateKey, o);
    }

    /// <summary>
    /// What the project holds of a model, in one hash: each entity's id, its Archicad guid and hash. Received or updated, the link keeps
    /// it; an undo or an element deleted here changes it.
    /// </summary>
    public static string StampOf(IModelView m, string key)
    {
        var lines = ArchicadReceive.Existing(m, key).Select(e => $"{e.Id}|{(string?)(e.Attrs[ArchicadReceive.Key] as JsonObject)?["guid"]}|{(string?)(e.Attrs[ArchicadReceive.Key] as JsonObject)?["hash"]}")
            .Order(StringComparer.Ordinal);
        return Hashes.Sha256Hex(string.Join("\n", lines))[..32];
    }
}

/// <summary>What Archicad says changed since a state: the new, modified and deleted elements' guids.</summary>
public sealed record ArchicadChanges(IReadOnlyList<string> New, IReadOnlyList<string> Modified, IReadOnlyList<string> Deleted);

/// <summary>Archicad no longer knows a state (restarted, the project closed, or another project open).</summary>
public sealed class ArchicadStateLost(string message) : Exception(message);

/// <summary>
/// Get latest from Archicad (docs/plan-archicad.md A2): <c>archicad.update</c>, one undo step, and the cheap <c>archicad.status</c>.
/// <para>
/// An update asks Archicad what changed since the state kept with the model's link (<c>GetElementChangesSince</c>), reads only the new
/// and modified elements (with a wall's doors and windows and a door's or window's wall, and every element of a composite that changed),
/// removes what Archicad deleted, and brings the rest in as a receive does (<see cref="ArchicadReceive.Apply"/>: in place by GlobalId,
/// the same recognition and fidelity gate); then it captures a new state and releases the old one. It falls back to reading the whole
/// model and comparing each element's hash when there is no state to ask from: none kept (a model received before A2), Archicad no
/// longer knows it (restarted, or the project closed and opened again), or the project no longer holds what it was captured with (an
/// undo). A whole read that shares no element with what the project received is another Archicad project: refused.
/// </para>
/// </summary>
public static class ArchicadUpdate
{
    /// <summary>The state of the project's elements captured now (modification stamps), or null when Archicad cannot (a note says so).</summary>
    public static string? Capture(ArchicadClient c, CancellationToken ct, List<string>? notes = null)
    {
        try { return (string?)c.Tapir("CaptureElementState", new JsonObject { ["type"] = "ModificationStamp" }, ct)?["stateId"]?["guid"]; }
        catch (CommandException ex)
        {
            if (ArchicadClient.Busy(ex.Message)) throw new CommandException(ArchicadClient.BusyText(ex.Message));
            notes?.Add($"Archicad's state was not captured ({ex.Message}): Get latest from Archicad will read the whole model.");
            return null;
        }
    }

    /// <summary>A state freed in Archicad; nothing when it is gone already.</summary>
    public static void Release(ArchicadClient c, string? stateId)
    {
        if (stateId is not { Length: > 0 }) return;
        try { c.Tapir("ReleaseElementState", new JsonObject { ["stateId"] = new JsonObject { ["guid"] = stateId } }); }
        catch (CommandException) { }
    }

    /// <summary>What changed in Archicad since a state; <see cref="ArchicadStateLost"/> when Archicad no longer knows it, a busy refusal when a dialog is open.</summary>
    public static ArchicadChanges Changes(ArchicadClient c, string stateId, CancellationToken ct)
    {
        JsonObject? r;
        try { r = c.Tapir("GetElementChangesSince", new JsonObject { ["stateId"] = new JsonObject { ["guid"] = stateId } }, ct); }
        catch (CommandException ex) when (ArchicadClient.Busy(ex.Message)) { throw new CommandException(ArchicadClient.BusyText(ex.Message)); }
        catch (CommandException ex) when (!ex.Message.Contains("no longer answers", StringComparison.Ordinal)) { throw new ArchicadStateLost(ex.Message); }
        static List<string> Guids(JsonNode? n) => [.. (n as JsonArray ?? []).OfType<JsonObject>().Select(e => (string?)e["elementId"]?["guid"]).OfType<string>()];
        return new(Guids(r?["newElements"]), Guids(r?["modifiedElements"]), Guids(r?["deletedElements"]));
    }

    static HashSet<string> Known(IModelView m, string key) =>
        ArchicadReceive.Existing(m, key).Select(e => (string?)(e.Attrs[ArchicadReceive.Key] as JsonObject)?["guid"]).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The doors and windows the project knows in a wall, by the wall's guid (and, for those received before A2 kept their wall, all of them).</summary>
    static Func<string, IEnumerable<string>> FillsOf(IModelView m, string key)
    {
        var byHost = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var unknown = new List<string>();
        foreach (var e in ArchicadReceive.Existing(m, key))
        {
            if (e.Attrs[ArchicadReceive.Key] is not JsonObject meta || (string?)meta["role"] is not (null or "element") || (string?)meta["type"] is not ("Door" or "Window") || (string?)meta["guid"] is not { } g) continue;
            if ((string?)meta["host"] is { } h) (byHost.TryGetValue(h, out var l) ? l : byHost[h] = []).Add(g);
            else unknown.Add(g);
        }
        return wall => (byHost.GetValueOrDefault(wall) ?? []).Concat(unknown);
    }

    /// <summary>The elements made of a composite Archicad changed since its type was tagged (its hash), by their guids; asks Archicad the tagged composites only.</summary>
    static HashSet<string> OfChangedComposites(ArchicadClient c, IModelView m, string key, CancellationToken ct)
    {
        var o = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tagged = ArchicadTypes.Tagged(m);
        if (tagged.Count == 0) return o;
        JsonObject? r;
        try { r = c.Tapir("GetComposites", new JsonObject { ["attributeIds"] = new JsonArray([.. tagged.Keys.Order(StringComparer.Ordinal).Select(g => (JsonNode)new JsonObject { ["attributeId"] = new JsonObject { ["guid"] = g } })]) }, ct); }
        catch (CommandException) { return o; }
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var comp in (r?["composites"] as JsonArray ?? []).OfType<JsonObject>())
            if ((string?)comp["attributeId"]?["guid"] is { } g && tagged.TryGetValue(g, out var hash) && hash != ArchicadTypes.Hash(comp)) changed.Add(g);
        if (changed.Count == 0) return o;
        foreach (var e in ArchicadReceive.Existing(m, key))
            if (e.Attrs[ArchicadReceive.Key] is JsonObject meta && (string?)meta["composite"] is { } cg && changed.Contains(cg) && (string?)meta["guid"] is { } g) o.Add(g);
        return o;
    }

    /// <summary>The names Archicad now gives the building materials the project's materials are tagged with (none when it cannot say).</summary>
    static Dictionary<string, string> MaterialNames(ArchicadClient c, IModelView m, CancellationToken ct)
    {
        var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tagged = ArchicadTypes.TaggedMaterials(m);
        if (tagged.Count == 0) return o;
        try
        {
            var r = c.Tapir("GetBuildingMaterials", new JsonObject { ["attributeIds"] = new JsonArray([.. tagged.Select(g => (JsonNode)new JsonObject { ["attributeId"] = new JsonObject { ["guid"] = g } })]) }, ct);
            foreach (var bm in (r?["buildingMaterials"] as JsonArray ?? []).OfType<JsonObject>())
                if ((string?)bm["attributeId"]?["guid"] is { } g && (string?)bm["name"] is { Length: > 0 } n) o[g] = n;
        }
        catch (CommandException) { }
        return o;
    }

    /// <summary>The model's link for the Archicad answering on <paramref name="c"/>: by its key, else (a project saved under a name since) the link whose stories are its.</summary>
    static (string Key, ArchicadLink? Link) Find(ArchicadClient c, ArchicadSource source, bool asCopy, Dictionary<string, ArchicadLink> links, CancellationToken ct)
    {
        var key = ArchicadReceive.ModelKey(source, asCopy);
        if (links.TryGetValue(key, out var link)) return (key, link);
        List<string> stories;
        try { stories = [.. (c.Tapir("GetStories", null, ct)?["stories"] as JsonArray ?? []).OfType<JsonObject>().Select(s => (string?)s["ifcId"]).OfType<string>()]; }
        catch (CommandException) { stories = []; }
        var same = links.Values.Where(l => l.Key.EndsWith("|copy", StringComparison.Ordinal) == asCopy && l.Stories.Count > 0 && l.Stories.Intersect(stories, StringComparer.Ordinal).Any())
            .OrderByDescending(l => l.At, StringComparer.Ordinal).FirstOrDefault();
        return same != null ? (same.Key, same) : (key, null);
    }

    /// <summary>Get latest from Archicad: in the command's undo step; the receive's result, with how (<c>mode</c>: incremental, full) and why a full read.</summary>
    public static JsonObject Run(ICommandContext ctx, ArchicadClient c, JsonObject args)
    {
        var ct = ctx.Query.Cancellation;
        var progress = ctx.Query.Progress;
        var copy = ctx.Query.CopyState;
        var m = ctx.Model;
        progress?.Invoke("Asking Archicad what changed", 0, 0);
        var me = c.Describe(ct);
        if (!me.Ok) throw new CommandException(me.Problem!);
        var source = new ArchicadSource(c.Port, me.Project, me.Untitled, me.Archicad, me.Tapir);
        var asCopy = args["asCopy"] is JsonValue av && av.TryGetValue<bool>(out var ab) && ab;
        var links = ArchicadLink.All(copy);
        var (key, link) = Find(c, source, asCopy, links, ct);
        var known = Known(m, key);
        if (link == null && known.Count == 0)
            throw new CommandException(links.Count > 0
                ? $"This project received {string.Join(", ", links.Values.Select(l => $"„{l.Project ?? "an untitled Archicad project"}”").Distinct())}; Archicad has {(me.Project is { } p ? $"„{p}”" : "an untitled project")} open. Open that project in Archicad, or receive this one (File → Collaborate → Archicad: Receive…)."
                : "Nothing was received from Archicad into this project yet: File → Collaborate → Archicad: Receive… first.");
        var options = (link != null ? ArchicadReceiveOptions.From(link.Options) : ArchicadReceiveOptions.From(args)) with { Port = c.Port, AsCopy = asCopy, Key = key };
        string? why = link == null ? "no Archicad state was kept for this model (it was received before updates existed)"
            : link.Stamp != ArchicadLink.StampOf(m, key) ? "the project's elements from Archicad changed here since (an undo, or elements deleted)" : null;
        var notes = new List<string>();
        var next = Capture(c, ct, notes);
        try
        {
            ArchicadChanges? changes = null;
            if (why == null)
                try { changes = Changes(c, link!.StateId, ct); }
                catch (ArchicadStateLost) { why = "Archicad no longer knows the state kept (it was restarted, or the project closed and opened again)"; }
            JsonObject result;
            if (changes != null)
            {
                var deleted = changes.Deleted.Where(known.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var forced = OfChangedComposites(c, m, key, ct);
                var want = changes.New.Concat(changes.Modified).Concat(forced).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (want.Count == 0 && deleted.Count == 0)
                {
                    // No element changed: a building material renamed renames its material; the state kept still holds, the one just captured is not needed.
                    var renamed = ArchicadTypes.RenameMaterials(ctx, MaterialNames(c, m, ct));
                    Release(c, next);
                    next = null;
                    var none = Nothing(m, key, source);
                    none["types"] = new JsonObject
                    {
                        ["updated"] = new JsonArray(), ["tagged"] = 0,
                        ["materialsRenamed"] = new JsonArray([.. renamed.Select(r => (JsonNode)new JsonObject { ["from"] = r.From, ["to"] = r.To })]),
                    };
                    return none;
                }
                progress?.Invoke($"Reading {want.Count} changed elements", 0, 0);
                ArchicadModel model;
                try { model = ArchicadRead.Read(c, progress, ct, new ArchicadReadScope(want, FillsOf(m, key))); }
                catch (OperationCanceledException) { throw new CommandException("Stopped: nothing was received."); }
                result = ArchicadReceive.Apply(ctx, model, options, ctx.Services, progress, ct, new ArchicadScope(deleted, forced)).ToJson();
                result["mode"] = "incremental";
                result["changes"] = new JsonObject { ["new"] = changes.New.Count, ["modified"] = changes.Modified.Count, ["deleted"] = changes.Deleted.Count, ["read"] = model.Scope?.Count ?? 0 };
                result["readMs"] = Math.Round(model.Milliseconds);
            }
            else
            {
                progress?.Invoke("Reading Archicad's whole model", 0, 0);
                ArchicadModel model;
                try { model = ArchicadRead.Read(c, progress, ct); }
                catch (OperationCanceledException) { throw new CommandException("Stopped: nothing was received."); }
                var (taken, _) = ArchicadReceive.Taken(model);
                if (known.Count > 0 && taken.Count > 0 && !taken.Any(t => known.Contains(t.Guid)))
                    throw new CommandException($"The Archicad project open ({source.Label}) is not the one this project received: none of its elements are those received. Open that project in Archicad, or receive this one as a copy (File → Collaborate → Archicad: Receive…).");
                result = ArchicadReceive.Apply(ctx, model, options, ctx.Services, progress, ct).ToJson();
                result["mode"] = "full";
                result["why"] = why;
                result["readMs"] = Math.Round(model.Milliseconds);
            }
            result["project"] = source.Label;
            if (notes.Count > 0 && result["notes"] is JsonArray n) foreach (var x in notes) n.Add((JsonNode)x);
            if (copy != null && next != null)
            {
                ArchicadLink.Keep(copy, new ArchicadLink(key, next, c.Port, me.Project, me.Untitled, ReadStories(c, ct), DateTime.UtcNow.ToString("o"), ArchicadLink.StampOf(m, key), options.ToJson()));
                if (link != null && link.StateId != next) Release(c, link.StateId);
            }
            result["linked"] = next != null;
            return result;
        }
        catch
        {
            Release(c, next);
            throw;
        }
    }

    static List<string> ReadStories(ArchicadClient c, CancellationToken ct)
    {
        try { return [.. (c.Tapir("GetStories", null, ct)?["stories"] as JsonArray ?? []).OfType<JsonObject>().Select(s => (string?)s["ifcId"]).OfType<string>().Order(StringComparer.Ordinal)]; }
        catch (CommandException) { return []; }
    }

    /// <summary>The model's link kept after a receive: the state captured before its read (so what changed while it read shows next time), the old one released.</summary>
    public static void Linked(ICommandContext ctx, ArchicadClient c, ArchicadModel model, ArchicadReceiveOptions options, string? stateId)
    {
        var copy = ctx.Query.CopyState;
        if (copy == null || stateId == null) return;
        var key = options.Key ?? ArchicadReceive.ModelKey(model.Source, options.AsCopy);
        var before = ArchicadLink.All(copy).GetValueOrDefault(key);
        ArchicadLink.Keep(copy, new ArchicadLink(key, stateId, c.Port, model.Source.Project, model.Source.Untitled, model.StoryIds, DateTime.UtcNow.ToString("o"),
            ArchicadLink.StampOf(ctx.Model, key), options.ToJson()));
        if (before != null && before.StateId != stateId) Release(c, before.StateId);
    }

    /// <summary>An update with nothing to do: what the project holds of the model, unchanged.</summary>
    static JsonObject Nothing(IModelView m, string key, ArchicadSource source)
    {
        var now = ArchicadReceive.Existing(m, key).Where(e => (string?)(e.Attrs[ArchicadReceive.Key] as JsonObject)?["role"] is null or "element").ToList();
        var native = now.Count(e => !StoredBodies.Has(e));
        return new JsonObject
        {
            ["elements"] = now.Count, ["added"] = 0, ["updated"] = 0, ["unchanged"] = now.Count, ["removed"] = 0, ["native"] = native, ["references"] = now.Count - native,
            ["storeys"] = new JsonObject(), ["skipped"] = new JsonObject(), ["recognized"] = 0, ["recognition"] = null, ["levels"] = null, ["groupId"] = null,
            ["notes"] = new JsonArray(), ["mode"] = "incremental", ["changes"] = new JsonObject { ["new"] = 0, ["modified"] = 0, ["deleted"] = 0, ["read"] = 0 },
            ["project"] = source.Label, ["linked"] = true, ["nothing"] = true,
        };
    }

    /// <summary>Types that only Archicad's 2D, its views or its markers are: never received, so never counted as a change.</summary>
    static readonly HashSet<string> Drawn =
    [
        "Dimension", "RadialDimension", "LevelDimension", "AngleDimension", "Text", "Label", "Hatch", "Line", "PolyLine", "Arc", "Circle", "Spline", "Hotspot",
        "CutPlane", "Camera", "CamSet", "Group", "SectElem", "Drawing", "Picture", "Detail", "Elevation", "InteriorElevation", "Worksheet", "ChangeMarker", "Hotlink",
    ];

    /// <summary>
    /// Whether Archicad's model changed since the project's link was kept, cheaply: Archicad's changes counted (no element read, no
    /// geometry), those the project does not hold told apart by their type alone (2D and parts never count). Without a link: <c>linked:
    /// false</c>, and Archicad is not asked. Archicad gone, busy or restarted: said, never thrown (the status bar asks it every 10 s).
    /// </summary>
    public static JsonObject Status(IArchicadTransport transport, ICopyState? copy, IModelView m, int? port, CancellationToken ct)
    {
        var links = ArchicadLink.All(copy);
        var link = links.Values.Where(l => port == null || l.Port == port).OrderByDescending(l => l.At, StringComparer.Ordinal).FirstOrDefault();
        if (link == null) return new JsonObject { ["linked"] = false };
        var o = new JsonObject { ["linked"] = true, ["port"] = link.Port, ["project"] = link.Project, ["at"] = link.At, ["reachable"] = true };
        var c = new ArchicadClient(transport, link.Port);
        ArchicadChanges changes;
        try { changes = Changes(c, link.StateId, ct); }
        catch (ArchicadStateLost) { o["lost"] = true; o["changed"] = true; o["count"] = null; return o; }
        catch (CommandException ex)
        {
            if (ArchicadClient.Busy(ex.Message)) { o["busy"] = true; o["problem"] = ex.Message; }
            else { o["reachable"] = false; o["problem"] = ex.Message; }
            o["changed"] = false;
            return o;
        }
        var known = Known(m, link.Key);
        var removed = changes.Deleted.Count(known.Contains);
        var updated = changes.Modified.Count(known.Contains);
        var unknown = changes.New.Concat(changes.Modified).Where(g => !known.Contains(g)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var added = 0;
        if (unknown.Count > 0)
        {
            try
            {
                foreach (var d in c.Each("GetDetailsOfElements", unknown, "detailsOfElements", b => new JsonObject { ["elements"] = b, ["fields"] = new JsonArray("elementId", "type") }, c.Batch, ct))
                    if ((string?)d?["type"] is { } t && !Drawn.Contains(t) && !ArchicadRead.Subelement(t)) added++;
            }
            catch (CommandException) { added = unknown.Count; }
        }
        // The project changed here since (an undo of an update): it is behind Archicad whatever Archicad says.
        var local = link.Stamp != ArchicadLink.StampOf(m, link.Key);
        o["added"] = added; o["updated"] = updated; o["removed"] = removed;
        o["count"] = added + updated + removed;
        o["local"] = local;
        o["changed"] = added + updated + removed > 0 || local;
        return o;
    }
}
