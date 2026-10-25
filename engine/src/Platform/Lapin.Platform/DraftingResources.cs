using System.Text.Json.Nodes;
using BubbleBim.Geometry;
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
    public static readonly string[] Textures = ["none", "brick", "concrete", "wood", "plaster", "stone", "metal", "insulation", "earth", "tiles"];

    /// <summary>The project's own hatch patterns (imported; a built-in's name overrides it).</summary>
    public static IReadOnlyList<HatchPattern> Patterns(IModelView m) =>
        Spatial.Project(m)?.Str("hatchPatterns") is { Length: > 0 } text ? Pat.Parse(text).Patterns : [];

    public static StyleBook Book(IModelView m) => Standards.Read(Spatial.Project(m)?.Attrs["drafting"]);

    static Entity Project(ICommandContext ctx) => Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");

    static readonly HashSet<string> GraphicsFields = ["hatch", "hatchScale", "fill", "hatchPen"];

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
            }
        }
        return o;
    }

    /// <summary>Whether anything in the model names the entity (an attribute holding its id, a relation).</summary>
    static bool InUse(IModelView m, string id) =>
        m.Entities.Any(e => e.Id != id && e.Attrs.ToJsonString().Contains($"\"{id}\"", StringComparison.Ordinal)) || m.RelationsOf(id).Any();

    public static void Register(IModuleRegistry r)
    {
        // ------------------------------------------------------------ materials
        r.Command("material.create", "New material", (ctx, a) =>
        {
            var name = ((string?)a["name"])?.Trim();
            if (string.IsNullOrEmpty(name)) throw new CommandException("A material needs a name.");
            if (Materials.Find(ctx.Model, name) != null) throw new CommandException($"There is already a material „{name}”.");
            var color = (string?)a["color"];
            if (color != null && !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) throw new CommandException("A colour is #rrggbb.");
            return new JsonObject { ["id"] = Materials.Ensure(ctx, name, color).Id };
        });

        // One step: the colour, transparency, category, membrane and joints, and each kind of view's graphics (null: back to the name / the section).
        r.Command("material.update", "Change material", (ctx, a) =>
        {
            var mat = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Material);
            var attrs = mat.CloneAttrs();
            if (a["color"] is { } c) attrs["color"] = (string?)c is { } cs && System.Text.RegularExpressions.Regex.IsMatch(cs, "^#[0-9a-fA-F]{6}$") ? cs : throw new CommandException("A colour is #rrggbb.");
            if (a.ContainsKey("transparency"))
            {
                if (a["transparency"] is null) attrs.Remove("transparency");
                else attrs["transparency"] = Json.Number(a["transparency"]) is { } t && t >= 0 && t <= 1 ? t : throw new CommandException("Transparency is 0 to 1.");
            }
            if (a.ContainsKey("category")) { if ((string?)a["category"] is { Length: > 0 } cat) attrs["category"] = cat; else attrs.Remove("category"); }
            // Its look in 3D: a texture (brick, concrete, wood, plaster, stone, metal, insulation, earth, tiles; "none"; null: guessed from its hatch and name).
            if (a.ContainsKey("texture"))
            {
                if ((string?)a["texture"] is { Length: > 0 } tex)
                    attrs["texture"] = Textures.Contains(tex) ? tex : throw new CommandException($"„{tex}” is not a texture: {string.Join(", ", Textures)}.");
                else attrs.Remove("texture");
            }
            foreach (var flag in new[] { "membrane", "drawJoints" })
                if (a.ContainsKey(flag))
                {
                    if (a[flag] is JsonValue fv && fv.TryGetValue<bool>(out var b)) attrs[flag] = b; else attrs.Remove(flag);
                }
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
            ctx.Replace(mat.With(name: ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : null, attrs: attrs));
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
            if (InUse(ctx.Model, mat.Id)) throw new CommandException($"„{mat.Name}” is in use.");
            ctx.Remove(mat.Id);
            return null;
        });

        // Every material, how it draws in each kind of view, and whether it is used.
        r.Query("material.list", (q, _) => new JsonArray(q.Model.OfClass(IfcClass.Material).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(mat =>
        {
            var o = new JsonObject { ["id"] = mat.Id, ["name"] = mat.Name, ["color"] = Materials.ColorOf(q.Model, mat.Id), ["inUse"] = InUse(q.Model, mat.Id) };
            foreach (var kind in MaterialGraphics.Kinds) o[kind] = MaterialGraphics.Resolve(mat, kind).ToJson();
            o["own"] = new JsonObject(MaterialGraphics.Kinds.Where(k => mat.Attrs[k] != null).Select(k => KeyValuePair.Create(k, (JsonNode?)mat.Attrs[k]!.DeepClone())));
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
            var (segs, _) = HatchFill.Segments([box], pattern, new HatchOptions(Json.Number(a["scale"]) ?? 1, Json.Number(a["angle"]) ?? 0, MaxSegments: 4000));
            return new JsonObject
            {
                ["solid"] = pattern.Solid,
                ["segments"] = new JsonArray(segs.SelectMany(s => new[] { s.A.X, s.A.Y, s.B.X, s.B.Y }).Select(v => (JsonNode)Math.Round(v, 4)).ToArray()),
            };
        });

        // ------------------------------------------------------------ drafting standards
        r.Query("standards.get", (q, _) => Standards.ToJson(Book(q.Model)));

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
            else list.Add((JsonNode)entry);
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
