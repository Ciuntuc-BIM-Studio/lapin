using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>What <see cref="ImportStoreys.Replace"/> did to the project's own storeys: merged into the file's, removed empty, kept with what they hold.</summary>
public sealed record StoreyReplacement(IReadOnlyList<(string Storey, string Into)> Merged, IReadOnlyList<string> Removed, IReadOnlyList<string> Kept)
{
    public static readonly StoreyReplacement None = new([], [], []);

    public bool Any => Merged.Count + Removed.Count + Kept.Count > 0;

    /// <summary>The report's sentences, for the import's notes.</summary>
    public IEnumerable<string> Notes()
    {
        if (Merged.Count > 0) yield return $"The file's storeys replace the project's: {string.Join(", ", Merged.Select(m => $"„{m.Storey}” merged into „{m.Into}”"))} (at the same level).";
        else if (Removed.Count > 0) yield return "The file's storeys replace the project's.";
        if (Removed.Count > 0) yield return $"Empty storey(s) removed: {string.Join(", ", Removed.Select(n => $"„{n}”"))}.";
        if (Kept.Count > 0) yield return $"Kept, since they hold elements and match no storey of the file: {string.Join(", ", Kept.Select(n => $"„{n}”"))}.";
    }

    public JsonObject ToJson() => new()
    {
        ["merged"] = new JsonArray(Merged.Select(m => (JsonNode)new JsonObject { ["storey"] = m.Storey, ["into"] = m.Into }).ToArray()),
        ["removed"] = new JsonArray(Removed.Select(n => (JsonNode)n).ToArray()),
        ["kept"] = new JsonArray(Kept.Select(n => (JsonNode)n).ToArray()),
    };
}

/// <summary>
/// The rule for importing a file into a project (IFC, Fragments, BIM Open Schema; never a link, which keeps its own):
/// the file's storeys replace the project's. A project storey at the level of one of the file's (within
/// <see cref="Tolerance"/>) is merged into it: what it holds (its elements, kept where they are in space; its spaces), what
/// refers to it (views, plans and their drafting, storeys shown) go to the file's storey, which keeps the file's name and
/// GlobalId. A project storey at no level of the file's that holds nothing (no element, no drafting on its plan) is
/// removed, its views retargeted to the file's nearest storey; one that holds something stays, and the report says so.
/// Part of the import's own edit, so one undo step.
/// </summary>
public static class ImportStoreys
{
    /// <summary>How close two levels are to count as the same storey (m).</summary>
    public const double Tolerance = 0.05;

    public static StoreyReplacement Replace(IEdit edit, IServices services, IEnumerable<string> fileStoreyIds)
    {
        var m = edit.Model;
        var file = fileStoreyIds.Distinct().Select(m.Get).OfType<Entity>().Where(s => s.Class == IfcClass.BuildingStorey).ToList();
        if (file.Count == 0) return StoreyReplacement.None;
        var fileIds = file.Select(s => s.Id).ToHashSet();
        var own = Spatial.Storeys(m).Where(s => !fileIds.Contains(s.Id)).ToList();
        if (own.Count == 0) return StoreyReplacement.None;

        // Who refers to each of the project's storeys by its id (or its default plan's), read once over the model.
        var ownIds = own.Select(s => s.Id).ToHashSet();
        var referers = own.ToDictionary(s => s.Id, _ => new List<string>());
        foreach (var e in m.Entities)
        {
            if (e.Attrs == null || ownIds.Contains(e.Id)) continue;
            foreach (var id in Mentions(e.Attrs, ownIds)) if (!referers[id].Contains(e.Id)) referers[id].Add(e.Id);
        }
        Entity Nearest(Entity s) => file.OrderBy(f => Math.Abs(f.Num("elevation") - s.Num("elevation"))).ThenBy(f => f.Num("elevation")).First();

        var merged = new List<(string, string)>();
        var removed = new List<string>();
        var kept = new List<string>();
        foreach (var s in own)
        {
            var near = Nearest(s);
            if (Math.Abs(near.Num("elevation") - s.Num("elevation")) <= Tolerance + 1e-9)
            {
                Merge(edit, services, s, near, referers[s.Id]);
                merged.Add((s.Name ?? s.Id, near.Name ?? near.Id));
            }
            else if (Holds(m, s, referers[s.Id]))
                kept.Add(s.Name ?? s.Id);
            else
            {
                Retarget(edit, s, near, referers[s.Id]);
                edit.Remove(s.Id);
                removed.Add(s.Name ?? s.Id);
            }
        }
        return new StoreyReplacement(merged, removed, kept);
    }

    /// <summary>Whether a storey holds something: elements or spaces on it, an element that names it, drafting on its plan.</summary>
    static bool Holds(IModelView m, Entity s, List<string> referers)
    {
        if (m.RelationsOf(s.Id).Any(r => r.Relating == s.Id && r.Related.Count > 0)) return true;
        var plans = new HashSet<string> { StoreyPlans.Prefix + s.Id };
        foreach (var id in referers)
            if (m.Get(id) is { } e && e.Class == Views.Class) { if (StoreyPlans.IsFloorPlan(e, s.Id)) plans.Add(e.Id); }
            else return true;
        // Drafting on its plan (or on its floor plan view) is what the level holds too.
        return m.Entities.Any(e => e.Class != Views.Class && e.Str("viewId") is { } v && plans.Contains(v));
    }

