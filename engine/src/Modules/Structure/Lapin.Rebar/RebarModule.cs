using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>The project's rebar drawing settings (<c>rebarSettings</c>): colours by diameter, the text and balloon sizes (paper mm), the steel, segment lengths written on the bars, the cover.</summary>
public sealed record RebarSettings(bool ColorByDiameter = true, double TextHeight = 2.5, double Balloon = 7.5, string Steel = RebarShapes.DefaultSteel, bool SegmentLengths = false, double Cover = 0.025)
{
    public const string Key = "rebarSettings";

    public static RebarSettings Of(IModelView m)
    {
        var o = Spatial.Project(m)?.Attrs[Key] as JsonObject;
        if (o == null) return new RebarSettings();
        return new RebarSettings(
            !(o["colorByDiameter"] is JsonValue cv && cv.TryGetValue<bool>(out var cb) && !cb),
            Json.Number(o["textHeight"]) ?? 2.5, Json.Number(o["balloon"]) ?? 7.5, (string?)o["steel"] ?? RebarShapes.DefaultSteel,
            o["segmentLengths"] is JsonValue sv && sv.TryGetValue<bool>(out var sl) && sl, Json.Number(o["cover"]) ?? 0.025);
    }

    public JsonObject ToJson() => new()
    {
        ["colorByDiameter"] = ColorByDiameter, ["textHeight"] = TextHeight, ["balloon"] = Balloon, ["steel"] = Steel, ["segmentLengths"] = SegmentLengths, ["cover"] = Cover,
    };
}

/// <summary>The bars (<c>LapinRebar</c>) and distributions (<c>LapinRebarRun</c>) drawn on views: reading them, naming them, their labels.</summary>
public static class Rebars
{
    public const string Class = "LapinRebar";
    public const string RunClass = "LapinRebarRun";
    public static readonly string[] Views = ["front", "top", "side", "section"];
    public static readonly string[] Shows = ["all", "ends", "one"];

    public static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
    public static JsonArray J(Vec2 p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6));

    public static RebarSpec SpecOf(Entity e)
    {
        var shape = RebarShapes.IsShape(e.Str("shape")) ? e.Str("shape")! : "straight";
        var def = RebarShapes.Def(shape);
        var p = new Dictionary<string, double>();
        if (e.Attrs["params"] is JsonObject po)
            foreach (var (k, v) in po) if (Json.Number(v) is { } x) p[k] = x;
        foreach (var d in def.Params) p.TryAdd(d.Key, d.Default);
        var vertices = e.Attrs["vertices"] is JsonArray va && va.Count >= 2 ? va.Select(P).ToList() : null;
        var hooks = e.Attrs["hooks"] as JsonObject;
        return new RebarSpec(shape, p, vertices, e.NumOrNull("diameter") ?? def.DefaultDiameter, Hook.From(hooks?["start"]), Hook.From(hooks?["end"]),
            e.Num("rotation"), e.Bool("mirrorX"), e.Bool("mirrorY"), e.NumOrNull("bendRadius"));
    }

    public static Vec2 At(Entity e) => P(e.Attrs["at"]);
    public static int Mark(Entity e) => (int)Math.Max(1, Math.Round(e.Num("mark", 1)));
    public static int Count(Entity e) => (int)Math.Max(1, Math.Round(e.Num("count", 1)));
    public static Vec2? LabelAt(Entity e) => e.Attrs["label"] is JsonObject o && o["at"] is JsonArray ? P(o["at"]) : null;

    /// <summary>A bar's array in its view: how many copies and the step along x and y (one copy: none).</summary>
    public static (int Nx, double Dx, int Ny, double Dy) Array(Entity e)
    {
        if (e.Attrs["array"] is not JsonObject o) return (1, 0, 1, 0);
        return ((int)Math.Max(1, Json.Number(o["nx"]) ?? 1), Json.Number(o["dx"]) ?? 0, (int)Math.Max(1, Json.Number(o["ny"]) ?? 1), Json.Number(o["dy"]) ?? 0);
    }

    public static int Pieces(Entity e) { var (nx, _, ny, _) = Array(e); return Count(e) * nx * ny; }

    public static IEnumerable<Entity> On(IModelView m, string viewId, string cls) => m.OfClass(cls).Where(e => e.Str("viewId") == viewId);

    /// <summary>What a label says of a bar: "4Ø12 L=2450".</summary>
    public static string Label(Entity e, RebarSpec s) => $"{Pieces(e)}{RebarShapes.Dia(s.Diameter)} L={RebarShapes.Mm(RebarShapes.Length(s))}";

    /// <summary>A run's path in its view, its positions, and what its label says ("11Ø8/100").</summary>
    public static (PathCurve Path, List<double> Positions, string Step, string Label) RunOf(Entity e, RebarSpec s)
    {
        var path = new PathCurve(P(e.Attrs["start"]), P(e.Attrs["end"]), e.Num("rise"));
        var zones = (e.Attrs["zones"] as JsonArray)?.Select(Runs.Zone.From).OfType<Runs.Zone>().ToList();
        var step = e.NumOrNull("step") ?? 0.15;
        var positions = Runs.Positions(path.Length, step, zones);
        var text = Runs.StepText(path.Length, step, zones);
        return (path, positions, text, $"{positions.Count * Count(e)}{RebarShapes.Dia(s.Diameter)}/{text}");
    }

    /// <summary>The next free mark: one over the highest in the model.</summary>
    public static int NextMark(IModelView m) => 1 + m.OfClass(Class).Concat(m.OfClass(RunClass)).Select(Mark).DefaultIfEmpty(0).Max();
}

