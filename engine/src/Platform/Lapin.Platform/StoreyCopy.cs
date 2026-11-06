using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What a module adds to Copy to storeys: what goes with an element copied with what it hosts (a room's finishes, a
/// stair's space under it), which of its attributes name other elements as relations (a wall attached to a roof: made
/// again among the copies only when relations are), and what it puts right once the copies stand on their storey (a
/// stair's opening in the floor above it).
/// </summary>
public interface IStoreyCopy
{
    /// <summary>The elements on a storey that go with <paramref name="e"/> when hosted things are copied.</summary>
    IEnumerable<Entity> Riders(IModelView m, Entity e) => [];

    /// <summary>The attributes that hold relations to other elements, remapped to their copies only when relations are rebuilt.</summary>
    IEnumerable<string> RelationKeys => [];

    /// <summary>After the copies of one storey are placed on it, in the same undo step.</summary>
    void Copied(ICommandContext ctx, StoreyCopyStep step) { }
}

/// <summary>One storey's copies: each original's copy (the hosted openings, doors, windows and assembly parts too), the options.</summary>
public sealed record StoreyCopyStep(Entity Target, IReadOnlyDictionary<string, string> Map, string Keep, bool Hosted, bool Relations);

/// <summary>
/// Copy to storeys (ArchiCAD's copy to stories, Revit's paste aligned to selected levels): the selection copied onto
/// each storey chosen, in one undo step, through element.transform's copy (the type shared, the doors and windows
/// with their walls, the joins among the copies, groups as groups) and placed as Move to storey places
/// (<see cref="StoreyMove.Move"/>): kept with the storey (the default), the copy stands at the same height over its
/// new storey; kept in place (world), where the original is in space. With what they host, the tags and dimensions
/// of the copied elements on their storey's plan go to the new storey's plan, and what a module names (a room's
/// finishes) goes too; with relations, a copy names the copies of the elements its original named (a wall attached
/// to a roof copied with it). An element the storey already holds the same, at the same place, is skipped and
/// reported (skip duplicates, on by default).
/// </summary>
public static class StoreyCopy
{
    public static void Register(IModuleRegistry r)
    {
        // {ids, storeyIds, keep?: "storey" | "world", hosted?: true, relations?: true, skipDuplicates?: true}
        // → {ids: every copy, storeys: [{storeyId, ids}], skipped: [{storeyId, id, name}]}.
        r.Command("element.copyToStoreys", "Copy to storeys", (ctx, a) =>
        {
            var m = ctx.Model;
            var targets = (a["storeyIds"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>().Distinct()
                .Select(id => Spatial.Require(m, id, IfcClass.BuildingStorey)).OrderBy(s => s.Num("elevation")).ToList();
            if (targets.Count == 0) throw new CommandException("Choose the storeys to copy to.");
            var keep = (string?)a["keep"] ?? "storey";
            if (keep is not ("storey" or "world")) throw new CommandException("Keep the copies with their storey (storey) or where the originals are (world).");
            static bool Flag(JsonNode? n, bool otherwise) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : otherwise;
            var hosted = Flag(a["hosted"], true);
            var relations = Flag(a["relations"], true);
            var skipDuplicates = Flag(a["skipDuplicates"], true);

            var (ids, groups) = Groups.Expand(m, Args.Ids(a));
            var picked = ids.Distinct().Select(id => m.Get(id) ?? throw new CommandException($"There is no {id}.")).ToList();
            var chosen = picked.Select(e => e.Id).ToHashSet();
            var top = new List<Entity>();
            var notes = new List<Entity>();
            foreach (var e in picked)
            {
                if (e.Class == IfcClass.Annotation) { if (e.Str("viewId") != null) notes.Add(e); continue; }
                if (e.Class is IfcClass.Project or IfcClass.Site or IfcClass.Building or IfcClass.BuildingStorey) continue;
                // A door or window goes with its wall, an opening with what it voids, a part with its assembly.
                if ((StoreyMove.Host(m, e) ?? (e.Class == IfcClass.OpeningElement ? m.RelatingOf(e.Id, IfcClass.RelVoidsElement) : null)) is { } host)
                {
                    if (!chosen.Contains(host.Id)) throw new CommandException($"„{e.Name}” is copied with „{host.Name}”: select that.");
                    continue;
                }
                if (AssemblyOf(m, e) is { } whole && chosen.Contains(whole.Id)) continue;
                if (StoredBodies.Has(e)) throw new CommandException($"„{e.Name}” is a reference from an IFC file: it stays where the file put it.");
                if (Spatial.StoreyOf(m, e.Id) is null) throw new CommandException($"„{e.Name}” is on no storey.");
                top.Add(e);
            }
            if (top.Count == 0 && notes.Count == 0) throw new CommandException("Select what to copy to other storeys.");
            var hooks = ctx.Services.All<IStoreyCopy>().ToList();
            // What a module names as going with an element (a room's finishes) goes with it.
            if (hosted)
            {
                var have = top.Select(e => e.Id).ToHashSet();
                foreach (var e in top.ToList())
                    foreach (var rider in hooks.SelectMany(h => h.Riders(m, e)))
                        if (have.Add(rider.Id) && Spatial.StoreyOf(m, rider.Id) != null && !StoredBodies.Has(rider)) top.Add(rider);
            }
            var relationKeys = hooks.SelectMany(h => h.RelationKeys).ToHashSet();

            var made = new JsonArray();
            var perStorey = new JsonArray();
            var skipped = new JsonArray();
            foreach (var target in targets)
            {
                var already = m.RelatedOf(target.Id, IfcClass.RelContainedInSpatialStructure).ToList();
                var use = new List<Entity>();
                foreach (var e in top)
                {
                    // A storey's own elements are not copied onto it.
                    if (Spatial.StoreyOf(m, e.Id) is not { } from || from.Id == target.Id) continue;
                    if (skipDuplicates && already.FirstOrDefault(x => Same(ctx, e, x, from, target, keep)) != null)
                    {
                        skipped.Add((JsonNode)new JsonObject { ["storeyId"] = target.Id, ["id"] = e.Id, ["name"] = e.Name });
                        continue;
                    }
                    use.Add(e);
                }
                var notesHere = notes.Where(n => PlanStorey(m, n) is { } s && s != target.Id).ToList();
                if (use.Count == 0 && notesHere.Count == 0) continue;
                var notesBefore = m.OfClass(IfcClass.Annotation).Select(x => x.Id).ToHashSet();
                var map = new Dictionary<string, string>();
                var twins = new Dictionary<string, string>();
                if (use.Count > 0)
                {
                    var copyIds = new JsonArray(use.Select(e => (JsonNode)e.Id).ToArray());
                    var result = ElementTransform.Run(ctx, new JsonObject { ["ids"] = copyIds, ["op"] = "copy", ["by"] = new JsonArray(0.0, 0.0) });
                    foreach (var (o, c) in result.Copies) if (c != o && m.Get(c) != null) map[o] = c;
                }
                var tops = map.ToList();
                // An assembly goes whole: its parts copied with it, unless its module made them for its copy.
                foreach (var (o, c) in tops) CopyParts(ctx, o, c, map);
                foreach (var (o, c) in map.ToList()) MapHosted(m, o, c, map);
                var placed = tops.Select(p => m.Get(p.Value)).OfType<Entity>().ToList();
                StoreyMove.Move(ctx, placed, target, keep);
                // What hosts nothing new: the doors, windows and openings left behind.
                if (!hosted)
                {
                    foreach (var (_, c) in tops)
                        foreach (var o in m.RelatedOf(c, IfcClass.RelVoidsElement).Where(o => o.Str("stairId") == null).ToList()) ctx.Remove(o.Id);
                    foreach (var k in map.Where(p => m.Get(p.Value) == null).Select(p => p.Key).ToList()) map.Remove(k);
                }
                // A group copied whole makes a group of its copies, when relations are rebuilt.
                if (relations)
                    foreach (var g in groups)
                        if (m.Get(g) is { } group && Groups.Make(ctx, group.Name, Groups.MembersOf(m, g).Where(map.ContainsKey).Select(x => map[x]).ToList()) is { } twin)
                            twins[g] = twin.Id;
                if (!relations)
                {
                    var copied = map.Values.ToHashSet();
                    foreach (var rel in copied.SelectMany(id => m.RelationsOf(id, Joins.Class)).DistinctBy(x => x.Id).ToList()) ctx.RemoveRelation(rel.Id);
                }
                // A copy names its fellow copies: a door's opening, a finish's room, a stair's opening (relations: a wall's roof).
                var names = new Dictionary<string, string>(map);
                foreach (var (g, t) in twins) names[g] = t;
                foreach (var id in map.Values.ToList())
                    if (m.Get(id) is { } c && Remap(c.Attrs, names, relations ? null : relationKeys) is { } attrs) ctx.Replace(c.With(attrs: attrs));
                // The tags and dimensions of the copied elements on their storey's plan, and what was selected on a plan, go to the new storey's plan.
                var fresh = m.OfClass(IfcClass.Annotation).Where(x => !notesBefore.Contains(x.Id)).ToList();
                foreach (var n in fresh)
                    if (PlanStorey(m, n) is { } s && s != target.Id && PlanOn(m, n.Str("viewId")!, target) is { } view)
                    {
                        var na = Remap(n.Attrs, names, null) ?? n.CloneAttrs();
                        na["viewId"] = view;
                        ctx.Replace(n.With(attrs: na));
                    }
                var copiedNotes = new List<string>();
                var originals = map.Keys.ToHashSet();
                var withNotes = hosted ? m.OfClass(IfcClass.Annotation).Where(n => notesBefore.Contains(n.Id) && Follows(m, n, originals)).ToList() : [];
                foreach (var n in notesHere.Concat(withNotes).DistinctBy(n => n.Id).ToList())
                {
                    if (PlanStorey(m, n) is not { } s || s == target.Id || PlanOn(m, n.Str("viewId")!, target) is not { } view) continue;
                    var na = Remap(n.Attrs, names, null) ?? n.CloneAttrs();
                    na["viewId"] = view;
                    var twin = new Entity(ctx.NewId(), n.Class, n.Name, na, n.PredefinedType);
                    ctx.Add(twin);
                    foreach (var rel in m.RelationsOf(n.Id).Where(x => x.Related.Contains(n.Id) && x.Class != Groups.Rel).ToList()) ctx.Relate(rel.Class, rel.Relating, twin.Id);
                    copiedNotes.Add(twin.Id);
                }
                var step = new StoreyCopyStep(target, map, keep, hosted, relations);
                foreach (var h in hooks) h.Copied(ctx, step);
                var here = new JsonArray();
                foreach (var (_, c) in tops) if (m.Get(c) != null) here.Add((JsonNode)c);
                foreach (var t in twins.Values) if (m.Get(t) != null) here.Add((JsonNode)t);
                foreach (var n in copiedNotes) here.Add((JsonNode)n);
                foreach (var id in here) made.Add((JsonNode)(string)id!);
                perStorey.Add((JsonNode)new JsonObject { ["storeyId"] = target.Id, ["ids"] = here });
            }
            return new JsonObject { ["ids"] = made, ["storeys"] = perStorey, ["skipped"] = skipped };
        });
    }

    /// <summary>The assembly an element is a part of (an element aggregating it, not the spatial structure).</summary>
    static Entity? AssemblyOf(IModelView m, Entity e) =>
        m.RelatingOf(e.Id, IfcClass.RelAggregates) is { } whole && whole.Class is not (IfcClass.Project or IfcClass.Site or IfcClass.Building or IfcClass.BuildingStorey) ? whole : null;

    /// <summary>An assembly's parts copied for its copy (each with its own parts), when its module did not make them.</summary>
    static void CopyParts(ICommandContext ctx, string original, string copy, Dictionary<string, string> map)
    {
        var m = ctx.Model;
        var parts = m.RelatedOf(original, IfcClass.RelAggregates).ToList();
        if (parts.Count == 0) return;
        var own = m.RelatedOf(copy, IfcClass.RelAggregates).ToList();
        if (own.Count > 0)
        {
            // The module made its copy's parts (a Studio box's slabs): matched in order, class by class.
            foreach (var g in parts.GroupBy(p => p.Class))
                foreach (var (p, c) in g.Zip(own.Where(x => x.Class == g.Key))) map[p.Id] = c.Id;
            return;
        }
        foreach (var p in parts)
        {
            if (map.ContainsKey(p.Id)) continue;
            var c = Placement.Copy(ctx, p, Placement2D.Move(default));
            if (c == p.Id || m.Get(c) == null) continue;
            ctx.Relate(IfcClass.RelAggregates, copy, c);
            map[p.Id] = c;
            CopyParts(ctx, p.Id, c, map);
        }
    }

    /// <summary>What a copy hosts matched with what its original hosts (openings, then their doors and windows), in order.</summary>
    static void MapHosted(IModelView m, string original, string copy, Dictionary<string, string> map)
    {
        foreach (var rel in new[] { IfcClass.RelVoidsElement, IfcClass.RelFillsElement })
        {
            var mine = m.RelatedOf(original, rel).ToList();
            var theirs = m.RelatedOf(copy, rel).ToList();
            foreach (var g in mine.GroupBy(x => x.Class))
                foreach (var (o, c) in g.Zip(theirs.Where(x => x.Class == g.Key)))
                    if (map.TryAdd(o.Id, c.Id)) MapHosted(m, o.Id, c.Id, map);
        }
    }

    /// <summary>The attributes with every element named by its copy (null: nothing named one); <paramref name="except"/>: keys left alone.</summary>
    public static JsonObject? Remap(JsonObject attrs, IReadOnlyDictionary<string, string> map, IReadOnlySet<string>? except)
    {
        var changed = false;
        JsonNode? Walk(JsonNode? n)
        {
            switch (n)
            {
                case JsonValue v when v.TryGetValue<string>(out var s) && map.TryGetValue(s, out var to):
                    changed = true;
                    return JsonValue.Create(to);
                case JsonArray arr:
                    return new JsonArray(arr.Select(Walk).ToArray());
                case JsonObject o:
                    var x = new JsonObject();
                    foreach (var (k, val) in o) x[k] = Walk(val);
                    return x;
                default:
                    return n?.DeepClone();
            }
        }
        var result = new JsonObject();
        foreach (var (k, v) in attrs) result[k] = except?.Contains(k) == true ? v?.DeepClone() : Walk(v);
        return changed ? result : null;
    }

    /// <summary>The storey whose plan an annotation is drawn on (the implicit plan or a plan view of it), else null.</summary>
    static string? PlanStorey(IModelView m, Entity n) =>
        n.Str("viewId") is { } vid && Views.Find(m, vid) is { } v && (v.Str("kind") ?? "plan") == "plan" && SitePlans.Of(v) == null ? v.Str("storeyId") : null;

    /// <summary>The plan on <paramref name="target"/> that answers a view of another storey: one of the same discipline, else the storey's own plan.</summary>
    static string? PlanOn(IModelView m, string viewId, Entity target)
    {
        if (viewId.StartsWith("plan:", StringComparison.Ordinal)) return "plan:" + target.Id;
        var v = m.Get(viewId);
        var same = m.OfClass(Views.Class).FirstOrDefault(x => (x.Str("kind") ?? "plan") == "plan" && x.Str("storeyId") == target.Id && SitePlans.Of(x) == null
            && x.Str("discipline") == v?.Str("discipline") && x.Attrs["reflected"]?.ToString() == v?.Attrs["reflected"]?.ToString());
        return same?.Id ?? "plan:" + target.Id;
    }

    /// <summary>Whether an annotation follows the elements copied: it names one of them, and every placed element it names is copied.</summary>
    static bool Follows(IModelView m, Entity n, IReadOnlySet<string> copied)
    {
        if (n.Str("viewId") == null) return false;
        var named = new List<string>();
        void Walk(JsonNode? x)
        {
            switch (x)
            {
                case JsonValue v when v.TryGetValue<string>(out var s) && s.Length > 8 && m.Get(s) is { } e && e.Class != Views.Class && Spatial.ShownOn(m, e) != null:
                    named.Add(s);
                    break;
                case JsonArray arr: foreach (var y in arr) Walk(y); break;
                case JsonObject o: foreach (var (_, y) in o) Walk(y); break;
            }
        }
        Walk(n.Attrs);
        return named.Count > 0 && named.All(copied.Contains);
    }

    /// <summary>Whether <paramref name="x"/>, on the target storey, is <paramref name="e"/> as its copy would stand there: its class, type and attributes.</summary>
    static bool Same(ICommandContext ctx, Entity e, Entity x, Entity from, Entity target, string keep)
    {
        var m = ctx.Model;
        if (x.Id == e.Id || x.Class != e.Class || x.PredefinedType != e.PredefinedType) return false;
        if (m.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.Id != m.RelatingOf(x.Id, IfcClass.RelDefinesByType)?.Id) return false;
        var expected = StoreyMove.Rehomed(ctx, e, from, target, keep) ?? e.Attrs;
        return Alike(m, expected, x.Attrs);
    }

    /// <summary>Two attribute trees alike: numbers within a tenth of a millimetre, names of elements of the same class.</summary>
    static bool Alike(IModelView m, JsonNode? a, JsonNode? b)
    {
        switch (a, b)
        {
            case (null, null): return true;
            case (JsonObject oa, JsonObject ob):
                return oa.Count == ob.Count && oa.All(p => ob.TryGetPropertyValue(p.Key, out var q) && Alike(m, p.Value, q));
            case (JsonArray xa, JsonArray xb):
                return xa.Count == xb.Count && xa.Zip(xb).All(p => Alike(m, p.First, p.Second));
            case (JsonValue va, JsonValue vb):
                if (Json.Number(va) is { } na && Json.Number(vb) is { } nb) return Math.Abs(na - nb) < 1e-4;
                if (va.TryGetValue<string>(out var sa) && vb.TryGetValue<string>(out var sb))
                    return sa == sb || (m.Get(sa) is { } ea && m.Get(sb) is { } eb && ea.Class == eb.Class);
                return JsonNode.DeepEquals(va, vb);
            default: return false;
        }
    }
}
