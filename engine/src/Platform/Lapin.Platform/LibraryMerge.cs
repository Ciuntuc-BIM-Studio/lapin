using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What a type library (a .lapintypes pack, a wall and floor types JSON) brings that the project already has under the same
/// name (a tester's report: nothing lost in silence): what differs, said shortly ("section hatch", "colour", "parameters"), and
/// the user's choice for each. A material: keep the project's (the default), update it from the library (in place: its id
/// and GlobalId stay, so what uses it follows) or keep both, the library's made as „Name (2)”. A type: update the project's
/// in place (its elements follow), keep both („Name (2)”) or skip the library's.
/// </summary>
public static class LibraryMerge
{
    public const string Keep = "keep", Update = "update", Both = "both", Skip = "skip";
    public static readonly string[] MaterialChoices = [Keep, Update, Both];
    public static readonly string[] TypeChoices = [Update, Both, Skip];

    /// <summary>A key of a material's or a type's attributes as the dialog names it.</summary>
    public static string Label(string key) => key switch
    {
        "color" => "colour",
        "section" => "section hatch",
        "plan" => "plan hatch",
        "elevation" => "elevation hatch",
        "params" => "parameters",
        "measure" => "measurement",
        "drawJoints" => "joints",
        "spec" => "configuration",
        _ => key,
    };

    /// <summary>The top-level keys whose values differ between two attribute sets (one has it, the other not, or not the same), as labels; <paramref name="ignore"/> left aside.</summary>
    public static List<string> Differs(JsonObject a, JsonObject b, IReadOnlyCollection<string>? ignore = null)
    {
        var o = new List<string>();
        foreach (var k in a.Select(x => x.Key).Union(b.Select(x => x.Key)))
        {
            if (ignore?.Contains(k) == true) continue;
            var x = a[k];
            var y = b[k];
            if (x is null && y is null) continue;
            if (!JsonNode.DeepEquals(x, y)) o.Add(Label(k));
        }
        return o.Distinct().ToList();
    }

    /// <summary>„Name (2)”, „Name (3)”…: the first that no entity of <paramref name="cls"/> has.</summary>
    public static string FreeName(IModelView m, string cls, string name)
    {
        var n = 2;
        while (m.OfClass(cls).Any(e => string.Equals(e.Name, $"{name} ({n})", StringComparison.OrdinalIgnoreCase))) n++;
        return $"{name} ({n})";
    }

    /// <summary>
    /// The choices a command was given: <c>{"&lt;key&gt;": choice}</c> (a material by its name, a type by its pack ref or its
    /// name), keys compared without case; an unknown choice refused.
    /// </summary>
    public static Dictionary<string, string> Choices(JsonNode? node, string[] allowed)
    {
        var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (node is null) return o;
        if (node is not JsonObject obj) throw new CommandException("The choices are an object: a name, then keep, update, both or skip.");
        foreach (var (k, v) in obj)
        {
            var c = (string?)v ?? "";
            if (!allowed.Contains(c)) throw new CommandException($"„{c}” is not a choice here ({string.Join(", ", allowed)}).");
            o[k] = c;
        }
        return o;
    }

    /// <summary>What a command reports of the materials it settled: made, updated in place, kept as the project had them, made beside as „Name (2)”.</summary>
    public sealed class MaterialTally
    {
        public int Made, Updated, Kept;
        public readonly List<string> Renamed = [];

        public JsonObject ToJson() => new()
        {
            ["made"] = Made, ["updated"] = Updated, ["kept"] = Kept,
            ["renamed"] = new JsonArray(Renamed.Select(n => (JsonNode)n).ToArray()),
        };
    }
}
