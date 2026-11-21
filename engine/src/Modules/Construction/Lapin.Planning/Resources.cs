using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>A resource on a task: how many units of it the task takes a working day, and for how many working days after the task's finish it keeps them (formwork struck later).</summary>
public sealed record Assignment(double Units, int Hold = 0);

/// <summary>
/// A resource of the project as IFC has them: a crew (IfcLaborResource), a machine (IfcConstructionEquipmentResource), a
/// set of formwork (IfcConstructionProductResource FORMWORK); how many units are there a working day (the limit the
/// levelling keeps to), its unit, what a unit costs a working day, and the tasks it is on (task id → units, hold).
/// </summary>
public sealed record ResourceDef(string Id, string Name, string Kind, double Capacity, string Unit, double Rate, IReadOnlyDictionary<string, Assignment> Assign, string? FormworkType = null)
{
    public string Class => Resources.ClassOf(Kind);
}

/// <summary>
/// The project's resources (docs/plan-4d5d.md; open since decision 772, written for Planning Pro first, decision 308): read,
/// written, and what each one is asked for on each day of the schedule. The classes and attributes are those Planning Pro
/// saved (<c>capacity</c>, <c>unit</c>, <c>rate</c>, <c>assign</c>: task → units or {units, hold}, <c>formworkType</c>,
/// <c>derived</c>), so a project it saved opens with its resources in either edition.
/// </summary>
public static class Resources
{
    public const string Labour = "IfcLaborResource", Equipment = "IfcConstructionEquipmentResource", Product = "IfcConstructionProductResource";
    public static readonly string[] Classes = [Labour, Equipment, Product];
    public static readonly string[] Kinds = ["labour", "equipment", "formwork"];

    public static string ClassOf(string kind) => kind switch { "equipment" => Equipment, "formwork" => Product, _ => Labour };
    public static string KindOf(string cls) => cls switch { Equipment => "equipment", Product => "formwork", _ => "labour" };
    public static string DefaultUnit(string kind) => kind switch { "equipment" => "pcs", "formwork" => "sets", _ => "workers" };

    public static ResourceDef Read(Entity e)
    {
        var assign = new Dictionary<string, Assignment>();
        if (e.Attrs["assign"] is JsonObject o)
            foreach (var (task, v) in o)
            {
                var units = v is JsonObject vo ? Json.Number(vo["units"]) : Json.Number(v);
                var hold = v is JsonObject vh ? (int)Math.Round(Json.Number(vh["hold"]) ?? 0) : 0;
                if (units is { } u && u > 0 && double.IsFinite(u)) assign[task] = new Assignment(u, Math.Max(0, hold));
            }
        var kind = KindOf(e.Class);
        return new ResourceDef(e.Id, e.Name, kind, Math.Max(0, e.NumOrNull("capacity") ?? 1), e.Str("unit") ?? DefaultUnit(kind), Math.Max(0, e.NumOrNull("rate") ?? 0), assign, e.Str("formworkType"));
    }

    /// <summary>The resources, their assignments kept to the tasks that are there (a deleted task's are dropped).</summary>
    public static List<ResourceDef> All(IModelView m)
    {
        var tasks = m.OfClass(Plan.TaskClass).Select(t => t.Id).ToHashSet();
        return Classes.SelectMany(m.OfClass).OrderBy(e => m.Order(e.Id)).Select(Read)
            .Select(r => r with { Assign = r.Assign.Where(kv => tasks.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value) }).ToList();
    }

    public static JsonObject AssignJson(IReadOnlyDictionary<string, Assignment> a) =>
        new(a.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => KeyValuePair.Create(kv.Key,
            (JsonNode?)(kv.Value.Hold > 0 ? new JsonObject { ["units"] = kv.Value.Units, ["hold"] = kv.Value.Hold } : JsonValue.Create(kv.Value.Units)))));

    /// <summary>
    /// The working days a task holds its resources from a start: its own working days (a wet task's skip the winter break),
    /// then <paramref name="hold"/> working days more after its finish. A milestone holds only what it keeps after.
    /// </summary>
    public static List<DateOnly> Held(WorkCalendar cal, DateOnly start, int duration, bool wet, int hold)
    {
        var o = new List<DateOnly>();
        var d = cal.NextWorking(start, wet);
        for (var i = 0; i < duration; i++) { o.Add(d); d = cal.NextWorking(d.AddDays(1), wet); }
        var after = duration > 0 ? cal.NextWorking(o[^1].AddDays(1)) : cal.NextWorking(start);
        for (var i = 0; i < hold; i++) { o.Add(after); after = cal.NextWorking(after.AddDays(1)); }
        return o;
    }

    /// <summary>A leaf's working days as the schedule has them now: from its start to its finish (a started one: its dates as they stand).</summary>
    public static int DaysOf(Plan p, TaskTimes t, string id) =>
        p.ById[id].Milestone || t.Duration == 0 ? 0 : p.Calendar.WorkingDays(t.EarlyStart, t.EarlyFinish, p.ById[id].Wet);

    /// <summary>What each resource is asked for each day the schedule covers, and the days it is asked for more than it has.</summary>
    public sealed record Load(DateOnly From, DateOnly To, IReadOnlyList<DateOnly> Days, IReadOnlyDictionary<string, double[]> Demand)
    {
        public int Over(ResourceDef r) => Demand.TryGetValue(r.Id, out var d) ? d.Count(x => x > r.Capacity + 1e-9) : 0;
        public double Peak(ResourceDef r) => Demand.TryGetValue(r.Id, out var d) && d.Length > 0 ? d.Max() : 0;
    }

    public static Load LoadOf(Plan p, CpmResult r, IReadOnlyList<ResourceDef> resources)
    {
        var cal = p.Calendar;
        var start = cal.NextWorking(p.Start);
        var held = new Dictionary<string, Dictionary<DateOnly, double>>();
        var last = r.Times.Count > 0 ? r.Times.Values.Max(t => t.Finish) : start;
        foreach (var res in resources)
        {
            var use = held[res.Id] = [];
            foreach (var (task, a) in res.Assign)
            {
                if (!r.Times.TryGetValue(task, out var tm) || p.IsSummary(task)) continue;
                foreach (var d in Held(cal, tm.EarlyStart, DaysOf(p, tm, task), p.ById[task].Wet, a.Hold))
                {
                    use[d] = use.GetValueOrDefault(d) + a.Units;
                    if (d > last) last = d;
                }
            }
        }
        var first = r.Times.Count > 0 ? r.Times.Values.Min(t => t.Start) : start;
        if (first > start) first = start;
        var days = new List<DateOnly>();
        for (var d = first; d <= last; d = d.AddDays(1)) days.Add(d);
        var demand = resources.ToDictionary(x => x.Id, x => days.Select(d => held[x.Id].GetValueOrDefault(d)).ToArray());
        return new Load(first, last, days, demand);
    }

    /// <summary>What a resource costs over the schedule: each day's units × its rate.</summary>
    public static double CostOf(ResourceDef r, Load load) => load.Demand.TryGetValue(r.Id, out var d) ? Money(d.Sum() * r.Rate) : 0;

    /// <summary>Two decimals, half away from zero, of the number as written (as the cost module's Money.Round: this module does not need it).</summary>
    static double Money(double v) =>
        double.IsFinite(v) && Math.Abs(v) < 1e15 ? (double)Math.Round((decimal)v, 2, MidpointRounding.AwayFromZero) : Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
