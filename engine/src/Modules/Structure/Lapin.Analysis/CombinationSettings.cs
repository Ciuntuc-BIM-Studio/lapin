using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis;

/// <summary>
/// The project's combination factors (docs/plan-fem.md §13.1), on the IfcProject (<c>analysisCombinations</c>): the named set
/// (<c>en</c>, EN 1990 Annex A1 recommended; <c>cr0</c>, CR 0-2012), each factor the project's own on top of it (γG,sup, γG,inf,
/// γQ, ψ by category), the expression of the fundamental combination (6.10, or 6.10a and 6.10b with ξ), and the generated
/// combinations switched off (their keys). A project without it (every project made before §13) has the set <c>en</c>:
/// exactly the combinations it had; a new project is given <c>cr0</c> (<see cref="NewProject"/>). Indicative, the user's.
/// </summary>
public sealed record CombinationSettings
{
    public const string Key = "analysisCombinations";
    public const string En = "en", Cr0 = "cr0";
    public static readonly string[] SetNames = [En, Cr0];
    public const string Expression610 = "6.10", Expression610ab = "6.10ab";
    public static readonly string[] Expressions = [Expression610, Expression610ab];
    public const double DefaultXi = 0.85;

    /// <summary>A named set of factors: its partial factors and its ψ by category (null: the category's EN value).</summary>
    public sealed record FactorSet(string Name, string Label, double GammaGSup, double GammaGInf, double GammaQ, Func<string?, Psi?> Table);

    public static readonly FactorSet EnSet = new(En, "EN 1990 Annex A1, recommended values", 1.35, 1.0, 1.5, Categories.TableOf);

    /// <summary>
    /// CR 0-2012 (Cod de proiectare. Bazele proiectării construcțiilor): Table 7.2 (γ, the fundamental combination, 6.9) and
    /// Table 7.1 (ψ): as EN for the imposed loads, but roofs 0.7/0/0, snow 0.7/0.5/0.4 (CR 1-1-3/2012 Table 4.3 too), wind
    /// 0.7/0.2/0 (docs/plan-fem.md §13.1: read from the text notified in April 2012; wind and roofs to confirm on the published one).
    /// </summary>
    public static readonly FactorSet Cr0Set = new(Cr0, "CR 0-2012", 1.35, 1.0, 1.5, c => c switch
    {
        Categories.Snow => new Psi(0.7, 0.5, 0.4),
        Categories.Wind => new Psi(0.7, 0.2, 0),
        "H" => new Psi(0.7, 0, 0),
        _ => Categories.TableOf(c),
    });

    public static FactorSet SetOf(string? name) => name == Cr0 ? Cr0Set : EnSet;

    public string Set { get; init; } = En;
    public double? OwnGammaGSup { get; init; }
    public double? OwnGammaGInf { get; init; }
    public double? OwnGammaQ { get; init; }
    public IReadOnlyDictionary<string, Psi> OwnPsi { get; init; } = new Dictionary<string, Psi>();
    public string Expression { get; init; } = Expression610;
    public double? OwnXi { get; init; }
    public IReadOnlySet<string> Disabled { get; init; } = new HashSet<string>();
    /// <summary>Whether the project says anything (a project without it gives exactly the combinations of before §13).</summary>
    public bool Stored { get; init; }

    public FactorSet Factors => SetOf(Set);
    public double GammaGSup => OwnGammaGSup ?? Factors.GammaGSup;
    public double GammaGInf => OwnGammaGInf ?? Factors.GammaGInf;
    public double GammaQ => OwnGammaQ ?? Factors.GammaQ;
    public double Xi => OwnXi ?? DefaultXi;
    public bool Split => Expression == Expression610ab;

    /// <summary>The set's ψ of a category (null for a case without one).</summary>
    public Psi? SetPsi(string? category) => Factors.Table(category);

    /// <summary>The project's own ψ of a category, when it gives one.</summary>
    public Psi? GivenPsi(string? category) => category != null && OwnPsi.TryGetValue(category, out var p) ? p : null;

    /// <summary>The keys ψ may be given for: the categories of use, snow and wind.</summary>
    public static IEnumerable<string> PsiKeys => Categories.All.Select(c => c.Key).Append(Categories.Snow).Append(Categories.Wind);

