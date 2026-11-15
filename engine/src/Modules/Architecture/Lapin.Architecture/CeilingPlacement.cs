using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A ceiling moved, turned, mirrored or copied takes its grid with it (audit 15.11.2026, M4): its own origin carried, its
/// turn kept on the ceiling (<see cref="Ceilings.TurnKey"/>, on top of the type's rotation), and, mirrored, its origin put
/// so the cells run as their mirror image, the fixtures in the cells they were in (a cell's row counted the other way).
/// </summary>
sealed class CeilingPlacement : IElementPlacement
{
    public bool Handles(Entity e) => e.Class == Ceilings.Class;

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        var attrs = Carried(ctx.Model, e, t);
        Placement.Own(ctx, e, t, moving);
        ctx.Replace(ctx.Model.Get(e.Id)!.With(attrs: attrs));
    }

    public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        var attrs = Carried(ctx.Model, e, t);
        var id = Placement.DeepCopy(ctx, e, t);
        ctx.Replace(ctx.Model.Get(id)!.With(attrs: attrs));
        return id;
    }

    static JsonObject Carried(IModelView m, Entity e, Placement2D t)
    {
        var attrs = Placement.Carry(e, t);
        if (Slabs.Contour(e).Count < 3) return attrs;
        var grid = Ceilings.GridOf(m, e);
        var u = t.Dir(grid.U);
        var turn = Math.Atan2(u.Y, u.X) * 180 / Math.PI - Ceilings.SpecOf(m, e).Rotation;
        turn = Math.Round(turn - 360 * Math.Round(turn / 360), 9);
        if (Math.Abs(turn) > 1e-9) attrs[Ceilings.TurnKey] = turn; else attrs.Remove(Ceilings.TurnKey);
        var origin = t.Point(grid.Origin);
        if (t.Mirrored)
        {
            // The cells' rows run the other way: from an origin a row up, row j is row −j.
            origin -= u.Perp * grid.L;
            attrs["fixtures"] = new JsonArray(Ceilings.Fixtures(e).Select(f => (JsonNode)new JsonObject { ["kind"] = f.Kind, ["i"] = f.I, ["j"] = -f.J }).ToArray());
        }
        // A grid centred (no origin of its own) stays centred: its centre goes with the outline's.
        if (Ceilings.OriginOf(e) != null) attrs["origin"] = new JsonArray(Math.Round(origin.X, 9), Math.Round(origin.Y, 9));
        return attrs;
    }
}
