using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Balconies;

/// <summary>
/// What a balcony is made of (a tester's report, #96): its kind, its outline, its slab and edge, its finish and fall,
/// its railing (the railings' designs, or a solid parapet), what carries it (posts, brackets, hangers) and, for a loggia,
/// the recess it cuts into its wall. Every field is an attribute of the balcony itself (so the properties panel edits
/// it and shows only the fields its kind uses), and of its type, the project's style (<see cref="Balconies.TypeClass"/>);
/// the built-in styles are <see cref="Styles"/>.
/// </summary>
public static class BalconySpec
{
    public static readonly string[] Kinds = ["juliet", "cantilever", "posts", "brackets", "loggia", "corner", "mezzanine"];
    public static readonly string[] Outlines = ["rect", "rounded", "curved", "polygon"];
    public static readonly string[] Edges = ["square", "drip", "nosing", "tapered"];
    public static readonly string[] RailStyles = ["bar", "balusters", "glass", "cable", "panel", "parapet", "none"];
    public static readonly string[] Supports = ["none", "posts", "brackets", "hangers"];

    static Dictionary<string, string[]> If(string key, params string[] values) => new() { [key] = values };
    static Dictionary<string, string[]> If2(string k1, string[] v1, string k2, string[] v2) => new() { [k1] = v1, [k2] = v2 };
    static IReadOnlyList<IReadOnlyDictionary<string, string[]>> When(params Dictionary<string, string[]>[] any) => any;

    static readonly string[] Projecting = ["juliet", "cantilever", "posts", "brackets", "corner"];
    static readonly string[] Shaped = ["juliet", "cantilever", "posts", "brackets"];
    static readonly string[] Railed = ["bar", "balusters", "glass", "cable", "panel"];
    static readonly string[] Posted = ["bar", "glass", "cable"];

