using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// Shapes drawn with the constraints recorded while drawing them (stage C2 of decision 520, §4.4), one command and so one undo
/// step: Undo takes the shape and its constraints together. The drawing itself is <c>drafting.draw</c>'s, run inside this
/// command (as <c>edit.batch</c> runs its steps): Drafting knows nothing of constraints, Studio already requires it.
/// </summary>
public static partial class SketchConstraints
{
    static void RegisterDraw(IModuleRegistry r)
    {
        // {viewId, layer?, role?, shapes | shape (as drafting.draw takes them), constraints: [{kind, refs: [{shape, part} | {new: i,
        // part}], value?, formula?, driving?, place?}]} → {ids, constraints: [id…], refused?: [{index, kind, reason}]}. A ref
        // {new: i} names the i-th shape drawn. The constraints are added and solved at once; when the solver refuses the batch
        // they are tried one by one, those that hold kept, the rest reported (never the drawing refused for them).
        r.Command("drawConstrained", "Draw", (ctx, a) =>
        {
            var catalog = ctx.Services as ModuleCatalog ?? throw new CommandException("No drafting here to draw with.");
            var draw = catalog.Command("drafting.draw") ?? throw new CommandException("Drawing needs the Drafting module.");
            var drawArgs = new JsonObject { ["viewId"] = (string?)a["viewId"] };
            if ((string?)a["layer"] is { } layer) drawArgs["layer"] = layer;
            if (a["shapes"] is JsonArray shapes) drawArgs["shapes"] = shapes.DeepClone(); else drawArgs["shape"] = a["shape"]?.DeepClone();
            if (a["role"] is { } role) drawArgs["role"] = role.DeepClone();
            var drawn = draw.Handler(ctx, drawArgs) as JsonObject ?? new JsonObject();
            var ids = (drawn["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [];
            // The sketch is the view the shapes went on (a storey's implicit plan as drafting names it).
            var view = ids.Count > 0 && ctx.Model.Get(ids[0])?.Str("viewId") is { } on ? on : (string?)a["viewId"] ?? "";
            var refused = new List<(int Index, string? Kind, string Reason)>();
            var items = new List<(int Index, JsonObject C)>();
            var given = a["constraints"] as JsonArray ?? [];
            for (var i = 0; i < given.Count; i++)
            {
                if (given[i] is not JsonObject item) { refused.Add((i, null, "A constraint is {kind, refs, …}.")); continue; }
                var (c, why) = Recorded(item, ids);
                if (c == null) refused.Add((i, (string?)item["kind"], why!));
                else items.Add((i, c));
            }
            var added = new List<string>();
            if (items.Count > 0)
            {
                // All at once, one solve: what drawing records usually holds together.
                var before = ListOf(CarrierOf(ctx.Model, view));
                var report = Report(ctx);
                var mark = report.Dropped.Count;
                try
                {
                    added.AddRange(AddAll(ctx, view, items.Select(x => x.C)));
                }
                catch (CommandException)
                {
                    // One conflicts: back to the sketch as it was, then one by one; what the solver refuses is told.
                    Undo(ctx, view, before, report, mark);
                    foreach (var (index, c) in items)
                    {
                        var was = ListOf(CarrierOf(ctx.Model, view));
                        var at = report.Dropped.Count;
                        try { added.AddRange(AddAll(ctx, view, [c])); }
                        catch (CommandException ex)
                        {
                            Undo(ctx, view, was, report, at);
                            refused.Add((index, (string?)c["kind"], ex.Message));
                        }
                    }
                }
            }
            var o = new JsonObject
            {
                ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()),
                ["constraints"] = new JsonArray(added.Select(x => (JsonNode)x).ToArray()),
            };
            if (refused.Count > 0)
                o["refused"] = new JsonArray(refused.OrderBy(x => x.Index).Select(x => (JsonNode)new JsonObject { ["index"] = x.Index, ["kind"] = x.Kind, ["reason"] = x.Reason }).ToArray());
            return Attach(ctx, o);
        });
    }

    /// <summary>A recorded constraint with its {new: i} refs named by the shapes drawn; null and why when one names no shape drawn.</summary>
    static (JsonObject?, string?) Recorded(JsonObject item, IReadOnlyList<string> ids)
    {
        var c = new JsonObject { ["kind"] = (string?)item["kind"] };
        var refs = new JsonArray();
        foreach (var r in item["refs"] as JsonArray ?? [])
        {
            if (r is not JsonObject ro) return (null, "A reference is {shape, part} or {new, part}.");
            var part = (string?)ro["part"] ?? "whole";
            if (ro["new"] is { } n)
            {
                var i = Json.Number(n) is { } x && x == Math.Floor(x) ? (int)x : -1;
                if (i < 0 || i >= ids.Count) return (null, $"No shape {n.ToJsonString()} was drawn.");
                refs.Add((JsonNode)new JsonObject { ["shape"] = ids[i], ["part"] = part });
            }
            else refs.Add((JsonNode)new JsonObject { ["shape"] = (string?)ro["shape"], ["part"] = part });
        }
        c["refs"] = refs;
        if (item["value"] is { } value) c["value"] = Json.Number(value) is { } v ? v : value.DeepClone();
        if ((string?)item["formula"] is { } formula) c["formula"] = formula;
        if (item["driving"] is JsonValue dv && dv.TryGetValue<bool>(out var driving)) c["driving"] = driving;
        if (item["place"] is JsonObject place) c["place"] = place.DeepClone();
        return (c, null);
    }

    /// <summary>Constraints checked, stored on <paramref name="view"/>'s sketch and solved, in one go; their ids. Throws when one does not fit or the solve fails.</summary>
    static List<string> AddAll(ICommandContext ctx, string view, IEnumerable<JsonObject> items)
    {
        var ids = new List<string>();
        var list = ListOf(CarrierOf(ctx.Model, view));
        foreach (var item in items)
        {
            var c = (JsonObject)item.DeepClone();
            Check(ctx.Model, c, view);
            var stored = new JsonObject { ["id"] = ctx.NewId() };
            foreach (var (k, v) in c) stored[k] = v?.DeepClone();
            list.Add(stored);
            ids.Add((string)stored["id"]!);
        }
        Write(ctx, view, list);
        AfterChange(ctx, view);
        return ids;
    }

    /// <summary>A failed try taken back: the sketch's constraints as they were, the drops it reported forgotten (a solve that fails writes no shape).</summary>
    static void Undo(ICommandContext ctx, string view, List<JsonObject> was, Outcome report, int mark)
    {
        Write(ctx, view, was);
        if (report.Dropped.Count > mark) report.Dropped.RemoveRange(mark, report.Dropped.Count - mark);
    }
}
