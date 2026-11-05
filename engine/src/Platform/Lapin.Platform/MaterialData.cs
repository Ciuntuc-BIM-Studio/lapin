using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// How a material is measured in the takeoff and bought (docs/plan-buildup-tags.md, <c>measure</c> on the IfcMaterial):
/// by <see cref="By"/> area (m², a row per thickness: boards, insulation, membranes), volume (m³: concrete, ballast,
/// screed), mass (kg, from its density) or count (pieces of a board of fixed size); its catalogue, the <see cref="Thicknesses"/>
/// it is made in (mm: offered in the types' layer tables, a layer of another thickness flagged); the <see cref="Unit"/> it is bought in (a label); the
/// <see cref="Board"/> a piece is (length × width, metres). A material without one is measured as it always was.
/// </summary>
public sealed record MaterialMeasure(string By, IReadOnlyList<double>? Thicknesses = null, string? Unit = null, (double Length, double Width)? Board = null)
{
    public static readonly string[] Kinds = ["area", "volume", "mass", "count"];

    /// <summary>The material's rule, null when it has none (or it cannot be read: a hand-edited file).</summary>
    public static MaterialMeasure? Of(Entity? material)
    {
        if (material?.Attrs["measure"] is not JsonObject o || (string?)o["by"] is not { } by || !Kinds.Contains(by)) return null;
        var sizes = o["thicknesses"] is JsonArray t ? t.Select(Json.Number).OfType<double>().Where(x => x > 0 && double.IsFinite(x)).Distinct().OrderBy(x => x).ToList() : null;
        (double, double)? board = o["board"] is JsonArray { Count: 2 } b && Json.Number(b[0]) is { } bl && Json.Number(b[1]) is { } bw && bl > 0 && bw > 0 ? (bl, bw) : null;
        return new(by, sizes is { Count: > 0 } ? sizes : null, (string?)o["unit"] is { Length: > 0 } u ? u : null, board);
    }

    /// <summary>A command's rule, checked and as it is stored; null takes it off.</summary>
    public static JsonObject? Parse(JsonNode? node)
    {
        if (node is null) return null;
        if (node is not JsonObject o) throw new CommandException("A material's measure is {by, thicknesses?, unit?, board?}.");
        var by = (string?)o["by"];
        if (by is null || !Kinds.Contains(by)) throw new CommandException($"A material is measured by {string.Join(", ", Kinds)}.");
        var r = new JsonObject { ["by"] = by };
        if (o["thicknesses"] is JsonArray t && t.Count > 0)
        {
            var sizes = new List<double>();
            foreach (var x in t)
            {
                if (Json.Number(x) is not { } v || !double.IsFinite(v) || v <= 0 || v > 10000) throw new CommandException("A commercial thickness is a number of millimetres above zero.");
                sizes.Add(Math.Round(v, 1));
            }
            if (by != "area" && by != "count") throw new CommandException("Commercial thicknesses are for a material measured by area or by count.");
            r["thicknesses"] = new JsonArray(sizes.Distinct().OrderBy(x => x).Select(x => (JsonNode)x).ToArray());
        }
        else if (o["thicknesses"] is not null and not JsonArray) throw new CommandException("Commercial thicknesses are a list of millimetres.");
        if (((string?)o["unit"])?.Trim() is { Length: > 0 } unit)
        {
            if (unit.Length > 16) throw new CommandException("A unit is a short label (m², m³, kg, t, pcs).");
            r["unit"] = unit;
        }
        if (o["board"] is JsonArray { Count: 2 } b)
        {
            if (Json.Number(b[0]) is not { } bl || Json.Number(b[1]) is not { } bw || !(bl > 0 && bw > 0 && bl <= 100 && bw <= 100))
                throw new CommandException("A board is [length, width] in metres, above zero.");
            r["board"] = new JsonArray(bl, bw);
        }
        else if (o["board"] is not null) throw new CommandException("A board is [length, width] in metres.");
        return r;
    }

