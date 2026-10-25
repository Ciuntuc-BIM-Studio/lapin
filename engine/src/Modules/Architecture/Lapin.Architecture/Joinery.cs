using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Architecture;

/// <summary>A panel of a door or window: the grid cell of its bottom-left corner, how many it spans, how it opens.</summary>
public sealed record JoineryPanel(int Col, int Row, string Operation, int ColSpan = 1, int RowSpan = 1, bool Glazed = false);

/// <summary>The frame (lining): across the wall, its face width, the mullions' and transoms', its centre across the wall (+ to the exterior).</summary>
public sealed record JoineryFrame(double Depth, double Width, double Mullion, double Transom, double Offset);

/// <summary>A sash's profile, a door leaf's thickness, the glass.</summary>
public sealed record JoinerySash(double Width, double Depth, double LeafThickness, double GlassThickness);

public sealed record JoineryBoard(double Interior, double Exterior);

public sealed record JoineryColors(string Frame, string Glass, string Leaf);

/// <summary>
/// A door or window type as the configurator edits it (the old Lapin's
/// joinery spec, ported): the overall size (the opening it fills), the frame
/// and where it sits across the wall, a grid of columns and rows split by
/// mullions and transoms, and per cell (or span of cells) a panel with its
/// operation. Everything else (the IFC type's terms, the 3D body, the plan
/// symbol, the schedule) comes from this record.
/// <para>
/// The type's frame: X along the wall (centred on the opening), Y across it
/// (0 on the wall's centre plane, +Y to the exterior), Z up from the
/// opening's bottom. Left and right are as seen from the interior.
/// </para>
/// </summary>
public sealed record JoinerySpec(
    string Kind,
    string Name,
    string Mark,
    double Width,
    double Height,
    double SillHeight,
    JoineryFrame Frame,
    JoinerySash Sash,
    double[] Columns,
    double[] Rows,
    JoineryPanel[] Panels,
    JoineryBoard Board,
    double Threshold,
    JoineryColors Colors,
    bool IsExternal,
    double? ThermalTransmittance = null,
    /// <summary>The lintel over its openings: null, the wall's rule; mode "none", none.</summary>
    LintelRule? Lintel = null)
{
    public static JoinerySpec DefaultWindow(string name = "Window") => new(
        "window", name, "W", 1.2, 1.5, 0.9,
        new JoineryFrame(0.07, 0.06, 0.08, 0.08, 0),
        new JoinerySash(0.07, 0.08, 0.04, 0.024),
        [1, 1], [1],
        [new(0, 0, "tilt-turn-left"), new(1, 0, "side-right")],
        new JoineryBoard(0.2, 0.05), 0,
        new JoineryColors("#f2f2f2", "#9cc8e8", "#c8a27a"),
        true, 1.1);

    public static JoinerySpec DefaultDoor(string name = "Door") => new(
        "door", name, "D", 0.9, 2.1, 0,
        new JoineryFrame(0.1, 0.05, 0.06, 0.06, 0),
        new JoinerySash(0.1, 0.06, 0.04, 0.024),
        [1], [1],
        [new(0, 0, "swing-left")],
        new JoineryBoard(0, 0), 0.02,
        new JoineryColors("#e8e8e8", "#9cc8e8", "#c8a27a"),
        false);

    public bool IsDoor => Kind == "door";

    public JsonObject ToJson() => (JsonObject)JsonSerializer.SerializeToNode(this, Json.Options)!;

    /// <summary>A stored spec read back; null when there is none or it cannot be read.</summary>
    public static JoinerySpec? From(JsonNode? node)
    {
        try
        {
            var s = node?.Deserialize<JoinerySpec>(Json.Options);
            // A partial object binds its missing parts to null: as unreadable as bad JSON.
            if (s is null || s.Kind is null || s.Name is null || s.Mark is null || s.Frame is null || s.Sash is null
                || s.Columns is null || s.Rows is null || s.Panels is null || s.Board is null || s.Colors is null) return null;
            return s;
        }
        catch (JsonException) { return null; }
    }
}

