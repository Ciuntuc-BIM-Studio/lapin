using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Graphs;

/// <summary>A polyline in space (a line is one of two points): what the geometry nodes make, drawn in the ghost.</summary>
public sealed record Polyline(IReadOnlyList<Vec3> Points, bool Closed = false);

/// <summary>
/// The values a wire carries (docs/plan-visual-graphs.md §3): a number (double), a text (string, an element's id too), a
/// bool, a point (<see cref="Vec3"/>), a polyline, a list (<see cref="List{T}"/> of values, a list of lists too), or JSON
/// (what a command or a query returned). Null is "nothing" (an input left out, an item that failed).
/// </summary>
public static class Values
{
    /// <summary>A value as a command's argument: points as [x, y] (or [x, y, z] off the level), polylines as their points.</summary>
    public static JsonNode? ToArg(object? v) => v switch
    {
        null => null,
        double d => JsonValue.Create(d),
        int i => JsonValue.Create(i),
        bool b => JsonValue.Create(b),
        string s => JsonValue.Create(s),
        Vec3 p => p.Z == 0 ? new JsonArray(p.X, p.Y) : new JsonArray(p.X, p.Y, p.Z),
        Polyline l => new JsonArray(l.Points.Select(p => ToArg(p)).ToArray()),
        List<object?> list => new JsonArray(list.Select(ToArg).ToArray()),
        JsonNode n => n.DeepClone(),
        _ => JsonValue.Create(v.ToString()),
    };

    /// <summary>A value as the interface shows it: points as {$pt}, polylines as {$line}, lists cut at <paramref name="most"/> items (with {$more}).</summary>
    public static JsonNode? ToDisplay(object? v, int most = 100) => v switch
    {
        Vec3 p => new JsonObject { ["$pt"] = new JsonArray(R(p.X), R(p.Y), R(p.Z)) },
        Polyline l => new JsonObject { ["$line"] = new JsonArray(l.Points.Take(most).Select(p => (JsonNode)new JsonArray(R(p.X), R(p.Y), R(p.Z))).ToArray()), ["closed"] = l.Closed },
        List<object?> list => Cut(list, most),
        JsonNode n => n.ToJsonString().Length > 4000 ? JsonValue.Create("{…}") : n.DeepClone(),
        _ => ToArg(v),
    };

    static JsonArray Cut(List<object?> list, int most)
    {
        var a = new JsonArray(list.Take(most).Select(x => ToDisplay(x, most)).ToArray());
        if (list.Count > most) a.Add((JsonNode)new JsonObject { ["$more"] = list.Count - most });
        return a;
    }

    static double R(double x) => Math.Round(x, 6);

    /// <summary>What a command or query gave, as values: arrays become lists, numbers doubles; objects stay JSON.</summary>
    public static object? FromJson(JsonNode? n) => n switch
    {
        null => null,
        JsonArray a => a.Select(FromJson).ToList(),
        JsonValue v when Json.Number(v) is { } d && v.GetValueKind() == System.Text.Json.JsonValueKind.Number => d,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        _ => n.DeepClone(),
    };

    /// <summary>A node's constant (from its <c>values</c>) as a value: [x, y, z] stays a list of numbers; {$pt} is a point.</summary>
    public static object? FromConstant(JsonNode? n) =>
        n is JsonObject o && o["$pt"] is JsonArray pt ? Point(pt) : FromJson(n);

    static Vec3 Point(JsonArray a) => new(Json.Number(a.ElementAtOrDefault(0)) ?? 0, Json.Number(a.ElementAtOrDefault(1)) ?? 0, Json.Number(a.ElementAtOrDefault(2)) ?? 0);

    // ── reading an input as a kind ──

    public static double Number(object? v, string what) => v switch
    {
        double d => d,
        int i => i,
        bool b => b ? 1 : 0,
        string s when double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) => p,
        JsonNode n when Json.Number(n) is { } d => d,
        null => throw new CommandException($"{what}: a number is missing."),
        _ => throw new CommandException($"{what}: {Describe(v)} is not a number."),
    };

    public static double Number(object? v, string what, double fallback) => v == null ? fallback : Number(v, what);

    public static int Integer(object? v, string what, int fallback) => v == null ? fallback : (int)Math.Round(Number(v, what));

    public static string Text(object? v) => v switch
    {
        null => "",
        string s => s,
        double d => d.ToString("0.######", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        Vec3 p => string.Create(CultureInfo.InvariantCulture, $"({p.X:0.###}, {p.Y:0.###}, {p.Z:0.###})"),
        List<object?> l => string.Join(", ", l.Select(Text)),
        JsonNode n => n.ToJsonString(),
        _ => v.ToString() ?? "",
    };

    public static bool Bool(object? v) => v switch
    {
        null => false,
        bool b => b,
        double d => d != 0,
        string s => s is "true" or "1" or "yes",
        _ => true,
    };

    public static Vec3 Point(object? v, string what) => v switch
    {
        Vec3 p => p,
        List<object?> { Count: 2 or 3 } l when l.All(x => x is double) => new((double)l[0]!, (double)l[1]!, l.Count > 2 ? (double)l[2]! : 0),
        JsonArray a when a.Count is 2 or 3 => Point(a),
        JsonObject o when o["$pt"] is JsonArray a => Point(a),
        null => throw new CommandException($"{what}: a point is missing."),
        _ => throw new CommandException($"{what}: {Describe(v)} is not a point."),
    };

    public static List<object?> List(object? v) => v switch
    {
        null => [],
        List<object?> l => l,
        _ => [v],
    };

    /// <summary>A value in a few words, for a message.</summary>
    public static string Describe(object? v) => v switch
    {
        null => "nothing",
        string s => $"„{(s.Length > 40 ? s[..40] + "…" : s)}”",
        List<object?> l => $"a list of {l.Count}",
        Vec3 => "a point",
        Polyline => "a polyline",
        JsonObject => "an object",
        _ => Text(v),
    };

    /// <summary>How many values a value holds, all its lists' items counted (the lists' limit is on this).</summary>
    public static int Size(object? v) => v is List<object?> l ? l.Sum(Size) + 1 : 1;
}
