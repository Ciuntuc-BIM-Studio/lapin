using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// View styles (docs/view-styles.md): how a kind of drawing shows the model,
/// as ArchiCAD's partial structure display and Revit's view templates. A
/// style is the project's (<c>viewStyles</c>), named, one per discipline by
/// default (Architectural, Structural, MEP), editable, and more can be made.
/// It says:
/// <list type="bullet">
/// <item><c>layers</c>: the partial structure display (as ArchiCAD's), which layers of layered elements draw: the entire
/// model (<c>all</c>), all but the finishes (<c>noFinishes</c>, <see cref="LayerSets.IsFinish"/>), the core only
/// (<c>core</c>), or the cores of the load-bearing elements only (<c>coreLoadBearing</c>, <see cref="BearsLoad"/>: the
/// others not drawn at all). A style says it for its plans (<c>layersPlan</c>), its sections and elevations
/// (<c>layersSection</c>) and its other views, 3D among them (<c>layers</c>, also what the first two take when unset); a
/// view sets its own (<c>layers</c> again). A new project's Architectural style draws its plans without the finishes
/// (decision 500); a project made before keeps what it drew (<see cref="StructureUpgrade"/>);</item>
/// <item><c>joins</c>: whether one material goes on from one element into the
/// next without a line (the visual joins), on by default;</item>
/// <item><c>overhead</c>: whether a plan draws what lies above its cut, up to
/// its range's top;</item>
/// <item><c>ifc2d</c>: whether a plan draws the 2D an IFC file gave its
/// elements (their footprints and symbols, <see cref="Plan2d"/>) instead of
/// their cut, on by default;</item>
/// <item><c>voids</c>: whether the openings' voids are drawn (decision 290,
/// <see cref="Coordination.VoidDrawings"/>): cut or seen, crossed, with their
/// marks; off by default, on in the Coordination style; <c>voidSizes</c>:
/// with their sizes and bottom levels under the marks, on by default;</item>
/// <item><c>finishHatch</c>: whether a plan hatches the rooms' floor finishes
/// (a reflected ceiling plan their ceilings') as their types say (decision
/// 299); on in the Architectural style, off in the others;</item>
/// <item><c>surfaceHatch</c>: whether elevations and sections hatch the faces they
/// see with their materials' elevation graphics (decision 325,
/// <see cref="SurfaceHatches"/>); on in the Architectural style, off in the others;</item>
/// <item><c>entourage</c>: how people, cars and plants draw in 2D (decision 341): <c>outline</c> (the default, a style
/// saved before it too: a plan's simple symbol, an elevation's or a section's silhouette, never their bodies' edges),
/// <c>abstract</c> (circles and boxes), <c>detailed</c> (the symbols and projected bodies drawn before) or <c>none</c>;
/// <c>entourageFill</c>: the silhouettes' fill in elevations and sections, a colour, none by default;</item>
/// <item><c>minHatchSpacing</c>: the closest (paper mm) a real-size hatch's lines may lie and still be drawn (decision 679):
/// 0.7 by default, a style saved before too; a brick's courses at 1:200 lie closer and are left out; 0 draws them all.</item>
/// <item><c>smallScaleCut</c>: how cuts draw from a scale on (decision 680, <see cref="SmallScaleCut"/>): {mode, from, color};
/// none (asMaterial, today's look) by default and for a style saved before.</item>
/// <item><c>materialGraphicsSet</c>: the material graphics set its views draw their materials with (decision 682,
/// <see cref="MaterialGraphicsSets"/>); in a project made since, the Structural style names „Structure” and the MEP style
/// „MEP”; none in a project made before (Î10). A view names another, or <c>none</c>, with <c>view.setStyle</c>.</item>
/// <item><c>beyond</c>: what sections and elevations draw beyond their cut (issue #93, decision 721): <c>full</c> (everything
/// up to the view's depth, as before; the default and a style saved before), <c>near</c> (what lies within <c>nearDepth</c>
/// of the cut as before, what lies wholly farther in the background pen: thin and grey, ArchiCAD's marked distant area)
/// or <c>cut</c> (only what the plane cuts: a detail); <c>nearDepth</c> in metres, 1 by default.</item>
/// </list>
/// A view takes its style (<c>viewStyle</c>, else its discipline's), and may
/// set any of these itself: what it sets wins over the style.
/// </summary>
public static class ViewStyles
{
    public const string Key = "viewStyles";
    public static readonly string[] LayerModes = ["all", "noFinishes", "core", "coreLoadBearing"];
    const string LayerModesText = "all, noFinishes, core or coreLoadBearing";
    public static readonly string[] Fields = ["layers", "joins", "overhead", "ifc2d", "voids", "voidSizes", "finishHatch", "surfaceHatch", "entourage", "entourageFill", "detail",
        "minHatchSpacing", "smallScaleCut", "materialGraphicsSet", "wallFinishesPlan", "wallFinishesSection", "floorFinishesPlan", "floorFinishesSection", "beyond", "nearDepth"];

    /// <summary>What a section or an elevation draws beyond its cut (decision 721): everything, the near part with the rest in the background, or the cut alone.</summary>
    public static readonly string[] BeyondModes = ["full", "near", "cut"];
    /// <summary>How far from the cut a view in <c>near</c> mode draws in full before the background (m).</summary>
    public const double DefaultNearDepth = 1.0;

    static string BeyondMode(JsonNode? n) =>
        (string?)n is { } b && BeyondModes.Contains(b) ? b : throw new CommandException($"„{n}” is not full, near or cut.");

