using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// Lists of points as the civil elements keep them and commands give them
/// (<c>[[x, y], …]</c> or <c>[[x, y, z], …]</c>), read one way: a point whose
/// coordinates are not finite numbers is left out, never read as 0 (a parcel
/// corner at the origin, a survey point at ±0.00).
/// </summary>
public static class PointLists
{
    /// <summary>Points in plan.</summary>
    public static List<Vec2> Xy(JsonNode? n) => Args.StoredPoints(n);

    /// <summary>Points in space; with <paramref name="needZ"/> false, a point given in plan only stands at 0.</summary>
    public static List<Vec3> Xyz(JsonNode? n, bool needZ = true) => Args.StoredPoints3(n, needZ);
}
