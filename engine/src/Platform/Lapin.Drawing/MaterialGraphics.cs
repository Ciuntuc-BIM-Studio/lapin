using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Drawing.Hatch;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Drawing;

/// <summary>
/// A line a material sets for itself in a kind of view (decision 670): its colour (#rrggbb), weight (paper mm) and line
/// type (a project's or a built-in one's name); a field left out keeps what the view's Object styles give.
/// </summary>
public sealed record MaterialLine(string? Color = null, double? Weight = null, string? LineType = null)
{
    public bool Empty => Color == null && Weight == null && LineType == null;

    public static MaterialLine? From(JsonNode? n) => n is JsonObject o && new MaterialLine(HexColor.Read(o["color"], null), Lapin.Model.Json.Number(o["weight"]) is { } w && w > 0 ? w : null,
        (string?)o["lineType"] is { Length: > 0 } lt ? lt : null) is { Empty: false } l ? l : null;

    /// <summary>Field by field: <paramref name="over"/>'s, else this one's.</summary>
    public MaterialLine? Over(MaterialLine? over) => over == null ? this : new MaterialLine(over.Color ?? Color, over.Weight ?? Weight, over.LineType ?? LineType);

    public JsonObject ToJson()
    {
        var o = new JsonObject();
        if (Color != null) o["color"] = Color;
        if (Weight is { } w) o["weight"] = w;
        if (LineType != null) o["lineType"] = LineType;
        return o;
    }
}

/// <summary>
/// A second hatch drawn under a material's own (decision 670, Revit's background pattern): its pattern, scale, angle,
/// colour (#rrggbb, ink when unset) and line weight (paper mm, the hatch pen when unset).
/// </summary>
public sealed record BackgroundHatch(string? Hatch = null, double? HatchScale = null, double? HatchAngle = null, string? HatchColor = null, double? HatchWeight = null)
{
    public static BackgroundHatch? From(JsonNode? n) => n is JsonObject o
        ? new BackgroundHatch((string?)o["hatch"] is { Length: > 0 } h ? h : null, Lapin.Model.Json.Number(o["hatchScale"]) is { } s && s > 0 ? s : null, Lapin.Model.Json.Number(o["hatchAngle"]),
            HexColor.Read(o["hatchColor"], null), Lapin.Model.Json.Number(o["hatchWeight"]) is { } w && w > 0 ? w : null)
        : null;

    public BackgroundHatch Over(BackgroundHatch? over) => over == null ? this
        : new BackgroundHatch(over.Hatch ?? Hatch, over.HatchScale ?? HatchScale, over.HatchAngle ?? HatchAngle, over.HatchColor ?? HatchColor, over.HatchWeight ?? HatchWeight);

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["hatch"] = Hatch };
        if (HatchScale is { } s) o["hatchScale"] = s;
        if (HatchAngle is { } a) o["hatchAngle"] = a;
        if (HatchColor != null) o["hatchColor"] = HatchColor;
        if (HatchWeight is { } w) o["hatchWeight"] = w;
        return o;
    }
}

/// <summary>
/// A material as one kind of view draws it: a hatch (a pattern name, or none), its scale, a fill colour, a hatch pen; and
/// (decision 670) the hatch's own colour, weight (paper mm, over the pen), line type, opacity (0–100) and halftone, the
/// fill's opacity, a background hatch under the hatch, and the lines of its cut (<see cref="CutLine"/>: its outline;
/// <see cref="SeparatorLine"/>: against the neighbouring layers of its element). Null: not given.
/// </summary>
public sealed record ViewGraphics(string? Hatch = null, double? HatchScale = null, string? Fill = null, int? HatchPen = null, double? HatchAngle = null, string? HatchAlign = null)
{
    public string? HatchColor { get; init; }
    public double? HatchWeight { get; init; }
    public string? HatchLineType { get; init; }
    public double? HatchOpacity { get; init; }
    public bool? Halftone { get; init; }
    public double? FillOpacity { get; init; }
    public BackgroundHatch? BackgroundHatch { get; init; }
    public MaterialLine? CutLine { get; init; }
    public MaterialLine? SeparatorLine { get; init; }
    /// <summary>The units its hatch lies in over the pattern's own (decision 671, rare): paper or model; null: the pattern's.</summary>
    public HatchUnits? HatchUnits { get; init; }

