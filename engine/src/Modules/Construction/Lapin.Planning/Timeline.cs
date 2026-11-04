using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>Two days, both counted: from the first to the last.</summary>
public sealed record DaySpan(DateOnly From, DateOnly To)
{
    public bool Holds(DateOnly d) => d >= From && d <= To;
    public DaySpan With(DaySpan o) => new(o.From < From ? o.From : From, o.To > To ? o.To : To);
    public JsonArray ToJson() => new((JsonNode)WorkCalendar.Date(From), (JsonNode)WorkCalendar.Date(To));
}

/// <summary>
/// An element's time on the schedule (docs/plan-4d5d.md, stage 4): the days it is built (its first task's start to its last
/// one's finish), each layer's own days when tasks take its layers apart (a wall's core in one, its plaster in another), the
/// days it is demolished, and, for a formwork or a scaffold, the days it stands.
/// </summary>
public sealed record ElementTime(string Id, DaySpan? Build, IReadOnlyDictionary<int, DaySpan>? Layers, DaySpan? Demolish, string? Temporary, DaySpan? Present)
{
    /// <summary>A layer's days: its own, else the element's (a layer no task takes goes up with the element).</summary>
    public DaySpan? LayerSpan(int index) => Layers != null && Layers.TryGetValue(index, out var s) ? s : Build;
}

/// <summary>
/// The states an element goes through on the schedule: not started yet (<see cref="Planned"/>), being built
/// (<see cref="Active"/>), built (<see cref="Done"/>), standing as it was (<see cref="Existing"/>: not built by the works, or
/// before its demolition), being demolished (<see cref="Demolishing"/>), gone (<see cref="Gone"/>: demolished, or a temporary
/// not up), a formwork or scaffold standing (<see cref="Temporary"/>), and in no task (<see cref="Unlinked"/>).
/// </summary>
public static class Status
{
    public const string Planned = "planned", Active = "active", Done = "done", Existing = "existing", Demolishing = "demolishing", Gone = "gone",
        Temporary = "temporary", Unlinked = "unlinked";
}

/// <summary>
/// How the simulation shows the states, the project's (on the schedule, <c>simulation</c>): the colours of what is being
/// built, demolished, of the temporaries and of the ghosts; whether what is not started shows as a ghost or not at all;
/// whether what no task takes shows; whether layers built apart show apart.
/// </summary>
public sealed record SimulationLook(string Active, string Demolishing, string Temporary, string Planned, bool Ghosts, bool HideUnlinked, bool Layers)
{
    public static readonly SimulationLook Default = new("#e8a33d", "#d64545", "#3d8fd6", "#b8c0c8", false, false, true);

    static string Color(JsonNode? n, string fallback) => (string?)n is { } s && System.Text.RegularExpressions.Regex.IsMatch(s, "^#[0-9a-fA-F]{6}$") ? s.ToLowerInvariant() : fallback;

    public static SimulationLook Read(JsonNode? n)
    {
        if (n is not JsonObject o) return Default;
        var d = Default;
        return new(Color(o["active"], d.Active), Color(o["demolishing"], d.Demolishing), Color(o["temporary"], d.Temporary), Color(o["planned"], d.Planned),
            (string?)o["plannedMode"] == "ghost", (string?)o["unlinked"] == "hide", o["layers"] is not JsonValue v || !v.TryGetValue<bool>(out var b) || b);
    }

    public JsonObject ToJson() => new()
    {
        ["active"] = Active, ["demolishing"] = Demolishing, ["temporary"] = Temporary, ["planned"] = Planned,
        ["plannedMode"] = Ghosts ? "ghost" : "hide", ["unlinked"] = HideUnlinked ? "hide" : "show", ["layers"] = Layers,
    };

    public static SimulationLook Of(Plan p) => Read(p.Schedule?.Attrs["simulation"]);
}