    static double NearDepthOf(JsonNode? n) =>
        Json.Number(n) is { } d && d >= 0 && double.IsFinite(d) ? d : throw new CommandException("nearDepth is a distance in metres, 0 or more.");

    /// <summary>
    /// The finish layers by the kind of view (the forum's topic #30): whether plans, and whether sections and elevations, draw
    /// the walls' finishes and the floors' (<see cref="LayerSets.SubOf"/>: a layer's own category, else its material's). All on by
    /// default and for a style saved before them; Visibility / Graphics hides what they leave out (<see cref="VisibilityGraphics.HidesPart"/>).
    /// </summary>
    public static readonly string[] FinishFields = ["wallFinishesPlan", "wallFinishesSection", "floorFinishesPlan", "floorFinishesSection"];
    /// <summary>How people, cars and plants draw in 2D (decision 341).</summary>
    public static readonly string[] EntourageModes = ["outline", "abstract", "detailed", "none"];

    static string EntourageMode(JsonNode? n) =>
        (string?)n is { } m ? EntourageModes.Contains(m) ? m : throw new CommandException($"„{m}” is not outline, abstract, detailed or none.") : "outline";

    /// <summary>A silhouettes' fill: a colour (#rrggbb), or null for none.</summary>
    static string? FillOf(JsonNode? n) =>
        n is JsonValue v && v.TryGetValue<string>(out var c) && c.Length == 7 && c[0] == '#' ? c : null;

    static string? CheckedFill(JsonNode? n) =>
        n == null || (string?)n is "" or "none" ? null : FillOf(n) ?? throw new CommandException("A fill is a colour (#rrggbb) or none.");

