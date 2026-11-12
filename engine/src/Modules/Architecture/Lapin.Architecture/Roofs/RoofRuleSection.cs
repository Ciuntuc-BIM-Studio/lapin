using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// The roofs' rules in a rule pack (docs/plan-user-extensibility.md §5.4): each covering's least pitch (°), below which the roof
/// warns that it will not shed water; Lapin's own the catalogue's (indicative). A covering the pack names that the catalogue does
/// not have is kept for a later one.
/// </summary>
public sealed class RoofRuleSection : IRuleSection
{
    public const string SectionKey = "roofs";
    public string Key => SectionKey;
    public string Label => "Roofs";

    public JsonObject Default => new() { ["minPitch"] = new JsonObject(RoofCatalog.Coverings.Select(c => KeyValuePair.Create(c.Id, (JsonNode?)c.MinPitch))) };

    public JsonObject Check(JsonNode? section)
    {
        var given = section?["minPitch"] as JsonObject ?? new JsonObject();
        var o = new JsonObject();
        foreach (var c in RoofCatalog.Coverings) o[c.Id] = RulePacks.Num(given, new JsonObject { [c.Id] = c.MinPitch }, c.Id, 0, 89, $"{c.Id}'s least pitch");
        foreach (var (k, v) in given) if (o[k] == null) o[k] = RulePacks.Num(given, new JsonObject { [k] = 0 }, k, 0, 89, $"{k}'s least pitch");
        return new JsonObject { ["minPitch"] = o };
    }

    /// <summary>A covering's least pitch (°): the project's pack's, else the catalogue's.</summary>
    public static double MinPitch(IModelView m, RoofCatalog.Covering c) =>
        Json.Number(RulePacks.RawSection(m, SectionKey)?["minPitch"]?[c.Id]) is { } p && p >= 0 && p < 90 ? p : c.MinPitch;
}
