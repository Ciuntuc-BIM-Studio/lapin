using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lapin.Phases;

public sealed record ProjectPhase(string Id, string Name, string Kind);
public sealed record PhaseFilter(string Id, string Name, IReadOnlyDictionary<string, string> States);
public sealed record PhaseOverride(string Color, double Opacity);

/// <summary>
/// The project's phases (the old Lapin's phase model): an ordered list —
/// existing, construction, execution — each element created in one and maybe
/// demolished in a later one. Seen from a view's phase an element is new,
/// existing, demolished or temporary (created and demolished in that phase:
/// formwork, scaffolding); one not created yet, or demolished before, is not
/// there. A filter says, per state, whether it shows as it is, with the
/// state's override, or not at all.
/// </summary>
public sealed record PhaseSettings(IReadOnlyList<ProjectPhase> Phases, IReadOnlyList<PhaseFilter> Filters, IReadOnlyDictionary<string, PhaseOverride> Overrides,
    string? Active = null, string? ActiveFilter = null)
{
    public static readonly string[] Kinds = ["existing", "construction", "execution"];
    public static readonly string[] States = ["new", "existing", "demolished", "temporary"];
    public static readonly string[] Presentations = ["show", "override", "hide"];

    static PhaseFilter F(string id, string name, string n, string e, string d, string t) =>
        new(id, name, new Dictionary<string, string> { ["new"] = n, ["existing"] = e, ["demolished"] = d, ["temporary"] = t });

    public static PhaseSettings Default => new(
        [new("existing", "Existing", "existing"), new("new-construction", "New construction", "construction")],
        [
            F("show-all", "Show all", "show", "override", "override", "override"),
            F("show-complete", "Show complete", "show", "show", "hide", "hide"),
            F("show-new", "Show new", "show", "hide", "hide", "hide"),
            F("show-previous-new", "Show previous + new", "show", "override", "hide", "hide"),
            F("show-demo-new", "Show demolition + new", "show", "hide", "override", "hide"),
            F("show-execution", "Show execution", "show", "override", "override", "show"),
        ],
        new Dictionary<string, PhaseOverride>
        {
            ["new"] = new("#4f9d5d", 1), ["existing"] = new("#a6a6a6", 1), ["demolished"] = new("#d64545", 0.45), ["temporary"] = new("#e8a33a", 0.85),
        });

    /// <summary>The phase new elements go in: the active one, else the last.</summary>
    public ProjectPhase? ActivePhase => Phases.FirstOrDefault(p => p.Id == Active) ?? Phases.LastOrDefault();

    /// <summary>The phase an unphased element counts as created in: the first construction phase, else the first.</summary>
    public ProjectPhase? DefaultCreated => Phases.FirstOrDefault(p => p.Kind == "construction") ?? Phases.FirstOrDefault();

    /// <summary>An element's state seen from phase <paramref name="view"/>, or null when it is not there.</summary>
    public string? State(string? created, string? demolished, string view)
    {
        int Index(string? id) => id == null ? -1 : Phases.ToList().FindIndex(p => p.Id == id);
        var v = Index(view);
        if (v < 0) return "new";
        var c = Index(created);
        if (c < 0) c = DefaultCreated is { } dc ? Phases.ToList().IndexOf(dc) : 0;
        var d = Index(demolished);
        if (c > v) return null;
        if (d >= 0 && d < v) return null;
        if (d == v) return c == v ? "temporary" : "demolished";
        return c == v ? "new" : "existing";
    }

    /// <summary>What a filter does with a state (no filter: show it as it is).</summary>
    public string Presentation(string? filterId, string state) =>
        Filters.FirstOrDefault(f => f.Id == filterId) is { } f ? f.States.GetValueOrDefault(state, "show") : "show";

    static bool Hex(string? s) => s != null && Regex.IsMatch(s, "^#[0-9a-fA-F]{6}$");

    /// <summary>Settings read from the project, malformed parts falling back to the defaults; null when there are none.</summary>
    public static PhaseSettings? Read(JsonNode? raw)
    {
        if (raw is not JsonObject o) return null;
        var d = Default;
        var phases = o["phases"] is JsonArray pa
            ? pa.OfType<JsonObject>().Where(p => (string?)p["id"] is { Length: > 0 } && p["name"] is JsonValue nv && nv.TryGetValue<string>(out _))
                .Select(p => new ProjectPhase((string)p["id"]!, (string)p["name"]!, Kinds.Contains((string?)p["kind"]) ? (string)p["kind"]! : "construction")).ToList()
            : [];
        string Pres(JsonNode? v) => v is JsonValue j && j.TryGetValue<string>(out var s) && Presentations.Contains(s) ? s : "show";
        var filters = o["filters"] is JsonArray fa
            ? fa.OfType<JsonObject>().Where(f => (string?)f["id"] is { Length: > 0 } && f["name"] is JsonValue)
                .Select(f => new PhaseFilter((string)f["id"]!, (string?)f["name"] ?? "", States.ToDictionary(st => st, st => Pres(f["states"]?[st])))).ToList()
            : d.Filters.ToList();
        var overrides = States.ToDictionary(st => st, st =>
        {
            var v = o["overrides"]?[st] as JsonObject;
            var color = (string?)v?["color"];
            var op = Lapin.Model.Json.Number(v?["opacity"]);
            return new PhaseOverride(Hex(color) ? color! : d.Overrides[st].Color, op is >= 0 and <= 1 ? op.Value : d.Overrides[st].Opacity);
        });
        var list = phases.Count > 0 ? phases : d.Phases.ToList();
        var active = (string?)o["active"] is { } a && list.Any(p => p.Id == a) ? a : null;
        var activeFilter = (string?)o["activeFilter"] is { } af && filters.Any(f => f.Id == af) ? af : null;
        return new PhaseSettings(list, filters, overrides, active, activeFilter);
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["phases"] = new JsonArray(Phases.Select(p => (JsonNode)new JsonObject { ["id"] = p.Id, ["name"] = p.Name, ["kind"] = p.Kind }).ToArray()),
            ["filters"] = new JsonArray(Filters.Select(f => (JsonNode)new JsonObject
            {
                ["id"] = f.Id, ["name"] = f.Name, ["states"] = new JsonObject(f.States.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
            }).ToArray()),
            ["overrides"] = new JsonObject(Overrides.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)new JsonObject { ["color"] = kv.Value.Color, ["opacity"] = kv.Value.Opacity }))),
        };
        if (Active != null) o["active"] = Active;
        if (ActiveFilter != null) o["activeFilter"] = ActiveFilter;
        return o;
    }

    /// <summary>A fresh id from a name, not among those taken.</summary>
    public static string FreshId(string name, IEnumerable<string> taken)
    {
        var used = taken.ToHashSet();
        var b = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (b.Length == 0) b = "phase";
        var id = b;
        for (var i = 2; used.Contains(id); i++) id = $"{b}-{i}";
        return id;
    }
}
