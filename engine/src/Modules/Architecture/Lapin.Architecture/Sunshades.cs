using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/*
 * Sunshades (report #56): IfcShadingDevice of an IfcShadingDeviceType whose spec says its kind and shape, so a change to the
 * type is seen on every sunshade of it. Kinds: a canopy (a slab, louvred or glass, on brackets, tie rods or posts), vertical
 * fins, horizontal louvres (brise-soleil), an egg-crate, a perforated screen, a fabric awning, a pergola, an external venetian
 * blind. A sunshade stands on a door or window (sized to its opening, past its jambs by the type's overhangs, over its head or
 * across it), along a wall (between two points on it) or free on a facade line; it follows what it stands on. Its body is
 * built here in its own frame: x along the facade, y out of it, z up from its reference height.
 */

/// <summary>A parameter of a sunshade kind, as the types dialog edits it; <see cref="ShowIf"/>: shown only when another parameter has one of the values.</summary>
public sealed record ShadeParam(string Key, string Label, string Kind, double? Min = null, double? Max = null, string[]? Choices = null, (string Key, string[] Values)? ShowIf = null);

/// <summary>
/// A kind of sunshade: its key, label, IFC predefined type (USERDEFINED with its object type when IFC has none), whether it
/// sits over a window (a canopy: <see cref="Over"/>) or across it (fins, louvres), its parameters and their first values.
/// </summary>
public sealed record ShadeKind(string Key, string Label, string Predefined, string? ObjectType, bool Over, IReadOnlyList<ShadeParam> Params, JsonObject Defaults);

/// <summary>A type's spec read with its kind's defaults under it.</summary>
public sealed class ShadeSpec(JsonObject? o)
{
    public JsonObject Raw { get; } = o ?? [];
    public ShadeKind Kind { get; } = Sunshades.Find((string?)o?["kind"]) ?? Sunshades.Kinds[0];
    JsonNode? Value(string key) => Raw[key] ?? Kind.Defaults[key];
    public double Num(string key, double fallback = 0) => Json.Number(Value(key)) is { } v && double.IsFinite(v) ? v : fallback;
    public string? Str(string key) => Value(key) is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    public bool Bool(string key) => Value(key) is JsonValue v && v.TryGetValue<bool>(out var b) && b;
}

/// <summary>Where a sunshade's body stands: the facade's start, the way along it and out of it, the reference height, its length and height, the storey's floor (world heights).</summary>
public readonly record struct ShadeFrame(Vec2 O, Vec2 U, Vec2 N, double Z, double Length, double Height, double ZFloor, double Sill)
{
    public Vec3 At(double x, double y, double z) => (O + U * x + N * y).At(Z + z);
}

public static class Sunshades
{
    public const string Class = "IfcShadingDevice";
    public const string TypeClass = "IfcShadingDeviceType";
    public const string StylesKey = "sunshadeStyles";
    public const string QuantitySet = "Lapin_SunshadeQuantities";

    static ShadeParam L(string k, string l, double min = 0, double max = 6, (string, string[])? show = null) => new(k, l, "length", min, max, ShowIf: show);
    static ShadeParam N(string k, string l, double min, double max, (string, string[])? show = null) => new(k, l, "number", min, max, ShowIf: show);
    static ShadeParam A(string k, string l, double min, double max, (string, string[])? show = null) => new(k, l, "angle", min, max, ShowIf: show);
    static ShadeParam C(string k, string l, string[] c, (string, string[])? show = null) => new(k, l, "choice", Choices: c, ShowIf: show);
    static ShadeParam B(string k, string l, (string, string[])? show = null) => new(k, l, "bool", ShowIf: show);
    static ShadeParam M(string k, string l, (string, string[])? show = null) => new(k, l, "material", ShowIf: show);
    static JsonObject D(params (string Key, JsonNode? Value)[] v) { var o = new JsonObject(); foreach (var (k, x) in v) o[k] = x; return o; }

    /// <summary>On a window: past its jambs, and over its head (a canopy) or below and above it (fins, louvres).</summary>
    static readonly ShadeParam[] OverWindow = [L("overhangLeft", "Past the left jamb", -1, 3), L("overhangRight", "Past the right jamb", -1, 3), L("gap", "Over the head", -0.5, 2)];
    static readonly ShadeParam[] AcrossWindow = [L("overhangLeft", "Past the left jamb", -1, 3), L("overhangRight", "Past the right jamb", -1, 3), L("below", "Below the sill", 0, 2), L("above", "Above the head", 0, 2), L("height", "Height (on a wall or free)", 0.1, 12)];

    public static readonly IReadOnlyList<ShadeKind> Kinds =
    [
        new("canopy", "Canopy", "USERDEFINED", "Canopy", true,
        [
            L("projection", "Projection", 0.1, 6), L("thickness", "Thickness", 0.01, 0.6), A("tilt", "Tilt (falls outwards)", -30, 45),
            C("surface", "Surface", ["solid", "louvred", "glass"]),
            L("bladeWidth", "Blade width", 0.02, 0.5, ("surface", ["louvred"])), L("bladeSpacing", "Blade spacing", 0.03, 1, ("surface", ["louvred"])),
            A("bladeAngle", "Blade angle", -80, 80, ("surface", ["louvred"])), L("frameWidth", "Frame width", 0.02, 0.3, ("surface", ["louvred"])),
            L("fascia", "Fascia below the front", 0, 0.8, ("surface", ["solid", "louvred"])),
            C("supports", "Supports", ["none", "brackets", "tieRods", "posts"]), L("supportSpacing", "Support spacing", 0.3, 6, ("supports", ["brackets", "tieRods", "posts"])),
            .. OverWindow, M("material", "Material"), M("supportMaterial", "Supports' material", ("supports", ["brackets", "tieRods", "posts"])), M("glassMaterial", "Glass", ("surface", ["glass"])),
        ],
            D(("projection", 0.9), ("thickness", 0.12), ("tilt", 0), ("surface", "solid"), ("bladeWidth", 0.1), ("bladeSpacing", 0.15), ("bladeAngle", 0), ("frameWidth", 0.06), ("fascia", 0),
              ("supports", "none"), ("supportSpacing", 1.5), ("overhangLeft", 0.15), ("overhangRight", 0.15), ("gap", 0.15), ("material", "Concrete"), ("supportMaterial", "Steel black"), ("glassMaterial", "Glass"))),
        new("fins", "Vertical fins", "USERDEFINED", "Fins", false,
        [
            L("projection", "Fin depth", 0.05, 2), L("thickness", "Fin thickness", 0.01, 0.4), L("spacing", "Spacing", 0.1, 4), A("angle", "Angle to the wall's normal", -75, 75),
            L("standoff", "Off the wall", 0, 1), B("rails", "Rails top and bottom"), .. AcrossWindow, M("material", "Material"), M("supportMaterial", "Rails' material", ("rails", ["true"])),
        ],
            D(("projection", 0.35), ("thickness", 0.05), ("spacing", 0.6), ("angle", 0), ("standoff", 0), ("rails", false), ("overhangLeft", 0), ("overhangRight", 0), ("below", 0), ("above", 0), ("height", 2.4),
              ("material", "Aluminium anodised"), ("supportMaterial", "Aluminium anodised"))),
        new("louvres", "Louvres (brise-soleil)", "LOUVER", null, false,
        [
            L("bladeDepth", "Blade depth", 0.03, 1), L("bladeThickness", "Blade thickness", 0.003, 0.2), L("spacing", "Spacing", 0.05, 1.5), A("angle", "Blade angle", -80, 80),
            C("profile", "Blade profile", ["flat", "aerofoil", "box"]), L("standoff", "Off the wall", 0, 1.5), B("endPlates", "End plates"),
            .. AcrossWindow, M("material", "Material"), M("supportMaterial", "End plates' material", ("endPlates", ["true"])),
        ],
            D(("bladeDepth", 0.2), ("bladeThickness", 0.03), ("spacing", 0.25), ("angle", 30), ("profile", "aerofoil"), ("standoff", 0.12), ("endPlates", true),
              ("overhangLeft", 0.1), ("overhangRight", 0.1), ("below", 0), ("above", 0.15), ("height", 1.6), ("material", "Aluminium anodised"), ("supportMaterial", "Aluminium anodised"))),
        new("eggcrate", "Egg-crate", "USERDEFINED", "Egg-crate", false,
        [
            L("projection", "Depth", 0.05, 2), L("thickness", "Thickness", 0.01, 0.4), L("hSpacing", "Horizontal spacing", 0.1, 3), L("vSpacing", "Vertical spacing", 0.1, 3),
            L("standoff", "Off the wall", 0, 1), .. AcrossWindow, M("material", "Material"),
        ],
            D(("projection", 0.3), ("thickness", 0.04), ("hSpacing", 0.45), ("vSpacing", 0.5), ("standoff", 0), ("overhangLeft", 0.05), ("overhangRight", 0.05), ("below", 0), ("above", 0), ("height", 1.6), ("material", "Concrete"))),
        new("screen", "Perforated screen", "USERDEFINED", "Screen", false,
        [
            L("thickness", "Thickness", 0.002, 0.1), L("standoff", "Off the wall", 0.02, 1.5), C("pattern", "Perforation", ["round", "square", "slots"]),
            L("holeSize", "Hole size", 0.005, 0.5), L("pitch", "Pitch", 0.01, 1), L("frameWidth", "Margin", 0, 0.5), .. AcrossWindow, M("material", "Material"), M("supportMaterial", "Brackets' material"),
        ],
            D(("thickness", 0.006), ("standoff", 0.12), ("pattern", "round"), ("holeSize", 0.05), ("pitch", 0.1), ("frameWidth", 0.06),
              ("overhangLeft", 0.1), ("overhangRight", 0.1), ("below", 0.1), ("above", 0.1), ("height", 2.4), ("material", "Corten steel"), ("supportMaterial", "Steel black"))),
        new("awning", "Awning", "AWNING", null, true,
        [
            L("projection", "Projection", 0.2, 5), A("slope", "Slope", 0, 75), L("valance", "Valance", 0, 0.6), B("cassette", "Cassette"), B("arms", "Folding arms"),
            .. OverWindow, M("material", "Fabric"), M("supportMaterial", "Frame's material"),
        ],
            D(("projection", 1.2), ("slope", 20), ("valance", 0.2), ("cassette", true), ("arms", true), ("overhangLeft", 0.15), ("overhangRight", 0.15), ("gap", 0.25),
              ("material", "Awning fabric"), ("supportMaterial", "Aluminium grey"))),
        new("pergola", "Pergola", "USERDEFINED", "Pergola", true,
        [
            L("projection", "Projection", 0.5, 8), L("rafterSpacing", "Rafter spacing", 0.2, 3), L("rafterWidth", "Rafter width", 0.03, 0.4), L("rafterDepth", "Rafter depth", 0.05, 0.6),
            L("beamWidth", "Beam width", 0.05, 0.5), L("beamDepth", "Beam depth", 0.05, 0.8), B("slats", "Slats on top"),
            L("slatSpacing", "Slat spacing", 0.05, 1, ("slats", ["true"])), L("slatSize", "Slat size", 0.01, 0.2, ("slats", ["true"])),
            B("posts", "Posts to the floor"), L("postSize", "Post size", 0.05, 0.5, ("posts", ["true"])), .. OverWindow, M("material", "Material"), M("supportMaterial", "Ledger's material"),
        ],
            D(("projection", 2.5), ("rafterSpacing", 0.6), ("rafterWidth", 0.06), ("rafterDepth", 0.18), ("beamWidth", 0.12), ("beamDepth", 0.24), ("slats", true), ("slatSpacing", 0.2), ("slatSize", 0.04),
              ("posts", true), ("postSize", 0.12), ("overhangLeft", 0.3), ("overhangRight", 0.3), ("gap", 0.3), ("material", "Timber larch"), ("supportMaterial", "Steel black"))),
        new("jalousie", "External venetian blind", "JALOUSIE", null, false,
        [
            L("slatWidth", "Slat width", 0.02, 0.3), L("slatThickness", "Slat thickness", 0.001, 0.02), L("spacing", "Spacing", 0.02, 0.3), A("angle", "Slat angle", -80, 80),
            N("lowered", "Lowered (0–1)", 0, 1), L("boxHeight", "Head box height", 0.05, 0.5), L("boxDepth", "Head box depth", 0.05, 0.5), B("rails", "Guide rails"), L("standoff", "Off the wall", 0, 0.5),
            .. AcrossWindow, M("material", "Material"),
        ],
            D(("slatWidth", 0.08), ("slatThickness", 0.004), ("spacing", 0.07), ("angle", 45), ("lowered", 1), ("boxHeight", 0.15), ("boxDepth", 0.14), ("rails", true), ("standoff", 0.03),
              ("overhangLeft", 0.03), ("overhangRight", 0.03), ("below", 0), ("above", 0), ("height", 1.5), ("material", "Aluminium grey"))),
    ];

