using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// One step of a template's sequence (docs/plan-4d5d.md, stage 3): its name, the elements it builds (IFC classes and the
/// part of them: whole, core, finishes), how it is split (once for the building, or a task a storey), its duration when
/// no norm gives one, the norm its quantity is timed by and the crew, whether it is wet, and how it follows the step
/// before it on the same storey (FS, or SS with a lag for finishes climbing after the structure). The step
/// <c>demolition</c> takes the elements the phases demolish; a step with no classes is a task with no elements (site set-up).
/// </summary>
public sealed record GenStep(string Key, string Name, IReadOnlyList<string> Classes, string? Part, string Split, int Days, string? Norm, int Crew,
    bool Wet = false, string Link = "FS", int Lag = 0, bool On = true, string? Kind = null, bool Cycle = false)
{
    public const string Demolition = "demolition";
    public static readonly string[] Splits = ["once", "storey"];

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["key"] = Key, ["name"] = Name, ["classes"] = new JsonArray(Classes.Select(c => (JsonNode)c).ToArray()), ["split"] = Split, ["days"] = Days, ["crew"] = Crew,
            ["link"] = Link, ["lag"] = Lag, ["on"] = On,
        };
        if (Part != null) o["part"] = Part;
        if (Norm != null) o["norm"] = Norm;
        if (Wet) o["wet"] = true;
        if (Kind != null) o["kind"] = Kind;
        if (Cycle) o["cycle"] = true;
        return o;
    }

    public static GenStep? Read(JsonNode? n, int i)
    {
        if (n is not JsonObject o) return null;
        var key = ((string?)o["key"])?.Trim() is { Length: > 0 } k ? k : $"step{i + 1}";
        var name = ((string?)o["name"])?.Trim() is { Length: > 0 } nm ? nm : key;
        var classes = o["classes"] is JsonArray a ? a.Select(x => (string?)x).OfType<string>().Where(s => s.Length > 0).Distinct().ToList() : [];
        string? part;
        try { part = TaskParts.Parse(o["part"]); } catch (CommandException) { part = null; }
        var split = Splits.Contains((string?)o["split"]) ? (string)o["split"]! : "once";
        var link = (string?)o["link"] is { } l && Plan.LinkTypes.Contains(l.ToUpperInvariant()) ? l.ToUpperInvariant() : "FS";
        var kind = (string?)o["kind"] is { } kd && Plan.Kinds.Contains(kd) ? kd : null;
        return new(key, name, classes, part, split, Math.Max(1, (int)Math.Round(Json.Number(o["days"]) ?? 5)), ((string?)o["norm"])?.Trim() is { Length: > 0 } nr ? nr : null,
            Math.Max(0, (int)Math.Round(Json.Number(o["crew"]) ?? 0)), o["wet"] is JsonValue w && w.TryGetValue<bool>(out var wb) && wb, link,
            (int)Math.Round(Json.Number(o["lag"]) ?? 0), o["on"] is not JsonValue ov || !ov.TryGetValue<bool>(out var on) || on, kind,
            o["cycle"] is JsonValue cy && cy.TryGetValue<bool>(out var cb) && cb);
    }
}

/// <summary>What the generator makes: the template it started from, its steps, whether storeys split them, demolitions first, and the temporaries tied to their hosts' tasks.</summary>
public sealed record GenSpec(string Template, IReadOnlyList<GenStep> Steps, bool ByStorey = true, bool Demolition = true, bool Formwork = true, bool Scaffolding = true, DateOnly? Start = null)
{
    public JsonObject ToJson(bool withStart = true)
    {
        var o = new JsonObject
        {
            ["template"] = Template, ["steps"] = new JsonArray(Steps.Select(s => (JsonNode)s.ToJson()).ToArray()),
            ["byStorey"] = ByStorey, ["demolition"] = Demolition, ["formwork"] = Formwork, ["scaffolding"] = Scaffolding,
        };
        if (withStart && Start is { } d) o["start"] = WorkCalendar.Date(d);
        return o;
    }

    static bool Flag(JsonNode? n, bool fallback) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    /// <summary>A spec as stored or given; what it does not say is taken from <paramref name="fallback"/>.</summary>
    public static GenSpec Read(JsonObject o, GenSpec fallback)
    {
        var steps = o["steps"] is JsonArray a ? a.Select((x, i) => GenStep.Read(x, i)).OfType<GenStep>().ToList() : fallback.Steps.ToList();
        // Two steps of one key would make the same tasks: the later ones get keys of their own.
        var seen = new HashSet<string>();
        for (var i = 0; i < steps.Count; i++)
        {
            var k = steps[i].Key;
            for (var n = 2; !seen.Add(k); n++) k = $"{steps[i].Key}-{n}";
            if (k != steps[i].Key) steps[i] = steps[i] with { Key = k };
        }
        return new(((string?)o["template"])?.Trim() is { Length: > 0 } t ? t : fallback.Template, steps,
            Flag(o["byStorey"], fallback.ByStorey), Flag(o["demolition"], fallback.Demolition), Flag(o["formwork"], fallback.Formwork), Flag(o["scaffolding"], fallback.Scaffolding),
            o.ContainsKey("start") ? WorkCalendar.ParseDate((string?)o["start"]) : fallback.Start);
    }
}

