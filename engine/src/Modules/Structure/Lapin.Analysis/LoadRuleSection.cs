using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis;

/// <summary>
/// The loads in a rule pack (docs/plan-national-packs.md, stage N1): the factor sets the combinations read and the imposed loads
/// of the categories of use, as data. Each set: its label, γG,sup, γG,inf, γQ, the fundamental combination's expression (6.10 or
/// 6.10a and 6.10b) and ξ, ψ by category (of use, snow, wind), ψ of snow by the site's s0,k (zones, each from an s0,k up), and
/// γd by safety class (on the load side, as some national rules give it). Lapin's own pack holds <c>en</c> (EN 1990 Annex A1,
/// recommended) and <c>cr0</c> (CR 0-2012) with exactly the values the code always had, and the imposed loads of
/// <see cref="Categories.All"/>; a set the pack does not name falls back to Lapin's of that name. Every value is indicative and
/// the user's to check against the edition in force; nothing of a norm's text is shipped.
/// </summary>
public sealed class LoadRuleSection : IRuleSection
{
    public const string SectionKey = "loads";
    public string Key => SectionKey;
    public string Label => "Loads";

    public static readonly LoadRuleSection Instance = new();

    /// <summary>Lapin's own sets, by name: the code's values as data.</summary>
    public static readonly IReadOnlyDictionary<string, CombinationSettings.FactorSet> BuiltinSets =
        new Dictionary<string, CombinationSettings.FactorSet> { [CombinationSettings.En] = CombinationSettings.EnSet, [CombinationSettings.Cr0] = CombinationSettings.Cr0Set };

