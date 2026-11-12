using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Parameters worked out by a formula (docs/plan-user-extensibility.md §5.3): a parameter of the user's given a <c>formula</c>
/// instead of a value (<c>Height * 2</c>, <c>round(NetArea * 1.1; 2)</c>, <c>if(IsExternal; 0.18; 0.35)</c>) holds no value of its own:
/// it is worked out on every ask (never stored, as everything that can be worked out), from the element's other parameters and its
/// type's, its own numbers (height, width…) and, where the caller has them, its quantities (Length, NetArea, NetVolume…). A
/// formula naming another formula reads it worked out, to a depth; one that does not read gives no value, its reason told.
/// </summary>
public static class ParamFormulas
{
    public const string Key = "formula";
    const int MaxDepth = 16;

    public static bool IsFormula(JsonObject p) => (string?)p[Key] is { Length: > 0 };

    /// <summary>A formula as a command gives it, checked that it reads (its names are read at its evaluation).</summary>
    public static string Check(string formula)
    {
        var f = formula.Trim();
        if (f.Length == 0) throw new CommandException("A formula cannot be empty.");
        var (_, error) = Expr.TryEval(f, _ => 1);
        if (error != null) throw new CommandException(error);
        return f;
    }

    /// <summary>
    /// What a formula's names read on <paramref name="e"/>: its parameters, then its type's (squashed names: „Fire rating” is FireRating),
    /// worked out when they are formulas; its numeric attributes and its type's; then <paramref name="more"/> (its quantities).
    /// </summary>
    static Func<string, double?> Names(IModelView m, Entity e, Entity? type, Func<string, double?>? more, int depth)
    {
        return name =>
        {
            var key = TagText.Squash(name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name);
            foreach (var owner in new[] { e, type })
            {
                if (owner?.Attrs["params"] is not JsonArray list) continue;
                var p = list.OfType<JsonObject>().FirstOrDefault(x => TagText.Squash((string?)x["name"] ?? "") == key);
                if (p == null) continue;
                if (IsFormula(p)) return depth < MaxDepth ? Evaluate(m, e, type, p, more, depth + 1).Value : null;
                if (p["value"] is JsonValue v)
                {
                    if (v.TryGetValue<bool>(out var b)) return b ? 1 : 0;
                    if (Json.Number(v) is { } n) return n;
                    if (v.TryGetValue<string>(out var s) && LiveText.Number(s) is { } sn) return sn;
                }
            }
            foreach (var owner in new[] { e, type })
                if (owner?.Attrs.FirstOrDefault(kv => TagText.Squash(kv.Key) == key).Value is JsonValue av && Json.Number(av) is { } an) return an;
            return more?.Invoke(name) ?? more?.Invoke(key);
        };
    }

    /// <summary>A formula parameter worked out: its value, or null with why.</summary>
    public static (double? Value, string? Error) Evaluate(IModelView m, Entity e, Entity? type, JsonObject p, Func<string, double?>? more = null, int depth = 0) =>
        Expr.TryEval((string?)p[Key] ?? "", Names(m, e, type, more, depth));

    /// <summary>The quantities a query can give an element, by name (case and spaces aside): the first of that name among its sets.</summary>
    public static Func<string, double?> Quantities(IQueryContext q, string id)
    {
        Dictionary<string, double>? map = null;
        return name =>
        {
            map ??= q.Quantities(id).SelectMany(s => s.Quantities).GroupBy(x => TagText.Squash(x.Name)).ToDictionary(g => g.Key, g => g.First().Value);
            return map.TryGetValue(TagText.Squash(name), out var v) ? v : null;
        };
    }

    /// <summary>An element's parameters with each formula's value worked out (in a copy; the model keeps its formula alone).</summary>
    public static List<JsonObject> Resolved(IModelView m, Entity? e, Entity? type, Func<string, double?>? more = null)
    {
        if (e?.Attrs["params"] is not JsonArray list) return [];
        var o = new List<JsonObject>();
        foreach (var p in list.OfType<JsonObject>())
        {
            if (!IsFormula(p)) { o.Add(p); continue; }
            var copy = (JsonObject)p.DeepClone();
            var (value, _) = Evaluate(m, e, type, p, more);
            // A yes/no formula is yes when it is not zero.
            copy["value"] = value is not { } v ? null : (string?)p["kind"] == "bool" ? JsonValue.Create(v != 0) : JsonValue.Create(Math.Round(v, 9));
            o.Add(copy);
        }
        return o;
    }

    public static void Register(IModuleRegistry r)
    {
        // An element's parameters worked out: each formula's value or why it has none, its quantities read too.
        r.Query("params.evaluate", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which element?");
            var type = q.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType);
            var quantities = Quantities(q, e.Id);
            var o = new JsonArray();
            foreach (var p in (e.Attrs["params"] as JsonArray ?? []).OfType<JsonObject>().Where(IsFormula))
            {
                var (value, error) = Evaluate(q.Model, e, type, p, quantities);
                o.Add((JsonNode)new JsonObject { ["name"] = (string?)p["name"], ["pset"] = (string?)p["pset"], ["formula"] = (string?)p[Key], ["value"] = value is { } v ? Math.Round(v, 9) : null, ["error"] = error });
            }
            return new JsonObject { ["params"] = o };
        });

        // A formula tried on an element before it is set: its value, or why it does not read.
        r.Query("params.tryFormula", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which element?");
            var type = q.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType);
            var p = new JsonObject { ["name"] = "?", [Key] = (string?)a["formula"] ?? "" };
            var (value, error) = Evaluate(q.Model, e, type, p, Quantities(q, e.Id));
            return new JsonObject { ["value"] = value is { } v ? Math.Round(v, 9) : null, ["error"] = error, ["text"] = value is { } t ? t.ToString("0.###", CultureInfo.InvariantCulture) : null };
        });
    }
}