    /// <summary>
    /// A style, or a view's settings as its style and its own choices make them. <see cref="Layers"/>: the partial structure
    /// display of its views (a view's: what it draws by); <see cref="LayersPlan"/>, <see cref="LayersSection"/>: a style's
    /// for its plans and for its sections and elevations, null to take <see cref="Layers"/> (<see cref="LayersFor"/>).
    /// </summary>
    public sealed record Style(string Id, string Name, string? Discipline, string Layers, bool Joins, bool Overhead, bool Ifc2d = true, bool Voids = false, bool VoidSizes = true, bool FinishHatch = true, bool SurfaceHatch = true, string Entourage = "outline", string? EntourageFill = null, string? GraphicsSet = null, int Detail = PlanDetail.Lod350,
        bool WallFinishesPlan = true, bool WallFinishesSection = true, bool FloorFinishesPlan = true, bool FloorFinishesSection = true, string? LayersPlan = null, string? LayersSection = null,
        bool FinishElements = true, double MinHatchSpacing = Lapin.Drawing.Hatch.HatchScale.DefaultMinSpacing, SmallScaleCut? SmallScaleCut = null, string? MaterialGraphicsSet = null,
        string Beyond = "full", double NearDepth = DefaultNearDepth)
    {
        /// <summary>The partial structure display of a view of this kind (a plan, a section or an elevation, another).</summary>
        public string LayersFor(string? viewKind) => viewKind switch
        {
            "plan" or null => LayersPlan ?? Layers,
            "section" or "elevation" => LayersSection ?? Layers,
            _ => Layers,
        };

        public JsonObject ToJson() => WithSet(new()
        {
            ["id"] = Id, ["name"] = Name, ["discipline"] = Discipline, ["layers"] = Layers, ["joins"] = Joins, ["overhead"] = Overhead, ["ifc2d"] = Ifc2d,
            ["voids"] = Voids, ["voidSizes"] = VoidSizes, ["finishHatch"] = FinishHatch, ["surfaceHatch"] = SurfaceHatch,
            ["entourage"] = Entourage, ["entourageFill"] = EntourageFill, ["detail"] = Detail,
            ["wallFinishesPlan"] = WallFinishesPlan, ["wallFinishesSection"] = WallFinishesSection, ["floorFinishesPlan"] = FloorFinishesPlan, ["floorFinishesSection"] = FloorFinishesSection,
            ["minHatchSpacing"] = MinHatchSpacing,
        });

        /// <summary>Whether a view of this kind draws the finishes of a category's layers (walls; slabs and coverings for floors); true for any other kind of view or category.</summary>
        public bool DrawsFinishes(string? viewKind, string? category)
        {
            var plan = viewKind is "plan" or null;
            if (!plan && viewKind is not ("section" or "elevation")) return true;
            return category switch
            {
                "walls" => plan ? WallFinishesPlan : WallFinishesSection,
                "slabs" => plan ? FloorFinishesPlan : FloorFinishesSection,
                _ => true,
            };
        }

        /// <summary>The graphics set its views take when they have none of their own (decision 391), and its plans' and sections' structure display, written only when set.</summary>
        JsonObject WithSet(JsonObject o)
        {
            if (GraphicsSet != null) o["graphicsSet"] = GraphicsSet;
            if (LayersPlan != null) o["layersPlan"] = LayersPlan;
            if (LayersSection != null) o["layersSection"] = LayersSection;
            if (!FinishElements) o["finishElements"] = false;
            // Written only when set (decision 680), so a style that sets none saves as before.
            if (SmallScaleCut != null) o["smallScaleCut"] = SmallScaleCut.ToJson();
            // The same (decision 682).
            if (MaterialGraphicsSet != null) o["materialGraphicsSet"] = MaterialGraphicsSet;
            // The same (decision 721): a style that draws everything beyond its cut saves as before.
            if (Beyond != "full") o["beyond"] = Beyond;
            if (Math.Abs(NearDepth - DefaultNearDepth) > 1e-9) o["nearDepth"] = NearDepth;
            return o;
        }

        public static Style From(JsonObject o) => new(
            (string?)o["id"] ?? "", (string?)o["name"] ?? "Style", (string?)o["discipline"],
            (string?)o["layers"] is { } l && LayerModes.Contains(l) ? l : "all",
            o["joins"] is not JsonValue jv || !jv.TryGetValue<bool>(out var j) || j,
            o["overhead"] is not JsonValue ov || !ov.TryGetValue<bool>(out var h) || h,
            o["ifc2d"] is not JsonValue iv || !iv.TryGetValue<bool>(out var i) || i,
            o["voids"] is JsonValue vv && vv.TryGetValue<bool>(out var v) && v,
            o["voidSizes"] is not JsonValue sv || !sv.TryGetValue<bool>(out var sz) || sz,
            // A style saved before floor finish hatches (decision 299): on unless it is a structural, MEP or coordination one.
            o["finishHatch"] is JsonValue fv && fv.TryGetValue<bool>(out var fh) ? fh : ArchitecturalByDefault(o),
            // The same for surface hatches (decision 325).
            o["surfaceHatch"] is JsonValue shv && shv.TryGetValue<bool>(out var sh) ? sh : ArchitecturalByDefault(o),
            // A style saved before decision 341 draws the entourage as outlines too: the new simple look.
            (string?)o["entourage"] is { } en && EntourageModes.Contains(en) ? en : "outline",
            FillOf(o["entourageFill"]),
            (string?)o["graphicsSet"] is { Length: > 0 } gs ? gs : null,
            // A style saved before the views' LOD (decision 404): LOD 400 for a structural one (a link's rebar too), else 350.
            PlanDetail.LodOf(o["detail"]) ?? ((string?)o["discipline"] == "Structure" || (string?)o["id"] == "structural" ? PlanDetail.Lod400 : PlanDetail.Lod350),
            // A style saved before the finishes by kind of view (topic #30): every finish drawn, as before.
            On(o["wallFinishesPlan"]), On(o["wallFinishesSection"]), On(o["floorFinishesPlan"]), On(o["floorFinishesSection"]),
            // A style saved before the structure display by kind of view (decision 500): its plans and sections take its layers.
            Mode(o["layersPlan"]), Mode(o["layersSection"]),
            // Whether the separate finish elements go with the finish layers (decision 502): a project made before is marked false (StructureUpgrade).
            On(o["finishElements"]),
            // A style saved before real-size hatches (decision 679): the default threshold (it had no real-size hatch to leave out).
            Spacing(o["minHatchSpacing"]) ?? Lapin.Drawing.Hatch.HatchScale.DefaultMinSpacing,
            // A style saved before the small-scale cuts (decision 680): none, today's look (Î5).
            Lapin.Platform.SmallScaleCut.Read(o["smallScaleCut"]),
            // A style saved before the material graphics sets (decision 682): none.
            (string?)o["materialGraphicsSet"] is { Length: > 0 } mgs ? mgs : null,
            // A style saved before what is seen beyond the cut (decision 721): everything, as before.
            (string?)o["beyond"] is { } bm && BeyondModes.Contains(bm) ? bm : "full",
            Json.Number(o["nearDepth"]) is { } nd && nd >= 0 && double.IsFinite(nd) ? nd : DefaultNearDepth);

        /// <summary>A hatch spacing threshold (paper mm, 0 or more); null when it is not one.</summary>
        internal static double? Spacing(JsonNode? n) => Json.Number(n) is { } v && v >= 0 && double.IsFinite(v) ? v : null;

        static string? Mode(JsonNode? n) => (string?)n is { } l && LayerModes.Contains(l) ? l : null;

        static bool On(JsonNode? n) => n is not JsonValue v || !v.TryGetValue<bool>(out var b) || b;

        /// <summary>Whether a style saved without a newer field takes it on: unless it is a structural, MEP or coordination one.</summary>
        static bool ArchitecturalByDefault(JsonObject o) =>
            !((string?)o["discipline"] is "Structure" or "MEP" || (string?)o["id"] is "structural" or "mep" or "coordination");
    }

    /// <summary>
    /// The styles a project without its own reads as: one per discipline; the architectural one's plans without the finishes
    /// (decision 500: the plasters and floor finishes left out, the insulation drawn), its sections, elevations and 3D views
    /// the entire model; the structural one draws the cores alone.
    /// </summary>
    public static readonly IReadOnlyList<Style> Defaults =
    [
        new("architectural", "Architectural", "Architecture", "all", true, true, LayersPlan: "noFinishes"),
        new("structural", "Structural", "Structure", "core", true, true, FinishHatch: false, SurfaceHatch: false, Detail: PlanDetail.Lod400),
        new("mep", "MEP", "MEP", "all", true, true, FinishHatch: false, SurfaceHatch: false),
        // For coordination drawings (decision 290): the openings' voids drawn, marked and sized; taken by name, no discipline's.
        new("coordination", "Coordination", null, "all", true, true, Voids: true, FinishHatch: false, SurfaceHatch: false),
    ];

    /// <summary>The styles a project made before decision 500 read as when it had none of its own: every view the entire model but the structural ones.</summary>
    public static IReadOnlyList<Style> LegacyDefaults => Defaults.Select(s => s with { LayersPlan = null, LayersSection = null, FinishElements = false }).ToList();

