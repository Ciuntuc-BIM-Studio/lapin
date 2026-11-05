using System.Globalization;
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
            if (Values.Count == 0) return false;
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

/// <summary>What a search set reads of an element, found once a run.</summary>
public sealed record ElementFacts(Entity Entity, string ClassLabel, string? Type, string? Storey, IReadOnlyList<string> Materials, string? Link);

/// <summary>
/// Search sets (docs/plan-visualisation.md, stage 2), after BimGo's SearchSets (MIT-0), on Lapin's own fields: the IFC
/// class (or its label), the predefined type, the type's name, the storey, the materials, the name, the link the element
/// came from (none: the model's own) and any parameter. Operators: is, is one of, is not, is not any of, contains, does
/// not contain, at least, at most, between (on the first number of the text: "600 mm" is 600; "1,200" and "2,5" read as
/// people write them). A set takes its rules' elements, any or all of them; a "not" rule at the top always takes elements
/// out; each group with a condition is one more condition; a set with no adding rule holds everything. Rules not filled
/// in yet are left out.
/// </summary>
public static class SearchSets
{
    public const string Class = "LapinSearchSet";
    public const string Is = "is", IsOneOf = "isOneOf", IsNot = "isNot", IsNotAnyOf = "isNotAnyOf", Contains = "contains", NotContains = "notContains",
        AtLeast = "atLeast", AtMost = "atMost", Between = "between";
    public static readonly string[] Ops = [Is, IsOneOf, IsNot, IsNotAnyOf, Contains, NotContains, AtLeast, AtMost, Between];
    public const string ClassField = "class", PredefinedType = "predefinedType", Type = "type", Storey = "storey", Material = "material", Name = "name", Link = "link", Parameter = "parameter";
    public static readonly string[] Fields = [ClassField, PredefinedType, Type, Storey, Material, Name, Link, Parameter];

    public static bool IsNegative(string op) => op is IsNot or IsNotAnyOf or NotContains;
    public static bool IsText(string op) => op is Contains or NotContains;
    public static bool IsNumeric(string op) => op is AtLeast or AtMost or Between;

    public sealed record Set(string Id, string Name, bool MatchAll, IReadOnlyList<SetRule> Rules, IReadOnlyList<RuleGroup> Groups);

    public static Set Read(Entity e)
    {
        var g = RuleGroup.From(e.Attrs);
        return new(e.Id, e.Name ?? "", g.MatchAll, g.Rules, g.Groups);
    }

    public static bool Matches(Set set, ElementFacts e)
    {
        bool any = false, included = set.MatchAll;
        foreach (var rule in set.Rules)
        {
            if (!rule.IsComplete) continue;
            var hit = RuleMatches(rule, e);
            if (IsNegative(rule.Op)) { if (hit) return false; continue; }
            any = true;
            included = set.MatchAll ? included && hit : included || hit;
        }
        foreach (var group in set.Groups)
        {
            if (!group.HasConditions) continue;
            any = true;
            var hit = GroupMatches(group, e);
            included = set.MatchAll ? included && hit : included || hit;
        }
        return !any || included;
    }

    public static bool GroupMatches(RuleGroup group, ElementFacts e)
    {
        bool any = false, result = group.MatchAll;
        foreach (var rule in group.Rules)
        {
            if (!rule.IsComplete) continue;
            var hit = RuleMatches(rule, e);
            if (IsNegative(rule.Op)) hit = !hit;
            any = true;
            result = group.MatchAll ? result && hit : result || hit;
            if (group.MatchAll ? !result : result) return result;
        }
        foreach (var inner in group.Groups)
        {
            if (!inner.HasConditions) continue;
            any = true;
            var hit = GroupMatches(inner, e);
            result = group.MatchAll ? result && hit : result || hit;
            if (group.MatchAll ? !result : result) return result;
        }
        return any && result;
    }