/// <summary>The bar bending schedule: the bars by mark, their count, length and mass, the totals by diameter.</summary>
public sealed class RebarSchedule : IScheduleSource
{
    public string Kind => "rebar";
    public string Label => "Bar bending schedule";
    public static readonly string[] Columns = ["Mark", "Ø (mm)", "Shape", "No.", "Length (m)", "Total (m)", "Mass (kg)", "Note"];

    static string N(double v, int decimals = 2) => v.ToString(decimals == 2 ? "0.00" : "0.###", CultureInfo.InvariantCulture);

    public sealed record Entry(int Mark, double Diameter, string Shape, int Count, double Length);

    public static List<Entry> Entries(IModelView m)
    {
        var o = new List<Entry>();
        foreach (var e in m.OfClass(Rebars.Class))
        {
            if (e.Bool("excludeSchedule")) continue;
            var s = Rebars.SpecOf(e);
            o.Add(new Entry(Rebars.Mark(e), s.Diameter, RebarShapes.Def(s.Shape).Label, Rebars.Pieces(e), RebarShapes.Length(s)));
        }
        foreach (var e in m.OfClass(Rebars.RunClass))
        {
            if (e.Bool("excludeSchedule")) continue;
            var s = Rebars.SpecOf(e);
            var (_, positions, _, _) = Rebars.RunOf(e, s);
            o.Add(new Entry(Rebars.Mark(e), s.Diameter, RebarShapes.Def(s.Shape).Label, positions.Count * Rebars.Count(e), RebarShapes.Length(s)));
        }
        return o;
    }

    /// <summary>The schedule's rows by mark (the count summed; a mark whose bars differ in diameter or length is noted) and the totals by diameter.</summary>
    public static List<IReadOnlyList<string>> Rows(IEnumerable<Entry> entries, out double totalMass)
    {
        var rows = new List<IReadOnlyList<string>>();
        var byDia = new SortedDictionary<int, (double Length, double Mass)>();
        totalMass = 0;
        foreach (var g in entries.GroupBy(x => x.Mark).OrderBy(g => g.Key))
        {
            var first = g.First();
            var count = g.Sum(x => x.Count);
            var conflict = g.Any(x => Math.Abs(x.Diameter - first.Diameter) > 1e-9 || Math.Abs(x.Length - first.Length) > 0.0005);
            var total = g.Sum(x => x.Count * x.Length);
            var mass = g.Sum(x => x.Count * x.Length * RebarShapes.MassPerMetre(x.Diameter));
            rows.Add([g.Key.ToString(CultureInfo.InvariantCulture), N(first.Diameter * 1000, 3), first.Shape, count.ToString(CultureInfo.InvariantCulture), N(first.Length), N(total), N(mass), conflict ? "Ø or length differs" : ""]);
            var d = (int)Math.Round(first.Diameter * 1000);
            byDia[d] = byDia.TryGetValue(d, out var acc) ? (acc.Length + total, acc.Mass + mass) : (total, mass);
            totalMass += mass;
        }
        foreach (var (d, (length, mass)) in byDia) rows.Add(["", d.ToString(CultureInfo.InvariantCulture), "Total", "", "", N(length), N(mass), ""]);
        if (rows.Count > 0) rows.Add(["Total", "", "", "", "", "", N(totalMass), ""]);
        return rows;
    }

    public ScheduleGrid Grid(IQueryContext q) => new(Label, Columns, Rows(Entries(q.Model), out _));
}

/// <summary>
/// Rebar 2D (docs/plan-rebar.md, decision 209): bars drawn on any view as parametric shapes bent at the Eurocode radius,
/// their marks and labels, distributions along a path with zones, and the bar bending schedule. A bar is an annotation of
/// its view (its coordinates the view's, metres); it is not a body. The 3D bars configured on the structural types are the
/// premium module's.
/// </summary>
public sealed class RebarModule : ILapinModule, IViewAnnotations, IElementPlacement
{
    public string Id => "rebar";
    public string Name => "Rebar 2D";

