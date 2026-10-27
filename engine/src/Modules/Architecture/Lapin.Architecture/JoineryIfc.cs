using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Doors and windows in IFC's terms, both ways (docs/spec-ifc.md, "Deschiderea
/// ușilor și ferestrelor").
/// <para>
/// The rule: a door's or window's local +Y is the way its panels open, and left
/// and right are as seen looking that way (IFC's IfcDoorTypeOperationEnum; a
/// window's "as seen from outside" is the same thing for one opening inwards).
/// The configurator's names already are that: a type's panels open to the
/// interior (−Y of the type frame), and "swing-left" has its hinges on the left
/// of someone pushing it open. An element flipped on one axis (hand or side)
/// is its type's mirror image; flipped on both, it is the type turned round.
/// </para>
/// </summary>
public sealed class JoineryIfc : IJoineryIfc
{
    public IfcJoineryType? Type(IModelView m, Entity type, bool mirrored) =>
        JoinerySpec.From(type.Attrs["spec"]) is { } spec ? Of(spec, mirrored) : null;

    public static IfcJoineryType Of(JoinerySpec spec, bool mirrored = false)
    {
        string M(string v) => mirrored ? IfcHands.Mirror(v) : v;
        var (op, user) = spec.IsDoor ? Joinery.DoorOperation(spec) : Joinery.WindowPartitioning(spec);
        // A door panel's width is a ratio of the clear opening's (inside the lining).
        var inner = Joinery.InnerRect(spec);
        var clear = Math.Max(1e-9, inner.X1 - inner.X0);
        var panels = Joinery.Normalised(spec)
            .Where(p => !spec.IsDoor || p.RowSpan == spec.Rows.Length || Joinery.IsDoorLeaf(p.Operation))
            .OrderBy(p => p.Row).ThenBy(p => p.Col)
            .Select(p =>
            {
                var r = Joinery.PanelRect(spec, p);
                return spec.IsDoor
                    ? new IfcJoineryPanel(Joinery.DoorPanelOperation(p.Operation), M(Joinery.PanelPosition(spec, p, "door")), Math.Round(Math.Min(1, (r.X1 - r.X0) / clear), 4))
                    : new IfcJoineryPanel(M(Joinery.WindowPanelOperation(p.Operation)), M(Joinery.PanelPosition(spec, p, "window")));
            }).ToList();
        var (mullions, transoms) = Joinery.LiningOffsets(spec);
        if (mirrored) mullions = mullions.Select(x => Math.Round(1 - x, 6)).Reverse().ToList();
        var lining = new IfcJoineryLining(spec.Frame.Depth, spec.Frame.Width, spec.Frame.Mullion, spec.Frame.Transom, spec.IsDoor ? Math.Max(0, spec.Threshold) : 0, mullions, transoms);
        return new IfcJoineryType(spec.IsDoor, M(op), user is { } u && mirrored ? IfcHands.MirrorText(u) : user, panels, lining);
    }

    public IfcJoineryPlace? Place(IModelView m, Entity element)
    {
        var opening = Openings.OpeningOf(m, element.Id);
        var wall = opening == null ? null : Openings.WallOf(m, opening.Id);
        if (opening == null || wall == null) return null;
        var p = Openings.Place(m, opening);
        var f = Openings.FrameOf(m, wall, p.Along);
        var sx = element.Bool("flipHand") ? -1 : 1;
        var sy = element.Bool("flipSide") ? -1 : 1;
        // The type frame's −Y (the interior, where the panels open) is the wall's left; a flipped side opens the other way.
        var y = f.Left * sy;
        var x = new Vec2(y.Y, -y.X);
        var face = sy > 0 ? f.YMin : f.YMax;
        var mid = f.At(p.Along, face, p.Sill);
        var origin = mid - new Vec3(x.X, x.Y, 0) * (p.Width / 2);
        return new IfcJoineryPlace(origin, new Vec3(x.X, x.Y, 0), new Vec3(y.X, y.Y, 0), sx * sy < 0);
    }

    // ─── From IFC ───────────────────────────────────────────────────────────

    static string? Lapin(string ifc) => ifc switch
    {
        "FIXEDCASEMENT" => "fixed",
        "SIDEHUNGLEFTHAND" => "side-left",
        "SIDEHUNGRIGHTHAND" => "side-right",
        "TILTANDTURNLEFTHAND" => "tilt-turn-left",
        "TILTANDTURNRIGHTHAND" => "tilt-turn-right",
        "BOTTOMHUNG" => "bottom-hung",
        "TOPHUNG" => "top-hung",
        "PIVOTHORIZONTAL" => "pivot-horizontal",
        "PIVOTVERTICAL" => "pivot-vertical",
        "SLIDINGVERTICAL" => "sliding-vertical",
        _ => null,
    };