    /// <summary>
    /// The styles a project made since decision 682 reads as when it has none of its own (<see cref="MaterialStandards.NewLook(Entity?)"/>):
    /// <see cref="Defaults"/>, the Structural style naming the material graphics set „Structure”, the MEP style „MEP” (Î10).
    /// </summary>
    public static readonly IReadOnlyList<Style> NewDefaults = Defaults.Select(s => s.Id switch
    {
        "structural" => s with { MaterialGraphicsSet = MaterialGraphicsSets.StructureId },
        "mep" => s with { MaterialGraphicsSet = MaterialGraphicsSets.MepId },
        _ => s,
    }).ToList();

    public static IReadOnlyList<Style> All(IModelView m) =>
        Spatial.Project(m) is var project && project?.Attrs[Key] is JsonArray arr && arr.Count > 0
            ? arr.OfType<JsonObject>().Select(Style.From).Where(s => s.Id.Length > 0).ToList()
            : MaterialStandards.NewLook(project) ? NewDefaults : Defaults;

    /// <summary>The style a view takes: the one it names, else its discipline's, else the first.</summary>
    public static Style StyleOf(IModelView m, Entity view)
    {
        var all = All(m);
        if (view.Str("viewStyle") is { } id && all.FirstOrDefault(s => s.Id == id) is { } named) return named;
        var discipline = view.Str("discipline") ?? "Architecture";
        return all.FirstOrDefault(s => string.Equals(s.Discipline, discipline, StringComparison.OrdinalIgnoreCase)) ?? all[0];
    }

    /// <summary>
    /// How a view draws: its style's settings, with what the view sets itself over them; its partial structure display its
    /// own, else its style's for a view of its kind (<see cref="Style.LayersFor"/>).
    /// </summary>
    public static Style Of(IModelView m, Entity view)
    {
        var st = StyleOf(m, view);
        var s = st with { Layers = st.LayersFor(view.Str("kind")), LayersPlan = null, LayersSection = null };
        var o = view.Attrs["styleOverrides"] as JsonObject;
        if (o == null) return s;
        return s with
        {
            Layers = (string?)o["layers"] is { } l && LayerModes.Contains(l) ? l : s.Layers,
            Joins = o["joins"] is JsonValue jv && jv.TryGetValue<bool>(out var j) ? j : s.Joins,
            Overhead = o["overhead"] is JsonValue ov && ov.TryGetValue<bool>(out var h) ? h : s.Overhead,
            Ifc2d = o["ifc2d"] is JsonValue iv && iv.TryGetValue<bool>(out var i) ? i : s.Ifc2d,
            Voids = o["voids"] is JsonValue vv && vv.TryGetValue<bool>(out var v) ? v : s.Voids,
            VoidSizes = o["voidSizes"] is JsonValue sv && sv.TryGetValue<bool>(out var sz) ? sz : s.VoidSizes,
            FinishHatch = o["finishHatch"] is JsonValue fv && fv.TryGetValue<bool>(out var fh) ? fh : s.FinishHatch,
            SurfaceHatch = o["surfaceHatch"] is JsonValue shv && shv.TryGetValue<bool>(out var sh) ? sh : s.SurfaceHatch,
            Entourage = (string?)o["entourage"] is { } en && EntourageModes.Contains(en) ? en : s.Entourage,
            EntourageFill = o.ContainsKey("entourageFill") ? FillOf(o["entourageFill"]) : s.EntourageFill,
            Detail = PlanDetail.LodOf(o["detail"]) ?? s.Detail,
            WallFinishesPlan = Own(o["wallFinishesPlan"]) ?? s.WallFinishesPlan,
            WallFinishesSection = Own(o["wallFinishesSection"]) ?? s.WallFinishesSection,
            FloorFinishesPlan = Own(o["floorFinishesPlan"]) ?? s.FloorFinishesPlan,
            FloorFinishesSection = Own(o["floorFinishesSection"]) ?? s.FloorFinishesSection,
            MinHatchSpacing = Style.Spacing(o["minHatchSpacing"]) ?? s.MinHatchSpacing,
            // The view's own, field by field over its style's (decision 680).
            SmallScaleCut = o["smallScaleCut"] is JsonObject sc ? SmallScaleCut.Read(sc, s.SmallScaleCut) : s.SmallScaleCut,
            // The view's own set (decision 682), or none ("none") over its style's.
            MaterialGraphicsSet = (string?)o["materialGraphicsSet"] is { Length: > 0 } ms ? (ms == NoSet ? null : ms) : s.MaterialGraphicsSet,
            // What it sees beyond its cut (decision 721), its own over its style's.
            Beyond = (string?)o["beyond"] is { } bm && BeyondModes.Contains(bm) ? bm : s.Beyond,
            NearDepth = Json.Number(o["nearDepth"]) is { } nd && nd >= 0 && double.IsFinite(nd) ? nd : s.NearDepth,
        };
    }

    /// <summary>What a view names over its style's set to draw with none (decision 682).</summary>
    public const string NoSet = "none";

    /// <summary>A command's set (viewStyle.create/update, view.setStyle): one of the project's (its id), or none ("none", "" or null on a style).</summary>
    static string? CheckedSet(IModelView m, JsonNode? v, bool view)
    {
        if (v is null || (string?)v is "" or NoSet) return view ? NoSet : null;
        var id = (string?)v ?? throw new CommandException("materialGraphicsSet is a material graphics set's id, or none.");
        return MaterialGraphicsSets.Find(m, id)?.Id ?? throw new CommandException($"There is no material graphics set „{id}”.");
    }

    const string SpacingProblem = "minHatchSpacing is a distance on paper in mm, 0 or more (0: every real-size hatch is drawn).";

