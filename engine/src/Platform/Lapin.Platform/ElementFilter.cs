using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A condition on a field (<c>{field, op, value}</c>), Lapin's one condition (audit D3): a field schedule's, a task's rule's, a
/// view filter's, Quick select's and a clash search set's. Its field is one of the element's fields (<see cref="ElementFields"/>:
/// numbers in SI, as a formula reads them), or one of its facets (<see cref="ElementFacets"/>: <c>@type</c>, <c>@storey</c>,
/// <c>@material</c>…, several values for its materials), or a field read as a tag writes it (<c>@text:Fire rating</c>, a
/// number taken from the text). Numbers are compared as numbers, texts case aside. <see cref="Values"/> holds the list of
/// <c>one of</c> / <c>none of</c> and the two ends of <c>between</c> (either empty: open).
/// </summary>
public sealed record FieldCondition(string Field, string Op, string? Value, IReadOnlyList<string>? Values = null)
{
    public static readonly string[] Ops = ["=", "≠", "contains", "not contains", "starts with", "<", "≤", ">", "≥", "between", "one of", "none of", "exists", "missing"];

    /// <summary>An operator as it may be written (<c>!=</c>, <c>&lt;=</c>, <c>startsWith</c>, a view filter's <c>greater</c>…) by its name; null when it is none.</summary>
    public static string? Normal(string? op) => op?.Trim().ToLowerInvariant() switch
    {
        "=" or "==" or "is" or "equals" => "=",
        "≠" or "!=" or "<>" or "is not" or "not" or "notequals" or "isnot" => "≠",
        "contains" or "has text" => "contains",
        "not contains" or "notcontains" or "does not contain" => "not contains",
        "starts with" or "startswith" or "starts" or "begins with" or "beginswith" => "starts with",
        "<" or "less" => "<", "≤" or "<=" or "atmost" or "at most" => "≤", ">" or "greater" => ">", "≥" or ">=" or "atleast" or "at least" => "≥",
        "between" => "between",
        "one of" or "oneof" or "isoneof" or "in" => "one of",
        "none of" or "noneof" or "isnotanyof" or "not in" => "none of",
        "exists" or "has" or "has value" or "hasvalue" => "exists",
        "missing" or "empty" or "not exists" or "is empty" or "hasnovalue" => "missing",
        _ => null,
    };

    /// <summary>Whether an operator compares with no value (<c>exists</c>, <c>missing</c>).</summary>
    public static bool TakesNoValue(string op) => op is "exists" or "missing";
    /// <summary>Whether an operator takes a list (<c>one of</c>, <c>none of</c>) or two ends (<c>between</c>).</summary>
    public static bool TakesList(string op) => op is "one of" or "none of" or "between";
    /// <summary>Whether an operator holds where its positive one does not (<c>≠</c>, <c>not contains</c>, <c>none of</c>, <c>missing</c>).</summary>
    public static bool IsNegative(string op) => op is "≠" or "not contains" or "none of" or "missing";
    static string Positive(string op) => op switch { "≠" => "=", "not contains" => "contains", "none of" => "one of", "missing" => "exists", _ => op };

    static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;

    /// <summary>What it compares with: its list, else its value (none for exists and missing).</summary>
    public IReadOnlyList<string> Wanted => Values ?? (Value != null ? [Value] : []);

    /// <summary>The first number in a text, by the one reader (<see cref="Numbers.Parse"/>: „H=600” 600, „1,200” 1.2, „−3” −3).</summary>
    static double? First(string? text) => Numbers.Parse(text, NumberMode.First);

    /// <summary>The two ends of a range (either null: open): its list, or a value written "min..max".</summary>
    public (double? Lo, double? Hi) Range()
    {
        if (Values is { } v) return (v.Count > 0 ? First(v[0]) : null, v.Count > 1 ? First(v[1]) : null);
        var s = Value ?? "";
        var k = s.IndexOf("..", StringComparison.Ordinal);
        if (k < 0) return (First(s), First(s));
        return (First(s[..k]), First(s[(k + 2)..]));
    }

