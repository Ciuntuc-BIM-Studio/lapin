using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Graphics overridden per element in a view (decision 718, Revit's Override Graphics in View → By Element; issue #88): the
/// third level over the material (Manage → Materials) and the view's category (Visibility / Graphics), the strongest of
/// <see cref="ViewMaterialGraphics"/>'s tiers. Kept on the view itself (<c>elementGraphics</c>: {elementId: row}; the 3D's on the
/// project's <c>view3d</c>), never in a graphics set or a view style, so a view that takes a set keeps following it. A row:
/// <list type="bullet">
/// <item><c>cutHatch</c> a pattern the project knows or "none", <c>cutHatchScale</c> (&gt; 0, 1 the pattern's own),
/// <c>cutHatchAngle</c> (degrees), <c>cutFill</c> a colour or "none": what its cut bands draw (a scale or an angle alone
/// changes the material's own pattern);</item>
/// <item><c>surfaceHatch</c>, <c>surfaceHatchScale</c>, <c>surfaceHatchAngle</c>, <c>surfaceFill</c>: the same for the faces an
/// elevation (or an uncut section's background) sees, "none" to leave them plain;</item>
/// <item><c>hatchColor</c> and <c>hatchHalftone</c> for both;</item>
/// <item><c>cutLine</c> and <c>projectionLine</c> {color?, weight? (mm on paper), lineType?}: its cut outline and the lines
/// seen of it;</item>
/// <item><c>halftone</c>: the whole element drawn grey (its lines and fills; 35 % opaque in 3D).</item>
/// </list>
/// Plans, sections, elevations, sheets, the PDF and DXF draw it (<see cref="Views.Render"/>, <see cref="SurfaceHatches"/>).
/// </summary>
public static class ElementGraphics
{
    /// <summary>The view's attribute holding the rows.</summary>
    public const string Key = "elementGraphics";

    public static readonly string[] Fields = ["cutHatch", "cutHatchScale", "cutHatchAngle", "cutFill", "surfaceHatch", "surfaceHatchScale", "surfaceHatchAngle", "surfaceFill",
        "hatchColor", "hatchHalftone", "cutLine", "projectionLine", "halftone"];

    /// <summary>The kinds of view "every view" reaches.</summary>
    static readonly string[] Kinds = ["plan", "section", "elevation"];

    /// <summary>A row's lines as the per-element line styles read them ({cut: {color, weight, pattern}, projection: …}); null: none.</summary>
    public static JsonObject? LinesOf(JsonObject? row)
    {
        if (row == null) return null;
        JsonObject? o = null;
        foreach (var (field, role) in new[] { ("cutLine", LineRole.Cut), ("projectionLine", LineRole.Projection) })
        {
            if (MaterialLine.From(row[field]) is not { } l) continue;
            var spec = new JsonObject();
            if (l.Color != null) spec["color"] = l.Color;
            if (l.Weight is { } w) spec["weight"] = w;
            if (l.LineType != null) spec["pattern"] = l.LineType;
            (o ??= [])[role] = spec;
        }
        return o;
    }

    /// <summary>A row checked: each field as <see cref="Fields"/> says; a field null left out; null when nothing is left.</summary>
    public static JsonObject? Check(IModelView m, JsonObject? row)
    {
        if (row == null) return null;
        var o = new JsonObject();
        foreach (var (k, v) in row)
        {
            if (!Fields.Contains(k)) throw new CommandException($"„{k}” is not a field of an element's graphics ({string.Join(", ", Fields)}).");
            if (v is null) continue;
            o[k] = k switch
            {
                "cutHatch" or "surfaceHatch" => (string?)v is "" or "none" ? "none" : Lapin.Drawing.Hatch.HatchLibrary.Find((string?)v ?? "", DraftingResources.Patterns(m))?.Name ?? throw new CommandException($"There is no hatch pattern „{(string?)v}”."),
                "cutHatchScale" or "surfaceHatchScale" => Json.Number(v) is { } s && s > 0 && s <= 10000 && double.IsFinite(s) ? s : throw new CommandException("A hatch's scale is over 0 (1: the pattern's own)."),
                "cutHatchAngle" or "surfaceHatchAngle" => Json.Number(v) is { } a && double.IsFinite(a) ? Math.Round(((a % 360) + 360) % 360, 6) : throw new CommandException("A hatch's angle is in degrees."),
                "cutFill" or "surfaceFill" when (string?)v is "" or "none" => "none",
                "cutFill" or "surfaceFill" or "hatchColor" => (string?)v is { } c && HexColor.IsColour(c) ? c.ToLowerInvariant() : throw new CommandException($"An element's {k} is a colour (#rrggbb){(k == "hatchColor" ? "" : " or none")}."),
                "hatchHalftone" or "halftone" => v is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : throw new CommandException($"An element's {k} is true or false."),
                _ => DraftingResources.CheckStandardLine(m, v, "An element's graphics", k == "cutLine" ? "its cut line" : "its projection line"),
            };
            if (o[k] is null) o.Remove(k);
        }
        // Halftone off is the default: not kept.
        foreach (var k in new[] { "halftone" }) if (o[k] is JsonValue hv && hv.TryGetValue<bool>(out var h) && !h) o.Remove(k);
        return o.Count > 0 ? o : null;
    }

    /// <summary>The views a command reaches: the one named ("3d" the 3D), the ones listed, or every plan, section and elevation.</summary>
    static List<string> ViewsOf(ICommandContext ctx, JsonObject a)
    {
        if ((string?)a["scope"] == "all")
            return ctx.Model.OfClass(Views.Class).Where(v => Kinds.Contains(v.Str("kind") ?? "plan") && v.Str("viewId") == null).Select(v => v.Id).ToList();
        if (a["views"] is JsonArray vs) return vs.Select(x => (string?)x).OfType<string>().Distinct().ToList();
        return [(string?)a["id"] ?? throw new CommandException("Name the view (id), the views, or scope: all.")];
    }

    public static void Register(IModuleRegistry r)
    {
        // An element's graphics in a view (decision 718): {id: view | views: [ids] | scope: all, ids: [elements], value: row | null, patch?: row}.
        // value replaces each element's row (null clears it); patch lays its fields over the row (a field null takes it out). One undo step.
        r.Command("view.elementGraphics", "Graphics in view", (ctx, a) =>
        {
            var ids = (a["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().Distinct().ToList()
                ?? ((string?)a["element"] is { } one ? [one] : throw new CommandException("Name the elements (ids)."));
            if (ids.Count == 0) throw new CommandException("Name the elements (ids).");
            foreach (var id in ids)
                if (ctx.Model.Get(id) is not { } el || el.Class is Views.Class or IfcClass.Material || el.Class.EndsWith("Type", StringComparison.Ordinal))
                    throw new CommandException($"„{id}” is not an element a view draws.");
            var patch = a["patch"] as JsonObject;
            var value = patch == null ? Check(ctx.Model, a["value"] as JsonObject) : null;
            if (patch != null) Check(ctx.Model, new JsonObject(patch.Where(kv => kv.Value != null).Select(kv => KeyValuePair.Create(kv.Key, kv.Value?.DeepClone()))));
            var views = ViewsOf(ctx, a);
            if (views.Count == 0) throw new CommandException("There is no plan, section or elevation to change.");
            foreach (var viewId in views)
            {
                var (v, write) = GraphicsSets.Target(ctx, viewId);
                var rows = v.Attrs[Key]?.DeepClone() as JsonObject ?? [];
                foreach (var id in ids)
                {
                    JsonObject? next;
                    if (patch != null)
                    {
                        var cur = rows[id]?.DeepClone() as JsonObject ?? [];
                        foreach (var (k, x) in patch) { cur.Remove(k); if (x != null) cur[k] = x.DeepClone(); }
                        next = Check(ctx.Model, cur);
                    }
                    else next = value?.DeepClone() as JsonObject;
                    if (next != null) rows[id] = next; else rows.Remove(id);
                }
                var attrs = v.CloneAttrs();
                if (rows.Count > 0) attrs[Key] = rows; else attrs.Remove(Key);
                write(attrs);
            }
            return new JsonObject { ["views"] = views.Count };
        });

        // What a view overrides of elements (decision 718): {id: view, ids?} → {rows: {id: row}, fields}.
        r.Query("view.elementGraphicsOf", (q, a) =>
        {
            var id = (string?)a["id"] ?? throw new CommandException("Name the view (id).");
            var view = id == GraphicsSets.View3d ? GraphicsSets.As3dView(q.Model) : q.Model.Get(id) is { Class: Views.Class } x ? x : throw new CommandException("Not a view.");
            var rows = view.Attrs[Key] as JsonObject ?? [];
            var want = (a["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet();
            var o = new JsonObject();
            foreach (var (k, row) in rows) if (want == null || want.Contains(k)) o[k] = row?.DeepClone();
            return new JsonObject { ["rows"] = o, ["fields"] = new JsonArray([.. Fields.Select(f => (JsonNode)f)]) };
        });
    }
}
