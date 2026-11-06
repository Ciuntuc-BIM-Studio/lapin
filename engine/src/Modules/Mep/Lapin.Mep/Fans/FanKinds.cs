using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep.Fans;

/// <summary>
/// A parameter of an exhaust fan's type, as the configurator edits it: a length, a number, a choice or a switch;
/// <see cref="ShowIf"/> says when it applies (the spec's keys and the values they may have, as decision 333's fields).
/// </summary>
public sealed record FanParam(string Key, string Label, string Kind, double? Min = null, double? Max = null, string[]? Choices = null,
    IReadOnlyDictionary<string, string[]>? ShowIf = null)
{
    public JsonObject ToJson() => new()
    {
        ["key"] = Key, ["label"] = Label, ["kind"] = Kind, ["min"] = Min, ["max"] = Max,
        ["choices"] = Choices is { } c ? new JsonArray([.. c.Select(x => (JsonNode)x)]) : null,
        ["showIf"] = ShowIf is { } s ? new JsonObject(s.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)new JsonArray([.. kv.Value.Select(v => (JsonNode)v)])))) : null,
    };

    /// <summary>Whether the parameter applies to a spec (every condition holds).</summary>
    public bool Applies(Func<string, string> value) => ShowIf is not { Count: > 0 } all || all.All(c => c.Value.Contains(value(c.Key)));
}

/// <summary>
/// A kind of exhaust fan (testers' report #55): its key, label, IFC predefined type (IfcFanTypeEnum), the mountings it
/// takes (the first its default) and the spec a new type starts with.
/// </summary>
public sealed record FanKind(string Key, string Label, string Predefined, string[] Mountings, JsonObject Defaults)
{
    public JsonObject ToJson() => new()
    {
        ["key"] = Key, ["label"] = Label, ["predefined"] = Predefined,
        ["mountings"] = new JsonArray([.. Mountings.Select(x => (JsonNode)x)]), ["defaults"] = Defaults.DeepClone(),
    };
}

/// <summary>The kinds of exhaust fan Lapin builds, their parameters, the finishes, and a type's spec read with its kind's defaults under it.</summary>
public static class FanKinds
{
    public static readonly string[] Diameters = ["100", "125", "150", "200"];
    public static readonly string[] Mountings = ["wall", "ceiling", "window", "inline"];
    public static readonly string[] Controls = ["switch", "humidity", "timer", "humidityTimer", "motion"];
    public static readonly string[] Patterns = ["louvres", "slots", "rings", "plain"];
    public static readonly string[] Finishes = ["white", "silver", "black", "stainless", "chrome"];

    /// <summary>A finish's material and colour (made in the project by name when a fan uses it).</summary>
    public static readonly IReadOnlyDictionary<string, (string Material, string Color)> FinishMaterials = new Dictionary<string, (string, string)>
    {
        ["white"] = ("Plastic white (fan)", "#f1f1ee"),
        ["silver"] = ("Aluminium silver (fan)", "#c3c8cd"),
        ["black"] = ("Plastic black (fan)", "#2a2a2a"),
        ["stainless"] = ("Stainless steel brushed", "#b5babe"),
        ["chrome"] = ("Chrome", "#dde1e4"),
    };

    /// <summary>The fan's works inside (the motor's housing, the sleeve, the shutter): grey plastic.</summary>
    public static readonly (string Material, string Color) Housing = ("Plastic grey (fan housing)", "#8f969d");
    public static readonly (string Material, string Color) Light = ("Opal diffuser", "#fbf7e4");
    public static readonly (string Material, string Color) Panel = ("Black glass", "#202326");

    static JsonObject D(params (string Key, JsonNode? Value)[] v) { var o = new JsonObject(); foreach (var (k, x) in v) o[k] = x; return o; }
    static Dictionary<string, string[]> If(string key, params string[] values) => new() { [key] = values };

