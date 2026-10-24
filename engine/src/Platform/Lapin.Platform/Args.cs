using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Command arguments read the same way by every module: a point, an outline,
/// the ids of a selection, a join style. What is malformed is refused with
/// the reason, never read as zero or let through to fail later inside the
/// engine.
/// </summary>
public static class Args
{
    /// <summary>A finite number under <paramref name="key"/>, required.</summary>
    public static double Number(JsonObject a, string key)
    {
        if (a[key] == null) throw new CommandException($"Missing {key}.");
        return Json.Number(a[key]) is { } n && double.IsFinite(n) ? n : throw new CommandException($"{key} must be a number.");
    }

    /// <summary>A plan point given as <c>[x, y]</c> (finite numbers); <paramref name="what"/> names it in the refusal.</summary>
    public static Vec2 Point(JsonNode? n, string what)
    {
        if (n is JsonArray p && p.Count >= 2 && Json.Number(p[0]) is { } x && Json.Number(p[1]) is { } y && double.IsFinite(x) && double.IsFinite(y)) return new Vec2(x, y);
        throw new CommandException($"The {what} is [x, y].");
    }

    /// <summary>A plan point given as two numbers under <paramref name="x"/> and <paramref name="y"/>, both required.</summary>
    public static Vec2 Point(JsonObject a, string x, string y) => new(Number(a, x), Number(a, y));

    /// <summary>
    /// An outline: <c>points</c>, a list of <c>[x, y]</c>, or else the rectangle
    /// between <c>x1, y1</c> and <c>x2, y2</c> (counter-clockwise from the first corner).
    /// </summary>
    public static IReadOnlyList<Vec2> Points(JsonObject a)
    {
        if (a["points"] is JsonArray arr)
        {
            if (arr.Count == 0) throw new CommandException("points is empty.");
            return arr.Select((n, i) => Point(n, $"point {i + 1} of points")).ToList();
        }
        if (a["points"] != null) throw new CommandException("points is a list of [x, y].");
        var (p, q) = (Point(a, "x1", "y1"), Point(a, "x2", "y2"));
        return [p, new Vec2(q.X, p.Y), q, new Vec2(p.X, q.Y)];
    }

    /// <summary>The ids a command addresses: <c>ids</c> (a list) and <c>id</c> (one), each once, in that order; what is not a string is skipped.</summary>
    public static IReadOnlyList<string> Ids(JsonObject a)
    {
        var ids = a["ids"] is JsonArray arr ? arr.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>().ToList() : [];
        if ((string?)a["id"] is { } one) ids.Add(one);
        return ids.Distinct().ToList();
    }

    /// <summary>A join style by name: butt or mitre; null when not given.</summary>
    public static JoinStyle? JoinStyle(JsonNode? n) => (string?)n switch
    {
        null => null,
        "butt" => Platform.JoinStyle.Butt,
        "mitre" => Platform.JoinStyle.Mitre,
        var other => throw new CommandException($"A join style is butt or mitre, not „{other}”."),
    };
}
