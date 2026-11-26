using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// A placed part's parameters as the element's fields and quantities (R6 of docs/plan-forum-round-1126.md, Fabio Emmolo's request),
/// worked out on every ask from its part's table and its own values, never stored.
///
/// <para><b>Fields.</b> Every published parameter (derived ones too: <c>N = floor(L / 2.5) + 1</c>) is a field named by its key, in
/// the set <see cref="PartIfc.SetName"/> (<c>L</c>, or <c>Lapin_PartParameters.L</c>): field schedules' columns, their filters'
/// conditions, formulas, tags and live text read it as they read a parameter of the user's own. Lengths in metres, angles in
/// degrees, yes/no as 1 or 0, as formulas read them. A parameter the author left unpublished is no field.</para>
///
/// <para><b>Quantities.</b> A parameter marked as a measure (<c>measure</c>: length, count, area, volume) is a quantity of that
/// kind named by its key in the set <see cref="AuthoredQuantities.Set"/>, beside the body's own: the estimate's articles by that
/// measure read it first (a fence of L = 20 m priced at 20 m), schedules list it with the quantities, IFC writes it as an
/// IfcElementQuantity of that name. Marked or not, published or not, as the author chose.</para>
/// </summary>
public static class PartFields
{
    const string Owner = "studio.parts";

    public static void Register()
    {
        foreach (var cls in StudioModule.Classes) DerivedFields.AddNamed(Owner, cls, Of);
    }

    static List<JsonObject> Params(JsonObject def) => [.. (def["params"] as JsonArray ?? []).OfType<JsonObject>()];

    static bool Published(JsonObject p) => p["published"] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : true;

    static (JsonObject Def, PartParams.Resolved Values)? Resolved(IModelView m, Entity e) =>
        Parts.IsInstance(e) && Parts.DefOf(Parts.TypeOf(m, e)) is { } def ? (def, PartParams.Resolve(def, e.Attrs["values"] as JsonObject)) : null;

    /// <summary>The placed part's published parameters as fields, in its table's order (none for anything else).</summary>
    public static IReadOnlyList<NamedField> Of(IModelView m, Entity e)
    {
        if (Resolved(m, e) is not { } res) return [];
        var (def, r) = res;
        var o = new List<NamedField>();
        foreach (var p in Params(def))
        {
            if (!Published(p) || (string?)p["key"] is not { Length: > 0 } key) continue;
            var label = (string?)p["label"] is { Length: > 0 } l ? l : key;
            var kind = (string?)p["kind"] ?? "length";
            var description = PartParams.FormulaOf(p) is { } formula ? $"A parameter of its part, worked out by its formula ({formula})" : "A parameter of its part";
            if (kind == "text")
            {
                if (r.Texts.TryGetValue(key, out var t)) o.Add(new(key, label, kind, null, t, PartIfc.SetName, description));
                continue;
            }
            if (!r.Numbers.TryGetValue(key, out var v)) continue;
            v = Math.Round(v, 9);
            o.Add(new(key, label, kind, v, kind == "bool" ? (v != 0 ? "yes" : "no") : null, PartIfc.SetName, description));
        }
        return o;
    }

    /// <summary>The placed part's parameters marked as measures, as a quantity set; null when none is marked (every part before R6).</summary>
    public static QuantitySet? Quantities(IModelView m, Entity e)
    {
        if (Resolved(m, e) is not { } res) return null;
        var (def, r) = res;
        List<Quantity>? list = null;
        foreach (var p in Params(def))
        {
            if (AuthoredQuantities.KindOf(PartParams.MeasureOf(p)) is not { } kind || (string?)p["key"] is not { Length: > 0 } key) continue;
            if (!r.Numbers.TryGetValue(key, out var v) || !double.IsFinite(v) || r.Problems.ContainsKey(key)) continue;
            (list ??= []).Add(new Quantity(key, Math.Round(v, 9), kind, (string?)p["label"] is { Length: > 0 } l ? l : key));
        }
        return list != null ? new QuantitySet(AuthoredQuantities.Set, list) : null;
    }
}