/// <summary>
/// The schedule generator (docs/plan-4d5d.md, stage 3, Planning → Generate schedule…): from a template's sequence, the
/// model's storeys and elements, a task a step (and a storey), its rule (stage 2) taking the step's elements so they stay
/// linked, its duration from their quantity × the user's norm ÷ (crew × the calendar's hours), the links of the sequence
/// (the step on the storey below, the step before on the same storey); the demolitions first, the formwork and scaffolds
/// tied to their hosts' tasks. Generated again, it keeps what was edited by hand: tasks marked <c>own</c> stay as they are,
/// tasks and links made by hand stay, the exceptions of a generated task stay; the rest is made again over the same ids
/// (<c>gen</c>: the step, and the storey).
/// </summary>
public static class Generator
{
    public const string StylesKey = "scheduleStyles";
    public const string OwnPrefix = "own:";

    static readonly string[] Mep =
    [
        "IfcPipeSegment", "IfcPipeFitting", "IfcDuctSegment", "IfcDuctFitting", "IfcCableSegment", "IfcCableCarrierSegment", "IfcCableCarrierFitting",
        "IfcSanitaryTerminal", "IfcValve", "IfcBoiler", "IfcFlowMeter", "IfcAirTerminal", "IfcTank", "IfcSwitchingDevice", "IfcSpaceHeater", "IfcOutlet",
        "IfcLightFixture", "IfcFilter", "IfcFan", "IfcPump", "IfcElectricDistributionBoard", "IfcBurner", "IfcDistributionChamberElement", "IfcFlowTerminal",
    ];
    static readonly string[] Interior = ["LapinRoomFinish", "LapinSkirting", "LapinCeiling", "IfcCovering", "LapinWallSweep", "LapinRailing", "IfcRailing", "LapinUnderStair", "IfcFurniture"];

    static GenStep S(string key, string name, string[] classes, string split, int days, string? norm, string? part = null, bool wet = false, string link = "FS", int lag = 0, string? kind = null, bool cycle = false) =>
        new(key, name, classes, part, split, days, norm, 0, wet, link, lag, true, kind, cycle);

    static readonly GenStep DemolitionStep = S(GenStep.Demolition, "Demolition", [], "once", 5, "LAP-DEMO", kind: "DEMOLITION");
    static readonly GenStep Site = S("site", "Site set-up", [], "once", 5, null);
    static readonly GenStep Earth = S("earthworks", "Earthworks", ["IfcEarthworksCut", "IfcEarthworksFill", IfcClass.GeographicElement], "once", 5, "LAP-EARTH");
    static readonly GenStep Found = S("foundations", "Foundations", [IfcClass.Footing, "IfcPile"], "once", 10, "LAP-FOUND", wet: true);
    // A roof in two steps (decision 332): its structure (the rafters, the trusses or the deck) then its covering; the downpipes with the covering.
    static readonly GenStep Roof = S("roof", "Roof structure", [IfcClass.Roof], "once", 10, "LAP-ROOF", TaskParts.Core);
    static readonly GenStep RoofCovering = S("roofCovering", "Roof covering", [IfcClass.Roof], "once", 5, "LAP-COVER", TaskParts.Finishes);
    static readonly GenStep Downpipes = S("downpipes", "Gutters and downpipes", ["LapinDownpipe"], "once", 2, null, link: "SS", lag: 2);
    static readonly GenStep Openings = S("openings", "Doors and windows", [IfcClass.Door, IfcClass.Window, "IfcCurtainWall"], "storey", 5, "LAP-OPEN");
    static readonly GenStep Services = S("mep", "Installations", Mep, "storey", 10, "LAP-MEP");
    static readonly GenStep WallFinishes = S("wallFinishes", "Wall finishes", [IfcClass.Wall], "storey", 10, "LAP-PLAST", TaskParts.Finishes, wet: true);
    static readonly GenStep Fittings = S("interior", "Floors, ceilings and fittings", Interior, "storey", 10, "LAP-FINISH", link: "SS", lag: 5);
    static readonly GenStep External = S("external", "External works", [IfcClass.Pavement, "LapinPlanting", "LapinSiteFeature", "LapinRetainingWall"], "once", 5, "LAP-EXT");
    static readonly string[] Concrete = [IfcClass.Column, IfcClass.Beam, IfcClass.Slab, IfcClass.Stair, IfcClass.Ramp, "LapinRebar", "LapinRebarRun"];

