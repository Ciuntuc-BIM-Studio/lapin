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

    static Entity Project(ICommandContext ctx) => Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");

    static readonly HashSet<string> GraphicsFields = ["hatch", "hatchScale", "fill", "hatchPen", "hatchAngle", "hatchAlign"];

    /// <summary>A view's graphics checked: a known pattern (or null for none), a positive scale, a colour (or "material"), a pen 1–16.</summary>
    static JsonObject CheckGraphics(IModelView m, JsonObject g, string where)
    {
        var o = new JsonObject();
        foreach (var (k, v) in g)
        {
            if (!GraphicsFields.Contains(k)) throw new CommandException($"{where}: „{k}” is not a material graphic.");
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
        // Its own texture: an image of the project (null takes it off; an image nothing uses goes) and how large it lies on the model, metres.
        if (a.ContainsKey("textureImage"))
        {
            var had = attrs["textureImage"] is JsonValue hv ? (string?)hv : null;
            if ((string?)a["textureImage"] is { Length: > 0 } img)
            {
                if (ctx.Model.Get(img) is not { Class: Images.Class }) throw new CommandException("There is no such image in the project.");
                attrs["textureImage"] = img;
            }
            else attrs.Remove("textureImage");
            if (had != null && had != (string?)attrs["textureImage"]) Images.DropIfUnused(ctx, had, mat.Id);
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
        // What it says of itself (docs/plan-buildup-tags.md): its description, its parameters, how it is measured and bought.
        MaterialFields.Apply(attrs, a);
        foreach (var kind in MaterialGraphics.Kinds)
        {
            if (!a.ContainsKey(kind)) continue;
            if (a[kind] is not JsonObject g) { attrs.Remove(kind); continue; }
            // Fields merge into what the view had (replace: they are all it has); a field set to null is "none" (hatch, fill).
            var replace = a["replace"] is JsonValue rv && rv.TryGetValue<bool>(out var rb) && rb;
            var merged = !replace && attrs[kind] as JsonObject is { } had ? (JsonObject)had.DeepClone() : new JsonObject();
            foreach (var (k, v) in CheckGraphics(ctx.Model, g, mat.Name)) merged[k] = v?.DeepClone();
            if (merged.Count > 0) attrs[kind] = merged; else attrs.Remove(kind);
        }
        return mat.With(name: ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : null, attrs: attrs);
    }

    /// <summary>A name no material has: the one asked for, else with a number after it.</summary>
    static string FreeName(IModelView m, string name)
    {
        if (Materials.Find(m, name) == null) return name;
        for (var i = 2; ; i++) if (Materials.Find(m, $"{name} {i}") == null) return $"{name} {i}";
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
        r.Command("material.update", "Change material", (ctx, a) =>
        {
            var mat = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Material);
            if (((string?)a["name"])?.Trim() is { Length: > 0 } n && !string.Equals(n, mat.Name, StringComparison.OrdinalIgnoreCase) && Materials.Find(ctx.Model, n) != null)
                throw new CommandException($"There is already a material „{n}”.");
            var renamed = ((string?)a["name"])?.Trim() is { Length: > 0 } nn && nn != mat.Name ? nn : null;
            var updated = Updated(ctx, mat, a);
            ctx.Replace(updated);
            // A material named elsewhere by its name (a door's frame, a sill, a zone) follows the new name.
            if (renamed != null) Rename(ctx, mat.Name, renamed, mat.Id);
            return null;
        });

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
        r.Query("material.list", (q, _) => new JsonArray(q.Model.OfClass(IfcClass.Material).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(mat =>
        {
            var o = new JsonObject { ["id"] = mat.Id, ["name"] = mat.Name, ["color"] = Materials.ColorOf(q.Model, mat.Id), ["inUse"] = InUse(q.Model, mat.Id), ["category"] = mat.Str("category") };
            // How it renders: its own values and, for the rest, what its name gives.
            o["look"] = MaterialLook.Of(mat).ToJson(mat);
            foreach (var kind in MaterialGraphics.Kinds) o[kind] = MaterialGraphics.Resolve(mat, kind).ToJson();
            o["own"] = new JsonObject(MaterialGraphics.Kinds.Where(k => mat.Attrs[k] != null).Select(k => KeyValuePair.Create(k, (JsonNode?)mat.Attrs[k]!.DeepClone())));
            MaterialFields.Write(mat, o);
            return (JsonNode)o;
        }).ToArray()));

        // ------------------------------------------------------------ hatch patterns
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
                .Select(x => (JsonNode)new JsonObject { ["name"] = x.p.Name, ["description"] = x.p.Description, ["solid"] = x.p.Solid, ["project"] = x.Item2 }).ToArray());
        });

        // A pattern's strokes over a square (paper mm): the swatches of the materials dialog.
        r.Query("hatch.preview", (q, a) =>
        {
            var size = Json.Number(a["size"]) ?? 20;
            var pattern = HatchLibrary.Find((string?)a["pattern"] ?? "", Patterns(q.Model));
            if (pattern == null) return new JsonObject { ["solid"] = false, ["segments"] = new JsonArray() };
            Vec2[] box = [new(0, 0), new(size, 0), new(size, size), new(0, size)];
            // The batt's swatch: a band a third of the square high, its loops along it.
            var segs = BattHatch.Is(pattern)
                ? BattHatch.Segments([[new(0, size / 3), new(size, size / 3), new(size, 2 * size / 3), new(0, 2 * size / 3)]], 0, 4000)
                : HatchFill.Segments([box], pattern, new HatchOptions(Json.Number(a["scale"]) ?? 1, Json.Number(a["angle"]) ?? 0, MaxSegments: 4000)).Segments;
            return new JsonObject
            {
                ["solid"] = pattern.Solid,
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
            raw[key] = list;
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
            if (id == Standards.Standard && kind != "tag" && kind != "layerGroup" || kind == "layer" && id == "0") throw new CommandException("The standard entry stays.");
            var raw = Standards.ToJson(Book(ctx.Model));
            raw[key] = new JsonArray(((raw[key] as JsonArray) ?? []).Where(x => (string?)x?["id"] != id).Select(x => x?.DeepClone()).ToArray());
            void Repoint(string listKey, string field)
            {
                foreach (var x in (raw[listKey] as JsonArray ?? []).OfType<JsonObject>()) if ((string?)x[field] == id) x[field] = kind == "layerGroup" ? null : Standards.Standard;
            }
            if (kind == "text") { Repoint("dimStyles", "textStyle"); Repoint("axisStyles", "textStyle"); }
            if (kind == "layerGroup") Repoint("layers", "group");
            if (kind == "axis" && (string?)raw["defaultAxisStyle"] == id) raw["defaultAxisStyle"] = Standards.Standard;
            Write(ctx, raw);
            return null;
        });

        // The project's options: the unit the interface shows lengths in (mm, cm, m, ft-in, in), the step areas are shown to (areaRounding), how lines end on screen and paper (round, square), whether dimensions drop their trailing zeros (suppressZeros), the section and elevation markers' sizes (sectionMarkSize, sectionMarkText, elevationMarkSize, elevationMarkText; markSize and markText both).
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
        "text" => "textStyles", "dim" => "dimStyles", "axis" => "axisStyles", "tag" => "tagTypes", "layer" => "layers", "layerGroup" => "layerGroups",
        _ => throw new CommandException("A standard is a text, dim, axis, tag, layer or layerGroup."),
    };

    static void Write(ICommandContext ctx, JsonObject raw)
    {
        var project = Project(ctx);
        var attrs = project.CloneAttrs();
        attrs["drafting"] = Standards.ToJson(Standards.Read(raw));
        ctx.Replace(project.With(attrs: attrs));
    }
}
