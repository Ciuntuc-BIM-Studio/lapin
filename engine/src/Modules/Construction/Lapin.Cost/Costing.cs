using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Cost;

/// <summary>
/// An estimate's settings (on its IfcCostSchedule): its currency (RON by default, or EUR) and the project's rate (RON for
/// one EUR), the rates applied under the direct costs — the contributions on labour (CAM), the overheads (cheltuieli
/// indirecte), the profit, the VAT — the price lists it prices by (none: all the project's, in their order), how its
/// chapters are made (by kind of work, or by storey and kind), whether it shows the other currency's equivalent, and
/// whether its lines come from the model (an imported estimate keeps only its own).
/// </summary>
public sealed record EstimateSettings(string Currency, double EurRate, double LabourTax, double Overheads, double Profit, double Vat,
    IReadOnlyList<string> PriceLists, string Grouping, bool Equivalent, bool Derive)
{
    public const double DefaultRate = 5.0, DefaultLabourTax = 2.25, DefaultOverheads = 10, DefaultProfit = 5, DefaultVat = 21;
    public static readonly string[] Groupings = ["chapter", "storey"];

    public string Other => Currency == "EUR" ? "RON" : "EUR";

    public static EstimateSettings Of(Entity e) => new(
        e.Str("currency") == "EUR" ? "EUR" : "RON",
        e.NumOrNull("eurRate") is { } r && r > 0 ? r : DefaultRate,
        e.NumOrNull("labourTax") ?? DefaultLabourTax,
        e.NumOrNull("overheads") ?? DefaultOverheads,
        e.NumOrNull("profit") ?? DefaultProfit,
        e.NumOrNull("vat") ?? DefaultVat,
        e.Attrs["priceLists"] is JsonArray a ? a.Select(x => (string?)x).OfType<string>().ToList() : [],
        e.Str("grouping") == "storey" ? "storey" : "chapter",
        e.Bool("equivalent"),
        e.Attrs["derive"] is not JsonValue d || !d.TryGetValue<bool>(out var dv) || dv);

    /// <summary>A price in <paramref name="from"/>'s currency in the estimate's.</summary>
    public double Convert(double v, string from) =>
        from == Currency ? v : from == "EUR" && Currency == "RON" ? v * EurRate : from == "RON" && Currency == "EUR" ? v / EurRate : v;

    /// <summary>A value of the estimate in the other currency.</summary>
    public double InOther(double v) => Currency == "RON" ? v / EurRate : v * EurRate;
}

/// <summary>A resource an estimate line consumes (C6–C9): its kind, code, name, unit, the quantity consumed and its unit price.</summary>
public sealed record LineResource(string Kind, string Code, string Name, string Unit, double Quantity, double Price, string? Trade)
{
    public double Value => Money.Round(Quantity * Price);
}

/// <summary>
/// One line of the estimate: an article of a chapter (its code, text and unit as the price list has them, the quantity the
/// model gives it — or the one typed, for a line of the user's — and its unit price split into material, labour,
/// equipment and transport, in the estimate's currency), the elements it measures, whether a price was found.
/// </summary>
public sealed class EstimateLine(string key, string chapter, string code)
{
    public string Key { get; } = key;
    public string Chapter { get; } = chapter;
    public string Code { get; } = code;
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public double Quantity { get; set; }
    public double M { get; set; }
    public double L { get; set; }
    public double E { get; set; }
    public double T { get; set; }
    public double? Hours { get; set; }
    public string? Trade { get; set; }
    public string Basis { get; set; } = "count";
    public bool Priced { get; set; }
    /// <summary>The decimals its quantity is kept to: 3, or 6 for a line a module counted in its article's unit (m² of sheet by the panel, decision 859).</summary>
    public int Decimals { get; set; } = 3;
    /// <summary>The id of a line of the user's (null: derived from the model).</summary>
    public string? Manual { get; set; }
    public HashSet<string> Elements { get; } = [];
    public List<LineResource> Resources { get; } = [];
    /// <summary>What each element gives the line, by the part measured (stage 7: the line's cost is laid on the tasks that build those parts).</summary>
    public List<LineShare> Shares { get; } = [];
    /// <summary>The tasks a line of the user's is done in (stage 7; ids: a summary stands for its tasks).</summary>
    public List<string> Tasks { get; } = [];
    /// <summary>The time its quantity is multiplied by ("× duration": months, workingDays, days, hours); null when none.</summary>
    public string? Time { get; set; }
    /// <summary>How long each element is on site, in <see cref="Time"/>, as the schedule has it (the span of its tasks).</summary>
    public Dictionary<string, double> Durations { get; } = [];
    /// <summary>The elements whose quantity was not multiplied by a duration: no task takes them (or there is no schedule).</summary>
    public HashSet<string> Untimed { get; } = [];
    /// <summary>Why no element of the line has a duration, when the project cannot give one (no Planning module, no schedule).</summary>
    public string? TimeProblem { get; set; }
    /// <summary>
    /// A time-related item of the user's (preliminaries, site management): the time unit its rate is by (days, workingDays,
    /// weeks, months; null: an ordinary line). Its quantity is the time its tasks last (<see cref="Tasks"/>; none: the whole
    /// project) as the schedule has them, so it grows when they slip; with no schedule, the quantity typed.
    /// </summary>
    public string? Per { get; set; }
    /// <summary>The days a time-related item runs over, first and last (null: not one, or no schedule).</summary>
    public (DateOnly From, DateOnly To)? Span { get; set; }

