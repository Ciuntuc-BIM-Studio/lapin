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

        // Priorities for some classes (null puts a class back to its default).
        r.Command("cutOrder.set", "Change the cut order", (ctx, a) =>
        {
            var cutters = ctx.Services.Get<ICutters>() ?? throw new CommandException("No cut priorities in this product.");
            if (a["priorities"] is not JsonObject given) throw new CommandException("Give the priorities by class.");
            var existing = ctx.Model.OfClass(Class).FirstOrDefault();
            var table = existing?.Attrs["priorities"] is JsonObject t ? (JsonObject)t.DeepClone() : new JsonObject();
            foreach (var (cls, v) in given)
            {
                if (!cutters.CutDefaults.ContainsKey(cls)) throw new CommandException($"{cls} takes no part in cutting.");
                if (v is null) { table.Remove(cls); continue; }
                var n = Json.Number(v) ?? throw new CommandException($"The priority of {cls} is a number.");
                if (n < 0 || n > 100) throw new CommandException("A priority runs from 0 to 100.");
                table[cls] = Math.Round(n);
            }
            var attrs = new JsonObject { ["priorities"] = table };
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