    static bool InRange(double n, (double? Lo, double? Hi) r)
    {
        if (r.Lo == null && r.Hi == null) return false;
        var (lo, hi) = r.Lo is { } a && r.Hi is { } b && a > b ? (r.Hi, r.Lo) : r;
        return (lo is not { } l || n > l || Numbers.Near(n, l)) && (hi is not { } h || n < h || Numbers.Near(n, h));
    }

    /// <summary>Whether it holds on one value of a field (<see cref="ElementFields"/>).</summary>
    public bool Holds(FieldValue v)
    {
        switch (Op)
        {
            case "exists": return v.Exists;
            case "missing": return !v.Exists;
            case "one of": return Wanted.Any(w => (this with { Op = "=", Value = w, Values = null }).Holds(v));
            case "none of": return !Wanted.Any(w => (this with { Op = "=", Value = w, Values = null }).Holds(v));
            case "between": return v.Number is { } x && InRange(x, Range());
        }
        var want = Value ?? "";
        if (!v.Exists) return Op is "≠" or "not contains" && want.Length > 0;
        var shown = v.Shown;
        switch (Op)
        {
            case "contains": return Compare.IndexOf(shown, want, CompareOptions.IgnoreCase) >= 0;
            case "not contains": return Compare.IndexOf(shown, want, CompareOptions.IgnoreCase) < 0;
            case "starts with": return Compare.IsPrefix(shown, want, CompareOptions.IgnoreCase);
        }
        int c;
        if (v.Number is { } n && FieldValue.StrictNumber(want) is { } w)
            c = Numbers.Near(n, w) ? 0 : n.CompareTo(w);
        else
            c = Op is "=" or "≠" ? (string.Equals(shown.Trim(), want.Trim(), StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                : string.Compare(shown, want, CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);
        return Op switch { "=" => c == 0, "≠" => c != 0, "<" => c < 0, "≤" => c <= 0, ">" => c > 0, "≥" => c >= 0, _ => false };
    }

    /// <summary>
    /// Whether it holds on the texts of a facet (several for its materials) or of a field read as text: a positive operator
    /// when one of them holds it, a negative one when none holds its positive (<c>≠ Brick</c>: no material is Brick). Equal
    /// texts case aside, or numbers the texts start with ("3000 mm" = 3000); the order and ranges by the first number in each
    /// ("H=600" is 600).
    /// </summary>
    public bool HoldsAny(IReadOnlyList<string> texts)
    {
        if (IsNegative(Op)) return !(this with { Op = Positive(Op) }).HoldsAny(texts);
        if (Op == "exists") return texts.Any(t => t.Length > 0);
        foreach (var t in texts)
            if (HoldsText(t)) return true;
        return false;
    }

    bool HoldsText(string t)
    {
        switch (Op)
        {
            case "=" or "one of": return Wanted.Any(w => EqualTexts(t, w));
            case "contains": return Wanted.Any(w => Compare.IndexOf(t, w, CompareOptions.IgnoreCase) >= 0);
            case "starts with": return Wanted.Any(w => Compare.IsPrefix(t.TrimStart(), w.TrimStart(), CompareOptions.IgnoreCase));
            case "between": return First(t) is { } x && InRange(x, Range());
        }
        if (First(t) is not { } a || First(Value) is not { } b) return false;
        var c = Numbers.Near(a, b) ? 0 : a.CompareTo(b);
        return Op switch { "<" => c < 0, "≤" => c <= 0, ">" => c > 0, "≥" => c >= 0, _ => false };
    }

    static bool EqualTexts(string t, string w) =>
        string.Equals(t.Trim(), w.Trim(), StringComparison.OrdinalIgnoreCase)
        || Numbers.Parse(t, NumberMode.Leading) is { } a && Numbers.Parse(w, NumberMode.Leading) is { } b && Numbers.Near(a, b);

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["field"] = Field, ["op"] = Op };
        if (TakesNoValue(Op)) return o;
        if (Values != null) o["values"] = new JsonArray([.. Values.Select(v => (JsonNode)v)]);
        else if (Value != null) o["value"] = Value;
        return o;
    }