    /// <summary>The duration used, as shown: one value when all its elements have the same, else the least and the most; null when none.</summary>
    public string? Duration => DurationIn(false);

    /// <summary>The duration used, in English or (a Romanian form) in Romanian.</summary>
    public string? DurationIn(bool ro)
    {
        if (Time is not { } t || Durations.Count == 0) return null;
        double lo = Durations.Values.Min(), hi = Durations.Values.Max();
        return Math.Round(hi - lo, 2) == 0 ? Measures.DurationText(lo, t, ro)
            : $"{Math.Round(lo, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}–{Measures.DurationText(hi, t, ro)}";
    }

    /// <summary>What is wrong with the line's duration, for people: why nothing was multiplied, or how many elements no task takes.</summary>
    public IReadOnlyList<string> Notes => Time == null ? [] : TimeProblem is { } p ? [p]
        : Untimed.Count > 0 ? [$"{Untimed.Count} element(s) in no task of the schedule: their quantity is not multiplied by the duration."] : [];

    public double Qty => Math.Round(Quantity, Decimals);
    public double VM => Money.Round(Qty * M);
    public double VL => Money.Round(Qty * L);
    public double VE => Money.Round(Qty * E);
    public double VT => Money.Round(Qty * T);
    public double UnitPrice => Money.Round(M + L + E + T);
    public double Value => VM + VL + VE + VT;
}

/// <summary>An element's share of a line: the part of it measured (null: the whole) and the quantity it gives.</summary>
public sealed record LineShare(string Element, string? Part, double Quantity);

