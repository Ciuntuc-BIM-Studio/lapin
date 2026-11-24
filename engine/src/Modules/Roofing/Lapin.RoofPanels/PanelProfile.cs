using System.Reflection;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.RoofPanels;

/// <summary>A colour a profile is made in: its code (RAL 8017) and its name.</summary>
public sealed record PanelColor(string Code, string Name);

/// <summary>
/// A roof panel profile (docs/plan-roof-panels.md §3), data and never code: millimetres throughout, as makers publish them.
/// <see cref="Lengths"/> are the lengths a panel can be bought in, ascending: a stock profile's few, a profile made to order
/// every length its order range gives (<see cref="OnOrder"/>). <see cref="Joints"/> says how a column is made of several
/// panels: <c>module</c>, every joint on the profile's module (a tile's step, the panel's length less the lap a whole number of
/// modules); <c>staggered</c>, the longest panels with the joints of every other column <see cref="Stagger"/> lower (a standing
/// seam). <see cref="EndFold"/>: what a panel's top end loses folded. <see cref="ValleyGap"/>: how far the panels stop short
/// of a valley's line. <see cref="AccessoryLaps"/>: each edge kind's accessory lap (flashings <see cref="AccessoryLength"/> long).
/// </summary>
public sealed record PanelProfile(
    string Id, string Name, string Maker, string Kind,
    int GrossWidth, int UsefulWidth, int SlopeLap, int Module, string Joints, int Stagger, int EndFold,
    IReadOnlyList<int> Lengths, bool OnOrder, IReadOnlyList<int> MaxLengths,
    IReadOnlyList<PanelColor> Colors, double MinPitch, double MinPitchHipValley, bool ReuseOffcuts, int ValleyGap,
    int Waves, double WaveHeight, int AccessoryLength, IReadOnlyDictionary<string, int> AccessoryLaps, string Notes, string Source)
{
    public double Gross => GrossWidth / 1000.0;
    public double Width => UsefulWidth / 1000.0;
    public double Lap => SlopeLap / 1000.0;
    public double Pitch => Module / 1000.0;
    public bool Staggered => Joints == "staggered";

    /// <summary>The length a panel of nominal <paramref name="nominal"/> mm covers along the slope, metres: less its end fold.</summary>
    public double EffectiveLength(int nominal) => (nominal - EndFold) / 1000.0;

    /// <summary>The accessory lap of an edge kind, metres (0.1 when the profile does not say).</summary>
    public double AccessoryLap(string kind) => (AccessoryLaps.TryGetValue(kind, out var v) ? v : 100) / 1000.0;

    /// <summary>
    /// The profile with its longest panel capped at <paramref name="max"/> mm (one of <see cref="MaxLengths"/>, a made-to-order
    /// profile's choice: Retro Panel at 4000 mm, joints on long slopes as the guide advises); itself for null.
    /// </summary>
    public PanelProfile WithMaxLength(int? max)
    {
        if (max is not { } cap || cap == Lengths[^1]) return this;
        if (!OnOrder || !MaxLengths.Contains(cap)) throw new ArgumentException($"{Name} takes a longest panel of {string.Join(" or ", MaxLengths)} mm.");
        return this with { Lengths = [.. Lengths.Where(n => n < cap), cap] };
    }

    /// <summary>What is wrong with the profile as data, or null.</summary>
    public string? Problem()
    {
        if (Id.Length == 0) return "A profile needs an id.";
        if (UsefulWidth <= 0 || GrossWidth < UsefulWidth) return "A profile's useful width is positive and no more than its whole width.";
        if (Module <= 0) return "A profile's module is positive.";
        if (SlopeLap < 0) return "A profile's slope lap is not negative.";
        if (Lengths.Count == 0 || Lengths.Any(n => n <= SlopeLap + EndFold)) return "A profile needs lengths longer than its lap and end fold.";
        if (Joints is not ("module" or "staggered")) return "A profile's joints are \"module\" or \"staggered\".";
        return null;
    }

    static int I(JsonNode? n, int fallback = 0) => Json.Number(n) is { } v ? (int)Math.Round(v) : fallback;

    /// <summary>
    /// The lengths of an order range: its least, then from <c>start</c> every <c>step</c> below its most, and its most
    /// (Clasic 35: 500, 850 … 6800, 7000; Retro Panel: 470, 500, 600 … 7100, 7130).
    /// </summary>
    public static List<int> OrderLengths(int min, int start, int step, int max)
    {
        var o = new SortedSet<int> { min, max };
        if (step > 0) for (var n = start; n < max; n += step) if (n >= min) o.Add(n);
        return [.. o];
    }

    public static PanelProfile Read(JsonObject o)
    {
        var onOrder = o["order"] is JsonObject;
        List<int> lengths;
        if (o["order"] is JsonObject r)
            lengths = OrderLengths(I(r["min"]), I(r["start"], I(r["min"])), I(r["step"]), I(r["max"]));
        else
            lengths = [.. ((o["lengths"] as JsonArray) ?? []).Select(n => I(n)).Where(n => n > 0).Distinct().Order()];
        var laps = new Dictionary<string, int>();
        if (o["accessoryLaps"] is JsonObject al) foreach (var (k, v) in al) laps[k] = I(v);
        var id = (string?)o["id"] ?? "";
        return new PanelProfile(
            id, (string?)o["name"] ?? id, (string?)o["maker"] ?? "", (string?)o["kind"] ?? "tile",
            I(o["grossWidth"]), I(o["usefulWidth"]), I(o["slopeLap"]), I(o["module"], 1), (string?)o["joints"] ?? "module",
            I(o["stagger"]), I(o["endFold"]),
            lengths, onOrder, [.. ((o["maxLengths"] as JsonArray) ?? []).Select(n => I(n))],
            [.. ((o["colors"] as JsonArray) ?? []).OfType<JsonObject>().Select(c => new PanelColor((string?)c["code"] ?? "", (string?)c["name"] ?? ""))],
            Json.Number(o["minPitch"]) ?? 8, Json.Number(o["minPitchHipValley"]) ?? Json.Number(o["minPitch"]) ?? 8,
            (bool?)o["reuseOffcuts"] ?? false, I(o["valleyGap"], 65), I(o["waves"]), Json.Number(o["waveHeight"]) ?? 20,
            I(o["accessoryLength"], 2000), laps, (string?)o["notes"] ?? "", (string?)o["source"] ?? "");
    }

    public JsonObject ToJson() => new()
    {
        ["id"] = Id, ["name"] = Name, ["maker"] = Maker, ["kind"] = Kind,
        ["grossWidth"] = GrossWidth, ["usefulWidth"] = UsefulWidth, ["slopeLap"] = SlopeLap, ["module"] = Module,
        ["joints"] = Joints, ["stagger"] = Stagger, ["endFold"] = EndFold, ["onOrder"] = OnOrder,
        ["minLength"] = Lengths[0], ["maxLength"] = Lengths[^1],
        ["lengths"] = OnOrder ? null : new JsonArray([.. Lengths.Select(n => (JsonNode)n)]),
        ["maxLengths"] = new JsonArray([.. MaxLengths.Select(n => (JsonNode)n)]),
        ["colors"] = new JsonArray([.. Colors.Select(c => (JsonNode)new JsonObject { ["code"] = c.Code, ["name"] = c.Name })]),
        ["minPitch"] = MinPitch, ["minPitchHipValley"] = MinPitchHipValley, ["reuseOffcuts"] = ReuseOffcuts, ["valleyGap"] = ValleyGap,
        ["notes"] = Notes, ["source"] = Source,
    };
}