    static bool? Own(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    /// <summary>Whether a view draws a part of a layered element (a part keyed layer:&lt;n&gt;; any other part always draws).</summary>
    public static bool Draws(IModelView m, Style s, string element, string? partKey, Dictionary<string, IReadOnlyList<LayerBand>?> cache) =>
        Draws(m, s.Layers, element, partKey, cache, s.FinishElements);

    /// <summary>
    /// Whether a layers mode (all, noFinishes, core, coreLoadBearing) draws a part of a layered element (a part keyed
    /// layer:&lt;n&gt;; any other part always draws); the cores of the load-bearing elements only draw no part of an element
    /// that bears no load (<see cref="BearsLoad"/>), and the core of one that does.
    /// </summary>
    /// <remarks>
    /// <paramref name="finishElements"/>: whether a separate finish element (<see cref="FinishElementSub"/>: a room's finish, a
    /// skirting, a reference all of one finish) goes with the layers of its role (decision 502); false for a style of a project
    /// made before, which drew them.
    /// </remarks>
    public static bool Draws(IModelView m, string layers, string element, string? partKey, Dictionary<string, IReadOnlyList<LayerBand>?> cache, bool finishElements = true)
    {
        if (layers == "all") return true;
        var el = m.Get(element);
        if (finishElements && el != null && LeavesOut(layers, FinishElementSub(m, el))) return false;
        if (layers == "coreLoadBearing")
        {
            if (el != null && !BearsLoad(m, el)) return false;
            layers = "core";
        }
        if (layers == "all" || partKey is not { } k || !k.StartsWith("layer:", StringComparison.Ordinal)
            || !int.TryParse(k.AsSpan(6), System.Globalization.CultureInfo.InvariantCulture, out var index)) return true;
        if (!cache.TryGetValue(element, out var bands)) cache[element] = bands = LayerSets.DrawnBandsOf(m, element);
        // A reference's layers, from its file's material (decision 400): the same modes.
        if (bands == null && m.Get(element) is { } refEl && ReferenceLayers.Of(m, refEl) is { } rs)
        {
            if (index < 0 || index >= rs.Layers.Count) return true;
            if (layers == "core") return rs.Layers[index].Core || !rs.Layers.Any(l => l.Core);
            return rs.Layers[index].Sub != "finishes";
        }
        if (bands == null || index < 0 || index >= bands.Count) return true;
        var band = bands[index];
        // An element without a marked core keeps all its layers: there is nothing to tell its structure by.
        if (layers == "core") return band.Core || !bands.Any(b => b.Core);
        // A finish by the layer's own category, else its material's (the forum's topic #30).
        return band.Core || !LayerSets.IsFinish(m, band);
    }

    /// <summary>
    /// Whether a 3D view (and what it exports) shows a part by its layers mode: the
    /// layers as a drawing keeps them (<see cref="Draws(IModelView, string, string, string?, Dictionary{string, IReadOnlyList{LayerBand}?})"/>);
    /// and, without the finishes or with the cores only, a door's or window's reveal
    /// lined in a finish (plaster round the opening) goes with the wall's own
    /// finishes, so the opening reads through the layers kept.
    /// </summary>
    public static bool Shows3d(IModelView m, string layers, string element, GeometryPart part, Dictionary<string, IReadOnlyList<LayerBand>?> cache)
    {
        if (layers == "all") return true;
        if (layers == "coreLoadBearing" && m.Get(element) is { } el && !BearsLoad(m, el)) return false;
        if (part.Key is { } k && k.StartsWith("reveal", StringComparison.Ordinal))
            return !(part.Material is { } mid && m.Get(mid) is { } mat && Materials.IsFinish(mat));
        return Draws(m, layers, element, part.Key, cache);
    }

    /// <summary>The layers modes that leave a part out of a 3D view (null: every mode shows it), sent with its render (<see cref="RenderPart.HiddenBy"/>).</summary>
    public static string[]? HiddenBy(IModelView m, string element, GeometryPart part, Dictionary<string, IReadOnlyList<LayerBand>?> cache)
    {
        List<string>? by = null;
        foreach (var mode in LayerModes)
            if (mode != "all" && !Shows3d(m, mode, element, part, cache)) (by ??= []).Add(mode);
        return by?.ToArray();
    }

    /// <summary>A layers mode as a request names it: all (also when absent), noFinishes, core or coreLoadBearing.</summary>
    public static string LayersOf(JsonNode? n) => (string?)n is { } l ? Checked(l) : "all";

    static string Checked(string l) => LayerModes.Contains(l) ? l : throw new CommandException($"„{l}” is not {LayerModesText}.");

    /// <summary>Whether a layers mode leaves out what is of a layer role (finishes; insulation, membranes): without the finishes the finishes, the core modes all three.</summary>
    public static bool LeavesOut(string layers, string? sub) =>
        sub != null && (layers == "noFinishes" ? sub == "finishes" : layers is "core" or "coreLoadBearing");

    /// <summary>The kinds of IfcCovering that are a finish (IFC's CoveringTypeEnum), and those of the other roles.</summary>
    static string? CoveringSub(string? predefined) => predefined switch
    {
        "FLOORING" or "CLADDING" or "CEILING" or "MOLDING" or "SKIRTINGBOARD" => "finishes",
        "INSULATION" => "insulation",
        "MEMBRANE" => "membranes",
        _ => null,
    };

    /// <summary>
    /// The layer role a whole element is of, so the partial structure display leaves it out with the layers of that role
    /// (decision 502): a room's finish (its floor, walls and ceiling) and a skirting are finishes; a reference all of one
    /// material by that material (<see cref="ReferenceLayers.WholeSub"/>: a plaster lining, a wool layer brought as a wall); a
    /// covering by its IFC kind (a flooring, a cladding, a ceiling, a moulding, a skirting board a finish, an insulation, a
    /// membrane; a roofing goes with its roof). Null for any other element.
    /// </summary>
    public static string? FinishElementSub(IModelView m, Entity e)
    {
        if (e.Class is "LapinRoomFinish" or "LapinSkirting" or "LapinFinishZone") return "finishes";
        if (ReferenceLayers.WholeSub(m, e) is { } whole) return whole;
        return e.Class == "IfcCovering" ? CoveringSub(e.PredefinedType ?? m.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.PredefinedType) : null;
    }

    /// <summary>The classes whose elements bear load or not (IFC's Pset_…Common LoadBearing), which the cores of the load-bearing elements only leave out when they do not.</summary>
    static readonly HashSet<string> Bearing = [IfcClass.Wall, "IfcWallStandardCase", "IfcCurtainWall", IfcClass.Slab, IfcClass.Roof, IfcClass.Column, IfcClass.Beam, IfcClass.Footing, "IfcPile", "IfcMember", "IfcPlate", "IfcCovering", "LapinRoomFinish", "LapinFinishZone"];

    /// <summary>Those taken as bearing load when nothing says: columns, beams, footings and piles (as the IFC export writes them), floors and roofs.</summary>
    static readonly HashSet<string> BearingUnlessSaid = [IfcClass.Slab, IfcClass.Roof, IfcClass.Column, IfcClass.Beam, IfcClass.Footing, "IfcPile"];

    /// <summary>
    /// Whether the cores of the load-bearing elements only (decision 500) keep an element: one of a class that bears load or
    /// not by IFC's LoadBearing (walls, floors, roofs, columns, beams, members, plates, footings, piles, coverings, a room's
    /// finishes) when it or its type says it bears load (<see cref="Coordination.Provisions.LoadBearing"/>: Pset_WallCommon.LoadBearing
    /// for a wall); when neither says, a wall bears load unless IFC calls it or its type a partition (PARTITIONING; decision
    /// 502: so the preset is of use before every wall is marked), and so do a column, beam, footing, pile, floor or roof; a
    /// covering or a room's finish never. Any other element (a door, a stair, furniture) is kept.
    /// The analytical model keeps its own rule (<see cref="Coordination.Provisions.LoadBearing"/>: a wall bears load only when
    /// it says so; the others are partitions, whose weight it lays on the floors).
    /// </summary>
    public static bool BearsLoad(IModelView m, Entity e)
    {
        if (!Bearing.Contains(e.Class)) return true;
        if (e.Class is "IfcCovering" or "LapinRoomFinish" or "LapinFinishZone") return false;
        if (Coordination.Provisions.LoadBearingOrNull(m, e) is { } said) return said;
        if (e.Class is IfcClass.Wall or "IfcWallStandardCase")
            return !(e.PredefinedType == "PARTITIONING" || m.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.PredefinedType == "PARTITIONING");
        return BearingUnlessSaid.Contains(e.Class);
    }

    static JsonArray Write(IEnumerable<Style> styles) => new(styles.Select(s => (JsonNode)s.ToJson()).ToArray());

    static void Save(ICommandContext ctx, IEnumerable<Style> styles)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var pa = project.CloneAttrs();
        pa[Key] = Write(styles);
        ctx.Replace(project.With(attrs: pa));
    }

