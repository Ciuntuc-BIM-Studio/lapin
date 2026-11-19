using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// How a view's cuts draw from a scale on (decision 680, Revit's coarse scale fill with a scale threshold instead of a LOD):
/// <list type="bullet">
/// <item><c>asMaterial</c>: as the materials say (the default, today's look; Î5);</item>
/// <item><c>solid</c>: each cut element one area filled in <see cref="Color"/> (else the category's fill colour, else ink), no
/// hatch, no lines between its layers (its bands merged as a coarse view merges them, membranes inside it filled);</item>
/// <item><c>outline</c>: no hatch, no fill, the element's outline alone (its layers merged; elements of one type joined);</item>
/// <item><c>coreOnly</c>: the core layers filled solid, the other layers empty, every line kept (membranes too).</item>
/// </list>
/// It holds from 1:<see cref="From"/> on (a scale denominator: from 200, 1:200 and 1:500, not 1:100). Only the cut bands of
/// walls, floors, roofs, columns, beams, members, plates, footings and piles take it (a window's glass keeps its look).
/// </summary>
public sealed record SmallScaleCut(string Mode = SmallScaleCut.AsMaterial, double From = SmallScaleCut.DefaultFrom, string? Color = null)
{
    public const string AsMaterial = "asMaterial", Solid = "solid", Outline = "outline", CoreOnly = "coreOnly";
    public static readonly string[] Modes = [AsMaterial, Solid, Outline, CoreOnly];
    /// <summary>The threshold a mode set without one takes: 1:200.</summary>
    public const double DefaultFrom = 200;
    static readonly HashSet<string> Fields = ["mode", "from", "color"];

    /// <summary>Whether it changes a view at 1:<paramref name="scale"/>.</summary>
    public bool Holds(double scale) => Mode != AsMaterial && scale >= From - 1e-9;

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["mode"] = Mode, ["from"] = From };
        if (Color != null) o["color"] = Color;
        return o;
    }

    /// <summary>One as saved, its fields over <paramref name="under"/>'s (a view's over its style's); null: none (a field it gives wrong is left out).</summary>
    public static SmallScaleCut? Read(JsonNode? n, SmallScaleCut? under = null)
    {
        if (n is not JsonObject o) return under;
        var b = under ?? new SmallScaleCut();
        var mode = (string?)o["mode"] is { } md && Modes.Contains(md) ? md : b.Mode;
        var from = Json.Number(o["from"]) is { } f && f >= 1 && double.IsFinite(f) ? f : b.From;
        var color = o.ContainsKey("color") ? ((string?)o["color"] is { } c && HexColor.IsColour(c) ? c.ToLowerInvariant() : null) : b.Color;
        return new SmallScaleCut(mode, from, color);
    }

    /// <summary>A command's value checked (view.setStyle, viewStyle.update): {mode?, from?, color?}; color null for the category's.</summary>
    public static JsonObject Checked(JsonNode? v)
    {
        if (v is not JsonObject o) throw new CommandException("smallScaleCut is {mode: asMaterial | solid | outline | coreOnly, from: a scale (200 for 1:200), color?: #rrggbb}.");
        var r = new JsonObject();
        foreach (var (k, x) in o)
        {
            if (!Fields.Contains(k)) throw new CommandException($"„{k}” is not a field of smallScaleCut (mode, from, color).");
            switch (k)
            {
                case "mode":
                    r[k] = (string?)x is { } m && Modes.Contains(m) ? m : throw new CommandException($"smallScaleCut's mode is {string.Join(", ", Modes)}.");
                    break;
                case "from":
                    r[k] = Json.Number(x) is { } f && f >= 1 && f <= 100000 && double.IsFinite(f) ? f : throw new CommandException("smallScaleCut's from is a scale denominator, 1 to 100000 (200 for 1:200).");
                    break;
                default:
                    r[k] = x is null ? null : (string?)x is { } c && HexColor.IsColour(c) ? c.ToLowerInvariant() : throw new CommandException("smallScaleCut's color is a colour (#rrggbb), or null for the category's.");
                    break;
            }
        }
        return r;
    }

    /// <summary>The classes whose cuts it changes.</summary>
    public static bool Takes(string ifcClass) => ifcClass.ToUpperInvariant() switch
    {
        "IFCWALL" or "IFCWALLSTANDARDCASE" or "IFCWALLELEMENTEDCASE" or "IFCSLAB" or "IFCSLABSTANDARDCASE" or "IFCROOF" or "IFCCOLUMN" or "IFCCOLUMNSTANDARDCASE"
            or "IFCBEAM" or "IFCBEAMSTANDARDCASE" or "IFCMEMBER" or "IFCMEMBERSTANDARDCASE" or "IFCPLATE" or "IFCFOOTING" or "IFCPILE" or "IFCCHIMNEY" => true,
        _ => false,
    };
}

