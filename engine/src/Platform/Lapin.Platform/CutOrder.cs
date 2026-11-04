using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The project's cut order: which class cuts which (higher cuts lower), kept
/// in one LapinCutOrder entity so that changing it rebuilds only what takes
/// part in cutting. A class it does not list keeps its module's priority; an
/// element's own <c>cutPriority</c> overrides both.
/// </summary>
public static class CutOrder
{
    public const string Class = "LapinCutOrder";

    /// <summary>The project's table (class → priority), or null when it has none.</summary>
    public static JsonObject? Of(IModelView m) => m.OfClass(Class).FirstOrDefault()?.Attrs["priorities"] as JsonObject;

    /// <summary>The project's priorities by material (material id → priority), or null when it gives none.</summary>
    public static JsonObject? MaterialsOf(IModelView m) => m.OfClass(Class).FirstOrDefault()?.Attrs["materials"] is JsonObject { Count: > 0 } t ? t : null;

    /// <summary>
    /// MEP in the cut order (decision 293): whether runs cut provision openings in the walls and floors they cross
    /// (<see cref="Cut"/>; a system may say otherwise, <c>cutHosts</c>), the clearance round the run, the step the sizes
    /// are rounded up to, the smallest opening, and whether load-bearing hosts are cut too (else reported, not cut).
    /// Kept in the cut order's entity under <c>mep</c>, so every wall and floor reads it already; a project without it: off.
    /// </summary>
    public sealed record MepCut(bool Cut = false, double Clearance = 0.02, double Step = 0.01, double MinSize = 0.05, bool CutStructural = false)
    {
        /// <summary>A new project's (decision 293): on, load-bearing hosts reported rather than cut.</summary>
        public static readonly MepCut NewProject = new(Cut: true);

        public static MepCut From(JsonNode? n)
        {
            if (n is not JsonObject o) return new();
            static bool B(JsonNode? x, bool d) => x is JsonValue v && v.TryGetValue<bool>(out var b) ? b : d;
            var d = new MepCut();
            return new(B(o["cut"], d.Cut), Math.Clamp(Json.Number(o["clearance"]) ?? d.Clearance, 0, 0.5), Math.Clamp(Json.Number(o["step"]) ?? d.Step, 0, 0.1),
                Math.Clamp(Json.Number(o["minSize"]) ?? d.MinSize, 0, 1), B(o["structural"], d.CutStructural));
        }

        public JsonObject ToJson() => new() { ["cut"] = Cut, ["clearance"] = Clearance, ["step"] = Step, ["minSize"] = MinSize, ["structural"] = CutStructural };

        /// <summary>A size grown by the clearance on both sides and rounded up to the step, never under the smallest opening.</summary>
        public double Size(double element, double clearance)
        {
            var s = element + 2 * clearance;
            if (Step > 1e-6) s = Math.Ceiling(s / Step - 1e-6) * Step;
            return Math.Max(s, MinSize);
        }
    }

    /// <summary>The project's MEP cutting rule.</summary>
    public static MepCut Mep(IModelView m) => MepCut.From(m.OfClass(Class).FirstOrDefault()?.Attrs["mep"]);

    /// <summary>The cut order's attributes kept when a part of it changes (the MEP rule beside the priorities).</summary>
    static JsonObject Kept(Entity? existing, params string[] except)
    {
        var o = new JsonObject();
        foreach (var (k, v) in existing?.Attrs ?? []) if (!except.Contains(k)) o[k] = v?.DeepClone();
        return o;
    }

    /// <summary>
    /// What an element is mainly made of: its core layer's material (a wall, a
    /// floor), else the material it or its type names (a beam, a column, a
    /// Studio solid).
    /// </summary>
    public static string? MainMaterial(Entity e, IModelView m)
    {
        if (LayerSets.BandsOf(m, e.Id) is { Count: > 0 } bands) return (bands.Any(b => b.Core) ? bands.First(b => b.Core) : bands[0]).Material;
        return e.Str("material") ?? m.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.Str("material");
    }

    /// <summary>An element's priority by its main material, when the project gives that material one.</summary>
    public static int? ByMaterial(Entity e, IModelView m) =>
        MaterialsOf(m) is { } table && MainMaterial(e, m) is { } mat && Json.Number(table[mat]) is { } n ? (int)n : null;

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Cut order", [], Kind: "resource"));