    /// <summary>The condition in words, as a task's rule or a schedule's filter is described („Pset_Firma.Etapa = E2”, „Sector exists”).</summary>
    public override string ToString() =>
        TakesNoValue(Op) ? $"{Field} {Op}" : Op == "between" && Range() is var (lo, hi) ? $"{Field} {Op} {lo}..{hi}" : $"{Field} {Op} {string.Join(", ", Wanted)}";

    /// <summary>Conditions as stored (<c>[{field, op, value | values}]</c>); those that do not read are left out.</summary>
    public static List<FieldCondition> ReadAll(JsonNode? n) => (n as JsonArray ?? []).OfType<JsonObject>().Select(Read).OfType<FieldCondition>().ToList();

    /// <summary>A condition as stored; null when it does not read.</summary>
    public static FieldCondition? Read(JsonObject c) =>
        ((string?)c["field"])?.Trim() is { Length: > 0 } f && Normal((string?)c["op"]) is { } op
            ? new FieldCondition(f, op, ValueText(c["value"]), c["values"] is JsonArray vs ? vs.Select(ValueText).Select(x => x ?? "").ToList() : null)
            : null;

    /// <summary>A condition's value as a text: a number written with a point, yes or no for a yes/no.</summary>
    public static string? ValueText(JsonNode? v) => v switch
    {
        null => null,
        JsonValue s when s.TryGetValue<string>(out var t) => t,
        JsonValue b when b.TryGetValue<bool>(out var x) => x ? "yes" : "no",
        _ => Json.Number(v) is { } n ? n.ToString("R", CultureInfo.InvariantCulture) : v.ToJsonString(),
    };

    /// <summary>Conditions as a command gives them, checked: each with a field, an operator Lapin knows and a value when it needs one; at most <paramref name="max"/>.</summary>
    public static List<FieldCondition> CheckAll(JsonNode? n, int max = ElementFilter.MaxConditions)
    {
        if (n is null) return [];
        if (n is not JsonArray conditions) throw new CommandException("Conditions are a list: [{field, op, value}].");
        if (conditions.Count > max) throw new CommandException($"At most {max} conditions.");
        foreach (var c in conditions) Check(c);
        return ReadAll(n);
    }

    /// <summary>One condition as a command gives it, checked.</summary>
    public static FieldCondition Check(JsonNode? c)
    {
        if (c is not JsonObject co || ((string?)co["field"])?.Trim() is not { Length: > 0 } field) throw new CommandException("A condition names a field: {field, op, value}.");
        if (Normal((string?)co["op"]) is not { } op) throw new CommandException($"„{field}”: a condition's operator is one of {string.Join(", ", Ops)}.");
        if (!TakesNoValue(op) && co["value"] is null && co["values"] is not JsonArray) throw new CommandException($"„{field} {op}”: which value?");
        if (ElementFacets.Is(field) && !ElementFacets.Known(field)) throw new CommandException($"„{field}” is not a facet of an element ({string.Join(", ", ElementFacets.Names.Select(x => "@" + x))}, @text:<field>).");
        if (ElementFields.FormulaOf(field) is { } f) ParamFormulas.Check(f);
        return Read(co)!;
    }
}

/// <summary>Conditions of which all hold, or any (<c>{match: all | any, conditions, nested}</c>), and groups of them inside, to any depth (a clash search set's groups).</summary>
public sealed record ConditionGroup(bool Any, IReadOnlyList<FieldCondition> Conditions, IReadOnlyList<ConditionGroup> Nested)
{
    public bool IsEmpty => Conditions.Count == 0 && Nested.All(g => g.IsEmpty);

