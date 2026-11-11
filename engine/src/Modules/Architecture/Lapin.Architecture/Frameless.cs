using Lapin.Geometry;

namespace Lapin.Architecture;

/// <summary>
/// A frameless glass door or window (tester's question 7, decision 444): toughened glass <see cref="Glass"/> thick, with no
/// frame and no sash, <see cref="Gap"/> clear of the opening's sides and head and of the panes beside it, a door's leaves
/// <see cref="Floor"/> over the floor. <see cref="Fixing"/> says what holds a hinged leaf: "pivot" (a floor spring and a top
/// pivot, patch fittings at its corners) or "hinges" (glass hinges clamped to the leaf, fixed to the jamb or the pane beside
/// it). Fixed panes stand in a channel on the floor or sill and under the head; a sliding leaf hangs from a track across the
/// head, in front of the panes (towards the room), past them by <see cref="Frameless.Overlap"/>.
/// </summary>
public sealed record JoineryFrameless(string Fixing = "pivot", double Glass = 0.012, double Gap = 0.004, double Floor = 0.008);

/// <summary>The rules and the body of a frameless glass type (<see cref="JoinerySpec.Frameless"/>).</summary>
public static class Frameless
{
    public static readonly string[] Fixings = ["pivot", "hinges"];

    /// <summary>What a frameless panel may do: a fixed pane, a leaf that swings, acts both ways or slides.</summary>
    public static readonly string[] Operations =
        ["fixed", "swing-left", "swing-right", "double-acting-left", "double-acting-right", "door-sliding-left", "door-sliding-right"];

    /// <summary>A patch fitting's, hinge's or channel's plate each side of the glass.</summary>
    public const double Plate = 0.012;

    /// <summary>The sliding track's height under the head.</summary>
    public const double Rail = 0.07;

    /// <summary>How far a sliding leaf runs past the panes beside it, closed.</summary>
    public const double Overlap = 0.05;

    /// <summary>A fixed pane's channel: its height.</summary>
    const double Channel = 0.03;

    /// <summary>A patch fitting along the glass and up it.</summary>
    const double PatchLength = 0.18, PatchHeight = 0.055;

    /// <summary>A glass hinge along the glass and up it.</summary>
    const double HingeLength = 0.085, HingeHeight = 0.09;

    /// <summary>Whether a type is frameless glass.</summary>
    public static bool Of(JoinerySpec s) => s.Frameless != null;

    /// <summary>Why a frameless type cannot be built, or null.</summary>
    public static string? Problem(JoinerySpec s)
    {
        var f = s.Frameless!;
        if (s.Bay != null) return "A bay, bow or box window has frames: frameless glass stands flat in its wall.";
        if (JoineryShapes.Shaped(s)) return "Frameless glass is a rectangle: choose the rectangle shape.";
        if (!Fixings.Contains(f.Fixing)) return $"„{f.Fixing}” is not a fixing of frameless glass ({string.Join(", ", Fixings)}).";
        if (!double.IsFinite(f.Glass) || f.Glass is < 0.006 or > 0.025) return "Frameless glass is toughened glass 6 to 25 mm thick.";
        if (!double.IsFinite(f.Gap) || f.Gap is < 0 or > 0.02) return "The gap round frameless glass is 0 to 20 mm.";
        if (!double.IsFinite(f.Floor) || f.Floor is < 0 or > 0.03) return "The gap under a frameless leaf is 0 to 30 mm.";
        foreach (var p in Joinery.Normalised(s))
        {
            if (!s.IsDoor && p.Operation != "fixed") return "A frameless window is fixed glazing: its panes do not open.";
            if (!Operations.Contains(p.Operation)) return "A frameless leaf swings, swings both ways or slides; folding needs frames.";
            var r = PaneRect(s, p);
            if (r.X1 - r.X0 < 0.15 || r.Z1 - r.Z0 < 0.15) return "A pane of frameless glass is too small: widen it.";
            if (Joinery.IsDoorLeaf(p.Operation) && r.Z0 > Joinery.InnerRect(s).Z0 + 1e-6) return "A frameless leaf stands on the floor: put it in the bottom row.";
        }
        return null;
    }

    /// <summary>The clear rectangle the panes share: the opening less the gap, a door's from the floor gap.</summary>
    public static CellRect Inner(JoinerySpec s)
    {
        var f = s.Frameless!;
        return new CellRect(-s.Width / 2 + f.Gap, s.Width / 2 - f.Gap, s.IsDoor ? f.Floor : f.Gap, s.Height - f.Gap);
    }

    public static bool Slides(string op) => op.StartsWith("door-sliding");

    /// <summary>
    /// A pane's glass: its cell, and for a sliding leaf the cell widened past the panes beside it by <see cref="Overlap"/> and
    /// lowered under the track.
    /// </summary>
    public static CellRect PaneRect(JoinerySpec s, JoineryPanel p)
    {
        var r = Joinery.PanelRect(s, p);
        if (!Slides(p.Operation)) return r;
        var inner = Inner(s);
        return new CellRect(Math.Max(inner.X0, r.X0 - Overlap), Math.Min(inner.X1, r.X1 + Overlap), r.Z0, Math.Min(r.Z1, s.Height - Rail - s.Frameless!.Gap));
    }

