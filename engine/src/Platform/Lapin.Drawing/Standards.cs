using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Lapin.Drawing;

/// <summary>A text style: paper height (mm), font, bold, italic, a colour (none: the layer's).</summary>
public sealed record TextStyle(string Id, string Name, double Height, string Font, bool Bold, bool Italic, string? Color = null);

/// <summary>A dimension style: its text style, arrow and size, extension-line gap and overshoot, where the value sits and its gap, precision and unit (paper mm).
/// <para>
/// <see cref="OpeningHeight"/> (report #92, decision 338): a segment that spans a door, a window or a bare wall opening
/// writes the opening's height under the dimension line, as Romanian and European drawings do (the width over it):
/// "off", "number" (150), "h" (h=150), "hp" (h=150 and, under it, the sill p=90 when there is one) or "slash" (150/90).
/// Off when a style does not say (older projects); a new project's Standard style says "number".
/// </para>
/// </summary>
public sealed record DimStyle(string Id, string Name, string TextStyle, string Arrow, double ArrowSize, double ExtGap, double ExtOver, string Placement, double TextGap, int Precision, string Unit, string? Color = null, string OpeningHeight = "off");

/// <summary>An axis (grid line) type: its label's text style, bubble and which ends carry it, size (paper mm), line type, colour and the label new axes start from.</summary>
public sealed record AxisStyle(string Id, string Name, string TextStyle, string Bubble, string Ends, double Size, string LineType, string FirstLabel, string? Color = null);

/// <summary>
/// A tag type: a text template whose {fields} read the element's parameters,
/// and the IFC classes it tags (none: any). Its shape: a box at a point with a
/// leader to the element ("box"), or a door's or window's marker in plan
/// ("opening"): a line across the wall through the opening's middle, reaching
/// <see cref="Reach"/> paper mm past both faces, the <see cref="Template"/>
/// at its exterior end and the <see cref="Inner"/> one at its interior end.
/// A template's "|" starts a new line; sizes ({width}, {height}, {sill}) are
/// written in <see cref="Unit"/>. A marker's texts run across its line (along
/// the wall, "across") or along it ("along"), <see cref="TextOrientation"/>,
/// always upright (decision 292).
/// A build-up tag ("buildup", docs/plan-buildup-tags.md) writes the element's layers: its <see cref="Header"/>, the
/// <see cref="Template"/> once per layer (its [optional parts] dropped when a field in them is empty), its
/// <see cref="Footer"/>; the layers exterior to interior (a wall) or top to bottom (a floor, a roof) as the element
/// stands, or the other way (<see cref="Order"/> "in-out"); layers of no thickness shown unless <see cref="ZeroLayers"/>
/// is false; one leader to the element, or one per layer into its own (<see cref="Leaders"/>); each line after a
/// <see cref="Bullet"/>, wrapped at <see cref="Width"/> paper mm. Each is null on other shapes (and then its default).
/// A tag's leader (decision 331), as ArchiCAD's labels: <see cref="LeaderShape"/> "straight" (from its box's side, bent
/// at a free elbow) or "elbow" (the default: an oblique segment to a knee, then a shoulder along the text, no box); the
/// shoulder underlines the text's first row, each, the last or none (<see cref="Underline"/>), reaching
/// <see cref="Extension"/> paper mm further out from the text towards the knee.
/// A box or a room's stamp may be drawn by a symbol of the user's instead (decision 552, <see cref="Symbol"/>: a symbol's id):
/// its lines as drawn, its texts' {fields} read from the tagged element as the template's are, at the tag's point.
/// </summary>
public sealed record TagType(string Id, string Name, string Template, IReadOnlyList<string> Classes,
    string Shape = "box", string Inner = "", string Unit = "cm", double Reach = 6, string TextStyle = Standards.Standard, string TextOrientation = "across",
    string? Header = null, string? Footer = null, string? Order = null, bool? ZeroLayers = null, string? Leaders = null, string? Bullet = null, double? Width = null,
    string? LeaderShape = null, string? Underline = null, double? Extension = null, string? Symbol = null)
{
    public bool Equals(TagType? o) => o is not null && Id == o.Id && Name == o.Name && Template == o.Template && Classes.SequenceEqual(o.Classes)
        && Shape == o.Shape && Inner == o.Inner && Unit == o.Unit && Reach == o.Reach && TextStyle == o.TextStyle && TextOrientation == o.TextOrientation
        && Header == o.Header && Footer == o.Footer && Order == o.Order && ZeroLayers == o.ZeroLayers && Leaders == o.Leaders && Bullet == o.Bullet && Width == o.Width
        && LeaderShape == o.LeaderShape && Underline == o.Underline && Extension == o.Extension && Symbol == o.Symbol;
    public override int GetHashCode() => HashCode.Combine(Id, Name, Template, Shape, Inner, Header, Footer, Order);

    /// <summary>A build-up tag's layers the other way: interior to exterior, bottom to top.</summary>
    [JsonIgnore] public bool Reversed => Order == "in-out";
    /// <summary>Whether a build-up tag writes the layers of no thickness (a membrane, paint; decision 291).</summary>
    [JsonIgnore] public bool ShowsZeroLayers => ZeroLayers != false;
    /// <summary>Whether a build-up tag draws a leader per layer, each into its own layer.</summary>
    [JsonIgnore] public bool LeaderPerLayer => Leaders == "per-layer";
    /// <summary>What a build-up tag's lines start with: a bullet, a dash, a number, nothing.</summary>
    [JsonIgnore] public string BulletOrDefault => Bullet ?? "bullet";
    /// <summary>The paper width (mm) a build-up tag's lines wrap at.</summary>
    [JsonIgnore] public double WidthOrDefault => Width is > 0 ? Width.Value : Standards.BuildupWidth;
    /// <summary>How the type's leaders run: an elbow under the text unless it says straight.</summary>
    [JsonIgnore] public string LeaderShapeOrDefault => LeaderShape ?? "elbow";
    /// <summary>What an elbow leader's shoulder underlines: the first row unless the type says each, the last or none.</summary>
    [JsonIgnore] public string UnderlineOrDefault => Underline ?? "first";
    /// <summary>How much further (paper mm) an elbow leader's shoulder reaches out from the text towards its knee.</summary>
    [JsonIgnore] public double ExtensionOrDefault => Extension is > 0 ? Extension.Value : 0;
}

