using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// A task's rule (docs/plan-4d5d.md, stage 2): the filter its elements are found by, as a schedule's filter is — the IFC
/// classes, the types, the storeys, the materials (an element with any of them), the groups and the phases created in —
/// every condition given must hold, and one left empty lets everything through. Stored on the task; the elements are worked
/// out from the model on every ask, so a wall drawn today joins the task "Masonry first floor" by itself.
/// </summary>
public sealed record TaskRule(IReadOnlyList<string> Classes, IReadOnlyList<string> Types, IReadOnlyList<string> Storeys,
    IReadOnlyList<string> Materials, IReadOnlyList<string> Groups, IReadOnlyList<string> Phases, IReadOnlyList<string>? DemolishedIn = null)
{
    public static readonly string[] Keys = ["classes", "types", "storeys", "materials", "groups", "phases", "demolished"];

    /// <summary>The phases its elements are demolished in (stage 3: a demolition task's rule); empty: demolished or not.</summary>
    public IReadOnlyList<string> Demolished => DemolishedIn ?? [];

    static List<string> Strings(JsonNode? n) => n is JsonArray a ? a.Select(x => (string?)x).OfType<string>().Where(s => s.Length > 0).Distinct().ToList() : [];

    /// <summary>A rule as stored (or as a command gives it); null when there is none.</summary>
    public static TaskRule? Read(JsonNode? n) => n is JsonObject o
        ? new(Strings(o["classes"]), Strings(o["types"]), Strings(o["storeys"]), Strings(o["materials"]), Strings(o["groups"]), Strings(o["phases"]), Strings(o["demolished"]))
        : null;

    IEnumerable<(string Key, IReadOnlyList<string> Values)> Lists() =>
        [("classes", Classes), ("types", Types), ("storeys", Storeys), ("materials", Materials), ("groups", Groups), ("phases", Phases), ("demolished", Demolished)];

    /// <summary>The rule as stored: only the conditions it has.</summary>
    public JsonObject ToJson()
    {
        var o = new JsonObject();
        foreach (var (k, v) in Lists()) if (v.Count > 0) o[k] = new JsonArray(v.Select(x => (JsonNode)x).ToArray());
        return o;
    }

    public bool Matches(ElementFacts f, string defaultPhase) =>
        (Classes.Count == 0 || Classes.Contains(f.Element.Class))
        && (Types.Count == 0 || f.Type != null && Types.Contains(f.Type))
        && (Storeys.Count == 0 || f.Storey != null && Storeys.Contains(f.Storey))
        && (Materials.Count == 0 || Materials.Any(f.Materials.Contains))
        && (Groups.Count == 0 || f.Group != null && Groups.Contains(f.Group))
        && (Phases.Count == 0 || Phases.Contains(f.Phase ?? defaultPhase))
        && (Demolished.Count == 0 || f.Demolished != null && Demolished.Contains(f.Demolished));
}

/// <summary>
/// One layer of an element as a task may take it: its place, whether it is the core, its name and its materials (its own and
/// those of its pieces up its height); <see cref="Keys"/> the element's parts it is drawn and measured as when they are not
/// <c>layer:&lt;n&gt;</c> (a roof's deck and covering, decision 332; one ending in ":" a prefix).
/// </summary>
public sealed record LayerFact(int Index, bool Core, string Name, IReadOnlyList<string> Materials, IReadOnlyList<string>? Keys = null)
{
    /// <summary>The keys the simulation gives this layer's days by: its parts', else <c>layer:&lt;n&gt;</c>.</summary>
    public IReadOnlyList<string> PartKeys => Keys ?? [$"layer:{Index}"];
}

/// <summary>
/// What a rule reads of an element: its type, storey (a door's or window's that of the wall it is in), group, the phases it
/// is created and demolished in, its layers (an element without layers is one layer, its core, of its own material) and
/// every material in it.
/// </summary>
public sealed record ElementFacts(Entity Element, string? Type, string? Storey, string? Group, string? Phase, string? Demolished,
    IReadOnlyList<LayerFact> Layers, IReadOnlySet<string> Materials)
{
    /// <summary>Every layer of the element, as a mask.</summary>
    public ulong All => Layers.Count >= 64 ? ulong.MaxValue : (1UL << Math.Max(1, Layers.Count)) - 1;
}

