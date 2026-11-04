using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing.Export;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// Progress (docs/plan-4d5d.md, stage 5): the status date (the day progress is reported through; the work left starts the
/// working day after it), each task's actual start and finish and its percentage, the elements marked started or done on
/// site (<c>planning.markDone</c>, <c>planning.markStarted</c>, <c>planning.unmark</c>: the commands a guest of a shared
/// session may run even when they joined to look), rolling up to their tasks' percentages; the baseline (one, in the open
/// module: several and scenarios are planning-pro's), saved from the dates as they are, with the variances; the progress
/// read back from the workbook the schedule was exported to. Every command one undo step.
/// </summary>
public sealed partial class PlanningModule
{
    /// <summary>
    /// The project's schedule read with its tasks' elements and their marks: each leaf's progress from its own fields and its
    /// elements (weighed in the task's unit when it has one, else counted), the summaries from their leaves.
    /// </summary>
    public static Plan ReadResolved(IQueryContext q, out TaskElements links)
    {
        var p = Plan.Read(q.Model);
        links = TaskElements.Resolve(q, p);
        ApplyProgress(q.Model, q.Quantities, p, links);
        return p;
    }

    public static Plan ReadResolved(IQueryContext q) => ReadResolved(q, out _);

    /// <summary>The leaves' progress from their elements' marks (the summaries rolled up).</summary>
    public static void ApplyProgress(IModelView m, Func<string, IReadOnlyList<QuantitySet>>? quantities, Plan p, TaskElements links)
    {
        if (!p.Rows.Any(t => t.Marks.Count > 0)) return;
        var sets = new Dictionary<string, IReadOnlyList<QuantitySet>>();
        var bases = new Dictionary<string, (string Basis, double Factor)?>();
        double Measure(PlanTask t, string id, ulong mask)
        {
            if (!bases.TryGetValue(t.Id, out var basis)) bases[t.Id] = basis = m.Get(t.Id)?.Str("unit") is { } u ? Norms.BasisOf(u) : null;
            if (quantities == null || basis is not { } b || b.Basis == "count" || links.Facts(id) is not { } f) return 1;
            if (!sets.TryGetValue(id, out var s)) sets[id] = s = quantities(id);
            return Generator.MeasureOf(s, f, mask, b.Basis);
        }
        p.ApplyElements(id => links.ByTask.TryGetValue(id, out var own) ? own : null, Measure);
    }

    /// <summary>The day a mark is put on: the one given, else the status date, else today.</summary>
    static DateOnly MarkDay(JsonObject a, Plan p) => a.ContainsKey("date") && (string?)a["date"] is { Length: > 0 } ? DateArg(a["date"], "date") : p.DataDate ?? Plan.Today();