    public static ShadeKind? Find(string? key) => Kinds.FirstOrDefault(k => k.Key == key);

    /// <summary>A kind's spec: its defaults with what is given over them (only its own parameters).</summary>
    public static JsonObject NewSpec(ShadeKind kind, JsonObject? given)
    {
        var spec = new JsonObject { ["kind"] = kind.Key };
        foreach (var (k, v) in kind.Defaults) spec[k] = v?.DeepClone();
        if (given != null) Merge(spec, given, kind);
        return spec;
    }

    /// <summary>Values merged into a spec, each checked against its parameter (null: back to the kind's default).</summary>
    public static void Merge(JsonObject spec, JsonObject given, ShadeKind kind)
    {
        foreach (var (k, v) in given)
        {
            if (k == "kind") continue;
            var p = kind.Params.FirstOrDefault(x => x.Key == k) ?? throw new CommandException($"A {kind.Label.ToLowerInvariant()} has no „{k}”.");
            if (v is null) { spec[k] = kind.Defaults[k]?.DeepClone(); continue; }
            switch (p.Kind)
            {
                case "length" or "number" or "angle":
                    var n = Json.Number(v) ?? throw new CommandException($"{p.Label} is a number.");
                    if (p.Min is { } lo && n < lo - 1e-9 || p.Max is { } hi && n > hi + 1e-9) throw new CommandException($"{p.Label} runs from {p.Min} to {p.Max}.");
                    spec[k] = n;
                    break;
                case "choice":
                    var c = (string?)v;
                    if (c == null || !(p.Choices ?? []).Contains(c)) throw new CommandException($"{p.Label} is one of {string.Join(", ", p.Choices ?? [])}.");
                    spec[k] = c;
                    break;
                case "bool":
                    spec[k] = v is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : throw new CommandException($"{p.Label} is on or off.");
                    break;
                default:
                    spec[k] = ((string?)v)?.Trim() is { Length: > 0 } name ? name : null;
                    break;
            }
        }
    }

    /// <summary>The kinds as the types dialog reads them.</summary>
    public static JsonArray Catalog() => new([.. Kinds.Select(k => (JsonNode)new JsonObject
    {
        ["key"] = k.Key, ["label"] = k.Label, ["predefined"] = k.Predefined, ["objectType"] = k.ObjectType, ["over"] = k.Over,
        ["params"] = new JsonArray([.. k.Params.Select(p => (JsonNode)new JsonObject
        {
            ["key"] = p.Key, ["label"] = p.Label, ["kind"] = p.Kind, ["min"] = p.Min, ["max"] = p.Max,
            ["choices"] = p.Choices is { } c ? new JsonArray([.. c.Select(x => (JsonNode)x)]) : null,
            ["showIf"] = p.ShowIf is { } s ? new JsonObject { [s.Key] = new JsonArray([.. s.Values.Select(x => (JsonNode)x)]) } : null,
        })]),
        ["defaults"] = k.Defaults.DeepClone(),
    })]);

    // ------------------------------------------------------------ styles

    static (string Id, string Label, string Kind, JsonObject Spec) S(string id, string label, string kind, params (string, JsonNode?)[] v) => (id, label, kind, D([("kind", kind), .. v]));

    /// <summary>The built-in styles a sunshade type starts from.</summary>
    public static readonly IReadOnlyList<(string Id, string Label, string Kind, JsonObject Spec)> Styles =
    [
        S("concrete-canopy", "Concrete canopy", "canopy", ("thickness", 0.15), ("projection", 0.9), ("material", "Concrete")),
        S("timber-canopy", "Timber canopy on brackets", "canopy", ("thickness", 0.1), ("projection", 1.0), ("material", "Timber larch"), ("supports", "brackets"), ("supportSpacing", 1.2)),
        S("glass-canopy", "Glass canopy on tie rods", "canopy", ("surface", "glass"), ("thickness", 0.02), ("projection", 1.1), ("tilt", 5), ("supports", "tieRods"), ("supportMaterial", "Stainless steel")),
        S("louvred-canopy", "Louvred canopy", "canopy", ("surface", "louvred"), ("projection", 1.2), ("thickness", 0.15), ("bladeAngle", 30), ("material", "Aluminium anodised"), ("supports", "tieRods")),
        S("tiled-hood", "Tiled hood on brackets", "canopy", ("tilt", 25), ("thickness", 0.12), ("projection", 0.7), ("material", "Clay roof tiles"), ("supports", "brackets"), ("supportMaterial", "Timber larch")),
        S("brise-soleil", "Brise-soleil", "louvres"),
        S("vertical-fins", "Vertical fins", "fins", ("projection", 0.4), ("spacing", 0.5)),
        S("concrete-egg-crate", "Concrete egg-crate", "eggcrate", ("thickness", 0.08), ("projection", 0.35)),
        S("corten-screen", "Perforated Corten screen", "screen"),
        S("fabric-awning", "Fabric awning", "awning"),
        S("timber-pergola", "Timber pergola", "pergola"),
        S("venetian-blind", "External venetian blind", "jalousie"),
    ];

    /// <summary>A style's spec by id: the built-in's, or the project's own.</summary>
    public static JsonObject? StyleSpec(IModelView m, string? id) =>
        Styles.FirstOrDefault(s => s.Id == id).Spec?.DeepClone() as JsonObject ?? ProjectStyles.Of(m, StylesKey, id)?["spec"]?.DeepClone() as JsonObject;

