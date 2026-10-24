using BubbleBim.Geometry;

namespace Lapin.Architecture.Schedules;

/// <summary>
/// A door or window type in plan, as a schedule's card shows it: in a wall
/// stub as deep as <paramref name="depth"/>, its jambs, a door's leaves open at
/// 90° into the interior (below) with their arcs, a window's frame and its
/// double glass line. x along the width (centred), y across the wall (up: the exterior).
/// </summary>
public static class JoineryPlan
{
    public static List<Stroke> Of(JoinerySpec spec, double depth)
    {
        var o = new List<Stroke>();
        var half = spec.Width / 2;
        void L(double x0, double y0, double x1, double y1, string w) => o.Add(new(new(x0, y0), new(x1, y1), w));
        // The wall either side, cut, and the jambs.
        foreach (var s in new[] { -1, 1 })
        {
            var (j, end) = (s * half, s * (half + Math.Max(0.25, spec.Width * 0.25)));
            L(j, -depth / 2, end, -depth / 2, "cut"); L(j, depth / 2, end, depth / 2, "cut"); L(j, -depth / 2, j, depth / 2, "cut");
        }
        if (spec.IsDoor)
        {
            foreach (var leaf in Joinery.Normalised(spec).Where(x => Joinery.IsDoorLeaf(x.Operation) && Joinery.SwingsInPlan(x.Operation)))
            {
                var r = Joinery.PanelRect(spec, leaf);
                var hingeLeft = leaf.Operation.EndsWith("-left");
                var (hinge, free) = hingeLeft ? (r.X0, r.X1) : (r.X1, r.X0);
                var w = Math.Abs(free - hinge);
                var y0 = -depth / 2;
                var t = Math.Clamp(spec.Frame.Depth * 0.4, 0.02, 0.05) * Math.Sign(free - hinge);
                L(hinge, y0, hinge, y0 - w, "cut"); L(hinge, y0 - w, hinge + t, y0 - w, "cut"); L(hinge + t, y0 - w, hinge + t, y0, "cut");
                Vec2? last = null;
                for (var i = 0; i <= 16; i++)
                {
                    var a = i / 16.0 * Math.PI / 2;
                    var p = new Vec2(hinge + Math.Sign(free - hinge) * w * Math.Cos(a), y0 - w * Math.Sin(a));
                    if (last is { } q) o.Add(new(q, p, "thin"));
                    last = p;
                }
            }
        }
        else
        {
            var (fd, off) = (spec.Frame.Depth, spec.Frame.Offset);
            var g = Math.Min(0.012, fd / 3);
            L(-half, off - fd / 2, half, off - fd / 2, "outline"); L(half, off - fd / 2, half, off + fd / 2, "outline");
            L(half, off + fd / 2, -half, off + fd / 2, "outline"); L(-half, off + fd / 2, -half, off - fd / 2, "outline");
            L(-half, off - g, half, off - g, "thin"); L(-half, off + g, half, off + g, "thin");
        }
        return o;
    }
}