    /// <summary>
    /// Its steps by scale (decision 672, <see cref="ScaleStep"/>): in order, the first whose range holds a view's scale lays its
    /// fields over these, field by field; null: none (the material looks the same at every scale, as before).
    /// </summary>
    public IReadOnlyList<ScaleStep>? ByScale { get; init; }

    /// <summary>The fields decision 670 added, beside the first six.</summary>
    public static readonly string[] FlexFields = ["hatchColor", "hatchWeight", "hatchLineType", "hatchOpacity", "halftone", "fillOpacity", "backgroundHatch", "cutLine", "separatorLine"];

    public static ViewGraphics? From(JsonNode? n) => n is JsonObject o
        ? new ViewGraphics(o.ContainsKey("hatch") ? (string?)o["hatch"] ?? "" : null, Lapin.Model.Json.Number(o["hatchScale"]),
            o.ContainsKey("fill") ? (string?)o["fill"] ?? "" : null, Lapin.Model.Json.Number(o["hatchPen"]) is { } p ? (int)p : null,
            Lapin.Model.Json.Number(o["hatchAngle"]), (string?)o["hatchAlign"] is { } al && al is "layer" or "view" ? al : null)
        {
            HatchColor = HexColor.Read(o["hatchColor"], null),
            HatchWeight = Lapin.Model.Json.Number(o["hatchWeight"]) is { } w && w > 0 ? w : null,
            HatchLineType = (string?)o["hatchLineType"] is { Length: > 0 } lt ? lt : null,
            HatchOpacity = Lapin.Model.Json.Number(o["hatchOpacity"]) is { } ho ? Math.Clamp(ho, 0, 100) : null,
            Halftone = o["halftone"] is JsonValue hv && hv.TryGetValue<bool>(out var ht) ? ht : null,
            FillOpacity = Lapin.Model.Json.Number(o["fillOpacity"]) is { } fo ? Math.Clamp(fo, 0, 100) : null,
            BackgroundHatch = Lapin.Drawing.BackgroundHatch.From(o["backgroundHatch"]),
            CutLine = MaterialLine.From(o["cutLine"]),
            SeparatorLine = MaterialLine.From(o["separatorLine"]),
            HatchUnits = Lapin.Drawing.Hatch.HatchScale.Parse((string?)o["hatchUnits"]),
            ByScale = o["byScale"] is JsonArray steps && steps.OfType<JsonObject>().Select(ScaleStep.Read).OfType<ScaleStep>().ToList() is { Count: > 0 } l ? l : null,
        }
        : null;

    /// <summary>The first of its steps whose range holds 1:<paramref name="scale"/> (its index), or none.</summary>
    public (ScaleStep Step, int Index)? StepAt(double? scale)
    {
        if (scale is not { } s || ByScale == null) return null;
        for (var i = 0; i < ByScale.Count; i++) if (ByScale[i].Holds(s)) return (ByScale[i], i);
        return null;
    }
}

/// <summary>
/// A step of a material's graphics by scale (decision 672): from 1:<see cref="From"/> to 1:<see cref="To"/>, both scale
/// denominators and both held (from 100: 1:100 and every smaller scale, 1:200, 1:500; to 50: 1:50 and every larger one, 1:20);
/// one left out is open. <see cref="Fields"/>: any of a kind of view's graphics (hatch, fill, pen, colours, weights, lines…),
/// laid over the material's own field by field where the step holds.
/// </summary>
public sealed record ScaleStep(double? From, double? To, ViewGraphics Fields)
{
    public bool Holds(double scale) => (From is not { } f || scale >= f - 1e-9) && (To is not { } t || scale <= t + 1e-9);

    public static ScaleStep? Read(JsonObject o) =>
        ViewGraphics.From(o) is { } g ? new ScaleStep(Lapin.Model.Json.Number(o["from"]), Lapin.Model.Json.Number(o["to"]), g with { ByScale = null }) : null;
}

/// <summary>Where a material's hatch starts and turns: from the view's origin, or from each band it fills (its origin on the band's longest edge, its angle along it).</summary>
public static class HatchAlign
{
    public const string View = "view", Layer = "layer";
    public static readonly string[] All = [View, Layer];

    /// <summary>
    /// An element's own choice over its materials' (an attribute <c>hatchAlign</c>, decision 369): "material" (or none) as
    /// the materials say; "axis", every band of it along its axis from the axis's start, so the courses run on from one
    /// layer to the next; "view", from the view's origin, unturned.
    /// </summary>
    public const string Material = "material", Axis = "axis";
    public static readonly string[] ElementChoices = [Material, Axis, View];
}