/// <summary>
/// The part of its elements a task takes (docs/plan-4d5d.md: a layered wall is built in one task and plastered in another):
/// all of it (null), its core, its finishes (every layer but the core), the layers of one material, or of one category of
/// material (Finish, Insulation…). An element without layers is its own core and has no finishes.
/// </summary>
public static class TaskParts
{
    public const string Core = "core", Finishes = "finishes", MaterialPrefix = "material:", CategoryPrefix = "category:";

    /// <summary>A part as a command gives it, checked; null for the whole element.</summary>
    public static string? Parse(JsonNode? n)
    {
        var s = ((string?)n)?.Trim();
        if (string.IsNullOrEmpty(s) || s == "all") return null;
        if (s is Core or Finishes) return s;
        if (s.StartsWith(MaterialPrefix, StringComparison.Ordinal) && s.Length > MaterialPrefix.Length) return s;
        if (s.StartsWith(CategoryPrefix, StringComparison.Ordinal) && s.Length > CategoryPrefix.Length) return s;
        throw new CommandException("A task takes all of its elements, their core, their finishes, a material (material:<id>) or a category of material (category:Finish).");
    }

    /// <summary>The layers of an element a part takes, as a mask (0: the element has no such part).</summary>
    public static ulong Mask(string? part, ElementFacts f, IModelView m)
    {
        if (part == null) return f.All;
        ulong mask = 0;
        foreach (var l in f.Layers)
        {
            if (l.Index >= 64) break;
            var takes = part switch
            {
                Core => l.Core,
                Finishes => !l.Core,
                _ when part.StartsWith(MaterialPrefix, StringComparison.Ordinal) => l.Materials.Contains(part[MaterialPrefix.Length..]),
                _ when part.StartsWith(CategoryPrefix, StringComparison.Ordinal) => l.Materials.Any(x => InCategory(m, x, part[CategoryPrefix.Length..])),
                _ => false,
            };
            if (takes) mask |= 1UL << l.Index;
        }
        return mask;
    }

    /// <summary>
    /// The layer of an element a part key (of its geometry or of a quantity set) is of: <c>layer:2</c> or
    /// <c>layer:2:&lt;material&gt;</c> its index, a zone (<c>zone:…</c>) the core, a key one of its layers is drawn as (a roof's
    /// <c>deck</c>, <c>timber:rafter:3</c>) that layer; null when none.
    /// </summary>
    public static int? LayerOf(ElementFacts f, string key)
    {
        if (key.StartsWith("layer:", StringComparison.Ordinal))
        {
            var rest = key[6..];
            var colon = rest.IndexOf(':');
            return int.TryParse(colon < 0 ? rest : rest[..colon], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i) ? i : null;
        }
        if (key.StartsWith("zone:", StringComparison.Ordinal)) return f.Layers.FirstOrDefault(l => l.Core)?.Index;
        foreach (var l in f.Layers)
            if (l.Keys is { } keys && BuildupLayer.KeyMatches(keys, key)) return l.Index;
        return null;
    }

    /// <summary>Whether a material is of a category: its own, else (for Finish) one by its name, as view styles tell finishes.</summary>
    public static bool InCategory(IModelView m, string materialId, string category)
    {
        if (m.Get(materialId) is not { Class: IfcClass.Material } mat) return false;
        if (mat.Str("category") is { Length: > 0 } c) return string.Equals(c.Trim(), category, StringComparison.OrdinalIgnoreCase);
        return string.Equals(category, "Finish", StringComparison.OrdinalIgnoreCase) && Platform.Materials.IsFinish(mat);
    }
}

/// <summary>
/// The links between the schedule's tasks and the model's elements, worked out from each task's rule and exceptions
/// (<c>rule</c>, <c>add</c>, <c>remove</c>, <c>part</c> on the IfcTask): for each task the elements it takes with the layers
/// of each (a mask), and for each element the layers some task takes. Derived on every ask, never stored.
/// </summary>
public sealed class TaskElements
{
    /// <summary>Classes that are never built by a task: openings, rooms, grids, drawings over the model, shared bodies.</summary>
    static readonly HashSet<string> NotBuilt =
    [
        IfcClass.OpeningElement, IfcClass.Space, IfcClass.Annotation, IfcClass.Grid, "IfcVirtualElement", "LapinOrthophoto", StoredBodies.SharedClass,
        "IfcElementAssembly",
    ];

    readonly IModelView m;
    readonly IServices services;
    readonly Func<string, ClassDescriptor?> describe;
    readonly Dictionary<string, ElementFacts?> facts = [];
    readonly string defaultPhase;
    readonly PhaseSettings phases;

