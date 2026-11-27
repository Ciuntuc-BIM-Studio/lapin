using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Drawing.Hatch;

namespace Lapin.Drawing;

/// <summary>
/// What a rule of a material standard matches (decision 681): a material's name, by the words or small regular expressions of
/// <see cref="Names"/> (per language: en, ro, de…; any of them found in the name, folded as <see cref="MaterialStandard.Fold"/>
/// folds it), and when <see cref="Category"/> is given the material's category too (a rule with no names matches by its
/// category alone).
/// </summary>
public sealed record StandardMatch(IReadOnlyDictionary<string, IReadOnlyList<string>> Names, string? Category);

/// <summary>
/// A rule of a material standard (decision 681): what it matches, and what a material it matches draws when the material does
/// not say so itself: its cut (<see cref="Section"/>), its plan over the cut (<see cref="Plan"/>), its facade
/// (<see cref="Elevation"/>), any field of a kind of view's graphics with its steps by scale; whether it is a membrane; the
/// category it suggests (taken by a material of none when the standard is applied).
/// </summary>
public sealed class StandardRule
{
    public required StandardMatch Match { get; init; }
    public ViewGraphics? Section { get; init; }
    public ViewGraphics? Plan { get; init; }
    public ViewGraphics? Elevation { get; init; }
    public bool? Membrane { get; init; }
    public string? Category { get; init; }
    /// <summary>The rule as written (its kinds' fields as given), for the file and what a standard sets.</summary>
    public required JsonObject Json { get; init; }
    internal Regex? Re { get; init; }

    /// <summary>Whether it says how a cut (or a plan) draws: section, plan or membrane.</summary>
    public bool SetsCut => Section != null || Plan != null || Membrane != null;

    /// <summary>The fields it gives a kind of view (as written; a plan: the section's and the plan's).</summary>
    public IEnumerable<string> FieldsOf(string kind) => kind switch
    {
        "plan" => Keys("section").Concat(Keys("plan")).Distinct(),
        _ => Keys(kind),
    };

    IEnumerable<string> Keys(string kind) => Json[kind] is JsonObject o ? o.Select(kv => kv.Key) : [];
}

/// <summary>
/// A standard of material graphics as data (docs/plan-material-graphics.md stage 3, decision 681): its id, name, author, country,
/// norm, version, licence and the languages of its rules' names; its ordered rules (<see cref="StandardRule"/>: a material's name
/// or category → its cut, plan and facade graphics, membrane, category); its own hatch patterns (<c>.pat</c> text, paper or
/// model units); the pens it gives every cut (<see cref="Pens"/>: the hatch's and the cut outline's colour, weight and line
/// type). It is the lowest tier of how a material draws (<see cref="MaterialGraphics.Resolve"/>): what a material does not set
/// itself comes from the first rule its name matches (a cut from the first rule that says how a cut draws, a facade from the
/// first that says how a facade draws). Lapin ships five (<see cref="BuiltIns"/>: Lapin, ISO 128-50, DIN 1356, România SR ISO
/// 128-50, BS 8888 / BS 1192), all indicative; a project keeps its own (duplicated, edited, made from its materials, imported).
/// </summary>
public sealed class MaterialStandard
{
    public const string LapinId = "lapin";
    public const string FoldRo = "ro", FoldAll = "all";
    public static readonly string[] Categories = ["Structure", "Finish", "Insulation", "Membrane", "Other"];
    /// <summary>The fields of a standard (beside its rules, patterns and pens), all text but <c>languages</c> and <c>indicative</c>.</summary>
    public static readonly string[] MetaFields = ["id", "name", "description", "author", "country", "norm", "version", "license", "languages", "indicative", "basedOn", "fold"];

    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Author { get; init; }
    public string? Country { get; init; }
    public string? Norm { get; init; }
    public string? Version { get; init; }
    public string? License { get; init; }
    public IReadOnlyList<string> Languages { get; init; } = [];
    public bool Indicative { get; init; }
    public string? BasedOn { get; init; }
    /// <summary>How names are folded before matching: <see cref="FoldAll"/> (every diacritic; the default) or <see cref="FoldRo"/> (Romanian letters only, as Lapin's first table did).</summary>
    public string Fold { get; init; } = FoldAll;
    public IReadOnlyList<StandardRule> Rules { get; init; } = [];
    /// <summary>Its own patterns as <c>.pat</c> text; null: none (it names built-in or the project's).</summary>
    public string? PatternText { get; init; }
    /// <summary>The pens it gives every cut and plan, under the rules: the hatch's (colour, weight, line type) and the cut outline's.</summary>
    public MaterialLine? HatchPen { get; init; }
    public MaterialLine? CutPen { get; init; }
    public bool BuiltIn { get; init; }
    /// <summary>The standard as written (canonical: its fields in order).</summary>
    public required JsonObject Json { get; init; }