/// <summary>
/// The one place a view decides how a material draws in it (decision 680). Precedence, the higher over the lower, field by field:
/// <list type="number">
/// <item>an element's own override in the view (decision 718, <see cref="ElementGraphics"/>): its cut hatch (a pattern or none, its
/// scale and angle), cut fill, hatch colour and halftone, cut line; an elevation's seen faces their surface hatch and fill;</item>
/// <item>the view's filters (stage 5 of docs/plan-material-graphics.md): the first filter in the view's list that takes the element
/// and gives a field gives it (<see cref="VisibilityGraphics.FilterGraphics"/>): a cut hatch, a cut fill, a hatch colour, the hatch
/// halftone, the cut outline; an elevation's seen faces their surface hatch and fill;</item>
/// <item>(2.5) the view's Visibility / Graphics subcategory graphics (decision 682): a layer's subcategory (core, finishes,
/// insulation, membranes, other layers) giving a cut hatch, a fill colour, a line colour and weight or halftone — the view's
/// own overrides, so over its style's set, and over its category's cut hatch and fill colour, being the more particular;</item>
/// <item>the material graphics set of the view (its own, else its style's; decision 682, <see cref="MaterialGraphicsSets"/>):
/// its entry for the material, at the view's scale;</item>
/// <item>the view style's small-scale cut (<see cref="SmallScaleCut"/>, cut bands only);</item>
/// <item>the material's step for the view's scale (<see cref="ScaleStep"/>);</item>
/// <item>the material's own graphics;</item>
/// <item>the project's material standard (<see cref="MaterialStandards"/>, decision 681): its rule for the material's name or category.</item>
/// </list>
/// The last three are <see cref="MaterialGraphics.Resolve"/>; this lays the view's tiers over them. Visibility / Graphics' category
/// fill colour and cut hatch still lay themselves over all of it when the pens are drawn (<see cref="Pens.Of"/>), as before, but
/// for a band whose subcategory gives its own (<see cref="ResolvedMaterial.KeepsHatch"/>, <see cref="ResolvedMaterial.KeepsFill"/>).
/// Plans, sections, cut elevations, sheets (each viewport at its view's scale), PDF and DXF draw through
/// <see cref="Views.Render"/>; elevations' surfaces through <see cref="SurfaceHatches"/>; legends at their view's scale.
/// </summary>
public sealed class ViewMaterialGraphics
{
    readonly IModelView _m;
    readonly string _kind;
    readonly double? _scale;
    readonly bool _paper;
    readonly MaterialStandard _standard;
    readonly SmallScaleCut? _small;
    readonly MaterialGraphicsSets.Set? _set;
    readonly VisibilityGraphics? _vg;
    readonly Dictionary<string, ResolvedMaterial?> _base = [];
    readonly Dictionary<string, ResolvedMaterial?> _materials = [];
    readonly Dictionary<string, IReadOnlyList<LayerBand>?> _bands = [];
    readonly Dictionary<string, string> _outlineKeys = [];