/// <summary>An element's hatch alignment in a drawing (<see cref="HatchAlign.ElementChoices"/>): along its axis (an angle and an origin), or from the view's origin.</summary>
public sealed record ElementHatch(double? Angle, Vec2? Origin);

/// <summary>A material's graphics in one kind of view, and where they came from: its own attributes ("own") or its name ("name").</summary>
/// <param name="Joins">Whether its elements join others (forum topic 59): off, each keeps its own outline in a drawing, never merged with a neighbour's.</param>
public sealed record ResolvedMaterial(string Name, string? Hatch, double HatchScale, string? Fill, int? HatchPen, bool Membrane, bool DrawJoints, string? Color, string Source, double HatchAngle = 0, string HatchAlign = HatchAlign.View, bool Joins = true)
{
    /// <summary>The hatch's own colour (#rrggbb) over the category's; null: the category's, else ink.</summary>
    public string? HatchColor { get; init; }
    /// <summary>The hatch's weight (paper mm) over its pen; null: the pen's, else Lapin's hatch weight.</summary>
    public double? HatchWeight { get; init; }
    /// <summary>The hatch's line type (a name); null: continuous.</summary>
    public string? HatchLineType { get; init; }
    /// <summary>The hatch's opacity, 0–100; null: opaque.</summary>
    public double? HatchOpacity { get; init; }
    /// <summary>The hatch drawn halftone (its colour halfway to white).</summary>
    public bool Halftone { get; init; }
    /// <summary>The fill's opacity, 0–100; null: opaque.</summary>
    public double? FillOpacity { get; init; }
    /// <summary>A second hatch drawn under the hatch (its pattern known); null: none.</summary>
    public BackgroundHatch? Background { get; init; }
    /// <summary>The material's cut outline, over the Object styles' cut lines; null: theirs.</summary>
    public MaterialLine? CutLine { get; init; }
    /// <summary>The material's lines against its element's other layers, over the Object styles' layer lines; null: theirs.</summary>
    public MaterialLine? SeparatorLine { get; init; }

    /// <summary>The units its hatch lies in over the pattern's own (decision 671): paper or model; null: the pattern's.</summary>
    public HatchUnits? HatchUnits { get; init; }

    /// <summary>The step by scale it was resolved with (decision 672): the kind of view whose steps gave it and its index; null: none.</summary>
    public (string Kind, int Index)? Step { get; init; }

    /// <summary>
    /// What its cut bands join by in place of its name (decision 672: a view's small-scale cut, whose solid bands of one
    /// colour read as one area); null: its name, as before.
    /// </summary>
    public string? JoinAs { get; init; }

    /// <summary>The material standard that gave what the material does not set itself, when not Lapin's (decision 673); null: Lapin's.</summary>
    public string? Standard { get; init; }

    /// <summary>Whether it sets anything decision 670 added (a drawing of materials that set none is drawn exactly as before).</summary>
    public bool Flex => HatchColor != null || HatchWeight != null || HatchLineType != null || HatchOpacity != null || Halftone || FillOpacity != null || Background != null || CutLine != null || SeparatorLine != null;

    /// <summary>The colour a hatch of this material is drawn in: its own over <paramref name="fallback"/>, halftone and opacity applied.</summary>
    public string HatchInk(string fallback) => Ink(HatchColor ?? fallback, Halftone, HatchOpacity);

    /// <summary>A colour washed halfway to white when halftone, given an opacity (0–100) when below full.</summary>
    public static string Ink(string color, bool halftone, double? opacity)
    {
        var c = halftone ? HexColor.Mix(color, 0.5) : color;
        return opacity is { } o && o < 100 ? HexColor.WithAlpha(c, o / 100) : c;
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["name"] = Name, ["hatch"] = Hatch, ["hatchScale"] = HatchScale, ["fill"] = Fill, ["membrane"] = Membrane, ["drawJoints"] = DrawJoints, ["source"] = Source, ["hatchAngle"] = HatchAngle, ["hatchAlign"] = HatchAlign, ["joins"] = Joins };
        if (HatchPen is { } p) o["hatchPen"] = p;
        if (Color != null) o["color"] = Color;
        if (HatchColor != null) o["hatchColor"] = HatchColor;
        if (HatchWeight is { } w) o["hatchWeight"] = w;
        if (HatchLineType != null) o["hatchLineType"] = HatchLineType;
        if (HatchOpacity is { } ho) o["hatchOpacity"] = ho;
        if (Halftone) o["halftone"] = true;
        if (FillOpacity is { } fo) o["fillOpacity"] = fo;
        if (Background != null) o["backgroundHatch"] = Background.ToJson();
        if (CutLine != null) o["cutLine"] = CutLine.ToJson();
        if (SeparatorLine != null) o["separatorLine"] = SeparatorLine.ToJson();
        if (HatchUnits is { } hu) o["hatchUnits"] = Lapin.Drawing.Hatch.HatchScale.Name(hu);
        if (Step is { } st) o["step"] = new JsonObject { ["kind"] = st.Kind, ["index"] = st.Index };
        if (Standard != null) o["standard"] = Standard;
        return o;
    }
}