    /// <summary>Each task's elements with the layers it takes of each (a summary's own only: <see cref="Under"/> adds its tasks').</summary>
    public Dictionary<string, Dictionary<string, ulong>> ByTask { get; } = [];
    /// <summary>Each element's layers some task takes.</summary>
    public Dictionary<string, ulong> Covered { get; } = [];
    /// <summary>The elements that could be built by a task, in the model's order.</summary>
    public List<ElementFacts> Candidates { get; } = [];
    readonly HashSet<string> candidateIds = [];
    readonly Plan plan;

    TaskElements(IModelView m, IServices services, Func<string, ClassDescriptor?> describe, Plan plan)
    {
        this.m = m;
        this.services = services;
        this.describe = describe;
        this.plan = plan;
        phases = PhasesModule.Settings(m);
        defaultPhase = phases.DefaultCreated?.Id ?? "";
    }

    public static TaskElements Resolve(ICommandContext ctx, Plan? plan = null) => Resolve(ctx.Model, ctx.Services, ctx.Describe, plan);
    public static TaskElements Resolve(IQueryContext q, Plan? plan = null) => Resolve(q.Model, q.Services, q.Describe, plan);

    public static TaskElements Resolve(IModelView m, IServices services, Func<string, ClassDescriptor?> describe, Plan? plan = null)
    {
        plan ??= Plan.Read(m);
        var t = new TaskElements(m, services, describe, plan);
        var producers = services.Get<IGeometryProducers>();
        foreach (var e in m.Entities)
            if (t.IsCandidate(e, producers) && t.Facts(e.Id) is { } f) { t.Candidates.Add(f); t.candidateIds.Add(e.Id); }
        foreach (var task in plan.Rows)
        {
            var own = t.Of(task);
            if (own.Count == 0) continue;
            t.ByTask[task.Id] = own;
            foreach (var (id, mask) in own) t.Covered[id] = t.Covered.GetValueOrDefault(id) | mask;
        }
        return t;
    }

    bool IsCandidate(Entity e, IGeometryProducers? producers)
    {
        if (NotBuilt.Contains(e.Class)) return false;
        if (describe(e.Class) is { } d && d.Kind != "element") return false;
        if (!StoredBodies.Has(e) && producers?.Geometry(e.Class) == null) return false;
        // A linked file's element belongs to that file; formwork and scaffolding (created and struck in one phase) live by their hosts' tasks.
        if (e.Str(Platform.Links.Key) != null) return false;
        if (e.Str("phaseCreated") is { } c && c == e.Str("phaseDemolished")) return false;
        return true;
    }

    /// <summary>Whether a candidate is built or demolished by the works, so it needs a task: not one that exists and stays.</summary>
    public bool NeedsTask(ElementFacts f)
    {
        if (f.Demolished != null) return true;
        var created = phases.Phases.FirstOrDefault(p => p.Id == (f.Phase ?? defaultPhase));
        return created?.Kind != "existing";
    }

    /// <summary>What a rule reads of an element (null: not an element a task can take).</summary>
    public ElementFacts? Facts(string id)
    {
        if (facts.TryGetValue(id, out var known)) return known;
        var e = m.Get(id);
        ElementFacts? f = null;
        if (e != null && !NotBuilt.Contains(e.Class) && (describe(e.Class) is not { } d || d.Kind == "element"))
        {
            var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
            var storey = Spatial.StoreyOf(m, e.Id)?.Id;
            if (storey == null && m.RelatingOf(e.Id, IfcClass.RelFillsElement) is { } opening && m.RelatingOf(opening.Id, IfcClass.RelVoidsElement) is { } host)
                storey = Spatial.StoreyOf(m, host.Id)?.Id;
            var layers = LayersOf(e, type);
            f = new ElementFacts(e, type?.Id, storey, Groups.Of(m, e.Id)?.Id, e.Str("phaseCreated"), e.Str("phaseDemolished"), layers,
                layers.SelectMany(l => l.Materials).ToHashSet());
        }
        facts[id] = f;
        return f;
    }