/// <summary>A rectangle in the type frame's X (along) and Z (up).</summary>
public readonly record struct CellRect(double X0, double X1, double Z0, double Z1);

/// <summary>One box of the joinery body in the type frame, with its role (frame, sash, glass, leaf, board, handle).</summary>
public sealed record JoineryBox(string Role, Vec3 Min, Vec3 Max);

/// <summary>The configurator's rules: the grid, the panels, what IFC calls them, the body.</summary>
public static class Joinery
{
    public static readonly string[] WindowOperations =
    [
        "fixed", "side-left", "side-right", "tilt-turn-left", "tilt-turn-right", "bottom-hung", "top-hung",
        "pivot-horizontal", "pivot-vertical", "sliding-left", "sliding-right", "sliding-vertical",
    ];

    public static readonly string[] DoorOperations =
    [
        "swing-left", "swing-right", "double-acting-left", "double-acting-right",
        "door-sliding-left", "door-sliding-right", "folding-left", "folding-right",
    ];

    /// <summary>A door leaf (opaque unless glazed) rather than a glazed sash.</summary>
    public static bool IsDoorLeaf(string operation) => DoorOperations.Contains(operation);

    /// <summary>Hinged on a vertical side: the panel swings open in plan.</summary>
    public static bool SwingsInPlan(string operation) =>
        operation is "side-left" or "side-right" or "tilt-turn-left" or "tilt-turn-right" or "swing-left" or "swing-right" or "double-acting-left" or "double-acting-right";

    /// <summary>The clear opening inside the outer frame members (a door has a threshold, not a bottom member).</summary>
    public static CellRect InnerRect(JoinerySpec s)
    {
        var f = s.Frame.Width;
        var bottom = s.IsDoor ? Math.Max(0, s.Threshold) : f;
        return new CellRect(-s.Width / 2 + f, s.Width / 2 - f, bottom, s.Height - f);
    }

    static List<(double A, double B)> Splits(double[] weights, double from, double to, double bar)
    {
        var total = weights.Sum(w => Math.Max(w, 0));
        if (total == 0) total = 1;
        var usable = to - from - bar * (weights.Length - 1);
        var o = new List<(double, double)>();
        var at = from;
        for (var i = 0; i < weights.Length; i++)
        {
            var size = usable * Math.Max(weights[i], 0) / total;
            o.Add((at, at + size));
            at += size + (i < weights.Length - 1 ? bar : 0);
        }
        return o;
    }

    /// <summary>Column [A, B] and row [A, B] extents, mullions and transoms excluded.</summary>
    public static (List<(double A, double B)> Cols, List<(double A, double B)> Rows) Grid(JoinerySpec s)
    {
        var r = InnerRect(s);
        return (Splits(s.Columns, r.X0, r.X1, s.Frame.Mullion), Splits(s.Rows, r.Z0, r.Z1, s.Frame.Transom));
    }

    /// <summary>A panel's rectangle: its cells and the mullions or transoms between them.</summary>
    public static CellRect PanelRect(JoinerySpec s, JoineryPanel p)
    {
        var (cols, rows) = Grid(s);
        var c1 = Math.Min(cols.Count - 1, p.Col + p.ColSpan - 1);
        var r1 = Math.Min(rows.Count - 1, p.Row + p.RowSpan - 1);
        return new CellRect(cols[p.Col].A, cols[c1].B, rows[p.Row].A, rows[r1].B);
    }

