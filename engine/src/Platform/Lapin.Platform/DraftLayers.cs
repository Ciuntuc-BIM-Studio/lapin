using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The project's drafting layers, one list (forum #168, decision 714): <c>draftLayers</c> on the project, what drafted shapes
/// draw by ("by layer"). The layer command, Draft → Layers, a DXF import, Manage → Drafting standards → Layers and a
/// <c>.lapinstandards</c> file all read and write it. A layer is keyed by its name (a drafted shape names its layer); each holds
/// its colour, pen (<c>weight</c>, paper mm), line type, whether it shows and whether it is locked, and the standards' group it
/// is in. The standards show each as a <see cref="DraftLayer"/> whose id is its name; layer "0" is always there.
/// Before decision 714 the standards kept a list of their own (<c>drafting.layers</c>), which nothing drew by;
/// <see cref="Upgrade"/> merges it into this one when an older project opens.
/// </summary>
public static class DraftLayers
{
    public const string Key = "draftLayers";
    /// <summary>A drafted shape's predefined type (the drafting module's <c>Drafts.Predefined</c>).</summary>
    public const string Predefined = "DRAFT";
    public const string Zero = "0";
    public const double DefaultWeight = 0.25;
    public const string DefaultColor = "#18181b";

    /// <summary>The stored entries that have a name, in their order.</summary>
    public static List<JsonObject> Stored(JsonObject? projectAttrs) =>
        (projectAttrs?[Key] as JsonArray ?? []).OfType<JsonObject>().Where(o => NameOf(o) != null).ToList();

    static string? NameOf(JsonObject o) => (o["name"] is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : null) is { Length: > 0 } n ? n : null;

    /// <summary>A stored layer as the standards show it (its line type one of <paramref name="lineTypes"/>, continuous as none).</summary>
    public static DraftLayer ToStandard(JsonObject o, string[] lineTypes)
    {
        var name = NameOf(o) ?? Zero;
        var v = new JsonObject
        {
            ["id"] = name, ["name"] = name, ["color"] = o["color"]?.DeepClone() ?? DefaultColor, ["visible"] = o["visible"]?.DeepClone(), ["locked"] = o["locked"]?.DeepClone(),
            ["group"] = o["group"]?.DeepClone(), ["lineWeight"] = Math.Clamp(Json.Number(o["weight"]) ?? DefaultWeight, 0.05, 2), ["lineType"] = o["lineType"]?.DeepClone(),
        };
        return Norm(Standards.ReadLayer(v, lineTypes));
    }

    /// <summary>A layer as the one list compares it: its id its name, continuous as no line type.</summary>
    public static DraftLayer Norm(DraftLayer l)
    {
        var name = l.Id == Zero ? Zero : l.Name.Trim() is { Length: > 0 } n ? n : l.Id;
        return l with { Id = name, Name = name, LineType = l.LineType == "continuous" ? null : l.LineType, LineWeight = l.LineWeight ?? DefaultWeight };
    }

    /// <summary>The layers a standards' JSON (a file's, an older project's) lists itself, as the one list compares them; layer "0" only when it lists it.</summary>
    public static List<DraftLayer> InFile(JsonObject? standards)
    {
        if (standards?["layers"] is not JsonArray raw) return [];
        var ids = raw.OfType<JsonObject>().Select(x => (string?)(x["id"] is JsonValue iv && iv.TryGetValue<string>(out var i) ? i : null) ?? (x["name"] is JsonValue nv && nv.TryGetValue<string>(out var n) ? n : null) ?? Zero).ToHashSet();
        return [.. Standards.Read(standards).Layers.Where(l => ids.Contains(l.Id)).Select(Norm).DistinctBy(l => l.Name)];
    }

    /// <summary>A standards' layer as stored, keeping what <paramref name="was"/> held that the standards do not show (a line type they do not know).</summary>
    public static JsonObject ToStored(DraftLayer l, JsonObject? was, string[] lineTypes)
    {
        l = Norm(l);
        var keepType = l.LineType == null && was?["lineType"] is JsonValue lv && lv.TryGetValue<string>(out var wt) && wt.Length is > 0 and <= 64
            && !lineTypes.Contains(wt) ? wt : null;
        var o = new JsonObject
        {
            ["name"] = l.Name, ["color"] = l.Color, ["weight"] = Math.Clamp(l.LineWeight ?? Json.Number(was?["weight"]) ?? DefaultWeight, 0.05, 2),
            ["lineType"] = l.LineType ?? keepType ?? "continuous", ["visible"] = l.Visible, ["locked"] = l.Locked,
        };
        if (l.Group != null) o["group"] = l.Group;
        return o;
    }

