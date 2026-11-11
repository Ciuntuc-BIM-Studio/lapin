using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// Planning (4D, docs/plan-4d5d.md, stages 1–5): the project's work schedule — tasks in a WBS, milestones, links FS / SS /
/// FF / SF with lags, the work calendar with Romania's legal holidays — the critical path worked out on every change, the
/// model's elements in the tasks (each task's rule, part and exceptions, <see cref="TaskElements"/>), and the schedule in
/// and out as IFC processes, MS Project XML and a workbook; the generator (<see cref="Generator"/>, stage 3); the 4D simulation (<see cref="Timeline"/>, stage 4); progress and the baseline (stage 5, <see cref="TaskProgress"/>). Every command is one undo step; the dates,
/// floats and critical flags are derived (<see cref="Cpm"/>), never stored.
/// </summary>
public sealed partial class PlanningModule : ILapinModule, IDeletion
{
    public string Id => "planning";
    public string Name => "Planning";

    // ------------------------------------------------------------------ the schedule's entities

    /// <summary>The project's schedule and its calendar, made when there is none (a schedule starting next Monday on the default calendar).</summary>
    public static (Entity Schedule, Entity Calendar) EnsureSchedule(IEdit edit, string? scheduleId = null, string? calendarId = null)
    {
        var schedule = Plan.ScheduleOf(edit.Model);
        Entity? calendar = schedule?.Str("calendar") is { } cid ? edit.Model.Get(cid) : null;
        calendar ??= edit.Model.OfClass(WorkCalendar.Class).FirstOrDefault();
        if (calendar == null)
        {
            var id = calendarId != null && edit.Model.Get(calendarId) == null && edit.Model.GetRelation(calendarId) == null ? calendarId : edit.NewId();
            calendar = new Entity(id, WorkCalendar.Class, "Standard", WorkCalendar.Default.ToJson(), "FIRSTSHIFT");
            edit.Add(calendar);
        }
        if (schedule == null)
        {
            var id = scheduleId != null && edit.Model.Get(scheduleId) == null && edit.Model.GetRelation(scheduleId) == null ? scheduleId : edit.NewId();
            schedule = new Entity(id, Plan.ScheduleClass, "Work schedule",
                new JsonObject { ["start"] = WorkCalendar.Date(Plan.NextMonday(Plan.Today())), ["calendar"] = calendar.Id }, "PLANNED");
            edit.Add(schedule);
        }
        else if (schedule.Str("calendar") != calendar.Id)
        {
            var attrs = schedule.CloneAttrs();
            attrs["calendar"] = calendar.Id;
            schedule = schedule.With(attrs: attrs);
            edit.Replace(schedule);
        }
        return (schedule, calendar);
    }

    /// <summary>What makes a generated task the user's when changed by hand.</summary>
    static readonly string[] OwnKeys = ["name", "duration", "finish", "start", "constraint", "constraintDate", "milestone", "kind", "wet", "rule", "part"];

    static Entity RequireTask(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: Plan.TaskClass } e ? e : throw new CommandException("There is no such task.");

    static void Set(ICommandContext ctx, Entity e, Action<JsonObject> change, string? name = null, string? kind = null)
    {
        var attrs = e.CloneAttrs();
        change(attrs);
        ctx.Replace(new Entity(e.Id, e.Class, name ?? e.Name, attrs, kind ?? e.PredefinedType));
    }

    /// <summary>A sibling list written in order again (1, 2, 3…), so a task put between two stays there.</summary>
    static void Renumber(ICommandContext ctx, IEnumerable<string> ids)
    {
        var n = 0;
        foreach (var id in ids)
        {
            n++;
            if (ctx.Model.Get(id) is { } e && e.Num("order") != n) Set(ctx, e, a => a["order"] = n);
        }
    }

    /// <summary>The children of a parent (null: the top) in row order.</summary>
    static List<string> SiblingsOf(Plan p, string? parent) =>
        parent == null ? p.Rows.Where(t => t.Parent == null || !p.ById.ContainsKey(t.Parent)).Select(t => t.Id).ToList() : p.Children.GetValueOrDefault(parent)?.ToList() ?? [];

    static string? ParentIn(Plan p, string id) => p.ById[id].Parent is { } par && p.ById.ContainsKey(par) ? par : null;

    static int Days(JsonNode? n, string what)
    {
        if (Json.Number(n) is not { } d || !double.IsFinite(d)) throw new CommandException($"The {what} is a number of working days.");
        return (int)Math.Round(d);
    }

    static DateOnly DateArg(JsonNode? n, string what) =>
        WorkCalendar.ParseDate((string?)n) ?? throw new CommandException($"The {what} is a date (2026-11-09).");

    static string LinkType(JsonNode? n) => (string?)n is { } t ? Plan.LinkTypes.Contains(t.ToUpperInvariant()) ? t.ToUpperInvariant() : throw new CommandException("A link is FS, SS, FF or SF.") : "FS";

    /// <summary>A link checked before it is made: not a task to itself, not between a summary and what it holds, no loop.</summary>
    static void CheckLink(Plan p, PlanLink l)
    {
        if (l.From == l.To) throw new CommandException("A task cannot follow itself.");
        if (p.Ancestors(l.To).Contains(l.From) || p.Ancestors(l.From).Contains(l.To)) throw new CommandException("A summary task and a task under it cannot be linked.");
        var others = p.Links.Where(x => x.Id != l.Id && !(x.From == l.From && x.To == l.To)).ToList();
        if (Cpm.HasLoop(p.WithLinks(others), l)) throw new CommandException($"„{p.ById[l.From].Name}” → „{p.ById[l.To].Name}” would make a loop: a task would wait for itself.");
    }

    static void PutLink(ICommandContext ctx, PlanLink l)
    {
        var attrs = new JsonObject { ["type"] = l.Type, ["lag"] = l.Lag };
        if (ctx.Model.GetRelation(l.Id) is { } old) ctx.ReplaceRelation(old with { Relating = l.From, Related = [l.To], Attrs = attrs });
        else ctx.AddRelation(new Relation(l.Id, Plan.SequenceClass, l.From, [l.To], attrs));
    }

