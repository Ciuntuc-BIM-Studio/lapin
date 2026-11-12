using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A frameless glass door or window (tester's question 7, decision 444): toughened glass <see cref="Glass"/> thick, with no
/// frame and no sash, <see cref="Gap"/> clear of the opening's sides and head and of the panes beside it, a door's leaves
/// <see cref="Floor"/> over the floor. <see cref="Fixing"/> says what holds a hinged leaf: "pivot" (a floor spring and a top
/// pivot, patch fittings at its corners) or "hinges" (glass hinges clamped to the leaf, fixed to the jamb or the pane beside
/// it). Fixed panes stand in a channel on the floor or sill and under the head; a sliding leaf hangs from a track across the
/// head, in front of the panes (towards the room), past them by <see cref="Frameless.Overlap"/>. "floor-spring" (a forum
/// user's request, topic #28) hangs a swinging leaf on a floor spring let into the floor under its pivot, <see cref="Pivot"/>
/// in from the leaf's hinge edge, with a top pivot over it: <see cref="Spring"/> the model ("auto": the lightest that carries
/// the leaf, <see cref="Frameless.Springs"/>), <see cref="Bottom"/> what clamps the leaf's bottom corner ("patch", a patch
/// fitting, or "strap", a heel strap), <see cref="HoldOpen"/> the angle it holds open at (degrees; 0: none). Single or double
/// action is the leaf's operation (swing-… or double-acting-…).
/// </summary>
public sealed record JoineryFrameless(string Fixing = "pivot", double Glass = 0.012, double Gap = 0.004, double Floor = 0.008,
    string Spring = "auto", double Pivot = 0.055, string Bottom = "patch", double HoldOpen = 0);

/// <summary>
/// A floor spring's model (indicative sizes, as the makers' catalogues give them; the user's own model is a matter of the
/// fittings data): the heaviest and widest leaf it carries, its closing force as EN 1154 grades it, its box (along the leaf,
/// across it, deep).
/// </summary>
public sealed record FloorSpringModel(string Id, double MaxMass, double MaxWidth, string Strength, double Length, double Width, double Depth);

/// <summary>The rules and the body of a frameless glass type (<see cref="JoinerySpec.Frameless"/>).</summary>
public static class Frameless
{
    public static readonly string[] Fixings = ["pivot", "hinges", "floor-spring"];

    /// <summary>The floor springs offered, lightest first (indicative).</summary>
    public static readonly FloorSpringModel[] Springs =
    [
        new("60", 60, 0.95, "EN 1–3", 0.27, 0.07, 0.038),
        new("100", 100, 1.1, "EN 1–4", 0.28, 0.09, 0.045),
        new("150", 150, 1.4, "EN 3–6", 0.32, 0.1, 0.055),
        new("300", 300, 1.6, "EN 4–7", 0.35, 0.12, 0.07),
    ];

    /// <summary>What clamps a floor-spring leaf's bottom corner: a patch fitting or a heel strap.</summary>
    public static readonly string[] Bottoms = ["patch", "strap"];

    /// <summary>Toughened glass's density, kg/m³.</summary>
    public const double GlassDensity = 2500;

    /// <summary>A floor spring's cover plate: its thickness (flush with the floor finish) and how far it runs past the box each side.</summary>
    public const double CoverPlate = 0.003, CoverMargin = 0.01;

    /// <summary>A heel strap along the glass and up it.</summary>
    public const double StrapLength = 0.1, StrapHeight = 0.04;

    /// <summary>How far the spring's spindle (the pivot) stands from the box's end on the hinge side.</summary>
    const double Spindle = 0.04;

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
        if (f.Fixing == "floor-spring" && SpringProblem(s) is { } sp) return sp;
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

    /// <summary>Whether a leaf swings on a floor spring (a swinging leaf of a type hung so).</summary>
    public static bool OnSpring(JoinerySpec s, JoineryPanel p) =>
        s.Frameless is { Fixing: "floor-spring" } && Joinery.IsDoorLeaf(p.Operation) && !Slides(p.Operation);

    /// <summary>A leaf's glass mass, kg.</summary>
    public static double LeafMass(JoinerySpec s, JoineryPanel p)
    {
        var r = PaneRect(s, p);
        return (r.X1 - r.X0) * (r.Z1 - r.Z0) * s.Frameless!.Glass * GlassDensity;
    }

    /// <summary>
    /// The floor spring a type's leaves hang on: the one chosen, or ("auto") the lightest that carries the heaviest and widest
    /// of them; null when the type has none, or (auto) none carries it.
    /// </summary>
    public static FloorSpringModel? SpringOf(JoinerySpec s)
    {
        if (s.Frameless is not { Fixing: "floor-spring" } f) return null;
        if (f.Spring != "auto") return Springs.FirstOrDefault(m => m.Id == f.Spring);
        var leaves = Joinery.Normalised(s).Where(p => OnSpring(s, p)).ToList();
        var mass = leaves.Count == 0 ? 0 : leaves.Max(p => LeafMass(s, p));
        var width = leaves.Count == 0 ? 0 : leaves.Max(p => PaneRect(s, p).X1 - PaneRect(s, p).X0);
        return Springs.FirstOrDefault(m => mass <= m.MaxMass + 1e-9 && width <= m.MaxWidth + 1e-9);
    }

    static string Kg(double v) => Math.Round(v).ToString(System.Globalization.CultureInfo.InvariantCulture);
    static string Mm(double v) => Math.Round(v * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture);

    static string? SpringProblem(JoinerySpec s)
    {
        var f = s.Frameless!;
        if (f.Spring != "auto" && Springs.All(m => m.Id != f.Spring)) return $"„{f.Spring}” is not a floor spring (auto, {string.Join(", ", Springs.Select(m => m.Id))}).";
        if (!Bottoms.Contains(f.Bottom)) return "A floor spring's leaf is held at the bottom by a patch fitting or a heel strap.";
        if (!double.IsFinite(f.Pivot) || f.Pivot is < 0.03 or > 0.15) return "A floor spring's pivot stands 30 to 150 mm in from the leaf's edge.";
        if (!double.IsFinite(f.HoldOpen) || (f.HoldOpen != 0 && f.HoldOpen is < 60 or > 180)) return "A floor spring holds open at 60° to 180°, or not at all (0).";
        foreach (var p in Joinery.Normalised(s).Where(p => OnSpring(s, p)))
        {
            var r = PaneRect(s, p);
            var (w, h) = (r.X1 - r.X0, r.Z1 - r.Z0);
            if (f.Pivot > w / 3) return "The pivot stands within a third of the leaf's width from its edge: bring it in or widen the leaf.";
            var mass = LeafMass(s, p);
            var leaf = $"A leaf of {Mm(w)} × {Mm(h)} in {Mm(f.Glass)} mm glass weighs {Kg(mass)} kg";
            if (SpringOf(s) is not { } m) return $"{leaf}: it is heavier or wider than any floor spring carries ({Kg(Springs[^1].MaxMass)} kg, {Mm(Springs[^1].MaxWidth)} mm).";
            if (mass > m.MaxMass + 1e-9) return $"{leaf}: the floor spring „{m.Id}” carries {Kg(m.MaxMass)} kg; choose a stronger one.";
            if (w > m.MaxWidth + 1e-9) return $"{leaf}: the floor spring „{m.Id}” carries leaves up to {Mm(m.MaxWidth)} mm wide; choose a stronger one.";
        }
        return null;
    }

    /// <summary>A floor-spring leaf's pivot along the type's x: <see cref="JoineryFrameless.Pivot"/> in from its hinge edge.</summary>
    public static double PivotX(JoinerySpec s, JoineryPanel p)
    {
        var r = PaneRect(s, p);
        return p.Operation.EndsWith("-left") ? r.X0 + s.Frameless!.Pivot : r.X1 - s.Frameless!.Pivot;
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
                if (OnSpring(s, p) && SpringOf(s) is { } m)
                {
                    // The floor spring: its box let into the floor under the pivot, the spindle near its hinge end, the box running
                    // under the closed leaf; the cover plate over it flush with the floor finish; a top pivot in the gap over the leaf.
                    var px = PivotX(s, p);
                    var (b0, b1) = (px - sg * Spindle, px - sg * Spindle + sg * m.Length);
                    o.Add(new JoineryBox("hardware", new Vec3(Math.Min(b0, b1), y - m.Width / 2, -CoverPlate - m.Depth), new Vec3(Math.Max(b0, b1), y + m.Width / 2, -CoverPlate)) { Underfloor = true });
                    o.Add(new JoineryBox("hardware", new Vec3(Math.Min(b0, b1) - CoverMargin, y - m.Width / 2 - CoverMargin, -CoverPlate), new Vec3(Math.Max(b0, b1) + CoverMargin, y + m.Width / 2 + CoverMargin, 0)) { Underfloor = true });
                    o.Add(new JoineryBox("hardware", new Vec3(px - 0.02, y - 0.02, r.Z1), new Vec3(px + 0.02, y + 0.02, s.Height)));
                    // The top patch fitting the top pivot turns, the bottom one (or a heel strap) the spindle, a lock patch opposite.
                    o.Add(Clamp(xh, xh + sg * PatchLength, r.Z1 - PatchHeight, r.Z1, y, true));
                    o.Add(f.Bottom == "strap" ? Clamp(xh, xh + sg * StrapLength, r.Z0, r.Z0 + StrapHeight, y, true) : Clamp(xh, xh + sg * PatchLength, r.Z0, r.Z0 + PatchHeight, y, true));
                    o.Add(Clamp(xl, xl - sg * PatchLength, r.Z0, r.Z0 + PatchHeight, y, true));
                }
                else if (f.Fixing == "hinges")
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

/// <summary>
/// A frameless glass door's floor spring in IFC (topic #28): a set of Lapin's, <c>Lapin_FloorSpring</c>, on the door (its type's
/// spec): the model, what it carries, its closing force, the leaf's weight, single or double action, the pivot's offset, the
/// fittings, the hold-open angle and the box. The spring and the fittings stay parts of the door's body, as the patch fittings do.
/// </summary>
sealed class FloorSpringSets : IIfcElementSets
{
    public const string Set = "Lapin_FloorSpring";

    public IEnumerable<IfcStandardSet> Sets(IModelView model, Entity element)
    {
        if (element.Class != IfcClass.Door || Openings.SpecOf(model, element.Id) is not { Frameless: { Fixing: "floor-spring" } f } spec) yield break;
        var leaves = Joinery.Normalised(spec).Where(p => Frameless.OnSpring(spec, p)).ToList();
        if (leaves.Count == 0 || Frameless.SpringOf(spec) is not { } m) yield break;
        var dbl = leaves.Count(p => p.Operation.StartsWith("double-acting", StringComparison.Ordinal));
        var props = new List<IfcStandardProperty>
        {
            new("Model", "IFCLABEL", m.Id),
            new("MaxLeafMass", "IFCMASSMEASURE", m.MaxMass),
            new("MaxLeafWidth", "IFCPOSITIVELENGTHMEASURE", m.MaxWidth),
            new("ClosingForce", "IFCLABEL", m.Strength),
            new("LeafMass", "IFCMASSMEASURE", Math.Round(leaves.Max(p => Frameless.LeafMass(spec, p)), 1)),
            new("Action", "IFCLABEL", dbl == leaves.Count ? "double action" : dbl == 0 ? "single action" : "single and double action"),
            new("PivotOffset", "IFCPOSITIVELENGTHMEASURE", f.Pivot),
            new("TopFitting", "IFCLABEL", "top pivot and patch fitting"),
            new("BottomFitting", "IFCLABEL", f.Bottom == "strap" ? "heel strap" : "patch fitting"),
            new("BoxLength", "IFCPOSITIVELENGTHMEASURE", m.Length),
            new("BoxWidth", "IFCPOSITIVELENGTHMEASURE", m.Width),
            new("BoxDepth", "IFCPOSITIVELENGTHMEASURE", m.Depth),
        };
        if (f.HoldOpen > 0) props.Add(new("HoldOpenAngle", "IFCPLANEANGLEMEASURE", f.HoldOpen * Math.PI / 180));
        yield return new IfcStandardSet(Set, props);
    }
}
