using Lapin.Geometry;

namespace Lapin.Civil;

/// <summary>
/// A plant as an architectural model's (decision 818, the vegetation's maquette style): neutral, faceted, flat shaded, so it
/// reads as a massing model's tree. Each body is Lapin's own, made from the plant's seed so neighbours differ and the same
/// plant always comes out the same:
/// <list type="bullet">
/// <item>a broadleaf a rounded cluster of 4–7 faceted blobs (icosahedra, their corners drawn in by the seed) on a trunk;
/// a fruit tree the same, wider and lower, on a shorter trunk;</item>
/// <item>a fir or spruce 3–4 stacked six-sided cones on a short trunk;</item>
/// <item>a pine 3–5 flattened blobs high on a tall bare trunk;</item>
/// <item>a shrub a low cluster of 3–5 blobs, no trunk;</item>
/// <item>a hedge plant an eight-sided block bevelled at its top;</item>
/// <item>grass a thin six-sided mat.</item>
/// </list>
/// Every piece is a closed polyhedron (each edge shared by two faces) inside the plant's height and crown; the faceting is
/// the style's own, so the blobs are explicit polyhedra and never chords of a curve (<see cref="Chords"/> rules curves).
/// The pieces overlap and are not joined (a boolean per plant would cost a site of thousands).
/// </summary>
public static class PlantMaquette
{
    /// <summary>The crowns' colour: a warm off-white, a model's card; the trunks a little darker. Both read in the light and dark themes.</summary>
    public const string CrownColor = "#e6e3dc", TrunkColor = "#aea698";

    /// <summary>The most faces a plant of each family has (the tests hold every body to it).</summary>
    public static int Budget(string kind) => kind switch
    {
        "broadleaf" or "fruit" => 7 * 20 + Trunk,
        "fir" => 4 * 7 + Trunk,
        "pine" => 5 * 20 + Trunk,
        "shrub" => 5 * 20,
        "hedge" => 18,
        _ => 8,
    };

    /// <summary>
    /// The body a species takes, as its coloured one does: a tree by its family (fir, pine, fruit; any other a broadleaf's), a
    /// bush a hedge plant's block or a shrub's cluster, grass a mat ("ground").
    /// </summary>
    public static string Kind(Species sp) => sp.Type switch
    {
        "grass" => "ground",
        "bush" => sp.DrawnAs == "hedge" ? "hedge" : "shrub",
        _ => sp.DrawnAs is "fir" or "pine" or "fruit" ? sp.DrawnAs : "broadleaf",
    };

    /// <summary>A trunk's faces: a five-sided frustum.</summary>
    const int Trunk = 7;

    /// <summary>A plant's crown pieces and its trunk, from <paramref name="z"/> up <paramref name="h"/>, <paramref name="r"/> the crown's radius.</summary>
    public static (List<Solid> Crown, List<Solid> Trunk) Body(Species sp, Vec2 at, double z, double h, double r, int seed)
    {
        var rng = new SiteGeometry.Random(Mix(seed));
        var crown = new List<Solid>();
        var trunk = new List<Solid>();
        var tr = Math.Max(0.06, r * 0.08);
        switch (Kind(sp))
        {
            case "fir":
            {
                var c0 = h * 0.1;
                trunk.Add(Frustum(at, z, tr, tr * 0.7, c0 + h * 0.1, 5, rng.Next()));
                // 3 or 4 tiers, each a six-sided cone narrower than the one under it, overlapping it; the last to the top.
                var n = rng.Next() < 0.5 ? 3 : 4;
                var span = h - c0;
                var step = span * 0.66 / (n - 1 + 0.5);
                for (var i = 0; i < n; i++)
                {
                    var bottom = c0 + i * step;
                    var top = i == n - 1 ? h : Math.Min(h, bottom + step * 1.55);
                    var radius = r * (1 - 0.6 * i / n) * (0.9 + 0.1 * rng.Next());
                    crown.Add(Cone(at, z + bottom, radius, top - bottom, 6, rng.Next()));
                }
                break;
            }
            case "pine":
            {
                // A tall bare trunk; the crown a flat clump of blobs in the top third.
                var c0 = h * 0.6;
                trunk.Add(Frustum(at, z, tr, tr * 0.7, c0 + (h - c0) * 0.3, 5, rng.Next()));
                Cluster(crown, at, z + c0, h - c0, r, 1 + (int)(rng.Next() * 3), rng, ring: 0.48, side: 0.52, height: 0.62, centre: 0.6);
                break;
            }
            case "shrub":
                Cluster(crown, at, z, h, r, 1 + (int)(rng.Next() * 3), rng, ring: 0.45, side: 0.55, height: 0.8, centre: 1.0);
                break;
            case "hedge":
                crown.Add(Block(at, z, r, h, 0.55 + 0.15 * rng.Next(), rng.Next() * Math.PI / 4));
                break;
            case "ground":
                crown.Add(Frustum(at, z, r, r, Math.Max(0.02, h), 6, rng.Next()));
                break;
            default:
            {
                // A rounded cluster: a fruit tree wider and lower, its crown from a quarter of its height; a broadleaf from a third.
                var fruit = sp.DrawnAs == "fruit";
                var c0 = h * (fruit ? 0.25 : 0.35);
                trunk.Add(Frustum(at, z, tr, tr * 0.7, c0 + (h - c0) * 0.3, 5, rng.Next()));
                Cluster(crown, at, z + c0, h - c0, r, 2 + (int)(rng.Next() * 4), rng, ring: fruit ? 0.5 : 0.42, side: fruit ? 0.5 : 0.56, height: fruit ? 0.62 : 0.74, centre: fruit ? 0.85 : 1.0);
                break;
            }
        }
        return (crown, trunk);
    }

