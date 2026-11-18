using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The project's drafting resources, in every product (part of core): how
/// materials draw (on each IfcMaterial), the hatch patterns imported from
/// <c>.pat</c> files and the drafting standards (both on the IfcProject, so
/// they undo, save and travel with the model).
/// </summary>
public static class DraftingResources
{
    public static readonly string[] Textures = ["none", "brick", "brick-end", "concrete", "wood", "wood-end", "plaster", "stone", "metal", "insulation", "earth", "tiles"];

    /// <summary>The project's own hatch patterns (imported; a built-in's name overrides it).</summary>
    public static IReadOnlyList<HatchPattern> Patterns(IModelView m) =>
        Spatial.Project(m)?.Str("hatchPatterns") is { Length: > 0 } text ? Pat.Parse(text).Patterns : [];

    public static StyleBook Book(IModelView m) => Standards.Read(Spatial.Project(m)?.Attrs["drafting"]);

    /// <summary>A .pat file's patterns as the import preview shows them: new, the same as the project's of its name, or the same pattern drawn otherwise (updated by default).</summary>
    static List<LibraryMerge.Row> PatternRows(IModelView m, string text)
    {
        var (incoming, errors) = Pat.Parse(text);
        if (incoming.Count == 0) throw new CommandException(errors.FirstOrDefault() ?? "The file has no hatch pattern.");
        var mine = Patterns(m);
        return [.. incoming.Select(p => mine.FirstOrDefault(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase)) is { } have
            ? new LibraryMerge.Row(p.Name, "pattern", p.Name, Pat.Serialize([have]) == Pat.Serialize([p]) ? LibraryMerge.Match.Same : LibraryMerge.Match.Id, ["lines"])
            : new LibraryMerge.Row(p.Name, "pattern", p.Name, LibraryMerge.Match.New))];
    }

    static Entity Project(ICommandContext ctx) => Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");

    static readonly HashSet<string> GraphicsFields = ["hatch", "hatchScale", "fill", "hatchPen", "hatchAngle", "hatchAlign", .. ViewGraphics.FlexFields, "hatchUnits", "reset"];

    /// <summary>The fields a null takes back to the default (not given): all but hatch and fill, whose null is an explicit none.</summary>
    /// <summary>The scale a real-size pattern's swatch is drawn at when none is asked (decision 671): 1:20, where a brick reads as one.</summary>
    public const double PreviewScale = 20;

    static bool NullResets(string field) => field is not ("hatch" or "fill");

    static readonly HashSet<string> BackgroundFields = ["hatch", "hatchScale", "hatchAngle", "hatchColor", "hatchWeight"];
    static readonly HashSet<string> LineFields = ["color", "weight", "lineType"];

    /// <summary>A colour as a material's graphics take it: #rrggbb.</summary>
    static string Colour(JsonNode? v, string where, string what) =>
        (string?)v is { } c && HexColor.IsColour(c) ? c.ToLowerInvariant() : throw new CommandException($"{where}: {what} is a colour (#rrggbb).");

    /// <summary>A line weight in paper millimetres: 0.05 to 5 (the ISO series 0.13–1.0, or any value between).</summary>
    static double Weight(JsonNode? v, string where, string what) =>
        Json.Number(v) is { } w && double.IsFinite(w) && w >= 0.05 && w <= 5 ? Math.Round(w, 3) : throw new CommandException($"{where}: {what} is a line weight in mm, 0.05 to 5.");

    /// <summary>An opacity in percent, 0 to 100.</summary>
    static double Opacity(JsonNode? v, string where, string what) =>
        Json.Number(v) is { } o && double.IsFinite(o) && o >= 0 && o <= 100 ? o : throw new CommandException($"{where}: {what} is a percentage, 0 to 100.");

    /// <summary>A line type of the project's standards or a built-in one (continuous, dashed, dotted…), as the book names it.</summary>
    static string LineType(IModelView m, JsonNode? v, string where)
    {
        var name = (string?)v ?? "";
        return Standards.LineTypeNames(Book(m)).FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new CommandException($"{where}: there is no line type „{name}”.");
    }

    /// <summary>A material's line (cutLine, separatorLine): {color, weight, lineType}, each checked; a null field is left out (the Object styles').</summary>
    static JsonObject? CheckLine(IModelView m, JsonNode? v, string where, string what)
    {
        if (v is null) return null;
        if (v is not JsonObject o) throw new CommandException($"{where}: {what} is {{color, weight, lineType}}.");
        var r = new JsonObject();
        foreach (var (k, x) in o)
        {
            if (!LineFields.Contains(k)) throw new CommandException($"{where}: „{k}” is not a field of {what} (color, weight, lineType).");
            if (x is null) continue;
            r[k] = k switch { "color" => Colour(x, where, what + "'s colour"), "weight" => Weight(x, where, what + "'s weight"), _ => LineType(m, x, where) };
        }
        return r.Count > 0 ? r : null;
    }

    /// <summary>A view's graphics checked: a known pattern (or null for none), a positive scale, a colour (or "material"), a pen 1–16.</summary>
    static JsonObject CheckGraphics(IModelView m, JsonObject g, string where)
    {
        var o = new JsonObject();
        foreach (var (k, v) in g)
        {
            if (!GraphicsFields.Contains(k)) throw new CommandException($"{where}: „{k}” is not a material graphic.");
            // Null takes a field back to its default (decision 670), but for hatch and fill, where it is "none".
            if (v is null && NullResets(k)) { o[k] = null; continue; }
            switch (k)
            {
                case "hatch":
                    if (v is null || (string?)v is "" or null) { o[k] = null; break; }
                    var name = (string)v!;
                    o[k] = HatchLibrary.Find(name, Patterns(m))?.Name ?? throw new CommandException($"There is no hatch pattern „{name}”.");
                    break;
                case "hatchScale":
                    o[k] = Json.Number(v) is { } s && s > 0 && double.IsFinite(s) ? s : throw new CommandException($"{where}: the hatch scale must be positive.");
                    break;
                case "fill":
                    if (v is null || (string?)v is "" or null) { o[k] = null; break; }
                    var f = (string)v!;
                    o[k] = f == "material" || System.Text.RegularExpressions.Regex.IsMatch(f, "^#[0-9a-fA-F]{6}$") ? f : throw new CommandException($"{where}: a fill is a colour (#rrggbb) or \"material\".");
                    break;
                case "hatchPen":
                    o[k] = Json.Number(v) is { } p && p >= 1 && p <= 16 ? (int)p : throw new CommandException($"{where}: a pen is 1 to 16.");
                    break;
                case "hatchAngle":
                    o[k] = Json.Number(v) is { } ang && double.IsFinite(ang) ? ang : throw new CommandException($"{where}: the hatch angle is in degrees.");
                    break;
                case "hatchAlign":
                    o[k] = (string?)v is { } al && HatchAlign.All.Contains(al) ? al : throw new CommandException($"{where}: a hatch aligns to the \"view\" or to the \"layer\".");
                    break;
                // Decision 670: the hatch's own colour, weight, line type, opacity and halftone; the fill's opacity; a background hatch; the material's lines.
                // Decision 671: the units its hatch lies in over the pattern's own (rare).
                case "hatchUnits":
                    o[k] = HatchScale.Parse((string?)v) is { } hu ? HatchScale.Name(hu) : throw new CommandException($"{where}: hatch units are \"paper\" or \"model\".");
                    break;
                case "hatchColor": o[k] = Colour(v, where, "a hatch colour"); break;
                case "hatchWeight": o[k] = Weight(v, where, "a hatch weight"); break;
                case "hatchLineType": o[k] = LineType(m, v, where); break;
                case "hatchOpacity": o[k] = Opacity(v, where, "a hatch's opacity"); break;
                case "fillOpacity": o[k] = Opacity(v, where, "a fill's opacity"); break;
                case "halftone":
                    o[k] = v is JsonValue hv && hv.TryGetValue<bool>(out var ht) ? ht : throw new CommandException($"{where}: halftone is true or false.");
                    break;
                case "backgroundHatch":
                {
                    if (v is not JsonObject bg) throw new CommandException($"{where}: a background hatch is {{hatch, hatchScale, hatchAngle, hatchColor, hatchWeight}}.");
                    var b = new JsonObject();
                    foreach (var (bk, bv) in bg)
                    {
                        if (!BackgroundFields.Contains(bk)) throw new CommandException($"{where}: „{bk}” is not a field of a background hatch.");
                        if (bv is null || (bk == "hatch" && (string?)bv is "")) continue;
                        b[bk] = bk switch
                        {
                            "hatch" => HatchLibrary.Find((string)bv!, Patterns(m))?.Name ?? throw new CommandException($"There is no hatch pattern „{(string?)bv}”."),
                            "hatchScale" => Json.Number(bv) is { } bs && bs > 0 && double.IsFinite(bs) ? bs : throw new CommandException($"{where}: the background hatch's scale must be positive."),
                            "hatchAngle" => Json.Number(bv) is { } ba && double.IsFinite(ba) ? ba : throw new CommandException($"{where}: the background hatch's angle is in degrees."),
                            "hatchColor" => Colour(bv, where, "the background hatch's colour"),
                            _ => Weight(bv, where, "the background hatch's weight"),
                        };
                    }
                    // Without a pattern it draws nothing: none kept.
                    o[k] = b.ContainsKey("hatch") ? b : null;
                    break;
                }
                case "cutLine": case "separatorLine": o[k] = CheckLine(m, v, where, k == "cutLine" ? "a cut line" : "a separator line"); break;
                case "reset":
                    if (v is not JsonArray fields || fields.Any(f => (string?)f is not { } fs || fs == "reset" || !GraphicsFields.Contains(fs)))
                        throw new CommandException($"{where}: reset lists the fields to take back to their default ({string.Join(", ", GraphicsFields.Where(f => f != "reset"))}).");
                    o[k] = v.DeepClone();
                    break;
            }
        }
        return o;
    }

    /// <summary>Whether anything in the model names the entity (an attribute holding its id, a relation).</summary>
    static bool InUse(IModelView m, string id) =>
        m.Entities.Any(e => e.Id != id && e.Attrs.ToJsonString().Contains($"\"{id}\"", StringComparison.Ordinal)) || m.RelationsOf(id).Any()
        // A material named by its name (a door's frame, a sill, a curtain wall's glass) is in use too.
        || m.Get(id) is { } mat && m.Entities.Any(e => e.Id != id && e.Attrs.ToJsonString().Contains(mat.Name, StringComparison.OrdinalIgnoreCase) && Names(e.Attrs, false, mat.Name));

    /// <summary>Whether a JSON tree names a material by <paramref name="name"/> under a material key.</summary>
    static bool Names(JsonNode node, bool named, string name) => node switch
    {
        JsonObject o => o.Any(kv => kv.Value is JsonValue v && v.TryGetValue<string>(out var s)
            ? (named || MaterialKey(kv.Key)) && string.Equals(s, name, StringComparison.OrdinalIgnoreCase)
            : kv.Value != null && Names(kv.Value, named || MaterialKey(kv.Key), name)),
        JsonArray a => a.Any(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? named && string.Equals(s, name, StringComparison.OrdinalIgnoreCase) : x != null && Names(x, named, name)),
        _ => false,
    };

    /// <summary>
    /// A material that stops joining (forum topic 59): the walls, beams and strip footings mainly of it lose their joins, in the
    /// same step; one that joins again: they join what they meet, as drawing them does.
    /// </summary>
    static void Rejoin(ICommandContext ctx, string materialId, bool joins)
    {
        if (ctx.Services.Get<IPathElements>() is not { } paths) return;
        var elements = ctx.Model.Entities.Where(e => paths.IsPath(e.Class) && e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray && CutOrder.MainMaterial(e, ctx.Model) == materialId).ToList();
        foreach (var e in elements)
        {
            if (!joins) { foreach (var r in Joins.Of(ctx.Model, e.Id).ToList()) ctx.RemoveRelation(r.Id); continue; }
            try { Joins.Auto(ctx, paths, e.Id); }
            catch (CommandException) { }
        }
    }

    /// <summary>A material with a command's changes (material.update's fields), checked.</summary>
    static Entity Updated(ICommandContext ctx, Entity mat, JsonObject a)
    {
        var attrs = mat.CloneAttrs();
        if (a["color"] is { } c) attrs["color"] = (string?)c is { } cs && System.Text.RegularExpressions.Regex.IsMatch(cs, "^#[0-9a-fA-F]{6}$") ? cs : throw new CommandException("A colour is #rrggbb.");
        MaterialLook.Apply(attrs, a);
        if (a.ContainsKey("category")) { if ((string?)a["category"] is { Length: > 0 } cat) attrs["category"] = cat; else attrs.Remove("category"); }
        // Its look in 3D: a texture (brick, concrete, wood, plaster, stone, metal, insulation, earth, tiles; "none"; null: guessed from its hatch and name).
        // Its faces along it (texture), its ends (textureEnd: where a wall is cut across, a brick's holes) and its top and bottom (textureTop); the ends and top follow the faces unless given.
        foreach (var key in new[] { "texture", "textureEnd", "textureTop" })
            if (a.ContainsKey(key))
            {
                if ((string?)a[key] is { Length: > 0 } tex)
                    attrs[key] = Textures.Contains(tex) ? tex : throw new CommandException($"„{tex}” is not a texture: {string.Join(", ", Textures)}.");
                else attrs.Remove(key);
            }
        // Its own texture, its normal map and its bump map: each an image of the project (null takes it off; an image nothing uses
        // goes), all three laid on at one size, metres, and turned alike.
        foreach (var key in Images.MaterialKeys)
        {
            if (!a.ContainsKey(key)) continue;
            var had = attrs[key] is JsonValue hv ? (string?)hv : null;
            if ((string?)a[key] is { Length: > 0 } img)
            {
                if (ctx.Model.Get(img) is not { Class: Images.Class }) throw new CommandException("There is no such image in the project.");
                attrs[key] = img;
            }
            else attrs.Remove(key);
            if (had != null && had != (string?)attrs[key] && !Images.MaterialKeys.Any(k => (string?)attrs[k] == had)) Images.DropIfUnused(ctx, had, mat.Id);
        }
        if (a.ContainsKey("textureSize"))
        {
            if (a["textureSize"] is JsonArray { Count: 2 } ts && Json.Number(ts[0]) is { } tw && Json.Number(ts[1]) is { } th)
            {
                if (!(tw >= 0.01 && th >= 0.01 && tw <= 1000 && th <= 1000)) throw new CommandException("A texture lies 1 cm to 1 km wide and high.");
                attrs["textureSize"] = new JsonArray(tw, th);
            }
            else if (a["textureSize"] is null) attrs.Remove("textureSize");
            else throw new CommandException("A texture's size is [width, height] in metres.");
        }
        foreach (var flag in new[] { "membrane", "drawJoints" })
            if (a.ContainsKey(flag))
            {
                if (a[flag] is JsonValue fv && fv.TryGetValue<bool>(out var b)) attrs[flag] = b; else attrs.Remove(flag);
            }
        // Whether its elements join others (forum topic 59): kept only when they do not, the default being that they do.
        if (a.ContainsKey("joins"))
        {
            if (a["joins"] is JsonValue jv && jv.TryGetValue<bool>(out var joins) && !joins) attrs["joins"] = false; else attrs.Remove("joins");
        }
        // What it says of itself (docs/plan-buildup-tags.md): its description, its parameters, how it is measured and bought.
        MaterialFields.Apply(attrs, a);
        foreach (var kind in MaterialGraphics.Kinds)
        {
            if (!a.ContainsKey(kind)) continue;
            if (a[kind] is not JsonObject g) { attrs.Remove(kind); continue; }
            // Fields merge into what the view had (replace: they are all it has); a field set to null is "none" (hatch, fill).
            var replace = a["replace"] is JsonValue rv && rv.TryGetValue<bool>(out var rb) && rb;
            var merged = !replace && attrs[kind] as JsonObject is { } had ? (JsonObject)had.DeepClone() : new JsonObject();
            var checkedGraphics = CheckGraphics(ctx.Model, g, mat.Name);
            // "reset": [fields] takes those back to the default first (hatch and fill to what the name suggests); a null does the same for any other field.
            if (checkedGraphics["reset"] is JsonArray reset) foreach (var f in reset) merged.Remove((string)f!);
            foreach (var (k, v) in checkedGraphics)
            {
                if (k == "reset") continue;
                if (v is null && NullResets(k)) merged.Remove(k);
                else merged[k] = v?.DeepClone();
            }
            if (merged.Count > 0) attrs[kind] = merged; else attrs.Remove(kind);
        }
        return mat.With(name: ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : null, attrs: attrs);
    }

    /// <summary>The sample a material's graphics draw (material.preview, decision 670).</summary>
    public static JsonObject Preview(IQueryContext q, JsonObject a)
    {
        var kind = (string?)a["kind"] ?? "section";
        if (!MaterialGraphics.Kinds.Contains(kind)) throw new CommandException($"A kind of view is {string.Join(", ", MaterialGraphics.Kinds)}.");
        var scale = Json.Number(a["scale"]) ?? 50;
        if (!(scale >= 1 && scale <= 10000)) throw new CommandException("A scale is 1 to 10000.");
        var width = Json.Number(a["width"]) ?? 60;
        var height = Json.Number(a["height"]) ?? 15;
        if (!(width >= 5 && width <= 500 && height >= 1 && height <= 200)) throw new CommandException("A sample is 5–500 mm wide and 1–200 mm high.");
        var layout = (string?)a["layout"] ?? "strip";
        if (layout is not ("strip" or "band")) throw new CommandException("A sample's layout is strip or band.");
        var mat = (string?)a["id"] is { Length: > 0 } id ? Spatial.Require(q.Model, id, IfcClass.Material)
            : new Entity("preview", IfcClass.Material, ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : "Sample", []);
        if (a["graphics"] is JsonObject draft)
        {
            var attrs = mat.CloneAttrs();
            var merged = attrs[kind] as JsonObject is { } had ? (JsonObject)had.DeepClone() : new JsonObject();
            var checkedGraphics = CheckGraphics(q.Model, draft, mat.Name);
            if (checkedGraphics["reset"] is JsonArray reset) foreach (var f in reset) merged.Remove((string)f!);
            foreach (var (k, v) in checkedGraphics)
            {
                if (k == "reset") continue;
                if (v is null && NullResets(k)) merged.Remove(k); else merged[k] = v?.DeepClone();
            }
            attrs[kind] = merged;
            if (a["color"] is JsonValue c && (string?)c is { } cs && HexColor.IsColour(cs)) attrs["color"] = cs;
            mat = mat.With(attrs: attrs);
        }
        var look = MaterialGraphics.Resolve(mat, kind, paperSurfaces: MaterialHatchUpgrade.PaperSurfaces(q.Model));
        var book = Book(q.Model);
        var project = Spatial.Project(q.Model);
        var options = new Lapin.Drawing.Views.Pens.Options(scale, Patterns: Patterns(q.Model), Table: LineStyles.ReadTable(project?.Attrs["penTable"]), LineTypes: book.LineTypes,
            LineStyle: LineStyles.For(project?.Attrs["objectStyles"] as JsonObject, null, null, LineStyles.ReadTable(project?.Attrs["penTable"]), scale, book.LineTypes));
        var (pens, bounds) = Lapin.Drawing.Views.MaterialSample.Draw(look, kind, scale, width, height, layout == "strip", options);
        return new JsonObject
        {
            ["kind"] = kind, ["scale"] = scale, ["lineCap"] = book.LineCap, ["layout"] = layout,
            ["pens"] = new JsonArray(pens.Select(p => (JsonNode)p.ToJson()).ToArray()),
            ["bounds"] = new JsonArray(bounds.Min.X, bounds.Min.Y, bounds.Max.X, bounds.Max.Y),
            ["resolved"] = look.ToJson(),
        };
    }

    /// <summary>A name no material has: the one asked for, else with a number after it.</summary>
    static string FreeName(IModelView m, string name)
    {
        return Lapin.Model.Names.Free(n => Materials.Find(m, n) != null, name);
    }

    /// <summary>Whether a JSON key names a material (material, gutterMaterial, materials: {frame…}).</summary>
    static bool MaterialKey(string? key) => key != null && key.Contains("material", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Every use of <paramref name="from"/> made a use of <paramref name="to"/>: its id anywhere in an entity's attributes
    /// (layer sets, zones, a column's type, a finish), its name under a key that names a material (a door's frame, a sill,
    /// a roof's gutter), and the relationships it is in (IfcRelAssociatesMaterial). How many entities and relationships changed.
    /// </summary>
    public static int Replace(IEdit edit, Entity from, Entity to)
    {
        var n = 0;
        foreach (var e in edit.Model.Entities.ToList())
        {
            if (e.Id == from.Id || e.Id == to.Id) continue;
            var text = e.Attrs.ToJsonString();
            if (!text.Contains(from.Id, StringComparison.Ordinal) && !text.Contains(from.Name, StringComparison.OrdinalIgnoreCase)) continue;
            var attrs = e.CloneAttrs();
            if (Swap(attrs, null, false, from, to)) { edit.Replace(e.With(attrs: attrs)); n++; }
        }
        foreach (var r in edit.Model.RelationsOf(from.Id).ToList())
        {
            var related = r.Related.Select(x => x == from.Id ? to.Id : x).Distinct().ToList();
            edit.ReplaceRelation(r with { Relating = r.Relating == from.Id ? to.Id : r.Relating, Related = related });
            n++;
        }
        return n;
    }

    /// <summary>A material renamed: where it is named (under a material key), the new name.</summary>
    static void Rename(IEdit edit, string oldName, string newName, string id)
    {
        var to = new Entity(id, IfcClass.Material, newName, []);
        var from = new Entity("\u0000none", IfcClass.Material, oldName, []);
        foreach (var e in edit.Model.Entities.ToList())
        {
            if (e.Id == id || !e.Attrs.ToJsonString().Contains(oldName, StringComparison.OrdinalIgnoreCase)) continue;
            var attrs = e.CloneAttrs();
            if (Swap(attrs, null, false, from, to)) edit.Replace(e.With(attrs: attrs));
        }
    }

    /// <summary>Swaps the material's id (anywhere) and name (under a material key) in a JSON tree; whether anything changed.</summary>
    static bool Swap(JsonNode node, string? key, bool named, Entity from, Entity to)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject o:
                foreach (var k in o.Select(kv => kv.Key).ToList())
                {
                    var v = o[k];
                    if (v is JsonValue jv && jv.TryGetValue<string>(out var s))
                    {
                        if (s == from.Id) { o[k] = to.Id; changed = true; }
                        else if ((named || MaterialKey(k)) && string.Equals(s, from.Name, StringComparison.OrdinalIgnoreCase)) { o[k] = to.Name; changed = true; }
                    }
                    else if (v != null) changed |= Swap(v, k, named || MaterialKey(k), from, to);
                }
                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    var v = arr[i];
                    if (v is JsonValue jv && jv.TryGetValue<string>(out var s))
                    {
                        if (s == from.Id) { arr[i] = to.Id; changed = true; }
                        else if (named && string.Equals(s, from.Name, StringComparison.OrdinalIgnoreCase)) { arr[i] = to.Name; changed = true; }
                    }
                    else if (v != null) changed |= Swap(v, key, named, from, to);
                }
                break;
        }
        return changed;
    }

    public static void Register(IModuleRegistry r)
    {
        // ------------------------------------------------------------ materials
        // A new material, with whatever material.update takes (a preset from the library comes in one step).
        r.Command("material.create", "New material", (ctx, a) =>
        {
            var name = ((string?)a["name"])?.Trim();
            if (string.IsNullOrEmpty(name)) throw new CommandException("A material needs a name.");
            if (Materials.Find(ctx.Model, name) != null) throw new CommandException($"There is already a material „{name}”.");
            var color = (string?)a["color"];
            if (color != null && !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) throw new CommandException("A colour is #rrggbb.");
            var mat = Materials.Ensure(ctx, name, color);
            var rest = (JsonObject)a.DeepClone();
            rest.Remove("name");
            if (rest.Count > 0) ctx.Replace(Updated(ctx, mat, rest));
            return new JsonObject { ["id"] = mat.Id };
        });

        // A copy of a material under a new name (its look, graphics and image), unused.
        r.Command("material.duplicate", "Duplicate material", (ctx, a) =>
        {
            var mat = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Material);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } given ? given : FreeName(ctx.Model, mat.Name + " copy");
            if (Materials.Find(ctx.Model, name) != null) throw new CommandException($"There is already a material „{name}”.");
            var copy = new Entity(ctx.NewId(), IfcClass.Material, name, mat.CloneAttrs());
            ctx.Add(copy);
            return new JsonObject { ["id"] = copy.Id };
        });

        // Everything that uses one material uses another: layers, zones, types, joinery parts, elements (one undo step); the first deleted on request.
        r.Command("material.replace", "Replace material", (ctx, a) =>
        {
            var from = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Material);
            var to = Spatial.Require(ctx.Model, (string?)a["with"], IfcClass.Material);
            if (from.Id == to.Id) throw new CommandException("A material is replaced by another.");
            var n = Replace(ctx, from, to);
            if (a["delete"] is JsonValue dv && dv.TryGetValue<bool>(out var del) && del) ctx.Remove(from.Id);
            return new JsonObject { ["changed"] = n };
        });

        // The elements whose body has a part in the material (a wall's layer, a window's frame), to select them.
        r.Query("material.uses", (q, a) =>
        {
            var id = (string?)a["id"] ?? "";
            var o = new JsonArray();
            foreach (var e in q.Model.Entities)
            {
                if (e.Class == IfcClass.Material) continue;
                ElementGeometry? g;
                try { g = q.Geometry(e.Id); }
                catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException or CommandException) { continue; }
                if (g?.Parts.Any(p => p.Material == id) == true) o.Add((JsonNode)e.Id);
            }
            return o;
        });

        // One step: the colour, transparency, category, membrane and joints, each kind of view's graphics (null: back to the name / the section),
        // and its data as material.setData takes it (description, params, measure).
        // With ids (decision 670): the same change on several materials at once, one undo step (copying one material's graphics onto others); no renaming then.
        r.Command("material.update", "Change material", (ctx, a) =>
        {
            if (a["ids"] is JsonArray many)
            {
                if (a.ContainsKey("id")) throw new CommandException("Give id or ids, not both.");
                if (a.ContainsKey("name")) throw new CommandException("Several materials cannot take one name.");
                var ids = many.Select(x => (string?)x).Distinct().ToList();
                if (ids.Count == 0) throw new CommandException("No material to change.");
                foreach (var id in ids) Update(ctx, Spatial.Require(ctx.Model, id, IfcClass.Material), a);
                return new JsonObject { ["changed"] = ids.Count };
            }
            Update(ctx, Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Material), a);
            return null;
        });

        static void Update(ICommandContext ctx, Entity mat, JsonObject a)
        {
            if (((string?)a["name"])?.Trim() is { Length: > 0 } n && !string.Equals(n, mat.Name, StringComparison.OrdinalIgnoreCase) && Materials.Find(ctx.Model, n) != null)
                throw new CommandException($"There is already a material „{n}”.");
            var renamed = ((string?)a["name"])?.Trim() is { Length: > 0 } nn && nn != mat.Name ? nn : null;
            var updated = Updated(ctx, mat, a);
            ctx.Replace(updated);
            if (MaterialGraphics.JoinsOf(mat) != MaterialGraphics.JoinsOf(updated)) Rejoin(ctx, updated.Id, MaterialGraphics.JoinsOf(updated));
            // A material named elsewhere by its name (a door's frame, a sill, a zone) follows the new name.
            if (renamed != null) Rename(ctx, mat.Name, renamed, mat.Id);
        }

        // What a material draws like, on a sample drawn by the drawings' own path (decision 670): the configurator's preview.
        // {id?, graphics?, kind (section|plan|elevation), scale, width?, height? (paper mm), layout? (strip|band)}: the material's
        // graphics with the unsaved ones (graphics, checked as material.update checks a kind's) over them; the pens as view.drawing
        // gives them (view metres, y up), their bounds, the scale.
        r.Query("material.preview", (q, a) => Preview(q, a));

        // What the material says of itself (docs/plan-buildup-tags.md), in one step: its description (text, several lines), its
        // parameters ([{name, kind, value, pset?, options?}] as element.setParam checks them; a standard set's only with its own
        // names: Pset_MaterialThermal.ThermalConductivity), how it is measured ({by: area|volume|mass|count, thicknesses?: [mm],
        // unit?, board?: [length, width]}). A key present replaces, null takes it off; the others stay.
        r.Command("material.setData", "Change material data", (ctx, a) =>
        {
            var mat = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Material);
            if (!a.ContainsKey("description") && !a.ContainsKey("params") && !a.ContainsKey("measure")) throw new CommandException("Nothing to change.");
            var attrs = mat.CloneAttrs();
            MaterialFields.Apply(attrs, a);
            ctx.Replace(mat.With(attrs: attrs));
            return null;
        });

        // Back to what the name suggests: the material keeps its colour.
        r.Command("material.reset", "Reset material", (ctx, a) =>
        {
            var mat = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Material);
            var attrs = mat.CloneAttrs();
            foreach (var k in MaterialGraphics.Kinds.Append("membrane").Append("drawJoints")) attrs.Remove(k);
            ctx.Replace(mat.With(attrs: attrs));
            return null;
        });

        r.Command("material.delete", "Delete material", (ctx, a) =>
        {
            var mat = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Material);
            // In use, it goes only with another to take its place (replaceWith), in the same step.
            if ((string?)a["replaceWith"] is { Length: > 0 } with)
            {
                var to = Spatial.Require(ctx.Model, with, IfcClass.Material);
                if (to.Id == mat.Id) throw new CommandException("A material is replaced by another.");
                Replace(ctx, mat, to);
            }
            else if (InUse(ctx.Model, mat.Id)) throw new CommandException($"„{mat.Name}” is in use.");
            ctx.Remove(mat.Id);
            return null;
        });

        // Every material, how it draws in each kind of view, and whether it is used.
        r.Query("material.list", (q, _) =>
        {
        // A project made before real-size hatches (decision 671) suggests its surfaces' paper ones.
        var paper = MaterialHatchUpgrade.PaperSurfaces(q.Model);
        return new JsonArray(q.Model.OfClass(IfcClass.Material).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(mat =>
        {
            var o = new JsonObject { ["id"] = mat.Id, ["name"] = mat.Name, ["color"] = Materials.ColorOf(q.Model, mat.Id), ["inUse"] = InUse(q.Model, mat.Id), ["category"] = mat.Str("category") };
            // How it renders: its own values and, for the rest, what its name gives.
            o["look"] = MaterialLook.Of(mat).ToJson(mat);
            foreach (var kind in MaterialGraphics.Kinds) o[kind] = MaterialGraphics.Resolve(mat, kind, paperSurfaces: paper).ToJson();
            o["own"] = new JsonObject(MaterialGraphics.Kinds.Where(k => mat.Attrs[k] != null).Select(k => KeyValuePair.Create(k, (JsonNode?)mat.Attrs[k]!.DeepClone())));
            MaterialFields.Write(mat, o);
            return (JsonNode)o;
        }).ToArray());
        });

        // ------------------------------------------------------------ hatch patterns
        // A .pat file as a kind of library file (LibraryMerge): a pattern of a name the project has is that pattern, updated by
        // default (as hatch.import always replaced it), kept both as „NAME_2” (a pattern's name stays one word) or skipped.
        r.Service<ILibraryFormat>(new LibraryFormat(LibraryFile.Of("autocad-pat"), (q, text) => PatternRows(q.Model, text), (ctx, text, choices, all) =>
        {
            var (incoming, _) = Pat.Parse(text);
            if (incoming.Count == 0) throw new CommandException("The file has no hatch pattern.");
            var mine = Patterns(ctx.Model).ToList();
            var tally = new LibraryMerge.Tally();
            foreach (var (p, row) in incoming.Zip(PatternRows(ctx.Model, text)))
            {
                var i = mine.FindIndex(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                switch (LibraryMerge.Decide(row, choices, all))
                {
                    case LibraryMerge.Add: mine.Add(p); tally.Added++; break;
                    case LibraryMerge.Update: mine[i] = p; tally.Updated++; break;
                    case LibraryMerge.Both:
                        var n = 2;
                        while (mine.Any(x => string.Equals(x.Name, $"{p.Name}_{n}", StringComparison.OrdinalIgnoreCase))) n++;
                        mine.Add(p with { Name = $"{p.Name}_{n}" });
                        tally.Added++;
                        tally.Renamed.Add($"{p.Name}_{n}");
                        break;
                    case LibraryMerge.Skip: tally.Skipped++; break;
                    default: tally.Kept++; break;
                }
            }
            var project = Project(ctx);
            var attrs = project.CloneAttrs();
            attrs["hatchPatterns"] = Pat.Serialize(mine);
            ctx.Replace(project.With(attrs: attrs));
            var o = tally.ToJson();
            o["count"] = incoming.Count;
            return o;
        }));

        r.Command("hatch.import", "Import hatch patterns", (ctx, a) =>
        {
            var (incoming, errors) = Pat.Parse((string?)a["text"] ?? "");
            if (incoming.Count == 0) throw new CommandException(errors.FirstOrDefault() ?? "The file has no hatch pattern.");
            var project = Project(ctx);
            var names = incoming.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var merged = Patterns(ctx.Model).Where(p => !names.Contains(p.Name)).Concat(incoming).ToList();
            var attrs = project.CloneAttrs();
            attrs["hatchPatterns"] = Pat.Serialize(merged);
            ctx.Replace(project.With(attrs: attrs));
            return new JsonObject { ["count"] = incoming.Count, ["errors"] = new JsonArray(errors.Select(e => (JsonNode)e).ToArray()) };
        });

        r.Command("hatch.remove", "Remove hatch pattern", (ctx, a) =>
        {
            var name = (string?)a["name"] ?? "";
            var project = Project(ctx);
            var left = Patterns(ctx.Model).Where(p => !p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
            var attrs = project.CloneAttrs();
            if (left.Count > 0) attrs["hatchPatterns"] = Pat.Serialize(left); else attrs.Remove("hatchPatterns");
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });

        // The patterns a material can take: the project's, then the built-in ones it does not override.
        r.Query("hatch.list", (q, _) =>
        {
            var own = Patterns(q.Model);
            var names = own.Select(p => p.Name).ToHashSet();
            return new JsonArray(own.Select(p => (p, true)).Concat(HatchLibrary.BuiltInPatterns.Where(p => !names.Contains(p.Name)).Select(p => (p, false)))
                .Select(x => (JsonNode)new JsonObject { ["name"] = x.p.Name, ["description"] = x.p.Description, ["solid"] = x.p.Solid, ["project"] = x.Item2, ["units"] = HatchScale.Name(x.p.Units) }).ToArray());
        });

        // A pattern's strokes over a square (paper mm): the swatches of the materials dialog. A real-size pattern (decision 671) is
        // drawn at 1:viewScale (default 20, where a brick reads as one), or at the units asked ({units: "paper"|"model"}).
        r.Query("hatch.preview", (q, a) =>
        {
            var size = Json.Number(a["size"]) ?? 20;
            var pattern = HatchLibrary.Find((string?)a["pattern"] ?? "", Patterns(q.Model));
            if (pattern == null) return new JsonObject { ["solid"] = false, ["segments"] = new JsonArray() };
            var units = HatchScale.UnitsOf(pattern, HatchScale.Parse((string?)a["units"]));
            var viewScale = Json.Number(a["viewScale"]) is { } vs && vs > 0 && double.IsFinite(vs) ? vs : PreviewScale;
            // Paper mm per pattern unit: a paper pattern's are paper mm; a model one's the object's, at 1:viewScale.
            var per = (Json.Number(a["scale"]) ?? 1) * (units == HatchUnits.Model ? 1 / viewScale : 1);
            Vec2[] box = [new(0, 0), new(size, 0), new(size, size), new(0, size)];
            // The batt's swatch: a band a third of the square high, its loops along it.
            var segs = BattHatch.Is(pattern)
                ? BattHatch.Segments([[new(0, size / 3), new(size, size / 3), new(size, 2 * size / 3), new(0, 2 * size / 3)]], 0, 4000)
                : HatchFill.Segments([box], pattern, new HatchOptions(per, Json.Number(a["angle"]) ?? 0, MaxSegments: 4000)).Segments;
            return new JsonObject
            {
                ["solid"] = pattern.Solid,
                ["units"] = HatchScale.Name(units),
                ["viewScale"] = units == HatchUnits.Model ? viewScale : null,
                ["segments"] = new JsonArray(segs.SelectMany(s => new[] { s.A.X, s.A.Y, s.B.X, s.B.Y }).Select(v => (JsonNode)Math.Round(v, 4)).ToArray()),
            };
        });

        // ------------------------------------------------------------ drafting standards
        // The standards, and the scales a view's picker offers (the project's own, else the usual ones: decision 492).
        r.Query("standards.get", (q, _) =>
        {
            var book = Book(q.Model);
            var o = Standards.ToJson(book);
            o["scaleChoices"] = new JsonArray(Standards.ScalesOf(book).Select(x => (JsonNode)x).ToArray());
            return o;
        });

        // Add or change one entry: a text, dimension or axis style, a tag type, a layer or a layer group (read as a file would be: tolerantly).
        r.Command("standards.set", "Change drafting standards", (ctx, a) =>
        {
            var key = ListKey((string?)a["kind"]);
            if (a["entry"] is not JsonObject entry) throw new CommandException("Nothing to set.");
            var id = (string?)entry["id"] is { Length: > 0 } given ? given : $"{(string?)a["kind"]}-{ctx.NewId()[..8]}";
            entry = (JsonObject)entry.DeepClone();
            entry["id"] = id;
            var raw = Standards.ToJson(Book(ctx.Model));
            var list = raw[key] as JsonArray ?? [];
            var at = list.Select((x, i) => (x, i)).FirstOrDefault(p => (string?)p.x?["id"] == id);
            if (at.x is JsonObject old)
            {
                var merged = (JsonObject)old.DeepClone();
                foreach (var (k, v) in entry) merged[k] = v?.DeepClone();
                list[at.i] = merged;
            }
            else
            {
                // A new dimension style writes openings' heights as the project's Standard one does (decision 338).
                if ((string?)a["kind"] == "dim" && entry["openingHeight"] == null) entry["openingHeight"] = Standards.DimOf(Book(ctx.Model), Standards.Standard).OpeningHeight;
                list.Add((JsonNode)entry);
            }
            // A tag rule (issue #72): its condition a formula that reads, its type one of the standards'.
            if ((string?)a["kind"] == "tagRule" && list.OfType<JsonObject>().FirstOrDefault(x => (string?)x["id"] == id) is { } rule) TagRules.Check(rule, Book(ctx.Model));
            raw[key] = list;
            // A table style (issue #71, stage 3): a name and a table of rows and columns, or it is refused rather than dropped.
            if ((string?)a["kind"] == "table" && !(Standards.Read(raw).TableStyles ?? []).Any(t => t.Id == id))
                throw new CommandException("A table style needs a name (80 characters at most) and a table with its rows and columns.");
            if ((string?)a["kind"] == "axis" && a["default"] is JsonValue dv && dv.TryGetValue<bool>(out var isDefault) && isDefault) raw["defaultAxisStyle"] = id;
            Write(ctx, raw);
            return new JsonObject { ["id"] = id };
        });

        // Remove an entry (not a Standard one, not layer 0): what used it falls back to Standard (layers: to 0).
        r.Command("standards.remove", "Change drafting standards", (ctx, a) =>
        {
            var kind = (string?)a["kind"];
            var key = ListKey(kind);
            var id = (string?)a["id"] ?? "";
            if (id == Standards.Standard && kind is not ("tag" or "layerGroup" or "tagRule" or "table") || kind == "layer" && id == "0") throw new CommandException("The standard entry stays.");
            var raw = Standards.ToJson(Book(ctx.Model));
            raw[key] = new JsonArray(((raw[key] as JsonArray) ?? []).Where(x => (string?)x?["id"] != id).Select(x => x?.DeepClone()).ToArray());
            void Repoint(string listKey, string field)
            {
                foreach (var x in (raw[listKey] as JsonArray ?? []).OfType<JsonObject>()) if ((string?)x[field] == id) x[field] = kind == "layerGroup" ? null : Standards.Standard;
            }
            if (kind == "text") { Repoint("dimStyles", "textStyle"); Repoint("axisStyles", "textStyle"); }
            if (kind == "layerGroup") Repoint("layers", "group");
            if (kind == "axis" && (string?)raw["defaultAxisStyle"] == id) raw["defaultAxisStyle"] = Standards.Standard;
            // A tag type gone takes its rules with it (issue #72).
            if (kind == "tag" && raw["tagRules"] is JsonArray rules)
                raw["tagRules"] = new JsonArray(rules.Where(x => (string?)x?["tagType"] != id).Select(x => x?.DeepClone()).ToArray());
            Write(ctx, raw);
            return null;
        });

        // An entry moved to another place in its list (index from 0, clamped): the tag rules' order is the order they are tried in (issue #72).
        r.Command("standards.move", "Change drafting standards", (ctx, a) =>
        {
            var key = ListKey((string?)a["kind"]);
            var id = (string?)a["id"] ?? "";
            if (Lapin.Model.Json.Number(a["index"]) is not { } to || !double.IsFinite(to)) throw new CommandException("Where to? An index in the list, from 0.");
            var raw = Standards.ToJson(Book(ctx.Model));
            var list = (raw[key] as JsonArray ?? []).Select(x => x?.DeepClone()).ToList();
            var from = list.FindIndex(x => (string?)x?["id"] == id);
            if (from < 0) throw new CommandException("There is no such entry.");
            var item = list[from];
            list.RemoveAt(from);
            list.Insert(Math.Clamp((int)Math.Round(to), 0, list.Count), item);
            raw[key] = new JsonArray([.. list]);
            Write(ctx, raw);
            return null;
        });

        // The project's options: the unit the interface shows lengths in (mm, cm, m, ft-in, in), the step areas are shown to (areaRounding), how lines end on screen and paper (round, square), whether dimensions drop their trailing zeros (suppressZeros), whether values that do not fit are moved clear of their neighbours (dimAutoText) and past how many paper mm they take a leader (dimTextLeader), the decimals and trailing zeros levels are written with (levelDecimals 0–4, levelZeros project, keep or suppress), the section and elevation markers' sizes (sectionMarkSize, sectionMarkText, elevationMarkSize, elevationMarkText; markSize and markText both).
        r.Command("standards.options", "Change drafting standards", (ctx, a) =>
        {
            var raw = Standards.ToJson(Book(ctx.Model));
            if (a.ContainsKey("units")) raw["units"] = (string?)a["units"] is { } u && Standards.ProjectUnits.Contains(u) ? u : throw new CommandException($"The units are one of {string.Join(", ", Standards.ProjectUnits)}.");
            // The step the areas of spaces are shown to (report #76, decision 471): 0.1, 0.5 or 1; null (or 0): as measured.
            if (a.ContainsKey("areaRounding"))
            {
                if (Lapin.Model.Json.Number(a["areaRounding"]) is not { } step || step == 0) raw.Remove("areaRounding");
                else raw["areaRounding"] = AreaRounding.Read(step) ?? throw new CommandException("Areas are rounded to 0.1, 0.5 or 1 m², or not at all.");
            }
            if (a.ContainsKey("lineCap")) raw["lineCap"] = (string?)a["lineCap"] is { } c && Standards.LineCaps.Contains(c) ? c : throw new CommandException("Lines end round or square.");
            // Dimensions drop their trailing zeros (2.5, not 2.500), unless their style keeps them (forum #81).
            if (a.ContainsKey("suppressZeros")) raw["suppressZeros"] = a["suppressZeros"] is JsonValue zv && zv.TryGetValue<bool>(out var z) ? z : throw new CommandException("Trailing zeros are suppressed or not (true or false).");
            // Dimension values that do not fit moved clear of their neighbours, with a leader past dimTextLeader paper mm (forum #78); null: the defaults (on, 5 mm).
            if (a.ContainsKey("dimAutoText"))
            {
                if (a["dimAutoText"] is null) raw.Remove("dimAutoText");
                else raw["dimAutoText"] = a["dimAutoText"] is JsonValue av && av.TryGetValue<bool>(out var auto) ? auto : throw new CommandException("Automatic text placement is on or off (true or false).");
            }
            if (a.ContainsKey("dimTextLeader"))
            {
                if (a["dimTextLeader"] is null) raw.Remove("dimTextLeader");
                else raw["dimTextLeader"] = Standards.TextLeaderOf(a["dimTextLeader"]) ?? throw new CommandException($"A moved value takes a leader beyond 0 to {Standards.MaxTextLeader} mm on paper.");
            }
            // Levels (+3.00): their decimals and trailing zeros, wherever a drawing writes one (follow-up to forum #81); null: the defaults.
            if (a.ContainsKey("levelDecimals"))
            {
                if (a["levelDecimals"] is null) raw.Remove("levelDecimals");
                else raw["levelDecimals"] = Standards.LevelDecimalsOf(a["levelDecimals"]) ?? throw new CommandException($"Levels are written with 0 to {Standards.MaxLevelDecimals} decimals.");
            }
            if (a.ContainsKey("levelZeros"))
            {
                if (a["levelZeros"] is null) raw.Remove("levelZeros");
                else raw["levelZeros"] = (string?)a["levelZeros"] is { } lz && Standards.ZeroChoices.Contains(lz) ? lz : throw new CommandException("A level's trailing zeros are as the project's, kept or suppressed (project, keep, suppress).");
            }
            // The section and elevation markers' symbol size and text height (paper mm, decision 455); null: the defaults.
            void Mark(string key, double min, double max, string what)
            {
                if (!a.ContainsKey(key)) return;
                if (a[key] == null) { raw.Remove(key); return; }
                raw[key] = Standards.MarkValue(a[key], min, max) ?? throw new CommandException($"A marker's {what} is {min}–{max} mm.");
            }
            // A section's and an elevation's apart (decision 460); decision 455's single pair still sets both.
            foreach (var kind in new[] { "section", "elevation" })
            {
                if (a.ContainsKey("markSize") && !a.ContainsKey(kind + "MarkSize")) a[kind + "MarkSize"] = a["markSize"]?.DeepClone();
                if (a.ContainsKey("markText") && !a.ContainsKey(kind + "MarkText")) a[kind + "MarkText"] = a["markText"]?.DeepClone();
                Mark(kind + "MarkSize", Standards.MinMarkSize, Standards.MaxMarkSize, "symbol size");
                Mark(kind + "MarkText", Standards.MinMarkText, Standards.MaxMarkText, "text height");
            }
            raw.Remove("markSize");
            raw.Remove("markText");
            // The project's standard scales and each kind's scale for a new view (forum #30, decision 492); null: the usual ones, 1:100.
            if (a.ContainsKey("scales"))
            {
                if (a["scales"] is not JsonArray { Count: > 0 } list) raw.Remove("scales");
                else if (list.Any(x => Standards.ViewScale(x) == null)) throw new CommandException($"A scale is 1:{Standards.MinViewScale} to 1:{Standards.MaxViewScale}.");
                else raw["scales"] = new JsonArray(Standards.ViewScales(list)!.Select(x => (JsonNode)x).ToArray());
            }
            foreach (var key in new[] { "planScale", "sectionScale", "elevationScale" })
            {
                if (!a.ContainsKey(key)) continue;
                if (a[key] == null) raw.Remove(key);
                else raw[key] = Standards.ViewScale(a[key]) ?? throw new CommandException($"A scale is 1:{Standards.MinViewScale} to 1:{Standards.MaxViewScale}.");
            }
            Write(ctx, raw);
            return null;
        });

        // A whole standards file (another project's, an office template): read tolerantly, replacing the project's.
        r.Command("standards.replace", "Load drafting standards", (ctx, a) =>
        {
            Write(ctx, Standards.ToJson(Standards.Read(a["standards"])));
            return null;
        });
    }

    static string ListKey(string? kind) => kind switch
    {
        "text" => "textStyles", "dim" => "dimStyles", "axis" => "axisStyles", "tag" => "tagTypes", "layer" => "layers", "layerGroup" => "layerGroups", "tagRule" => "tagRules",
        "table" => "tableStyles",
        _ => throw new CommandException("A standard is a text, dim, axis, tag, tagRule, table, layer or layerGroup."),
    };

    /// <summary>The project's drafting standards replaced by <paramref name="raw"/>, read as a file is (one change of the step).</summary>
    public static void Write(ICommandContext ctx, JsonObject raw)
    {
        var project = Project(ctx);
        var attrs = project.CloneAttrs();
        attrs["drafting"] = Standards.ToJson(Standards.Read(raw));
        ctx.Replace(project.With(attrs: attrs));
    }
}
