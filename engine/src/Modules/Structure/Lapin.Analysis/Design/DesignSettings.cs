using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis.Design;

/// <summary>
/// The design checks' own values (docs/plan-fem.md §12.6): the project's on the IfcProject (<c>analysisDesign</c>: the timber's
/// service class, the concrete cover, the bars' and stirrups' diameters, fyk, γc, γs, αcc, the steel's γM0, γM1, γM2, the masonry's
/// mortar and γM, its seismic γM, the timber's deflection limits), and an element's on it (<c>analysisDesign</c>: the buckling length
/// factors or lengths about each axis, the laterally unrestrained length, the service class, the span/depth system, the timber's
/// deflection limits), set by <c>analysis.setDesign</c> (one undo step; null back to the default).
/// </summary>
public static class DesignSettings
{
    public const string Key = "analysisDesign";

    /// <summary>Each value a key takes: its range, and whether it is the project's, an element's, or both.</summary>
    sealed record Spec(string Name, double Min, double Max, bool Project, bool Element, string What);

    static readonly Spec[] Numbers =
    [
        new("ky", 0.3, 10, false, true, "the buckling length factor about the strong axis"),
        new("kz", 0.3, 10, false, true, "the buckling length factor about the weak axis"),
        new("lcrY", 0.01, 200, false, true, "the buckling length about the strong axis (m)"),
        new("lcrZ", 0.01, 200, false, true, "the buckling length about the weak axis (m)"),
        new("llt", 0, 200, false, true, "the laterally unrestrained length (m; 0: restrained)"),
        new("serviceClass", 1, 3, true, true, "the timber's service class (1, 2 or 3)"),
        new("cover", 0.005, 0.1, true, false, "the concrete cover to the stirrups (m)"),
        new("bar", 0.006, 0.05, true, false, "the main bars' diameter (m)"),
        new("stirrup", 0.004, 0.02, true, false, "the stirrups' diameter (m)"),
        new("fyk", 200e6, 700e6, true, false, "the reinforcement's characteristic yield strength (Pa)"),
        new("fvk0", 0.01e6, 1e6, true, false, "the masonry's initial shear strength fvk0 (Pa)"),
        new("gammaM", 1, 4, true, false, "the masonry's partial factor γM"),
        new("gammaMSeismic", 1, 4, true, false, "the masonry's γM in the seismic design situation where ag ≤ 0.12 g (CR 6 2.4.2.3.1(2))"),
        new("gammaC", 1, 2, true, false, "the concrete's partial factor γc"),
        new("gammaS", 1, 1.5, true, false, "the reinforcement's partial factor γs"),
        new("alphaCc", 0.8, 1, true, false, "the concrete's long-term coefficient αcc"),
        new("gammaM0", 1, 1.5, true, false, "the steel's partial factor γM0 (cross-sections)"),
        new("gammaM1", 1, 1.5, true, false, "the steel's partial factor γM1 (members' buckling)"),
        new("gammaM2", 1, 1.5, true, false, "the steel's partial factor γM2 (net sections, connections)"),
        new("timberInst", 100, 1000, true, true, "the timber's instantaneous deflection limit, the span over it"),
        new("timberFin", 100, 1000, true, true, "the timber's final deflection limit, the span over it"),
    ];
    public static readonly string[] Systems = ["simple", "end", "interior", "cantilever", "flat"];
    /// <summary>The steel members' interaction factors (6.3.3): EN 1993-1-1 Annex A (method 1, the Romanian annex's, the default) or Annex B (method 2).</summary>
    public static readonly string[] SteelMethods = ["A", "B"];
    /// <summary>The snow's load duration for timber's kmod (EN 1995-1-1 Table 2.2: medium or short term, as the site's climate says; short by default).</summary>
    public static readonly string[] SnowDurations = ["short", "medium"];

    /// <summary>A project's choice by name, or its default.</summary>
    public static string Choice(JsonObject? project, string key, string[] choices) => (string?)project?[key] is { } v && choices.Contains(v) ? v : choices[0];

    /// <summary>The project's choices: a key, its choices (the first the default).</summary>
    static readonly (string Key, string[] Choices, string What)[] ProjectChoices =
    [
        ("steelInteraction", SteelMethods, "The steel interaction method is A (EN 1993-1-1 Annex A, method 1) or B (Annex B, method 2)."),
        ("snowDuration", SnowDurations, "The snow's load duration for timber is short or medium."),
    ];

    public static double? Num(JsonObject? o, string key) => o?[key] is JsonNode n && Json.Number(n) is { } v && double.IsFinite(v) ? v : null;

