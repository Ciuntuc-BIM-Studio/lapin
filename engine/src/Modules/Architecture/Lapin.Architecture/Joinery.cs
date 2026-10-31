using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

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
/// How the opening is finished around the frame (asked 25.10.2026, pending
/// decision 127): the reveal's finish on the jambs and head, by mode: "wall"
/// (the wall's own face layers, each side its own, as thick as the layer up to
/// 25 mm), "material" (one named material, <see cref="Thickness"/>), "none".
/// </summary>
public sealed record JoineryReveal(string Mode = "wall", string? Material = null, double Thickness = 0.015);

/// <summary>A sill board: how far it projects past the wall face, its thickness, how far it runs past the jambs, its slope (degrees, the outside one), its material.</summary>
public sealed record JoinerySill(bool On = true, double Projection = 0.03, double Thickness = 0.02, double Overhang = 0.02, double Slope = 0, string? Material = null);

/// <summary>A surround on the facade: a band of <see cref="Width"/> around the opening, standing <see cref="Projection"/> proud, with or without its sill side.</summary>
public sealed record JoinerySurround(bool On = false, double Width = 0.08, double Projection = 0.02, bool Sill = true, string? Material = null);

/// <summary>A door's threshold across the wall, its height and material.</summary>
public sealed record JoineryThreshold(bool On = false, double Height = 0.02, string? Material = null);

/// <summary>The details of a door or window type; a null field keeps the default (the reveal from the wall, the boards as before, no surround, the frame's threshold, no dressings: shutters, grille, window box, canopy).</summary>
public sealed record JoineryDetails(JoineryReveal? Reveal = null, JoinerySill? SillInside = null, JoinerySill? SillOutside = null, JoinerySurround? Surround = null, JoineryThreshold? Threshold = null,
    JoineryShutters? Shutters = null, JoineryGrille? Grille = null, JoineryWindowBox? WindowBox = null, JoineryCanopy? Canopy = null,
    JoineryKeystone? Keystone = null, JoineryHead? Head = null, JoineryApron? Apron = null);

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
/// opening's bottom. The panels open to the interior (−Y); left and right are
/// as seen looking the way they open (IFC's rule, docs/spec-ifc.md): a
/// "swing-left" door has its hinges on the left of someone pushing it open.
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
    LintelRule? Lintel = null,
    /// <summary>The reveal's finish, the sills, a surround, a threshold; null: the defaults.</summary>
    JoineryDetails? Details = null,
    /// <summary>The outline (an arched head, a round window); null: a rectangle.</summary>
    JoineryShape? Shape = null,
    /// <summary>Glazing bars on the rectangular panes; null: none.</summary>
    JoineryMuntins? Muntins = null,
    /// <summary>The handles; null: a lever on doors, a window handle on windows.</summary>
    JoineryHardware? Hardware = null)
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

    public JsonObject ToJson() => (JsonObject)JsonSerializer.SerializeToNode(this, ArchitectureJson.Default.JoinerySpec)!;

    /// <summary>A stored spec read back; null when there is none or it cannot be read.</summary>
    public static JoinerySpec? From(JsonNode? node)
    {
        try
        {
            var s = node?.Deserialize(ArchitectureJson.Default.JoinerySpec);
            // A partial object binds its missing parts to null: as unreadable as bad JSON.
            if (s is null || s.Kind is null || s.Name is null || s.Mark is null || s.Frame is null || s.Sash is null
                || s.Columns is null || s.Rows is null || s.Panels is null || s.Board is null || s.Colors is null) return null;
            return s;
        }
        catch (JsonException) { return null; }
    }
}

/// <summary>
/// The handles of a type (report #48, decision 188): <see cref="Handle"/> "auto" (a lever on a door,
/// a window handle on a window), "lever", "knob", "pull" (a bar, both faces of a door), "window"
/// (a turn handle on a plate), "none"; <see cref="Height"/>: a door handle's over the leaf's bottom;
/// <see cref="Color"/>: null, the default dark metal.
/// </summary>
public sealed record JoineryHardware(string Handle = "auto", double Height = 1.05, string? Color = null);

/// <summary>A rectangle in the type frame's X (along) and Z (up).</summary>
public readonly record struct CellRect(double X0, double X1, double Z0, double Z1);

