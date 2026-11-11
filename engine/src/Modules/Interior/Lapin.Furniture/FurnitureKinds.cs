using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Furniture;

/// <summary>A parameter of a furniture kind, as the types dialog edits it: a length, a number, a choice or a switch.</summary>
public sealed record KindParam(string Key, string Label, string Kind, double? Min = null, double? Max = null, string[]? Choices = null, string[]? Layouts = null);

/// <summary>
/// A kind of furniture (docs/plan-furniture.md, Felurile): its key, label, group, IFC predefined type (USERDEFINED with
/// its object type when IFC has none), the size a new type starts with, its own parameters with their first values and
/// the material roles its body uses.
/// </summary>
public sealed record FurnitureKind(string Key, string Label, string Group, string Predefined, string? ObjectType,
    double Width, double Depth, double Height, IReadOnlyList<KindParam> Params, JsonObject Defaults, IReadOnlyList<string> Roles)
{
    public JsonObject ToJson() => new()
    {
        ["key"] = Key, ["label"] = Label, ["group"] = Group, ["predefined"] = Predefined, ["objectType"] = ObjectType,
        ["width"] = Width, ["depth"] = Depth, ["height"] = Height,
        ["params"] = new JsonArray([.. Params.Select(p => (JsonNode)new JsonObject
        {
            ["key"] = p.Key, ["label"] = p.Label, ["kind"] = p.Kind, ["min"] = p.Min, ["max"] = p.Max,
            ["choices"] = p.Choices is { } c ? new JsonArray([.. c.Select(x => (JsonNode)x)]) : null,
            ["layouts"] = p.Layouts is { } ly ? new JsonArray([.. ly.Select(x => (JsonNode)x)]) : null,
        })]),
        ["defaults"] = Defaults.DeepClone(),
        ["roles"] = new JsonArray([.. Roles.Select(r => (JsonNode)r)]),
    };
}

/// <summary>The kinds Lapin builds, and a type's spec read with its kind's defaults under it.</summary>
public static class FurnitureKinds
{
    /// <summary>The material roles, with the material a new type takes for each and its colour.</summary>
    public static readonly IReadOnlyDictionary<string, (string Material, string Color)> RoleDefaults = new Dictionary<string, (string, string)>
    {
        ["body"] = ("Oak veneer", "#b98b5a"),
        ["upholstery"] = ("Upholstery fabric grey", "#8e9399"),
        ["top"] = ("Oak solid wood", "#a87b4c"),
        ["fronts"] = ("Lacquered MDF white", "#ece9e2"),
        ["linen"] = ("Linen white", "#f2efe8"),
        ["metal"] = ("Brushed steel", "#9ea4aa"),
        ["glass"] = ("Black glass", "#202326"),
        // A kitchen's plinth and handles (decision 472); a type without them takes its body's and its metal's.
        ["plinth"] = ("Plinth anthracite", "#3c3f43"),
        ["handles"] = ("Brushed steel", "#9ea4aa"),
    };

    public static readonly string[] RoleOrder = ["body", "upholstery", "top", "fronts", "linen", "metal", "glass", "plinth", "handles"];

    static KindParam L(string k, string l, double min = 0, double max = 3) => new(k, l, "length", min, max);
    static KindParam N(string k, string l, double min, double max) => new(k, l, "number", min, max);
    static KindParam C(string k, string l, params string[] c) => new(k, l, "choice", Choices: c);
    /// <summary>A parameter shown (and used) for some of the kitchen's layouts only.</summary>
    static KindParam In(KindParam p, params string[] layouts) => p with { Layouts = layouts };
    static KindParam B(string k, string l) => new(k, l, "bool");
    static JsonObject D(params (string Key, JsonNode? Value)[] v) { var o = new JsonObject(); foreach (var (k, x) in v) o[k] = x; return o; }

    static readonly KindParam[] SofaParams =
    [
        N("seats", "Seats", 1, 6), C("arms", "Arms", "both", "left", "right", "none"), L("armWidth", "Arm width", 0.05, 0.4),
        L("seatHeight", "Seat height", 0.3, 0.6), C("chaise", "Chaise longue", "none", "left", "right"),
        L("chaiseLength", "Chaise length", 0.4, 1.6), B("legs", "Legs"),
    ];