    /// <param name="kind">section, plan or elevation (the material's kind of view).</param>
    /// <param name="scale">The view's scale denominator; null: no step by scale (an export with no scale).</param>
    /// <param name="style">The view's style as it draws (<see cref="ViewStyles.Of"/>); null: none of its tiers.</param>
    /// <param name="visibility">The view's Visibility / Graphics, whose subcategories' graphics lay over the bands (decision 682); null: none.</param>
    /// <param name="smallScale">Whether the style's small-scale cut holds (a legend shows a material without it, decision 680).</param>
    public ViewMaterialGraphics(IModelView m, string kind, double? scale, ViewStyles.Style? style = null, VisibilityGraphics? visibility = null, bool smallScale = true)
    {
        _m = m;
        _kind = kind;
        _scale = scale;
        _paper = MaterialHatchUpgrade.PaperSurfaces(m);
        // The project's material standard (decision 681): tier 7, what a material does not set itself.
        _standard = MaterialStandards.Of(m);
        // Cuts only: an elevation's surfaces keep their materials' look.
        _small = smallScale && kind != "elevation" && scale is { } s && style?.SmallScaleCut is { } sc && sc.Holds(s) ? sc : null;
        // The view's material graphics set (decision 682): tier 3, over the small-scale cut.
        _set = style?.MaterialGraphicsSet is { } setId ? MaterialGraphicsSets.Find(m, setId) : null;
        _vg = visibility is { HasSubGraphics: true } or { HasFilterGraphics: true } or { HasElementGraphics: true } ? visibility : null;
    }

    public static ViewMaterialGraphics For(IModelView m, Entity view, string kind, double scale) => new(m, kind, scale, ViewStyles.Of(m, view), VisibilityGraphics.Of(m, view));

    /// <summary>The material graphics set the view draws with (null: none).</summary>
    public MaterialGraphicsSets.Set? Set => _set;

    /// <summary>The small-scale cut that holds in the view (null: none, or asMaterial).</summary>
    public SmallScaleCut? SmallScale => _small;

    /// <summary>Whether an element's bands draw as one area (solid, outline), as a coarse view's.</summary>
    public bool MergesBands => _small?.Mode is SmallScaleCut.Solid or SmallScaleCut.Outline;

    /// <summary>
    /// A material in the view (an id the project keeps, or a name its look is guessed from): its tiers up to its step by scale.
    /// What the view's cut bands draw with is <see cref="Band"/>.
    /// </summary>
    public ResolvedMaterial? Material(string? id)
    {
        if (id == null) return null;
        if (_materials.TryGetValue(id, out var r)) return r;
        return _materials[id] = SetOver(Base(id), id);
    }

    /// <summary>A material's own tiers: the standard, the material, their steps by scale.</summary>
    ResolvedMaterial? Base(string id)
    {
        if (_base.TryGetValue(id, out var r)) return r;
        // A material named rather than kept (a track's ballast, its rails): its look guessed from the name.
        return _base[id] = _m.Get(id) is { } mat ? MaterialGraphics.Resolve(mat, _kind, paperSurfaces: _paper, scale: _scale, standard: _standard)
            : GlobalId.IsValid(id) ? null : MaterialGraphics.Resolve(null, _kind, id, _paper, _scale, _standard);
    }

    /// <summary>The set's entry for a material laid over its graphics (tier 3); unchanged without a set, an entry, or graphics.</summary>
    ResolvedMaterial? SetOver(ResolvedMaterial? g, string? id)
    {
        if (_set == null || g == null || id == null) return g;
        var mat = _m.Get(id) is { Class: IfcClass.Material } e ? e : null;
        var name = mat?.Name ?? (GlobalId.IsValid(id) ? null : id);
        if (name == null || _set.For(mat?.Id, name, MaterialGraphicsSets.CategoryOf(mat, name, _standard)) is not { } entry || entry.For(_kind, _scale) is not { } eg) return g;
        return MaterialGraphics.Over(g, eg) with { GraphicsSet = _set.Id };
    }

    /// <summary>A cut band's graphics in the view: every tier, in the order of the precedence (the lowest first).</summary>
    public ResolvedMaterial? Band(DPolygon p)
    {
        ResolvedMaterial? g;
        // The small-scale cut (tier 4) under the set (tier 3).
        if (_small != null && SmallScaleCut.Takes(p.Class)) g = SetOver(SmallScaled(p.Material == null ? null : Base(p.Material), p, _small), p.Material);
        else g = Material(p.Material);
        // Tier 2.5: the view's subcategory graphics. Tier 2: the view's filters (stage 5). Then an element's own override.
        if (_vg != null && _m.Get(p.Element) is { } el)
        {
            if (g != null && _vg.SubGraphics(el, p.Key) is { } sub) g = SubOver(g, sub.Sub, sub.Category);
            if (_vg.FilterGraphics(el) is { } fl) g = FilterOver(g, fl, p.Class, cut: true);
            // Tier 1: the element's own override in this view (decision 718).
            if (_vg.ElementRow(el.Id) is { } own) g = ElementOver(g, own, p.Class, cut: true);
        }
        return g;
    }

