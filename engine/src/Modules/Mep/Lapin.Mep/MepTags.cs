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

    static Vec2? Pt(JsonNode? n) => n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : null;

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
    /// are; every system's when none is named. Each tag goes to the first of its candidate places (a run's beside its legs, a
    /// node's round it on a leader) whose text and leader cross least of what the plan draws (walls, windows, runs, symbols),
    /// the tags on it and those placed before it (<see cref="Place"/>): one step, the same places each time. Returns how many tags were made.
    /// </summary>
    public static int TagSystem(ICommandContext ctx, Entity view, IReadOnlyList<Entity> systems)
    {
        var m = ctx.Model;
        if (view.Str("kind") is not ("plan" or null) || view.Str("storeyId") is not { } storeyId) throw new CommandException("Tag system tags a plan: open a plan of the storey.");
        var tagged = m.OfClass(IfcClass.Annotation).Where(a => a.PredefinedType == "TAG" && a.Str("viewId") == view.Id).Select(a => a.Str("element")).OfType<string>().ToHashSet();
        bool Here(Entity e) => Spatial.StoreyOf(m, e.Id)?.Id == storeyId && !tagged.Contains(e.Id);
        var todo = new List<(Entity Element, string Type)>();
        foreach (var system in systems)
        {
            foreach (var run in Runs.Of(m, system.Id).Where(Here).OrderBy(r => r.Name, StringComparer.Ordinal))
                if (Runs.Points(run).Count >= 2) todo.Add((run, RunType));
            foreach (var node in Nodes.Of(m, system.Id).Where(Here).OrderBy(n => n.Name, StringComparer.Ordinal))
                if ((Nodes.RoleOf(node) is { IsRise: true } ? RiseType : Duty(m, node) != null ? TerminalType : null) is { } type) todo.Add((node, type));
        }
        if (todo.Count == 0) return 0;
        var book = DraftingResources.Book(m);
        var mm = (view.NumOrNull("scale") ?? 100) / 1000;
        var h = Standards.TextOf(book, Standards.Standard).Height * mm;
        // What the plan draws now (its lines and its marks: other tags' text boxes among them), as segments to keep clear of.
        var obstacles = Obstacles(ctx.Query, view);
        var placed = new List<Vec2[]>();
        var made = new List<(string Element, JsonObject Args)>();
        foreach (var (e, typeId) in todo)
        {
            var type = book.TagTypes.FirstOrDefault(t => t.Id == typeId) ?? Types.First(t => t.Id == typeId);
            var lines = TagText.Lines(type.Written, m, e).Where(l => l.Length > 0).ToList();
            if (lines.Count == 0) lines = ["?"];
            var w = lines.Max(l => BuildupText.Measure(l, h / mm, false)) * mm;
            var (args, box) = Runs.IsRun(e) ? Place(RunCandidates(m, e, lines.Count, w, h), obstacles, placed) : Place(NodeCandidates(m, e, lines.Count, w, h, mm), obstacles, placed);
            args["tagType"] = typeId;
            placed.Add(box);
            made.Add((e.Id, args));
        }
        EnsureTypes(ctx);
        foreach (var (element, args) in made)
        {
            args["viewId"] = view.Id;
            args["element"] = element;
            var tag = Tags.Add(ctx, args);
            // A run's tag keeps its leader's end (not drawn) behind its knee, so its text reads on from there.
            if (Pt(args["end"]) is { } end)
            {
                var attrs = tag.CloneAttrs();
                attrs["end"] = new JsonArray(end.X, end.Y);
                ctx.Replace(tag.With(attrs: attrs));
            }
        }
        return made.Count;
    }

    /// <summary>The candidate whose box and leader cross least (the first of those, in the candidates' order): deterministic.</summary>
    static (JsonObject Args, Vec2[] Box) Place(List<(JsonObject Args, Vec2[] Box, (Vec2 A, Vec2 B)? Leader)> candidates, List<(Vec2 A, Vec2 B)> obstacles, List<Vec2[]> placed)
    {
        var best = candidates[0];
        var least = int.MaxValue;
        // A leader crossing a line (a wall between the node and its text) counts as the text crossing it; every candidate's
        // leader leaves the node's own symbol alike, so those crossings only add the same to each.
        foreach (var c in candidates)
        {
            var hits = obstacles.Count(o => Crosses(c.Box, o.A, o.B)) + placed.Count(p => Overlap(c.Box, p)) * 100
                + (c.Leader is { } l ? obstacles.Count(o => Segments(l.A, l.B, o.A, o.B)) + placed.Count(p => Crosses(p, l.A, l.B)) * 100 : 0);
            if (hits < least) (best, least) = (c, hits);
        }
        return (best.Args, best.Box);
    }

    /// <summary>A run's tag along it, beside the line on either side, at the middle of its legs, the longest first, and at their quarters.</summary>
    static List<(JsonObject, Vec2[], (Vec2, Vec2)?)> RunCandidates(IModelView m, Entity run, int rows, double w, double h)
    {
        var pts = Runs.Points(run);
        var s = Runs.SettingOf(m, run);
        var clear = (s.Pair ? s.Separation / 2 : 0) + s.Size / 2 + 0.3 * h;
        var pad = TagLeaders.Pad * h;
        var o = new List<(JsonObject, Vec2[], (Vec2, Vec2)?)>();
        var legs = Enumerable.Range(1, pts.Count - 1).Where(i => (pts[i] - pts[i - 1]).Length > 1e-6).OrderByDescending(i => (pts[i] - pts[i - 1]).Length).ThenBy(i => i).ToList();
        foreach (var at in new[] { 0.5, 0.25, 0.75 })
            foreach (var leg in legs)
                foreach (var left in new[] { true, false })
                {
                    var (a, b) = (pts[leg - 1], pts[leg]);
                    var along = (b - a).Normalized();
                    // Read upright: along to the right (up for a vertical leg), its left above the line.
                    if (along.X < -1e-9 || (Math.Abs(along.X) < 1e-9 && along.Y < 0)) along = -along;
                    var up = new Vec2(-along.Y, along.X);
                    var mid = a + (b - a) * at;
                    // The box a plain tag's text takes (TagLeaders.Plain, no underline): the first row centred on the knee, the
                    // others down from it, a pad round them; from the knee along to past the text.
                    var top = h / 2 + pad;
                    var bottom = -h / 2 - TagLeaders.Pitch * h * (rows - 1) - TagLeaders.Under * h - pad;
                    var v = left ? clear - bottom : -clear - top;
                    var knee = mid + up * v - along * (pad + w / 2);
                    Vec2 P(double du, double dv) => knee + along * du + up * dv;
                    // Its leader's end (not drawn) behind the knee: the text then reads on from the knee (TagLeaders.Side), never back over it.
                    var behind = knee - along * h;
                    // Turned along its own leg (not the run's longest line, as aligned tags are).
                    var angle = Math.Round(Math.Atan2(along.Y, along.X) * 180 / Math.PI, 4);
                    o.Add((new JsonObject { ["at"] = new JsonArray(knee.X, knee.Y), ["end"] = new JsonArray(behind.X, behind.Y), ["leader"] = false, ["rotation"] = angle },
                        [P(0, bottom), P(2 * pad + w, bottom), P(2 * pad + w, top), P(0, top)], null));
                }
        return o;
    }

    /// <summary>A node's tag on a leader, its knee round the node at two reaches in eight directions (the room side found by what is clear).</summary>
    static List<(JsonObject, Vec2[], (Vec2, Vec2)?)> NodeCandidates(IModelView m, Entity node, int rows, double w, double h, double mm)
    {
        var p = Nodes.Position(node);
        var o = new List<(JsonObject, Vec2[], (Vec2, Vec2)?)>();
        (double X, double Y)[] dirs = [(1, 1), (-1, 1), (1, -1), (-1, -1), (1, 0), (-1, 0), (0, 1), (0, -1)];
        foreach (var reach in new[] { 8.0, 14.0, 22.0 })
            foreach (var (dx, dy) in dirs)
            {
                var d = new Vec2(dx, dy);
                var knee = p + d.Normalized() * (reach * mm);
                // The text on the side away from the node (TagLeaders.Side), its rows down from the first, over the shoulder.
                var side = knee.X < p.X - 1e-9 ? -1 : 1;
                var pad = TagLeaders.Pad * h;
                var x0 = knee.X; var x1 = knee.X + side * (2 * pad + w);
                var top = knee.Y + TagLeaders.Under * h + h + pad;
                var bottom = knee.Y + TagLeaders.Under * h - TagLeaders.Pitch * h * (rows - 1) - TagLeaders.Under * h - pad;
                var (lo, hi) = (Math.Min(x0, x1), Math.Max(x0, x1));
                o.Add((new JsonObject { ["at"] = new JsonArray(knee.X, knee.Y) }, [new(lo, bottom), new(hi, bottom), new(hi, top), new(lo, top)], (p, knee)));
            }
        return o;
    }

    /// <summary>The plan's lines as segments (its pens' and its marks', the tags' boxes among them; never the hidden ones, a coverage circle).</summary>
    static List<(Vec2 A, Vec2 B)> Obstacles(IQueryContext q, Entity view)
    {
        var o = new List<(Vec2, Vec2)>();
        Views.Rendered r;
        try { r = Views.Render(q, view); }
        catch (CommandException) { return o; }
        foreach (var pen in r.Pens)
        {
            if (pen.Layer is PenLayer.Hidden or PenLayer.Fill or PenLayer.Hatch) continue;
            o.AddRange(pen.Segments);
            foreach (var loop in pen.Loops)
                for (var i = 0; i < loop.Count; i++) o.Add((loop[i], loop[(i + 1) % loop.Count]));
        }
        foreach (var mk in r.Marks)
            foreach (var l in mk.Lines)
                if (!mk.Pick || l.Layer == "PICK" && mk.Owner != null && m(q, mk.Owner)) o.Add((l.A, l.B));
        return o;

        // A pick mark's lines count only for an annotation's (a tag's text box); an element's pick lines repeat its own.
        static bool m(IQueryContext q, string owner) => q.Model.Get(owner) is { Class: IfcClass.Annotation };
    }

    /// <summary>Whether a segment touches a convex box (an end inside, or crossing an edge).</summary>
    static bool Crosses(Vec2[] box, Vec2 a, Vec2 b)
    {
        if (Inside(box, a) || Inside(box, b)) return true;
        for (var i = 0; i < box.Length; i++)
            if (Segments(box[i], box[(i + 1) % box.Length], a, b)) return true;
        return false;
    }

    static bool Overlap(Vec2[] x, Vec2[] y) =>
        x.Any(p => Inside(y, p)) || y.Any(p => Inside(x, p)) || Enumerable.Range(0, x.Length).Any(i => Crosses(y, x[i], x[(i + 1) % x.Length]));

    static bool Inside(Vec2[] box, Vec2 p)
    {
        int pos = 0, neg = 0;
        for (var i = 0; i < box.Length; i++)
        {
            var (a, b) = (box[i], box[(i + 1) % box.Length]);
            var c = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            if (c > 1e-12) pos++; else if (c < -1e-12) neg++;
        }
        return pos == 0 || neg == 0;
    }

    static bool Segments(Vec2 p1, Vec2 p2, Vec2 p3, Vec2 p4)
    {
        static double Cross(Vec2 o, Vec2 a, Vec2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var d1 = Cross(p3, p4, p1); var d2 = Cross(p3, p4, p2); var d3 = Cross(p1, p2, p3); var d4 = Cross(p1, p2, p4);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
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
