using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// Product × Location (docs/plan-4d5d.md, "Produs × Loc"): the project's PBS chosen among its classifications (or Lapin's
/// indicative starter made), its items counted on the model's elements, a rule's elements and quantity previewed while it is
/// picked, and tasks made from the cross-product — an activity, a product and a list of locations, one task a location under
/// a summary, linked finish-to-start in the order of the locations, as the generator links a step's storeys.
/// </summary>
public sealed partial class PlanningModule
{
    static void RegisterBreakdowns(IModuleRegistry r)
    {
        // The PBS and what it labels: {system: {id, name, source} | null, systems: [{id, name}], items: [{code, title, parent, depth, count}], unlabelled}.
        r.Query("pbs", (q, _) => PbsJson(q));

        // The classification that is the project's PBS ({system: id | name}; null or "" forgets the choice).
        r.Command("setPbs", "Product breakdown", (ctx, a) =>
        {
            var p = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = p.CloneAttrs();
            if (((string?)a["system"])?.Trim() is { Length: > 0 } key)
                attrs[Breakdowns.PbsKey] = (Classifications.System(ctx.Model, key) ?? throw new CommandException($"The project has no classification „{key}”.")).Id;
            else attrs.Remove(Breakdowns.PbsKey);
            ctx.Replace(p.With(attrs: attrs));
            return null;
        });

        // Lapin's starter PBS made (a classification „PBS”, marked indicative) and chosen as the project's.
        r.Command("pbsStarter", "Starter product breakdown", (ctx, a) =>
        {
            if (Classifications.System(ctx.Model, Breakdowns.StarterName) != null) throw new CommandException($"There is a classification „{Breakdowns.StarterName}” already.");
            var items = new JsonArray(Breakdowns.Starter.Select(i =>
            {
                var o = new JsonObject { ["code"] = i.Code, ["title"] = i.Title };
                if (i.Parent != null) o["parent"] = i.Parent;
                return (JsonNode)o;
            }).ToArray());
            var system = new Entity(ctx.NewId(), Classifications.Class, Breakdowns.StarterName, new JsonObject
            {
                ["items"] = items, ["source"] = "Lapin (indicative)", ["description"] = "A starter product breakdown, indicative: change it, or import your own.",
            });
            ctx.Add(system);
            var p = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = p.CloneAttrs();
            attrs[Breakdowns.PbsKey] = system.Id;
            ctx.Replace(p.With(attrs: attrs));
            return new JsonObject { ["id"] = system.Id };
        });

        // What a rule would take now, for its pickers: {rule, part?, norm?} → {count, quantity?, unit?} (the quantity in the norm's unit, the generator's measure).
        r.Query("rulePreview", (q, a) =>
        {
            var rule = a["rule"] as JsonObject ?? new JsonObject();
            TaskRule.Check(rule);
            var part = TaskParts.Parse(a["part"]);
            var links = TaskElements.Resolve(q);
            var els = links.Of(Probe(rule, part));
            var o = new JsonObject { ["count"] = els.Count };
            if (Norms.Of(q.Model).Find((string?)a["norm"]) is { } norm && Measure(q, links, els, norm) is { } qty) { o["quantity"] = Math.Round(qty, 3); o["unit"] = norm.Unit; }
            return o;
        });

        // Tasks from the cross-product (activity × product × locations): {name, products, locations, part?, norm?, crew?, days?, kind?, link?, parent?}
        // → {summary, tasks}. One undo step.
        r.Command("tasksFromBreakdown", "Tasks from product × location", (ctx, a) => FromBreakdown(ctx, a));
    }

    static PlanTask Probe(JsonObject rule, string? part) =>
        new("probe", "", null, 0, 1, false, null, null, false, null, null, TaskRule.Read(rule), part);

    /// <summary>The elements' quantity in a norm's unit, by the generator's measure (null: Lapin does not measure in it, or nothing is there).</summary>
    static double? Measure(IQueryContext q, TaskElements links, Dictionary<string, ulong> els, Norm norm)
    {
        if (els.Count == 0 || Norms.BasisOf(norm.Unit) is not { } b) return null;
        double sum = 0;
        foreach (var (id, mask) in els) if (links.Facts(id) is { } f) sum += Generator.MeasureOf(q.Quantities(id), f, mask, b.Basis);
        var qty = sum * b.Factor;
        return qty > 1e-9 ? qty : null;
    }

    static JsonObject PbsJson(IQueryContext q)
    {
        var m = q.Model;
        var system = Breakdowns.Pbs(m);
        var o = new JsonObject
        {
            ["system"] = system == null ? null : new JsonObject { ["id"] = system.Id, ["name"] = system.Name, ["source"] = system.Str("source") },
            ["chosen"] = Spatial.Project(m)?.Str(Breakdowns.PbsKey) != null,
            ["systems"] = new JsonArray(Classifications.Systems(m).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Select(s => (JsonNode)new JsonObject { ["id"] = s.Id, ["name"] = s.Name }).ToArray()),
        };
        if (system != null)
        {
            var links = TaskElements.Resolve(q);
            var (items, none) = Breakdowns.Counted(m, system, links.Candidates);
            o["items"] = items;
            o["unlabelled"] = none;
        }
        return o;
    }

