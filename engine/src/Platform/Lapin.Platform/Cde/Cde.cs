using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Cde;

/// <summary>
/// The project folder as a common data environment (docs/plan-cde.md,
/// decision 180): its path and naming convention kept on the IfcProject
/// (<c>cde</c>), the names the convention gives, the pre-issue checks and the
/// issues recorded. The issue itself (files written, hashed, the transmittal)
/// is the host's, <c>cde.issue</c> in the dispatcher: it needs the exports
/// and the project's file.
/// </summary>
public static class Cde
{
    public const string Key = "cde";

    /// <summary>The project's CDE settings as kept; empty when none.</summary>
    public static JsonObject Settings(IModelView m) => Spatial.Project(m)?.Attrs[Key] as JsonObject ?? [];

    public static string? Folder(IModelView m) => (string?)Settings(m)["folder"] is { Length: > 0 } f ? f : null;

    static void Write(ICommandContext ctx, Action<JsonObject> change)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        var cde = (attrs[Key] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
        change(cde);
        attrs[Key] = cde;
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>Whether the folder is on this computer's disk (false in a host without files, and for a folder not there: a warning, never an error).</summary>
    static bool Exists(string? folder)
    {
        try { return folder != null && Directory.Exists(folder); } catch { return false; }
    }

    /// <summary>The sheets asked for (ids), else every sheet in order.</summary>
    public static List<Entity> SheetsOf(IModelView m, JsonNode? ids)
    {
        var all = m.OfClass(Sheets.Class).ToList();
        if (ids is not JsonArray a) return all;
        var wanted = a.Select(x => (string?)x).OfType<string>().ToList();
        return wanted.Select(id => all.FirstOrDefault(s => s.Id == id) ?? throw new CommandException("There is no such sheet.")).ToList();
    }

    /// <summary>The names an issue would write, for the sheets asked (all by default), the model, the set and the transmittal.</summary>
    public static JsonObject Names(IModelView m, JsonObject a)
    {
        var naming = CdeHooks.Naming?.Invoke(CdeNaming.Of(m)) ?? CdeNaming.Of(m);
        var revision = (string?)a["revision"] is { Length: > 0 } r ? r : CdeRevisions.Next(CdeIssues.LastRevision(m));
        var status = (string?)a["status"] is { Length: > 0 } s ? s : "S2";
        var n = CdeIssues.NextNumber(m);
        return new JsonObject
        {
            ["revision"] = revision, ["status"] = status, ["number"] = n,
            ["sheets"] = new JsonArray(SheetsOf(m, a["sheets"]).Select(sheet =>
            {
                var name = naming.For(m, sheet);
                return (JsonNode)new JsonObject
                {
                    ["id"] = sheet.Id, ["number"] = sheet.Str("number"), ["title"] = sheet.Name, ["name"] = name,
                    ["file"] = CdeNaming.WithRevision(name, status, revision, "pdf"),
                    ["problems"] = new JsonArray(naming.Validate(name).Select(p => (JsonNode)p).ToArray()),
                };
            }).ToArray()),
            ["model"] = CdeNaming.WithRevision(naming.ForModel(), status, revision, "ifc"),
            ["set"] = CdeNaming.WithRevision(naming.ForSet(), status, revision, "pdf"),
            ["transmittal"] = CdeNaming.WithRevision(naming.ForTransmittal(n), status, revision, "pdf"),
        };
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(CdeIssues.Class, "Issue", [], "#888888", Kind: "resource"));

        // The project folder: a path on this computer (or a share, or a synced cloud folder); empty clears it. Kept in the project, so it travels with the file.
        r.Command("cde.setFolder", "Project folder", (ctx, a) =>
        {
            var path = ((string?)a["path"])?.Trim();
            Write(ctx, cde => { if (path is { Length: > 0 }) cde["folder"] = path; else cde.Remove("folder"); });
            return new JsonObject { ["folder"] = path is { Length: > 0 } ? path : null, ["exists"] = Exists(path) };
        });

        // The naming fields (project, originator, volume, level, role) and the required Pset_…Common properties (names; null: the defaults).
        r.Command("cde.setNaming", "Naming convention", (ctx, a) =>
        {
            var naming = CdeNaming.Of(ctx.Model).With(a);
            Write(ctx, cde =>
            {
                cde["naming"] = naming.ToJson();
                if (a.ContainsKey("requiredProps"))
                {
                    if (a["requiredProps"] is JsonArray props) cde["requiredProps"] = new JsonArray(props.Select(x => (string?)x).OfType<string>().Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().Select(x => (JsonNode)x).ToArray());
                    else cde.Remove("requiredProps");
                }
            });
            return naming.ToJson();
        });

        // The issue as the host wrote it (cde.issue): recorded in the project, the printed sheets given its revision and status.
        r.Command("cde.recordIssue", "Issue", (ctx, a) =>
        {
            var files = (a["files"] as JsonArray ?? []).Select(IssuedFile.From).OfType<IssuedFile>().ToList();
            if (files.Count == 0) throw new CommandException("An issue has at least one file.");
            var issue = CdeIssues.Record(ctx, (string?)a["name"] ?? "", (string?)a["revision"] ?? "", (string?)a["status"] ?? "", (string?)a["container"] ?? "",
                (string?)a["folder"], (string?)a["note"], files, a["version"] is JsonValue v && v.TryGetValue<int>(out var n) ? n : null);
            return CdeIssues.Read(issue).ToJson();
        });

        // The version the interface saved after the issue (project.saveVersion), so the two are found together.
        r.Command("cde.setIssueVersion", "Issue version", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: CdeIssues.Class } i ? i : throw new CommandException("There is no such issue.");
            var attrs = e.CloneAttrs();
            if (a["version"] is JsonValue v && v.TryGetValue<int>(out var n)) attrs["version"] = n; else attrs.Remove("version");
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        r.Command("cde.deleteIssue", "Delete issue", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: CdeIssues.Class } i ? i : throw new CommandException("There is no such issue.");
            ctx.Remove(e.Id);
            return null;
        });

        // The settings with what the interface shows beside them: the containers, the codes, the next revision and number, an example name.
        r.Query("cde.settings", (q, _) =>
        {
            var m = q.Model;
            var naming = CdeNaming.Of(m);
            var folder = Folder(m);
            return new JsonObject
            {
                ["folder"] = folder, ["exists"] = Exists(folder),
                ["containers"] = new JsonArray(CdeRevisions.Containers.Select(c => (JsonNode)c).ToArray()),
                ["naming"] = naming.ToJson(),
                ["example"] = naming.ForModel(),
                ["requiredProps"] = new JsonArray(CdeChecks.Required(m).Select(x => (JsonNode)x.Name).ToArray()),
                ["roles"] = new JsonArray(CdeNaming.Roles.Select(x => (JsonNode)new JsonObject { ["code"] = x.Code, ["label"] = x.Label }).ToArray()),
                ["statuses"] = new JsonArray(CdeRevisions.Statuses.Select(x => (JsonNode)new JsonObject { ["code"] = x.Code, ["label"] = x.Label, ["container"] = CdeRevisions.ContainerFor(x.Code) }).ToArray()),
                ["lastRevision"] = CdeIssues.LastRevision(m),
                ["nextRevision"] = CdeRevisions.Next(CdeIssues.LastRevision(m)),
                ["nextNumber"] = CdeIssues.NextNumber(m),
            };
        });

        r.Query("cde.naming", (q, a) => Names(q.Model, a));

        // The checks: every rule over the sheets asked (all by default), with the issue's revision when one is given.
        r.Query("cde.check", (q, a) => CdeChecks.ToJson(q, CdeChecks.Run(q, SheetsOf(q.Model, a["sheets"]), (string?)a["revision"] is { Length: > 0 } rev ? rev : null)));

        r.Query("cde.issues", (q, _) => new JsonArray(CdeIssues.All(q.Model).Select(i => (JsonNode)i.ToJson()).ToArray()));
    }
}