    static Style Patch(Style s, JsonObject a)
    {
        if (((string?)a["name"])?.Trim() is { Length: > 0 } name) s = s with { Name = name };
        if (a.ContainsKey("discipline")) s = s with { Discipline = ((string?)a["discipline"])?.Trim() is { Length: > 0 } d ? d : null };
        if ((string?)a["layers"] is { } l) s = s with { Layers = Checked(l) };
        // Its plans' and its sections' own (null: its layers).
        if (a.ContainsKey("layersPlan")) s = s with { LayersPlan = (string?)a["layersPlan"] is { } lp ? Checked(lp) : null };
        if (a.ContainsKey("layersSection")) s = s with { LayersSection = (string?)a["layersSection"] is { } ls ? Checked(ls) : null };
        if (a["joins"] is JsonValue jv && jv.TryGetValue<bool>(out var j)) s = s with { Joins = j };
        if (a["overhead"] is JsonValue ov && ov.TryGetValue<bool>(out var h)) s = s with { Overhead = h };
        if (a["ifc2d"] is JsonValue iv && iv.TryGetValue<bool>(out var i)) s = s with { Ifc2d = i };
        if (a["voids"] is JsonValue vv && vv.TryGetValue<bool>(out var v)) s = s with { Voids = v };
        if (a["voidSizes"] is JsonValue sv && sv.TryGetValue<bool>(out var sz)) s = s with { VoidSizes = sz };
        if (a["finishHatch"] is JsonValue fv && fv.TryGetValue<bool>(out var fh)) s = s with { FinishHatch = fh };
        if (a["surfaceHatch"] is JsonValue shv && shv.TryGetValue<bool>(out var sh)) s = s with { SurfaceHatch = sh };
        if (a.ContainsKey("entourage")) s = s with { Entourage = EntourageMode(a["entourage"]) };
        if (a.ContainsKey("entourageFill")) s = s with { EntourageFill = CheckedFill(a["entourageFill"]) };
        if (a.ContainsKey("detail")) s = s with { Detail = PlanDetail.LodOf(a["detail"]) ?? throw new CommandException(PlanDetail.LodProblem) };
        if (a.ContainsKey("graphicsSet")) s = s with { GraphicsSet = (string?)a["graphicsSet"] is { Length: > 0 } gs ? gs : null };
        if (a.ContainsKey("minHatchSpacing")) s = s with { MinHatchSpacing = Style.Spacing(a["minHatchSpacing"]) ?? throw new CommandException(SpacingProblem) };
        // The small-scale cuts (decision 680): fields over the style's, null back to none.
        if (a.ContainsKey("smallScaleCut")) s = s with { SmallScaleCut = a["smallScaleCut"] is null ? null : SmallScaleCut.Read(SmallScaleCut.Checked(a["smallScaleCut"]), s.SmallScaleCut) };
        if (a.ContainsKey("materialGraphicsSet")) s = s with { MaterialGraphicsSet = (string?)a["materialGraphicsSet"] is { Length: > 0 } ms && ms != NoSet ? ms : null };
        if (a.ContainsKey("beyond")) s = s with { Beyond = a["beyond"] is null ? "full" : BeyondMode(a["beyond"]) };
        if (a.ContainsKey("nearDepth")) s = s with { NearDepth = a["nearDepth"] is null ? DefaultNearDepth : NearDepthOf(a["nearDepth"]) };
        if (a.ContainsKey("finishElements")) s = s with { FinishElements = Own(a["finishElements"]) ?? throw new CommandException("finishElements is true or false.") };
        foreach (var f in FinishFields)
        {
            if (!a.ContainsKey(f)) continue;
            var on = Own(a[f]) ?? throw new CommandException($"{f} is true or false.");
            s = f switch
            {
                "wallFinishesPlan" => s with { WallFinishesPlan = on },
                "wallFinishesSection" => s with { WallFinishesSection = on },
                "floorFinishesPlan" => s with { FloorFinishesPlan = on },
                _ => s with { FloorFinishesSection = on },
            };
        }
        return s;
    }