    public static readonly IReadOnlyList<FurnitureKind> All =
    [
        new("sofa", "Sofa", "Seating", "SOFA", null, 2.2, 0.95, 0.82, SofaParams,
            D(("seats", 3), ("arms", "both"), ("armWidth", 0.18), ("seatHeight", 0.43), ("chaise", "none"), ("chaiseLength", 0.75), ("legs", true)),
            ["upholstery", "metal"]),
        new("cornerSofa", "Corner sofa", "Seating", "SOFA", null, 2.7, 0.95, 0.82,
        [
            C("corner", "Corner on the", "left", "right"), L("cornerLength", "Length of the corner side", 1.2, 3.5), N("seats", "Seats on the long side", 1, 6),
            C("arms", "Arms", "both", "left", "right", "none"), L("armWidth", "Arm width", 0.05, 0.4), L("seatHeight", "Seat height", 0.3, 0.6),
            B("legs", "Legs"),
        ],
            D(("corner", "left"), ("cornerLength", 1.9), ("seats", 2), ("arms", "both"), ("armWidth", 0.15), ("seatHeight", 0.43), ("legs", true)),
            ["upholstery", "metal"]),
        new("armchair", "Armchair", "Seating", "CHAIR", null, 0.85, 0.85, 0.85,
            [L("armWidth", "Arm width", 0.05, 0.4), L("seatHeight", "Seat height", 0.3, 0.6), B("legs", "Legs")],
            D(("armWidth", 0.17), ("seatHeight", 0.42), ("legs", true)),
            ["upholstery", "metal"]),
        new("chair", "Chair", "Seating", "CHAIR", null, 0.45, 0.52, 0.85,
            [L("seatHeight", "Seat height", 0.3, 0.8), B("arms", "Arms"), B("upholstered", "Upholstered seat")],
            D(("seatHeight", 0.46), ("arms", false), ("upholstered", true)),
            ["body", "upholstery"]),
        new("bed", "Bed", "Beds", "BED", null, 1.6, 2.05, 0.95,
        [
            L("mattressHeight", "Top of the mattress", 0.3, 0.8), B("headboard", "Headboard (as high as the bed)"), B("footboard", "Footboard"),
            N("pillows", "Pillows", 0, 4), B("duvet", "Duvet turned down"),
        ],
            D(("mattressHeight", 0.5), ("headboard", true), ("footboard", false), ("pillows", 2), ("duvet", true)),
            ["body", "upholstery", "linen"]),
        new("bedside", "Bedside table", "Beds", "USERDEFINED", "Bedside table", 0.45, 0.4, 0.5,
            [N("drawers", "Drawers", 0, 4), B("legs", "Legs")],
            D(("drawers", 2), ("legs", true)),
            ["body", "fronts", "metal"]),
        new("wardrobe", "Wardrobe", "Storage", "USERDEFINED", "Wardrobe", 2.0, 0.6, 2.3,
            [N("doors", "Doors", 1, 8), C("opening", "Doors", "hinged", "sliding"), N("drawers", "Drawers at the bottom", 0, 4), L("plinth", "Plinth", 0, 0.2)],
            D(("doors", 3), ("opening", "hinged"), ("drawers", 0), ("plinth", 0.08)),
            ["body", "fronts", "metal"]),
        new("chest", "Chest of drawers", "Storage", "USERDEFINED", "Chest of drawers", 1.0, 0.48, 0.85,
            [N("rows", "Rows of drawers", 1, 8), N("columns", "Columns", 1, 4), B("legs", "Legs")],
            D(("rows", 4), ("columns", 1), ("legs", false)),
            ["body", "fronts", "metal"]),
        new("bookcase", "Bookcase", "Storage", "SHELF", null, 1.0, 0.35, 2.0,
            [N("shelves", "Shelves", 1, 10), L("doorsHeight", "Doors at the bottom, as high as", 0, 1.5)],
            D(("shelves", 5), ("doorsHeight", 0.0)),
            ["body", "fronts", "metal"]),
        new("sideboard", "Sideboard", "Storage", "USERDEFINED", "Sideboard", 1.8, 0.45, 0.75,
            [N("doors", "Doors", 0, 6), N("drawers", "Drawers on top", 0, 6), B("legs", "Legs")],
            D(("doors", 3), ("drawers", 3), ("legs", true)),
            ["body", "fronts", "metal"]),
        new("table", "Table", "Tables", "TABLE", null, 1.6, 0.9, 0.75,
            [C("shape", "Shape", "rectangular", "round", "oval"), C("base", "Legs", "four", "pedestal"), L("topThickness", "Top thickness", 0.015, 0.1)],
            D(("shape", "rectangular"), ("base", "four"), ("topThickness", 0.03)),
            ["top", "metal"]),
        new("coffeeTable", "Coffee table", "Tables", "TABLE", null, 1.1, 0.6, 0.42,
            [C("shape", "Shape", "rectangular", "round", "oval"), B("shelf", "Shelf under the top")],
            D(("shape", "rectangular"), ("shelf", true)),
            ["top", "metal"]),
        new("desk", "Desk", "Tables", "DESK", null, 1.4, 0.7, 0.75,
            [C("pedestal", "Drawer pedestal", "none", "left", "right"), N("drawers", "Drawers", 1, 5)],
            D(("pedestal", "right"), ("drawers", 3)),
            ["top", "fronts", "metal"]),
        new("kitchen", "Kitchen", "Kitchen", "USERDEFINED", "Kitchen", 3.0, 0.6, 0.9,
        [
            C("layout", "Layout", KitchenLayouts.Keys),
            L("worktop", "Worktop thickness", 0.01, 0.08), L("plinth", "Plinth", 0, 0.2), L("unit", "Base unit width", 0.3, 1.2),
            In(C("corner", "Corner on the", "left", "right"), "l-shaped", "peninsula"),
            In(L("leg", "Leg length from the back wall", 1.2, 6), "l-shaped", "u-shaped", "peninsula"),
            In(L("leg2", "Second leg length", 1.2, 6), "u-shaped"),
            In(L("gap", "Aisle between the runs", 0.8, 3), "galley", "island"),
            In(L("secondRun", "Opposite run length (0: as long as the first)", 0, 12), "galley"),
            In(L("islandLength", "Island length", 0.6, 6), "island"),
            In(L("islandDepth", "Island depth", 0.6, 1.6), "island"),
            In(N("islandAt", "Island at (0–1 along)", 0, 1), "island"),
            In(L("peninsula", "Peninsula length", 0.6, 4), "peninsula"),
            In(L("overhang", "Worktop overhang for stools", 0, 0.5), "peninsula"),
            C("sinkOn", "Sink on", "run", "other"), N("sinkAt", "Sink at (0–1 along)", 0, 1),
            C("hobOn", "Hob on", "run", "other"), N("hobAt", "Hob at (0–1 along)", 0, 1), B("wallUnits", "Wall units"),
            L("wallFrom", "Wall units from", 1.2, 1.8), L("wallHeight", "Wall unit height", 0.3, 1.2), L("wallDepth", "Wall unit depth", 0.2, 0.5),
            C("tall", "Tall unit (fridge)", "none", "left", "right"),
            In(B("islandHood", "Hood over the hob there"), "island", "peninsula"),
        ],
            D(("layout", "one-wall"), ("worktop", 0.04), ("plinth", 0.1), ("unit", 0.6), ("corner", "left"), ("leg", 2.4), ("leg2", 2.4), ("gap", 1.2), ("secondRun", 0.0),
              ("islandLength", 1.8), ("islandDepth", 0.9), ("islandAt", 0.5), ("peninsula", 1.2), ("overhang", 0.3), ("sinkOn", "run"), ("sinkAt", 0.3), ("hobOn", "run"), ("hobAt", 0.72),
              ("wallUnits", true), ("wallFrom", 1.45), ("wallHeight", 0.72), ("wallDepth", 0.35), ("tall", "right"), ("islandHood", false)),
            ["body", "fronts", "top", "plinth", "handles", "metal", "glass"]),
    ];

