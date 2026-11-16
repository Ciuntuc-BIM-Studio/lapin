using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The one conflict handling of every library import (audit D13; <see cref="LibraryFile"/> for the files): what a file brings
/// that the project (or this computer's library) already has, and the user's choice for each, said the same way everywhere.
/// <para>An item of the file is <b>new</b> (added), <b>the same</b> as one there (kept, nothing to choose), or a conflict: the
/// same <b>name</b> (a symbol, a type, a rule pack, a title block, a part: keep both, the file's made beside as „Name (2)”, is the
/// default) or the same <b>id</b> (what is the same thing by its identity: a drafting standard's style, a line type, a shared
/// parameter, a bar shape's code, my furniture's entry: updating it in place is the default). The choices are always the same
/// three words: <c>both</c> (keep both), <c>update</c> (replace the one there in place: its id and GlobalId stay, so what uses it
/// follows; „replace” is read as it) and <c>skip</c> (leave the one there; „keep” is read as it). A row may offer fewer (a bar
/// shape's code is its identity: no „both”). A command given no choice keeps the default it always had.</para>
/// <para>A material a type library brings is the one exception kept from before: the project's (keep, the default), update it
/// from the library, or keep both; what differs is said shortly ("section hatch", "colour", "parameters").</para>
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

    /// <summary>
    /// „Name (2)”, „Name (3)”…: the first that no entity of <paramref name="cls"/> has. An import's copy kept beside the
    /// project's own always takes its number in brackets (the import's previews and reports name it so), unlike the free names
    /// of everything else (<see cref="Names.Free(Func{string, bool}, string)"/>, „Name 2”).
    /// </summary>
    public static string FreeName(IModelView m, string cls, string name) =>
        FreeName(n => m.OfClass(cls).Any(e => string.Equals(e.Name, n, StringComparison.OrdinalIgnoreCase)), name);

    /// <summary>„Name (2)”, „Name (3)”…: the first <paramref name="taken"/> refuses not, for what is not an entity (a style, a line type, a parameter, a library entry).</summary>
    public static string FreeName(Func<string, bool> taken, string name)
    {
        var n = 2;
        while (taken($"{name} ({n})")) n++;
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
            var c = Word((string?)v ?? "", allowed);
            if (!allowed.Contains(c)) throw new CommandException($"„{(string?)v}” is not a choice here ({string.Join(", ", allowed)}).");
            o[k] = c;
        }
        return o;
    }

    /// <summary>A choice as <paramref name="allowed"/> spells it: „replace” is update; „keep” and „skip” are the same (the one there stays), whichever the list has.</summary>
    public static string Word(string c, IReadOnlyCollection<string> allowed) => c switch
    {
        "replace" => Update,
        Skip when !allowed.Contains(Skip) && allowed.Contains(Keep) => Keep,
        Keep when !allowed.Contains(Keep) && allowed.Contains(Skip) => Skip,
        _ => c,
    };

    // ------------------------------------------------------------ the one handling

    /// <summary>The three choices every conflict offers: keep both, update the one there, skip the file's.</summary>
    public static readonly string[] Unified = [Both, Update, Skip];

    /// <summary>What an item of a file is to what is there: new, the same, the same name, the same id.</summary>
    public enum Match { New, Same, Name, Id }

    /// <summary>What an import does with an item: add it, keep what is there (the same), or the choice made for a conflict.</summary>
    public const string Add = "add", Same = "same";

    /// <summary>
    /// One item of a file as the import preview shows it: its key (what the choices name it by), its kind, its name, how it matches
    /// what is there, what differs, the choices it offers (none when new or the same) and the one it takes when none is given.
    /// </summary>
    public sealed record Row(string Key, string Kind, string Name, Match Match, IReadOnlyList<string>? Differs = null, IReadOnlyList<string>? Allowed = null, string? Default = null)
    {
        public bool Conflict => Match is Match.Name or Match.Id;
        public IReadOnlyList<string> Choices => Conflict ? Allowed ?? Unified : [];
        public string? Choice => Match switch { Match.Name => Default ?? Both, Match.Id => Default ?? Update, _ => null };

        public JsonObject ToJson() => new()
        {
            ["key"] = Key, ["kind"] = Kind, ["name"] = Name,
            ["status"] = Match switch { Match.New => "new", Match.Same => "same", Match.Name => "name", _ => "id" },
            ["differs"] = new JsonArray((Differs ?? []).Select(d => (JsonNode)d).ToArray()),
            ["choices"] = new JsonArray(Choices.Select(c => (JsonNode)c).ToArray()),
            ["choice"] = Choice,
        };
    }

    /// <summary>The preview's rows as JSON, with how many are new, the same and in conflict.</summary>
    public static JsonObject Preview(IEnumerable<Row> rows)
    {
        var list = rows.ToList();
        return new JsonObject
        {
            ["rows"] = new JsonArray(list.Select(r => (JsonNode)r.ToJson()).ToArray()),
            ["new"] = list.Count(r => r.Match == Match.New),
            ["same"] = list.Count(r => r.Match == Match.Same),
            ["conflicts"] = list.Count(r => r.Conflict),
        };
    }

    /// <summary>
    /// What becomes of an item: <see cref="Add"/> when it is new, <see cref="Same"/> when it is the same, else its choice — the one
    /// given for its key, else the one for all (when the item offers it), else <paramref name="fallback"/> (the command's default
    /// from before, when it had one), else the row's default. A choice given for the item that it does not offer is refused.
    /// </summary>
    public static string Decide(Row row, JsonObject? choices, string? all, string? fallback = null)
    {
        if (row.Match == Match.New) return Add;
        if (row.Match == Match.Same) return Same;
        var offered = row.Choices;
        if (choices?[row.Key] is JsonValue jv && jv.TryGetValue<string>(out var given) && given.Length > 0)
        {
            var c = Word(given, offered);
            return offered.Contains(c) ? c : throw new CommandException($"„{given}” is not a choice for „{row.Name}” ({string.Join(", ", offered)}).");
        }
        if (all is { Length: > 0 } && Word(all, offered) is var a && offered.Contains(a)) return a;
        return fallback is { } f && offered.Contains(f) ? f : row.Choice!;
    }

    /// <summary>What an import did, counted the same way by every kind: added, updated in place, kept (the same, or the project's), skipped, made beside as „Name (2)”.</summary>
    public sealed class Tally
    {
        public int Added, Updated, Kept, Skipped;
        public readonly List<string> Renamed = [];

        public JsonObject ToJson() => new()
        {
            ["added"] = Added, ["updated"] = Updated, ["kept"] = Kept, ["skipped"] = Skipped,
            ["renamed"] = new JsonArray(Renamed.Select(n => (JsonNode)n).ToArray()),
        };
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