    /// <summary>
    /// The graphics an elevation's seen face of an element draws with: its material's (<see cref="Material"/>) with the view's
    /// filters over them (stage 5: their surface hatch and fill, hatch colour and halftone); null: no surface drawn.
    /// </summary>
    public ResolvedMaterial? Surface(string element, string cls, ResolvedMaterial? g)
    {
        if (_vg == null || _m.Get(element) is not { } el) return g;
        if (_vg.FilterGraphics(el) is { } fl) g = FilterOver(g, fl, cls, cut: false);
        if (_vg.ElementRow(el.Id) is { } own) g = ElementOver(g, own, cls, cut: false);
        return g;
    }

    /// <summary>
    /// A band (or, <paramref name="cut"/> false, a seen face) with the element's own override in the view over it (decision 718,
    /// <see cref="ElementGraphics"/>): the pattern ("none": none) at its scale (<c>cutHatchScale</c>, 1 the pattern's own) and
    /// angle; or, without a pattern, the material's pattern at that scale and angle; the fill ("none": none); the hatch colour and
    /// halftone; the cut outline. A band with no material takes what the row draws; a membrane keeps its line.
    /// Bands of one material join only with bands the same override draws alike.
    /// </summary>
    static ResolvedMaterial? ElementOver(ResolvedMaterial? g, JsonObject r, string cls, bool cut)
    {
        static string? Colour(JsonNode? n) => (string?)n is { } c && HexColor.IsColour(c) ? c.ToLowerInvariant() : null;
        static bool None(JsonNode? n) => (string?)n is "" or "none" or null;
        var (hatchKey, fillKey, scaleKey, angleKey) = cut ? ("cutHatch", "cutFill", "cutHatchScale", "cutHatchAngle") : ("surfaceHatch", "surfaceFill", "surfaceHatchScale", "surfaceHatchAngle");
        bool Gives(string k) => r.ContainsKey(k);
        var line = cut ? MaterialLine.From(r["cutLine"]) : null;
        if (!(Gives(hatchKey) || Gives(fillKey) || Gives(scaleKey) || Gives(angleKey) || Gives("hatchColor") || Gives("hatchHalftone") || line != null)) return g;
        if (g?.Membrane == true) return g;
        if (g == null)
        {
            if (!(Gives(hatchKey) && !None(r[hatchKey]) || Gives(fillKey) && !None(r[fillKey]) || line != null)) return null;
            g = new ResolvedMaterial("", null, 1, null, null, false, false, null, "element") { JoinAs = "\u0001class:" + cls };
        }
        var o = g;
        var scale = Json.Number(r[scaleKey]) is { } sc && sc > 0 && double.IsFinite(sc) ? sc : (double?)null;
        var angle = Json.Number(r[angleKey]) is { } an && double.IsFinite(an) ? an : (double?)null;
        if (Gives(hatchKey))
            o = o with { Hatch = None(r[hatchKey]) ? null : ((string)r[hatchKey]!).ToUpperInvariant(), HatchScale = scale ?? 1, HatchAngle = angle ?? 0, HatchUnits = null, Background = null, KeepsHatch = true };
        else if (scale != null || angle != null)
            o = o with { HatchScale = scale ?? o.HatchScale, HatchAngle = angle ?? o.HatchAngle, KeepsHatch = o.Hatch != null || o.KeepsHatch };
        if (Gives(fillKey)) o = o with { Fill = None(r[fillKey]) ? null : Colour(r[fillKey]), FillOpacity = null, KeepsFill = true, FilterFill = true };
        if (Colour(r["hatchColor"]) is { } hc) o = o with { HatchColor = hc };
        if (r["hatchHalftone"] is JsonValue hv && hv.TryGetValue<bool>(out var half)) o = o with { Halftone = half };
        if (line != null) o = o with { CutLine = o.CutLine is { } had ? had.Over(line) : line };
        return o with { JoinAs = (o.JoinAs ?? MaterialGraphics.Normalise(o.Name)) + "\u0001element:" + r.ToJsonString() };
    }

