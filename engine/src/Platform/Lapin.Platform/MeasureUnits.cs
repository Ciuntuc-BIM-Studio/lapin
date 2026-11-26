namespace Lapin.Platform;

/// <summary>
/// The one reading of a unit of measure (a price list's, a productivity norm's) and of the quantity names a measure is
/// read by, shared by the cost and planning modules so an estimate and a schedule measure an element alike.
/// </summary>
public static class MeasureUnits
{
    /// <summary>A unit's measure (volume, area, length, count, weight) and the factor the model's SI value is multiplied by to be in it; null when Lapin cannot measure it.</summary>
    public static (string Basis, double Factor)? BasisOf(string? unit)
    {
        var u = (unit ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace(".", "");
        return u switch
        {
            "m3" or "m³" or "mc" or "cum" or "cbm" => ("volume", 1),
            "m2" or "m²" or "mp" or "sqm" => ("area", 1),
            "100m2" or "100mp" or "100m²" => ("area", 0.01),
            "m" or "ml" or "lm" or "rm" => ("length", 1),
            "100m" or "100ml" => ("length", 0.01),
            "buc" or "pcs" or "pc" or "nr" or "no" or "u" or "ea" or "each" or "stk" or "szt" or "un" or "pz" or "piece" or "pièce" or "set" or "cad" => ("count", 1),
            "kg" => ("weight", 1),
            "t" or "to" or "tonne" or "tona" or "tone" => ("weight", 0.001),
            _ => null,
        };
    }

    /// <summary>The time units an article's quantity may be multiplied by (docs/plan-4d5d.md, "× duration"): months, working days, calendar days, working hours.</summary>
    public const string Months = "months", WorkingDays = "workingDays", Days = "days", Hours = "hours";
    /// <summary>Weeks of the calendar (its days over seven): the rate of a time-related estimate item may be by the week (not a unit word an article's unit is read as).</summary>
    public const string Weeks = "weeks";

    static readonly Dictionary<string, string> TimeWords = new()
    {
        ["month"] = Months, ["months"] = Months, ["mo"] = Months, ["mth"] = Months, ["mon"] = Months, ["luna"] = Months, ["lună"] = Months, ["luni"] = Months,
        ["mese"] = Months, ["mesi"] = Months, ["mois"] = Months, ["mes"] = Months, ["meses"] = Months, ["monat"] = Months, ["monate"] = Months, ["miesiąc"] = Months, ["miesiac"] = Months,
        ["workingday"] = WorkingDays, ["workingdays"] = WorkingDays, ["workday"] = WorkingDays, ["workdays"] = WorkingDays, ["wd"] = WorkingDays,
        ["zilucratoare"] = WorkingDays, ["zilucrătoare"] = WorkingDays, ["zilucr"] = WorkingDays, ["giornilavorativi"] = WorkingDays, ["arbeitstag"] = WorkingDays, ["arbeitstage"] = WorkingDays,
        ["day"] = Days, ["days"] = Days, ["d"] = Days, ["zi"] = Days, ["zile"] = Days, ["giorno"] = Days, ["giorni"] = Days, ["gg"] = Days, ["jour"] = Days, ["jours"] = Days,
        ["día"] = Days, ["dia"] = Days, ["días"] = Days, ["dias"] = Days, ["tag"] = Days, ["tage"] = Days, ["dzień"] = Days, ["dzien"] = Days, ["dni"] = Days,
        ["h"] = Hours, ["hr"] = Hours, ["hrs"] = Hours, ["hour"] = Hours, ["hours"] = Hours, ["ora"] = Hours, ["oră"] = Hours, ["ore"] = Hours, ["heure"] = Hours, ["heures"] = Hours,
        ["hora"] = Hours, ["horas"] = Hours, ["std"] = Hours, ["stunde"] = Hours, ["stunden"] = Hours, ["godz"] = Hours,
    };

    static string Fold(string? unit) => (unit ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace(".", "");

    /// <summary>
    /// A unit split into its measure and its time (<c>m × month</c>: <c>m</c> and months; <c>buc/zi</c>: <c>buc</c> and
    /// days; <c>month</c>: no measure and months). Time is null when the unit has none; the measure is then the unit as it is.
    /// A unit of time with no measure before it prices a count of the element (cad × month).
    /// </summary>
    public static (string Measure, string? Time) SplitTime(string? unit)
    {
        var u = Fold(unit);
        if (TimeWords.TryGetValue(u, out var alone)) return ("", alone);
        var at = u.LastIndexOfAny(['×', 'x', '*', '/', '·', '-']);
        if (at > 0 && TimeWords.TryGetValue(u[(at + 1)..], out var time))
        {
            var measure = u[..at];
            if (BasisOf(measure) != null) return (measure, time);
        }
        return (unit ?? "", null);
    }

    /// <summary>The time a unit is priced by (months, workingDays, days, hours), null when it has none.</summary>
    public static string? TimeOf(string? unit) => SplitTime(unit).Time;

    /// <summary>A unit's measure and factor, its time left aside (<c>m × month</c> is a length; <c>month</c> alone a count); null when Lapin cannot measure it.</summary>
    public static (string Basis, double Factor)? BasisWithTime(string? unit)
    {
        var (measure, time) = SplitTime(unit);
        if (time == null) return BasisOf(unit);
        return measure.Length == 0 ? ("count", 1) : BasisOf(measure);
    }

    /// <summary>The unit a measure is shown in.</summary>
    public static string UnitOf(string basis) => basis switch { "volume" or "grossVolume" => "m³", "area" or "grossArea" => "m²", "length" => "m", "weight" => "kg", _ => "pcs" };
}

/// <summary>The quantities an estimate's or a schedule's measure is read from, in the order they are looked for (the first an element's set has wins); <see cref="QuantityNames"/> is the takeoff's and the formulas' common names.</summary>
public static class MeasureQuantities
{
    static readonly Dictionary<string, string[]> Names = new()
    {
        ["volume"] = ["NetVolume", "Volume", "GrossVolume"],
        ["grossVolume"] = ["GrossVolume", "NetVolume", "Volume"],
        ["area"] = ["NetSideArea", "NetArea", "NetFloorArea", "NetSurfaceArea", "ContactArea", "Area", "GrossSideArea", "GrossArea", "GrossFloorArea", "GrossSurfaceArea", "OuterSurfaceArea"],
        ["grossArea"] = ["GrossSideArea", "GrossArea", "GrossFloorArea", "GrossSurfaceArea", "ContactArea", "Area", "NetSideArea", "NetArea", "NetFloorArea", "NetSurfaceArea"],
        ["length"] = ["Length", "Perimeter", "FormedLength"],
        ["weight"] = ["NetWeight", "GrossWeight", "Weight"],
    };

    /// <summary>The names a measure reads (a measure Lapin does not name is a quantity's own name).</summary>
    public static IReadOnlyList<string> Of(string basis) => Names.TryGetValue(basis, out var n) ? n : [basis];

    /// <summary>The value of the first of the measure's names the quantities have; null when they have none.</summary>
    public static double? Pick(IReadOnlyList<Quantity> quantities, string basis)
    {
        foreach (var name in Of(basis)) if (quantities.FirstOrDefault(q => q.Name == name) is { } hit) return hit.Value;
        return null;
    }
}
