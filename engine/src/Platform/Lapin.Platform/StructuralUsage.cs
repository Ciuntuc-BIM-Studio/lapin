using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Whether an element is structural (load-bearing), Lapin's one resolver of it (docs/plan-structural-usage.md, decision 866):
/// what the element or its type says, else the rule of the one who asks. What is said, in this order: the element's own
/// attribute <c>loadBearing</c> (the Structural tick), its own Pset_…Common LoadBearing (a parameter the user typed, then the
/// one its IFC file gave it), its type's attribute, its type's Pset value. When nothing says, each consumer keeps its own
/// rule, so nothing changes until the user ticks: the views' (<see cref="ForViews"/>, decisions 500/502: a wall bears load
/// unless IFC calls it or its type a partition; floors, roofs, columns, beams, footings and piles do; coverings and a
/// room's finishes never) and the analysis' (<see cref="ForAnalysis"/>: a wall by its material and core, which the
/// analytical model works out, <c>rule:thickness</c>). The IFC export writes Pset_…Common.LoadBearing from what is said
/// (and from the views' rule when asked to); an import keeps the file's value where the file has it and adds nothing.
/// </summary>
public static class StructuralUsage
{
    /// <summary>The element's (or type's) own attribute: true, false, or absent (the rule decides).</summary>
    public const string Key = "loadBearing";

    /// <summary>
    /// What an element is: <see cref="Said"/> what it or its type says (null: nothing), <see cref="Effective"/> what the rule
    /// asked makes of it, and <see cref="Source"/> where that came from: own, ownPset, file, type, typePset, typeFile, or
    /// rule:partition, rule:class, rule:thickness. <see cref="Conflict"/>: the element (or its type) has the attribute and
    /// a Pset value that disagree (the attribute wins).
    /// </summary>
    public sealed record Usage(bool? Said, bool Effective, string Source, bool Conflict = false)
    {
        public JsonObject ToJson() => new()
        {
            ["said"] = Said is { } s ? JsonValue.Create(s) : null, ["effective"] = Effective, ["source"] = Source, ["conflict"] = Conflict,
        };
    }

    /// <summary>The classes whose elements bear load or not (IFC's Pset_…Common LoadBearing).</summary>
    public static readonly IReadOnlySet<string> Classes = new HashSet<string>
    {
        IfcClass.Wall, "IfcWallStandardCase", "IfcCurtainWall", IfcClass.Slab, IfcClass.Roof, IfcClass.Column, IfcClass.Beam, IfcClass.Footing, "IfcPile",
        "IfcMember", "IfcPlate", "IfcCovering", "LapinRoomFinish", "LapinFinishZone",
    };

    /// <summary>Those taken as bearing load when nothing says (the views' rule): floors, roofs, columns, beams, footings and piles.</summary>
    static readonly HashSet<string> BearingUnlessSaid = [IfcClass.Slab, IfcClass.Roof, IfcClass.Column, IfcClass.Beam, IfcClass.Footing, "IfcPile"];

    /// <summary>Those that never bear load, whatever is said of them: coverings and a room's finishes.</summary>
    static readonly HashSet<string> Never = ["IfcCovering", "LapinRoomFinish", "LapinFinishZone"];

    /// <summary>Whether the Structural tick applies to an element (or a type) of this class: one that may bear load.</summary>
    public static bool Applies(string cls) => Tickable(cls.EndsWith("Type", StringComparison.Ordinal) ? cls[..^4] : cls);

    static bool Tickable(string cls) => Classes.Contains(cls) && !Never.Contains(cls);