    List<LayerFact> LayersOf(Entity e, Entity? type)
    {
        string NameOf(string material) => m.Get(material)?.Name ?? material;
        if (LayerSets.BandsOf(m, e.Id) is { Count: > 0 } bands)
            return bands.Select(b => new LayerFact(b.Index, b.Core, b.Name, [b.Material, .. (b.Parts ?? []).Select(p => p.Material)])).ToList();
        if (type != null && LayerSets.LayersOf(m, type.Id) is { Count: > 0 } layers)
        {
            var core = Layers.CoreIndex(layers);
            return layers.Select((l, i) => new LayerFact(i, i == core, l.Name ?? NameOf(l.Material), [l.Material, .. (l.Parts ?? []).Select(p => p.Material)])).ToList();
        }
        // One layer: the element's own material, its type's, or the one associated with either.
        var materials = new List<string>();
        void Take(string? id) { if (id != null && m.Get(id) is { Class: IfcClass.Material } && !materials.Contains(id)) materials.Add(id); }
        Take(e.Str("material"));
        Take(m.RelatingOf(e.Id, IfcClass.RelAssociatesMaterial)?.Id);
        if (type != null) { Take(type.Str("material")); Take(m.RelatingOf(type.Id, IfcClass.RelAssociatesMaterial)?.Id); }
        // What a module says it is built of: layers of their own when it says which parts each is (a roof's deck or framing
        // and its covering, decision 332), else one layer of every material in it (decision 328).
        var built = BuildUps.Of(m, e, services);
        if (built is { Layers.Count: > 1 } && built.Layers.All(l => l.Keys != null))
            return built.Layers.OrderBy(l => l.Index).Select(l => new LayerFact(l.Index, l.Core, l.Name,
                l.Material is { } lm && m.Get(lm) is { Class: IfcClass.Material } ? [lm, .. l.PartMaterials ?? []] : [], l.Keys)).ToList();
        foreach (var l in built?.Layers ?? []) Take(l.Material);
        return [new LayerFact(0, true, materials.Count > 0 ? NameOf(materials[0]) : e.Name, materials)];
    }

    /// <summary>A task's own elements: those its rule takes, with those added by hand, without those taken off, each with the layers its part takes (none of it: left out).</summary>
    public Dictionary<string, ulong> Of(PlanTask task)
    {
        var o = new Dictionary<string, ulong>();
        if (task.Rule == null && task.Add.Count == 0) return o;
        var removed = task.Remove.ToHashSet();
        void Take(ElementFacts f)
        {
            if (removed.Contains(f.Element.Id) || o.ContainsKey(f.Element.Id)) return;
            var mask = TaskParts.Mask(task.Part, f, m);
            if (mask != 0) o[f.Element.Id] = mask;
        }
        if (task.Rule is { } rule) foreach (var f in Candidates) if (rule.Matches(f, defaultPhase)) Take(f);
        foreach (var id in task.Add) if (Facts(id) is { } f) Take(f);
        return o;
    }

    /// <summary>Whether a task's rule takes an element (its exceptions left aside).</summary>
    public bool RuleTakes(PlanTask task, ElementFacts f) => task.Rule is { } r && candidateIds.Contains(f.Element.Id) && r.Matches(f, defaultPhase);

    /// <summary>A task's elements with those of every task under it (a summary's are its tasks').</summary>
    public HashSet<string> Under(string taskId)
    {
        var o = new HashSet<string>(ByTask.GetValueOrDefault(taskId)?.Keys ?? Enumerable.Empty<string>());
        foreach (var d in plan.Descendants(taskId)) if (ByTask.TryGetValue(d, out var own)) o.UnionWith(own.Keys);
        return o;
    }

    /// <summary>The tasks that take an element, in row order.</summary>
    public IEnumerable<string> TasksOf(string elementId) => plan.Rows.Where(t => ByTask.TryGetValue(t.Id, out var own) && own.ContainsKey(elementId)).Select(t => t.Id);

    /// <summary>The elements that need a task and have none, and those some of whose layers have none (with those layers).</summary>
    public (List<ElementFacts> None, List<(ElementFacts Element, List<LayerFact> Left)> Partial) Unlinked()
    {
        var none = new List<ElementFacts>();
        var partial = new List<(ElementFacts, List<LayerFact>)>();
        foreach (var f in Candidates)
        {
            if (!NeedsTask(f)) continue;
            var covered = Covered.GetValueOrDefault(f.Element.Id);
            if (covered == 0) none.Add(f);
            else if ((covered & f.All) != f.All)
                partial.Add((f, f.Layers.Where(l => l.Index < 64 && (covered & (1UL << l.Index)) == 0).ToList()));
        }
        return (none, partial);
    }

