using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// Families of the user's own, a light parametric object (docs/plan-user-extensibility.md §5.6): a <c>LapinFamily</c> of the project
/// holds parameters (lengths, numbers, angles, yes/no, with defaults) and bodies, each a closed outline on a plane of the object
/// (xy lying, xz or yz standing) whose corners are numbers or formulas of the parameters (<c>W - T</c>), pushed by a depth (a
/// formula too), set off by an offset, and made only when its condition holds (<c>if</c>-free: <c>HasShelf</c>, <c>W &gt; 1.2</c>).
/// Drawn in Drafting it becomes one with „Make family from sketch”: each closed outline a body, its width and height made
/// parameters W and H that its right and top corners follow. An instance (<c>LapinFamilyInstance</c>) stands at a point, turned,
/// maybe mirrored, with values of its own over the family's defaults; it is exported as the IFC class the family names. Its body is
/// worked out on every ask (the one rule: derived, never stored).
/// </summary>
public static class Families
{
    public const string Class = "LapinFamily";
    public const string InstanceClass = "LapinFamilyInstance";
    public const string FileFormat = "lapin-family";
    public static readonly string[] Planes = ["xy", "xz", "yz"];
    public static readonly string[] ParamKinds = ["length", "number", "angle", "bool"];

    sealed record Param(string Key, string Label, string Kind, double Default);

    static double? Num(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? (b ? 1 : 0) : Json.Number(n);

    static List<Param> Params(JsonObject def) =>
        [.. (def["params"] as JsonArray ?? []).OfType<JsonObject>().Select(p => new Param((string)p["key"]!, (string?)p["label"] ?? (string)p["key"]!, (string?)p["kind"] ?? "length", Num(p["default"]) ?? 0))];

    /// <summary>The values a body is built with: the family's defaults, an instance's own over them.</summary>
    static Dictionary<string, double> Values(JsonObject def, JsonObject? own)
    {
        var o = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Params(def)) o[p.Key] = p.Default;
        foreach (var (k, v) in own ?? []) if (o.ContainsKey(k) && Num(v) is { } x) o[k] = x;
        return o;
    }

    static double Eval(JsonNode? n, IReadOnlyDictionary<string, double> values, string what)
    {
        if (n is null) return 0;
        var text = n is JsonValue jv && jv.TryGetValue<double>(out var d) ? d.ToString("R", CultureInfo.InvariantCulture) : n.ToString();
        var (v, error) = Expr.TryEval(text, k => values.TryGetValue(k, out var x) ? x : null);
        return v ?? throw new CommandException($"{what}: {error}");
    }

    /// <summary>A body's local frame: its outline's x and y on the plane, its depth along the plane's normal.</summary>
    static (Vec3 U, Vec3 V, Vec3 N) Frame(string plane) => plane switch
    {
        "xz" => (new Vec3(1, 0, 0), Vec3.UnitZ, new Vec3(0, 1, 0)),
        "yz" => (new Vec3(0, 1, 0), Vec3.UnitZ, new Vec3(1, 0, 0)),
        _ => (new Vec3(1, 0, 0), new Vec3(0, 1, 0), Vec3.UnitZ),
    };

    /// <summary>The bodies of a family for some values, in the object's own frame (metres): each with its name and material role.</summary>
    public static List<(string Name, string? Material, Solid Solid)> Bodies(JsonObject def, IReadOnlyDictionary<string, double> values)
    {
        var o = new List<(string, string?, Solid)>();
        var i = 0;
        foreach (var b in (def["bodies"] as JsonArray ?? []).OfType<JsonObject>())
        {
            i++;
            var name = (string?)b["name"] is { Length: > 0 } nm ? nm : $"Body {i}";
            if (b["when"] is { } when && Eval(when, values, $"{name}'s condition") == 0) continue;
            var (u, v, n) = Frame((string?)b["plane"] ?? "xy");
            var off = b["offset"] is JsonArray oa && oa.Count == 3 ? new Vec3(Eval(oa[0], values, name), Eval(oa[1], values, name), Eval(oa[2], values, name)) : default;
            List<Vec3> Ring(JsonNode? pts) => [.. (pts as JsonArray ?? []).OfType<JsonArray>().Select(p => off + u * Eval(p[0], values, $"{name}'s corners") + v * Eval(p[1], values, $"{name}'s corners"))];
            var outer = Ring(b["points"]);
            if (outer.Count < 3) continue;
            var holes = (b["holes"] as JsonArray ?? []).Select(h => (IReadOnlyList<Vec3>)Ring(h)).Where(h => h.Count >= 3).ToList();
            var depth = Eval(b["depth"], values, $"{name}'s depth");
            if (Math.Abs(depth) < 1e-6) continue;
            if (Prism.Extrude(outer, holes, n * depth, "family") is { } solid) o.Add((name, (string?)b["material"], solid));
        }
        return o;
    }