    /// <summary>The door leaves an OperationType names, left to right; null when it names none Lapin draws.</summary>
    static string[]? Leaves(string op) => op switch
    {
        "SINGLE_SWING_LEFT" => ["swing-left"],
        "SINGLE_SWING_RIGHT" => ["swing-right"],
        "DOUBLE_SWING_LEFT" => ["double-acting-left"],
        "DOUBLE_SWING_RIGHT" => ["double-acting-right"],
        "SLIDING_TO_LEFT" => ["door-sliding-left"],
        "SLIDING_TO_RIGHT" => ["door-sliding-right"],
        "FOLDING_TO_LEFT" => ["folding-left"],
        "FOLDING_TO_RIGHT" => ["folding-right"],
        "SWING_FIXED_LEFT" => ["swing-left", "fixed"],
        "SWING_FIXED_RIGHT" => ["fixed", "swing-right"],
        "DOUBLE_DOOR_SINGLE_SWING" => ["swing-left", "swing-right"],
        "DOUBLE_DOOR_SINGLE_SWING_OPPOSITE_LEFT" => ["swing-left", "swing-left"],
        "DOUBLE_DOOR_SINGLE_SWING_OPPOSITE_RIGHT" => ["swing-right", "swing-right"],
        "DOUBLE_DOOR_DOUBLE_SWING" => ["double-acting-left", "double-acting-right"],
        "DOUBLE_DOOR_SLIDING" => ["door-sliding-left", "door-sliding-right"],
        "DOUBLE_DOOR_FOLDING" => ["folding-left", "folding-right"],
        _ => null,
    };

    static int Order(string position) => position switch { "LEFT" => 0, "RIGHT" => 2, _ => 1 };

    /// <summary>
    /// A type's panels from what an IFC file says of it: a door's OperationType (or
    /// its user-defined text when it is Lapin's own, or its panels' properties), a
    /// window's PartitioningType with its panels' operations by position. What the
    /// file does not say keeps <paramref name="baseSpec"/>'s; null when it says
    /// nothing Lapin can use.
    /// </summary>
    public static JoinerySpec? FromIfc(JoinerySpec baseSpec, string? operation, string? userDefined, IReadOnlyList<IfcJoineryPanel>? panels)
    {
        operation = operation is "NOTDEFINED" or "" ? null : operation;
        panels ??= [];
        return baseSpec.IsDoor ? Door(baseSpec, operation, userDefined, panels) : Window(baseSpec, operation, panels);
    }

    static JoinerySpec? Door(JoinerySpec s, string? operation, string? userDefined, IReadOnlyList<IfcJoineryPanel> panels)
    {
        string[]? leaves = operation == null ? null : Leaves(operation);
        double[]? widths = null;
        if (leaves == null && userDefined is { Length: > 0 } text)
        {
            // Lapin's own user-defined text: its operations, left to right.
            var ops = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (ops.Length > 0 && ops.All(o => o == "fixed" || Joinery.IsDoorLeaf(o))) leaves = ops;
        }
        if (panels.Count > 0)
        {
            var ordered = panels.OrderBy(p => Order(p.Position)).ToList();
            if (leaves == null)
                leaves = [.. ordered.Select(p => p.Operation switch
                {
                    "SWINGING" => p.Position == "RIGHT" ? "swing-right" : "swing-left",
                    "DOUBLE_ACTING" => p.Position == "RIGHT" ? "double-acting-right" : "double-acting-left",
                    "SLIDING" => p.Position == "RIGHT" ? "door-sliding-right" : "door-sliding-left",
                    "FOLDING" => p.Position == "RIGHT" ? "folding-right" : "folding-left",
                    _ => "fixed",
                })];
            if (ordered.Count == leaves.Length && ordered.All(p => p.Width is > 0 and <= 1)) widths = [.. ordered.Select(p => p.Width!.Value)];
        }
        if (leaves == null || leaves.Length == 0) return null;
        widths ??= leaves.Length == 2 && leaves.Contains("fixed") ? leaves.Select(l => l == "fixed" ? 1.0 : 2.0).ToArray() : leaves.Select(_ => 1.0).ToArray();
        return s with { Columns = widths, Rows = [1], Panels = [.. leaves.Select((op, i) => new JoineryPanel(i, 0, op))] };
    }

