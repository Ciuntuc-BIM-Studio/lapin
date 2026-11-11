using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform.Analysis;

namespace Lapin.Platform.ProfileLibraries;

/// <summary>One closed body of a profile: its outside anticlockwise and its holes clockwise, about the profile's box's centre (metres).</summary>
public sealed record MemberOutline(IReadOnlyList<Vec2> Outer, IReadOnlyList<IReadOnlyList<Vec2>> Holes)
{
    /// <summary>
    /// The real shape of a section a curtain wall's or a door's or window's member keeps (decision 417; a section's JSON, as
    /// <see cref="SectionProfile.SectionJson"/> gives it): one body for each outside (a composite profile has several); empty for
    /// none or one that cannot be read, the member then a plain box. Fillets by the chord rule.
    /// </summary>
    public static IReadOnlyList<MemberOutline> Of(JsonNode? section)
    {
        if (section is not JsonObject o) return [];
        var kind = (string?)o["kind"] ?? "rectangle";
        var shape = new SectionShape(kind, Json.Number(o["width"]) ?? 0, Json.Number(o["depth"]) ?? 0, Json.Number(o["web"]) ?? 0, Json.Number(o["flange"]) ?? 0, Json.Number(o["lip"]) ?? 0, (string?)o["name"])
        {
            Root = Json.Number(o["r1"]), Toe = Json.Number(o["r2"]), Slope = Json.Number(o["slope"]) ?? 0, Standard = (string?)o["standard"], Family = (string?)o["family"],
            Rings = (o["rings"] as JsonArray)?.OfType<JsonArray>().Select(r => (IReadOnlyList<Vec2>)r.OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0)).ToList()).Where(r => r.Count >= 3).ToList(),
        };
        if (SectionProperties.RingsOf(shape, chords: true) is not { Count: > 0 } rings) return [];
        var outsides = rings.Where(r => Polygon2.SignedArea(r) > 0).ToList();
        var holes = rings.Where(r => Polygon2.SignedArea(r) < 0).ToList();
        return outsides.Select(a => new MemberOutline(a, holes.Where(h => Polygon2.Contains(a, h[0])).Select(h => (IReadOnlyList<Vec2>)h).ToList())).ToList();
    }
}
