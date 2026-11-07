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

    readonly IModelView m;
    readonly bool in3d;
    readonly Dictionary<string, IReadOnlyList<LayerBand>?> bands = [];

    /// <summary>The view's graphics as they rule it (null: none).</summary>
    public JsonObject? Graphics { get; }

    VisibilityGraphics(IModelView model, JsonObject? graphics, bool in3d)
    {
        m = model;
        Graphics = graphics;
        this.in3d = in3d;
    }

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
        return in3d ? GraphicsSets.Hides3d(g, e) : GraphicsSets.Hides(g, e);
    }

    /// <summary>Whether a linked element draws halftone in this view (its link's row; Manage → Links halftones it everywhere).</summary>
    public bool Halftone(Entity e) => LinkRow(e.Str(Links.Key)) is { } row && True(row["halftone"]);

    /// <summary>The subcategories an element's category hides in this view (null: none, so its parts need not be asked).</summary>
    public JsonObject? HiddenSubs(Entity e)
    {
        if (LineStyles.CategoryOf(e.Class) is not { } c || RulesFor(e)?["categories"]?[c]?["sub"] is not JsonObject sub) return null;
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
        if (list == null || index < 0 || index >= list.Count) return null;
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
        if (e.Class == IfcClass.Annotation && e.PredefinedType != "DRAFT")
            return LinkRow(e.Str(Links.Key)) is { } row && True(row["hidden"]);
        return Hides(e);
    }
}
