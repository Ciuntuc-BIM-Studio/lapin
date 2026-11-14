using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// How the Gantt shows its tasks by fields (docs/plan-user-extensibility.md §12), kept on the work schedule (<c>gantt</c>) so
/// the window and the Gantt on a sheet show the same: the field its bars are coloured by (<c>colourBy</c>) with the colours
/// the user chose for some values (<c>colours</c>: value → #rrggbb; the others from Lapin's palette), the field its rows may
/// be grouped by (<c>groupBy</c>, the window's), and the fields shown as columns (<c>columns</c>, at most
/// <see cref="MaxColumns"/>). A field is any of the elements' (<see cref="ElementFields"/>: a property set, a parameter, an
/// alias, a formula) or the task's own (<c>task.name</c>, <c>task.wbs</c>, <c>task.kind</c>, <c>task.notes</c>…).
/// </summary>
public sealed record GanttSettings(string? ColourBy, IReadOnlyDictionary<string, string> Colours, string? GroupBy, IReadOnlyList<string> Columns)
{
    public const string Key = "gantt";
    public const int MaxColumns = 6;
    public static readonly GanttSettings None = new(null, new Dictionary<string, string>(), null, []);

    /// <summary>Every field the settings read, once.</summary>
    public IReadOnlyList<string> Fields => new[] { ColourBy, GroupBy }.Concat(Columns).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    public bool IsEmpty => Fields.Count == 0;

    static string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim().Length > 0 ? s.Trim() : null;

    static bool IsColour(string s) => s.Length == 7 && s[0] == '#' && s[1..].All(Uri.IsHexDigit);

    public static GanttSettings Of(Entity? schedule) => Read(schedule?.Attrs[Key]);

    public static GanttSettings Read(JsonNode? n)
    {
        if (n is not JsonObject o) return None;
        var colours = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (o["colours"] is JsonObject co)
            foreach (var (k, v) in co) if (Text(v) is { } c && IsColour(c) && k.Trim().Length > 0) colours[k.Trim()] = c.ToLowerInvariant();
        var columns = (o["columns"] as JsonArray ?? []).Select(Text).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxColumns).ToList();
        return new(Text(o["colourBy"]), colours, Text(o["groupBy"]), columns);
    }

    /// <summary>The settings as a command gives them, checked (null: none).</summary>
    public static GanttSettings Check(JsonNode? n)
    {
        if (n is null) return None;
        if (n is not JsonObject o) throw new CommandException("The Gantt's settings are an object: {colourBy, colours, groupBy, columns}.");
        if (o["columns"] is not null and not JsonArray) throw new CommandException("columns is a list of fields.");
        if ((o["columns"] as JsonArray)?.Count > MaxColumns) throw new CommandException($"The Gantt shows at most {MaxColumns} columns of fields.");
        if (o["colours"] is not null and not JsonObject) throw new CommandException("colours is an object: {value: \"#rrggbb\"}.");
        foreach (var (k, v) in o["colours"] as JsonObject ?? [])
            if (Text(v) is not { } c || !IsColour(c)) throw new CommandException($"„{k}”: a colour is written #rrggbb.");
        var s = Read(o);
        foreach (var f in s.Fields)
            if (ElementFields.FormulaOf(f) is { } fx && !ParamFormulas.IsTemplate(fx)) ParamFormulas.Check(fx);
        return s;
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject();
        if (ColourBy != null) o["colourBy"] = ColourBy;
        if (Colours.Count > 0) o["colours"] = new JsonObject(Colours.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)));
        if (GroupBy != null) o["groupBy"] = GroupBy;
        if (Columns.Count > 0) o["columns"] = new JsonArray(Columns.Select(c => (JsonNode)c).ToArray());
        return o;
    }
}

/// <summary>A field's value on a task: the one its elements (a summary's: its tasks') agree on, or theirs listed when they do not; each value with how many elements have it.</summary>
public sealed record TaskFieldValue(string? Value, bool Mixed, IReadOnlyList<(string Value, int Count)> Values, int Count, int Missing)
{
    public static readonly TaskFieldValue None = new(null, false, [], 0, 0);

    public JsonObject ToJson() => new()
    {
        ["value"] = Value, ["mixed"] = Mixed, ["count"] = Count, ["missing"] = Missing,
        ["values"] = new JsonArray(Values.Select(v => (JsonNode)new JsonObject { ["value"] = v.Value, ["count"] = v.Count }).ToArray()),
    };
}

/// <summary>The fields of the schedule's tasks: their own (<c>task.…</c>) and their elements' (<see cref="TaskElements.Field"/>), and the colours a field gives the bars.</summary>
public static class TaskFields
{
    public const string TaskPrefix = "task.";
    /// <summary>The colour of a task whose elements do not agree.</summary>
    public const string MixedColour = "#9aa3ad";
    /// <summary>Lapin's palette for the values no colour was chosen for, in the values' order.</summary>
    public static readonly string[] Palette = ["#4e79a7", "#f28e2b", "#59a14f", "#e15759", "#76b7b2", "#edc948", "#b07aa1", "#ff9da7", "#9c755f", "#86bc86", "#d4a6c8", "#5f9ed1"];