    static JsonObject FromBreakdown(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 and <= 200 } n ? n : throw new CommandException("Name the activity: „Install doors”.");
        var products = ProductRef.ReadAll(a["products"] ?? (a["product"] is { } one ? new JsonArray(one.DeepClone()) : null));
        foreach (var p in products) if (Breakdowns.SystemOf(m, p) is not { } s) throw new CommandException("The project has no product breakdown: choose a classification as its PBS, or make the starter one.");
            else if (!Classifications.Items(s).Any(i => i.Code == p.Code)) throw new CommandException($"{s.Name} has no code „{p.Code}”.");
        var keys = (a["locations"] as JsonArray ?? []).Select(x => ((string?)x)?.Trim()).OfType<string>().Where(x => x.Length > 0).Distinct().ToList();
        if (keys.Count == 0) keys = Spatial.Storeys(m).Select(s => s.Id).ToList();
        if (keys.Count == 0) throw new CommandException("The model has no storeys to make a task on each of.");
        if (keys.Count > TaskTiming.MaxSplits) throw new CommandException($"At most {TaskTiming.MaxSplits} locations.");
        var locations = keys.Select(k => m.Get(k) is { Class: IfcClass.Building or IfcClass.BuildingStorey or IfcClass.Space or IfcClass.Site } e ? e
            : throw new CommandException("A location is a building, a storey or a room of the model.")).ToList();
        var part = TaskParts.Parse(a["part"]);
        var norm = (string?)a["norm"] is { Length: > 0 } nc ? Norms.Of(m).Find(nc) ?? throw new CommandException($"The project has no norm „{nc}”.") : null;
        var days = a["days"] is null ? 5 : Math.Max(1, Days(a["days"], "duration"));
        var crewGiven = Json.Number(a["crew"]) is { } cg && cg >= 1 ? (int?)Math.Round(cg) : null;
        var kind = (string?)a["kind"] is { Length: > 0 } k ? Plan.Kinds.Contains(k) ? k : throw new CommandException("Not a kind of task IFC knows.") : null;
        var link = a["link"] is not JsonValue lv || !lv.TryGetValue<bool>(out var lb) || lb;
        var lag = a["lag"] is null ? 0 : Days(a["lag"], "lag");

        var (schedule, _) = EnsureSchedule(ctx);
        var plan = Plan.Read(m);
        var parent = (string?)a["parent"] is { } par && plan.ById.ContainsKey(par) ? par : null;
        var siblings = SiblingsOf(plan, parent);
        var summaryId = ctx.NewId();
        var sAttrs = new JsonObject { ["schedule"] = schedule.Id, ["order"] = siblings.Count + 1, ["duration"] = days };
        if (parent != null) sAttrs["parent"] = parent;
        ctx.Add(new Entity(summaryId, Plan.TaskClass, name, sAttrs, kind));

        var links = TaskElements.Resolve(ctx);
        var hours = plan.Calendar.Hours;
        var made = new JsonArray();
        string? previous = null;
        var order = 0;
        foreach (var loc in locations)
        {
            var rule = new JsonObject { ["locations"] = new JsonArray(loc.Id) };
            if (products.Count > 0) rule["products"] = new JsonArray(products.Select(p => p.ToJson()).ToArray());
            var attrs = new JsonObject { ["schedule"] = schedule.Id, ["order"] = ++order, ["parent"] = summaryId, ["rule"] = rule, ["duration"] = days, ["durationFrom"] = "default" };
            if (part != null) attrs["part"] = part;
            if (norm != null)
            {
                attrs["norm"] = norm.Code;
                var crew = crewGiven ?? norm.Crew ?? 1;
                attrs["crew"] = crew;
                if (Measure(ctx.Query, links, links.Of(Probe(rule, part)), norm) is { } qty)
                {
                    attrs["duration"] = Math.Max(1, (int)Math.Ceiling(qty * norm.Hours / (crew * Math.Max(0.5, hours)) - 1e-9));
                    attrs["durationFrom"] = "norm";
                    attrs["quantity"] = Math.Round(qty, 3);
                    attrs["unit"] = norm.Unit;
                }
            }
            else if (crewGiven is { } c) attrs["crew"] = c;
            var id = ctx.NewId();
            ctx.Add(new Entity(id, Plan.TaskClass, $"{name} · {LocationName(loc)}", attrs, kind));
            if (link && previous != null) PutLink(ctx, new PlanLink(ctx.NewId(), previous, id, "FS", lag));
            previous = id;
            made.Add((JsonNode)id);
        }
        return new JsonObject { ["summary"] = summaryId, ["tasks"] = made };
    }

    static string LocationName(Entity e) => e.Class == IfcClass.Space && e.Str("number") is { Length: > 0 } n ? $"{n} {e.Name}" : e.Name;
}