        r.Query("cutOrder.list", (q, _) =>
        {
            var cutters = q.Services.Get<ICutters>() ?? throw new CommandException("No cut priorities in this product.");
            var rows = new JsonArray();
            foreach (var (cls, def) in cutters.CutDefaults.OrderByDescending(x => x.Value))
            {
                var label = (q.Services as ModuleCatalog)?.Class(cls)?.Label ?? cls;
                rows.Add((JsonNode)new JsonObject { ["class"] = cls, ["label"] = label, ["default"] = def, ["priority"] = cutters.ClassPriority(cls, q.Model) });
            }
            return rows;
        });

        // The project's materials, with the priority each is given (null: its elements rank by their class).
        r.Query("cutOrder.materials", (q, _) =>
        {
            var table = MaterialsOf(q.Model);
            return new JsonArray(q.Model.OfClass(IfcClass.Material).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => (JsonNode)new JsonObject
            {
                ["id"] = x.Id, ["name"] = x.Name, ["priority"] = table?[x.Id] is { } v ? Json.Number(v) : null,
            }).ToArray());
        });

        // Priorities for some classes and some materials (null puts one back to its default).
        r.Command("cutOrder.set", "Change the cut order", (ctx, a) =>
        {
            var cutters = ctx.Services.Get<ICutters>() ?? throw new CommandException("No cut priorities in this product.");
            if (a["priorities"] is not JsonObject && a["materials"] is not JsonObject) throw new CommandException("Give the priorities by class or by material.");
            var existing = ctx.Model.OfClass(Class).FirstOrDefault();
            static double Priority(JsonNode v, string what)
            {
                var n = Json.Number(v) ?? throw new CommandException($"The priority of {what} is a number.");
                if (n < 0 || n > 100) throw new CommandException("A priority runs from 0 to 100.");
                return Math.Round(n);
            }
            var table = existing?.Attrs["priorities"] is JsonObject t ? (JsonObject)t.DeepClone() : new JsonObject();
            foreach (var (cls, v) in a["priorities"] as JsonObject ?? [])
            {
                if (!cutters.CutDefaults.ContainsKey(cls)) throw new CommandException($"{cls} takes no part in cutting.");
                if (v is null) table.Remove(cls); else table[cls] = Priority(v, cls);
            }
            var materials = existing?.Attrs["materials"] is JsonObject mt ? (JsonObject)mt.DeepClone() : new JsonObject();
            foreach (var (id, v) in a["materials"] as JsonObject ?? [])
            {
                var mat = ctx.Model.Get(id) is { Class: IfcClass.Material } x ? x : throw new CommandException($"There is no material {id}.");
                if (v is null) materials.Remove(id); else materials[id] = Priority(v, mat.Name);
            }
            var attrs = Kept(existing, "priorities", "materials");
            attrs["priorities"] = table;
            if (materials.Count > 0) attrs["materials"] = materials;
            if (existing == null) ctx.Add(new Entity(ctx.NewId(), Class, "Cut order", attrs));
            else ctx.Replace(existing.With(attrs: attrs));
            return null;
        });

        // The priorities back to their defaults; the MEP rule stays as it is.
        r.Command("cutOrder.reset", "Default cut order", (ctx, _) =>
        {
            foreach (var e in ctx.Model.OfClass(Class).ToList())
            {
                if (e.Attrs["mep"] is JsonObject mep) ctx.Replace(e.With(attrs: new JsonObject { ["priorities"] = new JsonObject(), ["mep"] = mep.DeepClone() }));
                else ctx.Remove(e.Id);
            }
            return null;
        });

        // MEP in the cut order (decision 293): {cut?, clearance?, step?, minSize?, structural?}.
        r.Query("cutOrder.mep", (q, _) => Mep(q.Model).ToJson());
        r.Command("cutOrder.setMep", "MEP cuts walls and floors", (ctx, a) =>
        {
            var existing = ctx.Model.OfClass(Class).FirstOrDefault();
            var now = Mep(ctx.Model).ToJson();
            foreach (var k in new[] { "cut", "clearance", "step", "minSize", "structural" })
                if (a[k] is JsonValue v) now[k] = v.DeepClone();
            var next = MepCut.From(now);
            var attrs = Kept(existing, "mep");
            if (!attrs.ContainsKey("priorities")) attrs["priorities"] = new JsonObject();
            attrs["mep"] = next.ToJson();
            if (existing == null) ctx.Add(new Entity(ctx.NewId(), Class, "Cut order", attrs));
            else ctx.Replace(existing.With(attrs: attrs));
            return next.ToJson();
        });
    }
}