/// <summary>Layers gathered under a group that shows and locks them together.</summary>
public sealed record LayerGroup(string Id, string Name, bool Visible, bool Locked);

/// <summary>A drafting layer: its colour, pen (paper mm) and line type, shown or locked by itself or its group.</summary>
/// <summary>
/// A line type of the project's own (docs/plan-user-extensibility.md §3.6): its name (what a layer, an axis style or an object
/// style names), its dashes on paper (mm: dash, gap, dash, gap…; a dot is a dash of 0.1 mm) and what it is. Read from an
/// AutoCAD <c>.lin</c> file or made in Lapin; Lapin's own (continuous, dashed, hidden, center, dotted, dashdot) stay.
/// </summary>
public sealed record LineTypeDef(string Id, string Name, double[] Dashes, string? Description = null);

public sealed record DraftLayer(string Id, string Name, string Color, bool Visible, bool Locked, string? Group = null, double? LineWeight = null, string? LineType = null);

/// <summary>
/// The project's drafting standards (the old Lapin's): text and dimension
/// styles, axis types and the default one, tag types, layers and layer
/// groups. Sizes are paper millimetres; a view turns them into model units
/// with its scale. Each list always has its Standard entry (layer "0").
/// An annotation names its styles in its params and may override any field
/// of them in <c>o.&lt;field&gt;</c>.
/// <see cref="SectionMarkSize"/>, <see cref="SectionMarkText"/>, <see cref="ElevationMarkSize"/> and
/// <see cref="ElevationMarkText"/> (decisions 455, 460): the section markers' and the elevation markers' symbol size and
/// text height (paper mm; null: <see cref="Standards.DefaultMarkSize"/> and the Standard text's height), which a marker's
/// own <c>markSize</c> and <c>markTextSize</c> override. Standards saved with one <c>markSize</c> and <c>markText</c>
/// (decision 455) give them to both kinds.
/// <see cref="AreaRounding"/> (report #76, decision 471): the step the areas of spaces are shown to (0.1, 0.5 or 1 m², ft² in
/// an imperial project; null: as measured), presentation only (<see cref="Lapin.Drawing.AreaRounding"/>).
/// <see cref="Scales"/> (forum #30, decision 492): the project's standard scales, the ones a view's scale picker offers
/// (null: <see cref="Standards.DefaultViewScales"/>); <see cref="PlanScale"/>, <see cref="SectionScale"/> and
/// <see cref="ElevationScale"/>: the scale a new view of that kind takes when none is asked for (null: 1:100).
/// </summary>
public sealed record StyleBook(
    IReadOnlyList<TextStyle> TextStyles, IReadOnlyList<DimStyle> DimStyles, IReadOnlyList<AxisStyle> AxisStyles,
    string DefaultAxisStyle, IReadOnlyList<TagType> TagTypes, IReadOnlyList<DraftLayer> Layers, IReadOnlyList<LayerGroup> LayerGroups,
    string LineCap = "round", string Units = "mm", double? SectionMarkSize = null, double? SectionMarkText = null, double? ElevationMarkSize = null, double? ElevationMarkText = null,
    double? AreaRounding = null, IReadOnlyList<double>? Scales = null, double? PlanScale = null, double? SectionScale = null, double? ElevationScale = null,
    IReadOnlyList<LineTypeDef>? LineTypes = null)
{
    public static StyleBook Default => Standards.Read(null);
}