    /// <summary>The fields a style carries (everything but where the balcony is), with when each applies (decision 333).</summary>
    public static readonly FieldDescriptor[] StyleFields =
    [
        new("kind", "Kind", "choice", Choices: Kinds),
        new("outline", "Outline", "choice", Choices: Outlines, ShowIf: When(If("kind", Shaped))),
        new("width", "Width", "length", Min: 0.3, Max: 40),
        new("depth", "Depth", "length", Min: 0, Max: 10),
        new("cornerRadius", "Corner radius", "length", Min: 0.05, Max: 5, ShowIf: When(If2("kind", Shaped, "outline", ["rounded"]))),
        new("bulge", "Front bulge", "length", Min: 0.05, Max: 5, ShowIf: When(If2("kind", Shaped, "outline", ["curved"]))),
        new("returnLength", "Return along the side", "length", Min: 0.3, Max: 20, ShowIf: When(If("kind", "corner"))),
        new("cornerSide", "Corner on the", "choice", Choices: ["right", "left"], ShowIf: When(If("kind", "corner"))),
        new("gallerySlab", "Gallery slab", "bool", ShowIf: When(If("kind", "mezzanine"))),
        new("fascia", "Fascia height", "length", Min: 0, Max: 1.5, ShowIf: When(If("kind", "mezzanine"))),
        new("thickness", "Slab thickness", "length", Min: 0.04, Max: 0.6),
        new("edge", "Edge", "choice", Choices: Edges),
        new("edgeThickness", "Edge thickness", "length", Min: 0.03, Max: 0.6, ShowIf: When(If("edge", "tapered"))),
        new("material", "Slab material", "material"),
        new("finishThickness", "Finish thickness", "length", Min: 0, Max: 0.15),
        new("finishMaterial", "Finish material", "material"),
        new("fall", "Fall (%)", "number", Min: 0, Max: 5),
        new("soffitThickness", "Soffit thickness", "length", Min: 0, Max: 0.1),
        new("soffitMaterial", "Soffit material", "material"),
        new("thermalBreak", "Thermal break", "bool", ShowIf: When(If("kind", [.. Projecting, "loggia"]))),
        new("railStyle", "Railing", "choice", Choices: RailStyles),
        new("railHeight", "Railing height", "length", Min: 0.3, Max: 2.5, ShowIf: When(If("railStyle", "!none"))),
        new("railSides", "Railing on", "choice", Choices: ["front", "all"], ShowIf: When(If2("kind", Projecting, "railStyle", ["!none"]))),
        new("railInset", "Railing in from the edge", "length", Min: 0, Max: 0.5, ShowIf: When(If("railStyle", "!none"))),
        new("railProfile", "Handrail", "choice", Choices: ["round", "square"], ShowIf: When(If("railStyle", Railed))),
        new("barSize", "Handrail size", "length", Min: 0.02, Max: 0.15, ShowIf: When(If("railStyle", Railed))),
        new("postSize", "Railing post size", "length", Min: 0.01, Max: 0.15, ShowIf: When(If("railStyle", [.. Posted, "balusters", "panel"]))),
        new("postSpacing", "Railing post spacing", "length", Min: 0.3, Max: 3, ShowIf: When(If("railStyle", Posted))),
        new("balusterSpacing", "Baluster spacing", "length", Min: 0.05, Max: 0.3, ShowIf: When(If("railStyle", "balusters"))),
        new("parapetThickness", "Parapet thickness", "length", Min: 0.05, Max: 0.5, ShowIf: When(If("railStyle", "parapet"))),
        new("parapetMaterial", "Parapet material", "material", ShowIf: When(If("railStyle", "parapet"))),
        new("railColor", "Railing colour", "color", ShowIf: When(If("railStyle", Railed))),
        new("support", "Carried by", "choice", Choices: Supports, ShowIf: When(If("kind", Projecting))),
        new("supportShape", "Post shape", "choice", Choices: ["round", "square"], ShowIf: When(If2("kind", Projecting, "support", ["posts"]))),
        new("supportSize", "Post, bracket or rod size", "length", Min: 0.01, Max: 0.6, ShowIf: When(If2("kind", Projecting, "support", ["posts", "brackets", "hangers"]))),
        new("supportSpacing", "Support spacing", "length", Min: 0.3, Max: 10, ShowIf: When(If2("kind", Projecting, "support", ["posts", "brackets"]))),
        new("bracketDepth", "Bracket depth", "length", Min: 0.05, Max: 3, ShowIf: When(If2("kind", Projecting, "support", ["brackets"]))),
        new("bracketHeight", "Bracket height", "length", Min: 0.05, Max: 2, ShowIf: When(If2("kind", Projecting, "support", ["brackets"]))),
        new("hangerHeight", "Rods fixed above the slab", "length", Min: 0.3, Max: 6, ShowIf: When(If2("kind", Projecting, "support", ["hangers"]))),
        new("supportMaterial", "Support material", "material", ShowIf: When(If2("kind", Projecting, "support", ["posts", "brackets", "hangers"]))),
        new("recessHeight", "Recess height", "length", Min: 1.8, Max: 10, ShowIf: When(If("kind", "loggia"))),
        new("cheekThickness", "Side walls", "length", Min: 0.05, Max: 0.6, ShowIf: When(If("kind", "loggia"))),
        new("projection", "Slab past the face", "length", Min: 0, Max: 3, ShowIf: When(If("kind", "loggia"))),
        new("ceiling", "Ceiling slab", "bool", ShowIf: When(If("kind", "loggia"))),
        new("backWall", "Back wall", "bool", ShowIf: When(If("kind", "loggia"))),
    ];

    /// <summary>Where the balcony is: on its wall (along it), its level, the other side of its line, or by its drawn outline.</summary>
    public static readonly FieldDescriptor[] PlaceFields =
    [
        new("along", "Along the wall", "length", Min: -100, Max: 1000),
        new("baseOffset", "Floor above the storey", "length", Min: -3, Max: 3),
        new("flip", "Other side", "bool", ShowIf: When(If("kind", "mezzanine"))),
    ];

    public static IEnumerable<string> Keys => StyleFields.Select(f => f.Key);