    public static JsonObject? Project(IModelView m) => Spatial.Project(m)?.Attrs[Key] as JsonObject;
    public static JsonObject? Element(IModelView m, string? id) => id != null ? m.Get(id)?.Attrs[Key] as JsonObject : null;

    /// <summary>{ids?, …}: with ids, the elements' own values; without, the project's. One undo step.</summary>
    public static void Write(ICommandContext ctx, JsonObject a)
    {
        var ids = Args.Ids(a);
        var element = ids.Count > 0;
        var keys = a.Select(x => x.Key).Where(k => k != "ids" && k != "id").ToList();
        if (keys.Count == 0) throw new CommandException("Say a value: " + string.Join(", ", Numbers.Where(s => element ? s.Element : s.Project).Select(s => s.Name).Concat(element ? ["system"] : ProjectChoices.Select(x => x.Key))) + ".");
        void Apply(JsonObject own)
        {
            foreach (var k in keys)
            {
                if (!element && ProjectChoices.FirstOrDefault(x => x.Key == k) is { Key: not null } pc)
                {
                    if (a[k] is null) own.Remove(k);
                    else if ((string?)a[k] is { } s && pc.Choices.Contains(s)) own[k] = s;
                    else throw new CommandException(pc.What);
                    continue;
                }
                if (k == "system" && element)
                {
                    if (a[k] is null) own.Remove(k);
                    else if ((string?)a[k] is { } s && Systems.Contains(s)) own[k] = s;
                    else throw new CommandException($"The system is one of {string.Join(", ", Systems)}.");
                    continue;
                }
                var spec = Numbers.FirstOrDefault(s => s.Name == k && (element ? s.Element : s.Project))
                    ?? throw new CommandException($"„{k}” is not a design value of {(element ? "an element" : "the project")}.");
                if (a[k] is null) { own.Remove(k); continue; }
                if (Json.Number(a[k]) is not { } v || !double.IsFinite(v) || v < spec.Min || v > spec.Max)
                    throw new CommandException($"{char.ToUpperInvariant(spec.What[0])}{spec.What[1..]} is a number from {spec.Min.ToString(CultureInfo.InvariantCulture)} to {spec.Max.ToString(CultureInfo.InvariantCulture)}.");
                if (k == "serviceClass" && v != Math.Round(v)) throw new CommandException("The service class is 1, 2 or 3.");
                own[k] = v;
            }
        }
        if (!element)
        {
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            ctx.Replace(Written(project, Apply));
            return;
        }
        foreach (var id in ids)
        {
            var e = ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
            if (UserLoads.Classes.Contains(e.Class) || e.Class == LoadCases.Class) throw new CommandException("Design values are a structural element's.");
            ctx.Replace(Written(e, Apply));
        }
    }

    static Entity Written(Entity e, Action<JsonObject> apply)
    {
        var attrs = e.CloneAttrs();
        var own = attrs[Key] is JsonObject had ? (JsonObject)had.DeepClone() : new JsonObject();
        apply(own);
        attrs.Remove(Key);
        if (own.Count > 0) attrs[Key] = own;
        return e.With(attrs: attrs);
    }

    public static JsonObject ToJson(IModelView m) => new()
    {
        ["project"] = Project(m)?.DeepClone(), ["indicative"] = true,
        ["defaults"] = new JsonObject
        {
            ["serviceClass"] = 1, ["cover"] = 0.030, ["bar"] = 0.016, ["stirrup"] = 0.008, ["fyk"] = 500e6, ["fvk0"] = 0.20e6, ["gammaM"] = 2.2, ["steelInteraction"] = "A", ["snowDuration"] = "short",
            ["gammaMSeismic"] = MasonryDesign.GammaMSeismic, ["gammaC"] = 1.5, ["gammaS"] = 1.15, ["alphaCc"] = 1.0,
            ["gammaM0"] = SteelDesign.GammaM0, ["gammaM1"] = SteelDesign.GammaM1, ["gammaM2"] = SteelDesign.GammaM2,
            ["timberInst"] = TimberDesign.InstLimit, ["timberFin"] = TimberDesign.FinLimit,
        },
        ["systems"] = new JsonArray(Systems.Select(s => (JsonNode)s).ToArray()),
        ["steelInteraction"] = new JsonArray(SteelMethods.Select(s => (JsonNode)s).ToArray()),
        ["snowDuration"] = new JsonArray(SnowDurations.Select(s => (JsonNode)s).ToArray()),
    };
}
