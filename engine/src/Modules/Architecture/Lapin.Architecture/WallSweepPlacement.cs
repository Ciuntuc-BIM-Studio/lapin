using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A wall sweep or reveal moved, copied, turned or mirrored on its own, its wall staying (decision 298; MOVE and COPY
/// as doors and windows have them, <see cref="HostedOpenings"/>): the middle of its stretch on the wall's axis is carried,
/// and what of that goes along the wall shifts its start and end (a sweep along the whole face gets them, the face's ends
/// shifted); across the wall the move is left out; up (a move in an elevation or a section) its height changes. Landing
/// inside another wall of the storey, it goes onto that wall, as long, at the same level, on the face the carried face
/// point lands on. Turned or mirrored, it stays on its wall, on the face the transformation turns it to. A copy is a sweep of
/// the wall's own; a wall type's sweep moved on its wall keeps what changed as its own, and moved to another wall leaves
/// its type's sweep switched off on the first.
/// </summary>
sealed class WallSweepPlacement : IHostedPlacement
{
    public bool Handles(IModelView m, Entity e) => e.Class == WallSweeps.Class && WallSweeps.HostOf(m, e) != null;

    public IReadOnlyList<string> Transform(ICommandContext ctx, IReadOnlyList<Entity> elements, Placement2D t, bool copy)
    {
        var made = new List<string>();
        foreach (var given in elements)
        {
            var m = ctx.Model;
            if (m.Get(given.Id) is not { } s || WallSweeps.HostOf(m, s) is not { } wall) continue;
            if (WallSweeps.ReachOf(s, wall, m, ctx.Services, []) is not { } reach) continue;
            var (s0, s1) = (reach.S0, reach.S1);
            var mid = (s0 + s1) / 2;
            var f = Openings.FrameOf(m, wall, mid);
            var onAxis = f.Start + f.Dir * mid;
            var onFace = onAxis + f.Left * WallSweeps.FaceY(m, wall, WallSweeps.Side(s));
            var to = t.Point(onAxis);
            // Only a move or a copy takes it to another wall; turned or mirrored it stays on its own.
            var turned = t.Mirrored || Math.Abs(t.Turn) > 1e-12;
            var host = turned || HostedOpenings.Inside(m, wall, to) ? wall : HostedOpenings.WallAt(m, wall, to) ?? wall;
            var along = Openings.AlongOf(host, to);
            var attrs = s.CloneAttrs();
            static double R(double v) => Math.Round(v, 6);
            var height = s.Num("height");
            if (host.Id == wall.Id)
            {
                var delta = along - mid;
                if (Math.Abs(delta) > 1e-9) { attrs["start"] = R(Math.Max(0, s0 + delta)); attrs["end"] = R(Math.Max(0, s1 + delta)); }
                if (turned) attrs["side"] = WallSweeps.SideOf(wall, t.Point(onFace));
            }
            else
            {
                var half = (s1 - s0) / 2;
                attrs["start"] = R(Math.Max(0, along - half));
                attrs["end"] = R(along + half);
                attrs["side"] = WallSweeps.SideOf(host, t.Point(onFace));
                // At the same level over the new wall's base (under its top, from the top).
                if (!s.Bool("fromTop")) height += WallSweeps.Base(m, wall) - WallSweeps.Base(m, host);
            }
            height += s.Bool("fromTop") ? -t.Rise : t.Rise;
            if (Math.Abs(height - s.Num("height")) > 1e-9) attrs["height"] = Math.Round(height, 4);
            if (copy || host.Id != wall.Id) { attrs.Remove("typeSweep"); attrs.Remove("own"); }
            if (copy) { made.Add(WallSweeps.Make(ctx, host, attrs).Id); continue; }
            if (host.Id == wall.Id)
            {
                WallSweeps.MarkOwn(attrs, s);
                ctx.Replace(s.With(attrs: attrs));
                continue;
            }
            if (s.Str("typeSweep") is { } tag) WallSweepTypes.MarkOff(ctx, wall, tag);
            ctx.Unrelate(IfcClass.RelVoidsElement, s.Id);
            ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, host.Id, [s.Id]));
            if (Spatial.StoreyOf(m, host.Id) is { } storey && Spatial.StoreyOf(m, s.Id)?.Id != storey.Id) Spatial.Contain(ctx, storey.Id, s.Id);
            ctx.Replace(s.With(attrs: attrs));
        }
        return made;
    }
}