public static class Standards
{
    public const string Standard = "standard";
    public static readonly TextStyle DefaultText = new(Standard, "Standard", 2.5, "sans", false, false);
    /// <summary>The Standard dimension style: in the project's units, with the unit's usual decimals (precision −1).</summary>
    public static readonly DimStyle DefaultDim = new(Standard, "Standard", Standard, "tick", 1.5, 1, 1.5, "above", 0.8, -1, "project");
    public static readonly AxisStyle DefaultAxis = new(Standard, "Standard", Standard, "circle", "both", 10, "dashdot", "1");
    public static readonly DraftLayer Layer0 = new("0", "0", "#18181b", true, false);

    /// <summary>A section's or elevation's marker symbol (paper mm): an elevation's circle across, a section's arrow 0.7 of it.</summary>
    public const double DefaultMarkSize = 10;
    /// <summary>What a marker's symbol size and text height may be (paper mm).</summary>
    public const double MinMarkSize = 1, MaxMarkSize = 50, MinMarkText = 0.5, MaxMarkText = 20;

    /// <summary>The scales a view's picker offers when the project names none of its own (decision 492).</summary>
    public static readonly double[] DefaultViewScales = [1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000];
    /// <summary>What a view's scale may be (1:1 to 1:10000, as a view's Scale field).</summary>
    public const double MinViewScale = 1, MaxViewScale = 10000;

    /// <summary>A scale within its range, or null.</summary>
    public static double? ViewScale(JsonNode? v) => Lapin.Model.Json.Number(v) is { } d && double.IsFinite(d) && d >= MinViewScale && d <= MaxViewScale ? d : null;

    /// <summary>A list of scales read tolerantly: those within range, sorted, each once; null when none is left.</summary>
    public static IReadOnlyList<double>? ViewScales(JsonNode? v) =>
        v is JsonArray a && a.Select(ViewScale).OfType<double>().Distinct().Order().ToList() is { Count: > 0 } list ? list : null;

    /// <summary>The scales the project offers: its own, else the usual ones.</summary>
    public static IReadOnlyList<double> ScalesOf(StyleBook b) => b.Scales ?? DefaultViewScales;

    /// <summary>The scale a new view of a kind takes when none is asked for (plan, section, elevation; else null).</summary>
    public static double? DefaultScaleOf(StyleBook b, string kind) => kind switch
    {
        "plan" => b.PlanScale, "section" => b.SectionScale, "elevation" => b.ElevationScale, _ => null,
    };

    /// <summary>A marker size or text height within its range, or null.</summary>
    public static double? MarkValue(JsonNode? v, double min, double max) => Lapin.Model.Json.Number(v) is { } d && double.IsFinite(d) && d >= min && d <= max ? d : null;

    public static readonly string[] Fonts = ["sans", "serif", "mono"];
    public static readonly string[] Arrows = ["tick", "arrow", "dot", "none"];
    public static readonly string[] Placements = ["above", "centered", "below"];
    /// <summary>How a dimension writes an opening's height under its line (decision 338): not at all, 150, h=150, h=150 with p=90 under it, 150/90.</summary>
    public static readonly string[] OpeningHeights = ["off", "number", "h", "hp", "slash"];
    /// <summary>What a new project's Standard dimension style writes under a segment across an opening.</summary>
    public const string NewProjectOpeningHeight = "number";
    /// <summary>A dimension's unit: metric, or feet and inches / inches (fractions of an inch).</summary>
    /// <summary>A dimension style's units: "project" is the project's, whatever they are.</summary>
    public static readonly string[] Units = ["project", "m", "cm", "mm", "ft-in", "in"];
    /// <summary>The unit the interface shows and takes lengths in (the project's).</summary>
    public static readonly string[] ProjectUnits = ["mm", "cm", "m", "ft-in", "in"];
    /// <summary>How a drawing's lines end and meet on screen and on paper: round, or square (projecting caps, mitred corners).</summary>
    public static readonly string[] LineCaps = ["round", "square"];
    public static readonly string[] Bubbles = ["circle", "square"];
    public static readonly string[] AxisEnds = ["both", "start", "end", "none"];
    public static readonly string[] LineTypes = ["continuous", "dashed", "dotted", "dashdot"];

