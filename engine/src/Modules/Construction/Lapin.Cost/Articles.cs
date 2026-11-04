using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Cost;

/// <summary>What a price list's unit measures and how the model's SI value turns into it.</summary>
public static class Units
{
    /// <summary>A unit's measure (volume, area, length, count, weight) and the factor the model's SI value is multiplied by to be in it; null when Lapin cannot measure it.</summary>
    public static (string Basis, double Factor)? BasisOf(string? unit)
    {
        var u = (unit ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace(".", "");
        return u switch
        {
            "m3" or "m³" or "mc" or "cum" or "cbm" => ("volume", 1),
            "m2" or "m²" or "mp" or "sqm" => ("area", 1),
            "100m2" or "100mp" or "100m²" => ("area", 0.01),
            "m" or "ml" or "lm" or "rm" => ("length", 1),
            "100m" or "100ml" => ("length", 0.01),
            "buc" or "pcs" or "pc" or "nr" or "no" or "u" or "ea" or "each" or "stk" or "szt" or "un" or "pz" or "piece" or "pièce" or "set" => ("count", 1),
            "kg" => ("weight", 1),
            "t" or "to" or "tonne" or "tona" or "tone" => ("weight", 0.001),
            _ => null,
        };
    }

    /// <summary>The unit a measure is shown in when the article's price has none.</summary>
    public static string UnitOf(string basis) => basis switch { "volume" or "grossVolume" => "m³", "area" or "grossArea" => "m²", "length" => "m", "weight" => "kg", _ => "pcs" };

    /// <summary>IFC's kind of quantity for a measure.</summary>
    public static string KindOf(string basis) => basis switch
    {
        "volume" or "grossVolume" => "VOLUME", "area" or "grossArea" => "AREA", "length" => "LENGTH", "weight" => "WEIGHT", _ => "COUNT",
    };
}

/// <summary>
/// An article on a type (docs/plan-4d5d.md, stage 6: <c>articles</c> on any type — a wall, a slab, a door, a room
/// finish, a formwork, a steel profile): the price list's code, the measure its quantity is read by (auto: by the price's
/// unit; volume, gross volume, area, gross area, length, count, weight, or a quantity's own name), the part of the
/// element it measures (all, the core, the finishes, a layer, a material, a category of material), a factor, the waste
/// (%) and the chapter it goes under (null: the element's).
/// </summary>
public sealed record ArticleRef(string Code, string Basis = "auto", string? Part = null, double Factor = 1, double Waste = 0, string? Chapter = null)
{
    public static readonly string[] Bases = ["auto", "volume", "grossVolume", "area", "grossArea", "length", "count", "weight"];

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["code"] = Code };
        if (Basis != "auto") o["basis"] = Basis;
        if (Part != null) o["part"] = Part;
        if (Factor != 1) o["factor"] = Factor;
        if (Waste != 0) o["waste"] = Waste;
        if (Chapter is { Length: > 0 }) o["chapter"] = Chapter;
        return o;
    }

    public static ArticleRef? Read(JsonNode? n)
    {
        if (n is not JsonObject o || ((string?)o["code"])?.Trim() is not { Length: > 0 } code) return null;
        var basis = ((string?)o["basis"])?.Trim() is { Length: > 0 } b ? b : "auto";
        var part = ((string?)o["part"])?.Trim() is { Length: > 0 } p && p != "all" ? p : null;
        var factor = Json.Number(o["factor"]) is { } f && double.IsFinite(f) ? f : 1;
        var waste = Json.Number(o["waste"]) is { } w && double.IsFinite(w) ? w : 0;
        return new(code, basis, part, factor, waste, ((string?)o["chapter"])?.Trim() is { Length: > 0 } c ? c : null);
    }

    /// <summary>A command's articles, checked.</summary>
    public static List<ArticleRef> Parse(JsonNode? n)
    {
        if (n is null) return [];
        if (n is not JsonArray a) throw new CommandException("Articles are a list.");
        var o = new List<ArticleRef>();
        foreach (var x in a)
        {
            var r = Read(x) ?? throw new CommandException("Each article needs a code.");
            if (!Bases.Contains(r.Basis) && !char.IsUpper(r.Basis[0])) throw new CommandException($"„{r.Basis}” is not a measure: auto, volume, area, length, count, weight, or a quantity's name.");
            if (r.Factor <= 0) throw new CommandException("An article's factor is positive.");
            if (r.Waste < 0 || r.Waste > 100) throw new CommandException("The waste is a percentage between 0 and 100.");
            o.Add(r);
        }
        return o;
    }

    /// <summary>
    /// A type's articles: its own list; else the deviz article a site works type has had since decision 152
    /// (<c>spec.article</c> on formwork and scaffolding types), one article by the price's unit.
    /// </summary>
    public static List<ArticleRef> Of(Entity? type)
    {
        if (type == null) return [];
        if (type.Attrs["articles"] is JsonArray a) return a.Select(Read).OfType<ArticleRef>().ToList();
        if (type.Attrs["spec"] is JsonObject spec && ((string?)spec["article"])?.Trim() is { Length: > 0 } code) return [new ArticleRef(code)];
        return [];
    }
}

