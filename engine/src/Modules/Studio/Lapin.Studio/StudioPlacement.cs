using Lapin.Drafting.Cad;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>A Studio solid moves, turns and copies with its sketch (outline, holes, path or axis): the solid follows it.</summary>
public sealed class StudioPlacement : IElementPlacement
{
    public bool Handles(Entity e) => e.Str("operation") != null && StudioModule.Classes.Contains(e.Class);

    /// <summary>The sketch's shapes: the outline, its holes, the path or axis.</summary>
    static List<Entity> Sketch(IModelView m, Entity e)
    {
        var o = new List<Entity>();
        if (m.Get(e.Str("profile") ?? "") is not { } profile) return o;
        o.Add(profile);
        o.AddRange(Sketches.Holes(m, profile, [profile.Id]).Select(h => m.Get(h.Id)).OfType<Entity>());
        if (m.Get(e.Str("guide") ?? "") is { } g) o.Add(g);
        return o;
    }

    static void PlanOnly(IModelView m, Entity e)
    {
        var profile = m.Get(e.Str("profile") ?? "");
        var view = profile == null ? null : Views.Find(m, profile.Str("viewId") ?? "");
        if (view?.Str("kind") is not (null or "plan")) throw new CommandException($"„{e.Name}” is sketched on a section, an elevation or a reference plane: move its sketch there, or move the plane it lies on.");
    }

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        PlanOnly(ctx.Model, e);
        foreach (var d in Sketch(ctx.Model, e).Where(d => !moving.Contains(d.Id)))
            if (Drafts.ShapeOf(d) is { } s) Drafts.Set(ctx, d, AnnotationPlacement.Map(s, t));
    }

    public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        // A box's bottom or top is copied with its box (the copy makes its own).
        if (e.Str("operation") == Thick.BoxSlab) return e.Id;
        PlanOnly(ctx.Model, e);
        var map = new Dictionary<string, string>();
        foreach (var d in Sketch(ctx.Model, e))
            if (Drafts.ShapeOf(d) is { } s) map[d.Id] = Drafts.Add(ctx, d.Str("viewId")!, d.Str("layer"), AnnotationPlacement.Map(s, t)).Id;
        var attrs = e.CloneAttrs();
        attrs["profile"] = map.GetValueOrDefault(e.Str("profile") ?? "") ?? e.Str("profile");
        if (e.Str("guide") is { } g) attrs["guide"] = map.GetValueOrDefault(g) ?? g;
        var copy = new Entity(ctx.NewId(), e.Class, e.Name, attrs, e.PredefinedType);
        ctx.Add(copy);
        if (Spatial.StoreyOf(ctx.Model, e.Id) is { } st) Spatial.Contain(ctx, st.Id, copy.Id);
        if (e.Str("operation") == Thick.Box) Boxes.Reconcile(ctx, copy);
        return copy.Id;
    }
}
