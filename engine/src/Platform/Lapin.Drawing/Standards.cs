using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Lapin.Geometry;

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
/// <para>
/// <see cref="Zeros"/> (forum #81): whether a value's trailing zeros are written (2.500, "keep") or dropped (2.5, and 3 for
/// 3.000, "suppress"), or as the project's standards say ("project", the default; <see cref="StyleBook.SuppressZeros"/>).
/// </para>
/// <para>
/// <see cref="AutoText"/> (forum #78): a value that does not fit between its witness lines is moved clear of its neighbours by
/// the engine (just outside its segment on the free side, else staggered above and below the line), with a leader once it
/// stands further than <see cref="TextLeader"/> paper mm from its place: "on", "off", or as the project's standards say
/// ("project", the default; <see cref="StyleBook.DimAutoText"/>; a null leader: <see cref="StyleBook.DimTextLeader"/>).
/// </para>
/// <para>
/// The extension lines and the dimension line, as AutoCAD's dimension styles (issue #66, forum #131): <see cref="ExtSuppress"/>
/// leaves out a string's first extension line, its last, or both ("first", "second", "both"; null: none, as before), which a
/// dimension's own <c>extSuppress</c> overrides; <see cref="ExtFixed"/> gives every extension line one length on paper (mm),
/// measured back from the dimension line towards what it measures, never nearer to it than <see cref="ExtGap"/> (null or 0:
/// from the gap, as before); <see cref="ExtWeight"/>, <see cref="ExtColor"/>, <see cref="ExtLineType"/> and
/// <see cref="DimLineWeight"/>, <see cref="DimLineColor"/>, <see cref="DimLineType"/> give the extension lines and the
/// dimension line (its arrows and ticks take its colour) their own pen (paper mm), colour and line type; null: as before (a
/// 0.13 mm pen, the style's colour, continuous).
/// </para>
/// <para>
/// Its values' colour (forum topic 173, decision 732): <see cref="TextColor"/>, the style's own text colour, else its text
/// style's colour, else the style's colour (<see cref="Standards.DimTextColor"/>); a dimension's own colour goes before all.
/// </para>
/// </summary>
public sealed record DimStyle(string Id, string Name, string TextStyle, string Arrow, double ArrowSize, double ExtGap, double ExtOver, string Placement, double TextGap, int Precision, string Unit, string? Color = null, string OpeningHeight = "off", string Zeros = "project",
    string AutoText = "project", double? TextLeader = null,
    string? ExtSuppress = null, double? ExtFixed = null, double? ExtWeight = null, string? ExtColor = null, string? ExtLineType = null,
    double? DimLineWeight = null, string? DimLineColor = null, string? DimLineType = null, string? TextColor = null, double? TickWeight = null)
{
    /// <summary>The pen a tick or a dot is drawn with (paper mm, audit 22.11.2026 §5.3): the style's own, else 0.25 mm as always.</summary>
    public const double DefaultTickWeight = 0.25;
    public double TickWeightOr() => TickWeight is > 0 and var w ? w : DefaultTickWeight;

    /// <summary>Whether the extension line at point <paramref name="index"/> of a string of <paramref name="count"/> points is left out: the first, the last, or both.</summary>
    public bool SuppressesExtension(int index, int count) => ExtSuppress switch
    {
        "both" => index == 0 || index == count - 1,
        "first" => index == 0,
        "second" => index == count - 1,
        _ => false,
    };

    /// <summary>
    /// An extension line from the measured point <paramref name="p"/> to its foot <paramref name="q"/> on the dimension line
    /// (<paramref name="dir"/>: from p towards q, unit; <paramref name="length"/>: from p to q), past it by the overshoot:
    /// from the gap, or <see cref="ExtFixed"/> back from the dimension line (never nearer p than the gap); null when the point
    /// stands within the gap of its line.
    /// </summary>
    public (Vec2 A, Vec2 B)? Extension(Vec2 p, Vec2 q, Vec2 dir, double length, double mm)
    {
        if (!(length > ExtGap * mm)) return null;
        var from = ExtFixed is > 0 and var f && length - f * mm > ExtGap * mm ? q - dir * (f * mm) : p + dir * (ExtGap * mm);
        return (from, q + dir * (ExtOver * mm));
    }

    /// <summary>The extension lines' pen (paper mm), the dimension line's: the style's own, else <paramref name="fallback"/>.</summary>
    public double ExtWeightOr(double fallback) => ExtWeight is > 0 and var w ? w : fallback;
    public double DimLineWeightOr(double fallback) => DimLineWeight is > 0 and var w ? w : fallback;

    /// <summary>Whether this (resolved) style drops its values' trailing zeros.</summary>
    [JsonIgnore] public bool SuppressesZeros => Zeros == "suppress";
    /// <summary>Whether this (resolved) style moves the values that do not fit clear of their neighbours.</summary>
    [JsonIgnore] public bool PlacesTexts => AutoText == "on";
}

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
/// its lines as drawn, its texts' {fields} read from the tagged element as the template's are, at the tag's point. A door's or
/// window's marker (issue #72) draws the symbol at its line's exterior end instead of its outer text, its texts reading the
/// door or window ({mark}, {width}, {height}, {sill} as the marker writes them, any field, formulas); without one it is as before.
/// A tag's text is built as a mark is (decision 737): a <see cref="Prefix"/> and a <see cref="Suffix"/> round its template (<see cref="Written"/>).
/// </summary>
public sealed record TagType(string Id, string Name, string Template, IReadOnlyList<string> Classes,
    string Shape = "box", string Inner = "", string Unit = "cm", double Reach = 6, string TextStyle = Standards.Standard, string TextOrientation = "across",
    string? Header = null, string? Footer = null, string? Order = null, bool? ZeroLayers = null, string? Leaders = null, string? Bullet = null, double? Width = null,
    string? LeaderShape = null, string? Underline = null, double? Extension = null, string? Symbol = null, string? Prefix = null, string? Suffix = null,
    string? Layout = null, string? Above = null, string? Below = null, string? Centre = null, double? LineLength = null, double? LineOffset = null,
    double? LineInside = null, double? BubbleSize = null, double? CentreOffset = null, double? InnerOffset = null)
{
    public bool Equals(TagType? o) => o is not null && Id == o.Id && Name == o.Name && Template == o.Template && Classes.SequenceEqual(o.Classes)
        && Shape == o.Shape && Inner == o.Inner && Unit == o.Unit && Reach == o.Reach && TextStyle == o.TextStyle && TextOrientation == o.TextOrientation
        && Header == o.Header && Footer == o.Footer && Order == o.Order && ZeroLayers == o.ZeroLayers && Leaders == o.Leaders && Bullet == o.Bullet && Width == o.Width
        && LeaderShape == o.LeaderShape && Underline == o.Underline && Extension == o.Extension && Symbol == o.Symbol && Prefix == o.Prefix && Suffix == o.Suffix
        && Layout == o.Layout && Above == o.Above && Below == o.Below && Centre == o.Centre && LineLength == o.LineLength && LineOffset == o.LineOffset
        && LineInside == o.LineInside && BubbleSize == o.BubbleSize && CentreOffset == o.CentreOffset && InnerOffset == o.InnerOffset;
    public override int GetHashCode() => HashCode.Combine(Id, Name, Template, Shape, Inner, Header, Footer, Order);

    /// <summary>
    /// What the tag writes (decision 737, tags built as marks are): its <see cref="Prefix"/>, its template, its <see cref="Suffix"/>;
    /// the prefix opens the first line, the suffix closes the last. A build-up tag's template is its line per layer: as it is.
    /// </summary>
    [JsonIgnore] public string Written => Shape == "buildup" ? Template : (Prefix ?? "") + Template + (Suffix ?? "");

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

    /// <summary>A marker drawn as a line out of the opening's middle to a bubble (forum topic 205, <see cref="Layout"/> "bubble").</summary>
    [JsonIgnore] public bool Bubbled => Shape == "opening" && Layout == "bubble";
    /// <summary>A bubble marker's line from its origin to the bubble, model metres (60 cm by default).</summary>
    [JsonIgnore] public double LineLengthOrDefault => LineLength is > 0 ? LineLength.Value : Standards.BubbleLine;
    /// <summary>A bubble marker's bubble across, model metres (30 cm by default; 0: no circle, the mark alone).</summary>
    [JsonIgnore] public double BubbleSizeOrDefault => BubbleSize is >= 0 ? BubbleSize.Value : Standards.BubbleSize;
}