/// <summary>
/// The 4D simulation's timeline (docs/plan-4d5d.md, stage 4): every element's days on the schedule worked out from the tasks
/// that take it (their critical path dates), the parts of layered elements by the tasks that take each layer, the
/// demolitions by the tasks that demolish (kind DEMOLITION, REMOVAL, DISMANTLE or DISPOSAL, or a rule on what is
/// demolished), the formwork and scaffolds by their hosts' tasks (<see cref="TaskLives"/>). Derived, never stored; the
/// interface gets it once a change and shows the state at any date without asking again; the views at a date read the
/// same (<see cref="StateAtDate"/>), so a sheet prints what the screen shows.
/// </summary>
public sealed class Timeline
{
    public static readonly string[] DemolitionKinds = ["DEMOLITION", "REMOVAL", "DISMANTLE", "DISPOSAL"];

    public Plan Plan { get; }
    public CpmResult Result { get; }
    public SimulationLook Look { get; }
    public Dictionary<string, ElementTime> Elements { get; } = [];
    /// <summary>The number of layers of each layered element (those with more than one).</summary>
    public Dictionary<string, int> LayerCounts { get; } = [];
    public DateOnly Start { get; }
    public DateOnly Finish { get; }

    Timeline(Plan p, CpmResult r)
    {
        Plan = p;
        Result = r;
        Look = SimulationLook.Of(p);
        Start = p.Calendar.NextWorking(p.Start);
        Finish = r.Times.Count > 0 ? r.Times.Values.Max(t => t.Finish) : Start;
    }

    /// <summary>Whether a task takes its elements down rather than up.</summary>
    public static bool Demolishes(PlanTask t) => (t.Kind != null && DemolitionKinds.Contains(t.Kind)) || (t.Rule?.Demolished.Count ?? 0) > 0;

    public static Timeline Build(IQueryContext q) => Build(q.Model, q.Services, q.Describe);

    public static Timeline Build(IModelView m, IServices services, Func<string, ClassDescriptor?> describe)
    {
        var p = Plan.Read(m);
        var (r, _) = PlanningModule.Solve(p);
        var links = TaskElements.Resolve(m, services, describe, p);
        var t = new Timeline(p, r);
        var build = new Dictionary<string, DaySpan>();
        var layers = new Dictionary<string, Dictionary<int, DaySpan>>();
        var demolish = new Dictionary<string, DaySpan>();
        foreach (var task in p.Rows)
        {
            if (!links.ByTask.TryGetValue(task.Id, out var own) || !r.Times.TryGetValue(task.Id, out var tm)) continue;
            var span = new DaySpan(tm.Start, tm.Finish);
            var down = Demolishes(task);
            foreach (var (id, mask) in own)
            {
                if (down) { demolish[id] = demolish.TryGetValue(id, out var d) ? d.With(span) : span; continue; }
                build[id] = build.TryGetValue(id, out var b) ? b.With(span) : span;
                var count = links.Facts(id)?.Layers.Count ?? 1;
                if (count <= 1) continue;
                t.LayerCounts[id] = count;
                var per = layers.TryGetValue(id, out var l) ? l : layers[id] = [];
                for (var i = 0; i < Math.Min(64, count); i++)
                    if ((mask & (1UL << i)) != 0) per[i] = per.TryGetValue(i, out var s) ? s.With(span) : span;
            }
        }
        foreach (var id in build.Keys.Union(demolish.Keys))
        {
            var b = build.GetValueOrDefault(id);
            // Layers apart only when some layer's days are not the element's.
            IReadOnlyDictionary<int, DaySpan>? per = layers.TryGetValue(id, out var l) && b != null && (l.Count < t.LayerCounts[id] || l.Values.Any(s => s != b)) ? l : null;
            t.Elements[id] = new ElementTime(id, b, per, demolish.GetValueOrDefault(id), null, null);
        }
        foreach (var life in TaskLives.Of(p, r, links, services, m))
            t.Elements[life.Element] = new ElementTime(life.Element, null, null, null, life.Kind, new DaySpan(life.From, life.To));
        return t;
    }

    sealed class Holder(long revision, Timeline value)
    {
        public long Revision { get; } = revision;
        public Timeline Value { get; } = value;
    }

    static readonly ConditionalWeakTable<object, Holder> Cache = new();

    /// <summary>The timeline of the model as it is now, worked out once a revision (every element of a drawing asks).</summary>
    public static Timeline Of(IQueryContext q)
    {
        var m = q.Model;
        var key = (object)m;
        lock (Cache)
            if (Cache.TryGetValue(key, out var h) && h.Revision == m.Revision) return h.Value;
        var t = Build(q);
        lock (Cache) Cache.AddOrUpdate(key, new Holder(m.Revision, t));
        return t;
    }

