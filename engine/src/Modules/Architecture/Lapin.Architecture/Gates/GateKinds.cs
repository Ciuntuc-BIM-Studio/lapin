using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Gates;

/// <summary>
/// A parameter of a gate's type, as the configurator edits it: a length, a number, a choice or a switch;
/// <see cref="ShowIf"/> says when it applies (the spec's keys and the values they may have, as decision 333's fields).
/// </summary>
public sealed record GateParam(string Key, string Label, string Kind, double? Min = null, double? Max = null, string[]? Choices = null,
    IReadOnlyDictionary<string, string[]>? ShowIf = null, string Group = "Gate")
{
    public JsonObject ToJson() => new()
    {
        ["key"] = Key, ["label"] = Label, ["kind"] = Kind, ["min"] = Min, ["max"] = Max, ["group"] = Group,
        ["choices"] = Choices is { } c ? new JsonArray([.. c.Select(x => (JsonNode)x)]) : null,
        ["showIf"] = ShowIf is { } s ? new JsonObject(s.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)new JsonArray([.. kv.Value.Select(v => (JsonNode)v)])))) : null,
    };
}

/// <summary>A kind of gate (testers' report #57): its key, label, its IFC operation (IfcDoorTypeOperationEnum) and the spec a new type starts with.</summary>
public sealed record GateKind(string Key, string Label, string Operation, string? UserOperation, JsonObject Defaults)
{
    public JsonObject ToJson() => new() { ["key"] = Key, ["label"] = Label, ["operation"] = Operation, ["defaults"] = Defaults.DeepClone() };
    public bool Swings => Key is "swing" or "doubleSwing" or "pedestrian" or "swingWicket";
    public bool Slides => Key is "sliding" or "cantilever";
}

/// <summary>The kinds of gate Lapin builds, their parameters and materials, and a type's spec read with its kind's defaults under it.</summary>
public static class GateKinds
{
    public static readonly string[] Infills = ["verticalBars", "horizontalSlats", "solid", "mesh", "perforated"];
    public static readonly string[] Tops = ["flat", "arched", "pointed"];
    public static readonly string[] Piers = ["masonry", "steel", "none"];
    public static readonly string[] Caps = ["flat", "pyramid", "none"];
    public static readonly string[] Roles = ["frame", "infill", "pier", "cap", "hardware", "motor"];

    /// <summary>The material each role takes in a new type, with its colour (made in the project by name).</summary>
    public static readonly IReadOnlyDictionary<string, (string Material, string Color)> RoleDefaults = new Dictionary<string, (string, string)>
    {
        ["frame"] = ("Steel painted anthracite", "#363b40"),
        ["infill"] = ("Steel painted anthracite", "#363b40"),
        ["pier"] = ("Brick facing", "#a4583f"),
        ["cap"] = ("Precast concrete", "#bdb8ad"),
        ["hardware"] = ("Steel galvanised", "#a7adb2"),
        ["motor"] = ("Plastic grey (motor)", "#8f969d"),
    };

