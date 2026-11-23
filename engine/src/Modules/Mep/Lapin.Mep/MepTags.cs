using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// MEP tags (docs/plan-mep.md): Annotate's tags, with fields the MEP module works out and tag types of its own the user edits
/// as any other (Drafting standards → Tag types). A run's tag writes its material and size and, where it falls, its slope
/// ({mepSize}|{mepSlope}: <c>PP-R 20</c>, <c>PVC 110</c> / <c>i=2 %</c>); a rise's the elevation it goes to ({mepRise}:
/// <c>↑ +2.40</c>); a terminal's its power or flow ({mepDutyText}: <c>P=1200 W</c>). <c>mep.tagSystem</c> tags a system's runs,
/// rises and terminals on a plan in one step, skipping what has a tag there already; a tag follows its element as any tag.
/// </summary>
public static class MepTags
{
    public const string RunType = "mep-run", RiseType = "mep-rise", TerminalType = "mep-terminal";

    /// <summary>The tag types the MEP module brings: a project gets them the first time it tags a system, as its own to change.</summary>
    public static readonly IReadOnlyList<TagType> Types =
    [
        new(RunType, "MEP run", "{mepSize}|{mepSlope}", [], LeaderShape: "elbow", Underline: "none", Unit: "mm"),
        new(RiseType, "MEP rise / drop", "{mepRise}", [], LeaderShape: "elbow", Unit: "mm"),
        new(TerminalType, "MEP terminal", "{mepDutyText}", [], LeaderShape: "elbow", Unit: "mm"),
    ];

    static string Mm(double metres) => Math.Round(metres * 1000, 1).ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>A run's material and size as a drawing writes them: Cu 18, PP-R 20, PVC 110; a duct Ø125 or 300×200; null for what is not a run.</summary>
    public static string? Size(IModelView m, Entity e)
    {
        if (!Runs.IsRun(e)) return null;
        var s = Runs.SettingOf(m, e);
        if (Services.IsAir(s.Service))
            return e.NumOrNull("height") is { } h ? $"{Mm(s.Size)}×{Mm(h)}" : $"Ø{Mm(s.Size)}";
        var short_ = RunMaterials.Of(s.Service, s.Material).Short;
        return short_.Length > 0 ? $"{short_} {Mm(s.Size)}" : Mm(s.Size);
    }

    /// <summary>A run's fall as a drawing writes it (i=2 %), only where it falls; else empty.</summary>
    public static string? Slope(IModelView m, Entity e)
    {
        if (!Runs.IsRun(e)) return null;
        var slope = Runs.SettingOf(m, e).Slope;
        return slope > 1e-9 ? $"i={Math.Round(slope * 100, 1).ToString("0.#", CultureInfo.InvariantCulture)} %" : "";
    }

