namespace Lapin.Geometry;

/// <summary>
/// Kernel tolerances, in metres. Models are authored to about a millimetre, so
/// these sit orders of magnitude below what a user can express and far above
/// double noise at site scale (kilometres).
/// </summary>
public static class Tolerance
{
    /// <summary>Points closer than this are the same vertex: 1 micron.</summary>
    public const double Distance = 1e-6;

    /// <summary>Max deviation of a loop vertex from its face plane: 10 microns.</summary>
    public const double Planar = 1e-5;

    /// <summary>Loops below this area (m²) are degenerate and dropped.</summary>
    public const double Area = 1e-12;
}