    static bool? Bool(JsonNode? n)
    {
        if (n is JsonObject o && o["value"] is { } inner) n = inner;
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<bool>(out var b)) return b;
        if (v.TryGetValue<string>(out var s))
            return s.Trim().Trim('.').ToLowerInvariant() switch { "true" or "yes" or "1" or "t" => true, "false" or "no" or "0" or "f" => false, _ => null };
        return null;
    }

    /// <summary>The entity's own attribute.</summary>
    public static bool? Own(Entity x) => x.Attrs[Key] is JsonValue lv && lv.TryGetValue<bool>(out var own) ? own : null;

    /// <summary>The LoadBearing the user typed in a Pset_ set (a parameter), as the properties set it.</summary>
    public static bool? Typed(Entity x)
    {
        if (x.Attrs["params"] is not JsonArray ps) return null;
        foreach (var p in ps.OfType<JsonObject>())
            if (string.Equals((string?)p["name"], "LoadBearing", StringComparison.OrdinalIgnoreCase) && ((string?)p["pset"])?.StartsWith("Pset_", StringComparison.Ordinal) != false)
                return p["value"] is JsonValue v && (v.TryGetValue<bool>(out var b) ? b : v.TryGetValue<string>(out var s) && s.Trim().ToLowerInvariant() is "true" or "yes" or "1");
        return null;
    }

    /// <summary>The LoadBearing its IFC file gave it in a Pset_…Common (a reference's <c>properties</c>, a recognised element's <c>imported.properties</c>).</summary>
    public static bool? FromFile(Entity x)
    {
        foreach (var sets in new[] { x.Attrs["properties"], x.Attrs["imported"]?["properties"] })
        {
            if (sets is not JsonObject so) continue;
            foreach (var (set, props) in so)
            {
                if (props is not JsonObject po || !set.StartsWith("Pset_", StringComparison.Ordinal) || !set.EndsWith("Common", StringComparison.Ordinal)) continue;
                foreach (var (n, v) in po)
                    if (string.Equals(n, "LoadBearing", StringComparison.OrdinalIgnoreCase) && Bool(v) is { } b) return b;
            }
        }
        return null;
    }

    /// <summary>What an element or its type says, with where it came from; null when nothing says.</summary>
    public static (bool Value, string Source, bool Conflict)? SaidOf(IModelView m, Entity e)
    {
        static (bool, string, bool)? In(Entity x, string own, string pset, string file)
        {
            var (a, t, f) = (Own(x), Typed(x), FromFile(x));
            var other = t ?? f;
            if (a is { } av) return (av, own, other is { } ov && ov != av);
            if (t is { } tv) return (tv, pset, false);
            if (f is { } fv) return (fv, file, false);
            return null;
        }
        return In(e, "own", "ownPset", "file")
            ?? (m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } type ? In(type, "type", "typePset", "typeFile") : null);
    }

    /// <summary>What an element or its type says of its load, null when neither says (as Provisions.LoadBearingOrNull has always answered).</summary>
    public static bool? Said(IModelView m, Entity e) => SaidOf(m, e)?.Value;

    static bool IsWall(string cls) => cls is IfcClass.Wall or "IfcWallStandardCase";

    static bool Partition(IModelView m, Entity e) =>
        e.PredefinedType == "PARTITIONING" || m.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.PredefinedType == "PARTITIONING";

    /// <summary>
    /// The views' answer (<see cref="ViewStyles.BearsLoad"/>, the cores of the load-bearing elements only, the Structure
    /// discipline's halftone, the filter field <c>structural</c>): what is said, else a wall bears load unless it or its
    /// type is a partition, and a floor, roof, column, beam, footing or pile does; a covering or a room's finish never. An
    /// element of any other class (a door, a stair) is kept: effective, by its class.
    /// </summary>
    public static Usage ForViews(IModelView m, Entity e)
    {
        if (!Classes.Contains(e.Class)) return new(null, true, "rule:class");
        if (Never.Contains(e.Class)) return new(null, false, "rule:class");
        if (SaidOf(m, e) is { } said) return new(said.Value, said.Value, said.Source, said.Conflict);
        if (IsWall(e.Class)) return Partition(m, e) ? new(null, false, "rule:partition") : new(null, true, "rule:class");
        return new(null, BearingUnlessSaid.Contains(e.Class), "rule:class");
    }

    /// <summary>
    /// The analysis' answer: what is said, else a wall a partition (out), else by its material and core (<c>rule:thickness</c>,
    /// which <see cref="Analysis.AnalyticalModel"/> works out: <see cref="Effective"/> true here, the model leaves out the thin
    /// and the light); floors, roofs and the structure's members are in.
    /// </summary>
    public static Usage ForAnalysis(IModelView m, Entity e)
    {
        if (SaidOf(m, e) is { } said) return new(said.Value, said.Value, said.Source, said.Conflict);
        if (IsWall(e.Class)) return Partition(m, e) ? new(null, false, "rule:partition") : new(null, true, "rule:thickness");
        return new(null, !Never.Contains(e.Class), "rule:class");
    }

    /// <summary>A yes or no as fields, tags and filters read the effective value (the views' rule); null for a class that bears no load either way.</summary>
    public static string? FieldValue(IModelView m, Entity e) => Classes.Contains(e.Class) ? ForViews(m, e).Effective ? "yes" : "no" : null;

    /// <summary>The field schedules, tags, filters and quantities read: <c>structural</c>, yes or no, on every class that may bear load.</summary>
    public const string Field = "structural";

    static int registered;

    /// <summary>The worked-out field <c>structural</c> on each class (once a process).</summary>
    public static void RegisterFields()
    {
        if (Interlocked.Exchange(ref registered, 1) == 1) return;
        foreach (var cls in Classes)
            DerivedFields.Add(cls, new DerivedField(Field, "Structural (load-bearing)", FieldValue));
    }

    public static void Register(IModuleRegistry r)
    {
        RegisterFields();

        // What each element (or type) is: {ids} → [{id, applies, said, effective, source, conflict, analysis: {effective, source}, type?: {said, source}}].
        r.Query("element.structural", (q, a) =>
        {
            var m = q.Model;
            var o = new JsonArray();
            foreach (var id in Args.Ids(a))
            {
                if (m.Get(id) is not { } e) continue;
                var row = new JsonObject { ["id"] = id, ["applies"] = Applies(e.Class) };
                if (Applies(e.Class))
                {
                    foreach (var (k, v) in ForViews(m, e).ToJson()) row[k] = v?.DeepClone();
                    row["analysis"] = ForAnalysis(m, e).ToJson();
                    if (m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } type && Own(type) is { } typeOwn) row["typeSaid"] = typeOwn;
                }
                o.Add((JsonNode)row);
            }
            return o;
        });

        // The Structural tick (docs/plan-structural-usage.md): {ids, value: true | false | null, onType?}; null takes it off (the
        // rule decides again). With onType, on each element's type instead (a type given is set itself). One undo step; the
        // element's Pset values are left as they are (the tick wins, and the properties say when they disagree).
        r.Command("element.setStructural", "Structural", (ctx, a) =>
        {
            var ids = Args.Ids(a).ToList();
            if (ids.Count == 0) throw new CommandException("Select what is or is not structural.");
            if (!a.ContainsKey("value")) throw new CommandException("Say true, false or null.");
            bool? value = a["value"] switch
            {
                null => null,
                JsonValue v when v.TryGetValue<bool>(out var b) => b,
                _ => throw new CommandException("Structural is true, false or null."),
            };
            var onType = a["onType"] is JsonValue tv && tv.TryGetValue<bool>(out var ot) && ot;
            var (expanded, _) = Groups.Expand(ctx.Model, ids);
            var targets = new List<Entity>();
            foreach (var id in expanded)
            {
                if (ctx.Model.Get(id) is not { } e) continue;
                var target = onType && !e.Class.EndsWith("Type", StringComparison.Ordinal) ? ctx.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType) : e;
                if (target == null || !Applies(target.Class) || targets.Any(t => t.Id == target.Id)) continue;
                targets.Add(target);
            }
            if (targets.Count == 0) throw new CommandException(onType ? "None of these has a type that may bear load." : "None of these may bear load (walls, floors, roofs, columns, beams, members, plates, footings).");
            var changed = 0;
            foreach (var t in targets)
            {
                if (Own(t) == value) continue;
                var attrs = t.CloneAttrs();
                if (value is { } v) attrs[Key] = v; else attrs.Remove(Key);
                ctx.Replace(t.With(attrs: attrs));
                changed++;
            }
            return new JsonObject { ["changed"] = changed };
        });
    }
}
