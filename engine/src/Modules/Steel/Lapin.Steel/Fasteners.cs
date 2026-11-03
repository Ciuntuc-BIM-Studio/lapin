using Lapin.Geometry;

namespace Lapin.Steel;

/// <summary>
/// The bodies of a connection's fasteners (decision 273), simplified as a detailer's model draws them: a bolt a hexagon
/// head, a round shank, a washer and a hexagon nut; an anchor a round rod, straight or bent into a hook at its foot, with
/// its washer and nut over the base plate. Each one solid, made of rings stacked along its axis (<see cref="Stack"/>): a
/// ring is a section of the body across it, and two rings at one station make the step between them.
/// </summary>
public static class Fasteners
{
    /// <summary>The points a ring has: a multiple of 6, so a hexagon's corners are among them.</summary>
    public const int Sides = 12;

    /// <summary>Head height, nut height, washer thickness and diameter, and how far the bolt stands out of its nut, as fractions of its diameter.</summary>
    const double Head = 0.65, Nut = 0.8, WasherT = 0.15, WasherD = 1.85, Protrude = 0.3, AcrossFlats = 1.5;

    /// <summary>A bolt's length to order: its grip, a washer, a nut and its end standing out, rounded up to 5 mm.</summary>
    public static double Length(double d, double grip) => Math.Ceiling((grip + d * (WasherT + Nut + Protrude)) * 200 - 1e-9) / 200;

    /// <summary>The radius at angle <paramref name="deg"/> of a ring: a circle's, or a hexagon's of that width across its flats.</summary>
    static double Radius(bool hex, double size, double deg) =>
        hex ? size / 2 / Math.Cos(((deg % 60 + 60) % 60 - 30) * Math.PI / 180) : size / 2;

    /// <summary>A ring about <paramref name="c"/> across <paramref name="dir"/>: <see cref="Sides"/> points counter-clockwise about it.</summary>
    static Vec3[] Ring(Vec3 c, Vec3 e1, Vec3 e2, bool hex, double size)
    {
        var o = new Vec3[Sides];
        for (var k = 0; k < Sides; k++)
        {
            var deg = 360.0 * k / Sides;
            var r = Radius(hex, size, deg);
            var a = deg * Math.PI / 180;
            o[k] = c + e1 * (r * Math.Cos(a)) + e2 * (r * Math.Sin(a));
        }
        return o;
    }

    /// <summary>Two unit vectors across <paramref name="dir"/>, the second <c>dir × first</c>.</summary>
    public static (Vec3 E1, Vec3 E2) Across(Vec3 dir)
    {
        var u = dir.Normalized();
        var e1 = Math.Abs(u.Z) < 0.9 ? Vec3.Cross(new Vec3(0, 0, 1), u).Normalized() : Vec3.Cross(new Vec3(1, 0, 0), u).Normalized();
        return (e1, Vec3.Cross(u, e1));
    }

    /// <summary>
    /// A solid of rings stacked along a path: side faces between each ring and the next (a step where two lie at one
    /// station), the first and the last capped. Every ring has the same count of points, counter-clockwise about the way
    /// the stack runs.
    /// </summary>
    public static Solid? Stack(IReadOnlyList<Vec3[]> rings, string tag)
    {
        if (rings.Count < 2) return null;
        var n = rings[0].Length;
        var verts = new List<Vec3>();
        var idx = new List<int[]>();
        foreach (var r in rings) idx.Add(r.Select(p => { verts.Add(p); return verts.Count - 1; }).ToArray());
        var faces = new List<FaceInput> { new(idx[0].Reverse().ToArray(), null, null, "start"), new(idx[^1], null, null, "end") };
        for (var k = 0; k + 1 < rings.Count; k++)
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                faces.Add(new FaceInput([idx[k][i], idx[k][j], idx[k + 1][j], idx[k + 1][i]], null, null, "side"));
            }
        return SolidBuilder.Make(verts, faces, tag);
    }

    /// <summary>
    /// A bolt whose head bears on <paramref name="headFace"/>, its shank running along <paramref name="dir"/> through the
    /// grip, then a washer and a nut, its end standing out of the nut.
    /// </summary>
    public static Solid? Bolt(Vec3 headFace, Vec3 dir, double d, double grip)
    {
        var u = dir.Normalized();
        var (e1, e2) = Across(u);
        Vec3[] R(double t, bool hex, double size) => Ring(headFace + u * t, e1, e2, hex, size);
        var (s, w, m) = (AcrossFlats * d, WasherD * d, grip + WasherT * d);
        var end = Length(d, grip);
        return Stack(
        [
            R(-Head * d, true, s), R(0, true, s), R(0, false, d), R(grip, false, d), R(grip, false, w), R(m, false, w),
            R(m, true, s), R(m + Nut * d, true, s), R(m + Nut * d, false, d), R(Math.Max(end, m + Nut * d + 0.002), false, d),
        ], "bolt");
    }

    /// <summary>
    /// An anchor rod through a base plate whose top is <paramref name="plateTop"/>: its nut and washer on the plate, the rod
    /// down to <paramref name="bottom"/> and, hooked, bent there and running <paramref name="hook"/> along
    /// <paramref name="hookDir"/> (level). The rod's length (to order) with it.
    /// </summary>
    public static (Solid? Solid, double Length) Anchor(Vec2 at, double plateTop, double bottom, double d, double hook, Vec2 hookDir)
    {
        var down = new Vec3(0, 0, -1);
        var (e1, e2) = Across(down);
        var top = plateTop + d * (WasherT + Nut + Protrude);
        Vec3[] R(double z, bool hex, double size) => Ring(new Vec3(at.X, at.Y, z), e1, e2, hex, size);
        var (s, w) = (AcrossFlats * d, WasherD * d);
        var nutBottom = plateTop + WasherT * d;
        var rings = new List<Vec3[]>
        {
            R(top, false, d), R(nutBottom + Nut * d, false, d), R(nutBottom + Nut * d, true, s), R(nutBottom, true, s),
            R(nutBottom, false, w), R(plateTop, false, w), R(plateTop, false, d),
        };
        var length = top - bottom;
        if (hook > 0 && hookDir.Length > 1e-9)
        {
            // The bend mitred: the rod's circle at its foot carried down onto the plane halving the bend, then along the hook.
            var h = new Vec3(hookDir.X, hookDir.Y, 0).Normalized();
            var corner = new Vec3(at.X, at.Y, bottom + d / 2);
            var bis = (down + h).Normalized();
            var foot = R(corner.Z, false, d).Select(p => p + down * (-Vec3.Dot(bis, p - corner) / Vec3.Dot(bis, down))).ToArray();
            var tip = corner + h * hook;
            rings.Add(foot);
            rings.Add(foot.Select(p => p + h * Vec3.Dot(h, tip - p)).ToArray());
            length = top - corner.Z + hook;
        }
        else rings.Add(R(bottom, false, d));
        return (Stack(rings, "anchor"), length);
    }
}