    /// <summary>The fields an annotation may override, by kind (an axis overrides its line colour as lineColor).</summary>
    public static readonly string[] TextOverrides = ["height", "font", "bold", "italic", "color"];
    public static readonly string[] DimOverrides = ["arrow", "arrowSize", "placement", "textGap", "precision", "unit", "color", "openingHeight"];
    public static readonly string[] AxisOverrides = ["bubble", "ends", "size", "lineType"];

    static string Str(JsonNode? v, string fallback) => v is JsonValue j && j.TryGetValue<string>(out var s) && s.Length > 0 ? s : fallback;
    static double Num(JsonNode? v, double fallback) => Lapin.Model.Json.Number(v) is { } d && double.IsFinite(d) && d >= 0 ? d : fallback;
    static bool True(JsonNode? v) => v is JsonValue j && j.TryGetValue<bool>(out var b) && b;
    static string OneOf(JsonNode? v, string[] values, string fallback) => v is JsonValue j && j.TryGetValue<string>(out var s) && values.Contains(s) ? s : fallback;
    static string? Color(JsonNode? v) => v is JsonValue j && j.TryGetValue<string>(out var s) && Regex.IsMatch(s, "^#[0-9a-fA-F]{6}$") ? s : null;
    static double Positive(JsonNode? v, double fallback) => Num(v, fallback) is var n && n > 0 ? n : fallback;

    static TextStyle ReadText(JsonObject v)
    {
        var d = DefaultText;
        return new(Str(v["id"], d.Id), Str(v["name"], d.Name), Positive(v["height"], d.Height), OneOf(v["font"], Fonts, d.Font), True(v["bold"]), True(v["italic"]), Color(v["color"]));
    }

    static DimStyle ReadDim(JsonObject v)
    {
        var d = DefaultDim;
        return new(Str(v["id"], d.Id), Str(v["name"], d.Name), Str(v["textStyle"], d.TextStyle), OneOf(v["arrow"], Arrows, d.Arrow), Num(v["arrowSize"], d.ArrowSize),
            Num(v["extGap"], d.ExtGap), Num(v["extOver"], d.ExtOver), OneOf(v["placement"], Placements, d.Placement), Num(v["textGap"], d.TextGap),
            v["precision"] is JsonValue pv && Lapin.Model.Json.Number(pv) is { } pn && double.IsFinite(pn) ? Math.Max(-1, (int)Math.Round(pn)) : d.Precision, OneOf(v["unit"], Units, d.Unit), Color(v["color"]), OneOf(v["openingHeight"], OpeningHeights, d.OpeningHeight));
    }

    static AxisStyle ReadAxis(JsonObject v) => ReadAxis(v, LineTypes);

    static AxisStyle ReadAxis(JsonObject v, string[] lineTypes)
    {
        var d = DefaultAxis;
        var first = v["firstLabel"] is JsonValue j && j.TryGetValue<string>(out var fl) && fl.Trim().Length > 0 ? fl.Trim() : d.FirstLabel;
        return new(Str(v["id"], d.Id), Str(v["name"], d.Name), Str(v["textStyle"], d.TextStyle), OneOf(v["bubble"], Bubbles, d.Bubble), OneOf(v["ends"], AxisEnds, d.Ends),
            Positive(v["size"], d.Size), OneOf(v["lineType"], lineTypes, d.LineType), first, Color(v["color"]));
    }

    static TagType? ReadTag(JsonObject v)
    {
        if (v["id"] is not JsonValue idv || !idv.TryGetValue<string>(out var id) || id.Length == 0) return null;
        if (v["template"] is not JsonValue tv || !tv.TryGetValue<string>(out var template)) return null;
        var classes = v["classes"] is JsonArray arr
            ? arr.Select(c => c is JsonValue cv && cv.TryGetValue<string>(out var s) ? s.Trim() : "").Where(s => s.Length > 0).ToList()
            : v["classes"] is JsonValue cs && cs.TryGetValue<string>(out var text)
                ? text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : [];
        string? Text(string key) => v[key] is JsonValue x && x.TryGetValue<string>(out var t) && t.Length > 0 ? t : null;
        string? Choice(string key, string[] values) => v[key] is JsonValue x && x.TryGetValue<string>(out var t) && values.Contains(t) ? t : null;
        // A build-up's order, as a wall or as a floor says it: top to bottom is exterior to interior.
        var order = Choice("order", [.. BuildupOrders, "top-down", "bottom-up"]) switch { "top-down" => "out-in", "bottom-up" => "in-out", var o => o };
        return new TagType(id, Str(v["name"], "Tag"), template, classes,
            OneOf(v["shape"], TagShapes, "box"), v["inner"] is JsonValue iv && iv.TryGetValue<string>(out var inner) ? inner : "",
            OneOf(v["unit"], TagUnits, "cm"), Positive(v["reach"], 6), Str(v["textStyle"], Standard), OneOf(v["textOrientation"], TagOrientations, "across"),
            Text("header"), Text("footer"), order, v["zeroLayers"] is JsonValue zv && zv.TryGetValue<bool>(out var zero) ? zero : null,
            Choice("leaders", BuildupLeaders), Choice("bullet", BuildupBullets),
            Lapin.Model.Json.Number(v["width"]) is { } w && double.IsFinite(w) && w > 0 ? Math.Clamp(w, 10, 1000) : null,
            Choice("leaderShape", LeaderShapes), Choice("underline", Underlines),
            Lapin.Model.Json.Number(v["extension"]) is { } ext && double.IsFinite(ext) && ext >= 0 ? Math.Min(ext, 100) : null,
            Text("symbol"));
    }