    /// <summary>Materials an infill suggests when the type is made (a timber gate's larch, a mesh's galvanised wire).</summary>
    public static readonly IReadOnlyDictionary<string, string> KnownColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Larch wood"] = "#a8754a", ["Steel mesh galvanised"] = "#9aa1a6", ["Perforated steel sheet"] = "#5a6066",
        ["Rendered masonry"] = "#d9d3c7", ["Steel painted black"] = "#1f2124", ["Aluminium white"] = "#efefec",
        ["Stone cladding"] = "#b9ad97", ["Steel painted green"] = "#2f4a3a",
    };

    static JsonObject D(params (string Key, JsonNode? Value)[] v) { var o = new JsonObject(); foreach (var (k, x) in v) o[k] = x; return o; }
    static Dictionary<string, string[]> If(string key, params string[] values) => new() { [key] = values };

    static JsonObject Common(double width, double height, string infill, string piers, params (string Key, JsonNode? Value)[] more)
    {
        var o = D(("width", width), ("height", height), ("gap", 0.05), ("frame", 0.06), ("infill", infill), ("spacing", 0.12), ("top", "flat"), ("rise", 0.25), ("spears", false),
            ("piers", piers), ("pierSize", piers == "steel" ? 0.1 : 0.4), ("pierHeight", height + 0.2), ("cap", "pyramid"), ("motor", false), ("lock", true), ("openingAngle", 90.0));
        foreach (var (k, v) in more) o[k] = v;
        return o;
    }

    public static readonly IReadOnlyList<GateKind> All =
    [
        new("swing", "Swing gate", "SINGLE_SWING_LEFT", null, Common(2.5, 1.6, "verticalBars", "masonry")),
        new("doubleSwing", "Double swing gate", "DOUBLE_DOOR_SINGLE_SWING", null, Common(3.6, 1.6, "verticalBars", "masonry")),
        new("sliding", "Sliding gate", "SLIDING_TO_LEFT", null, Common(4.0, 1.6, "horizontalSlats", "masonry", ("gap", 0.08), ("motor", true))),
        new("cantilever", "Cantilever sliding gate", "SLIDING_TO_LEFT", null, Common(4.5, 1.6, "verticalBars", "steel", ("gap", 0.1), ("motor", true))),
        new("pedestrian", "Pedestrian gate", "SINGLE_SWING_LEFT", null, Common(1.0, 1.6, "verticalBars", "masonry")),
        new("swingWicket", "Gate with side pedestrian door", "USERDEFINED", "Double swing gate with a pedestrian door", Common(3.6, 1.6, "verticalBars", "masonry", ("wicketWidth", 1.0))),
        new("folding", "Folding gate", "DOUBLE_DOOR_FOLDING", null, Common(3.6, 1.6, "solid", "steel", ("panels", 2))),
    ];

    static readonly string[] SwingKinds = ["swing", "doubleSwing", "pedestrian", "swingWicket", "folding"];

    /// <summary>Every kind's parameters (the configurator's fields, by group), each with when it applies.</summary>
    public static readonly IReadOnlyList<GateParam> Params =
    [
        new("width", "Clear width", "length", 0.6, 12),
        new("height", "Height", "length", 0.6, 3.5),
        new("gap", "Gap to the ground", "length", 0, 0.3),
        new("wicketWidth", "Pedestrian door width", "length", 0.7, 1.5, ShowIf: If("kind", "swingWicket")),
        new("panels", "Panels each side", "number", 1, 4, ShowIf: If("kind", "folding")),
        new("openingAngle", "Opening angle (°)", "number", 45, 180, ShowIf: If("kind", SwingKinds)),
        new("frame", "Frame size", "length", 0.03, 0.15, Group: "Leaves"),
        new("infill", "Infill", "choice", Choices: Infills, Group: "Leaves"),
        new("spacing", "Bar or slat spacing", "length", 0.05, 0.4, ShowIf: If("infill", "verticalBars", "horizontalSlats"), Group: "Leaves"),
        new("spears", "Spear tips on the bars", "bool", ShowIf: If("infill", "verticalBars"), Group: "Leaves"),
        new("top", "Top", "choice", Choices: Tops, Group: "Leaves"),
        new("rise", "Rise of the top", "length", 0.05, 1.2, ShowIf: If("top", "arched", "pointed"), Group: "Leaves"),
        new("piers", "Piers", "choice", Choices: Piers, Group: "Piers"),
        new("pierSize", "Pier size", "length", 0.05, 0.9, ShowIf: If("piers", "masonry", "steel"), Group: "Piers"),
        new("pierHeight", "Pier height", "length", 0.5, 4, ShowIf: If("piers", "masonry", "steel"), Group: "Piers"),
        new("cap", "Cap", "choice", Choices: Caps, ShowIf: If("piers", "masonry"), Group: "Piers"),
        new("motor", "Motor", "bool", Group: "Hardware"),
        new("lock", "Handle and lock", "bool", Group: "Hardware"),
    ];

    public static GateKind? Find(string? key) => All.FirstOrDefault(k => k.Key == key);

    public static JsonObject NewSpec(GateKind kind, JsonObject? given)
    {
        var spec = new JsonObject { ["kind"] = kind.Key };
        foreach (var (k, v) in kind.Defaults) spec[k] = v?.DeepClone();
        spec["materials"] = new JsonObject(RoleDefaults.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value.Material)));
        if (given != null) Merge(spec, given, kind);
        return spec;
    }

    /// <summary>Values over a spec, each checked against its parameter (a null puts the kind's default back); materials merged by role.</summary>
    public static void Merge(JsonObject spec, JsonObject given, GateKind kind)
    {
        foreach (var (key, value) in given)
        {
            if (key == "kind") continue;
            if (key == "materials")
            {
                if (value is not JsonObject mats) throw new CommandException("Materials are given by role.");
                var own = spec["materials"] as JsonObject ?? new JsonObject();
                foreach (var (role, name) in mats)
                {
                    if (!Roles.Contains(role)) throw new CommandException($"A gate's materials are its {string.Join(", ", Roles)}.");
                    if (((string?)name)?.Trim() is { Length: > 0 } n) own[role] = n; else own[role] = RoleDefaults[role].Material;
                }
                spec["materials"] = own;
                continue;
            }
            var p = Params.FirstOrDefault(x => x.Key == key) ?? throw new CommandException($"A gate has no „{key}”.");
            if (value is null) { if (kind.Defaults[key] is { } d) spec[key] = d.DeepClone(); else spec.Remove(key); continue; }
            switch (p.Kind)
            {
                case "length" or "number":
                    var n = Json.Number(value) ?? throw new CommandException($"{p.Label} is a number.");
                    if (n < p.Min - 1e-9 || n > p.Max + 1e-9) throw new CommandException($"{p.Label} is between {p.Min?.ToString(CultureInfo.InvariantCulture)} and {p.Max?.ToString(CultureInfo.InvariantCulture)}.");
                    spec[key] = p.Kind == "number" && key == "panels" ? Math.Round(n) : n;
                    break;
                case "bool":
                    spec[key] = value is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : throw new CommandException($"{p.Label} is on or off.");
                    break;
                default:
                    var s = value is JsonValue sv && sv.TryGetValue<string>(out var str) ? str : value.ToJsonString().Trim('"');
                    if (!p.Choices!.Contains(s)) throw new CommandException($"{p.Label} is one of {string.Join(", ", p.Choices!)}.");
                    spec[key] = s;
                    break;
            }
        }
    }

    public static JsonArray Catalog() => new([.. All.Select(k => (JsonNode)k.ToJson())]);
    public static JsonArray ParamsCatalog() => new([.. Params.Select(p => (JsonNode)p.ToJson())]);
}

