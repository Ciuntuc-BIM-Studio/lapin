using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Civil;

/// <summary>
/// Lists of points as the civil elements keep them and commands give them
/// (<c>[[x, y], …]</c> or <c>[[x, y, z], …]</c>), read one way: a point whose
/// coordinates are not finite numbers is left out, never read as 0 (a parcel
/// corner at the origin, a survey point at ±0.00).
/// </summary>
public static class PointLists
{
    static double At(JsonArray p, int i) => i < p.Count && Json.Number(p[i]) is { } d ? d : double.NaN;

    /// <summary>Points in plan.</summary>
    public static List<Vec2> Xy(JsonNode? n) =>
        [.. ((n as JsonArray)?.OfType<JsonArray>() ?? []).Select(p => new Vec2(At(p, 0), At(p, 1))).Where(q => double.IsFinite(q.X) && double.IsFinite(q.Y))];

    /// <summary>Points in space; with <paramref name="needZ"/> false, a point given in plan only stands at 0.</summary>
    public static List<Vec3> Xyz(JsonNode? n, bool needZ = true) =>
        [.. ((n as JsonArray)?.OfType<JsonArray>() ?? []).Select(p => new Vec3(At(p, 0), At(p, 1), !needZ && p.Count < 3 ? 0 : At(p, 2)))
            .Where(q => double.IsFinite(q.X) && double.IsFinite(q.Y) && double.IsFinite(q.Z))];
}