/// <summary>
/// How an IfcMaterial draws where a view cuts or sees it (the old Lapin's
/// project materials, held now on the material itself). Its attributes:
/// <c>section</c> {hatch, hatchScale, fill, hatchPen} — what a cut shows (fill: a
/// colour, or "material" for the material's own colour; none unless asked);
/// <c>plan</c> and <c>elevation</c>, the same, for those kinds of view;
/// <c>membrane</c> (drawn as a heavy dashed line); <c>drawJoints</c>.
/// A field the material does not give comes from its name: concrete,
/// masonry, insulation, timber, steel… as Revit's default materials (<see cref="Guesses"/>; an unknown name none). A plan takes the section's graphics
/// unless it has its own; an elevation guesses its surface from the name
/// apart (<see cref="ElevationFromName"/>: brick, stone, boards, never BATT).
/// <c>hatch</c> null (JSON null) means "no hatch", absent means "from the name".
/// </summary>
public static class MaterialGraphics
{
    public static readonly string[] Kinds = ["section", "plan", "elevation"];

    /// <summary>Layers thinner than this are not cut into bands: they draw as membranes.</summary>
    public const double MembraneThickness = 0.002;

    public static string Normalise(string name) => Regex.Replace(name.Trim().ToLowerInvariant(), @"\s+", " ");

    /// <summary>
    /// What a name suggests for a cut (decision 460, after the tester's request of decision 456) is now data: the rules of the
    /// built-in standard „Lapin" (<see cref="MaterialStandard.Lapin"/>, MaterialStandards/lapin.json, decision 673), with exactly the
    /// results of the table it replaced. Brick, masonry and stone 45° lines (MASONRY); concrete blocks, aerated concrete and CMU a
    /// diagonal crosshatch (BLOCK); concrete the speckle (CONCRETE); rigid insulation RIGID, batts and wool BATT; gypsum, plaster
    /// and render GYPSUM; sand SAND; timber WOOD, plywood, OSB and CLT WOODCROSS; steel and metal STEEL; earth and gravel theirs;
    /// tiles TILES. Glass, air, paint and finishes, membranes and a name nothing matches: none. The first match wins: membranes
    /// and insulation first ("bitumen sheet", "stone wool"), glass and air ("glass block"), blocks before concrete and before
    /// masonry ("concrete block", "zidărie BCA").
    /// </summary>
    sealed record Guess(bool Membrane = false, string? Hatch = null);

    /// <summary>
    /// The suggestions decision 325 made (a project with no <c>hatchRev</c>): brick and masonry in courses (BRICK), an
    /// unknown name nothing. Kept only so a project saved then keeps the hatches it drew (<c>MaterialHatchUpgrade</c>).
    /// </summary>
    static readonly (Regex Re, Guess G)[] Guesses325 =
    [
        (new(@"vapou?r|barrier|membrane|foil|\bdpc\b|damp|bitum|sheet|folie|barier"), new(Membrane: true)),
        (new(@"insul|wool|eps\b|xps\b|polystyren|pir\b|pur\b|vata|izola"), new(Hatch: "BATT")),
        (new(@"brick|masonry|block|caramid|zidari|bca|aac"), new(Hatch: "BRICK")),
        (new(@"concrete|beton|screed|sapa|c\d\d/"), new(Hatch: "CONCRETE")),
        (new(@"timber|wood|clt|osb|plywood|lemn"), new(Hatch: "WOOD")),
        (new(@"steel|metal|otel|alumin"), new(Hatch: "STEEL")),
        (new(@"gravel|ballast|pietris|aggregate"), new(Hatch: "GRAVEL")),
        (new(@"earth|soil|pamant|ground"), new(Hatch: "EARTH")),
        (new(@"tile|ceramic|gresie|faianta"), new(Hatch: "TILES")),
        (new(@"plaster|render|gypsum|tencuial|gips|finish|paint"), new()),
    ];