    /// <summary>Every grid cell covered by exactly one panel: overlapping panels are dropped, cells no panel covers become fixed glazing.</summary>
    public static List<JoineryPanel> Normalised(JoinerySpec s)
    {
        int nc = s.Columns.Length, nr = s.Rows.Length;
        var taken = new bool[nc, nr];
        var o = new List<JoineryPanel>();
        foreach (var p in s.Panels)
        {
            if (p.Col < 0 || p.Row < 0 || p.Col >= nc || p.Row >= nr) continue;
            var cs = Math.Max(1, Math.Min(p.ColSpan, nc - p.Col));
            var rs = Math.Max(1, Math.Min(p.RowSpan, nr - p.Row));
            var free = true;
            for (var c = p.Col; c < p.Col + cs; c++) for (var r = p.Row; r < p.Row + rs; r++) if (taken[c, r]) free = false;
            if (!free) continue;
            for (var c = p.Col; c < p.Col + cs; c++) for (var r = p.Row; r < p.Row + rs; r++) taken[c, r] = true;
            o.Add(p with { ColSpan = cs, RowSpan = rs });
        }
        for (var c = 0; c < nc; c++)
            for (var r = 0; r < nr; r++)
                if (!taken[c, r]) o.Add(new JoineryPanel(c, r, "fixed"));
        return o;
    }

    /// <summary>Why the spec cannot be built (the first problem), or null.</summary>
    public static string? Problem(JoinerySpec s)
    {
        static bool Positive(double v) => double.IsFinite(v) && v > 0;
        if (!Positive(s.Width) || !Positive(s.Height)) return "Width and height must be positive.";
        if (!Positive(s.Frame.Depth) || !Positive(s.Frame.Width)) return "The frame needs a positive depth and width.";
        if (s.Columns.Length is < 1 or > 12 || s.Rows.Length is < 1 or > 12) return "Use 1 to 12 columns and rows.";
        if (!s.Columns.Concat(s.Rows).All(Positive)) return "Column widths and row heights must be positive.";
        var (cols, rows) = Grid(s);
        const double minCell = 0.05;
        if (cols.Any(c => c.B - c.A < minCell) || rows.Any(r => r.B - r.A < minCell)) return "The frame, mullions and transoms leave no room for the panels.";
        foreach (var p in Normalised(s))
        {
            var r = PanelRect(s, p);
            if (p.Operation != "fixed" && !IsDoorLeaf(p.Operation) && (r.X1 - r.X0 < 2 * s.Sash.Width + minCell || r.Z1 - r.Z0 < 2 * s.Sash.Width + minCell))
                return "A sash is too small for its profile: widen the panel or slim the sash.";
        }
        return null;
    }

    // ------------------------------------------------------------ IFC terms

    public static string WindowPanelOperation(string op) => op switch
    {
        "fixed" => "FIXEDCASEMENT",
        "side-left" => "SIDEHUNGLEFTHAND",
        "side-right" => "SIDEHUNGRIGHTHAND",
        "tilt-turn-left" => "TILTANDTURNLEFTHAND",
        "tilt-turn-right" => "TILTANDTURNRIGHTHAND",
        "bottom-hung" => "BOTTOMHUNG",
        "top-hung" => "TOPHUNG",
        "pivot-horizontal" => "PIVOTHORIZONTAL",
        "pivot-vertical" => "PIVOTVERTICAL",
        "sliding-left" or "sliding-right" => "SLIDINGHORIZONTAL",
        "sliding-vertical" => "SLIDINGVERTICAL",
        _ => "OTHEROPERATION",
    };

    public static string DoorPanelOperation(string op) => op switch
    {
        "swing-left" or "swing-right" => "SWINGING",
        "double-acting-left" or "double-acting-right" => "DOUBLE_ACTING",
        "door-sliding-left" or "door-sliding-right" => "SLIDING",
        "folding-left" or "folding-right" => "FOLDING",
        _ => "FIXEDPANEL",
    };

    public static string PanelPosition(JoinerySpec s, JoineryPanel p, string kind)
    {
        int nc = s.Columns.Length, nr = s.Rows.Length;
        var last = p.Col + p.ColSpan - 1;
        if (nc > 1)
        {
            if (p.Col == 0 && last < nc - 1) return "LEFT";
            if (last == nc - 1 && p.Col > 0) return "RIGHT";
            return "MIDDLE";
        }
        if (kind == "window" && nr > 1)
        {
            var top = p.Row + p.RowSpan - 1;
            if (p.Row == 0 && top < nr - 1) return "BOTTOM";
            if (top == nr - 1 && p.Row > 0) return "TOP";
        }
        return "MIDDLE";
    }

