using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Sweeps and reveals on the wall type (decision 298, Revit's wall type Sweeps and Reveals): an IfcWallType keeps a
/// list of them (<c>sweeps</c>: each a <c>key</c>, its profile's id and copy, mode, face, height or from the top, offset,
/// material, flip, set-backs, returns), and every wall of the type carries each as a <see cref="WallSweeps"/> sweep of its
/// own, tagged <c>typeSweep</c> (the type's id and the key), so it is measured, scheduled, written to IFC and mitred round
/// corners like one drawn by hand. They are made again over the same ids (<see cref="Rebuild"/>) when a wall is made or
/// changes type, when it is flipped, and when the type's sweeps change. The type's face is the face its first layer is on
/// (the right as the wall is drawn, the left on a flipped wall). A wall may change an inherited sweep's fields (they become
/// its own: <c>own</c>, kept when the type changes) or switch one off (<c>sweepsOff</c> on the wall: deleting it does).
/// </summary>
public static class WallSweepTypes
{
    public const string Key = "sweeps";
    public const string OffKey = "sweepsOff";

    public static void Register(IModuleRegistry r)
    {
        r.Service<IDeletion>(new Deletion());

        // {wallId | ids, tag?}: the type's sweeps switched off on these walls switched on again (one, or all).
        r.Command("restoreWallSweeps", "Switch type sweeps on", (ctx, a) =>
        {
            var ids = a["wallId"] is JsonValue ? [(string)a["wallId"]!] : Args.Ids(a);
            foreach (var id in ids) SwitchOn(ctx, Spatial.Require(ctx.Model, id, IfcClass.Wall), (string?)a["tag"]);
            return null;
        });
    }

    /// <summary>The type's sweep definitions, each with its key.</summary>
    public static IReadOnlyList<JsonObject> Defs(Entity? type) =>
        type?.Attrs[Key] is JsonArray a ? [.. a.OfType<JsonObject>().Where(o => (string?)o["key"] is { Length: > 0 })] : [];

    /// <summary>What a wall's sweep of the type's names: the type and the key.</summary>
    public static string Tag(Entity type, string key) => $"{type.Id}/{key}";

    public static HashSet<string> OffOf(Entity wall) => wall.Attrs[OffKey] is JsonArray a ? [.. a.Select(x => (string?)x).OfType<string>()] : [];

    /// <summary>
    /// A type's sweeps as a command gives them (<c>[{key?, profileId | catalog, params | profile, mode, side, height, fromTop?,
    /// offset?, material? | materialId?, flip?, setbackStart?, setbackEnd?, returns?, returnsAtOpenings?, gaps?}]</c>), checked:
    /// a key the type has stays (its walls' sweeps stay the same ones), a new one gets its own.
    /// </summary>
    public static JsonArray Parse(ICommandContext ctx, Entity? type, JsonNode? node)
    {
        if (node is not JsonArray list) throw new CommandException("A wall type's sweeps are a list.");
        var known = Defs(type).Select(d => (string)d["key"]!).ToHashSet();
        var o = new JsonArray();
        foreach (var item in list.OfType<JsonObject>())
        {
            var mode = (string?)item["mode"] == "cut" ? "cut" : "add";
            var def = new JsonObject
            {
                ["key"] = (string?)item["key"] is { Length: > 0 } k && known.Remove(k) ? k : ctx.NewId(),
                ["mode"] = mode,
            };
            // A profile the project no longer has (a file's types came without their profiles): the copy kept.
            if ((string?)item[Profiles.IdKey] is { Length: > 0 } pid && ctx.Model.Get(pid) is not { Class: Profiles.Class } && ProfileShape.From(item[Profiles.Key]) is { } copy)
                Profiles.Assign(def, copy, null);
            else if (Profiles.FromArgs(ctx, item) is { } p) Profiles.Assign(def, p.Shape, p.Id);
            else
            {
                var made = Profiles.Ensure(ctx, mode == "cut" ? "groove" : "rectangle", null);
                Profiles.Assign(def, Profiles.Read(made)!, made.Id);
            }
            def["side"] = (string?)item["side"] == "left" ? "left" : "right";
            var height = Json.Number(item["height"]) ?? (mode == "cut" ? 1.0 : 0.3);
            if (height is < -50 or > 50) throw new CommandException("A sweep's height is within 50 m of the wall's base or top.");
            def["height"] = Math.Round(height, 4);
            if (item["fromTop"] is JsonValue ft && ft.TryGetValue<bool>(out var top) && top) def["fromTop"] = true;
            WallSweeps.ReadOptions(def, item);
            // A type's sweep runs the whole face; its walls may set their own start and end.
            def.Remove("start");
            def.Remove("end");
            if (mode == "add")
            {
                var mat = (string?)item["material"] is { Length: > 0 } mid && ctx.Model.Get(mid) is { Class: IfcClass.Material } byId ? byId
                    : WallSweeps.MaterialFrom(ctx, item) ?? WallSweeps.DefaultMaterial(ctx);
                def["material"] = mat.Id;
            }
            o.Add((JsonNode)def);
        }
        return o;
    }

    /// <summary>The type's sweeps set (none: an empty list), and every wall of the type given them.</summary>
    public static void Set(IEdit edit, Entity type, JsonArray sweeps)
    {
        var now = edit.Model.Get(type.Id)!;
        var attrs = now.CloneAttrs();
        if (sweeps.Count == 0) attrs.Remove(Key); else attrs[Key] = sweeps;
        if (JsonNode.DeepEquals(attrs, now.Attrs)) return;
        edit.Replace(now.With(attrs: attrs));
        foreach (var wall in edit.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(w => w.Class == IfcClass.Wall).ToList()) Rebuild(edit, wall);
    }

    /// <summary>
    /// The wall's sweeps of its type made as the type has them: each definition's one kept (its id, and the fields the wall
    /// made its own), a missing one made, one of a definition gone or of another type removed; those switched off left out.
    /// </summary>
    public static void Rebuild(IEdit edit, Entity wallIn)
    {
        var m = edit.Model;
        if (m.Get(wallIn.Id) is not { Class: IfcClass.Wall } wall) return;
        var type = Walls.TypeOf(m, wall.Id);
        var wanted = type == null ? [] : Defs(type).ToDictionary(d => Tag(type, (string)d["key"]!), d => d);
        var off = OffOf(wall);
        var mine = m.RelatedOf(wall.Id, IfcClass.RelVoidsElement).Where(x => x.Class == WallSweeps.Class && x.Str("typeSweep") != null).ToList();
        var kept = new Dictionary<string, Entity>();
        foreach (var s in mine)
        {
            var tag = s.Str("typeSweep")!;
            if (!wanted.ContainsKey(tag) || off.Contains(tag) || kept.ContainsKey(tag)) edit.Remove(s.Id);
            else kept[tag] = s;
        }
        if (wanted.Count == 0) return;
        // The type's face is its first layer's: a flipped wall has it on its left.
        var flipped = LayerSets.UsageOf(m, wall.Id) is { } u && Layers.ReadUsage(u) is { Negative: true };
        foreach (var (tag, def) in wanted)
        {
            if (off.Contains(tag)) continue;
            var existing = kept.GetValueOrDefault(tag);
            var attrs = existing?.CloneAttrs() ?? new JsonObject();
            var own = WallSweeps.OwnOf(attrs);
            foreach (var f in WallSweeps.Inherited)
            {
                if (own.Contains(WallSweeps.OwnKey(f))) continue;
                var v = def[f]?.DeepClone();
                if (f == "side" && flipped && v != null) v = (string?)v == "left" ? "right" : "left";
                if (v == null) attrs.Remove(f); else attrs[f] = v;
            }
            attrs["typeSweep"] = tag;
            if (existing == null) { WallSweeps.Make(edit, wall, attrs); continue; }
            var mode = attrs["mode"]?.ToString() == "cut" ? "cut" : "add";
            WallSweeps.SetExport(attrs, mode);
            var predefined = mode == "cut" ? "RECESS" : "MOLDING";
            if (!JsonNode.DeepEquals(attrs, existing.Attrs) || predefined != existing.PredefinedType)
                edit.Replace(new Entity(existing.Id, existing.Class, existing.Name, attrs, predefined));
        }
    }

    /// <summary>A wall type's sweep switched off on its wall (the wall keeps its tag in <c>sweepsOff</c>) and gone; false for a sweep of the wall's own.</summary>
    public static bool SwitchOff(IEdit edit, Entity sweep)
    {
        if (sweep.Str("typeSweep") is not { } tag) return false;
        if (WallSweeps.HostOf(edit.Model, sweep) is { } wall) MarkOff(edit, wall, tag);
        edit.Remove(sweep.Id);
        return true;
    }

    /// <summary>The wall keeps the type's sweep tagged so switched off (its sweep moved to another wall, or deleted).</summary>
    public static void MarkOff(IEdit edit, Entity wall, string tag)
    {
        var now = edit.Model.Get(wall.Id) ?? wall;
        var off = OffOf(now);
        if (!off.Add(tag)) return;
        var attrs = now.CloneAttrs();
        attrs[OffKey] = new JsonArray([.. off.Order(StringComparer.Ordinal).Select(x => (JsonNode)x)]);
        edit.Replace(now.With(attrs: attrs));
    }

    /// <summary>The type's sweeps switched off on the wall switched on again (the one tagged, or all), made again.</summary>
    public static void SwitchOn(IEdit edit, Entity wall, string? tag)
    {
        var off = OffOf(wall);
        if (off.Count == 0) return;
        if (tag != null) off.Remove(tag); else off.Clear();
        var attrs = wall.CloneAttrs();
        if (off.Count == 0) attrs.Remove(OffKey); else attrs[OffKey] = new JsonArray([.. off.Order(StringComparer.Ordinal).Select(x => (JsonNode)x)]);
        edit.Replace(wall.With(attrs: attrs));
        Rebuild(edit, edit.Model.Get(wall.Id)!);
    }

    /// <summary>A wall type's sweep deleted on its own is switched off on its wall, so the type does not bring it back.</summary>
    sealed class Deletion : IDeletion
    {
        public bool Delete(ICommandContext ctx, string id) =>
            ctx.Model.Get(id) is { Class: WallSweeps.Class } s && s.Str("typeSweep") != null && SwitchOff(ctx, s);
    }

    /// <summary>The type's sweeps as the type file writes them (lapin-types JSON): the profile's copy with its name, the material by name.</summary>
    public static JsonArray? Export(IModelView m, Entity type)
    {
        var defs = Defs(type);
        if (defs.Count == 0) return null;
        var o = new JsonArray();
        foreach (var d in defs)
        {
            var x = new JsonObject();
            foreach (var (k, v) in d)
            {
                if (k is "key" or Profiles.IdKey || v == null) continue;
                x[k] = k == "material" && (string?)v is { } mid ? m.Get(mid)?.Name ?? mid : v.DeepClone();
            }
            o.Add((JsonNode)x);
        }
        return o;
    }

    /// <summary>
    /// A type file's sweeps for <see cref="Parse"/>: each profile the project's (the one of the same outline, else made), the
    /// material by name; brought into a type that has sweeps, they take its keys in order, so its walls keep their sweeps.
    /// </summary>
    public static JsonArray Import(ICommandContext ctx, JsonArray sweeps, Entity? into = null)
    {
        var o = new JsonArray();
        var keys = Defs(into).Select(d => (string)d["key"]!).ToList();
        foreach (var item in sweeps.OfType<JsonObject>())
        {
            var x = (JsonObject)item.DeepClone();
            if (o.Count < keys.Count) x["key"] = keys[o.Count];
            if (x[Profiles.Key] is JsonObject pj && ProfileShape.From(pj) is { } shape)
            {
                var json = shape.ToJson();
                var found = ctx.Model.OfClass(Profiles.Class).FirstOrDefault(p => JsonNode.DeepEquals(p.Attrs[Profiles.Key]?["points"], json["points"]) && JsonNode.DeepEquals(p.Attrs[Profiles.Key]?["insertion"], json["insertion"]))
                    ?? Profiles.Create(ctx, (string?)pj["name"] is { Length: > 0 } n ? n : "Profile", shape, (string?)x["mode"] == "cut" ? "reveal" : "sweep");
                x[Profiles.IdKey] = found.Id;
                x.Remove(Profiles.Key);
            }
            o.Add((JsonNode)x);
        }
        return o;
    }
}