    /// <summary>Whether it holds, each condition tested by <paramref name="holds"/>: all of them (any); an empty group holds, and is left out of its parent's.</summary>
    public bool Holds(Func<FieldCondition, bool> holds)
    {
        var any = false;
        foreach (var c in Conditions)
        {
            any = true;
            if (holds(c) == Any) return Any;
        }
        foreach (var g in Nested)
        {
            if (g.IsEmpty) continue;
            any = true;
            if (g.Holds(holds) == Any) return Any;
        }
        return !any || !Any;
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["match"] = Any ? "any" : "all", ["conditions"] = new JsonArray([.. Conditions.Select(c => (JsonNode)c.ToJson())]) };
        if (Nested.Count > 0) o["nested"] = new JsonArray([.. Nested.Select(g => (JsonNode)g.ToJson())]);
        return o;
    }

    public static ConditionGroup Read(JsonObject? o, bool check = false, int depth = 0)
    {
        if (depth > 8) throw new CommandException("Groups of conditions go at most 8 deep.");
        var conditions = check ? FieldCondition.CheckAll(o?["conditions"]) : FieldCondition.ReadAll(o?["conditions"]);
        return new((string?)o?["match"] == "any", conditions, (o?["nested"] as JsonArray ?? []).Select(g => Read(g as JsonObject, check, depth + 1)).ToList());
    }

    /// <summary>Every condition in it, its groups' too.</summary>
    public IEnumerable<FieldCondition> All() => Conditions.Concat(Nested.SelectMany(g => g.All()));
}

/// <summary>
/// The facets an element is filtered by besides its fields, as a condition's field names them (<c>@type</c>…): its IFC class
/// (with its name without Ifc and its label), its drawing category, its type's name (a reference's file type too), its name,
/// its materials' names, its storey's name (a door's or a window's on its wall's, <see cref="Spatial.StoreyOf(IModelView, Entity, bool)"/>),
/// its group's, its link's, the phase it is created in (its id and name; the default one when it has none) and demolished
/// in, its predefined type, whether it is structural (yes or no, <see cref="StructuralUsage.FieldValue"/>); and <c>@text:&lt;field&gt;</c>, a field read as a tag writes it (<see cref="TagText.Value"/>, in
/// the project's units) with the element's own attribute of that name (an object's member too) as it is stored.
/// </summary>
public static class ElementFacets
{
    public const string TextPrefix = "@text:";
    public static readonly string[] Names = ["class", "category", "type", "name", "material", "storey", "group", "link", "phase", "demolished", "predefinedType", "structural"];

    public static bool Is(string field) => field.StartsWith('@');
    public static bool Known(string field) =>
        field.StartsWith(TextPrefix, StringComparison.Ordinal) ? field[TextPrefix.Length..].Trim().Length > 0 : Is(field) && Names.Contains(field[1..]);

    public static IReadOnlyList<string> Values(ElementFilter.Context ctx, Entity e, string field)
    {
        var m = ctx.Query.Model;
        if (field.StartsWith(TextPrefix, StringComparison.Ordinal))
        {
            var name = field[TextPrefix.Length..].Trim();
            var o = new List<string>(2);
            if (TagText.Value(m, e, name, null, ctx.Units, ctx.QuantitiesOf(e)) is { Length: > 0 } v) o.Add(v);
            if (StoredValue(e, name) is { Length: > 0 } s && !o.Contains(s)) o.Add(s);
            return o;
        }
        static IReadOnlyList<string> One(string? s) => s is { Length: > 0 } ? [s] : [];
        switch (field[1..])
        {
            case "class":
                var label = ctx.Query.Describe(e.Class)?.Label;
                return new[] { e.Class, e.Class.StartsWith("Ifc", StringComparison.Ordinal) ? e.Class[3..] : null, label }.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            case "category": return One(LineStyles.CategoryOf(e.Class));
            case "type": return One(ReferenceTypes.NameOf(m, e));
            case "name": return One(e.Name);
            case "material": return ctx.MaterialsOf(e).Select(id => m.Get(id)?.Name).OfType<string>().Where(x => x.Length > 0).Distinct().ToList();
            case "storey": return One(Spatial.StoreyOf(m, e, followHost: true)?.Name);
            case "group": return One(Groups.Of(m, e.Id)?.Name);
            case "link": return One(Links.Of(m, e)?.Name ?? e.Str(Links.Key));
            case "phase": return ctx.PhaseNames(e.Str("phaseCreated") ?? ctx.DefaultPhase);
            case "demolished": return e.Str("phaseDemolished") is { } d ? ctx.PhaseNames(d) : [];
            case "predefinedType": return One(e.PredefinedType);
            // Whether it bears load, yes or no (docs/plan-structural-usage.md: the Structural tick, else the views' rule); none for a door, a stair.
            case "structural": return One(StructuralUsage.FieldValue(m, e));
        }
        return [];
    }

