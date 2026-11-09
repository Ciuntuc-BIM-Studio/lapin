using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform.Analysis;

namespace Lapin.Platform.ProfileLibraries;

/// <summary>
/// A cross-section profile as a profile library keeps it (docs/plan-profile-libraries.md), whatever its material: its kind
/// and dimensions in metres, centred on its box, its name, family, standard and material, and the values the file it came
/// from gave (<see cref="Given"/>, SI units: m², m⁴, m³, m⁶, kg/m, m²/m). Kinds: I (and H, flanges parallel or sloping),
/// asymI (an asymmetric I, its bottom flange <see cref="B2"/> × <see cref="Tf2"/>), U, T, L, RHS (and SHS), CHS, C, Z,
/// SIGMA (cold-formed: one thickness, lips), rectangle, circle (<see cref="H"/> = <see cref="B"/> its diameter), ellipse,
/// outline (any outline: <see cref="Rings"/>, outsides anticlockwise, holes clockwise).
/// </summary>
public sealed record SectionProfile
{
    public static readonly string[] Kinds = ["I", "asymI", "U", "T", "L", "RHS", "CHS", "C", "Z", "SIGMA", "rectangle", "circle", "ellipse", "outline"];
    /// <summary>The materials a library sorts by, and their density (kg/m³) when the profile does not say its own.</summary>
    public static readonly IReadOnlyDictionary<string, double> Densities = new Dictionary<string, double>
    {
        ["steel"] = 7850, ["aluminium"] = 2700, ["timber"] = 470, ["concrete"] = 2500, ["pvc"] = 1400, ["stainless"] = 7900, ["other"] = 0,
    };

    public required string Name { get; init; }
    public required string Kind { get; init; }
    /// <summary>Depth (a circle's or a CHS's diameter), width (a Z's whole, both flanges), web and flange thickness (a thin-walled section's one thickness both).</summary>
    public double H { get; init; }
    public double B { get; init; }
    public double Tw { get; init; }
    public double Tf { get; init; }
    /// <summary>The root radius (a hollow section's outer corner) and the toes' (a hollow section's inner corner); 0 when sharp or not said.</summary>
    public double R1 { get; init; }
    public double R2 { get; init; }
    public double Lip { get; init; }
    /// <summary>The flanges' inner slope (rise over run), the flange thickness where it is measured.</summary>
    public double Slope { get; init; }
    public double B2 { get; init; }
    public double Tf2 { get; init; }
    public IReadOnlyList<IReadOnlyList<Vec2>>? Rings { get; init; }

    public string? Family { get; init; }
    public string? Standard { get; init; }
    public string Material { get; init; } = "steel";
    /// <summary>Hot-rolled, cold-formed, welded, glulam, joinery, precast…: free text the library sorts by.</summary>
    public string? Category { get; init; }
    public double? Density { get; init; }
    /// <summary>Where it came from (a file's name, a library), said in the project's copy.</summary>
    public string? Source { get; init; }
    public IReadOnlyDictionary<string, double> Given { get; init; } = new Dictionary<string, double>();

    public double DensityOrDefault => Density ?? Densities.GetValueOrDefault(Material, 0);

    /// <summary>Whether IFC has a parametric form for its kind (a C only with lips, a Z only without); an outline, a Σ and the others go by their rings.</summary>
    public bool HasIfcForm => Kind switch
    {
        "I" or "asymI" or "U" or "T" or "L" or "RHS" or "CHS" or "rectangle" or "circle" or "ellipse" => true,
        "C" => Lip > 0, "Z" => Lip <= 0, _ => false,
    };

    /// <summary>How the profile's shape is read for its properties.</summary>
    public SectionShape Shape() => new(Kind, B, Kind is "circle" or "CHS" ? B : H, Tw, Tf, Lip, Name)
    {
        Root = R1,
        Toe = R2 > 0 ? R2 : Kind == "L" ? R1 / 2 : Kind == "RHS" ? Math.Max(R1 - Tw, 0) : 0,
        Slope = Slope, Width2 = B2, Flange2 = Tf2, Rings = Rings, Standard = Standard, Family = Family,
    };

