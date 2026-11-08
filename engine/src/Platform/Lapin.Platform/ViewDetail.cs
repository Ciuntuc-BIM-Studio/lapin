using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A view's level of development (decision 404, <see cref="PlanDetail.Lods"/>): a way of viewing the model, never taken from
/// the view's scale. The view's own (<c>styleOverrides.detail</c>, View settings or Visibility / Graphics), else the level a
/// graphics set kept before (decision 394's <c>detailLevel</c>: coarse, medium, fine as 100, 200, 400), else its view style's.
/// </summary>
public static class ViewDetail
{
    /// <summary>The project's mark that its views' levels are kept as this version reads them (projects made before are upgraded).</summary>
    public const string RevKey = "detailRev";

    /// <summary>The level a view sets itself, or null when it takes its style's.</summary>
    public static int? Own(Entity view, VisibilityGraphics? vg = null) =>
        PlanDetail.LodOf(view.Attrs["styleOverrides"]?["detail"]) ?? vg?.DetailLevel;

    /// <summary>The level a view draws at.</summary>
    public static int Of(IModelView m, Entity view, VisibilityGraphics? vg = null) =>
        Own(view, vg ?? VisibilityGraphics.Of(m, view)) ?? ViewStyles.StyleOf(m, view).Detail;

    /// <summary>
    /// A project made before views had a level of development: each view keeps how it drew (decision 401 took it from the
    /// scale: fine at 1:50 and larger, medium to 1:100, coarse from 1:200, as LOD 400, 200 and 100; a 3D view drew
    /// everything, LOD 400) as a level of its own, unless it set one; then the project is marked.
    /// </summary>
    public sealed class LevelUpgrade : IProjectUpgrade
    {
        public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
        {
            if (entities.FirstOrDefault(e => e.Class == IfcClass.Project) is not { } project || project.Attrs[RevKey] != null) return (entities, relations);
            return (entities.Select(e =>
            {
                if (e.Class == Views.Class)
                {
                    var kind = e.Str("kind") ?? "plan";
                    if (kind is "plan" or "section" or "elevation" or "3d") return Kept(e, kind == "3d" ? PlanDetail.Lod400 : FromScale(e.NumOrNull("scale") ?? 100));
                    return e;
                }
                if (e.Id != project.Id) return e;
                var attrs = e.CloneAttrs();
                attrs[RevKey] = 1;
                // {3D}'s settings live on the project.
                if (attrs["view3d"] is JsonObject v3)
                {
                    var own = v3["styleOverrides"] as JsonObject;
                    if (own?["detail"] == null && v3["graphics"]?["detailLevel"] == null)
                    {
                        own ??= [];
                        own["detail"] = PlanDetail.Lod400;
                        v3["styleOverrides"] = own;
                    }
                }
                else attrs["view3d"] = new JsonObject { ["styleOverrides"] = new JsonObject { ["detail"] = PlanDetail.Lod400 } };
                return e.With(attrs: attrs);
            }).ToList(), relations);
        }

        /// <summary>The level decision 401 gave a view by its scale.</summary>
        public static int FromScale(double scale) => scale <= 50 + 1e-9 ? PlanDetail.Lod400 : scale <= 100 + 1e-9 ? PlanDetail.Lod200 : PlanDetail.Lod100;

        static Entity Kept(Entity view, int lod)
        {
            if (view.Attrs["styleOverrides"]?["detail"] != null || view.Attrs["graphics"]?["detailLevel"] != null) return view;
            var attrs = view.CloneAttrs();
            var own = attrs["styleOverrides"] as JsonObject ?? [];
            own["detail"] = lod;
            attrs["styleOverrides"] = own;
            return view.With(attrs: attrs);
        }
    }
}
