using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis;

/// <summary>
/// What a support says of its stiffness (docs/plan-fem.md §14.2), the attribute <c>analysisSupport</c> on a footing, a steel
/// column's base or a slab on the ground: springs on its six degrees of freedom (global; kN/m, kN·m/rad; null: as the support
/// holds it; 0: free), Winkler's modulus of subgrade reaction ks (kN/m³: the footing's footprint, the slab's area), and whether
/// it works in compression only (§14.4: it lifts when it would pull). Absent: rigid, as before S5.
/// </summary>
public sealed record SupportSettings(double?[]? Springs, double? Winkler, bool CompressionOnly)
{
    public const string Key = "analysisSupport";
    public const double MaxStiffness = 1e12;
    /// <summary>The classes a support's springs go on: footings, slabs on the ground, steel column bases (the steel module's node, by its class name).</summary>
    public static readonly string[] Classes = [IfcClass.Footing, IfcClass.Slab, "LapinSteelNode"];

    public bool Any => Springs != null || Winkler != null || CompressionOnly;

    public static SupportSettings? Of(Entity? e)
    {
        if (e?.Attrs[Key] is not JsonObject o) return null;
        double?[]? springs = null;
        if (o["springs"] is JsonArray a && a.Count == 6)
            springs = a.Select(x => Json.Number(x) is { } v && double.IsFinite(v) && v >= 0 ? v : (double?)null).ToArray();
        var ks = Json.Number(o["winkler"]) is { } k && double.IsFinite(k) && k > 0 ? k : (double?)null;
        var only = o["compressionOnly"] is JsonValue c && c.TryGetValue<bool>(out var b) && b;
        var s = new SupportSettings(springs, ks, only);
        return s.Any ? s : null;
    }

    public JsonObject ToJson() => new()
    {
        ["springs"] = Springs == null ? null : new JsonArray(Springs.Select(x => (JsonNode?)(x is { } v ? JsonValue.Create(v) : null)).ToArray()),
        ["winkler"] = Winkler, ["compressionOnly"] = CompressionOnly,
    };

    /// <summary>Typical moduli of subgrade reaction (kN/m³), indicative only (Bowles, "Foundation Analysis and Design", 5th ed., Table 9-1): ks is the geotechnical engineer's.</summary>
    public static JsonArray Typical() => new(
        new[] { ("loose sand", 4800, 16000), ("medium dense sand", 9600, 80000), ("dense sand", 64000, 128000), ("clayey medium dense sand", 32000, 80000),
                ("silty medium dense sand", 24000, 48000), ("clay, qu ≤ 200 kPa", 12000, 24000), ("clay, 200 < qu ≤ 800 kPa", 24000, 48000), ("clay, qu > 800 kPa", 48000, 96000) }
            .Select(x => (JsonNode)new JsonObject { ["soil"] = x.Item1, ["min"] = x.Item2, ["max"] = x.Item3 }).ToArray());

    /// <summary>{ids, springs?: [6 × (number ≥ 0 | null)] | null, winkler?: ks | null, compressionOnly?: bool | null}: null takes one off, absent leaves it. One undo step.</summary>
    public static void Write(ICommandContext ctx, JsonObject a)
    {
        var ids = Args.Ids(a);
        if (ids.Count == 0) throw new CommandException("Select the footings, column bases or slabs on the ground.");
        if (!a.ContainsKey("springs") && !a.ContainsKey("winkler") && !a.ContainsKey("compressionOnly"))
            throw new CommandException("Say the springs, the modulus of subgrade reaction (winkler), compressionOnly or some of them.");
        JsonArray? springs = null;
        if (a["springs"] is JsonArray sa)
        {
            if (sa.Count != 6) throw new CommandException("The springs are six stiffnesses [kx, ky, kz, krx, kry, krz] (kN/m, kN·m/rad; null: as the support holds it, 0: free).");
            springs = [];
            foreach (var x in sa)
            {
                if (x is null) { springs.Add((JsonNode?)null); continue; }
                if (Json.Number(x) is not { } v || !double.IsFinite(v) || v < 0 || v > MaxStiffness) throw new CommandException("A spring's stiffness is a number from 0 up (kN/m, kN·m/rad).");
                springs.Add((JsonNode)JsonValue.Create(v));
            }
        }
        else if (a.ContainsKey("springs") && a["springs"] is not null) throw new CommandException("The springs are six stiffnesses [kx, ky, kz, krx, kry, krz], or null.");
        double? ks = null;
        if (a["winkler"] is { } w)
        {
            if (Json.Number(w) is not { } v || !double.IsFinite(v) || v <= 0 || v > MaxStiffness) throw new CommandException("The modulus of subgrade reaction ks is a number of kN/m³ above 0.");
            ks = v;
        }
        bool? only = null;
        if (a["compressionOnly"] is { } c)
        {
            if (c is not JsonValue cv || !cv.TryGetValue<bool>(out var b)) throw new CommandException("compressionOnly is true, false or null.");
            only = b;
        }
        foreach (var id in ids)
        {
            var e = ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
            if (!Classes.Contains(e.Class)) throw new CommandException($"„{e.Name}” is no footing, column base or slab: springs are a support's.");
            if (e.Class == IfcClass.Slab && a.ContainsKey("springs") && a["springs"] is not null) throw new CommandException("A slab on the ground takes Winkler's ks (winkler), not springs.");
            if (e.Class == "LapinSteelNode" && ks != null) throw new CommandException("A column base takes springs, not Winkler's ks: it has no footprint.");
            var attrs = e.CloneAttrs();
            var own = attrs[Key] is JsonObject had ? (JsonObject)had.DeepClone() : new JsonObject();
            if (a.ContainsKey("springs")) { if (springs == null) own.Remove("springs"); else own["springs"] = springs.DeepClone(); }
            if (a.ContainsKey("winkler")) { if (ks == null) own.Remove("winkler"); else own["winkler"] = ks; }
            if (a.ContainsKey("compressionOnly")) { if (only != true) own.Remove("compressionOnly"); else own["compressionOnly"] = true; }
            attrs.Remove(Key);
            if (own.Count > 0) attrs[Key] = own;
            ctx.Replace(e.With(attrs: attrs));
        }
    }
}

/// <summary>A bar that works one way only (§14.4): the attribute <c>analysisOnly</c>, "tension" (a brace, a tie) or "compression" (a strut).</summary>
public static class OneWay
{
    public const string Key = "analysisOnly";
    public static readonly string[] Choices = ["tension", "compression"];

    public static string? Of(Entity? e) => e?.Attrs[Key] is JsonValue v && v.TryGetValue<string>(out var s) && Choices.Contains(s) ? s : null;
}

/// <summary>A bar its sheeting braces (§14.15): the attribute <c>analysisSheeting</c> (true): held straight across in the sheeting's plane.</summary>
public static class SheetingSettings
{
    public const string Key = "analysisSheeting";

    public static bool Of(Entity? e) => e?.Attrs[Key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
}
