using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform.Documents;

namespace Lapin.Platform;

/// <summary>
/// A field's value on one element as a schedule or a filter reads it (<see cref="ElementFields"/>): a number (in SI, as a
/// formula reads it: metres, m², m³, kg) and the text a tag writes, either or both; neither when the element has no such field.
/// </summary>
public readonly record struct FieldValue(double? Number, string? Text)
{
    public static readonly FieldValue Missing = new(null, null);
    public bool Exists => Number != null || !string.IsNullOrEmpty(Text);
    /// <summary>As a text: its own, else its number to three decimals.</summary>
    public string Shown => !string.IsNullOrEmpty(Text) ? Text : Number is { } n ? Expr.Format(n, null) : "";

    /// <summary>A text, a number too when it is one and nothing else („12”, „-3.5”).</summary>
    public static FieldValue OfText(string? s) => string.IsNullOrEmpty(s) ? Missing : new(StrictNumber(s), s);

    /// <summary>A text read as a number only when it is one and nothing else (a dot or a comma for decimals).</summary>
    public static double? StrictNumber(string? s) => Numbers.Parse(s, NumberMode.Strict);
}

/// <summary>
/// The fields of one element, read through the one field dictionary (<see cref="FieldCatalog"/>): a field as a formula reads it
/// (<see cref="ParamFormulas.Read"/>: its parameters, its attributes, its quantities, the property sets its file gave it, its
/// scopes', the office's aliases) when that is a number, else as a tag writes it (<see cref="TagText.Value"/>); a formula
/// <c>= …</c> (or <c>{= …}</c>) worked out by the formula engine; a template with fields in braces (<c>{storey.code}-{mark}</c>)
/// filled in. Its type and its quantities are found once, the quantities only when a field asks for one.
/// </summary>
public sealed class ElementFields(IQueryContext q, Entity e, string units)
{
    Entity? _type;
    bool _typed;
    Func<string, double?>? _quantities;

    public Entity Element => e;

    /// <summary>A part's own values (<see cref="ElementPart"/>), read before the element's: a field schedule listing parts reads each part through its element.</summary>
    public IReadOnlyDictionary<string, FieldValue>? Own { get; init; }

    FieldValue? OwnValue(string name) =>
        Own == null ? null : Own.TryGetValue(name, out var v) ? v : Own.FirstOrDefault(kv => string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) is { Key: not null } kv2 ? kv2.Value : null;
    public Entity? Type { get { if (!_typed) { _type = q.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType); _typed = true; } return _type; } }
    public Func<string, double?> Quantities => _quantities ??= ParamFormulas.Quantities(q, e.Id);

    public ElementFields(IQueryContext q, Entity e) : this(q, e, DraftingResources.Book(q.Model).Units) { }

    /// <summary>Whether a column's text is a formula (<c>= …</c>, <c>{= …}</c>): its body, else null.</summary>
    public static string? FormulaOf(string token)
    {
        var t = token.Trim();
        if (t.StartsWith("{=", StringComparison.Ordinal) && t.EndsWith('}') && t.IndexOf('}') == t.Length - 1) return t[2..^1].Trim();
        return t.StartsWith('=') ? t[1..].Trim() : null;
    }

    public FieldValue Value(string token)
    {
        var m = q.Model;
        var t = token.Trim();
        if (t.Length == 0) return FieldValue.Missing;
        if (FormulaOf(t) is { } formula) return Formula(formula);
        if (OwnValue(t) is { } own) return own;
        if (DerivedFields.Value(q, e, t) is { } queried) return FieldValue.OfText(queried);
        if (ParamFormulas.IsTemplate(t)) return FieldValue.OfText(TagText.Fill(t, m, e, null, units, Quantities));
        var x = ParamFormulas.Read(m, e, Type, Quantities, t);
        if (x is { Number: { } n } xv && (xv.Text == null || xv.IsNumericText)) return new(n, xv.Text);
        var text = TagText.Value(m, e, t, null, units, Quantities);
        if (text.Length > 0) return FieldValue.OfText(text);
        return x is { Text: { Length: > 0 } xt } ? FieldValue.OfText(xt) : FieldValue.Missing;
    }

    /// <summary>A formula worked out on the element (its names read as a formula parameter's, <see cref="ParamFormulas.Read"/>); missing when it does not read.</summary>
    public FieldValue Formula(string body)
    {
        // A template (a text with fields in braces, {storey.code}-{mark}), filled in as a tag writes it.
        if (ParamFormulas.IsTemplate(body)) return FieldValue.OfText(TagText.Fill(body, q.Model, e, null, units, Quantities));
        var (formula, format) = LiveText.SplitFormat(body);
        if (formula.Trim().Length == 0) return FieldValue.Missing;
        var (v, _) = Expr.TryEvalValue(formula, n => OwnValue(n) is { Exists: true } own ? new XValue(own.Number, own.Text) : ParamFormulas.Read(q.Model, e, Type, Quantities, n));
        if (v is not { } value) return FieldValue.Missing;
        if (value.Number is { } n && (value.Text == null || value.IsNumericText)) return new(n, format != null ? Expr.Format(n, format) : value.Text);
        return FieldValue.OfText(value.AsText);
    }
}