    public static (string Type, string? UserDefined) WindowPartitioning(JoinerySpec s)
    {
        var panels = Normalised(s);
        int nc = s.Columns.Length, nr = s.Rows.Length;
        var grid = $"{nc}x{nr} grid, {panels.Count} panels";
        if (panels.Count == 1) return ("SINGLE_PANEL", null);
        if (nr == 1) return nc switch { 2 => ("DOUBLE_PANEL_VERTICAL", null), 3 => ("TRIPLE_PANEL_VERTICAL", null), _ => ("USERDEFINED", grid) };
        if (nc == 1) return nr switch { 2 => ("DOUBLE_PANEL_HORIZONTAL", null), 3 => ("TRIPLE_PANEL_HORIZONTAL", null), _ => ("USERDEFINED", grid) };
        if (nc == 2 && nr == 2 && panels.Count == 3)
        {
            if (panels.FirstOrDefault(p => p.ColSpan == 2) is { } full) return (full.Row == 1 ? "TRIPLE_PANEL_TOP" : "TRIPLE_PANEL_BOTTOM", null);
            if (panels.FirstOrDefault(p => p.RowSpan == 2) is { } tall) return (tall.Col == 0 ? "TRIPLE_PANEL_LEFT" : "TRIPLE_PANEL_RIGHT", null);
        }
        return ("USERDEFINED", grid);
    }

    public static (string Type, string? UserDefined) DoorOperation(JoinerySpec s)
    {
        var all = Normalised(s);
        var ops = all.Where(p => IsDoorLeaf(p.Operation)).OrderBy(p => p.Col).Select(p => p.Operation).ToList();
        var fixedBeside = all.Any(p => p.Operation == "fixed" && p.RowSpan == s.Rows.Length);
        if (ops.Count == 1)
            switch (ops[0])
            {
                case "swing-left": return (fixedBeside ? "SWING_FIXED_LEFT" : "SINGLE_SWING_LEFT", null);
                case "swing-right": return (fixedBeside ? "SWING_FIXED_RIGHT" : "SINGLE_SWING_RIGHT", null);
                case "double-acting-left": return ("DOUBLE_SWING_LEFT", null);
                case "double-acting-right": return ("DOUBLE_SWING_RIGHT", null);
                case "door-sliding-left": return ("SLIDING_TO_LEFT", null);
                case "door-sliding-right": return ("SLIDING_TO_RIGHT", null);
                case "folding-left": return ("FOLDING_TO_LEFT", null);
                case "folding-right": return ("FOLDING_TO_RIGHT", null);
            }
        if (ops.Count == 2)
        {
            var (a, b) = (ops[0], ops[1]);
            if (a == "swing-left" && b == "swing-right") return ("DOUBLE_DOOR_SINGLE_SWING", null);
            if (a == "swing-left" && b == "swing-left") return ("DOUBLE_DOOR_SINGLE_SWING_OPPOSITE_LEFT", null);
            if (a == "swing-right" && b == "swing-right") return ("DOUBLE_DOOR_SINGLE_SWING_OPPOSITE_RIGHT", null);
            if (a == "double-acting-left" && b == "double-acting-right") return ("DOUBLE_DOOR_DOUBLE_SWING", null);
            if (a.StartsWith("door-sliding") && b.StartsWith("door-sliding")) return ("DOUBLE_DOOR_SLIDING", null);
            if (a.StartsWith("folding") && b.StartsWith("folding")) return ("DOUBLE_DOOR_FOLDING", null);
        }
        return ("USERDEFINED", ops.Count > 0 ? string.Join(" + ", ops) : "fixed");
    }

