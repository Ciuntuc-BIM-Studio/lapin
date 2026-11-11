using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// The 4D simulation (docs/plan-4d5d.md, stage 4): the timeline the interface plays (<c>planning.timeline</c>), the states at
/// a date (<c>planning.stateAt</c>), the simulation's colours (<c>planning.setSimulation</c>), a view's State at date
/// (<c>planning.viewDate</c>, drawn by <see cref="StateAtDate"/>), the Gantt on sheets (<c>planning.ganttSheet</c>,
/// <see cref="GanttSheet"/>) and the stages sheet (<c>planning.stagesSheet</c>, <see cref="StagesSheet"/>). Each command one undo step.
/// </summary>
public sealed partial class PlanningModule
{
    static void RegisterSimulation(IModuleRegistry r)
    {
        r.Class(new(StagesSheet.Class, "Stages", [], "#5b7a99", Kind: "resource", Template: false));
        var filter = new StateAtDate();
        r.Service<IViewElementFilter>(filter);
        r.Service<IViewPartFilter>(filter);
        r.Service<ISheetContent>(new GanttSheet());
        r.Service<ISheetContent>(new StagesSheet());

        // Every element's days on the schedule, worked out once a change: the interface shows the state at any date from it.
        r.Query("timeline", (q, _) => Timeline.Of(q).ToJson());

        // Each element's state on a day (those the schedule knows), and the tasks under way that day.
        r.Query("stateAt", (q, a) =>
        {
            var d = DateArg(a["date"], "date");
            var t = Timeline.Of(q);
            var states = new JsonObject();
            foreach (var e in t.Elements.Values)
            {
                var o = new JsonObject { ["state"] = Timeline.StateOf(e, d) };
                // Against the baseline (stage 5): late or ahead, as the simulation shows it.
                if (t.Shown(e, d) is var shown && shown != (string)o["state"]!) o["shown"] = shown;
                if (e.Layers != null)
                    o["layers"] = new JsonArray(Enumerable.Range(0, t.LayerCounts.GetValueOrDefault(e.Id)).Select(i => (JsonNode)Timeline.LayerStateOf(e, i, d)).ToArray());
                states[e.Id] = o;
            }
            return new JsonObject
            {
                ["date"] = D(d), ["elements"] = states,
                ["tasks"] = new JsonArray(t.TasksOn(d).Select(x => (JsonNode)x.Id).ToArray()),
            };
        });

        // The simulation's look: the colours of what is under way, demolished and temporary and of the ghosts; what is not
        // started hidden or a ghost; what no task takes shown or hidden; layers built apart shown apart.
        r.Command("setSimulation", "Simulation settings", (ctx, a) =>
        {
            var (s, _) = EnsureSchedule(ctx);
            var o = SimulationLook.Read(s.Attrs["simulation"]).ToJson();
            foreach (var key in new[] { "active", "demolishing", "temporary", "planned", "late", "ahead" })
                if (a.ContainsKey(key))
                {
                    if ((string?)a[key] is not { } c || !System.Text.RegularExpressions.Regex.IsMatch(c, "^#[0-9a-fA-F]{6}$")) throw new CommandException("A colour is #rrggbb.");
                    o[key] = c.ToLowerInvariant();
                }
            if (a.ContainsKey("plannedMode")) o["plannedMode"] = (string?)a["plannedMode"] is "hide" or "ghost" ? (string?)a["plannedMode"] : throw new CommandException("What is not started is hidden or a ghost.");
            if (a.ContainsKey("unlinked")) o["unlinked"] = (string?)a["unlinked"] is "show" or "hide" ? (string?)a["unlinked"] : throw new CommandException("What no task takes is shown or hidden.");
            if (a.ContainsKey("layers")) o["layers"] = a["layers"] is JsonValue lv && lv.TryGetValue<bool>(out var lb) && lb;
            if (a.ContainsKey("compare")) o["compare"] = a["compare"] is JsonValue cv && cv.TryGetValue<bool>(out var cb) && cb;
            if (a["reset"] is JsonValue rv && rv.TryGetValue<bool>(out var reset) && reset) o = SimulationLook.Default.ToJson();
            var attrs = s.CloneAttrs();
            attrs["simulation"] = SimulationLook.Read(o).ToJson();
            ctx.Replace(s.With(attrs: attrs));
            return null;
        });

        // A view drawn as the schedule has the model on a day (null: as it is); a plan, a section, an elevation or a 3D view.
        r.Command("viewDate", "State at date", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Views.Class } x ? x : throw new CommandException("Not a view: State at date is a view's.");
            var attrs = v.CloneAttrs();
            if ((string?)a["date"] is { Length: > 0 } date) attrs["stateDate"] = D(DateArg(date, "date"));
            else attrs.Remove("stateDate");
            ctx.Replace(v.With(attrs: attrs));
            return null;
        });

        // The Gantt on sheets: a new sheet a page (A3 landscape unless asked: an execution chart is read at arm's length), the schedule as the sheet's content.
        r.Command("ganttSheet", "Gantt on sheet", (ctx, a) =>
        {
            var (s, _) = EnsureSchedule(ctx);
            if (Plan.Read(ctx.Model).Rows.Count == 0) throw new CommandException("The schedule has no tasks to put on a sheet.");
            var paper = (string?)a["paper"] ?? "A3";
            var portrait = (string?)a["orientation"] == "portrait";
            var (fw, fh) = Frame(paper, portrait);
            var pages = new GanttSheet().Pages(ctx.Query, ctx.Model.Get(s.Id)!, fw, fh);
            var ids = new JsonArray();
            for (var i = 0; i < pages.Count; i++)
            {
                var (w, h, _) = pages[i];
                var name = pages.Count == 1 ? s.Name : $"{s.Name} ({i + 1}/{pages.Count})";
                var sheet = Sheets.AddSheet(ctx, name, paper, portrait, [(s.Id, Sheets.Margin + 4 + w / 2, Sheets.Margin + 4 + h / 2, i + 1)]);
                ids.Add((JsonNode)sheet.Id);
            }
            return new JsonObject { ["ids"] = ids };
        });

        // A stages sheet: a view at each date (the engine draws it), or the 3D's pictures at those dates (taken by the
        // interface: PNG or JPEG, base64), each labelled with its date and the tasks under way.
        r.Command("stagesSheet", "Stages sheet", (ctx, a) =>
        {
            var frames = new JsonArray();
            var viewId = (string?)a["viewId"];
            if (a["snapshots"] is JsonArray shots && shots.Count > 0)
            {
                foreach (var shot in shots.OfType<JsonObject>())
                {
                    var date = DateArg(shot["date"], "stage's date");
                    byte[] bytes;
                    try { bytes = Convert.FromBase64String((string?)shot["data"] ?? ""); }
                    catch (FormatException) { throw new CommandException("A stage's picture is not base64."); }
                    var mime = Images.Sniff(bytes) ?? throw new CommandException("A stage's picture is a PNG or a JPEG.");
                    if (bytes.Length > Images.MaxBytes) throw new CommandException("A stage's picture is at most 8 MB.");
                    var image = new Entity(ctx.NewId(), Images.Class, $"Stage {D(date)}", new JsonObject { ["mime"] = mime, ["data"] = Convert.ToBase64String(bytes) });
                    ctx.Add(image);
                    frames.Add((JsonNode)new JsonObject { ["date"] = D(date), ["image"] = image.Id });
                }
                viewId = null;
            }
            else
            {
                if (viewId == null || Views.Find(ctx.Model, viewId) is not { } view || view.Str("kind") is "3d") throw new CommandException("A stages sheet draws a plan, a section or an elevation at each date, or takes the 3D's pictures.");
                var dates = (a["dates"] as JsonArray)?.Select(x => DateArg(x, "stage's date")).Distinct().Order().ToList() ?? [];
                if (dates.Count == 0) dates = Timeline.Of(ctx.Query).StageDates((string?)a["mode"] ?? "chapters");
                foreach (var d in dates) frames.Add((JsonNode)new JsonObject { ["date"] = D(d) });
            }
            if (frames.Count == 0) throw new CommandException("A stages sheet needs a date at least.");
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : "Stages";
            var attrs = new JsonObject { ["frames"] = frames };
            if (viewId != null) attrs["viewId"] = viewId;
            if (Json.Number(a["columns"]) is { } cols && cols >= 1) attrs["columns"] = (int)cols;
            var stages = new Entity(ctx.NewId(), StagesSheet.Class, name, attrs);
            ctx.Add(stages);
            var paper = (string?)a["paper"] ?? "A1";
            var portrait = (string?)a["orientation"] == "portrait";
            var (fw, fh) = Frame(paper, portrait);
            var (w, h, _) = new StagesSheet().Pages(ctx.Query, stages, fw, fh)[0];
            var sheet = Sheets.AddSheet(ctx, name, paper, portrait, [(stages.Id, Sheets.Margin + 4 + w / 2, Sheets.Margin + 4 + h / 2, 1)]);
            return new JsonObject { ["sheetId"] = sheet.Id, ["id"] = stages.Id };
        });
    }

    /// <summary>A sheet's frame inside its margins and over its title block (what a content pages into), by its paper.</summary>
    static (double W, double H) Frame(string paper, bool portrait)
    {
        if (!Sheets.Papers.TryGetValue(paper, out var size)) throw new CommandException("A sheet is A0, A1, A2, A3 or A4.");
        var (w, h) = portrait ? (size.H, size.W) : (size.W, size.H);
        return (w - 2 * Sheets.Margin - 4, h - 2 * Sheets.Margin - Sheets.TitleBlock.H - 4);
    }
}
