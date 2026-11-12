using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis;

/// <summary>One category of use: its key (A, C1…), what it is, and its imposed load qk (kN/m²) by default.</summary>
public sealed record UseCategory(string Key, string Label, double Qk);

/// <summary>ψ0, ψ1, ψ2 of a variable action (EN 1990 Table A1.1).</summary>
public sealed record Psi(double Psi0, double Psi1, double Psi2)
{
    public JsonObject ToJson() => new() { ["0"] = Psi0, ["1"] = Psi1, ["2"] = Psi2 };
}

/// <summary>
/// The categories of use and their imposed loads (docs/plan-fem.md §8.4): EN 1991-1-1 Tables 6.2 and 6.10, the recommended
/// values, indicative; a project gives its national annex's own (<c>imposedLoads</c> on the IfcProject). The ψ factors of
/// each category, of snow and of wind (EN 1990 Annex A1, Table A1.1, recommended, indicative). A room's category: its own
/// <c>loadCategory</c>, else derived from its function; <c>none</c> carries no load.
/// </summary>
public static class Categories
{
    public const string None = "none", Snow = "snow", Wind = "wind";
    public const string ProjectKey = "imposedLoads";
    public const string RoomKey = "loadCategory";
    public const string Indicative = "indicative: EN 1991-1-1 Tables 6.2 and 6.10, recommended values; set the national annex's with analysis.setImposedLoad";

    public static readonly IReadOnlyList<UseCategory> All =
    [
        new("A", "Residential: rooms", 2.0),
        new("A-stairs", "Residential: stairs", 2.0),
        new("A-balcony", "Residential: balconies", 2.5),
        new("B", "Offices", 3.0),
        new("C1", "With tables (schools, cafés, restaurants)", 3.0),
        new("C2", "With fixed seats (churches, theatres, conference rooms)", 4.0),
        new("C3", "Without obstacles (museums, halls of public buildings)", 5.0),
        new("C4", "Physical activities (dance halls, gymnasia)", 5.0),
        new("C5", "Crowds (concert halls, sports halls, stands)", 5.0),
        new("D1", "Small shops", 4.0),
        new("D2", "Department stores", 5.0),
        new("E1", "Storage", 7.5),
        new("F", "Garages, vehicles ≤ 30 kN", 2.5),
        new("G", "Vehicles 30–160 kN", 5.0),
        new("H", "Roofs not accessible", 0.4),
    ];

    public static UseCategory? Of(string? key) => All.FirstOrDefault(c => c.Key == key);

    /// <summary>A variable case's category: a category of use, snow or wind.</summary>
    public static bool IsCaseCategory(string? key) => key is Snow or Wind || Of(key) != null;

    /// <summary>The project's own qk (kN/m²) by category, as stored on the IfcProject.</summary>
    public static Dictionary<string, double> Overrides(IModelView m)
    {
        var o = new Dictionary<string, double>();
        if (Spatial.Project(m)?.Attrs[ProjectKey] is JsonObject own)
            foreach (var (k, v) in own)
                if (Of(k) != null && Json.Number(v) is { } q && double.IsFinite(q) && q >= 0) o[k] = q;
        return o;
    }

    /// <summary>The imposed load of a category (kN/m²): the project's, else the default; null for an unknown key.</summary>
    public static double? Qk(IModelView m, string key) => Overrides(m).TryGetValue(key, out var q) ? q : Of(key)?.Qk;

    /// <summary>The category a room's function says (Living, Bedroom… → A; Balcony → A-balcony; Office → B; Storage, Technical → E1; Garage → F; anything else A).</summary>
    public static string FromFunction(string? function) => (function ?? "").Trim().ToLowerInvariant() switch
    {
        "living" or "bedroom" or "kitchen" or "dining" or "bath" or "wc" or "circulation" => "A",
        "balcony" => "A-balcony",
        "office" => "B",
        "storage" or "technical" => "E1",
        "garage" => "F",
        _ => "A",
    };

    /// <summary>A room's category: its <c>loadCategory</c> (a key, or none), else its function's.</summary>
    public static string OfRoom(Entity room) =>
        room.Str(RoomKey) is { } own && (own == None || Of(own) != null) ? own : FromFunction(room.Str("function"));

    /// <summary>
    /// The ψ factors of a category (EN 1990 Table A1.1, recommended): A and B 0.7/0.5/0.3; C and D 0.7/0.7/0.6; E1 1.0/0.9/0.8;
    /// F 0.7/0.7/0.6; G 0.7/0.5/0.3; H 0/0/0; snow (sites up to 1000 m) 0.5/0.2/0; wind 0.6/0.2/0; null without a category
    /// (the case then takes A's, and the report says so).
    /// </summary>
    public static Psi? TableOf(string? category)
    {
        if (category == null) return null;
        if (category == Snow) return new(0.5, 0.2, 0);
        if (category == Wind) return new(0.6, 0.2, 0);
        if (category.StartsWith('A') || category == "B" || category == "G") return new(0.7, 0.5, 0.3);
        if (category.StartsWith('C') || category.StartsWith('D') || category == "F") return new(0.7, 0.7, 0.6);
        if (category == "E1") return new(1.0, 0.9, 0.8);
        if (category == "H") return new(0, 0, 0);
        return null;
    }

    /// <summary>What a case without a category takes (A's), said in the report.</summary>
    public static readonly Psi Fallback = new(0.7, 0.5, 0.3);
}