    /// <summary>
    /// A band (or, <paramref name="cut"/> false, a seen face) with the view's filters' hatch and fill over it (stage 5): the pattern
    /// ("none": none) at its own scale, the fill ("none": none; a filter's line colour no longer pales over it), the hatch colour,
    /// the hatch halftone, the cut outline's colour and weight. A band with no material takes them too (a wall with none filled
    /// black as load-bearing); a membrane keeps its line. Bands of one material join only when the filters draw them alike.
    /// </summary>
    static ResolvedMaterial? FilterOver(ResolvedMaterial? g, VisibilityGraphics.FilterLook fl, string cls, bool cut)
    {
        static string? Colour(JsonNode? n) => (string?)n is { } c && HexColor.IsColour(c) ? c.ToLowerInvariant() : null;
        static bool None(JsonNode? n) => (string?)n is "" or "none" or null;
        var r = fl.Row;
        var (hatchKey, fillKey) = cut ? ("cutHatch", "cutFill") : ("surfaceHatch", "surfaceFill");
        var gives = fl.Gives(hatchKey) || fl.Gives(fillKey) || fl.Gives("hatchColor") || fl.Gives("hatchHalftone") || cut && fl.Gives("cutLine");
        if (!gives || g?.Membrane == true) return g;
        // A band with no material: only what the filter itself draws.
        if (g == null)
        {
            if (!(fl.Gives(hatchKey) && !None(r[hatchKey]) || fl.Gives(fillKey) && !None(r[fillKey]) || cut && fl.Gives("cutLine"))) return null;
            g = new ResolvedMaterial("", null, 1, null, null, false, false, null, "filter") { JoinAs = "\u0001class:" + cls };
        }
        var o = g;
        if (fl.Gives(hatchKey))
            o = o with { Hatch = None(r[hatchKey]) ? null : ((string)r[hatchKey]!).ToUpperInvariant(), HatchScale = 1, HatchAngle = 0, HatchUnits = null, Background = null, KeepsHatch = true };
        if (fl.Gives(fillKey)) o = o with { Fill = None(r[fillKey]) ? null : Colour(r[fillKey]), FillOpacity = null, KeepsFill = true, FilterFill = true };
        if (Colour(r["hatchColor"]) is { } hc) o = o with { HatchColor = hc };
        if (r["hatchHalftone"] is JsonValue hv && hv.TryGetValue<bool>(out var half)) o = o with { Halftone = half };
        if (cut && MaterialLine.From(r["cutLine"]) is { } line) o = o with { CutLine = o.CutLine is { } had ? had.Over(line) : line };
        return o with { Filters = fl.Names, JoinAs = (o.JoinAs ?? MaterialGraphics.Normalise(o.Name)) + "\u0001filter:" + fl.Key };
    }