    public sealed record Template(string Id, string Label, string Description, GenSpec Spec)
    {
        public JsonObject ToJson() => new() { ["id"] = Id, ["label"] = Label, ["description"] = Description, ["spec"] = Spec.ToJson(false) };
    }

    /// <summary>The catalogue's templates: a reinforced concrete frame, load-bearing masonry, a steel hall, a timber frame.</summary>
    public static readonly IReadOnlyList<Template> Templates =
    [
        new("concrete", "Concrete frame", "Columns, beams and floors a storey at a time, then the walls between them", new("concrete",
        [
            DemolitionStep, Site, Earth, Found,
            S("structure", "Structure", Concrete, "storey", 10, "LAP-FRAME", wet: true, cycle: true),
            S("masonry", "Masonry", [IfcClass.Wall], "storey", 10, "LAP-MASON", TaskParts.Core),
            Roof, RoofCovering, Downpipes, Openings, Services, WallFinishes, Fittings, External,
        ])),
        new("masonry", "Load-bearing masonry", "The walls of a storey, then its floor and ring beams over them", new("masonry",
        [
            DemolitionStep, Site, Earth, Found,
            S("walls", "Load-bearing walls", [IfcClass.Wall], "storey", 10, "LAP-MASON", TaskParts.Core, cycle: true),
            S("floors", "Floors and ring beams", Concrete, "storey", 10, "LAP-FLOOR", wet: true, cycle: true),
            Roof, RoofCovering, Downpipes, Openings, Services, WallFinishes, Fittings, External,
        ])),
        new("steel", "Steel hall", "The frames and their bracing, the cladding, then the floor slab inside", new("steel",
        [
            DemolitionStep, Site, Earth, Found,
            S("steel", "Steel structure", ["LapinSteelMember", "LapinSteelNode", IfcClass.Column, IfcClass.Beam, "IfcMember", "IfcPlate"], "once", 15, "LAP-STEEL"),
            S("roof", "Roof cladding", [IfcClass.Roof, "LapinDownpipe"], "once", 10, "LAP-CLAD"),
            S("cladding", "Wall cladding", [IfcClass.Wall, "IfcCurtainWall"], "once", 10, "LAP-CLAD"),
            S("slab", "Floor slab", [IfcClass.Slab, IfcClass.Stair], "once", 10, "LAP-FLOOR", wet: true),
            Openings with { Split = "once" }, Services with { Split = "once" }, Fittings with { Split = "once" }, External,
        ])),
        new("timber", "Timber frame", "Framed walls a storey at a time, then the floor over them", new("timber",
        [
            DemolitionStep, Site, Earth, Found,
            S("frame", "Timber frame walls", [IfcClass.Wall], "storey", 10, "LAP-TIMBER", TaskParts.Core, cycle: true),
            S("floors", "Floors", [IfcClass.Slab, IfcClass.Beam, IfcClass.Column, IfcClass.Stair, "IfcMember"], "storey", 5, "LAP-JOIST", cycle: true),
            Roof, RoofCovering, Downpipes, Openings, Services, WallFinishes with { Wet = false }, Fittings, External,
        ])),
    ];

    /// <summary>
    /// The template the model asks for: a steel hall when it has one, a timber frame when its walls' types are framed,
    /// load-bearing masonry when it has no columns, else a concrete frame.
    /// </summary>
    public static string Suggest(IModelView m)
    {
        if (m.OfClass("LapinSteelHall").Any()) return "steel";
        if (m.OfClass(IfcClass.WallType).Any(t => (t.Attrs["wood"] as JsonObject)?["construction"]?.ToString() == "timberFrame")
            && m.OfClass(IfcClass.Wall).Any(w => m.RelatingOf(w.Id, IfcClass.RelDefinesByType) is { } t && (t.Attrs["wood"] as JsonObject)?["construction"]?.ToString() == "timberFrame"))
            return "timber";
        return m.OfClass(IfcClass.Column).Any() ? "concrete" : "masonry";
    }

    public static Template TemplateOf(string? id) => Templates.FirstOrDefault(t => t.Id == id) ?? Templates[0];

    // ------------------------------------------------------------------ the project's styles

    public static List<JsonObject> OwnStyles(IModelView m) => (Spatial.Project(m)?.Attrs[StylesKey] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];