    public void Register(IModuleRegistry r)
    {
        r.Class(new(Rebars.Class, "Bar", [
            new("mark", "Mark", "number", Min: 1), new("count", "Count", "number", Min: 1), new("diameter", "Diameter", "length", Min: 0.004, Max: 0.05),
            new("steel", "Steel", "choice", Choices: RebarShapes.Steels), new("view", "Seen", "choice", Choices: Rebars.Views),
            new("rotation", "Rotation (°)", "angle"), new("excludeSchedule", "Not in the schedule", "bool"), new("layer", "Layer", "text"),
        ], "#0f766e", Kind: "annotation"));
        r.Class(new(Rebars.RunClass, "Bar distribution", [
            new("mark", "Mark", "number", Min: 1), new("count", "Bars at each position", "number", Min: 1), new("diameter", "Diameter", "length", Min: 0.004, Max: 0.05),
            new("step", "Step", "length", Min: 0.01), new("size", "Drawn length", "length", Min: 0.01), new("show", "Shown", "choice", Choices: Rebars.Shows),
            new("steel", "Steel", "choice", Choices: RebarShapes.Steels), new("excludeSchedule", "Not in the schedule", "bool"),
        ], "#1d4ed8", Kind: "annotation"));

        r.Catalog("shapes", new JsonObject
        {
            ["shapes"] = new JsonArray(RebarShapes.Catalogue.Select(s => (JsonNode)new JsonObject
            {
                ["code"] = s.Code, ["label"] = s.Label, ["layer"] = s.Layer, ["diameter"] = s.DefaultDiameter, ["freeVertices"] = s.FreeVertices, ["hooks"] = s.BornHooks != null,
                ["params"] = new JsonArray(s.Params.Select(p => (JsonNode)new JsonObject { ["key"] = p.Key, ["label"] = p.Label, ["default"] = p.Default, ["min"] = p.Min }).ToArray()),
            }).ToArray()),
            ["diameters"] = new JsonArray(RebarShapes.Diameters.Select(d => (JsonNode)d).ToArray()),
            ["steels"] = new JsonArray(RebarShapes.Steels.Select(s => (JsonNode)s).ToArray()),
            ["views"] = new JsonArray(Rebars.Views.Select(s => (JsonNode)s).ToArray()),
            ["settings"] = new RebarSettings().ToJson(),
        });

        r.Service<IViewAnnotations>(this);
        r.Service<IElementPlacement>(this);
        r.Service<IScheduleSource>(new RebarSchedule());

        // A bar on a view: {viewId, shape, at: [x, y], params?: {key: m}, vertices?, diameter?, mark?, count?, rotation?, mirrorX?, mirrorY?, hooks?: {start?: {angle, length, flip}, end?}, view?, steel?, layer?, array?, excludeSchedule?}.
        r.Command("add", "Bar", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("A bar is drawn on a view.");
            var shape = (string?)a["shape"] ?? "straight";
            var def = RebarShapes.Def(shape);
            var attrs = new JsonObject { ["viewId"] = view.Id, ["shape"] = shape, ["at"] = Rebars.J(Args.Point(a["at"], "the bar's point")), ["diameter"] = def.DefaultDiameter, ["mark"] = Rebars.NextMark(ctx.Model), ["count"] = 1 };
            if (def.FreeVertices && a["vertices"] == null) attrs["vertices"] = new JsonArray(def.Vertices(new Dictionary<string, double>()).Select(p => (JsonNode)Rebars.J(p)).ToArray());
            Write(attrs, a, run: false);
            var e = new Entity(ctx.NewId(), Rebars.Class, $"{def.Label} {Rebars.Mark(new Entity("", Rebars.Class, "", attrs))}", attrs);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });
        r.Command("update", "Bar", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"], Rebars.Class);
            var attrs = e.CloneAttrs();
            Write(attrs, a, run: false);
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });
        // A grip dragged: {id, key, point: [x, y]} (the view's coordinates); the parameter it drives follows, or the polyline's vertex.
        r.Command("grip", "Bar grip", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"], Rebars.Class);
            var s = Rebars.SpecOf(e);
            var at = Rebars.At(e);
            var key = (string?)a["key"] ?? "";
            var p = Args.Point(a["point"], "the grip's point");
            var attrs = e.CloneAttrs();
            if (key == "at") attrs["at"] = Rebars.J(p);
            else if (key == "label") attrs["label"] = new JsonObject { ["at"] = Rebars.J(p) };
            else
            {
                var next = RebarShapes.Apply(s, key, RebarShapes.Local(s, at, p));
                if (next.Vertices != null && RebarShapes.Def(s.Shape).FreeVertices) attrs["vertices"] = new JsonArray(next.Vertices.Select(q => (JsonNode)Rebars.J(q)).ToArray());
                else attrs["params"] = new JsonObject(next.Params.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)));
            }
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });
        // The same bar seen another way ({id, view, at}): a copy in that view, left out of the schedule, remembering its original.
        r.Command("duplicateAs", "Bar in another view", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"], Rebars.Class);
            var view = (string?)a["view"] ?? "section";
            if (!Rebars.Views.Contains(view)) throw new CommandException($"A bar is seen from the front, the top, the side or in section, not „{view}”.");
            var attrs = e.CloneAttrs();
            attrs["view"] = view;
            attrs["at"] = Rebars.J(Args.Point(a["at"], "where the copy goes"));
            attrs["excludeSchedule"] = true;
            attrs["original"] = e.Id;
            attrs.Remove("label");
            attrs.Remove("array");
            if ((string?)a["viewId"] is { Length: > 0 } vid) attrs["viewId"] = (Views.Find(ctx.Model, vid) ?? throw new CommandException("There is no such view.")).Id;
            var copy = new Entity(ctx.NewId(), Rebars.Class, e.Name, attrs);
            ctx.Add(copy);
            return new JsonObject { ["id"] = copy.Id };
        });
        // Bars distributed along a path: {viewId, start, end, rise?, shape?, params?, diameter?, step?, zones?: [{length, step}], count?, mark?, size?, side?, show?, offset?, hooks?}.
        r.Command("addRun", "Bar distribution", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("A distribution is drawn on a view.");
            var shape = (string?)a["shape"] ?? "stirrup";
            var def = RebarShapes.Def(shape);
            var start = Args.Point(a["start"], "the path's start");
            var end = Args.Point(a["end"], "the path's end");
            if ((end - start).Length < 1e-6) throw new CommandException("A distribution needs two different points.");
            var attrs = new JsonObject { ["viewId"] = view.Id, ["shape"] = shape, ["start"] = Rebars.J(start), ["end"] = Rebars.J(end), ["diameter"] = def.DefaultDiameter, ["mark"] = Rebars.NextMark(ctx.Model), ["count"] = 1, ["step"] = 0.15 };
            Write(attrs, a, run: true);
            var e = new Entity(ctx.NewId(), Rebars.RunClass, $"{def.Label}s {Rebars.Mark(new Entity("", Rebars.RunClass, "", attrs))}", attrs);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });
        r.Command("updateRun", "Bar distribution", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"], Rebars.RunClass);
            var attrs = e.CloneAttrs();
            Write(attrs, a, run: true);
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });
        // The project's rebar drawing settings: {colorByDiameter?, textHeight?, balloon?, steel?, segmentLengths?, cover?}.
        r.Command("setSettings", "Rebar settings", (ctx, a) =>
        {
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var now = RebarSettings.Of(ctx.Model);
            bool B(string k, bool d) => a[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : d;
            double Nn(string k, double d, double lo, double hi) { var v = Json.Number(a[k]) ?? d; if (v < lo || v > hi) throw new CommandException($"{k} is between {lo} and {hi}."); return v; }
            var steel = (string?)a["steel"] ?? now.Steel;
            var next = new RebarSettings(B("colorByDiameter", now.ColorByDiameter), Nn("textHeight", now.TextHeight, 1, 10), Nn("balloon", now.Balloon, 3, 20), steel.Trim().Length > 0 ? steel.Trim() : now.Steel, B("segmentLengths", now.SegmentLengths), Nn("cover", now.Cover, 0, 0.2));
            var attrs = project.CloneAttrs();
            attrs[RebarSettings.Key] = next.ToJson();
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });

        // A section across an element where it was clicked, inset beside the click in the same view (docs/plan-rebar.md):
        // {viewId (the host), id (the element), point: [x, y] (host coordinates), at?: [x, y] (where the inset goes), flip?, margin?, inset?: false (the section alone)}.
        r.Command("sectionHere", "Section here", (ctx, a) =>
        {
            var host = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("The section is inset in a view.");
            var e = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Pick the element to cut across.");
            var p = Args.Point(a["point"], "where to cut");
            // The click back in the model: a plan's point is the model's; a section's or elevation's goes through its frame.
            Vec2 xy = p;
            double? z = null;
            if (host.Str("kind") is not ("plan" or null))
            {
                var spec = Views.Generate(ctx.Query, host).Spec ?? throw new CommandException("The view cannot be drawn, so the click cannot be placed.");
                var world = spec.Frame.Origin + spec.Frame.Right * p.X + spec.Frame.Up * p.Y;
                xy = new Vec2(world.X, world.Y);
                z = world.Z;
            }
            double? station = null;
            if (e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray) station = PathCurve.Of(e).Project(xy);
            else if (z is { } zz) station = zz - (Spatial.StoreyOf(ctx.Model, e.Id)?.Num("elevation") ?? 0) - e.Num("baseOffset");
            var flip = a["flip"] is JsonValue fv && fv.TryGetValue<bool>(out var f) && f;
            var across = AcrossSections.Create(ctx, e.Id, station, Json.Number(a["margin"]), flip, host.Str("discipline"));
            if (a["inset"] is JsonValue iv && iv.TryGetValue<bool>(out var withInset) && !withInset) return new JsonObject { ["viewId"] = across.Id };
            // The inset to the right of the click, a window's width away, unless told where.
            var hostScale = host.NumOrNull("scale") ?? 100;
            var factor = hostScale / 20;
            var margin = Json.Number(a["margin"]) ?? AcrossSections.DefaultMargin;
            var at = a["at"] != null ? Args.Point(a["at"], "where the inset goes") : p + new Vec2(factor * (2 * margin + 0.5) + hostScale / 1000 * 15, 0);
            var inset = Insets.Create(ctx, host.Id, across.Id, at);
            return new JsonObject { ["viewId"] = across.Id, ["insetId"] = inset.Id };
        });

        // What a bar or a distribution is: its length, developed length, mass, label, positions.
        r.Query("describe", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") is { } x && x.Class is Rebars.Class or Rebars.RunClass ? x : throw new CommandException("Not a bar.");
            var s = Rebars.SpecOf(e);
            var length = RebarShapes.Length(s);
            var o = new JsonObject
            {
                ["length"] = Math.Round(length, 4), ["trueLength"] = Math.Round(RebarShapes.TrueLength(s), 4), ["massPerMetre"] = Math.Round(RebarShapes.MassPerMetre(s.Diameter), 4),
                ["bendRadius"] = RebarShapes.RadiusOf(s), ["mandrel"] = RebarShapes.Mandrel(s.Diameter),
            };
            var (start, end) = RebarShapes.HooksOf(s);
            if (start != null) o["hookStart"] = start.ToJson();
            if (end != null) o["hookEnd"] = end.ToJson();
            if (e.Class == Rebars.Class)
            {
                o["pieces"] = Rebars.Pieces(e);
                o["mass"] = Math.Round(length * RebarShapes.MassPerMetre(s.Diameter) * Rebars.Pieces(e), 3);
                o["label"] = Rebars.Label(e, s);
                var at = Rebars.At(e);
                o["grips"] = new JsonArray(RebarShapes.Grips(s).Select(g => (JsonNode)new JsonObject { ["key"] = g.Key, ["at"] = Rebars.J(RebarShapes.Place(s, at, g.At)) }).ToArray());
            }
            else
            {
                var (path, positions, step, label) = Rebars.RunOf(e, s);
                o["positions"] = positions.Count;
                o["pathLength"] = Math.Round(path.Length, 4);
                o["step"] = step;
                o["label"] = label;
                o["mass"] = Math.Round(length * RebarShapes.MassPerMetre(s.Diameter) * positions.Count * Rebars.Count(e), 3);
            }
            return o;
        });
        r.Query("settings", (q, _) => RebarSettings.Of(q.Model).ToJson());
    }

    static Entity Require(IModelView m, string? id, string cls) => m.Get(id ?? "") is { } e && e.Class == cls ? e : throw new CommandException(cls == Rebars.Class ? "Not a bar." : "Not a bar distribution.");

    /// <summary>The attributes a command may set on a bar or a distribution, checked.</summary>
    static void Write(JsonObject attrs, JsonObject a, bool run)
    {
        if ((string?)a["shape"] is { } shape) { RebarShapes.Def(shape); attrs["shape"] = shape; }
        var def = RebarShapes.Def((string?)attrs["shape"] ?? "straight");
        if (a["params"] is JsonObject po)
        {
            var p = attrs["params"] as JsonObject ?? new JsonObject();
            foreach (var (k, v) in po)
            {
                var d = def.Params.FirstOrDefault(x => x.Key == k) ?? throw new CommandException($"„{k}” is not a parameter of a {def.Label.ToLowerInvariant()} ({string.Join(", ", def.Params.Select(x => x.Key))}).");
                var val = Json.Number(v) ?? throw new CommandException($"{k} is a length.");
                if (val < d.Min - 1e-9) throw new CommandException($"{d.Label} is {Math.Round(d.Min * 1000)} mm at least.");
                p[k] = Math.Round(val, 6);
            }
            attrs["params"] = p;
        }
        if (a["vertices"] is JsonArray va)
        {
            if (!def.FreeVertices) throw new CommandException("Only a polyline bar has its own vertices.");
            var pts = va.Select(n => Args.Point(n, "a vertex")).ToList();
            if (pts.Count < 2) throw new CommandException("A polyline bar has two vertices at least.");
            attrs["vertices"] = new JsonArray(pts.Select(p => (JsonNode)Rebars.J(p)).ToArray());
        }
        if (a.ContainsKey("diameter"))
        {
            var d = Json.Number(a["diameter"]) ?? throw new CommandException("The diameter is a length.");
            if (d < 0.004 || d > 0.05) throw new CommandException("A bar is between 4 and 50 mm thick.");
            attrs["diameter"] = Math.Round(d, 6);
        }
        foreach (var k in new[] { "mark", "count" })
            if (a.ContainsKey(k))
            {
                var n = Json.Number(a[k]) ?? throw new CommandException($"{k} is a number.");
                if (n < 1) throw new CommandException($"{k} is 1 at least.");
                attrs[k] = Math.Round(n);
            }
        if (a.ContainsKey("steel")) { if ((string?)a["steel"] is { Length: > 0 } st) attrs["steel"] = st; else attrs.Remove("steel"); }
        if (a.ContainsKey("layer")) { if ((string?)a["layer"] is { Length: > 0 } la) attrs["layer"] = la; else attrs.Remove("layer"); }
        if (a.ContainsKey("excludeSchedule")) { if (a["excludeSchedule"] is JsonValue ev && ev.TryGetValue<bool>(out var ex) && ex) attrs["excludeSchedule"] = true; else attrs.Remove("excludeSchedule"); }
        if (a.ContainsKey("hooks"))
        {
            if (a["hooks"] is JsonObject ho)
            {
                var hooks = new JsonObject();
                foreach (var end in new[] { "start", "end" })
                    if (ho[end] is JsonObject h)
                    {
                        var angle = Json.Number(h["angle"]) ?? 90;
                        if (angle is < 10 or > 180) throw new CommandException("A hook turns between 10° and 180°.");
                        var length = Json.Number(h["length"]) ?? RebarShapes.HookLength(Json.Number(attrs["diameter"]) ?? def.DefaultDiameter, angle);
                        hooks[end] = new Hook(angle, Math.Max(0, length), h["flip"] is JsonValue fv && fv.TryGetValue<bool>(out var f) && f).ToJson();
                    }
                    else if (ho.ContainsKey(end) && ho[end] == null) hooks[end] = new JsonObject { ["angle"] = 0 }; // none, over the shape's own
                if (hooks.Count > 0) attrs["hooks"] = hooks; else attrs.Remove("hooks");
            }
            else attrs.Remove("hooks");
        }
        if (a.ContainsKey("label"))
        {
            if (a["label"] is JsonObject lo && lo["at"] is JsonArray) attrs["label"] = new JsonObject { ["at"] = Rebars.J(Args.Point(lo["at"], "the label's point")) };
            else attrs.Remove("label");
        }
        if (!run)
        {
            if (a.ContainsKey("at")) attrs["at"] = Rebars.J(Args.Point(a["at"], "the bar's point"));
            if (a.ContainsKey("rotation")) attrs["rotation"] = Json.Number(a["rotation"]) ?? 0;
            foreach (var k in new[] { "mirrorX", "mirrorY" })
                if (a.ContainsKey(k)) { if (a[k] is JsonValue mv && mv.TryGetValue<bool>(out var mb) && mb) attrs[k] = true; else attrs.Remove(k); }
            if (a.ContainsKey("view"))
            {
                var v = (string?)a["view"] ?? "front";
                if (!Rebars.Views.Contains(v)) throw new CommandException($"A bar is seen from the front, the top, the side or in section, not „{v}”.");
                if (v == "front") attrs.Remove("view"); else attrs["view"] = v;
            }
            if (a.ContainsKey("array"))
            {
                if (a["array"] is JsonObject ao && (Json.Number(ao["nx"]) ?? 1) * (Json.Number(ao["ny"]) ?? 1) > 1)
                    attrs["array"] = new JsonObject { ["nx"] = Math.Max(1, Math.Round(Json.Number(ao["nx"]) ?? 1)), ["dx"] = Json.Number(ao["dx"]) ?? 0, ["ny"] = Math.Max(1, Math.Round(Json.Number(ao["ny"]) ?? 1)), ["dy"] = Json.Number(ao["dy"]) ?? 0 };
                else attrs.Remove("array");
            }
            if (a.ContainsKey("bendRadius")) { if (Json.Number(a["bendRadius"]) is { } br && br >= 0) attrs["bendRadius"] = br; else attrs.Remove("bendRadius"); }
        }
        else
        {
            if (a.ContainsKey("start")) attrs["start"] = Rebars.J(Args.Point(a["start"], "the path's start"));
            if (a.ContainsKey("end")) attrs["end"] = Rebars.J(Args.Point(a["end"], "the path's end"));
            if (a.ContainsKey("rise")) { if (Json.Number(a["rise"]) is { } rise && Math.Abs(rise) > 1e-9) attrs["rise"] = rise; else attrs.Remove("rise"); }
            if (a.ContainsKey("step"))
            {
                var st = Json.Number(a["step"]) ?? throw new CommandException("The step is a length.");
                if (st < 0.01) throw new CommandException("The step is 10 mm at least.");
                attrs["step"] = Math.Round(st, 6);
            }
            if (a.ContainsKey("zones"))
            {
                var zones = (a["zones"] as JsonArray)?.Select(Runs.Zone.From).OfType<Runs.Zone>().ToList() ?? [];
                if (zones.Count > 0) attrs["zones"] = new JsonArray(zones.Select(z => (JsonNode)z.ToJson()).ToArray()); else attrs.Remove("zones");
            }
            foreach (var k in new[] { "size", "offset", "side" })
                if (a.ContainsKey(k)) { if (Json.Number(a[k]) is { } v && (k != "size" || v > 0)) attrs[k] = Math.Round(v, 6); else attrs.Remove(k); }
            if (a.ContainsKey("show"))
            {
                var sh = (string?)a["show"] ?? "all";
                if (!Rebars.Shows.Contains(sh)) throw new CommandException("A distribution shows all its bars, its ends, or one.");
                if (sh == "all") attrs.Remove("show"); else attrs["show"] = sh;
            }
        }
    }

    // ─── Drawing ───────────────────────────────────────────────────────────

    public IEnumerable<ViewMark> Marks(ViewContext c)
    {
        var st = RebarSettings.Of(c.Model);
        var hidden = (c.View.Attrs["hiddenLayers"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        foreach (var e in Rebars.On(c.Model, c.View.Id, Rebars.Class))
            if (BarMark(c, st, e, hidden) is { } mk) yield return mk;
        foreach (var e in Rebars.On(c.Model, c.View.Id, Rebars.RunClass))
            if (RunMark(c, st, e, hidden) is { } mk) yield return mk;
    }

    static double Width(RebarSpec s, ViewContext c) => Math.Max(0.25, s.Diameter * 1000 / c.Scale);
    static string Color(RebarSettings st, RebarSpec s) => st.ColorByDiameter ? RebarShapes.ColorOf(s.Diameter) : "#000000";

    static ViewMark? BarMark(ViewContext c, RebarSettings st, Entity e, HashSet<string> hidden)
    {
        var s = Rebars.SpecOf(e);
        var layer = e.Str("layer") ?? RebarShapes.Def(s.Shape).Layer;
        if (hidden.Contains(layer)) return null;
        var at = Rebars.At(e);
        var mk = new ViewMark(e.Id);
        var color = Color(st, s);
        var width = Width(s, c);
        var view = s.Shape == "dot" ? "section" : e.Str("view") ?? "front";
        var (nx, dx, ny, dy) = Rebars.Array(e);
        var chain = RebarShapes.Chain(RebarShapes.Segments(s).Select(x => RebarShapes.Place(s, at, x)).ToList());
        var ext = RebarShapes.Extent(s);
        var nominal = RebarShapes.Nominal(s).Select(p => RebarShapes.Place(s, at, p)).ToList();
        for (var ix = 0; ix < nx; ix++)
            for (var iy = 0; iy < ny; iy++)
            {
                var off = new Vec2(ix * dx, iy * dy);
                switch (view)
                {
                    case "top":
                        mk.Line(RebarShapes.Place(s, at, new Vec2(ext.Min.X, 0)) + off, RebarShapes.Place(s, at, new Vec2(ext.Max.X, 0)) + off, layer, width, color);
                        break;
                    case "side":
                        mk.Line(RebarShapes.Place(s, at, new Vec2(0, ext.Min.Y)) + off, RebarShapes.Place(s, at, new Vec2(0, ext.Max.Y)) + off, layer, width, color);
                        break;
                    case "section":
                    {
                        var circle = ViewMark.Circle(at + off, Math.Max(s.Diameter / 2, 0.6 * c.Mm), 24);
                        mk.Fills.Add(([circle], color));
                        mk.Polyline(circle, true, layer, 0.18, color);
                        break;
                    }
                    default:
                        if (chain.Count >= 2) mk.Polyline(chain.Select(p => p + off).ToList(), false, layer, width, color);
                        break;
                }
            }
        mk.Snaps.AddRange(nominal.Count > 0 ? nominal : [at]);
        // Segment lengths written along the bar (mm), when the project asks.
        if (st.SegmentLengths && view == "front")
            for (var i = 0; i + 1 < nominal.Count; i++)
            {
                var (p, q) = (nominal[i], nominal[i + 1]);
                var u = (q - p).Normalized();
                var angle = Math.Atan2(u.Y, u.X) * 180 / Math.PI;
                var n = u.Perp;
                if (angle > 90 || angle <= -90) { angle += 180; n = -n; }
                mk.Text(new PenText((p + q) * 0.5 + n * (1.5 * c.Mm), RebarShapes.Mm((q - p).Length), st.TextHeight * 0.8, angle, "middle", "bottom"), color);
            }
        // The mark's balloon with its leader, and what the bar is.
        var centre = RebarShapes.Place(s, at, new Vec2((ext.Min.X + ext.Max.X) / 2, ext.Max.Y));
        var lab = Rebars.LabelAt(e) ?? centre + new Vec2(0, 10 * c.Mm);
        var r = st.Balloon / 2 * c.Mm;
        var balloon = ViewMark.Circle(lab, r, 32);
        mk.Polyline(balloon, true, RebarShapes.AnnotationLayer, 0.35, color);
        mk.Text(new PenText(lab, Rebars.Mark(e).ToString(CultureInfo.InvariantCulture), st.TextHeight, 0, "middle", "middle", true), color);
        var target = Nearest(chain, lab, at);
        if ((target - lab).Length > r + 1e-6) mk.Line(lab + (target - lab).Normalized() * r, target, RebarShapes.AnnotationLayer, 0.25, color);
        mk.Text(new PenText(lab + new Vec2(r + 1.0 * c.Mm, 0), Rebars.Label(e, s), st.TextHeight, 0, "start", "middle"), color);
        mk.Snaps.Add(lab);
        // What the interface drags: the shape's grips where they stand now, its point and its label.
        var grips = new JsonArray(RebarShapes.Grips(s).Select(g => (JsonNode)new JsonObject { ["key"] = g.Key, ["at"] = Rebars.J(RebarShapes.Place(s, at, g.At)) }).ToArray());
        mk.Data = new JsonObject { ["grips"] = grips, ["at"] = Rebars.J(at), ["label"] = Rebars.J(lab) };
        return mk;
    }

    /// <summary>The point of the drawn chain nearest to <paramref name="p"/> (on its segments, not only at its vertices); <paramref name="fallback"/> for an empty chain.</summary>
    static Vec2 Nearest(IReadOnlyList<Vec2> chain, Vec2 p, Vec2 fallback)
    {
        if (chain.Count == 0) return fallback;
        var best = chain[0];
        var bestD = (best - p).Length;
        for (var i = 0; i + 1 < chain.Count; i++)
        {
            var (a, b) = (chain[i], chain[i + 1]);
            var ab = b - a;
            var l2 = Vec2.Dot(ab, ab);
            var t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p - a, ab) / l2, 0, 1);
            var q = a + ab * t;
            var d = (q - p).Length;
            if (d < bestD) { bestD = d; best = q; }
        }
        return best;
    }

    static ViewMark? RunMark(ViewContext c, RebarSettings st, Entity e, HashSet<string> hidden)
    {
        var s = Rebars.SpecOf(e);
        var layer = e.Str("layer") ?? RebarShapes.Def(s.Shape).Layer;
        if (hidden.Contains(layer)) return null;
        var (path, positions, _, label) = Rebars.RunOf(e, s);
        if (path.Length < 1e-6) return null;
        var mk = new ViewMark(e.Id);
        var color = Color(st, s);
        var width = Width(s, c);
        var ext = RebarShapes.Extent(s);
        // Each bar seen across the path: a stirrup in a beam's section is a line as tall as it is (b), in plan as wide (a).
        var size = e.NumOrNull("size") ?? (s.Shape == "stirrup" ? s["b"] : Math.Max(ext.Max.Y - ext.Min.Y, ext.Max.X - ext.Min.X));
        var side = e.Num("side");
        var show = e.Str("show") ?? "all";
        List<double> drawn = positions;
        if (show == "one") drawn = [positions[positions.Count / 2]];
        else if (show == "ends" && positions.Count > 3) drawn = [positions[0], positions[positions.Count / 2], positions[^1]];
        foreach (var st0 in drawn)
        {
            var (p, n) = (path.PointAt(st0), path.TangentAt(st0).Perp);
            mk.Line(p + n * (side - size / 2), p + n * (side + size / 2), layer, width, color);
        }
        // Fewer drawn than there are: the path dashed through them says the rest.
        if (drawn.Count < positions.Count) mk.Polyline(path.Line(0, path.Length, side), false, layer, 0.25, color, [3, 1.5]);
        // The dimension line beside the bars, with the ticks at its ends and the label over it.
        var offset = e.NumOrNull("offset") ?? -(size / 2 + 6 * c.Mm);
        var line = path.Line(0, path.Length, side + offset);
        mk.Polyline(line, false, RebarShapes.AnnotationLayer, 0.25, color);
        foreach (var st1 in new[] { 0.0, path.Length })
        {
            var (p, n) = (path.At(st1, side + offset), path.TangentAt(st1).Perp);
            mk.Line(p - n * (1.5 * c.Mm), p + n * (1.5 * c.Mm), RebarShapes.AnnotationLayer, 0.35, color);
        }
        var mid = path.Length / 2;
        var tangent = path.TangentAt(mid);
        var angle = Math.Atan2(tangent.Y, tangent.X) * 180 / Math.PI;
        var up = tangent.Perp;
        if (angle > 90 || angle <= -90) { angle += 180; up = -up; }
        var textAt = path.At(mid, side + offset) + up * (1.0 * c.Mm);
        mk.Text(new PenText(textAt, label, st.TextHeight, angle, "middle", "bottom"), color);
        // The mark's balloon at the label, or where it was put.
        var lab = Rebars.LabelAt(e) ?? path.At(mid, side + offset) - up * (st.Balloon * c.Mm);
        var r = st.Balloon / 2 * c.Mm;
        mk.Polyline(ViewMark.Circle(lab, r, 32), true, RebarShapes.AnnotationLayer, 0.35, color);
        mk.Text(new PenText(lab, Rebars.Mark(e).ToString(CultureInfo.InvariantCulture), st.TextHeight, 0, "middle", "middle", true), color);
        mk.Text(new PenText(lab + new Vec2(r + 1.0 * c.Mm, 0), $"{RebarShapes.Def(s.Shape).Label} {RebarShapes.Dia(s.Diameter)} L={RebarShapes.Mm(RebarShapes.Length(s))}", st.TextHeight, 0, "start", "middle"), color);
        mk.Snaps.AddRange(positions.Select(p0 => path.PointAt(p0)));
        mk.Snaps.Add(lab);
        mk.Data = new JsonObject { ["positions"] = positions.Count, ["label"] = Rebars.J(lab) };
        return mk;
    }

    // ─── Placement ─────────────────────────────────────────────────────────

    public bool Handles(Entity e) => e.Class is Rebars.Class or Rebars.RunClass;

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving) => ctx.Replace(e.With(attrs: Carried(e, t)));

    public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        var attrs = Carried(e, t);
        var copy = new Entity(ctx.NewId(), e.Class, e.Name, attrs);
        ctx.Add(copy);
        return copy.Id;
    }

    /// <summary>The bar's attributes carried by a plan transformation: its point, turn and mirror (a mirror flips its y and reflects its turn), a run's ends and rise, the label.</summary>
    static JsonObject Carried(Entity e, Placement2D t)
    {
        var attrs = e.CloneAttrs();
        if (e.Class == Rebars.Class)
        {
            attrs["at"] = Rebars.J(t.Point(Rebars.At(e)));
            var rot = e.Num("rotation");
            if (t.Mirrored)
            {
                attrs["rotation"] = Math.Round(2 * t.Axis * 180 / Math.PI - rot, 6);
                if (e.Bool("mirrorY")) attrs.Remove("mirrorY"); else attrs["mirrorY"] = true;
            }
            else attrs["rotation"] = Math.Round(rot + t.Turn * 180 / Math.PI, 6);
        }
        else
        {
            attrs["start"] = Rebars.J(t.Point(Rebars.P(e.Attrs["start"])));
            attrs["end"] = Rebars.J(t.Point(Rebars.P(e.Attrs["end"])));
            if (t.Mirrored && e.NumOrNull("rise") is { } rise) attrs["rise"] = -rise;
        }
        if (Rebars.LabelAt(e) is { } lab) attrs["label"] = new JsonObject { ["at"] = Rebars.J(t.Point(lab)) };
        return attrs;
    }
}