    /// <summary>
    /// The section a column, beam or member type keeps (<c>Section</c> in the structure module): the same kind and size
    /// for the kinds it knows, an outline for the others (an asymmetric I, an ellipse); <paramref name="profileId"/> the
    /// project's copy of the profile, so the type can follow it.
    /// </summary>
    public JsonObject SectionJson(string? profileId)
    {
        var o = new JsonObject();
        switch (Kind)
        {
            case "asymI" or "ellipse":
                o["kind"] = "outline";
                o["rings"] = RingsJson(Outline());
                break;
            case "outline":
                o["kind"] = "outline";
                o["rings"] = RingsJson(Rings ?? []);
                break;
            default:
                o["kind"] = Kind;
                break;
        }
        o["width"] = Math.Max(B, B2);
        o["depth"] = Kind is "circle" or "CHS" ? B : H;
        if (Kind is not ("rectangle" or "circle" or "ellipse" or "outline" or "asymI")) { o["web"] = Tw; o["flange"] = Tf; }
        if (Lip > 0) o["lip"] = Lip;
        o["name"] = Name;
        if (R1 > 0) o["r1"] = R1;
        if (R2 > 0) o["r2"] = R2;
        if (Slope > 0) o["slope"] = Slope;
        if (Standard != null) o["standard"] = Standard;
        if (Family != null) o["family"] = Family;
        if (profileId != null) o["profileId"] = profileId;
        return o;
    }

    /// <summary>The profile's rings as its properties are worked out (fillets rounded; an outline's own), about the box's centre.</summary>
    public IReadOnlyList<IReadOnlyList<Vec2>> Outline() => Drawing.Rings(this);

