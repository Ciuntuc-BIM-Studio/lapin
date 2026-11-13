using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis;

/// <summary>
/// The site's snow (CR 1-1-3/2012, which follows EN 1991-1-3): the characteristic ground snow load s0,k (kN/m²) the user reads
/// for the site (CR 1-1-3's zone map, Figure 3.1, or its Annex A list of localities: Lapin does not ship the map), the exposure
/// coefficient Ce (0.8 complete exposure, 1.0 partial, 1.2 sheltered: CR 1-1-3 Table 4.1), the thermal coefficient Ct (1.0;
/// less only over a glazed roof that loses heat) and the importance factor γIs (CR 1-1-3 Table 4.2: 1.0 for ordinary
/// buildings). On a roof s = γIs · μi · Ce · Ct · s0,k.
/// </summary>
public sealed record SnowSite(double S0k, double Ce = 1.0, double Ct = 1.0, double Importance = 1.0)
{
    /// <summary>The snow load on the roof for a shape coefficient μ (kN/m² of the roof's plan).</summary>
    public double S(double mu) => Importance * mu * Ce * Ct * S0k;
}

/// <summary>
/// The site's wind (CR 1-1-4/2012, which follows EN 1991-1-4): the reference pressure qb (kN/m², the user's: CR 1-1-4's map
/// of qb at 10 m, Figure 2.1, or its Annex A), the terrain category (0, I, II, III, IV: EN 1991-1-4 Table 4.1, its roughness
/// length z0 and least height zmin) and the importance factor γIw (CR 1-1-4 Table 3.3: 1.0 for ordinary buildings). The peak
/// pressure at a height z over the ground is qp(z) = ce(z) · qb, with ce(z) = (1 + 7 Iv(z)) · cr(z)² (EN 1991-1-4 4.5, the
/// orography and turbulence factors 1): cr(z) = kr ln(z / z0), kr = 0.19 (z0 / 0.05)^0.07, Iv(z) = 1 / ln(z / z0), z not less
/// than zmin. On a surface w = γIw · cpe · qp(ze).
/// </summary>
public sealed record WindSite(double Qb, string Terrain = "II", double Importance = 1.0)
{
    public double Z0 => Climate.Terrains[Terrain].Z0;
    public double ZMin => Climate.Terrains[Terrain].ZMin;
    public double Kr => 0.19 * Math.Pow(Z0 / 0.05, 0.07);

    /// <summary>The exposure factor ce(z) at a height z (metres over the ground).</summary>
    public double Ce(double z)
    {
        var zz = Math.Max(z, ZMin);
        var ln = Math.Log(zz / Z0);
        var cr = Kr * ln;
        return (1 + 7 / ln) * cr * cr;
    }

    /// <summary>The peak pressure qp(z) = ce(z) · qb (kN/m²).</summary>
    public double Qp(double z) => Ce(z) * Qb;
}

/// <summary>
/// The site's climate on the IfcProject (<c>climate</c>: <c>{snow: {s0k, ce, ct, importance}, wind: {qb, terrain, importance}}</c>),
/// set by <c>analysis.setClimate</c>; the values are the user's (the national maps are theirs to read), the rules are
/// EN 1991-1-3 and EN 1991-1-4's as CR 1-1-3/2012 and CR 1-1-4/2012 take them, indicative. Setting them makes the snow and
/// wind cases that take the loads they derive (<see cref="EnsureCases"/>).
/// </summary>
public static class Climate
{
    public const string Key = "climate";
    public const string Indicative = "indicative: CR 1-1-3/2012 and CR 1-1-4/2012 (EN 1991-1-3, EN 1991-1-4, their recommended values); the site's s0,k and qb are yours";

