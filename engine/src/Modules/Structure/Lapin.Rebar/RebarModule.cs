using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// The project's rebar drawing settings (<c>rebarSettings</c>): colours by diameter, the text and balloon sizes (paper mm), the steel,
/// segment lengths written on the bars, the cover, the labels' styles by kind of group (partial, over <see cref="LabelStyle.Default"/>),
/// the stock length a bar is delivered in (12 m, or 6 m) and the lap a split bar overlaps by (null: 50Ø; decision 227).
/// </summary>
public sealed record RebarSettings(bool ColorByDiameter = true, double TextHeight = 2.5, double Balloon = 7.5, string Steel = RebarShapes.DefaultSteel, bool SegmentLengths = false, double Cover = 0.025,
    IReadOnlyDictionary<string, JsonObject>? Labels = null, double StockLength = 12, double? LapLength = null)
{
    public const string Key = "rebarSettings";
    public const double DefaultStock = 12;
    /// <summary>The lap a bar overlaps by when the project names none: 50Ø.</summary>
    public const double LapDiameters = 50;

    public IReadOnlyDictionary<string, JsonObject> Labels { get; init; } = Labels ?? new Dictionary<string, JsonObject>();

    /// <summary>The lap (m) for a bar of this diameter: the project's, or 50Ø.</summary>
    public double LapOf(double diameter) => LapLength ?? LapDiameters * diameter;

    public static RebarSettings Of(IModelView m)
    {
        var o = Spatial.Project(m)?.Attrs[Key] as JsonObject;
        if (o == null) return new RebarSettings();
        var labels = new Dictionary<string, JsonObject>();
        if (o["labels"] is JsonObject lo)
            foreach (var (k, v) in lo) if (v is JsonObject so && LabelStyle.Kinds.Contains(k)) labels[k] = so;
        return new RebarSettings(
            !(o["colorByDiameter"] is JsonValue cv && cv.TryGetValue<bool>(out var cb) && !cb),
            Json.Number(o["textHeight"]) ?? 2.5, Json.Number(o["balloon"]) ?? 7.5, (string?)o["steel"] ?? RebarShapes.DefaultSteel,
            o["segmentLengths"] is JsonValue sv && sv.TryGetValue<bool>(out var sl) && sl, Json.Number(o["cover"]) ?? 0.025, labels,
            Json.Number(o["stockLength"]) is { } stock && stock > 0 ? stock : DefaultStock, Json.Number(o["lapLength"]) is { } lap && lap > 0 ? lap : null);
    }

    public JsonObject ToJson() => new()
    {
        ["colorByDiameter"] = ColorByDiameter, ["textHeight"] = TextHeight, ["balloon"] = Balloon, ["steel"] = Steel, ["segmentLengths"] = SegmentLengths, ["cover"] = Cover,
        ["labels"] = new JsonObject(Labels.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value.DeepClone()))),
        ["stockLength"] = StockLength, ["lapLength"] = LapLength,
    };

    /// <summary>The settings as the interface reads them: each kind's label style resolved whole.</summary>
    public JsonObject Resolved()
    {
        var o = ToJson();
        o["labels"] = new JsonObject(LabelStyle.Kinds.Select(k => KeyValuePair.Create(k, (JsonNode?)LabelStyle.Resolve(k, this).ToJson())));
        return o;
    }
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
    /// <summary>Where the user put a bar's or a distribution's label (<c>label: {at, elbow, offset}</c>); null when nowhere.</summary>
    public static LabelPlace? LabelPlaceOf(Entity e) => e.Attrs[LabelStyle.Key] is JsonObject o && LabelPlace.From(o) is { IsEmpty: false } p ? p : null;

    /// <summary>A bar's array in its view: how many copies and the step along x and y (one copy: none).</summary>
    public static (int Nx, double Dx, int Ny, double Dy) Array(Entity e)
    {
        if (e.Attrs["array"] is not JsonObject o) return (1, 0, 1, 0);
        return ((int)Math.Max(1, Json.Number(o["nx"]) ?? 1), Json.Number(o["dx"]) ?? 0, (int)Math.Max(1, Json.Number(o["ny"]) ?? 1), Json.Number(o["dy"]) ?? 0);
    }

    public static int Pieces(Entity e) { var (nx, _, ny, _) = Array(e); return Count(e) * nx * ny * (Rebar3D.Across.Of(e)?.Count ?? 1); }

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
    public static int NextMark(IModelView m) => 1 + Math.Max(m.OfClass(Class).Concat(m.OfClass(RunClass)).Select(Mark).DefaultIfEmpty(0).Max(), Math.Max(TypeStirrups.HighestMark(m), ColumnBars.HighestMark(m)));
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
        o.AddRange(TypeStirrups.Entries(m));
        o.AddRange(ColumnBars.Entries(m));
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
            new("lapOffset", "Drawn beside its lap", "length"),
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
            ["settings"] = new RebarSettings().Resolved(),
            ["labelTokens"] = new JsonArray(LabelStyle.Tokens.Select(s => (JsonNode)s).ToArray()),
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
            // Drawn inside an element's cut (a beam in its longitudinal section): the bar lies in that element, one across its width unless told more.
            if (a["host"] == null && Rebar3D.HostAt(ctx, view, Rebars.At(new Entity("", Rebars.Class, "", attrs))) is { } host)
            {
                attrs["host"] = host;
                if (attrs["across"] == null) attrs["across"] = new Rebar3D.Across(1, "cover", 0.05).ToJson();
            }
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
        // The label's grips go through update/updateRun ({label: {at, elbow, offset}}), a generated label's through placeLabel.
        r.Command("grip", "Bar grip", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"], Rebars.Class);
            var s = Rebars.SpecOf(e);
            var at = Rebars.At(e);
            var key = (string?)a["key"] ?? "";
            var p = Args.Point(a["point"], "the grip's point");
            var attrs = e.CloneAttrs();
            if (key == "at") attrs["at"] = Rebars.J(p);
            else if (RebarAnchors.IsAnchor(key))
            {
                // An anchor (decision 226): the formwork's face dragged onto the cut; the bar lands the cover inside it.
                var (cover, _) = RebarAnchors.CoverOf(ctx.Model, e, RebarSettings.Of(ctx.Model));
                var (nextAt, next) = RebarAnchors.Drag(s, at, cover, key, p);
                attrs["at"] = Rebars.J(nextAt);
                attrs["params"] = new JsonObject(next.Params.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)));
            }
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
        // The project's rebar drawing settings: {colorByDiameter?, textHeight?, balloon?, steel?, segmentLengths?, cover?, labels?: {bars?, stirrups?, columnBars?, runs?: a style patch | null},
        // stockLength?: m (1..24), lapLength?: m | null (null: 50Ø)}.
        r.Command("setSettings", "Rebar settings", (ctx, a) =>
        {
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var now = RebarSettings.Of(ctx.Model);
            bool B(string k, bool d) => a[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : d;
            double Nn(string k, double d, double lo, double hi) { var v = Json.Number(a[k]) ?? d; if (v < lo || v > hi) throw new CommandException($"{k} is between {lo} and {hi}."); return v; }
            var steel = (string?)a["steel"] ?? now.Steel;
            var labels = new Dictionary<string, JsonObject>(now.Labels);
            if (a["labels"] is JsonObject lo)
                foreach (var (k, v) in lo)
                {
                    if (!LabelStyle.Kinds.Contains(k)) throw new CommandException($"Labels are styled for bars, stirrups, columnBars and runs, not „{k}”.");
                    if (LabelStyle.Patch(labels.GetValueOrDefault(k), v) is { } patched) labels[k] = patched; else labels.Remove(k);
                }
            var lap = now.LapLength;
            if (a.ContainsKey("lapLength"))
            {
                lap = Json.Number(a["lapLength"]);
                if (lap is { } l && l <= 0) lap = null;
                if (lap is { } l2 && l2 > 5) throw new CommandException("The lap length is 5 m at most.");
            }
            var next = new RebarSettings(B("colorByDiameter", now.ColorByDiameter), Nn("textHeight", now.TextHeight, 1, 10), Nn("balloon", now.Balloon, 3, 20), steel.Trim().Length > 0 ? steel.Trim() : now.Steel, B("segmentLengths", now.SegmentLengths), Nn("cover", now.Cover, 0, 0.2), labels,
                Nn("stockLength", now.StockLength, 1, 24), lap);
            var attrs = project.CloneAttrs();
            attrs[RebarSettings.Key] = next.ToJson();
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });
        // A generated group's label (a type's stirrups, a column's bars, a bar seen in another view) put where the user dragged it, on the view:
        // {viewId, owner, key, at?: [x, y] | null, elbow?: [x, y] | null, offset?: mm | null} (a bar's or a distribution's own label goes through update/updateRun).
        r.Command("placeLabel", "Place label", (ctx, a) => { RebarLabels.Place(ctx, a); return null; });

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
            var margin = Json.Number(a["margin"]) ?? AcrossSections.DefaultMargin;
            var at = a["at"] != null ? Args.Point(a["at"], "where the inset goes") : p + new Vec2(2 * margin + 0.5 + hostScale / 1000 * 15, 0);
            var inset = Insets.Create(ctx, host.Id, across.Id, at);
            return new JsonObject { ["viewId"] = across.Id, ["insetId"] = inset.Id };
        });

        // A longitudinal bar cut in two with a lap (decision 227): {id, station?: m from the bar's start, point?: [x, y] (the view's; the nearest point of the bar gives the station),
        // lap?: m (else the settings' lap length, 50Ø when none)}. The bar becomes the first piece, a new bar the second, the lap centred on the station → {first, second, lap, station}.
        r.Command("split", "Split bar", (ctx, a) => RebarSplit.Split(ctx, Require(ctx.Model, (string?)a["id"], Rebars.Class), a));

        // A beam's or a column's type gets its stirrups (decision 215): {id, enabled, diameter, cover, mode, step, zones, mark}.
        r.Command("setTypeStirrups", "Stirrups on type", (ctx, a) => { TypeStirrups.Set(ctx, a); return null; });
        // A column type's longitudinal bars (decision 218): {id, enabled, perFace, diameter, lap, mark}.
        r.Command("setColumnBars", "Column bars", (ctx, a) => { ColumnBars.Set(ctx, a); return null; });

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
                // The cover its anchors stand off by, where it comes from (own, host, settings), and the anchors themselves (decision 226).
                var (cover, source) = RebarAnchors.CoverOf(q.Model, e, RebarSettings.Of(q.Model));
                o["cover"] = Math.Round(cover, 6);
                o["coverSource"] = source;
                o["anchors"] = AnchorsJson(s, at, cover);
                // Whether it can be cut in two with a lap, the stock length it is measured against and the lap it would get (decision 227).
                var st = RebarSettings.Of(q.Model);
                o["splittable"] = RebarSplit.IsLongitudinal(s.Shape);
                o["stockLength"] = st.StockLength;
                o["lap"] = Math.Round(st.LapOf(s.Diameter), 4);
                if (RebarSplit.IsLongitudinal(s.Shape) && length > st.StockLength + 1e-6) o["overStock"] = true;
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
        // What a cut would give ({id, station, lap?}): the two pieces' lengths, or why it cannot be made; the interface shows them while the user types.
        r.Query("splitPreview", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") is { } x && x.Class == Rebars.Class ? x : throw new CommandException("Not a bar.");
            return RebarSplit.Preview(RebarSettings.Of(q.Model), e, Json.Number(a["station"]) ?? throw new CommandException("The station is a length from the bar's start."), Json.Number(a["lap"]));
        });
        r.Query("settings", (q, _) => RebarSettings.Of(q.Model).Resolved());
        RebarBodies.Register(r);
    }

    static Entity Require(IModelView m, string? id, string cls) => m.Get(id ?? "") is { } e && e.Class == cls ? e : throw new CommandException(cls == Rebars.Class ? "Not a bar." : "Not a bar distribution.");

    /// <summary>The bar's anchors as the interface reads them: <c>[{key, at}]</c>.</summary>
    static JsonArray AnchorsJson(RebarSpec s, Vec2 at, double cover) =>
        new(RebarAnchors.Anchors(s, at, cover).Select(a => (JsonNode)new JsonObject { ["key"] = a.Key, ["at"] = Rebars.J(a.At) }).ToArray());

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
        // The label: where it was put ({at, elbow, offset}: each set or, null, taken away) and its own style ({style: a patch, null to inherit}); null takes all away.
        if (a.ContainsKey("label"))
        {
            if (a["label"] is JsonObject lo)
            {
                if (attrs["label"] is not JsonObject label) attrs["label"] = label = new JsonObject();
                LabelPlace.Write(label, lo);
                if (lo.ContainsKey("style")) { if (LabelStyle.Patch(label["style"] as JsonObject, lo["style"]) is { } st) label["style"] = st; else label.Remove("style"); }
                if (label.Count == 0) attrs.Remove("label");
            }
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
            // The bar's own cover (decision 226): null takes it away, the host's or the settings' standing in again.
            if (a.ContainsKey("cover"))
            {
                if (a["cover"] == null) attrs.Remove("cover");
                else
                {
                    var cv = Json.Number(a["cover"]) ?? throw new CommandException("The cover is a length.");
                    if (cv < 0 || cv > 0.2) throw new CommandException("The cover is between 0 and 200 mm.");
                    attrs["cover"] = Math.Round(cv, 6);
                }
            }
            // The second piece of a split bar is drawn beside its lap, this far (m, signed) across the bar; its place in space stays.
            if (a.ContainsKey("lapOffset")) { if (Json.Number(a["lapOffset"]) is { } lo && Math.Abs(lo) > 1e-9) attrs["lapOffset"] = Math.Round(lo, 6); else attrs.Remove("lapOffset"); }
            if (a.ContainsKey("host")) { if ((string?)a["host"] is { Length: > 0 } h) attrs["host"] = h; else attrs.Remove("host"); }
            if (a.ContainsKey("across"))
            {
                if (a["across"] is JsonObject ao)
                {
                    var n = (int)Math.Round(Json.Number(ao["count"]) ?? 1);
                    if (n < 1 || n > 40) throw new CommandException("A bar is laid 1 to 40 times across the drawing.");
                    var mode = (string?)ao["mode"] ?? "cover";
                    if (mode is not ("cover" or "step")) throw new CommandException("Bars across the drawing are laid by the cover or at a step.");
                    var step = Json.Number(ao["step"]) ?? 0.05;
                    if (step < 0.005) throw new CommandException("The step across is 5 mm at least.");
                    attrs["across"] = new Rebar3D.Across(n, mode, step).ToJson();
                }
                else attrs.Remove("across");
            }
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
            foreach (var k in new[] { "size", "side" })
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
        // Every group's bars first, its label asked for; the labels drawn together at the end, so they stack in the marks' order.
        var marks = new List<ViewMark>();
        var labels = new List<LabelRequest>();
        foreach (var e in Rebars.On(c.Model, c.View.Id, Rebars.Class))
            if (BarMark(c, st, e, hidden, labels) is { } mk) marks.Add(mk);
        foreach (var e in Rebars.On(c.Model, c.View.Id, Rebars.RunClass))
            if (RunMark(c, st, e, hidden, labels) is { } mk) marks.Add(mk);
        // The stirrups the beams' and columns' types are configured with, projected as the view lies to each axis.
        marks.AddRange(TypeStirrups.Marks(c, st, labels));
        // Bars drawn on other views, where they stand in space: dots where they run into this view, lines along.
        marks.AddRange(Rebar3D.Marks(c, st, labels));
        marks.AddRange(ColumnBars.Marks(c, st, labels));
        RebarLabels.Draw(c, st, labels);
        return marks;
    }

    static double Width(RebarSpec s, ViewContext c) => Math.Max(0.25, s.Diameter * 1000 / c.Scale);
    static string Color(RebarSettings st, RebarSpec s) => st.ColorByDiameter ? RebarShapes.ColorOf(s.Diameter) : "#000000";

    static ViewMark? BarMark(ViewContext c, RebarSettings st, Entity e, HashSet<string> hidden, List<LabelRequest> labels)
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
        // The second piece of a split bar is drawn beside its lap (one diameter and half a millimetre of paper across the bar) so the lap reads;
        // where it stands in space (its snaps, grips, its copies across) does not move.
        var lapOffset = new Vec2(0, 0);
        if (e.NumOrNull("lapOffset") is { } lo && Math.Abs(lo) > 1e-9 && nominal.Count >= 2)
            lapOffset = (nominal[1] - nominal[0]).Normalized().Perp * (lo + Math.Sign(lo) * 0.5 * c.Mm);
        for (var ix = 0; ix < nx; ix++)
            for (var iy = 0; iy < ny; iy++)
            {
                var off = new Vec2(ix * dx, iy * dy) + lapOffset;
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
        // The mark's balloon with its leader, and what the bar is: drawn with the view's other labels, beside what was drawn here.
        var drawn = mk.Lines.Where(l => l.Layer == layer).Select(l => (l.A, l.B)).ToList();
        var style = LabelStyle.Resolve("bars", st, element: RebarLabels.StyleOf(e));
        var text = style.Text(RebarLabels.FactsOf(e, s, st, Rebars.Pieces(e).ToString(CultureInfo.InvariantCulture)));
        labels.Add(new LabelRequest(mk, e.Id, "label", style, text, Rebars.Mark(e), color, drawn, view == "section" ? [at] : [], view == "section" ? null : RebarLabels.AlongOf(drawn), Rebars.LabelPlaceOf(e)) { Own = true });
        // What the interface drags: the shape's grips where they stand now, its point and its label.
        var grips = new JsonArray(RebarShapes.Grips(s).Select(g => (JsonNode)new JsonObject { ["key"] = g.Key, ["at"] = Rebars.J(RebarShapes.Place(s, at, g.At)) }).ToArray());
        mk.Data = new JsonObject { ["grips"] = grips, ["at"] = Rebars.J(at) };
        // Seen from the front, the anchors standing the cover outside the bar (decision 226): dragged onto the formwork's face.
        if (view == "front") mk.Data["anchors"] = AnchorsJson(s, at, RebarAnchors.CoverOf(c.Model, e, st).Cover);
        return mk;
    }

    /// <summary>The point of the drawn chain nearest to <paramref name="p"/> (on its segments, not only at its vertices); <paramref name="fallback"/> for an empty chain.</summary>
    internal static Vec2 Nearest(IReadOnlyList<Vec2> chain, Vec2 p, Vec2 fallback)
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

    static ViewMark? RunMark(ViewContext c, RebarSettings st, Entity e, HashSet<string> hidden, List<LabelRequest> labels)
    {
        var s = Rebars.SpecOf(e);
        var layer = e.Str("layer") ?? RebarShapes.Def(s.Shape).Layer;
        if (hidden.Contains(layer)) return null;
        var (path, positions, stepText, _) = Rebars.RunOf(e, s);
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
        // The label, drawn with the view's other labels: as a dimension string along the path outside the bars (every position a tick), or beside them.
        var style = LabelStyle.Resolve("runs", st, element: RebarLabels.StyleOf(e));
        var text = style.Text(RebarLabels.FactsOf(e, s, st, (positions.Count * Rebars.Count(e)).ToString(CultureInfo.InvariantCulture), stepText));
        var bars = mk.Lines.Where(l => l.Layer == layer).Select(l => (l.A, l.B)).ToList();
        labels.Add(new LabelRequest(mk, e.Id, "label", style, text, Rebars.Mark(e), color, bars, [], path.TangentAt(path.Length / 2).Normalized(), Rebars.LabelPlaceOf(e))
        { Own = true, String = new LabelString(path, positions, side - size / 2, side + size / 2) });
        mk.Snaps.AddRange(positions.Select(p0 => path.PointAt(p0)));
        mk.Data = new JsonObject { ["positions"] = positions.Count };
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

    /// <summary>The bar's attributes carried by a plan transformation: its point, turn and mirror (a mirror flips its y and reflects its turn), a run's ends and rise, the label's point and elbow.</summary>
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
        if (Rebars.LabelPlaceOf(e) is { } place && attrs[LabelStyle.Key] is JsonObject lo)
        {
            if (place.At is { } at) lo["at"] = Rebars.J(t.Point(at));
            if (place.Elbow is { } elbow) lo["elbow"] = Rebars.J(t.Point(elbow));
        }
        return attrs;
    }
}