    /// <summary>Whether an element's field matches a rule's values, ignoring case and the rule's "not": equal to one, or containing one; numbers compared.</summary>
    public static bool RuleMatches(SetRule rule, ElementFacts e)
    {
        var texts = Texts(rule.Field, rule.Parameter, e);
        if (IsNumeric(rule.Op))
        {
            if (!TryNumber(rule.Values[0], out var a)) return false;
            foreach (var t in texts)
            {
                if (!TryNumber(t, out var n)) continue;
                if (rule.Op == AtLeast ? n >= a : rule.Op == AtMost ? n <= a : rule.Values.Count > 1 && TryNumber(rule.Values[1], out var b) && n >= Math.Min(a, b) && n <= Math.Max(a, b)) return true;
            }
            return false;
        }
        var contains = IsText(rule.Op);
        foreach (var raw in rule.Values)
        {
            var value = raw.Trim();
            if (contains && value.Length == 0) continue;
            // The link field's empty value: the model's own elements.
            if (rule.Field == Link && value.Length == 0) { if (e.Link == null) return true; continue; }
            foreach (var t in texts) if (Compare(t, value, contains)) return true;
        }
        return false;
    }

    /// <summary>The texts an element has for a field (the class has its IFC name, its label and the name without "Ifc").</summary>
    public static IEnumerable<string> Texts(string field, string? parameter, ElementFacts e) => field switch
    {
        ClassField => [e.Entity.Class, e.ClassLabel, e.Entity.Class.StartsWith("Ifc", StringComparison.Ordinal) ? e.Entity.Class[3..] : e.Entity.Class],
        PredefinedType => e.Entity.PredefinedType is { } p ? [p] : [],
        Type => e.Type is { } t ? [t] : [],
        Storey => e.Storey is { } s ? [s] : [],
        Material => e.Materials,
        Name => e.Entity.Name is { } n ? [n] : [],
        Link => e.Link is { } l ? [l] : [],
        Parameter => parameter is { Length: > 0 } && ParameterValue(e.Entity, parameter) is { } v ? [v] : [],
        _ => [],
    };

    /// <summary>A parameter's value: the element's attribute of that name, or one of that name in an object of its attributes (its properties).</summary>
    public static string? ParameterValue(Entity e, string name)
    {
        static string? Text(JsonNode? n) => n switch
        {
            null => null,
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            JsonValue v when v.TryGetValue<double>(out var d) => d.ToString(CultureInfo.InvariantCulture),
            JsonValue v when v.TryGetValue<bool>(out var b) => b ? "true" : "false",
            JsonValue v => v.ToJsonString(),
            _ => null,
        };
        foreach (var (k, v) in e.Attrs) if (string.Equals(k, name, StringComparison.OrdinalIgnoreCase) && Text(v) is { } s) return s;
        foreach (var (_, v) in e.Attrs)
            if (v is JsonObject o)
                foreach (var (k, w) in o)
                    if (string.Equals(k, name, StringComparison.OrdinalIgnoreCase) && Text(w) is { } s) return s;
        return null;
    }