    /// <summary>
    /// The suggestions decision 456 made (<c>hatchRev</c> 1): masonry 45° lines, reinforced concrete RCONCRETE, an unknown
    /// name the 45° lines. Kept only so a project saved then keeps the hatches it drew.
    /// </summary>
    static readonly (Regex Re, Guess G)[] Guesses456 =
    [
        (new(@"vapou?r|barrier|membrane|foil|\bdpc\b|damp|bitum|sheet|folie|barier"), new(Membrane: true)),
        (new(@"insul|wool|eps\b|xps\b|polystyren|pir\b|pur\b|vata|izola"), new(Hatch: "BATT")),
        (new(@"glass|glazing|sticla|geam|\bair\b|cavity|\baer\b|lama de aer"), new()),
        (new(@"reinforced|armat|\brc\b|stahlbeton"), new(Hatch: "RCONCRETE")),
        (new(@"brick|masonry|block|caramid|zidari|bca|aac"), new(Hatch: "LINES45")),
        (new(@"concrete|beton|screed|sapa|c\d\d/"), new(Hatch: "CONCRETE")),
        (new(@"timber|wood|clt|osb|plywood|lemn"), new(Hatch: "WOOD")),
        (new(@"steel|metal|otel|alumin"), new(Hatch: "STEEL")),
        (new(@"gravel|ballast|pietris|aggregate"), new(Hatch: "GRAVEL")),
        (new(@"earth|soil|pamant|ground"), new(Hatch: "EARTH")),
        (new(@"tile|ceramic|gresie|faianta"), new(Hatch: "TILES")),
        (new(@"plaster|render|gypsum|tencuial|gips|finish|paint|vopsea|stucco"), new()),
    ];

    /// <summary>
    /// What a name suggests for the surface an elevation sees (decision 327): what the material looks like on a facade,
    /// not what its cut shows. Insulation and membranes are hidden in a build-up and draw nothing (BATT is a section's
    /// hatch only); plaster, render and paint none; concrete none either (its speckle over a whole facade reads as noise);
    /// timber as boards, metal cladding and sheet roofs as vertical seams, stone in courses, roof tiles, shingles and slates
    /// as horizontal courses. The first match wins: roof coverings first ("concrete tile", "bitumen shingle"), then the hidden
    /// layers ("stone wool" is insulation), then timber before cladding ("timber cladding" is boards).
    /// </summary>
    static readonly (Regex Re, string? Hatch)[] ElevationGuesses327 =
    [
        (new(@"clay tile|concrete tile|roof ?tile|tigla|olan|shingle|sindrila|slate|ardezie"), "HORIZONTAL"),
        (new(@"vapou?r|barrier|membrane|foil|\bdpc\b|damp|bitum|folie|barier"), null),
        (new(@"insul|wool|eps\b|xps\b|polystyren|pir\b|pur\b|vata|izola"), null),
        (new(@"plaster|render|gypsum|tencuial|gips|paint|vopsea|stucco|adhesive|adeziv"), null),
        (new(@"brick|masonry|block|caramid|zidari|bca|aac|clinker"), "BRICK"),
        (new(@"stone|piatra|granit|marble|marmura|travertin|limestone|calcar|sandstone|ashlar"), "STONE"),
        (new(@"timber|wood|lemn|larch|cedar|pine|spruce|lambriu|siding|weatherboard|scandura"), "BOARDS"),
        (new(@"clad|placare|sandwich|trapez|tabla|zinc|copper|cupru|standing seam|faltuit|corrugated|cutat|metal sheet|steel sheet|roof sheet"), "VERTICAL"),
        (new(@"concrete|beton|screed|sapa|c\d\d/"), null),
        (new(@"\bsand\b|nisip"), "SAND"),
        (new(@"gravel|ballast|pietris|aggregate"), "GRAVEL"),
        (new(@"earth|soil|pamant|ground"), "EARTH"),
        (new(@"tile|ceramic|gresie|faianta|porcelain"), "TILES"),
    ];

    // What a name suggests for a facade in a project made since decision 671 (hatchRev 3) is the rules of the built-in standard
    // „Lapin" (decision 673): decision 327's matches drawn at the object's real size (brick 240×63, blocks 400×200, aerated
    // blocks 600×250, coursed stone 600×300, boards 150 mm, standing seams every 500 mm, roof tiles at a 330 mm gauge, tiles
    // 300×300; sand, gravel and earth keep their paper patterns). A project made before keeps decision 327's (MaterialHatchUpgrade).