    /// <summary>An elevation over ±0.00 as levels write it: +2.40, -0.30, ±0.00.</summary>
    public static string Level(double z)
    {
        var r = Math.Round(z, 2);
        return Math.Abs(r) < 0.005 ? "±0.00" : (r > 0 ? "+" : "") + r.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>Where a rise goes to, its way and the elevation over ±0.00 (↑ +2.40, ↓ -0.30); null for what is not a rise.</summary>
    public static string? Rise(IModelView m, Entity e)
    {
        if (!Nodes.IsNode(e) || Nodes.RoleOf(e) is not { IsRise: true } role) return null;
        var bottom = Nodes.StoreyZ(m, e) + Nodes.Elevation(e, role);
        var top = Nodes.RiseTop(m, e);
        return (top >= bottom ? "↑ " : "↓ ") + Level(top);
    }

    /// <summary>A terminal's or an appliance's duty as a drawing writes it: P=1200 W, Q=0.1 L/s; null when it has none.</summary>
    public static string? Duty(IModelView m, Entity e)
    {
        if (!Nodes.IsNode(e) || Nodes.RoleOf(e) is not { } role) return null;
        var duty = e.NumOrNull("duty") ?? role.Duty;
        if (duty <= 0 || role.DutyUnit is not { Length: > 0 } unit) return null;
        // A boiler's 24000 W as 24 kW: a drawing writes large powers in kilowatts.
        if (unit == "W" && duty >= 10000) (duty, unit) = (duty / 1000, "kW");
        var v = duty.ToString("0.###", CultureInfo.InvariantCulture);
        return unit switch { "W" or "kW" => $"P={v} {unit}", "L/s" or "m³/h" or "L/h" => $"Q={v} {unit}", _ => $"{v} {unit}" };
    }

    /// <summary>The fields a tag, a live text or a formula reads of a run or a node ({mepSize}, {mepSlope}, {mepRise}, {mepDutyText}).</summary>
    public static void RegisterFields(IEnumerable<string> classes)
    {
        foreach (var cls in classes)
        {
            DerivedFields.Add(cls, new("mepSize", "MEP: a run's material and size (PP-R 20, Ø125)", Size));
            DerivedFields.Add(cls, new("mepSlope", "MEP: a run's fall (i=2 %), where it falls", Slope));
            DerivedFields.Add(cls, new("mepRise", "MEP: where a rise goes (↑ +2.40)", Rise));
            DerivedFields.Add(cls, new("mepDutyText", "MEP: a terminal's power or flow (P=1200 W)", Duty));
        }
    }

    /// <summary>The MEP tag types put among the project's tag types where it has none of their ids (one change of the command's step).</summary>
    public static void EnsureTypes(ICommandContext ctx)
    {
        var book = DraftingResources.Book(ctx.Model);
        var missing = Types.Where(t => book.TagTypes.All(x => x.Id != t.Id)).ToList();
        if (missing.Count == 0) return;
        var raw = Standards.ToJson(book);
        var list = raw["tagTypes"] as JsonArray ?? [];
        foreach (var t in missing) list.Add((JsonNode)JsonSerializer.SerializeToNode(t, DrawingJson.Default.TagType)!);
        raw["tagTypes"] = list;
        DraftingResources.Write(ctx, raw);
    }

    /// <summary>
    /// A system's runs, rises and terminals (with a duty) on a plan tagged, those that have a tag on it already left as they
    /// are; every system's when none is named. A run's tag along its longest leg, beside it; a node's at a leader's reach.
    /// Returns how many tags were made.
    /// </summary>
    public static int TagSystem(ICommandContext ctx, Entity view, IReadOnlyList<Entity> systems)
    {
        var m = ctx.Model;
        if (view.Str("kind") is not ("plan" or null) || view.Str("storeyId") is not { } storeyId) throw new CommandException("Tag system tags a plan: open a plan of the storey.");
        var tagged = m.OfClass(IfcClass.Annotation).Where(a => a.PredefinedType == "TAG" && a.Str("viewId") == view.Id).Select(a => a.Str("element")).OfType<string>().ToHashSet();
        var scale = view.NumOrNull("scale") ?? 100;
        // Paper millimetres on the model at the view's scale.
        var mm = scale / 1000;
        var made = new List<(string Element, JsonObject Args)>();
        bool Here(Entity e) => Spatial.StoreyOf(m, e.Id)?.Id == storeyId && !tagged.Contains(e.Id);
        foreach (var system in systems)
        {
            foreach (var run in Runs.Of(m, system.Id).Where(Here).OrderBy(r => r.Name, StringComparer.Ordinal))
            {
                var pts = Runs.Points(run);
                if (pts.Count < 2) continue;
                var leg = Enumerable.Range(1, pts.Count - 1).MaxBy(i => (pts[i] - pts[i - 1]).Length);
                var (a, b) = (pts[leg - 1], pts[leg]);
                if ((b - a).Length < 1e-6) continue;
                var along = (b - a).Normalized();
                // Read upright: the side above the line as it reads (left of a run drawn rightwards).
                if (along.X < -1e-9 || (Math.Abs(along.X) < 1e-9 && along.Y < 0)) along = -along;
                var left = new Vec2(-along.Y, along.X);
                var s = Runs.SettingOf(m, run);
                var clear = (s.Pair ? s.Separation / 2 : 0) + s.Size / 2 + 1.5 * mm;
                var text = (Size(m, run) ?? "").Length * 2.5 * 0.6 * mm;
                var at = (a + b) * 0.5 + left * clear - along * (text / 2);
                made.Add((run.Id, new JsonObject { ["tagType"] = RunType, ["at"] = new JsonArray(at.X, at.Y), ["leader"] = false, ["align"] = true }));
            }
            foreach (var node in Nodes.Of(m, system.Id).Where(Here).OrderBy(n => n.Name, StringComparer.Ordinal))
            {
                var role = Nodes.RoleOf(node);
                var type = role is { IsRise: true } ? RiseType : Duty(m, node) != null ? TerminalType : null;
                if (type == null) continue;
                var p = Nodes.Position(node);
                var at = p + new Vec2(8, 6) * mm;
                made.Add((node.Id, new JsonObject { ["tagType"] = type, ["at"] = new JsonArray(at.X, at.Y) }));
            }
        }
        if (made.Count == 0) return 0;
        EnsureTypes(ctx);
        foreach (var (element, args) in made)
        {
            args["viewId"] = view.Id;
            args["element"] = element;
            Tags.Add(ctx, args);
        }
        return made.Count;
    }

    public static void Register(IModuleRegistry r)
    {
        // A plan's MEP tags in one step: the system's (systemId), else every system's; what has a tag on the view already is left.
        r.Command("tagSystem", "Tag system", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("Which plan? (viewId)");
            var systems = (string?)a["systemId"] is { } sid ? [Systems.Require(ctx.Model, sid)] : Systems.All(ctx.Model).ToList();
            var count = TagSystem(ctx, view, systems);
            return new JsonObject { ["tagged"] = count };
        });

        // How a plan draws the devices' coverage circles (mode: all, selected, none; null: as each system says).
        r.Command("setCoverage", "Coverage", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("Which plan? (viewId)");
            var mode = a["mode"] is null ? null : CoverageModes.Checked(a["mode"]) ?? throw new CommandException("Coverage is all, selected or none.");
            var id = Views.OwnView(ctx, view, attrs => { if (mode == null) attrs.Remove("mepCoverage"); else attrs["mepCoverage"] = mode; });
            return new JsonObject { ["id"] = id };
        });
    }
}

/// <summary>
/// A device's coverage circle as a view draws it (<see cref="Systems.Coverage"/>): in every view, or in none; selected only
/// (the default) leaves it out of the drawing, the interface drawing it over the plan for the devices selected, never printed.
/// </summary>
public sealed class MepPlanInView : IPlanInView
{
    public bool Handles(Entity e) => Nodes.IsNode(e);

    public IReadOnlyList<PlanItem> PlanIn(IModelView m, Entity view, Entity e, IReadOnlyList<PlanItem> items)
    {
        if (!items.Any(i => i.Kind == "reach")) return items;
        var system = m.Get(e.Str("systemId") ?? "") is { Class: Systems.Class } s ? s : null;
        return Systems.Coverage(view, system) == CoverageModes.All ? items : items.Where(i => i.Kind != "reach").ToList();
    }
}
