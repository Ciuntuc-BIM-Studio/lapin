using System.Text;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// Planning → Generate schedule… (docs/plan-4d5d.md, stage 3): the configurator's engine side — the templates and the
/// project's styles, the preview of a spec (the schedule it would make, worked out on a draft of the model), applying it as
/// one undo step, and the productivity norms (imported from the user's CSV or XLSX, or Lapin's indicative template).
/// </summary>
public sealed partial class PlanningModule
{
    /// <summary>The spec a command or query names: a style's (a template or the project's own), else the last one generated, with the fields given over it.</summary>
    static GenSpec SpecOf(IModelView m, JsonObject a)
    {
        var baseSpec = (string?)a["style"] is { Length: > 0 } style
            ? Generator.StyleSpec(m, style) ?? throw new CommandException($"There is no schedule style „{style}”.")
            : Generator.Current(m);
        return a["spec"] is JsonObject o ? GenSpec.Read(o, baseSpec) : baseSpec;
    }

    static bool Keep(JsonObject a) => a["keep"] is not JsonValue v || !v.TryGetValue<bool>(out var b) || b;

    sealed class CommandQuery(ICommandContext ctx) : IQueryContext
    {
        public IModelView Model => ctx.Model;
        public IServices Services => ctx.Services;
        public Lapin.Platform.ElementGeometry? Geometry(string entityId) => ctx.Query.Geometry(entityId);
        public IReadOnlyList<QuantitySet> Quantities(string id) => ctx.Query.Quantities(id);
        public ClassDescriptor? Describe(string ifcClass) => ctx.Describe(ifcClass);
    }

    static void RegisterGenerator(IModuleRegistry r)
    {
        // The configurator's choices: the template the model asks for, the spec it opens on, the project's styles, the norms.
        r.Query("generatorOptions", (q, _) =>
        {
            var m = q.Model;
            var norms = Norms.Of(m);
            var schedule = Plan.ScheduleOf(m);
            return new JsonObject
            {
                ["suggested"] = Generator.Suggest(m),
                ["spec"] = Generator.Current(m).ToJson(false),
                ["generated"] = schedule?.Attrs["generator"] != null,
                ["start"] = WorkCalendar.Date(Plan.Read(m).Start),
                ["tasks"] = m.OfClass(Plan.TaskClass).Count(),
                ["styles"] = new JsonArray(Generator.OwnStyles(m).Select(s => (JsonNode)s.DeepClone()).ToArray()),
                ["norms"] = NormsJson(norms),
            };
        });

        // The schedule a spec would make, worked out on a draft of the model: its tasks with their dates, and the summary.
        r.Query("preview", (q, a) => Preview(q, SpecOf(q.Model, a), Keep(a)));

        // The schedule made from a spec: one undo step; generated again, what was edited by hand stays.
        r.Command("generate", "Generate schedule", (ctx, a) =>
        {
            var spec = SpecOf(ctx.Model, a);
            if (a.ContainsKey("start")) spec = spec with { Start = DateArg(a["start"], "start") };
            var o = Generator.Build(new CommandQuery(ctx), spec, Keep(a), ctx.NewId);
            Generator.Apply(ctx, o);
            return new JsonObject
            {
                ["tasks"] = o.Tasks.Count, ["made"] = o.Made.Count, ["removed"] = o.Removed.Count,
                ["kept"] = o.Tasks.Count(t => t.Bool("own") || t.Str("gen") == null),
            };
        });

        r.Command("saveScheduleStyle", "Save schedule style", (ctx, a) =>
            new JsonObject { ["styleId"] = Generator.SaveStyle(ctx, SpecOf(ctx.Model, a), (string?)a["name"] ?? "", (string?)a["styleId"]) });
        r.Command("renameScheduleStyle", "Rename schedule style", (ctx, a) => { Generator.RenameStyle(ctx, (string?)a["styleId"] ?? "", (string?)a["name"] ?? ""); return null; });
        r.Command("deleteScheduleStyle", "Delete schedule style", (ctx, a) => { Generator.DeleteStyle(ctx, (string?)a["styleId"] ?? ""); return null; });

        // The user's norms, a CSV or a workbook (its bytes in base64, or CSV text), in place of the project's table.
        r.Command("importNorms", "Import norms", (ctx, a) =>
        {
            var bytes = (string?)a["data"] is { } b64 ? Convert.FromBase64String(b64)
                : (string?)a["csv"] is { } csv ? Encoding.UTF8.GetBytes(csv) : throw new CommandException("No table of norms to read.");
            var res = Norms.Import(bytes);
            Norms.Put(ctx, res.Rows, ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : "Imported norms", false);
            return new JsonObject { ["rows"] = res.Rows.Count, ["skipped"] = res.Skipped, ["notes"] = new JsonArray(res.Notes.Select(x => (JsonNode)x).ToArray()) };
        });

        // Lapin's indicative values as the project's table, marked so everywhere it shows.
        r.Command("useIndicativeNorms", "Indicative norms", (ctx, _) =>
        {
            Norms.Put(ctx, Norms.Indicative, "Lapin's indicative values", true);
            return new JsonObject { ["rows"] = Norms.Indicative.Count };
        });

        r.Command("clearNorms", "Remove norms", (ctx, _) =>
        {
            if (ctx.Model.OfClass(Norms.Class).FirstOrDefault() is not { } e) throw new CommandException("The project has no norms.");
            ctx.Remove(e.Id);
            return null;
        });

        // The template of a norms table: its columns and Lapin's indicative rows, as CSV in base64.
        r.Query("normTemplate", (_, _) => Convert.ToBase64String(Encoding.UTF8.GetBytes("﻿" + Norms.TemplateCsv())));
    }

