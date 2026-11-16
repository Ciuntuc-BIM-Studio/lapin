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

    /// <summary>
    /// A whole circle about <paramref name="centre"/> as a counter-clockwise ring of <see cref="Circle"/> sides, its first
    /// corner on +x. Inscribed (corners on the circle), or <paramref name="circumscribed"/>: sides tangent to it, so the ring
    /// holds the whole circle (an opening that must clear a round pipe or duct).
    /// </summary>
    public static List<Vec2> Ring(Vec2 centre, double radius, bool circumscribed = false)
    {
        var n = Circle(radius);
        var r = circumscribed ? radius / Math.Cos(Math.PI / n) : radius;
        var o = new List<Vec2>(n);
        for (var k = 0; k < n; k++)
        {
            var a = 2 * Math.PI * k / n;
            o.Add(new Vec2(centre.X + r * Math.Cos(a), centre.Y + r * Math.Sin(a)));
        }
        return o;
    }

    /// <summary>
    /// The points of an arc about <paramref name="centre"/> from angle <paramref name="from"/> turning <paramref name="sweep"/>
    /// (radians, anticlockwise when positive), both ends included, in <see cref="Count"/> pieces (at least <paramref name="min"/>).
    /// </summary>
    public static List<Vec2> Arc(Vec2 centre, double radius, double from, double sweep, int min = 1)
    {
        var n = Count(radius, sweep, min);
        var o = new List<Vec2>(n + 1);
        for (var k = 0; k <= n; k++)
        {
            var a = from + sweep * k / n;
            o.Add(new Vec2(centre.X + radius * Math.Cos(a), centre.Y + radius * Math.Sin(a)));
        }
        return o;
    }
}
