using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A wall moved, turned, mirrored or copied: the platform's own placement,
/// and, mirrored, the mirror image of it. Its ends are carried, so what was
/// left of its axis would come out on the right: its layers are flipped across
/// the axis and the doors and windows it hosts turned to the other side, so
/// the exterior stays where the mirror puts it (the hand follows by itself).
/// </summary>
sealed class WallPlacement : IElementPlacement
{
    public bool Handles(Entity e) => e.Class == IfcClass.Wall && e.Str("operation") == null;

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        Placement.Own(ctx, ctx.Model.Get(e.Id) ?? e, t, moving);
        if (t.Mirrored) Mirror(ctx, e.Id);
    }

    public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        var id = Placement.DeepCopy(ctx, e, t);
        if (t.Mirrored) Mirror(ctx, id);
        return id;
    }

    static void Mirror(ICommandContext ctx, string wallId)
    {
        LayerSets.Flip(ctx, wallId);
        foreach (var opening in ctx.Model.RelatedOf(wallId, IfcClass.RelVoidsElement).ToList())
            if (Openings.FillOf(ctx.Model, opening.Id) is { } fill)
            {
                var attrs = fill.CloneAttrs();
                attrs["flipSide"] = !fill.Bool("flipSide");
                ctx.Replace(fill.With(attrs: attrs));
            }
    }
}
