using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// How a material looks when rendered (tester's report #76): the physically based values a renderer
/// takes (Twinmotion, Enscape, Blender read them from glTF, USD, OBJ's MTL and IFC's surface styles)
/// beside its 2D graphics (hatches and pens, <see cref="Lapin.Drawing.MaterialGraphics"/>), which stay apart.
/// <list type="bullet">
/// <item><see cref="Roughness"/> 0 (a mirror) to 1 (chalk); <see cref="Metalness"/> 0 (paint, wood, stone) or 1 (bare metal).</item>
/// <item><see cref="Transparency"/> 0 (opaque) to 1 (invisible): how much shows through, what every renderer takes as opacity.</item>
/// <item><see cref="Transmission"/> 0 to 1: glass's light through its body (glTF KHR_materials_transmission); <see cref="Ior"/> its index of refraction.</item>
/// <item><see cref="Emissive"/> a colour it gives off (a lamp's diffuser), null for none, at <see cref="EmissiveStrength"/>.</item>
/// <item><see cref="TextureRotation"/> degrees its texture (drawn or the user's image) turns on the faces.</item>
/// </list>
/// What the material does not set comes from its name (<see cref="Guess"/>): glass is clear and smooth, a metal metallic, a tile glossy.
/// The interface guesses the same (ui/src/viewport/look.ts).
/// </summary>
public sealed record MaterialLook(double Roughness, double Metalness, double Transparency, double Transmission, double Ior, string? Emissive, double EmissiveStrength, double TextureRotation)
{
    /// <summary>The attributes of an IfcMaterial that hold its look, with their ranges.</summary>
    public static readonly (string Key, double Min, double Max)[] Numbers =
    [
        ("roughness", 0, 1), ("metalness", 0, 1), ("transparency", 0, 1), ("transmission", 0, 1), ("ior", 1, 3),
        ("emissiveStrength", 0, 100), ("textureRotation", -360, 360),
    ];

    public static readonly MaterialLook Plain = new(0.85, 0, 0, 0, 1.5, null, 1, 0);

    /// <summary>What a material's name says about its look (English and Romanian names).</summary>
    public static MaterialLook Guess(string? name)
    {
        var n = Fold(name);
        // Insulation first: glass wool is no glass.
        if (Has(n, "insul", "wool", "vata", " eps", " xps", "polistiren", "mineral")) return Plain with { Roughness = 0.97 };
        if (Has(n, "glass", "glazing", "sticla", "geam", "vitra", "verre", "vidrio", "glas", "szklo", "vetro", "vidro"))
            return Plain with { Roughness = Has(n, "frosted", "matt", " mat ", "satin", "sablat") ? 0.45 : 0.05, Transparency = Has(n, "frosted", "satin", "sablat") ? 0.35 : 0.6, Transmission = 0.9, Ior = 1.52 };
        // Painted or powder-coated metal is a paint: a dielectric, half glossy.
        if (Has(n, "ral ", "powder", "painted", "vopsit", "lacuit", "coated"))
            return Plain with { Roughness = 0.45, Metalness = Has(n, "metallic", "metalizat") ? 0.6 : 0.1 };
        if (Has(n, "chrome", "crom", "mirror", "oglinda", "polished", "lustruit"))
            return Plain with { Roughness = 0.08, Metalness = 1 };
        if (Has(n, "steel", "otel", "inox", "alumin", "metal", "zinc", "copper", "cupru", "brass", "alama", "iron", "fier", "titan", "anodi", "hardware", "feronerie"))
            return Plain with { Roughness = Has(n, "brushed", "periat", "anodi") ? 0.3 : Has(n, "galvan", "zinc", "rust", "corten") ? 0.55 : 0.35, Metalness = 1 };
        if (Has(n, "tile", "ceramic", "gresie", "faianta", "porcelain", "portelan", "marble", "marmur", "granite", "granit"))
            return Plain with { Roughness = Has(n, "matt", " mat ") ? 0.55 : 0.2 };
        if (Has(n, "water", " apa ", "pool", "piscina")) return Plain with { Roughness = 0.02, Transparency = 0.5, Transmission = 0.8, Ior = 1.33 };
        if (Has(n, "plastic", "pvc", "acrylic", "acril", "polycarb", "policarb")) return Plain with { Roughness = 0.4 };
        if (Has(n, "parquet", "parchet", "laminat", "lacquer", "varnish", "lac ")) return Plain with { Roughness = 0.45 };
        if (Has(n, "timber", "wood", "lemn", "oak", "stejar", "pine", "pin ", "brad", "beech", "fag", "walnut", "nuc", "plank", "deck", "osb", "clt", "plywood", "placaj"))
            return Plain with { Roughness = 0.7 };
        if (Has(n, "paint", "vopsea", "lavabil", "enamel", "email")) return Plain with { Roughness = 0.6 };
        if (Has(n, "fabric", "textil", "carpet", "mocheta", "cushion", "perna", "felt", "earth", "soil", "pamant", "gravel", "pietris"))
            return Plain with { Roughness = 0.97 };
        if (Has(n, "concrete", "beton", "brick", "caramid", "plaster", "render", "tencu", "stone", "piatra", "screed", "sapa", "gips", "gypsum", "mortar"))
            return Plain with { Roughness = 0.9 };
        if (Has(n, "lamp", " led ", "diffuser", "difuzor", "lumina", "luminaire")) return Plain with { Roughness = 0.5, Emissive = "#fff4e0", EmissiveStrength = 1 };
        return Plain;
    }