    /// <summary>
    /// The predecessors column as MS Project reads it: rows separated by commas or semicolons, each a row number, a type
    /// (FS when none) and a lag in working days ("3", "5SS", "7FF+2", "2FS-1d").
    /// </summary>
    public static List<(int Row, string Type, int Lag)> ParsePredecessors(string text)
    {
        var o = new List<(int, string, int)>();
        foreach (var raw in text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var m = Regex.Match(raw, @"^(\d+)\s*(FS|SS|FF|SF|IS|IÎ|ÎS|ÎÎ)?\s*(?:([+-])\s*(\d+)\s*(?:d|days?|z|zile|zi)?)?$", RegexOptions.IgnoreCase);
            if (!m.Success) throw new CommandException($"„{raw}” is not a predecessor: a row number, maybe a type and a lag (5SS+2).");
            var type = m.Groups[2].Success ? m.Groups[2].Value.ToUpperInvariant() switch { "IS" or "ÎS" => "SF", "IÎ" => "SS", "ÎÎ" => "FF", var t => t } : "FS";
            var lag = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) * (m.Groups[3].Value == "-" ? -1 : 1) : 0;
            o.Add((int.Parse(m.Groups[1].Value), type, lag));
        }
        return o;
    }

    public bool Delete(ICommandContext ctx, string id)
    {
        if (ctx.Model.Get(id) is not { Class: Plan.TaskClass }) return false;
        DeleteTasks(ctx, [id]);
        return true;
    }

    /// <summary>Tasks deleted with everything under them; their links go with them.</summary>
    static void DeleteTasks(ICommandContext ctx, IEnumerable<string> ids)
    {
        var p = Plan.Read(ctx.Model);
        var all = new HashSet<string>();
        foreach (var id in ids) if (p.ById.ContainsKey(id)) { all.Add(id); foreach (var d in p.Descendants(id)) all.Add(d); }
        foreach (var id in all) ctx.Remove(id);
    }

    // ------------------------------------------------------------------ registration

    public void Register(IModuleRegistry r)
    {
        r.Class(new(Plan.ScheduleClass, "Work schedule", [], "#5b7a99", Kind: "resource", Template: false));
        r.Class(new(WorkCalendar.Class, "Work calendar", [], "#5b7a99", Kind: "resource"));
        r.Class(new(Plan.TaskClass, "Task", [], "#5b7a99", Kind: "resource", Template: false));
        r.Class(new(Norms.Class, "Productivity norms", [], "#5b7a99", Kind: "resource"));
        r.Catalog("scheduleTemplates", new JsonArray(Generator.Templates.Select(t => (JsonNode)t.ToJson()).ToArray()));
        r.Service<IDeletion>(this);
        r.Service<IIfcProcesses>(new ScheduleIfc());
        r.Service<Lapin.Platform.Cde.ICdeRule>(new TaskCheck());

        r.Command("createSchedule", "New work schedule", (ctx, a) =>
        {
            var (s, c) = EnsureSchedule(ctx);
            if (a.ContainsKey("name") || a.ContainsKey("start")) UpdateSchedule(ctx, s, a);
            return new JsonObject { ["id"] = s.Id, ["calendar"] = c.Id };
        });

        r.Command("updateSchedule", "Work schedule", (ctx, a) =>
        {
            var (s, _) = EnsureSchedule(ctx);
            UpdateSchedule(ctx, s, a);
            return null;
        });

        // A task (or a milestone) before a task, after one, as the last under a summary, or at the end.
        r.Command("addTask", "New task", (ctx, a) =>
        {
            var (schedule, _) = EnsureSchedule(ctx);
            var p = Plan.Read(ctx.Model);
            var milestone = a["milestone"] is JsonValue mv && mv.TryGetValue<bool>(out var mb) && mb;
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : milestone ? "Milestone" : "New task";
            var duration = milestone ? 0 : a.ContainsKey("duration") ? Math.Max(0, Days(a["duration"], "duration")) : 5;
            string? parent = null;
            List<string> siblings;
            int at;
            if ((string?)a["before"] is { } before && p.ById.ContainsKey(before))
            {
                parent = ParentIn(p, before);
                siblings = SiblingsOf(p, parent);
                at = siblings.IndexOf(before);
            }
            else if ((string?)a["after"] is { } after && p.ById.ContainsKey(after))
            {
                parent = ParentIn(p, after);
                siblings = SiblingsOf(p, parent);
                at = siblings.IndexOf(after) + 1;
            }
            else
            {
                parent = (string?)a["parent"] is { } par && p.ById.ContainsKey(par) ? par : null;
                siblings = SiblingsOf(p, parent);
                at = siblings.Count;
            }
            var id = ctx.NewId();
            var attrs = new JsonObject { ["schedule"] = schedule.Id, ["order"] = at + 1, ["duration"] = duration };
            if (parent != null) attrs["parent"] = parent;
            if (milestone) attrs["milestone"] = true;
            ctx.Add(new Entity(id, Plan.TaskClass, name, attrs));
            siblings.Insert(at, id);
            Renumber(ctx, siblings);
            return new JsonObject { ["id"] = id };
        });

        r.Command("updateTask", "Change task", (ctx, a) =>
        {
            var e = RequireTask(ctx.Model, (string?)a["id"]);
            var p = Plan.Read(ctx.Model);
            var summary = p.IsSummary(e.Id);
            var name = a.ContainsKey("name") ? ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A task has a name.") : null;
            string? kind = null;
            if (a.ContainsKey("kind")) kind = (string?)a["kind"] is { } k && Plan.Kinds.Contains(k) ? k : (string?)a["kind"] is null or "" ? "" : throw new CommandException("Not a kind of task IFC knows.");
            if (summary && (a.ContainsKey("duration") || a.ContainsKey("finish") || a.ContainsKey("start") || a.ContainsKey("milestone") || a.ContainsKey("constraint")))
                throw new CommandException("A summary task takes its dates from the tasks under it.");
            var attrs = e.CloneAttrs();
            if (a.ContainsKey("milestone"))
            {
                var m = a["milestone"] is JsonValue mv && mv.TryGetValue<bool>(out var mb) && mb;
                if (m) { attrs["milestone"] = true; attrs["duration"] = 0; }
                else { attrs.Remove("milestone"); if (e.Num("duration") <= 0) attrs["duration"] = 1; }
            }
            if (a.ContainsKey("duration"))
            {
                var d = Days(a["duration"], "duration");
                if (d < 0) throw new CommandException("A duration is not negative.");
                attrs["duration"] = d;
                if (d == 0) attrs["milestone"] = true; else attrs.Remove("milestone");
            }
            if (a.ContainsKey("constraint"))
            {
                var c = (string?)a["constraint"];
                if (c is null or "" or "asap") { attrs.Remove("constraint"); attrs.Remove("constraintDate"); }
                else if (!Plan.Constraints.Contains(c)) throw new CommandException("A constraint is snet (start no earlier than) or mso (must start on).");
                else
                {
                    attrs["constraint"] = c;
                    attrs["constraintDate"] = WorkCalendar.Date(a.ContainsKey("constraintDate") ? DateArg(a["constraintDate"], "constraint's date") : Solve(ReadResolved(ctx.Query)).Result.Times[e.Id].Start);
                }
            }
            else if (a.ContainsKey("constraintDate") && attrs["constraint"] != null) attrs["constraintDate"] = WorkCalendar.Date(DateArg(a["constraintDate"], "constraint's date"));
            // Dragged or typed to a start: it starts no earlier than that (its must-start-on date moves, when it has one).
            if (a.ContainsKey("start"))
            {
                var s = DateArg(a["start"], "start");
                if ((string?)attrs["constraint"] != "mso") attrs["constraint"] = "snet";
                attrs["constraintDate"] = WorkCalendar.Date(s);
                attrs.Remove("levelStart");
            }
            // Dragged or typed to a finish: its duration is the working days from its start to that day.
            if (a.ContainsKey("finish"))
            {
                var f = DateArg(a["finish"], "finish");
                var start = a.ContainsKey("start") ? p.Calendar.NextWorking(DateArg(a["start"], "start"), e.Bool("wet")) : Solve(ReadResolved(ctx.Query)).Result.Times[e.Id].Start;
                var d = Math.Max(1, p.Calendar.WorkingDays(start, f.AddDays(1), attrs["wet"] is JsonValue wv && wv.TryGetValue<bool>(out var wb) && wb));
                attrs["duration"] = d;
                attrs.Remove("milestone");
            }
            if (a.ContainsKey("wet")) { if (a["wet"] is JsonValue wv && wv.TryGetValue<bool>(out var w) && w) attrs["wet"] = true; else attrs.Remove("wet"); }
            if (a.ContainsKey("notes")) { if (((string?)a["notes"])?.Trim() is { Length: > 0 } notes) attrs["notes"] = notes; else attrs.Remove("notes"); }
            SetLinks(attrs, a);
            // A generated task changed by hand is the user's now: the generator leaves it as it is (its exceptions are not such a change).
            if (a.ContainsKey("own")) { if (a["own"] is JsonValue ov && ov.TryGetValue<bool>(out var ob) && ob) attrs["own"] = true; else attrs.Remove("own"); }
            else if (e.Str("gen") != null && OwnKeys.Any(a.ContainsKey)) attrs["own"] = true;
            ctx.Replace(new Entity(e.Id, e.Class, name ?? e.Name, attrs, kind == null ? e.PredefinedType : kind.Length == 0 ? null : kind));
            return null;
        });

        r.Command("deleteTasks", "Delete tasks", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            if (ids.Count == 0) throw new CommandException("Select the tasks to delete.");
            DeleteTasks(ctx, ids);
            return null;
        });

        // Up or down among its siblings (offset −1 or +1), its subtree with it.
        r.Command("moveTask", "Move task", (ctx, a) =>
        {
            var e = RequireTask(ctx.Model, (string?)a["id"]);
            var p = Plan.Read(ctx.Model);
            var siblings = SiblingsOf(p, ParentIn(p, e.Id));
            var i = siblings.IndexOf(e.Id);
            var j = Math.Clamp(i + (int)Math.Round(Json.Number(a["offset"]) ?? 0), 0, siblings.Count - 1);
            if (i == j) return null;
            siblings.RemoveAt(i);
            siblings.Insert(j, e.Id);
            Renumber(ctx, siblings);
            return null;
        });

        // Indented: each task becomes the last under the task before it at its level (which becomes a summary).
        r.Command("indent", "Indent", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            var p = Plan.Read(ctx.Model);
            var done = 0;
            foreach (var id in ids.Where(p.ById.ContainsKey).OrderBy(id => p.RowOf[id]))
            {
                p = Plan.Read(ctx.Model);
                var parent = ParentIn(p, id);
                // A task whose parent was indented with it goes along.
                if (parent != null && ids.Contains(parent)) continue;
                var siblings = SiblingsOf(p, parent);
                var i = siblings.IndexOf(id);
                if (i <= 0) continue;
                var into = siblings[i - 1];
                var under = SiblingsOf(p, into);
                // A link between the task and its new parent cannot stay: it goes.
                foreach (var l in p.Links.Where(l => (l.From == id && l.To == into) || (l.From == into && l.To == id))) ctx.RemoveRelation(l.Id);
                Set(ctx, ctx.Model.Get(id)!, at => { at["parent"] = into; at["order"] = under.Count + 1; });
                done++;
            }
            if (done == 0) throw new CommandException("A task is indented under the task above it at its level: the first has none.");
            return null;
        });

        // Outdented: each task goes up a level, right after its parent; the tasks after it under that parent become its own (the rows keep their order).
        r.Command("outdent", "Outdent", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            var p0 = Plan.Read(ctx.Model);
            var done = 0;
            foreach (var id in ids.Where(p0.ById.ContainsKey).OrderByDescending(id => p0.RowOf[id]))
            {
                var p = Plan.Read(ctx.Model);
                var parent = ParentIn(p, id);
                if (parent == null) continue;
                var siblings = SiblingsOf(p, parent);
                var i = siblings.IndexOf(id);
                var own = SiblingsOf(p, id);
                var after = siblings.Skip(i + 1).ToList();
                foreach (var (x, k) in after.Select((x, k) => (x, k)))
                {
                    foreach (var l in p.Links.Where(l => (l.From == id && l.To == x) || (l.From == x && l.To == id))) ctx.RemoveRelation(l.Id);
                    Set(ctx, ctx.Model.Get(x)!, at => { at["parent"] = id; at["order"] = own.Count + k + 1; });
                }
                var grand = ParentIn(p, parent);
                var up = SiblingsOf(p, grand);
                up.Insert(up.IndexOf(parent) + 1, id);
                Set(ctx, ctx.Model.Get(id)!, at => { if (grand == null) at.Remove("parent"); else at["parent"] = grand; });
                Renumber(ctx, up);
                done++;
            }
            if (done == 0) throw new CommandException("Only a task under a summary can be outdented.");
            return null;
        });

        // A link from a predecessor to a successor; one between the same two tasks already is changed instead.
        r.Command("link", "Link tasks", (ctx, a) =>
        {
            var from = RequireTask(ctx.Model, (string?)a["from"]).Id;
            var to = RequireTask(ctx.Model, (string?)a["to"]).Id;
            var p = Plan.Read(ctx.Model);
            var existing = p.Links.FirstOrDefault(l => l.From == from && l.To == to);
            var l = new PlanLink(existing?.Id ?? ctx.NewId(), from, to, a.ContainsKey("type") ? LinkType(a["type"]) : existing?.Type ?? "FS",
                a.ContainsKey("lag") ? Days(a["lag"], "lag") : existing?.Lag ?? 0);
            CheckLink(p, l);
            PutLink(ctx, l);
            return new JsonObject { ["id"] = l.Id };
        });

        // The tasks selected linked one after the other, in row order (FS).
        r.Command("linkChain", "Link tasks", (ctx, a) =>
        {
            var p = Plan.Read(ctx.Model);
            var ids = Args.Ids(a).Where(p.ById.ContainsKey).Distinct().OrderBy(id => p.RowOf[id]).ToList();
            if (ids.Count < 2) throw new CommandException("Select two tasks or more to link them one after the other.");
            for (var i = 1; i < ids.Count; i++)
            {
                p = Plan.Read(ctx.Model);
                if (p.Links.Any(l => l.From == ids[i - 1] && l.To == ids[i])) continue;
                var l = new PlanLink(ctx.NewId(), ids[i - 1], ids[i], "FS", 0);
                CheckLink(p, l);
                PutLink(ctx, l);
            }
            return null;
        });

        r.Command("updateLink", "Change link", (ctx, a) =>
        {
            var rel = ctx.Model.GetRelation((string?)a["id"] ?? "") is { Class: Plan.SequenceClass } x ? x : throw new CommandException("There is no such link.");
            var l = Plan.ReadLink(rel)!;
            l = l with { Type = a.ContainsKey("type") ? LinkType(a["type"]) : l.Type, Lag = a.ContainsKey("lag") ? Days(a["lag"], "lag") : l.Lag };
            CheckLink(Plan.Read(ctx.Model), l);
            PutLink(ctx, l);
            return null;
        });

        // Links taken off: by id, or every link among the tasks given (both ways).
        r.Command("unlink", "Unlink tasks", (ctx, a) =>
        {
            var p = Plan.Read(ctx.Model);
            var gone = new List<string>();
            if (a["links"] is JsonArray la) gone.AddRange(la.Select(x => (string?)x).OfType<string>());
            if ((string?)a["link"] is { } one) gone.Add(one);
            var ids = Args.Ids(a).ToHashSet();
            if (ids.Count == 1) gone.AddRange(p.Links.Where(l => ids.Contains(l.From) || ids.Contains(l.To)).Select(l => l.Id));
            else if (ids.Count > 1) gone.AddRange(p.Links.Where(l => ids.Contains(l.From) && ids.Contains(l.To)).Select(l => l.Id));
            var n = 0;
            foreach (var id in gone.Distinct()) if (ctx.Model.GetRelation(id) is { Class: Plan.SequenceClass }) { ctx.RemoveRelation(id); n++; }
            if (n == 0) throw new CommandException("No link to take off.");
            return new JsonObject { ["removed"] = n };
        });

        // The predecessors column typed: the task's links in from others replaced by those it lists.
        r.Command("setPredecessors", "Predecessors", (ctx, a) =>
        {
            var e = RequireTask(ctx.Model, (string?)a["id"]);
            var p = Plan.Read(ctx.Model);
            var wanted = ParsePredecessors((string?)a["text"] ?? "");
            var byRow = p.RowOf.ToDictionary(kv => kv.Value, kv => kv.Key);
            var keep = p.Links.Where(l => l.To != e.Id).ToList();
            var made = new List<PlanLink>();
            foreach (var (row, type, lag) in wanted)
            {
                if (!byRow.TryGetValue(row, out var from)) throw new CommandException($"There is no task {row}.");
                var existing = p.Links.FirstOrDefault(l => l.From == from && l.To == e.Id);
                var l = new PlanLink(existing?.Id ?? ctx.NewId(), from, e.Id, type, lag);
                CheckLink(p.WithLinks([.. keep, .. made]), l);
                made.Add(l);
            }
            foreach (var l in p.Links.Where(l => l.To == e.Id && made.All(m => m.Id != l.Id))) ctx.RemoveRelation(l.Id);
            foreach (var l in made) PutLink(ctx, l);
            return null;
        });

        // The calendar's fields changed (the weekdays, the day's start and hours, the holidays, the exceptions, the winter break).
        r.Command("setCalendar", "Work calendar", (ctx, a) =>
        {
            var (_, cal) = EnsureSchedule(ctx);
            var o = WorkCalendar.Read(cal.Attrs).ToJson();
            foreach (var key in new[] { "days", "dayStart", "hours", "holidays", "exceptions" })
                if (a.ContainsKey(key)) o[key] = a[key]?.DeepClone();
            if (a["winter"] is JsonObject w)
            {
                var cur = (JsonObject)o["winter"]!;
                foreach (var (k, v) in w) cur[k] = v?.DeepClone();
            }
            if (o["days"] is JsonArray days && !days.Any(d => Json.Number(d) is >= 1 and <= 7)) throw new CommandException("The calendar works on one weekday at least.");
            if (a.ContainsKey("hours") && Json.Number(a["hours"]) is not (> 0 and <= 24)) throw new CommandException("A working day has between 0 and 24 hours.");
            var parsed = WorkCalendar.Read(o);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : cal.Name;
            ctx.Replace(cal.With(name: name, attrs: parsed.ToJson()));
            return null;
        });

        // An MS Project XML file (by its text, or its bytes in base64) in place of the project's schedule.
        r.Command("importMsp", "Import MS Project XML", (ctx, a) =>
        {
            var xml = (string?)a["xml"] ?? ((string?)a["data"] is { } b64 ? Encoding.UTF8.GetString(Convert.FromBase64String(b64)) : throw new CommandException("No MS Project XML to read."));
            var s = MsProject.Read(xml.TrimStart('﻿'));
            return ScheduleImport.Apply(ctx, s).ToJson();
        });

        // Elements put in a task by hand (dragged onto its row, TA), or taken out of it (Shift): exceptions to its rule, the rule kept.
        r.Command("assign", "Add to task", (ctx, a) =>
        {
            var e = RequireTask(ctx.Model, (string?)a["task"] ?? (string?)a["id"]);
            var remove = a["remove"] is JsonValue rv && rv.TryGetValue<bool>(out var rb) && rb;
            var (ids, _) = Groups.Expand(ctx.Model, IdList(a["ids"]));
            var p = Plan.Read(ctx.Model);
            var task = p.ById[e.Id];
            var links = TaskElements.Resolve(ctx, p);
            var elements = ids.Select(links.Facts).OfType<ElementFacts>().ToList();
            if (elements.Count == 0) throw new CommandException(remove ? "Select the task's elements to take out of it." : "Select elements of the model to add to the task.");
            var add = task.Add.ToList();
            var gone = task.Remove.ToList();
            int changed = 0, noPart = 0;
            foreach (var f in elements)
            {
                var id = f.Element.Id;
                var byRule = links.RuleTakes(task, f);
                if (remove)
                {
                    if (add.Remove(id)) changed++;
                    if (byRule && !gone.Contains(id)) { gone.Add(id); changed++; }
                }
                else
                {
                    if (TaskParts.Mask(task.Part, f, ctx.Model) == 0) { noPart++; continue; }
                    if (gone.Remove(id)) changed++;
                    if (!byRule && !add.Contains(id)) { add.Add(id); changed++; }
                }
            }
            if (changed > 0)
            {
                var attrs = e.CloneAttrs();
                WriteIds(attrs, "add", add);
                WriteIds(attrs, "remove", gone);
                ctx.Replace(e.With(attrs: attrs));
            }
            return new JsonObject { ["changed"] = changed, ["noPart"] = noPart, ["count"] = TaskElements.Resolve(ctx).ByTask.GetValueOrDefault(e.Id)?.Count ?? 0 };
        });

        // A rule made from the elements selected: their classes and their storeys; the exceptions it now covers dropped.
        r.Command("ruleFromSelection", "Task rule", (ctx, a) =>
        {
            var e = RequireTask(ctx.Model, (string?)a["id"]);
            var (ids, _) = Groups.Expand(ctx.Model, IdList(a["ids"]));
            var links = TaskElements.Resolve(ctx);
            var elements = ids.Select(links.Facts).OfType<ElementFacts>().ToList();
            if (elements.Count == 0) throw new CommandException("Select elements of the model: the rule takes their kinds and storeys.");
            var rule = new TaskRule(elements.Select(f => f.Element.Class).Distinct().ToList(), [], elements.Select(f => f.Storey).OfType<string>().Distinct().ToList(), [], [], []);
            var attrs = e.CloneAttrs();
            attrs["rule"] = rule.ToJson();
            var task = Plan.ReadTask(e);
            WriteIds(attrs, "add", task.Add.Where(id => links.Facts(id) is not { } f || !rule.Matches(f, PhasesModule.Settings(ctx.Model).DefaultCreated?.Id ?? "")).ToList());
            if (e.Str("gen") != null) attrs["own"] = true;
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        RegisterGenerator(r);
        RegisterSimulation(r);
        RegisterProgress(r);
        r.Service<IVersionCompare>(new ScheduleCompare());
        // The schedule read by others (stage 7: the cost module lays its estimate on it).
        r.Service<IWorkSchedules>(new ScheduleTimesOf());

        r.Query("schedule", (q, _) => ScheduleJson(q));

        // What a task's rule can name, from the model's elements: their classes, types, storeys, materials, groups and phases, each with how many elements have it.
        r.Query("ruleOptions", (q, _) => RuleOptions(q));

        // The elements no task takes, and those some of whose layers no task takes (what the CDE check lists).
        r.Query("unlinked", (q, _) =>
        {
            var links = TaskElements.Resolve(q);
            var (none, partial) = links.Unlinked();
            return new JsonObject
            {
                ["none"] = new JsonArray(none.Select(f => (JsonNode)f.Element.Id).ToArray()),
                ["partial"] = new JsonArray(partial.Select(x => (JsonNode)x.Element.Element.Id).ToArray()),
            };
        });

        // A year's legal holidays (the calendar dialog lists them).
        r.Query("holidays", (q, a) =>
        {
            var year = (int)(Json.Number(a["year"]) ?? Plan.Today().Year);
            return new JsonArray(WorkCalendar.RomanianHolidays(year).Select(h => (JsonNode)new JsonObject { ["date"] = WorkCalendar.Date(h.Date), ["name"] = h.Name }).ToArray());
        });

        r.Query("exportMsp", (q, _) =>
        {
            var p = ReadResolved(q);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(MsProject.Write(p, Solve(p).Result)));
        });

        r.Query("exportXlsx", (q, _) =>
        {
            var p = ReadResolved(q);
            return Convert.ToBase64String(Lapin.Drawing.Export.XlsxWriter.Write([("Schedule", Table(p, Solve(p).Result, q)), ("Calendar", CalendarTable(p))]));
        });
    }

    // ------------------------------------------------------------------ the elements of a task (stage 2)

    static List<string> IdList(JsonNode? n) => n is JsonArray arr ? arr.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>().Distinct().ToList() : [];

    static void WriteIds(JsonObject attrs, string key, IReadOnlyList<string> ids)
    {
        if (ids.Count == 0) attrs.Remove(key);
        else attrs[key] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray());
    }

    /// <summary>A task's rule (an object, null to take it off), its part (null or "all": the whole elements) and its exceptions (the lists as given) set from a command's arguments.</summary>
    static void SetLinks(JsonObject attrs, JsonObject a)
    {
        if (a.ContainsKey("rule"))
        {
            if (a["rule"] is JsonObject) attrs["rule"] = TaskRule.Read(a["rule"])!.ToJson();
            else if (a["rule"] is null) attrs.Remove("rule");
            else throw new CommandException("A task's rule is an object (classes, types, storeys, materials, groups, phases), or null.");
        }
        if (a.ContainsKey("part")) { if (TaskParts.Parse(a["part"]) is { } part) attrs["part"] = part; else attrs.Remove("part"); }
        if (a.ContainsKey("add")) WriteIds(attrs, "add", IdList(a["add"]));
        if (a.ContainsKey("remove")) WriteIds(attrs, "remove", IdList(a["remove"]));
    }

    static JsonObject RuleOptions(IQueryContext q)
    {
        var links = TaskElements.Resolve(q);
        var m = q.Model;
        JsonArray Counted(IEnumerable<string?> keys, Func<string, JsonObject?> make)
        {
            var o = new JsonArray();
            foreach (var g in keys.OfType<string>().GroupBy(k => k).OrderBy(g => g.Key, StringComparer.Ordinal))
                if (make(g.Key) is { } x) { x["count"] = g.Count(); o.Add((JsonNode)x); }
            return o;
        }
        JsonObject? Named(string id) => m.Get(id) is { } e ? new JsonObject { ["id"] = id, ["name"] = e.Name } : null;
        var storeyOrder = Spatial.Storeys(m).Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var storeys = Counted(links.Candidates.Select(f => f.Storey), Named);
        var sorted = storeys.OfType<JsonObject>().OrderBy(o => storeyOrder.GetValueOrDefault((string)o["id"]!, int.MaxValue)).Select(o => (JsonNode)o.DeepClone()).ToArray();
        var ph = PhasesModule.Settings(m);
        var defaultPhase = ph.DefaultCreated?.Id ?? "";
        return new JsonObject
        {
            ["classes"] = Counted(links.Candidates.Select(f => f.Element.Class), c => new JsonObject { ["id"] = c, ["name"] = q.Describe(c)?.Label ?? c.Replace("Ifc", "") }),
            ["types"] = Counted(links.Candidates.Select(f => f.Type), id => m.Get(id) is { } t ? new JsonObject { ["id"] = id, ["name"] = t.Name, ["class"] = t.Class } : null),
            ["storeys"] = new JsonArray(sorted),
            ["materials"] = Counted(links.Candidates.SelectMany(f => f.Materials), id => m.Get(id) is { } mat ? new JsonObject { ["id"] = id, ["name"] = mat.Name, ["category"] = mat.Str("category") } : null),
            ["groups"] = Counted(links.Candidates.Select(f => f.Group), Named),
            ["phases"] = new JsonArray(ph.Phases.Select(p => (JsonNode)new JsonObject { ["id"] = p.Id, ["name"] = p.Name, ["kind"] = p.Kind, ["count"] = links.Candidates.Count(f => (f.Phase ?? defaultPhase) == p.Id) }).ToArray()),
            ["demolished"] = new JsonArray(ph.Phases.Select(p => (p, n: links.Candidates.Count(f => f.Demolished == p.Id))).Where(x => x.n > 0)
                .Select(x => (JsonNode)new JsonObject { ["id"] = x.p.Id, ["name"] = x.p.Name, ["kind"] = x.p.Kind, ["count"] = x.n }).ToArray()),
            ["categories"] = new JsonArray(Platform.Materials.Categories.Select(c => (JsonNode)c).ToArray()),
        };
    }

    static void UpdateSchedule(ICommandContext ctx, Entity s, JsonObject a)
    {
        var attrs = s.CloneAttrs();
        if (a.ContainsKey("start")) attrs["start"] = WorkCalendar.Date(DateArg(a["start"], "start"));
        if (a.ContainsKey("dataDate")) { if ((string?)a["dataDate"] is { Length: > 0 }) attrs["dataDate"] = WorkCalendar.Date(DateArg(a["dataDate"], "status date")); else attrs.Remove("dataDate"); }
        var name = a.ContainsKey("name") ? ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A schedule has a name.") : s.Name;
        ctx.Replace(s.With(name: name, attrs: attrs));
    }

    /// <summary>The critical path (without the levelling's starts when <paramref name="levelled"/> is false), or (links looping, from a file) every task at the start, with the reason.</summary>
    public static (CpmResult Result, string? Problem) Solve(Plan p, bool levelled = true)
    {
        try { return (Cpm.Solve(p, levelled), null); }
        catch (CommandException ex)
        {
            var start = p.Calendar.NextWorking(p.Start);
            var times = p.Rows.ToDictionary(t => t.Id, t => new TaskTimes(start, start, start, start, 0, 0, false, 0));
            return (new CpmResult(times, start, new HashSet<string>()), ex.Message);
        }
    }

    static string D(DateOnly d) => WorkCalendar.Date(d);

    /// <summary>The schedule as the Gantt shows it: its tasks in row order with their dates, floats and critical flags; the links; the calendar and its days not worked.</summary>
    public static JsonObject ScheduleJson(IQueryContext q)
    {
        var m = q.Model;
        var p = ReadResolved(q, out var links);
        var (r, problem) = Solve(p);
        var (none, partial) = links.Unlinked();
        // Each task's cost from the estimate (stage 7), when the cost module is there and the project has one.
        var costs = p.Rows.Count > 0 ? q.Services.Get<IScheduleCosts>()?.Costs(q, ScheduleTimesOf.Of(p, r, links)) : null;
        var incoming = p.Links.GroupBy(l => l.To).ToDictionary(g => g.Key, g => g.OrderBy(l => p.RowOf.GetValueOrDefault(l.From)).ToList());
        var tasks = new JsonArray();
        foreach (var t in p.Rows)
        {
            var tm = r.Times[t.Id];
            var summary = p.IsSummary(t.Id);
            var milestone = !summary && (t.Milestone || t.Duration == 0);
            var o = new JsonObject
            {
                ["id"] = t.Id, ["row"] = p.RowOf[t.Id], ["name"] = t.Name, ["wbs"] = p.Wbs[t.Id], ["level"] = p.Level[t.Id],
                ["parent"] = t.Parent is { } par && p.ById.ContainsKey(par) ? par : null,
                ["summary"] = summary, ["milestone"] = milestone, ["duration"] = summary ? tm.Duration : milestone ? 0 : t.Duration,
                ["start"] = D(tm.Start), ["finish"] = D(tm.Finish),
                ["lateStart"] = D(tm.LateStart), ["lateFinish"] = D(tm.LateFinish > tm.LateStart ? tm.LateFinish.AddDays(-1) : tm.LateStart),
                ["totalFloat"] = tm.TotalFloat, ["freeFloat"] = tm.FreeFloat, ["critical"] = tm.Critical,
                ["predecessors"] = string.Join(", ", (incoming.GetValueOrDefault(t.Id) ?? []).Select(p.LinkText)),
            };
            if (t.Constraint != null && t.ConstraintDate is { } cd) { o["constraint"] = t.Constraint; o["constraintDate"] = D(cd); }
            if (t.Wet) o["wet"] = true;
            if (t.LevelStart is { } lvs) o["levelStart"] = D(lvs);
            if (t.Kind != null) o["kind"] = t.Kind;
            if (t.Notes != null) o["notes"] = t.Notes;
            // Its elements (a summary's: those of the tasks under it too), and how they are found.
            var elements = links.Under(t.Id);
            o["count"] = elements.Count;
            if (elements.Count > 0) o["elements"] = new JsonArray(elements.Select(x => (JsonNode)x).ToArray());
            if (t.Rule != null) o["rule"] = t.Rule.ToJson();
            if (t.Part != null) o["part"] = t.Part;
            if (t.Add.Count > 0) o["added"] = t.Add.Count;
            if (t.Remove.Count > 0) o["removed"] = t.Remove.Count;
            // Made by the generator (stage 3): from which step, whether edited by hand since, and where its duration came from.
            // Its progress (stage 5): how much is done and where that came from, its status and actual dates, the marks on its elements; its baseline and how far it moved.
            var pr = p.ProgressOf(t.Id);
            o["complete"] = Math.Round(pr.Complete, 1);
            o["progressFrom"] = pr.From;
            o["status"] = tm.Status;
            if (tm.Status == "started") o["remaining"] = tm.Remaining;
            if (pr.ActualStart is { } ast) o["actualStart"] = D(ast);
            if (pr.ActualFinish is { } afi) o["actualFinish"] = D(afi);
            if (pr.Done + pr.Started > 0) { o["marked"] = new JsonObject { ["done"] = pr.Done, ["started"] = pr.Started, ["of"] = pr.Count }; }
            if (!summary)
            {
                if (t.Complete is { } own) o["completeOwn"] = own;
                if (t.ActualStart is { } ownS) o["actualStartOwn"] = D(ownS);
                if (t.ActualFinish is { } ownF) o["actualFinishOwn"] = D(ownF);
                if (t.Marks.Count > 0) o["marks"] = new JsonObject(t.Marks.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value.ToJson())));
            }
            if (costs != null && costs.Tasks.TryGetValue(t.Id, out var tc)) { o["cost"] = tc.Cost; o["costDone"] = tc.Done; }
            if (t.Baseline is { } bl)
            {
                o["baselineStart"] = D(bl.Start);
                o["baselineFinish"] = D(bl.Finish);
                o["baselineDuration"] = bl.Duration;
                o["startVariance"] = bl.StartVariance(p.Calendar, tm.Start);
                o["finishVariance"] = bl.FinishVariance(p.Calendar, tm.Finish);
            }
            if (m.Get(t.Id) is { } te)
            {
                if (te.Str("gen") is { } gen) o["gen"] = gen;
                if (te.Bool("own")) o["own"] = true;
                foreach (var k in new[] { "durationFrom", "norm", "unit" }) if (te.Str(k) is { } v) o[k] = v;
                if (te.NumOrNull("quantity") is { } qv) o["quantity"] = qv;
                if (te.NumOrNull("crew") is { } cv) o["crew"] = (int)Math.Round(cv);
            }
            tasks.Add((JsonNode)o);
        }
        var linkRows = new JsonArray(p.Links.Select(l => (JsonNode)new JsonObject
        {
            ["id"] = l.Id, ["from"] = l.From, ["to"] = l.To, ["type"] = l.Type, ["lag"] = l.Lag, ["critical"] = r.CriticalLinks.Contains(l.Id),
        }).ToArray());
        var finish = r.Times.Count > 0 ? r.Times.Values.Max(t => t.Finish) : p.Start;
        var cal = p.Calendar.ToJson();
        cal["id"] = p.CalendarEntity?.Id;
        cal["name"] = p.CalendarEntity?.Name ?? "Standard";
        var shownFrom = p.Start.AddDays(-62);
        var leaves = p.Rows.Where(t => !p.IsSummary(t.Id)).ToList();
        double weights = leaves.Sum(t => t.Milestone ? 0 : Math.Max(0, t.Duration));
        var overall = leaves.Count == 0 ? 0 : weights > 0 ? leaves.Sum(t => p.ProgressOf(t.Id).Complete * (t.Milestone ? 0 : Math.Max(0, t.Duration))) / weights : leaves.Average(t => p.ProgressOf(t.Id).Complete);
        var late = leaves.Count(t => t.Baseline is { } b && r.Times.TryGetValue(t.Id, out var x) && b.FinishVariance(p.Calendar, x.Finish) > 0);
        var baseFinish = p.Rows.Where(t => t.Baseline != null).Select(t => t.Baseline!.Finish).DefaultIfEmpty().Max();
        var shownTo = finish.AddDays(400);
        return new JsonObject
        {
            ["id"] = p.Schedule?.Id, ["name"] = p.Name, ["start"] = D(p.Start), ["finish"] = D(finish),
            ["dataDate"] = p.DataDate is { } dd ? D(dd) : null,
            // Progress and the baseline (stage 5): the whole schedule's percentage (by the tasks' durations), the tasks late on the baseline, when it was saved and the finish it had.
            ["complete"] = Math.Round(overall, 1),
            ["baseline"] = p.Rows.Any(t => t.Baseline != null) ? new JsonObject
            {
                ["saved"] = p.BaselineSaved is { } bs ? D(bs) : null, ["finish"] = D(baseFinish),
                ["finishVariance"] = new TaskBaseline(baseFinish, baseFinish, 0).FinishVariance(p.Calendar, finish), ["late"] = late,
            } : null,
            ["workingDays"] = p.Calendar.WorkingDays(p.Calendar.NextWorking(p.Start), r.Finish),
            ["calendar"] = cal,
            ["nonWorking"] = new JsonArray(p.Calendar.NonWorking(shownFrom, shownTo).Select(x => (JsonNode)new JsonObject { ["date"] = D(x.Day), ["why"] = x.Why }).ToArray()),
            ["tasks"] = tasks, ["links"] = linkRows, ["problem"] = problem,
            // The estimate on the schedule (stage 7): the budget at completion, what the tasks carry of it, the cost to date.
            ["cost"] = costs == null ? null : new JsonObject { ["currency"] = costs.Currency, ["budget"] = costs.Budget, ["scheduled"] = costs.Scheduled, ["toDate"] = costs.ToDate },
            // The elements a task could take, how many some task takes, and those no task takes (whole, or some of their layers).
            ["elements"] = new JsonObject { ["total"] = links.Candidates.Count(links.NeedsTask), ["none"] = none.Count, ["partial"] = partial.Count },
            // The formwork and scaffolds, each on site while its hosts' tasks need it.
            ["temporaries"] = TemporariesJson(TaskLives.Of(p, r, links, q.Services, m)),
        };
    }

    static JsonArray TemporariesJson(IEnumerable<TemporaryTime> lives) => new(lives.Select(x => (JsonNode)new JsonObject
    {
        ["id"] = x.Element, ["kind"] = x.Kind, ["from"] = D(x.From), ["to"] = D(x.To), ["tasks"] = new JsonArray(x.Tasks.Select(t => (JsonNode)t).ToArray()),
    }).ToArray());

    /// <summary>The schedule as a table: what the Gantt's columns show, a row a task.</summary>
    public static List<IReadOnlyList<string>> Table(Plan p, CpmResult r, IQueryContext q)
    {
        var incoming = p.Links.GroupBy(l => l.To).ToDictionary(g => g.Key, g => g.ToList());
        var links = TaskElements.Resolve(q, p);
        var rows = new List<IReadOnlyList<string>> { new[] { "ID", "WBS", "Task", "Duration (working days)", "Start", "Finish", "Predecessors", "Total float", "Free float", "Critical", "Milestone", "Summary",
            "% complete", "Actual start", "Actual finish", "Baseline start", "Baseline finish", "Finish variance", "GlobalId", "Elements", "Rule" } };
        foreach (var t in p.Rows)
        {
            var tm = r.Times[t.Id];
            var summary = p.IsSummary(t.Id);
            var milestone = !summary && (t.Milestone || t.Duration == 0);
            rows.Add([
                $"{p.RowOf[t.Id]}", p.Wbs[t.Id], new string(' ', 2 * p.Level[t.Id]) + t.Name, $"{(summary ? tm.Duration : milestone ? 0 : t.Duration)}",
                D(tm.Start), D(tm.Finish), string.Join(", ", (incoming.GetValueOrDefault(t.Id) ?? []).Select(p.LinkText)),
                $"{tm.TotalFloat}", $"{tm.FreeFloat}", tm.Critical ? "yes" : "", milestone ? "yes" : "", summary ? "yes" : "",
                Pct(p.ProgressOf(t.Id).Complete), p.ProgressOf(t.Id).ActualStart is { } ast ? D(ast) : "", p.ProgressOf(t.Id).ActualFinish is { } afi ? D(afi) : "",
                t.Baseline is { } bl ? D(bl.Start) : "", t.Baseline is { } bf ? D(bf.Finish) : "", t.Baseline is { } bv ? $"{bv.FinishVariance(p.Calendar, tm.Finish)}" : "", t.Id,
                $"{links.Under(t.Id).Count}", t.Links || t.Part != null ? TaskElements.Describe(q.Model, q.Describe, t.Rule, t.Part, t.Add.Count, t.Remove.Count) : "",
            ]);
        }
        return rows;
    }

    static string Pct(double v) => Math.Round(v, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The calendar as a table: its rules, then each day not worked from the schedule's first year to the year after its finish.</summary>
    static List<IReadOnlyList<string>> CalendarTable(Plan p)
    {
        var c = p.Calendar;
        var names = new[] { "", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "Calendar", p.CalendarEntity?.Name ?? "Standard" },
            new[] { "Working days", string.Join(" ", c.Days.Select(d => names[d])) },
            new[] { "Day", $"{c.DayStart}, {c.Hours} h" },
            new[] { "Holidays", c.Holidays == WorkCalendar.Romania ? "Romania's legal holidays" : "none" },
            new[] { "Winter break (wet tasks)", c.Winter ? $"{c.WinterFrom} to {c.WinterTo}" : "off" },
            new[] { "", "" },
            new[] { "Not worked", "Why" },
        };
        var finish = Solve(p).Result.Finish;
        foreach (var (day, why) in c.NonWorking(new DateOnly(p.Start.Year, 1, 1), new DateOnly(finish.Year + 1, 12, 31))) rows.Add([D(day), why]);
        return rows;
    }
}