    public static readonly IReadOnlyList<FanKind> All =
    [
        new("wallAxial", "Wall fan (axial)", "PROPELLORAXIAL", ["wall", "window"],
            D(("diameter", "100"), ("grille", "round"), ("faceSize", 0.165), ("pattern", "rings"), ("finish", "white"), ("airflow", 95.0), ("power", 14.0), ("control", "switch"), ("backdraft", true))),
        new("ceilingDuct", "Ceiling fan with duct", "CENTRIFUGALRADIAL", ["ceiling"],
            D(("diameter", "100"), ("grille", "square"), ("faceSize", 0.25), ("pattern", "slots"), ("finish", "white"), ("airflow", 110.0), ("power", 18.0), ("control", "switch"), ("backdraft", true))),
        new("inline", "Inline duct fan", "TUBEAXIAL", ["inline"],
            D(("diameter", "125"), ("grille", "round"), ("faceSize", 0.2), ("pattern", "plain"), ("finish", "white"), ("airflow", 280.0), ("power", 25.0), ("control", "switch"), ("backdraft", true), ("bodyLength", 0.3))),
        new("window", "Window fan", "PROPELLORAXIAL", ["window", "wall"],
            D(("diameter", "150"), ("grille", "square"), ("faceSize", 0.24), ("pattern", "louvres"), ("finish", "white"), ("airflow", 240.0), ("power", 30.0), ("control", "switch"), ("backdraft", true))),
        new("ceilingLight", "Ceiling fan with light", "CENTRIFUGALRADIAL", ["ceiling"],
            D(("diameter", "100"), ("grille", "round"), ("faceSize", 0.3), ("pattern", "plain"), ("finish", "white"), ("airflow", 90.0), ("power", 15.0), ("control", "switch"), ("backdraft", true), ("light", true))),
        new("louvrePanel", "Fan with louvre panel", "PROPELLORAXIAL", ["wall"],
            D(("diameter", "100"), ("grille", "round"), ("faceSize", 0.2), ("pattern", "rings"), ("finish", "white"), ("airflow", 105.0), ("power", 16.0), ("control", "switch"), ("backdraft", true),
              ("panelWidth", 0.6), ("panelHeight", 0.42))),
    ];

    /// <summary>The parameters of every kind (the configurator's fields), each with when it applies.</summary>
    public static readonly IReadOnlyList<FanParam> Params =
    [
        new("mounting", "Mounting", "choice", Choices: Mountings),
        new("diameter", "Duct size (mm)", "choice", Choices: Diameters),
        new("grille", "Grille shape", "choice", Choices: ["round", "square"]),
        new("faceSize", "Grille size", "length", 0.1, 0.6),
        new("pattern", "Grille pattern", "choice", Choices: Patterns),
        new("finish", "Finish", "choice", Choices: Finishes),
        new("airflow", "Airflow (m³/h)", "number", 10, 3000),
        new("power", "Power (W)", "number", 1, 1000),
        new("control", "Control", "choice", Choices: Controls),
        new("backdraft", "Backdraft shutter", "bool"),
        new("light", "Light", "bool", ShowIf: If("kind", "ceilingLight")),
        new("bodyLength", "Body length", "length", 0.15, 0.8, ShowIf: If("kind", "inline")),
        new("panelWidth", "Panel width", "length", 0.3, 1.2, ShowIf: If("kind", "louvrePanel")),
        new("panelHeight", "Panel height", "length", 0.2, 0.9, ShowIf: If("kind", "louvrePanel")),
    ];

    public static FanKind? Find(string? key) => All.FirstOrDefault(k => k.Key == key);

    public static JsonObject NewSpec(FanKind kind, JsonObject? given)
    {
        var spec = new JsonObject { ["kind"] = kind.Key, ["mounting"] = kind.Mountings[0] };
        foreach (var (k, v) in kind.Defaults) spec[k] = v?.DeepClone();
        if (given != null) Merge(spec, given, kind);
        return spec;
    }