    /// <summary>A tag's leader (decision 331): straight from its box, or an elbow whose shoulder runs along the text.</summary>
    public static readonly string[] LeaderShapes = ["straight", "elbow"];
    /// <summary>What an elbow leader's shoulder underlines: the text's first row, each row, the last, or none (it stops at the text's side).</summary>
    public static readonly string[] Underlines = ["first", "each", "last", "none"];

    /// <summary>A tag's shape: a box with a leader, a door's or window's marker across its wall in plan, or a room's stamp.</summary>
    public static readonly string[] TagShapes = ["box", "opening", "room", "buildup"];
    /// <summary>A build-up tag's order: exterior to interior (a wall; top to bottom on a floor or a roof), or the other way.</summary>
    public static readonly string[] BuildupOrders = ["out-in", "in-out"];
    /// <summary>A build-up tag's leaders: one to the element, or one per layer.</summary>
    public static readonly string[] BuildupLeaders = ["one", "per-layer"];
    /// <summary>What a build-up tag's lines start with: a bullet (•), a dash (–), their number, nothing.</summary>
    public static readonly string[] BuildupBullets = ["bullet", "dash", "number", "none"];
    /// <summary>The paper width (mm) a build-up tag's lines wrap at by default.</summary>
    public const double BuildupWidth = 80;

    /// <summary>
    /// The build-up tag a project starts with (docs/plan-buildup-tags.md), after the tester's: EXTERIOR, a line per layer
    /// (the material's description, its λ when it has one, the layer's thickness in mm), INTERIOR.
    /// </summary>
    public static readonly TagType DefaultBuildupTag = new("buildup", "Build-up (exterior → interior)", "{description}[, λ = {λ} W/mK]: {thickness} mm",
        ["IfcWall", "IfcSlab", "IfcRoof", "IfcCovering", "LapinRoomFinish"], "buildup", Unit: "mm", Header: "EXTERIOR", Footer: "INTERIOR",
        Order: "out-in", ZeroLayers: true, Leaders: "one", Bullet: "bullet", Width: BuildupWidth);
    /// <summary>How a marker's texts lie to its line: across it (along the wall), or along it.</summary>
    public static readonly string[] TagOrientations = ["across", "along"];
    /// <summary>The unit a tag writes sizes in.</summary>
    public static readonly string[] TagUnits = ["cm", "mm", "m", "ft-in", "in", "project"];

    /// <summary>A room's stamp in plan (shape "room"): its name, number and net area, one line each; what a project without a room tag type uses.</summary>
    public static readonly TagType DefaultRoomTag = new("room", "Room stamp", "{name}|{number}|{area}", ["IfcSpace"], "room");

    /// <summary>
    /// The tag types a project starts with: a door's and a window's marker,
    /// the mark at the exterior end, width/height (and a window's sill
    /// height) at the interior end, in centimetres; a room's stamp; boxes for
    /// the fire and acoustic ratings and the thermal transmittance.
    /// </summary>
    public static readonly IReadOnlyList<TagType> DefaultTagTypes =
    [
        new("door", "Door marker", "{mark}", ["IfcDoor"], "opening", "{width}/{height}"),
        new("window", "Window marker", "{mark}", ["IfcWindow"], "opening", "{width}/{height}|sill {sill}"),
        DefaultRoomTag,
        // Boxes any element can take (report #64): a parameter read by its name, the user's own ("Fire rating") or a standard set's.
        new("fire", "Fire rating", "{FireRating}", []),
        new("acoustic", "Acoustic rating", "{AcousticRating}", []),
        new("thermal", "Thermal transmittance", "U = {ThermalTransmittance}", []),
        DefaultBuildupTag,
    ];