    /// <summary>The project's mark that its views' structure display is read as decisions 500 and 502 have it (a project made before is upgraded).</summary>
    public const string StructureRevKey = "structureRev";

    /// <summary>The mark's value now: 1 decision 500's display by kind of view, 2 the finish elements going with the finish layers (decision 502).</summary>
    public const int StructureRev = 2;

    /// <summary>
    /// A project made before the structure display by kind of view (decision 500) keeps what its views drew: one without
    /// styles of its own reads the styles it read then (<see cref="LegacyDefaults"/>: its plans the entire model), written
    /// on it; one with its own keeps them as they are (a style without <c>layersPlan</c> or <c>layersSection</c> takes its
    /// <c>layers</c> for every view, as before), and decision 496's finish choices by kind of view stay as they were, the
    /// advanced detail of the structure display. A project made before the finish elements went with the finish layers
    /// (decision 502, a mark under 2) keeps drawing them: its styles say <c>finishElements</c> false (one without styles of
    /// its own gets the defaults it read, so marked). Then the project is marked.
    /// </summary>
    public sealed class StructureUpgrade : IProjectUpgrade
    {
        public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
        {
            if (entities.FirstOrDefault(e => e.Class == IfcClass.Project) is not { } project) return (entities, relations);
            int? rev = Json.Number(project.Attrs[StructureRevKey]) is { } r ? (int)Math.Round(r) : null;
            if (rev >= StructureRev) return (entities, relations);
            var attrs = project.CloneAttrs();
            if (rev == null && attrs[Key] is not JsonArray { Count: > 0 }) attrs[Key] = Write(LegacyDefaults);
            if (attrs[Key] is not JsonArray { Count: > 0 } own) attrs[Key] = Write(Defaults.Select(s => s with { FinishElements = false }));
            else foreach (var o in own.OfType<JsonObject>()) o["finishElements"] = false;
            attrs[StructureRevKey] = StructureRev;
            var up = project.With(attrs: attrs);
            return (entities.Select(e => e.Id == project.Id ? up : e).ToList(), relations);
        }
    }

    /// <summary>A material graphics set removed (decision 682): the styles that named it name none, the views that named it take their style's.</summary>
    internal static void ForgetMaterialSet(ICommandContext ctx, string setId)
    {
        var all = All(ctx.Model).ToList();
        bool Is(string? id) => string.Equals(id, setId, StringComparison.OrdinalIgnoreCase);
        if (all.Any(s => Is(s.MaterialGraphicsSet))) Save(ctx, all.Select(s => Is(s.MaterialGraphicsSet) ? s with { MaterialGraphicsSet = null } : s));
        foreach (var v in ctx.Model.OfClass(Views.Class).Where(v => Is((string?)v.Attrs["styleOverrides"]?["materialGraphicsSet"])).ToList())
        {
            var attrs = v.CloneAttrs();
            var own = (JsonObject)attrs["styleOverrides"]!.DeepClone();
            own.Remove("materialGraphicsSet");
            if (own.Count > 0) attrs["styleOverrides"] = own; else attrs.Remove("styleOverrides");
            ctx.Replace(v.With(attrs: attrs));
        }
    }

    /// <summary>A graphics set removed: the styles that named it name none.</summary>
    internal static void ForgetSet(ICommandContext ctx, string setId)
    {
        var all = All(ctx.Model).ToList();
        if (all.Any(s => s.GraphicsSet == setId)) Save(ctx, all.Select(s => s.GraphicsSet == setId ? s with { GraphicsSet = null } : s));
    }