    /// <summary>A material's look: its own values, the rest from its name.</summary>
    public static MaterialLook Of(Entity? material)
    {
        if (material == null) return Plain;
        var g = Guess(material.Name);
        double V(string key, double fallback) => material.NumOrNull(key) is { } v && double.IsFinite(v) ? v : fallback;
        var emissive = material.Attrs.ContainsKey("emissive") ? material.Str("emissive") : g.Emissive;
        return new MaterialLook(V("roughness", g.Roughness), V("metalness", g.Metalness), V("transparency", g.Transparency), V("transmission", g.Transmission), V("ior", g.Ior),
            emissive is { Length: 7 } ? emissive : null, V("emissiveStrength", g.EmissiveStrength), V("textureRotation", g.TextureRotation));
    }

    /// <summary>The look as the interface reads it, with which fields the material sets itself.</summary>
    public JsonObject ToJson(Entity? material)
    {
        var o = new JsonObject
        {
            ["roughness"] = Roughness, ["metalness"] = Metalness, ["transparency"] = Transparency, ["transmission"] = Transmission, ["ior"] = Ior,
            ["emissive"] = Emissive, ["emissiveStrength"] = EmissiveStrength, ["textureRotation"] = TextureRotation,
        };
        o["own"] = new JsonArray(Numbers.Select(n => n.Key).Append("emissive").Where(k => material?.Attrs.ContainsKey(k) == true).Select(k => (JsonNode)k).ToArray());
        return o;
    }

    /// <summary>
    /// The look's fields of a command's arguments checked into <paramref name="attrs"/>: a number in its range, an emissive colour
    /// (#rrggbb); null takes the field off (back to what the name gives).
    /// </summary>
    public static void Apply(JsonObject attrs, JsonObject a)
    {
        foreach (var (key, min, max) in Numbers)
        {
            if (!a.ContainsKey(key)) continue;
            if (a[key] is null) { attrs.Remove(key); continue; }
            attrs[key] = Json.Number(a[key]) is { } v && double.IsFinite(v) && v >= min && v <= max ? v : throw new CommandException($"{Label(key)} is {min} to {max}.");
        }
        if (a.ContainsKey("emissive"))
        {
            if ((string?)a["emissive"] is { Length: > 0 } e)
                attrs["emissive"] = System.Text.RegularExpressions.Regex.IsMatch(e, "^#[0-9a-fA-F]{6}$") ? e : throw new CommandException("An emissive colour is #rrggbb.");
            else if (a["emissive"] is null) attrs.Remove("emissive");
            else attrs["emissive"] = null; // "": none, whatever the name says
        }
    }

    static string Label(string key) => key switch
    {
        "ior" => "The index of refraction", "emissiveStrength" => "The emissive strength", "textureRotation" => "The texture's rotation",
        _ => char.ToUpperInvariant(key[0]) + key[1..],
    };

    static bool Has(string n, params string[] words) => words.Any(w => n.Contains(w, StringComparison.Ordinal));

    /// <summary>Letters with diacritics as their base letter (the engine runs without ICU: no Unicode normalisation to lean on).</summary>
    static readonly Dictionary<char, char> Base = new()
    {
        ['ă'] = 'a', ['â'] = 'a', ['á'] = 'a', ['à'] = 'a', ['ä'] = 'a', ['ã'] = 'a', ['å'] = 'a', ['ą'] = 'a', ['î'] = 'i', ['í'] = 'i', ['ì'] = 'i', ['ï'] = 'i',
        ['ș'] = 's', ['ş'] = 's', ['ś'] = 's', ['ț'] = 't', ['ţ'] = 't', ['é'] = 'e', ['è'] = 'e', ['ê'] = 'e', ['ë'] = 'e', ['ę'] = 'e', ['ó'] = 'o', ['ò'] = 'o',
        ['ô'] = 'o', ['ö'] = 'o', ['õ'] = 'o', ['ø'] = 'o', ['ú'] = 'u', ['ù'] = 'u', ['û'] = 'u', ['ü'] = 'u', ['ç'] = 'c', ['ć'] = 'c', ['ñ'] = 'n', ['ń'] = 'n',
        ['ł'] = 'l', ['ź'] = 'z', ['ż'] = 'z', ['æ'] = 'a', ['ß'] = 's',
    };

    /// <summary>Lower case without diacritics, padded with spaces so a word's end can be matched ("ral ", "pin ").</summary>
    public static string Fold(string? name)
    {
        var sb = new System.Text.StringBuilder(" ");
        foreach (var ch in (name ?? "").ToLowerInvariant()) sb.Append(Base.TryGetValue(ch, out var b) ? b : ch);
        return sb.Append(' ').ToString();
    }
}
