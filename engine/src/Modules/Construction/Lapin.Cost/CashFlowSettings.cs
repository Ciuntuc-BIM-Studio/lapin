using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Cost;

/// <summary>
/// The terms of the contract the cash flow follows (docs/plan-4d5d.md, cash flow), kept on the estimate (IfcCostSchedule)
/// as its attribute <c>cashFlow</c>; the only thing stored, the cash flow itself is worked out on every ask. Every value
/// is the user's: the defaults are Romanian practice as Lapin reads it (monthly statements of works at the month's end,
/// paid in 30 days, 5 % retention released half at the taking-over and half after the defects period), indicative only —
/// the contract decides.
/// </summary>
/// <param name="Basis">The value certified: <c>net</c> (the estimate without VAT: direct costs, overheads and profit), <c>direct</c> (the direct costs) or <c>contract</c> (<see cref="ContractValue"/>, the client's price).</param>
/// <param name="CostBasis">What the contractor pays out: <c>direct</c> (the direct costs) or <c>withOverheads</c> (all but the profit).</param>
/// <param name="CostSide">Whether the payments out (suppliers, subcontractors, labour) are shown.</param>
/// <param name="SupplierDays">Days from a cost incurred to its payment.</param>
/// <param name="AdvancePercent">The advance, a percentage of the contract value, recovered pro rata from each certificate (the last takes the rest).</param>
/// <param name="AdvanceDate">The day the advance is paid (yyyy-MM-dd); else the task's start; else the schedule's start.</param>
/// <param name="RetentionPercent">The part of each certificate held back.</param>
/// <param name="RetentionAtTakeover">The percentage of the retention released at the taking-over; the rest after the defects period.</param>
/// <param name="DefectsMonths">The defects (guarantee) period, months from the taking-over.</param>
/// <param name="TakeoverDate">The taking-over day; else the task's finish; else the schedule's finish.</param>
/// <param name="Cycle">The certificates: <c>monthly</c> (on <see cref="CertificateDay"/>) or <c>weekly</c> (each Sunday).</param>
/// <param name="CertificateDay">The day of the month a certificate is made (1–28; 0: the month's last day).</param>
/// <param name="PaymentDays">Days from a certificate (or a retention release) to its payment.</param>
/// <param name="Vat">Whether VAT (the estimate's rate) is paid with each payment, in and out.</param>
/// <param name="Indicative">Nothing was set: these are Lapin's defaults.</param>
public sealed record CashFlowSettings(
    string Basis, double? ContractValue, string CostBasis, bool CostSide, int SupplierDays,
    double AdvancePercent, string? AdvanceDate, string? AdvanceTask,
    double RetentionPercent, double RetentionAtTakeover, int DefectsMonths, string? TakeoverDate, string? TakeoverTask,
    string Cycle, int CertificateDay, int PaymentDays, bool Vat, bool Indicative)
{
    public const string Attr = "cashFlow";
    public static readonly string[] Bases = ["net", "direct", "contract"];
    public static readonly string[] CostBases = ["direct", "withOverheads"];
    public static readonly string[] Cycles = ["monthly", "weekly"];

    /// <summary>Lapin's defaults (indicative: Romanian practice, the contract decides).</summary>
    public static readonly CashFlowSettings Defaults = new("net", null, "withOverheads", true, 30, 0, null, null, 5, 50, 24, null, null, "monthly", 0, 30, false, true);

    /// <summary>The terms with nothing held back, advanced or delayed: the cash in is the value certified the day it is certified.</summary>
    public static readonly CashFlowSettings None = Defaults with { CostBasis = "direct", SupplierDays = 0, RetentionPercent = 0, PaymentDays = 0, Indicative = false };

    public static CashFlowSettings Of(Entity estimate) => Read(estimate.Attrs[Attr] as JsonObject);

    /// <summary>The terms written in an object (the attribute); what it lacks or has wrong is the default.</summary>
    public static CashFlowSettings Read(JsonObject? o)
    {
        if (o == null) return Defaults;
        var d = Defaults;
        double Num(string k, double def, double min, double max) => Json.Number(o[k]) is { } v && double.IsFinite(v) && v >= min && v <= max ? v : def;
        string? Text(string k) => (string?)(o[k] as JsonValue) is { Length: > 0 } s ? s : null;
        bool Flag(string k, bool def) => o[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : def;
        string Pick(string k, string[] of, string def) => Text(k) is { } s && of.Contains(s) ? s : def;
        return new CashFlowSettings(
            Pick("basis", Bases, d.Basis), Json.Number(o["contractValue"]) is { } cv && cv >= 0 && double.IsFinite(cv) ? cv : null,
            Pick("costBasis", CostBases, d.CostBasis), Flag("costSide", d.CostSide), (int)Num("supplierDays", d.SupplierDays, 0, 3650),
            Num("advance", d.AdvancePercent, 0, 100), Text("advanceDate"), Text("advanceTask"),
            Num("retention", d.RetentionPercent, 0, 100), Num("retentionAtTakeover", d.RetentionAtTakeover, 0, 100), (int)Num("defectsMonths", d.DefectsMonths, 0, 600),
            Text("takeoverDate"), Text("takeoverTask"),
            Pick("cycle", Cycles, d.Cycle), (int)Num("certificateDay", d.CertificateDay, 0, 28), (int)Num("paymentDays", d.PaymentDays, 0, 3650), Flag("vat", d.Vat), false);
    }

    /// <summary>The fields a command may set, each with its check (the message is the user's).</summary>
    public static JsonObject Merge(JsonObject? current, JsonObject args, IModelView model)
    {
        var o = current?.DeepClone() as JsonObject ?? [];
        void Range(string k, double min, double max, string what, bool whole = false)
        {
            if (!args.ContainsKey(k)) return;
            if (args[k] is null) { o.Remove(k); return; }
            if (Json.Number(args[k]) is not { } v || !double.IsFinite(v) || v < min || v > max || whole && v != Math.Floor(v))
                throw new CommandException($"The {what} is {(whole ? "a whole number" : "a number")} from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}.");
            o[k] = v;
        }
        void Choice(string k, string[] of, string what)
        {
            if (!args.ContainsKey(k)) return;
            if (args[k] is null) { o.Remove(k); return; }
            o[k] = (string?)args[k] is { } s && of.Contains(s) ? s : throw new CommandException($"The {what} is one of: {string.Join(", ", of)}.");
        }
        void Day(string k, string what)
        {
            if (!args.ContainsKey(k)) return;
            if (args[k] is null || (string?)args[k] is "") { o.Remove(k); return; }
            o[k] = (string?)args[k] is { } s && DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? s : throw new CommandException($"The {what} is a date (yyyy-MM-dd).");
        }
        void Task(string k)
        {
            if (!args.ContainsKey(k)) return;
            if (args[k] is null || (string?)args[k] is "") { o.Remove(k); return; }
            o[k] = (string?)args[k] is { } s && model.Get(s)?.Class == "IfcTask" ? s : throw new CommandException("That is not a task of the work schedule.");
        }
        void Flag(string k)
        {
            if (!args.ContainsKey(k)) return;
            if (args[k] is null) { o.Remove(k); return; }
            o[k] = args[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : throw new CommandException($"„{k}” is true or false.");
        }
        Choice("basis", Bases, "value certified");
        Range("contractValue", 0, 1e13, "contract value");
        Choice("costBasis", CostBases, "cost paid out");
        Flag("costSide");
        Range("supplierDays", 0, 3650, "suppliers' payment term in days", true);
        Range("advance", 0, 100, "advance (a percentage)");
        Day("advanceDate", "advance's date");
        Task("advanceTask");
        Range("retention", 0, 100, "retention (a percentage)");
        Range("retentionAtTakeover", 0, 100, "part of the retention released at the taking-over (a percentage)");
        Range("defectsMonths", 0, 600, "defects period in months", true);
        Day("takeoverDate", "taking-over date");
        Task("takeoverTask");
        Choice("cycle", Cycles, "certificates' cycle");
        Range("certificateDay", 0, 28, "certificates' day of the month (0: its last day)", true);
        Range("paymentDays", 0, 3650, "payment term in days", true);
        Flag("vat");
        return o;
    }

    public JsonObject ToJson() => new()
    {
        ["basis"] = Basis, ["contractValue"] = ContractValue, ["costBasis"] = CostBasis, ["costSide"] = CostSide, ["supplierDays"] = SupplierDays,
        ["advance"] = AdvancePercent, ["advanceDate"] = AdvanceDate, ["advanceTask"] = AdvanceTask,
        ["retention"] = RetentionPercent, ["retentionAtTakeover"] = RetentionAtTakeover, ["defectsMonths"] = DefectsMonths,
        ["takeoverDate"] = TakeoverDate, ["takeoverTask"] = TakeoverTask,
        ["cycle"] = Cycle, ["certificateDay"] = CertificateDay, ["paymentDays"] = PaymentDays, ["vat"] = Vat, ["indicative"] = Indicative,
    };

    // ------------------------------------------------------------------ IFC: Lapin_Estimate's properties

    const string Prefix = "CashFlow.";

    /// <summary>The attribute as properties of Lapin_Estimate (<c>CashFlow.Advance</c>…), only the keys the user set.</summary>
    public static IEnumerable<(string Name, string Value)> ToProperties(JsonObject? attr)
    {
        if (attr == null) yield break;
        foreach (var (k, v) in attr)
            if (v is JsonValue jv)
                yield return (Prefix + char.ToUpperInvariant(k[0]) + k[1..],
                    jv.TryGetValue<bool>(out var b) ? (b ? "true" : "false") : Json.Number(jv) is { } n ? n.ToString(CultureInfo.InvariantCulture) : (string?)jv ?? "");
    }

    /// <summary>The attribute read back from Lapin_Estimate's properties; null when it has none. Task references are dropped (the file's tasks may not be read).</summary>
    public static JsonObject? FromProperties(IReadOnlyDictionary<string, string> props)
    {
        var o = new JsonObject();
        foreach (var (name, value) in props)
        {
            if (!name.StartsWith(Prefix, StringComparison.Ordinal) || name.Length <= Prefix.Length) continue;
            var key = char.ToLowerInvariant(name[Prefix.Length]) + name[(Prefix.Length + 1)..];
            if (key is "advanceTask" or "takeoverTask") continue;
            if (value is "true" or "false") o[key] = value == "true";
            else if (key is not ("basis" or "costBasis" or "cycle" or "advanceDate" or "takeoverDate") && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) o[key] = n;
            else o[key] = value;
        }
        return o.Count > 0 ? o : null;
    }
}
