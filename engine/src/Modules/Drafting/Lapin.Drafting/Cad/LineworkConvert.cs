using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting.Cad;

/// <summary>
/// Convert drafting to… (docs/plan-user-extensibility.md §7.4–7.5): drafted shapes — drawn, traced from a PDF or imported from a
/// DXF — become what a module's converter makes of them (walls, floors, rooms, railings, beams, columns, footings, bars). By the
/// shapes chosen, or by layers (a DXF's A-WALL to walls of a type, S-COLS to columns…, a map the user keeps). One undo step;
/// the drafting goes unless kept.
/// </summary>
public static class LineworkConvert
{
    /// <summary>A drafted shape as converters read it; null for hatches and construction lines.</summary>
    public static LineworkShape? Of(IModelView m, Entity e)
    {
        if (e.PredefinedType != Drafts.Predefined || e.Attrs["hatch"] != null || e.Str("role") == "draft") return null;
        var layer = e.Str("layer");
        // The heights a DXF brought, one per point (decision 268's civil linework reads them too).
        IReadOnlyList<double>? Heights(int n) => e.Attrs["heights"] is JsonArray za && za.Count == n ? [.. za.Select(z => Json.Number(z) ?? double.NaN)] : null;
        switch (Drafts.ShapeOf(e))
        {
            case Line l: return new(e.Id, "line", l.ToJson(), [l.A, l.B], false, layer, Heights: Heights(2));
            case Polyline p: return new(e.Id, "polyline", p.ToJson(), p.Pts, p.Closed && p.Pts.Count >= 3, layer, Heights: Heights(p.Pts.Count));
            case Circle c:
            {
                var loop = Drafts.Flatten(c)[0];
                loop.RemoveAt(loop.Count - 1);
                return new(e.Id, "circle", c.ToJson(), loop, true, layer, c.C, c.R);
            }
            case Arc a:
                return new(e.Id, "arc", a.ToJson(), Drafts.Flatten(a)[0], false, layer, a.C, a.R, a.At(a.Start + a.Sweep / 2));
            // An ellipse (audit S4): a whole one closed, made into outlines with its elliptical edges; an elliptical arc open.
            case Ellipse el:
            {
                var pts = el.Points();
                if (el.Whole) pts.RemoveAt(pts.Count - 1);
                return new(e.Id, "ellipse", el.ToJson(), pts, el.Whole, layer, el.C);
            }
            default: return null;
        }
    }

    static IEnumerable<Entity> Chosen(IModelView m, Entity view, JsonObject a)
    {
        var all = Drafts.On(m, view.Id);
        if (a["ids"] is JsonArray ids)
        {
            // The shapes chosen on this view; on a plan, those drawn on the level's default plan (plan:<storey>) or on another plan
            // view of its level too, as the interface may name either (audit S12).
            var set = ids.Select(x => (string?)x).OfType<string>().ToHashSet();
            var storey = view.Str("kind") == "plan" ? view.Str("storeyId") : null;
            bool Same(string? v) => v == view.Id || (storey != null && v != null && Views.Find(m, v) is { } o && o.Str("kind") == "plan" && o.Str("storeyId") == storey);
            return set.Select(m.Get).OfType<Entity>().Where(e => e.Class == IfcClass.Annotation && e.PredefinedType == Drafts.Predefined && Same(e.Str("viewId")));
        }
        if (a["layers"] is JsonArray layers)
        {
            var set = layers.Select(x => (string?)x).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            return all.Where(e => set.Contains(e.Str("layer") ?? "0"));
        }
        throw new CommandException("Which drafting? Choose shapes, or name their layers.");
    }

    /// <summary>The conversions asked: one converter for the shapes chosen, or a map of layers each to its converter and options.</summary>
    static List<(LineworkConverter Converter, JsonObject Options, List<Entity> Shapes)> Jobs(IQueryContext q, Entity view, JsonObject a)
    {
        var m = q.Model;
        var jobs = new List<(LineworkConverter, JsonObject, List<Entity>)>();
        if (a["map"] is JsonArray map)
        {
            foreach (var row in map.OfType<JsonObject>())
            {
                var layer = (string?)row["layer"] ?? throw new CommandException("Each row of the map names its layer.");
                if ((string?)row["converter"] is not { Length: > 0 } cid) continue;
                var conv = LineworkConversion.Converter(q.Services, cid);
                var shapes = Drafts.On(m, view.Id).Where(e => string.Equals(e.Str("layer") ?? "0", layer, StringComparison.OrdinalIgnoreCase)).ToList();
                jobs.Add((conv, row["options"] as JsonObject ?? [], shapes));
            }
        }
        else jobs.Add((LineworkConversion.Converter(q.Services, (string?)a["converter"]), a["options"] as JsonObject ?? [], Chosen(m, view, a).ToList()));
        foreach (var (conv, _, _) in jobs)
            if (!conv.Views.Contains(view.Str("kind") ?? "")) throw new CommandException($"{conv.Label} are made from a {string.Join(" or ", conv.Views)}'s drafting.");
        return jobs;
    }

