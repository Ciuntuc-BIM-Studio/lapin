using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Rule-based view filters as in Revit (docs/plan-visibility-graphics.md, Filters): the project's
/// (<c>viewFilters</c>), each a name, <c>all</c> or <c>any</c> of its rules, and rules
/// <c>{field, op, value}</c>. A view lists the filters it uses in order in its graphics
/// (<c>filters: [{id, hidden, …}]</c>); <see cref="VisibilityGraphics"/> applies them. Quick select reads the same rules.
/// A rule is a condition of Lapin's one filter (<see cref="ElementFilter"/>, audit D3), kept in its own words: its field one
/// of the element's facets (<see cref="ElementFacets"/>: class, type, storey…), <c>param</c> a field read as a tag writes it
/// (in the project's units, as rules have always read it; <c>name</c> names it), or <c>field</c> any field read as a
/// schedule reads it (numbers in SI, quantities where the caller has the geometry; <c>name</c> its token).
/// </summary>
public static class ViewFilters
{
    public const string Key = "viewFilters";

    /// <summary>What a rule reads of an element; <c>param</c> and <c>field</c> name a field in <c>name</c>.</summary>
    public static readonly string[] Fields = ["class", "category", "type", "name", "material", "storey", "group", "link", "phase", "demolished", "predefinedType", "param", "field"];
    public static readonly string[] Ops = ["equals", "notEquals", "contains", "notContains", "beginsWith", "greater", "less", "atLeast", "atMost", "between", "oneOf", "noneOf", "hasValue", "hasNoValue"];

    /// <summary>A rule: a field, an operator, its value (a range "min..max"), or its list (<c>oneOf</c>, <c>noneOf</c>), and the field's name for <c>param</c> and <c>field</c>.</summary>
    public sealed record Rule(string Field, string Op, string? Value, string? Name, IReadOnlyList<string>? Values = null);
    public sealed record Filter(string Id, string Name, bool Any, IReadOnlyList<Rule> Rules);

    public static IReadOnlyList<Filter> All(IModelView m) =>
        (Spatial.Project(m)?.Attrs[Key] as JsonArray)?.OfType<JsonObject>().Select(Read).Where(f => f.Id.Length > 0).ToList() ?? [];

    static IReadOnlyList<string>? ValuesOf(JsonNode? n) => n is JsonArray a ? a.Select(ValueText).Select(x => x ?? "").ToList() : null;

    static Filter Read(JsonObject o) => new(
        (string?)o["id"] ?? "", (string?)o["name"] ?? "Filter", (string?)o["match"] == "any",
        (o["rules"] as JsonArray)?.OfType<JsonObject>().Select(r => new Rule((string?)r["field"] ?? "class", (string?)r["op"] ?? "equals", ValueText(r["value"]), (string?)r["name"], ValuesOf(r["values"]))).ToList() ?? []);

