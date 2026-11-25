using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// One text a domain writes on drawings, schedules and exports (a symbol, a prefix, a suffix, a heading): its key, what the
/// settings call it, Lapin's coded default (what was written before notations existed) and the <c>{…}</c> tokens its template
/// reads. A <see cref="Symbol"/> is a short text with no tokens (Ø, H, #), offered with a picker.
/// </summary>
public sealed record NotationKey(string Key, string Label, string Default, IReadOnlyList<string> Tokens, string Kind = NotationKey.Template)
{
    public const string Template = "template", Symbol = "symbol";
}

/// <summary>A country's or a standard's notation, as values over the defaults; indicative: what Lapin ships is checked by whoever uses it.</summary>
public sealed record NotationPreset(string Id, string Label, string Source, IReadOnlyDictionary<string, string> Values);

/// <summary>
/// A domain of notation (decision 864): a module's local-standard texts — rebar's diameter symbol and spacing, and next a tag's
/// prefix, a level's sign, a schedule's headings. Registered as a service by its module (<see cref="IModuleRegistry.Service{T}"/>);
/// the core describes and sets every one (<c>notation.describe</c>, <c>notation.set</c>).
/// </summary>
/// <remarks><paramref name="Samples"/>: a value for each token, for the settings' examples ("12" for <c>d</c>).</remarks>
public sealed record NotationDomain(string Id, string Label, IReadOnlyList<NotationKey> Keys, IReadOnlyList<NotationPreset> Presets, IReadOnlyDictionary<string, string>? Samples = null)
{
    public NotationKey? KeyOf(string key) => Keys.FirstOrDefault(k => k.Key == key);
}

/// <summary>
/// A domain's notation as a project resolves it: Lapin's defaults, the preset's values over them, the user's own over those.
/// A template is filled by <see cref="Fill"/>: its <c>{= …}</c> formulas, then its plain tokens.
/// </summary>
public sealed partial class Notation
{
    public NotationDomain Domain { get; }
    public string? Preset { get; }
    readonly IReadOnlyDictionary<string, string> _own;
    readonly Dictionary<string, string> _values;

    Notation(NotationDomain domain, string? preset, IReadOnlyDictionary<string, string> own)
    {
        Domain = domain;
        Preset = domain.Presets.Any(p => p.Id == preset) ? preset : null;
        _own = own;
        _values = domain.Keys.ToDictionary(k => k.Key, k => k.Default);
        if (Preset != null)
            foreach (var (k, v) in domain.Presets.First(p => p.Id == Preset).Values) if (_values.ContainsKey(k)) _values[k] = v;
        foreach (var (k, v) in own) if (_values.ContainsKey(k)) _values[k] = v;
    }

    /// <summary>Lapin's own: every key its coded default.</summary>
    public static Notation Default(NotationDomain domain) => new(domain, null, new Dictionary<string, string>());

    /// <summary>The user's values over the defaults (tests, previews), with a preset between when named.</summary>
    public static Notation Of(IReadOnlyDictionary<string, string> own, NotationDomain domain, string? preset = null) => new(domain, preset, own);

    /// <summary>The project's notation for a domain (<c>notations.&lt;domain&gt; = {preset?, values?}</c> on the project); none stored: the default.</summary>
    public static Notation Of(IModelView m, NotationDomain domain)
    {
        if (Spatial.Project(m)?.Attrs[Notations.Key]?[domain.Id] is not JsonObject o) return Default(domain);
        var own = new Dictionary<string, string>();
        if (o["values"] is JsonObject vo)
            foreach (var (k, v) in vo) if ((string?)v is { } s) own[k] = s;
        return new Notation(domain, (string?)o["preset"], own);
    }

    /// <summary>A key's text as resolved ("" for a key the domain does not have).</summary>
    public string this[string key] => _values.GetValueOrDefault(key, "");

    /// <summary>Whether the key's text is the user's own (not the default's nor the preset's).</summary>
    public bool Own(string key) => _own.ContainsKey(key) && Domain.KeyOf(key) != null;

    /// <summary>Every key Lapin's default: what was written before notations existed.</summary>
    public bool IsDefault => Domain.Keys.All(k => _values[k.Key] == k.Default);

    /// <summary>A key's template filled: its tokens by <paramref name="token"/>, a symbol key's value by its name.</summary>
    public string Format(string key, Func<string, string?> token) =>
        Fill(this[key], k => token(k) ?? (Domain.KeyOf(k) is { Kind: NotationKey.Symbol } ? this[k] : null));

    /// <summary>
    /// A template filled: its <c>{= …}</c> formulas (names read as values, <see cref="LiveText.Formulas"/>), then its plain
    /// <c>{token}</c>s; a token <paramref name="token"/> does not know stays as it is written.
    /// </summary>
    public static string Fill(string template, Func<string, string?> token)
    {
        if (template.Length == 0 || template.IndexOf('{') < 0) return template;
        var text = template.Contains("{=", StringComparison.Ordinal)
            ? LiveText.Formulas(template, n => token(n) is { } v ? XValue.Read(v) : null, null) : template;
        return TokenRe().Replace(text, m => token(m.Groups[1].Value) ?? m.Value);
    }

