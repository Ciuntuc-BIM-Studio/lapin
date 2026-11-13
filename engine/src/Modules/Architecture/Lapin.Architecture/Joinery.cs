using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;

namespace Lapin.Architecture;

/// <summary>
/// A panel of a door or window: the grid cell of its bottom-left corner, how many it spans, how it opens, and what fills it
/// (decision 370): <see cref="Infill"/> "glass" or "solid" (an opaque panel), on a fixed pane, a sash or a door leaf alike;
/// null keeps the old rule (a door leaf solid unless <see cref="Glazed"/>, everything else glass).
/// </summary>
public sealed record JoineryPanel(int Col, int Row, string Operation, int ColSpan = 1, int RowSpan = 1, bool Glazed = false, string? Infill = null);

/// <summary>
/// A profile system's finer shapes (decision 370): the frame's rebate (the stop the sash closes on, standing <see cref="Rebate"/>
/// into the opening on the exterior half), the sash's lip lapping over the frame and mullions by <see cref="Overlap"/> on the
/// interior face, glazing beads <see cref="Bead"/> wide holding the glass, a drip under the bottom rail. <see cref="System"/>
/// names the preset it came from (pvc, aluminium, timber, steel). Null on a type: plain boxes, as before.
/// </summary>
public sealed record JoineryProfile(string System = "custom", double Rebate = 0.015, double Overlap = 0.008, double Bead = 0.018, bool Drip = true);

/// <summary>The frame (lining): across the wall, its face width, the mullions' and transoms', its centre across the wall (+ to the exterior).</summary>
public sealed record JoineryFrame(double Depth, double Width, double Mullion, double Transom, double Offset);

/// <summary>
/// How two leaves meet where no mullion stands between them (decision 466): <see cref="Kind"/> "butt" (square edges, a gap),
/// "rebated" (a rebated meeting stile: each leaf's edge stepped half its thickness, overlapping by <see cref="Width"/>, the
/// active leaf's lip on the room side, so the passive leaf cannot open before it), "astragal" (a strip <see cref="Width"/> wide
/// and <see cref="Thickness"/> thick on the active leaf's room face, covering the joint). <see cref="Active"/>: the leaf that
/// opens first, "left" or "right" as the operations name their hands (the left leaf is the one hinged on the left, "swing-left");
/// it has the handle, the passive one flush bolts at its top and bottom (<see cref="Bolts"/>). One setting for every pair of a
/// type. Double-acting leaves always meet butt.
/// </summary>
public sealed record JoineryMeeting(string Kind = "rebated", string Active = "right", double? Width = null, double? Thickness = null, bool Bolts = true)
{
    public static readonly string[] Kinds = ["butt", "rebated", "astragal"];
    public static readonly string[] Sides = ["left", "right"];

    /// <summary>A rebated meeting stile's overlap, and an astragal's face and thickness, when the type gives none.</summary>
    public const double DefaultRebate = 0.014, DefaultAstragalWidth = 0.045, DefaultAstragalThickness = 0.012;

    /// <summary>The overlap of a rebate, or an astragal's width.</summary>
    public double WidthOf => Width ?? (Kind == "astragal" ? DefaultAstragalWidth : DefaultRebate);

    public double ThicknessOf => Thickness ?? DefaultAstragalThickness;

    public JoineryMeeting Mirrored() => this with { Active = Active == "left" ? "right" : "left" };
}

/// <summary>Two leaves meeting at a column boundary with no mullion (<see cref="Joinery.Meetings"/>): which is which, and how they meet.</summary>
public sealed record JoineryPair(int Boundary, JoineryPanel Left, JoineryPanel Right, JoineryPanel Active, JoineryPanel Passive, string Kind);

/// <summary>A sash's profile, a door leaf's thickness, the glass.</summary>
public sealed record JoinerySash(double Width, double Depth, double LeafThickness, double GlassThickness);

/// <summary>
/// Where the frame sits across the wall (report #76), as a value from a reference: <see cref="From"/> "exterior" (the wall's
/// exterior finish face), "core" (the core's exterior face), "centre" (the wall's middle plane) or "interior" (the wall's
/// interior finish face, where a garage door is hung behind its opening, decision 530); <see cref="Distance"/> metres
/// inwards (from the interior face: outwards, into the wall; negative: proud). For the exterior faces it is the frame's outer
/// face that is that far back, from the interior face its inner face; from the centre, the frame's centre. A type without one
/// keeps <see cref="JoineryFrame.Offset"/>.
/// </summary>
public sealed record JoineryPosition(string From = "exterior", double Distance = 0)
{
    public static readonly string[] Froms = ["exterior", "core", "centre", "interior"];

    /// <summary>A position as a command or an element's <c>position</c> attribute gives it; null when it names none.</summary>
    public static JoineryPosition? From_(JsonNode? node)
    {
        if (node is not JsonObject o) return null;
        var from = (string?)o["from"] ?? "exterior";
        if (!Froms.Contains(from)) throw new CommandException($"„{from}” is not a reference for the frame's position ({string.Join(", ", Froms)}).");
        return new JoineryPosition(from, Json.Number(o["distance"]) ?? 0);
    }

    public JsonObject ToJson() => new() { ["from"] = From, ["distance"] = Distance };
}

public sealed record JoineryBoard(double Interior, double Exterior);

public sealed record JoineryColors(string Frame, string Glass, string Leaf);