    public static void Register(IModuleRegistry r)
    {
        r.Query("viewStyle.list", (q, _) => Write(All(q.Model)));

        // A view's settings as it draws, and where each comes from (its style, or itself).
        r.Query("view.style", (q, a) =>
        {
            if (Views.Find(q.Model, (string?)a["id"] ?? "") is not { } v) return null;
            var style = StyleOf(q.Model, v);
            var own = v.Attrs["styleOverrides"] as JsonObject ?? [];
            return new JsonObject
            {
                ["style"] = style.ToJson(), ["named"] = v.Str("viewStyle") != null,
                ["resolved"] = Of(q.Model, v).ToJson(),
                ["overrides"] = new JsonArray(Fields.Where(own.ContainsKey).Select(f => (JsonNode)f).ToArray()),
            };
        });

        // A new style: a copy of one (from), else of the first; its name.
        r.Command("viewStyle.create", "New view style", (ctx, a) =>
        {
            var all = All(ctx.Model).ToList();
            var source = (string?)a["from"] is { } fid ? all.FirstOrDefault(s => s.Id == fid) ?? throw new CommandException("There is no such view style.") : all[0];
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : $"{source.Name} copy";
            for (var k = 2; all.Any(x => x.Name == name); k++) name = $"{source.Name} copy {k}";
            if (a.ContainsKey("materialGraphicsSet")) { a = (JsonObject)a.DeepClone(); a["materialGraphicsSet"] = CheckedSet(ctx.Model, a["materialGraphicsSet"], false); }
            var made = Patch(source with { Id = ctx.NewId(), Name = name }, a);
            all.Add(made);
            Save(ctx, all);
            return new JsonObject { ["id"] = made.Id };
        });

        r.Command("viewStyle.update", "Change view style", (ctx, a) =>
        {
            var all = All(ctx.Model).ToList();
            var i = all.FindIndex(s => s.Id == (string?)a["id"]);
            if (i < 0) throw new CommandException("There is no such view style.");
            if (a.ContainsKey("materialGraphicsSet")) { a = (JsonObject)a.DeepClone(); a["materialGraphicsSet"] = CheckedSet(ctx.Model, a["materialGraphicsSet"], false); }
            all[i] = Patch(all[i], a);
            Save(ctx, all);
            return null;
        });

        // A style gone: its views take their discipline's again.
        r.Command("viewStyle.delete", "Delete view style", (ctx, a) =>
        {
            var all = All(ctx.Model).ToList();
            var id = (string?)a["id"];
            if (all.All(s => s.Id != id)) throw new CommandException("There is no such view style.");
            if (all.Count == 1) throw new CommandException("A project keeps at least one view style.");
            Save(ctx, all.Where(s => s.Id != id));
            foreach (var v in ctx.Model.OfClass(Views.Class).Where(v => v.Str("viewStyle") == id).ToList())
            {
                var attrs = v.CloneAttrs();
                attrs.Remove("viewStyle");
                ctx.Replace(v.With(attrs: attrs));
            }
            return null;
        });

        // A view's style (style: an id; null: its discipline's) and its own settings over it (a field: its value; null: the style's again).
        r.Command("view.setStyle", "View style", (ctx, a) =>
        {
            // {3D} ("3d") keeps its settings on the project, as its storeys shown and its graphics.
            var (v, write) = GraphicsSets.Target(ctx, (string?)a["id"]);
            var attrs = v.CloneAttrs();
            if (v.Id == GraphicsSets.View3d) attrs.Remove("kind");
            // {3D}'s discipline (a saved view's is its attribute, set as any other).
            if (a.ContainsKey("discipline"))
            {
                if (((string?)a["discipline"])?.Trim() is { Length: > 0 } d) attrs["discipline"] = d; else attrs.Remove("discipline");
            }
            if (a.ContainsKey("style"))
            {
                if ((string?)a["style"] is { Length: > 0 } sid)
                    attrs["viewStyle"] = All(ctx.Model).Any(s => s.Id == sid) ? sid : throw new CommandException("There is no such view style.");
                else attrs.Remove("viewStyle");
            }
            var own = attrs["styleOverrides"]?.DeepClone() as JsonObject ?? [];
            foreach (var f in Fields)
            {
                if (!a.ContainsKey(f)) continue;
                var value = a[f];
                if (value == null) { own.Remove(f); continue; }
                if (f == "layers") own[f] = (string?)value is { } l ? Checked(l) : throw new CommandException($"„{value}” is not {LayerModesText}.");
                else if (f == "entourage") own[f] = EntourageMode(value);
                // A fill set to none on the view, over a style's colour: kept as "none".
                else if (f == "entourageFill") own[f] = CheckedFill(value) ?? "none";
                // The view's level of development (decision 404): 100, 200, 300, 350 or 400.
                else if (f == "detail") own[f] = PlanDetail.LodOf(value) ?? throw new CommandException(PlanDetail.LodProblem);
                // The real-size hatches' threshold (decision 679), paper mm.
                else if (f == "minHatchSpacing") own[f] = Style.Spacing(value) ?? throw new CommandException(SpacingProblem);
                // The small-scale cuts (decision 680): the fields given, over the style's (mode, from, color).
                else if (f == "smallScaleCut") own[f] = SmallScaleCut.Checked(value);
                // The material graphics set (decision 682): one of the project's, or "none" over the style's.
                else if (f == "materialGraphicsSet") own[f] = CheckedSet(ctx.Model, value, true);
                // What a section or an elevation sees beyond its cut (decision 721): full, near or cut; the near part's depth.
                else if (f == "beyond") own[f] = BeyondMode(value);
                else if (f == "nearDepth") own[f] = NearDepthOf(value);
                else own[f] = value is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : throw new CommandException($"{f} is true or false.");
            }
            if (own.Count > 0) attrs["styleOverrides"] = own; else attrs.Remove("styleOverrides");
            write(attrs);
            return null;
        });
    }
}