    IReadOnlyList<HatchPattern>? _patterns;
    /// <summary>Its own patterns (parsed from <see cref="PatternText"/>).</summary>
    public IReadOnlyList<HatchPattern> Patterns => _patterns ??= PatternText is { Length: > 0 } t ? Pat.Parse(t).Patterns : [];

    public bool IsLapin => BuiltIn && Id == LapinId;

    // ------------------------------------------------------------ matching

    /// <summary>A name as the rules see it: trimmed, lower case, one space, folded (<see cref="Fold"/>).</summary>
    public string FoldName(string name) => FoldText(MaterialGraphics.Normalise(name), Fold);

    /// <summary>
    /// Text folded: <see cref="FoldRo"/> takes ă â î ș ş ț ţ to their letters (Lapin's first table, as old names were typed);
    /// <see cref="FoldAll"/> takes every diacritic off (ä → a, é → e, ł → l), ß to ss, æ to ae, ø to o.
    /// </summary>
    public static string FoldText(string s, string fold)
    {
        if (fold == FoldRo) return s.Replace('ă', 'a').Replace('â', 'a').Replace('î', 'i').Replace('ș', 's').Replace('ş', 's').Replace('ț', 't').Replace('ţ', 't');
        // A table rather than Unicode's decomposition: the engine runs without ICU (invariant globalization, the browser's too).
        StringBuilder? b = null;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c < 0x80) { b?.Append(c); continue; }
            var lower = char.ToLowerInvariant(c);
            var to = FoldMap.TryGetValue(lower, out var m) ? m : null;
            if (to == null) { b?.Append(c); continue; }
            b ??= new StringBuilder(s, 0, i, s.Length + 4);
            b.Append(c == lower ? to : to.ToUpperInvariant());
        }
        return b?.ToString() ?? s;
    }

    static readonly Dictionary<char, string> FoldMap = BuildFoldMap();

    static Dictionary<char, string> BuildFoldMap()
    {
        var o = new Dictionary<char, string>();
        foreach (var (letters, to) in new[]
        {
            ("àáâãäåāăą", "a"), ("çćĉċč", "c"), ("ďđð", "d"), ("èéêëēĕėęě", "e"), ("ĝğġģ", "g"), ("ĥħ", "h"), ("ìíîïĩīĭįı", "i"), ("ĵ", "j"), ("ķ", "k"),
            ("ĺļľŀł", "l"), ("ñńņňŉŋ", "n"), ("òóôõöøōŏő", "o"), ("ŕŗř", "r"), ("śŝşšș", "s"), ("ţťŧț", "t"), ("ùúûüũūŭůűų", "u"), ("ŵ", "w"),
            ("ýÿŷ", "y"), ("źżž", "z"), ("ß", "ss"), ("æ", "ae"), ("œ", "oe"), ("þ", "th"), ("ĳ", "ij"),
        })
            foreach (var c in letters) o[c] = to;
        return o;
    }

    /// <summary>
    /// A name expression as rules and material graphics sets match it (any case): in time linear in the name
    /// (<see cref="RegexOptions.NonBacktracking"/>), so that no expression a user writes runs away and none needs a time limit. A limit
    /// is kept by the clock: on a loaded machine a match cut short by 50 ms was taken for no match, and a masonry was drawn as concrete
    /// (issue #122). An expression only the backtracking engine reads (a lookaround, a back-reference) keeps the 50 ms limit.
    /// In the browser's engine the backtracking engine with its limit, as before: the non-backtracking engine's generic matchers
    /// are not in the AOT-compiled code and Mono's interpreter stops the worker on them (interp.c assertion), so a project
    /// opened in the browser never drew.
    /// </summary>
    public static Regex NameExpression(string pattern)
    {
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (OperatingSystem.IsBrowser()) return new Regex(pattern, options, TimeSpan.FromMilliseconds(50));
        try { return new Regex(pattern, options | RegexOptions.NonBacktracking); }
        catch (NotSupportedException) { return new Regex(pattern, options, TimeSpan.FromMilliseconds(50)); }
    }

    bool Matches(StandardRule r, string folded, string? category)
    {
        if (r.Match.Category is { } c && !string.Equals(c, category, StringComparison.OrdinalIgnoreCase)) return false;
        if (r.Re == null) return r.Match.Category != null;
        try { return r.Re.IsMatch(folded); }
        catch (RegexMatchTimeoutException) { return false; }
    }

    /// <summary>The first rule that says how a cut draws and matches the material (its name, its category); null: none.</summary>
    public StandardRule? CutRule(string name, string? category = null)
    {
        var n = FoldName(name);
        foreach (var r in Rules) if (r.SetsCut && Matches(r, n, category)) return r;
        return null;
    }

    /// <summary>The first rule that says how a facade draws and matches the material; null: none.</summary>
    public StandardRule? ElevationRule(string name, string? category = null)
    {
        var n = FoldName(name);
        foreach (var r in Rules) if (r.Elevation != null && Matches(r, n, category)) return r;
        return null;
    }

    /// <summary>
    /// What the standard gives a material in a kind of view at 1:<paramref name="scale"/>: its pens (cuts and plans), the rule's
    /// section with its step for the scale, a plan's own over it with its step; a facade the rule's elevation with its step.
    /// Null: nothing. No scale, no step.
    /// </summary>
    public (ViewGraphics? Graphics, StandardRule? Rule) For(string kind, string name, string? category, double? scale = null)
    {
        static ViewGraphics? Stepped(ViewGraphics? g, double? scale) => g?.StepAt(scale) is { } at ? MaterialGraphics.Merge(g, at.Step.Fields) : g;
        if (kind == "elevation") return ElevationRule(name, category) is { } er ? (Stepped(er.Elevation, scale), er) : (null, null);
        var rule = CutRule(name, category);
        var g = MaterialGraphics.Merge(PenGraphics, Stepped(rule?.Section, scale));
        if (kind == "plan") g = MaterialGraphics.Merge(g, Stepped(rule?.Plan, scale));
        return (g, rule);
    }

    ViewGraphics? PenGraphics => HatchPen == null && CutPen == null ? null
        : new ViewGraphics { HatchColor = HatchPen?.Color, HatchWeight = HatchPen?.Weight, HatchLineType = HatchPen?.LineType, CutLine = CutPen };

    // ------------------------------------------------------------ reading and writing

    /// <summary>
    /// A standard read from its JSON: refused (an exception naming what is wrong) when it has no id or name, a rule no names
    /// nor category, a name that is not a valid expression, or a category that is not one of <see cref="Categories"/>. Kinds'
    /// graphics are read as a material's; the platform checks their patterns and line types when a project takes it.
    /// </summary>
    public static MaterialStandard Read(JsonObject o, bool builtIn = false)
    {
        var id = ((string?)o["id"])?.Trim() is { Length: > 0 and <= 80 } i ? i : throw new FormatException("A material standard needs an id (80 characters at most).");
        if (!Regex.IsMatch(id, @"^[A-Za-z0-9][A-Za-z0-9._-]*$")) throw new FormatException($"A standard's id is letters, digits, dots, dashes and underscores: „{id}” is not.");
        var name = ((string?)o["name"])?.Trim() is { Length: > 0 and <= 120 } n ? n : throw new FormatException("A material standard needs a name (120 characters at most).");
        var fold = (string?)o["fold"] == FoldRo ? FoldRo : FoldAll;
        var rules = new List<StandardRule>();
        var ri = 0;
        foreach (var x in o["rules"] as JsonArray ?? [])
        {
            ri++;
            if (x is not JsonObject r) throw new FormatException($"Rule {ri} is not an object.");
            rules.Add(ReadRule(r, ri, fold));
        }
        var canon = new JsonObject();
        foreach (var k in MetaFields)
        {
            if (k == "id") { canon[k] = id; continue; }
            if (k == "name") { canon[k] = name; continue; }
            if (k == "fold") { if (fold == FoldRo) canon[k] = FoldRo; continue; }
            if (k == "languages") { if (o[k] is JsonArray la) canon[k] = new JsonArray(la.Select(l => (string?)l).OfType<string>().Select(l => (JsonNode)l.Trim().ToLowerInvariant()).ToArray()); continue; }
            if (k == "indicative") { if (o[k] is JsonValue iv && iv.TryGetValue<bool>(out var ib)) canon[k] = ib; continue; }
            if (((string?)o[k])?.Trim() is { Length: > 0 } s) canon[k] = s.Length > 2000 ? s[..2000] : s;
        }
        MaterialLine? hatchPen = null, cutPen = null;
        if (o["pens"] is JsonObject pens)
        {
            hatchPen = MaterialLine.From(pens["hatch"]);
            cutPen = MaterialLine.From(pens["cut"]);
            var po = new JsonObject();
            if (hatchPen != null) po["hatch"] = hatchPen.ToJson();
            if (cutPen != null) po["cut"] = cutPen.ToJson();
            if (po.Count > 0) canon["pens"] = po;
        }
        var patterns = (string?)o["patterns"] is { Length: > 0 } pt ? pt : null;
        if (patterns != null)
        {
            var (parsed, errors) = Pat.Parse(patterns);
            if (parsed.Count == 0) throw new FormatException(errors.FirstOrDefault() ?? "The standard's patterns are not a .pat text.");
            canon["patterns"] = patterns;
        }
        canon["rules"] = new JsonArray(rules.Select(r => (JsonNode)r.Json.DeepClone()).ToArray());
        return new MaterialStandard
        {
            Id = id, Name = name, Fold = fold, Rules = rules, PatternText = patterns, HatchPen = hatchPen, CutPen = cutPen, BuiltIn = builtIn, Json = canon,
            Description = (string?)canon["description"], Author = (string?)canon["author"], Country = (string?)canon["country"], Norm = (string?)canon["norm"],
            Version = (string?)canon["version"], License = (string?)canon["license"], BasedOn = (string?)canon["basedOn"],
            Languages = canon["languages"] is JsonArray ls ? [.. ls.Select(l => (string?)l).OfType<string>()] : [],
            Indicative = canon["indicative"] is JsonValue v && v.TryGetValue<bool>(out var b) && b,
        };
    }

    static StandardRule ReadRule(JsonObject r, int index, string fold)
    {
        var match = r["match"] as JsonObject ?? throw new FormatException($"Rule {index} has no match.");
        var names = new Dictionary<string, IReadOnlyList<string>>();
        var mo = new JsonObject();
        // Names by language ({en: [...], ro: [...]}); a plain list is taken as English.
        var given = match["names"] switch
        {
            JsonObject byLang => byLang.Select(kv => (kv.Key.Trim().ToLowerInvariant(), kv.Value as JsonArray)),
            JsonArray plain => [("en", plain)],
            _ => [],
        };
        var no = new JsonObject();
        foreach (var (lang, list) in given)
        {
            if (list == null) continue;
            if (!Regex.IsMatch(lang, "^[a-z]{2,3}$")) throw new FormatException($"Rule {index}: „{lang}” is not a language (en, ro, de…).");
            var words = list.Select(w => ((string?)w)?.Trim()).OfType<string>().Where(w => w.Length > 0).Distinct().ToList();
            if (words.Count == 0) continue;
            if (words.Any(w => w.Length > 200)) throw new FormatException($"Rule {index}: a name is 200 characters at most.");
            names[lang] = words;
            no[lang] = new JsonArray(words.Select(w => (JsonNode)w).ToArray());
        }
        if (no.Count > 0) mo["names"] = no;
        var category = ((string?)match["category"])?.Trim() is { Length: > 0 } c ? Categories.FirstOrDefault(x => x.Equals(c, StringComparison.OrdinalIgnoreCase)) ?? throw new FormatException($"Rule {index}: „{c}” is not a category ({string.Join(", ", Categories)}).") : null;
        if (category != null) mo["category"] = category;
        if (names.Count == 0 && category == null) throw new FormatException($"Rule {index} matches nothing: give it names or a category.");
        Regex? re = null;
        if (names.Count > 0)
        {
            // One expression: any of its names (each folded as names are), as Lapin's first table wrote its alternatives.
            var alternatives = names.Values.SelectMany(w => w).Select(w => "(?:" + FoldText(w, fold) + ")");
            try { re = NameExpression(string.Join("|", alternatives)); }
            catch (ArgumentException e) { throw new FormatException($"Rule {index}: a name is not a valid expression ({e.Message})."); }
        }
        var json = new JsonObject { ["match"] = mo };
        ViewGraphics? Kind(string k)
        {
            if (r[k] is not JsonObject g) return null;
            json[k] = g.DeepClone();
            return ViewGraphics.From(g) ?? new ViewGraphics();
        }
        var section = Kind("section");
        var plan = Kind("plan");
        var elevation = Kind("elevation");
        bool? membrane = r["membrane"] is JsonValue mv && mv.TryGetValue<bool>(out var mb) ? mb : null;
        if (membrane != null) json["membrane"] = membrane;
        var cat = ((string?)r["category"])?.Trim() is { Length: > 0 } rc ? Categories.FirstOrDefault(x => x.Equals(rc, StringComparison.OrdinalIgnoreCase)) ?? throw new FormatException($"Rule {index}: „{rc}” is not a category.") : null;
        if (cat != null) json["category"] = cat;
        if (section == null && plan == null && elevation == null && membrane == null && cat == null) throw new FormatException($"Rule {index} gives nothing: a section, plan, elevation, membrane or category.");
        return new StandardRule { Match = new StandardMatch(names, category), Section = section, Plan = plan, Elevation = elevation, Membrane = membrane, Category = cat, Json = json, Re = re };
    }

    /// <summary>The standard's summary as lists show it.</summary>
    public JsonObject Summary()
    {
        var o = new JsonObject();
        foreach (var k in MetaFields) if (Json[k] is { } v) o[k] = v.DeepClone();
        o["builtin"] = BuiltIn;
        o["rules"] = Rules.Count;
        o["patterns"] = new JsonArray(Patterns.Select(p => (JsonNode)p.Name).ToArray());
        return o;
    }

    /// <summary>The standard as its file and the project keep it (with <c>builtin</c> when asked).</summary>
    public JsonObject ToJson() => (JsonObject)Json.DeepClone();

    // ------------------------------------------------------------ the built-in ones

    static readonly string[] BuiltInIds = [LapinId, "iso-128-50", "din-1356", "ro-sr-iso-128-50", "bs-8888"];

    static readonly Lazy<IReadOnlyList<MaterialStandard>> Shipped = new(() => BuiltInIds.Select(id => Load(id)).ToList());

    static readonly Lazy<MaterialStandard> Lapin10 = new(() => Load(LapinId, "lapin-1.0"));

    static MaterialStandard Load(string id, string? file = null)
    {
        using var s = typeof(MaterialStandard).Assembly.GetManifestResourceStream($"Lapin.Drawing.MaterialStandards.{file ?? id}.json")
            ?? throw new InvalidOperationException($"The built-in material standard „{id}” is missing.");
        using var reader = new StreamReader(s);
        return Read(JsonNode.Parse(reader.ReadToEnd()) as JsonObject ?? throw new InvalidOperationException($"„{id}” is not a standard."), builtIn: true);
    }

    /// <summary>The standards Lapin ships, Lapin first (its current version, <see cref="LapinCurrent"/>).</summary>
    public static IReadOnlyList<MaterialStandard> BuiltIns => Shipped.Value;

    /// <summary>
    /// Lapin's own as it was first shipped (version 1.0, MaterialStandards/lapin-1.0.json: exactly the coded tables it replaced): the
    /// look of a project made before version 1.1 (decision 682; one without the mark <c>materialLookRev</c>) and of code that
    /// resolves a material with no project. A project made since draws by <see cref="LapinCurrent"/>.
    /// </summary>
    public static MaterialStandard Lapin => Lapin10.Value;

    /// <summary>
    /// Lapin's own now (version 1.1, MaterialStandards/lapin.json, decision 682): version 1.0 with its tones balanced (rigid
    /// insulation's grid at 1.5 and the blocks' crosshatch at 1.25 times, the concrete speckle at 0.75) and floor coverings' surfaces
    /// (parquet and laminate boards, carpet, sheet floors, poured floors, floor tiles 600×600). What a new project draws by, what the
    /// store's pack and the lists show.
    /// </summary>
    public static MaterialStandard LapinCurrent => Shipped.Value[0];

    public static MaterialStandard? FindBuiltIn(string? id) => id == null ? null : Shipped.Value.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
}
