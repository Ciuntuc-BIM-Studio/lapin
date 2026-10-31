namespace Lapin.Geometry;

/// <summary>
/// How finely a curve is made into straight pieces (docs/plan-display-precision.md):
/// by how far a chord strays from the arc, not by a fixed angle, so a 10 m curved
/// wall is as true as a 50 mm pipe and the pipe is not cut into 64 sides. A step
/// turns at most <see cref="MaxStep"/> (under <see cref="Tessellator.CreaseDeg"/>,
/// so a round surface draws no line between its facets) and at least
/// <see cref="MinStep"/> (a road's 500 m radius stops there). The model's
/// geometry, its drawings, exports and quantities all use this one rule; the
/// screen's coarser levels are made from it afterwards (<see cref="MeshSimplify"/>).
/// </summary>
public static class Chords
{
    /// <summary>The most a chord strays from its arc (m).</summary>
    public const double Tolerance = 0.001;

    /// <summary>The largest step (15°: a whole circle has at least 24 sides).</summary>
    public const double MaxStep = Math.PI / 12;

    /// <summary>The smallest step (0.5°: reached past a radius of about 105 m).</summary>
    public const double MinStep = Math.PI / 360;

    /// <summary>The angle one piece of an arc of <paramref name="radius"/> may turn through (radians).</summary>
    public static double Step(double radius, double maxStep = MaxStep)
    {
        if (!(radius > Tolerance * 2)) return maxStep;
        return Math.Clamp(2 * Math.Acos(1 - Tolerance / radius), MinStep, maxStep);
    }

    /// <summary>
    /// The pieces an arc of <paramref name="radius"/> turning <paramref name="sweep"/> radians is made of (at least
    /// <paramref name="min"/>); <paramref name="maxStep"/> larger than <see cref="MaxStep"/> lets thin bars stay light.
    /// </summary>
    public static int Count(double radius, double sweep, int min = 1, double maxStep = MaxStep) =>
        Math.Max(min, (int)Math.Ceiling(Math.Abs(sweep) / Step(radius, maxStep) - 1e-9));

    /// <summary>The sides of a whole circle of <paramref name="radius"/> (at least 24).</summary>
    public static int Circle(double radius) => Count(radius, 2 * Math.PI, 3);
}
