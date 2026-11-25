using System.Reflection;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.RoofPanels;

/// <summary>A colour a profile is made in: its code (RAL 8017), its name and how the views show it (#rrggbb; null: the covering's own colour).</summary>
public sealed record PanelColor(string Code, string Name, string? Rgb = null);

/// <summary>
/// An accessory of a profile (S3, the Panotaj Bilka app's estimate rows), data: its code, name and unit, and how it is counted.
/// <see cref="Rule"/>: <c>edges</c>, pieces of <see cref="Length"/> mm lapped <see cref="Lap"/> (the edge kind's accessory lap
/// when not given) run along each edge of the kinds in <see cref="Edges"/>, each counted as many times as its weight (a ridge
/// support on both sides), plus a short bar under every few panel joints when <see cref="JointBars"/>; <c>run</c>, pieces of
/// <see cref="Length"/> lapped <see cref="Lap"/> along the edges' whole length (weighted, with <see cref="Joints"/> × the panels'
/// useful width a joint across the slope and <see cref="Snow"/> × the snow guards' length) times <see cref="Factor"/>;
/// <c>perLength</c>, that length times <see cref="Factor"/> in whole pieces of <see cref="Length"/>, unlapped; <c>count</c>,
/// a number worked out on the roof (<see cref="Of"/>: hipCaps, gableCaps, ridgeNodes, valleyClips, windows).
/// <see cref="Option"/>: only when the roof's ridge option (the profile's <see cref="PanelProfile.Ridges"/>) is this one.
/// </summary>
public sealed record AccessoryItem(string Code, string Name, string Unit, string Rule, IReadOnlyDictionary<string, double> Edges,
    int Length, int? Lap, double Factor, string? Of, string? Option, bool JointBars, double Joints, double Snow, string Note);

/// <summary>
/// A way to carry the panels (S3): <c>battens</c> across the slope (the first at <see cref="First"/> mm up from the eave, the
/// second at <see cref="Second"/> when given, then every <see cref="Spacing"/>; <see cref="SteepSpacing"/> on a slope steeper
/// than <see cref="SteepFrom"/>°) or <c>boards</c> (a standing seam's, <see cref="Spacing"/> apart centre to centre), each
/// <see cref="Section"/> in mm, bought in <see cref="Bar"/> mm lengths with <see cref="Reserve"/> to spare.
/// </summary>
public sealed record SupportScheme(string Id, string Name, string Kind, string Code, string Section, int First, int? Second,
    int Spacing, int? SteepSpacing, double SteepFrom, int Bar, double Reserve, string Note)
{
    /// <summary>How far apart the battens or boards are on a slope of <paramref name="pitch"/> degrees, metres.</summary>
    public double SpacingAt(double pitch) => (SteepSpacing is { } s && pitch > SteepFrom ? s : Spacing) / 1000.0;
}