/// <summary>
/// One box of the joinery body in the type frame, with its role (frame, sash, glass, leaf, board,
/// handle, muntin); or, with a <see cref="Profile"/> (X along, Z up; and its <see cref="Holes"/>),
/// that outline through Min.Y..Max.Y, Min and Max its bounds (an arch, a bar on the slant).
/// </summary>
public sealed record JoineryBox(string Role, Vec3 Min, Vec3 Max, IReadOnlyList<Vec2>? Profile = null, IReadOnlyList<IReadOnlyList<Vec2>>? Holes = null);

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
    /// <summary>How the opening symbols are drawn: the makers' way (the apex at the handle, fixed panes crossed) or DIN 1356 (the lines meet at the hinges).</summary>
    public static readonly string[] Conventions = ["handle", "hinge"];

    /// <summary>The project's symbol settings (<c>joinerySymbols</c> on the project): the convention, and whether hinges and handles are drawn.</summary>
    public static (string Convention, bool Hardware) SymbolSettings(IModelView m)
    {
        var o = Spatial.Project(m)?.Attrs["joinerySymbols"] as JsonObject;
        var hardware = o?["hardware"] is JsonValue v && v.TryGetValue<bool>(out var h) && h;
        return ((string?)o?["convention"] == "hinge" ? "hinge" : "handle", hardware);
    }

    public static bool IsDoorLeaf(string operation) => DoorOperations.Contains(operation);

    /// <summary>Hinged on a vertical side: the panel swings open in plan.</summary>
    public static bool SwingsInPlan(string operation) =>
        operation is "side-left" or "side-right" or "tilt-turn-left" or "tilt-turn-right" or "swing-left" or "swing-right" or "double-acting-left" or "double-acting-right";

    /// <summary>The clear opening inside the outer frame members (a door has a threshold, not a bottom member).</summary>
    public static CellRect InnerRect(JoinerySpec s)
    {
        var f = s.Frame.Width;
        var bottom = s.IsDoor ? Math.Max(0, s.Threshold) : f;
        // A headed type's grid stops under the transom on the spring line; a round window's is the square inside its ring.
        var top = JoineryShapes.Headed(s) ? JoineryShapes.Spring(s) - Math.Max(s.Frame.Transom, 0.02) : s.Height - f;
        if (JoineryShapes.Round(s)) { var (a, b) = ((s.Width / 2 - f) / Math.Sqrt(2), (s.Height / 2 - f) / Math.Sqrt(2)); return new CellRect(-a, a, s.Height / 2 - b, s.Height / 2 + b); }
        return new CellRect(-s.Width / 2 + f, s.Width / 2 - f, bottom, top);
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
        if (JoineryShapes.Round(s))
            return s.Width < 2 * s.Frame.Width + 0.1 || s.Height < 2 * s.Frame.Width + 0.1 ? "A round window needs room inside its frame." : null;
        if (JoineryShapes.Headed(s) && JoineryShapes.Spring(s) - Math.Max(s.Frame.Transom, 0.02) - InnerRect(s).Z0 < 0.1)
            return "The head leaves no room for the panels under it: make it taller or lower the rise.";
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

    public static readonly string[] HandleStyles = ["auto", "lever", "knob", "pull", "window", "none"];

    /// <summary>The handle's style for a type: its own, or a lever on a door and a window handle on a window.</summary>
    public static string HandleStyle(JoinerySpec s) => s.Hardware?.Handle is { } h && HandleStyles.Contains(h) && h != "auto" ? h : s.IsDoor ? "lever" : "window";

    /// <summary>Where a panel's handle goes: its centre in X and Z, and whether it lies across (a top- or bottom-hung sash); null for panels without one.</summary>
    public static (double X, double Z, bool Across)? HandleAt(JoinerySpec s, CellRect r, string op, bool door)
    {
        var side = HandleSide(op);
        if (side == null) return null;
        var inset = Handle.Inset + Handle.Width / 2;
        if (side is "left" or "right")
        {
            var x = side == "left" ? r.X0 + inset : r.X1 - inset;
            var zc = door ? Math.Min(r.Z0 + Math.Clamp(s.Hardware?.Height ?? 1.05, 0.3, 2.2), (r.Z0 + r.Z1) / 2 + 0.3) : (r.Z0 + r.Z1) / 2;
            return (x, zc, false);
        }
        return ((r.X0 + r.X1) / 2, side == "top" ? r.Z1 - inset : r.Z0 + inset, true);
    }

    /// <summary>An octagon about (x, z), <paramref name="r"/> across the flats' circle, for round hardware.</summary>
    static List<Vec2> Octagon(double x, double z, double r) =>
        Enumerable.Range(0, 8).Select(i => new Vec2(x + r * Math.Cos(Math.PI * (i + 0.5) / 4), z + r * Math.Sin(Math.PI * (i + 0.5) / 4))).ToList();

    /// <summary>
    /// A panel's handle on one face (<paramref name="face"/>, the leaf's or sash's face; <paramref name="outwards"/>
    /// −1 into the room, +1 out): the grip ("handle", one box or prism) and what fixes it ("hardware": a rose, a
    /// plate, the bar's posts). A lever turns towards the panel's middle; a window handle hangs down.
    /// </summary>
    static IEnumerable<JoineryBox> HandleBoxes(JoinerySpec s, CellRect r, string op, double face, int outwards, bool door)
    {
        var style = HandleStyle(s);
        if (style == "none" || HandleAt(s, r, op, door) is not var (x, z, across)) yield break;
        double Y(double d) => face + outwards * d;
        JoineryBox Slab(string role, double x0, double x1, double d0, double d1, double z0, double z1) =>
            new(role, new(Math.Min(x0, x1), Math.Min(Y(d0), Y(d1)), Math.Min(z0, z1)), new(Math.Max(x0, x1), Math.Max(Y(d0), Y(d1)), Math.Max(z0, z1)));
        // Towards the panel's middle from the handle.
        var inward = x > (r.X0 + r.X1) / 2 ? -1 : 1;
        switch (style)
        {
            case "lever":
            {
                yield return JoineryShapes.Prism("hardware", Octagon(x, z, 0.026), Y(0), Y(0.008));
                yield return Slab("hardware", x - 0.009, x + 0.009, 0.008, 0.055, z - 0.009, z + 0.009);
                if (across) yield return Slab("handle", x - 0.065, x + 0.065, 0.04, 0.058, z - 0.009, z + 0.009);
                else yield return Slab("handle", x - 0.009 * inward, x + inward * 0.13, 0.04, 0.058, z - 0.01, z + 0.01);
                break;
            }
            case "knob":
                yield return JoineryShapes.Prism("hardware", Octagon(x, z, 0.028), Y(0), Y(0.008));
                yield return JoineryShapes.Prism("hardware", Octagon(x, z, 0.011), Y(0.008), Y(0.035));
                yield return JoineryShapes.Prism("handle", Octagon(x, z, 0.03), Y(0.035), Y(0.07));
                break;
            case "pull":
            {
                // A bar standing off the face on two posts: tall on a door, short on a window.
                var len = door ? 0.6 : 0.25;
                var (z0, z1) = across ? (z, z) : (z - len / 2, z + len / 2);
                var (x0, x1) = across ? (x - len / 2, x + len / 2) : (x, x);
                foreach (var (px, pz) in new[] { (x0 + (across ? 0.02 : 0), z0 + (across ? 0 : 0.02)), (x1 - (across ? 0.02 : 0), z1 - (across ? 0 : 0.02)) })
                    yield return Slab("hardware", px - 0.009, px + 0.009, 0, 0.06, pz - 0.009, pz + 0.009);
                yield return Slab("handle", x0 - 0.0125, x1 + 0.0125, 0.06, 0.085, z0 - 0.0125, z1 + 0.0125);
                break;
            }
            default:
            {
                // A window handle: a plate, and the grip hanging down from it (lying along the rail on a top- or bottom-hung sash).
                yield return Slab("hardware", x - 0.016, x + 0.016, 0, 0.012, z - 0.04, z + 0.04);
                yield return Slab("hardware", x - 0.009, x + 0.009, 0.012, 0.035, z - 0.009, z + 0.009);
                if (across) yield return Slab("handle", x - 0.06, x + 0.06, 0.03, 0.05, z - 0.01, z + 0.01);
                else yield return Slab("handle", x - 0.011, x + 0.011, 0.03, 0.05, z - 0.12, z + 0.01);
                break;
            }
        }
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
        if (JoineryShapes.Round(s)) return JoineryShapes.RoundBoxes(s, y0, y1);
        var outer = new CellRect(-s.Width / 2, s.Width / 2, 0, s.Height);
        var inner = InnerRect(s);
        var headed = JoineryShapes.Headed(s);
        // The jambs run to the head's spring line, and the head is the arch over it; a rectangle's head is its top member.
        var jambTop = headed ? JoineryShapes.Spring(s) : s.Height;
        var boxes = new List<JoineryBox>
        {
            new("frame", new(outer.X0, y0, 0), new(inner.X0, y1, jambTop)),
            new("frame", new(inner.X1, y0, 0), new(outer.X1, y1, jambTop)),
        };
        if (headed) boxes.AddRange(JoineryShapes.HeadBoxes(s, y0, y1));
        else boxes.Add(new("frame", new(inner.X0, y0, inner.Z1), new(inner.X1, y1, s.Height)));
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
                // A door has its handle on both faces.
                boxes.AddRange(HandleBoxes(s, r, p.Operation, ly0, -1, true));
                boxes.AddRange(HandleBoxes(s, r, p.Operation, ly1, +1, true));
            }
            else if (p.Operation == "fixed") { boxes.Add(Glass(r, 0, f.Offset, sh.GlassThickness)); boxes.AddRange(Muntins(s, r, f.Offset, sh.Depth)); }
            else
            {
                boxes.AddRange(Ring(r, sh.Width, sy0, sy1, "sash"));
                boxes.Add(Glass(r, sh.Width, f.Offset, sh.GlassThickness));
                boxes.AddRange(Muntins(s, new CellRect(r.X0 + sh.Width, r.X1 - sh.Width, r.Z0 + sh.Width, r.Z1 - sh.Width), f.Offset, sh.Depth));
                boxes.AddRange(HandleBoxes(s, r, p.Operation, sy0, -1, false));
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

    /// <summary>The glazing bars of one pane (its glass's rectangle), each across the glass, standing proud of it both sides.</summary>
    static IEnumerable<JoineryBox> Muntins(JoinerySpec s, CellRect pane, double y, double depth)
    {
        if (s.Muntins is not { Pattern: not "none" } m) yield break;
        var d = Math.Max(s.Sash.GlassThickness + 0.01, depth * 0.5);
        var w = Math.Clamp(m.Width, 0.008, 0.1);
        foreach (var (a, b) in JoineryShapes.MuntinLines(m, pane))
            if (JoineryShapes.Bar("muntin", a, b, w, y - d / 2, y + d / 2) is { } bar) yield return bar;
    }
}