    public static FurnitureKind? Find(string? key) => All.FirstOrDefault(k => k.Key == key);

    /// <summary>A new type's spec: the kind's size and defaults, its materials, under what was given.</summary>
    public static JsonObject NewSpec(FurnitureKind k, JsonObject? given)
    {
        var s = new JsonObject { ["kind"] = k.Key, ["width"] = k.Width, ["depth"] = k.Depth, ["height"] = k.Height };
        foreach (var (key, v) in k.Defaults) s[key] = v?.DeepClone();
        s["materials"] = new JsonObject([.. k.Roles.Select(r => new KeyValuePair<string, JsonNode?>(r, RoleDefaults[r].Material))]);
        if (given != null) Merge(s, given, k);
        return s;
    }

    /// <summary>
    /// Values given merged into a spec, each checked against its kind: lengths and numbers within their range, choices
    /// among their choices, switches true or false; null takes the default again; materials merged by role.
    /// </summary>
    public static void Merge(JsonObject spec, JsonObject given, FurnitureKind k)
    {
        foreach (var (key, v) in given)
        {
            if (key == "kind") continue;
            if (key == "materials")
            {
                var mats = spec["materials"] as JsonObject ?? [];
                if (v is JsonObject g)
                    foreach (var (role, name) in g)
                    {
                        if (!RoleDefaults.ContainsKey(role)) throw new CommandException($"„{role}” is not a material role: {string.Join(", ", RoleOrder)}.");
                        if (name is null || (string?)name is not { Length: > 0 } n) mats[role] = RoleDefaults[role].Material; else mats[role] = n.Trim();
                    }
                spec["materials"] = mats;
                continue;
            }
            // A kitchen's own units, along its main run and above it (decision 472): each checked; null or [] goes back to the even split.
            if (key is KitchenUnits.BaseKey or KitchenUnits.UpperKey)
            {
                if (k.Key != "kitchen") throw new CommandException($"A {k.Label.ToLowerInvariant()} has no „{key}”.");
                var list = KitchenUnits.Normalise(v, key == KitchenUnits.UpperKey);
                if (list.Count == 0) spec.Remove(key); else spec[key] = list;
                continue;
            }
            if (key is "width" or "depth" or "height")
            {
                var d = Json.Number(v) ?? throw new CommandException($"The {key} is a length in metres.");
                if (d < 0.1 || d > 12) throw new CommandException($"A piece of furniture's {key} is between 100 mm and 12 m.");
                spec[key] = d;
                continue;
            }
            var p = k.Params.FirstOrDefault(x => x.Key == key) ?? throw new CommandException($"A {k.Label.ToLowerInvariant()} has no „{key}”.");
            if (v == null) { spec[key] = k.Defaults[key]?.DeepClone(); continue; }
            switch (p.Kind)
            {
                case "length" or "number":
                    var n = Json.Number(v) ?? throw new CommandException($"{p.Label} is a number.");
                    if (p.Min is { } lo && n < lo - 1e-9 || p.Max is { } hi && n > hi + 1e-9)
                        throw new CommandException($"{p.Label} is between {F(p.Min)} and {F(p.Max)}.");
                    spec[key] = p.Kind == "number" && p.Max > 1 ? Math.Round(n) : n;
                    break;
                case "choice":
                    var c = (string?)v;
                    spec[key] = c != null && p.Choices!.Contains(c) ? c : throw new CommandException($"{p.Label}: one of {string.Join(", ", p.Choices!)}.");
                    break;
                case "bool":
                    spec[key] = v is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : throw new CommandException($"{p.Label} is on or off.");
                    break;
            }
        }
    }