    /// <summary>An element's state on a day; <see cref="Status.Unlinked"/> when the schedule does not know it.</summary>
    public string StateOf(string id, DateOnly d) => Elements.TryGetValue(id, out var e) ? StateOf(e, d) : Status.Unlinked;

    public static string StateOf(ElementTime e, DateOnly d)
    {
        if (e.Present is { } present) return present.Holds(d) ? Status.Temporary : Status.Gone;
        if (e.Demolish is { } dem && (e.Build == null || d >= dem.From))
            return d < dem.From ? Status.Existing : d <= dem.To ? Status.Demolishing : Status.Gone;
        return Built(e.Build, d);
    }

    static string Built(DaySpan? s, DateOnly d) => s == null ? Status.Unlinked : d < s.From ? Status.Planned : d <= s.To ? Status.Active : Status.Done;

    /// <summary>A layer's state on a day (an element built in one piece: its own).</summary>
    public static string LayerStateOf(ElementTime e, int index, DateOnly d)
    {
        if (e.Layers == null || e.Present != null || (e.Demolish is { } dem && d >= dem.From)) return StateOf(e, d);
        return Built(e.LayerSpan(index), d);
    }

    /// <summary>The leaf tasks under way on a day (started, not finished), in row order.</summary>
    public IEnumerable<PlanTask> TasksOn(DateOnly d) =>
        Plan.Rows.Where(t => !Plan.IsSummary(t.Id) && Result.Times.TryGetValue(t.Id, out var tm) && tm.Duration > 0 && d >= tm.Start && d <= tm.Finish);

    /// <summary>
    /// Dates for a stages sheet: the finish of each task at the top of the WBS (a chapter), or the last day of each month the
    /// works run through (and the finish), or every <paramref name="weeks"/> weeks from the start; at most <paramref name="max"/>.
    /// </summary>
    public List<DateOnly> StageDates(string mode, int max = 12, int weeks = 4)
    {
        var o = new List<DateOnly>();
        if (mode == "months")
        {
            for (var d = new DateOnly(Start.Year, Start.Month, 1).AddMonths(1).AddDays(-1); d < Finish; d = new DateOnly(d.Year, d.Month, 1).AddMonths(2).AddDays(-1)) o.Add(d);
            o.Add(Finish);
        }
        else if (mode == "weeks")
        {
            for (var d = Start.AddDays(7 * Math.Max(1, weeks) - 1); d < Finish; d = d.AddDays(7 * Math.Max(1, weeks))) o.Add(d);
            o.Add(Finish);
        }
        else
            foreach (var t in Plan.Rows.Where(t => Plan.Level.GetValueOrDefault(t.Id) == 0))
                if (Result.Times.TryGetValue(t.Id, out var tm)) o.Add(tm.Finish);
        o = o.Distinct().Order().ToList();
        // Too many: every n-th, the last kept.
        if (o.Count > max && max > 1)
        {
            var step = (double)(o.Count - 1) / (max - 1);
            o = Enumerable.Range(0, max).Select(i => o[(int)Math.Round(i * step)]).Distinct().ToList();
        }
        return o;
    }

