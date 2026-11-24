using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform.Cde.Remarks;

namespace Lapin.Platform.Cde;

/// <summary>
/// The project's IDS files (docs/ids.md): each kept in the project as a resource of its own, <see cref="Class"/>, with the file's
/// text as it came, its name, its hash and what its info block says; its id a GlobalId made when it is first added and kept when
/// the file is replaced by a newer one of the same name, so results, remarks and a server's copy keep pointing at it. Reading the
/// file (and refusing one that is not IDS 1.0) and checking it are the engine's IDS part (Lapin.Ids), which hands this one
/// the checked text: <c>ids.add</c> in the dispatcher. The project's IDS settings (whether failures stop a submission) are kept
/// with its CDE settings, under <c>ids</c>.
/// </summary>
public static class IdsFiles
{
    public const string Class = "LapinIdsFile";
    /// <summary>The largest IDS kept (bytes of its text).</summary>
    public const int MaxBytes = 4_000_000;

    public static IEnumerable<Entity> All(IModelView m) => m.OfClass(Class).OrderBy(e => e.Str("addedAt"), StringComparer.Ordinal).ThenBy(e => e.Id, StringComparer.Ordinal);

    public static string? Text(Entity e) => e.Str("xml");

    /// <summary>Whether a failing specification stops a submission (on unless the project says otherwise), and whether Issue… runs the IDS files at all.</summary>
    public static (bool Blocks, bool OnSubmit) Rules(IModelView m)
    {
        var o = Cde.Settings(m)["ids"] as JsonObject;
        static bool B(JsonNode? n, bool d) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : d;
        return (B(o?["blocks"], true), B(o?["onSubmit"], true));
    }