    /// <summary>The plane a pane's glass stands in (its centre across the wall): the frame's, a sliding leaf's in front of it towards the room.</summary>
    public static double Plane(JoinerySpec s, JoineryPanel p) => Slides(p.Operation) ? SlidingPlane(s) : s.Frame.Offset;

    static double SlidingPlane(JoinerySpec s) => s.Frame.Offset - (s.Frameless!.Glass + 0.02);

    /// <summary>The body: each pane's glass, the channels, patch fittings, hinges, the track and rollers, the handles.</summary>
    public static List<JoineryBox> Boxes(JoinerySpec s)
    {
        var f = s.Frameless!;
        var g = f.Glass;
        var inner = Inner(s);
        var o = new List<JoineryBox>();
        // A piece of hardware clamping the glass from both faces, standing Plate proud of each.
        JoineryBox Clamp(double x0, double x1, double z0, double z1, double y, bool leaf) =>
            new("hardware", new Vec3(Math.Min(x0, x1), y - g / 2 - Plate, z0), new Vec3(Math.Max(x0, x1), y + g / 2 + Plate, z1)) { Leaf = leaf };
        foreach (var p in Joinery.Normalised(s))
        {
            var r = PaneRect(s, p);
            var y = Plane(s, p);
            var leaf = Joinery.IsDoorLeaf(p.Operation);
            o.Add(new JoineryBox("glass", new Vec3(r.X0, y - g / 2, r.Z0), new Vec3(r.X1, y + g / 2, r.Z1)) { Leaf = leaf });
            if (p.Operation == "fixed")
            {
                // In a channel on the floor or sill and under the head, where the pane reaches them.
                if (r.Z0 <= inner.Z0 + 1e-6) o.Add(Clamp(r.X0, r.X1, r.Z0, r.Z0 + Channel, y, false));
                if (r.Z1 >= inner.Z1 - 1e-6) o.Add(Clamp(r.X0, r.X1, r.Z1 - Channel, r.Z1, y, false));
                continue;
            }
            if (Slides(p.Operation))
            {
                // Two rollers clamping the leaf's top, hanging from the track.
                foreach (var x in new[] { r.X0 + 0.15, r.X1 - 0.15 })
                    o.Add(Clamp(x - 0.04, x + 0.04, r.Z1 - 0.06, r.Z1 + f.Gap, y, true));
                // A guide on the floor at the end it slides from, holding the leaf's bottom edge.
                var from = p.Operation.EndsWith("-left") ? r.X1 : r.X0;
                o.Add(new JoineryBox("hardware", new Vec3(from - 0.03, y - g / 2 - 0.008, 0), new Vec3(from + 0.03, y + g / 2 + 0.008, Math.Max(r.Z0, 0.012))));
            }
            else
            {
                var hingeLeft = p.Operation.EndsWith("-left");
                var (xh, xl, sg) = hingeLeft ? (r.X0, r.X1, 1) : (r.X1, r.X0, -1);
                if (f.Fixing == "hinges")
                {
                    // Glass hinges on the hinge side, reaching across the gap to the jamb or the pane beside it; three on a tall leaf.
                    var h = r.Z1 - r.Z0;
                    var zs = h > 2.3 ? new[] { r.Z0 + 0.25, (r.Z0 + r.Z1) / 2, r.Z1 - 0.25 } : [r.Z0 + 0.25, r.Z1 - 0.25];
                    foreach (var z in zs) o.Add(Clamp(xh - sg * f.Gap, xh + sg * HingeLength, z - HingeHeight / 2, z + HingeHeight / 2, y, false));
                }
                else
                {
                    // Patch fittings at the hinge side's corners (the top pivot and the floor spring turn them), a lock patch at the bottom opposite.
                    o.Add(Clamp(xh, xh + sg * PatchLength, r.Z0, r.Z0 + PatchHeight, y, true));
                    o.Add(Clamp(xh, xh + sg * PatchLength, r.Z1 - PatchHeight, r.Z1, y, true));
                    o.Add(Clamp(xl, xl - sg * PatchLength, r.Z0, r.Z0 + PatchHeight, y, true));
                }
            }
            // A handle on both faces.
            foreach (var b in Joinery.HandleBoxes(s, r, p.Operation, y - g / 2, -1, true)) o.Add(b with { Leaf = true });
            foreach (var b in Joinery.HandleBoxes(s, r, p.Operation, y + g / 2, +1, true)) o.Add(b with { Leaf = true });
        }
        // The track across the head, in front of the panes.
        if (Joinery.Normalised(s).Any(p => Slides(p.Operation)))
        {
            var ys = SlidingPlane(s);
            o.Add(new JoineryBox("hardware", new Vec3(inner.X0, ys - g / 2 - 0.02, s.Height - Rail), new Vec3(inner.X1, ys + g / 2 + 0.02, s.Height)));
        }
        return o.Where(b => b.Max.X - b.Min.X > 1e-6 && b.Max.Y - b.Min.Y > 1e-6 && b.Max.Z - b.Min.Z > 1e-6).ToList();
    }
}
