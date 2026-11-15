using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Stairs;

/// <summary>
/// What goes with a stair or a ramp moved, turned, mirrored or copied (audit 15.11.2026): mirrored, it turns the other way
/// (<c>turn</c>), its railing's side and the open side of the spaces under it (<c>side</c>) change over, as a mirror image
/// has them; its opening in the floor above is found again where it now arrives (<see cref="StairBuilder.EnsureOpening"/>),
/// a copy getting its own. A floor copied without the stairs whose openings it holds does not take those openings (the
/// copy would hold a stair's second opening); a floor moved away from a stair's top gives the opening up, or another floor
/// takes it. A copy in place (Copy to storeys) is left to Copy to storeys (<see cref="StoreyCopies"/>).
/// </summary>
sealed class StairLinks : ITransformFollowers
{
    public object? Before(ICommandContext ctx, IReadOnlySet<string> moving, IReadOnlySet<string> selected) => null;

    public void After(ICommandContext ctx, object? state, Placement2D t, Placement2D plan, IReadOnlySet<string> moving, IReadOnlySet<string> selected, IReadOnlyDictionary<string, string> copies)
    {
        var m = ctx.Model;
        if (copies.Count == 0)
        {
            var stairs = moving.Select(m.Get).OfType<Entity>().Where(IsStair).ToList();
            if (plan.Mirrored) foreach (var s in stairs) Mirror(ctx, s);
            foreach (var s in stairs) if (m.Get(s.Id) is { } now) StairBuilder.EnsureOpening(ctx, now);
            // A floor moved: the stairs left behind find their opening again.
            foreach (var slab in moving.Select(m.Get).OfType<Entity>().Where(e => e.Class == IfcClass.Slab).ToList())
                foreach (var o in m.RelatedOf(slab.Id, IfcClass.RelVoidsElement).Where(o => o.Str("stairId") is { } sid && !moving.Contains(sid)).ToList())
                    if (m.Get(o.Str("stairId")!) is { } stair && IsStair(stair)) StairBuilder.EnsureOpening(ctx, stair);
            return;
        }
        if (InPlace(plan)) return;
        var made = copies.Values.ToHashSet();
        // A copied floor's openings for stairs not copied with it (after the copies were named, a copied stair's is its copy's).
        foreach (var c in made)
            if (m.Get(c) is { Class: IfcClass.Slab })
                foreach (var o in m.RelatedOf(c, IfcClass.RelVoidsElement).Where(o => o.Str("stairId") is { } sid && !made.Contains(sid)).ToList())
                    ctx.Remove(o.Id);
        foreach (var c in made)
            if (m.Get(c) is { } copy && IsStair(copy))
            {
                if (plan.Mirrored) Mirror(ctx, copy);
                StairBuilder.EnsureOpening(ctx, m.Get(c)!);
            }
    }

    static bool IsStair(Entity e) => e.Class is IfcClass.Stair or IfcClass.Ramp;

    /// <summary>The stair as its mirror image: turning the other way, its railing on the other side, the spaces under it open on the other side.</summary>
    static void Mirror(ICommandContext ctx, Entity stair)
    {
        var attrs = stair.CloneAttrs();
        attrs["turn"] = stair.Str("turn") == "right" ? "left" : "right";
        var sides = stair.Str("railingSides") is { } own && StairSpec.Sides.Contains(own) ? own : StairBuilder.RailingOf(stair, StairBuilder.SpecOf(ctx.Model, stair)).Sides;
        if (sides is "left" or "right") attrs["railingSides"] = sides == "left" ? "right" : "left";
        ctx.Replace(stair.With(attrs: attrs));
        foreach (var nook in UnderStairs.Of(ctx.Model, stair.Id).ToList())
            if (nook.Str("side") is "left" or "right")
            {
                var na = nook.CloneAttrs();
                na["side"] = nook.Str("side") == "left" ? "right" : "left";
                ctx.Replace(nook.With(attrs: na));
            }
    }

    static bool InPlace(Placement2D t) =>
        !t.Mirrored && (t.Point(new Vec2(0, 0)) - new Vec2(0, 0)).Length < 1e-12 && (t.Point(new Vec2(1, 0)) - new Vec2(1, 0)).Length < 1e-12;
}
