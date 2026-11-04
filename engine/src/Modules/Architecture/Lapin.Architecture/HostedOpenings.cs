using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Doors, windows and bare openings moved, copied, turned or mirrored on their
/// own (MOVE, COPY, ROTATE, MIRROR on the command line; tester's report #82),
/// their wall staying: each goes where the transformation takes the middle of
/// its opening on the wall's axis. Landing inside another wall of the storey (in
/// its thickness), it goes into that wall; anywhere else it stays in its own,
/// at the point's place along it, so a move keeps only its part along the wall.
/// Its sill stays. A door or window keeps the way it opens in the world: what
/// the transformation turns against its new wall's direction or faces flips it
/// (a mirror changes its hand). Refused, as a whole, where one does not fit.
/// </summary>
sealed class HostedOpenings : IHostedPlacement
{
    /// <summary>A door or window in a wall's opening, or an opening in a wall (a floor's opening is its slab's, not this).</summary>
    public bool Handles(IModelView m, Entity e) => e.Class is IfcClass.Door or IfcClass.Window
        ? Openings.OpeningOf(m, e.Id) is { } o && Openings.WallOf(m, o.Id) is { Class: IfcClass.Wall }
        : e.Class == IfcClass.OpeningElement && Openings.WallOf(m, e.Id) is { Class: IfcClass.Wall };

    readonly record struct Target(Entity Opening, Entity? Fill, Entity Wall, Entity Host, double Along, double Sill, double Width, double Height, bool FlipHand, bool FlipSide);

    public IReadOnlyList<string> Transform(ICommandContext ctx, IReadOnlyList<Entity> elements, Placement2D t, bool copy)
    {
        var m = ctx.Model;
        var targets = elements.Select(e => TargetOf(m, e, t)).DistinctBy(x => x.Opening.Id).ToList();
        if (copy) return targets.Select(x => Copy(ctx, x)).ToList();
        // Moved together, then checked where they all are: two windows moved along their wall do not meet on the way.
        foreach (var x in targets)
        {
            var attrs = x.Opening.CloneAttrs();
            attrs["along"] = x.Along;
            ctx.Replace(x.Opening.With(attrs: attrs));
            if (x.Host.Id != x.Wall.Id)
            {
                ctx.Unrelate(IfcClass.RelVoidsElement, x.Opening.Id);
                ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, x.Host.Id, [x.Opening.Id]));
                if (x.Fill != null && Spatial.StoreyOf(m, x.Host.Id) is { } storey && Spatial.StoreyOf(m, x.Fill.Id)?.Id != storey.Id) Spatial.Contain(ctx, storey.Id, x.Fill.Id);
            }
            if (x.Fill != null && ctx.Model.Get(x.Fill.Id) is { } fill) ctx.Replace(fill.With(attrs: Flipped(fill, x)));
        }
        foreach (var x in targets) Openings.CheckFits(m, x.Host, x.Along, x.Sill, x.Width, x.Height, x.Opening.Id);
        return [];
    }

    /// <summary>Where one goes: its middle on the wall's axis carried, the wall it lands in (else its own), its place along that one, the flips.</summary>
    static Target TargetOf(IModelView m, Entity e, Placement2D t)
    {
        var opening = e.Class == IfcClass.OpeningElement ? e : Openings.OpeningOf(m, e.Id)!;
        var fill = e.Class == IfcClass.OpeningElement ? Openings.FillOf(m, e.Id) : e;
        var wall = Openings.WallOf(m, opening.Id)!;
        var placed = Openings.Place(m, opening);
        var f = Openings.FrameOf(m, wall, placed.Along);
        var to = t.Point(f.Start + f.Dir * placed.Along);
        var host = Inside(m, wall, to) ? wall : WallAt(m, wall, to) ?? wall;
        var along = Openings.AlongOf(host, to);
        var g = Openings.FrameOf(m, host, along);
        // Its frame carried, against its new wall's: along the wall turned back flips the hand, across it the side.
        var hand = Vec2.Dot(t.Dir(f.Dir), g.Dir) < 0;
        var side = Vec2.Dot(t.Dir(f.Left), g.Left) < 0;
        return new Target(opening, fill, wall, host, along, placed.Sill, placed.Width, placed.Height, hand, side);
    }

    /// <summary>Whether a point lies in the wall's thickness, along its length.</summary>
    static bool Inside(IModelView m, Entity wall, Vec2 p)
    {
        var along = Openings.AlongOf(wall, p);
        if (along < -1e-6 || along > PathCurve.Of(wall).Length + 1e-6) return false;
        var f = Openings.FrameOf(m, wall, along);
        var across = Vec2.Dot(p - (f.Start + f.Dir * along), f.Left);
        return across >= f.YMin - 1e-6 && across <= f.YMax + 1e-6;
    }

    /// <summary>Another wall of the storey the point lies in, if any.</summary>
    static Entity? WallAt(IModelView m, Entity wall, Vec2 p)
    {
        var storey = Spatial.StoreyOf(m, wall.Id)?.Id;
        return m.OfClass(IfcClass.Wall).FirstOrDefault(w => w.Id != wall.Id && Spatial.StoreyOf(m, w.Id)?.Id == storey && Inside(m, w, p));
    }

    static JsonObject Flipped(Entity fill, Target x)
    {
        var attrs = fill.CloneAttrs();
        if (x.FlipHand) attrs["flipHand"] = !fill.Bool("flipHand");
        if (x.FlipSide) attrs["flipSide"] = !fill.Bool("flipSide");
        return attrs;
    }

    /// <summary>A copy where it goes: its opening (a bare one's size and head with it), and a door or window of the same type, numbered on.</summary>
    static string Copy(ICommandContext ctx, Target x)
    {
        var m = ctx.Model;
        Openings.CheckFits(m, x.Host, x.Along, x.Sill, x.Width, x.Height);
        var attrs = x.Opening.CloneAttrs();
        attrs["along"] = x.Along;
        var opening = new Entity(ctx.NewId(), IfcClass.OpeningElement, x.Opening.Name, attrs, x.Opening.PredefinedType);
        ctx.Add(opening);
        ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, x.Host.Id, [opening.Id]));
        if (x.Fill == null) return opening.Id;
        var type = m.RelatingOf(x.Fill.Id, IfcClass.RelDefinesByType);
        var mark = type != null && JoinerySpec.From(type.Attrs["spec"]) is { } spec ? spec.Mark : "";
        var name = mark.Length > 0 ? $"{mark}{Numbering.Next(m.OfClass(x.Fill.Class))}" : x.Fill.Name;
        var fill = new Entity(ctx.NewId(), x.Fill.Class, name, Flipped(x.Fill, x), x.Fill.PredefinedType);
        ctx.Add(fill);
        ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelFillsElement, opening.Id, [fill.Id]));
        if (type != null) ctx.Relate(IfcClass.RelDefinesByType, type.Id, fill.Id);
        if (Spatial.StoreyOf(m, x.Host.Id) is { } storey) Spatial.Contain(ctx, storey.Id, fill.Id);
        return fill.Id;
    }
}