    /// <summary>The material's density (kg/m³): Pset_MaterialCommon.MassDensity, else a parameter named Density (ρ, Mass density) in any set; null without one.</summary>
    public static double? DensityOf(Entity? material)
    {
        if (material == null) return null;
        var f = MaterialFields.Of(material);
        foreach (var key in new[] { "Pset_MaterialCommon.MassDensity", "Density", "MassDensity", "ρ" })
            if (f.Param(key) is { } p && Json.Number(p.Value) is { } d && d > 0) return d;
        return material.Attrs["density"] is { } own && Json.Number(own) is { } od && od > 0 ? od : null;
    }

    /// <summary>The board a piece is (metres): the rule's, else the parameters Board length and Board width; null without one.</summary>
    public (double Length, double Width)? BoardOf(Entity material)
    {
        if (Board is { } b) return b;
        var f = MaterialFields.Of(material);
        return f.Param("Board length") is { } l && f.Param("Board width") is { } w && Json.Number(l.Value) is { } lv && Json.Number(w.Value) is { } wv && lv > 0 && wv > 0
            ? (lv, wv) : null;
    }

    /// <summary>
    /// Whether a thickness (mm) is one the material is made in: any when it has no catalogue (<see cref="Thicknesses"/>),
    /// else one of them to a tenth of a millimetre. The user lays the build-ups in these; Lapin only flags the others.
    /// </summary>
    public static bool InCatalogue(MaterialMeasure? rule, double thicknessMm) =>
        rule?.Thicknesses is not { Count: > 0 } sizes || thicknessMm <= 1e-6 || sizes.Any(x => Math.Abs(x - thicknessMm) < 0.05);

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["by"] = By };
        if (Thicknesses != null) o["thicknesses"] = new JsonArray(Thicknesses.Select(x => (JsonNode)x).ToArray());
        if (Unit != null) o["unit"] = Unit;
        if (Board is { } b) o["board"] = new JsonArray(b.Length, b.Width);
        return o;
    }
}

/// <summary>One parameter of a material: its name, its set (null: the user's own), its kind and value, as stored.</summary>
public sealed record MaterialParam(string Name, string? Pset, string Kind, JsonNode? Value, JsonObject Stored)
{
    /// <summary>The field that names it: "Set.Name" in a set, else the name.</summary>
    public string Key => Pset is null ? Name : $"{Pset}.{Name}";
}

/// <summary>
/// What a material says of itself (docs/plan-buildup-tags.md), for a build-up tag, a schedule, a document: its name,
/// description, category, measure and parameters, each parameter found by its name, by "Set.Name", by its name without
/// spaces or case ({ThermalConductivity}, {Thermal conductivity}) and by its symbol (λ, μ, ρ). Values are as stored:
/// a caller writes them (<see cref="Text"/> does it as a tag does).
/// </summary>
public sealed class MaterialFieldSet
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public MaterialMeasure? Measure { get; init; }
    public required IReadOnlyList<MaterialParam> Params { get; init; }

    /// <summary>Symbols a field may give for a standard property (the first the material has).</summary>
    static readonly Dictionary<string, string[]> Symbols = new(StringComparer.Ordinal)
    {
        ["λ"] = ["Pset_MaterialThermal.ThermalConductivity"],
        ["lambda"] = ["Pset_MaterialThermal.ThermalConductivity"],
        ["μ"] = ["Pset_MaterialHygroscopic.UpperVaporResistanceFactor", "Pset_MaterialHygroscopic.LowerVaporResistanceFactor"],
        ["µ"] = ["Pset_MaterialHygroscopic.UpperVaporResistanceFactor", "Pset_MaterialHygroscopic.LowerVaporResistanceFactor"],
        ["mu"] = ["Pset_MaterialHygroscopic.UpperVaporResistanceFactor", "Pset_MaterialHygroscopic.LowerVaporResistanceFactor"],
        ["ρ"] = ["Pset_MaterialCommon.MassDensity"],
        ["density"] = ["Pset_MaterialCommon.MassDensity"],
    };

    /// <summary>The parameter a field names (null when the material has none of it): "Set.Name" in that set, a name in any set (the user's own first), a symbol.</summary>
    public MaterialParam? Param(string field)
    {
        var key = field.Trim();
        var dot = key.IndexOf('.');
        if (dot > 0)
        {
            var (set, name) = (key[..dot].Trim(), TagText.Squash(key[(dot + 1)..]));
            var own = set.Equals(StandardPsets.Own, StringComparison.OrdinalIgnoreCase) || set.Equals(StandardPsets.MaterialOwn, StringComparison.OrdinalIgnoreCase);
            var hit = Params.FirstOrDefault(p => TagText.Squash(p.Name) == name && (own ? p.Pset == null : string.Equals(p.Pset, set, StringComparison.OrdinalIgnoreCase)));
            if (hit != null) return hit;
        }
        var k = TagText.Squash(key);
        if (k.Length > 0 && Params.Where(p => TagText.Squash(p.Name) == k).OrderBy(p => p.Pset == null ? 0 : 1).FirstOrDefault() is { } named) return named;
        if (Symbols.TryGetValue(key.ToLowerInvariant(), out var standard) || Symbols.TryGetValue(key, out standard))
            foreach (var s in standard) if (Param(s) is { } p) return p;
        return null;
    }

    /// <summary>
    /// A field as a tag writes it: name (material), description (else the name), category, or a parameter (a length in
    /// <paramref name="units"/>, an area in m², yes or no); null when the material has nothing of that name.
    /// </summary>
    public string? Text(string field, string units = "mm")
    {
        switch (field.Trim().ToLowerInvariant())
        {
            case "name": case "material": return Name;
            case "description": return Description is { Length: > 0 } d ? d : Name;
            case "category": return Category;
        }
        return Param(field) is { } p ? TagText.ParamText(p.Stored, units) : null;
    }
}