    /// <summary>An attribute's value as stored: the element's own of that name, or one of that name in an object of its attributes (case aside).</summary>
    public static string? StoredValue(Entity e, string name)
    {
        static string? Text(JsonNode? n) => n switch
        {
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            JsonValue v when v.TryGetValue<bool>(out var b) => b ? "true" : "false",
            JsonValue v when Json.Number(v) is { } d => d.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };
        foreach (var (k, v) in e.Attrs) if (string.Equals(k, name, StringComparison.OrdinalIgnoreCase) && Text(v) is { } s) return s;
        foreach (var (_, v) in e.Attrs)
            if (v is JsonObject o)
                foreach (var (k, w) in o)
                    if (string.Equals(k, name, StringComparison.OrdinalIgnoreCase) && Text(w) is { } s) return s;
        return null;
    }
}

/// <summary>
/// Which elements a filter takes (docs/plan-user-extensibility.md §11–§12, audit D3), Lapin's one element filter: a field
/// schedule's, a task's rule's (<c>Lapin.Planning.TaskRule</c>), and under the view filters, Quick select and the clash search
/// sets. Its dimensions — the IFC classes (a supertype takes its kinds), the types, the storeys, the materials (an element
/// with any of them), the groups, the phases created in and demolished in — each by its id or its name, so a template
/// brought into another project finds its own; then its conditions on any field or facet, all of which must hold (or any,
/// <see cref="Any"/>), with groups of them inside (<see cref="Nested"/>). A dimension or a block of conditions left empty lets
/// everything through.
/// </summary>
public sealed record ElementFilter(IReadOnlyList<string> Classes, IReadOnlyList<string> Types, IReadOnlyList<string> Storeys,
    IReadOnlyList<string> Materials, IReadOnlyList<string> Groups, IReadOnlyList<string> Phases, IReadOnlyList<FieldCondition> Conditions)
{
    public static readonly string[] Keys = ["classes", "types", "storeys", "materials", "groups", "phases", "demolished"];
    public static readonly ElementFilter All = new([], [], [], [], [], [], []);
    public const int MaxConditions = 32;

    /// <summary>The phases its elements are demolished in; empty: demolished or not.</summary>
    public IReadOnlyList<string> Demolished { get; init; } = [];
    /// <summary>Whether any of its conditions (and groups) is enough, rather than all.</summary>
    public bool Any { get; init; }
    /// <summary>Groups of conditions inside its block of conditions.</summary>
    public IReadOnlyList<ConditionGroup> Nested { get; init; } = [];

    /// <summary>Its block of conditions as one group.</summary>
    public ConditionGroup Block => new(Any, Conditions, Nested);

    /// <summary>What is never a schedule's row: openings, drawings over the model, grids, shared bodies, the file's type records.</summary>
    static readonly HashSet<string> NotListed =
    [
        IfcClass.OpeningElement, IfcClass.Annotation, IfcClass.Grid, "IfcVirtualElement", StoredBodies.SharedClass, IfcSource.RecordClass,
    ];

    static List<string> Strings(JsonNode? n) => n is JsonArray a ? a.Select(x => ((string?)x)?.Trim()).OfType<string>().Where(s => s.Length > 0).Distinct().ToList() : [];

    IEnumerable<(string Key, IReadOnlyList<string> Values)> Lists() =>
        [("classes", Classes), ("types", Types), ("storeys", Storeys), ("materials", Materials), ("groups", Groups), ("phases", Phases), ("demolished", Demolished)];

    public bool IsEmpty => Lists().All(l => l.Values.Count == 0) && Block.IsEmpty;

    /// <summary>A filter as stored; conditions that do not read are left out. Null node: none.</summary>
    public static ElementFilter? Read(JsonNode? n) => n is JsonObject o ? From(o, false) : null;

    static ElementFilter From(JsonObject o, bool check)
    {
        var block = ConditionGroup.Read(o, check);
        return new(Strings(o["classes"]), Strings(o["types"]), Strings(o["storeys"]), Strings(o["materials"]), Strings(o["groups"]), Strings(o["phases"]), block.Conditions)
        {
            Demolished = Strings(o["demolished"]), Any = block.Any, Nested = block.Nested,
        };
    }

    /// <summary>A filter as a command gives it, checked: each condition with a field and an operator Lapin knows.</summary>
    public static ElementFilter? Check(JsonNode? n)
    {
        if (n is null) return null;
        if (n is not JsonObject o) throw new CommandException("A filter is an object: {classes, types, storeys, materials, groups, phases, demolished, match, conditions, nested}.");
        var f = From(o, true);
        if (f.Block.All().Count() > MaxConditions) throw new CommandException($"A filter has at most {MaxConditions} conditions.");
        return f;
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject();
        foreach (var (k, v) in Lists()) if (v.Count > 0) o[k] = new JsonArray([.. v.Select(x => (JsonNode)x)]);
        if (Any) o["match"] = "any";
        if (Conditions.Count > 0) o["conditions"] = new JsonArray([.. Conditions.Select(c => (JsonNode)c.ToJson())]);
        if (Nested.Count > 0) o["nested"] = new JsonArray([.. Nested.Select(g => (JsonNode)g.ToJson())]);
        return o;
    }

    /// <summary>Whether an entity is one a schedule may list: an element of the model (one of a file's too), not an opening, a drawing, a grid or a record.</summary>
    public static bool IsListed(IQueryContext q, Entity e)
    {
        if (NotListed.Contains(e.Class)) return false;
        // Solids kept hidden behind the layered wall made of them (forum topic 101): the wall is listed, not they.
        if (!Superseded.Counts(q.Model, e)) return false;
        return q.Describe(e.Class) is { } d ? d.Kind == "element" : StoredBodies.Has(e);
    }

    /// <summary>The elements the filter takes, in the model's order (null filter: every element listed).</summary>
    public static List<Entity> Elements(IQueryContext q, ElementFilter? filter, string? units = null)
    {
        units ??= DraftingResources.Book(q.Model).Units;
        var ctx = filter != null ? new Context(q, units: units) : null;
        var o = new List<Entity>();
        foreach (var e in q.Model.Entities)
        {
            if (!IsListed(q, e)) continue;
            if (filter != null && !filter.Matches(ctx!, e, new ElementFields(q, e, units))) continue;
            o.Add(e);
        }
        return o;
    }

    /// <summary>
    /// What matching reads of the model once: the phases' names and the default one, each element's materials. A query without
    /// geometry (<see cref="ModelQuery"/>: a view's graphics) leaves out what only the geometry says: the materials of an
    /// element's parts and its quantities.
    /// </summary>
    public sealed class Context(IQueryContext q, string? defaultPhase = null, string? units = null)
    {
        public IQueryContext Query => q;
        public string Units { get; } = units ?? DraftingResources.Book(q.Model).Units;
        readonly Dictionary<string, HashSet<string>> _materials = [];
        readonly Dictionary<string, Func<string, double?>> _quantities = [];
        List<(string Id, string Name, string Kind)>? _phases;
        public List<(string Id, string Name, string Kind)> Phases => _phases ??= ((Spatial.Project(q.Model)?.Attrs["phases"] as JsonObject)?["phases"] as JsonArray ?? [])
            .OfType<JsonObject>().Select(p => ((string?)p["id"] ?? "", (string?)p["name"] ?? "", (string?)p["kind"] ?? "")).Where(p => p.Item1.Length > 0).ToList();
        /// <summary>The phase an unphased element counts as created in: the one given (the phases module's), else the first construction phase, else the first (the default list: new construction).</summary>
        public string DefaultPhase => defaultPhase is { Length: > 0 } d ? d : Phases.FirstOrDefault(p => p.Kind == "construction").Id ?? Phases.FirstOrDefault().Id ?? "new-construction";

        /// <summary>A phase's id and name (its name from the default list when the project wrote none down).</summary>
        public IReadOnlyList<string> PhaseNames(string id)
        {
            var name = Phases.FirstOrDefault(p => p.Id == id).Name ?? (Phases.Count == 0 ? DefaultNames.GetValueOrDefault(id) : null);
            return name is { Length: > 0 } && name != id ? [id, name] : [id];
        }

        static readonly Dictionary<string, string> DefaultNames = new() { ["existing"] = "Existing", ["new-construction"] = "New construction" };

        public HashSet<string> MaterialsOf(Entity e) => _materials.TryGetValue(e.Id, out var s) ? s : _materials[e.Id] = ElementFilter.MaterialsOf(q, e);
        public Func<string, double?> QuantitiesOf(Entity e) => _quantities.TryGetValue(e.Id, out var f) ? f : _quantities[e.Id] = ParamFormulas.Quantities(q, e.Id);

        /// <summary>Whether a condition holds on an element: a facet's (<see cref="ElementFacets"/>) or a field's, read by <paramref name="field"/>.</summary>
        public bool Holds(FieldCondition c, Entity e, Func<string, FieldValue> field) =>
            ElementFacets.Is(c.Field) ? c.HoldsAny(ElementFacets.Values(this, e, c.Field)) : c.Holds(field(c.Field));
    }

    static bool Named(IReadOnlyList<string> wanted, Entity? x, string? name = null) =>
        x != null && wanted.Any(w => w == x.Id || string.Equals(w, name ?? x.Name, StringComparison.OrdinalIgnoreCase));

    static bool InPhases(Context ctx, IReadOnlyList<string> wanted, string phase) =>
        ctx.PhaseNames(phase) is var names && wanted.Any(w => names.Any(n => string.Equals(w, n, w == phase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)));

    public bool TakesClass(string cls) => Classes.Any(c =>
        c == cls || "Ifc" + c == cls || string.Equals(c, cls, StringComparison.OrdinalIgnoreCase) || IfcSchema.Parent(cls) != null && IfcSchema.IsA(cls, c));

    public bool Matches(Context ctx, Entity e, ElementFields fields) => Matches(ctx, e, fields.Value);

    /// <summary>Whether it takes an element, its conditions on fields read by <paramref name="field"/>.</summary>
    public bool Matches(Context ctx, Entity e, Func<string, FieldValue> field)
    {
        var m = ctx.Query.Model;
        if (Classes.Count > 0 && !TakesClass(e.Class)) return false;
        if (Types.Count > 0 && (Lapin.Platform.Types.Of(m, e) is not { } type ? !Types.Any(t => string.Equals(t, ReferenceTypes.NameOf(m, e), StringComparison.OrdinalIgnoreCase))
            : !Named(Types, type, ReferenceTypes.Name(type)))) return false;
        if (Storeys.Count > 0 && !Named(Storeys, Spatial.StoreyOf(m, e, followHost: true))) return false;
        if (Groups.Count > 0 && !Named(Groups, Lapin.Platform.Groups.Of(m, e.Id))) return false;
        if (Phases.Count > 0 && !InPhases(ctx, Phases, e.Str("phaseCreated") ?? ctx.DefaultPhase)) return false;
        if (Demolished.Count > 0 && (e.Str("phaseDemolished") is not { } demolished || !InPhases(ctx, Demolished, demolished))) return false;
        if (Materials.Count > 0)
        {
            var mats = ctx.MaterialsOf(e);
            if (!Materials.Any(w => mats.Contains(w) || mats.Any(id => string.Equals(m.Get(id)?.Name, w, StringComparison.OrdinalIgnoreCase)))) return false;
        }
        return Block.Holds(c => ctx.Holds(c, e, field));
    }

    /// <summary>An element's storey: its own, else (a door, a window) its host's.</summary>
    public static Entity? StoreyOf(IModelView m, Entity e) => Spatial.StoreyOf(m, e, followHost: true);

    /// <summary>
    /// Every material in an element: its own and its type's (the one associated with either, a layer set's layers), its layers'
    /// and their pieces', what its module says it is built of, and its built parts' (when the query has its geometry).
    /// </summary>
    public static HashSet<string> MaterialsOf(IQueryContext q, Entity e)
    {
        var m = q.Model;
        var o = new HashSet<string>(StringComparer.Ordinal);
        void Take(string? id) { if (id != null && m.Get(id) is { Class: IfcClass.Material }) o.Add(id); }
        void Associated(Entity? r, int depth)
        {
            if (r == null || depth > 2) return;
            if (r.Class == IfcClass.Material) { o.Add(r.Id); return; }
            // A layer set (or its usage): the materials its layers name.
            foreach (var id in Ids(r.Attrs)) if (m.Get(id) is { } x && x.Id != r.Id) Associated(x, depth + 1);
        }
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        foreach (var x in new[] { e, type })
        {
            if (x == null) continue;
            Take(x.Str("material"));
            Associated(m.RelatingOf(x.Id, IfcClass.RelAssociatesMaterial), 0);
        }
        foreach (var b in LayerSets.DrawnBandsOf(m, e.Id) ?? []) { Take(b.Material); foreach (var p in b.Parts ?? []) Take(p.Material); }
        if (type != null) foreach (var l in LayerSets.LayersOf(m, type.Id)) { Take(l.Material); foreach (var p in l.Parts ?? []) Take(p.Material); }
        foreach (var l in BuildUps.Of(m, e, q.Services)?.Layers ?? []) { Take(l.Material); foreach (var p in l.PartMaterials ?? []) Take(p); }
        foreach (var p in q.Geometry(e.Id)?.Parts ?? []) Take(p.Material);
        return o;
    }

    static IEnumerable<string> Ids(JsonNode? n)
    {
        switch (n)
        {
            case JsonValue v when v.TryGetValue<string>(out var s): yield return s; break;
            case JsonArray a: foreach (var x in a) foreach (var s in Ids(x)) yield return s; break;
            case JsonObject o: foreach (var (_, x) in o) foreach (var s in Ids(x)) yield return s; break;
        }
    }
}

