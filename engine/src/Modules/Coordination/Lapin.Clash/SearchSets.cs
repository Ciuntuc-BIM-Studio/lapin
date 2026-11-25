using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Clash;

/// <summary>A rule of a search set: a field of the element, an operator, its values (and a parameter's name for <see cref="SearchSets.Parameter"/>).</summary>
public sealed record SetRule(string Field, string Op, IReadOnlyList<string> Values, string? Parameter = null)
{
    public bool IsComplete
    {
        get
        {
            if (SearchSets.TakesNoValue(Op)) return Field != SearchSets.Parameter && Field != SearchSets.Field || !string.IsNullOrWhiteSpace(Parameter);
            if (Values.Count == 0) return false;
            if ((Field is SearchSets.Parameter or SearchSets.Field) && string.IsNullOrWhiteSpace(Parameter)) return false;
            if (SearchSets.IsNumeric(Op))
            {
                var needed = Op == SearchSets.Between ? 2 : 1;
                return Values.Count >= needed && Values.Take(needed).All(v => SearchSets.TryNumber(v, out _));
            }
            return !SearchSets.IsText(Op) || Values.Any(v => v.Trim().Length > 0);
        }
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["field"] = Field, ["op"] = Op, ["values"] = new JsonArray(Values.Select(v => (JsonNode)v).ToArray()) };
        if (Parameter != null) o["parameter"] = Parameter;
        return o;
    }

    public static SetRule? From(JsonNode? n) => n is JsonObject o && (string?)o["field"] is { } f
        ? new SetRule(f, (string?)o["op"] ?? SearchSets.Is, (o["values"] as JsonArray ?? []).Select(v => v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : v?.ToJsonString() ?? "").ToList(), (string?)o["parameter"])
        : null;
}

/// <summary>A group of rules inside a search set (to any depth): any or all of them; inside a group a "not" rule is an ordinary condition.</summary>
public sealed record RuleGroup(bool MatchAll, IReadOnlyList<SetRule> Rules, IReadOnlyList<RuleGroup> Groups)
{
    public bool HasConditions => Rules.Any(r => r.IsComplete) || Groups.Any(g => g.HasConditions);

    public JsonObject ToJson() => new()
    {
        ["matchAll"] = MatchAll,
        ["rules"] = new JsonArray(Rules.Select(r => (JsonNode)r.ToJson()).ToArray()),
        ["groups"] = new JsonArray(Groups.Select(g => (JsonNode)g.ToJson()).ToArray()),
    };

    public static RuleGroup From(JsonObject? o) => new(
        o?["matchAll"] is JsonValue v && v.TryGetValue<bool>(out var b) && b,
        (o?["rules"] as JsonArray ?? []).Select(SetRule.From).OfType<SetRule>().ToList(),
        (o?["groups"] as JsonArray ?? []).Select(g => From(g as JsonObject)).ToList());
}

/// <summary>What a clash's report and groups say of an element: its class's label, type, storey (a door's its wall's), materials and link.</summary>
public sealed record ClashFacts(Entity Entity, string ClassLabel, string? Type, string? Storey, IReadOnlyList<string> Materials, string? Link);

/// <summary>
/// Search sets (docs/plan-visualisation.md, stage 2), after BimGo's SearchSets (MIT-0), on Lapin's own fields: the IFC
/// class (or its label), the predefined type, the type's name, the storey, the materials, the name, the link the element
/// came from (none: the model's own), its category, group and phases, any parameter (read as a tag writes it, with the
/// element's own attribute of that name as stored) and any field (read as a schedule reads it). Operators: is, is one of,
/// is not, is not any of, contains, does not contain, starts with, at least, at most, greater, less, between (on the first
/// number of the text: "600 mm" is 600; "1,200" and "2,5" read as people write them), has a value, has none. A set takes
/// its rules' elements, any or all of them; a "not" rule at the top always takes elements out; each group with a condition
/// is one more condition; a set with no adding rule holds everything. Rules not filled in yet are left out. A set is a
/// filter of Lapin's one engine (<see cref="ElementFilter"/>, audit D3), its rules kept in their own words.
/// </summary>
public static class SearchSets
{
    public const string Class = "LapinSearchSet";
    public const string Is = "is", IsOneOf = "isOneOf", IsNot = "isNot", IsNotAnyOf = "isNotAnyOf", Contains = "contains", NotContains = "notContains",
        StartsWith = "startsWith", AtLeast = "atLeast", AtMost = "atMost", Greater = "greater", Less = "less", Between = "between", Exists = "exists", Missing = "missing";
    public static readonly string[] Ops = [Is, IsOneOf, IsNot, IsNotAnyOf, Contains, NotContains, StartsWith, AtLeast, AtMost, Greater, Less, Between, Exists, Missing];
    public const string ClassField = "class", PredefinedType = "predefinedType", Type = "type", Storey = "storey", Material = "material", Name = "name", Link = "link", Parameter = "parameter",
        Category = "category", Group = "group", Phase = "phase", Demolished = "demolished", Structural = "structural", Field = "field";
    public static readonly string[] Fields = [ClassField, PredefinedType, Type, Storey, Material, Name, Link, Parameter, Category, Group, Phase, Demolished, Structural, Field];