    static JsonObject NormsJson(Norms.Table t) => new()
    {
        ["count"] = t.Rows.Count, ["source"] = t.Source, ["indicative"] = t.IsIndicative,
        ["rows"] = new JsonArray(t.Rows.Select(n => (JsonNode)n.ToJson()).ToArray()),
    };

    /// <summary>The schedule a spec would make, as the configurator previews it.</summary>
    public static JsonObject Preview(IQueryContext q, GenSpec spec, bool keep)
    {
        var n = 0;
        var o = Generator.Build(q, spec, keep, () => $"draft-{++n}");
        var draft = new ScheduleDraft(q.Model, [o.Schedule, .. o.Tasks], o.Links);
        var p = Plan.Read(draft);
        var (r, problem) = Solve(p);
        var links = TaskElements.Resolve(draft, q.Services, q.Describe, p);
        var (none, partial) = links.Unlinked();
        var made = o.Made.ToDictionary(x => x.Id);
        var rows = new JsonArray();
        foreach (var t in p.Rows)
        {
            var tm = r.Times[t.Id];
            var e = draft.Get(t.Id)!;
            var x = new JsonObject
            {
                ["id"] = t.Id, ["name"] = t.Name, ["level"] = p.Level[t.Id], ["summary"] = p.IsSummary(t.Id),
                ["start"] = D(tm.Start), ["finish"] = D(tm.Finish), ["duration"] = p.IsSummary(t.Id) ? tm.Duration : t.Duration,
                ["critical"] = tm.Critical, ["count"] = links.Under(t.Id).Count,
            };
            if (e.Str("durationFrom") is { } src) x["source"] = src;
            if (e.NumOrNull("quantity") is { } qv) { x["quantity"] = qv; x["unit"] = e.Str("unit"); }
            if (e.Str("norm") is { } norm) x["norm"] = norm;
            if (e.NumOrNull("crew") is { } crew) x["crew"] = (int)Math.Round(crew);
            // Kept as it was: edited by hand (own), or a task made by hand.
            if (!made.ContainsKey(t.Id) || e.Bool("own")) x["kept"] = true;
            rows.Add((JsonNode)x);
        }
        var leaves = o.Made.Where(x => !x.Summary).ToList();
        var lives = TaskLives.Of(p, r, links, q.Services, draft);
        var finish = r.Times.Count > 0 ? r.Times.Values.Max(t => t.Finish) : p.Start;
        var norms = Norms.Of(q.Model);
        return new JsonObject
        {
            ["spec"] = o.Spec.ToJson(false),
            ["tasks"] = rows,
            ["problem"] = problem,
            ["summary"] = new JsonObject
            {
                ["tasks"] = p.Rows.Count,
                ["start"] = D(p.Calendar.NextWorking(p.Start)), ["finish"] = D(finish),
                ["workingDays"] = r.Times.Count > 0 ? p.Calendar.WorkingDays(p.Calendar.NextWorking(p.Start), r.Finish) : 0,
                ["total"] = links.Candidates.Count(links.NeedsTask),
                ["linked"] = links.Candidates.Count(f => links.NeedsTask(f) && links.Covered.ContainsKey(f.Element.Id)),
                ["none"] = new JsonArray(none.Select(f => (JsonNode)f.Element.Id).ToArray()),
                ["partial"] = new JsonArray(partial.Select(f => (JsonNode)f.Element.Element.Id).ToArray()),
                ["unlinkedClasses"] = new JsonArray(none.Select(f => f.Element.Class).Distinct().Select(c => (JsonNode)c).ToArray()),
                ["fromNorms"] = leaves.Count(x => x.Source == "norm"),
                ["byDefault"] = leaves.Count(x => x.Source == "default" && x.Step.Classes.Count > 0),
                ["noNorm"] = new JsonArray(leaves.Where(x => x.Source == "default" && x.Step.Classes.Count > 0).Select(x => x.Step.Name).Distinct().Select(s => (JsonNode)s).ToArray()),
                ["indicative"] = norms.IsIndicative && leaves.Any(x => x.Source == "norm"),
                ["formwork"] = lives.Count(x => x.Kind == "formwork"),
                ["scaffolds"] = lives.Count(x => x.Kind == "scaffold"),
                ["demolished"] = links.Candidates.Count(f => f.Demolished != null),
                ["kept"] = o.Tasks.Count(t => t.Bool("own") || t.Str("gen") == null),
                ["removed"] = o.Removed.Count,
            },
        };
    }
}
