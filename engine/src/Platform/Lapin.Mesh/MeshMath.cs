using System.Globalization;
using Lapin.Geometry;

namespace Lapin.Mesh;

/// <summary>
/// What every writer and importer of this assembly shares: numbers as text,
/// bounds of a flat xyz array, the turn between the model's frame (Z up) and
/// the Y-up formats' (glTF, Fragments), and the tolerances corners are welded
/// and faces grouped by.
/// </summary>
internal static class MeshMath
{
    /// <summary>Corners are the same point to 0.01 mm (five decimals of a metre): two copies of one body differ by float noise.</summary>
    public const int WeldDigits = 5;
    /// <summary>Triangles lie in one plane when their unit normals and distances agree to four decimals.</summary>
    public const int PlaneDigits = 4;

    /// <summary>A number as the text formats write it: round-trip, invariant, never −0.</summary>
    public static string F(float v) => (v == 0 ? 0f : v).ToString("R", CultureInfo.InvariantCulture);
    public static string F(double v) => (v == 0 ? 0d : v).ToString("R", CultureInfo.InvariantCulture);

    /// <summary>A value rounded to <paramref name="digits"/> decimals, −0 made 0 (so it hashes as 0).</summary>
    public static double Round(double v, int digits) { var r = Math.Round(v, digits); return r == 0 ? 0 : r; }

    /// <summary>The bounds of points given as x y z … (float.MaxValue / MinValue when there are none).</summary>
    public static (float[] Min, float[] Max) Bounds(float[] xyz)
    {
        float[] min = [float.MaxValue, float.MaxValue, float.MaxValue], max = [float.MinValue, float.MinValue, float.MinValue];
        for (var i = 0; i + 2 < xyz.Length; i += 3)
            for (var k = 0; k < 3; k++) { min[k] = Math.Min(min[k], xyz[i + k]); max[k] = Math.Max(max[k], xyz[i + k]); }
        return (min, max);
    }

    /// <summary>The model's frame (Z up) to a Y-up format's: (x, y, z) → (x, z, −y), a rotation, so windings stay.</summary>
    public static (double X, double Y, double Z) ToYUp(double x, double y, double z) => (x, z, y == 0 ? 0 : -y);

    /// <summary>A Y-up format's frame to the model's (Z up): (x, y, z) → (x, −z, y).</summary>
    public static Vec3 FromYUp(double x, double y, double z) => new(x, z == 0 ? 0 : -z, y);

    /// <summary>
    /// Corners welded as they come: a point within <see cref="WeldDigits"/> of one already added is that one,
    /// so a mesh written a vertex per face corner (an STL, a flat-shaded glTF) closes into one surface.
    /// </summary>
    public sealed class Welder(List<Vec3> vertices)
    {
        readonly Dictionary<(double, double, double), int> _index = [];

        public int Add(Vec3 p)
        {
            var key = (Round(p.X, WeldDigits), Round(p.Y, WeldDigits), Round(p.Z, WeldDigits));
            if (!_index.TryGetValue(key, out var i)) { i = vertices.Count; vertices.Add(p); _index[key] = i; }
            return i;
        }
    }
}