    public static bool IsNegative(string op) => op is IsNot or IsNotAnyOf or NotContains;
    public static bool IsText(string op) => op is Contains or NotContains or StartsWith;
    public static bool IsNumeric(string op) => op is AtLeast or AtMost or Greater or Less or Between;
    public static bool TakesNoValue(string op) => op is Exists or Missing;

    public sealed record Set(string Id, string Name, bool MatchAll, IReadOnlyList<SetRule> Rules, IReadOnlyList<RuleGroup> Groups);

    public static Set Read(Entity e)
    {
        var g = RuleGroup.From(e.Attrs);
        return new(e.Id, e.Name ?? "", g.MatchAll, g.Rules, g.Groups);
    }

    /// <summary>The field a rule reads, as a condition names it: a facet (<c>@type</c>), a parameter read as text (<c>@text:</c>), or a field itself.</summary>
    static string FieldOf(SetRule r) => r.Field switch
    {
        Parameter => ElementFacets.TextPrefix + (r.Parameter ?? "").Trim(),
        Field => (r.Parameter ?? "").Trim(),
        _ => "@" + r.Field,
    };

    /// <summary>A complete rule as conditions of Lapin's one filter: one condition, or a group of them (the link's empty value: the model's own).</summary>
    static ConditionGroup Translate(SetRule r)
    {
        var field = FieldOf(r);
        var values = r.Values.Select(v => v.Trim()).ToList();
        FieldCondition C(string op, string? value = null, IReadOnlyList<string>? list = null) => new(field, op, value, list);
        static ConditionGroup One(FieldCondition c) => new(false, [c], []);
        switch (r.Op)
        {
            case Is or IsOneOf or IsNot or IsNotAnyOf:
                var not = r.Op is IsNot or IsNotAnyOf;
                if (r.Field == Link && values.Contains(""))
                {
                    var named = values.Where(v => v.Length > 0).ToList();
                    return not
                        ? new(false, [C("exists"), .. named.Count > 0 ? [C("none of", null, named)] : Array.Empty<FieldCondition>()], [])
                        : new(true, [C("missing"), .. named.Count > 0 ? [C("one of", null, named)] : Array.Empty<FieldCondition>()], []);
                }
                return One(C(not ? "none of" : "one of", null, values));
            case Contains: return One(C("contains", null, values.Where(v => v.Length > 0).ToList()));
            case NotContains: return One(C("not contains", null, values.Where(v => v.Length > 0).ToList()));
            case StartsWith: return One(C("starts with", null, values.Where(v => v.Length > 0).ToList()));
            case AtLeast: return One(C("≥", values[0]));
            case AtMost: return One(C("≤", values[0]));
            case Greater: return One(C(">", values[0]));
            case Less: return One(C("<", values[0]));
            case Between: return One(C("between", null, values.Take(2).ToList()));
            case Exists: return One(C("exists"));
            case Missing: return One(C("missing"));
        }
        return new(false, [], []);
    }

    static void Add(List<FieldCondition> conditions, List<ConditionGroup> groups, ConditionGroup g)
    {
        if (g.Conditions.Count == 1 && g.Nested.Count == 0) conditions.Add(g.Conditions[0]);
        else groups.Add(g);
    }

    /// <summary>A group of rules as a group of conditions: inside it a "not" rule is an ordinary condition.</summary>
    static ConditionGroup Translate(RuleGroup group)
    {
        var (conditions, groups) = (new List<FieldCondition>(), new List<ConditionGroup>());
        foreach (var r in group.Rules) if (r.IsComplete) Add(conditions, groups, Translate(r));
        foreach (var g in group.Groups) if (g.HasConditions) groups.Add(Translate(g));
        return new(!group.MatchAll, conditions, groups);
    }