/// <summary>
/// Money as estimates write it: two decimals, half away from zero, of the number as written (its decimal of 15 digits):
/// on the double, 1.005 (1.00499… there) came out a ban short (audit P19). What a decimal cannot hold (not finite, beyond
/// 10¹⁵) is rounded on the double.
/// </summary>
public static class Money
{
    public static double Round(double v) =>
        double.IsFinite(v) && Math.Abs(v) < 1e15 ? (double)Math.Round((decimal)v, 2, MidpointRounding.AwayFromZero) : Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Money as text, the one way the cost module writes it: for people, or as a workbook takes it.</summary>
public static class MoneyText
{
    /// <summary>Two decimals, thousands grouped by a space („1 234.50”), as reports and the S-curve's labels show it.</summary>
    public static string Grouped(double v) => v.ToString("N2", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");

    /// <summary>Two decimals („1234.50”), or (raw) the value rounded to cents as a plain number („1234.5”) for a workbook's cell.</summary>
    public static string Plain(double v, bool raw = false) =>
        raw ? Money.Round(v).ToString(System.Globalization.CultureInfo.InvariantCulture) : v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>A chapter's or the whole estimate's sums by kind of cost.</summary>
public sealed record Sums(double M, double L, double E, double T)
{
    public double Direct => Money.Round(M + L + E + T);
    public static Sums Of(IEnumerable<EstimateLine> lines)
    {
        var l = lines.ToList();
        return new(Money.Round(l.Sum(x => x.VM)), Money.Round(l.Sum(x => x.VL)), Money.Round(l.Sum(x => x.VE)), Money.Round(l.Sum(x => x.VT)));
    }
}

/// <summary>
/// The estimate's totals as Romanian estimates lay them out: the direct costs by kind, the contributions on labour,
/// the direct costs with them, the overheads on those, the profit on the direct costs and overheads, the value without
/// VAT, the VAT and the total; and the total in the other currency.
/// </summary>
public sealed record Totals(Sums Direct, double LabourTax, double DirectTotal, double Overheads, double Profit, double Net, double Vat, double Total, double Other)
{
    public static Totals Of(Sums d, EstimateSettings s)
    {
        var tax = Money.Round(d.L * s.LabourTax / 100);
        var direct = Money.Round(d.Direct + tax);
        var over = Money.Round(direct * s.Overheads / 100);
        var profit = Money.Round((direct + over) * s.Profit / 100);
        var net = Money.Round(direct + over + profit);
        var vat = Money.Round(net * s.Vat / 100);
        var total = Money.Round(net + vat);
        return new(d, tax, direct, over, profit, net, vat, total, Money.Round(s.InOther(total)));
    }
}

public sealed record EstimateChapter(string Name, int Order, IReadOnlyList<EstimateLine> Lines)
{
    public Sums Sums => Sums.Of(Lines);
}

/// <summary>What the model has that no article prices: a type (or a kind of element without a type) and how many of it.</summary>
public sealed record Unpriced(string? TypeId, string Class, string Name, int Count, IReadOnlyList<string> Elements);

public sealed record EstimateResult(Entity Entity, EstimateSettings Settings, IReadOnlyList<EstimateChapter> Chapters, Totals Totals,
    IReadOnlyList<Unpriced> Unpriced, IReadOnlyList<string> Missing)
{
    public IEnumerable<EstimateLine> Lines => Chapters.SelectMany(c => c.Lines);
}

/// <summary>
/// The estimate (docs/plan-4d5d.md, stage 6) worked out from the model on every ask, never stored: each element's type's
/// articles (on the part each measures), then the article rules on the layers and parts its type's articles leave, the
/// quantity read from the element's quantity sets — the same as the takeoff's — times the price list's unit price; the
/// lines of the user's kept as they are; chapters by kind of work (or by storey and kind); the totals after Romanian
/// practice. Every element without a price is counted by its type.
/// </summary>
public static class Costing
{
    public const string Class = "IfcCostSchedule";

    /// <summary>The chapters by kind of work, in their order.</summary>
    public static readonly string[] Chapters =
        ["Demolition", "Earthworks", "Foundations", "Structure", "Walls", "Roof", "Doors and windows", "Finishes and insulation", "Installations", "Site works", "Other"];

    static readonly string[] Mep = ["Pipe", "Duct", "Cable", "Flow", "Sanitary", "Light", "Valve", "Pump", "Boiler", "SpaceHeater", "Distribution", "Electric", "Fire", "Tank", "Fan", "AirTerminal", "Outlet", "Switch"];

    /// <summary>A class's chapter by the kind of work it is.</summary>
    public static string ChapterOf(string cls) => cls switch
    {
        "IfcEarthworksCut" or "IfcEarthworksFill" or "LapinPlatform" or "LapinTerrain" => "Earthworks",
        "IfcFooting" or "IfcPile" or "LapinRetainingWall" => "Foundations",
        "IfcColumn" or "IfcBeam" or "IfcSlab" or "IfcMember" or "IfcPlate" or "IfcStair" or "IfcStairFlight" or "IfcRamp" or "IfcRampFlight" or "LapinSteelMember" or "LapinSteelNode" or "LapinRebar" or "IfcReinforcingBar" => "Structure",
        "IfcWall" or "IfcCurtainWall" or "LapinWallSweep" => "Walls",
        "IfcRoof" => "Roof",
        "IfcDoor" or "IfcWindow" => "Doors and windows",
        "IfcCovering" or "LapinRoomFinish" or "LapinFinishZone" or "LapinSkirting" or "LapinCeiling" or "IfcRailing" or "LapinRailing" => "Finishes and insulation",
        "LapinFormwork" or "LapinScaffold" => "Site works",
        // Every building service by its supertype (a ventilation unit, a damper, a sensor), else by its name's words.
        _ when IfcSchema.IsDistribution(cls) || Mep.Any(x => cls.Contains(x, StringComparison.Ordinal)) => "Installations",
        _ => "Other",
    };

    /// <summary>The element's own material: its attribute, else the material associated with it or with its type.</summary>
    public static string? OwnMaterial(IModelView m, string id)
    {
        if (m.Get(id) is not { } e) return null;
        if (e.Str("material") is { } mat && m.Get(mat) is { Class: IfcClass.Material }) return mat;
        if (m.RelatingOf(id, IfcClass.RelAssociatesMaterial) is { Class: IfcClass.Material } a) return a.Id;
        if (TypeOf(m, e) is { } t)
        {
            if (t.Str("material") is { } tm && m.Get(tm) is { Class: IfcClass.Material }) return tm;
            if (m.RelatingOf(t.Id, IfcClass.RelAssociatesMaterial) is { Class: IfcClass.Material } ta) return ta.Id;
        }
        return null;
    }

    /// <summary>The element's type, whose articles price it: its own, or the file's type of a reference (<see cref="ReferenceTypes"/>).</summary>
    public static Entity? TypeOf(IModelView m, Entity e) => Types.Of(m, e);

    /// <summary>
    /// The project's phases' kinds by id (existing, construction, execution), as the phases module keeps them on the project
    /// (read here so cost needs no phases module): Existing and New construction when the project has none of its own.
    /// </summary>
    public static Dictionary<string, string> PhaseKinds(IModelView m)
    {
        var o = new Dictionary<string, string>();
        if (Spatial.Project(m)?.Attrs["phases"] is JsonObject ph && ph["phases"] is JsonArray list)
            foreach (var p in list.OfType<JsonObject>())
                if ((string?)p["id"] is { Length: > 0 } id) o[id] = (string?)p["kind"] ?? "construction";
        if (o.Count == 0) { o["existing"] = "existing"; o["new-construction"] = "construction"; }
        return o;
    }

    /// <summary>
    /// What the works do to an element by its phases: an element that was there before the works (made in an existing phase) is
    /// not new work, and one demolished (in a phase after it was made: formwork made and struck in one is temporary, new work).
    /// </summary>
    public static (bool Existing, bool Demolished) WorkOn(Entity e, IReadOnlyDictionary<string, string> kinds)
    {
        var created = e.Str("phaseCreated");
        var demolished = e.Str("phaseDemolished");
        var temporary = created != null && created == demolished;
        return (created != null && kinds.GetValueOrDefault(created) == "existing", demolished != null && !temporary);
    }

    /// <summary>The project's estimate: the one asked for, else the first.</summary>
    public static Entity? EstimateOf(IModelView m, string? id = null) =>
        id != null && m.Get(id) is { Class: Class } e ? e : m.OfClass(Class).FirstOrDefault();

    /// <summary>The price lists an estimate prices by, in their order: those it names, else all of the project's.</summary>
    public static List<PriceLists.List> ListsFor(IModelView m, EstimateSettings s)
    {
        var all = PriceLists.All(m);
        if (s.PriceLists.Count == 0) return all;
        var picked = s.PriceLists.Select(id => all.FirstOrDefault(l => l.Id == id)).OfType<PriceLists.List>().ToList();
        return picked.Count > 0 ? picked : all;
    }

    /// <summary>An article's price row and the list's currency, from the first list that has its code.</summary>
    public static (PriceRow Row, PriceLists.List List)? Find(IReadOnlyList<PriceLists.List> lists, string code)
    {
        foreach (var l in lists) if (l.Articles.TryGetValue(code, out var r)) return (r, l);
        return null;
    }

    /// <summary>The elements an estimate measures: what the model measures, built (its class an element's), rooms and voids left out.</summary>
    public static IEnumerable<(Entity Element, IReadOnlyList<QuantitySet> Sets)> Measured(IQueryContext q)
    {
        foreach (var e in q.Model.Entities)
        {
            if (e.Class is IfcClass.Space or IfcClass.OpeningElement or IfcClass.Annotation or IfcClass.Grid) continue;
            if (q.Describe(e.Class) is { } d && d.Kind != "element") continue;
            var sets = q.Quantities(e.Id);
            if (sets.Count > 0) yield return (e, sets);
        }
    }

    /// <summary>
    /// The estimate worked out. <paramref name="times"/>: the work schedule when the caller has it (cost over time, the
    /// Gantt), else asked of the Planning module only when an article is priced by a duration.
    /// </summary>
    public static EstimateResult Compute(IQueryContext q, Entity estimate, ScheduleTimes? times = null)
    {
        var m = q.Model;
        // The schedule, read once and only when an article needs it: whether the Planning module is there, and its times.
        var planning = times != null || q.Services.Get<IWorkSchedules>() != null;
        var timesRead = times != null;
        ScheduleTimes? Times()
        {
            if (!timesRead) { times = q.Services.Get<IWorkSchedules>()?.Times(q); timesRead = true; }
            return times;
        }
        var s = EstimateSettings.Of(estimate);
        var lists = ListsFor(m, s);
        var rules = ArticleRule.All(m);
        var lines = new Dictionary<string, EstimateLine>();
        var missing = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var unpriced = new Dictionary<string, (string? Type, string Class, string Name, List<string> Elements)>();
        var storeyOrder = Spatial.Storeys(m).Select((st, i) => (st.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var chapterOrder = new Dictionary<string, int>();
        int OrderOf(string chapter, string? storey)
        {
            var k = Array.IndexOf(Chapters, chapter);
            if (k < 0) k = Chapters.Length + (chapterOrder.TryGetValue(chapter, out var known) ? known : chapterOrder[chapter] = chapterOrder.Count);
            return (storey != null && storeyOrder.TryGetValue(storey, out var so) ? so + 1 : 0) * 1000 + k;
        }
        var chapterMeta = new Dictionary<string, int>();

        void Price(EstimateLine line, string code)
        {
            if (Find(lists, code) is { } hit)
            {
                var (row, list) = hit;
                line.Name = row.Name;
                line.Unit = row.Unit.Length > 0 ? row.Unit : Units.UnitOf(line.Basis);
                double cm = row.Material, cl = row.Labour, ce = row.Equipment, ct = row.Transport;
                var resources = list.Resources[row.Code].ToList();
                // An article given only by its resources: its unit price is what they cost.
                if (row.Total == 0 && resources.Count > 0)
                {
                    cm = resources.Where(r => r.Kind == "material").Sum(r => (r.Consumption ?? 1) * r.ResourcePrice);
                    cl = resources.Where(r => r.Kind == "labour").Sum(r => (r.Consumption ?? 1) * r.ResourcePrice);
                    ce = resources.Where(r => r.Kind == "equipment").Sum(r => (r.Consumption ?? 1) * r.ResourcePrice);
                    ct = resources.Where(r => r.Kind == "transport").Sum(r => (r.Consumption ?? 1) * r.ResourcePrice);
                }
                line.M = Money.Round(s.Convert(cm, list.Currency));
                line.L = Money.Round(s.Convert(cl, list.Currency));
                line.E = Money.Round(s.Convert(ce, list.Currency));
                line.T = Money.Round(s.Convert(ct, list.Currency));
                line.Hours = row.Hours;
                line.Trade = row.Trade;
                line.Priced = true;
                line.Resources.Clear();
                foreach (var r in resources)
                    line.Resources.Add(new LineResource(r.Kind ?? "material", r.Code, r.Name, r.Unit, r.Consumption ?? 1, Money.Round(s.Convert(r.ResourcePrice, list.Currency)), r.Trade));
            }
            else
            {
                line.Name = line.Name.Length > 0 ? line.Name : code;
                line.Unit = line.Unit.Length > 0 ? line.Unit : Units.UnitOf(line.Basis);
                missing.Add(code);
            }
        }

        void Add(Entity e, ArticleRef a, IReadOnlyList<QuantitySet> sets, bool core, string? material = null)
        {
            var hit = Find(lists, a.Code);
            var basis = Measures.BasisFor(a, hit?.Row);
            // An article bound to a material (a rule, or a type's article on material:<id>) takes its quantity by the material's
            // measure (docs/plan-buildup-tags.md) unless it names its own; by area or count, a line per thickness.
            string? thickness = null;
            if (material != null && MaterialMeasures.For(m.Get(material), sets) is { } byRule)
            {
                if (a.Basis == "auto") basis = byRule.Basis;
                sets = byRule.Sets;
                thickness = byRule.Thickness;
            }
            if (Measures.Quantity(sets, basis, a, hit?.Row) is not { } qty || qty == 0) return;
            // "× duration": the quantity times how long the element is on site, from its tasks' span (derived, never stored).
            // Without a Planning module an article priced so only by its unit is as before; asked for, it says why it is not.
            var time = Measures.TimeFor(a, hit?.Row);
            if (time != null && !planning && a.Time == ArticleRef.AutoTime) time = null;
            double? duration = null;
            string? timeProblem = null;
            if (time != null)
            {
                if (!planning) timeProblem = "The Planning module is not in this edition: the quantity is not multiplied by the duration.";
                else if (Times() is not { } st) timeProblem = "The project has no work schedule: the quantity is not multiplied by the duration.";
                else if (st.Presence(e.Id) is { } span) duration = st.Length(span.Start, span.Finish, time);
                if (duration is { } d) qty *= d;
            }
            var chapter = a.Chapter ?? (core ? ChapterOf(e.Class) : "Finishes and insulation");
            string? storey = null;
            if (s.Grouping == "storey")
            {
                storey = Spatial.StoreyOf(m, e, followHost: true)?.Id;
                chapter = $"{(storey != null ? m.Get(storey)?.Name : null) ?? "—"} · {chapter}";
            }
            var key = $"{chapter}\u001f{a.Code.ToUpperInvariant()}" + (thickness != null ? $"\u001f{thickness}" : "");
            if (!lines.TryGetValue(key, out var line))
            {
                line = new EstimateLine(key, chapter, a.Code) { Basis = basis, Time = time };
                Price(line, a.Code);
                if (thickness != null) line.Name = $"{line.Name} · {thickness} mm";
                lines[key] = line;
                chapterMeta.TryAdd(chapter, OrderOf(a.Chapter ?? (core ? ChapterOf(e.Class) : "Finishes and insulation"), storey));
            }
            line.Quantity += qty;
            line.Elements.Add(e.Id);
            if (time != null)
            {
                line.Time ??= time;
                if (duration is { } dd) line.Durations[e.Id] = dd;
                else { line.Untimed.Add(e.Id); line.TimeProblem ??= timeProblem; }
            }
            // Each part's share of what the element gives (by its own measure; alike when none has one). The measures its author
            // marked are the whole element's, no part of it.
            IReadOnlyList<QuantitySet> shared = sets.Count > 1 ? sets.Where(x => !AuthoredQuantities.Is(x)).ToList() : sets;
            if (shared.Count <= 1) line.Shares.Add(new LineShare(e.Id, shared.Count == 1 ? shared[0].Part : null, qty));
            else
            {
                var each = shared.Select(x => (x.Part, Q: Measures.Quantity([x], basis, a, hit?.Row) ?? 0)).ToList();
                var sum = each.Sum(x => x.Q);
                foreach (var (part, pq) in each) line.Shares.Add(new LineShare(e.Id, part, sum > 0 ? qty * pq / sum : qty / each.Count));
            }
        }

        // A line a module counted in its article's unit: the code (and its variant: a line apiece), the quantity as it is.
        void AddArticle(Entity e, Quantity x)
        {
            var (code, variant) = ArticleQuantities.Split(x.Name);
            var chapter = ChapterOf(e.Class);
            string? storey = null;
            if (s.Grouping == "storey")
            {
                storey = Spatial.StoreyOf(m, e, followHost: true)?.Id;
                chapter = $"{(storey != null ? m.Get(storey)?.Name : null) ?? "—"} · {chapter}";
            }
            var key = $"{chapter}\u001f{code.ToUpperInvariant()}" + (variant != null ? $"\u001f{variant}" : "");
            if (!lines.TryGetValue(key, out var line))
            {
                line = new EstimateLine(key, chapter, code) { Basis = x.Kind == "AREA" ? "area" : x.Kind == "LENGTH" ? "length" : "count", Decimals = 6, Name = x.Label ?? "" };
                Price(line, code);
                if (variant != null) line.Name = $"{line.Name} · {variant}";
                lines[key] = line;
                chapterMeta.TryAdd(chapter, OrderOf(ChapterOf(e.Class), storey));
            }
            line.Quantity += x.Value;
            line.Elements.Add(e.Id);
            line.Shares.Add(new LineShare(e.Id, ArticleQuantities.Part, x.Value));
        }

        var kinds = PhaseKinds(m);
        if (s.Derive)
            foreach (var (e, sets) in Measured(q))
            {
                var type = TypeOf(m, e);
                var all = ArticleRef.Of(type);
                // The phases: what was there and stays is not priced; a demolition is priced by the type's demolition article, if it has one.
                var (existing, demolished) = WorkOn(e, kinds);
                if (demolished)
                    foreach (var d in all.Where(x => x.Part == Parts.Demolition))
                        Add(e, d with { Part = null, Chapter = d.Chapter ?? "Demolition" }, Parts.Select(m, e.Id, sets, null), true);
                if (existing) continue;
                var arts = all.Where(x => x.Part != Parts.Demolition).ToList();
                var whole = false;
                var covered = new HashSet<string>();
                var priced = false;
                foreach (var a in arts)
                {
                    var sel = Parts.Select(m, e.Id, sets, a.Part);
                    var isCore = a.Part is null or Parts.Core || a.Part.StartsWith(Parts.LayerPrefix, StringComparison.Ordinal) && IsCoreLayer(m, e.Id, a.Part);
                    // An article on one material: each of its parts by the material's measure (a line per thickness).
                    if (a.Part?.StartsWith(Parts.MaterialPrefix, StringComparison.Ordinal) == true && MaterialMeasure.Of(m.Get(a.Part[Parts.MaterialPrefix.Length..])) != null)
                        foreach (var one in sel) Add(e, a, [one], isCore, a.Part[Parts.MaterialPrefix.Length..]);
                    else Add(e, a, sel, isCore);
                    priced |= sel.Count > 0;
                    if (a.Part == null) whole = true;
                    foreach (var x in sel) if (x.Part != null) covered.Add(x.Part);
                }
                if (!whole && rules.Count > 0)
                {
                    var partSets = sets.Where(x => x.Part != null && x.Material != null && !covered.Contains(x.Part)).ToList();
                    var bands = LayerSets.BandsOf(m, e.Id);
                    int? coreIndex = bands?.FirstOrDefault(b => b.Core) is { Core: true } cb ? cb.Index : null;
                    foreach (var ps in partSets)
                        if (RuleFor(m, rules, ps.Material) is { } rule)
                        {
                            // A roof's covering (decision 328), its deck (329) and its dormers' covering (335) are the roof's own work, in its chapter.
                            var isCore = Parts.LayerIndex(ps.Part) is { } li ? li == coreIndex : ps.Part!.StartsWith("zone:", StringComparison.Ordinal) || ps.Part is "covering" or "deck" or "dormer-roofs";
                            Add(e, rule.Article, [ps], isCore, ps.Material);
                            priced = true;
                        }
                    // An element of one material and no parts: ruled by its own material, whole.
                    if (arts.Count == 0 && !sets.Any(x => x.Part != null) && OwnMaterial(m, e.Id) is { } ownMaterial && RuleFor(m, rules, ownMaterial) is { } r2)
                    {
                        Add(e, r2.Article, Parts.Select(m, e.Id, sets, null), true, ownMaterial);
                        priced = true;
                    }
                }
                // What a module counted in its articles' units (a roof's panels and their accessories, ArticleQuantities): priced by its codes.
                foreach (var set in sets.Where(x => x.Part == ArticleQuantities.Part))
                {
                    foreach (var x in set.Quantities.Where(x => x.Value > 0 && double.IsFinite(x.Value))) AddArticle(e, x);
                    priced = true;
                }
                if (!priced && arts.Count == 0)
                {
                    var k = type?.Id ?? "class:" + e.Class;
                    if (!unpriced.TryGetValue(k, out var u)) unpriced[k] = u = (type?.Id, e.Class, type != null && ReferenceTypes.Name(type) is { Length: > 0 } tn ? tn : e.Class, []);
                    u.Elements.Add(e.Id);
                }
            }

        // The user's own lines, as typed; a line naming a code and no price takes the list's.
        if (estimate.Attrs["lines"] is JsonArray own)
            foreach (var o in own.OfType<JsonObject>())
            {
                var id = (string?)o["id"] ?? "";
                var chapter = ((string?)o["chapter"])?.Trim() is { Length: > 0 } ch ? ch : "Other";
                var code = ((string?)o["code"])?.Trim() ?? "";
                var line = new EstimateLine("manual:" + id, chapter, code) { Manual = id, Name = (string?)o["name"] ?? "", Unit = (string?)o["unit"] ?? "", Quantity = Json.Number(o["qty"]) ?? 0 };
                var typed = new[] { "m", "l", "e", "t" }.Any(k => o[k] != null);
                if (!typed && code.Length > 0) Price(line, code);
                else
                {
                    line.M = Json.Number(o["m"]) ?? 0; line.L = Json.Number(o["l"]) ?? 0; line.E = Json.Number(o["e"]) ?? 0; line.T = Json.Number(o["t"]) ?? 0;
                    line.Hours = Json.Number(o["hours"]);
                    line.Priced = true;
                }
                if (o["elements"] is JsonArray els) foreach (var x in els.Select(x => (string?)x).OfType<string>()) line.Elements.Add(x);
                if (o["tasks"] is JsonArray tks) line.Tasks.AddRange(tks.Select(x => (string?)x).OfType<string>().Distinct());
                if ((string?)o["per"] is { } per && TimeRelated.Units.Contains(per)) TimeRelated.Measure(line, per, planning, Times);
                lines[line.Key] = line;
                chapterMeta.TryAdd(chapter, OrderOf(chapter, null));
            }

        var chapters = lines.Values.GroupBy(l => l.Chapter)
            .Select(g => new EstimateChapter(g.Key, chapterMeta.GetValueOrDefault(g.Key), g.OrderBy(l => l.Manual != null).ThenBy(l => l.Code, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(c => c.Order).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var totals = Totals.Of(Sums.Of(chapters.SelectMany(c => c.Lines)), s);
        return new EstimateResult(estimate, s, chapters, totals,
            unpriced.Values.Select(u => new Unpriced(u.Type, u.Class, u.Name, u.Elements.Count, u.Elements)).OrderByDescending(u => u.Count).ToList(), missing.ToList());
    }

    /// <summary>The chapter a time-related item goes in unless the user names one.</summary>
    public const string TimeRelatedChapter = "Time-related / preliminaries";

    static bool IsCoreLayer(IModelView m, string id, string part) =>
        Parts.LayerIndex(part) is { } i && LayerSets.BandsOf(m, id)?.FirstOrDefault(b => b.Index == i) is { Core: true };

    /// <summary>The rule for a material: one naming it, else one of its category.</summary>
    public static ArticleRule? RuleFor(IModelView m, IReadOnlyList<ArticleRule> rules, string? material) =>
        material == null ? null : rules.FirstOrDefault(r => r.Material == material) ?? rules.FirstOrDefault(r => r.Material == null && r.Matches(m, material));

    /// <summary>
    /// Each chapter's value without VAT, VAT and total, as the object estimate lists them: its own totals after the same
    /// rules as the estimate's (the CAM on its own labour, the overheads and profit on its own costs), the last taking what
    /// rounding left so the rows add up to the totals.
    /// </summary>
    public static List<(EstimateChapter Chapter, double Net, double Vat, double Total)> Shares(EstimateResult r)
    {
        var o = new List<(EstimateChapter, double, double, double)>();
        double net = 0, vat = 0;
        for (var i = 0; i < r.Chapters.Count; i++)
        {
            var c = r.Chapters[i];
            double n, v;
            if (i == r.Chapters.Count - 1) { n = Money.Round(r.Totals.Net - net); v = Money.Round(r.Totals.Vat - vat); }
            else
            {
                var own = Totals.Of(c.Sums, r.Settings);
                n = own.Net; v = own.Vat;
            }
            net += n; vat += v;
            o.Add((c, n, v, Money.Round(n + v)));
        }
        return o;
    }
}

/// <summary>
/// Time-related items (docs/plan-4d5d.md, "Time-related costs"): a rate a day, working day, week or month times how long
/// its tasks last (a level of effort task usually; any task, or the whole project when it names none), read from the work
/// schedule on every ask, never stored. Percentage overheads stay as they are, beside them.
/// </summary>
public static class TimeRelated
{
    public static readonly string[] Units = [MeasureUnits.Days, MeasureUnits.WorkingDays, MeasureUnits.Weeks, MeasureUnits.Months];

    /// <summary>The item's quantity from its tasks' span (or the project's), or why the quantity typed is kept.</summary>
    public static void Measure(EstimateLine line, string per, bool planning, Func<ScheduleTimes?> times)
    {
        line.Per = per;
        line.Time = per;
        if (!planning) { line.TimeProblem = "The Planning module is not in this edition: the quantity typed is used."; return; }
        if (times() is not { } st) { line.TimeProblem = "The project has no work schedule: the quantity typed is used."; return; }
        DateOnly? from = null, to = null;
        if (line.Tasks.Count == 0) { from = st.Start; to = st.Finish; }
        else
            foreach (var id in line.Tasks)
                if (st.Task(id) is { } t)
                {
                    if (from == null || t.Start < from) from = t.Start;
                    if (to == null || t.Finish > to) to = t.Finish;
                }
        if (from is not { } f || to is not { } e) { line.TimeProblem = "None of its tasks is in the work schedule: the quantity typed is used."; return; }
        if (e < f) e = f;
        line.Span = (f, e);
        line.Quantity = st.Length(f, e, per);
        line.Durations["span"] = line.Quantity;
    }
}
