using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Documents;

/// <summary>
/// The project's information (docs/plan-documents.md, Datele proiectului; Manage → Project information…): kept on the IfcProject
/// as <c>info</c> — its number, phase, client and their address, the site's address, cadastral number and land book, the
/// designer and their address, the chief architect, who drew and who checked it; since decision 340 grouped, with what a title
/// block commonly asks (the permit, the designer's registration and contacts, the engineers, who approved it) and the user's own
/// parameters (<c>info.custom</c>: a unique key, label, type and group). Documents read it as <c>{{project.*}}</c>, title blocks
/// as <c>{project.*}</c>, and a sheet's title block takes who drew and checked it from here when the sheet does not say.
/// </summary>
public static class ProjectInfo
{
    public const string Key = "info";

    /// <summary>
    /// The fields, with their labels and groups (decision 340: what a title block commonly asks): the project, the client, the
    /// site and its permit, the designer's company, the people who sign.
    /// </summary>
    public static readonly (string Key, string Label, string Group)[] Grouped =
    [
        ("number", "Project number", "project"), ("phase", "Phase", "project"), ("phaseName", "Phase name", "project"), ("contract", "Contract number", "project"),
        ("client", "Client", "client"), ("clientAddress", "Client's address", "client"),
        ("address", "Site address", "site"), ("cadastral", "Cadastral number", "site"), ("landBook", "Land book", "site"),
        ("permit", "Building permit number", "site"), ("permitDate", "Building permit date", "site"),
        ("designer", "Designer", "designer"), ("designerAddress", "Designer's address", "designer"), ("designerRegistration", "Registration number", "designer"),
        ("designerPhone", "Phone", "designer"), ("designerEmail", "E-mail", "designer"),
        ("projectManager", "Project manager", "people"), ("chiefArchitect", "Chief architect", "people"), ("structuralEngineer", "Structural engineer", "people"),
        ("mepEngineer", "MEP engineer", "people"), ("author", "Drawn by", "people"), ("checkedBy", "Checked by", "people"), ("approvedBy", "Approved by", "people"),
    ];

    /// <summary>The fields with their labels.</summary>
    public static readonly (string Key, string Label)[] Fields = [.. Grouped.Select(f => (f.Key, f.Label))];

    /// <summary>The groups, in the order the form shows them; a parameter of the user's may name any other.</summary>
    public static readonly (string Key, string Label)[] Groups =
        [("project", "Project"), ("client", "Client"), ("site", "Site and permit"), ("designer", "Designer"), ("people", "People"), ("other", "Other")];

    /// <summary>The kinds a parameter of the user's is.</summary>
    public static readonly string[] CustomTypes = ["text", "number", "date"];

    /// <summary>The key the user's own parameters are kept under in <c>info</c>.</summary>
    public const string CustomKey = "custom";

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

    /// <summary>A parameter of the user's, as kept: its key, label, type, group and value (text; a number or a date as typed, checked).</summary>
    public sealed record Custom(string Key, string Label, string Type, string Group, string? Value)
    {
        public JsonObject ToJson()
        {
            var o = new JsonObject { ["key"] = Key, ["label"] = Label, ["type"] = Type, ["group"] = Group };
            if (Value != null) o["value"] = Value;
            return o;
        }

        /// <summary>The value as a title block or a document prints it: a date as dd.MM.yyyy.</summary>
        public string? Printed => Type == "date" && Value is { } v && DateTime.TryParseExact(v, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
            ? d.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture) : Value;
    }

    /// <summary>The user's own parameters, in order.</summary>
    public static List<Custom> Customs(IModelView m) =>
        (Of(m)[CustomKey] as JsonArray)?.OfType<JsonObject>().Select(o => (string?)o["key"] is { Length: > 0 } k
            ? new Custom(k, (string?)o["label"] is { Length: > 0 } l ? l : k, (string?)o["type"] is { } t && CustomTypes.Contains(t) ? t : "text",
                (string?)o["group"] is { Length: > 0 } g ? g : "other", (string?)o["value"] is { Length: > 0 } v ? v : null)
            : null).OfType<Custom>().ToList() ?? [];