    /// <summary>The project's layers as the standards show them, layer "0" first when it is not stored.</summary>
    public static List<DraftLayer> Of(JsonObject? projectAttrs, string[] lineTypes)
    {
        var list = new List<DraftLayer>();
        foreach (var o in Stored(projectAttrs))
        {
            var l = ToStandard(o, lineTypes);
            if (list.All(x => x.Name != l.Name)) list.Add(l);
        }
        if (list.All(l => l.Name != Zero)) list.Insert(0, ToStandard(new JsonObject { ["name"] = Zero }, lineTypes));
        return list;
    }

    /// <summary>
    /// The stored list made <paramref name="layers"/> (by name, in their order, the first of a name taken): a layer the same as
    /// stored kept as it is, one changed or new written; one not among them gone (what it held is the caller's to move).
    /// </summary>
    public static JsonArray Merged(JsonObject projectAttrs, IEnumerable<DraftLayer> layers, string[] lineTypes)
    {
        var stored = Stored(projectAttrs);
        var o = new JsonArray();
        var seen = new HashSet<string>();
        foreach (var raw in layers)
        {
            var l = Norm(raw);
            if (!seen.Add(l.Name)) continue;
            var was = stored.FirstOrDefault(x => NameOf(x) == l.Name);
            // Layer 0 as Lapin gives it, never stored, stays unstored.
            if (was == null && l.Name == Zero && l == ToStandard(new JsonObject { ["name"] = Zero }, lineTypes)) continue;
            o.Add((JsonNode)(was != null && ToStandard(was, lineTypes) == l ? was.DeepClone() : ToStored(l, was, lineTypes)));
        }
        return o;
    }

    /// <summary>The drafted shapes on layer <paramref name="from"/> moved to <paramref name="to"/> (a layer renamed, or removed: to "0").</summary>
    public static void Repoint(ICommandContext ctx, string from, string to)
    {
        foreach (var e in ctx.Model.OfClass(IfcClass.Annotation).Where(e => e.PredefinedType == Predefined && e.Str("layer") == from).ToList())
        {
            var at = e.CloneAttrs();
            at["layer"] = to;
            ctx.Replace(e.With(attrs: at));
        }
    }

    /// <summary>
    /// A project saved before decision 714: the standards' own layers (<c>drafting.layers</c>) merged into the one list, by name
    /// (ignoring case). A layer both lists hold keeps the drafting layer's colour, pen, line type and switches (what its shapes drew
    /// by), taking the standards' group when it has none; one only the standards held is added with its values; layer "0" as
    /// Lapin gives it adds nothing. The standards then keep no list of their own. Whether anything changed.
    /// </summary>
    public static bool Upgrade(JsonObject projectAttrs)
    {
        if (projectAttrs["drafting"] is not JsonObject drafting || drafting["layers"] is not JsonArray) return false;
        var book = Standards.Read(drafting);
        var names = Standards.LineTypeNames(book);
        var list = (projectAttrs[Key] as JsonArray ?? []).Select(x => x?.DeepClone()).OfType<JsonNode>().ToList();
        JsonObject? Find(string name) => list.OfType<JsonObject>().FirstOrDefault(x => string.Equals(NameOf(x), name, StringComparison.OrdinalIgnoreCase));
        var defaultZero = Norm(Standards.Layer0);
        foreach (var l in InFile(drafting))
        {
            if (Find(l.Name) is { } have)
            {
                if (have["group"] is null && l.Group != null) have["group"] = l.Group;
                continue;
            }
            if (l.Name == Zero && l == defaultZero) continue;
            var stored = ToStored(l, null, names);
            if (l.Name == Zero) list.Insert(0, stored); else list.Add(stored);
        }
        drafting.Remove("layers");
        projectAttrs[Key] = new JsonArray([.. list]);
        return true;
    }

    /// <summary>The upgrade of an older project's standards' layers (<see cref="Upgrade"/>).</summary>
    public sealed class ListUpgrade : IProjectUpgrade
    {
        public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
        {
            if (entities.FirstOrDefault(e => e.Class == IfcClass.Project) is not { } project) return (entities, relations);
            var attrs = project.CloneAttrs();
            if (!DraftLayers.Upgrade(attrs)) return (entities, relations);
            var upgraded = project.With(attrs: attrs);
            return (entities.Select(e => e.Id == project.Id ? upgraded : e).ToList(), relations);
        }
    }
}