    /// <summary>EN 1991-1-4 Table 4.1: each terrain category's roughness length z0 and least height zmin (metres).</summary>
    public static readonly IReadOnlyDictionary<string, (double Z0, double ZMin, string Label)> Terrains = new Dictionary<string, (double, double, string)>
    {
        ["0"] = (0.003, 1, "Sea or coastal area exposed to the open sea"),
        ["I"] = (0.01, 1, "Lakes, or flat land without obstacles"),
        ["II"] = (0.05, 2, "Low vegetation, isolated obstacles 20 heights apart"),
        ["III"] = (0.3, 5, "Regular cover of vegetation or buildings, villages, suburbs"),
        ["IV"] = (1.0, 10, "At least 15 % covered by buildings over 15 m high"),
    };

    /// <summary>The four directions a wind blows towards (along the project's axes), and the snow drift each leaves.</summary>
    public static readonly string[] Directions = ["+x", "-x", "+y", "-y"];
    /// <summary>A wind case's set of the roofs' external pressure coefficients: the least (suction) or the greatest (pressure).</summary>
    public static readonly string[] RoofSets = ["min", "max"];

    public static Lapin.Geometry.Vec2 Vector(string direction) => direction switch
    {
        "+x" => new(1, 0), "-x" => new(-1, 0), "+y" => new(0, 1), _ => new(0, -1),
    };

    public static string Label(string direction) => direction.ToUpperInvariant();

    public static SnowSite? Snow(IModelView m) =>
        Spatial.Project(m)?.Attrs[Key] is JsonObject c && c["snow"] is JsonObject s && Json.Number(s["s0k"]) is { } sk
            ? new SnowSite(sk, Json.Number(s["ce"]) ?? 1, Json.Number(s["ct"]) ?? 1, Json.Number(s["importance"]) ?? 1) : null;

    public static WindSite? Wind(IModelView m) =>
        Spatial.Project(m)?.Attrs[Key] is JsonObject c && c["wind"] is JsonObject w && Json.Number(w["qb"]) is { } qb
            ? new WindSite(qb, (string?)w["terrain"] is { } t && Terrains.ContainsKey(t) ? t : "II", Json.Number(w["importance"]) ?? 1) : null;

    static double Read(JsonObject o, string key, double fallback, double min, double max, string what)
    {
        if (!o.ContainsKey(key) || o[key] is null) return fallback;
        if (Json.Number(o[key]) is { } v && double.IsFinite(v) && v >= min && v <= max) return v;
        throw new CommandException($"{what} is a number from {min} to {max}.");
    }

    /// <summary>The snow's parameters from a command, checked.</summary>
    public static JsonObject WriteSnow(JsonObject a) => new()
    {
        ["s0k"] = a.ContainsKey("s0k") && a["s0k"] is not null ? Read(a, "s0k", 0, 0, 20, "The ground snow load s0,k (kN/m²)") : throw new CommandException("Give the site's ground snow load s0,k (kN/m²), as CR 1-1-3's map gives it."),
        ["ce"] = Read(a, "ce", 1, 0.5, 2, "The exposure coefficient Ce"),
        ["ct"] = Read(a, "ct", 1, 0.1, 1.5, "The thermal coefficient Ct"),
        ["importance"] = Read(a, "importance", 1, 0.5, 2, "The importance factor γIs"),
    };

    /// <summary>The wind's parameters from a command, checked.</summary>
    public static JsonObject WriteWind(JsonObject a)
    {
        var terrain = (string?)a["terrain"] ?? "II";
        if (!Terrains.ContainsKey(terrain)) throw new CommandException("The terrain category is 0, I, II, III or IV.");
        return new JsonObject
        {
            ["qb"] = a.ContainsKey("qb") && a["qb"] is not null ? Read(a, "qb", 0, 0, 10, "The reference wind pressure qb (kN/m²)") : throw new CommandException("Give the site's reference wind pressure qb (kN/m²), as CR 1-1-4's map gives it."),
            ["terrain"] = terrain,
            ["importance"] = Read(a, "importance", 1, 0.5, 2, "The importance factor γIw"),
        };
    }