    /// <summary>A seed spread over the generator's bits: the plants of a zone come one seed apart.</summary>
    static int Mix(int seed)
    {
        unchecked
        {
            var h = (uint)seed * 2654435761u ^ 0x5bd1e995u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return (int)(h == 0 ? 1 : h);
        }
    }

    /// <summary>
    /// A cluster from <paramref name="z"/> up <paramref name="span"/>: a central blob, one over it, and <paramref name="around"/>
    /// round them, each an icosahedron squashed to its own sizes; all inside the crown's radius and its height.
    /// </summary>
    /// <param name="ring">How far from the axis the blobs round it stand, of the crown's radius.</param>
    /// <param name="side">Their horizontal radius, of the crown's (ring + side ≤ 1 keeps them in the crown).</param>
    /// <param name="height">Their vertical radius, of the crown's half height.</param>
    /// <param name="centre">The central blob's size over theirs.</param>
    static void Cluster(List<Solid> o, Vec2 at, double z, double span, double r, int around, SiteGeometry.Random rng, double ring, double side, double height, double centre)
    {
        var a = span / 2;
        var zc = z + a;
        // The central blob, as wide as the ring leaves, and as high as the crown allows.
        var cr = Math.Min(1, (ring + side) * 0.82 * centre);
        o.Add(Blob(new Vec3(at.X, at.Y, zc - a * 0.1), r * cr, a * 0.88, rng));
        // One over it, rounding the top.
        o.Add(Blob(new Vec3(at.X, at.Y, zc + a * 0.42), r * side * 0.95, a * 0.56, rng));
        var turn = rng.Next() * 2 * Math.PI;
        for (var k = 0; k < around; k++)
        {
            var angle = turn + 2 * Math.PI * (k + 0.25 * (rng.Next() - 0.5)) / around;
            var d = r * ring * (0.85 + 0.15 * rng.Next());
            var vz = a * height * (0.85 + 0.15 * rng.Next());
            // A little up or down (most a little under the middle, the crown fuller below), never out of the crown's height.
            var cz = Math.Clamp(zc + a * (1 - height) * (2 * rng.Next() - 1) - a * 0.12, z + vz, z + span - vz);
            o.Add(Blob(new Vec3(at.X + d * Math.Cos(angle), at.Y + d * Math.Sin(angle), cz), r * side * (0.85 + 0.15 * rng.Next()), vz, rng));
        }
    }

    static readonly double Phi = (1 + Math.Sqrt(5)) / 2;

    /// <summary>The icosahedron's twelve corners on the unit sphere, and its twenty triangles.</summary>
    static readonly Vec3[] Ico = [.. new (double, double, double)[]
    {
        (-1, Phi, 0), (1, Phi, 0), (-1, -Phi, 0), (1, -Phi, 0), (0, -1, Phi), (0, 1, Phi),
        (0, -1, -Phi), (0, 1, -Phi), (Phi, 0, -1), (Phi, 0, 1), (-Phi, 0, -1), (-Phi, 0, 1),
    }.Select(p => new Vec3(p.Item1, p.Item2, p.Item3).Normalized())];

    static readonly int[][] IcoFaces =
    [
        [0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11], [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6], [7, 1, 8],
        [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9], [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1],
    ];

    /// <summary>
    /// A faceted blob at <paramref name="c"/>: an icosahedron turned by the seed, <paramref name="rx"/> wide and <paramref name="rz"/>
    /// high, each corner drawn in towards the centre by up to a sixth (never out, so the blob stays in its box).
    /// </summary>
    static Solid Blob(Vec3 c, double rx, double rz, SiteGeometry.Random rng)
    {
        var spin = rng.Next() * 2 * Math.PI;
        var tilt = (rng.Next() - 0.5) * 0.8;
        var (cs, sn, ct, st) = (Math.Cos(spin), Math.Sin(spin), Math.Cos(tilt), Math.Sin(tilt));
        var v = new Vec3[Ico.Length];
        for (var i = 0; i < Ico.Length; i++)
        {
            var p = Ico[i];
            // Tilted about x, then spun about z: the unit sphere stays the unit sphere.
            var y = p.Y * ct - p.Z * st;
            var zz = p.Y * st + p.Z * ct;
            var x = p.X * cs - y * sn;
            y = p.X * sn + y * cs;
            var k = 0.84 + 0.16 * rng.Next();
            v[i] = new Vec3(c.X + x * rx * k, c.Y + y * rx * k, c.Z + zz * rz * k);
        }
        return Closed(v, IcoFaces, c, "crown");
    }

