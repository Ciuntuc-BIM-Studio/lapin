using System.Text.Json.Nodes;
using Lapin.Architecture.Stairs;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Copy to storeys for architecture: a room goes with its finishes, skirting and finish slab, a stair with the space
/// under it; a wall's roofs (<c>roofIds</c>) are a relation, made again among the copies only when relations are. Once
/// placed, a stair copied climbs to the storey above its new one (one fixed to the storey above its old one is fixed
/// to the one above its new one) with its own opening in the floor there; an opening a stair cut in a floor copied
/// without that stair goes.
/// </summary>
sealed class StoreyCopies : IStoreyCopy
{
    public IEnumerable<Entity> Riders(IModelView m, Entity e) => e.Class switch
    {
        Rooms.Class => RoomFinishes.Laid(m, e.Id),
        IfcClass.Stair or IfcClass.Ramp => m.OfClass(UnderStairs.Class).Where(x => x.Str("host") == e.Id),
        _ => [],
    };

    public IEnumerable<string> RelationKeys => ["roofIds"];

    public void Copied(ICommandContext ctx, StoreyCopyStep step)
    {
        var m = ctx.Model;
        var copies = step.Map.Values.ToHashSet();
        foreach (var (o, c) in step.Map)
        {
            if (m.Get(c) is not { } copy) continue;
            if (copy.Class == IfcClass.OpeningElement && copy.Str("stairId") is { } sid && !copies.Contains(sid))
            {
                ctx.Remove(copy.Id);
                continue;
            }
            if (copy.Class is not (IfcClass.Stair or IfcClass.Ramp)) continue;
            if (copy.Str("topStoreyId") is { } top && m.Get(o) is { } original && Spatial.StoreyOf(m, original.Id) is { } from)
            {
                var storeys = Spatial.Storeys(m).ToList();
                Entity? Above(Entity s) => storeys.FirstOrDefault(x => x.Num("elevation") > s.Num("elevation") + 1e-6);
                if (Above(from)?.Id == top)
                {
                    var attrs = copy.CloneAttrs();
                    if (Above(step.Target) is { } next) attrs["topStoreyId"] = next.Id;
                    else attrs.Remove("topStoreyId");
                    ctx.Replace(copy.With(attrs: attrs));
                }
            }
            StairBuilder.EnsureOpening(ctx, m.Get(c)!);
        }
    }
}