    /// <summary>
    /// The cases the climate's loads go in, made when missing (one undo step with the command that asks): snow, its undrifted
    /// arrangement (a case of category snow that derives nothing — the default S — takes it) and its drifted ones, the wind
    /// blowing towards +X, −X, +Y and −Y (EN 1991-1-3 §5.3.3 (ii), (iii)); wind, each direction with the roofs' least
    /// coefficients (suction) and their greatest (pressure; EN 1991-1-4 Table 7.4a's two sets), the default W (category wind,
    /// deriving nothing, holding no load of the user's) becoming the first. The ids made or changed.
    /// </summary>
    public static List<string> EnsureCases(ICommandContext ctx, bool snow, bool wind)
    {
        var o = LoadCases.EnsureDefaults(ctx);
        if (snow)
        {
            foreach (var dir in new string?[] { null }.Concat(Directions))
            {
                var cases = LoadCases.All(ctx.Model);
                if (LoadCases.Taking(cases, LoadCases.Snow, Categories.Snow, dir) != null) continue;
                if (dir == null && cases.FirstOrDefault(c => c.Variable && c.Category == Categories.Snow && c.Derives.Count == 0) is { } plain
                    && ctx.Model.Get(plain.Id) is { } e)
                {
                    var attrs = e.CloneAttrs();
                    attrs["derives"] = new JsonArray(LoadCases.Snow);
                    ctx.Replace(e.With(attrs: attrs));
                    o.Add(e.Id);
                    continue;
                }
                o.Add(LoadCases.Make(ctx, LoadCases.Unique(cases, dir == null ? "S" : $"S{Label(dir)}"),
                    dir == null ? "Snow" : $"Snow, drifted by wind towards {Label(dir)}", Categories.Snow, LoadCases.Snow, dir, null));
            }
        }
        if (wind)
        {
            foreach (var dir in Directions)
                foreach (var set in RoofSets)
                {
                    var cases = LoadCases.All(ctx.Model);
                    if (LoadCases.Taking(cases, LoadCases.Wind, Categories.Wind, dir, set) != null) continue;
                    var code = $"W{Label(dir)}/{(set == "min" ? "s" : "p")}";
                    var name = $"Wind towards {Label(dir)}, roof {(set == "min" ? "suction" : "pressure")}";
                    if (cases.FirstOrDefault(c => c.Variable && c.Category == Categories.Wind && c.Derives.Count == 0 && !LoadCases.UserLoadsOf(ctx.Model, c.Id).Any()) is { } plain
                        && ctx.Model.Get(plain.Id) is { } e)
                    {
                        var attrs = e.CloneAttrs();
                        attrs["derives"] = new JsonArray(LoadCases.Wind);
                        attrs["windDirection"] = dir;
                        attrs["roofCpe"] = set;
                        attrs["code"] = LoadCases.Unique(cases.Where(c => c.Id != e.Id).ToList(), code);
                        ctx.Replace(e.With(name: name, attrs: attrs));
                        o.Add(e.Id);
                        continue;
                    }
                    o.Add(LoadCases.Make(ctx, LoadCases.Unique(cases, code), name, Categories.Wind, LoadCases.Wind, dir, set));
                }
        }
        return o;
    }

    public static JsonObject ToJson(IModelView m)
    {
        var s = Snow(m);
        var w = Wind(m);
        return new JsonObject
        {
            ["snow"] = s == null ? null : new JsonObject { ["s0k"] = s.S0k, ["ce"] = s.Ce, ["ct"] = s.Ct, ["importance"] = s.Importance },
            ["wind"] = w == null ? null : new JsonObject { ["qb"] = w.Qb, ["terrain"] = w.Terrain, ["importance"] = w.Importance },
            ["terrains"] = new JsonArray(Terrains.Select(t => (JsonNode)new JsonObject { ["key"] = t.Key, ["z0"] = t.Value.Z0, ["zmin"] = t.Value.ZMin, ["label"] = t.Value.Label }).ToArray()),
        };
    }
}