    // Romanian diacritics fold to their letters, as the old names were typed.
    static string Fold(string name) => Normalise(name).Replace('ă', 'a').Replace('â', 'a').Replace('î', 'i').Replace('ș', 's').Replace('ş', 's').Replace('ț', 't').Replace('ţ', 't');

    /// <summary>What the name matches in a legacy table (null: nothing does).</summary>
    static Guess? GuessOf(string name, (Regex Re, Guess G)[] table)
    {
        var n = Fold(name);
        foreach (var (re, g) in table) if (re.IsMatch(n)) return g;
        return null;
    }

    /// <summary>What a standard's rules say of a cut by the name (<paramref name="standard"/> null: Lapin's): its hatch and whether it is a membrane; null when no rule matches.</summary>
    static Guess? Suggested(string name, MaterialStandard? standard = null, string? category = null) =>
        (standard ?? MaterialStandard.Lapin).CutRule(name, category) is { } r ? new Guess(r.Membrane ?? false, r.Section?.Hatch is { Length: > 0 } h ? h.ToUpperInvariant() : null) : null;

    /// <summary>
    /// What an earlier build's names suggested for a cut (a pattern, or none): decision 325's for a project of no
    /// <c>hatchRev</c> (<paramref name="rev"/> 0), decision 456's for <c>hatchRev</c> 1 (a name nothing matched: 45° lines).
    /// </summary>
    public static string? LegacyFromName(string name, int rev = 0) => rev switch
    {
        0 => GuessOf(name, Guesses325) is { Membrane: false } g ? g.Hatch : null,
        1 => GuessOf(name, Guesses456) is { } g ? (g.Membrane ? null : g.Hatch) : "LINES45",
        _ => FromName(name).Hatch,
    };

    /// <summary>
    /// What a name suggests for an elevation's surface hatch: a pattern, or none (insulation, membranes, finishes, concrete,
    /// an unknown name); real-size patterns (decision 671), or with <paramref name="paper"/> decision 327's paper ones (a
    /// project made before decision 671).
    /// </summary>
    public static string? ElevationFromName(string name, bool paper = false)
    {
        if (paper)
        {
            var n = Fold(name);
            foreach (var (re, h) in ElevationGuesses327) if (re.IsMatch(n)) return h;
            return null;
        }
        return MaterialStandard.Lapin.ElevationRule(name)?.Elevation?.Hatch is { Length: > 0 } eh ? eh.ToUpperInvariant() : null;
    }

    /// <summary>What a name suggests for a section: its hatch (or none) and whether it is a membrane.</summary>
    public static (string? Hatch, bool Membrane) FromName(string name) => Suggested(name) is { } g ? (g.Hatch, g.Membrane) : default;

    /// <summary>
    /// The material's own graphics for a kind of view (a plan: the section's fields, then the plan's own), each kind's step
    /// for 1:<paramref name="scale"/> laid over its fields (decision 672: a plan takes the section's step, then its own
    /// fields, then its own step); the step that did, the last one laid. No scale, no step.
    /// </summary>
    static (ViewGraphics? Own, (string, int)? Step) Own(Entity? material, string kind, double? scale)
    {
        if (material == null) return (null, null);
        (string, int)? step = null;
        ViewGraphics? Stepped(string k)
        {
            var g = ViewGraphics.From(material.Attrs[k]);
            if (g?.StepAt(scale) is not { } at) return g;
            step = (k, at.Index);
            return Merge(g, at.Step.Fields);
        }
        var own = kind switch
        {
            "section" => Stepped("section"),
            "plan" => Merge(Stepped("section"), Stepped("plan")),
            _ => Stepped("elevation"),
        };
        return (own, step);
    }