/// <summary>The built-in profiles (profiles.json, embedded): indicative data, the user's own profiles come beside them.</summary>
public static class PanelProfiles
{
    static readonly Lazy<(IReadOnlyList<PanelProfile> All, JsonObject Raw)> Data = new(() =>
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Lapin.RoofPanels.profiles.json")
            ?? throw new InvalidOperationException("profiles.json is not embedded.");
        var raw = (JsonObject)JsonNode.Parse(s)!;
        return ([.. ((JsonArray)raw["profiles"]!).OfType<JsonObject>().Select(PanelProfile.Read)], raw);
    });

    public static IReadOnlyList<PanelProfile> BuiltIn => Data.Value.All;

    public static PanelProfile? Find(string? id) => BuiltIn.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>A profile as a command or query gives it: a built-in's id, or a profile of its own (an object, the catalogue's format).</summary>
    public static PanelProfile Resolve(JsonNode? n)
    {
        var p = n switch
        {
            JsonObject o => PanelProfile.Read(o),
            JsonValue v when (string?)v is { } id => Find(id) ?? throw new ArgumentException($"There is no roof panel profile \"{id}\"."),
            _ => throw new ArgumentException("Name a roof panel profile."),
        };
        return p.Problem() is { } problem ? throw new ArgumentException(problem) : p;
    }

    public static JsonArray Catalog() => new([.. BuiltIn.Select(p => (JsonNode)p.ToJson())]);
}