/// <summary>
/// A tag rule (issue #72, forum topic 114): per class, a condition (a formula over the element's fields, as <c>{= …}</c> reads
/// them: <c>width &gt; 1.2</c>, <c>FireRating != ""</c>, <c>IsExternal</c>) and the tag type it gives when the condition holds.
/// The rules of a class are tried in their order and the first that holds wins, over the type a tag was made with; a tag whose
/// type was chosen in its properties keeps that one. No rule, or none that holds: the tag's own type, as before.
/// </summary>
public sealed record TagRule(string Id, string Class, string When, string TagType);

/// <summary>
/// A table style of the user's own (docs/plan-drafting-tables.md §6, stage 3): a drafting table saved in the drafting standards,
/// by its id and name: its <see cref="Table"/> is a table's JSON (its style, its columns' and rows' sizes, the header rows and the
/// cells' own styles; with <see cref="Cells"/> its texts too, a template a new table copies whole). Kept as given, read by the
/// drafting tables (<c>DraftingTables.ApplyStyle</c>); carried by <c>.lapinstandards</c> files with the rest.
/// </summary>
public sealed record TableStyleDef(string Id, string Name, System.Text.Json.Nodes.JsonObject Table, bool Cells = false);

/// <summary>Layers gathered under a group that shows and locks them together.</summary>
public sealed record LayerGroup(string Id, string Name, bool Visible, bool Locked);