    static string F(double? v) => v?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";

    public static JsonArray Catalog() => new([.. All.Select(k => (JsonNode)k.ToJson())]);
}

/// <summary>A type's spec read with its kind's defaults under it.</summary>
public sealed class Spec(JsonObject? o)
{
    readonly JsonObject s = o ?? [];
    public FurnitureKind Kind => FurnitureKinds.Find(s["kind"] is JsonValue k && k.TryGetValue<string>(out var key) ? key : null) ?? FurnitureKinds.All[0];
    public double W => Num("width", Kind.Width);
    public double D => Num("depth", Kind.Depth);
    public double H => Num("height", Kind.Height);
    public double Num(string key, double fallback) => Json.Number(s[key]) ?? Json.Number(Kind.Defaults[key]) ?? fallback;
    public int Int(string key, int fallback) => (int)Math.Round(Num(key, fallback));
    public string Str(string key, string fallback = "") => s[key] is JsonValue v && v.TryGetValue<string>(out var t) ? t : Kind.Defaults[key] is JsonValue d && d.TryGetValue<string>(out var dt) ? dt : fallback;
    public bool Bool(string key) => s[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : Kind.Defaults[key] is JsonValue d && d.TryGetValue<bool>(out var db) && db;
    /// <summary>Whether the spec names a material for the role itself (a kitchen made before decision 472 has none for its plinth and handles).</summary>
    public bool HasMaterial(string role) => (s["materials"] as JsonObject)?[role] is JsonValue v && v.TryGetValue<string>(out var n) && n.Length > 0;
    public JsonArray? List(string key) => s[key] as JsonArray;
    public string MaterialName(string role) => (s["materials"] as JsonObject)?[role] is JsonValue v && v.TryGetValue<string>(out var n) && n.Length > 0 ? n : FurnitureKinds.RoleDefaults[role].Material;
}
