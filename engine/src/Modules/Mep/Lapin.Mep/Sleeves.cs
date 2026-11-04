using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// The sleeves a gas or fuel oil pipe goes through walls in (decision 285):
/// one where a leg of the run's centreline crosses a straight wall at a
/// height within it, as long as the wall is thick along the pipe and 10 mm
/// proud of each face, its bore the pipe's and 10 mm round it. Generated
/// with the run (its parts, keyed by the wall), so the run reads the walls it
/// crosses and is built again when one moves; counted in the gas list.
/// </summary>
public static class Sleeves
{
    /// <summary>A sleeve: the wall it goes through, its two ends, its outer diameter.</summary>
    public sealed record Sleeve(string WallId, Vec3 A, Vec3 B, double Diameter)
    {
        public double Length => (B - A).Length;
    }

    /// <summary>How far a sleeve stands out of each face of the wall, and the gap round the pipe.</summary>
    public const double Proud = 0.01, Gap = 0.01;

    /// <summary>The sleeves of a run whose service takes them (gas, fuel oil); none for the others.</summary>
    public static List<Sleeve> Of(IModelView m, Entity run, Runs.Setting s, IReadOnlyList<Vec3> line)
    {
        var o = new List<Sleeve>();
        if (!Services.IsFuel(s.Service) || line.Count < 2) return o;
        var diameter = s.Size + 2 * Gap;
        foreach (var wall in m.OfClass(IfcClass.Wall))
        {
            if (PathCurve.Of(wall) is { IsArc: true } || Walls.Frame(wall, m) is not { } f) continue;
            var axis = f.End - f.Start;
            var length = axis.Length;
            if (length < 1e-6) continue;
            var along = axis * (1 / length);
            var normal = new Vec2(-along.Y, along.X);
            var z0 = (Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");
            var z1 = z0 + (wall.NumOrNull("height") ?? Walls.DefaultHeight);
            for (var i = 1; i < line.Count; i++)
            {
                Vec2 a = line[i - 1].Xy, b = line[i].Xy;
                var leg = b - a;
                var legLength = leg.Length;
                if (legLength < 1e-6) continue;
                var dir = leg * (1 / legLength);
                var across = Vec2.Dot(dir, normal);
                // Along the wall, or nearly: no crossing to sleeve.
                if (Math.Abs(across) < 0.05) continue;
                // Where the leg meets the wall's axis, and whether that is within both.
                var t = Vec2.Dot(f.Start - a, normal) / Vec2.Dot(leg, normal);
                if (t < 0 || t > 1) continue;
                var hit = a + leg * t;
                var u = Vec2.Dot(hit - f.Start, along);
                if (u < -1e-6 || u > length + 1e-6) continue;
                var z = line[i - 1].Z + (line[i].Z - line[i - 1].Z) * t;
                if (z < z0 || z > z1) continue;
                // The wall's body across its axis (its layers may sit off it): from YMin to YMax along the normal.
                var t0 = f.YMin / across - Proud / Math.Abs(across);
                var t1 = f.YMax / across + Proud / Math.Abs(across);
                if (t0 > t1) (t0, t1) = (t1, t0);
                var dz = (line[i].Z - line[i - 1].Z) / legLength;
                Vec3 P(double d) => new(hit.X + dir.X * d, hit.Y + dir.Y * d, z + dz * d);
                o.Add(new Sleeve(wall.Id, P(t0), P(t1), diameter));
            }
        }
        return o;
    }
}