/// <summary>A gate type's spec read with its kind's defaults under it.</summary>
public sealed class GateSpec(JsonObject? spec)
{
    readonly JsonObject o = spec ?? new JsonObject();
    public GateKind Kind { get; } = GateKinds.Find((string?)spec?["kind"]) ?? GateKinds.All[0];

    JsonNode? Get(string key) => o[key] ?? Kind.Defaults[key];
    public string Str(string key, string fallback = "") => Get(key) is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;
    public double Num(string key, double fallback = 0) => Json.Number(Get(key)) ?? fallback;
    public bool Bool(string key) => Get(key) is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public double Width => Num("width", 3);
    public double Height => Num("height", 1.6);
    public double Gap => Num("gap", 0.05);
    public double Frame => Num("frame", 0.06);
    public string Infill => Str("infill", "verticalBars");
    public string Top => Str("top", "flat");
    public string Piers => Str("piers", "masonry");
    /// <summary>A pier's side (0 without piers).</summary>
    public double PierSize => Piers == "none" ? 0 : Num("pierSize", Piers == "steel" ? 0.1 : 0.4);
    public double PierHeight => Num("pierHeight", Height + 0.2);
    public double WicketWidth => Kind.Key == "swingWicket" ? Num("wicketWidth", 1.0) : 0;
    public int Panels => Math.Clamp((int)Math.Round(Num("panels", 2)), 1, 4);
    public double OpeningAngle => Num("openingAngle", 90);
    public bool Motor => Bool("motor");
    public bool Lock => Bool("lock");
    /// <summary>The overall width, from the outer faces of the outer piers.</summary>
    public double Overall => Width + WicketWidth + (Kind.Key == "swingWicket" ? 3 : 2) * PierSize;
    public string MaterialName(string role) => (o["materials"] as JsonObject)?[role] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : GateKinds.RoleDefaults[role].Material;

    /// <summary>A sliding leaf's length: the opening and 100 mm over each pier; a cantilever leaf's adds its counterbalance tail (45 % of the opening).</summary>
    public double LeafLength => Kind.Key switch { "sliding" => Width + 0.2, "cantilever" => Width * 1.45 + 0.1, _ => 0 };

    /// <summary>Where a sliding leaf parks when open, along the gate (local x, the parking side +x): from the opening's edge, as long as the leaf.</summary>
    public (double X0, double X1)? ParkingZone => Kind.Slides ? (Width / 2, Width / 2 + LeafLength) : null;
}