    static string? ValueText(JsonNode? n) => n switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v when Json.Number(v) is { } d => d.ToString(CultureInfo.InvariantCulture),
        _ => n.ToJsonString(),
    };

    public static JsonObject Write(Rule r)
    {
        var o = new JsonObject { ["field"] = r.Field, ["op"] = r.Op };
        if (r.Value != null) o["value"] = r.Value;
        if (r.Values != null) o["values"] = new JsonArray([.. r.Values.Select(v => (JsonNode)v)]);
        if (r.Name != null) o["name"] = r.Name;
        return o;
    }

    static JsonObject Write(Filter f) => new()
    {
        ["id"] = f.Id, ["name"] = f.Name, ["match"] = f.Any ? "any" : "all",
        ["rules"] = new JsonArray(f.Rules.Select(r => (JsonNode)Write(r)).ToArray()),
    };

    /// <summary>Rules as a command gives them, checked: a field it reads, an operator, a parameter rule's name.</summary>
    public static IReadOnlyList<Rule> Rules(JsonNode? n)
    {
        if (n is not JsonArray a) return [];
        var list = new List<Rule>();
        foreach (var r in a.OfType<JsonObject>())
        {
            var field = (string?)r["field"] ?? throw new CommandException("A rule needs a field.");
            var op = (string?)r["op"] ?? "equals";
            if (!Fields.Contains(field)) throw new CommandException($"„{field}” is not a field a filter reads.");
            if (!Ops.Contains(op)) throw new CommandException($"„{op}” is not an operator.");
            var name = ((string?)r["name"])?.Trim();
            var named = field is "param" or "field";
            if (named && string.IsNullOrEmpty(name)) throw new CommandException(field == "param" ? "A parameter rule names its parameter." : "A field rule names its field.");
            if (field == "field" && ElementFields.FormulaOf(name!) is { } formula) ParamFormulas.Check(formula);
            var values = op is "oneOf" or "noneOf" ? ValuesOf(r["values"]) ?? (ValueText(r["value"]) is { } v ? v.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList() : []) : null;
            list.Add(new Rule(field, op, values != null ? null : ValueText(r["value"]), named ? name : null, values));
        }
        return list;
    }

    /// <summary>A rule as a condition of Lapin's one filter: a facet <c>@field</c>, <c>param</c> a field read as text (<c>@text:</c>), <c>field</c> the field itself.</summary>
    public static FieldCondition Condition(Rule r) => new(
        r.Field switch { "param" => ElementFacets.TextPrefix + r.Name, "field" => r.Name ?? "", _ => "@" + r.Field },
        FieldCondition.Normal(r.Op) ?? "=", r.Value, r.Values);

    /// <summary>Rules as Lapin's one filter: all of them, or any.</summary>
    public static ElementFilter ToFilter(IReadOnlyList<Rule> rules, bool any) => ElementFilter.All with { Conditions = [.. rules.Select(Condition)], Any = any };

    /// <summary>
    /// Matching on one model, what it reads read once: <paramref name="q"/> with the geometry (Quick select) lets <c>field</c>
    /// rules read quantities and the materials of an element's parts; a <see cref="ModelQuery"/> (a view's graphics) does not.
    /// </summary>
    public sealed class Matcher(IQueryContext q)
    {
        readonly ElementFilter.Context ctx = new(q);
        readonly Dictionary<Filter, ElementFilter> filters = new(ReferenceEqualityComparer.Instance);

        public bool Matches(Entity e, ElementFilter f) => f.Matches(ctx, e, new ElementFields(q, e, ctx.Units));

        /// <summary>Whether an element passes a filter: all its rules (or any); a filter without rules takes nothing.</summary>
        public bool Matches(Entity e, Filter f)
        {
            if (f.Rules.Count == 0) return false;
            if (!filters.TryGetValue(f, out var u)) filters[f] = u = ToFilter(f.Rules, f.Any);
            return Matches(e, u);
        }
    }

    public static bool Matches(IModelView m, Entity e, Rule r) => new Matcher(new ModelQuery(m)).Matches(e, ToFilter([r], false));

    /// <summary>Whether an element passes a filter: all its rules (or any); a filter without rules takes nothing.</summary>
    public static bool Matches(IModelView m, Entity e, Filter f) => new Matcher(new ModelQuery(m)).Matches(e, f);

    /// <summary>The elements a filter or Quick select looks at: those on a storey (a door or a window on its wall's), not the openings.</summary>
    public static bool OnStorey(IModelView m, Entity e) => e.Class != IfcClass.OpeningElement && Spatial.StoreyOf(m, e, followHost: true) != null;

    static void Save(ICommandContext ctx, IEnumerable<Filter> all)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var pa = project.CloneAttrs();
        pa[Key] = new JsonArray(all.Select(f => (JsonNode)Write(f)).ToArray());
        ctx.Replace(project.With(attrs: pa));
    }

    public static void Register(IModuleRegistry r)
    {
        r.Query("viewFilter.list", (q, _) => new JsonArray(All(q.Model).Select(f => (JsonNode)Write(f)).ToArray()));

        // How many elements on storeys a filter takes now (the editor's count).
        r.Query("viewFilter.count", (q, a) =>
        {
            var f = All(q.Model).FirstOrDefault(x => x.Id == (string?)a["id"]) ?? throw new CommandException("There is no such filter.");
            // Read as a view reads it (no geometry), so the count is what the views show.
            var matcher = new Matcher(new ModelQuery(q.Model));
            return q.Model.Entities.Count(e => OnStorey(q.Model, e) && matcher.Matches(e, f));
        });

        r.Command("viewFilter.create", "New view filter", (ctx, a) =>
        {
            var all = All(ctx.Model).ToList();
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : "Filter";
            for (var k = 2; all.Any(x => x.Name == name); k++) name = $"{((string?)a["name"])?.Trim() ?? "Filter"} {k}";
            var f = new Filter(ctx.NewId(), name, (string?)a["match"] == "any", Rules(a["rules"]));
            all.Add(f);
            Save(ctx, all);
            return new JsonObject { ["id"] = f.Id };
        });

        r.Command("viewFilter.update", "Change view filter", (ctx, a) =>
        {
            var all = All(ctx.Model).ToList();
            var i = all.FindIndex(x => x.Id == (string?)a["id"]);
            if (i < 0) throw new CommandException("There is no such filter.");
            var f = all[i];
            if (((string?)a["name"])?.Trim() is { Length: > 0 } name) f = f with { Name = name };
            if ((string?)a["match"] is { } match) f = f with { Any = match == "any" };
            if (a.ContainsKey("rules")) f = f with { Rules = Rules(a["rules"]) };
            all[i] = f;
            Save(ctx, all);
            return null;
        });

        // A filter gone: the views that used it forget it (a row naming no filter is left out anyway).
        r.Command("viewFilter.delete", "Delete view filter", (ctx, a) =>
        {
            var id = (string?)a["id"];
            var all = All(ctx.Model).ToList();
            if (all.All(x => x.Id != id)) throw new CommandException("There is no such filter.");
            Save(ctx, all.Where(x => x.Id != id));
            return null;
        });
    }
}
