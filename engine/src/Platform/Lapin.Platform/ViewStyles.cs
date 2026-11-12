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
/// <item><c>layers</c>: which layers of layered elements draw: all of them,
/// all but the finishes (<see cref="Materials.IsFinish"/>), or the core only;</item>
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
/// <c>entourageFill</c>: the silhouettes' fill in elevations and sections, a colour, none by default.</item>
/// </list>
/// A view takes its style (<c>viewStyle</c>, else its discipline's), and may
/// set any of these itself: what it sets wins over the style.
/// </summary>
public static class ViewStyles
{
    public const string Key = "viewStyles";
    public static readonly string[] LayerModes = ["all", "noFinishes", "core"];
    public static readonly string[] Fields = ["layers", "joins", "overhead", "ifc2d", "voids", "voidSizes", "finishHatch", "surfaceHatch", "entourage", "entourageFill", "detail",
        "wallFinishesPlan", "wallFinishesSection", "floorFinishesPlan", "floorFinishesSection"];

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

    /// <summary>A style, or a view's settings as its style and its own choices make them.</summary>
    public sealed record Style(string Id, string Name, string? Discipline, string Layers, bool Joins, bool Overhead, bool Ifc2d = true, bool Voids = false, bool VoidSizes = true, bool FinishHatch = true, bool SurfaceHatch = true, string Entourage = "outline", string? EntourageFill = null, string? GraphicsSet = null, int Detail = PlanDetail.Lod350,
        bool WallFinishesPlan = true, bool WallFinishesSection = true, bool FloorFinishesPlan = true, bool FloorFinishesSection = true)
    {
        public JsonObject ToJson() => WithSet(new()
        {
            ["id"] = Id, ["name"] = Name, ["discipline"] = Discipline, ["layers"] = Layers, ["joins"] = Joins, ["overhead"] = Overhead, ["ifc2d"] = Ifc2d,
            ["voids"] = Voids, ["voidSizes"] = VoidSizes, ["finishHatch"] = FinishHatch, ["surfaceHatch"] = SurfaceHatch,
            ["entourage"] = Entourage, ["entourageFill"] = EntourageFill, ["detail"] = Detail,
            ["wallFinishesPlan"] = WallFinishesPlan, ["wallFinishesSection"] = WallFinishesSection, ["floorFinishesPlan"] = FloorFinishesPlan, ["floorFinishesSection"] = FloorFinishesSection,
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

        /// <summary>The graphics set its views take when they have none of their own (decision 391), written only when it names one.</summary>
        JsonObject WithSet(JsonObject o)
        {
            if (GraphicsSet != null) o["graphicsSet"] = GraphicsSet;
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
            On(o["wallFinishesPlan"]), On(o["wallFinishesSection"]), On(o["floorFinishesPlan"]), On(o["floorFinishesSection"]));

        static bool On(JsonNode? n) => n is not JsonValue v || !v.TryGetValue<bool>(out var b) || b;

        /// <summary>Whether a style saved without a newer field takes it on: unless it is a structural, MEP or coordination one.</summary>
        static bool ArchitecturalByDefault(JsonObject o) =>
            !((string?)o["discipline"] is "Structure" or "MEP" || (string?)o["id"] is "structural" or "mep" or "coordination");
    }

    /// <summary>The styles a project without its own reads as: one per discipline; the structural one draws the cores alone.</summary>
    public static readonly IReadOnlyList<Style> Defaults =
    [
        new("architectural", "Architectural", "Architecture", "all", true, true),
        new("structural", "Structural", "Structure", "core", true, true, FinishHatch: false, SurfaceHatch: false, Detail: PlanDetail.Lod400),
        new("mep", "MEP", "MEP", "all", true, true, FinishHatch: false, SurfaceHatch: false),
        // For coordination drawings (decision 290): the openings' voids drawn, marked and sized; taken by name, no discipline's.
        new("coordination", "Coordination", null, "all", true, true, Voids: true, FinishHatch: false, SurfaceHatch: false),
    ];

    public static IReadOnlyList<Style> All(IModelView m) =>
        Spatial.Project(m)?.Attrs[Key] is JsonArray arr && arr.Count > 0
            ? arr.OfType<JsonObject>().Select(Style.From).Where(s => s.Id.Length > 0).ToList()
            : Defaults;

    /// <summary>The style a view takes: the one it names, else its discipline's, else the first.</summary>
    public static Style StyleOf(IModelView m, Entity view)
    {
        var all = All(m);
        if (view.Str("viewStyle") is { } id && all.FirstOrDefault(s => s.Id == id) is { } named) return named;
        var discipline = view.Str("discipline") ?? "Architecture";
        return all.FirstOrDefault(s => string.Equals(s.Discipline, discipline, StringComparison.OrdinalIgnoreCase)) ?? all[0];
    }

    /// <summary>How a view draws: its style's settings, with what the view sets itself over them.</summary>
    public static Style Of(IModelView m, Entity view)
    {
        var s = StyleOf(m, view);
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
        };
    }

    static bool? Own(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    /// <summary>Whether a view draws a part of a layered element (a part keyed layer:&lt;n&gt;; any other part always draws).</summary>
    public static bool Draws(IModelView m, Style s, string element, string? partKey, Dictionary<string, IReadOnlyList<LayerBand>?> cache) =>
        Draws(m, s.Layers, element, partKey, cache);

    /// <summary>Whether a layers mode (all, noFinishes, core) draws a part of a layered element (a part keyed layer:&lt;n&gt;; any other part always draws).</summary>
    public static bool Draws(IModelView m, string layers, string element, string? partKey, Dictionary<string, IReadOnlyList<LayerBand>?> cache)
    {
        if (layers == "all" || partKey is not { } k || !k.StartsWith("layer:", StringComparison.Ordinal)
            || !int.TryParse(k.AsSpan(6), System.Globalization.CultureInfo.InvariantCulture, out var index)) return true;
        if (!cache.TryGetValue(element, out var bands)) cache[element] = bands = LayerSets.BandsOf(m, element);
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

    /// <summary>A layers mode as a request names it: all (also when absent), noFinishes or core.</summary>
    public static string LayersOf(JsonNode? n) =>
        (string?)n is { } l ? LayerModes.Contains(l) ? l : throw new CommandException($"„{l}” is not all, noFinishes or core.") : "all";

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
        if ((string?)a["layers"] is { } l) s = s with { Layers = LayerModes.Contains(l) ? l : throw new CommandException($"„{l}” is not all, noFinishes or core.") };
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
                if (f == "layers") own[f] = (string?)value is { } l && LayerModes.Contains(l) ? l : throw new CommandException($"„{value}” is not all, noFinishes or core.");
                else if (f == "entourage") own[f] = EntourageMode(value);
                // A fill set to none on the view, over a style's colour: kept as "none".
                else if (f == "entourageFill") own[f] = CheckedFill(value) ?? "none";
                // The view's level of development (decision 404): 100, 200, 300, 350 or 400.
                else if (f == "detail") own[f] = PlanDetail.LodOf(value) ?? throw new CommandException(PlanDetail.LodProblem);
                else own[f] = value is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : throw new CommandException($"{f} is true or false.");
            }
            if (own.Count > 0) attrs["styleOverrides"] = own; else attrs.Remove("styleOverrides");
            write(attrs);
            return null;
        });
    }
}