    /// <summary>
    /// A tag's type: the one it names, else the first that tags its element's class (a build-up tag only when named: a
    /// wall tagged by its mark stays so); null when none does.
    /// </summary>
    public static TagType? TagTypeOf(StyleBook b, string? id, string? ifcClass = null) =>
        (id != null ? b.TagTypes.FirstOrDefault(t => t.Id == id) : null)
        ?? (ifcClass != null && id == null ? b.TagTypes.FirstOrDefault(t => t.Classes.Contains(ifcClass) && t.Shape != "buildup") : null);

    static DraftLayer ReadLayer(JsonObject v) => ReadLayer(v, LineTypes);

    static DraftLayer ReadLayer(JsonObject v, string[] lineTypes) => new(
        Str(v["id"], "0"), Str(v["name"], Str(v["id"], "0")), Color(v["color"]) ?? Layer0.Color, v["visible"] is not JsonValue vv || !vv.TryGetValue<bool>(out var vis) || vis,
        True(v["locked"]), v["group"] is JsonValue g && g.TryGetValue<string>(out var gs) && gs.Length > 0 ? gs : null,
        Num(v["lineWeight"], -1) is var w && w > 0 ? w : null, OneOf(v["lineType"], lineTypes, "") is { Length: > 0 } lt ? lt : null);

    /// <summary>A line type of the project's: a name, dashes on paper (mm, two at least, each above 0 and under 100); null when it is not one.</summary>
    public static LineTypeDef? ReadLineType(JsonObject v)
    {
        var name = v["name"] is JsonValue nv && nv.TryGetValue<string>(out var n) ? n.Trim() : "";
        if (name.Length is 0 or > 64 || LineTypes.Contains(name, StringComparer.OrdinalIgnoreCase) || LineStyles.Patterns.Contains(name, StringComparer.OrdinalIgnoreCase)) return null;
        if (v["dashes"] is not JsonArray da) return null;
        var dashes = da.Select(x => Lapin.Model.Json.Number(x) ?? double.NaN).ToArray();
        if (dashes.Length < 2 || dashes.Length > 24 || dashes.Any(x => !(x > 0 && x < 100))) return null;
        return new(name, name, [.. dashes.Select(x => Math.Round(x, 4))], v["description"] is JsonValue dv && dv.TryGetValue<string>(out var ds) && ds.Trim().Length > 0 ? ds.Trim() : null);
    }

    /// <summary>Every line type name a layer, an axis or an object style may name: Lapin's and the project's.</summary>
    public static string[] LineTypeNames(StyleBook? b) => [.. LineTypes, .. LineStyles.Patterns.Where(p => !LineTypes.Contains(p)), .. (b?.LineTypes ?? []).Select(l => l.Name)];

    /// <summary>A line type's dashes on paper (mm): the project's of that name, else Lapin's; continuous or unknown: none.</summary>
    public static double[] DashOf(StyleBook? b, string? lineType) => Dashes.Of(lineType, b?.LineTypes);

    static List<T> WithStandard<T>(List<T> list, T standard, Func<T, string> id, string key = Standard) =>
        list.Any(s => id(s) == key) ? list : [standard, .. list];

    /// <summary>The standards from a project's JSON, tolerantly: a malformed field falls back to the Standard style's, an unreadable entry is dropped, the Standard entries are always there.</summary>
    public static StyleBook Read(JsonNode? raw)
    {
        var o = raw as JsonObject ?? [];
        IEnumerable<JsonObject> List(string key) => o[key] is JsonArray a ? a.OfType<JsonObject>() : [];
        // The project's own line types first: its layers and axes may name them.
        var own = List("lineTypes").Select(ReadLineType).OfType<LineTypeDef>().GroupBy(l => l.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).ToList();
        string[] names = [.. LineTypes, .. LineStyles.Patterns.Where(p => !LineTypes.Contains(p)), .. own.Select(l => l.Name)];
        var axes = WithStandard(List("axisStyles").Select(v => ReadAxis(v, names)).ToList(), DefaultAxis, a => a.Id);
        var def = o["defaultAxisStyle"] is JsonValue dv && dv.TryGetValue<string>(out var ds) && axes.Any(a => a.Id == ds) ? ds : Standard;
        return new StyleBook(
            WithStandard(List("textStyles").Select(ReadText).ToList(), DefaultText, t => t.Id),
            WithStandard(List("dimStyles").Select(ReadDim).ToList(), DefaultDim, d => d.Id),
            axes, def,
            o["tagTypes"] is JsonArray ? List("tagTypes").Select(ReadTag).OfType<TagType>().ToList() : [.. DefaultTagTypes],
            WithStandard(List("layers").Select(v => ReadLayer(v, names)).ToList(), Layer0, l => l.Id, "0"),
            List("layerGroups").Where(g => g["id"] is JsonValue).Select(g => new LayerGroup(Str(g["id"], ""), Str(g["name"], "Group"), g["visible"] is not JsonValue gv || !gv.TryGetValue<bool>(out var b) || b, True(g["locked"]))).Where(g => g.Id.Length > 0).ToList(),
            OneOf(o["lineCap"], LineCaps, "round"), OneOf(o["units"], ProjectUnits, "mm"),
            MarkValue(o["sectionMarkSize"] ?? o["markSize"], MinMarkSize, MaxMarkSize), MarkValue(o["sectionMarkText"] ?? o["markText"], MinMarkText, MaxMarkText),
            MarkValue(o["elevationMarkSize"] ?? o["markSize"], MinMarkSize, MaxMarkSize), MarkValue(o["elevationMarkText"] ?? o["markText"], MinMarkText, MaxMarkText),
            Lapin.Drawing.AreaRounding.Read(Lapin.Model.Json.Number(o["areaRounding"])),
            ViewScales(o["scales"]), ViewScale(o["planScale"]), ViewScale(o["sectionScale"]), ViewScale(o["elevationScale"]),
            own.Count > 0 ? own : null);
    }