/// <summary>
/// A line type of the project's own (docs/plan-user-extensibility.md §3.6): its name (what a layer, an axis style or an object
/// style names), its dashes on paper (mm: dash, gap, dash, gap…; a dot is a dash of 0.1 mm) and what it is. Read from an
/// AutoCAD <c>.lin</c> file or made in Lapin; Lapin's own (continuous, dashed, hidden, center, dotted, dashdot) stay.
/// </summary>
public sealed record LineTypeDef(string Id, string Name, double[] Dashes, string? Description = null);

/// <summary>
/// A drafting layer as the standards show it: one of the project's <c>draftLayers</c> (forum #168, decision 714: one list, keyed by
/// its name, so <see cref="Id"/> is its name), its colour, pen (paper mm) and line type, shown or locked by itself or its group.
/// </summary>
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
/// <see cref="SuppressZeros"/> (forum #81): dimensions drop their trailing zeros (2.5, not 2.500; 3, not 3.000) unless their
/// style says to keep them; off by default, as drawings were written before.
/// <see cref="LevelDecimals"/> and <see cref="LevelZeros"/> (follow-up to forum #81): how levels are written wherever a drawing
/// prints one (level marks on sections and elevations, spot levels, level dimensions, base and survey points, terrain levels):
/// their decimals (0–4, metres; 2 by default) and their trailing zeros ("project": as <see cref="SuppressZeros"/>, "keep", "suppress").
/// <see cref="DimAutoText"/> and <see cref="DimTextLeader"/> (forum #78): dimension values that do not fit between their witness
/// lines are moved clear of their neighbours, with a leader once moved further than that many paper mm, unless a dimension style
/// says otherwise; on by default (values that fit are drawn exactly as before).
/// <see cref="TagRules"/> (issue #72): the conditional tag rules, in their order (null: none, every tag its own type).
/// <see cref="TableStyles"/> (issue #71, stage 3): the drafting tables' styles of the user's own (null: none; the built-in
/// presets are not listed here).
/// </summary>
public sealed record StyleBook(
    IReadOnlyList<TextStyle> TextStyles, IReadOnlyList<DimStyle> DimStyles, IReadOnlyList<AxisStyle> AxisStyles,
    string DefaultAxisStyle, IReadOnlyList<TagType> TagTypes, IReadOnlyList<DraftLayer> Layers, IReadOnlyList<LayerGroup> LayerGroups,
    string LineCap = "round", string Units = "mm", double? SectionMarkSize = null, double? SectionMarkText = null, double? ElevationMarkSize = null, double? ElevationMarkText = null,
    double? AreaRounding = null, IReadOnlyList<double>? Scales = null, double? PlanScale = null, double? SectionScale = null, double? ElevationScale = null,
    IReadOnlyList<LineTypeDef>? LineTypes = null, bool SuppressZeros = false, int LevelDecimals = Standards.DefaultLevelDecimals, string LevelZeros = "project",
    bool DimAutoText = true, double DimTextLeader = Standards.DefaultTextLeader, IReadOnlyList<TagRule>? TagRules = null,
    IReadOnlyList<TableStyleDef>? TableStyles = null)
{
    public static StyleBook Default => Standards.Read(null);

    /// <summary>How detail callouts' boundaries and inset views' frames are drawn by default (issue #73; null: as Lapin draws them).</summary>
    public FrameLine? CalloutLine { get; init; }
    /// <inheritdoc cref="CalloutLine"/>
    public FrameLine? InsetFrame { get; init; }
    /// <summary>How title blocks, sheets, symbols and documents write a date (<see cref="DrawingDates"/>): one of <see cref="DrawingDates.Formats"/>; null: dd.MM.yyyy (documents: by their language).</summary>
    public string? DateFormat { get; init; }
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
    /// <summary>A dimension style's trailing zeros (forum #81): as the project, kept, or dropped.</summary>
    public static readonly string[] ZeroChoices = ["project", "keep", "suppress"];
    /// <summary>A dimension style's automatic text placement (forum #78): as the project, on, off.</summary>
    public static readonly string[] AutoTextChoices = ["project", "on", "off"];
    /// <summary>Which extension lines a dimension style or a dimension leaves out (issue #66): none, a string's first, its last, both.</summary>
    public static readonly string[] ExtSuppressChoices = ["none", "first", "second", "both"];
    /// <summary>The most a dimension style's pens (paper mm) and its fixed extension line length may be.</summary>
    public const double MaxDimPen = 5, MaxExtFixed = 100;
    /// <summary>An extension-line suppression as stored: first, second, both; null (none) for anything else.</summary>
    public static string? ExtSuppressOf(JsonNode? v) => v is JsonValue j && j.TryGetValue<string>(out var s) && s is "first" or "second" or "both" ? s : null;
    static double? Within(JsonNode? v, double max) => Lapin.Model.Json.Number(v) is { } d && double.IsFinite(d) && d > 0 && d <= max ? d : null;
    /// <summary>How far (paper mm) a value placed automatically may stand from its place before it takes a leader, by default; the most it may be set to.</summary>
    public const double DefaultTextLeader = 5, MaxTextLeader = 100;
    /// <summary>A leader distance (paper mm, 0 to <see cref="MaxTextLeader"/>), or null.</summary>
    public static double? TextLeaderOf(JsonNode? v) => Lapin.Model.Json.Number(v) is { } d && double.IsFinite(d) && d >= 0 && d <= MaxTextLeader ? d : null;
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
    public static readonly string[] LineTypes = ["continuous", "dashed", "dotted", "dashdot", "dashdotdot"];

    /// <summary>The fields an annotation may override, by kind (an axis overrides its line colour as lineColor).</summary>
    public static readonly string[] TextOverrides = ["height", "font", "bold", "italic", "color"];
    // The extension lines' and the dimension line's pens of issue #66, and the ticks' (audit 22.11.2026), may be overridden too.
    public static readonly string[] DimOverrides = ["arrow", "arrowSize", "placement", "textGap", "precision", "unit", "color", "openingHeight", "zeros", "autoText", "textLeader", "extSuppress", "extFixed",
        "extWeight", "extColor", "extLineType", "dimLineWeight", "dimLineColor", "dimLineType", "tickWeight"];
    public static readonly string[] AxisOverrides = ["bubble", "ends", "size", "lineType"];

    static string Str(JsonNode? v, string fallback) => v is JsonValue j && j.TryGetValue<string>(out var s) && s.Length > 0 ? s : fallback;
    static double Num(JsonNode? v, double fallback) => Lapin.Model.Json.Number(v) is { } d && double.IsFinite(d) && d >= 0 ? d : fallback;
    static bool True(JsonNode? v) => v is JsonValue j && j.TryGetValue<bool>(out var b) && b;
    static string OneOf(JsonNode? v, string[] values, string fallback) => v is JsonValue j && j.TryGetValue<string>(out var s) && values.Contains(s) ? s : fallback;
    static string? Color(JsonNode? v) => Lapin.Geometry.HexColor.Read(v, null);
    static double Positive(JsonNode? v, double fallback) => Num(v, fallback) is var n && n > 0 ? n : fallback;

    static TextStyle ReadText(JsonObject v)
    {
        var d = DefaultText;
        return new(Str(v["id"], d.Id), Str(v["name"], d.Name), Positive(v["height"], d.Height), OneOf(v["font"], Fonts, d.Font), True(v["bold"]), True(v["italic"]), Color(v["color"]));
    }

    static DimStyle ReadDim(JsonObject v) => ReadDim(v, LineTypes);

    static DimStyle ReadDim(JsonObject v, string[] lineTypes)
    {
        var d = DefaultDim;
        string? LineType(JsonNode? x) => OneOf(x, lineTypes, "") is { Length: > 0 } lt ? lt : null;
        return new(Str(v["id"], d.Id), Str(v["name"], d.Name), Str(v["textStyle"], d.TextStyle), OneOf(v["arrow"], Arrows, d.Arrow), Num(v["arrowSize"], d.ArrowSize),
            Num(v["extGap"], d.ExtGap), Num(v["extOver"], d.ExtOver), OneOf(v["placement"], Placements, d.Placement), Num(v["textGap"], d.TextGap),
            v["precision"] is JsonValue pv && Lapin.Model.Json.Number(pv) is { } pn && double.IsFinite(pn) ? Math.Max(-1, (int)Math.Round(pn)) : d.Precision, OneOf(v["unit"], Units, d.Unit), Color(v["color"]), OneOf(v["openingHeight"], OpeningHeights, d.OpeningHeight),
            OneOf(v["zeros"], ZeroChoices, d.Zeros), OneOf(v["autoText"], AutoTextChoices, d.AutoText), TextLeaderOf(v["textLeader"]),
            ExtSuppressOf(v["extSuppress"]), Within(v["extFixed"], MaxExtFixed), Within(v["extWeight"], MaxDimPen), Color(v["extColor"]), LineType(v["extLineType"]),
            Within(v["dimLineWeight"], MaxDimPen), Color(v["dimLineColor"]), LineType(v["dimLineType"]), Color(v["textColor"]), Within(v["tickWeight"], MaxDimPen));
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
            Text("symbol"), Text("prefix"), Text("suffix"),
            Choice("layout", MarkerLayouts) is "bubble" ? "bubble" : null, Text("above"), Text("below"), Text("centre"),
            Metres(v["lineLength"], 0.01, MaxMarkerReach), Metres(v["lineOffset"], -MaxMarkerReach, MaxMarkerReach), Metres(v["lineInside"], -MaxMarkerReach, MaxMarkerReach),
            Metres(v["bubbleSize"], 0, MaxMarkerReach), Metres(v["centreOffset"], -MaxMarkerReach, MaxMarkerReach), Metres(v["innerOffset"], -MaxMarkerReach, MaxMarkerReach));
    }

    /// <summary>A bubble marker's length (model metres) within its bounds, to the tenth of a millimetre; null when not a number.</summary>
    static double? Metres(JsonNode? v, double min, double max) =>
        Lapin.Model.Json.Number(v) is { } n && double.IsFinite(n) ? Math.Round(Math.Clamp(n, min, max), 4) : null;

    /// <summary>A tag rule as a file or a command gives it: a class, a condition and a tag type, each not empty; null when it is not one.</summary>
    static TagRule? ReadTagRule(JsonObject v)
    {
        string Text(string key) => v[key] is JsonValue x && x.TryGetValue<string>(out var t) ? t.Trim() : "";
        var (cls, when, type) = (Text("class"), Text("when"), Text("tagType"));
        if (cls.Length == 0 || when.Length is 0 or > 2000 || type.Length == 0) return null;
        var id = Text("id") is { Length: > 0 } given ? given : $"rule-{cls}-{type}";
        return new TagRule(id, cls, when, type);
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
    /// <summary>
    /// How a door's or window's marker is laid (forum topic 205): "line", across the wall past both faces with its texts at the
    /// ends (as every marker was), or "bubble", a line out of the opening's middle to a bubble holding the mark, texts over and
    /// under the line, at its origin and at its interior end.
    /// </summary>
    public static readonly string[] MarkerLayouts = ["line", "bubble"];
    /// <summary>A bubble marker's line and bubble by default (model metres): the Polish drawings' 60 cm and 30 cm.</summary>
    public const double BubbleLine = 0.6, BubbleSize = 0.3;
    /// <summary>How far a bubble marker's lengths and offsets reach at most (model metres).</summary>
    public const double MaxMarkerReach = 10;

    /// <summary>
    /// Ready-made door and window markers after national conventions (forum topic 205), a project adds from Drafting standards →
    /// Tags (standards.addTagPreset) and changes as its own; never in a new project's list, so a project starts as before.
    /// <list type="bullet">
    /// <item>Polish door: a line from the frame's middle 60 cm out to a 30 cm bubble with the mark, the clear width over the line
    /// and the clear height under it, the fire rating (when the door has one) at the frame's middle;</item>
    /// <item>Polish window: the same line, run inside to 10 cm past the wall's interior face, the wall opening's width over and
    /// height under it, the sill over the finished floor at the line's interior end;</item>
    /// <item>Czech door: the classic marker, its mark outside and the clear size in mm inside (800/1970).</item>
    /// </list>
    /// </summary>
    public static readonly IReadOnlyList<TagType> TagTypePresets =
    [
        new("door-pl", "Door marker (Polish: bubble, clear opening)", "{mark}", ["IfcDoor"], "opening", "", "cm", TextOrientation: "across",
            Layout: "bubble", Above: "{clearWidth}", Below: "{clearHeight}", Centre: "{FireRating}", LineLength: BubbleLine, LineOffset: 0, BubbleSize: BubbleSize, CentreOffset: 0),
        new("window-pl", "Window marker (Polish: bubble, wall opening, sill)", "{mark}", ["IfcWindow"], "opening", "{sillFinished}", "cm", TextOrientation: "across",
            Layout: "bubble", Above: "{openingWidth}", Below: "{openingHeight}", LineLength: BubbleLine, LineOffset: 0, LineInside: 0.1, BubbleSize: BubbleSize, InnerOffset: 0),
        new("door-cz", "Door marker (Czech: clear opening 800/1970)", "{mark}", ["IfcDoor"], "opening", "{clearWidth}/{clearHeight}", "mm"),
    ];

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

    /// <summary>A drafting layer as a file or a command gives it, tolerantly, its line type one of <paramref name="lineTypes"/> (else none: continuous).</summary>
    public static DraftLayer ReadLayer(JsonObject v, string[] lineTypes) => new(
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
    public static StyleBook Read(JsonNode? raw) => FrameLines.With(ReadBook(raw), raw as JsonObject) with { DateFormat = DrawingDates.Of(raw?["dateFormat"]) };

    static StyleBook ReadBook(JsonNode? raw)
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
            WithStandard(List("dimStyles").Select(v => ReadDim(v, names)).ToList(), DefaultDim, d => d.Id),
            axes, def,
            o["tagTypes"] is JsonArray ? List("tagTypes").Select(ReadTag).OfType<TagType>().ToList() : [.. DefaultTagTypes],
            WithStandard(List("layers").Select(v => ReadLayer(v, names)).ToList(), Layer0, l => l.Id, "0"),
            List("layerGroups").Where(g => g["id"] is JsonValue).Select(g => new LayerGroup(Str(g["id"], ""), Str(g["name"], "Group"), g["visible"] is not JsonValue gv || !gv.TryGetValue<bool>(out var b) || b, True(g["locked"]))).Where(g => g.Id.Length > 0).ToList(),
            OneOf(o["lineCap"], LineCaps, "round"), OneOf(o["units"], ProjectUnits, "mm"),
            MarkValue(o["sectionMarkSize"] ?? o["markSize"], MinMarkSize, MaxMarkSize), MarkValue(o["sectionMarkText"] ?? o["markText"], MinMarkText, MaxMarkText),
            MarkValue(o["elevationMarkSize"] ?? o["markSize"], MinMarkSize, MaxMarkSize), MarkValue(o["elevationMarkText"] ?? o["markText"], MinMarkText, MaxMarkText),
            Lapin.Drawing.AreaRounding.Read(Lapin.Model.Json.Number(o["areaRounding"])),
            ViewScales(o["scales"]), ViewScale(o["planScale"]), ViewScale(o["sectionScale"]), ViewScale(o["elevationScale"]),
            own.Count > 0 ? own : null, True(o["suppressZeros"]),
            LevelDecimalsOf(o["levelDecimals"]) ?? DefaultLevelDecimals, OneOf(o["levelZeros"], ZeroChoices, "project"),
            o["dimAutoText"] is not JsonValue av || !av.TryGetValue<bool>(out var autoText) || autoText, TextLeaderOf(o["dimTextLeader"]) ?? DefaultTextLeader,
            List("tagRules").Select(ReadTagRule).OfType<TagRule>().DistinctBy(r => r.Id).ToList() is { Count: > 0 } rules ? rules : null,
            List("tableStyles").Select(ReadTableStyle).OfType<TableStyleDef>().DistinctBy(t => t.Id).ToList() is { Count: > 0 } tables ? tables : null);
    }

    /// <summary>The longest table style's JSON kept (characters): a table of the drafting module's bounds.</summary>
    public const int MaxTableStyle = 2_000_000;

    /// <summary>A table style as a file or a command gives it: an id, a name and a table (its rows and columns); null when it is not one.</summary>
    static TableStyleDef? ReadTableStyle(JsonObject v)
    {
        string Text(string key) => v[key] is JsonValue x && x.TryGetValue<string>(out var t) ? t.Trim() : "";
        if (v["table"] is not JsonObject table || table["rows"] is not JsonArray { Count: > 0 } || table["cols"] is not JsonArray { Count: > 0 }) return null;
        var name = Text("name");
        if (name.Length is 0 or > 80 || table.ToJsonString().Length > MaxTableStyle) return null;
        var id = Text("id") is { Length: > 0 } given ? given : "table-" + name;
        return new TableStyleDef(id, name, (JsonObject)table.DeepClone(), True(v["cells"]));
    }

    public static JsonObject ToJson(StyleBook b) => (JsonObject)JsonSerializer.SerializeToNode(b, DrawingJson.Default.StyleBook)!;

    // ------------------------------------------------------------ resolving

    // A style by its id, else by its name as typed (forum topic 148: a name typed in a text's properties found nothing), else the standard one.
    public static TextStyle TextOf(StyleBook b, string? id) => b.TextStyles.FirstOrDefault(s => s.Id == id) ?? b.TextStyles.FirstOrDefault(s => Named(s.Name, id)) ?? b.TextStyles.FirstOrDefault(s => s.Id == Standard) ?? DefaultText;
    public static DimStyle DimOf(StyleBook b, string? id) => b.DimStyles.FirstOrDefault(s => s.Id == id) ?? b.DimStyles.FirstOrDefault(s => Named(s.Name, id)) ?? b.DimStyles.FirstOrDefault(s => s.Id == Standard) ?? DefaultDim;
    static bool Named(string name, string? typed) => typed is { Length: > 0 } t && string.Equals(name.Trim(), t.Trim(), StringComparison.OrdinalIgnoreCase);
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

    /// <summary>A dimension's style with its overrides, "project" units and trailing zeros made the project's.</summary>
    public static DimStyle ResolveDim(StyleBook b, JsonObject p)
    {
        var s = Overridden(DimOf(b, (string?)p["dimStyle"]), DrawingJson.Default.DimStyle, p, DimOverrides);
        if (s.Unit == "project") s = s with { Unit = b.Units };
        if (s.Zeros is not ("keep" or "suppress")) s = s with { Zeros = b.SuppressZeros ? "suppress" : "keep" };
        if (s.AutoText is not ("on" or "off")) s = s with { AutoText = b.DimAutoText ? "on" : "off" };
        if (s.TextLeader is not { } leader || !(leader >= 0)) s = s with { TextLeader = b.DimTextLeader };
        // A dimension's own suppression (issue #66) over its style's, "none" too; a fixed length of 0 or below: from the gap.
        if (s.ExtSuppress is not ("first" or "second" or "both")) s = s with { ExtSuppress = null };
        if (s.ExtFixed is not > 0) s = s with { ExtFixed = null };
        return s;
    }

    /// <summary>
    /// The colour a dimension's values are written in (forum topic 173, decision 732), with <paramref name="dim"/> and
    /// <paramref name="text"/> resolved from <paramref name="p"/>: the dimension's own colour when it has one (o.color), else its
    /// style's text colour, else its text style's colour, else its style's colour; null: black.
    /// </summary>
    public static string? DimTextColor(JsonObject p, DimStyle dim, TextStyle text) =>
        p["o.color"] is JsonValue ? dim.Color : dim.TextColor ?? text.Color ?? dim.Color;

    public static AxisStyle ResolveAxis(StyleBook b, JsonObject p)
    {
        var s = Overridden(AxisOf(b, (string?)p["axisStyle"]), DrawingJson.Default.AxisStyle, p, AxisOverrides);
        return p["o.lineColor"] is JsonValue c && c.TryGetValue<string>(out var color) ? s with { Color = color } : s;
    }

    /// <summary>The decimals a unit is usually written with: none in millimetres, one in centimetres, two in metres; 1/16" in imperial.</summary>
    public static int UsualPrecision(string unit) => unit switch { "mm" => 0, "cm" => 1, _ => 2 };

    /// <summary>
    /// A length (metres) in a dimension style's unit and precision (below 0: the unit's usual); with
    /// <paramref name="suppressZeros"/> (forum #81) its trailing zeros dropped, and the point with them (2.500 → 2.5, 3.000 → 3).
    /// Feet and inches carry fractions, which have no trailing zeros.
    /// </summary>
    public static string FormatDimension(double metres, string unit, int precision, bool suppressZeros = false)
    {
        if (precision < 0) precision = UsualPrecision(unit);
        if (unit is "ft-in" or "in") return FormatImperial(metres, unit == "ft-in", precision);
        var factor = unit == "mm" ? 1000 : unit == "cm" ? 100 : 1;
        var text = (metres * factor).ToString("F" + Math.Clamp(precision, 0, 6), CultureInfo.InvariantCulture);
        return suppressZeros ? TrimZeros(text) : text;
    }

    /// <summary>A number's trailing zeros after its point dropped, and the point when nothing is left after it (2.500 → 2.5, 3.000 → 3, 120 stays); "-0" is "0".</summary>
    public static string TrimZeros(string text)
    {
        if (!text.Contains('.')) return text;
        var t = text.TrimEnd('0').TrimEnd('.');
        return t is "-0" or "-" or "" ? "0" : t;
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

    /// <summary>The decimals a level is written with unless the project says otherwise (+3.00), and the most it may ask for.</summary>
    public const int DefaultLevelDecimals = 2, MaxLevelDecimals = 4;

    /// <summary>A level's decimals as stored or asked: a whole number 0–4; null when it is not one.</summary>
    public static int? LevelDecimalsOf(JsonNode? v) =>
        Lapin.Model.Json.Number(v) is { } d && double.IsFinite(d) && Math.Abs(d - Math.Round(d)) < 1e-9 && d >= 0 && d <= MaxLevelDecimals ? (int)Math.Round(d) : null;

    /// <summary>Whether the project's levels drop their trailing zeros: their own choice, else the project's dimensions'.</summary>
    public static bool SuppressesLevelZeros(StyleBook b) => b.LevelZeros switch { "keep" => false, "suppress" => true, _ => b.SuppressZeros };

    /// <summary>A level as drawings write it: ±0.00, +3.06, −3.98 (metres, two decimals, a true minus).</summary>
    public static string FormatLevel(double z) => FormatLevel(z, DefaultLevelDecimals, false);

    /// <summary>
    /// A level in metres with <paramref name="decimals"/> (0–4) and, with <paramref name="suppressZeros"/>, its trailing zeros
    /// dropped (+3.150 → +3.15, +3.000 → +3, ±0.000 → ±0); signed with a true minus (−0.15). One that rounds to nothing is
    /// ±0.00, or +0.00 with <paramref name="plusAtZero"/> (as the level marks across sections and elevations have written it).
    /// </summary>
    public static string FormatLevel(double z, int decimals, bool suppressZeros, bool plusAtZero = false)
    {
        decimals = Math.Clamp(decimals, 0, MaxLevelDecimals);
        var text = Math.Abs(z).ToString("F" + decimals, CultureInfo.InvariantCulture);
        if (suppressZeros) text = TrimZeros(text);
        var zero = text.All(ch => ch is '0' or '.');
        return (zero ? (plusAtZero ? "+" : "±") : z > 0 ? "+" : "\u2212") + text;
    }

    /// <summary>A level as the project's standards write it: in an imperial project in feet and inches or inches; else in metres with the project's level decimals and trailing zeros.</summary>
    public static string FormatLevel(double z, StyleBook b, bool plusAtZero = false) =>
        IsImperial(b.Units) ? FormatLevel(z, b.Units) : FormatLevel(z, b.LevelDecimals, SuppressesLevelZeros(b), plusAtZero);

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
}

/// <summary>The drafting standards' JSON contracts, generated at build time (web names, nulls left out), so a trimmed engine writes them as the desktop does.</summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[System.Text.Json.Serialization.JsonSerializable(typeof(StyleBook))]
[System.Text.Json.Serialization.JsonSerializable(typeof(TextStyle))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DimStyle))]
[System.Text.Json.Serialization.JsonSerializable(typeof(AxisStyle))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DraftLayer))]
[System.Text.Json.Serialization.JsonSerializable(typeof(TagType))]
public partial class DrawingJson : System.Text.Json.Serialization.JsonSerializerContext;