    public static JsonArray RingsJson(IEnumerable<IReadOnlyList<Vec2>> rings) =>
        new(rings.Select(r => (JsonNode)new JsonArray(r.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 7), Math.Round(p.Y, 7))).ToArray())).ToArray());

    /// <summary>The name a profile without one is given from its kind and size (mm): IPE-like names are a library's.</summary>
    public static string LabelOf(string kind, double h, double b, double tw, double tf)
    {
        static string M(double v) => (v * 1000).ToString("0.#", CultureInfo.InvariantCulture);
        return kind switch
        {
            "CHS" => $"CHS {M(b)}×{M(tw)}",
            "RHS" => $"{(Math.Abs(h - b) < 1e-9 ? "SHS" : "RHS")} {M(h)}×{M(b)}×{M(tw)}",
            "L" => $"L {M(h)}×{M(b)}×{M(tw)}",
            "circle" => $"Ø{M(b)}",
            "rectangle" => $"{M(b)}×{M(h)}",
            "C" or "Z" => $"{kind} {M(h)}×{M(b)}×{M(tw)}",
            "SIGMA" => $"Σ {M(h)}×{M(b)}×{M(tw)}",
            "outline" => $"Outline {M(h)}×{M(b)}",
            _ => $"{kind} {M(h)}×{M(b)}×{M(tw)}×{M(tf)}",
        };
    }

    // ------------------------------------------------------------------ JSON

    static double R(double v) => Math.Round(v, 9);

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["name"] = Name, ["kind"] = Kind };
        if (Family != null) o["family"] = Family;
        if (Standard != null) o["standard"] = Standard;
        o["material"] = Material;
        if (Category != null) o["category"] = Category;
        if (Density is { } d) o["density"] = d;
        if (H > 0) o["h"] = R(H);
        if (B > 0) o["b"] = R(B);
        if (Tw > 0) o["tw"] = R(Tw);
        if (Tf > 0) o["tf"] = R(Tf);
        if (R1 > 0) o["r1"] = R(R1);
        if (R2 > 0) o["r2"] = R(R2);
        if (Lip > 0) o["lip"] = R(Lip);
        if (Slope > 0) o["slope"] = R(Slope);
        if (B2 > 0) o["b2"] = R(B2);
        if (Tf2 > 0) o["tf2"] = R(Tf2);
        if (Rings is { Count: > 0 } rings) o["rings"] = RingsJson(rings);
        if (Given.Count > 0) o["props"] = new JsonObject(Given.OrderBy(k => Array.IndexOf(ProfileProps.Keys, k.Key)).Select(k => KeyValuePair.Create(k.Key, (JsonNode?)JsonValue.Create(Math.Round(k.Value, 15)))));
        if (Source != null) o["source"] = Source;
        return o;
    }

    public static SectionProfile? From(JsonNode? n)
    {
        if (n is not JsonObject o || (string?)o["name"] is not { Length: > 0 } name) return null;
        var kind = KindOf((string?)o["kind"]) ?? "outline";
        double N(string k) => Json.Number(o[k]) ?? 0;
        var given = new Dictionary<string, double>();
        if (o["props"] is JsonObject props)
            foreach (var (k, v) in props)
                if (Json.Number(v) is { } x && double.IsFinite(x) && ProfileProps.Keys.Contains(k)) given[k] = x;
        var p = new SectionProfile
        {
            Name = name.Trim(), Kind = kind, H = N("h"), B = N("b"), Tw = N("tw"), Tf = N("tf"), R1 = N("r1"), R2 = N("r2"), Lip = N("lip"), Slope = N("slope"),
            B2 = N("b2"), Tf2 = N("tf2"), Rings = RingsFrom(o["rings"]), Family = (string?)o["family"], Standard = (string?)o["standard"],
            Material = (string?)o["material"] is { Length: > 0 } m ? m : "steel", Category = (string?)o["category"], Density = Json.Number(o["density"]),
            Source = (string?)o["source"], Given = given,
        };
        if (kind is "circle" or "CHS" && p.B <= 0) p = p with { B = p.H };
        if (kind is "circle" or "CHS" && p.H <= 0) p = p with { H = p.B };
        if (kind == "outline" && p.Rings is { Count: > 0 } r && (p.H <= 0 || p.B <= 0))
        {
            var all = r.SelectMany(x => x).ToList();
            p = p with { B = all.Max(q => q.X) - all.Min(q => q.X), H = all.Max(q => q.Y) - all.Min(q => q.Y) };
        }
        return p;
    }

    static IReadOnlyList<IReadOnlyList<Vec2>>? RingsFrom(JsonNode? n) =>
        n is JsonArray a && a.OfType<JsonArray>().Select(r => (IReadOnlyList<Vec2>)r.OfType<JsonArray>().Where(q => q.Count >= 2)
            .Select(q => new Vec2(Json.Number(q[0]) ?? 0, Json.Number(q[1]) ?? 0)).ToList()).Where(r => r.Count >= 3).ToList() is { Count: > 0 } l ? l : null;

    /// <summary>A kind as files write it (H → I, SHS → RHS, Σ → SIGMA, a case of its own), null when it is none.</summary>
    public static string? KindOf(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var k = s.Trim();
        foreach (var known in Kinds) if (string.Equals(known, k, StringComparison.OrdinalIgnoreCase)) return known;
        return k.ToUpperInvariant() switch
        {
            "H" => "I", "SHS" => "RHS", "Σ" or "SIGMA" => "SIGMA", "RECT" => "rectangle", "ROUND" or "BAR" => "circle", "ARBITRARY" => "outline",
            _ => null,
        };
    }

    /// <summary>What is wrong with its dimensions for its kind; null when it can be built.</summary>
    public string? Problem()
    {
        if (!Kinds.Contains(Kind)) return $"„{Name}”: an unknown kind „{Kind}”.";
        if (Kind == "outline") return Rings is { Count: > 0 } r && r.Any(x => x.Count >= 3 && Math.Abs(Polygon2.SignedArea(x)) > 1e-12) ? null : $"„{Name}”: its outline encloses no area.";
        if (!(H > 0 && B > 0)) return $"„{Name}”: it needs a depth and a width.";
        switch (Kind)
        {
            case "I" or "U" or "T" or "asymI":
                if (!(Tw > 0 && Tf > 0 && Tw < B && 2 * Tf < H)) return $"„{Name}”: its web and flanges must be thinner than it.";
                break;
            case "L":
                if (!(Tw > 0 && Tw < Math.Min(H, B))) return $"„{Name}”: its legs must be thinner than they are long.";
                break;
            case "RHS" or "CHS":
                if (!(Tw > 0 && 2 * Tw < Math.Min(H, B))) return $"„{Name}”: its wall must be thinner than half of it.";
                break;
            case "C" or "Z" or "SIGMA":
                if (!(Tw > 0 && 2 * Tw < B && Lip >= 0 && Lip < H / 2)) return $"„{Name}”: its sheet must be thinner than its flanges, its lips shorter than half its depth.";
                break;
        }
        return null;
    }
}

/// <summary>A profile's rings, with its fillets, for drawing and for its paint area: the same outline its properties are found from.</summary>
public static class Drawing
{
    public static IReadOnlyList<IReadOnlyList<Vec2>> Rings(SectionProfile p) => SectionProperties.RingsOf(p.Shape()) ?? [];
}