    public static JsonObject ToJson(StyleBook b) => (JsonObject)JsonSerializer.SerializeToNode(b, DrawingJson.Default.StyleBook)!;

    // ------------------------------------------------------------ resolving

    public static TextStyle TextOf(StyleBook b, string? id) => b.TextStyles.FirstOrDefault(s => s.Id == id) ?? b.TextStyles.FirstOrDefault(s => s.Id == Standard) ?? DefaultText;
    public static DimStyle DimOf(StyleBook b, string? id) => b.DimStyles.FirstOrDefault(s => s.Id == id) ?? b.DimStyles.FirstOrDefault(s => s.Id == Standard) ?? DefaultDim;
    public static AxisStyle AxisOf(StyleBook b, string? id) => b.AxisStyles.FirstOrDefault(s => s.Id == id) ?? b.AxisStyles.FirstOrDefault(s => s.Id == Standard) ?? DefaultAxis;

    /// <summary>A record with the overrides in <paramref name="p"/> (<c>o.field</c>) applied, each only when its type fits.</summary>
    static T Overridden<T>(T style, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info, JsonObject p, string[] fields) where T : class
    {
        var o = (JsonObject)JsonSerializer.SerializeToNode(style, info)!;
        foreach (var f in fields)
        {
            if (p["o." + f] is not JsonValue v) continue;
            var cur = o[f] as JsonValue;
            var fits = cur == null
                || (cur.GetValueKind() == JsonValueKind.Number && v.GetValueKind() == JsonValueKind.Number)
                || (cur.GetValueKind() is JsonValueKind.True or JsonValueKind.False && v.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
                || v.GetValueKind() == JsonValueKind.String;
            if (fits) o[f] = v.DeepClone();
        }
        return o.Deserialize(info)!;
    }

    /// <summary>The text style an annotation is drawn with: its own (or its axis or dimension style's), then its overrides.</summary>
    public static TextStyle ResolveText(StyleBook b, JsonObject p)
    {
        var baseStyle = p["textStyle"] is JsonValue t ? TextOf(b, (string?)t)
            : p["axisStyle"] is JsonValue a ? TextOf(b, AxisOf(b, (string?)a).TextStyle)
            : TextOf(b, DimOf(b, (string?)p["dimStyle"]).TextStyle);
        return Overridden(baseStyle, DrawingJson.Default.TextStyle, p, TextOverrides);
    }

    /// <summary>A dimension's style with its overrides, "project" units made the project's.</summary>
    public static DimStyle ResolveDim(StyleBook b, JsonObject p) => Overridden(DimOf(b, (string?)p["dimStyle"]), DrawingJson.Default.DimStyle, p, DimOverrides) is var s && s.Unit == "project" ? s with { Unit = b.Units } : s;

    public static AxisStyle ResolveAxis(StyleBook b, JsonObject p)
    {
        var s = Overridden(AxisOf(b, (string?)p["axisStyle"]), DrawingJson.Default.AxisStyle, p, AxisOverrides);
        return p["o.lineColor"] is JsonValue c && c.TryGetValue<string>(out var color) ? s with { Color = color } : s;
    }

    /// <summary>The decimals a unit is usually written with: none in millimetres, one in centimetres, two in metres; 1/16" in imperial.</summary>
    public static int UsualPrecision(string unit) => unit switch { "mm" => 0, "cm" => 1, _ => 2 };

    /// <summary>A length (metres) in a dimension style's unit and precision (below 0: the unit's usual).</summary>
    public static string FormatDimension(double metres, string unit, int precision)
    {
        if (precision < 0) precision = UsualPrecision(unit);
        if (unit is "ft-in" or "in") return FormatImperial(metres, unit == "ft-in", precision);
        var factor = unit == "mm" ? 1000 : unit == "cm" ? 100 : 1;
        return (metres * factor).ToString("F" + Math.Clamp(precision, 0, 6), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Feet and inches (12'-3 1/2") or inches (147 1/2"), the inch in fractions
    /// to the nearest 1/4, 1/8, 1/16… (precision 0, 1, 2…; 2 is the usual 1/16).
    /// </summary>
    public static string FormatImperial(double metres, bool feet, int precision)
    {
        var denominator = 1 << Math.Clamp(precision + 2, 0, 6);
        var totalInches = Math.Abs(metres) / 0.0254;
        var sixteenths = (long)Math.Round(totalInches * denominator);
        var whole = sixteenths / denominator;
        var num = sixteenths % denominator;
        var den = denominator;
        while (num > 0 && num % 2 == 0) { num /= 2; den /= 2; }
        var sign = metres < 0 ? "-" : "";
        // Inches with their fraction: 3 1/2", 1/32", 0".
        string Inches(long inch) => inch == 0 && num > 0 ? $"{num}/{den}\"" : num > 0 ? $"{inch} {num}/{den}\"" : $"{inch}\"";
        if (!feet) return sign + Inches(whole);
        var ft = whole / 12;
        return ft > 0 ? $"{sign}{ft}'-{Inches(whole % 12)}" : sign + Inches(whole);
    }

    /// <summary>A level as drawings write it: ±0.00, +3.06, −3.98 (metres, two decimals, a true minus).</summary>
    public static string FormatLevel(double z)
    {
        var r = Math.Round(z, 2);
        if (Math.Abs(r) < 0.005) return "±0.00";
        return (r > 0 ? "+" : "\u2212") + Math.Abs(r).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Whether a unit is feet and inches or inches.</summary>
    public static bool IsImperial(string unit) => unit is "ft-in" or "in";

    /// <summary>
    /// A level in the project's units (report #68): metric projects write levels in metres as drawings do (±0.00, +3.06);
    /// imperial ones in feet and inches or inches, to 1/16" (±0", +9'-10 1/8", −1'-3 3/4").
    /// </summary>
    public static string FormatLevel(double z, string units)
    {
        if (!IsImperial(units)) return FormatLevel(z);
        var text = FormatImperial(Math.Abs(z), units == "ft-in", 2);
        return text is "0\"" ? "±0\"" : (z > 0 ? "+" : "\u2212") + text;
    }

    /// <summary>Text reads left to right, and bottom to top when it runs vertically: a screen angle (°, y down) folded into [−90°, 90°).</summary>
    public static double ReadableDeg(double deg)
    {
        var d = ((deg % 360) + 540) % 360 - 180;
        if (d >= 90 - 1e-6) d -= 180;
        else if (d < -90 - 1e-6) d += 180;
        return d + 0.0;
    }

    /// <summary>A line type's dashes on paper (mm); continuous: none.</summary>
    public static double[] DashOf(string? lineType) => Dashes.Of(lineType);

    /// <summary>Whether a layer shows: its own switch, its group's, then the view's and the sheet viewport's.</summary>
    public static bool LayerShows(DraftLayer layer, IEnumerable<LayerGroup> groups, IEnumerable<string>? viewHides = null, IEnumerable<string>? viewportHides = null)
    {
        if (!layer.Visible) return false;
        if (layer.Group != null && groups.FirstOrDefault(g => g.Id == layer.Group) is { Visible: false }) return false;
        if (viewHides?.Contains(layer.Id) == true) return false;
        return viewportHides?.Contains(layer.Id) != true;
    }

    public static bool LayerLocked(DraftLayer layer, IEnumerable<LayerGroup> groups) =>
        layer.Locked || (layer.Group != null && groups.FirstOrDefault(g => g.Id == layer.Group)?.Locked == true);
}

/// <summary>The drafting standards' JSON contracts, generated at build time (web names, nulls left out), so a trimmed engine writes them as the desktop does.</summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[System.Text.Json.Serialization.JsonSerializable(typeof(StyleBook))]
[System.Text.Json.Serialization.JsonSerializable(typeof(TextStyle))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DimStyle))]
[System.Text.Json.Serialization.JsonSerializable(typeof(AxisStyle))]
public partial class DrawingJson : System.Text.Json.Serialization.JsonSerializerContext;
