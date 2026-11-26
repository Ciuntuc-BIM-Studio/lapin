using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// One step of a procurement chain (docs/plan-4d5d.md, procurement): its key, its name, its default days and IFC's kind
/// of task (LOGISTIC for the transport; none for the others, which are ordinary tasks tagged <c>phase: procurement</c>).
/// </summary>
public sealed record ProcStep(string Key, string Name, int Days, string? Kind = null)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["key"] = Key, ["name"] = Name, ["days"] = Days };
        if (Kind != null) o["kind"] = Kind;
        return o;
    }

    public static ProcStep? Read(JsonNode? n, int i)
    {
        if (n is not JsonObject o) return null;
        var key = ((string?)o["key"])?.Trim() is { Length: > 0 } k ? k : $"p{i + 1}";
        var name = ((string?)o["name"])?.Trim() is { Length: > 0 } nm ? nm : key;
        var kind = (string?)o["kind"] is { } kd && Plan.Kinds.Contains(kd) ? kd : null;
        return new(key, name, Math.Max(0, (int)Math.Round(Json.Number(o["days"]) ?? 5)), kind);
    }
}

/// <summary>
/// A package bought for the works (a steel structure, the windows, a kitchen): its key, its name (empty: the step's), the
/// generator step whose tasks it precedes (<c>before</c>), whether it is made, its own days for some of the chain's steps
/// (by their keys, over the chain's), and the lag between its last step (the transport) and the installation.
/// </summary>
public sealed record ProcPackage(string Key, string Name, string Before, bool On = true, IReadOnlyDictionary<string, int>? DaysOf = null, int Lag = 0)
{
    public IReadOnlyDictionary<string, int> Days => DaysOf ?? new Dictionary<string, int>();

    public int DaysFor(ProcStep s) => Days.TryGetValue(s.Key, out var d) ? d : s.Days;

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["key"] = Key, ["name"] = Name, ["before"] = Before, ["on"] = On };
        if (Days.Count > 0) o["days"] = new JsonObject(Days.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)));
        if (Lag != 0) o["lag"] = Lag;
        return o;
    }

    public static ProcPackage? Read(JsonNode? n, int i)
    {
        if (n is not JsonObject o) return null;
        var before = ((string?)o["before"])?.Trim() ?? "";
        var key = ((string?)o["key"])?.Trim() is { Length: > 0 } k ? k : before.Length > 0 ? before : $"package{i + 1}";
        var days = new Dictionary<string, int>();
        if (o["days"] is JsonObject d)
            foreach (var (dk, dv) in d)
                if (Json.Number(dv) is { } x && double.IsFinite(x)) days[dk] = Math.Max(0, (int)Math.Round(x));
        return new(key, ((string?)o["name"])?.Trim() ?? "", before, o["on"] is not JsonValue ov || !ov.TryGetValue<bool>(out var on) || on,
            days.Count > 0 ? days : null, (int)Math.Round(Json.Number(o["lag"]) ?? 0));
    }
}

/// <summary>
/// The generator's procurement (docs/plan-4d5d.md, procurement chains): switched on, every package is a summary under
/// „Procurement” holding the chain (by default: design and shop drawings, the client's approval, manufacturing, the
/// transport to site, 5 working days each, Lapin's indicative values), its first step after the milestone „Drawings
/// received” at the project's start, each step finish-to-start after the one before, the last before the first task of the
/// package's step. The chain is the user's template, kept with the spec (and so in the project's schedule styles); each
/// package may change its days. Its tasks are left out of location-based charts (<c>chart: false</c>).
/// </summary>
public sealed record Procurement(bool On, IReadOnlyList<ProcStep> Chain, IReadOnlyList<ProcPackage> Packages, string Milestone = Procurement.DefaultMilestone, string Title = Procurement.DefaultTitle)
{
    public const string Phase = "procurement";
    public const string DefaultMilestone = "Drawings received", DefaultTitle = "Procurement";

    /// <summary>Lapin's chain, indicative: four steps of 5 working days.</summary>
    public static readonly IReadOnlyList<ProcStep> DefaultChain =
    [
        new("design", "Design and shop drawings", 5),
        new("approval", "Client approval", 5),
        new("manufacturing", "Manufacturing", 5),
        new("transport", "Transport to site", 5, "LOGISTIC"),
    ];

    /// <summary>What the configurator switches on: Lapin's chain, no package yet.</summary>
    public static readonly Procurement Default = new(true, DefaultChain, []);

    /// <summary>Whether the chain is Lapin's own (its days indicative).</summary>
    public bool Indicative => Chain.Count == DefaultChain.Count && Chain.Zip(DefaultChain).All(x => x.First == x.Second);

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["on"] = On,
            ["chain"] = new JsonArray(Chain.Select(s => (JsonNode)s.ToJson()).ToArray()),
            ["packages"] = new JsonArray(Packages.Select(p => (JsonNode)p.ToJson()).ToArray()),
        };
        if (Milestone != DefaultMilestone) o["milestone"] = Milestone;
        if (Title != DefaultTitle) o["title"] = Title;
        return o;
    }

    public static Procurement? Read(JsonNode? n)
    {
        if (n is not JsonObject o) return null;
        var chain = o["chain"] is JsonArray c ? Unique(c.Select(ProcStep.Read).OfType<ProcStep>(), s => s.Key, (s, k) => s with { Key = k }) : DefaultChain.ToList();
        var packages = o["packages"] is JsonArray a ? Unique(a.Select(ProcPackage.Read).OfType<ProcPackage>(), p => p.Key, (p, k) => p with { Key = k }) : [];
        return new(o["on"] is JsonValue v && v.TryGetValue<bool>(out var on) && on, chain, packages,
            ((string?)o["milestone"])?.Trim() is { Length: > 0 } m ? m : DefaultMilestone,
            ((string?)o["title"])?.Trim() is { Length: > 0 } t ? t : DefaultTitle);
    }

    /// <summary>Two of one key would make the same tasks: the later ones get keys of their own.</summary>
    static List<T> Unique<T>(IEnumerable<T> xs, Func<T, string> key, Func<T, string, T> with)
    {
        var seen = new HashSet<string>();
        var o = new List<T>();
        foreach (var x in xs)
        {
            var k = key(x);
            for (var n = 2; !seen.Add(k); n++) k = $"{key(x)}-{n}";
            o.Add(k == key(x) ? x : with(x, k));
        }
        return o;
    }

    /// <summary>A procurement as a command gives it, checked.</summary>
    public static void Check(JsonNode? n)
    {
        if (n is null) return;
        if (n is not JsonObject o) throw new CommandException("Procurement is an object (on, chain, packages).");
        if (o["chain"] is not null and not JsonArray) throw new CommandException("A procurement chain is a list of steps.");
        if (o["packages"] is not null and not JsonArray) throw new CommandException("Procurement packages are a list.");
        if ((o["chain"] as JsonArray)?.Count > 12) throw new CommandException("A procurement chain has at most 12 steps.");
    }
}