    public JsonObject Default => new()
    {
        ["sets"] = new JsonObject(BuiltinSets.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)ToJson(x.Value)))),
        ["imposed"] = new JsonObject(Categories.All.Select(c => KeyValuePair.Create(c.Key, (JsonNode?)c.Qk))),
    };

    public static JsonObject ToJson(CombinationSettings.FactorSet s) => new()
    {
        ["label"] = s.Label, ["gammaGSup"] = s.GammaGSup, ["gammaGInf"] = s.GammaGInf, ["gammaQ"] = s.GammaQ,
        ["expression"] = s.Expression, ["xi"] = s.Xi,
        ["psi"] = new JsonObject(CombinationSettings.PsiKeys.Select(k => KeyValuePair.Create(k, (JsonNode?)(s.Table(k) ?? Categories.Fallback).ToJson()))),
        ["snowPsi"] = new JsonArray(s.SnowZones.Select(z => (JsonNode)new JsonObject { ["from"] = z.From, ["psi"] = z.Psi.ToJson() }).ToArray()),
        ["safetyClasses"] = new JsonObject(s.SafetyClasses.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Value))),
    };

    const int MaxSets = 20, MaxZones = 20, MaxClasses = 10;

    static bool GoodName(string? k) => k is { Length: > 0 and <= 32 } && k.Trim() == k;

    /// <summary>
    /// A pack's loads, checked and completed: each set given its missing values from Lapin's set of its name (else <c>en</c>),
    /// the imposed loads a category does not give from Lapin's; no sets given, Lapin's. A value out of its range, a category
    /// that is none of the table's, a set or a class without a proper name is refused with why.
    /// </summary>
    public JsonObject Check(JsonNode? section)
    {
        if (section is null) return Default;
        if (section is not JsonObject o) throw new CommandException("The loads are an object: {sets, imposed}.");
        var sets = new JsonObject();
        if (o["sets"] is JsonObject given && given.Count > 0)
        {
            if (given.Count > MaxSets) throw new CommandException($"A pack holds at most {MaxSets} factor sets.");
            foreach (var (name, v) in given)
            {
                if (!GoodName(name)) throw new CommandException("A factor set's name is 1 to 32 characters, without spaces at its ends.");
                sets[name] = ToJson(CheckSet(name, v));
            }
        }
        else if (o["sets"] is not null and not JsonObject) throw new CommandException("The factor sets are an object: {name: set}.");
        else sets = (JsonObject)Default["sets"]!.DeepClone();

        var imposed = new JsonObject();
        var givenQ = o["imposed"] as JsonObject ?? new JsonObject();
        if (o["imposed"] is not null and not JsonObject) throw new CommandException("The imposed loads are an object: {category: qk}.");
        foreach (var (k, _) in givenQ)
            if (Categories.Of(k) == null) throw new CommandException($"„{k}” is not a category of use: one of {string.Join(", ", Categories.All.Select(c => c.Key))}.");
        foreach (var c in Categories.All)
            imposed[c.Key] = RulePacks.Num(givenQ, new JsonObject { [c.Key] = c.Qk }, c.Key, 0, 1000, $"The imposed load of {c.Key} (kN/m²)");
        return new JsonObject { ["sets"] = sets, ["imposed"] = imposed };
    }

    /// <summary>One set as a pack gives it, completed from Lapin's of its name (else en).</summary>
    public static CombinationSettings.FactorSet CheckSet(string name, JsonNode? v)
    {
        if (v is not JsonObject s) throw new CommandException($"The factor set „{name}” is an object.");
        var own = BuiltinSets.TryGetValue(name, out var b);
        var basis = b ?? CombinationSettings.EnSet;
        var fallback = ToJson(basis);
        string what(string x) => $"{name}: {x}";
        var label = s["label"] is null ? (own ? basis.Label : name) : (string?)s["label"] is { } l && l.Trim() is { Length: > 0 and <= 120 } lt ? lt : throw new CommandException(what("the label is 1 to 120 characters."));
        var gSup = RulePacks.Num(s, fallback, "gammaGSup", 1.0, 2.0, what("γG,sup"));
        var gInf = RulePacks.Num(s, fallback, "gammaGInf", 0, 1.5, what("γG,inf"));
        var gQ = RulePacks.Num(s, fallback, "gammaQ", 1.0, 2.0, what("γQ"));
        var xi = RulePacks.Num(s, fallback, "xi", 0.5, 1.0, what("ξ"));
        var expression = s["expression"] is null ? basis.Expression
            : (string?)s["expression"] is { } e && CombinationSettings.Expressions.Contains(e) ? e : throw new CommandException(what("the expression is 6.10 or 6.10ab (6.10a and 6.10b)."));

        var psi = new Dictionary<string, Psi>();
        if (s["psi"] is not null and not JsonObject) throw new CommandException(what("ψ is {category: {\"0\": ψ0, \"1\": ψ1, \"2\": ψ2}}."));
        var givenPsi = s["psi"] as JsonObject ?? new JsonObject();
        foreach (var (k, p) in givenPsi)
            if (!CombinationSettings.PsiKeys.Contains(k)) throw new CommandException(what($"„{k}” is not a category: one of {string.Join(", ", CombinationSettings.PsiKeys)}."));
        foreach (var k in CombinationSettings.PsiKeys)
            psi[k] = givenPsi[k] is { } p ? ReadPsi(p, what($"ψ of {k}")) : basis.Table(k) ?? Categories.Fallback;

        var zones = new List<(double From, Psi Psi)>();
        if (s["snowPsi"] is JsonArray za)
        {
            if (za.Count > MaxZones) throw new CommandException(what($"at most {MaxZones} snow zones."));
            foreach (var z in za)
            {
                if (z is not JsonObject zo) throw new CommandException(what("a snow zone is {from: s0,k in kN/m², psi}."));
                var from = RulePacks.Num(zo, new JsonObject { ["from"] = 0 }, "from", 0, 20, what("a snow zone's s0,k (kN/m²)"));
                if (zones.Any(x => Math.Abs(x.From - from) < 1e-9)) throw new CommandException(what($"two snow zones start at {from} kN/m²."));
                zones.Add((from, ReadPsi(zo["psi"], what("a snow zone's ψ"))));
            }
        }
        else if (s["snowPsi"] is not null) throw new CommandException(what("the snow zones are a list."));
        else zones.AddRange(basis.SnowZones);

        var classes = new Dictionary<string, double>();
        if (s["safetyClasses"] is JsonObject sc)
        {
            if (sc.Count > MaxClasses) throw new CommandException(what($"at most {MaxClasses} safety classes."));
            foreach (var (k, g) in sc)
            {
                if (!GoodName(k)) throw new CommandException(what("a safety class's name is 1 to 32 characters."));
                classes[k] = RulePacks.Num(sc, new JsonObject(), k, 0.5, 1.5, what($"γd of safety class {k}"));
            }
        }
        else if (s["safetyClasses"] is not null) throw new CommandException(what("the safety classes are an object: {class: γd}."));
        else foreach (var (k, g) in basis.SafetyClasses) classes[k] = g;

        return new CombinationSettings.FactorSet(name, label, gSup, gInf, gQ, psi)
        {
            Expression = expression, Xi = xi, SnowZones = zones.OrderBy(z => z.From).ToList(), SafetyClasses = classes,
        };
    }

    static Psi ReadPsi(JsonNode? n, string what)
    {
        if (n is not JsonObject x) throw new CommandException($"{what} is {{\"0\": ψ0, \"1\": ψ1, \"2\": ψ2}}.");
        var v = new double[3];
        for (var i = 0; i < 3; i++)
            v[i] = Json.Number(x[i.ToString()]) is { } d && double.IsFinite(d) && d >= 0 && d <= 1 ? d : throw new CommandException($"{what}: ψ0, ψ1 and ψ2 are numbers between 0 and 1.");
        return new Psi(v[0], v[1], v[2]);
    }

    sealed record Read(IReadOnlyDictionary<string, CombinationSettings.FactorSet> Sets, IReadOnlyDictionary<string, double> Imposed, bool Own);

    static readonly ConditionalWeakTable<JsonObject, Read> Cache = new();
    static readonly Read Builtin = new(BuiltinSets, Categories.All.ToDictionary(c => c.Key, c => c.Qk), false);

    /// <summary>The loads of the project's pack, read from the model alone (Lapin's when the pack says nothing, or says it wrong).</summary>
    static Read Of(IModelView m)
    {
        if (RulePacks.RawSection(m, SectionKey) is not { } raw) return Builtin;
        return Cache.GetValue(raw, r =>
        {
            try
            {
                var c = Instance.Check(r);
                var sets = ((JsonObject)c["sets"]!).ToDictionary(x => x.Key, x => CheckSet(x.Key, x.Value));
                var imposed = ((JsonObject)c["imposed"]!).ToDictionary(x => x.Key, x => Json.Number(x.Value) ?? 0);
                return new Read(sets, imposed, true);
            }
            catch (CommandException) { return Builtin; }
        });
    }

    /// <summary>The factor sets the project may use: its pack's, then Lapin's of a name the pack does not give.</summary>
    public static IReadOnlyDictionary<string, CombinationSettings.FactorSet> Sets(IModelView m)
    {
        var r = Of(m);
        if (!r.Own) return BuiltinSets;
        var o = new Dictionary<string, CombinationSettings.FactorSet>(r.Sets);
        foreach (var (k, v) in BuiltinSets) o.TryAdd(k, v);
        return o;
    }

    /// <summary>The pack's imposed load of a category (kN/m²); null for an unknown one.</summary>
    public static double? Imposed(IModelView m, string key) => Of(m).Imposed.TryGetValue(key, out var q) ? q : null;
}