/// <summary>
/// The materials of a door's or window's parts, by name (report #76: every window frame one dark grey metal, every glass one
/// blue glass, as a renderer swaps them by name). <see cref="Sash"/> and the glazing bars follow the frame unless given; a part
/// whose material is not set, or not in the project, keeps its colour (<see cref="JoineryColors"/>).
/// </summary>
public sealed record JoineryMaterials(string? Frame = null, string? Glass = null, string? Leaf = null, string? Hardware = null, string? Board = null, string? Sash = null)
{
    /// <summary>The material of a part of the body by its role (frame, sash, muntin, glass, leaf, board, handle, hardware).</summary>
    public string? Of(string role) => role switch
    {
        "frame" => Frame, "sash" or "muntin" => Sash ?? Frame, "glass" => Glass, "leaf" => Leaf, "board" => Board,
        "handle" or "hardware" => Hardware, _ => null,
    };

    /// <summary>The parts a type names materials for, as the configurator and Set for all… offer them.</summary>
    public static readonly string[] Parts = ["frame", "sash", "glass", "leaf", "board", "hardware"];

    public JoineryMaterials With(string part, string? name) => part switch
    {
        "frame" => this with { Frame = name }, "sash" => this with { Sash = name }, "glass" => this with { Glass = name }, "leaf" => this with { Leaf = name },
        "board" => this with { Board = name }, "hardware" => this with { Hardware = name },
        _ => throw new CommandException($"„{part}” is not a part of a door or window: {string.Join(", ", Parts)}."),
    };
}

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
    JoineryHardware? Hardware = null,
    /// <summary>The parts' materials by name; null: their colours only (a type made before report #76).</summary>
    JoineryMaterials? Materials = null,
    /// <summary>A window that projects from its wall (report #60): a bay, a bow or a box, its faces the grid's columns; null: flat in the wall.</summary>
    JoineryBay? Bay = null,
    /// <summary>The profile system's finer shapes (rebate, lapped sash, beads, drip; decision 370); null: plain boxes.</summary>
    JoineryProfile? Profile = null,
    /// <summary>The configurator's style last applied (modern, georgian…; reports #110–111), shown in its select; null: none applied.</summary>
    string? Style = null,
    /// <summary>
    /// The library profiles of the frame and the sash (a section's JSON, decision 417): straight members take their real outline, x
    /// towards the glass, y out of the building; their width and depth are then the profile's. They replace what they are the real
    /// shape of (the frame's stop and drip, the sash's lip and drip); beads stay, curved members a plain band. Null: plain.
    /// </summary>
    JsonObject? FrameSection = null,
    JsonObject? SashSection = null,
    /// <summary>The frame's place across the wall as a value from the exterior face, the core or the centre (report #76); null: <see cref="JoineryFrame.Offset"/>.</summary>
    JoineryPosition? Position = null,
    /// <summary>Frameless toughened glass, no frame or sash (tester's question 7): how it is held, the glass, the gaps; null: framed.</summary>
    JoineryFrameless? Frameless = null,
    /// <summary>
    /// Each column boundary's mullion, left to right (decision 466): null (or missing), <see cref="JoineryFrame.Mullion"/>; 0, none, the
    /// panels meet (a double door's leaves). <see cref="Transoms"/> the same for the row boundaries, bottom up. Null: the frame's everywhere.
    /// </summary>
    double?[]? Mullions = null,
    double?[]? Transoms = null,
    /// <summary>How two leaves meet where no mullion parts them (decision 466); null: butt, both leaves with their handles (as before).</summary>
    JoineryMeeting? Meeting = null,
    /// <summary>A garage door's settings (topic #48, decision 530), for a type whose panel is a garage door's operation; null: the defaults.</summary>
    JoineryGarage? Garage = null)
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

    /// <summary>
    /// The spec with the frame placed where its position says in <paramref name="wall"/>'s build-up (the element's own over the
    /// type's); the spec itself when there is none. <paramref name="flipSide"/>: the exterior is the wall's left.
    /// </summary>
    public JoinerySpec Positioned(IModelView m, Entity wall, JoineryPosition? own, bool flipSide)
    {
        var pos = own ?? Position;
        if (pos == null) return this;
        double offset;
        if (pos.From == "centre") offset = -pos.Distance;
        else
        {
            var bands = Walls.Section(m, wall).Where(b => b.To - b.From > 1e-9).ToList();
            if (bands.Count == 0) return this;
            var centre = (bands.Min(b => b.From) + bands.Max(b => b.To)) / 2;
            var sy = flipSide ? -1 : 1;
            double Y(double w) => sy * (centre - w);
            var face = bands.Max(b => Math.Max(Y(b.From), Y(b.To)));
            if (pos.From == "interior")
                return this with { Frame = Frame with { Offset = bands.Min(b => Math.Min(Y(b.From), Y(b.To))) + pos.Distance + (Frameless?.Glass ?? Frame.Depth) / 2 } };
            var reference = pos.From == "core" && bands.Where(b => b.Core).ToList() is { Count: > 0 } core ? core.Max(b => Math.Max(Y(b.From), Y(b.To))) : face;
            // Frameless glass (tester's question 7) is as deep as its glass.
            offset = reference - pos.Distance - (Frameless?.Glass ?? Frame.Depth) / 2;
        }
        return this with { Frame = Frame with { Offset = offset } };
    }

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
            // A profile's sizes are the member's, even after the profile was updated from its library.
            if (s.FrameSection is { } fs && Json.Number(fs["width"]) is { } fw && Json.Number(fs["depth"]) is { } fd) s = s with { Frame = s.Frame with { Width = fw, Depth = fd } };
            if (s.SashSection is { } ss && Json.Number(ss["width"]) is { } sw && Json.Number(ss["depth"]) is { } sd) s = s with { Sash = s.Sash with { Width = sw, Depth = sd } };
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
public sealed record JoineryBox(string Role, Vec3 Min, Vec3 Max, IReadOnlyList<Vec2>? Profile = null, IReadOnlyList<IReadOnlyList<Vec2>>? Holes = null)
{
    /// <summary>A member with a library profile's real outline through this box (decision 417): else the box itself.</summary>
    public JoineryMember? Member { get; init; }