    /// <summary>A storey into another: its elements kept where they are in space, its other relationships, and what names it.</summary>
    static void Merge(IEdit edit, IServices services, Entity from, Entity into, List<string> referers)
    {
        var m = edit.Model;
        var levels = services.All<IStoreyLevels>();
        foreach (var r in m.RelationsOf(from.Id).Where(r => r.Relating == from.Id).ToList())
        {
            if (r.Class == IfcClass.RelContainedInSpatialStructure)
                foreach (var id in r.Related)
                    if (m.Get(id) is { } e && levels.SelectMany(l => l.LevelsOf(e)).Distinct().ToList() is { Count: > 0 } keys)
                    {
                        var attrs = e.CloneAttrs();
                        foreach (var (key, fromTop) in keys)
                        {
                            double Ref(Entity st) => st.Num("elevation") + (fromTop ? st.Num("height", 3) : 0);
                            attrs[key] = e.Num(key) + Ref(from) - Ref(into);
                        }
                        edit.Replace(e.With(attrs: attrs));
                    }
            edit.RelateAll(r.Class, into.Id, r.Related.ToList());
        }
        Retarget(edit, from, into, referers);
        edit.Remove(from.Id);
    }

    /// <summary>What names <paramref name="from"/> (views, plans, drafting on its default plan, storeys shown) names <paramref name="to"/> instead.</summary>
    static void Retarget(IEdit edit, Entity from, Entity to, List<string> referers)
    {
        var map = new Dictionary<string, string> { [from.Id] = to.Id, [StoreyPlans.Prefix + from.Id] = StoreyPlans.Prefix + to.Id };
        foreach (var id in referers)
        {
            if (edit.Model.Get(id) is not { Attrs: { } a } e) continue;
            // A second floor plan for the file's level, with nothing on it and on no sheet, goes: the level has its own.
            if (StoreyPlans.IsFloorPlan(e, from.Id) && StoreyPlans.Of(edit.Model, to.Id) != null
                && !edit.Model.Entities.Any(x => x.Id != e.Id && x.Attrs != null && Mentions(x.Attrs, [e.Id]).Any()))
            {
                edit.Remove(e.Id);
                continue;
            }
            var attrs = (JsonObject)a.DeepClone();
            if (Swap(attrs, map)) edit.Replace(e.With(attrs: attrs));
        }
    }

    /// <summary>Which of <paramref name="ids"/> an entity's attributes name (as themselves or as their default plan); big numeric arrays (bodies) are not walked.</summary>
    static IEnumerable<string> Mentions(JsonNode node, HashSet<string> ids)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var (_, v) in o) if (v != null) foreach (var x in Mentions(v, ids)) yield return x;
                break;
            case JsonArray a:
                if (a.Count > 0 && a[0] is JsonValue first && first.GetValueKind() == System.Text.Json.JsonValueKind.Number) yield break;
                foreach (var v in a) if (v != null) foreach (var x in Mentions(v, ids)) yield return x;
                break;
            case JsonValue v when v.GetValueKind() == System.Text.Json.JsonValueKind.String:
                var s = v.GetValue<string>();
                if (ids.Contains(s)) yield return s;
                else if (s.StartsWith(StoreyPlans.Prefix, StringComparison.Ordinal) && ids.Contains(s[StoreyPlans.Prefix.Length..])) yield return s[StoreyPlans.Prefix.Length..];
                break;
        }
    }

    /// <summary>The strings <paramref name="map"/> names replaced in place; a list of names keeps each once.</summary>
    static bool Swap(JsonNode node, Dictionary<string, string> map)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject o:
                foreach (var key in o.Select(kv => kv.Key).ToList())
                    if (o[key] is { } v)
                    {
                        if (v is JsonValue jv && jv.GetValueKind() == System.Text.Json.JsonValueKind.String && map.TryGetValue(jv.GetValue<string>(), out var to)) { o[key] = to; changed = true; }
                        else changed |= Swap(v, map);
                    }
                break;
            case JsonArray a:
                var swapped = false;
                for (var i = 0; i < a.Count; i++)
                    if (a[i] is { } v)
                    {
                        if (v is JsonValue jv && jv.GetValueKind() == System.Text.Json.JsonValueKind.String && map.TryGetValue(jv.GetValue<string>(), out var to)) { a[i] = to; swapped = true; }
                        else changed |= Swap(v, map);
                    }
                if (swapped)
                {
                    changed = true;
                    var seen = new HashSet<string>();
                    for (var i = 0; i < a.Count;)
                        if (a[i] is JsonValue s && s.GetValueKind() == System.Text.Json.JsonValueKind.String && !seen.Add(s.GetValue<string>())) a.RemoveAt(i);
                        else i++;
                }
                break;
        }
        return changed;
    }
}
