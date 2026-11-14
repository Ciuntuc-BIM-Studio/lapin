using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Rule-based view filters as in Revit (docs/plan-visibility-graphics.md, Filters): the project's
/// (<c>viewFilters</c>), each a name, <c>all</c> or <c>any</c> of its rules, and rules
/// <c>{field, op, value}</c>. A view lists the filters it uses in order in its graphics
/// (<c>filters: [{id, hidden, …}]</c>); <see cref="VisibilityGraphics"/> applies them.
/// </summary>
public static class ViewFilters
{
    public const string Key = "viewFilters";

    /// <summary>What a rule reads of an element; <c>param</c> names a parameter (the user's, a property set's) or any stored attribute in <c>name</c>.</summary>
    public static readonly string[] Fields = ["class", "category", "type", "name", "material", "storey", "group", "link", "phase", "predefinedType", "param"];
    public static readonly string[] Ops = ["equals", "notEquals", "contains", "notContains", "beginsWith", "greater", "less", "between", "hasValue", "hasNoValue"];

    public sealed record Rule(string Field, string Op, string? Value, string? Name);
    public sealed record Filter(string Id, string Name, bool Any, IReadOnlyList<Rule> Rules);

    public static IReadOnlyList<Filter> All(IModelView m) =>
        (Spatial.Project(m)?.Attrs[Key] as JsonArray)?.OfType<JsonObject>().Select(Read).Where(f => f.Id.Length > 0).ToList() ?? [];

    static Filter Read(JsonObject o) => new(
        (string?)o["id"] ?? "", (string?)o["name"] ?? "Filter", (string?)o["match"] == "any",
        (o["rules"] as JsonArray)?.OfType<JsonObject>().Select(r => new Rule((string?)r["field"] ?? "class", (string?)r["op"] ?? "equals", ValueText(r["value"]), (string?)r["name"])).ToList() ?? []);

    static string? ValueText(JsonNode? n) => n switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v when Json.Number(v) is { } d => d.ToString(CultureInfo.InvariantCulture),
        _ => n.ToJsonString(),
    };

    static JsonObject Write(Filter f) => new()
    {
        ["id"] = f.Id, ["name"] = f.Name, ["match"] = f.Any ? "any" : "all",
        ["rules"] = new JsonArray(f.Rules.Select(r =>
        {
            var o = new JsonObject { ["field"] = r.Field, ["op"] = r.Op };
            if (r.Value != null) o["value"] = r.Value;
            if (r.Name != null) o["name"] = r.Name;
            return (JsonNode)o;
        }).ToArray()),
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
            if (field == "param" && string.IsNullOrEmpty(name)) throw new CommandException("A parameter rule names its parameter.");
            list.Add(new Rule(field, op, ValueText(r["value"]), field == "param" ? name : null));
        }
        return list;
    }

    /// <summary>The values an element has for a field (several for its materials).</summary>
    public static IEnumerable<string> Values(IModelView m, Entity e, Rule r)
    {
        switch (r.Field)
        {
            case "class": yield return e.Class; break;
            case "category": if (LineStyles.CategoryOf(e.Class) is { } c) yield return c; break;
            case "type": if (m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } t) yield return t.Name; break;
            case "name": yield return e.Name; break;
            case "storey": if (Spatial.StoreyOf(m, e.Id) is { } s) yield return s.Name; break;
            case "group": if (Groups.Of(m, e.Id) is { } g) yield return g.Name; break;
            case "link": if (Links.Of(m, e) is { } l) yield return l.Name; break;
            case "phase": if (e.Str("phaseCreated") is { } ph) yield return m.Get(ph)?.Name ?? ph; break;
            case "predefinedType": if (e.PredefinedType is { Length: > 0 } pt) yield return pt; break;
            case "material":
                if (LayerSets.BandsOf(m, e.Id) is { } bands)
                    foreach (var b in bands) if (m.Get(b.Material) is { } bm) yield return bm.Name;
                if (m.RelatingOf(e.Id, IfcClass.RelAssociatesMaterial) is { Class: IfcClass.Material } mat) yield return mat.Name;
                break;
            case "param":
                if (TagText.Value(m, e, r.Name ?? "") is { Length: > 0 } v) yield return v;
                break;
        }
    }

    static double? Number(string s)
    {
        var t = s.TrimStart();
        var n = 0;
        while (n < t.Length && (char.IsDigit(t[n]) || t[n] is '.' or '-' or '+' or ',')) n++;
        return n > 0 && double.TryParse(t[..n].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    /// <summary>A range written "min..max" (either end empty: open); null ends both when it is not one.</summary>
    static (double? Lo, double? Hi) Range(string s)
    {
        var k = s.IndexOf("..", StringComparison.Ordinal);
        if (k < 0) return (Number(s), Number(s));
        var (a, b) = (s[..k].Trim(), s[(k + 2)..].Trim());
        return (a.Length > 0 ? Number(a) : null, b.Length > 0 ? Number(b) : null);
    }

    public static bool Matches(IModelView m, Entity e, Rule r)
    {
        var values = Values(m, e, r).ToList();
        var want = r.Value ?? "";
        bool Eq(string v) => string.Equals(v.Trim(), want.Trim(), StringComparison.OrdinalIgnoreCase)
            || (Number(v) is { } a && Number(want) is { } b && Math.Abs(a - b) < 1e-9);
        bool Has(string v) => v.Contains(want, StringComparison.OrdinalIgnoreCase);
        return r.Op switch
        {
            "equals" => values.Any(Eq),
            "notEquals" => !values.Any(Eq),
            "contains" => values.Any(Has),
            "notContains" => !values.Any(Has),
            "beginsWith" => values.Any(v => v.StartsWith(want, StringComparison.OrdinalIgnoreCase)),
            "greater" => values.Any(v => Number(v) is { } a && Number(want) is { } b && a > b),
            "less" => values.Any(v => Number(v) is { } a && Number(want) is { } b && a < b),
            // A range, both ends in it: "min..max", either end left empty for none (Quick select, forum topic 73).
            "between" => Range(want) is var (lo, hi) && values.Any(v => Number(v) is { } a && (lo is not { } l || a >= l - 1e-9) && (hi is not { } h || a <= h + 1e-9)),
            "hasValue" => values.Any(v => v.Length > 0),
            "hasNoValue" => !values.Any(v => v.Length > 0),
            _ => false,
        };
    }

    /// <summary>Whether an element passes a filter: all its rules (or any); a filter without rules takes nothing.</summary>
    public static bool Matches(IModelView m, Entity e, Filter f) =>
        f.Rules.Count > 0 && (f.Any ? f.Rules.Any(r => Matches(m, e, r)) : f.Rules.All(r => Matches(m, e, r)));

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
            return q.Model.Entities.Count(e => Spatial.StoreyOf(q.Model, e.Id) != null && Matches(q.Model, e, f));
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
