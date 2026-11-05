using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Documents;

/// <summary>
/// The project's information (docs/plan-documents.md, Datele proiectului; Manage → Project information…): kept on the IfcProject
/// as <c>info</c> — its number, phase, client and their address, the site's address, cadastral number and land book, the
/// designer and their address, the chief architect, who drew and who checked it. Documents read it as <c>{{project.*}}</c>, and a
/// sheet's title block takes who drew and checked it from here when the sheet does not say: one place to write it.
/// </summary>
public static class ProjectInfo
{
    public const string Key = "info";

    /// <summary>The fields, with their labels.</summary>
    public static readonly (string Key, string Label)[] Fields =
    [
        ("number", "Project number"), ("phase", "Phase"), ("client", "Client"), ("clientAddress", "Client's address"), ("address", "Site address"),
        ("cadastral", "Cadastral number"), ("landBook", "Land book"), ("designer", "Designer"), ("designerAddress", "Designer's address"),
        ("chiefArchitect", "Chief architect"), ("author", "Drawn by"), ("checkedBy", "Checked by"),
    ];

    /// <summary>The phases offered first (any text is taken).</summary>
    public static readonly string[] Phases = ["SF", "DALI", "PUZ", "PUD", "DTAC", "DTOE", "PT", "DE", "PTh"];

    /// <summary>The information as kept; empty when none.</summary>
    public static JsonObject Of(IModelView m) => Spatial.Project(m)?.Attrs[Key] as JsonObject ?? [];

    /// <summary>One field, trimmed; null when it is not set.</summary>
    public static string? Get(IModelView m, string key) => Of(m)[key] is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim().Length > 0 ? s.Trim() : null;

    /// <summary>The title block fields the project's information stands in for, when a sheet leaves them empty.</summary>
    static readonly Dictionary<string, string> TitleBlockFallback = new() { ["drawnBy"] = "author", ["checkedBy"] = "checkedBy", ["phase"] = "phase" };

    /// <summary>A sheet's title block field: its own, else the project's information's (drawn by, checked by, phase); null when neither says.</summary>
    public static string? TitleField(IModelView m, Entity sheet, string key) =>
        Cde.CdeCheckContext.Field(sheet, key) ?? (TitleBlockFallback.TryGetValue(key, out var info) ? Get(m, info) : null);

    /// <summary>The fields given set (an empty or null value clears one; <c>name</c> renames the project), as one undo step.</summary>
    public static void Set(ICommandContext ctx, JsonObject a)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        var info = (attrs[Key] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
        foreach (var (key, _) in Fields)
        {
            if (!a.ContainsKey(key)) continue;
            if (((string?)a[key])?.Trim() is { Length: > 0 } v) info[key] = v; else info.Remove(key);
        }
        if (info.Count > 0) attrs[Key] = info; else attrs.Remove(Key);
        var name = a.ContainsKey("name") ? ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : "Project" : null;
        ctx.Replace(project.With(name: name, attrs: attrs));
    }

    public static JsonObject ToJson(IQueryContext q)
    {
        var info = Of(q.Model);
        var o = new JsonObject { ["name"] = Sheets.ProjectName(q) };
        foreach (var (key, _) in Fields) o[key] = (string?)info[key];
        o["fields"] = new JsonArray(Fields.Select(f => (JsonNode)new JsonObject { ["key"] = f.Key, ["label"] = f.Label }).ToArray());
        o["phases"] = new JsonArray(Phases.Select(p => (JsonNode)p).ToArray());
        // The project's own parameters ({{param.Name}}): edited through element.setParam on the project.
        o["projectId"] = Spatial.Project(q.Model)?.Id;
        o["params"] = Spatial.Project(q.Model)?.Attrs["params"]?.DeepClone() ?? new JsonArray();
        return o;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Command("project.setInfo", "Project information", (ctx, a) => { Set(ctx, a); return null; });
        r.Query("project.info", (q, _) => ToJson(q));
    }
}
