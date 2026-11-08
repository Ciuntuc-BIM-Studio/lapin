using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What a view's Visibility / Graphics (docs/plan-visibility-graphics.md) makes of an element, a part
/// and a mark, worked out once per view: the view's graphics (its own, its set's or its style's set,
/// <see cref="GraphicsSets.Of"/>), and for a linked element the link's row in them: hidden or halftone
/// in this view, and, displayed <c>custom</c>, the link's own categories, subcategories and classes in
/// place of the view's (an architect's plan without the structural link's rebar). Plans, sections,
/// elevations and sheets (<see cref="Views"/>), the 3D (<c>view.looks3d</c>) and the mesh exports ask it.
/// </summary>
public sealed class VisibilityGraphics
{
    /// <summary>The parts of an element a category can show or hide apart: its layers by their role, a door's or a window's reveals.</summary>
    public static readonly string[] Subcategories = ["core", "finishes", "insulation", "membranes", "otherLayers", "reveals"];
    static readonly string[] LayerSubs = ["core", "finishes", "insulation", "membranes", "otherLayers"];

    /// <summary>The subcategories each category offers: the layers where its elements can be layered, the reveals of doors and windows.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> SubcategoriesOf = new Dictionary<string, string[]>
    {
        ["walls"] = LayerSubs, ["slabs"] = LayerSubs, ["roofs"] = LayerSubs, ["coverings"] = LayerSubs,
        ["beams"] = LayerSubs, ["columns"] = LayerSubs, ["other"] = LayerSubs,
        ["doors"] = ["reveals"], ["windows"] = ["reveals"],
    };

    /// <summary>A link's display in a view: its elements ruled by the view's graphics (the default) or by the link's own.</summary>
    public static readonly string[] LinkDisplays = ["host", "custom"];

    /// <summary>The annotation categories, by what owns a mark (stage B).</summary>
    public static readonly string[] AnnotationCategories = ["dimensions", "texts", "tags", "roomTags", "grids", "levels", "sections", "elevations", "callouts", "refPlanes", "viewBoundaries", "legends"];

    readonly IModelView m;
    readonly bool in3d;
    readonly Dictionary<string, IReadOnlyList<LayerBand>?> bands = [];
    readonly IReadOnlyDictionary<string, ViewFilters.Filter> filters;
    readonly Dictionary<(string, string), bool> matched = [];

    /// <summary>The view's graphics as they rule it (null: none).</summary>
    public JsonObject? Graphics { get; }

    VisibilityGraphics(IModelView model, JsonObject? graphics, bool in3d)
    {
        m = model;
        Graphics = graphics;
        this.in3d = in3d;
        filters = graphics == null ? new Dictionary<string, ViewFilters.Filter>() : ViewFilters.All(model).ToDictionary(f => f.Id);
    }

    /// <summary>The filters a set of rules uses, in order, with their rows ({id, hidden, …}), those the project no longer has left out.</summary>
    IEnumerable<(ViewFilters.Filter Filter, JsonObject Row)> FiltersOf(JsonObject? rules) =>
        (rules?["filters"] as JsonArray)?.OfType<JsonObject>().Select(r => ((string?)r["id"] is { } id && filters.TryGetValue(id, out var f) ? f : null, r))
            .Where(x => x.Item1 != null).Select(x => (x.Item1!, x.r)) ?? [];

    bool Takes(ViewFilters.Filter f, Entity e)
    {
        if (!matched.TryGetValue((f.Id, e.Id), out var hit)) matched[(f.Id, e.Id)] = hit = ViewFilters.Matches(m, e, f);
        return hit;
    }

    /// <summary>The rows of the filters that take an element, in the view's order (the first gives each field, as Revit's).</summary>
    public IEnumerable<JsonObject> FilterRows(Entity e) => FiltersOf(RulesFor(e)).Where(x => Takes(x.Filter, e)).Select(x => x.Row);