    /// <summary>
    /// A band with its subcategory's graphics over it (decision 682): its cut hatch (a pattern; "" or "none": none) at its pattern's
    /// own scale, its fill colour ("" or "none": none), its lines' colour and weight (an ISO pen: the cut outline that weight, the
    /// lines between layers two pens lighter, as a category's), and halftone: the hatch, the fill (the subcategory's, else the
    /// category's, else the band's) and the lines washed halfway to white.
    /// </summary>
    static ResolvedMaterial SubOver(ResolvedMaterial g, JsonObject row, JsonObject? category)
    {
        static string? Colour(JsonNode? n) => (string?)n is { } c && HexColor.IsColour(c) ? c.ToLowerInvariant() : null;
        static bool None(JsonNode? n) => (string?)n is "" or "none" or null;
        var o = g;
        if (row.ContainsKey("cutHatch") && !g.Membrane)
            o = o with { Hatch = None(row["cutHatch"]) ? null : ((string)row["cutHatch"]!).ToUpperInvariant(), HatchScale = 1, HatchAngle = 0, HatchUnits = null, Background = null, KeepsHatch = true };
        if (row.ContainsKey("fillColor")) o = o with { Fill = None(row["fillColor"]) ? null : Colour(row["fillColor"]), FillOpacity = null, KeepsFill = true };
        var color = Colour(row["lineColor"]);
        var weight = Pens.WeightMm(VisibilityGraphics.WeightText(row["lineWeight"]));
        if (color != null || weight != null)
        {
            var cut = new MaterialLine(color, weight);
            var sep = new MaterialLine(color, weight is { } w ? Pens.SeenWeight(w) : null);
            o = o with { CutLine = o.CutLine is { } cl ? cl.Over(cut) : cut, SeparatorLine = o.SeparatorLine is { } sl ? sl.Over(sep) : sep };
        }
        if (row["halftone"] is JsonValue hv && hv.TryGetValue<bool>(out var half) && half)
        {
            var fill = row.ContainsKey("fillColor") ? o.Fill : Colour(category?["fillColor"]) ?? o.Fill;
            string Wash(string? c) => HexColor.Mix(c ?? "#000000", 0.5);
            o = o with
            {
                Halftone = true, Fill = fill == null ? null : Wash(fill), KeepsFill = fill != null || o.KeepsFill,
                CutLine = (o.CutLine ?? new MaterialLine()) with { Color = Wash(o.CutLine?.Color) },
                SeparatorLine = (o.SeparatorLine ?? new MaterialLine()) with { Color = Wash(o.SeparatorLine?.Color) },
            };
        }
        return o;
    }

    /// <summary>A band as the small-scale cut draws it.</summary>
    ResolvedMaterial? SmallScaled(ResolvedMaterial? g, DPolygon p, SmallScaleCut sc)
    {
        var plain = g ?? new ResolvedMaterial("", null, 1, null, null, false, false, null, "name");
        // Ink unless it says (the category's fill colour lays itself over it in the pens).
        var color = sc.Color ?? "#000000";
        switch (sc.Mode)
        {
            case SmallScaleCut.Solid:
                return plain with { Hatch = null, Background = null, Fill = color, FillOpacity = null, Membrane = false, SeparatorLine = null, JoinAs = "\u0001solid:" + color };
            case SmallScaleCut.Outline:
                // Its own bands one outline; an element joins those of its type (two walls round a corner), never a column in a wall.
                return plain with { Hatch = null, Background = null, Fill = null, FillOpacity = null, Membrane = false, SeparatorLine = null, JoinAs = "\u0001outline:" + OutlineKey(p.Element) };
            default:
                if (g?.Membrane == true) return g;
                return IsCore(p) ? plain with { Hatch = null, Background = null, Fill = color, FillOpacity = null } : plain with { Hatch = null, Background = null, Fill = null, FillOpacity = null };
        }
    }

    string OutlineKey(string element)
    {
        if (_outlineKeys.TryGetValue(element, out var k)) return k;
        return _outlineKeys[element] = _m.RelatingOf(element, IfcClass.RelDefinesByType)?.Id ?? element;
    }

    /// <summary>
    /// Whether a band is of its element's core: its layer's core flag (by its part key layer:&lt;n&gt;, else its material's first
    /// layer); an element whose set marks no core takes its thickest layer; one with no layer set is all core.
    /// </summary>
    bool IsCore(DPolygon p)
    {
        if (!_bands.TryGetValue(p.Element, out var bands)) _bands[p.Element] = bands = LayerSets.DrawnBandsOf(_m, p.Element);
        if (bands is not { Count: > 0 }) return true;
        var i = -1;
        if (p.Key is { } key && key.StartsWith("layer:", StringComparison.Ordinal) && int.TryParse(key.AsSpan(6), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 0 && n < bands.Count) i = n;
        if (i < 0) for (var j = 0; j < bands.Count; j++) if (p.Material != null && bands[j].Material == p.Material) { i = j; break; }
        // A part no layer names (an opening's lintel in it): drawn as the core.
        if (i < 0) return true;
        if (bands.Any(b => b.Core)) return bands[i].Core;
        var thickest = 0;
        for (var j = 1; j < bands.Count; j++) if (Math.Abs(bands[j].To - bands[j].From) > Math.Abs(bands[thickest].To - bands[thickest].From)) thickest = j;
        return i == thickest;
    }
}