    static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static JsonObject ToJson(Entity e) => new()
    {
        ["id"] = e.Id, ["name"] = e.Name, ["file"] = e.Str("file"), ["title"] = e.Str("title"), ["version"] = e.Str("version"),
        ["specifications"] = e.NumOrNull("specifications") is { } n ? (int)n : 0, ["size"] = e.NumOrNull("size") is { } s ? (long)s : 0,
        ["sha256"] = e.Str("sha256"), ["addedAt"] = e.Str("addedAt"), ["addedBy"] = e.Str("addedBy"),
    };

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "IDS file", [], "#888888", Kind: "resource", Template: false));

        // The checked text of an IDS (ids.add reads and checks it first): a new file, or a newer copy of one the project has (same file
        // name, or the id given), which keeps its id. The same text twice is refused.
        r.Command("ids.store", "Add IDS file", (ctx, a) =>
        {
            var xml = (string?)a["xml"] ?? throw new CommandException("The IDS file's text is missing.");
            if (Encoding.UTF8.GetByteCount(xml) > MaxBytes) throw new CommandException($"The IDS file is over {MaxBytes / 1_000_000} MB.");
            var file = Path.GetFileName(((string?)a["file"])?.Trim() is { Length: > 0 } f ? f : "requirements.ids");
            var sha = Hash(xml);
            var all = All(ctx.Model).ToList();
            if (all.FirstOrDefault(e => e.Str("sha256") == sha) is { } same && (string?)a["id"] != same.Id)
                throw new CommandException($"The project has this IDS already („{same.Name}”).");
            var existing = (string?)a["id"] is { Length: > 0 } id ? all.FirstOrDefault(e => e.Id == id) ?? throw new CommandException("There is no such IDS file.")
                : all.FirstOrDefault(e => string.Equals(e.Str("file"), file, StringComparison.OrdinalIgnoreCase));
            var title = ((string?)a["title"])?.Trim();
            var attrs = new JsonObject
            {
                ["file"] = file, ["xml"] = xml, ["sha256"] = sha, ["size"] = Encoding.UTF8.GetByteCount(xml),
                ["title"] = title, ["version"] = (string?)a["version"],
                ["specifications"] = a["specifications"] is JsonValue sv && sv.TryGetValue<int>(out var n) ? n : 0,
                ["addedAt"] = existing?.Str("addedAt") ?? Bcf.Now(), ["addedBy"] = existing?.Str("addedBy") ?? ((string?)a["author"] is { Length: > 0 } by ? by : "Lapin"),
            };
            if (existing != null) attrs["replacedAt"] = Bcf.Now();
            var name = title is { Length: > 0 } ? title : Path.GetFileNameWithoutExtension(file);
            var entity = new Entity(existing?.Id ?? ctx.NewId(), Class, name, attrs);
            if (existing != null) ctx.Replace(entity); else ctx.Add(entity);
            return ToJson(entity);
        });

        r.Command("ids.remove", "Remove IDS file", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("There is no such IDS file.");
            ctx.Remove(e.Id);
            return null;
        });

        // Whether a failing specification stops Submit for approval (blocks), and whether Issue… runs the IDS files before it (onSubmit).
        r.Command("ids.setRules", "IDS settings", (ctx, a) =>
        {
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = project.CloneAttrs();
            var cde = (attrs[Cde.Key] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
            var ids = (cde["ids"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
            foreach (var key in new[] { "blocks", "onSubmit" })
                if (a[key] is JsonValue v && v.TryGetValue<bool>(out var b)) ids[key] = b;
            cde["ids"] = ids;
            attrs[Cde.Key] = cde;
            ctx.Replace(project.With(attrs: attrs));
            var (blocks, onSubmit) = Rules(ctx.Model);
            return new JsonObject { ["blocks"] = blocks, ["onSubmit"] = onSubmit };
        });

        r.Query("ids.files", (q, _) =>
        {
            var (blocks, onSubmit) = Rules(q.Model);
            return new JsonObject
            {
                ["files"] = new JsonArray([.. All(q.Model).Select(e => (JsonNode)ToJson(e))]),
                ["blocks"] = blocks, ["onSubmit"] = onSubmit,
            };
        });

        // The remarks of a run (ids.makeRemarks works them out): one per failing specification, made or brought up to date in one step.
        // A remark already made for that specification (the same GUID) gets the new description and selection and is opened again.
        r.Command("ids.writeRemarks", "IDS remarks", (ctx, a) =>
        {
            var made = 0; var updated = 0;
            var ids = new JsonArray();
            foreach (var n in a["remarks"] as JsonArray ?? [])
            {
                if (n is not JsonObject x) continue;
                var guid = Bcf.ParseGuid((string?)x["guid"]) ?? throw new CommandException("A remark needs its GUID.");
                var title = ((string?)x["title"])?.Trim() is { Length: > 0 } t ? t : throw new CommandException("A remark has a title.");
                var selection = (x["selection"] as JsonArray ?? []).Select(s => (string?)s).OfType<string>().ToList();
                // A camera on the elements (a CDE server takes no viewpoint without a camera or a snapshot).
                var vp = new BcfViewpoint(GlobalId.ToGuid(GlobalId.Derive(guid.ToString("N"), "viewpoint")), BcfCamera.From(x["camera"]), selection, true, [], [], [], null);
                var now = Bcf.Now();
                var author = (string?)x["author"] is { Length: > 0 } au ? au : (string?)a["author"] is { Length: > 0 } au2 ? au2 : "Lapin";
                if (ctx.Model.Get(Remarks.Remarks.IdOf(guid)) is { Class: Remarks.Remarks.Class } old)
                {
                    var t0 = Remarks.Remarks.Read(old);
                    var reopened = Remarks.Remarks.IsOpen(t0.Status) ? t0.Status : "Open";
                    ctx.Replace(Remarks.Remarks.ToEntity(t0 with { Title = title, Description = (string?)x["description"], Status = reopened, ModifiedDate = now, ModifiedAuthor = author, Viewpoints = [vp, .. t0.Viewpoints.Where(v => v.Guid != vp.Guid)] }));
                    updated++;
                }
                else if (ctx.Model.Get(Remarks.Remarks.IdOf(guid)) != null) throw new CommandException("Something else has this remark's id.");
                else
                {
                    ctx.Add(Remarks.Remarks.ToEntity(new BcfTopic(guid, title, (string?)x["description"], "Issue", "Open", (string?)x["priority"] ?? "Normal", null, now, author,
                        null, null, null, null, null, [], [], [vp])));
                    made++;
                }
                ids.Add((JsonNode)Remarks.Remarks.IdOf(guid));
            }
            return new JsonObject { ["made"] = made, ["updated"] = updated, ["ids"] = ids };
        });
    }
}
