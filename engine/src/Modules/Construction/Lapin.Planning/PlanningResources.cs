using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// Resources and their levelling (docs/plan-4d5d.md; written for Planning Pro, decision 308, open and free since decision 772):
/// the project's crews (IfcLaborResource), machines (IfcConstructionEquipmentResource) and formwork sets
/// (IfcConstructionProductResource FORMWORK), each with the units there are a working day, its unit and what a unit costs a
/// day; put on tasks (a summary's go on its tasks) with their units and, for formwork, the working days a set stays after its
/// task; the formwork laid in Site works made a limited resource in one click; the histogram (<c>planning.resources</c>);
/// the serial levelling (<see cref="Levelling"/>) written as each moved task's levelling start, which the critical path keeps,
/// and cleared. Every command one undo step.
/// </summary>
public sealed partial class PlanningModule
{
    static string? ResText(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim().Length > 0 ? s.Trim() : null;

    static double ResNum(JsonNode? n, string what)
    {
        if (Json.Number(n) is not { } v || !double.IsFinite(v) || v < 0) throw new CommandException($"The {what} is a number not below 0.");
        return v;
    }

    static Entity RequireResource(IModelView m, string? id) =>
        m.Get(id ?? "") is { } e && Resources.Classes.Contains(e.Class) ? e : throw new CommandException("There is no such resource.");

    static void RegisterResourceClasses(IModuleRegistry r)
    {
        r.Class(new(Resources.Labour, "Crew", [], "#5b7a99", Kind: "resource"));
        r.Class(new(Resources.Equipment, "Equipment", [], "#5b7a99", Kind: "resource"));
        r.Class(new(Resources.Product, "Formwork sets", [], "#5b7a99", Kind: "resource"));
    }

    static void RegisterResources(IModuleRegistry r)
    {
        RegisterResourceClasses(r);

        // {kind: labour | equipment | formwork, name?, capacity?, unit?, rate?} → {id}
        r.Command("addResource", "New resource", (ctx, a) =>
        {
            var kind = (string?)a["kind"] is { } k ? (Resources.Kinds.Contains(k) ? k : throw new CommandException("A resource is labour, equipment or formwork.")) : "labour";
            var name = ResText(a["name"]) ?? kind switch { "equipment" => "Equipment", "formwork" => "Formwork sets", _ => "Crew" };
            var attrs = new JsonObject { ["capacity"] = a["capacity"] is null ? 1 : ResNum(a["capacity"], "limit"), ["unit"] = ResText(a["unit"]) ?? Resources.DefaultUnit(kind) };
            if (a["rate"] is not null) attrs["rate"] = ResNum(a["rate"], "rate");
            var e = new Entity(ctx.NewId(), Resources.ClassOf(kind), name, attrs, kind == "formwork" ? "FORMWORK" : null);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });

        // {id, name?, capacity?, rate?, unit?}
        r.Command("updateResource", "Change resource", (ctx, a) =>
        {
            var e = RequireResource(ctx.Model, (string?)a["id"]);
            var attrs = e.CloneAttrs();
            if (a.ContainsKey("capacity")) attrs["capacity"] = ResNum(a["capacity"], "limit");
            if (a.ContainsKey("rate")) attrs["rate"] = ResNum(a["rate"], "rate");
            if (a.ContainsKey("unit")) attrs["unit"] = ResText(a["unit"]) ?? Resources.DefaultUnit(Resources.KindOf(e.Class));
            ctx.Replace(e.With(name: a.ContainsKey("name") ? ResText(a["name"]) ?? throw new CommandException("A resource has a name.") : null, attrs: attrs));
            return null;
        });

        // A resource on tasks (the leaves: a summary's tasks), so many units a day, kept so many working days after (0 units: taken off).
        // {resource, tasks, units?, hold?} → {tasks}
        r.Command("assignResource", "Assign resource", (ctx, a) =>
        {
            var e = RequireResource(ctx.Model, (string?)a["resource"] ?? (string?)a["id"]);
            var p = Plan.Read(ctx.Model);
            var tasks = IdList(a["tasks"]).Where(p.ById.ContainsKey).SelectMany(p.LeavesOf).Distinct().ToList();
            if (tasks.Count == 0) throw new CommandException("Select the tasks to put the resource on in the Gantt.");
            var units = Json.Number(a["units"]) ?? 1;
            if (!double.IsFinite(units) || units < 0) throw new CommandException("A resource's units on a task are a number not below 0.");
            var hold = (int)Math.Round(Json.Number(a["hold"]) ?? 0);
            var res = Resources.Read(e);
            var assign = res.Assign.ToDictionary(kv => kv.Key, kv => kv.Value);
            foreach (var t in tasks)
                if (units <= 0) assign.Remove(t);
                else assign[t] = new Assignment(units, a.ContainsKey("hold") ? Math.Max(0, hold) : assign.GetValueOrDefault(t)?.Hold ?? 0);
            var attrs = e.CloneAttrs();
            if (assign.Count == 0) attrs.Remove("assign"); else attrs["assign"] = Resources.AssignJson(assign);
            ctx.Replace(e.With(attrs: attrs));
            return new JsonObject { ["tasks"] = tasks.Count };
        });

        // The formwork as a limited resource: so many sets, each formwork on site one set, held by the task that casts its host until it is struck.
        // {type?, capacity?, name?} → {id, tasks, formworks}
        r.Command("formworkResource", "Formwork as a resource", (ctx, a) =>
        {
            var p = ReadResolved(ctx.Query, out var links);
            var (res, problem) = Solve(p, levelled: false);
            if (problem != null) throw new CommandException(problem);
            var type = ResText(a["type"]);
            var lives = TaskLives.Of(p, res, links, ctx.Services, ctx.Model).Where(l => l.Kind == "formwork").ToList();
            if (type != null) lives = lives.Where(l => ctx.Model.Get(l.Element) is { } el && Types.Of(ctx.Model, el)?.Id == type).ToList();
            if (lives.Count == 0) throw new CommandException("No formwork stands on the schedule: lay formwork (Site works) on elements some task builds.");
            var assign = new Dictionary<string, Assignment>();
            foreach (var l in lives)
            {
                var casting = l.Tasks.Where(t => res.Times.ContainsKey(t) && !p.IsSummary(t)).MaxBy(t => res.Times[t].Finish);
                if (casting == null) continue;
                var finish = res.Times[casting].Finish;
                var hold = Math.Max(0, p.Calendar.WorkingDays(finish.AddDays(1), l.To.AddDays(1)));
                var was = assign.GetValueOrDefault(casting);
                assign[casting] = new Assignment((was?.Units ?? 0) + 1, Math.Max(was?.Hold ?? 0, hold));
            }
            if (assign.Count == 0) throw new CommandException("No formwork stands on the schedule: lay formwork (Site works) on elements some task builds.");
            var capacity = a["capacity"] is not null ? ResNum(a["capacity"], "number of sets") : Math.Max(1, assign.Values.Max(x => x.Units));
            var attrs = new JsonObject { ["capacity"] = capacity, ["unit"] = "sets", ["assign"] = Resources.AssignJson(assign) };
            if (type != null) attrs["formworkType"] = type;
            var existing = ctx.Model.OfClass(Resources.Product).FirstOrDefault(x => x.Str("formworkType") == type && x.Attrs["derived"] is JsonValue);
            attrs["derived"] = true;
            var name = ResText(a["name"]) ?? (type != null && ctx.Model.Get(type) is { } te ? $"Formwork sets · {te.Name}" : "Formwork sets");
            if (existing != null) { ctx.Replace(existing.With(name: name, attrs: attrs)); return new JsonObject { ["id"] = existing.Id, ["tasks"] = assign.Count, ["formworks"] = lives.Count }; }
            var e = new Entity(ctx.NewId(), Resources.Product, name, attrs, "FORMWORK");
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id, ["tasks"] = assign.Count, ["formworks"] = lives.Count };
        });

        // The tasks not started placed again so no resource is asked for more than it has: a start of their own for those that move.
        // → {moved, finishBefore, finishAfter, delay, tooBig: [task ids]}
        r.Command("level", "Level resources", (ctx, _) =>
        {
            var p = ReadResolved(ctx.Query);
            if (p.Rows.Count == 0) throw new CommandException("The schedule has no tasks.");
            var (unlevelled, problem) = Solve(p, levelled: false);
            if (problem != null) throw new CommandException(problem);
            var resources = Resources.All(ctx.Model);
            if (resources.All(x => x.Assign.Count == 0)) throw new CommandException("No resource is on a task yet: Construction → Resources… puts them on the tasks.");
            var lv = Levelling.Level(p, unlevelled, resources);
            WriteLevelStarts(ctx, lv.Starts);
            return new JsonObject
            {
                ["moved"] = lv.Starts.Count, ["finishBefore"] = D(lv.FinishBefore.AddDays(-1)), ["finishAfter"] = D(lv.FinishAfter.AddDays(-1)),
                ["delay"] = p.Calendar.WorkingDays(lv.FinishBefore, lv.FinishAfter),
                ["tooBig"] = new JsonArray(lv.TooBig.Select(x => (JsonNode)x).ToArray()),
                ["tooBigNames"] = new JsonArray(lv.TooBig.Select(x => (JsonNode)p.ById[x].Name).ToArray()),
            };
        });

        // → {cleared}
        r.Command("clearLevelling", "Clear levelling", (ctx, _) =>
        {
            var n = WriteLevelStarts(ctx, new Dictionary<string, DateOnly>());
            return new JsonObject { ["cleared"] = n };
        });

        // The resources with their tasks, and each one's demand a day over the schedule (the histogram), its peak and the days it is short.
        r.Query("resources", (q, _) =>
        {
            var p = ReadResolved(q);
            var (res, _) = Solve(p);
            var list = Resources.All(q.Model);
            var load = Resources.LoadOf(p, res, list);
            return new JsonObject
            {
                ["from"] = D(load.From), ["to"] = D(load.To),
                ["nonWorking"] = new JsonArray(load.Days.Select((d, i) => (d, i)).Where(x => !p.Calendar.Working(x.d)).Select(x => (JsonNode)x.i).ToArray()),
                ["levelled"] = p.Rows.Count(t => t.LevelStart != null),
                ["resources"] = new JsonArray(list.Select(x => (JsonNode)new JsonObject
                {
                    ["id"] = x.Id, ["name"] = x.Name, ["kind"] = x.Kind, ["capacity"] = x.Capacity, ["unit"] = x.Unit, ["rate"] = x.Rate, ["formworkType"] = x.FormworkType,
                    ["assign"] = new JsonArray(x.Assign.Where(kv => p.ById.ContainsKey(kv.Key)).OrderBy(kv => p.RowOf.GetValueOrDefault(kv.Key)).Select(kv => (JsonNode)new JsonObject
                    {
                        ["task"] = kv.Key, ["name"] = p.ById[kv.Key].Name, ["units"] = kv.Value.Units, ["hold"] = kv.Value.Hold,
                    }).ToArray()),
                    ["demand"] = new JsonArray(load.Demand[x.Id].Select(v => (JsonNode)Math.Round(v, 3)).ToArray()),
                    ["peak"] = Math.Round(load.Peak(x), 3), ["over"] = load.Over(x), ["cost"] = Resources.CostOf(x, load),
                }).ToArray()),
            };
        });
    }

    /// <summary>
    /// The levelling's starts written (each task's own; the others' cleared), in the command's undo step. Returns how many
    /// tasks changed. Public for Planning Pro's scenarios, which level with other limits and apply the result.
    /// </summary>
    public static int WriteLevelStarts(ICommandContext ctx, IReadOnlyDictionary<string, DateOnly> starts)
    {
        var n = 0;
        foreach (var t in ctx.Model.OfClass(Plan.TaskClass).ToList())
        {
            var want = starts.TryGetValue(t.Id, out var s) ? D(s) : null;
            if (t.Str("levelStart") == want) continue;
            var attrs = t.CloneAttrs();
            if (want == null) attrs.Remove("levelStart"); else attrs["levelStart"] = want;
            ctx.Replace(t.With(attrs: attrs));
            n++;
        }
        return n;
    }
}