    /// <summary>The mullions' and transoms' positions as ratios of the width and height (IfcDoorLiningProperties, IfcWindowLiningProperties).</summary>
    public static (List<double> Mullions, List<double> Transoms) LiningOffsets(JoinerySpec s)
    {
        var (cols, rows) = Grid(s);
        return (cols.Take(cols.Count - 1).Select(c => (c.B + s.Frame.Mullion / 2 + s.Width / 2) / s.Width).ToList(),
                rows.Take(rows.Count - 1).Select(r => (r.B + s.Frame.Transom / 2) / s.Height).ToList());
    }

    // ------------------------------------------------------------ body

    static (double Width, double Depth, double Height, double Inset) Handle => (0.03, 0.05, 0.14, 0.03);

    /// <summary>Which side the handle goes: opposite the hinges; null for panels without one.</summary>
    public static string? HandleSide(string op) =>
        op.EndsWith("-left") ? "right" : op.EndsWith("-right") ? "left" : op == "bottom-hung" ? "top" : op == "top-hung" ? "bottom" : null;

    static IEnumerable<JoineryBox> Bars(JoinerySpec s, List<JoineryPanel> panels, double y0, double y1)
    {
        var (cols, rows) = Grid(s);
        bool SpansCol(int boundary, int row) => panels.Any(p => p.Col <= boundary && p.Col + p.ColSpan - 1 > boundary && p.Row <= row && p.Row + p.RowSpan - 1 >= row);
        bool SpansRow(int boundary, int col) => panels.Any(p => p.Row <= boundary && p.Row + p.RowSpan - 1 > boundary && p.Col <= col && p.Col + p.ColSpan - 1 >= col);
        JoineryBox Box(double x0, double x1, double z0, double z1) => new("frame", new Vec3(x0, y0, z0), new Vec3(x1, y1, z1));
        for (var i = 0; i < cols.Count - 1; i++)
            for (var r = 0; r < rows.Count; r++)
                if (!SpansCol(i, r)) yield return Box(cols[i].B, cols[i + 1].A, rows[r].A, rows[r].B);
        for (var j = 0; j < rows.Count - 1; j++)
            for (var c = 0; c < cols.Count; c++)
                if (!SpansRow(j, c)) yield return Box(cols[c].A, cols[c].B, rows[j].B, rows[j + 1].A);
        // Crossings: where any bar meets the square between four cells.
        for (var i = 0; i < cols.Count - 1; i++)
            for (var j = 0; j < rows.Count - 1; j++)
                if (!SpansCol(i, j) || !SpansCol(i, j + 1) || !SpansRow(j, i) || !SpansRow(j, i + 1))
                    yield return Box(cols[i].B, cols[i + 1].A, rows[j].B, rows[j + 1].A);
    }

    /// <summary>Four members of width <paramref name="w"/> around <paramref name="r"/>, the vertical ones full height.</summary>
    static IEnumerable<JoineryBox> Ring(CellRect r, double w, double y0, double y1, string role)
    {
        yield return new(role, new(r.X0, y0, r.Z0), new(r.X0 + w, y1, r.Z1));
        yield return new(role, new(r.X1 - w, y0, r.Z0), new(r.X1, y1, r.Z1));
        yield return new(role, new(r.X0 + w, y0, r.Z0), new(r.X1 - w, y1, r.Z0 + w));
        yield return new(role, new(r.X0 + w, y0, r.Z1 - w), new(r.X1 - w, y1, r.Z1));
    }

    static JoineryBox Glass(CellRect r, double inset, double y, double t) =>
        new("glass", new(r.X0 + inset, y - t / 2, r.Z0 + inset), new(r.X1 - inset, y + t / 2, r.Z1 - inset));