/// <summary>
/// A fixing of the sheet (S3), data with a coded rule (the app's): <c>stitches</c>, a short screw where each side lap crosses a
/// module; <c>tileFixings</c>, <see cref="PerM2"/> a m² of cover, one each <see cref="EdgeSpacing"/> mm of eave (and one more an
/// eave), two a wave along ridges, hips and snow guards, two a valley batten, two each <see cref="EdgeSpacing"/> along verges,
/// top edges, abutments and breaks; <c>seamClips</c>, a standing seam's flat-headed screw on each board a panel crosses, at the
/// eaves and along the ridges, hips, top edges, valleys and breaks, and three a joint; <c>seamFlashings</c>, the flashings'
/// screws on a standing seam. Counted with <see cref="Reserve"/> to spare, rounded up to <see cref="RoundTo"/>, in packs of <see cref="Pack"/>.
/// </summary>
public sealed record FixingItem(string Code, string Name, string Unit, string Rule, int Pack, int RoundTo, double PerM2, int EdgeSpacing, double Reserve, string Note);

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
    /// <summary>Its accessories (S3), in the order a takeoff lists them.</summary>
    public IReadOnlyList<AccessoryItem> Accessories { get; init; } = [];
    /// <summary>The ways it can be carried, the first the default (Retro Panel: boards, or the older guide's battens).</summary>
    public IReadOnlyList<SupportScheme> Support { get; init; } = [];
    /// <summary>The sheet's fixings.</summary>
    public IReadOnlyList<FixingItem> Fixings { get; init; } = [];
    /// <summary>The ridge options its accessories come in (a tile's large and small ridge caps), the first the default; none for one.</summary>
    public IReadOnlyList<string> Ridges { get; init; } = [];

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
            [.. ((o["colors"] as JsonArray) ?? []).OfType<JsonObject>().Select(c => new PanelColor((string?)c["code"] ?? "", (string?)c["name"] ?? "", (string?)c["rgb"] is { Length: 7 } rgb && rgb[0] == '#' ? rgb : null))],
            Json.Number(o["minPitch"]) ?? 8, Json.Number(o["minPitchHipValley"]) ?? Json.Number(o["minPitch"]) ?? 8,
            (bool?)o["reuseOffcuts"] ?? false, I(o["valleyGap"], 65), I(o["waves"]), Json.Number(o["waveHeight"]) ?? 20,
            I(o["accessoryLength"], 2000), laps, (string?)o["notes"] ?? "", (string?)o["source"] ?? "")
        {
            Accessories = [.. ((o["accessories"] as JsonArray) ?? []).OfType<JsonObject>().Select(ReadAccessory)],
            Support = [.. ((o["support"] as JsonArray) ?? []).OfType<JsonObject>().Select(ReadSupport)],
            Fixings = [.. ((o["fixings"] as JsonArray) ?? []).OfType<JsonObject>().Select(ReadFixing)],
            Ridges = [.. ((o["ridges"] as JsonArray) ?? []).Select(n => (string?)n).OfType<string>()],
        };
    }

    static AccessoryItem ReadAccessory(JsonObject a)
    {
        var edges = new Dictionary<string, double>();
        switch (a["edges"])
        {
            case JsonArray arr: foreach (var k in arr.Select(n => (string?)n).OfType<string>()) edges[k] = 1; break;
            case JsonObject obj: foreach (var (k, v) in obj) edges[k] = Json.Number(v) ?? 1; break;
        }
        var code = (string?)a["code"] ?? "";
        return new AccessoryItem(code, (string?)a["name"] ?? code, (string?)a["unit"] ?? "piece", (string?)a["rule"] ?? "edges", edges,
            I(a["length"], 2000), Json.Number(a["lap"]) is { } lap ? (int)lap : null, Json.Number(a["factor"]) ?? 1, (string?)a["of"],
            (string?)a["option"], (bool?)a["jointBars"] ?? false, Json.Number(a["joints"]) ?? 0, Json.Number(a["snow"]) ?? 0, (string?)a["note"] ?? "");
    }

    static SupportScheme ReadSupport(JsonObject a)
    {
        var id = (string?)a["id"] ?? "battens";
        return new SupportScheme(id, (string?)a["name"] ?? id, (string?)a["kind"] ?? "battens", (string?)a["code"] ?? id, (string?)a["section"] ?? "",
            I(a["first"]), Json.Number(a["second"]) is { } s ? (int)s : null, I(a["spacing"], 350), Json.Number(a["steepSpacing"]) is { } ss ? (int)ss : null,
            Json.Number(a["steepFrom"]) ?? 90, I(a["bar"], 3000), Json.Number(a["reserve"]) ?? 1.05, (string?)a["note"] ?? "");
    }

    static FixingItem ReadFixing(JsonObject a)
    {
        var code = (string?)a["code"] ?? "";
        return new FixingItem(code, (string?)a["name"] ?? code, (string?)a["unit"] ?? "piece", (string?)a["rule"] ?? "", I(a["pack"], 1), I(a["roundTo"], 1),
            Json.Number(a["perM2"]) ?? 0, I(a["edgeSpacing"], 250), Json.Number(a["reserve"]) ?? 1.1, (string?)a["note"] ?? "");
    }

    public JsonObject ToJson() => new()
    {
        ["id"] = Id, ["name"] = Name, ["maker"] = Maker, ["kind"] = Kind,
        ["grossWidth"] = GrossWidth, ["usefulWidth"] = UsefulWidth, ["slopeLap"] = SlopeLap, ["module"] = Module,
        ["joints"] = Joints, ["stagger"] = Stagger, ["endFold"] = EndFold, ["onOrder"] = OnOrder,
        ["minLength"] = Lengths[0], ["maxLength"] = Lengths[^1],
        ["lengths"] = OnOrder ? null : new JsonArray([.. Lengths.Select(n => (JsonNode)n)]),
        ["maxLengths"] = new JsonArray([.. MaxLengths.Select(n => (JsonNode)n)]),
        ["colors"] = new JsonArray([.. Colors.Select(c => (JsonNode)new JsonObject { ["code"] = c.Code, ["name"] = c.Name, ["rgb"] = c.Rgb })]),
        ["minPitch"] = MinPitch, ["minPitchHipValley"] = MinPitchHipValley, ["reuseOffcuts"] = ReuseOffcuts, ["valleyGap"] = ValleyGap,
        ["waves"] = Waves, ["waveHeight"] = WaveHeight, ["notes"] = Notes, ["source"] = Source,
        ["ridges"] = new JsonArray([.. Ridges.Select(r => (JsonNode)r)]),
        ["support"] = new JsonArray([.. Support.Select(s => (JsonNode)new JsonObject { ["id"] = s.Id, ["name"] = s.Name, ["kind"] = s.Kind, ["section"] = s.Section })]),
        ["accessories"] = new JsonArray([.. Accessories.Select(a => (JsonNode)new JsonObject { ["code"] = a.Code, ["name"] = a.Name, ["unit"] = a.Unit, ["option"] = a.Option })]),
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

    /// <summary>What every profile's takeoff shares (S3): the membrane, its tape, the counter-battens and their fixings (profiles.json's <c>common</c>).</summary>
    public static Common Shared => SharedData.Value;

    static readonly Lazy<Common> SharedData = new(() => Common.Read(Data.Value.Raw["common"] as JsonObject ?? []));

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

/// <summary>
/// The parts of a takeoff every profile shares (profiles.json's <c>common</c>): the membrane (<see cref="MembraneWidth"/> mm wide
/// strips lapped <see cref="MembraneLap"/>, <see cref="MembraneRoll"/> m² rolls, <see cref="MembraneReserve"/> for cuts), its
/// tape on the strips' laps, the counter-battens (<see cref="CounterBatten"/>, nailed every <see cref="CounterFixing"/> mm) and
/// the fixings of battens and counter-battens to the rafters, nails or screws (<see cref="WoodFixings"/>, the first the default).
/// </summary>
public sealed record Common(string MembraneCode, string MembraneName, string MembraneUnit, int MembraneWidth, int MembraneLap, double MembraneRoll, double MembraneReserve,
    string TapeCode, string TapeName, string TapeUnit, double TapeRoll, SupportScheme CounterBatten, int CounterFixing, double FixingReserve,
    IReadOnlyList<(string Id, string Code, string Name, string Unit, int Pack)> WoodFixings)
{
    static int I(JsonNode? n, int fallback) => Json.Number(n) is { } v ? (int)Math.Round(v) : fallback;

    public static Common Read(JsonObject o)
    {
        var m = o["membrane"] as JsonObject ?? [];
        var t = o["membraneTape"] as JsonObject ?? [];
        var c = o["counterBatten"] as JsonObject ?? [];
        var wood = ((o["woodFixings"] as JsonArray) ?? []).OfType<JsonObject>()
            .Select(w => ((string?)w["id"] ?? "", (string?)w["code"] ?? "", (string?)w["name"] ?? "", (string?)w["unit"] ?? "piece", I(w["pack"], 1))).ToList();
        return new Common((string?)m["code"] ?? "membrane", (string?)m["name"] ?? "Roof membrane", (string?)m["unit"] ?? "roll", I(m["width"], 1500), I(m["lap"], 100),
            Json.Number(m["roll"]) ?? 75, Json.Number(m["reserve"]) ?? 1.08,
            (string?)t["code"] ?? "membrane-tape", (string?)t["name"] ?? "Membrane tape", (string?)t["unit"] ?? "roll", Json.Number(t["roll"]) ?? 25,
            new SupportScheme("counter", (string?)c["name"] ?? "Counter-batten", "counter", (string?)c["code"] ?? "counter-batten", (string?)c["section"] ?? "50x30",
                0, null, 0, null, 90, I(c["bar"], 3000), Json.Number(c["reserve"]) ?? 1.05, (string?)c["note"] ?? ""),
            I(o["counterBattenFixing"], 600), Json.Number(o["fixingReserve"]) ?? 1.1, wood);
    }
}