    /// <summary>
    /// Elements marked (state done, started or none) in the tasks that take them (only <c>task</c> when given; the leaves:
    /// a summary's own elements are its tasks'). Returns how many marks changed and in how many tasks.
    /// </summary>
    static JsonObject Mark(ICommandContext ctx, JsonObject a, string state)
    {
        var (ids, _) = Groups.Expand(ctx.Model, IdList(a["ids"]));
        var p = Plan.Read(ctx.Model);
        var links = TaskElements.Resolve(ctx, p);
        var day = MarkDay(a, p);
        var only = (string?)a["task"] is { Length: > 0 } tid ? RequireTask(ctx.Model, tid).Id : null;
        var elements = ids.Where(id => links.Facts(id) != null).ToList();
        if (elements.Count == 0) throw new CommandException("Select elements of the model: in 3D or in a plan.");
        int changed = 0, inTasks = 0, none = 0;
        var touched = new Dictionary<string, Dictionary<string, ElementMark>>();
        foreach (var id in elements)
        {
            var tasks = links.TasksOf(id).Where(t => !p.IsSummary(t) && (only == null || t == only)).ToList();
            if (tasks.Count == 0) { none++; continue; }
            foreach (var t in tasks)
            {
                var marks = touched.TryGetValue(t, out var m) ? m : touched[t] = p.ById[t].Marks.ToDictionary(kv => kv.Key, kv => kv.Value);
                var was = marks.GetValueOrDefault(id);
                ElementMark? now = state switch
                {
                    "done" => new ElementMark(was?.Started is { } s && s <= day ? s : day, day),
                    "started" => new ElementMark(was?.Started is { } s && s <= day ? s : day, null),
                    _ => null,
                };
                if (now == was) continue;
                if (now == null) marks.Remove(id); else marks[id] = now;
                changed++;
            }
        }
        foreach (var (t, marks) in touched)
        {
            var e = ctx.Model.Get(t)!;
            var attrs = e.CloneAttrs();
            if (marks.Count == 0) attrs.Remove("marks");
            else attrs["marks"] = new JsonObject(marks.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value.ToJson())));
            if (!JsonNode.DeepEquals(attrs["marks"], e.Attrs["marks"])) { ctx.Replace(e.With(attrs: attrs)); inTasks++; }
        }
        if (none == elements.Count) throw new CommandException(only != null ? "None of the elements selected is in that task." : "None of the elements selected is in a task: add them to one first (Planning → Add to task…).");
        return new JsonObject { ["changed"] = changed, ["tasks"] = inTasks, ["none"] = none, ["date"] = D(day) };
    }

    static void RegisterProgress(IModuleRegistry r)
    {
        // Elements done on a day (the status date unless one is given): in each task that takes them (or the one named).
        r.Command("markDone", "Mark done", (ctx, a) => Mark(ctx, a, "done"));
        // Elements started (their done day taken off).
        r.Command("markStarted", "Mark started", (ctx, a) => Mark(ctx, a, "started"));
        // Elements back to not started.
        r.Command("unmark", "Mark not started", (ctx, a) => Mark(ctx, a, "none"));

        // A task's progress typed: its percentage (null: from its elements, or its dates), its actual start and finish (null or "": none).
        r.Command("setProgress", "Progress", (ctx, a) =>
        {
            var e = RequireTask(ctx.Model, (string?)a["id"]);
            var p = Plan.Read(ctx.Model);
            if (p.IsSummary(e.Id)) throw new CommandException("A summary task takes its progress from the tasks under it.");
            var attrs = e.CloneAttrs();
            if (a.ContainsKey("complete"))
            {
                if (a["complete"] is null || (a["complete"] is JsonValue cv && cv.TryGetValue<string>(out var cs) && cs.Trim().Length == 0)) attrs.Remove("complete");
                else if (Json.Number(a["complete"]) is { } c && double.IsFinite(c) && c >= 0 && c <= 100) attrs["complete"] = Math.Round(c, 2);
                else throw new CommandException("A task's progress is a percentage, from 0 to 100.");
            }
            foreach (var key in new[] { "actualStart", "actualFinish" })
                if (a.ContainsKey(key))
                {
                    if ((string?)a[key] is { Length: > 0 }) attrs[key] = D(DateArg(a[key], key == "actualStart" ? "actual start" : "actual finish"));
                    else attrs.Remove(key);
                }
            if (WorkCalendar.ParseDate((string?)attrs["actualStart"]) is { } s && WorkCalendar.ParseDate((string?)attrs["actualFinish"]) is { } f && f < s)
                throw new CommandException("A task finishes on or after the day it started.");
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // The baseline: every task's dates and duration as the schedule has them now (the one baseline: saved again, it is replaced).
        r.Command("saveBaseline", "Save baseline", (ctx, a) =>
        {
            var (s, _) = EnsureSchedule(ctx);
            var p = ReadResolved(ctx.Query);
            if (p.Rows.Count == 0) throw new CommandException("The schedule has no tasks to keep as a baseline.");
            var (res, problem) = Solve(p);
            if (problem != null) throw new CommandException(problem);
            foreach (var t in p.Rows)
            {
                var tm = res.Times[t.Id];
                var e = ctx.Model.Get(t.Id)!;
                var attrs = e.CloneAttrs();
                var dur = p.IsSummary(t.Id) ? tm.Duration : t.Milestone ? 0 : t.Duration;
                attrs["baseline"] = new TaskBaseline(tm.Start, tm.Finish, dur).ToJson();
                ctx.Replace(e.With(attrs: attrs));
            }
            var sa = ctx.Model.Get(s.Id)!.CloneAttrs();
            sa["baselineSaved"] = D(a.ContainsKey("date") ? DateArg(a["date"], "date") : Plan.Today());
            ctx.Replace(ctx.Model.Get(s.Id)!.With(attrs: sa));
            return new JsonObject { ["tasks"] = p.Rows.Count };
        });

        r.Command("clearBaseline", "Clear baseline", (ctx, _) =>
        {
            var (s, _) = EnsureSchedule(ctx);
            foreach (var t in ctx.Model.OfClass(Plan.TaskClass).Where(t => t.Attrs["baseline"] != null).ToList())
            {
                var attrs = t.CloneAttrs();
                attrs.Remove("baseline");
                ctx.Replace(t.With(attrs: attrs));
            }
            var sa = ctx.Model.Get(s.Id)!.CloneAttrs();
            sa.Remove("baselineSaved");
            ctx.Replace(ctx.Model.Get(s.Id)!.With(attrs: sa));
            return null;
        });

        // The progress read back from a workbook (or CSV) of the schedule, as Export XLSX writes it and the site fills it in.
        r.Command("importProgress", "Import progress", (ctx, a) =>
        {
            var bytes = (string?)a["data"] is { } b64 ? Convert.FromBase64String(b64) : throw new CommandException("No workbook to read.");
            return ImportProgress(ctx, bytes).ToJson();
        });
    }

    /// <summary>What reading progress from a workbook did.</summary>
    public sealed record ProgressImport(int Rows, int Changed, int Unknown, DateOnly? DataDate)
    {
        public JsonObject ToJson() => new() { ["rows"] = Rows, ["changed"] = Changed, ["unknown"] = Unknown, ["dataDate"] = DataDate is { } d ? D(d) : null };
    }

    static readonly string[] IdHeads = ["globalid", "guid"], RowHeads = ["id", "nr", "nr."], WbsHeads = ["wbs"],
        CompleteHeads = ["% complete", "% done", "complete", "% realizat", "realizat %", "progres", "progress"],
        ActualStartHeads = ["actual start", "start real", "început real", "inceput real"], ActualFinishHeads = ["actual finish", "final real", "sfârșit real", "sfarsit real"],
        StatusHeads = ["status date", "data de stare"];

    /// <summary>
    /// A cell's date: ISO (2027-03-15), day first (15.03.2027, 15/03/27), or a spreadsheet's day number (Excel's, from
    /// 30 December 1899); null when empty or not a date.
    /// </summary>
    public static DateOnly? CellDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (WorkCalendar.ParseDate(s.Length >= 10 ? s[..10] : s) is { } iso) return iso;
        if (DateOnly.TryParseExact(s, ["dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy", "dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && n > 20000 && n < 80000) return new DateOnly(1899, 12, 30).AddDays((int)Math.Floor(n));
        return null;
    }

    static ProgressImport ImportProgress(ICommandContext ctx, byte[] bytes)
    {
        List<List<string>> table;
        if (XlsxReader.IsZip(bytes))
        {
            try { table = XlsxReader.Read(bytes); }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException) { throw new CommandException("The workbook could not be read: " + ex.Message); }
        }
        else table = Norms.Csv(Encoding.UTF8.GetString(bytes).TrimStart('﻿'));
        static string Head(string s) => s.Trim().Trim('"').ToLowerInvariant();
        var at = table.FindIndex(r => r.Select(Head).Any(c => CompleteHeads.Contains(c) || ActualStartHeads.Contains(c) || ActualFinishHeads.Contains(c))
            && r.Select(Head).Any(c => IdHeads.Contains(c) || RowHeads.Contains(c) || WbsHeads.Contains(c)));
        if (at < 0) throw new CommandException("The table has no headings Lapin knows: it needs a task's ID, WBS or GlobalId and a % complete, actual start or actual finish column (as Export XLSX writes them).");
        var heads = table[at].Select(Head).ToList();
        int Col(string[] names) => heads.FindIndex(names.Contains);
        int gid = Col(IdHeads), row = Col(RowHeads), wbs = Col(WbsHeads), pc = Col(CompleteHeads), st = Col(ActualStartHeads), fi = Col(ActualFinishHeads);
        string Cell(List<string> r, int i) => i >= 0 && i < r.Count ? r[i].Trim() : "";
        var p = ReadResolved(ctx.Query);
        var byRow = p.RowOf.ToDictionary(kv => kv.Value.ToString(CultureInfo.InvariantCulture), kv => kv.Key);
        var byWbs = p.Wbs.ToDictionary(kv => kv.Value, kv => kv.Key);
        int rows = 0, changed = 0, unknown = 0;
        // A status date written above the table (Export XLSX writes none; a site's sheet may).
        DateOnly? dataDate = null;
        foreach (var r in table.Take(at))
            for (var i = 0; i + 1 < r.Count; i++)
                if (StatusHeads.Contains(Head(r[i])) && CellDate(r[i + 1]) is { } dd) dataDate = dd;
        foreach (var r in table.Skip(at + 1))
        {
            if (r.All(string.IsNullOrWhiteSpace)) continue;
            rows++;
            string? id = Cell(r, gid) is { Length: > 0 } g && p.ById.ContainsKey(g) ? g
                : Cell(r, wbs) is { Length: > 0 } w && byWbs.TryGetValue(w, out var bw) ? bw
                : Cell(r, row) is { Length: > 0 } n && byRow.TryGetValue(n, out var br) ? br : null;
            if (id == null) { unknown++; continue; }
            if (p.IsSummary(id)) continue;
            var e = ctx.Model.Get(id)!;
            var attrs = e.CloneAttrs();
            if (pc >= 0 && Cell(r, pc).TrimEnd('%').Trim() is { Length: > 0 } text)
            {
                var v = XlsxReader.Number(text);
                // What the workbook says, kept only when it is not what the task has already (its elements' or its dates', rounded as written).
                if (v is { } x && double.IsFinite(x) && x >= 0 && x <= 100 && Math.Abs(x - p.ProgressOf(id).Complete) > 0.051) attrs["complete"] = Math.Round(x, 2);
            }
            if (st >= 0 && CellDate(Cell(r, st)) is { } s) attrs["actualStart"] = D(s);
            if (fi >= 0 && CellDate(Cell(r, fi)) is { } f) attrs["actualFinish"] = D(f);
            if (WorkCalendar.ParseDate((string?)attrs["actualStart"]) is { } a0 && WorkCalendar.ParseDate((string?)attrs["actualFinish"]) is { } f0 && f0 < a0) attrs.Remove("actualFinish");
            if (JsonNode.DeepEquals(attrs, e.Attrs)) continue;
            ctx.Replace(e.With(attrs: attrs));
            changed++;
        }
        if (dataDate is { } date)
        {
            var (s, _) = EnsureSchedule(ctx);
            var sa = s.CloneAttrs();
            sa["dataDate"] = D(date);
            ctx.Replace(s.With(attrs: sa));
        }
        return new ProgressImport(rows, changed, unknown, dataDate);
    }
}
