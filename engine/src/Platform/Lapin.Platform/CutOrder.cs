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
                rows.Add(new JsonObject { ["class"] = cls, ["label"] = label, ["default"] = def, ["priority"] = cutters.ClassPriority(cls, q.Model) });
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
            var attrs = new JsonObject { ["priorities"] = table };
            if (materials.Count > 0) attrs["materials"] = materials;
            if (existing == null) ctx.Add(new Entity(ctx.NewId(), Class, "Cut order", attrs));
            else ctx.Replace(existing.With(attrs: attrs));
            return null;
        });

        r.Command("cutOrder.reset", "Default cut order", (ctx, _) =>
        {
            foreach (var e in ctx.Model.OfClass(Class).ToList()) ctx.Remove(e.Id);
            return null;
        });
    }
}