    static JoineryBox? HandleBox(CellRect r, string op, double interiorFace, bool door)
    {
        var side = HandleSide(op);
        if (side == null) return null;
        var (w, depth, h, inset0) = Handle;
        double y1 = interiorFace, y0 = interiorFace - depth, inset = inset0 + w / 2;
        if (side is "left" or "right")
        {
            var x = side == "left" ? r.X0 + inset : r.X1 - inset;
            var zc = door ? Math.Min(r.Z0 + 1.05, (r.Z0 + r.Z1) / 2 + 0.3) : (r.Z0 + r.Z1) / 2;
            return new("handle", new(x - w / 2, y0, zc - h / 2), new(x + w / 2, y1, zc + h / 2));
        }
        var z = side == "top" ? r.Z1 - inset : r.Z0 + inset;
        var xc = (r.X0 + r.X1) / 2;
        return new("handle", new(xc - h / 2, y0, z - w / 2), new(xc + h / 2, y1, z + w / 2));
    }

    /// <summary>
    /// The body as boxes in the type frame: the outer frame (a door's
    /// threshold instead of a bottom member), mullions and transoms that stop
    /// at panels spanning across them, per panel a sash with its glass or a
    /// door leaf, a handle opposite the hinges, the window boards.
    /// </summary>
    public static List<JoineryBox> Boxes(JoinerySpec s)
    {
        var f = s.Frame;
        double y0 = f.Offset - f.Depth / 2, y1 = f.Offset + f.Depth / 2;
        var outer = new CellRect(-s.Width / 2, s.Width / 2, 0, s.Height);
        var inner = InnerRect(s);
        var boxes = new List<JoineryBox>
        {
            new("frame", new(outer.X0, y0, 0), new(inner.X0, y1, s.Height)),
            new("frame", new(inner.X1, y0, 0), new(outer.X1, y1, s.Height)),
            new("frame", new(inner.X0, y0, inner.Z1), new(inner.X1, y1, s.Height)),
        };
        if (inner.Z0 > 1e-6) boxes.Add(new("frame", new(inner.X0, y0, 0), new(inner.X1, y1, inner.Z0)));
        var panels = Normalised(s);
        boxes.AddRange(Bars(s, panels, y0, y1));

        var sh = s.Sash;
        foreach (var p in panels)
        {
            var r = PanelRect(s, p);
            double sy0 = f.Offset - sh.Depth / 2, sy1 = f.Offset + sh.Depth / 2;
            if (IsDoorLeaf(p.Operation))
            {
                var t = sh.LeafThickness;
                double ly0 = f.Offset - t / 2, ly1 = f.Offset + t / 2;
                if (p.Glazed)
                {
                    boxes.AddRange(Ring(r, sh.Width, ly0, ly1, "leaf"));
                    boxes.Add(Glass(r, sh.Width, f.Offset, sh.GlassThickness));
                }
                else boxes.Add(new("leaf", new(r.X0, ly0, r.Z0), new(r.X1, ly1, r.Z1)));
                if (HandleBox(r, p.Operation, ly0, true) is { } h) boxes.Add(h);
            }
            else if (p.Operation == "fixed") boxes.Add(Glass(r, 0, f.Offset, sh.GlassThickness));
            else
            {
                boxes.AddRange(Ring(r, sh.Width, sy0, sy1, "sash"));
                boxes.Add(Glass(r, sh.Width, f.Offset, sh.GlassThickness));
                if (HandleBox(r, p.Operation, sy0, false) is { } h) boxes.Add(h);
            }
        }

        if (!s.IsDoor)
        {
            const double t = 0.025;
            if (s.Board.Interior > 0) boxes.Add(new("board", new(outer.X0, y0 - s.Board.Interior, 0), new(outer.X1, y0, t)));
            if (s.Board.Exterior > 0) boxes.Add(new("board", new(outer.X0, y1, 0), new(outer.X1, y1 + s.Board.Exterior, t)));
        }
        return boxes.Where(b => b.Max.X - b.Min.X > 1e-6 && b.Max.Y - b.Min.Y > 1e-6 && b.Max.Z - b.Min.Z > 1e-6).ToList();
    }
}