    /// <summary>Two graphics field by field, <paramref name="b"/>'s over <paramref name="a"/>'s (lines and background hatches field by field too).</summary>
    public static ViewGraphics? Merge(ViewGraphics? a, ViewGraphics? b) => (a, b) switch
    {
        (null, null) => null,
        (null, _) => b,
        (_, null) => a,
        _ => new ViewGraphics(b!.Hatch ?? a!.Hatch, b.HatchScale ?? a!.HatchScale, b.Fill ?? a!.Fill, b.HatchPen ?? a!.HatchPen, b.HatchAngle ?? a!.HatchAngle, b.HatchAlign ?? a!.HatchAlign)
        {
            HatchColor = b.HatchColor ?? a!.HatchColor, HatchWeight = b.HatchWeight ?? a!.HatchWeight, HatchLineType = b.HatchLineType ?? a!.HatchLineType,
            HatchOpacity = b.HatchOpacity ?? a!.HatchOpacity, Halftone = b.Halftone ?? a!.Halftone, FillOpacity = b.FillOpacity ?? a!.FillOpacity,
            BackgroundHatch = a!.BackgroundHatch is { } ab ? ab.Over(b.BackgroundHatch) : b.BackgroundHatch,
            CutLine = a!.CutLine is { } ac ? ac.Over(b.CutLine) : b.CutLine,
            SeparatorLine = a!.SeparatorLine is { } asl ? asl.Over(b.SeparatorLine) : b.SeparatorLine,
            HatchUnits = b.HatchUnits ?? a!.HatchUnits,
        },
    };

    /// <summary>
    /// The material's graphics in one kind of view: its own fields, else what the project's material standard gives it by its
    /// name (<paramref name="standard"/>, decision 673; null: Lapin's, <see cref="MaterialStandard.Lapin"/>, today's look). An
    /// elevation of a project made before decision 671 under Lapin's standard takes decision 327's paper patterns
    /// (<paramref name="paperSurfaces"/>, <c>MaterialHatchUpgrade.PaperSurfaces</c>). At a view's <paramref name="scale"/> (a
    /// denominator), each tier's step for it over its fields (decision 672, <see cref="ScaleStep"/>): the standard, its step, the
    /// material's own, its step. A material that sets its own pattern takes its own scale, angle and units, never the
    /// standard's for another pattern; one that sets its own background hatch, none of the standard's. No scale, no step.
    /// This is the material's tier of a view's graphics; what a view lays over it is <c>ViewMaterialGraphics</c> (Lapin.Platform).
    /// </summary>
    public static ResolvedMaterial Resolve(Entity? material, string kind = "section", string? name = null, bool paperSurfaces = false, double? scale = null, MaterialStandard? standard = null)
    {
        name ??= material?.Name ?? "";
        standard ??= MaterialStandard.Lapin;
        var (own, step) = Own(material, kind, scale);
        var elevation = kind == "elevation";
        var category = material?.Str("category");
        // The standard's tier: its rule for the name (a project before decision 671 under Lapin's: the paper facades).
        ViewGraphics? std;
        StandardRule? rule;
        if (elevation && paperSurfaces && standard.IsLapin) (std, rule) = (new ViewGraphics(ElevationFromName(name, true) ?? ""), null);
        else (std, rule) = standard.For(kind, name, category, scale);
        if (std != null && own?.Hatch != null) std = std with { HatchScale = null, HatchAngle = null, HatchUnits = null };
        if (std != null && own?.BackgroundHatch?.Hatch != null) std = std with { BackgroundHatch = null };
        var g = Merge(std, own);
        var color = material?.Str("color");
        // "" is an explicit none; null is "not given" (nothing suggested either: no hatch).
        string? hatch = g?.Hatch is { Length: > 0 } h ? h.ToUpperInvariant() : null;
        // A fill only when asked: a colour, or "material" for the material's own (every material has a colour for 3D).
        string? fill = g?.Fill is { Length: > 0 } f ? (f == "material" ? color : f) : null;
        var hasOwn = own != null || material?.Attrs.ContainsKey("membrane") == true;
        // A cut's membrane by its name (never a facade's).
        var membrane = material?.Attrs["membrane"] is JsonValue mv && mv.TryGetValue<bool>(out var mb) ? mb : !elevation && rule?.Membrane == true;
        if (membrane) hatch = null;
        var joints = material?.Attrs["drawJoints"] is JsonValue jv && jv.TryGetValue<bool>(out var j) && j;
        // The flexible fields (decision 670): none given, none set, so a material that sets none draws as before.
        var background = g?.BackgroundHatch is { Hatch: { Length: > 0 } bh } bg ? bg with { Hatch = bh.ToUpperInvariant() } : null;
        return new ResolvedMaterial(name, hatch, g?.HatchScale ?? 1, fill, g?.HatchPen, membrane, joints, color, hasOwn ? "own" : "name", g?.HatchAngle ?? 0, g?.HatchAlign ?? HatchAlign.View,
            JoinsOf(material))
        {
            HatchColor = g?.HatchColor, HatchWeight = g?.HatchWeight, HatchLineType = g?.HatchLineType, HatchOpacity = g?.HatchOpacity is { } ho && ho < 100 ? ho : null,
            Halftone = g?.Halftone == true, FillOpacity = fill != null && g?.FillOpacity is { } fo && fo < 100 ? fo : null,
            Background = membrane ? null : background, CutLine = g?.CutLine, SeparatorLine = g?.SeparatorLine, HatchUnits = g?.HatchUnits, Step = step,
            Standard = standard.IsLapin ? null : standard.Id,
        };
    }