    /// <summary>A key a parameter of the user's may take: a letter, then letters, digits or _; not a field's, the name's or another parameter's (but <paramref name="except"/>).</summary>
    public static string CheckKey(IModelView m, string? key, string? except = null)
    {
        var k = key?.Trim() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(k, "^[A-Za-z][A-Za-z0-9_]{0,39}$"))
            throw new CommandException("A parameter's key starts with a letter and has only letters, digits and _ (40 at most).");
        if (k.Equals("name", StringComparison.OrdinalIgnoreCase) || Fields.Any(f => f.Key.Equals(k, StringComparison.OrdinalIgnoreCase)))
            throw new CommandException($"„{k}” is a field of the project's information already.");
        if (Customs(m).Any(c => c.Key.Equals(k, StringComparison.OrdinalIgnoreCase) && !c.Key.Equals(except, StringComparison.OrdinalIgnoreCase)))
            throw new CommandException($"The project has a parameter „{k}” already.");
        return k;
    }

    /// <summary>A value checked against its type: a number as a number, a date as YYYY-MM-DD (dd.MM.yyyy taken too); empty is none.</summary>
    public static string? CheckValue(string type, string? value)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        switch (type)
        {
            case "number":
                if (!double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, inv, out var n) || !double.IsFinite(n)) throw new CommandException($"„{v}” is not a number.");
                return n.ToString("R", inv);
            case "date":
                if (DateTime.TryParseExact(v, ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy"], inv, System.Globalization.DateTimeStyles.None, out var d)) return d.ToString("yyyy-MM-dd", inv);
                throw new CommandException($"„{v}” is not a date (YYYY-MM-DD or DD.MM.YYYY).");
            default: return v;
        }
    }

    static void WriteCustoms(ICommandContext ctx, List<Custom> list)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        var info = (attrs[Key] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
        if (list.Count > 0) info[CustomKey] = new JsonArray(list.Select(c => (JsonNode)c.ToJson()).ToArray()); else info.Remove(CustomKey);
        if (info.Count > 0) attrs[Key] = info; else attrs.Remove(Key);
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>
    /// A parameter of the user's added or changed (<c>key</c>; <c>newKey</c> renames it; <c>label</c>, <c>type</c>, <c>group</c>,
    /// <c>value</c> as given, the others kept), one undo step.
    /// </summary>
    public static void SetCustom(ICommandContext ctx, JsonObject a)
    {
        var list = Customs(ctx.Model);
        var key = ((string?)a["key"])?.Trim() ?? throw new CommandException("Which parameter?");
        var i = list.FindIndex(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        var was = i >= 0 ? list[i] : new Custom(CheckKey(ctx.Model, key), key, "text", "other", null);
        var newKey = a.ContainsKey("newKey") ? CheckKey(ctx.Model, (string?)a["newKey"], was.Key) : was.Key;
        var type = a.ContainsKey("type") ? (string?)a["type"] is { } t && CustomTypes.Contains(t) ? t : throw new CommandException($"A parameter is {string.Join(", ", CustomTypes)}.") : was.Type;
        var value = a.ContainsKey("value") ? CheckValue(type, (string?)a["value"]) : CheckValue(type, was.Value);
        var next = new Custom(newKey,
            a.ContainsKey("label") ? ((string?)a["label"])?.Trim() is { Length: > 0 } l ? l : newKey : was.Label == was.Key ? newKey : was.Label, type,
            a.ContainsKey("group") ? ((string?)a["group"])?.Trim() is { Length: > 0 } g ? g : "other" : was.Group, value);
        if (i >= 0) list[i] = next; else list.Add(next);
        WriteCustoms(ctx, list);
    }

    public static void RemoveCustom(ICommandContext ctx, string? key)
    {
        var list = Customs(ctx.Model);
        if (list.RemoveAll(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) == 0) throw new CommandException("The project has no such parameter.");
        WriteCustoms(ctx, list);
    }

    /// <summary>A value by its key: a field's or a parameter of the user's (printed); null when neither has one.</summary>
    public static string? Value(IModelView m, string key) =>
        Fields.Any(f => f.Key == key) ? Get(m, key) : Customs(m).FirstOrDefault(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Printed;

    public static JsonObject ToJson(IQueryContext q)
    {
        var info = Of(q.Model);
        var o = new JsonObject { ["name"] = Sheets.ProjectName(q) };
        foreach (var (key, _) in Fields) o[key] = info[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        o["fields"] = new JsonArray(Grouped.Select(f => (JsonNode)new JsonObject { ["key"] = f.Key, ["label"] = f.Label, ["group"] = f.Group }).ToArray());
        o["groups"] = new JsonArray(Groups.Select(g => (JsonNode)new JsonObject { ["key"] = g.Key, ["label"] = g.Label }).ToArray());
        o["custom"] = new JsonArray(Customs(q.Model).Select(c => (JsonNode)c.ToJson()).ToArray());
        o["phases"] = new JsonArray(Phases.Select(p => (JsonNode)p).ToArray());
        // The project's own parameters ({{param.Name}}): edited through element.setParam on the project.
        o["projectId"] = Spatial.Project(q.Model)?.Id;
        o["params"] = Spatial.Project(q.Model)?.Attrs["params"]?.DeepClone() ?? new JsonArray();
        return o;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Command("project.setInfo", "Project information", (ctx, a) => { Set(ctx, a); return null; });
        r.Command("project.setParameter", "Project parameter", (ctx, a) => { SetCustom(ctx, a); return null; });
        r.Command("project.removeParameter", "Remove project parameter", (ctx, a) => { RemoveCustom(ctx, (string?)a["key"]); return null; });
        r.Query("project.info", (q, _) => ToJson(q));
    }
}