    /// <summary>A family as a command or a file gives it, checked: its parameters, bodies whose corners and depths read and make a body with the defaults.</summary>
    public static JsonObject Check(JsonNode? n)
    {
        if (n is not JsonObject o) throw new CommandException("A family is {params, bodies}.");
        var ps = new JsonArray();
        var keys = new List<string>();
        foreach (var p in o["params"] as JsonArray ?? [])
        {
            if (p is not JsonObject po || ((string?)po["key"])?.Trim() is not { Length: > 0 and <= 24 } key || !char.IsLetter(key[0]) || !key.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new CommandException("Each parameter has a key of letters and digits (W, Height, HasShelf).");
            if (keys.Contains(key, StringComparer.OrdinalIgnoreCase)) throw new CommandException($"The parameter „{key}” is there twice.");
            keys.Add(key);
            var kind = (string?)po["kind"] ?? "length";
            if (!ParamKinds.Contains(kind)) throw new CommandException($"A parameter is a {string.Join(", ", ParamKinds)}.");
            var d = Num(po["default"]) ?? throw new CommandException($"{key} needs its default.");
            if (!double.IsFinite(d) || Math.Abs(d) > 1000) throw new CommandException($"{key}'s default is a number within ±1000.");
            var pc = new JsonObject { ["key"] = key, ["label"] = ((string?)po["label"])?.Trim() is { Length: > 0 } l ? l : key, ["kind"] = kind, ["default"] = d };
            ps.Add((JsonNode)pc);
        }
        var bodies = new JsonArray();
        foreach (var b in o["bodies"] as JsonArray ?? [])
        {
            if (b is not JsonObject bo) throw new CommandException("Each body is {plane, points, depth}.");
            var plane = (string?)bo["plane"] ?? "xy";
            if (!Planes.Contains(plane)) throw new CommandException($"A body's plane is {string.Join(", ", Planes)}.");
            if (bo["points"] is not JsonArray { Count: >= 3 } pts || pts.Any(p => p is not JsonArray { Count: 2 })) throw new CommandException("A body's outline has three corners at least, each [x, y].");
            bodies.Add((JsonNode)bo.DeepClone());
        }
        if (bodies.Count == 0) throw new CommandException("A family has one body at least.");
        var cls = ((string?)o["ifcClass"])?.Trim() is { Length: > 3 } c && c.StartsWith("Ifc", StringComparison.Ordinal) ? c : IfcClass.BuildingElementProxy;
        var def = new JsonObject { ["ifcClass"] = cls, ["params"] = ps, ["bodies"] = bodies };
        if (((string?)o["description"])?.Trim() is { Length: > 0 } desc) def["description"] = desc;
        if (((string?)o["planSymbol"])?.Trim() is { Length: > 0 } sym) def["planSymbol"] = sym;
        // Built with its defaults: every corner, depth and condition reads, and something is made.
        if (Bodies(def, Values(def, null)).Count == 0) throw new CommandException("With its defaults the family makes no body.");
        return def;
    }

    /// <summary>
    /// Drafted closed outlines made a family (a plan's lying, a section's or an elevation's standing): their corners from the lowest
    /// left one, in metres; the right-most corners following W and the top-most H when <paramref name="parameterize"/> (the width and
    /// height stretch the outline), each outline a body pushed by <paramref name="depth"/> (a parameter D).
    /// </summary>
    public static JsonObject FromOutlines(IReadOnlyList<IReadOnlyList<Vec2>> outlines, bool standing, double depth, bool parameterize)
    {
        var all = outlines.SelectMany(o => o).ToList();
        var (x0, y0, x1, y1) = (all.Min(p => p.X), all.Min(p => p.Y), all.Max(p => p.X), all.Max(p => p.Y));
        var (w, h) = (x1 - x0, y1 - y0);
        static JsonNode N(double v) => JsonValue.Create(Math.Round(v, 6))!;
        JsonNode X(double x) => parameterize && Math.Abs(x - x1) < 1e-6 ? "W" : parameterize && Math.Abs(x - x0) < 1e-6 ? N(0) : N(x - x0);
        JsonNode Y(double y) => parameterize && Math.Abs(y - y1) < 1e-6 ? "H" : parameterize && Math.Abs(y - y0) < 1e-6 ? N(0) : N(y - y0);
        var bodies = new JsonArray();
        var i = 0;
        foreach (var o in outlines)
            bodies.Add((JsonNode)new JsonObject
            {
                ["name"] = $"Body {++i}", ["plane"] = standing ? "xz" : "xy",
                ["points"] = new JsonArray(o.Select(p => (JsonNode)new JsonArray(X(p.X), Y(p.Y))).ToArray()),
                ["depth"] = parameterize ? "D" : N(depth),
            });
        var ps = new JsonArray();
        if (parameterize)
        {
            ps.Add((JsonNode)new JsonObject { ["key"] = "W", ["label"] = "Width", ["kind"] = "length", ["default"] = N(w) });
            ps.Add((JsonNode)new JsonObject { ["key"] = "H", ["label"] = standing ? "Height" : "Depth", ["kind"] = "length", ["default"] = N(h) });
            ps.Add((JsonNode)new JsonObject { ["key"] = "D", ["label"] = standing ? "Thickness" : "Height", ["kind"] = "length", ["default"] = N(depth) });
        }
        return new JsonObject { ["params"] = ps, ["bodies"] = bodies };
    }

    // ------------------------------------------------------------ instances

    static (Vec2 At, double Rotation, bool Mirror) Pose(Entity e) =>
        (Args.TryPoint(e.Attrs["at"]) ?? default, e.Num("rotation"), e.Bool("mirror"));

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices services)
    {
        if (m.Get(e.Str("familyId") ?? "") is not { Class: Class } family || family.Attrs["def"] is not JsonObject def) return null;
        var (at, rotation, mirror) = Pose(e);
        var z = (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("z");
        List<(string Name, string? Material, Solid Solid)> bodies;
        try { bodies = Bodies(def, Values(def, e.Attrs["values"] as JsonObject)); }
        catch (CommandException ex) { return new ElementGeometry([], Diagnostics: [ex.Message]); }
        var (c, s) = (Math.Cos(rotation * Math.PI / 180), Math.Sin(rotation * Math.PI / 180));
        Vec3 Dir(Vec3 d)
        {
            var x = mirror ? -d.X : d.X;
            return new Vec3(x * c - d.Y * s, x * s + d.Y * c, d.Z);
        }
        Vec3 Place(Vec3 p)
        {
            var x = mirror ? -p.X : p.X;
            return new Vec3(at.X + x * c - p.Y * s, at.Y + x * s + p.Y * c, z + p.Z);
        }
        var parts = new List<GeometryPart>();
        foreach (var (name, material, solid) in bodies)
        {
            // Its faces' normals turned with it; turned over (mirrored), each face's loops run the other way.
            var faces = solid.Faces.Select(f =>
            {
                var turned = Dir(f.Normal);
                return mirror ? f with { Outer = [.. f.Outer.Reverse()], Holes = [.. f.Holes.Select(h => h.Reverse().ToArray())], Normal = turned } : f with { Normal = turned };
            }).ToArray();
            var moved = new Solid([.. solid.Vertices.Select(Place)], faces, "family");
            var mat = material != null ? Materials.Find(m, material) : null;
            parts.Add(new GeometryPart(name, mat?.Str("color") ?? "#b9b2a6", moved, mat?.Id, "family:" + name));
        }
        return new ElementGeometry(parts);
    }

    static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null) yield break;
        var volume = g.Parts.Sum(p => Measure.Volume(p.Solid));
        yield return new QuantitySet("Qto_BuildingElementProxyQuantities", [new Quantity("NetVolume", Math.Round(volume, 9), "VOLUME")]);
    }

    /// <summary>Instances moved, turned and mirrored with the other elements: their point, their angle, a mirror turning them over.</summary>
    sealed class Placement : IElementPlacement
    {
        public bool Handles(Entity e) => e.Class == InstanceClass;

        public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
        {
            var (at, rotation, mirror) = Pose(e);
            var attrs = e.CloneAttrs();
            attrs["at"] = new JsonArray(t.Point(at).X, t.Point(at).Y);
            var turned = t.Angle(rotation * Math.PI / 180) * 180 / Math.PI;
            attrs["rotation"] = Math.Round(((turned % 360) + 360) % 360, 9);
            if (t.Mirrored) { attrs["mirror"] = !mirror; attrs["rotation"] = Math.Round((((turned + 180) % 360) + 360) % 360, 9); }
            ctx.Replace(e.With(attrs: attrs));
        }
    }

    static Entity Require(IModelView m, string? id) => m.Get(id ?? "") is { Class: Class } e ? e : throw new CommandException("There is no such family.");

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Family", [new("description", "Description", "text")], "#b9b2a6", Kind: "type"));
        r.Class(new(InstanceClass, "Family object",
        [
            new("rotation", "Rotation (°)", "number", Min: -360, Max: 360),
            new("z", "Height above the storey", "length"),
            new("mirror", "Mirrored", "bool"),
        ], "#b9b2a6"));
        r.Geometry(InstanceClass, Geometry);
        r.Quantities(InstanceClass, Quantities);
        r.Service<IElementPlacement>(new Placement());

        r.Query("families", (q, _) => new JsonObject
        {
            ["families"] = new JsonArray(q.Model.OfClass(Class).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Select(e => (JsonNode)new JsonObject
            {
                ["id"] = e.Id, ["name"] = e.Name, ["def"] = e.Attrs["def"]?.DeepClone(), ["uses"] = q.Model.OfClass(InstanceClass).Count(i => i.Str("familyId") == e.Id),
            }).ToArray()),
        });

        // A family's bodies for some values (its defaults over them), as corner rings in its own frame: the editor's preview, or why it does not build.
        r.Query("familyPreview", (q, a) =>
        {
            var def = Check(a["def"] ?? Require(q.Model, (string?)a["id"]).Attrs["def"]);
            var bodies = Bodies(def, Values(def, a["values"] as JsonObject));
            return new JsonObject
            {
                ["bodies"] = new JsonArray(bodies.Select(b => (JsonNode)new JsonObject
                {
                    ["name"] = b.Name,
                    ["edges"] = new JsonArray(b.Solid.Faces.SelectMany(f => f.Outer.Select((v, k) => (v, w: f.Outer[(k + 1) % f.Outer.Length])))
                        .Select(e => (JsonNode)new JsonArray(Math.Round(b.Solid.Vertices[e.v].X, 6), Math.Round(b.Solid.Vertices[e.v].Y, 6), Math.Round(b.Solid.Vertices[e.v].Z, 6),
                            Math.Round(b.Solid.Vertices[e.w].X, 6), Math.Round(b.Solid.Vertices[e.w].Y, 6), Math.Round(b.Solid.Vertices[e.w].Z, 6))).ToArray()),
                    ["volume"] = Math.Round(Measure.Volume(b.Solid), 9),
                }).ToArray()),
            };
        });

        r.Command("createFamily", "New family", (ctx, a) =>
        {
            var def = Check(a["def"]);
            var e = new Entity(ctx.NewId(), Class, ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : "Family", new JsonObject { ["def"] = def });
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });

        // A family changed: its objects follow (their own values kept where it still has the parameter).
        r.Command("updateFamily", "Change family", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var attrs = e.CloneAttrs();
            if (a["def"] != null) attrs["def"] = Check(a["def"]);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : null;
            ctx.Replace(e.With(name: name, attrs: attrs));
            return null;
        });

        r.Command("deleteFamily", "Delete family", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var used = ctx.Model.OfClass(InstanceClass).Count(i => i.Str("familyId") == e.Id);
            if (used > 0) throw new CommandException($"{used} object{(used == 1 ? " is" : "s are")} of „{e.Name}”: delete them first.");
            ctx.Remove(e.Id);
            return null;
        });

        // An object of a family: {familyId, storeyId, at: [x, y], z?, rotation?, mirror?, values?}.
        r.Command("placeFamily", "Family object", (ctx, a) =>
        {
            var family = Require(ctx.Model, (string?)a["familyId"]);
            var storey = Spatial.Require(ctx.Model, (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
            var at = Args.Point(a["at"], "where it stands");
            var attrs = new JsonObject
            {
                ["familyId"] = family.Id, ["at"] = new JsonArray(at.X, at.Y), ["rotation"] = Json.Number(a["rotation"]) ?? 0,
                ["exportAs"] = (string?)family.Attrs["def"]?["ifcClass"] ?? IfcClass.BuildingElementProxy,
            };
            if (Json.Number(a["z"]) is { } z) attrs["z"] = z;
            if (a["mirror"] is JsonValue mv && mv.TryGetValue<bool>(out var mirror) && mirror) attrs["mirror"] = true;
            if (a["values"] is JsonObject values) attrs["values"] = Own(family, values, null);
            var e = new Entity(ctx.NewId(), InstanceClass, family.Name, attrs);
            ctx.Add(e);
            Spatial.Contain(ctx, storey.Id, e.Id);
            return new JsonObject { ["id"] = e.Id };
        });

        // Objects' own values ({ids, values}: a value null goes back to the family's default).
        r.Command("setFamilyValues", "Family values", (ctx, a) =>
        {
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) is { Class: InstanceClass } x ? x : throw new CommandException("Not an object of a family.");
                var family = Require(ctx.Model, e.Str("familyId"));
                var attrs = e.CloneAttrs();
                var own = Own(family, a["values"] as JsonObject ?? [], e.Attrs["values"] as JsonObject);
                if (own.Count == 0) attrs.Remove("values"); else attrs["values"] = own;
                ctx.Replace(e.With(attrs: attrs));
                // Built with them, or refused with why.
                if (Geometry(ctx.Model.Get(id)!, ctx.Model, ctx.Services)?.Diagnostics is [{ } problem, ..]) throw new CommandException(problem);
            }
            return null;
        });

        // Drafted closed outlines of a view made a family ({viewId, ids, name, depth, parameterize? (default true), ifcClass?}); the drafting stays.
        r.Command("familyFromSketch", "Family from sketch", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
            var outlines = new List<IReadOnlyList<Vec2>>();
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException("Something chosen is gone.");
                if (e.Attrs["shape"] is not JsonObject shape) continue;
                if ((string?)shape["type"] == "polyline" && shape["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var closed) && closed)
                    outlines.Add([.. Args.StoredPoints(shape["pts"])]);
                else if ((string?)shape["type"] == "circle" && Args.TryPoint(shape["c"]) is { } c && Json.Number(shape["r"]) is { } rad)
                    outlines.Add([.. Enumerable.Range(0, Chords.Circle(rad)).Select(k => c + new Vec2(Math.Cos(2 * Math.PI * k / Chords.Circle(rad)), Math.Sin(2 * Math.PI * k / Chords.Circle(rad))) * rad)]);
            }
            if (outlines.Count == 0) throw new CommandException("A family is made of closed outlines: closed polylines, rectangles or circles.");
            var standing = view.Str("kind") is "section" or "elevation";
            var def = FromOutlines(outlines, standing, Json.Number(a["depth"]) ?? 0.5, a["parameterize"] is not JsonValue pv || !pv.TryGetValue<bool>(out var p) || p);
            if ((string?)a["ifcClass"] is { Length: > 3 } cls) def["ifcClass"] = cls;
            def = Check(def);
            var family = new Entity(ctx.NewId(), Class, ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : "Family", new JsonObject { ["def"] = def });
            ctx.Add(family);
            return new JsonObject { ["id"] = family.Id, ["def"] = def.DeepClone() };
        });

        r.Query("exportFamily", (q, a) =>
        {
            var e = Require(q.Model, (string?)a["id"]);
            var o = new JsonObject { ["format"] = FileFormat, ["version"] = 1, ["name"] = e.Name, ["def"] = e.Attrs["def"]?.DeepClone() };
            return new JsonObject { ["text"] = o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ["file"] = e.Name + ".lapinfamily" };
        });

        r.Command("importFamily", "Import family", (ctx, a) =>
        {
            JsonNode? n;
            try { n = JsonNode.Parse((string?)a["text"] ?? ""); } catch (JsonException) { throw new CommandException("That is not a family file (.lapinfamily)."); }
            if (n is not JsonObject o || (string?)o["format"] != FileFormat) throw new CommandException("That is not a family file (.lapinfamily).");
            var e = new Entity(ctx.NewId(), Class, ((string?)o["name"])?.Trim() is { Length: > 0 } nm ? nm : "Family", new JsonObject { ["def"] = Check(o["def"]) });
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });
    }

    /// <summary>An object's own values over <paramref name="had"/>: each the family's parameter of that key, checked a number; null takes the default back.</summary>
    static JsonObject Own(Entity family, JsonObject values, JsonObject? had)
    {
        var keys = Params((JsonObject)family.Attrs["def"]!).ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
        var o = had?.DeepClone() as JsonObject ?? new JsonObject();
        foreach (var (k, v) in values)
        {
            if (!keys.TryGetValue(k, out var p)) throw new CommandException($"„{family.Name}” has no parameter „{k}”.");
            if (v == null) { o.Remove(p.Key); continue; }
            o[p.Key] = Num(v) ?? throw new CommandException($"{p.Label} is a number.");
        }
        return o;
    }
}