/// <summary>An element's type, the one way (audit: the ~14 copies of „the element's type”).</summary>
public static class Types
{
    /// <summary>
    /// The element's type: with <paramref name="typeClass"/>, its IfcRelDefinesByType's when it is of that class (else none);
    /// without, that one, else for a reference the record of the type its file relates it to (<see cref="IfcSource"/>).
    /// </summary>
    public static Entity? Of(IModelView m, Entity e, string? typeClass = null)
    {
        var t = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        if (typeClass != null) return t != null && t.Class == typeClass ? t : null;
        return t ?? ((string?)e.Attrs[IfcSource.Key]?["type"]?["record"] is { Length: > 0 } rec && m.Get(rec) is { Class: IfcSource.RecordClass } record ? record : null);
    }

    /// <summary>The type of the element <paramref name="id"/> (<see cref="Of(IModelView, Entity, string?)"/>).</summary>
    public static Entity? Of(IModelView m, string id, string? typeClass = null) =>
        m.Get(id) is { } e ? Of(m, e, typeClass) : null;
}

/// <summary>A query on a model alone: no geometry and no quantities (a view's graphics, which match filters on the model, never build elements).</summary>
public sealed class ModelQuery(IModelView m, IServices? services = null, Func<string, ClassDescriptor?>? describe = null) : IQueryContext
{
    sealed class None : IServices
    {
        public T? Get<T>() where T : class => null;
        public IReadOnlyList<T> All<T>() where T : class => [];
    }

    public IModelView Model => m;
    public IServices Services { get; } = services ?? new None();
    public ElementGeometry? Geometry(string entityId) => null;
    public IReadOnlyList<QuantitySet> Quantities(string id) => [];
    public ClassDescriptor? Describe(string ifcClass) => describe?.Invoke(ifcClass);
}