    /// <summary>What every balcony has unless its style says otherwise.</summary>
    public static JsonObject Base() => new()
    {
        ["kind"] = "cantilever", ["outline"] = "rect", ["width"] = 3.0, ["depth"] = 1.5, ["cornerRadius"] = 0.4, ["bulge"] = 0.4, ["returnLength"] = 2.0, ["cornerSide"] = "right",
        ["gallerySlab"] = true, ["fascia"] = 0.3,
        ["thickness"] = 0.18, ["edge"] = "drip", ["edgeThickness"] = 0.12, ["material"] = "Reinforced concrete",
        ["finishThickness"] = 0.05, ["finishMaterial"] = "Ceramic tiles", ["fall"] = 1.5, ["soffitThickness"] = 0.015, ["soffitMaterial"] = "Exterior plaster",
        ["thermalBreak"] = true,
        ["railStyle"] = "glass", ["railHeight"] = 1.0, ["railSides"] = "all", ["railInset"] = 0.05, ["railProfile"] = "round", ["barSize"] = 0.045,
        ["postSize"] = 0.04, ["postSpacing"] = 1.2, ["balusterSpacing"] = 0.11, ["parapetThickness"] = 0.15, ["parapetMaterial"] = "Reinforced concrete", ["railColor"] = "#3a3d42",
        ["support"] = "none", ["supportShape"] = "round", ["supportSize"] = 0.15, ["supportSpacing"] = 3.0, ["bracketDepth"] = 0.6, ["bracketHeight"] = 0.45, ["hangerHeight"] = 2.2,
        ["supportMaterial"] = "Steel",
        ["recessHeight"] = 2.6, ["cheekThickness"] = 0.2, ["projection"] = 0.0, ["ceiling"] = true, ["backWall"] = true,
    };

    /// <summary>A built-in style: its id, its label, what it is for, its fields over <see cref="Base"/>.</summary>
    public sealed record Style(string Id, string Label, string Description, JsonObject Attrs)
    {
        public JsonObject Fields()
        {
            var o = Base();
            foreach (var (k, v) in Attrs) o[k] = v?.DeepClone();
            return o;
        }
    }