    /// <summary>
    /// A set as Lapin's one filter: its "not" rules at the top, each of which must hold (they take elements out whatever the
    /// set's match), and, when it has any, its adding rules and groups, all or any of them; with none, everything else.
    /// </summary>
    public static ElementFilter ToFilter(Set set)
    {
        var (not, notGroups) = (new List<FieldCondition>(), new List<ConditionGroup>());
        var (adding, addingGroups) = (new List<FieldCondition>(), new List<ConditionGroup>());
        foreach (var r in set.Rules)
        {
            if (!r.IsComplete) continue;
            if (IsNegative(r.Op)) Add(not, notGroups, Translate(r));
            else Add(adding, addingGroups, Translate(r));
        }
        foreach (var g in set.Groups) if (g.HasConditions) addingGroups.Add(Translate(g));
        if (adding.Count + addingGroups.Count > 0) notGroups.Add(new ConditionGroup(!set.MatchAll, adding, addingGroups));
        return ElementFilter.All with { Conditions = not, Nested = notGroups };
    }

    /// <summary>A run's matching: what it reads of the model once.</summary>
    public sealed class Matcher(IQueryContext q)
    {
        public readonly ElementFilter.Context Context = new(q);
        public bool Matches(ElementFilter f, Entity e) => f.Matches(Context, e, new ElementFields(q, e, Context.Units));
    }

    public static bool Matches(IQueryContext q, Set set, Entity e) => new Matcher(q).Matches(ToFilter(set), e);

    /// <summary>The texts an element has for a field, as a rule's value picker lists them (the class has its IFC name, its label and the name without "Ifc").</summary>
    public static IReadOnlyList<string> Texts(ElementFilter.Context ctx, string field, string? parameter, Entity e)
    {
        if (field == Field) return ((string?)parameter)?.Trim() is { Length: > 0 } token && new ElementFields(ctx.Query, e, ctx.Units).Value(token) is { Exists: true } v ? [v.Shown] : [];
        if (field == Parameter && string.IsNullOrWhiteSpace(parameter)) return [];
        return ElementFacets.Values(ctx, e, FieldOf(new SetRule(field, Is, [], parameter)));
    }

    /// <summary>A parameter's value: the element's attribute of that name, or one of that name in an object of its attributes (its properties).</summary>
    public static string? ParameterValue(Entity e, string name) => ElementFacets.StoredValue(e, name);

    /// <summary>The first number in a text by the one reader ("600 mm" 600, "1,200" 1.2, "2,5" 2.5, "−3" −3); false when it has none.</summary>
    public static bool TryNumber(string? text, out double value)
    {
        var n = Numbers.Parse(text, NumberMode.First);
        value = n ?? 0;
        return n != null;
    }

    /// <summary>What a clash's report says of an element.</summary>
    public static ClashFacts Facts(IQueryContext q, Entity e)
    {
        var m = q.Model;
        var materials = ElementFilter.MaterialsOf(new ModelQuery(m, q.Services, q.Describe), e).Select(id => m.Get(id)?.Name).OfType<string>().Where(n => n.Length > 0).Distinct().ToList();
        var link = Links.Of(m, e)?.Name ?? e.Str(Links.Key);
        return new ClashFacts(e, q.Describe(e.Class)?.Label ?? e.Class, ReferenceTypes.NameOf(m, e), Spatial.StoreyOf(m, e, followHost: true)?.Name, materials, link);
    }

    /// <summary>The classes of the sets made with a new project's clash tests.</summary>
    public static readonly string[] StructureClasses = ["IfcColumn", "IfcBeam", "IfcFooting", "IfcSlab", "IfcMember", "IfcPlate", "IfcPile", "LapinSteelMember"];
    public static readonly string[] ServicesClasses = ["IfcDuctSegment", "IfcDuctFitting", "IfcPipeSegment", "IfcPipeFitting", "IfcCableCarrierSegment", "IfcCableCarrierFitting", "IfcCableSegment",
        "IfcFlowSegment", "IfcFlowTerminal", "IfcAirTerminal", "IfcSanitaryTerminal", "IfcSpaceHeater", "IfcLightFixture", "IfcOutlet", "IfcSwitchingDevice", "IfcElectricDistributionBoard",
        "IfcFlowController", "IfcValve", "IfcFlowMeter", "IfcFilter", "IfcBoiler", "IfcBurner", "IfcFan", "IfcPump", "IfcTank", "IfcDistributionChamberElement", "IfcUnitaryEquipment", "IfcFireSuppressionTerminal"];
    public static readonly string[] ArchitectureClasses = ["IfcWall", "IfcCurtainWall", "IfcDoor", "IfcWindow", "IfcStair", "IfcStairFlight", "IfcRamp", "IfcRailing", "IfcRoof", "IfcCovering"];

    public static JsonObject ClassSet(IEnumerable<string> classes) => new RuleGroup(false, [new SetRule(ClassField, IsOneOf, classes.ToList())], []).ToJson();
}