    static readonly StringComparer Natural = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);

    /// <summary>A field of the task itself: its name, WBS, level, parent's name, kind, notes, or any attribute it holds (norm, crew, durationFrom…).</summary>
    static string? Own(Plan p, IModelView m, string taskId, string key)
    {
        var t = p.ById.GetValueOrDefault(taskId);
        switch (key.ToLowerInvariant())
        {
            case "name": return t?.Name;
            case "wbs": return p.Wbs.GetValueOrDefault(taskId);
            case "level": return p.Level.TryGetValue(taskId, out var l) ? l.ToString(CultureInfo.InvariantCulture) : null;
            case "parent": return t?.Parent is { } par && p.ById.TryGetValue(par, out var pt) ? pt.Name : null;
            case "kind": return t?.Kind;
            case "notes": return t?.Notes;
        }
        var v = m.Get(taskId)?.Attrs.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
        return v is JsonValue jv ? jv.TryGetValue<string>(out var s) ? s : Json.Number(jv) is { } n ? n.ToString("0.###", CultureInfo.InvariantCulture) : jv.ToJsonString() : null;
    }

    /// <summary>A field's value on a task: its own (<c>task.name</c>), or across the elements it takes (a summary: those of the tasks under it).</summary>
    public static TaskFieldValue Of(Plan p, IModelView m, TaskElements links, string taskId, string field)
    {
        if (field.StartsWith(TaskPrefix, StringComparison.OrdinalIgnoreCase))
            return Own(p, m, taskId, field[TaskPrefix.Length..]) is { Length: > 0 } own ? new(own, false, [(own, 1)], 1, 0) : new(null, false, [], 0, 1);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var numbers = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
        int with = 0, missing = 0;
        foreach (var id in links.Under(taskId))
        {
            var v = links.Field(id, field);
            if (!v.Exists) { missing++; continue; }
            with++;
            var shown = v.Shown.Trim();
            counts[shown] = counts.GetValueOrDefault(shown) + 1;
            numbers.TryAdd(shown, v.Number);
        }
        if (with == 0) return new(null, false, [], 0, missing);
        var ordered = counts.Keys.All(k => numbers[k] != null) ? counts.OrderBy(kv => numbers[kv.Key]).ToList() : counts.OrderBy(kv => kv.Key, Natural).ToList();
        var values = ordered.Select(kv => (kv.Key, kv.Value)).ToList();
        return values.Count == 1 ? new(values[0].Key, false, values, with, missing) : new(string.Join(", ", values.Select(v => v.Key)), true, values, with, missing);
    }

    /// <summary>
    /// The colour each value of the colouring field takes: the user's, else the palette's in the values' order (numbers by
    /// their size, texts as a person sorts them); every value with how many tasks have it.
    /// </summary>
    public static List<(string Value, string Colour, int Tasks)> Legend(GanttSettings s, IEnumerable<TaskFieldValue> values)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in values) if (v is { Value: { } x, Mixed: false }) counts[x] = counts.GetValueOrDefault(x) + 1;
        var keys = counts.Keys.All(k => FieldValue.StrictNumber(k) != null) ? counts.Keys.OrderBy(k => FieldValue.StrictNumber(k)).ToList() : counts.Keys.Order(Natural).ToList();
        var o = new List<(string, string, int)>();
        var next = 0;
        foreach (var k in keys)
            o.Add((k, s.Colours.TryGetValue(k, out var c) ? c : Palette[next++ % Palette.Length], counts[k]));
        return o;
    }

    /// <summary>The colour a task's value gives its bar: its value's, grey when its elements do not agree, none when it has none.</summary>
    public static string? ColourOf(TaskFieldValue v, IReadOnlyList<(string Value, string Colour, int Tasks)> legend) =>
        v.Value == null ? null : v.Mixed ? MixedColour : legend.FirstOrDefault(l => string.Equals(l.Value, v.Value, StringComparison.OrdinalIgnoreCase)).Colour;

    /// <summary>Each task's value of each field asked, worked out once.</summary>
    public static Dictionary<string, Dictionary<string, TaskFieldValue>> All(Plan p, IModelView m, TaskElements links, IReadOnlyList<string> fields, IEnumerable<string>? tasks = null)
    {
        var o = new Dictionary<string, Dictionary<string, TaskFieldValue>>();
        foreach (var id in tasks ?? p.Rows.Select(t => t.Id))
        {
            if (!p.ById.ContainsKey(id)) continue;
            o[id] = fields.ToDictionary(f => f, f => Of(p, m, links, id, f), StringComparer.OrdinalIgnoreCase);
        }
        return o;
    }

    /// <summary>The tasks' values of fields (<c>planning.taskFields</c>: {fields, tasks?} → {tasks: [{id, fields: {token: {value, mixed, values, count, missing}}}]}).</summary>
    public static JsonObject Query(IQueryContext q, JsonObject a)
    {
        var fields = (a["fields"] as JsonArray ?? []).Select(x => ((string?)x)?.Trim()).OfType<string>().Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (fields.Count == 0) throw new CommandException("Which fields?");
        if (fields.Count > 32) throw new CommandException("At most 32 fields at once.");
        var p = PlanningModule.ReadResolved(q, out var links);
        var only = a["tasks"] is JsonArray ids ? ids.Select(x => (string?)x).OfType<string>().ToList() : null;
        var all = All(p, q.Model, links, fields, only);
        return new JsonObject
        {
            ["tasks"] = new JsonArray(all.Select(kv => (JsonNode)new JsonObject
            {
                ["id"] = kv.Key,
                ["fields"] = new JsonObject(kv.Value.Select(f => KeyValuePair.Create(f.Key, (JsonNode?)f.Value.ToJson()))),
            }).ToArray()),
        };
    }
}