/// <summary>The fields across the model (for a schedule's or a filter's pickers) and one field's values.</summary>
public static partial class FieldCatalog
{
    sealed class Seen(string group, string token, string label)
    {
        public readonly string Group = group, Token = token, Label = label;
        public string? Set, Description;
        public int Count, Numbers, Bools, Texts;
        public readonly List<string> Examples = [];
        public void Saw(string? text, char kind, int examples)
        {
            Count++;
            if (kind == 'n') Numbers++; else if (kind == 'b') Bools++; else Texts++;
            if (text is { Length: > 0 } t && Examples.Count < examples && !Examples.Contains(t)) Examples.Add(t);
        }
        public string Kind => Bools > 0 && Numbers == 0 && Texts == 0 ? "bool" : Numbers > 0 && Texts == 0 && Bools == 0 ? "number" : "text";
    }

    /// <summary>A stored value's text and kind (n number, b yes/no, t text).</summary>
    static (string Text, char Kind) Stored(JsonNode? v)
    {
        if (v is JsonObject o && o["value"] is JsonValue inner) v = inner;
        if (v is JsonValue b && b.TryGetValue<bool>(out var flag)) return (flag ? "yes" : "no", 'b');
        if (v is JsonValue s && s.TryGetValue<string>(out var text)) return (text, FieldValue.StrictNumber(text) != null ? 'n' : 't');
        if (Json.Number(v) is { } n) return (n.ToString("0.###", CultureInfo.InvariantCulture), 'n');
        return (v?.ToJsonString() ?? "", 't');
    }

    static (string Text, char Kind) Read(FieldValue v) => (v.Shown, v.Number != null ? 'n' : v.Shown is "yes" or "no" ? 'b' : 't');

