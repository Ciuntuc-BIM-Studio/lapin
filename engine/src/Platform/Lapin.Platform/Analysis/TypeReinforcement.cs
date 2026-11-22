using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Analysis;

/// <summary>
/// The stirrups a beam's or a column's type is configured with (the type's <c>stirrups</c>, decision 215), read the one way the
/// rebar module lays them and the design checks compare them: their diameter, the cover, one step throughout or zones each
/// taking a share of the length at its own step, and a column's pattern (one loop, a diamond inside, or two inside).
/// </summary>
public sealed record StirrupLayout(bool Enabled, double Diameter, double Cover, string Mode, double Step, IReadOnlyList<(double Share, double Step)> Zones, string Pattern = "single")
{
    public const string Key = "stirrups";
    public static readonly string[] Modes = ["uniform", "zones"];
    public static readonly string[] Patterns = ["single", "diamond", "cross"];

    public static StirrupLayout Default() => new(false, 0.008, 0.025, "uniform", 0.15, [(1.0 / 3, 0.1), (1.0 / 3, 0.2), (1.0 / 3, 0.1)]);

    public static StirrupLayout From(JsonObject o)
    {
        var d = Default();
        var zones = (o["zones"] as JsonArray)?.OfType<JsonObject>().Select(z => (Json.Number(z["share"]) ?? 1.0 / 3, Json.Number(z["step"]) ?? d.Step)).ToList();
        return new StirrupLayout(o["enabled"] is JsonValue ev && ev.TryGetValue<bool>(out var en) && en, Json.Number(o["diameter"]) ?? d.Diameter, Json.Number(o["cover"]) ?? d.Cover,
            (string?)o["mode"] is { } md && Modes.Contains(md) ? md : d.Mode, Json.Number(o["step"]) ?? d.Step, zones is { Count: > 0 } ? zones : d.Zones,
            (string?)o["pattern"] is { } pt && Patterns.Contains(pt) ? pt : "single");
    }

    /// <summary>How many stirrups stand at each station: one, two with a diamond, three with the two inside.</summary>
    public int LoopsPerStation => Pattern switch { "diamond" => 2, "cross" => 3, _ => 1 };

    /// <summary>The steps along a length: each zone from where it starts to where it ends at its step (its share of the length), or one step throughout.</summary>
    public List<(double From, double To, double Step)> Along(double length)
    {
        var total = Zones.Sum(z => Math.Max(0, z.Share));
        if (Mode != "zones" || total <= 1e-9) return [(0, length, Step)];
        var o = new List<(double, double, double)>();
        var at = 0.0;
        foreach (var z in Zones.Where(z => z.Share > 0))
        {
            var l = length * z.Share / total;
            o.Add((at, at + l, z.Step));
            at += l;
        }
        return o;
    }

    /// <summary>The legs' area per metre (m²/m) at a step: two legs of each loop.</summary>
    public double AswS(double step) => step > 0 ? 2 * LoopsPerStation * Math.PI * Diameter * Diameter / 4 / step : 0;
}

/// <summary>The longitudinal bars a column's type is configured with (the type's <c>bars</c>, decision 218): so many on each face, the corners shared, of one diameter.</summary>
public sealed record ColumnBarLayout(bool Enabled, int PerFace, double Diameter)
{
    public const string Key = "bars";
    public static ColumnBarLayout Default() => new(false, 3, 0.016);

    public static ColumnBarLayout From(JsonObject o)
    {
        var d = Default();
        return new ColumnBarLayout(o["enabled"] is JsonValue ev && ev.TryGetValue<bool>(out var en) && en, (int)Math.Max(2, Json.Number(o["perFace"]) ?? d.PerFace), Json.Number(o["diameter"]) ?? d.Diameter);
    }

    /// <summary>The bars round the perimeter: four faces of <see cref="PerFace"/>, the corners shared.</summary>
    public int Count => 4 * (PerFace - 1);
    public double Area => Count * Math.PI * Diameter * Diameter / 4;
}
