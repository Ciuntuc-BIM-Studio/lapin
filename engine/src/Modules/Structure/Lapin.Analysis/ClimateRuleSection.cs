using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis;

/// <summary>
/// The climate in a rule pack (docs/plan-national-packs.md, stage N2): a list of regions (for Sweden the kommun, for another
/// country its county or locality), each giving the characteristic ground snow load sk (kN/m²) and the basic wind velocity
/// vb,0 (m/s) or, where the national rules give it so, the reference pressure qb (kN/m²). Loads → Snow and wind picks a region
/// (<c>analysis.setClimate {region}</c>) and fills the site's s0,k and qb from it; typing the values stays. A list, not map
/// polygons (the user's answer 4). Lapin's own pack ships no regions (its maps are the user's to read), so a project's climate
/// stays what it was until the user picks one. Every value is the pack's writer's, indicative.
/// </summary>
public sealed class ClimateRuleSection : IRuleSection
{
    public const string SectionKey = "climate";
    public string Key => SectionKey;
    public string Label => "Climate";

    public static readonly ClimateRuleSection Instance = new();

    /// <summary>The air's density for qb from vb,0: EN 1991-1-4 4.5's recommended 1.25 kg/m³ (qb = ½ ρ vb², cdir and cseason 1).</summary>
    public const double AirDensity = 1.25;
    public const string DefaultRegionLabel = "Region";
    const int MaxRegions = 500;

    /// <summary>A region of the list: its name, sk (null: the region says nothing of snow), vb,0 or qb (null: nothing of wind).</summary>
    public sealed record Region(string Name, double? Sk, double? Vb0, double? QbGiven, string? Note)
    {
        /// <summary>The reference wind pressure (kN/m²): the region's own, else ½ ρ vb,0² (N/m² → kN/m²).</summary>
        public double? Qb => QbGiven ?? (Vb0 is { } v ? Math.Round(0.5 * AirDensity * v * v / 1000, 4) : null);

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["name"] = Name };
            if (Sk is { } s) o["sk"] = s;
            if (Vb0 is { } v) o["vb0"] = v;
            if (QbGiven is { } q) o["qb"] = q;
            if (Note is { } n) o["note"] = n;
            return o;
        }
    }

    public JsonObject Default => new() { ["regionLabel"] = DefaultRegionLabel, ["regions"] = new JsonArray() };

    /// <summary>
    /// A pack's climate, checked and completed: the label of its regions (Region by default), its regions each with a name (1 to
    /// 80 characters, once), sk 0 to 20 kN/m², vb,0 0 to 60 m/s or qb 0 to 10 kN/m² (not both), at least one of snow and wind, a
    /// note of up to 200 characters. A value out of its range is refused with why.
    /// </summary>
    public JsonObject Check(JsonNode? section)
    {
        if (section is null) return Default;
        if (section is not JsonObject o) throw new CommandException("The climate is an object: {regionLabel, regions}.");
        var label = o["regionLabel"] is null ? DefaultRegionLabel
            : (string?)o["regionLabel"] is { } l && l.Trim() is { Length: > 0 and <= 40 } lt ? lt : throw new CommandException("The regions' label (Kommun, County) is 1 to 40 characters.");
        var regions = new JsonArray();
        if (o["regions"] is JsonArray given)
        {
            if (given.Count > MaxRegions) throw new CommandException($"A pack lists at most {MaxRegions} regions.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in given)
            {
                var region = ReadRegion(r);
                if (!seen.Add(region.Name)) throw new CommandException($"The region „{region.Name}” is listed twice.");
                regions.Add((JsonNode)region.ToJson());
            }
        }
        else if (o["regions"] is not null) throw new CommandException("The regions are a list: [{name, sk, vb0}].");
        return new JsonObject { ["regionLabel"] = label, ["regions"] = regions };
    }

    static double? Opt(JsonObject o, string key, double min, double max, string what)
    {
        if (o[key] is null) return null;
        return RulePacks.Num(o, new JsonObject(), key, min, max, what);
    }

    public static Region ReadRegion(JsonNode? n)
    {
        if (n is not JsonObject r) throw new CommandException("A region is {name, sk, vb0}.");
        var name = (string?)r["name"] is { } nm && nm.Trim() is { Length: > 0 and <= 80 } nt ? nt : throw new CommandException("A region's name is 1 to 80 characters.");
        string what(string x) => $"{name}: {x}";
        var sk = Opt(r, "sk", 0, 20, what("sk (kN/m²)"));
        var vb0 = Opt(r, "vb0", 0, 60, what("vb,0 (m/s)"));
        var qb = Opt(r, "qb", 0, 10, what("qb (kN/m²)"));
        if (vb0 != null && qb != null) throw new CommandException(what("give vb,0 or qb, not both."));
        if (sk == null && vb0 == null && qb == null) throw new CommandException(what("a region gives sk, vb,0 or qb."));
        var note = r["note"] is null ? null : (string?)r["note"] is { } t && t.Trim() is { Length: <= 200 } tt ? (tt.Length > 0 ? tt : null) : throw new CommandException(what("a note is at most 200 characters."));
        return new Region(name, sk, vb0, qb, note);
    }

    sealed record Read(string Label, IReadOnlyList<Region> Regions);
    static readonly Read Empty = new(DefaultRegionLabel, []);
    static readonly ConditionalWeakTable<JsonObject, Read> Cache = new();

    static Read Of(IModelView m)
    {
        if (RulePacks.RawSection(m, SectionKey) is not { } raw) return Empty;
        return Cache.GetValue(raw, r =>
        {
            try
            {
                var c = Instance.Check(r);
                return new Read((string)c["regionLabel"]!, c["regions"]!.AsArray().Select(ReadRegion).ToList());
            }
            catch (CommandException) { return Empty; }
        });
    }

    /// <summary>The regions of the project's pack (none for Lapin's).</summary>
    public static IReadOnlyList<Region> Regions(IModelView m) => Of(m).Regions;
    public static string RegionLabel(IModelView m) => Of(m).Label;

    public static Region? Find(IModelView m, string name) =>
        Of(m).Regions.FirstOrDefault(r => string.Equals(r.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
}