/// <summary>
/// An article rule (<c>LapinArticleRule</c> on the project): every layer of a material (or of a category of material)
/// is priced by an article, so one row covers all the types that use it ("any plaster → article X by area"). An
/// article on the type comes first: a layer its type's articles take is not ruled.
/// </summary>
public sealed record ArticleRule(string Id, string? Material, string? Category, ArticleRef Article)
{
    public const string Class = "LapinArticleRule";

    public static ArticleRule? Read(Entity e) => ArticleRef.Read(e.Attrs) is { } a && (e.Str("material") != null || e.Str("category") != null)
        ? new(e.Id, e.Str("material"), e.Str("category"), a with { Part = null }) : null;

    public static List<ArticleRule> All(IModelView m) => m.OfClass(Class).Select(Read).OfType<ArticleRule>().ToList();

    public bool Matches(IModelView m, string? materialId)
    {
        if (materialId == null) return false;
        if (Material != null) return Material == materialId;
        return Category != null && Parts.InCategory(m, materialId, Category);
    }
}

/// <summary>The parts of an element an article can measure, as the element's quantity sets have them (a layer, a zone, a finish: their own set).</summary>
public static class Parts
{
    public const string Core = "core", Finishes = "finishes", LayerPrefix = "layer:", MaterialPrefix = "material:", CategoryPrefix = "category:";
    /// <summary>An article on the element's demolition: the whole element, priced only when it is demolished (stage 7, the phases).</summary>
    public const string Demolition = "demolition";

    /// <summary>A part as a command gives it, checked; null for the whole element.</summary>
    public static string? Parse(string? s)
    {
        s = s?.Trim();
        if (string.IsNullOrEmpty(s) || s == "all") return null;
        if (s is Core or Finishes or Demolition) return s;
        foreach (var p in new[] { LayerPrefix, MaterialPrefix, CategoryPrefix })
            if (s.StartsWith(p, StringComparison.Ordinal) && s.Length > p.Length) return s;
        throw new CommandException("An article measures the whole element, its core, its finishes, a layer (layer:0), a material (material:<id>), a category of material (category:Finish) or its demolition.");
    }

    /// <summary>Whether a material is of a category: its own, else (for Finish) one by its name, as view styles tell finishes.</summary>
    public static bool InCategory(IModelView m, string materialId, string category)
    {
        if (m.Get(materialId) is not { Class: IfcClass.Material } mat) return false;
        if (mat.Str("category") is { Length: > 0 } c) return string.Equals(c.Trim(), category, StringComparison.OrdinalIgnoreCase);
        return string.Equals(category, "Finish", StringComparison.OrdinalIgnoreCase) && Platform.Materials.IsFinish(mat);
    }

    /// <summary>The index of the layer a part set is of ("layer:2", "layer:2:&lt;material&gt;"), null when it is not a layer's.</summary>
    public static int? LayerIndex(string? part)
    {
        if (part == null || !part.StartsWith(LayerPrefix, StringComparison.Ordinal)) return null;
        var rest = part[LayerPrefix.Length..];
        var colon = rest.IndexOf(':');
        return int.TryParse(colon < 0 ? rest : rest[..colon], out var i) ? i : null;
    }

    /// <summary>Whether a part set is the core: the core layer's, or a zone of a zoned core.</summary>
    static bool IsCore(QuantitySet s, int? core) =>
        s.Part != null && (LayerIndex(s.Part) is { } i && i == core || s.Part.StartsWith("zone:", StringComparison.Ordinal));