    /// <summary>
    /// Each shape's steps, or why it is left: (shape id, converter, steps, error, joined). A converter that makes one element of
    /// several shapes gives its steps to the group's first shape; the others name it (<c>Joined</c>) and carry none.
    /// </summary>
    static List<(string Id, LineworkConverter Converter, IReadOnlyList<TableStep> Steps, string? Error, string? Joined)> Plan(IQueryContext q, Entity view, JsonObject a)
    {
        var o = new List<(string, LineworkConverter, IReadOnlyList<TableStep>, string?, string?)>();
        foreach (var (conv, options, shapes) in Jobs(q, view, a))
        {
            var passed = new List<LineworkShape>();
            var at = o.Count;
            foreach (var e in shapes)
            {
                var s = Of(q.Model, e);
                if (s == null) { o.Add((e.Id, conv, [], "A hatch or a construction line is not converted.", null)); continue; }
                if (!conv.Accepts(s)) { o.Add((e.Id, conv, [], conv.Takes == "closed" ? $"{conv.Label} need a closed outline." : $"{conv.Label} need lines.", null)); continue; }
                try
                {
                    var steps = conv.Steps(q, view, s, options);
                    o.Add((e.Id, conv, conv.Together == null ? steps : [], null, null));
                    passed.Add(s);
                }
                catch (CommandException ex) { o.Add((e.Id, conv, [], ex.Message, null)); }
            }
            if (conv.Together == null || passed.Count == 0) continue;
            IReadOnlyList<LineworkGroup> groups;
            try { groups = conv.Together(q, view, passed, options); }
            catch (CommandException ex)
            {
                for (var i = at; i < o.Count; i++) if (o[i].Item4 == null) o[i] = (o[i].Item1, conv, [], ex.Message, null);
                continue;
            }
            var row = new Dictionary<string, int>();
            for (var i = at; i < o.Count; i++) row.TryAdd(o[i].Item1, i);
            var grouped = new HashSet<string>();
            foreach (var g in groups)
            {
                if (g.Ids.Count == 0) continue;
                for (var k = 0; k < g.Ids.Count; k++)
                {
                    if (!row.TryGetValue(g.Ids[k], out var i) || !grouped.Add(g.Ids[k])) continue;
                    o[i] = k == 0 ? (g.Ids[0], conv, g.Steps, null, null) : (g.Ids[k], conv, [], null, g.Ids[0]);
                }
            }
            // A shape no group took is left, with why.
            foreach (var s in passed) if (!grouped.Contains(s.Id)) o[row[s.Id]] = (s.Id, conv, [], $"{conv.Label}: nothing is made of this shape.", null);
        }
        return o;
    }

    public static void Register(IModuleRegistry r)
    {
        // What a view's drafting can become here (its kind's converters), with their options; and the view's drafting layers with how many shapes each holds.
        r.Query("drafting.converters", (q, a) =>
        {
            var view = Views.Find(q.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
            var kind = view.Str("kind") ?? "";
            var counts = Drafts.On(q.Model, view.Id).Where(e => Of(q.Model, e) != null).GroupBy(e => e.Str("layer") ?? "0").ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            return new JsonObject
            {
                ["converters"] = new JsonArray(LineworkConversion.Converters(q.Services).Where(c => c.Views.Contains(kind)).Select(c => (JsonNode)c.ToJson()).ToArray()),
                ["layers"] = new JsonArray(counts.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).Select(k => (JsonNode)new JsonObject { ["layer"] = k.Key, ["shapes"] = k.Value }).ToArray()),
            };
        });

        // What converting would make, shape by shape, or why a shape is left: {viewId, ids | layers, converter, options} or {viewId, map: [{layer, converter, options}]}.
        r.Query("drafting.convertPreview", (q, a) =>
        {
            var view = Views.Find(q.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
            var plan = Plan(q, view, a);
            return new JsonObject
            {
                ["shapes"] = new JsonArray(plan.Select(p => (JsonNode)new JsonObject
                {
                    ["id"] = p.Id, ["converter"] = p.Converter.Id, ["error"] = p.Error, ["steps"] = new JsonArray(p.Steps.Select(s => (JsonNode)s.Describe).ToArray()),
                    ["joined"] = p.Joined,
                }).ToArray()),
                ["ok"] = plan.Count(p => p.Error == null), ["left"] = plan.Count(p => p.Error != null), ["steps"] = plan.Sum(p => p.Steps.Count),
            };
        });

        // The drafting converted, one undo step: what each shape becomes through its module's commands; the shapes converted go
        // unless `keep` (by default kept when converting by layers: a DXF stays the underlay it was).
        r.Command("drafting.convert", "Convert drafting", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
            var plan = Plan(ctx.Query, view, a);
            if (plan.All(p => p.Error != null))
                throw new CommandException(plan.Count == 0 ? "There is nothing to convert." : $"Nothing can be converted: {plan[0].Error}");
            var catalog = ctx.Services as ModuleCatalog ?? throw new CommandException("No commands to convert through here.");
            var keep = a["keep"] is JsonValue kv && kv.TryGetValue<bool>(out var k) ? k : a["ids"] == null;
            var made = new JsonArray();
            var skipped = 0;
            foreach (var (id, _, steps, error, _) in plan)
            {
                if (error != null) continue;
                foreach (var step in steps)
                {
                    var entry = catalog.Command(step.Command) ?? throw new CommandException($"This edition has no „{step.Command}”.");
                    var result = entry.Handler(ctx, step.Args);
                    if ((string?)result?["id"] is { } nid) { if (!made.Any(x => (string?)x == nid)) made.Add((JsonNode)nid); }
                    else if (result?["ids"] is JsonArray nids) foreach (var x in nids) made.Add(x?.DeepClone());
                    // What a command makes beside (walls along a shape's bottom and top floors).
                    if (result?["slabs"] is JsonArray more) foreach (var x in more) made.Add(x?.DeepClone());
                    // The pieces a command left out (curtain walls along a path: segments under 100 mm).
                    if (Json.Number(result?["skipped"]) is { } sk) skipped += (int)sk;
                }
                if (!keep && ctx.Model.Get(id) != null) ctx.Remove(id);
            }
            var outcome = new JsonObject { ["ids"] = made, ["converted"] = plan.Count(p => p.Error == null), ["left"] = plan.Count(p => p.Error != null) };
            if (skipped > 0) outcome["skipped"] = skipped;
            return outcome;
        });
    }
}