    static readonly (string Key, double Min, double Max, string What)[] Ranges =
    [
        ("gammaGSup", 1.0, 2.0, "γG,sup"), ("gammaGInf", 0, 1.5, "γG,inf"), ("gammaQ", 1.0, 2.0, "γQ"), ("xi", 0.5, 1.0, "ξ"),
    ];

    static double? Num(JsonObject o, string key, double min, double max) =>
        Json.Number(o[key]) is { } v && double.IsFinite(v) && v >= min && v <= max ? v : null;

    public static CombinationSettings Of(IModelView m)
    {
        if (Spatial.Project(m)?.Attrs[Key] is not JsonObject o) return new();
        var psi = new Dictionary<string, Psi>();
        if (o["psi"] is JsonObject po)
            foreach (var (k, v) in po)
                if (PsiKeys.Contains(k) && v is JsonObject x && Json.Number(x["0"]) is { } a && Json.Number(x["1"]) is { } b && Json.Number(x["2"]) is { } c)
                    psi[k] = new Psi(a, b, c);
        return new CombinationSettings
        {
            Set = (string?)o["set"] is { } s && SetNames.Contains(s) ? s : En,
            OwnGammaGSup = Num(o, "gammaGSup", 1.0, 2.0),
            OwnGammaGInf = Num(o, "gammaGInf", 0, 1.5),
            OwnGammaQ = Num(o, "gammaQ", 1.0, 2.0),
            OwnPsi = psi,
            Expression = (string?)o["expression"] is { } e && Expressions.Contains(e) ? e : Expression610,
            OwnXi = Num(o, "xi", 0.5, 1.0),
            Disabled = (o["disabled"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [],
            Stored = true,
        };
    }

    static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>What the combinations' notes say of the factors.</summary>
    public string Describe() =>
        $"{Factors.Label}: γG,sup {N(GammaGSup)}, γG,inf {N(GammaGInf)}, γQ {N(GammaQ)}" +
        (OwnGammaGSup != null || OwnGammaGInf != null || OwnGammaQ != null || OwnPsi.Count > 0 ? " (some the project's own)" : "") +
        (Split ? $"; 6.10a and 6.10b, ξ {N(Xi)}" : "");

    public JsonObject ToJson()
    {
        JsonObject Table(FactorSet s) => new(PsiKeys.Select(k => new KeyValuePair<string, JsonNode?>(k, (s.Table(k) ?? Categories.Fallback).ToJson())));
        JsonObject SetJson(FactorSet s) => new() { ["name"] = s.Name, ["label"] = s.Label, ["gammaGSup"] = s.GammaGSup, ["gammaGInf"] = s.GammaGInf, ["gammaQ"] = s.GammaQ, ["psi"] = Table(s) };
        return new JsonObject
        {
            ["set"] = Set, ["gammaGSup"] = GammaGSup, ["gammaGInf"] = GammaGInf, ["gammaQ"] = GammaQ,
            ["own"] = new JsonObject
            {
                ["gammaGSup"] = OwnGammaGSup, ["gammaGInf"] = OwnGammaGInf, ["gammaQ"] = OwnGammaQ, ["xi"] = OwnXi,
                ["psi"] = new JsonObject(OwnPsi.Select(x => new KeyValuePair<string, JsonNode?>(x.Key, x.Value.ToJson()))),
            },
            ["psi"] = new JsonObject(PsiKeys.Select(k => new KeyValuePair<string, JsonNode?>(k, (GivenPsi(k) ?? SetPsi(k) ?? Categories.Fallback).ToJson()))),
            ["expression"] = Expression, ["xi"] = Xi,
            ["disabled"] = new JsonArray(Disabled.Order(StringComparer.Ordinal).Select(k => (JsonNode)k).ToArray()),
            ["sets"] = new JsonObject { [En] = SetJson(EnSet), [Cr0] = SetJson(Cr0Set) },
        };
    }

    static JsonObject Own(ICommandContext ctx, out Entity project)
    {
        project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        return project.Attrs[Key] is JsonObject had ? (JsonObject)had.DeepClone() : new JsonObject();
    }

    static void Store(ICommandContext ctx, Entity project, JsonObject own)
    {
        var attrs = project.CloneAttrs();
        attrs.Remove(Key);
        // A project that said nothing, and says nothing still, keeps no attribute (its set: en).
        if (own.Count > 0) attrs[Key] = own;
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>
    /// {set?, gammaGSup?, gammaGInf?, gammaQ?, psi?: {category: {0, 1, 2} | null} | null, expression?, xi?}: written onto the
    /// project's own; null takes one back to the set's (the set itself back to en, as a project of before §13), absent leaves it.
    /// </summary>
    public static void Write(ICommandContext ctx, JsonObject a)
    {
        string[] keys = ["set", "gammaGSup", "gammaGInf", "gammaQ", "psi", "expression", "xi"];
        if (!keys.Any(a.ContainsKey)) throw new CommandException("Say the set, a partial factor, ψ, the expression or ξ.");
        var own = Own(ctx, out var project);
        if (a.ContainsKey("set"))
        {
            if (a["set"] is null) own.Remove("set");
            else if ((string?)a["set"] is { } s && SetNames.Contains(s)) own["set"] = s;
            else throw new CommandException($"The set of factors is one of {string.Join(", ", SetNames)}.");
        }
        foreach (var (key, min, max, what) in Ranges)
            if (a.ContainsKey(key))
            {
                if (a[key] is null) own.Remove(key);
                else if (Json.Number(a[key]) is { } v && double.IsFinite(v) && v >= min && v <= max) own[key] = v;
                else throw new CommandException($"{what} is a number from {N(min)} to {N(max)}.");
            }
        if (a.ContainsKey("expression"))
        {
            if (a["expression"] is null) own.Remove("expression");
            else if ((string?)a["expression"] is { } e && Expressions.Contains(e)) own["expression"] = e;
            else throw new CommandException("The expression is 6.10 or 6.10ab (6.10a and 6.10b).");
        }
        if (a.ContainsKey("psi"))
        {
            if (a["psi"] is null) own.Remove("psi");
            else if (a["psi"] is JsonObject p)
            {
                var psi = own["psi"] is JsonObject had ? (JsonObject)had.DeepClone() : new JsonObject();
                foreach (var (k, v) in p)
                {
                    if (!PsiKeys.Contains(k)) throw new CommandException($"„{k}” is not a category: one of {string.Join(", ", PsiKeys)}.");
                    if (v is null) { psi.Remove(k); continue; }
                    if (v is not JsonObject x) throw new CommandException("ψ is {\"0\": ψ0, \"1\": ψ1, \"2\": ψ2}.");
                    var o = new JsonObject();
                    foreach (var i in new[] { "0", "1", "2" })
                    {
                        if (Json.Number(x[i]) is not { } n || !(n >= 0 && n <= 1)) throw new CommandException("ψ0, ψ1 and ψ2 are numbers between 0 and 1.");
                        o[i] = n;
                    }
                    psi[k] = o;
                }
                own.Remove("psi");
                if (psi.Count > 0) own["psi"] = psi;
            }
            else throw new CommandException("ψ is {category: {\"0\": ψ0, \"1\": ψ1, \"2\": ψ2}}.");
        }
        Store(ctx, project, own);
    }

    /// <summary>A generated (or the user's) combination switched off or on again, by its key.</summary>
    public static void Enable(ICommandContext ctx, string key, bool enabled)
    {
        var own = Own(ctx, out var project);
        var list = (own["disabled"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [];
        list.Remove(key);
        if (!enabled) list.Add(key);
        own.Remove("disabled");
        if (list.Count > 0) own["disabled"] = new JsonArray(list.Select(k => (JsonNode)k).ToArray());
        Store(ctx, project, own);
    }

    /// <summary>What a new project starts with (§13.1): CR 0-2012, written explicitly, so the project keeps it whatever a later default.</summary>
    public sealed class NewProject : INewProject
    {
        public void Start(JsonObject projectAttrs) => projectAttrs[Key] = new JsonObject { ["set"] = Cr0 };
    }
}