    /// <summary>A style's spec by its id: the project's own, or the catalogue's template.</summary>
    public static GenSpec? StyleSpec(IModelView m, string? id)
    {
        if (id == null) return null;
        if (id.StartsWith(OwnPrefix, StringComparison.Ordinal))
            return OwnStyles(m).FirstOrDefault(s => (string?)s["id"] == id)?["spec"] is JsonObject o ? GenSpec.Read(o, TemplateOf(null).Spec) : null;
        return Templates.FirstOrDefault(t => t.Id == id)?.Spec;
    }

    /// <summary>The spec the schedule was last generated with; else the template the model asks for.</summary>
    public static GenSpec Current(IModelView m)
    {
        var suggested = TemplateOf(Suggest(m)).Spec;
        return Plan.ScheduleOf(m)?.Attrs["generator"] is JsonObject o ? GenSpec.Read(o, suggested) : suggested;
    }

    /// <summary>A spec saved as a style of the project under a name: a new one, or the one of that id (or name) saved again. Its id.</summary>
    public static string SaveStyle(ICommandContext ctx, GenSpec spec, string label, string? id)
    {
        label = label.Trim();
        if (label.Length == 0) throw new CommandException("A schedule style needs a name.");
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var list = OwnStyles(ctx.Model);
        var same = list.FirstOrDefault(s => id != null ? (string?)s["id"] == id : string.Equals((string?)s["label"], label, StringComparison.OrdinalIgnoreCase));
        if (id != null && same == null) throw new CommandException("The project has no such schedule style.");
        var sid = (string?)same?["id"] ?? OwnPrefix + ctx.NewId();
        var style = new JsonObject { ["id"] = sid, ["label"] = label, ["spec"] = spec.ToJson(false) };
        var next = list.Select(s => (string?)s["id"] == sid ? style : (JsonNode)s.DeepClone()).ToList();
        if (same == null) next.Add(style);
        var attrs = project.CloneAttrs();
        attrs[StylesKey] = new JsonArray(next.ToArray());
        ctx.Replace(project.With(attrs: attrs));
        return sid;
    }

    public static void RenameStyle(ICommandContext ctx, string id, string label)
    {
        label = label.Trim();
        if (label.Length == 0) throw new CommandException("A schedule style needs a name.");
        EditStyles(ctx, list =>
        {
            var s = list.FirstOrDefault(x => (string?)x["id"] == id) ?? throw new CommandException("The project has no such schedule style.");
            s["label"] = label;
        });
    }

    public static void DeleteStyle(ICommandContext ctx, string id) =>
        EditStyles(ctx, list => { if (list.RemoveAll(x => (string?)x["id"] == id) == 0) throw new CommandException("The project has no such schedule style."); });