    /// <summary>The four kinds of the tester's picture (Juliet, loggia, stacked, mezzanine) and a few more.</summary>
    public static readonly IReadOnlyList<Style> Styles =
    [
        new("juliet", "Juliet balcony", "A railing in front of a French door over a small stone ledge on two brackets.", new JsonObject
        {
            ["kind"] = "juliet", ["width"] = 1.6, ["depth"] = 0.3, ["thickness"] = 0.1, ["edge"] = "square", ["material"] = "Natural stone",
            ["finishThickness"] = 0.0, ["fall"] = 0.0, ["soffitThickness"] = 0.0, ["thermalBreak"] = false,
            ["railStyle"] = "balusters", ["railHeight"] = 1.0, ["railSides"] = "all", ["railInset"] = 0.04, ["railProfile"] = "square", ["barSize"] = 0.04, ["postSize"] = 0.02, ["railColor"] = "#1f2124",
            ["support"] = "brackets", ["supportSize"] = 0.12, ["bracketDepth"] = 0.26, ["bracketHeight"] = 0.3, ["supportSpacing"] = 1.2, ["supportMaterial"] = "Natural stone",
        }),
        new("cantilever", "Cantilevered slab, glass", "A concrete slab cantilevered from the floor through a thermal break, tiled to a fall, glass railing on three sides.", new JsonObject
        {
            ["kind"] = "cantilever", ["width"] = 3.0, ["depth"] = 1.5, ["thickness"] = 0.18, ["edge"] = "drip", ["railStyle"] = "glass",
        }),
        new("stacked", "Stacked, on posts", "A slab on steel posts at its front corners, repeated storey over storey; glass railing.", new JsonObject
        {
            ["kind"] = "posts", ["width"] = 3.6, ["depth"] = 1.8, ["thickness"] = 0.2, ["edge"] = "square", ["support"] = "posts", ["supportShape"] = "round", ["supportSize"] = 0.14,
            ["supportSpacing"] = 3.6, ["railStyle"] = "glass", ["railColor"] = "#2b2d30", ["thermalBreak"] = true,
        }),
        new("brackets", "On steel brackets", "A thin slab on steel brackets fixed to the wall, posts and a handrail.", new JsonObject
        {
            ["kind"] = "brackets", ["width"] = 2.4, ["depth"] = 1.0, ["thickness"] = 0.12, ["edge"] = "nosing", ["support"] = "brackets", ["supportSize"] = 0.08,
            ["bracketDepth"] = 0.8, ["bracketHeight"] = 0.5, ["supportSpacing"] = 1.2, ["railStyle"] = "bar", ["railProfile"] = "square", ["thermalBreak"] = false,
        }),
        new("hung", "Hung from tie rods", "A light slab hung from the wall above by steel tie rods at its front corners; cable railing.", new JsonObject
        {
            ["kind"] = "cantilever", ["width"] = 2.8, ["depth"] = 1.2, ["thickness"] = 0.14, ["edge"] = "tapered", ["edgeThickness"] = 0.08, ["support"] = "hangers", ["supportSize"] = 0.03,
            ["hangerHeight"] = 2.4, ["railStyle"] = "cable",
        }),
        new("rounded", "Rounded corners", "A cantilevered slab with rounded front corners and balusters.", new JsonObject
        {
            ["kind"] = "cantilever", ["outline"] = "rounded", ["width"] = 3.2, ["depth"] = 1.4, ["cornerRadius"] = 0.6, ["railStyle"] = "balusters", ["railProfile"] = "round",
        }),
        new("curved", "Curved front", "A cantilevered slab with a segmental front and a solid parapet.", new JsonObject
        {
            ["kind"] = "cantilever", ["outline"] = "curved", ["width"] = 3.6, ["depth"] = 1.6, ["bulge"] = 0.6, ["railStyle"] = "parapet", ["railHeight"] = 1.0, ["parapetThickness"] = 0.15,
        }),
        new("loggia", "Loggia", "Recessed into the facade: the floor inside the building line, side walls and a ceiling, the railing at the face.", new JsonObject
        {
            ["kind"] = "loggia", ["width"] = 3.0, ["depth"] = 1.6, ["thickness"] = 0.2, ["edge"] = "square", ["railStyle"] = "balusters", ["railProfile"] = "square", ["railColor"] = "#1f2124",
            ["recessHeight"] = 2.6, ["cheekThickness"] = 0.2, ["projection"] = 0.1, ["ceiling"] = true, ["backWall"] = true,
        }),
        new("corner", "Corner balcony", "Round a building's corner: along this wall and back along the side one, glass railing.", new JsonObject
        {
            ["kind"] = "corner", ["width"] = 3.0, ["depth"] = 1.5, ["returnLength"] = 2.0, ["railStyle"] = "glass",
        }),
        new("mezzanine", "Mezzanine gallery edge", "An interior gallery over a double-height space: the gallery's edge, its fascia and a bar railing.", new JsonObject
        {
            ["kind"] = "mezzanine", ["width"] = 5.0, ["depth"] = 1.2, ["thickness"] = 0.2, ["edge"] = "square", ["finishThickness"] = 0.03, ["finishMaterial"] = "Oak flooring", ["fall"] = 0.0,
            ["soffitThickness"] = 0.0, ["thermalBreak"] = false, ["gallerySlab"] = true, ["fascia"] = 0.3,
            ["railStyle"] = "balusters", ["railProfile"] = "square", ["railColor"] = "#1f2124", ["railHeight"] = 1.1,
        }),
    ];

    public static Style? StyleOf(string? id) => Styles.FirstOrDefault(s => s.Id == id);

    /// <summary>What carries a balcony of a kind, unless it says.</summary>
    public static string SupportOf(string kind) => kind switch { "posts" => "posts", "brackets" or "juliet" => "brackets", _ => "none" };

    /// <summary>A balcony's or type's fields, each the entity's own or <see cref="Base"/>'s.</summary>
    public static JsonObject Read(Entity e)
    {
        var o = Base();
        foreach (var k in Keys) if (e.Attrs[k] is { } v) o[k] = v.DeepClone();
        return o;
    }

    /// <summary>The style fields of <paramref name="from"/>, checked against their descriptors (values out of range dropped).</summary>
    public static JsonObject Clean(JsonObject from)
    {
        var o = new JsonObject();
        foreach (var f in StyleFields)
        {
            if (from[f.Key] is not { } v) continue;
            try { o[f.Key] = Fields.Check(f, v.DeepClone()); }
            catch (CommandException) { }
        }
        return o;
    }

    /// <summary>The materials the fields name, made in the project when they are not there yet.</summary>
    public static void EnsureMaterials(IEdit edit, JsonObject fields)
    {
        foreach (var f in StyleFields.Where(f => f.Kind == "material"))
            if ((string?)fields[f.Key] is { Length: > 0 } name) Materials.Ensure(edit, name);
    }
}
