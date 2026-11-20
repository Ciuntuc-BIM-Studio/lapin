using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis.Design;

/// <summary>
/// The design checks' own values (docs/plan-fem.md §12.6): the project's on the IfcProject (<c>analysisDesign</c>: the timber's
/// service class, the concrete cover, the bars' and stirrups' diameters, fyk, the masonry's mortar and γM), and an element's on
/// it (<c>analysisDesign</c>: the buckling length factors or lengths about each axis, the laterally unrestrained length, the
/// service class, the span/depth system), set by <c>analysis.setDesign</c> (one undo step; null back to the default).
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
    ];
    public static readonly string[] Systems = ["simple", "end", "interior", "cantilever", "flat"];

    public static double? Num(JsonObject? o, string key) => o?[key] is JsonNode n && Json.Number(n) is { } v && double.IsFinite(v) ? v : null;

    public static JsonObject? Project(IModelView m) => Spatial.Project(m)?.Attrs[Key] as JsonObject;
    public static JsonObject? Element(IModelView m, string? id) => id != null ? m.Get(id)?.Attrs[Key] as JsonObject : null;

    /// <summary>{ids?, …}: with ids, the elements' own values; without, the project's. One undo step.</summary>
    public static void Write(ICommandContext ctx, JsonObject a)
    {
        var ids = Args.Ids(a);
        var element = ids.Count > 0;
        var keys = a.Select(x => x.Key).Where(k => k != "ids" && k != "id").ToList();
        if (keys.Count == 0) throw new CommandException("Say a value: " + string.Join(", ", Numbers.Where(s => element ? s.Element : s.Project).Select(s => s.Name).Concat(element ? ["system"] : [])) + ".");
        void Apply(JsonObject own)
        {
            foreach (var k in keys)
            {
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
        ["project"] = Project(m)?.DeepClone(),
        ["defaults"] = new JsonObject
        {
            ["serviceClass"] = 1, ["cover"] = 0.030, ["bar"] = 0.016, ["stirrup"] = 0.008, ["fyk"] = 500e6, ["fvk0"] = 0.20e6, ["gammaM"] = 2.2,
        },
        ["systems"] = new JsonArray(Systems.Select(s => (JsonNode)s).ToArray()),
    };
}
