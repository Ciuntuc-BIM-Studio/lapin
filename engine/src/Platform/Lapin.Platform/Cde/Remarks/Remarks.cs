using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Cde.Remarks;

/// <summary>
/// Remarks on the model (docs/plan-cde.md, stage 2): BCF topics kept in the
/// project as <c>LapinRemark</c> entities (resources, never exported to IFC),
/// each with its comments and viewpoints. A remark's entity id is its topic
/// GUID in IFC's 22-character form, so the same topic read again from a BCF
/// file (BIMcollab, Solibri, Revit) finds its remark and updates it, and a
/// remark written out carries the GUID it was born with. The snapshots stay
/// on the entity but out of the interface's node (<see cref="Strip"/>): the
/// list asks for one when it shows it.
/// </summary>
public static class Remarks
{
    public const string Class = "LapinRemark";

    /// <summary>The statuses, types and priorities Lapin proposes (BCF leaves them free; a file's own are kept as they come).</summary>
    public static readonly string[] Statuses = ["Open", "In progress", "Resolved", "Closed"];
    public static readonly string[] Types = ["Issue", "Clash", "Request", "Remark", "Question"];
    public static readonly string[] Priorities = ["Low", "Normal", "High", "Critical"];

    /// <summary>A remark is open unless closed (a resolved one still waits for whoever raised it).</summary>
    public static bool IsOpen(string? status) => !string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase);

    /// <summary>The entity id of a topic: its GUID as an IFC GlobalId (22 characters), both ways.</summary>
    public static string IdOf(Guid guid) => GlobalId.FromGuid(guid);
    public static Guid GuidOf(Entity e) => Bcf.ParseGuid(e.Str("guid")) ?? (GlobalId.IsValid(e.Id) ? GlobalId.ToGuid(e.Id) : Guid.NewGuid());

    public static BcfTopic Read(Entity e) => BcfTopic.From(GuidOf(e), e.Name, e.Attrs);

    public static Entity ToEntity(BcfTopic t) => new(IdOf(t.Guid), Class, t.Title, t.ToJson());

    public static List<BcfTopic> All(IModelView m) => m.OfClass(Class).Select(Read).ToList();

    /// <summary>What the interface's node keeps of a remark: everything but the snapshots (a PNG each; asked for by remarks.snapshot).</summary>
    public static void Strip(Entity e, JsonObject props)
    {
        if (e.Class != Class || props["viewpoints"] is not JsonArray vps) return;
        foreach (var vp in vps.OfType<JsonObject>()) vp.Remove("snapshot");
    }

    static Entity Require(IModelView m, string? id) => m.Get(id ?? "") is { Class: Class } e ? e : throw new CommandException("There is no such remark.");

    static string Author(JsonObject a) => ((string?)a["author"])?.Trim() is { Length: > 0 } s ? s : "Lapin";

    /// <summary>The topic's fields a command may set, applied over <paramref name="t"/>; null leaves a field, an empty string clears it.</summary>
    static BcfTopic Apply(BcfTopic t, JsonObject a)
    {
        string? Field(string key, string? current) => a.ContainsKey(key) ? ((string?)a[key])?.Trim() is { Length: > 0 } s ? s : null : current;
        return t with
        {
            Title = ((string?)a["title"])?.Trim() is { Length: > 0 } title ? title : t.Title,
            Description = Field("description", t.Description),
            Type = Field("type", t.Type),
            Status = Field("status", t.Status),
            Priority = Field("priority", t.Priority),
            AssignedTo = Field("assignedTo", t.AssignedTo),
            DueDate = Field("dueDate", t.DueDate),
            Stage = Field("stage", t.Stage),
            Labels = a.ContainsKey("labels") ? Bcf.Strings(a["labels"]) : t.Labels,
        };
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Remark", [], "#888888", Kind: "resource"));

        // A new remark: its fields, who makes it, and the viewpoint of the 3D view it was made from (camera, selection, planes, snapshot).
        r.Command("remarks.create", "New remark", (ctx, a) =>
        {
            var title = ((string?)a["title"])?.Trim();
            if (string.IsNullOrEmpty(title)) throw new CommandException("A remark has a title.");
            var now = Bcf.Now();
            var guid = Bcf.ParseGuid((string?)a["guid"]) ?? Guid.NewGuid();
            if (ctx.Model.Get(IdOf(guid)) != null) throw new CommandException("There is a remark with this GUID already.");
            var viewpoints = BcfViewpoint.From(a["viewpoint"]) is { } vp ? new List<BcfViewpoint> { vp } : [];
            var topic = Apply(new BcfTopic(guid, title, null, "Issue", "Open", "Normal", null, now, Author(a), null, null, null, null, null, [], [], viewpoints), a);
            ctx.Add(ToEntity(topic));
            return new JsonObject { ["id"] = IdOf(guid), ["guid"] = guid.ToString("D") };
        });

        // The fields changed (status, priority, who, when, labels, title, description): the remark says when and by whom.
        r.Command("remarks.update", "Edit remark", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var t = Apply(Read(e), a) with { ModifiedDate = Bcf.Now(), ModifiedAuthor = Author(a) };
            ctx.Replace(ToEntity(t));
            return t.ToJson(withSnapshots: false);
        });

        // A comment on the remark; with a viewpoint of its own (the 3D view as it is), or pointing at one the remark has.
        r.Command("remarks.comment", "Comment", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var text = ((string?)a["text"])?.Trim();
            if (string.IsNullOrEmpty(text)) throw new CommandException("A comment has a text.");
            var t = Read(e);
            var viewpoints = t.Viewpoints.ToList();
            Guid? vpGuid = Bcf.ParseGuid((string?)a["viewpointGuid"]);
            if (BcfViewpoint.From(a["viewpoint"]) is { } vp) { viewpoints.Add(vp); vpGuid = vp.Guid; }
            if (vpGuid is { } g && viewpoints.All(v => v.Guid != g)) vpGuid = null;
            var comment = new BcfComment(Guid.NewGuid(), Bcf.Now(), Author(a), text, vpGuid);
            ctx.Replace(ToEntity(t with { Comments = [.. t.Comments, comment], Viewpoints = viewpoints, ModifiedDate = comment.Date, ModifiedAuthor = comment.Author }));
            return comment.ToJson();
        });

        // A viewpoint added to the remark (the 3D view as it is now); `replace` puts it in the first one's place.
        r.Command("remarks.addViewpoint", "Remark viewpoint", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var vp = BcfViewpoint.From(a["viewpoint"]) ?? throw new CommandException("The viewpoint is missing.");
            var t = Read(e);
            var viewpoints = t.Viewpoints.ToList();
            if (a["replace"] is JsonValue rv && rv.TryGetValue<bool>(out var replace) && replace && viewpoints.Count > 0)
            {
                // The comments that pointed at the old one follow it.
                var old = viewpoints[0].Guid;
                viewpoints[0] = vp;
                t = t with { Comments = t.Comments.Select(c => c.Viewpoint == old ? c with { Viewpoint = vp.Guid } : c).ToList() };
            }
            else viewpoints.Add(vp);
            ctx.Replace(ToEntity(t with { Viewpoints = viewpoints, ModifiedDate = Bcf.Now(), ModifiedAuthor = Author(a) }));
            return new JsonObject { ["guid"] = vp.Guid.ToString("D") };
        });

        r.Command("remarks.delete", "Delete remark", (ctx, a) =>
        {
            foreach (var id in (a["ids"] as JsonArray ?? [(string?)a["id"]]).Select(x => (string?)x).OfType<string>())
                ctx.Remove(Require(ctx.Model, id).Id);
            return null;
        });

        // The remarks, each with its comments and viewpoints (without the snapshots), and the choices the interface offers.
        r.Query("remarks.list", (q, _) => new JsonObject
        {
            ["remarks"] = new JsonArray(All(q.Model).Select(t => { var o = t.ToJson(withSnapshots: false); o["id"] = IdOf(t.Guid); o["title"] = t.Title; o["open"] = IsOpen(t.Status); return (JsonNode)o; }).ToArray()),
            ["statuses"] = new JsonArray(Statuses.Select(s => (JsonNode)s).ToArray()),
            ["types"] = new JsonArray(Types.Select(s => (JsonNode)s).ToArray()),
            ["priorities"] = new JsonArray(Priorities.Select(s => (JsonNode)s).ToArray()),
        });

        // One viewpoint's snapshot, as base64 PNG (null when it has none).
        r.Query("remarks.snapshot", (q, a) =>
        {
            var t = Read(Require(q.Model, (string?)a["id"]));
            var g = Bcf.ParseGuid((string?)a["guid"]);
            var vp = g is { } gg ? t.Viewpoints.FirstOrDefault(v => v.Guid == gg) : t.Viewpoints.FirstOrDefault();
            return new JsonObject { ["data"] = vp?.Snapshot is { Length: > 0 } png ? Convert.ToBase64String(png) : null };
        });
    }

    // ------------------------------------------------------------ BCF in and out

    /// <summary>What an import did: topics read, remarks added and updated, comments and viewpoints brought in.</summary>
    public sealed record ImportResult(int Topics, int Added, int Updated, int Comments, int Viewpoints, string Version)
    {
        public JsonObject ToJson() => new() { ["topics"] = Topics, ["added"] = Added, ["updated"] = Updated, ["comments"] = Comments, ["viewpoints"] = Viewpoints, ["version"] = Version };
    }

    /// <summary>
    /// The file's topics into the project: a topic whose GUID the project has
    /// updates that remark (the file's fields win; comments and viewpoints are
    /// merged by GUID, so what was answered here and not yet sent stays), the
    /// others are added. One undo step for the caller's edit.
    /// </summary>
    public static ImportResult Import(IEdit edit, BcfFile file)
    {
        int added = 0, updated = 0, comments = 0, viewpoints = 0;
        foreach (var t in file.Topics)
        {
            var existing = edit.Model.Get(IdOf(t.Guid));
            if (existing is { Class: Class })
            {
                var mine = Read(existing);
                var mergedComments = t.Comments.ToList();
                foreach (var c in mine.Comments) if (mergedComments.All(x => x.Guid != c.Guid)) mergedComments.Add(c);
                var mergedViewpoints = t.Viewpoints.ToList();
                foreach (var v in mine.Viewpoints) if (mergedViewpoints.All(x => x.Guid != v.Guid)) mergedViewpoints.Add(v);
                // A viewpoint the file sends without its snapshot keeps the one we had.
                for (var i = 0; i < mergedViewpoints.Count; i++)
                    if (mergedViewpoints[i].Snapshot == null && mine.Viewpoints.FirstOrDefault(v => v.Guid == mergedViewpoints[i].Guid)?.Snapshot is { } png) mergedViewpoints[i] = mergedViewpoints[i] with { Snapshot = png };
                edit.Replace(ToEntity(t with { Comments = mergedComments, Viewpoints = mergedViewpoints }));
                updated++;
            }
            else if (existing != null) continue;   // an element with this GUID, not a remark: left as it is
            else { edit.Add(ToEntity(t)); added++; }
            comments += t.Comments.Count;
            viewpoints += t.Viewpoints.Count;
        }
        return new ImportResult(file.Topics.Count, added, updated, comments, viewpoints, file.Version);
    }

    /// <summary>The remarks asked for (ids; else the open ones, else all) as a BCF file of the version asked (2.1 unless "3.0").</summary>
    public static BcfFile Export(IModelView m, JsonObject a)
    {
        var all = All(m);
        IEnumerable<BcfTopic> chosen = all;
        if (a["ids"] is JsonArray ids)
        {
            var wanted = ids.Select(x => (string?)x).OfType<string>().ToHashSet();
            chosen = all.Where(t => wanted.Contains(IdOf(t.Guid)));
        }
        else if (a["open"] is JsonValue ov && ov.TryGetValue<bool>(out var open) && open) chosen = all.Where(t => IsOpen(t.Status));
        var version = (string?)a["version"] == Bcf.Version30 ? Bcf.Version30 : Bcf.Version21;
        var topics = chosen.Select((t, i) => t.Index is null ? t with { Index = i } : t).ToList();
        return new BcfFile(version, topics);
    }
}