    static void EditStyles(ICommandContext ctx, Action<List<JsonObject>> edit)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var list = OwnStyles(ctx.Model).Select(s => (JsonObject)s.DeepClone()).ToList();
        edit(list);
        var attrs = project.CloneAttrs();
        if (list.Count == 0) attrs.Remove(StylesKey);
        else attrs[StylesKey] = new JsonArray(list.Select(x => (JsonNode)x).ToArray());
        ctx.Replace(project.With(attrs: attrs));
    }

    // ------------------------------------------------------------------ building the schedule

    /// <summary>A task the generator makes: its key, step, storey, and what its duration came from.</summary>
    public sealed record Made(string Id, string Gen, GenStep Step, string? Storey, bool Summary, string Source, double? Quantity, string? Unit, int Count);

    /// <summary>
    /// The schedule as it would be: the schedule entity, every task after generating (made, kept by hand), every link,
    /// what was made and how, and the ids of the tasks it removes.
    /// </summary>
    public sealed record Outcome(Entity Schedule, List<Entity> Tasks, List<Relation> Links, List<Made> Made, HashSet<string> Removed, GenSpec Spec, List<string> Notes);

    static readonly string[] Volume = ["NetVolume", "Volume", "GrossVolume"];
    static readonly string[] Area = ["NetSideArea", "NetArea", "NetFloorArea", "NetSurfaceArea", "GrossSideArea", "GrossArea", "GrossFloorArea", "GrossSurfaceArea", "Area"];
    static readonly string[] Length = ["Length"];
    static readonly string[] Weight = ["NetWeight", "GrossWeight", "Weight"];

    static string[] NamesOf(string basis) => basis switch { "volume" => Volume, "area" => Area, "length" => Length, "weight" => Weight, _ => [] };

    static double? Pick(IReadOnlyList<Quantity> qs, string[] names)
    {
        foreach (var n in names) if (qs.FirstOrDefault(x => x.Name == n) is { } hit) return hit.Value;
        return null;
    }

    /// <summary>
    /// What an element measures in a kind of quantity, for the layers a task takes of it: the whole element's set when it
    /// takes all of it, else its layers' sets (a core built of zones, the zones' sets); a count is one.
    /// </summary>
    public static double MeasureOf(IReadOnlyList<QuantitySet> sets, ElementFacts f, ulong mask, string basis)
    {
        if (mask == 0) return 0;
        if (basis == "count") return 1;
        var names = NamesOf(basis);
        if ((mask & f.All) == f.All || f.Layers.Count <= 1)
        {
            foreach (var s in sets.Where(s => s.Part == null)) if (Pick(s.Quantities, names) is { } v) return v;
            return sets.Where(s => s.Part != null).Sum(s => Pick(s.Quantities, names) ?? 0);
        }
        double sum = 0;
        var found = new HashSet<int>();
        foreach (var s in sets)
        {
            // A layer's own set: layer:<n>, or a part a layer is drawn as (a roof's deck or covering, decision 332).
            if (s.Part is not { } part || part.StartsWith("zone:", StringComparison.Ordinal) || TaskParts.LayerOf(f, part) is not { } i || i >= 64 || (mask & (1UL << i)) == 0) continue;
            found.Add(i);
            sum += Pick(s.Quantities, names) ?? 0;
        }
        var core = f.Layers.FirstOrDefault(l => l.Core);
        if (core != null && core.Index < 64 && (mask & (1UL << core.Index)) != 0 && !found.Contains(core.Index))
            sum += sets.Where(s => s.Part?.StartsWith("zone:", StringComparison.Ordinal) == true).Sum(s => Pick(s.Quantities, names) ?? 0);
        // A layer drawn as parts of its own but measured by no set (a roof's rafters, a covering of no material): the element's own measure.
        double? whole = null;
        foreach (var l in f.Layers)
            if (l.Keys is { Count: > 0 } && l.Index < 64 && (mask & (1UL << l.Index)) != 0 && !found.Contains(l.Index))
                sum += whole ??= sets.Where(s => s.Part == null).Select(s => Pick(s.Quantities, names)).FirstOrDefault(v => v != null) ?? 0;
        return sum;
    }

    static JsonArray Arr(IEnumerable<string> xs) => new(xs.Select(x => (JsonNode)x).ToArray());

    /// <summary>
    /// The schedule a spec makes of the model: what <see cref="Apply"/> writes and the preview shows. <paramref name="keep"/>:
    /// the tasks made by hand stay (else only the generated ones and those edited by hand stay).
    /// </summary>
    public static Outcome Build(IQueryContext q, GenSpec spec, bool keep, Func<string> newId)
    {
        var m = q.Model;
        var notes = new List<string>();
        var plan = Plan.Read(m);
        var links = TaskElements.Resolve(q, plan);
        var norms = Norms.Of(m);
        var calendar = plan.Calendar;
        var phases = PhasesModule.Settings(m);
        var defaultPhase = phases.DefaultCreated?.Id ?? "";
        // Kept elements of an existing phase are no task's: when the model has some, the steps take the new phases' only.
        var existing = phases.Phases.Where(p => p.Kind == "existing").Select(p => p.Id).ToHashSet();
        var built = existing.Count > 0 && links.Candidates.Any(f => existing.Contains(f.Phase ?? defaultPhase))
            ? phases.Phases.Where(p => !existing.Contains(p.Id)).Select(p => p.Id).ToList() : [];
        var storeys = Spatial.Storeys(m).ToList();
        var schedule = Plan.ScheduleOf(m);
        var scheduleId = schedule?.Id ?? "draft-schedule";
        var oldTasks = m.OfClass(Plan.TaskClass).ToList();
        var byGen = new Dictionary<string, Entity>();
        foreach (var t in oldTasks) if (t.Str("gen") is { } g && !byGen.ContainsKey(g)) byGen[g] = t;

        var made = new List<Made>();
        var tasks = new List<Entity>();
        var order = 0;
        // The tasks of each step, by storey ("" for once), to link them.
        var stepTasks = new List<(GenStep Step, bool PerStorey, Dictionary<string, string> ByStorey)>();
        var quantities = new Dictionary<string, IReadOnlyList<QuantitySet>>();
        IReadOnlyList<QuantitySet> SetsOf(string id) => quantities.TryGetValue(id, out var s) ? s : quantities[id] = q.Quantities(id);

        Entity Task(string gen, string name, string? parent, int ord, JsonObject extra, string? kind)
        {
            var old = byGen.GetValueOrDefault(gen);
            var id = old?.Id ?? newId();
            var attrs = new JsonObject { ["schedule"] = scheduleId, ["order"] = ord, ["gen"] = gen };
            if (parent != null) attrs["parent"] = parent;
            foreach (var (k, v) in extra) attrs[k] = v?.DeepClone();
            // A generated task's exceptions and notes are the user's: they stay.
            foreach (var k in new[] { "add", "remove", "notes" }) if (old?.Attrs[k] is { } v) attrs[k] = v.DeepClone();
            return new Entity(id, Plan.TaskClass, name, attrs, kind);
        }

        // A leaf's duration: its elements' quantity in the norm's unit × the norm's hours ÷ (crew × hours a day); else the step's days.
        (int Days, string Source, double? Qty, string? Unit, int Crew) Timing(GenStep step, Dictionary<string, ulong> elements)
        {
            var norm = norms.Find(step.Norm);
            var crew = step.Crew > 0 ? step.Crew : norm?.Crew ?? 1;
            if (norm != null && Norms.BasisOf(norm.Unit) is { } basis && elements.Count > 0)
            {
                double qty = 0;
                foreach (var (id, mask) in elements) if (links.Facts(id) is { } f) qty += MeasureOf(SetsOf(id), f, mask, basis.Basis);
                qty *= basis.Factor;
                if (qty > 1e-9)
                {
                    var days = (int)Math.Ceiling(qty * norm.Hours / (crew * Math.Max(0.5, calendar.Hours)) - 1e-9);
                    return (Math.Max(1, days), "norm", qty, norm.Unit, crew);
                }
            }
            return (step.Days, "default", null, null, crew);
        }

        JsonObject Rule(GenStep step, string? storey, IReadOnlyList<string>? demolished = null)
        {
            var o = new JsonObject();
            if (demolished != null) { o["demolished"] = Arr(demolished); return o; }
            o["classes"] = Arr(step.Classes);
            if (storey != null) o["storeys"] = Arr([storey]);
            if (built.Count > 0) o["phases"] = Arr(built);
            return o;
        }

        Entity Leaf(GenStep step, string gen, string name, string? parent, int ord, JsonObject? rule, Dictionary<string, ulong> elements, string? storey)
        {
            var (days, source, qty, unit, crew) = Timing(step, elements);
            var extra = new JsonObject { ["duration"] = days, ["crew"] = crew, ["durationFrom"] = source };
            if (rule != null) extra["rule"] = rule;
            if (step.Part != null) extra["part"] = step.Part;
            if (step.Wet) extra["wet"] = true;
            if (step.Norm != null) extra["norm"] = step.Norm;
            if (qty is { } qv) { extra["quantity"] = Math.Round(qv, 3); extra["unit"] = unit; }
            var e = Task(gen, name, parent, ord, extra, step.Kind);
            made.Add(new(e.Id, gen, step, storey, false, source, qty, unit, elements.Count));
            return e;
        }

        var demolishedIn = links.Candidates.Where(f => f.Demolished != null).Select(f => f.Demolished!).Distinct().ToList();
        foreach (var step in spec.Steps.Where(s => s.On))
        {
            var byStorey = new Dictionary<string, string>();
            if (step.Key == GenStep.Demolition)
            {
                if (!spec.Demolition || demolishedIn.Count == 0) continue;
                var rule = Rule(step, null, demolishedIn);
                var els = links.Of(Probe(rule, step.Part));
                if (els.Count == 0) continue;
                var e = Leaf(step, step.Key, step.Name, null, ++order, rule, els, null);
                tasks.Add(e);
                byStorey[""] = e.Id;
                stepTasks.Add((step, false, byStorey));
                continue;
            }
            if (step.Classes.Count == 0)
            {
                var e = Leaf(step, step.Key, step.Name, null, ++order, null, [], null);
                tasks.Add(e);
                byStorey[""] = e.Id;
                stepTasks.Add((step, false, byStorey));
                continue;
            }
            if (spec.ByStorey && step.Split == "storey")
            {
                var children = new List<(Entity Storey, JsonObject Rule, Dictionary<string, ulong> Els)>();
                foreach (var s in storeys)
                {
                    var rule = Rule(step, s.Id);
                    var els = links.Of(Probe(rule, step.Part));
                    if (els.Count > 0) children.Add((s, rule, els));
                }
                if (children.Count == 0) continue;
                var summaryGen = step.Key + "/";
                var summary = Task(summaryGen, step.Name, null, ++order, new JsonObject { ["duration"] = 1 }, null);
                tasks.Add(summary);
                made.Add(new(summary.Id, summaryGen, step, null, true, "summary", null, null, children.Sum(c => c.Els.Count)));
                var k = 0;
                foreach (var (s, rule, els) in children)
                {
                    var e = Leaf(step, $"{step.Key}/{s.Id}", $"{step.Name} · {s.Name}", summary.Id, ++k, rule, els, s.Id);
                    tasks.Add(e);
                    byStorey[s.Id] = e.Id;
                }
                stepTasks.Add((step, true, byStorey));
            }
            else
            {
                var rule = Rule(step, null);
                var els = links.Of(Probe(rule, step.Part));
                if (els.Count == 0) continue;
                var e = Leaf(step, step.Key, step.Name, null, ++order, rule, els, null);
                tasks.Add(e);
                byStorey[""] = e.Id;
                stepTasks.Add((step, false, byStorey));
            }
        }

        // ---- what is kept: tasks edited by hand (own) in place of their generated selves, and the tasks made by hand
        var madeIds = tasks.Select(t => t.Id).ToHashSet();
        var final = new List<Entity>();
        foreach (var t in tasks)
            final.Add(m.Get(t.Id) is { } old && old.Bool("own") && old.Class == Plan.TaskClass ? old : t);
        var removed = new HashSet<string>();
        var keptOld = new List<Entity>();
        foreach (var t in oldTasks)
        {
            if (madeIds.Contains(t.Id)) continue;
            var generated = t.Str("gen") != null;
            if (t.Bool("own") || (!generated && keep)) keptOld.Add(t);
            else removed.Add(t.Id);
        }
        // A kept task under a summary that goes moves to the top; the kept ones follow the generated ones, in their order.
        var finalIds = final.Select(t => t.Id).Concat(keptOld.Select(t => t.Id)).ToHashSet();
        var nextOrder = new Dictionary<string, int>();
        foreach (var t in final) { var p = t.Str("parent") ?? ""; nextOrder[p] = Math.Max(nextOrder.GetValueOrDefault(p), (int)t.Num("order")); }
        foreach (var t in keptOld.OrderBy(t => plan.RowOf.GetValueOrDefault(t.Id, int.MaxValue)))
        {
            var attrs = t.CloneAttrs();
            if (t.Str("parent") is { } par && !finalIds.Contains(par)) attrs.Remove("parent");
            var p = (string?)attrs["parent"] ?? "";
            var ord = nextOrder[p] = nextOrder.GetValueOrDefault(p) + 1;
            attrs["order"] = ord;
            attrs["schedule"] = scheduleId;
            final.Add(t.With(attrs: attrs));
        }

        // ---- the links: those made by hand between tasks that stay, then the sequence's (each checked for a loop)
        var oldLinks = new List<Relation>();
        foreach (var t in oldTasks)
            foreach (var r in m.RelationsOf(t.Id, Plan.SequenceClass))
                if (r.Relating == t.Id && !oldLinks.Any(x => x.Id == r.Id)) oldLinks.Add(r);
        var finalLinks = new List<Relation>();
        foreach (var r in oldLinks)
            if (r.Attrs?["gen"] == null && finalIds.Contains(r.Relating) && r.Related.Count > 0 && finalIds.Contains(r.Related[0])) finalLinks.Add(r);
        var oldGen = oldLinks.Where(r => r.Attrs?["gen"] != null).ToList();
        var graph = new Dictionary<string, HashSet<string>>();
        void Edge(string a, string b) => (graph.TryGetValue(a, out var s) ? s : graph[a] = []).Add(b);
        bool Reaches(string from, string to)
        {
            var seen = new HashSet<string>();
            var stack = new Stack<string>([from]);
            while (stack.Count > 0)
            {
                var x = stack.Pop();
                if (x == to) return true;
                if (!seen.Add(x)) continue;
                if (graph.TryGetValue(x, out var next)) foreach (var n in next) stack.Push(n);
            }
            return false;
        }
        foreach (var r in finalLinks) Edge(r.Relating, r.Related[0]);
        void Link(string from, string to, string type, int lag)
        {
            if (from == to || finalLinks.Any(r => r.Relating == from && r.Related[0] == to) || Reaches(to, from)) return;
            var id = oldGen.FirstOrDefault(r => r.Relating == from && r.Related[0] == to)?.Id ?? newId();
            finalLinks.Add(new Relation(id, Plan.SequenceClass, from, [to], new JsonObject { ["type"] = type, ["lag"] = lag, ["gen"] = true }));
            Edge(from, to);
        }
        string? CycleBelow(string storey, int k)
        {
            var at = storeys.FindIndex(x => x.Id == storey);
            for (var i = at - 1; i >= 0; i--)
            {
                // The latest cycle step that has a task on the nearest storey below with any, if it comes after this one.
                var last = stepTasks.Select((x, j) => (x, j)).Where(x => x.x.PerStorey && x.x.Step.Cycle && x.x.ByStorey.ContainsKey(storeys[i].Id)).ToList();
                if (last.Count == 0) continue;
                var (top, j) = last[^1];
                return j > k ? top.ByStorey[storeys[i].Id] : null;
            }
            return null;
        }
        for (var k = 0; k < stepTasks.Count; k++)
        {
            var (step, perStorey, own) = stepTasks[k];
            if (!perStorey)
            {
                // Once: after the step before (its top storey's task, the last of its chain).
                if (k > 0) foreach (var from in LastOf(stepTasks[k - 1], storeys)) Link(from, own[""], "FS", 0);
                continue;
            }
            string? below = null;
            foreach (var s in storeys)
            {
                if (!own.TryGetValue(s.Id, out var task)) continue;
                // The same step on the storey below.
                if (below != null) Link(below, task, "FS", 0);
                // A storey's structural cycle (walls, then the floor over them) waits for the cycle of the storey below: its last step there.
                if (step.Cycle && CycleBelow(s.Id, k) is { } under) Link(under, task, "FS", 0);
                below = task;
                // The step before on the same storey; else a step done once before it.
                for (var j = k - 1; j >= 0; j--)
                {
                    var (prev, prevPer, prevOwn) = stepTasks[j];
                    if (!prevPer) { if (prevOwn.TryGetValue("", out var once)) Link(once, task, step.Link, step.Lag); break; }
                    if (prevOwn.TryGetValue(s.Id, out var same)) { Link(same, task, step.Link, step.Lag); break; }
                }
            }
        }

        // ---- the schedule: its start, and the spec it was made with (the configurator opens on it again)
        var sAttrs = schedule?.CloneAttrs() ?? new JsonObject();
        if (spec.Start is { } start) sAttrs["start"] = WorkCalendar.Date(start);
        else if (sAttrs["start"] == null) sAttrs["start"] = WorkCalendar.Date(Plan.NextMonday(Plan.Today()));
        sAttrs["generator"] = spec.ToJson(false);
        var scheduleEntity = schedule != null ? schedule.With(attrs: sAttrs) : new Entity(scheduleId, Plan.ScheduleClass, "Work schedule", sAttrs, "PLANNED");

        var defaults = made.Where(x => !x.Summary && x.Source == "default" && x.Step.Classes.Count > 0 && x.Step.Key != "site").Select(x => x.Step.Name).Distinct().ToList();
        if (defaults.Count > 0) notes.Add("No norm timed: " + string.Join(", ", defaults));
        return new(scheduleEntity, final, finalLinks, made, removed, spec, notes);
    }

    /// <summary>The tasks a step ends with: its task, or its top storey's.</summary>
    static IEnumerable<string> LastOf((GenStep Step, bool PerStorey, Dictionary<string, string> ByStorey) st, List<Entity> storeys)
    {
        if (!st.PerStorey) { if (st.ByStorey.TryGetValue("", out var t)) yield return t; yield break; }
        for (var i = storeys.Count - 1; i >= 0; i--)
            if (st.ByStorey.TryGetValue(storeys[i].Id, out var t)) { yield return t; yield break; }
    }

    static PlanTask Probe(JsonObject rule, string? part) =>
        new("probe", "", null, 0, 1, false, null, null, false, null, null, TaskRule.Read(rule), part);

    /// <summary>The outcome written into the model (one undo step with its command): tasks added, replaced and removed, links likewise.</summary>
    public static void Apply(ICommandContext ctx, Outcome o)
    {
        var (schedule, calendar) = PlanningModule.EnsureSchedule(ctx);
        var sAttrs = o.Schedule.CloneAttrs();
        sAttrs["calendar"] = calendar.Id;
        ctx.Replace(schedule.With(attrs: sAttrs));
        foreach (var id in o.Removed) if (ctx.Model.Get(id) is { Class: Plan.TaskClass }) ctx.Remove(id);
        foreach (var t in o.Tasks)
        {
            var attrs = t.CloneAttrs();
            attrs["schedule"] = schedule.Id;
            var e = new Entity(t.Id, t.Class, t.Name, attrs, t.PredefinedType);
            if (ctx.Model.Get(t.Id) is { } old)
            {
                if (old.Name != e.Name || old.PredefinedType != e.PredefinedType || old.Attrs.ToJsonString() != e.Attrs.ToJsonString()) ctx.Replace(e);
            }
            else ctx.Add(e);
        }
        var want = o.Links.ToDictionary(r => r.Id);
        foreach (var r in ctx.Model.Relations.Where(r => r.Class == Plan.SequenceClass).ToList())
            if (!want.ContainsKey(r.Id)) ctx.RemoveRelation(r.Id);
        foreach (var r in o.Links)
        {
            if (ctx.Model.GetRelation(r.Id) is { } old)
            {
                if (old.Relating != r.Relating || !old.Related.SequenceEqual(r.Related) || old.Attrs?.ToJsonString() != r.Attrs?.ToJsonString()) ctx.ReplaceRelation(r);
            }
            else ctx.AddRelation(r);
        }
    }
}