    public static JsonArray StylesCatalog() => new([.. Styles.Select(s => (JsonNode)new JsonObject { ["id"] = s.Id, ["label"] = s.Label, ["kind"] = s.Kind, ["spec"] = s.Spec.DeepClone() })]);

    // ------------------------------------------------------------ the model

    public static Entity? TypeOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { Class: TypeClass } t ? t : null;
    public static ShadeSpec SpecOf(IModelView m, Entity e) => new(TypeOf(m, e)?.Attrs["spec"] as JsonObject);

    static Entity RequireType(IModelView m, string? id) => m.Get(id ?? "") is { Class: TypeClass } t ? t : throw new CommandException("There is no such sunshade type.");

    static string Unique(IModelView m, string name)
    {
        var taken = m.OfClass(TypeClass).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name)) return name;
        for (var i = 2; ; i++) if (!taken.Contains($"{name} {i}")) return $"{name} {i}";
    }

    static string Mm(double v) => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);

    /// <summary>The materials a spec names, made when the project has none.</summary>
    static void EnsureMaterials(IEdit edit, JsonObject spec)
    {
        var s = new ShadeSpec(spec);
        foreach (var p in s.Kind.Params.Where(p => p.Kind == "material"))
            if (s.Str(p.Key) is { Length: > 0 } name) Materials.Ensure(edit, name);
    }

    public static Entity CreateType(IEdit edit, ShadeKind kind, string? name, JsonObject? given)
    {
        var g = given?.DeepClone() as JsonObject;
        g?.Remove("kind");
        var spec = NewSpec(kind, g);
        EnsureMaterials(edit, spec);
        var s = new ShadeSpec(spec);
        var label = string.IsNullOrWhiteSpace(name) ? kind.Over ? $"{kind.Label} {Mm(s.Num("projection"))}" : kind.Label : name.Trim();
        var type = new Entity(edit.NewId(), TypeClass, Unique(edit.Model, label), new JsonObject { ["spec"] = spec }, kind.Predefined);
        edit.Add(type);
        return type;
    }

    /// <summary>The type asked for; else one made from a style; else the project's first of the kind; else a new one of the kind.</summary>
    public static Entity TypeFor(IEdit edit, string? typeId, string? kind, string? style = null)
    {
        if (typeId != null) return RequireType(edit.Model, typeId);
        if (style != null)
        {
            var spec = StyleSpec(edit.Model, style) ?? throw new CommandException("There is no such sunshade style.");
            var label = Styles.FirstOrDefault(s => s.Id == style).Label ?? (string?)ProjectStyles.Of(edit.Model, StylesKey, style)?["label"];
            return CreateType(edit, Find((string?)spec["kind"]) ?? Kinds[0], label, spec);
        }
        var k = Find(kind ?? "canopy") ?? throw new CommandException($"A sunshade is one of {string.Join(", ", Kinds.Select(x => x.Key))}.");
        return edit.Model.OfClass(TypeClass).FirstOrDefault(t => new ShadeSpec(t.Attrs["spec"] as JsonObject).Kind.Key == k.Key) ?? CreateType(edit, k, null, null);
    }

    static Entity Add(IEdit edit, Entity storey, Entity type, JsonObject attrs)
    {
        var k = new ShadeSpec(type.Attrs["spec"] as JsonObject).Kind;
        if (k.ObjectType != null) attrs["objectType"] = k.ObjectType;
        var e = new Entity(edit.NewId(), Class, type.Name, attrs, k.Predefined);
        edit.Add(e);
        Spatial.Contain(edit, storey.Id, e.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        return e;
    }

    // ------------------------------------------------------------ where it stands

    /// <summary>
    /// A sunshade's frame: on a door or window (<c>host</c>), its opening's width past the jambs on the wall's exterior face,
    /// over its head or from below its sill; along a wall (<c>wall</c>, <c>from</c>, <c>to</c>, <c>z</c> over the wall's base);
    /// or free (<c>start</c>, <c>end</c>, <c>z</c> over the storey), out to the line's right. <c>flip</c>: to the other side.
    /// </summary>
    public static ShadeFrame? FrameOf(Entity e, IModelView m, ShadeSpec s)
    {
        var over = s.Kind.Over;
        var flip = e.Bool("flip");
        if (e.Str("host") is { } hostId)
        {
            if (m.Get(hostId) is not { Class: IfcClass.Window or IfcClass.Door } fill || Openings.OpeningOf(m, fill.Id) is not { } opening
                || Openings.WallOf(m, opening.Id) is not { Class: IfcClass.Wall } wall) return null;
            var p = Openings.Place(m, opening);
            var f = Openings.FrameOf(m, wall, p.Along);
            var (face, n) = fill.Bool("flipSide") ^ flip ? (f.YMax, f.Left) : (f.YMin, -f.Left);
            var (ol, or) = (s.Num("overhangLeft"), s.Num("overhangRight"));
            var x0 = p.Along - p.Width / 2 - ol;
            var len = p.Width + ol + or;
            var sill = f.Base + p.Sill;
            var head = sill + p.Height;
            var z = over ? head + s.Num("gap") : sill - s.Num("below");
            var height = over ? 0 : p.Height + s.Num("below") + s.Num("above");
            var floor = Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0;
            return len > 0.05 ? new ShadeFrame(f.Start + f.Dir * x0 + f.Left * face, f.Dir, n, z, len, height, floor, sill) : null;
        }
        if (e.Str("wall") is { } wallId)
        {
            if (m.Get(wallId) is not { Class: IfcClass.Wall } wall) return null;
            var (a0, a1) = (e.Num("from"), e.NumOrNull("to") ?? e.Num("from") + 1);
            if (a1 < a0) (a0, a1) = (a1, a0);
            if (a1 - a0 < 0.05) return null;
            var f = Openings.FrameOf(m, wall, (a0 + a1) / 2);
            var (face, n) = flip ? (f.YMax, f.Left) : (f.YMin, -f.Left);
            var floor = Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0;
            var z = f.Base + (e.NumOrNull("z") ?? (over ? 2.4 : 0.9));
            return new ShadeFrame(f.Start + f.Dir * a0 + f.Left * face, f.Dir, n, z, a1 - a0, over ? 0 : s.Num("height"), floor, z);
        }
        if (Args.TryPoint(e.Attrs["start"]) is { } a && Args.TryPoint(e.Attrs["end"]) is { } b && (b - a).Length > 0.05)
        {
            var u = (b - a).Normalized();
            var n = flip ? u.Perp : -u.Perp;
            var floor = Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0;
            var z = floor + (e.NumOrNull("z") ?? (over ? 2.4 : 0.9));
            return new ShadeFrame(a, u, n, z, (b - a).Length, over ? 0 : s.Num("height"), floor, z);
        }
        return null;
    }

    // ------------------------------------------------------------ the body

    /// <summary>Builds a body in a sunshade's frame, its solids by role (main, support, glass).</summary>
    sealed class Builder(ShadeFrame f)
    {
        public readonly Dictionary<string, List<Solid>> Parts = [];
        public readonly List<string> Notes = [];
        public ShadeFrame F => f;

        void Add(string role, Solid? s) { if (s == null) return; if (!Parts.TryGetValue(role, out var l)) Parts[role] = l = []; l.Add(s); }

        /// <summary>A footprint in (x, y) raised from z0 to z1.</summary>
        public void Up(string role, IReadOnlyList<Vec2> fp, double z0, double z1)
        {
            if (z1 - z0 < 1e-5 || fp.Count < 3) return;
            var world = Polygon2.EnsureCcw(fp.Select(q => f.At(q.X, q.Y, 0).Xy).ToList());
            Add(role, Prism.ExtrudeFootprint(world, f.Z + z0, z1 - z0, role));
        }

        public void Box(string role, double x0, double x1, double y0, double y1, double z0, double z1)
        {
            if (x1 - x0 < 1e-5 || y1 - y0 < 1e-5) return;
            Up(role, [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)], z0, z1);
        }

        /// <summary>A profile in (y, z) swept along the facade from x0 to x1.</summary>
        public void Along(string role, double x0, double x1, IReadOnlyList<(double Y, double Z)> profile)
        {
            if (x1 - x0 < 1e-5 || profile.Count < 3) return;
            Add(role, Prism.Extrude(profile.Select(q => f.At(x0, q.Y, q.Z)).ToList(), null, f.At(x1, 0, 0) - f.At(x0, 0, 0), role));
        }

        /// <summary>An outline in (x, z), with holes, from y0 out to y1.</summary>
        public void Plate(string role, double y0, double y1, IReadOnlyList<Vec2> outline, IReadOnlyList<IReadOnlyList<Vec2>> holes)
        {
            if (y1 - y0 < 1e-5) return;
            Add(role, Prism.Extrude(outline.Select(q => f.At(q.X, y0, q.Y)).ToList(),
                holes.Select(h => (IReadOnlyList<Vec3>)h.Select(q => f.At(q.X, y0, q.Y)).ToList()).ToList(), f.At(0, y1, 0) - f.At(0, y0, 0), role));
        }

        /// <summary>A bar from a to b in the (y, z) plane at x, <paramref name="t"/> square.</summary>
        public void Rod(string role, double x, (double Y, double Z) a, (double Y, double Z) b, double t)
        {
            var (dy, dz) = (b.Y - a.Y, b.Z - a.Z);
            var l = Math.Sqrt(dy * dy + dz * dz);
            if (l < 1e-4) return;
            var (ny, nz) = (-dz / l * t / 2, dy / l * t / 2);
            Along(role, x - t / 2, x + t / 2, [(a.Y + ny, a.Z + nz), (b.Y + ny, b.Z + nz), (b.Y - ny, b.Z - nz), (a.Y - ny, a.Z - nz)]);
        }
    }

    /// <summary>A rectangle w × t centred at (y, z) in the (y, z) plane, turned <paramref name="deg"/> down at its outer edge.</summary>
    static List<(double, double)> Blade(double yc, double zc, double w, double t, double deg, string profile = "flat")
    {
        var a = -deg * Math.PI / 180;
        var (c, s) = (Math.Cos(a), Math.Sin(a));
        List<(double U, double V)> pts = profile switch
        {
            "aerofoil" => [.. Enumerable.Range(0, 6).Select(i => { var u = -w / 2 + w * i / 5; return (u, t / 2 * Math.Sqrt(Math.Max(0.05, 1 - 4 * u * u / (w * w)))); }),
                .. Enumerable.Range(0, 6).Select(i => { var u = w / 2 - w * i / 5; return (u, -t / 2 * Math.Sqrt(Math.Max(0.05, 1 - 4 * u * u / (w * w)))); })],
            "box" => [(-w / 2, -Math.Max(t, 0.04) / 2), (w / 2, -Math.Max(t, 0.04) / 2), (w / 2, Math.Max(t, 0.04) / 2), (-w / 2, Math.Max(t, 0.04) / 2)],
            _ => [(-w / 2, -t / 2), (w / 2, -t / 2), (w / 2, t / 2), (-w / 2, t / 2)],
        };
        return [.. pts.Select(p => (yc + p.U * c - p.V * s, zc + p.U * s + p.V * c))];
    }

    /// <summary>Positions across a length: <paramref name="n"/> or as many as a spacing gives, from <paramref name="a"/> to <paramref name="b"/> both included.</summary>
    static List<double> Spread(double a, double b, double spacing)
    {
        if (b - a < 1e-6) return [(a + b) / 2];
        var n = Math.Max(1, (int)Math.Round((b - a) / Math.Max(spacing, 0.01)));
        return [.. Enumerable.Range(0, n + 1).Select(i => a + (b - a) * i / n)];
    }

    /// <summary>The projection a sunshade has: its own (the noon helper's, or typed), else its type's.</summary>
    public static double Projection(Entity? e, ShadeSpec s) => e?.NumOrNull("projection") is { } p && p > 0 ? p : s.Num("projection", 0.5);

    static void Build(Builder b, ShadeSpec s, double projection)
    {
        var f = b.F;
        var (len, h) = (f.Length, f.Height);
        switch (s.Kind.Key)
        {
            case "canopy":
            {
                var (p, t) = (projection, s.Num("thickness"));
                var tan = Math.Tan(Math.Clamp(s.Num("tilt"), -30, 45) * Math.PI / 180);
                double Drop(double y) => y * tan;
                switch (s.Str("surface"))
                {
                    case "glass":
                        b.Along("glass", 0.02, len - 0.02, [(0.03, 0), (p, -Drop(p)), (p, -Drop(p) + Math.Max(0.01, t)), (0.03, Math.Max(0.01, t))]);
                        b.Along("support", 0, len, [(0, -0.04), (0.06, -0.04), (0.06, 0.05), (0, 0.05)]);
                        break;
                    case "louvred":
                    {
                        var fw = Math.Clamp(s.Num("frameWidth"), 0.02, Math.Min(0.3, p / 3));
                        b.Along("main", 0, len, [(0, 0), (fw, -Drop(fw)), (fw, -Drop(fw) + t), (0, t)]);
                        b.Along("main", 0, len, [(p - fw, -Drop(p - fw)), (p, -Drop(p)), (p, -Drop(p) + t), (p - fw, -Drop(p - fw) + t)]);
                        foreach (var x in new[] { 0.0, len - fw })
                            b.Along("main", x, x + fw, [(fw, -Drop(fw)), (p - fw, -Drop(p - fw)), (p - fw, -Drop(p - fw) + t), (fw, -Drop(fw) + t)]);
                        var (bw, sp) = (s.Num("bladeWidth"), Math.Max(0.03, s.Num("bladeSpacing")));
                        for (var y = fw + sp / 2; y < p - fw - 1e-6; y += sp)
                            b.Along("main", fw, len - fw, Blade(y, -Drop(y) + t / 2, Math.Min(bw, sp * 1.5), 0.02, s.Num("bladeAngle")));
                        break;
                    }
                    default:
                        b.Along("main", 0, len, [(0, 0), (p, -Drop(p)), (p, -Drop(p) + t), (0, t)]);
                        break;
                }
                var fascia = s.Num("fascia");
                if (fascia > 0 && s.Str("surface") != "glass") b.Along("main", 0, len, [(p - 0.03, -Drop(p) - fascia), (p, -Drop(p) - fascia), (p, -Drop(p) + t), (p - 0.03, -Drop(p) + t)]);
                var spacing = Math.Max(0.3, s.Num("supportSpacing", 1.5));
                switch (s.Str("supports"))
                {
                    case "brackets":
                        foreach (var x in Spread(0.15, len - 0.15, spacing))
                            b.Along("support", x - 0.025, x + 0.025, [(0, 0), (0.75 * p, -Drop(0.75 * p)), (0, -Math.Min(0.8, 0.6 * p))]);
                        break;
                    case "tieRods":
                        foreach (var x in Spread(0.15, len - 0.15, spacing))
                            b.Rod("support", x, (0.02, t + Math.Min(1.2, 0.8 * p)), (p - 0.05, -Drop(p - 0.05) + t), 0.02);
                        break;
                    case "posts":
                    {
                        var floor = f.ZFloor - f.Z;
                        foreach (var x in Spread(0.06, len - 0.06, Math.Max(spacing, 2)))
                        {
                            var y = p - 0.08;
                            if (-Drop(y) - floor > 0.2) b.Box("support", x - 0.05, x + 0.05, y - 0.05, y + 0.05, floor, -Drop(y));
                        }
                        break;
                    }
                }
                break;
            }
            case "fins":
            {
                var (p, t, st) = (projection, s.Num("thickness"), s.Num("standoff"));
                var a = Math.Clamp(s.Num("angle"), -75, 75) * Math.PI / 180;
                var (c, sn) = (Math.Cos(a), Math.Sin(a));
                foreach (var x in Spread(t / 2, len - t / 2, s.Num("spacing", 0.6)))
                {
                    var (cx, cy) = (x, st + p / 2 * c);
                    Vec2 R(double along, double across) => new(cx + across * c + along * sn, cy - across * sn + along * c);
                    b.Up("main", [R(-p / 2, -t / 2), R(-p / 2, t / 2), R(p / 2, t / 2), R(p / 2, -t / 2)], 0, h);
                }
                if (s.Bool("rails"))
                    foreach (var z in new[] { 0.0, h - 0.06 })
                        b.Box("support", 0, len, 0, Math.Max(0.04, st + 0.04), z, z + 0.06);
                break;
            }
            case "louvres":
            {
                var (bd, bt, st) = (s.Num("bladeDepth"), s.Num("bladeThickness"), s.Num("standoff"));
                var sp = Math.Max(0.05, s.Num("spacing"));
                var plates = s.Bool("endPlates");
                var (x0, x1) = plates ? (0.012, len - 0.012) : (0.0, len);
                for (var z = h - sp / 2; z > 0; z -= sp)
                    b.Along("main", x0, x1, Blade(st + bd / 2, z, bd, bt, s.Num("angle"), s.Str("profile") ?? "flat"));
                if (plates)
                {
                    b.Box("support", 0, 0.012, 0, st + bd, 0, h);
                    b.Box("support", len - 0.012, len, 0, st + bd, 0, h);
                }
                break;
            }
            case "eggcrate":
            {
                var (p, t, st) = (projection, s.Num("thickness"), s.Num("standoff"));
                foreach (var z in Spread(t / 2, h - t / 2, s.Num("hSpacing", 0.45)))
                    b.Box("main", 0, len, st, st + p, z - t / 2, z + t / 2);
                foreach (var x in Spread(t / 2, len - t / 2, s.Num("vSpacing", 0.5)))
                    b.Box("main", x - t / 2, x + t / 2, st, st + p, 0, h);
                break;
            }
            case "screen":
            {
                var (t, st, fw) = (s.Num("thickness"), s.Num("standoff"), Math.Max(0, s.Num("frameWidth")));
                var size = Math.Max(0.005, s.Num("holeSize"));
                var pitch = Math.Max(size * 1.15, s.Num("pitch"));
                var pattern = s.Str("pattern") ?? "round";
                var (hw, hh) = pattern == "slots" ? (size, size * 3) : (size, size);
                var pitchZ = pattern == "slots" ? Math.Max(pitch, hh * 1.15) : pitch;
                var (ax, az) = (len - 2 * fw - hw, h - 2 * fw - hh);
                // At most so many holes: a finer pattern is drawn coarser (the screen's own spec keeps it for its schedule).
                while (ax > 0 && az > 0 && (Math.Floor(ax / pitch) + 1) * (Math.Floor(az / pitchZ) + 1) > 400) { pitch *= 1.25; pitchZ *= 1.25; b.Notes.Add("coarser"); }
                var thick = Math.Max(0.002, t);
                if (ax > 0 && az > 0)
                {
                    // A strip of the plate per row of holes (a face of a few holes triangulates quickly; one of hundreds does not).
                    var (nx, nz) = ((int)Math.Floor(ax / pitch) + 1, (int)Math.Floor(az / pitchZ) + 1);
                    var (ox, oz) = (fw + hw / 2 + (ax - (nx - 1) * pitch) / 2, fw + hh / 2 + (az - (nz - 1) * pitchZ) / 2);
                    for (var k = 0; k < nz; k++)
                    {
                        var cz = oz + k * pitchZ;
                        var (za, zb) = (k == 0 ? 0 : cz - pitchZ / 2, k == nz - 1 ? h : cz + pitchZ / 2);
                        var holes = new List<IReadOnlyList<Vec2>>();
                        for (var i = 0; i < nx; i++)
                        {
                            var cx = ox + i * pitch;
                            holes.Add(pattern == "round"
                                ? [.. Enumerable.Range(0, 8).Select(j => new Vec2(cx + hw / 2 * Math.Cos(Math.PI * (j + 0.5) / 4), cz + hw / 2 * Math.Sin(Math.PI * (j + 0.5) / 4)))]
                                : [new(cx - hw / 2, cz - hh / 2), new(cx + hw / 2, cz - hh / 2), new(cx + hw / 2, cz + hh / 2), new(cx - hw / 2, cz + hh / 2)]);
                        }
                        b.Plate("main", st, st + thick, [new(0, za), new(len, za), new(len, zb), new(0, zb)], holes);
                    }
                }
                else b.Plate("main", st, st + thick, [new(0, 0), new(len, 0), new(len, h), new(0, h)], []);
                foreach (var x in new[] { 0.05, len - 0.09 })
                    foreach (var z in new[] { 0.05, h - 0.09 })
                        if (st > 0.005) b.Box("support", x, x + 0.04, 0, st, z, z + 0.04);
                break;
            }
            case "awning":
            {
                var p = projection;
                var drop = p * Math.Tan(Math.Clamp(s.Num("slope"), 0, 75) * Math.PI / 180);
                var valance = s.Num("valance");
                b.Along("main", 0.02, len - 0.02, [(0.12, 0), (p, -drop), (p, -drop - 0.01), (0.12, -0.01)]);
                if (valance > 0) b.Along("main", 0.02, len - 0.02, [(p, -drop), (p + 0.01, -drop), (p + 0.01, -drop - valance), (p, -drop - valance)]);
                b.Along("support", 0, len, [(p - 0.04, -drop - 0.05), (p, -drop - 0.05), (p, -drop), (p - 0.04, -drop)]);
                if (s.Bool("cassette")) b.Along("support", 0, len, [(0, -0.06), (0.16, -0.06), (0.16, 0.1), (0, 0.1)]);
                if (s.Bool("arms"))
                    foreach (var x in new[] { 0.15, len - 0.15 })
                        b.Rod("support", x, (0.03, -Math.Min(0.7, drop + 0.5)), (p - 0.05, -drop - 0.03), 0.03);
                break;
            }
            case "pergola":
            {
                var p = projection;
                var (rw, rd, bw, bd) = (s.Num("rafterWidth"), s.Num("rafterDepth"), s.Num("beamWidth"), s.Num("beamDepth"));
                b.Box("support", 0, len, 0, 0.06, 0, rd);
                foreach (var x in Spread(rw / 2, len - rw / 2, s.Num("rafterSpacing", 0.6)))
                    b.Box("main", x - rw / 2, x + rw / 2, 0.06, p + 0.1, 0, rd);
                var by = p - 0.05;
                b.Box("main", 0, len, by - bw, by, -bd, 0);
                if (s.Bool("slats"))
                {
                    var size = s.Num("slatSize");
                    for (var y = 0.1; y + size < p + 0.1; y += Math.Max(size * 1.2, s.Num("slatSpacing")))
                        b.Box("main", 0, len, y, y + size, rd, rd + size);
                }
                if (s.Bool("posts"))
                {
                    var floor = f.ZFloor - f.Z;
                    var ps = s.Num("postSize");
                    if (-bd - floor > 0.3)
                        foreach (var x in new[] { ps / 2, len - ps / 2 })
                            b.Box("main", x - ps / 2, x + ps / 2, by - bw / 2 - ps / 2, by - bw / 2 + ps / 2, floor, -bd);
                }
                break;
            }
            case "jalousie":
            {
                var (sw, st) = (s.Num("slatWidth"), s.Num("standoff"));
                var (bh, bdp) = (s.Num("boxHeight"), s.Num("boxDepth"));
                b.Box("main", 0, len, 0, Math.Max(bdp, st + sw), h, h + bh);
                var bottom = h * (1 - Math.Clamp(s.Num("lowered", 1), 0, 1));
                var sp = Math.Max(0.02, s.Num("spacing"));
                var rails = s.Bool("rails");
                var (x0, x1) = rails ? (0.03, len - 0.03) : (0.0, len);
                for (var z = h - sp / 2; z > bottom; z -= sp)
                    b.Along("main", x0, x1, Blade(st + sw / 2, z, sw, Math.Max(0.001, s.Num("slatThickness")), s.Num("angle")));
                if (rails)
                {
                    b.Box("main", 0, 0.03, st, st + sw, 0, h);
                    b.Box("main", len - 0.03, len, st, st + sw, 0, h);
                }
                break;
            }
        }
    }

    static string Mat(ShadeSpec s, string role) => role switch { "support" => s.Str("supportMaterial") ?? s.Str("material") ?? "", "glass" => s.Str("glassMaterial") ?? "Glass", _ => s.Str("material") ?? "" };

    static readonly Dictionary<string, string> RoleNames = new() { ["main"] = "Body", ["support"] = "Supports", ["glass"] = "Glass" };

    /// <summary>A sunshade's body and plan (its outlines: cut where they cross the plan's cut, seen below it, dashed above it).</summary>
    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        var s = SpecOf(m, e);
        if (FrameOf(e, m, s) is not { } f) return new ElementGeometry([], Diagnostics: ["A sunshade needs a door or window, a wall or a line on a facade to stand on."]);
        var b = new Builder(f);
        Build(b, s, Projection(e, s));
        var parts = new List<GeometryPart>();
        foreach (var role in new[] { "main", "support", "glass" })
        {
            if (!b.Parts.TryGetValue(role, out var solids) || solids.Count == 0) continue;
            var name = Mat(s, role);
            var mat = name.Length > 0 ? Materials.Find(m, name) : null;
            var color = mat != null ? Materials.ColorOf(m, mat.Id) : name.Length > 0 ? Materials.DefaultColor(name) : "#9a9fa6";
            parts.Add(new GeometryPart(RoleNames[role], color, Openings.Combine(solids, "sunshade:" + role), mat?.Id, "sunshade:" + role));
        }
        if (parts.Count == 0) return new ElementGeometry([], Diagnostics: ["The sunshade has no body: make it longer or higher."]);
        return new ElementGeometry(parts, Plan(b, f), [f.At(0, 0, 0).Xy, f.At(f.Length, 0, 0).Xy], b.Notes.Count > 0 ? ["The perforation is drawn coarser than it is (too many holes to draw)."] : null);
    }

    /// <summary>Each solid's outline in plan (identical ones once), by where it stands against the plan's cut (1.2 m over its storey's floor).</summary>
    static List<PlanItem> Plan(Builder b, ShadeFrame f)
    {
        var o = new List<PlanItem>();
        var cut = f.ZFloor + Openings.PlanCut;
        var seen = new HashSet<string>();
        var all = new List<Vec2>();
        // What the cut crosses (fins, posts, a screen) is drawn piece by piece; what is over or under it as one outline per part.
        var merged = new Dictionary<(string Role, string Kind), List<Vec2>>();
        foreach (var (role, solids) in b.Parts)
            foreach (var solid in solids)
            {
                var (z0, z1) = (solid.Vertices.Min(v => v.Z), solid.Vertices.Max(v => v.Z));
                var hull = Polygon2.ConvexHull(solid.Vertices.Select(v => v.Xy));
                if (hull.Length < 3) continue;
                all.AddRange(hull);
                var kind = z0 <= cut && z1 >= cut ? "heavy" : z1 < cut ? "symbol" : "overhead";
                if (kind != "heavy")
                {
                    if (!merged.TryGetValue((role, kind), out var pts)) merged[(role, kind)] = pts = [];
                    pts.AddRange(hull);
                    continue;
                }
                var key = kind + string.Join(";", hull.Select(q => $"{Math.Round(q.X, 3)},{Math.Round(q.Y, 3)}"));
                if (seen.Add(key)) o.Add(new PlanItem(kind, true, hull));
            }
        foreach (var ((_, kind), pts) in merged)
            if (Polygon2.ConvexHull(pts) is { Length: >= 3 } hull) o.Add(new PlanItem(kind, true, hull));
        if (all.Count >= 3) o.Insert(0, new PlanItem("hit", true, Polygon2.ConvexHull(all)));
        return o;
    }

    /// <summary>A sunshade's measures: its length, projection (or depth), height, the area it shades (plan or face), its volume.</summary>
    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        var s = SpecOf(m, e);
        if (FrameOf(e, m, s) is not { } f) yield break;
        var p = s.Kind.Key is "louvres" ? s.Num("standoff") + s.Num("bladeDepth") : s.Kind.Key is "jalousie" ? s.Num("standoff") + s.Num("slatWidth") : s.Kind.Key == "screen" ? s.Num("standoff") : Projection(e, s);
        var volume = (g?.Parts ?? []).Sum(x => Measure.Volume(x.Solid));
        List<Quantity> q =
        [
            new("Length", f.Length, "LENGTH"), new("Width", p, "LENGTH"),
            new("Area", s.Kind.Over ? f.Length * p : f.Length * f.Height, "AREA"),
            new("NetVolume", volume, "VOLUME"),
        ];
        if (!s.Kind.Over) q.Insert(2, new Quantity("Height", f.Height, "LENGTH"));
        yield return new QuantitySet(QuantitySet, q);
    }

    // ------------------------------------------------------------ the sun at noon

    /// <summary>The sun when it stands highest on a day (UTC midnight of it) at a place: found minute by minute round the place's noon.</summary>
    public static (SunPosition Sun, DateTime Utc) Noon(DateOnly date, double lat, double lon)
    {
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(12 - lon / 15);
        var best = (Sun.At(start, lat, lon), start);
        for (var mnt = -120; mnt <= 120; mnt++)
        {
            var t = start.AddMinutes(mnt);
            var sp = Sun.At(t, lat, lon);
            if (sp.Altitude > best.Item1.Altitude) best = (sp, t);
        }
        return best;
    }

    /// <summary>The direction towards the sun in a plan whose true north is turned <paramref name="northAngle"/> (degrees) from +y.</summary>
    public static Vec3 Towards(SunPosition s, double northAngle)
    {
        var th = northAngle * Math.PI / 180;
        var north = new Vec2(-Math.Sin(th), Math.Cos(th));
        var east = new Vec2(Math.Cos(th), Math.Sin(th));
        var (az, alt) = (s.Azimuth * Math.PI / 180, s.Altitude * Math.PI / 180);
        var hz = north * Math.Cos(az) + east * Math.Sin(az);
        return new Vec3(hz.X * Math.Cos(alt), hz.Y * Math.Cos(alt), Math.Sin(alt));
    }

    /// <summary>
    /// How far a shade over a window must project (horizontally) so that the sun from <paramref name="sun"/> leaves the whole
    /// window in its shadow: its front edge's shadow falls <paramref name="rise"/> below the shade's underside at the wall (to
    /// the sill), its underside falling <paramref name="tanTilt"/> per metre out; and how far past each jamb it must run. Zero
    /// when the sun is behind the facade (it shades itself).
    /// </summary>
    public static (double Depth, double Side, double Profile) DepthFor(Vec3 sun, Vec2 outward, Vec2 along, double rise, double tanTilt = 0)
    {
        var h = sun.X * outward.X + sun.Y * outward.Y;
        if (h <= 1e-6 || sun.Z <= 1e-6) return (0, 0, 90);
        var tanP = sun.Z / h;
        var depth = rise / (tanP + tanTilt);
        var side = (rise - depth * tanTilt) * Math.Abs(sun.X * along.X + sun.Y * along.Y) / sun.Z;
        return (depth, side, Math.Atan(tanP) * 180 / Math.PI);
    }

    /// <summary>The noon helper for a sunshade on a window: the projection for full shade at noon on 21 June at the project's place.</summary>
    public static JsonObject NoonShade(IModelView m, Entity e)
    {
        var s = SpecOf(m, e);
        if (!s.Kind.Over) throw new CommandException("Full shade at noon is worked out for a canopy, an awning or a pergola over a window.");
        if (e.Str("host") == null || FrameOf(e, m, s) is not { } f) throw new CommandException("Full shade at noon needs the sunshade on a window: put it on one.");
        var (lon, lat, known) = Sun.Place(m);
        var (sun, utc) = Noon(new DateOnly(DateTime.UtcNow.Year, 6, 21), lat, lon);
        var dir = Sun.Direction(m, sun);
        var tanTilt = s.Kind.Key switch
        {
            "canopy" => Math.Tan(Math.Clamp(s.Num("tilt"), -30, 45) * Math.PI / 180),
            "awning" => Math.Tan(Math.Clamp(s.Num("slope"), 0, 75) * Math.PI / 180),
            _ => 0,
        };
        // The underside at the wall: a canopy's and an awning's is the reference height; a pergola's front beam hangs under it.
        var under = f.Z - (s.Kind.Key == "pergola" ? s.Num("beamDepth") : s.Kind.Key == "awning" ? 0.01 : 0);
        var (depth, side, profile) = DepthFor(dir, f.N, f.U, under - f.Sill, tanTilt);
        return new JsonObject
        {
            ["depth"] = Math.Round(depth, 4), ["side"] = Math.Round(side, 4), ["profile"] = Math.Round(profile, 3),
            ["altitude"] = Math.Round(sun.Altitude, 3), ["azimuth"] = Math.Round(sun.Azimuth, 3), ["utc"] = utc.ToString("HH:mm", CultureInfo.InvariantCulture),
            ["latitude"] = lat, ["longitude"] = lon, ["placeKnown"] = known, ["shaded"] = depth <= 0,
        };
    }

    // ------------------------------------------------------------ the module's part

    public static void Register(IModuleRegistry r)
    {
        var ownSide = new Dictionary<string, string[]> { ["wall"] = ["!"] };
        r.Class(new(Class, "Sunshade",
        [
            new("projection", "Projection (its own)", "length", Min: 0.05, Max: 8),
            new("z", "Height", "length", Min: -5, Max: 50, ShowIf: FieldDescriptor.When("host", "")),
            new("from", "From (along the wall)", "length", ShowIf: [ownSide]),
            new("to", "To (along the wall)", "length", ShowIf: [ownSide]),
            new("flip", "On the other face", "bool"),
        ], "#8f9aa5"));
        r.Class(new(TypeClass, "Sunshade type", [], "#8f9aa5", Kind: "type"));
        r.Geometry(Class, (e, m, _) => Geometry(e, m));
        r.Quantities(Class, (e, g, m) => Quantities(e, g, m));
        r.Catalog("sunshadeKinds", Catalog());
        r.Catalog("sunshadeStyles", StylesCatalog());
        r.Service<IScheduleSource>(new SunshadeSchedule());
        // Where it stands, for its grips: the facade's start, the way along and out, its length and projection.
        r.Service<INodeProps>(new SunshadeFrames());

        // A type of a kind or from a style: {kind? | style?, name?, spec?} → {id}.
        r.Command("createSunshadeType", "New sunshade type", (ctx, a) =>
        {
            if ((string?)a["style"] is { } style)
            {
                var t = TypeFor(ctx, null, null, style);
                if (a["spec"] is JsonObject || (string?)a["name"] != null)
                {
                    var spec = (JsonObject)t.Attrs["spec"]!.DeepClone();
                    if (a["spec"] is JsonObject g) Merge(spec, g, new ShadeSpec(spec).Kind);
                    EnsureMaterials(ctx, spec);
                    var attrs = t.CloneAttrs();
                    attrs["spec"] = spec;
                    ctx.Replace(t.With(name: ((string?)a["name"])?.Trim() is { Length: > 0 } n ? Unique(ctx.Model, n) : t.Name, attrs: attrs));
                }
                return new JsonObject { ["id"] = t.Id };
            }
            var kind = Find((string?)a["kind"]) ?? throw new CommandException($"A sunshade is one of {string.Join(", ", Kinds.Select(k => k.Key))}.");
            return new JsonObject { ["id"] = CreateType(ctx, kind, (string?)a["name"], a["spec"] as JsonObject).Id };
        });

        // A type changed: {id, name?, spec?} (values merged; null: the kind's default again; a new kind starts from its defaults). Its sunshades follow.
        r.Command("updateSunshadeType", "Change sunshade type", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["id"]);
            var spec = (type.Attrs["spec"] as JsonObject)?.DeepClone() as JsonObject ?? NewSpec(Kinds[0], null);
            var pre = type.PredefinedType;
            if (a["spec"] is JsonObject given)
            {
                if ((string?)given["kind"] is { } nk && nk != (string?)spec["kind"])
                    spec = NewSpec(Find(nk) ?? throw new CommandException($"A sunshade is one of {string.Join(", ", Kinds.Select(k => k.Key))}."), null);
                Merge(spec, given, new ShadeSpec(spec).Kind);
            }
            EnsureMaterials(ctx, spec);
            var kind = new ShadeSpec(spec).Kind;
            var attrs = type.CloneAttrs();
            attrs["spec"] = spec;
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : type.Name;
            ctx.Replace(type.With(name: name, attrs: attrs, predefinedType: kind.Predefined));
            foreach (var e in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(x => x.Class == Class).ToList())
            {
                var ea = e.CloneAttrs();
                if (kind.ObjectType != null) ea["objectType"] = kind.ObjectType; else ea.Remove("objectType");
                ctx.Replace(e.With(name: e.Name == type.Name ? name : e.Name, attrs: ea, predefinedType: kind.Predefined));
            }
            return null;
        });

        r.Command("duplicateSunshadeType", "Duplicate sunshade type", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["id"]);
            var copy = new Entity(ctx.NewId(), TypeClass, ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : Unique(ctx.Model, type.Name + " copy"), type.CloneAttrs(), type.PredefinedType);
            ctx.Add(copy);
            return new JsonObject { ["id"] = copy.Id };
        });

        r.Command("setSunshadeType", "Change type", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["typeId"]);
            var kind = new ShadeSpec(type.Attrs["spec"] as JsonObject).Kind;
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) is { Class: Class } x ? x : throw new CommandException("Only a sunshade takes a sunshade type.");
                var named = e.Name == TypeOf(ctx.Model, e)?.Name;
                ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
                var attrs = e.CloneAttrs();
                if (kind.ObjectType != null) attrs["objectType"] = kind.ObjectType; else attrs.Remove("objectType");
                attrs.Remove("projection");
                ctx.Replace(e.With(name: named ? type.Name : e.Name, attrs: attrs, predefinedType: kind.Predefined));
            }
            return null;
        });

        // A sunshade: {typeId? | kind? | style?} and where — {hostId} (a door or window), {wallId, x1, y1, x2, y2, z?} (along a wall),
        // or {storeyId, x1, y1, x2, y2, z?} (free on a facade line, out to its right); flip? to the other face. → {id}.
        r.Command("addSunshade", "Sunshade", (ctx, a) =>
        {
            var type = TypeFor(ctx, (string?)a["typeId"], (string?)a["kind"], (string?)a["style"]);
            var attrs = new JsonObject();
            Entity storey;
            if ((string?)a["hostId"] is { } hostId)
            {
                var fill = ctx.Model.Get(hostId) is { Class: IfcClass.Window or IfcClass.Door } x ? x : throw new CommandException("A sunshade goes on a door or a window.");
                var wall = Openings.OpeningOf(ctx.Model, fill.Id) is { } o ? Openings.WallOf(ctx.Model, o.Id) : null;
                storey = (wall == null ? null : Spatial.StoreyOf(ctx.Model, wall.Id)) ?? throw new CommandException("The window is in no wall.");
                attrs["host"] = fill.Id;
            }
            else if ((string?)a["wallId"] is { } wallId)
            {
                var wall = Spatial.Require(ctx.Model, wallId, IfcClass.Wall);
                storey = Spatial.StoreyOf(ctx.Model, wall.Id) ?? throw new CommandException("The wall stands on no storey.");
                var (t0, t1) = (Openings.AlongOf(wall, Args.Point(a, "x1", "y1")), Openings.AlongOf(wall, Args.Point(a, "x2", "y2")));
                var len = PathCurve.Of(wall).Length;
                (t0, t1) = (Math.Clamp(Math.Min(t0, t1), 0, len), Math.Clamp(Math.Max(t0, t1), 0, len));
                if (t1 - t0 < 0.1) throw new CommandException("A sunshade along a wall is 100 mm long at least: two points on the wall, apart.");
                attrs["wall"] = wall.Id;
                attrs["from"] = Math.Round(t0, 4);
                attrs["to"] = Math.Round(t1, 4);
                if (Json.Number(a["z"]) is { } z) attrs["z"] = z;
            }
            else if (WallUnder(ctx.Model, (string?)a["storeyId"], Args.Point(a, "x1", "y1"), Args.Point(a, "x2", "y2")) is { } under)
            {
                // Two points in one wall's thickness: along that wall, on the face nearer to them... its exterior unless asked.
                var (p1, p2) = (Args.Point(a, "x1", "y1"), Args.Point(a, "x2", "y2"));
                storey = Spatial.StoreyOf(ctx.Model, under.Id)!;
                var (t0, t1) = (Openings.AlongOf(under, p1), Openings.AlongOf(under, p2));
                if (Math.Abs(t1 - t0) < 0.1) throw new CommandException("A sunshade along a wall is 100 mm long at least: two points on the wall, apart.");
                attrs["wall"] = under.Id;
                attrs["from"] = Math.Round(Math.Min(t0, t1), 4);
                attrs["to"] = Math.Round(Math.Max(t0, t1), 4);
                if (Json.Number(a["z"]) is { } z) attrs["z"] = z;
            }
            else
            {
                storey = Spatial.Require(ctx.Model, (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
                var (p1, p2) = (Args.Point(a, "x1", "y1"), Args.Point(a, "x2", "y2"));
                if ((p2 - p1).Length < 0.1) throw new CommandException("A sunshade is 100 mm long at least: two points apart.");
                attrs["start"] = new JsonArray(Math.Round(p1.X, 5), Math.Round(p1.Y, 5));
                attrs["end"] = new JsonArray(Math.Round(p2.X, 5), Math.Round(p2.Y, 5));
                if (Json.Number(a["z"]) is { } z) attrs["z"] = z;
            }
            if (a["flip"] is JsonValue fv && fv.TryGetValue<bool>(out var flip) && flip) attrs["flip"] = true;
            return new JsonObject { ["id"] = Add(ctx, storey, type, attrs).Id };
        });

        // Its grips: {id, start?, end? (free), from?, to? (along its wall), projection? (null: its type's)}.
        r.Command("updateSunshade", "Change sunshade", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("Pick a sunshade.");
            var attrs = e.CloneAttrs();
            foreach (var k in new[] { "start", "end" })
                if (Args.TryPoint(a[k]) is { } p) attrs[k] = new JsonArray(Math.Round(p.X, 5), Math.Round(p.Y, 5));
            foreach (var k in new[] { "from", "to" })
                if (Json.Number(a[k]) is { } v) attrs[k] = Math.Round(v, 4);
            if (a.ContainsKey("projection"))
                if (Json.Number(a["projection"]) is { } pr) attrs["projection"] = pr >= 0.05 && pr <= 8 ? pr : throw new CommandException("A projection runs from 50 mm to 8 m.");
                else attrs.Remove("projection");
            if (Args.TryPoint(attrs["start"]) is { } s0 && Args.TryPoint(attrs["end"]) is { } s1 && (s1 - s0).Length < 0.1) throw new CommandException("A sunshade is 100 mm long at least.");
            if (attrs["wall"] != null && ctx.Model.Get((string?)attrs["wall"] ?? "") is { } wall)
            {
                var len = PathCurve.Of(wall).Length;
                var (f0, f1) = (Json.Number(attrs["from"]) ?? 0, Json.Number(attrs["to"]) ?? 0);
                if (f0 < -1e-6 || f1 > len + 1e-6 || Math.Abs(f1 - f0) < 0.1) throw new CommandException("A sunshade along a wall stays on it, 100 mm long at least.");
            }
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // The noon helper: {id} → the projection for full shade at noon on 21 June (and the sun then).
        r.Query("noonShade", (q, a) => NoonShade(q.Model, q.Model.Get((string?)a["id"] ?? "") is { Class: Class } e ? e : throw new CommandException("Pick a sunshade.")));
        // ... and set as the sunshades' own projection, rounded up to the centimetre: {ids} → {results: [{id, depth}]}.
        r.Command("shadeAtNoon", "Full shade at noon", (ctx, a) =>
        {
            var results = new JsonArray();
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) is { Class: Class } x ? x : throw new CommandException("Pick a sunshade.");
                var r0 = NoonShade(ctx.Model, e);
                var depth = Json.Number(r0["depth"]) ?? 0;
                var attrs = e.CloneAttrs();
                if (depth > 0) attrs["projection"] = Math.Max(0.05, Math.Ceiling(depth * 100 - 1e-9) / 100);
                ctx.Replace(e.With(attrs: attrs));
                r0["id"] = id;
                results.Add((JsonNode)r0);
            }
            return new JsonObject { ["results"] = results };
        });

        // The project's own styles: a type saved under a name, renamed, deleted.
        r.Command("saveSunshadeStyle", "Save sunshade style", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["typeId"]);
            var spec = type.Attrs["spec"] as JsonObject ?? throw new CommandException("The type has no spec.");
            return new JsonObject { ["styleId"] = ProjectStyles.Save(ctx, StylesKey, (string?)a["name"] ?? type.Name, (string?)a["styleId"], spec, "sunshade") };
        });
        r.Command("renameSunshadeStyle", "Rename sunshade style", (ctx, a) => { ProjectStyles.Rename(ctx, StylesKey, (string?)a["styleId"] ?? "", (string?)a["name"] ?? "", "sunshade"); return null; });
        r.Command("deleteSunshadeStyle", "Delete sunshade style", (ctx, a) => { ProjectStyles.Delete(ctx, StylesKey, (string?)a["styleId"] ?? "", "sunshade"); return null; });

        // The types dialog's picture of a spec: {kind?, spec?, length?, height?} → {front, section, plan: {strokes}, width, depth, height}.
        r.Query("sunshadePreview", (q, a) =>
        {
            var given = a["spec"] as JsonObject;
            var kind = Find((string?)a["kind"] ?? (string?)given?["kind"]) ?? Kinds[0];
            var g = given?.DeepClone() as JsonObject;
            g?.Remove("kind");
            var spec = NewSpec(kind, null);
            if (g != null) foreach (var (k, v) in g) if (kind.Params.Any(p => p.Key == k)) spec[k] = v?.DeepClone();
            return Preview(new ShadeSpec(spec), Json.Number(a["length"]), Json.Number(a["height"]));
        });
    }

    /// <summary>The wall of a storey both points lie in (in its thickness, along its length), if any.</summary>
    static Entity? WallUnder(IModelView m, string? storeyId, Vec2 a, Vec2 b)
    {
        storeyId ??= Spatial.Storeys(m).FirstOrDefault()?.Id;
        return m.OfClass(IfcClass.Wall).FirstOrDefault(w => Spatial.StoreyOf(m, w.Id)?.Id == storeyId && HostedOpenings.Inside(m, w, a) && HostedOpenings.Inside(m, w, b));
    }

    /// <summary>
    /// A spec drawn over a window 1.2 m wide and 1.5 m high in a wall (or as long and high as asked): the front seen from outside,
    /// the section through its middle with the wall and the window, the plan; as the joinery configurator's strokes.
    /// </summary>
    public static JsonObject Preview(ShadeSpec s, double? length = null, double? height = null)
    {
        var (win, wh, sill) = (1.2, 1.5, 0.9);
        var over = s.Kind.Over;
        var len = length ?? win + s.Num("overhangLeft") + s.Num("overhangRight");
        var h = over ? 0 : height ?? wh + s.Num("below") + s.Num("above");
        var z = over ? sill + wh + s.Num("gap") : sill - s.Num("below");
        var f = new ShadeFrame(new Vec2(0, 0), new Vec2(1, 0), new Vec2(0, 1), z, len, h, 0, sill);
        var b = new Builder(f);
        Build(b, s, s.Num("projection", 0.5));
        var solids = b.Parts.SelectMany(x => x.Value.Select(v => (Role: x.Key, Solid: v))).ToList();
        var x0 = s.Num("overhangLeft");
        var front = new List<JsonNode>();
        var plan = new List<JsonNode>();
        var section = new List<JsonNode>();
        JsonNode Stroke(IEnumerable<Vec2> pts, string weight, bool closed, bool dashed = false) => new JsonObject
        {
            ["pts"] = new JsonArray([.. pts.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 5), Math.Round(p.Y, 5)))]), ["weight"] = weight, ["closed"] = closed, ["dashed"] = dashed,
        };
        // The window behind it: its opening in front and in section, the wall's face in section.
        front.Add(Stroke([new(x0, sill), new(x0 + win, sill), new(x0 + win, sill + wh), new(x0, sill + wh)], "thin", true));
        var seen = new HashSet<string>();
        foreach (var (_, solid) in solids)
        {
            var hull = Polygon2.ConvexHull(solid.Vertices.Select(v => new Vec2(v.X, v.Z)));
            if (hull.Length >= 3 && seen.Add("f" + string.Join(";", hull.Select(p => $"{Math.Round(p.X, 3)},{Math.Round(p.Y, 3)}")))) front.Add(Stroke(hull, "outline", true));
            var ph = Polygon2.ConvexHull(solid.Vertices.Select(v => v.Xy));
            if (ph.Length >= 3 && seen.Add("p" + string.Join(";", ph.Select(p => $"{Math.Round(p.X, 3)},{Math.Round(p.Y, 3)}")))) plan.Add(Stroke(ph, over ? "thin" : "cut", true, over));
            // The section through the middle: where the solid's edges cross it.
            var mid = len / 2;
            var cross = new List<Vec2>();
            foreach (var face in solid.Faces)
                for (var i = 0; i < face.Outer.Length; i++)
                {
                    var (p, q) = (solid.Vertices[face.Outer[i]], solid.Vertices[face.Outer[(i + 1) % face.Outer.Length]]);
                    if ((p.X <= mid && q.X > mid) || (q.X <= mid && p.X > mid)) { var k = (mid - p.X) / (q.X - p.X); cross.Add(new Vec2(p.Y + (q.Y - p.Y) * k, p.Z + (q.Z - p.Z) * k)); }
                }
            var sh = Polygon2.ConvexHull(cross);
            if (sh.Length >= 3) section.Add(Stroke(sh, "cut", true));
        }
        var (lo, hi) = (Math.Min(0, solids.Count > 0 ? solids.Min(x => x.Solid.Vertices.Min(v => v.Z)) : 0), Math.Max(sill + wh + 0.3, solids.Count > 0 ? solids.Max(x => x.Solid.Vertices.Max(v => v.Z)) : 0));
        section.Add(Stroke([new(0, lo), new(0, sill)], "cut", false));
        section.Add(Stroke([new(0, sill + wh), new(0, hi)], "cut", false));
        section.Add(Stroke([new(-0.05, sill), new(0.02, sill), new(0.02, sill + wh), new(-0.05, sill + wh)], "thin", true));
        plan.Add(Stroke([new(-0.3, 0), new(x0, 0)], "cut", false));
        plan.Add(Stroke([new(x0 + win, 0), new(len + 0.3, 0)], "cut", false));
        var all = solids.SelectMany(x => x.Solid.Vertices).ToList();
        return new JsonObject
        {
            ["front"] = new JsonObject { ["strokes"] = new JsonArray([.. front]) },
            ["section"] = new JsonObject { ["strokes"] = new JsonArray([.. section]) },
            ["plan"] = new JsonObject { ["strokes"] = new JsonArray([.. plan]) },
            ["width"] = Math.Round(len, 4),
            ["depth"] = all.Count > 0 ? Math.Round(all.Max(v => v.Y), 4) : 0,
            ["height"] = all.Count > 0 ? Math.Round(all.Max(v => v.Z) - all.Min(v => v.Z), 4) : 0,
            ["over"] = over,
        };
    }
}