    /// <summary>A rule and part in words, for the workbook and the guide's tables ("Wall, Column · Ground floor · core").</summary>
    public static string Describe(IModelView m, Func<string, ClassDescriptor?> describe, TaskRule? rule, string? part, int added, int removed)
    {
        var bits = new List<string>();
        string Names(IEnumerable<string> ids) => string.Join(", ", ids.Select(id => m.Get(id)?.Name ?? id));
        if (rule != null)
        {
            if (rule.Classes.Count > 0) bits.Add(string.Join(", ", rule.Classes.Select(c => describe(c)?.Label ?? c.Replace("Ifc", ""))));
            if (rule.Types.Count > 0) bits.Add(Names(rule.Types));
            if (rule.Storeys.Count > 0) bits.Add(Names(rule.Storeys));
            if (rule.Materials.Count > 0) bits.Add(Names(rule.Materials));
            if (rule.Groups.Count > 0) bits.Add(Names(rule.Groups));
            if (rule.Phases.Count > 0)
            {
                var ph = PhasesModule.Settings(m);
                bits.Add(string.Join(", ", rule.Phases.Select(p => ph.Phases.FirstOrDefault(x => x.Id == p)?.Name ?? p)));
            }
            if (rule.Demolished.Count > 0)
            {
                var ph = PhasesModule.Settings(m);
                bits.Add("demolished in " + string.Join(", ", rule.Demolished.Select(p => ph.Phases.FirstOrDefault(x => x.Id == p)?.Name ?? p)));
            }
            if (bits.Count == 0) bits.Add("every element");
        }
        if (part != null)
            bits.Add(part switch
            {
                TaskParts.Core => "core",
                TaskParts.Finishes => "finishes",
                _ when part.StartsWith(TaskParts.MaterialPrefix, StringComparison.Ordinal) => m.Get(part[TaskParts.MaterialPrefix.Length..])?.Name ?? part,
                _ => part[TaskParts.CategoryPrefix.Length..],
            });
        if (added > 0) bits.Add($"+{added}");
        if (removed > 0) bits.Add($"−{removed}");
        return string.Join(" · ", bits);
    }
}

/// <summary>
/// A task's links to elements in one object — <c>rule</c>, <c>part</c>, <c>add</c>, <c>remove</c> — as a file carries them
/// (MS Project's Text1 field, IFC's Lapin_Task set) and as the task keeps them.
/// </summary>
public static class TaskLinkData
{
    public static readonly string[] Keys = ["rule", "part", "add", "remove", "marks"];

    /// <summary>What a task holds of its elements; null when it takes none and names no part.</summary>
    public static JsonObject? Of(PlanTask t)
    {
        if (!t.Links && t.Part == null && t.Marks.Count == 0) return null;
        var o = new JsonObject();
        if (t.Rule != null) o["rule"] = t.Rule.ToJson();
        if (t.Part != null) o["part"] = t.Part;
        if (t.Add.Count > 0) o["add"] = new JsonArray(t.Add.Select(x => (JsonNode)x).ToArray());
        if (t.Remove.Count > 0) o["remove"] = new JsonArray(t.Remove.Select(x => (JsonNode)x).ToArray());
        // Its elements marked on site (stage 5), so a schedule exchanged keeps them.
        if (t.Marks.Count > 0) o["marks"] = new JsonObject(t.Marks.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value.ToJson())));
        return o;
    }

    /// <summary>A task's links read from text (JSON); null when it is not theirs.</summary>
    public static JsonObject? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return JsonNode.Parse(text) is JsonObject o && Keys.Any(o.ContainsKey) ? o : null; }
        catch (System.Text.Json.JsonException) { return null; }
    }

    /// <summary>The links written onto a task's attributes, checked (a part Lapin does not know is left out).</summary>
    public static void Write(JsonObject attrs, JsonObject links)
    {
        if (TaskRule.Read(links["rule"]) is { } rule) attrs["rule"] = rule.ToJson();
        try { if (TaskParts.Parse(links["part"]) is { } part) attrs["part"] = part; } catch (CommandException) { }
        foreach (var key in new[] { "add", "remove" })
            if (links[key] is JsonArray a && a.Select(x => (string?)x).OfType<string>().Distinct().ToList() is { Count: > 0 } ids)
                attrs[key] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray());
        if (ElementMark.ReadAll(links["marks"]) is { } marks)
            attrs["marks"] = new JsonObject(marks.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value.ToJson())));
    }
}