    /// <summary>
    /// The fields the model's elements (or a filter's) have, grouped by where they come from — Lapin's own (every element's,
    /// its attributes, what modules work out), its type's, its quantities, its parameters, each property set its file gave it,
    /// the office's aliases, its scopes' —, each with its token, a label, its kind (number, text, bool), a few values and how
    /// many elements have it. Stored fields are counted on every element; quantities, worked-out fields, aliases and scopes on
    /// a sample of at most <paramref name="sample"/> elements spread over the list (the group says so).
    /// </summary>
    public static JsonObject Across(IQueryContext q, ElementFilter? filter, int sample = 2000, int examples = 5)
    {
        var m = q.Model;
        var units = DraftingResources.Book(m).Units;
        var elements = ElementFilter.Elements(q, filter, units);
        var stride = Math.Max(1, (int)Math.Ceiling(elements.Count / (double)Math.Max(1, sample)));
        var fields = new Dictionary<string, Seen>(StringComparer.OrdinalIgnoreCase);
        Seen Field(string group, string token, string label, string? set = null, string? description = null)
        {
            var key = group + "\u001f" + token;
            if (!fields.TryGetValue(key, out var s)) fields[key] = s = new Seen(group, token, label) { Set = set, Description = description };
            return s;
        }
        var described = new Dictionary<string, ClassDescriptor?>();
        var typeFields = new Dictionary<string, List<(string Token, string Label, string Text, char Kind)>>();
        var aliases = FieldAliases.All(m);
        var sampled = 0;

        for (var i = 0; i < elements.Count; i++)
        {
            var e = elements[i];
            var inSample = i % stride == 0;
            var reader = new ElementFields(q, e, units);
            var once = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Once(string group, string token, string label, string text, char kind, string? set = null, string? description = null)
            {
                if (once.Add(group + "\u001f" + token)) Field(group, token, label, set, description).Saw(text, kind, examples);
            }

            // Lapin's own: the fields every element has, its attributes.
            foreach (var (key, description) in Builtins)
                if (inSample) { var v = reader.Value(key); Once("lapin", key, key, v.Shown, 't', null, description); }
                else Field("lapin", key, key, null, description).Count++;
            if (!described.TryGetValue(e.Class, out var d)) described[e.Class] = d = q.Describe(e.Class);
            foreach (var (key, v) in e.Attrs)
            {
                if (v is not JsonValue || Internal.Contains(key) || TagText.Builtin.Contains(key, StringComparer.OrdinalIgnoreCase)) continue;
                if (v.GetValueKind() == System.Text.Json.JsonValueKind.String && (string?)v is { } sv && (GlobalId.IsValid(sv) || sv.Length > 200)) continue;
                var (text, kind) = Stored(v);
                Once("lapin", key, d?.Fields.FirstOrDefault(f => f.Key == key)?.Label ?? key, text, kind, null, "An attribute of the element");
            }

            // Its parameters.
            foreach (var p in StoredParams(e))
                if (((string?)p["name"])?.Trim() is { Length: > 0 } name)
                {
                    var set = Params.SetOf(p);
                    var (text, kind) = ParamFormulas.IsFormula(p) ? (inSample ? Read(reader.Value(set is { } s0 ? $"{s0}.{name}" : name)) : ("", 't')) : Stored(p["value"]);
                    if ((string?)p["kind"] is "number" or "length" or "area" or "angle" or "volume" && text.Length > 0) kind = 'n';
                    Once("params", set is { } s1 ? $"{s1}.{name}" : name, name, text, kind, set, ParamFormulas.IsFormula(p) ? "Your parameter, worked out by its formula" : "Your parameter");
                }

            // The parameters its own definition names (a placed part's), worked out on every ask.
            foreach (var nf in DerivedFields.NamedOf(m, e))
                Once("params", nf.Key, nf.Label, nf.Shown, nf.Kind == "text" ? 't' : nf.Kind == "bool" ? 'b' : 'n', nf.Set, nf.Description ?? "A parameter of its part, worked out on every ask");

            // Its type's: its name and mark, its attributes and parameters (scope type.).
            var type = reader.Type;
            if (type != null)
            {
                if (!typeFields.TryGetValue(type.Id, out var tf))
                {
                    tf = [("type.name", "name", type.Name, 't'), ("type.mark", "mark", MarkRules.TypeMark(q.Model, type) ?? "", 't')];
                    var td = q.Describe(type.Class);
                    foreach (var (key, v) in type.Attrs)
                    {
                        if (v is not JsonValue || Internal.Contains(key) || TagText.Builtin.Contains(key, StringComparer.OrdinalIgnoreCase)) continue;
                        if (v.GetValueKind() == System.Text.Json.JsonValueKind.String && (string?)v is { } sv && (GlobalId.IsValid(sv) || sv.Length > 200)) continue;
                        var (text, kind) = Stored(v);
                        tf.Add(($"type.{key}", td?.Fields.FirstOrDefault(f => f.Key == key)?.Label ?? key, text, kind));
                    }
                    foreach (var p in StoredParams(type))
                        if (((string?)p["name"])?.Trim() is { Length: > 0 } name)
                        {
                            var (text, kind) = Stored(p["value"]);
                            tf.Add((Params.SetOf(p) is { } set ? $"type.{set}.{name}" : $"type.{name}", name, text, kind));
                        }
                    typeFields[type.Id] = tf;
                }
                foreach (var (token, label, text, kind) in tf) if (text.Length > 0 || token == "type.name") Once("type", token, label, text, kind);
            }

            // The property sets its file gave it (and its type).
            foreach (var x in new[] { e, type })
                if (x?.Attrs["properties"] is JsonObject sets)
                    foreach (var (set, props) in sets)
                        if (props is JsonObject po)
                            foreach (var (name, v) in po)
                            {
                                if (v is null) continue;
                                var (text, kind) = Stored(v);
                                Once("pset:" + set, $"{set}.{name}", name, text, kind, set, "From the file it came from");
                            }

            if (!inSample) continue;
            sampled++;
            // Its quantities.
            foreach (var s in q.Quantities(e.Id))
                foreach (var x in s.Quantities)
                    Once("quantities", x.Name, x.Label ?? x.Name, x.Value.ToString("0.###", CultureInfo.InvariantCulture), 'n', s.Name, "A quantity (SI: m, m², m³, kg)");
            // What its module works out.
            foreach (var df in DerivedFields.Of(e.Class))
                if (reader.Value(df.Key) is { Exists: true } dv) Once("lapin", df.Key, df.Label, dv.Shown, Read(dv).Kind, null, "Worked out on every ask, never stored");
            foreach (var df in DerivedFields.QueriedOf(e.Class))
                if (reader.Value(df.Key) is { Exists: true } dv) Once("lapin", df.Key, df.Label, dv.Shown, Read(dv).Kind, null, "Worked out on every ask, never stored");
            // The office's aliases.
            foreach (var a in aliases)
                if (reader.Value(a.Name) is { Exists: true } av) Once("aliases", a.Name, a.Name, av.Shown, Read(av).Kind, null, a.Description ?? "The first of: " + string.Join(", ", a.Sources));
            // Its scopes': their names.
            foreach (var scope in ParamScopes.Names)
            {
                if (scope is "type" or "project" || ParamScopes.Of(m, e, scope) is not { } owner || owner.Id == e.Id) continue;
                Once("scopes", $"{scope}.name", $"{ScopeLabel(scope)}: name", owner.Name, 't', null, "Its " + scope + "'s name");
            }
        }

        // A property's name alone, when only one set across these elements has it and Lapin has no field of that name.
        var lapinNames = fields.Values.Where(f => !f.Group.StartsWith("pset:", StringComparison.Ordinal)).Select(f => TagText.Squash(f.Token)).ToHashSet();
        var short_ = fields.Values.Where(f => f.Group.StartsWith("pset:", StringComparison.Ordinal))
            .GroupBy(f => TagText.Squash(f.Label)).Where(g => g.Count() == 1 && !lapinNames.Contains(g.Key)).Select(g => g.Single()).ToHashSet();

        (string Id, string Label, bool Sampled) GroupOf(string id) => id switch
        {
            "lapin" => (id, "Lapin", false),
            "type" => (id, "Its type", false),
            "quantities" => (id, "Its quantities", true),
            "params" => (id, "Its parameters", false),
            "aliases" => (id, "Your office's names (aliases)", true),
            "scopes" => (id, "Its storey, building, group…", true),
            _ => (id, id[5..], false),
        };
        int Rank(string id) => id switch { "lapin" => 0, "type" => 1, "quantities" => 2, "params" => 3, "aliases" => 4, "scopes" => 6, _ => 5 };
        var builtinOrder = Builtins.Select(b => b.Key).ToList();
        var groups = new JsonArray();
        foreach (var g in fields.Values.GroupBy(f => f.Group).OrderBy(g => Rank(g.Key)).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var (id, label, isSampled) = GroupOf(g.Key);
            var list = g.OrderBy(f => builtinOrder.IndexOf(f.Token) is var bi && bi >= 0 ? bi : int.MaxValue)
                .ThenBy(f => f.Label, StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering)).ToList();
            groups.Add((JsonNode)new JsonObject
            {
                ["id"] = id, ["label"] = label, ["sampled"] = isSampled && sampled < elements.Count,
                ["set"] = g.Key.StartsWith("pset:", StringComparison.Ordinal) ? g.Key[5..] : null,
                ["fields"] = new JsonArray([.. list.Select(f => (JsonNode)new JsonObject
                {
                    ["token"] = f.Token, ["label"] = f.Label, ["kind"] = f.Kind, ["count"] = f.Count,
                    ["examples"] = new JsonArray([.. f.Examples.Select(x => (JsonNode)x)]),
                    ["set"] = f.Set, ["short"] = short_.Contains(f) ? f.Label : null, ["description"] = f.Description,
                })]),
            });
        }
        return new JsonObject { ["elements"] = elements.Count, ["sample"] = sampled, ["groups"] = groups };
    }

    /// <summary>
    /// One field's values across the model's elements (or a filter's), for a filter's value picker: each distinct value with
    /// how many elements have it, numbers in their order, texts as a person sorts them; at most <paramref name="limit"/>, the
    /// first <paramref name="max"/> elements read.
    /// </summary>
    public static JsonObject Values(IQueryContext q, string field, ElementFilter? filter, int limit = 200, int max = 100_000)
    {
        var units = DraftingResources.Book(q.Model).Units;
        var elements = ElementFilter.Elements(q, filter, units);
        var counts = new Dictionary<string, (int Count, double? Number)>(StringComparer.OrdinalIgnoreCase);
        int with = 0, missing = 0;
        // A facet (@type, @material…, @text:<field>): each of its texts, a number when it starts with one.
        var facets = ElementFacets.Is(field) && ElementFacets.Known(field) ? new ElementFilter.Context(q, units: units) : null;
        foreach (var e in elements.Take(max))
        {
            if (facets != null)
            {
                var texts = ElementFacets.Values(facets, e, field);
                if (texts.Count == 0) { missing++; continue; }
                with++;
                // A class by its IFC name, a phase by its name: one entry each, not its other spellings.
                foreach (var t in field == "@class" ? texts.Take(1) : field is "@phase" or "@demolished" ? texts.TakeLast(1) : texts)
                    counts[t] = counts.TryGetValue(t, out var tc) ? (tc.Count + 1, tc.Number) : (1, FieldValue.StrictNumber(t));
                continue;
            }
            var v = new ElementFields(q, e, units).Value(field);
            if (!v.Exists) { missing++; continue; }
            with++;
            var key = v.Shown;
            counts[key] = counts.TryGetValue(key, out var c) ? (c.Count + 1, c.Number) : (1, v.Number);
        }
        var numeric = counts.Count > 0 && counts.Values.All(c => c.Number != null);
        var ordered = numeric ? counts.OrderBy(kv => kv.Value.Number) : counts.OrderBy(kv => kv.Key, StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering));
        return new JsonObject
        {
            ["field"] = field, ["kind"] = numeric ? "number" : "text", ["elements"] = elements.Count, ["count"] = with, ["missing"] = missing,
            ["values"] = new JsonArray([.. ordered.Take(limit).Select(kv => (JsonNode)new JsonObject { ["value"] = kv.Key, ["count"] = kv.Value.Count })]),
            ["truncated"] = counts.Count > limit, ["sampled"] = elements.Count > max,
        };
    }

    static int Int(JsonNode? n, int fallback, int min, int max) => Json.Number(n) is { } v ? (int)Math.Clamp(v, min, max) : fallback;

    static void RegisterModel(IModuleRegistry r)
    {
        // The fields across the model or a filter's elements: {filter?, sample?, examples?} → {elements, sample, groups: [{id, label, set, sampled, fields: [{token, label, kind, count, examples, set, short, description}]}]}.
        r.Query("fields.model", (q, a) => Across(q, ElementFilter.Check(a["filter"]), Int(a["sample"], 2000, 10, 1_000_000), Int(a["examples"], 5, 1, 50)));

        // One field's values: {field, filter?, limit?, max?} → {field, kind, elements, count, missing, values: [{value, count}], truncated, sampled}.
        r.Query("fields.values", (q, a) =>
        {
            var field = ((string?)a["field"])?.Trim() is { Length: > 0 } f ? f : throw new CommandException("Which field?");
            return Values(q, field, ElementFilter.Check(a["filter"]), Int(a["limit"], 200, 1, 10_000), Int(a["max"], 100_000, 1, 10_000_000));
        });

        // One element's value of each field asked (a schedule column's preview): {id, fields: [...]} → {values: [{field, value, number}]}.
        r.Query("fields.read", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which element?");
            var reader = new ElementFields(q, e);
            return new JsonObject
            {
                ["values"] = new JsonArray([.. (a["fields"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>().Select(f =>
                {
                    var v = reader.Value(f);
                    return (JsonNode)new JsonObject { ["field"] = f, ["value"] = v.Exists ? v.Shown : null, ["number"] = v.Number };
                })]),
            };
        });
    }
}