/// <summary>A sunshade's frame for its grips (<c>shadeFrame</c>): the facade's start, along and out, its length, its projection, whether it is over a window.</summary>
sealed class SunshadeFrames : INodeProps
{
    public void Add(Entity e, IModelView m, JsonObject props)
    {
        if (e.Class != Sunshades.Class) return;
        var s = Sunshades.SpecOf(m, e);
        if (Sunshades.FrameOf(e, m, s) is not { } f) return;
        static JsonArray P(Vec2 v) => new(Math.Round(v.X, 6), Math.Round(v.Y, 6));
        props["shadeFrame"] = new JsonObject
        {
            ["o"] = P(f.O), ["u"] = P(f.U), ["n"] = P(f.N), ["length"] = Math.Round(f.Length, 6),
            ["projection"] = Math.Round(Sunshades.Projection(e, s), 6), ["over"] = s.Kind.Over, ["kind"] = s.Kind.Key,
        };
    }
}

/// <summary>The sunshade schedule: a row per type and size (its kind, where it stands, length, projection, how many, the area they shade, material); the total.</summary>
public sealed class SunshadeSchedule : IScheduleSource
{
    public string Kind => "sunshades";
    public string Label => "Sunshade schedule";

    static string Mm(double v) => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        var rows = new List<IReadOnlyList<string>>();
        var keys = new List<string?>();
        var items = m.OfClass(Sunshades.Class).Select(e =>
        {
            var s = Sunshades.SpecOf(m, e);
            var sets = q.Quantities(e.Id).FirstOrDefault(x => x.Name == Sunshades.QuantitySet)?.Quantities ?? [];
            double Q(string n) => sets.FirstOrDefault(x => x.Name == n)?.Value ?? 0;
            var where = e.Str("host") is { } h ? m.Get(h)?.Name ?? "" : e.Str("wall") != null ? "wall" : "facade";
            return (E: e, S: s, Type: Sunshades.TypeOf(m, e), Length: Q("Length"), Width: Q("Width"), Area: Q("Area"), Where: where);
        }).ToList();
        foreach (var g in items.GroupBy(x => (x.Type?.Id ?? "", Mm(x.Length), Mm(x.Width))).OrderBy(g => g.First().Type?.Name ?? "", StringComparer.Ordinal).ThenBy(g => g.First().Length))
        {
            var first = g.First();
            var hosts = g.Select(x => x.Where).Where(x => x.Length > 0).Distinct().OrderBy(x => x, StringComparer.Ordinal);
            rows.Add([first.Type?.Name ?? first.E.Name, first.S.Kind.Label, string.Join(", ", hosts), Mm(first.Length), Mm(first.Width), g.Count().ToString(CultureInfo.InvariantCulture),
                g.Sum(x => x.Area).ToString("0.00", CultureInfo.InvariantCulture), first.S.Str("material") ?? ""]);
            keys.Add(first.Type?.Id);
        }
        if (rows.Count > 0)
        {
            rows.Add(["Total", "", "", "", "", items.Count.ToString(CultureInfo.InvariantCulture), items.Sum(x => x.Area).ToString("0.00", CultureInfo.InvariantCulture), ""]);
            keys.Add(null);
        }
        return new ScheduleGrid(Label, ["Type", "Kind", "On", "Length (mm)", "Projection (mm)", "Count", "Area (m²)", "Material"], rows, keys);
    }
}
