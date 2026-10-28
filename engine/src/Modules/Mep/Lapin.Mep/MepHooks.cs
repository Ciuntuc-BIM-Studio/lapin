using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Mep;

/// <summary>
/// How a material is laid: its legs on allowed directions (copper at 45°
/// steps, cable horizontal and vertical), the largest turn of one corner
/// (PVC drainage: 45°), its bend radius. The open module knows the materials
/// by name (<see cref="RunMaterials"/>) and lays a run as it is drawn; the
/// rules come with mep-pro, through <see cref="MepHooks.Laying"/>.
/// </summary>
public interface ILayingRules
{
    /// <summary>The points laid by the material's rules; a material without rules keeps them.</summary>
    List<Vec2> Lay(string service, string? material, IReadOnlyList<Vec2> points);

    /// <summary>Whether the legs between these points break the material's rules (an elbow is needed between two ends that no longer line up).</summary>
    bool Breaks(string service, string? material, IReadOnlyList<Vec2> points);

    /// <summary>The material's bend radius as a multiple of the size; null when the material has none of its own.</summary>
    double? BendRadius(string service, string? material);
}

/// <summary>A route between two points of a storey in plan, around its walls (mep-pro's router); null when none is found.</summary>
public interface IRouter
{
    IReadOnlyList<Vec2>? Route(IModelView model, string storeyId, Vec2 from, Vec2 to);
}

/// <summary>
/// What another module adds over the open MEP, set when it registers: the
/// laying rules by material and the router round the walls. Without them a
/// run keeps the points it is drawn with and a new endpoint joins its source
/// by an L.
/// </summary>
public static class MepHooks
{
    public static ILayingRules? Laying { get; set; }
    public static IRouter? Router { get; set; }
}
