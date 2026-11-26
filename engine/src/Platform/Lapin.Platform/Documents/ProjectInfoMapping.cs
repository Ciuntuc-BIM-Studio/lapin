using System.Text.Json.Nodes;

namespace Lapin.Platform.Documents;

/// <summary>A source's value: what the source calls it (Archicad's <c>projectInfoName</c>), and the value.</summary>
public sealed record SourceValue(string? Label, string Value);

/// <summary>One row of a source's table: the Lapin key it gives, the source's ids in order, the parts joined when none has a value, and its rules.</summary>
public sealed record ProjectInfoMappingRow(string Key, string[] From, string[] Parts, string? Match, string? When);

/// <summary>
/// How a source's project information becomes the project's information (<see cref="ProjectInfo"/>; stage A1b of docs/plan-archicad.md):
/// Archicad's Project Info (Tapir's <c>GetProjectInfoFields</c>) and what an IFC model says about its project, mapped to Lapin's keys
/// (<c>name</c> and <see cref="ProjectInfo.Grouped"/>). The tables are data, <c>project-info-mapping.json</c>, embedded: the very file
/// Atrium Server embeds and serves (its docs/project-info-mapping.json; a test checks they are the same bytes), so a value lands under
/// the same key whether Lapin receives it from Archicad or the server maps a proposal. A row takes the first of its <c>from</c> ids with a
/// value (passing its <c>match</c> and <c>when</c>), else its <c>parts</c> joined; what is left with a value is the extras, kept under
/// <c>source.&lt;source&gt;.&lt;id&gt;</c> (a value a field took already is not kept twice).
/// </summary>
public sealed class ProjectInfoMapping
{
    public const string ResourceName = "Lapin.Platform.Documents.project-info-mapping.json";

    public required string[] Keys { get; init; }
    public required string[] PhaseCodes { get; init; }
    public required string Join { get; init; }
    public required IReadOnlyDictionary<string, ProjectInfoMappingRow[]> Sources { get; init; }

    /// <summary>The file's text as embedded.</summary>
    public required string Text { get; init; }

    static readonly Lazy<ProjectInfoMapping> Embedded = new(() =>
    {
        using var stream = typeof(ProjectInfoMapping).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The project information's mapping table (project-info-mapping.json) is not embedded.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    });

    /// <summary>The table Lapin ships.</summary>
    public static ProjectInfoMapping Default => Embedded.Value;

    /// <summary>A table from its text (the embedded one, or the server's <c>GET …/info/mapping</c>).</summary>
    public static ProjectInfoMapping Parse(string text)
    {
        var o = JsonNode.Parse(text) as JsonObject ?? throw new InvalidDataException("project-info-mapping.json is not an object.");
        static string[] Strings(JsonNode? n) => [.. (n as JsonArray ?? []).Select(x => (string?)x).OfType<string>()];
        var sources = new Dictionary<string, ProjectInfoMappingRow[]>(StringComparer.Ordinal);
        foreach (var (source, rows) in o["sources"] as JsonObject ?? [])
            sources[source] = [.. (rows as JsonArray ?? []).OfType<JsonObject>().Select(r =>
                new ProjectInfoMappingRow((string?)r["key"] ?? "", Strings(r["from"]), Strings(r["parts"]), (string?)r["match"], (string?)r["when"]))];
        return new ProjectInfoMapping { Keys = Strings(o["keys"]), PhaseCodes = Strings(o["phaseCodes"]), Join = (string?)o["join"] ?? ", ", Sources = sources, Text = text };
    }

    /// <summary>A phase code Lapin offers (SF, DALI… PTh) in its own spelling, when the text is one ignoring case; else null.</summary>
    public string? PhaseCode(string text) => PhaseCodes.FirstOrDefault(c => string.Equals(c, text.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>A source's values (id → label and value) mapped to Lapin's keys, and what is left with a value (the extras), as Atrium Server maps a proposal.</summary>
    public (Dictionary<string, string> Fields, Dictionary<string, SourceValue> Extras) Map(string source, IReadOnlyDictionary<string, SourceValue> raw)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        string? Value(string id) => raw.TryGetValue(id, out var v) && v.Value.Trim() is { Length: > 0 } t ? t : null;
        foreach (var row in Sources.GetValueOrDefault(source) ?? [])
        {
            if (fields.ContainsKey(row.Key)) continue;
            string? taken = null, takenFrom = null;
            foreach (var id in row.From)
            {
                if (Value(id) is not { } v) continue;
                if (row.Match == "phaseCode") { if (PhaseCode(v) is { } code) v = code; else continue; }
                else if (row.Match == "notPhaseCode" && PhaseCode(v) != null) continue;
                if (row.When != null && (Value(row.When) is not { } w || w == v)) continue;
                taken = v;
                takenFrom = id;
                break;
            }
            string[] parts = [.. row.Parts.Where(p => Value(p) != null)];
            if (taken == null && parts.Length > 0) taken = string.Join(Join, parts.Select(p => Value(p)!));
            if (taken == null) continue;
            fields[row.Key] = taken;
            if (takenFrom != null) used.Add(takenFrom);
            foreach (var p in parts) used.Add(p);
        }
        var extras = new Dictionary<string, SourceValue>(StringComparer.Ordinal);
        foreach (var (id, v) in raw)
        {
            var value = v.Value.Trim();
            if (!used.Contains(id) && value.Length > 0 && !fields.ContainsValue(value)) extras[id] = v with { Value = value };
        }
        return (fields, extras);
    }

    /// <summary>
    /// A source's values as given: Tapir's <c>GetProjectInfoFields</c> answer (<c>{fields: […]}</c> or its list
    /// <c>[{projectInfoId, projectInfoName, projectInfoValue}]</c>), or an object <c>{id: value}</c> / <c>{id: {label, value}}</c>. An entry
    /// without an id is skipped; empty values are kept (the mapping skips them).
    /// </summary>
    public static Dictionary<string, SourceValue> Raw(JsonNode? node)
    {
        var o = new Dictionary<string, SourceValue>(StringComparer.Ordinal);
        static string? Text(JsonNode? n) => n is JsonValue v ? v.TryGetValue<string>(out var s) ? s : v.ToJsonString() : null;
        void Add(string? id, string? label, JsonNode? value)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 100) return;
            o[id] = new SourceValue(label?.Trim() is { Length: > 0 } l ? l : null, Text(value) ?? "");
        }
        if (node is JsonObject { } wrapped && wrapped["fields"] is JsonArray inner) node = inner;
        switch (node)
        {
            case JsonArray list:
                foreach (var item in list.OfType<JsonObject>())
                    Add(Text(item["projectInfoId"]) ?? Text(item["id"]), Text(item["projectInfoName"]) ?? Text(item["label"]), item["projectInfoValue"] ?? item["value"]);
                break;
            case JsonObject map:
                foreach (var (id, v) in map)
                    if (v is JsonObject entry) Add(id, Text(entry["label"]), entry["value"]); else Add(id, null, v);
                break;
        }
        return o;
    }
}