    static JoinerySpec? Window(JoinerySpec s, string? partitioning, IReadOnlyList<IfcJoineryPanel> panels)
    {
        (double[] Cols, double[] Rows, List<JoineryPanel> Cells)? grid = partitioning switch
        {
            "SINGLE_PANEL" => ([1], [1], [new(0, 0, "")]),
            "DOUBLE_PANEL_VERTICAL" => ([1, 1], [1], [new(0, 0, ""), new(1, 0, "")]),
            "TRIPLE_PANEL_VERTICAL" => ([1, 1, 1], [1], [new(0, 0, ""), new(1, 0, ""), new(2, 0, "")]),
            "DOUBLE_PANEL_HORIZONTAL" => ([1], [1, 1], [new(0, 0, ""), new(0, 1, "")]),
            "TRIPLE_PANEL_HORIZONTAL" => ([1], [1, 1, 1], [new(0, 0, ""), new(0, 1, ""), new(0, 2, "")]),
            "TRIPLE_PANEL_BOTTOM" => ([1, 1], [1, 1], [new(0, 0, "", ColSpan: 2), new(0, 1, ""), new(1, 1, "")]),
            "TRIPLE_PANEL_TOP" => ([1, 1], [1, 1], [new(0, 0, ""), new(1, 0, ""), new(0, 1, "", ColSpan: 2)]),
            "TRIPLE_PANEL_LEFT" => ([1, 1], [1, 1], [new(0, 0, "", RowSpan: 2), new(1, 0, ""), new(1, 1, "")]),
            "TRIPLE_PANEL_RIGHT" => ([1, 1], [1, 1], [new(0, 0, ""), new(0, 1, ""), new(1, 0, "", RowSpan: 2)]),
            _ => null,
        };
        if (grid == null)
        {
            // No partitioning Lapin knows: the panels side by side, as the file lists them by position.
            if (panels.Count is 0 or > 6) return null;
            var n = panels.Count;
            grid = ([.. Enumerable.Repeat(1.0, n)], [1], [.. Enumerable.Range(0, n).Select(i => new JoineryPanel(i, 0, ""))]);
        }
        var (cols, rows, cells) = grid.Value;
        string Default(JoineryPanel c) =>
            cells.Count == 1 ? "tilt-turn-left"
            : cols.Length > 1 && c.Col == 0 && c.Row == 0 ? "tilt-turn-left"
            : cols.Length > 1 && c.Col + c.ColSpan == cols.Length && c.Row == 0 && cells.Count(x => x.Row == 0) > 1 ? "side-right"
            : rows.Length > 1 && cols.Length == 1 && c.Row == 0 ? "tilt-turn-left"
            : "fixed";
        // Each panel the file describes goes to the cell at its position; the rest keep Lapin's default.
        var ops = cells.Select(Default).ToArray();
        var left = panels.ToList();
        IfcJoineryPanel? Take(Func<IfcJoineryPanel, bool> where) { var p = left.FirstOrDefault(where); if (p != null) left.Remove(p); return p; }
        for (var i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            var position = cells.Count == 1 ? null
                : cols.Length > 1 && rows.Length == 1 ? (c.Col == 0 ? "LEFT" : c.Col + c.ColSpan == cols.Length ? "RIGHT" : "MIDDLE")
                : cols.Length == 1 ? (c.Row == 0 ? "BOTTOM" : c.Row + c.RowSpan == rows.Length ? "TOP" : "MIDDLE")
                : c.ColSpan == cols.Length ? (c.Row == 0 ? "BOTTOM" : "TOP")
                : c.RowSpan == rows.Length ? (c.Col == 0 ? "LEFT" : "RIGHT")
                : c.Col == 0 ? "LEFT" : "RIGHT";
            // Lapin itself calls a panel across the whole width (or height) MIDDLE.
            var spans = cells.Count > 1 && (c.ColSpan == cols.Length && cols.Length > 1 || c.RowSpan == rows.Length && rows.Length > 1);
            var p = position == null ? Take(_ => true) : Take(x => x.Position == position) ?? (spans ? Take(x => x.Position == "MIDDLE") : null);
            if (p != null && Lapin(p.Operation) is { } op) ops[i] = op;
            else if (p is { Operation: "SLIDINGHORIZONTAL" }) ops[i] = position == "RIGHT" ? "sliding-right" : "sliding-left";
        }
        return s with { Columns = cols, Rows = rows, Panels = [.. cells.Select((c, i) => c with { Operation = ops[i] })] };
    }
}