    /// <summary>The fill a resolved material lays under its hatch: its colour at its opacity; null: none.</summary>
    public static string? FillInk(ResolvedMaterial g) => g.Fill is { } f ? (g.FillOpacity is { } o ? HexColor.WithAlpha(f, o / 100) : f) : null;

    /// <summary>Whether a material's elements join others (<c>joins</c>, forum topic 59): unless it says no; no material joins.</summary>
    public static bool JoinsOf(Entity? material) => !(material?.Attrs["joins"] is JsonValue v && v.TryGetValue<bool>(out var j) && !j);

    /// <summary>
    /// A hatch as an IFC file describes it, as a pattern of the library: a
    /// named pattern (known, or guessed from its name), or a line hatching
    /// (horizontal, else the 45° lines). For reading IFC materials into Lapin's.
    /// </summary>
    public static ViewGraphics? FromIfcHatch(string? pattern, double? scale, double? linesAngleDeg, string? background, IEnumerable<HatchPattern>? extra = null)
    {
        ViewGraphics? g = null;
        if (pattern != null)
        {
            // A pattern named as no pattern of the library: what its name says, if it says anything (never the general lines).
            var known = HatchLibrary.Find(pattern, extra) != null ? pattern.ToUpperInvariant() : Suggested(pattern)?.Hatch;
            if (known != null) g = new ViewGraphics(known, scale ?? 1);
        }
        else if (linesAngleDeg is { } a)
        {
            var d = ((a % 180) + 180) % 180;
            g = new ViewGraphics(Math.Abs(d) < 1 || Math.Abs(d - 180) < 1 ? "HORIZONTAL" : "LINES45", 1);
        }
        if (background != null) g = (g ?? new ViewGraphics("")) with { Fill = background };
        return g;
    }

    /// <summary>
    /// What IFC gets from the material (null when it gives nothing of its own):
    /// its colour and the hatches of the section, plan and elevation — written
    /// on export as IfcSurfaceStyle and IfcFillAreaStyle, named by view.
    /// </summary>
    public static JsonObject? Appearance(Entity material)
    {
        var a = material.Attrs;
        if (!a.ContainsKey("section") && !a.ContainsKey("plan") && !a.ContainsKey("elevation") && !a.ContainsKey("color")) return null;
        var o = new JsonObject();
        if (material.Str("color") is { } c) o["color"] = c;
        if (material.Attrs["transparency"] is { } t) o["transparency"] = t.DeepClone();
        var hatches = new JsonObject();
        // Its hatch, fill and (decision 670) the hatch's colour and weight and a background hatch, as IfcFillAreaStyle carries them.
        JsonObject? HatchOf(ResolvedMaterial g)
        {
            if (string.IsNullOrEmpty(g.Hatch) && string.IsNullOrEmpty(g.Fill) && g.Background == null) return null;
            var h = new JsonObject();
            if (!string.IsNullOrEmpty(g.Hatch)) h["pattern"] = g.Hatch.ToUpperInvariant();
            if (!string.IsNullOrEmpty(g.Hatch) && g.HatchScale != 1) h["scale"] = g.HatchScale;
            if (!string.IsNullOrEmpty(g.Fill)) h["background"] = g.Fill;
            if (g.HatchColor != null) h["color"] = g.HatchColor;
            if (g.HatchWeight is { } w) h["weight"] = w;
            if (g.Background is { } bg) h["backgroundHatch"] = bg.ToJson();
            return h;
        }
        var section = Resolve(material, "section");
        if (!section.Membrane && HatchOf(section) is { } sh) hatches["section"] = sh;
        if (a.ContainsKey("plan") && Resolve(material, "plan") is var plan && HatchOf(plan) is { } ph) hatches["plan"] = ph;
        if (a.ContainsKey("elevation") && Resolve(material, "elevation") is var el && HatchOf(el) is { } eh) hatches["elevation"] = eh;
        if (hatches.Count > 0) o["hatches"] = hatches;
        return o;
    }
}