    static bool Compare(string text, string value, bool contains) =>
        contains ? text.Contains(value, StringComparison.CurrentCultureIgnoreCase) : string.Equals(text.Trim(), value, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>The first number in a text ("600 mm" 600, "1,200" 1200, "2,5" 2.5, "−3" −3); false when it has none (BimGo's RuleNumbers).</summary>
    public static bool TryNumber(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text)) return false;
        var i = 0;
        while (i < text.Length && !char.IsDigit(text[i])) i++;
        if (i >= text.Length) return false;
        var negative = i > 0 && (text[i - 1] == '-' || text[i - 1] == '−') && (i < 2 || !char.IsDigit(text[i - 2]));
        if (i > 0 && text[i - 1] == '.') i--;
        var digits = new System.Text.StringBuilder();
        var dot = false;
        static bool GroupFollows(string s, int at)
        {
            var n = 0;
            while (at + n < s.Length && char.IsDigit(s[at + n])) n++;
            return n == 3;
        }
        for (; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsDigit(c)) { digits.Append(c); continue; }
            var nextDigit = i + 1 < text.Length && char.IsDigit(text[i + 1]);
            if (!nextDigit) break;
            if ((c == ' ' || c == ' ' || c == ' ' || c == '\'') && digits.Length > 0 && !dot && GroupFollows(text, i + 1)) continue;
            if (c == '.' && !dot) { dot = true; digits.Append('.'); continue; }
            if (c == ',' && !dot)
            {
                if (GroupFollows(text, i + 1)) continue;
                dot = true;
                digits.Append('.');
                continue;
            }
            break;
        }
        if (digits.Length == 0 || digits.ToString() == ".") return false;
        if (!double.TryParse(digits.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)) return false;
        if (negative) value = -value;
        return double.IsFinite(value);
    }

    /// <summary>What a search set reads of an element.</summary>
    public static ElementFacts Facts(IQueryContext q, Entity e)
    {
        var m = q.Model;
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        var storey = Spatial.StoreyOf(m, e.Id);
        var materials = new List<string>();
        void From(Entity? x)
        {
            if (x == null) return;
            if (m.RelatingOf(x.Id, IfcClass.RelAssociatesMaterial) is { } r) Collect(r);
        }
        void Collect(Entity r)
        {
            if (r.Class == IfcClass.Material) { if (r.Name is { } n && !materials.Contains(n)) materials.Add(n); return; }
            // A layer set (or a usage): the materials its layers name.
            foreach (var id in Ids(r.Attrs)) if (m.Get(id) is { Class: IfcClass.Material, Name: { } n } && !materials.Contains(n)) materials.Add(n);
        }
        From(e);
        From(type);
        if (e.Attrs["material"] is JsonValue mv && mv.TryGetValue<string>(out var mid) && m.Get(mid) is { Class: IfcClass.Material } own) Collect(own);
        var link = e.Attrs[Links.Key] is JsonValue lv && lv.TryGetValue<string>(out var lid) ? m.Get(lid)?.Name ?? lid : null;
        return new ElementFacts(e, q.Describe(e.Class)?.Label ?? e.Class, type?.Name, storey?.Name, materials, link);
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

    /// <summary>The classes of the sets made with a new project's clash tests.</summary>
    public static readonly string[] StructureClasses = ["IfcColumn", "IfcBeam", "IfcFooting", "IfcSlab", "IfcMember", "IfcPlate", "IfcPile", "LapinSteelMember"];
    public static readonly string[] ServicesClasses = ["IfcDuctSegment", "IfcDuctFitting", "IfcPipeSegment", "IfcPipeFitting", "IfcCableCarrierSegment", "IfcCableCarrierFitting", "IfcCableSegment",
        "IfcFlowSegment", "IfcFlowTerminal", "IfcAirTerminal", "IfcSanitaryTerminal", "IfcSpaceHeater", "IfcLightFixture", "IfcOutlet", "IfcSwitchingDevice", "IfcElectricDistributionBoard",
        "IfcFlowController", "IfcValve", "IfcFlowMeter", "IfcFilter", "IfcBoiler", "IfcBurner", "IfcFan", "IfcPump", "IfcTank", "IfcDistributionChamberElement", "IfcUnitaryEquipment", "IfcFireSuppressionTerminal"];
    public static readonly string[] ArchitectureClasses = ["IfcWall", "IfcCurtainWall", "IfcDoor", "IfcWindow", "IfcStair", "IfcStairFlight", "IfcRamp", "IfcRailing", "IfcRoof", "IfcCovering"];

    public static JsonObject ClassSet(IEnumerable<string> classes) => new RuleGroup(false, [new SetRule(ClassField, IsOneOf, classes.ToList())], []).ToJson();
}
