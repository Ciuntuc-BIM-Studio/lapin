using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// One opening in a wall as a drawing dimensions it (report #92, decision 338): its box's corners in the
/// world (the opening's width along the wall, through the wall's whole thickness, from its sill to its head),
/// the jambs at the sill (<see cref="Left"/>, <see cref="Right"/>, what says which way its width runs in a
/// view), its width and height, and its sill above its storey's level (a door's is 0).
/// </summary>
public sealed record WallOpening(string OpeningId, string? FillId, string WallId, string? StoreyId, IReadOnlyList<Vec3> Corners, Vec3 Left, Vec3 Right, double Width, double Height, double Sill);

/// <summary>The openings in a module's walls (doors, windows, bare openings), read from the model each time a drawing asks.</summary>
public interface IWallOpenings
{
    IEnumerable<WallOpening> Openings(IModelView m);
}