    /// <summary>A part that moves with a door leaf (frameless glass, its fittings and handles): a plan draws the leaf open by its swing, not this closed.</summary>
    public bool Leaf { get; init; }

    /// <summary>A part let into the floor under the door (a floor spring's box and its cover plate, topic #28): elevations draw it dashed, plans its cover plate seen.</summary>
    public bool Underfloor { get; init; }
}

/// <summary>
/// A library profile's outline on a straight member: running up (<see cref="Upright"/>, a jamb, along Z) or along (a head or a sill, along
/// X), the profile's x towards the glass (<see cref="Inward"/>: +1 or −1 along the axis across the member), its y along type Y, out of the building.
/// </summary>
public sealed record JoineryMember(MemberOutline Outline, bool Upright, int Inward)
{
    /// <summary>A profile's point at the box's centre, in the plane across the member: X and Y on an upright one, Y and Z on one lying along.</summary>
    public (double Across, double Y) On(JoineryBox b, Vec2 q) => (Upright ? (b.Min.X + b.Max.X) / 2 + Inward * q.X : (b.Min.Z + b.Max.Z) / 2 + Inward * q.X, (b.Min.Y + b.Max.Y) / 2 + q.Y);
}

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
        // Garage doors (topic #48, decision 530): one panel filling the opening.
        "sectional-overhead", "roller", "up-and-over", "sectional-side-left", "sectional-side-right",
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

    /// <summary>What fills a panel: its own infill, else a door leaf is solid unless glazed and everything else glass.</summary>
    public static string InfillOf(JoineryPanel p) => p.Infill is "glass" or "solid" ? p.Infill : IsDoorLeaf(p.Operation) ? (p.Glazed ? "glass" : "solid") : "glass";

    public static readonly string[] Infills = ["glass", "solid"];

    /// <summary>
    /// The profile systems a type starts from (decision 370): frame, mullion, transom and sash sizes with their finer shapes, in
    /// metres. A type keeps the numbers, so each stays editable.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (JoineryFrame Frame, JoinerySash Sash, JoineryProfile Profile)> ProfileSystems =
        new Dictionary<string, (JoineryFrame, JoinerySash, JoineryProfile)>
        {
            ["pvc"] = (new(0.07, 0.06, 0.08, 0.08, 0), new(0.075, 0.07, 0.04, 0.024), new("pvc", 0.018, 0.008, 0.02, true)),
            ["aluminium"] = (new(0.065, 0.05, 0.06, 0.06, 0), new(0.06, 0.065, 0.04, 0.024), new("aluminium", 0.012, 0.006, 0.015, true)),
            ["timber"] = (new(0.078, 0.068, 0.09, 0.09, 0), new(0.078, 0.068, 0.044, 0.024), new("timber", 0.02, 0.012, 0.018, true)),
            ["steel"] = (new(0.06, 0.04, 0.05, 0.05, 0), new(0.045, 0.055, 0.04, 0.02), new("steel", 0.01, 0.005, 0.012, false)),
        };

    /// <summary>Hinged on a vertical side: the panel swings open in plan.</summary>
    public static bool SwingsInPlan(string operation) =>
        operation is "side-left" or "side-right" or "tilt-turn-left" or "tilt-turn-right" or "swing-left" or "swing-right" or "double-acting-left" or "double-acting-right";

    /// <summary>The clear opening inside the outer frame members (a door has a threshold, not a bottom member).</summary>
    public static CellRect InnerRect(JoinerySpec s)
    {
        if (s.Frameless != null) return Frameless.Inner(s);
        var f = s.Frame.Width;
        var bottom = s.IsDoor ? Math.Max(0, s.Threshold) : f;
        // A headed type's grid stops under the transom on the spring line, or without it reaches the head's inner apex (report #112);
        // a round window's is the square inside its ring.
        var top = JoineryShapes.ArchedPanes(s) ? JoineryShapes.Top(s, 0, f)
            : JoineryShapes.Headed(s) ? JoineryShapes.Spring(s) - Math.Max(s.Frame.Transom, 0.02) : s.Height - f;
        if (JoineryShapes.Round(s)) { var (a, b) = ((s.Width / 2 - f) / Math.Sqrt(2), (s.Height / 2 - f) / Math.Sqrt(2)); return new CellRect(-a, a, s.Height / 2 - b, s.Height / 2 + b); }
        return new CellRect(-s.Width / 2 + f, s.Width / 2 - f, bottom, top);
    }

    static List<(double A, double B)> Splits(double[] weights, double from, double to, double[] bars)
    {
        var total = weights.Sum(w => Math.Max(w, 0));
        if (total == 0) total = 1;
        var usable = to - from - bars.Sum();
        var o = new List<(double, double)>();
        var at = from;
        for (var i = 0; i < weights.Length; i++)
        {
            var size = usable * Math.Max(weights[i], 0) / total;
            o.Add((at, at + size));
            at += size + (i < weights.Length - 1 ? bars[i] : 0);
        }
        return o;
    }

    /// <summary>Column [A, B] and row [A, B] extents, mullions and transoms excluded.</summary>
    public static (List<(double A, double B)> Cols, List<(double A, double B)> Rows) Grid(JoinerySpec s)
    {
        var r = InnerRect(s);
        return (Splits(s.Columns, r.X0, r.X1, MullionWidths(s)), Splits(s.Rows, r.Z0, r.Z1, TransomWidths(s)));
    }

    /// <summary>What parts the grid's columns by default: the frame's mullion, or frameless glass's gap.</summary>
    public static double MullionOf(JoinerySpec s) => s.Frameless?.Gap ?? s.Frame.Mullion;

    /// <summary>What parts the grid's rows by default: the transom, or frameless glass's gap.</summary>
    public static double TransomOf(JoinerySpec s) => s.Frameless?.Gap ?? s.Frame.Transom;

    /// <summary>A boundary's own width from a per-boundary list (decision 466): its entry, else the default; frameless glass's gap whatever the list says.</summary>
    static double At(JoinerySpec s, double?[]? list, int i, double fallback) =>
        s.Frameless != null ? fallback : list is { } l && i < l.Length && l[i] is { } w && double.IsFinite(w) ? Math.Max(0, w) : fallback;

    /// <summary>Each column boundary's mullion, left to right: <see cref="JoinerySpec.Mullions"/>'s entry, else <see cref="MullionOf"/>; 0 where the panels meet.</summary>
    public static double[] MullionWidths(JoinerySpec s) => [.. Enumerable.Range(0, Math.Max(0, s.Columns.Length - 1)).Select(i => At(s, s.Mullions, i, MullionOf(s)))];

    /// <summary>Each row boundary's transom, bottom up, as <see cref="MullionWidths"/>.</summary>
    public static double[] TransomWidths(JoinerySpec s) => [.. Enumerable.Range(0, Math.Max(0, s.Rows.Length - 1)).Select(i => At(s, s.Transoms, i, TransomOf(s)))];

    /// <summary>The leaves of a pair: hinged on the left and on the right, so their free edges meet.</summary>
    static bool HingedLeft(string op) => op is "swing-left" or "double-acting-left";
    static bool HingedRight(string op) => op is "swing-right" or "double-acting-right";

    /// <summary>
    /// Where two swinging (or double-acting) leaves meet with no mullion between them (decision 466): the left one hinged on
    /// its left, the right one on its right, side by side over a column boundary whose mullion is 0. The kind is the type's
    /// <see cref="JoinerySpec.Meeting"/> (butt without one, and always for double-acting leaves); frameless glass has none.
    /// </summary>
    public static List<JoineryPair> Meetings(JoinerySpec s)
    {
        var o = new List<JoineryPair>();
        if (s.Frameless != null || !s.IsDoor) return o;
        var widths = MullionWidths(s);
        var panels = Normalised(s);
        for (var i = 0; i < widths.Length; i++)
        {
            if (widths[i] > 1e-9) continue;
            var l = panels.FirstOrDefault(p => p.Col + p.ColSpan - 1 == i && HingedLeft(p.Operation));
            var r = panels.FirstOrDefault(p => p.Col == i + 1 && HingedRight(p.Operation));
            if (l == null || r == null || l.Row + l.RowSpan <= r.Row || r.Row + r.RowSpan <= l.Row) continue;
            var m = s.Meeting;
            var kind = m == null || !l.Operation.StartsWith("swing") || !r.Operation.StartsWith("swing") ? "butt" : m.Kind;
            var leftActive = m?.Active == "left";
            o.Add(new JoineryPair(i, l, r, leftActive ? l : r, leftActive ? r : l, kind));
        }
        return o;
    }

    /// <summary>A panel's rectangle: its cells and the mullions or transoms between them.</summary>
    public static CellRect PanelRect(JoinerySpec s, JoineryPanel p)
    {
        var (cols, rows) = Grid(s);
        var c1 = Math.Min(cols.Count - 1, p.Col + p.ColSpan - 1);
        var r1 = Math.Min(rows.Count - 1, p.Row + p.RowSpan - 1);
        return new CellRect(cols[p.Col].A, cols[c1].B, rows[p.Row].A, rows[r1].B);
    }

    /// <summary>
    /// The type's mirror image (a door or window flipped on one axis, report #83): its columns in the other order, each panel
    /// in the column across from its own, and the other hand (swing-left ↔ swing-right, tilt-turn-left ↔ tilt-turn-right…).
    /// </summary>
    public static JoinerySpec Mirrored(JoinerySpec s)
    {
        var nc = s.Columns.Length;
        return s with
        {
            Columns = [.. s.Columns.Reverse()],
            Mullions = s.Mullions == null ? null : [.. Enumerable.Range(0, Math.Max(0, nc - 1)).Select(i => i < s.Mullions.Length ? s.Mullions[i] : null).Reverse()],
            Meeting = s.Meeting?.Mirrored(),
            Panels = [.. s.Panels.Select(p =>
            {
                var span = Math.Max(1, Math.Min(p.ColSpan, nc - p.Col));
                return p with { Col = p.Col >= 0 && p.Col < nc ? nc - p.Col - span : p.Col, ColSpan = span, Operation = IfcHands.MirrorText(p.Operation) };
            })],
        };
    }

    /// <summary>Whether a door or window is its type's mirror image: flipped on one axis, not both (two flips turn it round).</summary>
    public static bool IsMirrored(Entity fill) => fill.Bool("flipHand") != fill.Bool("flipSide");

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
        // A bay, bow or box window (report #60): its faces are flat windows of their own, each checked.
        if (s.Bay != null && s.IsDoor) return "A door does not project: only a window is a bay, a bow or a box.";
        if (BayWindows.Projecting(s)) return BayWindows.Problem(s);
        if (!Positive(s.Width) || !Positive(s.Height)) return "Width and height must be positive.";
        if (!Positive(s.Frame.Depth) || !Positive(s.Frame.Width)) return "The frame needs a positive depth and width.";
        if (s.Columns.Length is < 1 or > 12 || s.Rows.Length is < 1 or > 12) return "Use 1 to 12 columns and rows.";
        if (!s.Columns.Concat(s.Rows).All(Positive)) return "Column widths and row heights must be positive.";
        if ((s.Mullions ?? []).Concat(s.Transoms ?? []).Any(v => v is { } w && (!double.IsFinite(w) || w < 0 || w > 1)))
            return "Mullion and transom widths must be zero or positive (and under a metre).";
        if (s.Meeting is { } meet)
        {
            if (!JoineryMeeting.Kinds.Contains(meet.Kind)) return $"„{meet.Kind}” is not a meeting of two leaves ({string.Join(", ", JoineryMeeting.Kinds)}).";
            if (!JoineryMeeting.Sides.Contains(meet.Active)) return "The active leaf is the left or the right one.";
            if (meet.Width is { } mw && (!double.IsFinite(mw) || mw <= 0 || mw > 0.12)) return "A rebate's overlap or an astragal's width is over 0 and up to 120 mm.";
            if (meet.Thickness is { } mt && (!double.IsFinite(mt) || mt <= 0 || mt > 0.04)) return "An astragal is over 0 and up to 40 mm thick.";
        }
        if (s.Frameless != null) return Frameless.Problem(s);
        if (s.Panels.Any(p => GarageDoors.Is(p.Operation)))
            return !s.IsDoor ? "A garage door's operation is a door's." : GarageDoors.Problem(s);
        if (JoineryShapes.Round(s))
            return s.Width < 2 * s.Frame.Width + 0.1 || s.Height < 2 * s.Frame.Width + 0.1 ? "A round window needs room inside its frame." : null;
        if (JoineryShapes.Headed(s) && !JoineryShapes.ArchedPanes(s) && JoineryShapes.Spring(s) - Math.Max(s.Frame.Transom, 0.02) - InnerRect(s).Z0 < 0.1)
            return "The head leaves no room for the panels under it: make it taller or lower the rise.";
        var (cols, rows) = Grid(s);
        const double minCell = 0.05;
        // Panes that follow the head (report #112): the arch may not come down onto the top row's bottom beside a jamb or mullion.
        if (JoineryShapes.ArchedPanes(s) && cols.SelectMany(c => new[] { c.A, c.B }).Any(x => JoineryShapes.Top(s, x, s.Frame.Width) - rows[^1].A < 0.1))
            return "The head comes down onto the top row of panes: lower that row, add the transom at the spring line or lower the rise.";
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
        _ when GarageDoors.Is(op) => GarageDoors.PanelOperation(op),
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
        // A bay, bow or box window says so: IFC has no partitioning for a window that projects (report #60).
        if (BayWindows.Projecting(s)) return ("USERDEFINED", BayWindows.Describe(s) + $", {Normalised(BayWindows.Developed(s)).Count} panels");
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
                case var g when GarageDoors.Is(g): { var (op, user, _) = GarageDoors.Operation(g); return (op, user); }
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
        var (mw, tw) = (MullionWidths(s), TransomWidths(s));
        // Where the panels meet with no bar between them (decision 466) the lining has none; frameless glass's gaps all count.
        bool Present(double w) => s.Frameless != null || w > 1e-9;
        return (cols.Take(cols.Count - 1).Select((c, i) => (c, i)).Where(x => Present(mw[x.i])).Select(x => (x.c.B + mw[x.i] / 2 + s.Width / 2) / s.Width).ToList(),
                rows.Take(rows.Count - 1).Select((r, i) => (r, i)).Where(x => Present(tw[x.i])).Select(x => (x.r.B + tw[x.i] / 2) / s.Height).ToList());
    }

    /// <summary>The lining's one mullion and transom thickness (IFC has one each): the widest there is, else the frame's.</summary>
    public static (double Mullion, double Transom) LiningBars(JoinerySpec s) =>
        (MullionWidths(s).DefaultIfEmpty(0).Max() is var m && m > 1e-9 ? m : MullionOf(s), TransomWidths(s).DefaultIfEmpty(0).Max() is var t && t > 1e-9 ? t : TransomOf(s));

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
        var arched = JoineryShapes.ArchedPanes(s);
        for (var i = 0; i < cols.Count - 1; i++)
            for (var r = 0; r < rows.Count; r++)
                if (SpansCol(i, r)) continue;
                // A mullion in a top row that follows the head runs up to the arch (report #112).
                else if (arched && r == rows.Count - 1)
                {
                    var (x0, x1) = (cols[i].B, cols[i + 1].A);
                    yield return JoineryShapes.Prism("frame", [new(x0, rows[r].A), new(x1, rows[r].A), new(x1, JoineryShapes.Top(s, x1, s.Frame.Width)), new(x0, JoineryShapes.Top(s, x0, s.Frame.Width))], y0, y1);
                }
                else yield return Box(cols[i].B, cols[i + 1].A, rows[r].A, rows[r].B);
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

    /// <summary>A box with a library profile through it: one for each of its bodies (a plain box when it has none).</summary>
    static IEnumerable<JoineryBox> WithSection(JoineryBox box, JsonObject? section, bool upright, int inward)
    {
        var outlines = MemberOutline.Of(section);
        if (outlines.Count == 0) { yield return box; yield break; }
        foreach (var o in outlines) yield return box with { Member = new JoineryMember(o, upright, inward) };
    }

    /// <summary>Four straight members round <paramref name="r"/>, as <see cref="Ring"/> lays them, each through its profile.</summary>
    static IEnumerable<JoineryBox> SectionRing(CellRect r, double w, double y0, double y1, string role, JsonObject? section)
    {
        foreach (var b in WithSection(new(role, new(r.X0, y0, r.Z0), new(r.X0 + w, y1, r.Z1)), section, true, 1)) yield return b;
        foreach (var b in WithSection(new(role, new(r.X1 - w, y0, r.Z0), new(r.X1, y1, r.Z1)), section, true, -1)) yield return b;
        foreach (var b in WithSection(new(role, new(r.X0 + w, y0, r.Z0), new(r.X1 - w, y1, r.Z0 + w)), section, false, 1)) yield return b;
        foreach (var b in WithSection(new(role, new(r.X0 + w, y0, r.Z1 - w), new(r.X1 - w, y1, r.Z1)), section, false, -1)) yield return b;
    }

    /// <summary>A pane's rectangle taken <paramref name="d"/> in (negative: out). Bands of it are the sash's members, the frame's stop, the glazing beads.</summary>
    static CellRect Inset(CellRect r, double d) => new(r.X0 + d, r.X1 - d, r.Z0 + d, r.Z1 - d);

    public static readonly string[] HandleStyles = ["auto", "lever", "knob", "pull", "window", "none"];

    /// <summary>The handle's style for a type: its own, or a lever on a door and a window handle on a window.</summary>
    public static string HandleStyle(JoinerySpec s) => s.Hardware?.Handle is { } h && HandleStyles.Contains(h) && h != "auto" ? h : s.Frameless != null ? "pull" : s.IsDoor ? "lever" : "window";

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
    internal static IEnumerable<JoineryBox> HandleBoxes(JoinerySpec s, CellRect r, string op, double face, int outwards, bool door)
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
        // Frameless glass (tester's question 7): the panes and their fittings, and the window boards under them.
        if (s.Frameless != null) return [.. Frameless.Boxes(s), .. Boards(s, y0, y1, new CellRect(-s.Width / 2, s.Width / 2, 0, s.Height))];
        if (JoineryShapes.Round(s)) return JoineryShapes.RoundBoxes(s, y0, y1);
        // A garage door (topic #48): its frame, its curtain or panel, its tracks or roll box.
        if (GarageDoors.Of(s)) return GarageDoors.Boxes(s);
        var outer = new CellRect(-s.Width / 2, s.Width / 2, 0, s.Height);
        var inner = InnerRect(s);
        var headed = JoineryShapes.Headed(s);
        // The jambs run to the head's spring line, and the head is the arch over it; a rectangle's head is its top member.
        var jambTop = headed ? JoineryShapes.Spring(s) : s.Height;
        // The frame's straight members, through a library profile when it has one (decision 417); a head's curve keeps its band.
        var boxes = new List<JoineryBox>();
        boxes.AddRange(WithSection(new("frame", new(outer.X0, y0, 0), new(inner.X0, y1, jambTop)), s.FrameSection, true, 1));
        boxes.AddRange(WithSection(new("frame", new(inner.X1, y0, 0), new(outer.X1, y1, jambTop)), s.FrameSection, true, -1));
        if (headed) boxes.AddRange(JoineryShapes.HeadBoxes(s, y0, y1));
        else boxes.AddRange(WithSection(new("frame", new(inner.X0, y0, inner.Z1), new(inner.X1, y1, s.Height)), s.FrameSection, false, -1));
        if (inner.Z0 > 1e-6) boxes.AddRange(WithSection(new("frame", new(inner.X0, y0, 0), new(inner.X1, y1, inner.Z0)), s.FrameSection, false, 1));
        // What a library profile is the real shape of is not added over it (the system's beads stay: they are pieces of their own).
        bool frameStop = s.FrameSection == null, sashLip = s.SashSection == null;
        var panels = Normalised(s);
        boxes.AddRange(Bars(s, panels, y0, y1));

        var sh = s.Sash;
        var pr = s.Profile;
        var pairs = Meetings(s);
        foreach (var p in panels)
        {
            var r = PanelRect(s, p);
            // A leaf of a pair meeting with no mullion (decision 466): a rebated one's body stops half the overlap short of the meeting line.
            var pair = pairs.FirstOrDefault(m => m.Left == p || m.Right == p);
            var meeting = pair == null ? null : new LeafMeeting(pair, p, r, s.Meeting);
            if (meeting != null) r = meeting.Body;
            // A pane in the top row of a type without its spring-line transom follows the head (report #112): its members,
            // glass and beads are bands of its outline instead of rectangles.
            List<Vec2>? Out(double inset) => JoineryShapes.PaneOutline(s, r, inset);
            // A band between two insets of the pane (a sash's members, the frame's stop, beads), from ya to yb.
            IEnumerable<JoineryBox> Band(double outer, double inner, double ya, double yb, string role) =>
                inner - outer <= 1e-6 || yb - ya <= 1e-6 ? []
                : Out(outer) is { } o && Out(inner) is { } i ? [JoineryShapes.Prism(role, o, ya, yb) with { Holes = [i] }]
                : Ring(Inset(r, outer), inner - outer, ya, yb, role);
            // A sheet filling the pane inside an inset (glass, an opaque panel, a solid leaf), centred on y.
            JoineryBox Sheet(string role, double inset, double y, double t) =>
                Out(inset) is { } o ? JoineryShapes.Prism(role, o, y - t / 2, y + t / 2) : new(role, new(r.X0 + inset, y - t / 2, r.Z0 + inset), new(r.X1 - inset, y + t / 2, r.Z1 - inset));
            var solid = InfillOf(p) == "solid";
            double sy0 = f.Offset - sh.Depth / 2, sy1 = f.Offset + sh.Depth / 2;
            var infillT = solid ? Math.Max(sh.LeafThickness, sh.GlassThickness) : sh.GlassThickness;
            if (IsDoorLeaf(p.Operation))
            {
                var t = sh.LeafThickness;
                double ly0 = f.Offset - t / 2, ly1 = f.Offset + t / 2;
                if (!solid)
                {
                    boxes.AddRange(Band(0, sh.Width, ly0, ly1, "leaf"));
                    boxes.Add(Sheet("glass", sh.Width, f.Offset, sh.GlassThickness));
                    if (pr != null) boxes.AddRange(Band(sh.Width, sh.Width + pr.Bead, f.Offset - sh.GlassThickness / 2 - 0.018, f.Offset - sh.GlassThickness / 2, "leaf"));
                }
                else boxes.Add(Sheet("leaf", 0, f.Offset, t));
                if (meeting != null) boxes.AddRange(meeting.Boxes(s, ly0, ly1));
                // A door has its handle on both faces; of a pair with a meeting setting, only the active leaf.
                if (meeting is not { HasHandle: false })
                {
                    var hr = meeting?.HandleRect ?? r;
                    boxes.AddRange(HandleBoxes(s, hr, p.Operation, ly0, -1, true));
                    boxes.AddRange(HandleBoxes(s, hr, p.Operation, ly1, +1, true));
                }
            }
            else if (p.Operation == "fixed")
            {
                // Glazed straight into the frame (a rebate holds it, beads inside), or an opaque panel.
                if (solid) boxes.Add(Sheet("leaf", 0, f.Offset, infillT));
                else
                {
                    boxes.Add(Sheet("glass", 0, f.Offset, sh.GlassThickness));
                    boxes.AddRange(Muntins(s, r, f.Offset, sh.Depth, Out(0)));
                }
                if (pr != null)
                {
                    if (frameStop) boxes.AddRange(Band(0, pr.Rebate, f.Offset + infillT / 2, y1, "frame"));
                    boxes.AddRange(Band(0, pr.Bead, f.Offset - infillT / 2 - 0.018, f.Offset - infillT / 2, "frame"));
                }
            }
            else
            {
                if (s.SashSection != null && Out(0) == null) boxes.AddRange(SectionRing(r, sh.Width, sy0, sy1, "sash", s.SashSection));
                else boxes.AddRange(Band(0, sh.Width, sy0, sy1, "sash"));
                if (solid) boxes.Add(Sheet("leaf", sh.Width, f.Offset, infillT));
                else
                {
                    boxes.Add(Sheet("glass", sh.Width, f.Offset, sh.GlassThickness));
                    boxes.AddRange(Muntins(s, Inset(r, sh.Width), f.Offset, sh.Depth, Out(sh.Width)));
                }
                if (pr != null)
                {
                    // The frame's stop on the exterior half, the sash's lip lapping over the frame inside, its beads, its drip.
                    if (frameStop) boxes.AddRange(Band(0, pr.Rebate, sy1, y1, "frame"));
                    if (sashLip && pr.Overlap > 0) boxes.AddRange(Band(-pr.Overlap, Math.Min(0.012, sh.Width / 3), sy0 - 0.006, sy0, "sash"));
                    boxes.AddRange(Band(sh.Width, sh.Width + pr.Bead, f.Offset - infillT / 2 - 0.018, f.Offset - infillT / 2, "sash"));
                    if (sashLip && pr.Drip && !s.IsDoor) boxes.Add(new("sash", new(r.X0 + sh.Width, sy1, r.Z0 + 0.005), new(r.X1 - sh.Width, sy1 + 0.012, r.Z0 + 0.02)));
                }
                boxes.AddRange(HandleBoxes(s, r, p.Operation, sy0 - (pr != null && sashLip && pr.Overlap > 0 ? 0.006 : 0), -1, false));
            }
        }
        // A drip on the frame's bottom member outside.
        if (frameStop && pr is { Drip: true } && !s.IsDoor && inner.Z0 > 1e-6) boxes.Add(new("frame", new(inner.X0, y1, inner.Z0 - 0.012), new(inner.X1, y1 + 0.015, inner.Z0)));

        boxes.AddRange(Boards(s, y0, y1, outer));
        return boxes.Where(b => b.Max.X - b.Min.X > 1e-6 && b.Max.Y - b.Min.Y > 1e-6 && b.Max.Z - b.Min.Z > 1e-6).ToList();
    }

    /// <summary>
    /// One leaf of a pair meeting with no mullion (decision 466), from its grid rectangle <see cref="Cell"/>: the rectangle its
    /// body fills (a rebated leaf's stops half the overlap short of the meeting line), the strips it adds (the rebate's lip on
    /// half its thickness, the active leaf's astragal on its room face, the passive leaf's flush bolts), where its handle goes.
    /// </summary>
    sealed record LeafMeeting(JoineryPair Pair, JoineryPanel Panel, CellRect Cell, JoineryMeeting? Setting)
    {
        public bool IsLeft => Pair.Left == Panel;
        public bool IsActive => Pair.Active == Panel;
        /// <summary>The meeting line's X, and the way into this leaf from it.</summary>
        public double X => IsLeft ? Cell.X1 : Cell.X0;
        int Into => IsLeft ? -1 : 1;
        double Half => Pair.Kind == "rebated" ? Setting!.WidthOf / 2 : 0;
        public CellRect Body => IsLeft ? Cell with { X1 = Cell.X1 - Half } : Cell with { X0 = Cell.X0 + Half };
        /// <summary>A meeting setting gives the handle to the active leaf only; without one both keep theirs (as before).</summary>
        public bool HasHandle => Setting == null || IsActive;
        /// <summary>Where the handle is set out from: clear of an astragal.</summary>
        public CellRect HandleRect => Pair.Kind == "astragal" && IsActive ? (IsLeft ? Body with { X1 = X - Setting!.WidthOf / 2 } : Body with { X0 = X + Setting!.WidthOf / 2 }) : Body;

        public IEnumerable<JoineryBox> Boxes(JoinerySpec s, double ly0, double ly1)
        {
            if (Setting == null) yield break;
            var mid = (ly0 + ly1) / 2;
            // A pane following an arched head (report #112): the strips stop under the arch.
            var top = JoineryShapes.PaneTop(s, Cell) is { } t ? Math.Min(t(X - Setting.WidthOf / 2), t(X + Setting.WidthOf / 2)) : Cell.Z1;
            if (Pair.Kind == "rebated")
            {
                // The lip across the meeting line: the active leaf's on the room side (it closes last, over the passive one), the passive's outside.
                var (y0, y1) = IsActive ? (ly0, mid) : (mid, ly1);
                yield return new("leaf", new(X - Half, y0, Cell.Z0), new(X + Half, y1, top));
            }
            else if (Pair.Kind == "astragal" && IsActive)
                yield return new("leaf", new(X - Setting.WidthOf / 2, ly0 - Setting.ThicknessOf, Cell.Z0), new(X + Setting.WidthOf / 2, ly0, top));
            if (!IsActive && Setting.Bolts)
            {
                // Flush bolts at the passive leaf's top and bottom, by its meeting edge on the room face, clear of a rebate's lip or an astragal.
                var edge = X + Into * (Pair.Kind == "astragal" ? Setting.WidthOf / 2 : Half);
                var (x0, x1) = (edge + Into * 0.012, edge + Into * 0.032);
                foreach (var (z0, z1) in new[] { (Cell.Z0 + 0.03, Cell.Z0 + 0.23), (top - 0.23, top - 0.03) })
                    yield return new("hardware", new(Math.Min(x0, x1), ly0 - 0.005, z0), new(Math.Max(x0, x1), ly0, z1));
            }
        }
    }

    /// <summary>A window's boards inside and outside, under the frame's depth <paramref name="y0"/>..<paramref name="y1"/>.</summary>
    static IEnumerable<JoineryBox> Boards(JoinerySpec s, double y0, double y1, CellRect outer)
    {
        if (s.IsDoor) yield break;
        const double t = 0.025;
        if (s.Board.Interior > 0) yield return new("board", new(outer.X0, y0 - s.Board.Interior, 0), new(outer.X1, y0, t));
        if (s.Board.Exterior > 0) yield return new("board", new(outer.X0, y1, 0), new(outer.X1, y1 + s.Board.Exterior, t));
    }

    /// <summary>The glazing bars of one pane (its glass's rectangle), each across the glass, standing proud of it both sides.</summary>
    static IEnumerable<JoineryBox> Muntins(JoinerySpec s, CellRect pane, double y, double depth, IReadOnlyList<Vec2>? outline = null)
    {
        if (s.Muntins is not { Pattern: not "none" } m) yield break;
        var d = Math.Max(s.Sash.GlassThickness + 0.01, depth * 0.5);
        var w = Math.Clamp(m.Width, 0.008, 0.1);
        foreach (var (a0, b0) in JoineryShapes.MuntinLines(m, pane))
        {
            // On a pane following the head (report #112), each bar stops at the arch.
            if ((outline == null ? (a0, b0) : JoineryShapes.ClipConvex(a0, b0, outline)) is not var (a, b)) continue;
            if (JoineryShapes.Bar("muntin", a, b, w, y - d / 2, y + d / 2) is { } bar) yield return bar;
        }
    }
}