    /// <summary>What the interface gets once a change: the dates and every element's days (the interface works out each frame's state itself).</summary>
    public JsonObject ToJson()
    {
        var elements = new JsonArray();
        foreach (var e in Elements.Values)
        {
            var o = new JsonObject { ["id"] = e.Id };
            if (e.Build is { } b) o["build"] = b.ToJson();
            if (e.Layers is { } per)
            {
                var l = new JsonObject();
                for (var i = 0; i < LayerCounts.GetValueOrDefault(e.Id); i++)
                    if (e.LayerSpan(i) is { } s) l[$"layer:{i}"] = s.ToJson();
                o["layers"] = l;
            }
            if (e.Demolish is { } d) o["demolish"] = d.ToJson();
            if (e.Present is { } pr) { o["temporary"] = e.Temporary; o["present"] = pr.ToJson(); }
            elements.Add((JsonNode)o);
        }
        var tasks = new JsonArray();
        foreach (var t in Plan.Rows)
            if (!Plan.IsSummary(t.Id) && Result.Times.TryGetValue(t.Id, out var tm))
                tasks.Add((JsonNode)new JsonObject { ["id"] = t.Id, ["name"] = t.Name, ["start"] = WorkCalendar.Date(tm.Start), ["finish"] = WorkCalendar.Date(tm.Finish), ["duration"] = tm.Duration });
        return new JsonObject
        {
            ["start"] = WorkCalendar.Date(Start), ["finish"] = WorkCalendar.Date(Finish),
            ["dataDate"] = Plan.DataDate is { } dd ? WorkCalendar.Date(dd) : null,
            ["look"] = Look.ToJson(), ["elements"] = elements, ["tasks"] = tasks,
            ["stages"] = new JsonObject
            {
                ["chapters"] = new JsonArray(StageDates("chapters").Select(x => (JsonNode)WorkCalendar.Date(x)).ToArray()),
                ["months"] = new JsonArray(StageDates("months").Select(x => (JsonNode)WorkCalendar.Date(x)).ToArray()),
            },
        };
    }
}

/// <summary>
/// A view at a date (its <c>stateDate</c>, docs/plan-4d5d.md: State at date): it draws the model as the schedule has it that
/// day, as the 3D simulation shows it — what is not started left out (or drawn as a ghost in the simulation's planned
/// colour), what is being built in its colour, what is being demolished in red, the formwork and scaffolds only while they
/// stand, what is demolished gone; a layered element's layers not yet laid left out. Plans, sections, elevations, their
/// sheets and the 3D exports read it; the phases step aside for such a view.
/// </summary>
public sealed class StateAtDate : IViewElementFilter, IViewPartFilter
{
    public (bool Hide, string? Color)? For(IModelView model, Entity view, Entity element) => null;

    public static DateOnly? DateOf(Entity view) => WorkCalendar.ParseDate(view.Str("stateDate"));

    public (bool Hide, string? Color)? For(IQueryContext q, Entity view, Entity element)
    {
        if (DateOf(view) is not { } d || element.Str(Platform.Links.Key) != null) return null;
        var t = Timeline.Of(q);
        if (!t.Elements.TryGetValue(element.Id, out var e))
            return t.Look.HideUnlinked && t.Elements.Count > 0 && Spatial.StoreyOf(q.Model, element.Id) != null && IsBuilt(q, element) ? (true, null) : null;
        var state = Timeline.StateOf(e, d);
        // A layered element partly built: drawn by the state of the layers it shows.
        if (state is Status.Planned or Status.Active or Status.Done && e.Layers != null && t.Look.Layers)
        {
            var states = Enumerable.Range(0, t.LayerCounts.GetValueOrDefault(element.Id)).Select(i => Timeline.LayerStateOf(e, i, d)).ToList();
            state = states.Contains(Status.Active) ? Status.Active : states.All(s => s == Status.Planned) ? Status.Planned : Status.Done;
        }
        return state switch
        {
            Status.Planned => t.Look.Ghosts ? (false, t.Look.Planned) : (true, null),
            Status.Active => (false, t.Look.Active),
            Status.Demolishing => (false, t.Look.Demolishing),
            Status.Temporary => (false, t.Look.Temporary),
            Status.Gone => (true, null),
            _ => (false, null),
        };
    }

    /// <summary>What a task could take: an element of the model (not a type, a drawing, a room).</summary>
    static bool IsBuilt(IQueryContext q, Entity e) => q.Describe(e.Class) is not { } d || d.Kind == "element";

    public bool Hides(IQueryContext q, Entity view, Entity element, string? partKey)
    {
        if (DateOf(view) is not { } d || partKey is null || !partKey.StartsWith("layer:", StringComparison.Ordinal)) return false;
        if (!int.TryParse(partKey.AsSpan(6), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index)) return false;
        var t = Timeline.Of(q);
        if (!t.Look.Layers || !t.Elements.TryGetValue(element.Id, out var e) || e.Layers == null) return false;
        // The element shown at all: its layers not yet laid left out (a ghost's layers all drawn).
        return Timeline.StateOf(e, d) is Status.Active or Status.Done && Timeline.LayerStateOf(e, index, d) == Status.Planned;
    }
}