    /// <summary>Values over a spec, each checked against its parameter (a null puts the kind's default back); a kind's mountings only.</summary>
    public static void Merge(JsonObject spec, JsonObject given, FanKind kind)
    {
        foreach (var (key, value) in given)
        {
            if (key == "kind") continue;
            if (key == "articles") { spec[key] = value?.DeepClone(); continue; }
            var p = Params.FirstOrDefault(x => x.Key == key) ?? throw new CommandException($"An exhaust fan has no „{key}”.");
            if (value is null) { if (kind.Defaults[key] is { } d) spec[key] = d.DeepClone(); else spec.Remove(key); continue; }
            switch (p.Kind)
            {
                case "length" or "number":
                    var n = Json.Number(value) ?? throw new CommandException($"{p.Label} is a number.");
                    if (n < p.Min - 1e-9 || n > p.Max + 1e-9) throw new CommandException($"{p.Label} is between {p.Min?.ToString(CultureInfo.InvariantCulture)} and {p.Max?.ToString(CultureInfo.InvariantCulture)}.");
                    spec[key] = n;
                    break;
                case "bool":
                    spec[key] = value is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : throw new CommandException($"{p.Label} is on or off.");
                    break;
                default:
                    var s = value is JsonValue sv && sv.TryGetValue<string>(out var str) ? str : value.ToJsonString().Trim('"');
                    if (!p.Choices!.Contains(s)) throw new CommandException($"{p.Label} is one of {string.Join(", ", p.Choices!)}.");
                    if (key == "mounting" && !kind.Mountings.Contains(s)) throw new CommandException($"A {kind.Label.ToLowerInvariant()} is mounted {string.Join(" or ", kind.Mountings)}.");
                    spec[key] = s;
                    break;
            }
        }
    }

    public static JsonArray Catalog() => new([.. All.Select(k => (JsonNode)k.ToJson())]);
    public static JsonArray ParamsCatalog() => new([.. Params.Select(p => (JsonNode)p.ToJson())]);
}

/// <summary>A fan type's spec read with its kind's defaults under it.</summary>
public sealed class FanSpec(JsonObject? spec)
{
    readonly JsonObject o = spec ?? new JsonObject();
    public FanKind Kind { get; } = FanKinds.Find((string?)spec?["kind"]) ?? FanKinds.All[0];

    JsonNode? Get(string key) => o[key] ?? Kind.Defaults[key];
    public string Str(string key, string fallback = "") => Get(key) is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;
    public double Num(string key, double fallback = 0) => Json.Number(Get(key)) ?? fallback;
    public bool Bool(string key) => Get(key) is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public string Mounting => Str("mounting", Kind.Mountings[0]) is var m && Kind.Mountings.Contains(m) ? m : Kind.Mountings[0];
    /// <summary>The duct's nominal diameter (m).</summary>
    public double Diameter => double.TryParse(Str("diameter", "100"), NumberStyles.Float, CultureInfo.InvariantCulture, out var mm) ? mm / 1000 : 0.1;
    public bool Square => Str("grille", "round") == "square";
    public double Face => Math.Max(Num("faceSize", Diameter + 0.065), Diameter + 0.02);
    public double Airflow => Num("airflow", 100);
    public double Power => Num("power", 15);
    public string Finish => Str("finish", "white");
    public string FinishMaterial => FanKinds.FinishMaterials.TryGetValue(Finish, out var f) ? f.Material : FanKinds.FinishMaterials["white"].Material;
    public string Control => Str("control", "switch");
    public bool Backdraft => Bool("backdraft");
    public bool HasLight => Kind.Key == "ceilingLight" && Bool("light");
    public bool Inline => Mounting == "inline";
    public bool OnCeiling => Mounting == "ceiling";
    /// <summary>On a wall or in a window: its face upright, against the room.</summary>
    public bool Upright => Mounting is "wall" or "window";
    /// <summary>The face's width and height (a louvre panel's whole panel).</summary>
    public (double W, double H) FaceSize => Kind.Key == "louvrePanel" ? (Num("panelWidth", 0.6), Num("panelHeight", 0.42)) : (Face, Face);
}