/// <summary>A material's fields (docs/plan-buildup-tags.md): what a build-up tag, a schedule or a document reads of it.</summary>
public static class MaterialFields
{
    public static MaterialFieldSet Of(IModelView m, Entity material) => Of(material);

    public static MaterialFieldSet Of(Entity material) => new()
    {
        Id = material.Id,
        Name = material.Name,
        Description = material.Str("description") is { Length: > 0 } d ? d : null,
        Category = material.Str("category") is { Length: > 0 } c ? c : null,
        Measure = MaterialMeasure.Of(material),
        Params = (material.Attrs["params"] as JsonArray)?.OfType<JsonObject>()
            .Where(p => ((string?)p["name"])?.Trim() is { Length: > 0 })
            .Select(p => new MaterialParam(((string?)p["name"])!.Trim(), Params.SetOf(p), (string?)p["kind"] ?? "text", p["value"], p)).ToList() ?? [],
    };

    /// <summary>A material's data as a command sets it: its description (text, its lines kept), its parameters and its measure, each present key replacing (null takes it off).</summary>
    public static void Apply(JsonObject attrs, JsonObject a)
    {
        if (a.ContainsKey("description"))
        {
            var dn = a["description"];
            if (dn is null) attrs.Remove("description");
            else if (dn is JsonValue dv && dv.TryGetValue<string>(out var d))
            {
                if (d.Length > 8000) throw new CommandException("A material's description is at most 8000 characters.");
                if (d.Trim().Length == 0) attrs.Remove("description"); else attrs["description"] = d.Replace("\r\n", "\n").TrimEnd();
            }
            else throw new CommandException("A material's description is text.");
        }
        if (a.ContainsKey("params"))
        {
            var list = Params.ParseList(a["params"], IfcClass.Material);
            if (list.Count > 0) attrs["params"] = list; else attrs.Remove("params");
        }
        if (a.ContainsKey("measure"))
        {
            if (MaterialMeasure.Parse(a["measure"]) is { } measure) attrs["measure"] = measure; else attrs.Remove("measure");
        }
    }

    /// <summary>The material's data for the interface: description, parameters and measure, as stored.</summary>
    public static void Write(Entity material, JsonObject into)
    {
        into["description"] = material.Str("description");
        into["params"] = material.Attrs["params"]?.DeepClone() ?? new JsonArray();
        into["measure"] = material.Attrs["measure"]?.DeepClone();
        if (MaterialMeasure.DensityOf(material) is { } density) into["density"] = density;
    }

    /// <summary>A number as the takeoff writes a thickness: millimetres to a tenth, whole ones without decimals.</summary>
    public static string Mm(double mm) => (Math.Round(mm * 10) / 10).ToString("0.#", CultureInfo.InvariantCulture);
}
