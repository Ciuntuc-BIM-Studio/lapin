using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A level's plan (a tester's report #113, decision 386): its default plan (<c>plan:&lt;storey&gt;</c>, held by no entity)
/// stands in until the level has a floor plan view of its own (made by the first drafting or DXF import on it, or by hand);
/// from then on that view is the level's plan, as the project browser lists it and the interface shows it. What was drawn on
/// the default plan moves onto it then, so nothing stays behind on a plan that is no longer shown: when the view is made,
/// and for projects saved before, as they open (<see cref="DefaultPlanUpgrade"/>).
/// </summary>
public static class StoreyPlans
{
    public const string Prefix = "plan:";

    /// <summary>Whether a view is a level's floor plan: a plan on it, not a site or location plan, not a foundation plan.</summary>
    public static bool IsFloorPlan(Entity v, string storeyId) =>
        v.Class == Views.Class && (v.Str("kind") ?? "plan") == "plan" && v.Str("storeyId") == storeyId && v.Str("site") == null && !v.Bool("foundation");

    /// <summary>The level's own floor plan view, the first made; null while it has none.</summary>
    public static Entity? Of(IModelView m, string storeyId) => m.OfClass(Views.Class).FirstOrDefault(v => IsFloorPlan(v, storeyId));

    /// <summary>What was drawn on the level's default plan (drafting, texts, dimensions, an imported DXF…), put on <paramref name="viewId"/>.</summary>
    public static int Rehome(IEdit edit, string storeyId, string viewId)
    {
        var from = Prefix + storeyId;
        var moved = 0;
        foreach (var e in edit.Model.Entities.Where(e => e.Class != Views.Class && e.Str("viewId") == from).ToList())
        {
            var attrs = e.CloneAttrs();
            attrs["viewId"] = viewId;
            edit.Replace(e.With(attrs: attrs));
            moved++;
        }
        return moved;
    }

    /// <summary>A project saved before: what lies on a default plan whose level has a floor plan view goes onto that view.</summary>
    public sealed class DefaultPlanUpgrade : IProjectUpgrade
    {
        public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
        {
            var plans = new Dictionary<string, string>();
            foreach (var v in entities)
                if (v.Class == Views.Class && v.Str("storeyId") is { } s && !plans.ContainsKey(Prefix + s) && IsFloorPlan(v, s)) plans[Prefix + s] = v.Id;
            if (plans.Count == 0) return (entities, relations);
            var changed = false;
            var list = entities.Select(e =>
            {
                if (e.Class == Views.Class || e.Str("viewId") is not { } id || !plans.TryGetValue(id, out var to)) return e;
                changed = true;
                var attrs = e.CloneAttrs();
                attrs["viewId"] = to;
                return e.With(attrs: attrs);
            }).ToList();
            return changed ? (list, relations) : (entities, relations);
        }
    }
}