    /// <summary>The plain tokens a template names, formulas' names among them.</summary>
    public static IEnumerable<string> TokensOf(string template)
    {
        foreach (Match m in TokenRe().Matches(template)) yield return m.Groups[1].Value;
        var i = 0;
        while ((i = template.IndexOf("{=", i, StringComparison.Ordinal)) >= 0)
        {
            var close = LiveText.CloseOf(template, i + 2);
            if (close < 0) break;
            var (formula, _) = LiveText.SplitFormat(template[(i + 2)..close]);
            foreach (var n in Expr.Names(formula)) yield return n;
            i = close + 1;
        }
    }

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9_]*)\}")]
    private static partial Regex TokenRe();
}

/// <summary>
/// The project's notations (decision 864): symbols, prefixes and suffixes are local standards in engineering and architecture,
/// so a domain's texts are templates the user may change per project, from a country preset (indicative) or by hand; Lapin's
/// coded texts stay the default with the same output, and nothing is stored until the user changes something (an older
/// project needs no upgrade).
/// </summary>
public static class Notations
{
    public const string Key = "notations";
    public const int MaxTemplate = 60, MaxSymbol = 8;

    public static void Register(IModuleRegistry r)
    {
        // Every domain the product's modules bring, or one ({domain?}): its keys (default, value, tokens, kind, own), its presets, the preset chosen.
        r.Query("notation.describe", (q, a) =>
        {
            var only = (string?)a["domain"];
            return new JsonArray([.. q.Services.All<NotationDomain>().Where(d => only == null || d.Id == only).Select(d => (JsonNode)Describe(Notation.Of(q.Model, d)))]);
        });
        // {domain, preset?: id | null, values?: {key: text | null}, reset?: true}: the preset, the user's own values over it (null: the preset's
        // or the default's again), reset: back to Lapin's. One undo step.
        r.Command("notation.set", "Notation", (ctx, a) =>
        {
            var id = (string?)a["domain"] ?? "";
            var domain = ctx.Services.All<NotationDomain>().FirstOrDefault(d => d.Id == id)
                ?? throw new CommandException($"There is no notation „{id}” ({string.Join(", ", ctx.Services.All<NotationDomain>().Select(d => d.Id))}).");
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = project.CloneAttrs();
            var all = attrs[Key] as JsonObject ?? new JsonObject();
            var o = a["reset"] is JsonValue rv && rv.TryGetValue<bool>(out var reset) && reset ? new JsonObject() : all[id]?.DeepClone() as JsonObject ?? new JsonObject();
            if (a.ContainsKey("preset"))
            {
                var preset = (string?)a["preset"];
                if (string.IsNullOrEmpty(preset)) o.Remove("preset");
                else if (domain.Presets.All(p => p.Id != preset)) throw new CommandException($"The notation „{domain.Label}” has no preset „{preset}” ({string.Join(", ", domain.Presets.Select(p => p.Id))}).");
                else o["preset"] = preset;
            }
            if (a["values"] is JsonObject vo)
            {
                var values = o["values"] as JsonObject ?? new JsonObject();
                foreach (var (k, v) in vo)
                {
                    var key = domain.KeyOf(k) ?? throw new CommandException($"„{k}” is not a text of the notation „{domain.Label}”.");
                    if (v == null) { values.Remove(k); continue; }
                    var text = (string?)v ?? throw new CommandException($"{key.Label} is a text.");
                    Check(domain, key, text);
                    values[k] = text;
                }
                if (values.Count > 0) o["values"] = values; else o.Remove("values");
            }
            all = (JsonObject)all.DeepClone();
            if (o.Count > 0) all[id] = o; else all.Remove(id);
            if (all.Count > 0) attrs[Key] = all; else attrs.Remove(Key);
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });
    }

    /// <summary>A text refused when it is too long or names a token its key does not read (a symbol: no token at all).</summary>
    public static void Check(NotationDomain domain, NotationKey key, string text)
    {
        if (key.Kind == NotationKey.Symbol)
        {
            if (text.Length > MaxSymbol) throw new CommandException($"{key.Label} is {MaxSymbol} characters at most.");
            if (text.Contains('{')) throw new CommandException($"{key.Label} is a symbol, without tokens.");
            return;
        }
        if (text.Length > MaxTemplate) throw new CommandException($"{key.Label} is {MaxTemplate} characters at most.");
        var symbols = domain.Keys.Where(k => k.Kind == NotationKey.Symbol).Select(k => k.Key);
        foreach (var t in Notation.TokensOf(text))
            if (!key.Tokens.Contains(t) && !symbols.Contains(t))
                throw new CommandException($"{key.Label} reads {(key.Tokens.Count > 0 ? string.Join(", ", key.Tokens.Select(x => "{" + x + "}")) : "no token")}, not {{{t}}}.");
    }

    /// <summary>A notation as the settings dialog reads it.</summary>
    public static JsonObject Describe(Notation n) => new()
    {
        ["id"] = n.Domain.Id, ["label"] = n.Domain.Label, ["preset"] = n.Preset,
        ["presets"] = new JsonArray([.. n.Domain.Presets.Select(p => (JsonNode)new JsonObject { ["id"] = p.Id, ["label"] = p.Label, ["source"] = p.Source, ["indicative"] = true })]),
        ["keys"] = new JsonArray([.. n.Domain.Keys.Select(k => (JsonNode)new JsonObject
        {
            ["key"] = k.Key, ["label"] = k.Label, ["kind"] = k.Kind, ["default"] = k.Default, ["value"] = n[k.Key], ["own"] = n.Own(k.Key),
            ["example"] = n.Format(k.Key, t => n.Domain.Samples?.GetValueOrDefault(t)),
            ["tokens"] = new JsonArray([.. k.Tokens.Select(t => (JsonNode)t)]),
        })]),
    };
}