    /// <summary>
    /// The sets of an element a part takes: the whole element's (the first without a part; an element measured only by
    /// parts, all of them); the core (an element without layers is its own core); the finishes (every layer but the core);
    /// a layer; a material's or a category's layers and parts.
    /// </summary>
    public static List<QuantitySet> Select(IModelView m, string elementId, IReadOnlyList<QuantitySet> sets, string? part)
    {
        var main = sets.Where(s => s.Part == null).Take(1).ToList();
        var parts = sets.Where(s => s.Part != null).ToList();
        if (part == null) return main.Count > 0 ? main : parts;
        var bands = LayerSets.BandsOf(m, elementId);
        int? core = bands?.FirstOrDefault(b => b.Core) is { Core: true } c ? c.Index : null;
        var layered = parts.Any(s => LayerIndex(s.Part) != null);
        switch (part)
        {
            case Core:
                if (!layered) return main.Count > 0 ? main : parts;
                return parts.Where(s => IsCore(s, core)).ToList();
            case Finishes:
                return parts.Where(s => LayerIndex(s.Part) != null && !IsCore(s, core)).ToList();
        }
        if (part.StartsWith(LayerPrefix, StringComparison.Ordinal) && int.TryParse(part[LayerPrefix.Length..], out var li))
            return parts.Where(s => LayerIndex(s.Part) == li).ToList();
        if (part.StartsWith(MaterialPrefix, StringComparison.Ordinal))
        {
            var mat = part[MaterialPrefix.Length..];
            var hit = parts.Where(s => s.Material == mat).ToList();
            return hit.Count > 0 || parts.Count > 0 ? hit : main.Where(_ => Costing.OwnMaterial(m, elementId) == mat).ToList();
        }
        if (part.StartsWith(CategoryPrefix, StringComparison.Ordinal))
        {
            var cat = part[CategoryPrefix.Length..];
            return parts.Where(s => s.Material != null && InCategory(m, s.Material, cat)).ToList();
        }
        return [];
    }
}

/// <summary>Reading an article's quantity off an element's sets.</summary>
public static class Measures
{
    static readonly Dictionary<string, string[]> Names = new()
    {
        ["volume"] = ["NetVolume", "Volume", "GrossVolume"],
        ["grossVolume"] = ["GrossVolume", "NetVolume", "Volume"],
        ["area"] = ["NetSideArea", "NetArea", "NetFloorArea", "NetSurfaceArea", "ContactArea", "Area", "GrossSideArea", "GrossArea", "GrossFloorArea", "GrossSurfaceArea", "OuterSurfaceArea"],
        ["grossArea"] = ["GrossSideArea", "GrossArea", "GrossFloorArea", "GrossSurfaceArea", "ContactArea", "Area", "NetSideArea", "NetArea", "NetFloorArea", "NetSurfaceArea"],
        ["length"] = ["Length", "Perimeter", "FormedLength"],
        ["weight"] = ["NetWeight", "GrossWeight", "Weight"],
    };

    /// <summary>The measure an article reads: its own, else (auto) the price's unit's, else a count.</summary>
    public static string BasisFor(ArticleRef a, PriceRow? price) =>
        a.Basis != "auto" ? a.Basis : Units.BasisOf(price?.Unit)?.Basis ?? "count";

    /// <summary>A set's value of a measure (null when it has none of the names): the first name it has.</summary>
    public static double? Of(QuantitySet s, string basis)
    {
        if (basis == "count") return 1;
        var names = Names.TryGetValue(basis, out var n) ? n : [basis];
        foreach (var name in names) if (s.Quantities.FirstOrDefault(q => q.Name == name) is { } hit) return hit.Value;
        return null;
    }

    /// <summary>
    /// What an element gives an article, in the article's price unit: the measure summed over the sets its part takes
    /// (a count is one an element), times the factor, plus the waste. Null when the part has nothing of the measure.
    /// </summary>
    public static double? Quantity(IReadOnlyList<QuantitySet> sets, string basis, ArticleRef a, PriceRow? price)
    {
        if (sets.Count == 0) return null;
        double? sum = null;
        if (basis == "count") sum = 1;
        else foreach (var s in sets) if (Of(s, basis) is { } v) sum = (sum ?? 0) + v;
        if (sum is not { } q) return null;
        var unitFactor = Units.BasisOf(price?.Unit) is { } u && SameMeasure(u.Basis, basis) ? u.Factor : 1;
        return q * unitFactor * a.Factor * (1 + a.Waste / 100);
    }

    static bool SameMeasure(string unitBasis, string basis) => basis.Equals(unitBasis, StringComparison.Ordinal)
        || unitBasis == "volume" && basis == "grossVolume" || unitBasis == "area" && basis == "grossArea";
}
