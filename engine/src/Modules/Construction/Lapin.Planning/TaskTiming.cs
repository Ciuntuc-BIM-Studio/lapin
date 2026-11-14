using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// A task timed by its elements' fields (docs/plan-user-extensibility.md §12: an office's own naming): its days read on each
/// element from a field or a formula (<c>durationField</c>: <c>Pset_Firma.Durata</c>, <c>= Pset_Firma.Ore / 8</c>) and summed;
/// or its quantity read from a field in place of the built-in measure (<c>quantityField</c>), timed by its norm as the
/// generator times a step. Kept on the task, so Planning → Retime from fields works it out again after the model changed;
/// a task with neither keeps its duration.
/// </summary>
public static class TaskTiming
{
    public const string DurationKey = "durationField", QuantityKey = "quantityField", FromField = "field";
    /// <summary>The most tasks a step is split into by its fields' values.</summary>
    public const int MaxSplits = 200;

    /// <summary>A field's numbers summed over elements; null when none has one.</summary>
    public static double? Sum(TaskElements links, IEnumerable<string> ids, string field)
    {
        double sum = 0;
        var any = false;
        foreach (var id in ids)
            if (links.Field(id, field).Number is { } n && double.IsFinite(n)) { sum += n; any = true; }
        return any ? sum : null;
    }

    /// <summary>The working days a field gives the elements, summed and rounded up (at least one); null when none has it.</summary>
    public static int? Days(TaskElements links, IEnumerable<string> ids, string field) =>
        Sum(links, ids, field) is { } d && d > 1e-9 ? Math.Max(1, (int)Math.Ceiling(d - 1e-9)) : null;

    /// <summary>A field's name as a task's name shows it: the set it is in left out (<c>Pset_Firma.Sector</c>: Sector), a scope's kept.</summary>
    public static string FieldName(string field)
    {
        var f = field.Trim();
        if (f.StartsWith('=') || f.Contains('{')) return f;
        var dot = f.IndexOf('.');
        return dot > 0 && !ParamScopes.IsScope(f[..dot]) ? f[(f.LastIndexOf('.') + 1)..] : f;
    }

    /// <summary>
    /// A split value as a task's name shows it: the value when it says what it is („Sector A”), else with its field's name
    /// before it („Sector 2” of Pset_Firma.Sector = 2); „no Sector” when the element has none.
    /// </summary>
    public static string ValueLabel(string field, FieldValue v)
    {
        var name = FieldName(field);
        if (!v.Exists) return $"no {name}";
        var shown = v.Shown.Trim();
        if (name.StartsWith('=') || shown.StartsWith(name, StringComparison.OrdinalIgnoreCase)) return shown;
        return shown.Length <= 3 || FieldValue.StrictNumber(shown) != null ? $"{name} {shown}" : shown;
    }

    /// <summary>
    /// A task's duration (and quantity) worked out again from its fields into its attributes: its days from its duration field,
    /// else its quantity from its quantity field × its norm's hours ÷ (crew × hours a day). Whether it had either to work from.
    /// </summary>
    public static bool Apply(JsonObject attrs, IReadOnlyDictionary<string, ulong> elements, TaskElements links, Norms.Table norms, double hoursPerDay)
    {
        if ((string?)attrs[DurationKey] is { Length: > 0 } df)
        {
            if (Days(links, elements.Keys, df) is not { } days) return true;
            attrs["duration"] = days;
            attrs["durationFrom"] = FromField;
            attrs.Remove("milestone");
            attrs.Remove("quantity");
            attrs.Remove("unit");
            return true;
        }
        if ((string?)attrs[QuantityKey] is not { Length: > 0 } qf) return false;
        if (norms.Find((string?)attrs["norm"]) is not { } norm) return true;
        var basis = Norms.BasisOf(norm.Unit);
        if (Sum(links, elements.Keys, qf) is not { } sum) return true;
        var qty = sum * (basis?.Factor ?? 1);
        if (qty <= 1e-9) return true;
        var crew = Json.Number(attrs["crew"]) is { } c && c >= 1 ? (int)Math.Round(c) : norm.Crew ?? 1;
        attrs["duration"] = Math.Max(1, (int)Math.Ceiling(qty * norm.Hours / (crew * Math.Max(0.5, hoursPerDay)) - 1e-9));
        attrs["durationFrom"] = "norm";
        attrs["quantity"] = Math.Round(qty, 3);
        attrs["unit"] = norm.Unit;
        attrs["crew"] = crew;
        attrs.Remove("milestone");
        return true;
    }
}