    /// <summary>A cone of <paramref name="sides"/> faces from a ring at <paramref name="z"/> to its apex, turned by <paramref name="turn"/> (of a side).</summary>
    static Solid Cone(Vec2 at, double z, double r, double height, int sides, double turn)
    {
        var v = new Vec3[sides + 1];
        for (var k = 0; k < sides; k++)
        {
            var a = 2 * Math.PI * (k + turn) / sides;
            v[k] = new Vec3(at.X + r * Math.Cos(a), at.Y + r * Math.Sin(a), z);
        }
        v[sides] = new Vec3(at.X, at.Y, z + height);
        List<int[]> faces = [[.. Enumerable.Range(0, sides).Reverse()]];
        for (var k = 0; k < sides; k++) faces.Add([k, (k + 1) % sides, sides]);
        return Closed(v, faces, new Vec3(at.X, at.Y, z + height / 3), "crown");
    }

    /// <summary>A frustum of <paramref name="sides"/> faces, <paramref name="r0"/> at its foot and <paramref name="r1"/> at its top (a trunk, a mat).</summary>
    static Solid Frustum(Vec2 at, double z, double r0, double r1, double height, int sides, double turn)
    {
        var v = new Vec3[2 * sides];
        for (var k = 0; k < sides; k++)
        {
            var a = 2 * Math.PI * (k + turn) / sides;
            v[k] = new Vec3(at.X + r0 * Math.Cos(a), at.Y + r0 * Math.Sin(a), z);
            v[sides + k] = new Vec3(at.X + r1 * Math.Cos(a), at.Y + r1 * Math.Sin(a), z + height);
        }
        List<int[]> faces = [[.. Enumerable.Range(0, sides).Reverse()], [.. Enumerable.Range(sides, sides)]];
        for (var k = 0; k < sides; k++) faces.Add([k, (k + 1) % sides, sides + (k + 1) % sides, sides + k]);
        return Closed(v, faces, new Vec3(at.X, at.Y, z + height / 2), "trunk");
    }

    /// <summary>A hedge plant: an eight-sided block <paramref name="r"/> round, bevelled over its top sixth to <paramref name="top"/> of its width.</summary>
    static Solid Block(Vec2 at, double z, double r, double h, double top, double turn)
    {
        const int sides = 8;
        var v = new Vec3[3 * sides];
        var shoulder = h * (1 - Math.Min(0.3, r * 0.6 / Math.Max(h, 1e-6)) - 0.1);
        for (var k = 0; k < sides; k++)
        {
            var a = turn + 2 * Math.PI * (k + 0.5) / sides;
            var (c, s) = (Math.Cos(a), Math.Sin(a));
            v[k] = new Vec3(at.X + r * c, at.Y + r * s, z);
            v[sides + k] = new Vec3(at.X + r * c, at.Y + r * s, z + shoulder);
            v[2 * sides + k] = new Vec3(at.X + r * top * c, at.Y + r * top * s, z + h);
        }
        List<int[]> faces = [[.. Enumerable.Range(0, sides).Reverse()], [.. Enumerable.Range(2 * sides, sides)]];
        for (var k = 0; k < sides; k++)
        {
            var n = (k + 1) % sides;
            faces.Add([k, n, sides + n, sides + k]);
            faces.Add([sides + k, sides + n, 2 * sides + n, 2 * sides + k]);
        }
        return Closed(v, faces, new Vec3(at.X, at.Y, z + h / 2), "crown");
    }

    /// <summary>A closed polyhedron of planar faces, each turned to face away from <paramref name="inside"/>, its normal its own (flat shaded).</summary>
    static Solid Closed(Vec3[] v, IReadOnlyList<int[]> loops, Vec3 inside, string tag)
    {
        var faces = new Face[loops.Count];
        for (var i = 0; i < loops.Count; i++)
        {
            var loop = loops[i];
            // Newell's normal: right for any planar loop, whichever corner is sharp.
            var n = new Vec3(0, 0, 0);
            var mid = new Vec3(0, 0, 0);
            for (var k = 0; k < loop.Length; k++)
            {
                var (p, q) = (v[loop[k]], v[loop[(k + 1) % loop.Length]]);
                n = new Vec3(n.X + (p.Y - q.Y) * (p.Z + q.Z), n.Y + (p.Z - q.Z) * (p.X + q.X), n.Z + (p.X - q.X) * (p.Y + q.Y));
                mid = new Vec3(mid.X + p.X, mid.Y + p.Y, mid.Z + p.Z);
            }
            mid = new Vec3(mid.X / loop.Length, mid.Y / loop.Length, mid.Z / loop.Length);
            if (Vec3.Dot(n, mid - inside) < 0) { loop = [.. loop.Reverse()]; n = new Vec3(-n.X, -n.Y, -n.Z); }
            faces[i] = new Face(loop, [], n.Normalized(), tag);
        }
        return new Solid(v, faces, tag);
    }
}