    /// <summary>A view's (a 2D view, a 3D view, or {3D} as <see cref="GraphicsSets.As3dView"/> gives it).</summary>
    public static VisibilityGraphics Of(IModelView m, Entity view) => new(m, GraphicsSets.Of(m, view), view.Str("kind") == "3d");

    /// <summary>A view's graphics read as given (a test, an export with graphics of its own).</summary>
    public static VisibilityGraphics From(IModelView m, JsonObject? graphics, bool in3d) => new(m, graphics, in3d);

    static bool True(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>The row a view gives a link ({display, hidden, halftone, categories, classes}), or null.</summary>
    public JsonObject? LinkRow(string? linkId) => linkId == null ? null : Graphics?["links"]?[linkId] as JsonObject;

    /// <summary>Whether the view has rows for links at all (the drawing then asks each linked element).</summary>
    public bool HasLinks => Graphics?["links"] is JsonObject { Count: > 0 };

    /// <summary>The graphics that rule an element: a link's own when the view displays it custom, else the view's.</summary>
    public JsonObject? RulesFor(Entity e) =>
        LinkRow(e.Str(Links.Key)) is { } row && (string?)row["display"] == "custom" ? row : Graphics;

    /// <summary>Whether the view hides an element: the link hidden in this view, its category or IFC class hidden (the link's own when custom).</summary>
    public bool Hides(Entity e)
    {
        if (LinkRow(e.Str(Links.Key)) is { } row && True(row["hidden"])) return true;
        var g = RulesFor(e);
        if (in3d ? GraphicsSets.Hides3d(g, e) : GraphicsSets.Hides(g, e)) return true;
        // A reference all of one finish, insulation or membrane (a plaster part of a wall, a covering): hidden with its subcategory (decision 400).
        if (HiddenSubs(e) is { } sub && ReferenceLayers.WholeSub(m, e) is { } whole && True(sub[whole]?["hidden"])) return true;
        // A filter that takes it and hides what it takes (the first that says, in the view's order).
        return FilterRows(e).Select(r => r["hidden"]).FirstOrDefault(h => h is JsonValue) is { } hv && True(hv);
    }

    /// <summary>Whether a linked element draws halftone in this view (its link's row; Manage → Links halftones it everywhere).</summary>
    public bool Halftone(Entity e) => LinkRow(e.Str(Links.Key)) is { } row && True(row["halftone"])
        || (e.Str(DxfImportKey) is { } imp && ImportRow(imp) is { } ir && True(ir["halftone"]));

    /// <summary>What a DXF import's shapes carry, and the project's list of the imports (the drafting module's <c>dxfImport</c>, <c>dxfImports</c>).</summary>
    public const string DxfImportKey = "dxfImport", DxfImportsKey = "dxfImports";

    /// <summary>The row a view gives an import (a DXF import by its id, a traced PDF or image by its own): {hidden, halftone, layers: {name: {hidden}}}.</summary>
    public JsonObject? ImportRow(string? id) => id == null ? null : Graphics?["imports"]?[id] as JsonObject;

    /// <summary>The annotation category of a mark's owner (a dimension, a grid, a level, a room's stamp…), or null when it is not an annotation.</summary>
    public static string? AnnotationOf(Entity e, ViewMark? mk = null) => e.Class switch
    {
        IfcClass.Annotation => e.PredefinedType switch { "DIMENSION" => "dimensions", "TEXT" => "texts", "TAG" => "tags", "LEGEND" => "legends", _ => null },
        IfcClass.Grid => "grids",
        IfcClass.BuildingStorey => "levels",
        IfcClass.Space => mk is { Under: false, Pick: false } ? "roomTags" : null,
        Views.Class => e.Str("kind") switch { "section" => "sections", "elevation" => "elevations", "plane" => "refPlanes", _ => null },
        "LapinInset" => "callouts",
        ViewBoundaries.Class => "viewBoundaries",
        _ => null,
    };

    // Stage C: graphic overrides (decision 394).

    /// <summary>The first value a field has in the rows of the filters that take an element (the view's order: the first that says gives it).</summary>
    JsonNode? FromFilters(Entity e, string field) => FilterRows(e).Select(r => r[field]).FirstOrDefault(x => x != null);

    JsonObject? CategoryRow(Entity e) => ReferenceLayers.CategoryOf(e) is { } c ? RulesFor(e)?["categories"]?[c] as JsonObject : null;

    /// <summary>Whether anything in the view's graphics draws an element otherwise than its styles (a filter's colour or lines, a halftone, a transparency).</summary>
    public bool HasOverrides =>
        HasLinks || (Graphics?["filters"] as JsonArray)?.OfType<JsonObject>().Any(r => r.Count > 2 || r.Any(kv => kv.Key is not ("id" or "hidden"))) == true
        || (Graphics?["categories"] as JsonObject)?.Any(c => c.Value?["halftone"] != null || c.Value?["transparency"] != null) == true;

    /// <summary>The colour an element's lines (and its fills, paled) draw in: a filter's colour or halftone, else its category's halftone, else its link's; null: its own.</summary>
    public string? ColorOf(Entity e)
    {
        foreach (var r in FilterRows(e))
        {
            if ((string?)r["color"] is { Length: 7 } c) return c;
            if (True(r["halftone"])) return Links.Halftone;
        }
        if (True(CategoryRow(e)?["halftone"])) return Links.Halftone;
        return Halftone(e) ? Links.Halftone : null;
    }

    /// <summary>The line styles a filter gives an element ({role: {pen, pattern}} for cut and projection), as the per-element overrides read them; null: none.</summary>
    public JsonObject? LinesOf(Entity e)
    {
        var pen = Json.Number(FromFilters(e, "pen"));
        var pattern = (string?)FromFilters(e, "pattern");
        if (pen == null && pattern == null) return null;
        JsonObject Spec()
        {
            var o = new JsonObject();
            if (pen != null) o["pen"] = (int)pen.Value;
            if (pattern != null) o["pattern"] = pattern;
            return o;
        }
        return new JsonObject { [LineRole.Cut] = Spec(), [LineRole.Projection] = Spec() };
    }

    /// <summary>How opaque an element draws in 3D: a filter's transparency (0–100), else its category's; a halftone 35 %; null: as it is.</summary>
    public double? OpacityOf(Entity e)
    {
        var t = Json.Number(FromFilters(e, "transparency")) ?? Json.Number(CategoryRow(e)?["transparency"]);
        if (t is { } pct && pct > 0) return Math.Clamp(1 - pct / 100, 0.05, 1);
        return ColorOf(e) == Links.Halftone ? 0.35 : null;
    }

    /// <summary>The colour an annotation category draws in this view (its own, or halftone grey); null: as it is.</summary>
    public string? AnnotationColor(string category) =>
        Graphics?["annotations"]?[category] is JsonObject row ? (string?)row["color"] is { Length: 7 } c ? c : True(row["halftone"]) ? Links.Halftone : null : null;

    /// <summary>The level of development the view's graphics keep (decision 394's coarse, medium, fine read as LOD 100, 200, 400; decision 404's), or null; <see cref="ViewDetail.Of"/> says the view's.</summary>
    public int? DetailLevel => PlanDetail.LodOf(Graphics?["detailLevel"]);

    /// <summary>A mark recoloured as its annotation category says (lines, texts and fills); unchanged otherwise.</summary>
    public void Recolor(ViewMark mk)
    {
        if (OwnerOf(m, mk.Owner) is not { } e || AnnotationOf(e, mk) is not { } ac || AnnotationColor(ac) is not { } color) return;
        for (var i = 0; i < mk.Lines.Count; i++) mk.Lines[i] = mk.Lines[i] with { Color = color };
        for (var i = 0; i < mk.Texts.Count; i++) mk.Texts[i] = mk.Texts[i] with { Color = color };
        for (var i = 0; i < mk.Fills.Count; i++) mk.Fills[i] = mk.Fills[i] with { Color = color };
    }

    /// <summary>Whether the view hides an annotation category.</summary>
    public bool HidesAnnotation(string category) => True(Graphics?["annotations"]?[category]?["hidden"]);

    /// <summary>The subcategories an element's category hides in this view (null: none, so its parts need not be asked).</summary>
    public JsonObject? HiddenSubs(Entity e)
    {
        if (ReferenceLayers.CategoryOf(e) is not { } c || RulesFor(e)?["categories"]?[c]?["sub"] is not JsonObject sub) return null;
        return sub.Any(s => True(s.Value?["hidden"])) ? sub : null;
    }

    /// <summary>Whether the view hides one part of an element by its subcategory (a wall's finishes, a door's reveals).</summary>
    public bool HidesPart(Entity e, string? partKey)
    {
        if (HiddenSubs(e) is not { } sub || SubOf(e, partKey) is not { } s) return false;
        return True(sub[s]?["hidden"]);
    }

    /// <summary>
    /// A part's subcategory by its key: a layer (<c>layer:&lt;n&gt;</c>) as its band is the core, a finish
    /// (<see cref="Materials.IsFinish"/>), an insulation or a membrane (the material's category), or another
    /// layer; a reveal (<c>reveal…</c>); null for any other part (it goes with its element).
    /// </summary>
    public string? SubOf(Entity e, string? partKey)
    {
        if (partKey == null) return null;
        if (partKey.StartsWith("reveal", StringComparison.Ordinal)) return "reveals";
        if (!partKey.StartsWith("layer:", StringComparison.Ordinal)
            || !int.TryParse(partKey.AsSpan(6), System.Globalization.CultureInfo.InvariantCulture, out var index)) return null;
        if (!bands.TryGetValue(e.Id, out var list)) bands[e.Id] = list = LayerSets.BandsOf(m, e.Id);
        // A reference's layers, from its file's material (decision 400).
        if (list == null) return ReferenceLayers.SubOfPart(m, e, partKey);
        if (index < 0 || index >= list.Count) return null;
        var band = list[index];
        if (band.Core) return "core";
        var mat = m.Get(band.Material);
        if (mat != null && Materials.IsFinish(mat)) return "finishes";
        return mat?.Str("category")?.Trim().ToLowerInvariant() switch
        {
            "insulation" => "insulation",
            "membrane" => "membranes",
            _ => "otherLayers",
        };
    }

    /// <summary>The entity a mark belongs to: its owner, or the element before the owner's colon (a beam's stirrups, a grid's axis).</summary>
    public static Entity? OwnerOf(IModelView m, string? owner)
    {
        if (owner == null) return null;
        if (m.Get(owner) is { } e) return e;
        var colon = owner.IndexOf(':');
        return colon > 0 ? m.Get(owner[..colon]) : null;
    }

    /// <summary>
    /// Whether the view hides a mark by its owner: an element the view hides (a rebar's marks with the rebar, a
    /// drafted line with the linework, a link's shapes with the link hidden in this view); annotations
    /// (dimensions, texts, tags) only by their link here.
    /// </summary>
    public bool HidesMark(ViewMark mk)
    {
        if (OwnerOf(m, mk.Owner) is not { } e) return false;
        // Annotation categories (stage B): a dimension, a grid, a section's mark, a room's stamp…
        if (AnnotationOf(e, mk) is { } ac && HidesAnnotation(ac)) return true;
        // Imported categories: a DXF import by its file and its layers, a traced PDF or image.
        if (e.Str(DxfImportKey) is { } imp && ImportRow(imp) is { } ir && (True(ir["hidden"]) || (e.Str("layer") is { } layer && True(ir["layers"]?[layer]?["hidden"])))) return true;
        if (e.Class == "LapinViewImage" && True(ImportRow(e.Id)?["hidden"])) return true;
        if (e.Class == IfcClass.Annotation && e.PredefinedType != "DRAFT")
            return LinkRow(e.Str(Links.Key)) is { } row && True(row["hidden"]);
        // A room's stamp goes with its room only in the rooms' own marks (they ask the graphics themselves).
        return Hides(e);
    }
}
